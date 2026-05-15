# Universe Logistics - Agent Guide

## Project Identity

Universe Logistics is a logistics / warehouse automation management simulation game.

The core focus is:
- bottleneck management
- throughput optimization
- logistics flow stabilization
- human labor vs automation tradeoffs

This project prioritizes readable simulation and debuggable gameplay systems over strict realism or excessive abstraction.

---

## Core Rules

- Bottlenecks are the gameplay.
- Readability is more important than realism.
- Information is more important than hidden automation.
- Prefer explicit logic over hidden side effects.
- Preserve debuggability.
- Use existing systems and patterns before introducing new ones.
- Follow SOLID principles where they improve readability and maintainability.

---

## Implementation Philosophy

Prefer:
- small incremental changes
- clear responsibility boundaries
- service-oriented gameplay systems
- explicit ownership of important data
- simple extension points

Avoid:
- large rewrites
- unnecessary frameworks
- deep inheritance chains
- speculative architecture
- hidden gameplay calculations

If a class grows beyond its original responsibility, propose a refactor before expanding it further.

If a change requires a new manager, service, framework, or major architecture split, explain why before implementing it.

---

## Current Gameplay Loop

Inbound  
-> Storage  
-> Order  
-> Picking  
-> Packing  
-> Outbound  
-> Settlement

This current loop is the source of truth for implementation work.

---

## Current Architecture Summary

Major existing / expected systems:

- GameContext
- WorkerManager & Behavioral AI
- TaskManager
- GridService
- EconomyService
- Inbound / Outbound Workflow Managers
- ShelfBase hierarchy
- WorkPolicy System
- Contract / Order system
- Metrics & Simulation Time
- SelectionCard / DetailWindow UI pattern
- Service-based gameplay actions

For details, read `docs/architecture/architecture.md`.

---


## Do NOT

- Do not overengineer systems.
- Do not introduce unnecessary frameworks.
- Do not rewrite large systems without request.
- Do not hide money, reputation, or settlement changes.
- Do not add complex future systems just because they are mentioned in planning documents.
- Do not mix worker-specific logic such as fatigue or robot battery directly into generic task logic.
- Do not optimize before the behavior is correct and debuggable.

---

## Current vs Future

The `docs/current/` and `docs/architecture/` directories describe the active implementation direction of the project.

The `docs/future/` directory contains experimental or future ideas and should not be treated as required implementation targets unless explicitly requested.

---

## When Unsure

Prefer the smallest change that preserves:
- current gameplay flow
- readability
- debuggability
- future extension potential

Ask before making major architectural changes.

---

# Ownership Priority

Gameplay state should be modified through the owning system.

Examples:
- Economy -> EconomyService
- Grid -> GridService
- Task dispatch -> TaskManager

# Document Routing

Only read the documents relevant to the current task.

Avoid loading unrelated documents unless the task genuinely requires broader architectural context.

## Gameplay / Design Changes
Read:
- docs/project/identity.md
- docs/project/design_philosophy.md
- docs/current/gameplay_loop.md

## Architecture / System Changes
Read:
- docs/architecture/architecture.md
- relevant architecture documents

## Worker / Task Logic
Read:
- docs/architecture/workflow_task_worker.md
- docs/architecture/ownership_state_flow.md

## Grid / Placement / Pathfinding
Read:
- docs/architecture/spatial_grid.md
- docs/architecture/ownership_state_flow.md

## UI Changes
Read:
- docs/architecture/ui_interaction.md
- docs/technical/coding_rules.md

## General Coding
Read:
- docs/technical/coding_rules.md
- docs/technical/debugging_rules.md

Do not treat future ideas as current implementation requirements unless explicitly requested.

---
