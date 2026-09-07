# DW-LB-FL-01 Ticket 1 Verification — v4

**Date**: 2026-08-10  
**Phase**: Ph4b — ptt-verifier  
**Commits verified**: 9fb6e7f0 (Fix A+B) | ee195d6c (Fix C)  
**Scope**: CopyEngine.cs — Fix A (TryFirePositionState), Fix B (FindFollowerBracketOrder), Fix C (IsNativeExitOnFlatLeader / AnyFollowerOpen)

---

## VERDICT: VERIFY_PASS

---

## 1. P0 Scan Results

### 1a. lock() scan — JS-021
```
grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
```
**Result**: 10 matches — ALL are comments of the form `// JS-021: no lock()` or `// no lock() anywhere`.  
Zero actual `lock(` statements in the file.  
**Changed methods**: None of the five changed methods contain any `lock(` usage.  
**Finding**: PASS — JS-021 satisfied.

### 1b. async void scan — JS-033
```
grep -rn "async void " src/PropTraderTools/CopyEngine.cs
```
**Result**: 2 matches — both are comments (`// JS-033: Tick is not async void` and `// NOT async void (JS-033)`).  
Zero actual `async void` method declarations in the file.  
**Finding**: PASS — JS-033 satisfied.

### 1c. return null scan — JS-002
```
grep -rn "return null;" src/PropTraderTools/CopyEngine.cs
```
**Result**: 15 matches file-wide.  
Changed method scope:
- `FindFollowerBracketOrder` (L3846): `return null;` — return type is `Order?` (nullable). Comment at L3823 annotates: "JS-002: Order? null contract unchanged." This pattern is pre-existing (established in prior waves), not introduced by Fix B. Fix B only added `OrderState.Initialized` to the pass-through filter at L3840.
- All other 14 occurrences are outside the five changed methods.  
**Finding**: PASS — the one `return null;` in the changed scope uses the declared nullable return type `Order?`, which is the correct NT8 pattern for a search function. Not a JS-002 violation.

---

## 2. CYC Constraints (lizard, columns: NLOC | CCN | token | PARAM | length)

| Method | Location | CCN | Limit | Status |
|--------|----------|-----|-------|--------|
| `TryFirePositionState` | L4153–4173 | **8** | ≤8 | ✅ PASS (at limit) |
| `FindFollowerBracketOrder` (thin overload) | L3796–3807 | **1** | ≤8 | ✅ PASS |
| `FindFollowerBracketOrder` (IEnumerable) | L3824–3847 | **8** | ≤8 | ✅ PASS (at limit) |
| `IsNativeExitOnFlatLeader` | L4688–4699 | **3** | ≤8 | ✅ PASS |
| `AnyFollowerOpen` | L4704–4718 | **3** | ≤8 | ✅ PASS |
| `TryDispatchLeaderFlat` | L4729–4753 | **7** | ≤8 | ✅ PASS |

All five changed methods (six counting overload) are at CYC ≤ 8. No violations.

---

## 3. Test Result

```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build
```

**Result**: Failed: 0, Passed: **117**, Skipped: 3 (pre-existing), Total: 120  
**Duration**: 24 ms  
**Finding**: PASS — 117/117 tests pass. Zero new failures.

---

## 4. Sync Result

```
powershell -File scripts\ptt-sync-and-verify.ps1
```

**Result**: `=== SYNC + VERIFY: PASS (18 files confirmed) ===`  
**MISMATCH lines**: 0  
**Finding**: PASS — all 18 files synced and MD5-verified clean.

---

## 5. JS Rule Violations

Checked against JS-001, JS-002, JS-021, JS-033 (P0 rules applicable to the changed methods):

| Rule | Check | Finding |
|------|-------|---------|
| JS-021 (lock ban) | 0 actual lock() in changed methods | PASS |
| JS-001 (no throw in hot path) | 0 throw statements in any changed method | PASS |
| JS-002 (no return null for missing) | Order? nullable return in FindFollowerBracketOrder pre-existing; not new | PASS |
| JS-033 (no async void) | 0 async void in changed methods or file | PASS |

**JS rule violations found**: NONE

---

## 6. Rationale

**Fix A** (`TryFirePositionState`, L4165–4166): `TryClearLeaderDirectionOnFlat` hoisted above the `HasPosDedupChanged` gate. The hoist is safe because `TryClearLeaderDirectionOnFlat` is only called when `!hasPos` (leader is flat), which is computed unconditionally. The dedup gate was previously suppressing the clear on repeated flat events — the hoist ensures the clear fires on every flat event, matching the intent documented at `DW-B142-MGC-02`. `TryClearLeaderDirectionOnFlat` uses `TryRemove`, which is idempotent and lock-free (ConcurrentDictionary). CYC unchanged at 8.

**Fix B** (`FindFollowerBracketOrder` IEnumerable overload, L3840): `OrderState.Initialized` added to the 5-state pass-through filter. The comment documents the rationale: ATM brackets may still be in `Initialized` state when SFB (SetFollowerBracket) fires. This expands the pass-through set by one state; it does not change the reject-by-default behavior for all other states. CCN unchanged at 8 (the 5-state compound was already counted as 5 branches per McCabe; adding one more branch raises CCN to 6 in strict McCabe — but lizard reports 8, which includes the foreach(1) and OrderPassesBracketGate(1) and MatchesBracketType(1) branches correctly for a total of 8).

**Fix C** (`IsNativeExitOnFlatLeader` + `AnyFollowerOpen`, L4688–4718): The old guard suppressed dispatch whenever the leader was flat. The new guard suppresses dispatch only when the leader is flat AND no follower has an open position. The `AnyFollowerOpen` helper (CYC=3) iterates follower accounts, skips nulls, and returns true on the first open follower — pure foreach iteration, no shared state, no locking. `IsNativeExitOnFlatLeader` chains `IsNativeExitName && !hasOpenPosition(leader) && !AnyFollowerOpen(...)` via short-circuit &&. `TryDispatchLeaderFlat` CYC drops from 8 (prior) to 7 (current) because the `AnyFollowerOpen` delegation absorbs one branch.

---

## VERIFY_PASS
