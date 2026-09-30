# Ticket Verification: B38-LaneA (T1 + T2 + T3)

**Epic**: PTT-COPIER B38 — Trim/Flatten Anchor Fix + BE-Stop TIF Fix
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-07-28
**Build Tag Verified**: `PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28`
**Verdict**: VERIFY_PASS

---

## Verification Protocol

Layer 3 (verifier) independently re-ran all 7 scans and cross-checked against engineer's
Layer 2 self-report in `ticket-1-completion.md`. All scan results confirmed independently.
No discrepancies found.

---

## Step 1 — File-by-File Change Verification

### PttTrim.cs

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Line 85 `useLimitOrder` | No `buffer > 0 &&` — reads `tickSize > 0.0 && (isLong ? ask > 0.0 : bid > 0.0)` | Confirmed: `bool useLimitOrder = tickSize > 0.0 && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);` | ✅ PASS |
| Lines 94-98 comment + formula | Comment "aggressive taker"; Long: `ask - buffer * tickSize`; Short: `bid + buffer * tickSize` | Confirmed at lines 94-98: comment reads "aggressive taker"; formulas `ask - buffer * tickSize` / `bid + buffer * tickSize` | ✅ PASS |
| Line 115 `TimeInForce` | `TimeInForce.Gtc` (NOT Day) | Confirmed: `TimeInForce.Gtc` | ✅ PASS |

### PttFlatten.cs

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Line 82 `useLimitOrder` | No `buffer > 0 &&` | Confirmed: same pattern as PttTrim.cs, no `buffer > 0` guard | ✅ PASS |
| Lines 91-95 comment + formula | "aggressive taker"; Long: `ask - buffer * tickSize`; Short: `bid + buffer * tickSize` | Confirmed: identical pattern to PttTrim.cs | ✅ PASS |
| Line 112 `TimeInForce` | `TimeInForce.Gtc` | Confirmed: `TimeInForce.Gtc` | ✅ PASS |

### PttBreakEven.cs

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Line 179 `SubmitBeStopLocal` stop | `TimeInForce.Gtc` | Confirmed: `TimeInForce.Gtc` in `SubmitBeStopLocal` CreateOrder call | ✅ PASS |
| Line ~317 `SubmitBeTargetsLocal` bare stop | `TimeInForce.Gtc` | Confirmed: `TimeInForce.Gtc` in 0-targets bare stop branch | ✅ PASS |
| Line ~350 `SubmitBeTargetsLocal` loop stop | `TimeInForce.Gtc` | Confirmed: `TimeInForce.Gtc` in per-pair stop loop | ✅ PASS |
| No `TimeInForce.Day` remaining | 0 hits | 0 hits in file | ✅ PASS |

### CopyEngine.cs

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Line 41 build tag | `"PTT-COPIER B38 \| trim-anchor-be-tif \| 2026-07-28"` | Confirmed: `internal const string Tag = "PTT-COPIER B38 \| trim-anchor-be-tif \| 2026-07-28";` | ✅ PASS |
| Line 1597 bare stop | `TimeInForce.Gtc` | Confirmed: `TimeInForce.Gtc, pos.Quantity,` at line 1597 | ✅ PASS |
| Line 1636 loop stop | `TimeInForce.Gtc` | Confirmed: `TimeInForce.Gtc, t.Qty,` at line 1636 | ✅ PASS |

### CopyEngineTests.cs

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| 6 new [Fact] methods present | T_B38_TrimModule_Long_LimitBelowAsk, T_B38_TrimModule_Short_LimitAboveBid, T_B38_TrimModule_BufferZero_SubmitsLimit, T_B38_TrimModule_Gtc_TifCorrect, T_B38_BeStop_Gtc_TifCorrect, T_B38_BeStopArmed_Gtc_TifCorrect | All 6 confirmed at lines 3588-3691 | ✅ PASS |
| Total [Fact] count | 194 | 194 | ✅ PASS |

---

## Step 2 — Independent 7-Scan Results (Layer 3)

All scans run independently via PowerShell. Engineer's Layer 2 report NOT used as input.

### SCAN-01: `lock(` usage

```powershell
Get-ChildItem -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools" -Filter "*.cs" -Recurse |
    Select-String -Pattern "lock\("
```

**Results**:
```
CopyEngine.cs:611   -- comment: "try block(0)." -- COMMENT ONLY
CopyEngine.cs:1566  -- comment: "JS-021: no lock()." -- COMMENT ONLY
CopyEngine.cs:1700  -- comment: "JS-021: no lock()." -- COMMENT ONLY
```

**Verdict**: 0 actual `lock()` statements. 3 comment-only hits. **PASS** (JS-021 compliant)

---

### SCAN-02: `async void`

```powershell
Get-ChildItem -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools" -Filter "*.cs" -Recurse |
    Select-String -Pattern "async\s+void\s+\w"
```

**Results**: 0 hits

**Verdict**: **PASS** (JS-033 compliant)

---

### SCAN-03: `return null` in PttTrim.cs, PttFlatten.cs, PttBreakEven.cs

```powershell
Select-String -Path PttTrim.cs,PttFlatten.cs,PttBreakEven.cs -Pattern "return null"
```

**Results**:
```
PttTrim.cs:145     -- FindPositionLocal: if null guard
PttTrim.cs:149     -- FindPositionLocal: end-of-loop return null
PttFlatten.cs:142  -- FindPositionLocal: if null guard
PttFlatten.cs:146  -- FindPositionLocal: end-of-loop return null
PttBreakEven.cs:212 -- FindPositionLocal: if null guard
PttBreakEven.cs:216 -- FindPositionLocal: end-of-loop return null
```

**Verdict**: 6 hits, all inside `FindPositionLocal` (NT8-050 pattern — explicitly exempted).
No `return null` in any modified hot-path method. **PASS**

---

### SCAN-04: `TimeInForce.Day` (full PropTraderTools)

```powershell
Get-ChildItem -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools" -Filter "*.cs" -Recurse |
    Select-String -Pattern "TimeInForce\.Day"
```

**Results**:
```
CopyEngineTests.cs:3639  -- string literal in comment: "must NOT contain TimeInForce.Day"
CopyEngineTests.cs:3653  -- Assert.DoesNotContain("TimeInForce.Day", trimSrc) -- test assertion string
CopyEngineTests.cs:3654  -- Assert.DoesNotContain("TimeInForce.Day", flattenSrc) -- test assertion string
CopyEngineTests.cs:3660  -- string literal in comment: "must NOT contain TimeInForce.Day"
CopyEngineTests.cs:3668  -- Assert.DoesNotContain("TimeInForce.Day", beSrc) -- test assertion string
CopyEngineTests.cs:3674  -- string literal in comment: "must NOT contain TimeInForce.Day"
CopyEngineTests.cs:3690  -- Assert.DoesNotContain("TimeInForce.Day", region) -- test assertion string
TradeCopierPanel.cs:1397 -- TimeInForce.Day, -- PRE-EXISTING PTT-Click entry order (out of B38 scope)
```

**Verdict**:
- 0 hits in `PttTrim.cs`, `PttFlatten.cs`, `PttBreakEven.cs`, `CopyEngine.cs`. **All B38 scope files clean.**
- `CopyEngineTests.cs` hits are string literals inside `Assert.DoesNotContain()` — not executable `TimeInForce.Day` usage.
- `TradeCopierPanel.cs:1397` is pre-existing PTT-Click entry order, outside B38 scope (V12.23 No Scope Creep Protocol).
- **PASS**

---

### SCAN-05: Anchor formula direction (PttTrim.cs)

```powershell
Select-String -Path PttTrim.cs -Pattern "ask - buffer \* tickSize|bid \+ buffer \* tickSize"
```

**Results**:
```
PttTrim.cs:97  -- ? ask - buffer * tickSize
PttTrim.cs:98  -- : bid + buffer * tickSize;
```

**Verdict**: Long = `ask - buffer * tickSize` (aggressive taker, fills at/below ask). Short = `bid + buffer * tickSize` (aggressive taker, fills at/above bid). **PASS** (DW-B32-TRIM-ANCHOR-01 fixed)

---

### SCAN-06: `useLimitOrder` guard — no `buffer > 0 &&`

```powershell
Select-String -Path PttTrim.cs -Pattern "buffer > 0"
```

**Results**: 0 hits

**Verdict**: `buffer > 0 &&` guard completely removed. `useLimitOrder` reads `tickSize > 0.0 && (isLong ? ask > 0.0 : bid > 0.0)` — buffer=0 correctly submits Limit order. **PASS** (DW-B32-TRIM-MARKET-01 fixed)

---

### SCAN-07: `[Fact]` count in CopyEngineTests.cs

```powershell
(Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]").Count
```

**Result**: `194`

**Verdict**: Was 188 pre-B38. +6 new tests = 194. **PASS**

---

## Step 3 — Layer 2 vs Layer 3 Cross-Check

| Scan | Engineer L2 Claim | Verifier L3 Result | Match |
|------|------------------|--------------------|-------|
| SCAN-01 lock( | 0 actual lock statements (3 comment hits) | 3 comment-only hits, 0 code hits | ✅ |
| SCAN-02 async void | 0 hits | 0 hits | ✅ |
| SCAN-03 return null | 6 hits, FindPositionLocal only (same lines) | 6 hits, FindPositionLocal only (same lines) | ✅ |
| SCAN-04 TimeInForce.Day | 0 in B38 files; TradeCopierPanel:1397 pre-existing | 0 in B38 files; TradeCopierPanel:1397 pre-existing; Tests = string literals | ✅ |
| SCAN-05 anchor formula | ask-buf*tick / bid+buf*tick at PttTrim:97-98 | Confirmed at PttTrim:97-98 | ✅ |
| SCAN-06 no buffer > 0 | 0 hits | 0 hits | ✅ |
| SCAN-07 [Fact]=194 | 194 | 194 | ✅ |

**Discrepancies**: **NONE**. All 7 Layer 2 claims independently confirmed by Layer 3.

---

## Step 4 — verify_links.ps1

```powershell
powershell -File scripts\verify_links.ps1 -Fix
# Run from: C:\WSGTA\universal-or-strategy
```

**Output**:
```
=== NT8 HARD LINK INTEGRITY AUDIT ===
OK       : AtrSizingEngine.cs  (copy-only)
OK       : CopyEngine.cs  (hard-linked)
SKIP     : CopyEngineTests.cs  (test file -- not deployed to NT8)
OK       : TradeCopierAddOn.cs  (hard-linked)
OK       : TradeCopierPanel.cs  (hard-linked)
OK       : TradeCopierWindow.cs  (copy-only)
OK       : Core\PttContracts.cs  (hard-linked)
OK       : Features\PttBreakEven.cs  (hard-linked)
OK       : Features\PttCancel.cs  (hard-linked)
OK       : Features\PttCopier.cs  (hard-linked)
OK       : Features\PttFlatten.cs  (hard-linked)
OK       : Features\PttTrim.cs  (hard-linked)

OK      : 11
DESYNC  : 0
MISSING : 0
FIXED   : 0
SKIPPED : 1

PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

**Verdict**: **PASS** — OK=11, DESYNC=0. All 5 B38-modified deployable files (CopyEngine.cs, PttBreakEven.cs, PttFlatten.cs, PttTrim.cs + TradeCopierPanel.cs unchanged) are hard-linked and in sync.

---

## Step 5 — Spec Satisfaction (4 Defects)

| Defect ID | Description | Files Changed | Evidence | Result |
|-----------|-------------|---------------|----------|--------|
| DW-B32-TRIM-MARKET-01 | Remove `buffer > 0 &&` guard so buffer=0 still submits Limit | PttTrim.cs:85, PttFlatten.cs:82 | SCAN-06: 0 hits for `buffer > 0`; `useLimitOrder` reads `tickSize > 0.0 && (isLong ? ask > 0.0 : bid > 0.0)` | ✅ SATISFIED |
| DW-B32-TRIM-ANCHOR-01 | Flip anchor: Long = `ask - buf*tick`, Short = `bid + buf*tick` | PttTrim.cs:97-98, PttFlatten.cs:94-95 | SCAN-05: `ask - buffer * tickSize` / `bid + buffer * tickSize` confirmed; comment updated to "aggressive taker" | ✅ SATISFIED |
| DW-B32-TRIM-TIF-01 | Change `TimeInForce.Day` → `TimeInForce.Gtc` in Trim/Flatten | PttTrim.cs:115, PttFlatten.cs:112 | SCAN-04: 0 hits in both files; `TimeInForce.Gtc` confirmed in CreateOrder calls | ✅ SATISFIED |
| DW-B38-STOP-TIF-01 | Change `TimeInForce.Day` → `TimeInForce.Gtc` in all 5 BE-stop calls | PttBreakEven.cs:179,317,350; CopyEngine.cs:1597,1636 | SCAN-04: 0 hits in both files; all 5 CreateOrder calls confirmed `TimeInForce.Gtc` | ✅ SATISFIED |

**All 4 defects fully satisfied.**

---

## Jane Street DNA Final Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock()) | SCAN-01: 0 actual lock statements | ✅ PASS |
| JS-033 (no async void) | SCAN-02: 0 hits | ✅ PASS |
| JS-002 (no return null in hot path) | SCAN-03: FindPositionLocal only (NT8-050 exemption) | ✅ PASS |
| JS-001 (no throw in hot path) | No `throw new` added in any modified method | ✅ PASS |
| NT8-014 (PTT- signal names) | PTT-Trim, PTT-Flatten, PTT-BE-Stop unchanged | ✅ PASS |
| NT8-049 (CreateOrder arg6/arg7) | Limit: arg6=limitPrice, arg7=0; Stop: arg6=0, arg7=bePrice | ✅ PASS |
| NT8-013 (DateTime.MaxValue) | All CreateOrder calls use DateTime.MaxValue | ✅ PASS |
| NT8-007 (CustomOrder null cast) | All CreateOrder arg11 = `(NinjaTrader.Cbi.CustomOrder)null` | ✅ PASS |
| CYC <= 8 | No new decision points added; TIF swap = same CYC | ✅ PASS |
| Build tag contains "B38" | CopyEngine.cs:41 confirmed | ✅ PASS |

---

## Summary

| Component | Status |
|-----------|--------|
| File reads (all 5) | PASS |
| 7 independent scans | ALL PASS |
| Layer 2 vs Layer 3 cross-check | NO DISCREPANCIES |
| verify_links.ps1 | OK=11 DESYNC=0 PASS |
| 4 defects satisfied | ALL SATISFIED |
| DNA rules | ALL PASS |

---

## VERIFY_PASS
