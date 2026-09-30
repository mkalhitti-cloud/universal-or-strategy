# B28-LaneA Architecture Plan

**Block**: B28-LaneA
**Defect**: DW-B28-01 (P0 CRITICAL) — BE stop price never changes on live account
**Date**: 2026-07-16
**Status**: REVIEW_PASS

---

## 1. Problem Statement

### DW-B28-01 — BE Button Fires But Stop Price Never Changes

During the B27 live test, the Break-Even (BE) button fires correctly:
- The button transitions amber → blue
- `PendingBeFired` event fires

However, the stop price on all four bracket stop orders **never changes**. Zero
"Change submitted" events appear in the NinjaTrader order grid log.

### Root-Cause Hypothesis

`acc.Change()` at [`CopyEngine.cs`](../../../src/PropTraderTools/CopyEngine.cs:1200) is
throwing an exception inside the `try/catch` block at lines 1196–1203. The catch block
writes to `StatusUpdate`, but the Director was not watching the status bar during the B27
live test, so the exception message was silently swallowed.

### Why Diagnostic Hardening Is the Correct Approach

Before changing any BE logic (which would be premature), the team needs **definitive
evidence** of whether `acc.Change()` is reached and whether it throws. A single
`StatusUpdate` line inserted immediately before `acc.Change()` creates the
"we reached Change()" vs "we got past Change()" distinction needed on the next live test.

This is a **zero-behaviour-change** diagnostic insertion. No NT8 API calls are modified,
no branching logic is altered, no order flow is affected.

---

## 2. Architecture Decision — Diagnostic Hardening

**Decision (Director-approved, LOCKED):** Insert exactly **1 `StatusUpdate` line**
immediately before `acc.Change()` inside the existing `try` block in
[`MoveStopToBreakEven`](../../../src/PropTraderTools/CopyEngine.cs:1188).

**Rationale:**
- The defect cannot be debugged further without knowing whether `acc.Change()` is
  reached or whether it throws silently.
- Inserting a pre-Change status message costs zero tokens of behaviour change.
- Zero new overloads. Zero test changes. Zero CYC impact.
- The next live test will show one of three outcomes:
  1. "BE attempting acc.Change" appears AND "BE moved to" appears → Change() succeeded;
     look elsewhere for why stop price is not updating.
  2. "BE attempting acc.Change" appears AND "PTT-BE error:" appears → Change() threw;
     the exception message reveals the NT8 API error.
  3. "BE attempting acc.Change" does NOT appear → execution never reached this line;
     defect is upstream in the gate chain.

---

## 3. Exact Code Change

### Location

File: `src/PropTraderTools/CopyEngine.cs`
Method: `MoveStopToBreakEven`
Lines: 1196–1202 (the `try` block body)

### Before

```csharp
    order.StopPrice = newStop;
    acc.Change(new Order[] { order });
    StatusUpdate?.Invoke(acc.Name + ": BE moved to " + newStop);
```

### After

```csharp
    order.StopPrice = newStop;
    StatusUpdate?.Invoke(acc.Name + ": BE attempting acc.Change -> " + newStop);  // DW-B28-01 diagnostic
    acc.Change(new Order[] { order });
    StatusUpdate?.Invoke(acc.Name + ": BE moved to " + newStop);
```

**Change summary:** 1 line inserted, 0 lines deleted.

The new line is:
```
StatusUpdate?.Invoke(acc.Name + ": BE attempting acc.Change -> " + newStop);  // DW-B28-01 diagnostic
```

---

## 4. Files Affected

| File | Change | Lines Modified |
|------|--------|---------------|
| `src/PropTraderTools/CopyEngine.cs` | 1 `StatusUpdate` line inserted inside existing `try` block | +1 |
| `src/PropTraderTools/TradeCopierPanel.cs` | **No change** | — |
| `src/PropTraderTools/CopyEngineTests.cs` | **No change** | — |

**Total source delta:** 1 line added, 0 lines deleted.

---

## 5. Scan Checklist

All four scans MUST be run by the engineer (ptt-engineer) after applying the change and
before commit. Expected results are binding.

```
SCAN-01: grep -n "lock(" CopyEngine.cs
         Expected: 0 results

SCAN-02: grep -n "async void " CopyEngine.cs
         Expected: 0 results

SCAN-03: Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object
         Expected: Count = 135

SCAN-04: grep -n "BE attempting acc.Change" CopyEngine.cs
         Expected: exactly 1 result
```

Any scan returning an unexpected result is a **HARD STOP**. Do not commit until all
four scans pass.

---

## 6. \[Fact\] Count

| Metric | Value |
|--------|-------|
| Baseline \[Fact\] count | 135 |
| Target \[Fact\] count | 135 |
| Lane A new tests added | 0 |

Lane A is a diagnostic-only change. No new test cases are required because:
- The change adds no branching logic (CYC is unchanged).
- The inserted line is a `StatusUpdate?.Invoke(...)` — a null-conditional fire-and-forget.
- Existing tests cover the `MoveStopToBreakEven` happy path and all error paths.

---

## 7. JS / NT8 Rule Constraints

| Rule | Constraint | Compliance |
|------|-----------|------------|
| JS-021 | No `lock()` in `CopyEngine.cs` | PASS — no lock() added; SCAN-01 confirms 0 results |
| JS-033 | No `async void` in `CopyEngine.cs` | PASS — no async void added; SCAN-02 confirms 0 results |
| CYC <= 8 | `MoveStopToBreakEven` cyclomatic complexity unchanged | PASS — 1 `StatusUpdate` line added inside existing `try`, no new branches |
| ASCII-only | New StatusUpdate message is ASCII-only | PASS — `"BE attempting acc.Change -> "` contains no Unicode characters |
| NT8-007 | `CreateOrder` arg 12 pattern | N/A — no `CreateOrder` call in this change |
| DateTime.UtcNow | No `DateTime.Now` usage | N/A — no DateTime usage in this change |

---

## 8. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `StatusUpdate` delegate is null | Extremely low | Null-conditional operator `?.` used — safe no-op if null |
| Message string allocation on hot path | Negligible | BE is a user-triggered once-per-trade action, not a hot path |
| CYC regression | Zero | No branching added; single straight-line statement |
| Behaviour change | Zero | Statement fires and returns; does not affect `acc.Change()` call or its outcome |
| NT8 compiler rejection | Zero | `StatusUpdate?.Invoke(...)` is idiomatic C# 6+, fully supported in NT8/.NET 4.8 |
| Test regression | Zero | No test files modified; 135 \[Fact\] baseline unchanged |

**Overall risk: MINIMAL.** This is a one-line diagnostic insertion with no observable
effect on trading behaviour.

---

## 9. Status

```
REVIEW_PASS
```

This plan is Director-approved and locked. No review cycle is required before ticket
generation (04-tickets.md). The ptt-engineer may proceed directly to implementation.
