# PR-E Fix Queue -- S5 Entries + SignalBroadcaster
# Branch: repairs/s5-entries-signals (create fresh from main)
# Cluster: S5 Signals & Entries, S7 Infrastructure
# OKF Rules: 3 (DateTime.Now), 12 (SA1503 braces, StringComparison.Ordinal)

---

## FINDING E-A1-1 through E-A1-9 -- DateTime.Now in SignalBroadcaster.cs

**File**: src/SignalBroadcaster.cs
**Lines**: 289, 306, 321, 330, 340, 355, 370, 385, 400
**Issue**: 9x `Timestamp = DateTime.Now` on signal structs (TradeSignal,
  TrailUpdate, TargetAction, FlattenSignal, BreakevenSignal, StopUpdate,
  EntryUpdate, OrderCancel, ExternalCommand). These signals are broadcast
  cross-process via MMIO/Photon ring. A cross-process consumer reading
  UTC expects UTC timestamps -- local timestamps cause incorrect age
  calculations on the consumer side.
**Fix**: All 9 -> `DateTime.UtcNow`
**Commit separately from Entries fixes** (different file cluster).
**OKF Rule 3**: "All time comparisons must use the SAME clock source (UTC only)."

---

## FINDING E-A1-10,11 -- DateTime.Now in Entries.OR.cs

**File**: src/V12_002.Entries.OR.cs
**Lines**: 62, 106 (from okf-violation-scan.md -- verify exact lines in live file)
**Issue**: `lastArmedTime = DateTime.Now` -- armed time used for OR window
  expiry comparisons. If expiry check uses UtcNow, the comparison is wrong
  by timezone offset outside UTC.
**Fix**: `DateTime.Now` -> `DateTime.UtcNow`
  Verify all read sites of `lastArmedTime` also use UtcNow for consistency.
**OKF Rule 3**: consistent clock source.

---

## FINDING E-B1-1,2,3 -- DateTime.Now name suffixes in Entries.FFMA.cs

**File**: src/V12_002.Entries.FFMA.cs
**Lines**: 182, 392, 628
**DD entries**: DD-013, DD-014, DD-015
**Issue**: `DateTime.Now.ToString("HHmmssfff")` and `DateTime.Now.ToString(...)`
  used as entry name timestamp suffixes. Not time comparisons, but violates
  UTC-only rule.
**Fix**: `DateTime.Now` -> `DateTime.UtcNow` at all 3 lines.
**OKF Rule 3**: UTC-only.

---

## FINDING E-B1-4 -- DateTime.Now name suffix in Entries.RMA.cs

**File**: src/V12_002.Entries.RMA.cs
**Line**: 107
**Issue**: `string timestamp = DateTime.Now.ToString("HHmmssffff")` -- naming only.
**Fix**: `DateTime.Now` -> `DateTime.UtcNow`
**OKF Rule 3**: UTC-only.

---

## FINDING E-B3 -- SA1503 missing braces in Entries files

**Files**: src/V12_002.Entries.FFMA.cs (12), src/V12_002.Entries.RMA.cs (14),
           src/V12_002.Entries.OR.cs (4)
**Issue**: Single-line if bodies without braces -- Roslyn SA1503 violation.
  These are the high-count files from the braceless-if scan.
**Fix**: Run `dotnet csharpier format src/` -- CSharpier adds braces automatically.
  Run AFTER all other fixes in this PR so formatting is applied to final state.
  Verify no logic changes by reading diff carefully.
**OKF Rule 12**: SA1503 braces mandate.

---

## FINDING E-B4 -- StringComparison.Ordinal missing in Entries files

**Files**: src/V12_002.Entries.FFMA.cs, src/V12_002.Entries.RMA.cs,
           src/V12_002.Entries.OR.cs, src/SignalBroadcaster.cs
**Issue**: String comparisons without StringComparison.Ordinal.
**Fix**: Add `, StringComparison.Ordinal` to applicable `.StartsWith/EndsWith/
  Contains/IndexOf` calls on internal fixed-format strings.
**OKF Rule 6**: culture-safe string operations.

---

## Commit order recommendation

1. fix(repairs/pr-e): SignalBroadcaster DateTime.Now -> UtcNow (9 timestamps)
2. fix(repairs/pr-e): Entries.OR DateTime.Now -> UtcNow (lastArmedTime)
3. fix(repairs/pr-e): FFMA/RMA DateTime.Now -> UtcNow (name suffixes, DD-013..015)
4. fix(repairs/pr-e): SA1503 braces + StringComparison.Ordinal (CSharpier + bulk)

---

## Gate Requirements

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No DateTime.Now in SignalBroadcaster.cs, Entries.OR.cs, Entries.FFMA.cs, Entries.RMA.cs
- [ ] No lock() introduced
- [ ] All modified methods CYC <= 8

## PR title
"fix(repairs): S5 entries + SignalBroadcaster -- DateTime.Now UTC, SA1503 braces, StringComparison.Ordinal"
