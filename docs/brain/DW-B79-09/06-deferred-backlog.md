# DW-B79-09 — Deferred Backlog

**Block**: DW-B79 (sub-pipeline 09)
**Title**: RemoveAll race guard — CancelQxBrackets x2 + CancelStaleBracketsLocal
**Date**: 2026-08-21
**Author**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: NONE (first DW-B79 backlog file; DW-B79-03/06-deferred-backlog.md does not exist)

---

## New Deferred Items from DW-B79-09

DW-B79-09 is a P3 cosmetic uniformity pipeline. It introduced **no new technical debt**.

All items below are pre-existing conditions surfaced during the 7-scan process and carried
forward here for Director visibility and future block targeting.

---

## Deferred Work Register

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B79-09-01 | **Pre-existing `return null;` debt** — 30 occurrences across CopyEngine.cs (6), TradeCopierPanel.cs (6), TradeCopierWindow.cs (2), PttBreakEven.cs (2), PttFlatten.cs (2), PttTrim.cs (2), CopyEngineTests.cs (1), TradeCopierAddOn.cs (8), B45Tests.cs (1). JS-002 violations. Not caused by DW-B79-09. | P2 | future | OPEN |
| DW-B79-09-02 | **Pre-existing CSharpier violations** — 34 formatting issues across 37 files. Not caused by DW-B79-09. None at DW-B79-09 edit sites (CopyEngine.cs ~L630/L704, PttBreakEven.cs ~L193). | P2 | future | OPEN |
| DW-B79-09-03 | **AtrSizingEngine.cs build errors** — CS0234 (`NinjaTrader.NinjaScript.Indicators` does not exist) and CS0246 (`Indicator` not found) at AtrSizingEngine.cs:20 and :24. NT8 runtime-only types; suppressed in production via `<NoWarn>` in csproj. Pre-existing at HEAD 5925b618. | P1 | future | OPEN |
| DW-B79-09-04 | **Pre-existing Lizard CCN inflation** — Lizard reports CCN=14, 16, 16 on CancelQxBrackets(2-param), CancelQxBrackets(3-param), and CancelStaleBracketsLocal respectively. Caused by Lizard counting `||` in boolean assignment expressions. Roslyn CYC ≤ 8 confirmed for all three. No action required unless Lizard enforcement is activated. | P2 | future | OPEN |

---

## Carry-Forward from Prior DW-B79 Sub-Pipelines

None. No prior `06-deferred-backlog.md` exists in the DW-B79 block series. This is the
first backlog file. All items above originate from DW-B79-09 scan observations.

---

## PIPELINE_COMPLETE — DW-B79-09

| Gate | Status |
|------|--------|
| 3 source insertions implemented and verified | COMPLETE |
| 3 new `[Fact]` methods (+3 structural) | COMPLETE |
| All 7 scans pass | COMPLETE |
| No new JS violations | COMPLETE |
| `05-final-review.md` written (incl. Section K) | COMPLETE |
| `06-deferred-backlog.md` written | COMPLETE |
| `git commit` | COMPLETE (see Step 4) |
| `deploy-sync.ps1` | COMPLETE (see Step 5) |
| Director F5 GREEN | COMPLETE 2026-08-21 — compiled clean |

**DW-B79-09: PIPELINE_COMPLETE — F5 GREEN — ALL GATES CLOSED**
