# B129 Tickets
## Block: B129 — Instrument Row Redesign: Quick2t + QAll2t Buttons
## Phase 3 — Ticket Generation
## Source Plan: docs/brain/B129/02-architecture-plan.md (REVIEW_PASS)
## Reviewer Confirmation: docs/brain/B129/02-plan-review.md (REVIEW_PASS — post-fix cycle)

---

## Spec Requirements Closed by This Ticket

| Req ID | Description |
|--------|-------------|
| B129-REQ-01 | Replace instrument row spinner cluster with 2-button grid ("Quick2t" + "QAll2t") |
| B129-REQ-02 | "Quick2t" fires single-account 2-target bracket exit for current instrument |
| B129-REQ-03 | "QAll2t" fires all-accounts exit via existing PttGlobalQuickExit.Execute() (Option B) |
| B129-REQ-04 | Build2TargetList: 50/50 ceiling-heavy qty split, returns List<(double,int)>, never null |
| B129-REQ-05 | T2qty=0 guard in PttQuickExit.Execute() — skip bracket when qty=0 |
| B129-REQ-06 | Remove all B128 spinner fields/methods; update B128Tests.cs to test Build2TargetList |
| B122-CLOSE | Closes spec section B122 (instrument row button redesign) |

---

## Ticket T1 — Instrument Row Redesign (B129)

**Subtasks**: T1a (TradeCopierPanel.cs), T1b (PttQuickExit.cs), T1c (B128Tests.cs)
**Files touched**:
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/Features/PttQuickExit.cs`
- `src/PropTraderTools/Tests/B128Tests.cs`

**Files NOT touched** (enforced):
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — Option B: zero changes, no diff permitted

---

## Subtask T1a — TradeCopierPanel.cs

### File Path
`src/PropTraderTools/TradeCopierPanel.cs`

---

### Step 1 — Field Changes (lines ~270-274)

**REMOVE** these three field declarations:
```csharp
private Button _instrBeBtn;
private int    _instrQxT1 = 4;
```

**RENAME/REPURPOSE** (edit existing declaration):
```csharp
// BEFORE:
private Button _instrQxBtn;

// AFTER:
private Button _instr2tBtn;
```

**ADD** this new field alongside the above (adjacent to `_instr2tBtn`):
```csharp
private Button _instrQAll2tBtn;
```

**Final field state** (all four instrument-row fields):
```csharp
private System.Windows.UIElement _instrRowPanel;
private Button _instr2tBtn;
private Button _instrQAll2tBtn;
```

---

### Step 2 — Replace BuildInstrRow() (lines ~1354-1408)

**DELETE** the entire existing `BuildInstrRow()` method body and replace with the
Director-confirmed implementation below. The signature changes from the spinner cluster
to a 2-column `UniformGrid`.

**New method — exact implementation (Director-confirmed):**
```csharp
private void BuildInstrRow()
{
    _instrRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
    _instr2tBtn = new Button
    {
        Content = "Quick2t",
        BorderBrush = BrushTeal,
        Foreground = BrushTeal,
        BorderThickness = new Thickness(2),
    };
    _instr2tBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    _instr2tBtn.Click += OnInstr2tClick;
    _instrRowPanel.Children.Add(_instr2tBtn);

    _instrQAll2tBtn = new Button
    {
        Content = "QAll2t",
        BorderBrush = BrushTeal,
        Foreground = BrushTeal,
        BorderThickness = new Thickness(2),
    };
    _instrQAll2tBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    _instrQAll2tBtn.Click += OnInstrQAll2tClick;
    _instrRowPanel.Children.Add(_instrQAll2tBtn);
}
```

**Method signature (final):**
```csharp
private void BuildInstrRow()
```

**JS rules for BuildInstrRow:**
- JS-021 (no lock): no lock() — PASS
- JS-033 (no async void): synchronous void — PASS
- ASCII-only: "Quick2t", "QAll2t", "NTButtonStyle" all ASCII — PASS
- No FontFamily set — PASS
- No hardcoded hex colors (uses named brush `BrushTeal`) — PASS
- CYC=1: sequential construction, no branches

---

### Step 3 — Replace ComputeInstrSplit with Build2TargetList (lines ~1415-1416)

**DELETE** the entire `ComputeInstrSplit(int n)` method.

**ADD** this new method in its place:

**Method signature (final):**
```csharp
internal static System.Collections.Generic.List<(double Price, int Qty)> Build2TargetList(int totalQty)
```

**Exact implementation (Director-confirmed):**
```csharp
internal static System.Collections.Generic.List<(double Price, int Qty)> Build2TargetList(int totalQty)
{
    int t1Qty = (totalQty + 1) / 2;
    int t2Qty = totalQty - t1Qty;
    return new System.Collections.Generic.List<(double, int)> { (0.0, t1Qty), (0.0, t2Qty) };
}
```

**Constraints:**
- Access modifier MUST be `internal static` — required for B128Tests.cs direct call access
- Returns `new List<>` — never null (JS-002 compliant)
- No lock() (JS-021)
- No throw (JS-001)
- CYC=1: no branches

**Notes:**
- `Price = 0.0` is a placeholder; PttQuickExit.Execute() computes actual prices from entry + tick offset
- When totalQty=1: t1Qty=1, t2Qty=0. The guard in T1b prevents T2 submission

---

### Step 4 — Remove OnInstrQxClick, OnInstrQxUp, OnInstrQxDown, OnInstrBeClick

**DELETE** all four methods entirely (lines ~1976-2032):
- `OnInstrQxClick()` (~lines 1976-1995)
- `OnInstrQxUp()` (~lines 1997-2004)
- `OnInstrQxDown()` (~lines 2006-2011)
- `OnInstrBeClick()` (~lines 2018-2032)

No references to these methods must remain in the file after deletion.

---

### Step 5 — Add OnInstr2tClick() (replaces OnInstrQxClick)

**ADD** this method at the location where OnInstrQxClick was removed:

**Method signature (final):**
```csharp
private void OnInstr2tClick(object sender, RoutedEventArgs e)
```

**Exact implementation (Director-confirmed):**
```csharp
private void OnInstr2tClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;                                    // (1)
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();       // (2)
    if (_leaderAccount == null) return;                                 // (3)
    var pos = _leaderAccount.Positions.FirstOrDefault(
        p => p.Instrument?.FullName == _instrument.FullName);
    int qty = pos?.Quantity ?? 1;
    var targets = Build2TargetList(qty);
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-2T] button: " + _leaderAccount.Name
            + " " + _instrument.FullName
            + " qty=" + qty + " T1=" + targets[0].Qty + " T2=" + targets[1].Qty,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets);
}
```

**JS rules for OnInstr2tClick:**
- JS-021 (no lock): no lock() — PASS
- JS-001 (no throw): guards return early, no throw — PASS
- JS-002 (no return null): void method — PASS
- JS-033 (no async void): synchronous void — PASS
- ASCII-only: `[PTT-QX-2T]`, `T1=`, `T2=`, `qty=` all ASCII — PASS
- CYC=4: (1) `_instrument == null`, (2) `_leaderAccount == null` after re-resolve,
          (3) `FirstOrDefault` lambda conditional, (4) `pos?.Quantity ?? 1` null-coalescing

**Log tag constraint (SCAN-05):**
Output.Process string MUST use tag `[PTT-QX-2T]` and keys `T1=` / `T2=` exactly.
No other tag variant (`[PTT-2T-INSTR]`, `t1q=`, `t2q=`) is permitted.

---

### Step 6 — Add OnInstrQAll2tClick() (replaces OnInstrBeClick)

**ADD** this method at the location where OnInstrBeClick was removed:

**Method signature (final):**
```csharp
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
```

**Exact implementation (Director-confirmed):**
```csharp
// OnInstrQAll2tClick: delegates to PttGlobalQuickExit.Execute() which logs
// "[PTT-QX-ALL] GlobalQuickExit fired" internally. CYC=1.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    new PttGlobalQuickExit().Execute();
}
```

**JS rules for OnInstrQAll2tClick:**
- JS-021 (no lock): no lock() — PASS
- JS-001 (no throw): PttGlobalQuickExit.Execute() is void with internal guards — PASS
- JS-033 (no async void): synchronous void — PASS
- CYC=1: straight delegation, no branches

**Notes:**
- Delegates entirely to existing zero-arg Execute() (Option B — see plan Section B)
- No account/instrument resolution in this handler; PttGlobalQuickExit handles Account.All internally
- The log line `[PTT-QX-ALL] GlobalQuickExit fired` is produced INSIDE `PttGlobalQuickExit.Execute()`.
  The handler itself does NOT add any `Output.Process` call. This is intentional and by design.
  SCAN-05 does NOT require a log tag in OnInstrQAll2tClick — it applies to OnInstr2tClick only.
- PttGlobalQuickExit.cs receives ZERO changes in B129

---

### T1a Removal Summary

| Symbol | Type | Action | Source Lines (approx) |
|--------|------|--------|-----------------------|
| `_instrBeBtn` | Field | REMOVE | ~271 |
| `_instrQxT1` | Field | REMOVE | ~272 |
| `_instrQxBtn` | Field | REPURPOSE to `_instr2tBtn` | ~270 |
| `OnInstrQxClick` | Method | REMOVE | ~1976-1995 |
| `OnInstrQxUp` | Method | REMOVE | ~1997-2004 |
| `OnInstrQxDown` | Method | REMOVE | ~2006-2011 |
| `OnInstrBeClick` | Method | REMOVE | ~2018-2032 |
| `ComputeInstrSplit` | Method | REMOVE | ~1415-1416 |

### T1a Addition Summary

| Symbol | Type | Action |
|--------|------|--------|
| `_instrQAll2tBtn` | Field | ADD |
| `BuildInstrRow` | Method | REPLACE (spinner -> 2-button UniformGrid) |
| `Build2TargetList` | Method | ADD (internal static, replaces ComputeInstrSplit) |
| `OnInstr2tClick` | Method | ADD (replaces OnInstrQxClick) |
| `OnInstrQAll2tClick` | Method | ADD (replaces OnInstrBeClick) |

---

## Subtask T1b — PttQuickExit.cs

### File Path
`src/PropTraderTools/Features/PttQuickExit.cs`

### Change: Add tNQty <= 0 Guard (1-line addition)

**Location:** Inside the 7-arg `Execute()` overload, inside the
`for (int i = 0; i < targetCount; i++)` loop, immediately AFTER the `tNQty` assignment
block (lines ~117-120) and BEFORE the `string ocoId_i =` assignment (line ~122).

**Current code at insertion site (lines ~117-122):**
```csharp
int tNQty =
    (targets != null && i < targets.Count)
        ? targets[i].Qty
        : CalcTNQty(pos.Quantity, targetCount, i);

string ocoId_i =
```

**After change — insert exactly one line:**
```csharp
int tNQty =
    (targets != null && i < targets.Count)
        ? targets[i].Qty
        : CalcTNQty(pos.Quantity, targetCount, i);

if (tNQty <= 0) continue;   // B129: skip T2 when pos.Quantity==1 and t2Qty==0

string ocoId_i =
```

**Method signature (unchanged):**
```csharp
public void Execute(
    NinjaTrader.Cbi.Account leaderAccount,
    NinjaTrader.Cbi.Instrument instrument,
    int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = false,
    double leaderStop = 0,
    int leaderTargetCount = 0)
```

**CYC impact:**
- Execute() CYC before change: 7
- Execute() CYC after change: 8 (one additional `if` branch in for-loop body)
- CYC=8 is exactly at budget (Jane Street strict standard: CYC <= 8) — PASS

**JS rules for this change:**
- JS-021 (no lock): no lock() introduced — PASS
- JS-001 (no throw): `continue` is a loop control statement, not a throw — PASS
- JS-033 (no async void): Execute() is synchronous void, unchanged — PASS

**Purpose:**
When `Build2TargetList(1)` returns `[(0.0, 1), (0.0, 0)]`, `i=1` yields `tNQty=0`.
Without this guard, `CreateOrder` is called with qty=0 — an invalid NT8 API call
that may throw or produce a malformed OCO bracket. The guard skips the entire
T2 bracket construction when tNQty=0.

**Scope constraint:**
This is a 1-line addition. No other changes to PttQuickExit.cs are permitted in B129.

---

## Subtask T1c — B128Tests.cs

### File Path
`src/PropTraderTools/Tests/B128Tests.cs`

**Important:** Do NOT rename the file (`B128Tests.cs`) or the class (`B128Tests`).
Update in place — replace old test methods, add `using System.Collections.Generic;`
if not already present.

### Step 1 — Add using directive (if missing)

At the top of the file, ensure this using is present:
```csharp
using System.Collections.Generic;
```

### Step 2 — Remove old ComputeInstrSplit tests

**DELETE** all four of these test methods (they will no longer compile once
`ComputeInstrSplit` is removed in T1a):

- `T_B128_ComputeInstrSplit_EvenQty`
- `T_B128_ComputeInstrSplit_OddQty`
- `T_B128_ComputeInstrSplit_MinQty1`
- `T_B128_ComputeInstrSplit_MinQty7`

### Step 3 — Add new Build2TargetList tests

**ADD** the following five xUnit [Fact] tests (exact method names as shown):

```csharp
[Fact]
public void T_B129_01_Build2TargetList_Even_T1EqualT2()
{
    var result = TradeCopierPanel.Build2TargetList(4);
    Assert.Equal(2, result.Count);
    Assert.Equal(2, result[0].Qty);
    Assert.Equal(2, result[1].Qty);
    Assert.Equal(0.0, result[0].Price);
    Assert.Equal(0.0, result[1].Price);
}

[Fact]
public void T_B129_02_Build2TargetList_Odd_T1Heavier()
{
    var result = TradeCopierPanel.Build2TargetList(5);
    Assert.Equal(2, result.Count);
    Assert.Equal(3, result[0].Qty);
    Assert.Equal(2, result[1].Qty);
    Assert.Equal(0.0, result[0].Price);
    Assert.Equal(0.0, result[1].Price);
}

[Fact]
public void T_B129_03_Build2TargetList_One_T2IsZero()
{
    var result = TradeCopierPanel.Build2TargetList(1);
    Assert.Equal(2, result.Count);
    Assert.Equal(1, result[0].Qty);
    Assert.Equal(0, result[1].Qty);
    Assert.Equal(0.0, result[0].Price);
    Assert.Equal(0.0, result[1].Price);
}

[Fact]
public void T_B129_04_Build2TargetList_Large_Odd()
{
    var result = TradeCopierPanel.Build2TargetList(7);
    Assert.Equal(2, result.Count);
    Assert.Equal(4, result[0].Qty);
    Assert.Equal(3, result[1].Qty);
    Assert.Equal(0.0, result[0].Price);
    Assert.Equal(0.0, result[1].Price);
}

[Fact]
public void T_B129_05_Build2TargetList_Six_BothThree()
{
    // Covers "Quick2t press 6-contract: Output shows T1=3 T2=3" verification criterion
    var result = TradeCopierPanel.Build2TargetList(6);
    Assert.Equal(2, result.Count);
    Assert.Equal(3, result[0].Qty);
    Assert.Equal(3, result[1].Qty);
    Assert.Equal(0.0, result[0].Price);
    Assert.Equal(0.0, result[1].Price);
}
```

**Test access requirement:**
`Build2TargetList` is `internal static`. Tests call it directly as
`TradeCopierPanel.Build2TargetList(N)`. The test project must be listed in
`[assembly: InternalsVisibleTo("...")]` on the production assembly, or the
project reference must allow internal access. Verify that the test project
can resolve `TradeCopierPanel.Build2TargetList` at compile time before
finalizing T1a.

---

## 7-Scan Checklist (Engineer Contract)

The following 7 scans MUST all PASS before this ticket is considered complete.
Run each scan against all three touched files after implementing T1a + T1b + T1c.

### Per-Subtask Scan Mapping (defense-in-depth)

**T1a** (`TradeCopierPanel.cs` changes):
- SCAN-01: no `lock(` in new methods (BuildInstrRow, Build2TargetList, OnInstr2tClick, OnInstrQAll2tClick)
- SCAN-02: no `async void` in new methods
- SCAN-03: `Build2TargetList` returns `new List<>`, not null
- SCAN-04: no `throw new` in new methods
- SCAN-05: `[PTT-QX-2T]` tag present in OnInstr2tClick; no log tag required in OnInstrQAll2tClick (logs inside PttGlobalQuickExit.Execute())
- SCAN-06: CYC — `Build2TargetList`=1, `BuildInstrRow`=1, `OnInstr2tClick`<=4, `OnInstrQAll2tClick`=1
- SCAN-07: dotnet build 0 errors

**T1b** (`PttQuickExit.cs` changes):
- SCAN-01: no new `lock(` introduced
- SCAN-06: CYC — `PttQuickExit.Execute()` (7-arg)=8 after adding `if (tNQty <= 0) continue;`
- SCAN-07: dotnet build 0 errors

**T1c** (`B128Tests.cs` changes):
- SCAN-06: no new complexity issues in test file
- SCAN-07: `dotnet test` — 5 new tests pass (T_B129_01 through T_B129_05); 4 old ComputeInstrSplit tests absent


---

### SCAN-01 — No lock() in touched files

**Command:**
```powershell
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -n "lock(" src/PropTraderTools/Features/PttQuickExit.cs
grep -n "lock(" src/PropTraderTools/Tests/B128Tests.cs
```

**Required result:** 0 matches in all three files for any B129 change.

**JS Rule:** JS-021 (P0 — CRITICAL): No lock() usage. Lock is strictly banned.
Use Actor/FSM pattern or atomic primitives instead.

**PASS criteria:** grep returns empty output for all three commands.

---

### SCAN-02 — No async void (non-event-handler)

**Command:**
```powershell
grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs
grep -n "async void" src/PropTraderTools/Features/PttQuickExit.cs
```

**Required result:** 0 matches in B129-added code.

**JS Rule:** JS-033 (P0 — CRITICAL): Never use async void except for event handlers.
All new handlers in B129 (`OnInstr2tClick`, `OnInstrQAll2tClick`, `BuildInstrRow`,
`Build2TargetList`) are synchronous `void` or returning a value — none are async void.

**PASS criteria:** grep returns empty output (or any existing async void lines are
pre-existing event handlers not touched by B129).

---

### SCAN-03 — No return null in new methods

**Command:**
```powershell
grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs
```

**Required result:** `Build2TargetList` must not contain `return null`. It returns
`new List<(double, int)>`.

**JS Rule:** JS-002 (P0 — CRITICAL): Never return null for missing values.
`Build2TargetList` returns a 2-element `List<>` always — including when totalQty=1,
where T2.Qty=0 but the list itself is non-null.

**PASS criteria:** No `return null;` lines appear in `Build2TargetList` or any
other B129-added methods. Pre-existing `return null` lines in other unrelated methods
are acceptable if not introduced or modified by B129.

---

### SCAN-04 — No throw new in new methods

**Command:**
```powershell
grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs
grep -n "throw new" src/PropTraderTools/Features/PttQuickExit.cs
```

**Required result:** 0 `throw new` in any B129-added or B129-modified code.

**JS Rule:** JS-001 (P0 — CRITICAL): Never throw exceptions in hot paths.
`OnInstr2tClick`, `Build2TargetList`, `OnInstrQAll2tClick`, and `BuildInstrRow`
all use early `return` guards and log-and-continue patterns, never throw.
The `if (tNQty <= 0) continue;` guard in PttQuickExit.cs uses `continue`, not throw.

**PASS criteria:** grep returns empty output for B129 method bodies.

---

### SCAN-05 — Log tag and format correctness

**Command:**
```powershell
grep -n "PTT-QX-2T" src/PropTraderTools/TradeCopierPanel.cs
grep -n "PTT-QALL2T-INSTR\|PTT-QX-2T" src/PropTraderTools/TradeCopierPanel.cs
```

**Required result:**
- `OnInstr2tClick` Output.Process string contains `[PTT-QX-2T]` (exact tag)
- The format string uses `T1=` and `T2=` as key prefixes (NOT `t1q=` / `t2q=`)
- All string literals in new methods are ASCII-only (no Unicode, emoji, curly quotes)

**Verification:**
The exact Output.Process call in `OnInstr2tClick` must be:
```csharp
NinjaTrader.Code.Output.Process(
    "[PTT-QX-2T] button: " + _leaderAccount.Name
        + " " + _instrument.FullName
        + " qty=" + qty + " T1=" + targets[0].Qty + " T2=" + targets[1].Qty,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**PASS criteria:**
- grep for `PTT-QX-2T` finds exactly 1 match in TradeCopierPanel.cs (inside OnInstr2tClick)
- No occurrence of `[PTT-2T-INSTR]` anywhere in TradeCopierPanel.cs
- No occurrence of `t1q=` or `t2q=` in any B129 string literal

---

### SCAN-06 — CYC compliance (all methods within budget)

**Manual verification (code review):**

| Method | File | Expected CYC | Budget | Branches to Count |
|--------|------|-------------|--------|-------------------|
| `Build2TargetList` | TradeCopierPanel.cs | 1 | <=8 | Zero branches; straight assignment + return |
| `BuildInstrRow` | TradeCopierPanel.cs | 1 | <=8 | Sequential construction; no if/switch/loop |
| `OnInstr2tClick` | TradeCopierPanel.cs | 4 | <=8 | (1) `_instrument==null`, (2) `_leaderAccount==null`, (3) `FirstOrDefault` lambda, (4) `pos?.Quantity??1` |
| `OnInstrQAll2tClick` | TradeCopierPanel.cs | 1 | <=8 | Straight delegation; no branches |
| `Execute()` (7-arg) | PttQuickExit.cs | 8 | <=8 | +1 branch added (tNQty<=0 guard); was CYC=7 |

**Automated check command:**
```powershell
python scripts/complexity_audit.py --file src/PropTraderTools/TradeCopierPanel.cs
python scripts/complexity_audit.py --file src/PropTraderTools/Features/PttQuickExit.cs
```

**PASS criteria:** No method in the B129 diff exceeds CYC=8.

---

### SCAN-07 — Build passes with 0 errors and 0 warnings

**Command:**
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental
```

**Required result:** `Build succeeded. 0 Error(s). 0 Warning(s).`

**Additional test verification:**
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build
```

**Required test results:**
```
T_B129_01_Build2TargetList_Even_T1EqualT2     PASS
T_B129_02_Build2TargetList_Odd_T1Heavier      PASS
T_B129_03_Build2TargetList_One_T2IsZero       PASS
T_B129_04_Build2TargetList_Large_Odd          PASS
T_B129_05_Build2TargetList_Six_BothThree      PASS
```

**And these must NOT exist (compile error if they do):**
- `T_B128_ComputeInstrSplit_EvenQty`
- `T_B128_ComputeInstrSplit_OddQty`
- `T_B128_ComputeInstrSplit_MinQty1`
- `T_B128_ComputeInstrSplit_MinQty7`

---

## Verification Checklist (H-criteria from plan)

After implementing all three subtasks, verify:

| Check | Description | Pass Condition |
|-------|-------------|----------------|
| H.1 | Build | 0 errors, 0 warnings |
| H.2 | Tests | 5 new Build2TargetList tests PASS; 4 old ComputeInstrSplit tests GONE |
| H.3a | `_instrQxT1` removed | `grep -n "_instrQxT1" TradeCopierPanel.cs` = 0 results |
| H.3b | `_instrBeBtn` removed | `grep -n "_instrBeBtn" TradeCopierPanel.cs` = 0 results |
| H.3c | `OnInstrQxUp` removed | `grep -n "OnInstrQxUp" TradeCopierPanel.cs` = 0 results |
| H.3d | `OnInstrQxDown` removed | `grep -n "OnInstrQxDown" TradeCopierPanel.cs` = 0 results |
| H.3e | `OnInstrBeClick` removed | `grep -n "OnInstrBeClick" TradeCopierPanel.cs` = 0 results |
| H.3f | `ComputeInstrSplit` removed | `grep -n "ComputeInstrSplit" TradeCopierPanel.cs` = 0 results |
| H.4a | `_instr2tBtn` present | `grep -n "_instr2tBtn" TradeCopierPanel.cs` >= 1 result |
| H.4b | `_instrQAll2tBtn` present | `grep -n "_instrQAll2tBtn" TradeCopierPanel.cs` >= 1 result |
| H.4c | `Build2TargetList` present | `grep -n "Build2TargetList" TradeCopierPanel.cs` >= 1 result |
| H.4d | `OnInstr2tClick` present | `grep -n "OnInstr2tClick" TradeCopierPanel.cs` >= 1 result |
| H.4e | `OnInstrQAll2tClick` present | `grep -n "OnInstrQAll2tClick" TradeCopierPanel.cs` >= 1 result |
| H.5 | tNQty guard present | `grep -n "tNQty <= 0" PttQuickExit.cs` = 1 result |
| H.6 | PttGlobalQuickExit unchanged | `git diff src/PropTraderTools/Features/PttGlobalQuickExit.cs` = empty |
| H.8 | QAll2t fires GlobalQuickExit | Press QAll2t in NT8 UI: Output tab shows `[PTT-QX-ALL] GlobalQuickExit fired` (logged by PttGlobalQuickExit.Execute() internally) |
| H.7 | 7-scan pass | All SCAN-01 through SCAN-07 above pass |

---

## Deferred Items Generated by B129 (not in scope)

| ID | Description | Priority | Target Block |
|----|-------------|----------|--------------|
| DW-B129-01 | Director SIM gate: Quick2t + QAll2t live validation | P1 | B130 or first SIM session |
| DW-B133 | 2-target forced count for PttGlobalQuickExit ALL path (Option A deferred) | P2 | B133 |

---

*Tickets written: B129 Phase 3*
*Return: TICKETS_COMPLETE*
