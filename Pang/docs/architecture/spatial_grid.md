
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

Current direction for building space:
- a building owns a set of interior cells
- worker spawn and rocket landing areas are defined only on outdoor cells
- region calculation is primarily spatial classification such as indoor / outdoor / boundary
- region classification should not become the source of truth for building ownership
- future building shapes are expected to expand beyond simple rectangles, so ownership logic should increasingly be based on owned cell sets rather than rectangle-only assumptions

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

## 4 Building / Facility / Area Interpretation

Spatial logic should not treat every operational concept as the same kind of placeable.

Current direction:
- `Building` = a logistics process space that owns an interior area and participates in the player-built network
- `Facility` = installed logistics or support function inside a building when a more granular internal object is needed
- `Area` = an outdoor rectangular marker used only for worker spawning or rocket landing

This distinction should guide future placement, interaction, and refactoring work.

Additional interpretation rules:
- a building answers `what is this space for`
- a facility rule answers `how should this facility operate`
- an area answers `where may this spawn or landing operation occur`
- areas are not owned by buildings and cannot overlap building cells
- areas do not own facilities, item filters, or worker policies

Examples:
- `StorageBuilding`, `Packing`, and `Staging` are building-level identities
- fragile handling, hazard restrictions, and item filters are facility-rule policies

Current implementation note:
- the present building creation flow may still use simple rectangular wall-based construction
- this should be treated as an implementation stage, not the final conceptual boundary of the building system

---
