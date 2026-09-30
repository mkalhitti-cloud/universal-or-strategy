# B108 Tickets
**Status**: TICKETS_COMPLETE
**Source plan**: docs/brain/B108/02-architecture-plan.md (REVIEW_PASS)
**Defect closed**: DW-B107
**Author**: ptt-architect
**Date**: 2026-08-11

---

## Ticket B108-T1 — SnapshotBeTargets Extraction + Cap-at-3

**Ticket ID**: B108-T1
**Status**: TICKETS_COMPLETE
**Source plan**: docs/brain/B108/02-architecture-plan.md (REVIEW_PASS)
**Defect closed**: DW-B107

---

## 1. Spec Requirements Closed

**B108-T1 closes DW-B107**: stale `PTT-BE-Target-*` residues inflate BE target snapshot in
`MoveStopToBreakEven` Step A.

`MoveStopToBreakEven` built its target list (Step A, L3373-3422) with a single flat-collect
loop that accumulated every qualifying `Limit` order into one list with no native-vs-PTT
discrimination and no count cap. When a prior-session `PTT-BE-Target-4` order was still
`Working` in `acc.Orders`, all four entries entered `targets`, causing `PttBreakEvenSwap.Execute`
to submit 4 OCO pairs on a 3-target ATM — one more than the ATM expects. This ticket
applies the same two-pass native-first pattern that DW-B106 applied to the QX path.

| Change | Closes | Spec Requirement |
|--------|--------|-----------------|
| CHANGE A | DW-B107 | Extract `SnapshotBeTargets` private method with two-pass native-first collect and 7-state `stateOk` |
| CHANGE B | DW-B107 | Replace Step A flat-collect loop with `SnapshotBeTargets` call; reduce `MoveStopToBreakEven` CYC from 8 to 7 |
| CHANGE C | DW-B107 | Hard cap `targets.Count` at 3 via `while + RemoveAt` before `PttBreakEvenSwap.Execute` |

---

## 2. Files In Scope

**EXACTLY ONE FILE**: `src/PropTraderTools/CopyEngine.cs`

| Change | Description | Location |
|--------|-------------|----------|
| CHANGE A | New private `SnapshotBeTargets` method | Inserted immediately before `MoveStopToBreakEven` (~L3335) |
| CHANGE B | Replace Step A loop with `SnapshotBeTargets` call; update CYC annotation | L3271-3272 (annotation) + L3373-3422 (loop) |
| CHANGE C | Insert `while` cap before `PttBreakEvenSwap.Execute` | After CHANGE B call site, before `PttBreakEvenSwap.Execute` |

**Explicitly Out of Scope — DO NOT TOUCH**:

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | Fixed in B107 (DW-B106). DO NOT TOUCH. |
| `src/PropTraderTools/Features/PttQuickExit.cs` | Fixed in B107 (DW-B106). DO NOT TOUCH. |
| `src/PropTraderTools/Features/PttBreakEvenSwap.cs` | Cap applied upstream (before `Execute`). DO NOT TOUCH. |

---

## 3. Method Signatures Touched

| Method | File | Change Type |
|--------|------|-------------|
| `SnapshotBeTargets(Account acc, Instrument instrument)` | `CopyEngine.cs` | New private method, inserted immediately before `MoveStopToBreakEven` (~L3335) |
| `MoveStopToBreakEven` | `CopyEngine.cs` | Step A loop (L3373-3422) replaced; CYC annotation (L3271-3272) updated |

**Return type of `SnapshotBeTargets`**: `List<(double Price, int Qty, OrderAction Action)>`
(3-tuple with `OrderAction` — required by `PttBreakEvenSwap.Execute`. Differs from the
2-tuple `(double Price, int Qty)` used by the QX path `SnapshotTargetOrders`.)

---

## 4. Precise Code Changes

### CHANGE A — New private method `SnapshotBeTargets`

**Insertion point**: Immediately before `MoveStopToBreakEven` (~L3335) in `CopyEngine.cs`.

Insert the following COMPLETE verbatim method:

```csharp
        // CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5)
        //        + if(isNative)(6) + else if(isPtt)(7). JS-002: returns List, never null.
        // JS-021: no lock. JS-001: no throw. ASCII-only.
        // DW-B107: two-pass native-first collect for MoveStopToBreakEven Step A.
        // stateOk is wider than SnapshotTargetOrders (7 states vs 2) per DW-B79-01 + REPAIR-09 DW-B79-05.
        private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(
            Account acc, Instrument instrument)
        {
            var nativeTargets = new List<(double Price, int Qty, OrderAction Action)>();
            var pttTargets    = new List<(double Price, int Qty, OrderAction Action)>();
            if (acc == null || instrument == null)
                return nativeTargets; // (1) JS-002: empty list, never null
            foreach (Order o in acc.Orders) // (2)
            {
                if (o == null)
                    continue; // (3)
                bool stateOk =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Initialized
                    || o.OrderState == OrderState.TriggerPending   // (4)
                    || o.OrderState == OrderState.ChangeSubmitted
                    || o.OrderState == OrderState.CancelSubmitted;
                bool instrOk = o.Instrument != null && o.Instrument.FullName == instrument.FullName; // (5)
                if (!stateOk || !instrOk || o.OrderType != OrderType.Limit)
                    continue;
                if (string.IsNullOrEmpty(o.Name))
                    continue;
                bool isNative =
                    o.Name.Length >= 7
                    && o.Name.StartsWith("Target", StringComparison.Ordinal)
                    && char.IsDigit(o.Name[6])
                    && o.Name[6] != '0';
                bool isPtt =
                    (o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                     && o.Name.Length > 8
                     && char.IsDigit(o.Name[8]))
                    || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
                if (isNative)            // (6)
                    nativeTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
                else if (isPtt)          // (7)
                    pttTargets.Add((o.LimitPrice, o.Quantity, o.OrderAction));
            }
            return nativeTargets.Count > 0 ? nativeTargets : pttTargets;
        }
```

---

### CHANGE B — Replace Step A loop in `MoveStopToBreakEven`

**Two sub-changes**:

#### B.1 — Update CYC annotation at L3271-3272

Replace the old annotation:
```
// CYC=8: IsFlat(1) + tickSize/pos guard(2) + snapshot-foreach(3) + stateOk(4) + instrOk(5)
//        + cancel-try(6) + 0-targets branch(7) + targets-for-loop(8).
```

With the new annotation:
```csharp
        // CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4)
        //        + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7).
        // DW-B107: Step A extracted to SnapshotBeTargets; while cap reduces stale residue.
```

#### B.2 — Replace Step A block L3373-3422

Replace the entire Step A comment block + `var targets = new List<...>()` declaration +
the full `foreach` loop body (L3373-3422) with:

```csharp
            // -- Step A: snapshot ATM target orders BEFORE cancelling anything ----
            // DW-B107: extracted to SnapshotBeTargets to keep MoveStopToBreakEven CYC=7.
            // Two-pass native-first collect: native Target1..9 take priority over
            // stale PTT-QX-T*/PTT-BE-Target-* residues (same logic as DW-B106).
            var targets = SnapshotBeTargets(acc, instrument); // (3)
```

Note: The `// (3)` suffix is required to maintain CYC annotation numbering consistency with
the updated annotation in B.1.

---

### CHANGE C — Insert `while` cap after `SnapshotBeTargets` call

**Insertion point**: Immediately after `var targets = SnapshotBeTargets(acc, instrument); // (3)`,
BEFORE `PttBreakEvenSwap.Execute(acc, instrument, newStop, targets)`.

Insert:
```csharp
            // DW-B107: hard cap -- BE/QX contract is always exactly 3 targets max.
            // Prevents stale partial-fill residue submitting extra OCO pairs.
            // No LINQ -- while-loop trim per JS zero-alloc mandate.
            while (targets.Count > 3)
                targets.RemoveAt(targets.Count - 1);
```

---

## 5. JS Rule Constraints

| Change | JS Rules Applied |
|--------|-----------------|
| CHANGE A (`SnapshotBeTargets`) | JS-002 (returns empty list, not null — never `return null`); JS-001 (no `throw`, all paths use early `return` or value return); JS-021 (no `lock()`, local list operations only); JS-033 (synchronous — no `async` keyword); ASCII-only identifiers and string literals; no LINQ |
| CHANGE B (call site + annotation update) | ASCII-only comments; CYC annotation update consistent with new branch count |
| CHANGE C (`while` cap) | JS-001 (no `throw`); JS-021 (no `lock()`); ASCII-only comment text; no LINQ (`while + RemoveAt` only — not `.Take()`, `.GetRange()`, `.Where()`, `.Select()`) |

---

## 6. 7-Scan Checklist

The engineer MUST run ALL 7 scans and achieve zero new findings before reporting BUILD_PASS.
Pre-existing results in unmodified code are explicitly out of scope for each scan.

---

### SCAN-01 — `lock()` check (JS-021 P0 BLOCKER)

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("
```

**Expected**: Zero new `lock()` occurrences in `SnapshotBeTargets` or the `while`-cap block.
Pre-existing `lock(` in unmodified code is out of scope.
**FAIL condition**: Any `lock(` found in the new `SnapshotBeTargets` body or the cap block.

---

### SCAN-02 — `async void` check (JS-033 P0 BLOCKER)

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void "
```

**Expected**: Zero results. All new code is synchronous. No `async` keyword anywhere in
`SnapshotBeTargets` or the cap block.
**FAIL condition**: Any `async void` in newly added lines.

---

### SCAN-03 — `return null` check (JS-002 P0 BLOCKER)

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null;"
```

**Expected**: Zero new `return null` in `SnapshotBeTargets`. Pre-existing `return null` lines
in unmodified regions (e.g., L1509, L2004, L2050, L3162, L3168, L3231, L4049) are out of scope.
`SnapshotBeTargets` MUST return `nativeTargets` (empty list) on null input — never `null`.
**FAIL condition**: Any `return null;` found inside `SnapshotBeTargets`.

---

### SCAN-04 — Non-ASCII check

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"
```

**Expected**: Zero non-ASCII characters in any new or changed lines.
Pre-existing non-ASCII at L316, L317, L2880, L2881 are out of scope (those are unmodified).
**FAIL condition**: Any non-ASCII byte in `SnapshotBeTargets` body, Step A replacement, or cap block.

---

### SCAN-05 — CYC check

Run manual count OR `python scripts/complexity_audit.py`.

| Method | File | Expected CYC |
|--------|------|-------------|
| `MoveStopToBreakEven` | `CopyEngine.cs` | 7 |
| `SnapshotBeTargets` | `CopyEngine.cs` | 7 |

Both methods MUST be <= 8. Ticket is invalid if either method exceeds 8.
**FAIL condition**: `MoveStopToBreakEven` or `SnapshotBeTargets` reports CYC > 8.

---

### SCAN-06 — LINQ check

```powershell
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\.Take\(|\.GetRange\(|\.Where\(|\.Select\("
```

**Expected**: Zero LINQ calls in new code (`SnapshotBeTargets` body and the `while`-cap block).
`while + RemoveAt` is the ONLY accepted cap pattern.
**FAIL condition**: Any `.Take(`, `.GetRange(`, `.Where(`, or `.Select(` found in newly added lines.

---

### SCAN-07 — `stateOk` 7-state completeness check (correctness gate)

**Manual inspection** of `SnapshotBeTargets` `stateOk` block. Verify all 7 states are present:

| # | State | Must Be Present |
|---|-------|----------------|
| 1 | `OrderState.Working` | YES |
| 2 | `OrderState.Accepted` | YES |
| 3 | `OrderState.Submitted` | YES |
| 4 | `OrderState.Initialized` | YES |
| 5 | `OrderState.TriggerPending` | YES |
| 6 | `OrderState.ChangeSubmitted` | YES |
| 7 | `OrderState.CancelSubmitted` | YES |

**FAIL condition**: Any of the 7 states is missing. Narrowing `stateOk` to `Working|Accepted`
only is a DW-B79 regression and constitutes a P0 correctness failure.

---

## 7. Acceptance Criteria (T1-T15)

Each criterion is pass/fail binary. Verifier inspects `src/PropTraderTools/CopyEngine.cs` only.

### [T1] — `SnapshotBeTargets` method exists
- Private instance method named `SnapshotBeTargets` present in `CopyEngine.cs`
- Return type: `List<(double Price, int Qty, OrderAction Action)>`
- Parameters: `(Account acc, Instrument instrument)`
- Located immediately before `MoveStopToBreakEven` in the file

### [T2] — `SnapshotBeTargets` null guard (JS-002)
- First statement after the two `var` declarations is:
  `if (acc == null || instrument == null) return nativeTargets;`
- Returns `nativeTargets` (empty list), NOT `null`
- No `return null` anywhere in the method

### [T3] — `SnapshotBeTargets` two-pass structure
- Two separate lists declared: `nativeTargets` and `pttTargets`
- Both typed `List<(double Price, int Qty, OrderAction Action)>`
- `if (isNative)` adds to `nativeTargets`
- `else if (isPtt)` adds to `pttTargets`
- Return: `nativeTargets.Count > 0 ? nativeTargets : pttTargets`

### [T4] — `SnapshotBeTargets` `stateOk` has exactly 7 states
- `stateOk` includes all of: `Working`, `Accepted`, `Submitted`, `Initialized`,
  `TriggerPending`, `ChangeSubmitted`, `CancelSubmitted`
- No states added or removed vs the original Step A loop

### [T5] — `SnapshotBeTargets` `isNative` includes `[6] != '0'` guard
- `isNative` condition:
  `o.Name.Length >= 7 && o.Name.StartsWith("Target", StringComparison.Ordinal) && char.IsDigit(o.Name[6]) && o.Name[6] != '0'`
- All four sub-conditions present

### [T6] — `SnapshotBeTargets` `isPtt` covers both `PTT-QX-T*` and `PTT-BE-Target-*`
- `isPtt` condition:
  `(o.Name.StartsWith("PTT-QX-T", ...) && o.Name.Length > 8 && char.IsDigit(o.Name[8])) || o.Name.StartsWith("PTT-BE-Target-", ...)`
- Both branches of the OR present

### [T7] — `SnapshotBeTargets` CYC annotation present and reads CYC=7
- Header comment `// CYC=7: null guard(1)+foreach(2)+o==null(3)+stateOk(4)+instrOk+type(5)+if(isNative)(6)+else if(isPtt)(7).` present
- Or equivalent annotation identifying exactly 7 counted branches

### [T8] — Step A loop replaced (CHANGE B)
- Lines L3373-3422 no longer contain the old `var targets = new List<...>()` + `foreach` block
- Replacement is exactly:
  ```
  var targets = SnapshotBeTargets(acc, instrument); // (3)
  ```
  with the DW-B107 extraction comment above it

### [T9] — Step A comment block updated (CHANGE B)
- Old multi-line Step A comment (DW-B79-01, HOTFIX-MSTBE-QX-TARGETS-01 text) is replaced by
  the new 3-line comment referencing DW-B107 extraction and two-pass logic
- No references to `var targets = new List<(double Price, int Qty, OrderAction Action)>();`
  remain at the call site

### [T10] — `while` cap inserted (CHANGE C)
- `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1);` (or equivalent
  block form) is present immediately after `var targets = SnapshotBeTargets(...)` and
  BEFORE `PttBreakEvenSwap.Execute(acc, instrument, newStop, targets)`
- DW-B107 cap comment present

### [T11] — No LINQ in cap
- `targets.Take(3)`, `targets.GetRange(0,3)`, `.Where(...)`, `.Select(...)` must NOT appear
  near the cap site
- Only `while + RemoveAt` pattern acceptable

### [T12] — `MoveStopToBreakEven` CYC annotation updated
- Old annotation `CYC=8: IsFlat(1) + tickSize/pos guard(2) + snapshot-foreach(3) + stateOk(4) + instrOk(5) + cancel-try(6) + 0-targets branch(7) + targets-for-loop(8)` REMOVED
- New annotation referencing CYC=7 present with `while-cap(3)` as branch 3 and
  `DW-B107: Step A extracted to SnapshotBeTargets; while cap reduces stale residue.`

### [T13] — No `lock()` anywhere in new code
- `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` returns zero new occurrences in
  `SnapshotBeTargets` or the cap block

### [T14] — No `return null` anywhere in new code
- `SnapshotBeTargets` has no `return null;` statement
- `while`-cap block has no null return

### [T15] — `PttGlobalQuickExit.cs`, `PttQuickExit.cs`, `PttBreakEvenSwap.cs` unchanged
- File modification timestamps and content of these three files must be identical to pre-B108
- The engineer MUST NOT touch these files

---

## 8. CYC Before/After Table

| Method | File | CYC Before | CYC After | Delta | Limit | Status |
|--------|------|-----------|-----------|-------|-------|--------|
| `MoveStopToBreakEven` | `CopyEngine.cs` | 8 | 7 | -1 | 8 | PASS |
| `SnapshotBeTargets` | `CopyEngine.cs` | n/a (new) | 7 | n/a | 8 | PASS |

No existing method exceeds CYC=8 after B108. No other methods are touched.

---

## 9. Build Verification Steps

The engineer runs ALL of the following in order. Every step must pass before reporting
`BUILD_PASS`. Any failure = stop, fix, restart from SCAN-01.

1. **Run all 7 scans** (Section 6 above) — every scan must return zero new findings.
2. **Sync and verify**:
   ```powershell
   powershell -File scripts\ptt-sync-and-verify.ps1
   ```
   Must show **0 MISMATCH** lines. Any mismatch = fix before proceeding.
3. **F5 in NinjaTrader 8** — must compile green (zero errors). Do NOT merge without a
   green F5. This is the mandatory NT8 compilation gate.
4. **Confirm T1-T15** by code inspection of `src/PropTraderTools/CopyEngine.cs`.
   All 15 criteria must be PASS.
5. **Commit**:
   ```
   git commit -m "feat(ptt): B108 DW-B107 SnapshotBeTargets extraction + cap-at-3"
   ```
   Stage `src/PropTraderTools/CopyEngine.cs` and `docs/brain/B108/` only.

---

## 10. Prior Fixes Preserved (engineer checklist)

The following fixes from earlier blocks MUST NOT be regressed by B108. Verify each by
inspection of `SnapshotBeTargets` after implementation:

| Fix | Source | Preservation Required |
|-----|--------|----------------------|
| 7-state `stateOk` widening | DW-B79-01 + REPAIR-09 DW-B79-05 | Carried verbatim into `SnapshotBeTargets`; not narrowed to 2 states |
| `[6] != '0'` on `isNative` | Existing Step A (L3408) | Carried verbatim into `SnapshotBeTargets` |
| `PTT-QX-T*` and `PTT-BE-Target-*` fallback | HOTFIX-MSTBE-QX-TARGETS-01 | Carried into `pttTargets` bucket |
| `isRetry` guard on retry registration | DW-B79-04 | Untouched — lives outside the replaced Step A block |
| `diagTotal` logging block | DW-B79-02 DIAG | Untouched — lives at L3364-3371, before Step A |
