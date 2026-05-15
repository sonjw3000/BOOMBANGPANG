#  UI & Interaction Structure

## 1 Selection Pattern

The UI follows a selection/detail structure.

### SelectionCard

Used for:
- quick summaries
- lightweight inspection
- immediate operational information

### DetailWindow

Used for:
- deeper inspection
- management actions
- detailed information

---

## 2 UI State Flow

UI should not directly mutate gameplay state.

UI is expected to:
- display information
- request actions
- call services

Gameplay state changes should flow through the owning systems.

---
