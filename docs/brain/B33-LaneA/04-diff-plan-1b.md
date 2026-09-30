# B33 Phase 1b Diff Plan
# Author: ptt-architect (Lane A)
# Phase: 1b (BUG-B33-02 + BUG-B33-03)
# Status: READY FOR LANE B EXECUTION
# Date: 2026-07-21

---

## Section 1: Rules Gate

### JS P0 Rules — checked against all 9 planned changes

| Rule | Pattern | Status |
|------|---------|--------|
| JS-021 | `lock(` anywhere | PASS — zero lock() in any planned change |
| JS-033 | `async void` | PASS — no async void introduced |
| JS-001 | `throw new XxxException` in hot path | PASS — no throws; all errors caught |
| JS-002 | `return null` for missing values | PASS — no null returns; TryGetValue pattern |

### NT8 P0 Rules — checked against all 9 planned changes

| Rule | Pattern | Status |
|------|---------|--------|
| NT8-003 | `volatile double` | PASS — removing `volatile` from `Order` field (field becomes ConcurrentDictionary with no volatile needed) |
| NT8-007 | `CreateOrder` arg 12 as `string` | PASS — existing `(NinjaTrader.Cbi.CustomOrder)null` not touched |
| NT8-013 | `DateTime.Now` in CreateOrder | PASS — existing `DateTime.MaxValue` not touched |
| NT8-019 | `async void` | PASS — none introduced |
| NT8-031 | `OrderState.PendingSubmit` | PASS — none used |
| NT8-042 | `Dispatcher.InvokeAsync` | PASS — none used |
| NT8-043 | Null-conditional compound assignment `?.` + `-=` | PASS — none introduced |
| NT8-050 | `acc.Positions[instr]` | PASS — CancelStaleBrackets uses `acc.Orders` (not Positions) |

**GATE RESULT: PASS — zero P0 violations in any planned change.**

---

## Section 2: NT8 Compiler Check

| Item | Check | Result |
|------|-------|--------|
| `using System.Linq` present | CancelStaleBrackets uses `.Where(...).ToList()` — NT8-006 requires explicit import | **CONFIRMED** — `using System.Linq;` at `CopyEngine.cs:29` |
| `ConcurrentDictionary<string, Order>` | NT8-004: Immutable collections banned; ConcurrentDictionary safe | **CONFIRMED SAFE** — existing code uses ConcurrentDictionary at lines 102, 129, 136-139, 144 in same file; NT8-004 note confirms "confirmed safe in NT8" |
| `out var existing` / `out var stop` / `out _` | Requires C# 7.0+ out var; NT8 compiles at C# 7.3 | **CONFIRMED SAFE** — existing code uses `out var slot` at lines 1807, 1848, 1867, 1900, 1910, 1938; same pattern |
| `acc.Cancel(stale.ToArray())` where stale is `List<Order>` | All three existing Cancel sites pass `Order[]`; ToArray() matches exact established pattern | **CONFIRMED SAFE** — consistent with lines 1125, 1150, 1608; no new overload required |
| `OrderState.Accepted` in CancelStaleBrackets filter | Queried for existence | **CONFIRMED** — NT8 OrderState includes `Accepted` (NT8-031 bans only `PendingSubmit`; `Accepted` is valid) |
| `.Orders` collection on Account | Used as `acc.Orders.ToList()` — same pattern as FindFollowerBracketOrder at line 678 | **CONFIRMED SAFE** — identical usage at line 1115, 1142 |
| `leaderAcc.Cancel(stale.ToArray())` — no overload mismatch | `stale.ToArray()` produces `Order[]` matching existing signature | **CONFIRMED SAFE** |

**NT8 COMPILER CHECK: ALL ITEMS PASS.**

---

## Section 3: Source Location Map

Exact confirmed line numbers from source inspection on 2026-07-21.

| Change | Description | File | Exact Line(s) |
|--------|-------------|------|---------------|
| C1 | `_pendingBeStop` field declaration | `CopyEngine.cs` | **162–164** |
| C2 | SubmitBeStop duplicate guard | `CopyEngine.cs` | **1563** |
| C3a | SubmitBeStop `_pendingBeStop =` CreateOrder assign | `CopyEngine.cs` | **1573** |
| C3b | SubmitBeStop `leaderAcc.Submit(new[] { _pendingBeStop })` | `CopyEngine.cs` | **1580** |
| C4 | OrphanCancelGuard null check | `CopyEngine.cs` | **1599** |
| C5a | OrphanCancelGuard state guard + null reset | `CopyEngine.cs` | **1601–1604** |
| C5b | OrphanCancelGuard cancel call | `CopyEngine.cs` | **1608** |
| C5c | OrphanCancelGuard trailing `_pendingBeStop = null` | `CopyEngine.cs` | **1616** |
| C6 | Insert CancelStaleBrackets new method | `CopyEngine.cs` | **after 1617** (blank line before BreakEven at 1619) |
| C7 | TryFirePositionState hook (OrphanCancelGuard call site) | `CopyEngine.cs` | **740–741** |
| C8 | Build tag string | `CopyEngine.cs` | **41** |
| C9 | PendingBeStop_FieldExists test | `CopyEngineTests.cs` | **2756–2764** |

---

## Section 4: Complete Diff Plan

### Change 1 — `_pendingBeStop` field: `volatile Order` → `ConcurrentDictionary<string, Order>`

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Lines:** 162–164

**Old:**
```csharp
        // B33 DW-B33-01: pending BE stop reference. volatile (NT8-017: read on order thread, written on BE arm).
        // null = no BE stop pending. Set by SubmitBeStop. Cleared by OrphanCancelGuard.
        private volatile Order _pendingBeStop = null;
```

**New:**
```csharp
        // B33 BUG-B33-03 fix: per-account dict replaces singleton volatile Order.
        // JS-021: ConcurrentDictionary is lock-free. NT8-003: no volatile needed -- ConcurrentDictionary provides memory barrier.
        // Key = acc.Name. null entry = no pending BE stop for that account.
        private readonly ConcurrentDictionary<string, Order> _pendingBeStop
            = new ConcurrentDictionary<string, Order>();
```

**Rationale:** A single `volatile Order` field is overwritten when two accounts arm BE simultaneously; a per-account dictionary eliminates the race.

---

### Change 2 — SubmitBeStop duplicate guard

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Lines:** 1563–1567 (replace the if-block including its body)

**Old:**
```csharp
            if (_pendingBeStop != null && _pendingBeStop.OrderState == OrderState.Working) // (2) duplicate guard
            {
                NinjaTrader.Code.Output.Process("[BE] SubmitBeStop -- pending BE stop already live, skip", PrintTo.OutputTab1);
                return;
            }
```

**New:**
```csharp
            if (_pendingBeStop.TryGetValue(leaderAcc.Name, out var existing)              // (2) duplicate guard
                && existing != null && existing.OrderState == OrderState.Working)
            {
                NinjaTrader.Code.Output.Process("[BE] SubmitBeStop -- pending BE stop already live, skip", PrintTo.OutputTab1);
                return;
            }
```

**Rationale:** Guard now checks the per-account dictionary entry instead of a shared singleton field.

---

### Change 3 — SubmitBeStop CreateOrder assign + Submit

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Lines:** 1573–1580 (replace the two assignment lines)

**Old:**
```csharp
                _pendingBeStop = leaderAcc.CreateOrder(
                    instr, direction, OrderType.StopMarket, OrderEntry.Manual,
                    TimeInForce.Day, pos.Quantity,
                    0,        // arg6: limitPrice -- MUST be 0 for StopMarket (NT8-049)
                    bePrice,  // arg7: stopPrice  -- bePrice goes HERE (NT8-049)
                    "", "PTT-BE-Stop", DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null);
                leaderAcc.Submit(new[] { _pendingBeStop });
```

**New:**
```csharp
                var beStop = leaderAcc.CreateOrder(
                    instr, direction, OrderType.StopMarket, OrderEntry.Manual,
                    TimeInForce.Day, pos.Quantity,
                    0,        // arg6: limitPrice -- MUST be 0 for StopMarket (NT8-049)
                    bePrice,  // arg7: stopPrice  -- bePrice goes HERE (NT8-049)
                    "", "PTT-BE-Stop", DateTime.MaxValue,
                    (NinjaTrader.Cbi.CustomOrder)null);
                _pendingBeStop[leaderAcc.Name] = beStop;
                leaderAcc.Submit(new[] { beStop });
```

**Rationale:** Store the new order in the per-account dictionary slot and submit using the local variable, not the shared field.

---

### Change 4 — OrphanCancelGuard null check

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Lines:** 1599–1600

**Old:**
```csharp
            if (_pendingBeStop == null)                                                    // (1) null check
                return;
```

**New:**
```csharp
            if (!_pendingBeStop.TryGetValue(acc.Name, out var stop) || stop == null)      // (1) null check
                return;
```

**Rationale:** TryGetValue on the per-account dictionary replaces the singleton null check.

---

### Change 5 — OrphanCancelGuard state guard + cancel + clear

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Lines:** 1601–1616 (replace the entire state-guard block through the trailing null-clear)

**Old:**
```csharp
            if (_pendingBeStop.OrderState != OrderState.Working)                          // (2) not working
            {
                _pendingBeStop = null;
                return;
            }
            try                                                                            // (3) cancel
            {
                acc.Cancel(new Order[] { _pendingBeStop });
                NinjaTrader.Code.Output.Process("[BE] OrphanCancelGuard fired -- pending BE stop cancelled", PrintTo.OutputTab1);
                StatusUpdate?.Invoke(acc.Name + ": OrphanCancelGuard -- BE stop cancelled");
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("[BE] OrphanCancelGuard EXCEPTION -- " + ex.Message, PrintTo.OutputTab1);
            }
            _pendingBeStop = null;
```

**New:**
```csharp
            if (stop.OrderState != OrderState.Working)                                    // (2) not working
            {
                _pendingBeStop.TryRemove(acc.Name, out _);
                return;
            }
            try                                                                            // (3) cancel
            {
                acc.Cancel(new Order[] { stop });
                NinjaTrader.Code.Output.Process("[BE] OrphanCancelGuard fired -- pending BE stop cancelled", PrintTo.OutputTab1);
                StatusUpdate?.Invoke(acc.Name + ": OrphanCancelGuard -- BE stop cancelled");
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("[BE] OrphanCancelGuard EXCEPTION -- " + ex.Message, PrintTo.OutputTab1);
            }
            _pendingBeStop.TryRemove(acc.Name, out _);
```

**Rationale:** All three sites that previously set `_pendingBeStop = null` now call `TryRemove` on the per-account key; the local `stop` variable is used for the Cancel call.

---

### Change 6 — New `CancelStaleBrackets` method

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Insert after:** line 1617 (the closing brace of `OrphanCancelGuard`), before line 1618 (blank line) and line 1619 (`internal void BreakEven(Instrument instrument, int bufferTicks)`)

**Insert (new lines):**
```csharp

        // B33 BUG-B33-02: cancel ATM bracket orders that remain Working after PTT-BE fills.
        // NT8 internal sim accounts (Sim101/Sim102) do NOT auto-cancel ATM brackets on position close.
        // Real brokers auto-cancel; this method handles the sim gap without affecting live accounts.
        // JS-021: no lock. CYC=3: null guard(1), Where filter(2), Count==0 guard(3).
        // NT8-006: requires using System.Linq (confirmed present at CopyEngine.cs:29).
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
            try
            {
                leaderAcc.Cancel(stale.ToArray());
                NinjaTrader.Code.Output.Process(
                    "[BE] CancelStaleBrackets: cancelled " + stale.Count + " bracket orders",
                    PrintTo.OutputTab1);
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process("[BE] CancelStaleBrackets EXCEPTION -- " + ex.Message, PrintTo.OutputTab1);
            }
        }
```

**Rationale:** Cancels all Working/Accepted orders for the leader account+instrument (excluding the PTT-BE-Stop itself) after position goes flat; required for NT8 sim accounts which do not auto-cancel ATM brackets.

---

### Change 7 — TryFirePositionState hook: add `CancelStaleBrackets` call

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Lines:** 740–741

**Old:**
```csharp
            if (!hasPos)
                OrphanCancelGuard(e.Order.Account, e.Order.Instrument);
```

**New:**
```csharp
            if (!hasPos)
            {
                OrphanCancelGuard(e.Order.Account, e.Order.Instrument);
                CancelStaleBrackets(e.Order.Account, e.Order.Instrument);
            }
```

**Rationale:** After position goes flat, cancel the orphan BE stop (existing) then sweep any remaining bracket orders (new).

---

### Change 8 — Build tag

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Line:** 41

**Old:**
```csharp
        internal const string Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-20";
```

**New:**
```csharp
        internal const string Tag = "PTT-COPIER B33 | 1b-dict-BE | 2026-07-21";
```

**Rationale:** Build tag change confirms 1b patch is live in NT8 Output tab.

---

### Change 9 — `PendingBeStop_FieldExists_And_InitialValueIsNull` test

**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Lines:** 2752–2765 (replace entire test body)

**Old:**
```csharp
        // B33 DW-B33-01: _pendingBeStop field exists and initializes to null
        [Fact]
        public void PendingBeStop_FieldExists_And_InitialValueIsNull()
        {
            var fi = typeof(CopyEngine).GetField(
                "_pendingBeStop",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(fi);
            // Field type must be NinjaTrader.Cbi.Order
            Assert.Equal(typeof(NinjaTrader.Cbi.Order), fi.FieldType);
            // Initial value on singleton instance must be null (no BE armed at startup)
            var value = fi.GetValue(CopyEngine.Instance);
            Assert.Null(value);
        }
```

**New:**
```csharp
        // B33 BUG-B33-03: _pendingBeStop is now per-account ConcurrentDictionary, not singleton Order
        [Fact]
        public void PendingBeStop_FieldExists_And_IsConcurrentDictionary()
        {
            var fi = typeof(CopyEngine).GetField(
                "_pendingBeStop",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(fi);
            // Field type must be ConcurrentDictionary<string, NinjaTrader.Cbi.Order>
            Assert.Equal(
                typeof(System.Collections.Concurrent.ConcurrentDictionary<string, NinjaTrader.Cbi.Order>),
                fi.FieldType);
            // Initial value on singleton instance must be an empty dictionary (no BE armed at startup)
            var value = fi.GetValue(CopyEngine.Instance)
                as System.Collections.Concurrent.ConcurrentDictionary<string, NinjaTrader.Cbi.Order>;
            Assert.NotNull(value);
            Assert.Empty(value);
        }
```

**Rationale:** The field type changed from `Order` to `ConcurrentDictionary<string,Order>`; the test must verify the new type and empty-at-startup invariant.

---

## Section 5: Verification Checklist for Lane C

After Lane B completes all 9 changes, Lane C verifies using this 12-item checklist.

| # | Check | Pass Criterion |
|---|-------|---------------|
| V1 | Build tag visible | NT8 Output tab shows `PTT-COPIER B33 | new-stop BE | 2026-07-21` on first chart inject |
| V2 | `_pendingBeStop` field type | `grep "_pendingBeStop"` at line ~162 shows `ConcurrentDictionary<string, Order>` not `volatile Order` |
| V3 | SubmitBeStop duplicate guard uses TryGetValue | Line ~1563: contains `TryGetValue(leaderAcc.Name, out var existing)` |
| V4 | SubmitBeStop assign stores in dict, submits local var | Lines ~1573 and ~1580: uses `beStop` local var; `_pendingBeStop[leaderAcc.Name] = beStop` present |
| V5 | OrphanCancelGuard uses TryGetValue + TryRemove | Line ~1599: `TryGetValue(acc.Name, out var stop)`; line ~1601: `TryRemove(acc.Name, out _)` |
| V6 | OrphanCancelGuard Cancel uses local `stop` not field | Line ~1608: `acc.Cancel(new Order[] { stop })` not `{ _pendingBeStop }` |
| V7 | `CancelStaleBrackets` method exists | `grep "private void CancelStaleBrackets"` returns one match between OrphanCancelGuard close and BreakEven |
| V8 | `CancelStaleBrackets` called from TryFirePositionState | Lines 740–744: block `{ OrphanCancelGuard(...); CancelStaleBrackets(...); }` |
| V9 | No `volatile` on `_pendingBeStop` | `grep "volatile.*_pendingBeStop"` returns zero matches |
| V10 | No `lock(` introduced anywhere | `grep "lock(" src/PropTraderTools/CopyEngine.cs` returns zero new matches |
| V11 | Unit test updated and renamed | `CopyEngineTests.cs` contains `PendingBeStop_FieldExists_And_IsConcurrentDictionary` not `...InitialValueIsNull` |
| V12 | Hard-link sync run | `powershell -File scripts\verify_links.ps1 -Fix` executed after all edits; exits with 0 |

### Live Test Procedure (after V1–V12 pass)

1. Recompile — confirm Output: `PTT-COPIER B33 | 1b-dict-BE | 2026-07-21`
2. Open two charts: Sim101 Long, Sim102 Short
3. Enter positions on both accounts simultaneously
4. Click BE on Sim101 → arm
5. Click BE on Sim102 → arm independently (must not overwrite Sim101)
6. Output must show TWO `[BE] SubmitBeStop` lines (one per account)
7. Sim101 fills → Output: `[BE] OrphanCancelGuard fired` + `[BE] CancelStaleBrackets: cancelled N bracket orders`
8. Verify Sim101 Orders tab empty after fill
9. Sim102 fills → same confirmation
10. Orphan test: arm both, manually flatten one → only that account's OrphanCancelGuard fires; other still armed

---

*End of B33 Phase 1b Diff Plan*
