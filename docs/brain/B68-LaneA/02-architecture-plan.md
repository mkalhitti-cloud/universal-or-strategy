# B68-LaneA Architecture Plan

**Block**: B68-LaneA
**Written by**: ptt-architect (Phase 1)
**Date**: 2026-08-14
**Status**: REVIEW_PENDING
**DW item closed**: DW-B68-01 (P0) — stale follower brackets on QX and BE paths

---

## 1. Problem Summary

When Quick All or Quick (per-chart) is pressed while a position is open, or when Break-Even fires,
follower accounts retain their original ATM brackets (Stop1/Stop2/Target1/Target2) from the initial
entry. These stale orders persist alongside any new PTT-QX-* or PTT-BE-Stop orders, creating
conflicting bracket protection in live trading. Confirmed live 2026-08-13.

Two independent code paths contribute:

**Path A (QX)**: `PttGlobalQuickExit.Execute` iterates `Account.All`, skips followers, and calls
`ExecuteOne` for leaders only. `PttQuickExit.Execute` (via `ExecuteOne`) cancels leader brackets but
never touches follower brackets. Follower ATM orders survive.

**Path B (BE)**: `CopyEngine.RelayBe` iterates `AllAccounts(instr)` (master + followers) and calls
`SubmitBeStop` for each. No bracket cancellation precedes the new stop order. All accounts retain
live ATM brackets when the new BE stop is placed.

---

## 2. Code Reading Findings

All key facts from the orchestrator are confirmed by direct source inspection:

### 2.1 IsExitSignalName / DispatchCopy Gate 0.5 (line 820)

`DispatchCopy` Gate 0.5 calls `IsExitSignalName(order.Name)` and returns immediately for any name
starting with `"PTT-"`. PTT-QX-* orders placed on the leader **never reach** `DispatchCopy`.
Option C (cancel in DispatchCopy) is **structurally impossible** for QX orders. CONFIRMED.

### 2.2 SendCopy hardcodes "PTT-Copy" (line 1184)

`SendCopy` always assigns `signalName = "PTT-Copy"`. It never receives PTT-QX-* or PTT-BE-*
signal names. Option A (cancel in SendCopy) is **impossible**. CONFIRMED.

### 2.3 RelayBe calls SubmitBeStop for all accounts without prior cancellation (lines 348-352)

```csharp
public void RelayBe(BeEventArgs e)
{
    foreach (var acc in AllAccounts(e.Instrument))
        SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
}
```

No bracket cancellation occurs before `SubmitBeStop`. Fix location: **inside this foreach**.
CONFIRMED.

### 2.4 PttGlobalQuickExit.Execute skips followers (lines 29-38)

```csharp
foreach (Account acc in Account.All)                        // (1)
{
    if (engine != null && engine.IsFollowerAccount(acc)) continue; // (2)
    foreach (Position pos in acc.Positions)                 // (3)
    {
        if (pos == null || pos.Quantity == 0) continue;     // (4)
        var ticks = ResolveQuickTicks(pos.Instrument);
        ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2); // (5)
    }
}
```

`ExecuteOne` delegates to `PttQuickExit.Execute(leader, ...)` which calls
`CopyEngine.Instance?.CancelQxBrackets(leader, instr)` — leader only. Follower brackets are never
cancelled. Fix location: **PttGlobalQuickExit.Execute** — add follower cancel before `ExecuteOne`.
CONFIRMED.

### 2.5 Constraint: Do NOT modify PttQuickExit.Execute

Confirmed. PttQuickExit.Execute at lines 33-60 is untouched.

### 2.6 CopyRule.FollowerAccounts (line 181)

`CopyRule` is a `readonly struct` with `Account[] FollowerAccounts`. `FindRule(Instrument)` returns
`CopyRule?`. The new helper `CancelQxBracketsForFollowers` uses `FindRule` to obtain the rule and
iterates `rule.Value.FollowerAccounts` directly — avoids the double-loop cost of `IsFollowerAccount`.

### 2.7 AllAccounts (lines 1636-1648)

`AllAccounts(Instrument)` yields `rule.Value.MasterAccount` first, then each non-null follower.
For RelayBe, cancelling on master before SubmitBeStop is also correct: master's ATM brackets
from the original ATM strategy are replaced by the new single-leg BE stop.

### 2.8 CancelQxBrackets signature and coverage (lines 447-464)

```csharp
internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)
```

Cancels all Working/Initialized/Accepted orders for `instr` on `acc` where
`IsQxCancelCandidate(o)` returns true. Covers: ATM bracket names (Stop1/Stop2/Target1/Target2),
PTT-QX-* prefix, PTT-BE-* prefix. Internal `try { acc.Cancel(...) } catch { }` absorbs NT8
cancellation errors. **Not modified in this block.**

### 2.9 Carry-forward deferred items

The following OPEN items are **not addressed** in B68-LaneA (deferred, no scope creep):

| ID | Status |
|----|--------|
| DW-B66-C-02 (DispatchCopy dedup Gate 5 StopLimit) | OPEN — B67+ |
| DW-B66-BE-01 (CancelQxBrackets cancels PTT-BE-Stop on QX) | OPEN — Director confirm |
| DW-B63-01 (spurious PTT-Copy on Sim102 after ATM fill) | OPEN — B67+ |
| DW-B58-01/02/03, DW-B54-01 | OPEN — future/blocked |
| PRE-EXISTING-01/02/03 | OPEN — pre-existing |

---

## 3. Approach Selection

### Site 1 — PttGlobalQuickExit.cs (QX path)

**Chosen**: Add a new helper `CancelQxBracketsForFollowers(NinjaTrader.Cbi.Instrument instr)` on
`CopyEngine`, then call it from `PttGlobalQuickExit.Execute` before `ExecuteOne`.

**Why not alternative (call `CancelQxBrackets` directly from Execute)?**
`PttGlobalQuickExit` already holds an `engine` reference. A dedicated helper encapsulates follower
iteration inside `CopyEngine` (the owner of `_rules`), keeps PttGlobalQuickExit free of `CopyRule`
internals, and adds a single clear call site. This is the Jane Street "make illegal states
unrepresentable" principle applied to ownership: only `CopyEngine` knows its rule topology.

**Why not `AllAccounts(instr)` + `IsFollowerAccount(acc)` inside the helper?**
`FindRule` + `rule.Value.FollowerAccounts` is O(1) rule lookup + O(F) follower iteration.
`AllAccounts` + `IsFollowerAccount` is O(1) rule lookup + O(F) yield + O(R*F) re-check per
follower against ALL rules. The direct `FollowerAccounts` traversal is simpler and cheaper.

### Site 2 — CopyEngine.RelayBe (BE path)

**Chosen**: Expand the `foreach` body in `RelayBe` to call `CancelQxBrackets(acc, e.Instrument)`
before `SubmitBeStop(...)`.

**Justification**: One-line addition inside an existing loop. No new if-branch. CYC unchanged (2).
Guarantees every account (master and followers) has stale brackets cleared before the new BE stop.

---

## 4. Exact Code Changes

### Change 1 — CopyEngine.cs: new method `CancelQxBracketsForFollowers`

**Insert after line 464** (after the closing brace of `CancelQxBrackets`), before line 466
(`// NextQxOcoId`).

```
OLD (gap between CancelQxBrackets and NextQxOcoId):
    (no code — blank separator line at ~465)

NEW (insert 13 lines):
        // B68 DW-B68-01: CancelQxBracketsForFollowers -- cancel stale brackets on all followers.
        // Called by PttGlobalQuickExit.Execute before placing new PTT-QX-* orders on the leader.
        // Ensures follower ATM brackets (Stop1/Stop2/Target1/Target2) and prior PTT-QX-*/PTT-BE-*
        // orders do not persist as stale orders alongside new QX bracket pairs.
        // CYC=5: instr-null-guard(1) + rule-null-guard(2) + foreach(3) + acc-null-guard(4) + delegate(5).
        // JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void.
        // NT8-REF: Account.Cancel -- via CancelQxBrackets (existing, tested, line 462).
        internal void CancelQxBracketsForFollowers(NinjaTrader.Cbi.Instrument instr)
        {
            if (instr == null) return;                                   // (1)
            var rule = FindRule(instr);
            if (rule == null) return;                                    // (2)
            foreach (var acc in rule.Value.FollowerAccounts)            // (3)
            {
                if (acc == null) continue;                               // (4)
                CancelQxBrackets(acc, instr);                            // (5)
            }
        }
```

**CYC**: 1 (base) + 4 decision points (two guards + foreach + null continue) = 5.

---

### Change 2 — CopyEngine.cs: `RelayBe` expanded foreach body

**Location**: lines 348-352.

```
OLD:
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // B66 DW-B66-BE-01: e.IsLong passed to SubmitBeStop (was relying on re-read inside method -- race).
        // CYC=2 (1 base + 1 foreach branch). JS-021: no lock -- AllAccounts snapshot; SubmitBeStop lock-free.
        // JS-002: void method, no return null. JS-033: synchronous void.
        public void RelayBe(BeEventArgs e)
        {
            foreach (var acc in AllAccounts(e.Instrument))
                SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
        }

NEW:
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // B66 DW-B66-BE-01: e.IsLong passed to SubmitBeStop (was relying on re-read inside method -- race).
        // B68 DW-B68-01: CancelQxBrackets added before SubmitBeStop -- clears stale ATM brackets
        //   (Stop1/Stop2/Target1/Target2) on each account before the new BE stop is placed.
        //   No new McCabe branch: the cancel is a void call in the loop body, not an if-branch.
        // CYC=2 (unchanged: 1 base + 1 foreach branch). JS-021: no lock. JS-002: void. JS-033: synchronous.
        public void RelayBe(BeEventArgs e)
        {
            foreach (var acc in AllAccounts(e.Instrument))
            {
                CancelQxBrackets(acc, e.Instrument);
                SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);
            }
        }
```

**CYC**: Remains 2 (no new if-branch; the additional statement in the loop body is not a decision point).

---

### Change 3 — PttGlobalQuickExit.cs: `Execute` inner position loop

**Location**: lines 26-39 (method `Execute`).

```
OLD:
        /// <summary>
        /// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts.
        /// CYC=5: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4), delegate(5).
        /// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped via CopyEngine.IsFollowerAccount.
        /// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
        /// </summary>
        internal void Execute()
        {
            var engine = CopyEngine.Instance;                   // capture once
            foreach (Account acc in Account.All)                // (1)
            {
                if (engine != null && engine.IsFollowerAccount(acc)) continue; // (2) follower skip
                foreach (Position pos in acc.Positions)         // (3)
                {
                    if (pos == null || pos.Quantity == 0) continue;  // (4)
                    var ticks = ResolveQuickTicks(pos.Instrument);
                    ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2);
                }
            }
        }

NEW:
        /// <summary>
        /// Execute: all-accounts Quick Exit bracket swap, skipping follower accounts.
        /// CYC=6: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4),
        ///        engine?. null-check on cancel call(5), delegate(6).
        /// DW-B47-BE-FOLLOWER-SCOPE: follower accounts skipped via CopyEngine.IsFollowerAccount.
        /// B68 DW-B68-01: follower brackets cancelled via CancelQxBracketsForFollowers before ExecuteOne.
        /// JS-021: no lock. NT8-021: Account.All safe -- called from UI thread after Loaded.
        /// </summary>
        internal void Execute()
        {
            var engine = CopyEngine.Instance;                   // capture once
            foreach (Account acc in Account.All)                // (1)
            {
                if (engine != null && engine.IsFollowerAccount(acc)) continue; // (2) follower skip
                foreach (Position pos in acc.Positions)         // (3)
                {
                    if (pos == null || pos.Quantity == 0) continue;  // (4)
                    var ticks = ResolveQuickTicks(pos.Instrument);
                    engine?.CancelQxBracketsForFollowers(pos.Instrument); // B68 DW-B68-01 (5)
                    ExecuteOne(acc, pos.Instrument, ticks.t1, ticks.t2); // (6)
                }
            }
        }
```

**CYC**: 5 → 6. The `?.` safe-navigation operator on `engine` introduces one new McCabe decision
point (conditional null-check). Remains well within ≤ 8.

---

## 5. CYC Analysis

| Method | File | CYC Before | CYC After | Branches |
|--------|------|-----------|-----------|---------|
| `CancelQxBracketsForFollowers` | CopyEngine.cs | N/A (new) | **5** | instr null(1) + rule null(2) + foreach(3) + acc null(4) + delegate(5) |
| `RelayBe` | CopyEngine.cs | 2 | **2** | base(1) + foreach(2) — no new if-branch added |
| `Execute` | PttGlobalQuickExit.cs | 5 | **6** | +1 for `engine?.` null-check on cancel call |
| `CancelQxBrackets` | CopyEngine.cs | 6 | **6** | unchanged — not modified |
| `PttQuickExit.Execute` | PttQuickExit.cs | unchanged | unchanged | not modified |

All modified/new methods: **CYC ≤ 8**. Jane Street strict standard: PASS.

---

## 6. Test Plan

**File**: `tests/PropTraderTools.Tests/CopyEngineB68Tests.cs` (new file)
**Framework**: xUnit only. No NUnit. No MSTest.
**Namespace**: `PropTraderTools.Tests`

---

### T_B68_01 — QX path: CancelQxBracketsForFollowers cancels follower brackets, leaves master untouched

**[Fact] method name**: `T_B68_01`

**Scenario**: CopyEngine has one rule (master=MasterAcc, followers=[Follower1, Follower2]).
Follower1 has two Working ATM bracket orders: "Stop1" and "Target1".
Follower2 has one Working order: "PTT-QX-00001" (stale from a prior QX).
MasterAcc has no Working orders.

**Act**: `engine.CancelQxBracketsForFollowers(instr)`

**Assert**:
- Follower1's cancel list contains "Stop1" and "Target1"
- Follower2's cancel list contains "PTT-QX-00001"
- MasterAcc's cancel list is empty (master NOT touched by this helper)

**Why**: Verifies the core QX fix: only followers are cancelled by the new helper; master is unaffected.

---

### T_B68_02 — BE path: CancelQxBrackets fires before SubmitBeStop in RelayBe

**[Fact] method name**: `T_B68_02`

**Scenario**: CopyEngine has one rule (master=MasterAcc, follower=[Follower1]).
Follower1 has Working orders: "Stop1", "Target1".
Follower1 has an open position (qty=1, long).
MasterAcc has Working orders: "Stop2", "Target2".
MasterAcc has an open position (qty=1, long).

**Act**: `engine.RelayBe(new BeEventArgs { Instrument = instr, BePrice = 100.0, IsLong = true })`

**Assert**:
- For Follower1: cancel was called with "Stop1" and "Target1" included, BEFORE SubmitBeStop fired
- For MasterAcc: cancel was called with "Stop2" and "Target2" included, BEFORE SubmitBeStop fired
- Both accounts received a new PTT-BE-Stop order at bePrice=100.0
- Call-order verified: cancel precedes CreateOrder on each account (sequence tracking)

**Why**: Verifies the BE fix: ATM brackets are cleared before the BE stop is placed on every account.

---

### T_B68_03 — No regression: normal PTT-Copy dispatch does not trigger bracket cancellation

**[Fact] method name**: `T_B68_03`

**Scenario**: Leader places a new entry order. DispatchCopy fires with order.Name NOT starting
with "PTT-" (e.g. a plain market entry). SendCopy is expected to dispatch the order to followers.

**Act**: Simulate `DispatchCopy` firing (non-PTT-prefixed entry order on leader).

**Assert**:
- `SendCopy` is called for each follower (copy dispatched normally)
- `CancelQxBracketsForFollowers` is NOT called (no spurious cancellation on copy path)
- Follower order count unchanged after copy dispatch (no brackets removed)

**Why**: Regression guard — the QX/BE cancel path must not interfere with normal trade copying.

---

### T_B68_04 — Follower with no stale brackets: CancelQxBracketsForFollowers returns cleanly

**[Fact] method name**: `T_B68_04`

**Scenario**: CopyEngine has one rule (follower=[Follower1]).
Follower1 has ZERO Working/Accepted/Initialized orders (empty order book).

**Act**: `engine.CancelQxBracketsForFollowers(instr)`

**Assert**:
- No exception thrown (method completes without error)
- `Account.Cancel` is NOT called with a non-empty array (CancelQxBrackets exits early on empty stale list)
- Method returns without side effects

**Why**: Edge case coverage — empty bracket state must not cause null-ref or empty Cancel call.

---

### T_B68_05 — Null instrument guard: CancelQxBracketsForFollowers returns immediately

**[Fact] method name**: `T_B68_05`

**Scenario**: CopyEngine has a rule configured. Caller passes `null` as instrument.

**Act**: `engine.CancelQxBracketsForFollowers(null)`

**Assert**:
- No exception thrown (null guard at branch (1) fires)
- No `FindRule` call attempted
- No orders touched

**Why**: Defensive guard verification — null instrument must be a no-op.

---

### T_B68_06 — RelayBe with no rule: method returns cleanly without error

**[Fact] method name**: `T_B68_06`

**Scenario**: CopyEngine has NO rule configured for the given instrument.
RelayBe fires for that instrument.

**Act**: `engine.RelayBe(new BeEventArgs { Instrument = unknownInstr, BePrice = 99.0, IsLong = true })`

**Assert**:
- No exception thrown
- `AllAccounts(unknownInstr)` yields no accounts (rule not found, yields break)
- Neither CancelQxBrackets nor SubmitBeStop is called

**Why**: Edge case — BE event for un-configured instrument must not crash.

---

## 7. 7-Scan Checklist (Engineer Runs)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| **S1** | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 hits in new/changed code (excl. comments) |
| **S2** | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 hits in new/changed code |
| **S3** | Complexity audit: `python scripts/complexity_audit.py --file src/PropTraderTools/CopyEngine.cs` | `CancelQxBracketsForFollowers` CYC=5 ≤ 8; `RelayBe` CYC=2 ≤ 8 |
| **S4** | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 new hits in lines added by B68 (pre-existing on lines 398, 499 are exempt) |
| **S5** | `grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` | 0 hits |
| **S6** | Manual inspect of all string literals in changed lines | ASCII-only confirmed; no new string literals except comments |
| **S7** | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings in changed files |

---

## 8. NT8 Citations

| Fact | Source |
|------|--------|
| `Account.Cancel(Order[])` — cancel method used by CancelQxBrackets | NT8_FULL_REFERENCE.md (existing CopyEngine.cs line 462, established in prior blocks) |
| ATM bracket order names: "Stop1", "Stop2", "Target1", "Target2" | NT8_FULL_REFERENCE.md line 1631 (cited at CopyEngine.cs line 424) |
| `Account.All` — safe from UI thread after Loaded | NT8_FULL_REFERENCE.md (NT8-021, cited in PttGlobalQuickExit.cs line 5) |
| `CreateOrder()` requires `Submit()` | NT8_FULL_REFERENCE.md (confirmed B57, cited CopyEngine.cs line 1204) |
| `AtmStrategyCreate()` is StrategyBase-only — NOT used here | NT8_FULL_REFERENCE.md (DW-B54-01 OPEN-blocked, not relevant to B68) |

No new NT8 API surface is introduced in B68-LaneA. All NT8 calls delegate through existing
`CancelQxBrackets` and `SubmitBeStop` which have established NT8 API usage patterns.

---

## 9. Files Modified

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Add method + modify method | Add `CancelQxBracketsForFollowers` (~line 465); expand `RelayBe` body (lines 348-352) |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Modify method | Add `engine?.CancelQxBracketsForFollowers(pos.Instrument)` in `Execute` (lines 32-38) |
| `tests/PropTraderTools.Tests/CopyEngineB68Tests.cs` | New file | T_B68_01..T_B68_06 [Fact] tests |

**NOT modified** (by constraint or design):
- `src/PropTraderTools/Features/PttQuickExit.cs` — DO NOT TOUCH
- `src/PropTraderTools/CopyEngine.cs` — `IsQxCancelCandidate`, `IsAtmBracketName` — DO NOT TOUCH
- All other files

---

## Deferred Items Carried Forward

All OPEN items from B66-LaneC/06-deferred-backlog.md are carried forward unchanged.

**New deferred item opened by B68-LaneA**: None.

---

**Return**: PLAN_COMPLETE
