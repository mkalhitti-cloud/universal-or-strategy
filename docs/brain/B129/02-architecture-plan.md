# B129 Architecture Plan
## Block: B129 — Instrument Row Redesign: Quick2t + QAll2t Buttons
## Phase 1 — Architecture
## Status: REVIEW_PASS
## Spec Section Closed: B122

---

## RULES CATALOG GATE

**Result: PASS**

P0 rules checked against all planned new code:
| Rule ID | Description | Status |
|---------|-------------|--------|
| JS-001 | No throw in hot paths | PASS — new handlers log via Output.Process, never throw |
| JS-002 | No return null | PASS — Build2TargetList returns List<>, never null; void handlers have no return value |
| JS-021 | No lock() | PASS — zero lock() in any new or modified code |
| JS-033 | No async void | PASS — all new handlers are synchronous void |
| DateTime.Now ban | Use DateTime.UtcNow | PASS — no DateTime.Now in new code; unchanged call sites already use UtcNow |
| ASCII-only | No Unicode in strings | PASS — "Quick2t", "QAll2t", all log tags are pure ASCII |
| FontFamily ban | No FontFamily usage | PASS — no FontFamily on new buttons |
| Hex color ban | No hardcoded hex | PASS — no hardcoded hex colors in new UI code |

---

## Section A — Problem Statement

### What B128 Built

B128 added the instrument row (`_instrRowPanel`) to TradeCopierPanel.cs above the quick-row panel.
It contained two elements:
1. **QX-Instr** — a button with up/down spinner arrows, label "QX-Instr T1=N", default T1=4 ticks.
   Handler: `OnInstrQxClick()` called `PttQuickExit.Execute(_leaderAccount, _instrument, t1, t2)`.
   Spinners: `OnInstrQxUp()` and `OnInstrQxDown()` incremented/decremented `_instrQxT1`.
2. **BE-Instr** — a plain button.
   Handler: `OnInstrBeClick()` called `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)`.

B128 also added `ComputeInstrSplit(int n)` which returned `(t1=(n+1)/2, t2=n/2)` for the QX-Instr qty split.

### Why B128 Is Being Redesigned

The QX-Instr + spinner + BE-Instr design introduced unnecessary UX complexity:
- The spinner state (`_instrQxT1`) is a mutable int field that must be managed and guarded.
- The BE-Instr button on the instrument row duplicates existing BE-ALL functionality without
  sufficient differentiation for the live-trading use case.
- Director confirmed the canonical live workflow for instrument-scoped exits uses a fixed
  2-target bracket: T1 at 4 ticks (1 pt MES), T2 at 8 ticks (2 pt MES), with a 50/50 qty
  split (ceiling-heavy for odd sizes).

The redesign replaces the spinner cluster entirely with two clean, fixed-behavior buttons:
- **"Quick2t"** — single-account 2-target bracket exit for the current instrument.
- **"QAll2t"** — all-accounts bracket exit (delegates to PttGlobalQuickExit).

---

## Section B — Architect Decision: QAll2t ALL-Accounts Path (Option A vs Option B)

### Option A Analysis

Option A proposed adding `Execute(int forcedTargetCount, bool t1Heavy)` to PttGlobalQuickExit.

**CYC Budget Analysis of Option A:**

`PttGlobalQuickExit.Execute()` (zero-arg) is the authoritative all-accounts loop with CYC=7:
- Account loop (1), follower guard (2), position loop (3), null/flat continue (4),
  DW-B115-DIAG for-loop (5), NeedsLeaderFallbackFlatten guard (6), ExecuteFollowers dispatch (7).

A new overload `Execute(int forcedTargetCount, bool t1Heavy)` that overrides the target
construction would require nearly identical outer loop structure (accounts, positions, cancels,
snapshots, diag blocks, flatten guard, follower dispatch) with one substitution:
instead of using `SnapshotTargetOrders()` count, it would build a 2-entry list.

Adding that substitution branch to the same outer structure reaches CYC=8. However:
- Implementing the substitution requires code duplication of the entire ~80-line inner body
  (accounts + positions double loop), which violates DRY.
- OR it requires extracting the inner double-loop body to a new private helper, changing the
  structure of the already-proven Execute() method (CYC=7, SIM-validated, tested).

Director's guidance: "if this overload adds CYC > 8 to PttGlobalQuickExit, scope it down."
The correct scope-down is Option B.

### Option B Decision

**DECISION: Option B APPROVED.**

`OnInstrQAll2tClick()` calls `new PttGlobalQuickExit().Execute()` — the existing zero-arg
all-accounts path (3-target snapshot-driven behavior).

**Rationale:**
1. Zero risk — Execute() is SIM-validated through multiple test cycles.
2. No changes to PttGlobalQuickExit.cs in B129 — fully isolated block scope.
3. For accounts with 3-target ATMs (standard MES/ES setup), QAll2t behaves identically
   to the existing QX-ALL button behavior — correct for the majority use case.
4. DW-B133 formally defers the forced 2-target ALL path to a future block where
   the correct architecture (passing forcedTargets into ExecuteOne rather than forking
   the outer loop) can be planned without destabilizing the existing execution chain.

**DW-B133 deferred item logged in Section G.**

---

## Section C — TradeCopierPanel.cs Change Plan

### C.1 — Field Changes

| Field | Action | New State |
|-------|--------|-----------|
| `_instrQxBtn` (Button) | Repurpose — rename to `_instr2tBtn` | `private Button _instr2tBtn;` |
| `_instrBeBtn` (Button) | Remove | Deleted |
| `_instrQxT1` (int, =4) | Remove | Deleted |
| `_instrRowPanel` (Panel) | Retain | `private System.Windows.UIElement _instrRowPanel;` |
| _(new)_ `_instrQAll2tBtn` | Add | `private Button _instrQAll2tBtn;` |

### C.2 — BuildInstrRow() Redesign

**Current state (B128):** Spinner cluster — QX-Instr label + up/down arrows + BE-Instr button.

**New state (B129):** 2-column UniformGrid with 2 plain buttons.

**New layout pseudocode (WPF):**
```
var grid = new System.Windows.Controls.Grid();
// 2 equal columns
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

_instr2tBtn = new Button { Content = "Quick2t" };
_instr2tBtn.Click += (s, e) => OnInstr2tClick();
Grid.SetColumn(_instr2tBtn, 0);
grid.Children.Add(_instr2tBtn);

_instrQAll2tBtn = new Button { Content = "QAll2t" };
_instrQAll2tBtn.Click += (s, e) => OnInstrQAll2tClick();
Grid.SetColumn(_instrQAll2tBtn, 1);
grid.Children.Add(_instrQAll2tBtn);

_instrRowPanel = grid;
```

No FontFamily. No hex colors. No Unicode. ASCII labels only.

### C.3 — Build2TargetList Helper (replaces ComputeInstrSplit)

**Signature:**
```csharp
internal static System.Collections.Generic.List<(double Price, int Qty)> Build2TargetList(int totalQty)
```

**Access modifier:** `internal static` (not private static) — required for testability from B128Tests.cs.

**Implementation:**
```csharp
internal static System.Collections.Generic.List<(double Price, int Qty)> Build2TargetList(int totalQty)
{
    int t1Qty = (totalQty + 1) / 2;
    int t2Qty = totalQty - t1Qty;
    return new System.Collections.Generic.List<(double, int)> { (0.0, t1Qty), (0.0, t2Qty) };
}
```

**Notes:**
- Price is 0.0 (placeholder) — PttQuickExit.Execute() computes actual prices from
  position entry price + tick offsets. Only the Qty values are used from this list.
- When totalQty=1: t1Qty=1, t2Qty=0. The `if (tNQty <= 0) continue;` guard in
  PttQuickExit.Execute() (Section D) prevents T2 order submission when t2Qty=0.
- CYC=1 (no branches). JS-002 compliant (returns list, never null).

**CYC=1.** JS-021: no lock. JS-001: no throw. ASCII-only.

### C.4 — OnInstr2tClick() Handler (replaces OnInstrQxClick)

**Signature:**
```csharp
private void OnInstr2tClick()
```

**Implementation pseudocode:**
```csharp
private void OnInstr2tClick()
{
    var leader = TryResolveLeaderAccount();      // (1) null guard
    if (leader == null) return;
    if (_instrument == null) return;             // (2) instrument guard

    Position pos = null;
    foreach (Position p in leader.Positions)    // (3) position loop
        if (p?.Instrument == _instrument)
        {
            pos = p;
            break;
        }
    if (pos == null || pos.Quantity == 0) return; // (4) flat guard

    var targets = Build2TargetList(pos.Quantity);
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-2T] button: " + leader.Name
        + " " + _instrument.FullName
        + " qty=" + pos.Quantity
        + " T1=" + targets[0].Qty
        + " T2=" + targets[1].Qty,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    new PttQuickExit().Execute(leader, _instrument, 4, targets, true, 0, 0);
}
```

**Notes:**
- `t1Ticks=4` is hardcoded per spec (T1=4 ticks=1pt MES, T2=8 ticks=2pt MES).
- `skipIfFollower=true` — single-account path; leader account only.
- `leaderStop=0, leaderTargetCount=0` — not needed for single-account path.
- `ResolveTargetCount(targets, 0)` inside Execute() returns `targets.Count=2`.
- CYC=4: (1) leader null, (2) instrument null, (3) position foreach, (4) flat guard.

**CYC=4.** JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.

### C.5 — OnInstrQAll2tClick() Handler (replaces OnInstrBeClick)

**Signature:**
```csharp
private void OnInstrQAll2tClick()
```

**Implementation:**
```csharp
private void OnInstrQAll2tClick()
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QALL2T-INSTR] button: fired",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    new PttGlobalQuickExit().Execute();
}
```

**Notes:**
- Delegates entirely to the existing all-accounts Execute() (Option B, see Section B).
- No account/instrument resolution needed — PttGlobalQuickExit.Execute() handles Account.All.
- CYC=1 (straight sequence, no branches).

**CYC=1.** JS-021: no lock. JS-001: no throw. JS-033: synchronous void. ASCII-only.

### C.6 — Handler and Spinner Removals

The following B128 methods are REMOVED in their entirety:

| Method | Reason |
|--------|--------|
| `OnInstrQxClick()` | Replaced by `OnInstr2tClick()` |
| `OnInstrQxUp()` | Spinner removed; no T1 tick adjustment in new design |
| `OnInstrQxDown()` | Spinner removed; no T1 tick adjustment in new design |
| `OnInstrBeClick()` | Replaced by `OnInstrQAll2tClick()` |
| `ComputeInstrSplit(int n)` | Replaced by `Build2TargetList(int totalQty)` |

All click delegate wires for the removed methods are also removed from BuildInstrRow().

### C.7 — B128Tests.cs Update Plan

**File:** `src/PropTraderTools/Tests/B128Tests.cs`

**Remove** (4 tests that call `TradeCopierPanel.ComputeInstrSplit`):
- `T_B128_ComputeInstrSplit_EvenQty` (input 4: expected t1=2, t2=2)
- `T_B128_ComputeInstrSplit_OddQty` (input 5: expected t1=3, t2=2)
- `T_B128_ComputeInstrSplit_MinQty1` (input 1: expected t1=1, t2=0)
- `T_B128_ComputeInstrSplit_MinQty7` (input 7: expected t1=4, t2=3)

**Add** (4 tests calling `TradeCopierPanel.Build2TargetList`):

| Test Name | Input | Asserts |
|-----------|-------|---------|
| `T_B129_Build2TargetList_EvenQty` | totalQty=4 | Count==2; [0].Qty==2; [1].Qty==2 |
| `T_B129_Build2TargetList_OddQty` | totalQty=5 | Count==2; [0].Qty==3; [1].Qty==2 |
| `T_B129_Build2TargetList_SingleQty` | totalQty=1 | Count==2; [0].Qty==1; [1].Qty==0 |
| `T_B129_Build2TargetList_LargeQty` | totalQty=7 | Count==2; [0].Qty==4; [1].Qty==3 |

Additional assertions for all tests: `[0].Price == 0.0` and `[1].Price == 0.0`
(confirms placeholder pricing is correct).

**Important:** All tests call `TradeCopierPanel.Build2TargetList` directly via `internal static`
access (enabled by existing `[assembly: InternalsVisibleTo("PropTraderTools")]` attribute or
equivalent in the test project configuration).

---

## Section D — PttQuickExit.cs Change Plan

### D.1 — tNQty <= 0 Guard Addition

**File:** `src/PropTraderTools/Features/PttQuickExit.cs`

**Location:** Inside the `for (int i = 0; i < targetCount; i++)` loop at lines 111-197,
after the `tNQty` assignment (lines 117-120) and before the `string ocoId_i` assignment (line 122).

**Current code at lines 117-122:**
```csharp
int tNQty =
    (targets != null && i < targets.Count)
        ? targets[i].Qty
        : CalcTNQty(pos.Quantity, targetCount, i);

string ocoId_i =
```

**Change — insert one line between tNQty assignment and ocoId_i:**
```csharp
int tNQty =
    (targets != null && i < targets.Count)
        ? targets[i].Qty
        : CalcTNQty(pos.Quantity, targetCount, i);

if (tNQty <= 0) continue;   // B129: skip T2 when pos.Quantity==1 and t2Qty==0

string ocoId_i =
```

**Purpose:** When `pos.Quantity == 1`, `Build2TargetList(1)` returns `[(0.0, 1), (0.0, 0)]`.
Without this guard, `CreateOrder` is called with `tNQty=0`, which is an invalid NT8 API call
that may throw or produce a malformed order.

**CYC impact:** Execute() current CYC=7 (per comment). Adding one `if` inside the for-loop
body = +1 branch. New CYC=8. Still within budget (<=8). ✅

**Scope:** This is a 1-line addition. No other changes to PttQuickExit.cs.

---

## Section E — PttGlobalQuickExit.cs Change Plan

**Option B APPROVED — no changes to PttGlobalQuickExit.cs in B129.**

See Section B for decision rationale. PttGlobalQuickExit.Execute() is unchanged.

DW-B133 (2-target forced count for ALL path) is logged as a new deferred item.

---

## Section F — CYC Analysis

| Method | File | CYC | Budget | Notes |
|--------|------|-----|--------|-------|
| `Build2TargetList(int)` | TradeCopierPanel.cs | 1 | <=8 ✅ | No branches; straight sequence |
| `OnInstr2tClick()` | TradeCopierPanel.cs | 4 | <=8 ✅ | leader null(1), instr null(2), pos foreach(3), flat guard(4) |
| `OnInstrQAll2tClick()` | TradeCopierPanel.cs | 1 | <=8 ✅ | Straight delegation to Execute() |
| `BuildInstrRow()` (modified) | TradeCopierPanel.cs | ~2 | <=8 ✅ | Grid construction; no conditional logic |
| `PttQuickExit.Execute()` (modified) | PttQuickExit.cs | 8 | <=8 ✅ | Adds 1 branch (tNQty guard); was CYC=7 |

No method exceeds CYC=8. All within Jane Street strict standard.

---

## Section G — 7-Scan Checklist

All items below apply to every new/modified method in B129.

### SCAN-01: No lock() anywhere
- `Build2TargetList` — no lock ✅
- `OnInstr2tClick` — no lock ✅
- `OnInstrQAll2tClick` — no lock ✅
- `BuildInstrRow` — no lock ✅
- `PttQuickExit.Execute` (modified) — no lock (existing; unchanged) ✅
- Grep: `grep -r "lock(" src/PropTraderTools/ --include="*.cs"` must return 0 results in B129 diff

### SCAN-02: No async void (non-event-handler)
- All new handlers are `private void` (synchronous) ✅
- No `async void` in any B129 addition ✅

### SCAN-03: No DateTime.Now
- No `DateTime.Now` in any B129 new or modified code ✅
- `DateTime.UtcNow` is used in existing ExecuteOne (unchanged) ✅

### SCAN-04: No return null
- `Build2TargetList` returns `new List<>` never null ✅
- `OnInstr2tClick`, `OnInstrQAll2tClick` are void ✅

### SCAN-05: ASCII-only identifiers and string literals
- Button labels: "Quick2t", "QAll2t" — ASCII ✅
- Log tags: "[PTT-QX-2T]", "[PTT-QALL2T-INSTR]" — ASCII ✅
- No Unicode, no emoji, no curly quotes in any new string ✅

### SCAN-06: No FontFamily, no hardcoded hex colors
- New buttons have no explicit FontFamily set ✅
- New buttons have no hardcoded hex Background/Foreground ✅

### SCAN-07: All CreateOrder calls use "PTT-" prefixed names
- No new CreateOrder calls in TradeCopierPanel.cs changes ✅
- PttQuickExit.Execute() CreateOrder calls use "PTT-QX-Stop", "PTT-QX-T{N}" — unchanged ✅
- B129 does not add any new CreateOrder calls; existing "PTT-QX-*" naming unchanged ✅

---

## Section H — Verify Criteria

After ptt-engineer implements B129-T1, the following must all pass:

### H.1 — Build Pass
- `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors, 0 warnings for B129 changes

### H.2 — Test Pass
```
T_B129_Build2TargetList_EvenQty     → PASS
T_B129_Build2TargetList_OddQty      → PASS
T_B129_Build2TargetList_SingleQty   → PASS
T_B129_Build2TargetList_LargeQty    → PASS
```
Old B128 ComputeInstrSplit tests must be gone (no compilation errors referencing ComputeInstrSplit).

### H.3 — Field Removal Verified
- `_instrQxT1` field: not present in TradeCopierPanel.cs ✅
- `_instrBeBtn` field: not present ✅
- `OnInstrQxUp` method: not present ✅
- `OnInstrQxDown` method: not present ✅
- `OnInstrBeClick` method: not present ✅
- `ComputeInstrSplit` method: not present ✅

### H.4 — New Members Present
- `_instr2tBtn` (Button field): present ✅
- `_instrQAll2tBtn` (Button field): present ✅
- `Build2TargetList(int)` (internal static): present ✅
- `OnInstr2tClick()` (private void): present ✅
- `OnInstrQAll2tClick()` (private void): present ✅

### H.5 — PttQuickExit Guard
- `if (tNQty <= 0) continue;` present inside Execute() for-loop, after tNQty assignment ✅

### H.6 — PttGlobalQuickExit Unchanged (Option B)
- `PttGlobalQuickExit.cs` diff is empty (no changes) ✅

### H.7 — 7-Scan Pass
```powershell
grep -r "lock(" src/PropTraderTools/ --include="*.cs"           # 0 results required
grep -rn "async void " src/PropTraderTools/ --include="*.cs"    # 0 results in new code
grep -rn "DateTime.Now" src/PropTraderTools/ --include="*.cs"   # 0 results required
grep -rn "return null;" src/PropTraderTools/ --include="*.cs"   # 0 results in new code
```

### H.8 — DW-B128-01 Status
- DW-B128-01 (QX-Instr + BE-Instr SIM gate) is SUPERSEDED — those buttons are removed.
- New DW-B129-01 (Quick2t + QAll2t SIM gate) is logged in deferred backlog.

---

## Deferred Items Generated by B129

### DW-B129-01 — Director SIM Gate: Quick2t + QAll2t Live Validation
**Priority:** P1 — required before using Quick2t / QAll2t in a live session.
**Context:** The `_instr2tBtn` ("Quick2t") and `_instrQAll2tBtn` ("QAll2t") buttons added in
B129 require a Director SIM session to confirm:
- `_instrument` resolves non-null on a chart with an instrument loaded.
- `TryResolveLeaderAccount()` returns a non-null leader when copier is configured.
- `[PTT-QX-2T]` log appears in Output tab with correct account/instrument/qty/T1=/T2=.
- `PttQuickExit.Execute(_leader, _instrument, 4, targets, true)` fires and submits PTT-QX-T1 and PTT-QX-T2 brackets for the leader account only.
- For qty=1 position: PTT-QX-T1 submitted (qty=1), PTT-QX-T2 skipped (qty=0 guard fires).
- `[PTT-QALL2T-INSTR]` log appears in Output tab when QAll2t clicked.
- `PttGlobalQuickExit.Execute()` fires and covers all accounts with non-flat positions.
- No naked positions result from either button action.
**Deferred to:** B130 or first SIM gate session after B129 sync.

### DW-B133 — 2-Target Forced Count for PttGlobalQuickExit ALL Path
**Priority:** P2 — enhancement to make QAll2t use exactly 2-target bracket on all accounts.
**Context:** Option B was chosen in B129 (QAll2t calls existing Execute() — 3-target snapshot
path). The correct architectural approach for forced 2-target ALL behavior is:
- Pass a `forcedTargets` list parameter to `ExecuteOne()` (currently CYC=2) alongside `targets`.
- When `forcedTargets != null && forcedTargets.Count > 0`, `ExecuteOne` uses `forcedTargets`
  instead of the snapshotted `targets` for order qty calculation.
- This avoids duplicating the outer account/position loop and stays within CYC budget for all
  affected methods.
- `ExecuteFollowers` would need a parallel forced-targets passthrough.
- New overload `Execute(List<(double, int)> forcedTargets)` on PttGlobalQuickExit calls existing
  outer loop with the forced list substitution applied at ExecuteOne call site.
**Deferred to:** B133 or first block after B129 SIM gate passes.

---

## Component Summary

| Component | File | Change Type |
|-----------|------|-------------|
| BuildInstrRow | TradeCopierPanel.cs | REPLACE (spinner → 2-button grid) |
| Build2TargetList | TradeCopierPanel.cs | ADD (replaces ComputeInstrSplit) |
| OnInstr2tClick | TradeCopierPanel.cs | ADD (replaces OnInstrQxClick) |
| OnInstrQAll2tClick | TradeCopierPanel.cs | ADD (replaces OnInstrBeClick) |
| OnInstrQxClick | TradeCopierPanel.cs | REMOVE |
| OnInstrQxUp | TradeCopierPanel.cs | REMOVE |
| OnInstrQxDown | TradeCopierPanel.cs | REMOVE |
| OnInstrBeClick | TradeCopierPanel.cs | REMOVE |
| ComputeInstrSplit | TradeCopierPanel.cs | REMOVE |
| _instrQxT1 field | TradeCopierPanel.cs | REMOVE |
| _instrBeBtn field | TradeCopierPanel.cs | REMOVE |
| _instrQxBtn field | TradeCopierPanel.cs | REPURPOSE as _instr2tBtn |
| _instrQAll2tBtn field | TradeCopierPanel.cs | ADD |
| tNQty <= 0 guard | PttQuickExit.cs | ADD (1-line inside for-loop) |
| B128Tests.cs | Tests/B128Tests.cs | UPDATE (4 tests removed, 4 added) |
| PttGlobalQuickExit.cs | Features/PttGlobalQuickExit.cs | NO CHANGES (Option B) |

---

*Plan written: B129 Phase 1*
*Return: PLAN_COMPLETE*
