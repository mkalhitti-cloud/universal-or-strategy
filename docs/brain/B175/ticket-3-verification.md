# Ticket Verification — B175-3
# Phase 4b — Verifier (PTT Verifier)
# Ticket: B175-3 — New xUnit Tests (CopyEngineTests.cs)
# Date: 2025-01-27
# Verdict: VERIFY_PASS

---

## Scope

File verified (READ-ONLY): `src/PropTraderTools/CopyEngineTests.cs`
Engineer completion doc read: `docs/brain/B175/ticket-3-completion.md`
Ticket spec read: `docs/brain/B175/04-tickets.md` (B175-3 section)
Architecture plan read: `docs/brain/B175/02-architecture-plan.md` (test spec section)

All scans run independently. Engineer self-report (Layer 2) cross-checked against Layer 3 results below.

---

## CHECK 1 — Class and Methods Present

File total lines: **10424** (confirmed via PowerShell `(Get-Content).Count`).

`B175AtrCalcTests` class located at lines **10318–10422** of `CopyEngineTests.cs`.

| Required Symbol | Line | Present |
|---|---|---|
| `public class B175AtrCalcTests` | 10318 | YES |
| `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR` | 10324 | YES |
| `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg` | 10346 | YES |
| `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg` | 10390 | YES |
| `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg` | 10413 | YES |

All 4 [Fact] methods present with exact names matching ticket spec. **CHECK 1: PASS**

---

## CHECK 2 — Test Logic Correctness (Manual Trace)

### T1-01: `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR`

Setup: `highs[i]=i+2, lows[i]=i, closes[i]=i+1` for i=0..28. `len=29, period=14`.

Seed loop (i=1..14):
- pc = closes[i-1] = i
- H-L = (i+2)-i = 2
- |H-pc| = |(i+2)-i| = 2
- |L-pc| = |i-i| = 0
- TR = max(2, 2, 0) = **2.0** for all i
- seed = 28.0/14 = **2.0**

Wilder's check: 29 >= 2*14+1=29. Branch entered.

Wilder's loop (i=15..28):
- pc = closes[i-1] = i (e.g. i=15: pc=closes[14]=15)
- H-L = 2 for all. TR=2.0 constant.
- atr = (2.0*(14-1)+2.0)/14 = (26+2)/14 = 28/14 = **2.0** (identity)

Result = **2.0**. `Assert.Equal(2.0, result, precision:10)` → **PASS** ✓

### T1-02: `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg`

29-bar arrays seed loop (i=1..14), reading from highs29/lows29/closes29:
- i=1: pc=closes29[0]=0.5, H=2, L=1. TR=max(1, 1.5, 0.5)=**1.5**
- i=2..14: pc=closes29[i-1]=(i-1)+0.5; H=i+1, L=i. H-L=1, |H-pc|=1.5, |L-pc|=0.5. TR=**1.5**
- seed = 1.5*14/14 = **1.5**

Wilder's loop (i=15..28), TR from highs29/lows29/closes29 with spike pattern:
- i=15: pc=closes29[14]=14.5, H=18, L=15. TR=max(3, |18-14.5|=3.5, |15-14.5|=0.5)=3.5
  Note: actual TR for i=15 is 3.5 (prev-close contribution), not exactly 3.0 due to C[14]=14.5.
  For i=16..28: pc=closes29[i-1]=(i-1)+1; H=i+3, L=i. H-L=3, |H-pc|=|(i+3)-(i)| = 3 for i=16..28.
  All TRs >= 3.0 in the spike region.
- After 14 smoothing steps starting from seed=1.5 with TR>=3.0, atr converges toward 3.0+.
  atr after step 1 (at minimum): (1.5*13+3)/14=22.5/14≈1.607, grows monotonically.
  After 14 steps: atr >> 1.5 (converges toward 3.0).

15-bar simple fallback: `len=15 < 29`. Same seed calculation gives **1.5**. Returns 1.5.

Assert `wilders > simple` = wilders (>> 1.5) > simple (1.5) → **PASS** ✓

Note: The test comment says "simple avg=1.0" — this is a comment imprecision (TR is 1.5 for the
previous-close-affected first bar), but the assert `wilders > simple` holds unconditionally since
Wilder's smoothing with TR>=3.0 over seed=1.5 must exceed 1.5. The test logic is sound.

### T1-03: `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg`

Setup: same H/L/C construction as T1-01 but `len=15, period=14`.

Length check: 15 < 2*14+1=29 → Wilder's block NOT entered.

Seed loop (i=1..14): TR=2.0 constant (identical to T1-01 analysis). seed = **2.0**.

Returns 2.0 (fallback). `Assert.Equal(2.0, result, precision:10)` → **PASS** ✓

### T1-04: `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg`

Input: H=[10,12,11,13], L=[8,9,10,10], C=[9,10,11,12]. period=3.

Length check: 4 < 2*3+1=7 → Wilder's NOT entered.

Seed loop (i=1..3):
- i=1: pc=C[0]=9, H=12, L=9. TR=max(3, |12-9|=3, |9-9|=0)=**3**
- i=2: pc=C[1]=10, H=11, L=10. TR=max(1, |11-10|=1, |10-10|=0)=**1**
- i=3: pc=C[2]=11, H=13, L=10. TR=max(3, |13-11|=2, |10-11|=1)=**3**

sum=7, atr=7/3. `Assert.Equal(7.0/3.0, result, precision:10)` → **PASS** ✓

**CHECK 2: All 4 test logic traces correct. PASS**

---

## CHECK 3 — 7-Scan Results (Layer 3, Independent)

All scans run via PowerShell `execute_command`. Results are authoritative.

### SCAN-01: B175AtrCalcTests class presence
```powershell
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "B175AtrCalcTests").Count
```
**Result: 2** (class declaration line 10318 + comment header line 10313)
Engineer Layer 2: 2. Match. **PASS**

### SCAN-02: [Fact] count
```powershell
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "\[Fact\]").Count
```
**Result: 251** (delta = +4 vs prior 247)
Engineer Layer 2: 251. Match. **PASS**

### SCAN-03: lock() in file
```powershell
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "lock\(").Count
```
**Result: 5**

Detailed hits:
- Line 9223: `// JS-001: no throw. JS-021: no lock(). ASCII-only.` — comment only
- Line 9350: `// JS-001: no throw. JS-021: no lock(). ASCII-only.` — comment only
- Line 9471: `// JS-001: no throw. JS-021: no lock(). ASCII-only.` — comment only
- Line 9885: `// No NT8 runtime required. ASCII-only. No lock().` — comment only
- Line 9970: `// No NT8 runtime required. ASCII-only. No lock(). JS-001: no throw.` — comment only

All 5 hits are in comment text before line 10313 (B175 class start). Zero lock() in B175AtrCalcTests.
Engineer Layer 2: 5 (same lines cited). Match. **PASS**

### SCAN-04: Non-ASCII characters
```powershell
$content = Get-Content "src/PropTraderTools/CopyEngineTests.cs" -Raw
$nonAscii = [regex]::Matches($content, '[^\x00-\x7F]')
$nonAscii.Count
```
**Result: 0**
Engineer Layer 2: 0. Match. **PASS**

### SCAN-05: FontFamily
```powershell
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "FontFamily").Count
```
**Result: 1**

Hit: Line 10260 — `// JS-021: no lock. ASCII-only. No DateTime.Now. No FontFamily.` — comment only.
Line 10260 < 10313 (B175 class start). Zero FontFamily in B175AtrCalcTests.
Engineer Layer 2: 1 (same line cited). Match. **PASS**

### SCAN-06: Hex color literals (#RRGGBB)
```powershell
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "#[0-9A-Fa-f]{6}").Count
```
**Result: 0**
Engineer Layer 2: 0. Match. **PASS**

### SCAN-07: DateTime.Now
```powershell
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "DateTime\.Now[^U]").Count
```
**Result: 1**

Hit: Line 10260 — `// JS-021: no lock. ASCII-only. No DateTime.Now. No FontFamily.` — comment text.
Line 10260 < 10313. Zero DateTime.Now usage in B175AtrCalcTests.
Engineer Layer 2: 1 (same line cited). Match. **PASS**

### Scan Summary

| Scan | Check | Verifier Result | Engineer Layer 2 | Layer 2/3 Match | Status |
|---|---|---|---|---|---|
| SCAN-01 | B175AtrCalcTests present | 2 occurrences | 2 | YES | PASS |
| SCAN-02 | [Fact] count +4 | 251 (delta=+4) | 251 | YES | PASS |
| SCAN-03 | No lock() in B175 code | 5 hits, all pre-B175 comments | 5 (same) | YES | PASS |
| SCAN-04 | ASCII-only | 0 non-ASCII | 0 | YES | PASS |
| SCAN-05 | No FontFamily in B175 | 1 hit (comment, pre-B175) | 1 (same) | YES | PASS |
| SCAN-06 | No #RRGGBB hex colors | 0 | 0 | YES | PASS |
| SCAN-07 | No DateTime.Now in B175 | 1 hit (comment, pre-B175) | 1 (same) | YES | PASS |

All 7 scans: **PASS**. All Layer 2/3 results match.

---

## CHECK 4 — Class Placement Inside Namespace

From file read (lines 10300–10424):

```
10311 |     }                            <- B174AtrCalcTests class closes
10312 |                                  <- blank line
10313 |     // B175AtrCalcTests -- ...   <- comment header
10318 |     public class B175AtrCalcTests
...
10422 |     }                            <- B175AtrCalcTests closes
10423 |                                  <- blank line
10424 | }                                <- namespace PropTraderTools closes
```

Namespace `PropTraderTools` declared at line 13. The `B175AtrCalcTests` class body spans lines
10318–10422, which is **before** the namespace closing brace at line 10424.

The class is **inside** the namespace. No floating class after namespace.
**CHECK 4: PASS**

---

## dotnet build

```powershell
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
```
**Result: Build succeeded. 0 Warning(s). 0 Error(s).**
**PASS**

---

## dotnet test — B175 filter

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B175AtrCalcTests"
```

**Result:**
```
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg
Passed PropTraderTools.B175AtrCalcTests.T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR

Test Run Successful.
Total tests: 4
     Passed: 4
```
All 4 new tests pass. 0 failures. **PASS**

---

## DNA Rule Checklist

| Rule | Category | Check | Result |
|---|---|---|---|
| JS-021 / JS-025 | P0 Concurrency | No lock() in B175AtrCalcTests | PASS — 0 lock statements |
| JS-023 | P0 Concurrency | No Monitor.Enter / Mutex / Semaphore | PASS — not present |
| JS-001 / JS-002 / JS-003 | P0 Type Safety | No throw in test methods | PASS — xUnit captures as failures; no throw |
| JS-001 | P0 Type Safety | No return null where non-null expected | PASS — no reference returns |
| JS-008 / JS-009 | P1 Immutability | No mutable struct across threads | PASS — no structs, no threading |
| NT8 — FontFamily | Hard | No FontFamily= in B175 section | PASS — 0 |
| NT8 — hex colors | Hard | No #RRGGBB in B175 section | PASS — 0 |
| NT8 — DateTime.Now | Hard | No DateTime.Now in B175 section | PASS — 0 |
| NT8 — async/await | Hard | No async/await in B175 section | PASS — not present |
| NT8 — Account.All | Hard | No Account.All outside Loaded | PASS — not present |
| NT8 — sealed TradeCopierWindow | Hard | No sealed TradeCopierWindow | N/A — test file |
| CYC <= 8 | P1 Complexity | All test methods CYC=1..3 | PASS — T1-01:2, T1-02:3, T1-03:2, T1-04:1 |
| ASCII-only | Mandatory | No Unicode / curly quotes | PASS — 0 non-ASCII |

All DNA rules: **PASS**

---

## Architecture Compliance

| Check | Spec | Implementation | Status |
|---|---|---|---|
| Class name | `public class B175AtrCalcTests` | Line 10318: exact match | PASS |
| Namespace | Inside `namespace PropTraderTools` | Lines 13–10424 | PASS |
| 4 [Fact] methods | As specified in 04-tickets.md B175-3 | All 4 present | PASS |
| Method names | Exact names from ticket spec | All match exactly | PASS |
| `AtrCalc.Compute` signature used | `(double[], double[], double[], int)` | All 4 tests call this | PASS |
| No NT8 runtime dependency | Pure-array tests only | Confirmed — no NT8 mock | PASS |
| apply_diff placement | Before namespace closing brace | Correct (CHECK 4) | PASS |
| 7-scan pre-conditions | All zero or comment-only | Confirmed Layer 3 | PASS |

---

## Spec Coverage

| Spec Requirement | Test | Status |
|---|---|---|
| DW-B175-ATR-FORMULA: Wilder's constant-TR identity | T1-01: seed+smooth=constant | COVERED |
| DW-B175-ATR-FORMULA: Wilder's smoothing > simple avg | T1-02: spike comparison | COVERED |
| DW-B175-ATR-FORMULA: fallback (short history) = simple avg | T1-03: 15-bar fallback | COVERED |
| B174 regression guard: exact H/L/C baseline preserved | T1-04: 7/3 guard | COVERED |

All spec requirements covered. **PASS**

---

## Layer 2 vs Layer 3 Cross-Check

All 7 engineer self-reported scan results (Layer 2) match Verifier's independent Layer 3 results exactly.
No discrepancy found. Engineer's BUILD_PASS claim confirmed (dotnet build: 0 errors).
Engineer's test PASS claim confirmed (dotnet test filter: 4/4 passed).

---

## Violations

**None.**

---

## VERDICT

**VERIFY_PASS**

B175-3 implementation is complete and correct.
- All 4 required [Fact] methods present with exact names.
- All 4 tests pass (confirmed via `dotnet test`).
- 0 lock(), 0 non-ASCII, 0 hex colors, 0 FontFamily, 0 DateTime.Now in B175 code.
- Class placement inside namespace confirmed.
- Layer 2/3 scan results match on all 7 scans.
- All DNA rules pass.
