## DW-NEW-08 -- ATM Bracket Missing After Fill/Cancel Race

**Source**: Live MGC DEC26 trade, PA-APEX-422136-04, 2026-09-03
**Discovered**: Post-BWAVE-DW live trading session
**Status**: OPEN -- repair design complete, awaiting wave assignment

---

### Observed Symptom

During a live MGC DEC26 trade on 2026-09-03, PA-APEX-422136-04 received a filled entry
but had ZERO bracket orders (no Stop, no Target). Every TP4-SFB log line for PA-04
showed `fo=NULL`. The position was protected by the PTT-Flatten safety net, which closed
it, but there was a window of unprotected exposure.

PA-APEX-422136-03 on the same trade found `fo=Stop1` correctly. Same code path, different
timing -- the race condition is non-deterministic.

---

### Root Cause

The leader's ATM strategy was repositioning its entry order repeatedly via drag (14+
cancelled Entry orders visible in follower order history before the winning fill).
Each drag cycle dispatched a new copy to followers, which was then cancelled when the
next drag happened.

The fill that landed on PA-04 arrived as a **PartFilled → CancelPending → Filled**
sequence: the cancel was already in-flight when the partial fill hit. The ATM bracket
arm fires on the Working→Filled transition, but the ATM strategy was already tearing
down due to the cancel signal. Result: entry Filled, ATM bracket arm incomplete, no
brackets placed.

**This is a pre-existing race condition made more frequent by B119 (reversal guard)**
because B119 increases the number of direction-flip entry cycles before a fill.
B119 itself is working correctly.

---

### Why MES Was Different (Same Session)

MES SEP26 on the same session did NOT produce a naked fill:
- MES used a 3-OCO ATM template (Stop1/2/3, Target1/2/3) -- more bracket surface area
- MES entry filled via a **ChangePending → ChangeSubmitted → Accepted → Working → Filled**
  path -- clean drag reposition, no cancel-in-flight at fill time
- MGC entry filled via **PartFilled → CancelPending → CancelSubmitted → Filled** -- cancel
  was already submitted when the partial fill arrived

Same code, different timing at the exchange level.

---

### Current Safety Net (Working But Slow)

PTT-Flatten (REAPER) fires when it detects a naked position. The detection interval
(`ReaperIntervalMs`) is 500–2000ms by default. On a fast instrument like MGC, 2 seconds
of unprotected exposure represents meaningful P&L risk. The safety net caught the 09-03
trade but the window exists.

---

### Repair Design (Best-of-5 Architecture Review, 2026-09-04)

Five repair options were designed and evaluated by independent subagent architects.
All five concluded no single option is sufficient. The recommended two-layer approach:

#### Layer 1 -- Option E: Accelerated Naked Detection (implement first)

**What it does**: Hooks the existing PTT-Flatten safety net into the `OnAccountOrderUpdate`
callback, which fires on every order state change. Instead of waiting for the REAPER
timer (up to 2s), the naked-position check runs within one order-update event (<50ms).

**New code**:
- `NakedPositionDetector(Account acct)` -- CYC=6, in new `V12_002.REAPER.NakedDetector.cs`
- `HasNakedPosition(Account acct)` -- CYC=4, checks for non-flat position with zero
  Working/PendingSubmit Stop or Target orders
- `_nakedDetectLastQueuedTicks` -- `ConcurrentDictionary<string, long>` debounce dict
- One tail-call added to `OnAccountOrderUpdate` for Filled/Cancelled/Rejected states

**Result**: Naked window shrinks from ~2000ms to ~50ms. Existing PTT-Flatten path unchanged.

**Risk**: Low. No new order types. No new bracket creation. Just faster detection.
`NakedPositionGraceSec` guard still prevents false fires during normal bracket lag.

#### Layer 2 -- Option D: Cancel-Before-Dispatch Drain (implement second)

**What it does**: Before dispatching a new entry copy to a follower, cancel ALL existing
Working/Accepted entry orders for that follower+instrument first. Parks the dispatch intent
in a `_pendingDispatchDrains` dictionary and waits for cancel acknowledgment before
submitting the new single entry. Ensures only one live entry exists per follower at a time,
eliminating the cancel/fill race at source.

**New code**:
- `PendingDispatchDrain` sealed class (fleet entry name, qty, price, action, acct, pending count)
- `_pendingDispatchDrains` -- `ConcurrentDictionary<string, PendingDispatchDrain>`
- `DrainThenDispatch(...)` -- CYC=4
- `OnDrainCancelAck(string acctKey)` -- CYC=3, decrements pending count, fires submit at zero
- `SubmitDrainedEntry(string acctKey)` -- CYC=3, removes payload, calls existing CreateOrder+Submit
- Modified `PropagateFollowerEntryReplace` -- +1 branch (absorb in-flight payload), stays <=8
- Modified `OnOrderUpdate` -- +1 branch (drain ack detection), stays <=8

**Result**: Eliminates the root cause. Multiple live entries per follower become impossible.
Small latency added (~50-200ms per dispatch) -- acceptable as leader already filled.

**Risk**: Medium. Adds dispatch latency. Drain timeout (2s watchdog) needed for stuck cancels.

#### Why NOT Option C (PTT places its own brackets)

Option C proposes PTT creating its own Stop/Target bracket orders immediately on fo=NULL.
Rejected: if the original ATM bracket arrives late while PTT's emergency brackets are
already live, the follower ends up with TWO stops and TWO targets. Untangling requires a
fragile cancellation handshake. All 5 architects flagged this as highest-risk.

---

### Acceptance Criteria

**Layer 1 (Option E)**:
- [ ] `NakedPositionDetector` fires within 50ms of a Filled/Cancelled/Rejected event on a naked follower
- [ ] No false fires during normal bracket confirmation lag (NakedPositionGraceSec guard active)
- [ ] Multi-follower isolation: PA-04 naked does not queue a flatten for PA-03
- [ ] No lock(), no async void, CYC <=8 on all new methods

**Layer 2 (Option D)**:
- [ ] Under 14+ drag-resubmit cycles, PA-04 ends every cycle with either flat or Entry:Filled + brackets
- [ ] Log shows [DRAIN] cancel-sent before [DRAIN-SUBMIT] on every cycle
- [ ] Drain timeout fires [DRAIN-TIMEOUT] after 2s if cancel unacknowledged
- [ ] No lock(), CYC <=8 on all new methods

---

### Wave Assignment

| Layer | Priority | Target Wave | Lane |
|-------|----------|-------------|------|
| Option E (accelerated detection) | P1 | BWAVE-NEXT | Lane A T4 (small, low risk) |
| Option D (cancel-before-dispatch) | P1 | BWAVE-NEXT | Lane B (dedicated lane, larger) |

---

### Files Affected

| File | Layer | Change Type |
|------|-------|-------------|
| `CopyEngine.cs` (or new `V12_002.REAPER.NakedDetector.cs`) | E | New methods: NakedPositionDetector, HasNakedPosition, GetNonFlatPosition |
| `OnAccountOrderUpdate` callback file | E | +1 tail-call branch |
| `V12_002.Orders.DispatchDrain.cs` (new file) | D | New: DrainThenDispatch, OnDrainCancelAck, SubmitDrainedEntry |
| `PropagateFollowerEntryReplace` | D | +1 absorb branch |
| `OnOrderUpdate` | D | +1 drain-ack branch |

---

### Director Notes (2026-09-04)

- B119 reversal guard confirmed working correctly -- not the source of this issue
- MES and MGC behaved differently due to fill timing, not code differences
- Current PTT-Flatten safety net DID catch the 09-03 MGC trade (no permanent naked position)
- The 2s window is the risk to close, not the overall protection model

*DW-NEW-08 backlog file created: 2026-09-04 | copier-spec mode*
