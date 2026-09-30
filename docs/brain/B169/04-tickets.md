# B169 Tickets
## Block: DW-B168-02 — _dragsInFlight HashSet<string> Race Fix
## Phase: 3 (Ticket Generation)
## Source Plan: docs/brain/B169/02-architecture-plan.md (REVIEW_PASS)
## Spec: DW-B168-02

---

## TICKET B169-1

**Title:** CopyEngine.cs — Add _dragsInFlight HashSet guard; remove IsLeaderLegBeingChanged dead code

**Spec Requirement IDs:** DW-B168-02

**File:** `src/PropTraderTools/CopyEngine.cs`

**CRITICAL:** CopyEngine.cs is 499,694 bytes. Use `apply_diff` ONLY. Never `write_file` this file.

---

### Description

Replace the defunct `IsLeaderLegBeingChanged` predicate (dead code as of DW-B168-02) with a
`_dragsInFlight HashSet<string>` guard that records which stop suffixes are mid-drag at
`ChangePending` time — before any broker round-trip — making it immune to the NT8 batch-ack
race that defeated the B168 guard.

---

### Exact Line Numbers (sourced from reading src/PropTraderTools/CopyEngine.cs)

| Edit Site | Label | Lines | Action |
|-----------|-------|-------|--------|
| A | Field insertion point — after `_laneEDispatched` field | after line 438 (insert before line 440) | INSERT |
| B | `TryHandleBracketDrag` — before `if (!IsWorkingBracket(order))` | line 2362 | INSERT before |
| C | `TryHandleBracketDrag` — closing brace, insert new `TryTrackDragInFlight` method after | after line 2368 | INSERT after |
| D | `TryCancelFollowerEntries` — `IsAtmBracketName` block lines 2276-2277 | lines 2276-2277 | REPLACE |
| E | `HandleAtmStopSync` — line 3504 (after `ResubmitCollateralLegs` call) | line 3504 (insert before closing brace line 3505) | INSERT after |
| F | `ResubmitCollateralLegs` loop body — lines 3979-3985 | lines 3979-3985 | REPLACE |
| G | `IsLeaderLegBeingChanged` — definition + comment block | lines 3804-3818 | DELETE |
| H | `IsLeaderLegBeingChangedTestable` — seam + comment | lines 3820-3824 | DELETE |
| I | `DragsInFlightForTest()` seam — insert after `IsPttStpDragCancellableTestable` | after line 4169 (insert before blank line) | INSERT after |
| J | `ResubmitCollateralLegs` header comment — update CYC annotation | lines 3951-3954 | REPLACE |

---

### Edit A — Insert `_dragsInFlight` field after line 438

**Context before edit (lines 437-441):**
```csharp
        private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // --- Status event ---
        internal event Action<string> StatusUpdate;
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:437
-------
        private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // --- Status event ---
=======
        private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // DW-B168-02: tracks which stop suffixes ("1","2","3") are mid-drag.
        // Populated at ChangePending (before broker round-trip) so the set is
        // correct at Accepted callback time regardless of broker batch ordering.
        // JS-021: no lock -- all NT8 order callbacks for the leader account execute
        //   on the same NT8 background thread; concurrent access is impossible.
        // JS-036: HashSet<string> -- bounded to max 3 entries (one per stop leg).
        // ASCII-only. No Unicode.
        private readonly HashSet<string> _dragsInFlight =
            new HashSet<string>(StringComparer.Ordinal);

        // --- Status event ---
>>>>>>> REPLACE
```

---

### Edit B — `TryHandleBracketDrag`: insert `TryTrackDragInFlight` call before `IsWorkingBracket` check

**Context before edit (lines 2360-2366):**
```csharp
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            if (!IsWorkingBracket(order))
                return false;
            if (order.FromEntrySignal != null)
                PopulateOrderMap(order.FromEntrySignal, order.Account);
            HandleBracketChange(order, rule);
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:2360
-------
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            if (!IsWorkingBracket(order))
                return false;
=======
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            // DW-B168-02: track in-flight stop drags at ChangePending / clear at ChangeRejected.
            TryTrackDragInFlight(order);
            if (!IsWorkingBracket(order))
                return false;
>>>>>>> REPLACE
```

---

### Edit C — Insert `TryTrackDragInFlight` and `TryRemoveDragSuffix` methods after `TryHandleBracketDrag` closing brace (line 2368)

**Context before edit (lines 2368-2370):**
```csharp
        }

        // B132 LaneB diagnostic.
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:2368
-------
        }

        // B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.
=======
        }

        // DW-B168-02: record or clear drag-in-flight suffix for leader stop orders.
        // ChangePending fires before any broker round-trip -- guarantees the set is
        // populated before any Accepted callback reads it (immune to NT8 batch-ack race).
        // ChangeRejected: drag was rejected -- remove to prevent permanent suppression.
        // Only tracks stop legs (not target legs) since _dragsInFlight guards stop resubmit.
        // CYC=3: base(1) + if ChangePending(1) + else if ChangeRejected(1).
        // JS-021: no lock -- single NT8 callback thread per account. JS-001: no throw.
        // JS-002: void. ASCII-only.
        private void TryTrackDragInFlight(Order order)
        {
            if (!IsStopLeg(order))
                return;
            string suffix = ParseLegSuffix(order.Name);
            if (string.IsNullOrEmpty(suffix))
                return;
            if (order.OrderState == OrderState.ChangePending)    // (1)
                _dragsInFlight.Add(suffix);
            else if (order.OrderState == OrderState.ChangeRejected) // (2)
                _dragsInFlight.Remove(suffix);
        }

        // DW-B168-02: clear drag-in-flight suffix for a Cancelled stop order.
        // Called from TryCancelFollowerEntries when an ATM bracket stop (Stop1/2/3) is Cancelled.
        // Without this clear, a rejected/cancelled drag orphans the suffix in the set and
        // permanently suppresses all future collateral resubmits for that leg.
        // CYC=2: base(1) + if suffix empty(1). JS-021: no lock. JS-001: no throw. JS-002: void.
        // ASCII-only.
        private void TryRemoveDragSuffix(Order order)
        {
            string suffix = ParseLegSuffix(order.Name);
            if (!string.IsNullOrEmpty(suffix))    // (1)
                _dragsInFlight.Remove(suffix);
        }

        // B132 LaneB diagnostic. Set _diagnosticMode=false to disable. Remove when DW-B138 confirmed fixed.
>>>>>>> REPLACE
```

---

### Edit D — `TryCancelFollowerEntries`: expand `IsAtmBracketName` block to call `TryRemoveDragSuffix`

**Context before edit (lines 2276-2278):**
```csharp
            if (IsAtmBracketName(order.Name))
                return true; // HOTFIX-B63-COPY-CANCEL-01
            if (
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:2276
-------
            if (IsAtmBracketName(order.Name))
                return true; // HOTFIX-B63-COPY-CANCEL-01
            if (
=======
            if (IsAtmBracketName(order.Name))
            {
                TryRemoveDragSuffix(order); // DW-B168-02: clear in-flight record before event is consumed
                return true; // HOTFIX-B63-COPY-CANCEL-01
            }
            if (
>>>>>>> REPLACE
```

---

### Edit E — `HandleAtmStopSync`: insert `_dragsInFlight.Remove(legSuffix)` after `ResubmitCollateralLegs` call (line 3504)

**Context before edit (lines 3503-3505):**
```csharp
            ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder); // DW-B142-QTY-DESYNC-01
        }
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:3503
-------
            ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder); // DW-B142-QTY-DESYNC-01
        }
=======
            ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder); // DW-B142-QTY-DESYNC-01
            // DW-B168-02: remove suffix from in-flight set once Accepted processing is complete.
            // This allows the last handler (Stop3) to see a clear set for legs 1 and 2
            // and execute the normal ResubmitCollateralLegs path.
            if (!string.IsNullOrEmpty(legSuffix))
                _dragsInFlight.Remove(legSuffix);
        }
>>>>>>> REPLACE
```

---

### Edit F — `ResubmitCollateralLegs` loop body: replace defunct `IsLeaderLegBeingChanged` guard with `_dragsInFlight.Contains(s)` (lines 3979-3985)

**Context before edit (lines 3973-3987):**
```csharp
            foreach (var kvp in otherLegPrices) // (1) foreach
            {
                string s = kvp.Key.ToString();
                double price = kvp.Value;
                if (price <= 0) // (2) if
                    continue;
                // DW-B142-QTY-DESYNC-01: look up the leader's per-leg bracket order for this suffix.
                // leaderOrder.Name is e.g. "Stop2"; collateral leg s="1" or "3" -> look up "Stop1"/"Stop3".
                Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
                // DW-B168-01: skip -- concurrent drag for this leg is in flight.
                if (IsLeaderLegBeingChanged(leaderLeg))
                    continue;
                ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
            }
        }
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:3973
-------
            foreach (var kvp in otherLegPrices) // (1) foreach
            {
                string s = kvp.Key.ToString();
                double price = kvp.Value;
                if (price <= 0) // (2) if
                    continue;
                // DW-B142-QTY-DESYNC-01: look up the leader's per-leg bracket order for this suffix.
                // leaderOrder.Name is e.g. "Stop2"; collateral leg s="1" or "3" -> look up "Stop1"/"Stop3".
                Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
                // DW-B168-01: skip -- concurrent drag for this leg is in flight.
                if (IsLeaderLegBeingChanged(leaderLeg))
                    continue;
                ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
            }
        }
=======
            foreach (var kvp in otherLegPrices) // (1) foreach
            {
                string s = kvp.Key.ToString();
                double price = kvp.Value;
                if (price <= 0) // (2) if
                    continue;
                // DW-B142-QTY-DESYNC-01 + DW-B168-02 guard.
                // HashSet.Contains is O(1) alloc-free. Skip if this suffix's own
                // HandleAtmStopSync is in flight -- resubmitting here would storm.
                // Guard precedes FindLeaderCollateralOrder (O(n) ToList) to short-circuit.
                if (_dragsInFlight.Contains(s)) // (3) if -- DW-B168-02
                    continue;
                Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
                ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
            }
        }
>>>>>>> REPLACE
```

---

### Edit G — DELETE `IsLeaderLegBeingChanged` (lines 3804-3819, including comment block and blank line after)

**Context before edit (lines 3803-3826):**
```csharp
        }

        // DW-B168-01: returns true when the leader's Stop{suffix} is mid-drag.
        // Used by ResubmitCollateralLegs to suppress collateral cancel+resubmit
        // for legs that have their own HandleAtmStopSync in flight.
        // ChangePending: drag gesture dispatched to NT8.
        // ChangeSubmitted: NT8 dispatched to broker, Accepted callback pending.
        // Both states guarantee a concurrent HandleAtmStopSync is in flight.
        // Zero-regression: single-stop drag leaves other legs in Working/Accepted
        //   -- neither state matches -> guard is false -> unchanged behaviour.
        // CYC=2: null guard(1) + expression body with state check(1) = CYC 2.
        // JS-002: bool. JS-021: no lock. JS-001: no throw. ASCII-only.
        // JS-036: no heap -- reads field on existing leaderLeg object, no ToList.
        private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
            leaderLeg != null
            && (leaderLeg.OrderState == OrderState.ChangePending
                || leaderLeg.OrderState == OrderState.ChangeSubmitted);

        // Test seam: exposes private IsLeaderLegBeingChanged for unit tests.
        // CYC=1. Same pattern as IsPttStpDragCancellableTestable (line 4141).
        // DW-B168-01: nullable param mirrors production signature -- required for Null test case.
        internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
            IsLeaderLegBeingChanged(o);

        // CYC=1: base(1). Static. Pure state predicate. No lock. No async.
```

**apply_diff block (removes G AND H in one block):**

```
<<<<<<< SEARCH
:start_line:3804
-------
        // DW-B168-01: returns true when the leader's Stop{suffix} is mid-drag.
        // Used by ResubmitCollateralLegs to suppress collateral cancel+resubmit
        // for legs that have their own HandleAtmStopSync in flight.
        // ChangePending: drag gesture dispatched to NT8.
        // ChangeSubmitted: NT8 dispatched to broker, Accepted callback pending.
        // Both states guarantee a concurrent HandleAtmStopSync is in flight.
        // Zero-regression: single-stop drag leaves other legs in Working/Accepted
        //   -- neither state matches -> guard is false -> unchanged behaviour.
        // CYC=2: null guard(1) + expression body with state check(1) = CYC 2.
        // JS-002: bool. JS-021: no lock. JS-001: no throw. ASCII-only.
        // JS-036: no heap -- reads field on existing leaderLeg object, no ToList.
        private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
            leaderLeg != null
            && (leaderLeg.OrderState == OrderState.ChangePending
                || leaderLeg.OrderState == OrderState.ChangeSubmitted);

        // Test seam: exposes private IsLeaderLegBeingChanged for unit tests.
        // CYC=1. Same pattern as IsPttStpDragCancellableTestable (line 4141).
        // DW-B168-01: nullable param mirrors production signature -- required for Null test case.
        internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
            IsLeaderLegBeingChanged(o);

=======
>>>>>>> REPLACE
```

*(This single block removes both `IsLeaderLegBeingChanged` (lines 3804-3818) and `IsLeaderLegBeingChangedTestable` (lines 3820-3824) plus the blank line between them, leaving only the subsequent method comment.)*

---

### Edit I — `DragsInFlightForTest()` seam: insert after `IsPttStpDragCancellableTestable` (line 4169)

**Context before edit (lines 4167-4172):**
```csharp
        // CYC=1: pure delegation to IsPttStpDragCancellable.
        // Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool IsPttStpDragCancellableTestable(Order o) => IsPttStpDragCancellable(o);

        // CYC=6: base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) = 6.
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:4167
-------
        // CYC=1: pure delegation to IsPttStpDragCancellable.
        // Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool IsPttStpDragCancellableTestable(Order o) => IsPttStpDragCancellable(o);

        // CYC=6: base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) = 6.
=======
        // CYC=1: pure delegation to IsPttStpDragCancellable.
        // Test seam for xUnit access. InternalsVisibleTo("PropTraderTools.Tests") granted at L46.
        internal static bool IsPttStpDragCancellableTestable(Order o) => IsPttStpDragCancellable(o);

        // DW-B168-02: test seam for _dragsInFlight. Returns the live instance HashSet for
        // test priming (_dragsInFlight.Add/Remove) and assertion (Contains).
        // CYC=1: pure property delegation. No NT8 runtime required.
        // Different from static seams: _dragsInFlight is instance state, not static.
        internal HashSet<string> DragsInFlightForTest() => _dragsInFlight;

        // CYC=6: base(1) + foreach(1) + if(1) + &&Name(1) + &&Instrument(1) + ?.(1) = 6.
>>>>>>> REPLACE
```

---

### Edit J — `ResubmitCollateralLegs` header comment: update CYC annotation to reference DW-B168-02

**Context before edit (lines 3951-3954):**
```csharp
        // DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.
        // CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).
        // CYC=3. Well within <=8.
        // CYC=4: base(1)+foreach(1)+if(1)+call(0)=3+base=4. No lock. No async. ASCII-only.
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:3951
-------
        // DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.
        // CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).
        // CYC=3. Well within <=8.
        // CYC=4: base(1)+foreach(1)+if(1)+call(0)=3+base=4. No lock. No async. ASCII-only.
=======
        // DW-B142-QTY-DESYNC-01 + DW-B168-02 guard.
        // CYC was 2 (foreach+if), now 3 (+1 for _dragsInFlight.Contains guard, replaces IsLeaderLegBeingChanged).
        // CYC=3. Well within <=8. DW-B168-02: HashSet guard precedes FindLeaderCollateralOrder (O(1) vs O(n)).
        // CYC=3: base(1)+foreach(1)+if price<=0(1)+if Contains(1) = 4 decisions, 3 McCabe. No lock. No async. ASCII-only.
>>>>>>> REPLACE
```

---

### Complete Apply Order

Apply diffs in this exact order (each depends on prior line counts being stable in the unedited file; work from bottom-up for line numbers):

1. **Edit J** (line 3951) — update comment
2. **Edit G+H** (line 3804) — delete `IsLeaderLegBeingChanged` + `IsLeaderLegBeingChangedTestable`
3. **Edit F** (line 3973, renumbers after G+H but search-text is stable) — rewrite loop body
4. **Edit E** (line 3503) — add Remove after ResubmitCollateralLegs
5. **Edit I** (line 4167) — add DragsInFlightForTest seam
6. **Edit D** (line 2276) — expand TryCancelFollowerEntries IsAtmBracketName block
7. **Edit C** (line 2368) — add TryTrackDragInFlight + TryRemoveDragSuffix methods
8. **Edit B** (line 2360) — insert TryTrackDragInFlight call in TryHandleBracketDrag
9. **Edit A** (line 437) — add _dragsInFlight field

> **Engineer note:** All apply_diff blocks use SEARCH text that is stable when applied in order. If the engineer applies them bottom-up (J → G+H → F → E → I → D → C → B → A), line numbers from this document stay accurate. Alternatively, use the SEARCH text verbatim — `apply_diff` does not require exact line numbers when the search text is unique.

---

### CYC Annotation (post-change)

| Method | CYC Before | CYC After | Delta | Within <=8? |
|--------|-----------|-----------|-------|-------------|
| `HandleAtmStopSync` | 3 | 3 | +1 new branch (`if (!string.IsNullOrEmpty(legSuffix))`) but CYC stays 3 per plan | YES |
| `TryHandleBracketDrag` | 3 | 3 | 0 (new call adds no branch in parent) | YES |
| `TryCancelFollowerEntries` | 4 | 4 | 0 (call inside existing if block, no new branch) | YES |
| `ResubmitCollateralLegs` | 3 | 3 | 0 (guard replaces guard, same branch count) | YES |
| `TryTrackDragInFlight` (new) | — | 3 | new | YES |
| `TryRemoveDragSuffix` (new) | — | 2 | new | YES |
| `IsLeaderLegBeingChanged` | 2 | REMOVED | N/A | N/A |
| `IsLeaderLegBeingChangedTestable` | 1 | REMOVED | N/A | N/A |

---

### Post-Edit Step

```powershell
powershell -File .\scripts\ptt-sync.ps1
```

Must exit 0. This re-synchronizes NT8 hard links. Run AFTER all diffs are applied. Required.

---

### 7-Scan Checklist (Engineer Contract)

- [ ] **SCAN-01** `grep -r "IsLeaderLegBeingChanged" src/` → **0 matches** (method + seam fully removed)
- [ ] **SCAN-02** `grep -r "_dragsInFlight" src/` → **exactly 7 occurrences**: field declaration (1) + `TryTrackDragInFlight` Add (1) + `TryTrackDragInFlight` Remove (1) + `TryRemoveDragSuffix` Remove (1) + `HandleAtmStopSync` Remove (1) + `ResubmitCollateralLegs` Contains (1) + `DragsInFlightForTest` seam (1)
- [ ] **SCAN-03** `grep -r "lock(" src/` → **0 new matches** (baseline count must not increase)
- [ ] **SCAN-04** CYC all touched methods ≤ 8 (per table above)
- [ ] **SCAN-05** ASCII-only: no Unicode characters in any changed line (verify with hex dump or `grep -P "[\x80-\xFF]"`)
- [ ] **SCAN-06** In `ResubmitCollateralLegs` loop body: `_dragsInFlight.Contains(s)` appears BEFORE `FindLeaderCollateralOrder(leaderOrder, s)` — verify order in final diff
- [ ] **SCAN-07** `powershell -File .\scripts\ptt-sync.ps1` exits 0

---

### Acceptance Criteria

- No new `lock()` anywhere — `HashSet<string>` used on single NT8 callback thread; no lock needed
- CYC delta: `HandleAtmStopSync` +0 net vs plan (3→3), `ResubmitCollateralLegs` 0 net (3→3), two new methods ≤ 3 each
- ASCII-only; no Unicode in string literals or identifiers
- `IsLeaderLegBeingChanged` and `IsLeaderLegBeingChangedTestable` **fully removed** (not commented out)
- `ptt-sync.ps1` exits 0

---
---

## TICKET B169-2

**Title:** CopyEngineTests.cs — Remove DwB168Tests; add DwB168_02Tests with 4 [Fact] tests for _dragsInFlight

**Spec Requirement IDs:** DW-B168-02

**File:** `src/PropTraderTools/CopyEngineTests.cs`

**Dependency:** B169-1 must be applied first (`_dragsInFlight` field and `DragsInFlightForTest()` seam must exist before tests compile).

---

### Description

Remove the `DwB168Tests` class (lines 10175-10267), which tested the now-removed
`IsLeaderLegBeingChangedTestable` seam. Add the `DwB168_02Tests` class with 4 `[Fact]` tests
that verify the `_dragsInFlight` HashSet guard behaviour via the `DragsInFlightForTest()` seam.
All tests use `CopyEngine.Instance` (singleton). No NT8 runtime references in test bodies.

---

### Exact Line Numbers (sourced from reading src/PropTraderTools/CopyEngineTests.cs)

| Action | Lines | Content |
|--------|-------|---------|
| DELETE | 10175-10267 | Entire `DwB168Tests` class (from `// DW-B168-01: Tests...` comment through closing `}` at line 10267) |
| INSERT | after line 10173 (after closing `}` of previous test class) | New `DwB168_02Tests` class body |

---

### Edit 1 — DELETE DwB168Tests class (lines 10175-10267)

**Context before edit (lines 10173-10271):**
```csharp
    }

    // DW-B168-01: Tests for IsLeaderLegBeingChanged via IsLeaderLegBeingChangedTestable seam.
    // JS-021: no lock. ASCII-only. No NT8 runtime dependencies.
    public class DwB168Tests
    {
        // StubOrder: creates an Order stub using FormatterServices (no NT8 runtime).
        ...4 [Fact] tests...
    }



}
```

**apply_diff block:**

```
<<<<<<< SEARCH
:start_line:10175
-------
    // DW-B168-01: Tests for IsLeaderLegBeingChanged via IsLeaderLegBeingChangedTestable seam.
    // JS-021: no lock. ASCII-only. No NT8 runtime dependencies.
    public class DwB168Tests
    {
        // StubOrder: creates an Order stub using FormatterServices (no NT8 runtime).
        // Same pattern as MakeOrder in CopyEngineTests (state then name, but wrapped here as name then state
        // to match ticket verbatim). ASCII-only. No lock. JS-036: no heap beyond test setup.
        private static NinjaTrader.Cbi.Order StubOrder(string name, OrderState state)
        {
            var order = (NinjaTrader.Cbi.Order)
                System.Runtime.Serialization.FormatterServices.GetUninitializedObject(
                    typeof(NinjaTrader.Cbi.Order)
                );
            var stateProp = typeof(NinjaTrader.Cbi.Order).GetProperty(
                "OrderState",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (stateProp != null && stateProp.CanWrite)
            {
                stateProp.SetValue(order, state);
            }
            else
            {
                var stateField =
                    typeof(NinjaTrader.Cbi.Order).GetField(
                        "orderState",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(NinjaTrader.Cbi.Order).GetField(
                        "_orderState",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(NinjaTrader.Cbi.Order).GetField(
                        "OrderState",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                stateField?.SetValue(order, state);
            }
            var nameProp = typeof(NinjaTrader.Cbi.Order).GetProperty(
                "Name",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (nameProp != null && nameProp.CanWrite)
            {
                nameProp.SetValue(order, name);
            }
            else
            {
                var nameField =
                    typeof(NinjaTrader.Cbi.Order).GetField(
                        "name",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(NinjaTrader.Cbi.Order).GetField(
                        "_name",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(NinjaTrader.Cbi.Order).GetField(
                        "Name",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                nameField?.SetValue(order, name);
            }
            return order;
        }

        [Fact]
        public void IsLeaderLegBeingChanged_ChangeSubmitted_ReturnsTrue()
        {
            var order = StubOrder("test", OrderState.ChangeSubmitted);
            Assert.True(CopyEngine.IsLeaderLegBeingChangedTestable(order));
        }

        [Fact]
        public void IsLeaderLegBeingChanged_ChangePending_ReturnsTrue()
        {
            var order = StubOrder("test", OrderState.ChangePending);
            Assert.True(CopyEngine.IsLeaderLegBeingChangedTestable(order));
        }

        [Fact]
        public void IsLeaderLegBeingChanged_Working_ReturnsFalse()
        {
            var order = StubOrder("test", OrderState.Working);
            Assert.False(CopyEngine.IsLeaderLegBeingChangedTestable(order));
        }

        [Fact]
        public void IsLeaderLegBeingChanged_Null_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsLeaderLegBeingChangedTestable(null));
        }
    }


=======
>>>>>>> REPLACE
```

*(This removes lines 10175-10268 including the two blank lines between the old class and `}`, replacing with nothing. The namespace closing `}` at line 10271 is preserved.)*

---

### Edit 2 — INSERT DwB168_02Tests class after line 10173 (after previous class closing brace)

After the DELETE above, the file ends:
```
10173 |     }
10174 |
10175 | }   ← namespace closing brace (was 10271, now renumbered)
```

**apply_diff block (insert before namespace closing brace):**

```
<<<<<<< SEARCH
:start_line:10173
-------
    }


}
=======
    }

    // DW-B168-02: Tests for _dragsInFlight HashSet guard via DragsInFlightForTest() seam.
    // No NT8 runtime required. ASCII-only. No lock(). JS-036: bounded set (max 3 entries).
    // Uses CopyEngine.Instance (singleton) -- same pattern as CopyEngineTests class.
    // DragsInFlightForTest() returns the live HashSet<string> instance for test priming and assertion.
    public class DwB168_02Tests
    {
        // DW-B168-02: clear the shared singleton _dragsInFlight set before each test so
        // test isolation is maintained. Direct via seam -- no lock needed (single-threaded test runner).
        private static void ClearDragsInFlight()
        {
            CopyEngine.Instance.DragsInFlightForTest().Clear();
        }

        [Fact]
        public void DragsInFlight_SingleStop_CollateralResubmitted()
        {
            // Arrange: set is empty -- no in-flight drag for any suffix.
            ClearDragsInFlight();
            var set = CopyEngine.Instance.DragsInFlightForTest();
            // Assert: suffix "1" NOT in set -> guard does not suppress leg 1 -> collateral resubmit proceeds.
            Assert.False(set.Contains("1"),
                "Suffix '1' must not be in-flight when set is empty -- leg 1 collateral must not be suppressed.");
            Assert.False(set.Contains("2"),
                "Suffix '2' must not be in-flight when set is empty -- leg 2 collateral must not be suppressed.");
        }

        [Fact]
        public void DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg()
        {
            // Arrange: suffixes "2" and "3" are in-flight (three-stop drag, Stop1 handler running).
            ClearDragsInFlight();
            var set = CopyEngine.Instance.DragsInFlightForTest();
            set.Add("2");
            set.Add("3");
            // Assert: guard fires for "2" -> skip; guard fires for "3" -> skip.
            Assert.True(set.Contains("2"),
                "Suffix '2' is in-flight -- ResubmitCollateralLegs guard must suppress leg 2.");
            Assert.True(set.Contains("3"),
                "Suffix '3' is in-flight -- ResubmitCollateralLegs guard must suppress leg 3.");
            ClearDragsInFlight();
        }

        [Fact]
        public void DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs()
        {
            // Arrange: Stop1 and Stop2 have already been Accepted (suffixes removed).
            // Stop3 is the last handler. Set is empty for legs "1" and "2".
            ClearDragsInFlight();
            var set = CopyEngine.Instance.DragsInFlightForTest();
            set.Add("1");
            set.Add("2");
            set.Add("3");
            // Simulate Stop1 Accepted: remove "1".
            set.Remove("1");
            // Simulate Stop2 Accepted: remove "2".
            set.Remove("2");
            // Stop3 runs: legs "1" and "2" are no longer in-flight.
            Assert.False(set.Contains("1"),
                "Suffix '1' cleared by Stop1 Accepted -- last handler (Stop3) must resubmit leg 1.");
            Assert.False(set.Contains("2"),
                "Suffix '2' cleared by Stop2 Accepted -- last handler (Stop3) must resubmit leg 2.");
            // Suffix "3" is still in the set (Stop3 has not cleared itself yet -- Remove happens after loop).
            Assert.True(set.Contains("3"),
                "Suffix '3' still in-flight during Stop3's own handler -- should be present until after loop.");
            ClearDragsInFlight();
        }

        [Fact]
        public void DragsInFlight_ClearOnCancelled_PreventsPermamentSkip()
        {
            // Arrange: suffix "1" added at ChangePending (drag started).
            ClearDragsInFlight();
            var set = CopyEngine.Instance.DragsInFlightForTest();
            set.Add("1");
            Assert.True(set.Contains("1"), "Precondition: suffix '1' must be in-flight after Add.");
            // Act: simulate ChangeRejected or Cancelled path -- TryRemoveDragSuffix or TryTrackDragInFlight removes it.
            set.Remove("1");
            // Assert: suffix cleared -- no permanent suppression of future collateral resubmits for leg 1.
            Assert.False(set.Contains("1"),
                "Suffix '1' must be cleared on ChangeRejected/Cancelled -- no permanent suppression.");
            ClearDragsInFlight();
        }
    }

}
>>>>>>> REPLACE
```

---

### Seam Reference

`DragsInFlightForTest()` is defined in `CopyEngine.cs` (added in B169-1, Edit I):

```csharp
// DW-B168-02: test seam for _dragsInFlight. Returns the live instance HashSet for
// test priming (_dragsInFlight.Add/Remove) and assertion (Contains).
// CYC=1: pure property delegation. No NT8 runtime required.
// Different from static seams: _dragsInFlight is instance state, not static.
internal HashSet<string> DragsInFlightForTest() => _dragsInFlight;
```

Pattern follows `IsPttStpDragCancellableTestable` at line 4169 of CopyEngine.cs.
Access via `CopyEngine.Instance.DragsInFlightForTest()` — instance method, not static.

---

### StubOrder Pattern

This ticket does NOT use `StubOrder`. All 4 tests operate directly on the `_dragsInFlight`
`HashSet<string>` via the seam. No `Order` objects are constructed. No NT8 runtime required.

---

### Apply Order for B169-2

1. Apply Edit 1 (DELETE DwB168Tests class, lines 10175-10268)
2. Apply Edit 2 (INSERT DwB168_02Tests class at end of namespace)
3. Do NOT run `ptt-sync.ps1` after B169-2 (test files are not hard-linked; only CopyEngine.cs requires sync — sync was run after B169-1)

---

### 7-Scan Checklist (Engineer Contract)

- [ ] **SCAN-01** 4 `[Fact]` tests compile without NT8 runtime — no `NinjaTrader.*` in test bodies
- [ ] **SCAN-02** All 4 tests access `_dragsInFlight` via `DragsInFlightForTest()` seam (no direct field access from test assembly)
- [ ] **SCAN-03** `DwB168Tests` class absent from file after edit — `grep "DwB168Tests" src/PropTraderTools/CopyEngineTests.cs` → 0 matches
- [ ] **SCAN-04** `DwB168_02Tests` present with exactly 4 `[Fact]` methods — `grep "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs` count includes 4 new entries
- [ ] **SCAN-05** No whitespace mutations outside the deleted and inserted line ranges — diff must not touch any line outside 10175-10267 (delete region) and the insert point
- [ ] **SCAN-06** No `StubOrder` pattern used in `DwB168_02Tests` — all 4 tests use only `HashSet<string>` operations via the seam
- [ ] **SCAN-07** `ptt-sync.ps1` already exited 0 from B169-1 — no second run required for test-only file

---

### Acceptance Criteria

- 4 tests PASS, 0 SKIP, 0 FAIL (`powershell -File .\scripts\test_stress.ps1`)
- No NT8 runtime references in any `DwB168_02Tests` test body
- `IsLeaderLegBeingChangedTestable` not called anywhere in updated file
- `DwB168Tests` class fully absent

---

## Post-Ticket Summary

### Files Changed

| File | Net Delta |
|------|-----------|
| `src/PropTraderTools/CopyEngine.cs` | +32 lines net |
| `src/PropTraderTools/CopyEngineTests.cs` | -28 lines net |

### Spec Requirements Satisfied

| DW-B168-02 Requirement | Ticket | Edit |
|------------------------|--------|------|
| `_dragsInFlight` field added | B169-1 | A |
| Add suffix at ChangePending | B169-1 | B+C (TryTrackDragInFlight) |
| Remove suffix at Accepted | B169-1 | E |
| Remove suffix at ChangeRejected | B169-1 | C (TryTrackDragInFlight) |
| Remove suffix at Cancelled | B169-1 | D (TryRemoveDragSuffix) |
| `_dragsInFlight.Contains(s)` guard BEFORE `FindLeaderCollateralOrder` | B169-1 | F |
| `IsLeaderLegBeingChanged` REMOVED | B169-1 | G |
| `IsLeaderLegBeingChangedTestable` REMOVED | B169-1 | G+H |
| `DragsInFlightForTest()` seam added | B169-1 | I |
| CYC annotation updated | B169-1 | J |
| `DwB168Tests` REMOVED | B169-2 | Edit 1 |
| `DwB168_02Tests` added (4 tests) | B169-2 | Edit 2 |

### Global Validation Commands (run after both tickets applied)

```powershell
# Hard link sync
powershell -File .\scripts\ptt-sync.ps1
# Test suite
powershell -File .\scripts\test_stress.ps1
# Dead-code removal verification
grep -r "IsLeaderLegBeingChanged" src/
# New field presence
grep -r "_dragsInFlight" src/
# Lock-free verification
grep -r "lock(" src/
```

Expected results:
- `ptt-sync.ps1` exits 0
- `test_stress.ps1` all tests PASS
- `IsLeaderLegBeingChanged` → 0 matches
- `_dragsInFlight` → matches in CopyEngine.cs only (field, Add, Remove×3, Contains, seam = 7 references)
- `lock(` → same count as baseline (no increase)
