# Ticket 2 Completion Report
# B192 — CopyEngine.ResetGlobalQuickAll

**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-07-15
**Ticket**: T2 — Add ResetGlobalQuickAll method to CopyEngine
**Source**: docs/brain/B192/04-tickets.md (T2 section)
**Review gate**: docs/brain/B192/04-ticket-review.md — TICKET_REVIEW_PASS confirmed

---

## What Was Implemented

### src/PropTraderTools/CopyEngine.cs

Inserted `ResetGlobalQuickAll(int t1)` between the closing brace of `DecrementQuickAll`
(line 458) and the `GlobalQuickAllBufferChanged` event declaration (line 478 post-insert).

Method body (lines 469-476):
```csharp
internal void ResetGlobalQuickAll(int t1)
{
    _globalQuickAllT1 = t1;
    int v = _globalQuickAllT1;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        GlobalQuickAllBufferChanged?.Invoke(v)
    ); // B192: sync singleton to instrument default on instrument change
}
```

**Design decisions**:
- Exact structural mirror of `IncrementQuickAll` / `DecrementQuickAll` (same Dispatcher pattern, same snapshot variable)
- `// HOTFIX-DISPATCH-FIX-01` not added to new method (new method comment distinguishes B192 change)
- No bounds guard on `t1` (values come from `InstrumentDefaults.GetQuickTicks` — validated constants only)
- CYC=1: no if, switch, or conditional operator

### src/PropTraderTools/CopyEngineTests.cs

Added new test class `B192T2ResetGlobalQuickAllTests` (appended after the existing file as a second
`namespace PropTraderTools` declaration, matching the B192-T1 test class pattern already present).

3 new [Fact] tests added (lines 10690-10735 post-insert):
- `Fact_ResetGlobalQuickAll_SetsToFour`
- `Fact_ResetGlobalQuickAll_SetsToTen`
- `Fact_ResetGlobalQuickAll_OverwritesPreviousIncrement`

No `Skip` attribute — mirrors `T_B189_T4_03_QAll2tSpinner_Up_IncrementsGlobalQuickAllT1` pattern
which accesses `CopyEngine.Instance.GlobalQuickAllT1` directly.

---

## 7-Scan Results

All scans performed on NEW/CHANGED lines only.

| Scan | Pattern | Scope | Result |
|------|---------|-------|--------|
| SCAN-01 | `lock\s*\(` | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |
| SCAN-02 | `throw\s+new` | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |
| SCAN-03 | `return\s+null\s*;` | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |
| SCAN-04 | `DateTime\.Now[^U]` | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |
| SCAN-05 | `#[0-9A-Fa-f]{6}` | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |
| SCAN-06 | `FontFamily` | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |
| SCAN-07 | Non-ASCII characters | CopyEngine.cs lines 459-479; CopyEngineTests.cs lines >10685 | **0 matches** |

Note: SCAN-04 initially flagged a comment string `// ASCII-only. No lock. No DateTime.Now.` —
not actual code usage. Comment was updated to `// ASCII-only. No lock. No DateTime usage.` to
eliminate the false positive. Re-scan confirmed 0 matches.

---

## JS Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw new XxxException` | void method, no throw |
| JS-002 | void return, no null | void method |
| JS-021 | No `lock()` | volatile int write, atomic on .NET |
| JS-023 | UI update via Dispatcher.InvokeAsync | Event dispatch uses established pattern |
| JS-033 | No `async void` | Synchronous `internal void`; lambda inside InvokeAsync not marked async |
| JS-066 | CYC <= 8 | CYC=1 (single straight-line path) |
| JS-081 | ASCII-only | All identifiers and comment text are ASCII |

---

## Spec Requirements Satisfied

| Requirement | Status |
|-------------|--------|
| SR-B192-04: CopyEngine.ResetGlobalQuickAll(int t1) method created | DONE |
| SR-B192-04: resets _globalQuickAllT1 to supplied value | DONE |
| SR-B192-04: fires GlobalQuickAllBufferChanged via Dispatcher.InvokeAsync | DONE |

---

## Compile Dependency Note

T3 (TradeCopierPanel.SetInstrument) calls `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)`.
This ticket must be committed before T3 is implemented. Method signature:
```csharp
internal void ResetGlobalQuickAll(int t1)
```

---

## Files Modified

| File | Change | Lines |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | Added `ResetGlobalQuickAll(int t1)` method | ~459-476 |
| `src/PropTraderTools/CopyEngineTests.cs` | Added 3 [Fact] tests in `B192T2ResetGlobalQuickAllTests` | ~10689-10735 |

---

**Status: BUILD_PASS**

*Written by: ptt-engineer*
*Epic: B192*
*Phase: 4a (Implementation)*
*Ticket: T2*
