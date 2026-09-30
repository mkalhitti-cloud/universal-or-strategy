# B42-LaneA — Plan Review
**Block**: PTT-COPIER-B42 — PTTFollowerStrategy: Native ATM Brackets on Followers
**Phase**: 2 — Plan Review (Cycle 2)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-05
**Plan under review**: `docs/brain/B42-LaneA/02-architecture-plan.md` (cycle 2 revision)
**Spec reference**: `specs/002-trade-copier-spec.html#block-b42` (lines 19597–19766)

---

## VERDICT: REVIEW_PASS

**Zero violations found.** Cycle 1 violation V-01 (JS-010) is confirmed corrected.
Ticket generation is now unlocked.

---

## Cycle History

| Cycle | Verdict | Violations |
|-------|---------|-----------|
| 1 | REVIEW_FAIL | V-01: JS-010 — `FillSignalEventArgs` had public constructor; no `Create()` factory |
| 2 | **REVIEW_PASS** | **0 violations** |

---

## Category 1 — Spec Traceability

### Coverage Matrix

| Spec Requirement | Plan Addressed? | Plan Section |
|-----------------|-----------------|--------------|
| `FillSignalEventArgs` has 6 required fields: `Account`, `Instrument`, `AtmTemplateName`, `OrderAction`, `Quantity`, `EntryOrderId` | ✅ YES | §2 field table |
| `PttBus.FillSignal` declared as `static event Action<FillSignalEventArgs>` (NOT `EventHandler<T>`) | ✅ YES | §3 event declaration |
| `RaiseFillSignal` signature: `public static void RaiseFillSignal(FillSignalEventArgs args)` | ✅ YES | §3 method body |
| `RaiseFillSignal` null-guard pattern: local-copy-then-null-check (NT8-043) | ✅ YES | §3 |
| `SendCopy` publish point: after `CreateOrder`, before `return true`, inside try block | ✅ YES | §4 |
| `SendCopy` does NOT publish if `CreateOrder` throws | ✅ YES | §4 rationale |
| `PTTFollowerStrategy.OnStateChange` — `SetDefaults`, `Realtime` subscribe, `Terminated` unsubscribe | ✅ YES | §5 skeleton |
| `PTTFollowerStrategy.OnFillSignal` — account guard + instrument guard | ✅ YES | §5 skeleton |
| `AtmStrategyCreate` — 9-arg call (action, Market, 0, 0, GTC, entryOrderId, templateName, guid, callback) | ✅ YES | §5 skeleton |
| `OnBarUpdate` — empty body (headless strategy) | ✅ YES | §5 skeleton |
| All 7 `[Fact]` tests covered: T_B42_01 through T_B42_07 | ✅ YES | §6 test table |
| New file `Features/PttFollowerStrategy.cs` | ✅ YES | §1 file table |
| Modify `Core/PttContracts.cs` (spec mislabels this `PttBus.cs` — plan correctly targets `PttContracts.cs`) | ✅ YES | §1, §3 |
| Modify `CopyEngine.cs` `SendCopy()` | ✅ YES | §1, §4 |

**Spec Traceability Result**: ✅ PASS — all spec requirements addressed.

**Note on spec field count discrepancy**: Spec line 19749 (test summary) lists 5 fields for T_B42_01. The authoritative spec code block at lines 19684–19693 requires `args.EntryOrderId` as the ATM entry order ID. Plan correctly includes all 6 fields; the 5-field summary in the test listing is an abbreviated description, not a constraint.

**Note on spec file name discrepancy**: Spec line 19740 references `src/PropTraderTools/Core/PttBus.cs` — no such file exists. `PttBus` class lives in `PttContracts.cs` (confirmed by reading live source). Plan correctly targets `PttContracts.cs`.

---

## Category 2 — Jane Street Rule Pre-Check (Cycle 2)

### V-01 Confirmation — JS-010 (P1)

**Rule**: JS-010 — "Public constructor on singleton or signal struct = FAIL"
**Cycle 1 finding**: Public constructor on `FillSignalEventArgs`.
**Cycle 2 status**: ✅ **FIXED**

Evidence from plan §2 (cycle 2 revision):
```csharp
private FillSignalEventArgs(
    Account     account,
    Instrument  instrument,
    string      atmTemplateName,
    OrderAction orderAction,
    int         quantity,
    string      entryOrderId)
{ ... }

public static FillSignalEventArgs Create(
    Account account, Instrument instrument, string atmTemplateName,
    OrderAction orderAction, int quantity, string entryOrderId)
    => new FillSignalEventArgs(account, instrument,
           atmTemplateName, orderAction, quantity, entryOrderId);
```

Constructor is `private`. Only public construction path is `FillSignalEventArgs.Create(...)`.
Consistent with `CopyRule.Create` / `CopySignal.Create` patterns in the codebase. **JS-010 PASS.**

### Full JS Rule Scan (Cycle 2)

| Rule ID | Severity | Check | Result | Evidence |
|---------|----------|-------|--------|---------|
| JS-021 | P0 | No `lock()` anywhere in B42 scope | ✅ PASS | CLR atomic delegate `+=` / `-=`; `RaiseFillSignal` uses local-copy pattern; zero `lock(` keywords in any planned code |
| JS-001 | P0 | No `throw new XxxException` in hot paths | ✅ PASS | `RaiseFillSignal` is void; `SendCopy` catch path logs and returns false (unchanged); error in `OnFillSignal` callback uses `Print(...)`, not `throw` |
| JS-002 | P0 | No `return null` for value-type returns | ✅ PASS | All B42 methods return `void` or `bool`; `Create()` returns a value-type struct (cannot be null) |
| JS-033 | P0 | No `async void` (non-event-handler) | ✅ PASS | No async methods anywhere in B42 scope; `OnFillSignal` is `private void`; `OnBarUpdate` is `protected override void`; `OnStateChange` is `protected override void` |
| **JS-010** | **P1** | **Private constructor + public static factory on signal structs** | ✅ **PASS (fixed cycle 2)** | Constructor is `private`; `FillSignalEventArgs.Create(...)` is sole public construction path |
| JS-008 | P1 | Readonly structs for immutable data | ✅ PASS | `FillSignalEventArgs` declared `public readonly struct`; all 6 fields `{ get; private set; }` |
| JS-003 | P0 | Magic string / discriminated state | N/A | `FillSignalEventArgs` is a data carrier struct, not a sum type |
| JS-009 | P1 | `Dictionary<K,V>` for shared/thread-touched collection | ✅ PASS | No `Dictionary<K,V>` in B42 scope |

**JS Rule Result**: ✅ PASS — zero violations.

---

## Category 3 — Focus Check: T2 Call Site (Cycle 2)

**Check**: Does `SendCopy()` T2 diff use `FillSignalEventArgs.Create(...)` (not `new FillSignalEventArgs(...)`)?

From plan §4 exact diff:
```csharp
PttBus.RaiseFillSignal(FillSignalEventArgs.Create(
    follower,
    instrument,
    atmTemplate ?? string.Empty,
    signal.Action,
    signal.Quantity,
    signal.OrderId));
return true;
```

✅ `FillSignalEventArgs.Create(...)` used. No `new FillSignalEventArgs(...)` anywhere in the plan's T2 diff. **T2 call site PASS.**

---

## Category 4 — Focus Check: T4 Tests (Cycle 2)

**Check**: Do tests use `FillSignalEventArgs.Create(...)` for struct construction?

- T_B42_01 explicitly stated: *"Call `FillSignalEventArgs.Create(...)`; assert all 6 field values round-trip correctly"* (plan §6 test table). ✅ Explicit use.
- T_B42_03, T_B42_04, T_B42_05: Pass a `FillSignalEventArgs` to `OnFillSignal`. Constructor is `private`; the only available construction path is `Create()`. Any test code must use `FillSignalEventArgs.Create(...)`. ✅ Forced by type system.
- T_B42_06, T_B42_07: Subscribe counter lambda to `PttBus.FillSignal`; trigger via `SendCopy()`. The `FillSignalEventArgs` is constructed inside `SendCopy()` via `Create()` (per T2 diff). Tests do not directly construct the struct. ✅ Correct.

**T4 tests PASS.**

---

## Category 5 — NT8 Compiler Rule Pre-Check (Cycle 2)

| Rule ID | Description | Result | Evidence |
|---------|-------------|--------|---------|
| NT8-001 | No `init` accessors — use `{ get; private set; }` + constructor | ✅ PASS | All 6 fields use `{ get; private set; }`; constructor assigns all; cycle 2 revision unchanged |
| NT8-002 | No `record` or `abstract record` | ✅ PASS | `FillSignalEventArgs` is a `struct`; `PttFollowerStrategy` is a `class`; no records |
| NT8-003 | No `volatile double` | ✅ PASS | `FillSignalEventArgs` has no `double` fields; `PttFollowerStrategy` has no fields at all |
| NT8-007 | `CreateOrder` arg12 must be `(NinjaTrader.Cbi.CustomOrder)null` | ✅ PASS | `CopyEngine.SendCopy` already passes `(NinjaTrader.Cbi.CustomOrder)null`; B42 makes no change to that call |
| NT8-033 | No `async void` methods | ✅ PASS | No async methods in B42 scope |
| NT8-043 | Local-copy-then-null-check pattern for events | ✅ PASS | `RaiseFillSignal`: `var h = FillSignal; if (h != null) h(args);` |

**NT8 Compiler Result**: ✅ PASS — zero violations.

---

## Category 6 — CYC Analysis (Cycle 2)

| Method | File | CYC (plan claim) | Reviewer Verified | Result |
|--------|------|-----------------|-------------------|--------|
| `FillSignalEventArgs` constructor (private) | PttContracts.cs | 1 | 6 assignments, 0 branches = CYC 1 | ✅ PASS |
| `FillSignalEventArgs.Create` (static factory) | PttContracts.cs | 1 | Expression-body, 0 branches = CYC 1 | ✅ PASS |
| `RaiseFillSignal` | PttContracts.cs | 2 | 1 null-check branch = CYC 2 | ✅ PASS |
| `SendCopy` (after T2 change) | CopyEngine.cs | 5 (unchanged) | `RaiseFillSignal` call adds 0 branches; existing CYC=5 preserved | ✅ PASS |
| `OnStateChange` | PttFollowerStrategy.cs | 4 | 3 `if/else if` on `State` value = CYC 4 | ✅ PASS |
| `OnBarUpdate` | PttFollowerStrategy.cs | 1 | Empty body, 0 branches | ✅ PASS |
| `OnFillSignal` | PttFollowerStrategy.cs | 3 | 2 guard returns + 1 `if (code != NoError)` in lambda = CYC 3 | ✅ PASS |
| `CallAtmStrategyCreate` (virtual hook, §6 note) | PttFollowerStrategy.cs | 1 | Single call to `AtmStrategyCreate`; no branches | ✅ PASS |

All methods ≤ 8. **CYC Result**: ✅ PASS.

---

## Category 7 — Invariants (Cycle 2)

| Invariant | Result | Evidence |
|-----------|--------|---------|
| Leader account untouched | ✅ PASS | `SendCopy()` is only called for follower accounts; `PttFollowerStrategy` instances configured with follower accounts only; `PttBus.FillSignal` never raised for leader |
| Existing tests remain green (additive only) | ✅ PASS | No existing file is modified except targeted +5 line insertion in `CopyEngine.cs` try block and +25 lines in `PttContracts.cs`; all existing `CopyEngine` tests operate on code paths that precede the new lines |
| `PttContracts.cs` existing types unchanged | ✅ PASS | Plan §9 lists all 5 existing EventArgs types and 5 existing PttBus events as unchanged; confirmed by reading live `PttContracts.cs` |
| No circular dependency introduced | ✅ PASS | `PttFollowerStrategy` → `PttBus` (subscribes); `CopyEngine` → `PttBus` (raises); `PttFollowerStrategy` not referenced by `CopyEngine` — one-directional via event bus |
| `PttContracts.cs` existing `EventHandler<T>` pattern preserved | ✅ PASS | New `FillSignal` event uses `Action<FillSignalEventArgs>` (struct, not EventArgs subclass) — distinct type; no change to existing events |

**Invariants Result**: ✅ PASS.

---

## Category 8 — Architecture Correctness (Cycle 2)

| Check | Result | Evidence |
|-------|--------|---------|
| FillSignal publish is additive (does NOT change existing `SendCopy` behaviour) | ✅ PASS | Call inserted AFTER `CreateOrder`, BEFORE `return true`; catch path unchanged; existing return semantics preserved |
| `PTTFollowerStrategy` is account-scoped (guards by `Account.Name`) | ✅ PASS | `if (args.Account.Name != Account.Name) return;` — first guard in `OnFillSignal` |
| Headless strategy — no UI dependencies | ✅ PASS | No `Window`, `Grid`, `Canvas`, `DispatcherObject`, or UI namespace imports in skeleton |
| `FillSignal` event type is `Action<T>` not `EventHandler<T>` | ✅ PASS | Spec and plan both use `Action<FillSignalEventArgs>`; struct is not `EventArgs` subclass |
| `AtmStrategyCreate` called with 9 args matching spec exactly | ✅ PASS | Spec code block lines 19684–19693 and plan §5 match exactly: action, Market, 0, 0, GTC, entryOrderId, templateName, guid, callback |
| `PttFollowerStrategy` in `namespace PropTraderTools` (flat, consistent with codebase) | ✅ PASS | Plan §5 |
| `signal.OrderId` assumption flagged as engineer verification required | ✅ PASS | Plan §10 Open Question 1 explicitly flags this for engineer verification |
| `protected virtual CallAtmStrategyCreate` test hook pattern | ✅ PASS | CYC=1; no JS or NT8 violation; enables T_B42_05 without NT8 runtime |

**Architecture Correctness Result**: ✅ PASS.

---

## Summary

| Category | Result |
|----------|--------|
| 1. Spec Traceability | ✅ PASS |
| 2. JS Rule Pre-Check | ✅ PASS |
| 3. Focus Check: T2 call site | ✅ PASS |
| 4. Focus Check: T4 tests | ✅ PASS |
| 5. NT8 Compiler Pre-Check | ✅ PASS |
| 6. CYC Analysis | ✅ PASS |
| 7. Invariants | ✅ PASS |
| 8. Architecture Correctness | ✅ PASS |

---

## Open Questions (Not Plan Defects)

These are flagged by the architect in §10 and require engineer verification — they are not reviewer findings:

| # | Question | Engineer Action Required |
|---|----------|--------------------------|
| OQ-1 | Exact property name on `CopySignal` for entry order ID (plan uses `signal.OrderId`) | Confirm field name in `CopySignal` before implementing T2 diff |
| OQ-2 | `PttFollowerStrategy` namespace (`PropTraderTools` vs `PropTraderTools.Features`) | Use project convention; add `using` directive if needed |
| OQ-3 | `protected virtual CallAtmStrategyCreate` test hook approval | Reviewer notes: pattern is sound. Implement as planned. |
| OQ-4 | Test file naming/directory convention | Engineer to confirm alongside existing test files |

None of OQ-1 through OQ-4 are plan violations. They are correctly deferred to the engineer.

---

*Review complete. Verdict: **REVIEW_PASS**. Cycle 2 of 2 consumed. Ticket generation is now unlocked.*
