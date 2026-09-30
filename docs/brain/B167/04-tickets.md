# B167 — Engineering Tickets
**Status:** TICKETS_COMPLETE  
**Plan source:** `docs/brain/B167/02-architecture-plan.md` (REVIEW_PASS)  
**Reviewer confirmation:** `docs/brain/B167/02-plan-review.md` — 22/22 checks PASS  
**Pipeline:** SINGLE-PIPELINE — Ticket 1 must be merged before Ticket 2 is processed.  
**Source file (both tickets):** `src/PropTraderTools/CopyEngine.cs`

---

## TICKET 1 — DW-B167-01: DRAIN-ABORT-FLAT guard tightening

**Spec req IDs:** DW-B167-01  
**Source file:** `src/PropTraderTools/CopyEngine.cs`  
**Methods touched:** `PendingDispatchDrain` (data class), `DrainThenDispatch`, `SubmitDrainedEntry`  
**Net-new production lines:** ~5 (plus ~2 diagnostic log lines)  
**No existing logic deleted.**

---

### Method Signatures

```csharp
// UNCHANGED external signatures:
private void DrainThenDispatch(
    Account follower,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType)

private void SubmitDrainedEntry(string acctKey)

// INTERNAL-ONLY — PendingDispatchDrain constructor signature AFTER Ticket 1:
internal PendingDispatchDrain(
    string followerAcctKey,
    Instrument instrument,
    int qty,
    double price,
    OrderAction action,
    OrderType orderType,
    IReadOnlyList<string> drainedOrderIds,
    Account followerAccount,
    int pendingCancelCount,
    long timestampTicks,
    bool hadPositionAtDrainTime)   // <-- new last param
```

---

### Touch T1 — `PendingDispatchDrain` class (L9613 region)

**Location:** The `PendingDispatchDrain` data class, approximately L9613–L9653. Three sub-edits:

#### T1-A — Add property (after the `TimestampTicks` property)

**BEFORE:**
```csharp
internal long TimestampTicks { get; private set; }
```

**AFTER:**
```csharp
internal long TimestampTicks { get; private set; }
internal bool HadPositionAtDrainTime { get; private set; }
```

#### T1-B — Add constructor parameter (after `long timestampTicks`)

Locate the constructor parameter list. The current last parameter is `long timestampTicks`.

**BEFORE (last two params of ctor signature):**
```csharp
    int pendingCancelCount,
    long timestampTicks)
```

**AFTER:**
```csharp
    int pendingCancelCount,
    long timestampTicks,
    bool hadPositionAtDrainTime)
```

#### T1-C — Add constructor body assignment (after `TimestampTicks = timestampTicks;`)

**BEFORE:**
```csharp
TimestampTicks = timestampTicks;
```

**AFTER:**
```csharp
TimestampTicks = timestampTicks;
HadPositionAtDrainTime = hadPositionAtDrainTime;
```

**CYC impact:** Zero. This is a data class. No control-flow added.

---

### Touch T2 — `DrainThenDispatch` (L8525 region)

**Location:** Inside `DrainThenDispatch`, immediately before the `new PendingDispatchDrain(...)` constructor call (approximately L8525–L8536).

#### T2-A — Add `hadPos` local variable capture

Insert this line immediately before the `var payload = new PendingDispatchDrain(...)` line:

```csharp
bool hadPos = !IsFlat(FindPosition(follower, instrument));
```

> **CRITICAL:** Use `!IsFlat(FindPosition(...))` — NOT `HasOpenPosition` or any other helper.

#### T2-B — Pass `hadPos` as new last argument to `PendingDispatchDrain`

**BEFORE:**
```csharp
var payload = new PendingDispatchDrain(
    acctKey,
    instrument,
    qty,
    price,
    action,
    orderType,
    drainedIds,
    follower,
    pendingCancelCount: entryCandidates.Count,
    now
);
```

**AFTER:**
```csharp
var payload = new PendingDispatchDrain(
    acctKey,
    instrument,
    qty,
    price,
    action,
    orderType,
    drainedIds,
    follower,
    pendingCancelCount: entryCandidates.Count,
    now,
    hadPos
);
```

**CYC impact:** Zero. `hadPos` is a local variable assignment with no branch. `FindPosition` and `IsFlat` are lock-free read-only calls (JS-021 PASS).

---

### Touch T3 — `SubmitDrainedEntry` guard (L8663)

**Location:** `SubmitDrainedEntry` method, guard (3), approximately L8663.

**BEFORE:**
```csharp
if (IsFlat(FindPosition(follower, payload.Instrument))) // (3) NEW -- abort if flat
```

**AFTER:**
```csharp
if (payload.HadPositionAtDrainTime && IsFlat(FindPosition(follower, payload.Instrument))) // (3) abort if flat and had position at drain-start
```

> **Logic table for the modified guard:**
> 
> | Scenario | `HadPositionAtDrainTime` | `IsFlat` | Guard fires? | Result |
> |---|---|---|---|---|
> | Pre-fill entry drag | `false` | `true` (never filled) | `false && true = NO` | Resubmit ✅ |
> | Post-flatten QX race | `true` | `true` (position closed) | `true && true = YES` | Abort ✅ |
> | Normal drain, open position | `true` | `false` (still open) | `true && false = NO` | Resubmit ✅ |

**CYC impact:** T3 **modifies** the existing condition expression. No new branch vertex added. `SubmitDrainedEntry` CYC remains 5 after T3.

---

### Touch T4 — `SubmitDrainedEntry` diagnostic log (before `SubmitEntryDirect`)

**Location:** `SubmitDrainedEntry` method, immediately before the `SubmitEntryDirect(...)` call (approximately L8673 area). This is on the resubmit path — AFTER guard T3, NOT inside the guard's abort branch.

**BEFORE (the SubmitEntryDirect call, no log above it):**
```csharp
SubmitEntryDirect(/* ... existing args ... */);
```

**AFTER (add the diagnostic block immediately above `SubmitEntryDirect`):**
```csharp
if (!payload.HadPositionAtDrainTime)
    NinjaTrader.Code.Output.Process(
        "[DRAIN-SUBMIT-PREFILL] acct=" + acctKey,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
SubmitEntryDirect(/* ... existing args unchanged ... */);
```

> **String literal check:** `"[DRAIN-SUBMIT-PREFILL] acct="` — all ASCII. No Unicode, no curly quotes, no emoji. JS ASCII-only PASS.

**CYC impact:** +1 (one new `if` branch). `SubmitDrainedEntry` CYC: 5 → 6. ≤ 8. PASS.

---

### 7-Scan Checklist — Ticket 1

- [ ] **SCAN-01 JS-001: No new throw paths.**  
  `bool hadPos = !IsFlat(FindPosition(...))` — cannot throw (read-only, null-safe helper).  
  `HadPositionAtDrainTime` is a `bool` property assignment — cannot throw.  
  Guard modification (`&&` prepend) — cannot throw.  
  Diagnostic `if` block — `Output.Process` does not throw in the dispatch path.  
  **PASS.**

- [ ] **SCAN-02 JS-021: No new `lock()` blocks.**  
  `IsFlat` and `FindPosition` are lock-free read-only helpers (confirmed in plan §4).  
  `HadPositionAtDrainTime` is written once at construction time (immutable after initialization).  
  No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` introduced anywhere.  
  **PASS.**

- [ ] **SCAN-03 JS-CYC: All modified methods ≤ 8.**  
  `PendingDispatchDrain` ctor: CYC = 0 (data class, no branches). Unchanged. PASS.  
  `DrainThenDispatch`: local variable + ctor arg added, zero branches. CYC unchanged (≤4). PASS.  
  `SubmitDrainedEntry` after T3: condition modification, no new vertex. CYC = 5. PASS.  
  `SubmitDrainedEntry` after T4: +1 `if` branch. CYC = 6 ≤ 8. PASS.  
  **All PASS.**

- [ ] **SCAN-04 JS-002: No null-returning methods changed or added.**  
  `HadPositionAtDrainTime` is `bool` (value type, cannot be null).  
  All modified methods are `void` or constructors.  
  `IsFlat` and `FindPosition` are unchanged helpers.  
  **PASS.**

- [ ] **SCAN-05 ASCII-only: All new string literals are ASCII.**  
  `"[DRAIN-SUBMIT-PREFILL] acct="` — ASCII only. No Unicode, no curly quotes, no emoji.  
  `"[DRAIN-ABORT-FLAT]"` — unchanged, already ASCII.  
  **PASS.**

- [ ] **SCAN-06 AbortDrainOnFill: ZERO changes.**  
  `AbortDrainOnFill` method is not referenced in any T1–T4 change instruction.  
  The abort-on-fill field is irrelevant to the pre-fill path.  
  **PASS.**

- [ ] **SCAN-07 IsFollowerPositionBlocked: ZERO changes.**  
  `IsFollowerPositionBlocked` (B161–B166 fixes) is not touched, referenced, or called  
  anywhere in T1–T4 change instructions.  
  **PASS.**

---

### xUnit `[Fact]` Tests — Ticket 1

All 4 tests are in the PropTraderTools test project, targeting `CopyEngine` internal methods via the existing `InternalsVisibleTo` or test-friend pattern.

#### Test 1 — `SubmitDrainedEntry_HadPositionFalse_DoesNotAbort`

**Setup:**  
- Construct `PendingDispatchDrain` with `hadPositionAtDrainTime = false`.  
- Mock / stub `IsFlat(FindPosition(...))` to return `true`.

**Assert:**  
- Guard (3) (`payload.HadPositionAtDrainTime && IsFlat(...)`) evaluates to `false && true = false`.  
- Guard does NOT fire. `SubmitEntryDirect` IS called.  
- `[DRAIN-ABORT-FLAT]` is NOT logged.  
- `[DRAIN-SUBMIT-PREFILL]` IS logged (because `!payload.HadPositionAtDrainTime` is true).

**Confirms:** Pre-fill drag path resubmits correctly. No regression on T3.

---

#### Test 2 — `SubmitDrainedEntry_HadPositionTrue_IsFlat_Aborts`

**Setup:**  
- Construct `PendingDispatchDrain` with `hadPositionAtDrainTime = true`.  
- Mock / stub `IsFlat(FindPosition(...))` to return `true`.

**Assert:**  
- Guard (3) evaluates to `true && true = true`.  
- Guard fires. `SubmitEntryDirect` is NOT called.  
- `[DRAIN-ABORT-FLAT]` IS logged.

**Confirms:** Post-flatten QX race abort path preserved. The guard change does not break the original B155 DW-B154-04 fix.

---

#### Test 3 — `SubmitDrainedEntry_HadPositionTrue_NotFlat_Submits`

**Setup:**  
- Construct `PendingDispatchDrain` with `hadPositionAtDrainTime = true`.  
- Mock / stub `IsFlat(FindPosition(...))` to return `false`.

**Assert:**  
- Guard (3) evaluates to `true && false = false`.  
- Guard does NOT fire. `SubmitEntryDirect` IS called.

**Confirms:** Normal drain with open position proceeds correctly (the common path).

---

#### Test 4 — `DrainThenDispatch_CapturesHadPositionAtDrainTime`

**Setup:**  
- Follower account has an open position (mock `IsFlat(FindPosition(...))` returns `false` at drain time).

**Assert:**  
- The `PendingDispatchDrain` payload added to `_pendingDispatchDrains` has `HadPositionAtDrainTime == true`.

**Confirms:** Position snapshot is captured correctly at drain initiation time (T2 capture line).

---

### SIM Gate Criteria — Ticket 1

Executed in a single SIM session after Ticket 1 implementation is complete.

#### Pre-fill drag test (primary regression scenario)

1. Place a **limit entry** on the leader instrument. Do NOT fill it (leave as Working).
2. While entry is Working, **drag the leader entry to a new price**.
3. Observe Output Tab 1.
4. **REQUIRED:** `[DRAIN] acct=SimXXX cancel-sent=1` appears for each follower account.
5. **REQUIRED:** `[DRAIN-SUBMIT-PREFILL] acct=SimXXX` appears for each follower account.
6. **FORBIDDEN:** Zero `[DRAIN-ABORT-FLAT]` lines during this test sequence.
7. After drain completes, follower accounts show Working entries at the **new (dragged) price**.

#### Post-flatten abort test (regression preservation)

8. Fill the entry on leader. Confirm position opens on all followers.
9. Press QX to close the position on leader.
10. If a drain races the QX close: `[DRAIN-ABORT-FLAT]` MUST appear (`HadPositionAtDrainTime=true` + `IsFlat=true` path fires correctly).
11. No double positions or orphaned Working entries after QX.

---

## TICKET 2 — DW-B167-02: TrySetSlateReadyOnDrainFill Formal Record

**Spec req IDs:** DW-B167-02  
**Source file:** `src/PropTraderTools/CopyEngine.cs`  
**Methods touched:** NONE — formal record and source confirmation only.  
**ENGINEER INSTRUCTION: READ-ONLY. Zero `.cs` edits permitted for this ticket.**

---

### Method Signatures (reference only — no changes)

```csharp
// Confirmation target — no signature change:
private void TrySetSlateReadyOnDrainFill(Order order, string acctKey)

// Existing helper — no signature change:
private static bool IsPttCopyEntry(Order o) =>
    o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) || o.Name == "Entry";
```

---

### Engineer Instructions (READ-ONLY)

**Step 1.** Open `src/PropTraderTools/CopyEngine.cs`.  

**Step 2.** Navigate to approximately L1032. Confirm the line reads:

```csharp
if (!IsPttCopyEntry(order))  // (3) DW-B167-02: covers PTT-Copy + Entry
```

The line must NOT read the old form:
```csharp
if (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))
```

If the line reads the old form, this ticket FAILS — escalate to ptt-architect before any edit.

**Step 3.** Navigate to approximately L8313. Confirm `IsPttCopyEntry` reads:

```csharp
private static bool IsPttCopyEntry(Order o) =>
    o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal) || o.Name == "Entry";
```

Both clauses must be present:
- `o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)` — standard drain mode coverage
- `|| o.Name == "Entry"` — Clone / Named-ATM mode coverage

**Step 4.** Confirm the source matches the `no-pipeline-repairs.md` DW-B167-02 entry exactly (BEFORE/AFTER blocks at lines 614–628 of that file).

**Step 5.** Document confirmation in `docs/brain/B167/ticket-2-completion.md` with:
- Actual L1032 line content (copy-paste from source)
- Actual `IsPttCopyEntry` line content (copy-paste from source)
- Confirmation that both match `no-pipeline-repairs.md` DW-B167-02 entry
- Date and engineer identity

**Make zero edits to `CopyEngine.cs`.**

---

### Background (for engineer context — no action required)

The fix was applied directly to source (no-pipeline-repairs.md pattern) on 2026-09-18.

**What was changed at the time:**

`TrySetSlateReadyOnDrainFill` guard at L1032 was changed from:

```csharp
// BEFORE (old — must NOT be present):
if (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))   // (3)
    return;
```

To:

```csharp
// AFTER (current — must be present at L1032):
if (!IsPttCopyEntry(order))   // (3) DW-B167-02: covers PTT-Copy + Entry
    return;
```

**Why the fix is required:**  
In Clone/Named-ATM mode NT8 names follower entry orders `"Entry"`, not `"PTT-Copy"`. The old guard failed to recognize these fills, leaving `_followerSlateReady[Sim104] = false`, causing `FindFollowerBracketOrder` to return `null` on subsequent drag events (`fo=NULL` in output). This ticket formally records that the fix was applied and verified in source.

---

### 7-Scan Checklist — Ticket 2

- [ ] **SCAN-01 JS-001: No throw paths.**  
  No code changes. Trivially PASS.

- [ ] **SCAN-02 JS-021: No `lock()` blocks.**  
  No code changes. Trivially PASS.

- [ ] **SCAN-03 JS-CYC: CYC delta = 0.**  
  Confirmed by `no-pipeline-repairs.md` DW-B167-02 entry: guard (3) replaces one `StartsWith` call with one `IsPttCopyEntry` call. McCabe count for `TrySetSlateReadyOnDrainFill` is unchanged. PASS.

- [ ] **SCAN-04 JS-002: No null return.**  
  No code changes. Trivially PASS.

- [ ] **SCAN-05 ASCII-only.**  
  No code changes. Trivially PASS. `"[SLATE-READY-DRAIN]"` already in source (ASCII, confirmed).

- [ ] **SCAN-06 Source-match.**  
  Current L1032 must match the `no-pipeline-repairs.md` DW-B167-02 AFTER block exactly.  
  Current `IsPttCopyEntry` at L8313 must contain both clauses (`StartsWith("PTT-Copy")` AND `|| o.Name == "Entry"`).  
  Confirmed by engineer in ticket-2-completion.md.

- [ ] **SCAN-07 IsFollowerPositionBlocked: ZERO changes.**  
  No code changes anywhere in this ticket. `IsFollowerPositionBlocked` (B161–B166 fixes) is untouched.  
  **PASS.**

---

### xUnit `[Fact]` Test — Ticket 2

#### Test 5 — `TrySetSlateReadyOnDrainFill_EntryFill_SetsSlateReady`

**Setup:**  
- Order with `Name = "Entry"`, `OrderState = OrderState.Filled`.  
- `IsFollowerAccount(order.Account)` returns `true` (follower account, e.g., Sim104).  
- `IsFlat(FindPosition(account, order.Instrument))` returns `false` (position just opened after fill).

**Assert:**  
- `_followerSlateReady[order.Account.Name] == true` after the method returns.  
- `[SLATE-READY-DRAIN]` IS logged to OutputTab1.

**Confirms:** The DW-B167-02 extended `IsPttCopyEntry` guard passes for `Name == "Entry"` fills, restoring `_followerSlateReady` on the correct path and enabling subsequent `SyncFollowerBracket` calls to find `fo=Stop1` (not `fo=NULL`).

---

### SIM Gate Criteria — Ticket 2

Executed in the same SIM session as Ticket 1 (single combined session per §7 of the architecture plan).

12. Fill an `Entry` order on leader (Clone/Named-ATM mode fill).
13. Wait 3 seconds for all follower order state propagation.
14. **REQUIRED:** `[SLATE-READY-DRAIN]` appears for ALL 3 followers (Sim102, Sim103, Sim104).
15. Drag the leader stop after fill. All 3 followers show `fo=Stop1 FOUND` (not `fo=NULL`).
16. **Sim104 specifically** must show `fo=Stop1 FOUND` on the first drag after fill.

---

## Combined SIM Session Sequence

Both tickets are verified in a single SIM session (architecture plan §7):

| Step | Test | Required outcome |
|---|---|---|
| 1 | Place limit entry, drag before fill | `[DRAIN-SUBMIT-PREFILL]` fires; `[DRAIN-ABORT-FLAT]` does NOT fire |
| 2 | Follower orders after drag | Working entries at new (dragged) price on all followers |
| 3 | Fill entry, press QX | `[DRAIN-ABORT-FLAT]` fires (post-flatten guard preserved) |
| 4 | Clone-mode fill | `[SLATE-READY-DRAIN]` fires for Sim102, Sim103, Sim104 |
| 5 | Drag stop after fill | `fo=Stop1 FOUND` for all 3 followers; Sim104 not `fo=NULL` |

---

## Deferred (ECA-1)

Not an action item for B167. Documented in `docs/brain/B167/06-deferred-backlog.md`.

**Scenario:** Pre-fill drag + manual follower fill race within ~100–500 ms. Follower fills into position, then `SubmitDrainedEntry` fires with `HadPositionAtDrainTime=false`, skips the flat-guard, and resubmits — potentially resulting in a double position.

**Probability:** Low. Requires precise timing alignment.  
**Monitoring:** `[DRAIN-SUBMIT-PREFILL]` diagnostic log (T4) surfaces this if it occurs in live use.  
**Action:** If seen in live output, raise a new DW card. No code change required in B167.

---

*Tickets authored by ptt-architect. Plan REVIEW_PASS confirmed in `docs/brain/B167/02-plan-review.md` (22/22 checks PASS).*
