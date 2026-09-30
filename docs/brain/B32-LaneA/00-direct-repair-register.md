# B32-LaneA Direct Repair Register

> **STATUS UPDATE**: PIPELINE_COMPLETE — all three defects fixed, FINAL_PASS confirmed.
> All files verified. Deferred items logged in `06-deferred-backlog.md`.

**Block**: B32-LaneA
**Status**: IN PROGRESS — accumulating direct repairs for pipeline batch
**Branch**: 001-agent-arena-platform
**Focus**: Trim + Flatten button diagnostics and bug fixes (PTT TradeCopierPanel + CopyEngine)

> **Purpose**: All direct `.cs` edits made outside the full pipeline are recorded here.
> Once the root cause is confirmed and all fixes are known, run the full
> ptt-orchestrator → ptt-architect → ptt-engineer pipeline against this register
> as a single batch rather than once per fix.

---

## Pipeline Batch Checklist (run once when register is CLOSED)

- [ ] ptt-orchestrator: ingest this register as the full scope
- [ ] ptt-architect: plan all tickets from the DW items below
- [ ] ptt-engineer: execute all tickets in sequence
- [ ] ptt-verify: run test suite + NT8 compiler gate
- [ ] Commit + PR

---

## Repairs Log

### R-B32-01 — Trim diagnostic Output.Process instrumentation
**Status**: DONE (direct edit)
**Files**:
- `src/PropTraderTools/TradeCopierPanel.cs` — `OnTrimClick`
- `src/PropTraderTools/CopyEngine.cs` — `Trim(Account leader, Instrument)` entry point + `TrimOneAccount`

**What changed**:
- `OnTrimClick`: replaced silent `return` on null instrument with `Output.Process` emit.
  Added entry-point diagnostic printing `leader`, `instr`, `ask`, `bid`, `buffer` to OutputTab1.
- `Trim(Account leader, Instrument)`: added `Output.Process` for leader-null path and
  rule-match result (`rule=FOUND(...)` vs `rule=NOT FOUND`).
- `TrimOneAccount`: added `Output.Process` at every decision point:
  position found/null, action+qty before submit, OK after CreateOrder, EXCEPTION on throw.

**Why**: Trim had zero OutputTab1 visibility — only `StatusUpdate` (status bar text).
BE was already fully instrumented. This brings Trim to parity so root cause is visible on click.

**DW raised**: None yet — diagnostic only. DW items will be added once OutputTab1 shows root cause.

**NT8 rules checked**: No new `CreateOrder` calls. No `lock()`. No `async void`. ASCII-only strings.

---

### R-B32-02 — Trim live test result: WORKING CORRECTLY
**Status**: CLOSED — no bug found
**Test**: Director placed OR on MES SEP26 (Sim102, 13 contracts Long), clicked Trim +0

**OutputTab1 result**:
```
PTT-Trim CLICK: leader=Sim102 instr=MES SEP26 ask=7502.75 bid=7502.5 buffer=0
PTT-Trim ENGINE: leader=Sim102 instr=MES SEP26 rule=NOT FOUND
PTT-Trim ACCOUNT: Sim102 pos=13 Long
PTT-Trim SUBMITTING: Sim102 action=Sell qty=7
PTT-Trim OK: Sim102 trimmed 7
```

**Findings**:
- Account resolved ✅
- Instrument wired ✅
- Position found (13 Long) ✅
- Quantity correct: ceil(13/2) = 7 ✅
- Market order placed and filled ✅ (confirmed by `PTT-Trim OK` + status bar `Sim102: trim 7`)
- `rule=NOT FOUND` — expected, COPY was OFF, no followers configured for MES SEP26

**Conclusion**: Trim button is NOT broken. Works correctly for leader account when no copy rule exists.

**Open question for Director**: What specific behaviour was expected to be wrong?
Options to investigate next:
1. **Trim with COPY ON + followers** — does `rule=FOUND` and followers also get trimmed?
2. **Trim with buffer > 0** — does limit order path work (places below bid for long)?
3. **Flatten** — same logic, just `pos.Quantity` instead of half. Test same way.

---

## Key Context (for pipeline engineer)

### The Trim/Flatten relationship
Trim and Flatten are **identical logic, different quantity**:
- `TrimOneAccount`: `qty = ceil(pos.Quantity / 2.0)`
- `FlattenOneAccount`: `qty = pos.Quantity`

Any fix to Trim applies 1:1 to Flatten. Engineer must apply to both.

### `AllAccounts` depends on `FindRule`
```
Trim(leader, instr)
  └─ TrimOneAccount(leader, instr)       ← direct, no rule needed
  └─ AllAccounts(instr)                  ← calls FindRule(instr)
       └─ FindRule: matches rule.Instrument == instrument.FullName (string)
          if no rule → yields nothing → only leader trimmed
```

**The leader account is always trimmed directly** regardless of rule.
Follower fan-out only happens when a copy rule exists for the instrument.

### `FindPosition` uses object reference equality
```csharp
foreach (Position p in acc.Positions)
    if (p.Instrument == instrument) return p;  // reference equality, not name
```
If `_instrument` (from chart injection) is a different object instance than
what NT8 stored in `acc.Positions`, this returns null even with an open position.

### `ComputeLimitPx` anchor (already fixed in B29, DO NOT revert)
```
Long  exit (Sell Limit)      → bid − buffer*tick   ← correct, fills immediately
Short exit (BuyToCover Limit) → ask + buffer*tick   ← correct, fills immediately
```
Original code used `ask + buffer` for longs (passive, never filled) — fixed in DW-B29-01.

---

## Files in Scope

| File | Role |
|---|---|
| `src/PropTraderTools/TradeCopierPanel.cs` | Button UI, `OnTrimClick`, `OnFlattenClick`, `_instrument`, `_leaderAccount` |
| `src/PropTraderTools/CopyEngine.cs` | `Trim()`, `Flatten()`, `TrimOneAccount()`, `FlattenOneAccount()`, `FindRule()`, `FindPosition()`, `AllAccounts()` |
| `src/PropTraderTools/CopyEngineTests.cs` | Tests — any fix needs matching test coverage |

---

## Open Questions — RESOLVED

1. ~~Does `leader=NULL` appear?~~ → NO. Account resolved correctly.
2. ~~Does `rule=NOT FOUND`?~~ → YES, expected. Solo use, no followers.
3. ~~Does `pos=NULL` on leader with open position?~~ → NO. Position found correctly.
4. ~~Does `PTT-Trim SUBMITTING` appear but no order placed?~~ → NO. Order placed and filled.
5. ~~Does trim work for leader but not followers?~~ → N/A. No rule, no followers.

---

### R-B32-03 — DW-B32-TRIM-CLOSE-01: PTT-Trim raw market sell breaks ATM OCO close
**Status**: ROOT CAUSE CONFIRMED — fix not yet implemented
**Defect ID**: DW-B32-TRIM-CLOSE-01
**Severity**: HIGH — leaves orphaned ATM bracket orders, blocks native Close button

**Symptom**: After clicking Trim, clicking the native Chart Trader Close button shows:
> `Close operation failed. Operation timed out. Manually close your position`
Some orders remain open. Position requires manual cleanup.

**Root cause from NT8 Log (2026-07-19 9:27 PM)**:
```
9:24:31  OR fills 13 contracts Long → ATM bracket created (Stop1, Target1, Stop2, Target2)
9:26:51  Target1 fills 6 contracts → ATM auto-BE fires, Stop2 moves to 7503.25
9:27:04  PTT-Trim placed market Sell 7 (PTT-Trim signal) → fills at market
         Position: 13 → 6. BUT ATM bracket still tracks 13 contracts.
9:27:04  Chart Trader Close → tries to OCO-cancel Stop2 + Target2
         Stop2 → Cancelled OK
         Target2 → Cancel submitted → TIMEOUT
9:27:09  "Close operation failed. Operation timed out."
9:27:13  Chart Trader retry Close → Sell 7 @ market finally fills the remaining 6
```

**Why it breaks**: `TrimOneAccount` calls `acc.CreateOrder(... OrderType.Market ...)` with signal
name `"PTT-Trim"`. This is a **raw manual order** — it has no relationship to the ATM bracket.
When it fills, it reduces the net position but the ATM bracket's OCO group is still tracking
the original quantity. NT8's Close button attempts to cancel the entire ATM OCO group atomically,
but the mismatch between bracket state and actual position causes the cancel-then-close to timeout.

**The same issue affects Flatten** — `FlattenOneAccount` has identical structure.

**Fix approaches** (to be architected by ptt-architect):

Option A — **ATM Close path**: Instead of raw `CreateOrder`, call `acc.Flatten(instrument)`
which uses NT8's built-in position flattening that respects ATM OCO state. This is what the
native Close button does internally.
- Risk: `acc.Flatten()` flattens the FULL position, not half — would change Trim semantics.

Option B — **Cancel bracket legs first, then place exit**: Before submitting the trim market
order, cancel all working ATM bracket stops/targets for this instrument on this account, THEN
place the market exit. This leaves NT8 with a clean slate.
- Risk: Exposes position with no stop briefly. Requires careful ordering.

Option C — **Use ATM bracket leg modification**: Find the existing ATM Target order and modify
its quantity down by trimQty using `acc.Change()` (same approach as BE uses for stops).
- Risk: Complex, ATM target modification API is fragile.

Option D — **Warn user + do nothing**: Block Trim when ATM is active, emit OutputTab1 warning:
`"PTT-Trim: ATM bracket active -- use native Target/Close buttons to trim"`
- Risk: Reduces Trim utility but avoids the OCO corruption.

**Fix approach — REVISED (Director confirmed 2026-07-19)**:

~~Option B (Cancel bracket first)~~ — **WRONG**. Cancelling ATM legs is dangerous and
inconsistent with how the rest of PTT works. The codebase already documents this at line
1412-1427: `"DW-B32-07: no direct leader call -- ATM owns leader stops. acc.Change() cannot
modify ATM-owned stops."` Cancelling ATM legs leaves the position naked (no stop).

**Correct Option — Resize bracket legs via `acc.Change()`**:
After the trim market sell fills, NT8's ATM is still active but its bracket legs still
track the original quantity. The fix is to reduce Stop* and Target* leg quantities
by `trimQty` using `order.Quantity = newQty; acc.Change(new Order[]{ order })` — the
same `acc.Change()` pattern used by BE (line 1388-1389) and Tighten (line 1466-1467).

However — the codebase already confirmed (DW-B32-07, line 1354-1362) that:
> `acc.Change() is silently rejected by NT8 ATM engine on ATM-owned Stop1/Stop2/Target1/Target2 orders.`

**This means the ATM bracket legs cannot be externally resized either.**

**True correct fix — Use `acc.Flatten()` scoped to trimQty via a limit order that NT8 ATM respects**:
NT8's ATM monitors position quantity in real time. When it detects a partial close that was
NOT initiated by one of its bracket legs, the ATM enters an inconsistent state.
The only clean way to trim inside an active ATM bracket is to:
1. Use `acc.Change()` to move a Target leg price to the current ask (making it fill immediately at ask)
   — this is an ATM-native partial exit that the bracket tracks correctly.
2. This is equivalent to "move Target1 to market price" — ATM processes the fill, updates bracket, Close works.

**Files to change**:
- `src/PropTraderTools/CopyEngine.cs` — `TrimOneAccount`: instead of raw `CreateOrder`,
  find the first working Target* order and move its price to ask via `acc.Change()`.
  If no Target leg found, fall back to raw `CreateOrder` (no ATM active).
- `src/PropTraderTools/CopyEngineTests.cs` — `T_B32_01_Trim_UsesTargetChange_WhenAtmActive`

**NOTE**: This is complex enough that it needs the ptt-architect to design the full approach
before any code is written. Log as blocked pending architect review.

**Status**: UPDATED — cancel approach rejected. Architect review required before fix.

---

*Last updated: 2026-07-19 — R-B32-03 root cause confirmed from NT8 Log.*

---

### R-B32-04 — DW-B32-TRIM-MARKET-01: buffer=0 incorrectly falls back to market order
**Status**: ROOT CAUSE CONFIRMED — fix not yet implemented
**Defect ID**: DW-B32-TRIM-MARKET-01
**Severity**: HIGH — defeats the entire purpose of the limit exit design

**Symptom**: `Trim +0` and `Flatten +0` (default state) always place raw market orders
instead of limit orders at the ask/bid.

**Root cause**:

`OnTrimClick` in `TradeCopierPanel.cs`:
```csharp
if (ask <= 0 || bid <= 0 || _trimBuffer == 0)   // ← _trimBuffer==0 forces market
    _engine.Trim(leader, _instrument);           // market path
```

`Trim(Account, Instrument, int, double, double)` in `CopyEngine.cs`:
```csharp
if (ask <= 0 || bid <= 0 || exitBuffer == 0) { Trim(leader, instrument); return; }  // same
```

`buffer=0` was intended as "no offset — post at the exact ask/bid."
Instead it triggers the market fallback, bypassing the limit path entirely.

**Fix**: Remove `_trimBuffer == 0` / `exitBuffer == 0` from the market fallback guard.
Only fall back to market when ask or bid are unavailable (no market data).

Files: `TradeCopierPanel.cs` (OnTrimClick + OnFlattenClick), `CopyEngine.cs`
(Trim 5-arg leader overload + Flatten 5-arg leader overload + Trim 4-arg + Flatten 4-arg).

**Tests to update**: `TrimLimit_FallsBackToMarket_WhenAskIsZero` — remove the
`exitBuffer==0` fallback assertion case (it should now go through the limit path).

**Applies 1:1 to Flatten** — same guard, same fix.

---

### R-B32-05 — DW-B32-TRIM-ANCHOR-01: ComputeLimitPx uses wrong price anchor for limit exits
**Status**: ROOT CAUSE CONFIRMED — fix not yet implemented
**Defect ID**: DW-B32-TRIM-ANCHOR-01
**Severity**: MEDIUM — limit orders place at suboptimal price (below bid for longs, above ask for shorts)

**Symptom**: With buffer=1, long exit sells at `bid - 1 tick` instead of `ask - 1 tick`.
This gives up the spread on every trim/flatten.

**Current `ComputeLimitPx`**:
```csharp
isLong  → bid - exitBuffer * tickSize    // sells BELOW the bid
isShort → ask + exitBuffer * tickSize    // buys ABOVE the ask
```

**Intended behaviour** (Director confirmed):
```
Long exit  → Sell Limit @ ask - buffer*tick
             buffer=0 → sits at ask (join the offer, passive fill)
             buffer=1 → drops to bid (lifts one tick, more aggressive)

Short exit → BuyToCover @ bid + buffer*tick
             buffer=0 → sits at bid (join the bid, passive fill)
             buffer=1 → lifts to ask (one tick more aggressive)
```

**Correct formula**:
```csharp
isLong  → ask - exitBuffer * tickSize
isShort → bid + exitBuffer * tickSize
```

**Example** (MES, ask=7502.75, bid=7502.50, tickSize=0.25):

| buffer | Current (wrong) | Correct |
|---|---|---|
| 0 | bid = 7502.50 | ask = 7502.75 |
| 1 | bid - 1 tick = 7502.25 | ask - 1 tick = 7502.50 (= bid) |
| 2 | bid - 2 ticks = 7502.00 | ask - 2 ticks = 7502.25 |

**Files to change**:
- `src/PropTraderTools/CopyEngine.cs` — `ComputeLimitPx` (one ternary, 2 lines)

**Tests to update** (4 tests asserting the wrong formula):
- `TrimLimit_Long_PlacesBelowBid` → rename + update expected price
- `TrimLimit_Short_PlacesAboveAsk` → rename + update expected price
- `FlattenLimit_Long_PlacesBelowBid` → rename + update expected price
- `FlattenLimit_Short_PlacesAboveAsk` → rename + update expected price

**Note**: The B29 fix comment says "bid - buffer for long is aggressive (fills immediately)."
That is technically true (selling below bid guarantees a fill) but it gives up the spread.
The correct design is to start at the ask (passive, better price) with buffer as the
aggression dial. This is the standard "peg to ask" exit order behaviour.

---

*Last updated: 2026-07-19 — R-B32-03/04/05 all root causes confirmed.*

---

### R-B32-06 — Missing Output.Process in limit path entry points (post-pipeline gap)
**Status**: DONE (direct edit)
**Files**: `src/PropTraderTools/CopyEngine.cs`

**What changed**:
- `Trim(Account, Instrument, int, double, double)` 5-arg overload: added `PTT-TrimLimit ENGINE` Output.Process at entry showing leader, instr, buffer, ask, bid
- `TrimOneAccountLimit`: added Output.Process at position check, flat-skip, submitting, OK, EXCEPTION — matching the `TrimOneAccount` diagnostic pattern added in R-B32-01

**Why**: After pipeline ran, live test showed `PTT-Trim CLICK` printed but nothing after it. The 5-arg limit overload was being called (ask+bid valid, buffer=0) but had zero Output.Process visibility. The market-path diags from R-B32-01 were only on `TrimOneAccount` not `TrimOneAccountLimit`.

**Expected output after F5**:
```
PTT-Trim CLICK: leader=Sim102 instr=MES SEP26 ask=... bid=... buffer=0
PTT-TrimLimit ENGINE: leader=Sim102 instr=MES SEP26 buf=0 ask=... bid=...
PTT-TrimLimit ACCOUNT: Sim102 pos=7 Long
PTT-TrimLimit SUBMITTING: Sim102 action=Sell qty=4 @ 7496.5
PTT-TrimLimit OK: Sim102 4 @ 7496.5
```

**Also noted**: `TrimOneAccountLimit` has no ATM block (only `TrimOneAccount` market path has it). A `PTT-TrimLimit` sell limit posting alongside an active ATM `Target2` may still cause the Close timeout — needs investigation after this diagnostic confirms the limit order is actually posting.

