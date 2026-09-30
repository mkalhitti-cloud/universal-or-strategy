# B129 LaneB Plan Review — DW-B134

**Block**: B129 LaneB
**Plan file**: `docs/brain/B129/LaneB-02-architecture-plan.md`
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-08-21
**Verdict**: REVIEW_PASS

---

## RC Checklist

| ID | Check | Result | Notes |
|----|-------|--------|-------|
| RC-01 | OQ-03 SAFE/UNSAFE verdict + code-line citation | **PASS** | Section C: SAFE verdict. Gate 2 `FindMatchingRule` returns null at L1349-1350 (follower account != master account). Code lines cited: L1603-1614 (FindMatchingRule), L1346-1350 (Gate 2 null return). |
| RC-02 | If UNSAFE: DW stop present | **PASS (trivial)** | Plan verdict is SAFE; this check passes trivially. |
| RC-03 | IsBracketLegStatic — STP EndsWith clause + OrdinalIgnoreCase | **PASS** | Section D.1 specifies `order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)`. OrdinalIgnoreCase explicit. |
| RC-04 | IsTrailingStop guard — ATM STP branch placed BEFORE it | **PASS** | Section B Layer 3 identifies the regression risk. Section D.2 explicitly places the new `if (isStop && IsAtmSTPOrder(fo))` branch as (3), shifting `IsTrailingStop` to (4). Fix ordering confirmed correct. |
| RC-05 | Layer 2 fix — cancel then CreateOrder+Submit per NT8 API | **PASS** | `SyncAtmFollowerBracket` (Section D.4): `acc.Cancel(new Order[] { fo })` then `acc.CreateOrder(12 args) + acc.Submit(new Order[] { newStop })`. Argument count (12) matches NT8_FULL_REFERENCE.md L2106 confirmed signature. Submit syntax matches L2154. |
| RC-06 | CYC ≤ 8 on all modified/new methods, numbers stated | **PASS** | Section E table: IsBracketLegStatic 3→4, IsAtmSTPOrder 1, SyncFollowerBracket 5→6, SyncAtmFollowerBracket 3. All ≤ 8. Numbers stated in Section E and inline in Section D method-header comments. |
| RC-07 | PTT- prefix on new bracket order name (NT8-014) | **PASS** | New order name = `"PTT-STP-Drag"` (Section D.4, Section F table). Compliant with NT8-014. |
| RC-08 | No lock() in new/modified code (JS-021) | **PASS** | `IsAtmSTPOrder` comment: "JS-021: no lock". `SyncAtmFollowerBracket` comment: "JS-021: no lock". Neither method contains lock, Monitor, Mutex, or SemaphoreSlim. |
| RC-09 | No return null in new methods (JS-002) | **PASS** | `IsAtmSTPOrder` returns bool. `SyncAtmFollowerBracket` is void. `SyncFollowerBracket` is void. No new return-null paths introduced. |
| RC-10 | No throw in hot path (JS-001) | **PASS** | Both new methods with NT8 calls (`SyncAtmFollowerBracket`, `SyncFollowerBracket`) wrap in try/catch. Exceptions logged to StatusUpdate, not rethrown. Inline comments cite JS-001 compliance. |
| RC-11 | All 3 xUnit test stubs present with assertion specs | **PASS** | Section G contains all three stubs with named assertions: Test 1 (STP suffix detection), Test 2 (cancel+resubmit sequence), Test 3 (OQ-03 gate). Each includes numbered assertion specs. |
| RC-12 | Test 3 (OQ-03) designated as gate that must pass before Ph4a | **WARNING** | Plan does not explicitly state Test 3 must pass before Phase 4a starts. The test stub is present and the OQ-03 analysis is thorough, but no explicit gate ordering statement appears. Ticket-reviewer should add this gate requirement to the ticket. Not blocking. |
| RC-13 | Files touched list complete (CopyEngine.cs + Tests/B129Tests.cs) | **PASS** | Section I: `CopyEngine.cs` (4 changes), `Tests/B129Tests.cs` (new, 3 facts), `PropTraderTools.csproj` (compile include). Complete. |
| RC-14 | 7-scan checklist present | **PASS** | Section H: SCAN-01 through SCAN-07. All 7 scans present with commands and expected results. |
| RC-15 | No scope creep — IsBracketLeg (instance) untouched, no Target STP | **PASS** | Section B Layer 1 explicitly states `IsBracketLeg` (L3550 instance version) does NOT need STP. Section D touches only `IsBracketLegStatic`. No Target STP handling proposed. Section J defers OCO orphan risk to `DW-B134-OCO`. |

---

## Violations

**BLOCKING violations**: None.

**WARNING (non-blocking)**:

| ID | Level | Description |
|----|-------|-------------|
| RC-12 | WARNING | Test 3 (`B129_DW134_OQ03_*`) is identified as an OQ-03 gate test in the defect brief but the plan does not explicitly require it to pass before Phase 4a (ticket execution) begins. Ticket-reviewer must add this gate ordering to the ticket. |

---

## NT8 API Verification

| API Call | Plan Usage | Reference | Status |
|----------|-----------|-----------|--------|
| `acc.Cancel(Order[])` | `SyncAtmFollowerBracket` L291 | NT8_FULL_REFERENCE.md L329 | VALID |
| `acc.CreateOrder(12 args)` | `SyncAtmFollowerBracket` L292-305 | NT8_FULL_REFERENCE.md L2106 | VALID — 12-arg signature confirmed |
| `acc.Submit(Order[])` | `SyncAtmFollowerBracket` L306 | NT8_FULL_REFERENCE.md L2154 | VALID |
| `Core.Globals.MaxDate` | GTD arg in CreateOrder | NT8_FULL_REFERENCE.md L2120 | VALID — documented for non-Gtd orders |
| `(CustomOrder)null` | last arg in CreateOrder | NT8_FULL_REFERENCE.md L2121 | VALID |

---

## Spec Coverage Matrix

| Requirement (DW-B134) | Addressed By | Plan Section |
|-----------------------|-------------|--------------|
| ATM bracket stops ("Buy STP"/"Sell STP") not detected by `IsBracketLegStatic` | `EndsWith("STP", OrdinalIgnoreCase)` clause added | D.1 |
| `IsWorkingBracket` returns false for ATM STP (drag gate never fires) | Fixed via `IsBracketLegStatic` STP clause | D.1 + B (cascade analysis) |
| `acc.Change()` silently ignored for ATM-owned brackets | cancel+resubmit pattern in `SyncAtmFollowerBracket` | D.4 |
| `IsTrailingStop` guard fires before ATM STP path (Layer 3 regression) | New branch inserted BEFORE `IsTrailingStop` guard | D.2 + B Layer 3 |
| OQ-03: cancel+resubmit must not cascade to `TryCancelFollowerEntries` | Gate 2 `FindMatchingRule` null-return: SAFE | C |
| PTT- prefix on new order name | `"PTT-STP-Drag"` | D.4, F |
| Test: B129_DW134_STPSuffixDetectedByIsBracketLegStatic | Test stub with 4 assertion cases | G Test 1 |
| Test: B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket | Test stub with 6 assertion specs | G Test 2 |
| Test: B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel | Test stub with 3+1 assertion specs | G Test 3 |

---

## Summary

All 15 RC checks evaluated. Zero blocking violations. One non-blocking WARNING (RC-12: ticket-reviewer to add OQ-03 gate ordering to ticket).

The plan correctly addresses all three layers of the defect, demonstrates SAFE OQ-03 analysis with code-line citation, places the new ATM STP branch before the `IsTrailingStop` guard to prevent the Layer 3 regression, uses the confirmed cancel+resubmit NT8 pattern, and maintains CYC ≤ 8 across all modified and new methods.

**Plan is ready for Phase 3 (ticket generation).**

---

## VERDICT: REVIEW_PASS
