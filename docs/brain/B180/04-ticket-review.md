# B180 Ticket Review
# Block: B180 -- DW-B180-BE-SWAP-ALL
# Reviewer: ptt-ticket-reviewer (Phase 3.5)
# Source tickets: docs/brain/B180/04-tickets.md
# Reference plan: docs/brain/B180/02-architecture-plan.md (REVIEW_PASS)
# Reference spec: docs/brain/B180-orchestrator-prompt.md
# Reference rules: docs/standards/RULES_CATALOG.md

---

## Executive Summary

**TICKET_REVIEW_FAIL**

Two violations require architect correction before engineer handoff:

1. **Check 4 (SEARCH block mismatch) -- B180-1 Step 4:** The `apply_diff` SEARCH block for
   `OnGlobalBeClick` (`:start_line:1349`) omits the actual first line of the comment block
   (`// B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.`). The SEARCH content begins
   with line 1350 content while claiming line 1349 as start. This `apply_diff` call will fail
   at runtime with a "content not found" error.

2. **Check 10 (test coverage) -- B180-3 T-B180-T1-01 and T-B180-T1-04:** Tests use
   `Assert.True(true)` placeholder assertions. The spec requires explicit behavioral Assert
   statements ("pure C# seam-based") for every test. These two tests contain no behavioral
   assertion and would produce a false-green VERIFY_PASS for those test cases.

All other checks PASS. Details below.

---

## Per-Ticket Results

---

### B180-1 -- Additive Rewire

**Check 1 (Required Sections Present):** PASS
All required sections present: Spec Requirements, Dependency, Files Changed, Method Signatures,
Implementation Instructions, 7-Scan Checklist, Verify Steps.

**Check 2 (Traceability):** PASS
All ticket items map to orchestrator prompt requirements:
- Per-chart BE button -> orchestrator "Per-chart BE button" section
- BE ALL button -> orchestrator "BE ALL button" section
- SnapshotBeTargetsPublic -> orchestrator "SnapshotBeTargetsPublic" note
- Feature flag gate -> orchestrator "What is kept"
- Flat guard -> orchestrator "Flat guard"
- Follower skip -> orchestrator "BE ALL button"
- B180-1 additive-only -> orchestrator "SCOPE GATE"
- Entry price formula -> orchestrator "Entry price calculation"
No phantom work. No missing spec items for B180-1 scope.

**Check 3 (Staged Order):** PASS
B180-1 Dependency: NONE (first ticket). No removal of any existing symbols -- confirmed by
Step 6 "Dead code note" which explicitly leaves BeState, _beState, UpdateBeVisuals, etc. intact.

**Check 4 (apply_diff SEARCH Block Accuracy):** FAIL
**VIOLATION -- B180-1 Step 4 (OnGlobalBeClick SEARCH block)**

Ticket SEARCH block claims `:start_line:1349` and begins with:
```
        // Idle->Armed: arm all pending; if at least one slot entered Armed state, turn amber.
```

Actual TradeCopierPanel.cs at line 1349 (verified from source):
```
        // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
```

The first line of the comment block (`// B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.`)
is ABSENT from the SEARCH content. The SEARCH content corresponds to lines 1350-1377, not 1349-1377.
At runtime, `apply_diff` will fail with a content-not-found error because line 1349 contains
`// B40:...` not `// Idle->Armed:...`.

**Fix required:** The SEARCH block must be corrected to include line 1349 as its first line:
```
:start_line:1349
-------
        // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
        // Idle->Armed: arm all pending; if at least one slot entered Armed state, turn amber.
        // Armed->Idle (manual disarm): loop Account.All and DisarmPendingBe for each; turn purple.
        // JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
        // DW-B72-02: _globalBeState removed. IsPendingSlotsEmpty() is the truth source.
        // Idle (slots empty)  -> arm all, then show Armed visual if slots were taken.
        // Armed (slots exist) -> disarm all, show Idle visual.
        // Both panels read the same CopyEngine singleton so state is automatically shared.
        private void OnGlobalBeClick(object sender, RoutedEventArgs e)
        { ... through line 1377 ... }
```

B180-1 Step 5 (OnBeClick SEARCH block): PASS
- `:start_line:1474` content verified: source line 1474 = `// B33 T7 -- OnBeClick:...` matches
  first line of SEARCH block exactly.
- Block end at line 1534 = closing brace of ExecuteBeArmed. Content verified against source. Match.

B180-1 Step 3 (CopyEngine SnapshotBeTargetsPublic SEARCH block): PASS
- `:start_line:6977` content verified: source lines 6977-6981 match SEARCH block content.
  Line 6978 = `return nativeTargets.Count > 0...`, line 6979 = `}`, line 6981 = `// CCN<=6:...`
  All match.

**Check 5 (New File Completeness):** PASS
PttBeSwapOne.cs:
- `using System;` present. `using NinjaTrader.Cbi;` present. No NinjaTrader.NinjaScript import
  needed (references are fully qualified inline). Acceptable.
- `internal static class PttBeSwapOne` present.
- Guards in correct order: feature flag (1), instr null (2), tickSize ?. (3), tickSize?? (3 cont),
  tickSize<=0 (4), flat (5).
- No `lock()`. ASCII-only strings verified.

PttBeSwapAll.cs:
- `using System;` present. `using NinjaTrader.Cbi;` present.
- `internal static class PttBeSwapAll` present.
- Guards in correct order: feature flag (1), Account.All loop (2), follower skip (3), flat skip (4).
- `private static void ExecuteOne`: instr null (1), tickSize (2), swap (3).
- No `lock()`. ASCII-only strings verified.

**Check 6 (JS Pre-check):** PASS
- No `lock()` in any REPLACE block or new file content.
- No `throw` in any new code. All failure paths use early `return`.
- No `DateTime.Now` in any REPLACE block or new file.
- All string literals are ASCII-only: `"[BE] swap: "`, `"[BE-ALL] swap: "`,
  `"[BE-ALL] Blocked: Break Even requires Pro tier"`, `"[BE-ALL] PttBeSwapAll: firing buf="`,
  `"Break Even requires Pro tier"`, `": BE flat skip"`. All straight double-quotes, no Unicode.
- REPLACE blocks for OnBeClick and OnGlobalBeClick contain no lock(), no throw, no DateTime.Now.

**Check 7 (CYC Pre-check):** PASS (with documentation note)
Manual McCabe count using RULES_CATALOG JS-013:

PttBeSwapOne.Execute:
  base(1) + if(!BreakEven)(1) + if(instr==null)(1) + ?.(1) + ??(1)
  + if(tickSize<=0)(1) + if(pos==null||qty==0): if(1) + ||(1) = **CYC=8**
  Ticket states CYC=7. Actual McCabe count per RULES_CATALOG §JS-013 yields 8.
  CYC=8 is exactly the limit (<=8). Method is COMPLIANT. The CYC annotation in the
  ticket is a minor documentation error (off-by-one) but does NOT cause a violation
  because the method itself satisfies the <=8 constraint.

PttBeSwapAll.Execute:
  base(1) + if(!BreakEven)(1) + foreach(1) + if(engine!=null&&IsFollower): if(1) + &&(1)
  + foreach(1) + if(pos==null||qty==0): if(1) + ||(1) = CYC=8.
  Ticket states CYC=6. Actual McCabe = 8. CYC=8 is exactly the limit. COMPLIANT.
  Documentation note only.

PttBeSwapAll.ExecuteOne:
  base(1) + if(instr==null)(1) + ?.(1) + ??(1) + if(tickSize<=0)(1) = CYC=5.
  Ticket states CYC=5. Match. COMPLIANT.

OnBeClick (replacement):
  base(1) + if(_instrument==null)(1) + ??(1) + if(_leaderAccount==null)(1) = CYC=4.
  Ticket states CYC=4. Match. COMPLIANT.
  Note: ticket method signature section previously states CYC=4 but one inline note in the
  REPLACE block comment says "CYC=4: (1)...(2)...(3) PttBeSwapOne.Execute" with 3 items
  listed -- the ?? operator on _leaderAccount is decision point (2) and leader-null is (3),
  so the bracket mapping has 4 elements total. CYC=4. COMPLIANT.

OnGlobalBeClick (replacement):
  base(1) = CYC=1 (straight-line delegation). Ticket states CYC=1. Match. COMPLIANT.

SnapshotBeTargetsPublic:
  Expression body, no branches. CYC=1. Ticket states CYC=1. Match. COMPLIANT.

All methods <= CYC=8. No violations.

**Check 8 (NT8 Constraints):** PASS
- No `AtmStrategyCreate` in any new or changed code.
- No `async`/`await` in any new or changed method.
- No `FontFamily` introduced.
- No `sealed` on TradeCopierWindow (not touched in B180-1).
- Account.All used in PttBeSwapAll.Execute -- this is called from OnGlobalBeClick which runs
  on the WPF UI thread (button click event), which is after Loaded. Acceptable per NT8-002
  (called from Subscribe/Loaded path context via UI thread).

**Check 9 (7-Scan Checklist Presence):** PASS (with WARN on canonical numbering)
B180-1 scan table has 7 distinct scan IDs (SCAN-01 through SCAN-07). All 7 appear.
WARN (not FAIL): The scan numbering diverges from RULES_CATALOG §4 canonical numbering:
- Canonical SCAN-06 = dotnet build + dotnet test. In B180-1 this appears in Verify Steps
  but NOT in the 7-scan checklist table. SCAN-06 in the ticket = FontFamily check.
- Canonical SCAN-07 = structural verification. In B180-1 SCAN-07 = ArmPendingBe leak check.
  The canonical structural intent IS covered (the Verify Steps grep sequence covers it).
The substance of all 7 canonical checks is addressed. The verifier (Ph4b) is reminded to run
canonical SCAN-06 (dotnet build+test) independently per RULES_CATALOG §4 even though it appears
in Verify Steps rather than the scan table. This is a WARN, not a FAIL, because all canonical
check substance is present in the ticket.

**Check 12 (ptt-sync.ps1 invocation):** PASS
Step 6 and Verify Steps both include `powershell -File .\scripts\ptt-sync.ps1`.

**B180-1 VERDICT: TICKET_REVIEW_FAIL** (Check 4: OnGlobalBeClick SEARCH block missing line 1349)

---

### B180-2 -- Dead Code Removal

**Check 1 (Required Sections Present):** PASS
All required sections present: Spec Requirements, Dependency, Files Changed, Method Signatures
(pure removal noted), Implementation Instructions, 7-Scan Checklist, Verify Steps.

**Check 2 (Traceability):** PASS
All removal items trace to orchestrator prompt "What is removed" section and architecture plan
Section 6A/6B/6C/6D. No phantom work items.

**Check 3 (Staged Order):** PASS
Dependency stated: "VERIFY_PASS B180-1 is required. B180-2 MUST NOT START until ptt-verifier has
issued VERIFY_PASS for B180-1." Explicit and correct.

**Check 4 (apply_diff SEARCH Block Accuracy):** PASS
SEARCH blocks verified:
- Step 1 (`_pendingBeSlots`, line 268): source line 268 = `// B27 -- Pending BE slot dictionary...`
  matches SEARCH first line. MATCH.
- Step 2 (`_pendingBePriceRetry/Attempts`, line 316): source line 316 = `// B159 DW-B159-FM-B:...`
  matches SEARCH first line. MATCH.
- Step 3 (events, line 449): source line 449 = `// B10 T2 -- Pending BE fired notification...`
  matches SEARCH first line. MATCH.
- Step 4 (`GlobalBeAllDisarmed`, line 487): source line 487 = `// HOTFIX-BEALL-DISARM-SYNC-01:...`
  matches SEARCH first line. MATCH.
- Step 8A (Detach region, line 576): source line 576 = `_engine.PendingBeFired -= OnPendingBeFiredDispatch;`
  matches. Full block 576-586 verified against source. MATCH.
- Step 8B (SubscribeEngineEvents, line 809): source line 809 = `_engine.PositionStateChanged += OnPositionStateChanged;`
  matches. Full block 809-815 verified. MATCH.
- Step 11 (BeState enum, line 390): SEARCH `:start_line:390` first line is
  `// Armed -> price crosses entry+buffer -> MoveStopToBreakEven once -> Idle`
  Source line 390: `// Armed -> price crosses entry+buffer -> MoveStopToBreakEven once -> Idle` MATCH.
- Step 11 (`_beState` field, line 242): SEARCH first line `// B12 T1 -- BE 3-state FSM...`
  Source line 242: `// B12 T1 -- BE 3-state FSM (UI-thread-only; no volatile)` MATCH.
- Steps 5/6/7/13 (method removals with approximate line numbers): These steps explicitly instruct
  the engineer to read exact content before applying each diff. The `apply_diff` SEARCH blocks for
  TryTriggerBeRecovery, MoveStopToBreakEven, ArmPendingBe, etc. are provided as templates with
  "*(Engineer: include the full method body through its closing `}` ... in the SEARCH block)*"
  guidance -- the engineer is expected to read and confirm exact lines. This is an acceptable
  pattern for large-scale removals where exact content cannot be pinned without parsing the full
  method body.

**Check 5 (New File Completeness -- PttGlobalBreakEven replacement):** PASS
Replacement file content shown in Step 9:
- `internal sealed class PttGlobalBreakEven` present.
- `private volatile int _globalBeBuffer = 0` present (volatile int = lock-free scalar, JS-021).
- `internal int GlobalBeBuffer => _globalBeBuffer` present.
- `IncrementBuffer()` and `DecrementBuffer()` present (call `RaiseBeBufferChanged`).
- No `lock()`, no `throw`, no `DateTime.Now`, ASCII-only. COMPLIANT.
- No `using` directives -- acceptable because `CopyEngine.Instance` is in the same namespace
  (`PropTraderTools`) and no external type references are needed in this thin holder.

**Check 6 (JS Pre-check):** PASS
- No `lock()` in any REPLACE block or replacement file content.
- No `throw` in replacement file.
- No `DateTime.Now`.
- All string literals and comments in replacement PttGlobalBreakEven.cs are ASCII-only.
- PttBreakEven.cs deletion has no JS rule implications.

**Check 7 (CYC Pre-check):** PASS
PttGlobalBreakEven retained methods:
- `IncrementBuffer`: base(1) + if(_globalBeBuffer<10)(1) = CYC=2. <= 8. COMPLIANT.
- `DecrementBuffer`: base(1) + if(_globalBeBuffer>-10)(1) = CYC=2. <= 8. COMPLIANT.
No new methods introduced. All removed methods had their CYC already verified in the plan.

**Check 8 (NT8 Constraints):** PASS
No AtmStrategyCreate, no async/await, no FontFamily in any removal or replacement code.

**Check 9 (7-Scan Checklist Presence):** PASS (with same WARN on canonical numbering as B180-1)
B180-2 scan table has 7 distinct scan IDs. All present. Same WARN as B180-1 regarding
canonical SCAN-06 (build+test) appearing as SCAN-07 and canonical SCAN-07 (structural)
not explicitly present. Substance is covered. WARN, not FAIL.

**Check 11 (B180-2 Removal Completeness):** PASS
All symbols from orchestrator prompt "What is removed" section are accounted for:
- `_pendingBeSlots`: Step 1 ✅
- `_pendingBePriceRetry`: Step 2 ✅
- `_bePriceRetryAttempts`: Step 2 ✅
- `ArmPendingBe`: Step 13 ✅
- `DisarmPendingBe`: Step 13 ✅
- `MoveStopToBreakEven`: Step 7 ✅
- `TryTriggerBeRecovery`: Step 6 ✅
- `TryFireBePriceRetry` (implicitly `TryFireBePriceRetry`): Step 7 ✅
- `BeState` enum: Step 11 ✅
- `_beState` field: Step 11 ✅
- `ExecuteBeIdle` / `ExecuteBeArmed`: eliminated by B180-1 SEARCH block (lines 1474-1534 replaced) ✅
- `GlobalBeAllDisarmed` event + `RaiseBeAllDisarmed`: Step 4 ✅
- `PendingBeArmed` event: Step 3 ✅
- `PendingBeFired` event: Step 3 ✅
- `ArmAllPendingBe`: Step 5 ✅
- `OnPendingBeAccountUpdate`: Step 13 ✅
- `IsPendingSlotsEmpty`: Step 13 ✅
- `ClearAllPendingBeSlots`: Step 13 ✅
- `TryEmitBeWarnIfFlatArmed`: pre-removal check (conditional, MN-03) ✅
- `LogBeCancelDiag`, `TryReplacePttBeBrackets`, `IsBeBracketRecoveryEligible`: Step 6 ✅
- `RegisterBeRetrySlotIfNeeded`, `RegisterPendingBeSlot`, `TryArmBePriceRetry`: Steps 7/13 ✅
- `IsBeRetryArmedFlat`, `IsBeRetrySlotNeeded`, `StillAtNonBePrice`: Steps 7/13 ✅
- `IsImmediateBeEligible`, `FireImmediateBe`: Step 7 ✅
- `HasActivePttBeExits`, `HasActivePttBeTargets`, `HasSettlingBeTargets`: Step 13 ✅
- `ResetBeStateOnFlat`, `DisarmBeAllOnFlat`, `OnPendingBeArmedDispatch`, `OnGlobalBeAllDisarmed`,
  `UpdateBeAllVisuals`, `ComputeBeTargetPrice`, `IsPriceAtOrPastTarget`, `IsPriceAlreadyAtBe`,
  `UpdateBeVisuals`, `OnBeConnected`: Step 12 ✅
- Event subscriptions (Detach, SubscribeEngineEvents): Steps 8A/8B ✅
- `PttGlobalBreakEven.Execute(int)` body: Step 9 ✅
- `PttBreakEven.cs` (entire file): Step 10 ✅

All required removals covered. PASS.

**Check 12 (ptt-sync.ps1 invocation):** PASS
Step 15 and Verify Steps both include `powershell -File .\scripts\ptt-sync.ps1`.

**B180-2 VERDICT: TICKET_REVIEW_PASS** (no violations; B180-1 SEARCH fix required before B180-2 can execute)

---

### B180-3 -- Tests

**Check 1 (Required Sections Present):** PASS
All required sections present: Spec Requirements, Dependency, Files Changed, Method Signatures,
Implementation Instructions, 7-Scan Checklist, Verify Steps.

**Check 2 (Traceability):** PASS
All 5 tests trace to orchestrator prompt test IDs T-B180-T1-01 through T-B180-T1-05. MN-01
correction (RoundToTickTestable vs RoundToTickPublic) explicitly addressed.

**Check 3 (Staged Order):** PASS
Dependency stated: "VERIFY_PASS B180-2 is required. B180-3 MUST NOT START until ptt-verifier
has issued VERIFY_PASS for B180-2." Explicit and correct.

**Check 4 (apply_diff SEARCH Block Accuracy):** N/A
B180-3 uses `insert_content` at line 0 (append) for a new test class. No SEARCH blocks
to verify.

**Check 5 (New File Completeness):** N/A
B180-3 appends to an existing test file (CopyEngineTests.cs), not a new standalone .cs file.
The appended content is a test class only.

**Check 6 (JS Pre-check):** PASS
- No `lock()` in any test method.
- No `throw` in test code.
- No `DateTime.Now`.
- String literals `"B180 DW-B180-BE-SWAP-ALL"`, `": BE flat skip"`, `"RoundToTickTestable"`
  (in comment) are ASCII-only.
- No Unicode characters.

**Check 7 (CYC Pre-check):** PASS
All 5 test methods are linear: CYC=1 each. No branches. COMPLIANT.

**Check 8 (NT8 Constraints):** PASS
No AtmStrategyCreate, no async/await, no FontFamily.

**Check 9 (7-Scan Checklist Presence):** PASS (with same WARN on canonical numbering)
B180-3 scan table has 7 scan IDs (SCAN-01 through SCAN-07). All present.
SCAN-07 = build+test, mapping to canonical SCAN-06. Same WARN as B180-1/B180-2.

**Check 10 (Test Coverage):** FAIL
**VIOLATION -- B180-3 T-B180-T1-01 and T-B180-T1-04 placeholder assertions**

T-B180-T1-01 (`PttBeSwapOne_FlatAccount_SkipsExecution`):
The test body is commented out. The only executable assertion is:
```csharp
Assert.True(true); // placeholder -- replace with real assertion per seam availability
```
This assertion is trivially true and tests nothing. The actual assertion lines
(`Assert.Contains(": BE flat skip", capturedStatus)`) are commented out with `//`. Per the
ticket review role definition: "each test must have explicit Assert statements" testing the
specified behavior. `Assert.True(true)` does not satisfy the behavioral test requirement.

T-B180-T1-04 (`PttBeSwapAll_FollowerAccounts_AreSkipped`):
Same issue. The only executable assertion is `Assert.True(true)`. The comment says
"Full integration assertion: PttBreakEvenSwap.Execute call count == leader-account count only"
but this is a comment, not code.

The spec requirement for T-B180-T1-04 (orchestrator prompt) states: "Expected: PttBreakEvenSwap.Execute
called exactly once (leader only, not follower)." This is a behavioral assertion that must be
implemented, not left as `Assert.True(true)`.

**Note:** T-B180-T1-02, T-B180-T1-03, and T-B180-T1-05 have proper explicit assertions and PASS
this check individually. Only T01 and T04 fail.

**Fix required:** The architect must provide either:
(a) Actual seam-based assertion code for T01 and T04 using the existing `CopyEngineTests.cs`
    infrastructure (per the ticket's own instruction to "follow the existing injection pattern"),
(b) OR a documented, justified rationale that T01/T04 cannot be tested without NT8 runtime and
    the tests are intentionally deferred to integration test scope, with a corresponding
    deferred backlog entry. In that case, these test IDs should not appear in the B180-3 spec
    requirements section if they are not implemented.

The placeholder `Assert.True(true)` is not acceptable as an engineer-submitted test artifact.
It will cause the verifier to count T01/T04 as "passing tests" when they test nothing.

**Check 12 (ptt-sync.ps1 invocation):** PASS
Step 3 and Verify Steps both include `powershell -File .\scripts\ptt-sync.ps1`.

**B180-3 VERDICT: TICKET_REVIEW_FAIL** (Check 10: T01 and T04 have placeholder assertions only)

---

## Violations Summary

| # | Check | Ticket | Section | Violation | Rule / Ref | Fix Required |
|---|-------|--------|---------|-----------|-----------|--------------|
| V-01 | Check 4 (SEARCH accuracy) | B180-1 | Step 4 (OnGlobalBeClick) | SEARCH content starts at line 1350 content while `:start_line:1349` is declared. Line 1349 (`// B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.`) is absent from SEARCH block. apply_diff will fail. | Check 4 -- SEARCH block must match actual source | Add `// B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.` as the first line of the SEARCH block content. |
| V-02 | Check 10 (test coverage) | B180-3 | T-B180-T1-01, T-B180-T1-04 | `Assert.True(true)` placeholder is the only executable assertion in both tests. Behavioral assertions (flat-skip capture, follower-skip call count) are commented out. | Check 10 -- explicit Assert statements required | Provide actual seam-based assertions OR defer T01/T04 to backlog with documented rationale and remove them from B180-3 spec requirements. |

---

## Notes (Non-Failing)

**CYC documentation mismatch (informational only):**
- PttBeSwapOne.Execute: ticket annotates CYC=7, actual McCabe (RULES_CATALOG JS-013) = 8.
  CYC=8 is the limit, not a violation. Architect may correct annotation at rewrite time.
- PttBeSwapAll.Execute: ticket annotates CYC=6, actual McCabe = 8. Same note.
  Both methods pass the <=8 constraint.

**Canonical scan numbering drift (informational only, addressed by MN-02):**
The per-ticket 7-scan tables use project-specific scan names (FontFamily, DateTime.Now, etc.)
that do not map 1:1 to RULES_CATALOG §4 canonical scan IDs. The canonical SCAN-06 (dotnet build
+test) appears in Verify Steps rather than the scan table. The canonical SCAN-07 (structural
verification) is functionally covered by the Verify Steps grep sequence. The verifier (Ph4b)
MUST run all canonical scans from RULES_CATALOG §4 independently regardless of ticket scan
table labelling.

---

## Engineer Instructions (Conditional on TICKET_REVIEW_PASS after fix)

The following instructions are provided for the engineer's reference once the architect reissues
04-tickets.md with violations V-01 and V-02 corrected and this reviewer issues TICKET_REVIEW_PASS.

**SCOPE LOCK:**
One ticket per engineer session. Do not begin B180-2 until B180-1 has received an explicit
VERIFY_PASS from ptt-verifier. Do not begin B180-3 until B180-2 has received VERIFY_PASS.
Violation of this constraint is a pipeline failure.

**TOOL ROUTING:**
- New .cs files (PttBeSwapOne.cs, PttBeSwapAll.cs): `write_file`
- Edits to existing .cs files (CopyEngine.cs, TradeCopierPanel.cs, PttGlobalBreakEven.cs,
  CopyEngineTests.cs): `apply_diff` ONLY. Never `write_file` on existing source files.
- PttBreakEven.cs deletion: PowerShell `Remove-Item` after confirming zero callers.

**SCAN ORDER (B180-1 and B180-2):**
Run all 7 scans to zero before declaring BUILD_PASS. Specifically:
1. `grep -c "lock(" src/PropTraderTools/Features/PttBeSwapOne.cs` -- must be 0
2. `grep -c "lock(" src/PropTraderTools/Features/PttBeSwapAll.cs` -- must be 0
3. `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` -- must be 0
4. `grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- must be 0
5. `grep -n "async\|await"` on all new/changed files -- must be 0
6. `grep -Pn "[^\x00-\x7F]"` on all new/changed files -- must be 0
7. `grep -n "AtmStrategyCreate\|AtmStrategyChangeStopTarget"` on new files -- must be 0
8. `dotnet build src/PropTraderTools/` -- must be 0 errors
9. `dotnet test src/PropTraderTools/` -- must be 0 new failures
10. Manual McCabe spot-check: confirm all changed methods <= CYC=8

**SYNC REMINDER:**
`powershell -File .\scripts\ptt-sync.ps1` MUST be run after ALL edits in each ticket session.
Run it as the final step before submitting ticket-N-completion.md. Expected: `N linked, 0 errors`.

**COMPLETION ARTIFACTS:**
For each ticket, produce `docs/brain/B180/ticket-N-completion.md` including:
- List of all files changed (with paths)
- Self-reported results for all 7 canonical scans (RULES_CATALOG §4)
- Build and test output
- ptt-sync.ps1 output
- Any deviations from the ticket with explanation

---

## Overall: TICKET_REVIEW_FAIL

**Reason:** Two violations (V-01, V-02) prevent engineer handoff.
Return to ptt-architect (Phase 3) for correction.

V-01: B180-1 Step 4 -- OnGlobalBeClick SEARCH block is missing its first line.
V-02: B180-3 T01/T04 -- behavioral assertions are commented out; `Assert.True(true)` placeholder only.

After architect corrects these two items and reissues 04-tickets.md, ptt-ticket-reviewer must
re-review the corrected tickets before engineer handoff.

---

## Re-Review Cycle 2 Results

**Date:** 2026-10-09
**Reviewer:** ptt-ticket-reviewer (Phase 3.5)
**Cycle:** Second review pass -- V-01 and V-02 fix verification

---

### V-01 Resolution Check (B180-1 Step 4 -- OnGlobalBeClick SEARCH block)

**Action taken by architect:** SEARCH block at `:start_line:1349` now begins with:
```
        // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
```

**Verification against source:**
- `src/PropTraderTools/TradeCopierPanel.cs` line 1349 (read from file, confirmed):
  ```
          // B40: OnGlobalBeClick -- armed/wait FSM for BE ALL. CYC=4.
  ```
- The SEARCH block first content line **exactly matches** actual source line 1349.
- SEARCH block continues through line 1377 (closing `}` of OnGlobalBeClick). Content
  match confirmed: lines 1350-1377 match the SEARCH block content verbatim.

**V-01 Status: RESOLVED** — SEARCH block now starts at the correct line with the correct content.
The `apply_diff` will locate and replace the block without a content-not-found failure.

---

### V-02 Resolution Check (B180-3 T-B180-T1-01 and T-B180-T1-04 placeholder assertions)

**Action taken by architect:** Both tests converted from `Assert.True(true)` to
`[Fact(Skip="DEFERRED:...")]` with `Assert.True(false, "DEFERRED:...")`.

**Verification:**

T-B180-T1-01 (`PttBeSwapOne_FlatAccount_SkipsExecution`):
- Attribute: `[Fact(Skip = "DEFERRED: NT8 Account/Instrument/Position seam not available in unit test harness")]`
- Assertion: `Assert.True(false, "DEFERRED: NT8 Account/Instrument/Position seam not available -- see DW-B180-BE-T01 backlog item")`
- Contract body present as comments: `Assert.Contains(": BE flat skip", capturedStatus)` documented.
- Deferred backlog item cited: `DW-B180-BE-T01`
- Result when skipped: xUnit will mark as `Skipped` (not Failed, not Passed).
- Result if skip flag removed accidentally: `Assert.True(false)` will FAIL, not silently pass.
- **No false-green risk.** RESOLVED.

T-B180-T1-04 (`PttBeSwapAll_FollowerAccounts_AreSkipped`):
- Attribute: `[Fact(Skip = "DEFERRED: NT8 Account.All and IsFollowerAccount seam not available in unit test harness")]`
- Assertion: `Assert.True(false, "DEFERRED: NT8 Account.All stub not available -- see DW-B180-BE-T04 backlog item")`
- Deferred backlog item cited: `DW-B180-BE-T04`
- Result when skipped: `Skipped`. Result if unskipped: explicit FAIL. No false-green.
- **No false-green risk.** RESOLVED.

T-B180-T1-02, T-B180-T1-03, T-B180-T1-05 (non-deferred tests):
- T-B180-T1-02: `Assert.Equal(4500.25, entryPrice, precision: 10)` — real behavioral assertion. PASS.
- T-B180-T1-03: `Assert.Equal(4999.5, entryLong, precision: 10)` + `Assert.Equal(5000.5, entryShort, precision: 10)` — real assertions. PASS.
- T-B180-T1-05: Four `Assert.Equal` calls on `CopyEngine.RoundToTickTestable`. Real assertions. PASS.

**V-02 Status: RESOLVED** — `Assert.True(true)` placeholders replaced by `[Fact(Skip)]` +
`Assert.True(false, ...)` pattern. Deferred tests are explicitly marked skipped, cannot produce
false-green VERIFY_PASS, and cite backlog items for future seam work.

---

### Full 12-Check Re-Sweep

All checks are re-evaluated against the updated `04-tickets.md`. Only new or previously
unresolved violations are noted.

#### B180-1 Re-Check

| Check | Result | Notes |
|-------|--------|-------|
| Check 1 (Required Sections Present) | PASS | Unchanged from Cycle 1. |
| Check 2 (Traceability) | PASS | Unchanged. |
| Check 3 (Staged Order) | PASS | Unchanged. |
| Check 4 (apply_diff SEARCH Block Accuracy) | **PASS** | V-01 resolved. Step 4 SEARCH block now starts with `// B40: OnGlobalBeClick...` matching line 1349 exactly. Steps 3 and 5 unchanged, previously PASS. |
| Check 5 (New File Completeness) | PASS | Unchanged. |
| Check 6 (JS Pre-check) | PASS | Unchanged. No lock(), throw, DateTime.Now, non-ASCII. |
| Check 7 (CYC Pre-check) | PASS | Unchanged. All methods <= CYC=8. |
| Check 8 (NT8 Constraints) | PASS | Unchanged. |
| Check 9 (7-Scan Checklist Presence) | PASS | SCAN-01 through SCAN-07 all present. |
| Check 12 (ptt-sync.ps1) | PASS | Unchanged. |
| **B180-1 Verdict** | **TICKET_REVIEW_PASS** | V-01 resolved. No remaining violations. |

#### B180-2 Re-Check

No changes made to B180-2 in this fix cycle. All checks retain their Cycle 1 verdicts.

| Check | Result | Notes |
|-------|--------|-------|
| All 10 checks | PASS | Unchanged from Cycle 1. B180-2 had no violations in Cycle 1. |
| **B180-2 Verdict** | **TICKET_REVIEW_PASS** | |

#### B180-3 Re-Check

| Check | Result | Notes |
|-------|--------|-------|
| Check 1 (Required Sections Present) | PASS | Unchanged. |
| Check 2 (Traceability) | PASS | T01 and T04 are now documented as DEFERRED with backlog IDs DW-B180-BE-T01 and DW-B180-BE-T04. The spec requirement entries in B180-3 header are annotated with `-- DEFERRED: ...` inline. Traceability to orchestrator prompt IDs T-B180-T1-01 and T-B180-T1-04 is preserved via the backlog references. PASS. |
| Check 3 (Staged Order) | PASS | Unchanged. |
| Check 4 (SEARCH Block) | N/A | No apply_diff SEARCH blocks. insert_content at line 0. |
| Check 5 (New File Completeness) | N/A | Append to existing file. |
| Check 6 (JS Pre-check) | PASS | Unchanged. |
| Check 7 (CYC Pre-check) | PASS | Unchanged. All test methods CYC=1. |
| Check 8 (NT8 Constraints) | PASS | Unchanged. |
| Check 9 (7-Scan Checklist Presence) | PASS | SCAN-01 through SCAN-07 all present. |
| Check 10 (Test Coverage) | **PASS** | V-02 resolved. T01/T04 use `[Fact(Skip)]` + `Assert.True(false)` — no false-green risk. T02, T03, T05 have real behavioral assertions. Deferred seam backlog items documented. |
| Check 12 (ptt-sync.ps1) | PASS | Unchanged. |
| **B180-3 Verdict** | **TICKET_REVIEW_PASS** | V-02 resolved. No remaining violations. |

---

### Engineer Handoff Note (Now Active)

Both violations are resolved. The engineer may proceed per the original engineer
instructions in this review document (see "Engineer Instructions" section above).

Sequence confirmation:
1. B180-1: additive rewire — proceed immediately.
2. B180-2: dead code removal — wait for ptt-verifier VERIFY_PASS on B180-1.
3. B180-3: tests — wait for ptt-verifier VERIFY_PASS on B180-2.

No scope changes, no new requirements, no new constraints introduced in this fix cycle.

---

### V-01 Status: RESOLVED
### V-02 Status: RESOLVED
### Overall: TICKET_REVIEW_PASS
