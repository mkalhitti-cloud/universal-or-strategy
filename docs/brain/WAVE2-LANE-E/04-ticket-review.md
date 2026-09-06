# WAVE2-LANE-E -- Ticket Review (Cycle 4 -- POST-V-4 REPAIR -- FINAL)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Cycle**: 4 (Post-V-4 repair confirmation -- gate decision)
**Date**: 2026-09-06
**Source tickets**: docs/brain/WAVE2-LANE-E/04-tickets.md (V-4-repaired version)
**Prior review**: docs/brain/WAVE2-LANE-E/04-ticket-review.md (Cycle 3 -- FAIL: V-4)

---

## Ticket Review Result: TICKET_REVIEW_PASS

---

## V-1 Resolution: FIXED (confirmed)

**E-2 SCAN-3**: `powershell -c "[System.IO.File]::ReadAllBytes('src/PropTraderTools/Features/PttGlobalQuickExit.cs') | Where-Object { $_ -gt 127 } | Measure-Object"` -- runnable PowerShell command with `-c` prefix. Confirmed still present in V-4-repaired ticket.

---

## V-2 Resolution: FIXED (confirmed)

**E-3 SCAN-3**: `powershell -c "@('PttBreakEven.cs','PttBreakEvenSwap.cs','PttFlatten.cs','PttTrim.cs') | ForEach-Object { [System.IO.File]::ReadAllBytes(\"src/PropTraderTools/Features/$_\") | Where-Object { $_ -gt 127 } } | Measure-Object"` -- runnable PowerShell loop covering all 4 files. Confirmed still present in V-4-repaired ticket.

---

## V-3 Resolution: FIXED (confirmed)

Phantom rule JS-066 replaced with valid language `CYC constraint (per plan)` in all three tickets (E-1, E-2, E-3). Confirmed still present in V-4-repaired ticket.

---

## V-4 Resolution: FIXED (confirmed)

**E-2 SCAN-7 (prior state -- Cycle 3)**: `lizard spot-check: PttGlobalQuickExit::SnapshotTargetOrders CCN<=8, PttGlobalQuickExit::Execute CCN<=8` -- descriptive annotation, not a runnable lizard CLI command.

**E-2 SCAN-7 (current state)**: `lizard src/PropTraderTools/Features/PttGlobalQuickExit.cs -C 8` -- valid lizard CLI command. Matches the required pattern established by E-1 SCAN-7.

**E-3 SCAN-7 (prior state -- Cycle 3)**: `lizard spot-check: SnapshotTargetsLocal CCN<=8, Execute CCN<=8, FlattenPositionLocal CCN<=8, TrimPositionLocal CCN<=8` -- descriptive annotation, not runnable.

**E-3 SCAN-7 (current state)**: `lizard src/PropTraderTools/Features/PttBreakEven.cs src/PropTraderTools/Features/PttBreakEvenSwap.cs src/PropTraderTools/Features/PttFlatten.cs src/PropTraderTools/Features/PttTrim.cs -C 8` -- valid lizard CLI command listing all 4 affected files.

V-4 is **FIXED** in both E-2 and E-3. Both SCAN-7 commands are now runnable.

---

## Per-Ticket Results (all 8 checks per ticket)

---

### Ticket E-1: PttQuickExit Complexity Reduction

**1. Traceability**: PASS
- Spec Req ID WAVE2-LANE-E-1 present.
- Maps to plan sections 3.1 (Execute CCN 17->6) and 3.2 (SubmitQxOcoPair CCN 9->7).
- All 6 helpers traced to plan section 4 helper table with full signatures and CYC projections.
- DW-LC-01 addressed: Execute was AT-LIMIT post-LaneC (CCN=8), +9 delta since LaneC, post-E-1 CCN=6 (headroom=2). Constraint lifted.
- No phantom work. No missing plan work.

**2. JS Pre-Check**: PASS
- JS-021 (no lock): PASS. All 6 helpers are pure static functions with no shared state.
- JS-001 (no throw): PASS. Errors expressed as bool returns.
- JS-002 (no return null): PASS. IsFlatOrMissing uses TryXxx pattern (out param, bool return). LeaderName returns "NULL" string literal. NewQxOcoId returns "PTT-QX-" fallback string.
- JS-033 (no async void): PASS. All helpers synchronous.
- CYC constraint: `CYC constraint (per plan)` language -- valid, no phantom rule ID. PASS.

**3. CYC Pre-Check**: PASS
- Execute: 17 - 4 (IsFlatOrMissing) - 1 (IsFollowerSkip) - 2 (LeaderName) - 2 (ResolveTick) - 2 (ComputeExitPrices) = 6. Branch arithmetic internally consistent and matches plan section 3.1.
- SubmitQxOcoPair: 9 - 2 (NewQxOcoId) = 7. Matches plan section 3.2.
- All 6 extracted helpers <= 8 per plan projections (max = IsFlatOrMissing CYC=4).
- No AT-LIMIT methods post-E-1 (headroom=2 on Execute).

**4. NT8 Constraints**: PASS
- PTT-QX- prefix in NewQxOcoId fallback preserved verbatim (ticket mandates exact prefix).
- No new CreateOrder calls in any helper.
- AtmStrategyChangeStopTarget / AtmStrategyCreate / Account.Change not referenced in extracted helpers.
- No async/await in any helper.

**5. Method Signatures / Completeness**: PASS
- All 6 helper signatures provided verbatim with correct access modifiers (private static).
- Extraction instructions specific: MOVE/KEEP/REPLACE with exact code for each step.
- File path unambiguous: `src/PropTraderTools/Features/PttQuickExit.cs`.
- Test file path: `src/PropTraderTools/Tests/BwaveLaneETests.cs` (CREATE NEW).

**6. 7-Scan Checklist**: PASS
- SCAN-1: `lizard src/PropTraderTools/Features/ --CCN 8 -x "*/obj/*" -x "*/bin/*" -x "*/Tests/*"` -- runnable. PASS.
- SCAN-2: `grep -rn "lock\s*(" src/PropTraderTools/Features/` -- runnable. PASS.
- SCAN-3: `[System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttQuickExit.cs") | Where-Object { $_ -gt 127 }` -- runnable PowerShell expression. PASS.
- SCAN-4: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- runnable. PASS.
- SCAN-5: `dotnet test src/PropTraderTools/` -- runnable. PASS.
- SCAN-6: `grep -rn "PTT-" src/PropTraderTools/Features/PttQuickExit.cs` -- runnable. PASS.
- SCAN-7: `lizard src/PropTraderTools/Features/PttQuickExit.cs -C 8` -- runnable. PASS.
All 7 scans present with verbatim executable commands.

**7. Test Coverage**: PASS
- 6 [Fact] methods provided with full xUnit source code.
- One [Fact] per helper: IsFlatOrMissing, IsFollowerSkip, LeaderName, ResolveTick, ComputeExitPrices, NewQxOcoId.
- Assertions: NotNull (method exists via reflection), param count, return type.
- New test file: `src/PropTraderTools/Tests/BwaveLaneETests.cs` CREATE NEW explicitly stated.
- xUnit framework only (using Xunit; using System.Reflection). No NUnit/MSTest. PASS.

**8. Acceptance Criteria / File Routing**: PASS
- Clear CCN gates: Execute CCN=6 (SCAN-7), SubmitQxOcoPair CCN=7 (SCAN-7).
- All source paths point to Wave workspace (`src/PropTraderTools/Features/`, `src/PropTraderTools/Tests/`) -- not Director workspace.
- ticket-1-completion.md gate before E-2 stated explicitly.
- 6 [Fact] tests + all 7 scans PASS required before completion.

**E-1 VERDICT: TICKET_REVIEW_PASS** (all 8 checks PASS)

---

### Ticket E-2: PttGlobalQuickExit Complexity Reduction

**1. Traceability**: PASS
- Spec Req ID WAVE2-LANE-E-2 present.
- Maps to plan sections 3.3 (SnapshotTargetOrders CCN 13->8) and 3.4 (Execute(forcedTargets) CCN 9->8).
- All 3 helpers traced to plan section 4 helper table with full signatures and CYC projections.
- DW-LC-01 addressed for Execute(forcedTargets): was AT-LIMIT post-LaneC (+1 delta), returns to AT-LIMIT (DW-LE-02 noted).
- RISK-LE-04 compliance documented: IsNativeTargetOrder intentionally omits name[6] != '0' guard.
- No phantom work. No missing plan work.

**2. JS Pre-Check**: PASS
- JS-021 (no lock): PASS. All 3 helpers are pure string/list predicates.
- JS-001 (no throw): PASS.
- JS-002 (no return null): PASS. All helpers return bool. IsPttTargetOrder returns false for null input.
- JS-033 (no async void): PASS. All helpers synchronous.
- CYC constraint: `CYC constraint (per plan)` language -- valid. PASS.

**3. CYC Pre-Check**: PASS
- SnapshotTargetOrders: 13 - 2 (IsNativeTargetOrder) - 3 (IsPttTargetOrder) = 8. AT-LIMIT flagged. DW-LE-02 noted. Branch arithmetic matches plan section 3.3.
- Execute(forcedTargets): 9 - 1 (IsInvalidForcedTargets) = 8. AT-LIMIT flagged. DW-LE-02 noted. Matches plan section 3.4.
- All 3 helpers <= 8 per plan projections (max = IsPttTargetOrder CYC=5).
- Both target methods AT-LIMIT post-E-2. DW-LE-02 enforces future-addition guard requirement.

**4. NT8 Constraints**: PASS
- No new CreateOrder calls.
- IsNativeTargetOrder, IsPttTargetOrder, IsInvalidForcedTargets: string/list-only logic, zero NT8 API calls.
- No async/await.

**5. Method Signatures / Completeness**: PASS
- All 3 helper signatures provided verbatim with inline bodies.
- Extraction instructions: MOVE/REPLACE with exact code references and RISK-LE-04 compliance note.
- File path unambiguous: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`.
- Test file: APPEND to existing BwaveLaneETests class stated explicitly (no new file/class).

**6. 7-Scan Checklist**: PASS
- SCAN-1: `lizard src/PropTraderTools/Features/ --CCN 8 -x "*/obj/*" -x "*/bin/*" -x "*/Tests/*"` -- runnable. PASS.
- SCAN-2: `grep -rn "lock\s*(" src/PropTraderTools/Features/ -- expected: 0 results` -- runnable. PASS.
- SCAN-3: `powershell -c "[System.IO.File]::ReadAllBytes('src/PropTraderTools/Features/PttGlobalQuickExit.cs') | Where-Object { $_ -gt 127 } | Measure-Object"` -- runnable. V-1 FIXED and confirmed. PASS.
- SCAN-4: `dotnet build -- expected: 0 errors` -- runnable. PASS.
- SCAN-5: `dotnet test -- expected: all prior tests pass (0 regressions)` -- runnable. PASS.
- SCAN-6: `grep -rn "PTT-" src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- runnable. PASS.
- SCAN-7: `lizard src/PropTraderTools/Features/PttGlobalQuickExit.cs -C 8` -- runnable. V-4 FIXED and confirmed. PASS.
All 7 scans present with verbatim executable commands.

**7. Test Coverage**: PASS
- 3 [Fact] methods provided with full xUnit source code.
- One [Fact] per helper: IsNativeTargetOrder, IsPttTargetOrder, IsInvalidForcedTargets.
- APPEND to existing BwaveLaneETests class (not new file/class). Running total: 9 tests after E-2.
- xUnit only. PASS.

**8. Acceptance Criteria / File Routing**: PASS
- Clear CCN gates: SnapshotTargetOrders CCN=8, Execute(forcedTargets) CCN=8 (SCAN-7).
- AT-LIMIT post-E-2 explicitly acknowledged; DW-LE-02 registered.
- IsNativeTargetOrder MUST NOT contain name[6] != '0' guard (RISK-LE-04) -- explicitly listed in acceptance criteria.
- All source paths point to Wave workspace.
- ticket-2-completion.md gate before E-3 stated explicitly.
- 9 total [Fact] tests (6+3) PASS required.

**E-2 VERDICT: TICKET_REVIEW_PASS** (all 8 checks PASS; V-4 confirmed FIXED)

---

### Ticket E-3: PttBreakEven/PttBreakEvenSwap/PttFlatten/PttTrim Complexity Reduction

**1. Traceability**: PASS
- Spec Req ID WAVE2-LANE-E-3 present.
- Maps to plan sections 3.5 (PttBreakEven 9->7), 3.6 (PttBreakEvenSwap 9->8), 3.7 (PttFlatten 9->8), 3.8 (PttTrim 9->8).
- All 4 helpers traced to plan section 4 helper table with full signatures and CYC projections.
- DW-LC-01 addressed for PttBreakEvenSwap::Execute: was AT-LIMIT post-LaneC (+1 delta), returns to AT-LIMIT (DW-LE-02 noted).
- DW-LE-01 (FormatOrderPrice duplication) documented as P2 non-blocking deferred item.
- IsSnapshotTargetOrder correctly delegates to existing IsAtmTargetName and IsPttQxTarget (no reimplementation).
- No phantom work. No missing plan work.

**2. JS Pre-Check**: PASS
- JS-021 (no lock): PASS. All 4 helpers are pure predicates or format functions.
- JS-001 (no throw): PASS.
- JS-002 (no return null): PASS. IsSnapshotTargetOrder returns false for null inputs. FormatOrderPrice returns "mkt" string literal (not null).
- JS-033 (no async void): PASS. All helpers synchronous.
- CYC constraint: `CYC constraint (per plan)` language -- valid. PASS.

**3. CYC Pre-Check**: PASS
- SnapshotTargetsLocal (PttBreakEven): 9 - 2 (IsSnapshotTargetOrder) = 7. No AT-LIMIT post-E-3. Matches plan section 3.5.
- Execute (PttBreakEvenSwap): 9 - 1 (HasNoTargets) = 8. AT-LIMIT. DW-LE-02. Matches plan section 3.6.
- FlattenPositionLocal: 9 - 1 (FormatOrderPrice) = 8. AT-LIMIT. DW-LE-02. Matches plan section 3.7.
- TrimPositionLocal: 9 - 1 (FormatOrderPrice) = 8. AT-LIMIT. DW-LE-02. Matches plan section 3.8.
- All 4 helpers <= 8 (max = IsSnapshotTargetOrder CYC=3).
- 3 AT-LIMIT methods post-E-3. DW-LE-02 governs future-addition constraint.

**4. NT8 Constraints**: PASS
- No new CreateOrder calls.
- IsSnapshotTargetOrder: calls existing IsAtmTargetName and IsPttQxTarget (no reimplementation -- explicitly noted).
- FormatOrderPrice: uses NinjaTrader.Cbi.OrderType.Limit; note to use fully qualified name if using-directive absent.
- HasNoTargets: tuple parameter type must match exact signature of PttBreakEvenSwap.Execute targets variable -- explicitly noted.
- No async/await.

**5. Method Signatures / Completeness**: PASS
- All 4 helper signatures provided verbatim with correct access modifiers.
- Helper bodies/intent documented.
- IMPORTANT note present: IsSnapshotTargetOrder must call existing IsAtmTargetName and IsPttQxTarget, not reimplement.
- DW-LE-01 note: FormatOrderPrice bodies in PttFlatten and PttTrim are structurally identical (non-blocking).
- All 4 file paths listed and unambiguous.
- Test file: APPEND to existing BwaveLaneETests class.

**6. 7-Scan Checklist**: PASS
- SCAN-1: `lizard src/PropTraderTools/Features/ --CCN 8 -x "*/obj/*" -x "*/bin/*" -x "*/Tests/*"` -- runnable. **Final gate: Warning cnt: 0 across all Features/*.cs** expected. PASS.
- SCAN-2: `grep -rn "lock\s*(" src/PropTraderTools/Features/ -- expected: 0 results` -- runnable. PASS.
- SCAN-3: `powershell -c "@('PttBreakEven.cs','PttBreakEvenSwap.cs','PttFlatten.cs','PttTrim.cs') | ForEach-Object { [System.IO.File]::ReadAllBytes(\"src/PropTraderTools/Features/$_\") | Where-Object { $_ -gt 127 } } | Measure-Object"` -- runnable. V-2 FIXED and confirmed. PASS.
- SCAN-4: `dotnet build -- expected: 0 errors` -- runnable. PASS.
- SCAN-5: `dotnet test -- expected: all prior tests pass (0 regressions)` -- runnable. PASS.
- SCAN-6: `grep -rn "PTT-" src/PropTraderTools/Features/PttBreakEven.cs [+ PttBreakEvenSwap.cs + PttFlatten.cs + PttTrim.cs]` -- runnable. PASS.
- SCAN-7: `lizard src/PropTraderTools/Features/PttBreakEven.cs src/PropTraderTools/Features/PttBreakEvenSwap.cs src/PropTraderTools/Features/PttFlatten.cs src/PropTraderTools/Features/PttTrim.cs -C 8` -- runnable. V-4 FIXED and confirmed. PASS.
All 7 scans present with verbatim executable commands.

**7. Test Coverage**: PASS
- 4 [Fact] methods provided with full xUnit source code.
- One [Fact] per helper: IsSnapshotTargetOrder (PttBreakEven), HasNoTargets (PttBreakEvenSwap), FormatOrderPrice (PttFlatten), FormatOrderPrice (PttTrim).
- APPEND to existing BwaveLaneETests class. Running total: 13 tests after E-3.
- xUnit only. PASS.

**8. Acceptance Criteria / File Routing**: PASS
- Clear CCN gates: SnapshotTargetsLocal CCN=7, Execute CCN=8, FlattenPositionLocal CCN=8, TrimPositionLocal CCN=8 (SCAN-7).
- SCAN-1 final gate explicitly required: `Warning cnt: 0 across all Features/*.cs`.
- FormatOrderPrice bodies structurally identical check explicitly listed (DW-LE-01 compliance).
- IsSnapshotTargetOrder delegation check (no reimplementation of IsAtmTargetName/IsPttQxTarget).
- AT-LIMIT post-E-3 acknowledged for 3 methods; DW-LE-02 registered.
- All source paths point to Wave workspace.
- ticket-3-completion.md required with scan results.
- 13 total [Fact] tests (6+3+4) PASS required.

**E-3 VERDICT: TICKET_REVIEW_PASS** (all 8 checks PASS; V-4 confirmed FIXED)

---

## Violations: NONE

All four previously identified violations are resolved:

| ID | Ticket | Check | Prior State | Current State |
|----|--------|-------|-------------|---------------|
| V-1 | E-2 | SCAN-3 | Descriptive (Cycle 2 FAIL) | `powershell -c` runnable command (Cycle 3 FIXED, confirmed Cycle 4) |
| V-2 | E-3 | SCAN-3 | Descriptive (Cycle 2 FAIL) | `powershell -c` loop form (Cycle 3 FIXED, confirmed Cycle 4) |
| V-3 | E-1,E-2,E-3 | JS Pre-Check | Phantom JS-066 rule (Cycle 2 FAIL) | Valid `CYC constraint (per plan)` language (Cycle 3 FIXED, confirmed Cycle 4) |
| V-4 | E-2 | SCAN-7 | `lizard spot-check: MethodName CCN<=8` (Cycle 3 FAIL) | `lizard src/.../PttGlobalQuickExit.cs -C 8` (Cycle 4 FIXED) |
| V-4 | E-3 | SCAN-7 | `lizard spot-check: MethodName CCN<=8` (Cycle 3 FAIL) | `lizard src/.../PttBreakEven.cs src/.../PttBreakEvenSwap.cs src/.../PttFlatten.cs src/.../PttTrim.cs -C 8` (Cycle 4 FIXED) |

No new violations found in Cycle 4 review.

---

## Approved For: Phase 4 engineer execution

All 3 tickets are approved for sequential execution in order E-1 -> E-2 -> E-3.
Sequential constraint is MANDATORY (shared test file BwaveLaneETests.cs, shared SCAN-1 baseline).
Each ticket requires ticket-N-completion.md + Phase 4b verification PASS before next ticket starts.

---

## Engineer Instructions

### AT-LIMIT Warnings (post-wave state -- do not treat as violations)

After Lane-E completes, 5 methods will be AT-LIMIT (CCN=8 exactly):
- `PttGlobalQuickExit::SnapshotTargetOrders` (E-2)
- `PttGlobalQuickExit::Execute(forcedTargets)` (E-2)
- `PttBreakEvenSwap::Execute` (E-3)
- `PttFlatten::FlattenPositionLocal` (E-3)
- `PttTrim::TrimPositionLocal` (E-3)

DW-LE-02 governs all 5: any future branch addition to these methods requires a prior extraction review before implementation. This is NOT a failure condition for Lane-E -- it is expected architecture.

### SCAN-1 Expectations Per Ticket

- **After E-1**: SCAN-1 warning count must decrease vs pre-E-1 baseline. Execute(CCN=17) and SubmitQxOcoPair(CCN=9) must not appear. New helpers must not introduce any CCN > 8.
- **After E-2**: SCAN-1 may still show warnings from remaining E-3 scope files. SnapshotTargetOrders and Execute(forcedTargets) at CCN=8 are AT-LIMIT, not violations (CCN 9+ would be a violation). No new violations in PttGlobalQuickExit.cs helpers.
- **After E-3**: SCAN-1 FINAL GATE -- `Warning cnt: 0 across all Features/*.cs` is required. This is the lane completion condition. If any file reports CCN > 8, E-3 is not complete.

### Scan Execution Notes

- SCAN-3 in E-1 uses bare PowerShell expression (no `powershell -c` prefix). This is runnable from a PowerShell session.
- SCAN-3 in E-2 and E-3 use `powershell -c` prefix (runnable from cmd.exe or any shell).
- SCAN-7 in all 3 tickets uses `-C 8` flag: lizard reports only methods with CCN > 8. Expected output for a passing ticket: no output / 0 warnings.
- SCAN-6 in E-3 covers 4 files; the ticket lists them on separate lines. Run all 4 independently or combine with `grep -rn "PTT-" src/PropTraderTools/Features/PttBreakEven.cs src/PropTraderTools/Features/PttBreakEvenSwap.cs src/PropTraderTools/Features/PttFlatten.cs src/PropTraderTools/Features/PttTrim.cs`.

### DW-LE-01 (Non-Blocking)

`FormatOrderPrice` in PttFlatten and PttTrim will have structurally identical bodies. This is documented, non-blocking for Lane-E, and deferred to a future wave (shared `PttOrderUtils` utility per DW-LC-02 pattern). Engineer must NOT attempt consolidation during E-3 -- it is out of scope and would risk introducing shared state.

### IsSnapshotTargetOrder Delegation (E-3 Critical Note)

`IsSnapshotTargetOrder` in PttBreakEven.cs MUST call the existing `IsAtmTargetName(o.Name)` and `IsPttQxTarget(o.Name)` helpers already present in PttBreakEven.cs. Do NOT copy or reimplement their bodies. These helpers already exist; IsSnapshotTargetOrder is a thin wrapper that composes them with the instrument check.

### RISK-LE-04 (E-2 Critical Note)

`IsNativeTargetOrder` MUST NOT include a `name[6] != '0'` guard. The existing SnapshotTargetOrders code does not have this guard; adding it would change behavior. The acceptance criteria check for this is explicit.