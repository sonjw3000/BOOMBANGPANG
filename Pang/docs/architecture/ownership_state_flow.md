
#  Ownership & State Flow

Each major gameplay state should have a clear owner.

State changes should go through the owning service or manager instead of being modified directly by unrelated systems.

This rule exists to keep the simulation:
- traceable
- debuggable
- maintainable

---

## 1 EconomyService

EconomyService owns:
- money
- expenses
- revenue
- settlement-related economy flow

Economy-related changes should go through EconomyService APIs.

---

## 2 GridService

GridService owns:
- occupancy
- placement state
- movement-related spatial state

Grid state should not be modified directly by unrelated systems.

---

## 3 TaskManager

TaskManager owns:
- task queues
- task assignment
- worker dispatch

Workflow systems generate tasks.
Workers execute tasks.
Task assignment flows through TaskManager.

---

## 4 TrafficCoordinator

TrafficCoordinator owns:
- worker movement conflict registration
- traffic wait state
- retry and detour decisions for blocked movement
- head-on conflict resolution
- clearing priority inheritance for detouring routes

TrafficCoordinator does not own grid occupancy. Grid reservations remain owned by `GridService`.

TrafficCoordinator does not complete tasks. Worker task flow remains owned by workers and task systems.

---

## 5 Building

Building-related ownership should stay explicit.

Building systems should own:
- building identity
- building type and process role
- building-owned interior space
- building-level logistics purpose

Buildings define `what` the player built into the logistics network.

Examples:
- storage
- packing
- staging

Region classification such as indoor / outdoor should support placement and spatial reasoning, but should not replace building ownership as the source of truth for space identity.

---

## 6 FacilityRule & Area

`FacilityRuleManager` owns building-scoped rule presets and their facility assignments. Rules provide explicit logistics filters and operating policy without owning physical space.

`AreaManager` owns outdoor rectangular areas used by:
- `WorkerSpawnManager` for `WorkerSpawn` candidates
- inbound rocket flow for `RocketLanding` candidates

Areas do not belong to buildings, register facilities, or own logistics policy.

---

## 7 UI

UI does not own gameplay state.

UI should not:
- directly modify economy state
- directly assign tasks
- directly modify grid occupancy
- directly mutate logistics state

UI should request:
- building creation and building edits through the owning building system
- area creation and area edits through `AreaManager`
- facility-rule changes through `FacilityRuleManager`

---
