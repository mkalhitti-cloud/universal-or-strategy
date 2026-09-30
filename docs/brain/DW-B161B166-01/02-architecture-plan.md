# Architecture Plan — DW-B161B166-01 (VIOLATION-01-FIX)

**Epic:** DW-B161B166-01
**Phase:** 1 — Architecture
**Author:** ptt-architect
**Date:** 2026-09-15
**Status:** PLAN_READY

---

## 1. Problem Statement

`IsFollowerPositionBlocked` in `src/PropTraderTools/CopyEngine.cs` (L3182–L3241) has
McCabe cyclomatic complexity CYC=9, exceeding the JS-CYC budget of 8 by exactly 1.

Root cause: B166 added Signal 6 (`if (IsBeRetryArmedFlat(acc, instr))` at L3231) as a
ninth `if`-branch directly in the parent body. B165 had brought CYC to exactly 8. B166
pushed it to 9.

Additionally, the inline comment at L3178 reads `CYC=8` — a stale annotation written
before B166 and must be corrected as part of this fix.

---

## 2. Deferred Backlog Closure

This epic closes **DW-B161B166-01 (OPEN, P1)** from
`docs/brain/B161-B166/06-deferred-backlog.md`.

No other deferred items (DW-B161B166-02, DW-B161B166-03) are in scope.

---

## 3. Lane-Split Gate

| Question | Answer |
|---|---|
| Touches more than one .cs file? | NO |
| Requires a new file? | NO |
| Touches any interface or public contract? | NO |
| Adds any new field, property, or class member beyond the helper? | NO |
| Affects any test file signature? | NO |
| Multiple pipelines needed? | NO |

**Result: SINGLE-PIPELINE** — all changes in one file, one class, one extraction.

---

## 4. Component Inventory

### 4.1 File in scope

`src/PropTraderTools/CopyEngine.cs`

### 4.2 Methods touched

| Method | Action | Approximate Line |
|---|---|---|
| `IsFollowerPositionBlocked` | Modify: replace Signal5+6 blocks with single delegating call; update CYC comment | L3178–L3241 |
| `IsBeSettleOrRetrySignalActive` *(new)* | Insert immediately before `IsFollowerPositionBlocked` | Before L3182 |

### 4.3 Methods NOT touched

| Method | Reason |
|---|---|
| `IsBeSlotNonTerminal` (L9382) | Callee only — called from new helper, definition unchanged |
| `IsBeRetryArmedFlat` (L3253) | Callee only — called from new helper, definition unchanged |
| `ShouldSkipFollower` (L3261) | Calls `IsFollowerPositionBlocked` — signature unchanged, no update needed |
| `TryReplaceOnAtmCancel` (L947) | Calls `IsFollowerPositionBlocked` — signature unchanged, no update needed |

---

## 5. Method Specifications

### 5.1 New Helper: `IsBeSettleOrRetrySignalActive`

```
Signature:  private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
Visibility: private
Location:   Immediately before IsFollowerPositionBlocked (before L3182)
```

**Body (exact):**

```csharp
// DW-B161B166-01 VIOLATION-01-FIX: extracted Signals 5+6 from IsFollowerPositionBlocked
// to restore parent CYC to 8. Each signal emits its own reason-tagged log before returning.
// CYC=4 (project statement-level convention): base(1) + null-guard(2) + IsBeSlotNonTerminal(3) + IsBeRetryArmedFlat(4).
// CYC=5 (McCabe strict, counting ||): base(1) + if(2) + ||(3) + IsBeSlotNonTerminal(4) + IsBeRetryArmedFlat(5).
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
// private: not called externally; same visibility as IsBeRetryArmedFlat.
private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
{
    if (acc == null || instr == null) return false;    // (2)
    if (IsBeSlotNonTerminal(acc.Name))                 // (3) Signal 5
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    if (IsBeRetryArmedFlat(acc, instr))                // (4) Signal 6
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    return false;
}
```

### 5.2 Modified Parent: `IsFollowerPositionBlocked`

**Change 1 — Update stale CYC comment (L3178):**

```
REMOVE:
// CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1-branch(4) + Signal2-branch(5) + Signal3-branch(6) + Signal4-branch(7) + Signal5-branch(8). Signal 6 delegated.

REPLACE WITH:
// CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1(4) + Signal2(5)
//        + Signal3(6) + Signal4(7) + IsBeSettleOrRetrySignalActive(8). Signals 5+6 delegated.
//        DW-B161B166-01: was CYC=9 after B166 direct Signal6 add. Extraction restores budget.
```

**Change 2 — Replace Signal 5 + Signal 6 blocks (L3222–L3239):**

```
REMOVE (L3222–L3239):
    if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5 -- BE settle window (B165)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }
    if (IsBeRetryArmedFlat(acc, instr))    // (9) Signal 6 -- post-fallback-BE window (B166)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                + acc.Name + "\n" + instr.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        return true;
    }

REPLACE WITH:
    if (IsBeSettleOrRetrySignalActive(acc, instr))  // (8) Signals 5+6 -- BE settle/retry window (DW-B161B166-01)
        return true;
```

---

## 6. CYC Analysis

### 6.1 `IsFollowerPositionBlocked` — CYC transition

McCabe decision points (project statement-level convention — each `if` = 1 point):

| Branch | Label | Before B166 | After B166 | After fix |
|---|---|---|---|---|
| base | (1) | 1 | 1 | 1 |
| `if (acc == null)` | (2) | +1 | +1 | +1 |
| `if (instr == null)` | (3) | +1 | +1 | +1 |
| `if (HasOpenPosition)` | Signal 1 (4) | +1 | +1 | +1 |
| `if (HasActivePttBeExits)` | Signal 2 (5) | +1 | +1 | +1 |
| `if (HasSettlingBeTargets)` | Signal 3 (6) | +1 | +1 | +1 |
| `if (HasActivePttBeTargets)` | Signal 4 (7) | +1 | +1 | +1 |
| `if (IsBeSlotNonTerminal)` | Signal 5 (8) | +1 | +1 | — (extracted) |
| `if (IsBeRetryArmedFlat)` | Signal 6 (9) | — | +1 | — (extracted) |
| `if (IsBeSettleOrRetrySignalActive)` | Signals 5+6 (8) | — | — | +1 |
| **Total CYC** | | **8** | **9** | **8** |

**CYC: 9 → 8. Budget restored.** ✓

### 6.2 `IsBeSettleOrRetrySignalActive` — new helper CYC

**Project convention (statement-level, one decision per `if`):**

| Branch | Count |
|---|---|
| base | 1 |
| `if (acc == null \|\| instr == null)` — treated as one `if` | +1 = 2 |
| `if (IsBeSlotNonTerminal(acc.Name))` | +1 = 3 |
| `if (IsBeRetryArmedFlat(acc, instr))` | +1 = 4 |
| **CYC (project convention)** | **4** |

**McCabe strict (counting `||` as +1 per short-circuit branch):**

| Branch | Count |
|---|---|
| base | 1 |
| `if (...)` predicate node | +1 = 2 |
| `\|\|` short-circuit | +1 = 3 |
| `if (IsBeSlotNonTerminal)` | +1 = 4 |
| `if (IsBeRetryArmedFlat)` | +1 = 5 |
| **CYC (strict)** | **5** |

Both counts are within the budget of 8. The project's inline comment will use CYC=4
(consistent with statement-level convention used by IsBeRetryArmedFlat's comment at L3252
which says "CYC=2: base(1) + && short-circuit(1)" — noting the && explicitly, which means
the project does count short-circuit operators separately).

**Revised CYC count for helper using project convention (counts || explicitly):**

| Branch | Count |
|---|---|
| base | 1 |
| `if (acc == null \|\| instr == null)` — the `if` node | +1 = 2 |
| `\|\|` short-circuit node | +1 = 3 |
| `if (IsBeSlotNonTerminal(acc.Name))` | +1 = 4 |
| `if (IsBeRetryArmedFlat(acc, instr))` | +1 = 5 |
| **CYC** | **5** |

Wait — cross-checking with L3252: `IsBeRetryArmedFlat` is an expression-body lambda:
`private bool IsBeRetryArmedFlat(Account acc, Instrument instr) => IsFlat(...) && _bePriceRetryAttempts.ContainsKey(...);`
Its comment says "CYC=2: base(1) + && short-circuit(1)". This confirms the project **does** count `&&` and `||` as +1 each.

**Final settled count for `IsBeSettleOrRetrySignalActive`:**
`if (acc == null || instr == null)` → the `if` = +1, the `||` = +1 → **CYC = 5**

The inline comment in the helper must say CYC=5, not CYC=4. The task description's CYC=4 used the simpler convention. The plan authoritative count is **CYC=5** (consistent with IsBeRetryArmedFlat's own comment at L3252).

Both CYC=5 (helper) and CYC=8 (parent after) are within the 8-branch budget. ✓

---

## 7. Null-Guard Sufficiency

Guard: `if (acc == null || instr == null) return false;`

Downstream calls in helper:
- `IsBeSlotNonTerminal(acc.Name)` — requires `acc != null` to safely read `.Name`. Guard ensures acc non-null. ✓
- `IsBeRetryArmedFlat(acc, instr)` — requires both `acc != null` and `instr != null`. Guard ensures both. ✓

One compound guard covers both downstream method calls. The `||` short-circuits: if acc is null, instr is not evaluated.

**Note:** The parent `IsFollowerPositionBlocked` has its own separate null guards at L3184-L3185
(`if (acc == null) return false; if (instr == null) return false;`). In practice, the helper's
null guard is a defensive belt-and-suspenders check — the parent will always pass non-null args
in normal flow. The guard is retained for correctness and to make the helper safe if ever called
independently in a test harness.

---

## 8. Log String Preservation

| Signal | Log text (exact) | PrintTo | Source line | Helper line |
|---|---|---|---|---|
| Signal 5 | `[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): ` + acc.Name + `\n` + instr.FullName | OutputTab1 | L3224–3228 | Preserved verbatim in helper |
| Signal 6 | `[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): ` + acc.Name + `\n` + instr.FullName | OutputTab1 | L3233–3237 | Preserved verbatim in helper |

All log strings are ASCII-only. No Unicode, no curly quotes. JS-042 satisfied. ✓

---

## 9. Call Site Impact

### `IsFollowerPositionBlocked` call sites (unchanged)

| Site | File | Line | Change needed |
|---|---|---|---|
| `TryReplaceOnAtmCancel` | CopyEngine.cs | L957 | None — signature unchanged |
| `ShouldSkipFollower` | CopyEngine.cs | L3270 | None — signature unchanged |

Signature `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)` is preserved exactly. ✓

### `IsBeSlotNonTerminal` call sites (unchanged)

| Site | File | Line | Change needed |
|---|---|---|---|
| Signal 5 block (being extracted) | CopyEngine.cs | L3222 | Moved to helper — no external caller affected |
| Definition | CopyEngine.cs | L9382 | Unchanged |

Zero external callers outside the signal block being moved. ✓

### `IsBeRetryArmedFlat` call sites (unchanged)

| Site | File | Line | Change needed |
|---|---|---|---|
| Signal 6 block (being extracted) | CopyEngine.cs | L3231 | Moved to helper — no external caller affected |
| Definition | CopyEngine.cs | L3253 | Unchanged |

Zero external callers outside the signal block being moved. ✓

---

## 10. Jane Street Rule Compliance

| Rule | Description | Status |
|---|---|---|
| JS-001 | No throw statements in non-exceptional paths | PASS — no throw in helper |
| JS-002 | No null return from non-void | PASS — helper returns bool, base case is `false` |
| JS-021 | No lock() anywhere | PASS — ConcurrentDictionary reads only, no lock |
| JS-042 | ASCII-only string literals | PASS — all log strings confirmed ASCII |
| JS-CYC | CYC ≤ 8 per method | PASS — parent 9→8, helper=5, both ≤ 8 |

No DateTime.Now. No FontFamily. No hex color literals. No mutable struct fields written. ✓

---

## 11. Threading Model

- `IsBeSettleOrRetrySignalActive` is a synchronous predicate called on the NT8 callback thread.
- It reads `_pendingFollowerBeSlots` (ConcurrentDictionary) via `IsBeSlotNonTerminal` — O(1) lock-free read.
- It reads `_bePriceRetryAttempts` (ConcurrentDictionary) via `IsBeRetryArmedFlat.ContainsKey` — O(1) lock-free read.
- No Dispatcher.InvokeAsync needed — no UI interaction.
- No ConcurrentQueue changes.
- Thread affinity after extraction is identical to before: NT8 callback thread, same lock-free reads.

---

## 12. NT8 API Surface

Zero new NT8 API calls introduced. All API usage in the helper is carried over verbatim
from the existing Signal 5 and Signal 6 blocks in the parent body:

- `NinjaTrader.Code.Output.Process(string, PrintTo)` — existing pattern
- `NinjaTrader.NinjaScript.PrintTo.OutputTab1` — existing enum value
- `Account.Name` (string property) — existing read
- `Instrument.FullName` (string property) — existing read

No AtmStrategyCreate, AtmStrategyChangeStopTarget (StrategyBase-only — banned). ✓
No Account.Change (AddOnBase but silent no-op on ATM brackets). ✓

---

## 13. Test Impact

`IsFollowerPositionBlocked` is `internal` (accessible to xUnit via InternalsVisibleTo).
Existing tests exercising Signal 5 and Signal 6 paths through `IsFollowerPositionBlocked`
continue to work without modification — the parent signature is unchanged and the helper is
called by the parent on those paths.

`IsBeSettleOrRetrySignalActive` is `private` — not directly testable from xUnit. Its
behaviour is covered transitively by any existing test that exercises Signal 5 or Signal 6
through the parent.

**No new tests required. No existing tests require update.**

---

## 14. Summary of Changes

| # | File | Change |
|---|---|---|
| 1 | `src/PropTraderTools/CopyEngine.cs` | Insert `IsBeSettleOrRetrySignalActive` private method immediately before `IsFollowerPositionBlocked` (before L3182) |
| 2 | `src/PropTraderTools/CopyEngine.cs` | In `IsFollowerPositionBlocked`: remove Signal 5 if-block (L3222–3230) and Signal 6 if-block (L3231–3239); replace with single `if (IsBeSettleOrRetrySignalActive(acc, instr)) return true;` |
| 3 | `src/PropTraderTools/CopyEngine.cs` | Update stale CYC comment at L3178 to reflect CYC=8 with correct description |

**Three edits, one file, one class. SINGLE-PIPELINE.**

---

## 15. Deferred Items NOT in this Epic

| ID | Description | Status |
|---|---|---|
| DW-B161B166-02 | Rename `IsPttBeStopActiveState` to `IsPttBeOrderActiveState` | OPEN — P3, future pass |
| DW-B161B166-03 | Add asymmetry comment to `TryTriggerBeRecovery` at L5193 | OPEN — P3, future pass |
| DW-B161B166-04 | B161 original SIM gate superseded | CLOSED |

---

PLAN_READY
