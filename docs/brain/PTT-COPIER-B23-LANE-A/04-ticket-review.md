# PTT-COPIER-B23-LANE-A — Ticket Review
# Block: PTT-COPIER-B23 | Lane: A | Phase: Ticket Review
# Reviewer: ptt-ticket-reviewer
# Date: 2026-07-16

---

## Checklist

C1 PASS: "Replace with" block (ticket lines 74–105) wraps `follower.CreateOrder(...)` as the
  lambda body of `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(() => follower.CreateOrder(...))`.
  The CreateOrder call is the sole body of the lambda. Structurally correct.

C2 PASS: `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher` is used at the single call site
  (ticket line 82). The Constraints section (ticket lines 108–109) explicitly bans
  `Application.Current.Dispatcher` and `System.Windows.Threading.Dispatcher.CurrentDispatcher`.
  SCAN-05 (ticket line 193) independently verifies the correct pattern at build time.
  No inconsistency between ticket sections.

C3 PASS: No `await` keyword appears before the `InvokeAsync` call in the "Replace with" block.
  Ticket line 82 reads `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(() =>` —
  no `await` prefix. Ticket line 112 explicitly states "do NOT `await` it (fire-and-forget)".

C4 PASS: `[Fact]` method `SendCopy_CompletesWithoutThrow_WhenDispatcherNotAvailable` is
  specified (ticket lines 120 and 130). Method signature, purpose, body, and append location
  are all provided.

C5 PASS: The new test uses `[Fact]` attribute (xUnit) at ticket line 129. No `[Test]` (NUnit)
  or `[TestMethod]` (MSTest) appears anywhere in the test specification.
  SCAN-07 (ticket line 202) further enforces this at build time.

C6 PASS: All 7 scans present with PowerShell command and expected result:
  SCAN-01 (JS-021 lock): 0 new matches.
  SCAN-02 (JS-033 async void): 0 matches.
  SCAN-03 (JS-002 return null): no new return null.
  SCAN-04 (NT8-003 volatile double): 0 matches.
  SCAN-05 (correct dispatcher pattern): 1 match in SendCopy.
  SCAN-06 (CYC SendCopy <= 8): manual inspection, expected CYC=5.
  SCAN-07 (no NUnit/MSTest): 0 matches.
  None missing.

C7 PASS: All 7 success criteria (ticket lines 213–219) are specific and unambiguous.
  SC#2: "SCAN-05 returns 1 match" (countable). SC#5: "[Fact] count = 123" with exact
  measurement command. SC#6: "All 7 scans pass (0 violations)". SC#7: "0 errors".
  SC#1, #3, #4: "Read file — [pattern] present/absent" (binary, unambiguous).

C8 PASS: Defect ID `DW-B22-NULLREF-01` appears in the ticket header (line 4) and in the
  Spec Requirement Satisfied section (ticket line 25). The architecture plan
  (`02-architecture-plan.md`) is cited as the source plan (ticket line 12) and carries the
  same defect anchor. Full trace: ticket → DW-B22-NULLREF-01 → REVIEW_PASS plan.

C9 PASS: Write-set is limited to `CopyEngine.cs` and `CopyEngineTests.cs` (ticket lines 32–33).
  Explicit DO NOT TOUCH list (ticket lines 35–36) covers `TradeCopierPanel.cs`,
  `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, `AtrSizingEngine.cs`, and all `.md` files.

C10 PASS: Preamble (ticket lines 16–17) states baseline=122, target=123, net +1. Success
  Criterion #5 (ticket line 217) confirms "123" with a verification command. Both are explicit
  and consistent.

---

## Verdict

TICKET_REVIEW_PASS
