# Workflow & Task Pipeline

The logistics simulation currently follows a layered structure:

Workflow
-> Task
-> Worker

---

## 1 Workflow Managers

Workflow managers monitor high-level logistics flow and generate tasks.

Examples:
- inbound logistics
- outbound logistics
- order fulfillment
- storage workflows

Workflow systems decide:
- what work should happen
- when work should happen
- which policy or strategy should be used

Workflow systems should not directly execute work themselves.

---

## 2 TaskManager

`TaskManager` owns:
- pending task queues
- task dispatch
- worker-task assignment

Tasks are assigned based on factors such as:
- availability
- distance
- worker ability
- task priority

Workflow systems generate tasks.
Workers execute assigned tasks.

For capsule routing, cargo-stage evaluation and FacilityRule destination queries are read-only policy operations. They do not create relocation tasks. The Capsule relocation workflow/coordinator consumes those results, owns matching and reservations, and submits the resulting Task through TaskManager.

Item transfer may update physical cargo and its PickingManifest in separate calls. A future relocation-dirty producer must therefore enqueue reevaluation after both updates are committed, rather than treating the intermediate quantity-change event as a stable cargo-stage snapshot.

---

## 3 Workers & AI

Workers currently share a common `AIWorker` foundation.

Both humans and robots use behavior-tree-driven task execution.

Worker differences are implemented through:
- abilities
- modifiers
- fatigue
- battery systems
- worker-specific logic

Human workers are affected by fatigue and incidents.

Robot workers are affected by battery and efficiency systems.

Worker abilities determine which tasks a worker can perform.

---

## 4 Worker Movement Traffic

Worker route execution is handled by `FindRoute`.

Movement conflicts are coordinated by `TrafficCoordinator` instead of being resolved locally inside each worker route.

Current flow:
- `FindRoute` attempts to reserve the next movement cell
- on reservation failure, `FindRoute` registers itself with `TrafficCoordinator`
- `TrafficCoordinator` decides whether the route should retry, wait, or request a detour
- `FindRoute` executes the resulting movement or subpath behavior

The current traffic system keeps yield planning out of worker task logic. Worker-specific systems such as fatigue or battery should not be mixed into traffic conflict rules.

See `worker_traffic_coordination.md` for details.
