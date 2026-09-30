# B66-LaneC Tickets
**Plan**: `docs/brain/B66-LaneC/02-architecture-plan.md` (REVIEW_PASS)
**Review**: `docs/brain/B66-LaneC/02-plan-review.md` (REVIEW_PASS — no violations)
**Block**: B66-LaneC
**Ticket count**: 1
**Status**: TICKETS_COMPLETE

---

## T1 — Fix HandleEntryChange for StopLimit drag-sync

### Ticket ID
**T1 — B66-LaneC**

### Title
Fix three independent gates in `CopyEngine.cs` that together completely block StopLimit entry drag-sync

---

### Spec Requirement IDs

| ID | Priority | Description |
|----|----------|-------------|
| DW-B64-01 (partial) | P0 | `HandleEntryChange` never fires for StopLimit entry orders — three independent code path failures |
| Defect 1 | P0 | Gate C type guard excludes StopLimit (line 669) |
| Defect 2 | P0 | `FindFollowerEntryOrder` excludes StopLimit type and Accepted state (lines 986-988) |
| Defect 3 | P0 | `HandleEntryChange` reads and writes wrong price field (`LimitPrice`) for StopLimit (lines 1007, 1024, 1030) |
| DW-B66-C-02 | P1 | DispatchCopy dedup key = 0.0 for all StopLimit entries — **DO NOT FIX in this ticket. Document only.** Track in `docs/brain/B66-LaneC/06-deferred-backlog.md`. Target block: B67+. |

---

### Files to Modify

| Action | File |
|--------|------|
| MODIFY | `src/PropTraderTools/CopyEngine.cs` |
| CREATE | `src/PropTraderTools/Tests/CopyEngineB66Tests.cs` |

---

### Pre-conditions (engineer must verify before starting)

1. Read `docs/brain/B66-LaneC/02-architecture-plan.md` — REVIEW_PASS confirmed by `02-plan-review.md`.
2. Read `docs/brain/B66-LaneC/02-plan-review.md` — confirms no violations, no open issues.
3. Run `dotnet build src/PropTraderTools/PropTraderTools.csproj` — must be **zero errors** before touching any file. If build fails, stop and report; do not proceed.
4. Confirm Gate C exists at expected location: `grep -n "Gate C (B62)" src/PropTraderTools/CopyEngine.cs` — must return lines ~665-678.
5. Confirm `FindFollowerEntryOrder` is at expected location: `grep -n "FindFollowerEntryOrder" src/PropTraderTools/CopyEngine.cs` — method body must be at lines ~980-992.
6. Confirm `HandleEntryChange` is at expected location: `grep -n "HandleEntryChange" src/PropTraderTools/CopyEngine.cs` — method body must begin at lines ~1000-1007.
7. **Never fix DW-B66-C-02** (DispatchCopy Gate 5 / `IsDedup` dedup key = 0.0) in this ticket. Scope creep = protocol violation (AGENTS.md Section 11).

---

### NT8 Ground Truth (engineer must know before coding)

These facts are verified from primary NT8 sources. No NT8 claim is made from memory.

| Fact | Statement | Primary Source |
|------|-----------|---------------|
| Fact 1 | `StopLimit.LimitPrice == 0` always. All drag price lives in `StopPrice`. | `V12_002.Orders.Callbacks.Propagation.cs` line 209; confirmed `CopyEngine.cs` line 1734 |
| Fact 2 | `Account.Change()` for StopLimit: assign `fo.StopPrice`, not `fo.LimitPrice`. Writing `LimitPrice` for StopLimit does not change the broker trigger price. | `docs/standards/NT8_FULL_REFERENCE.md` lines 898-899: "StopPriceChanged — A double value representing the new stop price of an order. Used with Account.Change()" |
| Fact 3 | In real-time, broker-simulated StopLimit orders may only reach `OrderState.Accepted` and never transition to `Working`. | `docs/standards/NT8_FULL_REFERENCE.md` lines 1005-1006: "In real-time, some stop orders may only reach 'Accepted' state if they are simulated/held on a brokers server." |
| Fact 4 (deferred) | `DispatchCopy` line 805 passes `order.LimitPrice` to `IsDedup`. Since StopLimit.LimitPrice == 0 always, all StopLimit entries share dedup key 0.0. **Do NOT fix here.** Tracked as DW-B66-C-02. | `src/PropTraderTools/CopyEngine.cs` line 805 |

---

### New Private Static Helper Signatures

Both helpers must be placed **immediately before** `FindFollowerEntryOrder` (currently line ~980).
Add one blank line before the first helper block and one blank line after the second helper block.

```csharp
        // CYC=2. Returns StopPrice for StopLimit orders, LimitPrice for all others.
        // NT8 fact: StopLimit.LimitPrice==0 always; drag price lives in StopPrice (Fact 1).
        // B66-LaneC: DW-B64-01 fix -- GetOrderPrice used in Gate C and HandleEntryChange.
        // JS-021: no lock. JS-001: no throw. Pure computation. Zero heap allocation (JS-036).
        private static double GetOrderPrice(Order order)
            => order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;

        // CYC=2. Sets StopPrice for StopLimit follower orders, LimitPrice for all others.
        // NT8: for Account.Change() on StopLimit, assign StopPrice not LimitPrice
        //   (NT8_FULL_REFERENCE.md lines 898-899, Fact 2).
        // B66-LaneC: DW-B64-01 fix -- SetFollowerPrice replaces direct fo.LimitPrice assignment.
        // JS-021: no lock. JS-001: no throw. Pure field assignment.
        private static void SetFollowerPrice(Order fo, double newPrice)
        {
            if (fo.OrderType == OrderType.StopLimit)
                fo.StopPrice = newPrice;
            else
                fo.LimitPrice = newPrice;
        }
```

---

### Step-by-Step Implementation

Execute steps 1-6 in order. Do not skip. Report each step's result in `ticket-1-completion.md`.

---

#### STEP 1 — Gate C (lines 665-678): Widen type guard and fix price read

Locate the Gate C block by grepping: `grep -n "Gate C (B62)" src/PropTraderTools/CopyEngine.cs`

Replace the **entire** Gate C block (all lines from the `// Gate C (B62)` comment through the closing `}` of the outer if, inclusive):

**BEFORE** (current source — confirm exact match before replacing):
```csharp
            // Gate C (B62): entry drag detection -- same orderId + new LimitPrice = leader dragged.
            // Fires when state is Accepted or Working (the two states that carry updated price post-drag).
            // Only for Limit orders (Market orders have no LimitPrice to track).
            // _dedupCache.TryGetValue: orderId was previously dispatched; compare stored price.
            if (e.Order.OrderType == OrderType.Limit
                && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))
            {
                if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)
                    && Math.Abs(e.Order.LimitPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01))
                {
                    HandleEntryChange(e.Order, matchedRule.Value);
                    return;
                }
            }
```

**AFTER** (exact replacement):
```csharp
            // Gate C (B62/B66-LaneC): entry drag detection -- same orderId + new price = leader dragged.
            // Fires when state is Accepted or Working (the two states that carry updated price post-drag).
            // Widened in B66-LaneC to accept StopLimit in addition to Limit (DW-B64-01 fix).
            // NT8: StopLimit.LimitPrice==0 always; drag price lives in StopPrice -- use GetOrderPrice().
            // _dedupCache.TryGetValue: orderId was previously dispatched; compare stored price.
            if ((e.Order.OrderType == OrderType.Limit || e.Order.OrderType == OrderType.StopLimit)
                && (e.Order.OrderState == OrderState.Accepted || e.Order.OrderState == OrderState.Working))
            {
                double currentPrice = GetOrderPrice(e.Order);
                if (_dedupCache.TryGetValue(e.Order.OrderId.ToString(), out double storedPrice)
                    && Math.Abs(currentPrice - storedPrice) >= (e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.01))
                {
                    HandleEntryChange(e.Order, matchedRule.Value);
                    return;
                }
            }
```

**Verify after Step 1**: `grep -n "GetOrderPrice" src/PropTraderTools/CopyEngine.cs` must return at least 1 hit in Gate C.

---

#### STEP 2 — Add GetOrderPrice and SetFollowerPrice helpers

Locate `FindFollowerEntryOrder`:
```
grep -n "private static Order? FindFollowerEntryOrder" src/PropTraderTools/CopyEngine.cs
```

Insert the two helpers **immediately before** that method declaration (above its XML doc comment if one exists, or above the method comment block). The insertion must be:

```csharp

        // CYC=2. Returns StopPrice for StopLimit orders, LimitPrice for all others.
        // NT8 fact: StopLimit.LimitPrice==0 always; drag price lives in StopPrice (Fact 1).
        // B66-LaneC: DW-B64-01 fix -- GetOrderPrice used in Gate C and HandleEntryChange.
        // JS-021: no lock. JS-001: no throw. Pure computation. Zero heap allocation (JS-036).
        private static double GetOrderPrice(Order order)
            => order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice;

        // CYC=2. Sets StopPrice for StopLimit follower orders, LimitPrice for all others.
        // NT8: for Account.Change() on StopLimit, assign StopPrice not LimitPrice
        //   (NT8_FULL_REFERENCE.md lines 898-899, Fact 2).
        // B66-LaneC: DW-B64-01 fix -- SetFollowerPrice replaces direct fo.LimitPrice assignment.
        // JS-021: no lock. JS-001: no throw. Pure field assignment.
        private static void SetFollowerPrice(Order fo, double newPrice)
        {
            if (fo.OrderType == OrderType.StopLimit)
                fo.StopPrice = newPrice;
            else
                fo.LimitPrice = newPrice;
        }

```

**Verify after Step 2**: `grep -n "GetOrderPrice\|SetFollowerPrice" src/PropTraderTools/CopyEngine.cs` must return exactly 3 hits initially (1 definition GetOrderPrice, 1 definition SetFollowerPrice, 1 call site in Gate C from Step 1). After Steps 3-4, total will be 5 hits.

---

#### STEP 3 — FindFollowerEntryOrder (lines 986-989): Widen state and type guard

Locate the compound guard inside `FindFollowerEntryOrder`. Find and replace the inner `if` predicate block only (three guard conditions + `return order;`):

**BEFORE** (exact match required):
```csharp
                if (order.OrderState == OrderState.Working                        // (3)
                    && order.OrderType == OrderType.Limit
                    && order.Name == "PTT-Copy")
                    return order;
```

**AFTER**:
```csharp
                if ((order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted) // (3)
                    && (order.OrderType == OrderType.Limit || order.OrderType == OrderType.StopLimit)
                    && order.Name == "PTT-Copy")
                    return order;
```

Also update the method's comment block immediately above the method declaration. Find:
```csharp
        // CYC=3: foreach (1), instrument guard (2), state+name+type compound guard (3).
```
Replace with:
```csharp
        // CYC=3: foreach (1), instrument guard (2), state+type+name compound guard (3).
        // B66-LaneC: widened state to Working||Accepted, type to Limit||StopLimit (DW-B64-01).
        // NT8: broker-simulated StopLimit may stay in Accepted (NT8_FULL_REFERENCE.md line 1005).
```

**Verify after Step 3**: `grep -n "OrderState.Accepted" src/PropTraderTools/CopyEngine.cs` must return hits in both Gate C (Step 1) and `FindFollowerEntryOrder` (Step 3). `grep -n "OrderType.StopLimit" src/PropTraderTools/CopyEngine.cs` must return hits in Gate C, both helpers (Step 2), and `FindFollowerEntryOrder`.

---

#### STEP 4 — HandleEntryChange: Fix rawPrice, currentPrice, and follower price write

Locate `HandleEntryChange` via: `grep -n "HandleEntryChange" src/PropTraderTools/CopyEngine.cs` (look for the method definition, not call sites).

**4a. Update the method comment block** (at lines ~997-999). Find:
```csharp
        // B62: sync a leader entry drag to all follower working PTT-Copy entry orders.
```
Replace with (preserve any surrounding comment lines that are not changed):
```csharp
        // B62/B66-LaneC: sync a leader entry drag to all follower working PTT-Copy entry orders.
        // B66-LaneC: widened to StopLimit via GetOrderPrice/SetFollowerPrice helpers (DW-B64-01).
        // CYC=6: instr null (1), tickSize ternary (2), foreach acc (3), acc null (4), fo null (5), price delta guard (6).
```
If the existing comment already contains a CYC=6 line, update only the first two lines and leave the CYC line unchanged.

**4b. Fix rawPrice at line ~1007.** Find:
```csharp
            double rawPrice = leaderOrder.LimitPrice;
```
Replace with:
```csharp
            double rawPrice = GetOrderPrice(leaderOrder); // B66-LaneC: StopLimit price in StopPrice
```

**4c. Fix currentPrice at line ~1024.** Find:
```csharp
                double currentPrice = fo.LimitPrice;
```
Replace with:
```csharp
                double currentPrice = GetOrderPrice(fo); // B66-LaneC: StopLimit price in StopPrice
```

**4d. Fix follower price write at line ~1030.** Find:
```csharp
                    fo.LimitPrice = newPrice;
```
Replace with:
```csharp
                    SetFollowerPrice(fo, newPrice); // B66-LaneC: StopLimit -> fo.StopPrice (NT8_FULL_REFERENCE.md lines 898-899)
```

**Do NOT touch** the `acc.Change(new Order[] { fo });` line on the line immediately after 4d. It stays exactly as-is.

**Verify after Step 4**: `grep -n "GetOrderPrice\|SetFollowerPrice" src/PropTraderTools/CopyEngine.cs` must now return exactly 5 hits: 2 definitions (helpers), 1 in Gate C, 2 in `HandleEntryChange` (GetOrderPrice called twice, SetFollowerPrice called once = 3 call sites; plus 2 definitions = 5 total).

---

#### STEP 5 — Create CopyEngineB66Tests.cs

Create `src/PropTraderTools/Tests/CopyEngineB66Tests.cs` with exactly the following structure and content.

**Class**: `CopyEngineB66CTests`
**Namespace**: `PropTraderTools.Tests`
**Framework**: xUnit `[Fact]` only. No `[Theory]`, `[Test]`, `[TestMethod]`, `[DataRow]`.
**Mirror pattern**: Follow the same reflection / test-double / internal-accessor pattern used in the existing B62Tests file (`grep -rn "CopyEngineB62" src/PropTraderTools/Tests/` to locate and read the B62 tests file before writing).

The file must contain **exactly 8 `[Fact]` tests** with the method names and assertions described below.

| ID | Method Name | What It Asserts |
|----|-------------|-----------------|
| T_B66_C_01 | `T_B66_C_01_GateC_Fires_Limit_Working` | Gate C still fires for `OrderType.Limit + OrderState.Working` leader with cached price change >= 1 tick. Regression guard: Limit path must not be broken by widening to StopLimit. Setup: stub `Order` with `OrderType=Limit`, `OrderState=Working`, `LimitPrice` differing from stored dedup-cache value by >= 1 tick. Assert `HandleEntryChange` invoked (observable via spy or side effect). |
| T_B66_C_02 | `T_B66_C_02_GateC_Fires_StopLimit_Working` | Gate C fires for `OrderType.StopLimit + OrderState.Working` leader with `StopPrice` delta >= 1 tick (previously blocked). Setup: `OrderType=StopLimit`, `OrderState=Working`, `LimitPrice=0.0`, `StopPrice` changed by >= 1 tick from stored cache value. Assert `HandleEntryChange` invoked. |
| T_B66_C_03 | `T_B66_C_03_GateC_Fires_StopLimit_Accepted` | Gate C fires for `OrderType.StopLimit + OrderState.Accepted`. Addresses broker-server-held order scenario (NT8_FULL_REFERENCE.md line 1005). Setup: `OrderType=StopLimit`, `OrderState=Accepted`, `StopPrice` delta >= 1 tick. Assert `HandleEntryChange` invoked. |
| T_B66_C_04 | `T_B66_C_04_FindFollower_Working_Limit` | `FindFollowerEntryOrder` returns a `Working + Limit + "PTT-Copy"` order. Regression guard. Setup: list with one order `{OrderState=Working, OrderType=Limit, Name="PTT-Copy"}`. Assert returned order is not null and equals the input order. |
| T_B66_C_05 | `T_B66_C_05_FindFollower_Working_StopLimit` | `FindFollowerEntryOrder` returns a `Working + StopLimit + "PTT-Copy"` order (previously returned null — Limit-only type guard). Setup: `{OrderState=Working, OrderType=StopLimit, Name="PTT-Copy"}`. Assert non-null return matching input order. |
| T_B66_C_06 | `T_B66_C_06_FindFollower_Accepted_StopLimit` | `FindFollowerEntryOrder` returns an `Accepted + StopLimit + "PTT-Copy"` order (previously returned null — double exclusion: wrong state AND wrong type). Addresses broker-server-held scenario. Setup: `{OrderState=Accepted, OrderType=StopLimit, Name="PTT-Copy"}`. Assert non-null return matching input order. |
| T_B66_C_07 | `T_B66_C_07_GetOrderPrice_Returns_StopPrice_For_StopLimit` | `GetOrderPrice` returns `order.StopPrice` (not `order.LimitPrice`) when `order.OrderType == OrderType.StopLimit`. Confirms Gate C and HandleEntryChange line 1007 read the correct field for StopLimit (NT8 Fact 1: LimitPrice is always 0 for StopLimit). Setup: stub `{OrderType=StopLimit, LimitPrice=0.0, StopPrice=4500.25}`. Assert `GetOrderPrice(order) == 4500.25`. Assert result `!= 0.0`. Also assert `GetOrderPrice` for a `Limit` order with `LimitPrice=4499.75` returns `4499.75`. |
| T_B66_C_08 | `T_B66_C_08_SetFollowerPrice_Sets_StopPrice_For_StopLimit` | `SetFollowerPrice` writes to `fo.StopPrice` (not `fo.LimitPrice`) when `fo.OrderType == OrderType.StopLimit`. Confirms HandleEntryChange line 1030 submits the correct field to `acc.Change()` (NT8 Fact 2). Setup: stub follower `{OrderType=StopLimit, LimitPrice=0.0, StopPrice=4500.00}`. Call `SetFollowerPrice(fo, 4501.25)`. Assert `fo.StopPrice == 4501.25`. Assert `fo.LimitPrice == 0.0` (unchanged). |

**Verify after Step 5**: `grep -n "T_B66_C_0" src/PropTraderTools/Tests/CopyEngineB66Tests.cs` must return **exactly 8 lines** (T_B66_C_01 through T_B66_C_08).

---

#### STEP 6 — Build and test

```powershell
# Must complete with 0 errors, 0 warnings in new/modified files:
dotnet build src/PropTraderTools/PropTraderTools.csproj

# All 8 new tests must pass:
dotnet test src/PropTraderTools/Tests/ --filter "T_B66_C_0"
```

If build fails: stop, report error output in `ticket-1-completion.md`, do not commit.
If any test fails: stop, report failing test name and assertion error, do not commit.

---

### 7-Scan Checklist (engineer must run ALL 7 and record results in ticket-1-completion.md)

The engineer contract: every scan must pass before committing. Report each scan's output verbatim.

```
SCAN 1 — lock() ban (JS-021)
  grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  MUST: return 0 new hits in any modified or added line (entire file check; existing hits
        from pre-existing code are acceptable if unchanged)

SCAN 2 — throw new ban (JS-001)
  grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  MUST: return 0 hits in any new or modified method (Gate C, GetOrderPrice,
        SetFollowerPrice, FindFollowerEntryOrder, HandleEntryChange)

SCAN 3 — test count verification
  grep -n "T_B66_C_0" src/PropTraderTools/Tests/CopyEngineB66Tests.cs
  MUST: return exactly 8 lines (T_B66_C_01, T_B66_C_02, T_B66_C_03, T_B66_C_04,
        T_B66_C_05, T_B66_C_06, T_B66_C_07, T_B66_C_08)

SCAN 4 — async void ban (JS-033)
  grep -n "async void" src/PropTraderTools/CopyEngine.cs
  MUST: return 0 hits in any new or modified code

SCAN 5 — ASCII-only compliance
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  MUST: return 0 hits in any new or modified line

SCAN 6 — build gate
  dotnet build src/PropTraderTools/PropTraderTools.csproj
  MUST: exit with 0 errors, 0 warnings in new/modified files

SCAN 7 — CYC complexity audit
  python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
  MUST: report CYC <= 8 for all five targets:
    - Gate C block
    - FindFollowerEntryOrder
    - HandleEntryChange
    - GetOrderPrice
    - SetFollowerPrice
```

**Scan failure protocol**: If any scan returns a violation, stop. Do not commit. Report the exact scan output and failing line in `ticket-1-completion.md` under a `## BLOCKED` section.

---

### Test Specifications (complete table)

| ID | Test Method | Class | File | What it asserts |
|----|-------------|-------|------|----------------|
| T_B66_C_01 | `T_B66_C_01_GateC_Fires_Limit_Working` | `CopyEngineB66CTests` | `src/PropTraderTools/Tests/CopyEngineB66Tests.cs` | Gate C invokes HandleEntryChange for Limit+Working leader with cached price change >= 1 tick (regression guard) |
| T_B66_C_02 | `T_B66_C_02_GateC_Fires_StopLimit_Working` | `CopyEngineB66CTests` | same | Gate C invokes HandleEntryChange for StopLimit+Working leader with StopPrice change >= 1 tick (new path) |
| T_B66_C_03 | `T_B66_C_03_GateC_Fires_StopLimit_Accepted` | `CopyEngineB66CTests` | same | Gate C invokes HandleEntryChange for StopLimit+Accepted leader with StopPrice change >= 1 tick (broker-held order) |
| T_B66_C_04 | `T_B66_C_04_FindFollower_Working_Limit` | `CopyEngineB66CTests` | same | FindFollowerEntryOrder returns Working+Limit "PTT-Copy" order (regression guard) |
| T_B66_C_05 | `T_B66_C_05_FindFollower_Working_StopLimit` | `CopyEngineB66CTests` | same | FindFollowerEntryOrder returns Working+StopLimit "PTT-Copy" order (previously null) |
| T_B66_C_06 | `T_B66_C_06_FindFollower_Accepted_StopLimit` | `CopyEngineB66CTests` | same | FindFollowerEntryOrder returns Accepted+StopLimit "PTT-Copy" order (previously null — double exclusion) |
| T_B66_C_07 | `T_B66_C_07_GetOrderPrice_Returns_StopPrice_For_StopLimit` | `CopyEngineB66CTests` | same | GetOrderPrice returns order.StopPrice when OrderType==StopLimit; returns LimitPrice for Limit orders |
| T_B66_C_08 | `T_B66_C_08_SetFollowerPrice_Sets_StopPrice_For_StopLimit` | `CopyEngineB66CTests` | same | SetFollowerPrice sets fo.StopPrice when fo.OrderType==StopLimit; fo.LimitPrice remains 0.0 (unchanged) |

---

### JS-DNA Compliance Summary

| Rule | Constraint | How satisfied |
|------|-----------|---------------|
| JS-021 | No `lock()` anywhere | All new code is pure conditional expressions and field reads/writes on `Order` objects. No synchronization primitives. `_dedupCache` is existing `ConcurrentDictionary`, unchanged. |
| JS-001 | No `throw new XxxException` in hot paths | No throws in Gate C block, GetOrderPrice, SetFollowerPrice, FindFollowerEntryOrder guard widening, or HandleEntryChange fix lines. |
| JS-002 | `return null` documented | `FindFollowerEntryOrder` existing `return null` at end-of-method is unchanged. Fix broadens the match predicate (reduces nulls, never adds new null paths). Existing XML doc comment (unchanged) documents the null-return contract. |
| JS-033 | No `async void` (non-event-handler) | Both new helpers are synchronous `private static` methods. No async introduced. |
| JS-036 | No heap allocation in hot path | `GetOrderPrice` returns a stack `double`. `double currentPrice = GetOrderPrice(...)` is stack-local. Zero heap allocation. |
| ASCII-only | No Unicode in identifiers or string literals | New identifiers: `GetOrderPrice`, `SetFollowerPrice`, `currentPrice`. All ASCII. No string literals changed. `"PTT-Copy"` is existing, unchanged, ASCII. |
| DateTime.UtcNow | No `DateTime.Now` | No timestamp access in any proposed change. |
| CYC <= 8 | All modified methods | Gate C: 3; FindFollowerEntryOrder: 3-5 (both <= 8 under either counting convention); HandleEntryChange: 6 (unchanged); GetOrderPrice: 2; SetFollowerPrice: 2. |
| `[Fact]` only | No NUnit or MSTest | All 8 tests use `[Fact]`. No `[Test]`, `[TestMethod]`, `[Theory]`, `[DataRow]`. |

---

### Commit Message (exact format)

```
git add src/PropTraderTools/
git commit -m "fix(ptt): B66-LaneC -- HandleEntryChange StopLimit drag fix [8 tests]"
```

---

### Definition of Done

All items must be checked before reporting TICKETS_COMPLETE:

- [ ] **Step 1 applied**: Gate C block replaced — type guard widened to `Limit || StopLimit`, price comparison uses `GetOrderPrice()` via `currentPrice` local
- [ ] **Step 2 applied**: `GetOrderPrice` and `SetFollowerPrice` helpers inserted immediately before `FindFollowerEntryOrder`
- [ ] **Step 3 applied**: `FindFollowerEntryOrder` inner guard widened — `Working || Accepted` state, `Limit || StopLimit` type; method comment updated with B66-LaneC note
- [ ] **Step 4 applied**: All three HandleEntryChange fix lines applied — 4b (`rawPrice`), 4c (`currentPrice`), 4d (`SetFollowerPrice`); method comment updated
- [ ] **All 7 scans pass** — zero violations reported; scan output recorded in `ticket-1-completion.md`
- [ ] **8 new tests pass** — T_B66_C_01 through T_B66_C_08; all xUnit `[Fact]`; `dotnet test` exits 0
- [ ] **`dotnet build` zero errors** — zero new errors or warnings in modified/created files
- [ ] **DW-B66-C-02 NOT touched** — DispatchCopy Gate 5 / `IsDedup` dedup key unchanged; deferred to B67+
- [ ] **`ticket-1-completion.md` written** — includes scan results table and test run output
- [ ] **Commit pushed** with exact commit message above

---

*Tickets written by ptt-architect (B66-LaneC Phase 3).
Architecture plan: REVIEW_PASS (`02-plan-review.md`).
Deferred item DW-B66-C-02 documented in `06-deferred-backlog.md` — do not implement in this ticket.*
