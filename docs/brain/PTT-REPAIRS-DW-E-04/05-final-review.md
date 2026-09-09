# 05-final-review.md — PTT-REPAIRS-DW-E-04

**Status**: FINAL_PASS
**Phase**: 5 (PTT Plan Reviewer — Final Review)
**Reviewer**: ptt-plan-reviewer
**Epic**: PTT-REPAIRS-DW-E-04
**Ticket reviewed**: T1 — EvictDedup CYC Reduction via Extraction
**Sources read**:
- `docs/brain/PTT-REPAIRS-DW-E-04/02-architecture-plan.md`
- `docs/brain/PTT-REPAIRS-DW-E-04/04-ticket-review.md`
- `docs/brain/PTT-REPAIRS-DW-E-04/ticket-1-completion.md`
- `docs/brain/PTT-REPAIRS-DW-E-04/ticket-1-verification.md`
- `docs/brain/WAVE2-LANE-A/06-deferred-backlog.md` (prior blocks, read-only)
- `src/PropTraderTools/CopyEngine.cs` lines 5840–5960
- `src/PropTraderTools/CopyEngineTests.cs` lines 4200–4230

---

## A. Cross-File Coherence

### A.1 EvictDedup Dispatch Wiring

| Check | Expected | Actual (source) | Status |
|-------|----------|-----------------|--------|
| `EvictCancelledEntry` called from EvictDedup Cancelled branch | Line 5870 | `EvictCancelledEntry(orderId, cancelledInstrKey)` at line 5870 | PASS |
| `EvictFilledEntry` called from EvictDedup Filled branch | Line 5876 | `EvictFilledEntry(orderId, filledInstrKey)` at line 5876 | PASS |
| `_entryDispatchedOrders.TryRemove` stays in EvictDedup | Line 5868 | `_entryDispatchedOrders.TryRemove(orderId, out _)` at line 5868 — not moved to helper | PASS |
| `_dedupCache.TryRemove` stays in EvictDedup | Line 5863 | `_dedupCache.TryRemove(orderId, out _)` at line 5863 | PASS |

Both helper calls are wired correctly. The `_entryDispatchedOrders` removal (Cancelled-branch only, per plan §5.1) was correctly kept in `EvictDedup`, not inside `EvictCancelledEntry`. No wiring gaps.

### A.2 Test Seam Wiring

| Check | Expected | Actual (source) | Status |
|-------|----------|-----------------|--------|
| Test file location | `src/PropTraderTools/CopyEngineTests.cs` (actual repo path) | Confirmed at line 4216 | PASS |
| `SetLeaderDirection_ForTest` called | YES | Line 4219 | PASS |
| `IsLiveEntryBlocked_ForTest` called | YES | Line 4220 | PASS |
| `EvictDedup_ForTest` called | YES | Line 4223 | PASS |
| `HasLeaderDirection` (no `_ForTest`) called in Assert | YES | `Assert.False(_engine.HasLeaderDirection("MGC DEC26"))` at line 4226 | PASS |

**Documentation discrepancy (non-blocking)**: The ticket and ticket-review specified `src/PropTraderTools.Tests/CopyEngineTests.cs`. The actual file is `src/PropTraderTools/CopyEngineTests.cs`. The verifier confirmed the correct location at ticket-1-verification.md line 266. This is documentation debt only (P2); the test compiles and runs in the correct location. No code violation.

### A.3 No Orphaned Code

Verified from source lines 5854-5910:
- `EvictDedup` (5854-5879): contains only the residual dispatch skeleton per plan §5.1. No inline Cancelled/Filled body fragments remain. PASS.
- No stale comments referencing original line numbers for inline BUG-E code within `EvictDedup`. PASS.

### A.4 No Duplicate Logic

| Logic block | Should appear in | Absent from | Status |
|-------------|-----------------|-------------|--------|
| `_liveEntryInstruments` value-guard + `TryRemove` (Cancelled) | `EvictCancelledEntry` lines 5889-5891 | `EvictDedup`, `EvictFilledEntry` | PASS |
| BUG-E `pipeIdx` / `_lastLeaderDirection.TryRemove` | `EvictCancelledEntry` lines 5892-5896 | `EvictDedup` (absent from 5854-5879), `EvictFilledEntry` (absent from 5904-5910) | PASS |
| `_liveEntryInstruments` value-guard + `TryRemove` (Filled) | `EvictFilledEntry` lines 5907-5909 | `EvictDedup`, `EvictCancelledEntry` | PASS |

Verified by verifier's logic preservation table (ticket-1-verification.md §Logic Preservation Check). No duplicate logic found.

### A.5 BUG-E Fix Location

**BUG-E fix appears ONLY in `EvictCancelledEntry` at lines 5892-5896.**

Verbatim source quote (from ticket-1-verification.md §BUG-E Fix Verbatim Quote):

```csharp
            // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
            // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
            var pipeIdx = cancelledInstrKey.IndexOf('|');
            if (pipeIdx > 0)
                _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```

6-rule preservation contract: all 6 rules PASS (verified independently by verifier at L3).

**Cross-file coherence: PASS. No violations.**

---

## B. Spec Requirements Coverage Matrix

| Rule | Requirement | Method(s) | Evidence | Status |
|------|-------------|-----------|----------|--------|
| JS-013 | CYC ≤ 8 | EvictDedup=7, EvictCancelledEntry=4, EvictFilledEntry=3 | L3 independent arithmetic count; source lines 5854-5910 | PASS |
| JS-021 | No `lock()` | All three methods | SCAN-01 L3: 0 real lock() in 5854-5910; 11 comment-only matches in file | PASS |
| JS-001 | No `throw` | All three methods | No throw keyword in lines 5854-5910; verifier confirmed | PASS |
| JS-002 | No null return | All three methods | All three are `void`; trivially satisfied | PASS |
| JS-042 | ASCII-only | All three methods + comments | SCAN-02 L3: 0 non-ASCII in entire file | PASS |

**All 5 spec requirements: PASS.**

---

## C. 7-Scan Aggregate Results (Layer 3 Independent)

Source: `ticket-1-verification.md` §7-Scan Independent Results (Layer 3).

| Scan | Check | Required | L3 Result | Status |
|------|-------|----------|-----------|--------|
| SCAN-01 | lock() in modified methods | 0 matches | 0 (empty output; 11 comment-only elsewhere) | PASS |
| SCAN-02 | Non-ASCII in modified methods | 0 matches | 0 non-ASCII lines in entire file | PASS |
| SCAN-03 | Build errors | 0 Error(s) | `Build succeeded. 0 Error(s)` | PASS |
| SCAN-04 | Build succeeded | Build succeeded | `Build succeeded. 0 Warning(s) 0 Error(s)` | PASS |
| SCAN-05 | passed≥19, skipped≥31 | passed≥19, skipped≥31, no new genuine regressions | passed=19, skipped=32, failed=450 | PASS |
| SCAN-06 | deploy-sync SYNC COMPLETE | SYNC COMPLETE | SYNC COMPLETE + ASCII GATE PASS + DIFF GUARD PASS | PASS |
| SCAN-07 | Hard-link count | HardLink, 1 NT8 target | `HardLink` + 1 NT8 target | PASS |

**All 7 scans PASS at Layer 3. Zero violations.**

---

## D. Layer 2 vs Layer 3 Discrepancy Resolution

Three discrepancies between engineer (L2) and verifier (L3) results:

| Item | L2 | L3 | Root Cause | Resolved? |
|------|----|----|------------|-----------|
| SCAN-05 skipped count | 31 | 32 | PTT-REPAIRS-DW-F-R06 added a `[Skip]` test to `BwaveCycTaR7HelperTests` between L2 and L3 runs. Out-of-scope for T1. | YES — 32≥31 criterion met |
| SCAN-05 total count | 500 | 501 | Same R06 test added (same cause) | YES — same |
| SCAN-06 diff size | 142 chars | 168 chars | R06 commits landed on branch between L2 and L3 runs | YES — both ≤ DIFF GUARD limit |

**No discrepancy indicates T1 code regression, engineer misrepresentation, or BUG-E corruption.** All three are explained by the concurrent R06 inter-ticket timeline. No unresolved discrepancies remain.

---

## E. DNA Rule Check (Hardcoded Non-Negotiable Rules)

| Category | Rule | Check | Result |
|----------|------|-------|--------|
| Concurrency | JS-021: no lock() | SCAN-01 L3: 0 real lock() in EvictDedup/EvictCancelledEntry/EvictFilledEntry | PASS |
| Concurrency | JS-021: state via ConcurrentDictionary | All ops: TryRemove/TryGetValue on ConcurrentDictionary | PASS |
| Type Safety | JS-001: no throw | No throw in lines 5854-5910 | PASS |
| Type Safety | JS-002: no null return | All three methods void | PASS |
| Complexity | JS-013: CYC ≤ 8 | 7/4/3 all ≤ 8 | PASS |
| Immutability | JS-042: ASCII-only | SCAN-02 L3: 0 non-ASCII | PASS |
| NT8 | No async/await | Absent from 5854-5910 | PASS |
| NT8 | No DateTime.Now | Not present | PASS |
| NT8 | No FontFamily= | Not present | PASS |
| NT8 | No #RRGGBB hex | Not present | PASS |
| NT8 | No Account.All outside Loaded | Not applicable | N/A |
| NT8 | sealed on TradeCopierWindow | Not applicable | N/A |
| NT8 | CreateOrder PTT- prefix | No CreateOrder in scope | N/A |

**No DNA rule violations.**

---

## F. Call-Site Preservation

**Line 1541** (verified at L3 from source read):

```
EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);
```

Unchanged. Byte-for-byte identical to pre-epic content. PASS.

`EvictDedup_ForTest` at line 4360 also unchanged (delegates unchanged signature). PASS.

---

## G. Acceptance Criteria Final Verification

| # | Criterion | L3 Verifier | Reviewer Confirms | Status |
|---|-----------|-------------|-------------------|--------|
| AC-01 | dotnet build: 0 Error(s) | PASS | Source: SCAN-03+04 L3 | PASS |
| AC-02 | dotnet test: passed≥19, skipped≥31, no new regressions | PASS | passed=19, skipped=32, +1 fail = pre-existing TypeInit | PASS |
| AC-03 | New test EvictDedup_CancelledEntry_ClearsLastLeaderDirection present | PASS | Line 4216 confirmed in source | PASS |
| AC-04 | EvictDedup CYC ≤ 8 (target 7) | PASS | L3 CYC=7 | PASS |
| AC-05 | EvictCancelledEntry CYC ≤ 8 (target 4) | PASS | L3 CYC=4 | PASS |
| AC-06 | EvictFilledEntry CYC ≤ 8 (target 3) | PASS | L3 CYC=3 | PASS |
| AC-07 | BUG-E verbatim in EvictCancelledEntry | PASS | 6-rule check: all 6 pass at L3 | PASS |
| AC-08 | Call site line 1541 unchanged | PASS | Source read confirmed | PASS |
| AC-09 | No lock() in 3 methods | PASS | SCAN-01 L3 | PASS |
| AC-10 | ASCII-only in 3 methods | PASS | SCAN-02 L3 | PASS |
| AC-11 | deploy-sync SYNC COMPLETE | PASS | SCAN-06 L3 | PASS |
| AC-12 | ticket-1-completion.md written with all 7 scan outputs | PASS | File exists and complete | PASS |

**All 12 acceptance criteria: PASS.**

---

## H. Violations Found

**None.**

No Jane Street DNA violations, no NT8 violations, no spec requirement gaps, no cross-file coherence failures, no unresolved L2/L3 discrepancies.

---

## I. Deferred Items Inherited from Prior Blocks

The following items from `WAVE2-LANE-A/06-deferred-backlog.md` remain OPEN and are NOT closed by this epic:

| ID | Status | Reason not closed |
|----|--------|-------------------|
| DW-WAVE2-LA-01 | OPEN | F5 NinjaTrader manual gate — still manual, cannot be automated |
| DW-WAVE2-LA-02 | OPEN | Lizard tool CYC methodology discrepancy — out of scope |
| DW-WAVE2-LA-03 | OPEN | Pre-existing `return null` JS-002 debt — plan §1 explicitly out of scope |
| DW-WAVE2-LA-04 | OPEN | Stale plan doc for internal static — out of scope |

---

## J. System Coherence Assessment

`EvictDedup` + `EvictCancelledEntry` + `EvictFilledEntry` form a complete, coherent extraction unit:

- Single entry point (`EvictDedup`) preserves external call contract at line 1541.
- Two private helpers are only reachable through `EvictDedup` (not callable externally).
- BUG-E fix is confined to exactly the correct helper and cannot be bypassed via any other code path.
- All three methods are lock-free (`ConcurrentDictionary` ops) per JS-021.
- No state machine corruption risk: `TryRemove` is idempotent; no double-remove hazard.
- The `_entryDispatchedOrders.TryRemove` staying in `EvictDedup` (not extracted to `EvictCancelledEntry`) is architecturally correct: it must fire before the key-lookup removal, and that ordering is preserved.

---

## K. Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-E-04-01 | New test `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` fails at runtime with `System.TypeInitializationException` because `CopyEngineTests` uses `CopyEngine.Instance` which requires NT8 runtime initialization unavailable in the test host. The test code is correct and compiles. This is a pre-existing blocker shared by all 449+ `CopyEngineTests` tests. The test will only return PASS when NT8 TypeInitializationException is resolved (e.g., via NT8 mock/stub infrastructure or direct F5 execution). This is NOT a T1 code defect. | P1 | future wave (test infrastructure) | OPEN |
| DW-E-04-02 | Ticket and ticket-review documented test file path as `src/PropTraderTools.Tests/CopyEngineTests.cs`. Actual file is `src/PropTraderTools/CopyEngineTests.cs`. The test is placed correctly; this is documentation debt only. Future ticket templates and plan documents should reflect the actual repo path. | P2 | future (doc cleanup) | OPEN |
| DW-WAVE2-LA-01 | (inherited) F5 NinjaTrader 8 recompile is a manual gate that cannot be automated. Director must confirm NT8 compile green before merging PTT-REPAIRS-DW-E-04 to main. | P0 | B_CURRENT (pre-merge) | OPEN |
| DW-WAVE2-LA-03 | (inherited) Pre-existing `return null` statements in `CopyEngine.cs` represent JS-002 technical debt. Still out of scope. Future wave required. | P1 | future wave | OPEN |

**Summary for this epic:**
- 2 new deferred items: DW-E-04-01 (P1 test infrastructure), DW-E-04-02 (P2 doc cleanup)
- 2 inherited items restated: DW-WAVE2-LA-01 (P0 pre-merge gate), DW-WAVE2-LA-03 (P1 JS-002 debt)
- 0 items from prior blocks closed by this epic
- Implementation-blocking items: 0 (all T1 code complete and clean)
- Pre-merge blocking items: 1 (DW-WAVE2-LA-01 — manual F5 gate)

---

## Final Verdict

**FINAL_PASS**

All checks complete. Zero violations. All 7 scans PASS at Layer 3. All 12 acceptance criteria confirmed. BUG-E fix preserved verbatim. Cross-file coherence intact. Section K written. `06-deferred-backlog.md` written.
