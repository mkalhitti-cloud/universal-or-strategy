# Ticket 1 Verification: BUG-C gate5 instrKey false-block (DW-B142-MGC-03)

**Epic:** PTT-REPAIRS-03-POST
**Ticket:** TICKET-1
**Verifier:** ptt-verifier (Phase 4b)
**Date:** 2026-09-06
**Role:** Independent Layer 3 verification — does NOT trust Ph4a results

---

## Phase Gate Pre-check

ticket-1-completion.md status line: **BUILD_PASS**
Confirmed at line 175: "**BUILD_PASS**"
Proceeding.

---

## VERIFY STEP 1 — SOURCE (independent reads)

### 1a. `_liveEntryInstruments` field — CopyEngine.cs lines 203–204

**Required:** `ConcurrentDictionary<string, string>` (not byte)

**Actual (lines 203–204):**
```
203:     private readonly ConcurrentDictionary<string, string> _liveEntryInstruments =
204:         new ConcurrentDictionary<string, string>();
```

**Result: MATCH** — Type is `<string, string>`. Lock-free (JS-021/JS-025 compliant).

---

### 1b. `IsLiveEntryBlocked_Check` — CopyEngine.cs lines 5802–5811

**Required:** `TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId` at line 5804; NO `ContainsKey(instrKey)` anywhere in body.

**Actual (lines 5800–5811):**
```
5800: // CYC=4: TryGetValue(1)+equality(2)+IsDedup(3)+ContainsKey(4). Within JS-013 limit.
5801: // JS-021: no lock. JS-001: no throw. JS-002: returns bool. JS-023: pure check path. ASCII-only.
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

**Line 5804 exact content:** `if (_liveEntryInstruments.TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId)`

**ContainsKey(instrKey) present?** NO — line 5808 uses `ContainsKey(orderId)` (orderId, not instrKey). Correct.

**Result: MATCH** — Predicate is TryGetValue+equality guard. BUG-C fix confirmed present.

---

### 1c. `SetLiveEntryDispatched` — CopyEngine.cs lines 5821–5826

**Required:** `_liveEntryInstruments[instrKey] = orderId` at line 5823; NO TryAdd on `_liveEntryInstruments`.

**Actual (lines 5821–5826):**
```
5821: private void SetLiveEntryDispatched(string instrKey, string orderId)
5822: {
5823:     _liveEntryInstruments[instrKey] = orderId;
5824:     _entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
5825:     _entryDispatchedOrders.TryAdd(orderId, 0);
5826: }
```

**Line 5823 exact content:** `_liveEntryInstruments[instrKey] = orderId;`

**TryAdd on _liveEntryInstruments?** NO — only TryAdd on `_entryInstrKeyByOrderId` (line 5824) and `_entryDispatchedOrders` (line 5825). Correct.

**Result: MATCH** — Indexer overwrite confirmed. Stale value for same instrKey is correctly overwritten.

---

### 1d. `EvictDedup` Cancelled branch — CopyEngine.cs lines 5866–5872

**Required:** value-guard `TryGetValue(cancelledInstrKey, out storedId) && storedId == orderId`; TryRemove only inside inner if.

**Actual (lines 5866–5872):**
```
5866: if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
5867: {
5868:     string storedId;
5869:     if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
5870:         && storedId == orderId)
5871:         _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
5872: }
```

**Lines 5868–5871 exact content:**
- 5868: `    string storedId;`
- 5869: `    if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)`
- 5870: `        && storedId == orderId)`
- 5871: `        _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);`

**Result: MATCH** — Value-guarded removal confirmed. TryRemove executes only inside inner if. TOCTOU-safe.

---

### 1e. `EvictDedup` Filled branch — CopyEngine.cs lines 5881–5887

**Required:** same value-guard pattern.

**Actual (lines 5881–5887):**
```
5881: if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
5882: {
5883:     string storedId;
5884:     if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)
5885:         && storedId == orderId)
5886:         _liveEntryInstruments.TryRemove(filledInstrKey, out _);
5887: }
```

**Lines 5883–5886 exact content:**
- 5883: `    string storedId;`
- 5884: `    if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)`
- 5885: `        && storedId == orderId)`
- 5886: `        _liveEntryInstruments.TryRemove(filledInstrKey, out _);`

**Result: MATCH** — Identical value-guard pattern as Cancelled branch. Confirmed.

---

## VERIFY STEP 2 — NEW TEST

**Search for:** `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked`

**Found at:** CopyEngineTests.cs line 7932

**Full test block (lines 7928–7952):**
```
7928:     // when orderId-B reaches Accepted on the same instrument+direction.
7929:     // Pre-fix behaviour (ContainsKey-only): blocked == true (regression).
7930:     // Post-fix behaviour (TryGetValue+equality): blocked == false (correct).
7931:     [Fact]
7932:     public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()
7933:     {
7934:         // Arrange: clear any residual state for this instrKey
7935:         _engine.ClearLiveEntryForInstrument_ForTest("MES SEP26");
7936:
7937:         // Arrange: prime _liveEntryInstruments with orderId-A for instrKey
7938:         // IsLiveEntryBlocked_ForTest: if not blocked, calls SetLiveEntryDispatched internally.
7939:         // This replicates DispatchCopy check+commit for orderId-A.
7940:         bool firstResult = _engine.IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-A", 0.0);
7941:         // Verify the arrange step: orderId-A must pass (instrKey was clean)
7942:         Assert.False(firstResult);
7943:         // _liveEntryInstruments["MES SEP26|Sell"] is now "orderId-A"
7944:
7945:         // Act: orderId-B arrives on the same instrKey (NT8 Cancelled for orderId-A not yet delivered)
7946:         bool blocked = _engine.IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-B", 0.0);
7947:
7948:         // Assert: different orderId on same instrKey must NOT be blocked (BUG-C fix)
7949:         // Simulates NT8 late-cancel scenario: orderId-A set but not yet evicted
7950:         // when orderId-B arrives on same instrKey. New order must pass gate5.
7951:         Assert.False(blocked);
7952:     }
```

**Checklist:**
- [x] `[Fact]` at line 7931 — immediately before method declaration at line 7932 (consecutive, no gap)
- [x] `ClearLiveEntryForInstrument_ForTest("MES SEP26")` at line 7935
- [x] `IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-A", 0.0)` ? `Assert.False(firstResult)` at lines 7940/7942
- [x] Act: `IsLiveEntryBlocked_ForTest("MES SEP26|Sell", "orderId-B", 0.0)` at line 7946
- [x] `Assert.False(blocked)` at line 7951

**Result: MATCH** — All required assertions present. BUG-C regression test confirmed.

---

## VERIFY STEP 3 — INDEPENDENT 7-SCAN RESULTS

### SCAN-01: lock() in production methods

**Command:** `Select-String -Pattern "lock\(" src/PropTraderTools/CopyEngine.cs | Where-Object { $_.Line -notmatch "//" } | Select-Object LineNumber, Line`

**Output:** (no output — zero matches)

**Engineer Ph4a report:** PASS — zero matches
**Layer 3 result:** PASS — zero matches
**Discrepancy:** NONE

---

### SCAN-02: Non-ASCII characters in CopyEngine.cs

**Command:** `Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs | Select-Object -First 5`

**Output:** (no output — zero matches)

**Engineer Ph4a report:** PASS — zero matches
**Layer 3 result:** PASS — zero matches
**Discrepancy:** NONE

---

### SCAN-03: CYC check on IsLiveEntryBlocked_Check (lines 5803–5810)

**Method:** Independent branch count from directly-read source.

| Decision point | Line | Count |
|---|---|---|
| base path | — | 1 |
| `TryGetValue(instrKey, out var liveOrderId)` | 5804 | +1 |
| `&& liveOrderId == orderId` | 5804 | +1 |
| `IsDedup(orderId, limitPrice)` | 5806 | +1 |
| `_entryDispatchedOrders.ContainsKey(orderId)` | 5808 | +1 |
| **Total** | | **CYC = 4** |

**Engineer Ph4a report:** PASS — CYC=4
**Layer 3 result:** PASS — CYC=4 (= JS-013 limit of 8)
**Discrepancy:** NONE

---

### SCAN-04: [Fact] annotation immediately before new test method

**Command:** `Select-String -Pattern "\[Fact\]|IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked" src/PropTraderTools/CopyEngineTests.cs | Select-Object -Last 5`

**Output:**
```
7836         [Fact]
7858         [Fact]
7893         [Fact]
7931         [Fact]
7932         public void IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked()
```

`[Fact]` at line 7931 is on the line immediately preceding method declaration at line 7932. No intervening lines.

**Engineer Ph4a report:** PASS — [Fact] at line 7931, method at 7932
**Layer 3 result:** PASS — confirmed identical
**Discrepancy:** NONE

---

### SCAN-05: dotnet build — CopyEngine.cs / CopyEngineTests.cs errors

**Command:** `dotnet build Linting.csproj 2>&1 > build_out_verify.txt`
Then: `Select-String -Path build_out_verify.txt -Pattern "CopyEngine" | Select-Object LineNumber, Line`
Then: `Select-String -Path build_out_verify.txt -Pattern " error " | Where-Object { $_.Line -notmatch "V12_002" }`

**CopyEngine.cs errors:** 0
**CopyEngineTests.cs errors:** 0
**Total build errors not matching V12_002:** 0
**Note:** 307 pre-existing V12_002.* assembly reference errors (unrelated to this ticket, present before ticket work).

**Engineer Ph4a report:** PASS — zero errors in CopyEngine.cs or CopyEngineTests.cs; 307 pre-existing V12_002.* errors
**Layer 3 result:** PASS — confirmed identical
**Discrepancy:** NONE

---

### SCAN-06: InternalsVisibleTo("PropTraderTools.Tests") present

**Read:** CopyEngine.cs lines 44–46

**Actual:**
```
44: // B113 test seam: grants PropTraderTools.Tests access to internal members
45: // (_qxPendingFollowerCleanup, TryCleanupReArmedAtmBracket).
46: [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]
```

**Engineer Ph4a report:** PASS — present at line 46
**Layer 3 result:** PASS — confirmed at line 46
**Discrepancy:** NONE

---

### SCAN-07: N/A

**Rationale:** Verification-only ticket. No new production logic paths added to CopyEngine.cs.
**Engineer Ph4a report:** N/A (documented)
**Layer 3 result:** N/A — confirmed

---

## Comparison: Ph4a Layer 2 vs Layer 3 (this report)

| Item | Ph4a Report | Layer 3 (Independent) | Discrepancy |
|------|-------------|----------------------|-------------|
| 1a field type | ConcurrentDictionary<string,string> | ConcurrentDictionary<string,string> | NONE |
| 1b line 5804 predicate | TryGetValue+equality | TryGetValue+equality | NONE |
| 1b no ContainsKey(instrKey) | confirmed | confirmed | NONE |
| 1c line 5823 indexer | _liveEntryInstruments[instrKey]=orderId | _liveEntryInstruments[instrKey]=orderId | NONE |
| 1c no TryAdd on _liveEntryInstruments | confirmed | confirmed | NONE |
| 1d value-guard (Cancelled) | lines 5868-5871 | lines 5868-5871 | NONE |
| 1e value-guard (Filled) | lines 5883-5886 | lines 5883-5886 | NONE |
| Test method line | 7932 | 7932 | NONE |
| [Fact] line | 7931 | 7931 | NONE |
| SCAN-01 lock() | 0 matches | 0 matches | NONE |
| SCAN-02 non-ASCII | 0 matches | 0 matches | NONE |
| SCAN-03 CYC | 4 | 4 | NONE |
| SCAN-04 [Fact] present | line 7931 | line 7931 | NONE |
| SCAN-05 build errors | 0 (CopyEngine) | 0 (CopyEngine) | NONE |
| SCAN-06 InternalsVisibleTo | line 46 | line 46 | NONE |

**All Ph4a self-reported results verified independently. Zero discrepancies.**

---

## DNA Rule Audit

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: zero lock() matches | PASS |
| JS-023 (no Monitor/Mutex for state) | No Monitor/Mutex in new methods | PASS |
| JS-025 (ConcurrentDictionary not plain Dictionary) | _liveEntryInstruments is ConcurrentDictionary<string,string> | PASS |
| JS-001 (no throw in gate methods) | No throw in IsLiveEntryBlocked_Check, SetLiveEntryDispatched, EvictDedup | PASS |
| JS-002 (no null return where non-null expected) | Methods return bool/void | PASS |
| JS-008/JS-009 (immutability) | No SolidColorBrush, no mutable struct across threads | PASS |
| JS-013 (CYC <= 8) | IsLiveEntryBlocked_Check CYC=4 | PASS |
| ASCII-only | SCAN-02: zero non-ASCII | PASS |
| NT8: no async/await in lifecycle methods | Not applicable to this ticket | N/A |
| NT8: no sealed on TradeCopierWindow | Not applicable | N/A |
| NT8: no FontFamily= | Not in scope of changes | N/A |
| NT8: no #RRGGBB hex colors | Not in scope of changes | N/A |
| NT8: CreateOrder PTT- prefix | No CreateOrder in changed methods | N/A |
| NT8: DateTime.UtcNow not Now | No DateTime.Now in changed methods | N/A |

---

## Files Changed

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | No changes (all source verified as correct — no drift) |
| `src/PropTraderTools/CopyEngineTests.cs` | 1 `[Fact]` regression test added (lines 7923–7952) |

---

## Final Verdict

**VERIFY_PASS**

All 5 source verification items confirmed exact match with spec.
New regression test present with all required assertions.
All 6 applicable scans pass (SCAN-07 N/A).
Zero discrepancies between Ph4a Layer 2 self-report and independent Layer 3 verification.
Zero DNA rule violations found.

---

*ptt-verifier · PTT-REPAIRS-03-POST · ticket-1-verification.md · 2026-09-06*
