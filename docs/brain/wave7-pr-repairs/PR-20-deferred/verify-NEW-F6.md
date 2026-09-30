# Verify NEW-F6 -- Cascade suppression active-state guard
# Branch: wave7/pr20-deferred-repairs
# Finding: VALID-LOGIC-BUG (P2)

## Fix Description
Added `IsFsmStateActive` helper and gated the cascade suppression in
`ExecuteFollowerCascadeProcessFollower` to only fire for in-flight FSM states
(PendingCancel, Submitting). SubmitFailed and Idle specs no longer block cascade cleanup.

## Verification

### Code Changes
File: src/V12_002.Orders.Callbacks.AccountOrders.cs

ADDED (line 424):
```csharp
// [PR-20-deferred NEW-F6] Guard: true only for in-flight replace states (PendingCancel or Submitting).
// SubmitFailed and Idle specs must NOT suppress cascade cleanup -- the replace is not active.
private static bool IsFsmStateActive(FollowerReplaceSpec spec) =>
    spec != null
    && (
        spec.State == FollowerReplaceState.PendingCancel
        || spec.State == FollowerReplaceState.Submitting
    );
```

CHANGED (line 914):
BEFORE:
```
if (_followerReplaceSpecs.TryGetValue(followerKey, out b948FsmSpec))
```
AFTER:
```
if (_followerReplaceSpecs.TryGetValue(followerKey, out b948FsmSpec) && IsFsmStateActive(b948FsmSpec))
```

### Gates
- dotnet build Linting.csproj: 0 errors, 0 warnings -- PASS
- wave7_prepush_gate.py: GATE PASSED (6/6 checks)
- CYC check: ExecuteFollowerCascadeProcessFollower CYC=7 (threshold 8) -- PASS
- lock() check: none found -- PASS
- ASCII check: PASS
- DateTime.Now check: none introduced -- PASS

### Commit
SHA: 87f8d32b
Message: fix(wave7/pr20-deferred): NEW-F6 -- restrict cascade suppression to active FSM states (PendingCancel|Submitting)

### OKF Alignment
- Rule 5 (production safety): defense in depth -- only active-state FSMs should suppress
  cascade teardown. SubmitFailed spec means replace already failed; cascade must proceed
  to clean up the position.
- Rule 3 (FSM determinism): FSM state transitions are auditable -- suppression predicate
  now correctly reflects only in-flight states.
- Rule 6 (complexity): CYC=7 <= 8 -- PASS

verification_verdict: PASS
fix_confirmed: true
build_passed: true
gate_passed: true
cyc_achieved: 7
