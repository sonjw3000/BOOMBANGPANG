
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
- zones are defined as sub-areas inside that owned building space
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

## 4 Building / Facility / Zone Interpretation

Spatial logic should not treat every operational concept as the same kind of placeable.

Current direction:
- `Building` = a logistics process space that owns an interior area and participates in the player-built network
- `Facility` = installed logistics or support function inside a building when a more granular internal object is needed
- `Zone` = an internal rule area inside a building, used to control handling policy rather than define the building's core logistics purpose

This distinction should guide future placement, interaction, and refactoring work.

Additional interpretation rules:
- a building answers `what is this space for`
- a zone answers `how should this part of the space operate`
- one building may contain multiple zones
- zones must remain inside the owning building boundary
- zone logic should not define standalone logistics process identity outside of a building

Examples:
- `StorageBuilding`, `Packing`, and `Staging` are building-level identities
- fragile handling, hazard restrictions, temperature rules, and worker-only rules are zone-level policies

Current implementation note:
- the present building creation flow may still use simple rectangular wall-based construction
- this should be treated as an implementation stage, not the final conceptual boundary of the building system

---

