# Future: Yield Plans for Worker Traffic

This document describes future yield planning for worker movement.

The project now has a current `TrafficCoordinator`.

Current traffic behavior is documented in:

```text
docs/architecture/worker_traffic_coordination.md
```

This document should only be used for future work beyond the current coordinator.

---

## Current Baseline

The current implementation already covers:

- `FindRoute` reports blocked movement to `TrafficCoordinator`
- `TrafficCoordinator` owns traffic wait state
- blocked routes retry through coordinator update flow
- moving blockers cause waiting until cell release
- idle/static blockers can trigger detour requests
- direct head-on conflicts use priority comparison
- low-priority routes may request detours
- detouring routes inherit the priority of the route they are clearing for

The current implementation does not have yield plans.

---

## Future Goal

Yield should become an explicit traffic behavior.

Yield means:

```text
a worker temporarily moves to a valid clearing or parking cell
so a priority route or priority group can pass
```

Yield is not just a normal detour. A yield path's target is a traffic-clearing cell, not the next node of the worker's original route.

---

## Design Intent

Universe Logistics should treat movement congestion as visible logistics pressure, not hidden magic.

Future yield planning should:

- keep movement behavior readable
- make bottlenecks debuggable
- avoid atomic swaps
- avoid worker-local race conditions
- preserve `GridService` ownership of grid and reservation state
- keep `FindRoute` focused on route execution
- expose physically impossible traffic states as bottlenecks

The system should not try to make every warehouse layout automatically solvable.

A one-tile corridor with no passing space can become a real bottleneck. That should be visible to the player and debugger.

---

## Non-Goals

Future yield should not:

- teleport workers
- support atomic position swaps
- silently ignore reservations
- let workers push each other directly
- let each worker independently decide global traffic priority
- hide no-space failures by forcing arbitrary movement
- mix worker-specific systems such as fatigue or battery into traffic rules

---

## Why Current Detours Are Not Enough

The current coordinator can resolve simple head-on conflicts by making the lower-priority route request a detour.

This can still fail in one-tile corridors or chains.

Example:

```text
empty = O
blocked = X

O X X X X X X
O O A B C D O
O X X X X X X
```

If `A` and `B` are moving right, while `C` and `D` are moving left, a single low-priority detour may not be enough.

The correct future unit of resolution is not only:

```text
B versus C
```

It may be:

```text
a traffic plan for the conflicting corridor segment
```

---

## Route Intent

Future path requests should make route intent explicit.

```csharp
public enum RouteIntent
{
	Normal,
	Detour,
	Yield,
}
```

### Normal

The worker is moving toward its gameplay goal.

### Detour

The worker is temporarily avoiding a blocking route and intends to rejoin its original path.

### Yield

The worker is temporarily moving out of a priority route's way.

On yield arrival:

```text
wait until the traffic plan releases it
then resume the original goal
```

---

## Yield Request

A yield request represents a priority route asking lower-priority workers to clear space.

Example:

```csharp
public sealed class YieldRequest
{
	public int RequestId;
	public FindRoute PriorityRoute;
	public int3 PriorityDirection;
	public HashSet<int3> ProtectedCells;
	public HashSet<FindRoute> Participants;
	public int Depth;
}
```

Important rule:

When a yielding worker is blocked by another worker, the blocker should not start a new independent head-on decision.

Instead, the existing request should expand.

```text
C has priority
B is yielding for C
B is blocked by A
A joins C's YieldRequest
```

The comparison should be:

```text
Does the original priority route outrank this blocker?
```

not:

```text
Does B outrank A?
```

The current `TrafficCoordinator` already has a small bridge toward this through clearing priority inheritance. Future yield should replace that bridge with explicit plan ownership.

---

## Yield Plan

A `YieldPlan` is the committed version of a yield request.

Example:

```csharp
public sealed class YieldPlan
{
	public int PlanId;
	public FindRoute PriorityRoute;
	public List<FindRoute> PriorityFollowers;
	public List<FindRoute> Participants;
	public HashSet<int3> ProtectedCells;
	public Dictionary<FindRoute, int3> ParkingTargets;
	public List<FindRoute> ExecutionOrder;
	public float CreatedAtTime;
	public float TimeoutSeconds;
	public int Version;
}
```

Definitions:

- `PriorityRoute`: the route that won traffic priority
- `PriorityFollowers`: same-direction routes that should pass with the priority route when safe
- `Participants`: lower-priority routes that must clear the protected cells
- `ProtectedCells`: cells that must not be used as final yield parking cells
- `ParkingTargets`: assigned yield destinations for participants
- `ExecutionOrder`: the order in which participants should move
- `Version`: used to reject stale route commands

---

## Protected Cells

Protected cells are the route corridor that must be cleared for the priority flow.

They should include:

- the immediate blocked cell
- the priority route's near-future path through the conflict
- cells occupied by lower-priority workers that must be cleared
- corridor cells needed by priority followers

Yield parking targets must not be inside protected cells.

---

## Group Yield Planning

The coordinator should plan a group yield in one deterministic operation.

Suggested flow:

```text
1. Detect a priority conflict.
2. Determine the priority route.
3. Build protected cells from the priority route and nearby same-direction followers.
4. Scan the opposing direction for blocking participants.
5. Search for available parking targets outside protected cells.
6. Assign unique targets to participants.
7. Commit the plan or fail explicitly.
8. Dispatch yield commands in execution order.
```

The coordinator should not rely on worker `Update()` ordering to discover the chain gradually.

---

## One-Tile Corridor Behavior

In a one-tile corridor, an immediate adjacent yield cell may not exist.

This can require a worker to retreat several cells to reach:

- a branch
- a bay
- an exit
- another valid parking cell

Long retreats are valid but should be bounded.

Suggested tuning values:

```csharp
[SerializeField] private int maxYieldDepth = 12;
[SerializeField] private int maxYieldParticipants = 8;
[SerializeField] private float yieldPlanTimeout = 5f;
```

---

## No-Yield-Space State

If no valid yield plan exists, the system should report an explicit state.

Example:

```csharp
public enum TrafficFailureReason
{
	NoYieldSpace,
	YieldDepthExceeded,
	TooManyParticipants,
	ReservationConflict,
	PlanTimeout,
	StalePlan,
}
```

This state should be visible through logs and later UI/debug overlays.

The game should treat this as a real bottleneck, not as a hidden pathfinding bug.

---

## Plan Reservation

Future yield plans need planning constraints that are separate from current one-step movement reservations.

Possible approach:

```text
GridService actual reservation:
used for current and next movement tiles

TrafficCoordinator plan lock:
used for future yield parking targets and protected cells
```

Plan locks are not the same as current tile reservations. They prevent two yield plans from selecting the same future cells.

---

## Debugging Requirements

Future yield debugging should expose:

- active plan id
- priority route
- priority owner
- participants
- parking targets
- protected cells
- execution order
- failure reason
- timeout remaining
- route intent per worker

Useful overlays:

- protected cells
- assigned parking cells
- active yield participants
- blocked reports
- no-yield-space bottleneck markers

---

## Future Implementation Path

Recommended next stages:

1. Add explicit `RouteIntent` to path requests and results.
2. Add plan id/version validation for traffic commands.
3. Add one-worker `Yield` intent with a real parking cell.
4. Replace clearing priority inheritance with explicit yield request ownership.
5. Add group yield planning for one-tile corridors.
6. Add long retreat limits and explicit failure states.
7. Add debug overlay and bottleneck reporting.

Each stage should keep current movement behavior debuggable and avoid large rewrites.

---

## Open Questions

- Should same-direction followers be included automatically, or only after they become blocked?
- Should long retreat limits be based on tile count, estimated time, or participant count?
- Should priority consider task type, carried item value, or settlement urgency?
- Should no-yield-space become a player-facing warning immediately, or remain debug-only first?
- Should traffic plan locks live inside `TrafficCoordinator` or be exposed through `GridService`?

