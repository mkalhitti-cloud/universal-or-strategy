# Ticket 1 Verification — DW-B161B166-01 (VIOLATION-01-FIX)

**Verifier:** ptt-verifier (Phase 4b)
**Date:** 2026-09-15
**File verified:** `src/PropTraderTools/CopyEngine.cs` (READ-ONLY)
**Epic:** DW-B161B166-01
**Engineer report:** `docs/brain/DW-B161B166-01/ticket-1-completion.md`

---

## Independent Scans (Layer 3 — run by verifier, NOT from engineer report)

All scans executed independently against the live source file.

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 lock() | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("` | All 14 hits are in **comments only** — zero executable `lock(` blocks. PASS |
| SCAN-02 non-ASCII | `Get-Content CopyEngine.cs \| Where-Object { $_ -match '[^\x00-\x7F]' }` | **0 hits**. PASS |
| SCAN-03 FontFamily | `Select-String … -Pattern "FontFamily"` | All 3 hits are in **comments only**. PASS |
| SCAN-04 hex colour | `Select-String … -Pattern "#[0-9A-Fa-f]{6}"` | **0 hits**. PASS |
| SCAN-05 CreateOrder prefix | Not applicable — new helper contains no CreateOrder call. N/A / PASS |
| SCAN-06 DateTime.Now | `Select-String … -Pattern "DateTime\.Now[^U]"` | All hits are in **comments only**. PASS |
| SCAN-07 \block\s*\( | `Select-String … -Pattern "\block\s*\("` | All hits are in **comments only**. PASS |

---

## Checklist

### (a) `IsBeSettleOrRetrySignalActive` exists and is correct

Source lines: L3165–L3187

| Sub-check | Expected | Actual | Status |
|-----------|---------|--------|--------|
| Access modifier | `private` | `private bool IsBeSettleOrRetrySignalActive(…)` at L3165 | **PASS** |
| Parameters | `(Account acc, Instrument instr)` | `(Account acc, Instrument instr)` | **PASS** |
| Return type | `bool` | `bool` | **PASS** |
| Null guard before acc.Name access | `if (acc == null \|\| instr == null) return false;` | L3167: `if (acc == null \|\| instr == null) return false;` | **PASS** |
| Signal 5 — IsBeSlotNonTerminal call | Present with correct log | L3168–L3175 | **PASS** |
| Signal 5 log string | `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` | L3171: identical | **PASS** |
| Signal 6 — IsBeRetryArmedFlat call | Present with correct log | L3177–L3184 | **PASS** |
| Signal 6 log string | `"[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "` | L3180: identical | **PASS** |
| Signal 5 before Signal 6 | L(Signal5) < L(Signal6) | 3168 < 3177 | **PASS** |
| Final `return false;` | Present | L3186 | **PASS** |
| PrintTo target | `NinjaTrader.NinjaScript.PrintTo.OutputTab1` | L3173, L3182: `PrintTo.OutputTab1` | **PASS** |

**CHECK (a): PASS**

---

### (b) `IsFollowerPositionBlocked` body no longer contains direct calls

`IsFollowerPositionBlocked` spans L3213–L3256.

- `IsBeSlotNonTerminal` calls in file: L3162 (comment), L3168 (inside new helper L3165–3187). Neither is inside L3213–3256. ✓
- `IsBeRetryArmedFlat` calls in file: L3162 (comment), L3164 (comment), L3177 (inside new helper), L3198/3205 (comments in parent doc-block). No executable call inside L3213–3256. ✓
- Only reference to the signals inside `IsFollowerPositionBlocked` body is the delegating call: L3253 `if (IsBeSettleOrRetrySignalActive(acc, instr))`.

**CHECK (b): PASS**

---

### (c) CYC of `IsFollowerPositionBlocked` = 8

Independent branch count (L3213–L3256):

| # | Branch | Line |
|---|--------|------|
| 1 | base | — |
| 2 | `if (acc == null)` | L3215 |
| 3 | `if (instr == null)` | L3216 |
| 4 | `if (HasOpenPosition(…))` | L3217 |
| 5 | `if (HasActivePttBeExits(…))` | L3226 |
| 6 | `if (HasSettlingBeTargets(…))` | L3235 |
| 7 | `if (HasActivePttBeTargets(…))` | L3244 |
| 8 | `if (IsBeSettleOrRetrySignalActive(…))` | L3253 |

No `&&`, `||`, `for`, `while`, or `case` in the body. **CYC = 8. Budget (≤ 8): ✓**

**CHECK (c): PASS**

---

### (d) CYC of `IsBeSettleOrRetrySignalActive` = 5

Independent branch count (L3165–L3187):

| # | Branch | Line |
|---|--------|------|
| 1 | base | — |
| 2 | `if (…)` node of null guard | L3167 |
| 3 | `\|\|` short-circuit in null guard | L3167 |
| 4 | `if (IsBeSlotNonTerminal(…))` | L3168 |
| 5 | `if (IsBeRetryArmedFlat(…))` | L3177 |

**CYC = 5. Budget (≤ 8): ✓**

Matches engineer's report and plan convention (if-node counts as 1 + each `||` counts as 1).

**CHECK (d): PASS**

---

### (e) Log strings byte-for-byte identical to originals

Signal 5 log at L3171–L3172:
```
"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
    + acc.Name + "\n" + instr.FullName
```
Required: `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): " + acc.Name + "\n" + instr.FullName`
**Match: ✓**

Signal 6 log at L3180–L3181:
```
"[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): "
    + acc.Name + "\n" + instr.FullName
```
Required: `"[PTT-COPY-GUARD] ghost-entry blocked (be-retry-armed-flat): " + acc.Name + "\n" + instr.FullName`
**Match: ✓**

Both strings are ASCII-only (SCAN-02 confirmed 0 non-ASCII chars file-wide).

**CHECK (e): PASS**

---

### (f) Updated CYC comment — no stale text, correct new text

Comment at L3207–L3209:
```
// CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1(4) + Signal2(5)
//        + Signal3(6) + Signal4(7) + IsBeSettleOrRetrySignalActive(8). Signals 5+6 delegated.
//        DW-B161B166-01: was CYC=9 after B166 direct Signal6 add. Extraction restores budget.
```

| Sub-check | Required | Actual | Status |
|-----------|---------|--------|--------|
| Old stale text absent | Must NOT contain "Signal5-branch(8). Signal 6 delegated." | Absent (grep: 0 hits) | **PASS** |
| References `IsBeSettleOrRetrySignalActive(8)` | Must be present | L3208: present | **PASS** |
| References `DW-B161B166-01` | Must be present | L3209: present | **PASS** |
| Says "Signals 5+6 delegated." | Must be present | L3208: present | **PASS** |

**CHECK (f): PASS**

---

### (g) Diff is minimal — exactly 3 change sites

`DW-B161B166-01` appears at exactly 3 locations:
- L3160 — header comment of new helper (T1a)
- L3209 — CYC comment update (T1c)
- L3253 — delegating call (T1b)

`IsBeSettleOrRetrySignalActive` appears at exactly 3 locations:
- L3165 — definition (T1a)
- L3208 — CYC comment (T1c)
- L3253 — delegating call (T1b)

No other modification markers or unexpected edits were observed in the 3155–3260 region or in the SCAN results. All 7 scans show only pre-existing patterns; no new violations introduced.

**CHECK (g): PASS**

---

### (h) Jane Street rules in new helper (`IsBeSettleOrRetrySignalActive`, L3165–L3187)

| Rule | Check | Evidence | Status |
|------|-------|----------|--------|
| JS-001: no `throw` | No `throw` statement in L3165–L3187 | Not present in source read | **PASS** |
| JS-021: no `lock()` | No executable `lock(` anywhere in file | SCAN-01/07: all `lock(` hits in comments only | **PASS** |
| JS-042: ASCII-only string literals | No Unicode, curly quotes, or emoji | SCAN-02: 0 non-ASCII chars file-wide | **PASS** |
| JS-002: base case returns false | Final statement is `return false;` | L3167 (guard) and L3186 (final) both `return false` | **PASS** |

**CHECK (h): PASS**

---

## Engineer Layer 2 Cross-Check

| Item | Engineer Claimed | Verifier Independent Result | Match |
|------|-----------------|----------------------------|-------|
| Helper definition line | L3165 | L3165 | ✓ |
| Helper end line | L3187 | L3187 | ✓ |
| IsFollowerPositionBlocked start | L3213 | L3213 | ✓ |
| IsFollowerPositionBlocked end | L3256 | L3256 | ✓ |
| CYC IsFollowerPositionBlocked | 8 | 8 | ✓ |
| CYC IsBeSettleOrRetrySignalActive | 5 | 5 | ✓ |
| IsBeSettleOrRetrySignalActive hits | 3 | 3 (L3165, L3208, L3253) | ✓ |
| All 7 scans PASS | Yes | Yes | ✓ |
| ptt-sync 0 errors | Yes | Not re-run (verifier READ-ONLY) | N/A |

No discrepancies found between Layer 2 and Layer 3.

---

## Summary

All 8 checklist items (a)–(h) passed. All 7 independent scans passed. No Jane Street DNA violations found. CYC budgets met. Log strings identical. Diff minimal and confined to the 3 specified change sites.

---

## VERIFY_PASS
