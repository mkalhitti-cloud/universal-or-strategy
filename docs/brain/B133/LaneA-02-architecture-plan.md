# B133 LaneA Architecture Plan
**Phase**: 1 (Architecture)
**Status**: REVIEW_PASS
**Author**: ptt-architect
**Date**: 2026-08-21

---

## 1. CHANGE SUMMARY

**Defect ID**: DW-B142 (P0)

**Root Cause**: `SignalOrNameMatches` at `src/PropTraderTools/CopyEngine.cs` L2512 evaluates
`order.FromEntrySignal == signalName` as the primary matching branch. When both operands are
`null` (ATM bracket orders have `FromEntrySignal = null`; `FindFollowerBracketOrder` passes
`leaderOrder.FromEntrySignal` which is also `null` for ATM entries), the C# equality expression
`null == null` evaluates to `true`, causing the method to return `true` for every ATM bracket
order iterated. Because iteration order is deterministic, `Target1` is always encountered first
and matched. The caller `FindFollowerBracketOrder` then returns `Target1` to `SyncFollowerBracket`,
which calls `acc.Cancel(Target1)`. Cancelling `Target1` OCO-cancels the entire ATM bracket group
(Stop1-3, Target1-3), so a drag of `Target3` silently cancels all follower brackets.

**One-Line Fix Description**: Add a null-guard on `signalName` before the equality test so that
`null == null` can no longer produce a false-positive match; ATM orders then fall through to the
correct name-based fallback branch.

---

## 2. SCOPE

### Files Touched

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | 1-line change at L2512 — null-guard added to signal equality branch |
| `src/PropTraderTools/Tests/B133Tests.cs` | New file — 5 xUnit [Fact] tests, class `B133LaneATests` |

### Files NOT Touched

| File | Reason |
|------|--------|
| `FindFollowerBracketOrder` (L2524-2570) | Caller passes `leaderOrder.FromEntrySignal` correctly; fix is entirely in the predicate |
| `SyncFollowerBracket` (L2187) | Caller passes `leaderOrder.Name` correctly; no change needed |
| Any other `.cs` file | Fix is fully self-contained in the one predicate line |

---

## 3. FIX DESIGN

### Exact Before / After

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line**: 2512

```csharp
// BEFORE (DW-B142 bug):
if (order.FromEntrySignal == signalName) // (1) primary: signal equality (covers null==null)

// AFTER (fix):
if (signalName != null && order.FromEntrySignal == signalName) // (1) primary: signal equality (null-guarded)
```

The inline comment on L2512 must be updated to remove the parenthetical `(covers null==null)` since
that is precisely the behaviour being prohibited:

```csharp
if (signalName != null && order.FromEntrySignal == signalName) // (1) primary: signal equality (null-guarded)
```

### Why the Fix Is Correct

The null-guard `signalName != null` prevents the short-circuit true when both `signalName` and
`order.FromEntrySignal` are null. After the guard:

- **ATM orders** (`signalName == null`): branch (1) evaluates to `false` immediately. Execution
  falls through to branch (2) (`leaderName == null` check) and then branch (3) (`order.Name ==
  leaderName`), which is the correct ATM name-based matching path.
- **Strategy orders** (`signalName != null`): the guard passes, and `order.FromEntrySignal ==
  signalName` behaves identically to the pre-fix code. No regression possible.

CYC of `SignalOrNameMatches` remains 3 after the fix (the null-guard is a short-circuit within
the same conditional expression, not a new branch node in the control-flow graph).

### Why Callers Need No Changes

`FindFollowerBracketOrder` (L2524) passes `leaderOrder.FromEntrySignal` as `signalName`. For ATM
entries this value is `null`. After the fix, `signalName=null` correctly routes to the name-based
fallback without matching every order. The caller's semantics are unchanged.

`SyncFollowerBracket` (L2187) passes `leaderOrder.Name` as `leaderName`. This is the value used
in fallback branch (3) and is unaffected by the guard in branch (1).

### Jane Street DNA Compliance

This fix introduces no new `lock()`, `throw new`, `return null`, or `async void` constructs.
The one-line null-guard addition preserves the existing CYC=3 and violates no JS rules.

**CreateOrder**: N/A — this fix does not introduce any new `CreateOrder` calls. The PTT-
prefix mandate is not applicable to this change.

---

## 4. TEST DESIGN

### Mock / Stub Strategy for `Order`

`NinjaTrader.Cbi.Order` is a concrete class (not sealed in the NT8 assembly available to the test
project). The established pattern used by all prior B13x tests (`B131Tests.cs`, `B132Tests.cs`) is
**direct instantiation with property assignment**:

```csharp
private static Order StubOrder(string name, string? fromEntrySignal)
{
    var o = new Order();
    o.Name = name;
    o.FromEntrySignal = fromEntrySignal;
    return o;
}
```

This pattern works because `SignalOrNameMatches` reads only `order.FromEntrySignal` and
`order.Name` — no other NT8 state is accessed. The same stub helper is used in `B133Tests.cs`.

### Testable Accessor

`SignalOrNameMatches` is `internal static`. The test project accesses it via the testable
wrapper already present at `CopyEngine.cs` L2555-2557:

```csharp
internal static bool SignalOrNameMatchesTestable(Order order, string? signalName, string? leaderName)
    => SignalOrNameMatches(order, signalName, leaderName);
```

`[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` is declared at L46. No new accessor
or assembly attribute is required.

### All 5 Test Method Signatures

**Class**: `B133LaneATests` in `src/PropTraderTools/Tests/B133Tests.cs`

| # | Method Name | Inputs | Expected Output |
|---|-------------|--------|-----------------|
| 1 | `SignalOrNameMatches_NullSignal_DoesNotMatchBySignal` | `order.FromEntrySignal=null`, `signalName=null`, `leaderName="Target3"` | `false` (branch 1 guard fires, branch 3 requires `order.Name="Target3"` which is not set here) |
| 2 | `SignalOrNameMatches_NullSignal_MatchesByName` | `order.Name="Target3"`, `order.FromEntrySignal=null`, `signalName=null`, `leaderName="Target3"` | `true` (falls through to branch 3, name matches) |
| 3 | `SignalOrNameMatches_NullSignal_NoMatch_WrongName` | `order.Name="Target1"`, `order.FromEntrySignal=null`, `signalName=null`, `leaderName="Target3"` | `false` (branch 3 name mismatch) |
| 4 | `SignalOrNameMatches_NonNullSignal_MatchesBySignal` | `order.FromEntrySignal="ES"`, `signalName="ES"`, `leaderName=null` | `true` (branch 1 guard passes, signal matches) |
| 5 | `SignalOrNameMatches_NullLeaderName_NullSignal_NoMatch` | `order.FromEntrySignal=null`, `signalName=null`, `leaderName=null` | `false` (branch 1 guard fires, branch 2 null guard fires) |

### Test Detail Notes

**Test 1** — The key regression guard for DW-B142: after the fix, `null==null` must NOT return
`true`. The stub order has `Name="Stop1"` (anything other than `"Target3"`) so that branch 3 also
returns false, isolating branch 1 as the cause.

**Test 2** — Confirms the ATM fallback path works end-to-end after the null-guard is in place.
`order.Name` and `leaderName` both equal `"Target3"` — branch 3 fires correctly.

**Test 3** — Confirms branch 3 correctly rejects a wrong-name order, preventing false positives
via the fallback path.

**Test 4** — Confirms the existing strategy-order path is completely unbroken. `signalName` is
non-null, guard passes, `FromEntrySignal == signalName` fires as before.

**Test 5** — Confirms double-null (both `signalName` and `leaderName` are null) returns `false`.
Branch 1 guard fires; branch 2 `leaderName==null` guard fires. No match possible.

### Regression Strategy

All five prior test suites must continue to pass without modification:

| Suite | Count | Concern |
|-------|-------|---------|
| `B132Tests.cs` | 5 | `DeriveLeaderBracketIndex`, `FindLeaderStopPrice` — unrelated to `SignalOrNameMatches` |
| `B131Tests.cs` | 7 | Directly tests `SignalOrNameMatchesTestable` — must all pass with the new null-guard |
| `B130Tests.cs` | 8 | Unrelated scope |
| `B129Tests.cs` | 13 | Unrelated scope |

`B131Tests.cs` is the critical regression suite because it tests `SignalOrNameMatchesTestable`
directly. Verifying that all 7 B131 tests pass after the fix confirms no existing signal-based
matching is broken.

### ASCII Compliance

All new identifiers in `B133Tests.cs` (class name `B133LaneATests`, all method names, all
variable names) are ASCII-only characters. No Unicode, emoji, or curly quotes are used.

---

## 5. 7-SCAN CHECKLIST

Engineer MUST run all 7 scans against the post-change codebase and confirm all pass before
committing.

| ID | Command | Required Result |
|----|---------|-----------------|
| SCAN-01 | `grep -r "lock(" src/ --include="*.cs"` | 0 results |
| SCAN-02 | `grep -rn "async void " src/ --include="*.cs"` | 0 results |
| SCAN-03 | `grep -rn "return null;" src/ --include="*.cs"` | 0 results (or pre-existing only — no new occurrences in B133-touched files) |
| SCAN-04 | `grep -rn "throw new" src/ --include="*.cs"` | 0 new results in B133-touched files |
| SCAN-05 | `python scripts/complexity_audit.py` | 0 methods > CYC 8 in `CopyEngine.cs` and `B133Tests.cs` |
| SCAN-06 | Non-ASCII check on touched files: `Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\Tests\B133Tests.cs" -Pattern "[^\x00-\x7F]"` | 0 results |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 warnings |

---

## 6. RISKS / DEFERRED WORK

**DW- items**: None.

The fix is a single null-guard on one boolean subexpression. No new methods, no new fields, no
threading model changes, no NT8 API surface changes. CYC of `SignalOrNameMatches` stays at 3
(unchanged). The test file is additive (new class in new file). No existing code paths are
removed or restructured.

The NT8 `Order` constructor and `Name`/`FromEntrySignal` property assignment have been
empirically validated by B131Tests.cs (already in CI) using the same stub pattern. No test
harness uncertainty exists.

---

*Plan written by ptt-architect. Awaiting ptt-plan-reviewer.*
