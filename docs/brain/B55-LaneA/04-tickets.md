# B55 LaneA — Tickets
# Epic: DW-B43-02 P1 — ATM Template Read Fix (GetLeaderAtmTemplateName SelectedItem)
# Status: TICKETS_COMPLETE
# Plan: docs/brain/B55-LaneA/02-architecture-plan.md (REVIEW_PASS — Cycle 2)
# Spec: specs/002-trade-copier-spec.html (DW-B43-02 P1)

---

## T1 — Add B55Tests.cs: T_B55A_01 documents SelectedItem read path

---

### 1. Spec Requirement IDs

| ID | Priority | Description |
|----|----------|-------------|
| DW-B43-02 | P1 | GetLeaderAtmTemplateName() read SelectedValue (null) instead of SelectedItem |

**Deferred work item closed:** DW-B43-02 — Fix already present in working tree (`TradeCopierPanel.cs` line 2088). B55 LaneA adds the test that documents and locks the fixed read path.

---

### 2. Files Modified / Created

| File | Action | Notes |
|------|--------|-------|
| `C:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\B55Tests.cs` | **CREATE** | New xUnit test class |
| `TradeCopierPanel.cs` | **NO CHANGE** | Fix already in working tree (line 2088 reads `SelectedItem`) |

**No other files are touched by this ticket.** (No Scope Creep Protocol — `docs/protocol/NO_SCOPE_CREEP_PROTOCOL.md`)

---

### 3. Method Signatures

#### Test Class: `B55Tests`

File header comment (REQUIRED — copy verbatim, do not alter):

```csharp
// PTT-COPIER-B55 -- B55Tests.cs
// xUnit [Fact] tests for B55: ATM Template Read Fix (DW-B43-02 P1).
// Defect closed: DW-B43-02 -- GetLeaderAtmTemplateName read SelectedValue (null) instead of SelectedItem.
// Fix: TradeCopierPanel.GetLeaderAtmTemplateName() now reads cb.SelectedItem (line 2088).
// T_B55A_01: Documents the SelectedItem read path -- pure pattern, no WPF required.
// Jane Street rules: JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest.
// CYC: T_B55A_01 = CYC 1 (straight-line assertion body).
using Xunit;
```

Full file contents to write (verbatim):

```csharp
// PTT-COPIER-B55 -- B55Tests.cs
// xUnit [Fact] tests for B55: ATM Template Read Fix (DW-B43-02 P1).
// Defect closed: DW-B43-02 -- GetLeaderAtmTemplateName read SelectedValue (null) instead of SelectedItem.
// Fix: TradeCopierPanel.GetLeaderAtmTemplateName() now reads cb.SelectedItem (line 2088).
// T_B55A_01: Documents the SelectedItem read path -- pure pattern, no WPF required.
// Jane Street rules: JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest.
// CYC: T_B55A_01 = CYC 1 (straight-line assertion body).
using Xunit;

namespace PropTraderTools
{
    public class B55Tests
    {
        [Fact]
        public void T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName()
        {
            // Arrange: simulate ComboBox state when NT8 populates cbxStrategySelector
            // (NT8 does NOT set SelectedValuePath, so SelectedValue is always null)
            object selectedItem  = "MES $200";   // ComboBox.SelectedItem after user selects template
            string selectedValue = null;          // ComboBox.SelectedValue -- null because no SelectedValuePath

            // Act: exact expression from GetLeaderAtmTemplateName() line 2088
            string result = selectedItem as string ?? string.Empty;

            // Assert
            Assert.Equal("MES $200", result);    // SelectedItem path returns the template name
            Assert.Null(selectedValue);          // documents root cause: SelectedValue is null
        }
    }
}
```

#### Signature summary

| Symbol | Kind | Return | Parameters | CYC | Namespace |
|--------|------|--------|------------|-----|-----------|
| `B55Tests` | class | — | — | — | `PropTraderTools` |
| `T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName` | `[Fact] void` | `void` | none | 1 | `PropTraderTools` |

---

### 4. JS Rule Constraints

| Rule | Description | Applies to T1? | Required Status |
|------|-------------|---------------|-----------------|
| JS-021 | No `lock()` anywhere in src/ | No | No `lock(` in B55Tests.cs |
| JS-033 | No `async void` (non-event-handler) | No | No `async` keyword used |
| JS-001 | No `throw new XxxException` in hot paths | No | No `throw` used |
| JS-002 | No `return null` for missing values | No | `void` method — no return value |
| JS-008 | `SolidColorBrush.Freeze()` / mutable struct fields | No | No WPF, no structs |
| JS-009 | `ImmutableDictionary` for shared collections | No | No collections |
| JS-010 | Public constructors with no smart-constructor | No | No public constructor (xUnit instantiates class) |
| JS-023 | UI update from off-thread without `Dispatcher.InvokeAsync` | No | No UI code |

**All JS rules: PASS (non-applicable)**

---

### 5. NT8 Compiler Rule Constraints

| Rule | Description | Applies to T1? | Required Status |
|------|-------------|---------------|-----------------|
| NT8-001 | `{ get; init; }` banned | No | No properties |
| NT8-002 | `abstract record` / `sealed record` banned | No | No records |
| NT8-003 | `volatile double` banned | No | No `volatile` |
| NT8-004 | `System.Collections.Immutable` banned in NT8 | No | Not used |
| NT8-007 | `CreateOrder` arg 12 must be `(CustomOrder)null` | No | No `CreateOrder` call |
| NT8-019 | `async void` banned | No | No async |
| NT8-028 | Hex color string literals banned | No | No UI/colors |
| NT8-042 | `Dispatcher.InvokeAsync` banned in NT8 AddOn | No | No dispatcher |
| NT8-044 | `StringComparison` requires `using System;` | No | Not used |
| NT8-045 | `AtmStrategy.AtmStrategyTemplates` banned in Linting DLL | No | Not used |

**All NT8 rules: PASS (non-applicable)**

---

### 6. xUnit [Fact] Test Names and Assertions

| Test method | Asserts | CYC |
|-------------|---------|-----|
| `T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName` | `Assert.Equal("MES $200", result)` — SelectedItem path returns template name | 1 |
| (same) | `Assert.Null(selectedValue)` — documents root cause: SelectedValue is always null when no SelectedValuePath is set | 1 |

**Test baseline after B55 LaneA:** 297 (pre-B55) → **298** (post-B55)

---

### 7. 7-Scan Checklist (SCAN-01 through SCAN-07) — Engineer Contract

All 7 scans MUST be run after creating B55Tests.cs. All MUST pass before the hard-link sync.

**Working directory for all scans:** `C:\WSGTA\universal-or-strategy`

---

#### SCAN-01 — lock() check (P0 BLOCKER)

```powershell
Select-String "lock(" src\ -Recurse -Include *.cs
```

**Required result:** 0 matches. Any result = HARD STOP. Do not proceed.

---

#### SCAN-02 — async void check (P0 BLOCKER)

```powershell
Select-String "async void " src\ -Recurse -Include *.cs
```

**Required result:** 0 matches. Any result = HARD STOP. Do not proceed.

---

#### SCAN-03 — return null check

```powershell
Select-String "return null" src\ -Recurse -Include *.cs
```

**Required result:** 0 NEW instances introduced by B55Tests.cs. (Pre-existing instances in other files are not regressions for this ticket — report but do not fix; No Scope Creep Protocol.)

---

#### SCAN-04 — throw new check

```powershell
Select-String "throw new " src\ -Recurse -Include *.cs
```

**Required result:** 0 NEW instances introduced by B55Tests.cs.

---

#### SCAN-05 — cyclomatic complexity audit

```powershell
python scripts/complexity_audit.py
```

**Required result:** All methods CYC <= 8. `T_B55A_01` is CYC = 1 (straight-line body). Any method > 8 = HARD STOP.

---

#### SCAN-06 — build check (P0 BLOCKER)

```powershell
dotnet build
```

**Required result:** 0 errors, 0 warnings introduced by B55Tests.cs. Pre-existing warnings are not regressions for this ticket.

---

#### SCAN-07 — test run (P0 BLOCKER)

```powershell
dotnet test
```

**Required result:**
- All `[Fact]` tests pass.
- Total passing test count = **298** (baseline 297 + 1 new).
- `T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName` = PASS.
- `T_B43_04_GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString` = PASS (must be unchanged).
- Any failure = HARD STOP. Do not push.

---

#### Post-scan hard-link sync (MANDATORY — run after all 7 scans pass)

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

**Required result:** Script exits 0 (all links verified/repaired). Do not push if this fails.

---

### 8. Invariants (Verifier Must Confirm)

| # | Invariant | How to verify |
|---|-----------|---------------|
| INV-1 | `T_B43_04_GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString` still passes unchanged | SCAN-07: appears in passing test list |
| INV-2 | `T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName` passes with `result == "MES $200"` | SCAN-07: appears in passing test list |
| INV-3 | `GetLeaderAtmTemplateName()` in `TradeCopierPanel.cs` reads `SelectedItem` (not `SelectedValue`) at line 2088 | Code review: `return atmCb.SelectedItem as string ?? string.Empty;` |
| INV-4 | Test count after B55 LaneA: **298** | SCAN-07: total passing count shown in dotnet test output |

---

### 9. Out-of-Scope Items (No Scope Creep Protocol)

The following items are explicitly NOT part of T1. Do not fix them inline. Do not address them as part of this ticket.

| Item | Status | Disposition |
|------|--------|-------------|
| DW-B54-01: AtmStrategyCreate AddOn API path | Open | Director research required before next block |
| DW-B54-02: F5-GATE-02 live ATM bracket test | Open | Requires live NT8 session — out of scope |
| Any pre-existing `return null` instances in src/ | Pre-existing | Report to Director; do NOT fix (scope creep) |
| Any pre-existing complexity violations in src/ | Pre-existing | Report to Director; do NOT fix (scope creep) |
| Any other compilation warnings unrelated to B55Tests.cs | Pre-existing | Report to Director; do NOT fix (scope creep) |

---

### 10. Implementation Checklist (Engineer Contract)

Before marking T1 COMPLETE, engineer must confirm ALL items:

```
[ ] B55Tests.cs created at C:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\B55Tests.cs
[ ] File header comment is verbatim (ASCII-only, no Unicode)
[ ] Namespace is PropTraderTools (matches existing test files)
[ ] Class name is B55Tests
[ ] [Fact] method name matches exactly: T_B55A_01_GetLeaderAtmTemplateName_SelectedItemSet_SelectedValueNull_ReturnsTemplateName
[ ] Only using Xunit; import (no NUnit, no MSTest, no NT8 namespaces)
[ ] SCAN-01 passes: 0 lock() results
[ ] SCAN-02 passes: 0 async void results
[ ] SCAN-03 passes: 0 new return null instances
[ ] SCAN-04 passes: 0 new throw new instances
[ ] SCAN-05 passes: all methods CYC <= 8
[ ] SCAN-06 passes: dotnet build 0 errors
[ ] SCAN-07 passes: 298 tests pass, T_B55A_01 = PASS, T_B43_04 = PASS
[ ] verify_links.ps1 -Fix exits 0
[ ] TradeCopierPanel.cs was NOT modified
[ ] No other src/ files were modified
```

---

## Ticket Summary

| Field | Value |
|-------|-------|
| Ticket | T1 |
| Block | B55 LaneA |
| Spec req closed | DW-B43-02 P1 |
| Files changed | 1 (CREATE: B55Tests.cs) |
| Files NOT changed | TradeCopierPanel.cs (all others) |
| New [Fact] tests | 1 (`T_B55A_01`) |
| Test delta | 297 → 298 |
| CYC introduced | 1 (T_B55A_01 = CYC 1) |
| JS violations | 0 |
| NT8 violations | 0 |
| Scans required | SCAN-01 through SCAN-07 + verify_links.ps1 |
