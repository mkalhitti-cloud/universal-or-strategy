# B192 Ticket 1 Verification Report

**Ticket**: T1 -- InstrumentDefaults.GetQuickTicks
**Epic**: B192
**Phase**: 4b (PTT Verifier)
**Verifier**: ptt-verifier
**Date**: 2026-07-15
**Files Verified**:
- `src/PropTraderTools/Features/PttQuickExit.cs` (READ-ONLY)
- `src/PropTraderTools/CopyEngineTests.cs` (READ-ONLY)
**Spec ref**: `docs/brain/B192/04-tickets.md` (T1 section)
**Engineer report**: `docs/brain/B192/ticket-1-completion.md`

---

## 7-SCAN RESULTS (Layer 3 -- Independent Verifier Runs)

All scans run independently against `src/PropTraderTools/Features/PttQuickExit.cs`.
Verifier NEVER trusts engineer self-report -- all scans executed and observed directly.

| Scan | Pattern | Verifier Result | Engineer Claim | Match? |
|------|---------|-----------------|----------------|--------|
| SCAN-01 | `lock\s*\(` | **0 matches** | 0 matches | YES |
| SCAN-02 | `throw\s+new\s+\w+Exception\(` | **0 matches** | 0 matches | YES |
| SCAN-03 | `return\s+null\s*;` | **0 matches** | 0 matches | YES |
| SCAN-04 | `DateTime\.Now[^U]` | **0 matches** | 0 matches | YES |
| SCAN-05 | `#[0-9A-Fa-f]{6}` | **0 matches** | 0 matches | YES |
| SCAN-06 | `FontFamily` | **0 matches** | 0 matches | YES |
| SCAN-07 | Non-ASCII bytes (>0x7F) | **0 bytes** | 0 bytes | YES |

**No discrepancies between Layer 2 (engineer) and Layer 3 (verifier).** All scans PASS.

---

## IMPLEMENTATION VERIFICATION

### Q1: Does GetQuickTicks have 7 branches?
YES. Lines 389-403 in `PttQuickExit.cs`:
- (1) `string.IsNullOrEmpty(masterName)` -> `(4, 8)` [line 389]
- (2) `StartsWith("MES")` -> `(4, 8)` [line 391]
- (3) `StartsWith("ES")` -> `(4, 8)` [line 393] B192-T1 new
- (4) `StartsWith("NQ")` -> `(4, 8)` [line 395] B192-T1 new
- (5) `StartsWith("MNQ")` -> `(4, 8)` [line 397] B192-T1 new
- (6) `StartsWith("MGC")` -> `(10, 20)` [line 399] regression
- (7) `StartsWith("GC")` -> `(10, 20)` [line 401] B192-T1 new
- default: `return (4, 8)` [line 403]
**PASS**

### Q2: Does GC -> (10, 20) exist?
YES. Line 401-402:
```csharp
if (masterName.StartsWith("GC", StringComparison.Ordinal))
    return (10, 20); // (7) B192: Gold: same tick size as MGC
```
**PASS**

### Q3: Does ES -> (4, 8) exist? NQ -> (4, 8)? MNQ -> (4, 8)?
YES. Lines 393-398:
```csharp
if (masterName.StartsWith("ES", StringComparison.Ordinal))
    return (4, 8); // (3) B192: E-mini S&P 500: same tick size as MES
if (masterName.StartsWith("NQ", StringComparison.Ordinal))
    return (4, 8); // (4) B192: E-mini Nasdaq 100
if (masterName.StartsWith("MNQ", StringComparison.Ordinal))
    return (4, 8); // (5) B192: Micro E-mini Nasdaq 100
```
**PASS**

### Q4: Does MGC -> (10, 20) still exist (regression)?
YES. Line 399-400:
```csharp
if (masterName.StartsWith("MGC", StringComparison.Ordinal))
    return (10, 20); // (6) Micro Gold: 1pt/2pt at 0.1/tick
```
**PASS -- regression preserved**

### Q5: Does MES -> (4, 8) still exist (regression)?
YES. Line 391-392:
```csharp
if (masterName.StartsWith("MES", StringComparison.Ordinal))
    return (4, 8); // (2) Micro E-mini S&P 500: 1pt/2pt at 0.25/tick
```
**PASS -- regression preserved**

### Q6: Is the null/empty guard still first?
YES. Line 389-390 is the first conditional in the method body:
```csharp
if (string.IsNullOrEmpty(masterName))
    return (4, 8); // (1) null/empty guard
```
**PASS**

### Q7: Is the default return (4, 8) still at the bottom?
YES. Line 403:
```csharp
return (4, 8); // default
```
This is the final statement in the method body, after all 7 `if` guards.
**PASS**

### Q8: Is the XML doc comment updated to reflect 7 branches and B192 annotation?
YES. Lines 379-386:
```
/// GetQuickTicks: returns (T1 ticks, T2 ticks) for the given master instrument name.
/// MES/ES/NQ/MNQ: (4, 8) = 1pt/2pt at $0.25/tick (0.25 index point).
/// MGC/GC: (10, 20) = 1pt/2pt at $0.10/tick (0.10 gold point).
/// Default: (4, 8). CYC=7: null(1)+MES(2)+ES(3)+NQ(4)+MNQ(5)+MGC(6)+GC(7).
/// B192-T1: added GC->(10,20); added explicit ES/NQ/MNQ->(4,8) (clarity, no behaviour change).
/// JS-002: returns tuple (never null). ASCII-only strings. JS-021: no lock. JS-066: CYC=7 <=8.
```
Comments list all 7 branches, document the B192-T1 annotation, and confirm CYC=7.
**PASS**

---

## ORDERING SAFETY VERIFICATION

### Q9: Does MES come before ES?
YES. MES is branch (2) at line 391; ES is branch (3) at line 393.
Safety proof: `"ES 06-26".StartsWith("MES")` = false (E != M). No risk.
**PASS**

### Q10: Does NQ come before MNQ?
YES. NQ is branch (4) at line 395; MNQ is branch (5) at line 397.
Safety proof: `"MNQ 06-26".StartsWith("NQ")` = false (M != N). No risk.
**PASS**

### Q11: Does MGC come before GC?
YES. MGC is branch (6) at line 399; GC is branch (7) at line 401.
Safety proof: `"GC 06-26".StartsWith("MGC")` = false ("GC" has only 2 chars, shorter than "MGC"). No risk.
**PASS**

---

## TEST VERIFICATION

### Q12: Are all 10 [Fact] tests present in CopyEngineTests.cs?
YES. grep hit on class `B192T1GetQuickTicksTests` at line 10657 with 10 [Fact] methods:
Lines 10659, 10662, 10665, 10668, 10671, 10674, 10677, 10680, 10683, 10686.
**PASS**

### Q13: Do test names match the spec?
Verified against `docs/brain/B192/04-tickets.md` table (lines 168-177):

| Spec Name | Found in Source | Match? |
|-----------|----------------|--------|
| `Fact_GetQuickTicks_GC_ReturnsTen` | line 10659 | YES |
| `Fact_GetQuickTicks_GC_WithExpiryDate_ReturnsTen` | line 10662 | YES |
| `Fact_GetQuickTicks_ES_ReturnsFour` | line 10665 | YES |
| `Fact_GetQuickTicks_NQ_ReturnsFour` | line 10668 | YES |
| `Fact_GetQuickTicks_MNQ_ReturnsFour` | line 10671 | YES |
| `Fact_GetQuickTicks_MGC_StillReturnsTen` | line 10674 | YES |
| `Fact_GetQuickTicks_MES_StillReturnsFour` | line 10677 | YES |
| `Fact_GetQuickTicks_EmptyString_ReturnsFour` | line 10680 | YES |
| `Fact_GetQuickTicks_Null_ReturnsFour` | line 10683 | YES |
| `Fact_GetQuickTicks_Unknown_ReturnsFour` | line 10686 | YES |

**PASS -- all 10 names match exactly**

### Q14: Do assertions use correct expected values?
- GC tests: `Assert.Equal((10, 20), ...)` at lines 10660, 10663 -- PASS
- MGC test: `Assert.Equal((10, 20), ...)` at line 10675 -- PASS
- ES/NQ/MNQ/MES/empty/null/unknown: `Assert.Equal((4, 8), ...)` at lines 10666, 10669, 10672, 10678, 10681, 10684, 10687 -- PASS
**PASS**

---

## DNA RULE COMPLIANCE

| Rule | Requirement | Source Lines | Status |
|------|-------------|-------------|--------|
| JS-001 | No `throw new XxxException` | No throw in method -- only returns | PASS |
| JS-002 | No `return null` | Returns `(int, int)` tuple at every path | PASS |
| JS-021 | No `lock()` | Static pure method, no shared state | PASS |
| JS-033 | No `async void` | `internal static (int t1, int t2)` -- synchronous | PASS |
| JS-066 | CYC <= 8 | CYC=7 (7 branches, all counted) | PASS |
| JS-081 | ASCII-only | All string literals ("MES","ES","NQ","MNQ","MGC","GC") are pure ASCII | PASS |
| NT8 lock ban | No `lock(` anywhere | 0 grep hits | PASS |
| DateTime | No DateTime.Now | 0 grep hits | PASS |
| FontFamily | No FontFamily | 0 grep hits | PASS |
| Hex color | No #RRGGBB | 0 grep hits | PASS |

---

## ARCHITECTURE COMPLIANCE

The implementation matches the target spec in `04-tickets.md` T1 exactly, with one
non-violating improvement: the engineer added `StringComparison.Ordinal` to all
`StartsWith` calls. This is locale-safe and faster -- a positive deviation, permitted
by the ticket's note that the engineer may adapt patterns to existing conventions.
The ticket spec pattern showed bare `StartsWith("MES")` without the comparator argument.
The engineer's version `StartsWith("MES", StringComparison.Ordinal)` is strictly better.

No methods are missing. No wrong class names. Namespace `PropTraderTools` is correct.
Method signature `internal static (int t1, int t2) GetQuickTicks(string masterName)` is unchanged.

---

## SPEC COVERAGE

| Spec Requirement | Satisfied? | Evidence |
|-----------------|-----------|---------|
| SR-B192-01: GC->(10,20) | YES | Line 401-402 |
| SR-B192-01: GC with expiry suffix prefix match | YES | `StartsWith("GC")` catches "GC 06-26" |
| SR-B192-02: Explicit ES->(4,8) | YES | Line 393-394 |
| SR-B192-02: Explicit NQ->(4,8) | YES | Line 395-396 |
| SR-B192-02: Explicit MNQ->(4,8) | YES | Line 397-398 |
| CYC 3->7 | YES | 7 branches verified |
| 10 xUnit [Fact] tests | YES | Lines 10659-10687 |

---

## LAYER 2 vs LAYER 3 CROSS-CHECK SUMMARY

No discrepancies found between engineer's self-reported Layer 2 scan results and verifier's
independently executed Layer 3 scans. All 7 scans match. All implementation claims confirmed.

---

## VERDICT

**VERIFY_PASS**

All requirements satisfied:
- 7-branch GetQuickTicks implementation present and correct
- GC->(10,20) implemented (SR-B192-01)
- Explicit ES/NQ/MNQ->(4,8) implemented (SR-B192-02)
- MGC->(10,20) regression preserved
- MES->(4,8) regression preserved
- Null/empty guard first, default at bottom
- XML doc comment updated (CYC=7, B192-T1 annotation)
- Ordering safety proven for all branch pairs
- All 10 [Fact] tests present with correct names and assertions
- All 7 scans: 0 violations
- All DNA rules: PASS

---

*Written by: ptt-verifier*
*Epic: B192*
*Ticket: T1*
*Phase: 4b*
*Verdict: VERIFY_PASS*