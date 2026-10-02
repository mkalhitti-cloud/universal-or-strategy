# B185 Ticket 2 Completion Report
## ptt-engineer Phase 4a

**Date**: 2026-07-08
**Epic**: B185 -- BE ALL Instrument Scope + DispatchCopy CYC Headroom + Click-Trader Qty + B184 Tests
**Ticket**: T2 -- DispatchCopy CYC Headroom Extraction
**DW Card**: DW-B184-01 (CLOSE)
**Brain dir**: `docs/brain/B185/`
**Engineer**: ptt-engineer
**Review basis**: `docs/brain/B185/04-ticket-review.md` -- T2 VERDICT: TICKET_REVIEW_PASS

---

## Files Modified

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Replace 2 inline `_lastLeaderDirection` usages in DispatchCopy; add 2 new private helper methods after DispatchCopy |
| `src/PropTraderTools/CopyEngineTests.cs` | Appended 3 new [Fact] tests (T_B185_04, T_B185_05, T_B185_06) in new `B185T2DispatchCopyTests` class at line 9465 |

---

## Changes to CopyEngine.cs

### Change 1: TryGetValue inline -> TryGetLastDirection helper call

**Location**: line 2472 (inside DispatchCopy)

**BEFORE** (lines 2472-2475):
```csharp
bool hasLastDirection = _lastLeaderDirection.TryGetValue(
    instr.FullName,
    out OrderAction lastAction
);
```

**AFTER** (line 2472):
```csharp
bool hasLastDirection = TryGetLastDirection(instr.FullName, out OrderAction lastAction);
```

3-line inline collapsed to 1 line via helper. Zero-branch extraction: no branches added or removed.

---

### Change 2: Indexer assignment inline -> RecordLastDirection helper call

**Location**: line 2507 (inside DispatchCopy, after the follower loop)

**BEFORE** (line 2510 original):
```csharp
_lastLeaderDirection[instr.FullName] = currentAction;
```

**AFTER** (line 2507):
```csharp
RecordLastDirection(instr.FullName, currentAction);
```

1-line inline replaced by helper call. Zero-branch extraction: no branches added or removed.

---

### New Helper Methods (added after DispatchCopy closing brace, lines 2510-2520)

```csharp
// CYC=1 (straight-line expression body). Lock-free: ConcurrentDictionary.TryGetValue is atomic.
// JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). JS-081: ASCII-only.
private bool TryGetLastDirection(string instrFullName, out OrderAction lastAction)
    => _lastLeaderDirection.TryGetValue(instrFullName, out lastAction);

// CYC=1 (straight-line expression body). Lock-free: ConcurrentDictionary indexer write is atomic.
// JS-021: no lock. JS-001: no throw. JS-081: ASCII-only.
private void RecordLastDirection(string instrFullName, OrderAction action)
    => _lastLeaderDirection[instrFullName] = action;
```

Both are private expression-body methods placed immediately after the closing brace of `DispatchCopy` and before the `TryLaneDHook` method.

---

## CYC Count: DispatchCopy After T2 Extraction

| Branch | Description | Count |
|--------|-------------|-------|
| 1 | `if (IsExitSignalName(order.Name))` -- Gate 0.5 | +1 |
| 2 | `if (!IsDispatchTriggerState(...))` -- Gate 3 | +1 |
| 3 | `if (!IsDispatchableOrderType(...))` -- Gate 4 | +1 |
| 4 | `if (IsLiveEntryBlocked(...))` -- Gate 5 dedup | +1 |
| 5 | `foreach (var acc in rule.FollowerAccounts)` -- loop | +1 |
| 6 | `if (ShouldSkipFollowerDispatch(acc))` -- follower skip | +1 |
| 7 | `if (ShouldSkipForReversalGuard(...))` -- reversal guard | +1 |
| **Total** | | **CYC = 8 (UNCHANGED)** |

T2 is a zero-branch extraction: no branches were added or removed from DispatchCopy. CYC remains at ceiling (8). DW-B184-01 satisfied.

| Method | CYC Before T2 | CYC After T2 | Ceiling | Status |
|--------|--------------|-------------|---------|--------|
| `DispatchCopy` | 8 | 8 (UNCHANGED) | 8 | PASS |
| `TryGetLastDirection` | (new) | 1 | 8 | PASS |
| `RecordLastDirection` | (new) | 1 | 8 | PASS |

---

## Tests Added to CopyEngineTests.cs

**Class**: `B185T2DispatchCopyTests` (new class, inserted at line 9465)

| Test Name | Line | Description |
|-----------|------|-------------|
| `TryGetLastDirection_ReturnsTrue_WhenKeyExists` | 9475 | Seeds "MES"->Buy via RecordLastDirection, verifies TryGetLastDirection returns true + correct value |
| `TryGetLastDirection_ReturnsFalse_WhenKeyAbsent` | 9507 | Verifies TryGetLastDirection returns false for key "UNKNOWN-INSTR-B185-T2-99" |
| `RecordLastDirection_WritesCorrectValue` | 9528 | Writes "MGC"->SellShort via RecordLastDirection, confirms read-back via TryGetLastDirection |

All 3 tests use `BindingFlags.NonPublic | BindingFlags.Instance` reflection pattern. No NT8 stubs required.

---

## 7-Scan Results

### SCAN-01: No lock() in new code
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "lock\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```
**Result: 0 matches** -- TryGetLastDirection and RecordLastDirection use ConcurrentDictionary (lock-free). No lock() added. ✅

### SCAN-02: No return null in new code
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "return null"
```
**Result: 0 new violations** -- All `return null` matches are pre-existing in the codebase. TryGetLastDirection returns `bool`; RecordLastDirection returns `void`. Neither new helper can return null. ✅

### SCAN-03: No async void in new code
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void "
```
**Result: 0 actual async void declarations** -- All matches are in comments documenting JS-033 compliance. No async keyword in any new T2 code. ✅

### SCAN-04: CYC <= 8 for all modified methods
**Manual branch count:**
- `DispatchCopy`: 7 branches = CYC 8 (UNCHANGED) -- zero-branch extraction, no branches added or removed ✅
- `TryGetLastDirection`: expression body, 0 branches = CYC 1 ✅
- `RecordLastDirection`: expression body, 0 branches = CYC 1 ✅

All methods <= 8. JS-066: PASS. ✅

### SCAN-05: No non-ASCII characters in new code
**New code verified ASCII-only:**
- Method names: `TryGetLastDirection`, `RecordLastDirection` -- ASCII ✅
- Parameter names: `instrFullName`, `lastAction`, `action` -- ASCII ✅
- No new string literals introduced in T2 ✅

**Result: 0 non-ASCII characters in new T2 code.** ✅

### SCAN-06: No throw new in new code
```
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "throw new " | Where-Object { $_.Line -notmatch "^\s*//" }
```
**Result: 1 pre-existing match** -- `TradeCopierWindow.cs:887` (NotImplementedException in AccountDisplayConverter -- pre-existing, not T2 code). No `throw new` in new T2 helper methods or test code. ✅

### SCAN-07: dotnet build 0 errors
```
dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental
```
**Result: Build succeeded. 0 errors.** 2 pre-existing warnings:
- CS0067 `CopyEngine.PendingBeArmed` never used (pre-existing, DW-B184-04 carry-forward)
- xUnit2004 in B131Tests.cs (pre-existing)

Neither warning is introduced by T2. ✅

---

## AC Verification

| AC | ID | Criterion | Result |
|----|-----|-----------|--------|
| AC1 | T2-AC1 | `TryGetLastDirection` and `RecordLastDirection` present, `private`, expression-body | PASS -- both present at CopyEngine.cs:2512-2520 |
| AC2 | T2-AC2 | `DispatchCopy` body no longer contains inline `_lastLeaderDirection.TryGetValue(...)` or `_lastLeaderDirection[...] = ` | PASS -- both replaced by helper calls |
| AC3 | T2-AC3 | `DispatchCopy` CYC = 8 (unchanged) | PASS -- 7 branches = CYC 8 |
| AC4 | T2-AC4 | `dotnet build` = 0 errors | PASS |
| AC5 | T2-AC5 | T_B185_04, T_B185_05, T_B185_06 present | PASS -- all 3 appended in B185T2DispatchCopyTests class |

**DW-B184-01**: CLOSE -- TryGetLastDirection and RecordLastDirection extraction complete.

---

## Scan Summary Table

| Scan | Description | Result |
|------|-------------|--------|
| SCAN-01 | `lock(` in new code | 0 ✅ |
| SCAN-02 | `return null` in new code | 0 new violations ✅ |
| SCAN-03 | `async void` in new code | 0 ✅ |
| SCAN-04 | CYC <= 8 (DispatchCopy=8, helpers=1) | PASS ✅ |
| SCAN-05 | Non-ASCII in new code | 0 ✅ |
| SCAN-06 | `throw new` in new code | 0 new violations ✅ |
| SCAN-07 | dotnet build | 0 errors ✅ |

---

## Result

**BUILD_PASS**

All 7 scans zero (no new violations). Build succeeded with 0 errors. 2 pre-existing warnings (not introduced by T2). DispatchCopy CYC unchanged at 8. DW-B184-01 CLOSE. 3 new [Fact] tests appended.

---

*Written by: ptt-engineer*
*Epic: B185*
*Ticket: T2*
*Phase: 4a (Engineer)*
*Source: docs/brain/B185/04-tickets.md (T2 section only)*
*Review basis: docs/brain/B185/04-ticket-review.md (T2 VERDICT: TICKET_REVIEW_PASS)*
*Return: BUILD_PASS*
