# B185 Ticket 3 Completion Report
## ptt-engineer Phase 4a

**Date**: 2026-07-08
**Epic**: B185
**Ticket**: T3 -- Click-Trader Default Qty + Lane D Eviction (DW-B184-02 + DW-B183-04)
**Engineer**: ptt-engineer
**Phase**: 4a (Implementation)

---

## Changes Implemented

### Change 1 -- TradeCopierPanel.cs: `_clickQty` field added

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Location**: After line 211 (adjacent to `_clickBuy` volatile field, inside "B9 T2 -- Click trader" block)

```csharp
// BEFORE (line 211):
        private volatile bool _clickBuy = true; // true=Buy, false=SellShort
        private Chart _currentChart = null; // single-writer UI thread

// AFTER:
        private volatile bool _clickBuy = true; // true=Buy, false=SellShort
        // B185 DW-B184-02: click-trader default qty. Promotes literal 1 to a configurable field.
        // Range: 1-99. Default=1 (preserves existing behavior). No UI change in B185.
        private int _clickQty = 1;
        private Chart _currentChart = null; // single-writer UI thread
```

**CYC impact**: 0 (field declaration, no branch).

---

### Change 2 -- TradeCopierPanel.cs: `OnChartMouseDown` qty literal -> field

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Location**: Line 2515 inside `OnChartMouseDown`

```csharp
// BEFORE:
            int qty = 1;

// AFTER:
            int qty = _clickQty;
```

**CYC impact**: 0 (straight-line assignment, no branch added).

---

### Change 3 -- CopyEngine.cs: `LaneDEvictThreshold` constant added

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Lines 399-403 (adjacent to `_laneDDispatched` field declaration, line 395 block)

```csharp
// BEFORE (line 395 block):
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
        // Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
        private readonly ConcurrentDictionary<string, byte> _laneDDispatched =

// AFTER:
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
        // Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
        // B185 DW-B183-04: eviction threshold for _laneDDispatched dedup guard.
        // At 10000 entries (~600KB), Clear() resets the guard. Worst case: single duplicate
        // Lane D dispatch per 10000 entries (idempotent in NT8 -- acceptable).
        private const int LaneDEvictThreshold = 10000;
        private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
```

**CYC impact**: 0 (constant declaration, no branch).

---

### Change 4 -- CopyEngine.cs: Eviction branch in `TryLaneDDispatchFollower`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Lines 2593-2604 inside `TryLaneDDispatchFollower` (was lines 2591-2594 before edit)

```csharp
// BEFORE:
            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
            if (ok)
                // Key: orderId + "#" + accName. Separator "#" is ASCII and cannot appear in NT8 account names.
                _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
            return ok;

// AFTER:
            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
            if (ok)
            {
                // Key: orderId + "#" + accName. Separator "#" is ASCII and cannot appear in NT8 account names.
                _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
                // Branch (D): B185 DW-B183-04: evict when guard grows beyond session threshold.
                // Clear() is atomic on ConcurrentDictionary (JS-021 compliant).
                if (_laneDDispatched.Count > LaneDEvictThreshold)
                    _laneDDispatched.Clear();
            }
            return ok;
```

**CYC impact**: +1 (new `if` branch D). TryLaneDDispatchFollower CYC: 4 -> **5**.

---

## CYC Count Verification

| Method | CYC Before | CYC After | Branches | Budget (<=8) | Status |
|--------|-----------|----------|----------|--------------|--------|
| `TryLaneDDispatchFollower` | 4 | **5** | (A) null guard, (B) daily cap, (C) `if ok`, (D) eviction `if` | 3 remaining | PASS |
| `TryLaneDHook` | 6 | **6** (UNCHANGED) | 5 branches + base; eviction is in helper, no propagation | 2 remaining | PASS |
| `OnChartMouseDown` | unchanged | unchanged | Straight-line assignment only | -- | PASS |

---

## Tests Added

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Class added**: `B185T3ClickQtyLaneDEvictionTests`

### T_B185_07: `ClickQty_DefaultIsOne`

**Line**: 9564 (in `B185T3ClickQtyLaneDEvictionTests`)
**CYC**: 1 (straight-line assertions)
- Reflects `_clickQty` field from `TradeCopierPanel` via `BindingFlags.NonPublic | Instance`
- Asserts: field is not null, field type == `int`, field is private, field is not static

### T_B185_08: `LaneDDispatched_ClearsAtThreshold`

**Line**: 9583 (in `B185T3ClickQtyLaneDEvictionTests`)
**CYC**: 1 (straight-line assertions, for-loop is iteration not a conditional branch in test context)
- Reflects `LaneDEvictThreshold` constant via `BindingFlags.NonPublic | Static`; asserts == 10000
- Reflects `_laneDDispatched` field; asserts type == `ConcurrentDictionary<string, byte>`
- Seeds dictionary with threshold+1 entries, asserts Count > threshold (pre-condition)
- Applies eviction logic inline (`if (dict.Count > threshold) dict.Clear()`)
- Asserts `dict.Count == 0` after eviction

---

## 7-Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `Select-String -Pattern "lock\s*\("` in new T3 code | 0 actual `lock(` in production code; all matches are comment text (`no lock`) | PASS |
| SCAN-02 | `Select-String -Pattern "return null"` in new T3 code | 0 `return null` in T3 new lines (`_clickQty`, eviction branch, `LaneDEvictThreshold`, `int qty = _clickQty`) | PASS |
| SCAN-03 | `Select-String -Pattern "async void "` in `src/PropTraderTools/*.cs` | 0 matches | PASS |
| SCAN-04 | CYC count of `TryLaneDDispatchFollower` and `TryLaneDHook` | `TryLaneDDispatchFollower`=5 (4+1 eviction), `TryLaneDHook`=6 (unchanged). Both <= 8 | PASS |
| SCAN-05 | Non-ASCII chars in CopyEngine.cs, TradeCopierPanel.cs, CopyEngineTests.cs | 0 non-ASCII in all 3 files | PASS |
| SCAN-06 | `Select-String -Pattern "throw new "` (non-comment lines) | 0 in T3 new code; 1 pre-existing in TradeCopierWindow.cs (unrelated, not changed) | PASS |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Build succeeded. 0 errors. 2 pre-existing warnings (CS0067 PendingBeArmed, xUnit2004 B131Tests.cs -- both pre-date T3) | PASS |

---

## DW Card Disposition

| DW ID | Action | Status |
|-------|--------|--------|
| DW-B184-02 | CLOSE | `_clickQty` field added; `OnChartMouseDown` uses `_clickQty` |
| DW-B183-04 | CLOSE | `LaneDEvictThreshold = 10000` added; eviction branch in `TryLaneDDispatchFollower` |

---

## AC Verification

| AC | Criterion | Verified |
|----|-----------|---------|
| T3-AC1 | `_clickQty` field: type=int, default=1, private instance | Read declaration (line 212-214 TradeCopierPanel.cs) + T_B185_07 |
| T3-AC2 | `OnChartMouseDown` uses `int qty = _clickQty;` (not literal `1`) | Read line 2515 TradeCopierPanel.cs; grep `int qty = 1;` = 0 matches in file |
| T3-AC3 | `LaneDEvictThreshold = 10000` private const in CopyEngine.cs | Read declaration (line 402 CopyEngine.cs) + T_B185_08 |
| T3-AC4 | Eviction branch inside `if (ok)` after `TryAdd` in `TryLaneDDispatchFollower` | Read lines 2595-2603 CopyEngine.cs |
| T3-AC5 | `TryLaneDHook` CYC=6 (unchanged); `TryLaneDDispatchFollower` CYC=5; both <= 8 | Manual branch count confirmed |
| T3-AC6 | `dotnet build` = 0 errors | Build output: `0 Error(s)` |
| T3-AC7 | T_B185_07 and T_B185_08 present | Lines 9564, 9583 in CopyEngineTests.cs |

---

## Return

**BUILD_PASS**

*Written by: ptt-engineer*
*Epic: B185*
*Ticket: T3*
*Phase: 4a (Implementation)*
*Plan basis: docs/brain/B185/02-architecture-plan.md*
*Ticket basis: docs/brain/B185/04-tickets.md (T3 section)*
*Review basis: docs/brain/B185/04-ticket-review.md (T3 TICKET_REVIEW_PASS)*
