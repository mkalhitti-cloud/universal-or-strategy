# B131 LaneA Plan Review
## DW-B138 — ATM Bracket Drag Not Reaching SyncFollowerBracket for Stop1/T1/T2

**Status**: REVIEW_PASS
**Reviewer**: ptt-plan-reviewer
**Plan reviewed**: docs/brain/B131/LaneA-02-architecture-plan.md
**Date**: 2026-08-31

---

## REVIEW SUMMARY

**Overall verdict: REVIEW_PASS**

All 8 gates pass. No Jane Street rule violations. No NT8 API misuse. No spec gaps.
One non-blocking annotation note (Gate 3: CYC accounting discrepancy — both values are well within the ≤8 budget).

---

## Gate Results

| Gate | Result | Notes |
|------|--------|-------|
| GATE 1 — Root Cause Validity | **PASS** | All line numbers independently confirmed against actual source. Mechanism correct. T3 asymmetry logically consistent with code. H3 assessment correct. |
| GATE 2 — Fix Strategy Correctness | **PASS** | FIX-A correctly addresses null-FromEntrySignal case. Single call site (L2139) confirmed by grep. All ATM slot names covered. DispatchCopy/TryCopyEntry untouched. |
| GATE 3 — Method Signatures & CYC | **PASS** | Before/after signatures shown for all changed methods. All CYC values ≤8. Non-blocking note: plan claims FindFollowerBracketOrder AFTER CYC=5; actual value is CYC=4 (substitution of 1 branch for 1 branch from BEFORE CYC=4 confirmed at L2336). Not a violation — both ≤8. |
| GATE 4 — Non-Regression Scope | **PASS** | "Buy STP"/"Sell STP" path safe (signal match fires first; Name fallback cannot collide). Entry-order copy path untouched. 4 B129/B130 regression tests listed. Default parameter ensures backward compat. |
| GATE 5 — Test Specifications | **PASS** | All 4 [Fact] tests specified with mock setup. xUnit only. File path correct. Testability pattern (internal + InternalsVisibleTo) acknowledged. |
| GATE 6 — Jane Street Rules | **PASS** | JS-021: no lock. JS-001: no throw. JS-002: Order? return type makes null contract explicit. ASCII-only confirmed in all code blocks. |
| GATE 7 — 7-Scan Checklist | **PASS** | All 7 scans present in Section H with exact grep commands. SCAN-03 correctly notes existing return null as pre-existing — no new additions. |
| GATE 8 — Spec Completeness | **PASS** | All changes trace to DW-B138. All defect brief requirements addressed. Root cause is upstream of IsAtmSTPOrder as required. |

---

## Violations

**None.**

No blocking violations found. One non-blocking annotation note (see Gate 3 above).

---

## Confirmed Facts
(independently verified against actual CopyEngine.cs source)

- **L2315**: `HandleBracketChange(Order leaderOrder, CopyRule rule)` — signature confirmed. Loops `rule.FollowerAccounts` and calls `SyncFollowerBracket(acc, leaderOrder, isStop, newPrice, tickSize)`.
- **L2339**: `FindFollowerBracketOrder(Account follower, string fromEntrySignalName, bool isStop)` — signature confirmed. Comment at L2336 states `CYC=4`.
- **L2345**: `foreach (var order in follower.Orders.ToList())` — confirmed first branch point.
- **L2347**: `if (order.FromEntrySignal != fromEntrySignalName)` — confirmed as the exact failure line cited in the plan.
- **L2349**: `if (order.OrderState != OrderState.Working)` — confirmed.
- **L2365**: `return null;` — confirmed existing null return (pre-existing, not a new addition).
- **L2139**: `var fo = FindFollowerBracketOrder(acc, leaderOrder.FromEntrySignal, isStop);` — confirmed single call site. Grep returns exactly 2 hits (L2139 call + L2339 definition).
- **L2140**: `if (fo == null) return;` — confirmed early-exit before IsAtmSTPOrder is reached.
- **L2083-2089**: `IsWorkingBracket` accepts `OrderState.Working || OrderState.Accepted` AND `IsBracketLegStatic`. Confirmed `ChangeSubmitted` fails this gate.
- **L2107-2113**: `IsAtmSTPOrder` checks `EndsWith("STP")`, `StartsWith("Stop")`, `StartsWith("Target")` — confirmed exact implementation. "Stop1" → true. "Target1" → true. "Buy STP" → true (EndsWith "STP").
- **L2127**: `SyncFollowerBracket` CYC=7 — confirmed in comment.
- **FindFollowerBracketOrder call site count**: grep confirms exactly 1 call site (L2139). Plan's claim of single caller is accurate.
- **CYC accounting note**: BEFORE CYC=4 is stated in L2336 comment. Plan claims AFTER CYC=5. The substitution replaces one branch (FromEntrySignal check) with one branch (SignalOrNameMatches gate), leaving all other branches unchanged. Actual AFTER CYC = 4, not 5. This discrepancy is non-blocking (both ≤8).
- **IsAtmSTPOrder("Buy STP")**: Returns `true` (EndsWith "STP"). Test 4 correctly tests that the `"Buy STP"` follower is found via signal match (not Name fallback), confirming no false-positive Name collision between "Buy STP" leader-name "Stop1".
