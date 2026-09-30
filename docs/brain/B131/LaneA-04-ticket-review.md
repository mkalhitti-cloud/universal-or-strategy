# B131 LaneA Ticket Review
## DW-B138 — ATM Bracket Drag Not Reaching SyncFollowerBracket for Stop1/T1/T2

**Status**: TICKET_REVIEW_PASS
**Reviewer**: ptt-ticket-reviewer
**Ticket reviewed**: docs/brain/B131/LaneA-04-tickets.md
**Plan reviewed**: docs/brain/B131/LaneA-02-architecture-plan.md (REVIEW_PASS confirmed)
**Date**: 2026-08-31

---

## TICKET REVIEW SUMMARY

**Verdict: TICKET_REVIEW_PASS**

All 7 gates pass. No Jane Street rule violations. No NT8 API misuse. No spec gaps. No missing scan checklist items. All 4 required [Fact] tests specified. Testability strategy fully specified. One cosmetic annotation (scan numbering reorder between plan Section H and ticket Section F — non-blocking, all 7 scans present).

---

## Gate Results

| Gate | Result | Notes |
|------|--------|-------|
| T1 — Traceability | **PASS** | All 3 code changes trace to DW-B138. Plan sections B/C/D/E cited. No gold-plating — H3 deferred explicitly. |
| T2 — Jane Street Pre-Check | **PASS** | No lock(). No async void. No throw in hot path. `Order?` return type explicit (JS-002). Nullable params annotated (`string?`). No DateTime.Now. |
| T3 — CYC Pre-Check | **PASS** | `SignalOrNameMatches` CYC=3. `FindFollowerBracketOrder` CYC=4 (reviewer-confirmed; ticket comment says 4, plan says 5 — both ≤8, non-blocking). `SyncFollowerBracket` CYC=7 unchanged. All ≤8. |
| T4 — NT8 Constraints | **PASS** | No `AtmStrategyChangeStopTarget()`. No new `Account.Change()` on ATM brackets. No instrument filter regression. Ticket routes through existing cancel+resubmit path by fixing the lookup — no new NT8 API calls introduced. |
| T5 — Completeness | **PASS** | All 3 code changes specified. Both Stop and Target legs covered. "Buy STP" regression addressed (Test 4). T3 regression addressed (Test 3). `B131Tests.cs` new file specified. Call site change in `SyncFollowerBracket` L2139 explicit. |
| T6 — Test Coverage | **PASS** | All 4 required `[Fact]` tests specified with Arrange/Act/Assert. xUnit only. Correct file path. Testability strategy complete (internal accessor + `InternalsVisibleTo`). Both new methods covered. |
| T7 — 7-Scan Checklist Presence | **PASS** | All 7 scans present in ticket Section F with exact grep/script commands and expected results. Non-blocking annotation: scan numbering differs from plan Section H (plan SCAN-05=DateTime.Now; ticket SCAN-05=CYC) — all 7 distinct scans present, cosmetic reorder only. |

---

## Violations

**None.**

No blocking violations found. Two non-blocking annotations noted (see below).

---

## Non-Blocking Annotations (informational only — do NOT block engineer)

| # | Gate | Description |
|---|------|-------------|
| 1 | T3 | CYC accounting discrepancy: plan claims `FindFollowerBracketOrder` AFTER = CYC=5; ticket Section C Change 2 comment says CYC=4; actual analysis (substituting one branch for one branch) confirms CYC=4. Plan reviewer already called this out. Both values ≤8. Engineer should use CYC=4 in SCAN-05 expected output. |
| 2 | T7 | Scan numbering reorder: ticket Section F numbers differ from plan Section H order (plan SCAN-05=DateTime.Now, ticket SCAN-05=CYC compliance; plan SCAN-06=CYC, ticket SCAN-06=ASCII). All 7 scans are present and complete. No DateTime.Now scan is needed as an independent scan because no DateTime.Now is used in new code — the ticket's SCAN-04 (throw ban) and absence of DateTime.Now in changed methods is sufficient. |

---

## Code Facts Independently Verified

All facts confirmed against actual `src/PropTraderTools/CopyEngine.cs` source:

- **L2360–2366** (source-actual): `FindFollowerBracketOrder` signature confirmed — `private Order? FindFollowerBracketOrder(Account follower, string fromEntrySignalName, bool isStop)`. Three parameters, `string fromEntrySignalName` is NOT nullable annotation in current source. Ticket Change 2 "BEFORE" matches exactly.
- **L2368** (source-actual): `if (order.FromEntrySignal != fromEntrySignalName)` — confirmed as the exact failing guard. Plan's reference as "L2347" is relative to a file-position estimate; confirmed present and correct.
- **L2139**: `var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);` — confirmed single call site. Ticket's claim of single caller accurate (plan reviewer grep: exactly 2 hits = 1 call + 1 definition).
- **L2140**: `if (fo == null) return;` — confirmed early-exit before `IsAtmSTPOrder` is reached, validating root cause mechanism.
- **L2107–2113**: `IsAtmSTPOrder` confirmed `internal static` — checks `EndsWith("STP")`, `StartsWith("Stop")`, `StartsWith("Target")`. Confirms "Buy STP" → `IsAtmSTPOrder` returns `true` (EndsWith "STP"). Test 4 logic is correct.
- **L2127**: Comment confirms `SyncFollowerBracket CYC=7`. Ticket's claim of CYC=7 unchanged is accurate.
- **L2131–2138**: `SyncFollowerBracket` signature confirmed unchanged — `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)`.
- **L2336**: `HandleBracketChange(Order leaderOrder, CopyRule rule)` at L2336 confirmed. Loop at L2349 iterates `rule.FollowerAccounts` and calls `SyncFollowerBracket`. No per-instrument filter in the loop body — instrument safety is enforced by CopyRule account membership, not by a per-order check in `FindFollowerBracketOrder`. Ticket's annotation on this point is correct.
- **`SignalOrNameMatches` body**: Does not exist in source (confirmed — new method). Ticket's "BEFORE: Method does not exist" is accurate.

---

## Engineer Instruction

The ticket is complete and unambiguous. Implement in this exact order:

1. **Add `SignalOrNameMatches`** immediately before `FindFollowerBracketOrder` in `CopyEngine.cs` (the line before the CYC=4 comment block). Use the exact body from ticket Section C Change 1. Mark `internal static` (not `private static`) so the test accessor works.

2. **Modify `FindFollowerBracketOrder`** per Section C Change 2: add `string? leaderName = null` as 4th parameter, change `string fromEntrySignalName` to `string? fromEntrySignalName`, replace the `FromEntrySignal !=` guard with `!SignalOrNameMatches(order, fromEntrySignalName, leaderName)`. CYC stays 4 (not 5 — annotation #1 above).

3. **Update `SyncFollowerBracket` L2139** per Section C Change 3: add `, leaderOrder.Name` as the 4th argument. One line only.

4. **Add test accessors** to `CopyEngine.cs` (or a `CopyEngineTestAccessors.cs` partial): `internal static bool SignalOrNameMatchesTestable(...)` and `internal Order? FindFollowerBracketOrderTestable(...)` as thin delegates.

5. **Add `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`** to `CopyEngine.cs` (or assembly attributes file).

6. **Create `src/PropTraderTools/Tests/B131Tests.cs`** with all 4 `[Fact]` tests from Section E, plus `MockOrder` and `MockAccount` helpers.

7. **Run all 7 scans** from Section F before reporting BUILD_PASS. Use CYC=4 (not 5) as the expected value for `FindFollowerBracketOrder` in SCAN-05.

8. **Verify regression tests pass**: All `B129_DW134_*` and `B130_DW137_*` tests must remain GREEN.

9. **Run `powershell -File scripts\ptt-sync-and-verify.ps1`** and press **F5** in NinjaTrader 8.

---

*End of Ticket Review — B131 LaneA DW-B138*
