# Ticket C-3 Verification Report

**Ticket**: C-3 - Test Name Inversions (5 Renames)
**Epic**: BWAVE-DW LaneC
**Branch**: `feature/bwave-dw-lane-c`
**Verifier**: ptt-verifier (independent Layer 3)
**Date**: 2026-09-04
**Verdict**: VERIFY_PASS

---

## 1. Rename Confirmations (5 of 5)

All 5 renames independently confirmed by reading the source file at the specified line ranges.

| # | DW Item | Line (approx.) | Old Name (inverted) | New Name (correct) | Status |
|---|---------|---------------|---------------------|-------------------|--------|
| 1 | DW-B37-02 | 433 | `IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat` | `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT` | CONFIRMED |
| 2 | DW-B37-04 | 546 | `IsNativeExitName_ReturnsTrue_WhenNameIsTarget` | `IsNativeExitName_ReturnsFalse_WhenNameIsTarget` | CONFIRMED |
| 3 | DW-B37-06 | 707 | `ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull` | `ResolveMultipliers_ReturnsNull_WhenMultipliersNull` | CONFIRMED |
| 4 | DW-B37-07 | 723 | `SelectRefPriceByDirection_ReturnsBid_WhenLongAndBidPositive` | `SelectRefPriceByDirection_ReturnsAsk_WhenLong` | CONFIRMED |
| 5 | DW-B37-08 | 752 | `SelectRefPriceByDirection_ReturnsAsk_WhenShortAndAskPositive` | `SelectRefPriceByDirection_ReturnsBid_WhenShort` | CONFIRMED |

---

## 2. Assertion Verification (per method body)

Each method body was read independently and assertion statements confirmed:

### DW-B37-02 (line 433): `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT`
```csharp
bool result = CopyEngine.IsPttBeRetryTriggerOrderTestable("PTT-QX-T1");
Assert.True(result);
```
- Assert statement: `Assert.True(result)` -- **CONFIRMED** (matches new name: ReturnsTrue)
- Body: unchanged (pure rename)

### DW-B37-04 (line 546): `IsNativeExitName_ReturnsFalse_WhenNameIsTarget`
```csharp
bool result = CopyEngine.IsNativeExitName("Target1");
Assert.False(result);
```
- Assert statement: `Assert.False(result)` -- **CONFIRMED** (matches new name: ReturnsFalse)
- Body: unchanged (pure rename)

### DW-B37-06 (line 707): `ResolveMultipliers_ReturnsNull_WhenMultipliersNull`
```csharp
var dto = new CopyEngine.CopyRuleDto { FollowerMultipliers = null };
int[] result = CopyEngine.ResolveMultipliers(dto);
Assert.Null(result);
```
- Assert statement: `Assert.Null(result)` -- **CONFIRMED** (matches new name: ReturnsNull)
- Body: unchanged (pure rename)

### DW-B37-07 (line 723): `SelectRefPriceByDirection_ReturnsAsk_WhenLong`
```csharp
double result = CopyEngine.SelectRefPriceByDirection(isLong: true, bid: 100.0, ask: 101.0);
Assert.Equal(101.0, result);
```
- Assert statement: `Assert.Equal(101.0, result)` -- **CONFIRMED** (101.0 = ask value, matches ReturnsAsk)
- Body: unchanged (pure rename)

### DW-B37-08 (line 752): `SelectRefPriceByDirection_ReturnsBid_WhenShort`
```csharp
double result = CopyEngine.SelectRefPriceByDirection(isLong: false, bid: 100.0, ask: 101.0);
Assert.Equal(100.0, result);
```
- Assert statement: `Assert.Equal(100.0, result)` -- **CONFIRMED** (100.0 = bid value, matches ReturnsBid)
- Body: unchanged (pure rename)

---

## 3. Old Names Absent Verification

**Pattern searched**:
```
IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat
IsNativeExitName_ReturnsTrue_WhenNameIsTarget
ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull
SelectRefPriceByDirection_ReturnsBid_WhenLongAndBidPositive
SelectRefPriceByDirection_ReturnsAsk_WhenShortAndAskPositive
```

**Result**: 0 matches for all 5 specific old names.

**Note on `ReturnsAllOnes`**: The broad pattern `ReturnsAllOnes` does appear at lines 687 and 690
in a comment and method named `ResolveMultipliers_ReturnsAllOnes_WhenLengthMismatch`. This is a
**distinct pre-existing test method** (different condition: length mismatch, not null) that was
NOT the rename target. The specific old target `ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull`
returns 0 matches -- correctly absent.

---

## 4. Independent 7-Scan Results (Layer 3)

All scans run independently against `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs`.

| Scan | Check | Command | Layer 3 Result | Notes |
|------|-------|---------|----------------|-------|
| SCAN-01 | No `lock()` code | `Select-String -Pattern "lock\("` | **4 comment-only matches** | Lines 107, 233, 325, 630 -- all in `// ... No lock(). ...` comment headers, not executable code |
| SCAN-02 | No `async void` | `Select-String -Pattern "async void"` | **0 matches** | PASS |
| SCAN-03 | No `return null;` | `Select-String -Pattern "return null;"` | **0 matches** | PASS |
| SCAN-04 | No `throw new` | `Select-String -Pattern "throw new"` | **0 matches** | PASS |
| SCAN-05 | CYC unchanged | Pure rename -- no branching logic modified | **PASS** | Method bodies byte-for-byte identical; no new decision points |
| SCAN-06 | ASCII-only | PowerShell byte scan | **3 non-ASCII bytes = UTF-8 BOM only** | Bytes 0xEF 0xBB 0xBF = file-level UTF-8 BOM, zero content non-ASCII |
| SCAN-07 | xUnit only | `Select-String -Pattern "using NUnit\|using Microsoft.VisualStudio"` | **0 matches** | PASS |

### SCAN-01 Detail: lock() in comments only
Lines 107, 233, 325, 630 each contain the comment `// ASCII-only. No DateTime.Now. No lock(). xUnit only.`
or similar. These are section-header comments documenting compliance -- not executable `lock(...)` statements.
Zero code-level lock() usage.

### SCAN-06 Detail: UTF-8 BOM
The file has a 3-byte UTF-8 BOM (EF BB BF) at position 0. This is the only non-ASCII content.
No content non-ASCII bytes exist. This is a pre-existing file characteristic; the renames added
zero non-ASCII content.

---

## 5. Layer 2 vs Layer 3 Comparison

| Check | Layer 2 (Engineer self-report) | Layer 3 (Verifier independent) | Match? |
|-------|-------------------------------|-------------------------------|--------|
| Rename 1 (DW-B37-02) | line 433, Assert.True | Confirmed line 433, Assert.True | MATCH |
| Rename 2 (DW-B37-04) | line 546, Assert.False | Confirmed line 546, Assert.False | MATCH |
| Rename 3 (DW-B37-06) | line 707, Assert.Null | Confirmed line 707, Assert.Null | MATCH |
| Rename 4 (DW-B37-07) | line 723, Assert.Equal(101.0) | Confirmed line 723, Assert.Equal(101.0, result) | MATCH |
| Rename 5 (DW-B37-08) | line 752, Assert.Equal(100.0) | Confirmed line 752, Assert.Equal(100.0, result) | MATCH |
| SCAN-01 lock() | 4 matches, comment-only | 4 matches, comment-only (lines 107,233,325,630) | MATCH |
| SCAN-02 async void | 0 | 0 | MATCH |
| SCAN-03 return null | 0 | 0 | MATCH |
| SCAN-04 throw new | 0 | 0 | MATCH |
| SCAN-05 CYC | PASS (rename only) | PASS (confirmed pure rename) | MATCH |
| SCAN-06 ASCII | 3 bytes = BOM only | 3 bytes = UTF-8 BOM (EF BB BF) | MATCH |
| SCAN-07 xUnit | 0 | 0 | MATCH |
| Old names absent | 0 matches | 0 for all 5 specific old names | MATCH |

**Discrepancies**: None. Layer 2 and Layer 3 results are in complete agreement.

---

## 6. DW Item Closure

| DW Item | Rename Applied | Assert Verified | Old Name Absent | Status |
|---------|---------------|-----------------|-----------------|--------|
| DW-B37-02 | IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT | Assert.True(result) | YES | CLOSED |
| DW-B37-04 | IsNativeExitName_ReturnsFalse_WhenNameIsTarget | Assert.False(result) | YES | CLOSED |
| DW-B37-06 | ResolveMultipliers_ReturnsNull_WhenMultipliersNull | Assert.Null(result) | YES | CLOSED |
| DW-B37-07 | SelectRefPriceByDirection_ReturnsAsk_WhenLong | Assert.Equal(101.0, result) | YES | CLOSED |
| DW-B37-08 | SelectRefPriceByDirection_ReturnsBid_WhenShort | Assert.Equal(100.0, result) | YES | CLOSED |

---

## 7. DNA Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (No lock) | 4 comment hits, zero code lock() | PASS |
| JS-001 (No throw new) | 0 throw new statements | PASS |
| JS-002 (No return null) | 0 return null statements | PASS |
| JS-010 (No public ctors) | No constructors added (pure rename) | PASS |
| ASCII-Only | UTF-8 BOM only; zero content non-ASCII bytes | PASS |
| xUnit-only | No NUnit/MSTest imports | PASS |
| CYC <= 8 | Pure rename; no branching logic modified | PASS |
| NT8 Sync | Not required (test files excluded per ticket SCOPE GATE) | N/A |

---

## Verdict

**VERIFY_PASS**

All 5 renames are correctly applied with matching assertions. All old inverted names are absent.
All 7 independent scans pass. Layer 2 and Layer 3 results match exactly. DW items DW-B37-02,
DW-B37-04, DW-B37-06, DW-B37-07, DW-B37-08 are closed.

---

*ptt-verifier | BWAVE-DW LaneC Ticket C-3 | VERIFY_PASS*