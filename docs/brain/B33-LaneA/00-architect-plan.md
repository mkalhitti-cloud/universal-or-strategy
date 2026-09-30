# B33-LaneA Architecture Plan

**Block**: B33-LaneA
**Status**: DIAGNOSTIC_REQUIRED
**Date**: 2026-07-20
**Focus**: DW-B32-10 — BE button does not move ATM-owned stops (Stop1/Stop2)

---

## 1. Defect Summary

### DW-B32-10 | P0 — BE fires but ATM-owned stops do not move

**Symptom**: BE button runs `MoveStopToBreakEven`, `acc.Change()` is called, no exception,
but `order.StopPrice` remains at old value. Stop on Orders tab unchanged.

**Live test evidence (2026-07-20)**:
```
Sim101: BE Change() OK -- stop=7520.5   ← old value, should be 7514.5
Sim102: BE Change() OK -- stop=7508.25  ← old value, should be 7514.25
```
No "ATM BE TRIGGER SET" line anywhere in Output tab.

**Root cause chain**:
1. `TriggerAtmBreakEven` (Path A): `acc.ServerStrategies == null` → early return at line 1397, zero output.
2. `MoveStopToBreakEven` inner loop (Path B): `Stop\d+` filter was removed in DW-B32-09. Stop1/Stop2
   pass all loop filters (`isStopLeg=True`, `alreadyAtBe=False`).
3. `acc.Change(new Order[]{order})` called on ATM-owned Stop1/Stop2 → NT8 ATM engine silently
   rejects (NT8-046). No exception. `order.StopPrice` reverts to old value immediately.

---

## 2. Prior Art

| Item | Finding |
|------|---------|
| DW-B32-07 | Confirmed: `acc.Change()` on ATM slot orders rejected. `Stop\d+` filter added to loop. |
| DW-B32-09 | Removed `Stop\d+` filter, added `TriggerAtmBreakEven` via `ServerBracket`. |
| NT8-046 | Confirmed: `acc.Change(new Order[]{order})` silently rejected on ATM-owned stops. |
| NT8-047 | ATM slot order pattern: `Name.StartsWith("Stop") && char.IsDigit(Name[4])`. |
| B33 architecture | `Stop\d+` filter STAYS per locked decision. PTT cannot move ATM-owned stops. |

---

## 3. Diagnostic Required Before Fix (MANDATORY)

The correct fix depends on what `acc.ServerStrategies` contains.
**Director must run diagnostic D-1 → D-4 before any code fix.**

### D-1: Instrument `TriggerAtmBreakEven`

Replace line 1397 in `CopyEngine.cs` (SPEC ONLY — no src edit until pipeline):
```csharp
// BEFORE:
if (acc.ServerStrategies == null) return;

// AFTER (diagnostic):
if (acc.ServerStrategies == null)
{
    NinjaTrader.Code.Output.Process(
        acc.Name + ": TriggerAtmBE -- ServerStrategies NULL",
        PrintTo.OutputTab1);
    return;
}
NinjaTrader.Code.Output.Process(
    acc.Name + ": TriggerAtmBE -- ServerStrategies count=" + acc.ServerStrategies.Count,
    PrintTo.OutputTab1);
foreach (var strat in acc.ServerStrategies)
{
    if (strat == null) continue;
    NinjaTrader.Code.Output.Process(
        acc.Name + ": TriggerAtmBE strat=" + strat.StrategyId
        + " orders=" + (strat.Orders != null ? strat.Orders.Count.ToString() : "null"),
        PrintTo.OutputTab1);
```

Also add `order.FromEntrySignal` to the CANDIDATE log in the main loop:
```csharp
NinjaTrader.Code.Output.Process(
    acc.Name + ": CANDIDATE order=" + order.Name
    + " fromEntry=" + (order.FromEntrySignal ?? "null")   // ADD THIS
    + " type=" + order.OrderType
    + ...
```

### D-2: F5 + Click BE, read Output tab

Expected outputs and their meaning:

| Output | Meaning |
|--------|---------|
| `TriggerAtmBE -- ServerStrategies NULL` | ServerStrategies API unavailable for Sim |
| `TriggerAtmBE -- ServerStrategies count=0` | API exists but empty for Sim accounts |
| `TriggerAtmBE strat=X orders=N` | API works — check instrument match logic |
| `CANDIDATE order=Stop1 fromEntry=null` | ATM-owned (correct — should be filtered) |
| `CANDIDATE order=PTT-BE-Stop fromEntry=...` | PTT-created — Change() can work on this |

### D-3: If ServerStrategies null/empty

Investigate alternative NT8 APIs. Reflection scan (can be done in NT8 script window):
```csharp
// Paste into NinjaTrader script editor:
var acc = Account.All.FirstOrDefault(a => a.Name == "Sim101");
var methods = acc.GetType().GetMethods().Where(m => m.Name.ToLower().Contains("atm")).ToList();
// Also check:
var props = acc.GetType().GetProperties()
    .Where(p => p.Name.ToLower().Contains("strat") || p.Name.ToLower().Contains("atm"))
    .Select(p => p.Name + "=" + p.GetValue(acc)?.ToString()).ToList();
```

### D-4: Director reports Output tab result

Paste Output tab lines into new message. Architect will determine fix path based on evidence.

---

## 4. Fix Options (pending diagnostic)

### Option A — If ServerStrategies works on live account (not Sim)

The API is correct. The issue is Sim-only. On live account:
- TriggerAtmBreakEven sets `BreakEvenTrigger=0 BreakEvenPlus=buf` → ATM engine moves stop on next tick
- Test on live simulator (SimMNQ etc.) may behave differently from pure Sim101/Sim102

**Fix**: No code change needed. Test on live simulator to confirm Path A works.

### Option B — If ServerStrategies null on all accounts

The API doesn't exist in this NT8 build. Need alternative.

**Option B1**: Use `acc.Strategies` (different from ServerStrategies — may be `AtmStrategy[]`)
  - Investigate `NinjaTrader.Cbi.Account.Strategies` or `Account.AtmStrategies`

**Option B2**: Accept architectural constraint (ATM manages its own BE)
  - Restore `Stop\d+` filter in loop (Path B skips ATM-owned stops)
  - Remove `TriggerAtmBreakEven` call (Path A removed — doesn't work)
  - PTT BE only moves PTT-created follower stops
  - ATM manages its own BE via template settings (Director must set BE in ATM template)
  - Document as architectural constraint in spec

### Option C — Restore DW-B32-07 Stop\d+ filter now (minimal fix)

While diagnostic runs, at minimum restore the safety filter:
```csharp
// In MoveStopToBreakEven inner loop, after isStop assignment:
if (IsAtmSlotName(order.Name))  // DW-B32-10: skip ATM-owned stops (NT8-046 confirmed)
    continue;
```

This stops the loop from calling `acc.Change()` on Stop1/Stop2 (which fails silently anyway).
PTT-created follower stops with non-ATM names will still be moved.

---

## 5. Tickets (pending diagnostic result)

Tickets will be finalized after D-4 (Director reports Output tab result).

**Provisional Ticket 1 (T-B33-T1)**:
- Add diagnostic instrumentation to `TriggerAtmBreakEven` and CANDIDATE loop
- F5 + live test → collect Output tab
- Report to spec

**Provisional Ticket 2 (T-B33-T2)** (after D-4):
- Implement correct ATM BE path based on diagnostic evidence
- Restore `IsAtmSlotName` filter in loop (Option C always applies regardless)
- Update CYC annotations
- Add [Fact] tests

---

## 6. NT8 Rules Gate

| Rule | Status |
|------|--------|
| NT8-046 | ATM slot orders: Change() rejected. Stop\d+ filter must stay in Path B. |
| NT8-047 | ATM slot name pattern: `Name.StartsWith("Stop") && char.IsDigit(Name[4])` |
| NT8-018 | No lock() |
| NT8-019 | No async void |
| NT8-044 | `using System;` confirmed present |
| JS-021 | No lock() |
| JS-002 | No new return null |

---

## 7. [Fact] Impact

| State | Count |
|-------|-------|
| B32-LaneA FINAL_PASS baseline | 150 |
| B33-LaneA target | 152 (+T_B33_01 filter test, +T_B33_02 ATM slot skip) |
