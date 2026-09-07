# DW-LB-FL-02 — Tickets
# Clone mode + BE ALL — Infinite PTT-Flatten Loop Fix

**Epic**: DW-LB-FL-02
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Source plan**: `docs/brain/DW-LB-FL-02/02-architecture-plan.md` (Revision 2, REVIEW_PASS)
**Ticket author**: ptt-architect
**Date**: 2026-08-22

---

## SPEC REQUIREMENTS SATISFIED

| Req ID | Description |
|--------|-------------|
| **DW-LB-FL-02** | Block PTT-Flatten dispatch when leader account is already flat and a native NT8 exit order (e.g. "Close") fires. Eliminates infinite flatten loop in Clone + BE ALL mode. |
| **DW-B65-01** (regression guard) | When leader HAS an open position and closes natively (position-lag case), dispatch MUST still propagate to followers. Guard 3.5 must evaluate false in this scenario. This regression MUST be covered by a dedicated xUnit [Fact]. |

---

## SCOPE LOCK

**TICKET 1 ONLY — `CopyEngine.cs` only — no other files.**

Production code changes: `src/PropTraderTools/CopyEngine.cs` (one file, three changes, ~24 lines net).
Test code changes: `tests/` project (new test class, ~100 lines, 10 [Fact] methods).
No other `.cs`, `.xaml`, `.csproj`, or resource file is touched by this ticket.

---

## TICKET 1 — Guard 3.5 + IsDispatchableState Extraction

### Overview

Three precise changes to `src/PropTraderTools/CopyEngine.cs`:

1. **Add** `IsDispatchableState` static helper method (immediately before `TryDispatchLeaderFlat`)
2. **Add** `IsNativeExitOnFlatLeader` static helper method (immediately after `IsDispatchableState`)
3. **Modify** `TryDispatchLeaderFlat` body: replace guard (1) expression + insert guard (3.5) + update method comment

---

### CHANGE 1 — Add `IsDispatchableState`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Insert immediately before the existing comment block at line 4659
(between `HasOpenPosition` closing brace at line 4657 and the `TryDispatchLeaderFlat` comment at line 4659)

**Exact code to insert** (verbatim — no modifications):

```csharp
        // DW-LB-FL-02 (V-01 extraction): extracted from TryDispatchLeaderFlat guard (1).
        // Returns true when the order state is one that triggers a flat dispatch (Filled or Cancelled).
        // Extraction moves the && out of TryDispatchLeaderFlat, freeing 1 CYC budget for guard (3.5).
        // CYC=2: 1 base + 1 boolean short-circuit (||).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        internal static bool IsDispatchableState(OrderState state)
        {
            return state == OrderState.Filled || state == OrderState.Cancelled;
        }

```

**Method signature**:
```csharp
internal static bool IsDispatchableState(OrderState state)
```

**Return type**: `bool`
**CYC**: 2 (1 base + 1 `||` short-circuit decision point)
**Visibility**: `internal static` (required for xUnit test access via `[InternalsVisibleTo]`)

---

### CHANGE 2 — Add `IsNativeExitOnFlatLeader`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Insert immediately after the `IsDispatchableState` closing brace (the blank line inserted above),
and immediately before the `TryDispatchLeaderFlat` comment block at line 4659.

**Exact code to insert** (verbatim — no modifications):

```csharp
        // DW-LB-FL-02: Guard helper -- returns true when a native exit order arrived on an
        // already-flat leader account. In that state the DW-B65-01 bypass must NOT propagate
        // a PTT-Flatten to followers because no follower position needs closing.
        // CYC=2: 1 base + 1 boolean short-circuit (&&).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        internal static bool IsNativeExitOnFlatLeader(
            string orderName,
            Account account,
            Instrument instrument,
            Func<Account, Instrument, bool> hasOpenPosition
        )
        {
            return IsNativeExitName(orderName) && !hasOpenPosition(account, instrument);
        }

```

**Method signature**:
```csharp
internal static bool IsNativeExitOnFlatLeader(
    string orderName,
    Account account,
    Instrument instrument,
    Func<Account, Instrument, bool> hasOpenPosition
)
```

**Return type**: `bool`
**CYC**: 2 (1 base + 1 `&&` short-circuit decision point)
**Visibility**: `internal static` (required for xUnit test access via `[InternalsVisibleTo]`)
**Thread safety**: Calls `hasOpenPosition` delegate which reads `acc.Positions` from the NT8
dispatch thread — same thread safety contract as existing guard (3) in `TryDispatchLeaderFlat`.
No `Dispatcher.InvokeAsync` required.

---

### CHANGE 3 — Modify `TryDispatchLeaderFlat`

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Lines 4659–4691 (current source)

**Replace the existing comment block + method body** (lines 4659–4691 inclusive) with the following
verbatim content:

```csharp
        // B65 T1 / DW-B91-B / DW-LB-FL-02: TryDispatchLeaderFlat -- CYC=8 (strict McCabe, at limit).
        // Guards: (1) state via IsDispatchableState, (2) follower, (2.5+2.6) non-flat-dispatch name,
        // (3.5) native-exit on flat leader (DW-LB-FL-02), (3) open-position race-safe, (4) foreach follower.
        // DW-B91-B: foreach body extracted to FlattenFollower. DW-LB-FL-02: guard (3.5) -- when a native
        // NT8 exit fires on a leader account that is already flat (e.g. user clicks Close after
        // PTT-BE-Stop filled), skip dispatch. Preserves DW-B65-01: when leader HAS position,
        // IsNativeExitOnFlatLeader returns false and guard (3.5) does not block.
        // DW-LB-FL-02 V-01: guard (1) uses IsDispatchableState to free 1 CYC budget for guard (3.5).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
        private static bool TryDispatchLeaderFlat(
            Account account,
            Instrument instrument,
            OrderState state,
            string orderName,
            CopyRule rule,
            Func<Account, bool> isFollower,
            Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne
        )
        {
            if (!IsDispatchableState(state))
                return false; // (1)
            if (isFollower(account))
                return false; // (2)
            if (IsNonFlatDispatchName(orderName))
                return false; // (2.5+2.6)
            if (IsNativeExitOnFlatLeader(orderName, account, instrument, hasOpenPosition))
                return false; // (3.5) DW-LB-FL-02: native exit on already-flat leader -- nothing to propagate
            if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument))
                return false; // (3)
            foreach (var acc in rule.FollowerAccounts) // (4)
                FlattenFollower(acc, instrument, hasOpenPosition, flattenOne); // DW-B91-B
            return true;
        }
```

**Signature** (unchanged from pre-fix):
```csharp
private static bool TryDispatchLeaderFlat(
    Account account,
    Instrument instrument,
    OrderState state,
    string orderName,
    CopyRule rule,
    Func<Account, bool> isFollower,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne
)
```

**Return type**: `bool`
**Signature change**: None — only the comment block and body change.

---

### CYC COMPLEXITY VERIFICATION (post-fix)

| Method | Pre-fix CYC (live source) | Post-fix CYC | Limit | Status |
|--------|---------------------------|--------------|-------|--------|
| `TryDispatchLeaderFlat` | 8 | 8 | 8 | PASS (at limit) |
| `IsDispatchableState` | N/A (new) | 2 | 8 | PASS |
| `IsNativeExitOnFlatLeader` | N/A (new) | 2 | 8 | PASS |
| `IsNativeExitName` | 6 (unchanged) | 6 | 8 | PASS |
| `FlattenFollower` | 3 (unchanged) | 3 | 8 | PASS |
| `FlattenOneAccount` | 2 (unchanged) | 2 | 8 | PASS |

**CYC arithmetic for `TryDispatchLeaderFlat` post-fix** (manual McCabe):
- Guard (1): `if (!IsDispatchableState(state))` = 1 DP (single `if`; `||` moved to helper)
- Guard (2): `if (isFollower(account))` = 1 DP
- Guard (2.5): `if (IsNonFlatDispatchName(orderName))` = 1 DP
- Guard (3.5): `if (IsNativeExitOnFlatLeader(...))` = 1 DP (single `if`; `&&` lives in helper)
- Guard (3): `if (!IsNativeExitName(orderName) && hasOpenPosition(...))` = 2 DP (`if` + `&&`)
- foreach (4): = 1 DP
- Total DPs = 7. CYC = 1 + 7 = **8**. At limit. PASS.

---

### JS RULE CONSTRAINTS

All constraints apply to new and modified code only (`IsDispatchableState`, `IsNativeExitOnFlatLeader`,
modified body of `TryDispatchLeaderFlat`).

| Rule | Requirement | Verification |
|------|-------------|--------------|
| **JS-021** (P0) | No `lock()` anywhere in new or modified code | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` — zero matches in new methods. PASS. |
| **JS-033** (P0) | No `async void` | No new async methods introduced by this fix. PASS. |
| **JS-001** (P0) | No `throw new XxxException` in hot path | New guards return `bool`; no `throw` in any new or modified method. PASS. |
| **JS-002** (P0) | No `return null` | `IsDispatchableState` returns `bool`. `IsNativeExitOnFlatLeader` returns `bool`. `TryDispatchLeaderFlat` returns `bool`. No null return path exists. PASS. |
| **JS-036/037** (P0) | No heap allocation in hot path | No `new` allocations in any guard. No array, list, or object created. PASS. |
| **ASCII-only** | All identifiers and string literals are ASCII | `IsDispatchableState`, `IsNativeExitOnFlatLeader`, `orderName`, `account`, `instrument`, `hasOpenPosition`, `state` — all ASCII. All comment text is ASCII. PASS. |
| **CYC <= 8** | All methods at or below the Jane Street strict limit | See table above. Max post-fix CYC = 8. PASS. |
| **DateTime.Now ban** | No `DateTime.Now` usage | Not used. PASS. |
| **NT8 API compliance** | No banned NT8 AddOn calls | See NT8 API table below. PASS. |

**NT8 API compliance table** (new and modified code only):

| API | Status in this ticket |
|-----|----------------------|
| `AtmStrategyCreate()` | Not used. PASS. |
| `AtmStrategyChangeStopTarget()` | Not used. PASS. |
| `Account.Change()` | Not used. PASS. |
| `Account.Positions` (via `hasOpenPosition` delegate) | Used via existing delegate — confirmed AddOnBase-safe per NT8_FULL_REFERENCE.md. PASS. |
| `DateTime.Now` | Not used. PASS. |
| `FontFamily` | Not used. PASS. |
| `CreateOrder` without `PTT-` prefix | Not used (no order creation). PASS. |

---

### 7-SCAN CHECKLIST

This checklist is the Layer 1 engineer contract. All 7 scans MUST be run and must return
the expected result before marking this ticket complete.

#### SCAN-01: lock() grep

```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```

**Expected**: Zero matches in `IsDispatchableState`, `IsNativeExitOnFlatLeader`, or the
modified body of `TryDispatchLeaderFlat`. (Pre-existing `lock()` in unrelated methods is
not this ticket's concern and must not be introduced by this change.)
**Fail condition**: Any match in newly added or modified lines.

---

#### SCAN-02: async void grep

```powershell
grep -n "async void " src/PropTraderTools/CopyEngine.cs
```

**Expected**: Zero new `async void` methods introduced by this ticket. No new async methods
are added at all.
**Fail condition**: Any new `async void` line in the diff introduced by this ticket.

---

#### SCAN-03: return null grep

```powershell
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```

**Expected**: Zero `return null` in `IsDispatchableState`, `IsNativeExitOnFlatLeader`, or
the modified `TryDispatchLeaderFlat` body. All three methods return `bool`.
**Fail condition**: Any `return null` in lines touched by this ticket.

---

#### SCAN-04: CYC complexity measurement

Manual McCabe count as documented in the CYC table above.

Post-fix expected values:
- `TryDispatchLeaderFlat`: **8** (at limit — not 9, not 7)
- `IsDispatchableState`: **2**
- `IsNativeExitOnFlatLeader`: **2**

**Fail condition**: `TryDispatchLeaderFlat` CYC > 8 (ticket must be rejected and returned
to architect if this happens). `IsDispatchableState` or `IsNativeExitOnFlatLeader` CYC > 8.

---

#### SCAN-05: ASCII-only scan

```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```

**Expected**: Zero matches in any line added or modified by this ticket.
New identifiers: `IsDispatchableState`, `IsNativeExitOnFlatLeader`, `orderName`, `account`,
`instrument`, `hasOpenPosition`, `state` — all ASCII. All comment text is ASCII.
**Fail condition**: Any non-ASCII character in lines touched by this ticket.

---

#### SCAN-06: NT8 API compliance

Manual review of new code only:
- No `AtmStrategyCreate()` call — PASS
- No `AtmStrategyChangeStopTarget()` call — PASS
- No `Account.Change()` call — PASS
- No `DateTime.Now` — PASS
- No `FontFamily` — PASS
- No `new Order(...)` or `CreateOrder(...)` without `PTT-` prefix — PASS
- `hasOpenPosition` delegate call: confirmed AddOnBase-safe (reads `acc.Positions`, same
  thread contract as existing guard (3)) — PASS

**Fail condition**: Any banned NT8 API in lines touched by this ticket.

---

#### SCAN-07: xUnit [Fact] coverage

10 named `[Fact]` test methods must be present and passing in the `tests/` project.
All use delegate injection — no NT8 runtime required.
Confirm `[InternalsVisibleTo("tests")]` assembly attribute exists in `CopyEngine.cs` or
the containing project's `AssemblyInfo.cs` (match the existing pattern used by `IsNativeExitName`).

See **xUnit Test Specification** section below for exact test body requirements.

**Fail condition**: Fewer than 10 `[Fact]` methods. Any `[Fact]` fails. Any test requires
NT8 runtime. `IsDispatchableState` or `IsNativeExitOnFlatLeader` not accessible from test
project (missing `[InternalsVisibleTo]` or wrong visibility modifier).

---

### XUNIT TEST SPECIFICATION

**Test class**: `CopyEngineLeaderFlatGuardTests` (new file in `tests/` project)
**Framework**: xUnit only (never NUnit, never MSTest — JS testing standard)
**Dependencies**: Delegate injection only. No NT8 runtime. No mocking framework required.

All 10 `[Fact]` methods are listed below with exact names and what each asserts.

---

#### 1. `IsDispatchableState_WhenFilled_ReturnsTrue`

```csharp
[Fact]
public void IsDispatchableState_WhenFilled_ReturnsTrue()
{
    Assert.True(CopyEngine.IsDispatchableState(OrderState.Filled));
}
```

**Covers**: `IsDispatchableState` returns true for `Filled` state.

---

#### 2. `IsDispatchableState_WhenCancelled_ReturnsTrue`

```csharp
[Fact]
public void IsDispatchableState_WhenCancelled_ReturnsTrue()
{
    Assert.True(CopyEngine.IsDispatchableState(OrderState.Cancelled));
}
```

**Covers**: `IsDispatchableState` returns true for `Cancelled` state.

---

#### 3. `IsDispatchableState_WhenWorking_ReturnsFalse`

```csharp
[Fact]
public void IsDispatchableState_WhenWorking_ReturnsFalse()
{
    Assert.False(CopyEngine.IsDispatchableState(OrderState.Working));
}
```

**Covers**: `IsDispatchableState` returns false for `Working` (non-terminal) state.

---

#### 4. `IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlat_ReturnsTrue`

```csharp
[Fact]
public void IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlat_ReturnsTrue()
{
    Func<Account, Instrument, bool> hasPos = (_, __) => false; // leader is flat
    bool result = CopyEngine.IsNativeExitOnFlatLeader("Close", null, null, hasPos);
    Assert.True(result);
}
```

**Covers**: The defect scenario — native exit name ("Close") + leader is flat = guard fires (returns true = block dispatch).

---

#### 5. `IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderHasPosition_ReturnsFalse`

```csharp
[Fact]
public void IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderHasPosition_ReturnsFalse()
{
    Func<Account, Instrument, bool> hasPos = (_, __) => true; // leader has position
    bool result = CopyEngine.IsNativeExitOnFlatLeader("Close", null, null, hasPos);
    Assert.False(result);
}
```

**Covers**: **DW-B65-01 regression path for the helper** — when leader HAS a position, guard must NOT fire.

---

#### 6. `IsNativeExitOnFlatLeader_WhenNonNativeExitAndLeaderFlat_ReturnsFalse`

```csharp
[Fact]
public void IsNativeExitOnFlatLeader_WhenNonNativeExitAndLeaderFlat_ReturnsFalse()
{
    Func<Account, Instrument, bool> hasPos = (_, __) => false; // leader is flat
    bool result = CopyEngine.IsNativeExitOnFlatLeader("PTT-BE-Stop-12345", null, null, hasPos);
    Assert.False(result);
}
```

**Covers**: Non-native exit name does not trigger guard (IsNativeExitName returns false for `PTT-` prefix).

---

#### 7. `TryDispatchLeaderFlat_WhenCloseOnFlatLeader_DoesNotFlattenFollowers`

```csharp
[Fact]
public void TryDispatchLeaderFlat_WhenCloseOnFlatLeader_DoesNotFlattenFollowers()
{
    // Arrange
    int flattenCallCount = 0;
    Func<Account, bool> isFollower = _ => false;
    Func<Account, Instrument, bool> hasPos = (_, __) => false; // leader is flat
    Action<Account, Instrument> flattenOne = (_, __) => flattenCallCount++;
    var rule = MakeSingleFollowerRule();

    // Act
    bool result = CallTryDispatchLeaderFlat(
        account: LeaderAccount(),
        instrument: TestInstrument(),
        state: OrderState.Filled,
        orderName: "Close",
        rule: rule,
        isFollower: isFollower,
        hasOpenPosition: hasPos,
        flattenOne: flattenOne
    );

    // Assert
    Assert.False(result);
    Assert.Equal(0, flattenCallCount);
}
```

**Covers**: **DW-LB-FL-02 root cause** — "Close" on already-flat leader must NOT dispatch PTT-Flatten to followers.

---

#### 8. `TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers`

```csharp
[Fact]
public void TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers()
{
    // Arrange
    int flattenCallCount = 0;
    Func<Account, bool> isFollower = _ => false;
    Func<Account, Instrument, bool> hasPos = (acc, _) =>
        // leader has position; follower also has position (so FlattenFollower proceeds)
        true;
    Action<Account, Instrument> flattenOne = (_, __) => flattenCallCount++;
    var rule = MakeSingleFollowerRule();

    // Act
    bool result = CallTryDispatchLeaderFlat(
        account: LeaderAccount(),
        instrument: TestInstrument(),
        state: OrderState.Filled,
        orderName: "Close",
        rule: rule,
        isFollower: isFollower,
        hasOpenPosition: hasPos,
        flattenOne: flattenOne
    );

    // Assert
    Assert.True(result);
    Assert.Equal(1, flattenCallCount);
}
```

**Covers**: **DW-B65-01 regression guard (MUST PASS)** — when leader has an open position,
"Close" fill must dispatch PTT-Flatten to followers. Guard 3.5 must evaluate false and not block.

---

#### 9. `TryDispatchLeaderFlat_WhenFlattenOnFlatLeader_DoesNotFlattenFollowers`

```csharp
[Fact]
public void TryDispatchLeaderFlat_WhenFlattenOnFlatLeader_DoesNotFlattenFollowers()
{
    int flattenCallCount = 0;
    Func<Account, bool> isFollower = _ => false;
    Func<Account, Instrument, bool> hasPos = (_, __) => false; // leader is flat
    Action<Account, Instrument> flattenOne = (_, __) => flattenCallCount++;
    var rule = MakeSingleFollowerRule();

    bool result = CallTryDispatchLeaderFlat(
        account: LeaderAccount(),
        instrument: TestInstrument(),
        state: OrderState.Filled,
        orderName: "Flatten",
        rule: rule,
        isFollower: isFollower,
        hasOpenPosition: hasPos,
        flattenOne: flattenOne
    );

    Assert.False(result);
    Assert.Equal(0, flattenCallCount);
}
```

**Covers**: Edge case — "Flatten" (also a native exit name) on already-flat leader must not dispatch.

---

#### 10. `TryDispatchLeaderFlat_WhenRevOnFlatLeader_DoesNotFlattenFollowers`

```csharp
[Fact]
public void TryDispatchLeaderFlat_WhenRevOnFlatLeader_DoesNotFlattenFollowers()
{
    int flattenCallCount = 0;
    Func<Account, bool> isFollower = _ => false;
    Func<Account, Instrument, bool> hasPos = (_, __) => false; // leader is flat
    Action<Account, Instrument> flattenOne = (_, __) => flattenCallCount++;
    var rule = MakeSingleFollowerRule();

    bool result = CallTryDispatchLeaderFlat(
        account: LeaderAccount(),
        instrument: TestInstrument(),
        state: OrderState.Filled,
        orderName: "RevToLong",
        rule: rule,
        isFollower: isFollower,
        hasOpenPosition: hasPos,
        flattenOne: flattenOne
    );

    Assert.False(result);
    Assert.Equal(0, flattenCallCount);
}
```

**Covers**: Edge case — "Rev*" native exit on already-flat leader must not dispatch.

---

### DW-B65-01 REGRESSION GUARD

**Explicit statement**: When the leader account has an open position (`hasOpenPosition(account, instrument) == true`),
guard (3.5) MUST evaluate to `false`. Specifically:

```
IsNativeExitOnFlatLeader("Close", LEADER, ES, hasOpenPosition)
= IsNativeExitName("Close") && !hasOpenPosition(LEADER, ES)
= true && !true
= true && false
= false           // guard does NOT block
```

This means the `return false` at guard (3.5) is NOT executed. Execution falls through to guard (3),
which also does not block for native exit names, and `FlattenFollower` is called for each follower.
DW-B65-01 is fully preserved.

**Dedicated [Fact] test for this regression case**:
`TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers` (test 8 above)

This test MUST pass. If it fails, the fix has broken DW-B65-01 and the ticket must be rejected.

---

### ACCEPTANCE CRITERIA — NT8 SIM GATE

**Setup**: NinjaTrader SIM mode. Clone copy mode. BE ALL enabled. 1 leader account + 1 follower account.

**Step 1**: Enter a long position on the leader (buy 1 ES at market).
- Verify: leader shows long 1 ES. Follower shows long 1 ES (copied). PTT-BE-Stop-* Working on follower.

**Step 2**: Trigger the BE stop fill. Move SIM price to the BE stop level or use SIM fill to fill
the PTT-BE-Stop-* on the follower.
- Verify: Leader position = 0 (flat). Follower position = 0 (flat, closed by PTT-BE-Stop-* fill).
- No PTT-Flatten should fire in this step.

**Step 3**: Click the "Close" button on ChartTrader for the ALREADY-FLAT leader account.
Wait 2–3 seconds.

**Step 4 — PASS criteria**:
- NT8 Output window: ZERO "PTT-Flatten Accepted" or "PTT-Flatten Working" messages on follower.
- NT8 Output window: ZERO "flat-guard: in-flight skip" repeating messages.
- Follower position: remains 0 (flat). No inversion to -1 (short).
- Order count in NT8 Order History: stable. No 40–100+ order loop.

**Step 5 — FAIL criteria** (loop still present — ticket must be rejected):
- Any "PTT-Flatten Accepted" message on follower after the Close click in Step 3.
- Follower position flips to -1 (short) after the Close click.
- Repeating status messages in the NT8 Output window.
- Order count grows after Step 3.

**Step 6 — DW-B65-01 regression test** (MUST PASS):
Enter a fresh long position on leader. Immediately (within the same bar, while position is live)
click Close on ChartTrader.
- Verify: follower receives PTT-Flatten and flattens to 0.
- If follower does NOT flatten = DW-B65-01 regression. Ticket FAIL.

---

### CHANGE SURFACE SUMMARY

| Change | File | Location | Lines delta |
|--------|------|----------|-------------|
| Add `IsDispatchableState` | `src/PropTraderTools/CopyEngine.cs` | Before line 4659 (insert) | +8 lines |
| Add `IsNativeExitOnFlatLeader` | `src/PropTraderTools/CopyEngine.cs` | After `IsDispatchableState` (insert) | +12 lines |
| Modify guard (1) + insert guard (3.5) + update comment | `src/PropTraderTools/CopyEngine.cs` | Lines 4659–4691 (replace) | +4 lines net |
| Add 10 xUnit [Fact] tests | `tests/` project (new class) | N/A | ~100 lines |

**Total production code delta**: ~24 lines in one file. Zero architectural changes. Zero new dependencies.
Zero new `using` statements required (all types — `OrderState`, `Account`, `Instrument`, `Func<>` — are
already in scope in `CopyEngine.cs`).

---

## RETURN: TICKETS_COMPLETE
