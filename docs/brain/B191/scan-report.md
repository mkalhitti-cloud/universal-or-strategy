# B191 Full Code Scan Report
# Hardcoded Qty / Per-Account Sizing Safety Audit
# Written: 2026-10-06 (post QAll2t repair)

## Trigger
PA-01 received 15 contracts when it held only 5 (MES $100 SL4 ATM = 5 contracts).
Root cause: `PttGlobalQuickExit.Execute()` built `forcedTargets` once from the leader
panel's position (PAAPEX10=20 contracts, T1=10, T2=10) then passed that same fixed list
to every account in Account.All.
Fix: B191-DIRECT -- `perAccTargets = TradeCopierPanel.Build2TargetList(pos.Quantity)`
built inside the per-account loop.

## Files Scanned and Status

### Features/ directory

| File | Status | Notes |
|------|--------|-------|
| PttGlobalQuickExit.cs | FIXED (B191-DIRECT) | Lines 177-181: perAccTargets per account. ExecuteFollowers passes perAccTargets to ResolveFollowerTargets. |
| PttQuickExit.cs | SAFE | Reads pos.Quantity live per account in Execute(). |
| PttBeSwapAll.cs | SAFE | Loops per account, reads pos.Quantity + SnapshotBeTargetsPublic per account. |
| PttBeSwapOne.cs | SAFE | Per-panel, reads own pos.Quantity. |
| PttBreakEvenSwap.cs | SAFE | Receives pre-resolved qty from caller per account. |
| PttTrim.cs | SAFE | pos.Quantity / 2 per leader account. |
| PttFlatten.cs | SAFE | pos.Quantity per leader account. |
| PttCancel.cs | SAFE | Cancels orders by state/instrument, no qty logic. |
| PttCopier.cs | SAFE | Pure relay, no qty logic. |
| PttGlobalBreakEven.cs | SAFE | Buffer holder only, no qty. |
| PttFollowerStrategy.cs | N/A (INACTIVE) | Guarded by #if PTT_FOLLOWER_ACTIVE -- not compiled in production. |

### CopyEngine.cs

| Method | Status | Notes |
|--------|--------|-------|
| TrimOneAccount | SAFE | FindPosition(acc, instrument).Quantity per account. |
| FlattenOneAccount | SAFE | FindPosition(acc, instrument).Quantity per account. |
| TrimOneAccountLimit | SAFE | FindPosition(acc, instrument).Quantity, ceiling/2 for trim. |
| FlattenOneAccountLimit | SAFE | FindPosition(acc, instrument).Quantity per account. |
| AllAccounts() | SAFE | Returns master + followers from rule -- no qty logic. |
| FindRule() | SAFE | Instrument-name lookup only -- no qty. |
| BreakEven(Instrument,int) | SAFE | Delegates to PttBeSwapOne.Execute per account -- qty resolved inside. |
| BreakEven(Account,Instrument,int) | SAFE | Same -- followers + leader each execute independently. |
| DispatchToFollower | SAFE | baseQty * mult from original order signal -- not stale position read. |
| TryLaneDDispatchFollower | SAFE | baseQty * mult from original order -- Clone mode entry. |
| TryLaneEDispatchFollower | SAFE | pos.Quantity from follower's remaining position (post-partial QX). |
| DrainThenDispatch | SAFE | qty parameter from caller (baseQty * mult from order signal). |
| SnapshotBeTargetsPublic | SAFE | Returns order snapshots -- no qty arithmetic. |
| TightenOneAccountStops | SAFE | Uses stop prices, not position qty. |
| SyncAtmFollowerBracket | SAFE | Uses leaderOrder.Quantity (DW-B142-QTY-DESYNC-01 fix). |

### TradeCopierPanel.cs (button handlers)

| Handler | Status | Notes |
|---------|--------|-------|
| OnTrim | SAFE | Delegates to _engine.Trim(_leaderAccount, _instrument) -- qty resolved in TrimOneAccount. |
| OnFlatten | SAFE | Delegates to _engine.Flatten -- qty resolved in FlattenOneAccount. |
| OnBeUp/OnBeDown | SAFE | Only modifies _beBuffer (UI spinner). |
| OnBeClick (line 1392) | SAFE | Passes _beBuffer (ticks buffer, not qty) to PttBeSwapOne.Execute. |
| OnGlobalBeClick (line 1282) | SAFE | Passes GlobalBeBuffer (ticks) to PttBeSwapAll.Execute. |
| OnQuickClick | SAFE | Calls PttQuickExit.Execute(_leaderAccount, _instrument, _quickT1, null). Per-account qty inside. |
| OnQuickAllClick | SAFE | Calls PttGlobalQuickExit (no 2-target version). |
| OnInstr2tClick | SAFE | TryResolve2TargetContext -> Build2TargetList(leader.qty). Fires on leader only. |
| OnInstrQAll2tClick | FIXED (B191-DIRECT) | Was: passed leader qty targets to all accounts. Now: each account uses own pos.Quantity. |
| DispatchShortcut (Key.B) | NOTE | Default buf=2 if _beBufferBox.Text unparseable. Not a qty bug. Existing behavior. |

## Build2TargetList Call Sites (production only)

| Location | Input | Status |
|----------|-------|--------|
| PttGlobalQuickExit.cs:180 | pos.Quantity (per-account loop) | SAFE (B191 fix) |
| TradeCopierPanel.cs:1701 | leader pos.Quantity (TryResolve2TargetContext) | SAFE (leader panel only, not passed verbatim to other accounts since B191) |

## ResolveFollowerTargets Analysis

Called in ExecuteFollowers at PttGlobalQuickExit.cs:216-221 with followerPosQty=0.
When followerPosQty=0, ScaleLeaderTargets is NOT called (line 556-557 early return).
Followers always use their own live snapshot from SnapshotTargetOrders(follower, pos.Instrument).
This is correct -- follower exit quantities are driven by their own open brackets.

## Verdict

ROOT CAUSE: FIXED (B191-DIRECT).
No other hardcoded qty patterns or similar per-account sizing risks found.
All exit flows (Quick, Quick2t, Trim, Flatten, BE, BE ALL, QAll, QAll2t) resolve qty
from live per-account position or per-account order snapshot.

## Outstanding Items (carry forward)

| ID | Priority | Description |
|----|----------|-------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen -- blocked until Lane E is next modified |
| DW-B187-01 | P3 | followerPosQty=0 in ExecuteFollowers -- follower scaling path unreachable (by design per ResolveFollowerTargets logic, but should be documented in a ticket for review) |

## Scan Coverage

- Features/: 11 files (10 scanned, 1 inactive)
- CopyEngine.cs: all qty-critical methods read (TrimOneAccount, FlattenOneAccount,
  TrimOneAccountLimit, FlattenOneAccountLimit, DispatchToFollower, TryLaneDDispatchFollower,
  TryLaneEDispatchFollower, DrainThenDispatch, AllAccounts, FindRule, SnapshotBeTargetsPublic,
  ResolveFollowerTargets, ExecuteFollowers)
- TradeCopierPanel.cs: all button + keyboard handlers for exit operations

Scan complete. No additional repairs required.
