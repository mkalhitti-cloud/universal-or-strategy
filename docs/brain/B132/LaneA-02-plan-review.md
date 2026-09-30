# B132 LaneA -- Plan Review

**Status**: REVIEW_PASS
**Epic**: B132 LaneA
**Phase**: 2 -- Plan Review (Pass 2)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-31
**Review Cycle**: 2 of 2 (V-01 corrected)
**Input**: `docs/brain/B132/LaneA-02-architecture-plan.md` (REVIEW_PENDING, corrected)

---

## STEP 0 -- Rules Catalog Gate

**File**: `docs/standards/jane-street/RULES_CATALOG.md`
**Encoding**: UTF-8 clean. Lines 1-30 verified (Version 1.0, Active Standard, V12 DNA Mandatory).

**Gate Result**: PASS -- catalog is readable. No P0 violations found in the plan.

---

## Review Checklist (R-01 through R-12)

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| R-01 | Root cause clear (OCO-cancel-all, Account.Change() no-op, Cancel()+CreateOrder()+Submit() pattern) | **PASS** | Section A: all 3 NT8 facts cited with source; B131 SIM session 2 and B129 SIM gate cited |
| R-02 | Phase C fix: CreateOrder(StopMarket)+Submit, oco="", price from "Stop{N}" | **PASS** | Section B C3: `CreateOrder(... OrderType.StopMarket ... "PTT-STP-Drag")`, `oco=""`, `FindLeaderStopPrice` fetches Working "Stop{N}" price |
| R-03 | All 4 methods: C# signatures + CYC <= 8 | **PASS** | Section C: SyncAtmFollowerTarget=8, DeriveLeaderBracketIndex=3, FindLeaderStopPrice=5, CreateFollowerReplacementStop=4; SyncFollowerBracket=7 (call site only) |
| R-04 | Block A-Prime UNCHANGED | **PASS** | Section D table: "Block A-Prime (L2270-2288) -- zero modification; exactly as built" |
| R-05 | 5+ xUnit [Fact] tests with edge cases | **PASS** | Section E: 5 named [Fact] tests; edge cases include null leaderOrder, unparseable suffix, missing stop, null account, zero index |
| R-06 | No spurious DW items | **PASS** | Section F: "DW-F: None." Rationale given for all confirmed API facts |
| R-07 | SCAN-01..07 with exact commands | **PASS** | Section G table: 7 scans, exact grep/python/dotnet commands, required result per scan |
| R-08 | Rules Catalog Gate with JS-XXX citations | **PASS** | Section H: JS-021, JS-001, JS-002, JS-033 cited; all PASS; Gate Result: PASS |
| R-09 | Phase C adds 0 branches to SyncAtmFollowerTarget | **PASS** | Section C: "Phase C adds: 0 branches (3 void helper calls with no `if` in main body). CYC = 8." |
| R-10 | Backward compatibility: call site update addressed | **PASS** | Section B + C: one call site at SyncFollowerBracket L2158; leaderOrder in scope; null gracefully propagates to skip; B129/B130/B131 tests unaffected |
| R-11 | No lock(), async void, return null, throw new Exception | **PASS** | Section G SCAN notes + Section H: no lock (NT8 thread-safe collections); no async; returns int/double/void (no null); try/catch with no rethrow (JS-001) |
| R-12 (V-01) | Phase C pseudocode uses leaderOrder?.Account (null-safe) | **PASS (V-01 resolved)** | Section B flow L70: `stopPrice = FindLeaderStopPrice(leaderOrder?.Account, n)` -- null-safe; FindLeaderStopPrice guard `if (leaderAccount == null) return 0.0` handles null propagation |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Follower stop cancelled by OCO group effect identified | YES | Section A |
| Replacement stop placed after every Block B target drag | YES | Section B Phase C |
| Replacement stop uses correct OrderType (StopMarket) | YES | Section B C3 |
| Replacement stop uses correct OrderAction (fo.OrderAction) | YES | Section B C3 rationale |
| Replacement stop NOT added to OCO group (oco="") | YES | Section B C3 + no-OCO note |
| Replacement stop name follows PTT- prefix convention | YES | Section B C3: "PTT-STP-Drag" |
| Replacement stop price = leader's Working Stop{N} price | YES | Section B C2 + FindLeaderStopPrice |
| Graceful skip if stop price not found | YES | Section B C3: guard `stopPrice <= 0.0` |
| SyncAtmFollowerTarget CYC unchanged (stays <= 8) | YES | Section C: CYC=8, 0 new branches |
| Block A-Prime (DW-B139/B131 LaneB) unchanged | YES | Section D |
| 4th param leaderOrder nullable, backward compatible | YES | Section B signature change |
| One call site updated (SyncFollowerBracket L2158) | YES | Section B + Section C |
| 5 xUnit [Fact] tests | YES | Section E |
| SCAN-01..07 checklist | YES | Section G |
| No spurious DW items | YES | Section F |

All 15 spec requirements are addressed in the plan.

---

## V-01 Violation Resolution Confirmed

**Original V-01**: Phase C pseudocode wrote `FindLeaderStopPrice(leaderOrder.Account, n)` -- 
direct (non-null-safe) dereference of `leaderOrder.Account` before confirming `leaderOrder != null`.
This was a JS-002 / nullable dereference risk.

**Corrected plan (Cycle 2)**:
- Flow pseudocode (Section B, line 70): `stopPrice = FindLeaderStopPrice(leaderOrder?.Account, n)` -- uses null-safe `?.` operator.
- `FindLeaderStopPrice` signature: `Account? leaderAccount` -- explicitly nullable.
- `FindLeaderStopPrice` first branch: `if (leaderAccount == null) return 0.0` -- guard confirmed.
- End-to-end null propagation: null leaderOrder -> null Account arg -> 0.0 return -> CreateFollowerReplacementStop guard skips.

**V-01 is fully resolved.**

---

## Jane Street DNA Compliance Summary

| Rule | Category | Verdict |
|------|----------|---------|
| JS-001 (P0) | No throw in hot paths | PASS -- try/catch with log+return, no rethrow |
| JS-002 (P0) | No return null | PASS -- int/double/void returns; null-safe propagation |
| JS-021 (P0) | No lock() | PASS -- no lock anywhere; NT8 thread-safe collections used |
| JS-033 (P0) | No async void | PASS -- all new methods synchronous |
| CYC <= 8 (P1) | Complexity | PASS -- SyncAtmFollowerTarget=8, helpers=3/5/4 |
| ASCII-only | NT8 constraint | PASS -- all new string literals ASCII |
| PTT- prefix | NT8 constraint | PASS -- "PTT-STP-Drag" |
| DateTime.UtcNow | NT8 constraint | PASS -- no date/time in new code |
| Minimal change | Engineering discipline | PASS -- 1 sig change + 1 call site + ~50 lines |

No violations found.

---

## Gate Decision

**REVIEW_PASS**

Architecture plan is approved. Proceed to Phase 3 ticket generation.

All 12 review items pass. V-01 (null-safe leaderOrder?.Account dereference) is confirmed resolved
in the corrected plan. All NT8 API facts cited with source. All Jane Street P0 rules satisfied.
CYC budget preserved at 8. Block A-Prime unchanged. Call site backward compatible. 5 xUnit tests
specified with edge cases. SCAN-01..07 checklist complete.

---

*Epic: B132 LaneA | Phase: 2 -- Plan Review (Pass 2) | Gate: REVIEW_PASS*
