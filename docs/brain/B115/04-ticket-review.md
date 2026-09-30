# B115 Ticket Review

**Date**: 2026-08-27
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Block**: B115 -- Formalize DW-B119 + DW-B121 + DW-B122 Hotfixes
**Input**: docs/brain/B115/04-tickets.md
**Input**: docs/brain/B115/02-architecture-plan.md (REVIEW_PASS -- 26/26 items PASS)
**Input**: docs/brain/B115/02-plan-review.md (REVIEW_PASS -- 2026-08-27)

---

## Summary Table

| Section | Item | Status | Notes |
|---------|------|--------|-------|
| A -- Traceability | A1: T1 references DW-B121 | PASS | T1 header and Spec IDs cite DW-B121 |
| A -- Traceability | A2: T2 references DW-B122 | PASS | T2 header and Spec IDs cite DW-B122 |
| A -- Traceability | A3: T3 references DW-B122 operator clarity | PASS | T3 header: "DW-B122 (operator precedence clarity confirmation)" |
| A -- Traceability | A4: DW-B119 handled as already-closed by B114-T1 | PASS | T1 Spec IDs explicitly state "No structural change needed" |
| B -- Completeness | B1: T1 specifies exact lines in B113Tests.cs | PASS | Table cites L32 and L42; grep confirms accuracy |
| B -- Completeness | B2: T1 constants AddSeconds(2)->10 and AddSeconds(3)->11 | PASS | Exact table in T1 Exact Changes section |
| B -- Completeness | B3: T2 specifies new file B115Tests.cs | PASS | T2 header: "new file -- does not yet exist" |
| B -- Completeness | B4: T2 includes [Fact] method name | PASS | TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState specified |
| B -- Completeness | B5: T2 uses _qxPendingFollowerCleanup seam (not sealed NT8 objects) | PASS | T2 Context section explicitly uses seam design |
| B -- Completeness | B6: T3 specifies exact parentheses location | PASS | T3 Exact Change Description steps 1 and 2 are precise |
| B -- Completeness | B7: T3 states CYC unchanged at 5 | PASS | T3 CYC impact and SCAN-06 both confirm CYC=5 |
| C -- JS Pre-Check | C1: No lock() introduced | PASS | No ticket describes lock(); SCAN-01 in all three tickets |
| C -- JS Pre-Check | C2: No async void introduced | PASS | T2 signatures: "No async". T3: no signature change |
| C -- JS Pre-Check | C3: No throw new XxxException | PASS | T2 SCAN-03 note: "Tests use Assert.* only. No throw." |
| C -- JS Pre-Check | C4: xUnit [Fact] only (not NUnit/MSTest) | PASS | T2 Acceptance Criteria: "xUnit [Fact] only -- no [Theory], no NUnit, no MSTest" |
| C -- JS Pre-Check | C5: ASCII-only strings confirmed | PASS | SCAN-07 in each ticket confirms ASCII scope |
| D -- CYC Pre-Check | D1: T1 test method CYC unchanged | PASS | SCAN-06: "CYC=1 unchanged. Two constant replacements add zero branches." |
| D -- CYC Pre-Check | D2: T2 new test method CYC <= 8 | PASS | T2 Method Signatures: "CYC = 1 each (linear assertions)." |
| D -- CYC Pre-Check | D3: T3 TryCleanupReArmedAtmBracket CYC stays at 5 | PASS | Source L2383 annotation confirmed: "// CYC=5"; T3 SCAN-06 confirms unchanged |
| E -- NT8 Constraints | E1: T2 does NOT instantiate sealed NT8 types | PASS | T2 uses OrderState enum only; Context section documents sealed-type limitation |
| E -- NT8 Constraints | E2: T3 does NOT change method behavior | PASS | T3: "Behavior change: None. C# ECMA-334 natural &&-before-|| precedence." |
| E -- NT8 Constraints | E3: No API calls requiring live NT8 host | PASS | T2 explicitly lists what cannot be tested (live Account/OrderEventArgs) |
| F -- 7-Scan Checklist | F1: T1 contains SCAN-01..SCAN-07 | PASS | T1 lines 73-106 contain all 7 scans |
| F -- 7-Scan Checklist | F2: T2 contains SCAN-01..SCAN-07 | PASS | T2 lines 273-313 contain all 7 scans |
| F -- 7-Scan Checklist | F3: T3 contains SCAN-01..SCAN-07 | PASS | T3 lines 421-458 contain all 7 scans |
| F -- 7-Scan Checklist | F4: Each scan specifies exact grep command | **FAIL** | SCAN-07 in T1, T2, and T3 uses "Confirm:" prose -- no grep command provided |
| G -- Test Coverage | G1: T1 targets correct TTL constant (AddSeconds(10) at L165) | PASS | Production value confirmed in PttGlobalQuickExit.cs L165 |
| G -- Test Coverage | G2: T2 validates DW-B122 Accepted-state guard logic | PASS | Assert.False(guardFires) for Accepted: (true && false) = false -- correct |
| G -- Test Coverage | G3: Existing T_B113_01..T_B113_04 not broken by any ticket | PASS | T1: two constants only; T2: new file; T3: compiler-equivalent parentheses |

---

## Detailed Findings

### FAIL: F4 -- SCAN-07 Missing Exact Grep Command (all three tickets)

**Rule**: Section F, Item F4 -- "Each scan specifies the exact grep command (not just 'run scan')."

**Violation**: SCAN-07 (ASCII-only check) in T1, T2, and T3 provides "Confirm:" prose description
instead of an exact grep command. The engineer contract requires a concrete command so the
engineer can execute it and the verifier (Phase 4b) can reproduce it for cross-check.

**T1 SCAN-07** (04-tickets.md, T1 scan block):
```
SCAN-07  ASCII-only check
  Confirm: no Unicode characters or emoji in the two modified lines.
  AddSeconds(10) and AddSeconds(11) are ASCII-only numeric literals.
```
Missing required command, e.g.: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B113Tests.cs`

**T2 SCAN-07** (04-tickets.md, T2 scan block):
```
SCAN-07  ASCII-only check
  Confirm: all string literals and identifiers in the new file are ASCII-only.
  Keys: "Sim101", "Sim102" -- ASCII.
  Assert messages (if any): ASCII-only.
  No Unicode, no emoji, no curly quotes.
```
Missing required command, e.g.: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B115Tests.cs`

**T3 SCAN-07** (04-tickets.md, T3 scan block):
```
SCAN-07  ASCII-only check
  Confirm: the two modified lines contain only ASCII characters.
  Opening paren, closing paren, and the // DW-B122 comment are all ASCII.
  No Unicode, no emoji, no curly quotes.
```
Missing required command, e.g.: `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs`

**Impact**: Without an exact grep command, the engineer cannot run SCAN-07 as a repeatable gate
and the verifier (Phase 4b) has no anchor command to reproduce independently. This breaks the
three-layer defense chain (ticket contract -> engineer attestation -> verifier cross-check).

**Fix Required**: Architect must add `Command: grep -Pn "[^\x00-\x7F]" <file>` to SCAN-07 in
each of T1, T2, and T3. Expected result: zero results (zero non-ASCII bytes in modified files).

---

## Verdict Per Ticket

### T1 -- Update T_B113_01 TTL Constants
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS
- NT8 Check: PASS
- Test Coverage: PASS
- Scan Checklist Presence (SCAN-01..07 all present): PASS
- Scan Checklist Specificity (F4 -- exact grep): **FAIL** (SCAN-07 missing grep command)
- File Routing: PASS (src/PropTraderTools/Tests/B113Tests.cs)
- **VERDICT: TICKET_REVIEW_FAIL**

### T2 -- New Test: Accepted-State Guard (B115Tests.cs)
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS
- NT8 Check: PASS
- Test Coverage: PASS
- Scan Checklist Presence (SCAN-01..07 all present): PASS
- Scan Checklist Specificity (F4 -- exact grep): **FAIL** (SCAN-07 missing grep command)
- File Routing: PASS (src/PropTraderTools/Tests/B115Tests.cs)
- **VERDICT: TICKET_REVIEW_FAIL**

### T3 -- Parentheses Clarity Edit in TryCleanupReArmedAtmBracket
- Traceability: PASS
- JS Pre-Check: PASS
- CYC Pre-Check: PASS
- NT8 Check: PASS
- Test Coverage: PASS (parentheses-only; no new test required)
- Scan Checklist Presence (SCAN-01..07 all present): PASS
- Scan Checklist Specificity (F4 -- exact grep): **FAIL** (SCAN-07 missing grep command)
- File Routing: PASS (src/PropTraderTools/CopyEngine.cs)
- **VERDICT: TICKET_REVIEW_FAIL**

---

## Overall

**TICKET_REVIEW_FAIL**

**Violation**: F.F4 -- SCAN-07 in T1, T2, and T3 provides "Confirm:" prose instead of an exact
grep command. Required fix: add `Command: grep -Pn "[^\x00-\x7F]" <file>` to SCAN-07 in each
ticket before engineer execution.

**All other checks PASS** (27 of 28 items). No JS rule violations. No spec gaps. No NT8 constraint
violations. No CYC violations. All [Fact] names are specified. All other 6 scans (SCAN-01 through
SCAN-06) carry exact grep commands or explicit structural verification criteria.

**Disposition**: Return to ptt-architect for targeted fix -- add exact grep command to SCAN-07 in
T1, T2, and T3 only. No other changes required.

---

## Cycle 2 Review

**Date**: 2026-08-27
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Cycle**: 2 — Re-review after F.F4 fix
**Prior verdict**: TICKET_REVIEW_FAIL (F.F4: SCAN-07 missing grep command in T1, T2, T3)
**Scope**: Focused re-review — F.F4 fix verification + spot-check for unintended changes

---

### F.F4 Re-Check: SCAN-07 Exact Grep Command

| Ticket | File | SCAN-07 Command Present | Correct Target File |
|--------|------|------------------------|---------------------|
| T1 | B113Tests.cs | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B113Tests.cs` ✅ | B113Tests.cs ✅ |
| T2 | B115Tests.cs | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Tests/B115Tests.cs` ✅ | B115Tests.cs ✅ |
| T3 | CopyEngine.cs | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` ✅ | CopyEngine.cs ✅ |

**F.F4 Result: PASS** — All three tickets now carry an exact `grep -Pn "[^\x00-\x7F]"` command
targeting the correct file. The "Confirm:" prose has been replaced by a concrete, reproducible
command. The three-layer defense chain (ticket contract → engineer attestation → verifier
cross-check) is intact.

---

### Spot-Check: No Unintended Changes

Checked all elements validated as PASS in Cycle 1:

| Element | T1 | T2 | T3 |
|---------|----|----|-----|
| DW Reference header | DW-B121 ✅ | DW-B122 ✅ | DW-B122 (operator clarity) ✅ |
| `[Fact]` names specified | Existing name retained ✅ | Both names specified ✅ | N/A stated ✅ |
| SCAN-01 grep command | Intact ✅ | Intact ✅ | Intact ✅ |
| SCAN-02 grep command | Intact ✅ | Intact ✅ | Intact ✅ |
| SCAN-03 grep command | Intact ✅ | Intact ✅ | Intact ✅ |
| SCAN-04 grep command | Intact ✅ | Intact ✅ | Intact ✅ |
| SCAN-05 grep command | Intact ✅ | Intact ✅ | Intact ✅ |
| SCAN-06 CYC check | Manual count, CYC=1 ✅ | CYC=1 each ✅ | CYC=5 annotation ✅ |
| File routing | B113Tests.cs ✅ | B115Tests.cs ✅ | CopyEngine.cs ✅ |
| Acceptance criteria | Intact ✅ | Intact ✅ | Intact ✅ |

**Spot-check Result: CLEAN** — Only SCAN-07 was modified. No other content changed.
No new violations introduced by the revision.

---

### Cycle 2 Verdict Per Ticket

**T1 -- Update T_B113_01 TTL Constants**
- F.F4 SCAN-07 fix: PASS
- Spot-check: PASS (no unintended changes)
- All Cycle 1 PASS items: confirmed intact
- **VERDICT: TICKET_REVIEW_PASS**

**T2 -- New Test: Accepted-State Guard (B115Tests.cs)**
- F.F4 SCAN-07 fix: PASS
- Spot-check: PASS (no unintended changes)
- All Cycle 1 PASS items: confirmed intact
- **VERDICT: TICKET_REVIEW_PASS**

**T3 -- Parentheses Clarity Edit in TryCleanupReArmedAtmBracket**
- F.F4 SCAN-07 fix: PASS
- Spot-check: PASS (no unintended changes)
- All Cycle 1 PASS items: confirmed intact
- **VERDICT: TICKET_REVIEW_PASS**

---

### Cycle 2 Overall

**TICKET_REVIEW_PASS**

All 28 checks now PASS (27 from Cycle 1 + F.F4 now resolved). No violations remain.
Safe to spawn ptt-engineer for B115 T1, T2, T3.
