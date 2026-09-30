# PTT-BE-FIX -- Ticket Review
Status: TICKET_REVIEW_PASS
Date: 2026-08-22
Reviewer: ptt-ticket-reviewer (Phase 3.5)
Input: docs/brain/PTT-BE-FIX/04-tickets.md
Plan: docs/brain/PTT-BE-FIX/02-architecture-plan.md (REVIEW_PASS Cycle 2)

---

## Verdict

TICKET_REVIEW_PASS

All four tickets (T1, T2, T3, T4) passed all ten review checks (A–J) with zero
violations. No blocking issues. Engineering may proceed to Phase 4.

---

## Check Results

| Check | T1 | T2 | T3 | T4 | Overall |
|-------|----|----|----|----|---------|
| A — Traceability | PASS | PASS | PASS | PASS | PASS |
| B — 7-Scan Checklist (BLOCKING) | PASS | PASS | PASS | PASS | PASS |
| C — JS Pre-Check | PASS | PASS | PASS | PASS | PASS |
| D — NT8 Constraints | PASS | PASS | N/A | PASS | PASS |
| E — Acceptance Criteria | PASS | PASS | PASS | PASS | PASS |
| F — Before/After Accuracy | PASS | PASS | N/A (new file) | PASS | PASS |
| G — Test Coverage (T3) | N/A | N/A | PASS | N/A | PASS |
| H — Post-Implementation Steps | PASS | PASS | PASS | PASS | PASS |
| I — CYC Pre-Check | PASS (+0) | PASS (8->7) | PASS (1-2/Fact) | PASS (5, unchanged) | PASS |
| J — Ordering Enforcement | PASS | PASS | PASS | PASS | PASS |

---

## Check Detail

### CHECK A — Traceability

- T1: Cites section-b86 (spec) + DW-B84-03 (context). Maps to Plan Section B T1. PASS.
- T2: Cites section-b85 Option B (spec). Maps to Plan Section B T2. PASS.
- T3: Cites section-b84, DW-B84-01/02/03, DW-B86. Maps to Plan Section D. PASS.
- T4: Cites DW-T4, DW-B84-01. Maps to Plan Section B T4. PASS.
- No phantom work (all ticket items traceable to plan/spec).
- No missing work (T1, T2, T3, T4 fully cover the plan).

### CHECK B — 7-Scan Checklist Presence (BLOCKING)

All 7 scans present in all 4 tickets. Detail:

| Scan | T1 | T2 | T3 | T4 |
|------|----|----|----|-----|
| Scan 1 lock() | PASS | PASS | PASS | PASS |
| Scan 2 async void | PASS | PASS | PASS | PASS |
| Scan 3 throw new | PASS | PASS | PASS | PASS |
| Scan 4 CYC <= 8 | PASS | PASS | PASS | PASS |
| Scan 5 ASCII-only | PASS | PASS | PASS (note 1) | PASS |
| Scan 6 xUnit (N/A for T1/T2/T4) | N/A noted | N/A noted | PASS (active) | N/A noted |
| Scan 7 build | PASS | PASS | PASS | PASS |

Note 1 (T3 Scan 5, non-blocking): T3 Scan 5 scopes the ASCII grep to
`src/PropTraderTools/` and does not cover the new `tests/` file path. The
test file itself is described as ASCII-only throughout the ticket. No non-ASCII
content is introduced. The omission does not rise to a FAIL because (a) the scan
IS present for production code, (b) no Unicode is described in the test file,
and (c) the scan absence is for test-only content. Engineer should add
`tests/PropTraderTools.Tests/` to the Scan 5 path as a quality improvement.

### CHECK C — JS Pre-Check

T1:
  JS-021: No lock() introduced. bool isBeStop is a stack-local inside an existing loop. PASS.
  JS-001: No throw introduced. PASS.
  JS-002: bool isBeStop is non-nullable. No null return. PASS.
  JS-033: No async void introduced. PASS.
  JS-036: bool is a stack allocation, zero heap. PASS.
  JS-066: CYC +0 (inline if -> bool var + if(isBeStop) = same 1 branch point). PASS.
  ASCII-only: All added comment lines verified ASCII-only. PASS.

T2:
  JS-021: No lock(). Account.All is NT8 thread-safe. PASS.
  JS-001: No throw. PASS.
  JS-002: FindFollowerAccount returns Account? (nullable annotated). Caller assigns to
    followers[i] and then checks `if (followers[i] == null)`. Nullability explicit. PASS.
  JS-033: No async void. PASS.
  JS-036: DtoToRule runs at cold load time, not hot path. PASS.
  JS-066: DtoToRule 8->7, FindFollowerAccount CYC=2. Both <= 8. PASS.
  ASCII-only: Apostrophe 0x27, hyphens 0x2D0x2D verified. No em-dash, no curly quotes. PASS.

T3:
  JS-051: [Fact] attribute required; [Test]/[TestMethod] forbidden; confirmed. PASS.
  JS-021: No lock() in test file. PASS.
  ASCII-only: All string literals described as ASCII-only. PASS.
  JS-001: No throw in test methods; Assert methods used exclusively. PASS.

T4:
  JS-021: No lock(). Comment-only change. PASS.
  ASCII-only: Two hyphens (--) used, not em-dash. PASS.
  Comment-only, zero logic change. PASS.

### CHECK D — NT8 Constraints

T2: NinjaTrader.Code.Output.Process(..., NinjaTrader.NinjaScript.PrintTo.OutputTab1)
  Verified against existing calls in same file (L2770-2773, L2781-2784, L2786-2789).
  This is the established pattern for AddOnBase context in CopyEngine.cs. Consistent with
  existing production usage. Plan review Cycle 2 confirmed NT8 API valid for AddOnBase. PASS.

T2: Account.All usage confirmed AddOnBase-safe by plan review Cycle 2. PASS.

T1: acc.Change() not added or modified; ticket explicitly notes all code at L2764-2792
  is unchanged. PASS.

T4: Source read at L1820-1823 confirms guard `if (!IsFollowerAccount(cancelledStop.Account)) return;`
  exists and ticket correctly identifies it (defensive secondary check). Comment-only edit
  does not touch the guard. PASS.

Additional NT8 hard constraints (none violated):
  No async/await in lifecycle methods: N/A.
  No Account.All in constructor: PASS (used in DtoToRule/FindFollowerAccount, not constructor).
  No sealed on TradeCopierWindow: N/A.
  No FontFamily: N/A.
  No hardcoded hex color: N/A.
  No CreateOrder without PTT- prefix: N/A.
  No DateTime.Now: N/A.

### CHECK E — Acceptance Criteria Completeness

T1: beSt>0 for QX stops; Stop1..Stop9 regression preserved; false-positives excluded;
  acc.Change() correct; SIM gate 3 cycles Path B. All 5 criteria present. PASS.

T2: Warning string format; ASCII-only; per-null-slot emission; no warning when all resolve;
  DtoToRule CYC 7; FindFollowerAccount CYC 2; build 0 errors. All 7 criteria present. PASS.

T3: Test file path; 10 [Fact] pass; no NUnit/MSTest; 5 coverage areas; build 0 errors.
  All 5 criteria present. PASS.

T4: Engineer confirms call site; structural unreachability; 2-line ASCII comment added;
  no logic change (ANALYSIS-COMPLETE). All 4 criteria present. PASS.

### CHECK F — Before/After Code Accuracy

T1 BEFORE vs actual CopyEngine.cs L2753-2762:
  EXACT MATCH. Two comment lines + 4-condition if-guard + body match byte-for-byte. PASS.

T2 BEFORE vs actual CopyEngine.cs L3396-3407:
  EXACT MATCH. var followers declaration + for loop + inner foreach + if/break match
  byte-for-byte. PASS.

T1 AFTER: bool isBeStop with ATM OR-branch and PTT-QX-Stop OR-branch present and correct.
  Original ATM guard (StartsWith "Stop" && Length==5 && IsDigit) preserved as first branch.
  New branch: StartsWith("PTT-QX-Stop", StringComparison.Ordinal). PASS.

T2 AFTER: FindFollowerAccount call, followers[i] assignment, null-check if-block with
  warning string and correct PrintTo.OutputTab1 argument. PASS.

T4 BEFORE vs actual CopyEngine.cs L1818-1820:
  EXACT MATCH. CYC comment L1818, JS comment L1819, method signature L1820. PASS.

### CHECK G — Test Coverage (T3)

All 10 [Fact] names from Plan Section D present in T3 ticket Section 5:
  1. FollowerPath_EarlyReturn_SkipsStepBAndC       PASS
  2. StopNameGuard_AtmStop1_Matches                PASS
  3. StopNameGuard_AtmStop9_Matches                PASS
  4. StopNameGuard_PttQxStop_Matches               PASS
  5. StopNameGuard_PttQxStop4_Matches              PASS
  6. StopNameGuard_StopMarket_Rejected             PASS
  7. StateGuard_Working_Accepted_ChangeSubmitted_Included PASS
  8. StateGuard_CancelSubmitted_Excluded           PASS
  9. Stops0_EmitsBeDiagFLogLine                    PASS
  10. BreakEvenOverload_FollowersRunBeforeLeader   PASS

5 coverage areas mapped:
  (a) Follower early return:        Fact #1  PASS
  (b) ATM stop name guard:          Facts #2, #3  PASS
  (c) QX stop name guard (DW-B86):  Facts #4, #5  PASS
  (d) State guard:                  Facts #7, #8  PASS
  (e) Followers-before-leader:      Fact #10  PASS
  Negative case:                    Fact #6  PASS
  DIAG log:                         Fact #9  PASS

### CHECK H — Post-Implementation Steps

T1+T4 (Session 1): sync-ptt-to-nt8.ps1 + git add src/ + git commit. SIM gate block. PASS.
T2 (Session 2):    sync-ptt-to-nt8.ps1 + git add src/ + git commit + manual Output tab verification. PASS.
T3 (Session 3):    git add tests/...CopyEngineBreakEvenFollowerTests.cs + git commit. PASS.
  Note: T3 correctly omits sync-ptt-to-nt8.ps1 (test-only file, not in src/). PASS.

### CHECK I — CYC Pre-Check

T1 MoveStopToBreakEven: +0 net. Inline if (1 branch) -> bool assignment (0) + if(isBeStop) (1).
  Branch count identical. The || inside the bool assignment is a compound boolean
  sub-expression, not an if-statement branch point. PASS.

T2 DtoToRule: 8 -> 7 confirmed by Plan Section C arithmetic:
  -2 (extracted inner foreach+if) + 1 (new null warning if) = -1. CYC=7. PASS.
T2 FindFollowerAccount: CYC=2 (foreach=1, if=1). PASS.

T3 [Fact] methods: each is a linear assertion sequence, no branch points.
  CYC per [Fact] = 1 (Fact #7 with 3 Assert.True is still CYC=1, sequential assertions).
  All well within <= 8. PASS.

T4 TryReplacePttBeBrackets: CYC=5, unchanged (comment-only). Confirmed by Scan 4 target
  in T4 checklist. PASS.

### CHECK J — Ordering Enforcement

Execution order table in 04-tickets.md:
  Session 1: T1 + T4 (same file, same commit)
  Session 2: T2
  Session 3: T3 (depends on T1)

T4 engineering note: "T4 is implemented in the SAME commit as T1." PASS.
T1 before T3 dependency: explicit at ticket line 19 and T3 header. PASS.
T2 before T3: preferred but flexible; correctly noted. PASS.

---

## Violations

None.

---

## Approval

Engineering may proceed to Phase 4. Execution order: T1+T4 (Session 1) → T2 (Session 2) → T3 (Session 3).

T1 must complete and pass SIM gate (3 cycles Path B) before T3 is written.
T3 StopNameGuard_PttQxStop_* facts assert the T1 guard; they will fail if T1 is not
committed first.

Engineer note on T3 Scan 5 (non-blocking, quality improvement):
  Extend Scan 5 to also cover the test file path:
    grep -Prn "[^\x00-\x7F]" src/PropTraderTools/ tests/PropTraderTools.Tests/ --include="*.cs"
  Current scope (src/ only) does not cover the new test file. No non-ASCII is described
  in the test file, so this is not a FAIL — it is a recommended extension.
