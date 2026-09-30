# Verification Report -- PR-22 CR-1 + CR-2

**Finding IDs**: CR-1, CR-2  
**Branch**: wave7/pr3-s1-sima-core  
**Commit**: 4e2e211e  
**File**: src/V12_002.SIMA.Lifecycle.cs  
**Verifier**: v12-phase5-v-verify (independent, Tier 3)  
**Date**: 2025-07-22

---

## Findings Under Review

- **CR-1** (Line 157, `EnumerateApexAccounts`): `Account.All` enumerated without snapshot -- collection-modified exception risk
- **CR-2** (Line 1428, `SweepBrokerOrders`): `Account.All` enumerated without snapshot -- collection-modified exception risk

**Fix plan**: Add `.ToArray()` snapshot at both sites to prevent `InvalidOperationException` if `Account.All` is mutated during iteration.

---

## Step 1 -- Commit Present

```
git -C /tmp/wt-pr22 log --oneline -5
4e2e211e fix(wave7/pr22): CR-1+CR-2 -- Account.All.ToArray() snapshot in EnumerateApexAccounts and SweepBrokerOrders
```

Commit 4e2e211e is present as HEAD. **PASS**

---

## Step 2 -- Source Truth Check

### CR-1 (Line 157)

```csharp
// Line 157 -- AFTER fix:
foreach (Account acct in Account.All.ToArray())
```

- `Account.All.ToArray()` **IS present** at line 157. PASS
- `Account.All` without `.ToArray()` is **NOT present** at line 157. PASS

### CR-2 (Line 1428)

```csharp
// Line 1428 -- AFTER fix:
foreach (Account acct in Account.All.ToArray())
```

- `Account.All.ToArray()` **IS present** at line 1428. PASS
- `Account.All` without `.ToArray()` is **NOT present** at line 1428. PASS

---

## Step 3 -- Scope-Clean Check

`grep -n "Account.All"` output:

```
157:    foreach (Account acct in Account.All.ToArray())   <-- CR-1 FIXED
229:    foreach (Account acct in Account.All)             <-- out-of-scope, plain (expected)
655:    foreach (Account acct in Account.All)             <-- out-of-scope, plain (expected)
947:    // [FREEZE-PROOF] Snapshot Account.All to prevent InvalidOperationException
950:    Account[] accountSnapshot = Account.All.ToArray(); <-- pre-existing fix (not in scope)
1428:   foreach (Account acct in Account.All.ToArray())   <-- CR-2 FIXED
```

Only lines 157 and 1428 carry the `.ToArray()` fix (as planned).  
Lines 229 and 655 remain as plain `Account.All` (out-of-scope, untouched). **PASS**

No scope creep detected. **scope_clean: true**

---

## Step 4 -- Build Gate

```
dotnet build Linting.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**build_passed: true**

---

## Step 5 -- Prepush Gate

```
[PASS] Check 0 -- CS-only
[PASS] Check 1 -- ASCII-only
[PASS] Check 2 -- DateTime.Now (none introduced)
[PASS] Check 3 -- lock() (none found)
[PASS] Check 4 -- underscore locals (none found)
[PASS] Check 5 -- diff size (11,137 raw / 10,711 stripped, under 150,000 limit)

GATE PASSED. Ready to push.
```

**gate_passed: true**

---

## Step 6 -- Lock() Check

`grep -n "lock(" /tmp/wt-pr22/src/V12_002.SIMA.Lifecycle.cs | grep -v "//"` -- **exit code 1, zero matches**.

No `lock()` added. **lock_check: PASS**

---

## Step 7 -- ASCII / Unicode Check

Gate Check 1 (ASCII-only): **PASS** -- no non-ASCII characters introduced.

---

## Step 8 -- Semantic Check (OKF Rules)

**Relevant OKF rule** -- `production-engineering-billions.md > independent_tracking`:  
"Each account tracked independently, never proxied through master."

The fix touches `Account.All` iteration (not `this.Account` proxying), so the `independent_tracking` rule is not implicated here. The fix is purely defensive: taking a point-in-time snapshot via `.ToArray()` before iterating to prevent `InvalidOperationException` if the broker asynchronously modifies the `Account.All` collection during the loop.

**Thought 1 -- Root cause correctly addressed?**  
Yes. The bug is enumeration of a live collection (`Account.All`) that can be mutated by the broker thread concurrently. `.ToArray()` freezes a snapshot before the loop begins, eliminating the race. Both sites (`EnumerateApexAccounts` and `SweepBrokerOrders`) are genuine call sites of this pattern.

**Thought 2 -- Fix satisfies OKF rule(s)?**  
The fix is lock-free (no `lock()`, no Monitor/Mutex). It uses an immutable snapshot (array) in lieu of iterating the live collection -- consistent with the "static readonly collections are safe (immutable after init)" principle and the broader immutable snapshot pattern. No new allocations on the hot path beyond the one-time `.ToArray()` snapshot (these are lifecycle/sweep methods, not `OnBarUpdate`). PASS.

**Thought 3 -- Regression risk?**  
The snapshot is created at the loop boundary. The only behavioral change is that additions/removals to `Account.All` occurring *after* the snapshot is taken are not seen until the next call -- which is the correct semantics for a sweep/lifecycle method. No callers of these methods depend on seeing mid-iteration changes. No regression risk identified.

**semantic_check: PASS**

---

## OKF Rules Checked

| Rule | Check | Result |
|------|-------|--------|
| lock() banned | grep -n "lock(" -- 0 matches | PASS |
| DateTime.Now banned | gate Check 2 | PASS |
| NUnit/MSTest banned | no test files modified | N/A |
| ASCII-only | gate Check 1 | PASS |
| Underscore locals | gate Check 4 | PASS |
| Hot-path zero-alloc | lifecycle/sweep method -- not hot path | PASS |
| independent_tracking | fix is snapshot, not proxy change | PASS |

---

## Summary

```
verification_verdict:     PASS
fix_confirmed_line_157:   true
fix_confirmed_line_1428:  true
scope_clean:              true
build_passed:             true
gate_passed:              true
lock_check:               PASS
semantic_check:           PASS
```
