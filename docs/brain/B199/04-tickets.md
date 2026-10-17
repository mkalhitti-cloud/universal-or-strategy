# B199 Tickets

**Epic**: B199 — Direct Repair Promotion + B198 Deferred Backlog
**Author**: ptt-architect
**Date**: 2026-10-14
**Phase**: 3 (Ticket Generation)
**Source plan**: docs/brain/B199/02-architecture-plan.md (Revision 2 — REVIEW_PASS)
**Reviewer gate**: ptt-ticket-reviewer → docs/brain/B199/04-ticket-review.md (TICKET_REVIEW_PASS gate)

---

## Execution Order

Tickets MUST be executed sequentially: T1 → T2 → T3 → T4.
T4 (documentation only) MAY execute in parallel with T3, but T1 and T2 MUST complete first.
All code tickets (T1, T2, T3) modify `src/PropTraderTools/CopyEngine.cs` and write to
`src/PropTraderTools/Tests/B199Tests.cs`. Sequential execution prevents merge conflicts.

**Ph4a engineer MUST read `CopyEngine.cs` before applying any changes.**
**DR changes are already in code. Do NOT re-apply DR-4, DR-5, or DR-8 changes.**
**ptt-sync.ps1 MUST be run after each code ticket commit (T1, T2, T3).**

---

## Ticket 1 — DR-5 `EvictDedup` Promotion + 4 xUnit Tests

### Spec Requirements Satisfied
- DR-5: EvictDedup orderId reassignment fix (GUID→numeric NT8/Rithmic orderId fallback)
- DW-B142-MGC-02: Companion dict eviction (instrKey→orderId eviction path)

### File: `src/PropTraderTools/CopyEngine.cs`

**Engineer action: VERIFY ONLY — no code changes.**
All DR-5 changes are already present in `CopyEngine.cs`. The engineer MUST confirm the
following are present before moving to T2. If any item is missing, STOP and report.

**Verify present (DO NOT re-apply)**:
1. Method signature at ~line 5920:
   ```csharp
   internal void EvictDedup(string orderId, OrderState state, string instrKey = null)
   ```
2. Fallback block at ~lines 5945-5953 inside the `Cancelled` branch:
   ```csharp
   else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))
   {
       _liveEntryInstruments.TryRemove(instrKey, out _);
       NinjaTrader.Code.Output.Process(
           "[PTT-DR5-FALLBACK] instrKey=" + instrKey + " orderId=" + orderId,
           NinjaTrader.NinjaScript.PrintTo.OutputTab1);
   }
   ```
3. Primary evict log `[PTT-DR5-EVICT]` at ~lines 5941-5943 (fires when orderId key is found).
4. OnOrderUpdate call site at ~lines 1555-1559:
   ```csharp
   EvictDedup(
       e.Order.OrderId.ToString(),
       e.Order.OrderState,
       e.Order.Instrument?.FullName + "|" + e.Order.OrderAction);
   ```

### Method Signatures (for record)

```csharp
// Existing — verified present — do not re-add
internal void EvictDedup(string orderId, OrderState state, string instrKey = null)
// CopyEngine.cs ~line 5920
// CYC = 6 (base 1 + state == Cancelled branch +1 + TryRemove found +1 + else-if fallback +1
//           + instrKey != null && ContainsKey compound +1 = 5 decisions total, base 1 = CYC 6)
// JS-021: ConcurrentDictionary.TryRemove (lock-free). No lock() anywhere.
// JS-001: catch/fallback only logs, never throws or rethrows.
// JS-066: CYC=6 <= 8. PASS.
// JS-081: [PTT-DR5-EVICT], [PTT-DR5-FALLBACK] are ASCII-only.
```

### JS Rule Constraints

| Rule | Applies To | Requirement | Status |
|------|-----------|-------------|--------|
| JS-021 | `EvictDedup` | `_entryInstrKeyByOrderId.TryRemove` and `_liveEntryInstruments.TryRemove` are lock-free ConcurrentDictionary ops — no lock() | MUST PASS |
| JS-001 | `EvictDedup` fallback branch | No throw, no rethrow — only log on fallback path | MUST PASS |
| JS-033 | `EvictDedup` | Synchronous method — no async void | MUST PASS |
| JS-066 | `EvictDedup` | CYC=6 <= 8 after DR-5 (baseline was CYC=5 pre-DR-5, +1 for else-if fallback) | MUST PASS |
| JS-081 | `[PTT-DR5-EVICT]`, `[PTT-DR5-FALLBACK]` | ASCII-only string literals — no Unicode, no emoji, no curly quotes | MUST PASS |

### File: `src/PropTraderTools/Tests/B199Tests.cs`

**Engineer action: CREATE this file (new). Add the 13 total [Fact] tests for B199 (T1=4, T2=6, T3=3).**
T1 adds the first 4 facts. T2 and T3 append to the same file.

**Test class scaffold**:
```csharp
using Xunit;
// InternalsVisibleTo is already configured in PropTraderTools.Tests.csproj for CopyEngine.
// All internal methods (EvictDedup, SetEnabled, PassesDailyCapCheck,
// TryParseAtmTemplateGroupQty) are accessible via the test project.

namespace PropTraderTools.Tests
{
    public class B199Tests
    {
        // T1 facts — DR-5 EvictDedup promotion (4 facts)
        // T2 facts — DR-4 + DR-8 promotion (6 facts) — added by T2 execution
        // T3 facts — DW-B198-CHECKREV-CYC-01 (3 facts) — added by T3 execution
    }
}
```

### xUnit [Fact] Tests — T1 (4 facts)

**Test 1: T1_DR5_01**
```csharp
[Fact]
public void T1_DR5_01_EvictDedup_Cancelled_NormalPath_RemovesBothDicts_EmitsEvictLog()
// Spec: DR-5 normal eviction path (orderId key found in _entryInstrKeyByOrderId)
// Arrange:
//   - Pre-populate _entryInstrKeyByOrderId["orderId1"] = "ES|BuyToOpen"
//   - Pre-populate _liveEntryInstruments["ES|BuyToOpen"] = 0
//   - Attach Output.Process capture seam to collect log output
// Act:
//   engine.EvictDedup("orderId1", OrderState.Cancelled, "ES|BuyToOpen")
// Assert:
//   - _entryInstrKeyByOrderId does NOT contain key "orderId1"
//   - _liveEntryInstruments does NOT contain key "ES|BuyToOpen"
//   - captured output contains "[PTT-DR5-EVICT]"
//   - captured output does NOT contain "[PTT-DR5-FALLBACK]" (primary path taken, not fallback)
```

**Test 2: T1_DR5_02**
```csharp
[Fact]
public void T1_DR5_02_EvictDedup_Cancelled_FallbackPath_RemovesLiveEntry_EmitsFallbackLog()
// Spec: DR-5 fallback path (orderId NOT in _entryInstrKeyByOrderId — GUID reassigned to numeric)
// Arrange:
//   - _entryInstrKeyByOrderId is empty (no entry for "numericId1")
//   - Pre-populate _liveEntryInstruments["NQ|BuyToOpen"] = 0
//   - Attach Output.Process capture seam
// Act:
//   engine.EvictDedup("numericId1", OrderState.Cancelled, "NQ|BuyToOpen")
// Assert:
//   - _liveEntryInstruments does NOT contain key "NQ|BuyToOpen"
//   - captured output contains "[PTT-DR5-FALLBACK]"
```

**Test 3: T1_DR5_03**
```csharp
[Fact]
public void T1_DR5_03_EvictDedup_Cancelled_NullInstrKey_NoOp_NoFallback()
// Spec: DR-5 null instrKey guard (fallback must not fire when instrKey is null)
// Arrange:
//   - _entryInstrKeyByOrderId is empty (no entry for "orderId2")
//   - Pre-populate _liveEntryInstruments["MES|BuyToOpen"] = 0
// Act:
//   engine.EvictDedup("orderId2", OrderState.Cancelled, null)
// Assert:
//   - _liveEntryInstruments STILL contains key "MES|BuyToOpen"
//     (null instrKey prevents fallback from firing — no removal)
//   - captured output does NOT contain "[PTT-DR5-FALLBACK]"
```

**Test 4: T1_DR5_04**
```csharp
[Fact]
public void T1_DR5_04_EvictDedup_Filled_DoesNotRemoveLiveEntry_RemovesCompanionDict()
// Spec: DR-5 Filled state — _liveEntryInstruments must NOT be removed (trade is live)
//       but _entryInstrKeyByOrderId companion entry IS cleaned up
// Arrange:
//   - Pre-populate _entryInstrKeyByOrderId["fillId1"] = "GC|BuyToOpen"
//   - Pre-populate _liveEntryInstruments["GC|BuyToOpen"] = 0
// Act:
//   engine.EvictDedup("fillId1", OrderState.Filled, "GC|BuyToOpen")
// Assert:
//   - _liveEntryInstruments STILL contains key "GC|BuyToOpen"
//     (Filled = trade is live, gate key must remain)
//   - _entryInstrKeyByOrderId does NOT contain key "fillId1"
//     (companion dict cleaned regardless of live-entry retention)
```

### 7-Scan Checklist (SCAN-01 through SCAN-07)

| Scan | Check | Command | Expected Result |
|------|-------|---------|----------------|
| SCAN-01 | lock() grep | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in EvictDedup and OnOrderUpdate call site |
| SCAN-02 | async void grep | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` | 0 matches for new or modified code (NT8 event handlers are pre-existing and exempt) |
| SCAN-03 | CYC check | Manual review of EvictDedup decision points | EvictDedup CYC=6 <= 8. PASS. (Baseline was CYC=5 pre-DR-5; DR-5 adds +1 for else-if fallback.) |
| SCAN-04 | ASCII-only | Inspect `[PTT-DR5-EVICT]` and `[PTT-DR5-FALLBACK]` literals | All string literals in EvictDedup are ASCII-only. No Unicode, emoji, or curly quotes. |
| SCAN-05 | xUnit-only | Inspect B199Tests.cs | All 4 T1 tests use `[Fact]` attribute. No `[Test]`, no `[TestMethod]`, no NUnit, no MSTest. |
| SCAN-06 | throw new Exception / DateTime.Now | `grep -n "throw new\|DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 matches in EvictDedup scope. No throw in hot paths. No DateTime.Now. |
| SCAN-07 | PTT sync | `powershell -File .\scripts\ptt-sync.ps1` | 19 linked 0 errors after T1 commit. |

### Acceptance Criteria (T1)

- All 4 [Fact] tests GREEN.
- EvictDedup signature confirmed present with optional `instrKey` param.
- Fallback path confirmed present and correctly guarded (`instrKey != null && ContainsKey`).
- OnOrderUpdate call site confirmed passing `instrument?.FullName + "|" + OrderAction`.
- SCAN-01 through SCAN-07 all PASS.

---

## Ticket 2 — DR-4 `SetEnabled` + DR-8 `PassesDailyCapCheck` Promotion + Cleanup + 6 xUnit Tests

### Spec Requirements Satisfied
- DR-4: `SetEnabled(true)` companion dict clear (`_entryInstrKeyByOrderId.Clear()` + `[PTT-DR4-CLEAR]` log)
- DR-8: `PassesDailyCapCheck` SIM/Playback account name prefix bypass
- DR-6: Remove `[PTT-DISPATCH]`, `[PTT-LANED-ENTRY]`, and verbose `[PTT-LANED-SKIP]` noise logs

### File: `src/PropTraderTools/CopyEngine.cs`

**Step 1 — Verify already-present DR changes (DO NOT re-apply)**:

Confirm present before making any changes:
- `SetEnabled(true)` block (~line 677): `_entryInstrKeyByOrderId.Clear()` present.
- `SetEnabled(true)` block (~line 679): `Output.Process("[PTT-DR4-CLEAR] ...")` present.
- `PassesDailyCapCheck` (~lines 6051-6054): SIM/Playback bypass present:
  ```csharp
  if (acc.Name.StartsWith("Sim", StringComparison.OrdinalIgnoreCase) ||
      acc.Name.StartsWith("Playback", StringComparison.OrdinalIgnoreCase))
      return true;
  ```

**Step 2 — Change `PassesDailyCapCheck` visibility from `private` to `internal`**:

Find the current declaration (~line 6048):
```csharp
private bool PassesDailyCapCheck(Account acc)
```
Change to:
```csharp
internal bool PassesDailyCapCheck(Account acc)
```
This enables direct unit-testing via InternalsVisibleTo (already configured in test project).

**Step 3 — Remove `[PTT-DISPATCH]` log block from `DispatchCopy` (~lines 2735-2741)**:

Remove this block entirely:
```csharp
// DR-6: all gates passed -- confirm dispatch reached and log key context.
NinjaTrader.Code.Output.Process(
    "[PTT-DISPATCH] all gates passed instrKey=" + instrKey
        + " orderId=" + orderId
        + " mode=" + GetCopyMode()
        + " followers=" + rule.FollowerAccounts.Length,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```
After removal, execution falls through directly to the dispatch logic that follows.
The `return false` / dispatch call that follows MUST be preserved unchanged.

**Step 4 — Remove `[PTT-LANED-ENTRY]` log block from `TryLaneDHook` (~lines 2818-2824)**:

Remove this block entirely:
```csharp
// DR-6: entry log -- confirms TryLaneDHook was reached and shows ATM cache state.
NinjaTrader.Code.Output.Process(
    "[PTT-LANED-ENTRY] mode=" + GetCopyMode()
        + " cache='" + _cloneAtmCache + "'"
        + " obj=" + (_cloneAtmObject != null ? "set" : "null")
        + " followers=" + rule.FollowerAccounts.Length,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```
The code that follows (ATM dispatch logic) MUST be preserved unchanged.

**Step 5 — Remove verbose `[PTT-LANED-SKIP] null_acc` log from `TryLaneDDispatchFollower` Branch A (~lines 2884-2889)**:

The branch reads approximately:
```csharp
if (acc == null)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-LANED-SKIP] idx=" + idx + " reason=null_acc",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return false;
}
```
After removal the branch MUST read:
```csharp
if (acc == null) return false;
```
The `return false` MUST be preserved. Only the Output.Process call is removed.

**Step 6 — Remove verbose `[PTT-LANED-SKIP] daily_cap` log from `TryLaneDDispatchFollower` Branch B (~lines 2892-2900)**:

The branch reads approximately:
```csharp
if (!PassesDailyCapCheck(acc))
{
    double pnlDbg = acc.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
    NinjaTrader.Code.Output.Process(
        "[PTT-LANED-SKIP] idx=" + idx + " reason=daily_cap acct=" + acc.Name
            + " pnl=" + pnlDbg + " floor=" + _dailyCapFloor,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return false;
}
```
After removal the branch MUST read:
```csharp
if (!PassesDailyCapCheck(acc)) return false;
```
The `return false` MUST be preserved. The `pnlDbg` local and the Output.Process call are removed.
`_dailyCapFloor` reference in the removed log is not used elsewhere in this branch — safe to remove.

### Method Signatures (for record)

```csharp
// SetEnabled — existing, confirmed present
internal void SetEnabled(bool enabled)
// CopyEngine.cs ~line 659
// CYC = 2 (1 base + 1 if (enabled) branch).
// JS-021: ConcurrentDictionary.Clear() is lock-free atomic. No lock().
// JS-001: No throw. JS-033: Synchronous. JS-081: [PTT-DR4-CLEAR] ASCII-only.

// PassesDailyCapCheck — change visibility private → internal
internal bool PassesDailyCapCheck(Account acc)
// CopyEngine.cs ~line 6048
// CYC = 3 (1 base + 1 SIM/Playback prefix guard + 1 pnl comparison).
// JS-021: acc.Get() is NT8 read — no lock. StartsWith is pure string op — no lock.
// JS-001: No throw. JS-033: Synchronous. JS-081: "Sim"/"Playback" literals ASCII-only.
```

### JS Rule Constraints

| Rule | Applies To | Requirement | Status |
|------|-----------|-------------|--------|
| JS-021 | `SetEnabled` | `ConcurrentDictionary.Clear()` is lock-free — no lock() | MUST PASS |
| JS-021 | `PassesDailyCapCheck` | `acc.Get()` NT8 read op, `StartsWith` pure string — no lock() | MUST PASS |
| JS-001 | `PassesDailyCapCheck` | No throw in SIM bypass path or pnl comparison | MUST PASS |
| JS-033 | Both methods | Synchronous — no async void | MUST PASS |
| JS-066 | `SetEnabled` | CYC=2 <= 8 | MUST PASS |
| JS-066 | `PassesDailyCapCheck` | CYC=3 <= 8 (was CYC=2 before DR-8; +1 for SIM guard branch) | MUST PASS |
| JS-066 | `DispatchCopy` | CYC=6 baseline (unchanged after log removal — no branch change) | MUST PASS |
| JS-066 | `TryLaneDHook` | CYC=6 baseline (unchanged after log removal — no branch change) | MUST PASS |
| JS-066 | `TryLaneDDispatchFollower` | CYC=4 baseline (unchanged after log removal — no branch change) | MUST PASS |
| JS-077 | Log removals | Removals are noise-reduction per spec mandate, not dead code deletion | MUST PASS |
| JS-081 | `[PTT-DR4-CLEAR]` | ASCII-only | MUST PASS |

### File: `src/PropTraderTools/Tests/B199Tests.cs`

**Engineer action: APPEND 6 [Fact] tests to B199Tests.cs (file created by T1).**

### xUnit [Fact] Tests — T2 (6 facts)

**Test 5: T2_DR4_01**
```csharp
[Fact]
public void T2_DR4_01_SetEnabled_True_ClearsBothDicts()
// Spec: DR-4 — SetEnabled(true) must flush both _liveEntryInstruments AND
//       _entryInstrKeyByOrderId atomically on Copy ON toggle
// Arrange:
//   - Pre-populate _liveEntryInstruments with 2 entries
//   - Pre-populate _entryInstrKeyByOrderId with 2 entries
//   - Confirm both dicts: Count == 2 before act
// Act:
//   engine.SetEnabled(true)
// Assert:
//   - _liveEntryInstruments.Count == 0
//   - _entryInstrKeyByOrderId.Count == 0
```

**Test 6: T2_DR4_02**
```csharp
[Fact]
public void T2_DR4_02_SetEnabled_False_DoesNotClearDicts()
// Spec: DR-4 — Clear only fires on SetEnabled(true). SetEnabled(false) must not clear.
// Arrange:
//   - Pre-populate _liveEntryInstruments with 1 entry
//   - Pre-populate _entryInstrKeyByOrderId with 1 entry
// Act:
//   engine.SetEnabled(false)
// Assert:
//   - _liveEntryInstruments.Count == 1 (unchanged)
//   - _entryInstrKeyByOrderId.Count == 1 (unchanged)
```

**Test 7: T2_DR8_01**
```csharp
[Fact]
public void T2_DR8_01_PassesDailyCapCheck_SimAccount_ReturnsTrueWithoutPnlRead()
// Spec: DR-8 — SIM accounts bypass the P&L floor check entirely.
//       SIM accounts were returning P&L <= -500 on NT8 SIM — the cap was silently
//       blocking all SIM followers. This bypass is the correct fix.
// Arrange:
//   - Stub Account with Name = "Sim101"
// Act:
//   bool result = engine.PassesDailyCapCheck(simAccount)
// Assert:
//   - result == true
```

**Test 8: T2_DR8_02**
```csharp
[Fact]
public void T2_DR8_02_PassesDailyCapCheck_PlaybackAccount_ReturnsTrueWithoutPnlRead()
// Spec: DR-8 — Playback accounts also bypass the P&L floor check.
// Arrange:
//   - Stub Account with Name = "Playback1"
// Act:
//   bool result = engine.PassesDailyCapCheck(playbackAccount)
// Assert:
//   - result == true
```

**Test 9: T2_DR8_03**
```csharp
[Fact]
public void T2_DR8_03_PassesDailyCapCheck_LiveAccount_AboveFloor_ReturnsTrue()
// Spec: DR-8 — Live accounts still use the P&L floor check.
//       P&L above floor = PASS (allow dispatch).
// Arrange:
//   - Stub Account with Name = "LiveAcct"
//   - Stub Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar) = 0.0
//   - _dailyCapFloor = -500.0 (default)
// Act:
//   bool result = engine.PassesDailyCapCheck(liveAccount)
// Assert:
//   - result == true  (pnl 0.0 > floor -500.0)
```

**Test 10: T2_DR8_04**
```csharp
[Fact]
public void T2_DR8_04_PassesDailyCapCheck_LiveAccount_AtOrBelowFloor_ReturnsFalse()
// Spec: DR-8 — Live accounts at or below the floor are blocked.
// Arrange:
//   - Stub Account with Name = "LiveAcct"
//   - Stub Account.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar) = -600.0
//   - _dailyCapFloor = -500.0
// Act:
//   bool result = engine.PassesDailyCapCheck(liveAccount)
// Assert:
//   - result == false  (pnl -600.0 <= floor -500.0)
```

### 7-Scan Checklist (SCAN-01 through SCAN-07)

| Scan | Check | Command | Expected Result |
|------|-------|---------|----------------|
| SCAN-01 | lock() grep | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in SetEnabled, PassesDailyCapCheck, DispatchCopy, TryLaneDHook, TryLaneDDispatchFollower |
| SCAN-02 | async void grep | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` | 0 matches for new or modified code |
| SCAN-03 | CYC check | Manual review of all modified methods | SetEnabled=2, PassesDailyCapCheck=3, DispatchCopy=6 (unchanged — log removal adds 0 branches), TryLaneDHook=6 (unchanged), TryLaneDDispatchFollower=4 (unchanged). All <= 8. PASS. |
| SCAN-04 | ASCII-only | Inspect string literals in modified scope | `[PTT-DR4-CLEAR]` is ASCII. Removed logs leave no residue. No non-ASCII added. PASS. |
| SCAN-05 | xUnit-only | Inspect B199Tests.cs T2 tests | All 6 T2 tests use `[Fact]` only. No `[Theory]`, no NUnit, no MSTest. |
| SCAN-06 | throw new Exception / DateTime.Now | `grep -n "throw new\|DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 matches in modified scope. No throw in hot paths. No DateTime.Now. |
| SCAN-07 | PTT sync | `powershell -File .\scripts\ptt-sync.ps1` | 19 linked 0 errors after T2 commit. |

### Acceptance Criteria (T2)

- All 6 [Fact] tests GREEN.
- `[PTT-DISPATCH]` log block removed from `DispatchCopy`.
- `[PTT-LANED-ENTRY]` log block removed from `TryLaneDHook`.
- `[PTT-LANED-SKIP] null_acc` Output.Process call removed from `TryLaneDDispatchFollower` Branch A (return false preserved).
- `[PTT-LANED-SKIP] daily_cap pnl=` Output.Process call and pnlDbg local removed from `TryLaneDDispatchFollower` Branch B (return false preserved).
- `PassesDailyCapCheck` visibility changed from `private` to `internal`.
- SCAN-01 through SCAN-07 all PASS.

---

## Ticket 3 — DW-B198-CHECKREV-CYC-01: `CheckAtmTemplatesForReverse` CYC Extraction

### Spec Requirements Satisfied
- DW-B198-CHECKREV-CYC-01: `CheckAtmTemplatesForReverse(string)` CYC=11 exceeds JS-066 ceiling of 8. Extract qty-parse block to reduce to CYC=8 (at ceiling, PASS).

### File: `src/PropTraderTools/CopyEngine.cs`

**Current state of `CheckAtmTemplatesForReverse(string)` (~line 1971)**:
- CYC = 11 (B198 Ph5 measured; Lizard CCN=12)
- Root cause: T2b added second try/catch block (+1 McCabe) plus compound if with two `&&` operators (+3 strict McCabe)

**Step 1 — Remove the qty-parse block from `CheckAtmTemplatesForReverse(string)`**:

Locate and remove lines ~2010-2024 (the T2b H2 qty-parse block):
```csharp
// REMOVE this entire block from CheckAtmTemplatesForReverse(string):
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

**Step 2 — Insert call at same location**:

Replace the removed block with:
```csharp
// T3 -- DW-B198-CHECKREV-CYC-01: delegate to helper; TryAdd at call site.
if (TryParseAtmTemplateGroupQty(file, out int qty))
    _atmTemplateGroupQty.TryAdd(name, qty);
```

**Step 3 — Add new `TryParseAtmTemplateGroupQty` method**:

Add the following method immediately after the closing brace of `CheckAtmTemplatesForReverse(string)`:
```csharp
// T3 -- DW-B198-CHECKREV-CYC-01: extracted qty-parse helper.
// CYC = 3 (1 base + 1 catch + 1 compound if-guard).
// JS-021: no lock; caller handles ConcurrentDictionary.TryAdd at call site.
// JS-001: catch block silences exception -- no rethrow, no throw.
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

**CYC arithmetic post-extraction** (for SCAN-03 record):
- `CheckAtmTemplatesForReverse` before: CYC=11
- Removed: second try/catch (+1), compound if with two `&&` operators (+3) = -4
- Added: `if (TryParseAtmTemplateGroupQty(file, out int qty))` at call site = +1
- Net: 11 - 4 + 1 = **CYC=8** (at JS-066 ceiling, not over it — PASS)
- `TryParseAtmTemplateGroupQty` CYC: base(1) + catch(1) + compound if-guard(1 Lizard) = **CYC=3**

### Method Signatures

```csharp
// CheckAtmTemplatesForReverse — existing, CYC reduced by extraction
internal void CheckAtmTemplatesForReverse(string atmDir)
// CopyEngine.cs ~line 1971
// CYC = 8 after extraction (down from 11). At JS-066 ceiling. PASS.
// Note: CYC=8 not CYC=7 as estimated in orchestrator prompt. Unavoidable: the call-site
// if (TryParseAtmTemplateGroupQty(file, out int qty)) adds +1 branch. See plan CYC NOTE.
// The binding constraint is JS-066 (CYC <= 8). CYC=8 PASSES.

// TryParseAtmTemplateGroupQty — NEW method, internal for testability
internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)
// CYC = 3 (1 base + 1 catch + 1 compound if-guard).
// JS-021: no lock; ConcurrentDictionary.TryAdd is called at the call site, not here.
// JS-001: catch silences exception — no throw, no rethrow.
// JS-081: No new string literals; existing XPath "//Quantity" is ASCII.
```

### JS Rule Constraints

| Rule | Applies To | Requirement | Status |
|------|-----------|-------------|--------|
| JS-021 | `TryParseAtmTemplateGroupQty` | No lock(); `_atmTemplateGroupQty.TryAdd` is called at the call site (caller handles ConcurrentDictionary op) | MUST PASS |
| JS-001 | `TryParseAtmTemplateGroupQty` | catch block silences — no throw, no rethrow | MUST PASS |
| JS-033 | `TryParseAtmTemplateGroupQty` | Synchronous — no async void | MUST PASS |
| JS-066 | `CheckAtmTemplatesForReverse` | CYC=8 <= 8 (at ceiling — PASS; arithmetic: 11 - 4 + 1 = 8) | MUST PASS |
| JS-066 | `TryParseAtmTemplateGroupQty` | CYC=3 <= 8 | MUST PASS |
| JS-081 | `TryParseAtmTemplateGroupQty` | XPath "//Quantity" is ASCII; comment strings are ASCII | MUST PASS |

### File: `src/PropTraderTools/Tests/B199Tests.cs`

**Engineer action: APPEND 3 [Fact] tests to B199Tests.cs.**

### xUnit [Fact] Tests — T3 (3 facts)

**Test 11: T3_CHECKREV_01**
```csharp
[Fact]
public void T3_CHECKREV_01_TryParseAtmTemplateGroupQty_ValidXmlWithQuantity_ReturnsTrueQty5()
// Spec: DW-B198-CHECKREV-CYC-01 — valid XML with Quantity node returns true + qty
// Arrange:
//   - Create a temp file containing:
//     "<AtmStrategy><Quantity>5</Quantity></AtmStrategy>"
//   - tempFile = Path.GetTempFileName() + ".xml"  (or equivalent temp path)
// Act:
//   bool result = engine.TryParseAtmTemplateGroupQty(tempFile, out int qty)
// Assert:
//   - result == true
//   - qty == 5
// Cleanup: delete tempFile in test teardown
```

**Test 12: T3_CHECKREV_02**
```csharp
[Fact]
public void T3_CHECKREV_02_TryParseAtmTemplateGroupQty_XmlWithNoQuantityNode_ReturnsFalse()
// Spec: DW-B198-CHECKREV-CYC-01 — XML missing Quantity node returns false + default qty
// Arrange:
//   - Create a temp file containing:
//     "<AtmStrategy></AtmStrategy>"
// Act:
//   bool result = engine.TryParseAtmTemplateGroupQty(tempFile, out int qty)
// Assert:
//   - result == false
//   - qty == 0  (out param default on false return)
// Cleanup: delete tempFile in test teardown
```

**Test 13: T3_CHECKREV_03**
```csharp
[Fact]
public void T3_CHECKREV_03_TryParseAtmTemplateGroupQty_InvalidFilePath_ReturnsFalseNoThrow()
// Spec: DW-B198-CHECKREV-CYC-01 — invalid file path must be caught gracefully; no throw escapes
// Arrange:
//   - filePath = @"C:\nonexistent\fake.xml"
// Act:
//   bool result = engine.TryParseAtmTemplateGroupQty(@"C:\nonexistent\fake.xml", out int qty)
//   (no exception must escape — catch block absorbs it per JS-001)
// Assert:
//   - result == false
//   - does NOT throw (test itself must not require Assert.Throws wrapping — method is safe)
```

### 7-Scan Checklist (SCAN-01 through SCAN-07)

| Scan | Check | Command | Expected Result |
|------|-------|---------|----------------|
| SCAN-01 | lock() grep | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in TryParseAtmTemplateGroupQty and CheckAtmTemplatesForReverse call site |
| SCAN-02 | async void grep | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` | 0 matches for new or modified code |
| SCAN-03 | CYC check | Manual review of modified methods | CheckAtmTemplatesForReverse CYC=8 (at ceiling, PASS; arithmetic: 11-4+1=8). TryParseAtmTemplateGroupQty CYC=3. Both <= 8. NOTE: orchestrator prompt estimated <=7 for CheckAtmTemplatesForReverse; CYC=8 is unavoidable with bool/out-param signature; JS-066 ceiling is 8, not 7. PASS. |
| SCAN-04 | ASCII-only | Inspect string literals in TryParseAtmTemplateGroupQty | No new non-ASCII literals. XPath "//Quantity" is ASCII. Comment strings are ASCII. PASS. |
| SCAN-05 | xUnit-only | Inspect B199Tests.cs T3 tests | All 3 T3 tests use `[Fact]` only. Temp files cleaned up in test teardown. No `[Theory]`, no NUnit, no MSTest. |
| SCAN-06 | throw new Exception / DateTime.Now | `grep -n "throw new\|DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | 0 matches in TryParseAtmTemplateGroupQty. Catch block absorbs, no rethrow. |
| SCAN-07 | PTT sync | `powershell -File .\scripts\ptt-sync.ps1` | 19 linked 0 errors after T3 commit. |

### Acceptance Criteria (T3)

- All 3 [Fact] tests GREEN.
- `TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)` method added as `internal bool` immediately after `CheckAtmTemplatesForReverse(string)`.
- qty-parse try/catch block removed from `CheckAtmTemplatesForReverse(string)`.
- Call site `if (TryParseAtmTemplateGroupQty(file, out int qty)) _atmTemplateGroupQty.TryAdd(name, qty);` inserted at same location.
- `CheckAtmTemplatesForReverse` CYC verified at 8 (at ceiling, PASS).
- SCAN-01 through SCAN-07 all PASS.
- DW-B198-CHECKREV-CYC-01: CLOSED.

---

## Ticket 4 — DW-B198-QTY-SIM-01: SIM Evidence Document

### Spec Requirements Satisfied
- DW-B198-QTY-SIM-01: Record T2a SIM evidence confirming H1 or H2 hypothesis. Close the item.

### Documentation Only — No Code Changes

**No .cs edits. No test additions. No changes to B199Tests.cs.**

### File: `docs/brain/B198/ticket-2a-sim-evidence.md` (NEW)

**Engineer action: CREATE this file with the exact content below.**

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

### Verification Gate

The file `docs/brain/B198/ticket-2a-sim-evidence.md` MUST:
1. Exist at the specified path.
2. Contain the string `H1 CONFIRMED`.
3. Contain the string `DW-B198-QTY-SIM-01: CLOSED.`

All three conditions must pass for T4 VERIFY_PASS.

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Documentation-only ticket. All code scans are N/A.

| Scan | Check | Expected Result |
|------|-------|----------------|
| SCAN-01 | lock() grep | N/A — no code change |
| SCAN-02 | async void grep | N/A — no code change |
| SCAN-03 | CYC check | N/A — no code change |
| SCAN-04 | ASCII-only | N/A — documentation file; verify no non-ASCII characters in the markdown |
| SCAN-05 | xUnit-only | N/A — no tests |
| SCAN-06 | throw new Exception / DateTime.Now | N/A — no code change |
| SCAN-07 | PTT sync | N/A — documentation only; ptt-sync.ps1 not required for T4 |

**Artifact check (replaces code scans)**:
- `Test-Path docs/brain/B198/ticket-2a-sim-evidence.md` → True
- `Select-String "H1 CONFIRMED" docs/brain/B198/ticket-2a-sim-evidence.md` → match found
- `Select-String "DW-B198-QTY-SIM-01: CLOSED" docs/brain/B198/ticket-2a-sim-evidence.md` → match found

### Acceptance Criteria (T4)

- `docs/brain/B198/ticket-2a-sim-evidence.md` exists.
- File contains "H1 CONFIRMED".
- File contains "DW-B198-QTY-SIM-01: CLOSED."
- DW-B198-QTY-SIM-01: CLOSED.

---

## Summary

| Ticket | Spec IDs | File(s) Modified | Tests | DW Items Closed |
|--------|----------|-----------------|-------|----------------|
| T1 | DR-5, DW-B142-MGC-02 | CopyEngine.cs (verify only), B199Tests.cs (new) | 4 [Fact] | — |
| T2 | DR-4, DR-6, DR-8 | CopyEngine.cs (log removal + visibility change), B199Tests.cs (append) | 6 [Fact] | — |
| T3 | DW-B198-CHECKREV-CYC-01 | CopyEngine.cs (extract method), B199Tests.cs (append) | 3 [Fact] | DW-B198-CHECKREV-CYC-01 |
| T4 | DW-B198-QTY-SIM-01 | docs/brain/B198/ticket-2a-sim-evidence.md (new doc) | 0 | DW-B198-QTY-SIM-01 |

**Total tests**: 13 [Fact] in B199Tests.cs
**All scans**: lock=0, async void=0, CYC <= 8 (all modified methods), ASCII-only, xUnit-only
**PTT sync**: `powershell -File .\scripts\ptt-sync.ps1` → 19 linked 0 errors after each code ticket commit (T1, T2, T3)
**Guardian**: `powershell -File .\scripts\verify_critical_symbols.ps1` → pre-existing DW-B198-GUARDIAN-01 failure is documented carry-forward; NOT a B199 blocker

---

*Written by: ptt-architect*
*Epic: B199*
*Phase: 3 (Ticket Generation)*
*Source plan: docs/brain/B199/02-architecture-plan.md (Revision 2 — REVIEW_PASS)*
*Next: ptt-ticket-reviewer → docs/brain/B199/04-ticket-review.md*
