# Ticket 1 Completion — Block B154
# Sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on new entry fill

**Engineer:** PTT Engineer (ptt-engineer mode)
**Phase:** 4a — Implementation
**Ticket:** DW-B154-01 (HIGH), DW-B154-02 (MEDIUM)
**Status:** BUILD_PASS

---

## Changes Made

### File: `src/PropTraderTools/CopyEngine.cs`

#### (a) Added `IsPttCopyEntryFilled` — private static predicate
- **Location:** After `IsPttCopyEntry` at line ~7898
- **Lines:** 7891–7907 (including XML doc + comment header + method + testable seam)
- **CYC:** 4 (lizard measured CCN=7, within <=8 threshold; no lizard warning)
- **Variant used:** CYC=4 inline variant (not CYC=3 IsPttCopyEntry delegation)
  - Reason: `IsPttCopyEntry` does not null-check `o.Name` before calling `StartsWith`.
    Using the inline variant ensures safe handling of null Name without relying on
    IsPttCopyEntry's null-unsafe path. Spec explicitly allows either variant.
- **Guards (in order):**
  1. `if (order == null) return false;`
  2. `if (order.OrderState != OrderState.Filled) return false;`
  3. `if (order.Name != "Entry" && order.Name != "PTT-Copy") return false;`
  4. `if (order.Instrument?.FullName == null) return false;`

#### (b) Added `IsPttCopyEntryFilledTestable` — internal test seam
- **Location:** Immediately after `IsPttCopyEntryFilled` at line ~7907
- **Signature:** `internal bool IsPttCopyEntryFilledTestable(Order o) => IsPttCopyEntryFilled(o);`

#### (c) Added helper extractions for CYC compliance (required to achieve TryCancelStaleGtcExitOnEntry CYC=8)
- **`IsStaleGtcExitNameFor(Order o)`** — lines ~4928-4933
  - CCN=4. Checks o+o.Name null, then PTT-BE-* or PTT-QX-* prefix.
  - Extracted from TryCancelStaleGtcExitOnEntry to reduce parent CCN.
- **`IsGtcExitCancellableState(OrderState s)`** — lines ~4939-4944
  - CCN=5. Returns true for {Working, Accepted, Submitted, Initialized, TriggerPending}.
  - TriggerPending included per HOTFIX-QX-DOUBLE-01. NOT using IsCancelAllStateOk.

#### (d) Added `TryCancelStaleGtcExitOnEntry` — private void instance helper
- **Location:** After `TryTriggerBeRecovery` at line ~4956
- **Lines:** ~4956-4987
- **CYC:** 8 (lizard measured CCN=8 exactly — no warning)
- **Branch table (lizard-measured):**
  1. `if (!IsPttCopyEntryFilled(order)) return;`
  2. `foreach (var o in acc.Orders.ToList())`
  3. `if (!IsStaleGtcExitNameFor(o)) continue;`
  4. `if (o.Instrument?.FullName != instr.FullName) continue;`
  5. `if (!IsGtcExitCancellableState(o.OrderState)) continue;`
  6. `if (stale.Count == 0) return;`
  7. `try { acc.Cancel(stale); } catch { }`
- **Log format:** `[BE-ENTRY-SWEEP] acc=<name> cancelled=<N>` → OutputTab1 (ASCII-only)
- **JS rules:** JS-001 (catch absorbs), JS-002 (void), JS-021 (ToList() snapshot, no lock), JS-033 (sync)

#### (e) Inserted call in `OnOrderUpdate`
- **Location:** Lines ~1714-1717 (after `TryNakedDetect(e)` at line 1712, before `HandleDrainTerminalState` at line 1722)
- **Inserted:**
  ```csharp
  // B154 DW-B154-01/02: sweep stale PTT-BE-* and PTT-QX-* GTC exit orders on
  // new entry fill. Prevents prior-trade survivors hitting the new position.
  // CYC delta=0 to OnOrderUpdate (extracted helper). See TryCancelStaleGtcExitOnEntry.
  TryCancelStaleGtcExitOnEntry(e.Order);
  ```
- **CYC delta=0:** Call is unconditional; all branching inside extracted helper.

---

### File: `src/PropTraderTools/CopyEngineTests.cs`

#### 6 [Fact] tests added in `CopyEngineTests` class (lines ~4229-4403):
- Helper methods added: `MakeInstrument(string fullName)`, `MakeOrderWithInstrument(OrderState, string, Instrument)`
- Tests 1-5 (`IsPttCopyEntryFilled_*`): `[Fact(Skip = "NT8-runtime")]`
  - Skip required: `CopyEngineTests` constructor initializes `CopyEngine.Instance` which
    triggers AgileDotNetRT static initializer. Throws TypeInitializationException in test
    context. Same pattern as all existing MakeOrder-based tests in this class.
  - Tests include try/catch NullReferenceException as B63-pattern safety net.
- Test 6 (`TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper`): in `CopyEngineTests` also marked `[Fact(Skip = ...)]` to avoid the TypeInitializationException.

#### New class `B154T1ContractTests` added (lines ~9268-9285):
- Test 6 (method existence) moved here as `[Fact]` WITHOUT Skip.
- Class has NO `_engine` field — avoids CopyEngine.Instance / AgileDotNetRT cctor.
- Test runs cleanly and PASSES in dotnet test.
- Follows the exact same architecture as `B153IsPttQxTargetTests` (lines ~9203-9265).

---

## DW Card Annotation (as specified in ticket)

`TryTriggerBeRecovery` (line ~4908) fires on `PTT-BE-Stop-*:Cancelled`.
`TryCancelStaleGtcExitOnEntry` fires on `Entry:Filled` / `PTT-Copy:Filled`.
These are orthogonal NT8 dispatcher events — no concurrent execution on the same Order object.

If a prior-trade cancel ack arrives after a new entry fill, `TryTriggerBeRecovery` may
re-place BE brackets for the new position. This is pre-existing behaviour. B154 does not
change it.

---

## CYC Preferred Variant Decision

**Used:** CYC=4 inline variant (NOT the CYC=3 IsPttCopyEntry delegation).

Reason: `IsPttCopyEntry` calls `o.Name.StartsWith(...)` without null-checking `o.Name` first.
After guard (1) `if (order == null) return false;`, a null Name would still cause NRE inside
`IsPttCopyEntry`. The inline variant uses `!=` comparisons which safely return false for
null Name without throwing. The spec explicitly permits either form.

---

## Scan Results

### SCAN-7: `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs`
**Result: 0 new warnings from B154 code.**

New method CCN values (measured by lizard):
| Method | CCN | Lizard Warning? |
|--------|-----|-----------------|
| `IsPttCopyEntryFilled` | 7 | NO (<=8) |
| `IsPttCopyEntryFilledTestable` | 1 | NO |
| `IsStaleGtcExitNameFor` | 4 | NO |
| `IsGtcExitCancellableState` | 5 | NO |
| `TryCancelStaleGtcExitOnEntry` | 8 | NO (exactly 8) |

Pre-existing warnings (not from B154, not changed): 7 methods (MirrorCloseOneAccount,
TrimOneAccount, CancelExistingTgtDragOrders, HasInFlightFlattenOrder, ResubmitFollowerEntry,
and 2 others).

### SCAN-8: `grep -rn "lock\s*(" src/PropTraderTools/`
**Result: 0 matches in B154 new code.**

Pre-existing lock() calls in CopyEngine.cs: 79 (unchanged). No lock() in new methods.
CopyEngineTests.cs: 0 lock() matches total.

### SCAN-9: PowerShell byte scan — `CopyEngine.cs` bytes > 127
**Result: 0 bytes > 127**

### SCAN-9b: PowerShell byte scan — `CopyEngineTests.cs` bytes > 127
**Result: 0 bytes > 127**

### SCAN-10: `dotnet build src/PropTraderTools/`
**Result: 0 errors, 0 new warnings** (1139 pre-existing xUnit1004 analyzer warnings for
existing `[Fact(Skip=...)]` tests — unchanged from pre-B154 state).

### SCAN-L: `grep -rn "lock(" src/PropTraderTools/`
**Result: 0 matches in B154 new code.** (Same as SCAN-8 — pre-existing only.)

### SCAN-B: `dotnet test src/PropTraderTools/`
**Result: Failed=0, Passed=186 (+1 from B154T1ContractTests), Skipped=369 (+5 from B154 NT8-skip tests)**

B154 test results:
| Test | Class | Status |
|------|-------|--------|
| `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledEntryOrder` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnTrue_WhenFilledPttCopyOrder` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenOrderStateIsNotFilled` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenNameIsNeither` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `IsPttCopyEntryFilled_ShouldReturnFalse_WhenInstrumentIsNull` | CopyEngineTests | SKIPPED (NT8-runtime) |
| `TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper` (in CopyEngineTests) | CopyEngineTests | SKIPPED (NT8-runtime) |
| `TryCancelStaleGtcExitOnEntry_ShouldExist_AsPrivateInstanceHelper` (in B154T1ContractTests) | B154T1ContractTests | **PASSED** |

Zero failures. The contract test (Test 6) runs and passes in `B154T1ContractTests`.

---

## BUILD_PASS
