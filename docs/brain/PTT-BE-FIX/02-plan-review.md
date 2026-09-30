# PTT-BE-FIX -- Plan Review (Cycle 2)
Status: REVIEW_PASS
Date: 2026-08-22
Reviewer: ptt-plan-reviewer
Cycle: 2 (Cycle 1 issued REVIEW_FAIL on JS-002; architect revised; Cycle 2 clears it)

---

## Cycle 1 Blocking Violation -- Cleared

| Rule | Location in plan | Cycle 1 verdict | Cycle 2 verdict |
|------|-----------------|-----------------|-----------------|
| JS-002 | Section B T2 -- `FindFollowerAccount` return type | FAIL -- returned `Account` (non-nullable) with `return null` path | PASS -- return type changed to `Account?`; call site uses `Account? found = FindFollowerAccount(...)`; nullability explicit end-to-end |

Verification checkpoints (all three required per task spec):
1. Helper declaration: `private static Account? FindFollowerAccount(string name)` -- CONFIRMED
2. Call site: `Account? found = FindFollowerAccount(dto.FollowerAccountNames[i]);` -- CONFIRMED
3. Section E JS-002 row: PASS for all tickets, no asterisk, no footnote about implicit null -- CONFIRMED

---

## Full Check Matrix (Cycle 2)

| # | Rule ID | Description | T1 | T2 | T3 | T4 | Result |
|---|---------|-------------|----|----|----|----|--------|
| 1 | JS-021 | No lock() / Monitor / Mutex / SemaphoreSlim | PASS | PASS | PASS | PASS | PASS |
| 2 | JS-001 | No throw in OnOrderUpdate / hot path | PASS | PASS | PASS | PASS | PASS |
| 3 | JS-002 | No null return on non-nullable type | PASS | PASS | PASS | PASS | PASS |
| 4 | JS-033 | No async void | PASS | PASS | PASS | PASS | PASS |
| 5 | JS-036 | No heap alloc in hot path | PASS (bool local) | PASS (Account? ref local) | N/A | N/A | PASS |
| 6 | JS-066 | CYC <= 8 per method | PASS (+0 to MoveStopToBreakEven) | PASS (DtoToRule 8->7, helper CYC=2) | N/A | N/A | PASS |
| 7 | JS-051 | xUnit only; no NUnit / MSTest | N/A | N/A | PASS ([Fact], Assert.True/False/Equal/Contains/NotNull) | N/A | PASS |
| 8 | ASCII-only | No Unicode / curly quotes in string literals | PASS | PASS (verified in plan) | PASS | PASS | PASS |
| 9 | NT8 API | All NT8 API claims valid for AddOnBase | PASS | PASS (Account.All is AddOnBase-safe) | N/A | PASS | PASS |
| 10 | Spec completeness | All spec requirements addressed | PASS | PASS | PASS | PASS | PASS |

Additional NT8 hard constraints (no violations found in any ticket):
- No async/await in OnInitialize / OnDestroyed / OnWindowCreated: N/A (not used)
- No Account.All in constructor: PASS (Account.All used in DtoToRule / FindFollowerAccount, not constructor)
- No sealed TradeCopierWindow: N/A
- No FontFamily override: N/A
- No hardcoded #RRGGBB hex: N/A
- No CreateOrder without PTT- prefix: N/A (T1 does not create orders; T4 confirms PTT-BE-Stop-* is leader-only)
- No DateTime.Now: N/A

---

## Spec Coverage Matrix

| Requirement | Plan section | Addressed? |
|-------------|-------------|------------|
| DW-B86: stop name guard extended to PTT-QX-Stop* | Section B T1 | YES |
| DW-B85: startup warning when follower not in Account.All | Section B T2 (Option B) | YES |
| DW-B85 Option A (lazy re-resolve): deferred per spec | Section A / Deferred Backlog | YES -- correctly deferred |
| DW-B84: xUnit tests for follower acc.Change() path | Section B T3 (10 [Fact] tests) | YES |
| DW-T4: TryReplacePttBeBrackets follower reachability | Section B T4 (ANALYSIS-COMPLETE) | YES |
| No regression to existing DIAG dump / StatusUpdate / acc.Change() plumbing | Section B T1 "What does NOT change" | YES |
| SIM gate protocol for Path B (QX then BE) | Section G | YES |

---

## CYC Re-Check (nullable annotation impact)

The `Account?` annotation on `FindFollowerAccount`'s return type is a compile-time type
annotation only. It adds zero if / foreach / while / ternary / catch branches.

- `DtoToRule` CYC: 8 (Cycle 1 baseline) minus 2 (inner foreach+if extracted) plus 1 (new null warning if) = 7. No change from Cycle 1.
- `FindFollowerAccount` CYC: 2 (foreach=1, if=1). No change from Cycle 1.
- `MoveStopToBreakEven` CYC: +0 net (isBeStop bool refactor is same branch count as original inline if). No change from Cycle 1.

All modified methods: CYC <= 8. PASS.

---

## Violations

None. Zero violations in Cycle 2.

---

## Decision

REVIEW_PASS

Cycle 2 clears the single blocking violation (JS-002) from Cycle 1. All other checks
remain PASS with no regression. The plan is approved to proceed to Phase 3 (ticket generation).
