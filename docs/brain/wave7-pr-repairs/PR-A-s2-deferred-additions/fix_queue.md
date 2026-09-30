# PR-A Fix Queue -- S2 Execution Engine deferred additions
# Branch: wave7/pr20-deferred-repairs (EXISTS -- add commits then open PR)
# Cluster: S2 Execution Engine
# Predecessor: lane-L7 complete (5 fixes already committed)
# Gate status on branch: PASS @ 7c9221dd

## CONTEXT
The branch wave7/pr20-deferred-repairs already contains 5 verified fixes
(NEW-F5, NEW-F6, NEW-F7, G-01, G-02). Two small additions are needed before
opening the PR. Add them as new commits on the existing branch.

DO NOT rebase or amend existing commits. Append only.

---

## FINDING A1-DD019 -- DateTime.Now.Ticks suffix in StopSync.cs

**File**: src/V12_002.Orders.Management.StopSync.cs
**Line**: 968
**DD entry**: DD-019
**Issue**: `string suffix = (DateTime.Now.Ticks % 100000000).ToString();`
  Used as a stop order name suffix. Not a time comparison, but violates
  UTC-only rule -- Ticks from DateTime.Now are local-time ticks on some
  machines (timezone offset baked in).
**Fix**: `DateTime.Now.Ticks` -> `DateTime.UtcNow.Ticks`
**OKF Rule 3**: All clock sources must be UTC.
**CYC impact**: None -- single token replacement.
**Commit message**: fix(repairs/pr-a): DD-019 -- DateTime.Now.Ticks -> UtcNow.Ticks in StopSync suffix

---

## FINDING A1-DD020 -- DateTime.Now.Ticks suffix in Trailing.StopUpdate.cs

**File**: src/V12_002.Trailing.StopUpdate.cs
**Line**: 393
**DD entry**: DD-020
**Issue**: `string suffix = (DateTime.Now.Ticks % 100000000).ToString();`
  Same pattern as DD-019, in CreateNewPendingForEmergencyStop.
**Fix**: `DateTime.Now.Ticks` -> `DateTime.UtcNow.Ticks`
**OKF Rule 3**: UTC-only.
**CYC impact**: None.
**Commit message**: fix(repairs/pr-a): DD-020 -- DateTime.Now.Ticks -> UtcNow.Ticks in StopUpdate suffix

---

## Gate Requirements (after both additions)

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No DateTime.Now remaining in modified files (check with grep)
- [ ] No lock() introduced
- [ ] CYC of modified methods unchanged

## PR title
"fix(wave7): S2 Execution Engine deferred repairs -- NEW-F5/F6/F7 + G-01/G-02 + DD-019/DD-020"
