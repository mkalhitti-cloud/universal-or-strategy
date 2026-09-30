# B33 — Ticket Review: Modular Independence Architecture
# Reviewer: ptt-ticket-reviewer
# Tickets under review: docs/brain/B33-Modular/04-tickets.md
# Plan reference: docs/brain/B33-Modular/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/B33-Modular/02-plan-review.md (REVIEW_PASS)
# Date: 2026-07-25

---

## STEP 0 — Rules Catalog Gate

```
  [x] Read docs/standards/jane-street/RULES_CATALOG.md
      File: UTF-8 clean, 1560 lines, Version 1.0. Fully readable.
  [x] Read docs/standards/NT8_COMPILER_RULES.md
      File: UTF-8 clean, Version 1.6, B1-B33 confirmed.
  [x] Zero P0 violations confirmed in ticket descriptions before review proceeds.

GATE RESULT: PASS
```

---

## T1 — Core/PttContracts.cs (NEW FILE)

### Traceability
- B33-01 spec requirement cited in ticket header. ✅
- Maps to plan Section 2 (interfaces), Section 3 (PttBus), Section 4 (EventArgs). ✅
- No scope beyond plan.

**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Explicitly cited. No lock() in code. ✅
- JS-033 (async void): Explicitly cited. No async in file. ✅
- JS-001 (throw): Explicitly cited. No throw in code. ✅
- JS-002 (return null): Explicitly confirmed not applicable (void/event methods only). ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- All constructors: CYC = 1. ✅
- No methods with branches in T1. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-001 ({get; init;}): All EventArgs use `{get; private set;}` + constructor. ✅
- NT8-002 (records): All EventArgs are `class : EventArgs`. ✅
- NT8-043 (null-conditional): PttBus.Raise* uses local-copy null-check pattern (safe). ✅
- NT8-044 (using System): `using System;` present. ✅
- Namespace: `namespace PropTraderTools` — matches CopyEngine.cs. ✅

**NT8 Check: PASS**

### Test Coverage
- T1 has no standalone [Fact] tests. Ticket explicitly states: "No standalone tests for T1.
  Tested implicitly by T2–T6 module tests which all fire PttBus events." This is acceptable
  per the plan — PttContracts.cs is pure interface/event infrastructure exercised transitively. ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 present with exact PowerShell command. ✅
- SCAN-02 present. ✅
- SCAN-03 present. ✅
- SCAN-04 present (N/A noted — no CreateOrder; acceptable). ✅
- SCAN-05 present (N/A for T1; deferred to T8 full-tree — acceptable). ✅
- SCAN-06 present. ✅
- SCAN-07 present (N/A for T1 — no test code; acceptable). ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — Features/PttBreakEven.cs (NEW FILE)

### Traceability
- B33-02 spec requirements cited. DW-B36-01 fix explicitly noted. ✅
- Maps to plan Section 5a. ✅
- No scope beyond plan.

**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Explicitly cited. No lock(). ✅
- JS-033 (async void): Explicitly cited. No async. ✅
- JS-001 (throw): Explicitly cited. No throw in any method. ✅
- JS-002 (return null): `FindPosition()` returns null — call sites guard immediately with
  `if (pos == null || pos.Quantity == 0) return;`. Accepted NT8-050 idiom per checklist. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- `Execute()`: CYC = 4 stated. ✅
- `CancelStaleBracketsLocal()`: CYC = 4 stated. ✅
- `SubmitBeStopLocal()`: CYC = 3 stated. ✅
- `FindPosition()`: CYC = 2 stated. ✅
- All ≤ 8. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-006 (no .Any()): Explicit foreach + List accumulator. ✅
- NT8-007 (CreateOrder arg11): `(NinjaTrader.Cbi.CustomOrder)null` present. ✅
- NT8-013 (DateTime.MaxValue): Present at correct arg position. ✅
- NT8-014 (PTT- prefix): "PTT-BE-Stop" used. ✅
- NT8-031 (no PendingSubmit): Working + Initialized only. ✅
- NT8-044 (using System): Present. ✅
- NT8-049 (arg order): arg6=0 (limitPrice), arg7=bePrice (stopPrice). CRITICAL verified. ✅
- NT8-050 (no Positions[]): FindPosition uses foreach over acc.Positions. ✅
- NT8-051 (CancelStaleBracketsLocal before loop): Order in Execute() confirmed:
  CancelStaleBracketsLocal called BEFORE the foreach AllAccounts loop. ✅
- Namespace: `namespace PropTraderTools`. ✅

**NT8 Check: PASS**

### Test Coverage
- `T_B33_BE_Standalone` [Fact]: present with Assert.Equal(1, firedCount). ✅
- `T_B33_AllAccounts_BeLoop` [Fact]: present with Assert.Equal(accountCount, submitBeCallCount). ✅
- Both use `[Fact]` attribute (xUnit). ✅
- Both use `Assert.Equal` (xUnit assertions). ✅
- Both have `finally { PttBus.BeFired -= handler; }` cleanup. ✅
- 3-account test for AllAccounts loop confirmed. ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅
- SCAN-04 correctly points to SubmitBeStopLocal with manual arg verification instruction. ✅
- SCAN-07 checks BeFired +=/—= pairing in test file. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — Features/PttTrim.cs (NEW FILE)

### Traceability
- B33-03 spec requirements cited. ✅
- Maps to plan Section 5b. ✅

**Traceability: PASS**

### JS Pre-Check
- JS-021, JS-033, JS-001 all explicitly cited. No violations in code. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- `Execute()`: CYC = 3 stated. ✅
- `TrimPositionLocal()`: CYC = 2 stated. ✅
- `FindPositionLocal()`: CYC = 2 stated. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-007: `(NinjaTrader.Cbi.CustomOrder)null` present. ✅
- NT8-013: `DateTime.MaxValue` present. ✅
- NT8-014: "PTT-Trim" used. ✅
- NT8-049: arg6=0 (limitPrice), arg7=0 (stopPrice) for market order — correct. ✅
- NT8-050: FindPositionLocal via foreach. ✅
- NT8-044: `using System;` present (for Math.Max). ✅
- Namespace: `namespace PropTraderTools`. ✅

**NT8 Check: PASS**

### Test Coverage
- `T_B33_Trim_Standalone` [Fact]: present. ✅
- Uses Assert.Equal(1, firedCount). ✅
- `finally { PttBus.TrimFired -= handler; }` present. ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T4 — Features/PttFlatten.cs (NEW FILE)

### Traceability
- B33-04 spec requirements cited. ✅
- Maps to plan Section 5c. ✅

**Traceability: PASS**

### JS Pre-Check
- JS-021, JS-033, JS-001: same pattern as T3. No violations. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- `Execute()`: CYC = 2 stated. ✅
- `FlattenPositionLocal()`: CYC = 2 stated. ✅
- `FindPositionLocal()`: CYC = 2 stated. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-007: `(NinjaTrader.Cbi.CustomOrder)null` present. ✅
- NT8-013: `DateTime.MaxValue` present. ✅
- NT8-014: "PTT-Flatten" used. ✅
- NT8-049: arg6=0, arg7=0 (market order). ✅
- NT8-050: FindPositionLocal via foreach. ✅
- Namespace: `namespace PropTraderTools`. ✅

**NT8 Check: PASS**

### Test Coverage
- `T_B33_Flatten_Standalone` [Fact]: present. ✅
- Assert.Equal(1, firedCount). ✅
- `finally { PttBus.FlatFired -= handler; }` present. ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T5 — Features/PttCancel.cs (NEW FILE)

### Traceability
- B33-05 spec requirements cited. ✅
- Maps to plan Section 5d. ✅

**Traceability: PASS**

### JS Pre-Check
- JS-021, JS-033: explicitly cited. No violations. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- `Execute()`: CYC = 2 stated. ✅
- `CancelWorkingEntriesLocal()`: CYC = 3 stated. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-006 (no .Any()): explicit foreach + List accumulator. ✅
- NT8-031 (Working + Initialized only): confirmed. ✅
- NT8-044 (using System): present. ✅
- No CreateOrder in T5 — correct (cancel module does not submit orders). ✅
- Namespace: `namespace PropTraderTools`. ✅

**NT8 Check: PASS**

### Test Coverage
- `T_B33_Cancel_Standalone` [Fact]: present. ✅
- Assert.Equal(1, firedCount). ✅
- `finally { PttBus.CancelFired -= handler; }` present. ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T6 — Features/PttCopier.cs (NEW FILE)

### Traceability
- B33-06 spec requirements cited (all 4 subscribe/unsubscribe/relay items). ✅
- Maps to plan Section 5e. ✅

**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Explicitly cited. No lock(). ✅
- JS-033 (async void): Explicitly cited. No async. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- `Initialize()`: CYC = 1. ✅
- `Teardown()`: CYC = 1. ✅
- `OnBeFired`, `OnTrimFired`, `OnFlatFired`, `OnCancelFired`: CYC = 1 each
  (null/IsEnabled guard + single relay call). ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-043 (direct -=): Teardown uses direct `-=` for all 4 events. Explicitly cited. ✅
- Dependency rule: PttCopier holds `private readonly CopyEngine _engine` — ONLY Features
  file permitted to import CopyEngine. Explicitly documented. ✅
- Namespace: `namespace PropTraderTools`. ✅

**NT8 Check: PASS**

### Test Coverage — FAIL

**VIOLATION [T6-TEST-01]: `T_B33_Copier_BeFanOut` test is uncompilable as specified.**

The test constructs `new PttCopier(mockEngine)` where `mockEngine` is of type
`MockCopyEngineRelay`. However, `PttCopier`'s constructor signature is
`PttCopier(CopyEngine engine)` — it accepts a `CopyEngine`, not `MockCopyEngineRelay`.
`MockCopyEngineRelay` is not declared as inheriting from `CopyEngine` nor implementing any
shared interface. The ticket notes: _"Inherits CopyEngine only if CopyEngine is unsealed and
has a parameterless ctor; otherwise implement as a wrapper/spy pattern"_ — but this is
deferred resolution without a defined fallback.

This is not merely a code quality concern. The [Fact] test as written will not compile
unless one of the following is true (none of which are specified in any ticket):
1. `CopyEngine` is unsealed and `MockCopyEngineRelay` is declared `class MockCopyEngineRelay : CopyEngine`,
2. `PttCopier`'s constructor is changed to accept an `ICopyEngine` interface (no such
   interface exists and no ticket creates one), or
3. `MockCopyEngineRelay` somehow substitutes for `CopyEngine` (impossible without 1 or 2).

The engineer cannot implement this test as written without resolving an undescribed design
decision. Per the Test Coverage rule: _"Every new method described in the ticket must have a
[Fact] test specified"_ — the specified [Fact] is not buildable as written.

**Result: FAIL — T6-TEST-01: T_B33_Copier_BeFanOut test type mismatch: MockCopyEngineRelay
cannot be passed to PttCopier(CopyEngine engine). Resolution requires either (a) unsealed
CopyEngine with MockCopyEngineRelay : CopyEngine, or (b) an ICopyEngine interface added to
PttCopier constructor — neither is specified in any ticket.**

**Test Coverage: FAIL**

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_FAIL
**Reason: T6-TEST-01 — T_B33_Copier_BeFanOut [Fact] test uncompilable as specified.**

---

## T7 — TradeCopierPanel.cs (MODIFY EXISTING)

### Traceability
- B33-07 spec requirements cited. CRITICAL FSM preservation explicitly noted. ✅
- Maps to plan Section 6a–6f. ✅
- No scope beyond plan.

**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): Explicitly cited. No lock() in new additions. ✅
- JS-033, JS-001: No violations in described changes. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- Module dispatch loops (foreach + if): CYC = 2 each. ✅
- `AddModule()`: CYC = 1. ✅

**CYC Pre-Check: PASS**

### NT8 Check
- NT8-017 (_modules field volatile): Correctly noted as not needed — UI thread only. ✅
- NT8-021 (Account.All): Populated inside Attach/Initialize handler, not constructor. ✅
- NT8-042 (Dispatcher): Not needed — all on UI thread. ✅
- NT8-043 (null-conditional): T7 changes do not add event unsubscriptions (modules self-manage). ✅
- IPttHostContext implementation: `LeaderAccount`, `Instrument`, `AllAccounts` all specified. ✅
- AllAccounts populated before modules are registered. ✅
- License bool defaults set to `true`. ✅
- `SetEnabled()` wired after `AddModule()` calls, before `Initialize()`. ✅
- `m.Teardown()` called in Detach/Close handler. `_modules.Clear()` and `_allAccounts.Clear()` follow. ✅
- OnBeClick FSM: Armed path (DisarmPendingBe) UNCHANGED. ArmPendingBe path UNCHANGED.
  Only Idle-immediate path replaces `_engine.BreakEven()` with module dispatch. ✅
- No new [Fact] tests for T7: ticket explicitly states "verified by F5 compile + manual smoke test".
  This is acceptable — T7 is pure wiring, no new business logic. ✅

**NT8 Check: PASS**

### Test Coverage
- No new [Fact] tests for T7. Justification provided (wiring ticket, integration via T2–T6). ✅

**Test Coverage: PASS**

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅
- Build Verification block additionally checks ArmPendingBe and DisarmPendingBe are still
  referenced (1 match each), confirming FSM preservation. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_PASS

---

## T8 — CopyEngine.cs (MODIFY EXISTING — Dead Code Removal + Relay Methods)

### Traceability
- B33-08 (5 dead-code deletions) and B33-06 (4 relay methods) cited. ✅
- Maps to plan Sections 7a–7c and 12 (build tag). ✅

**Traceability: PASS**

### JS Pre-Check
- JS-021 (lock): No lock() in new relay methods. ✅
- JS-033, JS-001: No violations. ✅

**JS Pre-Check: PASS**

### CYC Pre-Check
- `RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel`: CYC = 2 each (null guard + foreach). ✅
- `GetFollowerAccounts()`: CYC = 1 (single return). ✅

**CYC Pre-Check: PASS**

### NT8 Check — FAIL

**VIOLATION [T8-NT8-01]: `GetFollowerAccounts()` stub uses `Enumerable.Empty<Account>()`.**

The stub body:
```csharp
return _followerAccounts ?? Enumerable.Empty<Account>();
```
requires `using System.Linq`. The T8 ticket does not list `using System.Linq` among its NT8
constraints, and the director prompt explicitly states: `T8 (CopyEngine.cs): NO new using
directives for Core/ or Features/ (same-namespace flat compilation)`. Adding `using System.Linq`
to CopyEngine.cs would constitute a new import, violating NT8-006 (avoid LINQ) and the
dependency rule stated in the director prompt.

The ticket has an engineer note acknowledging this is a stub, but the stub code itself will
be read and copied by the engineer. The note does not include a compliant alternative body.
The engineer is at risk of introducing `using System.Linq` to CopyEngine.cs. The ticket must
specify the non-LINQ fallback pattern explicitly (e.g., `return _followerAccounts;` with a
note that `_followerAccounts` must be resolved to the actual field name).

**Result: FAIL — T8-NT8-01: GetFollowerAccounts() stub body `Enumerable.Empty<Account>()`
requires System.Linq — violates NT8-006 and the CopyEngine no-new-imports rule. Ticket must
provide a LINQ-free fallback or remove the stub body entirely, leaving only an engineer
instruction.**

**NT8 Check: FAIL**

### Test Coverage
- No new [Fact] tests for T8 dead code removal. Justification: relay methods tested via
  `T_B33_Copier_BeFanOut` in T6. Pre-flight dead-code check specified. ✅
  (Note: T6's T_B33_Copier_BeFanOut test has its own FAIL — see T6 above. T8's relay
  methods remain untestable by that test as currently specified.)

**Test Coverage: PASS** (T8 itself does not claim new test coverage; the dependency test
failure is T6's issue, not T8's.)

### Scan Checklist
- SCAN-01 through SCAN-07 all present with exact PowerShell paths. ✅
- SCAN-05 (dead code verification) is a full-tree scan covering all .cs files in
  PropTraderTools — correct. ✅
- SCAN-07 correctly checks that CopyEngine.cs has zero PttBus references. ✅
- Pre-deletion grep commands (3 mandatory checks before any delete) specified. ✅

**Scan Checklist: PASS**

### File Routing
- Path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` — Wave workspace. ✅

**File Routing: PASS**

### VERDICT: TICKET_REVIEW_FAIL
**Reason: T8-NT8-01 — GetFollowerAccounts() stub uses Enumerable.Empty<Account>() which
requires System.Linq — violates NT8-006 and the CopyEngine no-new-imports constraint.**

---

## Aggregate Spec Coverage Check

| Spec ID  | Requirement                                        | Ticket | Status  |
|----------|----------------------------------------------------|--------|---------|
| B33-01   | Core/PttContracts.cs (interfaces + bus + args)     | T1     | ✅ PASS |
| B33-02   | PttBreakEven + DW-B36-01 AllAccounts loop          | T2     | ✅ PASS |
| B33-03   | PttTrim (partial close)                            | T3     | ✅ PASS |
| B33-04   | PttFlatten (full close)                            | T4     | ✅ PASS |
| B33-05   | PttCancel (cancel working entries)                 | T5     | ✅ PASS |
| B33-06   | PttCopier (PttBus fan-out) + CopyEngine relays     | T6+T8  | ⚠️ FAIL (T6 test type mismatch; T8 LINQ stub) |
| B33-07   | TradeCopierPanel IPttHostContext wiring            | T7     | ✅ PASS |
| B33-08   | CopyEngine dead code removal (5 symbols)           | T8     | ⚠️ FAIL (LINQ stub in relay method) |

No spec requirements are missing from tickets. No phantom work found.

---

## Violations Summary

| ID         | Ticket | Category        | Rule / Constraint                       | Severity |
|------------|--------|-----------------|-----------------------------------------|----------|
| T6-TEST-01 | T6     | Test Coverage   | T_B33_Copier_BeFanOut: MockCopyEngineRelay is not CopyEngine — test uncompilable as written | FAIL |
| T8-NT8-01  | T8     | NT8 Constraint  | GetFollowerAccounts() uses Enumerable.Empty<Account>() — requires System.Linq (NT8-006) and violates CopyEngine no-new-imports rule | FAIL |

---

## Architect Action Required

### Fix T6-TEST-01
**Option A (Preferred):** Add `ICopyEngine` interface to PttCopier constructor:
- Create `ICopyEngine` in `Core/PttContracts.cs` (T1) with the 4 relay method signatures.
- Change `PttCopier(CopyEngine engine)` to `PttCopier(ICopyEngine engine)`.
- `MockCopyEngineRelay` implements `ICopyEngine` — test compiles.
- CopyEngine.cs implements `ICopyEngine` — no behavior change.

**Option B:** Declare `MockCopyEngineRelay : CopyEngine` explicitly in the ticket, confirming
that CopyEngine is unsealed. Add a note that the engineer must verify CopyEngine is not sealed
before proceeding; if sealed, fall back to Option A.

### Fix T8-NT8-01
Replace the `GetFollowerAccounts()` stub body with a LINQ-free instruction:
```csharp
// Engineer: replace the body below with the actual follower collection field from CopyEngine.
// Check TrimOneAccount (L992) or FlattenOneAccount (L1040) for the iteration source.
// Example: return _followerAccounts;  // where _followerAccounts is IEnumerable<Account>
// DO NOT use Enumerable.Empty<Account>() or any System.Linq call.
private IEnumerable<Account> GetFollowerAccounts()
{
    // TODO: Engineer fills in actual field name after reading CopyEngine L992.
    throw new NotImplementedException("Engineer: replace with actual follower field");
}
```
Or simplify the relay methods to inline the iteration directly (no GetFollowerAccounts()
helper), letting the engineer fill in the collection name once:
```csharp
public void RelayBe(BeEventArgs e)
{
    if (e == null) return;
    // Engineer: replace _followers with the actual follower account collection field name.
    // See TrimOneAccount (L992) for the correct field/iteration pattern.
    foreach (Account follower in _followers)
        SubmitBeStop(follower, e.Instrument, e.BePrice);
}
```

---

## Overall

| Check                  | T1   | T2   | T3   | T4   | T5   | T6       | T7   | T8       |
|------------------------|------|------|------|------|------|----------|------|----------|
| Traceability           | PASS | PASS | PASS | PASS | PASS | PASS     | PASS | PASS     |
| JS Pre-Check           | PASS | PASS | PASS | PASS | PASS | PASS     | PASS | PASS     |
| CYC Pre-Check          | PASS | PASS | PASS | PASS | PASS | PASS     | PASS | PASS     |
| NT8 Check              | PASS | PASS | PASS | PASS | PASS | PASS     | PASS | **FAIL** |
| Test Coverage          | PASS | PASS | PASS | PASS | PASS | **FAIL** | PASS | PASS     |
| Scan Checklist (7/7)   | PASS | PASS | PASS | PASS | PASS | PASS     | PASS | PASS     |
| File Routing           | PASS | PASS | PASS | PASS | PASS | PASS     | PASS | PASS     |
| **Ticket Verdict**     | PASS | PASS | PASS | PASS | PASS | **FAIL** | PASS | **FAIL** |

---

## TICKET_REVIEW_FAIL

**2 violations prevent engineer start:**

1. **T6-TEST-01** — `T_B33_Copier_BeFanOut` [Fact] test uncompilable: `MockCopyEngineRelay`
   is not assignable to `CopyEngine`. PttCopier constructor requires `CopyEngine engine`;
   no ICopyEngine interface exists. Test cannot be built as written.

2. **T8-NT8-01** — `GetFollowerAccounts()` stub uses `Enumerable.Empty<Account>()` which
   requires `using System.Linq`. This violates NT8-006 and the CopyEngine no-new-imports
   constraint (director prompt, checklist item 7). CopyEngine.cs must not gain new using
   directives for this block.

**Send back to architect for targeted fixes to T6 and T8. T1–T5 and T7 are clean.**

---

*ptt-ticket-reviewer | B33-Modular | Phase 3.5 — Ticket Review*
*Return: TICKET_REVIEW_FAIL*


---

## SECOND CYCLE REVIEW

**Reviewer**: ptt-ticket-reviewer
**Tickets version under review**: 04-tickets.md v1.1
**First-cycle failures being re-checked**: T6-TEST-01, T8-NT8-01
**Unmodified tickets (cycle-1 PASS status confirmed unchanged)**: T2, T3, T4, T5, T7

---

### STEP 0 — Rules Catalog Gate (Second Cycle)

```
  [x] RULES_CATALOG.md: Get-Content first line = "# Jane Street Rules Catalog"
      UTF-8 readable — not garbled, not UTF-16. CLEAN.
  [x] NT8_COMPILER_RULES.md: confirmed available and UTF-8 clean (cycle 1 verified).
  [x] Zero P0 violations in ticket descriptions confirmed before proceeding.

GATE RESULT: PASS
```

---

### T1 — Core/PttContracts.cs (CHANGED: ICopyEngine interface added)

#### ICopyEngine Interface Checklist

- ICopyEngine has exactly 4 methods: `RelayBe(BeEventArgs e)`, `RelayTrim(TrimEventArgs e)`,
  `RelayFlatten(FlatEventArgs e)`, `RelayCancel(CancelEventArgs e)`. ✅
- All parameter types (`BeEventArgs`, `TrimEventArgs`, `FlatEventArgs`, `CancelEventArgs`)
  are defined in the same file (`PttContracts.cs`). ✅ No circular dependency.
- `namespace PropTraderTools` — matches CopyEngine.cs flat namespace. ✅
- No new `using` directives needed for ICopyEngine — void methods, same-namespace types only. ✅
- `ICopyEngine` accessible from `PttCopier.cs` (same namespace `PropTraderTools`, flat compile). ✅
- Interface members: void return, no LINQ, no async, no init accessors. ✅ (NT8 clean)

#### Full Checklist Carry-Forward

All checks from cycle 1 (Traceability, JS Pre-Check, CYC, NT8, Test Coverage, Scan 01-07,
File Routing) were **PASS** in cycle 1. The only change in v1.1 is the addition of ICopyEngine,
which passes all applicable checks above.

**B33-01 spec coverage**: ICopyEngine is now listed under Spec Requirements Satisfied. ✅

**Traceability**: PASS
**JS Pre-Check**: PASS
**CYC Pre-Check**: PASS (interface has no implementation — CYC = 0)
**NT8 Check**: PASS
**Test Coverage**: PASS (no standalone tests for T1 — implicitly tested by T6)
**Scan Checklist**: PASS (SCAN-01 through SCAN-07 all present — no change from cycle 1)
**File Routing**: PASS

### T1 VERDICT: TICKET_REVIEW_PASS

---

### T6 — Features/PttCopier.cs (CHANGED: T6-TEST-01 fix — ICopyEngine injection)

#### T6-TEST-01 Fix Checklist

- Constructor signature: `public PttCopier(ICopyEngine engine)` — NOT `CopyEngine engine`. ✅
- `_engine` field: `private readonly ICopyEngine _engine`. ✅
- `MockCopyEngineRelay : ICopyEngine` — explicitly implements the interface from T1. ✅
  All 4 relay method stubs (`RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel`) present. ✅
- Test `T_B33_Copier_BeFanOut` constructs `new PttCopier(mockEngine)` where
  `mockEngine` is `MockCopyEngineRelay : ICopyEngine` — compiles without subclassing
  the concrete `CopyEngine` class. ✅ **T6-TEST-01 is resolved.**
- `MockCopyEngineRelay` uses no LINQ, no async, no init accessors, plain class
  implementing interface. ✅ NT8 clean.
- `finally { copier.Teardown(); }` — Teardown uses direct `-=` for all 4 events (NT8-043). ✅

#### All 7-Scan Items Present

- SCAN-01: `lock\s*\(` on `PttCopier.cs` — present. ✅
- SCAN-02: `async\s+void` on `PttCopier.cs` — present. ✅
- SCAN-03: `{get; init;}` on `PttCopier.cs` — present. ✅
- SCAN-04: `.CreateOrder` on `PttCopier.cs` — present (expected zero). ✅
- SCAN-05: dead code names on `PttCopier.cs` — present. ✅
- SCAN-06: `.Positions[` on `PttCopier.cs` — present. ✅
- SCAN-07: `PttBus\.(BeFired|TrimFired|FlatFired|CancelFired)\s*\+=` unsubscribe check — present. ✅

#### PttBus Cleanup in Test

- `finally { copier.Teardown(); }` wraps the test body. `Teardown()` unsubscribes all 4
  PttBus events. ✅ No static event leak.

**Traceability**: PASS (B33-06 spec satisfied; T6-TEST-01 fix documented)
**JS Pre-Check**: PASS (JS-021, JS-033 explicitly cited; no violations in code)
**CYC Pre-Check**: PASS (Initialize CYC=1, Teardown CYC=1, all 4 handlers CYC=1)
**NT8 Check**: PASS (NT8-043 direct -= confirmed; no async void, no lock, no init)
**Test Coverage**: PASS (`T_B33_Copier_BeFanOut` [Fact] now compilable with ICopyEngine injection)
**Scan Checklist**: PASS (all 7 scans present)
**File Routing**: PASS (Wave workspace `src\PropTraderTools\Features\PttCopier.cs`)

### T6 VERDICT: TICKET_REVIEW_PASS

---

### T8 — CopyEngine.cs (CHANGED: T8-NT8-01 fix — relay methods use AllAccounts; class adds : ICopyEngine)

#### CopyEngine Source Verification (Mandatory)

Reviewer ran:
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "private IEnumerable.*AllAccounts|private.*AllAccounts"
# RESULT: LineNumber 1321 — private IEnumerable<Account> AllAccounts(Instrument instrument)
```
**`AllAccounts(Instrument)` EXISTS at L1321.** ✅

Private helper methods cited by relay bodies also verified to exist:
- `SubmitBeStop(Account, Instrument, double)` — L1575 (`private void SubmitBeStop(...)`) ✅
- `TrimOneAccount(Account, Instrument)` — L992 (`private void TrimOneAccount(...)`) ✅
- `FlattenOneAccount(Account, Instrument)` — L1040 (`private void FlattenOneAccount(...)`) ✅
- `CancelOneAccount(Account, Instrument)` — L1120 (`private void CancelOneAccount(...)`) ✅

All private helpers are in the same class → accessible by public relay methods added to the same class. ✅

#### T8-NT8-01 Fix Checklist

- `RelayBe` body: `foreach (var acc in AllAccounts(e.Instrument)) SubmitBeStop(acc, e.Instrument, e.BePrice)` — NO `Enumerable.Empty`, NO LINQ, NO `System.Linq`. ✅
- `RelayTrim` body: `foreach (var acc in AllAccounts(e.Instrument)) TrimOneAccount(acc, e.Instrument)` — NO LINQ. ✅
- `RelayFlatten` body: `foreach (var acc in AllAccounts(e.Instrument)) FlattenOneAccount(acc, e.Instrument)` — NO LINQ. ✅
- `RelayCancel` body: `foreach (var acc in AllAccounts(e.Instrument)) CancelOneAccount(acc, e.Instrument)` — NO LINQ. ✅
- **No new `using` directives** added — all methods (`AllAccounts`, `SubmitBeStop`, `TrimOneAccount`,
  `FlattenOneAccount`, `CancelOneAccount`) are private members of the same class. ✅ **T8-NT8-01 is resolved.**

#### Class Declaration Check

- `public class CopyEngine : ICopyEngine` — explicitly specified in T8. ✅
- `ICopyEngine` is declared in `Core/PttContracts.cs` (T1), same `namespace PropTraderTools` — no `using` needed. ✅

#### Remaining T8 Items (unchanged from cycle 1, re-confirmed)

- 5 dead symbol deletion specs present with pre-grep checks. ✅
- Build tag change spec present (`"PTT-COPIER B33 | modular-independence | 2026-07-{DATE}"`). ✅
- Pre-deletion mandatory grep commands (3 checks) all specified. ✅
- No new `[Fact]` tests for T8 — relay methods tested via `T_B33_Copier_BeFanOut` (T6). ✅

#### All 7-Scan Items Present

- SCAN-01: `lock\s*\(` on `CopyEngine.cs` — present with note about pre-existing vs new. ✅
- SCAN-02: `async\s+void` on `CopyEngine.cs` — present. ✅
- SCAN-03: `{get; init;}` on `CopyEngine.cs` — present. ✅
- SCAN-04: `.CreateOrder` on `CopyEngine.cs` — present (relay methods delegate to existing helpers, not CreateOrder directly). ✅
- SCAN-05: dead code zero-match across all `*.cs` in PropTraderTools — present (full-tree scan). ✅
- SCAN-06: `.Positions[` on `CopyEngine.cs` — present. ✅
- SCAN-07: no `PttBus.` references in `CopyEngine.cs` — present. ✅

**Traceability**: PASS (B33-08 dead deletions, B33-06 relay methods, T6-TEST-01 class declaration fix — all cited)
**JS Pre-Check**: PASS (no lock(), no async void in relay methods)
**CYC Pre-Check**: PASS (all 4 relay methods CYC = 2: null guard + foreach)
**NT8 Check**: PASS (no Enumerable.Empty, no System.Linq, no new using directives — T8-NT8-01 resolved)
**Test Coverage**: PASS (relay methods covered by T6's T_B33_Copier_BeFanOut; T8 itself claims no new [Fact])
**Scan Checklist**: PASS (all 7 scans present)
**File Routing**: PASS (Wave workspace `src\PropTraderTools\CopyEngine.cs`)

### T8 VERDICT: TICKET_REVIEW_PASS

---

### Unmodified Tickets — Cycle-1 PASS Status Confirmed

T2, T3, T4, T5, T7 were **TICKET_REVIEW_PASS** in cycle 1. The v1.1 diff adds only T1
ICopyEngine, T6 constructor change + MockCopyEngineRelay, and T8 relay method bodies +
class declaration change. No modification to T2, T3, T4, T5, or T7 content. Their cycle-1
verdicts carry forward unchanged.

| Ticket | Cycle 1 | Cycle 2 |
|--------|---------|---------|
| T2 | PASS | **CARRY-FORWARD PASS** |
| T3 | PASS | **CARRY-FORWARD PASS** |
| T4 | PASS | **CARRY-FORWARD PASS** |
| T5 | PASS | **CARRY-FORWARD PASS** |
| T7 | PASS | **CARRY-FORWARD PASS** |

---

### Second Cycle Aggregate Violations

| ID         | Ticket | Status  | Note                                        |
|------------|--------|---------|---------------------------------------------|
| T6-TEST-01 | T6     | CLOSED  | ICopyEngine interface added; MockCopyEngineRelay implements ICopyEngine; PttCopier(ICopyEngine) compiles |
| T8-NT8-01  | T8     | CLOSED  | Relay methods use AllAccounts(Instrument) at L1321; private helpers verified to exist; no Enumerable.Empty, no System.Linq |

No new violations found in second cycle.

---

### Second Cycle Overall Verdict

| Check                  | T1   | T2   | T3   | T4   | T5   | T6   | T7   | T8   |
|------------------------|------|------|------|------|------|------|------|------|
| Traceability           | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| JS Pre-Check           | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| CYC Pre-Check          | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| NT8 Check              | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| Test Coverage          | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| Scan Checklist (7/7)   | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| File Routing           | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| **Ticket Verdict**     | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

## TICKET_REVIEW_PASS

**All 8 tickets clean. Both cycle-1 violations resolved. Engineer may proceed.**

*ptt-ticket-reviewer | B33-Modular | Phase 3.5 — Second Cycle Ticket Review*
*Return: TICKET_REVIEW_PASS*
