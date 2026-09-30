# B130 LaneA Ticket Review

**Epic**: B130-LaneA
**Defect**: DW-B137 — IsAtmSTPOrder Wrong Name Format
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-09-01
**Tickets file**: docs/brain/B130/LaneA-04-tickets.md
**Plan file**: docs/brain/B130/LaneA-02-architecture-plan.md (REVIEW_PASS)

---

## Review Result: TICKET_REVIEW_PASS

---

## Checklist Results

| Check | Result | Notes |
|-------|--------|-------|
| CHECK-01 Traceability | PASS | DW-B137 cited in T1.1; all 5 changes (IsAtmSTPOrder, SyncFollowerBracket 3b, SyncAtmFollowerTarget, B130Tests.cs, csproj) specified; plan sections D.1/D.2/D.3/G/H/I all referenced |
| CHECK-02 JS Pre-Check lock ban (JS-021) | PASS | No lock() in any of the 3 new/modified method bodies; T1.3 JS rule table explicitly attests JS-021 compliance for all 3 methods |
| CHECK-03 CYC Pre-Check (JS-066) | PASS | IsAtmSTPOrder=1 (expression body, OR clauses are not McCabe nodes); SyncFollowerBracket=7 (5 existing decisions + 1 new branch 3b); SyncAtmFollowerTarget=4 (3 null guards, try/catch adds 0 McCabe); all ≤ 8 |
| CHECK-04 NT8 Constraints | PASS | OrderType.Limit confirmed; arg6=newPrice/arg7=0 correct for Limit; "PTT-TGT-Drag" satisfies NT8-014 PTT- prefix; Core.Globals.MaxDate present; (NinjaTrader.Cbi.CustomOrder)null present |
| CHECK-05 Completeness | PASS | BEFORE/AFTER for IsAtmSTPOrder present verbatim; exact 3b replacement block present; full SyncAtmFollowerTarget body present; full B130Tests.cs content present; B129Tests.cs stub pattern instruction present |
| CHECK-06 Test Coverage | PASS | Both [Fact] names correct; Test 1 asserts Stop1/Stop2/Stop3→true, Buy STP/Sell STP→true (backward compat), Entry/PTT-Copy→false; Test 2 asserts Target1/Target2/Target3→true, PTT-TGT-Drag/PTT-Copy→false |
| CHECK-07 7-Scan Checklist | PASS | All 7 scans present in T1.5 (SCAN-01 through SCAN-07); SCAN-05 uses python scripts/complexity_audit.py; SCAN-06 checks both PTT-TGT-Drag and PTT-STP-Drag; SCAN-07 uses build_readiness.ps1 |
| CHECK-08 Backward Compat | PASS | T1.7 lists all 6 B129Tests.cs tests (DW-B134 group + DW-B135 group); B129_DW134_STPSuffixDetectedByIsBracketLegStatic explicitly listed; EndsWith("STP") clause preservation explicitly explained |

---

## Violations Found

None.

---

## Detailed Check Notes

### CHECK-01 Traceability — PASS

T1.1 cites DW-B137 in two separate spec-requirement bullets (stop drag + target drag). All 3
CopyEngine.cs changes (Change 1: IsAtmSTPOrder ~L2028; Change 2: SyncFollowerBracket branch 3b
~L2067; Change 3: SyncAtmFollowerTarget after L2159) are mapped to plan sections D.1, D.2, D.3
respectively. Change 4 (csproj) and Change 5 (B130Tests.cs) are mapped to plan sections I and G.
No phantom work identified — all ticket items trace to plan or spec.

### CHECK-02 JS Pre-Check (JS-021) — PASS

IsAtmSTPOrder: expression body, structurally incapable of containing lock().
SyncFollowerBracket Change 2: the added block contains only two if-branches calling
SyncAtmFollowerTarget and returning — no lock().
SyncAtmFollowerTarget: two null guards and two independent try/catch blocks — no lock().
T1.3 explicitly records JS-021 as a constraint with "NO lock() anywhere" and lists all three
methods in scope. The engineer self-attestation anchor is in place.

### CHECK-03 CYC Pre-Check (JS-066) — PASS

IsAtmSTPOrder CYC=1:
  - Expression body with compound OR is 1 linear path.
  - Three OR clauses in a boolean expression are not independent decision nodes (McCabe).
  - Plan review section C verified and confirmed CYC=1.

SyncFollowerBracket CYC=7:
  - Existing 5 decisions (fo null, price delta, branch 3 isStop+ATM, branch 4 IsTrailingStop,
    branch 5 isStop inner) = base CYC 6.
  - Branch 3b (!isStop && IsAtmSTPOrder) adds 1 decision node = CYC 7.
  - Ticket claims CYC=7. Ticket CYC comment updated from 6 to 7. PASS (≤ 8).

SyncAtmFollowerTarget CYC=4:
  - 3 decision nodes: acc null (1), fo null (2), newTarget null in Block B (3).
  - try/catch blocks are NOT McCabe decision nodes (plan review section C confirmed).
  - CYC = 3+1 = 4. PASS (≤ 8).

Note: The ticket's CYC comment on SyncAtmFollowerTarget labels "(3) Block A try-body" as a
McCabe node. As the plan review noted, this is an imprecise label — the try/catch is not a
McCabe branch. The final numeric count CYC=4 is correct and the method is safely ≤ 8.
This imprecision is non-blocking (same finding as plan-reviewer E.minor observation).

### CHECK-04 NT8 Constraints — PASS

SyncAtmFollowerTarget CreateOrder call verified:
  - arg3: OrderType.Limit ✓
  - arg6 (limitPrice): newPrice ✓ — correct for Limit orders
  - arg7 (stopPrice): 0 ✓ — correct (stopPrice unused for Limit)
  - arg10 (order name): "PTT-TGT-Drag" ✓ — starts with "PTT-" per NT8-014
  - arg11: NinjaTrader.Core.Globals.MaxDate ✓ — per NT8-013
  - arg12: (NinjaTrader.Cbi.CustomOrder)null ✓ — per NT8-007
T1.8 NT8 API facts table provides engineer reference for all confirmed facts.

### CHECK-05 Completeness — PASS

All 5 required content items present:
1. BEFORE/AFTER verbatim code for IsAtmSTPOrder — present in T1.2 Change 1
2. Exact replacement block for SyncFollowerBracket branches (3) and (3b) — present in T1.2 Change 2
3. Full method body for SyncAtmFollowerTarget — present in T1.2 Change 3
4. Full B130Tests.cs content — present in T1.2 Change 5
5. Engineer instruction to read B129Tests.cs stub pattern — present as ENGINEER INSTRUCTION block
   after the test file content in T1.2 Change 5

### CHECK-06 Test Coverage — PASS

Test 1 (B130_DW137_Stop1NameRoutesToCancelResubmit):
  - Stop1/Stop2/Stop3 → true ✓
  - Buy STP/Sell STP → true (backward compat) ✓
  - Entry → false ✓
  - PTT-Copy → false ✓
  Backward compat assertion is explicitly labelled "// backward compat" in the test body ✓

Test 2 (B130_DW137_Target1NameRoutesCorrectly):
  - Target1/Target2/Target3 → true ✓
  - PTT-Copy → false ✓
  - PTT-TGT-Drag → false ✓ (labelled "// PTT order excluded")

Note: Test 2 does NOT include "Sell STP" backward compat assertion (it focuses on target
routing). This is correct — Test 1 owns all backward compat assertions for the STP suffix.
The split is intentional and complete. No gap.

### CHECK-07 7-Scan Checklist — PASS

All 7 scans present in T1.5 table with correct commands and expected results:
  SCAN-01: grep lock() on CopyEngine.cs — expected 0 new matches ✓
  SCAN-02: grep async void on CopyEngine.cs — expected 0 ✓
  SCAN-03: grep DateTime.Now on CopyEngine.cs — expected 0 ✓
  SCAN-04: grep non-ASCII on CopyEngine.cs — expected 0 ✓
  SCAN-05: python scripts/complexity_audit.py — all modified methods ≤ 8 ✓
  SCAN-06: grep PTT-TGT-Drag|PTT-STP-Drag — expected 2 matches ✓
  SCAN-07: powershell build_readiness.ps1 — 0 errors ✓

SCAN-06 correctly checks for BOTH "PTT-TGT-Drag" (SyncAtmFollowerTarget new method) and
"PTT-STP-Drag" (SyncAtmFollowerBracket existing method) — confirming both methods carry the
PTT- prefix on their order names.

Note: Plan section H uses PowerShell Select-String commands; ticket T1.5 uses grep commands.
Both are functionally equivalent for the scan purpose. The engineer may use either. Not a
violation — the expected result is stated correctly in both versions.

### CHECK-08 Backward Compatibility — PASS

T1.7 lists all 6 B129Tests.cs tests:
  DW-B134 group (3):
    - B129_DW134_STPSuffixDetectedByIsBracketLegStatic ✓
    - B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket ✓
    - B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel ✓
  DW-B135 group (3):
    - B129_DW135_GuardClearedAfterLeaderFlat ✓
    - B129_DW135_DW128ProtectionPreservedDuringRaceWindow ✓
    - B129_DW135_FirstEntryAfterRestartNotBlocked ✓

The ticket correctly explains WHY existing tests still pass: "The predicate extension adds new
OR clauses only — existing true/false results for 'Buy STP' / 'Sell STP' are PRESERVED."
This satisfies the plan-reviewer's non-blocking observation (plan review section E) by including
the full DW-B135 group in T1.7 (the plan had only 3 of 6 tests; the ticket corrects this).

---

## Reviewer Recommendation

**TICKET_REVIEW_PASS** — all 8 checks passed. Proceed to Phase 4 engineer.

Summary:
- T1 is the sole ticket. Single well-scoped change: 3 CopyEngine.cs edits + 1 new test file + 1 csproj line.
- DW-B137 root cause fully addressed (IsAtmSTPOrder predicate extended).
- Both stop drag (Stop1/Stop2/Stop3) and target drag (Target1/Target2/Target3) paths handled.
- Backward compat ("Buy STP"/"Sell STP") explicitly preserved and tested.
- No JS rule violations in any described method.
- CYC counts independently verified: 1, 7, 4 — all ≤ 8.
- NT8 API usage correct for Limit order cancel+resubmit.
- All 7 scans present in ticket as engineer contract.
- All 6 B129Tests.cs tests listed as must-still-pass.
- Engineer stub instruction present (B129Tests.cs pattern reference).
- File routing: all paths point to src/PropTraderTools/ (Wave workspace). No Director workspace .cs paths.
