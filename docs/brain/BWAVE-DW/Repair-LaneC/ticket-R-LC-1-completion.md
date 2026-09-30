# Ticket R-LC-1 Completion — BWAVE-DW-REPAIR-LANEC

**Engineer**: ptt-engineer (Phase 4a)
**Ticket**: R-LC-1 — ApplyFeatureFlags inside Dispatcher.InvokeAsync lambda in RefreshRuleRows()
**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Branch**: `feature/bwave-dw-lane-c`
**Date**: 2026-08-20

---

## What Was Implemented

Added ONE line inside the `Dispatcher.InvokeAsync` lambda in `RefreshRuleRows()` at line 173
(after `_rulesPanel.Children.Add(BuildRuleRow(instr));`, before `});`):

```csharp
ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built
```

**Line 153** (`ApplyFeatureFlags(CopyEngine.Instance.Flags)` inside `OnLoaded`) was NOT touched — confirmed present and unchanged.

### Diff (1 line added)

```diff
             Dispatcher.InvokeAsync(() =>
             {
                 _rulesPanel.Children.Clear();
                 foreach (var instr in instruments) // CYC branch (2): iterate instruments
                     _rulesPanel.Children.Add(BuildRuleRow(instr));
+                ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built
             });
```

---

## 7-Scan Results

| # | Scan | Command | Result | PASS/FAIL |
|---|------|---------|--------|-----------|
| SCAN-01 | No new `lock()` | `Select-String -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//" }` | **Count: 0** | PASS |
| SCAN-02 | No `async void` method declarations | `Select-String -Pattern "async void"` | **Count: 2** (both in comments only — pre-existing, no method declarations) | PASS |
| SCAN-03 | `return null` count (pre-existing note) | `Select-String -Pattern "return null"` | **Count: 6** (all pre-existing, none introduced by this ticket) | PASS |
| SCAN-04 | `throw new` count (pre-existing note) | `Select-String -Pattern "throw new"` | **Count: 1** (pre-existing, not introduced by this ticket) | PASS |
| SCAN-05 | CYC ≤ 8 — `RefreshRuleRows` | `lizard TradeCopierWindow.cs --CCN 1` | `RefreshRuleRows` CYC=2 (lizard) — no new branches added by method call | PASS |
| SCAN-06 | ASCII-only (non-ASCII bytes) | `[System.IO.File]::ReadAllBytes(...) | Where-Object { $_ -gt 127 }` | **Count: 0** | PASS |
| SCAN-07 | No NUnit/MSTest imports or attributes | `Select-String -Pattern "using NUnit|using MSTest|\[Test\]|\[TestMethod\]"` | **Count: 0** | PASS |

**All 7 scans: ZERO violations.**

---

## NT8 Sync Output

```
OK       Features\PttCancel.cs
OK       Features\PttCopier.cs
OK       Features\PttFlatten.cs
OK       Features\PttFollowerStrategy.cs
OK       Features\PttGlobalBreakEven.cs
OK       Features\PttGlobalQuickExit.cs
OK       Features\PttQuickExit.cs
OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===
```

**Result**: 18/18 OK — 0 MISMATCH ✅

---

## Build Output

```
dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal

1 Warning(s)   [pre-existing xUnit2004 — unrelated to this change]
0 Error(s)
Time Elapsed 00:00:02.88
```

**Result**: 0 errors ✅

---

## Acceptance Criteria Status

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | `ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built` is the final statement inside the `Dispatcher.InvokeAsync(() => { ... })` lambda in `RefreshRuleRows()` | PASS |
| AC-2 | Existing `ApplyFeatureFlags(CopyEngine.Instance.Flags)` at line 153 inside `OnLoaded` is present and unchanged | PASS |
| AC-3 | `RefreshRuleRows()` CYC remains ≤ 8 (lizard reports CYC=2; no new branches) | PASS |
| AC-4 | All 7 scans pass | PASS |
| AC-5 | `dotnet build` passes with zero errors | PASS |
| AC-6 | SIM gate — Starter license + persisted rules: Arm BE and Tighten buttons disabled after startup | PENDING (requires live NT8 host + F5 compile) |

---

## Verdict

**BUILD_PASS**
