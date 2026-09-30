# W9-L8-003 Plan: RouteTargetActionToHandler -- Dictionary Dispatch

## File
`src/V12_002.UI.Callbacks.cs`

## Method
`RouteTargetActionToHandler` (lines 628-669)

## Current CYC
7 (base 1 + 6 switch cases)

## Target CYC
3 (base 1 + TryGetValue branch + else branch)

---

## 1. Dictionary Field Declaration

Place this field **inside the class body**, immediately before the
`RouteTargetActionToHandler` method (after line 627 -- the blank line after the
preceding method's closing brace).

```csharp
private static readonly Dictionary<
    string,
    Action<V12_002, string, PositionInfo, string, ConcurrentDictionary<string, Order>, int, double>>
    _targetDispatch = new Dictionary<
        string,
        Action<V12_002, string, PositionInfo, string, ConcurrentDictionary<string, Order>, int, double>>(
        StringComparer.Ordinal)
    {
        { "market",      (self, en, p, tt, to, tc, cp) => self.ExecuteTarget_Market(en, p, tt, to, tc)       },
        { "1point",      (self, en, p, tt, to, tc, cp) => self.ExecuteTarget_OnePoint(en, p, tt, tc)          },
        { "2point",      (self, en, p, tt, to, tc, cp) => self.ExecuteTarget_TwoPoint(en, p, tt, tc)          },
        { "marketprice", (self, en, p, tt, to, tc, cp) => self.ExecuteTarget_MarketPrice(en, p, tt, tc, cp)  },
        { "breakeven",   (self, en, p, tt, to, tc, cp) => self.ExecuteTarget_Breakeven(en, p, tt, tc)         },
        { "cancel",      (self, en, p, tt, to, tc, cp) => self.ExecuteTarget_Cancel(en, p, tt, to, tc)        },
    };
```

### Field design notes
- `private static readonly` -- immutable after class init; zero runtime allocation.
  No `lock()` needed -- Dictionary is read-only after initialization (OKF: lock-free-patterns.md).
- `StringComparer.Ordinal` -- case-sensitive, byte-for-byte match, consistent with the
  original `switch (action)` behavior (no case-folding side effects).
- All 6 Action delegates accept the full 7-argument signature
  `(self, en, p, tt, to, tc, cp)` so callers never need to branch on argument count.
  Unused arguments (`to` for 1/2/breakeven; `cp` for all but marketprice) are silently
  ignored inside each lambda -- the compiler discards them with zero overhead.
- `self` is the `V12_002` instance; lambdas invoke `self.ExecuteTargetXxx(...)` which
  resolves the same virtual dispatch as the original `this.ExecuteTargetXxx(...)`.

---

## 2. Refactored RouteTargetActionToHandler Method Body

Replace the entire switch block with:

```csharp
private void RouteTargetActionToHandler(
    string action,
    string entryName,
    PositionInfo pos,
    string targetType,
    int targetNumber,
    ConcurrentDictionary<string, Order> targetOrders,
    int targetContracts,
    double currentPrice
)
{
    if (_targetDispatch.TryGetValue(action, out var handler))
        handler(this, entryName, pos, targetType, targetOrders, targetContracts, currentPrice);
    else
        Print(string.Format("[UI] Unknown target action: {0}", action));
}
```

### Method design notes
- Method signature is **unchanged** -- no callers need to be updated.
- `targetNumber` parameter is preserved in the signature (callers pass it) but is
  intentionally not forwarded; this matches the original switch which also did not
  forward it. No behavior change.
- The `default` print path is the `else` branch -- it is NOT a dispatch entry.
  This matches the dispatch requirement exactly.
- No new `public` API is introduced.
- All handler methods (`ExecuteTarget_Market`, etc.) remain private and are
  called with identical arguments to the original switch branches.

---

## 3. CYC Analysis

| Element | Delta |
|---------|-------|
| Base | +1 |
| `if (_targetDispatch.TryGetValue(...))` | +1 |
| `else` | +1 |

**Predicted CYC: 3**

Reduction: 7 → 3 (57% reduction). Well inside the Jane Street ≤8 mandate.

---

## 4. Field Placement

**File**: `src/V12_002.UI.Callbacks.cs`
**Location**: Inside the class body, **before** `RouteTargetActionToHandler` (line 628).
**Exact insert point**: After line 627 (blank line following the closing brace of the
preceding private method), before line 628 (`private void RouteTargetActionToHandler`).

The field is `static readonly` so it can live anywhere in the class body. Placing it
immediately before its sole consumer maximizes co-location readability.

---

## 5. No New Handler Methods Needed

**Confirmed: none.**

All six handler methods already exist:
- `ExecuteTarget_Market`
- `ExecuteTarget_OnePoint`
- `ExecuteTarget_TwoPoint`
- `ExecuteTarget_MarketPrice`
- `ExecuteTarget_Breakeven`
- `ExecuteTarget_Cancel`

The lambdas in `_targetDispatch` are thin call-through wrappers. They allocate at
class-load time only (static field initializer), never on the hot path call to
`RouteTargetActionToHandler`.

---

## 6. OKF Compliance Checklist

| Rule | Status |
|------|--------|
| `lock()` banned | PASS -- `static readonly` Dict; no lock needed |
| `DateTime.Now` banned | N/A |
| CYC ≤ 8 | PASS -- CYC 3 after refactor |
| No hot-path allocation | PASS -- Dict initialized once at class load |
| ASCII-only source | PASS -- all strings and comments are ASCII |
| xUnit tests required | ACTION REQUIRED -- at least 1 [Fact] per extracted helper |
| No new public API | PASS -- field and lambdas are private static |
| `StringComparer.Ordinal` | PASS -- case-sensitive, deterministic dispatch |

---

## 7. Agent Tracking

| Field | Value |
|-------|-------|
| Phase | 2 -- Architecture Planning |
| Finding ID | W9-L8-003 |
| Method | `RouteTargetActionToHandler` |
| File | `src/V12_002.UI.Callbacks.cs` |
| Current CYC | 7 |
| Target CYC | 3 |
| Approach | Static readonly Dictionary + TryGetValue dispatch |
| New types introduced | None |
| New public API | None |
| Handler methods changed | None |
| Test requirement | 1 [Fact] verifying dispatch table routes to correct handler |
