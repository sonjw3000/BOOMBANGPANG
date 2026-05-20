# Traffic Coordinator & Yield Logic

This document describes a future traffic coordination system for worker movement.

It is not a current implementation requirement. It records the intended design direction for resolving worker-to-worker movement deadlocks without atomic swaps.

## Design Intent

Universe Logistics should treat movement congestion as visible logistics pressure, not as hidden magic.

The traffic system should:

- keep movement behavior readable
- make bottlenecks debuggable
- avoid worker-local race conditions
- avoid atomic position swaps
- preserve `GridService` ownership of grid and reservation state
- keep `FindRoute` focused on route execution
- expose physically impossible traffic states as bottlenecks

The system should not try to make every warehouse layout automatically solvable. A one-tile corridor with no passing space can become a real bottleneck, and that should be visible to the player and debugger.

## Problem Summary

The current `FindRoute` logic can detect that a worker is blocked by another reserved tile.

However, worker-local decision making becomes unsafe when multiple workers are involved:

- worker `Update()` order is not deterministic
- two workers can observe different traffic states in the same frame
- local deadlock handling can create repeated wait states
- local yield propagation can oscillate
- two workers can choose the same escape tile
- a long one-tile corridor may require several workers to move as a group

Example:

```text
empty = O
blocked = X

O X X X X X X
O O A B C D O
O X X X X X X
```

If `A` and `B` are moving right, while `C` and `D` are moving left, and `C` has priority, then `B` cannot solve the conflict alone.

`B` must retreat left. If `A` blocks that retreat, `A` must also participate in the same yield request. The correct unit of resolution is not only `B` versus `C`; it is a traffic plan for the conflicting corridor segment.

## Non-Goals

The future traffic system should not:

- support atomic swaps
- teleport workers
- silently ignore reservations
- let workers push each other directly
- let each worker independently decide global traffic priority
- hide no-space failures by forcing arbitrary movement
- mix worker-specific systems such as fatigue or battery into traffic rules

## Responsibility Split

### GridService

`GridService` remains the owner of grid state.

It should own:

- current grid occupancy
- current tile reservations
- passability queries
- static obstacle checks
- reservation commit and release
- planned-path congestion registration

Future helper queries may be added here if they are pure spatial queries.

Examples:

- `IsCellPassableForRoute(...)`
- `TryReserveTrafficPlan(...)`
- `ReleaseTrafficPlan(...)`
- `FindCandidateYieldCells(...)`

### FindRoute

`FindRoute` should become the route executor.

It should own:

- requesting pathfinding for a specific target
- reserving the next movement tile
- moving the transform
- updating the worker grid position after movement succeeds
- reporting blocked movement to `TrafficCoordinator`
- executing traffic commands issued by `TrafficCoordinator`

`FindRoute` should not own global traffic decisions.

It should not decide:

- which worker has corridor priority
- which group must yield
- which escape cells are assigned to which workers
- whether a long corridor yield is allowed
- whether a conflict is impossible to solve

### TrafficCoordinator

`TrafficCoordinator` is the central decision maker for movement conflicts.

It should own:

- blocked report collection
- deterministic conflict processing
- head-on deadlock detection
- priority comparison
- yield group planning
- protected route selection
- yield parking target assignment
- plan-level reservation or locking
- traffic command dispatch
- timeout and failure handling
- debug reporting for bottleneck states

`TrafficCoordinator` should not move transforms or complete worker tasks.

## High-Level Flow

When `FindRoute` cannot reserve its next tile:

```text
FindRoute
-> stops route execution
-> keeps its current tile reserved
-> releases any failed next-tile reservation if needed
-> reports a TrafficBlockReport
-> waits for a TrafficCommand
```

Then:

```text
TrafficCoordinator
-> processes blocked reports in deterministic order
-> takes a grid/reservation snapshot
-> decides Wait, Retry, Detour, Yield, or Failure
-> issues commands to affected FindRoute instances
```

Finally:

```text
FindRoute
-> executes the received command
-> reports completion, failure, or new blockage
```

## Traffic Reports

A blocked route should report facts, not make decisions.

Example structure:

```csharp
public readonly struct TrafficBlockReport
{
	public readonly FindRoute Route;
	public readonly int3 CurrentCell;
	public readonly int3 BlockedCell;
	public readonly int3 GoalCell;
	public readonly FindRoute BlockedBy;
	public readonly RouteIntent Intent;
	public readonly int ActivePlanId;
}
```

The report should mean:

```text
This route tried to enter BlockedCell from CurrentCell, but could not reserve it.
```

It should not mean:

```text
This route has decided who should yield.
```

## Traffic Commands

`TrafficCoordinator` should answer reports by issuing explicit commands.

Example:

```csharp
public enum TrafficCommandType
{
	Wait,
	RetryReserve,
	RequestDetour,
	RequestYieldMove,
	ResumeOriginalGoal,
	FailNoYieldSpace,
	CancelTrafficPlan,
}

public readonly struct TrafficCommand
{
	public readonly TrafficCommandType Type;
	public readonly int PlanId;
	public readonly int3 TargetCell;
	public readonly FindRoute AvoidRoute;
	public readonly RouteIntent Intent;
}
```

`FindRoute` should execute commands without re-deciding their policy.

## Route Intent

The pathfinding algorithm can be shared, but the meaning of a path must be explicit.

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

On arrival:

```text
continue task flow
```

### Detour

The worker is temporarily avoiding a blocking route and intends to rejoin its original path.

On arrival:

```text
merge back into existing path if still valid
otherwise request a fresh route to the original goal
```

### Yield

The worker is temporarily moving out of a priority route's way.

On arrival:

```text
wait until the traffic plan releases it
then resume the original goal
```

Yield is not just a detour. A yield path's target is an escape or parking cell, not the next node of the original route.

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

When a yielding worker is blocked by another worker, the blocker should not start a new independent head-on deadlock decision.

Instead, the existing request expands:

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

Not:

```text
Does B outrank A?
```

This keeps the traffic decision stable across a corridor chain.

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

## Protected Cells

Protected cells are the route corridor that must be cleared for the priority flow.

They should include:

- the immediate blocked cell
- the priority route's near-future path through the conflict
- cells occupied by lower-priority workers that must be cleared
- corridor cells needed by priority followers

Yield parking targets must not be inside protected cells.

For the example:

```text
O X X X X X X
O O A B C D O
O X X X X X X
```

If `C` and `D` must exit left, the center corridor cells occupied by `A` and `B` are protected until `C` and `D` have passed.

The left-side branch cells are candidate parking cells, but the corridor entrance itself should usually remain pass-through space rather than final parking.

## Yield Group Planning

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

The important part is that the coordinator calculates the plan as a whole. It should not rely on worker `Update()` ordering to discover the chain gradually.

## One-Tile Corridor Behavior

In a one-tile corridor, an immediate adjacent yield cell may not exist.

This is valid:

```text
worker must retreat several cells to reach a branch, bay, or exit
```

The coordinator should support long retreats if a valid parking target exists within limits.

Example:

```text
Entrance ... A B C D ... Exit
```

If `C` has priority and moves left:

```text
B must yield left
A may also need to yield left
A should move first
B should move after A clears space
C and D pass after protected cells are clear
```

Execution order should usually start with the participant closest to the available parking area or farthest from the priority route, because that creates space for the next participant.

## Long Retreat Limits

Long retreats are logically valid but should be bounded.

Suggested tuning values:

```csharp
[SerializeField] private int maxYieldDepth = 12;
[SerializeField] private int maxYieldParticipants = 8;
[SerializeField] private float yieldPlanTimeout = 5f;
```

If a valid parking target is too far away, the coordinator may choose to fail the plan and expose the bottleneck.

Failure is acceptable when:

- no parking target exists
- all parking targets are inside protected cells
- the required participant count is too high
- the retreat distance exceeds the configured limit
- another committed plan already owns the necessary space
- the plan times out

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

## Determinism & Safety

The coordinator must be deterministic.

Blocked reports should be processed in a stable order.

Suggested ordering:

```text
1. emergency or incident-handling routes
2. express or urgent task routes
3. shorter remaining distance
4. lower WorkerID
5. lower RequestId
```

The exact priority rule should reuse or align with `WorkPolicyService.IsTargetHigherPriority(...)` where possible.

Safety rules:

- a worker can belong to only one active traffic plan at a time
- a parking target can be assigned to only one worker
- protected cells cannot be parking targets
- stale plan commands must be ignored
- timeout should release plan locks
- current worker cells remain reserved while waiting
- next cells should only be reserved through normal movement or committed plan rules

## Plan Reservation

The coordinator should prevent two yield plans from selecting the same cells.

Possible future approach:

```text
GridService actual reservation:
used for current and next movement tiles

TrafficCoordinator plan lock:
used for future yield parking targets and protected cells
```

Plan locks are not the same as current tile reservations. They are planning constraints that prevent conflicting yield plans.

This keeps movement execution compatible with the existing one-step reservation model while allowing group planning.

## Head-On Deadlock

A head-on deadlock occurs when two routes want each other's current cell.

Example:

```text
A wants B's current cell
B wants A's current cell
```

Because atomic swap is unsupported, one route must yield.

Future handling:

```text
1. Detect mutual cell dependency.
2. Select priority route.
3. Create a YieldRequest for the lower-priority route.
4. Search for a yield parking target outside the priority route.
5. If blocked by a chain, create a group YieldPlan.
6. If no plan exists, report NoYieldSpace.
```

## Same-Direction Followers

When a priority route wins in a corridor, same-direction followers may need to pass as part of the same traffic flow.

Example:

```text
A B -> moving right
C D -> moving left
C has priority
```

If `C` passes left but `D` remains blocked behind the conflict, the coordinator may immediately recreate a similar conflict.

A future `YieldPlan` may include `D` as a priority follower so the opposing group clears enough space for both `C` and `D` to exit the corridor.

This should be conservative. Followers should be included only when they are close, aligned, and part of the same corridor conflict.

## Coordinator Tick Model

The coordinator should avoid immediate recursive calls between workers.

Preferred model:

```text
FindRoute reports blocked
TrafficCoordinator queues report
TrafficCoordinator processes queued reports during its own tick
TrafficCoordinator dispatches commands
FindRoute executes commands on later updates
```

This prevents:

- recursive yield propagation
- partial decisions based on update order
- worker A seeing a plan before worker B has joined it
- duplicated requests in the same frame

## Debugging Requirements

Future debugging should make traffic plans inspectable.

Useful debug data:

- active plan id
- priority route
- participants
- parking targets
- protected cells
- execution order
- failure reason
- timeout remaining
- route intent per worker

Useful visual overlays:

- protected cells
- assigned parking cells
- active yield participants
- blocked reports
- no-yield-space bottleneck markers

Logs should include enough information to reproduce a traffic decision:

```text
plan id
priority worker id
participant worker ids
protected cells
parking targets
failure reason
```

## Incremental Implementation Path

Recommended implementation stages:

1. Add `RouteIntent` to route requests and results.
2. Add `TrafficBlockReport` and make `FindRoute` report blocked movement.
3. Add `TrafficCoordinator` with `Wait`, `RetryReserve`, and `RequestDetour` commands only.
4. Move existing head-on priority logic out of `FindRoute` and into `TrafficCoordinator`.
5. Add one-worker `Yield` intent with a real yield parking cell.
6. Add plan id/version validation.
7. Add group yield planning for one-tile corridors.
8. Add long retreat limits and explicit failure states.
9. Add debug overlay and bottleneck reporting.

Each stage should keep current movement behavior debuggable and avoid large rewrites.

## Open Questions

- Should same-direction followers be included automatically, or only after they become blocked?
- Should long retreat limits be based on tile count, estimated time, or participant count?
- Should priority consider task type, carried item value, or settlement urgency?
- Should no-yield-space become a player-facing warning immediately, or remain debug-only first?
- Should traffic plan locks live inside `TrafficCoordinator` or be exposed through `GridService`?

## Summary

The future traffic system should centralize movement conflict decisions in `TrafficCoordinator`.

`FindRoute` should report blockage and execute commands.

`GridService` should remain the owner of grid and reservation state.

Yield should be treated as a distinct route intent with a real parking target, not as a normal detour to the next original path node.

One-tile corridor conflicts should be resolved with deterministic group `YieldPlan`s when valid yield space exists. When no valid space exists, the system should expose an explicit bottleneck instead of hiding the failure.
