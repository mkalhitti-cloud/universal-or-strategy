# B74-LaneC Architecture Plan

**Phase**: 1 (Architecture — Retrospective)
**Status**: REVIEW_PASS candidate
**Written by**: ptt-architect
**Pipeline mode**: Retrospective — code changes already applied in `src/`. This plan describes
what is in source, not what should be built.

---

## Section 1: Block Overview

| Field | Value |
|-------|-------|
| Block ID | B74-LaneC |
| Lane | C |
| Hotfix IDs | B74-C-01, B74-C-02, B74-C-03, B74-C-04, B74-C-05 |
| Pipeline mode | Retrospective (code already in `src/`) |

### Files Modified

| File | Hotfixes |
|------|----------|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | B74-C-01 |
| `src/PropTraderTools/CopyEngine.cs` | B74-C-02, B74-C-03 |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | B74-C-03, B74-C-04 |
| `src/PropTraderTools/Features/PttQuickExit.cs` | B74-C-04, B74-C-05 |

### Prior Block Context

Block B66-LaneC was the most recent prior LaneC block. Its deferred backlog carried 9 OPEN
items forward (DW-B66-C-02, DW-B66-BE-01, DW-B63-01, DW-B54-01, DW-B58-01..03,
PRE-EXISTING-01..03). None of those items are closed by B74-LaneC; they remain OPEN and
carry forward to the next block.

The No-Pipeline-Repairs log (`docs/brain/NO-PIPELINE-REPAIRS.md`) records live-trading hotfixes
applied outside the pipeline. The 5 hotfixes in this block are a subset of those repairs;
this pipeline run brings them formally through Phase 1–5.

---

## Section 2: Hotfix Descriptions

### B74-C-01 — HOTFIX-BEALL-BUFFER-SYNC-01

**File**: `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**Methods**: `IncrementBuffer`, `DecrementBuffer`

**Problem**:
`PttGlobalBreakEven.IncrementBuffer` and `DecrementBuffer` previously mutated `_globalBeBuffer`
(a `volatile int` field) and then directly invoked `CopyEngine.Instance.GlobalBeBufferChanged?.Invoke(newValue)`.
This violates C# language rule CS0070: an event may only be raised (invoked via `?.Invoke(...)`) from
inside the class that declares it. `GlobalBeBufferChanged` is declared on `CopyEngine`; any external
invocation is a compile error.

**Fix**:
Both methods now call `CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer)` after mutating the
field. `RaiseBeBufferChanged` is a relay method declared on `CopyEngine` that raises the event on its
behalf (see B74-C-02). The call site in `PttGlobalBreakEven` uses the relay; it never invokes the
event directly.

**Source lines** (as-built):
```csharp
// PttGlobalBreakEven.cs line 92-99
internal void IncrementBuffer()   // CYC=2
{
    if (_globalBeBuffer < 10) _globalBeBuffer++;
    CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer); // HOTFIX-CS0070
}

internal void DecrementBuffer()   // CYC=2
{
    if (_globalBeBuffer > -10) _globalBeBuffer--;
    CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer); // HOTFIX-CS0070
}
```

**Compliance**:
- JS-021 no `lock()`: ✅ (`_globalBeBuffer` is `volatile int`, no lock)
- JS-001 no `throw new`: ✅
- JS-002 no `return null`: ✅ (void methods)
- JS-033 no `async void`: ✅ (synchronous void only)
- CYC: `IncrementBuffer` = 2, `DecrementBuffer` = 2 — both ≤ 8 ✅

---

### B74-C-02 — HOTFIX-CS0070-BEBUFFER-01

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `RaiseBeBufferChanged(int newValue)` (new method, line 186)
**Also**: `GlobalBeBufferChanged` event declaration (line 184)

**Problem**:
CS0070 requires that `GlobalBeBufferChanged` be raised only from inside `CopyEngine`.
Without a relay method, external callers had no legal path to fire the event.

**Fix**:
Added event declaration and relay method at lines 184–188:
```csharp
internal event Action<int> GlobalBeBufferChanged;
// CS0070 fix: relay so external callers can raise this event without violating CS0070.
internal void RaiseBeBufferChanged(int newValue)
    => System.Windows.Application.Current.Dispatcher.InvokeAsync(
        () => GlobalBeBufferChanged?.Invoke(newValue));
```

The relay dispatches to the WPF application UI thread via `Dispatcher.InvokeAsync` before
invoking the event. This ensures all `GlobalBeBufferChanged` subscribers (panel UI handlers)
execute on the UI thread, which is required for WPF control updates.

**Pattern reference**: Same pattern as `RaiseBeAllDisarmed` (line 211, added in B73):
```csharp
internal void RaiseBeAllDisarmed() => GlobalBeAllDisarmed?.Invoke();
```
`RaiseBeBufferChanged` adds `Dispatcher.InvokeAsync` because buffer changes originate from
UI button handlers (correct path already on UI thread via the Dispatcher, no double-dispatch
risk) and the relay adds no additional thread-hop overhead.

**Compliance**:
- JS-021 no `lock()`: ✅ (`Dispatcher.InvokeAsync` is the correct NT8/WPF async-dispatch primitive)
- JS-001 no `throw new`: ✅
- JS-002 no `return null`: ✅
- JS-033 no `async void`: ✅ (expression-bodied method returning `DispatcherOperation`)
- CYC: 1 (expression body, zero branches) ✅

---

### B74-C-03 — HOTFIX-QUICKALL-SINGLETON-01

**Files**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Methods**: `CopyEngine.IncrementQuickAll`, `CopyEngine.DecrementQuickAll`, `CopyEngine.GlobalQuickAllT1` (property),
             `PttGlobalQuickExit.ResolveQuickTicks`

**Problem**:
Prior to this fix `PttGlobalQuickExit.ResolveQuickTicks` fell back to `InstrumentDefaults.GetQuickTicks`
for the t1 value, which uses a per-instrument hardcoded default (MES=4, MGC=2). There was no
globally shared Quick ALL T1 value that all open panels would read consistently. Changing the
Quick ALL tick buffer on one panel did not propagate to the shared execution path.

Additionally, the t1 value was expressed in a way that was ambiguous about units (ticks vs. points).

**Fix — CopyEngine additions (lines 191–207)**:
```csharp
// HOTFIX-QUICKALL-SINGLETON-01: Quick ALL tick buffer -- singleton.
private volatile int _globalQuickAllT1 = 4;   // default 4 ticks
internal int GlobalQuickAllT1 => _globalQuickAllT1;

internal void IncrementQuickAll()
{
    if (_globalQuickAllT1 < 99) _globalQuickAllT1++;
    int v = _globalQuickAllT1;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(
        () => GlobalQuickAllBufferChanged?.Invoke(v));
}

internal void DecrementQuickAll()
{
    if (_globalQuickAllT1 > 1) _globalQuickAllT1--;
    int v = _globalQuickAllT1;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(
        () => GlobalQuickAllBufferChanged?.Invoke(v));
}

internal event Action<int> GlobalQuickAllBufferChanged;
```

`_globalQuickAllT1` is `volatile int` (JS-023 compliant; `volatile double` is NT8-003-banned and
is not used here). The default 4 matches the per-panel default for MES. The upper bound 99 and
lower bound 1 prevent degenerate zero-tick exits.

**Fix — `ResolveQuickTicks` (PttGlobalQuickExit.cs line 61)**:
```csharp
private static (int t1, int t2) ResolveQuickTicks(Instrument instr)
{
    var engine = CopyEngine.Instance;
    if (engine == null)
        return InstrumentDefaults.GetQuickTicks(instr?.MasterInstrument?.Name ?? string.Empty);
    int t1 = engine.GlobalQuickAllT1;   // HOTFIX-QUICKALL-SINGLETON-01
    int t2 = t1 * 2;
    return (t1, t2);
}
```

When engine is non-null the singleton value is used. The `InstrumentDefaults` fallback is
preserved only for the engine-null path (test seams, defensive startup guard).

The label suffix "t" (e.g. `"Quick ALL +4t"`) displayed in the panel makes the ticks unit
explicit to the user. Unit is ticks for multi-instrument compatibility (MES tick = 0.25pt,
MGC tick = 0.1pt; a 4-tick value means different point distances per instrument).

**Compliance**:
- JS-021 no `lock()`: ✅ (`volatile int`, `Dispatcher.InvokeAsync`)
- JS-001 no `throw new`: ✅
- JS-002 no `return null`: ✅ (returns tuple, never null)
- JS-033 no `async void`: ✅
- CYC: `IncrementQuickAll` = 2, `DecrementQuickAll` = 2, `ResolveQuickTicks` = 2 — all ≤ 8 ✅

---

### B74-C-04 — HOTFIX-QUICK-T3-01

**Files**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`, `src/PropTraderTools/Features/PttQuickExit.cs`
**Methods**: `PttGlobalQuickExit.SnapshotTargetOrders`, `PttGlobalQuickExit.Execute`,
             `PttGlobalQuickExit.ExecuteOne`, `PttQuickExit.Execute` (primary overload),
             `PttQuickExit.Execute` (compat overload)

**Problem**:
Prior to this fix the Quick Exit always placed exactly 2 OCO pairs (T1 + T2), regardless of
how many active target orders the account had. When a trader had 3 or more ATM targets (e.g.
from a 3-contract ATM strategy: Target1, Target2, Target3), pressing Quick ALL/Quick Exit
replaced them with only 2 PTT-QX targets, leaving one contract without a target order. This
was functionally incorrect when targetCount > 2.

Additionally, all target orders shared the same OCO ID, so filling T1 cancelled the T2 stop
as well — the two brackets were not independent.

**Fix — `SnapshotTargetOrders` (PttGlobalQuickExit.cs lines 87–111)**:
Scans `acc.Orders` for active (`Working` or `Accepted`) `Limit` orders on `instr` before
any cancel happens. Identifies target orders by three name patterns:
- ATM targets: `name.StartsWith("Target")` with digit after position 6 (e.g. `Target1`–`Target9`)
- PTT-QX targets: `name.StartsWith("PTT-QX-T")` with digit after position 8
- PTT-BE targets: `name.StartsWith("PTT-BE-Target-")`

Returns `List<(double Price, int Qty)>` — never returns null (JS-002). Each entry stores
the order's `LimitPrice` and `Quantity` for use in the submission loop.

**Fix — `Execute` flow (PttGlobalQuickExit.cs lines 29–52)**:
`Execute()` now calls `SnapshotTargetOrders(acc, pos.Instrument)` before `ExecuteOne`. The
snapshot result is passed through the call chain: `Execute → ExecuteOne → PttQuickExit.Execute`.

**Fix — `ExecuteOne` (PttGlobalQuickExit.cs lines 72–79)**:
Accepts `List<(double Price, int Qty)> targets` parameter; delegates directly to `PttQuickExit.Execute`.
CYC = 1 (straight delegation).

**Fix — `PttQuickExit.Execute` primary overload (PttQuickExit.cs lines 36–159)**:
New primary signature:
```csharp
internal void Execute(
    Account leader, Instrument instr, int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true)
```

Key behavioral changes:
1. **targetCount**: `(targets != null && targets.Count > 0) ? targets.Count : 2` — uses snapshot
   count; falls back to 2 when no live targets found.
2. **N-bracket for-loop** (`i = 0 .. targetCount-1`): each iteration computes:
   - `tNTicks = t1Ticks * (i + 1)` — proportional: T1=t1, T2=t1×2, T3=t1×3
   - `tNPrice = round((entry ± tNTicks × tick) / tick) × tick`
   - `tNQty`: from `targets[i].Qty` when available; fallback `max(1, pos.Quantity / targetCount)`
3. **Independent OCO IDs**: each pair calls `CopyEngine.Instance.NextQxOcoId()` independently.
   Filling T1 only cancels Stop1 (same OCO group); T2 and T3 remain live.
4. **Stop names**: `PTT-QX-Stop` (i=0), `PTT-QX-Stop2` (i=1), `PTT-QX-Stop3` (i=2) ...
5. **Target names**: `PTT-QX-T1` (i=0), `PTT-QX-T2` (i=1), `PTT-QX-T3` (i=2) ...

Order submission uses NT8-049 arg order: StopMarket `(arg6=0, arg7=stopPrice)`,
Limit `(arg6=limitPrice, arg7=0)`. NT8-007 cast `(CustomOrder)null`. NT8-013 `DateTime.MaxValue`
for GTC. NT8-014 `PTT-QX-*` prefix.

**Fix — `PttQuickExit.Execute` compat overload (PttQuickExit.cs lines 168–173)**:
```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)
{
    Execute(leader, instr, t1Ticks,
        new System.Collections.Generic.List<(double Price, int Qty)>(),
        skipIfFollower);
}
```
Bridges the old `(t1, t2)` call signature used by `TradeCopierPanel.OnQuickClick` to the new
targets-based `Execute`. Empty targets list triggers the targetCount=2 fallback (identical to
prior 2-target behavior). `TradeCopierPanel.cs` is not modified.

**Execution path** (Quick ALL):
```
TradeCopierPanel.OnQuickAllClick
  → PttGlobalQuickExit.Execute()
    → SnapshotTargetOrders(acc, instr)    -- per account+instrument, before any cancel
    → ResolveQuickTicks(instr)             -- reads CopyEngine.GlobalQuickAllT1
    → ExecuteOne(acc, instr, t1, targets)
      → PttQuickExit.Execute(acc, instr, t1, targets)
        → SnapshotStopPrice                -- reads stop price before cancel
        → CopyEngine.CancelQxBrackets      -- cancel ATM + prior PTT-QX
        → for-loop (N pairs)
          → CreateOrder PTT-QX-StopN + Submit
          → CreateOrder PTT-QX-TN + Submit
```

**Compliance**:
- JS-021 no `lock()`: ✅ (OCO counter via `Interlocked.Increment` in `NextQxOcoId`)
- JS-001 no `throw new`: ✅ (exceptions caught and logged via `Output.Process`)
- JS-002 no `return null`: ✅ (`SnapshotTargetOrders` returns list; `Execute` is void)
- JS-033 no `async void`: ✅ (all methods synchronous void or return type)
- CYC: `Execute` (primary) = 8, `Execute` (compat) = 1, `SnapshotTargetOrders` = 4,
  `ExecuteOne` = 1 — all ≤ 8 ✅

---

### B74-C-05 — HOTFIX-SNAPSHOT-STOP-INSTRREF

**File**: `src/PropTraderTools/Features/PttQuickExit.cs`
**Method**: `SnapshotStopPrice`

**Problem**:
`SnapshotStopPrice` previously filtered orders with reference equality:
```csharp
if (o.Instrument != instr)  continue;
```
NT8 creates a separate `Instrument` object instance per account context. When `SnapshotStopPrice`
scans `leader.Orders` for an `Instrument` reference that was obtained from a different account
context or from `pos.Instrument`, the reference equality check always returns `true` (different
objects = not equal), silently skipping every order. The result: `snapshotStop = 0.0` on every
call, no existing stop price was found, and the PTT-QX stop orders were placed without the
correct stop reference from the ATM bracket.

Root cause is identical to:
- B72-A-08: `MoveStopToBreakEven` — same fix applied there
- B69 DW-B69-02: `FindPosition` and `SubmitBeStop` — same fix applied there
- B74-C-05 is the third occurrence of this pattern in the codebase

**Fix** (as-built, line 183):
```csharp
if (o.Instrument == null || o.Instrument.FullName != instr?.FullName) continue;
// HOTFIX-SNAPSHOT-STOP-INSTRREF: FullName comparison
// (NT8 creates separate Instrument instances per account context)
```

Null-guards `o.Instrument` before accessing `FullName` (prevents NRE). Null-guards `instr`
with `?.FullName` on the right side. String equality on `FullName` correctly identifies the
same instrument across account contexts.

**Compliance**:
- JS-021 no `lock()`: ✅
- JS-001 no `throw new`: ✅
- JS-002 no `return null`: ✅ (returns `double 0.0` not null)
- JS-033 no `async void`: ✅
- CYC: unchanged (same filter shape; null guard is part of the existing boolean expression;
  Roslyn counts the `||` chain as one decision block) ✅

---

## Section 3: Architecture Themes

### Theme 1 — CS0070 Relay Pattern

**Rule**: C# CS0070 — "The event may only appear on the left-hand side of += or -=
(except when used from within the type `CopyEngine`)."

Events declared on `CopyEngine` may only be raised (`?.Invoke(...)`) from within `CopyEngine`.
External classes (`PttGlobalBreakEven`, panel code) that need to trigger an event must call a
relay method on `CopyEngine` instead.

**Pattern** (as-built in CopyEngine.cs):
```csharp
// Declaration
internal event Action<int> GlobalBeBufferChanged;

// Relay method (the CS0070 fix)
internal void RaiseBeBufferChanged(int newValue)
    => System.Windows.Application.Current.Dispatcher.InvokeAsync(
        () => GlobalBeBufferChanged?.Invoke(newValue));
```

**External caller** (PttGlobalBreakEven.cs):
```csharp
CopyEngine.Instance.RaiseBeBufferChanged(_globalBeBuffer);  // CORRECT
// GlobalBeBufferChanged?.Invoke(_globalBeBuffer);          // BANNED (CS0070)
```

**Existing relay precedent**: `RaiseBeAllDisarmed()` (line 211) — same pattern, no
`Dispatcher.InvokeAsync` needed there because that relay is always called from the UI thread.
`RaiseBeBufferChanged` adds the `Dispatcher.InvokeAsync` wrapper because the event consumers
are WPF UI handlers that must run on the UI thread, and the relay provides the correct dispatch.

**Rule for future blocks**: Any new `CopyEngine` event that external classes must raise needs
a corresponding `Raise*` relay method. Never call `event?.Invoke()` from outside the declaring
class.

---

### Theme 2 — N-Bracket Quick Exit Design

**Motivation**: Traders using ATM strategies with 3+ targets (3-contract setups) need Quick Exit
to place the same number of bracket pairs as their ATM, not a hardcoded 2.

**Snapshot-before-cancel ordering**:
`SnapshotTargetOrders` is called before `CancelQxBrackets`. This ensures the live target orders
are captured while they still exist. If called after cancel, the list would always be empty
and targetCount would always fall back to 2.

**targetCount resolution**:
```
targetCount = snapshot.Count > 0 ? snapshot.Count : 2
```
- Snapshot found targets → use exact count (1, 2, 3, ... up to 9)
- No targets found (flat recovery, test path) → fallback 2

**Proportional tick spacing**:
```
T1 price = entry ± t1 ticks
T2 price = entry ± (t1×2) ticks
T3 price = entry ± (t1×3) ticks
TN price = entry ± (t1×N) ticks
```
All spacing is from entry (not from the prior target). Equal tick distance between each tier.

**Quantity per bracket**:
```
tNQty = snapshot[i].Qty   if snapshot[i] exists
      = max(1, pos.Quantity / targetCount)  otherwise
```
Snapshot quantities respect the original ATM allocation. Fallback splits position evenly.

**Independent OCO pairs**:
Each bracket pair (stopN + targetN) gets its own OCO ID via `CopyEngine.NextQxOcoId()`.
When T1 fills, its OCO group triggers Cancel on Stop1 only. Stop2 and Stop3 remain live.
This is the correct behavior for a multi-bracket bracket-management approach.

**Order naming**:
```
Stops:   PTT-QX-Stop (i=0), PTT-QX-Stop2 (i=1), PTT-QX-Stop3 (i=2)
Targets: PTT-QX-T1   (i=0), PTT-QX-T2   (i=1), PTT-QX-T3   (i=2)
```
Note: first stop is `PTT-QX-Stop` (no suffix digit) for backward compatibility with
`IsQxCancelCandidate` detection logic.

**Compat overload for TradeCopierPanel**:
`TradeCopierPanel.OnQuickClick` calls the old `(t1, t2)` signature. The compat overload
passes an empty list, triggering the targetCount=2 fallback — identical to prior behavior.
This allows `TradeCopierPanel.cs` to remain unmodified.

---

### Theme 3 — GlobalQuickAllT1 Singleton

**Motivation**: Multiple panels open simultaneously (e.g., MES panel + MGC panel) must read
the same Quick ALL tick buffer value. A per-panel field would allow the two panels to show
different values, and changing t1 on one panel would not affect the other.

**Singleton storage** (CopyEngine.cs lines 191–207):
```csharp
private volatile int _globalQuickAllT1 = 4;   // default 4 ticks
internal int GlobalQuickAllT1 => _globalQuickAllT1;
```

`volatile int` is used per JS-023 (volatile int allowed). `volatile double` is banned by
NT8-003. Default 4 ticks matches the per-panel MES default.

**Bounds**: `IncrementQuickAll` caps at 99; `DecrementQuickAll` floor at 1. Zero is excluded
because a zero-tick exit is degenerate (target at entry price = immediate fill, no exit effect).

**Broadcast on change**:
```csharp
internal event Action<int> GlobalQuickAllBufferChanged;
// raised via Dispatcher.InvokeAsync in IncrementQuickAll / DecrementQuickAll
```
All panels subscribe to `GlobalQuickAllBufferChanged` and update their label on receipt.
The `Dispatcher.InvokeAsync` dispatch ensures the label update runs on the WPF UI thread.

**Consumption in `ResolveQuickTicks`**:
```csharp
int t1 = engine.GlobalQuickAllT1;
int t2 = t1 * 2;
```
`t2` is derived from `t1` at execution time (not stored separately). This keeps the singleton
to one integer and makes the relationship explicit.

**Unit**: Ticks (not points). The label suffix `"t"` (e.g. `"Quick ALL +4t"`) makes this
visible to the user. Multi-instrument compatibility: 4 ticks on MES = 1.00 pt; 4 ticks on
MGC = 0.40 pt. Using ticks as the stored unit avoids the per-instrument conversion.

**Fallback path**: When `engine == null` (test seam, defensive startup), `InstrumentDefaults.GetQuickTicks`
provides per-instrument defaults. The t1=0 path (previously existed) is eliminated; the only
zero-tick path is if the engine is null AND `InstrumentDefaults` returns 0, which it never
does (minimum return is 2 for MGC).

---

### Theme 4 — Instrument FullName Equality

**Root cause**: NT8 creates a separate `Instrument` object instance per account context.
When the same instrument (e.g., MES 09-26) appears on two different accounts in the same
session, NT8 gives each account a distinct `Instrument` reference. C# reference equality
(`!=`, `==` for objects) checks whether the two variables point to the same heap address —
not whether they represent the same instrument. Since the objects are always different
instances, `o.Instrument != instr` is always `true`, causing every order to be skipped silently.

**Correct pattern** (used in all fixed call sites):
```csharp
// WRONG -- reference equality (always false for cross-account Instrument):
if (o.Instrument != instr)  continue;

// CORRECT -- value equality via FullName:
if (o.Instrument == null || o.Instrument.FullName != instr?.FullName)  continue;
```

The null guard on `o.Instrument` prevents an NRE when NT8 returns an order with no instrument
reference. The null-conditional `instr?.FullName` on the right side prevents NRE if the caller
passes a null `instr` argument.

**Codebase occurrences** (all now fixed):

| Block | Method | File |
|-------|--------|------|
| B69 DW-B69-02 | `FindPosition`, `SubmitBeStop` | `CopyEngine.cs` |
| B72-A-08 | `MoveStopToBreakEven` | `CopyEngine.cs` |
| B74-C-05 | `SnapshotStopPrice` | `PttQuickExit.cs` |

**Rule for future blocks**: Any new method scanning `acc.Orders` or `acc.Positions` for a
specific instrument MUST use `FullName` string equality, never reference equality.

---

## Section 4: Method Signatures (As-Built)

### PttGlobalBreakEven.cs

```csharp
// CYC=2
internal void IncrementBuffer()
internal void DecrementBuffer()
// unchanged from prior block:
internal void Execute(int bufferTicks)                                     // CYC=1
internal void Execute(IEnumerable<Account> accounts, int bufferTicks)     // CYC=5
internal int GlobalBeBuffer { get; }                                       // CYC=1
internal static string BuildGlobalBeOcoId(int seq, int accIdx, int pairIndex)  // CYC=1
```

### CopyEngine.cs (new/modified in B74-LaneC)

```csharp
// New event (line 184)
internal event Action<int> GlobalBeBufferChanged;

// New relay method (line 186) -- CYC=1
internal void RaiseBeBufferChanged(int newValue)

// New field (line 191)
private volatile int _globalQuickAllT1 = 4;

// New property (line 192) -- CYC=1
internal int GlobalQuickAllT1 { get; }

// New methods (lines 193-206) -- CYC=2 each
internal void IncrementQuickAll()
internal void DecrementQuickAll()

// New event (line 207)
internal event Action<int> GlobalQuickAllBufferChanged;
```

### PttGlobalQuickExit.cs

```csharp
// Modified (HOTFIX-QUICK-T3-01): added SnapshotTargetOrders call and targets passthrough
internal void Execute()                                                    // CYC=8

// Modified (HOTFIX-QUICKALL-SINGLETON-01): reads GlobalQuickAllT1 from singleton
private static (int t1, int t2) ResolveQuickTicks(Instrument instr)      // CYC=2

// Modified (HOTFIX-QUICK-T3-01): accepts targets parameter
private void ExecuteOne(
    Account acc, Instrument instr, int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true)                                            // CYC=1

// New (HOTFIX-QUICK-T3-01): scans orders before cancel
private static System.Collections.Generic.List<(double Price, int Qty)> SnapshotTargetOrders(
    Account acc, NinjaTrader.Cbi.Instrument instr)                        // CYC=4
```

### PttQuickExit.cs

```csharp
// New primary overload (HOTFIX-QUICK-T3-01): N-bracket for-loop
internal void Execute(
    Account leader, Instrument instr, int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true)                                            // CYC=8

// New compat overload (bridges old 4-arg callers)
internal void Execute(
    Account leader, Instrument instr, int t1Ticks, int t2Ticks,
    bool skipIfFollower = true)                                            // CYC=1

// Modified (HOTFIX-SNAPSHOT-STOP-INSTRREF): FullName comparison
private static double SnapshotStopPrice(Account acc, Instrument instr)   // CYC=2
```

---

## Section 5: Test Requirements Matrix

### T_BE_BUF_RELAY_01 — IncrementBuffer calls relay, not event directly
- **Method under test**: `PttGlobalBreakEven.IncrementBuffer`
- **Scenario**: Call `IncrementBuffer()` with injected relay counter spy
- **Expected**: Relay (`RaiseBeBufferChanged`) called exactly once with `newValue = _globalBeBuffer`

### T_BE_BUF_RELAY_02 — DecrementBuffer calls relay, not event directly
- **Method under test**: `PttGlobalBreakEven.DecrementBuffer`
- **Scenario**: Call `DecrementBuffer()` with injected relay counter spy
- **Expected**: Relay (`RaiseBeBufferChanged`) called exactly once with `newValue = _globalBeBuffer`

### T_BE_BUF_RELAY_03 — Buffer clamped at bounds: no relay call when at limit
- **Method under test**: `PttGlobalBreakEven.IncrementBuffer`, `DecrementBuffer`
- **Scenario**: `_globalBeBuffer = 10`, call `IncrementBuffer()` → buffer stays 10.
  `_globalBeBuffer = -10`, call `DecrementBuffer()` → buffer stays -10.
- **Expected**: Relay still called (relay unconditional after clamp guard); buffer value unchanged

### T_QA_EXEC_01 — GlobalQuickAllT1 default is 4
- **Method under test**: `CopyEngine.GlobalQuickAllT1` (property)
- **Scenario**: Fresh `CopyEngine` instance (or mock), read `GlobalQuickAllT1`
- **Expected**: returns `4`

### T_QA_EXEC_02 — IncrementQuickAll increments and broadcasts
- **Method under test**: `CopyEngine.IncrementQuickAll`
- **Scenario**: Start at default (4), call `IncrementQuickAll()`
- **Expected**: `GlobalQuickAllT1 == 5`; `GlobalQuickAllBufferChanged` event fires with `5`

### T_QA_EXEC_03 — DecrementQuickAll decrements and broadcasts
- **Method under test**: `CopyEngine.DecrementQuickAll`
- **Scenario**: `_globalQuickAllT1 = 4`, call `DecrementQuickAll()`
- **Expected**: `GlobalQuickAllT1 == 3`; `GlobalQuickAllBufferChanged` event fires with `3`

### T_QX_T3_01 — targetCount from snapshot when snapshot.Count = 3
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: 3-element targets list, t1=4, pos.Quantity=3
- **Expected**: loop iterates 3 times (3 OCO pairs submitted)

### T_QX_T3_02 — targetCount fallback to 2 when snapshot is empty
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: empty targets list, pos.Quantity=2
- **Expected**: loop iterates 2 times

### T_QX_T3_03 — Proportional tick spacing: TN = entry ± t1*N*tick
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: long entry=5000, t1=4, tick=0.25, i=0: T1 price = 5001.00; i=1: T2 = 5002.00; i=2: T3 = 5003.00
- **Expected**: `tNPrice` values match formula for each i

### T_QX_T3_04 — Quantity from snapshot per-target when available
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: targets = [(5001, 2), (5002, 1)]; i=0 qty=2, i=1 qty=1
- **Expected**: `tNQty` equals `targets[i].Qty` for each i

### T_QX_T3_05 — Quantity fallback: evenly split when no snapshot
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: empty targets, pos.Quantity=4, targetCount=2
- **Expected**: `tNQty = max(1, 4/2) = 2` for both brackets

### T_QX_T3_06 — Independent OCO IDs per pair
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: 2-element targets list; capture OCO IDs per iteration
- **Expected**: ocoId for i=0 != ocoId for i=1

### T_QX_T3_07 — Stop and target names follow PTT-QX-* convention
- **Method under test**: `PttQuickExit.Execute` (primary)
- **Scenario**: 3-element targets list
- **Expected**: stop names = "PTT-QX-Stop", "PTT-QX-Stop2", "PTT-QX-Stop3";
  target names = "PTT-QX-T1", "PTT-QX-T2", "PTT-QX-T3"

### T_QX_T3_08 — Compat overload bridges to N-bracket Execute with empty list
- **Method under test**: `PttQuickExit.Execute` (compat overload)
- **Scenario**: Call `Execute(acc, instr, t1=4, t2=8)` on flat account, verify delegation
- **Expected**: primary `Execute` called with `targets.Count == 0`; targetCount fallback = 2

### T_QX_T3_09 — SnapshotTargetOrders captures ATM + PTT-QX-T + PTT-BE-Target names
- **Method under test**: `PttGlobalQuickExit.SnapshotTargetOrders`
- **Scenario**: account with `Target1` (Working, Limit), `PTT-QX-T1` (Accepted, Limit),
  `PTT-BE-Target-1` (Working, Limit), `Stop1` (Working, StopMarket)
- **Expected**: result contains 3 entries (Target1, PTT-QX-T1, PTT-BE-Target-1); Stop1 excluded

### T_SNAP_STOP_01 — SnapshotStopPrice returns 0 when Instrument objects differ but FullName matches
- **Method under test**: `PttQuickExit.SnapshotStopPrice` (pre-fix behavior check — verifies fix)
- **Scenario**: order has `o.Instrument.FullName == instr.FullName` but `o.Instrument != instr` (different objects)
- **Expected**: stop price IS returned (FullName match = include); pre-fix would have returned 0

### T_SNAP_STOP_02 — SnapshotStopPrice returns stop price for Working StopMarket
- **Method under test**: `PttQuickExit.SnapshotStopPrice`
- **Scenario**: order `OrderState.Working`, `OrderType.StopMarket`, `FullName` match
- **Expected**: `StopPrice` value returned

### T_SNAP_STOP_03 — SnapshotStopPrice returns stop price for Accepted StopMarket
- **Method under test**: `PttQuickExit.SnapshotStopPrice`
- **Scenario**: order `OrderState.Accepted`, `OrderType.StopMarket`, `FullName` match
- **Expected**: `StopPrice` value returned

### T_SNAP_STOP_04 — SnapshotStopPrice returns 0 when o.Instrument is null
- **Method under test**: `PttQuickExit.SnapshotStopPrice`
- **Scenario**: order with `o.Instrument == null`
- **Expected**: order skipped (no NRE), returns `0.0`

---

## Section 6: JS-DNA Compliance Summary

| Hotfix | JS-021 no lock | JS-001 no throw | JS-002 no return null | JS-033 no async void | CYC ≤ 8 |
|--------|---------------|-----------------|----------------------|---------------------|---------|
| B74-C-01 (IncrementBuffer / DecrementBuffer) | ✅ volatile int | ✅ | ✅ void | ✅ | ✅ CYC=2 |
| B74-C-02 (RaiseBeBufferChanged) | ✅ Dispatcher.InvokeAsync | ✅ | ✅ no return | ✅ | ✅ CYC=1 |
| B74-C-03 (GlobalQuickAllT1 + ResolveQuickTicks) | ✅ volatile int + Dispatcher.InvokeAsync | ✅ | ✅ returns tuple | ✅ | ✅ CYC=2 max |
| B74-C-04 (N-bracket Execute + SnapshotTargetOrders) | ✅ NextQxOcoId via Interlocked | ✅ caught+logged | ✅ returns list/void | ✅ | ✅ CYC=8 max |
| B74-C-05 (SnapshotStopPrice FullName) | ✅ | ✅ | ✅ returns double 0.0 | ✅ | ✅ CYC=2 |

**Additional NT8-specific compliance**:
- NT8-003 (`volatile double` banned): ✅ Not used; all volatile fields are `int`
- NT8-007 (`(CustomOrder)null` cast): ✅ Present in all `CreateOrder` calls in B74-C-04
- NT8-013 (`DateTime.MaxValue` for GTC): ✅ Present in all `CreateOrder` calls in B74-C-04
- NT8-014 (PTT- prefix on all order names): ✅ `PTT-QX-Stop`, `PTT-QX-T1` etc.
- NT8-049 (CreateOrder arg order: StopMarket arg6=0/arg7=stop; Limit arg6=price/arg7=0): ✅

---

## Section 7: Deferred Items

### Carry-forward from B66-LaneC (all remain OPEN, no change in B74-LaneC)

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B66-C-02 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit entries | P1 | OPEN |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on Quick Exit — Director confirmation | P1 | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | OPEN |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | OPEN (blocked) |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1449-1450 | P2 | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | OPEN |

### New deferred items opened by B74-LaneC

None. All 5 hotfixes are complete, self-contained fixes. No new deferred work is introduced.

### Note on DW-B58-01 (SnapshotTargetsPublic hardcoded prefixes)

B74-C-04 adds `PTT-BE-Target-` as a recognized target prefix in `PttGlobalQuickExit.SnapshotTargetOrders`.
`SnapshotTargetsPublic` (separate method in CopyEngine.cs, B58 scope) was **not** modified in this block.
If `SnapshotTargetsPublic` is used in a future block to snapshot the same targets, it will require the
`PTT-BE-Target-` prefix addition as well. DW-B58-01 remains OPEN and this observation is noted for the
next block that touches `SnapshotTargetsPublic`.
