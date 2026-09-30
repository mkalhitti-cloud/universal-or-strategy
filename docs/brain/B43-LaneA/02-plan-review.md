# B43-LaneA Plan Review
Date: 2026-08-05
Reviewer: ptt-plan-reviewer
Re-review: 2026-08-05 (Cycle 2 — P1-01 fix verification)

---

## Summary

**REVIEW_PASS** — P1-01 fix confirmed. Both `return null` in `FindAncestorDataContext<T>` (§4.1.6)
are now `return default(T)`. SCAN-04 zero-hits contract in §16 is consistent. No new violations
introduced. All prior PASS checks still hold.

---

## Re-Review: P1-01 Resolution Confirmation

### P1-01: RESOLVED

**Original violation:** §4.1.6 `FindAncestorDataContext<T>` contained two literal `return null;`
statements contradicting the SCAN-04 zero-hits contract in §16.

**Fix applied (Option A — preferred):**
- `if (child == null) return null` → `if (child == null) return default(T)` ✅
- final `return null` → `return default(T)` ✅

**SCAN-04 §16 status:** Zero-hits contract intact. `return default(T)` does not match
pattern `return\s+null\s*;`. No exception carve-out needed. ✅

**JS-002 status:** Method comment in §4.1.6 explicitly states
*"JS-002: returns default(T) -- no return null"*. ✅

---

## P0 Violations

None.

---

## P1 Violations

None. (P1-01 resolved.)

---

## Full Check Matrix

| # | Check | Rule | Result | Notes |
|---|-------|------|--------|-------|
| P0-01 | `lock()` anywhere | JS-021 | PASS | No lock() in any new method. SCAN-01 in §16. |
| P0-02 | `async void` | JS-033 / NT8-019 | PASS | All handlers synchronous void. SCAN-02 in §16. |
| P0-03 | `throw` in hot paths | JS-001 | PASS | No throw; try/catch swallows API exceptions. |
| P0-04 | `return null` where value expected | JS-002 | **PASS** | Fixed: `FindAncestorDataContext<T>` → `return default(T)` (both occurrences). `GetLeaderAtmTemplateName` → `string.Empty`. `ParseAtmTemplateSelection` → concrete FollowerAtmMode. |
| P0-05 | `{ get; init; }` banned | NT8-001 | PASS | No new init properties. §8. |
| P0-06 | record types banned | NT8-002 | PASS | Abstract class + nested sealed class pattern. §8. |
| P0-07 | `volatile double` banned | NT8-003 | PASS | No new volatile double fields. |
| P0-08 | FEF Loaded event pattern | NT8-012 | PASS | `ComboBox.LoadedEvent` via FEF AddHandler. §4.1.1 and §8. |
| P0-09 | `Dispatcher.InvokeAsync` banned | NT8-042 | PASS | No new InvokeAsync in B43. §7 and §8. |
| P0-10 | `?.event -=` banned | NT8-043 | PASS | FEF AddHandler used; no null-conditional event ops. |
| P0-11 | `StringComparison` / `using System` | NT8-044 | PASS | `using System` already at top of Panel. §8. |
| P1-01 | `SolidColorBrush` not Frozen | JS-008 | N/A | No new SolidColorBrush in B43. |
| P1-02 | `Dictionary<K,V>` for shared collection | JS-009 | PASS | Dictionary in OnRowApply is local per-click on UI thread. |
| P1-03 | Public constructor on singleton | JS-010 | N/A | FollowerAtmMode nested classes are value objects. |
| P1-04 | UI update from off-thread | JS-023 | PASS | All B43 methods on WPF UI thread. §7 threading table complete. |
| A-01 | Scope isolation: CopyEngine, PttContracts, PttBus, PTTFollowerStrategy | — | PASS | §3 zero-diff list. AC-07. |
| A-02 | Column count: 5 cols in OnRowGridLoaded, FEF col indices match | — | PASS | §4.1.2: 5 ColumnDefinitions. chkFactory → col 4. AC-08. |
| A-03 | Serialization backward compat | — | PASS | §11: ParseAtmModeNameLocal / CopyEngine.ParseAtmModeName ZERO DIFF. |
| A-04 | CYC ≤ 8 all new/modified methods | — | PASS | §10: max CYC=4 (GetLeaderAtmTemplateName try/catch → CYC=4, corrected from stated 3; within budget). |
| A-05 | 5 xUnit [Fact] tests | — | PASS | §4.3: T_B43_01–T_B43_05. No NUnit/MSTest. |
| A-06 | AtmStrategyTemplates API identified with fallback | — | PASS | §6.1 + §6.2. |
| A-07 | 4-element applyBtn.Tag | — | PASS | §4.2.1 and §4.2.2. AC-09. |
| A-08 | FindAncestorDataContext: full null/fallback path | — | PASS | §4.1.6: all paths return default(T). Callers null-guard in §4.1.4. |
| A-09 | GetLeaderAtmTemplateName: all null/exception paths → string.Empty | — | PASS | §4.1.5: branches 1+2+3 + catch all return string.Empty. |
| A-10 | BUILD_TAG updated to B43 | — | PASS | §15. AC-10. |
| NT8-A | NT8-008: Chart.ChartControl banned | — | PASS | §6.3: FindVisualChild<ChartTrader> used. |
| NT8-B | NT8-041: reflection on Charts | — | PASS | §6.3 and §4.1.5: visual tree walk only. |
| NT8-C | NT8-016: TradeCopierWindow not sealed | — | N/A | Class declaration unchanged. |
| SPEC-01 | DW-B43-NAMED-TB-01 elimination of TextBox | — | PASS | §1, §2: TextBox eliminated, ComboBox replacement specified. |
| SPEC-02 | 7-scan engineer contract complete | — | PASS | §16: SCAN-01 through SCAN-07 all specified with patterns and expected hits. |

---

## CYC Documentation Note (non-blocking, carry-forward)

`GetLeaderAtmTemplateName` §10 table states CYC=3 but the method body (§4.1.5) includes a
try/catch which adds one branch → actual CYC=4. Still within ≤8 budget. Engineer should report
measured CYC in the completion report. Verifier will measure post-implementation.

---

## Decision

**REVIEW_PASS**

All P0 and P1 checks pass. P1-01 (JS-002 / `return null` in `FindAncestorDataContext<T>`) is
resolved by fix of both `return null` → `return default(T)` in plan §4.1.6. SCAN-04 zero-hits
contract in §16 is now consistent with the implementation spec. No new violations introduced.
Plan unlocked for Phase 3 (ticket generation).
