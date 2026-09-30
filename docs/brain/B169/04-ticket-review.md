# Ticket Review: B169 — Cycle 2
## Block: DW-B168-02 — _dragsInFlight HashSet<string> Race Fix
## Phase: 3.5 (PTT Ticket Reviewer)
## Source Tickets: docs/brain/B169/04-tickets.md (corrected — cycle 2)
## Source Plan: docs/brain/B169/02-architecture-plan.md (REVIEW_PASS)
## Spec: DW-B168-02
## Prior Review: docs/brain/B169/04-ticket-review.md (cycle 1 — TICKET_REVIEW_FAIL)

---

## Cycle 2 Focus Checks

### Fix 1 — B169-2 Edit 2 SEARCH blank-line count
**VERIFIED FIXED.**

Prior failure: Edit 2 SEARCH required 3 blank lines after `    }` at line 10173 but post-Edit-1 file state had only 2 blank lines.

Actual file state confirmed (CopyEngineTests.cs lines 10267-10271):
- 10267: `    }` (DwB168Tests closing brace)
- 10268: blank
- 10269: blank
- 10270: blank
- 10271: `}`

Edit 1 SEARCH captures lines 10175-10269 (the DwB168Tests class + 2 trailing blank lines).
After Edit 1 deletes 10175-10269, post-Edit-1 state from 10173:
- 10173: `    }` (previous class close — unchanged)
- 10174: blank (unchanged)
- 10175: blank (was line 10270 — not captured by Edit 1)
- 10176: `}` (namespace close, was 10271)

Post-Edit-1 = **2 blank lines** between `    }` and `}`.

Edit 2 SEARCH (ticket lines 679-685):
```
    }


}
```
Counts: `    }` + blank (line 683) + blank (line 684) + `}` = **2 blank lines**. ✓ MATCHES.

### Fix 2 — B169-1 SCAN-02 count consistency
**VERIFIED FIXED.**

Prior advisory: SCAN-02 had "5 occurrences" in a heading contradicted by "7" in the parenthetical.

Corrected ticket line 489:
> `grep -r "_dragsInFlight" src/` → **exactly 7 occurrences**: field declaration (1) + `TryTrackDragInFlight` Add (1) + `TryTrackDragInFlight` Remove (1) + `TryRemoveDragSuffix` Remove (1) + `HandleAtmStopSync` Remove (1) + `ResubmitCollateralLegs` Contains (1) + `DragsInFlightForTest` seam (1)

Single consistent count of 7. No heading with a different number. No contradictions. ✓

---

## TICKET B169-1 — CopyEngine.cs: Add _dragsInFlight HashSet guard; remove IsLeaderLegBeingChanged dead code

### Check 1 — Line numbers sourced from actual source (spot-check 3+)
PASS — 7 sites spot-checked against actual CopyEngine.cs:
- Edit A `:start_line:437`: `_laneEDispatched` field at 437-438; blank at 439; `// --- Status event ---` at 440. Exact match. ✓
- Edit B `:start_line:2360`: `NinjaTrader.NinjaScript.PrintTo.OutputTab1` at 2360; `);` at 2361; `if (!IsWorkingBracket(order))` at 2362. Exact match. ✓
- Edit C `:start_line:2368`: `}` at 2368; blank at 2369; `// B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.` at 2370. Exact match. ✓
- Edit F `:start_line:3973`: foreach loop body with `FindLeaderCollateralOrder` at 3981, `IsLeaderLegBeingChanged` at 3983, closing `}` at 3986-3987. Exact match. ✓
- Edit G/H `:start_line:3804`: IsLeaderLegBeingChanged comment block starts at 3804; method definition at 3815; seam at 3823-3824; trailing blank at 3825. Exact match. ✓
- Edit I `:start_line:4167`: `IsPttStpDragCancellableTestable` at 4169; blank at 4170; next comment at 4171. Exact match. ✓
- Edit J `:start_line:3951`: Comment block verified in prior review cycle. ✓

### Check 2 — FindLeaderCollateralOrder AFTER Contains check in ResubmitCollateralLegs
PASS — Edit F REPLACE block has `_dragsInFlight.Contains(s)` guard (with `continue`) at position before `Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);`. Short-circuit order is correct. Also enforced by SCAN-06.

### Check 3 — IsLeaderLegBeingChanged fully REMOVED (not commented out)
PASS — Edit G+H combined REPLACE block is empty (no replacement text). The SEARCH covers the complete definition: comment block (3804-3814) + method body (3815-3818) + blank (3819) + seam comment (3820-3822) + seam body (3823-3824) + blank (3825). Full removal confirmed.

### Check 4 — IsLeaderLegBeingChangedTestable fully REMOVED (not commented out)
PASS — Included in the combined G+H SEARCH block (lines 3820-3824). Both methods removed atomically in one diff block.

### Check 5 — No whitespace mutations outside changed lines
PASS — All SEARCH blocks match exact source. No trailing-space additions or indentation changes outside intentional edit lines. Apply order (bottom-up: J→G+H→F→E→I→D→C→B→A) preserves line number stability for sequential application.

### Check 6 — No lock()
PASS — Zero `lock(` calls in any C# code sample in B169-1. New methods `TryTrackDragInFlight` and `TryRemoveDragSuffix` and the field declaration all carry explicit JS-021 no-lock annotations.

### Check 7 — ASCII-only
PASS — All string literals and comment text use ASCII only. Dashes rendered as `--` (two ASCII hyphens), not em-dashes (U+2014). No Unicode identifiers or curly quotes.

### Check 8 — Internal seam matches IsPttStpDragCancellableTestable pattern
PASS — Actual source at line 4169:
```csharp
internal static bool IsPttStpDragCancellableTestable(Order o) => IsPttStpDragCancellable(o);
```
New seam:
```csharp
internal HashSet<string> DragsInFlightForTest() => _dragsInFlight;
```
Pattern (internal, single-expression delegation, no NT8 runtime) preserved. Instance vs static difference is intentional and documented. ✓

### Check 9 — 7-scan checklist present in B169-1
PASS — All 7 scans present with no contradictions:
- SCAN-01: grep IsLeaderLegBeingChanged → 0 matches ✓
- SCAN-02: grep _dragsInFlight → exactly 7 occurrences with full breakdown ✓ (previously advisory; now clean)
- SCAN-03: grep lock( → 0 new matches ✓
- SCAN-04: CYC all touched methods ≤ 8 ✓
- SCAN-05: ASCII-only check ✓
- SCAN-06: Contains before FindLeaderCollateralOrder ✓
- SCAN-07: ptt-sync.ps1 exits 0 ✓

### Check 10 — CYC numbers stated for all touched methods, all < 8
PASS — CYC table covers all 8 affected methods:
| Method | CYC After |
|--------|-----------|
| HandleAtmStopSync | 3 |
| TryHandleBracketDrag | 3 |
| TryCancelFollowerEntries | 4 |
| ResubmitCollateralLegs | 3 |
| TryTrackDragInFlight (new) | 3 |
| TryRemoveDragSuffix (new) | 2 |
| IsLeaderLegBeingChanged | REMOVED |
| IsLeaderLegBeingChangedTestable | REMOVED |
All retained methods ≤ 8. ✓

### JS Pre-Check (JS-001/002/008/009/021/023/025)
PASS — No lock() calls. No mutable struct fields. No SolidColorBrush. `HashSet<string>` is instance field on CopyEngine (single NT8 callback thread — no concurrent access). No throw in hot path. No null returns for optional values. No Dictionary on engine fields.

### NT8 Constraint Check
PASS — No async/await in lifecycle methods. No Account.All outside Loaded. No sealed on TradeCopierWindow. No FontFamily on WPF elements. No hardcoded hex colors. No CreateOrder with non-PTT- prefix. No DateTime.Now.

### File Routing
PASS — `src/PropTraderTools/CopyEngine.cs` is the correct Wave workspace path.

### Test Coverage
PASS — B169-1 adds implementation methods only. Full test coverage is provided in B169-2 via the `DragsInFlightForTest()` seam.

### Traceability
PASS — All edits A through J trace to DW-B168-02 requirement. No phantom work. No plan items unaccounted for.

### VERDICT B169-1: TICKET_REVIEW_PASS

---

## TICKET B169-2 — CopyEngineTests.cs: Remove DwB168Tests; add DwB168_02Tests

### Check 11 — DwB168Tests line range accurate (spot-check)
PASS — Actual file confirmed:
- Line 10173: `    }` (previous class closing brace) ✓
- Line 10174: blank ✓
- Line 10175: `// DW-B168-01: Tests for IsLeaderLegBeingChanged via IsLeaderLegBeingChangedTestable seam.` ✓
- Line 10177: `public class DwB168Tests` ✓
- Line 10267: `    }` (DwB168Tests closing brace) ✓
- Lines 10268-10270: three blank lines ✓
- Line 10271: `}` (namespace closing brace) ✓

### Check 12 — 4 [Fact] methods with correct names present
PASS — All 4 required test methods present:
- `DragsInFlight_SingleStop_CollateralResubmitted` ✓
- `DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg` ✓
- `DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs` ✓
- `DragsInFlight_ClearOnCancelled_PreventsPermamentSkip` ✓

### Check 13 — Tests use _dragsInFlight seam (not direct field access)
PASS — All 4 tests access `_dragsInFlight` exclusively via `CopyEngine.Instance.DragsInFlightForTest()`. No direct field reference from test assembly.

### Check 14 — No NT8 runtime references in test bodies
PASS — Test bodies contain only: `CopyEngine.Instance.DragsInFlightForTest()`, `Assert`, and `HashSet<string>` BCL operations (Add, Remove, Contains, Clear). Zero `NinjaTrader.*` namespace references in any test body.

### Check 15 — Tests compile without NT8 runtime
PASS — All 4 tests use only BCL types (`HashSet<string>`, `bool`, `string`) and `Assert`. `DragsInFlightForTest()` returns `HashSet<string>` (BCL). No NT8 runtime dependency required for test execution.

### Check 16 — 7-scan checklist present in B169-2
PASS — All 7 scans present:
- SCAN-01: 4 [Fact] tests compile without NT8 runtime ✓
- SCAN-02: All 4 tests access via DragsInFlightForTest() seam ✓
- SCAN-03: DwB168Tests absent after edit (grep → 0 matches) ✓
- SCAN-04: DwB168_02Tests present with exactly 4 [Fact] methods ✓
- SCAN-05: No whitespace mutations outside delete/insert regions ✓
- SCAN-06: No StubOrder pattern in DwB168_02Tests ✓
- SCAN-07: ptt-sync.ps1 already exited 0 from B169-1 (no second run) ✓

### Check 17 — apply_diff blocks are surgical (no write_file on CopyEngine.cs or CopyEngineTests.cs)
PASS — Both edits use `apply_diff` blocks exclusively. No `write_file` call anywhere in either ticket.

### Edit 2 SEARCH blank-line count (primary focus check — cycle 2)
PASS — Verified above. Edit 1 SEARCH captures lines 10175-10269 (class body + 2 trailing blanks). Post-Edit-1 state from line 10173: `    }`, blank, blank, `}` (2 blank lines). Edit 2 SEARCH contains exactly 2 blank lines between `    }` and `}`. Match confirmed. ✓

### JS Pre-Check
PASS — No lock() in test code. No NT8 runtime in test bodies. ASCII-only. No heap allocation beyond HashSet operations inherent to the test (BCL).

### NT8 Constraint Check
PASS — Test file has no lifecycle method issues; no WPF, no Account.All, no CreateOrder, no DateTime.Now.

### File Routing
PASS — `src/PropTraderTools/CopyEngineTests.cs` is the correct Wave workspace path.

### Traceability
PASS — Both edits (remove DwB168Tests, add DwB168_02Tests) trace directly to DW-B168-02 requirement. No phantom work.

### VERDICT B169-2: TICKET_REVIEW_PASS

---

## Cross-Ticket Checks

### Check 18 — Tickets are independent of each other
PASS — B169-2 references only `DragsInFlightForTest()` by method name and `DwB168Tests` by class name. No internal implementation details of B169-1 are required by B169-2 beyond the seam contract.

### Check 19 — DW-B168-02 requirement ID in both tickets
PASS — `DW-B168-02` appears in Spec Requirement IDs header for both B169-1 and B169-2. Post-Ticket Summary maps all 12 sub-requirements to ticket/edit entries.

### Check 20 — ptt-sync.ps1 listed in B169-1
PASS — Listed explicitly in B169-1 "Post-Edit Step" section with exact command `powershell -File .\scripts\ptt-sync.ps1`. Marked required with "Must exit 0."

---

## Overall Summary (All 20 Checks)

| # | Check | Cycle 1 | Cycle 2 |
|---|-------|---------|---------|
| 1 | Line numbers sourced from actual source | PASS | PASS |
| 2 | Contains BEFORE FindLeaderCollateralOrder | PASS | PASS |
| 3 | IsLeaderLegBeingChanged fully REMOVED | PASS | PASS |
| 4 | IsLeaderLegBeingChangedTestable fully REMOVED | PASS | PASS |
| 5 | No whitespace mutations | PASS | PASS |
| 6 | No lock() | PASS | PASS |
| 7 | ASCII-only | PASS | PASS |
| 8 | Internal seam matches existing pattern | PASS | PASS |
| 9 | 7-scan checklist B169-1 (SCAN-02 count) | PASS (advisory) | **PASS (clean)** |
| 10 | CYC numbers all < 8 | PASS | PASS |
| 11 | DwB168Tests line range accurate | PASS | PASS |
| 12 | 4 [Fact] methods with correct names | PASS | PASS |
| 13 | Tests use DragsInFlightForTest seam | PASS | PASS |
| 14 | No NT8 runtime references in test bodies | PASS | PASS |
| 15 | Tests compile without NT8 runtime | PASS | PASS |
| 16 | 7-scan checklist B169-2 | PASS | PASS |
| 17 | apply_diff blocks surgical | PASS | PASS |
| 18 | Tickets independent | PASS | PASS |
| 19 | DW-B168-02 requirement ID in both tickets | PASS | PASS |
| 20 | ptt-sync.ps1 mandatory post-edit step | PASS | PASS |
| — | **Edit 2 SEARCH blank-line count (focus fix)** | **FAIL** | **PASS** |

---

## Overall: TICKET_REVIEW_PASS

Both corrective fixes from cycle 1 verified:
1. B169-2 Edit 2 SEARCH now contains exactly 2 blank lines, matching the post-Edit-1 file state. ✓
2. B169-1 SCAN-02 states exactly 7 occurrences with a full consistent breakdown. No contradictory count. ✓

All 20 checks PASS. No blocking violations. No advisories. The engineer contract is complete.

**TICKET_REVIEW_PASS**
