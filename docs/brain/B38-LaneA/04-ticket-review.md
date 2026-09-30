# Ticket Review: B38-LaneA

**Epic**: PTT-COPIER B38 — Trim/Flatten Anchor Fix + BE-Stop TIF Fix
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-28
**Input plan status**: REVIEW_PASS
**Ticket count**: 3

---

## T1 — PttTrim + PttFlatten: 3 bug fixes (guard, anchor, TIF)

**Spec Requirements Covered**: DW-B32-TRIM-ANCHOR-01, DW-B32-TRIM-TIF-01, DW-B32-TRIM-MARKET-01

### Traceability
PASS — all 3 defect IDs (DW-B32-TRIM-ANCHOR-01, DW-B32-TRIM-TIF-01, DW-B32-TRIM-MARKET-01)
map to architecture plan §11 traceability table and spec section-b38. No phantom work.
No plan items missing from T1 scope.

### File Routing
PASS — `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs`
and `PttFlatten.cs` are correct Wave workspace paths. No Director-workspace `.cs` paths present.

### Exact Find/Replace (source ground truth cross-check)
PASS — all 6 sub-changes (T-1a through T-1f) verified against source ground truth:
- T-1a FIND `bool useLimitOrder = buffer > 0 && tickSize > 0.0` matches PttTrim.cs:85 ✓
- T-1b FIND comment `// Long sell limit: above ask. Short buy-to-cover limit: below bid.` matches PttTrim.cs:94 ✓
- T-1b FIND limitPrice `? ask + buffer * tickSize` / `: bid - buffer * tickSize;` matches PttTrim.cs:97-98 ✓
- T-1c FIND `TimeInForce.Day,` matches PttTrim.cs:115 ✓
- T-1d FIND guard (identical to T-1a) matches PttFlatten.cs:82 ✓
- T-1e FIND comment + limitPrice (identical to T-1b) matches PttFlatten.cs:91,93-95 ✓
- T-1f FIND `TimeInForce.Day,` matches PttFlatten.cs:112 ✓

### JS Pre-Check
PASS
- JS-021 (lock): No `lock()` described. `TrimPositionLocal` and `FlattenPositionLocal` are private static
  helpers with no shared mutable state.
- JS-033 (async void): Both methods are synchronous `void`. No `async void` introduced.
- JS-002 (return null): No `return null` in modified methods. Only `FindPositionLocal` (NT8-050 pattern,
  explicitly exempted) returns null.
- JS-001 (throw exception): No exception throwing described.

### CYC Pre-Check
PASS — Removing `buffer > 0 &&` from a boolean `&&` chain removes an operand, not a branch node.
The boolean expression has no new conditional paths. CYC=5 pre-B38 and post-B38 for both
`TrimPositionLocal` and `FlattenPositionLocal`. Architecture plan §9 corroborates. All methods ≤ 8.

### NT8 Constraints
PASS
- NT8-049: arg6=limitPrice, arg7=stopPrice=0 positions unchanged. Only the VALUE of limitPrice changes.
- NT8-014: Signal names `"PTT-Trim"` and `"PTT-Flatten"` explicitly listed as unchanged.
- NT8-006: No LINQ introduced.
- NT8-007: Not applicable to Trim/Flatten Limit orders (no CustomOrder null arg).

### Completeness (7 TIF.Day locations)
PASS — T1 covers PttTrim.cs:115 and PttFlatten.cs:112 (2 of 7 Day locations).
The remaining 5 (PttBreakEven.cs:179/317/350, CopyEngine.cs:1597/1636) are covered by T2.
All 7 locations accounted for across T1 + T2.

### Anchor Direction
PASS — REPLACE formula is `ask - buffer * tickSize` (Long) and `bid + buffer * tickSize` (Short).
This matches `CopyEngine.ComputeLimitPx` reference formula cited in plan §2. Direction is correct.

### Guard Removal
PASS — T-1a removes `buffer > 0 &&` from `TrimPositionLocal` useLimitOrder.
T-1d removes `buffer > 0 &&` from `FlattenPositionLocal` useLimitOrder. Both covered.

### Build Tag
N/A for T1 (build tag update is in T2/C-3). No violation.

### Test Coverage
PASS — All modified methods (`TrimPositionLocal`, `FlattenPositionLocal`) have corresponding
[Fact] tests specified in T3:
- `T_B38_TrimModule_Long_LimitBelowAsk` → DW-B32-TRIM-ANCHOR-01 (Long direction)
- `T_B38_TrimModule_Short_LimitAboveBid` → DW-B32-TRIM-ANCHOR-01 (Short direction)
- `T_B38_TrimModule_BufferZero_SubmitsLimit` → DW-B32-TRIM-MARKET-01
- `T_B38_TrimModule_Gtc_TifCorrect` → DW-B32-TRIM-TIF-01
T3 spec requirements table explicitly maps each [Fact] back to the defect IDs above.

### Scan Checklist
PASS — SCAN-01 through SCAN-07 all present in T1. Defense-in-depth contract intact.
Note: SCAN-04 and SCAN-07 are cross-ticket (span T1+T2+T3); this is documented and valid.
The note instructs the engineer to apply all 3 tickets before verifying those two scans.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — PttBreakEven + CopyEngine: BE-Stop TIF fix + build tag

**Spec Requirements Covered**: DW-B38-STOP-TIF-01, section-b38/build-tag

### Traceability
PASS — DW-B38-STOP-TIF-01 and section-b38/build-tag both map to architecture plan §11.
No phantom work. No plan items covering PttBreakEven or CopyEngine SubmitBeStop omitted.

### File Routing
PASS — `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs`
and `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` are correct
Wave workspace paths. No Director-workspace `.cs` paths present.

### Exact Find/Replace (source ground truth cross-check)
PASS — all 6 sub-changes (B-1 through C-3) verified against source ground truth:
- B-1 FIND partial context matches PttBreakEven.cs:179 `TimeInForce.Day,` with correct surrounding
  args (pos.Quantity, 0, bePrice, ocoId, "PTT-BE-Stop") ✓
- B-2 FIND `TimeInForce.Day, barePos.Quantity,` matches PttBreakEven.cs:317 ✓
- B-3 FIND `TimeInForce.Day, t.Qty,` matches PttBreakEven.cs:350 ✓
- C-1 FIND `TimeInForce.Day, pos.Quantity,` with context `0, // arg6: limitPrice=0` matches
  CopyEngine.cs:1597 ✓
- C-2 FIND `TimeInForce.Day, t.Qty,` with context `0, // arg6: limitPrice=0` matches
  CopyEngine.cs:1636 ✓
- C-3 FIND `internal const string Tag = "PTT-COPIER B37 | be-oco-per-pair | 2026-07-27";`
  matches CopyEngine.cs:41 ✓

### JS Pre-Check
PASS
- JS-021 (lock): No `lock()` described. Methods are synchronous private void on their respective objects.
- JS-033 (async void): All methods are synchronous void. No `async void` introduced.
- JS-002 (return null): No `return null` in modified methods.
- ASCII-only: Build tag string uses only ASCII characters including pipe `|`. No Unicode.

### CYC Pre-Check
PASS — All changes are TIF token swaps (`TimeInForce.Day` → `TimeInForce.Gtc`) and a
string literal replacement for the build tag. No new branches, no branch removals. CYC unchanged
for `SubmitBeStopLocal` (=3), `SubmitBeTargetsLocal` (unchanged), and `SubmitBeStop` (unchanged).
All methods ≤ 8.

### NT8 Constraints
PASS
- NT8-049: arg6=limitPrice=0 and arg7=stopPrice=bePrice positions explicitly called out as preserved.
  Only the TimeInForce argument (different positional index) changes.
- NT8-013: `DateTime.MaxValue` noted as unchanged in T2 FIND context for C-1 and C-2.
- NT8-014: Signal names `"PTT-BE-Stop"` and `"PTT-BE-Stop-" + (i+1)` explicitly listed as unchanged.
- NT8-006: No LINQ introduced.

### Completeness (7 TIF.Day locations)
PASS — B-1 covers PttBreakEven.cs:179, B-2 covers PttBreakEven.cs:317, B-3 covers
PttBreakEven.cs:350, C-1 covers CopyEngine.cs:1597, C-2 covers CopyEngine.cs:1636.
Together with T1, all 7 Day locations in PropTraderTools are replaced.

### Anchor Direction
N/A for T2 — SubmitBeStop path submits stop orders (no limitPrice calculation). No anchor
direction applies. No violation.

### Guard Removal
N/A for T2 — no `buffer > 0` guard exists in the BE-Stop submission path. No violation.

### Build Tag
PASS — C-3 REPLACE: `"PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28"`. Matches
architecture plan §13 and spec section-b38/build-tag. ASCII-only confirmed.

### Test Coverage
PASS — Both DW-B38-STOP-TIF-01 defect paths (SubmitBeStopLocal and SubmitBeStop) have
corresponding [Fact] tests specified in T3:
- `T_B38_BeStop_Gtc_TifCorrect` → DW-B38-STOP-TIF-01 (PttBreakEven path)
- `T_B38_BeStopArmed_Gtc_TifCorrect` → DW-B38-STOP-TIF-01 (CopyEngine path)
T3 spec requirements table explicitly maps both to DW-B38-STOP-TIF-01.
Build tag is a constant update — no [Fact] required for a string literal assignment.

### Scan Checklist
PASS — SCAN-01 through SCAN-07 all present in T2. Defense-in-depth contract intact.
Cross-ticket scans (SCAN-04, SCAN-07) correctly note they require T1+T2+T3 to all be applied first.

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — CopyEngineTests.cs: 6 new [Fact] tests (188 → 194)

**Spec Requirements Covered**: section-b38/tests, DW-B32-TRIM-ANCHOR-01, DW-B32-TRIM-MARKET-01,
DW-B32-TRIM-TIF-01, DW-B38-STOP-TIF-01

### Traceability
PASS — section-b38/tests and all 4 defect IDs are present in architecture plan §11 test row
and §5 (FILE 5). No phantom test methods. Each [Fact] is explicitly mapped back to a defect ID
in T3's spec requirements table.

### File Routing
PASS — `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` is correct
Wave workspace path. No Director-workspace `.cs` paths present.

### Exact Find/Replace
PASS — T3 is an append-only operation. No FIND patterns required. Test bodies are given verbatim
with exact method names, assertion values, and fallback logic for NT8 runtime dependency.

### JS Pre-Check
PASS
- JS-021 (lock): No `lock()` in test file.
- JS-033 (async void): All 6 new methods are `public void`, not `async void`.
- JS-002 (return null): No `return null` in any test method. SCAN-03 command in T3
  checklist confirms `== 0 results`.
- xUnit-only mandate: All 6 methods use `[Fact]` attribute (xUnit). No NUnit `[Test]`
  or MSTest `[TestMethod]` present.

### CYC Pre-Check
PASS — All 6 new methods are CYC=1 (single linear path, no branches). Plan §9 confirms.
No existing tests modified. No method exceeds CYC=8.

### NT8 Constraints
PASS — First 3 tests call `CopyEngine.ComputeLimitPx` (pure double arithmetic, no NT8 runtime
types). Last 3 tests use `TimeInForce.Day` and `TimeInForce.Gtc` enum values with a documented
fallback approach (source-text scan) if NT8 enums are not available in the test project.
No NT8 runtime dependency violations. NT8-014 signal names not referenced in tests.

### Completeness
PASS — All 4 defects have at least one test:
- DW-B32-TRIM-ANCHOR-01: 2 tests (Long + Short direction)
- DW-B32-TRIM-MARKET-01: 1 test (buffer=0 → Limit, not Market)
- DW-B32-TRIM-TIF-01: 1 test (Gtc regression anchor)
- DW-B38-STOP-TIF-01: 2 tests (PttBreakEven path + CopyEngine path)
Total: 6 new [Fact] methods. Count 188 → 194 anchored by SCAN-07.

### Anchor Direction
PASS — T_B38_TrimModule_Long_LimitBelowAsk asserts `ask - 1*tick = 7499.75` (correct Long
direction). T_B38_TrimModule_Short_LimitAboveBid asserts `bid + 1*tick = 7500.25` (correct
Short direction). Values and formula consistent with T1 anchor fix and plan §2 reference
implementation.

### Guard Removal
PASS — T_B38_TrimModule_BufferZero_SubmitsLimit asserts that `exitBuffer=0` produces
`ask - 0*tick = ask` (7500.00), documenting that buffer=0 results in a valid Limit price
rather than degenerating to Market. This correctly anchors the T-1a/T-1d guard removal.

### Build Tag
N/A for T3 (test file). No violation.

### Test Coverage
PASS — T3 IS the test ticket. All 6 [Fact] method names are specified exactly. CYC=1 for
each new test method means no further tests are required for the tests themselves.
SCAN-07 command `(Get-Content ... CopyEngineTests.cs | Select-String "\[Fact\]").Count -eq 194`
provides the binding contract.

### Scan Checklist
PASS — SCAN-01 through SCAN-07 all present in T3. Defense-in-depth contract intact.
Note regarding SCAN-04 and SCAN-07: both correctly require T1+T2 to be applied before T3's
scan suite can be verified in full, which is architecturally sound (T3 scans confirm the
complete post-B38 state).

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

**Summary of checks performed**:

| Check | T1 | T2 | T3 |
|-------|----|----|-----|
| Traceability (spec req IDs) | PASS | PASS | PASS |
| File routing (Wave workspace) | PASS | PASS | PASS |
| Exact find/replace (source ground truth) | PASS | PASS | PASS |
| JS Pre-Check (JS-021/033/002/001) | PASS | PASS | PASS |
| CYC Pre-Check (≤ 8, no new branches) | PASS | PASS | PASS |
| NT8 Constraints (NT8-049/013/014/006) | PASS | PASS | PASS |
| Completeness (all 7 TIF.Day locations) | PASS | PASS | N/A |
| Anchor direction (Long=ask-buf*tick, Short=bid+buf*tick) | PASS | N/A | PASS |
| Guard removal (buffer > 0 && removed) | PASS | N/A | PASS |
| Build tag (B38 slug in CopyEngine.cs:41) | N/A | PASS | N/A |
| Test coverage ([Fact] for every new method) | PASS | PASS | PASS |
| 7-Scan checklist (SCAN-01 through SCAN-07) | PASS | PASS | PASS |

**Violations found**: 0

**Engineer instruction**: Apply tickets in order T1 → T2 → T3. After all three are applied,
run the full 7-scan suite once to confirm cross-ticket consistency. SCAN-04 and SCAN-07 span
all three tickets and must be verified after the complete set is applied.
