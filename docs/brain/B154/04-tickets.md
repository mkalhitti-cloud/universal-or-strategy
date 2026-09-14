# Tickets — Block B154
# GTC Exit Order Survival — BE/QX Stale Order Sweep + Drag Orphan Suffix Fix

**Block ID:** B154
**Phase:** 3 — Ticket Generation
**Architect:** PTT Architect (ptt-architect mode)
**Source Plan:** `docs/brain/B154/02-architecture-plan.md` (REVIEW_PASS)

---

## SEQUENCING CONSTRAINT

TICKET 1 MUST be fully committed before TICKET 2 begins.
Both tickets share `src/PropTraderTools/CopyEngine.cs`.
No parallel execution.

---

## TICKET 1

**Title:** Sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on new entry fill
**Spec Requirements:** DW-B154-01 (HIGH), DW-B154-02 (MEDIUM)
**Files Modified:**
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

**Completion Report Target:** `docs/brain/B154/ticket-1-completion.md`
**Verification Report Target:** `docs/brain/B154/ticket-1-verification.md`

---

### Atomic Changes — ALL REQUIRED, NO ABBREVIATIONS

#### (a) Add private static predicate `IsPttCopyEntryFilled`

**Location:** `src/PropTraderTools/CopyEngine.cs` — add in the predicate region near `IsPttCopyEntry` at line ~7819.

**Method signature (exact):**
```csharp
private static bool IsPttCopyEntryFilled(Order order)
```

**CYC budget:** CYC <= 4 (all conditions short-circuit AND).

**Required comment header on the method:**
```
// CYC=4, JS-021, JS-001, JS-002, ASCII-only, B154 DW-B154-01/02
```

**Required logic — all four conditions must hold (short-circuit AND):**
```
(a) order != null
(b) order.OrderState == OrderState.Filled
(c) order.Name == "Entry" || order.Name == "PTT-Copy"
    [ENGINEER NOTE: condition (c) MAY be replaced with a call to IsPttCopyEntry(order)
     at CopyEngine.cs line 7819. Reduces CYC from 4 to 3. Both are correct.
     Preferred if CYC budget allows (CYC=3 is strictly better). Use whichever form
     does not introduce a redundant null check already covered by (a).]
(d) order.Instrument?.FullName != null
```

**Full method body:**
```csharp
/// <summary>
/// Returns true when <paramref name="order"/> is a filled entry order
/// that should trigger a GTC exit sweep. CYC=4, JS-021, JS-001, JS-002,
/// ASCII-only, B154 DW-B154-01/02.
/// </summary>
private static bool IsPttCopyEntryFilled(Order order)
{
    if (order == null) return false;
    if (order.OrderState != OrderState.Filled) return false;
    if (order.Name != "Entry" && order.Name != "PTT-Copy") return false;
    if (order.Instrument?.FullName == null) return false;
    return true;
}
```

> **CYC=3 preferred variant** — if `IsPttCopyEntry` already encapsulates the name check:
> ```csharp
> private static bool IsPttCopyEntryFilled(Order order)
> {
>     if (order == null) return false;
>     if (order.OrderState != OrderState.Filled) return false;
>     if (!IsPttCopyEntry(order)) return false;
>     if (order.Instrument?.FullName == null) return false;
>     return true;
> }
> ```
> Engineer chooses the form that produces lower lizard CYC. Both satisfy spec.

---

#### (b) Add internal test seam `IsPttCopyEntryFilledTestable`

**Location:** `src/PropTraderTools/CopyEngine.cs` — immediately after or near `IsPttCopyEntryFilled`, adjacent to the existing `IsPttCopyEntry` testable seam pattern at line ~7819.

**Method signature (exact):**
```csharp
internal bool IsPttCopyEntryFilledTestable(Order o) => IsPttCopyEntryFilled(o);
```

No CYC cost (expression body delegates to the private static method). No comment header required beyond the single-line form.

---

#### (c) Add private instance helper `TryCancelStaleGtcExitOnEntry`

**Location:** `src/PropTraderTools/CopyEngine.cs` — add in the helper region near `TryTriggerBeRecovery` or `TryNakedDetect`.

**Method signature (exact):**
```csharp
private void TryCancelStaleGtcExitOnEntry(Order order)
```

**CYC budget:** CYC = 8 (8 conditional branches — within CYC<=8 limit).

**Required comment header on the method:**
```
// CYC=8, JS-021, JS-001, JS-002, ASCII-only, B154 DW-B154-01/02
```

**Required full implementation:**
```csharp
/// <summary>
/// On a new entry fill, cancels any stale PTT-BE-* or PTT-QX-* GTC exit orders
/// on the same account and instrument. Prevents prior-trade survivors from
/// filling against the new position. CYC=8, JS-021, JS-001, JS-002,
/// ASCII-only, B154 DW-B154-01/02.
/// </summary>
private void TryCancelStaleGtcExitOnEntry(Order order)
{
    if (!IsPttCopyEntryFilled(order)) return;                              // branch 1

    var acc   = order.Account;
    var instr = order.Instrument;
    var stale = new List<Order>();

    foreach (var o in acc.Orders.ToList())                                 // branch 2 (foreach)
    {
        if (o == null || o.Name == null) continue;                         // branch 3

        if (!o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
            && !o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal))   // branch 4
            continue;

        if (o.Instrument?.FullName != instr.FullName) continue;           // branch 5

        // TriggerPending MUST be included — see HOTFIX-QX-DOUBLE-01 at CopyEngine.cs line 1039.
        // Do NOT use IsCancelAllStateOk (omits TriggerPending).
        // Use IsQxCancelEligible2 pattern.
        if (o.OrderState != OrderState.Working
            && o.OrderState != OrderState.Accepted
            && o.OrderState != OrderState.Submitted
            && o.OrderState != OrderState.Initialized
            && o.OrderState != OrderState.TriggerPending) continue;       // branches 6 (multi-condition guard)

        stale.Add(o);
    }

    if (stale.Count == 0) return;                                          // branch 7

    try                                                                    // branch 8
    {
        acc.Cancel(stale);
    }
    catch { }

    // Unconditional log — always fires when stale.Count > 0
    NinjaTrader.Code.Output.Process(
        "[BE-ENTRY-SWEEP] acc=" + acc.Name + " cancelled=" + stale.Count,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**JS Rule constraints enforced in this method:**
- **JS-001 (no throw in hot path):** `try { acc.Cancel(stale); } catch { }` — exception absorbed, never rethrown.
- **JS-002 (void helper):** Method returns `void`. No return value.
- **JS-021 (no lock):** `acc.Orders.ToList()` snapshots the collection without a lock. No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim`.
- **JS-033 (no async void):** Method is synchronous `void`, not `async void`.

**State filter MANDATORY requirement:**
The cancellable state set is `{Working, Accepted, Submitted, Initialized, TriggerPending}`.
`TriggerPending` MUST be included (HOTFIX-QX-DOUBLE-01 precedent). If `IsCancelAllStateOk` is used as a helper, engineer must verify it includes `TriggerPending`; if not, inline the check as shown above.

**Log line MUST be:**
- ASCII-only (no Unicode, no curly quotes, no emoji).
- Format: `"[BE-ENTRY-SWEEP] acc=" + acc.Name + " cancelled=" + stale.Count`
- Target: `NinjaTrader.NinjaScript.PrintTo.OutputTab1`
- Placement: unconditional — fires whenever `stale.Count > 0` (after the early-return guard on `stale.Count == 0`).

---

#### (d) Insert call site in `OnOrderUpdate`

**Location:** `src/PropTraderTools/CopyEngine.cs` — inside `OnOrderUpdate`, after `TryNakedDetect(e)` at line ~1712, before `HandleDrainTerminalState` at line ~1717.

**Exact lines to insert (verbatim — no changes to surrounding lines):**
```csharp
// B154 DW-B154-01/02: sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on
// new entry fill. Prevents prior-trade survivors hitting the new position.
// CYC delta=0 to OnOrderUpdate (extracted helper). See TryCancelStaleGtcExitOnEntry.
TryCancelStaleGtcExitOnEntry(e.Order);
```

**CYC constraint:** `OnOrderUpdate` CYC delta = 0. The call is unconditional; all branching is inside the extracted helper. Engineer MUST verify lizard reports no new warnings on `OnOrderUpdate` after insertion.

**Insertion position is exact:**
- AFTER: `TryNakedDetect(e);`
- BEFORE: `HandleDrainTerminalState(...)` (or whatever the next call at line ~1717 is)
- Do NOT move or reorder other existing calls in `OnOrderUpdate`.

---

#### (e) Add 6 tests in `CopyEngineTests.cs`

All tests use the `IsPttCopyEntryFilledTestable` seam. Test class: existing `CopyEngineTests` class (do not create a new class).

**Test 1 — exact method signature:**
```csharp
[Fact]
public void IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledEntryOrder()
```
**Assert:** `IsPttCopyEntryFilledTestable(order)` returns `true`
**Setup:** `order.OrderState = OrderState.Filled`, `order.Name = "Entry"`, `order.Instrument.FullName = "ES 03-25"` (or any non-null string)

---

**Test 2 — exact method signature:**
```csharp
[Fact]
public void IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledPttCopyOrder()
```
**Assert:** `IsPttCopyEntryFilledTestable(order)` returns `true`
**Setup:** `order.OrderState = OrderState.Filled`, `order.Name = "PTT-Copy"`, `order.Instrument.FullName = "ES 03-25"` (or any non-null string)

---

**Test 3 — exact method signature:**
```csharp
[Fact]
public void IsPttCopyEntryFilled_ShouldReturnFalse_WhenOrderStateIsNotFilled()
```
**Assert:** `IsPttCopyEntryFilledTestable(order)` returns `false`
**Setup:** `order.OrderState = OrderState.Working`, `order.Name = "Entry"`, `order.Instrument.FullName = "ES 03-25"`

---

**Test 4 — exact method signature:**
```csharp
[Fact]
public void IsPttCopyEntryFilled_ShouldReturnFalse_WhenNameIsNeither()
```
**Assert:** `IsPttCopyEntryFilledTestable(order)` returns `false`
**Setup:** `order.OrderState = OrderState.Filled`, `order.Name = "PTT-BE-Stop"`, `order.Instrument.FullName = "ES 03-25"`

---

**Test 5 — exact method signature:**
```csharp
[Fact]
public void IsPttCopyEntryFilled_ShouldReturnFalse_WhenInstrumentIsNull()
```
**Assert:** `IsPttCopyEntryFilledTestable(order)` returns `false`
**Setup:** `order.OrderState = OrderState.Filled`, `order.Name = "Entry"`, `order.Instrument = null`

---

**Test 6 — exact method signature:**
```csharp
[Fact]
public void TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper()
```
**Assert:** Reflection confirms the method exists on the `CopyEngine` class with the correct binding flags.
**Implementation:**
```csharp
var method = typeof(CopyEngine).GetMethod(
    "TryCancelStaleGtcExitOnEntry",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Assert.NotNull(method);
Assert.Equal(typeof(void), method.ReturnType);
```

---

### DW Card Annotation (documentation only — no code change)

`TryTriggerBeRecovery` (line ~1700) fires on `PTT-BE-Stop-*:Cancelled`.
`TryCancelStaleGtcExitOnEntry` fires on `Entry:Filled` / `PTT-Copy:Filled`.
These are orthogonal NT8 dispatcher events — no concurrent execution on the same Order object.

If a prior-trade cancel ack arrives after a new entry fill, `TryTriggerBeRecovery` may
re-place BE brackets for the new position. This is pre-existing behaviour. B154 does not
change it. Engineer should document this in `ticket-1-completion.md`.

---

### JS Rule Constraints — TICKET 1 Summary

| Rule | Constraint | Where Enforced |
|------|-----------|----------------|
| JS-001 | No throw in hot path | `try { acc.Cancel(stale); } catch { }` |
| JS-002 | Void helper, no nullable return | `TryCancelStaleGtcExitOnEntry` returns `void`; `IsPttCopyEntryFilled` returns `bool` |
| JS-021 | No lock() / Monitor / Mutex | `acc.Orders.ToList()` snapshot — no lock |
| JS-033 | No async void | Both new methods are synchronous |

---

### CYC Constraints — TICKET 1

| Method | Required CYC | Enforcement |
|--------|-------------|-------------|
| `IsPttCopyEntryFilled` | <= 4 | SCAN-7 lizard |
| `TryCancelStaleGtcExitOnEntry` | = 8 (max) | SCAN-7 lizard |
| `OnOrderUpdate` | delta = 0 | SCAN-7 lizard (no new warning) |

---

### 7-Scan Checklist — TICKET 1

Engineer MUST run ALL scans and report pass/fail for each in `ticket-1-completion.md`. Zero exceptions.

| Scan ID | Command | Required Result |
|---------|---------|-----------------|
| SCAN-7 | `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` | 0 warnings |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 matches |
| SCAN-9 | `powershell -Command "(Get-Content -Encoding Byte src/PropTraderTools/CopyEngine.cs) | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count"` equals `0` | 0 bytes > 127 |
| SCAN-9b | Same PowerShell command for `CopyEngineTests.cs` | 0 bytes > 127 |
| SCAN-10 | `dotnet build src/PropTraderTools/` | 0 errors, 0 warnings |
| SCAN-L | `grep -rn "lock(" src/PropTraderTools/` | 0 matches (secondary lock check) |
| SCAN-B | `dotnet test src/PropTraderTools/` | All 6 new [Fact] tests pass; 0 failures |

---

### Verify Criteria — TICKET 1

**Unit verification (automated — must pass before SIM):**
- `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledEntryOrder` → green
- `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledPttCopyOrder` → green
- `IsPttCopyEntryFilled_ShouldReturnFalse_WhenOrderStateIsNotFilled` → green
- `IsPttCopyEntryFilled_ShouldReturnFalse_WhenNameIsNeither` → green
- `IsPttCopyEntryFilled_ShouldReturnFalse_WhenInstrumentIsNull` → green
- `TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper` → green (reflection check)

**SIM gate (manual — verifier performs in NinjaTrader sim account):**
1. Open NinjaTrader Output Tab 1.
2. On a sim account that has a live `PTT-BE-Stop` or `PTT-BE-Target` in `Working` state, submit a new entry order for the same instrument.
3. When the entry fills, confirm `[BE-ENTRY-SWEEP] acc=<name> cancelled=<N>` appears in Output Tab 1 within 1 second of fill confirmation.
4. Confirm the `PTT-BE-Stop` / `PTT-BE-Target` order transitions to `Cancelled` in the Orders grid.
5. Repeat with a `PTT-QX-Stop` order in `Working` state — same expectation.

**SIM pass criteria:** Log line appears AND stale order(s) are cancelled. Zero exceptions in Output Tab 2.

---

## TICKET 2

**Title:** Fix `IsPttDragOrphanCancellable` to match suffixed Drag order names (B142)
**Spec Requirements:** DW-B154-03 (LOW)
**Files Modified:**
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

**Prerequisite:** TICKET 1 MUST be committed before TICKET 2 begins.

**Completion Report Target:** `docs/brain/B154/ticket-2-completion.md`
**Verification Report Target:** `docs/brain/B154/ticket-2-verification.md`

---

### Atomic Changes — ALL REQUIRED, NO ABBREVIATIONS

#### (a) Replace equality check in `IsPttDragOrphanCancellable` at line ~1998

**Location:** `src/PropTraderTools/CopyEngine.cs`, method `IsPttDragOrphanCancellable`, at line ~1998.

**Find this exact code (SEARCH):**
```csharp
if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")
    return false;
```

**Replace with (REPLACE):**
```csharp
if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**CYC delta:** 0. McCabe branch count is unchanged — equality replaced by prefix check, same two conditions.

---

#### (b) Replace equality check in `IsPttDragOrphanCancellableTestable` at line ~2016

**Location:** `src/PropTraderTools/CopyEngine.cs`, method `IsPttDragOrphanCancellableTestable`, at line ~2016.

**Find this exact code (SEARCH):**
```csharp
if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
    return false;
```

**Replace with (REPLACE):**
```csharp
if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**CYC delta:** 0. Same reasoning as (a).

---

#### (c) Update comment on `IsPttDragOrphanCancellable` method header (~line 1989)

**Add this line to the method header comment (or XML doc summary):**
```
// StartsWith: matches suffixed variants PTT-TGT-Drag-N / PTT-STP-Drag-N (B142).
```

**Remove this line from the method header comment (superseded by B154 — do NOT leave it in):**
```
// must not be generalized
```
(Exact wording may vary slightly. Remove whichever comment line contains the phrase "must not be generalized".)

If the existing comment uses `/// <summary>` XML doc format, update the summary text accordingly. ASCII-only; no Unicode in the comment.

---

#### (d) Add 6 tests in `CopyEngineTests.cs`

All tests use the `IsPttDragOrphanCancellableTestable` seam (existing method at line ~2016). Test class: existing `CopyEngineTests` class. Do not create a new test class.

**Test 1 — exact method signature:**
```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragExact()
```
**Assert:** returns `true`
**Setup:** `orderName = "PTT-TGT-Drag"`, `orderState = OrderState.Working`

---

**Test 2 — exact method signature:**
```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragSuffixed()
```
**Assert:** returns `true`
**Setup:** `orderName = "PTT-TGT-Drag-2"`, `orderState = OrderState.Working`

---

**Test 3 — exact method signature:**
```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragExact()
```
**Assert:** returns `true`
**Setup:** `orderName = "PTT-STP-Drag"`, `orderState = OrderState.Working`

---

**Test 4 — exact method signature:**
```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragSuffixed()
```
**Assert:** returns `true`
**Setup:** `orderName = "PTT-STP-Drag-1"`, `orderState = OrderState.Working`

---

**Test 5 — exact method signature:**
```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenNameIsUnrelated()
```
**Assert:** returns `false`
**Setup:** `orderName = "PTT-TGT-Other"`, `orderState = OrderState.Working`

---

**Test 6 — exact method signature:**
```csharp
[Fact]
public void IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenStateIsNotWorking()
```
**Assert:** returns `false` regardless of order name
**Setup:** `orderName = "PTT-TGT-Drag"`, `orderState = OrderState.Filled`

---

### JS Rule Constraints — TICKET 2 Summary

| Rule | Constraint | Where Enforced |
|------|-----------|----------------|
| JS-021 | No lock() / Monitor / Mutex | No new locking introduced; existing method unchanged except predicate logic |
| ASCII-only | All string literals 7-bit ASCII | `"PTT-TGT-Drag"`, `"PTT-STP-Drag"`, `StringComparison.Ordinal` — all ASCII |

---

### CYC Constraints — TICKET 2

| Method | CYC Change | Enforcement |
|--------|-----------|-------------|
| `IsPttDragOrphanCancellable` | delta = 0 (equality → prefix, same branch count) | SCAN-7 lizard |
| `IsPttDragOrphanCancellableTestable` | delta = 0 (same) | SCAN-7 lizard |

---

### 7-Scan Checklist — TICKET 2

Engineer MUST run ALL scans and report pass/fail for each in `ticket-2-completion.md`. Zero exceptions.

| Scan ID | Command | Required Result |
|---------|---------|-----------------|
| SCAN-7 | `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` | 0 warnings |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 matches |
| SCAN-9 | `powershell -Command "(Get-Content -Encoding Byte src/PropTraderTools/CopyEngine.cs) | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count"` equals `0` | 0 bytes > 127 |
| SCAN-9b | Same PowerShell command for `CopyEngineTests.cs` | 0 bytes > 127 |
| SCAN-10 | `dotnet build src/PropTraderTools/` | 0 errors, 0 warnings |
| SCAN-L | `grep -rn "lock(" src/PropTraderTools/` | 0 matches (secondary lock check) |
| SCAN-B | `dotnet test src/PropTraderTools/` | All 6 new [Fact] tests pass; 0 failures |

---

### Verify Criteria — TICKET 2

**Unit verification (automated — all must pass):**
- `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragExact` → green
- `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragSuffixed` → green
- `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragExact` → green
- `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragSuffixed` → green
- `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenNameIsUnrelated` → green
- `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenStateIsNotWorking` → green

**SCAN-7 gate:** `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` = 0 warnings.
Any new warning = TICKET 2 FAIL. Do not proceed to completion.md until 0 warnings confirmed.

---

## OUT OF SCOPE — EXPLICIT EXCLUSION

**DW-B154-04** (PTT-Copy / Entry late fills drain issue) is **NOT** in either ticket.
It is deferred to a future block. Engineer must not address DW-B154-04 in this block.

---

## PRIOR BACKLOG CARRY-FORWARD (for engineer awareness — no code change required)

| ID | Item | Status in B154 |
|----|------|----------------|
| DW-B153-05 | Stale CYC annotation at CopyEngine.cs line ~6675 — comment still reads old CYC; lizard-measured post-fix CCN=6. | STILL OPEN — if your TICKET 1 or TICKET 2 changes are adjacent to line 6675, fix the annotation. Otherwise leave it for the next src-touching block. |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` at CopyEngine.cs line ~3445 — zero callers confirmed. | STILL OPEN — not in scope for B154. Target nearest src-touching block for stop-drag region. |

---

## COMPLETION PROTOCOL

For each ticket, engineer produces:
1. `docs/brain/B154/ticket-N-completion.md` — lists every atomic change made, SCAN results (pass/fail per scan), and DW card annotation for TICKET 1.
2. `docs/brain/B154/ticket-N-verification.md` — verifier independently confirms changes match this ticket spec.

Both files MUST exist before the block is marked COMPLETE.

---

**Return: TICKETS_COMPLETE**
