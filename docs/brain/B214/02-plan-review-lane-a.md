# B214 Plan Review -- Lane A
## Phase 2 Review: 02-architecture-plan-lane-a.md

**Epic**: B214 -- B211 + B212 + B213 Repair Batch Validation
**Reviewer**: ptt-plan-reviewer
**Review Phase**: 2 (Plan Review)
**Date**: 2026
**Plan Under Review**: `docs/brain/B214/02-architecture-plan-lane-a.md`

---

## REVIEW RESULT: REVIEW_PASS

No violations found. All 8 mandatory checks passed. Zero Jane Street P0/P1 violations identified in the plan.

---

## Mandatory Check Matrix

| # | Check | Status | Evidence |
|---|-------|--------|----------|
| 1 | LANE-SPLIT GATE compliance (Q1-Q4 + result line) | PASS | Plan Section 1, lines 16-23: Q1-Q4 answered; "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" present verbatim |
| 2 | Validation-only mandate (Ph4a does NOT modify .cs files) | PASS | Plan line 8 header, Section 4 opening (line 268), Section 5 (line 355-364): three independent explicit statements |
| 3 | CYC targets documented for all named methods | PASS | Plan Section 4 Step 4 table (lines 295-311) + Section 8 pass/fail criteria (lines 417-419, 422) |
| 4 | All 10 hotfixes listed with file+method+validation approach | PASS | HF-01 through HF-10 documented with file, method(s), source line in NO-PIPELINE-REPAIRS.md, SIM status, and per-hotfix validation criteria |
| 5 | Zero-new-code mandate enforced | PASS | Section 5 explicitly states mandate with Permitted/Forbidden action lists and cites authority (NO-PIPELINE-REPAIRS.md header SRC CODE BAN) |
| 6 | JS rules coverage (JS-021, JS-033, CYC<=8) | PASS | Section 4 Step 5 (scan commands) and Section 6 (rules table with severity) cover JS-021, JS-033, JS-001, JS-002, CYC<=8, ASCII-only, PTT- prefix, DateTime.Now |
| 7 | Deferred backlog from B212 carried forward | PASS | Section 7 carries 8 items (DW-B209-01/02/04, DW-B211-01/02, DW-B212-01/02/03) with priority, status, and explicit notes that B214 does NOT close them |
| 8 | Plan vs spec cross-reference (NO-PIPELINE-REPAIRS.md) | PASS | Each HF references exact source section line in NO-PIPELINE-REPAIRS.md; all 10 hotfix IDs map to confirmed sections in that document |

---

## CYC Target Verification

| Method | File | Required CYC | Plan Location | Check |
|--------|------|-------------|---------------|-------|
| `IsLaneEEligible` | CopyEngine.cs | **= 8** | Section 4 Step 4 table, line 296; Section 8 line 417 | PASS |
| `SubmitDrainedEntry` | CopyEngine.cs | **= 8** | Section 4 Step 4 table, line 297; Section 8 line 418 | PASS |
| `DrainThenDispatch` | CopyEngine.cs | **<= 8** | Section 4 Step 4 table, line 298; Section 8 line 419 | PASS |
| `TryResolveLeaderAccount` | TradeCopierPanel.cs | **= 6** | Section 4 Step 4 table, line 301; Section 8 line 422; HF-10 narrative | PASS |
| `DispatchCopy` | CopyEngine.cs | **<= 8** | Section 4 Step 4 table, line 299; Section 8 line 420 | PASS |
| `IsSuppressedReentry` | CopyEngine.cs | **<= 8** | Section 4 Step 4 table, line 300; Section 8 line 421 | PASS |
| `Execute` (PttMoveTargetOne) | PttMoveTargetOne.cs | **<= 8** | Section 4 Step 4 table, line 302 | PASS |
| `CollectWorkingTargets` | PttMoveTargetOne.cs | **<= 8** | Section 4 Step 4 table, line 303 | PASS |
| `SubmitTargetPair` | PttMoveTargetOne.cs | **<= 8** | Section 4 Step 4 table, line 304; HF-09 | PASS |
| `IsLaneEFlattenLatched` | CopyEngine.cs | **<= 2** | Section 4 Step 4 table, line 305 | PASS |
| `IsPositionOrInstrumentNull` | CopyEngine.cs | **<= 2** | Section 4 Step 4 table, line 306 | PASS |
| `IsFollowerSlateNotClosed` | CopyEngine.cs | **<= 2** | Section 4 Step 4 table, line 307 | PASS |
| `IsLaneEBlockedByFlattenOrLeader` | CopyEngine.cs | **<= 2** | Section 4 Step 4 table, line 308 | PASS |
| `OnAskAllClick` | TradeCopierPanel.cs | **<= 8** | Section 4 Step 4 table, line 309 | PASS |
| `OnBidAllClick` | TradeCopierPanel.cs | **<= 8** | Section 4 Step 4 table, line 310 | PASS |

**Note on `IsLaneEEligible` CYC breakdown**: Plan provides an explicit branch-by-branch count at HF-07:
`(0)latch(1)+(1)mode(1)+(2)null(1)+(3)qty(1)+(4)slate(1)+(5)atm(1)+(5b)beCount(1)+(6+7)flattenLeader(1) = 8`
This satisfies the JS-066 (CYC<=8) ceiling with zero margin. Ph4a must confirm this count from source; any branch deviation = VERIFY_FAIL.

---

## Jane Street Rules Coverage Matrix

| Rule ID | Category | Severity | Addressed in Plan | Notes |
|---------|----------|----------|-------------------|-------|
| JS-021 | Concurrency | P0 | YES -- Section 4 Step 5 scan command; Section 6 table | `lock()` ban explicitly scanned; ConcurrentDictionary + Interlocked cited as correct patterns for all hotfixes |
| JS-033 | Concurrency | P0 | YES -- Section 4 Step 5 scan command; Section 6 table | `async void` ban scan included; pre-existing event handler `async void`s noted as non-violations |
| JS-001 | Type Safety | P0 | YES -- Section 4 Step 5 scan; Section 6 table | `throw new` banned in hot-path methods explicitly; SubmitTargetPair try/catch is NT8 scaffold (pre-existing), not a new throw in hot path |
| JS-002 | Type Safety | P0 | YES -- Section 6 table with explicit rationale | `TryResolveLeaderAccount` returning null correctly classified as pre-existing UI sentinel pattern, not a new violation introduced by HF-10 |
| JS-066 | Code Review | P1 | YES -- Section 4 Step 4 full table; Section 8 pass/fail criteria | CYC<=8 ceiling enforced for all 15 named methods with exact or bounded targets |
| ASCII-only | Code Review | P1 | YES -- Section 6 table; per-hotfix "JS-DNA declared" blocks | All 10 hotfixes declared ASCII-only; ph4a grep scan for non-ASCII in modified sections called out |
| SCAN-05 (PTT- prefix) | NT8 | P1 | YES -- Section 6 table | SubmitTargetPair CreateOrder names `PTT-MA-Ask/Bid/Stop-NNNNN` confirmed in HF-09 |
| SCAN-06 (DateTime.Now ban) | NT8 | P1 | YES -- Section 4 Step 5 scan command; Section 6 table | grep scan for `DateTime\.Now` included |
| NT8 API scope (AtmStrategyCreate) | NT8 | P0 | YES -- Section 6 NT8 API scope note | Explicitly states `AtmStrategyCreate` / `AtmStrategyChangeStopTarget` are NOT used; AddOnBase pattern (`Account.Cancel()` + `Account.CreateOrder()` + `Submit()`) confirmed |

---

## Hotfix Spec Coverage Matrix

All 10 hotfixes cross-referenced to `docs/brain/NO-PIPELINE-REPAIRS.md`:

| Hotfix ID | Plan Section | NO-PIPELINE-REPAIRS.md Source | Spec Requirement Addressed | Status |
|-----------|-------------|-------------------------------|---------------------------|--------|
| HF-01 (HOTFIX-B211-ASK-01) | Section 3, lines 53-68 | L3232 | `TryFindWorkingTarget` accepts PTT- AND Target prefix; single-acct Ask button | PASS |
| HF-02 (HOTFIX-B211-ASKALL-01) | Section 3, lines 71-85 | L3292 | `OnAskAllClick` direct loop (no AllAccounts/FindRule), calls PttMoveTargetOne.Execute | PASS |
| HF-03 (HOTFIX-B211-BIDALL-01) | Section 3, lines 88-103 | L3341 | `OnBidAllClick` mirrors OnAskAllClick with useAsk: false | PASS |
| HF-04 (HOTFIX-B211-MOVETARGET-01 / B212-MAFIX2) | Section 3, lines 106-124 | L3262 | `CollectWorkingTargets` returns Order[] two-pass; `Execute` iterates ALL targets | PASS |
| HF-05 (HOTFIX-B212-REENTRY-V3) | Section 3, lines 126-144 | L3728 | Gates B212-REENTRY-V3 and B212-REENTRY-FLATTEN in DispatchCopy before Gate 3 | PASS |
| HF-06 (HOTFIX-B212-REENTRY-BETARGET V4) | Section 3, lines 146-165 | L3805, L3877, L3937, L3984 | `IsSuppressedReentry` V4 guard; TryRemove placement after Gate 5; TryRecordBeTargetFill + TryEvictFollowerBeSlot | PASS |
| HF-07 (HOTFIX-B212-REENTRY-FOLLOWER-V1+V2) | Section 3, lines 167-195 | L4066, L4178, L4192 | V1 HasFilledBeTargetFast gates in SubmitDrainedEntry + DrainThenDispatch; V2 IsLaneEEligible extraction (11->8 CYC) + gate (5b) | PASS |
| HF-08 (HOTFIX-B212-FLATTEN-LEADERSKIP-01) | Section 3, lines 198-214 | L3451 | `Flatten(Instrument)` RelayFlatten leader-skip WHERE clause; direct overload unchanged | PASS |
| HF-09 (HOTFIX-B212-MA-STOP-02) | Section 3, lines 216-238 | L3660 | `SubmitTargetPair` OCO ID generation; StopMarket+Limit pair; guard for snapshotStop==0 | PASS |
| HF-10 (HOTFIX-B213-MULTI-CHART-INSTRUMENT-01) | Section 3, lines 240-262 | L4240 | `TryResolveLeaderAccount` lazy re-wire path with 6-step combo resolution | PASS |

---

## Deferred Backlog Carry-Forward Verification

Section 7 of the plan carries forward 8 items from B212's deferred backlog:

| DW Item | Priority | Plan Status | Correct Carry-Forward? |
|---------|----------|-------------|----------------------|
| DW-B209-01 | P2 | OPEN -- carried to B215+ | YES |
| DW-B209-02 | P2 | OPEN -- cosmetic, carried to B215+ | YES |
| DW-B209-04 | P1 | OPEN -- must update spec before production push | YES |
| DW-B211-01 | P2 | OPEN -- V2 design decision required | YES |
| DW-B211-02 | P2 | OPEN -- depends on DW-B211-01 | YES |
| DW-B212-01 | P1 | OPEN -- Trim(Instrument) investigation | YES |
| DW-B212-02 | P2 | OPEN -- PttMoveTargetAll.cs future role | YES |
| DW-B212-03 | P1 | OPEN -- addressed by Ph4a Step 7 (DIFF GUARD) | YES |

**Note on DW-B212-03**: The plan correctly notes that Ph4a Step 7 (DIFF GUARD via `deploy-sync.ps1`) addresses this item's pass criteria for the B214 pipeline. If Step 7 passes, DW-B212-03 can be closed in the B214 final review. The plan does not prematurely close it -- correct behavior.

---

## Validation Approach Completeness

Ph4a validation steps (Section 4) cover all required checks:

| Step | Check | Covered? |
|------|-------|---------|
| Step 1 | ptt-sync 21/0 | YES |
| Step 2 | dotnet build (0 errors) | YES |
| Step 3 | dotnet test (0 failures) | YES |
| Step 4 | CYC audit -- all 15 named methods | YES |
| Step 5 | P0 violation scans (lock, async void, throw new, return null, DateTime.Now) | YES |
| Step 6 | Guardian script (verify_critical_symbols.ps1) with known DW-B209-01 false positive exception | YES |
| Step 7 | DIFF GUARD (deploy-sync.ps1, < 10,000 chars) | YES |

**All 7 validation steps present.** Zero-new-code mandate enforced in Section 5.

---

## NT8 API Validation

The plan explicitly declares in Section 6 (NT8 API scope note):
- `AtmStrategyCreate()` is StrategyBase-only and is NOT used in any hotfix ✅
- `AtmStrategyChangeStopTarget()` is StrategyBase-only and is NOT used in any hotfix ✅
- Correct AddOnBase pattern: `Account.Cancel()` + `Account.CreateOrder()` + `Submit()` ✅
- `Account.Change()` not used in any hotfix ✅

HF-09 NT8-049 compliance (arg positions for StopMarket vs Limit) is explicitly documented ✅

---

## Notes for Ph4a Engineer

The following items require particular attention during Ph4a execution (NOT violations of the plan -- operational notes):

1. **IsLaneEEligible CYC must equal exactly 8** (not <=8): The pass criterion in Section 8 is `= 8`, meaning CYC=7 would be a FAIL (regression from V2 extraction). Ph4a must verify the exact branch count.

2. **DispatchCopy CYC**: HF-05 states "gates added CYC+2 max, final CYC must be confirmed <= 8." HF-06 states "CYC delta=0 in DispatchCopy." Ph4a must count total branches in the gated DispatchCopy body. If CYC > 8, this is a VERIFY_FAIL citing JS-066.

3. **DW-B209-01 false positive**: If `verify_critical_symbols.ps1` fires ONLY for the known `TradeCopierPanel._instr2tBtn.Background` case, this is a PASS with note per plan Section 4 Step 6.

4. **DW-B212-03 resolution**: If `deploy-sync.ps1` DIFF GUARD passes in Step 7, DW-B212-03 should be updated to CLOSED in `docs/brain/B214/06-deferred-backlog.md` (to be written by ptt-final-reviewer in Phase 5).

5. **Pre-existing `return null` in TryResolveLeaderAccount**: Plan Section 6 correctly classifies this as pre-existing UI pattern. Ph4a should confirm the null returns existed before HF-10 and no new `return null` was added in the HF-10 lazy re-wire path.

---

## Final Verdict

```
REVIEW_PASS

Violations found: 0
P0 violations: 0
P1 violations: 0
Plan completeness: 10/10 hotfixes specified
CYC targets: 15/15 methods documented
Deferred items: 8/8 carried forward from B212
LANE-SPLIT GATE: SINGLE-PIPELINE declared with Q1-Q4
Zero-new-code mandate: Explicitly stated in plan header, Section 4, and Section 5
NT8 API scope: Correct AddOnBase pattern confirmed
```

Phase 3 (ticket generation) is unlocked. Proceed to `04-tickets.md`.
