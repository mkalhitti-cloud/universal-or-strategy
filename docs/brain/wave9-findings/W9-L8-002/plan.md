# W9-L8-002 Plan: Dictionary Dispatch for ProcessBracketEvent

## Source Location

File: `src/V12_002.Symmetry.BracketFSM.cs`  
Method: `ProcessBracketEvent` -- lines 473-506  
Current CYC: 6 | Target CYC: 3 (Jane Street strict limit: <= 8; reduced to minimal)

---

## 1. Dictionary Field Declaration

Place this field immediately after the `#region BracketFSM Logic (Actor Consumer)` comment
(line 94), before `ProcessAccountMailbox`. It is `private static readonly` -- immutable after
class load, zero coordination overhead for concurrent reads (no lock() needed, per
lock-free-patterns.md).

```csharp
// W9-L8-002: Dictionary dispatch -- replaces switch in ProcessBracketEvent.
// static readonly = immutable after class init; thread-safe for concurrent reads.
// Action<AccountEvent, FollowerBracketFSM> carries both shared context objects.
// Two entries per fall-through pair (Accepted/Working, Filled/PartFilled).
private static readonly Dictionary<OrderState, Action<AccountEvent, FollowerBracketFSM>>
    _bracketDispatch = new Dictionary<OrderState, Action<AccountEvent, FollowerBracketFSM>>
    {
        { OrderState.Accepted,   (e, f) => TransitionToAccepted(f)        },
        { OrderState.Working,    (e, f) => TransitionToAccepted(f)        },
        { OrderState.Filled,     (e, f) => HandleFsmFilled(e, f)          },
        { OrderState.PartFilled, (e, f) => HandleFsmFilled(e, f)          },
        { OrderState.Cancelled,  (e, f) => TransitionToCancelled(e, f)    },
        { OrderState.Rejected,   (e, f) => TransitionToRejected(e, f)     },
    };
```

**Design notes:**
- Value type is `Action<AccountEvent, FollowerBracketFSM>` -- both shared context objects passed
  explicitly. No closure over `this` fields required for dispatch (avoids capturing a mutable
  receiver into a static field).
- `TransitionToAccepted` does not consume `evt`, so the lambda discards it via `(e, f)` -- `e`
  unused but signature uniform across all entries.
- `HandleFsmFilled`, `TransitionToCancelled`, `TransitionToRejected` each receive both `e` and
  `f` via `(e, f) => Method(e, f)`.
- Six entries total: 2 (Accepted/Working) + 2 (Filled/PartFilled) + 1 (Cancelled) + 1 (Rejected).
- `default` no-op is implicit: `TryGetValue` returns `false`, no action taken. No entry needed.
- Field-level initializer -- no static constructor, no lazy init, no lock().

---

## 2. Refactored ProcessBracketEvent Body

The guard, `oldState` capture, and `LogFsmTransition` post-hook are **unchanged**.
Only the `switch` block (lines 480-503) is replaced with a `TryGetValue` dispatch.

```csharp
/// <summary>
/// Core FSM transition logic. Driven exclusively by broker confirmations.
/// Shadow Mode: Observes reality and logs divergences.
/// </summary>
private void ProcessBracketEvent(AccountEvent evt)
{
    if (!ValidateFsmEventPreconditions(evt, out FollowerBracketFSM fsm))
        return;

    FollowerBracketState oldState = fsm.State;

    if (_bracketDispatch.TryGetValue(evt.NewState, out var handler))
        handler(evt, fsm);

    LogFsmTransition(fsm, oldState, evt);
}
```

**Structural diff vs original:**
- Removed: `switch (evt.NewState) { case ... }` block (24 lines)
- Added: single `if (_bracketDispatch.TryGetValue(...)) handler(evt, fsm);` (2 lines)
- Guard: identical (`if (!ValidateFsmEventPreconditions(...)) return;`)
- Pre-hook: identical (`FollowerBracketState oldState = fsm.State;`)
- Post-hook: identical (`LogFsmTransition(fsm, oldState, evt);`)

---

## 3. CYC Analysis

| # | Branch point | Expression |
|---|--------------|------------|
| 1 | Base | method entry |
| 2 | `if` guard | `!ValidateFsmEventPreconditions(evt, out fsm)` early-return |
| 3 | `if` dispatch | `_bracketDispatch.TryGetValue(evt.NewState, out var handler)` |

**Predicted CYC = 1 (base) + 1 (guard if) + 1 (TryGetValue if) = 3**

This is well within the Jane Street strict standard of <= 8.  
Reduction: CYC 6 -> CYC 3 (50% reduction).

---

## 4. Field Placement

- **File:** `src/V12_002.Symmetry.BracketFSM.cs`
- **Inside class:** `V12_002 : Strategy` (partial class)
- **Region:** `#region BracketFSM Logic (Actor Consumer)` -- immediately after line 94
- **Rationale:** Co-located with the method that uses it. Static readonly fields for dispatch
  tables should live in the same region as the dispatching method, not in the Definitions region
  (which contains enums and structs, not runtime logic).

The field is placed BEFORE `ProcessAccountMailbox` so it is declared before first use when
reading top-to-bottom, consistent with existing field ordering in this file.

---

## 5. Handler Methods -- No New Methods Needed

All four handler methods already exist as private instance methods:

| Lambda | Existing Method Signature |
|--------|--------------------------|
| `(e, f) => TransitionToAccepted(f)` | `private void TransitionToAccepted(FollowerBracketFSM fsm)` |
| `(e, f) => HandleFsmFilled(e, f)` | `private void HandleFsmFilled(AccountEvent evt, FollowerBracketFSM fsm)` |
| `(e, f) => TransitionToCancelled(e, f)` | `private void TransitionToCancelled(AccountEvent evt, FollowerBracketFSM fsm)` |
| `(e, f) => TransitionToRejected(e, f)` | `private void TransitionToRejected(AccountEvent evt, FollowerBracketFSM fsm)` |

**No new handler methods required.**  
**No existing method signatures change.**  
**No public API surface added.**

---

## 6. OKF Compliance Checklist

| Rule | Status |
|------|--------|
| `lock()` banned | PASS -- `static readonly` Dictionary, immutable after init, zero locks |
| `DateTime.Now` banned | PASS -- not touched by this change |
| CYC <= 8 | PASS -- CYC drops from 6 to 3 |
| No new alloc on hot path | PASS -- Dictionary lookup is O(1), no `new` per call, lambdas compiled once at class load |
| `switch expression` preferred over `switch statement` | PASS -- switch eliminated entirely in favor of Dictionary dispatch |
| xUnit tests needed for extracted helpers | N/A -- no new helpers extracted; existing handlers already tested |
| ASCII only | PASS -- all identifiers and comments are ASCII |
| camelCase locals | PASS -- `handler` (local from `out var handler`) is camelCase |
| Private stays private | PASS -- `_bracketDispatch` is `private static readonly` |

---

## 7. Risk Notes

- **Lambda capture:** Lambdas in `_bracketDispatch` reference instance methods via method
  group-like syntax. Since the field is `static readonly` but the methods are instance methods,
  the lambdas must capture `this`. This means the Dictionary cannot hold direct method group
  delegates (`TransitionToAccepted` alone would not compile as a static field value).  
  **Resolution:** The lambdas `(e, f) => TransitionToAccepted(f)` DO capture `this` implicitly
  at call time (they are not stored -- the lambda invocation `handler(evt, fsm)` is called
  from within an instance method, so `this` is in scope). However, because the lambdas are
  stored in a `static readonly` field, they must NOT capture `this` at field-init time.  
  **Correct approach:** Keep `_bracketDispatch` as `static readonly` but change the Value
  type to delegate that accepts `this` as well -- OR change to instance readonly field.

  **REVISED DESIGN (safer):**
  Use `private static readonly Dictionary<OrderState, Action<AccountEvent, FollowerBracketFSM>>`
  where lambdas are written as pure two-arg delegates:
  ```csharp
  { OrderState.Accepted, (e, f) => f.TransitionToAccepted_Static(f) }  // NO - methods are not static
  ```

  The cleanest resolution for a partial class with instance methods is to keep the Dictionary
  as an **instance field** OR use a **static helper with explicit `this`**:

  **Final resolution -- use instance readonly field:**
  ```csharp
  private readonly Dictionary<OrderState, Action<AccountEvent, FollowerBracketFSM>>
      _bracketDispatch;
  ```
  Initialized in constructor or `EnsureInitialized()`. However, this requires a constructor
  change and adds fragility.

  **Simplest correct design -- static field with Action<V12_002, AccountEvent, FollowerBracketFSM>:**
  ```csharp
  private static readonly Dictionary<
      OrderState,
      Action<V12_002, AccountEvent, FollowerBracketFSM>> _bracketDispatch =
      new Dictionary<OrderState, Action<V12_002, AccountEvent, FollowerBracketFSM>>
      {
          { OrderState.Accepted,   (self, e, f) => self.TransitionToAccepted(f)      },
          { OrderState.Working,    (self, e, f) => self.TransitionToAccepted(f)      },
          { OrderState.Filled,     (self, e, f) => self.HandleFsmFilled(e, f)        },
          { OrderState.PartFilled, (self, e, f) => self.HandleFsmFilled(e, f)        },
          { OrderState.Cancelled,  (self, e, f) => self.TransitionToCancelled(e, f)  },
          { OrderState.Rejected,   (self, e, f) => self.TransitionToRejected(e, f)   },
      };
  ```

  Call site in `ProcessBracketEvent` becomes:
  ```csharp
  if (_bracketDispatch.TryGetValue(evt.NewState, out var handler))
      handler(this, evt, fsm);
  ```

  This is the **recommended final design**: static readonly (no instance init needed), explicit
  `self` parameter avoids implicit capture, compiles cleanly for a partial class.

---

## 8. Final Recommended Code (Corrected for static field + instance method)

### Dictionary field (static readonly, Action takes self):

```csharp
// W9-L8-002: Dictionary dispatch -- replaces switch in ProcessBracketEvent.
// static readonly = immutable after class init; thread-safe for concurrent reads (no lock).
// Action<V12_002, AccountEvent, FollowerBracketFSM>: explicit self avoids closure capture
// in static field initializer (partial class pattern, per lock-free-patterns.md).
private static readonly Dictionary<
    OrderState,
    Action<V12_002, AccountEvent, FollowerBracketFSM>> _bracketDispatch =
    new Dictionary<OrderState, Action<V12_002, AccountEvent, FollowerBracketFSM>>
    {
        { OrderState.Accepted,   (self, e, f) => self.TransitionToAccepted(f)      },
        { OrderState.Working,    (self, e, f) => self.TransitionToAccepted(f)      },
        { OrderState.Filled,     (self, e, f) => self.HandleFsmFilled(e, f)        },
        { OrderState.PartFilled, (self, e, f) => self.HandleFsmFilled(e, f)        },
        { OrderState.Cancelled,  (self, e, f) => self.TransitionToCancelled(e, f)  },
        { OrderState.Rejected,   (self, e, f) => self.TransitionToRejected(e, f)   },
    };
```

### Refactored ProcessBracketEvent:

```csharp
private void ProcessBracketEvent(AccountEvent evt)
{
    if (!ValidateFsmEventPreconditions(evt, out FollowerBracketFSM fsm))
        return;

    FollowerBracketState oldState = fsm.State;

    if (_bracketDispatch.TryGetValue(evt.NewState, out var handler))
        handler(this, evt, fsm);

    LogFsmTransition(fsm, oldState, evt);
}
```

**CYC = 3** (base 1 + guard if 1 + TryGetValue if 1) -- confirmed correct.

---

## 9. Summary

| Item | Value |
|------|-------|
| File | `src/V12_002.Symmetry.BracketFSM.cs` |
| Region | `#region BracketFSM Logic (Actor Consumer)` |
| Field placement | After line 94 (`#region` comment), before `ProcessAccountMailbox` |
| Dictionary entries | 6 (2 Accepted/Working + 2 Filled/PartFilled + 1 Cancelled + 1 Rejected) |
| Default no-op | Implicit via `TryGetValue` false -- no entry needed |
| New handler methods | None -- all 4 handlers already exist |
| New public API | None |
| CYC before | 6 |
| CYC after | 3 |
| OKF compliance | PASS all rules |
