# PTT-REPAIRS-DW-E-04 Plan Review

**Status**: REVIEW_PASS  
**Phase**: 2 (Plan Review)  
**Epic**: PTT-REPAIRS-DW-E-04  
**Reviewer**: ptt-plan-reviewer  
**Review Date**: 2026-09-07  
**Review Cycle**: 1 of 2  
**Plan Author**: ptt-architect  
**Plan File**: `docs/brain/PTT-REPAIRS-DW-E-04/02-architecture-plan.md`

---

## Final Verdict: REVIEW_PASS

**Violations found**: 0 blocking  
**Warnings**: 0

All 11 checklist items PASS. Plan is approved for Phase 3 ticket generation.

---

## Per-Item Findings

---

### 1. LANE-SPLIT GATE

**Result**: PASS

- `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` label is present (§2). ✓
- Q1 = YES: both extractions are from the same single method body (`EvictDedup`, lines 5851-5901, confirmed in source). ✓
- STOP rule applied: Q2-Q4 not evaluated. Correctly documented. ✓
- Rationale is grounded: source confirms `EvictDedup` spans exactly 50 lines (5851-5901), single method, single file. ✓
- SINGLE-PIPELINE path is internally consistent. ✓

**Finding**: PASS

---

### 2. Spec Requirements (JS-013, JS-021, JS-001, JS-002, JS-042)

**Result**: PASS

| Rule | Plan Section | Verdict |
|------|-------------|---------|
| JS-013 CYC ≤ 8 | §3 + §5 CYC table | PASS — EvictDedup=7, EvictCancelledEntry=4, EvictFilledEntry=3 all stated explicitly |
| JS-021 no lock() | §3 + §5 method bodies | PASS — all ops are ConcurrentDictionary; no lock( in any method design |
| JS-001 no throw | §3 + §5 method bodies | PASS — all paths use early return or TryRemove; no throw or try/catch |
| JS-002 no null return | §3 + §5 signatures | PASS — all three methods are void; trivially satisfied |
| JS-042 ASCII-only | §3 + SCAN-02 in §9 | PASS — no Unicode in method bodies or comments; SCAN-02 command present |

**CYC arithmetic independently verified**:

`EvictDedup` residual (§5.1):
- Base(1) + `&&`x2 terminal guard(+2) + `if Cancelled`(+1) + `if TryRemove cancelledInstrKey`(+1) + `if Filled`(+1) + `if TryRemove filledInstrKey`(+1) = **CYC=7** ≤ 8 ✓

`EvictCancelledEntry` (§5.2):
- Base(1) + `if(TryGetValue && storedId == orderId)` if(+1) + `&&`(+1) + `if(pipeIdx > 0)`(+1) = **CYC=4** ≤ 8 ✓

`EvictFilledEntry` (§5.3):
- Base(1) + `if(TryGetValue && storedId == orderId)` if(+1) + `&&`(+1) = **CYC=3** ≤ 8 ✓

**Finding**: PASS

---

### 3. Design Correctness

**Result**: PASS

#### 3a. EvictDedup signature preservation

- §5.1 design: `internal void EvictDedup(string orderId, OrderState state)` — exact match to source line 5851. ✓
- §7 explicitly states: no parameter type changes, no return type changes, no accessibility changes. ✓

#### 3b. Call site at line 1541 confirmed unchanged

- Source line 1541 confirmed: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);`
- §7 quotes this exactly. Signature unchanged; call site requires no modification. ✓
- `EvictDedup_ForTest` wrapper confirmed at source line 4360: delegates unchanged to `EvictDedup(orderId, state)`. ✓

#### 3c. BUG-E fix preserved verbatim inside EvictCancelledEntry

Source lines 5878-5882 confirmed as:
```csharp
    // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
    // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
    var pipeIdx = cancelledInstrKey.IndexOf('|');
    if (pipeIdx > 0)
        _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```

- §6 quotes these lines verbatim with a 5-point preservation contract. ✓
- §5.2 design shows the exact same code inside `EvictCancelledEntry`. ✓
- Character literal `'|'`, guard `pipeIdx > 0`, and exact `TryRemove(Substring(0, pipeIdx), out _)` call all match. ✓

#### 3d. Extraction boundaries correct — no logic dropped, no logic duplicated

- `_dedupCache.TryRemove(orderId, out _)` stays in `EvictDedup` before dispatch (§5.1 line 148; source line 5860). ✓
- `_entryDispatchedOrders.TryRemove(orderId, out _)` stays in `EvictDedup` Cancelled branch (§5.1 line 152; source line 5866). ✓
- Terminal-state guard remains in `EvictDedup`. ✓
- `EvictCancelledEntry` receives only the inner body after `TryRemove(orderId, out cancelledInstrKey)` succeeds, parametrised as `(orderId, cancelledInstrKey)`. ✓
- `EvictFilledEntry` receives only the inner `TryGetValue`/`TryRemove` body, parametrised as `(orderId, filledInstrKey)`. ✓
- No logic duplication; no logic omitted. ✓

#### 3e. `_dedupCache.TryRemove` placement

- In §5.1 it appears before the `Cancelled`/`Filled` dispatch — matches source line 5860. ✓

#### 3f. InternalsVisibleTo already in place — no new assembly attribute needed

- Source comment at line 4340: `// InternalsVisibleTo("PropTraderTools.Tests") granted at L46.` ✓
- §8 confirms: "All helper methods required already exist and are internal/InternalsVisibleTo." ✓

**Finding**: PASS

---

### 4. Test Coverage

**Result**: PASS

- §8 decision: **INCLUDE in this pipeline ticket** — explicitly stated. ✓
- Test name: `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` matches checklist. ✓

**Helper methods verified against source**:

| Helper | Plan citation | Source line | Confirmed |
|--------|--------------|-------------|-----------|
| `EvictDedup_ForTest` | §8 | 4360 | ✓ |
| `SetLeaderDirection_ForTest` | §8 | 4333 | ✓ |
| `IsLiveEntryBlocked_ForTest` | §8 | 4348 | ✓ |
| `HasLeaderDirection` (no `_ForTest` suffix) | §8 | 4330 | ✓ |

- §8 correctly flags the naming distinction: `HasLeaderDirection` (not `HasLeaderDirection_ForTest`) to prevent engineer error. ✓
- Test logic is sound: `"MGC DEC26|Buy"` stripped at `|` yields `"MGC DEC26"` — consistent with BUG-E fix. ✓
- Baseline 19 passed / 449 failed / 31 skipped stated in §8 and §9 SCAN-05. ✓
- Post-epic baseline: ≥20 passed after new test. ✓

**Finding**: PASS

---

### 5. 7-Scan Checklist

**Result**: PASS

| Scan | Command present | Expected result stated |
|------|----------------|----------------------|
| SCAN-01 lock() | `grep -n "lock(" ... \| grep -E "Evict..."` | 0 matches |
| SCAN-02 Non-ASCII | `grep -Pn "[^\x00-\x7F]" ... \| grep -E "Evict..."` | 0 matches |
| SCAN-03 Build errors | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 Error(s) |
| SCAN-04 Build warnings | (from SCAN-03 output) | 0 new warnings |
| SCAN-05 Test counts | `dotnet test` | passed >= 19; new test PASS |
| SCAN-06 deploy-sync | `powershell -File .\deploy-sync.ps1` | SYNC COMPLETE |
| SCAN-07 Hard-link | `fsutil hardlink list src\PropTraderTools\CopyEngine.cs` | hardlink count = 1 |

All 7 scans listed with commands and expected results. ✓

**Finding**: PASS

---

### 6. Section Completeness (§1–§11)

**Result**: PASS

| Section | Required | Present |
|---------|----------|---------|
| §1 Epic summary | ✓ | ✓ |
| §2 LANE-SPLIT GATE RESULT | ✓ | ✓ |
| §3 Spec requirements mapped | ✓ | ✓ |
| §4 Current EvictDedup analysis | ✓ | ✓ |
| §5 Extracted method designs | ✓ | ✓ |
| §6 BUG-E preservation plan (verbatim lines) | ✓ | ✓ |
| §7 Call-site preservation | ✓ | ✓ |
| §8 New test decision | ✓ | ✓ |
| §9 7-scan checklist | ✓ | ✓ |
| §10 Ticket count | ✓ | ✓ |
| §11 Risks | ✓ | ✓ |

**Finding**: PASS

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|-------------|
| EvictDedup CYC ≤ 8 (JS-013) | YES | §3, §5.1, §5.4 |
| EvictCancelledEntry CYC ≤ 8 (JS-013) | YES | §3, §5.2, §5.4 |
| EvictFilledEntry CYC ≤ 8 (JS-013) | YES | §3, §5.3, §5.4 |
| No lock() in any method (JS-021) | YES | §3, §5 method bodies |
| No throw in any method (JS-001) | YES | §3, §5 method bodies |
| No null return in any method (JS-002) | YES | §3 (void methods) |
| ASCII-only string literals (JS-042) | YES | §3, §9 SCAN-02 |
| BUG-E fix preserved verbatim | YES | §6 |
| EvictDedup signature unchanged | YES | §5.1, §7 |
| Call site at line 1541 unchanged | YES | §7 |
| InternalsVisibleTo no new attribute needed | YES | §8 |
| New test EvictDedup_CancelledEntry_ClearsLastLeaderDirection | YES | §8 |
| All 7 scans with commands | YES | §9 |
| Single ticket (SINGLE-PIPELINE) | YES | §2, §10 |

All 14 spec requirements fully addressed. No gaps.

---

## Violations

None.

---

## Summary

| Check | Result |
|-------|--------|
| LANE-SPLIT GATE label present | PASS |
| LANE-SPLIT GATE reasoning consistent | PASS |
| JS-013 CYC targets stated explicitly | PASS |
| JS-021 no lock() | PASS |
| JS-001 no throw | PASS |
| JS-002 no null return | PASS |
| JS-042 ASCII-only | PASS |
| EvictDedup signature preserved | PASS |
| Call site line 1541 unchanged | PASS |
| BUG-E verbatim lines quoted | PASS |
| Extraction boundaries correct | PASS |
| _dedupCache.TryRemove placement correct | PASS |
| InternalsVisibleTo already in place | PASS |
| Test included in T1 | PASS |
| Test helpers all verified in source | PASS |
| Baseline passed >= 19 stated | PASS |
| All 7 scans with commands | PASS |
| §1-§11 all present | PASS |

**Verdict: REVIEW_PASS. Plan approved for Phase 3 ticket generation.**
