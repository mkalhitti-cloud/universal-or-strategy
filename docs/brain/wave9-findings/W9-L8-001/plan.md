# W9-L8-001 Plan: ProcessOnStateChange -- Dictionary Dispatch Refactor

## Status
PLAN

## File
`src/V12_002.Lifecycle.cs`

---

## 1. Dictionary Field Declaration

Add the following field to `src/V12_002.Lifecycle.cs`, inside the
`public partial class V12_002 : Strategy` class body, at the top of the
`#region OnStateChange` block (before `OnStateChange`):

```csharp
private static readonly Dictionary<State, Action<V12_002>> _stateDispatch =
    new Dictionary<State, Action<V12_002>>
    {
        { State.SetDefaults, s => s.HandleSetDefaults() },
        { State.Configure,   s => s.HandleConfigure()   },
        { State.DataLoaded,  s => s.HandleDataLoaded()  },
        { State.Realtime,    s => s.HandleRealtime()    },
        { State.Terminated,  s => s.HandleTerminated()  },
    };
```

### Field design notes

| Property | Value |
|----------|-------|
| Access | `private` -- no new public API |
| Modifier | `static readonly` -- initialized once at class load, immutable reference after |
| Key type | `NinjaTrader.Cbi.State` (enum) -- already imported via `using NinjaTrader.Cbi;` at line 20 |
| Value type | `Action<V12_002>` -- receives `this` as the instance parameter |
| Initializer | Field-level collection initializer syntax -- no static constructor needed |
| Thread safety | Immutable after initialization; reads require no coordination (no lock()) |

---

## 2. Refactored ProcessOnStateChange Method Body

```csharp
private void ProcessOnStateChange(State state)
{
    if (_stateDispatch.TryGetValue(state, out Action<V12_002> handler))
        handler(this);
}
```

### Design notes

- `TryGetValue` is a single Dictionary read -- O(1), zero allocation, no branch per case.
- Unknown `State` values (e.g. `State.Historical`, `State.Transition`, etc.) are silently
  ignored, which exactly matches the behavior of the original switch with no default case.
- `handler(this)` passes the current instance as the `Action<V12_002>` argument, routing
  to the correct private method on `this`.
- No new heap allocations on the hot path -- the lambda delegates are captured once at
  class load into the static Dictionary.

---

## 3. Predicted CYC of Refactored ProcessOnStateChange

**CYC = 2**

Calculation:
- Base: 1
- `if (_stateDispatch.TryGetValue(...))`: +1
- No additional branches
- **Total: 2**

Reduction: 6 -- 2 = **-4 CYC** (from 6 down to 2).

---

## 4. Target Partial File

**`src/V12_002.Lifecycle.cs`**

Rationale:
- The method `ProcessOnStateChange` lives in this file (lines 44-64).
- The five handler methods (`HandleSetDefaults`, `HandleConfigure`, `HandleDataLoaded`,
  `HandleRealtime`, `HandleTerminated`) are also all declared in this file (lines 66,
  400, 504, 671, 190 respectively).
- `private static readonly` fields on a partial class are accessible to all partial
  class files in C#; however, co-locating the field with its sole consumer (`ProcessOnStateChange`)
  is the minimal-change, least-surprise placement.
- No other partial file needs to reference `_stateDispatch`.

Exact insertion point: immediately before line 44 (the `private void ProcessOnStateChange` declaration),
inside the `#region OnStateChange` block that opens at line 34.

---

## 5. New Handler Methods Required

**None.**

All five handler methods already exist as private instance methods on the `V12_002` class
in `src/V12_002.Lifecycle.cs`:

| Handler | Confirmed Location |
|---------|-------------------|
| `HandleSetDefaults()` | Line 66 |
| `HandleTerminated()` | Line 190 |
| `HandleConfigure()` | Line 400 |
| `HandleDataLoaded()` | Line 504 |
| `HandleRealtime()` | Line 671 |

The `Action<V12_002>` lambdas in the Dictionary are inline call-throughs only:
`s => s.HandleXxx()`. They introduce no new methods, no new logic, and no change to
any handler's signature or visibility.

---

## OKF Compliance Summary

| Rule | Status |
|------|--------|
| No `lock()` | PASS -- `static readonly` Dictionary is immutable; TryGetValue is lock-free |
| CYC <= 8 | PASS -- refactored method CYC = 2 |
| No new allocation on hot path | PASS -- lambdas captured at class load; TryGetValue allocates nothing |
| No new public API | PASS -- field and refactored method both `private` |
| `DateTime.Now` ban | N/A |
| ASCII-only identifiers | PASS |
| camelCase locals | PASS -- `handler` is camelCase |
| switch expression over statement | PASS -- switch eliminated entirely (Dictionary dispatch) |

Per `complexity-reduction.md` Rule 3 (Lookup table / Dictionary dispatch): this is the
canonical OKF pattern for replacing a switch+N cases with CYC+1 dispatch.
