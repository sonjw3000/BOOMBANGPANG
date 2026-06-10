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

## 3 Building & Zone Model

`Building` and `Zone` serve different gameplay purposes and should not be merged conceptually.

- `Building` = what logistics function a space performs
- `Zone` = how a part of that space should be operated

Current design direction:
- `Building` is a logistics process node owned by the player
- buildings may represent functions such as `StorageBuilding`, `Packing`, `Staging`, and similar workflow-facing spaces
- players build a logistics network by connecting buildings and managing distance, throughput, specialization, and operational risk
- a building may contain multiple zones
- zones exist only inside a building and should not exist as free-floating global space rules

`Zone` is not a process node.

`Zone` is a rule layer applied to part of a building interior.

Examples of zone concerns:
- item filters
- worker filters
- handling rules
- specialization rules
- risk and compliance restrictions

Examples:
- `Contains Fragile`
- `Contains Hazard`
- `Under 0 Celsius`
- `SATP`
- `Only(WorkerType)`

This distinction should guide future system design:
- buildings define logistics purpose and network structure
- zones define local operating policy inside a building
- workflow routing should reason about buildings first, then zone rules
- zone rules must not silently replace the role of buildings as logistics process owners

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
