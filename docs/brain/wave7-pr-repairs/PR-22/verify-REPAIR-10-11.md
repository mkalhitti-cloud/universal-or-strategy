
# Verification Report: REPAIR-10 + REPAIR-11
# PR #22 -- wave7/pr3-s1-sima-core

verification_verdict: PASS
fix_confirmed: true
build_passed: true
gate_passed: true
no_regressions: true
semantic_check: PASS

---

## Findings Verified

### REPAIR-10 -- HasFsmForAccount null guard (line 699)
**File**: `src/V12_002.SIMA.Lifecycle.cs`
**Method**: `HasFsmForAccount(Account acct)`

- **Old text (absent)**: `f.AccountName` without null guard in `.Any()` lambda
- **New text (present)**: `f != null && string.Equals(f.AccountName, acct.Name, StringComparison.OrdinalIgnoreCase)`
- **Confirmed**: line 698-700 contains the `f != null &&` guard exactly as planned.

### REPAIR-11 -- FindOpenPositionForInstrument null guards (lines 706-707)
**File**: `src/V12_002.SIMA.Lifecycle.cs`
**Method**: `FindOpenPositionForInstrument(Account acct)`

- **Old text (absent)**: `p.Instrument.FullName` without null guards in `.FirstOrDefault()` lambda
- **New text (present)**: `p != null && p.Instrument != null && p.Instrument.FullName == Instrument.FullName && p.MarketPosition != MarketPosition.Flat`
- **Confirmed**: lines 706-709 contain both `p != null` and `p.Instrument != null` guards exactly as planned.

---

## Step-by-Step Results

| Step | Check | Result |
|------|-------|--------|
| 1 | Source truth -- old unsafe code absent | PASS |
| 1 | Source truth -- new guarded code present | PASS |
| 2 | `dotnet build Linting.csproj` | PASS (0 errors, 0 warnings) |
| 3 | `python3 scripts/wave7_prepush_gate.py --base origin/main` | GATE PASSED |
| 3a | ascii_only section | PASS |
| 3b | DateTime.Now check | PASS |
| 3c | lock() check | PASS |
| 3d | underscore locals | PASS |
| 3e | diff size | PASS (57,761 stripped -- under 150,000 limit) |
| 4 | `grep -n "lock(" src/V12_002.SIMA.Lifecycle.cs` | 0 new lock() calls (comment-only hit at line 500, not a lock call) |

---

## OKF Rules Checked

| Rule | Status |
|------|--------|
| lock() banned in src/ | PASS -- 0 actual lock() calls; line 500 hit is inside a // comment |
| DateTime.Now banned | PASS -- none introduced |
| ASCII-only | PASS -- gate check 1 passed |
| Underscore locals | PASS -- gate check 4 passed |
| CYC <= 8 | PASS -- both fixed methods are trivially single-expression lambdas, no new branches added |
| No new allocations on hot path | PASS -- guards are pure boolean short-circuits, zero allocation |
| OnStateChange idempotency | N/A -- fix touches helper lookup methods, not state transitions |
| independent_tracking | PASS -- acct.Positions and acct.Name used directly, no master proxy |

---

## Semantic Analysis (3-thought chain)

**Thought 1 -- Root cause correctly addressed?**
Both REPAIR-10 and REPAIR-11 guard against `NullReferenceException` in LINQ lambdas that
dereference fields (`f.AccountName`, `p.Instrument`, `p.Instrument.FullName`) on objects that
may be null in a concurrent broker feed. The `f != null` guard in REPAIR-10 and the
`p != null && p.Instrument != null` guards in REPAIR-11 precisely target the crash site.
Root cause is correctly identified and fixed.

**Thought 2 -- Fix satisfies OKF rules?**
The fix is two short-circuit boolean guards prepended to existing lambda predicates.
No new allocations, no lock(), no DateTime.Now, no scope widening. Both methods remain
single-expression lambda bodies. Cyclomatic complexity is unchanged or marginally reduced
(short-circuit evaluations cannot increase CYC). The production safety rule
(`independent_tracking`) is satisfied -- `acct.Positions` and `acct.Name` are read from
the specific Account instance passed as a parameter, not from `this.Account` master proxy.

**Thought 3 -- Regression risk?**
No regression risk. The guards are additive: they only skip iterations that would have
previously thrown `NullReferenceException`. Any non-null element behaves identically to
before the fix. Callers (`HydrateFromOpenPositions` and similar hydration paths) are
not affected because the return type and semantics are identical -- the methods now simply
return `false` / `null` when they encounter null entries instead of throwing.

---

## Git Verification

- Engineer commit: `bb5e552119318a18101466ab7a832f936e0f8a81`
- Worktree HEAD: `bb5e552119318a18101466ab7a832f936e0f8a81`
- HEAD matches committed SHA: YES

---

*Verification performed by independent Tier 3 verifier. Do NOT commit this file -- lane orchestrator commits all docs/ artifacts.*
