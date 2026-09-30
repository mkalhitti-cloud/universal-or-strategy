# PTT-COPIER-B14 Plan Review
# Block: PTT-COPIER-B14
# Date: 2026-07-14
# Reviewer: ptt-plan-reviewer (Phase 2)
# Input: docs/brain/PTT-COPIER-B14/02-architecture-plan.md
# Reference: docs/brain/PTT-COPIER-B13/06-deferred-backlog.md
#            docs/brain/PTT-COPIER-B12/04-tickets.md §1.10
#            docs/standards/jane-street/RULES_CATALOG.md
#            docs/standards/NT8_COMPILER_RULES.md
# Status: REVIEW_PASS

---

## 1. Scope Gate

| Check | Expected | Actual | Verdict |
|-------|----------|--------|---------|
| Exactly 2 in-scope items | DW-B12-DEFER-02 (original) + DW-B12-DEFER-04 | §1 scope table: exactly those 2 items | PASS |
| DW-B9-01 shelved (not planned) | Absent from in-scope | §1 "Shelved" row present | PASS |
| DW-B9-03 shelved (not planned) | Absent from in-scope | §1 "Shelved" row present | PASS |
| DW-B12-DEFER-01 (original) shelved | Absent from in-scope | §1 "Shelved" row present | PASS |
| No scope creep beyond B13 open items | Only DW-B12-DEFER-02 + DW-B12-DEFER-04 | Confirmed — no additional items | PASS |
| Scope consistent with B13 backlog | B13 §Open Items for B14 lists exactly these | B14 plan §1 matches exactly | PASS |

---

## 2. NT8 Constraint Review

| Rule | Check | Plan Location | Verdict |
|------|-------|---------------|---------|
| NT8-003 | `volatile double` absent; `_trailBeLastPnl` is `volatile long` with BitConverter | §2.2 new fields + §6 NT8 table | PASS |
| NT8-018 / JS-021 | No `lock()` — Interlocked only in all new methods | §2.3 methods + §8 SCAN-01 | PASS |
| NT8-019 / JS-033 | `OnTrailBeAccountUpdate` is plain `void`; `OnBeConnected` is `async void` via Dispatcher (PERMITTED) | §2.3 OnTrailBeAccountUpdate header; §2.4 OnBeConnected; §6 NT8 table | PASS |
| NT8-026 | No `order.TrailPrice` — uses `OrderType.StopMarket` via existing `IsTrailingStop()` | §6 NT8 table (NT8-026 row) | PASS |
| NT8-031 | `using System.Threading` already present in CopyEngine.cs (confirmed B10 T2) | §6 NT8 table (NT8-031 row) | PASS |
| NT8-007 | No `CreateOrder` in trail methods | §6 NT8 table (NT8-007 row) | PASS |
| NT8-013 | No `DateTime.Now` — no new CreateOrder calls | §6 NT8 table (NT8-013 row) | PASS |
| NT8-014 | No new CreateOrder signal names | §6 NT8 table (NT8-014 row) | PASS |
| NT8-020 | No new brushes in B14; existing `BrushConnected` already frozen via `MakeBrush()` | §6 NT8 table (NT8-020 row) | PASS |
| NT8-034 | `Math.Clamp` not used anywhere in new methods | §6 NT8 table (NT8-034 row) | PASS |

---

## 3. Jane Street Rules Review

| Rule | Severity | Check | Plan Location | Verdict |
|------|----------|-------|---------------|---------|
| JS-021 (no lock()) | P0 | All new methods use Interlocked.CompareExchange / Interlocked.Read / Interlocked.Increment — no lock() | §2.3 all three methods; §9 rule table | PASS |
| JS-001 (no throw in hot path) | P0 | OnTrailBeAccountUpdate has no try/catch and no throw; BreakEven wraps acc.Change() internally | §2.3 OnTrailBeAccountUpdate comment; §9 rule table JS-001 row | PASS |
| JS-002 (no return null) | P0 | All guard-path exits use bare `return;` — no `return null` in any new method | §2.3 ArmTrailBe code (lines `return;`), DisarmTrailBe code, OnTrailBeAccountUpdate code | PASS |
| JS-033 (no async void except event handlers) | P0 | OnTrailBeAccountUpdate is plain `void`; no new async void engine methods | §2.3 OnTrailBeAccountUpdate signature; §9 rule table JS-033 row | PASS |
| JS-023 (cross-thread volatile) | P1 | `_trailBeState` volatile int, `_trailBeBufferTicks` volatile int, `_trailBeLastPnl` volatile long; plain refs are single-writer UI thread protected by release fence | §2.2 new fields block; §9 rule table JS-023 row | PASS |
| JS-008 (SolidColorBrush frozen) | P1 | No new brushes in B14 | §6 NT8 table (NT8-020 row) | PASS |

---

## 4. Design Review — T1 (DW-B12-DEFER-02)

### 4.1 CYC Estimates

| Method | File | Claimed CYC | Reviewer Count | Limit | Verdict |
|--------|------|-------------|----------------|-------|---------|
| ArmTrailBe | CopyEngine.cs | 4 | 4 (instr null, acc null, IsFlat, arm write) | 8 | PASS |
| DisarmTrailBe | CopyEngine.cs | 2 | 2 (CAS active check, acc null) | 8 | PASS |
| OnTrailBeAccountUpdate | CopyEngine.cs | 5 | 5 decision branches (+1 baseline = CYC 6 by strict McCabe) | 8 | PASS (6 ≤ 8) |
| OnBeConnected | TradeCopierPanel.cs | 3 | 3 (+1 new null guard for _leaderAccount) | 8 | PASS |
| OnBeClick Connected case | TradeCopierPanel.cs | 5 | 5 (unchanged) | 8 | PASS |
| 6 new xUnit tests (T1) | CopyEngineTests.cs | 1 each | 1 (no branch logic in tests) | 8 | PASS |

**CYC Note — OnTrailBeAccountUpdate**: The plan claims CYC=5 by counting 5 decision points. Strict McCabe adds 1 baseline, yielding CYC=6. Either formulation passes the ≤8 limit; the discrepancy is in counting convention only, not a violation.

### 4.2 Release-Fence Ordering

| Check | Expected | Plan Location | Verdict |
|-------|----------|---------------|---------|
| `_trailBeState = 1` written LAST in ArmTrailBe | Establishes volatile release fence over preceding plain-ref writes | §2.2 rationale + §2.3 ArmTrailBe code line 95 (last assignment) | PASS |
| `OnTrailBeAccountUpdate` reads `_trailBeState` FIRST | Volatile acquire fence before plain-ref reads | §2.2 rationale + §2.3 OnTrailBeAccountUpdate code line 135 (first read) | PASS |

### 4.3 CAS Pattern for High-Water PnL

| Check | Plan Location | Verdict |
|-------|---------------|---------|
| `Interlocked.CompareExchange(ref _trailBeLastPnl, newBits, oldBits)` on concurrent-callback race | §2.3 OnTrailBeAccountUpdate step (4) | PASS |
| Return on CAS miss (prevents duplicate advance) | §2.3 comment "another concurrent callback already updated" | PASS |

### 4.4 DisarmTrailBe Wiring Completeness

| Call Site | Plan Location | Verdict |
|-----------|---------------|---------|
| OnBeClick Connected→Idle case | §2.5 — `_engine.DisarmTrailBe()` adjacent to `DisarmPendingBe()` | PASS |
| Panel unload/cleanup path | §2.6 — explicitly required; engineer must locate `OnUnloaded` or `OnClosed` | PASS |
| Panel receives ArmTrailBe call on CONNECTED | §2.4 — `_engine.ArmTrailBe(...)` after existing BreakEven call | PASS |

### 4.5 Scan Checklist Completeness (T1)

| Scan | Rule | Plan Location | Verdict |
|------|------|---------------|---------|
| SCAN-01: lock() | JS-021 | §8 scan table | PASS |
| SCAN-02: async void CopyEngine | JS-033 | §8 scan table | PASS |
| SCAN-03: return null | JS-002 | §8 scan table | PASS |
| SCAN-04: CYC audit | CYC gate | §8 scan table | PASS |
| SCAN-05: volatile double vs volatile long | NT8-003 | §8 scan table | PASS |
| SCAN-06: Math.Clamp | NT8-034 | §8 scan table | PASS |
| SCAN-07: BitConverter pattern present | NT8-003 compliance | §8 scan table | PASS |

---

## 5. Design Review — T2 (DW-B12-DEFER-04)

### 5.1 Mapping Table Completeness

| Contract Name (B12 §T1 §1.10) | Current Name (CopyEngineTests.cs) | Action in Plan | Matches Contract | Verdict |
|-------------------------------|----------------------------------|----------------|-----------------|---------|
| `Trim_LimitOverload_LongPosition_EmitsSellLimitAtRefPlusTick` (T1-Test-1) | `Trim_LongPosition_EmitsLimitSellAtBidPlusBuffer` | RENAME | ✅ | PASS |
| `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` (T1-Test-2) | *(not present)* | ADD NEW | ✅ | PASS |
| `Flatten_LimitOverload_LongPosition_EmitsSellLimitFullQty` (T1-Test-3) | `Flatten_LongPosition_EmitsLimitSellAtBidPlusBuffer` | RENAME | ✅ | PASS |
| `Flatten_LimitOverload_ShortPosition_EmitsBuyToCoverLimitFullQty` (T1-Test-4) | `Flatten_ShortPosition_EmitsLimitBuyAtAskMinusBuffer` | RENAME | ✅ | PASS |
| `DispatchCopy_PttPrefixGate_SkipsOrderNamedPttTrimLimit` (T1-Test-5) | `PttPrefixGate_SkipsDispatchForPttOrders` | RENAME | ✅ | PASS |

4 renames + 1 new test = 5 total. All 5 B12 §1.10 contract names accounted for. ✅

### 5.2 New Test Coverage

| Check | Plan Location | Verdict |
|-------|---------------|---------|
| New test `Trim_LimitOverload_ShortPosition_EmitsBuyToCoverLimitAtRefMinusTick` covers previously untested short-direction path | §3.4 — body covers 3-arg overload signature + PTT- prefix + null-instrument guard | PASS |
| New test CYC=1 (no branch logic beyond null guard) | §3.4 comment "CYC: 1" | PASS |
| xUnit `[Fact]` only (no NUnit/MSTest) | §3.4 test body | PASS |

### 5.3 Rename Strategy

| Check | Plan Location | Verdict |
|-------|---------------|---------|
| Only method declaration line changes — no test bodies modified | §3.3 explicit statement | PASS |
| CYC unchanged for all renamed tests | §5 CYC table "PASS (renames only)" | PASS |

---

## 6. Violations Log

**No violations found.**

| Rule | Status | Notes |
|------|--------|-------|
| JS-021 | PASS | No lock() in any new method |
| JS-001 | PASS | No throw in hot path |
| JS-002 | PASS | No return null; all bare return; |
| JS-033 | PASS | OnTrailBeAccountUpdate is plain void |
| JS-023 | PASS | All cross-thread fields are volatile |
| JS-008 | PASS | No new brushes |
| NT8-003 | PASS | volatile long + BitConverter; no volatile double |
| NT8-026 | PASS | No order.TrailPrice usage |
| NT8-018 | PASS | No lock() |
| NT8-019 | PASS | async void only on OnBeConnected (Dispatcher-invoked, PERMITTED) |
| NT8-031 | PASS | using System.Threading already present |
| CYC ≤ 8 | PASS | All new methods: 4, 2, 5/6, 3 — all under limit |

---

## 7. Minor Observations (Not Violations)

### OBS-01: T1-Test-F Section Header vs Method Name Inconsistency

**Location**: Plan §2.7 T1-Test-F block
**Observation**: The section header reads `T1-Test-F: TrailBe_InitialPnlSeed_NoBitsEqualForNegativeAnd0` but the code body declares `public void TrailBe_CasLogic_NewBitsGreaterThanOld_CasSucceeds()`.

**Impact**: Documentation only — the method name in code governs. No rule violation. The declared method name accurately describes the test content (CAS idiom verification). Engineer should ensure the code name is used as-written.

**Classification**: Documentation inconsistency. Not a FAIL trigger.

---

## 8. Spec Coverage Matrix

| Requirement | Source | Addressed? | Plan Section |
|-------------|--------|------------|--------------|
| Auto-trail stop from BE CONNECTED state | DW-B12-DEFER-02 (original) / B13 §Open Items | ✅ YES | §2 (T1 full design) |
| Stays subscribed until explicitly disarmed | DW-B12-DEFER-02 behavioral requirement | ✅ YES | §2.3 OnTrailBeAccountUpdate comment |
| Advance stop on each new PnL high-water mark | DW-B12-DEFER-02 behavioral requirement | ✅ YES | §2.3 OnTrailBeAccountUpdate step (3)+(4)+(5) |
| CAS guard prevents duplicate advance on concurrent callbacks | Concurrency requirement (JS-021, NT8-018) | ✅ YES | §2.3 step (4) |
| DisarmTrailBe in panel unload path | Resource-cleanup requirement | ✅ YES | §2.6 |
| volatile long + BitConverter for PnL storage | NT8-003 | ✅ YES | §2.2 fields + §6 |
| Align test names to B12 §T1 §1.10 contract | DW-B12-DEFER-04 / B13 §Open Items | ✅ YES | §3.2 mapping table |
| 4 renames + 1 new test | DW-B12-DEFER-04 scope | ✅ YES | §3.2 + §3.4 |
| New test covers uncovered short-direction behavior | DW-B12-DEFER-04 gap | ✅ YES | §3.4 body |
| 6 new xUnit tests for T1 | T1 test coverage | ✅ YES | §2.7 T1-Test-A through F |
| xUnit [Fact] only — no NUnit/MSTest | Testing mandate | ✅ YES | §2.7 and §3.4 |
| No new scope items beyond the 2 in-scope items | Scope gate | ✅ YES | §1 shelved table |

---

## 9. Final Verdict

```
REVIEW_PASS
```

All P0 and P1 checks pass. No JS rule violations. No NT8 compiler constraint violations.
CYC ≤ 8 for all new and modified methods. Scope is exactly 2 items.
Mapping table complete (4 renames + 1 new test, all 5 B12 §1.10 contract names covered).
One minor documentation inconsistency (OBS-01) noted — not a violation.

**Phase 2 gate: OPEN → Phase 3 (ticket generation) is UNLOCKED.**
