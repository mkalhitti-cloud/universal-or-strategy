# Ticket R-LC-1 Verification — BWAVE-DW-REPAIR-LANEC

**Verifier**: ptt-verifier (Phase 4b)
**Ticket**: R-LC-1 — ApplyFeatureFlags inside Dispatcher.InvokeAsync lambda in RefreshRuleRows()
**File verified**: `src/PropTraderTools/TradeCopierWindow.cs`
**Branch**: `feature/bwave-dw-lane-c`
**Date**: 2026-08-20
**Engineer report**: `docs/brain/BWAVE-DW/Repair-LaneC/ticket-R-LC-1-completion.md`

---

## Independent Scan Results (Layer 3 — Verifier)

All 7 scans run independently. Engineer Layer 2 results cross-checked below.

### SCAN-01: No `lock()` calls

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//" }`
**Result**: **0 matches**
**Engineer reported**: Count: 0
**Cross-check**: MATCH — PASS

### SCAN-02: No `async void` method declarations

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "async void"`
**Result**: **2 matches — BOTH in comments only** (lines 158, 581). No method declarations.
  - Line 158: `// JS-021: no lock. JS-033: private void (not async void).`
  - Line 581: `// All helpers: private instance, UI-thread only, CYC <= 2, no lock(), no async void, no return null.`
**Engineer reported**: Count: 2 (both in comments only)
**Cross-check**: MATCH — PASS

### SCAN-03: `return null` count

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "return null" | Measure-Object`
**Result**: **Count: 6** — confirmed at lines 291, 316, 330, 581 (comments), 1131, 1138 (actual returns).
  - None are in RefreshRuleRows (lines 161-175) or any new code introduced by R-LC-1.
**Engineer reported**: Count: 6 (all pre-existing)
**Cross-check**: MATCH — PASS (all pre-existing, zero introduced by this ticket)

### SCAN-04: `throw new` count

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "throw new" | Measure-Object`
**Result**: **Count: 1** — line 874: `throw new NotImplementedException("AccountDisplayConverter is one-way only")`.
  - Not in RefreshRuleRows or any new code introduced by R-LC-1.
**Engineer reported**: Count: 1 (pre-existing)
**Cross-check**: MATCH — PASS (pre-existing, zero introduced by this ticket)

### SCAN-05: CYC analysis — RefreshRuleRows

**Command**: `lizard src/PropTraderTools/TradeCopierWindow.cs --CCN 1`
**Result**: `TradeCopierWindow::RefreshRuleRows@161-175` — **CYC=2** (lizard)
  - Note: complexity_audit.py not present in scripts/ directory. Lizard used as authoritative substitute.
  - Branches: (1) `instruments.Count == 0` guard, (2) `foreach` inner loop on instruments.
  - The `ApplyFeatureFlags(...)` call adds zero branches — CYC unchanged.
**Engineer reported**: CYC=2 (lizard) — no new branches
**Cross-check**: MATCH — PASS (CYC=2, well within <=8)

### SCAN-06: Non-ASCII byte check

**Command**: `[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierWindow.cs") | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object Count`
**Result**: **Count: 0**
**Engineer reported**: Count: 0
**Cross-check**: MATCH — PASS

### SCAN-07: No NUnit/MSTest imports or test attributes

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "using NUnit|using MSTest|\[Test\]|\[TestMethod\]"`
**Result**: **0 matches**
**Engineer reported**: Count: 0
**Cross-check**: MATCH — PASS

---

## Specific Fact Verification

### FACT-1: ApplyFeatureFlags inside Dispatcher.InvokeAsync lambda

**Required**: `ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built`
appears INSIDE the `Dispatcher.InvokeAsync` lambda, AFTER the `BuildRuleRow` loop, BEFORE the closing `});`.

**Verified at line 173** (source read, lines 161-175):
```
168:            Dispatcher.InvokeAsync(() =>
169:            {
170:                _rulesPanel.Children.Clear();
171:                foreach (var instr in instruments) // CYC branch (2): iterate instruments
172:                    _rulesPanel.Children.Add(BuildRuleRow(instr));
173:                ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built
174:            });
175:        }
```

**Status**: CONFIRMED — comment text exactly matches ticket specification. Call is INSIDE lambda, after BuildRuleRow loop, before closing `});`. PASS

### FACT-2: Original ApplyFeatureFlags at line 153 retained

**Required**: The existing `ApplyFeatureFlags(CopyEngine.Instance.Flags)` at line ~153 inside `OnLoaded` is present and unchanged.

**Verified at line 153** (source read, lines 140-155):
```
152:            // BGTM-1: subscribe to flag changes, apply current flags, populate key display
153:            CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
154:            ApplyFeatureFlags(CopyEngine.Instance.Flags);
155:        }
```
Wait — line 153 is `CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;` and line 154 is `ApplyFeatureFlags(CopyEngine.Instance.Flags);`. The call is at line 154, not 153. This is a 1-line offset from the ticket description (ticket says "line 153"). The call is present and unchanged.

**Status**: CONFIRMED — `ApplyFeatureFlags(CopyEngine.Instance.Flags)` is present at line 154 (1-line offset from ticket reference of ~153 — the `//` comment on 152 shifted numbering slightly). The call is fully intact and unmodified. PASS

### FACT-3: No new lock() calls introduced

**Verified by SCAN-01**: 0 `lock(` calls anywhere (excluding comments). PASS

---

## Build and Sync Verification

### NT8 Sync

**Command**: `powershell -File scripts\ptt-sync-and-verify.ps1 2>&1 | Select-Object -Last 8`
**Result**:
```
OK       Features\PttTrim.cs

=== SYNC + VERIFY: PASS (18 files confirmed) ===

NEXT STEP (MANDATORY):
  Press F5 in NinjaTrader 8, or go to:
  Tools -> Edit NinjaScript -> Compile
  File copy alone does NOT activate the new code.
```
**Status**: 18/18 OK — 0 MISMATCH — PASS

### dotnet build

**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj --verbosity minimal 2>&1 | Select-Object -Last 5`
**Result**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.28
```
**Note**: Engineer reported 1 pre-existing xUnit2004 warning. Independent build shows 0 warnings. Both agree: 0 errors.
**Status**: PASS

---

## Architecture Compliance

- **Spec requirement**: DW-C39-05b / B4 — `ApplyFeatureFlags` must be called inside the `Dispatcher.InvokeAsync` lambda after rows are rebuilt.
- **Architecture plan (02-architecture-plan.md)**: R-LC-1 is listed as "Add ApplyFeatureFlags call inside Dispatcher.InvokeAsync lambda" — CONFIRMED.
- **Change type**: 1 line added inside existing lambda — matches ticket specification exactly.
- **No new methods, classes, or fields introduced** — CONFIRMED.
- **Threading model**: `ApplyFeatureFlags` runs on the UI thread (inside the Dispatcher lambda) — same thread context as the pre-existing line 154 call. SAFE.

---

## Acceptance Criteria

| AC | Description | Verifier Status |
|----|-------------|-----------------|
| AC-1 | `ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built` is final statement inside lambda in `RefreshRuleRows()` | PASS — confirmed at line 173 |
| AC-2 | Existing `ApplyFeatureFlags(CopyEngine.Instance.Flags)` at line ~153 inside `OnLoaded` present and unchanged | PASS — confirmed at line 154 (1-line offset) |
| AC-3 | `RefreshRuleRows()` CYC remains <=8 | PASS — CYC=2 (lizard) |
| AC-4 | All 7 scans pass | PASS — all 7 scans independently confirmed |
| AC-5 | `dotnet build` passes with zero errors | PASS — 0 errors, 0 warnings |
| AC-6 | SIM gate — Starter license + persisted rules | PENDING (requires live NT8 host — out of scope for static verification) |

---

## Discrepancies Between Engineer Layer 2 and Verifier Layer 3

| Item | Engineer | Verifier | Verdict |
|------|----------|----------|---------|
| SCAN-01 lock | 0 | 0 | MATCH |
| SCAN-02 async void | 2 comments | 2 comments | MATCH |
| SCAN-03 return null | 6 pre-existing | 6 pre-existing | MATCH |
| SCAN-04 throw new | 1 pre-existing | 1 pre-existing | MATCH |
| SCAN-05 CYC | CYC=2 (lizard) | CYC=2 (lizard) | MATCH |
| SCAN-06 non-ASCII | 0 | 0 | MATCH |
| SCAN-07 test framework | 0 | 0 | MATCH |
| Build warnings | 1 (xUnit2004) | 0 | MINOR DISCREPANCY — both agree on 0 errors; warning may have been fixed in a prior unrelated commit. Non-blocking. |
| Build errors | 0 | 0 | MATCH |
| NT8 sync | 18/18 OK | 18/18 OK | MATCH |

**No substantive discrepancies. One minor build-warning count difference (1 vs 0) — both report 0 errors, no impact.**

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: 0 lock() in non-comment lines | PASS |
| JS-033 (no async void) | SCAN-02: 0 async void method declarations | PASS |
| JS-001 (no throw in hot path) | SCAN-04: 1 throw, pre-existing in converter (line 874), not in new code | PASS |
| JS-002 (no return null) | SCAN-03: 6 returns, pre-existing, none in RefreshRuleRows | PASS |
| ASCII-only | SCAN-06: 0 non-ASCII bytes | PASS |
| CYC <= 8 | SCAN-05: RefreshRuleRows CYC=2 | PASS |
| NT8 Dispatcher | ApplyFeatureFlags runs on UI thread inside InvokeAsync lambda | PASS |
| No sealed on Window | TradeCopierWindow not sealed (not changed by this ticket) | PASS |
| No FontFamily= | Not applicable (no new WPF elements added) | PASS |
| No #RRGGBB hex color | Not applicable (no new color strings added) | PASS |
| No DateTime.Now | Not applicable (no datetime usage added) | PASS |

---

## Verdict

**VERIFY_PASS**

All 7 independent scans confirm zero violations. The implementation exactly matches the ticket specification. `ApplyFeatureFlags(CopyEngine.Instance.Flags); // DW-C39-05b: apply flags after rows are built` is correctly placed at line 173 inside the `Dispatcher.InvokeAsync` lambda after the `BuildRuleRow` loop. The original line-154 call in `OnLoaded` is retained intact. Build passes with 0 errors. NT8 sync is 18/18 OK. All AC-1 through AC-5 confirmed PASS. AC-6 (SIM gate) is PENDING — requires live NT8 host.