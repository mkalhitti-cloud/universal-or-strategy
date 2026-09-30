# B108-T1 Completion Report
**Engineer**: ptt-engineer
**Ticket**: B108-T1 (DW-B107 fix — SnapshotBeTargets extraction + cap-at-3)
**Epic**: B108
**Date**: 2026-08-11
**Verdict**: BUILD_PASS

---

## Changes Implemented

### CHANGE A — Insert `SnapshotBeTargets` private method
**Location**: `src/PropTraderTools/CopyEngine.cs` between L3323 (end of `CountLeaderTargets`) and L3325 (start of `MoveStopToBreakEven` comment block).
**New lines inserted**: ~47 lines (L3326–L3371 after patch).
**Description**: New private method `SnapshotBeTargets(Account acc, Instrument instrument)` performs a two-pass native-first collect of ATM target orders before cancelling, extracted from the inline foreach loop that previously lived inside `MoveStopToBreakEven`. Null guard returns empty list (JS-002), stateOk covers all 7 states (DW-B79-01 + REPAIR-09 DW-B79-05), isNative and isPtt predicates preserve all prior HOTFIX-MSTBE-QX-TARGETS-01 logic. CYC=7.

### CHANGE B1 — Update CYC annotation on `MoveStopToBreakEven`
**Location**: `src/PropTraderTools/CopyEngine.cs` L3271–3273 (after patch).
**Before**:
```
// CYC=8: IsFlat(1) + tickSize/pos guard(2) + snapshot-foreach(3) + stateOk(4) + instrOk(5)
//        + cancel-try(6) + 0-targets branch(7) + targets-for-loop(8).
```
**After**:
```
// CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4)
//        + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7).
// DW-B107: Step A extracted to SnapshotBeTargets; while cap reduces stale residue.
```

### CHANGE B2 — Replace Step A foreach loop with `SnapshotBeTargets` call
**Location**: `src/PropTraderTools/CopyEngine.cs` — replaced ~50-line foreach block (old L3373–L3422) with 4-line comment + single call.
**Before**: 50-line inline `var targets = new List<...>(); foreach (Order o in acc.Orders) { ... }` block.
**After**:
```csharp
// -- Step A: snapshot ATM target orders BEFORE cancelling anything ----
// DW-B107: extracted to SnapshotBeTargets to keep MoveStopToBreakEven CYC=7.
// Two-pass native-first collect: native Target1..9 take priority over
// stale PTT-QX-T*/PTT-BE-Target-* residues (same logic as DW-B106).
var targets = SnapshotBeTargets(acc, instrument); // (3)
```

### CHANGE C — Insert while cap (max 3 targets)
**Location**: `src/PropTraderTools/CopyEngine.cs` — immediately after `var targets = SnapshotBeTargets(acc, instrument);`, before `PttBreakEvenSwap.Execute(...)`.
**Inserted**:
```csharp
// DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
// Prevents stale partial-fill residue submitting extra OCO pairs.
// No LINQ -- while-loop trim per JS zero-alloc mandate.
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);
```

---

## 7-Scan Results

### SCAN-01: `lock(` check — PASS
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("`
**Output**:
```
src\PropTraderTools\CopyEngine.cs:1903:        // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
```
**Analysis**: One pre-existing hit at L1903 — inside a comment (`// CYC=5: fo null(1),...`), not a code `lock(` statement. Zero new `lock(` in B108 code. **PASS**.

### SCAN-02: `async void` check — PASS
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void "`
**Output**: (no output — zero results)
**Analysis**: Zero matches anywhere in file. **PASS**.

### SCAN-03: `return null` check — PASS
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null;"`
**Output**:
```
src\PropTraderTools\CopyEngine.cs:1509:            return null;
src\PropTraderTools\CopyEngine.cs:2004:            return null;
src\PropTraderTools\CopyEngine.cs:2050:            return null;
src\PropTraderTools\CopyEngine.cs:3162:                return null; // Change 8: null guard
src\PropTraderTools\CopyEngine.cs:3168:            return null;
src\PropTraderTools\CopyEngine.cs:3231:            return null;
src\PropTraderTools\CopyEngine.cs:4057:            return null;
```
**Analysis**: 7 pre-existing hits, all outside B108 scope. `SnapshotBeTargets` returns `nativeTargets` (empty list) on null guard — never `return null`. **PASS**.

### SCAN-04: Non-ASCII check — PASS
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"`
**Output**:
```
src\PropTraderTools\CopyEngine.cs:316:  (pre-existing)
src\PropTraderTools\CopyEngine.cs:317:  (pre-existing)
src\PropTraderTools\CopyEngine.cs:2880: (pre-existing)
src\PropTraderTools\CopyEngine.cs:2881: (pre-existing)
```
**Analysis**: 4 pre-existing hits. Zero non-ASCII characters in any B108 code. **PASS**.

### SCAN-05: CYC check — PASS
**Method**: Manual branch count.

| Method | Branch Nodes | CYC | Limit | Status |
|--------|-------------|-----|-------|--------|
| `SnapshotBeTargets` | null guard(1) + foreach(2) + o==null continue(3) + stateOk gate(4) + instrOk+type gate(5) + if(isNative)(6) + else if(isPtt)(7) | **7** | 8 | **PASS** |
| `MoveStopToBreakEven` | IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4) + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7) | **7** | 8 | **PASS** |

Both methods ≤ 8. **PASS**.

### SCAN-06: LINQ check — PASS
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\.Take\(|\.GetRange\(|\.Where\(|\.Select\("`
**Output**: (no output — zero results)
**Analysis**: Zero LINQ calls anywhere in file. While-loop cap used instead (JS zero-alloc mandate). **PASS**.

### SCAN-07: stateOk 7-state completeness — PASS
**Method**: Manual inspection of `SnapshotBeTargets` stateOk block (L3342–3349).

| State | Line | Present |
|-------|------|---------|
| `OrderState.Working` | L3343 | ✅ |
| `OrderState.Accepted` | L3344 | ✅ |
| `OrderState.Submitted` | L3345 | ✅ |
| `OrderState.Initialized` | L3346 | ✅ |
| `OrderState.TriggerPending` | L3347 | ✅ |
| `OrderState.ChangeSubmitted` | L3348 | ✅ |
| `OrderState.CancelSubmitted` | L3349 | ✅ |

All 7 required states present. **PASS**.

---

## Sync Result

**Command**: `powershell -File scripts\ptt-sync-and-verify.ps1`
**Output summary**:
```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  CopyEngine.cs
  Copied:   1  |  In-sync: 15  |  Excluded: 36

=== PTT VERIFY: MD5 check every synced file ===
  OK  AtrSizingEngine.cs       OK  CopyEngine.cs
  OK  TradeCopierAddOn.cs      OK  TradeCopierPanel.cs
  OK  TradeCopierWindow.cs     OK  Core\PttContracts.cs
  OK  Features\PttBreakEven.cs OK  Features\PttBreakEvenSwap.cs
  OK  Features\PttCancel.cs    OK  Features\PttCopier.cs
  OK  Features\PttFlatten.cs   OK  Features\PttFollowerStrategy.cs
  OK  Features\PttGlobalBreakEven.cs  OK  Features\PttGlobalQuickExit.cs
  OK  Features\PttQuickExit.cs OK  Features\PttTrim.cs

=== SYNC + VERIFY: PASS (16 files confirmed) ===
```
**Result**: 0 MISMATCH. **PASS**.

---

## JS Compliance Summary

| Rule | Scope | Status |
|------|-------|--------|
| JS-021: no `lock()` | `SnapshotBeTargets`, cap block | PASS — no new lock() |
| JS-001: no throw in hot path | Both methods | PASS — no throw |
| JS-002: no `return null` | `SnapshotBeTargets` null guard | PASS — returns empty list |
| JS-033: no `async void` | Both methods | PASS — synchronous only |
| ASCII-only | All new code | PASS — zero non-ASCII |
| No LINQ (NT8-006) | Cap block | PASS — while+RemoveAt only |
| CYC ≤ 8 | Both methods | PASS — both CYC=7 |

---

## Acceptance Criteria T1-T15

| Criterion | Status |
|-----------|--------|
| T1: `SnapshotBeTargets` method exists with correct signature | **PASS** — L3331-3371 |
| T2: null guard returns empty list, never null (JS-002) | **PASS** — L3336-3337 |
| T3: two-pass structure (nativeTargets + pttTargets) | **PASS** — L3334-3335, L3365-3368 |
| T4: stateOk includes all 7 states (regression guard DW-B79) | **PASS** — L3342-3349 |
| T5: `isNative` includes `[6] != '0'` guard | **PASS** — L3359 |
| T6: `isPtt` covers PTT-QX-T* and PTT-BE-Target-* | **PASS** — L3360-3364 |
| T7: `SnapshotBeTargets` CYC annotation = CYC=7 | **PASS** — L3326-3327 |
| T8: Step A inline foreach replaced by single call | **PASS** — old L3373-3422 removed |
| T9: Step A comment updated to DW-B107 extraction rationale | **PASS** — new comment block |
| T10: while cap present after `SnapshotBeTargets(...)`, before `PttBreakEvenSwap.Execute(...)` | **PASS** |
| T11: no LINQ at cap site (while+RemoveAt only) | **PASS** — SCAN-06 zero |
| T12: `MoveStopToBreakEven` CYC annotation updated CYC=8→CYC=7 | **PASS** — L3271-3273 |
| T13: no `lock()` in new code | **PASS** — SCAN-01 zero new |
| T14: no `return null` in new code | **PASS** — SCAN-03 zero new |
| T15: PttGlobalQuickExit.cs, PttQuickExit.cs, PttBreakEvenSwap.cs unchanged | **PASS** — sync OK, only CopyEngine.cs copied |

All 15 criteria: **PASS**.

---

## Commit Command

```powershell
git add src/PropTraderTools/CopyEngine.cs
git add docs/brain/B108/
git commit -m "feat(ptt): B108 DW-B107 SnapshotBeTargets extraction + cap-at-3"
```
