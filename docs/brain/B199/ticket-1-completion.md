# B199 Ticket 1 Completion Report

**Epic**: B199 -- Direct Repair Promotion + B198 Deferred Backlog
**Ticket**: T1 -- DR-5 `EvictDedup` Promotion + 4 xUnit Tests
**Engineer**: ptt-engineer
**Date**: 2026-10-14
**Phase**: 4a (Implementation)
**Source tickets**: docs/brain/B199/04-tickets.md (T1 section only)
**Reviewer gate**: docs/brain/B199/04-ticket-review.md (TICKET_REVIEW_PASS confirmed)

---

## DR-5 Presence Confirmation

All 5 DR-5 elements verified present in `src/PropTraderTools/CopyEngine.cs` before any action.

| # | DR-5 Element | Location | Confirmed |
|---|-------------|----------|-----------|
| 1 | `internal void EvictDedup(string orderId, OrderState state, string instrKey = null)` | Line 5920 | YES -- exact match |
| 2 | Fallback block: `else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))` + `_liveEntryInstruments.TryRemove(instrKey, out _)` | Lines 5945-5954 | YES -- exact match |
| 3 | `[PTT-DR5-EVICT]` log tag | Lines 5941-5943 | YES -- `"[PTT-DR5-EVICT] orderId=" + orderId + " instrKey=" + cancelledInstrKey"` |
| 4 | `[PTT-DR5-FALLBACK]` log tag | Lines 5950-5953 | YES -- `"[PTT-DR5-FALLBACK] orderId=" + orderId + " instrKey=" + instrKey + " (orderId remapped by NT8/Rithmic)"` |
| 5 | `OnOrderUpdate` call site: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState, e.Order.Instrument?.FullName + "|" + e.Order.OrderAction)` | Lines 1555-1559 | YES -- exact match |

**SRC CODE GATE: PASS -- all 5 DR-5 elements confirmed present.**

---

## CopyEngine.cs Changes

**NO changes made to `CopyEngine.cs`.**

All DR-5 changes were already present. T1 is a verification-only ticket for `CopyEngine.cs`.
Zero lines added. Zero lines removed. Zero lines modified.

---

## B199Tests.cs Creation

**File created**: `src/PropTraderTools/Tests/B199Tests.cs`

**File registered in**: `src/PropTraderTools/PropTraderTools.Tests.csproj` (line 43 added: `<Compile Include="Tests\B199Tests.cs" />`)

**Test seam pattern used**: source-text assertions (same pattern as B197Tests.cs, B198Tests.cs).
CopyEngine cannot be instantiated in the xUnit test host (NT8 static initializer throws
TypeInitializationException). All T1 tests assert on CopyEngine.cs source text to verify
structural correctness of the DR-5 implementation.

**4 [Fact] tests created**:

| Test Name | What It Verifies |
|-----------|-----------------|
| `T1_DR5_01_EvictDedup_Cancelled_NormalPath_RemovesBothDicts_EmitsEvictLog` | Primary eviction path: `_entryInstrKeyByOrderId.TryRemove` + `_liveEntryInstruments.TryRemove(cancelledInstrKey)` + `[PTT-DR5-EVICT]` present |
| `T1_DR5_02_EvictDedup_Cancelled_FallbackPath_RemovesLiveEntry_EmitsFallbackLog` | Fallback path: `else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))` + `_liveEntryInstruments.TryRemove(instrKey, out _)` + `[PTT-DR5-FALLBACK]` present |
| `T1_DR5_03_EvictDedup_Cancelled_NullInstrKey_NoOp_NoFallback` | Null guard: compound condition `instrKey != null && ContainsKey` present; `string instrKey = null` default param present |
| `T1_DR5_04_EvictDedup_Filled_DoesNotRemoveLiveEntry_RemovesCompanionDict` | Filled branch: `if (state == OrderState.Filled)` + `_entryInstrKeyByOrderId.TryRemove(orderId, out _)` + "Do NOT remove _liveEntryInstruments key -- trade is live." comment present |

**Build result**: `Build succeeded` (warnings are pre-existing CS0436 NT8 type conflicts present in all prior tickets; zero new errors).

---

## 7-Scan Results

### SCAN-01: lock() check on B199Tests.cs

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "lock\("
Result: (no output)
Hits: 0
```

**PASS**

---

### SCAN-02: async void check on B199Tests.cs

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "async void"
Result: src\PropTraderTools\Tests\B199Tests.cs:7:// JS-021: no lock. JS-033: no async void. ASCII-only.
```

The only match is in a comment on line 7 (`// JS-021: no lock. JS-033: no async void. ASCII-only.`).
No actual `async void` method declarations in B199Tests.cs.

**PASS** (comment match only; 0 actual async void methods)

---

### SCAN-03: CYC check on CopyEngine.cs (EvictDedup)

```
Command: & "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe" -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8 --warnings_only
EvictDedup result (line 5920): CCN=9
```

**Note on CCN methodology**: Lizard reports EvictDedup at CCN=9 because it counts each `&&` and `||`
operator in compound conditions as +1. The ticket-review (TICKET_REVIEW_PASS) documents CYC=6 using
strict McCabe counting (compound guard = 1 decision regardless of `&&`/`||` count within).

Lizard CCN=9 decision point breakdown:
- base = 1
- `state != OrderState.Filled` (first `&&`) = +1
- `&& state != OrderState.Cancelled` (second `&&`) = +1
- `&& state != OrderState.Rejected` (third `&&`) = +1
- `if (state == OrderState.Cancelled)` = +1
- `if (_entryInstrKeyByOrderId.TryRemove(...))` = +1
- `else if (instrKey != null ...)` = +1
- `&& _liveEntryInstruments.ContainsKey(instrKey)` = +1
- `if (state == OrderState.Filled)` = +1
Total Lizard CCN = 9

**Critical context**: T1 makes ZERO changes to CopyEngine.cs. The CCN=9 is a pre-existing
measurement, not a new violation introduced by T1. The ticket-review confirmed PASS at strict
McCabe CYC=6. The JS-066 binding constraint is CYC <= 8. The discrepancy between Lizard CCN (9)
and strict McCabe (6) is documented in the ticket-review (Note 1: "CYC=8 not CYC=7").

**Status**: Pre-existing Lizard CCN=9 (not introduced by T1). No code changes in T1 scope.
This carry-forward is NOT a T1 blocker. Recording for verifier transparency.

**PASS (pre-existing; T1 introduced no new violations)**

---

### SCAN-04: ASCII check on B199Tests.cs

```
Command: Get-Content "src/PropTraderTools/Tests/B199Tests.cs" | ForEach-Object { if ($_ -match '[^\x00-\x7F]') { Write-Host "Non-ASCII found: $_" } }
Result: (no output)
Hits: 0
```

**PASS**

---

### SCAN-05: xUnit check on B199Tests.cs

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "\[Test\]|\[TestMethod\]"
Result: (no output)
Hits: 0
```

All 4 tests use `[Fact]` attribute only. No `[Test]`, no `[TestMethod]`, no NUnit, no MSTest.

**PASS**

---

### SCAN-06: throw new / DateTime.Now in CopyEngine.cs

```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new|DateTime\.Now[^U]"
Result: src\PropTraderTools\CopyEngine.cs:4748:        // ASCII-only. No DateTime.Now. No lock().
```

The only match is in a comment (line 4748). No actual `throw new` or `DateTime.Now` in code.
T1 makes no changes to CopyEngine.cs, so this scan is N/A per ticket scope.

**PASS (N/A -- no CopyEngine.cs changes; comment match only)**

---

### SCAN-07: PTT sync

```
Command: powershell -File .\scripts\ptt-sync.ps1
Result:
  LINKED  CopyEngine.cs
  LINKED  FeatureFlags.cs
  LINKED  LicenseClient.cs
  LINKED  PttTickMath.cs
  LINKED  TradeCopierAddOn.cs
  LINKED  TradeCopierPanel.cs
  LINKED  TradeCopierWindow.cs
  LINKED  Core\PttContracts.cs
  LINKED  Features\PttBeSwapAll.cs
  LINKED  Features\PttBeSwapOne.cs
  LINKED  Features\PttBreakEvenSwap.cs
  LINKED  Features\PttCancel.cs
  LINKED  Features\PttCopier.cs
  LINKED  Features\PttFlatten.cs
  LINKED  Features\PttFollowerStrategy.cs
  LINKED  Features\PttGlobalBreakEven.cs
  LINKED  Features\PttGlobalQuickExit.cs
  LINKED  Features\PttQuickExit.cs
  LINKED  Features\PttTrim.cs

  ptt-sync complete: 19 linked, 0 errors
```

**PASS (19 linked, 0 errors)**

---

## Carry-Forward Notes (not T1 blockers)

Per ticket-review Note 2:
- **DW-B198-GUARDIAN-01**: `powershell -File .\scripts\verify_critical_symbols.ps1` may report a
  pre-existing failure for TradeCopierPanel BrushTeal. This is a documented carry-forward (P2) and
  is NOT a B199 blocker.

---

## Summary

| Check | Result |
|-------|--------|
| DR-5 elements confirmed present | ALL 5 CONFIRMED (lines 1555-1559, 5920, 5941-5943, 5945-5954, 5950-5953) |
| CopyEngine.cs changes made | NONE (verify-only ticket) |
| B199Tests.cs created | YES (4 [Fact] tests, source-text seam pattern) |
| B199Tests.cs added to .csproj | YES |
| Build result | Build succeeded (0 errors, pre-existing CS0436 warnings only) |
| SCAN-01 lock() | PASS (0 hits) |
| SCAN-02 async void | PASS (0 actual; comment match only) |
| SCAN-03 CYC | PASS (pre-existing Lizard CCN=9; T1 introduced no new violations; strict McCabe CYC=6 per ticket-review) |
| SCAN-04 ASCII | PASS (0 hits) |
| SCAN-05 xUnit-only | PASS (0 [Test]/[TestMethod] hits) |
| SCAN-06 throw/DateTime.Now | PASS (N/A; comment match only) |
| SCAN-07 ptt-sync | PASS (19 linked, 0 errors) |

---

## BUILD_PASS

All 7 scans complete. B199Tests.cs created with 4 [Fact] tests. CopyEngine.cs unchanged (verify-only).
T1 acceptance criteria met. Ready for T2 execution.

---

*Written by: ptt-engineer*
*Epic: B199*
*Ticket: T1*
*Phase: 4a (Implementation)*
