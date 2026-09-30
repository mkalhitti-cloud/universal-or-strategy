# PTT-COPIER-B19 — Plan Review
# Phase: 2 (Plan Review)
# Reviewer: ptt-plan-reviewer
# Cycle: 2 (Cycle 1 violation resolved — NT8-032 now registered)
# Plan file: docs/brain/PTT-COPIER-B19/02-architecture-plan.md
# Rules read: docs/standards/jane-street/RULES_CATALOG.md (§§ JS-001, JS-002, JS-003, JS-008, JS-009, JS-010, JS-021, JS-023)
# NT8 rules read: docs/standards/NT8_COMPILER_RULES.md (Version 1.2 — NT8-032 confirmed present)
# Date: 2026-07-07

---

## VERDICT: REVIEW_PASS

No violations found. All 14 checklist items pass. Plan is cleared for Phase 3 (ticket generation).

---

## Cycle History

| Cycle | Verdict | Violation |
|-------|---------|-----------|
| 1 | REVIEW_FAIL | NT8-032 not yet registered in NT8_COMPILER_RULES.md; `MarketData.Ask/.Bid` null-guard pattern unverifiable without catalog entry |
| **2** | **REVIEW_PASS** | NT8-032 registered (Version 1.2, B12/B19); all checks pass |

---

## NT8-032 Registration Verification

**Status:** CONFIRMED PRESENT  
NT8_COMPILER_RULES.md Version 1.2 contains:

> NT8-032 | P1 | `MarketData.Ask` / `MarketData.Bid` / `MarketData.Last` ARE `MarketDataEventArgs` — USE `.Price`  
> CONFIRMED: B12 (B19 documentation pass)  
> SCAN: `MarketData\.(Ask|Bid|Last)[^.]` — catches missing .Price

Plan §8 compliance table cites NT8-032 explicitly:
> NT8-032: `GetAsk` uses `md.Ask.Price`; `GetBid` uses `md.Bid.Price`; pattern confirmed in CopyEngine.cs:1179-1180

Cycle 1 blocker: **RESOLVED.**

---

## 14-Point Checklist Results

| # | Rule | Description | Plan Location | Result |
|---|------|-------------|---------------|--------|
| 1 | JS-021 | No `lock()` anywhere | §8 compliance table; no `lock(` in any code block | **PASS** |
| 2 | JS-023 | UI update from off-thread requires Dispatcher.InvokeAsync | All changed methods are UI-thread handlers (click/keyboard); no new off-thread paths | **PASS** |
| 3 | JS-001 | No `throw new XxxException` in business logic | `try/catch` catches and relays via StatusUpdate; no rethrow | **PASS** |
| 4 | JS-002 | No `return null` where value expected | GetAsk/GetBid return `0.0` on all null guards | **PASS** |
| 5 | JS-003 | No magic string for discriminated state | Signal names are NT8-014 identifiers, not state discriminators | **PASS** |
| 6 | JS-009 | No `Dictionary<K,V>` for shared/thread-touched collection | No new collections introduced | **PASS** |
| 7 | JS-008 | No mutable struct fields; SolidColorBrush must be Freeze()d | No new structs or brushes | **PASS** |
| 8 | JS-010 | No public constructor on singleton or signal struct | No new types | **PASS** |
| 9 | CYC ≤ 8 | All methods ≤ CYC 8 | §7 table: GetAsk=4, GetBid=4, OnTrimClick=4, OnFlattenClick=4, Trim 4-arg=7, Flatten 4-arg=7 | **PASS** |
| 10 | NT8-async | No async/await in OnInitialize/OnDestroyed/OnWindowCreated | None introduced | **PASS** |
| 11 | NT8-016 | TradeCopierWindow not sealed | Not touched by B19 | **PASS** |
| 12 | NT8-014/SCAN-05 | CreateOrder signal names start with `"PTT-"` | `"PTT-TrimLimit"` §4.1; `"PTT-FlattenLimit"` §4.2 | **PASS** |
| 13 | NT8-013/SCAN-06 | `DateTime.MaxValue` in CreateOrder | Both Trim and Flatten use `DateTime.MaxValue` | **PASS** |
| 14 | NT8-032 | MarketData.Ask/.Bid use `.Price`; full null-guard chain | GetAsk: `md.Ask.Price` with 3 null guards §5.2; GetBid: `md.Bid.Price` with 3 null guards §5.3 | **PASS** |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| REQ-1: Long exits anchor on Ask (not Last) | YES | §4.1 Trim — `refPrice = isLong ? ask : bid` |
| REQ-2: Short exits anchor on Bid (not Last) | YES | §4.1 Trim, §4.2 Flatten |
| REQ-3: Remove `GetRefPrice()` | YES | §5.1 — explicit removal with grep-zero post-check |
| REQ-4: Add `GetAsk()` helper | YES | §5.2 — CYC=4, full null-guard chain |
| REQ-5: Add `GetBid()` helper | YES | §5.3 — CYC=4, full null-guard chain |
| REQ-6: Update `OnTrimClick` call site | YES | §5.4 |
| REQ-7: Update `OnFlattenClick` call site | YES | §5.5 |
| REQ-8: Update `DispatchShortcut` Key.T / Key.F | YES | §5.6 |
| REQ-9: Update B12 reflection tests (3-arg → 4-arg) | YES | §6.1 — 5 tests listed with old/new type arrays |
| REQ-10: Add 5 new [Fact] tests | YES | §6.2 — TrimLimit_Long, TrimLimit_Short, FlattenLimit_Long, FlattenLimit_Short, TrimLimit_FallsBackToMarket |
| REQ-11: All CYC ≤ 8 | YES | §7 |
| REQ-12: NT8-007/NT8-013/NT8-014/NT8-032 compliance | YES | §8 compliance table |

All 12 requirements covered. No gaps.

---

## Advisory Notes (Non-Blocking)

### ADVISORY-1: CYC Label Inconsistency in Method Header Comments

**Location:** §4.1 Trim method header comment and §4.2 Flatten method header comment  
**Observation:** The inline comment labels CYC=7 and lists `(6) try, (7) catch` as decision items. The compliance table in §7 states: *"try/catch is not counted as a branch (consistent with project practice)."*

These two statements are contradictory. If try/catch are excluded (per §7 note), the method CYC is 5 under compound-predicate counting or 7 under strict McCabe with each `||` counted. Either way the value is ≤ 8.

**Impact:** None on correctness or rule compliance. The method bodies are well within limit.  
**Recommendation:** Engineer should reconcile the header comment with §7 during implementation (e.g., correct to `CYC=5` if using compound-predicate counting without try/catch, or document the counting methodology clearly in the comment).  
**This does not block REVIEW_PASS.**

---

## Gate Decision

**REVIEW_PASS — Cycle 2.**

The Cycle 1 blocking violation (NT8-032 unregistered) is resolved. The plan correctly:
- Removes `GetRefPrice()` and replaces with `GetAsk()` + `GetBid()` using the NT8-032-compliant null-guard chain
- Anchors long exits to Ask and short exits to Bid
- Maintains CYC ≤ 8 across all changed methods
- Complies with JS-001, JS-002, JS-021, NT8-007, NT8-013, NT8-014, NT8-032
- Covers all spec requirements
- Defers line-659 account reference equality bug as DW-B19-02 (appropriate scope management)

Phase 3 (ticket generation by ptt-architect) is **UNLOCKED**.
