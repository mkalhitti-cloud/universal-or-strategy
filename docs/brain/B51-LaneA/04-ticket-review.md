# B51-LaneA Ticket Review

**Block**: PTT-COPIER-B51
**Lane**: A
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-08
**Tickets reviewed**: `docs/brain/B51-LaneA/04-tickets.md`
**Plan reviewed**: `docs/brain/B51-LaneA/02-architecture-plan.md` (REVIEW_PASS confirmed)
**Rules applied**: `docs/standards/jane-street/RULES_CATALOG.md` + `docs/standards/NT8_COMPILER_RULES.md`

---

## T1 — Fix multiplier TextBox visibility + ATM combo timing + build tag bump

### Traceability: PASS

| Spec Requirement | Ticket Section | Plan Section |
|------------------|---------------|--------------|
| DW-B51-01: Multiplier TextBox column visible in follower rows | Change 1 of 3 | Plan §2 (DW-B51-01) |
| DW-B51-01: DO NOT delete TextBox | Change 1 of 3 (AddHandler line preserved in Before snippet) | Plan §2 Before/After |
| DW-B51-01: DO NOT remove OnFollowerMultiplierChanged | Change 1 of 3 (AddHandler kept; SetValue added after) | Plan §2, §5 |
| DW-B51-02: ATM dropdown reappears in Clone mode | Change 2 of 3 | Plan §2 (DW-B51-02) |
| DW-B51-02: Fix inside !_atmComboRefs.Contains(cb) block | Change 2 of 3 Before/After snippet | Plan §2 code snippet |
| DW-B51-02: CYC <= 8 after fix | Change 2 of 3 (CYC delta stated: 4→5) | Plan §6 |
| Build tag: "PTT-COPIER B51 \| ui-fixes \| 2026-08-08" | Change 3 of 3 | Plan §2 (Build Tag Bump) |
| Files in scope: TradeCopierPanel.cs + CopyEngine.cs only | Files table | Plan §3, §10 |
| No new tests required | No New Tests Rationale section | Plan §9 |

No phantom work detected (every change maps to a spec or plan item).
No missing plan items (all three plan components covered in tickets).

---

### JS Pre-Check: PASS

| Rule | Severity | Check | Result |
|------|----------|-------|--------|
| JS-021 | P0 | No `lock()` introduced in modified regions or new code | PASS — ticket states "No lock" explicitly in both Change 1 and Change 2; no lock pattern in any code snippet |
| JS-001 | P0 | No `throw new XxxException` in hot paths | PASS — no throw statement in any described code change |
| JS-002 | P0 | No `return null` for missing values | PASS — neither modified method has a return statement (both void) |
| JS-033 | P0 | No `async void` (non-event-handler) | PASS — `OnFollowerAtmTemplateComboLoaded` is a plain void RoutedEventHandler (explicitly flagged as "not async" in Change 2); `BuildCheckItemTemplate` is non-async |
| JS-008 | P1 | No mutable fields on struct | PASS — no structs declared |
| JS-009 | P2 | No Dictionary on engine/rule fields | PASS — no new collections introduced |

No JS violations detected.

---

### CYC Pre-Check: PASS

| Method | Before | After | Delta | Limit | Status |
|--------|--------|-------|-------|-------|--------|
| `BuildCheckItemTemplate()` | N/A (no new branch) | N/A | 0 | ≤8 | PASS — SetValue is a single-statement call, no branch |
| `OnFollowerAtmTemplateComboLoaded` | 4 | 5 | +1 | ≤8 | PASS — one new `if (GetCopyMode() == CopyMode.Clone)` inside existing block |

CYC spec constraint (≤8 for `OnFollowerAtmTemplateComboLoaded`) satisfied at 5.
SCAN-06 provides a branch-by-branch table (5 branches enumerated) for engineer verification.

---

### NT8 Check: PASS

| Rule | Severity | Check | Result |
|------|----------|-------|--------|
| NT8-001 | P0 | No `{ get; init; }` | PASS — no properties declared |
| NT8-002 | P0 | No `abstract record` / `sealed record` | PASS — no types declared |
| NT8-003 | P0 | No `volatile double` | PASS — no field declarations |
| NT8-007 | P0 | No `CreateOrder` with wrong arg 12 | PASS — no CreateOrder call in scope |
| NT8-013 | P0 | No `DateTime.Now` in CreateOrder | PASS — no CreateOrder call in scope |
| NT8-015 | P0 | No `sealed class : Indicator` | PASS — no class declarations |
| NT8-016 | P0 | No `sealed class : Window` | PASS — no class declarations |
| NT8-019 | P0 | No `async void` in NT8 callbacks | PASS — no async methods introduced |
| NT8-030 | P0 | `OnWindowCreated` idempotency guard | PASS — OnWindowCreated not touched |
| NT8-031 | P0 | No `OrderState.PendingSubmit` | PASS — no OrderState access |
| NT8-042 | P0 | No `Dispatcher.InvokeAsync` | PASS — ticket §Change 2 "Threading note" explicitly states both changes are UI-thread-local; no dispatcher call introduced or required |
| NT8-043 | P0 | No null-conditional compound assignment (`?.` with `-=`/`+=`) | PASS — no event subscriptions modified |
| NT8-044 | P0 | `StringComparison` requires `using System` | PASS — StringComparison not used |
| NT8-017 | P1 | Cross-thread bool/int fields must be `volatile` | PASS — no new fields declared |
| NT8-020 | P1 | `SolidColorBrush` must be frozen | PASS — no brush creation |
| NT8-028 | P1 | No hex color string literals | PASS — no string color literals |

No NT8 violations detected.

---

### Test Coverage: PASS

No new public or internal methods are introduced by this ticket. The changes are:

- A one-line `SetValue` call inserted into the body of the existing `BuildCheckItemTemplate()` method.
- A 4-line braced block inserted into the existing body of `OnFollowerAtmTemplateComboLoaded`.
- A one-string literal replacement in `CopyEngine.cs`.

The No New Tests Rationale section is present and valid:
- Both UI-touching changes require a live WPF `Application` + `DispatcherFrame` + NT8 NinjaScript host.
- xUnit console runners do not provide a WPF application context.
- This is consistent with the existing PTT test approach.
- SCAN-03 and SCAN-04 provide textual verification that the changes were applied correctly.

No `[Fact]` requirement applies. Test Coverage: PASS.

---

### Scan Checklist: PASS

All 7 scans are present on the ticket with exact commands and expected results.

| Scan | Description | Present | Expected Result Stated |
|------|-------------|---------|----------------------|
| SCAN-01 | `lock()` check (`Select-String -Pattern "lock\("`) | ✅ | ✅ Zero matches in modified regions |
| SCAN-02 | `async void` check | ✅ | ✅ Zero new async void methods |
| SCAN-03 | `Visibility.Collapsed` grep — confirms multFactory line present | ✅ | ✅ At least one match on multFactory line |
| SCAN-04 | `GetCopyMode\|CopyMode\.Clone` grep — confirms ATM timing fix | ✅ | ✅ At least two new matches inside OnFollowerAtmTemplateComboLoaded |
| SCAN-05 | Build gate (`dotnet build PropTraderTools.csproj`) | ✅ | ✅ `Build succeeded. 0 Error(s)` |
| SCAN-06 | CYC manual branch count for OnFollowerAtmTemplateComboLoaded | ✅ | ✅ CYC = 5, branch table enumerated |
| SCAN-07 | Hard-link integrity (`scripts\verify_links.ps1`) + `-Fix` fallback | ✅ | ✅ `DESYNC=0 MISSING=0` |

All 7 scans present. Scan Checklist: PASS.

---

### File Routing: PASS

| File | Path in Ticket | Workspace | Correct? |
|------|---------------|-----------|---------|
| TradeCopierPanel.cs | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | Wave | ✅ PASS |
| CopyEngine.cs | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Wave | ✅ PASS |

No Director workspace paths (`c:\WSGTA\universal-or-strategy-director\`) used for .cs files.

---

### Spec Coverage (Aggregate): PASS

All spec requirements present in the inline spec are covered exactly once:

| Spec Requirement | Coverage | Duplicate? |
|-----------------|----------|-----------|
| DW-B51-01 fix (SetValue Collapsed) | Change 1 of 3 | No |
| DW-B51-01 constraint (no TextBox delete) | Change 1 of 3 | No |
| DW-B51-01 constraint (no handler removal) | Change 1 of 3 | No |
| DW-B51-02 fix (GetCopyMode check) | Change 2 of 3 | No |
| DW-B51-02 CYC constraint | Change 2 of 3 | No |
| Build tag bump | Change 3 of 3 | No |

No uncovered requirements. No duplicate coverage.

---

### VERDICT: TICKET_REVIEW_PASS

All 7 checklist items PASS. Zero violations detected.

---

## Overall: TICKET_REVIEW_PASS

**T1 summary**: Single-ticket block covering 3 spec changes (DW-B51-01, DW-B51-02, build tag).
All items traceable to spec and plan. Zero JS P0/P1 violations in planned code. Zero NT8 P0
violations. CYC within limit (5 ≤ 8). No-test rationale valid. All 7 scan items present with
correct expected results. File routing correct (Wave workspace only).

**Approved for Phase 4a engineer.**

Engineer instruction: execute T1 in its entirety, run SCAN-01 through SCAN-07 in sequence
before committing, and record results in `ticket-1-completion.md`.
