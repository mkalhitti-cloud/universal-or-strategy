# Deferred Backlog: BWAVE-CYC LaneC-PR38-repair

**Block**: BWAVE-CYC-LaneC-PR38-repair  
**Date**: 2026-08-10  
**Author**: ptt-plan-reviewer (Phase 5 Final Review)  
**Branch**: feature/bwave-cyc-lane-c2 @ 737805b4  

---

## Current Block Entries

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-C38-01 | PRE-EXISTING `TryAdd(chart, null)` at `TradeCopierAddOn.cs:475` — ConcurrentDictionary null-slot reservation pattern introduced B10-EXEC. Consider `Lazy<T>` or sentinel value pattern in future wave. The C-3 null guard (`&& panel != null` in `OnWindowDestroyed`) mitigates the NRE risk introduced by this pattern, but the underlying slot-reservation idiom could be replaced with a cleaner design. | P2 / Low | future | OPEN |
| DW-C38-02 | Cubic P2 — `TradeCopierWindow.cs:508` — `BuildRuleRow` / `BuildDynamicRuleRow` share approximately 230 lines each with significant overlap but are not fully extracted into shared helpers. Restore shared extraction helpers (`BuildGridColumnDefinitions`, `BuildFollowerListBox`, `BuildAtmColumnPanel`) in a future targeted wave to reduce duplication and lower CCN in those two methods. | P1 / Medium | future | OPEN |
| DW-C38-03 | CodeAnt Major — `TradeCopierPanel.cs:614` — Detaching one panel disarms the shared pending BE slot for all accounts. Needs deeper investigation of BE slot scoping per chart/account. If slot state is truly global, multi-chart setups may experience behavioral regression when any single panel is detached. Requires investigation and potential scoping fix before high-frequency multi-chart use. | P1 / High | B5 or B6 | OPEN |
| DW-C38-04 | Cubic P3 — `TradeCopierWindow.cs:600` — ATM selector tabs appear before Apply/BE controls in the visual tree due to `grid.Children.Add` ordering. This causes keyboard navigation regression: Tab order follows visual tree insertion order, so tabbing through the panel does not follow expected left-to-right, top-to-bottom flow. Low priority UX debt; no data correctness impact. | P2 / Low | future | OPEN |

---

## Notes

- DW-C38-01: The C-3 null guard is in place and the NRE risk is mitigated. This item is logged for future architectural cleanup, not as a current defect.
- DW-C38-02: Complexity debt only. Both methods build within ≤ 8 CCN threshold post-extraction. This item tracks residual duplication, not a DNA violation.
- DW-C38-03: Flagged as High priority because multi-chart users may encounter unexpected BE disarm behavior. Director should assess whether current traffic warrants immediate fix or can wait for B5/B6.
- DW-C38-04: Tab order UX issue confirmed by CodeAnt tooling. No runtime error; keyboard users may need manual workaround until addressed.

---

## Previously OPEN Items from Prior Blocks

None — this is the first deferred backlog written for this repair lane.

---

*Written by ptt-plan-reviewer Phase 5 — 2026-08-10*  
*FINAL_PASS gate confirmed. 06-deferred-backlog.md existence verified.*
