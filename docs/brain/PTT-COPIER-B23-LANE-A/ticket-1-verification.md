# PTT-COPIER-B23-LANE-A — Ticket 1 Verification
# Verifier: ptt-verifier
# Date: 2026-07-16

## Defect
DW-B22-NULLREF-01 (P0) — wrap `follower.CreateOrder()` in `GeneralOptions.Dispatcher.InvokeAsync`
(fire-and-forget) to prevent NullReferenceException on non-active-chart follower accounts.

---

## Independent Scan Results

SCAN-01 lock():
  Result: 5 comment-only matches (all in `// no lock (JS-021)` / `// try block(0)` comments).
  0 executable lock() statements. PASS.

SCAN-02 async void:
  Result: 1 comment-only match (`// no await, no async void (JS-033 compliant)` at CopyEngine.cs:754).
  0 executable async void. PASS.

SCAN-03 return null:
  Result: 4 pre-existing executable return null at CopyEngine.cs:663, 1069, 1075, 1128.
  No new return null introduced by this ticket. PASS (pre-existing, not in changed block).

SCAN-04 volatile double:
  Result: 0 matches. PASS.

SCAN-05 GeneralOptions.Dispatcher:
  Result: 1 match at CopyEngine.cs:755 —
    `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(() =>`
  Exactly 1 match confirming correct NT8 dispatcher is used. PASS.

SCAN-06 CYC SendCopy (manual count from source lines 731–778):
  Base = 1
  Branch (1): `if (mode is FollowerAtmMode.Market)` at line 737
  Branch (2): ternary `mode is FollowerAtmMode.Named named ? ... : ...` at line 744
  Branch (3): `try { } catch` at line 748
  Lambda body: single expression, no decision points — +0
  CYC = 1 + 3 = 4 (strict V(G)). Method comment annotates as CYC=5 (alternate convention).
  Either value is well under the ≤8 limit. PASS.

SCAN-07 NUnit/MSTest:
  Result: 0 matches in CopyEngineTests.cs. xUnit only. PASS.

---

## Dispatcher Verification

- GeneralOptions.Dispatcher present: YES
  CopyEngine.cs:755 — `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(() =>`
- Application.Current.Dispatcher absent: YES
  Select-String for "Application\.Current\.Dispatcher" → 0 matches
- No await on InvokeAsync: YES
  Select-String for "await.*InvokeAsync" → 0 matches; source inspection confirms no await
- return true is OUTSIDE lambda: YES
  CopyEngine.cs:770 closes the InvokeAsync call with `);`
  CopyEngine.cs:771 `return true;` is outside the lambda, inside the try block — correct

---

## SendCopy Structure Verification (CopyEngine.cs:731–778)

  a. `NinjaTrader.Core.Globals.GeneralOptions.Dispatcher.InvokeAsync(` present at line 755 — YES
  b. Lambda wraps `follower.CreateOrder(` — YES, lines 756–769
  c. No `await` keyword before InvokeAsync — YES (line 754 is a comment, line 755 has no await)
  d. `return true;` at line 771 is OUTSIDE the lambda (after `;` at line 770) — YES
  e. try/catch wraps entire block (lines 748–777) — YES

---

## [Fact] Count

Total [Fact] count in CopyEngineTests.cs: 124

Baseline discrepancy note:
  - Ticket 04-tickets.md specifies baseline = 122, target = 123 (+1).
  - Actual baseline before this lane's edit was 123 (not 122) because an uncommitted test
    `AddRule_Replace_WhenSameInstrumentAndLeader` was already present from an adjacent B23 lane.
  - This ticket adds exactly +1 [Fact] (`SendCopy_CompletesWithoutThrow_WhenDispatcherNotAvailable`).
  - Final count 124 = 123 pre-existing (122 committed + 1 uncommitted from another lane) + 1 this ticket.
  - This ticket's contribution is confirmed as exactly +1.

Confirm SendCopy_CompletesWithoutThrow_WhenDispatcherNotAvailable IS present: YES
  CopyEngineTests.cs:2200–2216
  [Fact] attribute at line 2200, Assert.False(threw) at line 2215.
  Note: Reflection call skipped (CopySignal is private) per explicit ticket allowance.
  Fallback assert-true pattern is acceptable per ticket spec.

---

## Cross-Check vs Engineer Report

| Item | Engineer Report | Independent Verification | Discrepancy |
|------|----------------|--------------------------|-------------|
| SCAN-01 lock() | 0 exec, 5 comments | 0 exec, 5 comments | NONE |
| SCAN-02 async void | 0 exec, 1 comment | 0 exec, 1 comment | NONE |
| SCAN-03 return null | 4 pre-existing (663,1069,1075,1128) | 4 pre-existing (same lines) | NONE |
| SCAN-04 volatile double | 0 matches | 0 matches | NONE |
| SCAN-05 GeneralOptions.Dispatcher | 1 match CopyEngine.cs:755 | 1 match CopyEngine.cs:755 | NONE |
| SCAN-06 CYC | 5 (convention) | 4 strict V(G) — ≤8, passes | Minor convention diff, no violation |
| SCAN-07 NUnit/MSTest | 0 matches | 0 matches | NONE |
| [Fact] count | 124 | 124 | NONE |
| New test present | YES at line 2201 | YES at line 2201 | NONE |

No discrepancies. Engineer's self-report is accurate.

---

## Verdict

VERIFY_PASS  (all checks confirm correct implementation)

All 7 scans pass with 0 violations in changed code.
Dispatcher correctly uses NT8 GeneralOptions.Dispatcher (not Application.Current.Dispatcher).
InvokeAsync is fire-and-forget (no await, method is not async — JS-033 compliant).
return true is correctly placed outside the lambda.
New [Fact] SendCopy_CompletesWithoutThrow_WhenDispatcherNotAvailable is present with correct xUnit attributes.
CYC of SendCopy remains 4-5 (well under ≤8 limit).
[Fact] count = 124; discrepancy from ticket baseline is pre-existing uncommitted work from
another B23 lane — this ticket contributes exactly +1 as specified.
