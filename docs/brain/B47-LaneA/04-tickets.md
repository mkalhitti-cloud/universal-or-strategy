# B47-LaneA — Tickets
**Block**: PTT-COPIER-B47 Lane A
**Defect**: DW-B47-BE-FOLLOWER-SCOPE (P0 CRITICAL)
**Plan**: `docs/brain/B47-LaneA/02-architecture-plan.md` (REVIEW_PASS Cycle 2)
**Date**: 2026-08-08
**Lane scope**: Source edits only. NO test file — Lane C owns `B47Tests.cs`.

---

## Ticket T1 — DW-B47-BE-FOLLOWER-SCOPE: Add IsFollowerAccount guard to BE/QX all-accounts paths

### Spec Requirement IDs

- `specs/002-trade-copier-spec.html#dw-b47-be-follower-scope`
- Defect confirmed in NT8 Output: 17 `CancelStaleBrackets` calls across 5 accounts on BE ALL press;
  Sim102 (follower) lost ATM Stop/Target brackets.

---

### Files Modified

| # | File | Wave workspace path |
|---|------|---------------------|
| 1 | `CopyEngine.cs` | `src/PropTraderTools/CopyEngine.cs` |
| 2 | `PttBreakEven.cs` | `src/PropTraderTools/Features/PttBreakEven.cs` |
| 3 | `PttGlobalQuickExit.cs` | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` |

### Files Confirmed Unchanged

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | CYC=1; delegates unconditionally to `ArmAllPendingBe`; guard there covers this path |
| `src/PropTraderTools/Features/PttQuickExit.cs` | Operates on a single leader account passed as argument; no fan-out to followers |

---

### PRIMARY EDIT 1 — `CopyEngine.cs`

#### Change 1a: Insert `IsFollowerAccount` method

**Insertion point**: immediately after the closing brace of `FindRule` at line 1388
(new method body starts at line 1389).

**Method to insert**:

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

**CYC budget**: N/A (new) → **4** (≤ 8) ✓

**Method signatures**:
```
internal bool IsFollowerAccount(Account a)
```

**Visibility**: `internal` — matches pattern of `FindRule`, `IsPendingSlotsEmpty`
**No new `using` required**: `System.Array` is in scope via existing `using System;`

---

#### Change 1b: Add follower guard in `ArmAllPendingBe`

**Location**: `CopyEngine.cs`, line 2112 (confirmed: `foreach (Account acc in Account.All)`)
**Guard insertion**: first statement inside the `foreach` body at line 2113.

**Current code at lines 2112–2114**:
```csharp
foreach (Account acc in Account.All)           // line 2112
{
    foreach (Position pos in acc.Positions)    // line 2113 (currently first body line)
```

**After change**:
```csharp
foreach (Account acc in Account.All)                       // line 2112
{
    if (IsFollowerAccount(acc)) continue;                  // B47 guard — NEW at line 2113
    foreach (Position pos in acc.Positions)                // shifts to line 2114
```

**CYC budget**: `ArmAllPendingBe` before = **5** → after = **6** (≤ 8) ✓

---

### PRIMARY EDIT 2 — `PttBreakEven.cs`

#### Overview of changes

`PttBreakEven.Execute` has a true CYC of 14 (corrected from the header comment of 8 — see plan §4c).
Adding the follower guard (+2 CYC) without extraction would yield CYC=16, violating the ≤8 mandate.
The fix requires extraction of three private helpers before adding the guard.

**Four changes to this file**:
- Change 2a: Extract `ExecuteOneAccount` (private method, new)
- Change 2b: Extract `BuildBeRejectMsg` (private static method, new)
- Change 2c: Extract `RaiseBeNotify` (private method, new)
- Change 2d: Rewrite `Execute()` body with follower guard + delegations to new helpers

#### Method signatures — new methods

```csharp
// Change 2a
private void ExecuteOneAccount(Account acc, IPttHostContext ctx,
                                double buf, double tickSize, int seq)

// Change 2b
private static string BuildBeRejectMsg(string accName, double bePrice, bool isLong,
                                        double ask, double bid)

// Change 2c
private void RaiseBeNotify(IPttHostContext ctx, Position leaderPos, double buf)
```

#### Change 2d: Rewritten `Execute` body

**Current method**: `public void Execute(IPttHostContext ctx)` at line 66
**Change site**: entire method body (lines 66–124 approximately)

**After (complete new body)**:
```csharp
public void Execute(IPttHostContext ctx)
{
    if (!IsEnabled) return;                                                // (1) guard
    int seq = System.Threading.Interlocked.Increment(ref _beOcoSeq);

    Position leaderPos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
    if (leaderPos == null || leaderPos.Quantity == 0) return;              // (2) leader guard

    double tickSize = ctx.Instrument.MasterInstrument.TickSize;
    double buf      = (double)ctx.BeBuffer;

    var engine = CopyEngine.Instance;                                      // B47: capture once
    foreach (Account acc in ctx.AllAccounts)                               // (3) foreach
    {
        if (engine != null && engine.IsFollowerAccount(acc)) continue;     // B47 guard — NEW: (4)(5)
        ExecuteOneAccount(acc, ctx, buf, tickSize, seq);
    }

    RaiseBeNotify(ctx, leaderPos, buf);
}
```

**CYC budget**: `Execute` before = **14** → after = **7** (≤ 8) ✓

CYC accounting for new `Execute()`:
- Base: +1
- `if (!IsEnabled)`: +1
- `if (leaderPos == null ||`: +1
- `||` in leader null guard: +1
- `foreach (Account acc`: +1
- `if (engine != null && ...)`: +1
- `&&` in guard: +1
- **Total = 7** ✓

#### Change 2a detail: `ExecuteOneAccount` body

Receives the per-account loop body from the old `Execute()`:
- pos null/qty guard, isLong ternary, priceOk computation, `if (!priceOk)` block (delegating
  the two ternary strings inside to `BuildBeRejectMsg`), targets snapshot, cancel stale, submit.

```csharp
/// <summary>
/// Per-account BE logic extracted from Execute() to maintain CYC constraint.
/// CYC=7: pos null(1), ||(2), isLong ternary(3), priceOk ternary(4),
///        || in ternary condition(5), if !priceOk(6). Base=1.
/// The two isLong ternaries inside the !priceOk block are delegated to
/// BuildBeRejectMsg and do NOT count here.
/// JS-021: no lock. JS-033: synchronous void.
/// </summary>
private void ExecuteOneAccount(Account acc, IPttHostContext ctx,
                                double buf, double tickSize, int seq)
{
    Position pos = FindPositionLocal(acc, ctx.Instrument);
    if (pos == null || pos.Quantity == 0) return;

    bool   isLong  = pos.MarketPosition == MarketPosition.Long;
    double bePrice = pos.AveragePrice + (isLong ? +buf : -buf) * tickSize;

    double ask = ctx.Ask;
    double bid = ctx.Bid;
    bool priceOk = isLong ? (ask <= 0.0 || bePrice <= ask)
                           : (bid <= 0.0 || bePrice >= bid);
    if (!priceOk)
    {
        string msg = BuildBeRejectMsg(acc.Name, bePrice, isLong, ask, bid);
        NinjaTrader.Code.Output.Process(msg, NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        ctx.WarnUser(acc.Name + ": BE stop rejected (" + (isLong ? "above ask" : "below bid")
            + " " + (isLong ? ask.ToString("F2") : bid.ToString("F2")) + ")");
        return;
    }

    var targets = SnapshotTargetsLocal(acc, ctx.Instrument);
    CancelStaleBracketsLocal(acc, ctx.Instrument);
    SubmitBeTargetsLocal(acc, ctx.Instrument, bePrice, isLong, tickSize, targets, seq);
}
```

**CYC budget**: N/A (new) → **7** (≤ 8) ✓

#### Change 2b detail: `BuildBeRejectMsg` body

```csharp
/// <summary>
/// Formats the warning message for a rejected BE price move.
/// CYC=3: base(1), isLong ternary for side(1), isLong ternary for market(1).
/// JS-021: no lock. JS-002: returns string (never null).
/// </summary>
private static string BuildBeRejectMsg(string accName, double bePrice, bool isLong,
                                        double ask, double bid)
{
    string side   = isLong ? "above ask" : "below bid";
    string market = isLong ? ask.ToString("F2") : bid.ToString("F2");
    return "[BE] WARNING: " + accName + " BE stop @ "
           + bePrice.ToString("F2") + " rejected -- stop "
           + side + " market " + market + " -- position UNPROTECTED";
}
```

**CYC budget**: N/A (new) → **3** (≤ 8) ✓

#### Change 2c detail: `RaiseBeNotify` body

```csharp
/// <summary>
/// Compute leaderIsLong + leaderBePrice then fire PttBus.RaiseBe.
/// CYC=2: base(1), leaderIsLong ternary(1).
/// JS-021: no lock.
/// </summary>
private void RaiseBeNotify(IPttHostContext ctx, Position leaderPos, double buf)
{
    bool   leaderIsLong  = leaderPos.MarketPosition == MarketPosition.Long;
    double leaderBePrice = leaderPos.AveragePrice
                           + (leaderIsLong ? +buf : -buf)
                           * ctx.Instrument.MasterInstrument.TickSize;
    PttBus.RaiseBe(this, new BeEventArgs(
        ctx.Instrument, leaderBePrice, leaderPos.AveragePrice,
        leaderIsLong, string.Empty));
}
```

**CYC budget**: N/A (new) → **2** (≤ 8) ✓

---

### PRIMARY EDIT 3 — `PttGlobalQuickExit.cs`

#### Change 3a: Add follower guard in `Execute`

**Current method**: `internal void Execute()` at line 25
**Change site**: `foreach (Account acc in Account.All)` body at line 27

**Current code at lines 25–36**:
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

**After change**:
```csharp
internal void Execute()
{
    var engine = CopyEngine.Instance;                                            // B47: capture once
    foreach (Account acc in Account.All)                                         // (1)
    {
        if (engine != null && engine.IsFollowerAccount(acc)) continue;           // B47 guard — NEW: (2)(&&)
        foreach (Position pos in acc.Positions)                                  // (3)
        {
            if (pos == null || pos.Quantity == 0) continue;                      // (4)
            var ticks = ResolveQuickTicks(pos.Instrument);
            ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
        }
    }
}
```

**CYC budget**: `Execute` before = **3** (strict: 5) → after = **5** (strict: 7) (≤ 8) ✓

CYC accounting for new `Execute()` (strict Lizard):
- `foreach Account.All`: +1
- `if (engine != null && ...)`: +1
- `&&` in guard: +1
- `foreach pos`: +1
- `if (pos == null || ...)`: +1
- `||` in pos guard: +1
- Base: +1
- **Strict total = 7** ✓

---

### CYC Summary Table

| Method | File | CYC Before | CYC After | Limit | Status |
|--------|------|-----------|-----------|-------|--------|
| `CopyEngine.IsFollowerAccount` | CopyEngine.cs | N/A (new) | 4 | ≤ 8 | ✓ NEW |
| `CopyEngine.ArmAllPendingBe` | CopyEngine.cs | 5 | 6 | ≤ 8 | ✓ |
| `PttGlobalQuickExit.Execute` | Features/PttGlobalQuickExit.cs | 3–5 | 5–7 | ≤ 8 | ✓ |
| `PttBreakEven.Execute` | Features/PttBreakEven.cs | 14 | 7 | ≤ 8 | ✓ |
| `PttBreakEven.ExecuteOneAccount` | Features/PttBreakEven.cs | N/A (new) | 7 | ≤ 8 | ✓ NEW |
| `PttBreakEven.BuildBeRejectMsg` | Features/PttBreakEven.cs | N/A (new) | 3 | ≤ 8 | ✓ NEW |
| `PttBreakEven.RaiseBeNotify` | Features/PttBreakEven.cs | N/A (new) | 2 | ≤ 8 | ✓ NEW |
| `PttGlobalBreakEven.Execute(int)` | Features/PttGlobalBreakEven.cs | 1 | 1 (NO CHANGE) | ≤ 8 | ✓ |

---

### Jane Street Rule Constraints

| Rule ID | Rule | Applies to | Constraint |
|---------|------|-----------|------------|
| **JS-021** | No `lock()` anywhere | All new/modified methods | `IsFollowerAccount` uses `foreach` + `Array.IndexOf`; guards use `if`; no `lock` introduced |
| **JS-001** | No `throw` in hot paths | `IsFollowerAccount`, guards | Returns `bool`/`void`; no throws anywhere in this fix |
| **JS-002** | No `return null` for missing values | `IsFollowerAccount`, `BuildBeRejectMsg` | `IsFollowerAccount` returns `bool`; `BuildBeRejectMsg` returns `string` (never null — string concat always non-null) |
| **JS-033** | No `async void` | All | All new/modified methods are synchronous |
| **JS-003** | Readonly struct field safety | `CopyRule.FollowerAccounts` access | Read-only `foreach` iteration — no mutation |
| **JS-023** | `volatile int` for cross-thread fields | `_beOcoSeq` usage | Pre-existing `volatile int`; `Interlocked.Increment` unchanged |

---

### NT8 Constraints

| Rule | Applies to | Constraint |
|------|-----------|------------|
| NT8-006 no LINQ | `IsFollowerAccount` | Manual `foreach` + `Array.IndexOf` — no `.Any()`, `.Contains()`, or LINQ |
| NT8-003 no `volatile double` | All new fields | No new `volatile double` fields introduced |
| NT8-014 PTT- signal prefix | All `CreateOrder` calls | No new `CreateOrder` calls in this fix; existing signal names unchanged |
| NT8-021 `Account.All` not in constructor | `ArmAllPendingBe`, `PttGlobalQuickExit.Execute` | Called from UI button handlers post-init only |
| NT8-001 no `init` setter | All new methods | No `init` setters — `IsFollowerAccount` is a method, not a property |
| NT8-013 no `DateTime.Now` | All | No `DateTime.Now` usage; no new `CreateOrder` calls |

---

### xUnit Test Names (Lane C writes; Lane A specifies)

All tests in: `src/PropTraderTools.Tests/B47Tests.cs` (owned by Lane C — Lane A produces NO test file)

| Test Name | Asserts |
|-----------|---------|
| `T_B47_01_IsFollowerAccount_ReturnsTrueForFollower` | After `AddRule(leader, [follower1, follower2])`, `engine.IsFollowerAccount(follower1)` returns `true` |
| `T_B47_02_IsFollowerAccount_ReturnsFalseForLeader` | `engine.IsFollowerAccount(leader)` returns `false` (leader is not in `FollowerAccounts` of any rule) |
| `T_B47_03_ArmAllPendingBe_SkipsFollowerAccounts` | Structural: using an injected account list, verify that the follower guard `if (IsFollowerAccount(acc)) continue` is present in `ArmAllPendingBe`; any mock invocation on a follower account should not call the inner position loop |
| `T_B47_04_PttBreakEven_Execute_SkipsFollowerAccount` | Structural: with a follower account present in `ctx.AllAccounts`, verify that `ExecuteOneAccount` is NOT called for that account (guard fires `continue`) |

---

### 7-Scan Checklist

Each scan must be run against the three modified files after the engineer writes the code.
The engineer marks each scan PASS before committing.

---

#### SCAN-01 — No `lock()`

**PASS criteria**: Zero occurrences of `lock(` in any modified file.

```powershell
Select-String -Path `
  src/PropTraderTools/CopyEngine.cs, `
  src/PropTraderTools/Features/PttBreakEven.cs, `
  src/PropTraderTools/Features/PttGlobalQuickExit.cs `
  -Pattern "lock\(" | Select-Object LineNumber, Line
# Expected: 0 results
```

**Fail action**: Remove `lock()`. Replace with `ConcurrentBag` iteration or `Interlocked`.

---

#### SCAN-02 — No `async void`

**PASS criteria**: Zero occurrences of `async void` in any modified file.

```powershell
Select-String -Path `
  src/PropTraderTools/CopyEngine.cs, `
  src/PropTraderTools/Features/PttBreakEven.cs, `
  src/PropTraderTools/Features/PttGlobalQuickExit.cs `
  -Pattern "async void" | Select-Object LineNumber, Line
# Expected: 0 results
```

**Fail action**: Convert to `async Task` or make synchronous.

---

#### SCAN-03 — No `return null` in new non-nullable methods

**PASS criteria**: Zero new `return null` statements in `IsFollowerAccount`, `ExecuteOneAccount`,
`RaiseBeNotify`. `BuildBeRejectMsg` returns `string` (never null). `FindPositionLocal` (pre-existing)
may return null — that is the existing contract and is not modified by this ticket.

```powershell
Select-String -Path `
  src/PropTraderTools/CopyEngine.cs, `
  src/PropTraderTools/Features/PttBreakEven.cs `
  -Pattern "return null" | Select-Object LineNumber, Line
# Existing pre-B47 occurrences in FindPositionLocal are ALLOWED.
# New occurrences in IsFollowerAccount, ExecuteOneAccount, BuildBeRejectMsg, RaiseBeNotify = FAIL.
```

**Fail action**: Remove `return null`; use `return false` (bool) or return a default value.

---

#### SCAN-04 — No `throw new` in hot paths

**PASS criteria**: Zero new `throw new XxxException` statements in any new or modified method body.

```powershell
Select-String -Path `
  src/PropTraderTools/CopyEngine.cs, `
  src/PropTraderTools/Features/PttBreakEven.cs, `
  src/PropTraderTools/Features/PttGlobalQuickExit.cs `
  -Pattern "throw new" | Select-Object LineNumber, Line
# Expected: 0 results in NEW or MODIFIED method bodies
```

**Fail action**: Remove throw; log with `Output.Process` and return.

---

#### SCAN-05 — PTT- signal prefix

**PASS criteria**: No new `CreateOrder` calls introduced by this fix. All existing `CreateOrder`
signal names in the modified files start with `"PTT-"`.

```powershell
Select-String -Path `
  src/PropTraderTools/CopyEngine.cs, `
  src/PropTraderTools/Features/PttBreakEven.cs, `
  src/PropTraderTools/Features/PttGlobalQuickExit.cs `
  -Pattern "CreateOrder" | Select-Object LineNumber, Line
# Verify: count matches pre-B47 baseline (0 new CreateOrder calls added)
# Verify: every quoted signal name in existing CreateOrder calls starts with "PTT-"
```

**Fail action**: Remove or rename any signal name that does not start with `"PTT-"`.

---

#### SCAN-06 — CYC ≤ 8

**PASS criteria**: All modified and new methods report CYC ≤ 8 from the complexity audit script.

```powershell
python scripts/complexity_audit.py
# Check output for:
#   CopyEngine.IsFollowerAccount        <= 8
#   CopyEngine.ArmAllPendingBe          <= 8
#   PttBreakEven.Execute                <= 8
#   PttBreakEven.ExecuteOneAccount      <= 8
#   PttBreakEven.BuildBeRejectMsg       <= 8
#   PttBreakEven.RaiseBeNotify          <= 8
#   PttGlobalQuickExit.Execute          <= 8
```

**Fail action**: Extract sub-methods until every method in the list is ≤ 8. Consult plan §4c.

---

#### SCAN-07 — NT8 banned patterns

**PASS criteria**: Zero occurrences of `{ get; init; }`, `volatile double`, `ImmutableDictionary`,
or `abstract record` / `sealed record` in any new code introduced by this ticket.

```powershell
Select-String -Path `
  src/PropTraderTools/CopyEngine.cs, `
  src/PropTraderTools/Features/PttBreakEven.cs, `
  src/PropTraderTools/Features/PttGlobalQuickExit.cs `
  -Pattern "init;|volatile double|ImmutableDictionary|abstract record|sealed record" `
  | Select-Object LineNumber, Line
# Expected: 0 results in lines ADDED by this ticket
```

**Fail action**: Replace per NT8_COMPILER_RULES.md:
- `{ get; init; }` → `{ get; private set; }` + constructor assignment
- `volatile double` → remove `volatile`; use plain `double`
- `ImmutableDictionary` → `Dictionary<K,V>` written once at construction

---

### Acceptance Criteria

| # | Criterion | Verified by |
|---|-----------|-------------|
| **D1** | `CopyEngine.IsFollowerAccount(Account a)` exists; returns `true` for a registered follower account; returns `false` for a leader account | T_B47_01, T_B47_02 |
| **D2** | `CopyEngine.ArmAllPendingBe` skips follower accounts: the `if (IsFollowerAccount(acc)) continue` guard appears before the inner `Position` loop | T_B47_03; SCAN-01 |
| **D3** | `PttBreakEven.Execute` skips follower accounts in `ctx.AllAccounts` loop: the `if (engine != null && engine.IsFollowerAccount(acc)) continue` guard appears before `ExecuteOneAccount` | T_B47_04; SCAN-01 |
| **D4** | `PttGlobalQuickExit.Execute` skips follower accounts in `Account.All` loop: the `if (engine != null && engine.IsFollowerAccount(acc)) continue` guard appears before the inner `Position` loop | SCAN-01; manual code review |
| **D5** | All modified methods CYC ≤ 8 (see CYC table) | SCAN-06 |
| **D6** | No P0 rule violations: zero `lock(`, zero `async void`, zero new `return null` in new methods, zero `throw new` | SCAN-01 through SCAN-04 |
| **D7** | `PttGlobalBreakEven.cs` is unchanged (guard in `ArmAllPendingBe` covers the production path) | Diff: `PttGlobalBreakEven.cs` not modified |
| **D8** | `PttQuickExit.cs` is unchanged (already leader-scoped, no follower fan-out) | Diff: `PttQuickExit.cs` not modified |

---

### Build Tag Update

After all changes are confirmed passing, update the build tag in `CopyEngine.cs`:

```csharp
// current:
internal const string Tag = "PTT-COPIER B46 | atm-template-guard | 2026-08-06";

// replace with:
internal const string Tag = "PTT-COPIER B47 | be-follower-scope | 2026-08-08";
```

---

### Hard-Link Sync (post-commit, mandatory)

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

Run after every commit to the Wave workspace. Zero broken links = PASS.

---

*Tickets authored by: ptt-architect (Phase 3, 2026-08-08)*
*Plan basis: `docs/brain/B47-LaneA/02-architecture-plan.md` REVIEW_PASS (Cycle 2)*
*Test file: Lane C — `src/PropTraderTools.Tests/B47Tests.cs` (not produced by this lane)*
