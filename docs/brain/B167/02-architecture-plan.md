# B167 — Architecture Plan
**Status:** PLAN_COMPLETE  
**Pipeline:** SINGLE-PIPELINE (sequential tickets, same source file)  
**Source file:** `src/PropTraderTools/CopyEngine.cs`  
**LANE-SPLIT GATE RESULT:** SINGLE-PIPELINE (sequential tickets, same source file)

---

## §1 Block Identity

| Field | Value |
|---|---|
| Block | B167 |
| Ticket count | 2 |
| Pipeline | Single-pipeline, sequential |
| Write-set | `src/PropTraderTools/CopyEngine.cs` only |
| Spec cards | DW-B167-01 (engineering required) + DW-B167-02 (formal record only) |

Sequential requirement: both tickets write to the same `.cs` file. Concurrent lanes would produce a merge conflict. Ticket 1 must be completed, reviewed, and merged before Ticket 2 is processed.

---

## §2 Ticket 1 — DW-B167-01: DRAIN-ABORT-FLAT kills pre-fill entry drag

### Problem Statement

`SubmitDrainedEntry` guard (3) at L8663:

```csharp
if (IsFlat(FindPosition(follower, payload.Instrument)))
```

This guard is correct for the post-flatten race scenario (follower closed position before drain completes) but fires incorrectly for the **pre-fill entry drag** scenario: when a follower's entry order was never filled, the account is flat because the position never opened — not because it was closed. The guard cannot distinguish between these two cases with the current data model.

### Root Cause

`PendingDispatchDrain` captures no snapshot of position state at the moment the drain was initiated. At submission time (`SubmitDrainedEntry`), `IsFlat` returns `true` for both the "was flat all along (drag)" and "became flat after fill+close (QX race)" scenarios.

### Design — 4 Surgical Touches

#### T1: `PendingDispatchDrain` class (L9613–L9653)

**Add property** after `TimestampTicks`:
```csharp
internal bool HadPositionAtDrainTime { get; private set; }
```

**Add constructor parameter** after `long timestampTicks`:
```csharp
bool hadPositionAtDrainTime
```

**Add constructor assignment**:
```csharp
HadPositionAtDrainTime = hadPositionAtDrainTime;
```

No other changes to `PendingDispatchDrain`. This is a pure data-class extension. CYC = 0. Unchanged.

#### T2: `DrainThenDispatch` (L8525–L8536)

Before constructing the `PendingDispatchDrain` payload, capture the position snapshot:

```csharp
bool hadPos = !IsFlat(FindPosition(follower, instrument));
```

Pass `hadPos` as the last constructor argument to `PendingDispatchDrain` (after `now`):

```csharp
var payload = new PendingDispatchDrain(
    acctKey, instrument, qty, price, action, orderType,
    drainedIds, follower,
    pendingCancelCount: entryCandidates.Count,
    now,
    hadPos      // <-- new last arg
);
```

`FindPosition` and `IsFlat` are lock-free read-only calls. No new branches introduced. `DrainThenDispatch` CYC unchanged (≤4).

#### T3: `SubmitDrainedEntry` guard at L8663

Change:
```csharp
if (IsFlat(FindPosition(follower, payload.Instrument)))
```

To:
```csharp
if (payload.HadPositionAtDrainTime && IsFlat(FindPosition(follower, payload.Instrument)))
```

This is a modification of the existing condition, not a new branch. Short-circuit evaluation means `IsFlat` is only evaluated when `HadPositionAtDrainTime` is `true`. No new control-flow vertex added to CYC. `SubmitDrainedEntry` CYC remains 5.

#### T4: `SubmitDrainedEntry` — pre-fill diagnostic log

Add **before** `SubmitEntryDirect` call (L8673), on the resubmit path:

```csharp
if (!payload.HadPositionAtDrainTime)
    NinjaTrader.Code.Output.Process(
        "[DRAIN-SUBMIT-PREFILL] acct=" + acctKey,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```

This adds one `if` branch. `SubmitDrainedEntry` CYC: 5 → 6. ≤ 8. PASS.

### Guard Logic Table

| Scenario | HadPositionAtDrainTime | IsFlat after drain | Guard fires? | Result |
|---|---|---|---|---|
| Pre-fill drag | `false` | `true` (never filled) | `false && true = NO` | Resubmit ✅ |
| Post-flatten race | `true` | `true` (closed) | `true && true = YES` | Abort ✅ |
| Normal drain (open pos) | `true` | `false` (still open) | `true && false = NO` | Resubmit ✅ |

### CYC Proof

| Method | CYC Before | Change | CYC After | Limit | Status |
|---|---|---|---|---|---|
| `PendingDispatchDrain` ctor | 0 | new property/param (no branches) | 0 | 8 | PASS |
| `DrainThenDispatch` | ≤4 | local variable, no branch | ≤4 | 8 | PASS |
| `SubmitDrainedEntry` T3 | 5 | modify existing condition (no new vertex) | 5 | 8 | PASS |
| `SubmitDrainedEntry` T4 | 5 | +1 `if` branch | 6 | 8 | PASS |

### CRITICAL Naming Rule

The property is **`HadPositionAtDrainTime`** — this is the authoritative name for this block.

> The DW card DW-B167-01 uses the alias `WasFlat`. That alias is **REJECTED**. All artifacts — this plan, the tickets, and the engineer's implementation — MUST use `HadPositionAtDrainTime`. Using `WasFlat` anywhere is a protocol violation.

### Method Signatures (unchanged — no new public API)

All modified methods retain their existing signatures. The only signature change is the internal `PendingDispatchDrain` constructor, which is a private nested class.

```csharp
// UNCHANGED external signature:
private void DrainThenDispatch(Account follower, Instrument instrument, int qty, double price, OrderAction action, OrderType orderType)
private void SubmitDrainedEntry(string acctKey)

// INTERNAL ONLY — PendingDispatchDrain ctor (new last param):
// (string followerAcctKey, Instrument instrument, int qty, double price,
//  OrderAction action, OrderType orderType, IReadOnlyList<string> drainedOrderIds,
//  Account followerAccount, int pendingCancelCount, long timestampTicks,
//  bool hadPositionAtDrainTime)   // <-- added
```

---

## §3 Ticket 2 — DW-B167-02: Formal Record (already applied)

### Status

**Fix already applied to source.** ptt-engineer for Ticket 2 performs READ-ONLY source confirmation. Zero `.cs` edits permitted.

### What Was Applied

`TrySetSlateReadyOnDrainFill` guard at L1032 was extended from:

```csharp
if (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
    return;
```

To:

```csharp
if (!IsPttCopyEntry(order))
    return;
```

Where `IsPttCopyEntry` at L8313:

```csharp
private static bool IsPttCopyEntry(Order o) =>
    o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) || o.Name == "Entry";
```

### Source Verification Criteria

Engineer must confirm:
1. L1032 reads `if (!IsPttCopyEntry(order))` — not the old `StartsWith("PTT-Copy")` inline form.
2. `IsPttCopyEntry` at L8313 matches the signature above exactly (both clauses present, `StringComparison.Ordinal`, `|| o.Name == "Entry"`).
3. `TrySetSlateReadyOnDrainFill` full body at L1026–L1044 matches the source context provided in this plan.

### SIM Gate Criteria

Carried forward from `no-pipeline-repairs.md` DW-B167-02 entry (see §7 below).

---

## §4 Jane Street Compliance Matrix

| Rule | Ticket 1 | Ticket 2 | Status |
|---|---|---|---|
| **JS-001** no throw in dispatch path | No throws added. `bool hadPos` assignment and condition change cannot throw. | No code changes. | PASS |
| **JS-021** no `lock()` | `IsFlat` and `FindPosition` are lock-free read-only helpers. `HadPositionAtDrainTime` is written once at construction (immutable after). No `lock()` introduced. | No code changes. | PASS |
| **JS-CYC** ≤8 per method | `SubmitDrainedEntry`: 5→6 (T4 adds one branch). All other methods unchanged. Max CYC = 6. | N/A (no code changes). | PASS |
| **JS-002** no null return | All modified methods are `void`. `HadPositionAtDrainTime` is `bool` (value type, cannot be null). | N/A. | PASS |
| **ASCII-only** no Unicode in string literals | `"[DRAIN-SUBMIT-PREFILL] acct="` — all ASCII characters. No Unicode, no curly quotes, no emoji. | `"[SLATE-READY-DRAIN]"` already in source, confirmed ASCII. | PASS |
| **No `DateTime.Now`** | Not used. No time references added. | N/A. | PASS |
| **PTT- prefix on CreateOrder** | No `CreateOrder` calls added or modified. | N/A. | PASS |
| **No FontFamily / no hex colors** | No UI changes. | N/A. | PASS |

---

## §5 Test Plan

All tests are xUnit `[Fact]`. Tests are in the PropTraderTools test project, targeting `CopyEngine` internal methods via `InternalsVisibleTo` or test-friend pattern already established in the test suite.

### Ticket 1 — 4 Unit Tests

**Test 1:** `SubmitDrainedEntry_HadPositionFalse_DoesNotAbort`
- Setup: Build `PendingDispatchDrain` with `hadPositionAtDrainTime = false`. Mock `IsFlat` to return `true`.
- Assert: Guard (3) does NOT fire. `SubmitEntryDirect` IS called. `[DRAIN-ABORT-FLAT]` NOT logged.
- Confirms: Pre-fill drag path resubmits correctly.

**Test 2:** `SubmitDrainedEntry_HadPositionTrue_IsFlat_Aborts`
- Setup: `hadPositionAtDrainTime = true`. Mock `IsFlat` to return `true`.
- Assert: Guard (3) fires. `SubmitEntryDirect` NOT called. `[DRAIN-ABORT-FLAT]` IS logged.
- Confirms: Post-flatten race abort path preserved.

**Test 3:** `SubmitDrainedEntry_HadPositionTrue_NotFlat_Submits`
- Setup: `hadPositionAtDrainTime = true`. Mock `IsFlat` to return `false`.
- Assert: Guard (3) does NOT fire. `SubmitEntryDirect` IS called.
- Confirms: Normal drain with open position proceeds correctly.

**Test 4:** `DrainThenDispatch_CapturesHadPositionAtDrainTime`
- Setup: Follower has open position (mock `IsFlat` returns `false` for `FindPosition`).
- Assert: `PendingDispatchDrain.HadPositionAtDrainTime == true` in the payload added to `_pendingDispatchDrains`.
- Confirms: Position snapshot is captured correctly at drain-time.

### Ticket 2 — 1 Structural Confirmation Test

**Test 5:** `TrySetSlateReadyOnDrainFill_EntryFill_SetsSlateReady`
- Setup: Order with `Name = "Entry"`, `OrderState = Filled`, `IsFollowerAccount = true`, `IsFlat = false`.
- Assert: `_followerSlateReady[order.Account.Name] == true`. `[SLATE-READY-DRAIN]` IS logged.
- Confirms: DW-B167-02 extended guard covers `Name == "Entry"` fills.

---

## §6 Deferred Items

**ECA-1** (from 5-subagent adversarial review):

- **Scenario:** Pre-fill drag + manual follower fill race within ~100–500 ms window. Follower fill arrives and sets a position open, then `SubmitDrainedEntry` fires with `HadPositionAtDrainTime=false` and skips the flat-guard, resubmitting a second entry. This could result in a double position.
- **Probability:** Low. Requires precise timing alignment of a manual drag and a slow fill.
- **Mitigation in this block:** `[DRAIN-SUBMIT-PREFILL]` diagnostic log (T4) will surface this in output if it occurs.
- **Disposition:** DO NOT block pipeline. If reviewer determines a DW card is warranted, log as new deferred item in `06-deferred-backlog.md`. No code change required in B167.

No other edge cases require DW cards from this block.

---

## §7 SIM Gate Criteria

Both tickets are verified together in a single SIM session after the engineer completes both tickets.

### Ticket 1 — Pre-fill drag test

1. Place a **limit entry** on the leader instrument. Do NOT fill it (leave it as Working).
2. While entry is Working, **drag the leader entry to a new price**.
3. Observe Output Tab 1.
4. **Required:** `[DRAIN]` log line appears for each follower account.
5. **Required:** `[DRAIN-SUBMIT-PREFILL]` log line appears for each follower account.
6. **Forbidden:** Zero `[DRAIN-ABORT-FLAT]` lines during this test sequence.
7. After drain completes, follower accounts show Working entries at the **new (dragged) price**.

### Ticket 1 — Post-flatten abort test

8. Fill the entry on leader. Confirm position opens on all followers.
9. Press QX to close the position on leader.
10. If a drain races the QX close: `[DRAIN-ABORT-FLAT]` MUST appear (`HadPositionAtDrainTime=true` + `IsFlat=true` path).
11. No double positions or orphaned Working entries after QX.

### Ticket 2 — SLATE-READY-DRAIN test

12. Fill an `Entry` order on leader (Clone/Named-ATM mode fill).
13. Wait 3 seconds for all follower order state propagation.
14. `[SLATE-READY-DRAIN]` MUST appear for **all 3 followers** (Sim102, Sim103, Sim104).
15. Drag the leader stop after fill. All 3 followers show `fo=StopN` (not `fo=NULL`).
16. **Sim104 specifically** must show `fo=Stop1 FOUND` on the first drag after fill.

---

## §8 File Map

| Touch | File | Lines | Change Type |
|---|---|---|---|
| T1 | `src/PropTraderTools/CopyEngine.cs` | L9613–L9653 | Add property + ctor param + assignment to `PendingDispatchDrain` |
| T2 | `src/PropTraderTools/CopyEngine.cs` | L8525–L8536 | Add `hadPos` local, pass as last ctor arg |
| T3 | `src/PropTraderTools/CopyEngine.cs` | L8663 | Modify guard condition (add `payload.HadPositionAtDrainTime &&`) |
| T4 | `src/PropTraderTools/CopyEngine.cs` | L8673 area | Add diagnostic `if` block before `SubmitEntryDirect` |
| Ticket 2 | `src/PropTraderTools/CopyEngine.cs` | L1026–L1044 | READ-ONLY confirmation. Zero edits. |

**Total net-new lines (Ticket 1):** ~5 lines of production code, ~2 lines of diagnostic log. No existing logic deleted.

---

*Plan authored by ptt-architect. Reviewed by: pending (02-plan-review.md).*
