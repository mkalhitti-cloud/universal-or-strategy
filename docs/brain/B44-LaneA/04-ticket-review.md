# Ticket Review: B44-LaneA
Block: PTT-COPIER-B44
Epic: B44-LaneA
Reviewer: ptt-ticket-reviewer (Phase 3.5)
Plan reviewed: 02-architecture-plan.md (REVIEW_PASS — Cycle 2)
Tickets reviewed: 04-tickets.md
Date: 2026-08-05

---

## T1 — CopyEngine Idempotency Guards

**Files**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

### Traceability: PASS
| Spec ID | Ticket Change | Status |
|---------|--------------|--------|
| DW-B44-T1-01 | Change 1 — Add `_subscribed` field at L103 | COVERED |
| DW-B44-T1-02 | Change 2 — Subscribe() idempotency guard | COVERED |
| DW-B44-T1-03 | Change 3 — Unsubscribe() idempotency guard | COVERED |

No phantom work. No uncovered plan items (Plan §3.1/3.2/3.3 all mapped).

### JS Pre-Check: PASS
| Rule | Verdict | Evidence |
|------|---------|---------|
| JS-021 (no lock()) | PASS | No `lock(` in any T1 code block. Ticket constraint table confirms. |
| JS-002 (no return null) | PASS | `if (_subscribed) return;` and `if (!_subscribed) return;` are void returns, not null returns. |
| JS-033 (no async void) | PASS | No async methods introduced in T1. Ticket constraint table confirms. |
| JS-023 (volatile bool) | PASS | `private volatile bool _subscribed;` with inline comment `// JS-023 / NT8-017`. |

### CYC Pre-Check: PASS
| Method | CYC Before | CYC After | Limit | Status |
|--------|-----------|-----------|-------|--------|
| `Subscribe()` | 1 | 2 | 8 | PASS — 1 `if` branch added |
| `Unsubscribe()` | 1 | 2 | 8 | PASS — 1 `if` branch added |

### NT8 Check: PASS
| Rule | Verdict | Evidence |
|------|---------|---------|
| NT8-003 (`volatile double` banned) | PASS | Field is `volatile bool` (32-bit, permitted). SCAN-04 grep target confirms. |
| NT8-017 (`volatile bool` mandatory) | PASS | `_subscribed` is `private volatile bool`. Ticket constraint table cites NT8-017. |
| NT8-021 (`Account.All` not in ctor/field init) | PASS | `Account.All` only in method bodies (Subscribe/Unsubscribe), never in field initializers or constructors. |
| TradeCopierWindow.cs UNTOUCHED | PASS | T1 file targets contain only `CopyEngine.cs`. |

### Test Coverage: PASS
T1 method coverage (tests specified in T1 and implemented in T2 B44Tests.cs):
| Method | Test | Status |
|--------|------|--------|
| `Subscribe()` | `T_B44_01_Subscribe_CalledTwice_SubscribedFlagRemainsTrue` | COVERED |
| `Unsubscribe()` | `T_B44_02_Unsubscribe_WhenNotSubscribed_DoesNotThrow` | COVERED |

Both internal methods have [Fact] tests. xUnit only. No NUnit or MSTest.

### Scan Checklist: PASS
All 7 scans present with grep command and expected result:

| # | Scan | Command | Expected |
|---|------|---------|---------|
| SCAN-01 | No lock() | `grep -n "lock\s*(" CopyEngine.cs` | 0 matches |
| SCAN-02 | No async void | `grep -n "async void" CopyEngine.cs` | 0 matches |
| SCAN-03 | No return null | `grep -n "return null" CopyEngine.cs` | 0 matches in Subscribe/Unsubscribe |
| SCAN-04 | No volatile double | `grep -n "volatile double" CopyEngine.cs` | 0 matches |
| SCAN-05 | _subscribed field present | `grep -n "_subscribed" CopyEngine.cs` | >= 3 lines |
| SCAN-06 | CYC compliance | complexity_audit.py | Subscribe=2, Unsubscribe=2, both <= 8 |
| SCAN-07 | Idempotency proof | T_B44_01 xUnit green | PASS |

### File Routing: PASS
Path: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
— Wave workspace (`universal-or-strategy`), not Director workspace (`universal-or-strategy-director`). ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — TradeCopierPanel Wiring + B44Tests.cs

**Files**:
- FILE A: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
- FILE B: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B44Tests.cs` (NEW)

### Traceability: PASS
| Spec ID | Ticket Change | Status |
|---------|--------------|--------|
| DW-B44-T2-01 | Change 1 — Detach() first statement `_engine.Unsubscribe()` | COVERED |
| DW-B44-T2-02 | Change 2 — OnLoaded `_engine.Subscribe()` after IPttModules loop | COVERED |
| DW-B44-T2-03 | Change 3 B44Tests.cs — T_B44_01 double-Subscribe idempotency | COVERED |
| DW-B44-T2-04 | Change 3 B44Tests.cs — T_B44_02 cold-start Unsubscribe | COVERED |
| DW-B44-T2-05 | Change 3 B44Tests.cs — T_B44_03 full Subscribe/Unsubscribe/Subscribe cycle | COVERED |
| DW-B44-T2-06 | Change 3 B44Tests.cs — T_B44_04 fresh engine starts unsubscribed | COVERED |

No phantom work. No uncovered plan items (Plan §4.1/4.2/7 all mapped).

### JS Pre-Check: PASS
| Rule | Verdict | Evidence |
|------|---------|---------|
| JS-021 (no lock()) | PASS | No `lock(` in any T2 code block. Ticket constraint table confirms. |
| JS-002 (no return null) | PASS | New code adds no return statements (straight-line method calls only). |
| JS-033 (no async void) | PASS | No async methods added. `OnLoaded` is a `RoutedEventHandler` — event handler exemption applies per JS-033 definition. Ticket constraint table explicitly documents this. |
| JS-023 (volatile bool) | N/A | T2 adds no new fields. The `_subscribed` field is added by T1. |

### CYC Pre-Check: PASS
| Method | CYC Delta | Post-Change | Limit | Status |
|--------|-----------|-------------|-------|--------|
| `Detach()` | 0 | Unchanged | 8 | PASS — single straight-line call added, no new branch |
| `OnLoaded` | 0 | Unchanged | 8 | PASS — single straight-line call added, no new branch |

### NT8 Check: PASS
| Rule | Verdict | Evidence |
|------|---------|---------|
| NT8-003 (`volatile double` banned) | PASS | No fields introduced in T2. |
| NT8-017 (`volatile bool` mandatory) | PASS | No new fields in T2; `_subscribed` (T1) is `volatile bool`. |
| NT8-021 (`Account.All` not in ctor/field init) | PASS | No new `Account.All` references in T2 production code. |
| NT8-021 (B44Tests.cs — no Account.All) | PASS | B44Tests.cs body contains no `Account.All`. SCAN-07 FILE B verifies: `grep -n "Account.All" B44Tests.cs` → 0 matches. |
| TradeCopierWindow.cs UNTOUCHED | PASS | T2 file targets are `TradeCopierPanel.cs` and `B44Tests.cs` only. SCAN-07 FILE A: `git diff TradeCopierWindow.cs` → 0 lines changed. Cross-Ticket Notes section explicitly states "TradeCopierWindow.cs — UNTOUCHED". |

### Test Coverage: PASS
B44Tests.cs covers all new observable behaviour introduced in T1+T2:

| Test | Method(s) Exercised | Spec ID | Status |
|------|---------------------|---------|--------|
| T_B44_01_Subscribe_CalledTwice_SubscribedFlagRemainsTrue | Subscribe() | DW-B44-T1-02, T2-03 | COVERED |
| T_B44_02_Unsubscribe_WhenNotSubscribed_DoesNotThrow | Unsubscribe() | DW-B44-T1-03, T2-04 | COVERED |
| T_B44_03_ReSubscribe_AfterUnsubscribe_FlagIsTrue | Subscribe(), Unsubscribe() | DW-B44-T2-05 | COVERED |
| T_B44_04_WithoutSubscribe_SubscribedFlag_IsFalse | (field state only) | DW-B44-T2-06 | COVERED |

Framework compliance:
- `using Xunit;` present ✅
- No `using NUnit` or `using MSTest` ✅
- All test methods `[Fact]` only ✅
- `CopyEngine.Instance` singleton access pattern (matches B42Tests.cs:241) ✅
- `FieldInfo` reflection accessor for `_subscribed` private field ✅
- `IDisposable.Dispose()` resets singleton state: `SetSubscribed(false)` ✅
- Zero `Account.All` references — fully NT8-runtime-free ✅

### Scan Checklist: PASS
**FILE A — TradeCopierPanel.cs** (all 7 present):

| # | Scan | Command | Expected |
|---|------|---------|---------|
| SCAN-01 | No lock() | `grep -n "lock\s*(" TradeCopierPanel.cs` | 0 matches |
| SCAN-02 | No async void | `grep -n "async void" TradeCopierPanel.cs` | 0 matches |
| SCAN-03 | No return null in new code | Manual review of inserted lines | 0 new return statements |
| SCAN-04 | Subscribe call in OnLoaded | `grep -n "_engine.Subscribe" TradeCopierPanel.cs` | >= 1 result inside OnLoaded |
| SCAN-05 | Unsubscribe call in Detach | `grep -n "_engine.Unsubscribe" TradeCopierPanel.cs` | >= 1 result; FIRST statement in Detach body |
| SCAN-06 | CYC delta = 0 | complexity_audit.py on TradeCopierPanel.cs | Detach and OnLoaded CYC unchanged |
| SCAN-07 | TradeCopierWindow unchanged | `git diff TradeCopierWindow.cs` | 0 lines changed |

**FILE B — B44Tests.cs** (all 7 present):

| # | Scan | Command | Expected |
|---|------|---------|---------|
| SCAN-01 | xUnit only | `grep -n "using Xunit" B44Tests.cs` | >= 1 line |
| SCAN-02 | No NUnit/MSTest | `grep -n "NUnit\|MSTest" B44Tests.cs` | 0 matches |
| SCAN-03 | Exactly 4 [Fact] tests | `grep -c "\[Fact\]" B44Tests.cs` | 4 |
| SCAN-04 | FieldInfo resolves non-null | T_B44_01 passes | xUnit green |
| SCAN-05 | IDisposable.Dispose present | `grep -n "IDisposable\|Dispose" B44Tests.cs` | Both present |
| SCAN-06 | All 4 tests assert _subscribed | `grep -n "GetSubscribed\|Assert" B44Tests.cs` | >= 8 lines |
| SCAN-07 | NT8-runtime-free | `grep -n "Account.All" B44Tests.cs` | 0 matches |

### File Routing: PASS
FILE A: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` — Wave workspace ✅
FILE B: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B44Tests.cs` — Wave workspace ✅

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All checks across both tickets passed with no violations.

| Check | T1 | T2 |
|-------|----|----|
| Traceability | PASS | PASS |
| JS Pre-Check | PASS | PASS |
| CYC Pre-Check | PASS | PASS |
| NT8 Check | PASS | PASS |
| Completeness | PASS | PASS |
| Test Coverage | PASS | PASS |
| Scan Checklist | PASS | PASS |
| File Routing | PASS | PASS |

**No rule violations found.**
**No missing spec requirements.**
**No phantom work.**
**All 7 scans present on every ticket.**

The engineer may proceed. Execute T1 before T2 (T2 depends on T1's guards being in place).

---

## TICKET_REVIEW_PASS
