# PR-21 SA1204 Repair Log

## Summary

Two SA1204 (StyleCop -- static members before instance members) violations in
`src/V12_002.UI.IPC.Commands.Fleet.cs` were fixed via pure method relocation.
No logic changes. Both fixes were confirmed by verifier subtasks and gate checks.

---

## Fix 1 -- CancelAll_IsBracketOrder (SA1204a)

| Field | Value |
|-------|-------|
| finding_id | SA1204a |
| file | src/V12_002.UI.IPC.Commands.Fleet.cs |
| violation | CancelAll_IsBracketOrder (private static) at line ~356 appeared after non-static methods at lines 298-354 |
| fix | Relocated CancelAll_IsBracketOrder to immediately after CancelAll_IsOrderTerminal (line 296) |
| sa1204_fix_commit | 68ce25596ae6d7ffeeefb99f1c01677c551479e2 |
| build_passed | true |
| gate_passed | true |
| verifier_verdict | PASS (docs/brain/wave7-pr-repairs/PR-21/verify-SA1204.md) |
| push_time | 2026-07-04T04:54:42Z |

## Fix 2 -- IsLongOrShort (SA1204b)

| Field | Value |
|-------|-------|
| finding_id | SA1204b |
| file | src/V12_002.UI.IPC.Commands.Fleet.cs |
| violation | IsLongOrShort (private static) at line ~421 appeared after non-static methods at lines 312-420 |
| fix | Relocated IsLongOrShort to immediately after CancelAll_IsBracketOrder (line 310), grouping all 3 private statics together |
| sa1204_fix_commit | 69fdad80 (short); full SHA on branch |
| build_passed | true |
| gate_passed | true |
| verifier_verdict | PASS (docs/brain/wave7-pr-repairs/PR-21/verify-SA1204b.md) |
| push_time | 2026-07-04T05:19:16Z (CI run confirmed) |

## Final Static Group Layout (post-fix)

```
Line 289: private static bool CancelAll_IsOrderTerminal(...)
Line 299: private static bool CancelAll_IsBracketOrder(...)
Line 312: private static bool IsLongOrShort(...)
Line 314: private int CancelAll_ProcessFleetAccounts(...)   <-- first non-static
```

All three private statics precede all private non-static methods. SA1204 satisfied.

## CR Re-review

| Field | Value |
|-------|-------|
| cr_re_review_triggered | true (comment #4880735575 after SA1204a; comment #4880800306 after SA1204b) |
| cr_final_state | CHANGES_REQUESTED (stale -- latest CR run b5196128 reviewed commit d4e2f53d which predates both SA1204 fixes; new CR re-run requested but not yet completed) |
| cr_status_context | SUCCESS (CodeRabbit status badge is green on PR) |
| codefactor_state | FAILURE -- pre-existing SA1204 violations in other files (V12_002.UI.IPC.cs, V12_002.UI.Compliance.cs, V12_002.IPC.Hardening.cs) that are NOT in this PR's diff. CodeFactor is informational per lane protocol (V12 uses Codacy as gate). |

## All Prior Fixes Intact

| Fix | Status |
|-----|--------|
| F04 IsActionSqlInjection Print log | intact |
| F05 BuildAccountJsonEntry dead param | intact |
| F06 IsTargetOrderPrefix StringComparison.Ordinal | intact |
| F09 TryClearFlatExpectedPosition null guard | intact |
| F10 BuildAccountJsonEntry null guard | intact |
| F11 CancelAll_IsBracketOrder null guard + Ordinal | intact |
| F12 TryExecuteRmaEntry stopDist guard | intact |
| F13 SetMode_ActivateModeFlags default branch | intact |
| F14 lastComplianceLog = DateTime.UtcNow | intact |

all_prior_fixes_intact: true
