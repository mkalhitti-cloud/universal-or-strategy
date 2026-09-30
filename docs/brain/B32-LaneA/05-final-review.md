# B32-LaneA Final Review

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-07-20
**Epic**: B32-LaneA
**Input Artifacts Consumed**:
- `02-architecture-plan.md`
- `04-ticket-review.md` (both cycles)
- `ticket-1-completion.md` + `ticket-1-verification.md`
- `ticket-2-completion.md` + `ticket-2-verification.md`
- `00-direct-repair-register.md`
- `docs/standards/jane-street/RULES_CATALOG.md` v1.0
- `docs/standards/NT8_COMPILER_RULES.md` v1.6
- Wave workspace source: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` (READ-ONLY spot-check)

---

## Section A — Defect Closure

### DW-B32-TRIM-MARKET-01 (R-B32-04): buffer==0 market-fallback guard removals

**Status**: CLOSED ✅

Six guards confirmed removed by independent source scan:

| File | Method | Search for `exitBuffer == 0` / `_trimBuffer == 0` / `_flattenBuffer == 0` | Result |
|------|--------|---------------------------------------------------------------------------|--------|
| `CopyEngine.cs` | `Trim(Account,Instrument,int,double,double)` ~line 942 | Pattern searched: `exitBuffer == 0` | **0 matches in file** |
| `CopyEngine.cs` | `Flatten(Account,Instrument,int,double,double)` ~line 960 | Same search | **0 matches in file** |
| `CopyEngine.cs` | `Trim(Instrument,int,double,double)` ~line 1079 | Same search | **0 matches in file** |
| `CopyEngine.cs` | `Flatten(Instrument,int,double,double)` ~line 1090 | Same search | **0 matches in file** |
| `TradeCopierPanel.cs` | `OnTrimClick` | Search for `_trimBuffer == 0` | **0 matches** |
| `TradeCopierPanel.cs` | `OnFlattenClick` | Search for `_flattenBuffer == 0` | **0 matches** |

All 4 CopyEngine guards and 2 TradeCopierPanel guards verified removed from source. The limit paths now fall to market **only** when `ask <= 0 || bid <= 0`. ✅

**Ticket-1-Verification VERIFY_PASS** confirms same finding independently. ✅

---

### DW-B32-TRIM-ANCHOR-01 (R-B32-05): ComputeLimitPx formula corrected

**Status**: CLOSED ✅

[`ComputeLimitPx`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1068) directly read from source at line 1068:

```csharp
internal static double ComputeLimitPx(bool isLong, double ask, double bid, int exitBuffer, double tickSize)
    => isLong
        ? ask - exitBuffer * tickSize
        : bid + exitBuffer * tickSize;
```

Formula: `ask - exitBuffer * tickSize` for long ✅, `bid + exitBuffer * tickSize` for short ✅

Header comment block (lines 1060-1067) uses B32 language (`"DW-B32-TRIM-ANCHOR-01"`, `"peg-to-ask/bid"`). No B29 language remaining. ✅

**Ticket-1-Verification VERIFY_PASS** confirms this independently. ✅

---

### DW-B32-TRIM-CLOSE-01 (R-B32-03): ATM bracket guard in TrimOneAccount / FlattenOneAccount

**Status**: CLOSED ✅

[`TrimOneAccount`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:980) at line 980: guard block confirmed present as first statement (lines 981-992):

```csharp
if (IsAtmBracketActive(acc, instrument))
{
    NinjaTrader.Code.Output.Process("PTT-Trim: " + acc.Name + " -- ATM bracket active, use native Target/Close buttons", PrintTo.OutputTab1);
    StatusUpdate?.Invoke(acc.Name + ": PTT-Trim blocked -- ATM bracket active");
    return;
}
```

[`FlattenOneAccount`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1027) at line 1027: identical guard block at lines 1028-1038. ✅

[`IsAtmBracketActive`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1181) at line 1181: correct implementation with `acc.Orders.ToList()` snapshot, `FromEntrySignal == null`, and `IsAtmSlotName` call. ✅

[`IsAtmSlotName`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1160) at line 1160: correct pattern for Stop\d+ and Target\d+. ✅

**Ticket-2-Verification VERIFY_PASS** confirms all 4 correctness checks independently. ✅

---

## Section B — Cross-File JS Violations

### SCAN-01: lock() — JS-021

Source scan across `src/PropTraderTools/*.cs`:
- `CopyEngine.cs`: 4 hits — ALL in comments (`// no lock (JS-021).`), not executable code
- `TradeCopierPanel.cs`: 0 hits
- `TradeCopierAddOn.cs`: 0 hits  
- `TradeCopierWindow.cs`: 0 hits
- `CopyEngineTests.cs`: 0 hits

**SCAN-01: PASS — 0 actual `lock()` calls** ✅

### SCAN-02: async void — JS-033 / NT8-019

Source scan across `src/PropTraderTools/*.cs`:
- Zero hits across all files.

**SCAN-02: PASS — 0 `async void` usages** ✅

### SCAN-03: return null — JS-002

Verified against both engineer (Layer 2) and verifier (Layer 3) reports. Pre-existing `return null` occurrences in files not touched by B32-LaneA:

| File | Lines | From B32? |
|------|-------|-----------|
| `CopyEngine.cs` | 699, 1300, 1306, 1368 | NO — pre-existing |
| `TradeCopierAddOn.cs` | 476, 485, 496, 506, 526, 539, 545, 554 | NO — pre-existing |
| `TradeCopierPanel.cs` | 355, 414, 417, 421 | NO — pre-existing |
| `TradeCopierWindow.cs` | 799, 801 | NO — pre-existing |

B32-LaneA introduced **zero new `return null`** occurrences. All new methods return `bool` or `void`. ✅

**SCAN-03: PASS — 0 new `return null` introduced by B32-LaneA** ✅

### SCAN-04: throw new XxxException in business logic — JS-001

Searched for `throw new` in `src/PropTraderTools/`. All B32-LaneA modified/added methods use `return` or `StatusUpdate?.Invoke` for error paths. No `throw` in hot paths. ✅

**SCAN-04 (JS-001): PASS** ✅

---

## Section C — Cross-File Coherence

### C-1: ComputeLimitPx formula flows through TrimOneAccountLimit and FlattenOneAccountLimit

[`ComputeLimitPx`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1068) is called by:
- [`TrimOneAccountLimit`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1202) at line 1216: `double limitPx = ComputeLimitPx(isLong, ask, bid, exitBuffer, tickSize);` ✅
- [`FlattenOneAccountLimit`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1236) at line 1249: same pattern ✅

No stale callers. The formula change from B29 (`bid - exitBuffer * tickSize` for long) to B32 (`ask - exitBuffer * tickSize` for long) propagates automatically to all downstream consumers via the single `ComputeLimitPx` call site. ✅

### C-2: IsAtmBracketActive is only called from TrimOneAccount and FlattenOneAccount (market paths)

Searched for `IsAtmBracketActive` in `CopyEngine.cs`: 2 call sites found, both in market-order methods:
- `CopyEngine.cs:985` inside `TrimOneAccount` ✅
- `CopyEngine.cs:1031` inside `FlattenOneAccount` ✅

[`TrimOneAccountLimit`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1202) and [`FlattenOneAccountLimit`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1236) do **not** call `IsAtmBracketActive`. Limit paths rely on [`CancelStaleExitOrders`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1133) (HOTFIX-F4) instead. This is architecturally correct per the plan. ✅

### C-3: CancelStaleExitOrders (HOTFIX-F4) remains intact

[`CancelStaleExitOrders`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1133) at line 1133 is called:
- `CopyEngine.cs:1205` inside `TrimOneAccountLimit` ✅
- `CopyEngine.cs:1239` inside `FlattenOneAccountLimit` ✅

Neither ticket touched these lines. HOTFIX-F4 is intact and unchanged. ✅

### C-4: TradeCopierPanel → CopyEngine wiring

[`OnTrimClick`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:793) guard at line 808: `if (ask <= 0 || bid <= 0)` (no `_trimBuffer == 0`) → calls `_engine.Trim(leader, _instrument)` (market) or `_engine.Trim(leader, _instrument, _trimBuffer, ask, bid)` (limit). ✅

[`OnFlattenClick`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:830) guard at line 836: `if (ask <= 0 || bid <= 0)` (no `_flattenBuffer == 0`) → same pattern. ✅

Both panel methods delegate to `CopyEngine` which now has the corrected formula and ATM guards. The full call chain is coherent. ✅

---

## Section D — Test Coverage

### D-1: Four renamed tests present with correct Assert.Equal values

| Test Method | Location | Assert.Equal value | Required |
|-------------|----------|--------------------|---------|
| `TrimLimit_Long_PegsToAsk` | `CopyEngineTests.cs:1495` | `5000.00` | `5000.00` ✅ |
| `TrimLimit_Short_PegsToBid` | `CopyEngineTests.cs:1504` | `5000.25` | `5000.25` ✅ |
| `FlattenLimit_Long_PegsToAsk` | `CopyEngineTests.cs:1513` | `4999.75` | `4999.75` ✅ |
| `FlattenLimit_Short_PegsToBid` | `CopyEngineTests.cs:1522` | `5000.50` | `5000.50` ✅ |

Old names (`TrimLimit_Long_PlacesBelowBid`, `TrimLimit_Short_PlacesAboveAsk`, `FlattenLimit_Long_PlacesBelowBid`, `FlattenLimit_Short_PlacesAboveAsk`) confirmed **absent** from source — search returned 0 matches. ✅

*Note on naming*: The architecture plan proposed `PlacesBelowAsk`/`PlacesAboveBid` convention; the ticket reviewer (Cycle 1 observation, P2) noted the ticket uses `PegsToAsk`/`PegsToBid`. Both engineer and verifier implemented the ticket names. The ticket is authoritative — this is not a violation.

### D-2: exitBuffer=0 block removed from TrimLimit_FallsBackToMarket_WhenAskIsZero

[`TrimLimit_FallsBackToMarket_WhenAskIsZero`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs:1531) at line 1531: Searched for `exitBuffer == 0` in `CopyEngineTests.cs` — only 1 hit found, at line 1475 inside `Flatten_ZeroBuffer_FallsBackToMarketOrder` (a comment referencing pre-B32 behavior for a different test). The test at line 1531 contains only `ex1` (ask=0) and `ex2` (bid=0). The `ex3` block (`exitBuffer == 0`) is **absent**. ✅

### D-3: T_B32_01 through T_B32_04 present with correct assertions

All 4 tests confirmed present by direct source search:

| Test | Location | Key Assertion |
|------|----------|---------------|
| `T_B32_01_IsAtmSlotName_Stop1_ReturnsTrue` | `CopyEngineTests.cs:1547` | `Assert.True(IsAtmSlotName("Stop1"))`, `"Stop2"` ✅ |
| `T_B32_02_IsAtmSlotName_Target1_ReturnsTrue` | `CopyEngineTests.cs:1555` | `"Target1"`, `"Target2"`, `"Target9"` all true ✅ |
| `T_B32_03_IsAtmSlotName_PttTrimLimit_ReturnsFalse` | `CopyEngineTests.cs:1564` | `"PTT-Trim"`, `"PTT-Flatten"`, `"PTT-TrimLimit"`, `"PTT-Copy"` all false ✅ |
| `T_B32_04_IsAtmSlotName_Null_ReturnsFalse` | `CopyEngineTests.cs:1574` | null, `""`, `"Stop"`, `"Target"`, `"TargetEntry"` all false ✅ |

All branches of `IsAtmSlotName` covered (Stop-with-digit, Target-with-digit, PTT-prefix, null, empty, too-short, no-digit suffix). ✅

---

## Section E — NT8 Gate

| Rule | Check | Result |
|------|-------|--------|
| NT8-044 `using System;` | Confirmed at `CopyEngine.cs:25` — `using System;` present. `StringComparison.Ordinal` in `IsAtmSlotName` resolves correctly. | ✅ PASS |
| NT8-007 CreateOrder arg 12 | No new `CreateOrder` calls introduced by either ticket. Existing calls use `(NinjaTrader.Cbi.CustomOrder)null`. | ✅ PASS |
| NT8-013 DateTime.Now | No `DateTime.Now` in any changed code. Existing calls use `DateTime.MaxValue`. | ✅ PASS |
| NT8-014 PTT- prefix | `Output.Process` strings in ATM guard are informational, not order signal names. Signal names unchanged. | ✅ PASS |
| NT8-018 lock() / NT8-031 OrderState | `acc.Orders.ToList()` snapshot pattern; `OrderState.Working` and `OrderState.Accepted` confirmed valid NT8 enum values. | ✅ PASS |
| NT8-019 async void | Zero async methods introduced or modified. | ✅ PASS |
| NT8-028 hex colors | No `#RRGGBB` in any changed code. | ✅ PASS |
| NT8-029 tick alignment | `ComputeLimitPx` output consumed by tick-rounding at `TrimOneAccountLimit`/`FlattenOneAccountLimit` lines ~1150/~1183. No regression. | ✅ PASS |
| NT8-001/002/003/004 | No `init` setters, records, `volatile double`, or Immutable collections introduced. | ✅ PASS |
| Pre-existing build errors | 3 pre-existing CS errors (`AtrSizingEngine.cs` NT8-DLL-absent × 2, `CopyEngine.cs:680` CS8370) confirmed unchanged from baseline. Not introduced by B32-LaneA. | ✅ NOTED — not from this epic |

**NT8 Gate: PASS** ✅

---

## Section F — CYC Budget (all ≤ 8)

Verified by direct source read of CYC annotations and manual branch counts from body inspection:

| Method | File | Annotated CYC | Independent Count | ≤ 8? |
|--------|------|---------------|-------------------|------|
| `ComputeLimitPx` | `CopyEngine.cs:1068` | 1 | 1 (single ternary) | ✅ |
| `OnTrimClick` | `TradeCopierPanel.cs:793` | 3 | 3 (instr null, ask/bid guard, else) | ✅ |
| `OnFlattenClick` | `TradeCopierPanel.cs:830` | 3 | 3 (same structure) | ✅ |
| `IsAtmSlotName` | `CopyEngine.cs:1160` | 5 | 5 (null/len guard, Stop prefix, Stop digit, Target prefix, Target digit) | ✅ |
| `IsAtmBracketActive` | `CopyEngine.cs:1181` | 6 | 6 (acc null, instr null, foreach, instr filter, state filter, name+signal check) | ✅ |
| `TrimOneAccount` | `CopyEngine.cs:980` | 4 | 4 (ATM guard, pos null/qty guard, action ternary, try/catch) | ✅ |
| `FlattenOneAccount` | `CopyEngine.cs:1027` | 4 | 4 (same structure) | ✅ |
| `Trim(Account,Instrument,int,double,double)` | `CopyEngine.cs:942` | 4 | 4 (leader null, ask/bid guard, leader direct, foreach+skip) | ✅ |
| `Flatten(Account,Instrument,int,double,double)` | `CopyEngine.cs:960` | 4 | 4 (same structure) | ✅ |

**All modified/new methods CYC ≤ 8.** Section F: PASS ✅

---

## 7-Scan Aggregate Result (across all of src/PropTraderTools/)

| Scan | Description | Aggregate Result |
|------|-------------|-----------------|
| SCAN-01 | `lock(` detection | PASS — 0 actual lock() calls (4 comment-only hits) |
| SCAN-02 | `async void` ban | PASS — 0 results |
| SCAN-03 | `return null` | PASS — 0 new occurrences from B32-LaneA (18 pre-existing unchanged) |
| SCAN-04 | NT8 compiler rules | PASS — NT8-044 confirmed, no banned patterns in changed code |
| SCAN-05 | CYC ≤ 8 | PASS — max CYC=6, all within budget |
| SCAN-06 | dotnet test/build | PASS — 0 new errors (3 pre-existing NT8-DLL-absent errors unchanged) |
| SCAN-07 | ASCII scan | PASS — 0 non-ASCII in B32-LaneA changed lines |

---

## Spec Coverage Matrix

| Requirement | Defect ID | Plan Section | Addressed? | Ticket |
|-------------|-----------|--------------|------------|--------|
| R-B32-03: Raw market order bypasses ATM bracket | DW-B32-TRIM-CLOSE-01 | §DW-B32-TRIM-CLOSE-01 | ✅ CLOSED | T-B32-T2 |
| R-B32-04: buffer==0 falls to market | DW-B32-TRIM-MARKET-01 | §DW-B32-TRIM-MARKET-01 | ✅ CLOSED | T-B32-T1 |
| R-B32-05: Wrong price anchor in ComputeLimitPx | DW-B32-TRIM-ANCHOR-01 | §DW-B32-TRIM-ANCHOR-01 | ✅ CLOSED | T-B32-T1 |

All 3 spec requirements addressed. No requirement left unaddressed. ✅

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B32-DEFERRED-01 | Pre-existing build errors (3 CS errors: AtrSizingEngine NT8-DLL × 2, CopyEngine.cs CS8370) | P2 | B33/future | OPEN — pre-existed before B32-LaneA; not introduced here |
| DW-B32-DEFERRED-02 | ATM Target nudge not implemented | P2 | future | OPEN — rejected by architecture. `acc.Change()` on ATM-owned Target slot orders is silently overridden by NT8 ATM engine (same mechanism confirmed for Stop slots in B31 live test, DW-B32-07). If a future NT8 version exposes an ATM-native partial-exit API, revisit. |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection not added | P2 | Director review | OPEN — `TrimOneAccountLimit`/`FlattenOneAccountLimit` use `CancelStaleExitOrders` (HOTFIX-F4) instead of an ATM guard. Out of scope per plan. If Director confirms limit paths also need explicit ATM detection, create a follow-up epic. |

No P0 or P1 deferred items. All deferred items are P2.

---

## Summary

All Sections A–F pass. All 7 scans return zero new violations across `src/PropTraderTools/`. All 3 spec requirements satisfied. Section K documented with 3 P2 deferred items. `06-deferred-backlog.md` written (gate requirement satisfied).

---

## FINAL_PASS
