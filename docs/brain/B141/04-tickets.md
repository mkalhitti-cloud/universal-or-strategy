# B141 Tickets — OCO Cascade Dual-Resubmit

**Block**: B141
**Phase**: Ticket Generation (Phase 3)
**Author**: ptt-architect
**Plan Status**: REVIEW_PASS (Revision Cycle 1)
**Produced**: 2026-09-01
**Plan file**: `docs/brain/B141/02-architecture-plan.md`

---

## Ticket Index

| Ticket | Title | File | Status |
|--------|-------|------|--------|
| T1 | OCO Cascade Dual-Resubmit — SyncFollowerBracket + 4 new helpers + 7 xUnit tests | `src/PropTraderTools/CopyEngine.cs` | PENDING |

---

## T1 — OCO Cascade Dual-Resubmit

### Spec Requirements Closed

| ID | Title | Closure Mechanism |
|----|-------|-------------------|
| **DW-B153** | OCO cascade kills Target1/Target2/Target3 on stop drag | T1 dual-resubmit: capture target price before cancel, resubmit PTT-TGT-Drag after cascade |
| **DW-B154** | `acc.Change()` confirmed no-op on ATM Stop brackets (DOCUMENTED) | T1 accepts this NT8 constraint; cancel+resubmit is the correct AddOn pattern |

### File Target

**File**: `src/PropTraderTools/CopyEngine.cs`

**Method modified**: `SyncFollowerBracket` — branch (3) only (lines 2281–2285)
**New methods added**: `CaptureLinkedTargetPrice`, `TryParseStopSuffix`, `IsTargetOrderLive`, `ResubmitTargetAfterCascade` (all private, added after `SyncFollowerBracket`)

**Test file created**: `tests/PropTraderTools.Tests/B141Tests.cs`

---

### Change 1: Modify `SyncFollowerBracket` Branch (3)

**Current code** (lines 2281–2285, verified by mandatory read):

```csharp
            if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137
            {
                SyncAtmFollowerBracket(acc, fo, newPrice); // cancel+resubmit (acc.Change is no-op on ATM brackets)
                return;
            }
```

**Replacement code** (exact — engineer MUST match this verbatim):

```csharp
            if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134 + DW-B137 + DW-B153
            {
                double? capturedTargetPrice = CaptureLinkedTargetPrice(acc, fo.Name); // B141: capture before cascade
                SyncAtmFollowerBracket(acc, fo, newPrice);   // cascade kills linked target (accepted, by design)
                if (capturedTargetPrice.HasValue)            // B141: +1 branch -> CYC 8 (at limit -- no further branching may be added)
                    ResubmitTargetAfterCascade(acc, fo, capturedTargetPrice.Value, leaderOrder);
                return;
            }
```

**Invariants**:
- `SyncAtmFollowerBracket` is ALWAYS called unconditionally — preserves existing stop-price-update behavior (regression contract; T_B141_07).
- `ResubmitTargetAfterCascade` is called ONLY when `capturedTargetPrice.HasValue` — no resubmit when target was already absent/cancelled (T_B141_06).
- `leaderOrder` is already in scope in `SyncFollowerBracket` (confirmed: used at line 2288 in branch 3b).
- Do NOT touch any other branch in `SyncFollowerBracket`. Branch (3b) at line 2286 is UNCHANGED.

**CYC impact on `SyncFollowerBracket`** (post-B141):

| # | Branch element | Line | +N | Running |
|---|----------------|------|----|---------|
| — | base | — | +1 | 1 |
| 1 | `if (fo == null)` | 2269 | +1 | 2 |
| 2 | `if (Math.Abs(...) < tickSize)` | 2273 | +1 | 3 |
| 3 | `if (isStop && IsAtmSTPOrder(fo))` — `&&` NOT counted (project convention) | 2281 | +1 | 4 |
| 3b | `if (!isStop && IsAtmSTPOrder(fo))` — `&&` NOT counted | 2286 | +1 | 5 |
| 4 | `if (isStop && IsTrailingStop(fo))` — `&&` NOT counted | 2292 | +1 | 6 |
| 5 | `if (isStop)` inside try | 2300 | +1 | 7 |
| — | `catch` | 2313 | **0** (project convention) | 7 |
| B141 | `if (capturedTargetPrice.HasValue)` (new, inside branch 3 body) | new | +1 | **8** |

**CYC = 8 — PASS at JS-041 limit. Engineer MUST NOT add any further branches to this method.**

---

### Change 2: New Method `CaptureLinkedTargetPrice`

**Method signature** (exact):

```csharp
private double? CaptureLinkedTargetPrice(Account acc, string stopName)
```

**Full implementation** (engineer MUST match verbatim — CYC verified):

```csharp
        // CYC=4: base(1)+if(1)+foreach(1)+if(1). No lock. No async. ASCII-only.
        // B141: captures LimitPrice of the linked NT8 ATM target before Stop cancel+resubmit triggers OCO cascade.
        // "Stop1"->"Target1", "Stop2"->"Target2", "Stop3"->"Target3" (NT8 ATM naming, SIM log 2026-09-01).
        // Returns null if target not found (already cascade-cancelled) or suffix not 1/2/3.
        // JS-002 note: double? is a nullable VALUE type -- this is NOT a reference null return.
        private double? CaptureLinkedTargetPrice(Account acc, string stopName)
        {
            if (!TryParseStopSuffix(stopName, out string suffix)) // (1) if -- && NOT counted
                return null;
            string targetName = "Target" + suffix;
            foreach (var o in acc.Orders.ToList())                // (2) foreach
            {
                if (IsTargetOrderLive(o) && o.Name == targetName) // (3) if -- && NOT counted
                    return o.LimitPrice;
            }
            return null;
        }
```

**CYC = 4** (base:1 + if:1 + foreach:1 + if:1). `&&` not counted per project convention.

**NT8 API used**:
- `acc.Orders` → `IEnumerable<Order>` (NT8_ADDON_KNOWLEDGE.md line 219)
- `.ToList()` — snapshot to avoid enumeration-during-mutation issues
- `o.LimitPrice` → limit price property (NT8_ADDON_KNOWLEDGE.md line 226)
- `o.Name` → order name set at CreateOrder time (NT8_ADDON_KNOWLEDGE.md line 229)

**JS rule constraints**:
- JS-021: no `lock()` — acc.Orders enumeration is safe on NT8 dispatch thread
- JS-033: no `async void` — synchronous, returns `double?`
- JS-002: `double?` is a nullable VALUE type — the plan explicitly notes this is acceptable (Section 4.2)
- JS-041: CYC 4 <= 8 — PASS

---

### Change 3: New Method `TryParseStopSuffix`

**Method signature** (exact):

```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
```

**Full implementation** (engineer MUST match verbatim — CYC verified):

```csharp
        // CYC=3: base(1)+if(1)+if(1). Static. Pure predicate. No lock. No async.
        // B141: extracts suffix from NT8 ATM stop name ("Stop1"->"1", "Stop2"->"2", "Stop3"->"3").
        // Rejects null, length < 5, or suffix not in {1, 2, 3}.
        // Uses int.TryParse to accept only valid numeric suffixes 1-3.
        private static bool TryParseStopSuffix(string stopName, out string suffix)
        {
            suffix = null;
            if (stopName == null || stopName.Length < 5) // (1) if -- || NOT counted
                return false;
            string raw = stopName.Substring(4);
            if (!int.TryParse(raw, out int n) || n < 1 || n > 3) // (2) if -- || NOT counted
                return false;
            suffix = raw;
            return true;
        }
```

**CYC = 3** (base:1 + if:1 + if:1). `||` not counted per project convention.

**Parsing logic**:
- `stopName.Length < 5` — "Stop1" has length 5; rejects anything shorter.
- `stopName.Substring(4)` — extracts everything after "Stop" (index 4 onward): "Stop1" -> "1", "Stop10" -> "10".
- `!int.TryParse(raw, out int n) || n < 1 || n > 3` — accepts only integer suffixes 1, 2, or 3. Rejects "Stop10", "Stop0", "StopX".
- `suffix = null` initial assignment — standard NT8 .NET 4.8 `out` parameter pattern (non-nullable reference context).

**JS rule constraints**:
- JS-021: no `lock()` — static pure predicate
- JS-033: no `async void` — static bool
- JS-001: no throw — early returns only
- JS-041: CYC 3 <= 8 — PASS

---

### Change 4: New Method `IsTargetOrderLive`

**Method signature** (exact):

```csharp
private static bool IsTargetOrderLive(Order o)
```

**Full implementation** (engineer MUST match verbatim — CYC verified):

```csharp
        // CYC=1: base(1). Static. Pure state predicate. No lock. No async.
        // B141: returns true if order is Working or Accepted -- both are live states.
        // JS-002: bool return -- never null. || NOT counted per project convention.
        private static bool IsTargetOrderLive(Order o) =>
            o != null && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted);
```

**CYC = 1** (base:1 only). Pure boolean expression; no `if`, no branches. `&&` and `||` not counted per project convention.

**NT8 API used**:
- `OrderState.Working` — order active in market (NT8_FULL_REFERENCE.md lines 941-996)
- `OrderState.Accepted` — order accepted by broker (NT8_FULL_REFERENCE.md lines 941-996)

**JS rule constraints**:
- JS-002: returns bool — no null
- JS-021: no `lock()` — static pure predicate
- JS-041: CYC 1 <= 8 — PASS

---

### Change 5: New Method `ResubmitTargetAfterCascade`

**Method signature** (exact):

```csharp
private void ResubmitTargetAfterCascade(Account acc, Order stpOrder, double targetPrice, Order leaderOrder)
```

**Full implementation** (engineer MUST match verbatim — CYC verified):

```csharp
        // CYC=4: base(1)+foreach(1)+if(1)+if(1). No lock. No async. ASCII-only.
        // B141: after OCO cascade cancels linked ATM target, resubmits a standalone PTT-TGT-Drag
        // limit order at the captured price. Mirrors SyncAtmFollowerTarget Block A-Prime + Block B.
        // Block A-Prime: sweep stale PTT-TGT-Drag (prevents accumulation on consecutive drags -- DW-B139).
        // Block B: CreateOrder + Submit. oco="": PTT-TGT-Drag is NOT part of any ATM OCO group.
        // stpOrder.OrderAction: ATM brackets use matching exit action on both Stop and Target legs --
        //   e.g. LONG position: Stop=Sell, Target=Sell (both exit long). Use stpOrder.OrderAction directly.
        //   Confirmed by SyncAtmFollowerTarget Block B using fo.OrderAction where fo IS the target.
        //   Both stop and target legs of an ATM bracket share the same OrderAction direction.
        // JS-001: try/catch -- no throw in hot path. JS-021: no lock. NT8-007: arg12 cast guard.
        private void ResubmitTargetAfterCascade(
            Account acc,
            Order stpOrder,
            double targetPrice,
            Order leaderOrder)
        {
            // Block A-Prime: cancel any stale PTT-TGT-Drag for this instrument.
            // Mirrors SyncAtmFollowerTarget Block A-Prime (L2473-2490).
            // JS-021: no lock -- acc.Orders iteration safe on NT8 dispatch thread.
            foreach (var o in acc.Orders.ToList())                                      // (1) foreach
            {
                if (                                                                     // (2) if -- all && NOT counted
                    o.OrderState == OrderState.Working
                    && o.Name == "PTT-TGT-Drag"
                    && o.Instrument?.FullName == stpOrder.Instrument?.FullName
                )
                {
                    try
                    {
                        acc.Cancel(new Order[] { o });
                    }
                    catch (Exception ex)                                                 // catch = 0 (project convention)
                    {
                        StatusUpdate?.Invoke(acc.Name + ": TGT pre-cancel error (B141): " + ex.Message);
                    }
                }
            }

            // Block B: CreateOrder + Submit. Mirrors SyncAtmFollowerTarget Block B (L2502-2530).
            // JS-001: no throw -- absorb via StatusUpdate. NT8-007: arg12 = (NinjaTrader.Cbi.CustomOrder)null.
            try
            {
                var newTarget = acc.CreateOrder(
                    stpOrder.Instrument,
                    stpOrder.OrderAction,
                    OrderType.Limit,
                    OrderEntry.Automated,
                    TimeInForce.Day,
                    stpOrder.Quantity,
                    targetPrice,
                    0,
                    "",
                    "PTT-TGT-Drag",
                    NinjaTrader.Core.Globals.MaxDate,
                    (NinjaTrader.Cbi.CustomOrder)null
                );
                if (newTarget == null)                                                   // (3) if
                {
                    StatusUpdate?.Invoke(acc.Name + ": B141 TGT CreateOrder returned null");
                    return;
                }
                acc.Submit(new[] { newTarget });
                StatusUpdate?.Invoke(acc.Name + ": B141 TGT resubmit after cascade -> " + targetPrice);
            }
            catch (Exception ex)                                                         // catch = 0 (project convention)
            {
                StatusUpdate?.Invoke(acc.Name + ": B141 TGT create error: " + ex.Message);
            }
        }
```

**CYC = 4** (base:1 + foreach:1 + if:1 + if:1). All `&&` not counted; both `catch` blocks = 0 per project convention.

**`stpOrder.OrderAction` justification** (critical — engineer must read):

The plan (Section 4.3) explicitly directs use of `stpOrder.OrderAction` directly. The rationale:
ATM bracket Stop and Target legs both carry the same exit-direction `OrderAction` (e.g. for a LONG
position, Stop = `OrderAction.Sell`, Target = `OrderAction.Sell` — both exit the long). This is
confirmed by examining `SyncAtmFollowerTarget` Block B at lines 2505-2517: it uses `fo.OrderAction`
directly, where `fo` IS the target order. The stop order's `OrderAction` equals the target's
`OrderAction` for ATM brackets. No inversion is required.

**NT8 API used**:
- `acc.Orders` → `IEnumerable<Order>` (NT8_ADDON_KNOWLEDGE.md line 219)
- `acc.Cancel(Order[])` → cancels working order (NT8_ADDON_KNOWLEDGE.md line 222)
- `acc.CreateOrder(12 params)` → 12-parameter signature (NT8_FULL_REFERENCE.md line 2106)
- `(NinjaTrader.Cbi.CustomOrder)null` → arg12 CS1503 guard (NT8_ADDON_KNOWLEDGE.md line 262; NT8-007)
- `acc.Submit(IEnumerable<Order>)` → submits orders (NT8_FULL_REFERENCE.md line 2154)
- `NinjaTrader.Core.Globals.MaxDate` → order expiry (replaces DateTime.Now — BANNED)
- `OrderState.Working`, `OrderType.Limit`, `OrderEntry.Automated`, `TimeInForce.Day` — NT8 enums

**JS rule constraints**:
- JS-021: no `lock()` — acc.Orders iteration safe on NT8 dispatch thread
- JS-033: no `async void` — synchronous void
- JS-001: no throw — absorbed via try/catch + StatusUpdate
- JS-002: returns void — no null return concern
- **NT8-007**: arg12 must be `(NinjaTrader.Cbi.CustomOrder)null` — CS1503 guard
- **No DateTime.Now** — uses `NinjaTrader.Core.Globals.MaxDate`
- **PTT- prefix** — order name is `"PTT-TGT-Drag"` (compliant)
- **ASCII-only** — all string literals are ASCII
- JS-041: CYC 4 <= 8 — PASS

---

### Method Placement

Insert all four new methods as a contiguous block immediately after the closing brace of `SyncFollowerBracket` (approximately after line 2317, before the next method). Ordering:

1. `CaptureLinkedTargetPrice`
2. `TryParseStopSuffix`
3. `IsTargetOrderLive`
4. `ResubmitTargetAfterCascade`

This ordering places the top-level helper first, supporting helpers next, and the terminal resubmit method last — matching the call order in `SyncFollowerBracket`.

---

### 7-Scan Checklist (Engineer Contract — Mandatory Before PR)

All scans against modified and new methods only (branch 3 in `SyncFollowerBracket` + 4 new helpers).

**SCAN-01** — No `lock()`:
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "CaptureLinkedTargetPrice|TryParseStopSuffix|IsTargetOrderLive|ResubmitTargetAfterCascade|SyncFollowerBracket"
# Expected: 0 hits
```

**SCAN-02** — No `async void`:
```powershell
grep -n "async void" src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "CaptureLinkedTargetPrice|TryParseStopSuffix|IsTargetOrderLive|ResubmitTargetAfterCascade"
# Expected: 0 hits
```

**SCAN-03** — No `throw new` in hot paths:
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "CaptureLinkedTargetPrice|TryParseStopSuffix|IsTargetOrderLive|ResubmitTargetAfterCascade"
# Expected: 0 hits
```

**SCAN-04** — CYC verification (manual count — engineer must confirm line-by-line):

| Method | Expected CYC | Limit | Result |
|--------|-------------|-------|--------|
| `SyncFollowerBracket` (post-B141) | 8 | 8 | PASS — at limit |
| `CaptureLinkedTargetPrice` | 4 | 8 | PASS |
| `TryParseStopSuffix` | 3 | 8 | PASS |
| `IsTargetOrderLive` | 1 | 8 | PASS |
| `ResubmitTargetAfterCascade` | 4 | 8 | PASS |

Convention: base=1, each `if`/`foreach`/`for`/`while`/`?:`=+1, `&&`/`||`=0, `catch`=0.
Sources: L2250 comment ("CYC=7: fo null(1)..."), L2327 comment ("exception handlers add 0 McCabe branches each").

**SCAN-05** — ASCII-only string literals:
```powershell
# Scan new string literals in modified region for non-ASCII characters
[System.Text.RegularExpressions.Regex]::Matches((Get-Content src/PropTraderTools/CopyEngine.cs -Raw), '[^\x00-\x7F]').Count
# Expected: 0 new non-ASCII characters introduced by B141
```
New string literals to verify: `"Target"`, `"PTT-TGT-Drag"`, `"B141 TGT CreateOrder returned null"`, `"B141 TGT resubmit after cascade -> "`, `"B141 TGT create error: "`, `"TGT pre-cancel error (B141): "`.

**SCAN-06** — Build clean:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
# Expected: 0 errors, 0 CS1503 (arg12 cast guard), 0 CS0246 (missing type)
```

**SCAN-07** — Tests pass:
```powershell
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --filter "B141"
# Expected: 7/7 pass (T_B141_01 through T_B141_07)
```

---

### xUnit Tests

**Test file**: `tests/PropTraderTools.Tests/B141Tests.cs`
**Framework**: xUnit only — NEVER NUnit or MSTest (JS mandate)
**Count**: 7 `[Fact]` tests

Engineer MUST follow the established NT8 test double pattern from `B140Tests.cs` and prior blocks.
NT8 `Account` and `Order` types are NT8 platform classes; use the same stub/fake infrastructure
already present in the test project.

---

#### T_B141_01: `CaptureLinkedTargetPrice_Stop1_ReturnsTarget1LimitPrice`

**Asserts**:
- Arrange: `acc.Orders` contains exactly one `Order`: `Name="Target1"`, `OrderState=OrderState.Working`, `LimitPrice=4500.25`
- Act: `result = CaptureLinkedTargetPrice(acc, "Stop1")`
- Assert: `result.HasValue == true`
- Assert: `result.Value == 4500.25`
- **Confirms**: suffix parse `"Stop1"` → `"1"`, target lookup by `"Target1"`, `LimitPrice` returned correctly

---

#### T_B141_02: `CaptureLinkedTargetPrice_Stop2_ReturnsTarget2LimitPrice`

**Asserts**:
- Arrange: `acc.Orders` contains exactly one `Order`: `Name="Target2"`, `OrderState=OrderState.Accepted`, `LimitPrice=4510.50`
- Act: `result = CaptureLinkedTargetPrice(acc, "Stop2")`
- Assert: `result.HasValue == true`
- Assert: `result.Value == 4510.50`
- **Confirms**: `OrderState.Accepted` is also treated as live (not just `Working`); Stop2/Target2 pair

---

#### T_B141_03: `CaptureLinkedTargetPrice_Stop3_ReturnsTarget3LimitPrice`

**Asserts**:
- Arrange: `acc.Orders` contains exactly one `Order`: `Name="Target3"`, `OrderState=OrderState.Working`, `LimitPrice=4520.75`
- Act: `result = CaptureLinkedTargetPrice(acc, "Stop3")`
- Assert: `result.HasValue == true`
- Assert: `result.Value == 4520.75`
- **Confirms**: Stop3/Target3 pair handled correctly; all three suffix variants covered

---

#### T_B141_04: `CaptureLinkedTargetPrice_TargetAlreadyCancelled_ReturnsNull`

**Asserts**:
- Arrange: `acc.Orders` contains exactly one `Order`: `Name="Target1"`, `OrderState=OrderState.Cancelled`, `LimitPrice=4500.25`
- Act: `result = CaptureLinkedTargetPrice(acc, "Stop1")`
- Assert: `result.HasValue == false` (returns null)
- **Confirms**: `IsTargetOrderLive` predicate correctly excludes `Cancelled` state; cascade-already-cancelled scenario returns null

---

#### T_B141_05: `SyncFollowerBracket_AtmStop1Drag_ResubmitsPttTgtDrag_WhenTargetFound`

**Asserts**:
- Arrange: `acc.Orders` contains `"Target1"` in `Working` state at `LimitPrice=4500.25`; leader drags Stop1 to a new price
- Act: `SyncFollowerBracket` called with `fo` = ATM Stop1 order (`IsAtmSTPOrder(fo)=true`, `isStop=true`)
- Assert: `acc.CreateOrder` is called with `OrderType.Limit` and `name="PTT-TGT-Drag"` and `limitPrice=4500.25`
- Assert: `acc.Submit` is called with the new target order
- **Confirms**: end-to-end resubmit path executes when target price captured; `capturedTargetPrice.HasValue = true` branch taken

---

#### T_B141_06: `SyncFollowerBracket_AtmStop1Drag_NoResubmit_WhenTargetAbsent`

**Asserts**:
- Arrange: `acc.Orders` contains NO Target1 in `Working` or `Accepted` state (either absent, or `Cancelled`)
- Act: `SyncFollowerBracket` called with `fo` = ATM Stop1 order (`IsAtmSTPOrder(fo)=true`, `isStop=true`)
- Assert: `acc.CreateOrder` is NOT called with `name="PTT-TGT-Drag"` (the resubmit path is NOT triggered)
- **Confirms**: `capturedTargetPrice.HasValue == false` guard prevents `ResubmitTargetAfterCascade` call; no resubmit when target already absent

---

#### T_B141_07: `SyncFollowerBracket_AtmStop_SyncAtmFollowerBracketAlwaysCalled`

**Asserts**:
- Arrange scenario A: `acc.Orders` contains `"Target1"` in `Working` state (target found)
- Arrange scenario B: `acc.Orders` contains NO live Target1 (target absent)
- Act: `SyncFollowerBracket` called with ATM Stop1 in BOTH scenarios
- Assert scenario A: `SyncAtmFollowerBracket` is called (tracked via spy/mock/interceptor)
- Assert scenario B: `SyncAtmFollowerBracket` is called (tracked via spy/mock/interceptor)
- **Confirms**: `SyncAtmFollowerBracket` is unconditional — not gated on `capturedTargetPrice.HasValue`; regression guard for existing stop-price-update behavior

---

### SIM Verification Gates (Director — not engineer work)

After T1 merges and F5 compiles clean, Director runs:

| Gate | Procedure | Pass Criteria |
|------|-----------|--------------|
| **Gate 1 (P0 — BLOCKING merge)** | Drag leader Stop1; observe follower Order Grid | (1) Follower Stop1 updates to new price via PTT-STP-Drag. (2) Target1 initially cascade-cancelled (expected). (3) New `PTT-TGT-Drag` limit order appears at ORIGINAL Target1 price. (4) StatusUpdate shows `"B141 TGT resubmit after cascade -> [price]"`. (5) No naked-position window persists. |
| **Gate 2 (P1)** | Drag leader Stop2 | Same as Gate 1 for Stop2/Target2 pair |
| **Gate 3 (P1)** | Two consecutive Stop1 drags | After second drag: exactly ONE PTT-TGT-Drag exists (Block A-Prime prevents accumulation). Second resubmit fires at latest captured price. |

**Gate 1 FAIL protocol**: If PTT-TGT-Drag does NOT appear after cascade → STOP. Document as DW-B155. Do NOT implement further fallback. Director resolution required.

---

### Deferred Work Updates After T1

| ID | Previous Status | Post-T1 Status |
|----|----------------|----------------|
| DW-B153 | CLOSED (B140, then invalidated) | **CLOSED** (re-closed by B141 T1) |
| DW-B154 | DOCUMENTED | DOCUMENTED (unchanged) |
| DW-B140-01 | OPEN | **CLOSED** (superseded — SIM FAIL confirmed acc.Change no-op) |
| DW-B140-02 | OPEN | **CLOSED** (superseded — acc.Change approach abandoned) |
| DW-B140-03 | OPEN | **CLOSED** (superseded — B141 Gate 3 replaces) |
| **DW-B141-STP-CYC8-WALL** | NEW (created by B141) | **OPEN** — `SyncFollowerBracket` at CYC 8 limit; no further branching permitted without first extracting headroom |

---

## Return Status

**TICKETS_COMPLETE**

Single ticket T1. One file modified (`src/PropTraderTools/CopyEngine.cs`). One test file created (`tests/PropTraderTools.Tests/B141Tests.cs`). Zero cross-contamination. All CYC <= 8. 7-scan checklist embedded. 7 xUnit [Fact] test specs defined. NT8 API constraints embedded. JS rule constraints called out per method. Ready for ptt-engineer.
