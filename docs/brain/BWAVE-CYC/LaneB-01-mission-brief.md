# BWAVE-CYC Lane-B Mission Brief

**Date**: 2025-01-09
**Build Tag**: PTT-COPIER BWAVE-CYC Lane-B | 2025-01-09
**Status**: STAGE 1 COMPLETE

---

## Prerequisite Gate

- Lane A: LANE_A_FINAL_PASS confirmed at commit `68a1c1c4`
- Lane C: Complete

---

## Lane B Scope

Lane B owns the dispatch/entry/BE cluster of `src/PropTraderTools/CopyEngine.cs`.

**7 Tickets, Sequential Execution:**

| Ticket | Method(s) | CCN Before | Priority |
|--------|-----------|-----------|----------|
| TB-T1 | OnPendingBeAccountUpdate | 32 | HIGHEST (regression from Lane A) |
| TB-T2 | OnOrderUpdate | 23 | P0 (DW-B143-POSSTATE-CYC8) |
| TB-T3 | OnTrailBeAccountUpdate + SubmitBeStop | 9 + 10 | — |
| TB-T4 | DispatchCopy | 13 | — |
| TB-T5 | TryFireFollowerBeRetry + TryEvictFollowerBeSlot | 15 + 13 | — |
| TB-T6 | TryHandleEntryDrag + IsExitSignalName + SyncAtmFollowerBracket + CancelPttDragOrphansForAccount | 11 + 10 + 11 + 10 | — |
| TB-T7 | DtoToRule + GetRefPrice | 11 + 10 | — |

---

## Lane B Start Score (CodeScene)

**cs check src/PropTraderTools/CopyEngine.cs**

```
Code health score: 1.41
```

Captured at: commit `68a1c1c4` (post-Lane-A, pre-Lane-B)

All subsequent tickets must NOT decrease this score.

---

## Complexity Gate

- **PASS/FAIL GATE**: lizard CCN <= 8 on every modified method (ONLY hard gate)
- **TREND CHECK**: CodeScene cs delta score must NOT decrease vs pre-ticket HEAD
- No minimum CodeScene score target. Do NOT over-extract chasing a CodeScene number.

---

## Lizard Baseline (post-Lane-A, Lane B warnings = 42)

Lane B methods with CCN > 8 requiring reduction:

- `OnPendingBeAccountUpdate` L5480-5520 CCN=32
- `OnOrderUpdate` L1316-1431 CCN=23
- `TryFireFollowerBeRetry` L1483-1517 CCN=15
- `TryEvictFollowerBeSlot` L1542-1574 CCN=13
- `DispatchCopy` L2082-2199 CCN=13
- `TryHandleEntryDrag` L1886-1909 CCN=11
- `SyncAtmFollowerBracket` L2395-2445 CCN=11
- `DtoToRule` L5609-5672 CCN=11
- `OnTrailBeAccountUpdate` L5445-5472 CCN=9
- `SubmitBeStop` L1087-1142 CCN=10
- `IsExitSignalName` L2008-2033 CCN=10
- `CancelPttDragOrphansForAccount` L1606-1626 CCN=10
- `GetRefPrice` L5241-5248 CCN=10

---

## Known Baseline Failures

22 IL-reflection test failures in `archive/v12-reference` linting DLL.
Pre-existing since B87. Not caused by this wave. Accepted baseline.

---

## JS Rules in Scope

- **JS-021**: no `lock()` -- 0 results required (SCAN-01)
- **JS-002**: no `return null` for missing values -- 0 new instances (SCAN-03)
- **JS-033**: no `async void` (non-event-handler) -- 0 results (SCAN-02)
- **CYC**: all modified methods <= 8, all extracted helpers <= 4

---

## Pipeline: 3 Stages

| Stage | Owner | Output |
|-------|-------|--------|
| STAGE 1 | ptt-orchestrator | This document (LaneB-01-mission-brief.md) |
| STAGE 2 | ptt-architect | LaneB-02-architect-plan.md |
| STAGE 3 (x7) | ptt-engineer + ptt-verifier per ticket | LaneB-{TICKET_ID}-engineer.md + LaneB-{TICKET_ID}-verify.md |

---

## Final Pass Criteria

- All 7 tickets (TB-T1 through TB-T7): VERIFY_PASS confirmed
- lizard CopyEngine.cs --CCN 8: 0 warnings for ALL Lane B methods
- dotnet build: 0 errors, 0 warnings
- dotnet test: 0 new failures
- cs check CopyEngine.cs: score > 1.41 (Lane B start score)
- powershell -File scripts\verify_links.ps1 -Fix: PASS
- docs/brain/BWAVE-CYC/LaneB-final-report.md: written
- Output: "LANE_B_FINAL_PASS"
