# B198 Architecture Plan
**Author**: ptt-architect (Phase 1)
**Date**: 2026-10-13
**Epic**: B198 -- SimStop N-bracket generalisation + QTY instrument + fo=NULL drain + ATM dir fix
**Preceding epic**: B197 (FINAL_PASS 2026-10-12)
**Pre-pipeline diagnosis**: docs/brain/B198-diagnosis.md (pre-confirmed facts; not re-investigated here)

---

## RULES CATALOG GATE RESULT: PASS

Rules read: docs/standards/jane-street/RULES_CATALOG.md
P0 rules scoped to B198: JS-021 (no lock), JS-001 (no throw in hot path), JS-033 (no async void), JS-002 (no return null for missing values -- existing contract where null is the documented absence signal is exempt).
JS-066 (CYC <= 8) applies to every method touched.
P0 scan on files to be modified: zero existing lock(), async void, or hot-path throw in B198 change scope.
GATE RESULT: PASS

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

```
Q1: Same method or within 50 lines?
    T1: 10 methods across CopyEngine.cs (spread across file)
    T2: 1 method in CopyEngine.cs
    T3: 3 methods + 1 class + 1 new field in CopyEngine.cs
    T4: 1 method in CopyEngine.cs
    All 4 tickets edit the SAME FILE (CopyEngine.cs).
    LANE-SPLIT GATE RESULT: SINGLE-PIPELINE
    (4 tickets touch CopyEngine.cs -- no lane split possible)
```

---

## §1 Executive Summary

**Total tickets: 5** (T2 is split into T2a + T2b per architect split decision -- see §4).

| # | Ticket | Priority | DW Card | Split note |
|---|--------|----------|---------|------------|
| T1 | SimStop N-Bracket Generalisation | P0 | DW-B198-SIMSTP-01 | Part A: 6 predicates + B5 extra site. Part B: TryParseStopSuffix, CaptureOtherLegTargetPrices+ResubmitCollateralLegs, FindLeaderCollateralOrder, CaptureLinkedTargetPrice (auto-covered), FindLeaderStopPrice (newly discovered). |
| T2a | QTY Bug Instrumentation | P0 | DW-B198-QTY-01 | Add 2 log lines to SendCopyWithAtm. Run SIM. Record evidence. |
| T2b | QTY Bug Fix | P0 | DW-B198-QTY-01 | Implement fix based on T2a log evidence. Design covers H1/H2/H3. |
| T3 | fo=NULL on Drain Path | P1 | DW-B195-01 | Add _drainFollowerEntryOrders dict. Thread Order ref through SubmitDrainedEntry. |
| T4 | ATM Template Dir Startup Fix | P2 | DW-B198-ATMDIR-01 | Correct subpath in CheckAtmTemplatesForReverse. |

**Rationale for T2 split**: H2 is the most likely hypothesis (NT8 ignores passed qty, uses template per-group qty). H2 fix requires either ATM template XML group count parsing or a new UI config field. Both require a separate engineer session from the instrumentation. Architect recommends T2a (instrument + SIM run) → T2b (fix from evidence). Plan reviewer must confirm or deny this split.

**B198 files modified**: `src/PropTraderTools/CopyEngine.cs` (all 5 tickets).
**B198 test file**: `src/PropTraderTools/Tests/B198Tests.cs` (new file, T1+T2+T3+T4 tests).
**B198 artifacts dir**: `docs/brain/B198/`.

---

## §2 Pre-Pipeline Diagnosis Summary

Source: docs/brain/B198-diagnosis.md (do NOT re-investigate).

**MEGACOMMIT AUDIT**: SimStop support NEVER existed in any commit. Zero matches in `git log --all --oneline --grep="SimStop"`. DW-B198-SIMSTP-01 is a NEW FIX, not a restore.

**DW-B194-04 ALREADY DONE**: `_flattenFilledTicks` at CopyEngine.cs line 382. Debounce guard at FlattenOneAccount line 5363. Comment line 381 says "DW-B194-04: closed." No PTT-Flatten debounce ticket needed.

**Root cause of PTT-Flatten loop**: SimStop fills → IsNativeExitName("SimStop1")=false → DispatchCopy fires for SimStop fills → followers receive Sell copies → oversold → SHORT → NakedPositionDetector loops. Fixing IsNativeExitName (T1-A) alone stops the cascade.

**6 predicates confirmed missing SimStop** at exact lines (see §3 Part A table).

**4 additional hardcoded sites confirmed**:
- TryParseStopSuffix line 3382 `Substring(4)` + line 3383 `n > 3`
- CaptureOtherLegTargetPrices line 3350 `new double[3]` + line 3355 `i <= 3`
- ResubmitCollateralLegs line 3541 `i <= 3` (feeds otherLegPrices array -- must match CaptureOtherLegTargetPrices fix)
- FindLeaderCollateralOrder line 3409 builds `"Stop"+suffix` only
- FindLeaderStopPrice line 3890 builds `"Stop"+bracketIndex` only (NEWLY DISCOVERED by architect -- see §3 B5)

**QTY bug path confirmed**: DispatchCopy → TryLaneDHook → TryLaneDDispatchFollower → scaledSig.Quantity = baseQty * mult → SendCopyWithAtm → CreateOrder → StartAtmStrategy. NT8 behavior of StartAtmStrategy with qty=10 NOT documented. H2 (NT8 uses template per-group qty) is most likely.

**fo=NULL confirmed**: output.md line 95-96: `[TP4-SFB] acc=Sim102 fo=NULL followerOrders=[...]`. SyncFollowerBracket called after drain fill but bracket orders not yet present OR SimStop prefix not recognized. T1 fixes recognition; T3 fixes timing via order reference threading.

**ATM dir bug confirmed**: `[PTT-STARTUP-INFO] ATM template dir not found` fires on every startup. Root cause: path `Path.Combine(UserDataDir, "Templates", "ATM")` -- wrong subfolder name. Correct: `Path.Combine(UserDataDir, "templates", "AtmStrategy")`.

---

## §3 T1 SimStop N-Bracket Generalisation Design

### Part A: 6 Predicate Fixes

Each fix is a one-clause OR addition. CYC impact: +1 per method on entry-point branches. All stay <= 8. ASCII-only. No lock. No throw.

| Method | Line | Current (missing) | Fix to add | CYC before | CYC after |
|--------|------|-------------------|-----------|-----------:|----------:|
| `IsNativeExitName` | 2564 | No "SimStop" clause | `if (name.StartsWith("SimStop", StringComparison.Ordinal)) return true;` BEFORE `return false` | 4 | 5 |
| `IsAtmBracketName` | 858 | No "SimStop" clause | Add OR clause: `|| (name.StartsWith("SimStop", StringComparison.Ordinal) && name.Length > 7 && char.IsDigit(name[7]))` | 1 | 2 |
| `IsBracketLeg` | 5880 | No "SimStop" clause | Add to OR: `|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)` | 1 | 1 (compound OR, 0 McCabe delta on expression body) |
| `IsBracketLegStatic` | 5861 | No "SimStop" clause | Add to OR: `|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)` | 1 | 1 (compound OR, 0 McCabe delta) |
| `IsAtmSTPOrder` | 3044 | No "SimStop" clause | Add to OR: `|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)` | 1 | 1 (compound OR, 0 McCabe delta) |
| `IsStopLeg` | 5848 | No "SimStop" clause | Add to OR: `|| (order.Name != null && order.Name.StartsWith("SimStop", StringComparison.Ordinal))` | 1 | 1 (compound OR, 0 McCabe delta) |

**Effect of A (IsNativeExitName fix)**: This is the CRITICAL path fix. `TryDispatchLeaderFlat` gates on `IsNativeExitName` to bypass `DispatchCopy`. After fix, SimStop fills are treated as native exit events, not copy-triggering fills. Stops the double-Sell-copy cascade.

**A note on IsAtmBracketName**: The `char.IsDigit(name[7])` check correctly matches SimStop1..SimStop9 (single digit). For SimStop10+: `name[7]='1'` is a digit, `TryParseStopSuffix` handles the full numeric suffix. The char.IsDigit guard is prefix-discriminator only, not a completeness gate.

### Part B: Infrastructure Fixes

#### B1: TryParseStopSuffix (line 3377-3387) -- REPLACEMENT

**Current code** (CYC=3):
```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
{
    suffix = null;
    if (stopName == null || stopName.Length < 5) // (1) if
        return false;
    string raw = stopName.Substring(4);          // BROKEN: always slices at "Stop" prefix length
    if (!int.TryParse(raw, out int n) || n < 1 || n > 3) // (2) if -- n > 3 cap BROKEN
        return false;
    suffix = raw;
    return true;
}
```

**Replacement** (CYC=5):
```csharp
private static bool TryParseStopSuffix(string stopName, out string suffix)
{
    suffix = null;
    if (string.IsNullOrEmpty(stopName))                              // (1) if
        return false;
    int prefixLen;
    if (stopName.StartsWith("SimStop", StringComparison.Ordinal))    // (2) if
        prefixLen = 7;
    else if (stopName.StartsWith("Stop", StringComparison.Ordinal))  // (3) else if
        prefixLen = 4;
    else
        return false;
    if (stopName.Length <= prefixLen)                                 // (4) if
        return false;
    string raw = stopName.Substring(prefixLen);
    if (!int.TryParse(raw, out int n) || n < 1)                      // (5) if -- n > 3 cap REMOVED
        return false;
    suffix = raw;
    return true;
}
```

**CYC**: 3 → 5. Stays <= 8. JS-021: no lock. JS-001: no throw. ASCII-only.

**Impact cascade**: `CaptureLinkedTargetPrice` (line 3314) calls `TryParseStopSuffix` at line 3316 with leaderOrder.Name. After B1, `CaptureLinkedTargetPrice("SimStop1")` → suffix="1" → finds "Target1" on follower. **B4 is auto-covered by B1.** No separate B4 fix required.

Also covered: `ParseLegSuffix` (line 3393) is a thin wrapper over `TryParseStopSuffix` → also fixed by B1.

Also covered: `SyncFollowerBracket` line 3115 calls `TryParseStopSuffix(leaderOrder.Name, ...)` → legSuffix now correct for SimStop.

#### B2: CaptureOtherLegTargetPrices + ResubmitCollateralLegs -- N-BRACKET CAP REMOVAL

Two co-located fixes must be applied together to keep the array size and loop cap in sync.

**Site 1: CaptureOtherLegTargetPrices (line 3348)**:

Add `private const int MaxBracketLegs = 9;` at class-level (or method-level const). Replace:
- Line 3350: `new double[3]` → `new double[MaxBracketLegs]`
- Line 3351: `if (!fo.Name.StartsWith("Stop"))` → `if (!fo.Name.StartsWith("Stop") && !fo.Name.StartsWith("SimStop"))` (NEWLY required -- compound OR on existing branch, 0 McCabe delta, CYC stays 6)
- Line 3355: `for (int i = 1; i <= 3; i++)` → `for (int i = 1; i <= MaxBracketLegs; i++)`

**CYC impact**: 6 → 6. The StartsWith guard clause change is compound OR on existing if, 0 McCabe delta.

**Site 2: ResubmitCollateralLegs (line 3532)**:

The loop at line 3541 `for (int i = 1; i <= 3; i++)` iterates over `otherLegPrices[i-1]`. The array came from `CaptureOtherLegTargetPrices`. Both must use the same bound.

Replace:
- Line 3541: `for (int i = 1; i <= 3; i++)` → `for (int i = 1; i <= MaxBracketLegs; i++)`
- `otherLegPrices[i - 1]` -- no change to the indexing expression, but the receiving method `ResubmitCollateralLegs` signature receives `double[] otherLegPrices`. Since the array is now size MaxBracketLegs, the loop safely accesses `otherLegPrices[i-1]` for i=1..MaxBracketLegs. ✓

**CYC impact**: CYC of ResubmitCollateralLegs: unchanged (loop bound change, not a new branch).

**IMPORTANT**: `MaxBracketLegs = 9` should be declared at class scope (static readonly or const int) so it is shared by both methods. Place with other constants near the top of the class.

#### B3: FindLeaderCollateralOrder (line 3405-3416) -- SimStop Lookup Addition

**Current code** searches only "Stop"+suffix and "Target"+suffix.

Fix: add `string simStopName = "SimStop" + suffix;` and include it in the inner OR check:

```csharp
private static Order FindLeaderCollateralOrder(Order leaderOrder, string suffix)
{
    if (leaderOrder?.Account?.Orders == null || string.IsNullOrEmpty(suffix))
        return null;
    string stopName    = "Stop"    + suffix;
    string tgtName     = "Target"  + suffix;
    string simStopName = "SimStop" + suffix;          // NEW
    foreach (var o in leaderOrder.Account.Orders.ToList())
    {
        if (o != null && (o.Name == stopName || o.Name == tgtName || o.Name == simStopName)) // NEW: || simStopName
            return o;
    }
    return null;
}
```

**CYC impact**: 3 → 3. The `|| o.Name == simStopName` is added to an existing compound OR expression inside the `if` at branch (3). Compound OR on same boolean expression = 0 McCabe delta.

#### B5 (NEWLY DISCOVERED): FindLeaderStopPrice (line 3884) -- SimStop Stop Name Lookup

**Discovery**: This method was NOT in the pre-confirmed list but was found during mandatory grep. It builds `"Stop" + bracketIndex.ToString()` at line 3890 and searches leader account orders for a Working/Accepted order with that name. With SimStop ATM, the leader has "SimStop1", "SimStop2" (not "Stop1"). `FindLeaderStopPrice` returns 0.0 → `CreateFollowerReplacementStop` skips (stopPrice <= 0 guard) → follower replacement stop NOT created during SyncAtmFollowerTarget (Lane A target sync path).

**Fix**: also check `"SimStop" + bracketIndex.ToString()` in the same foreach:

```csharp
private static double FindLeaderStopPrice(Account? leaderAccount, int bracketIndex)
{
    if (leaderAccount == null)
        return 0.0;
    if (bracketIndex <= 0)
        return 0.0;
    var targetName    = "Stop"    + bracketIndex.ToString();
    var simStopName   = "SimStop" + bracketIndex.ToString();   // NEW
    foreach (var order in leaderAccount.Orders.ToList())
    {
        if (
            (order.Name == targetName || order.Name == simStopName)  // NEW: || simStopName
            && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted)
        )
            return order.StopPrice;
    }
    return 0.0;
}
```

**CYC impact**: 6 → 6. The `|| order.Name == simStopName` is compound OR on existing boolean expression = 0 McCabe delta on the branch count.

### T1 Test Cases

New file: `src/PropTraderTools/Tests/B198Tests.cs`

| # | Test name | Assertion |
|---|-----------|-----------|
| T1-01 | `IsAtmBracketName_SimStop1_ReturnsTrue` | `IsAtmBracketName("SimStop1") == true` |
| T1-02 | `IsAtmBracketName_SimStop9_ReturnsTrue` | `IsAtmBracketName("SimStop9") == true` |
| T1-03 | `IsAtmBracketName_SimStopNoDigit_ReturnsFalse` | `IsAtmBracketName("SimStop") == false` |
| T1-04 | `IsNativeExitName_SimStop1_ReturnsTrue` | `IsNativeExitName("SimStop1") == true` |
| T1-05 | `IsNativeExitName_SimStop99_ReturnsTrue` | `IsNativeExitName("SimStop99") == true` |
| T1-06 | `TryParseStopSuffix_SimStop1_ReturnsTrueS1` | source-text Assert.Contains: `StartsWith("SimStop", StringComparison.Ordinal)` in TryParseStopSuffix body |
| T1-07 | `TryParseStopSuffix_SimStop2_ReturnsTrueS2` | source-text Assert.Contains confirming SimStop branch exists |
| T1-08 | `TryParseStopSuffix_SimStop4_ReturnsTrueS4` | source-text Assert.Contains confirming `n > 3` cap removed |
| T1-09 | `TryParseStopSuffix_Stop4_ReturnsTrueS4` | source-text Assert.Contains confirming n > 3 cap removed |
| T1-10 | `TryParseStopSuffix_Stop3_RegressionTrue` | source-text Assert.Contains confirming Stop3 still valid |
| T1-11 | `TryParseStopSuffix_SimTarget1_ReturnsFalse` | source-text Assert.Contains confirming SimTarget prefix not matched |
| T1-12 | `MaxBracketLegs_ConstEquals9` | source-text Assert.Contains `MaxBracketLegs = 9` in CopyEngine.cs |
| T1-13 | `IsBracketLeg_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsBracketLeg` body |
| T1-14 | `IsBracketLegStatic_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsBracketLegStatic` body |
| T1-15 | `IsAtmSTPOrder_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsAtmSTPOrder` body |
| T1-16 | `IsStopLeg_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsStopLeg` body |

**Testability note**: `TryParseStopSuffix` is `private static`. `IsBracketLeg` is `private bool` (instance method). `IsBracketLegStatic` and `IsStopLeg` are `private static`. None of these can be called directly from xUnit without triggering the TypeInitializationException on CopyEngine's static initializer. `IsAtmSTPOrder` is `internal static` but takes an `Order` parameter requiring NT8 runtime — also not directly callable. All four T1-13..T1-16 test cases use `Assert.Contains` on the raw source text of `CopyEngine.cs`, loaded via `System.IO.File.ReadAllText`. Pattern established by B197Tests.cs. `IsAtmBracketName` and `IsNativeExitName` are `internal static` -- call directly if NT8 runtime is not required; otherwise use source-text assertion.

---

## §4 T2 QTY Bug Investigation + Fix Design

### T2a: Instrumentation Ticket

**Architect split decision: T2 SPLITS into T2a and T2b.**

Rationale: H2 (NT8 ignores passed qty, uses template per-group qty) is the most likely hypothesis per diagnosis §5. The H2 fix requires either reading ATM template XML for group count OR a new UI config field in TradeCopierPanel.cs. Both are non-trivial changes that cross the one-ticket-one-concern boundary. T2a can complete and SIM results reviewed before committing to T2b design. If H1 (no bug) is confirmed, T2b is cancelled.

**T2a scope**: Add 2 instrumentation log lines to `SendCopyWithAtm` (CopyEngine.cs line 4999). No logic change. CYC unchanged (0 McCabe delta on StatusUpdate invocations).

**Log line 1** (before `CreateOrder`, after function entry):
```csharp
StatusUpdate?.Invoke(
    follower.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName);
```
Insert immediately before `var order = follower.CreateOrder(...)` at line 5008.

**Log line 2** (after `CreateOrder` null check):
```csharp
if (order != null)
    StatusUpdate?.Invoke(follower.Name + ": ATM-order qty=" + order.Quantity);
```
Insert after the `if (order == null) return null;` guard (T3 changes return type -- see §5).

**SIM protocol**: Run one ATM entry with leader qty=10. Observe output log. Record ATM-send qty and ATM-order qty values.

**Expected log evidence per hypothesis**:
- H1: `ATM-send qty=10`, `ATM-order qty=10`, follower position = Long 10 → no fix needed, close DW-B198-QTY-01.
- H2: `ATM-send qty=10`, `ATM-order qty=5` → NT8 CreateOrder is reducing qty. Fix is in T2b.
- H3: `ATM-send qty=10`, `ATM-order qty=10`, follower position = Long 5 → NT8 StartAtmStrategy splitting. Fix is in T2b.

### T2b: Fix (dependent on T2a evidence)

**Precondition**: T2a SIM run complete, log evidence recorded in `docs/brain/B198/ticket-2a-sim-evidence.md`.

**H1 path** (qty=10 → Long 10): T2b ticket says "cannot reproduce. DW-B198-QTY-01 closed." No code change.

**H2 path** (ATM-order qty=5, NT8 CreateOrder reduces): The fix is to pass per-group qty rather than total leader qty. Per-group qty = total leader qty / numGroups. numGroups must be discovered.

Design for H2 fix:
- Extend `CheckAtmTemplatesForReverse(string atmDir)` to also parse `<Quantity>` from each template XML and store a mapping `templateName → perGroupQty` in a new field: `private readonly ConcurrentDictionary<string, int> _atmTemplateGroupQty` (new field, ConcurrentDictionary, no lock, JS-021 compliant).
- In `SendCopyWithAtm`, before building `signal`, look up `_atmTemplateGroupQty[namedMode.TemplateName]`. If found, use that as qty (overriding scaledSig.Quantity). If not found, use scaledSig.Quantity as before (backward-compatible fallback).
- CYC impact on `SendCopyWithAtm`: +1 (lookup branch). CYC: 4 → 5. <= 8. ✓
- CYC impact on `CheckAtmTemplatesForReverse(string)`: +1 (qty parse). CYC: 6 → 7. <= 8. ✓

**H3 path** (ATM-order qty=10, follower Long 5 from StartAtmStrategy): StartAtmStrategy internally splits into groups. PTT has no control over this. Fix would require matching template's group structure. This is complex: deferred to a future ticket if H3 is confirmed.

**T2b CYC table** (if H2):

| Method | File | Before | After |
|--------|------|--------|-------|
| `SendCopyWithAtm` | CopyEngine.cs | 4 | 5 |
| `CheckAtmTemplatesForReverse(string)` | CopyEngine.cs | 6 | 7 |

**T2b new field** (if H2): `private readonly ConcurrentDictionary<string, int> _atmTemplateGroupQty = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);`. Initialized inline at field declaration. No lock. JS-021 compliant.

---

## §5 T3 fo=NULL Drain Path Design

### Problem statement

`SyncFollowerBracket` logs `fo=NULL` for Sim102/Sim103 when called after a drain-path entry fills. The fix identified in B198-orchestrator-prompt: store the follower entry order reference created by `SendCopyWithAtm` so it is available when the drain fill event fires.

### Code analysis

**`SendCopyWithAtm` (line 4999)**: currently returns `bool`. Creates the entry order via `CreateOrder` and calls `StartAtmStrategy`. The created `Order` reference is not returned.

**`SubmitDrainedEntry` (line 7085)**: calls `TryRemove` FIRST (line 7087), then calls `SendCopyWithAtm`. After `TryRemove`, the payload is removed from `_pendingDispatchDrains`. Any Order reference we want to store for TrySetSlateReadyOnDrainFill cannot be stored in the already-removed payload.

**Threading analysis**: `SubmitDrainedEntry` is called from `OnDrainCancelAck` → `OnOrderUpdate` (NT8 dispatch thread, effectively single-threaded for a given account). No race on SubmitDrainedEntry for the same acctKey. However, `TrySetSlateReadyOnDrainFill` fires on the same NT8 dispatch thread (also from `OnOrderUpdate`), so these are effectively serialized. A separate `ConcurrentDictionary` for the Order reference is still the correct pattern (JS-021).

### Fix design

**New field**:
```csharp
// T3: tracks follower entry orders placed via drain path, keyed by followerAcctKey.
// Populated in SubmitDrainedEntry after SendCopyWithAtm. Removed on fill or abort.
// JS-021: ConcurrentDictionary -- no lock. JS-002: Order? nullable is intentional.
private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders =
    new ConcurrentDictionary<string, Order>(StringComparer.Ordinal);
```

**Change 1: `SendCopyWithAtm` return type** (CYC: 4, unchanged):

Change signature from `private bool SendCopyWithAtm(...)` to `private Order? SendCopyWithAtm(...)`.
- Return the created `order` on success (not `return true`).
- Return `null` on failure (not `return false`).
- All 6 call sites in CopyEngine.cs must be updated:
  - Line 2759: `bool ok = SendCopyWithAtm(...)` → `bool ok = (SendCopyWithAtm(...) != null)`
  - Line 2870: `bool ok = SendCopyWithAtm(...)` → `bool ok = (SendCopyWithAtm(...) != null)`
  - Line 2987: `SendCopyWithAtm(...)` (result unused) → no change needed (discard)
  - Line 4522: `SendCopyWithAtm(...)` (result unused) → no change needed (discard)
  - Line 6966: `SendCopyWithAtm(...)` (result unused) → no change needed (discard)
  - Line 7104: `SendCopyWithAtm(...)` → `Order? newOrder = SendCopyWithAtm(...)` (capture for storage below)

**Change 2: `SubmitDrainedEntry` (line 7085)** (CYC: 5 → 6):

After `SendCopyWithAtm` call on the Named ATM path (line 7104), store the returned Order:
```csharp
Order? newOrder = SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm);
if (newOrder != null)                                                         // NEW branch: CYC 5 -> 6
    _drainFollowerEntryOrders.TryAdd(acctKey, newOrder);
```

**CYC impact**: +1 branch (null check on newOrder). CYC: 5 → 6. <= 8. ✓

**Change 3: `TrySetSlateReadyOnDrainFill` (line 1070)** (CYC: 5, unchanged):

After setting `_followerSlateReady`, retrieve the stored follower entry order, remove it from the dict, and pass it as the `fo` argument to the existing `SyncFollowerBracket` call:

```csharp
_followerSlateReady[e.Order.Account.Name] = true;
// T3: retrieve and thread the stored follower entry order into SyncFollowerBracket.
// TryGetValue + TryRemove = 0 new branches; CYC stays at 5.
string acctKey = e.Order.Account.Name;
_drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo);   // no branch
_drainFollowerEntryOrders.TryRemove(acctKey, out _);                  // no branch
// Replace previous null argument with drainFo (may still be null if SendCopyWithAtm failed -- that is correct).
SyncFollowerBracket(acc, drainFo, ...);                               // pass drainFo instead of null
NinjaTrader.Code.Output.Process("[SLATE-READY-DRAIN] acct=" + acctKey, ...);
```

**CYC impact**: `TryGetValue` and `TryRemove` are not branching constructs — they are method calls that assign via `out` parameters. Zero new McCabe branches. `TrySetSlateReadyOnDrainFill` CYC: 5 → **5** (unchanged). ✓

**Design note — ConcurrentDictionary vs struct field**: The spec originally described adding a nullable `Order FollowerOrder` field to the `PendingDispatchDrain` struct. A mutable field on a struct would be a **JS-008** violation (mutable struct fields are banned). A class-level `ConcurrentDictionary<string, Order>` achieves the same intent without mutating the struct, is lock-free (JS-021), and is thread-safe by construction. This divergence from the spec's wording is an intentional architectural improvement.

**Change 4: Cleanup in `AbortDrainOnFill` (line 7126)** (CYC: 3, unchanged):

```csharp
_drainFollowerEntryOrders.TryRemove(acctKey, out _);  // NEW cleanup -- TryRemove, no branch
```

**Change 5: Cleanup in `TryDrainWatchdog` (line 7133)** (CYC: 4, unchanged):

Inside the `if (now - kv.Value.TimestampTicks > 2000L)` block:
```csharp
_drainFollowerEntryOrders.TryRemove(kv.Key, out _);  // NEW cleanup -- no new branch
```

### Thread safety

`_drainFollowerEntryOrders` is a `ConcurrentDictionary`. `TryAdd`, `TryRemove` are lock-free. JS-021 compliant. The entry is written in `SubmitDrainedEntry` and read/removed in `TrySetSlateReadyOnDrainFill` / `AbortDrainOnFill` / `TryDrainWatchdog`, all on NT8 dispatch thread for the same account. No race.

### Why this solves fo=NULL

The fo=NULL problem has two contributing factors:

1. **SimStop prefix not recognized**: `FindFollowerBracketOrder` uses `IsAtmSTPOrder`/`IsStopLeg` which did not match SimStop. **T1 fixes this** — the predicate gate-level cause. After T1, `SyncFollowerBracket` can locate the SimStop orders on the follower account.

2. **Timing race after drain**: The follower entry fills, `TrySetSlateReadyOnDrainFill` sets slate-ready, but the next `SyncFollowerBracket` call may fire before NT8's ATM engine places SimStop1/SimStop2 on the follower account. **T3 fixes this** by threading the stored `Order` reference (the one returned by `SendCopyWithAtm` at drain submission time) into `SyncFollowerBracket` as the `fo` argument. This eliminates the window where `fo=NULL` even if NT8's ATM engine has not yet made the order visible via `account.Orders`.

Both T1 and T3 are required for a complete fo=NULL fix: T1 ensures predicates recognize SimStop names, T3 ensures the entry order reference is available even before NT8 populates the bracket orders on the follower. Together they satisfy the SIM-3 PASS condition.

### T3 Test Cases

| # | Test name | Assertion |
|---|-----------|-----------|
| T3-01 | `DrainFollowerEntryOrders_FieldExists` | source-text Assert.Contains `_drainFollowerEntryOrders` in CopyEngine.cs |
| T3-02 | `SendCopyWithAtm_ReturnsOrderType` | source-text Assert.Contains `Order? SendCopyWithAtm` (return type is Order?) |
| T3-03 | `SubmitDrainedEntry_StoresEntryOrder` | source-text Assert.Contains `_drainFollowerEntryOrders.TryAdd` |
| T3-04 | `TrySetSlateReadyOnDrainFill_RetrievesAndPassesDrainFo` | source-text Assert.Contains `_drainFollowerEntryOrders.TryGetValue` in CopyEngine.cs (confirms wiring step) |

---

## §6 T4 ATM Template Dir Fix Design

### Problem

`CheckAtmTemplatesForReverse()` (parameterless overload, line 1883) builds the ATM template dir path:
```csharp
System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "Templates", "ATM")
```

`NinjaTrader.Core.Globals.UserDataDir` returns `%MyDocuments%\NinjaTrader 8\`. The actual NT8 ATM template directory is `%MyDocuments%\NinjaTrader 8\templates\AtmStrategy\`. The current path uses "Templates" (capital T) and "ATM" instead of the correct "templates" and "AtmStrategy". On Windows, filesystem lookups are case-insensitive so "Templates" vs "templates" is NOT the issue. The issue is "ATM" vs "AtmStrategy". The dir `NinjaTrader 8\Templates\ATM\` does not exist. The correct dir is `NinjaTrader 8\templates\AtmStrategy\`.

### Fix

**Site**: `CheckAtmTemplatesForReverse()` parameterless overload (line 1883-1885).

Change:
```csharp
// BEFORE (wrong subpath):
internal void CheckAtmTemplatesForReverse()
    => CheckAtmTemplatesForReverse(
        System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "Templates", "ATM"));

// AFTER (correct subpath):
internal void CheckAtmTemplatesForReverse()
    => CheckAtmTemplatesForReverse(
        System.IO.Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "templates", "AtmStrategy"));
```

**CYC**: 1 → 1. No change. Pure path string fix.

### Log message updates in CheckAtmTemplatesForReverse(string) (line 1891-1937)

The dir-not-found log at line 1897-1899 should be updated to a **warning** level (upgrade from INFO) and include the actual path checked:

```csharp
// BEFORE:
"[PTT-STARTUP-INFO] ATM template dir not found -- skipping Reverse check"
// AFTER:
"[PTT-STARTUP-WARN] ATM template dir not found: " + atmDir + " -- skipping Reverse check"
```

**CYC impact**: 6 → 6. Text change only, no new branches.

### Existing Reverse=true warning (line 1918-1922)

The existing `[PTT-STARTUP-WARN] ATM template {name} has Reverse=Yes` log is correct and covers the "warn if Reverse=true" requirement. No change needed to the Reverse warning text.

### Test cases

| # | Test name | Assertion |
|---|-----------|-----------|
| T4-01 | `CheckAtmTemplatesForReverse_UsesAtmStrategyPath` | source-text Assert.Contains `"templates", "AtmStrategy"` (correct subpath) |
| T4-02 | `CheckAtmTemplatesForReverse_DirNotFoundLogsPath` | source-text Assert.Contains `"[PTT-STARTUP-WARN] ATM template dir not found: "` |

---

## §7 CYC Impact Summary Table

All methods touched in B198. Before/after CYC. JS-066 (CYC <= 8) compliance column.

| Method | File | Line | Before CYC | After CYC | JS-066 |
|--------|------|------|-----------|----------|--------|
| `IsNativeExitName` | CopyEngine.cs | 2564 | 4 | 5 | PASS |
| `IsAtmBracketName` | CopyEngine.cs | 858 | 1 | 2 | PASS |
| `IsBracketLeg` | CopyEngine.cs | 5880 | 1 | 1 | PASS |
| `IsBracketLegStatic` | CopyEngine.cs | 5861 | 1 | 1 | PASS |
| `IsAtmSTPOrder` | CopyEngine.cs | 3044 | 1 | 1 | PASS |
| `IsStopLeg` | CopyEngine.cs | 5848 | 1 | 1 | PASS |
| `TryParseStopSuffix` | CopyEngine.cs | 3377 | 3 | 5 | PASS |
| `CaptureOtherLegTargetPrices` | CopyEngine.cs | 3348 | 6 | 6 | PASS |
| `ResubmitCollateralLegs` | CopyEngine.cs | 3532 | 5 (est.) | 5 | PASS |
| `FindLeaderCollateralOrder` | CopyEngine.cs | 3405 | 3 | 3 | PASS |
| `FindLeaderStopPrice` | CopyEngine.cs | 3884 | 6 | 6 | PASS |
| `SendCopyWithAtm` | CopyEngine.cs | 4999 | 4 | 4 | PASS (return type change only) |
| `SubmitDrainedEntry` | CopyEngine.cs | 7085 | 5 | 6 | PASS |
| `TrySetSlateReadyOnDrainFill` | CopyEngine.cs | 1070 | 5 | 5 | PASS |
| `AbortDrainOnFill` | CopyEngine.cs | 7126 | 3 | 3 | PASS |
| `TryDrainWatchdog` | CopyEngine.cs | 7133 | 4 | 4 | PASS |
| `CheckAtmTemplatesForReverse()` | CopyEngine.cs | 1883 | 1 | 1 | PASS |
| `CheckAtmTemplatesForReverse(string)` | CopyEngine.cs | 1891 | 6 | 6 | PASS |
| `CheckAtmTemplatesForReverse(string)` (T2b H2 only) | CopyEngine.cs | 1891 | 6 | 7 | PASS |
| `SendCopyWithAtm` (T2b H2 only) | CopyEngine.cs | 4999 | 4 | 5 | PASS |

**CYC ceiling violations: NONE.** All methods remain at or below CYC=8.

---

## §8 7-Scan Checklist

The engineer must run all 7 scans after implementing each ticket before issuing completion.

### SCAN-01: JS-021 lock() ban
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
```
Expected: zero matches in new or modified code.

### SCAN-02: JS-033 async void ban
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { $_.Line -notmatch "handler|Handler|Event" }
```
Expected: zero new async void methods.

### SCAN-03: JS-001 throw in hot path
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception" | Where-Object { $_.Line -notmatch "//" }
```
Expected: zero in B198 change scope.

### SCAN-04: JS-066 CYC > 8
Expected: no method in §7 table exceeds CYC=8 after changes. Engineer runs Lizard on modified methods:
```powershell
python -m lizard src/PropTraderTools/CopyEngine.cs -C 8 --csv | Select-String "IsNativeExitName|IsAtmBracketName|TryParseStopSuffix|CaptureOtherLeg|ResubmitCollateral|FindLeaderCollateral|FindLeaderStop|SendCopyWithAtm|SubmitDrained|TrySetSlate|AbortDrainOn|TryDrainWatch|CheckAtmTemplates"
```
Expected: all rows show CCN <= 8.

### SCAN-05: ASCII violation check
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```
Expected: zero non-ASCII characters in B198 change scope.

### SCAN-06: xUnit-only test framework
```powershell
Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
```
Expected: zero matches. All tests use `[Fact]` from xUnit.

### SCAN-07: Sync field access (new fields introduced by B198)
New fields: `_drainFollowerEntryOrders` (T3), `_atmTemplateGroupQty` (T2b H2 only).
Both are `ConcurrentDictionary` -- lock-free. Verify no `lock()` around any access to these fields:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders|_atmTemplateGroupQty" -Context 2 | Where-Object { $_.Context.PreContext -match "lock" -or $_.Context.PostContext -match "lock" }
```
Expected: zero lock blocks around these dictionaries.

---

## §9 SIM Gate Plan

Setup: Leader=Sim101, Followers=Sim102/Sim103/Sim104, Clone mode, MGC DEC26, ATM template: MGC $400 SL4 (SimStop1+SimStop2 leader bracket order names).
Record results in `docs/brain/B198/sim-gate-completion.md`.

### SIM-1 (T1 -- SimStop fix)
1. Place ATM entry qty=10 on Sim101.
2. Confirm: ZERO `"IsWorkingBracket=False name=SimStop"` in output log.
3. Confirm: Sim102/Sim103/Sim104 all Long N (no SHORT position on any follower).
4. Let leader SimStop trigger. Confirm followers flatten cleanly.
5. PASS condition: PTT-Flatten fires <= 1 time per follower after stop. ZERO double Sell copy in log.

### SIM-2 (T2a instrumentation)
1. Place ATM entry on Sim101, leader qty=10.
2. Log must show: `"ATM-send qty=10 tmpl=MGC $400 SL4"` for each follower.
3. Log must show: `"ATM-order qty=X"` for each follower.
4. Record X (the qty on the created order). This pins H1 vs H2 vs H3.
5. Record in `docs/brain/B198/ticket-2a-sim-evidence.md`.

### SIM-3 (T1+T3 -- fo=NULL fix)
1. Drag leader entry 2-3x. `[DRAIN]` must fire for all followers.
2. Let leader fill. Log must show ZERO `"fo=NULL"` in TP4-SFB lines.
3. `_drainFollowerEntryOrders` cleanup log (from TrySetSlateReadyOnDrainFill) confirms order reference was stored, retrieved, and cleared — the `TryGetValue` step is reached.
4. PASS: All `SyncFollowerBracket` calls have `fo=<order>` (not `fo=NULL`).
   - T1 enables this by ensuring `IsAtmSTPOrder`/`IsStopLeg`/`IsBracketLeg` recognize SimStop prefix → `FindFollowerBracketOrder` returns the bracket order.
   - T3 enables this for the timing-race window by passing `drainFo` (from `_drainFollowerEntryOrders`) directly as the `fo` argument in `TrySetSlateReadyOnDrainFill` → `SyncFollowerBracket`. Both paths must hold for PASS.

### SIM-4 (T4 -- ATM dir fix)
1. Restart PTT. Output log must show ZERO `"ATM template dir not found"` messages.
2. If MGC $400 SL4 template has Reverse=true: `[PTT-STARTUP-WARN] ATM template MGC $400 SL4 has Reverse=Yes` must fire.
3. PASS: no dir-not-found on startup.

---

## §10 Deferred Work Register

### Items Closed by B198

| DW ID | Status | Closing ticket |
|-------|--------|---------------|
| DW-B198-SIMSTP-01 | CLOSED (expected) | T1 |
| DW-B198-QTY-01 | CLOSED if H1; PARTIAL-CLOSED on T2a completion + T2b if H2 | T2a+T2b |
| DW-B195-01 | CLOSED (expected) | T3 |
| DW-B198-ATMDIR-01 | CLOSED (expected) | T4 |

### New Items Opened by B198 Analysis

#### DW-B198-FINDLDR-01: FindLeaderStopPrice SimStop Miss (NEWLY DISCOVERED by architect)

```
Priority: P0 (part of T1 scope -- fix designed in §3 B5 above)
Description: FindLeaderStopPrice (CopyEngine.cs line 3884) builds "Stop" + bracketIndex and
  never builds "SimStop" + bracketIndex. With SimStop ATM, the leader orders are named
  SimStop1/SimStop2. FindLeaderStopPrice returns 0.0 for all SimStop orders.
  ExecutePhaseCStopReplacement (called from SyncAtmFollowerTarget Lane A target sync path)
  gets stp=0.0 -> CreateFollowerReplacementStop skips (stopPrice <= 0 guard).
  Fix designed in §3 B5. Part of T1 scope.
Status: OPEN (will be closed by T1)
```

#### DW-B198-CAPTURELEG-01: CaptureOtherLegTargetPrices SimStop Guard Miss (NEWLY DISCOVERED by architect)

```
Priority: P0 (part of T1 scope -- fix designed in §3 B2 above)
Description: CaptureOtherLegTargetPrices (CopyEngine.cs line 3351) has an early-return guard:
  `if (!fo.Name.StartsWith("Stop"))`. With fo.Name=="SimStop1", this guard fires and returns
  all-zeros, preventing collateral leg target price capture during SimStop drag.
  Fix: add `&& !fo.Name.StartsWith("SimStop")` (compound AND on existing if, 0 McCabe delta).
  Part of T1-B2 scope.
Status: OPEN (will be closed by T1)
```

#### DW-B198-QTY-T2B-DESIGN: T2b Fix Design Deferred Pending SIM Evidence

```
Priority: P1 (H2 is most likely; fix design is provided in §4 but not confirmed until T2a SIM)
Description: T2b fix design (ATM template XML group count parsing + _atmTemplateGroupQty dict)
  is pre-designed in §4 for H2 path. Requires T2a SIM evidence to confirm which hypothesis
  applies. If H1: no fix needed. If H2: implement §4 H2 design. If H3: deferred further.
Status: OPEN (pending T2a completion)
```

### Items Carried Forward from B197 (UNCHANGED)

All 15 items from docs/brain/B197/06-deferred-backlog.md carry forward unchanged. Key P1 items:

| DW ID | Priority | Description |
|-------|----------|-------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen |
| DW-B167-DEFER-03 | P1 | Combined B161-B166 SIM gate |
| DW-B167-DEFER-04 | P1 | B167 SIM gate |
| DW-B170-SIM | P1 | B170 SIM gate (Accepted window fix) |
| DW-B197-SIM-01 | P1 | B197 SIM gate not run |
| DW-B192-SINST-01 | P2 | SetInstrument() CYC=8 frozen |
| DW-B159-K2 | P2 | 14 xUnit tests BE path |
| DW-B159-K3 | P2 | Attempt cap hardcoded at 3 in TryArmBePriceRetry |
| DW-B159-K4 | P2 | intendedBePrice sourcing dependency |
| DW-B167-DEFER-01 | P2 | ECA-1 double-position race |
| DW-B197-03 | P2 | Pre-existing DwB15701SlateReadyTests failures |
| DW-B193-01 | P3 | OnQuickAllBufferChanged old label strings |
| DW-B197-01 | P3 | Stale CCN comment at line 1993 |
| DW-B197-02 | P3 | T4 fact names misleading |
| DW-B197-04 | P3 | TryCancelSingleStaleGtcExit not in plan CYC table |

---

## Appendix: Grep Evidence (mandatory pre-condition for T1 scope)

All mandatory greps run before writing this plan. Results confirm known sites and two new sites.

| Pattern | Matches | Notes |
|---------|---------|-------|
| `StartsWith("Stop"` | Lines 862, 3048, 3351, 5851, 5867, 5885 | All 6 predicate sites confirmed. Line 3351 = CaptureOtherLegTargetPrices guard (B2). |
| `Substring(4)` | Line 3382 | TryParseStopSuffix only. Confirmed. |
| `n > 3` | Line 3383 | TryParseStopSuffix only. Confirmed. |
| `i <= 3` | Lines 3355, 3541 | CaptureOtherLegTargetPrices (3355) AND ResubmitCollateralLegs (3541). Both need fix (B2). |
| `double\[3\]` | Line 3350 | CaptureOtherLegTargetPrices only. Confirmed. |
| `"Stop" +` | Lines 3409, 3890 | FindLeaderCollateralOrder (3409, B3) AND FindLeaderStopPrice (3890, B5 NEWLY DISCOVERED). |
| `StartsWith("SimStop"` | Zero matches | Confirmed: SimStop support does not exist anywhere. |

No additional sites gating bracket behaviour beyond the known list + B5.

---

*Written by: ptt-architect*
*Epic: B198*
*Phase: 1 (Architecture Planning — Revision 1, post REVIEW_FAIL cycle 1)*
*Fixes: VIO-1 (T3 SyncFollowerBracket wiring), VIO-2 (T1-13..T1-16 test cases)*
*Returns: PLAN_COMPLETE*
