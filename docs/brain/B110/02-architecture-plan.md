# B110 Architecture Plan
# DW-B110: Remove CancelQxBracketsForFollowers from Leader Path

**Status**: REVIEW_PENDING
**Epic**: B110
**Phase**: 1 (Architecture)
**Author**: ptt-architect
**Date**: 2026-08-26

---

## 1. Problem Statement

**DW-B110**: `CancelQxBracketsForFollowers` in the leader execution path causes a race condition
with the per-follower `_qxCancelInProgress` guard (DW-B105 / "guard 3b").

### Collision Chain

1. QX-ALL fires. `PttGlobalQuickExit.ExecuteOne` is called for each account.
2. For the **leader** account (`skipIfFollower=true`), `PttQuickExit.Execute` reaches L106-107:
   ```
   if (skipIfFollower)
       CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
   ```
3. `CancelQxBracketsForFollowers` iterates all follower accounts and calls
   `CancelQxBrackets(acc, instr)` — cancelling their BE bracket orders.
4. The cancellation produces `OnOrderUpdate(Cancelled)` events on each follower account.
5. `TryReplacePttBeBrackets` receives the event. It checks `_qxCancelInProgress[acc.Name]`.
6. **Race**: the `_qxCancelInProgress` flag was set by `PttGlobalQuickExit.ExecuteOne` **only for
   the follower account's own ExecuteOne call** (DW-B79-03 block, L145-162). At the moment the
   leader fires step 3 above, the follower's `ExecuteOne` has not yet run — so the flag is absent.
7. Guard 3b evaluates to **FALSE** → `TryReplacePttBeBrackets` fires a BE-RETRY simultaneously
   with the pending QX order submission, causing Combo C defect (BE-ALL → QX-ALL).

---

## 2. Root Cause Confirmation

### Exact Lines to Delete

[`src/PropTraderTools/Features/PttQuickExit.cs`](src/PropTraderTools/Features/PttQuickExit.cs:100)
lines **L100–L107** (inclusive):

```csharp
            // B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders.
            // B78 DW-B78-02: ONLY from the leader execution path (skipIfFollower=true).
            // When skipIfFollower=false (follower account), CancelQxBracketsForFollowers would
            // silently erase every previous follower's just-submitted PTT-QX orders, because
            // each follower's Execute call runs on the same synchronous dispatch loop and the
            // sibling PTT-QX orders are in Submitted/Initialized state -- IsQxCancelCandidate matches them.
            if (skipIfFollower)
                CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

That is 8 lines (L100 comment through L107 call-site).

### Why `CancelQxBracketsForFollowers` Is Redundant

**DW-B79-03** in [`src/PropTraderTools/Features/PttGlobalQuickExit.cs`](src/PropTraderTools/Features/PttGlobalQuickExit.cs:144)
already handles per-follower cancel with the `_qxCancelInProgress` guard:

```csharp
if (!skipIfFollower) // (1) — follower account path only
{
    CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
    try
    {
        CopyEngine.Instance?.CancelQxBrackets(acc, instr);  // cancels follower brackets
    }
    finally
    {
        CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
    }
}
var executor = new PttQuickExit();
executor.Execute(acc, instr, t1Ticks, targets, skipIfFollower, leaderStop, leaderTargetCount);
```

- `CancelQxBrackets(acc, instr)` at L157 cancels follower brackets **before** `PttQuickExit.Execute`
  runs for that account.
- The `_qxCancelInProgress` guard is set for the correct account **before** the cancel fires,
  so `TryReplacePttBeBrackets` correctly skips the BE-RETRY.
- The leader's `CancelQxBracketsForFollowers` call at PttQuickExit.cs:107 performs the same cancel
  **without the guard**, on a racing timeline — it is strictly redundant and harmful.

### Production Call Sites Confirmed

From the `Select-String` scan, `CancelQxBracketsForFollowers` appears in production code at:

| File | Line | Role |
|------|------|------|
| `CopyEngine.cs:929` | Definition | Internal method — keep as-is |
| `PttQuickExit.cs:107` | **Call site to delete** | The redundant leader-path call |

All other occurrences are in test files (`B68Tests.cs`, `CopyEngineTests.cs`) and comments —
**no other production call site exists**.

---

## 3. Chosen Fix (Option 3 — Director Approved)

### Action

**Delete** lines L100–L107 from [`PttQuickExit.Execute`](src/PropTraderTools/Features/PttQuickExit.cs:100).
This removes the 8-line comment block and the `if (skipIfFollower)` call.

### What Does NOT Change

| Item | Decision | Rationale |
|------|----------|-----------|
| `skipIfFollower` parameter | **Keep** | Still needed for follower-account guard at L70-77 |
| `CancelQxBracketsForFollowers` method in `CopyEngine.cs` | **Keep** | Tested by `B68Tests.cs`; potential future use |
| `PttGlobalQuickExit.cs` DW-B79-03 block | **Keep** | Correct path for per-follower cancel |
| All test files | **Unchanged** | B68Tests, B78Tests, B79Tests remain as-is |

### Docstring Update

The `Execute` method docstring at L28-29 must be updated:

**Before**:
```
/// CYC=8: null/flat guard(1) + follower guard(2) + cancelFollowers guard(3) + snapshotStop guard(4)
///        + isLong(5) + for-loop(6) + stop-submit null check(7) + target-submit null check(8).
```

**After**:
```
/// CYC=7: null/flat guard(1) + follower guard(2) + snapshotStop guard(3)
///        + isLong(4) + for-loop(5) + stop-submit null check(6) + target-submit null check(7).
```

Also remove the B78 DW-B78-02 sentence from L35-36:
```
/// B78 DW-B78-02: CancelQxBracketsForFollowers guarded by skipIfFollower -- prevents sibling
///   follower QX orders from being cancelled by subsequent follower Execute calls.
```

---

## 4. CYC Impact Analysis

### Before Fix: CYC=8

Branch enumeration (current docstring):
1. `null/flat guard` — `if (pos == null || pos.Quantity == 0)` at L60
2. `follower guard` — `if (skipIfFollower && IsFollowerAccount(...))` at L70
3. **`cancelFollowers guard`** — `if (skipIfFollower)` at L106 ← **deleted**
4. `snapshotStop guard` — conditional inside `ResolveStop` (counted per docstring as branch 4)
5. `isLong` — direction branch in submit loop
6. `for-loop` — `foreach` over targets
7. `stop-submit null check` — stop order null guard
8. `target-submit null check` — target order null guard

### After Fix: CYC=7

Branch enumeration (new docstring, renumbered):
1. `null/flat guard` — `if (pos == null || pos.Quantity == 0)` at L60
2. `follower guard` — `if (skipIfFollower && IsFollowerAccount(...))` at L70
3. `snapshotStop guard` — conditional inside `ResolveStop`
4. `isLong` — direction branch in submit loop
5. `for-loop` — `foreach` over targets
6. `stop-submit null check` — stop order null guard
7. `target-submit null check` — target order null guard

CYC drops from 8 → 7. Remains within JS-080 threshold of ≤8.

---

## 5. Files In Scope

### MODIFY

#### `src/PropTraderTools/Features/PttQuickExit.cs`

**Change 1 — Delete L100–L107** (comment block + `if (skipIfFollower)` call):
```
// B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders.
// B78 DW-B78-02: ONLY from the leader execution path (skipIfFollower=true).
// When skipIfFollower=false (follower account), CancelQxBracketsForFollowers would
// silently erase every previous follower's just-submitted PTT-QX orders, because
// each follower's Execute call runs on the same synchronous dispatch loop and the
// sibling PTT-QX orders are in Submitted/Initialized state -- IsQxCancelCandidate matches them.
if (skipIfFollower)
    CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

**Change 2 — Update Execute docstring** (L28-29 CYC line, and L35-36 B78 sentence):
- L28: `CYC=8:` → `CYC=7:`
- L29: remove `cancelFollowers guard(3) +`, renumber guards 4→3, 5→4, 6→5, 7→6, 8→7
- L35-36: delete the two-line `B78 DW-B78-02: CancelQxBracketsForFollowers ...` sentence

### ADD NEW TEST FILE

#### `src/PropTraderTools/Tests/B110Tests.cs`

Two `[Fact]` tests using IL token scan (same pattern as `B68Tests.cs`):

**T_B110_01** — `PttQuickExit_Execute_does_not_call_CancelQxBracketsForFollowers`
- Arrange: resolve `PttQuickExit.Execute` via `typeof(PttQuickExit).GetMethod("Execute", ...)`
- Arrange: resolve `CopyEngine.CancelQxBracketsForFollowers` via `typeof(CopyEngine).GetMethod("CancelQxBracketsForFollowers", BindingFlags.Instance | BindingFlags.NonPublic)`
- Act: read `Execute` method body IL bytes; scan for `CancelQxBracketsForFollowers` token
- Assert: `Assert.False(tokenFound, "PttQuickExit.Execute must NOT call CancelQxBracketsForFollowers -- DW-B110 fix")`

**T_B110_02** — `PttQuickExit_Execute_CYC_is_7`
- Arrange: resolve `PttQuickExit.Execute` method body
- Act: count branch instructions in IL (brtrue/brfalse/bgt/blt/beq/bne/ble/bge variants + br.s, brfalse.s, brtrue.s) via `MethodBody.GetILAsByteArray()`
- Assert: `Assert.Equal(6, branchCount)` (CYC = branch_count + 1 = 7; branchCount = 6)

### NO CHANGES

| File | Reason |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | `CancelQxBracketsForFollowers` definition stays |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | DW-B79-03 block is the correct path |
| `src/PropTraderTools/Tests/B68Tests.cs` | Existing tests remain valid |
| `src/PropTraderTools/Tests/B78Tests.cs` | Unaffected |
| `src/PropTraderTools/Tests/B79Tests.cs` | Unaffected |

---

## 6. Combo Regression Map

| Combo | Description | Expected After Fix | Mechanism |
|-------|-------------|-------------------|-----------|
| **C** | BE-ALL then QX-ALL | **PASS** (target fix) | Leader no longer cancels follower brackets without guard; DW-B79-03 handles per-follower cancel with `_qxCancelInProgress` flag set; `TryReplacePttBeBrackets` correctly sees guard=TRUE and skips BE-RETRY |
| **D** | QX-ALL then BE-ALL | **PASS** (non-regression) | DW-B79-03 path unaffected; follower cancel always happens via the guarded `CancelQxBrackets(acc, instr)` call before `PttQuickExit.Execute` |
| **E** | QX-ALL direct (no BE brackets) | **PASS** (non-regression) | No BE brackets present; `CancelQxBracketsForFollowers` removal has no effect on follower QX submission loop |
| **F** | QX-ALL then BE-ALL while in green | **PASS** (non-regression) | B108 green-position path unaffected; no interaction with leader's old `CancelQxBracketsForFollowers` call |

---

## 7. Verify Criteria (10 checks — all PASS required)

| # | Check | Command | Pass Condition |
|---|-------|---------|----------------|
| T1 | Build | `dotnet build src/` | Zero errors, zero warnings |
| T2 | Tests | `dotnet test` | All existing tests green + `T_B110_01` + `T_B110_02` green |
| T3 | Lock scan | `Get-ChildItem src/ -Recurse -Filter *.cs \| Select-String "lock("` | Zero results in modified files |
| T4 | CYC | `python scripts/complexity_audit.py` | `PttQuickExit.Execute` score = 7 |
| T5 | ASCII | PowerShell byte scan on modified region | Zero non-ASCII bytes in modified region of `PttQuickExit.cs` |
| T6 | Combo C guard | `T_B110_01` asserts `CancelQxBracketsForFollowers` token absent from `Execute` IL | PASS |
| T7 | Combo D/E/F non-regression | `T_B68_03` still passes (DispatchCopy does not call `CancelQxBracketsForFollowers`) | PASS |
| T8 | DW-B79-03 intact | `Select-String "CancelQxBrackets(acc, instr)" PttGlobalQuickExit.cs` | Present at L157 |
| T9 | Sync | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH lines |
| T10 | Sync confirmation | Agent writes result to `ticket-1-verification.md` | PASS logged |

---

## 8. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|-----------|
| Functional regression — follower brackets not cancelled before QX | **Low** | DW-B79-03 in `PttGlobalQuickExit.ExecuteOne` performs the same cancel with the `_qxCancelInProgress` guard active; coverage verified by `T_B110_01` |
| `CancelQxBracketsForFollowers` method becomes dead code | **None** | Method kept in `CopyEngine.cs`; tested by `B68Tests.cs` T_B68_01/04/05; declared `internal` for potential future use |
| Docstring drift (CYC claim mismatch) | **Low** | `T_B110_02` IL branch count asserts CYC=7 at test time; docstring update is part of the ticket contract |
| Off-by-one in branch renumbering | **None** | Branch list is enumerated explicitly in Section 4 above; engineer follows verbatim |

---

## 9. JS-Rules Compliance

| Rule | Status | Notes |
|------|--------|-------|
| **JS-021** — No `lock()` | PASS | Change deletes code; no new lock introduced |
| **JS-001** — No `throw` in hot path | PASS | No exception-throwing code added |
| **JS-002** — No `return null` | PASS | No new return paths |
| **JS-033** — No `async void` | PASS | Method remains synchronous `void` |
| **JS-066** — Diff < 10k chars | PASS | Deletion of ~8 lines + docstring update; estimated diff ~600 chars |
| **JS-080** — CYC ≤ 8 | PASS | CYC decreases from 8 → 7; improves compliance margin |

---

## Component Summary

| Component | Change Type | File |
|-----------|------------|------|
| `PttQuickExit.Execute` | Delete L100-L107 + update docstring | `Features/PttQuickExit.cs` |
| `B110Tests.cs` | New file — T_B110_01 + T_B110_02 | `Tests/B110Tests.cs` |
| `CancelQxBracketsForFollowers` method | No change (kept) | `CopyEngine.cs:929` |
| `PttGlobalQuickExit.ExecuteOne` | No change (correct path) | `Features/PttGlobalQuickExit.cs` |

---

*Plan generated by ptt-architect. Awaiting ptt-plan-reviewer REVIEW_PASS before Phase 3 (tickets).*
