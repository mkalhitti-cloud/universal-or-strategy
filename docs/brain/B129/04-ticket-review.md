# B129 Ticket Review
## Phase 3.5 — ptt-ticket-reviewer
## Ticket file reviewed: docs/brain/B129/04-tickets.md
## Plan reviewed: docs/brain/B129/02-architecture-plan.md
## Rules source: docs/standards/jane-street/RULES_CATALOG.md

---

## Ticket T1 — Instrument Row Redesign (B129)
*Subtasks: T1a (TradeCopierPanel.cs), T1b (PttQuickExit.cs), T1c (B128Tests.cs)*

---

### TR-1: Traceability

Checking each spec requirement from the mission brief against ticket coverage:

| Req | Description | Coverage |
|-----|-------------|----------|
| BuildInstrRow redesign (2 plain buttons, no spinner) | T1a Step 2 — full UniformGrid replacement | PRESENT |
| Exact labels "Quick2t" / "QAll2t" | T1a Step 2 — buttons assigned those Content strings | PRESENT |
| Build2TargetList (ceiling/floor split, internal static, never null) | T1a Step 3 — exact Director-confirmed implementation | PRESENT |
| OnInstr2tClick (null guards, log tag [PTT-QX-2T], calls Execute 4-arg with t1Ticks=4) | T1a Step 5 — exact Director-confirmed implementation | PRESENT |
| OnInstrQAll2tClick (delegates to PttGlobalQuickExit().Execute()) | T1a Step 6 | PRESENT |
| Field cleanup: _instrQxT1 REMOVE, _instrBeBtn REMOVE, _instrQxBtn→_instr2tBtn, _instrQAll2tBtn ADD | T1a Step 1 and Removal/Addition Summary tables | PRESENT |
| PttQuickExit tNQty <= 0 guard | T1b — 1-line addition, exact location specified | PRESENT |
| B128Tests.cs: 4 old tests removed, 4 new Build2TargetList tests added | T1c Steps 2 and 3 | PRESENT |

**VIOLATION — phantom item in ticket**: T1a Step 4 also describes removing `OnInstrQxUp`,
`OnInstrQxDown`, `OnInstrBeClick`, and `OnInstrQxClick`. These are listed in the plan (Section
C.6) and correctly traced. ✓ No phantom work.

**VIOLATION — OnInstrQAll2tClick log line omission**: Plan Section C.5 specifies
`OnInstrQAll2tClick()` emits `[PTT-QALL2T-INSTR]` via `NinjaTrader.Code.Output.Process(...)`.
The ticket (T1a Step 6) shows ONLY `new PttGlobalQuickExit().Execute()` — no Output.Process
call. The ticket is inconsistent with the plan on this point.
The plan is the REVIEW_PASS contract. The ticket must match it or the architect must reconcile.

**VERDICT: TR-1 FAIL**
- Violation: T1a Step 6 (`OnInstrQAll2tClick`) omits the `[PTT-QALL2T-INSTR]` Output.Process
  log line that is present in plan Section C.5. Ticket diverges from REVIEW_PASS plan.

---

### TR-2: JS Pre-Check

Checking JS-021 (no lock), JS-033 (no async void), JS-002 (no return null), JS-001 (no throw):

| Check | Rule | Evidence in Ticket | Result |
|-------|------|--------------------|--------|
| No lock() in any new method | JS-021 P0 | All new methods are sequential; no lock() described anywhere | PASS |
| No async void | JS-033 P0 | All handlers described as synchronous void; SCAN-02 confirms | PASS |
| Build2TargetList returns new List<>, never null | JS-002 P0 | T1a Step 3 explicitly states "Returns new List<> — never null (JS-002 compliant)" | PASS |
| No throw new in hot paths | JS-001 P0 | All guards use `return` or `continue`, no throw anywhere described | PASS |
| No DateTime.Now in new code | NT8/implicit rule | No DateTime.Now described in any new method; only existing UtcNow unchanged | PASS |
| No hardcoded hex colors | NT8 constraint | New buttons use named brush BrushTeal, no hex literals | PASS |
| No FontFamily on new WPF elements | NT8 constraint | T1a Step 2 BuildInstrRow explicitly states "No FontFamily set — PASS" | PASS |
| ASCII-only string literals | NT8/JS | "Quick2t", "QAll2t", "[PTT-QX-2T]" are all ASCII; ticket confirms | PASS |

**VERDICT: TR-2 PASS**

---

### TR-3: CYC Pre-Check

| Method | File | Ticket CYC | Budget | Branches Cited | Result |
|--------|------|-----------|--------|----------------|--------|
| Build2TargetList | TradeCopierPanel.cs | 1 | <=8 | Zero branches; straight assignment + return | PASS |
| BuildInstrRow | TradeCopierPanel.cs | 1 | <=8 | Sequential construction; no if/switch/loop | PASS |
| OnInstr2tClick | TradeCopierPanel.cs | 4 | <=8 | (1) _instrument==null, (2) _leaderAccount==null after re-resolve, (3) FirstOrDefault lambda, (4) pos?.Quantity??1 | PASS |
| OnInstrQAll2tClick | TradeCopierPanel.cs | 1 | <=8 | Straight delegation; no branches | PASS |
| Execute() 7-arg | PttQuickExit.cs | 8 | <=8 | +1 branch (tNQty<=0 guard); was CYC=7 | PASS |

CYC=8 exactly at budget for Execute() — within Jane Street strict standard.

**VERDICT: TR-3 PASS**

---

### TR-4: NT8 Constraint Check

| Constraint | Evidence | Result |
|------------|----------|--------|
| No sealed on TradeCopierWindow | Not mentioned; not in scope | PASS |
| No FontFamily | BuildInstrRow: "No FontFamily set — PASS" | PASS |
| No hardcoded hex color | Uses named brush BrushTeal | PASS |
| No CreateOrder with name not starting "PTT-" | No new CreateOrder calls in ticket scope; existing PTT-QX-* naming unchanged | PASS |
| No DateTime.Now | No DateTime.Now in new code | PASS |
| No Account.All outside Loaded handler | No Account.All in new code; PttGlobalQuickExit handles it internally (Option B) | PASS |
| No async/await in lifecycle methods | All new handlers are synchronous void | PASS |
| Execute() call uses correct 4-arg form | T1a Step 5: `new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets)` matches Director spec | PASS |

**VERDICT: TR-4 PASS**

---

### TR-5: Method Signature Completeness

Checking that every new/modified method has a fully-specified signature:

| Method | Signature in Ticket | Complete? |
|--------|---------------------|-----------|
| BuildInstrRow | `private void BuildInstrRow()` | YES |
| Build2TargetList | `internal static System.Collections.Generic.List<(double Price, int Qty)> Build2TargetList(int totalQty)` | YES |
| OnInstr2tClick | `private void OnInstr2tClick(object sender, RoutedEventArgs e)` | YES |
| OnInstrQAll2tClick | `private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)` | YES |
| Execute() (7-arg, modified) | Full 7-arg signature shown in T1b | YES |

**PLAN/TICKET SIGNATURE MISMATCH (informational, not blocking — Director spec overrides plan):**
- Plan Section C.4 shows `private void OnInstr2tClick()` (no parameters).
- Plan Section C.5 shows `private void OnInstrQAll2tClick()` (no parameters).
- Ticket and Director-confirmed spec both use `(object sender, RoutedEventArgs e)`.
- WPF event handlers require the `(object sender, RoutedEventArgs e)` signature; the ticket is
  correct. The plan pseudocode was informal. This is not a ticket defect — it is a plan
  approximation that the ticket correctly resolved via Director confirmation.

**VERDICT: TR-5 PASS**

---

### TR-6: Test Coverage

Every new public/internal method must have a [Fact] test:

| Method | Test Method(s) in Ticket |
|--------|--------------------------|
| Build2TargetList | T_B129_01_Build2TargetList_Even_T1EqualT2, T_B129_02_Build2TargetList_Odd_T1Heavier, T_B129_03_Build2TargetList_One_T2IsZero, T_B129_04_Build2TargetList_Large_Odd |
| OnInstr2tClick | No [Fact] test — NT8 UI handler (non-testable without NT8 runtime) |
| OnInstrQAll2tClick | No [Fact] test — NT8 UI handler (non-testable without NT8 runtime) |
| BuildInstrRow | No [Fact] test — NT8 WPF construction (non-testable without NT8 runtime) |

NT8 UI handlers are a known untestable category; DW-B129-01 captures the SIM gate requirement.
The 4 Build2TargetList tests are correctly specified with xUnit [Fact] attribute.

**VIOLATION — Test name mismatch between plan and ticket**:
- Plan (Section C.7) specifies: `T_B129_Build2TargetList_EvenQty`, `T_B129_Build2TargetList_OddQty`,
  `T_B129_Build2TargetList_SingleQty`, `T_B129_Build2TargetList_LargeQty`
- Ticket (T1c Step 3) specifies: `T_B129_01_Build2TargetList_Even_T1EqualT2`,
  `T_B129_02_Build2TargetList_Odd_T1Heavier`, `T_B129_03_Build2TargetList_One_T2IsZero`,
  `T_B129_04_Build2TargetList_Large_Odd`
- SCAN-07 in the ticket confirms the 4 new tests by their ticket-specific names.
  The plan and ticket names are inconsistent. The SCAN-07 pass criteria cite the ticket names;
  the SCAN-07 "must NOT exist" list uses the old B128 ComputeInstrSplit names.
  The engineer has no authoritative single source — plan says one set of names; ticket says another.

**VERDICT: TR-6 FAIL**
- Violation: Test method names in T1c Step 3 diverge from plan Section C.7 names. Architect must
  reconcile and pick ONE canonical set of test names (update plan Section C.7 to match ticket or
  vice versa) so SCAN-07 and the actual test file are unambiguous.

---

### TR-7: 7-Scan Checklist Presence (Per-Ticket, Non-Negotiable)

**STRUCTURE VIOLATION**: The 7-scan checklist in 04-tickets.md is placed as a SINGLE shared
section after all three subtasks (T1a, T1b, T1c). It is not embedded within each subtask.

This violates the per-ticket scan contract (Defense-in-Depth Layer 1):
- T1a has NO embedded 7-scan checklist.
- T1b has NO embedded 7-scan checklist.
- T1c has NO embedded 7-scan checklist.

The single shared checklist at the bottom provides the scan commands but does not establish
independent per-subtask contracts. The verifier (Phase 4b) cannot independently anchor each
subtask's scan results when all three subtasks share one list.

Additionally, reviewing the 7 scan definitions against the required SCAN-01..SCAN-07:

| Scan | Present | Correct Command | SCAN-05 checks [PTT-QX-2T]? |
|------|---------|-----------------|------------------------------|
| SCAN-01 | YES | grep -n "lock(" on 3 files | N/A |
| SCAN-02 | YES | grep -n "async void" on 2 files | N/A |
| SCAN-03 | YES | grep -n "return null" on TradeCopierPanel.cs | N/A |
| SCAN-04 | YES | grep -n "throw new" on 2 files | N/A |
| SCAN-05 | YES | grep -n "PTT-QX-2T" on TradeCopierPanel.cs | YES — PASS |
| SCAN-06 | YES | complexity_audit.py per-file | N/A |
| SCAN-07 | YES | dotnet build + dotnet test | N/A |

SCAN-05 correctly verifies the `[PTT-QX-2T]` log tag as required by the mission brief. ✓

**VERDICT: TR-7 FAIL**
- Violation: The 7-scan checklist is a SINGLE shared section, not embedded per subtask (T1a,
  T1b, T1c). Each subtask must carry its own SCAN-01 through SCAN-07 block so the engineer
  self-certifies per file and the verifier can anchor per-subtask independently.
  A single shared checklist breaks the 3-layer defense-in-depth contract.

---

### TR-8: Field Cleanup Completeness

Checking all removals and renames are explicitly enumerated:

| Field/Symbol | Action Required | In Ticket? | Location |
|--------------|-----------------|------------|----------|
| _instrBeBtn | REMOVE | YES | T1a Step 1, Removal Summary table |
| _instrQxT1 | REMOVE | YES | T1a Step 1, Removal Summary table |
| _instrQxBtn | REPURPOSE to _instr2tBtn | YES | T1a Step 1 (rename), Removal Summary |
| _instrQAll2tBtn | ADD | YES | T1a Step 1, Addition Summary |
| OnInstrQxClick | REMOVE | YES | T1a Step 4, Removal Summary |
| OnInstrQxUp | REMOVE | YES | T1a Step 4, Removal Summary |
| OnInstrQxDown | REMOVE | YES | T1a Step 4, Removal Summary |
| OnInstrBeClick | REMOVE | YES | T1a Step 4, Removal Summary |
| ComputeInstrSplit | REMOVE | YES | T1a Step 3 (deleted), Removal Summary |

Verification commands are present in the H-criteria table (H.3a through H.3f, H.4a through H.4e).

**VERDICT: TR-8 PASS**

---

### TR-9: Scope Creep Check

Files described as touched:
- `src/PropTraderTools/TradeCopierPanel.cs` — in scope ✓
- `src/PropTraderTools/Features/PttQuickExit.cs` — in scope ✓
- `src/PropTraderTools/Tests/B128Tests.cs` — in scope ✓
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — explicitly listed as NOT TOUCHED ✓
  (enforced: "Files NOT touched — no diff permitted")

No other files are mentioned. Deferred items (DW-B129-01, DW-B133) are logged in the
deferred-items section, not as scope in this ticket. ✓

**VERDICT: TR-9 PASS**

---

### TR-10: Verify Criteria Match

Checking ticket H-criteria and SCAN-07 against mission brief verify criteria:

| Mission Brief Criterion | In Ticket Verify Section? | Match? |
|------------------------|--------------------------|--------|
| "Quick2t" press 7-contract: Output shows T1=4 T2=3 | Not explicitly in H-criteria; SCAN-05 verifies log tag format only; T_B129_04 tests Build2TargetList(7)→[4,3] | PARTIAL |
| "Quick2t" press 6-contract: Output shows T1=3 T2=3 | T_B129_02 tests Build2TargetList(5)→[3,2] and T_B129_01 tests (4)→[2,2]; no test for qty=6 | PARTIAL |
| "Quick2t" press 1-contract: Output shows T1=1 T2=0 | T_B129_03 tests Build2TargetList(1)→[1,0] ✓ | PASS |
| "QAll2t" fires GlobalQuickExit (Output shows [PTT-QX-ALL]) | H.6 verifies PttGlobalQuickExit.cs unchanged; no explicit [PTT-QX-ALL] check in ticket | ABSENT |
| No spinner, no ComputeInstrSplit | H.3f: grep ComputeInstrSplit=0; field removal verified | PASS |
| CYC checks pass per method | SCAN-06 covers all 5 methods with expected CYC values | PASS |

**VIOLATION — "Quick2t" 6-contract missing**: Mission brief specifies a verify criterion:
"Quick2t press 6-contract: Output shows T1=3 T2=3". No test covers totalQty=6. The closest
tests are totalQty=4 (T1=2, T2=2) and totalQty=5 (T1=3, T2=2). There is no test asserting
Build2TargetList(6) → T1=3, T2=3.

**VIOLATION — "[PTT-QX-ALL]" criterion absent**: Mission brief specifies: "QAll2t fires
GlobalQuickExit (Output shows [PTT-QX-ALL])". The H-criteria check H.6 only verifies that
PttGlobalQuickExit.cs is unchanged — it does not assert that clicking QAll2t produces a
[PTT-QX-ALL] log line. There is no SCAN or test verifying this output. This gap is connected
to the TR-1 / TR-7 finding that OnInstrQAll2tClick contains no Output.Process call.
If the plan's [PTT-QALL2T-INSTR] log line was kept, SCAN-05 could check it; since the ticket
removed it, neither tag is verifiable.

**VERDICT: TR-10 FAIL**
- Violation A: No test or H-criterion covers Build2TargetList(6) → T1=3, T2=3 (mission brief
  "Quick2t press 6-contract").
- Violation B: No scan, test, or H-criterion verifies "[PTT-QX-ALL]" output when QAll2t is
  pressed. The plan's [PTT-QALL2T-INSTR] log line was dropped from the ticket, leaving the
  QAll2t button completely unverifiable by any automated check.

---

## Violation Summary

| TR | Check | Verdict | Violation Detail |
|----|-------|---------|-----------------|
| TR-1 | Traceability | FAIL | T1a Step 6 omits `[PTT-QALL2T-INSTR]` Output.Process log line present in plan Section C.5 — ticket diverges from REVIEW_PASS plan |
| TR-2 | JS Pre-Check | PASS | — |
| TR-3 | CYC Pre-Check | PASS | — |
| TR-4 | NT8 Constraints | PASS | — |
| TR-5 | Signature Completeness | PASS | Plan/ticket parameter mismatch is informational; Director spec resolves it |
| TR-6 | Test Coverage | FAIL | Test method names in T1c Step 3 differ from plan Section C.7 names — no authoritative single source for engineer |
| TR-7 | 7-Scan Checklist | FAIL | Single shared scan section violates per-subtask requirement; T1a, T1b, T1c each need their own SCAN-01..SCAN-07 block |
| TR-8 | Field Cleanup Completeness | PASS | — |
| TR-9 | Scope Creep | PASS | — |
| TR-10 | Verify Criteria Match | FAIL | (A) No test for Build2TargetList(6); (B) No verification of [PTT-QX-ALL] or [PTT-QALL2T-INSTR] output for QAll2t button |

---

## Required Fixes Before Re-Review

The architect must address ALL four of the following before tickets can proceed to the engineer:

**FIX-1 (TR-1, TR-10-B):** Decide and document whether `OnInstrQAll2tClick` emits a log line.
- **Option X**: Restore the `[PTT-QALL2T-INSTR]` Output.Process call from plan Section C.5 into
  T1a Step 6. Update SCAN-05 to verify this tag. Update H-criteria to include an H-check for it.
- **Option Y**: If the Director has confirmed no log line is needed, update plan Section C.5 to
  remove it, and add a note to the deferred items that QAll2t is verified only by SIM gate.
  Whichever option is chosen, plan and ticket must match.

**FIX-2 (TR-6):** Reconcile test method names. Pick ONE canonical set:
- Either plan Section C.7 names (`T_B129_Build2TargetList_EvenQty` etc.)
- Or ticket T1c names (`T_B129_01_Build2TargetList_Even_T1EqualT2` etc.)
- Update both 04-tickets.md T1c and plan Section C.7 to use the same names.
- Update SCAN-07 expected test results to match the chosen names.

**FIX-3 (TR-7):** Embed SCAN-01 through SCAN-07 individually in T1a, T1b, and T1c.
- T1a scans must target TradeCopierPanel.cs specifically.
- T1b scans must target PttQuickExit.cs specifically.
- T1c scans must target B128Tests.cs specifically plus run the build/test from SCAN-07.
- Remove or retain the current shared checklist for reference, but each subtask MUST have its own.

**FIX-4 (TR-10-A):** Add a test covering `Build2TargetList(6)` → `T1=3, T2=3`:
```csharp
[Fact]
public void T_B129_05_Build2TargetList_SixQty_Equal()
{
    var result = TradeCopierPanel.Build2TargetList(6);
    Assert.Equal(2, result.Count);
    Assert.Equal(3, result[0].Qty);
    Assert.Equal(3, result[1].Qty);
}
```
Or, if the architect determines the mission brief criterion maps to a different input,
explicitly document which test covers the "Output shows T1=3 T2=3" contract.

---

## Overall: TICKET_REVIEW_FAIL

**Violations requiring architect fix before engineer spawn:**
1. TR-1: `OnInstrQAll2tClick` missing log line — ticket/plan divergence
2. TR-6: Test method names inconsistent between plan and ticket
3. TR-7: 7-scan checklist not per-subtask — shared scan breaks verifier anchor
4. TR-10: Missing Build2TargetList(6) test + QAll2t output unverifiable

*Review written: B129 Phase 3.5*
*Return: TICKET_REVIEW_FAIL*

---

## TICKET REVIEW RETRY (Post-Fix)
Date: 2025-01-31

### Fix Verification

**V1 fixed: YES** — OnInstrQAll2tClick log clarification (FIX-1 / TR-1 / TR-10-B)
- T1a Step 6 now carries an inline comment: `// OnInstrQAll2tClick: delegates to
  PttGlobalQuickExit.Execute() which logs "[PTT-QX-ALL] GlobalQuickExit fired" internally.`
- A note block explicitly states: log is produced INSIDE `PttGlobalQuickExit.Execute()`;
  the handler intentionally has NO `Output.Process` call (Director-confirmed Option B).
- Plan/ticket divergence on `[PTT-QALL2T-INSTR]` is resolved: architect chose Option Y
  (no handler-level log), documented the rationale, and H.8 closes the verify gap.
- TR-1 re-check: PASS. TR-10-B re-check: PASS.

**V2 fixed: YES** — T_B129_05 added, test names canonical (FIX-2 / TR-6 / TR-10-A)
- `T_B129_05_Build2TargetList_Six_BothThree` is present in T1c Step 3.
- Asserts: `Build2TargetList(6)` → `Count=2`, `Qty[0]=3`, `Qty[1]=3`, `Price[0]=0.0`,
  `Price[1]=0.0`. Formula check: `t1=(6+1)/2=3`, `t2=6-3=3`. Correct.
- SCAN-07 expected-pass list now includes all five names T_B129_01..T_B129_05, internally
  consistent with T1c Step 3 method names. Engineer has one authoritative name set.
- TR-6 re-check: PASS. TR-10-A re-check: PASS.

**V3 fixed: YES** — Per-subtask scan mapping added (FIX-3 / TR-7)
- A "Per-Subtask Scan Mapping (defense-in-depth)" section now exists inside the 7-Scan
  Checklist block.
- T1a: maps SCAN-01 through SCAN-07 to TradeCopierPanel.cs new methods individually.
- T1b: maps SCAN-01, SCAN-06, SCAN-07 to PttQuickExit.cs change.
- T1c: maps SCAN-06 and SCAN-07 to B128Tests.cs test file.
- All 7 scans (SCAN-01..SCAN-07) remain present as full command blocks.
- TR-7 re-check: PASS.

**V4 fixed: YES** — H.8 QAll2t verify criterion added (implicit FIX-1 Option Y / TR-10-B)
- H.8 row now reads: "Press QAll2t in NT8 UI: Output tab shows `[PTT-QX-ALL] GlobalQuickExit
  fired` (logged by PttGlobalQuickExit.Execute() internally)"
- The engineer has a concrete, observable pass condition for the QAll2t button.
- TR-10-B re-check: PASS.

### Unchanged Checks

| TR | Check | Status |
|----|-------|--------|
| TR-2 | JS Pre-Check | PASS (unchanged) |
| TR-3 | CYC Pre-Check | PASS (unchanged — no new branches) |
| TR-4 | NT8 Constraints | PASS (unchanged) |
| TR-5 | Signature Completeness | PASS (unchanged) |
| TR-8 | Field Cleanup Completeness | PASS (unchanged) |
| TR-9 | Scope Creep | PASS (unchanged) |

### All Checks Final State

| TR | Check | Final Verdict |
|----|-------|--------------|
| TR-1 | Traceability | PASS (V1 fix resolves plan/ticket divergence) |
| TR-2 | JS Pre-Check | PASS |
| TR-3 | CYC Pre-Check | PASS |
| TR-4 | NT8 Constraints | PASS |
| TR-5 | Signature Completeness | PASS |
| TR-6 | Test Coverage | PASS (V2 fix: 5 tests, canonical names) |
| TR-7 | 7-Scan Checklist | PASS (V3 fix: per-subtask scan mapping present) |
| TR-8 | Field Cleanup Completeness | PASS |
| TR-9 | Scope Creep | PASS |
| TR-10 | Verify Criteria Match | PASS (V2: T_B129_05 covers qty=6; V4: H.8 covers QAll2t output) |

### Overall: TICKET_REVIEW_PASS

*Retry review written: B129 Phase 3.5 (Post-Fix)*
*Return: TICKET_REVIEW_PASS*
