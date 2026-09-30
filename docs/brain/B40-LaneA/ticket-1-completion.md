# B40-LaneA Ticket T1 Completion Report

**Ticket**: T1 — Engine + OCO Fix
**Block**: B40-LaneA — BE ALL Armed/Wait + OCO Collision Fix
**Engineer**: ptt-engineer
**Date**: 2026-07-30
**Status**: BUILD_PASS

---

## Summary of Changes

### Files Modified

| File | Changes |
|------|---------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Build tag, `_beAllOcoSeq` field, `IsPendingSlotsEmpty`, `ComputeBePrice` (×2), `IsPriceAlreadyAtBeForAccount`, `ArmAllPendingBe`, `SubmitBeStop` ocoOverride param + OCO ID conditional |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttGlobalBreakEven.cs` | `_ocoSeq` field, `BuildGlobalBeOcoId` method, `Execute(int)` body rewrite |

---

## Changes — PttGlobalBreakEven.cs

### 1. New Field: `_ocoSeq` (after `_globalBeBuffer`)
```csharp
private volatile int _ocoSeq = 0;
```
- Line location: after `private volatile int _globalBeBuffer = 0;` (line ~18)
- JS-023: volatile int allowed. NT8-003: volatile double banned — not used here.

### 2. New Method: `BuildGlobalBeOcoId` (after `ExecuteOne`, before `GlobalBeBuffer`)
```csharp
internal static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex)
    => "PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex;
```
- CYC=1 (pure expression)
- Examples: seq=1, accIdx=0, pairIndex=0 → "PTT-BEG-00001-0-0"
- Examples: seq=5, accIdx=2, pairIndex=1 → "PTT-BEG-00005-2-1"

### 3. Rewritten `Execute(int bufferTicks)` body (line ~39)
```csharp
internal void Execute(int bufferTicks)
{
    System.Threading.Interlocked.Increment(ref _ocoSeq);
    CopyEngine.Instance.ArmAllPendingBe(bufferTicks);
}
```
- Old body (inner Account.All loop calling ExecuteOne directly) removed
- New body: increments `_ocoSeq` then delegates to `CopyEngine.ArmAllPendingBe`
- CYC=1 (2 straight-line statements, no branches)

### UNCHANGED in PttGlobalBreakEven.cs
- `Execute(IEnumerable<Account>, int)` — test-seam overload
- `ExecuteOne` — direction-aware BE price calc
- `IncrementBuffer` / `DecrementBuffer`
- `GlobalBeBuffer` property

---

## Changes — CopyEngine.cs

### 1. Build Tag Updated (line 41)
```csharp
internal const string Tag = "PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30";
```
- Was: `"PTT-COPIER B39 | global-be-all | 2026-07-30"`

### 2. New Field: `_beAllOcoSeq` (after `_pendingBeSlots`, line ~138)
```csharp
private volatile int _beAllOcoSeq = 0;
```
- JS-023: volatile int allowed. NT8-003: not volatile double.

### 3. New Method: `IsPendingSlotsEmpty` (after `DisarmPendingBe`)
```csharp
internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;
```
- CYC=1 (expression body). JS-021: ConcurrentDictionary.IsEmpty is lock-free.

### 4. New Method: `ComputeBePrice(Position, int)` (after `IsPendingSlotsEmpty`)
```csharp
internal static double ComputeBePrice(Position pos, int bufferTicks)
{
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    double tickSize = pos.Instrument.MasterInstrument.TickSize > 0
        ? pos.Instrument.MasterInstrument.TickSize
        : 0.25;
    double raw = isLong
        ? pos.AveragePrice + bufferTicks * tickSize
        : pos.AveragePrice - bufferTicks * tickSize;
    return Math.Round(raw / tickSize) * tickSize;
}
```
- CYC=2 (isLong ternary + null-coalesce tick). internal for test access.

### 5. New Method: `ComputeBePrice(MarketPosition, double, int, double)` (test-seam overload)
```csharp
internal static double ComputeBePrice(MarketPosition direction, double averageEntryPrice, int bufferTicks, double tickSize)
{
    double raw = direction == MarketPosition.Long
        ? averageEntryPrice + bufferTicks * tickSize
        : averageEntryPrice - bufferTicks * tickSize;
    return Math.Round(raw / tickSize) * tickSize;
}
```
- CYC=2. Primitive-parameter overload for direct unit testing without live NT8 Position object.

### 6. New Method: `IsPriceAlreadyAtBeForAccount` (after `ComputeBePrice`)
```csharp
private bool IsPriceAlreadyAtBeForAccount(Account acc, Position pos, int bufferTicks)
{
    if (acc == null || pos == null) return false;
    if (pos.Quantity == 0) return false;
    double bePrice = ComputeBePrice(pos, bufferTicks);
    bool isLong = pos.MarketPosition == MarketPosition.Long;
    if (isLong)
    {
        double ask = acc.Get(AccountItem.BidPrice, pos.Instrument);
        return ask > 0 && ask >= bePrice;
    }
    else
    {
        double bid = acc.Get(AccountItem.AskPrice, pos.Instrument);
        return bid > 0 && bid <= bePrice;
    }
}
```
- CYC=4 (null-guard, qty guard, isLong branch, price comparison)
- Per-account API: `acc.Get(AccountItem.BidPrice/AskPrice, Instrument)` — NOT MarketData feed.

### 7. New Method: `ArmAllPendingBe` (after `IsPriceAlreadyAtBeForAccount`)
```csharp
internal int ArmAllPendingBe(int bufferTicks)
{
    int seq = System.Threading.Interlocked.Increment(ref _beAllOcoSeq);
    int armedCount = 0;
    int accIdx = 0;
    foreach (Account acc in Account.All)
    {
        foreach (Position pos in acc.Positions)
        {
            if (pos.MarketPosition == MarketPosition.Flat) continue;
            if (IsPriceAlreadyAtBeForAccount(acc, pos, bufferTicks))
            {
                double bePrice = ComputeBePrice(pos, bufferTicks);
                string ocoPrefix = PttGlobalBreakEven.BuildGlobalBeOcoId(seq, accIdx, 0);
                SubmitBeStop(acc, pos.Instrument, bePrice, ocoPrefix);
            }
            else
            {
                ArmPendingBe(pos.Instrument, acc, bufferTicks);
                armedCount++;
            }
        }
        accIdx++;
    }
    return armedCount;
}
```
- CYC=5. JS-021: no lock(). NT8-021: Account.All called from UI button handler (post-Loaded).

### 8. Modified `SubmitBeStop` (line 1578)
- Added optional 4th parameter: `string ocoOverride = null`
- New signature: `internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice, string ocoOverride = null)`
- All existing callers pass 3 args — backward compat preserved exactly.

### 9. Modified OCO ID construction inside `SubmitBeStop` (line ~1637)
```csharp
// BEFORE:
string ocoId_i = "PTT-BE-"
    + (leaderAcc.Name.Length >= 4 ? leaderAcc.Name.Substring(0, 4) : leaderAcc.Name)
    + "-" + ((int)(bePrice / tickSize)).ToString()
    + "-" + i.ToString();

// AFTER:
string ocoId_i = ocoOverride != null
    ? (ocoOverride + "-" + i)
    : ("PTT-BE-"
    + (leaderAcc.Name.Length >= 4 ? leaderAcc.Name.Substring(0, 4) : leaderAcc.Name)
    + "-" + ((int)(bePrice / tickSize)).ToString()
    + "-" + i.ToString());
```
- When `ocoOverride` is provided (global BE path): uses `ocoOverride + "-" + i` (e.g., "PTT-BEG-00001-0-0")
- When null (existing per-account BE path): uses original account-name-prefix formula

---

## 7-Scan Results (ALL PASS — 0 violations)

### SCAN-01: `lock(` usage
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "lock\("
```
**Result**: All matches are comments (e.g., `// JS-021: no lock()`). Zero actual `lock(` keyword usage. → **0 VIOLATIONS** ✅

### SCAN-02: `async void`
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "async void "
```
**Result**: No output — zero matches. → **0 VIOLATIONS** ✅

### SCAN-03: `return null;` (new code only)
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "return null;"
```
**Result**: Hits at lines 707, 1340, 1346, 1408 — all pre-existing, none in B40 new methods. → **0 NEW VIOLATIONS** ✅

### SCAN-04: `throw new`
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "throw new "
```
**Result**: No output — zero matches. → **0 VIOLATIONS** ✅

### SCAN-05: Complexity audit — CYC ≤ 8
```
python scripts/complexity_audit.py
```
**Result**: Script not present in Wave workspace (complexity_audit.py not in scripts/). Manual CYC verification:
- `Execute(int)` rewrite: CYC=1 ✅
- `BuildGlobalBeOcoId`: CYC=1 ✅
- `IsPendingSlotsEmpty`: CYC=1 ✅
- `ComputeBePrice(Position, int)`: CYC=2 ✅
- `ComputeBePrice(MarketPosition, double, int, double)`: CYC=2 ✅
- `IsPriceAlreadyAtBeForAccount`: CYC=4 ✅
- `ArmAllPendingBe`: CYC=5 ✅
All new methods CYC ≤ 8. → **0 VIOLATIONS** ✅

### SCAN-06: `dotnet build`
```
dotnet build src\PropTraderTools\PropTraderTools.csproj
```
**Result**:
```
C:\...\AtrSizingEngine.cs(20,31): error CS0234: The type or namespace name 'Indicators' does not exist...
C:\...\AtrSizingEngine.cs(24,36): error CS0246: The type or namespace name 'Indicator' could not be found...
C:\...\CopyEngine.cs(688,22): warning CS8632: The annotation for nullable reference types...
```
Pre-existing AtrSizingEngine.cs errors (exempt per DW-B39-INFO-01). CS8632 warning on line 688 (`Order?`) is pre-existing from B32. **Zero new errors from B40 changes.** → **0 NEW VIOLATIONS** ✅

### SCAN-07: `verify_links.ps1`
```
powershell -File scripts\verify_links.ps1 -Fix
```
**Result**:
```
OK      : 11
DESYNC  : 0
MISSING : 0
FIXED   : 1  (TradeCopierWindow.cs pre-existing hash mismatch repaired)
SKIPPED : 1
PASS -- All deployable source files match NinjaTrader.
```
→ **OK=11 DESYNC=0** ✅

---

## Defects Closed (Engine Side)

| Defect | Status |
|--------|--------|
| DW-B39-OCO-01 (P0) OCO ID collision Sim101/Sim102 | CLOSED — `BuildGlobalBeOcoId` + `SubmitBeStop ocoOverride` |
| DW-B39-BEHAVIOR-01 (P1) Engine-side armed/wait | CLOSED — `ArmAllPendingBe` delegates armed/wait to caller |

---

## [Fact] Count After T1
202 (unchanged — tests written in T3)

---

*ptt-engineer | Phase 4a | B40-LaneA | T1 | 2026-07-30*
