# PTT-DEFERRED-0911 Lane G — Ticket 1 Completion

**Epic:** PTT-DEFERRED-0911
**Lane:** G
**Phase:** 4a (Engineer Implementation)
**Engineer:** ptt-engineer
**Date:** 2026-09-12
**Ticket:** T-G1 — Upgrade TradeCopierPanel_BeBufferBox_FieldDoesNotExist to Option A-partial (DW-B26-01)

---

## SCOPE LOCK CONFIRMATION

SCOPE: T-G1 ONLY. No other tickets read, referenced, or implemented in this session.

---

## What Was Implemented

### CHANGE 1 — `src/PropTraderTools/Core/PttContracts.cs`
Added `BreakEven` to `ICopyEngine` interface after `RelayCancel`, before closing brace.
Short names used (`Account`, `Instrument`) as `using NinjaTrader.Cbi;` was present at line 7.

```csharp
/// <summary>Dispatch break-even to leader and all follower accounts.</summary>
void BreakEven(Account leader, Instrument instrument, int bufferTicks);
```

### CHANGE 2 — `src/PropTraderTools/CopyEngine.cs` line 6871
Single-word change: `internal` → `public`. Body unchanged. CYC=5 unchanged.

### CHANGE 3 — `src/PropTraderTools/TradeCopierPanel.cs`
3a. Added `ExecuteBreakEven` static method after `DispatchShortcut` closing brace:
```csharp
// T-G1: ExecuteBreakEven -- testable BE dispatch seam (internal static, InternalsVisibleTo granted).
// CYC=1: straight-line call. No NT8 UI deps. JS-001: no throw. JS-021: no lock.
internal static void ExecuteBreakEven(
    ICopyEngine engine, Account leader, Instrument instrument, int beBuffer)
{
    engine.BreakEven(leader, instrument, beBuffer);
}
```

3b. Updated `DispatchShortcut` Key.B case:
- BEFORE: `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);`
- AFTER:  `ExecuteBreakEven(_engine, _leaderAccount, _instrument, _beBuffer);`

### CHANGE 4 — `src/PropTraderTools/CopyEngineTests.cs`
4a. Deleted lines 7735-7747: old `[Fact(Skip="NT8-runtime: CopyEngine.cctor requires NT8 host")]` test `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` and its leading comment.

4b. Added `LaneGTests` class after `LaneFTests` closing brace, before namespace closing brace.
Contains `StubEngine : ICopyEngine` and `[Fact] TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine`.

### BUILD FIX — `src/PropTraderTools/PropTraderTools.Tests.csproj`
Added `Core\PttContracts.cs` and all `Features\*.cs` files to the `<Compile>` list.
Root cause: `EnableDefaultCompileItems=false` and these files were absent from the list.
Before this change, the compiler resolved `ICopyEngine` from `NinjaTrader.Custom` (which did not have `BreakEven`).
Adding `PttContracts.cs` required also adding `Features/*.cs` to resolve the `IPttModule` type conflict.

---

## 7-Scan Results

### SCAN-01: `lock(` check
Command: `Select-String -Path src/PropTraderTools/*.cs, src/PropTraderTools/Core/*.cs -Pattern "lock\(" -SimpleMatch`
**Result: 0 matches ✓**

### SCAN-02: CYC audit

| Method | CYC | Limit | Result |
|--------|-----|-------|--------|
| `ExecuteBreakEven` | 1 | ≤8 | PASS |
| `DispatchShortcut` | 5 (unchanged) | ≤8 | PASS |
| `CopyEngine.BreakEven` | 5 (body unchanged) | ≤8 | PASS |
| `StubEngine.BreakEven` | 1 | ≤8 | PASS |
| `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` | 1 | ≤8 | PASS |

**Result: all methods ≤ CYC 8 ✓**

### SCAN-03: async/await check
Command: `Select-String -Path src/PropTraderTools/TradeCopierPanel.cs, src/PropTraderTools/Core/PttContracts.cs, src/PropTraderTools/CopyEngine.cs -Pattern "\basync\b|\bawait\b"`
**Result: 0 new async in new code (all hits are in pre-existing comments: "no async void", "not async void") ✓**

### SCAN-04: Non-ASCII byte scan
Command: PowerShell loop over all 4 modified files checking `[^\x00-\x7F]`
**Result: 0 non-ASCII characters ✓**

### SCAN-05: ATM API usage check
Visual inspection of all new code:
- `ICopyEngine.BreakEven` — interface declaration, no body
- `CopyEngine.BreakEven` — access modifier change only, body unchanged (pre-existing, no ATM)
- `ExecuteBreakEven` — calls `engine.BreakEven(...)` only
- `StubEngine` — stub methods only, no NT8 API calls
- `[Fact]` test — calls `ExecuteBreakEven` only

**Result: 0 ATM API usage in new code ✓**

### SCAN-06: Build + Test
Build command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj`
**Build result: Build succeeded. 0 errors.**

Test command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build`
**Test result: Passed! Failed: 0, Passed: 179, Skipped: 363, Total: 542, Duration: 842ms**

New `[Fact]` verification:
`dotnet test ... --filter "FullyQualifiedName~LaneGTests"` → **Passed: 1, Skipped: 0**

**Result: 179p/363s/542t, new [Fact] PASSES (not Skip) ✓**

### SCAN-07: Structural check

| Item | Result |
|------|--------|
| `ExecuteBreakEven` static seam exists in `TradeCopierPanel.cs` | ✓ (line 3071) |
| Old `[Fact(Skip="NT8-runtime: CopyEngine.cctor requires NT8 host")]` `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` deleted | ✓ |
| New `[Fact]` `TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine` passes (not Skip) | ✓ |
| DW-B26-01 CLOSED | ✓ |

**Result: all 4 structural items confirmed ✓**

---

## DW-B26-01 Closure Statement

**DW-B26-01 is CLOSED.**

The Option B reflection-based absence assertion (`TradeCopierPanel_BeBufferBox_FieldDoesNotExist`) has been replaced by the Option A-partial behavioral test (`TradeCopierPanel_ExecuteBreakEven_PassesBufferToEngine`). The new test exercises the break-even dispatch path directly via a static seam (`ExecuteBreakEven`) with a hand-rolled stub (`StubEngine : ICopyEngine`). No NT8 runtime is required. The test passes without `[Skip]`.

---

## Return Status

**BUILD_PASS**

All 7 scans: zero violations.
Build: 0 errors.
Test: 179 passed / 363 skipped / 0 failed / 542 total.
New `[Fact]`: PASSES (not Skip).
DW-B26-01: CLOSED.

---

*ptt-engineer · PTT-DEFERRED-0911 Lane G · Phase 4a · 2026-09-12*
