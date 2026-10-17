# B199 Ticket 3 Verification Report

**Ticket**: T3 -- DW-B198-CHECKREV-CYC-01: `CheckAtmTemplatesForReverse` CYC Extraction
**Epic**: B199 -- Direct Repair Promotion + B198 Deferred Backlog
**Verifier**: ptt-verifier
**Date**: 2026-10-14
**Phase**: 4b (Independent Verification)
**Engineer completion report**: docs/brain/B199/ticket-3-completion.md

---

## Verdict

**VERIFY_PASS**

DW-B198-CHECKREV-CYC-01: **CLOSED**

All 7 scans PASS. 13/13 tests GREEN. Extraction verified. Strict McCabe CYC=8 (at JS-066 ceiling).

---

## 1. Extraction Verification

**Command run independently**:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "TryParseAtmTemplateGroupQty|CheckAtmTemplatesForReverse"
```

**Results**:
```
CopyEngine.cs:424:    // Populated in CheckAtmTemplatesForReverse. Read in SendCopyWithAtm. ConcurrentDictionary -- no lock. JS-021.
CopyEngine.cs:1958:    // CheckAtmTemplatesForReverse: DW-B181-G5 -- scan ATM template XMLs for Reverse=Yes.
CopyEngine.cs:1963:    internal void CheckAtmTemplatesForReverse()
CopyEngine.cs:1964:        => CheckAtmTemplatesForReverse(
CopyEngine.cs:1969:    //         (7) if (TryParseAtmTemplateGroupQty) call-site branch. Removed T2b try/catch=-4, +1=net 8.
CopyEngine.cs:1971:    internal void CheckAtmTemplatesForReverse(string atmDir)
CopyEngine.cs:2011:                    if (TryParseAtmTemplateGroupQty(file, out int qty))
CopyEngine.cs:2028:    internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)
CopyEngine.cs:2049:    // Returns per-group qty for the named ATM template (populated by CheckAtmTemplatesForReverse)
```

**Extraction verification results**:

| Check | Expected | Actual | Status |
|-------|----------|--------|--------|
| `TryParseAtmTemplateGroupQty` declaration exists | line ~2028 | line 2028 | PASS |
| `TryParseAtmTemplateGroupQty` has `internal bool` visibility | `internal bool` | `internal bool` | PASS |
| `TryParseAtmTemplateGroupQty` placed after `CheckAtmTemplatesForReverse` | immediately after closing `}` | lines 2023-2046 | PASS |
| Call site exists in `CheckAtmTemplatesForReverse` | line ~2011 | line 2011 | PASS |
| Call site uses `if (TryParseAtmTemplateGroupQty(file, out int qty))` | exact | exact | PASS |
| Call site follows with `_atmTemplateGroupQty.TryAdd(name, qty)` | line 2012 | line 2012 | PASS |
| Old T2b try/catch qty-parse block REMOVED | not present | not present | PASS |
| Total grep matches (declaration + call site at minimum) | >= 2 | 2 (lines 2028, 2011) | PASS |

**Method body inspection (lines 2028-2046)**:

```csharp
internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)
{
    perGroupQty = 0;
    try
    {
        var doc = new System.Xml.XmlDocument();
        doc.Load(filePath);
        var qtyNode = doc.SelectSingleNode("//Quantity");
        if (qtyNode != null
            && int.TryParse(qtyNode.InnerText, out perGroupQty)
            && perGroupQty > 0)
            return true;
    }
    catch (Exception)
    {
        // Per-file qty-parse: skip unreadable/unparsable file. JS-001: no rethrow.
    }
    return false;
}
```

Method structure confirmed:
- `out int perGroupQty` param initialized to 0 at top: YES
- outer try/catch: YES
- XmlDocument.Load: YES
- SelectSingleNode("//Quantity"): YES
- compound guard (qtyNode != null && TryParse && > 0): YES
- `return true` inside guard: YES
- `catch (Exception)` silences (no rethrow, no throw new): YES (JS-001 PASS)
- `return false` at method bottom: YES

**Call site position note**: The call at line 2011 is placed OUTSIDE the inner per-file try/catch
(lines 1987-2009) but inside the outer try (line 1973) and foreach (line 1982). This is safe
because TryParseAtmTemplateGroupQty has its own try/catch and returns false on any exception;
the outer try at line 1973 also provides a safety net. JS-001 satisfied at both levels.

---

## 2. CYC Re-Verification (CRITICAL Gate -- DW-B198-CHECKREV-CYC-01)

### Lizard --warnings_only run (Layer 3 independent):

```
Command: python -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8 --warnings_only
```

**Output for T3 scope**:
```
src/PropTraderTools/CopyEngine.cs:1971: warning: TrimSignal::CheckAtmTemplatesForReverse has
  48 NLOC, 9 CCN, 249 token, 1 PARAM, 51 length, 0 ND
```

`CheckAtmTemplatesForReverse` (main overload, line 1971): Lizard CCN=9. Appears in warnings.
`TryParseAtmTemplateGroupQty` (line 2028): NOT in warnings (Lizard CCN=5, below 8 threshold).

### Full Lizard CCN output for both methods:

```
Command: python -m lizard src/PropTraderTools/CopyEngine.cs
         | Select-String "CheckAtmTemplatesForReverse|TryParseAtmTemplateGroupQty"
```

```
3      1     28      0       3  TrimSignal::CheckAtmTemplatesForReverse@1963-1965   (delegation overload, CCN=1)
48     9    249      1      51  TrimSignal::CheckAtmTemplatesForReverse@1971-2021   (main overload, CCN=9)
18     5     81      2      19  TrimSignal::TryParseAtmTemplateGroupQty@2028-2046   (CCN=5)
```

### Strict McCabe Analysis (JS-066 binding standard):

**`CheckAtmTemplatesForReverse` (lines 1971-2021) -- strict McCabe manual count**:

Verifier manually counted decision points from source read (lines 1971-2021):

| # | Decision Point | Location |
|---|----------------|----------|
| 1 | `catch (Exception ex)` outer catch | line 2015 |
| 2 | `if (!Directory.Exists(atmDir))` | line 1975 |
| 3 | `foreach (var file in ...)` loop | line 1982 |
| 4 | `if (_warnedAtmTemplates.Contains(name))` continue | line 1985 |
| 5 | `catch (Exception)` per-file inner | line 2005 |
| 6 | `if (!hasReverse)` continue | line 1995 |
| 7 | `if (TryParseAtmTemplateGroupQty(file, out int qty))` (T3 call site) | line 2011 |

Base = 1. Decision points = 7. **Strict McCabe CYC = 8.**

Note: The `||` operator in `hasReverse` assignment (lines 1992-1994) is counted by Lizard as an
additional branch (+1 = CCN 9), but strict McCabe (JS-066 standard) treats it as one compound
boolean expression, NOT a branch. This is the sole source of the Lizard vs McCabe discrepancy.
JS-066 ceiling is 8. Strict McCabe = 8. **PASS.**

**`TryParseAtmTemplateGroupQty` (lines 2028-2046) -- strict McCabe manual count**:

| # | Decision Point |
|---|----------------|
| 1 | `catch (Exception)` |
| 2 | compound `if (qtyNode != null && TryParse && > 0)` = 1 decision (strict McCabe, all `&&` = 1 branch) |

Base = 1. Decision points = 2. **Strict McCabe CYC = 3.**
Lizard CCN = 5 (counts both `&&` operators). Both values <= 8. **PASS.**

**DW-B198-CHECKREV-CYC-01: CLOSED.**
CheckAtmTemplatesForReverse(string): CYC reduced from 11 to 8 (strict McCabe). At JS-066 ceiling. PASS.

---

## 3. Test Verification

### Fact count (Layer 3 independent):

```
Command: (Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "\[Fact\]").Count
Result: 13
```

**Expected: 13. Actual: 13. PASS.**

### T3 test method names (Layer 3 independent):

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "T3_CHECKREV"
```

Results:
| Line | Method Name | Ticket Spec | Status |
|------|------------|-------------|--------|
| 178 | `T3_CHECKREV_01_TryParseAtmTemplateGroupQty_ValidXmlWithQuantity_ReturnsTrueQty5` | Exact match | PASS |
| 195 | `T3_CHECKREV_02_TryParseAtmTemplateGroupQty_XmlWithNoQuantityNode_ReturnsFalse` | Exact match | PASS |
| 210 | `T3_CHECKREV_03_TryParseAtmTemplateGroupQty_InvalidFilePath_ReturnsFalseNoThrow` | Exact match | PASS |

All 3 T3 test method names match ticket spec exactly. PASS.

---

## 4. Independent 7-Scan Results

### SCAN-01 -- lock() check

**CopyEngine.cs**:
```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("
```
Results: 9 matches, ALL are code comments ("no lock()", "without lock()", "no lock") --
zero actual `lock(` statements in executable code.

**B199Tests.cs**:
```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "lock\("
```
Result: 0 matches.

**Layer 2 (engineer)**: "0 matches in both files (all hits are comments only)"
**Layer 3 (verifier)**: CONFIRMED -- all hits are comment text only, no actual lock statements.
**SCAN-01: PASS** (JS-021 satisfied)

---

### SCAN-02 -- async void check

```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void"
```
Results: 2 matches, BOTH are comments:
- Line 2196: `// JS-033: Tick is not async void.`
- Line 7274: `// NOT async void (JS-033)`

Zero actual `async void` declarations. `TryParseAtmTemplateGroupQty` is synchronous `internal bool`.

**Layer 2 (engineer)**: "0 matches (all hits are comments)"
**Layer 3 (verifier)**: CONFIRMED.
**SCAN-02: PASS** (JS-033 satisfied)

---

### SCAN-03 -- CYC (primary gate)

See Section 2 above. Independent Lizard run confirms:
- `CheckAtmTemplatesForReverse`: Lizard CCN=9, Strict McCabe=8. At JS-066 ceiling. PASS.
- `TryParseAtmTemplateGroupQty`: Lizard CCN=5, Strict McCabe=3. Well under ceiling. PASS.

**Layer 2 (engineer)**: CheckAtmTemplatesForReverse CCN=9 (Lizard), McCabe=8. TryParseAtmTemplateGroupQty CCN=5 (Lizard).
**Layer 3 (verifier)**: CONFIRMED -- Lizard values match exactly.
**SCAN-03: PASS** (JS-066 satisfied, strict McCabe <= 8 for both methods)

---

### SCAN-04 -- ASCII check

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "[^\x00-\x7F]"
Result: 0 matches
```

**Layer 2 (engineer)**: "0 matches"
**Layer 3 (verifier)**: CONFIRMED.
**SCAN-04: PASS** (JS-081 satisfied)

---

### SCAN-05 -- xUnit-only check

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "\[Test\]|\[TestMethod\]"
Result: 0 matches
```

**Layer 2 (engineer)**: "0 matches"
**Layer 3 (verifier)**: CONFIRMED. All 13 tests use `[Fact]` only.
**SCAN-05: PASS**

---

### SCAN-06 -- throw new / DateTime.Now check

```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new"
Result: 0 matches

Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]"
Result: 1 match -- line 4732 (comment: "No DateTime.Now. No lock()") -- comment only, no actual use.
```

`TryParseAtmTemplateGroupQty` catch block confirmed: `// Per-file qty-parse: skip unreadable/unparsable file. JS-001: no rethrow.`
No `throw new` in method. No `throw` (rethrow). Returns `false` in catch path.

**Layer 2 (engineer)**: "0 matches for throw new"
**Layer 3 (verifier)**: CONFIRMED. No `throw new` anywhere in CopyEngine.cs. No DateTime.Now in executable code.
**SCAN-06: PASS** (JS-001 satisfied)

---

### SCAN-07 -- PTT sync

```
Command: powershell -File .\scripts\ptt-sync.ps1
Result: ptt-sync complete: 19 linked, 0 errors
```

All 19 source files hard-linked successfully.

**Layer 2 (engineer)**: "ptt-sync complete: 19 linked, 0 errors"
**Layer 3 (verifier)**: CONFIRMED -- exact same result.
**SCAN-07: PASS**

---

### SCAN-08 -- Guardian symbols

```
Command: powershell -File .\scripts\verify_critical_symbols.ps1
Result: GUARDIAN FAIL
  - TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
```

**Assessment**: This is the pre-existing DW-B198-GUARDIAN-01 carry-forward failure.
It exists in `TradeCopierPanel.cs`, NOT in any T3-modified scope (`CopyEngine.cs` / `B199Tests.cs`).
The failure predates B199 and is documented as a P2 carry-forward in `04-tickets.md` Summary:
"pre-existing DW-B198-GUARDIAN-01 failure is documented carry-forward; NOT a B199 blocker".

**SCAN-08: FAIL (pre-existing, NOT a T3 blocker -- documented carry-forward DW-B198-GUARDIAN-01)**

---

## 5. Deletion Audit (AGENTS.md Mandate)

```
Command: git diff -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
Result: 0 matches
```

No named symbols (methods, properties, fields) were deleted. The removed T2b block was an anonymous
inline try/catch, not a named method. No authorizing DW card required.

**Deletion audit: PASS**

---

## 6. Single-Hunk Ban

**Note**: B199 changes are uncommitted (CopyEngine.cs shows as modified in working tree, not in HEAD commit which is B194-DIRECT). Checked via working tree diff:

```
Command: (git diff -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
Result: 65
```

65 hunks in the cumulative working tree diff. Definitively NOT a single-hunk full-file replace.

**Single-hunk ban: PASS** (65 hunks, not 1)

---

## 7. dotnet Test Run

```
Command: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
         --filter "FullyQualifiedName~B199Tests" --verbosity quiet
Result: Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 206 ms
```

**13/13 PASS.** Pre-existing CS0436/CS8632 warnings (not introduced by T3).

---

## 8. Layer 2 vs Layer 3 Cross-Check

| Scan | Engineer L2 Claim | Verifier L3 Result | Match? |
|------|------------------|--------------------|--------|
| SCAN-01 lock() | "0 matches (comments only)" | 9 comment hits, 0 actual lock() | CONFIRMED |
| SCAN-02 async void | "0 matches (comments only)" | 2 comment hits, 0 actual async void | CONFIRMED |
| SCAN-03 CYC | CheckAtm CCN=9 (Lizard), McCabe=8; TryParse CCN=5 (Lizard) | CheckAtm CCN=9, TryParse CCN=5 | CONFIRMED |
| SCAN-04 ASCII | "0 matches" | 0 matches | CONFIRMED |
| SCAN-05 xUnit-only | "0 matches [Test]/[TestMethod]" | 0 matches | CONFIRMED |
| SCAN-06 throw new | "0 matches" | 0 matches | CONFIRMED |
| SCAN-07 ptt-sync | "19 linked, 0 errors" | 19 linked, 0 errors | CONFIRMED |

No discrepancies between engineer's Layer 2 self-report and verifier's independent Layer 3 results.

---

## 9. DNA Rule Check

| Rule | Scope | Check | Result |
|------|-------|-------|--------|
| JS-021 | `TryParseAtmTemplateGroupQty` | No `lock()`; caller handles ConcurrentDictionary.TryAdd | PASS |
| JS-021 | `CheckAtmTemplatesForReverse` | No `lock()`; only ConcurrentDictionary and ConcurrentBag ops | PASS |
| JS-001 | `TryParseAtmTemplateGroupQty` catch | `return false` in catch; no `throw new`, no rethrow | PASS |
| JS-001 | `CheckAtmTemplatesForReverse` inner catch | Comment-only catch: "skip unreadable file. no rethrow." | PASS |
| JS-001 | `CheckAtmTemplatesForReverse` outer catch | Logs to Output.Process, no rethrow | PASS |
| JS-002 | `TryParseAtmTemplateGroupQty` | Returns `false` (bool) on error, not null | PASS |
| JS-033 | `TryParseAtmTemplateGroupQty` | Synchronous `internal bool`, no async void | PASS |
| JS-066 | `CheckAtmTemplatesForReverse` | Strict McCabe CYC=8 (at ceiling). PASS. | PASS |
| JS-066 | `TryParseAtmTemplateGroupQty` | Strict McCabe CYC=3. Well under ceiling. | PASS |
| JS-081 | All string literals in T3 scope | XPath "//Quantity" is ASCII; all comments ASCII; no Unicode | PASS |
| JS-010 | `TryParseAtmTemplateGroupQty` | `internal` visibility (not public, no public constructor added) | PASS |

---

## 10. Architecture and Spec Compliance

### Spec Requirement
- **DW-B198-CHECKREV-CYC-01**: `CheckAtmTemplatesForReverse(string)` CYC=11 exceeds JS-066 ceiling of 8.
  Extract qty-parse block to reduce CYC.

### Compliance Check

| Requirement | Verified |
|-------------|----------|
| `TryParseAtmTemplateGroupQty` is `internal bool` (not private) | YES -- line 2028 |
| Method placed immediately after `CheckAtmTemplatesForReverse` closing `}` | YES -- lines 2023-2046 |
| Old T2b try/catch completely removed from `CheckAtmTemplatesForReverse` | YES -- confirmed absent |
| Call site uses `if (TryParseAtmTemplateGroupQty(file, out int qty))` | YES -- line 2011 |
| Call site follows with `_atmTemplateGroupQty.TryAdd(name, qty)` | YES -- line 2012 |
| `perGroupQty` out param initialized to 0 at method entry | YES -- line 2030 |
| `CheckAtmTemplatesForReverse` CYC reduced: 11 - 4 + 1 = 8 (strict McCabe) | YES |
| `TryParseAtmTemplateGroupQty` CYC = 3 (strict McCabe) | YES |
| Both CYC values <= JS-066 ceiling of 8 | YES |
| DW-B198-CHECKREV-CYC-01: CLOSED | YES |

---

## 11. Acceptance Criteria Status

| Criterion | Status |
|-----------|--------|
| 3 [Fact] tests GREEN (T3_CHECKREV_01/02/03) | PASS -- 13/13 total, 3 T3 tests confirmed GREEN |
| `CheckAtmTemplatesForReverse(string)` McCabe <= 8 | PASS -- CYC=8 (strict McCabe) |
| `TryParseAtmTemplateGroupQty` McCabe <= 3 | PASS -- CYC=3 (strict McCabe) |
| DW-B198-CHECKREV-CYC-01 CLOSED | CLOSED |
| SCAN-01 through SCAN-07 PASS | ALL PASS |
| SCAN-08 Guardian | Pre-existing DW-B198-GUARDIAN-01 carry-forward. NOT a T3 blocker. |

---

## 12. Notable Items

1. **Lizard CCN=9 vs strict McCabe=8 for `CheckAtmTemplatesForReverse`**: The `||` in the
   `hasReverse` boolean assignment (lines 1992-1994) causes Lizard to count an extra branch.
   This is a known Lizard behavior: it counts boolean operators in assignments as branches.
   Strict McCabe (the JS-066 standard) counts this as one compound boolean expression (not a
   separate decision). Strict McCabe=8. JS-066 ceiling is 8. **PASS.**

2. **Call site placement outside inner try/catch**: The T3 call site (lines 2010-2012) sits
   after the inner per-file try/catch closes (line 2009) rather than inside it. This is safe:
   `TryParseAtmTemplateGroupQty` has its own full try/catch and returns false on any exception.
   The outer try (line 1973) also provides a safety net. No escape path for exceptions.

3. **B199 not yet committed**: CopyEngine.cs shows as modified (uncommitted) in the working tree.
   All T1/T2/T3 changes are in working tree. The deletion audit and single-hunk ban were verified
   against the working tree diff (65 hunks; not 1 = single-hunk ban does not apply).

---

## Final Verdict

**VERIFY_PASS**

DW-B198-CHECKREV-CYC-01: **CLOSED**

- All 7 code scans: PASS
- All DNA rules: PASS
- Tests: 13/13 GREEN
- Extraction: CONFIRMED (declaration at line 2028, call site at line 2011)
- CYC: CheckAtmTemplatesForReverse strict McCabe=8 (at ceiling, PASS), TryParseAtmTemplateGroupQty strict McCabe=3
- Layer 2 vs Layer 3: No discrepancies found
- SCAN-08 Guardian failure is pre-existing DW-B198-GUARDIAN-01 carry-forward -- NOT a T3 blocker

---

*Written by: ptt-verifier*
*Epic: B199, Ticket 3*
*Phase: 4b (Independent Verification)*
*Source: docs/brain/B199/04-tickets.md (T3), docs/brain/B199/ticket-3-completion.md*
*Next: ptt-plan-reviewer (Phase 5 -- cross-file coherence)*