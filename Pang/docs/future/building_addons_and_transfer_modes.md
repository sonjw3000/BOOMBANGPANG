# Future: Building Addons and Transfer Modes

This document tracks future-facing expansion ideas that build on the current multi-building hub direction.

These ideas should not be treated as mandatory current implementation requirements.

---

## Current Baseline

The current direction already assumes:
- a lunar logistics hub made of multiple buildings
- separation between `Building`, `Facility`, and `Zone`
- cargo ports that can support both external flow and inter-building transfer

This document covers the next layer beyond that baseline.

---

## Near-Term Integration Path

Before full inter-building logistics, the next implementation step should be to make `Zone` membership affect actual gameplay decisions rather than only acting as presentation or debug data.

Recommended order:

1. promote `ZoneManager` facility lookup into safe runtime API such as `GetFacilitiesForZone`
2. start validating facility-to-zone fit
3. enrich Building / Zone detail views with zone-based counts and grouped facility lists
4. build building-local logistics flow before inter-building transfer

The key intent is:
- `Zone` should become the basis for facility placement judgment
- `Zone` should become the basis for local logistics routing
- `Zone` should stop being only a visual or inspection layer

### Facility-to-Zone Validation

Expected near-term examples:
- `Shelf` inside `Storage` zone
- `PackingStation` inside `Packing` zone

This does not need to hard-block all placement immediately.

Acceptable first versions:
- validation API only
- warning state in detail UI
- editor/runtime warning log for mismatched placement

### Building-Local Logistics Step

The intended step after zone-aware validation and query cleanup is:

```text
CargoPort
-> building local service
-> zone / facility distribution
```

This is the immediate precursor to full building-to-building logistics.

### Airlock Note

`Airlock` should be tracked as a required future building facility.

Reason:
- workers will need a readable path between exterior and interior building space
- outdoor and indoor logistics should not blur together invisibly
- later transfer rules may need to distinguish which routes can pass through airlocks

`Airlock` should be treated as part of the building logistics structure, not only as visual flavor.

---

## Building Addons

Addons are building-level modifiers that express an operating philosophy rather than only adding raw content.

Examples:
- Safety Module
- Productivity Module
- Hazard Module

Potential roles:
- change operating constraints
- modify worker or robot suitability
- shift throughput versus risk tradeoffs
- specialize buildings for certain cargo or contracts

Addons should ideally create meaningful strategic identity, not only flat stat bonuses.

---

## Inter-Building Transfer Modes

Candidate transport methods between buildings:
- human carriers
- outdoor robots
- conveyors
- logistics tubes

These modes can later differentiate:
- cost
- reliability
- weather or hazard exposure
- throughput
- maintenance burden
- suitability for human or robot operation

---

## Open Direction Questions

- Which transfer methods should be available in the first playable hub version?
- Should addons be attached to the whole building or to specific facilities?
- How much of building specialization should come from addons versus cargo policies?
- Which transfer methods should work indoors, outdoors, or across airlocks?

---

## Non-Goals For Now

This document does not require immediate implementation of:
- full addon progression trees
- final transport method roster
- final balance values
- advanced building upgrade chains
