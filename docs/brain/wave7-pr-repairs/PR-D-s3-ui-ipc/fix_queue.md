# PR-D Fix Queue -- S3 UI & IPC Safety
# Branch: repairs/s3-ui-ipc (create fresh from main)
# Cluster: S3 UI & IPC
# OKF Rules: 3 (DateTime.Now), 5 (Account.All, null guard, silent exception),
#             6 (SA1204 ordering), 12 (StringComparison.Ordinal)

---

## FINDING D-A1-1,2 -- DateTime.Now in UI.Compliance.cs

**File**: src/V12_002.UI.Compliance.cs
**Lines**: 49, 893 (approx -- verify exact lines in live file)
**Issue**:
  Line 49: `return ConvertToSelectedTimeZone(DateTime.Now)` -- passes local time
    to timezone converter; should pass UtcNow for deterministic conversion.
  Line 893: `DateTime.Now` in compliance log timestamp.
**Fix**: Both -> `DateTime.UtcNow`
**OKF Rule 3**: UTC-only clock source.

---

## FINDING D-A1-3,4 -- DateTime.Now comparisons in UI.Sizing.cs

**File**: src/V12_002.UI.Sizing.cs
**Lines**: 130, 314
**Issue**:
  Line 130: `(DateTime.Now - _lastSyncFailureTime).TotalMilliseconds` -- time
    comparison using local clock. If _lastSyncFailureTime is UtcNow elsewhere,
    this comparison is wrong by timezone offset.
  Line 314: `_lastSyncFailureTime = DateTime.Now` -- stores local time.
**Fix**: Both -> `DateTime.UtcNow`. Ensure _lastSyncFailureTime field is
  consistently UtcNow at all write sites (check all assignments in file).
**OKF Rule 3**: "All time comparisons must use the SAME clock source (UTC only)."

---

## FINDING D-A2-1 -- Account.All bare enumeration in UI.Compliance.cs

**File**: src/V12_002.UI.Compliance.cs
**Line**: 303
**Fix**: `foreach (Account acct in Account.All.ToArray())`
**OKF Rule 5**: independent_tracking.

---

## FINDING D-A2-2,3 -- Account.All bare enumeration in UI.IPC.Commands.Fleet.cs

**File**: src/V12_002.UI.IPC.Commands.Fleet.cs
**Lines**: 334 (Account.All), 409 (Account.All)
**Fix**: Add `.ToArray()` to both.
**OKF Rule 5**: independent_tracking.

---

## FINDING D-A2-4,5 -- acct.Orders bare enumeration in UI.IPC.Commands.Fleet.cs

**File**: src/V12_002.UI.IPC.Commands.Fleet.cs
**Lines**: 234 (Account.Orders), 338 (acct.Orders)
**Fix**: Add `.ToArray()` to both: `Account.Orders.ToArray()`, `acct.Orders.ToArray()`
**OKF Rule 5**: defense in depth.

---

## FINDING D-A2-6 -- Account.All bare enumeration in UI.IPC.Commands.Misc.cs

**File**: src/V12_002.UI.IPC.Commands.Misc.cs
**Line**: 141
**Fix**: `foreach (Account acct in Account.All.ToArray())`
**OKF Rule 5**: independent_tracking.

---

## FINDING D-A3-1 -- Empty catch {} in UI.IPC.cs

**File**: src/V12_002.UI.IPC.cs
**Line**: 427
**DD entry**: DD-007
**Issue**: Empty catch around TriggerCustomEvent in ProcessIpcCommands silently
  discards queue-drain failures. If IPC queue drain fails, the strategy
  continues without any indication.
**Fix**: `catch (Exception ex) { Print($"[V12 WARN] IPC TriggerCustomEvent: {ex.Message}"); }`
**OKF Rule 5**: infrastructure_telemetry.

---

## FINDING D-A5-1 -- Null order.Instrument guard in UI.IPC.Commands.Fleet.cs

**File**: src/V12_002.UI.IPC.Commands.Fleet.cs
**Line**: 374
**DD entry**: DD-005
**Issue**: `CancelAll_IsOrderCancellable` dereferences `order.Instrument.FullName`
  without a null guard on `order.Instrument`. NinjaTrader Order.Instrument
  can be null (disconnected/synthetic orders).
**Fix**: Add null guard: before `order.Instrument.FullName`, check
  `order.Instrument != null &&`. Pattern already used elsewhere in this file.
**OKF Rule 5**: production safety -- independent_tracking.

---

## FINDING D-B2-1,2 -- SA1204 static member ordering in UI.IPC.cs

**File**: src/V12_002.UI.IPC.cs
**Lines**: 297-340
**DD entry**: DD-002
**Issue**: Private static methods/fields (GlobalCommandsSet, IsGlobalCommand,
  IsMicroContractAlias, IsRoutingAlias, IsStrategyKeyword) appear after
  non-static methods. SA1204: static members should precede non-static.
**Fix**: Move the private static block (lines 297-340) to immediately after
  the constructor/field declarations, before the first non-static method.
  This is a pure reorder -- no logic change.
**OKF Rule 6**: naming/style SA1204.

---

## FINDING D-B2-3 -- SA1204 static member ordering in UI.Compliance.cs

**File**: src/V12_002.UI.Compliance.cs
**Line**: 67
**DD entry**: DD-003
**Issue**: `private static bool IsValidTradeExecution` appears after
  non-static methods (lines 48-66).
**Fix**: Move to precede non-static methods. Pure reorder, no logic change.
**OKF Rule 6**: SA1204.

---

## FINDING D-B4 -- StringComparison.Ordinal missing in S3 files

**Files**: src/V12_002.UI.IPC.cs, src/V12_002.UI.IPC.Commands.Fleet.cs,
           src/V12_002.UI.IPC.Commands.Misc.cs, src/V12_002.UI.Compliance.cs,
           src/V12_002.UI.Sizing.cs
**Issue**: `.StartsWith(...)`, `.EndsWith(...)`, `.Contains(...)`, `.IndexOf(...)`
  called on string literals without `StringComparison.Ordinal`. Culture-unsafe
  for internal fixed-format strings (order names, command names, prefixes).
**Fix**: Add `, StringComparison.Ordinal` to each applicable call.
  Use Roslyn bulk fix: in each file, apply IDE0046/CA1307 via
  `dotnet format --diagnostics CA1307 src/` or manually add the argument.
  EXCEPTION: Do NOT add StringComparison to calls where the string being
  compared is user-facing or locale-sensitive (e.g. display names).
  All internal V12 order/command name comparisons are ASCII -- Ordinal is correct.
**OKF Rule 6**: culture-safe string operations.

---

## Gate Requirements

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No DateTime.Now in UI.Compliance.cs, UI.Sizing.cs
- [ ] No bare Account.All / acct.Orders in modified files
- [ ] No empty catch {} in UI.IPC.cs
- [ ] No lock() introduced
- [ ] All modified methods CYC <= 8

## PR title
"fix(repairs): S3 UI & IPC safety -- DateTime.Now, Account.All snapshots, null guards, SA1204, StringComparison"
