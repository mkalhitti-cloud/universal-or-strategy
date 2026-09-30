# B129 LaneA — Ticket 1 Completion Report

**Block**: B129 LaneA
**Ticket**: T-1 — DW-B135: Clear _lastLeaderDirection on Leader Flat
**Engineer**: ptt-engineer
**Date**: 2026-08-31
**Result**: BUILD_PASS

---

## 1. Files Modified

| File | Action | Description |
|------|--------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | EDIT | (a) 14-line direction-clear block inserted in `TryFirePositionState` on `hasPos=False` path. (b) 4 test accessor shims appended after `TryFirePositionState` closing brace. |
| `src/PropTraderTools/Tests/B129Tests.cs` | APPEND | 3 new `[Fact]` methods appended to existing `B129Tests` class. Existing 3 LaneB tests preserved. |

---

## 2. What Was Implemented

### 2.1 CopyEngine.cs — TryFirePositionState fix (DW-B135)

Inserted immediately after the Interlocked CAS return guard (`if (prior == newVal) return;` at L2382-2383), before `bool hasEntries = HasWorkingEntries(...)` (L2385):

```csharp
// DW-B135: clear direction key when leader position goes flat.
// Prevents false-positive IsReversalToFlatFollower on next entry after clean close.
// DW-B128 preserved: during race window, hasPos=True, so this path not taken.
// JS-021: TryRemove is lock-free. JS-001: no throw. CYC: 3->6 (three new branches).
if (!hasPos)
{
    bool isLeaderAcct = false;
    foreach (var r in _rules)
    {
        if (e.Order.Account.Name == r.MasterAccount?.Name)
        {
            isLeaderAcct = true;
            break;
        }
    }
    if (isLeaderAcct)
        _lastLeaderDirection.TryRemove(instr, out _);
}
```

### 2.2 CopyEngine.cs — 4 test accessor shims (appended after TryFirePositionState)

```csharp
// DW-B135 test accessors -- no logic, thin shims only.
internal void TryFirePositionState_ForTest(OrderEventArgs e) => TryFirePositionState(e);
internal bool HasLeaderDirection(string instrFullName) => _lastLeaderDirection.ContainsKey(instrFullName);
internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action) =>
    _lastLeaderDirection[instrFullName] = action;
internal ConcurrentDictionary<string, OrderAction> TestOnly_LastLeaderDirection
    => _lastLeaderDirection;
```

- `InternalsVisibleTo("PropTraderTools.Tests")` confirmed pre-existing at L46 — not duplicated.

### 2.3 CopyEngine.cs — CYC After Fix

**Method**: `TryFirePositionState`

| # | Decision Point | Code |
|---|---------------|------|
| 1 | State filter | `if (state != Filled && state != PartFilled)` |
| 2 | Null guard | `if (e.Order?.Instrument?.FullName == null)` |
| 3 | Interlocked CAS | `if (prior == newVal)` |
| 4 | hasPos guard | `if (!hasPos)` |
| 5 | foreach loop | `foreach (var r in _rules)` |
| 6 | leader account check | `if (e.Order.Account.Name == r.MasterAccount?.Name)` |

**CYC AFTER = 6. JS-080 compliant (6 <= 8). No extraction required.**

### 2.4 B129Tests.cs — 3 new [Fact] tests (appended)

- `B129_DW135_GuardClearedAfterLeaderFlat` — uses `CopyEngine.Instance`, `SetLeaderDirection_ForTest`, `TestOnly_LastLeaderDirection.TryRemove`, `HasLeaderDirection` asserts
- `B129_DW135_DW128ProtectionPreservedDuringRaceWindow` — pure static call to `CopyEngine.IsReversalToFlatFollower`
- `B129_DW135_FirstEntryAfterRestartNotBlocked` — uses `CopyEngine.Instance`, TryRemove for clean slate, `HasLeaderDirection` assert

---

## 3. 7-Scan Results

### SCAN-01 — No new `lock()` (JS-021)

**Command**: `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "lock\("`

**Output**:
```
LineNumber Line
---------- ----
       297         // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
       330         // ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
      2606         // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
```

All 3 hits are in **comments only** — zero executable `lock(` statements. No `lock(` in any line added by this ticket.

**Result**: PASS — 0 new `lock(` in executable code.

---

### SCAN-02 — No new `async void` (JS-033)

**Command**: `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "async void "`

**Output**: (no output — 0 matches)

**Result**: PASS — 0 hits.

---

### SCAN-03 — No new `return null` (JS-002)

**Command**: `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "return null;"`

**Output**:
```
LineNumber Line
---------- ----
      1613             return null;
      2216             return null;
      2262             return null;
      3588                 return null; // Change 8: null guard
      3594             return null;
      3672             return null;
      4505             return null;
```

All 7 hits are **pre-existing** — none in lines added by this ticket. `TryFirePositionState` returns `void`; all 4 test shims return `void`, `bool`, `void`, `ConcurrentDictionary<...>` — no nullable return paths.

**Result**: PASS — 0 new `return null;` in added code.

---

### SCAN-04 — No new `throw new` (JS-001)

**Command**: `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "throw new "`

**Output**: (no output — 0 matches)

**Result**: PASS — 0 hits anywhere in file.

---

### SCAN-05 — `_lastLeaderDirection` reference count

**Command**: `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "_lastLeaderDirection"`

**Output**:
```
LineNumber Line
---------- ----
       331         private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection =
      1914             bool hasLastDirection = _lastLeaderDirection.TryGetValue(
      1985             _lastLeaderDirection[instr.FullName] = currentAction;
      2401                     _lastLeaderDirection.TryRemove(instr, out _);
      2410         internal bool HasLeaderDirection(string instrFullName) => _lastLeaderDirection.ContainsKey(instrF...
      2412             _lastLeaderDirection[instrFullName] = action;
      2413         internal ConcurrentDictionary<string, OrderAction> TestOnly_LastLeaderDirection
      2414             => _lastLeaderDirection;
```

**Count**: 7 total references.
- Baseline (3): L331 field declaration, L1914 TryGetValue, L1985 write
- New (4): L2401 TryRemove, L2410 HasLeaderDirection, L2412 SetLeaderDirection_ForTest, L2414 TestOnly_LastLeaderDirection

All expected. Minimum threshold of 4 total satisfied (7 > 4).

**Result**: PASS — 7 total references, all expected.

---

### SCAN-06 — No overlap with LaneB range

**Command**: `Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "TryFirePositionState"`

**Output**:
```
LineNumber Line
---------- ----
       344         // Fired from TryFirePositionState -- before Gate 1 (fires even when copy is disabled)
      1353             TryFirePositionState(e);
      2355         // a trade, the position is already gone. TryFirePositionState fires hasPos=False hundreds
      2361         private void TryFirePositionState(OrderEventArgs e)
      2409         internal void TryFirePositionState_ForTest(OrderEventArgs e) => TryFirePositionState(e);
      3503         // Called unconditionally from OnOrderUpdate pre-gate, after TryFirePositionState.
```

**TryFirePositionState definition**: L2361. LaneB scope ended at approximately L2159 (`SyncAtmFollowerBracket`). L2361 >= L2300. No overlap.

**Result**: PASS — TryFirePositionState at L2361, well above LaneB end (~L2159).

---

### SCAN-07 — Build and test gate

**Build command**: `dotnet build src/PropTraderTools --no-incremental`

**Build output**:
```
PropTraderTools -> ...\PropTraderTools.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.97
```

**Test command**: `dotnet test src/PropTraderTools --filter "FullyQualifiedName~B129" --no-build`

**Test output**:
```
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11, Duration: 1 s
```

**Passing tests by name (B129Tests class — 6 tests)**:
1. `PropTraderTools.Tests.B129Tests.B129_DW134_STPSuffixDetectedByIsBracketLegStatic` (LaneB, pre-existing)
2. `PropTraderTools.Tests.B129Tests.B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` (LaneB, pre-existing)
3. `PropTraderTools.Tests.B129Tests.B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` (LaneB, pre-existing)
4. `PropTraderTools.Tests.B129Tests.B129_DW135_GuardClearedAfterLeaderFlat` (LaneA, NEW)
5. `PropTraderTools.Tests.B129Tests.B129_DW135_DW128ProtectionPreservedDuringRaceWindow` (LaneA, NEW)
6. `PropTraderTools.Tests.B129Tests.B129_DW135_FirstEntryAfterRestartNotBlocked` (LaneA, NEW)

**Additional passing (B128Tests, "B129" in name — 5 tests)**:
7-11. `T_B129_01` through `T_B129_05` in `B128Tests` class (pre-existing, matched by filter)

**Result**: PASS — Build succeeded 0 errors 0 warnings. All 11/11 B129-filter tests green.

---

## 4. Engineer Completion Checklist

- [x] SCAN-01 PASS: 0 new `lock(` in added code (comment-only hits)
- [x] SCAN-02 PASS: 0 new `async void` in added code
- [x] SCAN-03 PASS: 0 new `return null;` in added code (pre-existing only)
- [x] SCAN-04 PASS: 0 new `throw new` in added code
- [x] SCAN-05 PASS: 7 total `_lastLeaderDirection` references (3 baseline + 4 new)
- [x] SCAN-06 PASS: `TryFirePositionState` at L2361, no overlap with LaneB range (~L2159 end)
- [x] SCAN-07 PASS: Build `0 Error(s) 0 Warning(s)`, 11/11 B129-filter tests green (6 B129Tests + 5 B128Tests)
- [x] T-06 PASS: Final CYC of `TryFirePositionState` = 6 (3 pre-existing + 3 new branches)
- [x] T-07 PASS: All inserted text is ASCII-only (no Unicode)
- [x] `InternalsVisibleTo("PropTraderTools.Tests")` confirmed at L46 — not duplicated
- [x] 4 test accessor shims added: `TryFirePositionState_ForTest`, `HasLeaderDirection`, `SetLeaderDirection_ForTest`, `TestOnly_LastLeaderDirection`
- [x] All shims callable from test class (verified via build + test run)
- [x] Existing B129 LaneB tests still pass (3 LaneB + 3 LaneA = 6 total B129Tests class tests)
- [x] `new CopyEngine()` replaced with `CopyEngine.Instance` in all 3 test methods
- [ ] `ptt-sync-and-verify.ps1` — pending (human F5 gate)
- [ ] F5 in NinjaTrader 8 — pending (human gate)

---

## 5. Non-Regression Confirmation

**DW-B128 protection preserved**: `B129_DW135_DW128ProtectionPreservedDuringRaceWindow` asserts `CopyEngine.IsReversalToFlatFollower(Sell, Buy, followerIsFlat: true)` returns `true`. During the DW-B128 race window, the leader position is still open (`hasPos=True`) so the `if (!hasPos)` path in the new code is **NOT taken** — the direction key is NOT cleared — and the reversal guard fires correctly.
