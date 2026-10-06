# B191 Ticket Review
# QAll2t Per-Account Qty Fix (formalize) + DW-B187-01 Close

**Epic**: B191
**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5 (Ticket Review)
**Tickets reviewed**: docs/brain/B191/04-tickets.md
**Plan reviewed**: docs/brain/B191/02-architecture-plan.md (REVIEW_PASS)
**Plan-review notes**: docs/brain/B191/02-plan-review.md (REVIEW_PASS — zero violations)
**Rules catalog**: docs/standards/jane-street/RULES_CATALOG.md

---

## T1 — Formalize B191-DIRECT (code already committed at 365b8779)

### Traceability
PASS
- DW-B191-QTY-01 closure maps to plan Section 7, lines 350–359.
- Commit 365b8779 is the authoritative artifact for T1; the plan confirms "COMMITTED at 365b8779. No new code required for Ph4a." (plan Section 2, line 70).
- `perAccTargets = Build2TargetList(pos.Quantity)` maps to plan Section 2, lines 86–88.
- `forcedTargets` guard/log-only role maps to plan Section 2, lines 99–104.
- No phantom work: every T1 item traces to the plan or spec. No plan item is missing from T1.

### JS Pre-Check (JS-001/002/021/033/066/081)
PASS
- JS-021 (lock() ban): T1 is read-only; confirmed zero `lock()` in SCAN-01. No violation.
- JS-033 (async void ban): T1 is read-only; SCAN-02 confirms zero `async void`. No violation.
- JS-001 (no throw in hot path): T1 introduces zero new code. No throw. No violation.
- JS-002 (no return null): T1 introduces zero new return sites. No violation.
- JS-066 (CYC <= 8): T1 adds 0 branches (straight-line variable assignment per plan Section 2, line 107). Execute() CYC remains 8 — at ceiling, within limit.
- JS-081 (ASCII-only): `perAccTargets` is ASCII; comment text is ASCII. No violation.

### CYC Pre-Check
PASS
- Execute() CYC = 8 (pre-existing ceiling). T1 delta = 0. Method not at risk.
- Ticket explicitly states: "single variable assignment statement, no if/else/while/for/switch/catch/||/&& added." (SCAN-05 text)
- No method in T1 scope is at risk of exceeding JS-066 limit.

### NT8 Check
PASS
- T1 is read-only (no code written). No NT8 API patterns introduced.
- `Account.All` is pre-existing in `Execute()` — not modified by T1 (plan Section 11, confirmed).
- No `async/await` in lifecycle methods. No `DateTime.Now`. No `CreateOrder` without PTT-prefix.
- No `sealed` on window class. No `FontFamily`. No hardcoded hex color.

### Test Coverage
PASS
- T1 describes NO new methods (read-only verification ticket; code already committed).
- The roleDefinition requirement "every new method described in the ticket must have a [Fact] test" does not apply: T1 implements nothing.
- T2 provides the arithmetic coverage for `Build2TargetList` and the structural coverage for the `Execute` overload — explicitly noted at ticket line 80: "None for T1. T2 provides arithmetic coverage of Build2TargetList."

### Scan Checklist
PASS — all 7 scans present with complete commands and expected outputs:
- SCAN-01: `lock\(` grep on PttGlobalQuickExit.cs → 0 results.
- SCAN-02: `async void` grep on PttGlobalQuickExit.cs → 0 results.
- SCAN-03: `git show 365b8779` with `Select-String "perAccTargets"` → 4 patterns expected (perAccTargets assignment, ExecuteFollowers call, forcedTargets guard, forcedTargets log).
- SCAN-04: `dotnet build` → 0 errors, 0 warnings.
- SCAN-05: CYC delta = 0 via diff inspection.
- SCAN-06: `dotnet test --filter B116|B120|B123|B128` → all pass, 0 failed.
- SCAN-07: `ptt-sync.ps1` → 19 linked, 0 errors.

### File Routing
PASS
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — correct Wave workspace path.
- T1 is READ-ONLY; engineer is explicitly forbidden from writing this path.
- Completion note: `docs/brain/B191/ticket-1-completion.md` — correct doc path.

### DW Closure: DW-B191-QTY-01
PASS
- DW item declared as P0 (live incident). Root cause described: forcedTargets built from leader panel qty passed verbatim to all accounts.
- Fix identified: `perAccTargets = Build2TargetList(pos.Quantity)` per-iteration.
- Closed by T1 (commit 365b8779). Closing artifacts: ticket-1-completion.md and ticket-1-verification.md.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — B191Tests.cs (new xUnit test file)

### Traceability
PASS
- Test coverage requirement maps to plan Section 3 ("T2 — Test Coverage Strategy").
- 5 [Fact] methods map to plan Section 10 method signatures (B191Tests class, 5 `[Fact]` entries).
- T2-1 oracle (`Build2TargetList(5)→{3,2}`) maps to plan Section 3, T2-1 table + line 142.
- T2-2 oracle (`Build2TargetList(20)→{10,10}`) maps to plan Section 3, T2-2 + line 143.
- T2-3 oracle (sum invariant == 5) maps to plan Section 3, T2-3 + line 145.
- T2-4 oracle (reflection overload check) maps to plan Section 3, T2-4 + lines 147–152.
- T2-5 oracle (`Build2TargetList(1)→{1,0}`) maps to plan Section 3, T2-5 + line 144.
- No phantom work. No plan coverage missing from T2.

### Test Oracle Arithmetic Verification
PASS — all three oracles verified against `Build2TargetList` formula `t1=(totalQty+1)/2; t2=totalQty-t1`:
- `Build2TargetList(5)`: `t1=(5+1)/2=6/2=3`; `t2=5-3=2` → `{first.Qty=3, second.Qty=2}` ✓
- `Build2TargetList(20)`: `t1=(20+1)/2=21/2=10` (integer truncation); `t2=20-10=10` → `{first.Qty=10, second.Qty=10}` ✓
- `Build2TargetList(1)`: `t1=(1+1)/2=2/2=1`; `t2=1-1=0` → `{first.Qty=1, second.Qty=0}` ✓ (boundary)
- Sum invariant: `t1+t2=3+2=5` ✓

### JS Pre-Check (JS-021/033/066/081 + xUnit mandate)
PASS
- JS-021 (lock() ban): SCAN-07 explicitly checks for `lock\(` → 0 results. No violation described.
- JS-033 (async void ban): JS rule table states "no async void test methods". No violation described.
- JS-066 (CYC <= 8): All 5 test methods are straight-line `[Fact]` methods, CYC=1. No violation.
- JS-081 (ASCII-only): JS rule table explicitly states "All test method names, Assert messages — ASCII only." No violation described.
- xUnit mandate: `[Fact]` only. No `[Theory]`. No `[Fact(Skip=...)]`. SCAN-01 AND SCAN-07 both enforce zero Skip. No violation.

### CYC Pre-Check
PASS
- All 5 test methods are trivially CYC=1 (straight-line Assert calls). No method approaches JS-066 limit of 8.

### NT8 Check
PASS
- SCAN-02 enforces zero `NinjaTrader.` namespace imports or usage.
- The permitted exception (`typeof(NinjaTrader.Cbi.Instrument)` in T2-4 reflection test) is a type reference only — no NT8 runtime instantiation.
- A fallback form (parameter count check) is provided, ensuring SCAN-02 can unconditionally return 0 if the type reference form is not used. This is correct architectural guidance, not a violation.
- `// No NinjaTrader.* using directives permitted in this file.` is stated in the method signature block (ticket line 161).

### Test Coverage
PASS
- T2 IS the test file. Every new method in `B191Tests` is itself a `[Fact]` test — no test-of-test is required.
- The 5 test method signatures are fully specified with exact names, parameters, and expected assertions (Test Oracles table).

### Scan Checklist
PASS — all 7 scans present with complete commands and expected outputs:
- SCAN-01: `[Fact]` count in B191Tests.cs → exactly 5; AND `Skip` → 0 results.
- SCAN-02: `NinjaTrader\.` grep on B191Tests.cs → 0 results (with documented exception for typeof form).
- SCAN-03: `dotnet test --filter "FullyQualifiedName~B191"` → 5 passed, 0 failed, 0 skipped.
- SCAN-04: `dotnet test` output for `Build2TargetList_Qty5_ReturnsT1_3_T2_2` — PASS.
- SCAN-05: `dotnet test` output for `Build2TargetList_Qty1_ReturnsT1_1_T2_0` — PASS (boundary).
- SCAN-06: `dotnet test` output for `Build2TargetList_Qty5_SumEqualsInput` — PASS (sum invariant).
- SCAN-07: `lock\(` grep on B191Tests.cs → 0 results.

### File Routing
PASS
- `src/PropTraderTools/Tests/B191Tests.cs` — correct Wave workspace path.
- Completion note: `docs/brain/B191/ticket-2-completion.md` — correct doc path.

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — Restore followerPosQty in ExecuteFollowers (closes DW-B187-01)

### Traceability
PASS
- DW-B187-01 closure maps to plan Section 7, lines 361–371.
- `int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0` maps to plan Section 4, lines 192–202 (exact T3 target state code block).
- CYC delta=0 for ExecuteFollowers maps to plan Section 4, lines 221–225 and CYC table.
- `FindPositionPublic` (internal) rather than `FindPosition` (private) maps to plan-review Check 4 note and plan Section 4, lines 215–219.
- Comment lines for DW-B187-01 map to plan Section 4 T3 target code block (3 comment lines shown).
- No phantom work. No plan item missing from T3.

### JS Pre-Check (JS-001/002/021/033/066/081)
PASS
- JS-021 (lock() ban): SCAN-01 checks for `lock\(` on PttGlobalQuickExit.cs → 0 results. T3 null-conditional chain `CopyEngine.Instance?.FindPositionPublic(...)?.Quantity ?? 0` contains no lock. No violation.
- JS-033 (async void ban): SCAN-02 checks for `async void`. `ExecuteFollowers` is synchronous void (not event handler, not async). No violation.
- JS-001 (no throw in hot path): `?.` propagates null safely through `CopyEngine.Instance` (can be null at shutdown), `FindPositionPublic` (returns null on no-match), and `.Quantity` (int). `?? 0` unwraps to value type. No NullReferenceException possible. No throw introduced. PASS.
- JS-002 (no return null at new sites): T3 adds zero new return sites. `FindPositionPublic` null return is a pre-existing contract (CopyEngine.cs:5538). No violation.
- JS-066 (CYC <= 4 for ExecuteFollowers): CYC Verification section confirms `int followerPosQty = ...` is a variable assignment expression. `?.` and `??` are expressions, NOT McCabe branches (no code path divergence). Delta = 0. CYC after T3 = 4. Confirmed within JS-066 limit of 8. PASS.
- JS-081 (ASCII-only): `followerPosQty` is ASCII. Three comment lines are ASCII-only. No violation.

### CYC Pre-Check
PASS
- `ExecuteFollowers` CYC = 4 before T3. CYC = 4 after T3. Delta = 0.
- Ticket explicitly documents: "The `?.` and `??` operators are null-conditional EXPRESSIONS — they do NOT create McCabe branches." (CYC Verification section)
- No method at risk of exceeding JS-066 ceiling.

### NT8 Check
PASS
- T3 accesses `CopyEngine.Instance` (singleton property) and calls `FindPositionPublic(follower, pos.Instrument)` — both are internal `PropTraderTools` APIs, not NT8 SDK APIs.
- `pos.Instrument` is a pre-existing NT8 object reference (not constructed here). No new NT8 construction.
- No `async/await` in lifecycle methods. No `DateTime.Now`. No `CreateOrder`. No `Account.All` outside Loaded handler (pre-existing usage in Execute(), not modified here).
- No `FontFamily`. No hardcoded hex. No `sealed` on window.

### Test Coverage
PASS
- T3 edits `ExecuteFollowers` (existing private method; signature unchanged). No new methods are introduced by T3.
- The roleDefinition requirement applies only to "new methods described in the ticket." No new methods → no new [Fact] required.
- Regression coverage is provided via the pre-existing B116 filter test suite (SCAN-06).

### Scan Checklist
PASS — all 7 scans present with complete commands and expected outputs:
- SCAN-01: `lock\(` grep on PttGlobalQuickExit.cs → 0 results.
- SCAN-02: `async void` grep on PttGlobalQuickExit.cs → 0 results.
- SCAN-03: `Select-String "followerPosQty"` with two expected match lines: (1) the new variable declaration using `FindPositionPublic` (not `FindPosition`); (2) `ResolveFollowerTargets` call with `followerPosQty` as arg 3 (not literal `0`). Negative patterns also specified.
- SCAN-04: CYC delta = 0 via diff inspection (one variable assignment + one argument substitution — no new branches).
- SCAN-05: `dotnet build` → 0 errors, 0 warnings.
- SCAN-06: `dotnet test --filter "FullyQualifiedName~B116|B120|B123|B128"` → all pass, 0 failed, 0 skipped.
- SCAN-07: `ptt-sync.ps1` → 19 linked, 0 errors.

### File Routing
PASS
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — correct Wave workspace path.
- Completion note: `docs/brain/B191/ticket-3-completion.md` — correct doc path.

### DW Closure: DW-B187-01
PASS
- DW item is P3 (carried from B187 via B188, B189, B190). Root cause described: B187-T2 deleted `_fPosQty` computation block; B187 engineer substituted literal `0`, leaving `ScaleLeaderTargets` permanently unreachable.
- Fix: `int followerPosQty = CopyEngine.Instance?.FindPositionPublic(follower, pos.Instrument)?.Quantity ?? 0`.
- Data source validated: same path used by `PttBreakEvenSwap.cs:53` and `TradeCopierPanel.cs:1788` (plan Section 4, lines 207–210).
- DW item closed by T3. Closing artifacts: ticket-3-completion.md and ticket-3-verification.md.

### VERDICT: TICKET_REVIEW_PASS

---

## Cross-Ticket Checks

### Sequential Order Enforcement
PASS
- PIPELINE ORDER section explicitly states: `T1 (read-only verification) -> T2 (new test file) -> T3 (.cs edit on top of T1 commit)`.
- T3 dependency on T1 is clearly stated: "T3 must apply on top of the already-committed T1 (commit 365b8779). T2 is independent and may run before or after T3, but T3 must not begin until T1 completion note is written."
- No ticket may be parallelized with a sibling that touches the same `.cs` file. Both T1 and T3 touch `PttGlobalQuickExit.cs` and are correctly serialized.

### Scope Isolation
PASS
- T1: read-only, zero file writes (`.cs` write explicitly forbidden).
- T2: creates exactly one new file (`B191Tests.cs`). No other files modified.
- T3: surgical edit of exactly two locations in `PttGlobalQuickExit.cs`. "No other lines in the file may be changed for this ticket."
- No ticket requests engineer to combine scope with another ticket.

### VERIFY Criteria Coverage
PASS
- Completion Gate section (ticket lines 485–497) lists all 9 binary criteria for Ph4b VERIFY_PASS.
- Criteria cover all three tickets: ticket-1-completion.md + 7 SCAN results; ticket-2-completion.md + 7 SCAN results + B191Tests.cs exists + 5/0/0 dotnet test; ticket-3-completion.md + 7 SCAN results + exact git diff + FindPositionPublic confirmed + ptt-sync 19 linked + dotnet build 0E 0W.
- Each criterion is binary (present/absent or pass/fail). Verifier has unambiguous pass/fail signals.

### Spec Coverage Matrix
PASS — all spec requirements from the plan are addressed by exactly one ticket:
- T1: perAccTargets per-account loop (commit 365b8779) → Ticket T1 only.
- T2: 5 xUnit [Fact] tests for Build2TargetList + reflection check → Ticket T2 only.
- T3: int followerPosQty restore + DW-B187-01 close → Ticket T3 only.
- DW-B183-03: correctly not addressed (carried forward, plan Section 8).

---

## Overall

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | DW Closure | Verdict |
|--------|-------------|-------------|--------------|-----------|--------------|----------------|-------------|------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS (DW-B191-QTY-01) | **PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | N/A | **PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS (DW-B187-01) | **PASS** |

**Cross-ticket: PASS** (sequential order, scope isolation, VERIFY criteria coverage, spec coverage matrix)

**Total violations**: 0
**Blocking issues**: None

---

## TICKET_REVIEW_PASS

All three tickets satisfy every mandatory check. Engineer is cleared to begin T1.

---

*Written by: ptt-ticket-reviewer*
*Epic: B191*
*Phase: 3.5 (Ticket Review)*
*Verdict: TICKET_REVIEW_PASS*
