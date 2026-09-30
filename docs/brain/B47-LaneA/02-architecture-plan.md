# B47-LaneA — Architecture Plan
**Block**: PTT-COPIER-B47 Lane A
**Defect**: DW-B47-BE-FOLLOWER-SCOPE (P0 CRITICAL)
**Date**: 2026-08-08
**Status**: PLAN_COMPLETE (Revision 1 — CYC fix)
**Spec anchor**: `specs/002-trade-copier-spec.html#dw-b47-be-follower-scope`

---

## 1. Root Cause Summary

### Confirmed in live NT8 session (2026-08-07)
NT8 Output showed 17 `CancelStaleBrackets` calls across 5 accounts when BE ALL was pressed.
Sim102 (follower) lost its ATM Stop/Target brackets; left with a bare BuyToCover stop only.

### Three independent fan-out paths all reach followers

| Path | Entry point | Fan-out source | Fix site |
|------|-------------|----------------|----------|
| BE ALL | `PttGlobalBreakEven.Execute(int)` → `CopyEngine.ArmAllPendingBe` | `Account.All` loop at CopyEngine.cs:2112 | Add guard at 2113 |
| Quick ALL | `PttGlobalQuickExit.Execute()` | `Account.All` outer loop at PttGlobalQuickExit.cs:26 | Add guard at 27 |
| BE button (single) | `PttBreakEven.Execute(ctx)` | `ctx.AllAccounts` loop (= leader + followers) at PttBreakEven.cs:79 | Add guard inside loop + extract helpers |

### Why the existing guard was insufficient
`ctx.AllAccounts` returns `rule.MasterAccount + rule.FollowerAccounts` (spec-correct for follower
fan-out of copied orders) but the BE module must only operate on non-follower accounts.
No `IsFollowerAccount()` predicate existed on `CopyEngine`.

---

## 2. Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | `IsFollowerAccount(Account a)` added to `CopyEngine` (not as extension method or helper class) | `_rules` is private to `CopyEngine`; only `CopyEngine` can safely read it without a lock |
| D2 | Manual `foreach` + `Array.IndexOf` — no LINQ `.Any()` | NT8-006: no LINQ on any path touching hot-path types; `ConcurrentBag.Any()` is LINQ |
| D3 | `internal` visibility on `IsFollowerAccount` | All other query methods on `CopyEngine` (`FindRule`, `IsPendingSlotsEmpty`) are `internal`; `internal` matches the project pattern |
| D4 | Placement after `FindRule` at line 1388 | Groups all rule-query methods together; no dependency ordering issue |
| D5 | `PttBreakEven.Execute()` extracts THREE helpers (`ExecuteOneAccount` + `RaiseBeNotify` + `BuildBeRejectMsg`) | True Execute() baseline is CYC=14 (not 8 as stated in source comment — 6 branches were undercounted; see §4c). Follower guard adds +2; three-helper extraction reduces Execute() by 7 → net CYC=7. `ExecuteOneAccount` alone would be CYC=9 without the third helper; delegating the two `!priceOk` ternaries to `BuildBeRejectMsg` brings it to CYC=7. |
| D6 | `PttGlobalBreakEven.Execute(int)` — NO CHANGE | CYC=1; it delegates unconditionally to `ArmAllPendingBe`; the guard in `ArmAllPendingBe` covers this path |
| D7 | Orphan guard at CopyEngine.cs:779 — NO CHANGE | Operates on `e.Order.Account` (account whose order just changed state); not a fan-out loop; correct behaviour |
| D8 | `PttGlobalBreakEven.Execute(IEnumerable<Account>)` test seam — NO CHANGE | Test seam accepts injected accounts; guards apply to production `Account.All`; test seam is for unit test isolation only |

---

## 3. New Method: `CopyEngine.IsFollowerAccount`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insertion point**: line 1389 (immediately after `FindRule` closing brace at line 1388)

```csharp
/// <summary>
/// Returns true if acc is registered as a follower in any active copy rule.
/// Used to guard BE ALL / Quick ALL paths from operating on follower accounts
/// managed by PTTFollowerStrategy.
/// CYC=4: foreach(1), null guard(2), Array.IndexOf check(3). Base=1.
/// NT8-006: no LINQ -- manual foreach + Array.IndexOf.
/// JS-021: no lock. JS-002: bool return only.
/// </summary>
internal bool IsFollowerAccount(Account a)
{
    foreach (CopyRule r in _rules)
    {
        if (r.FollowerAccounts == null) continue;
        if (Array.IndexOf(r.FollowerAccounts, a) >= 0) return true;
    }
    return false;
}
```

**CYC analysis**:
- Base: 1
- `foreach`: +1
- `if (r.FollowerAccounts == null)`: +1
- `if (Array.IndexOf(...) >= 0)`: +1
- **CYC = 4** ✓ (≤ 8)

**No new `using` required**: `System.Array` is in `System`, already available. `CopyRule.FollowerAccounts` is `Account[]` — `Array.IndexOf` operates on it directly.

---

## 4. Change Sites

### 4a. `CopyEngine.ArmAllPendingBe` guard

**File**: `src/PropTraderTools/CopyEngine.cs`
**Current lines 2107–2132** (confirmed from source):

```
2107: internal int ArmAllPendingBe(int bufferTicks)
2108: {
2109:     int seq = System.Threading.Interlocked.Increment(ref _beAllOcoSeq);
2110:     int armedCount = 0;
2111:     int accIdx = 0;
2112:     foreach (Account acc in Account.All)
2113:     {
```

**Change**: Insert `if (IsFollowerAccount(acc)) continue;` as the first statement in the outer `foreach` body at line 2113.

**After (relevant lines)**:
```csharp
foreach (Account acc in Account.All)           // line 2112
{
    if (IsFollowerAccount(acc)) continue;       // B47 guard — NEW
    foreach (Position pos in acc.Positions)
    {
```

**CYC before**: 5 (per existing header comment)
**CYC after**: 5 + 1 (new `if`) = **6** ✓

---

### 4b. `PttGlobalQuickExit.Execute` guard

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Current lines 26–36** (confirmed from source):

```csharp
internal void Execute()
{
    foreach (Account acc in Account.All)                // (1)
    {
        foreach (Position pos in acc.Positions)         // (2)
        {
            if (pos == null || pos.Quantity == 0) continue;  // (3)
            var ticks = ResolveQuickTicks(pos.Instrument);
            ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
        }
    }
}
```

**Change**: Pre-capture `CopyEngine.Instance` before the outer loop; insert follower guard as first statement of outer loop body.

**After**:
```csharp
internal void Execute()
{
    var engine = CopyEngine.Instance;                              // capture once
    foreach (Account acc in Account.All)                          // (1)
    {
        if (engine != null && engine.IsFollowerAccount(acc)) continue; // B47 guard — NEW: +1, +1(&&)
        foreach (Position pos in acc.Positions)                   // (2)
        {
            if (pos == null || pos.Quantity == 0) continue;       // (3)
            var ticks = ResolveQuickTicks(pos.Instrument);
            ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
        }
    }
}
```

**CYC before**: 3
**CYC after**: 3 + 1 (`if`) + 1 (`&&`) = **5** ✓

---

### 4c. `PttBreakEven.Execute` refactor + guard

**File**: `src/PropTraderTools/Features/PttBreakEven.cs`

#### CYC accounting (before) — CORRECTED BASELINE

The source header comment states CYC=8 but omits two ternary branches inside the `!priceOk` block
(PttBreakEven.cs lines 97–98). Strict Lizard rules count every ternary `?:` as +1 CYC.

**True CYC = 14** (complete enumeration):

| Branch | CYC delta |
|--------|-----------|
| Base | +1 |
| `if (!IsEnabled)` | +1 |
| `if (leaderPos == null` | +1 |
| `\|\|` in leader null guard | +1 |
| `foreach (Account acc in ctx.AllAccounts)` | +1 |
| `if (pos == null \|\| pos.Quantity == 0) continue` | +1 |
| `\|\|` in pos null guard | +1 |
| `isLong ? +buf : -buf` ternary (line 86) | +1 |
| `isLong ? (ask <= 0.0 \|\| ...)` outer ternary (line 93) | +1 |
| `\|\|` inside ternary branch (line 93) | +1 |
| `if (!priceOk)` | +1 |
| `isLong ? "above ask" : "below bid"` ternary (line 97) | +1 |
| `isLong ? ask.ToString("F2") : bid.ToString("F2")` ternary (line 98) | +1 |
| `leaderIsLong ? +buf : -buf` ternary (line 119) | +1 |
| **True CYC total** | **14** |

Adding the follower guard (`if (engine != null && engine.IsFollowerAccount(acc))`) = +2 → would-be CYC=16. **WOULD FAIL without extraction.**

#### Extraction plan — THREE helpers extracted from Execute()

**Helper 1**: `ExecuteOneAccount` — receives the per-account loop body
Removes: pos null||qty (+2), isLong ternary (+1), outer priceOk ternary+|| (+2), `if (!priceOk)` (+1),
plus the two `!priceOk` ternaries delegated to `BuildBeRejectMsg` (+2).
Total removed from Execute(): 8 points.

**Helper 2**: `RaiseBeNotify` — receives the leaderIsLong computation + bus notification
Removes: `leaderIsLong ? +buf : -buf` ternary (+1).

**Helper 3**: `BuildBeRejectMsg` — static helper for the two ternaries inside the `!priceOk` warning block
Removes: `isLong ? "above ask" : "below bid"` (+1) and `isLong ? ask.ToString("F2") : bid.ToString("F2")` (+1).
Called by `ExecuteOneAccount`, not by `Execute()`.

#### New `Execute()` after extraction
Items remaining in Execute(): base(1), `!IsEnabled`(1), `leaderPos null`(1), `||`(1),
`foreach`(1), B47 guard `if`(1), `&&`(1).

CYC = 1 + 1 + 1 + 1 + 1 + 1 + 1 = **7** ✓

```csharp
public void Execute(IPttHostContext ctx)
{
    if (!IsEnabled) return;
    int seq = System.Threading.Interlocked.Increment(ref _beOcoSeq);
    Position leaderPos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
    if (leaderPos == null || leaderPos.Quantity == 0) return;

    double tickSize = ctx.Instrument.MasterInstrument.TickSize;
    double buf      = (double)ctx.BeBuffer;

    var engine = CopyEngine.Instance;                                    // B47: capture once
    foreach (Account acc in ctx.AllAccounts)
    {
        if (engine != null && engine.IsFollowerAccount(acc)) continue;   // B47 guard — NEW
        ExecuteOneAccount(acc, ctx, buf, tickSize, seq);
    }

    RaiseBeNotify(ctx, leaderPos, buf);
}
```

#### New `ExecuteOneAccount` method signature

```csharp
/// <summary>
/// Per-account BE logic extracted from Execute() to maintain CYC constraint.
/// Contains the per-account position check, price computation, priceOk guard,
/// target snapshot, bracket cancel, and BE bracket submit.
/// Delegates the !priceOk warning-string construction to BuildBeRejectMsg.
/// CYC=7: pos null(1), ||(2), isLong ternary(3), priceOk ternary(4),
///        ||(5 — ask/bid condition), if !priceOk(6). Base=1.
/// NOTE: the two ternaries inside the !priceOk block (lines 97-98) are
///       delegated to BuildBeRejectMsg — they do NOT add CYC here.
/// JS-021: no lock. JS-033: synchronous void.
/// </summary>
private void ExecuteOneAccount(Account acc, IPttHostContext ctx,
                                double buf, double tickSize, int seq)
```

CYC analysis for `ExecuteOneAccount`:
- Base: 1
- `if (pos == null || pos.Quantity == 0) return`: +1 (`if`) + 1 (`||`) = +2
- `isLong ? +buf : -buf` ternary: +1
- `isLong ? (ask <= 0.0 || ...) : (bid <= 0.0 || ...)` ternary + `||`: +2
- `if (!priceOk)`: +1
- *(two ternaries in `!priceOk` body delegated to `BuildBeRejectMsg` — NOT counted here)*
- **CYC = 7** ✓

#### New `BuildBeRejectMsg` method signature

```csharp
/// <summary>
/// Formats the warning message for a rejected BE price move.
/// Extracted from ExecuteOneAccount to absorb the two isLong ternaries inside
/// the !priceOk block and keep ExecuteOneAccount CYC <= 8.
/// Returns the formatted warning string only; caller does Output.Process + ctx.WarnUser.
/// CYC=3: base(1), isLong ternary for side(1), isLong ternary for market(1).
/// JS-021: no lock. JS-002: returns string (never null — string.Format always returns).
/// </summary>
private static string BuildBeRejectMsg(string accName, double bePrice, bool isLong,
                                       double ask, double bid)
```

CYC analysis for `BuildBeRejectMsg`:
- Base: 1
- `isLong ? "above ask" : "below bid"` ternary: +1
- `isLong ? ask.ToString("F2") : bid.ToString("F2")` ternary: +1
- **CYC = 3** ✓

Usage in `ExecuteOneAccount` (inside `if (!priceOk)` block):
```csharp
if (!priceOk)
{
    string msg = BuildBeRejectMsg(acc.Name, bePrice, isLong, ask, bid);
    Output.Process(msg, PrintTo.OutputTab1);
    ctx.WarnUser(msg);
    return;
}
```

---

#### New `RaiseBeNotify` method signature

```csharp
/// <summary>
/// Compute leaderIsLong + leaderBePrice then fire PttBus.RaiseBe.
/// Extracted from Execute() to keep Execute() CYC ≤ 8.
/// CYC=2: base(1), leaderIsLong ternary(1).
/// JS-021: no lock.
/// </summary>
private void RaiseBeNotify(IPttHostContext ctx, Position leaderPos, double buf)
```

CYC analysis for `RaiseBeNotify`:
- Base: 1
- `leaderIsLong ? +buf : -buf` ternary: +1
- **CYC = 2** ✓

---

## 5. CYC Summary Table

| Method | File | CYC Before | CYC After | Status |
|--------|------|-----------|-----------|--------|
| `CopyEngine.IsFollowerAccount` | CopyEngine.cs | N/A (new) | 4 | ✓ NEW |
| `CopyEngine.ArmAllPendingBe` | CopyEngine.cs | 5 | 6 | ✓ |
| `PttGlobalQuickExit.Execute` | Features/PttGlobalQuickExit.cs | 3 | 5 | ✓ |
| `PttBreakEven.Execute` | Features/PttBreakEven.cs | **14** (corrected from 8) | 7 | ✓ |
| `PttBreakEven.ExecuteOneAccount` | Features/PttBreakEven.cs | N/A (new) | 7 | ✓ NEW |
| `PttBreakEven.RaiseBeNotify` | Features/PttBreakEven.cs | N/A (new) | 2 | ✓ NEW |
| `PttBreakEven.BuildBeRejectMsg` | Features/PttBreakEven.cs | N/A (new) | 3 | ✓ NEW |
| `PttGlobalBreakEven.Execute(int)` | Features/PttGlobalBreakEven.cs | 1 | 1 | ✓ NO CHANGE |

All methods ≤ 8. ✓

---

## 6. Files Modified / Confirmed No-Change

### Modified
| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Insert `IsFollowerAccount` after line 1388; insert guard in `ArmAllPendingBe` at line 2113 |
| `src/PropTraderTools/Features/PttBreakEven.cs` | Refactor `Execute()`: extract `ExecuteOneAccount` + `RaiseBeNotify` + `BuildBeRejectMsg`; add follower guard |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Add follower guard in `Execute()` outer loop |

### Confirmed no-change needed
| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | `Execute(int)` is CYC=1; it delegates to `ArmAllPendingBe`; the fix in `ArmAllPendingBe` covers the production path |
| `src/PropTraderTools/Features/PttQuickExit.cs` | Operates on a single leader account passed as argument; no fan-out to followers |
| `src/PropTraderTools/CopyEngine.cs:779` | Orphan guard on `e.Order.Account` — account-specific, not a fan-out loop |
| `src/PropTraderTools/TradeCopierPanel.cs` | Out of B47-LaneA scope |
| `src/PropTraderTools/PttFollowerStrategy.cs` | Out of B47-LaneA scope |

---

## 7. Jane Street / NT8 Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 no lock | `IsFollowerAccount` uses `foreach` + `Array.IndexOf`; all guards use simple boolean checks | ✓ PASS |
| JS-001 no throw in hot path | `IsFollowerAccount` returns `bool`; no throws in any guard | ✓ PASS |
| JS-002 no return null | All new/modified methods return `bool`, `void`, or `int` | ✓ PASS |
| JS-033 no async void | All methods are synchronous | ✓ PASS |
| NT8-006 no LINQ | `IsFollowerAccount` uses manual `foreach` + `Array.IndexOf` (not `.Any()` / `.Contains()`) | ✓ PASS |
| NT8-003 no volatile double | No new volatile double fields | ✓ PASS |
| NT8-014 PTT- prefix | No new `CreateOrder` calls; existing order names unchanged | ✓ PASS |
| CYC ≤ 8 (all methods) | See CYC table above | ✓ PASS |

---

## 8. Thread-Safety Analysis

| Method | Thread context | `_rules` access | Safety |
|--------|---------------|-----------------|--------|
| `IsFollowerAccount` | UI thread (called from button handlers) | `foreach` on `ConcurrentBag<CopyRule>` — safe snapshot semantics | ✓ |
| `ArmAllPendingBe` guard | UI thread | `IsFollowerAccount` on UI thread | ✓ |
| `PttGlobalQuickExit.Execute` guard | UI thread (from button handler) | `IsFollowerAccount` on UI thread | ✓ |
| `PttBreakEven.Execute` guard | UI thread (from button handler) | `IsFollowerAccount` on UI thread | ✓ |

`ConcurrentBag<CopyRule>` iteration during `IsFollowerAccount`: if a rule is added/removed concurrently,
the `ConcurrentBag` provides safe snapshot semantics. This is the existing behaviour for all `_rules`
reads and introduces no new risk.

No `Dispatcher.InvokeAsync` required — all call sites are already on the UI thread.

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| `_rules` is empty when `IsFollowerAccount` is called (no rules configured) | High (common in dev/test) | `foreach` terminates immediately; returns `false`; all accounts treated as non-follower | No action needed — correct fallback behaviour |
| `FollowerAccounts` array contains `null` entries | Low | `Array.IndexOf(arr, a)` with `a = null` would match a null entry. If `a` is always non-null (NT8 guarantees Account objects are non-null), no problem | `Account` objects from `Account.All` / `ctx.AllAccounts` are always non-null in NT8 |
| `PttBreakEven.ExecuteOneAccount` extraction changes observable behaviour | Very low | Extraction moves code only; no logic changed; follower accounts now skipped (the intentional fix) | T_B47_01 will verify leader-only scope |
| Copier copy-path regression (follower orders no longer dispatched on fill) | None | `IsFollowerAccount` guards only the BE/QX *cancel* paths; fill dispatch (`OnOrderUpdate` → `DispatchCopy`) is unaffected | No change to `OnOrderUpdate` or `DispatchCopy` |

---

## 10. Test Expectations (for Lane C)

| Test ID | Assertion |
|---------|-----------|
| `T_B47_01` | `engine.IsFollowerAccount(follower)` returns `true`; `engine.IsFollowerAccount(leader)` returns `false`; `CancelStaleBrackets` is NOT called for follower account in BE ALL path |
| `T_B47_01_IsFollowerAccount_ReturnsTrue_ForRegisteredFollower` | After `AddRule(leader, [follower1, follower2])`, `IsFollowerAccount(follower1)` is `true` |
| `T_B47_01_IsFollowerAccount_ReturnsFalse_ForLeader` | `IsFollowerAccount(leader)` is `false` |
| `T_B47_01_IsFollowerAccount_ReturnsFalse_WhenNoRules` | `IsFollowerAccount(any)` is `false` when `_rules` is empty |

These tests are owned by Lane C (`B47Tests.cs`). Lane A does not produce test files.

---

## 11. Deferred Backlog Delta

### Items carried forward from B46 (status unchanged)
- DW-B42-01: P2 — T_BUG_QX_BE_01 missing PTT-QX-T3 assertion — still open
- DW-B42-02: P1 — Live F5 verification of Quick All → BE All sequences — still open
- DW-B42-03: P2 — IsPttQxTarget range extension — still open
- DW-B42-04: P2 — Comment label NT8-NEW at PttContracts.cs:254 — still open
- DW-B43-02 component (a): P1 — GetLeaderAtmTemplateName visual-tree index — still open
- DW-B43-03: P2 — NT8-045 update if AtmStrategyTemplates API becomes accessible — still open
- DW-B44-01: P1 — CopyEngineTests.cs 60 pre-existing compile errors — still open
- DW-B44-02: P1 — Live F5 Subscribe() panel-only path verification — still open
- DW-B46-01: P1 — Live F5 verification DW-B42-05 after B46 — still open
- DW-B46-02: P1 — dotnet test runner blocked by DW-B44-01 — still open

### New items opened by B47-LaneA
None. The fix is surgical and introduces no new deferred items.

---

## 12. 7-Scan Checklist (Pre-implementation)

| Scan | Check | Status |
|------|-------|--------|
| SCAN-01 | No `lock()` in any modified or new method | ✓ CONFIRMED |
| SCAN-02 | No `throw new XxxException` in any hot path | ✓ CONFIRMED |
| SCAN-03 | No `return null` from any new method | ✓ CONFIRMED |
| SCAN-04 | No LINQ on hot path — `IsFollowerAccount` uses `foreach` + `Array.IndexOf` | ✓ CONFIRMED |
| SCAN-05 | All new method identifiers ASCII-only, no FontFamily, no hex colours | ✓ CONFIRMED |
| SCAN-06 | All CYC counts ≤ 8 (see table in §5) | ✓ CONFIRMED |
| SCAN-07 | All `CreateOrder` signal names start with "PTT-" — no new `CreateOrder` calls in this fix | ✓ CONFIRMED (N/A) |

---

*Plan originally authored by: ptt-architect (Phase 1, 2026-08-08)*
*Revised by: ptt-architect (Revision 1, 2026-08-08) — corrected true CYC baseline Execute()=14; added BuildBeRejectMsg helper*
*Next phase: ptt-plan-reviewer → 02-plan-review.md (Cycle 2)*
