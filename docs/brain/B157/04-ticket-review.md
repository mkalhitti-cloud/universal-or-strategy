# Ticket Review -- Block B157
# ptt-ticket-reviewer Phase 3.5 output
# Reviewed: 2026-08-10 (Cycle 1) / 2026-08-10 (Cycle 2)
# Source verified: CopyEngine.cs lines 991, 1920-1975, 3510-3540, 6521-6530, 6635-6655
# Test verified: CopyEngineTests.cs lines 8095-8135, 9350-9430

---

# CYCLE 2 REVIEW (re-review of corrected B157-T2)

## Focus of Cycle 2
V-01 from Cycle 1: Scope Lock in B157-T2 declared `CopyEngine.cs lines 1926-1973` but
Change 1 of 5 modified line 1925 (annotation comment) and inserted new annotation lines
before line 1926. Engineer received contradictory instructions.

**Architect correction applied:** Scope Lock now reads:
> `CopyEngine.cs lines 1925-1973 (TryEvictFollowerBeSlot -- annotation comment + method replaced with thin router)`

This revision:
1. Extends the lower bound from 1926 to 1925.
2. Adds parenthetical `annotation comment + method replaced` -- making explicit that the
   annotation comment line (line 1925) is part of the scope.

**Residual label in Change 1 sub-heading:** The Change 1 of 5 description still carries
the sub-heading `Lines 1926-1973.` This is a label for the diff block, not a second Scope
Lock. With the Scope Lock now correctly declaring `1925-1973`, this sub-heading is a minor
imprecision but does NOT recreate a contradiction: the engineer reads the Scope Lock as the
contract, the BEFORE/AFTER diff body shows what to change, and the two are now consistent.
No new ambiguity introduced. This does not constitute a new violation.

**V-01 status: RESOLVED.**

---

## T1 -- DW-B157-RG-01: IsReversalToFlatFollower logic inversion fix + regression tests

### Traceability
PASS
- DW-B157-RG-01 maps to B157-T1 exclusively. ✓
- All ticket items trace to plan Section 1 and plan Section 8 (T1 test plan). ✓
- No phantom work detected. ✓

### JS Pre-Check
PASS
- JS-013 (CYC<=8): IsReversalToFlatFollower CYC=2 (base=1 + &&=1). ✓
- JS-021 (no lock): Static method, no shared state, no lock() introduced. ✓
- JS-001 (no throw): No throw statements in fix or tests. ✓
- JS-002 (bool return): Return type is bool, no null where value expected. ✓
- JS-009 (no Dictionary for shared state): Not applicable. ✓
- ASCII-001 (ASCII-only): All test method names, comments, and literals are ASCII-only. ✓

### CYC Pre-Check
PASS
- IsReversalToFlatFollower post-fix: base(1) + &&(1) = CYC=2. Target <=8. PASS.
- Manual formula: `currentAction != lastAction && !followerIsFlat` -- one comparison operator. CYC=2. ✓

### NT8 Check
PASS
- No async/await introduced. ✓
- No DateTime.Now introduced. ✓
- No lock() introduced. ✓
- No Account.All call. ✓
- No FontFamily, no hardcoded hex color. ✓
- No CreateOrder without PTT- prefix. ✓
- Minimal change: 1-character insertion + 4 test changes only. ✓

### Test Coverage
PASS
All 4 branches of the corrected truth table are covered:
- T1-1 (ShouldBlockReversalCopy_WhenFollowerHasOpenPosition): flat=false, reversal -> Assert.True. Protection path. ✓
- T1-2 (IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse): flat=true, reversal -> Assert.False. Regression proof (pre-fix returned true here). ✓
- T1-3 (IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue): flat=false, reversal (Sell/Buy) -> Assert.True. Second protection path confirmation. ✓
- T1-4 (IsReversalToFlatFollower_SameDirection_ReturnsFalse): same direction -> Assert.False. Covers short-circuit case. ✓

All 4 [Fact] names are exact and non-placeholder. All carry `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` as specified.

### Scan Checklist
PASS -- All 7 scans present with exact commands:
- SCAN-01: lock() check -- Select-String on CopyEngine.cs. ✓
- SCAN-02: async void check -- Select-String on CopyEngine.cs + CopyEngineTests.cs. ✓
- SCAN-03: DateTime.Now check -- Select-String on CopyEngine.cs + CopyEngineTests.cs. ✓
- SCAN-04: non-ASCII byte check -- ReadAllBytes on both files. ✓
- SCAN-05: CYC scan -- lizard command with -f IsReversalToFlatFollower. ✓
- SCAN-06: xUnit test run -- dotnet test with logger. ✓
- SCAN-07: structural verification -- 7-item manual checklist. ✓

### File Routing
PASS
- CopyEngine.cs: src/PropTraderTools/CopyEngine.cs ✓
- CopyEngineTests.cs: src/PropTraderTools/CopyEngineTests.cs ✓
- No Director workspace paths for .cs files. ✓

### Line Number Verification
PASS
- CopyEngine.cs line 6527 actual content: `return currentAction != lastAction && followerIsFlat;`
  Ticket BEFORE text: `return currentAction != lastAction && followerIsFlat;` -- EXACT MATCH. ✓
- CopyEngineTests.cs lines 8103-8125: verified against source.
  Line 8103 actual: `// PTT-REPAIRS-03 T1: verify reversal guard does NOT skip when follower has working entry orders.` -- EXACT MATCH with ticket BEFORE. ✓
  Line 8110 actual: `public void ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders()` -- EXACT MATCH with ticket BEFORE. ✓
  Line 8125 actual: `}` -- EXACT MATCH. ✓

### VERDICT: TICKET_REVIEW_PASS

---

## T2 -- DW-B156-DEFER-1 + DW-B153-05 + DW-B153-06 + DW-B156-DEFER-2: TryEvictFollowerBeSlot decomposition + annotation fixes + dead code removal

### Traceability
PASS
- DW-B156-DEFER-1 -> B157-T2 (Change 1 of 5). ✓
- DW-B153-05 -> B157-T2 (Changes 3 and 4 of 5). ✓
- DW-B153-06 -> B157-T2 (Change 5 of 5). ✓
- DW-B156-DEFER-2 -> B157-T2 (Change 2 of 5). ✓
- No phantom work detected. ✓

### JS Pre-Check
PASS
- JS-013 (CYC<=8):
  - TryEvictFollowerBeSlot post-extraction: CYC=7. ✓
  - IsLeaderFlatDrainAbort (new): CYC=8 (at limit). ✓
  - RunFollowerEvictPath (new): CYC=4. ✓
- JS-021 (no lock): ConcurrentDictionary throughout; no lock() introduced. ✓
- JS-001 (no throw): No throw statements in any new or modified method. ✓
- JS-002 (void): Both new helpers are void. ✓
- JS-009 (no Dictionary for shared state): No new Dictionary<K,V> introduced. ✓
- ASCII-001 (ASCII-only): All string literals including "[DRAIN-ABORT-LEADER-FLAT]" are ASCII-only. ✓

### CYC Pre-Check
PASS
- TryEvictFollowerBeSlot thin router: base(1) + ?.(e?.Order)(+1) + if(o==null)(+1) + if(!IsEvictTriggerState)(+1) + ?.(o.Instrument?.FullName)(+1) + ??(+1) + if(!IsFollowerAccount)(+1) = 7. <=8. ✓
- IsLeaderFlatDrainAbort: base(1) + if(IsFollowerAccount)(+1) + if(OrderState!=Filled)(+1) + if(!IsFlat)(+1) + foreach(+1) + if(kv.Value.Instrument?.FullName==)(+1) + ?.(+1) + if(abortCount>0)(+1) = 8. <=8. ✓
- RunFollowerEvictPath: base(1) + if(isFilled&&...)(+1) + &&(+1) + if(slotEvicted)(+1) = 4. <=8. ✓
- CYC arithmetic: Original CYC=14 decomposed into parent CYC=7 + helper-A CYC=8 + helper-B CYC=4. No single method exceeds 8. ✓

### NT8 Check
PASS
- No async/await introduced. ✓
- No DateTime.Now introduced. ✓
- No lock() introduced. ✓
- No Account.All call. ✓
- No CreateOrder without PTT- prefix. ✓
- Minimal change: extraction only -- no logic change. Comment-only annotation fixes. Dead code removal. ✓

### Test Coverage
PASS
No new tests required. This is a pure structural extraction -- no logic change means the existing test suite is the correct regression gate. The ticket correctly explains this rationale and specifies SCAN-06 as the gate for confirming zero regressions on tests whose names contain `TryEvictFollowerBeSlot`, `DRAIN-ABORT`, or `BeSlot`.

### Scan Checklist
PASS -- All 7 scans present with exact commands:
- SCAN-01: lock() check -- Select-String on CopyEngine.cs. ✓
- SCAN-02: async void check -- Select-String on CopyEngine.cs. ✓
- SCAN-03: DateTime.Now check -- Select-String on CopyEngine.cs. ✓
- SCAN-04: non-ASCII byte check -- ReadAllBytes on CopyEngine.cs. ✓
- SCAN-05: CYC scan -- lizard command with -f for all 3 affected methods. ✓
- SCAN-06: xUnit regression test run -- dotnet test with regression name filter. ✓
- SCAN-07: structural verification -- 8-item manual checklist including UpdateLegTargetPrice absence. ✓

### File Routing
PASS
- CopyEngine.cs: src/PropTraderTools/CopyEngine.cs ✓
- No CopyEngineTests.cs touched by this ticket. ✓
- No Director workspace paths for .cs files. ✓

### Line Number Verification (Cycle 2)
PASS
- Scope Lock (corrected): `CopyEngine.cs lines 1925-1973 (TryEvictFollowerBeSlot -- annotation comment + method replaced with thin router)` -- now includes line 1925. V-01 RESOLVED. ✓
- Change 1 sub-heading `Lines 1926-1973` is a diff-block label, not a second Scope Lock. Not contradictory given Scope Lock now declares 1925-1973. ✓
- Line 991 BEFORE: `// CYC<=5. JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.` -- EXACT MATCH. ✓
- Line 6639 BEFORE: `// CountLeaderTargets: CYC=4. Returns the number of Working native target limit orders` -- EXACT MATCH. ✓
- Line 6646 BEFORE: `// CYC<=5: rule null(1) + leader null(2) + foreach(3) + o null continue(4) + IsNativeLeaderTarget(5).` -- EXACT MATCH. ✓
- Lines 3514-3532: First line actual: `// BWAVE-REFACTOR-LaneB-T5: extracted from CaptureOtherLegTargetPrices for-i body.` -- EXACT MATCH. Last line actual: `}` -- EXACT MATCH. ✓

### VERDICT: TICKET_REVIEW_PASS

---

## T3 -- DW-B156-DEFER-3: Exact-string assertion upgrade for [DRAIN-ABORT-LEADER-FLAT] log marker

### Traceability
PASS
- DW-B156-DEFER-3 maps to B157-T3 exclusively. ✓
- All ticket items trace to plan Section 6 and plan Section 8 (T3 test plan). ✓
- No phantom work detected. ✓

### JS Pre-Check
PASS
- ASCII-001 (ASCII-only): `"[DRAIN-ABORT-LEADER-FLAT]"` is ASCII-only straight double-quotes. ✓
- JS-021 (no lock): Test method uses no lock(). ✓
- JS-001 (no throw): No throw in test method. ✓

### CYC Pre-Check
PASS (N/A)
Test-file-only ticket. No CYC constraint on test bodies. Correctly acknowledged in ticket.

### NT8 Check
PASS
- No async/await introduced. ✓
- No DateTime.Now introduced. ✓
- No lock() introduced. ✓
- Minimal change: 1 new test method only. Existing test at ~line 9364 explicitly not modified. ✓

### Test Coverage
PASS
- T3-1 (TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString): `[Fact]` (no Skip). Asserts exact string `"[DRAIN-ABORT-LEADER-FLAT]"` including leading bracket, word LEADER, and trailing bracket. Stronger than existing test at line 9364 (`"DRAIN-ABORT-FLAT"` substring). ✓
- Fallback path (source unavailable) verifies `IsLeaderFlatDrainAbort` exists as a private instance method -- structurally coupled to B157-T2 delivery. ✓
- Assertion string verified against actual CopyEngine.cs line 1952: `"[DRAIN-ABORT-LEADER-FLAT] leader="` -- the exact marker `[DRAIN-ABORT-LEADER-FLAT]` is present in source. ✓

### Scan Checklist
PASS -- All 7 scans present:
- SCAN-01: lock() check -- Select-String on CopyEngineTests.cs. ✓
- SCAN-02: async void check -- Select-String on CopyEngineTests.cs. ✓
- SCAN-03: DateTime.Now check -- Select-String on CopyEngineTests.cs. ✓
- SCAN-04: non-ASCII byte check -- ReadAllBytes on CopyEngineTests.cs. ✓
- SCAN-05: CYC scan -- Present as "Not applicable for test file. N/A." Scan is acknowledged; trivially satisfied for test-only ticket. ✓
- SCAN-06: xUnit test run -- dotnet test; new test must appear as PASSED (not skipped). ✓
- SCAN-07: structural verification -- 5-item checklist including exact marker string and unchanged existing test. ✓

### File Routing
PASS
- CopyEngineTests.cs: src/PropTraderTools/CopyEngineTests.cs ✓
- CopyEngine.cs explicitly out of scope. ✓
- No Director workspace paths for .cs files. ✓

### Line Number Verification
PASS (approximate references only)
- Ticket references "~line 9379 (TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat)". Actual source: the `[Fact]` for this test begins at line 9381, closing `}` at line 9409. The ~ notation correctly signals approximation; method name is exact. ✓
- Ticket references "existing test at ~line 9364 (`Assert.Contains("DRAIN-ABORT-FLAT", src)`)". Actual source line 9364: `Assert.Contains("DRAIN-ABORT-FLAT", src);` -- EXACT MATCH. ✓

### VERDICT: TICKET_REVIEW_PASS

---

## Overall (Cycle 2)

### Violation Resolution Table

| # | Ticket | Violation | Cycle 1 Status | Cycle 2 Status |
|---|---|---|---|---|
| V-01 | B157-T2 | Scope Lock declared lines 1926-1973; Change 1 of 5 modified line 1925 | FAIL | RESOLVED -- Scope Lock corrected to 1925-1973 with explicit parenthetical |

No new violations found in Cycle 2.

### Defect Card Coverage Matrix

| Defect Card | Ticket | Present in Ticket? | Present in Plan? |
|---|---|---|---|
| DW-B157-RG-01 | B157-T1 | YES | YES (Section 1) |
| DW-B156-DEFER-1 | B157-T2 | YES | YES (Section 2) |
| DW-B153-05 | B157-T2 | YES | YES (Section 3) |
| DW-B153-06 | B157-T2 | YES | YES (Section 4) |
| DW-B156-DEFER-2 | B157-T2 | YES | YES (Section 5) |
| DW-B156-DEFER-3 | B157-T3 | YES | YES (Section 6) |

All 6 defect cards covered. No duplicates. No phantom cards. ✓

### Ticket Summary (Cycle 2)

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | Line Numbers | VERDICT |
|---|---|---|---|---|---|---|---|---|---|
| B157-T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| B157-T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| B157-T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |

## Overall: TICKET_REVIEW_PASS

All tickets pass all checks. No violations remain open. Safe to spawn engineer (Phase 4a).

Engineer reads: `docs/brain/B157/04-ticket-review.md` (this file), then `docs/brain/B157/04-tickets.md`.
Priority sequence: B157-T1 (P0) first; B157-T2 and B157-T3 are independent and may follow in any order.

---

_Ticket review Cycle 1 authored by ptt-ticket-reviewer (Phase 3.5). Block B157. 2026-08-10._
_Ticket review Cycle 2 authored by ptt-ticket-reviewer (Phase 3.5). Block B157. 2026-08-10._
_Source verified at review time: CopyEngine.cs lines 991, 1920-1975, 3510-3540, 6521-6530, 6635-6655._
_Test verified at review time: CopyEngineTests.cs lines 8095-8135, 9350-9430._
