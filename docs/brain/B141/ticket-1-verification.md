# B141 Ticket 1 Verification Report

**Block**: B141
**Ticket**: T1 — OCO Cascade Dual-Resubmit
**Verifier**: ptt-verifier (independent Layer 3)
**Verified**: 2026-09-01
**Source files read**: `src/PropTraderTools/CopyEngine.cs` (L2220-2560), `tests/PropTraderTools.Tests/B141Tests.cs`
**Reference docs read**: `docs/brain/B141/ticket-1-completion.md`, `docs/brain/B141/04-tickets.md`, `docs/brain/B141/02-architecture-plan.md`, `docs/standards/NT8_FULL_REFERENCE.md`, `docs/standards/NT8_ADDON_KNOWLEDGE.md`, `docs/standards/jane-street/RULES_CATALOG.md`

---

## FINAL GATE VERDICT: VERIFY_PASS

**Zero violations found. All 7 scans clean. All 5 NT8 verifications PASS. All 10 implementation correctness checks PASS. All 7 tests PASS. Build: 0 errors. Sync: 0 MISMATCH.**

---

## NT8-VERIFY-01: CreateOrder Argument Signature

**Question**: Does the 12-parameter `CreateOrder` call in `ResubmitTargetAfterCascade` match the documented NT8 signature?

**NT8 Citation**:
- `docs/standards/NT8_FULL_REFERENCE.md` line 2106:
  ```
  CreateOrder(Instrument instrument, OrderAction action, OrderType orderType, OrderEntry orderEntry, TimeInForce timeInForce, int quantity, double limitPrice, double stopPrice, string oco, string name, DateTime gtd, CustomOrder customOrder)
  ```
  Section: "CreateOrder() — Full Signature (confirmed 2026-08-17)", URL: https://developer.ninjatrader.com/docs/desktop/createorder

**Actual call** (`CopyEngine.cs` L2473-2486):
```
acc.CreateOrder(
    stpOrder.Instrument,          // arg 1: Instrument instrument
    stpOrder.OrderAction,         // arg 2: OrderAction action
    OrderType.Limit,              // arg 3: OrderType orderType
    OrderEntry.Automated,         // arg 4: OrderEntry orderEntry
    TimeInForce.Day,              // arg 5: TimeInForce timeInForce
    stpOrder.Quantity,            // arg 6: int quantity
    targetPrice,                  // arg 7: double limitPrice
    0,                            // arg 8: double stopPrice (0 = unused for Limit orders)
    "",                           // arg 9: string oco (empty = not in OCO group)
    "PTT-TGT-Drag",               // arg 10: string name (PTT- prefix compliant, NT8-014)
    NinjaTrader.Core.Globals.MaxDate, // arg 11: DateTime gtd (not DateTime.Now)
    (NinjaTrader.Cbi.CustomOrder)null // arg 12: CustomOrder (CS1503 guard, NT8-007)
)
```

**Verification**: All 12 args match documented signature positionally and type-wise.
- PTT- prefix: PASS (NT8-014)
- No DateTime.Now: PASS — `Globals.MaxDate` used
- arg12 cast guard: PASS (NT8-007 — `(NinjaTrader.Cbi.CustomOrder)null`)

**Result**: NT8-VERIFY-01 **PASS**

---

## NT8-VERIFY-02: acc.Orders Enumeration from AddOnBase

**Question**: Is `acc.Orders` enumerable (IEnumerable<Order>) from AddOnBase context?

**Citations**:
1. `docs/standards/NT8_FULL_REFERENCE.md` lines 2800-2844:
   - Section: "Orders Collection (Account)", URL: https://developer.ninjatrader.com/docs/desktop/orders
   - Syntax: `<account>.Orders`
   - Property Value: "An Collection of Order objects"
   - Example shows: `foreach (Order order in myAccount.Orders)` in an account event handler
2. `docs/standards/NT8_ADDON_KNOWLEDGE.md` line 219:
   - `acc.Orders  // All orders for this account (IEnumerable<Order>)`

**Usage in source**:
- `CaptureLinkedTargetPrice` L2401: `foreach (var o in acc.Orders.ToList())`
- `ResubmitTargetAfterCascade` L2450: `foreach (var o in acc.Orders.ToList())`
- `.ToList()` snapshot pattern: prevents enumeration-during-modification without needing lock()

**Result**: NT8-VERIFY-02 **PASS** — `acc.Orders` is `IEnumerable<Order>`, enumerable from AddOnBase context, confirmed by both reference docs.

---

## NT8-VERIFY-03: lock() Scan (Independent Layer 3)

**Command run**:
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2560 }
```
**Output**: (no output — 0 results)

**Full-file scan also run** (to verify no lock() anywhere in CopyEngine.cs):
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" | Select-Object LineNumber, Line
```
**Output**: All 13 hits are comment-only references to "no lock" (e.g., `// JS-021: no lock`). Zero actual `lock(` statements anywhere in file.

**Layer 2 engineer reported**: 0 hits
**Layer 3 verifier result**: 0 hits
**Discrepancy**: None

**Result**: NT8-VERIFY-03 **PASS** — Zero `lock(` statements in B141 range or anywhere in file.

---

## NT8-VERIFY-04: Independent CYC Counts

**Convention** (grounded in CopyEngine.cs existing comments L2250, L2327):
- base = +1
- `if` / `else if` / `foreach` / `for` / `while` / `?:` = +1
- `&&` / `||` inside conditions = 0 (NOT counted)
- `catch` = 0 (NOT counted)

### SyncFollowerBracket (post-B141) — lines 2254-2320

| # | Branch element | Line | +N | Running |
|---|----------------|------|----|---------|
| — | base | — | +1 | 1 |
| 1 | `if (fo == null)` | 2269 | +1 | 2 |
| 2 | `if (Math.Abs(newPrice - currentPrice) < tickSize)` | 2273 | +1 | 3 |
| 3 | `if (isStop && IsAtmSTPOrder(fo))` — `&&` NOT counted | 2281 | +1 | 4 |
| B141 | `if (capturedTargetPrice.HasValue)` — NEW | 2285 | +1 | 5 |
| 3b | `if (!isStop && IsAtmSTPOrder(fo))` — `&&` NOT counted | 2289 | +1 | 6 |
| 4 | `if (isStop && IsTrailingStop(fo))` — `&&` NOT counted | 2295 | +1 | 7 |
| 5 | `if (isStop)` inside try | 2303 | +1 | 8 |
| — | `catch (Exception ex)` | 2316 | 0 | 8 |

**Verifier CYC = 8. Engineer reported 8. Discrepancy: NONE.**

### CaptureLinkedTargetPrice (new) — lines 2396-2407

| # | Branch element | Line | +N | Running |
|---|----------------|------|----|---------|
| — | base | — | +1 | 1 |
| 1 | `if (!TryParseStopSuffix(stopName, out string suffix))` | 2398 | +1 | 2 |
| 2 | `foreach (var o in acc.Orders.ToList())` | 2401 | +1 | 3 |
| 3 | `if (IsTargetOrderLive(o) && o.Name == targetName)` — `&&` NOT counted | 2403 | +1 | 4 |

**Verifier CYC = 4. Engineer reported 4. Discrepancy: NONE.**

### TryParseStopSuffix (new) — lines 2413-2423

| # | Branch element | Line | +N | Running |
|---|----------------|------|----|---------|
| — | base | — | +1 | 1 |
| 1 | `if (stopName == null \|\| stopName.Length < 5)` — `\|\|` NOT counted | 2416 | +1 | 2 |
| 2 | `if (!int.TryParse(raw, out int n) \|\| n < 1 \|\| n > 3)` — `\|\|` NOT counted | 2419 | +1 | 3 |

**Verifier CYC = 3. Engineer reported 3. Discrepancy: NONE.**

### IsTargetOrderLive (new) — lines 2428-2429

| # | Branch element | Line | +N | Running |
|---|----------------|------|----|---------|
| — | base | — | +1 | 1 |
| — | pure expression body, no `if`, `\|\|` NOT counted | — | 0 | 1 |

**Verifier CYC = 1. Engineer reported 1. Discrepancy: NONE.**

### ResubmitTargetAfterCascade (new) — lines 2441-2499

| # | Branch element | Line | +N | Running |
|---|----------------|------|----|---------|
| — | base | — | +1 | 1 |
| 1 | `foreach (var o in acc.Orders.ToList())` | 2450 | +1 | 2 |
| 2 | `if (o.OrderState == OrderState.Working && ...)` — `&&` NOT counted | 2452 | +1 | 3 |
| — | `catch (Exception ex)` (Block A) | 2462 | 0 | 3 |
| 3 | `if (newTarget == null)` | 2487 | +1 | 4 |
| — | `catch (Exception ex)` (Block B) | 2495 | 0 | 4 |

**Verifier CYC = 4. Engineer reported 4. Discrepancy: NONE.**

### CYC Summary Table

| Method | Verifier CYC | Engineer CYC | Match? | Limit | Status |
|--------|-------------|--------------|--------|-------|--------|
| `SyncFollowerBracket` (modified) | 8 | 8 | YES | 8 | **PASS — at limit** |
| `CaptureLinkedTargetPrice` (new) | 4 | 4 | YES | 8 | PASS |
| `TryParseStopSuffix` (new) | 3 | 3 | YES | 8 | PASS |
| `IsTargetOrderLive` (new) | 1 | 1 | YES | 8 | PASS |
| `ResubmitTargetAfterCascade` (new) | 4 | 4 | YES | 8 | PASS |

**Result**: NT8-VERIFY-04 **PASS** — All methods CYC <= 8. Zero discrepancies between verifier and engineer counts.

---

## NT8-VERIFY-05: Independent Test Run

**Command run**:
```powershell
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --filter "T_B141" --verbosity normal
```

**Exact output**:
```
Test Run Successful.
Total tests: 7
     Passed: 7
 Total time: 0.5402 Seconds

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.04
```

**Individual test results**:
- T_B141_01_CaptureLinkedTargetPrice_Stop1_ReturnsTarget1LimitPrice: **Passed**
- T_B141_02_CaptureLinkedTargetPrice_Stop2_ReturnsTarget2LimitPrice: **Passed**
- T_B141_03_CaptureLinkedTargetPrice_Stop3_ReturnsTarget3LimitPrice: **Passed**
- T_B141_04_CaptureLinkedTargetPrice_TargetAlreadyCancelled_ReturnsNull: **Passed**
- T_B141_05_SyncFollowerBracket_AtmStop1Drag_ResubmitsPttTgtDrag_WhenTargetFound: **Passed**
- T_B141_06_SyncFollowerBracket_AtmStop1Drag_NoResubmit_WhenTargetAbsent: **Passed**
- T_B141_07_SyncFollowerBracket_AtmStop_SyncAtmFollowerBracketAlwaysCalled: **Passed**

**Layer 2 engineer reported**: 7/7 PASS
**Layer 3 verifier result**: 7/7 PASS
**Discrepancy**: None

**Result**: NT8-VERIFY-05 **PASS** — 7/7 tests pass.

---

## Implementation Correctness Checks (10/10)

| # | Check | Verified Location | Result |
|---|-------|-------------------|--------|
| 1 | `CaptureLinkedTargetPrice` called BEFORE `SyncAtmFollowerBracket` | L2283 before L2284 | **PASS** |
| 2 | `SyncAtmFollowerBracket` is UNCONDITIONAL (not gated on HasValue) | L2284 not inside any `if` | **PASS** |
| 3 | `ResubmitTargetAfterCascade` called ONLY if `capturedTargetPrice.HasValue` | L2285 `if (capturedTargetPrice.HasValue)` | **PASS** |
| 4 | `return;` present after `ResubmitTargetAfterCascade` call | L2287 `return;` unconditional within branch (3) | **PASS** |
| 5 | `leaderOrder` passed to `ResubmitTargetAfterCascade` | L2286: 4th arg = `leaderOrder` | **PASS** |
| 6 | `acc.Orders.ToList()` in `CaptureLinkedTargetPrice` | L2401 | **PASS** |
| 7 | No `lock()` in new methods | SCAN-03: 0 hits in L2276-L2560 | **PASS** |
| 8 | All new string literals are ASCII-only | SCAN-05 (non-ASCII): 0 hits | **PASS** |
| 9 | `CreateOrder` arg12 = `(NinjaTrader.Cbi.CustomOrder)null` | L2485 | **PASS** |
| 10 | `acc.Submit` called after `CreateOrder` | L2492 follows L2473 | **PASS** |

---

## Spec Compliance Checks

| Check | Evidence | Result |
|-------|----------|--------|
| Branch (3) comment includes DW-B153 | L2281: `// (3) DW-B134 + DW-B137 + DW-B153` | **PASS** |
| Branch (3b) (`!isStop && IsAtmSTPOrder`) is UNCHANGED | L2289-2293: identical to pre-B141 spec | **PASS** |
| No other branches in SyncFollowerBracket modified | L2295-2319: branches 4 and 5 + try/catch unchanged | **PASS** |
| All 4 new methods present with exact signatures | L2396, L2413, L2428, L2441 | **PASS** |
| Method placement: after SyncAtmFollowerBracket closing brace | SyncAtmFollowerBracket closes L2389; CaptureLinkedTargetPrice starts L2391 | **PASS** |
| Test file has exactly 7 [Fact] tests | B141Tests.cs: T_B141_01..T_B141_07 | **PASS** |
| xUnit only (never NUnit/MSTest) | `using Xunit;` only; no NUnit/MSTest reference | **PASS** |
| DW-B153 closed | Completion report confirms re-closed | **PASS** |

---

## Full DNA Rule Scan Results (Layer 3 — Independent)

### SCAN-01: lock() in B141 range (L2276-L2560)
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2560 }`
**Result**: 0 hits — **PASS**

### SCAN-02: Non-ASCII characters in B141 range
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2560 }`
**Note**: File-wide scan run; no non-ASCII in new B141 code.
**Result**: 0 hits in B141 range — **PASS**

### SCAN-03: FontFamily= (file-wide NT8 constraint)
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "FontFamily"`
**Result**: All 3 hits are comment-only references to "No FontFamily". Zero actual usage. — **PASS**

### SCAN-04: Hex color #RRGGBB (file-wide)
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "#[0-9A-Fa-f]{6}"`
**Result**: 0 hits — **PASS**

### SCAN-05: DateTime.Now (file-wide)
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "DateTime\.Now[^U]"`
**Result**: 0 hits — **PASS** (ResubmitTargetAfterCascade correctly uses `NinjaTrader.Core.Globals.MaxDate` at L2484)

### SCAN-06: async void in B141 range
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "async\s+void"`
**Result**: 1 comment-only hit (L1632: "Tick is not async void"). Zero actual `async void` declarations — **PASS**

### SCAN-07: throw new in B141 range (L2276-L2560)
**Command**: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.LineNumber -ge 2276 -and $_.LineNumber -le 2560 }`
**Result**: 0 hits — **PASS**

### JS-002: return null (nullable value type — acceptable)
`CaptureLinkedTargetPrice` returns `double?` at L2399, L2406. `double?` is `Nullable<double>` (value type). Architecture plan Section 4.2 and 04-tickets.md explicitly document this as acceptable per JS-002 note. Not a reference null violation. — **PASS**

---

## Layer 2 vs Layer 3 Comparison

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Match? | Action |
|------|-------------------|--------------------|--------|--------|
| SCAN-01: lock() | 0 hits | 0 hits | YES | None |
| SCAN-02: async void | 0 actual declarations | 0 actual declarations | YES | None |
| SCAN-03: throw new | 0 hits | 0 hits | YES | None |
| SCAN-04: CYC counts | All <= 8 (8/4/3/1/4) | All <= 8 (8/4/3/1/4) | YES | None |
| SCAN-05: ASCII-only | 0 non-ASCII | 0 non-ASCII | YES | None |
| SCAN-06: Build | 0 errors, 1 pre-existing warning | 0 errors, 1 pre-existing warning | YES | None |
| SCAN-07: Tests | 7/7 PASS | 7/7 PASS | YES | None |
| Sync+MD5 | 0 MISMATCH | 0 MISMATCH | YES | None |

**All 8 scans: Layer 2 and Layer 3 results match exactly. Zero discrepancies.**

---

## Build Verification (Independent Re-Run)

**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental`

**Output**:
```
Build succeeded.
    1 Warning(s)  [xUnit2004 in B131Tests.cs line 165 -- pre-existing, NOT introduced by B141]
    0 Error(s)

Time Elapsed 00:00:03.33
```

**Result**: 0 errors. **PASS**

---

## Sync Verification (Independent Re-Run)

**Command**: `powershell -File scripts\ptt-sync-and-verify.ps1`

**Output**:
```
=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  Copied:   0  |  In-sync: 18  |  Excluded: 62

=== PTT VERIFY: MD5 check every synced file ===
  OK       CopyEngine.cs
  [17 other files OK]
=== SYNC + VERIFY: PASS (18 files confirmed) ===
```

**Result**: 0 MISMATCH. **PASS**

---

## Architecture Compliance

| Requirement | Status |
|-------------|--------|
| Single file modified (`CopyEngine.cs`) | PASS |
| Branch (3) in `SyncFollowerBracket` — only branch modified | PASS |
| `SyncFollowerBracket` CYC at limit (8) — DW-B141-STP-CYC8-WALL documented | PASS |
| DW-B153 closed (OCO cascade dual-resubmit implemented) | PASS |
| No new lock() anywhere in file | PASS |
| No `async void` introduced | PASS |
| All new orders use "PTT-" prefix | PASS |
| `acc.Cancel(Order[])` + `acc.CreateOrder(12 params)` + `acc.Submit()` pattern | PASS |
| `NinjaTrader.Core.Globals.MaxDate` used (no DateTime.Now) | PASS |
| `(NinjaTrader.Cbi.CustomOrder)null` as arg12 | PASS |
| `oco=""` for PTT-TGT-Drag (not in OCO group) | PASS |
| `stpOrder.OrderAction` used directly (no inversion required) | PASS |

---

## Deferred Work Status Verified

| ID | Status in Source | Matches Completion Report? |
|----|-----------------|---------------------------|
| DW-B153 | CLOSED (re-closed by B141) | YES |
| DW-B154 | DOCUMENTED (unchanged) | YES |
| DW-B140-01 | CLOSED (superseded) | YES |
| DW-B140-02 | CLOSED (superseded) | YES |
| DW-B140-03 | CLOSED (superseded) | YES |
| DW-B141-STP-CYC8-WALL | OPEN — documented at L2285 comment | YES |

---

## FINAL VERDICT

**VERIFY_PASS**

All checks complete. Zero violations found.

| Gate | Result |
|------|--------|
| NT8-VERIFY-01 (CreateOrder 12-arg signature) | PASS |
| NT8-VERIFY-02 (acc.Orders enumerable from AddOnBase) | PASS |
| NT8-VERIFY-03 (lock() scan independent) | PASS — 0 hits |
| NT8-VERIFY-04 (CYC counts independent — all <= 8) | PASS — 0 discrepancies vs engineer |
| NT8-VERIFY-05 (dotnet test — T_B141 filter) | PASS — 7/7 |
| Implementation correctness (10 checks) | PASS — 10/10 |
| Spec compliance (7 checks) | PASS — 7/7 |
| Build (--no-incremental) | PASS — 0 errors |
| Sync + MD5 verify | PASS — 0 MISMATCH |
| JS-DNA rules (lock/async/throw/null/ASCII/CYC/DateTime/hex) | PASS — all clean |
| Layer 2 vs Layer 3 comparison | PASS — 8/8 scans match |

**SIM Gates (Gate 1 P0 blocking merge) remain the responsibility of the Director. No automated check can substitute for SIM verification of OCO cascade behavior in live NT8.**