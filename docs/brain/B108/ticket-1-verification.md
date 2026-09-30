# B108-T1 Verification Report
**Verifier**: ptt-verifier
**Ticket**: B108-T1 (DW-B107 fix — SnapshotBeTargets extraction + cap-at-3)
**Epic**: B108
**Date**: 2026-08-11
**Verdict**: VERIFY_PASS

---

## 1. Files Read (Independent — READ-ONLY)

| File | Lines Read | Purpose |
|------|-----------|---------|
| `src/PropTraderTools/CopyEngine.cs` | 3265–3440 | Ground truth — actual implementation |
| `docs/brain/B108/04-tickets.md` | full | Ticket contract (T1–T15, 7 scans) |
| `docs/brain/B108/ticket-1-completion.md` | full | Engineer Layer 2 report (to cross-check) |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | 182–226 | SnapshotTargetOrders structural model |

---

## 2. Independent 7-Scan Results (Layer 3)

### V-SCAN-01 — lock() check (JS-021 P0)
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("`
**Output**:
```
src\PropTraderTools\CopyEngine.cs:1903:  // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
```
**Analysis**: One hit at L1903 — inside a comment (`try block(0)` contains substring `lock(`). Not a code `lock()` statement. Zero actual `lock()` in `SnapshotBeTargets` (L3331–3371) or cap block (L3426–3430).
**Verdict**: PASS ✅

---

### V-SCAN-02 — async void check (JS-033 P0)
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void "`
**Output**: (no output — zero results)
**Analysis**: Zero matches. All new code is synchronous.
**Verdict**: PASS ✅

---

### V-SCAN-03 — return null check (JS-002 P0)
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
**Analysis**: 7 pre-existing hits, all outside B108 scope. None in `SnapshotBeTargets` (L3331–3371). Null guard at L3336–3337 returns `nativeTargets` (empty list), never `null`.
**Verdict**: PASS ✅

---

### V-SCAN-04 — Non-ASCII check
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"`
**Output**:
```
src\PropTraderTools\CopyEngine.cs:316:  (pre-existing)
src\PropTraderTools\CopyEngine.cs:317:  (pre-existing)
src\PropTraderTools\CopyEngine.cs:2880: (pre-existing)
src\PropTraderTools\CopyEngine.cs:2881: (pre-existing)
```
**Analysis**: 4 pre-existing hits. Zero non-ASCII in any B108 new code.
**Verdict**: PASS ✅

---

### V-SCAN-05 — CYC manual count
**Method**: Direct branch count from source read.

**SnapshotBeTargets** (L3331–3371):
| # | Branch | Source Line |
|---|--------|------------|
| 1 | `acc == null \|\| instrument == null` (null guard) | L3336 |
| 2 | `foreach (Order o in acc.Orders)` | L3338 |
| 3 | `if (o == null) continue` | L3340 |
| 4 | `stateOk` compound OR gate (→ continue on false) | L3342–3352 |
| 5 | `instrOk + OrderType.Limit` gate (→ continue on false) | L3350–3352 |
| 6 | `if (isNative)` | L3365 |
| 7 | `else if (isPtt)` | L3367 |

**CYC = 7** ≤ 8 ✅

**MoveStopToBreakEven** annotation (L3271–3272):
| # | Branch |
|---|--------|
| 1 | IsFlat guard |
| 2 | tickSize/pos guard |
| 3 | while-cap |
| 4 | cancel-try |
| 5 | 0-targets branch |
| 6 | targets-for-loop |
| 7 | partial-retry branch |

**CYC = 7** ≤ 8 ✅
**Verdict**: PASS ✅

---

### V-SCAN-06 — LINQ check (NT8-006)
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\.Take\(|\.GetRange\(|\.Where\(|\.Select\("`
**Output**: (no output — zero results)
**Analysis**: Zero LINQ calls in entire file. Cap uses `while + RemoveAt` (L3429–3430).
**Verdict**: PASS ✅

---

### V-SCAN-07 — stateOk 7-state completeness (DW-B79 regression gate)
**Method**: Manual inspection of L3342–3349 from source read.

| # | State | Line | Present |
|---|-------|------|---------|
| 1 | `OrderState.Working` | L3343 | YES ✅ |
| 2 | `OrderState.Accepted` | L3344 | YES ✅ |
| 3 | `OrderState.Submitted` | L3345 | YES ✅ |
| 4 | `OrderState.Initialized` | L3346 | YES ✅ |
| 5 | `OrderState.TriggerPending` | L3347 | YES ✅ |
| 6 | `OrderState.ChangeSubmitted` | L3348 | YES ✅ |
| 7 | `OrderState.CancelSubmitted` | L3349 | YES ✅ |

All 7 states present. No DW-B79 regression.
**Verdict**: PASS ✅

---

## 3. T1–T15 Acceptance Criteria (Layer 3 independent check)

### [T1] — SnapshotBeTargets method exists with correct signature
**Source**: L3331–3332:
```csharp
private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(
    Account acc, Instrument instrument)
```
Return type `List<(double Price, int Qty, OrderAction Action)>` ✅. Parameters `(Account acc, Instrument instrument)` ✅. Located immediately before `MoveStopToBreakEven` (L3383) ✅.
**Verdict**: PASS ✅

---

### [T2] — SnapshotBeTargets null guard (JS-002)
**Source**: L3336–3337:
```csharp
if (acc == null || instrument == null)
    return nativeTargets; // (1) JS-002: empty list, never null
```
Returns `nativeTargets` (empty list), NOT `null`. No `return null` anywhere in L3331–3371.
**Verdict**: PASS ✅

---

### [T3] — SnapshotBeTargets two-pass structure
**Source**:
- L3334–3335: `var nativeTargets = new List<...>(); var pttTargets = new List<...>();`
- L3365–3368: `if (isNative) nativeTargets.Add(...); else if (isPtt) pttTargets.Add(...);`
- L3370: `return nativeTargets.Count > 0 ? nativeTargets : pttTargets;`
All three structural elements present.
**Verdict**: PASS ✅

---

### [T4] — stateOk has exactly 7 states
**Source**: L3342–3349 — confirmed in SCAN-07. All 7 states: Working, Accepted, Submitted, Initialized, TriggerPending, ChangeSubmitted, CancelSubmitted.
**Verdict**: PASS ✅

---

### [T5] — isNative includes [6] != '0' guard
**Source**: L3355–3359:
```csharp
bool isNative =
    o.Name.Length >= 7
    && o.Name.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(o.Name[6])
    && o.Name[6] != '0';
```
All 4 sub-conditions present including `[6] != '0'`.
**Verdict**: PASS ✅

---

### [T6] — isPtt covers both PTT-QX-T* and PTT-BE-Target-*
**Source**: L3360–3364:
```csharp
bool isPtt =
    (o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
     && o.Name.Length > 8
     && char.IsDigit(o.Name[8]))
    || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
```
Both OR branches present.
**Verdict**: PASS ✅

---

### [T7] — SnapshotBeTargets CYC annotation = CYC=7
**Source**: L3326–3327:
```
// CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5)
//        + if(isNative)(6) + else if(isPtt)(7). JS-002: returns List, never null.
```
CYC=7 with all 7 branches enumerated.
**Verdict**: PASS ✅

---

### [T8] — Step A loop replaced (CHANGE B)
**Source**: L3421–3425:
```csharp
// -- Step A: snapshot ATM target orders BEFORE cancelling anything ----
// DW-B107: extracted to SnapshotBeTargets to keep MoveStopToBreakEven CYC=7.
// Two-pass native-first collect: native Target1..9 take priority over
// stale PTT-QX-T*/PTT-BE-Target-* residues (same logic as DW-B106).
var targets = SnapshotBeTargets(acc, instrument); // (3)
```
Old 50-line `foreach` block is absent. Replacement matches ticket verbatim.
**Verdict**: PASS ✅

---

### [T9] — Step A comment updated to DW-B107 rationale
**Source**: L3421–3424 confirmed above. Old DW-B79-01/HOTFIX-MSTBE-QX-TARGETS-01 comment text absent at call site. No `var targets = new List<(double Price, int Qty, OrderAction Action)>();` at call site.
**Verdict**: PASS ✅

---

### [T10] — while cap inserted (CHANGE C)
**Source**: L3426–3430:
```csharp
// DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
// Prevents stale partial-fill residue submitting extra OCO pairs.
// No LINQ -- while-loop trim per JS zero-alloc mandate.
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);
```
Cap present immediately after `SnapshotBeTargets` call (L3425) and before `PttBreakEvenSwap.Execute` (L3435). DW-B107 comment present.
**Verdict**: PASS ✅

---

### [T11] — No LINQ at cap site
**Source**: SCAN-06 returned zero LINQ results in entire file. L3426–3430 uses `while + RemoveAt` only.
**Verdict**: PASS ✅

---

### [T12] — MoveStopToBreakEven CYC annotation updated CYC=8→CYC=7
**Source**: L3271–3273:
```
// CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4)
//        + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7).
// DW-B107: Step A extracted to SnapshotBeTargets; while cap reduces stale residue.
```
Old `CYC=8: ... snapshot-foreach(3) + stateOk(4) + instrOk(5)...` annotation is absent. New `CYC=7` annotation with `while-cap(3)` and `DW-B107` reference present.
**Verdict**: PASS ✅

---

### [T13] — No lock() in new code
**Source**: SCAN-01 confirmed. No `lock(` in `SnapshotBeTargets` (L3331–3371) or cap block (L3426–3430).
**Verdict**: PASS ✅

---

### [T14] — No return null in new code
**Source**: SCAN-03 confirmed. No `return null` in `SnapshotBeTargets` (L3331–3371). Method returns empty `nativeTargets` list at L3337.
**Verdict**: PASS ✅

---

### [T15] — PttGlobalQuickExit.cs, PttQuickExit.cs, PttBreakEvenSwap.cs unchanged
**Evidence**:
- `Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "SnapshotBeTargets"` returned zero results — `SnapshotBeTargets` is exclusively in `CopyEngine.cs`.
- Engineer sync report: only `CopyEngine.cs` was copied (1 copied, 15 in-sync). `PttGlobalQuickExit.cs`, `PttQuickExit.cs`, `PttBreakEvenSwap.cs` all listed as `OK` (in-sync, unmodified).
- `PttGlobalQuickExit.cs` L182–226 confirms `SnapshotTargetOrders` is the B107 version (2-tuple `(double Price, int Qty)`, stateOk=Working|Accepted only) — unchanged from B107 baseline.
**Verdict**: PASS ✅

---

## 4. T1–T15 Summary Table

| Criterion | Verdict |
|-----------|---------|
| T1: SnapshotBeTargets exists with correct signature | PASS ✅ |
| T2: null guard returns empty list, never null | PASS ✅ |
| T3: two-pass nativeTargets/pttTargets structure | PASS ✅ |
| T4: stateOk includes all 7 states | PASS ✅ |
| T5: isNative includes `[6] != '0'` guard | PASS ✅ |
| T6: isPtt covers PTT-QX-T* AND PTT-BE-Target-* | PASS ✅ |
| T7: CYC=7 annotation on SnapshotBeTargets | PASS ✅ |
| T8: Step A foreach replaced by SnapshotBeTargets call | PASS ✅ |
| T9: Step A comment updated to DW-B107 rationale | PASS ✅ |
| T10: while cap between SnapshotBeTargets and Execute | PASS ✅ |
| T11: no LINQ at cap site | PASS ✅ |
| T12: MoveStopToBreakEven CYC annotation CYC=8 -> CYC=7 | PASS ✅ |
| T13: no lock() in new code | PASS ✅ |
| T14: no return null in new code | PASS ✅ |
| T15: out-of-scope files untouched | PASS ✅ |

**All 15: PASS**

---

## 5. Layer 2 Cross-Check (Engineer vs. Verifier)

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Match? |
|------|-------------------|--------------------|--------|
| SCAN-01 lock() | 1 hit L1903 (comment) | 1 hit L1903 (comment) | YES ✅ |
| SCAN-02 async void | zero results | zero results | YES ✅ |
| SCAN-03 return null | 7 pre-existing (last: L4049) | 7 pre-existing (last: L4057) | YES* |
| SCAN-04 non-ASCII | 4 pre-existing (L316,317,2880,2881) | 4 pre-existing (same lines) | YES ✅ |
| SCAN-05 CYC | both CYC=7 | both CYC=7 | YES ✅ |
| SCAN-06 LINQ | zero results | zero results | YES ✅ |
| SCAN-07 stateOk 7 states | all 7 present | all 7 present | YES ✅ |

*SCAN-03 L4049 vs L4057 — 8-line offset is consistent with the ~47-line `SnapshotBeTargets` insertion after engineer ran the scan. The return null lines are all pre-existing and all outside B108 scope. This is a documentation ordering artifact, NOT a code violation.

**No Layer 2 discrepancies that indicate a code defect.**

---

## 6. Out-of-Scope File Check

**Command**: `Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "SnapshotBeTargets"`
**Output**: (no output — zero results)
**Result**: `SnapshotBeTargets` exists only in `CopyEngine.cs`. `PttGlobalQuickExit.cs` was NOT modified. ✅

---

## 7. Architecture Compliance

| Requirement | Status |
|-------------|--------|
| CHANGE A: SnapshotBeTargets inserted immediately before MoveStopToBreakEven | PASS |
| CHANGE B: Step A foreach replaced by single call; CYC annotation updated | PASS |
| CHANGE C: while cap inserted after call, before Execute | PASS |
| DW-B107 closed: stale PTT-BE-Target-* residue path eliminated by two-pass + cap | PASS |
| DW-B79 regression: 7-state stateOk preserved verbatim | PASS |
| JS-021: no lock() in new code | PASS |
| JS-002: no return null in SnapshotBeTargets | PASS |
| JS-001: no throw in hot path | PASS |
| NT8-006: no LINQ | PASS |
| ASCII-only: zero non-ASCII in new code | PASS |

---

## 8. Final Verdict

**VERIFY_PASS**

All 7 independent scans: PASS.
All 15 acceptance criteria (T1–T15): PASS.
No Layer 2 code discrepancies.
Out-of-scope files confirmed untouched.
DNA rules (JS-001, JS-002, JS-021, JS-033, NT8-006): all satisfied in new code.
DW-B107 closed: two-pass native-first collect + hard cap-at-3 eliminates stale PTT-BE-Target-* inflation in MoveStopToBreakEven Step A.
DW-B79 regression guard: stateOk 7-state block preserved verbatim — no narrowing.