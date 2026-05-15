# Debugging Rules

## Preserve Traceability

Gameplay state changes should always remain traceable.

It should be possible to understand:
- why money changed
- why reputation changed
- why a task failed
- why logistics flow stopped

Avoid hidden gameplay state mutations.

---

## Preserve Ownership

Major gameplay state should only be modified through the owning service or manager.

Examples:
- Economy -> EconomyService
- Grid state -> GridService
- Task assignment -> TaskManager

This helps keep the simulation predictable and debuggable.

---

## Prefer Visible State

Important operational state should be visible and inspectable.

Examples:
- worker state
- queue size
- congestion
- occupancy
- bottlenecks
- logistics flow

Invisible failures should be avoided where possible.

---

## Logging Rules

Logs should represent meaningful events.

Examples:
- incidents
- settlement results
- task failures
- workflow interruptions

Avoid excessive log spam.

Important logs should include context and reason information where possible.

---

## Avoid Hidden Automation

Systems should avoid making large hidden decisions without visible feedback.

The player and developer should be able to understand:
- what happened
- why it happened
- which system caused it

---

## Prefer Debuggable Structure

Prefer:
- explicit flow
- readable logic
- isolated ownership
- predictable state changes

Avoid:
- hidden side effects
- deep implicit dependencies
- difficult-to-track state mutations
