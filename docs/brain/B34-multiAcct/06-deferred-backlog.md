# B34 Deferred Backlog — 06-deferred-backlog.md
<!-- PTT-COPIER B34 | be-multiAccount-fixes | ptt-plan-reviewer | 2026-07-27 -->

**Block:** B34 — be-multiAccount-fixes
**Pipeline stage:** Phase 5 Final Review — FINAL_PASS
**Prior blocks contributing:** B32, B33-Modular, B35-LaneB

---

## 1. Items CLOSED by B34

The following deferred-work items from prior blocks are formally closed by B34:

| ID | Source Block | Description | Resolution |
|----|-------------|-------------|------------|
| DW-B33-05 | B33-Modular | `isLong` derived from `leaderPos` OUTSIDE foreach — all followers got leader's direction | **CLOSED** B34-01: `isLong = pos.MarketPosition == MarketPosition.Long` inside loop per `pos` |
| DW-B33-06 | B33-Modular | `bePrice = leaderPos.AveragePrice` — no per-account price, no sign flip, no buffer | **CLOSED** B34-01: `bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize` per-account |
| DW-B33-07 | B33-Modular | `CancelStaleBracketsLocal` called once pre-loop for leader only | **CLOSED** B34-01: `CancelStaleBracketsLocal(acc, ctx.Instrument)` inside loop per-account |
| DW-B33-02 | B33-Modular | Buffer tick values (`BeBuffer`, `TrimBuffer`, `FlatBuffer`) absent from `IPttHostContext` | **CLOSED** B34-02: 5 new props added to interface + implemented in `TradeCopierPanel` |
| DW-B33-04 | B33-Modular | `PttTrim`/`PttFlatten` issue `OrderType.Market` unconditionally, buffer ignored | **CLOSED** B34-03: Limit order path when `buffer > 0`; Market path when `buffer == 0` |

---

## 2. New Deferred Items FROM B34

Items identified during B34 implementation but out of B34 scope. These require future block work.

| ID | Description | Priority | Target Block | Status |
|----|-------------|----------|--------------|--------|
| DW-B34-01 | `PttBus.RaiseBe` post-loop notification carries leader values only. In a mixed-direction portfolio where the leader is long and a follower is short, the event reports the long-side `bePrice` and `isLong=true` for all listeners. This is incorrect for any downstream consumer that acts on the event per-account. Fix requires a per-account event model or a multi-entry `BeEventArgs`. | P2 | B36 or future | OPEN |
| DW-B34-02 | Trim operates on the leader account only (`ctx.LeaderAccount`). Follower trim copy is handled by `PttCopier` relay. Verify the relay also passes `ask`/`bid` (added in B34) to any follower-side copy path. If the relay does not forward the buffer parameters, follower trim orders will default to Market regardless of buffer setting. | P2 | B35 relay audit | OPEN |

---

## 3. Items Carried Forward (Still OPEN from Prior Blocks)

### From B32

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B32-TRIM-ANCHOR-01 | `ComputeLimitPx` wrong price anchor — ask/bid peg uses the wrong side depending on direction. When long, trim sell limit should anchor to ask (not bid); when short, buy-to-cover limit should anchor to bid (not ask). Current implementation may swap the anchor. Architectural fix required. | P1 | OPEN |
| DW-B32-TRIM-MARKET-01 | `buffer=0` forces market fallback in the limit path — limit path degrades silently to market order when buffer equals zero. This hides the limit path from buffer=0 users. Fix: separate the "use limit" flag from the buffer magnitude; `buffer=0` can mean "limit at market price" (aggressive limit) rather than "use market order". | P1 | OPEN |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection: `TrimOneAccountLimit` and `FlattenOneAccountLimit` lack the `IsAtmBracketActive` guard that was added for the cancel path in DW-B32-07. Without this guard, a limit-close order placed on an ATM-managed account may conflict with the ATM engine's bracket slot management. Director approval required before adding the guard (risk of over-restraint). | P2 | OPEN |
| R-B32-03 / DW-B32-TRIM-CLOSE-01 | Trim ATM OCO bracket corruption on market exit path. When the market fills a trim order while an ATM stop/target bracket is active, the OCO group may not be properly cancelled, leaving orphan bracket orders. The `IsAtmBracketActive` guard pattern (from DW-B32-07) is the proposed fix. Architect review needed. | P1 | OPEN |

### From B33-Modular

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B33-01 | `dotnet test` NT8 Indicator base class gap. `AtrSizingEngine.cs` extends NT8's `Indicator` base class, which is not resolvable outside NT8's hosted Roslyn process. This causes 2 pre-existing CS0234/CS0246 errors on every `dotnet build` outside NT8. All tests must be verified via grep `[Fact]` count and NT8 F5 compilation. CI `dotnet test` pipeline remains blocked until a stub assembly is created. | LOW | OPEN |
| DW-B33-03 | `ArmPendingBe` armed path still calls `_engine.ArmPendingBe` and `_engine.DisarmPendingBe` directly. Only the Idle-immediate-fire path was modularized via `DispatchModule("BE")` in B33. If `PttBeArmed` is to be modularized, a `PttBeArmed` module with `Arm`/`Disarm` state is required. No functional impact for current behavior. | LOW | OPEN |

### From B34/B35 Handoff (Unresolved Observational Items)

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| U1 | NT8 `Account.CreateOrder` arg8 OCO group ID effectiveness on sim. The `arg8` OCO group ID passed to `CreateOrder` in `SubmitBeStopLocal`, `TrimPositionLocal`, and `FlattenPositionLocal` has not been verified to cause correct OCO linking in NinjaTrader sim. `CancelStaleBrackets(cancelPttBe:true)` cleans up on flat regardless, but the OCO linking may still be silently ignored by NT8. Requires a sim test session. | LOW | OPEN |
| U3 | Confirm Limit order `arg6=limitPrice, arg7=0` is correct in live NT8 sim (new `PttTrim`/`PttFlatten` Limit path added in B34-03). A swapped `arg6`/`arg7` would result in a stop-market order instead of a limit order, visible as wrong fill price in the Active Orders grid. Verify via a sim test session. | MEDIUM | OPEN |

---

## 4. B35 Candidates (Recommended Next-Block Work)

The following items are recommended for B35 planning, ordered by priority:

| ID | Description | Priority | Effort |
|----|-------------|----------|--------|
| U3 | Sim test: verify Limit order arg6/arg7 correctness for new PttTrim/PttFlatten Limit path | MEDIUM | 1 session |
| DW-B34-02 | Audit `PttCopier` relay: confirm `ask`/`bid` are forwarded for follower trim copies | P2 | Small |
| DW-B35-NEXT-01 | Full 8-step sim gate: BE bracket-replace + B35 bracket-cancel flows (from B35-LaneB backlog) | P1 | 1 session |
| DW-B35-NEXT-02 | DW-B32-TRIM-MARKET-01 fix: remove `buffer=0` market fallback | P1 | Medium |
| DW-B35-NEXT-03 | DW-B32-TRIM-ANCHOR-01 fix: `ComputeLimitPx` anchor correction | P1 | Medium |
| DW-B35-NEXT-04 | R-B32-03 / DW-B32-TRIM-CLOSE-01: ATM OCO bracket corruption on market exit — architect-led fix | P1 | Large |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection guard — Director approval required | P2 | Small |
| U1 | NT8 arg8 OCO group ID effectiveness — sim test | LOW | 1 session |

---

## 5. Deferred Work Count Summary

| State | Count |
|-------|-------|
| CLOSED by B34 | 5 |
| New deferred (DW-B34-*) | 2 |
| Carried forward OPEN | 8 |
| **Total OPEN after B34** | **10** |

---

*Author: ptt-plan-reviewer | Block: B34 | Phase 5 | 2026-07-27*
*Appended to chain: B33-Modular/06-deferred-backlog.md → B35-LaneB/06-deferred-backlog.md → B34-multiAcct/06-deferred-backlog.md*
*GATE: FINAL_PASS is confirmed — this file exists and lists all required items.*
