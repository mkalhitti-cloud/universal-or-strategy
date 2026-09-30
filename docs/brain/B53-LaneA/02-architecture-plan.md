# PTT-COPIER-B53-LaneA: Architecture Plan
# DW-B53-01 — Remove PttFollowerStrategy from follower entry-order path
#
# Status: DRAFT — pending ptt-plan-reviewer
# Author: ptt-architect (Phase 1)
# Date: 2026-08-09

---

## 1. Problem Statement

When CopyEngine (an AddOn citizen) submits a follower entry order via `Account.CreateOrder()`,
PttFollowerStrategy — a managed NinjaScript Strategy running on the same follower account — holds
open entry slot allocations under NT8's managed order framework (IsUnmanaged=False,
EntriesPerDirection=1). The framework detects a slot conflict between the externally-submitted
AddOn order and the managed strategy's claimed entry slot, stalling the AddOn order at
`OrderState.Initialized` and preventing exchange submission. Cancellation from AddOn context then
fails with a permanent "Cancel pending" state that requires an account reset to clear. The fix is
to remove PttFollowerStrategy from the live order path entirely: gate the class at compile time,
strip the PttBus.RaiseFillSignal call from SendCopy, and attach ATM brackets directly from
CopyEngine's OnOrderUpdate on the confirmed follower fill event.

---

## 2. Root Cause (Confirmed)

- PttFollowerStrategy: `IsUnmanaged=False`, `EntriesPerDirection=1` — managed framework owns entry
  slots for the follower account.
- CopyEngine calls `follower.CreateOrder()` from AddOn context. NT8 sees a slot conflict with the
  running managed strategy and holds the order at `Initialized`.
- `Account.Cancel()` from AddOn context on an Initialized order produces "Cancel pending" — a
  permanent stuck state requiring account reset.
- Source: `ARCH-BRACKET-03` probe (B42, live Sim101 test 2026-08-05); NT8-053 in
  `docs/standards/NT8_COMPILER_RULES.md`.

---

## 3. Files to Change

| File | Wave Workspace Path | Change Type |
|------|-------------------|-------------|
| CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | MODIFIED — 4 changes |
| PttFollowerStrategy.cs | `src/PropTraderTools/Features/PttFollowerStrategy.cs` | MODIFIED — compile-time gate |

**Not touched (out of scope, No Scope Creep Protocol §11):**
- `src/PropTraderTools/Core/PttContracts.cs` — `FillSignal` event and `FillSignalEventArgs` left
  intact; zero subscribers at runtime is harmless dead code. Removing them is a separate cleanup epic.

---

## 4. Method-Level Change List

### 4A. `CopyEngine.cs` — `OnOrderUpdate` (lines 468–510)

**What changes:** Add a new early-exit branch BETWEEN Gate 1 (`!_isCopyEnabled`) and Gate 2
(master-account match). The branch intercepts `PTT-Copy` fill events on follower accounts, calls
`TryAttachAtmToFollower`, and returns immediately to prevent the follower fill from being
dispatched as a copy signal (which would cause cascade copies).

**Lines affected:** Insert ~6 lines after line 475 (after Gate 1 return).

**New code block:**
```csharp
// B53 DW-B53-01: ATM attach on confirmed follower fill.
// PTT-Copy filled on a follower account -- find matching rule and attach ATM.
// Return immediately: follower fills are not master signals; must not cascade-copy.
if (e.Order.OrderState == OrderState.Filled && e.Order.Name == "PTT-Copy")
{
    var followerRule = FindRuleByFollower(e.Order.Account, e.Order.Instrument);
    if (followerRule != null)
        TryAttachAtmToFollower(e.Order.Account, followerRule.Value, e.Order);
    return;
}
```

**CYC before:** ~6 branches
**CYC after:** ~8 branches (+2: the outer Filled/PTT-Copy check + the inner null check)
**Verdict:** CYC = 8. At limit. PASSES.

---

### 4B. `CopyEngine.cs` — `SendCopy` (lines 829–881)

**What changes:** Remove the `PttBus.RaiseFillSignal(...)` block (lines 867–873). The `return true`
statement moves up to immediately follow the `CreateOrder` call (after the closing brace of
`CreateOrder`'s argument list at the existing line 866).

**Lines affected:** Delete lines 867–873 inclusive. The method body becomes:
```csharp
follower.CreateOrder( ... );  // lines 853-866 unchanged
return true;                  // was line 874, moves to line 867
```

**CYC before:** 3 branches (Market branch, Named template ternary, try/catch)
**CYC after:** 3 branches — unchanged
**Verdict:** CYC = 3. PASSES.

---

### 4C. `CopyEngine.cs` — NEW: `TryAttachAtmToFollower`

**Full signature:**
```csharp
private void TryAttachAtmToFollower(Account acc, CopyRule rule, Order order)
```

**Parameters:**
| Param | Type | Meaning |
|-------|------|---------|
| `acc` | `Account` | Follower account — the account that owns the filled order |
| `rule` | `CopyRule` | The copy rule containing the ATM template map for this follower |
| `order` | `Order` | The filled `PTT-Copy` order on the follower account |

**Purpose:** Resolve the ATM template for this follower; if Named mode, call
`NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` via static API to attach brackets.
If Inherit or Market mode, returns without action.

**Implementation:**
```csharp
// B53 DW-B53-01: Attach ATM brackets on follower fill. CYC=4.
// JS-001: try/catch, no throw. JS-002: void return. JS-021: no lock.
// NT8-API: NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate -- static call.
// RISK: static AtmStrategyCreate availability unconfirmed in Linting DLL
//       (same class-boundary pattern as NT8-045). Verified working in F5 runtime only.
//       Add NT8-055 to COMPILER_RULES if static call fails at F5 gate.
private void TryAttachAtmToFollower(Account acc, CopyRule rule, Order order)
{
    var mode = ResolveAtmMode(rule, acc.Name);   // branch (1): Inherit returns early below
    if (!(mode is FollowerAtmMode.Named named))
        return;                                   // Inherit or Market -- no ATM attach
    string templateName = named.TemplateName;
    if (string.IsNullOrWhiteSpace(templateName))  // branch (2): belt-and-suspenders
        return;
    try                                           // branch (3): protect against NT8 exceptions
    {
        NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate(
            order.OrderAction,
            OrderType.Market,
            0,
            0,
            TimeInForce.Gtc,
            string.Empty,
            templateName,
            Guid.NewGuid().ToString("N").Substring(0, 8),
            (code, msg) =>
            {
                if (code != ErrorCode.NoError)
                    StatusUpdate?.Invoke("PTT-ATM error: " + msg);
            });
    }
    catch (Exception ex)                          // branch (4): log and return, never rethrow
    {
        StatusUpdate?.Invoke("PTT-ATM static error: " + ex.Message);
    }
}
```

**CYC breakdown:**
| Branch | Condition |
|--------|-----------|
| (1) | `!(mode is FollowerAtmMode.Named named)` → return |
| (2) | `string.IsNullOrWhiteSpace(templateName)` → return |
| (3) | try block (implicit branch on exception) |
| (4) | catch block |

**CYC = 4. PASSES.**

**⚠ UNCONFIRMED NT8 STATIC API — CRITICAL:**
`NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate(...)` as a **static** call is **not yet
confirmed in this codebase**. Existing code (ARCH-BRACKET-03, PttFollowerStrategy.cs L74) uses the
StrategyBase **instance** method. NT8-045 confirms `NinjaTrader.NinjaScript.AtmStrategy` has
static members accessible in the F5 runtime that are NOT in the Linting DLL. The static
`AtmStrategyCreate` overload likely follows the same pattern — compiles at F5 but absent from
`NinjaTrader.Custom.dll` backup used for linting. The engineer **must** verify this at F5 gate
(Step 7 in SCAN checklist). If the static call does not exist, record NT8-055 and escalate to
Director before completing the ticket. Do not silently fall back to PttFollowerStrategy.

---

### 4D. `CopyEngine.cs` — NEW: `FindRuleByFollower`

**Full signature:**
```csharp
private CopyRule? FindRuleByFollower(Account follower, Instrument instrument)
```

**Parameters:**
| Param | Type | Meaning |
|-------|------|---------|
| `follower` | `Account` | Account to search for in `rule.FollowerAccounts` |
| `instrument` | `Instrument` | Instrument to match against `rule.Instrument` |

**Returns:** `CopyRule?` (nullable struct — null means no rule found). This is a C# nullable value
type, NOT a null reference return (JS-002 satisfied: there is no null reference; struct-null is
the Option<T> equivalent for value types in this codebase).

**Implementation:**
```csharp
// B53 DW-B53-01: Find copy rule by follower account + instrument. CYC=3.
// JS-002: returns CopyRule? nullable struct (not a null reference). JS-021: no lock.
// Mirrors FindRule(Instrument) (L1418) but searches follower array, not master.
private CopyRule? FindRuleByFollower(Account follower, Instrument instrument)
{
    if (follower == null || instrument == null)   // branch (1): null guard
        return null;
    foreach (var rule in _rules)                  // branch (2): foreach
    {
        if (rule.Instrument != instrument.FullName) continue;
        foreach (var acc in rule.FollowerAccounts) // branch (3): inner foreach
        {
            if (acc != null && acc.Name == follower.Name)
                return rule;
        }
    }
    return null;
}
```

**CYC breakdown:**
| Branch | Condition |
|--------|-----------|
| (1) | null guard at entry |
| (2) | outer foreach + instrument filter |
| (3) | inner foreach + account name match |

**CYC = 3. PASSES.**

**Note on JS-002:** `return null` here returns `CopyRule?` (a `Nullable<CopyRule>` struct).
This is identical to the existing pattern in `FindRule(Instrument)` at line 1418 which also returns
`CopyRule?`. The scan for `return null;` must be scoped to reference-type returns. This pattern
is already established in the codebase; no JS-002 violation.

---

### 4E. `PttFollowerStrategy.cs` — Compile-Time Gate

**What changes:** Wrap the entire class body with a preprocessor conditional. The file is retained
for NT8 import safety — if a user's NT8 installation already has `PttFollowerStrategy` compiled,
the file must exist to avoid CS0246 on import. With the gate, the class silently compiles away in
the default (non-gated) build.

**Lines affected:** Entire file.

**New structure:**
```csharp
// PTT-COPIER-B42 -- PttFollowerStrategy.cs
// [... existing header comments ...]
// B53 DW-B53-01: COMPILE-TIME GATE. Class is inactive in production build.
// Define PTT_FOLLOWER_ACTIVE to restore the pre-B53 architecture.
// DO NOT DELETE this file -- NT8 AddOn import safety requires the file to exist.

#if PTT_FOLLOWER_ACTIVE
using System;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace PropTraderTools
{
    public class PttFollowerStrategy : Strategy
    {
        // ... existing class body unchanged ...
    }
}
#endif
```

**CYC impact:** None. Compile-time gate does not affect runtime CYC.

---

## 5. New Method: TryAttachAtmToFollower — Summary

| Property | Value |
|----------|-------|
| Signature | `private void TryAttachAtmToFollower(Account acc, CopyRule rule, Order order)` |
| File | `src/PropTraderTools/CopyEngine.cs` |
| CYC | 4 |
| JS-001 | try/catch wraps all NT8 calls; no throw |
| JS-002 | void return; no null reference returned |
| JS-021 | No lock(); ResolveAtmMode already lock-free |
| JS-033 | private void; not async |
| NT8-001 | No init setters |
| NT8-003 | No volatile double |
| API risk | Static `AtmStrategy.AtmStrategyCreate` — unconfirmed in Linting DLL; F5 gate required |

---

## 6. Gating Strategy for PttFollowerStrategy.cs

**Mechanism:** C# preprocessor directive (`#if PTT_FOLLOWER_ACTIVE`).

**Rationale:**
- NT8 import safety: users who have previously loaded this Add-On may have `PttFollowerStrategy`
  compiled in their NT8 installation. Deleting the file would cause CS0246 on next NT8 reload.
  The `#if` gate silently compiles the class away without removing the file.
- Rollback path: Define `PTT_FOLLOWER_ACTIVE` in project properties to restore pre-B53 behavior.
- Tests: Existing B42 tests and B46 tests reference `PttFollowerStrategy` via subclass. These tests
  should also be wrapped in `#if PTT_FOLLOWER_ACTIVE` in their respective test files. This is IN
  SCOPE for the engineer (see T3 ticket scope below).

**Tests to gate:**
- `src/PropTraderTools/Tests/B42Tests.cs` — all tests subclassing PttFollowerStrategy
- `src/PropTraderTools/Tests/B46Tests.cs` — all tests using PttFollowerStrategy
- Wrap test class bodies (not individual methods) with `#if PTT_FOLLOWER_ACTIVE / #endif`.

---

## 7. Tests Required (xUnit [Fact])

All tests in: `src/PropTraderTools/Tests/B53Tests.cs`

```csharp
[Fact] T_B53_AtmAttachFiresOnFollowerFill
// Arrange: CopyEngine with follower account AccB, CopyRule with Named ATM ("MyTmpl") for AccB.
// Act: Simulate OnOrderUpdate with Order{Name="PTT-Copy", OrderState=Filled, Account=AccB, Instrument=ES}.
// Assert: TryAttachAtmToFollower stub called exactly once with correct account + rule + order.
```

```csharp
[Fact] T_B53_AtmSkippedOnInheritMode
// Arrange: CopyRule with Inherit ATM mode for AccB.
// Act: Same fill event as above.
// Assert: AtmStrategyCreate stub never invoked (counter = 0).
```

```csharp
[Fact] T_B53_AtmSkippedWhenOrderStateNotFilled
// Arrange: Order{Name="PTT-Copy", OrderState=Submitted}.
// Act: OnOrderUpdate fires.
// Assert: TryAttachAtmToFollower not called.
```

```csharp
[Fact] T_B53_AtmSkippedWhenNameIsNotPttCopy
// Arrange: Order{Name="PTT-BE-Stop", OrderState=Filled} on follower account.
// Act: OnOrderUpdate fires.
// Assert: TryAttachAtmToFollower not called (name guard fires).
```

```csharp
[Fact] T_B53_SendCopyNoLongerRaisesFillSignal
// Arrange: Subscribe counter delegate to PttBus.FillSignal.
// Act: Invoke SendCopy path (via testable subclass or reflection) with a Named ATM mode.
//      CreateOrder is stubbed to succeed.
// Assert: PttBus.FillSignal counter == 0 after successful SendCopy.
// Also assert: method returns true (success).
```

```csharp
[Fact] T_B53_FindRuleByFollowerReturnsMatchingRule
// Arrange: CopyEngine._rules contains one CopyRule{Instrument="ES 09-25", Master=AccA, Followers=[AccB]}.
// Assert: FindRuleByFollower(AccB, ESInstr).HasValue == true.
// Assert: FindRuleByFollower(AccA, ESInstr).HasValue == false (master is not a follower).
// Assert: FindRuleByFollower(AccC, ESInstr).HasValue == false (unknown account).
```

```csharp
[Fact] T_B53_FindRuleByFollowerNullSafe
// Assert: FindRuleByFollower(null, ESInstr).HasValue == false (null guard).
// Assert: FindRuleByFollower(AccB, null).HasValue == false (null guard).
```

**Test isolation strategy:** All tests must work without NT8 runtime. CopyEngine testable subclass
exposes `TryAttachAtmToFollower` and `FindRuleByFollower` as `internal` (or via virtual seam
override) for verification. The static `AtmStrategy.AtmStrategyCreate` call is NOT tested in
xUnit — it is an NT8 runtime API that can only be verified at F5 gate.

---

## 8. Seven-Scan Checklist (SCAN-01 through SCAN-07)

This checklist is the engineer's **contract**. Every item must pass before the ticket is complete.

| Scan | Rule | Command / Check | Expected Result |
|------|------|-----------------|-----------------|
| SCAN-01 | JS-021 — No lock() | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttFollowerStrategy.cs` | 0 matches |
| SCAN-02 | JS-002 — No null ref return | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` then manually confirm all hits are `CopyRule?` nullable struct returns (not reference-type nulls) | All `return null` hits are `CopyRule?` returns only |
| SCAN-03 | JS-033 — No async void | `grep -n "async void" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttFollowerStrategy.cs` | 0 matches |
| SCAN-04 | JS-001 — No throw in hot path | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 matches in new or modified methods |
| SCAN-05 | NT8-001 — No init accessors | `grep -n "init;" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Features/PttFollowerStrategy.cs` | 0 matches |
| SCAN-06 | NT8-013 + NT8-003 — No DateTime.Now, no volatile double | `grep -n "DateTime\.Now\|volatile double" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-07 | CYC <= 8 — Method complexity | Manual count of all modified + new methods: OnOrderUpdate(8), TryAttachAtmToFollower(4), FindRuleByFollower(3), SendCopy(3) | All <= 8 |

**Additional verification (not a SCAN number but mandatory before ticket close):**

| Step | Check | Action if fails |
|------|-------|-----------------|
| F5-GATE-01 | `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` static call compiles at NT8 F5 | Add NT8-055 to COMPILER_RULES, escalate to Director |
| F5-GATE-02 | ATM brackets appear on follower account after fill in Sim101 live test | Diagnose via ARCH-BRACKET-03 pattern; may need Director decision |
| BUILD-01 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` passes with 0 errors | Fix any linting errors before F5 |
| TEST-01 | All B53Tests.cs [Fact] tests pass green | Fix test failures before F5 |
| LINK-01 | `powershell -File scripts\verify_links.ps1 -Fix` succeeds; new test file excluded from deploy | Add B53Tests.cs to $DeployExcludes in verify_links.ps1 |

---

## 9. Requirement Traceability: DW-B53-01

| Requirement | File | Change | Satisfied By |
|-------------|------|--------|-------------|
| DW-B53-01 — Remove PttFollowerStrategy from follower entry path | PttFollowerStrategy.cs | Compile-time `#if PTT_FOLLOWER_ACTIVE` gate | Section 4E; PttFollowerStrategy class inactive at runtime |
| DW-B53-01 — Zero per-follower strategy setup required | CopyEngine.cs (SendCopy) | Remove `PttBus.RaiseFillSignal` lines 867–873 | Section 4B; no FillSignal raised, no strategy needed |
| DW-B53-01 — CopyEngine places follower entry orders directly as AddOn citizen | CopyEngine.cs | No change to entry placement (already AddOn) | Existing SendCopy unchanged except FillSignal removal |
| DW-B53-01 — AtmStrategyCreate called on follower fill | CopyEngine.cs (OnOrderUpdate + TryAttachAtmToFollower) | New branch in OnOrderUpdate; new helper | Sections 4A + 4C; ATM attach fires on Filled+PTT-Copy |
| DW-B53-01 — No entry slot conflict | PttFollowerStrategy.cs gate | Strategy not running on follower account | Section 4E; managed framework no longer holds entry slots |

---

## 10. Risk Register

| Risk | Severity | Probability | Mitigation |
|------|----------|-------------|------------|
| `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` static call absent from Linting DLL | P1 | HIGH (same pattern as NT8-045) | Expected: code compiles at F5 but not in Linting. Add `#if NT8_RUNTIME` guard if Linting fails. |
| Static `AtmStrategyCreate` does not exist in F5 runtime at all | P0 | LOW (planner has specific call signature) | If confirmed absent: record NT8-055, escalate Director. PttFollowerStrategy stays gated; alternative ATM path required. |
| B42Tests.cs + B46Tests.cs break due to #if gate on PttFollowerStrategy | P1 | CERTAIN | Engineer must gate test files too (in scope; see Section 6) |
| FindRuleByFollower O(N*M) scan | P2 | LOW | _rules count is typically 1-3 rules; N*M is negligible. No optimization needed. |

---

## Appendix: Data Flow Comparison

```
BEFORE (B42 architecture):
  Master filled → DispatchCopy → SendCopy
  SendCopy: CreateOrder(follower) + RaiseFillSignal(bus)
            └── PttFollowerStrategy.OnFillSignal → CallAtmStrategyCreate
  PROBLEM: PttFollowerStrategy holds entry slot → CreateOrder stalls at Initialized

AFTER (B53 architecture):
  Master filled → DispatchCopy → SendCopy
  SendCopy: CreateOrder(follower) [only]
  Follower order reaches exchange unimpeded
  Follower order Filled → OnOrderUpdate (new branch)
            └── FindRuleByFollower → TryAttachAtmToFollower
                └── NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate [static]
```

---

*Plan status: DRAFT. Awaiting ptt-plan-reviewer.*
