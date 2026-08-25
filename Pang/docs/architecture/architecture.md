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

- `Building` = player-owned logistics scope and building-level operating policy
- `FacilityRule` = how an installed facility should handle eligible work or items
- `Area` = an outdoor rectangular marker used only for worker spawning or rocket landing

Target design direction:
- one Building may contain facilities for labeling, storage, picking, packing, and outbound work at the same time
- installed facilities and their Rules determine which work can happen; every player-created structure uses the same `Building` class
- facility operating policy is assigned through building-scoped `FacilityRule` presets
- Building stores the cargo process stage at which its capsules should eventually become outbound candidates
- `AreaType` contains only `WorkerSpawn` and `RocketLanding`
- areas are not owned by buildings and do not contain facilities or gameplay rules
- workflows query buildings and facilities first; areas are used only by their owning spawn/landing systems

Rule-driven Capsule routing, Dirty reevaluation, lifecycle normalization, and relocation Task creation are owned by `CapsuleRelocateCoordinator`. Labeling and storing producers are owned by `InboundWorkflowService`, while picking, packing, and launch producers are owned by `OutboundWorkflowService`; any registered Building may host those operations when its facilities and Rules match.

Cargo process stages use one shared contract:

`None -> Unlabeled -> Labeled -> Picked -> Packed -> LaunchReady`

- `None` means no process-stage restriction for a FacilityRule and disables automatic outbound promotion when used as a Building policy
- the stage is derived from actual ItemStatus and PickingManifest data; it is not duplicated onto ItemStack or CargoCapsule
- `LaunchReady` is a Launch-context evaluation of otherwise Packed cargo using the existing outbound blocking and complete-manifest checks; callers must request that context explicitly
- stage matching is exact and never uses enum numeric ordering
- `Empty` is a Capsule lifecycle state, not a cargo process stage

The runtime Capsule lifecycle contract is `IB / Inside / Empty / OB`.

- `IB` and `OB` describe CargoPort-facing transport phases.
- `Inside` and `Empty` describe CapsuleBuffer payload phases.
- `CapsuleDockState` remains a separate facility-interface contract, so Dock roles such as `IBStandby` and `OBStandby` are not Capsule lifecycle states.
- Task implementations change physical cargo, ItemStatus, or manifests. Dirty routing evaluation derives the lifecycle of docked standard Capsules instead of each work Task assigning it independently. A relocation Task may set a carried rejected outbound Capsule back to `Inside` before it can be redocked and evaluated.

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
