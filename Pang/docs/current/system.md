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
- storage buildings
- packing buildings
- launch buildings
- interior build space
- cargo ports / transfer points
- airlocks and building access flow

## Facilities
- shelves
- packing stations
- worker bays
- break rooms
- charging stations

## Zones & Policies
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
- `Zone` as an operational policy layer

Examples:
- `Worker Bay` is a facility, not a policy zone
- `Break Room` and `Charging Station` are facilities, not generic zones
- `Fragile` or `Hazard` rules are policy zones, not buildings

See `docs/current/hub_structure.md` for the current interpretation and refactoring direction.

## UI
- SelectionCard
- DetailWindow
- provider-based UI
