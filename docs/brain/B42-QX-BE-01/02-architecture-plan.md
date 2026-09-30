# B42-QX-BE-01 — Architecture Plan
## Quick All / BE All any-order interaction repair

**Status**: REVIEW_PASS
**Block**: B42-QX-BE-01
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Director workspace**: `c:\WSGTA\universal-or-strategy-director\`
**Author**: ptt-architect
**Date**: 2026-08-05

---

## RULES CATALOG GATE — PASS

| Rule | Description | Verdict |
|------|-------------|---------|
| JS-001 | No throw in hot paths | PASS — no new throw introduced |
| JS-002 | No return null | PASS — new method returns bool |
| JS-021 | No lock() | PASS — no lock introduced |
| JS-033 | No async void | PASS — no async method introduced |
| NT8-006 | No LINQ in PttBreakEven.cs | PASS — IsPttQxTarget uses string methods only |
| NT8-007 | arg11 = CustomOrder null | PASS — no new CreateOrder calls |
| NT8-013 | DateTime.MaxValue for GTC | PASS — no new GTC orders |
| NT8-014 | PTT- prefix on order names | PASS — no new order names |
| NT8-049 | arg6/arg7 swap guard | PASS — no new CreateOrder calls |

---

## ROOT CAUSE SUMMARY

### Direction 1 — Quick All then BE All

`PttBreakEven.SnapshotTargetsLocal()` filters `acc.Orders` with:

```csharp
if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue;
```

`IsAtmTargetName()` accepts only ATM slot names (`"Target1"` .. `"Target9"`).
After Quick All fires, live target orders are named `"PTT-QX-T1"`, `"PTT-QX-T2"`, `"PTT-QX-T3"`.
Those names fail `IsAtmTargetName` → snapshot returns 0 targets →
`SubmitBeTargetsLocal` hits the 0-targets branch → submits a bare stop with no target pairs.

### Direction 2 — BE All then Quick All

`CopyEngine.CancelQxBrackets()` at line 2229:

```csharp
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: false, cancelPttQx: true);
```

`cancelPttBe: false` means `CancelStaleBrackets` excludes all `"PTT-BE-*"` orders.
After BE All fires, live orders are `"PTT-BE-Stop-1"`, `"PTT-BE-Target-1"`, etc.
When Quick All fires next, `CancelQxBrackets` runs but those orders survive → they compete
with the new `"PTT-QX-T1"` etc. orders for the same position.

---

## COMPONENT MAP

| Component | File (Wave workspace) | Role |
|-----------|----------------------|------|
| `PttBreakEven` | `Features/PttBreakEven.cs` | Fix T1 — snapshot filter + new predicate |
| `CopyEngine` | `CopyEngine.cs` | Fix T2 — CancelQxBrackets flag change |
| `CopyEngineTests` | `CopyEngineTests.cs` | xUnit [Fact] tests for both fixes |

---

## FIX T1 — PttBreakEven.cs

### New method: `IsPttQxTarget(string name)`

**Location**: immediately after `IsAtmTargetName()` (line ~248 in Wave workspace)

**Signature**:
```csharp
private static bool IsPttQxTarget(string name)
```

**Contract**:
- Returns `true` if `name` is exactly `"PTT-QX-T1"`, `"PTT-QX-T2"`, or `"PTT-QX-T3"`.
- These are Limit orders; `LimitPrice`, `Quantity`, and `OrderAction` are all readable
  from a Working Order object and are used identically to the ATM target snapshot.
- `IsAtmTargetName()` MUST NOT be changed — it is an invariant.
- NT8-006: ZERO LINQ — only `null` check, `.Length`, char indexer `name[N]` comparisons.
- JS-001: no throw. JS-002: returns bool (not nullable).

**Implementation**:
```csharp
/// <summary>
/// Return true if name is a PTT Quick Exit target order (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3).
/// These are plain GTC Limit orders; LimitPrice + Quantity + OrderAction are readable.
/// NT8-006: NO LINQ -- string primitives only.
/// CYC=2: (1) null/exact-length guard, (2) char-by-char prefix+digit check.
/// JS-021: no lock. JS-002: returns bool.
/// </summary>
private static bool IsPttQxTarget(string name)
{
    if (name == null || name.Length != 9) return false;                     // (1)
    return name[0] == 'P' && name[1] == 'T' && name[2] == 'T'
           && name[3] == '-' && name[4] == 'Q' && name[5] == 'X'
           && name[6] == '-' && name[7] == 'T'
           && name[8] >= '1' && name[8] <= '3';                            // (2)
}
```

**CYC = 2**: one if-guard branch + one return-expression (compound `&&` is single decision in counting).

### Modified filter in `SnapshotTargetsLocal()`

**Current line 266** (Wave workspace):
```csharp
if (!stateOk || !instrOk || !IsAtmTargetName(o.Name)) continue;
```

**Replacement**:
```csharp
if (!stateOk || !instrOk || (!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name))) continue;
```

**Rationale**: the negated-AND pattern means: skip if neither an ATM target name nor a QX target name.
A `"PTT-QX-T1"` order now satisfies `IsPttQxTarget` and is included in the snapshot.

**CYC of SnapshotTargetsLocal stays at 3**: the modification extends an existing compound
condition — no new branch node is added.

**No other changes to PttBreakEven.cs.**

---

## FIX T2 — CopyEngine.cs

### Modified `CancelQxBrackets()`

**Current lines 2229-2230** (Wave workspace):
```csharp
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: false, cancelPttQx: true);
```

**Replacement**:
```csharp
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: true, cancelPttQx: true);
```

**Rationale**: `cancelPttBe: true` includes `"PTT-BE-*"` orders in the cancel pass.
`CancelStaleBrackets` already handles this flag — its internal `.Where()` filter uses:
`(cancelPttBe || !o.Name.StartsWith("PTT-BE-"))`. Flipping the argument to `true` removes
the exclusion, so any surviving BE bracket orders are swept before Quick All submits new QX orders.

**CYC of CancelQxBrackets stays at 1**: single expression-body delegation, no new branch.
**CancelStaleBrackets body is unchanged**: only the call-site argument differs.

---

## DATA FLOW

### Direction 1 (corrected)

```
[BE All button, UI thread]
  PttBreakEven.Execute(ctx)
    foreach acc in ctx.AllAccounts
      SnapshotTargetsLocal(acc, ctx.Instrument)
        foreach Order o in acc.Orders
          stateOk:  Working || Accepted
          instrOk:  FullName match
          [NEW] include if IsAtmTargetName(o.Name) || IsPttQxTarget(o.Name)
          // "PTT-QX-T1" passes IsPttQxTarget -> snapshot collects Price/Qty/Action
        returns [ (price1,qty1,Sell), (price2,qty2,Sell), ... ]
      CancelStaleBracketsLocal(acc, ctx.Instrument)  -- wipes PTT-QX-T* (still Working)
      SubmitBeTargetsLocal(acc, ..., targets=[...], seq)
        // targets.Count > 0 -> per-pair OCO stop+target loop executes
        // PTT-BE-Stop-1 + PTT-BE-Target-1 submitted for tranche 1, etc.
```

### Direction 2 (corrected)

```
[Quick All button, UI thread]
  PttQuickExit.Execute(ctx)
    foreach acc
      CopyEngine.CancelQxBrackets(acc, instr)
        [NEW] CancelStaleBrackets(acc, instr, cancelPttBe:true, cancelPttQx:true)
          // PTT-BE-Stop-1, PTT-BE-Target-1 included in cancel list
          // acc.Cancel([...]) clears all BE and QX working orders
      // SubmitQuickExitBrackets -> PTT-QX-T1/T2/T3 submitted cleanly
```

---

## INVARIANTS CONFIRMED

| Invariant | Status |
|-----------|--------|
| `IsAtmTargetName()` signature and body unchanged | CONFIRMED |
| `CancelStaleBrackets()` signature and body unchanged | CONFIRMED |
| Single-button ATM-only path unchanged | CONFIRMED — `IsAtmTargetName` still fires first; `IsPttQxTarget` never matches ATM names (`"Target1"` has length 7 < 9) |
| Single-button QX-only path: no regression | CONFIRMED — `cancelPttBe: true` with no PTT-BE-* orders present is a no-op |
| Single-button BE-only path: no regression | CONFIRMED — `IsPttQxTarget` returns false when no QX orders present |
| No new order naming conventions | CONFIRMED |
| No new state fields | CONFIRMED |
| NT8-006 (no LINQ in PttBreakEven.cs) | CONFIRMED |
| CYC <= 8 on all touched methods | CONFIRMED — IsPttQxTarget=2, SnapshotTargetsLocal=3, CancelQxBrackets=1 |

---

## THREADING MODEL

Both fixes operate exclusively on the **UI thread** (button handler path). No new
thread-crossing paths are introduced.

- `IsPttQxTarget` is `private static` — pure computation, no shared state.
- `SnapshotTargetsLocal` iterates `acc.Orders` on the UI thread, same as B36.
- `CancelQxBrackets` delegates to `CancelStaleBrackets` which calls `acc.Cancel()` —
  same pattern used throughout `CopyEngine.cs` for all button-driven cancels.
- No `lock()`, no `ConcurrentQueue`, no `Dispatcher.InvokeAsync` required.

---

## NT8 API SURFACE

| API | Used in | Confirmed |
|-----|---------|-----------|
| `Order.Name` (string) | `IsPttQxTarget` call site in `SnapshotTargetsLocal` | Confirmed — same field used throughout `PttBreakEven.cs` and `CopyEngine.cs` |
| `Order.LimitPrice`, `Order.Quantity`, `Order.OrderAction` | Existing `result.Add()` in `SnapshotTargetsLocal` — unchanged | Confirmed at lines 268-271 of `PttBreakEven.cs` |
| `CancelStaleBrackets(Account, Instrument, bool, bool)` | `CancelQxBrackets` call site | Confirmed at line 1779-1780 of `CopyEngine.cs` |
| `string`, `string.Length`, char indexer `name[N]` | `IsPttQxTarget` | Standard .NET Framework 4.8 — no NT8-specific risk |

No new NT8 API surfaces introduced.

---

## XUNIT TEST PLAN

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
**Framework**: xUnit — `[Fact]` only, never NUnit or MSTest.

### T1 tests — `IsPttQxTarget` predicate (internal static, exposed via `InternalsVisibleTo`)

```
[Fact] IsPttQxTarget_Null_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsPttQxTarget(null))

[Fact] IsPttQxTarget_Empty_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsPttQxTarget(""))

[Fact] IsPttQxTarget_TooShort_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsPttQxTarget("PTT-QX-"))   // length 7 < 9

[Fact] IsPttQxTarget_T1_ReturnsTrue
  Assert.True(PttBreakEven_Testable.IsPttQxTarget("PTT-QX-T1"))

[Fact] IsPttQxTarget_T2_ReturnsTrue
  Assert.True(PttBreakEven_Testable.IsPttQxTarget("PTT-QX-T2"))

[Fact] IsPttQxTarget_T3_ReturnsTrue
  Assert.True(PttBreakEven_Testable.IsPttQxTarget("PTT-QX-T3"))

[Fact] IsPttQxTarget_T4_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsPttQxTarget("PTT-QX-T4"))   // only T1/T2/T3 valid

[Fact] IsPttQxTarget_Target1_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsPttQxTarget("Target1"))     // ATM name must not match

[Fact] IsPttQxTarget_WrongPrefix_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsPttQxTarget("PTT-BE-T1"))   // BE name must not match
```

### T1 regression test — `IsAtmTargetName` unchanged

```
[Fact] IsAtmTargetName_Target1_ReturnsTrue
  Assert.True(PttBreakEven_Testable.IsAtmTargetName("Target1"))

[Fact] IsAtmTargetName_Target9_ReturnsTrue
  Assert.True(PttBreakEven_Testable.IsAtmTargetName("Target9"))

[Fact] IsAtmTargetName_PttQxT1_ReturnsFalse
  Assert.False(PttBreakEven_Testable.IsAtmTargetName("PTT-QX-T1"))  // must not cross-match
```

### T2 tests — `CancelQxBrackets` includes PTT-BE-* orders

Since `CancelStaleBrackets` uses LINQ on `acc.Orders` (real NT8 objects), the T2 test
verifies the flag semantics using `IsBeNameCancelledByQxBrackets`, a thin testable helper:

```
[Fact] CancelQxBrackets_IncludesPttBeOrders
  // Arrange: stub order with Name="PTT-BE-Stop-1", state=Working
  // Act: pass through CancelStaleBrackets(cancelPttBe:true, cancelPttQx:true) logic check
  // Assert: order is included in stale list
  // Implementation: extract the filter predicate into a testable static method, or
  //   use a private helper IsCancelledByQxBrackets(string name) that applies the same logic.

[Fact] CancelQxBrackets_IncludesPttQxOrders
  // Same pattern, name="PTT-QX-T1"

[Fact] CancelQxBrackets_ExcludesAtmSlotOrders
  // name="Entry1" or "Target1" -- ATM slots that CancelStaleBrackets never includes
  // (state=Working + neither PTT-BE- nor PTT-QX- pattern = included by default;
  //   this test confirms no regression on default bracket cancel)
```

---

## 7-SCAN CHECKLIST (Engineer contract for Phase 5 execution)

| Scan | Requirement | Files |
|------|-------------|-------|
| SCAN-01 | No `lock(` anywhere in modified files | PttBreakEven.cs, CopyEngine.cs |
| SCAN-02 | No `async void` (non-handler) in modified files | PttBreakEven.cs, CopyEngine.cs |
| SCAN-03 | No `throw new` in hot paths (IsPttQxTarget, SnapshotTargetsLocal, CancelQxBrackets) | PttBreakEven.cs, CopyEngine.cs |
| SCAN-04 | No `return null` from non-nullable return types | PttBreakEven.cs (bool return) |
| SCAN-05 | `IsAtmTargetName()` body unchanged (diff shows zero lines changed in that method) | PttBreakEven.cs |
| SCAN-06 | `CancelStaleBrackets()` body unchanged (diff shows zero lines changed in that method) | CopyEngine.cs |
| SCAN-07 | NT8-006: no LINQ import or usage added to PttBreakEven.cs (grep `using System.Linq` + `.Where\|\.Select\|\.Any\|\.ToList` in PttBreakEven.cs) | PttBreakEven.cs |

---

## BUILD TAG UPDATE

Engineer must update `PttBuild.Tag` in `CopyEngine.cs`:

```csharp
internal const string Tag = "PTT-COPIER B42 | qx-be-interaction | 2026-08-05";
```

---

## SUMMARY

Two atomic, non-structural fixes:

1. **T1 — `PttBreakEven.cs`**: add private static `IsPttQxTarget(string)` predicate (CYC=2)
   and extend the `SnapshotTargetsLocal` filter condition to include QX target names alongside
   ATM target names. No other changes.

2. **T2 — `CopyEngine.cs`**: change `cancelPttBe: false` → `cancelPttBe: true` in the
   `CancelQxBrackets` one-liner. No structural change.

Total modified lines in source: ~3 lines changed + ~10 lines added.
