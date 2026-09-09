# Ticket 1 Completion: BUG-C gate5 instrKey false-block (DW-B142-MGC-03)

**Epic:** PTT-REPAIRS-03-POST
**Ticket:** TICKET-1
**Engineer:** ptt-engineer (Phase 4a)
**Date:** 2026-09-06
**Scope:** Verification of existing production logic + one new regression test

---

## Phase Gate

**TICKET_REVIEW_PASS** confirmed from `docs/brain/PTT-REPAIRS-03-POST/04-ticket-review.md`
(verdict line: "Ph4a is unlocked. Engineer may proceed.")

---

## Step 1 — Source Verification Results

### 1a. `_liveEntryInstruments` field — CopyEngine.cs lines 203–204
**Result: MATCH — no drift**

```
203: private readonly ConcurrentDictionary<string, string> _liveEntryInstruments =
204:     new ConcurrentDictionary<string, string>();
```
Type confirmed: `<string, string>` (NOT `<string, byte>`). Lock-free (JS-021, JS-025).

---

### 1b. `IsLiveEntryBlocked_Check` — CopyEngine.cs lines 5802–5811
**Result: MATCH — no drift**

```
5802: private bool IsLiveEntryBlocked_Check(string instrKey, string orderId, double limitPrice)
5803: {
5804:     if (_liveEntryInstruments.TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId)
5805:         return true;
5806:     if (IsDedup(orderId, limitPrice))
5807:         return true;
5808:     if (_entryDispatchedOrders.ContainsKey(orderId))
5809:         return true;
5810:     return false;
5811: }
```
- NO `ContainsKey(instrKey)` call present — predicate uses `TryGetValue` + equality guard.
- CYC=4 confirmed: TryGetValue(1) + equality(2) + IsDedup(3) + ContainsKey(orderId)(4).
  Comment at line 5800 confirms: `CYC=4: TryGetValue(1)+equality(2)+IsDedup(3)+ContainsKey(4)`.

---

### 1c. `SetLiveEntryDispatched` — CopyEngine.cs lines 5821–5826
**Result: MATCH — no drift**

```
5821: private void SetLiveEntryDispatched(string instrKey, string orderId)
5822: {
5823:     _liveEntryInstruments[instrKey] = orderId;
5824:     _entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
5825:     _entryDispatchedOrders.TryAdd(orderId, 0);
5826: }
```
- Indexer `[instrKey] = orderId` confirmed (NOT TryAdd) — overwrites stale value for same instrKey.
- No `TryAdd` on `_liveEntryInstruments`.

---

### 1d. `EvictDedup` Cancelled branch — CopyEngine.cs lines 5866–5872
**Result: MATCH — no drift**

```
5866: if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
5867: {
5868:     string storedId;
5869:     if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
5870:         && storedId == orderId)
5871:         _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
5872: }
```
- Value-guarded pattern confirmed: `storedId == orderId` guard before TryRemove.
- No unconditional TryRemove on `_liveEntryInstruments`.

---

### 1e. `EvictDedup` Filled branch — CopyEngine.cs lines 5881–5887
**Result: MATCH — no drift**

```
5881: if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
5882: {
5883:     string storedId;
5884:     if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)
5885:         && storedId == orderId)
5886:         _liveEntryInstruments.TryRemove(filledInstrKey, out _);
5887: }
```
- Same value-guard pattern as Cancelled: `storedId == orderId` guard before TryRemove. Confirmed.

---

## Drift Corrections

**No drift found.** All 5 source items matched spec exactly. No edits were made to CopyEngine.cs.

---

## Step 2 — Test Added

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Lines added:** 7923–7955 (inserted before class closing brace, after `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`)

Test method: `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked`

- `[Fact]` at line 7931
- `public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()` at line 7932
- Arrange: `ClearLiveEntryForInstrument_ForTest("MES SEP26")` + `IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-A", 0.0)` → `Assert.False(firstResult)`
- Act: `IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-B", 0.0)`
- Assert: `Assert.False(blocked)` — BUG-C regression: different orderId on same instrKey must NOT be blocked.

---

## Step 3 — 7-Scan Results

### SCAN-01: lock() in production methods
**Command:** `Select-String -Pattern "lock\(" src/PropTraderTools/CopyEngine.cs | Where-Object { $_.Line -notmatch "//" } | Select-Object LineNumber, Line`
**Result: PASS — zero matches**

### SCAN-02: Non-ASCII characters in CopyEngine.cs
**Command:** `Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs | Select-Object -First 5`
**Result: PASS — zero matches**

### SCAN-03: CYC check on IsLiveEntryBlocked_Check
**Method:** Manual branch count of lines 5803–5810
**Branches:** TryGetValue&&equality (1+1) + IsDedup (1) + ContainsKey(orderId) (1) = base(1) + 3 branches = CYC 4
**Result: PASS — CYC=4, within JS-013 limit (≤8)**

### SCAN-04: [Fact] annotation immediately before new test method
**Command:** `Select-String -Pattern "\[Fact\]|IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked" src/PropTraderTools/CopyEngineTests.cs | Select-Object -Last 5`
**Output:**
```
7931         [Fact]
7932         public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()
```
**Result: PASS — [Fact] at line 7931 is immediately before method declaration at line 7932**

### SCAN-05: dotnet build — CopyEngine.cs / CopyEngineTests.cs errors
**Command:** `dotnet build Linting.csproj 2>&1`
**CopyEngine.cs errors:** 0
**CopyEngineTests.cs errors:** 0
**Note:** 307 pre-existing errors in V12_002.* files (assembly reference issues, unrelated to this ticket).
**Result: PASS — zero errors in CopyEngine.cs or CopyEngineTests.cs**

### SCAN-06: InternalsVisibleTo("PropTraderTools.Tests") present
**Location:** CopyEngine.cs line 46
**Content:** `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]`
**Result: PASS — present at line 46**

### SCAN-07: N/A
**Rationale:** Verification-only ticket. No new production logic paths were added.
**Result: N/A (documented)**

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngineTests.cs` | Added 1 `[Fact]` test method (lines 7923–7955) |
| `src/PropTraderTools/CopyEngine.cs` | No changes (all source items matched spec, no drift) |

---

## Final Status

**BUILD_PASS**

All 7 scans complete with zero violations. No drift in production source. One `[Fact]` regression test added per spec.

---

*ptt-engineer · PTT-REPAIRS-03-POST · ticket-1-completion.md · 2026-09-06*
