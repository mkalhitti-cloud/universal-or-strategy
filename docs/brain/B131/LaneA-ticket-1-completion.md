# Ticket 1 Completion Report -- DW-B138
## B131 LaneA: ATM Bracket Drag Name-Fallback Fix

**Status**: BUILD_PASS
**Engineer**: ptt-engineer
**Ticket**: docs/brain/B131/LaneA-04-tickets.md
**Review**: docs/brain/B131/LaneA-04-ticket-review.md (TICKET_REVIEW_PASS confirmed)
**Date**: 2026-08-31

---

## Changes Made

### File 1: `src/PropTraderTools/CopyEngine.cs`

| Change | Location | Description |
|--------|----------|-------------|
| INSERT | L2357 (before FindFollowerBracketOrder) | Added `SignalOrNameMatches` internal static helper (CYC=3) |
| MODIFY | L2375-L2402 | `FindFollowerBracketOrder`: added `string? leaderName = null` param, changed `string fromEntrySignalName` to `string?`, replaced `!=` guard with `!SignalOrNameMatches(...)` |
| MODIFY | L2139 | `SyncFollowerBracket` call site: added `, leaderOrder.Name` as 4th argument |
| INSERT | L2405-L2416 (after FindFollowerBracketOrder) | Added `SignalOrNameMatchesTestable` and `FindFollowerBracketOrderTestable` internal test seam accessors |

**Note**: `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` was already present at L46 from B113.

### File 2: `src/PropTraderTools/Tests/B131Tests.cs`

New file created. Added `B131Tests` class (LaneA DW-B138) with 4 `[Fact]` tests. Preserved existing `B131LaneBTests` class (DW-B139 placeholders from prior session).

### File 3: `src/PropTraderTools/PropTraderTools.csproj`

Added `<Compile Include="Tests\B131Tests.cs" />` to the explicit compile item list.

---

## Scan Results (Layer 2)

| Scan | Command | Result | Pass/Fail |
|------|---------|--------|-----------|
| SCAN-01 | `Select-String -Pattern "lock\s*\("` (excluding comments) | 0 actual lock() calls | PASS |
| SCAN-02 | `Select-String -Pattern "async void "` | 0 matches | PASS |
| SCAN-03 | `Select-String -Pattern "return null"` | L2402: 1 pre-existing (FindFollowerBracketOrder Order? return -- JS-002 compliant). No new additions. | PASS |
| SCAN-04 | `Select-String -Pattern "throw new"` | 0 matches | PASS |
| SCAN-05 | Manual CYC count from source (complexity_audit.py absent from repo) | SignalOrNameMatches=3, FindFollowerBracketOrder=4, SyncFollowerBracket=7 (unchanged). All <=8. | PASS |
| SCAN-06 | `[System.IO.File]::ReadAllBytes()` byte scan for >127 | CopyEngine.cs: 0 non-ASCII bytes. B131Tests.cs: 0 non-ASCII bytes. | PASS |
| SCAN-07 | `dotnet build` + `dotnet test --filter "FullyQualifiedName~B131"` | Build: 0 errors 0 warnings. Tests: 7 passed 0 failed. | PASS |

---

## Test Results

| Test | Class | Result |
|------|-------|--------|
| `B131_DW138_Stop1DragReachesHandleBracketChange` | `B131Tests` | PASS |
| `B131_DW138_Target1DragReachesHandleBracketChange` | `B131Tests` | PASS |
| `B131_DW138_Target3DragStillReachesHandleBracketChange` | `B131Tests` | PASS |
| `B131_DW138_BuySTPDragStillRoutesCorrectly` | `B131Tests` | PASS |
| `B131_DW139_SecondDragCancelsPriorPttTgtDrag` | `B131LaneBTests` | PASS |
| `B131_DW139_FirstDragCreatesExactlyOnePttTgtDrag` | `B131LaneBTests` | PASS |
| `B131_DW139_NoPriorPttTgtDragNoExtraCancels` | `B131LaneBTests` | PASS |

**B129/B130 Regression Tests**: 19 passed, 0 failed. No regressions.

---

## CYC Report

| Method | CYC Before | CYC After | Delta | Status |
|--------|------------|-----------|-------|--------|
| `SignalOrNameMatches` | -- (new) | 3 | +3 | NEW -- within JS budget |
| `FindFollowerBracketOrder` | 4 | 4 | 0 | UNCHANGED (guard line substituted 1:1) |
| `SyncFollowerBracket` | 7 | 7 | 0 | UNCHANGED |

Reviewer annotation confirmed: FindFollowerBracketOrder CYC=4 (not 5 as plan stated).

---

## Build Status

BUILD_PASS

- `dotnet build`: 0 errors, 0 warnings
- 4 B131_DW138_* tests: PASS
- 3 B131_DW139_* tests: PASS
- 19 B129/B130 regression tests: PASS
- Total: 26 tests passing, 0 failing

---

## Notes

1. `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` was already at CopyEngine.cs L46 (added by B113). No duplicate attribute added.
2. `complexity_audit.py` is not present in `scripts/` directory. CYC verified manually from source comments and branch counting.
3. `SignalOrNameMatches` marked `internal static` (not `private static`) per ticket-review instruction to enable test seam access.
4. `FindFollowerBracketOrderTestable` is an instance method accessor (non-static) because `FindFollowerBracketOrder` is a private instance method.
5. The `.csproj` explicit compile list required manual addition of `Tests\B131Tests.cs` since `EnableDefaultCompileItems=false`.
