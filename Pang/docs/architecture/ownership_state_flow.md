
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

## 4 UI

UI does not own gameplay state.

UI should not:
- directly modify economy state
- directly assign tasks
- directly modify grid occupancy
- directly mutate logistics state

---
