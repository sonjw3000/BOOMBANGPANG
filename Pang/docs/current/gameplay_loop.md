# Gameplay Loop

## Core Loop

The current gameplay loop is structured around the flow of cargo through a logistics hub.

That flow may run inside one multi-purpose building or span several connected buildings.

Inbound
-> Storage
-> Order
-> Picking
-> Packing
-> Outbound
-> Settlement

The player is responsible for stabilizing and optimizing this logistics flow under increasing operational pressure.

At the hub level, this includes:
- choosing which facilities and operating Rules each building contains
- connecting those buildings through cargo interfaces
- deciding how cargo moves both inside buildings and between buildings

Inside a building, Capsule flow is Rule-driven:

Inbound CargoPort
-> matching CapsuleBuffer
-> task-driven item or manifest changes
-> matching CapsuleBuffer when the current Rule no longer matches
-> Building threshold + matching CargoPort Rule, decided by Relocation
-> Outbound CargoPort

The same Building may host several or all of these operations. Capsule routing, state normalization, and every Task producer use this generic model; BuildingId-scoped facilities and Rules select available work rather than Staging, Storage, Packing, or Launch subclasses.

---

## Inbound

Cargo spacecraft arrive and unload goods into the logistics hub through inbound cargo interfaces.

Inbound flow may enter a Building configured for storage directly or pass through transfer cargo ports first.

Inbound logistics may introduce:
- congestion
- unloading delays
- dangerous landing incidents
- infrastructure disruption

The player must maintain stable unloading flow and storage access.

---

## Storage

Items are transported into storage areas and organized throughout the storage side of the hub.

Storage may share a multi-purpose Building or live in a Building dedicated through its facilities and Rules.

Storage layout directly affects:
- travel distance
- worker congestion
- picking efficiency
- logistics throughput

Poor storage organization can create long-term bottlenecks.

---

## Order Generation

Orders are generated based on contracts and logistics demand.

Different orders may require:
- different item types
- different priorities
- different delivery speeds

Operational pressure increases as order volume grows.

---

## Picking

Workers or automation systems retrieve requested items from storage.

Picking efficiency is heavily affected by:
- worker movement
- congestion
- storage layout
- task distribution
- automation infrastructure

Picking is one of the primary bottleneck sources in the logistics flow.

---

## Packing

Retrieved items are packed and prepared for shipment.

Packing may happen in the same Building as storage or in a separately configured Building connected through transfer logistics.

Packing introduces additional:
- worker load
- queue pressure
- infrastructure demand

Inefficient packing flow may delay outbound logistics.

---

## Outbound

Packed cargo is transferred to outbound logistics infrastructure and launched toward off-world destinations.

Outbound flow may pass through a separately configured launch Building and outbound cargo ports, or leave from the same multi-purpose Building.

Outbound flow may be affected by:
- launch delays
- loading congestion
- infrastructure damage
- transportation incidents

---

## Settlement

Completed deliveries generate:
- revenue
- reputation changes
- penalties
- operational feedback

The player is expected to analyze the results and improve the logistics system accordingly.

---

## Demo Goal

The demo uses an ordered scenario objective sequence:

1. Complete the first order.
2. Complete three orders before their deadlines.
3. Research Temperature Monitoring and Thermal Operations.
4. Research Traffic Control and Human Recognition.
5. Complete a Lunar Produce Cold Chain order on time and reach 50 reputation.

`ScenarioObjectiveService` reads state and events from the owning Order, Research, and Economy systems. It does not mutate their gameplay state. The objective definitions are authored in `DemoScenario.asset`, while only runtime progress is saved.

Research uses one active project and an ordered waiting queue. A project can be queued when its prerequisites are either completed, currently active, or placed earlier in the queue. Operational features still unlock only when research completes. Research costs are paid when a queued project starts; if funds are insufficient, the queue pauses at its first project and resumes automatically after enough money becomes available.

---

## Continuous Optimization

The gameplay loop is designed around continuous operational optimization.

As the logistics hub expands, the player must:
- choose where each major building should be placed
- decide how buildings should connect to one another
- reorganize layouts
- expand infrastructure
- improve worker flow
- introduce automation
- resolve new bottlenecks

Operational stability becomes increasingly difficult as system complexity grows.
