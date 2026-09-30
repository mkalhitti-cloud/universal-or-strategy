# Final Review -- B131 LaneA DW-B138
## ATM Bracket Drag Name-Fallback Fix

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Epic**: B131 LaneA
**Requirement**: DW-B138 -- ATM Bracket Drag Not Reaching SyncFollowerBracket for Stop1/T1/T2
**Date**: 2026-08-31
**Inputs read**:
- `docs/brain/B131/LaneA-02-architecture-plan.md`
- `docs/brain/B131/LaneA-04-ticket-review.md` (TICKET_REVIEW_PASS)
- `docs/brain/B131/LaneA-ticket-1-completion.md` (BUILD_PASS)
- `docs/brain/B131/LaneA-ticket-1-verification.md` (VERIFY_PASS)
- `docs/standards/jane-street/RULES_CATALOG.md`
- `src/PropTraderTools/CopyEngine.cs` (source-actual, key lines)
- `src/PropTraderTools/Tests/B131Tests.cs` (source-actual, [Fact] enumeration)
- `docs/brain/B130/LaneA-06-deferred-backlog.md` -- NOT FOUND (no prior deferred items to import)

---

### Verdict: FINAL_PASS

No violations found. All 6 sections pass. All gate conditions satisfied.

---

## Section A: Epic Coherence

**Root cause addressed**: PASS
The plan identified H2 as primary: `FindFollowerBracketOrder` matches follower bracket orders by
`FromEntrySignal` string equality; PTT-placed follower brackets carry `FromEntrySignal = null / ""`;
ATM leader brackets carry a non-null signal name; `null != "signal"` evaluates `true` -> the follower
order is always skipped -> `FindFollowerBracketOrder` returns `null` -> `SyncFollowerBracket` exits at
the early-return guard (L2140) before any cancel+resubmit fires.

**Fix implemented as specified**: PASS
Three changes, all in `CopyEngine.cs`, exactly as the architecture plan specified:

| Plan item | Actual (source-verified) |
|-----------|--------------------------|
| `SignalOrNameMatches` -- new private/internal static predicate | L2361-2368 confirmed: `internal static bool SignalOrNameMatches(Order, string?, string?)` |
| `FindFollowerBracketOrder` -- new `string? leaderName = null` param; `string?` on `fromEntrySignalName`; `SignalOrNameMatches` guard replaces raw `!=` | L2375-2403 confirmed: signature matches spec exactly, guard at L2384 confirmed |
| `SyncFollowerBracket` call site -- add `, leaderOrder.Name` as 4th arg | L2139 confirmed: `FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop, leaderOrder.Name)` |

**ATM bracket names coverage**: PASS
Stop1/Target1/Target2 (null `FromEntrySignal`, name fallback fires) -- confirmed by Tests 1 and 2.
Target3 (non-null `FromEntrySignal`, primary signal path) -- confirmed by Test 3 regression.
The ATM naming scheme uses Stop1/Stop2/Stop3 and Target1/Target2/Target3; the fix applies universally
to any order whose `FromEntrySignal` is null/empty, covering the full bracket name set via name
equality fallback (branch 3 of `SignalOrNameMatches`).

**SECTION A: PASS**

---

## Section B: Cross-file JS Violations

All checks performed on actual source of `src/PropTraderTools/CopyEngine.cs`:

| Check | Pattern / Evidence | Result |
|-------|-------------------|--------|
| JS-021 lock() ban | `grep "lock\s*\("` -- 9 comment-only hits (`// JS-021: no lock`); zero actual `lock(` calls in new or changed code | PASS |
| JS-001 throw ban | `grep "throw new"` -- 0 matches in entire file | PASS |
| JS-002 null return | `grep "return null"` -- single pre-existing terminus at L2402 (Order? contract). Zero new additions in B131 code. `Order?` nullable annotation explicit. | PASS |
| JS-033 async void ban | `grep "async void "` -- comment at L1567 only (`// JS-033: Tick is not async void`); zero actual `async void` method declarations in new code | PASS |
| New method return types | `SignalOrNameMatches` returns `bool`. `FindFollowerBracketOrder` returns `Order?` (pre-existing contract, explicit nullable annotation). `SignalOrNameMatchesTestable` returns `bool`. `FindFollowerBracketOrderTestable` returns `Order?`. | PASS |
| DateTime.Now | `grep "DateTime\.Now[^U]"` -- 0 matches | PASS |
| SCAN-05 CYC (all new/changed methods <= 8) | `SignalOrNameMatches` CYC=3, `FindFollowerBracketOrder` CYC=4 (per verifier, reviewer annotation #1 corrects plan's CYC=5), `SyncFollowerBracket` CYC=7 unchanged. All <= 8. | PASS |

**SECTION B: PASS**

---

## Section C: Spec Coverage

| DW-B138 requirement | Addressed? | Evidence |
|---------------------|-----------|----------|
| Stop1 drag -> follower bracket updated | YES | Test 1 (`B131_DW138_Stop1DragReachesHandleBracketChange`): null `FromEntrySignal` fallback to name match confirmed. VERIFY_PASS REQ-4. |
| Target1 drag -> follower bracket updated | YES | Test 2 (`B131_DW138_Target1DragReachesHandleBracketChange`): same null fallback on Limit order. VERIFY_PASS REQ-4. |
| Target2 drag -> follower bracket updated | YES | Fix is generic (any order with null `FromEntrySignal` + matching name). `FindFollowerBracketOrder` isStop=false path applies to all Target brackets. No Target2-specific test required (same code path as Target1). |
| Target3 regression -- still works | YES | Test 3 (`B131_DW138_Target3DragStillReachesHandleBracketChange`): matching `FromEntrySignal` path unbroken. VERIFY_PASS REQ-5. |
| "Buy STP" regression -- still works | YES | Test 4 (`B131_DW138_BuySTPDragStillRoutesCorrectly`): signal match wins (branch 1), name fallback not reached. VERIFY_PASS REQ-6. |
| All 7 scans zero (independently confirmed) | YES | VERIFY_PASS Layer 3: all 7 scans match L2 report exactly; no discrepancies. |

**SECTION C: PASS**

---

## Section D: Test Quality

| Check | Evidence | Result |
|-------|----------|--------|
| All 4 `[Fact]` tests present in B131Tests.cs | `grep "\[Fact\]"` in `B131Tests.cs` returns 9 matches. Lines 29, 48, 67, 87 are the 4 B138 tests (class `B131Tests`). Lines 111, 121, 131 are B139 placeholder tests in `B131LaneBTests` class. | PASS |
| No NUnit/MSTest | VERIFY_PASS REQ-10 confirmed: only `using Xunit;` import. No NUnit/MSTest references. | PASS |
| Tests are deterministic | All tests use fixed mock data (fixed `OrderType`, `OrderState`, `OrderName`, `FromEntrySignal` string). No `DateTime.Now`, no `Random`, no time dependencies. | PASS |
| xUnit [Fact] only (no [Theory] with random data) | All 4 tests are `[Fact]`. | PASS |
| Test coverage note (verifier non-blocking annotation) | Verifier noted tests exercise `SignalOrNameMatchesTestable` directly rather than `FindFollowerBracketOrderTestable`. All 4 branch paths of the new predicate covered. `FindFollowerBracketOrderTestable` is a zero-logic delegate. Accepted as non-blocking simplification. | PASS (non-blocking) |

**SECTION D: PASS**

---

## Section E: Non-Regression

| Method | Change status | Evidence |
|--------|--------------|---------|
| `DispatchCopy` (L1944) | Unchanged | No B131 tag in method. VERIFY_PASS REQ-8. |
| `TryCopyEntry` | Unchanged | Not present in new-code span. VERIFY_PASS REQ-8. |
| `IsAtmSTPOrder` (L2107-2113) | Unchanged | Source-verified: checks `EndsWith("STP")`, `StartsWith("Stop")`, `StartsWith("Target")` -- body matches ticket spec verbatim. No B131 tag. |
| `SyncAtmFollowerBracket` (L2202) | Unchanged | Not in new-code span. No B131 tag. |
| `SyncAtmFollowerTarget` (L2263) | Unchanged | Not in new-code span. No B131 tag. |
| `SyncFollowerBracket` (L2131) | Call site only (L2139) | Signature unchanged: `private void SyncFollowerBracket(Account, Order, bool, double, double)`. Only L2139 argument list extended. CYC=7 unchanged. |
| B129/B130 regression tests | 19 passed, 0 failed (completion report + verifier report) | PASS |
| Default parameter backward-compat | `string? leaderName = null` -- all prior callers without 4th argument continue to behave identically (C# default parameter rule). Single call site exists in codebase (`SyncFollowerBracket` L2139). | PASS |

**SECTION E: PASS**

---

## Section F: CYC Compliance

| Method | CYC Before | CYC After | Budget (<=8) | Status |
|--------|------------|-----------|--------------|--------|
| `SignalOrNameMatches` (new) | N/A | 3 | 8 | PASS |
| `FindFollowerBracketOrder` | 4 | 4 | 8 | PASS (guard substituted 1:1; ticket reviewer annotation #1 confirmed CYC=4 corrects plan's CYC=5 estimate) |
| `SyncFollowerBracket` | 7 | 7 | 8 | PASS (call site argument addition only, no new branches) |
| `SignalOrNameMatchesTestable` | N/A | 1 | 8 | PASS (one-liner expression delegate) |
| `FindFollowerBracketOrderTestable` | N/A | 1 | 8 | PASS (one-liner expression delegate) |

Note on CYC discrepancy: The architecture plan estimated `FindFollowerBracketOrder` after = CYC=5.
The ticket reviewer annotation #1 and independent verifier both confirm CYC=4 (old `FromEntrySignal !=`
guard replaced 1-for-1 by `!SignalOrNameMatches(...)` guard; branch count unchanged). Both 4 and 5 are
well within the JS budget of 8. This discrepancy was pre-acknowledged in the ticket review and is
non-blocking. The actual CYC is 4.

**SECTION F: PASS**

---

## Section K: Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-H3-B131 | H3 assessment: `OrderState.ChangeSubmitted` events fall through to `DispatchCopy` (noise path). `IsWorkingBracket` accepts only `Working`/`Accepted`. The subsequent `Working` event correctly reaches `HandleBracketChange`, so H3 is not a blocker for drag sync. However, the spurious `DispatchCopy` call on `ChangeSubmitted` is acknowledged technical debt. | P2 | future | OPEN |

No P0 or P1 items deferred. All DW-B138 requirements implemented and verified.
H3 is P2 technical debt (noise, not a correctness failure) -- deferred explicitly by the architecture
plan (Section A: "H3 is noted as technical debt but is out of scope for DW-B138").

---

*End of Final Review -- B131 LaneA DW-B138*
