# Ticket Review: PTT-COPIER-B23-LANE-C
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Block**: PTT-COPIER-B23
**Lane**: C
**Defect**: DW-B22-BE-TRIGGER-01 (P1)
**Tickets Reviewed**: T1 (1 ticket total)
**Date**: 2026-07-16

---

## T1 — Replace Dollar-PnL Armed Trigger With Price-Based Trigger

### Traceability: PASS
- Spec requirement `DW-B22-BE-TRIGGER-01` explicitly cited in preamble and in the
  "Spec Requirement Satisfied" section.
- Edit A maps to architecture plan §2 (Revised Trigger design).
- Write-set matches architecture plan §3 exactly.
- No phantom work: every change in the ticket maps to a plan item.
- No missing plan work: §2 price-based trigger logic and §2 new [Fact] tests both
  appear in the ticket.

### JS Pre-Check: PASS
- **JS-021 (No lock())**: No `lock()` described. CAS via
  `Interlocked.CompareExchange(ref _pendingBeState, 0, 1)` is the correct
  lock-free primitive. PASS.
- **JS-001 (No throw in hot path)**: No exception throwing described. PASS.
- **JS-002 (No return null)**: Nullable references use `?? 0.0` fallback, not null
  returns. `acc?.AccountItemUpdate` is a null-conditional assignment, not a null
  return. PASS.
- **JS-023/025 (Concurrent collections)**: No new shared-state collections introduced.
  Existing fields `_pendingBeAccount`, `_pendingBeInstrument` are plain refs, already
  established by prior architecture. PASS.
- **JS-033 (No async void)**: No `async void` described anywhere. PASS.

### CYC Pre-Check: PASS
Enumeration of all 7 `if`-branches in the REPLACE block of `OnPendingBeAccountUpdate`:

| # | Branch condition | Location in REPLACE block |
|---|-----------------|--------------------------|
| 1 | `if (_pendingBeState != 1)` | volatile int read |
| 2 | `if (e.AccountItem != AccountItem.UnrealizedProfitLoss)` | item filter |
| 3 | `if (IsFlat(pos))` | flat position guard |
| 4 | `if (tickSize <= 0.0)` | tick size guard |
| 5 | `if (last <= 0.0)` | last price guard |
| 6 | `if (!triggered)` | price threshold check |
| 7 | `if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1)` | CAS disarm |

**Total**: 7 if-branches + method base = **CYC 8**. Ternary `(isLong ? 1.0 : -1.0)` and
ternary `isLong ? (last >= target) : (last <= target)` are expressions, not branches
per project CYC convention. Null-conditional `acc?.AccountItemUpdate -=` is not a
CYC branch per project convention (same as ternary). CYC = 8 ≤ 8 limit.
The CYC=8 comment in the REPLACE block correctly enumerates all 7 branches.

### NT8 Check: PASS
- No `async/await` in lifecycle method described.
- No `sealed` on `TradeCopierWindow`.
- No `DateTime.Now` usage.
- No `volatile double` (SCAN-04 guards this).
- No `FontFamily` set on WPF element.
- No hardcoded hex color.
- No `Account.All` call outside Loaded handler.
- No `CreateOrder` with non-"PTT-" prefix.
- `_pendingBeInstrument?.MarketData?.Last?.Price` — 3-level null-conditional chain
  follows NT8-032 pattern correctly.
- `acc?.AccountItemUpdate -= OnPendingBeAccountUpdate;` replaces `if (acc != null)`
  guard — correct null-conditional unsubscribe pattern.

### Test Coverage: PASS
Both new public-facing behaviors have `[Fact]` tests:

| Method/Behavior | Test | Assertion |
|-----------------|------|-----------|
| `OnPendingBeAccountUpdate` fires at price target (long) | `PendingBe_Armed_FiresAtPriceTarget_Long` | `Assert.True(fired)` with UPnL=-1.25 (negative UPnL must not block firing) |
| `OnPendingBeAccountUpdate` does NOT fire below target (long) | `PendingBe_Armed_DoesNotFireBelowTarget_Long` | `Assert.False(fired)` when price is 1 tick short, even when UPnL is positive |

Key assertions verified:
- Test 1 uses `UPnL = -1.25` (negative) — this is the critical regression proof that
  the old dollar-PnL check no longer gates the trigger. Correct.
- Test 2 uses `UPnL = +1.25` (positive) — old trigger WOULD have fired here. New
  trigger must NOT. Correct.
- xUnit `[Fact]` used on both tests. No NUnit/MSTest annotations.
- Net [Fact] count: baseline + 2. Matches preamble specification.

### Scan Checklist: PASS
All 7 scans present in ticket:

| Scan | Rule/Purpose | Expected Result | Present? |
|------|-------------|----------------|----------|
| SCAN-01 | JS-021: No `lock()` | 0 new matches | YES |
| SCAN-02 | JS-033: No `async void` | 0 matches | YES |
| SCAN-03 | JS-002: No new `return null` | no new return null in changed method | YES |
| SCAN-04 | NT8-003: No `volatile double` | 0 matches | YES |
| SCAN-05 | Old dollar-PnL trigger removed (`e\.Value < 0`) | 0 matches in `OnPendingBeAccountUpdate` | YES |
| SCAN-06 | CYC ≤ 8 manual count | CYC = 8 | YES |
| SCAN-07 | No NUnit/MSTest | 0 matches | YES |

Each scan includes PowerShell command (SCAN-01 through SCAN-05, SCAN-07) or manual
count protocol (SCAN-06). Engineer has a complete runnable checklist.

### File Routing: PASS
- `CopyEngine.cs` → `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` — Wave workspace. PASS.
- `CopyEngineTests.cs` → `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` — Wave workspace. PASS.
- No `.cs` file paths pointing to Director workspace (`c:\WSGTA\universal-or-strategy-director`).
- "DO NOT TOUCH" list explicitly excludes `TradeCopierPanel.cs`, `TradeCopierWindow.cs`,
  `TradeCopierAddOn.cs`, `AtrSizingEngine.cs`, and all `.md` files.

### Edit A Integrity: PASS
- FIND block contains the old `if (e.Value < 0)   // (3) threshold` line — confirmed present.
- REPLACE block does NOT contain `e.Value < 0` or `if (e.Value < 0) return;` anywhere.
- The old guard is fully removed and replaced with the 6-step price-based trigger.
- `if (acc != null)` guard is NOT in the REPLACE block; `acc?.AccountItemUpdate -=` IS present.

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All checks passed. No JS-XXX violations found. No NT8 violations found. No CYC violations.
All 7 scans present in T1. Write-set clean (2 files, Wave workspace). Traceability to
`DW-B22-BE-TRIGGER-01` confirmed. Two `[Fact]` tests covering both sides of the price
threshold check (fire at target, no-fire below target). Engineer may proceed.
