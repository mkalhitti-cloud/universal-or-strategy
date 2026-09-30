# B76-LaneA -- Tickets
# Ph3 ptt-architect output

**Block**: B76-LaneA
**Date**: 2026-08-18
**Author**: ptt-architect (Ph3)
**Gate prerequisite**: 02-plan-review.md REVIEW_PASS ✅

**Status summary**:
- TICKET-B76-1 and TICKET-B76-2: code already APPLIED live. Tickets = tests only + verification.
- TICKET-B76-3: code NOT YET APPLIED. Ticket = code change + tests.

---

## TICKET-B76-1 — FlattenOneAccount: in-flight guard + race guard

**Title**: Verify FlattenOneAccount FLATTEN-GUARD-01 v2 + FLATTEN-RACE-01 as-applied
**Priority**: P1
**Code already applied**: YES (live-verified 2026-08-18 12:48 PM)
**Files**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount` (line ~1878)

### What is already in the code (confirm by reading)

The current `FlattenOneAccount` body at lines 1878-1932 contains:

1. **In-flight order-book guard** (HOTFIX-B76-FLATTEN-GUARD-01 v2, lines 1888-1898):
```csharp
foreach (var o in acc.Orders.ToList())
{
    if (o.Name != "PTT-Flatten") continue;
    if (o.Instrument?.FullName != instrument.FullName) continue;
    if (o.OrderState == OrderState.Submitted
        || o.OrderState == OrderState.Accepted
        || o.OrderState == OrderState.Working)
    {
        StatusUpdate?.Invoke(acc.Name + ": flat-guard: in-flight skip");
        return;
    }
}
```

2. **Post-cancel re-read** (HOTFIX-B76-FLATTEN-RACE-01, lines 1907-1913):
```csharp
var posAfterCancel = FindPosition(acc, instrument);
if (posAfterCancel == null || posAfterCancel.Quantity == 0)
{
    StatusUpdate?.Invoke(acc.Name + ": flat-race skip (pos cleared by bracket fill)");
    return;
}
```

3. Header comment says `CYC=6`.
4. `CreateOrder` uses `posAfterCancel.Quantity` and `posAfterCancel.MarketPosition`.

### Tests to write in `src/PropTraderTools/Tests/B76Tests.cs`

```
T_B76_01: FlattenOneAccount method exists as non-public instance on CopyEngine (reflection).
T_B76_02: FlattenOneAccount body contains string literal "flat-guard: in-flight skip".
T_B76_03: FlattenOneAccount body contains string literal "flat-race skip".
T_B76_04: FlattenOneAccount IL contains at least 2 FindPosition call sites.
T_B76_05: FlattenOneAccount IL: CancelAllAccountOrders call offset < second FindPosition call offset.
T_B76_06: FlattenOneAccount IL: at least 5 local variables (pos, posAfterCancel, action, order, loop var o).
```

All via `typeof(CopyEngine).GetMethod("FlattenOneAccount", BindingFlags.NonPublic | BindingFlags.Instance)`.
Use same IL byte-scan pattern as T_B67_01..T_B67_04 in `CopyEngineTests.cs` (precedent).

### Acceptance criteria
- Build passes. T_B76_01..T_B76_06 all pass.
- T_B67_01..T_B67_04 regression tests still pass.
- Write `docs/brain/B76-LaneA/ticket-1-completion.md`.

---

## TICKET-B76-2 — PositionStateChanged dedup + leak fixes

**Title**: Verify POSSTATE-DEDUP-01 + POSSTATE-LEAK-01 + POSSTATE-LEAK-02 as-applied
**Priority**: P1
**Code already applied**: YES (live-verified 2026-08-18)
**Files**:
  - `src/PropTraderTools/CopyEngine.cs` (TryFirePositionState + _lastHasPos field)
  - `src/PropTraderTools/TradeCopierAddOn.cs` (DoInject stale panel removal)
  - `src/PropTraderTools/TradeCopierWindow.cs` (OnLoaded idempotency)

### What is already in the code (confirm by reading)

**CopyEngine.cs lines 181-188** (_lastHasPos field):
```csharp
private readonly ConcurrentDictionary<string, int[]> _lastHasPos
    = new ConcurrentDictionary<string, int[]>();
```

**CopyEngine.cs TryFirePositionState (lines 1418-1444)**: Interlocked.Exchange CAS.
```csharp
int newVal = hasPos ? 1 : 0;
var box    = _lastHasPos.GetOrAdd(instr, _ => new int[] { 2 });
int prior  = System.Threading.Interlocked.Exchange(ref box[0], newVal);
if (prior == newVal) return;
```

**TradeCopierAddOn.cs DoInject**: stale panel cast + Detach() call before grid child removal.
**TradeCopierWindow.cs OnLoaded**: `_engine.Unsubscribe()` as first call in try block.

### Tests to write in `src/PropTraderTools/Tests/B76Tests.cs`

```
T_B76_07: CopyEngine has field _lastHasPos of type ConcurrentDictionary<string,int[]> (reflection).
T_B76_08: TryFirePositionState method body contains string reference to Interlocked.Exchange or
          _lastHasPos (IL call site for System.Threading.Interlocked.Exchange exists in method IL).
T_B76_09: TryFirePositionState is private instance method on CopyEngine (accessibility check).
```

### Acceptance criteria
- Build passes. T_B76_07..T_B76_09 all pass.
- Write `docs/brain/B76-LaneA/ticket-2-completion.md`.

---

## TICKET-B76-3 — GetLeaderAtmTemplateName class-name guard

**Title**: Apply HOTFIX-B76-ATM-TPL-CLASSNAME to GetLeaderAtmTemplateName + write tests
**Priority**: P2
**Code already applied**: NO — apply in this ticket.
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method**: `GetLeaderAtmTemplateName(Chart currentChart)` (line 2218)

### Exact change to apply

Current lines 2227-2228:
```csharp
if (ct.AtmStrategy != null)                                  // branch 3 -- primary path
    return ct.AtmStrategy.Name ?? string.Empty;
```

Replace with:
```csharp
if (ct.AtmStrategy != null)                                  // branch 3 -- primary path
{
    var n = ct.AtmStrategy.Name ?? string.Empty;
    // B76 HOTFIX-B76-ATM-TPL-CLASSNAME: "AtmStrategy" is the NT8 class name returned when
    // no template is staged on ChartTrader -- not a user template name.
    // Observed live 2026-08-18: [PTT-CLONE] SetCloneAtmCache: 'AtmStrategy' (empty=False).
    // Fall through to AtmStrategySelector fallback to get the real template name.
    if (n.Length > 0 && n != "AtmStrategy")
        return n;
}
```

Also update the header comment block (lines 2210-2217). Add one line after the
`Primary: ct.AtmStrategy?.Name` doc line:
```
//   Class-name guard: if .Name == "AtmStrategy" (NT8 internal class, no template staged),
//   fall through to Fallback-1 selector. Observed 2026-08-18 session.
```
CYC unchanged (5 → strict 7, still ≤8).

### Tests to write in `src/PropTraderTools/Tests/B76Tests.cs`

```
T_B76_10: GetLeaderAtmTemplateName(null) returns string.Empty (regression guard).
T_B76_11: GetLeaderAtmTemplateName method body contains string literal comparison for "AtmStrategy"
          (confirms guard is present in compiled code — reflection body check).
T_B76_12: GetLeaderAtmTemplateName is internal static on TradeCopierPanel (accessibility check).
```

T_B76_10: `typeof(TradeCopierPanel).GetMethod("GetLeaderAtmTemplateName",
  BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, new object[] { null })` returns `""`.

### Acceptance criteria
- `apply_diff` used (not `write_file`) — surgical change only.
- Build passes. T_B76_10..T_B76_12 all pass.
- Existing T_B43_04 (null chart -> string.Empty) still passes.
- Existing T_B66TPL_01..05 still pass (if present).
- Write `docs/brain/B76-LaneA/ticket-3-completion.md`.

---

## Ticket Execution Order

1. TICKET-B76-1: Read FlattenOneAccount, write T_B76_01..T_B76_06, run tests.
2. TICKET-B76-2: Read TryFirePositionState + _lastHasPos, write T_B76_07..T_B76_09, run tests.
3. TICKET-B76-3: Apply class-name guard to GetLeaderAtmTemplateName, write T_B76_10..T_B76_12, run tests.
4. After all 3: run `dotnet test` (all 12 tests must pass).
5. Run `powershell -File scripts\sync-ptt-to-nt8.ps1`.
6. Write all 3 completion files.
