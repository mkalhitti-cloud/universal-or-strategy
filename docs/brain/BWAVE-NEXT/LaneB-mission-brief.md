# BWAVE-NEXT Lane B -- Mission Brief

**Lane**: B -- Cancel-Before-Dispatch Drain + Post-PR-42 Repairs
**Status**: SPEC_READY -- awaiting wave launch
**Source decisions**: Director session 2026-09-04 (DW-NEW-08 design) + post-PR-42 bot review 2026-09-04
**Brain dir**: `docs/brain/BWAVE-NEXT/LaneB/`

---

## Director Decisions Recorded

| Decision | Choice | Date |
|----------|--------|------|
| DW-NEW-08 Layer 2: which option | **Option D -- cancel-before-dispatch drain** | 2026-09-04 |
| DW-NEXT-A-07 + DW-NEXT-A-06: when to fix | **Bundle into Lane B T1 -- same file, same session** | 2026-09-04 |
| DW-NEXT-A-06 fix method | **(long)(int)Environment.TickCount explicit cast sequence** | 2026-09-04 |
| DW-NEXT-A-07 resolution | **Check NT8 docs first. If acc.Orders is NOT thread-safe during OnOrderUpdate: add .ToList() inside ActiveOrders body. CYC stays 1.** | 2026-09-04 |

---

## Live Trading Observations (Already Recorded -- Do Not Re-Investigate)

Source: DW-NEW-08-naked-fill-race.md Director Notes (2026-09-04) + DW-NEW-07-global-be-cleanup.md.

Key facts already confirmed:
- PA-APEX-422136-04 received MGC DEC26 fill with zero brackets on 2026-09-03.
- Root cause: PartFilled -> CancelPending -> Filled race during 14+ drag-reposition cycles.
- B119 reversal guard is working correctly. Not the source of this issue.
- PTT-Flatten safety net caught the trade (no permanent exposure).
- ~2s naked window is the risk to close. Layer 1 (T4, PR #42) shrinks it to ~50ms. Layer 2 (this lane) eliminates root cause.

---

## Scope -- 3 Tickets

| Ticket | DW Item | File | Type | Notes |
|--------|---------|------|------|-------|
| **T1** | DW-NEXT-A-07 + DW-NEXT-A-06 | `CopyEngine.cs` | Small repairs | Bundle: thread-safety fix + TickCount cast fix |
| **T2** | DW-NEW-08 Option D | `CopyEngine.cs` | New production code | Cancel-before-dispatch drain (larger) |
| **T3** | DW-NEXT-A-02 + DW-NEXT-A-01 | docs only | Housekeeping | Sync verbatim record + SIM gate calibration note |

T1 and T2 touch different line ranges in CopyEngine.cs and are independent.
T1 should complete and VERIFY_PASS before T2 begins (T2 touches OnOrderUpdate which T1 may also touch).
T3 is documentation-only, no pipeline needed -- Director action.

---

## Ticket T1 -- DW-NEXT-A-07 + DW-NEXT-A-06: Two Post-PR-42 Repairs

### Sub-item A: DW-NEXT-A-07 -- ActiveOrders Thread Safety

**Finding source**: PR #42 bot review (Greptile, cubic, CodeRabbit).

**The question**: `ActiveOrders` (T5, PR #42) uses lazy LINQ `Where()` over `acc.Orders`.
The pre-existing code used `.ToList()` which takes a snapshot. NT8's `acc.Orders` collection --
if NT8 can mutate it from a background thread while `OnOrderUpdate` is executing on the
Market Data thread, then lazy enumeration risks `InvalidOperationException`.

**MANDATORY Ph1 step**: Before designing anything, read:
- `docs/standards/NT8_FULL_REFERENCE.md` (grep for `Orders`, `ICollection`, thread safety)
- `docs/standards/NT8_ADDON_KNOWLEDGE.md` (confirm AddOn callback threading model)

**If NT8 confirms acc.Orders is safe for enumeration during OnOrderUpdate callbacks**:
- No production code change needed.
- T1 Sub-A is verification-only.
- Write 1 [Fact] confirming ActiveOrders enumerates correctly (structural guard).
- Record finding in ticket-1-completion.md.

**If NT8 docs are ambiguous or confirm it is NOT safe**:
- Add `.ToList()` inside the ActiveOrders body:
  ```csharp
  private static IEnumerable<Order> ActiveOrders(Account acc) =>
      acc.Orders.Where(static o =>
          o.OrderState != OrderState.Filled
          && o.OrderState != OrderState.Cancelled
          && o.OrderState != OrderState.Rejected).ToList();
  ```
- CYC stays 1. The filter is preserved. The change is 1 character (`.ToList()`).
- Return type stays `IEnumerable<Order>` (no callers change).
- Note: adding `.ToList()` means the return is now `List<T>` but exposed as `IEnumerable<T>` -- no allocation concern for the 2 call sites (each called once per OnOrderUpdate).

### Sub-item B: DW-NEXT-A-06 -- TickCount Wraparound

**Finding source**: PR #42 bot review (Greptile confirmed, CodeRabbit confirmed).

**Bug**: In `TryNakedDetect` and/or `NakedPositionDetector`, `Environment.TickCount` (int32) is
stored into `_nakedDetectLastQueuedTicks` (ConcurrentDictionary<string, long>). After ~24.9 days
uptime, TickCount wraps negative. `now - last` becomes large negative. Condition `>= GraceMs`
becomes false -- detection is suppressed for the remainder of the old timestamp range.

**Fix**: Change the TickCount read to use explicit cast sequence to preserve sign extension:
```csharp
long now = (long)(int)Environment.TickCount;
```
This ensures the int32 value is sign-extended to int64 correctly, not zero-extended.

Both `TryNakedDetect` and anywhere else that reads TickCount into a long for this debounce
must be changed. Confirm exact lines in CopyEngine.cs before editing (lines ~6403-6445 per T4).

### Acceptance Criteria (T1)

- [ ] NT8 thread-safety determination documented in ticket-1-completion.md (one of: SAFE-CONFIRMED / AMBIGUOUS-ADDED-TOLIST / UNSAFE-ADDED-TOLIST)
- [ ] If .ToList() added: `dotnet build` 0 errors, NT8 sync 18/18 OK
- [ ] `(long)(int)Environment.TickCount` cast applied wherever TickCount feeds the long debounce dict
- [ ] No new lock(), no async void, no return null, ASCII-only
- [ ] CYC of all modified methods unchanged or <=8
- [ ] 1 [Fact] test: `ActiveOrders_EnumerationIsSafe()` or `NakedDetect_DebounceUsesSignedLongCast()`
- [ ] NT8 sync 18/18 OK (if any .cs changes)

### Test Names (T1)
```
[Fact] ActiveOrders_ThreadSafetyVerification()
[Fact] NakedDetector_DebounceField_UsesLongArithmetic()
```

---

## Ticket T2 -- DW-NEW-08 Option D: Cancel-Before-Dispatch Drain

**Full spec**: `docs/brain/BWAVE-DW/Backlog/DW-NEW-08-naked-fill-race.md` (Layer 2 section).
Read the entire spec before Ph1. The design is complete -- do not re-design, implement it.

### New Code Required

```
sealed class PendingDispatchDrain  (or record -- CYC=0/1)
  Fields: FollowerAcctKey(string), Instrument, Qty, Price, OrderAction,
          PendingCancelCount(int -- Interlocked), TimestampTicks(long)

ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains
  Key = follower account name. readonly. StringComparer.Ordinal.

private void DrainThenDispatch(Account follower, Instrument instrument, ...)
  CYC target: 4. Cancel all Working/Accepted entry orders for follower+instrument.
  Store PendingDispatchDrain payload. Increment pending count per cancel submitted.
  If no Working entries found: submit directly (no drain needed path, CYC +=1).

private void OnDrainCancelAck(object sender, OrderEventArgs e)
  CYC target: 3. Check if cancel ack belongs to a drain (key = acct.Name).
  Decrement pending count via Interlocked.Decrement.
  If count reaches zero: call SubmitDrainedEntry.

private void SubmitDrainedEntry(string acctKey)
  CYC target: 3. Remove payload from dict.
  Call existing Account.CreateOrder() + Submit() pattern.
  Log [DRAIN-SUBMIT].

Drain watchdog (2s timeout):
  Check _pendingDispatchDrains for entries older than 2s (TickCount-based).
  If found: log [DRAIN-TIMEOUT], remove entry, do NOT submit (position may have changed).
  Watchdog fires in OnOrderUpdate (piggybacked, cheap check -- do not add a System.Threading.Timer).
```

### Modified Existing Methods

```
PropagateFollowerEntryReplace (existing):
  +1 branch: if Working entry exists for this follower+instrument, call DrainThenDispatch
  instead of direct CreateOrder+Submit. CYC must stay <=8.

OnOrderUpdate (existing):
  +1 branch: check _pendingDispatchDrains before processing. Route terminal order events
  for drain-pending accounts through OnDrainCancelAck. CYC must stay <=8.
```

### NT8 API Constraints (non-negotiable)

- NO Account.Change() -- silent no-op on ATM-owned orders.
- NO AtmStrategyCreate() -- StrategyBase-only, banned in AddOnBase.
- NO AtmStrategyChangeStopTarget() -- StrategyBase-only, banned in AddOnBase.
- Cancel pattern: Account.Cancel(order) -- AddOnBase available. Confirmed.
- Submit pattern: Account.CreateOrder() + Submit() -- AddOnBase available. Confirmed.
- NO lock() anywhere. Use ConcurrentDictionary + Interlocked for shared state.

### Acceptance Criteria (T2)

- [ ] Under 14+ drag-resubmit cycles, follower ends every cycle with either flat OR Entry:Filled + brackets
- [ ] Log shows `[DRAIN]` cancel-sent before `[DRAIN-SUBMIT]` on every dispatch
- [ ] Log shows `[DRAIN-TIMEOUT]` after 2s if cancel unacknowledged
- [ ] `DrainThenDispatch` CYC <=4, `OnDrainCancelAck` CYC <=3, `SubmitDrainedEntry` CYC <=3
- [ ] `PropagateFollowerEntryReplace` CYC stays <=8 after +1 branch
- [ ] `OnOrderUpdate` CYC stays <=8 after +1 branch
- [ ] No lock(), no Account.Change(), no AtmStrategyCreate(), no AtmStrategyChangeStopTarget()
- [ ] NT8 sync 18/18 OK
- [ ] `dotnet build` 0 errors
- [ ] 3 [Fact] tests pass (see below)
- [ ] SIM gate: deferred (live NT8 required). Non-blocking for VERIFY_PASS.

### Test Names (T2)
```
[Fact] DrainThenDispatch_CancelsExistingEntryBeforeSubmit()
[Fact] OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero()
[Fact] DrainWatchdog_ClearsStuckDrain_AfterTimeout()
```

---

## Ticket T3 -- Documentation Housekeeping (Director Action, No Pipeline)

### DW-NEXT-A-02: Record T3 NT8 Sync Verbatim Output

The `ticket-3-completion.md` from BWAVE-NEXT Lane A documents expected ptt-sync-and-verify.ps1
format but omits the actual verbatim run output. Protocol requires verbatim recording.

Director action: Re-run `powershell -File scripts\ptt-sync-and-verify.ps1` against current main
and append the verbatim output as a note at the bottom of:
`docs/brain/BWAVE-NEXT/LaneA/ticket-3-completion.md`

### DW-NEXT-A-01: GraceMs Calibration Note

After the first SIM or live session with NakedPositionDetector active (T4 from PR #42),
record GraceMs calibration result in `docs/brain/BWAVE-NEXT/LaneA/ticket-4-completion.md`:
- Were any `[NAKED-DETECT]` log lines observed?
- Any false fires during normal fill+bracket-arm sequence?
- Recommended: keep 500ms / increase to 750ms / decrease to 250ms?

This is observation-only. No code change unless calibration indicates adjustment needed.
If adjustment needed: record DW-NEXT-A-01B as a micro-ticket for Lane B T1 bundle.

---

## Out of Scope (explicitly excluded from this lane)

| ID | Reason |
|----|--------|
| DW-NEXT-A-03 (short position detection) | No shorts in current operational pattern. Future backlog. |
| DW-NEXT-A-04 (multi-instrument cross-contamination) | Single-instrument use only. Future backlog. |
| DW-NEXT-A-05 (entry orders misclassified as protective) | Edge case within 500ms grace window. Future backlog. |
| DW-RepairLC-01/02 (SIM gates) | Director action, live NT8 required. |
| DW-C39-09-LaneA (SaveRules) | TradeCopierWindow.cs scope -- separate lane. |
| All NEW-0x test quality gaps | Separate lane. |

---

## Files in Scope

| File | Tickets | Change Type |
|------|---------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | T1, T2 | Production -- requires NT8 sync + F5 |
| `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` OR new test file | T1, T2 | Test additions |

---

## Jane Street Compliance Checklist (all tickets)

| Rule | Requirement |
|------|------------|
| JS-021 | No `lock()` anywhere in new or modified code |
| JS-033 | No `async void` (non-event-handler) |
| JS-002 | No `return null` in new code |
| JS-001 | No `throw new XxxException` in hot paths |
| CYC <=8 | All new and modified methods <=8 |
| ASCII-only | No Unicode, emoji, curly quotes |
| xUnit-only | `[Fact]`, `Assert.*` -- no NUnit, no MSTest |
| NT8 banned | No Account.Change(), AtmStrategyCreate(), AtmStrategyChangeStopTarget() |

---

## Post-Implementation Gates (T1 + T2)

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1  # must show 18/18 OK, 0 MISMATCH
# Then: F5 in NinjaTrader 8 -- 0 new errors
dotnet test src/PropTraderTools --filter "T1-specific-tests"
dotnet test src/PropTraderTools --filter "DrainThenDispatch|OnDrainCancelAck|DrainWatchdog"
```

---

*Spec written: 2026-09-04 | copier-spec mode | Director decisions recorded above*
*Post-PR-42 bot findings DW-NEXT-A-06 + DW-NEXT-A-07 confirmed real and bundled into T1*
