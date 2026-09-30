# Implementation Tickets — DW-B161B166-01 (VIOLATION-01-FIX)

**Epic:** DW-B161B166-01
**Phase:** 3 — Ticket Generation
**Author:** ptt-architect
**Date:** 2026-09-15
**Input plan:** `docs/brain/DW-B161B166-01/02-architecture-plan.md` (REVIEW_PASS)
**Input review:** `docs/brain/DW-B161B166-01/02-plan-review.md` (REVIEW_PASS)
**Source verified:** `src/PropTraderTools/CopyEngine.cs` (L3150–L3190 read directly — V-01 correction)

---

## Spec Requirements Satisfied

| Req ID | Description |
|--------|-------------|
| DW-B161B166-01 | Extract Signals 5+6 from `IsFollowerPositionBlocked` into `IsBeSettleOrRetrySignalActive` to restore parent CYC from 9 to 8 |
| JS-CYC | Both methods CYC ≤ 8 after change |
| STALE-COMMENT | CYC comment at L3178 corrected to reflect post-fix state |

---

## Ticket T1 — CYC Restoration: Extract Signals 5+6 from `IsFollowerPositionBlocked`

**File:** `src/PropTraderTools/CopyEngine.cs`
**Class:** `CopyEngine` (partial)
**Epic:** DW-B161B166-01
**Scope:** Three surgical sub-changes, one file. SINGLE-PIPELINE.

---

### T1a — Insert new private helper `IsBeSettleOrRetrySignalActive`

**Action:** INSERT new method body immediately before the comment block that precedes `IsFollowerPositionBlocked`.

**Insertion point (text-match anchor — use this, NOT a line number):**

Search for the first occurrence of the line:

```
        // B160 DW-B160: extends FM-A guard with two additional signals to cover NT8 event-burst race.
```

Insert the new helper block (see below) **immediately before** that line.
The existing blank line at L3159 (between the closing brace of the preceding method and the `// B160` comment) remains as-is — it becomes the blank line that separates the preceding method from the **new helper**.
Add exactly **one blank line** between the new helper's closing `}` and the `// B160 DW-B160:` comment line.

**Source-confirmed context (original file, before edit):**
```
L3158:         }                    ← closing brace of HasActivePttBeTargets
L3159:                              ← blank line (keep unchanged)
L3160:         // B160 DW-B160: …  ← INSERT NEW HELPER BEFORE THIS LINE
…
L3182:         internal bool IsFollowerPositionBlocked(…)
```

**After insertion the sequence must be:**
```
        }                           ← L3158 unchanged
                                    ← L3159 blank line unchanged
        // DW-B161B166-01 VIOLATION-01-FIX: …  ← new helper comment (first new line)
        private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
        { … }
                                    ← one blank separator line (new)
        // B160 DW-B160: extends FM-A guard …  ← was L3160, now shifted
```

**Exact text to insert (byte-for-byte):**

```
        // DW-B161B166-01 VIOLATION-01-FIX: extracted Signals 5+6 from IsFollowerPositionBlocked
        // to restore parent CYC to 8. Each signal emits its own reason-tagged log before returning.
        // CYC=5: base(1) + if(2) + ||(3) + IsBeSlotNonTerminal(4) + IsBeRetryArmedFlat(5).
        // JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
        // private: not called externally; same visibility as IsBeRetryArmedFlat.
        private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)
        {
            if (acc == null || instr == null) return false;    // (2)
            if (IsBeSlotNonTerminal(acc.Name))                 // (3) Signal 5
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return true;
            }
            if (IsBeRetryArmedFlat(acc, instr))                // (4) Signal 6
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return true;
            }
            return false;
        }

```

> The trailing blank line above is the blank separator between the helper's closing brace and the first comment line of `IsFollowerPositionBlocked`. It must be present exactly as shown.

**Verification criterion:**

```
grep -n "IsBeSettleOrRetrySignalActive" src/PropTraderTools/CopyEngine.cs
```

Must return **exactly 3 hits**:
1. Definition line: `private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)`
2. Call site in `IsFollowerPositionBlocked`: `if (IsBeSettleOrRetrySignalActive(acc, instr))`
3. CYC comment in `IsFollowerPositionBlocked`: `// ... + IsBeSettleOrRetrySignalActive(8) ...`

---

### T1b — Replace Signal 5 + Signal 6 blocks with single delegating call

**Action:** REPLACE lines **3222–3239** (after T1a insertion these lines shift; use text-match, not line numbers, when applying after T1a).

> IMPORTANT: If T1a is applied first, line numbers shift by approximately 30 lines. Apply T1b by searching for the exact old text below, not by hard line number.

**Exact old text (verbatim, lines 3222–3239 in the original unmodified file):**

```
            if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5 -- BE settle window (B165)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return true;
            }
            if (IsBeRetryArmedFlat(acc, instr))    // (9) Signal 6 -- post-fallback-BE window (B166)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
                        + acc.Name + "\n" + instr.FullName,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                return true;
            }
```

**Exact new text (verbatim replacement):**

```
            if (IsBeSettleOrRetrySignalActive(acc, instr))  // (8) Signals 5+6 -- BE settle/retry window (DW-B161B166-01)
                return true;
```

**Verification criteria:**

```
grep -n "IsBeSlotNonTerminal" src/PropTraderTools/CopyEngine.cs
```
Must NOT appear inside `IsFollowerPositionBlocked` body. (It will still appear in the new helper and at its definition site L9382.)

```
grep -n "IsBeRetryArmedFlat" src/PropTraderTools/CopyEngine.cs
```
Must NOT appear inside `IsFollowerPositionBlocked` body. (It will still appear in the new helper and at its definition site ~L3253+offset.)

```
grep -n "IsBeSettleOrRetrySignalActive" src/PropTraderTools/CopyEngine.cs
```
Must return exactly 1 hit inside `IsFollowerPositionBlocked` body (the new delegating call).

---

### T1c — Update stale CYC comment at L3178

**Action:** REPLACE line **3178** (in original file; will be offset after T1a insertion — use text-match).

**Exact old text (verbatim, L3178 in original file — single line):**

```
        // CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1-branch(4) + Signal2-branch(5) + Signal3-branch(6) + Signal4-branch(7) + Signal5-branch(8). Signal 6 delegated.
```

**Exact new text (verbatim replacement — three lines):**

```
        // CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1(4) + Signal2(5)
        //        + Signal3(6) + Signal4(7) + IsBeSettleOrRetrySignalActive(8). Signals 5+6 delegated.
        //        DW-B161B166-01: was CYC=9 after B166 direct Signal6 add. Extraction restores budget.
```

**Verification criterion:**

```
grep -n "IsBeSettleOrRetrySignalActive(8)" src/PropTraderTools/CopyEngine.cs
```

Must return exactly **1 hit** — the new CYC comment line inside the `IsFollowerPositionBlocked` comment block.

---

## Method Signatures

| Method | Signature | Visibility | Action |
|--------|-----------|------------|--------|
| `IsBeSettleOrRetrySignalActive` | `private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)` | `private` | NEW — inserted before `IsFollowerPositionBlocked` |
| `IsFollowerPositionBlocked` | `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)` | `internal` | MODIFIED — Signal 5+6 blocks replaced; CYC comment updated |

**No other method signatures change.**

---

## Application Order

Apply in this order to avoid confusion from shifting line numbers:

1. **T1c first** — update the stale CYC comment (text-match on the old `// CYC=8:` line — see T1c below). No line shift at this stage.
2. **T1a second** — insert new helper before the `// B160 DW-B160:` line (text-match anchor, see T1a above). After T1a the file grows by ~30 lines.
3. **T1b last** — replace Signal 5+6 block. Use text-match (the old text is unique in the file). Line numbers are now shifted by ~30 from original.

> Alternatively, if the editor tool supports text-search-and-replace across the whole file, all three changes can be applied in a single pass by matching on the exact old text shown above. Text-match is preferred over line-number targeting whenever possible.

---

## CYC Summary (post-implementation)

| Method | CYC Before | CYC After | Budget |
|--------|-----------|-----------|--------|
| `IsFollowerPositionBlocked` | 9 | 8 | ≤ 8 ✓ |
| `IsBeSettleOrRetrySignalActive` | — (new) | 5 | ≤ 8 ✓ |

---

## xUnit Tests

**No new tests required. No existing tests require modification.**

Rationale (from plan §13, confirmed by reviewer):
- `IsFollowerPositionBlocked` is `internal` (accessible via `InternalsVisibleTo`). Its signature is unchanged. All existing tests exercising Signal 5 and Signal 6 paths through the parent continue to exercise the same paths — the parent delegates to the helper on those branches.
- `IsBeSettleOrRetrySignalActive` is `private` — not directly callable from xUnit. Its behaviour is covered transitively through any test that triggers Signal 5 or Signal 6 through the parent.

**Engineer must confirm:** Run existing `IsFollowerPositionBlocked`-related test suite after implementation. All existing tests must pass with zero changes.

---

## 7-Scan Checklist (Engineer Contract)

The engineer must check all 7 boxes before marking T1 complete. Reviewer will FAIL any ticket submission that has unchecked items.

- [ ] **SCAN-01 JS-001:** No `throw` statements in new or modified code. Helper uses `return false` / `return true` only. Grep: `grep -n "throw" src/PropTraderTools/CopyEngine.cs` — must show zero new hits in `IsBeSettleOrRetrySignalActive` or the modified `IsFollowerPositionBlocked` body.
- [ ] **SCAN-02 JS-002:** No null return from non-void method. Helper returns `bool`; base case is `return false`, never `return null`. Grep: `grep -n "return null" src/PropTraderTools/CopyEngine.cs` — must show zero hits in new helper.
- [ ] **SCAN-03 JS-021:** No `lock()` blocks in new or modified code. Grep: `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — must return zero new hits in the added or modified lines.
- [ ] **SCAN-04 JS-042:** All string literals are ASCII-only. Both log strings `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` and `"[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "` are ASCII-only, copied verbatim from existing source. Visual inspection required.
- [ ] **SCAN-05 JS-CYC:** `IsFollowerPositionBlocked` CYC = 8 (count 8 `if` branches: L3184, L3185, L3186, L3195, L3204, L3213, and the two new null-guard + delegate). `IsBeSettleOrRetrySignalActive` CYC = 5 (count: base=1, if=2, ||=3, IsBeSlotNonTerminal=4, IsBeRetryArmedFlat=5). Both ≤ 8.
- [ ] **SCAN-06 NT8-001:** No new NinjaTrader API calls beyond those already in the extracted Signal 5 and Signal 6 blocks. `NinjaTrader.Code.Output.Process` and `NinjaTrader.NinjaScript.PrintTo.OutputTab1` are existing calls — not new. No AtmStrategyCreate, no AtmStrategyChangeStopTarget, no Account.Change.
- [ ] **SCAN-07 GREP-LOCK:** `grep -r "lock(" src/PropTraderTools/` returns zero matches attributable to T1 changes. (Pre-existing hits, if any elsewhere in the file, are out of scope for this ticket.)

---

## Completion Criteria

T1 is **COMPLETE** when all of the following are true:

1. `grep -n "IsBeSettleOrRetrySignalActive" src/PropTraderTools/CopyEngine.cs` returns exactly 3 hits (definition, call site in parent, CYC comment in parent).
2. `grep -n "IsBeSlotNonTerminal" src/PropTraderTools/CopyEngine.cs` returns zero hits inside `IsFollowerPositionBlocked` body (helper only + definition at ~L9382).
3. `grep -n "IsBeRetryArmedFlat" src/PropTraderTools/CopyEngine.cs` returns zero hits inside `IsFollowerPositionBlocked` body (helper only + definition at ~L3253+offset).
4. `grep -n "IsBeSettleOrRetrySignalActive(8)" src/PropTraderTools/CopyEngine.cs` returns exactly 1 hit (the new CYC comment).
5. All 7 scan checklist items checked.
6. Existing `IsFollowerPositionBlocked` xUnit tests pass without modification.
7. `powershell -File .\scripts\ptt-sync.ps1` run after edit — hard-link sync confirmed.

---

TICKETS_COMPLETE
