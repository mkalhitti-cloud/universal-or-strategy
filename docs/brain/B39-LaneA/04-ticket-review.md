# Ticket Review: B39-LaneA
<!-- Phase 3.5 — ptt-ticket-reviewer | 2026-07-30 -->
<!-- Rev 3 final — F4 verified fixed; all checks PASS -->

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Tickets reviewed**: 04-tickets.md Rev 3
**Plan reviewed**: 02-architecture-plan.md REVIEW_PASS (Rev 2)
**Spec**: `specs/002-trade-copier-spec.html` id="section-b39"

---

## F4 Fix Verification (Rev 3)

| Item | Status |
|------|--------|
| T_B39_07 `GlobalBeBuffer_IncrementClampedAt10` present in T2 §4 | PASS |
| T_B39_07 asserts `GlobalBeBuffer == 10` after 11 increments | PASS |
| T_B39_08 `GlobalBeBuffer_DecrementClampedAtMinus10` present in T2 §4 | PASS |
| T_B39_08 asserts `GlobalBeBuffer == -10` after 11 decrements | PASS |
| Test count target updated to `>= 188` in T2 §7, §8, §9 | PASS |

**F4 is fully resolved.**

---

## T1 — Implement PttGlobalBreakEven + wire Panel/Window + update CopyEngine

### Traceability
PASS

All methods and structural changes map to plan sections:
- `PttGlobalBreakEven.cs` → plan §3 (fields, constructors, Execute, ExecuteOne, buffer helpers)
- `CopyEngine.cs` (3 changes) → plan §4 (tag, SubmitBeStop access, GlobalBe property)
- `TradeCopierPanel.cs` (Row 2/3 restructure, handlers, helper) → plan §5
- `TradeCopierWindow.cs` (toolbar row, handlers, helper) → plan §6

No phantom work found. No plan items missing from T1.

### JS Pre-Check
PASS

| Rule | Finding |
|------|---------|
| JS-021 no `lock()` | `_globalBeBuffer` is `volatile int`; no lock anywhere in new code. PASS |
| JS-008 SolidColorBrush Freeze() | `BrushPurple`, `WBrushPurple`, `WBrushFlash` all declared `static readonly` via `MakeBrush()`/`MakeWinBrush()` which call `.Freeze()` internally. No inline `new SolidColorBrush(...)` without Freeze(). PASS |
| JS-023 volatile int | `volatile int _globalBeBuffer` explicitly allowed. PASS |
| JS-002 no `return null` | `ExecuteOne` uses `return` (void early exit); `Execute` uses `continue`. No `return null`. PASS |
| JS-033 no `async void` | All handlers are synchronous `private void`. DispatcherTimer used for flash (not async/await). PASS |
| JS-001 no `throw new` | No exception throwing in any new method. PASS |

### CYC Pre-Check
PASS

All new methods within the ≤ 8 absolute budget (plan §3.4 advisory accepted CYC=5/4):

| Method | CYC | Budget |
|--------|-----|--------|
| `Execute(int)` | 5 | PASS |
| `Execute(IEnumerable<Account>, int)` | 5 | PASS |
| `ExecuteOne` | 4 | PASS |
| `GlobalBeBuffer` (property) | 1 | PASS |
| `IncrementBuffer()` | 2 | PASS |
| `DecrementBuffer()` | 2 | PASS |
| `OnGlobalBeClick`, `OnWindowGlobalBeClick` | 3 each | PASS |
| `OnGlobalBeUp/Down`, `OnWindowGlobalBeUp/Down` | 2 each | PASS |
| `FormatGlobalBeBuffer`, `FormatWindowGlobalBe` | 3 each | PASS |

### NT8 Check
PASS

| Constraint | Finding |
|------------|---------|
| No async/await in lifecycle methods | Not used. PASS |
| No `sealed` on `TradeCopierWindow` | Not described. PASS |
| No `FontFamily` | Not used. PASS |
| No hardcoded hex color strings | Colors use `MakeBrush(r,g,b)` numeric RGB. PASS |
| No `DateTime.Now` | Not used. PASS |
| No `{ get; init; }` | `GlobalBe { get; }` is getter-only auto-property. NT8-001 PASS |
| `volatile int` only (not `volatile double`) | Confirmed `volatile int _globalBeBuffer`. NT8-003 PASS |
| `Account.All` not called outside Loaded handler | Called in `Execute(int)` body method (not a lifecycle method). PASS |

### Test Coverage
PASS

All internal/public methods of `PttGlobalBreakEven` have `[Fact]` tests specified:

| Method | Covered by |
|--------|-----------|
| `Execute(int)` | T_B39_01–T_B39_06 (via IEnumerable overload seam) |
| `Execute(IEnumerable<Account>, int)` | T_B39_01–T_B39_06 |
| `ExecuteOne` | T_B39_05 (direction/price assertion) |
| `GlobalBeBuffer` (property) | T_B39_07, T_B39_08 |
| `IncrementBuffer()` | T_B39_07 |
| `DecrementBuffer()` | T_B39_08 |

WPF event handlers (`OnGlobalBeClick`, etc.) and format helpers (`FormatGlobalBeBuffer`, etc.) are
private, UI-dispatcher-dependent, and not independently `[Fact]`-testable without the NT8 runtime.
This is consistent with all prior B-block patterns. Plan §12 does not list handler tests. PASS.

### Scan Checklist
PASS

SCAN-01 through SCAN-07 all present in T1 §9. Defense-in-depth contract is intact.

### File Routing
PASS

All `.cs` paths are in the Wave workspace:
- `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/TradeCopierWindow.cs`

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — Add T_B39_01 through T_B39_08 to CopyEngineTests.cs

### Traceability
PASS

All 8 tests trace to plan/spec:
- T_B39_01–T_B39_06 → plan §10 (spec `section-b39 §Tests`)
- T_B39_07 → plan §3.6 `IncrementBuffer()` (F4 fix)
- T_B39_08 → plan §3.6 `DecrementBuffer()` (F4 fix)

No phantom work. No missing tests from plan §10.

### JS Pre-Check
PASS

| Rule | Finding |
|------|---------|
| JS-021 no `lock()` | Not present in any test body or helper. PASS |
| JS-002 no `return null` | No `return null` in test helpers. PASS |
| JS-033 no `async void` | All 8 test methods are synchronous `void`. PASS |
| xUnit `[Fact]` only | Explicitly stated; no NUnit/MSTest attributes. PASS |

### CYC Pre-Check
PASS

T_B39_01–T_B39_06: linear, CYC=1 each.
T_B39_07, T_B39_08: single `for` loop, CYC=2 each.
All ≤ 8. PASS.

### NT8 Check
PASS

| Constraint | Finding |
|------------|---------|
| `Account.All` not called in tests | `Execute(IEnumerable<Account>, int)` seam used; no NT8 static collection accessed. PASS |
| No async test methods | All tests synchronous. PASS |

### Test Coverage
PASS

T2 contains only test code and private static helpers. No new public/internal methods requiring
their own `[Fact]` tests.

### Scan Checklist
PASS

SCAN-01 through SCAN-07 all present in T2 §9. SCAN-07 correctly requires `count >= 188`.
Defense-in-depth contract is intact.

### File Routing
PASS

- `tests/V12_Performance.Tests/Core/CopyEngineTests.cs` — Wave workspace test path. PASS.

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All prior violations (F1, F2, F3, F4) are resolved. Rev 3 is clean.
No new violations introduced. Engineer may proceed.

| Ticket | Result |
|--------|--------|
| T1 — Source Code | TICKET_REVIEW_PASS |
| T2 — Tests | TICKET_REVIEW_PASS |
| **Overall** | **TICKET_REVIEW_PASS** |
