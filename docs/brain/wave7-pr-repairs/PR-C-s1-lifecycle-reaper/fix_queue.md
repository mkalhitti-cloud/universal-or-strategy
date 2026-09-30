# PR-C Fix Queue -- S1 Lifecycle + REAPER Safety
# Branch: repairs/s1-lifecycle-reaper (create fresh from main)
# Cluster: S1 SIMA Core, S4 REAPER Defense
# OKF Rules: 5 (Account.All snapshots, defense in depth), Rule 12 (SA1503)

---

## FINDING C-A2-1,2 -- Account.All bare enumeration in REAPER.Audit.cs

**File**: src/V12_002.REAPER.Audit.cs
**Lines**: 22, 933
**DD entries**: DD-016, DD-017
**Issue**:
  Line 22: `foreach (Account acct in Account.All)` in AuditApexPositions()
  Line 933: `foreach (Account acct in Account.All)` in ProcessReaperFlatten_FindAccount()
  Broker-thread mutation during fleet audit or flatten causes InvalidOperationException.
**Fix**: Add `.ToArray()` to both: `foreach (Account acct in Account.All.ToArray())`
**OKF Rule 5**: independent_tracking.

---

## FINDING C-A2-3 -- Account.All bare .FirstOrDefault in REAPER.NakedStop.cs

**File**: src/V12_002.REAPER.NakedStop.cs
**Line**: 27
**Issue**: `Account acct = Account.All.FirstOrDefault(a => ...)` -- LINQ on
  live broker collection without snapshot. LINQ enumerates Account.All lazily;
  broker mutation during enumeration causes InvalidOperationException.
**Fix**: `Account acct = Account.All.ToArray().FirstOrDefault(a => ...)`
**OKF Rule 5**: independent_tracking.

---

## FINDING C-A3-1,2 -- Empty catch {} in V12_002.cs

**File**: src/V12_002.cs
**Lines**: 892, 902
**Issue**: Two silent exception swallowers in actor/drain paths.
**Fix**: Replace with `catch (Exception ex) { Print($"[V12 WARN] ActorDrain: {ex.Message}"); }`
  Use context-appropriate label. Do NOT rethrow.
**OKF Rule 5**: infrastructure_telemetry.

---

## FINDING C-A3-3,4,5,6 -- Empty catch {} in Lifecycle.cs

**File**: src/V12_002.Lifecycle.cs
**Lines**: 261, 329, 334, 371
**Issue**: Four silent swallowers in startup/teardown lifecycle paths.
**Fix**: Replace each with logged catch. Read each call site to determine
  appropriate context label (e.g. "HandleRealtime", "HandleTerminated",
  "HandleConfigure", "OnStateChange").
**OKF Rule 5**: infrastructure_telemetry.

---

## FINDING C-A5-1 -- Watchdog cancel whitelist narrower than detection

**File**: src/V12_002.Safety.Watchdog.cs
**Lines**: 135-146
**DD entry**: DD-018
**Issue**: IsWatchdogCancellableOrder whitelist
  (Working/Submitted/Accepted/ChangePending/ChangeSubmitted) is narrower than
  HasWatchdogLeadAccountWorkingOrder which catches all non-terminal states via
  IsOrderTerminal(). PartFilled/CancelPending/Initialized orders trigger
  watchdog escalation but are skipped by the cancel sweep -- leaving live
  orders unmanaged after flatten fires.
**Fix**: Expand IsWatchdogCancellableOrder to include PartFilled, CancelPending,
  and Initialized states so the cancel sweep covers everything that triggered
  the escalation. Read both functions before making the change.
**OKF Rule 5**: defense in depth -- watchdog must cancel what it detected.

---

## FINDING C-A6-1,2 -- SA1503 missing braces in SIMA.Lifecycle.cs

**File**: src/V12_002.SIMA.Lifecycle.cs
**Lines**: 120-128
**DD entry**: DD-012
**Issue**: Single-line if bodies without braces in DrainPhotonRingOnShutdown.
**Fix**: Run `dotnet csharpier format src/V12_002.SIMA.Lifecycle.cs` --
  CSharpier adds braces automatically. Verify no logic change.
**OKF Rule 12**: SA1503 braces mandate.

---

## FINDING C-B6-1 -- Magic numbers in LogicAudit.cs (trading constants)

**File**: src/V12_002.LogicAudit.cs
**Issue**: Numeric literals used as trading thresholds in audit cases
  (AuditCase2_ContractSizing, AuditCase5_TrendRmaSplit, etc.).
  These are the HIGH-RISK magic numbers -- wrong value = wrong audit result.
**Fix**: Extract literals to named private const fields at top of partial class.
  Examples: tick values, contract multipliers, percentage thresholds.
  Read the file first. Only extract literals that are clearly trading parameters,
  not trivial 0/1/2 index values.
**OKF Rule 6**: Complexity / named constants for domain values.
**CYC impact**: None -- const declarations do not affect CYC.

---

## Gate Requirements

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No bare Account.All in REAPER.Audit.cs, REAPER.NakedStop.cs
- [ ] No empty catch {} in V12_002.cs, Lifecycle.cs
- [ ] No lock() introduced
- [ ] All modified methods CYC <= 8

## PR title
"fix(repairs): S1 lifecycle + REAPER safety -- Account.All snapshots, empty catch, watchdog whitelist"
