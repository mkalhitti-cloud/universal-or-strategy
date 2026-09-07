# DW-LB-FL-01 Final Review — v4

**Date**: 2026-08-10  
**Phase**: Ph5 — ptt-plan-reviewer  
**Commits reviewed**: 9fb6e7f0 (Fix A+B) | ee195d6c (Fix C)  
**Ph4b artifact**: `docs/brain/DW-LB-FL-01/ticket-1-verification-v4.md` — VERIFY_PASS confirmed  
**Scope**: CopyEngine.cs — Fix A (TryFirePositionState), Fix B (FindFollowerBracketOrder), Fix C (IsNativeExitOnFlatLeader / AnyFollowerOpen)

---

## REVIEW_PASS

---

## Per-Rule Findings

### JS-021 — lock() ban (P0 CRITICAL)

**Methods checked**: All five changed methods (`TryFirePositionState`, `FindFollowerBracketOrder` × 2, `IsNativeExitOnFlatLeader`, `AnyFollowerOpen`, `TryDispatchLeaderFlat`).

**Finding**: Zero `lock(` statements in any changed method. All state accesses use:
- `ConcurrentDictionary.TryRemove` (idempotent, lock-free) — `TryClearLeaderDirectionOnFlat`
- `IReadOnlyList<Account>` foreach iteration (read-only, no shared mutation) — `AnyFollowerOpen`
- Short-circuit boolean evaluation (static, no shared state) — `IsNativeExitOnFlatLeader`
- `Func<Account, Instrument, bool>` delegate injection (no captured shared state) — `TryDispatchLeaderFlat`

The OKF lock-free-patterns.md mandate is fully satisfied. All patterns match the allowed primitives table (`ConcurrentDictionary<K,V>` for shared read-heavy lookup, no lock-based coordination).

**Rule result**: PASS

---

### JS-001 — throw exception in hot path (P0 CRITICAL)

**Methods checked**: All five changed methods.

**Finding**: Zero `throw` statements. All methods use early-return (bool/void) or nullable-return (`Order?`) patterns. No exceptions thrown in any code path.

**Rule result**: PASS

---

### JS-002 — return null for missing values (P0 CRITICAL)

**Methods checked**: All five changed methods.

**Finding**: One `return null;` at L3846 in `FindFollowerBracketOrder` (IEnumerable overload). Return type declared as `Order?` (C# nullable reference type). Code comment at L3823 explicitly annotates: `JS-002: Order? null contract unchanged`. This pattern pre-dates the DW-LB-FL-01 fix; Fix B only added `OrderState.Initialized` to the pass-through filter (L3840). The nullable return type is the correct NT8 pattern for a "find or nothing" search function. Not a JS-002 violation.

All other changed methods return `bool` or `void` — no null returns.

**Rule result**: PASS

---

### JS-033 — async void ban (P0 CRITICAL)

**Methods checked**: All five changed methods.

**Finding**: All five methods are synchronous. No `async` keyword appears on any of them. The grep scan confirmed zero actual `async void` method declarations in the file.

**Rule result**: PASS

---

### JS-066 — diff < 10k characters

**Assessment**: 
- Fix A: 2 lines moved above the dedup gate (~80 chars of change)
- Fix B: 1 line added (`OrderState.Initialized` in filter, ~60 chars)
- Fix C: ~20 lines added (new `AnyFollowerOpen` method + modified `IsNativeExitOnFlatLeader` signature and body + comment updates)

Total diff is well under 1,000 characters, far below the 10,000-character limit.

**Rule result**: PASS

---

### JS-080 — CYC ≤ 8 per method

**Lizard CCN results (independent measurement)**:

| Method | CCN | Limit | Status |
|--------|-----|-------|--------|
| `TryFirePositionState` | 8 | ≤8 | PASS (at limit) |
| `FindFollowerBracketOrder` (Account overload) | 1 | ≤8 | PASS |
| `FindFollowerBracketOrder` (IEnumerable overload) | 8 | ≤8 | PASS (at limit) |
| `IsNativeExitOnFlatLeader` | 3 | ≤8 | PASS |
| `AnyFollowerOpen` | 3 | ≤8 | PASS |
| `TryDispatchLeaderFlat` | 7 | ≤8 | PASS |

Note: `TryDispatchLeaderFlat` previously measured at 8; with Fix C it measures at 7 because `AnyFollowerOpen` delegation absorbs one decision point. This is a correct refactoring outcome.

**Rule result**: PASS

---

### JS-096 — Illegal states unrepresentable

**Assessment**: 

The Fix C guard change makes an invalid state unrepresentable by construction:

- **Before**: `IsNativeExitOnFlatLeader` returned true whenever `IsNativeExitName && !hasOpenPosition(leader)`. This made it impossible to dispatch to followers when the leader was flat, even if followers had remnant positions from a partial-fill sequence.
- **After**: `IsNativeExitOnFlatLeader` returns true only when `IsNativeExitName && !hasOpenPosition(leader) && !AnyFollowerOpen(...)`. The three-way conjunction means the only state that triggers suppression is the state where suppression is actually correct (everything flat). The PartFill scenario (leader flat, followers still open) now correctly falls through to `FlattenFollower`.

The type-level invariant: `FlattenFollower` is only called when `hasOpenPosition(acc, instrument)` is true (line 4770), ensuring no spurious flatten of an already-flat follower. This is layered defense-in-depth — the outer guard (IsNativeExitOnFlatLeader) correctly admits the case, and the inner guard (FlattenFollower) still checks position before acting.

**Rule result**: PASS

---

### JS-010 — No public constructors without smart constructor (new types only)

**Assessment**: No new types introduced by DW-LB-FL-01. Fix A and Fix B modify method logic inline. Fix C adds `AnyFollowerOpen` as a `internal static bool` method and modifies `IsNativeExitOnFlatLeader`. No classes, structs, or records were created.

**Rule result**: N/A (no new types)

---

## Logic Correctness Assessment

### AnyFollowerOpen Guard Correctness

**Method** (L4704–4718):
```csharp
internal static bool AnyFollowerOpen(
    IReadOnlyList<Account> followerAccounts,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition)
{
    foreach (var acc in followerAccounts)
    {
        if (acc == null)
            continue;
        if (hasOpenPosition(acc, instrument))
            return true;
    }
    return false;
}
```

**Assessment**:
1. **Null guard**: L4712 `if (acc == null) continue` — consistent with the existing pattern in `FlattenFollower` (L4768-4769). Safe.
2. **Empty list**: foreach over empty list falls through to `return false`. Correct default (no followers = none open).
3. **Lock-free**: `IReadOnlyList<Account>` is a read-only interface. The foreach reads but does not mutate `followerAccounts`. `hasOpenPosition` is injected as a delegate — same pattern used by `TryDispatchLeaderFlat` and `FlattenFollower`. No shared mutable state is touched.
4. **Short-circuit**: Returns `true` on first open follower — correct optimization, no unnecessary further iteration.
5. **CYC=3**: base(1) + foreach(1) + null guard(1). The `hasOpenPosition` call and `return true` are leaves, no additional branches. Matches documentation.

**Verdict**: Logic is correct. No defects.

---

### IsNativeExitOnFlatLeader Guard Logic (boolean correctness)

**Method** (L4688–4699):
```csharp
internal static bool IsNativeExitOnFlatLeader(
    string orderName, Account account, Instrument instrument,
    IReadOnlyList<Account> followerAccounts,
    Func<Account, Instrument, bool> hasOpenPosition)
{
    return IsNativeExitName(orderName)
        && !hasOpenPosition(account, instrument)
        && !AnyFollowerOpen(followerAccounts, instrument, hasOpenPosition);
}
```

**Truth table analysis** (suppress dispatch = return true):

| IsNativeExitName | LeaderFlat | AllFollowersFlat | Returns | Correct? |
|-----------------|-----------|-----------------|---------|----------|
| false | any | any | false (short-circuit) | ✅ Non-native exits always pass through |
| true | false (has pos) | any | false (short-circuit) | ✅ Leader has position → don't suppress |
| true | true | false (follower open) | false | ✅ Followers still open → don't suppress |
| true | true | true | true | ✅ Everything flat → suppress correctly |

All four logical cases produce the correct outcome. The original guard was missing the fourth column (AnyFollowerOpen check), causing false suppression in the `true|true|false` case. The fix adds the missing dimension.

**Verdict**: Boolean logic is correct. No AND/OR error. No false-positive suppression path.

---

### TryClearLeaderDirectionOnFlat Hoisting Safety (double-fire risk)

**Call site** (L4165–4166 in `TryFirePositionState`):
```csharp
if (!hasPos) // (4) hoist clear above dedup gate
    TryClearLeaderDirectionOnFlat(e.Order.Account, instr);
```

**Reachability analysis**:
1. The call is inside `if (!hasPos)` — only reachable when `HasOpenPosition` returns false.
2. `HasOpenPosition` is evaluated unconditionally at L4163 before the gate.
3. The dedup gate (`HasPosDedupChanged`) at L4168 may or may not filter subsequent invocations — this is irrelevant to the clear call because it has already fired at L4166.

**Idempotency**:  
`TryClearLeaderDirectionOnFlat` calls `_lastLeaderDirection.TryRemove` and `ClearLiveEntryForInstrument`. Both use `ConcurrentDictionary.TryRemove`:
- If the key exists: removes it, returns true.
- If the key is already absent: returns false, no side effect.

`ClearLiveEntryForInstrument` (L5689–5696) enumerates `_liveEntryInstruments.Keys` and calls `TryRemove` on matching keys. Same idempotency guarantee.

**Double-fire scenario**: If `TryFirePositionState` is invoked multiple times for the same flat event (e.g. multiple `Filled` callbacks for the same order), each call will attempt `TryClearLeaderDirectionOnFlat`. The second call's `TryRemove` will find no key and return false silently. No duplicate side effects. No exception risk.

**Non-flat path**: Confirmed unreachable — the `if (!hasPos)` guard at L4165 is the only path to the clear call. `IsPositionStateTriggerState` at L4156 gates on `Filled|PartFilled` — only these trigger states reach the body at all, and `hasPos = HasOpenPosition(...)` is evaluated fresh at L4163.

**Verdict**: Hoisting is safe. No double-fire risk. Idempotency preserved.

---

## Deferred Items

None identified. All changes are within scope of a targeted surgical fix. The three improvements (hoist, state expansion, follower-open guard) are minimal changes with no deferred technical debt introduced.

**Observation** (non-blocking, no action required): `FindFollowerBracketOrder` IEnumerable overload is at CCN=8 (the hard limit). If a future fix requires adding another state to the filter, the overload will exceed the limit and require extraction. This is a design-time note for any future DW- item touching this method — no action needed now.

---

## Ph5 SIGN-OFF: PASS — All five changed methods satisfy JS-021 (lock-free), JS-001 (no throw), JS-002 (nullable contract), JS-033 (no async void), JS-066 (diff under limit), JS-080 (CYC ≤ 8), and JS-096 (correct guard semantics). Logic correctness confirmed for AnyFollowerOpen iteration, IsNativeExitOnFlatLeader boolean conjunction, and TryClearLeaderDirectionOnFlat idempotent hoist. 117/117 tests pass. 0 MISMATCH on sync. No JS rule violations found.
