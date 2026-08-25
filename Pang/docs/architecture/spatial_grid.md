
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
- a building answers `which interior scope and outbound policy owns this space`
- a facility rule answers `how should this facility operate`
- an area answers `where may this spawn or landing operation occur`
- areas are not owned by buildings and cannot overlap building cells
- areas do not own facilities, item filters, or worker policies

Examples:
- storage, staging, and packing are capabilities composed from installed facilities and their Rules
- legacy `BuildingType` values may still describe player-facing presets without selecting Labeling, Storing, or Picking runtime producers
- fragile handling, hazard restrictions, and item filters are facility-rule policies

---

## 5 Robot Navigation Coverage

`RobotNavigationService` owns robot navigation coverage derived from outdoor `NavigationHub` and `RelayNode` facilities.

Current rules:
- an operational Navigation Hub starts one Hub-owned coverage area
- a Relay belongs to one Hub and becomes active when its center cell is already inside that Hub's active coverage
- an active Relay extends the same Hub-owned coverage, so chained Relays can expand the network
- one grid cell may be influenced by multiple Hubs
- `GridCell.NavigationRegionId` is a derived runtime cache for the influencing Hub combination; the service remains the owner of its meaning
- coverage and region IDs are rebuilt after facility, power, or operational-state changes and are not persistent grid state

Navigation Hubs receive power directly from an in-range `PowerHub`. Active Relays add their configured power load to their owning Navigation Hub. Robot compute allocation and movement restrictions consume this coverage in later implementation stages; they are not owned by the grid cache.

Current implementation note:
- the present building creation flow may still use simple rectangular wall-based construction
- this should be treated as an implementation stage, not the final conceptual boundary of the building system

---
