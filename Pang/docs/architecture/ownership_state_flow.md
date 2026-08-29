
#  Ownership & State Flow

Each major gameplay state should have a clear owner.

State changes should go through the owning service or manager instead of being modified directly by unrelated systems.

This rule exists to keep the simulation:
- traceable
- debuggable
- maintainable

---

## 1 EconomyService

EconomyService owns:
- money
- expenses
- revenue
- settlement-related economy flow

Economy-related changes should go through EconomyService APIs.

---

## 2 GridService

GridService owns:
- occupancy
- placement state
- movement-related spatial state

Grid state should not be modified directly by unrelated systems.

---

## 3 TaskManager

TaskManager owns:
- task queues
- task assignment
- worker dispatch

Workflow systems generate tasks.
Workers execute tasks.
Task assignment flows through TaskManager.

---

## 4 TrafficCoordinator

TrafficCoordinator owns:
- worker movement conflict registration
- traffic wait state
- retry and detour decisions for blocked movement
- head-on conflict resolution
- clearing priority inheritance for detouring routes

TrafficCoordinator does not own grid occupancy. Grid reservations remain owned by `GridService`.

TrafficCoordinator does not complete tasks. Worker task flow remains owned by workers and task systems.

---

## 5 Building

Building-related ownership should stay explicit.

Building systems should own:
- building identity
- building-level operating policy such as capsule dispatch threshold
- building-owned interior space
- worker affiliation scope

Installed facilities and their FacilityRules are the source of available logistics capabilities. Existing inbound/outbound services own producer registration and planners by BuildingId, so Labeling, Storing, Picking, Packing, and Launch capability is not selected by a Building class or role enum.

Region classification such as indoor / outdoor should support placement and spatial reasoning, but should not replace building ownership as the source of truth for space identity.

---

## 6 FacilityRule & Area

`FacilityRuleManager` owns building-scoped rule presets and their facility assignments. Rules provide explicit logistics filters and operating policy without owning physical space.

`ItemProcessStageEvaluator` derives one aggregate stage for an `IItemContainer` from its actual `ItemStatus` values and an optional `PickingManifest`. A container with no positive-quantity stacks and no manifest work evaluates to `Empty`; every other concrete process stage implies physical cargo. `Any` remains only a query/policy sentinel and is not a concrete derived container stage. FacilityRule may require an exact stage in addition to its existing item, worker, and manifest filters. Capsule routing supplies the manifest owned by `OutboundWorkflowService`; ordinary storage can use the same evaluator without Capsule-specific state. A whole-container manifest matches only when every manifest destination is allowed by the Rule; single-work queries keep their existing any-match manifest behavior and must explicitly project a process stage when selecting an output destination.

FacilityRule has no separate content-state dimension. `Empty`, `Unlabeled`, `Labeled`, `Picked`, `Packed`, and `LaunchReady` are the complete concrete stage set, while a Rule stage mask of `None` means unrestricted. Item or manifest conditions cannot match an `Empty` filter because there is no cargo or manifest to inspect. CapsuleBuffer has one immutable `CapsuleDockState.Buffer` kind and no configurable inbound, outbound-standby, or empty role. A Rule with no conditions rejects all cargo. A docked empty Capsule is Buffer preparation and is excluded from cargo Rule mismatch relocation; projected post-transfer filters decide whether item work may use it. The Rule editor exposes only the process-stage dimension under `Item Conditions`.

Capsule item-transfer queries evaluate the stage that will exist after the transfer (`Picked` or `Packed`) instead of selecting an `Empty` Rule and relying on later relocation. Standard CapsuleBuffer content access is shareable across compatible ItemTransfer Tasks: `Picking` shares `Inside + Picked` outputs, while `PackingOutput` and `LaunchSort` share `Inside + Packed` outputs; `Storing`, `PackingInput`, and `LaunchSort` share item Pick access through `IItemPickReservable` quantity reservations. Pick and Put remain mutually exclusive on the same Buffer, and Capsule relocation, Labeling, invalidation, and player claims remain exclusive. Each selected Put Task retains the Buffer only while moving to it, then releases that retain after every successful Put so Relocation can reevaluate the resulting cargo before another item Task is scheduled. A failed Put also releases the retain and replans the remaining carried quantity.

`CapsuleBufferService` owns BuildingId-scoped logical queries for Rule-matched CapsuleBuffer destinations and the reverse registration index from CapsuleBuffer to BuildingId. The caller must explicitly choose whether the query evaluates Launch readiness, so ordinary Packing routing remains `Packed` while Launch routing may produce `LaunchReady`. These queries return eligible facilities only; they do not decide relocation scope, reserve a Dock, or create a Task.

`CapsuleRelocateCoordinator` owns lifecycle normalization, Rule mismatch evaluation, the complete outbound-route decision, relocation matching, Dock reservations, active relocation ownership, pending requests, and relocation Task creation. Item planners may ask it whether a current or projected cargo route exists, but must not independently combine CapsuleBuffer state, Building threshold, and CargoPort Rule. Dirty means that a Dock or Building must be reevaluated; it does not guarantee that a Task will be created. Repeated marks are coalesced and `BuildingManager.LateUpdate` flushes Relocation before item-work scheduling so the resulting reservation or lifecycle transition wins the race against a later Put.

During Dirty evaluation, `CapsuleRelocateCoordinator` derives the Capsule lifecycle from current physical data, the Building threshold, and the same-Building CargoPort Rules:

`empty payload -> Empty`

`InboundCargoPort payload -> IB`

`CapsuleBuffer payload below the Building threshold -> Inside`

`CapsuleBuffer payload at the effective threshold + matching non-empty same-Building OutboundCargoPort Rule -> OB`

Rule-mismatched loaded `Inside` capsules request another Rule-matched CapsuleBuffer in the same Building. Empty Capsules remain in their current Buffer as preparation for later projected item work. A CargoPort Rule is the sole final-stage policy: it may match the physical process stage or the derived `LaunchReady` stage. Once that Rule matches and the Building threshold is reached, Relocation promotes the Capsule to `OB` and requests that port. Temporary CargoPort occupancy leaves the Capsule in `OB` with a pending send until a matched port becomes available. If the applied outbound Rule is removed or becomes a mismatch, Dirty evaluation returns the Capsule to `Inside` and invalidates stale outbound work. `CapsuleRelocationTask` owns only the physical move; route selection and state derivation remain with `CapsuleRelocateCoordinator` and its facility services.

`AreaManager` owns outdoor rectangular areas used by:
- `WorkerSpawnManager` for `WorkerSpawn` candidates
- inbound rocket flow for `RocketLanding` candidates

Areas do not belong to buildings, register facilities, or own logistics policy.

---

## 7 UI

UI does not own gameplay state.

UI should not:
- directly modify economy state
- directly assign tasks
- directly modify grid occupancy
- directly mutate logistics state

UI should request:
- building creation and building edits through the owning building system
- area creation and area edits through `AreaManager`
- facility-rule changes through `FacilityRuleManager`

---

## 8 RobotNavigationService

`RobotNavigationService` owns:
- Navigation Hub and Relay runtime registration
- Relay-to-Hub ownership and active connection state
- Hub-expanded coverage calculation
- the mapping from navigation region IDs to influencing Hubs
- navigation coverage version changes

`GridCell.NavigationRegionId` is only a fast derived projection. `GridService` does not decide Hub membership, Relay activation, or robot orchestration capacity.

---
