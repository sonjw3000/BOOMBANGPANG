# Current Hub Structure

This document defines the current structural direction of the logistics hub.

It exists to prevent older `Zone = everything` assumptions from leaking back into new design or refactoring work.

---

## Direction Shift

The project is no longer framed only as a single warehouse shelf-layout simulation.

The current direction is a lunar logistics hub simulation where the player builds and operates multiple connected logistics buildings on the moon.

This changes the player question from:

```text
Where should this shelf go?
```

to:

```text
Where should storage, packing, and launch buildings go?
How should they connect?
Who or what should move cargo between them?
```

---

## Structural Layers

The hub should currently be interpreted through three separate layers:

### Building

Physical space.

Examples:
- Storage Building
- Packing Building
- Launch Building

Buildings are responsible for:
- interior logistics space
- cargo ports
- airlocks
- facility installation space

### Facility

Installed function inside a building.

Examples:
- Shelf
- Packing Station
- Worker Bay
- Break Room
- Charging Station

Facilities represent real, placeable operational functions.

### FacilityRule

Operational policy.

Examples:
- Fragile
- Hazard
- Refrigerated
- High Value
- Robot Only

Facility rules are not the same thing as buildings, facilities, or physical areas.
They express handling filters, permissions, or operating constraints and are assigned to facilities.

### Area

Outdoor rectangular space used only for:
- worker spawning
- rocket landing

Areas do not belong to buildings, contain facilities, or express operating policy.

---

## Semantic Reinterpretation

Several older `Zone` concepts are removed or reinterpreted:

- worker standby zones are removed
- `Rest Zone` should be treated as `Break Room`
- `Charging Zone` should be treated as `Charging Station`

These are physical or operational facilities, not generic policy zones.

---

## CargoPort Role

`CargoPort` should be treated as a logistics interface rather than only a simple inbound/outbound endpoint.

It can represent:
- inbound receiving
- outbound shipping
- transfer between buildings

Example flow:

```text
Rocket Landing
-> Inbound Port
-> Storage Building
-> Transfer Port
-> Packing Building
-> Transfer Port
-> Launch Building
-> Outbound Port
```

This is one of the key structures that makes the hub feel like a moon-base logistics operation rather than a single warehouse floor.

---

## Design Intent

This direction exists to support:
- stronger moon-base identity
- clearer human versus robot role separation
- visible inter-building logistics pressure
- higher-level hub layout decisions
- future building specialization without rewriting the gameplay loop

The core logistics loop remains:

```text
Inbound -> Storage -> Order -> Picking -> Packing -> Outbound -> Settlement
```

What changes is the scale and spatial framing of that loop.
