# B33-LaneA Session Handoff
# Status: PHASE-1b-COMPLETE — DW-B33-01 + BUG-B33-02 + BUG-B33-03 ALL IMPLEMENTED — 54/54 VERIFIED
# Date: 2026-07-21
# Authored by: ptt-orchestrator

---

## 1. Phase 1 — VERIFIED COMPLETE

### Build tag in source
```
internal const string Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-20";
```
Located: `CopyEngine.cs` line 41.

### Verification result
50/50 checklist items passed (03-validation-report.md).
All NT8-049 bugs fixed. SubmitBeStop + OrphanCancelGuard in source and verified.

### Live test result (2026-07-21)
- PTT-BE-Stop Sell 13 @ 7541 submitted to Sim101 (Long). Filled @ 7541.25. Position flat. ✅
- Core new-stop approach confirmed working on NT8 internal sim.

---

## 2. Phase 1b — COMPLETE (2 P0 Bugs Fixed)

### BUG-B33-02 — ATM brackets not cancelled after PTT-BE fill (sim only)
- **Symptom:** After PTT-BE-Stop fills, Target1/Target2/Stop1/Stop2 remain Working.
  Position is flat but brackets stay live → unwanted reverse position risk.
- **Root cause:** NT8 internal sim accounts (Sim101/Sim102) do NOT auto-cancel ATM
  brackets when position goes flat. Real brokers do auto-cancel. Must cancel explicitly.
- **Fix:** New `CancelStaleBrackets(Account, Instrument)` helper (~14 lines).
  Called from `TryFirePositionState` after `OrphanCancelGuard` when `!hasPos`.
  Cancels all Working/Accepted orders for leaderAcc+instr except "PTT-BE-Stop".

### BUG-B33-03 — Second account's BE stop overwrites first (singleton field)
- **Symptom:** Sim102 (Short) BE button = zero output. Nothing submitted.
- **Root cause:** `_pendingBeStop` at line 164 is `private volatile Order` — single field.
  Two simultaneous BE arms (Sim101 + Sim102) → second overwrites first.
  `_pendingBeSlots` (B27, arm/trigger path) is already per-account and fine.
  Only the B33 new-stop path (`_pendingBeStop`) is broken.
- **Fix:** Change `_pendingBeStop` from `volatile Order` to
  `ConcurrentDictionary<string, Order>` keyed on `acc.Name`.
  Update 4 read/write sites in SubmitBeStop + OrphanCancelGuard.
  ArmPendingBe / OnPendingBeAccountUpdate — UNTOUCHED (already per-account via B27).

---

## 3. Exact Diff Plan for Phase 1b

### Change 1 — Field (line 162–164)
```csharp
// BEFORE:
private volatile Order _pendingBeStop = null;

// AFTER:
// B33 BUG-B33-03 fix: per-account dict. JS-021: ConcurrentDictionary is lock-free.
// NT8-003: ConcurrentDictionary provides memory barrier -- no volatile needed.
private readonly ConcurrentDictionary<string, Order> _pendingBeStop
    = new ConcurrentDictionary<string, Order>();
```

### Change 2 — SubmitBeStop duplicate guard (line ~1563)
```csharp
// BEFORE:
if (_pendingBeStop != null && _pendingBeStop.OrderState == OrderState.Working) return;

// AFTER:
if (_pendingBeStop.TryGetValue(leaderAcc.Name, out var existing)
    && existing != null && existing.OrderState == OrderState.Working) return;
```

### Change 3 — SubmitBeStop CreateOrder + Submit (line ~1573)
```csharp
// BEFORE:
_pendingBeStop = leaderAcc.CreateOrder(...);
leaderAcc.Submit(new[] { _pendingBeStop });

// AFTER:
var beStop = leaderAcc.CreateOrder(...);
_pendingBeStop[leaderAcc.Name] = beStop;
leaderAcc.Submit(new[] { beStop });
```

### Change 4 — OrphanCancelGuard null check (line ~1599)
```csharp
// BEFORE:
if (_pendingBeStop == null) return;

// AFTER:
if (!_pendingBeStop.TryGetValue(acc.Name, out var stop) || stop == null) return;
```

### Change 5 — OrphanCancelGuard state guard + clear (lines ~1601–1616)
```csharp
// BEFORE:
if (_pendingBeStop.OrderState != OrderState.Working) { _pendingBeStop = null; return; }
acc.Cancel(new Order[] { _pendingBeStop });
_pendingBeStop = null;

// AFTER:
if (stop.OrderState != OrderState.Working) { _pendingBeStop.TryRemove(acc.Name, out _); return; }
acc.Cancel(new Order[] { stop });
_pendingBeStop.TryRemove(acc.Name, out _);
```

### Change 6 — New CancelStaleBrackets method (insert after OrphanCancelGuard, before BreakEven(Instrument,int) at line 1619)
```csharp
// B33 BUG-B33-02: cancel ATM bracket orders that remain Working after PTT-BE fills.
// NT8 internal sim (Sim101/Sim102) does NOT auto-cancel ATM brackets on position close.
// JS-021: no lock. CYC=3: null guard(1), Where filter(2), Count==0 guard(3).
private void CancelStaleBrackets(Account leaderAcc, Instrument instr)
{
    if (leaderAcc == null || instr == null) return;                              // (1)
    var stale = leaderAcc.Orders
        .Where(o => o.Instrument?.FullName == instr.FullName                     // (2)
                 && (o.OrderState == OrderState.Working
                     || o.OrderState == OrderState.Accepted)
                 && o.Name != "PTT-BE-Stop")
        .ToList();
    if (stale.Count == 0) return;                                                // (3)
    leaderAcc.Cancel(stale);
    NinjaTrader.Code.Output.Process(
        "[BE] CancelStaleBrackets: cancelled " + stale.Count + " bracket orders",
        PrintTo.OutputTab1);
}
```

### Change 7 — TryFirePositionState hook (line ~739–741)
```csharp
// BEFORE:
if (!hasPos)
    OrphanCancelGuard(e.Order.Account, e.Order.Instrument);

// AFTER:
if (!hasPos)
{
    OrphanCancelGuard(e.Order.Account, e.Order.Instrument);
    CancelStaleBrackets(e.Order.Account, e.Order.Instrument);
}
```

### Change 8 — Build tag (line 41)
```csharp
// BEFORE:
internal const string Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-20";

// AFTER:
internal const string Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-21";
```

### Change 9 — Test update (CopyEngineTests.cs ~line 2754)
`PendingBeStop_FieldExists_And_InitialValueIsNull` must be updated:
- Old: checks `typeof(Order)` field type
- New: checks `typeof(ConcurrentDictionary<string, Order>)` field type (or verify field name exists + is ConcurrentDictionary)

---

## 4. Impact
| File | Changes | Lines |
|---|---|---|
| CopyEngine.cs | Changes 1–8 above | ~26 lines total |
| CopyEngineTests.cs | Change 9 | ~3 lines |
| TradeCopierPanel.cs | None | 0 |
| TradeCopierWindow.cs | None | 0 |

---

## 5. Source State at Handoff
| File | Line | State |
|---|---|---|
| `CopyEngine.cs:41` | Build tag | `"PTT-COPIER B33 | new-stop BE | 2026-07-20"` — to update to 2026-07-21 |
| `CopyEngine.cs:164` | `_pendingBeStop` | `private volatile Order _pendingBeStop = null` — needs dict conversion |
| `CopyEngine.cs:1555` | `SubmitBeStop` | 3-param, NT8-049 fixed, needs 2 sites updated (guard + assign) |
| `CopyEngine.cs:1597` | `OrphanCancelGuard` | needs 3 sites updated |
| `CopyEngine.cs:740` | TryFirePositionState hook | OrphanCancelGuard call present — needs CancelStaleBrackets added |
| `CopyEngine.cs:1619` | After OrphanCancelGuard closes | Insert CancelStaleBrackets new method here |

Hard links: PASS (CopyEngine.cs hard-linked to NT8 AddOns dir — verify after any edit)

---

## 6. Test Procedure (Phase 1b)
1. Force recompile — confirm Output: `PTT-COPIER B33 | new-stop BE | 2026-07-21`
2. Open two charts: one Long on Sim101, one Short on Sim102
3. Click BE on Sim101 while long → arm
4. Click BE on Sim102 while short → arm independently
5. Output must show TWO `[BE] SubmitBeStop` lines — one per account
6. Sim101 fills → Output: `[BE] CancelStaleBrackets: cancelled N bracket orders`
7. Sim102 fills → same bracket cancel confirmation
8. Verify Orders tab empty for both accounts after fill
9. Orphan test: arm both, manually flatten one → OrphanCancelGuard for that account only; other account still armed

---

## 7. Open Defect Register
| ID | Description | Status |
|----|-------------|--------|
| BUG-B33-02 | ATM brackets not cancelled after PTT-BE fill on sim accounts | CLOSED — CancelStaleBrackets added in Phase 1b |
| BUG-B33-03 | `_pendingBeStop` single field breaks multi-account BE | CLOSED — ConcurrentDictionary in Phase 1b |

---

## 8. NT8 Rules Updated This Block
| Rule | Content |
|---|---|
| NT8-048 | Native "Breakeven ATM strategy" hotkey in Tools → KB Shortcuts → Order Entry. Instant, no arm. |
| NT8-049 | CreateOrder arg order: arg6=limitPrice, arg7=stopPrice. qty from position, not parameter. Leader acc only. |

`nt8-rules(B33-Phase1): NT8-048 and NT8-049 added`

| NT8-050 | Account.Positions[Instrument] is CS1503 -- use FindPosition(acc, instr) instead. |
| NT8-051 | NT8 sim (Sim101/Sim102) does NOT auto-cancel ATM brackets after position flat. CancelStaleBrackets handles this. |

`nt8-rules(B33): NT8-048, NT8-049, NT8-050, NT8-051 added`

---

## 9. Phase 1b Final Summary

### Build tag in source
```
internal const string Tag = "PTT-COPIER B33 | 1b-dict-BE | 2026-07-21";
```
Located: CopyEngine.cs line 41.

### Verification result
54/54 checklist items passed (03-validation-report-1b.md).
- V1-V12: all CopyEngine.cs source checks PASS
- T1-T6: all CopyEngineTests.cs test checks PASS
- NT8-C1 through NT8-C7: all NT8 compiler checks PASS

### Methods added/changed in Phase 1b
- `_pendingBeStop` field: volatile Order -> ConcurrentDictionary<string,Order> (line 165)
- `SubmitBeStop` duplicate guard + assign sites updated for dict (lines 1568, 1579-1587)
- `OrphanCancelGuard` null check + TryRemove at all 3 sites (lines 1606-1623)
- `CancelStaleBrackets(Account, Instrument)` new method (line 1631)
- TryFirePositionState hook expanded to call CancelStaleBrackets (line 745)

### Ready for Director SIM test
Test procedure: Section 6 of this handoff (Phase 1b test procedure).
Two-account test: Sim101 Long + Sim102 Short simultaneously armed -- both must fire independently.
