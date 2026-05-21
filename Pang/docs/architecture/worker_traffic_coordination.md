# Worker Traffic Coordination

This document describes the current worker movement conflict handling.

The current implementation has a `TrafficCoordinator`, but it does not implement yield plans yet.

---

## 1 Current Scope

`TrafficCoordinator` currently centralizes worker movement conflict decisions that used to live inside `FindRoute`.

Current behavior:
- `FindRoute` detects failed next-cell reservation
- `FindRoute` registers itself with `TrafficCoordinator`
- `TrafficCoordinator` resolves blocked movement during its own `Update`
- workers keep their current grid reservation while waiting
- blocked routes may retry, wait, or request a detour
- head-on conflicts use priority comparison and detour fallback

Current non-scope:
- no yield intent
- no parking cell assignment
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
if A has a future cell:
    request subpath to that future cell while avoiding the blocker
else:
    wait and retry later
```

This keeps destination-cell blockage as a wait state instead of forcing arbitrary movement.

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

Current head-on resolution still uses the pre-yield behavior:

```text
high-priority route waits
low-priority route requests a detour/subpath
```

Priority comparison uses `WorkPolicyService.IsTargetHigherPriority(...)`.

This is intentionally not a full yield system. The low-priority route is not assigned a parking cell. It only tries to route around the blocker using the existing subpath behavior.

---

## 6 Clearing Priority Inheritance

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

This is a limited bridge toward future yield behavior. It does not replace group yield planning.

---

## 7 Known Limits

The current coordinator can still fail or oscillate in layouts that require coordinated group movement.

Known limits:
- one-tile corridors may require several workers to move as a group
- detour paths may not exist
- wait chains that are not direct head-on conflicts remain wait chains
- no explicit no-yield-space state exists yet
- traffic plans do not reserve future parking or protected cells

These limits are tracked in `docs/future/traffic_coordinator_yield.md`.
