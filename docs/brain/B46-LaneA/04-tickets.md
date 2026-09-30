# B46-LaneA — Engineer Tickets
**Block**: PTT-COPIER-B46 — ATM Template Wiring Fix
**Epic**: B46-LaneA
**Phase**: 3 (Ticket Generation)
**Date**: 2026-08-06
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect (Phase 3)
**Wave Workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Architecture plan**: `docs/brain/B46-LaneA/02-architecture-plan.md` (REVIEW_PASS)

---

## Ticket Index

| ID | Title | File | Spec | CYC Before | CYC After |
|----|-------|------|------|-----------|-----------|
| T1 | PttFollowerStrategy ATM Empty Guard | `Features/PttFollowerStrategy.cs` | DW-B46-ATM-EMPTY-GUARD-01 | 1 | 2 |
| T2 | TradeCopierPanel ComboBox Auto-Select Wiring | `TradeCopierPanel.cs` | DW-B46-COMBO-AUTOSELECT-02 | 4 | 7 |
| T3 | CopyEngine Build Tag Update | `CopyEngine.cs` | DW-B46-ATM-EMPTY-GUARD-01, DW-B46-COMBO-AUTOSELECT-02 | 0 delta | 0 delta |
| T4 | B46Tests.cs New File | `B46Tests.cs` (NEW) | DW-B46-ATM-EMPTY-GUARD-01, DW-B46-COMBO-AUTOSELECT-02 | N/A | N/A |

**Recommended execution order**: T1 → T2 → T3 → T4
**Post-commit**: `powershell -File scripts\verify_links.ps1 -Fix` (Wave workspace)

---

## T1 — PttFollowerStrategy ATM Empty Guard

### Ticket Metadata
| Field | Value |
|-------|-------|
| **Ticket ID** | T1 |
| **Title** | PttFollowerStrategy ATM Empty Guard |
| **Spec Req ID** | DW-B46-ATM-EMPTY-GUARD-01 |
| **File** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFollowerStrategy.cs` |
| **Change Type** | Replace method body |
| **CYC Before** | 1 |
| **CYC After** | 2 |

### Problem Statement
`CallAtmStrategyCreate` forwards an empty `AtmTemplateName` to NT8's `AtmStrategyCreate`. NT8 throws
`"Strategy template name parameter missing"` → MaxRestarts (4 in 5 min) → strategy auto-disabled.
Empty template = user chose Inherit = no ATM bracket needed. Strategy must stay alive.

### Method to Find
```
Search for: protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)
```

### Before (exact current method body — verify in file before replacing)
```csharp
protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)
{
    AtmStrategyCreate(
        args.OrderAction,
        OrderType.Market,
        0,
        0,
        TimeInForce.Gtc,
        args.EntryOrderId,
        args.AtmTemplateName,
        Guid.NewGuid().ToString("N").Substring(0, 8),
        (code, msg) =>
        {
            if (code != ErrorCode.NoError)
                Print("B42 ATM error: " + msg);
        });
}
```

### After (exact replacement — write verbatim)
```csharp
// CYC=2: (1) empty-template guard + (2) base AtmStrategyCreate call.
// B46 T1: empty AtmTemplateName = Inherit mode (no ATM brackets requested).
// Skip AtmStrategyCreate to avoid "Strategy template name parameter missing" error
// which trips ErrorHandling=StopStrategy and kills the strategy after MaxRestarts.
// JS-001: no throw. JS-002: no return null (void return). JS-021: no lock.
protected virtual void CallAtmStrategyCreate(FillSignalEventArgs args)
{
    if (string.IsNullOrWhiteSpace(args.AtmTemplateName))   // branch (1): Inherit mode — skip
        return;
    AtmStrategyCreate(
        args.OrderAction,
        OrderType.Market,
        0,
        0,
        TimeInForce.Gtc,
        args.EntryOrderId,
        args.AtmTemplateName,
        Guid.NewGuid().ToString("N").Substring(0, 8),
        (code, msg) =>
        {
            if (code != ErrorCode.NoError)
                Print("B46 ATM error: " + msg);
        });
}
```

**Notes for engineer**:
- The Print string changes from `"B42 ATM error: "` to `"B46 ATM error: "` — this is a required change, not optional.
- `using System;` is already present at line 2 of `PttFollowerStrategy.cs` — `string.IsNullOrWhiteSpace` resolves without any new using directive.
- The guard reads `args.AtmTemplateName`, a field on a struct passed by value. No shared mutable state. No Dispatcher needed.

### Jane Street Compliance
| Rule | Status | Notes |
|------|--------|-------|
| JS-001 (no throw in hot path) | PASS | Guard uses `return;`, no throw introduced |
| JS-002 (no return null) | PASS | `return;` is a void return, not `return null` |
| JS-021 (no lock) | PASS | No lock introduced; guard reads stack-local struct field |
| JS-033 (no async void) | PASS | Method remains `protected virtual void`, synchronous |

### NT8 Compiler Compliance
| Rule | Status | Notes |
|------|--------|-------|
| NT8-001 (no `init` setters) | PASS | No new properties |
| NT8-019 (no `async void`) | PASS | Synchronous void method |
| NT8-013 (no `DateTime.Now`) | PASS | No DateTime usage |
| NT8-044 (`using System;` required) | PASS | Already present at line 2 of file |

### xUnit Tests Made Green by T1
- `T_B46_01` — Empty `AtmTemplateName` guard fires (`IsNullOrWhiteSpace` returns `true`)
- `T_B46_02` — Non-empty `AtmTemplateName` guard does NOT fire (`IsNullOrWhiteSpace` returns `false`)

### T1 — 7-SCAN CHECKLIST

Engineer MUST run all 7 scans from the Wave workspace root
(`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`) before committing T1.

```
SCAN-01  grep -n "lock\s*("           Features/PttFollowerStrategy.cs
         EXPECTED: 0 matches

SCAN-02  grep -n "async void"         Features/PttFollowerStrategy.cs
         EXPECTED: 0 matches

SCAN-03  grep -n "return null"        Features/PttFollowerStrategy.cs
         EXPECTED: 0 matches

SCAN-04  grep -n "IsNullOrWhiteSpace" Features/PttFollowerStrategy.cs
         EXPECTED: >= 1 match (CallAtmStrategyCreate contains the guard)

SCAN-05  grep -n "B46 ATM error"      Features/PttFollowerStrategy.cs
         EXPECTED: 1 match

SCAN-06  grep -n "B42 ATM error"      Features/PttFollowerStrategy.cs
         EXPECTED: 0 matches (old tag must be removed)

SCAN-07  python scripts/complexity_audit.py Features/PttFollowerStrategy.cs
         EXPECTED: CallAtmStrategyCreate CYC=2, <= 8
```

### T1 — Build Pass Criteria
```
[ ] dotnet build exits 0, zero errors, zero new warnings
[ ] SCAN-04: IsNullOrWhiteSpace present in CallAtmStrategyCreate body
[ ] SCAN-05: "B46 ATM error" present (1 match)
[ ] SCAN-06: "B42 ATM error" absent (0 matches)
[ ] SCAN-01 through SCAN-03: 0 matches each
[ ] T_B46_01 xUnit [Fact] test green
[ ] T_B46_02 xUnit [Fact] test green
```

---

## T2 — TradeCopierPanel ComboBox Auto-Select Wiring

### Ticket Metadata
| Field | Value |
|-------|-------|
| **Ticket ID** | T2 |
| **Title** | TradeCopierPanel ComboBox Auto-Select Wiring |
| **Spec Req ID** | DW-B46-COMBO-AUTOSELECT-02 |
| **File** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| **Change Type** | Block insertion (after existing line) |
| **CYC Before** | 4 |
| **CYC After** | 7 |

### Problem Statement
`OnFollowerAtmTemplateComboLoaded` sets `cb.SelectedIndex = defaultIdx` programmatically but
does NOT write `item.AtmModeName`. WPF `SelectionChangedEvent` is not reliably fired at
`DataTemplate` load time. `item.AtmModeName` stays `"Inherit"`. User clicks Apply without
touching the ComboBox → `OnApplyRule` reads `"Inherit"` → empty `atmTemplate` → triggers
DW-B46-ATM-EMPTY-GUARD-01 chain.

### Method to Find
```
Search for: private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
```

### Insertion Point
Find the line `cb.SelectedIndex = defaultIdx;` — it is the **last statement** in the method
(immediately before the closing `}`). Insert the following block **after** that line.

### Before (context — the last 3 lines of the method before the closing brace)
```csharp
    cb.SelectedIndex = defaultIdx;
}
```

### After (exact replacement — write verbatim)
```csharp
    cb.SelectedIndex = defaultIdx;
    // B46 T2: write item.AtmModeName immediately on auto-select so OnApplyRule
    // picks up Named mode without requiring a manual ComboBox interaction.
    // defaultIdx == 0 means "(none)" was selected — leave AtmModeName as "Inherit".
    if (defaultIdx > 0)
    {
        var selName = cb.Items[defaultIdx] as string;
        if (!string.IsNullOrEmpty(selName))
        {
            var item = (cb.DataContext as FollowerItem)
                       ?? FindAncestorDataContext<FollowerItem>(cb);
            if (item != null)
                item.AtmModeName = "Named:" + selName;
        }
    }
}
```

**Notes for engineer**:
- The only change is appending the `if (defaultIdx > 0)` block before the method's closing brace.
- `FindAncestorDataContext<FollowerItem>` is an existing helper method in `TradeCopierPanel.cs` — no new helper needed.
- `FollowerItem.AtmModeName` is a `string` property; the `"Named:" + selName` format matches the write pattern in `OnFollowerAtmTemplateComboChanged` exactly.
- This handler fires on the WPF UI thread. No `Dispatcher.InvokeAsync` needed.
- `TradeCopierWindow.cs` must NOT be touched — verify with SCAN-07 below.

### CYC Analysis
| State | CYC | Branches |
|-------|-----|---------|
| Before | 4 | null guard, idempotency guard, foreach loop, leader-found match |
| After | 7 | + `defaultIdx > 0` (5), `!string.IsNullOrEmpty(selName)` (6), `item != null` (7) |
| Limit | ≤ 8 | ✓ |

### Jane Street Compliance
| Rule | Status | Notes |
|------|--------|-------|
| JS-001 (no throw in hot path) | PASS | No throw; outer try/catch remains; new block has no throw |
| JS-002 (no return null) | PASS | No return null; `FindAncestorDataContext` returns `default(T)` checked via `!= null` |
| JS-021 (no lock) | PASS | No lock; all operations on WPF UI thread; `AtmModeName` written/read on UI thread only |
| JS-033 (no async void) | PASS | `private void` event handler, synchronous |

### NT8 Compiler Compliance
| Rule | Status | Notes |
|------|--------|-------|
| NT8-001 (no `init` setters) | PASS | No new properties |
| NT8-012 (FrameworkElementFactory Loaded pattern) | PASS | Appending to existing Loaded handler; no FEF changes |
| NT8-019 (no `async void`) | PASS | Synchronous void |
| NT8-042 (`Dispatcher.InvokeAsync` unavailable) | N/A | Handler fires on UI thread; no Dispatcher needed |
| NT8-043 (no null-conditional compound assignment) | PASS | No `?.Event -=` patterns |

### xUnit Tests Made Green by T2
- `T_B46_03` — `"Named:MES $200 SL5"` written by auto-select round-trips through `CopyEngine.ParseAtmModeName` to `Named` mode correctly

### T2 — 7-SCAN CHECKLIST

Engineer MUST run all 7 scans from the Wave workspace root
(`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`) before committing T2.

```
SCAN-01  grep -n "lock\s*("             TradeCopierPanel.cs
         EXPECTED: 0 matches

SCAN-02  grep -n "async void"           TradeCopierPanel.cs
         EXPECTED: 0 matches

SCAN-03  grep -n "return null"          TradeCopierPanel.cs  (scope: OnFollowerAtmTemplateComboLoaded)
         EXPECTED: 0 matches in OnFollowerAtmTemplateComboLoaded

SCAN-04  grep -n "B46 T2"              TradeCopierPanel.cs
         EXPECTED: >= 1 match (comment block present)

SCAN-05  grep -n "AtmModeName.*Named:" TradeCopierPanel.cs
         EXPECTED: >= 2 matches (OnFollowerAtmTemplateComboChanged + new T2 block)

SCAN-06  python scripts/complexity_audit.py TradeCopierPanel.cs
         EXPECTED: OnFollowerAtmTemplateComboLoaded CYC=7, <= 8

SCAN-07  git diff TradeCopierWindow.cs
         EXPECTED: 0 lines changed (TradeCopierWindow.cs must be UNTOUCHED)
```

### T2 — Build Pass Criteria
```
[ ] dotnet build exits 0, zero errors, zero new warnings
[ ] SCAN-04: "B46 T2" comment present in TradeCopierPanel.cs
[ ] SCAN-05: >= 2 "AtmModeName.*Named:" assignments
[ ] SCAN-06: OnFollowerAtmTemplateComboLoaded CYC=7, <= 8
[ ] SCAN-07: TradeCopierWindow.cs unchanged (git diff returns 0 lines)
[ ] T_B46_03 xUnit [Fact] test green
```

---

## T3 — CopyEngine Build Tag Update

### Ticket Metadata
| Field | Value |
|-------|-------|
| **Ticket ID** | T3 |
| **Title** | CopyEngine Build Tag Update |
| **Spec Req ID** | DW-B46-ATM-EMPTY-GUARD-01, DW-B46-COMBO-AUTOSELECT-02 (block-level provenance) |
| **File** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| **Change Type** | Single const string replacement |
| **CYC Delta** | 0 (no logic change) |

### Problem Statement
Build tag must be updated to reflect current block number and feature name for provenance
tracing in NT8 Output window and automated log parsing.

### Symbol to Find
```
Search for: internal const string Tag =
In class: PttBuild (internal static class, approx line 39 of CopyEngine.cs)
```

### Before (verify exact current value in file before replacing)
```csharp
internal const string Tag = "PTT-COPIER B43 | atm-template-picker | 2026-08-05";
```

### After (exact replacement)
```csharp
internal const string Tag = "PTT-COPIER B46 | atm-template-guard | 2026-08-06";
```

**Notes for engineer**:
- Read `CopyEngine.cs` first and confirm the exact current `Tag` value before replacing.
  The "before" value above is the last known value; it may differ if an intermediate block ran.
  The "after" value is authoritative regardless of what the current value is.
- Only the `Tag` constant line must change. No other lines in `CopyEngine.cs` may be touched.
- ASCII-only: all characters in the new string are ASCII (no Unicode, no curly quotes).

### Jane Street Compliance
| Rule | Status | Notes |
|------|--------|-------|
| JS-001 | PASS | No code logic, const string only |
| JS-002 | PASS | No return null |
| JS-021 | PASS | No lock |
| JS-033 | PASS | No method added |

### NT8 Compiler Compliance
| Rule | Status | Notes |
|------|--------|-------|
| All rules | PASS | Const string replacement; no new language constructs |

### xUnit Tests Made Green by T3
None — T3 is a cosmetic provenance update with no testable predicate.

### T3 — 7-SCAN CHECKLIST

Engineer MUST run all 7 scans from the Wave workspace root
(`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`) before committing T3.

```
SCAN-01  grep -n "lock\s*("            CopyEngine.cs
         EXPECTED: 0 new matches (existing matches, if any, unchanged)

SCAN-02  grep -n "async void"          CopyEngine.cs
         EXPECTED: 0 new matches

SCAN-03  grep -n "return null"         CopyEngine.cs
         EXPECTED: 0 new matches

SCAN-04  grep -n "PTT-COPIER B46"      CopyEngine.cs
         EXPECTED: 1 match (new Tag constant)

SCAN-05  grep -n "PTT-COPIER B43"      CopyEngine.cs
         EXPECTED: 0 matches (old tag removed)

SCAN-06  grep -n "PTT-COPIER B44\|PTT-COPIER B45" CopyEngine.cs
         EXPECTED: 0 matches (no intermediate tags)

SCAN-07  git diff CopyEngine.cs
         EXPECTED: only the Tag constant line changed (no other diffs)
```

### T3 — Build Pass Criteria
```
[ ] dotnet build exits 0, zero errors, zero new warnings
[ ] SCAN-04: "PTT-COPIER B46" present (1 match)
[ ] SCAN-05: "PTT-COPIER B43" absent (0 matches)
[ ] SCAN-07: git diff shows only the Tag line changed
```

---

## T4 — B46Tests.cs (New File)

### Ticket Metadata
| Field | Value |
|-------|-------|
| **Ticket ID** | T4 |
| **Title** | B46Tests.cs New File |
| **Spec Req ID** | DW-B46-ATM-EMPTY-GUARD-01, DW-B46-COMBO-AUTOSELECT-02 |
| **File** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B46Tests.cs` (NEW — does not exist yet) |
| **Change Type** | New file creation |
| **Framework** | xUnit ONLY — no NUnit, no MSTest |
| **NT8 Runtime** | ZERO NT8 API calls |

### Dependency
T4 depends on T1 being complete (guard predicate wired in production code for conceptual
correctness) and T2 being complete (ParseAtmModeName accessible). Write T4 last.

### Complete File Content (write verbatim — no paraphrasing)

```csharp
// B46Tests.cs
// Block: PTT-COPIER-B46
// Spec: DW-B46-ATM-EMPTY-GUARD-01, DW-B46-COMBO-AUTOSELECT-02
// Tests: T_B46_01 through T_B46_03
// Framework: xUnit only (no NUnit, no MSTest)
// NT8-runtime-free: zero NT8 API calls

using System;
using Xunit;

namespace PropTraderTools
{
    public sealed class B46Tests
    {
        // T_B46_01 — Empty AtmTemplateName triggers the guard (IsNullOrWhiteSpace = true).
        // Guard fires → AtmStrategyCreate is skipped → strategy stays alive.
        // Spec: DW-B46-ATM-EMPTY-GUARD-01
        [Fact]
        public void T_B46_01_EmptyAtmTemplateName_GuardFires()
        {
            var args = FillSignalEventArgs.Create(
                null, null,
                string.Empty,
                NinjaTrader.Cbi.OrderAction.Buy,
                8,
                "ORD-B46-001");

            // Guard predicate: string.IsNullOrWhiteSpace(args.AtmTemplateName)
            // This is exactly what production CallAtmStrategyCreate evaluates.
            Assert.True(string.IsNullOrWhiteSpace(args.AtmTemplateName));
        }

        // T_B46_02 — Non-empty AtmTemplateName does NOT trigger the guard.
        // Guard does not fire → AtmStrategyCreate is called with the template name.
        // Spec: DW-B46-ATM-EMPTY-GUARD-01 (negative / pass-through case)
        [Fact]
        public void T_B46_02_NonEmptyAtmTemplateName_GuardDoesNotFire()
        {
            var args = FillSignalEventArgs.Create(
                null, null,
                "MES $200 SL5",
                NinjaTrader.Cbi.OrderAction.Buy,
                8,
                "ORD-B46-002");

            Assert.False(string.IsNullOrWhiteSpace(args.AtmTemplateName));
            Assert.Equal("MES $200 SL5", args.AtmTemplateName);
        }

        // T_B46_03 — "Named:MES $200 SL5" (written by auto-select) round-trips
        //            through CopyEngine.ParseAtmModeName to Named mode correctly.
        //            Validates the serialisation format is consistent end-to-end.
        // Spec: DW-B46-COMBO-AUTOSELECT-02
        [Fact]
        public void T_B46_03_ComboAutoSelectFormat_ParsesAsNamedMode()
        {
            string written = "Named:MES $200 SL5";

            var mode = CopyEngine.ParseAtmModeName(written);

            var named = Assert.IsType<FollowerAtmMode.Named>(mode);
            Assert.Equal("MES $200 SL5", named.TemplateName);
        }
    }
}
```

**Notes for engineer**:
- File goes in `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B46Tests.cs` — same directory as `B42Tests.cs`, `B43Tests.cs`, etc.
- Namespace is `PropTraderTools.Tests` (matches pattern of prior test files in this project).
- `CopyEngine.ParseAtmModeName` is `internal static` — accessible from same assembly. No `InternalsVisibleTo` attribute needed since tests are in the same project.
- `FillSignalEventArgs.Create` factory is the existing production factory method; use it as-is.
- `FollowerAtmMode.Named` is the existing discriminated union case; no new types created.
- All strings are ASCII-only (no Unicode, no curly quotes).

### xUnit Tests in This File
| Test Name | Spec ID | What It Asserts |
|-----------|---------|----------------|
| `T_B46_01_EmptyAtmTemplateName_GuardFires` | DW-B46-ATM-EMPTY-GUARD-01 | `string.IsNullOrWhiteSpace(args.AtmTemplateName)` returns `true` when template is empty |
| `T_B46_02_NonEmptyAtmTemplateName_GuardDoesNotFire` | DW-B46-ATM-EMPTY-GUARD-01 | `string.IsNullOrWhiteSpace(args.AtmTemplateName)` returns `false` for non-empty template |
| `T_B46_03_ComboAutoSelectFormat_ParsesAsNamedMode` | DW-B46-COMBO-AUTOSELECT-02 | `CopyEngine.ParseAtmModeName("Named:MES $200 SL5")` returns `FollowerAtmMode.Named` with `TemplateName == "MES $200 SL5"` |

### T4 — 7-SCAN CHECKLIST

Engineer MUST run all 7 scans from the Wave workspace root
(`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`) before committing T4.

```
SCAN-01  grep -n "using Xunit"        B46Tests.cs
         EXPECTED: present (>= 1 match)

SCAN-02  grep -n "NUnit\|MSTest"      B46Tests.cs
         EXPECTED: 0 matches (xUnit only)

SCAN-03  grep -c "\[Fact\]"           B46Tests.cs
         EXPECTED: 3 (exactly 3 [Fact] methods)

SCAN-04  grep -n "Account.All"        B46Tests.cs
         EXPECTED: 0 matches (NT8-runtime-free)

SCAN-05  grep -n "AtmTemplateName"    B46Tests.cs
         EXPECTED: >= 3 matches

SCAN-06  dotnet test (all 3 [Fact] tests pass)
         EXPECTED: T_B46_01, T_B46_02, T_B46_03 all green

SCAN-07  grep -n "lock\s*("           B46Tests.cs
         EXPECTED: 0 matches
```

### T4 — Build Pass Criteria
```
[ ] dotnet build exits 0, zero errors, zero new warnings
[ ] SCAN-01: "using Xunit" present
[ ] SCAN-02: 0 NUnit/MSTest references
[ ] SCAN-03: exactly 3 [Fact] methods
[ ] SCAN-04: 0 Account.All references (NT8-runtime-free)
[ ] SCAN-06: all 3 tests green in dotnet test
[ ] SCAN-07: 0 lock() occurrences
```

---

## Cross-Ticket Acceptance Gate

After all 4 tickets are committed and `scripts\verify_links.ps1 -Fix` has run:

```
[ ] dotnet build exits 0, zero errors, zero new warnings
[ ] dotnet test exits 0 — T_B46_01, T_B46_02, T_B46_03 all green
[ ] FINAL-01: grep -n "B42 ATM error"  Features/PttFollowerStrategy.cs → 0 matches
[ ] FINAL-02: grep -n "B46 ATM error"  Features/PttFollowerStrategy.cs → 1 match
[ ] FINAL-03: grep -n "PTT-COPIER B46" CopyEngine.cs → 1 match
[ ] FINAL-04: grep -n "B46 T2"         TradeCopierPanel.cs → >= 1 match
[ ] FINAL-05: git diff TradeCopierWindow.cs → 0 lines changed
[ ] FINAL-06: B46Tests.cs exists, exactly 3 [Fact] methods, 0 lock(), 0 NUnit/MSTest
[ ] FINAL-07: powershell -File scripts\verify_links.ps1 -Fix exits 0
```

**DW-B42-05 acceptance test (live F5) is UNBLOCKED after this gate passes.**
Run per §9 of the architecture plan: configure PTTFollowerStrategy with Sim101, select ATM template, click Apply, fire test trade, verify D1–D6.

---

## Files NOT in Scope (Do Not Touch)

The following files MUST NOT be modified in B46:

- `PttContracts.cs`
- `TradeCopierWindow.cs`
- `PttBreakEven.cs`
- `PttGlobalBreakEven.cs`
- `CopyEngineTests.cs`
- Any other `.cs` file not listed in the ticket table above

---

*Tickets complete. Four tickets covering all B46 spec requirements. All 7-scan checklists present. All xUnit test names specified. All JS-001/002/021/033 and NT8 compliance notes included.*
