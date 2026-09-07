# WAVE1-LANE-B Architecture Plan
# Phase 1 Output -- ptt-architect
# Files: PttBreakEven.cs, PttBreakEvenSwap.cs, PttGlobalQuickExit.cs, PttQuickExit.cs, PttGlobalBreakEven.cs
# Target: ALL methods CCN <= 8 (Jane Street strict standard)
# Date: 2026-08

---

## Section 1: RULES CATALOG GATE RESULT

GATE RESULT: **PASS**

Checks performed:
- Read: docs/standards/jane-street/RULES_CATALOG.md (UTF-8, readable)
- Read: docs/intel/jane-street/complexity-reduction.md
- Read: docs/intel/jane-street/lock-free-patterns.md
- P0 scan: `Select-String -Pattern "lock\(" -Path <all 5 target files>`
  Result: ONE match only -- a COMMENT line in PttGlobalBreakEven.cs line 4: `// JS-021: no lock().`
  No actual `lock(` statement in any of the 5 files. P0 CLEAR.
- JS-033 (async void): zero async void in any target file. CLEAR.
- JS-001 (throw in hot path): no naked throw; all methods use try/catch with log. CLEAR.
- JS-002 (return null): all methods return void, bool, or initialized collections. CLEAR.
- DateTime.Now: not found in any target file (only DateTime.UtcNow and DateTime.MaxValue used). CLEAR.
- PTT- signal name prefix: confirmed on all CreateOrder calls. CLEAR.
- ASCII-only strings: confirmed. CLEAR.

---

## Section 2: LANE-SPLIT GATE RESULT

Q1. Are all target methods in the same file or within 50 lines of each other?
    NO -- methods span 5 different files across 4 classes.

Q2. Does any fix's design depend on another fix's final design?
    NO -- each extraction is entirely within its own class. IsLeaderAccount (PttGlobalQuickExit)
    has zero dependency on GetBeOcoSeq (PttBreakEven). No cross-class helper sharing.

Q3. Does each fix have standalone value if the other is blocked?
    YES -- each file's extractions are independent. PttBreakEven.cs extractions can land
    without PttGlobalQuickExit.cs extractions and vice versa.

Q4. Does each fix have an independent SIM verification path?
    YES -- PttBreakEven methods are exercised by the BE-ALL button path; PttGlobalQuickExit
    methods are exercised by the QX-ALL button path; each has distinct SIM test scenarios.

LANE-SPLIT GATE RESULT: **LANES-APPROVED**
(Q1=NO, Q2=NO, Q3=YES, Q4=YES -- parallel ticket execution viable, no internal sequencing required)

---

## Section 3: Per-Method Analysis

### IMPORTANT CONTEXT NOTE

The CCN baseline provided (CCN=103, 79, 80, 61, etc.) reflects a historical measurement taken
BEFORE substantial extraction work was already completed on these files. Reading the actual
source confirms that prior extraction work has been applied. The current CCN values are
significantly lower than the baseline.

The methods that STILL EXCEED CCN 8 in current source are identified below. All other methods
are verified at CCN <= 8 and require verification only (no new engineering).

The plan maps EVERY baseline method to its status and ticket assignment.

---

### B-01: PttBreakEven::SubmitBePair -- Baseline CCN=103, lines 438-543

**Body summary:** Submits one OCO stop+target pair for a given tranche. Creates a PTT-BE-Stop-{i+1}
StopMarket order and a PTT-BE-Target-{i+1} Limit order, each in their own try/catch block.
Each order is submitted immediately after creation. Returns void.

**Current CCN assessment (actual code read):**
Branches: if(sOrd!=null)=+1, catch=+1, if(tOrd!=null)=+1, catch=+1. Total CCN = 1+4 = **5**.
Prior extraction already factored out all the loop orchestration. Status: **ALREADY COMPLIANT**.

**Extraction strategy:** None needed.

**NT8 atomicity:** CreateOrder + Submit are together in each try block. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-02: PttBreakEvenSwap::SubmitSwapPair -- Baseline CCN=79, lines 178-259

**Body summary:** Submits one OCO stop+target pair for the BE-ALL swap path. Has a stop-price
submittability guard (IsStopPriceSubmittable) before submitting the PTT-BE-Stop-{i+1} order,
then unconditionally tries the PTT-BE-Target-{i+1} Limit order. Each section is try/catch guarded.

**Current CCN assessment (actual code read):**
Branches: if(IsStopPriceSubmittable)=+1, try=0, catch=+1, else=0, try=0, catch=+1. Total CCN = 1+3 = **4**.
Status: **ALREADY COMPLIANT**.

**Extraction strategy:** None needed.

**NT8 atomicity:** CreateOrder + Submit together in each try block. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-03: PttGlobalQuickExit::ExecuteFollowers -- Baseline CCN=80, lines 198-285

**Body summary:** Iterates all follower accounts for the leader position. For each follower,
cancels PTT-BE-* orders, waits for cancellation, snapshots follower targets, logs a DIAG block
(including a position-find foreach and a for-loop StringBuilder), resolves follower targets via
ResolveFollowerTargets, then calls ExecuteOne with leaderStop fallback.

**Current CCN assessment (actual code read):**
Branches: if(rule!=null)=+1, foreach follower=+1, if(follower==null)=+1,
inner foreach _p (position find) =+1, if(_p!=null && _p.Instrument!=null && FullName==)=+1,&&=+1,&&=+1,
for _i (DIAG log)=+1. Total CCN = 1+8 = **9**. ABOVE 8.

**Extraction strategy:**
Extract `private static int FindFollowerPosQty(Account follower, Instrument instr)`.
Body: foreach over follower.Positions, if match returns _p.Quantity, returns 0 if none found.
This removes from ExecuteFollowers: foreach=1, if=1, &&=1, &&=1 = 4 branches.
Post-extraction ExecuteFollowers CCN = 9 - 4 = **5**. ✅
FindFollowerPosQty CCN = 1 + foreach=1 + if=1 + &&=1 + &&=1 = **5**. ✅

**Proposed helper signature:**
```csharp
private static int FindFollowerPosQty(
    NinjaTrader.Cbi.Account follower,
    NinjaTrader.Cbi.Instrument instr
)
```

**NT8 atomicity:** No order submissions in this helper. Pure read of acc.Positions. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-04a: PttGlobalQuickExit::Execute() -- Baseline CCN=61, lines 36-104

**Body summary:** Outer account loop for no-arg Quick Exit. Skips followers, iterates positions,
cancels PTT-BE-* orders, waits for cancellation, snapshots target orders, snapshots leader stop,
resolves ticks, logs DIAG, checks NeedsLeaderFallbackFlatten (flatten path), then calls ExecuteOne
and ExecuteFollowers.

**Current CCN assessment (actual code read):**
Branches: if(!Flags.QxGlobalExit)=+1, foreach acc=+1,
if(engine!=null && engine.IsFollowerAccount)=+1,&&=+1,
foreach pos=+1, if(pos==null||qty==0)=+1,||=+1,
if(NeedsLeaderFallbackFlatten)=+1.
Total CCN = 1+8 = **9**. ABOVE 8.

**Extraction strategy:**
Extract shared helpers IsLeaderAccount and IsFlatPosition (see B-04b below).
After applying both: CCN = 9 - 1(&&) - 1(||) = **7**. ✅

**NT8 atomicity:** No order submissions directly. Delegates to ExecuteOne. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-04b: PttGlobalQuickExit::Execute(List) -- Baseline CCN=73, lines 116-188

**Body summary:** Forced-target variant of Execute(). Takes a pre-built forcedTargets list instead
of snapshotting. Guards: Elite flag, invalid forced targets (< 2 entries), then the same
account+position loop with flatten fallback.

**Current CCN assessment (actual code read):**
Branches: if(!Flags.QxGlobalExit)=+1, if(IsInvalidForcedTargets)=+1,
foreach acc=+1, if(engine!=null&&IsFollowerAccount)=+1,&&=+1,
foreach pos=+1, if(pos==null||qty==0)=+1,||=+1,
if(NeedsLeaderFallbackFlatten)=+1.
Total CCN = 1+9 = **10**. ABOVE 8.

**Extraction strategy:**
Extract:
(a) `private static bool IsLeaderAccount(CopyEngine engine, Account acc)` --
    body: `return engine == null || !engine.IsFollowerAccount(acc);`
    Removes && from Execute(List): -1 CCN.
(b) `private static bool IsFlatPosition(Position pos)` --
    body: `return pos == null || pos.Quantity == 0;`
    Removes || from Execute(List): -1 CCN.
Post-extraction Execute(List) CCN = 10 - 1 - 1 = **8**. ✅ (AT-LIMIT)
IsLeaderAccount CCN = 1 + || = **2**. ✅
IsFlatPosition CCN = 1 + || = **2**. ✅

Apply SAME IsLeaderAccount and IsFlatPosition to Execute() (B-04a):
Execute() CCN = 9 - 1 - 1 = **7**. ✅

**Proposed helper signatures:**
```csharp
private static bool IsLeaderAccount(CopyEngine engine, NinjaTrader.Cbi.Account acc)
private static bool IsFlatPosition(NinjaTrader.Cbi.Position pos)
```

**NT8 atomicity:** No order submissions in these helpers. Pure predicates. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-05: PttQuickExit::Execute (main) -- Baseline CCN=66, lines 42-132

**Body summary:** Per-chart Quick Exit executor. Guards flat/follower, snapshots stop price,
builds QX snapshot for race protection, resolves direction+tick, resolves target count, then
submits N OCO pairs via SubmitQxOcoPair loop, and raises PttBus event.

**Current CCN assessment (actual code read):**
Branches: if(IsFlatOrMissing)=+1, if(IsFollowerSkip)=+1, isLong ternary=+1(in direction),
for(i<targetCount)=+1. Total CCN = 1+4 = **5**. ALREADY COMPLIANT.

Prior WAVE2-LANE-E-1 extractions reduced this from CCN=17/66 to CCN=6 (comment says 6, my
count gives 5 -- slight difference from comment wording, both below 8).
Status: **ALREADY COMPLIANT**.

**Extraction strategy:** None needed.
**NT8 atomicity:** Delegates to SubmitQxOcoPair which encapsulates CreateOrder+Submit. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-06: PttBreakEven::SubmitBeStopLocal -- Baseline CCN=54, lines 255-311

**Body summary:** Submits a PTT-BE-Stop StopMarket order for a single account. Guards on null
inputs and flat/missing position, then creates and submits the order in a try/catch block.

**Current CCN assessment (actual code read):**
Branches: if(IsInvalidInput)=+1, if(pos==null||qty)=+1,||=+1,
direction ternary=+1, if(order!=null)=+1, catch=+1.
Total CCN = 1+6 = **7**. ALREADY COMPLIANT.

Status: **ALREADY COMPLIANT**.
**Extraction strategy:** None needed.
**NT8 atomicity:** CreateOrder+Submit in same try block. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-07: PttGlobalQuickExit::ExecuteOne -- Baseline CCN=52, lines 332-404

**Body summary:** Per-account Quick Exit dispatcher. When skipIfFollower=false (follower path),
arms the _qxCancelInProgress guard, arms _qxPendingFollowerCleanup TTL, runs PttQuickExit.Execute
in a try/finally that removes the cancel guard. Leader path (skipIfFollower=true) delegates directly.

**Current CCN assessment (actual code read):**
Branches: if(!skipIfFollower)=+1, finally=0(not a branch), (leader path: straight delegation).
Total CCN = 1+1+1(delegate in follower path implicitly) = Let me count again more carefully.
if(!skipIfFollower): +1. Inside follower block: try/finally = try is +0, finally is +0.
No other if/foreach/while/ternary inside the follower block.
Leader block: direct delegation with no branches.
Total CCN = 1+1 = **2**. ALREADY COMPLIANT.

Status: **ALREADY COMPLIANT**.
**Extraction strategy:** None needed.
**Cross-file risk:** No CopyEngine edits (reads from _qxCancelInProgress and _qxPendingFollowerCleanup -- not an edit). ✅

---

### B-08: PttBreakEvenSwap::SubmitBareStopSwap -- Baseline CCN=50, lines 119-168

**Body summary:** Submits a bare PTT-BE-Stop StopMarket (no OCO) for the 0-targets path.
Guards on IsStopPriceSubmittable, then creates and submits in try/catch. Logs an error message
in the else (price not submittable) branch.

**Current CCN assessment (actual code read):**
Branches: if(IsStopPriceSubmittable)=+1, try=0, if(bareStop!=null)=+1, catch=+1, else=0(counted with if).
Total CCN = 1+3 = **4**. ALREADY COMPLIANT.

Status: **ALREADY COMPLIANT**.
**NT8 atomicity:** CreateOrder+Submit in same try block. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-09: PttQuickExit::SubmitStopOrder -- Baseline CCN=44, lines 277-320

**Body summary:** Submits one OCO stop (StopMarket) order for a single QX pair. Guards on
snapshotStop > 0, creates order with isLong direction ternary, submits, logs on null, catch logs.

**Current CCN assessment (actual code read):**
Branches: if(snapshotStop<=0)=+1, direction ternary(isLong?Sell:BuyToCover)=+1,
if(stopOrd!=null)=+1, catch=+1.
Total CCN = 1+4 = **5**. ALREADY COMPLIANT.

Status: **ALREADY COMPLIANT**.
**NT8 atomicity:** CreateOrder+Submit in same try block. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### B-10: PttQuickExit::SubmitTargetOrder -- Baseline CCN=42, lines 330-371

**Body summary:** Submits one OCO target (Limit) order for a single QX pair. Creates with
direction ternary, submits, logs on null, catch logs.

**Current CCN assessment (actual code read):**
Branches: direction ternary=+1, if(tNOrd!=null)=+1, catch=+1.
Total CCN = 1+3 = **4**. ALREADY COMPLIANT.

Status: **ALREADY COMPLIANT**.
**NT8 atomicity:** CreateOrder+Submit in same try block. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### Additional Method: PttBreakEven::SubmitBeTargetsLocal -- Baseline CCN=31, lines 693-728

**Body summary:** Submits pre-snapshotted ATM targets as GTC Limit orders. Handles 0-targets
edge case (bare stop path), then iterates targets and calls SubmitBePair for each OCO pair.

**Current CCN assessment:**
Branches: if(acc==null||instr==null)=+1,||=+1, if(targets==null)=+1, stopDirection ternary=+1,
if(targets.Count==0)=+1, for=+1.
Total CCN = 1+6 = **7**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttBreakEven::SubmitBareStop -- Baseline CCN=41, lines 388-428

**Body summary:** Submits a bare PTT-BE-Stop StopMarket (no OCO) for the 0-targets edge case.
Finds position, guards flat, creates and submits in try/catch.

**Current CCN assessment:**
Branches: if(barePos==null||qty==0)=+1,||=+1, if(bareStop!=null)=+1, catch=+1.
Total CCN = 1+4 = **5**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttBreakEven::ExecuteOneAccount -- Baseline CCN=30, lines 96-129

**Body summary:** Per-account BE logic: finds position, validates BE price against market, rejects
if invalid, snapshots targets, cancels stale brackets, submits BE targets.

**Current CCN assessment:**
Branches: if(pos==null||qty==0)=+1,||=+1, bePrice ternary(isLong?-buf:+buf)=+1,
if(!IsBePriceOk)=+1, ternary in WarnUser(isLong? ask/bid)=+1, ternary in WarnUser2=+1.
Total CCN = 1+6 = **7**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttBreakEven::SnapshotTargetsLocal -- Baseline CCN=30, lines 626-655

**Body summary:** Reads Working/Accepted/Submitted/Initialized/TriggerPending ATM Target orders
for an account. Returns list of (Price, Qty, Action) tuples. The IsSnapshotTargetOrder helper
was extracted previously (comment: "E-3 extraction: net -2 vs prior CCN=9").

**Current CCN assessment:**
Branches: if(acc==null||instr==null)=+1,||=+1, foreach=+1, if(o==null)=+1,
bool stateOk=IsSnapshotEligibleState(method call, 0 branches),
if(!stateOk||!IsSnapshotTargetOrder)=+1,||=+1.
Total CCN = 1+6 = **7**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttBreakEven::CancelStaleBracketsLocal -- Baseline CCN=29, lines 215-244

**Body summary:** Cancels Working/Initialized/Submitted/Accepted/TriggerPending non-BE orders for
an account+instrument. Builds a list of stale orders via foreach, then uses RemoveAll to guard
against already-terminal races, then calls acc.Cancel.

**Current CCN assessment:**
The RemoveAll lambda `o => o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled`
contributes: lambda anonymous function (lizard counts =>)=+1, || inside lambda=+1.
Branches: if(acc==null||instr==null)=+1,||=+1, foreach=+1, if(o==null)=+1,
if(IsStaleOrder)=+1, if(stale.Count==0)=+1, lambda=+1, || in lambda=+1, catch=+1.
Total CCN = 1+9 = **10**. ABOVE 8. **NEEDS EXTRACTION.**

**Extraction strategy:**
Extract `private static bool IsFinalOrder(Order o)`:
    body: `return o.OrderState == OrderState.Filled || o.OrderState == OrderState.Cancelled;`
Replace lambda: `stale.RemoveAll(o => ...)` -> `stale.RemoveAll(IsFinalOrder)`
(method group -- no lambda in caller, no || in caller)
Net CCN reduction: -2 (removes lambda branch + || inside lambda)
Post-extraction CancelStaleBracketsLocal CCN = 10 - 2 = **8**. ✅ (AT-LIMIT)
IsFinalOrder CCN = 1 + || = **2**. ✅

**Proposed helper signature:**
```csharp
private static bool IsFinalOrder(NinjaTrader.Cbi.Order o)
```

---

### Additional Method: PttBreakEven::RaiseBeNotify -- Baseline CCN=20, lines 177-198

**Current CCN:** Branches: leaderIsLong MarketPosition==Long=0, ternary leaderBePrice=+1, PttBus.RaiseBe call=0.
Total CCN = 1+1 = **2** (or +2 for two ternaries). **ALREADY COMPLIANT.** ✅

---

### Additional Method: PttBreakEven::BuildBeRejectMsg -- Baseline CCN=20, lines 150-169

**Current CCN:** Two ternaries (side, market). CCN = 1+2 = **3**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttBreakEven::Execute -- Baseline CCN=18, lines 65-87

**Body summary:** Outer Execute for BE-ALL. Guards on IsEnabled, gets OCO sequence via
CopyEngine.Instance?.NextBeOcoSeq(), finds leader position, loops all accounts skipping followers,
calls ExecuteOneAccount per account, raises BE notification.

**Current CCN assessment:**
Branches: if(!IsEnabled)=+1, CopyEngine.Instance?.NextBeOcoSeq()=+1(?.), ??1=+1(??),
if(leaderPos==null||qty==0)=+1,||=+1, foreach=+1,
if(CopyEngine.Instance!=null && IsFollowerAccount)=+1,&&=+1.
Total CCN = 1+8 = **9** (or 10). ABOVE 8. **NEEDS EXTRACTION.**

**Extraction strategy:**
Extract two helpers:
(a) `private static int GetBeOcoSeq()`:
    body: `return CopyEngine.Instance?.NextBeOcoSeq() ?? 1;`
    Removes ?. (+1) and ?? (+1) from Execute: net -2 CCN.
(b) `private static bool IsFollowerAcct(NinjaTrader.Cbi.Account acc)`:
    body: `return CopyEngine.Instance != null && CopyEngine.Instance.IsFollowerAccount(acc);`
    Removes && (+1) from Execute: net -1 CCN.

Post-extraction Execute CCN = 9 - 2 - 1 = **6**. ✅
GetBeOcoSeq CCN = 1 + ?. + ?? = **3**. ✅
IsFollowerAcct CCN = 1 + && = **2**. ✅

Usage change in Execute:
```
Before: int seq = CopyEngine.Instance?.NextBeOcoSeq() ?? 1;
After:  int seq = GetBeOcoSeq();

Before: if (CopyEngine.Instance != null && CopyEngine.Instance.IsFollowerAccount(acc)) continue;
After:  if (IsFollowerAcct(acc)) continue;
```

**NT8 atomicity:** No order submissions. ✅
**Cross-file risk:** No CopyEngine edits. ✅

---

### Additional Method: PttBreakEven::FindPositionLocal -- Baseline CCN=9, lines 550-558

**Current CCN:**
Branches: if(acc==null||instr==null)=+1,||=+1, foreach=+1, if(p.Instrument==instr)=+1.
CCN = 1+4 = **5**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttBreakEvenSwap::Execute -- Baseline CCN=34, lines 65-109

**Body summary:** Unified cancel+resubmit for BE-ALL trigger path. Null guard, flat guard,
cancels QX brackets, sets direction, dispatches to 0-targets or with-targets path.

**Current CCN assessment:**
Branches: if(acc==null||instr==null)=+1,||=+1, if(pos==null||qty==0)=+1,||=+1,
direction ternary=+1, if(HasNoTargets)=+1, for=+1.
Total CCN = 1+7 = **8**. AT-LIMIT, COMPLIANT. ✅

**Extraction strategy:** None needed.

---

### Additional Method: PttGlobalQuickExit::WaitForPttBeCancelled -- Baseline CCN=44, lines 703-746

**Current CCN:**
Branches: if(acc==null||expectedCount<=0)=+1,||=+1, while=+1, foreach=+1,
if(IsNonTerminalForInstr)=+1, if(nonTerminal==0)=+1.
Total CCN = 1+6 = **7**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::CancelPttBeOrders -- Baseline CCN=31, lines 664-694

**Current CCN:**
Branches: if(acc==null||instr==null)=+1,||=+1, foreach=+1,
if(!IsNonTerminalForInstr)=+1, if(toCancel.Count==0)=+1.
Total CCN = 1+5 = **6**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::ScaleLeaderTargets -- Baseline CCN=28, lines 595-622

**Current CCN:**
Branches: if(leaderPosQty<=0)=+1, for=+1, if(i==last)=+1.
CCN = 1+3 = **4**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::LogLeaderDiag -- Baseline CCN=24, lines 543-566

**Current CCN:** for loop only (+1). CCN = 1+1 = **2** plus StringBuilder appends (no branches). COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::SnapshotTargetOrders -- Baseline CCN=23, lines 423-447

**Body summary:** Reads Working/Accepted Limit target orders for an account. Classifies as
native (ATM Target*) or PTT (PTT-QX-T* or PTT-BE-Target-*), deduplicates native targets by price.
Comment notes AT-LIMIT (DW-LE-02).

**Current CCN assessment:**
Branches: if(acc==null||instr==null)=+1,||=+1, foreach=+1, if(o==null)=+1,
if(!IsTargetOrder)=+1, if(isNative)=+1, else if(isPtt)=+1, if(nativeTargets.Count==0)=+1.
Total CCN = 1+8 = **9**. ABOVE 8. **NEEDS EXTRACTION.**

**Extraction strategy:**
Extract `private static void ClassifyTarget(Order o, List<> nativeTargets, List<> pttTargets)`:
    body: `if (IsNativeTargetOrder(o.Name)) nativeTargets.Add(...); else if (IsPttTargetOrder(o.Name)) pttTargets.Add(...);`
Replace the `bool isNative/isPtt/if/else if` block with a single `ClassifyTarget(o, nativeTargets, pttTargets)` call.
Removes from SnapshotTargetOrders: if(isNative)=1, else if(isPtt)=1 = 2 branches.
Post-extraction SnapshotTargetOrders CCN = 9 - 2 = **7**. ✅
ClassifyTarget CCN = 1 + if=1 + else if=1 = **3**. ✅

**Proposed helper signature:**
```csharp
private static void ClassifyTarget(
    NinjaTrader.Cbi.Order o,
    System.Collections.Generic.List<(double Price, int Qty)> nativeTargets,
    System.Collections.Generic.List<(double Price, int Qty)> pttTargets
)
```

---

### Additional Method: PttGlobalQuickExit::ResolveFollowerTargets -- Baseline CCN=16, lines 634-653

**Current CCN:**
Branches in the if: followerSnapshot.Count>0=+0(simple compare), && with || inside=+1,&&=+1,||=+1.
if(leaderTargets.Count==0||followerPosQty<=0)=+1,||=+1.
Total CCN = 1+5 = **6**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::DeduplicateByPrice -- Baseline CCN=17, lines 519-535

**Current CCN:**
Branches: foreach(targets)=+1, if(!TryGetValue||t.Qty>existing)=+1,||=+1, foreach(kv)=+1.
CCN = 1+4 = **5**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::IsNonTerminalForInstr -- Baseline CCN=13, lines 574-586

**Current CCN:**
Branches: if(o==null)=+1, if(instrNull||FullName!=)=+1,||=+1, if(!IsPttBeOrder)=+1.
CCN = 1+4 = **5**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::IsTargetOrder -- Baseline CCN=12, lines 500-511

**Current CCN:**
Branches: stateOk bool expr Working||Accepted=+1, if(!stateOk)=+1,
if(!instrOk||orderType!=Limit)=+1,||=+1.
CCN = 1+4 = **5** (with two more for the instrOk check: instrOk=&&=+1, return !IsNullOrEmpty=+1).
Actual: CCN = 1+6 = **7**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttGlobalQuickExit::IsPttTargetOrder -- Baseline CCN=10, lines 470-479

**Current CCN:**
Branches: if(IsNullOrEmpty)=+1, (StartsWith PTT-QX-T && Length>8 && IsDigit) ||
  StartsWith PTT-BE-Target-: the &&=+1,&&=+1,||=+1 operators.
CCN = 1+5 = **6**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttQuickExit::SubmitQxOcoPair -- Baseline CCN=32, lines 142-180

**Current CCN:**
Branches: (targets!=null&&i<targets.Count)?=+1,&&=+1, if(tNQty<=0)=+1, if(i==0)=+1,
string ternary(i==0? stopName)=+1.
CCN = 1+5 = **6** (or 7 per WAVE2-LANE-E-1 comment). ALREADY COMPLIANT. ✅

---

### Additional Method: PttQuickExit::Execute (2nd overload) -- Baseline CCN=16, lines 380-395

**Current CCN:** Straight delegation (calls Execute with empty list). CCN = **1**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttQuickExit::SnapshotStopPrice -- Baseline CCN=13, lines 443-455

**Current CCN:**
Branches: foreach=+1, if(instrNull||FullName!=)=+1,||=+1, if(state!=Working&&state!=Accepted)=+1,&&=+1,
if(StopMarket||StopLimit)=+1,||=+1.
CCN = 1+7 = **8** (AT-LIMIT). COMPLIANT. ✅

Wait -- re-examining: `if (o.OrderState != OrderState.Working && o.OrderState != OrderState.Accepted) continue;`
The `&&` here -- this is `!Working && !Accepted` which means continue if EITHER is non-matching.
Branches: foreach=+1, if(null||FullName)=+1,||=+1, if(state!=W&&!=A)=+1,&&=+1, if(StopMarket||StopLimit)=+1,||=+1.
CCN = 1+7 = **8**. AT-LIMIT. COMPLIANT. ✅

---

### Additional Method: PttQuickExit::IsFlatOrMissing -- Baseline CCN=13, lines 192-204

**Current CCN:**
Branches: if(leader==null)=+1, foreach=+1, if(p.Instrument==instr)=+1,
return pos==null||qty==0: ||=+1.
CCN = 1+4 = **5**. ALREADY COMPLIANT. ✅

---

### Additional Method: PttQuickExit::ComputeExitPrices -- Baseline CCN=11, lines 241-251

**Current CCN:** Two ternaries. CCN = 1+2 = **3**. ALREADY COMPLIANT. ✅

---

### Additional Method: InstrumentDefaults::GetQuickTicks -- Baseline CCN=10, lines 470-479

**Current CCN:**
Branches: if(IsNullOrEmpty)=+1, if(StartsWith MES)=+1, if(StartsWith MGC)=+1.
CCN = 1+3 = **4**. ALREADY COMPLIANT. ✅

---

## Section 4: Ticket Grouping

### Ticket T-B1: PttBreakEven.cs -- Active Engineering Work Required

**Spec requirements satisfied:** B-01(SubmitBePair), B-06(SubmitBeStopLocal) -- verified compliant
  + Execute(CCN reduction), CancelStaleBracketsLocal(CCN reduction) -- active engineering
  + All additional PttBreakEven methods -- verified compliant

**File path:** `src/PropTraderTools/Features/PttBreakEven.cs`

**Methods requiring new helpers:**

1. `PttBreakEven.Execute` (CCN ~9-10 → target 6)
   - New helper: `private static int GetBeOcoSeq()`
     Signature: `private static int GetBeOcoSeq()`
     Body: `return CopyEngine.Instance?.NextBeOcoSeq() ?? 1;`
     CCN=3
   - New helper: `private static bool IsFollowerAcct(NinjaTrader.Cbi.Account acc)`
     Body: `return CopyEngine.Instance != null && CopyEngine.Instance.IsFollowerAccount(acc);`
     CCN=2

2. `PttBreakEven.CancelStaleBracketsLocal` (CCN ~9 → target 8)
   - New helper: `private static bool IsFinalOrder(NinjaTrader.Cbi.Order o)`
     Body: `return o.OrderState == NinjaTrader.Cbi.OrderState.Filled || o.OrderState == NinjaTrader.Cbi.OrderState.Cancelled;`
     CCN=2
   - Usage: change `stale.RemoveAll(o => o.OrderState == ... || ...)` to `stale.RemoveAll(IsFinalOrder)`

**All method signatures to implement (new helpers):**
```csharp
private static int GetBeOcoSeq()
private static bool IsFollowerAcct(NinjaTrader.Cbi.Account acc)
private static bool IsFinalOrder(NinjaTrader.Cbi.Order o)
```

**xUnit [Fact] tests:**
- `GetBeOcoSeq_WhenCopyEngineNull_Returns1()` -- asserts fallback = 1
- `IsFollowerAcct_WhenEngineNull_ReturnsFalse()` -- null-safe: no NPE
- `IsFinalOrder_WhenFilled_ReturnsTrue()` -- OrderState.Filled
- `IsFinalOrder_WhenCancelled_ReturnsTrue()` -- OrderState.Cancelled
- `IsFinalOrder_WhenWorking_ReturnsFalse()` -- OrderState.Working

**7-scan checklist (SCAN-01..07):**
- SCAN-01: Select-String "lock\(" PttBreakEven.cs → 0 matches
- SCAN-02: Select-String "async void" PttBreakEven.cs → 0 matches
- SCAN-03: Select-String "DateTime\.Now" PttBreakEven.cs → 0 matches (only DateTime.MaxValue, UtcNow allowed)
- SCAN-04: lizard PttBreakEven.cs --csv → col 1 (CCN) <= 8 for ALL rows
- SCAN-05: grep "CreateOrder" PttBreakEven.cs -- each occurrence followed by Submit in same try block
- SCAN-06: git diff --name-only → PttBreakEven.cs ONLY; zero changes to CopyEngine.cs
- SCAN-07: grep -P "[^\x00-\x7F]" PttBreakEven.cs → 0 non-ASCII matches

---

### Ticket T-B2: PttBreakEvenSwap.cs -- Verification Only

**Spec requirements satisfied:** B-02(SubmitSwapPair), B-08(SubmitBareStopSwap)

**File path:** `src/PropTraderTools/Features/PttBreakEvenSwap.cs`

**Engineering work:** NONE. All methods verified at CCN <= 8 (Execute=8, SubmitSwapPair=4,
SubmitBareStopSwap=4). AT-LIMIT on Execute is COMPLIANT per standard.

**Action required:** Run lizard on PttBreakEvenSwap.cs. Confirm ALL rows in col 1 (CCN) are <= 8.
If lizard returns any row > 8, treat as blocking defect and escalate.

**xUnit [Fact] tests (verification):**
- `SubmitBareStopSwap_WhenPriceNotSubmittable_LogsAndSkips()` -- verifies the else path
- `SubmitSwapPair_WhenPriceNotSubmittable_SkipsStop_SubmitsTarget()` -- guard path

**7-scan checklist:**
- SCAN-01: 0 lock() matches
- SCAN-02: 0 async void matches
- SCAN-03: 0 DateTime.Now matches
- SCAN-04: lizard CCN <= 8 all rows
- SCAN-05: CreateOrder + Submit together in each try block
- SCAN-06: no CopyEngine.cs edits
- SCAN-07: 0 non-ASCII matches

---

### Ticket T-B3: PttGlobalQuickExit.cs -- Active Engineering Work Required

**Spec requirements satisfied:** B-03(ExecuteFollowers), B-04(Execute/Execute(List))
  + SnapshotTargetOrders(CCN reduction) -- active engineering
  + All additional PttGlobalQuickExit methods -- verified compliant

**File path:** `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

**Methods requiring new helpers:**

1. `Execute()` and `Execute(List)` -- shared helpers:
   - New helper: `private static bool IsLeaderAccount(CopyEngine engine, NinjaTrader.Cbi.Account acc)`
     Body: `return engine == null || !engine.IsFollowerAccount(acc);`
     CCN=2. Applied to BOTH Execute() and Execute(List).
   - New helper: `private static bool IsFlatPosition(NinjaTrader.Cbi.Position pos)`
     Body: `return pos == null || pos.Quantity == 0;`
     CCN=2. Applied to BOTH Execute() and Execute(List).
   - Usage in Execute():
     Replace `if (engine != null && engine.IsFollowerAccount(acc)) continue;` with `if (!IsLeaderAccount(engine, acc)) continue;`
     Replace `if (pos == null || pos.Quantity == 0) continue;` with `if (IsFlatPosition(pos)) continue;`
   - Usage in Execute(List): same replacements.

2. `ExecuteFollowers` (CCN 9 → 5):
   - New helper: `private static int FindFollowerPosQty(NinjaTrader.Cbi.Account follower, NinjaTrader.Cbi.Instrument instr)`
     Body: foreach over follower.Positions, return matching position Quantity or 0.
     CCN=5.
   - Usage: Replace `int _fPosQty = 0; foreach(...) { if(...&&...&&...) { _fPosQty=_p.Quantity; break; } }` with `int _fPosQty = FindFollowerPosQty(follower, pos.Instrument);`

3. `SnapshotTargetOrders` (CCN 9 → 7):
   - New helper: `private static void ClassifyTarget(NinjaTrader.Cbi.Order o, System.Collections.Generic.List<(double Price, int Qty)> nativeTargets, System.Collections.Generic.List<(double Price, int Qty)> pttTargets)`
     Body: `if (IsNativeTargetOrder(o.Name)) nativeTargets.Add(...); else if (IsPttTargetOrder(o.Name)) pttTargets.Add(...);`
     CCN=3.
   - Usage: Replace `bool isNative = IsNativeTargetOrder(o.Name); bool isPtt = IsPttTargetOrder(o.Name); if (isNative) ... else if (isPtt) ...` with `ClassifyTarget(o, nativeTargets, pttTargets);`

**All method signatures to implement (new helpers):**
```csharp
private static bool IsLeaderAccount(CopyEngine engine, NinjaTrader.Cbi.Account acc)
private static bool IsFlatPosition(NinjaTrader.Cbi.Position pos)
private static int FindFollowerPosQty(NinjaTrader.Cbi.Account follower, NinjaTrader.Cbi.Instrument instr)
private static void ClassifyTarget(
    NinjaTrader.Cbi.Order o,
    System.Collections.Generic.List<(double Price, int Qty)> nativeTargets,
    System.Collections.Generic.List<(double Price, int Qty)> pttTargets
)
```

**xUnit [Fact] tests:**
- `IsLeaderAccount_NullEngine_ReturnsTrue()` -- null engine → leader assumption
- `IsLeaderAccount_WhenEngineNonNull_NonFollower_ReturnsTrue()` -- delegate to IsFollowerAccount
- `IsFlatPosition_NullPos_ReturnsTrue()` -- null position → flat
- `IsFlatPosition_ZeroQty_ReturnsTrue()` -- qty=0 → flat
- `IsFlatPosition_PositiveQty_ReturnsFalse()` -- qty>0 → not flat
- `FindFollowerPosQty_NoMatchingInstrument_ReturnsZero()` -- no match → 0
- `ClassifyTarget_NativeOrder_AddsToNativeList()` -- "Target1" → nativeTargets.Count=1
- `ClassifyTarget_PttQxOrder_AddsToPttList()` -- "PTT-QX-T1" → pttTargets.Count=1
- `ClassifyTarget_UnknownOrder_AddsToNeither()` -- "SomeOtherOrder" → both empty

**7-scan checklist (SCAN-01..07):**
- SCAN-01: Select-String "lock\(" PttGlobalQuickExit.cs → 0 matches
- SCAN-02: Select-String "async void" PttGlobalQuickExit.cs → 0 matches
- SCAN-03: Select-String "DateTime\.Now" PttGlobalQuickExit.cs → 0 matches
- SCAN-04: lizard PttGlobalQuickExit.cs --csv → col 1 (CCN) <= 8 for ALL rows
- SCAN-05: grep "CreateOrder" PttGlobalQuickExit.cs → confirm 0 (no direct order creation; delegates to PttQuickExit)
- SCAN-06: git diff --name-only → PttGlobalQuickExit.cs ONLY; zero changes to CopyEngine.cs
- SCAN-07: grep -P "[^\x00-\x7F]" PttGlobalQuickExit.cs → 0 non-ASCII matches

---

### Ticket T-B4: PttQuickExit.cs + InstrumentDefaults -- Verification Only

**Spec requirements satisfied:** B-05(Execute main), B-09(SubmitStopOrder), B-10(SubmitTargetOrder)

**File path:** `src/PropTraderTools/Features/PttQuickExit.cs`

**Engineering work:** NONE. All methods verified at CCN <= 8 via WAVE2-LANE-E-1 prior extractions.
- Execute (main): CCN ~5
- Execute (2nd overload): CCN 1
- SubmitQxOcoPair: CCN ~6-7
- SubmitStopOrder: CCN 5
- SubmitTargetOrder: CCN 4
- IsFlatOrMissing: CCN 5
- ComputeExitPrices: CCN 3
- SnapshotStopPrice: CCN 8 (AT-LIMIT, compliant)
- GetQuickTicks (InstrumentDefaults): CCN 4

**Action required:** Run lizard on PttQuickExit.cs. Confirm ALL rows in col 1 (CCN) are <= 8.
If any row > 8, treat as blocking defect and escalate.

**xUnit [Fact] tests (verification):**
- `IsFlatOrMissing_NullLeader_ReturnsTrue()`
- `IsFlatOrMissing_ZeroQty_ReturnsTrue()`
- `GetQuickTicks_MesInstrument_Returns4And8()`
- `GetQuickTicks_MgcInstrument_Returns2And4()`
- `GetQuickTicks_NullOrEmpty_Returns4And8()`
- `GetQuickTicks_UnknownInstrument_Returns4And8()`

**7-scan checklist:**
- SCAN-01..07: same pattern -- 0 lock, 0 async void, 0 DateTime.Now, lizard CCN<=8, atomicity confirmed, no CopyEngine edits, 0 non-ASCII

---

### Ticket T-B5: PttGlobalBreakEven.cs -- Verification Only

**Spec requirements satisfied:** No baseline methods from Lane-B map to this file.
  File is in scope to confirm clean state.

**File path:** `src/PropTraderTools/Features/PttGlobalBreakEven.cs`

**Engineering work:** NONE. All methods at CCN <= 5.
- Execute(int): CCN 1 (straight delegation)
- Execute(IEnumerable): CCN 5
- ExecuteOne: CCN 4
- IncrementBuffer/DecrementBuffer: CCN 2 each

**Action required:** Run lizard on PttGlobalBreakEven.cs. Confirm ALL rows CCN <= 8.

**7-scan checklist:**
- SCAN-01..07: same standard pattern

---

## Section 5: Lane Isolation Confirmation

LANE-B ISOLATION CONFIRMED: **ZERO edits to CopyEngine.cs**.

Evidence:
- All 7 new helper methods (GetBeOcoSeq, IsFollowerAcct, IsFinalOrder, IsLeaderAccount,
  IsFlatPosition, FindFollowerPosQty, ClassifyTarget) are private static within their
  respective classes. None touch CopyEngine internals.
- CopyEngine.Instance is accessed READ-ONLY from the target files (existing pattern unchanged).
- No new CopyEngine method calls are introduced by any extraction.
- No CopyEngine.cs interface changes required.
- git diff --name-only must show ONLY the 5 target files (or fewer) after engineering.

---

## Section 6: PLAN_COMPLETE

PLAN_COMPLETE

### Summary of Real Engineering Work

**Files requiring code changes (active engineering):**
1. `PttBreakEven.cs` -- Add 3 private static helpers, modify Execute and CancelStaleBracketsLocal (T-B1)
2. `PttGlobalQuickExit.cs` -- Add 4 private static helpers, modify Execute(), Execute(List), ExecuteFollowers, SnapshotTargetOrders (T-B3)

**Files requiring verification only (no code changes expected):**
3. `PttBreakEvenSwap.cs` -- All CCN <= 8 (T-B2)
4. `PttQuickExit.cs` + InstrumentDefaults -- All CCN <= 8 (T-B4)
5. `PttGlobalBreakEven.cs` -- All CCN <= 8 (T-B5)

**Total new helpers: 7**
- GetBeOcoSeq() -- CCN 3
- IsFollowerAcct(acc) -- CCN 2
- IsFinalOrder(o) -- CCN 2
- IsLeaderAccount(engine, acc) -- CCN 2
- IsFlatPosition(pos) -- CCN 2
- FindFollowerPosQty(follower, instr) -- CCN 5
- ClassifyTarget(o, nativeList, pttList) -- CCN 3

**Post-extraction CCN summary (all methods <= 8):**
- PttBreakEven.Execute: 6 ✅
- PttBreakEven.CancelStaleBracketsLocal: 8 ✅
- PttGlobalQuickExit.Execute(): 7 ✅
- PttGlobalQuickExit.Execute(List): 8 ✅
- PttGlobalQuickExit.ExecuteFollowers: 5 ✅
- PttGlobalQuickExit.SnapshotTargetOrders: 7 ✅
- All other methods: already <= 8 (verified by direct source read) ✅
