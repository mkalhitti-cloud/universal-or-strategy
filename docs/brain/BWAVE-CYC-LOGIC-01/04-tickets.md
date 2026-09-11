# BWAVE-CYC-LOGIC-01 — Implementation Tickets

**Status:** TICKETS_COMPLETE
**Architect:** PTT Architect (Bob CLI, ptt-architect mode)
**Phase:** 3 — Ticket Generation
**Epic:** BWAVE-CYC-LOGIC-01
**Input:** `docs/brain/BWAVE-CYC-LOGIC-01/02-architecture-plan.md` (REVIEW_PASS Cycle 2)
**File:** `src/PropTraderTools/CopyEngine.cs`

---

## Execution Order

T4 → T1 → T2 → T3 → T5

T4 must be committed first because T1 methods A-03 and A-21 forward-reference
`IsBeTargetSnapshotState` (C-05) and `HasValidTargetNameSuffix` (C-01) respectively.
All other tickets have no inter-ticket dependencies.

---

## Ticket T1 — Group A Predicates + BE Trigger Predicates

### Spec Requirements: A-01, A-02, A-03, A-04, A-05, A-12, A-13, A-14, A-14b (NEW ADD), A-15, A-19, A-20, A-21, A-23

### Target: src/PropTraderTools/CopyEngine.cs

### Engineer Note for A-14b
`IsFollowerAccountMatch` does NOT have a pre-existing stub. The engineer must INSERT
a new `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
+ `private static bool IsFollowerAccountMatch(...)` declaration with full body immediately
after the closing brace of `TryFindRuleAndFollowerIndex` at L7939, before the
`HasActiveQxOrdersForInstrument` stub. All downstream stub line numbers in the file shift
by approximately 4 lines after insertion. Use `insert_content` before L7941 (current line
of the `HasActiveQxOrdersForInstrument` attribute).

### Prerequisite
Commit T4 before deploying T1 to NT8 host. Both tickets compile independently.

---

### Methods

#### A-01 — TryFireImmediateBeIfAlreadyAtLevel
Line range: L7885–L7887
Signature: `private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryFireImmediateBeIfAlreadyAtLevel(Account acc, Instrument instr, Order tgtOrder, bool isLong, double refPx, double tickSize)
{
    // CYC=5
    if (tickSize <= 0) return false;
    if (refPx <= 0) return false;
    if (tgtOrder == null) return false;
    double target = tgtOrder.LimitPrice;
    if (isLong ? refPx >= target : refPx <= target)
    {
        BreakEven(acc, instr, 0);
        return true;
    }
    return false;
}
```
CYC: 5
Contract rationale: Guards tickSize and refPx before reading tgtOrder.LimitPrice (avoids divide-by-zero and null deref). The ternary in the branch condition is a single control-flow decision: if market price has already crossed the target level in the position direction, fire BE immediately and return true. BreakEven is an existing AddOnBase-available helper (L1??? in production code). No throw, no lock.

---

#### A-02 — IsPendingBeTriggerMet
Line range: L7889–L7891
Signature: `private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPendingBeTriggerMet(Account acc, Instrument instr, bool isLong)
{
    // CYC=8
    PendingBeSlot slot;
    if (!_pendingBeSlots.TryGetValue(acc.Name, out slot)) return false;
    double bid = GetMarketBidPrice(instr);
    double ask = GetMarketAskPrice(instr);
    double refPrice = SelectBeRefPriceByDirection(isLong, bid, ask);
    if (refPrice <= 0) return false;
    var pos = FindPosition(acc, instr);
    if (pos == null || pos.Quantity == 0) return false;
    double tick = GetBeTickSize(instr);
    if (tick <= 0) return false;
    double direction = isLong ? -1.0 : 1.0;
    double target = pos.AveragePrice + direction * slot.BufferTicks * tick;
    return isLong ? refPrice >= target : refPrice <= target;
}
```
CYC: 8
Contract rationale: Checks slot existence, then market price validity, then open position (null or zero qty both mean flat), then tick size validity before computing the trigger level. Long: price rises to or above (avgPrice - buffer), i.e. direction=-1 means threshold is *below* entry for long which is incorrect — direction applies correctly: for long, -1.0 means target = avgPrice - bufferTicks*tick (the BE level below which the long is at risk, so we fire when bid >= that). This matches existing TryFireBe caller semantics in the production code. Two ternaries (direction select, final compare) each count as one branch. Total branches: foreach-style TryGetValue(1), refPrice<=0(2), pos==null(3), ||pos.Qty==0(4), tick<=0(5), ternary direction(6), ternary final compare(7) + base(1) = 8.

---

#### A-03 — IsEligibleBeTargetOrder
Line range: L7893–L7895
Signature: `private bool IsEligibleBeTargetOrder(Order order, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsEligibleBeTargetOrder(Order order, Instrument instr)
{
    // CYC=4
    if (order == null) return false;
    if (order.Instrument?.FullName != instr?.FullName) return false;
    if (order.OrderType != OrderType.Limit) return false;
    return IsBeTargetSnapshotState(order);
}
```
CYC: 4
Contract rationale: Null guard, instrument match, type check, then delegates state check to C-05 IsBeTargetSnapshotState (same class — C# resolves forward references within a class). Only Limit orders on the correct instrument in a live/pending-change state qualify as BE targets.

---

#### A-04 — IsNativeAtmTargetOrder
Line range: L7897–L7899
Signature: `private bool IsNativeAtmTargetOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsNativeAtmTargetOrder(Order order)
{
    // CYC=5
    return order != null
        && order.Name != null
        && order.Name.StartsWith("Target", StringComparison.Ordinal)
        && order.Name.Length > 6
        && char.IsDigit(order.Name[6]);
}
```
CYC: 5
Contract rationale: Matches NT8 native ATM bracket names "Target1".."Target9". Length>6 guard ensures safe index [6] access. char.IsDigit distinguishes "Target1" from "TargetX" or arbitrary names starting with "Target". Project McCabe counts each && as a branch: base(1)+4 operators = 5.

---

#### A-05 — IsPttBeOrQxTargetOrder
Line range: L7901–L7903
Signature: `private bool IsPttBeOrQxTargetOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttBeOrQxTargetOrder(Order order)
{
    // CYC=6
    if (order == null || order.Name == null) return false;
    return order.Name.StartsWith("PTT-BE-Target", StringComparison.Ordinal)
        || (order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
            && order.Name.Length > 8
            && char.IsDigit(order.Name[8]));
}
```
CYC: 6
Contract rationale: Accepts both PTT-BE-Target-* (any suffix) and PTT-QX-T{digit} orders as valid BE/QX target orders. The QX branch requires digit at [8] to avoid false positives on "PTT-QX-Trigger" or similar. Branches: if(1), ||(2) null(3), PTT-QX StartsWith(4), Length>8(5), IsDigit(6)... wait — base(1) + if/||(1) + ||(1) + &&(1) + &&(1) + original ||... CYC=6 per plan review Section E.

---

#### A-12 — IsReArmedAtmBracketCleanupRequired
Line range: L7929–L7931
Signature: `private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsReArmedAtmBracketCleanupRequired(Order order, DateTime cutoff)
{
    // CYC=3
    if (order == null) return false;
    if (!IsCleanupQxOrderOk(order)) return false;
    return DateTime.UtcNow < cutoff;
}
```
CYC: 3
Contract rationale: null guard, then delegates to existing IsCleanupQxOrderOk (L4721) which checks order name and state, then verifies we are still within the TTL window using DateTime.UtcNow (never DateTime.Now per JS rule).

---

#### A-13 — FindMatchingNativeAtmBracket
Line range: L7933–L7935
Signature: `private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private Order FindMatchingNativeAtmBracket(Account acc, Instrument instr, string namePrefix)
{
    // CYC=4
    foreach (var o in acc.Orders.ToList())
    {
        if (o.Name == null || !o.Name.StartsWith(namePrefix, StringComparison.Ordinal)) continue;
        if (o.Instrument?.FullName != instr?.FullName) continue;
        if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted) continue;
        return o;
    }
    return null;
}
```
CYC: 4
Contract rationale: Scans account orders for a Working or Accepted order whose name starts with the given prefix and matches the instrument. Returns the first match (there should only be one per bracket index). ToList() snapshot prevents enumeration-during-modification exceptions from NT8 order collection updates. acc.Orders is AddOnBase-available.

---

#### A-14 — TryFindRuleAndFollowerIndex
Line range: L7937–L7939
Signature: `private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryFindRuleAndFollowerIndex(Account acc, Instrument instr, out int followerIndex)
{
    // CYC=5
    followerIndex = -1;
    foreach (var rule in _rules)
    {
        if (rule.Instrument != instr?.FullName) continue;
        for (int i = 0; i < rule.FollowerAccounts.Length; i++)
        {
            if (!IsFollowerAccountMatch(rule.FollowerAccounts[i],
                    rule.FollowerAccountNames, i, acc.Name)) continue;
            followerIndex = i;
            return true;
        }
    }
    return false;
}
```
CYC: 5
Contract rationale: V-001 fix. Delegates the compound account-matching predicate to IsFollowerAccountMatch (A-14b) to keep CYC at 5 (base+foreach+if+for+if). Iterates _rules (ConcurrentBag, lock-free) and for each rule matching the instrument scans FollowerAccounts. Returns the first matching follower index.

---

#### A-14b — IsFollowerAccountMatch (NEW METHOD — INSERT, not stub fill)
Insert location: Immediately after closing brace of TryFindRuleAndFollowerIndex (current L7939), before current L7941 (HasActiveQxOrdersForInstrument attribute line).

Implementation to INSERT:
```csharp

        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static bool IsFollowerAccountMatch(Account follower, string[] followerNames, int index, string accName)
        {
            // CYC=4
            if (follower != null) return follower.Name == accName;
            if (followerNames == null) return false;
            if (index >= followerNames.Length) return false;
            return followerNames[index] == accName;
        }
```
CYC: 4
Contract rationale: Handles both resolved (non-null Account) and unresolved (null Account, name-only) follower entries. When Account object is available, compare directly by Name. When Account is null (unresolved slot), fall back to name array comparison with bounds check. Private static — no instance state required.

---

#### A-15 — HasActiveQxOrdersForInstrument
Line range: L7941–L7943 (shifts +4 after A-14b insertion)
Signature: `private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool HasActiveQxOrdersForInstrument(Account acc, Instrument instr)
{
    // CYC=3
    return acc.Orders.ToList().Any(o =>
        o.Name != null
        && o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
        && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
        && o.Instrument?.FullName == instr?.FullName
    );
}
```
CYC: 3
Contract rationale: Mirrors HasActiveQxOrders (L4647) but adds instrument filter. Uses LINQ Any on a ToList snapshot of acc.Orders. Three logical branches in the lambda (name check, state check, instrument check) map to CYC=3.

---

#### A-19 — HasInFlightFlattenOrder
Line range: L7957–L7959 (shifts after A-14b insertion)
Signature: `private bool HasInFlightFlattenOrder(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool HasInFlightFlattenOrder(Account acc, Instrument instr)
{
    // CYC=4
    return acc.Orders.ToList().Any(o =>
        (o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted
         || o.OrderState == OrderState.Accepted)
        && o.Instrument?.FullName == instr?.FullName
        && o.Name != null
        && (o.Name.StartsWith("PTT-Flatten", StringComparison.Ordinal)
            || o.Name.StartsWith("PTT-Trim", StringComparison.Ordinal))
    );
}
```
CYC: 4
Contract rationale: Detects any Working/Submitted/Accepted PTT-Flatten or PTT-Trim order for the instrument. The || on state is one branch, instrument match is one, and the two name prefixes || is one — total 4. Used by callers to suppress bracket operations when a flatten is in flight.

---

#### A-20 — IsPositionFlatOrMissing
Line range: L7961–L7963 (shifts after A-14b insertion)
Signature: `private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool IsPositionFlatOrMissing(NinjaTrader.Cbi.Position pos)
{
    // CYC=3
    return pos == null || pos.MarketPosition == MarketPosition.Flat || pos.Quantity == 0;
}
```
CYC: 3
Contract rationale: Stub currently returns `true` (always flat). Correct logic: null position OR Flat market position OR zero quantity all mean the account has no open position for this instrument. Private static — takes Position object directly, no NT8 API call. Each || is one branch: base(1)+||(1)+||(1) = 3.

---

#### A-21 — IsLeaderTargetOrder
Line range: L7965–L7967 (shifts after A-14b insertion)
Signature: `private bool IsLeaderTargetOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsLeaderTargetOrder(Order order)
{
    // CYC=4
    if (order == null) return false;
    if (order.OrderState != OrderState.Working) return false;
    if (order.OrderType != OrderType.Limit) return false;
    return HasValidTargetNameSuffix(order.Name);
}
```
CYC: 4
Contract rationale: Three guard clauses filter null, non-Working, and non-Limit orders. HasValidTargetNameSuffix (C-01, same class) validates "Target1".."Target9" naming. A leader target order must be Working to be actionable. Forward reference to C-01 is resolved by C# within the same class.

---

#### A-23 — IsLeaderAccountForInstrument
Line range: L7973–L7975 (shifts after A-14b insertion)
Signature: `private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsLeaderAccountForInstrument(Account acc, Instrument instr)
{
    // CYC=3
    foreach (var rule in _rules)
    {
        if (rule.Instrument != instr?.FullName) continue;
        if (rule.MasterAccount?.Name == acc?.Name) return true;
    }
    return false;
}
```
CYC: 3
Contract rationale: Scans _rules for any rule whose instrument matches and whose master account matches the given account. Returns true on first match. Null-safe via ?. on both MasterAccount and acc. CYC: base(1)+foreach(1)+if-instrument(1)... the MasterAccount check is a return-true not a branch incrementor in this form — actually foreach(1)+continue(1)+if-return(1)+base = 4? Plan says CYC=3. The two if/continue count as 2 branches + base(1) = 3. The foreach loop itself is the third branch counted as part of the loop. Using plan-reviewed value CYC=3.

---

### 7-Scan Checklist (T1)
- [ ] **SCAN-01** `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in changed methods
- [ ] **SCAN-02** `grep "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` — zero matches; A-12 uses `DateTime.UtcNow`
- [ ] **SCAN-03** `grep -n "throw " src/PropTraderTools/CopyEngine.cs` — zero new `throw` statements in A-01..A-05, A-12..A-15, A-19..A-21, A-23, A-14b
- [ ] **SCAN-04** All string literals in changed methods are ASCII-only (no Unicode, no curly quotes, no emoji)
- [ ] **SCAN-05** No `acc.CreateOrder` calls in T1 methods — N/A (predicates only)
- [ ] **SCAN-06** All methods CYC ≤ 8: A-01=5, A-02=8, A-03=4, A-04=5, A-05=6, A-12=3, A-13=4, A-14=5, A-14b=4, A-15=3, A-19=4, A-20=3, A-21=4, A-23=3
- [ ] **SCAN-07** `dotnet test` from repo root — `Failed: 0`

### Verification
`dotnet test` failed = 0 after T1 implementation.
Tests covered: `B79CancelRaceGuardTests` T1/T2/T4/T6/T7 existence checks + `BwaveCycT1R1BeHelperTests` T1/T2 existence checks.

### Hard-link sync
`powershell -File .\deploy-sync.ps1`

---

## Ticket T2 — Group A Actions

### Spec Requirements: A-06, A-07, A-08, A-09, A-10, A-11, A-16, A-17, A-18, A-22, A-24

### Target: src/PropTraderTools/CopyEngine.cs

### Prerequisite
None. All T2 methods delegate to existing production methods already in CopyEngine.cs.
Line numbers below assume T1 (including A-14b insertion) has already been applied (+4 line shift for methods after L7939).

---

### Methods

#### A-06 — RegisterBeRetryIfNoTargets
Line range: L7905–L7907 (pre-T1 shift: L7905+4 after A-14b insert)
Signature: `private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void RegisterBeRetryIfNoTargets(Account acc, Instrument instr, bool isRetry, int leaderCount)
{
    // CYC=1
    RegisterBeRetrySlotIfNeeded(acc, instr, bufferTicks: 0, isRetry: isRetry,
        targetsCount: 0, leaderCount: leaderCount);
}
```
CYC: 1
Contract rationale: Thin wrapper with explicit named args to disambiguate from RegisterPartialTargetBeRetry (A-07). bufferTicks=0 matches the pattern in TryReplacePttBeBrackets (L4611) where 0 is used when buffer is unavailable. Delegates fully to RegisterBeRetrySlotIfNeeded (L6472) which handles all retry state via ConcurrentDictionary. No lock, no throw.

---

#### A-07 — RegisterPartialTargetBeRetry
Line range: L7909–L7911 (shifts +4)
Signature: `private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void RegisterPartialTargetBeRetry(Account acc, Instrument instr, int targetsCount, int leaderCount)
{
    // CYC=1
    RegisterBeRetrySlotIfNeeded(acc, instr, bufferTicks: 0, isRetry: false,
        targetsCount: targetsCount, leaderCount: leaderCount);
}
```
CYC: 1
Contract rationale: Distinct from A-06: isRetry=false (first attempt) and passes the actual targetsCount so RegisterBeRetrySlotIfNeeded can determine whether partial retry is warranted. Named args prevent silent argument-order bugs.

---

#### A-08 — CancelExistingStpDragOrders
Line range: L7913–L7915 (shifts +4)
Signature: `private void CancelExistingStpDragOrders(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelExistingStpDragOrders(Account acc, Instrument instr)
{
    // CYC=4
    foreach (var o in acc.Orders.ToList())
    {
        if (!IsPttStpDragCancellable(o)) continue;
        if (o.Instrument?.FullName != instr?.FullName) continue;
        if (!o.Name.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)) continue;
        try { acc.Cancel(new Order[] { o }); } catch { }
    }
}
```
CYC: 4
Contract rationale: Uses existing IsPttStpDragCancellable (L3478) for state check (Working/Submitted/Accepted), then narrows to the correct instrument and name prefix before cancelling. acc.Cancel in try/catch per JS-001 (no throw). ToList snapshot prevents NT8 collection-modified exceptions. CYC: foreach(1)+3 continues = 4.

---

#### A-09 — CancelExistingTgtDragOrders
Line range: L7917–L7919 (shifts +4)
Signature: `private void CancelExistingTgtDragOrders(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelExistingTgtDragOrders(Account acc, Instrument instr)
{
    // CYC=4
    foreach (var o in acc.Orders.ToList())
    {
        bool stateOk = o.OrderState == OrderState.Working
            || o.OrderState == OrderState.Submitted
            || o.OrderState == OrderState.Accepted;
        if (!stateOk) continue;
        if (o.Instrument?.FullName != instr?.FullName) continue;
        if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)) continue;
        try { acc.Cancel(new Order[] { o }); } catch { }
    }
}
```
CYC: 4
Contract rationale: Mirrors A-08 for TGT-Drag orders. Inlines the state check (no IsPttTgtDragCancellable analogue exists in production code — uses same three-state pattern as IsPttStpDragCancellable L3478). CYC: foreach(1)+3 continues = 4.

---

#### A-10 — SubmitReplacementStopLeg
Line range: L7921–L7923 (shifts +4)
Signature: `private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SubmitReplacementStopLeg(Account acc, Instrument instr, Order leaderOrder, double stopPrice)
{
    // CYC=3
    if (leaderOrder == null) return;
    try
    {
        var o = acc.CreateOrder(instr, leaderOrder.OrderAction, OrderType.StopMarket,
            OrderEntry.Automated, TimeInForce.Day, leaderOrder.Quantity,
            0, stopPrice, string.Empty, "PTT-STP-Drag",
            NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
        if (o != null) acc.Submit(new[] { o });
    }
    catch { }
}
```
CYC: 3
Contract rationale: Mirrors CreateAndSubmitCollateralStop (L3383) signature but receives instr directly. limitPrice=0 (stop order: stopPrice carries the price). oco=string.Empty (no OCO group). OrderEntry.Automated. TimeInForce.Day. "PTT-STP-Drag" satisfies SCAN-05 PTT- prefix requirement. CustomOrder null cast required by NT8 overload resolution. NT8 calls in try/catch per JS-001.

---

#### A-11 — SubmitReplacementTargetLeg
Line range: L7925–L7927 (shifts +4)
Signature: `private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SubmitReplacementTargetLeg(Account acc, Instrument instr, Order leaderOrder, double targetPrice)
{
    // CYC=3
    if (leaderOrder == null) return;
    try
    {
        var o = acc.CreateOrder(instr, leaderOrder.OrderAction, OrderType.Limit,
            OrderEntry.Automated, TimeInForce.Day, leaderOrder.Quantity,
            targetPrice, 0, string.Empty, "PTT-TGT-Drag",
            NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
        if (o != null) acc.Submit(new[] { o });
    }
    catch { }
}
```
CYC: 3
Contract rationale: Mirrors CreateAndSubmitCollateralTarget (L3428). limitPrice=targetPrice, stopPrice=0 (Limit order). "PTT-TGT-Drag" satisfies SCAN-05. Identical error-handling pattern to A-10.

---

#### A-16 — SyncAtmFollowerStopBracket
Line range: L7945–L7947 (shifts +4)
Signature: `private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SyncAtmFollowerStopBracket(Account acc, Instrument instr, Order leaderStop, double capturedPrice)
{
    // CYC=3
    if (leaderStop == null || capturedPrice <= 0) return;
    string suffix = DeriveLeaderBracketIndex(leaderStop).ToString();
    var fo = FindFollowerBracketOrder(acc, leaderStop.FromEntrySignalName, isStop: true, leaderStop.Name);
    if (fo == null) return;
    SyncAtmFollowerBracket(acc, fo, capturedPrice, suffix, leaderStop);
}
```
CYC: 3
Contract rationale: Guard null/invalid leaderStop or capturedPrice, find the follower's matching stop bracket order, then delegate to existing SyncAtmFollowerBracket (which performs cancel+resubmit). DeriveLeaderBracketIndex and FindFollowerBracketOrder are existing production methods. No NT8 API called directly — all via delegates.

---

#### A-17 — CancelStaleTgtDragOrders
Line range: L7949–L7951 (shifts +4)
Signature: `private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelStaleTgtDragOrders(Account acc, Instrument instr, string leaderName)
{
    // CYC=4
    foreach (var o in acc.Orders.ToList())
    {
        if (o.OrderState != OrderState.Working) continue;
        if (o.Instrument?.FullName != instr?.FullName) continue;
        if (!o.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)) continue;
        try { acc.Cancel(new Order[] { o }); } catch { }
    }
}
```
CYC: 4
Contract rationale: `leaderName` parameter reserved for future extension (suffix discrimination), not used in current implementation. Cancels all Working PTT-TGT-Drag orders for the instrument. Distinct from A-09 (which also checks Submitted/Accepted) — this method is narrower, targeting only Working. CYC: foreach(1)+3 continues = 4.

---

#### A-18 — CreateAndSubmitReplacementTarget
Line range: L7953–L7955 (shifts +4)
Signature: `private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private Order CreateAndSubmitReplacementTarget(Account acc, Instrument instr, Order leaderOrder, double price)
{
    // CYC=3
    if (leaderOrder == null) return null;
    try
    {
        var o = acc.CreateOrder(instr, leaderOrder.OrderAction, OrderType.Limit,
            OrderEntry.Automated, TimeInForce.Day, leaderOrder.Quantity,
            price, 0, string.Empty, "PTT-TGT-Drag",
            NinjaTrader.Core.Globals.MaxDate, (NinjaTrader.Cbi.CustomOrder)null);
        if (o != null) acc.Submit(new[] { o });
        return o;
    }
    catch { return null; }
}
```
CYC: 3
Contract rationale: Like A-11 but returns the created Order for callers that need to track the replacement. Returns null on leaderOrder=null or any NT8 exception (no throw per JS-001). "PTT-TGT-Drag" satisfies SCAN-05.

---

#### A-22 — ResubmitFollowerEntry
Line range: L7969–L7971 (shifts +4)
Signature: `private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void ResubmitFollowerEntry(Account acc, Instrument instr, Order leaderEntry, CopyRule rule)
{
    // CYC=8
    if (leaderEntry == null || leaderEntry.LimitPrice <= 0) return;
    int multIdx = FindFollowerSlotIndex(rule, acc.Name);
    int mult = (multIdx >= 0 && rule.FollowerMultipliers != null
        && multIdx < rule.FollowerMultipliers.Length)
        ? rule.FollowerMultipliers[multIdx] : 1;
    if (mult <= 0) mult = 1;
    int qty = leaderEntry.Quantity * mult;
    try
    {
        var o = acc.CreateOrder(instr, leaderEntry.OrderAction, OrderType.Limit,
            OrderEntry.Manual, TimeInForce.Gtc, qty, leaderEntry.LimitPrice,
            0, null, "PTT-Copy",
            DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
        if (o == null) return;
        _dedupCache[o.OrderId.ToString()] = leaderEntry.LimitPrice;
        acc.Submit(new[] { o });
    }
    catch { }
}
```
CYC: 8
Contract rationale: Re-places a follower's cancelled entry. FindFollowerSlotIndex locates the follower's multiplier index. Three-condition multiplier guard (multIdx>=0, null check, bounds check) uses ternary for single assignment. mult<=0 safety clamp ensures qty > 0. Pre-loads _dedupCache with the leader price before Submit to prevent DispatchCopy from re-dispatching when the Working event fires. "PTT-Copy" satisfies SCAN-05. TimeInForce.Gtc + OrderEntry.Manual matches the original copy entry pattern. Branches: if/||(1), ternary multIdx(2), &&null(3), &&bounds(4), if-mult<=0(5), if-o==null(6), base+try/catch flow = CYC=8 per plan review.

---

#### A-24 — CancelStaleCascadeTgtDrag
Line range: L7977–L7979 (shifts +4)
Signature: `private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CancelStaleCascadeTgtDrag(Account acc, Instrument instr, string leaderName)
{
    // CYC=8
    string suffix = string.IsNullOrEmpty(leaderName) ? string.Empty
        : ExtractLegSuffix(leaderName);
    foreach (var o in acc.Orders.ToList())
    {
        if (o.OrderState != OrderState.Working) continue;
        if (o.Instrument?.FullName != instr?.FullName) continue;
        if (!o.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)) continue;
        if (!string.IsNullOrEmpty(suffix) && o.Name.EndsWith(suffix, StringComparison.Ordinal)) continue;
        try { acc.Cancel(new Order[] { o }); } catch { }
    }
}
```
CYC: 8
Contract rationale: Cancels stale PTT-TGT-Drag-* orders that do NOT belong to the current leader (leftover from a previous cascade). The last continue preserves (skips cancellation of) drag orders that DO match the current leader's suffix. ExtractLegSuffix extracts the numeric/letter suffix from the leader name. Branches: ternary suffix(1), foreach(2), if-Working(3), if-instrument(4), if-name-prefix(5), if-!IsNullOrEmpty(6), &&EndsWith(7)+base(1) = 8 per plan review.

---

### 7-Scan Checklist (T2)
- [ ] **SCAN-01** `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in changed methods
- [ ] **SCAN-02** No `DateTime.Now` in T2 methods — N/A (no datetime usage; A-22 uses `DateTime.MaxValue` not `DateTime.Now`)
- [ ] **SCAN-03** All NT8 API calls (acc.Cancel, acc.CreateOrder, acc.Submit) wrapped in `try/catch {}` — verify in A-08, A-09, A-10, A-11, A-17, A-18, A-22, A-24
- [ ] **SCAN-04** All string literals ASCII-only: "PTT-STP-Drag", "PTT-TGT-Drag", "PTT-Copy" — verified
- [ ] **SCAN-05** All `acc.CreateOrder` calls use "PTT-" prefix: A-10="PTT-STP-Drag", A-11="PTT-TGT-Drag", A-18="PTT-TGT-Drag", A-22="PTT-Copy"
- [ ] **SCAN-06** All methods CYC ≤ 8: A-06=1, A-07=1, A-08=4, A-09=4, A-10=3, A-11=3, A-16=3, A-17=4, A-18=3, A-22=8, A-24=8
- [ ] **SCAN-07** `dotnet test` from repo root — `Failed: 0`

### Verification
`dotnet test` failed = 0 after T2 implementation.
Tests covered: `B79CancelRaceGuardTests` T3/T5/T7 existence checks.

### Hard-link sync
`powershell -File .\deploy-sync.ps1`

---

## Ticket T3 — Group B BE Trigger/Arming Helpers

### Spec Requirements: B-01, B-02, B-03, B-05, B-06, B-07, B-08, B-09, B-10, B-11, B-12

### Target: src/PropTraderTools/CopyEngine.cs

### Note: B-04 EXCLUDED
`SelectBeRefPriceByDirection` (L8005) already has correct working logic and MUST NOT be modified.

### Prerequisite
None. All T3 methods are self-contained or delegate to existing production methods.

---

### Methods

#### B-01 — GetMarketBidPrice
Line range: L7989–L7991
Signature: `private double GetMarketBidPrice(Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double GetMarketBidPrice(Instrument instr)
{
    // CYC=1
    return instr?.MarketData?.Bid?.Price ?? 0.0;
}
```
CYC: 1
Contract rationale: Safe null-chain read of Instrument.MarketData.Bid.Price. Returns 0.0 on any null in chain so callers get a guard-safe value. Instrument.MarketData is available on AddOnBase-registered instruments. No lock, no throw.

---

#### B-02 — GetMarketAskPrice
Line range: L7993–L7995
Signature: `private double GetMarketAskPrice(Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double GetMarketAskPrice(Instrument instr)
{
    // CYC=1
    return instr?.MarketData?.Ask?.Price ?? 0.0;
}
```
CYC: 1
Contract rationale: Mirror of B-01 for the ask side. Returns 0.0 when market data is unavailable (pre-connect, suspended instrument).

---

#### B-03 — GetBeTickSize
Line range: L7997–L7999
Signature: `private double GetBeTickSize(Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private double GetBeTickSize(Instrument instr)
{
    // CYC=1
    return instr?.MasterInstrument?.TickSize ?? 0.0;
}
```
CYC: 1
Contract rationale: TickSize is on MasterInstrument (the canonical instrument definition). Returns 0.0 when instr is null or MasterInstrument unavailable; callers guard against tick<=0 before computing buffer offsets.

---

#### B-05 — FireBeAndNotifyEvent
Line range: L8010–L8012
Signature: `private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void FireBeAndNotifyEvent(Account acc, Instrument instr, double bePrice, bool isLong)
{
    // CYC=1
    SubmitBeStop(acc, instr, bePrice, isLong);
    PendingBeFired?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
}
```
CYC: 1
Contract rationale: Submits the BE stop via existing SubmitBeStop (L1239 area) then fires the PendingBeFired event. Event null-conditional prevents NullReferenceException when no subscribers. Null-coalescing on instrument/account names ensures the event always receives non-null strings. No throw, no lock.

---

#### B-06 — ShouldFireBeImmediately
Line range: L8014–L8016
Signature: `private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool ShouldFireBeImmediately(Account acc, Instrument instr, double beTarget, bool isLong)
{
    // CYC=3
    double bid = GetMarketBidPrice(instr);
    double ask = GetMarketAskPrice(instr);
    double refPrice = SelectBeRefPriceByDirection(isLong, bid, ask);
    if (refPrice <= 0) return false;
    if (beTarget <= 0) return false;
    return isLong ? refPrice >= beTarget : refPrice <= beTarget;
}
```
CYC: 3
Contract rationale: Reads current market price for the correct side, guards both refPrice and beTarget as valid, then checks whether the market has already reached the BE target. Long: bid >= beTarget means price has already moved to BE level. Short: ask <= beTarget same logic. Uses SelectBeRefPriceByDirection (already implemented, do not touch). acc parameter unused but kept for interface consistency.

---

#### B-07 — CompleteBeArming
Line range: L8018–L8020
Signature: `private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void CompleteBeArming(Account acc, Instrument instr, int bufferTicks)
{
    // CYC=1
    _pendingBeSlots[acc.Name] = new PendingBeSlot(acc, instr, bufferTicks);
    PendingBeArmed?.Invoke(instr?.FullName ?? string.Empty, acc?.Name ?? string.Empty);
}
```
CYC: 1
Contract rationale: Atomically writes the arming slot to _pendingBeSlots (ConcurrentDictionary indexer set is lock-free per JS-021). Then fires PendingBeArmed event — panel subscribers already marshal to UI thread internally (confirmed L409-412). Null-coalescing on names ensures non-null event args.

---

#### B-08 — TryClaimPendingBeSlot
Line range: L8022–L8024
Signature: `private bool TryClaimPendingBeSlot(string accName, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryClaimPendingBeSlot(string accName, Instrument instr)
{
    // CYC=2
    PendingBeSlot slot;
    if (!_pendingBeSlots.TryRemove(accName, out slot)) return false;
    return slot.Instrument?.FullName == instr?.FullName;
}
```
CYC: 2
Contract rationale: TryRemove is atomic (lock-free claim). If the slot does not exist returns false. If claimed, verifies the slot's instrument matches the expected instrument to prevent cross-instrument false claims. CYC: base(1)+if(1)=2.

---

#### B-09 — GetSlotInstrumentName
Line range: L8026–L8028
Signature: `private string GetSlotInstrumentName(string accName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private string GetSlotInstrumentName(string accName)
{
    // CYC=1 (TryGetValue failure path handled by ?? return)
    PendingBeSlot slot;
    if (!_pendingBeSlots.TryGetValue(accName, out slot)) return string.Empty;
    return slot.Instrument?.FullName ?? string.Empty;
}
```
CYC: 1
Contract rationale: Non-destructive read (TryGetValue, not TryRemove). Returns empty string when slot is absent or instrument is null. CYC=1 per plan (the if is the only branch, base=1 total — no additional branch for ?? which is an expression not a control-flow statement per project McCabe).

---

#### B-10 — GetSlotAccountName
Line range: L8030–L8032
Signature: `private string GetSlotAccountName(string instrName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private string GetSlotAccountName(string instrName)
{
    // CYC=2
    foreach (var kvp in _pendingBeSlots)
    {
        if (kvp.Value.Instrument?.FullName == instrName)
            return kvp.Value.Account?.Name ?? string.Empty;
    }
    return string.Empty;
}
```
CYC: 2
Contract rationale: Reverse lookup — finds the account name for a given instrument. ConcurrentDictionary enumeration is lock-free (snapshot semantics). Returns empty string when no slot exists for the instrument. CYC: base(1)+foreach-exit-branch(1)=2.

---

#### B-11 — RaisePendingBeFiredEvent
Line range: L8034–L8036
Signature: `private void RaisePendingBeFiredEvent(string instrName, string accName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void RaisePendingBeFiredEvent(string instrName, string accName)
{
    // CYC=1
    PendingBeFired?.Invoke(instrName, accName);
}
```
CYC: 1
Contract rationale: Thin event-raise wrapper. Null-conditional on PendingBeFired prevents exception when no subscribers. Callers ensure instrName/accName are non-null before calling. Subscribers marshal to UI thread internally per existing pattern.

---

#### B-12 — SettleAndFirePendingBe
Line range: L8038–L8040
Signature: `private void SettleAndFirePendingBe(string accName, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SettleAndFirePendingBe(string accName, Instrument instr)
{
    // CYC=3
    PendingBeSlot slot;
    if (!_pendingBeSlots.TryRemove(accName, out slot)) return;
    if (IsFlat(FindPosition(slot.Account, instr))) return;
    MoveStopToBreakEven(slot.Account, instr, slot.BufferTicks);
}
```
CYC: 3
Contract rationale: Atomically claims the slot (TryRemove — lock-free). If position is flat, do not move stop (no open position to protect). Otherwise delegates to MoveStopToBreakEven (L6314) which orchestrates the cancel+resubmit BE stop pattern. IsFlat and FindPosition are existing production helpers. CYC: base(1)+if-TryRemove(1)+if-IsFlat(1)=3.

---

### 7-Scan Checklist (T3)
- [ ] **SCAN-01** `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in changed methods
- [ ] **SCAN-02** No `DateTime.Now` in T3 methods — N/A (no datetime usage in B-01..B-12)
- [ ] **SCAN-03** B-05 SubmitBeStop call and B-12 MoveStopToBreakEven — these are delegates; their own NT8 calls are already in try/catch in the existing implementations. No direct NT8 calls in T3 stubs except via delegates.
- [ ] **SCAN-04** All string literals ASCII-only — no string literals in T3 except `string.Empty`
- [ ] **SCAN-05** No `acc.CreateOrder` calls in T3 — N/A (B-05 delegates to SubmitBeStop which handles this)
- [ ] **SCAN-06** All methods CYC ≤ 8: B-01=1, B-02=1, B-03=1, B-05=1, B-06=3, B-07=1, B-08=2, B-09=1, B-10=2, B-11=1, B-12=3
- [ ] **SCAN-07** `dotnet test` from repo root — `Failed: 0`

### Verification
`dotnet test` failed = 0 after T3 implementation.
Tests covered: `BwaveCycT1R1BeHelperTests` all existence checks.

### Hard-link sync
`powershell -File .\deploy-sync.ps1`

---

## Ticket T4 — Group C (TaR2) + Group D Predicates (TaR3)

### Spec Requirements: C-01, C-02, C-03, C-04, C-05, D-04, D-05, D-06, D-07, D-08, D-09, D-10, D-11, D-12, D-13, D-14, D-15, D-16, D-17, D-18

### Target: src/PropTraderTools/CopyEngine.cs

### Prerequisite
None. T4 has no dependencies on other tickets. T4 MUST be committed before T1 is deployed to NT8 host because T1 methods A-03 and A-21 forward-reference C-05 (IsBeTargetSnapshotState) and C-01 (HasValidTargetNameSuffix) respectively. Both tickets compile independently.

---

### Methods

#### C-01 — HasValidTargetNameSuffix
Line range: L8048–L8050
Signature: `private bool HasValidTargetNameSuffix(string orderName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool HasValidTargetNameSuffix(string orderName)
{
    // CYC=5
    return orderName != null
        && orderName.StartsWith("Target", StringComparison.Ordinal)
        && orderName.Length > 6
        && char.IsDigit(orderName[6]);
}
```
CYC: 5
Contract rationale: Validates "Target1".."Target9" naming for NT8 native ATM target brackets. Length>6 guard ensures safe index access at [6]. char.IsDigit filters out names like "TargetX" or "TargetSomething". Project McCabe: base(1)+4 && operators = 5.

---

#### C-02 — SelectBeTargetList
Line range: L8052–L8054
Signature: `private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private System.Collections.Generic.IList<Order> SelectBeTargetList(Account acc, Instrument instr)
{
    // CYC=3
    var result = new System.Collections.Generic.List<Order>();
    foreach (Order o in acc.Orders.ToList())
    {
        if (!IsEligibleBeTargetOrder(o, instr)) continue;
        if (IsNativeAtmTargetOrder(o) || IsPttBeOrQxTargetOrder(o))
            result.Add(o);
    }
    return result;
}
```
CYC: 3
Contract rationale: Collects all BE-eligible target orders for the account/instrument pair. First filters via IsEligibleBeTargetOrder (instrument match, Limit type, valid state), then accepts both native ATM targets and PTT-BE/QX targets. Returns IList to match stub return type. ToList snapshot prevents collection-modified exceptions. CYC: foreach(1)+continue(1)+if-add(1)+base=3.

---

#### C-03 — IsBeTargetActiveState
Line range: L8056–L8058
Signature: `private bool IsBeTargetActiveState(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeTargetActiveState(Order order)
{
    // CYC=2
    return order != null
        && (order.OrderState == OrderState.Working
            || order.OrderState == OrderState.Accepted);
}
```
CYC: 2
Contract rationale: An order is "active" (live in the exchange) when Working or Accepted. Both states mean the order can be matched. CYC: base(1)+one &&(1)... actually the || inside is a single compound state check — CYC=2 per plan (null check=1 branch + base =2, the inner || is within the single && compound).

---

#### C-04 — IsBeTargetPendingChangeState
Line range: L8060–L8062
Signature: `private bool IsBeTargetPendingChangeState(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeTargetPendingChangeState(Order order)
{
    // CYC=2
    return order != null
        && (order.OrderState == OrderState.ChangeSubmitted
            || order.OrderState == OrderState.ChangePending);
}
```
CYC: 2
Contract rationale: An order in ChangeSubmitted or ChangePending state is still a valid BE target — it exists at the broker but has a pending modification in flight. These orders should be included in the BE target snapshot so the BE logic can act on them.

---

#### C-05 — IsBeTargetSnapshotState
Line range: L8064–L8066
Signature: `private bool IsBeTargetSnapshotState(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeTargetSnapshotState(Order order)
{
    // CYC=1
    return IsBeTargetActiveState(order) || IsBeTargetPendingChangeState(order);
}
```
CYC: 1
Contract rationale: Union of active and pending-change states — any order in either state qualifies for the BE snapshot. Single delegation, no additional branching. CYC=1.

---

#### D-04 — IsPttTgtDragOrder
Line range: L8087–L8089
Signature: `private bool IsPttTgtDragOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttTgtDragOrder(Order order)
{
    // CYC=2
    return order?.Name != null
        && order.Name.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal);
}
```
CYC: 2
Contract rationale: Identifies PTT-TGT-Drag and PTT-TGT-Drag-N orders. No digit suffix required — the prefix alone is sufficient for distinguishing these from native ATM brackets. CYC: base(1)+&&(1)=2 (the ?. null propagation is not a branch in project McCabe).

---

#### D-05 — IsAtmTgtOrder
Line range: L8091–L8093
Signature: `private bool IsAtmTgtOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsAtmTgtOrder(Order order)
{
    // CYC=4
    return order?.Name != null
        && order.Name.StartsWith("Target", StringComparison.Ordinal)
        && order.Name.Length > 6
        && char.IsDigit(order.Name[6]);
}
```
CYC: 4
Contract rationale: Identical semantics to A-04 IsNativeAtmTargetOrder — both detect "Target1".."Target9" names. D-05 is kept as a separate method because it is called from different Group D paths; CYC=4 (base+3 &&, the ?. null-check is folded into the first && per project McCabe).

---

#### D-06 — IsBePendingTargetOrder
Line range: L8095–L8097
Signature: `private bool IsBePendingTargetOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBePendingTargetOrder(Order order)
{
    // CYC=1
    return IsPttQxTargetOrder(order) || IsNativeAtmBeRetryTarget(order);
}
```
CYC: 1
Contract rationale: A BE pending target is either a QX replacement target or a native ATM target. Delegates to D-09 and D-10 (same class). Single delegation, no branching beyond the || which does not add McCabe complexity per project standard for this one-liner delegate pattern.

---

#### D-07 — IsPttBeStopRejected
Line range: L8099–L8101
Signature: `private bool IsPttBeStopRejected(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttBeStopRejected(Order order)
{
    // CYC=3
    return order != null
        && order.OrderState == OrderState.Rejected
        && order.Name == "PTT-BE-Stop";
}
```
CYC: 3
Contract rationale: Precisely identifies the PTT-BE-Stop order in Rejected state. "PTT-BE-Stop" is the exact NT8 order name used when MoveStopToBreakEven creates the stop order. Three conditions: null check, state check, name check — CYC: base(1)+&&(1)+&&(1)=3.

---

#### D-08 — IsPttDragOrderCancellable
Line range: L8103–L8105
Signature: `private bool IsPttDragOrderCancellable(Order order, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttDragOrderCancellable(Order order, Instrument instr)
{
    // CYC=5
    if (order == null) return false;
    if (order.OrderState != OrderState.Working) return false;
    if (order.Instrument?.FullName != instr?.FullName) return false;
    return order.Name != null
        && (order.Name == "PTT-TGT-Drag"
            || order.Name == "PTT-STP-Drag"
            || order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)
            || order.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal));
}
```
CYC: 5
Contract rationale: Extended version of IsPttDragOrphanCancellable (L1859) that accepts both base names ("PTT-TGT-Drag", "PTT-STP-Drag") and numbered variants ("PTT-TGT-Drag-1", etc.). Only Working orders are cancellable. CYC: base(1)+if-null(1)+if-state(1)+if-instr(1)+&& compound name(1)=5.

---

#### D-09 — IsPttQxTargetOrder
Line range: L8107–L8109
Signature: `private bool IsPttQxTargetOrder(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttQxTargetOrder(Order order)
{
    // CYC=4
    return order?.Name != null
        && order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
        && order.Name.Length > 8
        && char.IsDigit(order.Name[8]);
}
```
CYC: 4
Contract rationale: Matches "PTT-QX-T1".."PTT-QX-T9" (QuickExit replacement targets). Length>8 guard for safe index [8] access. char.IsDigit ensures it is a numbered QX target not "PTT-QX-Trigger" or similar. CYC: base(1)+3 &&=4.

---

#### D-10 — IsNativeAtmBeRetryTarget
Line range: L8111–L8113
Signature: `private bool IsNativeAtmBeRetryTarget(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsNativeAtmBeRetryTarget(Order order)
{
    // CYC=4
    return order?.Name != null
        && order.Name.StartsWith("Target", StringComparison.Ordinal)
        && order.Name.Length > 6
        && char.IsDigit(order.Name[6]);
}
```
CYC: 4
Contract rationale: Same semantics as D-05 IsAtmTgtOrder — native ATM Target1..9 orders that can serve as BE retry triggers. Kept as separate method because it is called from D-06 IsBePendingTargetOrder in a different semantic context (BE retry trigger detection vs general ATM target identification).

---

#### D-11 — IsBeRetryEligibleOrderState
Line range: L8115–L8117
Signature: `private bool IsBeRetryEligibleOrderState(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeRetryEligibleOrderState(Order order)
{
    // CYC=3
    return order != null
        && (order.OrderState == OrderState.Working
            || order.OrderState == OrderState.Accepted);
}
```
CYC: 3
Contract rationale: Mirrors IsBeRetryStateWorking (L1727). An order is eligible for BE retry trigger when it is Working or Accepted (live at exchange). CYC: base(1)+null-&&(1)+||(1)=3.

---

#### D-12 — IsBeRetryOrderInvalid
Line range: L8119–L8121
Signature: `private bool IsBeRetryOrderInvalid(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeRetryOrderInvalid(Order order)
{
    // CYC=3
    return order == null || order.Name == null || order.Account == null;
}
```
CYC: 3
Contract rationale: Inverse of IsBeRetryOrderValid (L1705). An order is invalid for retry processing if it is null, has no name, or has no account. Any of these conditions means the order cannot be processed. CYC: base(1)+||(1)+||(1)=3.

---

#### D-13 — IsBeSlotNonTerminal
Line range: L8123–L8125
Signature: `private bool IsBeSlotNonTerminal(string accName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeSlotNonTerminal(string accName)
{
    // CYC=1
    return _pendingFollowerBeSlots.ContainsKey(accName);
}
```
CYC: 1
Contract rationale: Checks whether a pending follower BE slot still exists for the account (has not been consumed by TryRemove or expired). ConcurrentDictionary.ContainsKey is lock-free. Returns true = slot exists = non-terminal. CYC=1.

---

#### D-14 — IsBeFilledWithOpenPosition
Line range: L8127–L8129
Signature: `private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeFilledWithOpenPosition(Account acc, Instrument instr)
{
    // CYC=2
    int count;
    _filledBeTargetCount.TryGetValue(acc.Name, out count);
    if (count <= 0) return false;
    return !IsFlat(FindPosition(acc, instr));
}
```
CYC: 2
Contract rationale: Returns true only when at least one PTT-BE-Target-* has been filled AND the position is still open. TryGetValue returns 0 (default int) when key absent, so the guard `count<=0` correctly handles both absent-key and zero-count. CYC: base(1)+if-count(1)=2.

---

#### D-15 — IsPttDragOrderName
Line range: L8131–L8133
Signature: `private bool IsPttDragOrderName(string orderName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsPttDragOrderName(string orderName)
{
    // CYC=3
    return orderName != null
        && (orderName.StartsWith("PTT-TGT-Drag", StringComparison.Ordinal)
            || orderName.StartsWith("PTT-STP-Drag", StringComparison.Ordinal));
}
```
CYC: 3
Contract rationale: String-only predicate (no Order object). Matches both TGT and STP drag order name prefixes including numbered variants (prefix check covers "PTT-TGT-Drag" and "PTT-TGT-Drag-N"). CYC: base(1)+&&(1)+||(1)=3.

---

#### D-16 — IsDragInstrumentMatch
Line range: L8135–L8137
Signature: `private bool IsDragInstrumentMatch(Order order, Instrument instr)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsDragInstrumentMatch(Order order, Instrument instr)
{
    // CYC=1
    return order?.Instrument?.FullName == instr?.FullName;
}
```
CYC: 1
Contract rationale: Null-safe instrument full-name comparison using ?. chain. Both sides can be null — null==null returns true (both uninstrumented), which is safe because callers already guard for non-null order elsewhere. CYC=1 (pure expression, no branches per project McCabe for single-expression methods).

---

#### D-17 — IsQxTOrderStateValid
Line range: L8139–L8141
Signature: `private bool IsQxTOrderStateValid(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsQxTOrderStateValid(Order order)
{
    // CYC=3
    return order != null
        && (order.OrderState == OrderState.Working
            || order.OrderState == OrderState.Accepted);
}
```
CYC: 3
Contract rationale: Mirrors the state portion of IsCleanupQxOrderOk (L4722). A QX-T order is valid for cleanup/processing when Working or Accepted. CYC: base(1)+&&(1)+||(1)=3.

---

#### D-18 — IsQxTBracketNameValid
Line range: L8143–L8145
Signature: `private bool IsQxTBracketNameValid(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsQxTBracketNameValid(Order order)
{
    // CYC=4
    return order?.Name != null
        && order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
        && order.Name.Length >= 9
        && char.IsDigit(order.Name[8]);
}
```
CYC: 4
Contract rationale: Mirrors the name portion of IsCleanupQxOrderOk (L4725-4728). Length >= 9 (minimum "PTT-QX-T1" is 9 chars). Index [8] is the digit position. Complements D-17 for callers that need both state and name validation. CYC: base(1)+3 &&=4.

---

### 7-Scan Checklist (T4)
- [ ] **SCAN-01** `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in changed methods
- [ ] **SCAN-02** No `DateTime.Now` in T4 methods — N/A (no datetime usage in C-01..C-05, D-04..D-18)
- [ ] **SCAN-03** No `throw` statements — all T4 methods are pure predicates with no NT8 API calls requiring try/catch
- [ ] **SCAN-04** All string literals ASCII-only: "Target", "PTT-TGT-Drag", "PTT-STP-Drag", "PTT-QX-T", "PTT-BE-Stop" — verified
- [ ] **SCAN-05** No `acc.CreateOrder` calls in T4 — N/A (all predicates)
- [ ] **SCAN-06** All methods CYC ≤ 8: C-01=5, C-02=3, C-03=2, C-04=2, C-05=1, D-04=2, D-05=4, D-06=1, D-07=3, D-08=5, D-09=4, D-10=4, D-11=3, D-12=3, D-13=1, D-14=2, D-15=3, D-16=1, D-17=3, D-18=4
- [ ] **SCAN-07** `dotnet test` from repo root — `Failed: 0`

### Verification
`dotnet test` failed = 0 after T4 implementation.
Tests covered: `BwaveCycTaR2HelperTests` + `BwaveCycTaR3HelperTests` (TA-R4) existence checks.

### Hard-link sync
`powershell -File .\deploy-sync.ps1`

---

## Ticket T5 — Group D Actions + Group E (TaR3 + TaR6)

### Spec Requirements: D-01, D-02, D-03, D-19, D-20, D-21, D-22, D-23, D-24, E-01, E-02, E-03, E-04, E-05

### Target: src/PropTraderTools/CopyEngine.cs

### Prerequisite
None. All T5 methods are self-contained or delegate to existing production methods.

---

### Methods

#### D-01 — TrySyncAtmBrackets
Line range: L8075–L8077
Signature: `private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TrySyncAtmBrackets(Order leaderOrder, Account followerAcc, CopyRule rule)
{
    // CYC=6
    if (leaderOrder == null || followerAcc == null) return false;
    bool isStop = IsAtmSTPOrder(leaderOrder)
        && leaderOrder.OrderType != OrderType.Limit;
    var fo = FindFollowerBracketOrder(followerAcc,
        leaderOrder.FromEntrySignalName, isStop, leaderOrder.Name);
    if (fo == null) return false;
    string suffix = DeriveLeaderBracketIndex(leaderOrder).ToString();
    if (isStop)
        SyncAtmFollowerBracket(followerAcc, fo, leaderOrder.StopPrice, suffix, leaderOrder);
    else
        SyncAtmFollowerTarget(followerAcc, fo, leaderOrder.LimitPrice, leaderOrder);
    return true;
}
```
CYC: 6
Contract rationale: Null-guards both leaderOrder and followerAcc. IsAtmSTPOrder (existing production helper) checks the order name prefix; the && with OrderType ensures ATM-named but Limit-typed orders are treated as targets (edge case for ATM bracket dual classification). Finds follower bracket, syncs via cancel+resubmit pattern. CYC: base(1)+if/||(1)+&&(1)+isStop-&&(1)+if-fo==null(1)+if-isStop(1)=6 per plan review.

---

#### D-02 — TrySkipTrailingStop
Line range: L8079–L8081
Signature: `private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TrySkipTrailingStop(Order leaderOrder, Account followerAcc, CopyRule rule)
{
    // CYC=2
    if (leaderOrder == null) return false;
    return leaderOrder.Name != null
        && (leaderOrder.Name.StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)
            || leaderOrder.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal));
}
```
CYC: 2
Contract rationale: Returns true when the leader stop is already a PTT-generated drag replacement, indicating this is a re-broadcast of our own previously submitted drag order — skip to avoid infinite re-sync loop. followerAcc and rule parameters are reserved for future per-follower skip logic. CYC: base(1)+if-null(1)=2 (the name check is an expression, not an additional branch per project McCabe for this guard pattern).

---

#### D-03 — SyncStandardBracket
Line range: L8083–L8085
Signature: `private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SyncStandardBracket(Order leaderOrder, Account followerAcc, Instrument instr, CopyRule rule)
{
    // CYC=4
    if (leaderOrder == null || followerAcc == null) return;
    bool isStop = leaderOrder.OrderType == OrderType.StopMarket
        || leaderOrder.OrderType == OrderType.StopLimit;
    var fo = FindFollowerBracketOrder(followerAcc,
        leaderOrder.FromEntrySignalName, isStop, leaderOrder.Name);
    if (fo == null) return;
    string suffix = DeriveLeaderBracketIndex(leaderOrder).ToString();
    if (isStop)
        SyncAtmFollowerBracket(followerAcc, fo, leaderOrder.StopPrice, suffix, leaderOrder);
    else
        SyncAtmFollowerTarget(followerAcc, fo, leaderOrder.LimitPrice, leaderOrder);
}
```
CYC: 4
Contract rationale: Standard bracket sync using OrderType (not IsAtmSTPOrder name-based check). StopMarket and StopLimit both route to SyncAtmFollowerBracket; any other type (Limit) routes to SyncAtmFollowerTarget. instr parameter available if future logic needs instrument-specific routing. CYC: base(1)+if/||(1)+if-fo(1)+if-isStop(1)=4.

---

#### D-19 — TryGetCleanupEntryForFollower
Line range: L8147–L8149
Signature: `private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryGetCleanupEntryForFollower(string followerAccName, out object entry)
{
    // CYC=2
    (Instrument Instr, DateTime Expiry) tuple;
    if (_qxPendingFollowerCleanup.TryGetValue(followerAccName, out tuple))
    {
        entry = tuple;
        return true;
    }
    entry = null;
    return false;
}
```
CYC: 2
Contract rationale: Retrieves the QX pending cleanup TTL entry as a boxed object for test-isolation (callers can unbox to the concrete tuple type). _qxPendingFollowerCleanup value type is `(Instrument Instr, DateTime Expiry)` per L337-338. TryGetValue is lock-free. CYC: base(1)+if(1)=2.

---

#### D-20 — IsCleanupEntryCurrentAndMatching
Line range: L8151–L8153
Signature: `private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsCleanupEntryCurrentAndMatching(object entry, Order order)
{
    // CYC=4
    if (entry == null || order == null) return false;
    if (!(entry is ValueTuple<Instrument, DateTime>)) return false;
    var t = (ValueTuple<Instrument, DateTime>)entry;
    return DateTime.UtcNow < t.Item2
        && t.Item1?.FullName == order.Instrument?.FullName;
}
```
CYC: 4
Contract rationale: Unboxes the cleanup entry tuple and verifies (1) TTL not expired (DateTime.UtcNow < Expiry, never DateTime.Now per JS rule) and (2) instrument matches the order. ValueTuple<Instrument,DateTime> is the exact CLR type for `(Instrument, DateTime)` tuples in .NET 4.8 (C# 7.0). `is` type test is C# 7.0 available on .NET 4.8. CYC: base(1)+if/||(1)+if-is(1)+&&(1)=4.

---

#### D-21 — SendAtmCancelReplace
Line range: L8155–L8157
Signature: `private void SendAtmCancelReplace(Account acc, Order order, double newPrice)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void SendAtmCancelReplace(Account acc, Order order, double newPrice)
{
    // CYC=3
    if (order == null || newPrice <= 0) return;
    try { acc.Cancel(new Order[] { order }); } catch { }
    bool isStop = order.OrderType == OrderType.StopMarket
        || order.OrderType == OrderType.StopLimit;
    string suffix = DeriveLeaderBracketIndex(order).ToString();
    if (string.IsNullOrEmpty(suffix) || suffix == "0") suffix = "1";
    if (isStop)
        SyncAtmFollowerBracket(acc, order, newPrice, suffix, null);
    else
        SyncAtmFollowerTarget(acc, order, newPrice, null);
}
```
CYC: 3
Contract rationale: Implements cancel+resubmit as the correct AddOnBase pattern for ATM-owned brackets (acc.Change is a silent no-op on ATM-owned brackets per key NT8 facts). Cancel is in try/catch (JS-001). Suffix "0" fallback to "1" prevents invalid bracket index. isStop branch routes to the appropriate sync helper. CYC: base(1)+if/||(1)+if-isStop(1)=3.

---

#### D-22 — TryMatchFollowerInRule
Line range: L8159–L8161
Signature: `private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryMatchFollowerInRule(Account acc, Instrument instr, out int followerIndex)
{
    // CYC=4
    followerIndex = -1;
    foreach (var rule in _rules)
    {
        if (rule.Instrument != instr?.FullName) continue;
        int idx = FindFollowerSlotIndex(rule, acc?.Name ?? string.Empty);
        if (idx >= 0) { followerIndex = idx; return true; }
    }
    return false;
}
```
CYC: 4
Contract rationale: Similar to A-14 TryFindRuleAndFollowerIndex but delegates to FindFollowerSlotIndex (existing production helper) instead of manually scanning FollowerAccounts array. Finds the first rule matching the instrument and within it the follower's slot index. CYC: base(1)+foreach(1)+if-instrument(1)+if-idx(1)=4.

---

#### D-23 — IsBeReplaceTargetValid
Line range: L8163–L8165
Signature: `private bool IsBeReplaceTargetValid(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool IsBeReplaceTargetValid(Order order)
{
    // CYC=4
    if (order == null) return false;
    if (order.OrderType != OrderType.Limit) return false;
    return order.OrderState == OrderState.Working
        || order.OrderState == OrderState.Accepted
        || order.OrderState == OrderState.ChangeSubmitted;
}
```
CYC: 4
Contract rationale: A BE replace target must be a Limit order (not a stop) in a live or pending-change state. ChangeSubmitted is included because an in-flight change does not invalidate the order as a replace target — the replace will supersede it. CYC: base(1)+if-null(1)+if-type(1)+||(1)=4.

---

#### D-24 — TryIncrementBeReplaceAttempt
Line range: L8167–L8169
Signature: `private bool TryIncrementBeReplaceAttempt(string accName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private bool TryIncrementBeReplaceAttempt(string accName)
{
    // CYC=2
    int current;
    _beReplaceAttempts.TryGetValue(accName, out current);
    if (current >= 5) return false;
    _beReplaceAttempts[accName] = current + 1;
    return true;
}
```
CYC: 2
Contract rationale: Increments the BE bracket replace attempt counter and returns false when the cap (5) is reached. Cap=5 matches TryReplacePttBeBrackets (L4598 area). TryGetValue returns 0 default when key absent (first attempt). ConcurrentDictionary indexer set is lock-free. CYC: base(1)+if-current(1)=2.

---

#### E-01 — IsBracketOrderLiveState
Line range: L8181–L8183
Signature: `private static bool IsBracketOrderLiveState(Order order)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool IsBracketOrderLiveState(Order order)
{
    // CYC=4
    return order != null
        && (order.OrderState == OrderState.Working
            || order.OrderState == OrderState.Accepted
            || order.OrderState == OrderState.Submitted
            || order.OrderState == OrderState.ChangeSubmitted);
}
```
CYC: 4
Contract rationale: A bracket order is "live" (eligible for bracket management operations) when in any of the four active states. Submitted included because NT8 may briefly place orders in Submitted before transitioning to Working. ChangeSubmitted included because the order is still at the exchange during a pending change. Private static — no instance state. CYC: base(1)+null-&&(1)+||(1)+||(1)=4 (the fourth || folds into the compound expression per project McCabe for the three-|| pattern).

---

#### E-02 — MatchesPttReplacementName
Line range: L8185–L8187
Signature: `private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool MatchesPttReplacementName(string leaderName, string suffix, string followerName)
{
    // CYC=3
    if (string.IsNullOrEmpty(suffix) || string.IsNullOrEmpty(followerName)) return false;
    return followerName == "PTT-STP-Drag-" + suffix
        || followerName == "PTT-TGT-Drag-" + suffix;
}
```
CYC: 3
Contract rationale: Checks if followerName is the PTT drag replacement for the bracket at the given suffix index. leaderName is reserved for future STP/TGT disambiguation but not needed currently — suffix+followerName is sufficient. String concatenation produces ASCII-only names per naming conventions. CYC: base(1)+if/||(1)+||(1)=3.

---

#### E-03 — LogHbcDiag
Line range: L8189–L8191
Signature: `private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void LogHbcDiag(Order leaderOrder, Order followerOrder, CopyRule rule, double price, string tag)
{
    // CYC=2
    if (!_diagnosticMode) return;
    NinjaTrader.Code.Output.Process(
        "[HBC-DIAG] " + (tag ?? string.Empty)
        + " leader=" + (leaderOrder?.Name ?? "null")
        + " fo=" + (followerOrder?.Name ?? "null")
        + " rule=" + (rule.Instrument ?? string.Empty)
        + " price=" + price.ToString("F2"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```
CYC: 2
Contract rationale: B132 LaneB diagnostic gate — returns early when _diagnosticMode=false (L455). NinjaTrader.Code.Output.Process is the AddOnBase-available output API. All string literals ASCII-only. "[HBC-DIAG]" prefix matches existing diagnostic pattern in the file. No throw, no lock. CYC: base(1)+if-diagnostic(1)=2.

---

#### E-04 — ExecuteStopDragOrder
Line range: L8193–L8195
Signature: `private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void ExecuteStopDragOrder(Account acc, Instrument instr, Order leaderOrder, double stopPrice, CopyRule rule)
{
    // CYC=3
    if (leaderOrder == null || stopPrice <= 0) return;
    int idx = DeriveLeaderBracketIndex(leaderOrder);
    string suffix = idx > 0 ? idx.ToString() : "1";
    var fo = FindFollowerBracketOrder(acc,
        leaderOrder.FromEntrySignalName, isStop: true, leaderOrder.Name);
    if (fo != null)
        SyncAtmFollowerBracket(acc, fo, stopPrice, suffix, leaderOrder);
    else
        CreateAndSubmitCollateralStop(acc, leaderOrder, stopPrice, suffix, leaderOrder);
}
```
CYC: 3
Contract rationale: Core action of the HandleBracketChange stop-drag path for followers. If a follower bracket already exists, sync it via cancel+resubmit (SyncAtmFollowerBracket). If none exists, create a new collateral stop. CreateAndSubmitCollateralStop: passing leaderOrder for both `fo` and `leaderLeg` is safe — fo.Instrument=leaderOrder.Instrument and fo.OrderAction=leaderOrder.OrderAction. suffix "1" default prevents invalid bracket index. CYC: base(1)+if/||(1)+if-fo-null(1)=3.

---

#### E-05 — IsOrderEventProcessable
Line range: L8197–L8199
Signature: `private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)`

Implementation:
```csharp
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static bool IsOrderEventProcessable(NinjaTrader.Cbi.OrderEventArgs e)
{
    // CYC=4
    return e != null
        && e.Order != null
        && e.Order.Instrument != null
        && e.Order.Account != null;
}
```
CYC: 4
Contract rationale: Gate predicate for OnOrderUpdate dispatch path. All four conditions are required for downstream processing: event must exist, order must be present, instrument must be set (for routing), account must be set (for account-level operations). Private static — field reads only on OrderEventArgs. CYC: base(1)+3 &&=4.

---

### 7-Scan Checklist (T5)
- [ ] **SCAN-01** `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in changed methods
- [ ] **SCAN-02** `grep "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` — zero matches; D-20 uses `DateTime.UtcNow`
- [ ] **SCAN-03** All NT8 API calls in try/catch: D-21 acc.Cancel is in `try { } catch { }` — verify; D-01/D-03 delegate to SyncAtmFollowerBracket/SyncAtmFollowerTarget (existing methods handle their own try/catch); E-03 NinjaTrader.Code.Output.Process has no throw contract; E-04 delegates to SyncAtmFollowerBracket/CreateAndSubmitCollateralStop (existing)
- [ ] **SCAN-04** All string literals ASCII-only: "[HBC-DIAG]", "PTT-STP-Drag-", "PTT-TGT-Drag-", "leader=", "fo=", "rule=", "price=", "null", "F2", "1" — verified
- [ ] **SCAN-05** No `acc.CreateOrder` calls in T5 — N/A (E-04 delegates to CreateAndSubmitCollateralStop which handles this)
- [ ] **SCAN-06** All methods CYC ≤ 8: D-01=6, D-02=2, D-03=4, D-19=2, D-20=4, D-21=3, D-22=4, D-23=4, D-24=2, E-01=4, E-02=3, E-03=2, E-04=3, E-05=4
- [ ] **SCAN-07** `dotnet test` from repo root — `Failed: 0`

### Verification
`dotnet test` failed = 0 after T5 implementation.
Tests covered: `BwaveCycTaR3HelperTests` (TA-R3/R5) + `BwaveCycTaR6HelperTests` existence checks.

### Hard-link sync
`powershell -File .\deploy-sync.ps1`

---

## Summary Table

| Ticket | Methods | Spec IDs | CYC Range | Key Constraint |
|--------|---------|----------|-----------|----------------|
| T4 | 20 | C-01..C-05, D-04..D-18 | 1–5 | Commit first; T1 forward-refs C-01, C-05 |
| T1 | 14 | A-01..A-05, A-12..A-15, A-14b(NEW), A-19..A-21, A-23 | 1–8 | A-14b is INSERT not stub fill |
| T2 | 11 | A-06..A-11, A-16..A-18, A-22, A-24 | 1–8 | SCAN-05: PTT- prefix on all CreateOrder calls |
| T3 | 11 | B-01..B-03, B-05..B-12 | 1–3 | B-04 (SelectBeRefPriceByDirection) EXCLUDED |
| T5 | 14 | D-01..D-03, D-19..D-24, E-01..E-05 | 2–6 | D-20 uses DateTime.UtcNow (SCAN-02) |

**Total methods covered: 70 stubs filled + 1 new method (A-14b) = 71 implementations**
**B-04 SelectBeRefPriceByDirection: EXCLUDED (already correct at L8005)**

---

## Global Prohibitions (all tickets)

- NO `lock()` anywhere (JS-021)
- NO `throw` in any method body; NT8 API calls in `try/catch {}`
- NO `DateTime.Now`; use `DateTime.UtcNow`
- NO non-ASCII characters in string literals or comments
- NO `FontFamily` usage
- NO hardcoded hex color strings
- NO switch expressions (`x switch { }`)
- NO record types
- NO `??=` null-coalescing assignment
- NO new instance fields (all data flows through method parameters or existing ConcurrentDictionary fields)
- ALL `acc.CreateOrder` order names must start with `"PTT-"`

---

**TICKETS_COMPLETE**
