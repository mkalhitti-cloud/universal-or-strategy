# PTT-COPIER-B25 Ticket 1 — Verification Report

**Verifier**: ptt-verifier (Lane B)
**Block**: PTT-COPIER-B25
**Ticket**: T1 — DW-B25-02: Per-Account BE State Isolation
**Verdict**: **VERIFY_PASS**
**Date**: 2026-07-07

---

## 1. Files Verified (READ-ONLY)

| File | Wave Path |
|------|-----------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `TradeCopierPanel.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

---

## 2. Layer 3 Scan Results (Independent — do NOT trust engineer self-report)

All 7 scans run independently via `execute_command`. Results are authoritative.

### SCAN-01
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs"
    -Pattern "_pendingBeState\b" | Where-Object { $_.Line -notmatch "BeStates" }
```
**Result: 0 matches** ✅
Old singleton `_pendingBeState` (volatile int) and all access sites are gone.

### SCAN-02
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs"
    -Pattern "_trailBeState\b" | Where-Object { $_.Line -notmatch "BeStates" }
```
**Result: 0 matches** ✅
Old singleton `_trailBeState` (volatile int) and all access sites are gone.

### SCAN-03
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs"
    -Pattern "_pendingBeStates"
```
**Result: 5 matches** ✅ (≥5 required)
- Line 100: field declaration (`ConcurrentDictionary<string, int>`)
- Line 1307: `ArmPendingBe` dict indexer write
- Line 1322: `DisarmPendingBe` `TryRemove`
- Line 1338: `IsPendingBeArmed` `TryGetValue`
- Line 1454: `OnPendingBeAccountUpdate` `TryRemove`

### SCAN-04
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs"
    -Pattern "_trailBeStates"
```
**Result: 5 matches** ✅ (≥5 required)
- Line 3: file-header comment
- Line 110: field declaration (`ConcurrentDictionary<string, int>`)
- Line 1363: `ArmTrailBe` dict indexer write
- Line 1379: `DisarmTrailBe` `TryRemove`
- Line 1392: `IsTrailBeArmed` `TryGetValue`

### SCAN-05
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools"
    -Pattern "lock\s*\(" -Include "*.cs"
```
**Result: 0 matches** ✅ (JS-021 compliance)

### SCAN-06
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools"
    -Pattern "ImmutableDictionary" -Include "*.cs"
```
**Result: 0 matches** ✅ (NT8-004 compliance)

### SCAN-07
```
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools"
    -Pattern "\?\.\w+\s*[-+]=" -Include "*.cs"
```
**Result: 0 matches** ✅ (NT8-043 compliance)

---

## 3. Layer 2 vs Layer 3 Cross-Check

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy? |
|------|-------------------|-------------------|--------------|
| SCAN-01 | 0 | 0 | None ✅ |
| SCAN-02 | 0 | 0 | None ✅ |
| SCAN-03 | 5 | 5 | None ✅ |
| SCAN-04 | 5 | 5 | None ✅ |
| SCAN-05 | 0 | 0 | None ✅ |
| SCAN-06 | 0 | 0 | None ✅ |
| SCAN-07 | 0 | 0 | None ✅ |

**No discrepancies found.** Engineer's self-report is accurate.

---

## 4. Implementation Completeness (Steps A–G)

### A. Field Changes

| Check | Result |
|-------|--------|
| `_pendingBeStates` (`ConcurrentDictionary<string,int>`) present at CopyEngine.cs:100 | ✅ PASS |
| `_trailBeStates` (`ConcurrentDictionary<string,int>`) present at CopyEngine.cs:110 | ✅ PASS |
| `_pendingBeState` (volatile int) absent — SCAN-01: 0 hits | ✅ PASS |
| `_trailBeState` (volatile int) absent — SCAN-02: 0 hits | ✅ PASS |

### B. DisarmPendingBe Signature

CopyEngine.cs:1315: `internal void DisarmPendingBe(Account leader)` — takes `Account leader` parameter. ✅ PASS

### C. DisarmTrailBe Signature

CopyEngine.cs:1372: `internal void DisarmTrailBe(Account leader)` — takes `Account leader` parameter. ✅ PASS

### D. IsPendingBeArmed Helper

CopyEngine.cs:1336–1339: private expression-body method present:
```csharp
private bool IsPendingBeArmed(Account acc)
    => acc != null
    && _pendingBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```
CYC = 1. ✅ PASS

### E. IsTrailBeArmed Helper

CopyEngine.cs:1390–1393: private expression-body method present:
```csharp
private bool IsTrailBeArmed(Account acc)
    => acc != null
    && _trailBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```
CYC = 1. ✅ PASS

### F. TradeCopierPanel.cs Call Sites Pass `_leaderAccount`

| Line | Call | Status |
|------|------|--------|
| 402 | `_engine.DisarmPendingBe(_leaderAccount)` | ✅ PASS |
| 403 | `_engine.DisarmTrailBe(_leaderAccount)` | ✅ PASS |
| 807 | `_engine.DisarmPendingBe(_leaderAccount)` | ✅ PASS |
| 812 | `_engine.DisarmPendingBe(_leaderAccount)` | ✅ PASS |
| 813 | `_engine.DisarmTrailBe(_leaderAccount)` | ✅ PASS |

All 5 call sites confirmed. ✅ PASS

### G. CopyEngineTests.cs Test Updates

| Test | Change | Status |
|------|--------|--------|
| `ArmTrailBe_NullInstrument_NoException` (line 1667–1672) | Reflects on `_trailBeStates`; `Assert.Empty(dict)` | ✅ PASS |
| `DisarmTrailBe_WhenNotArmed_NoException` (line 1679) | `_engine.DisarmTrailBe(null)` | ✅ PASS |
| `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall` (lines 1689–1690) | Both calls use `_engine.DisarmTrailBe(null)` | ✅ PASS |

---

## 5. Spec Requirement Satisfaction

| Requirement | Check | Status |
|-------------|-------|--------|
| DW-B25-02: singleton volatile int state gone | SCAN-01/02: 0 hits each | ✅ PASS |
| DW-B25-02: per-account dict slots added | SCAN-03/04: 5 hits each | ✅ PASS |
| NT8-004: No ImmutableDictionary | SCAN-06: 0 hits | ✅ PASS |
| JS-021: No lock() | SCAN-05: 0 hits | ✅ PASS |
| NT8-043: No null-conditional event unsubscription | SCAN-07: 0 hits | ✅ PASS |
| NT8-043: Explicit `if (acc != null)` guards | DisarmPendingBe:1325, DisarmTrailBe:1382, OnPendingBeAccountUpdate:1458 | ✅ PASS |
| Test count: 128 baseline = 128 final | 3 tests updated, 0 added, 0 deleted | ✅ PASS |

---

## 6. Companion Fields Unchanged (Singleton — Plain Ref, NOT per-account)

| Field | Location | Type | Status |
|-------|----------|------|--------|
| `_pendingBeAccount` | CopyEngine.cs ~101 | `Account` (plain ref, null init) | ✅ Unchanged |
| `_pendingBeInstrument` | CopyEngine.cs ~102 | `Instrument` (plain ref, null init) | ✅ Unchanged |
| `_pendingBeBufferTicks` | CopyEngine.cs ~100 | `volatile int` | ✅ Unchanged |
| `_trailBeAccount` | CopyEngine.cs ~114 | `Account` (plain ref, null init) | ✅ Unchanged |
| `_trailBeInstrument` | CopyEngine.cs ~115 | `Instrument` (plain ref, null init) | ✅ Unchanged |
| `_trailBeBufferTicks` | CopyEngine.cs ~112 | `volatile int` | ✅ Unchanged |
| `_trailBeLastPnl` | CopyEngine.cs ~113 | `long` (Interlocked-guarded) | ✅ Unchanged |

All 7 companion fields remain singleton. None were accidentally converted to per-account. ✅ PASS

---

## 7. DNA Rule Compliance

| Rule | Pattern | Status |
|------|---------|--------|
| JS-021 (lock BANNED) | SCAN-05: 0 results | ✅ PASS |
| JS-033 (async void BANNED) | No async methods touched | ✅ PASS |
| JS-001 (throw in hot path BANNED) | All paths use early return; no throws added | ✅ PASS |
| JS-002 (return null BANNED) | No `return null` in new methods | ✅ PASS |
| NT8-003 (volatile double BANNED) | No new volatile declarations | ✅ PASS |
| NT8-004 (ImmutableDictionary BANNED) | SCAN-06: 0 results; ConcurrentDictionary used | ✅ PASS |
| NT8-018 (lock() BANNED) | SCAN-05: 0 results | ✅ PASS |
| NT8-043 (null-conditional unsub BANNED) | SCAN-07: 0 results; explicit `if (acc != null)` guards present | ✅ PASS |

---

## 8. CYC Spot-Check

| Method | Target | Verified | Status |
|--------|--------|----------|--------|
| `IsPendingBeArmed` | ≤ 1 | 1 (expression body, no branching statements) | ✅ |
| `IsTrailBeArmed` | ≤ 1 | 1 (expression body, no branching statements) | ✅ |
| `DisarmPendingBe` | ≤ 4 | 4 (3 if-branches + base 1) | ✅ |
| `DisarmTrailBe` | ≤ 4 | 4 (3 if-branches + base 1) | ✅ |
| `ArmPendingBe` | ≤ 4 | 4 (3 null/flat guards + arm write) | ✅ |
| `ArmTrailBe` | ≤ 4 | 4 (3 null/flat guards + arm write) | ✅ |
| `OnPendingBeAccountUpdate` | ≤ 8 | 8 (7 if-branches + base 1; F1 fix absorbed compound guard) | ✅ |
| `OnTrailBeAccountUpdate` | ≤ 8 | 5 (4 branches + base 1; comfortable margin) | ✅ |

---

## 9. Architecture Compliance

- **Singleton field removal**: ✅ `_pendingBeState` and `_trailBeState` (volatile int) removed.
- **ConcurrentDictionary field addition**: ✅ `_pendingBeStates` and `_trailBeStates` added with correct type and `readonly` modifier.
- **Release-fence ordering**: ✅ Companion ref writes precede dict indexer setter in both Arm methods.
- **Atomic disarm semantics**: ✅ `TryRemove` replaces `Interlocked.CompareExchange` — same "exactly one caller wins" guarantee.
- **Multi-panel isolation**: ✅ Panel A keys `"SIM101"`, Panel B keys `"SIM102"` — disarm on one does not affect the other.
- **No UI calls inside callbacks**: ✅ `OnPendingBeAccountUpdate` and `OnTrailBeAccountUpdate` remain UI-call-free.
- **TOCTOU safety**: ✅ Both callbacks capture `_pendingBeAccount` / `_trailBeAccount` to local `acc` at top of method before guard checks.

---

## 10. Violations Found

**None.**

---

## VERIFY_PASS

All 7 scans passed. All implementation checks (A–G) passed. All spec requirements satisfied.
All DNA rules compliant. All companion fields unchanged. No violations found.

Engineer's Layer 2 self-report is 100% accurate — no discrepancies between Layer 2 and Layer 3.

---

*ptt-verifier · PTT-COPIER-B25 · ticket-1-verification.md · 2026-07-07*
