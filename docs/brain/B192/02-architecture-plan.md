# B192 Architecture Plan
# GlobalQuickAll MGC/GC Tick Default Bug Fix

**Epic**: B192
**Author**: ptt-architect (Phase 1)
**Date**: 2026-07-15
**Status**: PLAN_COMPLETE (cycle 2 -- spec amendment acknowledged)
**Preceding backlog**: docs/brain/B191/06-deferred-backlog.md

---

## 0. Spec Amendment Acknowledgment (cycle 2)

### 0.1 REVIEW_FAIL Resolution

The Phase 2 plan review (`docs/brain/B192/02-plan-review.md`) returned **REVIEW_FAIL** with
one violation:

> **T2/T3 contradicts B190 spec line 41621 and 41958**: The B190 spec explicitly prohibited
> resetting `GlobalQuickAllT1` in `SetInstrument()` due to shared singleton stomp risk across
> open panels. The B192 plan called `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` from
> `SetInstrument()`, which was the exact pattern the B190 spec rejected.

### 0.2 Director Spec Amendment (2026-07-15)

The Director has amended `specs/002-trade-copier-spec.html` at line 41621. The B190 constraint
is superseded. The amended spec reads:

> **B192 SPEC AMENDMENT (supersedes B190 note):** `GlobalQuickAllT1` IS now reset in
> `SetInstrument()` via `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)`. Director
> decision 2026-07-15: Quick ALL and QAll2t must fire with instrument-correct tick counts
> (10 for MGC/GC, 4 for MES/ES/NQ/MNQ). The B190 multi-panel stomp risk is accepted -- the
> standard usage is one instrument panel per session. Both per-panel fields `_quickT1`/`_quickT2`
> AND the global singleton are now reset. See B192 tickets T2+T3.

### 0.3 Impact on This Plan

The spec amendment resolves the sole REVIEW_FAIL violation:

| Previous violation | Resolution |
|--------------------|------------|
| T2: `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` called from `SetInstrument()` prohibited by B190 spec line 41621 | **Superseded** -- B192 spec amendment at line 41621 explicitly authorises this call with Director sign-off 2026-07-15 |
| T3: `CopyEngine.ResetGlobalQuickAll(int t1)` method should not exist per B190 spec line 41621 | **Superseded** -- same amendment; method is now the required implementation vehicle for T2 |

### 0.4 Multi-Panel Stomp Risk -- Accepted Trade-Off

The B190 stomp risk (last `SetInstrument` call wins, overwriting the singleton for all other
open panels) is **explicitly accepted** by the Director. Rationale: the standard PTT usage
pattern is one instrument panel per trading session. The per-panel `_quickT1`/`_quickT2`
fields remain independently managed; only `GlobalQuickAllT1` (the shared Quick ALL singleton)
is now also kept in sync with the most-recently-changed panel's instrument.

No architectural changes to T1, T2, or T3 designs are required. The plan proceeds as written.

---

## 1. Problem Statement

### 1.1 Root Cause Analysis

When a user clicks **Quick ALL** or **QAll2t** on an MGC (Micro Gold) panel, the tick count
applied is **4** instead of the correct **10**. The same defect applies to **GC** (full-size Gold).

Three independent failure points combine to produce this bug:

**Failure Point A -- InstrumentDefaults.GetQuickTicks missing GC entry**
File: `src/PropTraderTools/Features/PttQuickExit.cs`, lines 385-394
```csharp
// BEFORE (broken):
if (masterName.StartsWith("MES")) return (4, 8);
if (masterName.StartsWith("MGC")) return (10, 20);
return (4, 8); // GC falls through to (4,8) -- WRONG
```
`GetQuickTicks("GC")` returns `(4, 8)` because there is no explicit GC guard.
GC has the same tick size as MGC ($0.10/tick, 10 ticks = 1 point) and must return `(10, 20)`.
Additionally, ES, NQ, and MNQ have no explicit entries; they fall through to the default.
Adding explicit entries for ES/NQ/MNQ is a documentation-clarity fix with no behavior change,
but it makes the intent unambiguous and prevents future misreads.

**Failure Point B -- SetInstrument does not sync the CopyEngine singleton**
File: `src/PropTraderTools/TradeCopierPanel.cs`, lines 501-516
```csharp
// BEFORE (broken):
var ticks = InstrumentDefaults.GetQuickTicks(instrument?.MasterInstrument?.Name ?? "");
_quickT1 = ticks.t1;  // per-panel correct for MGC (10) ✅
_quickT2 = ticks.t2;
// CopyEngine.Instance._globalQuickAllT1 stays at 4 ❌ -- never updated
```
The per-panel buttons are updated correctly. The global singleton `_globalQuickAllT1` is never
reset when the instrument changes, leaving it permanently at its initialised value of 4.

**Failure Point C -- CopyEngine has no ResetGlobalQuickAll method**
File: `src/PropTraderTools/CopyEngine.cs`, line ~437
```csharp
private volatile int _globalQuickAllT1 = 4; // never re-initialised on instrument change
```
`IncrementQuickAll` and `DecrementQuickAll` exist (lines 440-458), but there is no method to
reset the value to the instrument default. Without this method, Fix B cannot be implemented.

**Failure Point D -- ResolveQuickTicks reads stale singleton**
File: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`, lines 286-296
```csharp
int t1 = engine.GlobalQuickAllT1; // reads singleton -- will be correct AFTER Fix C
```
This method itself requires no change. Once `_globalQuickAllT1` is kept in sync by Fix B+C,
`ResolveQuickTicks` will read the correct value automatically.

### 1.2 Expected Outcome After Fix

| Instrument | GetQuickTicks | Quick ALL ticks | QAll2t ticks |
|------------|---------------|-----------------|--------------|
| MES        | (4, 8)        | +4 ✅           | +4 ✅        |
| MGC        | (10, 20)      | +10 ✅          | +10 ✅       |
| GC         | (10, 20)      | +10 ✅          | +10 ✅       |
| ES         | (4, 8) explicit | +4 ✅         | +4 ✅        |
| NQ         | (4, 8) explicit | +4 ✅         | +4 ✅        |
| MNQ        | (4, 8) explicit | +4 ✅         | +4 ✅        |

---

## 2. LANE-SPLIT GATE

### Gate Questions

**Q1. Same method or within 50 lines?**
No. T1 touches `GetQuickTicks` in `PttQuickExit.cs`. T2 touches `SetInstrument` in
`TradeCopierPanel.cs`. T3 adds `ResetGlobalQuickAll` in `CopyEngine.cs`.
Three separate files, three separate methods.

**Q2. Fix B design depends on Fix A final design?**
Partial. T2 (SetInstrument sync) depends on T3 (ResetGlobalQuickAll existence) at compile time.
T1 (GetQuickTicks GC entry) is fully independent of both T2 and T3.

**Q3. Each fix has standalone value if the other is blocked?**
T1 YES: Adding GC to GetQuickTicks fixes the per-panel Quick exit buttons for GC independently.
T3 alone: Zero user-visible value (adds a method nobody calls yet).
T2 alone: Compile error (calls a method that does not exist without T3).
Conclusion: T1 is independent (Lane A). T2 and T3 must ship as a coupled pair (Lane B).

**Q4. Each fix has an independent SIM verification path?**
T1: Load a GC panel in SIM, click per-panel Quick exit, verify 10-tick target. Independent.
T2+T3: Load MGC panel in SIM, click Quick ALL, verify 10-tick target. Independent.

### LANE-SPLIT GATE RESULT: LANES-APPROVED

- **Lane A**: T1 (PttQuickExit.cs -- InstrumentDefaults)
  Independent. Can be implemented, tested, and merged in isolation.
- **Lane B**: T3 then T2 (CopyEngine.cs then TradeCopierPanel.cs)
  Coupled pair. T3 must be implemented before T2 (compile-time dependency).
  Can be merged together in one commit or sequentially.

---

## 3. Ticket Designs

### T1 -- Lane A: InstrumentDefaults.GetQuickTicks (PttQuickExit.cs)

**File**: `src/PropTraderTools/Features/PttQuickExit.cs`
**Spec Requirements**: SR-B192-01 (GC -> (10,20)), SR-B192-02 (explicit ES/NQ/MNQ -> (4,8))
**Deferred backlog closed**: None (DW-B183-03 is unaffected -- IsLaneEEligible not touched)

#### Method Signature (unchanged)
```csharp
internal static (int t1, int t2) GetQuickTicks(string masterName)
```

#### Implementation (AFTER)
```csharp
/// <summary>
/// GetQuickTicks: returns (T1 ticks, T2 ticks) for the given master instrument name.
/// MES/ES: (4, 8) = 1pt/2pt at 0.25/tick.
/// NQ/MNQ: (4, 8) = 1pt/2pt at 0.25/tick.
/// MGC/GC: (10, 20) = 1pt/2pt at 0.1/tick.
/// Default: (4, 8).
/// CYC=7: null(1) + MES(2) + ES(3) + NQ(4) + MNQ(5) + MGC(6) + GC(7).
/// JS-002: returns tuple (never null). ASCII-only strings.
/// </summary>
internal static (int t1, int t2) GetQuickTicks(string masterName)
{
    if (string.IsNullOrEmpty(masterName))
        return (4, 8); // (1) null/empty guard
    if (masterName.StartsWith("MES"))
        return (4, 8); // (2) Micro E-mini S&P 500
    if (masterName.StartsWith("ES"))
        return (4, 8); // (3) B192: explicit E-mini S&P 500
    if (masterName.StartsWith("NQ"))
        return (4, 8); // (4) B192: explicit E-mini Nasdaq 100
    if (masterName.StartsWith("MNQ"))
        return (4, 8); // (5) B192: explicit Micro E-mini Nasdaq 100
    if (masterName.StartsWith("MGC"))
        return (10, 20); // (6) Micro Gold: 1pt/2pt at 0.1/tick
    if (masterName.StartsWith("GC"))
        return (10, 20); // (7) B192: Gold: 1pt/2pt at 0.1/tick
    return (4, 8); // default
}
```

**IMPORTANT ordering note**: `"MNQ".StartsWith("NQ")` = false (MNQ starts with 'M'), so placing
NQ before MNQ is safe. `"ES".StartsWith("MES")` = false (ES starts with 'E'), so placing MES
before ES is safe. All ordering is verified correct.

#### CYC Budget
- Before: CYC=3 (null + MES + MGC)
- After: CYC=7 (null + MES + ES + NQ + MNQ + MGC + GC)
- JS-066 ceiling: 8
- Headroom: 1 branch remaining ✅

#### JS Rule Compliance
- JS-001: No throw. ✅
- JS-002: Returns (int,int) tuple. Never null. ✅
- JS-021: No lock(). Static method, no shared state. ✅
- JS-033: Synchronous, not async. ✅
- JS-066: CYC=7 <= 8. ✅

---

### T3 -- Lane B Step 1: CopyEngine.ResetGlobalQuickAll (CopyEngine.cs)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Spec Requirements**: SR-B192-04 (ResetGlobalQuickAll method)
**Insertion point**: After `DecrementQuickAll` method (after line ~458), before `GlobalQuickAllBufferChanged` event declaration.

#### Method Signature (NEW)
```csharp
internal void ResetGlobalQuickAll(int t1)
```

#### Implementation
```csharp
/// <summary>
/// ResetGlobalQuickAll: resets the global Quick ALL tick count to the instrument default.
/// Called from TradeCopierPanel.SetInstrument() when the panel switches instruments.
/// Fires GlobalQuickAllBufferChanged via Dispatcher to update any subscribed UI.
/// CYC=1: single straight-line path.
/// JS-021: no lock. Volatile int assignment is atomic. Dispatcher marshals event to UI thread.
/// JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
/// </summary>
internal void ResetGlobalQuickAll(int t1)
{
    _globalQuickAllT1 = t1;
    int v = _globalQuickAllT1;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        GlobalQuickAllBufferChanged?.Invoke(v)
    ); // B192: sync singleton to instrument default on instrument change
}
```

#### Design Notes
- `_globalQuickAllT1` is `private volatile int`. A single volatile int assignment is atomic on
  .NET (no lock required). This is identical to the write pattern in `IncrementQuickAll` and
  `DecrementQuickAll` (lines 443, 453).
- The local variable `v` captures the value after assignment. This snapshot pattern prevents a
  race where another thread calls Increment/Decrement between the field write and the lambda
  execution — matching the existing pattern in both Increment and Decrement methods.
- `Dispatcher.InvokeAsync` marshals the event callback to the WPF UI thread. Required because
  `GlobalQuickAllBufferChanged` may update UI elements. This is the established pattern
  annotated as `HOTFIX-DISPATCH-FIX-01` in the existing codebase.
- No range guard (e.g., `t1 > 0 && t1 < 99`): the value comes exclusively from
  `InstrumentDefaults.GetQuickTicks` which returns only validated constants (4 or 10).
  Defensive bounds would add CYC without value.

#### CYC Budget
- CYC=1 (single path, no branches) ✅

#### JS Rule Compliance
- JS-001: No throw. ✅
- JS-002: void return. ✅
- JS-021: No lock(). Volatile int, Dispatcher marshaling. ✅
- JS-033: Synchronous void (the lambda inside InvokeAsync is the existing pattern, not async void on the method itself). ✅
- JS-066: CYC=1 <= 8. ✅

---

### T2 -- Lane B Step 2: TradeCopierPanel.SetInstrument (TradeCopierPanel.cs)

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Spec Requirements**: SR-B192-03 (SetInstrument syncs GlobalQuickAllT1)
**Dependency**: T3 must be implemented first (ResetGlobalQuickAll must exist to compile).

#### Method Signature (unchanged)
```csharp
public void SetInstrument(Instrument instrument)
```

#### Implementation (AFTER -- single line addition shown in context)
```csharp
public void SetInstrument(Instrument instrument)
{
    _instrument = instrument;
    if (_statusText != null && instrument != null)
        _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";

    // B190-T1: reset tick defaults when instrument changes
    var ticks = InstrumentDefaults.GetQuickTicks(
        instrument?.MasterInstrument?.Name ?? "");
    _quickT1 = ticks.t1;
    _quickT2 = ticks.t2;
    if (_quickBtn != null)
        _quickBtn.Content = FormatBuffer("Quick", _quickT1);
    if (_instr2tBtn != null)
        _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1);
    // B192-T2: sync global singleton so Quick ALL uses instrument-correct tick count
    CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1);
}
```

The only change is the final line: `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1);`

#### Design Notes
- Null-conditional operator `?.` is used: if `CopyEngine.Instance` is null (pathological case
  where the engine has not been initialized), the call is a no-op. This matches the defensive
  null-check pattern used in `PttGlobalQuickExit.ResolveQuickTicks` (line 289).
- `SetInstrument` runs on the UI thread (it directly assigns `_statusText.Text`). Therefore
  calling `ResetGlobalQuickAll` directly is safe. `ResetGlobalQuickAll` internally dispatches
  its event callback to the UI thread via `Dispatcher.InvokeAsync`.
- No new using directives required. `CopyEngine` is already in scope in `TradeCopierPanel.cs`.
- CYC of `SetInstrument` is unchanged: `?.` is a null-conditional, not a new branch in
  cyclomatic complexity terms (it is a null-safe dereference, equivalent to an existing null
  pattern already in the method body).

#### CYC Budget
- SetInstrument CYC: unchanged from pre-B192 (single new statement, no branches added). ✅

#### JS Rule Compliance
- JS-001: No throw. ✅
- JS-002: No return null. ✅
- JS-021: No lock(). ✅
- JS-033: Synchronous void. ✅
- JS-066: CYC unchanged. ✅

---

## 4. Data Flow (After Fix)

```
User loads MGC panel
       |
       v
TradeCopierPanel.SetInstrument("MGC")
       |
       v
InstrumentDefaults.GetQuickTicks("MGC") --> (10, 20)      [T1: GC also correct now]
       |
       +---> _quickT1 = 10, _quickT2 = 20   [per-panel buttons update]
       |
       +---> CopyEngine.Instance?.ResetGlobalQuickAll(10)  [T2: NEW]
                     |
                     v
             CopyEngine._globalQuickAllT1 = 10             [T3: volatile write]
                     |
                     v
             Dispatcher.InvokeAsync(
               GlobalQuickAllBufferChanged?.Invoke(10))    [T3: UI notification]

User clicks Quick ALL
       |
       v
PttGlobalQuickExit.ResolveQuickTicks(instr)
       |
       v
engine.GlobalQuickAllT1  --> 10  (was 4 before fix)       [NO CHANGE TO THIS METHOD]
       |
       v
Quick ALL submits 10-tick target ✅
```

---

## 5. Test Strategy

### Lane A Tests (T1 -- xUnit, PttQuickExit.cs)

All tests are pure unit tests on the static `InstrumentDefaults.GetQuickTicks` method.
No NT8 objects required. No mocking required.

| Test Name | Assert |
|-----------|--------|
| `Fact_GetQuickTicks_GC_ReturnsTen` | GetQuickTicks("GC") == (10, 20) |
| `Fact_GetQuickTicks_GC_MixedCase_ReturnsTen` | GetQuickTicks("GC 06-26") == (10, 20) (prefix match) |
| `Fact_GetQuickTicks_ES_ReturnsFour` | GetQuickTicks("ES 06-26") == (4, 8) |
| `Fact_GetQuickTicks_NQ_ReturnsFour` | GetQuickTicks("NQ 06-26") == (4, 8) |
| `Fact_GetQuickTicks_MNQ_ReturnsFour` | GetQuickTicks("MNQ 06-26") == (4, 8) |
| `Fact_GetQuickTicks_MGC_Unchanged_ReturnsTen` | GetQuickTicks("MGC 06-26") == (10, 20) (regression) |
| `Fact_GetQuickTicks_MES_Unchanged_ReturnsFour` | GetQuickTicks("MES 06-26") == (4, 8) (regression) |
| `Fact_GetQuickTicks_Empty_ReturnsFour` | GetQuickTicks("") == (4, 8) |
| `Fact_GetQuickTicks_Null_ReturnsFour` | GetQuickTicks(null) == (4, 8) |
| `Fact_GetQuickTicks_Unknown_ReturnsFour` | GetQuickTicks("ZB 06-26") == (4, 8) |

### Lane B Tests (T3 -- xUnit, CopyEngine.cs)

`ResetGlobalQuickAll` can be unit-tested by calling the method and reading `GlobalQuickAllT1`
(the internal property accessor).

| Test Name | Assert |
|-----------|--------|
| `Fact_ResetGlobalQuickAll_SetsFieldToFour` | After Reset(4), GlobalQuickAllT1 == 4 |
| `Fact_ResetGlobalQuickAll_SetsFieldToTen` | After Reset(10), GlobalQuickAllT1 == 10 |
| `Fact_ResetGlobalQuickAll_OverwritesPreviousValue` | After Increment() x3 then Reset(4), GlobalQuickAllT1 == 4 |

### Lane B Integration (T2 -- SIM verification)

`SetInstrument` requires a live NT8 `Instrument` object. No pure unit test is possible.
SIM verification steps:
1. Load PTT AddOn in NT8 SIM environment.
2. Add a panel and set instrument to MGC (Micro Gold).
3. Verify Quick ALL label shows "10" (or QuickAll button fires 10-tick target).
4. Switch instrument to GC (full Gold) on same or second panel.
5. Verify Quick ALL label shows "10".
6. Switch instrument to ES. Verify Quick ALL label shows "4".
7. Switch instrument to NQ. Verify Quick ALL label shows "4".

---

## 6. 7-Scan Checklist Specification

The engineer MUST run all 7 scans before issuing VERIFY_PASS. All must return zero matches
in the changed lines.

| Scan | Pattern | Target Files |
|------|---------|--------------|
| SCAN-01 | `lock\s*\(` | All three changed files |
| SCAN-02 | `throw\s+new\s+\w+Exception\(` | All three changed files |
| SCAN-03 | `return\s+null\s*;` | All three changed files |
| SCAN-04 | `DateTime\.Now` (not UtcNow) | All three changed files |
| SCAN-05 | `#[0-9A-Fa-f]{6}` or `Color\.FromRgb\(` | All three changed files |
| SCAN-06 | `FontFamily` | All three changed files |
| SCAN-07 | Non-ASCII characters in string literals or identifiers | All three changed files |

Expected result: 0 matches on all scans in all changed lines. ✅

---

## 7. Deferred Backlog Status

### Items from B191 Deferred Backlog

| DW ID | Priority | Status | Notes |
|-------|----------|--------|-------|
| DW-B183-03 | P1 | OPEN (unchanged) | IsLaneEEligible CYC=8 freeze. B192 does NOT touch IsLaneEEligible. No action required. |

### New Items Opened by B192

None anticipated. All three changes are surgical and confined to their respective methods.
No new architectural debt introduced.

---

## 8. Component Summary

| Component | File | Change Type | CYC Before | CYC After |
|-----------|------|-------------|-----------|-----------|
| `InstrumentDefaults.GetQuickTicks` | `PttQuickExit.cs` | Modify (add 4 if-guards + update XML doc) | 3 | 7 |
| `CopyEngine.ResetGlobalQuickAll` | `CopyEngine.cs` | Add new method | N/A | 1 |
| `TradeCopierPanel.SetInstrument` | `TradeCopierPanel.cs` | Modify (add 1 line) | unchanged | unchanged |

**Total files changed**: 3
**Total new methods**: 1 (ResetGlobalQuickAll)
**Total methods modified**: 2 (GetQuickTicks, SetInstrument)
**NT8 API surface**: No new NT8 APIs. Dispatcher.InvokeAsync pattern is existing/established.
**Lock-free**: Yes. volatile int assignment + Dispatcher event dispatch. No lock() anywhere.

---

## 9. Engineer Execution Order

```
Lane A (independent):
  T1 -- PttQuickExit.cs: modify GetQuickTicks
      -> xUnit tests pass
      -> SCAN-01..07 pass
      -> Commit: "B192-T1: add GC->10 and explicit ES/NQ/MNQ->4 in InstrumentDefaults"

Lane B (coupled, T3 before T2):
  T3 -- CopyEngine.cs: add ResetGlobalQuickAll
      -> xUnit tests pass
      -> SCAN-01..07 pass
  T2 -- TradeCopierPanel.cs: add ResetGlobalQuickAll call in SetInstrument
      -> SIM verification pass
      -> SCAN-01..07 pass
      -> Commit: "B192-T2T3: sync GlobalQuickAllT1 to instrument default on SetInstrument"
```

Lanes A and B may be committed independently or together. Lane B MUST have T3 before T2.

---

*Written by: ptt-architect*
*Epic: B192*
*Phase: 1 (Architecture Planning)*
*Status: PLAN_COMPLETE*
*Supersedes: N/A (first plan for B192)*
