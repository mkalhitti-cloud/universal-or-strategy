# Ticket Completion: B38-LaneA (T1 + T2 + T3)

**Epic**: PTT-COPIER B38 — Trim/Flatten Anchor Fix + BE-Stop TIF Fix
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-07-28
**Build**: `PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28`
**Status**: BUILD_PASS

---

## What Was Implemented

### Ticket 1 — PttTrim.cs + PttFlatten.cs (6 changes)

**DW-B32-TRIM-MARKET-01 (Guard Fix — T1a / T1d)**
- Removed `buffer > 0 &&` from `useLimitOrder` guard in both `TrimPositionLocal` and `FlattenPositionLocal`.
- Before: `buffer > 0 && tickSize > 0.0 && ask/bid > 0.0` — buffer=0 fell through to Market order.
- After: `tickSize > 0.0 && (isLong ? ask > 0.0 : bid > 0.0)` — buffer=0 correctly submits Limit @ ask/bid.

**DW-B32-TRIM-ANCHOR-01 (Anchor Direction Fix — T1b / T1e)**
- Corrected limitPrice formula in both files.
- Before: Long `ask + buffer*tick` / Short `bid - buffer*tick` (posting limit ABOVE ask / BELOW bid — maker, passive).
- After: Long `ask - buffer*tick` / Short `bid + buffer*tick` (aggressive taker, matches `CopyEngine.ComputeLimitPx`).
- Updated comment from "above ask / below bid" to "aggressive taker".

**DW-B32-TRIM-TIF-01 (TIF Fix — T1c / T1f)**
- Changed `TimeInForce.Day` → `TimeInForce.Gtc` in `acc.CreateOrder` inside both `TrimPositionLocal` and `FlattenPositionLocal`.

Files modified:
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs` (lines 85, 94-98, 115)
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs` (lines 82, 91-95, 112)

---

### Ticket 2 — PttBreakEven.cs + CopyEngine.cs (6 changes)

**DW-B38-STOP-TIF-01 (BE-Stop TIF Fix — B1/B2/B3/C1/C2)**
Changed all 5 BE-stop `CreateOrder` calls from `TimeInForce.Day` → `TimeInForce.Gtc`:

| Change | File | Method | Context |
|--------|------|--------|---------|
| B1 | PttBreakEven.cs:179 | `SubmitBeStopLocal` | Full-pos stop in OCO mode |
| B2 | PttBreakEven.cs:317 | `SubmitBeTargetsLocal` | 0-targets bare stop |
| B3 | PttBreakEven.cs:350 | `SubmitBeTargetsLocal` | Per-pair stop loop |
| C1 | CopyEngine.cs:1597 | `SubmitBeStop` | 0-targets bare stop |
| C2 | CopyEngine.cs:1636 | `SubmitBeStop` | Per-pair stop loop |

Also updated stale doc-comment in PttBreakEven.cs:287 from `TimeInForce.Day for bare stop` to
`TimeInForce.Gtc for all stops (B38 fix)`.

**Build Tag (C3)**
- `CopyEngine.cs:41`: `"PTT-COPIER B37 | be-oco-per-pair | 2026-07-27"` → `"PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28"`

Files modified:
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs` (lines 179, 287, 317, 350)
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` (lines 41, 1597, 1636)

---

### Ticket 3 — CopyEngineTests.cs (6 new [Fact] tests)

Appended 6 new test methods inside the `CopyEngineTests` class before the final closing braces.
Count: 188 → 194 [Fact] methods.

| Test Method | Defect | What It Validates |
|-------------|--------|-------------------|
| `T_B38_TrimModule_Long_LimitBelowAsk` | DW-B32-TRIM-ANCHOR-01 | Long: `ask - 1*tick = 7499.75` |
| `T_B38_TrimModule_Short_LimitAboveBid` | DW-B32-TRIM-ANCHOR-01 | Short: `bid + 1*tick = 7500.25` |
| `T_B38_TrimModule_BufferZero_SubmitsLimit` | DW-B32-TRIM-MARKET-01 | buffer=0 → still Limit order, price = ask |
| `T_B38_TrimModule_Gtc_TifCorrect` | DW-B32-TRIM-TIF-01 | PttTrim.cs + PttFlatten.cs contain no TimeInForce.Day |
| `T_B38_BeStop_Gtc_TifCorrect` | DW-B38-STOP-TIF-01 | PttBreakEven.cs contains no TimeInForce.Day |
| `T_B38_BeStopArmed_Gtc_TifCorrect` | DW-B38-STOP-TIF-01 | CopyEngine.cs SubmitBeStop region contains no TimeInForce.Day |

File modified:
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` (appended lines 3583-3697)

---

## 7-Scan Results

### SCAN-01: lock() usage — must be 0 actual lock() statements
```
Command: Get-ChildItem -Path ... -Filter "*.cs" -Recurse | Select-String -Pattern "^\s*lock\s*\("
Result: 0 hits (PASS)
```
Note: Pattern `lock(` appears in comments only (JS-021 compliance notes). Zero actual lock() statements.

### SCAN-02: async void — must be 0
```
Command: Get-ChildItem ... | Select-String -Pattern "async\s+void\s+\w"
Result: 0 hits (PASS)
```

### SCAN-03: return null in PttTrim.cs, PttFlatten.cs, PttBreakEven.cs
```
Command: Select-String ... -Pattern "return null"
Result: 6 hits — ALL in FindPositionLocal only (NT8-050 pattern, explicitly exempted)
  - PttTrim.cs:145,149
  - PttFlatten.cs:142,146
  - PttBreakEven.cs:212,216
PASS — only FindPositionLocal pattern; no return null in modified methods.
```

### SCAN-04: TimeInForce.Day — B38-scoped files must be 0
```
Command: Select-String -Path PttTrim.cs,PttFlatten.cs,PttBreakEven.cs,CopyEngine.cs -Pattern "TimeInForce\.Day"
Result: 0 hits in B38-scoped files (PASS)

Full scan note: TradeCopierPanel.cs:1397 has 1 pre-existing TimeInForce.Day (PTT-Click order,
outside B38 scope per V12.23 No Scope Creep Protocol). Not introduced by B38.
CopyEngineTests.cs hits are string literals inside Assert.DoesNotContain() — correct test assertions.
```

### SCAN-05: PttTrim.cs lines 97-98 anchor formula
```
Read PttTrim.cs:96-98 confirms:
  limitPrice = pos.MarketPosition == MarketPosition.Long           // (4)
      ? ask - buffer * tickSize
      : bid + buffer * tickSize;
Result: PASS — Long=ask-buf*tick, Short=bid+buf*tick confirmed.
```

### SCAN-06: PttTrim.cs line 85 guard (no buffer > 0 &&)
```
Read PttTrim.cs:85 confirms:
  bool useLimitOrder = tickSize > 0.0                                   // (2)
      && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
Result: PASS — no "buffer > 0 &&" present.
```

### SCAN-07: [Fact] count in CopyEngineTests.cs — must be 194
```
Command: Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object | Count
Result: 194 (PASS) — was 188, +6 = 194.
```

---

## Post-Implementation Sync

```
powershell -File scripts\verify_links.ps1 -Fix
Result:
  OK      : 11
  DESYNC  : 0
  MISSING : 0
  FIXED   : 0
  SKIPPED : 1  (CopyEngineTests.cs — test file, not deployed to NT8)
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

---

## Jane Street DNA Compliance Summary

| Rule | Status |
|------|--------|
| JS-021 (no lock()) | PASS — 0 lock() statements |
| JS-033 (no async void) | PASS — 0 async void methods |
| JS-002 (no return null in hot path) | PASS — only FindPositionLocal exemption |
| JS-001 (no throw in hot path) | PASS — no throws added |
| ASCII-only | PASS — all strings ASCII |
| NT8-049 (CreateOrder arg positions) | PASS — arg6/arg7 positions unchanged |
| NT8-014 (PTT- signal names) | PASS — PTT-Trim, PTT-Flatten, PTT-BE-Stop unchanged |
| NT8-013 (DateTime.MaxValue) | PASS — preserved in all CreateOrder calls |
| NT8-007 (CustomOrder null cast) | PASS — preserved |
| CYC <= 8 | PASS — no new branches added; TIF swap is same CYC |

---

## Scan Summary Table

| Scan | Description | Result |
|------|-------------|--------|
| SCAN-01 | lock() usage | PASS (0 actual lock statements) |
| SCAN-02 | async void | PASS (0) |
| SCAN-03 | return null (exempted) | PASS (FindPositionLocal only) |
| SCAN-04 | TimeInForce.Day in B38 files | PASS (0 in scoped files) |
| SCAN-05 | Anchor formula direction | PASS (ask-buf*tick / bid+buf*tick) |
| SCAN-06 | Guard: no buffer > 0 && | PASS (line 85 confirmed) |
| SCAN-07 | [Fact] count = 194 | PASS (188+6=194) |

**All 7 scans: ZERO violations in scope.**

---

## BUILD_PASS
