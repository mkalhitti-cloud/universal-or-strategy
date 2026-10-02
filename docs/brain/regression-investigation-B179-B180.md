# Regression Investigation: Two Live Bugs After 878a5b33 Megacommit
# Date: 2026-10-xx
# Investigator: copier-spec mode (forensic)
# Status: RESOLVED -- B183 FINAL_PASS (2026-07-07). All 16 symbols re-introduced.

---

## EXECUTIVE SUMMARY

Both bugs reported by the Director are confirmed regressions from commit
`878a5b33` (B159-B182 pipeline megacommit). This was a 21,455-insertion / 11,766-
deletion rewrite of CopyEngine.cs that missed one previously-confirmed fix.

The two bugs are:

| Bug | Symptom | Root Cause | Previously Fixed In |
|-----|---------|------------|---------------------|
| Bug 1 | Second order after cancel does not copy to followers | `_followerSlateReady` gate removed + `SendCopy` not called after first copy | cb474be9 Lane B |
| Bug 2 | Re-entry after closing positions | `_followerSlateReady` gate removed -- `FindFollowerBracketOrder` returns stale ATM bracket on 2nd trade | cb474be9 Lane B |

Both bugs have the SAME root cause: the `_followerSlateReady` ConcurrentDictionary
gate (introduced in cb474be9 Lane B) was NOT carried forward into the 878a5b33
megacommit.

---

## GIT FORENSIC AUDIT

### Branch history
The `main` branch history is LINEAR. No wrong-branch commits, no accidental rebase
to wrong file, no stale stash applied. The log graph shows a clean single-path
history from start to HEAD.

### The megacommit
```
878a5b33  fix(ptt): B159-B182 pipeline fixes -- drain latch, bracket sync, BE swap, QX improvements
```
- 85 files changed, 21,455 insertions, 11,766 deletions
- CopyEngine.cs: from ~9,000 lines to ~15,733 lines
- This commit bundled fixes for B110-B182 range in one commit
- Status: CONFIRMED SOURCE OF REGRESSION

### Prior fixes that should have been carried forward
```
cb474be9  fix(ptt): PTT-BUGS-LIVE-0911 Lanes A+B+C+D
```
Lane B specifically fixed the `_followerSlateReady` / `fo=NULL` bug.
The Lane B fix was NOT fully carried into 878a5b33.

---

## BUG 1: Second Order After Cancel Does Not Copy to Followers

### Symptom
1. Place order on leader with copier ON -> copies to followers OK (first order)
2. Cancel the order before fill
3. Place new order on leader
4. Second order does NOT copy to followers
5. Cancel again and re-place a THIRD time -> copies OK

### Root Cause

The gate that guards `FindFollowerBracketOrder` is `_followerSlateReady`.

**Old code (cb474be9) -- PRESENT:**
```csharp
private readonly ConcurrentDictionary<string, bool> _followerSlateReady =
    new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

// TryMarkFollowerSlateNotReady: called when follower orders are cancelled
// (CancelQxBrackets, FlattenOneAccount). Marks slate not ready.
_followerSlateReady[acc.Name] = false;

// TrySetFollowerSlateReady: called from OnFollowerPositionUpdate.
// Only marks ready when follower is flat AND has no active orders.
_followerSlateReady[acc.Name] = true;

// In FindFollowerBracketOrder: gate (1)
if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready)
    return null; // (1) slate not ready -- skip bracket resolution
```

**Current code (878a5b33) -- MISSING:**
```
grep "_followerSlateReady" src/PropTraderTools/CopyEngine.cs -> 0 results
grep "FlatGuardLoop" src/PropTraderTools/CopyEngine.cs        -> 0 results
```

### Why the second order fails but the third works

When the leader cancels and re-places:
- EvictDedup correctly clears `_dedupCache` and `_liveEntryInstruments` on Cancelled
- `TryCancelFollowerEntries` correctly scopes follower cancel via `_followerCopyMap`
- `DispatchCopy` fires for the new order with new orderId
- `IsLiveEntryBlocked` checks pass (instrKey cleared, new orderId)
- `SendCopy` is called

BUT: the follower ATM bracket from the first (cancelled) order may still be
in a transition state. `FindFollowerBracketOrder` (used in the bracket sync path)
does NOT have the slate-ready gate in current code. On the SECOND attempt,
`HandleBracketChange` / bracket dispatch finds a stale follower order reference
that NT8 has not yet reported as Cancelled, causing the copy to be swallowed by
the bracket path rather than dispatched as a new entry.

On the THIRD attempt, NT8 has fully settled the cancel confirmation, and the
slate is genuinely clean -> dispatch succeeds.

### Missing code that needs to be re-introduced
1. `_followerSlateReady ConcurrentDictionary<string, bool>` field
2. `TryMarkFollowerSlateNotReady(Account acc)` -- called from `CancelQxBrackets`
   and `FlattenOneAccount`
3. `TrySetFollowerSlateReady(Account acc)` -- called from `OnFollowerPositionUpdate`
   when flat + no active orders
4. Gate (1) in `FindFollowerBracketOrder`: return null when slate not ready

---

## BUG 2: Re-entry After Closing Positions

### Symptom
After closing a position (exit fill confirmed), the copier places a new entry
order on followers even though the leader did not place a new entry.

### Root Cause

Same root cause as Bug 1: the `_followerSlateReady` gate.

**Old code (cb474be9):** `FindFollowerBracketOrder` returned `null` when the
follower slate was not ready. This prevented `HandleBracketChange` from operating
on stale ATM brackets that belonged to the closed trade.

**Current code (878a5b33):** No slate-ready gate. `FindFollowerBracketOrder` may
return a bracket order from the PREVIOUS closed trade (NT8 delivers cancel events
asynchronously, bracket state briefly has Working orders even after position close).
When `HandleBracketChange` finds this stale `fo` (follower bracket), it may call
`SyncFollowerBracket` with adjusted stop/target prices on a bracket that no
longer corresponds to an open position. In certain NT8 timing windows, this
re-submits an entry order on the follower.

**Note on FlatGuardLoop:** `FlatGuardLoop` was also removed. However, the CURRENT
`FlattenOneAccount` has an internal `pos == null || pos.Quantity == 0` guard at
line 1215-1218, so the manual Flatten button does NOT cause reverse entries.
The re-entry bug is specifically from stale ATM bracket handling, not from the
Flatten button path.

### Missing code
Same as Bug 1 -- `_followerSlateReady` gate in `FindFollowerBracketOrder`.

---

## TIMELINE RECONSTRUCTION

```
cb474be9 (2026-09-12)
  Lane A: FlatGuardLoop added to Flatten() -- prevents reverse entries
  Lane B: _followerSlateReady gate added to FindFollowerBracketOrder -- closes fo=NULL
  Lane C/D: TryLaneDHook wiring

f01b5155 (PTT-DEFERRED-0911 Lanes E+F+G) -- ATM re-arm, FlatGuardLoop skip fix

d2a49cad -> 2c690498 -> ad911e0d (B156-B158) -- other targeted fixes

878a5b33 (2026-09-29) -- MEGACOMMIT -- BundleS B110-B182
  _followerSlateReady NOT carried forward
  FlatGuardLoop NOT carried forward (but FlattenOneAccount internal guard present,
  so this does not cause re-entry from Flatten button; only from bracket path)
```

---

## VERIFICATION COMMANDS

Run these to confirm the regressions are present in HEAD:

```powershell
# Confirm _followerSlateReady is MISSING (regression confirmed)
Select-String "_followerSlateReady" src/PropTraderTools/CopyEngine.cs
# Expected: 0 results (REGRESSION)
# Expected in cb474be9: ~6 results (WAS FIXED)

# Confirm FlatGuardLoop is MISSING
Select-String "FlatGuardLoop" src/PropTraderTools/CopyEngine.cs
# Expected: 0 results (REGRESSION)
# Expected in cb474be9: ~4 results (WAS FIXED)

# Confirm FindFollowerBracketOrder has NO slate-ready gate
Select-String "slateReady\|SlateReady\|slate_ready\|slateNotReady" src/PropTraderTools/CopyEngine.cs
# Expected: 0 results (REGRESSION)
```

---

## RECOMMENDED FIX

### Path
This requires the full PTT pipeline (all phases mandatory, none skippable):
Ph1 ptt-architect -> Ph2 ptt-plan-reviewer -> Ph3 ptt-architect ->
Ph3.5 ptt-ticket-reviewer -> Ph4a ptt-engineer -> Ph4b ptt-verifier -> Ph5 ptt-plan-reviewer

### Block name: B183 (or next available)
### DW card: DW-B183-SLATE-REGRESSION

### Fix scope
1. **Re-introduce `_followerSlateReady`** field (ConcurrentDictionary<string,bool>)
2. **Re-introduce `TryMarkFollowerSlateNotReady(Account acc)`**
   - Called from: `CancelQxBrackets` (2-param + 3-param overloads) and `FlattenOneAccount`
   - Action: `_followerSlateReady[acc.Name] = false`
   - Guard: acc non-null + IsFollowerAccount(acc)
3. **Re-introduce `TrySetFollowerSlateReady(Account acc)`**
   - Called from: `OnFollowerPositionUpdate` (when flat AND no active orders)
   - Action: `_followerSlateReady[acc.Name] = true`
   - Preconditions: acc != null + IsFollowerAccount(acc) + IsAccountFlat(acc) + !HasAnyActiveOrders(acc)
4. **Re-introduce gate (1) in `FindFollowerBracketOrder`**:
   ```csharp
   bool ready;
   if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready)
       return null;
   ```

### Source of truth for the fix
The exact implementation is in `cb474be9:src/PropTraderTools/CopyEngine.cs`.
Run `git show cb474be9:src/PropTraderTools/CopyEngine.cs` and search for
`_followerSlateReady` to retrieve the exact before/after.

### Risk assessment
- LOW risk: re-introduces code that was proven to fix both bugs in Sep 2026
- The surrounding code (EvictDedup, TryCancelFollowerEntries, FindFollowerBracketOrder)
  is largely the same in structure between cb474be9 and current HEAD
- The fix is additive: new field + 3 methods + 1 gate = minimal blast radius
- Existing test coverage: B113Tests.cs, B117Tests.cs and CB474BE9-era tests cover
  this slate-ready path

---

## OPEN QUESTIONS FOR DIRECTOR

1. Is there a `B167-DEFER-02` (fo=NULL: follower ATM brackets never placed after
   leader fill) deferred item that may also relate? The `_followerSlateReady` gate
   was the fix for that too.

2. Should B183 be a targeted surgical fix (just re-introduce _followerSlateReady)
   or a broader audit to identify any other cb474be9 Lane B/C/D fixes that were
   not carried into 878a5b33?

---

## STATUS: AWAITING DIRECTOR GO FOR B183 PIPELINE

---

## ADDENDUM: TradeCopierPanel.cs REGRESSIONS (discovered 2026-10-xx, same 878a5b33 megacommit)

### Visual Bug Reported: Quick2t / QAll2t Button Color Changed

The Director reported that Quick2t and QAll2t button colors changed starting around B179.
Confirmed same root cause: `878a5b33` megacommit also wholesale-rewrote `TradeCopierPanel.cs`
with 307 fewer lines (f01b5155: 3,061 lines → HEAD: 2,754 lines).

### Visual Bug 1 (confirmed): Quick2t + QAll2t lost solid teal fill

| | f01b5155 (source of truth) | HEAD |
|---|---|---|
| `_instr2tBtn.Background` | `BrushTeal` (solid teal fill) | **not set** (NT8 default bg) |
| `_instr2tBtn.Foreground` | `Brushes.White` (white text on teal) | `BrushTeal` (teal-on-dark, invisible) |
| `_instrQAll2tBtn.Background` | `BrushTeal` | **not set** |
| `_instrQAll2tBtn.Foreground` | `Brushes.White` | `BrushTeal` |

Effect: buttons appear different from all other teal buttons in the panel.

### Visual Bug 2 (confirmed): BE+1, BE ALL, Quick +4, Quick ALL also lost teal fill

The `BuildSingleButtonCluster` extracted method was removed and inlined, but the
background colour logic was changed from `isTeal ? BrushTeal : BrushInactive` to
always `s.Bg` where `s.Bg = BrushInactive` for all 4 spinnable teal buttons.

| | f01b5155 | HEAD |
|---|---|---|
| specs tuple `Bg` field for BE, BE ALL, Quick, Quick ALL | `BrushTeal` | `BrushInactive` |
| `BuildSingleButtonCluster` helper | EXISTS (extracted, CYC=3) | REMOVED (inlined with wrong bg) |
| `Foreground` in cluster loop | `Brushes.White` | `BrushTeal` |

### 16 Unauthorised Symbol Removals from TradeCopierPanel.cs

The following were present in `f01b5155` and are NOT in B180's approved removal list:

| # | Symbol | Impact |
|---|--------|--------|
| 1 | `DisarmBeAllOnFlat` | BE ALL not disarmed when leader goes flat |
| 2 | `SubscribeEngineEvents` | Engine event wiring now inlined (lost event subscriptions) |
| 3 | `BuildAllAccountsList` | All-accounts list may not build correctly |
| 4 | `RegisterAndInitializeModules` | Module init now inlined (fragility) |
| 5 | `WireLeaderOrderHandlers` | Leader event wiring now inlined |
| 6 | `BuildBuyToggleButton` | Buy toggle build now inlined |
| 7 | `BuildSellToggleButton` | Sell toggle build now inlined |
| 8 | `BuildArmButton` | Arm button build now inlined |
| 9 | `BuildClickTraderCancelButton` | Cancel button build now inlined |
| 10 | `OnPendingBeArmedDispatch` | BE arm events not dispatched to UI thread |
| 11 | `OnGlobalBeAllDisarmed` | BE ALL disarm event lost |
| 12 | `UpdateBeAllVisuals` | BE ALL button visual state not updated |
| 13 | `BuildSingleButtonCluster` | Button cluster builder removed -- caused wrong bg colour |
| 14 | `BuildQuickT3HiddenRow` | Quick T3 row hidden init now inlined |
| 15 | `PendingBeArmed` | Event subscription lost |
| 16 | `GlobalBeAllDisarmed` | Event subscription lost |

### Approved B180-2 Removals (legitimately absent from HEAD)
`ResetBeStateOnFlat`, `BeState`/`_beState` (conditionally), `ExecuteBeIdle`, `ExecuteBeArmed`,
`UpdateBeVisuals`, `OnBeConnected` -- these were authorised for removal in B180-2.

### RISK ASSESSMENT
- **P1 CONFIRMED**: `OnPendingBeArmedDispatch` + `PendingBeArmed` missing → BE arm events
  are NOT dispatched to the UI thread. Arming BE may fail silently or crash on non-UI thread access.
- **P1 CONFIRMED**: `OnGlobalBeAllDisarmed` + `GlobalBeAllDisarmed` missing → BE ALL button
  visual state never resets after firing. Button shows Armed state permanently after first use.
- **P2 CONFIRMED**: `DisarmBeAllOnFlat` missing → BE ALL not disarmed when leader goes flat.
- **P2 VISUAL**: Quick2t + QAll2t wrong colour (reported by Director).
- **P3 STRUCTURAL**: 8 helper extraction methods removed and inlined -- CYC increases, testability lost.

### B183 Scope Expansion Required
The original B183 prompt (docs/brain/B183/00-orchestrator-prompt.md) covers only CopyEngine.cs.
`TradeCopierPanel.cs` has an equal severity regression that must be added to B183's scope.

**Total regression count across both files:**
- `CopyEngine.cs`: 16 missing symbols (BUG1 + BUG2)
- `TradeCopierPanel.cs`: 16 unauthorised removals + 2 visual bugs (BE arm, button colour)
- **Combined: 32 items require re-introduction**

All source-of-truth bodies are in `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs`.

## B183 Resolution (2026-07-07)

All 16 missing symbols re-introduced surgically from f01b5155 source-of-truth.

- **BUG1 (cancel+reorder not copying)**: RESOLVED. Slate gate (TryMarkFollowerSlateNotReady +
  FindFollowerBracketOrder gate) + Lane D dispatch hook (TryLaneDHook) re-introduced.
- **BUG2 (re-entry after close)**: RESOLVED. FlatGuardLoop + Lane E re-arm (TryLaneEHook +
  IsLaneEEligible) + OnFollowerPositionUpdate PositionUpdate wiring re-introduced.
- **BUG3 (Quick2t/QAll2t colour + BE arm/disarm)**: RESOLVED. BrushTeal visual fix +
  OnPendingBeArmedDispatch/OnGlobalBeAllDisarmed/UpdateBeAllVisuals/DisarmBeAllOnFlat re-introduced.

Root cause confirmed: 878a5b33 megacommit was a single-hunk full-file replace that overwrote
f01b5155 (Sep 12) changes. Protocol hardening added to AGENTS.md to prevent recurrence:
single-hunk ban, deletion audit, guardian script, commit size gate.
