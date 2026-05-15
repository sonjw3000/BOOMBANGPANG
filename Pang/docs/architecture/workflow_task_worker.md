# Workflow & Task Pipeline

The logistics simulation currently follows a layered structure:

Workflow
-> Task
-> Worker

---

## 1 Workflow Managers

Workflow managers monitor high-level logistics flow and generate tasks.

Examples:
- inbound logistics
- outbound logistics
- order fulfillment
- storage workflows

Workflow systems decide:
- what work should happen
- when work should happen
- which policy or strategy should be used

Workflow systems should not directly execute work themselves.

---

## 2 TaskManager

`TaskManager` owns:
- pending task queues
- task dispatch
- worker-task assignment

Tasks are assigned based on factors such as:
- availability
- distance
- worker ability
- task priority

Workflow systems generate tasks.
Workers execute assigned tasks.

---

## 3 Workers & AI

Workers currently share a common `AIWorker` foundation.

Both humans and robots use behavior-tree-driven task execution.

Worker differences are implemented through:
- abilities
- modifiers
- fatigue
- battery systems
- worker-specific logic

Human workers are affected by fatigue and incidents.

Robot workers are affected by battery and efficiency systems.

Worker abilities determine which tasks a worker can perform.

---
