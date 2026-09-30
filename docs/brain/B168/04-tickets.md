# B168 Tickets
## COLLATERAL-STORM — Concurrent multi-stop drag kills follower stops
## Block: B168 | Design ref: DW-B168-01 | Plan: 02-architecture-plan.md (REVIEW_PASS)

---

## TICKET B168-1 — CopyEngine.cs src change

**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec req IDs:** DW-B168-01
**Depends on:** nothing (first ticket)

---

### Overview

Add one new private static predicate (`IsLeaderLegBeingChanged`), one internal
test-seam wrapper (`IsLeaderLegBeingChangedTestable`), and one 3-line guard
block inside `ResubmitCollateralLegs`. Exactly 3 edit sites. No other lines
changed. No new `lock()`. No whitespace mutations outside the 3 sites.

---

### Site A — Insert after line 3802 (closing `}` of `FindLeaderCollateralOrder`)

Add the following EXACT C# verbatim (15 lines):

```csharp
        // DW-B168-01: returns true when the leader's Stop{suffix} is mid-drag.
        // Used by ResubmitCollateralLegs to suppress collateral cancel+resubmit
        // for legs that have their own HandleAtmStopSync in flight.
        // ChangePending: drag gesture dispatched to NT8.
        // ChangeSubmitted: NT8 dispatched to broker, Accepted callback pending.
        // Both states guarantee a concurrent HandleAtmStopSync is in flight.
        // Zero-regression: single-stop drag leaves other legs in Working/Accepted
        //   -- neither state matches -> guard is false -> unchanged behaviour.
        // CYC=2: null guard(1) + expression body with state check(1) = CYC 2.
        // JS-002: bool. JS-021: no lock. JS-001: no throw. ASCII-only.
        // JS-036: no heap -- reads field on existing leaderLeg object, no ToList.
        private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
            leaderLeg != null
            && (leaderLeg.OrderState == OrderState.ChangePending
                || leaderLeg.OrderState == OrderState.ChangeSubmitted);
```

**Anchor:** Current file line 3802 is the closing `}` of `FindLeaderCollateralOrder`.
Site A inserts 15 lines occupying new lines 3803–3817.

---

### Site B — Insert immediately after Site A (after new line 3817)

Add the following EXACT C# verbatim (5 lines):

```csharp
        // Test seam: exposes private IsLeaderLegBeingChanged for unit tests.
        // CYC=1. Same pattern as IsPttStpDragCancellableTestable (line 4141).
        // DW-B168-01: nullable param mirrors production signature -- required for Null test case.
        internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
            IsLeaderLegBeingChanged(o);
```

**Anchor:** Immediately after last line of Site A (new line 3817).
Site B inserts 5 lines occupying new lines 3818–3822. Total inserted by Sites A+B: 20 lines.

---

### Site C — Insert after line 3976 (after `FindLeaderCollateralOrder` call inside `ResubmitCollateralLegs`)

**Derivation:** Current file line 3956 contains:
```
Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
```
After Sites A+B insert 20 lines before it, this line shifts to 3956 + 20 = **3976**.
Insert the guard immediately after new line 3976.

Add the following EXACT C# verbatim (3 lines):

```csharp
                // DW-B168-01: skip -- concurrent drag for this leg is in flight.
                if (IsLeaderLegBeingChanged(leaderLeg))
                    continue;
```

**Result:** The full `ResubmitCollateralLegs` method body (showing only changed area) reads:

```csharp
                Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
                // DW-B168-01: skip -- concurrent drag for this leg is in flight.
                if (IsLeaderLegBeingChanged(leaderLeg))
                    continue;
                ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
```

---

### CYC comment update — ResubmitCollateralLegs header

Update the CYC annotation in the method's header comment block:

- **Before:** `// CYC=2` (or equivalent existing annotation)
- **After:** `// DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.`
  `// CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).`
  `// CYC=3. Well within <=8.`

This is part of Site C — it is a comment-only update within the same method header,
not an additional edit site.

---

### Method signatures

```csharp
// NEW — private static predicate
private static bool IsLeaderLegBeingChanged(Order? leaderLeg)

// NEW — internal test seam
internal static bool IsLeaderLegBeingChangedTestable(Order? o)

// MODIFIED — ResubmitCollateralLegs (signature unchanged, body gains 3 lines)
private void ResubmitCollateralLegs(
    Account acc,
    Order fo,
    double newPrice,
    Dictionary<int, double> otherLegPrices,
    string excludeSuffix,
    Order leaderOrder
)
```

---

### JS rule constraints

| Rule | Constraint | How satisfied |
|------|-----------|---------------|
| JS-001 | No throw in dispatch path | `IsLeaderLegBeingChanged` is expression-body, no throw |
| JS-002 | Return bool not void | Returns `bool` |
| JS-021 | No `lock()` | Zero synchronization primitives introduced |
| JS-036 | No heap allocation in hot path | Reads existing field; no `ToList`, no new objects |
| ASCII | ASCII-only identifiers and strings | All comments and code are ASCII-only |

---

### Acceptance criteria

- No new `lock()` anywhere in `CopyEngine.cs`
- CYC delta: `IsLeaderLegBeingChanged` = 2 (new), `ResubmitCollateralLegs` 2→3 (+1)
- ASCII-only throughout; no Unicode, no curly quotes, no emoji
- Exactly 3 edit sites; no other lines changed
- No whitespace-only mutations outside the 3 edit sites
- `IsLeaderLegBeingChanged` appears exactly 3 times: definition + seam + call site

---

### Post-change step (MANDATORY)

After all edits are applied, run:

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

This re-synchronizes NinjaTrader hard links. Exit code must be 0.

---

### 7-Scan Checklist

- [ ] **SCAN-01** `grep "lock(" src/PropTraderTools/CopyEngine.cs` → 0 new matches
- [ ] **SCAN-02** CYC audit — `IsLeaderLegBeingChanged` = 2, `ResubmitCollateralLegs` = 3
- [ ] **SCAN-03** `grep` for Unicode / non-ASCII in changed lines → 0 matches
- [ ] **SCAN-04** Confirm `IsLeaderLegBeingChanged` appears exactly 3 times (definition + seam + call site)
- [ ] **SCAN-05** Confirm no whitespace-only line changes outside the 3 edit sites
- [ ] **SCAN-06** Confirm CYC comment on `ResubmitCollateralLegs` updated to `CYC=3` with `DW-B168-01` annotation
- [ ] **SCAN-07** `ptt-sync.ps1` ran successfully (exit 0)

---

---

## TICKET B168-2 — CopyEngineTests.cs test addition

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Spec req IDs:** DW-B168-01 §6
**Depends on:** B168-1 (requires `IsLeaderLegBeingChangedTestable` seam to exist)

---

### Overview

Add a new test class `DwB168Tests` (or append to the existing last class in the
file) containing exactly 4 `[Fact]` methods. All tests call via the
`IsLeaderLegBeingChangedTestable` internal seam. No NT8 runtime references in
test bodies. All order constructions use the existing `StubOrder(name, state)`
helper pattern from the existing test file.

---

### New test class — add verbatim

```csharp
public class DwB168Tests
{
    [Fact]
    public void IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue()
    {
        var order = StubOrder("test", OrderState.ChangeSubmitted);
        Assert.True(CopyEngine.IsLeaderLegBeingChangedTestable(order));
    }

    [Fact]
    public void IsLeaderLegBeingChanged_ChangePending_ReturnsTrue()
    {
        var order = StubOrder("test", OrderState.ChangePending);
        Assert.True(CopyEngine.IsLeaderLegBeingChangedTestable(order));
    }

    [Fact]
    public void IsLeaderLegBeingChanged_Working_ReturnsFalse()
    {
        var order = StubOrder("test", OrderState.Working);
        Assert.False(CopyEngine.IsLeaderLegBeingChangedTestable(order));
    }

    [Fact]
    public void IsLeaderLegBeingChanged_Null_ReturnsFalse()
    {
        Assert.False(CopyEngine.IsLeaderLegBeingChangedTestable(null));
    }
}
```

---

### Test method signatures (exact names — must match spec)

| # | Method name | Assert |
|---|------------|--------|
| 1 | `IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue` | `True` |
| 2 | `IsLeaderLegBeingChanged_ChangePending_ReturnsTrue` | `True` |
| 3 | `IsLeaderLegBeingChanged_Working_ReturnsFalse` | `False` |
| 4 | `IsLeaderLegBeingChanged_Null_ReturnsFalse` | `False` |

---

### JS rule constraints (test file)

| Rule | Constraint | How satisfied |
|------|-----------|---------------|
| JS-021 | No `lock()` | No synchronization in test code |
| ASCII | ASCII-only | All test code is ASCII-only |
| No NT8 runtime | No `NinjaTrader.Client`, no `OnExecutionUpdate` etc. | Tests use `StubOrder` + seam only |

---

### Acceptance criteria

- 4 tests PASS, 0 SKIP, 0 FAIL
- All 4 use `IsLeaderLegBeingChangedTestable` seam (not direct private access)
- No NT8 runtime dependencies in test bodies
- `StubOrder` helper used for all order constructions (3 tests; null test uses no helper)
- ASCII-only in all new test code
- Test names match spec exactly (4 names verified against table above)

---

### 7-Scan Checklist

- [ ] **SCAN-01** Confirm exactly 4 `[Fact]` methods present in the new `DwB168Tests` class in `CopyEngineTests.cs`
- [ ] **SCAN-02** All 4 test bodies call `CopyEngine.IsLeaderLegBeingChangedTestable` (not the private method directly)
- [ ] **SCAN-03** No NT8 runtime references in test bodies (no `NinjaTrader.Client`, no `OnExecutionUpdate`, no `Account.All`, etc.)
- [ ] **SCAN-04** `StubOrder` helper used for all order constructions (`ChangeSubmitted`, `ChangePending`, `Working` tests)
- [ ] **SCAN-05** ASCII-only in all new test code — no Unicode, no curly quotes, no emoji
- [ ] **SCAN-06** No `lock()` in test code
- [ ] **SCAN-07** Test names match spec exactly: all 4 names verified against the table above

---

STATUS: TICKETS_COMPLETE
