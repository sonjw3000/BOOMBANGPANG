
#  Spatial & Logistics Structure

## 1 GridService & GridMap

The warehouse simulation is built around a multi-layered spatial grid system.

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

---


