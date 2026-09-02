# Worker Traffic Coordination

This document describes the current worker movement conflict handling.

The current implementation has a `TrafficCoordinator` and a limited one-worker yield behavior.

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

Current non-scope:
- no group yield planning
- no protected-cell traffic plan
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
if yield cannot start and the blocker occupies A's final destination:
    wait and retry later
else if A has a future cell:
    request subpath to that future cell while avoiding the blocker
else:
    wait and retry later
```

Idle blockers yield first whether they occupy the destination or an intermediate path cell. If a yield cannot be started, the existing detour/wait fallback remains. Asynchronous yield/detour failure handling and alternate-destination selection are not extended by this rule.

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
2. If no yield cell exists for the low-priority route, try the high-priority route as fallback.
3. If neither route has a valid yield cell, keep both routes waiting and log the no-yield-space state.
```

This is intentionally not a full group yield system.

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

## 7 Clearing Priority Inheritance

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

Clearing priority inheritance remains a limited bridge toward future group yield behavior. It does not replace group yield planning.

---

## 8 Known Limits

The current coordinator can still fail or oscillate in layouts that require coordinated group movement.

Known limits:
- one-tile corridors may require several workers to move as a group
- detour paths may not exist
- wait chains that are not direct head-on conflicts remain wait chains
- no-yield-space is currently logged rather than surfaced to player UI
- traffic plans do not reserve future parking or protected cells

These limits are tracked in `docs/future/traffic_coordinator_yield.md`.
