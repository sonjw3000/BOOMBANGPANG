# Gameplay Loop

## Core Loop

The current gameplay loop is structured around the flow of cargo through a logistics hub.

That flow can now span multiple specialized buildings instead of only one large warehouse interior.

Inbound
-> Storage
-> Order
-> Picking
-> Packing
-> Outbound
-> Settlement

The player is responsible for stabilizing and optimizing this logistics flow under increasing operational pressure.

At the hub level, this includes:
- placing specialized buildings such as storage, packing, and launch buildings
- connecting those buildings through cargo interfaces
- deciding how cargo moves both inside buildings and between buildings

---

## Inbound

Cargo spacecraft arrive and unload goods into the logistics hub through inbound cargo interfaces.

Inbound flow may enter a dedicated storage building directly or pass through transfer cargo ports first.

Inbound logistics may introduce:
- congestion
- unloading delays
- dangerous landing incidents
- infrastructure disruption

The player must maintain stable unloading flow and storage access.

---

## Storage

Items are transported into storage areas and organized throughout the storage side of the hub.

Storage may live inside dedicated storage buildings rather than being treated as one shared warehouse floor.

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

Packing may happen in dedicated packing facilities or buildings that receive cargo from storage through transfer logistics.

Packing introduces additional:
- worker load
- queue pressure
- infrastructure demand

Inefficient packing flow may delay outbound logistics.

---

## Outbound

Packed cargo is transferred to outbound logistics infrastructure and launched toward off-world destinations.

Outbound flow may pass through launch buildings and outbound cargo ports rather than leaving directly from the original storage area.

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

The current demo clear condition is reaching the configured reputation target.
The demo goal system reads reputation from the economy owner, announces the target at game start, and announces game clear once the target is reached.

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
