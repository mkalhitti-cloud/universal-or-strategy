# B69-LaneA Architecture Plan

**Status**: REVIEW_PENDING
**Epic**: B69-LaneA
**Author**: ptt-architect
**Date**: 2026-08-13
**Target file**: `src/PropTraderTools/CopyEngine.cs`
**Test file**: `src/PropTraderTools/CopyEngineTests.cs`

---

## 1. Problem Summary

### DW-B69-01 (P0) — FlattenOneAccount: name-filtered cancel + missing Submit

**Root cause (two sub-issues in one method):**

**Sub-issue A — Name-filtered cancel:**
`FlattenOneAccount` calls `CancelQxBrackets(acc, instrument)` (line 1483) before issuing the
market flatten order. `CancelQxBrackets` uses `IsQxCancelCandidate`, which filters by order name
prefix (ATM bracket names and `PTT-*` prefix). Any active order on the follower account that does
NOT match that predicate — such as a `PTT-BE-Stop` or a plain limit entry not yet bracket-wrapped
— survives the cancel step. At Rithmic/Apex brokers, an incoming market order conflicts with a
live OCO bracket or a standing limit at the broker layer, producing a
`"Close operation failed. Operation timed out."` error. The flatten silently fails.

**Sub-issue B — Missing Submit:**
`FlattenOneAccount` calls `acc.CreateOrder(...)` (lines 1487-1490) but never calls
`acc.Submit(new[]{order})`. In the NT8 AddOn API `CreateOrder()` stages the order in NT8's
internal queue; `Submit()` is required to actually transmit it to the broker. Without it the
flatten market order is staged but never sent. The method appears to succeed (no exception is
thrown) but no fill occurs.

**Production impact:**
Both sub-issues compound: a follower account in a fast-moving market can have stale bracket orders
survive the cancel step AND the new flatten market order never reaches the broker. The net effect
is a follower account that remains fully open while the leader is flat.

**Evidence in source:**
- Line 1483: `CancelQxBrackets(acc, instrument)` — name-gated cancel.
- Lines 1487-1490: `acc.CreateOrder(...)` with no `acc.Submit(...)` call.
- Lines 1470-1472: existing comment acknowledges the cancel-before-market-order requirement.

---

### DW-B69-02 (P1) — Reference equality for Instrument comparison in SubmitBeStop and FindPosition

**Root cause:**
Two sites use C# reference equality (`==`) to match an `Instrument` parameter against entries
in `acc.Positions`:

- `SubmitBeStop` line 512: `if (p.Instrument == instr)`
- `FindPosition` line 1778: `if (p.Instrument == instrument) return p;`

NT8 resolves `Instrument` objects per-account context from its internal cache. In a multi-account
scenario, two `Position` entries representing the same logical instrument (e.g. `ES 09-26`) can
hold distinct object references. Reference equality returns `false` and the position is never
found. `SubmitBeStop` silently skips the BE-stop submission; `FindPosition` returns `null`,
which causes `FlattenOneAccount` to take the early-return path at line 1478-1481 even when a
real position exists.

**NT8 authority:** `NT8_FULL_REFERENCE.md` line 1926 documents `FullName` as the stable cross-context
instrument identity. `CancelQxBrackets` (line 463) already uses `FullName` correctly:
`o.Instrument.FullName != instr.FullName`.

**Production impact:**
- `SubmitBeStop`: BE-stop orders are silently not submitted for affected follower accounts,
  leaving them unprotected after a break-even trigger.
- `FindPosition` (via `FlattenOneAccount`): flatten skips with a `"flat skip"` log entry even
  when the account has an open position — flatten does not execute.

---

### DW-B69-03 (P1) — HandleEntryChange: missing _dedupCache preload after resubmit

**Root cause:**
`HandleEntryChange` (line 1078) processes a leader entry order price drag. It:
1. Removes the old `OrderId` from `_dedupCache` (line 1094).
2. Cancels the old follower order (line 1113).
3. Creates and submits a new follower order (lines 1114-1128).
4. Does NOT preload the new `order.OrderId` into `_dedupCache`.

`_dedupCache` is the dedup guard that prevents `DispatchCopy` from re-copying an order it has
already copied (line 115: `ConcurrentDictionary<string, double>`). When NT8 fires the `Accepted`
event for the newly submitted follower order, `DispatchCopy` sees an `OrderId` not in the cache
and treats it as a new leader event — triggering a second copy dispatch (double-copy).

The B67-LaneB comment at lines 1091-1093 explicitly says "New entry will be re-keyed by
`DispatchCopy` on the follower's Accepted event." This relied on a downstream re-key path
that is not present, leaving a race window between Submit and Accepted.

**Reference:** PropagateFollowerEntryReplace Build 947 pattern: "If a replace is already
in-flight... absorb... without firing a second cancel." The minimal equivalent here is
preloading the new `OrderId` into `_dedupCache` at `newPrice` immediately after `Submit`.

**Production impact:**
On fast markets where the leader drags an entry price multiple ticks, each drag can produce a
duplicate follower copy order. In the worst case N price drags produce 2N follower orders.

---

## 2. Architecture Decisions

### Decision A — CancelAllAccountOrders, not a patch to IsQxCancelCandidate

`IsQxCancelCandidate` is a named-predicate filter: it knows about ATM bracket suffixes and
`PTT-*` prefixed order names. Extending it to cover more names creates a growing string-list
that must be updated every time a new order type is introduced. This is a maintenance liability
and an architectural coupling between the cancel predicate and the full set of known PTT order
name strings.

The correct semantic for a flatten operation is: *cancel every active order on this account for
this instrument, regardless of name*. This matches the `EmergencyFlattenSingleFleetAccount`
`[938-EF-GUARD]` pattern (lines 240-258), which also makes no name distinction. A new
name-agnostic helper `CancelAllAccountOrders(Account acc, Instrument instr)` implements this
semantic cleanly with `CYC=4` and no coupling to order name strings.

### Decision B — FullName string equality, not reference equality

`Instrument` objects in NT8 are resolved per-account from an internal object cache. The cache is
not guaranteed to return the same reference across accounts. `FullName` (e.g. `"ES 09-26"`) is
the stable string identity for an instrument and is already the established pattern in this
codebase (see `CancelQxBrackets` line 463). Both fix sites add an explicit null-guard
(`p.Instrument != null &&`) before the `FullName` dereference to prevent NRE on partially
initialized position objects.

### Decision C — _dedupCache preload, not a per-order FSM

A full FSM (per-order `PendingCancel → Submitting → Accepted` state machine) would require:
- A new `ConcurrentDictionary<string, OrderReplaceState>` (new type, new ownership)
- Event-driven state transitions in `OnOrderUpdate`
- Timeout/cleanup logic for stuck states

This is significant complexity for a problem that `_dedupCache` already structurally solves.
`_dedupCache` is the in-flight guard: its presence for a given `OrderId` means "this order was
placed by PTT, do not re-copy." Preloading the new `OrderId` at `newPrice` immediately after
`acc.Submit()` is the minimal FSM-equivalent — a single atomic write to a `ConcurrentDictionary`
that closes the race window before NT8 can fire the `Accepted` event. No new data structures,
no new state machines, CYC delta = 0.

---

## 3. Method Signatures and CYC Annotations

### 3.1 — NEW: CancelAllAccountOrders

```
// Insertion point: src/PropTraderTools/CopyEngine.cs after line 470 (end of CancelQxBrackets block)
// CYC=4: (1) null-guard on acc/instr, (2) foreach loop, (3) stateOk compound check, (4) FullName check.
// JS-021: no lock. Uses FullName comparison (not reference equality). No name filter.
// States cancelled: Working | Initialized | Submitted | Accepted | ChangeSubmitted.
// Widens from CancelQxBrackets (name-gated) to all active orders for this instrument.
internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
```

CYC breakdown:
| Branch | Description |
|--------|-------------|
| 1 | `if (acc == null \|\| instr == null) return;` — null-guard |
| 2 | `foreach (Order o in acc.Orders)` — loop |
| 3 | `stateOk` compound: Working \| Initialized \| Submitted \| Accepted \| ChangeSubmitted |
| 4 | `o.Instrument == null \|\| o.Instrument.FullName != instr.FullName` — FullName gate |

No inner branches beyond stateOk evaluation. `CYC = 4`. PASS.

### 3.2 — MODIFIED: FlattenOneAccount

```
// src/PropTraderTools/CopyEngine.cs line 1475 (signature unchanged, body modified)
// CYC=4: (1) pos null/qty guard, (2) CancelAllAccountOrders call, (3) action ternary, (4) try/catch.
// Inner: if (order != null) acc.Submit(...) -- sub-branch inside try, does not add outer CYC.
// B69 DW-B69-01: widened from CancelQxBrackets to CancelAllAccountOrders (name-agnostic).
// B69 DW-B69-01: added acc.Submit(new[]{order}) -- was missing, order was staged but never sent.
private void FlattenOneAccount(Account acc, Instrument instrument)
```

Changes:
- Line 1473 comment: replace "CancelQxBrackets" with "CancelAllAccountOrders"; add B69 note.
- Line 1483: `CancelQxBrackets(acc, instrument)` → `CancelAllAccountOrders(acc, instrument)`
- Lines 1487-1490: capture `CreateOrder` return in `var order`; add `if (order != null) acc.Submit(new[] { order });`

### 3.3 — MODIFIED: SubmitBeStop (line 512)

```
// src/PropTraderTools/CopyEngine.cs line 507 (signature unchanged)
// B69 DW-B69-02: pos-find uses FullName comparison (not reference equality).
internal void SubmitBeStop(Account acc, NinjaTrader.Cbi.Instrument instr, double bePrice, bool isLong)
```

Change at line 512:
```
// OLD:
if (p.Instrument == instr) { pos = p; break; }
// NEW:
if (p.Instrument != null && p.Instrument.FullName == instr.FullName) { pos = p; break; }
```

### 3.4 — MODIFIED: FindPosition (line 1778)

```
// src/PropTraderTools/CopyEngine.cs line 1775 (signature unchanged)
// Pre-existing CYC=1 (no branching beyond foreach). FullName fix adds null-guard -- no CYC change.
private Position FindPosition(Account acc, Instrument instrument)
```

Change at line 1778:
```
// OLD:
if (p.Instrument == instrument) return p;
// NEW:
if (p.Instrument != null && p.Instrument.FullName == instrument.FullName) return p;
```

### 3.5 — MODIFIED: HandleEntryChange (after line 1128)

```
// src/PropTraderTools/CopyEngine.cs line 1078 (signature unchanged, inner body addition)
// B69 DW-B69-03: preload new orderId into _dedupCache after resubmit.
// Prevents Accepted event from re-entering DispatchCopy (double-copy guard).
// CYC delta = 0 (addition is inside existing if (order != null) block, straight-line assignment).
private void HandleEntryChange(Order leaderOrder, CopyRule rule)
```

Addition inside `if (order != null)` block (after `acc.Submit`, before `StatusUpdate`):
```csharp
if (order != null)
{
    acc.Submit(new[] { order });
    // B69 DW-B69-03: preload new orderId into _dedupCache at newPrice.
    // Prevents the new order's Accepted event from re-entering DispatchCopy
    // (same-account double-copy guard, lightweight FSM-in-flight equivalent).
    // Ref: PropagateFollowerEntryReplace Build 947 -- PendingCancel absorb pattern.
    _dedupCache[order.OrderId.ToString()] = newPrice;
}
```

### 3.6 — MODIFIED: CancelQxBrackets comment (line 450)

Delete line:
```
// Also called by FlattenOneAccount (B67 DW-B67-01) before market order submission.
```
Reason: FlattenOneAccount will no longer call `CancelQxBrackets` after DW-B69-01 fix.

---

## 4. Change Map

| # | File | Line(s) | Change Type | Description |
|---|------|---------|-------------|-------------|
| 1 | `CopyEngine.cs` | 450 | Delete line | Remove stale "Also called by FlattenOneAccount" comment |
| 2 | `CopyEngine.cs` | ~471 (after line 470) | Insert | New method `CancelAllAccountOrders` (~14 lines) |
| 3 | `CopyEngine.cs` | 1473 | Replace comment | Update CYC comment to reference `CancelAllAccountOrders`; add B69 note |
| 4 | `CopyEngine.cs` | 1483 | Replace call | `CancelQxBrackets(acc, instrument)` → `CancelAllAccountOrders(acc, instrument)` |
| 5 | `CopyEngine.cs` | 1487-1490 | Replace block | Capture `CreateOrder` in `var order`; add `if (order != null) acc.Submit(new[] { order });` |
| 6 | `CopyEngine.cs` | 512 | Replace line | FullName comparison with null-guard in `SubmitBeStop` |
| 7 | `CopyEngine.cs` | 1127-1128 | Insert inside block | `_dedupCache[order.OrderId.ToString()] = newPrice;` after `acc.Submit` in `HandleEntryChange` |
| 8 | `CopyEngine.cs` | 1778 | Replace line | FullName comparison with null-guard in `FindPosition` |
| 9 | `CopyEngineTests.cs` | After line 3553 | Insert | 7 new `[Fact]` test methods (see §5) |

---

## 5. Test Plan

All tests use xUnit `[Fact]`. Appended after line 3553 in `CopyEngineTests.cs`, before the
closing `}` braces at lines 3554-3555.

---

### T_B69_01 — CancelAllAccountOrders_cancels_PTT_Copy_orders

**Purpose:** Verify that `CancelAllAccountOrders` cancels a `Working` order even when its name
is not `PTT-Qx*` (i.e. a plain `PTT-Entry` order that `CancelQxBrackets` would have skipped).

**Setup:** Stub `Account` with one order: state=`Working`, name=`PTT-Entry`,
`Instrument.FullName` matches target.

**Assertions:**
- `acc.Cancel(...)` is called with a list containing that order.
- The list contains exactly 1 order.

---

### T_B69_02 — CancelAllAccountOrders_cancels_ChangeSubmitted_orders

**Purpose:** Verify that `ChangeSubmitted` is included in the cancelled states (it was absent
from `CancelQxBrackets` which only checked `Working | Initialized | Accepted`).

**Setup:** Stub `Account` with one order: state=`ChangeSubmitted`, `Instrument.FullName` matches.

**Assertions:**
- `acc.Cancel(...)` is called once.
- Cancelled list includes the `ChangeSubmitted` order.

---

### T_B69_03 — CancelAllAccountOrders_skips_Filled_orders

**Purpose:** Confirm that `Filled` orders are not included in the cancel list (they cannot be
cancelled and passing them to `acc.Cancel` would throw a broker error).

**Setup:** Stub `Account` with two orders: one `Filled` (same instrument), one `Working` (same
instrument).

**Assertions:**
- `acc.Cancel(...)` is called with a list of exactly 1 order.
- The `Filled` order is NOT in the cancelled list.

---

### T_B69_04 — CancelAllAccountOrders_skips_different_instrument

**Purpose:** Confirm that orders for a different instrument on the same account are not cancelled
(instrument isolation — must not cancel an open ES order when flattening NQ).

**Setup:** Stub `Account` with two `Working` orders: one for target instrument, one for a
different instrument (different `FullName`).

**Assertions:**
- `acc.Cancel(...)` is called with a list of exactly 1 order (only the matching instrument).

---

### T_B69_05 — SubmitBeStop_finds_position_by_FullName

**Purpose:** Verify that `SubmitBeStop` locates a position whose `Instrument` is a distinct
object from the `instr` parameter but has an equal `FullName` (reference inequality scenario).

**Setup:**
- Create two `Instrument` stubs with same `FullName` but different object references.
- Stub `Account.Positions` with a position using the first reference.
- Call `SubmitBeStop` with the second reference.

**Assertions:**
- `acc.CreateOrder(...)` is called (position found).
- `acc.Submit(...)` is called with the created order.

---

### T_B69_06 — HandleEntryChange_preloads_new_orderId_into_dedupCache

**Purpose:** Verify that after `HandleEntryChange` submits the new follower order, the new
`order.OrderId` is present in `_dedupCache` at `newPrice`.

**Setup:**
- Stub a leader order with a known `OrderId` in `_dedupCache`.
- Stub a follower account with a matching follower entry order.
- Invoke `HandleEntryChange` with a new price.

**Assertions:**
- `_dedupCache` no longer contains the old `OrderId`.
- `_dedupCache` contains the new `order.OrderId.ToString()` key mapped to `newPrice`.

---

### T_B69_07 — CancelAllAccountOrders_null_acc_noOp

**Purpose:** Verify that `CancelAllAccountOrders` returns without throwing when `acc` is `null`
(null-guard correctness — no NRE, no exception propagation).

**Setup:** Call `CancelAllAccountOrders(null, validInstr)`.

**Assertions:**
- No exception thrown.
- No `acc.Cancel(...)` call attempted.

---

## 6. JS-DNA Compliance Checklist

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` anywhere | PASS — `_dedupCache` is `ConcurrentDictionary`; `acc.Cancel`/`acc.Submit` are NT8 broker API calls with no internal lock in our code |
| JS-001 | No `throw` in hot-path dispatch | PASS — all new code uses null-guard early-return or try/catch-swallow; no re-throw |
| JS-002 | No new `return null` sites | PASS — `FindPosition` retains pre-existing `return null` contract; not a new site |
| JS-033 | No `async void` | PASS — all new/modified methods are synchronous `void` or `internal void` |
| JS-036/037 | No heap allocation on tick hot-path | PASS — `new List<Order>()` and `new[]{order}` are on broker-event paths, not per-tick |
| ASCII-only | No Unicode, emoji, curly quotes | PASS — all string literals are ASCII |
| No DateTime.Now | Use `DateTime.UtcNow` or `DateTime.MaxValue` | PASS — existing `DateTime.MaxValue` in `CreateOrder` calls unchanged |
| PTT- prefix | All `CreateOrder` names use `PTT-` prefix | PASS — `"PTT-Flatten"` unchanged; `"PTT-BE-Stop"` and `fo.Name` unchanged |
| No FontFamily / hex | No hardcoded hex colors or FontFamily | PASS — backend methods, no UI |
| CYC ≤ 8 | Every method within 8 branches | PASS — see §3 annotations: max CYC=4 for new method |
| FullName identity | Instrument comparison via `FullName` | PASS — all new comparisons use `FullName`; existing sites in scope also use `FullName` |

---

## 7. Spec Requirement Traceability

### DW-B69-01 — CancelAllAccountOrders + FlattenOneAccount Submit

| Artifact | Detail |
|----------|--------|
| **Requirement** | Flatten must cancel ALL active orders for the instrument (name-agnostic) then submit a market order. |
| **Root cause** | `CancelQxBrackets` is name-gated (only PTT-Qx* and ATM bracket names). `CreateOrder` result was not submitted. |
| **Fix A** | New `CancelAllAccountOrders` inserted after `CancelQxBrackets` block (~line 470); called from `FlattenOneAccount` line 1483. |
| **Fix B** | `FlattenOneAccount` lines 1487-1490: capture `var order`, add `acc.Submit(new[]{order})` guard. |
| **Comment cleanup** | `CancelQxBrackets` line 450: remove stale reference to `FlattenOneAccount`. |
| **Tests** | T_B69_01, T_B69_02, T_B69_03, T_B69_04, T_B69_07 |
| **NT8 authority** | `[938-EF-GUARD]` EmergencyFlattenSingleFleetAccount pattern; `acc.Submit()` requirement from NT8_FULL_REFERENCE.md and SubmitBeStop lines 524-525. |

---

### DW-B69-02 — FullName instrument comparison in SubmitBeStop and FindPosition

| Artifact | Detail |
|----------|--------|
| **Requirement** | Instrument identity must use `FullName` string comparison, not reference equality. |
| **Root cause** | NT8 can produce distinct `Instrument` objects for the same logical instrument across accounts. Reference equality (`==`) fails silently. |
| **Fix** | `SubmitBeStop` line 512: `p.Instrument != null && p.Instrument.FullName == instr.FullName`. `FindPosition` line 1778: same pattern. |
| **Tests** | T_B69_05 |
| **NT8 authority** | `NT8_FULL_REFERENCE.md` line 1926. Existing `CancelQxBrackets` line 463 pattern. |

---

### DW-B69-03 — HandleEntryChange _dedupCache preload

| Artifact | Detail |
|----------|--------|
| **Requirement** | After submitting a replacement follower order, the new `OrderId` must be preloaded into `_dedupCache` to prevent `DispatchCopy` double-copy on the `Accepted` event. |
| **Root cause** | Line 1091-1093 comment erroneously stated "re-keyed by DispatchCopy on Accepted event" — that path did not exist, leaving a race window. |
| **Fix** | After `acc.Submit(new[] { order })` (inside `if (order != null)` block, before `StatusUpdate?.Invoke`): `_dedupCache[order.OrderId.ToString()] = newPrice;` |
| **Tests** | T_B69_06 |
| **Reference** | PropagateFollowerEntryReplace Build 947: absorb in-flight state without a second cancel. `_dedupCache` ConcurrentDictionary atomic write closes the race window. |

---

## 8. Insertion Point Precision

```
CopyEngine.cs insertion summary:

Line 450        DELETE:  "// Also called by FlattenOneAccount (B67 DW-B67-01)..."
After line 470  INSERT:  CancelAllAccountOrders method (~14 lines)
Line 1473       REPLACE: CYC comment -- add CancelAllAccountOrders + B69 note
Line 1483       REPLACE: CancelQxBrackets call -> CancelAllAccountOrders call
Lines 1487-1491 REPLACE: CreateOrder block -- capture var order + add Submit guard
Line 512        REPLACE: reference equality -> FullName with null-guard
Lines 1127-1128 INSERT:  _dedupCache preload inside existing if (order != null) block
Line 1778       REPLACE: reference equality -> FullName with null-guard

CopyEngineTests.cs:
After line 3553 INSERT:  7 [Fact] test methods (T_B69_01 .. T_B69_07)
```

---

## 9. Pre-flight Sign-off

| Check | Result |
|-------|--------|
| All 8 sequential thoughts completed | PASS |
| JS P0 rules (lock, throw, async void, return null) | PASS |
| CYC ≤ 8 for all methods | PASS |
| NT8 API surface verified | PASS |
| Threading model verified (no new Dispatcher, no new ConcurrentQueue) | PASS |
| File split: 2 files, zero cross-contamination | PASS |
| No .cs files authored by ptt-architect | PASS — plan only |
| xUnit [Fact] tests only (no NUnit/MSTest) | PASS |
| ASCII-only identifiers and strings | PASS |
| PTT- prefix on all CreateOrder calls | PASS |

---

**PLAN STATUS: REVIEW_PENDING**
