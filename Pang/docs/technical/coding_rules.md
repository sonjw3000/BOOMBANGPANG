# Coding Rules

## State Mutation

- Do not modify gameplay state directly across unrelated systems.
- Use the owning service or manager APIs.
- Economy changes must go through EconomyService.
- Grid state changes must go through GridService.
- Task assignment must go through TaskManager.

---

## Event Usage

- Do not spam events every frame.
- Events should represent meaningful state changes.
- Prefer event-driven refreshes over polling where practical.

Examples:
- settlement completed
- contract completed
- incident occurred
- worker hired

- Subscribe/unsubscribe events in matching lifecycle methods.
- Avoid leaving dangling event subscriptions.

---

## Hidden Side Effects

- Avoid hidden gameplay state mutations.
- Method behavior should match method naming.

Examples:
- CanXXX should only validate.
- GetXXX should only return data.
- TryXXX may perform state changes and fail.
- ApplyXXX / CommitXXX should explicitly mutate state.
- ReportXXX should notify other systems.

## Item Transfer

- Move gameplay items through `ItemTransferUtility` instead of calling paired `AddItem` / `RemoveItem` or `AddStack` / `RemoveStack` directly from task flow.
- Use `MoveItem` for quantity-based workflow lines, reservations, and allocation/progress accounting.
- Only consume source pick reservations through an explicit transfer option such as `consumeSourcePickReservation`; plain container removal must not imply reservation consumption.
- Use `MoveItemAsStack` when a quantity is converted into a new stack object, such as packaging picked items into an `ItemPackage`.
- Use `MoveAllStacks` only for whole-stack transfers where stack identity must be preserved, such as packaged outbound cargo.
- Treat `TransferResultKind.None`, `Partial`, and `Complete` explicitly before advancing a task.
- `OnStackMove` callbacks should only perform after-commit reporting such as outbound stage updates or logging; they must not mutate source or target container quantities.

---

## Update Usage

- Keep Update() logic minimal.
- Avoid unnecessary per-frame allocations.
- Avoid GetComponent() or Find() calls inside Update().
- Prefer centralized ticking or event-driven flow where practical.

Simulation-critical systems should prefer centralized ticking where appropriate.

---

## Naming Rules

- Use tabs for indentation.
- Classes, structs, enums, and methods should use PascalCase.
- Interfaces should use the `I` prefix.
- Methods should start with an uppercase verb.
- Private member variables should start with lowercase names.
- Public members should start with uppercase names.
- Event handlers should use the `OnEventName` pattern.

Examples:
- TryAssignTask()
- RequestSettlement()
- ReportIncident()
- OnTaskCompleted()

- private int currentMoney;
- public int CurrentMoney;

Widely used project-level abbreviations are allowed where readability remains clear.

Examples:
- TaskMgr
- ItemDB

---

## Inspector & Serialization

- Avoid public fields where possible.
- Prefer `[SerializeField] private`.
- Expose balancing values through SerializeField where appropriate.
- Keep gameplay tuning values easy to modify.
- Avoid storing runtime gameplay state inside ScriptableObjects unless explicitly intended.

Examples:
```csharp
[SerializeField] private float moveSpeed;
public float MoveSpeed => moveSpeed;
