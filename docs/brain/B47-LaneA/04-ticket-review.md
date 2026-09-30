# B47-LaneA — Ticket Review
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-08
**Ticket file reviewed**: `docs/brain/B47-LaneA/04-tickets.md`
**Plan basis**: `docs/brain/B47-LaneA/02-architecture-plan.md` (REVIEW_PASS Cycle 2)
**Plan review**: `docs/brain/B47-LaneA/02-plan-review.md`
**Spec anchor**: `specs/002-trade-copier-spec.html#dw-b47-be-follower-scope`
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md`
**NT8 rules**: `docs/standards/NT8_COMPILER_RULES.md`

---

## Ticket Review: B47-LaneA

### T1 — DW-B47-BE-FOLLOWER-SCOPE: Add IsFollowerAccount guard to BE/QX all-accounts paths

---

#### 1. TRACEABILITY

**Status**: PASS

| Check | Result |
|-------|--------|
| Spec requirement `specs/002-trade-copier-spec.html#dw-b47-be-follower-scope` present in ticket header? | ✓ Present |
| Defect ID `DW-B47-BE-FOLLOWER-SCOPE` cited? | ✓ Present |
| Live-session root cause (17 `CancelStaleBrackets` calls, Sim102 bracket loss) cited? | ✓ Present |
| Change 1a (`IsFollowerAccount`) traces to plan §3 / architecture item D1? | ✓ Traces |
| Change 1b (`ArmAllPendingBe` guard) traces to plan §4a? | ✓ Traces |
| Changes 2a–2d (`PttBreakEven` extraction + guard) trace to plan §4c? | ✓ Traces |
| Change 3a (`PttGlobalQuickExit.Execute` guard) traces to plan §4b? | ✓ Traces |

No phantom work (items in ticket not in plan). No missing work (all plan §4 items represented).

---

#### 2. 7-SCAN CHECKLIST

**Status**: PASS

All 7 scans are present in the ticket with PowerShell commands and PASS criteria.

| Scan | Description | Command Present | PASS Criteria Stated | Fail Action Stated |
|------|-------------|-----------------|----------------------|--------------------|
| SCAN-01 | No `lock(` | `Select-String -Pattern "lock\("` against 3 modified files | ✓ Zero results | ✓ Remove lock; use `ConcurrentBag` or `Interlocked` |
| SCAN-02 | No `async void` | `Select-String -Pattern "async void"` against 3 files | ✓ Zero results | ✓ Convert to `async Task` or synchronous |
| SCAN-03 | No `return null` in new non-nullable methods | `Select-String -Pattern "return null"` with explicit scope to new methods only | ✓ Nuanced: pre-existing `FindPositionLocal` occurrences allowed; new method occurrences = FAIL | ✓ Return `false` or default value |
| SCAN-04 | No `throw new` in hot paths | `Select-String -Pattern "throw new"` against 3 files | ✓ Zero results in new/modified bodies | ✓ Log with `Output.Process` and return |
| SCAN-05 | PTT- signal prefix / no new `CreateOrder` | `Select-String -Pattern "CreateOrder"` against 3 files | ✓ Count matches pre-B47 baseline; all quoted names start with "PTT-" | ✓ Rename non-prefixed signals |
| SCAN-06 | CYC ≤ 8 | `python scripts/complexity_audit.py` with named method list | ✓ 7 methods listed explicitly | ✓ Extract sub-methods; consult plan §4c |
| SCAN-07 | NT8 banned patterns | `Select-String -Pattern "init;\|volatile double\|ImmutableDictionary\|abstract record\|sealed record"` against 3 files | ✓ Zero results in lines added by ticket | ✓ Replace per NT8_COMPILER_RULES.md |

---

#### 3. METHOD SIGNATURES

**Status**: PASS

| Method | Signature | Visibility | File | Present in Ticket |
|--------|-----------|-----------|------|-------------------|
| `IsFollowerAccount` | `internal bool IsFollowerAccount(Account a)` | `internal` | `CopyEngine.cs` | ✓ |
| `ArmAllPendingBe` | No signature change — guard insertion documented at line 2113 | N/A | `CopyEngine.cs` | ✓ |
| `Execute` (BE) | `public void Execute(IPttHostContext ctx)` | `public` | `PttBreakEven.cs` | ✓ |
| `ExecuteOneAccount` | `private void ExecuteOneAccount(Account acc, IPttHostContext ctx, double buf, double tickSize, int seq)` | `private` | `PttBreakEven.cs` | ✓ |
| `BuildBeRejectMsg` | `private static string BuildBeRejectMsg(string accName, double bePrice, bool isLong, double ask, double bid)` | `private static` | `PttBreakEven.cs` | ✓ |
| `RaiseBeNotify` | `private void RaiseBeNotify(IPttHostContext ctx, Position leaderPos, double buf)` | `private` | `PttBreakEven.cs` | ✓ |
| `Execute` (QX) | `internal void Execute()` | `internal` | `PttGlobalQuickExit.cs` | ✓ (change site documented at line 25) |

Return types, parameter names, and parameter types all specified for every new method.

---

#### 4. CYC PRE-CHECK

**Status**: PASS

| Method | CYC Before | CYC After | Limit | Status |
|--------|-----------|-----------|-------|--------|
| `CopyEngine.IsFollowerAccount` (new) | N/A | 4 | ≤ 8 | ✓ |
| `CopyEngine.ArmAllPendingBe` | 5 | 6 | ≤ 8 | ✓ |
| `PttGlobalQuickExit.Execute` | 3–5 | 5–7 (strict worst-case) | ≤ 8 | ✓ |
| `PttBreakEven.Execute` | 14 (corrected) | 7 | ≤ 8 | ✓ |
| `PttBreakEven.ExecuteOneAccount` (new) | N/A | 7 | ≤ 8 | ✓ |
| `PttBreakEven.BuildBeRejectMsg` (new) | N/A | 3 | ≤ 8 | ✓ |
| `PttBreakEven.RaiseBeNotify` (new) | N/A | 2 | ≤ 8 | ✓ |
| `PttGlobalBreakEven.Execute(int)` | 1 | 1 (NO CHANGE) | ≤ 8 | ✓ |

Extraction plan is internally consistent. The three-helper strategy (`ExecuteOneAccount` +
`BuildBeRejectMsg` + `RaiseBeNotify`) correctly resolves the CYC=14 → CYC=7 reduction,
consistent with plan §4c and verified in plan review Cycle 2. CYC accounting in ticket body
matches plan review counts for all 7 affected methods.

---

#### 5. NT8 CONSTRAINTS

**Status**: PASS

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 no `init` setters | `IsFollowerAccount` is a method; no `init` setters on any new field or property | ✓ PASS |
| NT8-003 no `volatile double` | No new `volatile double` fields introduced | ✓ PASS |
| NT8-006 no LINQ in NT8 context | `IsFollowerAccount` uses `foreach` + `Array.IndexOf` — explicitly stated; no `.Any()`, `.Contains()` | ✓ PASS |
| NT8-013 no `DateTime.Now` | No new `DateTime.Now` usage; no new `CreateOrder` calls | ✓ PASS (N/A) |
| NT8-014 PTT- prefix | No new `CreateOrder` calls; existing signal names unchanged | ✓ PASS (N/A) |
| NT8-021 `Account.All` not in constructor | `ArmAllPendingBe` and `PttGlobalQuickExit.Execute` called from UI button handlers post-init only | ✓ PASS |

---

#### 6. COMPLETENESS — All 3 files described

**Status**: PASS

| File | Changes Described | Complete |
|------|------------------|----------|
| `src/PropTraderTools/CopyEngine.cs` | Change 1a (new `IsFollowerAccount` at line 1389) + Change 1b (`ArmAllPendingBe` guard at line 2113) | ✓ |
| `src/PropTraderTools/Features/PttBreakEven.cs` | Changes 2a–2d: extract `ExecuteOneAccount`, `BuildBeRejectMsg`, `RaiseBeNotify`; rewrite `Execute()` with guard | ✓ |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Change 3a: follower guard in `Execute()` outer loop | ✓ |

All three files from the plan §6 "Modified" table are accounted for. No additional files.

---

#### 7. TEST COVERAGE

**Status**: PASS

| Test Name | Method(s) Covered | Assertion Described |
|-----------|------------------|---------------------|
| `T_B47_01_IsFollowerAccount_ReturnsTrueForFollower` | `CopyEngine.IsFollowerAccount` | After `AddRule(leader, [follower1, follower2])`, `IsFollowerAccount(follower1)` = `true` | ✓ |
| `T_B47_02_IsFollowerAccount_ReturnsFalseForLeader` | `CopyEngine.IsFollowerAccount` | `IsFollowerAccount(leader)` = `false` | ✓ |
| `T_B47_03_ArmAllPendingBe_SkipsFollowerAccounts` | `CopyEngine.ArmAllPendingBe` | Guard presence verified; follower account does not enter inner position loop | ✓ |
| `T_B47_04_PttBreakEven_Execute_SkipsFollowerAccount` | `PttBreakEven.Execute`, `ExecuteOneAccount` | With follower in `ctx.AllAccounts`, `ExecuteOneAccount` NOT called for that account | ✓ |

All new `internal`/`public` methods have at least one `[Fact]` test specified. `BuildBeRejectMsg`
and `RaiseBeNotify` are private helpers covered indirectly through `T_B47_04`. Direct tests for
private methods are not required; coverage through the public-facing `Execute` call path is sufficient.

**Observation (non-blocking, WARN)**: Plan §10 listed a fifth test case (`T_B47_01_IsFollowerAccount_ReturnsFalse_WhenNoRules` — empty `_rules` scenario). The ticket consolidates into 4 tests and omits the empty-rules case. This is a test coverage regression from the plan spec. It does not prevent the defect fix from being verified by T_B47_01/02, but Lane C should consider adding it. Flagged for architect awareness; does not block this ticket.

---

#### 8. ACCEPTANCE CRITERIA

**Status**: PASS

| Criterion | Present | Verifiable |
|-----------|---------|-----------|
| D1 — `IsFollowerAccount` exists; true/false behaviour correct | ✓ | T_B47_01, T_B47_02 |
| D2 — `ArmAllPendingBe` guard present before inner Position loop | ✓ | T_B47_03; SCAN-01 |
| D3 — `PttBreakEven.Execute` guard present before `ExecuteOneAccount` | ✓ | T_B47_04; SCAN-01 |
| D4 — `PttGlobalQuickExit.Execute` guard present before inner Position loop | ✓ | SCAN-01; code review |
| D5 — All modified methods CYC ≤ 8 | ✓ | SCAN-06 |
| D6 — No P0 violations: zero `lock(`, `async void`, new `return null`, `throw new` | ✓ | SCAN-01 through SCAN-04 |
| D7 — `PttGlobalBreakEven.cs` unchanged | ✓ | Diff verify |
| D8 — `PttQuickExit.cs` unchanged | ✓ | Diff verify |

All 8 acceptance criteria present and each paired with a verifiable scan or test reference.

---

#### 9. NO SCOPE CREEP

**Status**: PASS

| Check | Result |
|-------|--------|
| Only 3 files in scope (`CopyEngine.cs`, `PttBreakEven.cs`, `PttGlobalQuickExit.cs`)? | ✓ |
| `TradeCopierPanel.cs` not mentioned in change sites? | ✓ Not present |
| `PttFollowerStrategy.cs` not mentioned in change sites? | ✓ Not present |
| `CopyEngine.cs:779` orphan guard explicitly confirmed no-change in plan §2 D7? | ✓ (plan-level, not ticket-level — acceptable; plan review confirmed it) |
| Build tag update references `CopyEngine.cs` only? | ✓ |

---

#### 10. LINE NUMBERS

**Status**: PASS

| Change | Line Number(s) Specified | Matches Plan |
|--------|-------------------------|--------------|
| Change 1a: `IsFollowerAccount` insertion | After line 1388 (FindRule closing brace); new method at 1389 | ✓ Plan §3 |
| Change 1b: `ArmAllPendingBe` guard | `foreach` at line 2112; guard inserted at line 2113 | ✓ Plan §4a |
| Change 2d: `PttBreakEven.Execute()` | Method at line 66; body lines 66–124 approximately | ✓ Plan §4c |
| Change 3a: `PttGlobalQuickExit.Execute()` | Method at line 25; `foreach` body at line 27 | ✓ Plan §4b |

All change sites carry specific line numbers verified against actual source in plan review Cycle 2.

---

#### 11. NO-CHANGE FILES

**Status**: PASS

| File | No-Change Confirmed | Rationale Present |
|------|--------------------|--------------------|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | ✓ | CYC=1; delegates unconditionally to `ArmAllPendingBe`; guard there covers the production path |
| `src/PropTraderTools/Features/PttQuickExit.cs` | ✓ | Operates on a single leader account passed as argument; no fan-out to followers |

Both files explicitly listed in "Files Confirmed Unchanged" table with rationale. Rationale is accurate and verified against plan §2 D6/D7.

---

#### 12. JS P0 RULES — Explicit Compliance Statements

**Status**: PASS

| Rule | Explicit Statement in Ticket | Compliance |
|------|------------------------------|-----------|
| JS-021 no `lock()` | "no `lock` introduced" — Jane Street Rule Constraints table row 1; SCAN-01 with zero-result expectation | ✓ PASS |
| JS-001 no `throw` in hot paths | "Returns `bool`/`void`; no throws anywhere in this fix" — Rule Constraints table row 2; SCAN-04 with zero-result expectation | ✓ PASS |
| JS-002 no `return null` | "`IsFollowerAccount` returns `bool`; `BuildBeRejectMsg` returns `string` (never null — string concat always non-null)" — Rule Constraints table row 3 | ✓ PASS |
| JS-033 no `async void` | "All new/modified methods are synchronous" — Rule Constraints table row 4; SCAN-02 | ✓ PASS |

All four P0 rules have named citations in the ticket body and matching scans.

---

### FILE ROUTING

**Status**: PASS

All `.cs` source paths use `src/PropTraderTools/` prefix (Wave workspace). No Director-workspace paths (`c:\WSGTA\universal-or-strategy-director\src\`) are referenced for `.cs` files.

---

### VERDICT: TICKET_REVIEW_PASS

| Check | Result |
|-------|--------|
| 1. TRACEABILITY | ✓ PASS |
| 2. 7-SCAN CHECKLIST (all 7 present with commands + PASS criteria) | ✓ PASS |
| 3. METHOD SIGNATURES (all 7 methods with exact signatures) | ✓ PASS |
| 4. CYC PRE-CHECK (all ≤ 8, extraction plan consistent) | ✓ PASS |
| 5. NT8 CONSTRAINTS (init, volatile double, LINQ, DateTime.Now, PTT-prefix, Account.All) | ✓ PASS |
| 6. COMPLETENESS (all 3 files described) | ✓ PASS |
| 7. TEST COVERAGE (4 xUnit test names with assertions) | ✓ PASS (WARN: T_B47_empty_rules case dropped from plan §10 — non-blocking) |
| 8. ACCEPTANCE CRITERIA (D1–D8 present and verifiable) | ✓ PASS |
| 9. NO SCOPE CREEP (only 3 files; no TradeCopierPanel.cs, PttFollowerStrategy.cs) | ✓ PASS |
| 10. LINE NUMBERS (exact, matches source-verified plan) | ✓ PASS |
| 11. NO-CHANGE FILES (PttGlobalBreakEven.cs + PttQuickExit.cs confirmed with rationale) | ✓ PASS |
| 12. JS P0 RULES (JS-021, JS-001, JS-002, JS-033 explicitly stated per ticket) | ✓ PASS |
| FILE ROUTING (Wave workspace paths for all .cs files) | ✓ PASS |

**Violations found**: 0
**Warnings (non-blocking)**: 1 (empty-rules test case from plan §10 not carried into ticket; Lane C should add as T_B47_05)

---

## Overall: TICKET_REVIEW_PASS

The ticket satisfies every gate. The engineer may proceed.

---

*Reviewed by: ptt-ticket-reviewer (Phase 3.5, 2026-08-08)*
*Plan basis: `docs/brain/B47-LaneA/02-architecture-plan.md` REVIEW_PASS (Cycle 2)*
*Ticket file: `docs/brain/B47-LaneA/04-tickets.md`*
*Next phase: ptt-engineer (Phase 4a) — implement from TICKET_REVIEW_PASS tickets only*
