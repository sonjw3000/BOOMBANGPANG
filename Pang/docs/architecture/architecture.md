# Architecture: Universe Logistics

## Overview

This document describes the current architecture and implementation direction of the project.

Universe Logistics currently uses a service-oriented simulation architecture centered around the `GameContext`.

The project prioritizes:
- readable simulation flow
- explicit ownership
- debuggability
- maintainable systems
- incremental extensibility

Future or post-demo ideas are documented separately and should not be treated as current implementation requirements.

---

# Core Architectural Direction

## 1 GameContext & Service Access

Major gameplay systems are registered and accessed through `GameContext`.

Examples:
- EconomyService
- GridService
- WorkerManager
- TaskManager
- TrafficCoordinator
- ItemDatabase
- Workflow managers

The project currently uses a centralized service access pattern to keep gameplay systems loosely coupled and easy to access.

Gameplay logic should generally flow through services rather than direct cross-system mutation.

---

## 2 Service-Oriented Gameplay

Gameplay state and simulation logic should not live inside UI code or unrelated MonoBehaviours.

Major gameplay actions should flow through the appropriate service or manager.

Examples:
- economy changes
- task creation
- placement
- logistics flow
- worker assignment

This helps keep the simulation traceable and debuggable.

---

## 3 Building, Facility Rule & Area Model

These concepts have separate owners and purposes.

- `Building` = what logistics function a space performs
- `FacilityRule` = how an installed facility should handle eligible work or items
- `Area` = an outdoor rectangular marker used only for worker spawning or rocket landing

Current design direction:
- `Building` is a logistics process node owned by the player
- buildings may represent functions such as `StorageBuilding`, `Packing`, `Staging`, and similar workflow-facing spaces
- facility operating policy is assigned through building-scoped `FacilityRule` presets
- `AreaType` contains only `WorkerSpawn` and `RocketLanding`
- areas are not owned by buildings and do not contain facilities or gameplay rules
- workflows query buildings and facilities first; areas are used only by their owning spawn/landing systems

---

# Simulation Direction

The simulation prioritizes:
- readability
- operational clarity
- bottleneck visibility
- stable flow
- maintainable structure

The architecture should support:
- throughput visualization
- bottleneck detection
- worker logistics
- automation expansion
- incremental feature growth

Avoid unnecessary abstraction or speculative architecture unless the project structure genuinely requires it.

## Related Documents

- Workflow & Workers: `workflow_task_worker.md`
- Worker Traffic Coordination: `worker_traffic_coordination.md`
- Spatial/Grid Structure: `spatial_grid.md`
- Data & Catalog Structure: `data_catalog.md`
- UI Interaction Pattern: `ui_interaction.md`
- Ownership & State Flow: `ownership_state_flow.md`
