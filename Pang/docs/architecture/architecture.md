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
