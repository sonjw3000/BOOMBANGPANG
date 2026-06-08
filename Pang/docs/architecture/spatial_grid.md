
#  Spatial & Logistics Structure

## 1 GridService & GridMap

The logistics hub simulation is built around a multi-layered spatial grid system.

`GridService` manages:
- occupancy
- placement validation
- movement-related grid state
- pathfinding-related spatial queries
- current movement reservations

`GridMap` represents the physical warehouse layout and node state.

Worker traffic decisions are handled by `TrafficCoordinator`, but reservation ownership stays in `GridService`.

---

## 2 Footprint & Interaction Points

Buildings and placeables use a footprint-based structure.

The footprint system supports:
- multi-tile placement
- rotation
- interaction areas
- specialized building footprints
- interior facility placement within buildings

Interaction points define where workers can:
- pick
- put
- work
- interact with structures

---

## 3 ShelfBase Hierarchy

Storage-related entities share a common `ShelfBase` hierarchy.

Examples:
- shelves
- cargo ports
- launch pads
- storage-like logistics entities

This provides a consistent interface for:
- item storage
- item queries
- logistics interaction
- workflow integration

Cargo ports should increasingly be treated as logistics interfaces between:
- external inbound/outbound transport
- building-to-building transfer flow
- storage and packing side workflow handoffs

---

## 4 Building / Facility / Zone Interpretation

Spatial logic should not treat every operational concept as the same kind of placeable.

Current direction:
- `Building` = physical hub space and footprint
- `Facility` = installed logistics or support function inside a building
- `Zone` = policy or handling rule applied to an area or workflow

This distinction should guide future placement, interaction, and refactoring work.

---


