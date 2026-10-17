# B200+B201 Plan Review

**Epic**: PTT-COPIER-B200 + B201 — Direct Repair Formalisation + Entry-Order Cancel Fix
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-10-15
**Plan under review**: `docs/brain/B200/02-architecture-plan.md`
**References read**:
- `docs/brain/B200/02-architecture-plan.md`
- `docs/brain/B200-B201-orchestrator-prompt.md`
- `docs/brain/B200-orchestrator-prompt.md`
- `docs/standards/jane-street/RULES_CATALOG.md`
- `src/PropTraderTools/CopyEngine.cs` (direct code verification)
- `docs/brain/B199/06-deferred-backlog.md`

---

## VERDICT: REVIEW_PASS

No violations found. Zero JS-rule failures. Plan is internally consistent, traces to all bug IDs, and correctly resolves the B201 static constraint that the orchestrator prompt got wrong.

---

## Section A: Lane-Split Gate Compliance

**Gate result line present**: YES — appears at plan line 10 and again at line 20.

**Gate evaluation review**:
- Q1 (all changes in same method or within 50 lines?): NO — T2 touch points span L982–L1230 (248 lines apart). Correct answer.
- Q2 (any fix's design depends on another fix's final design?): YES — T3 (B201Tests.cs) tests `IsQxCancelCandidate(Order o, bool isFollower)`, a signature produced by T2. T3 cannot be finalised until T2 signature is confirmed. Correct dependency identified.
- Q2=YES is a STOP condition per gate protocol → SINGLE-PIPELINE is the correct result.

**Gate result**: `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` — **COMPLIANT**

---

## Section B: Spec Coverage Matrix

| Requirement ID | Spec Requirement | Plan Address? | Plan Section |
|---------------|-----------------|---------------|--------------|
| B200-QTY-01 | `fo.Quantity` → `fo.Quantity + fo.Filled` in HandleEntryChange | YES — code verified at L4643 | Section 1.1 |
| B200-SFB-NULL-01 | `_followerSlateReady[acc.Name] = true` missing in TryLaneDDispatchFollower | YES — code verified at L2966-2971 | Section 1.2 |
| B200-NAKED-01 | TryNakedDetect skips "Entry"-named orders on follower accounts | YES — code verified at L7173-7182 | Section 1.3 |
| B200-CLOSE-LANED-01 | V13 sentinel anchor + PartFilled in HasActivePttFlatten + FlattenOneAccount | YES — code verified at L3052-3065, L5670-5675, L5687 | Section 1.4 |
| B201-ENTRY-CANCEL-01 | IsQxCancelCandidate "Entry" check not scoped to follower accounts | YES — fix design with 5 touch points | Section 2 |
| T1 test coverage | xUnit tests for all 4 B200 repairs | YES — 4 [Fact] tests defined | Section 4, T1 |
| T3 test coverage | xUnit tests for B201 fix | YES — 4 [Fact] tests defined | Section 4, T3 |
| Deferred backlog | 06-deferred-backlog.md with 3 DW items | YES — specified in T4 | Section 4, T4 |
| Log cleanup | [PTT-ATM-CALLER] and [PTT-FLAT-OPEN] audit | YES — confirmed absent, T4 no-op | Section 4, T4 |

**Coverage: 9/9 PASS**

---

## Section C: JS Rules Compliance Audit

### C.1 JS-021 — Lock Ban (P0)

**Checked**: All new/modified code for `lock()`, `Monitor`, `Mutex`, `SemaphoreSlim`.

- T1 (B200Tests.cs): No lock. Test assertions only. **PASS**
- T2 (CopyEngine.cs): `bool isFollower = IsFollowerAccount(acc)` — pure method call, no lock. ConcurrentDictionary used throughout (lock-free by design, JS-021 compliant). Plan explicitly cites this at Section 2.2 and Section 6. **PASS**
- T3 (B201Tests.cs): No lock. Test assertions only. **PASS**
- T4 (log confirm + deferred backlog): No new code. **PASS**
- Pre-scan command present in Section 5 and Section 7 (T2 SCAN-01). **PASS**

**Result: JS-021 PASS**

### C.2 JS-001 — No throw in Hot Paths (P0)

**Checked**: `OnOrderUpdate`, `SendCopyWithAtm`, gate chain methods, all T2 touch points.

- None of the 5 T2 touch points introduce a `throw` statement.
- `IsQxCancelCandidate` body change is `if (isFollower && o.Name == "Entry") return true;` — no throw.
- `CancelQxBrackets` 2-arg: pre-existing `catch { }` at L1038 is untouched by T2; the new `bool isFollower = IsFollowerAccount(acc)` assignment is not a throw.
- Plan confirms at Section 5: JS-001 PASS for all tickets.

**Result: JS-001 PASS**

### C.3 JS-033 — No async void (P0)

**Code verified**: `grep "async void"` on CopyEngine.cs returns 2 comment-only matches (lines 2232 and 7439), both describing the *absence* of async void. Zero actual `async void` declarations in touched methods.

**Result: JS-033 PASS**

### C.4 JS-066 — CYC ≤ 8 per Method (P1)

**Full CYC table from plan Section 3, cross-checked against code:**

| Method | Pre-Epic CYC | Post-Epic CYC | Code Comment | Status |
|--------|-------------|--------------|--------------|--------|
| `HandleEntryChange` | 6 | 6 (+0) | L4517 (arithmetic change only) | PASS |
| `TryLaneDDispatchFollower` | 4 | 4 (+0) | L2966 (dict write, no branch) | PASS |
| `TryNakedDetect` | 4 | 5 (+1) | L7173 (new if guard) | PASS |
| `HasActivePttFlatten` | 3 | 4 (+1) | L3047-L3065 (PartFilled added) | PASS |
| `FlattenOneAccount` | 6 | 7 (+1) | L5650: "CYC=7" confirmed in code | PASS |
| `IsQxCancelCandidate` | 7 | 7 (+0) | L981: "CYC=7" confirmed in code | PASS |
| `CancelQxBrackets` 2-arg | 6 | 6 (+0) | L1005 comment | PASS |
| `CancelQxBrackets` 3-arg | 7 | 7 (+0) | L1197 comment | PASS |
| `BuildQxSnapshot` | 4 | 4 (+0) | L1046 comment | PASS |

**All methods ≤ 8. No budget breach.**

**Note on IsQxCancelCandidate CYC delta claim**: The plan claims `&&` extending an existing `if` is +0 per Roslyn McCabe convention (project standard). This is the established convention in the B19x/B20x epic series. The claim is architecturally documented in Section 2.4 and consistent with prior approved plans. Accepted.

**Note on FlattenOneAccount**: B200-orchestrator-prompt.md (older document) stated pre-fix CYC=7, post=8. The B200+B201 architecture plan states pre-fix CYC=6, post=7. The actual code comment at L5650 reads `CYC=7` and the fix is already applied. The plan's post-fix CYC=7 matches the code annotation. The discrepancy in the older orchestrator prompt (which predicted 8) is immaterial — the code is the authoritative source and it confirms CYC=7.

**Result: JS-066 PASS**

### C.5 JS-002 — No return null (P0)

**Checked**: `BuildQxSnapshot` returns `new HashSet<Order>()` on null input (confirmed at code L1056). No new `return null` introduced in T2 touch points.

**Result: JS-002 PASS**

---

## Section D: B201 Critical Finding Verification

**Finding**: `BuildQxSnapshot` is declared `internal static` (CopyEngine.cs L1050, confirmed by code read). `IsFollowerAccount` is an instance method (L861, confirmed by code read: `internal bool IsFollowerAccount(Account acc)`). A static method cannot call an instance method.

**Orchestrator error**: `B200-B201-orchestrator-prompt.md` lines 164-168 incorrectly states touch point 3c should add `bool isFollower = IsFollowerAccount(acc)` inside `BuildQxSnapshot`. This would not compile.

**Plan response**: Section 2.2 (lines 222-237) explicitly identifies this constraint as the "CRITICAL STATIC CONSTRAINT" and proposes the correct alternative: add `bool isFollower = false` as a defaulted parameter to `BuildQxSnapshot`. This solution is:
- Backward-compatible: existing callers (`PttQuickExit.cs` L92 calls `CopyEngine.BuildQxSnapshot(leader, instr)`) remain unmodified via C# default parameter semantics.
- Semantically correct: `isFollower=false` for the leader context means the leader's "Entry" order is NOT cancelled (the exact desired behaviour).
- Statically safe: no instance method called from static context.

**T2 acceptance criteria correctly reflects this**: plan line 522 says `grep "bool isFollower = IsFollowerAccount"` should return **2 matches** (not 3), because `BuildQxSnapshot` uses the defaulted parameter, not the `IsFollowerAccount` call. This is coherent and correct.

**Result: B201 Critical Finding PASS — plan correctly overrides the flawed orchestrator instruction**

---

## Section E: Scope Correctness Check

| Ticket | Expected Scope | Plan Specifies | Status |
|--------|---------------|----------------|--------|
| T1 | New test file only — no src changes | `B200Tests.cs` NEW, no `.cs` source changes | PASS |
| T2 | CopyEngine.cs only — 5 touch points | CopyEngine.cs only, 5 touch points, Feature files explicitly excluded | PASS |
| T3 | New test file only — no src changes | `B201Tests.cs` NEW, after T2 sig confirmed | PASS |
| T4 | CopyEngine.cs (log confirm, no-op) + 06-deferred-backlog.md | Correctly identifies as no-op; deferred backlog creation | PASS |

**Do-NOT-touch list present**: Plan explicitly calls out `PttBreakEvenSwap.cs`, `PttGlobalQuickExit.cs`, `PttQuickExit.cs`, and all Feature files at:
- Plan header table (line 51): "Do NOT touch: PttBreakEvenSwap.cs, PttGlobalQuickExit.cs, PttQuickExit.cs, or any Feature file"
- Section 4, T2 (line 337): "Do NOT change: PttBreakEvenSwap.cs, PttGlobalQuickExit.cs, PttQuickExit.cs, or any Feature file."

**Result: Scope PASS**

---

## Section F: xUnit-Only Test Framework Compliance

**T1 tests (B200Tests.cs)**:
- All 4 test methods named `T_B200_0N_*` — standard `[Fact]` pattern
- Section 7, T1 SCAN-05 explicitly states: "`using Xunit;` — `[Fact]` only. No `[TestMethod]`, no `[Test]`."
- No NUnit or MSTest attributes in any test design.

**T3 tests (B201Tests.cs)**:
- All 4 test methods named `T_B201_0N_*` — standard `[Fact]` pattern
- Section 7, T3 SCAN-05 explicitly states: "`using Xunit;` + `[Fact]` only."

**Result: xUnit-only PASS**

---

## Section G: ASCII-Only Compliance

**Checked**: All string literals in plan code examples and Section 7 scan checklist items.

- `"Entry"`, `"#pending"`, `"PTT-QX-Stop"`, `"PTT-BE-Target-1"`, `"PTT-CopyEntry"`, `"Stop1"`, `"Target2"` — all ASCII.
- Log tag strings `[PTT-ATM-SEND]`, `[PTT-DR4-CLEAR]`, `[PTT-GATE5-BLOCKED]` — all ASCII.
- No Unicode, no emoji, no curly quotes in any code block.
- Section 5 compliance table cites ASCII PASS for all tickets.

**Result: ASCII-only PASS**

---

## Section H: T4 Log Cleanup No-Op Verification

**Architect's claim**: Both `[PTT-ATM-CALLER]` and `[PTT-FLAT-OPEN]` tags are already absent from `CopyEngine.cs`.

**Code verification**:
```
grep "[PTT-ATM-CALLER]" CopyEngine.cs → 0 matches (CONFIRMED)
grep "[PTT-FLAT-OPEN]" CopyEngine.cs  → 0 matches (CONFIRMED)
```

**Plan reflects correctly**: Section 4 T4 (line 389) states: "Current state (architect verified): Both diagnostic tags are already absent from `CopyEngine.cs`. Log removal is a **no-op** for this ticket."

The orchestrator prompt (`B200-B201-orchestrator-prompt.md` lines 241-248) listed specific expected line numbers for these tags. The plan correctly documents that a grep-confirm must be run before any action, and if absent (as is the case), no changes are made.

**T4 SCAN-07 reflects this** (Section 7, T4): "T4 creates `06-deferred-backlog.md` ONLY (besides the log-confirm step). If logs already absent (architect verified: they are), no CopyEngine.cs changes in T4."

**Result: T4 Log Cleanup PASS**

---

## Section I: 7-Scan Checklist Verification

Plan Section 7 provides explicit 7-scan checklists for all 4 tickets:

| Ticket | SCAN-01 (Lock) | SCAN-02 (Async void) | SCAN-03 (CYC) | SCAN-04 (Throw) | SCAN-05 (Framework) | SCAN-06 (ASCII) | SCAN-07 (File scope) |
|--------|---------------|---------------------|----------------|-----------------|---------------------|-----------------|----------------------|
| T1 | PASS | PASS | PASS | PASS | Xunit/[Fact] only | PASS | B200Tests.cs ONLY |
| T2 | PASS | PASS | All methods ≤8 | PASS | Static constraint noted | PASS | CopyEngine.cs ONLY |
| T3 | PASS | PASS | CYC=1 per test | PASS | Xunit/[Fact] only | PASS | B201Tests.cs ONLY |
| T4 | PASS | PASS | No code changes | PASS | N/A | PASS | 06-deferred-backlog.md ONLY |

**All 4 tickets have full 7-scan checklists. Result: PASS**

---

## Section J: Deferred Backlog Items Verification

**Required**: Plan must reference the 3 deferred items as deferred — NOT to be implemented.

| Item | Plan Location | Status |
|------|--------------|--------|
| DW-B200-LANE-E-COPY-ENABLED (P2) | Section 4 T4, lines 404-406; Section 9 line 573 | Documented as deferred to B202+ |
| DW-B201-OPTION-B (P3) | Section 4 T4, lines 408-410; Section 9 line 574 | Documented as deferred pending Director decision |
| DW-B199-H2-CLEANUP-01 (P3) | Section 4 T4, lines 411-413; Section 9 line 569 | Carried forward from B199, conditional check in T4 |

All three items are clearly marked "do NOT implement" in Section 4, T4.

Prior B199 backlog items (18 items) are correctly listed in Section 9 as carry-forwards NOT addressed by B200+B201.

**Result: Deferred Items PASS**

---

## Section K: Threading Model Review

Plan Section 6 explicitly maps every modified method to its execution thread:

- Account background thread methods (`HandleEntryChange`, `TryLaneDDispatchFollower`, `TryNakedDetect`, `FlattenOneAccount`): use ConcurrentDictionary lock-free operations. **JS-023 compliance**: No UI updates from off-thread. No `Dispatcher.InvokeAsync` required (confirmed: none of these methods touch WPF/XAML elements).
- Dispatcher thread methods (`IsQxCancelCandidate`, `CancelQxBrackets` 2-arg and 3-arg, `BuildQxSnapshot`): pure predicates or ConcurrentDictionary operations. **JS-021 compliant**.

**Result: Threading PASS**

---

## Section L: NT8 API Validity Check

- `Account.Orders` enumeration used in `CancelQxBrackets` and `BuildQxSnapshot`: standard AddOnBase API. ✅
- `Account.Cancel(Order[])`: standard AddOnBase API. ✅
- `IsFollowerAccount(Account acc)`: instance method on `CopyEngine` — not an NT8 API call, project-internal method. ✅
- No `AtmStrategyCreate` or StrategyBase-only APIs proposed in the plan.
- No `Account.All` in constructor.
- No `sealed TradeCopierWindow`.
- No `FontFamily` override.
- No hardcoded `#RRGGBB` hex colors.
- No `CreateOrder` without PTT- prefix.
- No `DateTime.Now` (only `Environment.TickCount` used for debounce).

**Result: NT8 API PASS**

---

## Section M: Traceability Verification

Each ticket maps to exactly the required bug IDs:

| Ticket | Bug IDs Traced | Spec Coverage |
|--------|---------------|---------------|
| T1 | B200-QTY-01, B200-SFB-NULL-01, B200-NAKED-01, B200-CLOSE-LANED-01 | 4 of 4 B200 repairs |
| T2 | B201-ENTRY-CANCEL-01 | 1 of 1 B201 bugs |
| T3 | B201-ENTRY-CANCEL-01 | Tests for T2 fix |
| T4 | All 5 (CYC confirmation of all touched methods) | Code quality audit |

**Result: Traceability PASS**

---

## Section N: Acceptance Criteria Completeness

**T1**: 5 acceptance criteria including `dotnet test --filter "B200"` pass, no src changes, ptt-sync 19/0.
**T2**: 8 acceptance criteria including grep checks for both `isFollower` guard lines, lock-ban confirmation, deletion audit, single-hunk ban.
**T3**: 4 acceptance criteria including `dotnet test --filter "B201"` pass, no src changes.
**T4**: 5 acceptance criteria including absent-tag confirmation and 06-deferred-backlog.md creation.

All criteria are specific, grep-verifiable, and non-ambiguous.

**Result: Acceptance Criteria PASS**

---

## Section O: Observations (Non-Blocking)

These are informational notes — they do not affect the REVIEW_PASS verdict.

1. **Orchestrator prompt discrepancy on touch point 3c**: `B200-B201-orchestrator-prompt.md` line 164 instructs `bool isFollower = IsFollowerAccount(acc)` inside `BuildQxSnapshot`. This instruction is incorrect (static method cannot call instance method). The architect correctly identified this as the "CRITICAL STATIC CONSTRAINT" and overrode the orchestrator with the defaulted-parameter solution. The plan is correct; the orchestrator prompt contains an error that would have caused a compile failure. This is not a plan defect — it is a plan *correction* of an orchestrator defect. No action required.

2. **FlattenOneAccount CYC discrepancy between orchestrator prompts**: The older `B200-orchestrator-prompt.md` estimated post-fix CYC=8; the architecture plan states post-fix CYC=7. The code comment at L5650 authoratively states CYC=7. The plan is correct.

3. **B200-B201-orchestrator-prompt.md T2 verifier grep count**: The orchestrator prompt (line 183) states the grep should show "3 matches" for `bool isFollower = IsFollowerAccount`. The plan's T2 acceptance criteria correctly states "2 matches" because `BuildQxSnapshot` uses the defaulted parameter approach. The plan's 2-match assertion is correct; the orchestrator prompt's 3-match assertion is wrong. Engineer must follow the plan's 2-match criterion.

---

## Final Verdict

| Check | Result |
|-------|--------|
| Lane-split gate present and correct | PASS |
| All 5 bug IDs traced to tickets | PASS |
| B201 static constraint correctly handled | PASS |
| JS-021 (no lock) | PASS |
| JS-001 (no throw in hot path) | PASS |
| JS-002 (no return null) | PASS |
| JS-033 (no async void) | PASS |
| JS-066 (CYC ≤ 8 all methods) | PASS |
| xUnit [Fact] only for T1 and T3 | PASS |
| ASCII-only string literals | PASS |
| T4 log cleanup correctly identified as no-op | PASS |
| 7-scan checklist for all 4 tickets | PASS |
| Do-NOT-touch list present and complete | PASS |
| 3 deferred items documented, not implemented | PASS |
| NT8 API validity | PASS |
| Threading model correct | PASS |

**REVIEW_PASS**

Phase 3 (ptt-architect → `docs/brain/B200/04-tickets.md`) is UNLOCKED.

---

*Written by: ptt-plan-reviewer*
*Epic: B200+B201*
*Phase: 2 (Plan Review)*
*Verdict: REVIEW_PASS — zero violations*
