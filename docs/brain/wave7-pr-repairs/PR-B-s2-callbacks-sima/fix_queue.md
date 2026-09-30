# PR-B Fix Queue -- S2 Order Callbacks + SIMA Core Safety
# Branch: repairs/s2-callbacks-sima (create fresh from main)
# Cluster: S2 Execution Engine + S1 SIMA Core
# OKF Rules: 5 (Account.All snapshots, null guards), 7 (O(N) Contains)

---

## FINDING B-A4-1 through B-A4-4 -- O(N) .Values.Contains() on ConcurrentDictionary

**File**: src/V12_002.Orders.Callbacks.cs
**Lines**: 217, 478, 820, 847
**Issue**: `.Values.Contains(order)` is an O(N) LINQ scan on a ConcurrentDictionary.
  This runs on every order update callback -- a hot path.
  The LINQ `using System.Linq` at V12_002.cs:22 exists solely for this pattern.
**Fix**: Replace each `.Values.Contains(order)` with a dictionary key lookup.
  Pattern: check if any key maps to this order via `TryGetValue` on the entry key,
  OR if the check is "does this order exist in any entry?", use a reverse index or
  scan via `Any(kvp => kvp.Value == order)` only if unavoidable -- but prefer
  restructuring to key-based lookup using `order.Name` as the key.
  Read each call site carefully before deciding the correct replacement.
**OKF Rule 7**: Zero-alloc hot path -- no LINQ per-call.
**CYC impact**: Must remain <=8 per method.
**Commit message**: fix(repairs/pr-b): replace O(N) .Values.Contains() with key-based lookup in Orders.Callbacks

---

## FINDING B-A2-1 -- Account.All bare enumeration in SIMA.cs

**File**: src/V12_002.SIMA.cs
**Line**: 221
**Issue**: `foreach (Account acct in Account.All)` -- broker thread can mutate
  Account.All during enumeration -> InvalidOperationException.
**Fix**: `foreach (Account acct in Account.All.ToArray())`
**OKF Rule 5**: independent_tracking -- snapshot before enumeration.

---

## FINDING B-A2-2,3,4 -- Account.All bare enumeration in SIMA.Execution.cs

**File**: src/V12_002.SIMA.Execution.cs
**Lines**: 60, 252, 1064
**Fix**: Add `.ToArray()` to each `Account.All` enumeration.
**OKF Rule 5**: independent_tracking.

---

## FINDING B-A2-5,6 -- Account.All bare enumeration in SIMA.Lifecycle.cs

**File**: src/V12_002.SIMA.Lifecycle.cs
**Lines**: 230, 656
**DD entries**: DD-008, DD-009
**Fix**: Add `.ToArray()` to each. Pattern: `foreach (Account acct in Account.All.ToArray())`
**OKF Rule 5**: independent_tracking.

---

## FINDING B-A2-7,8 -- acct.Positions / acct.Orders enumeration in SIMA.Flatten.cs

**File**: src/V12_002.SIMA.Flatten.cs
**Lines**: 241 (acct.Positions), 471 (acct.Orders)
**Issue**: Broker collections -- same mutation risk.
**Fix**: `acct.Positions.ToArray()`, `acct.Orders.ToArray()`
**OKF Rule 5**: defense in depth.

---

## FINDING B-A2-9,10,11,12 -- bare enumerations in Orders.Management.Cleanup.cs

**File**: src/V12_002.Orders.Management.Cleanup.cs
**Lines**: 521 (Account.Orders), 623 (Account.All), 627 (acct.Orders), 639 (Account.Orders)
**Fix**: Add `.ToArray()` to each enumeration.
**OKF Rule 5**: independent_tracking.

---

## FINDING B-A3-1,2,3,4 -- Empty catch {} in SIMA.Lifecycle.cs

**File**: src/V12_002.SIMA.Lifecycle.cs
**Lines**: 62, 1400, 1437, 1486
**Issue**: Silent exception swallowing -- failures in hydration and sweep paths
  are silently discarded. No log, no retry, no escalation.
**Fix**: Replace each `catch { }` with `catch (Exception ex) { Print($"[V12 WARN] <context>: {ex.Message}"); }`
  Use context-appropriate label (e.g. "HydrateFleet", "SweepOrders", "DrainPhoton").
  Do NOT rethrow -- these are NinjaTrader lifecycle paths where exceptions must be swallowed,
  but they must be logged for diagnostics.
**OKF Rule 5**: infrastructure_telemetry -- log GC/memory/thread diagnostics alongside trade events.

---

## FINDING B-A5-1 -- Null ord guard in SIMA.Lifecycle.cs

**File**: src/V12_002.SIMA.Lifecycle.cs
**Lines**: 1469-1471
**DD entry**: DD-010
**Issue**: `SweepAccountOrders` iterates `acct.Orders.ToArray()` but calls
  `IsOrderInstrumentMatch(ord)` without null guard on `ord`.
  NinjaTrader can produce null entries in Orders collection.
**Fix**: Add `if (ord == null) continue;` before `IsOrderInstrumentMatch(ord)`.
**OKF Rule 5**: defense in depth.

---

## FINDING B-A5-2 -- Flatten order state whitelist divergence in SIMA.Flatten.cs

**File**: src/V12_002.SIMA.Flatten.cs
**Lines**: 476-481
**DD entry**: DD-011
**Issue**: EmergencyFlattenCollectWorkingOrders whitelist
  (Working/Submitted/Accepted/ChangePending/ChangeSubmitted) is narrower than
  IsTerminalOrderState. PartFilled/Initialized/TriggerPending/Unknown orders
  are not collected by flatten but also not terminal -- they escape the kill-switch.
**Fix**: Expand whitelist to also include PartFilled and Initialized states,
  OR align both functions to use the same state predicate. Read both functions
  before deciding which approach is safer. Do NOT break the existing terminal
  state logic.
**OKF Rule 5**: defense in depth -- DEAD-01 kill-switch must cover all non-terminal orders.

---

## FINDING B-B1-1,2 -- DateTime.Now.Ticks name suffixes in SIMA.Execution.cs

**File**: src/V12_002.SIMA.Execution.cs
**Lines**: 360, 992
**Issue**: `DateTime.Now.Ticks` used as OCO ID and RMA signal name suffix.
  Naming use only (not a time comparison), but violates UTC-only rule.
**Fix**: `DateTime.Now.Ticks` -> `DateTime.UtcNow.Ticks`
**OKF Rule 3**: UTC-only clock source.

---

## Gate Requirements

- [ ] dotnet build Linting.csproj -- 0 errors
- [ ] python scripts/wave7_prepush_gate.py --base origin/main -- GATE PASSED
- [ ] dotnet csharpier check src/ -- 0 issues
- [ ] No Account.All bare foreach remaining in modified files
- [ ] No .Values.Contains() remaining in Orders.Callbacks.cs
- [ ] No lock() introduced
- [ ] All modified methods CYC <= 8

## PR title
"fix(repairs): S2 callbacks + SIMA core safety -- Account.All snapshots, O(N) Contains, empty catch, null guards"
