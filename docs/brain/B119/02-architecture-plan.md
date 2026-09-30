# B119 Architecture Plan -- DW-B128 Reversal Entry Guard

**Block**: B119  
**Defect**: DW-B128  
**Status**: REVIEW_PENDING  
**Author**: ptt-architect  
**Date**: 2026-08-27  
**Phase**: 1 -- Architecture Plan

---

## 1. Problem Statement

When a leader account closes a position and immediately opens one in the **opposite direction**
(a reversal, e.g., short->flat->long), CopyEngine calls `DispatchCopy` for the new entry order.
If a follower account is already **flat** at that moment -- having exited its position at a
different time or price than the leader -- `DispatchCopy` dispatches the reversal entry to
that follower anyway, opening an **unwanted position in the reversed direction** on an account
that had no open trade.

### Observed scenario (DW-B128):

```
Leader:   [Short] --> OnOrderUpdate(Close Buy) --> [Flat] --> OnOrderUpdate(Open Buy again)
Follower: [Short] --> closes at earlier time  --> [Flat]
                                                         ^--- DispatchCopy fires Buy here
                                                              Follower is flat: UNWANTED LONG opened
```

The existing DW-B122 guard is race-dependent and does not reliably catch the case where
the follower reaches flat independently before the leader's reversal entry is dispatched.

---

## 2. Root Cause

`DispatchCopy` (CopyEngine.cs ~L1784) has no awareness of **prior leader direction**. It
fires whenever an entry-type order passes Gates 0.5 through 5. The per-follower loop has two
skip guards (null account, daily cap), but neither tests whether the current dispatch
**reverses the direction of the last dispatched signal** while the **follower is already flat**.

The combination of two independent conditions is what causes the harm:
1. The order action changed direction (e.g., Sell->Buy or Buy->Sell).
2. The follower has no open position on that instrument.

Neither condition alone is sufficient to block dispatch. Their conjunction is the guard.

---

## 3. Fix: Option A -- Direction-Change Guard

### 3.1 New Field

**Location**: class-level field block in CopyEngine, alongside other ConcurrentDictionary fields.

```csharp
// B119: DW-B128 -- reversal entry guard.
// Keyed by instrument FullName, value is the last OrderAction dispatched for that instrument.
// ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection
    = new ConcurrentDictionary<string, OrderAction>();
```

**Thread safety**: `ConcurrentDictionary<K,V>` provides atomic reads (`TryGetValue`) and
atomic writes (indexer set). No `lock()` required. JS-021 compliant.

**Key choice**: `instrument.FullName` (e.g., `"NQ 03-26 CME"`) is the canonical identifier
already used as the dictionary key throughout CopyEngine (e.g., L3351, L1649).

**Value type**: `OrderAction` is a C# `enum` -- a value type. Indexer assignment is atomic
on 64-bit CLR. No additional synchronization required.

---

### 3.2 New Helper: IsReversalToFlatFollower

**Location**: Static helper section of CopyEngine, near `IsFlat` (~L3302).

```csharp
// B119: DW-B128 -- direction-change guard predicate.
// Returns true iff the current dispatch reverses the last direction AND the follower is flat.
// CYC=2: (1) direction inequality check, (2) flat check.
// JS-001: no throw. JS-021: no lock. ASCII-only.
// Pure function: no side effects, no I/O, fully unit-testable.
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat)
{
    return currentAction != lastAction && followerIsFlat;
}
```

#### CYC=2 proof

| # | Decision point | Value |
|---|----------------|-------|
| 1 | `currentAction != lastAction` | +1 |
| 2 | `followerIsFlat` (short-circuit AND) | +1 |

Base: 1. Total CYC = 1 + 2 = **3 by some tools**, or **2 by McCabe strict** (one `&&` expression
in a single `return` is 1 branch in McCabe strict counting -- same convention used throughout
CopyEngine, e.g., compound || at L1808 counted as 1). Either way, CYC is at most 3, well
within the ≤8 limit.

**Why `internal static`**: Allows direct access from `src/PropTraderTools/Tests/B119Tests.cs`
without reflection. Pure function: takes 3 value arguments, returns bool, no dependencies
on instance state, no NT8 API calls. Fully testable in isolation.

**Why parameters are pre-computed before the call**:
- `currentAction` = `order.OrderAction` (from the order being dispatched -- same for all followers)
- `lastAction` = result of `TryGetValue` (per instrument, looked up once before the loop)
- `followerIsFlat` = `IsFlat(FindPosition(acc, instr))` (per follower, computed inside the loop)

Passing the pre-computed `bool followerIsFlat` keeps the helper side-effect-free and avoids
passing `Account` and `Instrument` objects into a static predicate.

---

### 3.3 DispatchCopy Modification

**Location**: `DispatchCopy` method, L1784-L1869 in `CopyEngine.cs`.

#### Current branch count (CYC=8 at limit, per comment L1782)

| # | Branch | Line |
|---|--------|------|
| 1 | `if (IsExitSignalName(...))` | L1787 |
| 2 | `if (!IsDispatchTriggerState(...))` | L1791 |
| 3 | `if (!isMarket && !isLimit)` | L1797 |
| 4 | `if (IsDedup(...) \|\| IsEntryDispatched(...))` | L1808 (compound = 1) |
| 5 | `foreach` | L1825 |
| 6 | `if (acc == null)` | L1827 |
| 7 | `if (!PassesDailyCapCheck(acc))` | L1832 |
| 8 | `if (mode is FollowerAtmMode.Named namedAtm)` | L1863 |

**Adding** `if (IsReversalToFlatFollower(...))` naively adds branch 9 = CYC 9. OVER LIMIT.

#### CYC budget solution: merge branches 6 and 7

Branches 6 (`acc == null`) and 7 (`!PassesDailyCapCheck`) are consecutive null-and-cap skip
guards that share the same `idx++; continue;` body. Merging them into a single compound
condition frees one branch slot:

```csharp
// Merged guard (branches 6+7 => 1 compound || = 1 McCabe branch, per project convention)
if (acc == null || !PassesDailyCapCheck(acc))
{
    idx++;
    continue;
}
```

This reduces the in-loop branch count by 1, restoring headroom for the reversal guard.

#### Revised branch count (CYC=8 maintained)

| # | Branch | Note |
|---|--------|------|
| 1 | `if (IsExitSignalName(...))` | unchanged |
| 2 | `if (!IsDispatchTriggerState(...))` | unchanged |
| 3 | `if (!isMarket && !isLimit)` | unchanged |
| 4 | `if (IsDedup(...) \|\| IsEntryDispatched(...))` | unchanged, compound=1 |
| 5 | `foreach` | unchanged |
| 6 | `if (acc == null \|\| !PassesDailyCapCheck(acc))` | merged, compound=1 |
| 7 | `if (hasLastDirection && IsReversalToFlatFollower(...))` | **new guard** |
| 8 | `if (mode is FollowerAtmMode.Named namedAtm)` | unchanged |

**CYC = 8. At limit. Compliant.**

#### Pseudocode for modified DispatchCopy inner section

```
// Before foreach: snapshot current direction and last direction for this instrument
OrderAction currentAction = order.OrderAction;
var instr = order.Instrument;
bool hasLastDirection = _lastLeaderDirection.TryGetValue(instr.FullName, out OrderAction lastAction);

int idx = 0;
foreach (var acc in rule.FollowerAccounts)
{
    // Merged null + cap guard (branches 6 = 1 compound)
    if (acc == null || !PassesDailyCapCheck(acc))
    {
        idx++;
        continue;
    }

    // DW-B128: reversal entry guard (branch 7)
    bool followerIsFlat = IsFlat(FindPosition(acc, instr));
    if (hasLastDirection && IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY-GUARD] skip reversal entry: "
                + acc.Name
                + " "
                + instr.FullName
                + " follower flat",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
        idx++;
        continue;
    }

    // ... existing: GetMultiplier, CopySignal.Create, ResolveAtmMode, Output.Process, SendCopy/SendCopyWithAtm ...
    idx++;
}

// After foreach: update last direction for this instrument
_lastLeaderDirection[instr.FullName] = currentAction;
```

**Key design notes**:
- `TryGetValue` happens **before** the loop (once per dispatch, not per follower).
- `followerIsFlat` is computed **inside** the loop (per-follower state check).
- Dictionary update happens **after** the loop, so the current dispatch's direction is recorded
  once all followers have been processed.
- `hasLastDirection = false` when key is absent (first dispatch ever) → guard cannot fire
  → copy proceeds normally on first entry.

---

### 3.4 Update Sequence

The `_lastLeaderDirection[instr.FullName] = currentAction` write occurs **after the foreach
loop completes**, not inside it. This ensures:

1. All followers see the same `lastAction` for this dispatch event (consistency within one dispatch).
2. The next dispatch event reads the direction that was just dispatched.
3. No partial-update scenario where some followers see the new direction mid-loop.

---

## 4. Invariants Preserved

| Invariant | How preserved |
|-----------|---------------|
| First entry after flat period | `TryGetValue` returns `false` → `hasLastDirection=false` → guard does not fire → copy proceeds normally |
| Same direction repeat | `IsReversalToFlatFollower(Buy, Buy, flat)` → `currentAction != lastAction` is `false` → returns `false` → copy proceeds |
| Direction reversal, follower has open position | `followerIsFlat=false` → `IsReversalToFlatFollower` returns `false` → copy proceeds (follower has open trade in old direction -- this is a close signal, not an errant entry) |
| Direction reversal, follower is flat | Guard fires → `continue` → follower skipped → no unwanted entry |
| Other followers in same dispatch | Each follower is evaluated independently; guard is per-follower, not per-instrument |
| CYC ≤ 8 for DispatchCopy | Branches 6+7 merged into compound; reversal guard takes freed slot; total = 8 |
| CYC ≤ 8 for IsReversalToFlatFollower | 2 decision points → CYC = 2 or 3 by counting convention; both ≤ 8 |
| Thread safety | ConcurrentDictionary: TryGetValue and indexer-set are atomic; no lock() |

---

## 5. JS Rule Compliance

| Rule | Requirement | This Plan |
|------|-------------|-----------|
| JS-021 | No `lock()` | `ConcurrentDictionary` used; zero `lock()` statements. PASS |
| JS-001 | No `throw` in hot path | `IsReversalToFlatFollower` returns `bool`; no throw paths in DispatchCopy changes. PASS |
| JS-002 | No `return null` for missing | No new nullable return sites. `TryGetValue` with `out` param is the correct pattern. PASS |
| CYC ≤ 8 | All methods ≤ 8 branches | DispatchCopy=8 (after merge); IsReversalToFlatFollower=2. PASS |
| ASCII-only | No Unicode in strings or identifiers | `[PTT-COPY-GUARD]` log line is pure ASCII; all identifiers are ASCII. PASS |
| No DateTime.Now | Use DateTime.UtcNow | Not applicable; no DateTime used. PASS |
| PTT- prefix on CreateOrder | All order names prefixed | No `CreateOrder` calls in this change. PASS |
| No FontFamily / hex colors | Not applicable | Not applicable. PASS |

---

## 6. Test Plan

All tests in `src/PropTraderTools/Tests/B119Tests.cs`. Framework: **xUnit only** (JS testing standard).

### Part A: Unit tests for IsReversalToFlatFollower (pure function, no mocks)

| # | [Fact] name | Inputs | Expected |
|---|-------------|--------|----------|
| A1 | `T_IsReversalToFlat_BuyBuy_Flat_ReturnsFalse` | Buy, Buy, flat=true | false -- same direction, not a reversal |
| A2 | `T_IsReversalToFlat_SellSell_Flat_ReturnsFalse` | Sell, Sell, flat=true | false -- same direction |
| A3 | `T_IsReversalToFlat_BuySell_Flat_ReturnsTrue` | Buy, Sell, flat=true | true -- direction changed, follower flat |
| A4 | `T_IsReversalToFlat_SellBuy_Flat_ReturnsTrue` | Sell, Buy, flat=true | true -- direction changed, follower flat |
| A5 | `T_IsReversalToFlat_BuySell_NotFlat_ReturnsFalse` | Buy, Sell, flat=false | false -- direction changed but follower has position |
| A6 | `T_IsReversalToFlat_SellBuy_NotFlat_ReturnsFalse` | Sell, Buy, flat=false | false -- direction changed but follower has position |

### Part B: _lastLeaderDirection dictionary invariants (no NT8 mocks needed)

| # | [Fact] name | Scenario | Expected |
|---|-------------|----------|----------|
| B1 | `T_DirDict_AbsentKey_TryGetValue_ReturnsFalse` | New ConcurrentDictionary, key "NQ 03-26 CME" absent | TryGetValue returns false |
| B2 | `T_DirDict_AfterWrite_KeyPresent_ReturnsBuy` | Write Buy for key "NQ 03-26 CME" | TryGetValue returns true, value=Buy |
| B3 | `T_DirDict_OverwriteUpdatesValue` | Write Buy then Sell for same key | TryGetValue returns Sell |

### Part C: BuyToCover / SellShort direction-change variants

| # | [Fact] name | Inputs | Expected |
|---|-------------|--------|----------|
| C1 | `T_IsReversalToFlat_BuyToCoverToSellShort_Flat_ReturnsTrue` | SellShort, BuyToCover, flat=true | true |
| C2 | `T_IsReversalToFlat_SellShortToBuyToCover_Flat_ReturnsTrue` | BuyToCover, SellShort, flat=true | true |

**Total**: 11 [Fact] tests (6 minimum required; 11 provided).

### Test isolation
- Part A tests call `CopyEngine.IsReversalToFlatFollower` directly (internal static, accessible from test assembly via `InternalsVisibleTo`).
- Part B tests exercise `ConcurrentDictionary<string, OrderAction>` directly -- no CopyEngine instance needed.
- Part C tests are identical in structure to Part A.

No NT8 API calls in any test. No `Account`, `Order`, or `Instrument` objects required.

---

## 7. Files Modified

| File | Change type | Scope |
|------|-------------|-------|
| `src/PropTraderTools/CopyEngine.cs` | Modify | 3 locations: (1) new field ~L250 area (near other ConcurrentDictionary fields); (2) `DispatchCopy` L1825-L1869 (loop guard changes); (3) new static helper `IsReversalToFlatFollower` ~L3302 (near `IsFlat`) |
| `src/PropTraderTools/Tests/B119Tests.cs` | New file | 11 [Fact] tests; zero NT8 mocks |

**Not modified**: Any other `.cs` file, any spec HTML, any panel file.

### Exact insertion points in CopyEngine.cs

1. **New field** (~L250 or wherever other `ConcurrentDictionary` fields are declared):
   ```csharp
   private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection
       = new ConcurrentDictionary<string, OrderAction>();
   ```

2. **DispatchCopy** (L1825-L1868, inside the foreach):
   - Replace separate `if (acc == null)` (L1827-L1831) + `if (!PassesDailyCapCheck(acc))` (L1832-L1836) with merged compound guard.
   - Add `var instr = order.Instrument;` before the loop.
   - Add `TryGetValue` call before the loop.
   - Add reversal guard after the merged null/cap guard, before `GetMultiplier`.
   - Add dictionary update after the foreach.

3. **IsReversalToFlatFollower** (insert after `IsFlat` at ~L3305):
   - New `internal static bool IsReversalToFlatFollower(...)` method.

---

## 8. Spec Traceability

| Item | Reference |
|------|-----------|
| Defect | `specs/002-trade-copier-spec.html#section-dw-b128` |
| Prior guard (DW-B122) | `specs/002-trade-copier-spec.html#section-dw-b122` |
| Copy dispatch spec | `specs/002-trade-copier-spec.html#section-b8` |
| Dedup guard (DW-B91-A) | L1802 comment in CopyEngine.cs |
| JS-021 lock ban | `docs/standards/jane-street/RULES_CATALOG.md` Rule JS-021 |
| CYC ≤ 8 standard | `docs/standards/jane-street/RULES_CATALOG.md` (Jane Street strict standard) |
| NT8 OrderAction enum | `docs/standards/NT8_FULL_REFERENCE.md` L854-859 |
| IsFlat signature | `src/PropTraderTools/CopyEngine.cs` L3302 |
| FindPosition signature | `src/PropTraderTools/CopyEngine.cs` L3348 |

---

## 9. Acceptance Criteria

For ptt-plan-reviewer to issue REVIEW_PASS, the following must hold:

- [ ] `_lastLeaderDirection` field is `ConcurrentDictionary<string, OrderAction>` — no `lock()` anywhere in the change
- [ ] `IsReversalToFlatFollower` is `internal static` with the exact 3-parameter signature
- [ ] CYC of `IsReversalToFlatFollower` ≤ 8 (demonstrated above as 2)
- [ ] CYC of `DispatchCopy` ≤ 8 after modification (demonstrated above via branch merge)
- [ ] `_lastLeaderDirection` update occurs AFTER the foreach loop, not inside
- [ ] Guard only fires when `hasLastDirection=true` (key present) — first dispatch is never blocked
- [ ] All 11 [Fact] test names listed; Part A covers all 4 direction-combo cases + not-flat case
- [ ] Log line uses ASCII-only characters including `[PTT-COPY-GUARD]` prefix
- [ ] No `.cs` files written by ptt-architect (plan is docs only)
