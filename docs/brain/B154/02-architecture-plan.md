# Architecture Plan — Block B154
# GTC Exit Order Survival — BE/QX Stale Order Sweep + Drag Orphan Suffix Fix

**Block ID:** B154
**Status:** PLAN_COMPLETE
**Architect:** PTT Architect (ptt-architect mode)
**Phase:** 1 — Architecture

---

## LANE-SPLIT GATE RESULT

**LANES-APPROVED (sequential — shared .cs file)**

| Question | Answer |
|---|---|
| Q1. Same method or within 50 lines? | NO — TICKET 1 touches OnOrderUpdate helper region; TICKET 2 touches IsPttDragOrphanCancellable region. Both in CopyEngine.cs but >50 lines apart. |
| Q2. Fix B design depends on Fix A final design? | NO — TICKET 2 (StartsWith fix) is independent of TICKET 1 (GTC sweep). |
| Q3. Each fix has standalone value if the other is blocked? | YES — each defect is independently harmful. |
| Q4. Each fix has an independent SIM/unit verification path? | YES |

Lanes are approved but tickets are run **sequentially** (not in parallel) because both tickets share a single `.cs` file. TICKET 1 must complete and be committed before TICKET 2 begins.

---

## 1. ROOT CAUSE ANALYSIS

### DW-B154-01 / DW-B154-02 — GTC Exit Order Survival

PTT-BE and PTT-QX exit orders are created with `TimeInForce.Gtc`.
`CancelQxBrackets` is called only on manual button presses (`RelayBe`, `PttQuickExit.Execute`).
`OnOrderUpdate` has no path that sweeps stale GTC exit orders when a new entry fills.

When a new entry fills before cancel acks from a prior flatten complete, stale exit orders
remain Working and fill against the new position (partial close + re-entry ghost).

### DW-B154-03 — Drag Orphan Suffix Miss

`PTT-STP-Drag-N` / `PTT-TGT-Drag-N` are suffixed names produced by B142.
The predicate `IsPttDragOrphanCancellable` uses exact equality:
```
o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag"
```
and does not match suffixed variants. `TrySweptPttDragOrphans` silently misses them.

### DW-B154-04 — Out of Scope

PTT-Copy / Entry late fills (drain issue) — deferred. Not addressed in B154.

### Defect Register

| ID | Description | Severity |
|----|-------------|----------|
| DW-B154-01 | PTT-BE-Stop / PTT-BE-Stop-N / PTT-BE-Target-N survive into new trade | HIGH |
| DW-B154-02 | PTT-QX-Stop / PTT-QX-StopN / PTT-QX-TN survive into new trade | MEDIUM |
| DW-B154-03 | PTT-STP-Drag-N / PTT-TGT-Drag-N (suffixed) missed by orphan sweep | LOW |
| DW-B154-04 | PTT-Copy / Entry late fills (drain issue) | OUT OF SCOPE — deferred |

---

## 2. SOLUTION DESIGN

### TICKET 1 — DW-B154-01 + DW-B154-02

**File:** `src/PropTraderTools/CopyEngine.cs`

#### New predicate: `IsPttCopyEntryFilled`

```
private static bool IsPttCopyEntryFilled(Order order)  // CYC=4
```

Conditions (all must hold — short-circuit AND):

| # | Check | CYC branch |
|---|-------|------------|
| a | `order != null` | 1 |
| b | `order.OrderState == OrderState.Filled` | 2 |
| c | `order.Name == "Entry" \|\| order.Name == "PTT-Copy"` | 3 |
| d | `order.Instrument?.FullName != null` | 4 |

> **ENGINEER NOTE:** Condition (c) may be replaced with a call to the existing
> `IsPttCopyEntry(order)` at CopyEngine.cs line 7819 to reduce CYC from 4 to 3 — preferred
> if CYC budget allows. Both are correct.

#### New helper: `TryCancelStaleGtcExitOnEntry`

```
private void TryCancelStaleGtcExitOnEntry(Order order)  // CYC=8
```

Logic (8 CYC branches — within CYC<=8 budget):

| Step | Code | Branch |
|------|------|--------|
| 1 | Guard: `!IsPttCopyEntryFilled(order) → return` | 1 |
| 2 | `acc = order.Account; instr = order.Instrument` | — |
| 3 | `var stale = new List<Order>()` | — |
| 4 | `foreach (var o in acc.Orders.ToList())` | 2 |
| 4a | `o == null \|\| o.Name == null → continue` | 3 |
| 4b | `!o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal) && !o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal) → continue` | 4 |
| 4c | `o.Instrument?.FullName != instr.FullName → continue` | 5 |
| 4d | State not in {Working, Accepted, Submitted, Initialized, TriggerPending} → continue | 6 |
| 4e | `stale.Add(o)` | — |
| 5 | `stale.Count == 0 → return` | 7 |
| 6 | `try { acc.Cancel(stale); } catch {}` | 8 |
| 7 | `NinjaTrader.Code.Output.Process("[BE-ENTRY-SWEEP] acc=" + acc.Name + " cancelled=" + stale.Count, NinjaTrader.NinjaScript.PrintTo.OutputTab1)` | 0 (unconditional) |

> **TriggerPending inclusion is MANDATORY.** `IsCancelAllStateOk` omits `TriggerPending`;
> `IsQxCancelEligible2` includes it. Use the `IsQxCancelEligible2` pattern here.
> See HOTFIX-QX-DOUBLE-01 at CopyEngine.cs line 1039 for rationale.

#### Call site: `OnOrderUpdate`

Insert after `TryNakedDetect(e)` at line 1712, before `HandleDrainTerminalState` at line 1717:

```csharp
// B154 DW-B154-01/02: sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on
// new entry fill. Prevents prior-trade survivors hitting the new position.
// CYC delta=0 to OnOrderUpdate (extracted helper). See TryCancelStaleGtcExitOnEntry.
TryCancelStaleGtcExitOnEntry(e.Order);
```

**Fires on leader Entry:Filled too** — correct, leader is also subject to GTC survival.

**Fires before Gate 1 check** (`TryResolveEnabledRule`) — applies even when copy is
momentarily disabled, consistent with `TryNakedDetect` and `TryTriggerBeRecovery`.

#### Interaction Risk (annotation — no code change)

`TryTriggerBeRecovery` (line 1700) fires on `PTT-BE-Stop-*:Cancelled`.
New helper fires on `Entry:Filled`. These are orthogonal events — no concurrent execution
on the same Order object. If a prior-trade cancel ack arrives after a new entry fill,
`TryTriggerBeRecovery` may re-place BE brackets for the new position.
This is pre-existing behaviour; B154 does not change it.

#### Test seam

Add near existing `IsPttCopyEntry` testable seam (line 7819 pattern):

```csharp
internal bool IsPttCopyEntryFilledTestable(Order o) => IsPttCopyEntryFilled(o);
```

---

### TICKET 2 — DW-B154-03

**File:** `src/PropTraderTools/CopyEngine.cs`

#### Change 1: `IsPttDragOrphanCancellable` at line 1998

**Current (confirmed exact):**
```csharp
if (o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag")
    return false;
```

**Replace with:**
```csharp
if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

#### Change 2: `IsPttDragOrphanCancellableTestable` at line 2016

**Current (confirmed exact):**
```csharp
if (orderName != "PTT-TGT-Drag" && orderName != "PTT-STP-Drag")
    return false;
```

**Replace with:**
```csharp
if (!orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
    && !orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal))
    return false;
```

**CYC delta: 0** — same branch count; equality → prefix substitution only.

#### Comment update on `IsPttDragOrphanCancellable`

- **Add:** `"StartsWith: matches suffixed variants PTT-TGT-Drag-N / PTT-STP-Drag-N (B142)."`
- **Remove:** obsolete `"must not be generalized"` comment (superseded by B154).

---

## 3. TEST PLAN

### TICKET 1 — New Tests in `CopyEngineTests.cs`

| Test Name | Assert |
|-----------|--------|
| `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledEntryOrder` | Returns `true` for `OrderState.Filled`, `Name="Entry"`, non-null instrument |
| `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledPttCopyOrder` | Returns `true` for `OrderState.Filled`, `Name="PTT-Copy"`, non-null instrument |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenOrderStateIsNotFilled` | Returns `false` for `OrderState.Working` (name and instrument valid) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenNameIsNeither` | Returns `false` for `Name="PTT-BE-Stop"` (state Filled, instrument valid) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenInstrumentIsNull` | Returns `false` when `Instrument == null` (state Filled, name "Entry") |
| `TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper` | Reflection existence check — asserts method exists with expected signature |

### TICKET 2 — New Tests in `CopyEngineTests.cs`

| Test Name | Input | Assert |
|-----------|-------|--------|
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragExact` | `orderName = "PTT-TGT-Drag"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttTgtDragSuffixed` | `orderName = "PTT-TGT-Drag-2"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragExact` | `orderName = "PTT-STP-Drag"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnTrue_WhenNameIsPttStpDragSuffixed` | `orderName = "PTT-STP-Drag-1"` | `true` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenNameIsUnrelated` | `orderName = "PTT-TGT-Other"` | `false` |
| `IsPttDragOrphanCancellableTestable_ShouldReturnFalse_WhenStateIsNotWorking` | `orderState = OrderState.Filled` (valid name) | `false` regardless of name |

---

## 4. SCAN GATES

All must be zero-failure before a ticket is marked COMPLETE.

| Scan | Command | Required Result |
|------|---------|-----------------|
| SCAN-7 | `lizard --CCN 8` | 0 warnings |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | 0 matches |
| SCAN-9 | Byte-scan any edited file for bytes > 127 | 0 bytes |
| SCAN-10 | `dotnet build` | 0 errors, 0 warnings |

---

## 5. FILES MODIFIED

| File | Tickets | Change |
|------|---------|--------|
| `src/PropTraderTools/CopyEngine.cs` | T1 + T2 | New predicate, new helper, call site insertion, StartsWith fix, comment update |
| `src/PropTraderTools/CopyEngineTests.cs` | T1 + T2 | 12 new test methods (6 per ticket) |
| `src/PropTraderTools/Features/PttBreakEven.cs` | — | **NO CHANGES** |
| `src/PropTraderTools/Features/PttQuickExit.cs` | — | **NO CHANGES** |

---

## 6. PRIOR BACKLOG CARRY-FORWARD

Source: `docs/brain/B153/06-deferred-backlog.md`

| ID | Item | Prior Status | B154 Disposition |
|----|------|-------------|-----------------|
| DW-B153-05 | Stale CYC annotation at `CopyEngine.cs` line 6675 — comment still reads `CYC<=4: IsFlat(1) + SnapshotBeTargets+while-cap(2) + targets==0 early return(3)`. The `while-cap` branch was removed by DW-B153-02; lizard-measured post-fix CCN=6. Annotation must be corrected to reflect actual post-fix CYC. | OPEN | STILL OPEN — B154 touches `CopyEngine.cs`; engineer should check and correct annotation if adjacent to changes. |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` at `CopyEngine.cs` line 3445 — private method with `double[]` signature became unreachable after T3 replaced `CaptureOtherLegTargetPrices` with the `Dictionary<int,double>` implementation. Zero callers confirmed by full-codebase grep. | OPEN | STILL OPEN — not addressed in B154. Target nearest src-touching block for stop-drag region in `CopyEngine.cs`. |
| DW-B154-04 | PTT-Copy / Entry late fills (drain issue) | NEW — OUT OF SCOPE | Deferred to future block. |
