# B34-LaneA Session Handoff
# Status: READY FOR PIPELINE — DW-B33-04 spec complete, awaiting ptt-orchestrator execution
# Date: 2026-07-22
# Authored by: ptt-orchestrator (Copier Architect Plan mode)

---

## 1. What This Block Does

**DW-B33-04 — ATM Bracket Replace on BE**

When the BE button is pressed, instead of leaving two competing stops live (ATM Stop1 + PTT-BE-Stop),
the engine now:
1. Snapshots all Working ATM Target orders (prices + quantities) before cancelling anything
2. Cancels the entire ATM bracket (stop + targets) via CancelStaleBrackets
3. Submits PTT-BE-Stop at entry price with an OCO group ID
4. Resubmits all snapshotted targets as static PTT-owned Limit orders with the same OCO group ID

Result: one clean stop, original target prices preserved, no reverse position risk.

---

## 2. Source State at Handoff

| File | Line | Current State |
|------|------|---------------|
| `CopyEngine.cs` | 41 | `"PTT-COPIER B33 | 1b-dict-BE | 2026-07-21"` |
| `CopyEngine.cs` | 1560 | `SubmitBeStop(Account, Instrument, double)` — 3-param, B33 1b version |
| `CopyEngine.cs` | 1631 | `CancelStaleBrackets(Account, Instrument)` — 2-param, excludes `"PTT-BE-Stop"` only |
| `CopyEngine.cs` | ~1172 | `IsAtmSlotName(string)` — detects Stop1..N + Target1..N |
| `CopyEngine.cs` | ~1186 | `IsAtmBracketActive(Account, Instrument)` |
| `CopyEngineTests.cs` | ~2770 | Last B33 test is `PendingBeStop_FieldExists_And_IsConcurrentDictionary` |

Hard links: `powershell -File scripts\verify_links.ps1` MUST PASS before any .cs edit.

---

## 3. Full Diff Plan (9 Changes)

### C1 — New `IsAtmTargetName(string)` helper (near line 1184, after IsAtmSlotName closes)
```csharp
// B34 DW-B33-04: target-only slot detection. IsAtmSlotName covers both stop+target;
// IsAtmTargetName is target-only -- used by SnapshotTargets to exclude stop orders.
// CYC=2: null/short guard(1), Target prefix + digit check(2).
// internal static -- CopyEngineTests.cs calls directly; no NT8 runtime deps.
internal static bool IsAtmTargetName(string name)
{
    if (string.IsNullOrEmpty(name) || name.Length < 7) return false;  // (1)
    return name.StartsWith("Target", StringComparison.Ordinal)
           && char.IsDigit(name[6]);                                   // (2)
}
```

### C2 — New `SnapshotTargets(Account, Instrument)` method (after CancelStaleBrackets closes, ~line 1653)
```csharp
// B34 DW-B33-04: snapshot ATM Target orders before cancelling ATM bracket.
// Returns list of (LimitPrice, Quantity, OrderAction) for each Working/Accepted ATM target.
// CYC=3: null guard(1), foreach(2), IsAtmTargetName filter(3).
// JS-021: no lock. JS-002: returns empty list, never null. NT8-006: System.Linq present.
private List<(double Price, int Qty, OrderAction Action)> SnapshotTargets(
    Account leaderAcc, Instrument instr)
{
    var result = new List<(double, int, OrderAction)>();
    if (leaderAcc == null || instr == null) return result;                         // (1)
    foreach (var o in leaderAcc.Orders.ToList())                                   // (2)
    {
        if (o.Instrument?.FullName != instr.FullName) continue;
        if (o.OrderState != OrderState.Working
            && o.OrderState != OrderState.Accepted) continue;
        if (!IsAtmTargetName(o.Name)) continue;                                    // (3)
        result.Add((o.LimitPrice, o.Quantity, o.OrderAction));
        NinjaTrader.Code.Output.Process(
            "[BE] Snapshot target: " + o.Name
            + " " + o.OrderAction + " " + o.Quantity
            + " @ " + o.LimitPrice.ToString("F2"),
            PrintTo.OutputTab1);
    }
    return result;
}
```

### C3 — Update `CancelStaleBrackets` signature + filter (~line 1631)
```csharp
// BEFORE signature:
private void CancelStaleBrackets(Account leaderAcc, Instrument instr)

// AFTER signature:
// B34 DW-B33-04: cancelPttBe=false at submit time (protect own PTT-BE orders);
// cancelPttBe=true at flat event (clean up all PTT-BE orders when position gone).
private void CancelStaleBrackets(Account leaderAcc, Instrument instr, bool cancelPttBe = false)

// BEFORE filter line (~1638):
&& o.Name != "PTT-BE-Stop")

// AFTER filter line:
&& (cancelPttBe || !o.Name.StartsWith("PTT-BE-")))
```

### C4 — Update TryFirePositionState call site (~line 745)
```csharp
// BEFORE:
CancelStaleBrackets(e.Order.Account, e.Order.Instrument);

// AFTER:
CancelStaleBrackets(e.Order.Account, e.Order.Instrument, cancelPttBe: true);
```

### C5 — Modify `SubmitBeStop` body (after existing guards, before the try block ~line 1577)
Insert before the existing `try { var beStop = ...` line:
```csharp
// B34 DW-B33-04: Step 1 - snapshot ATM targets BEFORE cancel
var beTargets = SnapshotTargets(leaderAcc, instr);
// Step 2 - OCO group ID shared by stop + all targets
string beOcoId = "PTT-BE-"
    + (leaderAcc.Name.Length >= 4 ? leaderAcc.Name.Substring(0, 4) : leaderAcc.Name)
    + "-" + (DateTime.Now.Ticks % 10000L).ToString();
// Step 3 - cancel ATM bracket (excludes PTT-BE-* at this call)
CancelStaleBrackets(leaderAcc, instr);
```

Inside the existing try block, change the `CreateOrder` call arg8 from `""` to `beOcoId`:
```csharp
// BEFORE arg8:
"", "PTT-BE-Stop", DateTime.MaxValue,

// AFTER arg8:
beOcoId, "PTT-BE-Stop", DateTime.MaxValue,
```

After `leaderAcc.Submit(new[] { beStop });`, add target loop:
```csharp
// Step 5-6: resubmit ATM targets as PTT-BE-Target-N (static Limit orders)
// NT8-007: Limit order -- arg6=limitPrice, arg7=0 (no stop price)
// NT8-014: signal name starts "PTT-". NT8-013: DateTime.MaxValue for GTC.
// V12-pattern: null-guard each CreateOrder; skip on null (stop still protects position).
for (int i = 0; i < beTargets.Count; i++)
{
    var t = beTargets[i];
    var tOrd = leaderAcc.CreateOrder(
        instr, t.Action, OrderType.Limit, OrderEntry.Manual,
        TimeInForce.Gtc, t.Qty,
        t.Price,  // arg6: limitPrice
        0,        // arg7: stopPrice = 0 for Limit orders
        beOcoId, "PTT-BE-Target-" + (i + 1), DateTime.MaxValue,
        (NinjaTrader.Cbi.CustomOrder)null);
    if (tOrd != null)
        leaderAcc.Submit(new[] { tOrd });
    else
        NinjaTrader.Code.Output.Process(
            "[BE] Target-" + (i + 1) + " CreateOrder null -- skip (stop still live)",
            PrintTo.OutputTab1);
}
NinjaTrader.Code.Output.Process(
    "[BE] bracket-replace: 1 stop + " + beTargets.Count + " targets submitted",
    PrintTo.OutputTab1);
```

### C6 — Build tag (line 41)
```csharp
// BEFORE:
internal const string Tag = "PTT-COPIER B33 | 1b-dict-BE | 2026-07-21";

// AFTER:
internal const string Tag = "PTT-COPIER B34 | bracket-replace-BE | 2026-07-22";
```

### T1–T4 — New tests in CopyEngineTests.cs (append to B33 test block, after line ~2769)
```csharp
// B34 DW-B33-04: IsAtmTargetName helper exists
[Fact]
public void IsAtmTargetName_MethodExists_And_HasCorrectSignature()
{
    var mi = typeof(CopyEngine).GetMethod(
        "IsAtmTargetName",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    Assert.NotNull(mi);
    var parms = mi.GetParameters();
    Assert.Equal(1, parms.Length);
    Assert.Equal(typeof(string), parms[0].ParameterType);
    Assert.Equal(typeof(bool), mi.ReturnType);
}

// B34 DW-B33-04: IsAtmTargetName identifies Target1..Target9
[Fact]
public void IsAtmTargetName_IdentifiesTarget1ToTarget9()
{
    Assert.True(CopyEngine.IsAtmTargetName("Target1"));
    Assert.True(CopyEngine.IsAtmTargetName("Target9"));
    Assert.False(CopyEngine.IsAtmTargetName("Stop1"));
    Assert.False(CopyEngine.IsAtmTargetName("PTT-BE-Stop"));
    Assert.False(CopyEngine.IsAtmTargetName("Target"));   // no digit suffix
    Assert.False(CopyEngine.IsAtmTargetName(null));
}

// B34 DW-B33-04: SnapshotTargets method exists with correct signature
[Fact]
public void SnapshotTargets_MethodExists_And_HasCorrectSignature()
{
    var mi = typeof(CopyEngine).GetMethod(
        "SnapshotTargets",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    Assert.NotNull(mi);
    var parms = mi.GetParameters();
    Assert.Equal(2, parms.Length);
    Assert.Equal(typeof(NinjaTrader.Cbi.Account),     parms[0].ParameterType);
    Assert.Equal(typeof(NinjaTrader.Cbi.Instrument),   parms[1].ParameterType);
}

// B34 DW-B33-04: CancelStaleBrackets has optional bool cancelPttBe parameter
[Fact]
public void CancelStaleBrackets_HasCancelPttBeBoolParameter()
{
    var mi = typeof(CopyEngine).GetMethod(
        "CancelStaleBrackets",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    Assert.NotNull(mi);
    var parms = mi.GetParameters();
    Assert.Equal(3, parms.Length);
    Assert.Equal(typeof(bool), parms[2].ParameterType);
    Assert.True(parms[2].HasDefaultValue);
    Assert.Equal(false, parms[2].DefaultValue);
}
```

---

## 4. Jane Street Rules Gate

| Rule | Status |
|------|--------|
| JS-021 lock() ban | PASS — zero lock() in any new/changed region |
| JS-033 async void ban | PASS — none introduced |
| JS-001 throw new in hot path | PASS — none introduced |
| JS-002 return null ban | PASS — SnapshotTargets returns empty list; IsAtmTargetName returns bool |
| NT8-003 volatile | PASS — no new volatile fields |
| NT8-007 CreateOrder arg order | PASS — Limit: arg6=limitPrice, arg7=0. StopMarket arg unchanged from B33. |
| NT8-014 signal name PTT- prefix | PASS — "PTT-BE-Target-N" starts with "PTT-" |
| NT8-046 acc.Change() on ATM | PASS — not used; cancel+resubmit only |

**GATE RESULT: PASS (all pre-verified before implementation)**

---

## 5. Sim Test Unknowns (gate items — verify in test procedure)

| ID | Question | Impact if fails |
|----|----------|-----------------|
| U1 | Does NT8 Add-On `Account.CreateOrder` arg8 OCO work on sim? | Low — CancelStaleBrackets(cancelPttBe:true) cleans up on flat regardless |
| U2 | CancelStaleBrackets dual-role resolved by bool param (C3) | RESOLVED pre-implementation |
| U3 | Limit order arg6=limitPrice, arg7=0 correct? | Medium — test output shows wrong order price if swapped |
| U4 | TimeInForce.Gtc for targets correct? | Low — Day also works on sim; Gtc confirmed as right choice for live |

---

## 6. Impact

| File | Changes | Lines |
|------|---------|-------|
| `CopyEngine.cs` | C1–C6 above | ~56 new + ~8 changed |
| `CopyEngineTests.cs` | T1–T4 above | ~38 new |
| `TradeCopierPanel.cs` | None | 0 |

---

## 7. Open Defect Register

| ID | Description | Status |
|----|-------------|--------|
| DW-B33-04 | Two competing stops after BE — ATM bracket not replaced | IN PROGRESS (B34) |

---

## 8. Ptt-Orchestrator Lane Prompt

```
PTT-COPIER B34 | DW-B33-04 | bracket-replace-BE
Ticket: DW-B33-04 — ATM Bracket Replace on BE
Spec: specs/002-trade-copier-spec.html id="section-dw-b33-04"
Handoff: docs/brain/B34-LaneA/00-session-handoff.md
Source baseline: CopyEngine.cs:41 tag = "PTT-COPIER B33 | 1b-dict-BE | 2026-07-21"
Hard-link gate: powershell -File scripts\verify_links.ps1 MUST PASS
Changes: 6 source changes (C1-C6) + 4 test additions (T1-T4) as documented in handoff Section 3.
Build tag target: "PTT-COPIER B34 | bracket-replace-BE | 2026-07-22"
Test procedure: Section 6 of handoff (9-step sim test).
```
