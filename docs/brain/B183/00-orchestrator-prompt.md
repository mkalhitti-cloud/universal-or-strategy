# B183 Orchestrator Prompt
# Re-introduce 16 Symbols Dropped by 878a5b33 Megacommit (CopyEngine.cs)
# Re-introduce 16 Symbols + Fix 2 Visual Bugs (TradeCopierPanel.cs)
# Root cause: cancel+reorder not copying; re-entry after close; Quick2t colour changed

---

## PASTE THIS ENTIRE DOCUMENT INTO A NEW ptt-orchestrator SESSION

---

## MISSION BRIEF

You are the B183 ptt-orchestrator. Two live regressions AND one live UI bug were introduced
by the B159-B182 megacommit (878a5b33, Sep 29) which wholesale-replaced BOTH CopyEngine.cs
(9,329->6,404 line single-hunk overwrite) AND TradeCopierPanel.cs (3,061->2,754 lines,
40-hunk rewrite). Both files dropped symbols from commits cb474be9 and f01b5155 (Sep 12).

### The Three Bugs

**BUG1 (cancel+reorder not copying)**: When the leader cancels an order and places a new
order while the copier is ON, the second order does NOT copy to followers. A third
cancel+reorder cycle is required for copy to resume. [CopyEngine.cs]

**BUG2 (re-entry after close)**: After closing positions, followers re-enter with a ghost
order. This was previously fixed and has now regressed. [CopyEngine.cs]

**BUG3 (Quick2t / QAll2t button colour)**: Quick2t and QAll2t buttons changed colour
starting around B179 -- they should match the other teal buttons but now appear with
a different background. Also: BE+1, BE ALL, Quick +4, Quick ALL lost their solid teal
fill background. [TradeCopierPanel.cs] Additionally: OnPendingBeArmedDispatch and
GlobalBeAllDisarmed event infrastructure was removed -- BE ALL arm/disarm visual
state may be broken.

### Root Cause (do not re-investigate -- confirmed by full git forensics)

16 symbols are missing from HEAD (5,951-line file) that exist in the source-of-truth
snapshot f01b5155 (8,632-line file). Cherry-pick is non-viable (12 conflicting hunks due
to renamed methods and completely different file structure). The correct approach is
surgical re-introduction: copy the exact bodies verbatim from f01b5155 and insert at
the correct call sites in the current HEAD file.

---

## THE PIPELINE (MANDATORY -- ALL 7 PHASES -- NONE SKIPPABLE -- NONE COMBINABLE)

You MUST execute ALL phases in sequence. No phase may be skipped, merged with another,
or abbreviated. Each phase produces a gate artifact; the next phase does not begin until
the gate artifact exists and passes.

```
Ph1  ptt-architect       -> docs/brain/B183/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B183/02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B183/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B183/04-ticket-review.md   (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B183/ticket-N-completion.md
Ph4b ptt-verifier        -> docs/brain/B183/ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B183/05-final-review.md + docs/brain/B183/06-deferred-backlog.md
```

### Phase gating rules

- Ph2 produces `REVIEW_PASS: YES` or `REVIEW_PASS: NO`. If NO, return to Ph1 with reviewer notes.
- Ph3.5 produces `TICKET_REVIEW_PASS: YES` or `TICKET_REVIEW_PASS: NO`. If NO, return to Ph3.
- Ph4b produces `VERIFY_PASS: YES` or `VERIFY_PASS: NO` per ticket. If NO, return to Ph4a.
- Ph5 produces `FINAL_PASS: YES` or `FINAL_PASS: NO`. If NO, return to Ph4a/Ph4b for fixes.

---

## CONSTRAINT SET (read before Ph1)

1. **SRC CODE BAN**: ptt-engineer (Ph4a) is the ONLY role that edits .cs files. All other
   phases are read/plan/review only. No other phase touches src/.

2. **SOURCE OF TRUTH**: All 16 symbol bodies MUST be copied verbatim from:
   `git show f01b5155:src/PropTraderTools/CopyEngine.cs`
   Do NOT rewrite, refactor, or modernize the code. Copy exact.

3. **Jane Street compliance** (verified already present in source-of-truth bodies):
   - CYC <= 8 for every method (each method's CYC is documented in its XML comment)
   - No `lock()` anywhere (JS-021: use ConcurrentDictionary / Interlocked only)
   - ASCII-only strings -- no Unicode, no emoji, no curly quotes (JS-081)
   - No `throw new XxxException` in hot paths (JS-001)
   - No `return null` replaced by null contract (JS-002: `Order?` used correctly)
   - No `async void` (JS-033)

4. **After any .cs edit**: run `powershell -File .\scripts\ptt-sync.ps1` to re-sync NT8 hard links.

5. **Approved Deletion List for B183**: NONE. This ticket adds code only; no deletions.

6. **TrySetSlateReadyOnDrainFill is NOT in scope**: It was deliberately removed in f01b5155
   (superseded by Lane E). Do NOT re-introduce it.

7. **DispatchCopy `dispatched` counter**: The HEAD DispatchCopy loop lacks both the
   `dispatched` counter and the `SetLiveEntryDispatched` call. These must be re-added
   as part of the Lane D wiring (Ticket 3).

8. **write_file ABSOLUTE BAN on CopyEngine.cs and TradeCopierPanel.cs** (P0 -- non-negotiable):
   ptt-engineer MUST use `apply_diff` ONLY for all edits to these files. `write_file` is
   BANNED. A full-file replace produces a single-hunk diff (@@ -1,NNNN +1,MMMM @@) which
   is the exact mechanism that caused the 878a5b33 megacommit regression. Any PR where
   `git show HEAD -- CopyEngine.cs | Select-String "^@@" | Measure-Object` returns Count=1
   is an INSTANT VERIFY_FAIL regardless of content. ptt-verifier MUST check this.

---

## THE 16 MISSING ITEMS (groups, bodies, and insertion points)

All bodies below are verbatim from `git show f01b5155:src/PropTraderTools/CopyEngine.cs`.
Line numbers given for HEAD (current file = 5,951 lines). Verify with grep before inserting.

---

### GROUP A: Slate Gate (BUG1 + BUG2 core mechanism)

**Purpose**: Tracks per-follower "slate ready" state. When a bracket wipe is imminent
(cancel or flatten), the slate is marked not-ready, blocking `FindFollowerBracketOrder`
from returning stale bracket orders. When the position goes flat and orders are clean,
the slate is re-armed (ready=true), unblocking bracket sync for the next trade.

#### A1: Field declaration `_followerSlateReady` (insert after line 385 in HEAD)

Insert after the `_drainOwnedOrderIds` field declaration (HEAD line 385), before the
`// --- Status event ---` comment (HEAD line 387):

```csharp
        // BUG1+BUG2: Slate-ready gate per follower. Key = acc.Name (ASCII). Value = bool.
        // false: bracket wipe imminent -- FindFollowerBracketOrder returns null (gate closed).
        // true / absent: gate open -- normal bracket search proceeds.
        // ConcurrentDictionary: no lock (JS-021). Indexer write is atomic.
        private readonly ConcurrentDictionary<string, bool> _followerSlateReady =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        // Lane D: dispatch record for TryLaneDHook-handled ATM followers (double-dispatch guard).
        // Key: orderId + "#" + accName (ASCII-only). Value: 0 (unused placeholder).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
        // Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
        private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        // Lane E: re-arm dedup guard. Prevents double-arm on multiple PositionUpdate events for the
        // same post-QX/BE slot. Key: accName + "#LANE-E#" + instrFullName (ASCII-only). Value: 0 (unused).
        // ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. TryRemove: called from CancelQxBrackets.
        // Growth: O(followers * instruments). ~60 bytes/entry. Cleaned up per QX/BE cycle.
        private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

#### A2: `TryMarkFollowerSlateNotReady` + `TrySetFollowerSlateReady` + `IsAccountFlat` + `HasAnyActiveOrders` + `OnFollowerPositionUpdate`

Insert as a new block AFTER the `BuildQxSnapshot` method and BEFORE `CancelQxBrackets`
(HEAD line 918 is `CancelQxBrackets`). Find the blank line before line 910 (`// IsQxCancelCandidate`
comment) as the anchor. Insert after `BuildQxSnapshot` ends (find `}` that closes it) and before
the `// IsQxCancelCandidate` comment.

Exact body to insert (5 methods, verbatim):

```csharp
        // BUG1+BUG2: Mark follower slate as not-ready. Called before any bracket wipe.
        // Conditions: acc non-null + IsFollowerAccount. Early returns prevent false negatives.
        // CYC<=2: acc null(1) + IsFollowerAccount(2). JS-021: ConcurrentDictionary indexer is lock-free.
        // JS-001: no throw. JS-002: void. ASCII-only.
        private void TryMarkFollowerSlateNotReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            _followerSlateReady[acc.Name] = false;
        }

        // BUG2: Mark follower slate as ready after position confirmed flat + no active orders.
        // Called from OnFollowerPositionUpdate.
        // Conditions: acc non-null + IsFollowerAccount + IsAccountFlat + !HasAnyActiveOrders.
        // Only writes ready=true when ALL conditions pass. Early-returns prevent false positives.
        // CYC<=4: acc null(1) + IsFollowerAccount(2) + !IsAccountFlat(3) + HasAnyActiveOrders(4).
        // JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        private void TrySetFollowerSlateReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            if (!IsAccountFlat(acc))            // (3)
                return;
            if (HasAnyActiveOrders(acc))        // (4)
                return;
            _followerSlateReady[acc.Name] = true;
        }

        // BUG2: Returns true when acc has no open positions across all instruments.
        // Used by TrySetFollowerSlateReady to confirm flat before marking slate ready.
        // NT8: acc.Positions is available in AddOnBase. Each Position.Quantity > 0 = open.
        // CYC<=3: acc null(1) + foreach(2) + Quantity>0(3). JS-021: no lock (read-only enumeration).
        // JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool IsAccountFlat(Account acc)
        {
            if (acc == null)                    // (1)
                return true;
            foreach (var pos in acc.Positions)  // (2)
                if (pos.Quantity > 0)           // (3)
                    return false;
            return true;
        }

        // BUG2: Returns true when acc has any Working/Submitted/Accepted orders remaining.
        // Used by TrySetFollowerSlateReady to confirm no residual active orders before ready.
        // NT8: acc.Orders.ToList() snapshot -- standard pattern in CopyEngine (line 4898).
        // CYC<=5: acc null(1) + foreach(2) + Working(3) + Submitted(4) + Accepted(5).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool HasAnyActiveOrders(Account acc)
        {
            if (acc == null)                    // (1)
                return false;
            foreach (var o in acc.Orders.ToList())  // (2)
            {
                if (o.OrderState == OrderState.Working    // (3)
                    || o.OrderState == OrderState.Submitted  // (4)
                    || o.OrderState == OrderState.Accepted)  // (5)
                    return true;
            }
            return false;
        }

        // BUG2: NT8 account.PositionUpdate event handler.
        // Fires on NT8 account background thread (same context as OnOrderUpdate).
        // When position goes flat and no active orders remain, marks slate ready for next trade.
        // NT8: PositionEventArgs.Position.Account confirmed from TradeCopierPanel.cs line 2109.
        // No UI operations -- ConcurrentDictionary write is thread-safe, no Dispatcher needed.
        // CYC<=3: e/Position/Account null(1) + IsFollowerAccount(2) + TrySetFollowerSlateReady(0).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. No async/await.
        private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
        {
            if (e?.Position?.Account == null)           // (1)
                return;
            if (!IsFollowerAccount(e.Position.Account)) // (2)
                return;
            TrySetFollowerSlateReady(e.Position.Account);
            TryLaneEHook(e.Position.Account, e.Position); // Lane E: re-arm ATM after partial QX/BE
        }
```

#### A3: Call sites for `TryMarkFollowerSlateNotReady` + `_laneEDispatched.TryRemove`

**CancelQxBrackets 2-param** (HEAD line 918-948): Insert after the null guard (after `return; // (1)`,
HEAD line 921), before `var stale = new`:

```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

**CancelQxBrackets 3-param** (HEAD line 998-1045): Insert after the null guard (after `return; // (1)`,
HEAD line 1005), before `var stale = new`:

```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG1+BUG2: bracket wipe imminent (3-param path)
            _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

**FlattenOneAccount** (HEAD line 4677): After `CancelAllAccountOrders` call (HEAD line 4709),
the source-of-truth has `TryMarkFollowerSlateNotReady` called before `CancelAllAccountOrders`.
Check f01b5155 line 5748 for exact position. In HEAD the call should go BEFORE `CancelAllAccountOrders`
(HEAD line 4709):

```csharp
            TryMarkFollowerSlateNotReady(acc); // BUG2: flatten dispatch imminent
```

#### A4: Gate `(1)` in `FindFollowerBracketOrder` (HEAD line 3571)

HEAD line 3571-3582 is the Account-overload that delegates to the IEnumerable overload.
It currently is an expression-body (`=>`). Must convert to block body to add the gate:

```csharp
        private Order? FindFollowerBracketOrder(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        )
        {
            // BUG1+BUG2: slate gate -- if bracket wipe was signalled, block bracket lookup.
            // Absent key (first trade): gate open. Key=false: gate closed. Key=true: gate open.
            // CYC: +1 (from 1 to 2). JS-021: ConcurrentDictionary.TryGetValue is lock-free. ASCII-only.
            bool ready;
            if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready) // (1)
                return null;
            return FindFollowerBracketOrder(
                ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
                fromEntrySignalName,
                isStop,
                leaderName
            );
        }
```

#### A5: PositionUpdate subscription in `Subscribe()` and `Unsubscribe()`

**Subscribe** (HEAD line 1350-1354): add `acc.PositionUpdate += OnFollowerPositionUpdate;` after
`acc.OrderUpdate += OnOrderUpdate;`:

```csharp
        internal void Subscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate += OnOrderUpdate;
                acc.PositionUpdate += OnFollowerPositionUpdate; // BUG2
            }
        }
```

**Unsubscribe** (HEAD line 1356-1360): add `acc.PositionUpdate -= OnFollowerPositionUpdate;` after
`acc.OrderUpdate -= OnOrderUpdate;`:

```csharp
        internal void Unsubscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate -= OnOrderUpdate;
                acc.PositionUpdate -= OnFollowerPositionUpdate; // BUG2
            }
        }
```

---

### GROUP B: FlatGuardLoop (BUG2 flatten guard)

**Purpose**: Prevents FlattenOneAccount from being called on already-flat followers, which
could cause position inversion.

#### B1: `FlatGuardLoop<TAccount>` method

Insert near the END of the class, before the final closing `}` (HEAD line 6403 ends the class).
The method is `private static`, so any position near the end works. Suggested insertion: before
the `PttDragOrphanPredicates` class (before HEAD line 6386).

```csharp
        // BUG-0911-A: extracted for testability -- mirrors FlattenFollower delegate-injection pattern.
        // CYC=3: (1) foreach, (2) isFlat true-branch (skip), (3) isFlat false-branch (flattenOne).
        // JS-021: no lock (static, pure iteration). JS-001: no throw. ASCII-only.
        private static void FlatGuardLoop<TAccount>(
            Func<IEnumerable<TAccount>> getAccounts,
            Func<TAccount, bool> isFlat,
            Action<TAccount> flattenOne)
        {
            foreach (var acc in getAccounts())
            {
                if (isFlat(acc))
                {
                    NinjaTrader.Code.Output.Process(
                        "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    continue;
                }
                flattenOne(acc);
            }
        }
```

#### B2: Call sites for `FlatGuardLoop`

**Flatten(Instrument)** (HEAD line 4482-4491): Replace `foreach (var acc in AllAccounts(instrument)) FlattenOneAccount(acc, instrument);` with:

```csharp
            FlatGuardLoop(
                () => AllAccounts(instrument),
                acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
                acc => FlattenOneAccount(acc, instrument)
            );
```

**Flatten(Account,Instrument)** (HEAD line 4518-4537): Replace the follower `foreach` loop
(after `FlattenOneAccount(leader, instrument);`) with:

```csharp
            FlatGuardLoop(
                () => AllAccounts(instrument).Where(acc => acc != leader),
                acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
                acc => FlattenOneAccount(acc, instrument)
            );
```

---

### GROUP C: Lane D (ATM dispatch hook -- fixes BUG1 copy dispatch path)

**Purpose**: When the copier is in Clone+Named-ATM mode, Lane D handles the initial ATM entry
dispatch separately from the regular `DispatchToFollower` loop. It also adds a dedup gate in
`DispatchToFollower` to prevent double-dispatch (one ATM + one raw).

#### C1: Field `_laneDDispatched` (already included in A1 block above -- do not add twice)

#### C2: `TryLaneDHook` + `TryLaneDDispatchFollower` methods

Insert after `DispatchCopy` ends (HEAD line 2415) as a new block:

```csharp
        // Lane D: ATM dispatch hook. Called once per confirmed leader entry signal after all 5
        // DispatchCopy gates. Handles Clone-mode Named followers only; all other modes are no-ops.
        // CYC=6: (1) mode!=Clone, (2) type!=Limit, (3) !Named, (4) dispatched in loop, (5) laneDCount>0.
        // JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
        // JS-023: no UI update here -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
        // ASCII-only. Thread: NT8 account background thread.
        private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
        {
            // Branch (1): non-Clone mode -> no-op for Signal/Mirror/Market
            if (GetCopyMode() != CopyMode.Clone)
                return;

            // Branch (2): Market leader order -> skip ATM dispatch (SendCopyWithAtm hardcodes Limit;
            // Limit @ price 0 is invalid). Market followers fall through to DispatchToFollower SendCopy.
            if (order.OrderType != OrderType.Limit)
                return;

            // Branch (3): resolve ATM mode; Inherit returned when _cloneAtmCache is empty -> no-op
            FollowerAtmMode cloneAtmMode = GetCloneAtmMode(order.Instrument.FullName);
            if (!(cloneAtmMode is FollowerAtmMode.Named namedMode))
                return;

            // Fan-out: dispatch each follower via ATM
            int laneDCount = 0;
            int idx = 0;
            foreach (Account acc in rule.FollowerAccounts)
            {
                // Branch (4): track dispatched count
                if (TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty))
                    laneDCount++;
                idx++;
            }

            // Branch (5): commit Gate-5 dedup entry so a second OnOrderUpdate for the same fill
            // cannot re-enter TryLaneDHook (SetLiveEntryDispatched is thread-safe by existing design).
            if (laneDCount > 0)
            {
                string instrKey = order.Instrument.FullName + "|" + order.OrderAction.ToString();
                SetLiveEntryDispatched(instrKey, order.OrderId.ToString());
            }
        }

        // Lane D: per-follower ATM dispatch helper. Keeps TryLaneDHook CYC low.
        // CYC=4: (A) null acc guard, (B) daily cap check, (C) SendCopyWithAtm result.
        // JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
        // JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneDHook caller).
        private bool TryLaneDDispatchFollower(
            Account acc,
            CopyRule rule,
            int idx,
            Order order,
            FollowerAtmMode.Named namedMode,
            CopySignal baseSignal,
            int baseQty
        )
        {
            // Branch (A): null guard
            if (acc == null)
                return false;

            // Branch (B): daily position cap -- respect existing cap contract
            if (!PassesDailyCapCheck(acc))
                return false;

            int mult = GetMultiplier(rule, idx);
            CopySignal scaledSig = CopySignal.Create(
                baseSignal.Action,
                baseSignal.Type,   // always Limit (TryLaneDHook guards order.OrderType above)
                baseQty * mult,
                baseSignal.LimitPrice,
                baseSignal.OrderId
            );

            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
            if (ok)
                // Key: orderId + "#" + accName. Separator "#" is ASCII and cannot appear in NT8 account names.
                _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
            return ok;
        }
```

#### C3: Call site for `TryLaneDHook` in `DispatchCopy`

After `int baseQty = ResolveBaseQty(...)` (HEAD line 2373), insert:

```csharp
            // Lane C: Lane D wiring point. Unconditional void call -- 0 CYC cost in parent. PTT-BUGS-LIVE-0911.
            TryLaneDHook(order, rule, baseSignal, baseQty);
```

#### C4: Lane D skip gate in `DispatchToFollower` + `dispatched` counter in `DispatchCopy`

**In `DispatchToFollower`** (HEAD line 2489-2528): After `var mode = ResolveAtmMode(rule, acc.Name);`
(HEAD line 2506), insert the Lane D dedup skip gate:

```csharp
            // Lane D skip gate: if TryLaneDHook already dispatched this (order, follower) pair via ATM,
            // return immediately -- prevents one ATM entry (hook) + one raw entry (loop) double-dispatch.
            // _laneDDispatched.ContainsKey is O(1) lock-free (JS-021). No counter modification -- dedup
            // commit for non-ATM followers in this loop is unaffected.
            string laneD_key = baseSignal.OrderId + "#" + acc.Name;
            if (_laneDDispatched.ContainsKey(laneD_key)) return;
```

**In `DispatchCopy`** (HEAD line 2384-2414): Replace the loop body to add `dispatched` counter
tracking and `SetLiveEntryDispatched`:

```csharp
            // B8 T1: index-tracking loop applies per-follower multiplier
            int dispatched = 0;
            int idx = 0;
            foreach (var acc in rule.FollowerAccounts)
            {
                if (ShouldSkipFollowerDispatch(acc))
                {
                    idx++;
                    continue;
                }

                if (
                    ShouldSkipForReversalGuard(
                        acc,
                        instr,
                        currentAction,
                        lastAction,
                        hasLastDirection
                    )
                )
                {
                    idx++;
                    continue;
                }

                DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
                idx++;
                dispatched++;
            }
            if (dispatched > 0)
                SetLiveEntryDispatched(instrKey, orderId);
```

---

### GROUP D: Lane E (re-arm ATM after partial QX/BE -- fixes BUG2 re-entry path)

**Purpose**: After a partial Quick Exit or Break Even move, a follower may have a remaining
position but no ATM brackets. Lane E detects this state and re-arms the ATM template, preventing
ghost re-entry from bracket-less positions.

#### D1: Fields `_laneEDispatched` (already included in A1 block -- do not add twice)

#### D2: `IsLaneEEligible` + `TryLaneEHook` + `TryLaneEDispatchFollower` + `BuildLaneEKey` methods

Insert as a block AFTER `TryLaneDDispatchFollower` ends (after C2 insertion point):

```csharp
        // Lane E: eligibility predicate for TryLaneEHook. Absorbs all compound boolean guards so
        // TryLaneEHook contains zero || or && operators (JS-013 compliance).
        // CYC=8: entry(1) + if(1) + if+||(2) + if(1) + if+||(2) + if(1). Exactly at budget.
        // JS-001: no throw. JS-021: no lock. JS-002: returns bool, never null.
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook).
        private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)
        {
            // (1): non-Clone mode -> not eligible (Signal/Mirror/Market no-op)
            if (GetCopyMode() != CopyMode.Clone)
                return false;

            // (2): null guard -- || is +1 CYC per JS-013, isolated here intentionally
            if (pos == null || pos.Instrument == null)
                return false;

            // (3): no remaining position -> not a re-arm event (flat or zero-qty update)
            if (pos.Quantity <= 0)
                return false;

            // (4): gate must be closed (false) -- confirms post-QX/BE state.
            // Absent key (first session entry) treated as ready -> returns false -> no-op. Correct.
            // || is +1 CYC per JS-013, isolated here intentionally.
            bool ready;
            if (!_followerSlateReady.TryGetValue(acc.Name, out ready) || ready)
                return false;

            // (5): ATM template must be Named -- Inherit/unconfigured = no-op
            if (!(GetCloneAtmMode(pos.Instrument.FullName) is FollowerAtmMode.Named))
                return false;

            return true;
        }

        // Lane E: re-arm ATM hook. Called from OnFollowerPositionUpdate after TrySetFollowerSlateReady.
        // Fires when follower has a remaining position (post-partial QX/BE) with gate closed and no ATM brackets.
        // CYC=5: (1)IsLaneEEligible gate,(2)dedup check,(3)ok gate-open. All compound guards in IsLaneEEligible.
        // JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
        // JS-023: no UI update -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
        // ASCII-only. Thread: NT8 account background thread (same as OnFollowerPositionUpdate).
        private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)
        {
            // Branch (1): eligibility gate -- all compound guards delegated to IsLaneEEligible
            if (!IsLaneEEligible(acc, pos))
                return;

            // Resolve ATM template and dedup key (assignments only, +0 CYC)
            string instrFullName = pos.Instrument.FullName;
            FollowerAtmMode.Named namedMode =
                GetCloneAtmMode(instrFullName) as FollowerAtmMode.Named;
            string key = acc.Name + "#LANE-E#" + instrFullName;

            // Branch (2): dedup check -- only re-arm once per account+instrument slot
            if (_laneEDispatched.ContainsKey(key))
                return;

            // Dispatch: re-arm ATM bracket for the remaining position
            bool ok = TryLaneEDispatchFollower(acc, pos.Instrument, pos, namedMode, key);

            // Branch (3): gate open -- ATM is now armed; bracket sync is safe to proceed
            if (ok)
                _followerSlateReady[acc.Name] = true;
        }

        // Lane E: per-follower ATM re-arm helper. Keeps TryLaneEHook CYC low.
        // CYC=5: (A) acc null guard, (B) daily cap check, ?: action ternary, (C) SendCopyWithAtm result.
        // JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
        // JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
        // ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook caller).
        private bool TryLaneEDispatchFollower(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            NinjaTrader.Cbi.Position pos,
            FollowerAtmMode.Named namedMode,
            string dedupKey
        )
        {
            // Branch (A): null guard
            if (acc == null)
                return false;

            // Branch (B): daily position cap -- respect existing cap contract
            if (!PassesDailyCapCheck(acc))
                return false;

            // Build signal from follower's remaining position
            OrderAction action = pos.MarketPosition == NinjaTrader.Cbi.MarketPosition.Long
                ? OrderAction.Buy
                : OrderAction.SellShort;
            string syntheticOrderId = "LANE-E#" + acc.Name + "#" + instr.FullName;
            CopySignal sig = CopySignal.Create(
                action,
                OrderType.Limit,
                pos.Quantity,       // remaining quantity (already multiplier-scaled from original entry)
                pos.AveragePrice,   // ATM bracket anchored at original entry price
                syntheticOrderId
            );

            // Branch (C): dispatch ATM entry; mark dedup only on success
            bool ok = SendCopyWithAtm(acc, instr, in sig, namedMode);
            if (ok)
                _laneEDispatched.TryAdd(dedupKey, 0);
            return ok;
        }

        // Lane E: dedup key builder. Isolates the instr null-guard so CancelQxBrackets callers
        // incur +0 CYC from the null check. Called from both CancelQxBrackets overloads.
        // CYC=2: entry(1) + ternary null guard(1). ASCII-only. No lock (JS-021). static: no state.
        private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            return acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty);
        }
```

---

## TICKET BREAKDOWN (for Ph3)

### MANDATORY SECTION IN EVERY TICKET (Ph3 ptt-architect template rule)

Every ticket produced by Ph3 ptt-architect MUST include an **Approved Deletions** section.
If the ticket removes any code, every deleted symbol must be listed with its authorising DW-card.
If the ticket is add-only, the section must say NONE. The verifier will reject any unlisted deletion.

```
### Approved Deletions (mandatory section in every ticket)
If this ticket removes any symbols, list each one here with the DW-card that
authorises the removal. The verifier rejects any deletion not listed here.
| Symbol | File | Line | Authorising DW-card |
|--------|------|------|---------------------|
| (none) | --   | --   | --                  |
```

For B183 specifically: ALL tickets are add-only. Approved Deletions = NONE for all 7 tickets.

---

Suggested ticket split (Ph3 ptt-architect may refine, but these groups are the required scope):

**Ticket 1 -- Slate Gate fields + methods (Group A items A1, A2)**
- Add 3 field declarations (_followerSlateReady, _laneDDispatched, _laneEDispatched)
- Add 5 methods (TryMarkFollowerSlateNotReady, TrySetFollowerSlateReady, IsAccountFlat,
  HasAnyActiveOrders, OnFollowerPositionUpdate)

**Ticket 2 -- Slate Gate call sites + Subscribe/Unsubscribe (Group A items A3, A4, A5)**
- Add TryMarkFollowerSlateNotReady + _laneEDispatched.TryRemove to CancelQxBrackets (both overloads)
- Add TryMarkFollowerSlateNotReady to FlattenOneAccount
- Add gate (1) to FindFollowerBracketOrder (Account overload) -- convert from expr-body to block-body
- Add PositionUpdate subscription to Subscribe() and Unsubscribe()

**Ticket 3 -- Lane D (Group C)**
- Add TryLaneDHook + TryLaneDDispatchFollower methods
- Add TryLaneDHook call to DispatchCopy (after baseQty line)
- Add Lane D skip gate to DispatchToFollower
- Add dispatched counter + SetLiveEntryDispatched to DispatchCopy loop

**Ticket 4 -- Lane E (Group D)**
- Add IsLaneEEligible + TryLaneEHook + TryLaneEDispatchFollower + BuildLaneEKey methods
- (All call sites are already in Ticket 1's OnFollowerPositionUpdate and Ticket 2's CancelQxBrackets)

**Ticket 5 -- FlatGuardLoop (Group B)**
- Add FlatGuardLoop<TAccount> method
- Replace Flatten(Instrument) foreach with FlatGuardLoop call
- Replace Flatten(Account,Instrument) follower foreach with FlatGuardLoop call

**Ticket 6 -- Guardian script + Protocol hardening (non-src)**
- Create scripts/verify_critical_symbols.ps1
- Update AGENTS.md with Ph4b single-hunk ban + deletion audit protocol
- Update docs/brain/regression-investigation-B179-B180.md with B183 resolution status

**Ticket 7 -- TradeCopierPanel.cs: BUG3 visual + 16 unauthorised removals**

Source of truth for all bodies: `git show f01b5155:src/PropTraderTools/TradeCopierPanel.cs`

Sub-scope 7A (visual fix -- P2, low risk, narrow):
- `BuildInstrRow()` at HEAD line ~1183: set `_instr2tBtn.Background = BrushTeal` and
  `_instr2tBtn.Foreground = System.Windows.Media.Brushes.White` (AFTER SetResourceReference).
  Same for `_instrQAll2tBtn`. Copy verbatim from f01b5155 lines 1290-1315.
- Specs tuple Bg field: change all 4 teal-button entries from `BrushInactive` to `BrushTeal`
  (BE, BE ALL, Quick, Quick ALL at HEAD lines 1079, 1089, 1099, 1109).
- In the specs foreach loop: change `btn.Foreground = BrushTeal` to
  `btn.Foreground = System.Windows.Media.Brushes.White` when `s.Teal == true` (HEAD line 1149).
- In the specs foreach loop: change `btn.Background = s.Bg` to
  `btn.Background = isTeal ? BrushTeal : BrushInactive` (HEAD line 1153).

Sub-scope 7B (BE arm/disarm P1 events -- medium risk):
- Re-add `OnPendingBeArmedDispatch(string instr, string accountName)` method
  (f01b5155 lines ~1350-1365). Wire in `SubscribeEngineEvents` / `OnLoaded`.
- Re-add `OnGlobalBeAllDisarmed()` method (f01b5155 lines ~1366-1370).
- Re-add `UpdateBeAllVisuals(BeState state)` method (f01b5155 lines ~1373-1395).
- Re-add `DisarmBeAllOnFlat(bool hasPosition)` method (f01b5155 lines ~1655-1665).
- Re-add `PendingBeArmed` and `GlobalBeAllDisarmed` event subscriptions.

Sub-scope 7C (structural extraction methods -- P3, low functional risk):
- Re-add `BuildSingleButtonCluster(...)` extracted method (f01b5155 lines ~1215-1260).
  This is optional if 7A correctly fixes the inline loop. If re-added, update the
  foreach loop to call `BuildSingleButtonCluster(s.Content, s.Teal, ...)`.
- Re-add `BuildQuickT3HiddenRow(root)` extracted method (f01b5155 lines ~1276-1286).
- Re-add `SubscribeEngineEvents()`, `BuildAllAccountsList()`,
  `RegisterAndInitializeModules()`, `WireLeaderOrderHandlers()` extraction methods
  (f01b5155 lines ~890-960). These are pure structural extractions; no behaviour change.

NOTE: `BuildBuyToggleButton`, `BuildSellToggleButton`, `BuildArmButton`,
`BuildClickTraderCancelButton` were extracted helpers. If their logic is correctly
inlined in HEAD `BuildClickTraderRow`, they may be acceptable as-is. Architect should
verify the inlined code matches f01b5155 before deciding whether to re-extract.

**Constraint for Ticket 7**: B180-2 approved removals (`ResetBeStateOnFlat`, `ExecuteBeIdle`,
`ExecuteBeArmed`, `UpdateBeVisuals`, `OnBeConnected`) must NOT be re-introduced.
Check `docs/brain/B180-orchestrator-prompt.md` approved removal list before restoring any symbol.

---

## GUARDIAN SCRIPT (create as scripts/verify_critical_symbols.ps1)

This script MUST be run at the end of Ph4a and start of Ph4b for every ticket.

```powershell
# verify_critical_symbols.ps1
# Guardian: all critical B183 symbols must be present in CopyEngine.cs + TradeCopierPanel.cs
# Run: powershell -File .\scripts\verify_critical_symbols.ps1
# Exit 0 = PASS. Exit 1 = FAIL (lists missing symbols).

$engineFile = "src/PropTraderTools/CopyEngine.cs"
$panelFile  = "src/PropTraderTools/TradeCopierPanel.cs"
foreach ($f in @($engineFile, $panelFile)) {
    if (-not (Test-Path $f)) { Write-Error "$f not found"; exit 1 }
}

$engine = Get-Content $engineFile -Raw
$panel  = Get-Content $panelFile  -Raw

# B183-specific symbols (re-introduced this pipeline)
$requiredEngine_B183 = @(
    "_followerSlateReady",
    "TryMarkFollowerSlateNotReady",
    "TrySetFollowerSlateReady",
    "IsAccountFlat",
    "HasAnyActiveOrders",
    "OnFollowerPositionUpdate",
    "FlatGuardLoop",
    "TryLaneDHook",
    "_laneDDispatched",
    "TryLaneDDispatchFollower",
    "TryLaneEHook",
    "_laneEDispatched",
    "TryLaneEDispatchFollower",
    "BuildLaneEKey",
    "IsLaneEEligible",
    "PositionUpdate"
)

# Permanent guardian symbols -- must ALWAYS be present regardless of any future pipeline
# Any pipeline that removes one of these requires Director explicit approval + DW-card
$requiredEngine_permanent = @(
    "FindFollowerBracketOrder",
    "EvictDedup",
    "TryCancelFollowerEntries",
    "CancelScopedFollowerEntries",
    "IsLiveEntryBlocked",
    "IsReversalToFlatFollower",
    "ShouldSkipForReversalGuard",
    "TryDispatchLeaderFlat",
    "DispatchCopy",
    "OnOrderUpdate",
    "Subscribe",
    "Unsubscribe"
)

$requiredEngine = $requiredEngine_B183 + $requiredEngine_permanent

$requiredPanel = @(
    "OnPendingBeArmedDispatch",
    "OnGlobalBeAllDisarmed",
    "UpdateBeAllVisuals",
    "DisarmBeAllOnFlat",
    "PendingBeArmed",
    "GlobalBeAllDisarmed"
)

$missing = @()
foreach ($sym in $requiredEngine) {
    if ($engine -notmatch [regex]::Escape($sym)) { $missing += "CopyEngine: $sym" }
}
foreach ($sym in $requiredPanel) {
    if ($panel -notmatch [regex]::Escape($sym)) { $missing += "TradeCopierPanel: $sym" }
}

# BUG3 visual check: Quick2t must have BrushTeal background (not just border)
if ($panel -match "Quick2t" -and $panel -notmatch "instr2tBtn\.Background\s*=\s*BrushTeal") {
    $missing += "TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)"
}

# Single-hunk ban on CopyEngine.cs
$hunkCount = (git diff HEAD~1 HEAD -- $engineFile 2>$null | Select-String "^@@").Count
if ($hunkCount -eq 1) {
    Write-Error "SINGLE-HUNK BAN: CopyEngine.cs diff is a full-file replace. B183-class regression risk."
    $missing += "[SINGLE-HUNK-REPLACE-DETECTED: CopyEngine.cs]"
}

# Deletion audit: catch unauthorised symbol removals
$deletedSymbols = git diff HEAD~1 HEAD -- $engineFile 2>$null |
    Select-String "^-\s+(private|internal|public)" |
    ForEach-Object { $_.Line.Trim() }
if ($deletedSymbols.Count -gt 0) {
    Write-Warning "DELETION AUDIT: $($deletedSymbols.Count) deleted symbol(s) detected in CopyEngine.cs:"
    $deletedSymbols | ForEach-Object { Write-Warning "  $_" }
    $missing += "[DELETION-AUDIT: $($deletedSymbols.Count) symbols deleted -- each requires DW-card]"
}

# Commit size gate: >300 deleted or added lines requires Director approval
$deletedLines = (git diff HEAD~1 HEAD -- $engineFile 2>$null | Select-String "^-" | Measure-Object).Count
$addedLines   = (git diff HEAD~1 HEAD -- $engineFile 2>$null | Select-String "^\+" | Measure-Object).Count
if ($deletedLines -gt 300 -or $addedLines -gt 300) {
    Write-Warning "COMMIT SIZE GATE: deleted=$deletedLines added=$addedLines on CopyEngine.cs."
    Write-Warning "  >300 lines requires Director explicit approval documented in the ticket."
    $missing += "[COMMIT-SIZE-GATE: del=$deletedLines add=$addedLines -- Director approval required if >300]"
}

if ($missing.Count -gt 0) {
    Write-Error "GUARDIAN FAIL -- missing symbols and/or gate violations:"
    $missing | ForEach-Object { Write-Error "  - $_" }
    exit 1
}

Write-Host "GUARDIAN PASS -- all symbols present. Hunk=$hunkCount Del=$deletedLines Add=$addedLines"
exit 0
```

---

## Ph4b VERIFIER GATE CHECKLIST (MANDATORY -- run for EVERY ticket before VERIFY_PASS)

This checklist is NOT optional. ptt-verifier MUST complete every item before issuing
`VERIFY_PASS: YES` for any ticket that touches `CopyEngine.cs` or `TradeCopierPanel.cs`.
If any item fails → issue `VERIFY_PASS: NO` immediately and report to orchestrator.

### Gate 1 -- write_file ban check
```powershell
# Count hunks in the last commit for CopyEngine.cs
(git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@" | Measure-Object).Count
```
Expected: **Count > 1** (targeted apply_diff edits produce multiple hunks).
If Count = 1 → **VERIFY_FAIL: full-file replace detected. Constraint 8 violated.**

### Gate 2 -- Deletion audit
```powershell
# List every deleted symbol declaration in CopyEngine.cs
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs |
    Select-String "^-\s+(private|internal|public)" |
    ForEach-Object { $_.Line.Trim() }
```
For each deleted symbol: confirm it appears in the ticket's **Approved Deletions** table.
If any deleted symbol has no DW-card → **VERIFY_FAIL: unauthorised deletion.**
For B183 all tickets are add-only → expected output: zero deleted symbols.

### Gate 3 -- Deleted line count (commit size)
```powershell
(git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-" | Measure-Object).Count
(git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\+" | Measure-Object).Count
```
If either exceeds 300 without Director approval in the ticket → **VERIFY_FAIL: commit size gate.**
For B183 add-only tickets: deletion count should be near 0, addition count may exceed 300
(adding 16 symbols). Director pre-approval is granted in this prompt for B183 add-only scope.

### Gate 4 -- Guardian script
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
Expected: exit 0 with `GUARDIAN PASS`. If exit 1 → **VERIFY_FAIL.**
Note: script does not exist until Ticket 6 completes. For Tickets 1-5, run the symbol
checks manually using the `$requiredEngine` list from the script definition above.

### Gate 5 -- ptt-sync
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
Must run after every .cs edit and report 0 errors before VERIFY_PASS.

All 5 gates must show PASS before proceeding to SIM GATE / bug verification below.

---

## SIM GATE CRITERIA (Ph4b verification)

Ph4b ptt-verifier MUST confirm ALL THREE bugs are architecturally resolved by tracing call paths:

**BUG1 verification** (cancel+reorder not copying):
1. Confirm `TryMarkFollowerSlateNotReady` is called in `CancelQxBrackets` (both overloads)
2. Confirm gate `(1)` exists in `FindFollowerBracketOrder(Account, ...)` overload
3. Confirm `TrySetFollowerSlateReady` is called from `OnFollowerPositionUpdate`
4. Confirm `acc.PositionUpdate += OnFollowerPositionUpdate` is in `Subscribe()`
5. Confirm `TryLaneDHook` is called from `DispatchCopy` after `baseQty`
6. Confirm Lane D skip gate exists in `DispatchToFollower`
7. Trace: cancel -> `CancelQxBrackets` -> `TryMarkFollowerSlateNotReady` (gate=false)
   -> `FindFollowerBracketOrder` returns null (correct)
   -> NT8 PositionUpdate fires flat -> `OnFollowerPositionUpdate` -> `TrySetFollowerSlateReady` (gate=true)
   -> next order -> `DispatchCopy` -> `TryLaneDHook` (if Named-ATM) or `DispatchToFollower` (raw)
   -> copy dispatched (BUG1 FIXED)

**BUG2 verification** (re-entry after close):
1. Confirm `TryMarkFollowerSlateNotReady` is called in `FlattenOneAccount`
2. Confirm `IsLaneEEligible` checks gate is CLOSED (not ready) before re-arming
3. Confirm `TryLaneEHook` is called from `OnFollowerPositionUpdate`
4. Trace: close -> `FlattenOneAccount` -> `TryMarkFollowerSlateNotReady` (gate=false)
   -> `FindFollowerBracketOrder` returns null -> no bracket sync -> no re-entry (BUG2 path 1 FIXED)
5. Trace: partial QX/BE -> position remains -> `OnFollowerPositionUpdate` fires ->
   `TrySetFollowerSlateReady` (not flat, returns early, gate stays false) ->
   `TryLaneEHook` -> `IsLaneEEligible` (gate=false, eligible) -> re-arms ATM bracket (BUG2 FIXED)

**BUG3 verification** (Quick2t / QAll2t colour + BE arm events):
1. Open TradeCopierPanel.cs `BuildInstrRow()` -- confirm:
   - `_instr2tBtn.Background = BrushTeal` is set AFTER `SetResourceReference`
   - `_instr2tBtn.Foreground = System.Windows.Media.Brushes.White` is set (not `BrushTeal`)
   - Same for `_instrQAll2tBtn`
2. Open specs tuple -- confirm BE, BE ALL, Quick, Quick ALL entries have `BrushTeal` as Bg field
3. In the foreach cluster loop -- confirm `btn.Foreground = Brushes.White` (not `BrushTeal`)
   when `s.Teal == true`, and `btn.Background = isTeal ? BrushTeal : BrushInactive`
4. Confirm `OnPendingBeArmedDispatch` exists and is subscribed to `_engine.PendingBeArmed`
5. Confirm `OnGlobalBeAllDisarmed` exists and is subscribed to `_engine.GlobalBeAllDisarmed`
6. Confirm `UpdateBeAllVisuals(BeState.Idle)` is called from `OnGlobalBeAllDisarmed`
7. Confirm `DisarmBeAllOnFlat(hasPosition)` is called from the position-state update path

---

## PROTOCOL HARDENING (add to AGENTS.md in Ph5)

Add to AGENTS.md Section 2 (Architectural Mandates):

```
- **Ph4b Deletion Audit (MANDATORY)**: Before VERIFY_PASS, engineer MUST run:
  `git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"`
  Every deleted symbol must have an authorising DW-card in the ticket. Zero tolerance.

- **Single-Hunk Ban**: `(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1` means
  a full-file replace was committed. This is ALWAYS a VERIFY_FAIL. Requires Director re-review.

- **Guardian Symbol Script**: After every .cs edit involving CopyEngine.cs, run:
  `powershell -File .\scripts\verify_critical_symbols.ps1`
  PASS required before VERIFY_PASS may be issued.

- **Commit Size Gate**: >300 changed lines on CopyEngine.cs requires Director explicit approval
  before Ph4b may issue VERIFY_PASS.
```

---

## DEFERRED BACKLOG (for Ph5 06-deferred-backlog.md)

Items out of B183 scope but should be tracked:

1. **xUnit test coverage for OnFollowerPositionUpdate**: TrySetFollowerSlateReady, IsAccountFlat,
   HasAnyActiveOrders are all pure static/instance methods -- ideal for property-based tests.
   Deferred to B184.

2. **xUnit test for FlatGuardLoop**: The delegate-injection pattern makes it directly testable
   without NT8 runtime. Deferred to B184.

3. **IsLaneEEligible CYC=8 at budget**: Any future modification that adds a branch will breach
   JS-066 (CYC <= 8). Note in deferred that extraction of one guard into a helper should be
   planned if a branch is ever needed.

4. **`_laneDDispatched` lifetime cleanup**: Currently grows for the session duration. A per-session
   or per-instrument cleanup pass would prevent unbounded growth in long sessions. Low priority.
   Deferred to B185.

---

## ARTIFACTS CHECKLIST (Ph5 must confirm all exist)

- [ ] `docs/brain/B183/02-architecture-plan.md` (Ph1 output)
- [ ] `docs/brain/B183/02-plan-review.md` with `REVIEW_PASS: YES` (Ph2 output)
- [ ] `docs/brain/B183/04-tickets.md` (Ph3 output)
- [ ] `docs/brain/B183/04-ticket-review.md` with `TICKET_REVIEW_PASS: YES` (Ph3.5 output)
- [ ] `docs/brain/B183/ticket-1-completion.md` through `ticket-6-completion.md` (Ph4a output)
- [ ] `docs/brain/B183/ticket-1-verification.md` through `ticket-6-verification.md` with `VERIFY_PASS: YES` (Ph4b output)
- [ ] `scripts/verify_critical_symbols.ps1` created and tested (Ticket 6)
- [ ] `powershell -File .\scripts\ptt-sync.ps1` run after all .cs edits
- [ ] `powershell -File .\scripts\verify_critical_symbols.ps1` passes (all 16 symbols present)
- [ ] `docs/brain/B183/05-final-review.md` with `FINAL_PASS: YES` (Ph5 output)
- [ ] `docs/brain/B183/06-deferred-backlog.md` (Ph5 output)

---

## POST-B183 VALIDATION ($continue prompt)

After `FINAL_PASS: YES` is issued, paste the following into the copier-spec session:

```
$continue B183 complete.

Run the exact-16 symbol validation:

$symbols = @("_followerSlateReady","TryMarkFollowerSlateNotReady","TrySetFollowerSlateReady",
"IsAccountFlat","HasAnyActiveOrders","OnFollowerPositionUpdate","FlatGuardLoop",
"TryLaneDHook","_laneDDispatched","TryLaneDDispatchFollower","TryLaneEHook",
"_laneEDispatched","TryLaneEDispatchFollower","BuildLaneEKey","IsLaneEEligible","PositionUpdate")
$head = Get-Content src/PropTraderTools/CopyEngine.cs -Raw
$missing = $symbols | Where-Object { $head -notmatch [regex]::Escape($_) }
if ($missing) { Write-Error "STILL MISSING: $($missing -join ', ')" } else { Write-Host "ALL 16 PRESENT -- regression resolved" }

Then confirm:
1. verify_critical_symbols.ps1 runs with exit 0
2. ptt-sync.ps1 ran after last .cs edit
3. Build compiles (no CS errors)
4. BUG1 call trace verified (cancel -> gate=false -> PositionUpdate -> gate=true -> copy dispatched)
5. BUG2 call trace verified (flatten -> gate=false -> no re-entry; partial QX -> Lane E re-arms)
6. 05-final-review.md shows FINAL_PASS: YES
7. Update docs/brain/regression-investigation-B179-B180.md status to RESOLVED
8. Confirm spec HTML is updated with B183 scope and resolution
```

---

END OF B183 ORCHESTRATOR PROMPT
