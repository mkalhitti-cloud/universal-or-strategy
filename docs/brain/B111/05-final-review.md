# Final Review -- Block B111-T1

**Block**: B111-T1
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Review Date**: 2026-08-28
**Engineer Commit**: 8a893796
**Plan**: docs/brain/B111/02-architecture-plan.md (REVIEW_PASS)
**Ticket Review**: docs/brain/B111/04-ticket-review.md (TICKET_REVIEW_PASS)
**Ticket Completion**: docs/brain/B111/ticket-1-completion.md (BUILD_PASS)
**Verification**: docs/brain/B111/ticket-1-verification.md (VERIFY_PASS)

---

## Block Summary

Block B111-T1 closes two P0 live-session defects discovered on 2026-08-28:

| DW ID | Name | Combo | Status |
|-------|------|-------|--------|
| DW-B111 | `_beReplaceAttempts` Counter Reset in Timer Callback (Infinite BE-Retry Loop) | Combo D (QX-ALL -> BE-ALL) | CLOSED |
| DW-B112 | `_qxCancelInProgress` Guard Cleared Before Async Cancel Events Arrive | Combo C (BE-ALL -> QX-ALL) | CLOSED |

**Files modified**:

| File | Change type | Commit |
|------|-------------|--------|
| `src/PropTraderTools/CopyEngine.cs` | 4 changes: delete L1465, update constants/strings, insert guard block, update comment | 8a893796 |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Comment addition only (Change E, zero structural change) | 8a893796 |
| `src/PropTraderTools/Tests/B111Tests.cs` | New file -- 4 xUnit [Fact] tests | 8a893796 |

---

## Cross-File Coherence Check

### DW-B111 Reset Contract

| Check | Source Evidence | Result |
|-------|----------------|--------|
| `_beReplaceAttempts.TryRemove` absent from timer callback (L1460-1490) | `grep _beReplaceAttempts\.TryRemove` returns only L1354 and L1409 -- zero matches in L1460-1490 region. L1465 is now `bool flat = IsFlat(FindPosition(...));` | **PASS** |
| TryRemove at L1354 (TryFireFollowerBeRetry) still present | Grep confirms: L1354 `_beReplaceAttempts.TryRemove(o.Account.Name, out _); // DW-B82-01: reset on slot consumption` | **PASS** |
| TryRemove at L1409 (TryEvictFollowerBeSlot) still present | Grep confirms: L1409 `_beReplaceAttempts.TryRemove(accName, out _); // ALWAYS reset on terminal` | **PASS** |
| Counter resets only on slot consumption or position close -- NOT on timer tick | Both correct reset sites intact; timer callback reset (former L1465) is absent | **PASS** |

**DW-B111 Reset Contract: COHERENT**

### DW-B112 Guard Layering

| Check | Source Evidence | Result |
|-------|----------------|--------|
| Belt-and-suspenders `_qxCancelInProgress` guard at L2294 still present | Direct source read L2294: `if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)) return;` -- intact, unchanged | **PASS** |
| New structural PTT-QX presence check (3c) inserted AFTER the belt-and-suspenders guard | Direct source read: guard (3b) at L2292-2295, guard (3c) at L2298-2324 -- ordering correct | **PASS** |
| Guard ordering: `_qxCancelInProgress` check first, PTT-QX presence check second | L2294 (3b) precedes L2303 (3c) -- confirmed | **PASS** |
| PTT-QX presence check uses `.ToList().Any()` (W1 resolved -- option b) | Direct source read L2305: `.ToList()` between `acc.Orders` and `.Any(` -- confirmed | **PASS** |
| Guard block correctly logs DW-B112 diagnostic and returns | L2317-2323: `NinjaTrader.Code.Output.Process("[BE-DIAG] TryReplacePttBeBrackets: " + acc.Name + " -- PTT-QX orders Working/Submitted, skipping recovery (DW-B112)"); return;` | **PASS** |
| PttGlobalQuickExit.cs `finally` block contains 4-line DW-B112 comment | Direct source read L161-164: all 4 comment lines present above `TryRemove` call at L165 | **PASS** |
| TryRemove in `finally` (PttGlobalQuickExit.cs L165) preserved unchanged | L165: `CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);` -- intact | **PASS** |

**DW-B112 Guard Layering: COHERENT**

### Cross-File JS Violation Scan

| Check | Evidence | Result |
|-------|---------|--------|
| `lock()` in CopyEngine.cs changed lines | `grep "lock\s*\("` returns 5 matches: L1085, L1107, L1902, L2622, L3461 -- ALL are comment text ("no lock (JS-021)") containing the word "lock". Zero actual `lock()` statements in any changed line. | **PASS (JS-021)** |
| `lock()` in PttGlobalQuickExit.cs | `grep "lock\s*\("` returns zero matches. | **PASS (JS-021)** |

**Zero P0 concurrency violations across both files.**

### CYC Final State

| Method | File | Plan Projection | Source Verified | Within Budget? | Result |
|--------|------|----------------|----------------|---------------|--------|
| `TryReplacePttBeBrackets` | CopyEngine.cs | CYC=7 | CYC=7 (7 guards: null, follower, flat, 3b qxCancelInProgress, 3c PTT-QX, 4 attempt, 5 TryAdd -- manually re-counted from L2284-2356 source) | YES (<=8) | **PASS** |
| `QueueBeRetryFallback` (outer method) | CopyEngine.cs | CYC=1 | CYC=1 (unchanged; removing a statement from inside an existing branch does not change branch count) | YES (<=8) | **PASS** |
| `TryFireFollowerBeRetry` (unchanged) | CopyEngine.cs | CYC=5 | CYC=5 | YES (<=8) | **PASS** |
| `TryEvictFollowerBeSlot` (unchanged) | CopyEngine.cs | CYC=6 | CYC=6 | YES (<=8) | **PASS** |
| `ExecuteOne` (PttGlobalQuickExit.cs, comment only) | PttGlobalQuickExit.cs | unchanged | unchanged | YES (<=8) | **PASS** |

**All touched methods: CYC <= 8. No budget violation.**

### Spec Requirements Satisfied

| Requirement | Change | Verified In Source | Result |
|-------------|--------|-------------------|--------|
| DW-B111 primary fix: TryRemove removed from timer callback | Change A (delete L1465) | Grep: zero `_beReplaceAttempts.TryRemove` in L1460-1490; L1465 is now `bool flat = IsFlat(...)` | **PASS** |
| DW-B111 cap raised 3 -> 5 (constants + log strings) | Changes B-1/B-2/B-3 | L2327: `prevAttempts >= 5`; L2332: `"max 5 attempts"`; L2352: `"/5, slot registered"` | **PASS** |
| DW-B112 structural PTT-QX presence check (Option 2) | Change C | L2298-L2324: guard block present with `.ToList().Any()`, correct filter, log, and return | **PASS** |
| DW-B112 `_qxCancelInProgress` belt-and-suspenders preserved | (no change) | L2294: guard intact, unchanged | **PASS** |
| DW-B112 PttGlobalQuickExit.cs comment (Change E) | Change E | L161-164: 4 comment lines present above TryRemove | **PASS** |
| Method header comment updated to CYC=7 (Change D) | Change D | L2278: `// CYC=7: (1) null guard, (2) follower guard, (3) flat guard, (3b) qxCancelInProgress guard,` | **PASS** |
| 4 xUnit [Fact] tests present | New B111Tests.cs | Independent verifier confirmed all 4 tests by name via Get-Content; `using Xunit;` -- xUnit only | **PASS** |
| CYC <= 8 all touched methods | See CYC table above | All confirmed <= 8 | **PASS** |

---

## All Spec Requirements Satisfied

- [x] DW-B111: primary fix (TryRemove removed from timer callback) -- CONFIRMED at source
- [x] DW-B112: structural PTT-QX presence check added -- CONFIRMED at source
- [x] `_qxCancelInProgress` preserved (belt-and-suspenders) -- CONFIRMED at L2294
- [x] 4 tests present and verified by independent verifier -- CONFIRMED (all 4 [Fact] methods)
- [x] CYC <= 8 all touched methods -- CONFIRMED (TryReplacePttBeBrackets=7, all others unchanged)

---

## JS Rules Final Compliance

Direct scan results from source (independent of engineer and verifier reports):

| Rule | Scan Result | Result |
|------|------------|--------|
| JS-021 No lock() | CopyEngine.cs: 5 grep hits, ALL comment text. PttGlobalQuickExit.cs: 0 hits. Zero actual lock() statements in any changed line. | **PASS** |
| JS-033 No async void | CopyEngine.cs: 1 hit at L1440 (comment only). PttGlobalQuickExit.cs: 1 hit at L4 (comment only). Zero actual async void methods. | **PASS** |
| JS-001 No throw in hot paths | TryReplacePttBeBrackets and QueueBeRetryFallback are void methods; zero new throw statements introduced. | **PASS** |
| JS-002 No return null | Both methods return void; new return at L2323 is bare `return;`. All pre-existing `return null` hits are in other methods, unchanged by B111-T1. | **PASS** |
| ASCII-only | Scan 7 (Layer 3 independent): zero non-ASCII in all 3 changed files (B111Tests.cs, CopyEngine.cs, PttGlobalQuickExit.cs). Engineer repaired 2 pre-existing non-ASCII sequences in CopyEngine.cs. | **PASS** |
| CYC <= 8 | TryReplacePttBeBrackets=7, QueueBeRetryFallback=1. All other touched methods unchanged. | **PASS** |
| PTT- prefix | No new CreateOrder calls introduced. | **PASS** |
| DateTime.UtcNow (not Now) | Not touched. | **PASS** |
| No FontFamily | Not touched. | **PASS** |
| No hardcoded hex color | Not touched. | **PASS** |
| No async/await in lifecycle | Not touched. | **PASS** |
| No sealed TradeCopierWindow | Not touched. | **PASS** |

**Zero P0 violations. Zero P1 violations.**

---

## 7-Scan Aggregate Result

All 7 scans returned zero violations across `src/PropTraderTools/` in aggregate (Layer 2 and Layer 3 independent results cross-checked with zero discrepancies per verification report):

| Scan | Scope | Layer 2 | Layer 3 | Result |
|------|-------|---------|---------|--------|
| SCAN-01: lock() | CopyEngine.cs | PASS | PASS | **PASS** |
| SCAN-02: async void | CopyEngine.cs | PASS | PASS | **PASS** |
| SCAN-03: return null (new lines) | CopyEngine.cs | PASS | PASS | **PASS** |
| SCAN-04: lock() | PttGlobalQuickExit.cs | PASS | PASS | **PASS** |
| SCAN-05: async void | PttGlobalQuickExit.cs | PASS | PASS | **PASS** |
| SCAN-06: CYC audit | Both methods (manual) | PASS | PASS | **PASS** |
| SCAN-07: ASCII-only | All 3 files | PASS | PASS | **PASS** |

---

## Section K -- Deferred Work

### Deferred Items This Block (B111-T1)

- **B111-DEFER-01**: `PttBreakEvenSwap.cs` secondary fix -- add `isRetry` parameter to `Execute()` to skip `CancelQxBrackets` call on retry invocations (~L70). Priority: **P2**. Rationale: Not required for DW-B111/B112 correctness. The primary fix (counter cap) terminates the loop regardless. The signature change requires a dedicated block with its own review cycle. Deferred per architect Section 5 decision. Target block: **B112 or next available block**.

- **B111-DEFER-02**: Combo D + Combo C live SIM re-test (Director gate). Priority: **P1**. Prerequisite for confirming both fixes behave correctly end-to-end in live NT8 SIM session. Requires: NT8 restart (fresh order book), run Combo D (QX-ALL -> BE-ALL), then Combo C (BE-ALL -> QX-ALL), capture Output Tab 1 log. Target: **Immediate (Director-owned)**.

- **B111-DEFER-03**: F5 NinjaTrader 8 compilation gate (Director-owned). Priority: **P0** (required before live re-test). Prerequisite for B111-DEFER-02. Sync + MD5 verify already passed (0 MISMATCH, 16 files). F5 is the runtime compile gate. Target: **Immediate (Director-owned)**.

### Section K -- Carry-Forward from B107 Backlog (Open Items)

Items from `docs/brain/B107/06-deferred-backlog.md` that remain open and are not resolved by B111-T1:

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B107 | `MoveStopToBreakEven` Step A snapshots stale `PTT-BE-Target-*` on followers. Fix requires `SnapshotBeTargets` helper extraction to keep CYC <= 8. | P2 | OPEN -- not affected by B111-T1 |
| B107-DEFER-01 | F5 NinjaTrader 8 Compilation Gate (B107 changes) | P0 | OPEN (Director-owned; also superseded/merged into B111-DEFER-03 for the B111-T1 sync) |
| B107-DEFER-02 | Combo C Live Re-Test (DW-B105 + DW-B106 behavioral validation) | P1 | OPEN -- now extended to cover B111-T1 fixes as well (merged into B111-DEFER-02) |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Low | OPEN (carry-forward from DW-B89) |
| DW-B42-02 | Live NT8 F5 verification (Direction 1 and Direction 2) | High | OPEN (carry-forward from DW-B89) |
| DW-B42-03 | IsPttQxTarget range extension for future T4/T5 slots | Conditional | OPEN (carry-forward from DW-B89) |
| DW-PTT-BE-FIX-01 | Lazy re-resolve for null followers (DW-B85 Option A) | Medium | OPEN (carry-forward from DW-B89) |
| DW-PTT-BE-FIX-02 | SIM gate Path B 3-cycle runtime verification | High | OPEN (carry-forward from DW-B89) |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (83 errors, CS0433) -- blocks full test suite | High | OPEN (carry-forward from DW-B89; complexity_audit.py also unavailable) |
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate (DW-B89 changes) | P0 | OPEN (carry-forward from DW-B89) |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal | High | OPEN (carry-forward from DW-B89) |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case | High | OPEN (carry-forward from DW-B89) |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) | High | OPEN (carry-forward from DW-B89) |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle | High | OPEN (carry-forward from DW-B89) |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 in spec HTML | Medium | OPEN (carry-forward from DW-B89) |

Note: **DW-B79-03** (`_qxCancelInProgress` belt-and-suspenders guard) is NOT closed and is NOT a deferred item -- it remains active as a live protective guard in the codebase at L2294. Its purpose continues: it covers the synchronous window between `TryAdd` and `TryRemove` in `PttGlobalQuickExit.ExecuteOne`.

### Section K -- Deferred Work Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B111 | `_beReplaceAttempts` Counter Reset in Timer Callback | P0 | B111 | **CLOSED B111-T1** |
| DW-B112 | `_qxCancelInProgress` Guard Cleared Before Async Events Arrive | P0 | B111 | **CLOSED B111-T1** |
| B111-DEFER-01 | PttBreakEvenSwap.cs -- add `isRetry` param to skip CancelQxBrackets on retry | P2 | B112 or later | OPEN |
| B111-DEFER-02 | Combo D + Combo C live SIM re-test | P1 | Immediate (Director) | OPEN |
| B111-DEFER-03 | F5 NinjaTrader 8 compilation gate | P0 | Immediate (Director) | OPEN |
| DW-B107 | MoveStopToBreakEven stale PTT-BE-Target-* on followers | P2 | B112 or later | OPEN |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (83 errors, CS0433) | High | Dedicated block | OPEN |

---

## Verdict

**FINAL_PASS**

All cross-file coherence checks passed. All spec requirements for DW-B111 and DW-B112 are satisfied.
Zero P0 or P1 Jane Street rule violations in any changed code. All 7 scans returned zero violations
(Layer 2 and Layer 3 consistent, no discrepancies). Method CYC annotations match source.
Section K present. `06-deferred-backlog.md` written.

---

*Reviewer: ptt-plan-reviewer | Block B111-T1 | Phase 5 (Final Review) | 2026-08-28*
*Plan Review: REVIEW_PASS | Ticket Review: TICKET_REVIEW_PASS | Verification: VERIFY_PASS*
