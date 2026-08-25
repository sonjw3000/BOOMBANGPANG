# Current Systems

## Hub Structure
- multi-building lunar logistics hub
- building-level layout decisions
- indoor logistics plus inter-building logistics
- cargo interfaces between external transport and internal workflows

## Logistics
- inbound
- storage
- picking
- packing
- outbound
- settlement

## Workers
- human workers
- robot workers
- fatigue/battery
- incidents (only for human worker)
- worker abilities

## Tasks & Workflow
- workflow-driven task generation
- task queues
- worker dispatch

## Contracts & Orders
- contracts
- order generation
- order lines
- order-line progress tracked per workflow step quantity
- picking, packing, and outbound progress may overlap on the same order line
- settlement
- penalties
- reputation impact

## Buildings
- generic buildings composed into storage, packing, and launch capabilities
- interior build space
- cargo ports / transfer points
- airlocks and building access flow

## Facilities
- shelves
- packing stations
- worker bays
- break rooms
- charging stations

## Facility Rules
- fragile handling
- hazard handling
- refrigerated handling
- high-value handling
- robot-only operation

## Economy
- revenue
- expenses
- operational costs

## Reputation
- delivery reliability
- operational trust

## Incidents
- worker accidents
- landing failures
- infrastructure damage

## Automation
- robot workers
- logistics automation
- inter-building transport candidates remain future-facing

## Grid & Placement
- grid-based placement
- building footprints
- facility placement inside buildings
- interaction points
- occupancy
- pathfinding
- worker movement reservations
- TrafficCoordinator-based movement conflict handling

## Terminology

Current terminology should distinguish:
- `Building` as physical space
- `Facility` as installed equipment or room function
- `FacilityRule` as an operational policy assigned to facilities
- `Area` as an outdoor worker-spawn or rocket-landing rectangle

Examples:
- worker standby zones are no longer used
- `Break Room` and `Charging Station` are facilities, not generic areas
- `Fragile` or `Hazard` rules are facility rules, not buildings or areas

See `docs/current/hub_structure.md` for the current interpretation and refactoring direction.

## UI
- SelectionCard
- DetailWindow
- provider-based UI
