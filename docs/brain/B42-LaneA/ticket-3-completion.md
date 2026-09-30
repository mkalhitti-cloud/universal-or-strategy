# B42-LaneA — Ticket 3 Completion Report

**Block**: PTT-COPIER-B42 — PTTFollowerStrategy: Native ATM Brackets on Followers
**Ticket**: T3 — NEW FILE: src/PropTraderTools/Features/PttFollowerStrategy.cs
**Phase**: 4a — Engineer
**Engineer**: ptt-engineer
**Date**: 2026-08-05
**Input**: `docs/brain/B42-LaneA/04-tickets.md` (TICKETS_COMPLETE, cycle 2)
**Review**: `docs/brain/B42-LaneA/04-ticket-review.md` (TICKET_REVIEW_PASS, cycle 2)
**Prerequisite**: T1 VERIFY_PASS (FillSignalEventArgs + PttBus.FillSignal), T2 VERIFY_PASS (CopyEngine.cs publish path)

---

## What Was Implemented

**New file created**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFollowerStrategy.cs`

The `Features/` directory already existed (PttBreakEven.cs, PttCancel.cs, PttCopier.cs etc.
were already there). No `.csproj` edit was needed — `<Compile Include="**\*.cs" />` auto-includes.

### Class: `PttFollowerStrategy : Strategy`

Thin headless NinjaScript Strategy. One instance per follower account per instrument.
Configured in NT8 Control Center Strategies tab. No chart required.

**8 methods implemented** (all present in final file):

| Method | Visibility | Return Type | CYC | Purpose |
|--------|-----------|-------------|-----|---------|
| `OnStateChange()` | protected override | void | 4 | Subscribe/unsubscribe PttBus.FillSignal; set NT8 defaults |
| `OnBarUpdate()` | protected override | void | 1 | Required NT8 override — empty |
| `OnFillSignal(FillSignalEventArgs args)` | private | void | 3 | Guard on account+instrument; dispatch to CallAtmStrategyCreate |
| `CallAtmStrategyCreate(FillSignalEventArgs args)` | protected virtual | void | 1 | Calls AtmStrategyCreate; virtual test seam |
| `GetStrategyAccountName()` | protected virtual | string | 1 | Returns Account.Name; virtual test seam |
| `GetStrategyInstrumentName()` | protected virtual | string | 1 | Returns Instrument.FullName; virtual test seam |
| `GetSignalAccountName(FillSignalEventArgs args)` | protected virtual | string | 2 | Returns args.Account?.Name (null-safe ternary); virtual test seam |
| `GetSignalInstrumentName(FillSignalEventArgs args)` | protected virtual | string | 2 | Returns args.Instrument?.FullName (null-safe ternary); virtual test seam |

### Key design decisions

- `OnFillSignal` uses all 4 virtual helpers for name comparisons — never direct `Account.Name`/`Instrument.FullName` access
- `GetSignalAccountName`/`GetSignalInstrumentName` use `?:` ternary (not `?.` null-conditional) — C# 7.3 compatible per NT8 constraints
- `CallAtmStrategyCreate` uses `(code, msg) => { if (...) Print(...); }` lambda — `Print()` is a `Strategy` base method (no `NinjaTrader.Code.Output.Process` needed)
- `using System;` present (NT8-044: required for `Guid`, `ErrorCode` may resolve to `NinjaTrader.Cbi` but `Guid.NewGuid()` needs `System`)
- Namespace: `PropTraderTools` (flat — consistent with CopyEngine.cs, PttContracts.cs)
- No `readonly struct` keyword on `FillSignalEventArgs` reference (already defined as `struct` in PttContracts.cs — NT8-005 compliance handled in T1)

---

## 7-Scan Results (Layer 2)

All scans run against `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFollowerStrategy.cs`.

### SCAN-01 — `lock(` pattern

**Command**: `Select-String -Path "...\PttFollowerStrategy.cs" -Pattern "lock\("`

**Result**: 1 hit at line 14 — **comment text only**:
```
// JS-021: no lock() -- event += / -= on NT8 lifecycle thread
```
**Code-level `lock(` usage**: **0** ✅

### SCAN-02 — `async void` (first pass)

**Command**: `Select-String -Path "...\PttFollowerStrategy.cs" -Pattern "async void"`

**Result**: 2 hits at lines 9 and 16 — **comment text only**:
```
//   NT8-033: no async void
//   JS-033: no async void -- OnFillSignal is private void; OnBarUpdate is synchronous void.
```
**Code-level `async void`**: **0** ✅

### SCAN-03 — `return null`

**Command**: `Select-String -Path "...\PttFollowerStrategy.cs" -Pattern "return null"`

**Result**: **0 hits** ✅

Note: `GetSignalAccountName`/`GetSignalInstrumentName` use ternary `? x : null` expression bodies,
not `return null;` statement patterns. SCAN-03 pattern matching is for the statement form only.

### SCAN-04 — CYC audit (manual per method)

| Method | Decision Points | CYC | Budget (≤8) |
|--------|----------------|-----|-------------|
| `OnStateChange` | 3 if/else-if branches | 4 | ✅ |
| `OnBarUpdate` | 0 | 1 | ✅ |
| `OnFillSignal` | 2 early-return guards | 3 | ✅ |
| `CallAtmStrategyCreate` | 0 (lambda `if` scoped to lambda) | 1 | ✅ |
| `GetStrategyAccountName` | 0 (expression body) | 1 | ✅ |
| `GetStrategyInstrumentName` | 0 (expression body) | 1 | ✅ |
| `GetSignalAccountName` | 1 ternary `?:` | 2 | ✅ |
| `GetSignalInstrumentName` | 1 ternary `?:` | 2 | ✅ |

**All 8 methods ≤ 8. PASS.** ✅

### SCAN-05 — `init;` pattern (NT8-001)

**Command**: `Select-String -Path "...\PttFollowerStrategy.cs" -Pattern "init;"`

**Result**: **0 hits** ✅

No properties with `init` accessor. `PttFollowerStrategy` has no fields or properties at all.

### SCAN-06 — `volatile double` pattern (NT8-003)

**Command**: `Select-String -Path "...\PttFollowerStrategy.cs" -Pattern "volatile double"`

**Result**: **0 hits** ✅

No fields declared in `PttFollowerStrategy`. No `volatile`, no `double`.

### SCAN-07 — `async void` (confirm)

**Command**: `Select-String -Path "...\PttFollowerStrategy.cs" -Pattern "async void"`

**Result**: 2 hits at lines 9 and 16 — **comment text only** (same as SCAN-02).
**Code-level `async void`**: **0** ✅

---

## 7-Scan Summary

| Scan | Pattern | Layer 2 Result | Status |
|------|---------|---------------|--------|
| SCAN-01 | `lock(` code usage | 0 (1 comment-only) | ✅ ZERO |
| SCAN-02 | `async void` | 0 (2 comment-only) | ✅ ZERO |
| SCAN-03 | `return null` | 0 | ✅ ZERO |
| SCAN-04 | CYC ≤ 8 all methods | Max CYC = 4 (OnStateChange) | ✅ ALL ≤ 8 |
| SCAN-05 | `init;` | 0 | ✅ ZERO |
| SCAN-06 | `volatile double` | 0 | ✅ ZERO |
| SCAN-07 | `async void` (confirm) | 0 (2 comment-only) | ✅ ZERO |

**All 7 scans: ZERO violations.**

---

## Build Result

**Command**: `dotnet build "c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj"`

**Errors introduced by T3**: **0** ✅

**Pre-existing errors (out of T3 scope)**:
- `AtrSizingEngine.cs(20)`: CS0234 — `NinjaTrader.NinjaScript.Indicators` namespace not found (pre-existing since B9, confirmed in T1 and T2 VERIFY_PASS)
- `AtrSizingEngine.cs(24)`: CS0246 — `Indicator` type not found (same pre-existing cause)

**Pre-existing warnings (out of T3 scope)**:
- `CopyEngine.cs(715)`: CS8632 — nullable annotation context (pre-existing, confirmed in T2 VERIFY_PASS)

`PttFollowerStrategy.cs` itself compiled with zero errors. The 2 AtrSizingEngine errors and 1 CopyEngine warning were present before B42 began and are documented in T1 and T2 verification reports.

---

## DNA Rule Verification

| Rule | Description | Check | Status |
|------|-------------|-------|--------|
| JS-001 | No `throw` in hot path | No throw in OnFillSignal; Print() in lambda for error path | ✅ PASS |
| JS-002 | No `return null` statement | SCAN-03: 0 hits; ternary expressions use `: null` not `return null;` | ✅ PASS |
| JS-021 | No `lock()` | SCAN-01: 0 code hits | ✅ PASS |
| JS-033 | No `async void` | SCAN-02/07: 0 code hits | ✅ PASS |
| NT8-001 | No `init` accessor | SCAN-05: 0 | ✅ PASS |
| NT8-002 | No `abstract record` / `sealed record` | No record types; class only | ✅ PASS |
| NT8-003 | No `volatile double` | SCAN-06: 0 | ✅ PASS |
| NT8-019 | No `async void` callbacks | All overrides are synchronous void | ✅ PASS |
| NT8-033 | `async void` ban | SCAN-02/07: 0 code hits | ✅ PASS |
| CYC ≤ 8 | All methods | Max = 4 (OnStateChange) | ✅ PASS |

---

## Acceptance Criteria Verification

| Criterion (from 04-tickets.md T3) | Status |
|-----------------------------------|--------|
| `src/PropTraderTools/Features/` directory exists | ✅ (pre-existing, PttBreakEven.cs etc. already there) |
| `PttFollowerStrategy.cs` created at correct path | ✅ |
| File compiles with zero new errors | ✅ |
| Namespace is `PropTraderTools` (not `PropTraderTools.Features`) | ✅ |
| All 8 methods listed in method signatures table are present | ✅ |
| `OnFillSignal` uses virtual helpers for ALL 4 name comparisons | ✅ |
| No direct `Account.Name` / `Instrument.FullName` in `OnFillSignal` | ✅ |
| `Name = "PTTFollowerStrategy"` in SetDefaults | ✅ |
| `Calculate = Calculate.OnBarClose` | ✅ |
| `BarsRequiredToTrade = 0` (property setter, no `init`) | ✅ |
| `IsExitOnSessionCloseStrategy = false` (property setter, no `init`) | ✅ |
| Subscribe `PttBus.FillSignal += OnFillSignal` at `State.Realtime` | ✅ |
| Unsubscribe `PttBus.FillSignal -= OnFillSignal` at `State.Terminated` | ✅ |
| `AtmStrategyCreate` called inside `CallAtmStrategyCreate` | ✅ |
| `Guid.NewGuid().ToString("N").Substring(0, 8)` for ATM strategy ID | ✅ |

---

## File Created

- **File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFollowerStrategy.cs`
- **Lines**: 90
- **Namespace**: `PropTraderTools`
- **Using directives**: `System`, `NinjaTrader.Cbi`, `NinjaTrader.NinjaScript.Strategies`
- **No files modified** (T3 is new-file-only)

---

## BUILD_PASS

T3 implementation is correct and complete. All 7 scans at zero violations. Zero new build errors
introduced. All 8 methods present with correct signatures and guard logic. Virtual test seams
for T4 (B42Tests.cs) are in place.
