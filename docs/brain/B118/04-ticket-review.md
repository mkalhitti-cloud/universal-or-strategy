# B118 Ticket Review -- DW-B126 BE/QX Race Condition Fix

**Block**: B118
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-28
**Input**: `docs/brain/B118/04-tickets.md` (TICKETS_COMPLETE -- 2026-08-28)
**Plan**: `docs/brain/B118/02-architecture-plan.md` (REVIEW_PASS -- 2026-08-28)
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-110)
**Source baseline**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

---

## Ticket B118-T1 -- Cancel PTT-BE-* orders before QX submit -- DW-B126 race fix

### Traceability

| Item | Check | Result |
|------|-------|--------|
| TR-1 | Spec requirement IDs present (DW-B126, DW-B127) | PASS -- both listed in "Spec Requirement IDs" section with severity and description |
| TR-2 | Every method traces back to architecture plan | PASS -- CancelPttBeOrders, WaitForPttBeCancelled, IsPttBeOrder, IsNonTerminalPttBeState all appear in plan Sections B, C, Appendix |
| TR-3 | No phantom work (methods not in plan) | PASS -- ticket defines exactly the 4 methods specified in plan; Execute() insertions match plan Appendix diff sketch exactly |

**Verdict**: PASS

---

### JS Pre-Check

| Rule | Check | Ticket Location | Result |
|------|-------|-----------------|--------|
| JS-021 | No lock() in any code block | All 4 method bodies + Execute() insertions | PASS -- zero lock() in new code |
| JS-001 | No throw in hot path code blocks | WaitForPttBeCancelled timeout path explicitly logs + returns, no throw | PASS |
| JS-033 | No async void in method signatures | CancelPttBeOrders=int, WaitForPttBeCancelled=void (synchronous), IsPttBeOrder=bool, IsNonTerminalPttBeState=bool | PASS -- no async keyword anywhere |
| JS-002 | No return null in new methods | All new methods return value types (int, void, bool) -- no reference return, no null | PASS |

**Verdict**: PASS

---

### CYC Pre-Check

| Method | CYC in Ticket | Annotation Present | <= 8? | Result |
|--------|---------------|--------------------|-------|--------|
| CancelPttBeOrders | 7 | Yes -- comment block + Step 8 | YES | PASS |
| WaitForPttBeCancelled | 7 | Yes -- comment block | YES | PASS |
| IsPttBeOrder | 1 | Yes -- comment block | YES | PASS |
| IsNonTerminalPttBeState | 1 | Yes -- comment block | YES | PASS |
| Execute() | 8 (unchanged) | Yes -- Step 8 verification instruction | YES | PASS |

Branch decomposition in ticket comments verified against plan Section C table. All counts match.
The 4 inserted lines (2x CancelPttBeOrders + 2x WaitForPttBeCancelled) add no branches to Execute().

**Verdict**: PASS

---

### NT8 Check

| Item | Check | Ticket Citation | Result |
|------|-------|-----------------|--------|
| NT8-1 | Account.Cancel grounded in NT8_FULL_REFERENCE.md | "NT8: Account.Cancel(IEnumerable<Order>) -- NT8_FULL_REFERENCE.md lines 2408-2451" in method comment | PASS |
| NT8-1 | OrderState terminal values grounded in reference | "Source: NT8_FULL_REFERENCE.md lines 976-997" in IsNonTerminalPttBeState comment | PASS |
| NT8-1 | acc.Orders.ToList() pattern grounded | Cited in plan Section E (referenced from ticket Spec IDs); consistent with existing CopyEngine.cs usage | PASS |
| NT8-2 | Thread.Sleep on UI thread justified | Synchronous blocking poll inherited from plan Section B threading note; bounded at 1000ms; plan review Item 12 accepted | PASS |
| NT8-3 | Poll loop bounded and fail-safe | maxWaitMs=1000 (50 iterations x 20ms); timeout logs warning and returns, no throw, no hang | PASS |

**No NT8 constraint violations found.**

**Verdict**: PASS

---

### Test Coverage

| Item | Check | Result |
|------|-------|--------|
| TEST-1 | At least 8 xUnit [Fact] test names listed | PASS -- exactly 8 [Fact] tests named |
| TEST-2 | Tests cover cancel path, skip-terminal path, timeout path | PASS -- see coverage matrix below |
| TEST-3 | Test file name specified (B118Tests.cs) | PASS -- "All tests in src/PropTraderTools/Tests/B118Tests.cs" |

**Test coverage matrix**:

| Test Name | Path Covered |
|-----------|-------------|
| T_B118_CancelPttBe_WorkingTargetCancelled | PTT-BE-Target-* cancel (Working state) |
| T_B118_CancelPttBe_WorkingStopCancelled | PTT-BE-Stop-* cancel (Working state) |
| T_B118_CancelPttBe_TerminalOrderSkipped | Skip-terminal path -- Cancelled state excluded |
| T_B118_CancelPttBe_NullAccountReturnsZero | Null guard fast path |
| T_B118_CancelPttBe_NonPttBeOrderSkipped | Name predicate filter precision |
| T_B118_WaitPttBe_ReturnsFastWhenNoOrders | Wait fast path (expectedCount=0) |
| T_B118_WaitPttBe_ReturnsAfterTimeout | Timeout fail-safe -- does not hang, does not throw |
| T_B118_DW127_StructuralElimination | DW-B127 second-press fast path (all PTT-BE-* already terminal) |

All three required coverage paths present: cancel path (tests 1-2), skip-terminal (test 3), timeout (test 7).

**Verdict**: PASS

---

### Completeness (COMP)

| Item | Check | Result |
|------|-------|--------|
| COMP-1 | 7-scan checklist present and complete | PASS -- SCAN-01 through SCAN-07 all present |
| COMP-2 | Scan commands are executable PowerShell/bash | PASS -- all 7 are copy-paste runnable from workspace root |
| COMP-3 | Acceptance criteria are verifiable (not vague) | PASS -- AC-1 through AC-10 all specify observable, grep/diff/test-verifiable conditions |
| COMP-4 | Implementation steps are numbered and precise | PASS -- Steps 1-8 with full code bodies and exact insertion point descriptions |

**Verdict**: PASS

---

### Scan Checklist Presence (Defense in Depth -- NON-NEGOTIABLE)

Each of the 7 scans must be present with its correct rule mapping. All three pipeline layers
(ticket contract, engineer attestation in ticket-N-completion.md, verifier cross-check in
ticket-N-verification.md) depend on this contract being complete.

| Scan | Rule Enforced | Command Present | Result |
|------|---------------|-----------------|--------|
| SCAN-01 | JS-021 (lock() ban P0) | `grep -r "lock(" src/ --include="*.cs"` | PASS |
| SCAN-02 | JS-033 (async void ban P0) | `grep -rn "async void " src/ --include="*.cs"` | PASS |
| SCAN-03 | JS-002 (return null ban P0) | `grep -rn "return null;" src/ --include="*.cs"` | PASS |
| SCAN-04 | CSharpier formatting P1 | `dotnet csharpier check src/` | PASS |
| SCAN-05 | JS-066 CYC <= 8 P0 | `python scripts/complexity_audit.py` with per-method targets | PASS |
| SCAN-06 | ASCII-only DNA mandate | `grep -rP "[^\x00-\x7F]" src/ --include="*.cs"` + DateTime.UtcNow note | PASS |
| SCAN-07 | Build clean (0 errors, 0 warnings) | `dotnet build ...` + `dotnet test ...` | PASS |

All 7 scans present. All rule citations match the RULES_CATALOG.md entries. Engineer contract is complete.

**Verdict**: PASS

---

### Preserved Patterns

| Pattern | Where Documented in Ticket | Result |
|---------|-----------------------------|--------|
| PRES-1: _qxCancelInProgress guard | Covered by AC-8 (ExecuteOne() untouched) + Files Modified section (CopyEngine.cs NO CHANGE) | PASS |
| PRES-2: _qxPendingFollowerCleanup | Covered by AC-8 (ExecuteOne() untouched) + Files Modified section (CopyEngine.cs NO CHANGE) | PASS |
| PRES-3: DW-B115-DIAG blocks | AC-7: "Leader DIAG block (original lines 66-80) and follower DIAG block (original lines 93-121) are bit-for-bit identical to the baseline" | PASS |
| PRES-4: ExecuteOne() structure | AC-8: "ExecuteOne() method body is bit-for-bit identical to the baseline. CYC=2 unchanged." | PASS |

**Verdict**: PASS

---

### File Routing

| Check | Expected | Found in Ticket | Result |
|-------|----------|-----------------|--------|
| C# source paths | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | PASS |
| Test file path | Wave workspace src/ | `src/PropTraderTools/Tests/B118Tests.cs` | PASS |
| No Director workspace paths | No `universal-or-strategy-director` reference | None found | PASS |

**Verdict**: PASS

---

### Non-Blocking Warnings (WARN -- do not block engineer)

**WARN-1: Minor acc.Cancel() call form inconsistency**
- Location: Plan Section E uses `acc.Cancel(toCancel.ToArray())` but ticket Step 1 code body uses `acc.Cancel(toCancel)` (List<T> passed directly).
- Assessment: Both forms are valid. `List<NinjaTrader.Cbi.Order>` implements `IEnumerable<Order>`. NT8 signature is `Cancel(IEnumerable<Order>)`. No behavior difference. Not a rule violation.
- Action: Engineer may use either form. Recommend `acc.Cancel(toCancel)` as written in ticket (avoids unnecessary ToArray() allocation).

**WARN-2: Test name divergence between plan (Section F) and ticket (xUnit Test Names section)**
- Plan names (e.g., `T_B118_CancelPttBeOrders_ReturnsCancelledCount`) differ from ticket names (e.g., `T_B118_CancelPttBe_WorkingTargetCancelled`).
- Assessment: Ticket supersedes plan at Phase 3.5. Ticket test names are valid, well-named, and provide equivalent or improved granularity (plan combined target/stop into one test; ticket separates them for better failure isolation). Not a rule violation.
- Action: Engineer implements ticket test names. Architect may reconcile for documentation purposes at their discretion.

---

### Summary Scorecard

| Check Category | Result |
|----------------|--------|
| Traceability (TR-1 through TR-3) | **PASS** |
| JS Pre-Check JS-021, JS-001, JS-033, JS-002 | **PASS** |
| CYC Pre-Check CYC-1, CYC-2 | **PASS** |
| NT8 Constraints NT8-1, NT8-2, NT8-3 | **PASS** |
| Completeness COMP-1 through COMP-4 | **PASS** |
| Test Coverage TEST-1, TEST-2, TEST-3 | **PASS** |
| Defense in Depth DEF-1 through DEF-7 | **PASS** |
| Preserved Patterns PRES-1 through PRES-4 | **PASS** |
| File Routing | **PASS** |

**Violations found**: 0
**Warnings found**: 2 (non-blocking, see above)

---

## VERDICT: TICKET_REVIEW_PASS

All 30 checklist items pass. Zero rule violations. Two non-blocking warnings documented above.
The engineer contract is complete: spec requirements traced, method signatures exact, [Fact] test
names specified, 7-scan checklist present as required by defense-in-depth design
(ticket contract + engineer attestation + verifier cross-check).

The engineer may proceed to implement B118-T1 from `docs/brain/B118/04-tickets.md`.
The verifier (ptt-verifier) will use SCAN-01 through SCAN-07 as the anchor for independent
cross-check against the engineer's self-report in ticket-1-completion.md.
