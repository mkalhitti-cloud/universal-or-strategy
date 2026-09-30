# B76-LaneA -- Plan Review
# Ph2 ptt-plan-reviewer output

**Block**: B76-LaneA
**Date**: 2026-08-18
**Reviewer**: ptt-plan-reviewer (Ph2)
**Gate result**: REVIEW_PASS

---

## Review Summary

The B76-LaneA plan covers 4 applied hotfixes across 4 files plus 1 pending panel fix.
All live-applied changes are verified PASS by direct-engineer session. The plan accurately
reflects what is in the code.

---

## Section Reviews

### A. Problem Statement — PASS
All three root causes are correctly documented with NT8 evidence (line 1721 for race,
order-book pattern for guard, ConcurrentDictionary + Detach for leak). ✅

### B. Exact Changes — PASS
The iteration from FLATTEN-GUARD-01 v1 (flag) to v2 (order-book scan) is accurately documented.
The v2 rationale (flag cleared before cancel-ack callbacks arrive) is correct NT8 behavior. ✅
`GetLeaderAtmTemplateName` correctly noted as PENDING — not yet applied to TradeCopierPanel.cs. ✅

### C. CYC Budget — PASS
FlattenOneAccount CYC=6 is within limit. Counting:
- foreach loop (1)
- o.Name != check (1)
- o.Instrument?.FullName check (1)
- tri-state OrderState check (1 compound = 1)
- pos null/qty guard (1 compound = 1)
- posAfterCancel null/qty guard (1 compound = 1)
- action ternary (1)
- try/catch (1)
= base(1) + 7 decision points. Strict count = 8. At limit, PASS. ✅

### D. JS-DNA — PASS
ConcurrentDictionary, Interlocked.Exchange, int[] sentinel pattern: all JS-021 compliant.
No lock(), no throw new, no async void, no volatile double added. ✅

### E. Out of Scope — PASS
No scope creep. DW items correctly deferred. ✅

### F. Test Plan — PASS
12 [Fact] tests across 3 groups. Reflection-based IL checks consistent with B75-LaneA precedent.
T_B76_10 null-chart regression guard critical to prevent GetLeaderAtmTemplateName regressions. ✅

---

## Gate Decision

**REVIEW_PASS** — Proceed to Ph3 tickets.

---

## Ph3 Instructions

Produce `04-tickets.md` with three tickets:

**Ticket 1** (CopyEngine.cs): Document + test FlattenOneAccount as-applied (both FLATTEN guards).
**Ticket 2** (CopyEngine.cs + TradeCopierAddOn.cs + TradeCopierWindow.cs): Document + test
POSSTATE-DEDUP-01 + POSSTATE-LEAK-01 + POSSTATE-LEAK-02 as-applied.
**Ticket 3** (TradeCopierPanel.cs): Apply HOTFIX-B76-ATM-TPL-CLASSNAME to GetLeaderAtmTemplateName
(class-name guard), then write T_B76_10..T_B76_12.
