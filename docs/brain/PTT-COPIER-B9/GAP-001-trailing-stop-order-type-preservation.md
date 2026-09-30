# GAP-001 — Trailing Stop Order Type Preservation
**Raised**: 2026-07-09
**Status**: OPEN — product decision required before implementation
**Affects**: Mode 2 (Mirror), BE button, planned Tighten Stop button
**Priority**: P1 — must be resolved before Mode 2 is considered production-ready

---

## Problem Statement

Any operation that calls `acc.Change(new Order[] { order })` after writing
a new value to `order.StopPrice` has an **undefined interaction with trailing stop orders**.

Trailing stops in NT8 are `OrderType.StopMarket` orders with an active `TrailPrice`
offset maintained by the NT8 order management engine. When `acc.Change()` is called
with only `StopPrice` written (and `TrailPrice` left at whatever value it currently
holds), the outcome is **broker/simulation dependent**:

- **Most likely outcome**: NT8 treats the order as now having a fixed stop at the new
  `StopPrice`. The trailing watermark is lost. The stop will NOT trail again when
  price moves further in the trader's favour.
- **Alternative outcome**: NT8 re-arms the trail from the new price at the same
  `TrailPrice` offset. This is the "correct" behaviour from the trader's perspective
  but is not guaranteed.

Our code does **not** read, inspect, or preserve `TrailPrice` anywhere. The three
affected call sites are:

| Call site | File | Line | Operation |
|---|---|---|---|
| `HandleBracketChange` | `CopyEngine.cs` | ~405 | Mode 2: relay leader stop drag to followers |
| `MoveStopToBreakEven` | `CopyEngine.cs` | ~834 | BE button: move stop to entry ± buffer |
| `MirrorClose` | `CopyEngine.cs` (B9 new) | T3 new | Mirror: flatten follower on leader bracket fill |

---

## Affected Scenarios

### Scenario A — Mode 2 + follower has trailing stop

1. Follower ATM sets a 10-tick trailing stop after entry.
2. Price moves 15 ticks in favour. Trail has moved 5 ticks (stop now 5 ticks above entry).
3. Leader manually drags their (fixed) stop to break-even.
4. `HandleBracketChange` fires → follower's stop price set to leader's BE price.
5. **Gap**: follower's trail is now dead. If price continues in favour, the stop stays
   fixed at BE. It will not trail further.

### Scenario B — BE button + follower has trailing stop

1. User clicks BE button. `MoveStopToBreakEven` fires on all follower accounts.
2. Each follower's `StopMarket` working order is found and `StopPrice` is overwritten
   with `entry ± bufferTicks`.
3. **Gap**: same as Scenario A — trailing behaviour lost on the follower's stop.
4. Note: this affects the **master account too** if master has a trailing stop,
   since `BreakEven()` calls `MoveStopToBreakEven` on `AllAccounts(instrument)`.

### Scenario C — Tighten Stop button (planned, not yet built)

The planned "Tighten Stop" feature (move all stops to trail by N ticks, e.g. trail
by 4 ticks / 8 ticks) would call `acc.Change()` with a new `StopPrice` computed as
`currentPrice ± N ticks`. This has the same gap — it would convert any existing
trailing stops to fixed stops at the computed price.

Additionally, the Tighten Stop feature itself **is** a form of trailing — so the
question becomes: is Tighten Stop a one-shot "move stop to price X" or a live trailing
mechanism? If it's a live trail, it needs a background thread / `OnOrderUpdate` loop,
not a one-shot `acc.Change()` call.

---

## Product Decision Required

Three distinct policy questions must be answered before coding:

### Q1 — Mode 2 stop relay: what should happen to a follower trailing stop?

| Option | Behaviour | Implementation |
|---|---|---|
| **A — Freeze it** | Convert trailing stop to fixed stop at relayed price. Simple, predictable, explicit. | Current behaviour (accidental). Already works. |
| **B — Skip it** | If follower's stop is a trailing stop, do NOT relay the leader's stop drag. Let the trail manage itself. | Add `if (fo.TrailPrice > 0) continue;` guard in `HandleBracketChange`. ~1 line. |
| **C — Re-arm it** | Set new `StopPrice` AND new `TrailPrice` offset on the follower's stop before calling `acc.Change()`. Trail resumes from new price at same offset distance. | Read `fo.TrailPrice`, preserve it, set both fields. ~5 lines. NT8 broker compliance uncertain — needs Sim101 verification. |

**Recommendation**: **Option B** (skip trailing stops in Mode 2 relay) is the safest default. A trailing stop is already "self-managing" — the follower's ATM is handling it. Only relay to fixed stops. Add a spec note: "Mode 2 does not override follower trailing stops." Option C is the right long-term answer but needs NT8 broker testing first.

### Q2 — BE button: should it cancel a trailing stop or skip it?

| Option | Behaviour |
|---|---|
| **A — Freeze it** | Convert trailing stop to fixed stop at BE price. Current accidental behaviour. |
| **B — Cancel + replace** | Cancel the trailing stop order. Submit a new `StopMarket` fixed stop at BE price. Guarantees the trailing behaviour is gone and BE is clean. |
| **C — Skip trailing stops** | If the account's stop is a trailing stop, skip it (don't move to BE). The trail may or may not already be above entry — ambiguous UX. |

**Recommendation**: **Option B** (cancel + replace) for BE. The user's intent when pressing BE is unambiguous — "get me to break-even right now." Leaving a trailing stop below entry would be wrong. Sending a fixed-price `acc.Change()` may silently fail or leave trail behaviour undefined. The safest implementation: `acc.Cancel(trailingStopOrder)` then `acc.CreateOrder(... StopMarket, BE price ...)`.

### Q3 — Tighten Stop: one-shot or live trail?

| Interpretation | Meaning | Implementation |
|---|---|---|
| **One-shot** | "Move all stops to currentPrice - N ticks right now." Single `acc.Change()` per account. Stop does not move again until user presses again. | Simple. Same `acc.Change()` pattern as BE. Same trailing-stop gap as Q1/Q2. |
| **Live trail** | "From now on, trail all stops N ticks behind price." Stop moves automatically as price improves. Never gives back more than N ticks. | Requires `OnOrderUpdate` / price tick listener. Cannot use one-shot `acc.Change()`. Significantly more complex. Must be a persistent mode that can be toggled off. |

**Recommendation**: Spec "Tighten Stop" as **one-shot first** (P2 — simpler, delivers value immediately), with a note that live trailing is a P1 follow-on once the one-shot behaviour is validated. The one-shot version is a single new button + same `acc.Change()` pattern as BE.

---

## What "TrailPrice" actually means in NT8

In NT8, a working `StopMarket` order placed by an ATM trailing stop template has:
- `order.StopPrice` — the current absolute stop price (e.g. 4500.00)
- `order.TrailPrice` — the trailing offset in ticks (e.g. 8 = 8 ticks)

When NT8's internal trailing logic fires (on each tick), it calls the equivalent of
`acc.Change()` internally, updating `StopPrice` while keeping `TrailPrice` constant.

When **we** call `acc.Change()` and only set `StopPrice`, we are doing what the NT8
trailing engine does — but without updating the watermark it tracks against. Whether
NT8 treats this as "continue trailing from new position" or "fixed stop, trail off"
is what needs to be verified on Sim101.

---

## Required Verification (before any implementation)

Before choosing an option for any of the three questions above, run this test on Sim101:

1. Enter a trade with a trailing stop ATM (e.g. trail = 8 ticks).
2. Let price move 10 ticks in favour (trail should have moved 2 ticks).
3. Programmatically call `acc.Change()` on the trailing stop order, setting
   `StopPrice` to a new value (e.g. move it 2 ticks closer to current price).
4. Observe: does the stop continue to trail? Or does it freeze at the new price?

Log `order.TrailPrice` before and after the call to confirm whether NT8 clears it.

---

## Deferred Backlog Entries Generated

See `docs/brain/PTT-COPIER-B9/06-deferred-backlog.md` (to be appended at B9 FINAL_PASS):

| ID | Item | Priority |
|---|---|---|
| DW-B9-GAP-001a | Trailing stop preservation in Mode 2 `HandleBracketChange` — choose Option A/B/C and implement | P1 |
| DW-B9-GAP-001b | BE button trailing stop handling — implement cancel+replace (Option B) | P1 |
| DW-B9-GAP-001c | Tighten Stop button — one-shot: move all stops to currentPrice - N ticks (configurable per instrument). Same trailing-stop caveat as GAP-001a applies. | P2 |
| DW-B9-GAP-001d | Sim101 verification test: does `acc.Change(StopPrice)` on a trailing stop order preserve or kill the trail? Document result before any GAP-001a/b/c implementation. | P1 (prerequisite for a/b/c) |

---

## Tighten Stop — Functional Spec (one-shot, P2)

Even though implementation is deferred, capturing the spec here so the architect
has it when B10 scope is decided:

**User intent**: "Tighten all my stops right now to trail by N ticks behind current price."

**One-shot behaviour**:
- For each account in the rule (master + all followers):
  - Find the working `StopMarket` stop leg order
  - Compute new stop = `currentBid - N*tickSize` (short) or `currentAsk + N*tickSize` (long)
    where N is the configurable tighten-ticks value (default 4 ticks for MES = 1 point)
  - Call `acc.Change()` with the new `StopPrice`
- UI: new button `[Tighten N]` on Panel and Window alongside BE button
- Config: `TightenTicks` int field on `CopyRule`, defaulting to 4
- XML persistence: add `TightenTicks` to `CopyRuleDto`
- Per-follower or global: global (same N for all accounts in the rule, same as BE buffer)

**Live trail behaviour** (P1 follow-on, not in one-shot scope):
- Toggle button `[Trail ON/OFF]`
- When ON: subscribe to `Account.OrderUpdate` on all accounts, monitor price, call
  `acc.Change()` each time price improves by 1 tick beyond trail watermark
- When OFF: cancel subscription
- Requires watermark tracking per account per instrument — significantly more state

---

*This gap document feeds directly into B10 architect scope. Do not start GAP-001a/b/c*
*without completing the Sim101 verification (GAP-001d) first.*

---

## Sim101 Verification Test — GAP-001d (REQUIRED before B10 implementation)

**Status**: PENDING — must be run manually in NinjaTrader against Sim101 before B10 starts.
**Blocks**: DW-B9-GAP-001a, DW-B9-GAP-001b, DW-B9-GAP-001c, DW-B10-GAP-002b

### Purpose

Determine whether `acc.Change(new Order[] { order })` after writing `order.StopPrice`
on a **working trailing stop** order preserves or destroys the trailing watermark.

The answer drives option selection for three B10 features:
- GAP-001a: Mode 2 `HandleBracketChange` policy (Option A / B / C)
- GAP-001b: `MoveStopToBreakEven` trailing stop path (Option A / B)
- GAP-002b: `MoveStopToBreakEven` cancel+replace implementation

### How to run

Add a temporary test method to any Strategy or the `TradeCopierAddOn` (accessible via a
hidden button or keyboard shortcut). Run against Sim101 on an active ES/MES chart.

```csharp
// ── STEP 1 ──────────────────────────────────────────────────────────────────
// Enter a Sim101 long position using a TRAILING STOP ATM template.
// Use trail = 8 ticks. Instrument: MES or ES.
// (Enter manually via Chart Trader or ATM before calling the test method.)

// ── STEP 2 ──────────────────────────────────────────────────────────────────
// Let price move 10 ticks in favour so the trailing stop has ratcheted.
// Expected at this point:
//   order.StopPrice  ≈ entry + 2 ticks  (stop ratcheted 2 ticks above entry)
//   order.TrailPrice ≈ 8 ticks worth of price units (e.g. 2.00 pts for ES, 0.50 pts for MES)

// ── STEP 3 ──────────────────────────────────────────────────────────────────
// Locate the working trailing stop order (OrderType.StopMarket with TrailPrice > 0).
// Call this method from a button handler or OnKeyDown:

private void RunGap001dTest(Account acc, Instrument instr)
{
    // Find the working trailing stop
    var order = acc.Orders
        .FirstOrDefault(o => o.OrderState == OrderState.Working
                          && o.OrderType  == OrderType.StopMarket
                          && o.TrailPrice > 0);

    if (order == null)
    {
        Print("GAP-001d: no working trailing stop found -- enter position with ATM trailing stop first");
        return;
    }

    // Log BEFORE
    double trailBefore = order.TrailPrice;
    double stopBefore  = order.StopPrice;
    Print($"GAP-001d BEFORE: StopPrice={stopBefore}  TrailPrice={trailBefore}");

    // Move stop 2 ticks closer to current price via acc.Change()
    double tickSize    = instr.MasterInstrument.TickSize;
    order.StopPrice    = stopBefore + (2.0 * tickSize);   // move 2 ticks toward price (tighten)
    acc.Change(new Order[] { order });

    // Log AFTER (allow 0.5s for NT8 order engine to process)
    System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
        Dispatcher.InvokeAsync(() =>
        {
            // Re-fetch: the Order object may have been replaced by NT8
            var updated = acc.Orders
                .FirstOrDefault(o => o.OrderState == OrderState.Working
                                  && o.OrderType  == OrderType.StopMarket);

            if (updated == null)
            {
                Print("GAP-001d AFTER: order no longer working (filled or cancelled?)");
                return;
            }
            Print($"GAP-001d AFTER:  StopPrice={updated.StopPrice}  TrailPrice={updated.TrailPrice}");

            // ── STEP 4 ──────────────────────────────────────────────────────────
            // Now let price move another 5 ticks in favour.
            // Watch NT8 Output window:
            //   If StopPrice moves with price → trail is ALIVE   (Option C viable)
            //   If StopPrice is frozen         → trail is DEAD   (use Option B: skip / cancel+replace)
            Print("GAP-001d: now let price move 5+ ticks in favour and observe stop behaviour");
        })
    );
}
```

### Observations to record

After running the test, fill in the table below before opening the B10 architect session:

| Field | Observed value |
|-------|---------------|
| `order.TrailPrice` BEFORE `acc.Change()` | *(fill in)* |
| `order.TrailPrice` AFTER `acc.Change()` | *(fill in)* |
| `order.StopPrice` moved to expected value? | YES / NO |
| Did stop continue to trail after `acc.Change()`? | YES / NO |
| TrailPrice cleared to 0 after Change? | YES / NO |
| Additional NT8 error/exception observed? | *(fill in)* |

### Decision matrix

| Observed result | Option for GAP-001a (Mode 2) | Option for GAP-001b (BE) |
|-----------------|------------------------------|--------------------------|
| Trail is **DEAD** after `acc.Change()` (stop froze) | **Option B** — skip trailing stops in Mode 2 relay | **Option B** — cancel+replace with fixed StopMarket |
| Trail is **ALIVE** after `acc.Change()` (stop kept trailing) | **Option C** — re-arm allowed; set both StopPrice + TrailPrice | **Option A** — acc.Change() is safe, current path OK |
| NT8 throws exception on trailing stop Change | **Option B** — skip trailing stops in Mode 2 relay | **Option B** — cancel+replace mandatory |

**Default recommendation before test result is known**: implement Option B for both
GAP-001a and GAP-001b (safest, no ambiguity). Upgrade to Option C only if Sim101
confirms trail is alive after `acc.Change()`.

### Record result here

```
DATE: _______________
INSTRUMENT: _______________
NT8 VERSION: _______________

RESULT: [ ] Trail ALIVE after acc.Change()
         [ ] Trail DEAD after acc.Change()  (stop froze)
         [ ] Exception thrown
         [ ] Other: _______________

TrailPrice BEFORE: _______________
TrailPrice AFTER:  _______________

DECISION:
  GAP-001a option selected: [ ] A  [ ] B  [ ] C
  GAP-001b option selected: [ ] A  [ ] B
  GAP-002b cancel+replace:  [ ] Required  [ ] Not required

NOTES:
_______________
```

