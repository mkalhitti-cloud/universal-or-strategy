# DW-B79-09 — Architecture Plan

**Pipeline ID**: DW-B79-09
**Title**: RemoveAll race guard — uniform application to CancelQxBrackets ×2 + CancelStaleBracketsLocal
**Priority**: P3 — cosmetic uniformity. No popup observed on QX/BE paths in production.
**Block**: DW-B79 (follow-on to DW-B79-04)
**Author**: ptt-architect (Phase 1)
**Date**: 2026-08-21
**Status**: PLAN_DRAFT — awaiting REVIEW_PASS gate (Ph2)

---

## 1. Context

DW-B79-04 fixed `CancelAllAccountOrders` (Flatten path) with two changes:
1. Removed `ChangeSubmitted` from `stateOk` — prevents async broker-rejection popup.
2. Added `RemoveAll(Filled || Cancelled)` race guard before `acc.Cancel()` — prevents
   cancelling an order that filled in the <1ms window between list construction and the
   cancel call.

Investigation during DW-B79-04 session confirmed:
- **Fix 1 (ChangeSubmitted)**: uniform — the other three cancel methods never had
  `ChangeSubmitted` in `stateOk`. No gap.
- **Fix 2 (RemoveAll race guard)**: NOT uniform — only `CancelAllAccountOrders` is guarded.
  Three methods remain unguarded.

This pipeline applies the race guard to the three unguarded methods.

---

## 2. Affected methods — exact source locations (HEAD 5925b618)

| Method | File | Line | Button path | Call site |
|--------|------|------|-------------|-----------|
| `CancelQxBrackets` (2-param) | `CopyEngine.cs` | 613 | QX button / QX-ALL | `PttQuickExit.cs:85`, `PttGlobalQuickExit.cs:152`, `TradeCopierPanel.cs:597` |
| `CancelQxBrackets` (3-param) | `CopyEngine.cs` | 677 | QX button (snapshot-gated path) | `PttQuickExit.cs:85` |
| `CancelStaleBracketsLocal` | `PttBreakEven.cs` | 171 | BE button / BE-ALL | `PttBreakEven.cs:108` |

**Not in scope** (already guarded):
- `CancelAllAccountOrders` (`CopyEngine.cs:713`) — guarded, commit 5925b618. No change.

---

## 3. Exact fix — one insertion per method

The insertion is identical in all three cases: one line inserted **immediately before**
the `acc.Cancel(...)` call in each method.

### 3a. `CancelQxBrackets` 2-param (`CopyEngine.cs:630`)

```csharp
// BEFORE (line 630):
try { acc.Cancel(stale.ToArray()); }
catch { }

// AFTER (DW-B79-09):
stale.RemoveAll(o => o.OrderState == OrderState.Filled
                  || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
try { acc.Cancel(stale.ToArray()); }
catch { }
```

### 3b. `CancelQxBrackets` 3-param (`CopyEngine.cs:702`)

```csharp
// BEFORE (line 701-703):
if (stale.Count == 0) return;                                                  // (7)
try { acc.Cancel(stale.ToArray()); }
catch { }

// AFTER (DW-B79-09): insert AFTER the stale.Count==0 guard, before acc.Cancel
if (stale.Count == 0) return;                                                  // (7)
stale.RemoveAll(o => o.OrderState == OrderState.Filled
                  || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
try { acc.Cancel(stale.ToArray()); }
catch { }
```

### 3c. `CancelStaleBracketsLocal` (`PttBreakEven.cs:193`)

```csharp
// BEFORE (lines 190-198):
if (stale.Count == 0) return;                                         // (3)
try
{
    acc.Cancel(stale.ToArray());
    NinjaTrader.Code.Output.Process(...);
}
catch { /* cancel on already-filled orders is non-fatal */ }

// AFTER (DW-B79-09): insert before acc.Cancel inside the try block
if (stale.Count == 0) return;                                         // (3)
try
{
    stale.RemoveAll(o => o.OrderState == OrderState.Filled
                      || o.OrderState == OrderState.Cancelled);   // DW-B79-09: race guard
    acc.Cancel(stale.ToArray());
    NinjaTrader.Code.Output.Process(...);
}
catch { /* cancel on already-filled orders is non-fatal */ }
```

---

## 4. CYC analysis

`RemoveAll(predicate)` is a single method call on `List<T>`. It is **not a branch** in the
calling method's control flow. The Roslyn/Lizard cyclomatic complexity counter does not
increment for a `RemoveAll` call.

| Method | CYC before | CYC after | Budget |
|--------|-----------|-----------|--------|
| `CancelQxBrackets` 2-param | 6 | 6 | ≤8 ✓ |
| `CancelQxBrackets` 3-param | 7 | 7 | ≤8 ✓ |
| `CancelStaleBracketsLocal` | 6 | 6 | ≤8 ✓ |

All three methods remain within the JS-080 CYC ≤ 8 budget. **No extraction required.**

---

## 5. Test plan — 3 new [Fact] methods

One structural contract test per method. Each test uses the existing `IL token scan`
pattern already established in `B79Tests.cs` for structural verification without NT8
runtime. Tests go in `CopyEngineTests.cs` alongside the existing DW-B79-04 tests.

| Test ID | Method under test | Contract verified |
|---------|-------------------|-------------------|
| `T_DW_B79_09_01` | `CancelQxBrackets` 2-param | IL body contains a `RemoveAll` call token before the `acc.Cancel` call token |
| `T_DW_B79_09_02` | `CancelQxBrackets` 3-param | IL body contains a `RemoveAll` call token before the `acc.Cancel` call token |
| `T_DW_B79_09_03` | `CancelStaleBracketsLocal` | IL body contains a `RemoveAll` call token |

**Alternative if IL scan is impractical for `CancelStaleBracketsLocal` (private static):**
Use reflection to verify the method body bytes contain the `RemoveAll` method token —
same `GetMethod(BindingFlags.NonPublic | BindingFlags.Static)` pattern already used for
private helpers in `PttBreakEvenB72Tests.cs`.

Test delta: **292 → 295** (+3 [Fact]).

---

## 6. Files written / modified

| File | Change type | Scope |
|------|-------------|-------|
| `src/PropTraderTools/CopyEngine.cs` | Edit — 2 insertions | Lines ~630 and ~702 |
| `src/PropTraderTools/Features/PttBreakEven.cs` | Edit — 1 insertion | Line ~193 |
| `src/PropTraderTools/CopyEngineTests.cs` | Edit — 3 new [Fact] methods | Appended to B79 test class |

No new files. No interface changes. No CopyRule fields. No method signature changes.

---

## 7. Jane Street compliance

| Rule | Check |
|------|-------|
| JS-021 (no lock) | No lock added. `RemoveAll` is called on a local `List<T>` — no shared state. ✓ |
| JS-001 (no throw) | `RemoveAll` does not throw for a valid predicate. The surrounding `try/catch` already handles any NT8 internal exceptions. ✓ |
| JS-080 (CYC ≤ 8) | All three methods stay at their current CYC (no new branches). ✓ |
| ASCII-only | The inserted line uses only ASCII characters. ✓ |

---

## 8. Risks and mitigations

| Risk | Probability | Mitigation |
|------|-------------|------------|
| An order fills between `RemoveAll` and `acc.Cancel` (second race window) | Extremely low — same microsecond window, identical to pre-B79-04 state | The `try/catch` already handles broker rejection. This is defence-in-depth, not a guarantee. |
| `RemoveAll` mutates the list while NT8 iterates it internally | N/A — `acc.Cancel` receives a new array (`ToArray()`) not the original list. List mutation before `ToArray()` is safe. | No mitigation required. |
| Test for private `CancelStaleBracketsLocal` fails reflection lookup | Low — existing `PttBreakEvenB72Tests.cs` already accesses this method via reflection | Use `BindingFlags.NonPublic | BindingFlags.Static` as in B72 tests. |

---

## 9. Execution order (single ticket)

This pipeline uses **1 ticket** (DW-B79-09-TICKET-1) covering all 3 insertions + 3 tests.
The insertions are independent (different files / different methods). No ordering
constraint between the three edits.

Recommended edit order within the ticket:
1. `CopyEngine.cs` — 2-param overload (L630)
2. `CopyEngine.cs` — 3-param overload (L702)
3. `PttBreakEven.cs` — `CancelStaleBracketsLocal` (L193)
4. `CopyEngineTests.cs` — 3 new [Fact] methods

---

## 10. Acceptance criteria

- [ ] `CancelQxBrackets` 2-param: `RemoveAll` line present immediately before `acc.Cancel`
- [ ] `CancelQxBrackets` 3-param: `RemoveAll` line present immediately before `acc.Cancel`
- [ ] `CancelStaleBracketsLocal`: `RemoveAll` line present immediately before `acc.Cancel`
- [ ] All three methods: CYC unchanged (6 / 7 / 6 respectively)
- [ ] `[Fact]` count: 295 (was 292, +3)
- [ ] `dotnet build` — 0 errors, 0 warnings on new lines
- [ ] `dotnet test` — all 295 [Fact] PASS
- [ ] 7-scan zero (ASCII, lock, async-void, return-null, new-array, CYC, JS P0)
- [ ] `deploy-sync.ps1` PASS — hard links re-synced
- [ ] F5 in NinjaTrader — GREEN (Director confirmation)
