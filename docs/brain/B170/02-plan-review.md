# B170 Plan Review — DW-B170-01 FOLLOWER-STOP-LOST-ON-TGT-DRAG

Reviewer: ptt-plan-reviewer
Plan under review: docs/brain/B170/02-architecture-plan.md
Source files spot-checked: src/PropTraderTools/CopyEngine.cs, src/PropTraderTools/CopyEngineTests.cs
RULES_CATALOG: docs/protocol/RULES_CATALOG.md — NOT FOUND (reviewed against hardcoded DNA block in role definition)

---

## Checklist Results

| # | Item | Result | Evidence |
|---|------|--------|----------|
| 1 | LANE-SPLIT GATE present and answered | **PASS** | Section 3 present; verdict SINGLE-PIPELINE; Q1–Q4 answered (N/A). |
| 2 | Zero-regression proof (Working + Accepted both in predicate) | **PASS** | Section 4 AFTER block contains `\|\| order.OrderState == OrderState.Accepted`; Section 8 includes companion regression test `FindLeaderStopPrice_WorkingStop_StillReturnsPrice`. |
| 3 | No `lock()` anywhere in plan or proposed change | **PASS** | Section 9 table row: "No lock() anywhere — PASS". No `lock` keyword appears anywhere in the plan. |
| 4 | ASCII-only in all changed lines | **PASS** | All characters in BEFORE/AFTER blocks and CYC comment update are 7-bit ASCII. No Unicode, curly quotes, or emoji. |
| 5 | CYC 5→6 correct; within budget ≤ 8 | **PASS** | Pre-change branches confirmed from source: null-account (1), zero-index (2), foreach (3), name-match (4), Working-state (5) = CYC 5. `\|\|` Accepted adds branch (6). CYC 6 ≤ 8. |
| 6 | `ptt-sync.ps1` listed as required post-edit step | **PASS** | Listed in Section 9 constraint table AND Section 10 step 3. |
| 7 | `FindLeaderStopPriceTestable` seam confirmed at stated line; no new seam needed | **PASS** | Source line 4835 confirmed: `internal static double FindLeaderStopPriceTestable(Account? leaderAccount, int bracketIndex)`. Seam already exists and delegates directly to `FindLeaderStopPrice`. No new seam required. |
| 8 | Line numbers sourced from actual source (not assumed) | **PASS** | See spot-check matrix below — all 5 stated line numbers match source exactly. |
| 9 | Test class and insertion point identified in CopyEngineTests.cs | **PASS** | Plan: `DwB170Tests` after line 10257 (class `}`), before line 10259 (namespace `}`). Source confirms: L10257 = `}` (class close of `DwB168_02Tests`), L10259 = `}` (namespace close), L10258 is blank. Insertion at 10258 is valid. |
| 10 | Files touched list complete (CopyEngine.cs + CopyEngineTests.cs only) | **PASS** | Section 7 table lists exactly two files. No other files mentioned. |

---

## Spot-Check Matrix — Line Numbers

| Item (plan-stated) | Plan Line | Source Actual | Match? |
|---------------------|-----------|---------------|--------|
| `FindLeaderStopPrice` method definition | 4412 | 4412 | ✓ |
| CYC comment (to be updated) | 4410 | 4410 | ✓ |
| Predicate `order.OrderState == OrderState.Working` | 4423 | 4423 | ✓ |
| `CreateFollowerReplacementStop` guard `if (stopPrice <= 0.0)` | 4445 | 4445 | ✓ |
| `ExecutePhaseCStopReplacement` call `FindLeaderStopPrice` | 4510 | 4510 | ✓ |
| `FindLeaderStopPriceTestable` seam definition | 4835 | 4835 | ✓ |

---

## Jane Street DNA Violations

None found.

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | `lock()` in proposed change | CLEAR |
| JS-021 (no Mutex/Monitor/SemaphoreSlim) | Plan proposes none | CLEAR |
| JS-023 (UI update from off-thread) | No UI touch in this plan | N/A |
| JS-001 (throw in gate chain) | No throw proposed | CLEAR |
| JS-002 (null return where value expected) | Returns 0.0 on all failure paths | CLEAR |
| JS-003 (magic string discriminated state) | `"Stop" + bracketIndex` is a name lookup, not a discriminated-state magic string | CLEAR |
| JS-009 (Dictionary for shared collection) | No dictionary added | CLEAR |
| JS-008 (mutable struct / SolidColorBrush) | No struct or brush touched | N/A |
| JS-010 (public constructor on singleton/signal struct) | No constructor added | N/A |
| CYC > 8 | CYC=6 post-change | CLEAR |
| async/await in OnInitialize/OnDestroyed/OnWindowCreated | No async proposed | N/A |
| Account.All in constructor | Not touched | N/A |
| sealed TradeCopierWindow | Not touched | N/A |
| FontFamily override (SCAN-03) | Not touched | N/A |
| Hardcoded #RRGGBB hex (SCAN-04) | No hex color in changed lines | CLEAR |
| CreateOrder without PTT- prefix (SCAN-05) | No CreateOrder in this fix | N/A |
| DateTime.Now (SCAN-06) | No DateTime usage in changed code | N/A |

---

## NT8 API Validity

No new NT8 API is introduced. `OrderState.Accepted` is a standard NinjaTrader 8 `OrderState` enum member. `leaderAccount.Orders.ToList()` (existing, unchanged) is a valid AddOn API pattern. No StrategyBase-only APIs (e.g. `AtmStrategyCreate`) are referenced.

---

## Spec Coverage

The plan addresses a single requirement: ATM bracket stops in `OrderState.Accepted` are not returned by `FindLeaderStopPrice`, causing the follower stop to be silently dropped on a target drag. The fix is minimal, targeted, and complete. No spec item identified in the plan is left unaddressed.

---

## Overall Verdict

**REVIEW_PASS**

All 10 checklist items pass. Zero Jane Street DNA violations. Line numbers verified against source. Regression safety confirmed by companion Working-state test. ptt-sync.ps1 included. CYC budget respected.

Plan is approved for Phase 3 ticket generation.
