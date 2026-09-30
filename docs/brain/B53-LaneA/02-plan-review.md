# B53-LaneA Plan Review
# Reviewer: ptt-plan-reviewer (Phase 2)
# Input: docs/brain/B53-LaneA/02-architecture-plan.md
# Rules: docs/standards/jane-street/RULES_CATALOG.md + docs/standards/NT8_COMPILER_RULES.md
# Date: 2026-08-09

---

## Verdict: REVIEW_PASS

No violations found. All 24 checklist items pass. Plan is approved for Phase 3 (ticket generation).

---

## Violations

None.

---

## Confirmed Items

### Correctness Checks

| # | Item | Result |
|---|------|--------|
| C-01 | Root cause accurately described as managed framework slot conflict (IsUnmanaged=False, EntriesPerDirection=1) — not an AddOn-ownership bug | PASS — §2 correctly identifies managed framework entry slot ownership by PttFollowerStrategy as root cause |
| C-02 | Fix removes PttBus.RaiseFillSignal publish from SendCopy() — not just moves it | PASS — §4B: "Delete lines 867–873 inclusive"; the entire RaiseFillSignal block is removed with no replacement |
| C-03 | TryAttachAtmToFollower is called from OnOrderUpdate on Filled+PTT-Copy (not from SendCopy) | PASS — §4A: new early-exit branch in OnOrderUpdate fires on `OrderState.Filled && order.Name == "PTT-Copy"` and calls TryAttachAtmToFollower |
| C-04 | AtmStrategyCreate called with string.Empty as entryOrderId (attaches to open position, not to entry order) | PASS — §4C call shows string.Empty as the entryOrderId argument (7th positional arg in the static overload) |
| C-05 | Inherit mode (empty template) → AtmStrategyCreate is SKIPPED (no error, clean skip) | PASS — §4C: `if (!(mode is FollowerAtmMode.Named named)) return;` silently returns for Inherit and Market modes |
| C-06 | PttFollowerStrategy.cs is GATED not deleted (NT8 import safety preserved) | PASS — §4E: `#if PTT_FOLLOWER_ACTIVE` wraps entire class body; file retained; NT8 import safety explicitly documented |

### Rules Catalog Checks

| # | Item | Rule | Result |
|---|------|------|--------|
| R-01 | No lock() in new methods | JS-021 | PASS — §4C and §4D both explicitly note "no lock()"; no lock() in any new method body |
| R-02 | try/catch around AtmStrategyCreate, no rethrow | JS-001 | PASS — §4C: try wraps static call; catch block logs via StatusUpdate and returns; no throw statement |
| R-03 | No return null for reference types — guard clauses used | JS-002 | PASS — TryAttachAtmToFollower is void (no return value). FindRuleByFollower returns CopyRule? (Nullable<CopyRule> value type), which is the correct Option<T> equivalent for value types in this codebase. Pattern mirrors existing FindRule(Instrument) at line 1418. §4D explicitly defends this interpretation. |
| R-04 | No async void | JS-033 | PASS — All new methods are private void (not async). OnOrderUpdate is protected override void (NT8-019 compliant). |
| R-05 | No init accessors in new types | NT8-001 | PASS — No new types defined. No init setters in any new code shown. |
| R-06 | No volatile double in new fields | NT8-003 | PASS — No new fields introduced by this plan. |

### CYC Checks

| # | Method | Planned CYC | Result |
|---|--------|-------------|--------|
| CYC-01 | OnOrderUpdate | 8 (at limit: before ~6 + 2 new branches) | PASS — §4A: "CYC = 8. At limit. PASSES." |
| CYC-02 | TryAttachAtmToFollower | 4 | PASS — §4C: 4 branches enumerated (Inherit guard, empty template guard, try block, catch block) |
| CYC-03 | FindRuleByFollower | 3 | PASS — §4D: 3 branches enumerated (null guard, outer foreach+instrument filter, inner foreach+account match) |
| CYC-04 | SendCopy | 3 (unchanged) | PASS — §4B: CYC before = 3, CYC after = 3; RaiseFillSignal removal does not affect branch count |

### Completeness Checks

| # | Item | Result |
|---|------|--------|
| CP-01 | All 5 change units (T1–T5) mapped in plan | PASS — §4A (OnOrderUpdate branch), §4B (SendCopy cleanup), §4C (TryAttachAtmToFollower), §4D (FindRuleByFollower), §4E (PttFollowerStrategy gate) = 5 distinct change units |
| CP-02 | xUnit [Fact] test names specified | PASS — §7: 7 named [Fact] tests in B53Tests.cs with Arrange/Act/Assert documented for each |
| CP-03 | 7-scan checklist present | PASS — §8: SCAN-01 through SCAN-07 table plus F5-GATE-01/02, BUILD-01, TEST-01, LINK-01 |
| CP-04 | DW-B53-01 requirement traced to each change | PASS — §9: 5-row traceability matrix maps every §4 change to DW-B53-01 |
| CP-05 | F5 gate risk identified (AtmStrategy static call NT8-045 pattern) | PASS — §4C "UNCONFIRMED NT8 STATIC API — CRITICAL" block; §8 F5-GATE-01; Risk Register §10 row 1 (P1/HIGH risk, NT8-045 cross-reference) |

### Scope Checks (No Scope Creep Protocol §11)

| # | Item | Result |
|---|------|--------|
| SC-01 | Only CopyEngine.cs, PttFollowerStrategy.cs, and B53 test file in scope | PASS — §3 lists exactly two production files; B42Tests.cs/B46Tests.cs gating is a direct consequence of the PttFollowerStrategy #if gate (not unrelated cleanup) |
| SC-02 | PttContracts.cs FillSignal event left in place (no unnecessary removal) | PASS — §3 explicitly: "Not touched (out of scope)… FillSignal event and FillSignalEventArgs left intact" |
| SC-03 | No unrelated cleanup included | PASS — Plan is tightly bounded to the 5 B53-specific changes; no adjacent refactoring |

---

## Reviewer Notes (non-blocking observations for engineer)

1. **CYC-01 OnOrderUpdate is at the CYC=8 limit.** The plan uses "~6 branches before" (approximate). Engineer must verify the exact pre-change count at implementation time. If the actual count is higher than 6, the post-change CYC will exceed 8 and a helper extraction will be required before committing. The plan already calls this out ("At limit").

2. **F5-GATE-01 is the critical path for ticket close.** `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` as a static call is unconfirmed in the Linting DLL (same class-boundary pattern as NT8-045). The plan correctly requires F5 verification and specifies escalation to Director if the static call is absent. Engineer must NOT skip this gate.

3. **JS-002 / FindRuleByFollower `return null`:** SCAN-02 in §8 must be interpreted correctly — the grep for `return null` in CopyEngine.cs will hit this method, but the plan correctly explains it is `CopyRule?` (Nullable struct), not a reference null. Engineer must document this in the ticket completion report to prevent false-positive flags by downstream reviewers.

4. **B42Tests.cs and B46Tests.cs gating (§6) is in scope and mandatory.** Without gating these test files, the build will fail when `PTT_FOLLOWER_ACTIVE` is not defined, because the test classes reference `PttFollowerStrategy` directly. This is correctly identified in §6 and §10 (Risk Register row 3: "P1 / CERTAIN").

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Remove PttFollowerStrategy from follower entry-order path (DW-B53-01) | YES | §4E (compile-time gate) |
| Zero per-follower strategy setup required after B53 | YES | §4B (RaiseFillSignal removed) |
| CopyEngine places follower entry orders directly as AddOn citizen | YES (unchanged — already AddOn) | §9 row 3 |
| AtmStrategyCreate called on confirmed follower fill | YES | §4A + §4C |
| No entry slot conflict (managed framework not involved) | YES | §4E (PttFollowerStrategy inactive) |
| xUnit tests covering all new branches | YES | §7 (7 [Fact] tests) |
| 7-scan compliance checklist | YES | §8 |
| DW-B53-01 traceability | YES | §9 |
| F5 gate risk documented | YES | §4C + §8 + §10 |

---

*Review completed by ptt-plan-reviewer. REVIEW_PASS. Proceed to Phase 3 (ticket generation).*
