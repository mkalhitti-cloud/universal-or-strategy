# PTT-REPAIRS-DW-F-R06 — Architecture Plan
**Epic**: PTT-REPAIRS-DW-F-R06
**Phase**: 1 (Architecture)
**Status**: REVIEW_PENDING
**Architect**: PTT Architect (ptt-architect mode)
**Date**: 2025-01-01

---

## 0. LANE-SPLIT GATE RESULT

```
Q1. F1 and F2 within 50 lines of each other? YES (lines 727 and 738, delta=11)
Q2. Fix B design depends on Fix A final design?  NO
Q3. Each fix has standalone value if the other is blocked? YES
Q4. Each fix has an independent SIM verification path? YES
Gate rule: LANES require NO on Q1+Q2 AND YES on Q3+Q4.
Q1=YES -> lanes NOT approved.

LANE-SPLIT GATE RESULT: SINGLE-PIPELINE
```

Rationale: F1 and F2 are in the same file within 11 lines. All three fixes (F1, F2, F3) ship
in a single pipeline pass per the mission brief. No lane split.

---

## 1. Epic Summary

Three independent direct edits delivered in a single pipeline pass:

| Fix | File | Change Type | Line(s) |
|-----|------|-------------|---------|
| F1  | `src/PropTraderTools/CopyEngine.cs` | Doc-only comment | 727 |
| F2  | `src/PropTraderTools/CopyEngine.cs` | Doc-only comment | 738 |
| F3  | `src/PropTraderTools/CopyEngineTests.cs` | New [Fact] test | append ~8037 |

**Behavior change: NONE for F1/F2. No production logic change for F3.**

---

## 2. Fix Specifications

### F1 -- CopyEngine.cs line 727 (doc-only)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 727
**Current text**:
```
// PTT-REPAIRS-04 BUG-F: SetCloneAtmObjectCache -- per-instrument. CYC=1.
```
**Correct text**:
```
// PTT-REPAIRS-04 BUG-F: SetCloneAtmObjectCache -- per-instrument. CYC=2.
```
**Justification**: `SetCloneAtmObjectCache` (lines 730-736) has one `if/else` decision.
McCabe formula: base(1) + if-branch(1) = CYC=2. The original comment "CYC=1" was
authored incorrectly. Zero logic change.

### F2 -- CopyEngine.cs line 738 (doc-only)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 738
**Current text**:
```
// PTT-REPAIRS-04 BUG-F: GetCloneAtmMode -- per-instrument. CYC=2.
```
**Correct text**:
```
// PTT-REPAIRS-04 BUG-F: GetCloneAtmMode -- per-instrument. CYC=4.
```
**Justification**: `GetCloneAtmMode` (lines 741-753) has:
- base = 1
- Line 744: `if (TryGetValue(...) && atmObj != null)` = +1 (compound AND = 1 decision)
- Line 746: ternary `TryGetValue(...) ? tpl : string.Empty` = +1
- Line 750: `if (TryGetValue(...) && cache.Length > 0)` = +1
- Total: CYC=4

The original comment "CYC=2" was authored incorrectly. Zero logic change.

### F3 -- CopyEngineTests.cs (new [Fact] test)

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Test class**: `CopyEngineTests` (existing, line 16)
**Insert position**: After last `}` on line 8036 (last test body), before the two closing
braces at lines 8038-8039 (class `}` and namespace `}`)
**Test name**: `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`
**Attribute**: `[Fact]` (no Skip -- test does NOT require NT8 type construction)

#### Helper Verification (all confirmed present in CopyEngine.cs)

| Helper | Line | Signature |
|--------|------|-----------|
| `SetLeaderDirection_ForTest` | 4333 | `internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action)` |
| `IsLiveEntryBlocked_ForTest` | 4348 | `internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)` |
| `EvictDedup_ForTest` | 4360 | `internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state)` |
| `HasLeaderDirection` | 4330 | `internal bool HasLeaderDirection(string instrFullName)` |

**DISAMBIGUATION NOTE**: The mission brief references `HasLeaderDirection_ForTest` but the
actual method in production code at line 4330 is `HasLeaderDirection` (no `_ForTest` suffix).
The engineer MUST use `engine.HasLeaderDirection("MGC DEC26")` -- not `HasLeaderDirection_ForTest`.

**InternalsVisibleTo**: Confirmed at `CopyEngine.cs:46`:
```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]
```

#### Test Body (exact implementation for engineer)

```csharp
// PTT-REPAIRS-DW-F-R06 F3: verify EvictDedup clears _lastLeaderDirection on Cancelled.
// Covers PTT-REPAIRS-04 BUG-E path: cancelled entry order removes stale direction record.
// Uses InternalsVisibleTo seams declared at CopyEngine.cs:46.
// No NT8 type construction required -- seams operate on string keys and OrderState enum.
[Fact]
public void EvictDedup_CancelledEntry_ClearsLastLeaderDirection()
{
    // Arrange: record a leader direction for the instrument
    _engine.SetLeaderDirection_ForTest("MGC DEC26", OrderAction.Buy);

    // Arrange: simulate a dispatched entry (sets _liveEntryInstruments and _entryInstrKeyByOrderId)
    _engine.IsLiveEntryBlocked_ForTest("MGC DEC26|Buy", "ord-1", 0.0);

    // Act: order is cancelled -- EvictDedup must clear _lastLeaderDirection["MGC DEC26"]
    _engine.EvictDedup_ForTest("ord-1", NinjaTrader.Cbi.OrderState.Cancelled);

    // Assert: direction record must be cleared so next entry is not reversal-blocked
    Assert.False(_engine.HasLeaderDirection("MGC DEC26"));
}
```

#### Data Flow Proof

The test exercises this production path in `EvictDedup` (lines 5862-5883):
1. `OrderState.Cancelled` branch entered
2. `_entryInstrKeyByOrderId.TryRemove("ord-1", out cancelledInstrKey)` -> `"MGC DEC26|Buy"`
3. `_liveEntryInstruments` value-guarded removal for `"MGC DEC26|Buy"`
4. `pipeIdx = "MGC DEC26|Buy".IndexOf('|')` = 9 (valid > 0)
5. `_lastLeaderDirection.TryRemove("MGC DEC26", out _)` -- removes the direction
6. `HasLeaderDirection("MGC DEC26")` -> `_lastLeaderDirection.ContainsKey("MGC DEC26")` -> `false`

Assert.False is correct. The test assertion is mathematically guaranteed by the
production code at lines 5880-5882.

#### NT8 Runtime Risk

- `CopyEngine.Instance` is called at class field initialization (`_engine = CopyEngine.Instance`)
- This is the same access pattern as all other tests in CopyEngineTests that currently pass (19 passing)
- No new NT8 Account, Instrument, or AtmStrategy objects are constructed in F3
- **Expected outcome**: test PASSES (no TypeInitializationException in test host)
- **Contingency** (document in ticket-1-completion.md if triggered):
  Apply `[Fact(Skip="NT8-runtime: CopyEngine.cctor requires NT8 host")]`

---

## 3. Ticket Structure

### Ticket T1 -- CopyEngine.cs comment corrections (F1 + F2)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Edits**: 2 doc-only line changes
**Requires deploy-sync**: YES (CopyEngine.cs is hard-linked to NinjaTrader)

### Ticket T2 -- CopyEngineTests.cs new [Fact] test (F3)

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Edit**: Insert new test method after line 8036
**Requires deploy-sync**: YES (SCAN-6/7 verification required for all changed files)

---

## 4. Component List and Class Names

No new classes, interfaces, or fields introduced.

| Component | Change | File | Type |
|-----------|--------|------|------|
| `SetCloneAtmObjectCache` comment | CYC annotation corrected | CopyEngine.cs:727 | doc-only |
| `GetCloneAtmMode` comment | CYC annotation corrected | CopyEngine.cs:738 | doc-only |
| `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` | New [Fact] method | CopyEngineTests.cs | test-only |

---

## 5. Method Signatures

### Changed (CopyEngine.cs) -- comment context only, signatures unchanged

```csharp
// Line 730 -- no signature change:
internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)

// Line 741 -- no signature change:
internal FollowerAtmMode GetCloneAtmMode(string instrFullName)
```

### New (CopyEngineTests.cs)

```csharp
// Insert before closing } of CopyEngineTests class (after line 8036):
[Fact]
public void EvictDedup_CancelledEntry_ClearsLastLeaderDirection()
```

---

## 6. NinjaTrader 8 API Usage

| API | Usage in this epic | Namespace | AddOn-safe? |
|-----|--------------------|-----------|-------------|
| `OrderAction.Buy` | SetLeaderDirection_ForTest arg | NinjaTrader.Cbi | Yes (enum value only) |
| `OrderState.Cancelled` | EvictDedup_ForTest arg | NinjaTrader.Cbi | Yes (enum value only) |

No new NT8 API calls introduced in production code.
No AtmStrategyCreate, no Account.CreateOrder, no Dispatcher.InvokeAsync needed.

---

## 7. Threading Model

- F1/F2: comment-only -- no threading considerations.
- F3: test runs on xUnit test thread. All ForTest shims operate on ConcurrentDictionary
  (lock-free). No Dispatcher.InvokeAsync needed in test code.
- No lock() anywhere in any changed or added code.
- JS-021 (ConcurrentDictionary lock-free writes): upheld. No new lock() added.

---

## 8. JS Rule Compliance

| Rule | Requirement | F1 | F2 | F3 |
|------|-------------|----|----|-----|
| JS-021 | No lock() | PASS | PASS | PASS |
| JS-042 | ASCII-only | PASS | PASS | PASS |
| JS-013 | New helpers CYC=1 | N/A | N/A | N/A (no new helpers) |
| JS-001 | No throw in dispatch | N/A | N/A | N/A (test, not production) |
| JS-002 | No null return | N/A | N/A | N/A (test, returns void) |

No JS violations introduced.

---

## 9. 7-Scan Checklist (per ticket)

Both tickets T1 and T2 must pass all 7 scans before engineer marks completion.

```
SCAN-1: grep -r "lock(" src/PropTraderTools/CopyEngine.cs       -> 0 matches required
SCAN-2: grep -rP "[^\x00-\x7F]" src/PropTraderTools/            -> 0 matches in changed files
SCAN-3: dotnet build 2>&1 | grep " error "                      -> 0 matches
SCAN-4: dotnet build 2>&1 | Select-String "Error\(s\)"          -> "0 Error(s)"
SCAN-5: dotnet test --filter "FullyQualifiedName~CopyEngineTests" 2>&1
        -> passed >= 19 (20 if F3 passes; 19 if F3 skipped, failed unchanged at 449/448)
SCAN-6: powershell -File .\deploy-sync.ps1                       -> "SYNC COMPLETE"
SCAN-7: (Get-Item src\PropTraderTools\CopyEngine.cs).LinkType    -> "HardLink" or count=1
        (Get-Item src\PropTraderTools\CopyEngineTests.cs).LinkType -> "HardLink" or count=1
```

---

## 10. Verify Criteria (post-implementation checklist)

- [ ] `CopyEngine.cs:727` text contains "CYC=2" (not "CYC=1")
- [ ] `CopyEngine.cs:738` text contains "CYC=4" (not "CYC=2")
- [ ] `CopyEngineTests.cs` contains method `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`
- [ ] `dotnet build`: 0 Error(s)
- [ ] `dotnet test`: passed >= 19, no new genuine regressions
- [ ] `deploy-sync.ps1`: SYNC COMPLETE
- [ ] Hardlink count = 1 for both changed files (SCAN-7)

---

## 11. Baseline

```
dotnet test before this epic:
  passed: 19  |  failed: 449  |  skipped: 31

After F3 (if passes):
  passed: 20  |  failed: 448  |  skipped: 31

After F3 (if NT8-runtime Skip applied):
  passed: 19  |  failed: 449  |  skipped: 32
```

---

## 12. Deferred Items

None. This epic closes no prior deferred backlog items.
No new deferred items introduced.

---

**Return: PLAN_COMPLETE**
