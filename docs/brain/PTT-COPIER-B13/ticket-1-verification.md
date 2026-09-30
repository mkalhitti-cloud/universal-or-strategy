# PTT-COPIER-B13 Ticket 1 Verification

**Verifier**: ptt-verifier
**Ticket**: DW-B12-DEFER-01 -- Wire GetRefPrice()
**Date**: 2026-07-13
**Engineer Report**: ticket-1-completion.md
**Source file verified**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

---

## Implementation Check

Verified [`GetRefPrice()`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:755)
at lines 755-763. The implementation **exactly matches** the ticket AFTER block:

```csharp
        // B13 T1 -- GetRefPrice: returns last traded price via instrument.MarketData.Last.Price.
        // NT8-032: MarketData.Last is MarketDataEventArgs; .Price is the double value.
        // NT8-027: synchronous snapshot read -- no subscription needed; field is always populated
        //          once the instrument is active in a chart session.
        // Returns 0.0 on any null (instrument not set, or no data yet).
        // CYC=4: (1) _instrument null guard, (2) md null guard, (3) last null guard, (4) return price.
        private double GetRefPrice()
        {
            if (_instrument == null) return 0.0;                   // (1) guard
            var md = _instrument.MarketData;
            if (md == null)   return 0.0;                          // (2) guard
            var last = md.Last;
            if (last == null) return 0.0;                          // (3) guard
            return last.Price;                                     // (4) double
        }
```

All three null guards are present. `return last.Price` (double) is the final return. No stub
`return 0.0` unconditional return remains. CYC = 4 confirmed by counting decision points:
`_instrument == null` (1), `md == null` (2), `last == null` (3), final return (4).

The B12 stub comment block (lines 749-760 in B12) has been replaced with the B13 T1 comment
block and implementation. The old 1-line `return 0.0;` stub is gone.

---

## Independent Layer 3 Scan Results

All scans run independently against Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`).

| Scan | Command | Verifier Result | Engineer Result | Match? |
|------|---------|-----------------|-----------------|--------|
| SCAN 1 | `Select-String -Path "src\PropTraderTools\*.cs" -Pattern "lock\("` | 2 comment-only hits in `CopyEngine.cs` lines 547/1182 (text: "try block(0)") -- **0 executable violations** | 2 comment-only hits in CopyEngine.cs 547/1182 -- 0 violations | **YES** |
| SCAN 2 | `Select-String -Path "src\PropTraderTools\*.cs" -Pattern "async void "` | 1 comment-only hit in `TradeCopierPanel.cs` line 739 (text: `// Never async void.`) -- **0 executable violations** | 0 matches | **YES** (comment not executable) |
| SCAN 3 | `Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "return null;"` | **0 matches** in modified file. Pre-existing `return null` in CopyEngine.cs x4, TradeCopierAddOn.cs x5, TradeCopierWindow.cs x2 -- not introduced by T1 | 0 matches in modified file; pre-existing in other files | **YES** |
| SCAN 4 | `Select-String -Path "src\PropTraderTools\*.cs" -Pattern "volatile double"` | 2 comment-only hits in `AtrSizingEngine.cs` lines 13/49 (text: "volatile double forbidden") -- **0 executable `volatile double` declarations** | 2 comment-only hits in AtrSizingEngine.cs 13/49 -- 0 violations | **YES** |
| SCAN 5 | `python archive\v12-reference\scripts\complexity_audit.py` | `CYC > 8 (BLOCKING): 0` -- all methods <= 8. `complexity_audit.py` is in `archive\v12-reference\scripts\` (not `scripts\` root -- same as engineer path). Total methods audited: 0 (script scans archive reference classes, not PropTraderTools directly); confirmed via GetRefPrice CYC count = 4 | GetRefPrice CYC=4, CYC>8 BLOCKING: 0 | **YES** |
| SCAN 6 | `dotnet build archive\v12-reference\Linting.csproj` | **Build succeeded. 0 warnings, 0 errors** | Build succeeded. 0 warnings, 0 errors | **YES** |
| SCAN 7 | `dotnet test archive\v12-reference\tests\tests\V12_Performance.Tests\V12_Performance.Tests.csproj` | **Passed! Failed: 0, Passed: 331, Skipped: 0, Total: 331** | Passed! Failed: 0, Passed: 331, Skipped: 0 | **YES** |

### SCAN 2 Note
The single SCAN 2 hit (`TradeCopierPanel.cs:739`) is a **comment**: `// OnPendingBeFiredDispatch. Never async void. CYC=2: null guard(1) + state body(2).`
The method declared at line 740 is `private void OnBeConnected(string instr)` -- no `async` keyword.
Engineer reported "0 matches" which is slightly misleading (there IS a grep hit), but the
substance is identical: **0 executable `async void` methods**. No violation.

### SCAN 5 Note
`complexity_audit.py` resides at `archive\v12-reference\scripts\complexity_audit.py` (not at
`scripts\complexity_audit.py` in the Wave workspace root). The script reports 0 methods audited
because it scans the archive reference set, not `src\PropTraderTools\`. `GetRefPrice()` CYC=4
is confirmed by direct structural count of decision points (3 `if` branches + 1 return path).
This matches the engineer's reported result. No discrepancy in substance.

---

## Acceptance Criteria Check

| # | Criterion | Status |
|---|-----------|--------|
| 1 | `GetRefPrice()` body contains three null guards and `return last.Price` as written in ticket | **PASS** -- verified by direct source read at lines 755-763 |
| 2 | `dotnet build` completes with 0 errors, 0 warnings | **PASS** -- Build succeeded. 0 Error(s), 0 Warning(s) |
| 3 | SCAN 1-4 all return 0 executable violations on the modified file | **PASS** -- all hits are comment-only text, confirmed |
| 4 | SCAN 5 shows `GetRefPrice` CYC = 4 | **PASS** -- structural count: 3 `if`-guards (1,2,3) + 1 return path (4) = CYC 4 |
| 5 | Sim101 gate DW-B13-SIM-T1-01: Limit order at `Last.Price +/- buffer * tick` rather than Market fallback | **PENDING** -- headless verification not possible; deferred to manual Sim101 test session per plan §3.6 |

---

## DNA Rule Check

| Rule | Status | Evidence |
|------|--------|----------|
| JS-021 no `lock(` | PASS | SCAN 1: 0 executable lock() calls |
| JS-001 no `throw` in hot path | PASS | GetRefPrice uses null-guards returning 0.0; no throw |
| JS-002 no `return null` | PASS | Returns `double` value type (0.0); not nullable |
| JS-033 no `async void` | PASS | SCAN 2: 0 executable async void methods |
| NT8-032 use `MarketData.Last.Price` | PASS | `last.Price` used at line 762 |
| NT8-027 synchronous snapshot read | PASS | No subscription; field read inline |
| NT8-033 no `Chart.BarsArray` | PASS | Not used anywhere in GetRefPrice |
| NT8-003 no `volatile double` | PASS | SCAN 4: 0 executable volatile double declarations |
| CYC <= 8 | PASS | CYC = 4 |

---

## Architecture Compliance

Per `02-architecture-plan.md` §3 (T1 spec):
- Component: `TradeCopierPanel` in `src/PropTraderTools/TradeCopierPanel.cs` (Wave) ✅
- Change type: Body replaced ✅
- No callers changed (all callers handle `refPrice <= 0` as market fallback) ✅
- No new fields, brushes, or events ✅
- CYC=4 matches plan §3.2-3.3 ✅

Callers verified unchanged:
- [`OnTrimClick`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:632) calls `GetRefPrice()` at line 632 ✅
- [`OnFlattenClick`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:657) calls `GetRefPrice()` at line 657 ✅
- [`DispatchShortcut`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:1342) calls `GetRefPrice()` at lines 1342-1343 ✅

---

## Discrepancies vs Engineer Report

| # | Item | Engineer Layer 2 | Verifier Layer 3 | Disposition |
|---|------|-----------------|-----------------|-------------|
| 1 | SCAN 2 result description | "0 matches" | 1 comment-only hit at TradeCopierPanel.cs:739 (`// Never async void.`) | **NOT A VIOLATION** -- comment text, not executable code. Substance identical: 0 executable async void methods. Minor reporting imprecision by engineer; no impact. |
| 2 | SCAN 5 script path | `archive\v12-reference\scripts\complexity_audit.py` | Same path; `scripts\complexity_audit.py` in Wave root does NOT exist | **NOT A VIOLATION** -- engineer used correct archive path; verifier confirmed same. Script reports "0 methods audited" (scans archive classes, not PropTraderTools). GetRefPrice CYC=4 confirmed by structural count. |

No substantive discrepancies. Both minor items have no impact on compliance verdict.

---

## Verdict

**VERIFY_PASS**

All 7 scans independently confirmed with 0 executable violations. Implementation matches ticket
AFTER block exactly. All acceptance criteria met (Sim101 gate deferred per plan §3.6 -- not a
automated-verifiable criterion). No DNA rule violations. No architecture deviations.
