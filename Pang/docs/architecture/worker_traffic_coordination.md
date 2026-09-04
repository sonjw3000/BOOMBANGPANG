# Worker Traffic Coordination

This document describes the current worker movement conflict handling.

The current implementation has a `TrafficCoordinator`, one-worker yield, and a bounded linear-chain clearing fallback.

---

## 1 Current Scope

`TrafficCoordinator` currently centralizes worker movement conflict decisions that used to live inside `FindRoute`.

Current behavior:
- `FindRoute` detects failed next-cell reservation
- `FindRoute` registers itself with `TrafficCoordinator`
- `TrafficCoordinator` resolves blocked movement during its own `Update`
- workers keep their current grid reservation while waiting
- blocked routes may retry, wait, or request a detour
- head-on conflicts use priority comparison, detour fallback, and one-worker yield fallback
- a bounded clearing plan can move a short linear blocker chain out of a priority route

Current non-scope:
- no unbounded or global crowd movement planning
- no branching or cyclic clearing plan
- no no-yield-space player warning

---

## 2 Responsibility Split

### GridService

`GridService` remains the owner of grid and reservation state.

It owns:
- current occupancy
- current tile reservations
- static obstacle checks
- reservation commit and release
- lookup of the `FindRoute` reserving a cell

`TrafficCoordinator` queries `GridService`; it does not own grid state.

### FindRoute

`FindRoute` is the route executor.

It owns:
- path requests
- next-tile reservation attempts
- transform movement
- committed worker grid position updates
- subpath execution

When reservation fails, `FindRoute` reports itself to `TrafficCoordinator` instead of deciding the conflict locally.

Traffic-facing route state exposed by `FindRoute`:
- `TrafficFromCell`: the worker's committed grid cell
- `TryGetTrafficToCell(...)`: the current traffic target cell
- `TryGetFutureToCell(...)`: the next cell after the current traffic target, when available
- `CollectUpcomingTrafficCells(...)`: bounded, ordered upcoming cells, including subpath continuation

`PathResultBuffer` merges a detour at its first intersection with the immediate parent buffer's remaining path, including the parent's current target. It trims the detour after that intersection and moves the parent's node cursor and index to the same cell. The detour visits the intersection; its completion resumes at the parent's following node. Nested detours apply the same rule to their immediate parent, and next-cell lookup continues through exhausted parent buffers. This preserves continuation without replaying the skipped parent segment.

### TrafficCoordinator

`TrafficCoordinator` owns current traffic conflict decisions.

It owns:
- blocked route registration
- traffic wait state
- retry queueing
- wait-cell unreserve subscriptions
- static/idle blocker handling
- moving blocker wait handling
- head-on conflict detection
- detour priority comparison
- one-worker yield hold/release
- clearing priority inheritance
- bounded clearing-plan construction, execution, cancellation, and release

It does not move transforms or complete worker tasks.

---

## 3 Traffic Edge Model

Current conflict handling treats each route as a traffic edge:

```text
TrafficFromCell -> TrafficToCell
```

For a blocked or waiting route:

```text
TrafficFromCell = worker.GridPosition
TrafficToCell = cell the worker wants to reserve next
```

For a moving route:

```text
TrafficFromCell = committed source cell
TrafficToCell = reserved destination cell
```

The coordinator uses `GridService.GetReservedFindRoute(TrafficToCell)` to identify the blocking route at resolution time.

---

## 4 Current Resolution Rules

When route `A` cannot reserve `A.TrafficToCell`:

### No Blocking Route

If no route reserves the desired cell, the coordinator distinguishes static blockage from a reservation race.

```text
if the desired cell is statically blocked:
    request a fresh route to the current goal
else:
    resume and retry movement
```

This handles cases where a structure was placed on the old path after the route was created.

### Static Or Idle Blocker

If the blocker has no traffic target, is idle, arrived, failed, or inactive outside coordinator traffic wait:

```text
if the blocker is idle and both workers can yield:
    try moving the blocker to an available adjacent cell
    if yield starts, A waits for the blocked cell to be released
if the idle blocker cannot move to one free adjacent cell:
    try a bounded clearing plan that may include the idle blocker and waiting workers behind it
if yield cannot start and the blocker occupies A's final destination:
    wait and retry later
else if A has a future cell:
    request subpath to that future cell while avoiding the blocker
else:
    wait and retry later
```

Idle blockers yield first whether they occupy the destination or an intermediate path cell. If a one-cell yield cannot start, the coordinator tries a complete bounded clearing plan before the existing detour/wait fallback. A genuinely idle participant is held outside task dispatch during that plan and returns to Idle without receiving a synthetic work goal. Manual workers may own the passing route, but manual workers are never commandeered as clearing participants.

Idle yield candidates exclude the requester's current cell, current traffic target, and the following path cell returned by `TryGetFutureToCell`. If there is no following path cell, the existing adjacent-cell checks apply without that additional exclusion.

### Moving Blocker

If the blocker is moving and is not waiting for traffic:

```text
A waits for the desired cell to be unreserved
```

The coordinator listens for the waited cell's unreserve event and queues a retry.

### Waiting Blocker

If the blocker is already registered in `TrafficCoordinator` wait state, the coordinator checks for head-on conflict:

```text
A.TrafficToCell == B.TrafficFromCell
B.TrafficToCell == A.TrafficFromCell
```

If this is not true, the situation is treated as a wait chain for now.

---

## 5 Head-On Conflict

Current head-on resolution first tries the pre-yield behavior:

```text
high-priority route waits
low-priority route requests a detour/subpath
```

Priority comparison uses `WorkPolicyService.IsTargetHigherPriority(...)`.

If the low-priority route cannot detour because there is no future cell, the coordinator tries one-worker yield.

One-worker yield:

```text
1. Try to move the low-priority route one cell away from the priority route.
2. If the low-priority route has no valid one-cell yield, try a bounded clearing plan for the high-priority route.
3. If no clearing plan exists, try the high-priority route as the existing yield fallback.
4. If neither route has a valid yield cell, keep both routes waiting and log the no-yield-space state.
```

The clearing fallback is intentionally bounded and does not act as a global crowd movement system.

---

## 6 One-Worker Yield

One-worker yield is a minimal head-on fallback.

The yielding route moves to a single adjacent yield cell opposite the conflicting route.

Yield cell requirements:
- the cell is in bounds
- the cell is not statically blocked
- the cell is not currently reserved by another route
- the cell is not already reserved as another yield target
- the cell is not the priority route's current cell
- the cell is not the priority route's current traffic target cell
- the cell is not the priority route's following path cell, when `TryGetFutureToCell` provides one

The coordinator stores a yield hold:

```text
priority route
yielding route
yield cell
yielding route original cell
yielding route original goal
```

When the yielding route reaches the yield cell:

```text
FindRoute disables itself
AIWorker remains disabled
TrafficCoordinator holds the route
```

The yielding route is released only after:

```text
the priority route enters the yielding route's original cell
and later that original cell becomes unreserved
```

On release, the yielding route requests a fresh route to its original goal.

If an active yield hold is found to be invalid, for example the yield cell is also the priority route's current traffic target cell, the coordinator clears the hold and requests fresh routes for both participants. This is a recovery guard for stale or bad yield decisions, not the normal yield path.

---

## 7 Bounded Clearing Plan

When a low-priority worker cannot perform a one-cell yield, the coordinator may build a complete local plan before moving anyone.

The default bounds are:

- at most four participating blockers
- a local Manhattan search radius of six cells
- at most ten atomic clearing moves
- up to twelve upcoming cells protected from final parking
- at most 4096 search states; exhaustion leaves the existing wait/yield fallback in place

The planner does not classify the map as a corridor. It follows the line behind the first blocker, then searches bounded local worker-position states for a result where every participant finishes outside the passing route's protected cells. Static obstacles, unrelated worker occupancy, existing reservations, regions, robot navigation coverage, and reserved yield cells remain unavailable during the search. In the head-on flow, participants must already be waiting for traffic with a preserved goal. In the idle-blocker flow, a participant may instead be genuinely idle with no Task or recovery reservation. Every participant must be at a committed cell, operational, and outside manual control or another clearing operation. Moving workers are not commandeered.

Plan ownership separates:

- `PriorityOwner`: the route whose inherited priority is used for later traffic comparisons
- `PassingRoute`: the route that must physically traverse the blocked cells
- `Participants`: the routes temporarily moved out of the protected path

All atomic moves are known before the plan starts. `FindRoute.RequestClearingStep(...)` reuses the existing yield executor with path traversal restricted to that step's source and destination. Intermediate moves may cross a protected cell while the passing route is suspended, but every participant's final holding cell must be outside the protected path.

The coordinator logically reserves participant origins, planned move cells, and the passage through its release cell. Both `FindRoute` next-step reservation and the traffic retry resolver check this ownership. Only the active participant may execute the current clearing step; only the passing route may use the passage after it opens. Actual per-step reservations and occupancy still belong to `GridService`. Stale retry queue entries cannot restart held participants or suspend the active clearing step.

The passing route resumes only after all clearing moves arrive. Participants remain held until the passing route reaches the first protected cell after all participant origin cells. Participants with work request fresh routes to their preserved original goals, rather than being teleported or guaranteed to retrace their original cells. Participants that entered as genuinely idle return to Idle at their cleared cell and re-enter worker dispatch availability. A rejected move, failed path, cancelled or changed route, invalid worker, or timeout aborts the whole plan and releases its temporary traffic ownership and cell reservations. If an operational participant is already between reserved cells, coordinator-triggered abort waits for that step to arrive before releasing the plan. The default timeout is thirty simulation seconds.

---

## 8 Clearing Priority Inheritance

When a low-priority route detours because a high-priority route won a head-on conflict, the detouring route inherits the high-priority route's traffic priority for later conflicts.

Example:

```text
A beats B
B detours to clear A
B later conflicts with C
```

The next comparison should be:

```text
A versus C
```

not:

```text
B versus C
```

`TrafficCoordinator` tracks this with a clearing-owner map. When the subpath avoid target is cleared, `FindRoute.RemoveBlocked(...)` notifies `TrafficCoordinator` so the inherited priority can be removed.

Yield-held routes are treated as traffic-controlled routes, not static blockers. A yield route that has not completed its hold is considered higher priority than normal traffic so that it can finish clearing or return safely.

Clearing priority inheritance also applies to bounded-plan participants. It remains priority propagation rather than movement planning itself.

---

## 9 Known Limits

The current coordinator can still fail or oscillate in layouts that require coordinated group movement.

Known limits:
- clearing plans handle only short linear blocker chains; branching or cyclic worker dependencies still wait
- detour paths may not exist
- wait chains that are not direct head-on conflicts remain wait chains
- no-yield-space is currently logged rather than surfaced to player UI
- clearing plans reserve their local move cells inside `TrafficCoordinator`, not as long-range `GridService` movement reservations

These limits are tracked in `docs/future/traffic_coordinator_yield.md`.
