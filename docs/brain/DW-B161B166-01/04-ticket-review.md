# Ticket Review: DW-B161B166-01 (RE-REVIEW — VIOLATION-01-FIX)

**Reviewer:** ptt-ticket-reviewer (Phase 3.5)
**Date:** 2026-09-15
**Round:** 2 (Re-review after V-01 fix by architect)
**Input tickets:** `docs/brain/DW-B161B166-01/04-tickets.md`
**Input plan:** `docs/brain/DW-B161B166-01/02-architecture-plan.md`
**Source verified:** `src/PropTraderTools/CopyEngine.cs` (L3155–3250 read directly)

---

## V-01 RESOLUTION CHECK

**Previous failure:** T1a insertion point was specified by line number only — no text-match anchor.

**Resolution:** Ticket now specifies a text-match anchor:

> Search for the first occurrence of the line:
> `        // B160 DW-B160: extends FM-A guard with two additional signals to cover NT8 event-burst race.`
> Insert the new helper block immediately before that line.

**Independent source verification:**

- Anchor text confirmed at **L3160** in `CopyEngine.cs` via grep:
  `grep -n "B160 DW-B160: extends FM-A guard" src/PropTraderTools/CopyEngine.cs` → 1 hit at L3160 ✓
- L3159 is a blank line (blank separator between preceding method close and comment block) ✓
- L3158 is closing brace `}` of `HasActivePttBeTargets` ✓
- The `// B160 DW-B160:` anchor is the **first line** of the `IsFollowerPositionBlocked` comment block ✓
- `IsFollowerPositionBlocked` method signature begins at L3182 (immediately after the comment block) ✓
- Insertion point is correctly positioned: new helper will be inserted between L3159 blank line and L3160 comment block ✓

**V-01 status: RESOLVED ✓**

---

## Ticket T1 — CYC Restoration: Extract Signals 5+6 from `IsFollowerPositionBlocked`

### (a) V-01 Resolution Check
**PASS** — See section above. Text-match anchor present, independently verified in source.

### (b) Old Text Source Verification

| Sub-change | Anchor / Old Text | Source Confirmed |
|------------|-------------------|-----------------|
| T1a insertion | `// B160 DW-B160: extends FM-A guard…` at L3160 | ✓ PASS |
| T1b old block | L3222–3239: `if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5…` through closing `}` of Signal 6 block | ✓ PASS — exact text match at L3222–3239 |
| T1c old line | L3178: `// CYC=8: base(1) + acc-null(2) + instr-null(3) + Signal1-branch(4)…Signal 6 delegated.` | ✓ PASS — exact text match at L3178 |

**Source Verification: PASS**

### (c) New Text Syntax Validity

| Sub-change | Check | Result |
|------------|-------|--------|
| T1a: `private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)` | Signature valid, `{` / `}` balanced, `return bool` ✓ | PASS |
| T1a: null guard `if (acc == null \|\| instr == null) return false;` | Before any `acc.Name` access ✓ | PASS |
| T1a: two if-blocks with `return true;`, base `return false;` | Braces balanced, no dangling else ✓ | PASS |
| T1a: trailing blank line separator | Present as required ✓ | PASS |
| T1b: `if (IsBeSettleOrRetrySignalActive(acc, instr))  // … \n    return true;` | Valid single-branch if, `acc`/`instr` in scope ✓ | PASS |
| T1c: three `//` comment lines | Valid C# comment syntax, ASCII-only ✓ | PASS |

**New Text Syntax: PASS**

### (d) Verification Criteria Grep-Testability

| Criterion | Grep Command | Testable |
|-----------|-------------|----------|
| T1a — 3 hits | `grep -n "IsBeSettleOrRetrySignalActive" src/PropTraderTools/CopyEngine.cs` | ✓ PASS |
| T1b — IsBeSlotNonTerminal not in parent body | `grep -n "IsBeSlotNonTerminal" src/PropTraderTools/CopyEngine.cs` | ✓ PASS |
| T1b — IsBeRetryArmedFlat not in parent body | `grep -n "IsBeRetryArmedFlat" src/PropTraderTools/CopyEngine.cs` | ✓ PASS |
| T1b — 1 hit inside parent | `grep -n "IsBeSettleOrRetrySignalActive" src/PropTraderTools/CopyEngine.cs` | ✓ PASS |
| T1c — 1 CYC comment hit | `grep -n "IsBeSettleOrRetrySignalActive(8)" src/PropTraderTools/CopyEngine.cs` | ✓ PASS |

**Verification Criteria: PASS**

### (e) 7-Scan Checklist Presence

| Scan | Description | Present |
|------|-------------|---------|
| SCAN-01 | JS-001: no throw in new/modified code | ✓ |
| SCAN-02 | JS-002: no null return from non-void method | ✓ |
| SCAN-03 | JS-021: no lock() blocks | ✓ |
| SCAN-04 | JS-042: ASCII-only string literals | ✓ |
| SCAN-05 | JS-CYC: both methods CYC ≤ 8 | ✓ |
| SCAN-06 | NT8-001: no new NT8 API calls | ✓ |
| SCAN-07 | GREP-LOCK: zero lock() matches attributable to T1 | ✓ |

All 7 scans present and have associated grep commands or inspection criteria.

**Scan Checklist: PASS**

### (f) Single-File Constraint

- T1 header: `**File:** src/PropTraderTools/CopyEngine.cs` ✓
- Scope declaration: `Three surgical sub-changes, one file. SINGLE-PIPELINE.` ✓
- T1a, T1b, T1c all reference `src/PropTraderTools/CopyEngine.cs` only ✓
- No other files referenced anywhere in the ticket ✓
- Path is Wave workspace (`src/PropTraderTools/`) — not Director workspace ✓

**File Routing: PASS**

### (g) IsBeSettleOrRetrySignalActive Helper Properties

| Property | Required | Ticket States | Result |
|----------|----------|---------------|--------|
| Visibility | `private` | `private bool IsBeSettleOrRetrySignalActive(...)` | ✓ PASS |
| Parameters | `(Account acc, Instrument instr)` | Exact match | ✓ PASS |
| Return type | `bool` | `private bool ...` | ✓ PASS |
| Null guard before `acc.Name` | Required | `if (acc == null \|\| instr == null) return false;` is first statement; `acc.Name` accessed only after | ✓ PASS |
| CYC ≤ 8 | Required | CYC=5: base(1) + if(2) + \|\|(3) + IsBeSlotNonTerminal branch(4) + IsBeRetryArmedFlat branch(5) | ✓ PASS |

**Helper Properties: PASS**

### (h) T1b Removes Both Signal Blocks

- Old text (18 lines) covers:
  - Signal 5 block: `if (IsBeSlotNonTerminal(acc.Name))` + 8-line body (L3222–3230) ✓
  - Signal 6 block: `if (IsBeRetryArmedFlat(acc, instr))` + 8-line body (L3231–3239) ✓
- Replacement is **exactly one** delegating call: `if (IsBeSettleOrRetrySignalActive(acc, instr))` ✓

**T1b Signal Block Replacement: PASS**

### (i) T1c Comment References

- References `IsBeSettleOrRetrySignalActive(8)` ✓ (exact method name, correct branch number)
- References `DW-B161B166-01` ✓ (exact epic ID)

**T1c Comment References: PASS**

### JS Pre-Check (Jane Street DNA)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw` in new helper or modified parent body | ✓ PASS — `return false`/`return true` only |
| JS-002 | No `null` return from non-void method | ✓ PASS — helper is `bool`, returns `false` not `null` |
| JS-021 | No `lock()` | ✓ PASS — no lock described |
| JS-042 | ASCII-only string literals | ✓ PASS — both log strings are ASCII; copies of existing source strings |
| JS-008/009 | No mutable struct fields, no unfreezed brushes | N/A — no structs or brushes |

**JS Pre-Check: PASS**

### CYC Pre-Check

| Method | CYC | Budget | Result |
|--------|-----|--------|--------|
| `IsFollowerPositionBlocked` (post-fix) | 8 | ≤ 8 | ✓ PASS |
| `IsBeSettleOrRetrySignalActive` (new) | 5 | ≤ 8 | ✓ PASS |

**CYC Pre-Check: PASS**

### NT8 Check

| Constraint | Check | Result |
|------------|-------|--------|
| No async/await in lifecycle method | Not present | ✓ PASS |
| No `Account.All` outside Loaded | Not present | ✓ PASS |
| No `sealed` on TradeCopierWindow | Not present (not window class) | ✓ PASS |
| No `FontFamily` on WPF element | Not present | ✓ PASS |
| No hardcoded hex color | Not present | ✓ PASS |
| No `CreateOrder` without `PTT-` prefix | Not present | ✓ PASS |
| No `DateTime.Now` | Not present | ✓ PASS |

**NT8 Check: PASS**

### Traceability

| Ticket Item | Spec Requirement / Plan Item | Mapped |
|-------------|------------------------------|--------|
| T1a — new helper | DW-B161B166-01 (Extract Signals 5+6) | ✓ |
| T1b — replace Signal 5+6 blocks | DW-B161B166-01, JS-CYC (restore CYC to 8) | ✓ |
| T1c — update CYC comment | STALE-COMMENT requirement | ✓ |

No phantom work detected. No plan items missing coverage.

**Traceability: PASS**

### Test Coverage

No new public or internal methods added (helper is `private`). `IsFollowerPositionBlocked` signature is unchanged. Ticket explicitly states:
- Private helper is covered transitively through parent tests.
- Engineer must confirm all existing `IsFollowerPositionBlocked` tests pass after implementation.

This satisfies the test coverage requirement: no new `[Fact]` is required because no new public/internal surface is created.

**Test Coverage: PASS**

---

## Summary

| Check | Result |
|-------|--------|
| V-01 Resolution (text-match anchor present + source-verified) | ✓ PASS |
| Source text verification (T1a anchor, T1b block, T1c line) | ✓ PASS |
| New text syntax validity (T1a, T1b, T1c) | ✓ PASS |
| Verification criteria grep-testable | ✓ PASS |
| 7-Scan checklist (SCAN-01 through SCAN-07) | ✓ PASS |
| Single-file constraint + file routing | ✓ PASS |
| Helper properties (private, signature, null guard, CYC≤8) | ✓ PASS |
| T1b removes both signal blocks, one delegating call | ✓ PASS |
| T1c references method name + ticket ID | ✓ PASS |
| JS Pre-Check | ✓ PASS |
| CYC Pre-Check | ✓ PASS |
| NT8 Check | ✓ PASS |
| Traceability | ✓ PASS |
| Test Coverage | ✓ PASS |

---

## Overall: TICKET_REVIEW_PASS
