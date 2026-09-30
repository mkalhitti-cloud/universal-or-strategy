# B129 LaneA Final Review — DW-B135

**Block**: B129 LaneA
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-31
**Epic**: DW-B135 — Reversal Guard False-Positive After Leader Flat
**Input artifacts**:
- `docs/brain/B129/LaneA-02-architecture-plan.md` — REVIEW_PASS
- `docs/brain/B129/LaneA-04-ticket-review.md` — TICKET_REVIEW_PASS
- `docs/brain/B129/LaneA-ticket-1-completion.md` — BUILD_PASS
- `docs/brain/B129/LaneA-ticket-1-verification.md` — VERIFY_PASS
- `docs/brain/B129/LaneB-06-deferred-backlog.md` — carry-forward items (READ ONLY)
- `src/PropTraderTools/CopyEngine.cs` — READ ONLY (final state verified)

---

## FK-1 — Build Clean

**Check**: SCAN-07 from `LaneA-ticket-1-verification.md` — build succeeded, 0 errors, 0 warnings.
Test count: minimum 6 B129Tests passing (3 LaneB + 3 LaneA).

| Item | Value |
|------|-------|
| Build result | `Build succeeded.` |
| Errors | 0 |
| Warnings | 0 |
| Total B129-filter tests | 11/11 (6 B129Tests class + 5 B128Tests class matched by filter) |
| B129Tests class — LaneA tests | 3 (all PASS) |
| B129Tests class — LaneB tests | 3 (all PASS, non-regression) |

**Verdict**: **PASS**

---

## FK-2 — Layer 2 vs Layer 3 Agreement

**Check**: V-09 table in `LaneA-ticket-1-verification.md` — zero discrepancies across all 7 scans.

| Scan | Layer 2 (Engineer) | Layer 3 (Verifier) | Agreement |
|------|-------------------|-------------------|-----------|
| SCAN-01 lock( | 3 hits, all comments | 3 hits, all comments (L297, L330, L2606) | EXACT MATCH |
| SCAN-02 async void | 0 hits | 0 hits | EXACT MATCH |
| SCAN-03 return null | 7 hits, all pre-existing | 7 hits, all pre-existing | EXACT MATCH |
| SCAN-04 throw new | 0 hits | 0 hits | EXACT MATCH |
| SCAN-05 _lastLeaderDirection | 7 hits (L331, 1914, 1985, 2401, 2410, 2412, 2413/2414) | 7 hits (same lines) | EXACT MATCH |
| SCAN-06 TryFirePositionState | Defn L2361, no LaneB overlap | Defn L2361, no LaneB overlap | EXACT MATCH |
| SCAN-07 Build+Test | 0 errors, 11/11 pass | 0 errors, 0 warnings, 11/11 pass | EXACT MATCH |

**Verdict**: **PASS** — zero discrepancies.

---

## FK-3 — Fix Correctness: Direction Clear Logic

**Check**: Direct read of `CopyEngine.cs` L2355-2420.

**Source code verified at L2361-2406**:
```
L2382-2383: if (prior == newVal) return;    // Interlocked CAS guard
L2385-2388: // DW-B135 comment block
L2389:      if (!hasPos)                     // (a) PRESENT, correct position
L2390:      {
L2391:          bool isLeaderAcct = false;
L2392:          foreach (var r in _rules)
L2393:          {
L2394:              if (e.Order.Account.Name == r.MasterAccount?.Name)  // (b) CORRECT predicate
L2395:              {
L2396:                  isLeaderAcct = true;
L2397:                  break;
L2398:              }
L2399:          }
L2400:          if (isLeaderAcct)
L2401:              _lastLeaderDirection.TryRemove(instr, out _);  // (c) CORRECT operation
L2402:      }
L2404:      bool hasEntries = HasWorkingEntries(...);
```

| Sub-check | Requirement | Actual | Result |
|-----------|-------------|--------|--------|
| (a) Block position | After Interlocked CAS (L2382-2383), before `bool hasEntries` | After L2383, before L2404 | PASS |
| (b) Predicate | `e.Order.Account.Name == r.MasterAccount?.Name` | Exact match at L2394 | PASS |
| (c) Operation | `_lastLeaderDirection.TryRemove(instr, out _)` | Exact match at L2401 | PASS |
| (d) No lock() | Zero `lock(` in inserted block | SCAN-01: 3 comment-only hits, none in L2389-2402 | PASS |

**Verdict**: **PASS**

---

## FK-4 — DW-B128 Preservation Proof

**Check**: `hasPos` assignment at L2372; `if (!hasPos)` block executes ONLY when leader fully flat.

**Evidence**:
- `bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument)` at L2372.
- `if (!hasPos)` at L2389: block body (including `TryRemove`) executes only when `hasPos=False`.
- During DW-B128 race window: leader position still open → `HasOpenPosition` returns `true` → `hasPos=True` → `!hasPos=False` → direction-clear block NOT entered → `TryRemove` NOT called.
- Direction key is preserved during the race window. `_lastLeaderDirection` retains the prior action. `IsReversalToFlatFollower` fires correctly on the next Sell signal.
- Test `B129_DW135_DW128ProtectionPreservedDuringRaceWindow` asserts `IsReversalToFlatFollower(Sell, Buy, followerIsFlat: true) == true` — PASS (SCAN-07 confirmed).

**Verdict**: **PASS** — DW-B128 protection fully preserved.

---

## FK-5 — LaneB Methods Untouched

**Check**: Direct read of `CopyEngine.cs` L2028-2060 (LaneB methods). Verification V-06 confirms full LaneB range.

| Method | Location | Status |
|--------|----------|--------|
| `IsAtmSTPOrder` | L2028-2030 | Intact — `EndsWith("STP", StringComparison.OrdinalIgnoreCase)` single-line lambda unchanged |
| `SyncFollowerBracket` header | L2048-2058 | Intact — `(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)` header, `FindFollowerBracketOrder` call at L2056 unchanged |
| `SyncAtmFollowerBracket` | L2113-2159 (per V-06) | Intact — cancel + CreateOrder + Submit blocks confirmed by V-06 independent read |
| `IsReversalToFlatFollower` | L3615-3621 (per V-06) | Intact — `return currentAction != lastAction && followerIsFlat;` confirmed |

Gap between LaneB end (~L2159) and `TryFirePositionState` definition (L2361): 202 lines. No overlap.

**Verdict**: **PASS**

---

## FK-6 — All 6 B129 Tests Green

**Check**: SCAN-07 from `LaneA-ticket-1-verification.md`.

| Test Name | Lane | Status |
|-----------|------|--------|
| `B129_DW134_STPSuffixDetectedByIsBracketLegStatic` | LaneB | PASS |
| `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` | LaneB | PASS |
| `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` | LaneB | PASS |
| `B129_DW135_GuardClearedAfterLeaderFlat` | LaneA | PASS |
| `B129_DW135_DW128ProtectionPreservedDuringRaceWindow` | LaneA | PASS |
| `B129_DW135_FirstEntryAfterRestartNotBlocked` | LaneA | PASS |

**Verdict**: **PASS** — all 6 required tests green. No failures.

---

## FK-7 — P0 Jane Street Rule Compliance

**Check**: All 7 scans from independent Layer 3 (verifier).

| Rule | Check | SCAN | Result |
|------|-------|------|--------|
| JS-021 — no lock() | 0 executable hits | SCAN-01 | **PASS** — 3 comment-only hits |
| JS-001 — no throw in hot paths | 0 throw new | SCAN-04 | **PASS** — 0 hits entire file |
| JS-002 — no new return null | 0 new return null | SCAN-03 | **PASS** — 7 pre-existing only, none in new code |
| JS-033 — no async void | 0 hits | SCAN-02 | **PASS** — 0 hits |
| ASCII-only | No Unicode/emoji/curly quotes | PART 3 DNA check | **PASS** — DW-B135 block is ASCII-only |
| CYC <= 8 | TryFirePositionState CYC = 6 | PART 5 | **PASS** — 6 <= 8 |

**Verdict**: **PASS** — all P0 rules clean.

---

## FK-8 — Carry-Forward Items Unaffected

**Check**: `LaneB-06-deferred-backlog.md` items vs LaneA scope.

**LaneA scope**: `TryFirePositionState` (L2361-2406) + 4 test accessor shims (L2408-2414) + B129Tests.cs append.

**All 19 carry-forward items reviewed**:

| Item | References TryFirePositionState / _lastLeaderDirection / reversal guard? | Affected? |
|------|-------------------------------------------------------------------------|-----------|
| DW-B134-OCO | No — OCO orphan in SyncAtmFollowerBracket (L2100) | NO |
| DW-B129-01 | No — Quick2t/QAll2t UI handlers | NO |
| DW-B133 | No — PttGlobalQuickExit.Execute() | NO |
| DW-B124-01/02 | No — OnGlobalBeClick / test assertion | NO |
| DW-B107 | No — MoveStopToBreakEven Step A loop | NO |
| B107-DEFER-01/02 | No — F5 gate and Combo C live test | NO |
| DW-B42-01/02/03 | No — IsPttQxTarget range / T3 assertion | NO |
| DW-PTT-BE-FIX-01/02/03 | No — lazy re-resolve / SIM gate paths / test build errors | NO |
| DW-B89-DEFERRED-01..06 | No — SIM gate paths for DW-B89 fix | NO |

**Verdict**: **PASS** — zero carry-forward items intersect LaneA scope.

---

## System Coherence Check

**CopyEngine + TradeCopierPanel + TradeCopierWindow form a complete coherent system?**

LaneA adds exactly one concern to `CopyEngine`: clear `_lastLeaderDirection` when the leader goes flat,
via `TryFirePositionState`. This does not alter the public surface of `CopyEngine`, does not add or
remove any event, property, or method visible to `TradeCopierPanel` or `TradeCopierWindow`. The fix is
fully internal to `CopyEngine`'s position-state tracking subsystem.

- `TradeCopierPanel` wires `PositionStateChanged` — unchanged. ✓
- `TradeCopierWindow` has no dependency on `_lastLeaderDirection` or reversal guard logic — unchanged. ✓
- No wiring gaps introduced. No spec requirement missed.

**System coherence: CONFIRMED.**

---

## Spec Requirements Satisfied End-to-End

| Requirement | Addressed | Evidence |
|-------------|-----------|----------|
| DW-B135: clear reversal guard when leader goes flat | YES | `if (!hasPos)` block in TryFirePositionState, `_lastLeaderDirection.TryRemove` at L2401 |
| DW-B128: preserve race-window guard | YES | `hasPos=True` during race window prevents TryRemove; V-05 confirmed; test 2 PASS |
| No lock() in new code (JS-021) | YES | SCAN-01: 0 executable hits |
| CYC <= 8 after fix | YES | CYC=6, PART 5 independent count confirmed |
| xUnit-only tests | YES | All 3 new tests are `[Fact]`, no NUnit/MSTest |
| LaneB non-regression | YES | 3 LaneB tests pass; LaneB methods verified intact |

---

## Section K — Deferred Work Register

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B134-OCO | OCO Orphan Risk After ATM STP Cancel+Resubmit — `SyncAtmFollowerBracket` cancels follower ATM bracket; resubmitted stop is outside original OCO pair; OCO target may be auto-cancelled or orphaned stop may persist after target fill. Requires Director SIM gate to determine ATM OCO behavior on `acc.Cancel`. | P2 | B130 | OPEN |
| DW-B129-01 | Director SIM Gate: Quick2t + QAll2t Live Validation — runtime behavioral assertions for `_instr2tBtn` and `_instrQAll2tBtn` cannot be exercised by unit tests. Verify qty split, T2 skip at qty=1, and QAll2t coverage. | P1 | B130 | OPEN |
| DW-B133 | 2-Target Forced Count for PttGlobalQuickExit ALL Path — Option A (forced 2-target count) deferred due to CYC budget; current Option B uses `SnapshotTargetOrders()` count. | P2 | B133 | OPEN |
| DW-B124-01 | Behavioral Change: Second Click No Longer Disarms BE-ALL — disarm-on-second-click UX removed; if Director requires restore, new block spec needed. | P2 | B125+ | OPEN |
| DW-B124-02 | Test 2 Assertion Weakness: callCount == 0 Instead of 1 — `FirstPressArmsWhenNotYetArmed` asserts empty-list delegate, not invocation count. | P2 | B125+ | OPEN |
| DW-B107 | MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers — correctness violation, functionally benign in observed test. Requires `SnapshotBeTargets` extraction. | P2 | B108+ | OPEN |
| B107-DEFER-01 | F5 NinjaTrader 8 Compilation Gate — Director presses F5 after sync pass. | P0 | Director immediate | OPEN |
| B107-DEFER-02 | Combo C Live Re-Test — BE-ALL then QX-ALL sequence SIM validation. | P1 | Director SIM | OPEN |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 — add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))`. | Low | B43+ | OPEN |
| DW-B42-02 | Live NT8 F5 verification required for both QX-BE bug directions. | High | Next live session | OPEN |
| DW-B42-03 | IsPttQxTarget range extension for future T4/T5 slots. | Low | Block adding T4/T5 | OPEN |
| DW-PTT-BE-FIX-01 | DW-B85 Option A: Lazy re-resolve for null followers in AllAccounts(). | Medium | Next PTT productionisation | OPEN |
| DW-PTT-BE-FIX-02 | SIM gate: Path B 3-cycle runtime verification (QX-ALL then BE-ALL). | High | DW-B89 SIM gate | OPEN |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors — CopyEngineTests.cs 83 errors + CS0433 Globals ambiguity. | High | Dedicated remediation block | OPEN |
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate for DW-B89 changes. | P0 | Director immediate | OPEN |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal — 3 cycles, zero [BE-ERR], no naked positions. | High | After DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case (short position). | High | After DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles). | High | After DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle. | High | After DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 in spec HTML after SIM gate. | Medium | After all DW-B89 SIM paths | OPEN |

**New LaneA deferred items**: None. LaneA implementation is complete as specified. No new deferred
items were identified during planning, ticket execution, or independent verification.

**Total open items**: 20 (19 carry-forward from LaneB + 0 new from LaneA).

---

## Spec Update Note

The following spec HTML updates are required after this FINAL_PASS (to be performed by the
orchestrator/Director):

| Update | Action |
|--------|--------|
| DW-B135 | Mark CLOSED — B129 LaneA PIPELINE_COMPLETE |
| DW-B134 | Mark CLOSED — B129 LaneB PIPELINE_COMPLETE (already VERIFY_PASS) |
| DW-B134-OCO | Add as OPEN deferred → B130 (carry-forward from LaneB-06-deferred-backlog.md) |
| DW-B136 Gap A | Mark RESOLVED — root cause was DW-B135, now fixed by B129 LaneA |
| B129 | Mark fully PIPELINE_COMPLETE (LaneA + LaneB both complete) |

---

## Summary Table

| FK Check | Result | Notes |
|----------|--------|-------|
| FK-1 Build Clean | **PASS** | 0 errors, 0 warnings; 11/11 tests; 6 B129Tests green |
| FK-2 Layer 2/3 Agreement | **PASS** | EXACT MATCH on all 7 scans |
| FK-3 Fix Correctness | **PASS** | if(!hasPos) present; predicate correct; TryRemove correct; no lock() |
| FK-4 DW-B128 Preservation | **PASS** | hasPos=True during race window; TryRemove not called; guard fires |
| FK-5 LaneB Untouched | **PASS** | IsAtmSTPOrder, SyncFollowerBracket, SyncAtmFollowerBracket, IsReversalToFlatFollower all intact |
| FK-6 All 6 Tests Green | **PASS** | All 6 B129Tests class tests named and confirmed |
| FK-7 P0 JS Rules | **PASS** | JS-021, JS-001, JS-002, JS-033 all clean; CYC=6 <= 8; ASCII-only |
| FK-8 Carry-Forward Unaffected | **PASS** | LaneA scope disjoint from all 19 carry-forward items |

**Total violations**: 0
**Section K**: PRESENT (20 deferred items — 19 carry-forward + 0 new)
**LaneA-06-deferred-backlog.md**: WRITTEN (required for FINAL_PASS)

---

## FINAL VERDICT

**FINAL_PASS**

All 8 final-review checks pass. Build 0 errors 0 warnings. 11/11 B129-filter tests green.
6/6 required B129Tests class tests confirmed by name. Layer 2 and Layer 3 in exact agreement
on all 7 scans. Fix correctness confirmed by direct source read (L2361-2414). DW-B128
preservation confirmed by logic analysis and passing test. LaneB methods verified intact.
All 19 carry-forward items unaffected by LaneA scope. No new deferred items.
Section K written. LaneA-06-deferred-backlog.md written.

**B129 LaneA: DW-B135 PIPELINE_COMPLETE.**
