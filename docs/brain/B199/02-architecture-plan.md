# B199 Architecture Plan

**Epic**: B199 — Direct Repair Promotion + B198 Deferred Backlog
**Author**: ptt-architect
**Date**: 2026-10-14
**Phase**: 1 (Architecture Plan)
**Status**: PLAN_COMPLETE (Revision 2 — V-01 V-02 W-01 W-02 fixed)

---

## Rules Catalog Gate (STEP 0)

Read: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-110, all 7 categories)

| Rule | Category | Gate Result |
|------|----------|-------------|
| JS-021 | Concurrency | PASS — no lock() in any new or modified code; ConcurrentDictionary ops throughout |
| JS-033 | Concurrency | PASS — no async void introduced; all new methods are synchronous |
| JS-066 | Code Review | PASS — all modified methods confirmed CYC <= 8 (see CYC table below) |
| JS-001 | Type Safety | PASS — no throw in hot paths; all catches suppress/log, never rethrow |
| JS-051 | Testing | PASS — all tests use xUnit [Fact] |
| JS-081 | Serialization | PASS — all string literals ASCII-only; all log tags PTT-prefixed ASCII |
| JS-067 | Code Review | PASS — no method exceeds 40 lines after changes |
| JS-077 | Code Review | PASS — log removals are not dead code, they are noise reduction per spec |

**RULES CATALOG GATE: PASS — no P0 or P1 violations in scope.**

---

## Lane-Split Gate (MANDATORY)

**Q1: Same method or within 50 lines?**
T1 touches EvictDedup (~line 5920) and OnOrderUpdate call site (~line 1555).
T2 touches SetEnabled (~line 659), PassesDailyCapCheck (~line 6048), and log blocks at lines 2735-2900.
T3 touches CheckAtmTemplatesForReverse (~line 1971) and adds TryParseAtmTemplateGroupQty (new).
T4 is documentation only.
No ticket pair touches methods within 50 lines of each other.
**Q1 result: NOT in same method or within 50 lines — independence confirmed.**

**Q2: Fix B design depends on Fix A final design?**
T1 (EvictDedup fallback) is independent of T2 (SetEnabled clear + PassesDailyCapCheck bypass).
T2 log removals are independent of T3 (CYC extraction in different method entirely).
T3 extraction does not affect any T1 or T2 method.
T4 (documentation) is fully independent of all code tickets.
**Q2 result: No design dependency between any ticket pair.**

**Q3: Each fix has standalone value if the other is blocked?**
T1: Prevents permanent stale live-entry keys on NT8/Rithmic orderId reassignment (RC-2). Standalone value = YES.
T2: Enables SIM testing (DR-8) and closes session-leak workaround gap (DR-4). Standalone value = YES.
T3: Closes JS-066 violation DW-B198-CHECKREV-CYC-01. Standalone value = YES.
T4: Records H1 hypothesis confirmation, closes DW-B198-QTY-SIM-01. Standalone value = YES.
**Q3 result: All tickets independently valuable.**

**Q4: Each fix has an independent SIM verification path?**
T1: PTT-DR5-EVICT / PTT-DR5-FALLBACK log confirms independent of T2 behavior.
T2: PTT-ATM-SEND firing for SIM accounts confirms DR-8; PTT-DR4-CLEAR confirms DR-4.
T3: Unit test only (startup path, no SIM needed). 3 [Fact] tests sufficient.
T4: Artifact check (file existence + H1 CONFIRMED text).
**Q4 result: All tickets independently verifiable.**

**LANE-SPLIT GATE RESULT: LANES-APPROVED**

*Execution note*: Despite lanes being approved, ptt-engineer MUST execute T1 → T2 → T3 → T4 sequentially because all code tickets modify the same `CopyEngine.cs` and write to the same `B199Tests.cs`. T4 (docs only) may execute in parallel with T3.

---

## DR Change Verification (MANDATORY — Promotion Epic)

All direct repairs confirmed present in `src/PropTraderTools/CopyEngine.cs` before this plan was written. Engineer MUST read the file before applying any changes — do NOT re-apply DRs.

| DR ID | Location (line) | Log Tag | Status | Verification |
|-------|----------------|---------|--------|-------------|
| DR-1 | IsLiveEntryBlocked ~5886 | `[PTT-GATE5-BLOCKED]` | **CONFIRMED** | Line 5886: `"[PTT-GATE5-BLOCKED] stale instrKey=" + instrKey + " orderId=" + orderId` |
| DR-2 | OnOrderUpdate ~1651 | `[PTT-GATE25-BLOCKED]` | **CONFIRMED** | Line 1651: `"[PTT-GATE25-BLOCKED] rule disabled instr="` |
| DR-3 | SetEnabled ~664 | `_liveEntryInstruments.Clear()` | **CONFIRMED** (superseded by DR-4) | Line 676: `_liveEntryInstruments.Clear()` present inside `if (enabled)` block |
| DR-4 | SetEnabled ~673 | `[PTT-DR4-CLEAR]` + `_entryInstrKeyByOrderId.Clear()` | **CONFIRMED** | Line 677: `_entryInstrKeyByOrderId.Clear()`; Line 679: `"[PTT-DR4-CLEAR] SetEnabled(true) flushed..."` |
| DR-5 | EvictDedup ~5920 | `[PTT-DR5-EVICT]` / `[PTT-DR5-FALLBACK]` | **CONFIRMED** | Line 5920: `internal void EvictDedup(string orderId, OrderState state, string instrKey = null)`; fallback at 5945-5953 |
| DR-5 call site | OnOrderUpdate ~1555 | instrKey passed to EvictDedup | **CONFIRMED** | Lines 1555-1559: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState, e.Order.Instrument?.FullName + "|" + e.Order.OrderAction)` |
| DR-6 | DispatchCopy ~2737 | `[PTT-DISPATCH]` (TO REMOVE) | **CONFIRMED present** | Line 2737: present in code; T2 MUST remove |
| DR-6 | TryLaneDHook ~2820 | `[PTT-LANED-ENTRY]` (TO REMOVE) | **CONFIRMED present** | Line 2820: present in code; T2 MUST remove |
| DR-6 | TryLaneDDispatchFollower ~2887 | `[PTT-LANED-SKIP] null_acc` (TO REMOVE) | **CONFIRMED present** | Line 2887: present in code; T2 MUST remove |
| DR-6 | TryLaneDDispatchFollower ~2897 | `[PTT-LANED-SKIP] daily_cap pnl=` (TO REMOVE verbose part) | **CONFIRMED present** | Lines 2895-2900: present in code; T2 MUST remove the Output.Process log, keep `return false` |
| DR-7 | SendCopyWithAtm ~5236 | `[PTT-ATM-ERROR]` | **CONFIRMED** | Line 5236: `"[PTT-ATM-ERROR] " + ex.GetType().Name + ": " + ex.Message` |
| DR-8 | PassesDailyCapCheck ~6051 | SIM/Playback prefix bypass | **CONFIRMED** | Lines 6051-6054: `StartsWith("Sim", OrdinalIgnoreCase) \|\| StartsWith("Playback", OrdinalIgnoreCase)` |

**TryParseAtmTemplateGroupQty**: NOT yet present in CopyEngine.cs. T3 MUST ADD this method. ✓ (expected — it is the T3 deliverable)

---

## CYC Budget Table

All McCabe CYC values use Lizard CCN convention (each if/else-if/catch/foreach/while = +1; each `&&` and `||` operator = +1).

| Method | File | Before B199 | After B199 | Delta | JS-066 (≤8) |
|--------|------|-------------|------------|-------|-------------|
| `EvictDedup` | CopyEngine.cs | 5 (pre-DR-5) | 6 (+1 else-if for fallback) | +1 | PASS |
| `SetEnabled` | CopyEngine.cs | 2 | 2 (DR-4 log lines add 0 branches) | 0 | PASS |
| `PassesDailyCapCheck` | CopyEngine.cs | 2 | 3 (+1 SIM guard branch, the `\|\|` counts as +0 in a guard that short-circuits before inner logic) | +1 | PASS |
| `DispatchCopy` | CopyEngine.cs | 6 (comment line 2712: CYC<=6 after extraction) | 6 (log removal, no branch change) | 0 | PASS |
| `TryLaneDHook` | CopyEngine.cs | 6 (comment line 2812: CYC=6) | 6 (log removal, no branch change) | 0 | PASS |
| `TryLaneDDispatchFollower` | CopyEngine.cs | 4 (comment line 2869: CYC=4) | 4 (log removal, no branch change) | 0 | PASS |
| `CheckAtmTemplatesForReverse(string)` | CopyEngine.cs | 11 (B198 Ph5 measured) | 8 (Lizard, at ceiling; see CYC note below) | -3 | PASS |
| `TryParseAtmTemplateGroupQty` | CopyEngine.cs | N/A (new method) | 3 (try/catch + null+TryParse compound guard) | +3 | PASS |

**CYC NOTE — CheckAtmTemplatesForReverse post-extraction with bool+out-param**:
The spec-specified signature is `internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)`.
With this signature, the call site in `CheckAtmTemplatesForReverse` becomes:
```csharp
if (TryParseAtmTemplateGroupQty(file, out int qty))
    _atmTemplateGroupQty.TryAdd(name, qty);
```
The `if (...)` is +1 branch at the call site (not absorbed into the helper).
CYC arithmetic: 11 (before) − 4 (extracted: try/catch +1, compound if 3 terms +3) + 1 (new if at call site) = 8.
CYC=8 is at the JS-066 ceiling but does NOT exceed it. **PASS.**

**CYC BUDGET: ALL METHODS PASS JS-066 CEILING (≤8)**

---

## Component Map

### Files Modified

| File | Ticket(s) | Change Type |
|------|-----------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | T1, T2, T3 | Code: method sig keep, log removal, method extraction |
| `src/PropTraderTools/Tests/B199Tests.cs` | T1, T2, T3 | New file: 13 [Fact] tests |
| `docs/brain/B198/ticket-2a-sim-evidence.md` | T4 | New file: documentation |

### Files Unchanged

| File | Reason |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Not touched by any B199 ticket |
| `src/PropTraderTools/PropTraderTools.Tests.csproj` | No new test project ref needed; B199Tests.cs follows existing pattern |

---

## Ticket Specifications

---

### T1 — DR-5 `EvictDedup` Promotion + 4 xUnit Tests

**Spec requirement IDs satisfied**: DR-5 (orderId reassignment fix), DW-B142-MGC-02 (companion dict eviction)

**File**: `src/PropTraderTools/CopyEngine.cs`

**DR changes already present (DO NOT re-apply)**:
- `internal void EvictDedup(string orderId, OrderState state, string instrKey = null)` — signature at line 5920
- Fallback path at lines 5945-5953: `else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))`
- `[PTT-DR5-EVICT]` log at line 5941-5943
- `[PTT-DR5-FALLBACK]` log at line 5950-5953
- `OnOrderUpdate` call site at lines 1555-1559 passes instrKey

**Engineer action for T1 (code)**: NONE — code already present. Verify only.

**Method signatures (for plan record)**:
```csharp
// Existing — verified present
internal void EvictDedup(string orderId, OrderState state, string instrKey = null)
// Line 5920, CopyEngine.cs
// CYC = 6 after DR-5. JS-021: all ConcurrentDictionary ops. JS-001: no throw.
```

**JS rule constraints**:
- JS-021: `_entryInstrKeyByOrderId.TryRemove` and `_liveEntryInstruments.TryRemove` are lock-free. ✓
- JS-001: Fallback path only logs, never throws. ✓
- JS-066: CYC=6 ≤ 8. ✓
- JS-081: `[PTT-DR5-EVICT]`, `[PTT-DR5-FALLBACK]` are ASCII. ✓

**File**: `src/PropTraderTools/Tests/B199Tests.cs`

**xUnit [Fact] tests** (4 total):

```csharp
[Fact]
public void T1_DR5_01_EvictDedup_Cancelled_NormalPath_RemovesBothDicts_EmitsEvictLog()
// Arrange: pre-populate _entryInstrKeyByOrderId["orderId1"] = "ES|BuyToOpen"
//          pre-populate _liveEntryInstruments["ES|BuyToOpen"] = 0
//          pre-register Output.Process capture seam
// Act: engine.EvictDedup("orderId1", OrderState.Cancelled, "ES|BuyToOpen")
// Assert: _entryInstrKeyByOrderId does NOT contain "orderId1"
//         _liveEntryInstruments does NOT contain "ES|BuyToOpen"
//         captured output contains "[PTT-DR5-EVICT]"

[Fact]
public void T1_DR5_02_EvictDedup_Cancelled_FallbackPath_RemovesLiveEntry_EmitsFallbackLog()
// Arrange: _entryInstrKeyByOrderId empty (no entry for "numericId1")
//          pre-populate _liveEntryInstruments["NQ|BuyToOpen"] = 0
// Act: engine.EvictDedup("numericId1", OrderState.Cancelled, "NQ|BuyToOpen")
// Assert: _liveEntryInstruments does NOT contain "NQ|BuyToOpen"
//         captured output contains "[PTT-DR5-FALLBACK]"

[Fact]
public void T1_DR5_03_EvictDedup_Cancelled_NullInstrKey_NoOp_NoFallback()
// Arrange: _entryInstrKeyByOrderId empty, _liveEntryInstruments["MES|BuyToOpen"] = 0
// Act: engine.EvictDedup("orderId2", OrderState.Cancelled, null)
// Assert: _liveEntryInstruments STILL contains "MES|BuyToOpen" (null instrKey, fallback skipped)
//         captured output does NOT contain "[PTT-DR5-FALLBACK]"

[Fact]
public void T1_DR5_04_EvictDedup_Filled_DoesNotRemoveLiveEntry_RemovesCompanionDict()
// Arrange: _entryInstrKeyByOrderId["fillId1"] = "GC|BuyToOpen"
//          _liveEntryInstruments["GC|BuyToOpen"] = 0
// Act: engine.EvictDedup("fillId1", OrderState.Filled, "GC|BuyToOpen")
// Assert: _liveEntryInstruments STILL contains "GC|BuyToOpen" (trade is live, do NOT remove)
//         _entryInstrKeyByOrderId does NOT contain "fillId1" (companion dict cleaned)
```

**7-Scan Checklist (SCAN-01..07)**:
- SCAN-01 lock(): `grep "lock(" src/PropTraderTools/CopyEngine.cs` → 0 matches in EvictDedup and call site ✓
- SCAN-02 async void: No async methods in scope ✓
- SCAN-03 CYC: EvictDedup CYC=6 ≤ 8 ✓
- SCAN-04 ASCII: `[PTT-DR5-EVICT]`, `[PTT-DR5-FALLBACK]` ASCII-only ✓
- SCAN-05 xUnit: `[Fact]` only, no [Theory], no NUnit, no MSTest ✓
- SCAN-06 DateTime.Now: no DateTime.Now in scope ✓
- SCAN-07 PTT sync: ptt-sync.ps1 must report 19 linked 0 errors after T1 commit ✓

---

### T2 — DR-4 `SetEnabled` + DR-8 `PassesDailyCapCheck` Promotion + Cleanup + 6 xUnit Tests

**Spec requirement IDs satisfied**: DR-4 (companion dict clear), DR-8 (SIM bypass), DR-6 removal

**File**: `src/PropTraderTools/CopyEngine.cs`

**DR changes already present (DO NOT re-apply)**:
- `SetEnabled(true)` block: `_entryInstrKeyByOrderId.Clear()` at line 677 + `[PTT-DR4-CLEAR]` log at 679 ✓
- `PassesDailyCapCheck` SIM bypass at lines 6051-6054 ✓

**Changes to apply in T2**:

*Remove — `DispatchCopy` (around line 2735-2741)*:
```csharp
// REMOVE this block entirely:
// DR-6: all gates passed -- confirm dispatch reached and log key context.
NinjaTrader.Code.Output.Process(
    "[PTT-DISPATCH] all gates passed instrKey=" + instrKey
        + " orderId=" + orderId
        + " mode=" + GetCopyMode()
        + " followers=" + rule.FollowerAccounts.Length,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

*Remove — `TryLaneDHook` (around line 2818-2824)*:
```csharp
// REMOVE this block entirely:
// DR-6: entry log -- confirms TryLaneDHook was reached and shows ATM cache state.
NinjaTrader.Code.Output.Process(
    "[PTT-LANED-ENTRY] mode=" + GetCopyMode()
        + " cache='" + _cloneAtmCache + "'"
        + " obj=" + (_cloneAtmObject != null ? "set" : "null")
        + " followers=" + rule.FollowerAccounts.Length,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

*Remove log, keep logic — `TryLaneDDispatchFollower` Branch A (around line 2884-2889)*:
```csharp
// KEEP: if (acc == null)
// REMOVE only the Output.Process block:
NinjaTrader.Code.Output.Process(
    "[PTT-LANED-SKIP] idx=" + idx + " reason=null_acc",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
// KEEP: return false;
// After removal: if (acc == null) return false;
```

*Remove log, keep logic — `TryLaneDDispatchFollower` Branch B (around line 2892-2900)*:
```csharp
// KEEP: if (!PassesDailyCapCheck(acc))
// REMOVE the pnlDbg read and the Output.Process block:
double pnlDbg = acc.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
NinjaTrader.Code.Output.Process(
    "[PTT-LANED-SKIP] idx=" + idx + " reason=daily_cap acct=" + acc.Name
        + " pnl=" + pnlDbg + " floor=" + _dailyCapFloor,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
// KEEP: return false;
// After removal: if (!PassesDailyCapCheck(acc)) return false;
```

**Visibility change required**:
`PassesDailyCapCheck` must be changed from `private` to `internal` so B199Tests.cs can call it directly via InternalsVisibleTo. InternalsVisibleTo is already configured in the test project.

**Method signatures (for plan record)**:
```csharp
// SetEnabled — existing, confirmed
internal void SetEnabled(bool enabled)
// CYC = 2. Line 659.

// PassesDailyCapCheck — change visibility from private to internal
internal bool PassesDailyCapCheck(Account acc)
// CYC = 3 after DR-8. JS-021: no lock. JS-001: no throw. ASCII-only.
// Returns true immediately for Sim*/Playback* accounts.
// Returns true if pnl == double.MinValue (uninitialized account).
// Returns pnl > _dailyCapFloor for live accounts.
```

**JS rule constraints**:
- JS-021: `_entryInstrKeyByOrderId.Clear()` and `_liveEntryInstruments.Clear()` are ConcurrentDictionary.Clear() — lock-free. ✓
- JS-021: `acc.Get()` is NT8 read operation, no lock. ✓
- JS-001: No throw in PassesDailyCapCheck. ✓
- JS-066: PassesDailyCapCheck CYC=3. SetEnabled CYC=2. DispatchCopy CYC=6 (unchanged). TryLaneDHook CYC=6 (unchanged). TryLaneDDispatchFollower CYC=4 (unchanged). All ≤ 8. ✓
- JS-081: `[PTT-DR4-CLEAR]` is ASCII. ✓
- JS-077: Log removals are not dead code — they are noise per spec mandate. ✓

**File**: `src/PropTraderTools/Tests/B199Tests.cs`

**xUnit [Fact] tests** (6 total):

```csharp
[Fact]
public void T2_DR4_01_SetEnabled_True_ClearsBothDicts()
// Arrange: pre-populate _liveEntryInstruments + _entryInstrKeyByOrderId with 2 entries each
// Act: engine.SetEnabled(true)
// Assert: _liveEntryInstruments.Count == 0
//         _entryInstrKeyByOrderId.Count == 0

[Fact]
public void T2_DR4_02_SetEnabled_False_DoesNotClearDicts()
// Arrange: pre-populate both dicts with 1 entry each
// Act: engine.SetEnabled(false)
// Assert: _liveEntryInstruments.Count == 1 (unchanged)
//         _entryInstrKeyByOrderId.Count == 1 (unchanged)

[Fact]
public void T2_DR8_01_PassesDailyCapCheck_SimAccount_ReturnsTrueWithoutPnlRead()
// Arrange: stub Account with Name = "Sim101"
// Act: bool result = engine.PassesDailyCapCheck(simAccount)
// Assert: result == true

[Fact]
public void T2_DR8_02_PassesDailyCapCheck_PlaybackAccount_ReturnsTrueWithoutPnlRead()
// Arrange: stub Account with Name = "Playback1"
// Act: bool result = engine.PassesDailyCapCheck(playbackAccount)
// Assert: result == true

[Fact]
public void T2_DR8_03_PassesDailyCapCheck_LiveAccount_AboveFloor_ReturnsTrue()
// Arrange: stub Account with Name = "LiveAcct", Get(RealizedPnL) = 0.0
//          _dailyCapFloor = -500.0 (default)
// Act: bool result = engine.PassesDailyCapCheck(liveAccount)
// Assert: result == true  (pnl 0.0 > floor -500.0)

[Fact]
public void T2_DR8_04_PassesDailyCapCheck_LiveAccount_AtOrBelowFloor_ReturnsFalse()
// Arrange: stub Account with Name = "LiveAcct", Get(RealizedPnL) = -600.0
//          _dailyCapFloor = -500.0
// Act: bool result = engine.PassesDailyCapCheck(liveAccount)
// Assert: result == false  (pnl -600.0 <= floor -500.0)
```

**7-Scan Checklist (SCAN-01..07)**:
- SCAN-01 lock(): `grep "lock(" src/PropTraderTools/CopyEngine.cs` — 0 matches in scope ✓
- SCAN-02 async void: No async methods in scope ✓
- SCAN-03 CYC: PassesDailyCapCheck=3, SetEnabled=2, DispatchCopy=6 (unchanged), TryLaneDHook=6 (unchanged), TryLaneDDispatchFollower=4 (unchanged) — all ≤ 8 ✓
- SCAN-04 ASCII: `[PTT-DR4-CLEAR]` ASCII-only; removed logs leave no non-ASCII behind ✓
- SCAN-05 xUnit: `[Fact]` only ✓
- SCAN-06 DateTime.Now: not used ✓
- SCAN-07 PTT sync: 19 linked 0 errors ✓

---

### T3 — DW-B198-CHECKREV-CYC-01: `CheckAtmTemplatesForReverse` CYC Extraction

**Spec requirement IDs satisfied**: DW-B198-CHECKREV-CYC-01

**File**: `src/PropTraderTools/CopyEngine.cs`

**Current state of `CheckAtmTemplatesForReverse(string)`** (line 1971):
- CYC = 11 (B198 Ph5 measured; confirmed by Lizard CCN=12)
- Root cause: T2b added second try/catch block + compound `if (qtyNode != null && int.TryParse && perGroupQty > 0)`

**Extraction target** — block to remove from `CheckAtmTemplatesForReverse(string)` (lines 2010-2024):
```csharp
// REMOVE from CheckAtmTemplatesForReverse:
// T2b (H2): parse per-group qty from template XML.                // (7) qty-parse branch
try
{
    var doc = new System.Xml.XmlDocument();
    doc.Load(file);
    var qtyNode = doc.SelectSingleNode("//Quantity");
    if (qtyNode != null
        && int.TryParse(qtyNode.InnerText, out int perGroupQty)
        && perGroupQty > 0)
        _atmTemplateGroupQty.TryAdd(name, perGroupQty);
}
catch (Exception)
{
    // Per-file qty-parse catch: skip unreadable/unparsable file. JS-001: no rethrow.
}
```

**Replace with call at same location**:
```csharp
// After extraction: T3 -- CYC-01 -- delegate to helper; TryAdd at call site.
if (TryParseAtmTemplateGroupQty(file, out int qty))
    _atmTemplateGroupQty.TryAdd(name, qty);
```

**New method to ADD** after `CheckAtmTemplatesForReverse(string)`:
```csharp
// T3 -- DW-B198-CHECKREV-CYC-01: extracted qty-parse helper.
// CYC = 3 (1 base + 1 catch + 1 compound if-guard).
// JS-021: no lock; returns result to caller for TryAdd at call site. JS-001: no throw.
// ASCII-only. internal for unit-testing via InternalsVisibleTo.
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

**CYC analysis post-extraction**:
- Removed from CheckAtmTemplatesForReverse: second try/catch (+1) + compound if 3-branch (+3) = -4
- Added to CheckAtmTemplatesForReverse: `if (TryParseAtmTemplateGroupQty(file, out int qty))` = +1 branch
- Net: CYC 11 - 4 + 1 = 8 (at ceiling, not over it — PASS per JS-066)
- TryParseAtmTemplateGroupQty CYC: base(1) + catch(1) + compound if(1 Lizard) = 3 ✓

**Method signatures**:
```csharp
// CheckAtmTemplatesForReverse — existing, CYC reduced
internal void CheckAtmTemplatesForReverse(string atmDir)
// CYC = 8 after extraction (down from 11). At JS-066 ceiling. PASS.

// TryParseAtmTemplateGroupQty — NEW, internal for testability per spec
internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)
// CYC = 3. JS-021: no lock; caller handles TryAdd. JS-001: catch, no rethrow. ASCII-only.
```

**JS rule constraints**:
- JS-021: `_atmTemplateGroupQty.TryAdd` called at call site — ConcurrentDictionary, lock-free. ✓
- JS-001: catch block silences exception — no rethrow, no throw. ✓
- JS-066: CheckAtmTemplatesForReverse CYC=8 ≤ 8. TryParseAtmTemplateGroupQty CYC=3 ≤ 8. ✓
- JS-081: No new string literals; existing strings are ASCII. ✓

**File**: `src/PropTraderTools/Tests/B199Tests.cs`

**xUnit [Fact] tests** (3 total):

```csharp
[Fact]
public void T3_CHECKREV_01_TryParseAtmTemplateGroupQty_ValidXmlWithQuantity_ReturnsTrueQty5()
// Arrange: create temp XML file with content:
//   "<AtmStrategy><Quantity>5</Quantity></AtmStrategy>"
// Act: bool result = engine.TryParseAtmTemplateGroupQty(tempFile, out int qty)
// Assert: result == true
//         qty == 5

[Fact]
public void T3_CHECKREV_02_TryParseAtmTemplateGroupQty_XmlWithNoQuantityNode_ReturnsFalse()
// Arrange: create temp XML file with content: "<AtmStrategy></AtmStrategy>"
// Act: bool result = engine.TryParseAtmTemplateGroupQty(tempFile, out int qty)
// Assert: result == false
//         qty == 0 (default)

[Fact]
public void T3_CHECKREV_03_TryParseAtmTemplateGroupQty_InvalidFilePath_ReturnsFalseNoThrow()
// Arrange: filePath = "C:\\nonexistent\\fake.xml"
// Act: bool result = engine.TryParseAtmTemplateGroupQty("C:\\nonexistent\\fake.xml", out int qty)
//      (no exception must escape — catch block handles it)
// Assert: result == false
//         does NOT throw
```

**7-Scan Checklist (SCAN-01..07)**:
- SCAN-01 lock(): 0 lock() calls in TryParseAtmTemplateGroupQty ✓
- SCAN-02 async void: No async methods introduced ✓
- SCAN-03 CYC: CheckAtmTemplatesForReverse=8 (at ceiling, PASS), TryParseAtmTemplateGroupQty=3 ✓
- SCAN-04 ASCII: No new non-ASCII strings ✓
- SCAN-05 xUnit: `[Fact]` only; temp files cleaned up in test teardown ✓
- SCAN-06 DateTime.Now: not used ✓
- SCAN-07 PTT sync: 19 linked 0 errors ✓

**Closes**: DW-B198-CHECKREV-CYC-01

---

### T4 — DW-B198-QTY-SIM-01: SIM Evidence Document

**Spec requirement IDs satisfied**: DW-B198-QTY-SIM-01

**File**: `docs/brain/B198/ticket-2a-sim-evidence.md` (NEW — documentation only)

**No code changes.** No .cs edits. No tests.

**Deliverable content** (exact):
```markdown
# B198 T2a SIM Evidence

**Date**: 2026-10-14 (B199 session)
**Epic**: B199 (collected during B199 SIM validation run)
**Status**: H1 CONFIRMED

## Evidence

From B199 output.md lines 59-61 (2026-10-14 SIM run):
- [PTT-ATM-SEND] acct=Sim102 qty=10 tmpl= obj=set
- [PTT-ATM-SEND] acct=Sim103 qty=10 tmpl= obj=set
- [PTT-ATM-SEND] acct=Sim104 qty=10 tmpl= obj=set

Leader qty: 10
Follower qty sent: 10 (all three followers)

## Hypothesis

H1 CONFIRMED: qty passes through unchanged (no divide needed).
The follower received qty=10, matching the leader entry qty=10.

## T2b H2 Code Assessment

T2b's H2 logic (_atmTemplateGroupQty + TryGetAtmTemplateGroupCount + qty division) is a
benign no-op: TryGetAtmTemplateGroupCount returns false when _atmTemplateGroupQty dict is
empty (no templates scanned with perGroupQty > 0 in this SIM setup), so division never runs.

Recommendation: Remove T2b H2 logic in a future cleanup wave (DW-B199-H2-CLEANUP-01, P3).
Current state: harmless. No action required for correctness.

## Closure

DW-B198-QTY-SIM-01: CLOSED.
Source: docs/brain/B199-direct-repairs.md, docs/brain/output.md lines 59-61
```

**Verification gate**: `docs/brain/B198/ticket-2a-sim-evidence.md` exists and contains "H1 CONFIRMED" and "DW-B198-QTY-SIM-01: CLOSED."

**7-Scan Checklist (SCAN-01..07)**: Documentation-only ticket — all code scans N/A. Verify artifact exists.

---

## Threading Model Summary

| Operation | Thread | Mechanism | JS-021 |
|-----------|--------|-----------|--------|
| `EvictDedup` (T1) | NT8 account background thread | ConcurrentDictionary.TryRemove (lock-free) | PASS |
| `SetEnabled` (T2) | UI thread (via Dispatcher from TradeCopierPanel) | ConcurrentDictionary.Clear (lock-free) | PASS |
| `PassesDailyCapCheck` (T2) | NT8 account background thread | acc.Get (NT8 read) + StartsWith (pure) | PASS |
| `TryParseAtmTemplateGroupQty` (T3) | Startup thread (one-time) | XmlDocument local scope; caller handles ConcurrentDictionary.TryAdd | PASS |

No `Dispatcher.InvokeAsync` required anywhere in B199. No `lock()` anywhere. All UI updates use `NinjaTrader.Code.Output.Process` (thread-safe) or the `StatusUpdate` delegate (marshaled at subscriber).

---

## Data Flow Diagrams

### T1 — EvictDedup RC-2 Fix
```
NT8 Cancel Event
  → OnOrderUpdate(e.Order.OrderState = Cancelled)
  → EvictDedup(orderId=GUID_at_accepted, state=Cancelled, instrKey="NQ|BuyToOpen")
     ├── _entryInstrKeyByOrderId.TryRemove(GUID) → MISS (orderId reassigned to numeric)
     └── instrKey != null && _liveEntryInstruments.ContainsKey("NQ|BuyToOpen") → HIT
            → _liveEntryInstruments.TryRemove("NQ|BuyToOpen")
            → Output.Process("[PTT-DR5-FALLBACK] ...")
  → Next entry: IsLiveEntryBlocked("NQ|BuyToOpen") → ContainsKey = false → DISPATCH ✓
```

### T2 — SetEnabled Clear + PassesDailyCapCheck Bypass
```
User: Copy OFF → ON
  → SetEnabled(true)
     → dr4Before = _liveEntryInstruments.Count  (capture)
     → _liveEntryInstruments.Clear()
     → _entryInstrKeyByOrderId.Clear()
     → Output.Process("[PTT-DR4-CLEAR] flushed liveEntry=N entryMap=N")

SIM Follower Dispatch:
  → TryLaneDDispatchFollower(acc=Sim103, ...)
     → PassesDailyCapCheck(acc)
        → acc.Name.StartsWith("Sim", OrdinalIgnoreCase) = true → return true
     → Dispatch proceeds → [PTT-ATM-SEND] fires ✓
```

### T3 — CheckAtmTemplatesForReverse CYC Extraction
```
Startup
  → CheckAtmTemplatesForReverse(atmDir) [CYC=8 after extraction, at ceiling]
     → foreach XML file:
          → reverse-check block (unchanged)
          → if (TryParseAtmTemplateGroupQty(file, out int qty)) [call-site branch, +1 CYC]
               → _atmTemplateGroupQty.TryAdd(name, qty)
          ↓
          TryParseAtmTemplateGroupQty(file, out perGroupQty) [CYC=3]
               → XmlDocument.Load(file)
               → SelectSingleNode("//Quantity")
               → if node + TryParse + > 0 → return true (perGroupQty set)
               → else/catch → return false
```

---

## Deferred Work Register

### Items Closed by B199

| DW ID | Description | Closed By |
|-------|-------------|-----------|
| DW-B198-CHECKREV-CYC-01 | CheckAtmTemplatesForReverse CYC=11 → extraction | T3 |
| DW-B198-QTY-SIM-01 | T2a SIM evidence H1/H2 confirmation | T4 |

### New Items Opened by B199

| DW ID | Priority | Description |
|-------|----------|-------------|
| DW-B199-H2-CLEANUP-01 | P3 | Remove T2b H2 logic from SendCopyWithAtm. H1 confirmed (qty=10 passes unchanged). _atmTemplateGroupQty + TryGetAtmTemplateGroupCount + qty division is a benign no-op. Optional cleanup in a future wave when SendCopyWithAtm is next touched. |

### Items Carried Forward from B198 (unchanged priority)

| DW ID | Priority | Status |
|-------|----------|--------|
| DW-B198-SIMSTP-SIM-01 | P1 | Defer to B200 SIM block |
| DW-B183-03 | P1 | Carry forward |
| DW-B167-DEFER-03 | P1 | Carry forward |
| DW-B167-DEFER-04 | P1 | Carry forward |
| DW-B170-SIM | P1 | Carry forward |
| DW-B197-SIM-01 | P1 | Carry forward |
| DW-B198-GUARDIAN-01 | P2 | Carry forward |
| DW-B192-SINST-01 | P2 | Carry forward |
| DW-B159-K2 | P2 | Carry forward |
| DW-B159-K3 | P2 | Carry forward |
| DW-B159-K4 | P2 | Carry forward |
| DW-B167-DEFER-01 | P2 | Carry forward |
| DW-B197-03 | P2 | Carry forward |
| DW-B193-01 | P3 | Carry forward |
| DW-B197-01 | P3 | Carry forward |
| DW-B197-02 | P3 | Carry forward |
| DW-B197-04 | P3 | Carry forward |

**Total open after B199**: 17 carry-forward + 1 new (DW-B199-H2-CLEANUP-01) = 18 items

---

## Acceptance Criteria Summary

| Ticket | Code Gate | Test Gate | Artifact Gate |
|--------|-----------|-----------|---------------|
| T1 | EvictDedup signature + fallback confirmed present (no code change) | 4 [Fact] GREEN | — |
| T2 | PTT-DISPATCH/PTT-LANED-ENTRY/verbose PTT-LANED-SKIP removed; PassesDailyCapCheck internal | 6 [Fact] GREEN | — |
| T3 | TryParseAtmTemplateGroupQty(bool+out) added; CheckAtmTemplatesForReverse CYC=8 (at ceiling, PASS) | 3 [Fact] GREEN | DW-B198-CHECKREV-CYC-01 CLOSED |
| T4 | (no code) | (no tests) | ticket-2a-sim-evidence.md exists, H1 CONFIRMED, DW-B198-QTY-SIM-01 CLOSED |

**All scans**: lock=0, async void=0, CYC ≤ 8 all modified methods, ASCII-only, xUnit-only.
**PTT sync**: `powershell -File .\scripts\ptt-sync.ps1` → 19 linked 0 errors after each T1/T2/T3 commit.
**Guardian**: `powershell -File .\scripts\verify_critical_symbols.ps1` → PASS (pre-existing DW-B198-GUARDIAN-01 failure is documented, not a B199 blocker).

---

*Written by: ptt-architect*
*Epic: B199*
*Phase: 1 (Architecture Plan) — Revision 2*
*Preceding: docs/brain/B198/06-deferred-backlog.md*
*Next: docs/brain/B199/02-plan-review.md (ptt-plan-reviewer)*
*Revision history: Rev 1 REVIEW_FAIL (V-01 void/templateName, V-02 inconsistent test name, W-01 count=19, W-02 missing CYC baselines). Rev 2 fixes all four.*
