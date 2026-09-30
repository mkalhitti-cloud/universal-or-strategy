# B66-LaneA Architecture Plan

**Block**: B66-LaneA
**Written by**: ptt-architect (Phase 1)
**Date**: 2026-08-13
**Status**: PLAN_COMPLETE

---

## A. Problem Summary

### DW-B66-01 -- CancelQxBrackets misses ATM bracket order names (CLOSED this block)

**Priority**: P0 (live trading correctness -- double-bracket incident)
**Production incident**: 2026-08-13 ~07:50 UTC. Double-bracket orders remained live on 4 follower
accounts after Quick Exit was pressed with an active ATM strategy. The ATM bracket orders
("Stop1", "Stop2", "Target1", "Target2") were not cancelled because `CancelQxBrackets` only
matches the `"PTT-QX-"` prefix.

**Root cause**: [`CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:436) line 436:

```csharp
if (o.Name != null && o.Name.StartsWith("PTT-QX-"))  // BEFORE: misses ATM bracket names
```

ATM bracket orders use names `"Stop1"`, `"Stop2"`, `"Target1"`, `"Target2"` -- confirmed by
`NT8_FULL_REFERENCE.md` line 1631:
> "The order name such as 'Stop1' or 'Target2'"

PTT-BE-* orders (`"PTT-BE-Stop"`, `"PTT-BE-Stop-{i+1}"`, `"PTT-BE-Target-{i+1}"`) were also not
matched by the old prefix filter.

**Fix**: Introduce two helpers:
- `IsAtmBracketName(string name)` (internal static, expression body, CYC=1) -- checks exact ATM bracket names.
- `IsQxCancelCandidate(Order o)` (internal static, CYC=5) -- null-guards then delegates to IsAtmBracketName and two prefix checks.

Replace the single-prefix check in `CancelQxBrackets` with a call to `IsQxCancelCandidate`.

---

## B. Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Modify | Add `IsAtmBracketName` and `IsQxCancelCandidate` before line 422; replace line 436 predicate |
| `src/PropTraderTools/CopyEngineTests.cs` | Modify | Append 7 new [Fact] tests T_B66_01 through T_B66_07 |

**Files NOT changed** (verified):
- `src/PropTraderTools/Features/PttQuickExit.cs` -- call site at line 52 unchanged
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- delegates to PttQuickExit.Execute, no direct call

---

## C. Change Specification (Precise)

### C.1 -- CopyEngine.cs: Add `IsAtmBracketName` and `IsQxCancelCandidate`

**Insert point**: Immediately before line 422 (before the `CancelQxBrackets` method).
Insert `IsAtmBracketName` first, then `IsQxCancelCandidate` immediately after.

**Method signatures**:
```csharp
internal static bool IsAtmBracketName(string name)
internal static bool IsQxCancelCandidate(Order o)
```

**Complete logic** (all branches explicit):

```csharp
// IsAtmBracketName: true if name is a standard NT8 ATM bracket order name.
// NT8-REF line 1631: ATM bracket order names are "Stop1","Stop2","Target1","Target2".
// CYC=1: expression body -- no if-branches in method body (Roslyn convention).
// JS-021: no lock. JS-001: no throw. ASCII-only string literals.
internal static bool IsAtmBracketName(string name) =>
    name == "Stop1" || name == "Stop2" || name == "Target1" || name == "Target2";

// IsQxCancelCandidate: returns true if order should be cancelled by CancelQxBrackets.
// Covers: ATM bracket names (via IsAtmBracketName), PTT-QX-* prefix, PTT-BE-* prefix.
// CYC=5: null guard(1) + IsAtmBracketName(2) + PTT-QX- prefix(3) + PTT-BE- prefix(4).
// Roslyn convention: 4 if-statements = 4 decisions. CYC = 1 (base) + 4 = 5.
// JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). ASCII-only.
internal static bool IsQxCancelCandidate(Order o)
{
    if (o == null || o.Name == null) return false;                                       // (1) null guard
    if (IsAtmBracketName(o.Name)) return true;                                           // (2) ATM bracket names
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;            // (3) QX prefix
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;            // (4) BE prefix
    return false;
}
```

### C.2 -- CopyEngine.cs: Modify `CancelQxBrackets` line 436

**Before** (current source, line 436):
```csharp
if (o.Name != null && o.Name.StartsWith("PTT-QX-"))  // (4)
    stale.Add(o);
```

**After**:
```csharp
if (IsQxCancelCandidate(o))                           // (5) widened via helper
    stale.Add(o);
```

Note: The `o.Name != null` guard moves into `IsQxCancelCandidate` branch (1). The
`StringComparison.Ordinal` parameter is now explicit inside the helper. No other lines in
`CancelQxBrackets` are changed.

**Updated method comment** (replace existing CYC comment):
```csharp
// CancelQxBrackets: cancel all Working/Initialized/Accepted ATM-bracket + PTT-* orders on acc for instr.
// Called by PttQuickExit.Execute() before re-placing new bracket.
// CYC=6: null guard(1) + foreach(2) + stateOk(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
// JS-021: no lock. Predicate logic in IsQxCancelCandidate (CYC=5) + IsAtmBracketName (CYC=1), both internal static.
```

### C.3 -- CopyEngineTests.cs: Append 7 [Fact] tests

**Insert point**: Before the final `}` closing the test class (after T_B63_04, line 3287).

All 7 tests use the existing `MakeOrder(OrderState, string)` helper (line 3133).
All tests call `CopyEngine.IsQxCancelCandidate(order)` directly (internal + same assembly).

---

## D. CYC Accounting (Explicit Branch-by-Branch)

### Governing Convention

**Tool**: Roslyn analyzer (same tool used by `scripts/complexity_audit.py` and Codacy in this project).
**Rule**: One decision point per `if`, `for`, `while`, `case`, or ternary `?:` in the **method body**.
An expression-bodied method (`=>`) has no control-flow branches in the method body -- CYC = 1 (base only).
The `||` operator inside an expression-bodied method does **not** add to method CYC under Roslyn/Lizard convention.

---

### `IsAtmBracketName(string name)` -- CYC = 1

Expression-bodied method (`=>`). No `if`/`for`/`while`/`case`/`?:` in the method body.
The `||` operators are inside the expression, not control-flow branches.

**CYC = 1 (base only). No decision points. Compliant with JS-066 (<= 8). ✓**

---

### `IsQxCancelCandidate(Order o)` -- CYC = 5

| Branch # | Code | Decision |
|----------|------|----------|
| 1 | `if (o == null \|\| o.Name == null) return false;` | null guard -- exits early |
| 2 | `if (IsAtmBracketName(o.Name)) return true;` | ATM bracket names (delegates to helper) |
| 3 | `if (o.Name.StartsWith("PTT-QX-", ...)) return true;` | QX prefix match |
| 4 | `if (o.Name.StartsWith("PTT-BE-", ...)) return true;` | BE prefix match |
| -- | `return false;` | Default -- no extra branch |

**CYC = 1 (base) + 4 (decisions: one per `if`) = 5. Compliant with JS-066 (<= 8). ✓**

*Note: The `||` inside branch (1) is a compound condition within a single `if` statement. Under
Roslyn/Lizard convention it counts as one decision point (the `if`), not two. CYC is therefore 5,
not 6.*

---

### `CancelQxBrackets(Account acc, Instrument instr)` -- CYC = 6 (unchanged by this change)

| Branch # | Line | Code | Decision |
|----------|------|------|----------|
| 1 | 427 | `if (acc == null \|\| instr == null) return;` | null guard |
| 2 | 429 | `foreach (Order o in acc.Orders)` | loop iteration |
| 3 | 434 | `if (!stateOk) continue;` | state filter |
| 4 | 435 | `if (o.Instrument == null \|\| o.Instrument.FullName != instr.FullName) continue;` | instrument match |
| 5 | 436 | `if (IsQxCancelCandidate(o))` | candidate predicate (replaces old StartsWith) |
| 6 | 439 | `if (stale.Count == 0) return;` | empty-list short-circuit |

**CYC = 1 (base) + 6 = 6 (before and after change). Not increased. Compliant (<= 8).**

*Note: The existing code comment on line 424 states "CYC=4" -- that comment undercounts by
missing branches 4 (instrument check) and 6 (stale.Count check). The corrected count is 6.
This plan corrects the comment; no architectural impact.*

---

## E. JS-DNA Compliance

| Rule | Requirement | Status in B66 New Code |
|------|-------------|------------------------|
| JS-021 | No `lock()` anywhere | PASS -- `IsQxCancelCandidate` is pure predicate; `CancelQxBrackets` unchanged |
| JS-001 | No `throw new XxxException` in hot path | PASS -- no throw in either method |
| JS-002 | No `return null` for missing values | PASS -- returns `bool` (true/false only) |
| JS-033 | No `async void` (non-event-handler) | PASS -- both methods are synchronous |
| JS-066 | CYC <= 8 per method | PASS -- IsAtmBracketName=1, IsQxCancelCandidate=5, CancelQxBrackets=6. All <= 8. |
| ASCII-only | All string literals ASCII 0x20-0x7E | PASS -- "Stop1","Stop2","Target1","Target2","PTT-QX-","PTT-BE-" all ASCII |
| No DateTime.Now | Use DateTime.UtcNow or none | PASS -- no datetime usage in new code |
| No FontFamily | No WPF font manipulation | PASS -- not applicable |

---

## F. NT8 Verification Points

### ATM Bracket Order Names (NT8_FULL_REFERENCE.md)

**Citation**: `docs/standards/NT8_FULL_REFERENCE.md` **line 1631**:
> "The order name such as 'Stop1' or 'Target2'"

This confirms NT8 ATM bracket orders use the exact names `"Stop1"`, `"Stop2"`, `"Target1"`,
`"Target2"`. No prefix pattern applies -- exact string matching is required.

The fix covers all 4 standard names with explicit equality checks (branches 2-5 in
`IsQxCancelCandidate`).

### Call Site Verification

`CancelQxBrackets` has exactly ONE call site:
- `src/PropTraderTools/Features/PttQuickExit.cs` **line 52**:
  ```csharp
  CopyEngine.Instance?.CancelQxBrackets(leader, instr);
  ```

`PttGlobalQuickExit.Execute()` iterates accounts and calls `ExecuteOne()` which delegates to
`new PttQuickExit().Execute(acc, instr, t1, t2)` -- the same PttQuickExit path. No second
`CancelQxBrackets` call site exists.

---

## G. Test Specification (7 Tests)

All tests are in `src/PropTraderTools/CopyEngineTests.cs`, appended before the final closing
brace of the test class. All use `[Fact]` (xUnit -- never NUnit/MSTest). All call
`CopyEngine.IsQxCancelCandidate(order)` directly (same assembly, internal visibility).

The `MakeOrder(OrderState, string)` helper at line 3133 is reused without modification.

---

### T_B66_01

```csharp
[Fact]
public void T_B66_01_IsQxCancelCandidate_PttQxStop01_ReturnsTrue()
{
    var order = MakeOrder(OrderState.Working, "PTT-QX-Stop01");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.True(result, "IsQxCancelCandidate: 'PTT-QX-Stop01' must return true (PTT-QX- prefix)");
}
```

**Asserts**: PTT-QX- prefix match (branch 6) still works after refactor.

---

### T_B66_02

```csharp
[Fact]
public void T_B66_02_IsQxCancelCandidate_Stop1_ReturnsTrue()
{
    var order = MakeOrder(OrderState.Working, "Stop1");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.True(result, "IsQxCancelCandidate: 'Stop1' must return true (ATM bracket name)");
}
```

**Asserts**: ATM bracket name "Stop1" is matched (branch 2). THE core fix.

---

### T_B66_03

```csharp
[Fact]
public void T_B66_03_IsQxCancelCandidate_Stop2_ReturnsTrue()
{
    var order = MakeOrder(OrderState.Working, "Stop2");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.True(result, "IsQxCancelCandidate: 'Stop2' must return true (ATM bracket name)");
}
```

**Asserts**: ATM bracket name "Stop2" is matched (branch 3).

---

### T_B66_04

```csharp
[Fact]
public void T_B66_04_IsQxCancelCandidate_Target1_ReturnsTrue()
{
    var order = MakeOrder(OrderState.Working, "Target1");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.True(result, "IsQxCancelCandidate: 'Target1' must return true (ATM bracket name)");
}
```

**Asserts**: ATM bracket name "Target1" is matched (branch 4).

---

### T_B66_05

```csharp
[Fact]
public void T_B66_05_IsQxCancelCandidate_Target2_ReturnsTrue()
{
    var order = MakeOrder(OrderState.Working, "Target2");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.True(result, "IsQxCancelCandidate: 'Target2' must return true (ATM bracket name)");
}
```

**Asserts**: ATM bracket name "Target2" is matched (branch 5).

---

### T_B66_06

```csharp
[Fact]
public void T_B66_06_IsQxCancelCandidate_PttBeStop_ReturnsTrue()
{
    var order = MakeOrder(OrderState.Working, "PTT-BE-Stop");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.True(result, "IsQxCancelCandidate: 'PTT-BE-Stop' must return true (PTT-BE- prefix)");
}
```

**Asserts**: PTT-BE-Stop is matched by PTT-BE- prefix (branch 7). Validates the PTT-BE cancellation path.

---

### T_B66_07

```csharp
[Fact]
public void T_B66_07_IsQxCancelCandidate_SomeOtherOrder_ReturnsFalse()
{
    var order = MakeOrder(OrderState.Working, "SomeOtherOrder");
    var result = CopyEngine.IsQxCancelCandidate(order);
    Assert.False(result, "IsQxCancelCandidate: 'SomeOtherOrder' must return false (no match)");
}
```

**Asserts**: Non-matching name returns false (default path). Guards against over-broad matching.

---

## H. Deferred Backlog Carry-Forward

### NEW This Block

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B66-01 | CancelQxBrackets misses ATM bracket names (Stop1/Stop2/Target1/Target2) | P0 | **CLOSED this block** |
| DW-B66-BE-01 | CancelQxBrackets now cancels PTT-BE-Stop during Quick Exit -- confirm intentional | P1 | **NEW -- OPEN** |

**DW-B66-BE-01 Detail**: The widened predicate (branch 7, `StartsWith("PTT-BE-")`) means that
pressing Quick Exit will now cancel any live `PTT-BE-Stop`, `PTT-BE-Stop-{i+1}`, or
`PTT-BE-Target-{i+1}` orders on the account for the instrument. This ensures a clean position exit
but removes breakeven stop protection. **Requires Director confirmation that this is the intended
behavior.** If not intended, branch 7 should be removed and only the ATM bracket names (branches
2-5) + PTT-QX- prefix (branch 6) should be retained.

---

### Carry-Forward OPEN Items (from B65-LaneA/06-deferred-backlog.md)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B64-01 | B62 drag sync -- HandleEntryChange not firing | P0 | B66+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B66+ | OPEN |
| DW-B54-01 | ATM auto-inject (blocked -- StrategyBase required) | P1 | future (blocked) | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines 1401-1402 | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Note**: DW-B64-01 (P0 -- HandleEntryChange not firing) and DW-B63-01 (P1 -- spurious PTT-Copy
brackets) are carried forward without action in B66. B66 is scoped exclusively to the Quick Exit
bracket cancellation fix (DW-B66-01).

---

## I. Summary

| Aspect | Value |
|--------|-------|
| Files changed | 2 (CopyEngine.cs, CopyEngineTests.cs) |
| New methods | 2 (IsAtmBracketName -- expression body, CYC=1; IsQxCancelCandidate -- internal static bool, CYC=5) |
| Modified methods | 1 (CancelQxBrackets -- line 436 only, CYC unchanged at 6) |
| New tests | 7 ([Fact] T_B66_01 through T_B66_07) |
| P0 violations | 0 |
| Lock usage | 0 |
| Async void | 0 |
| Non-ASCII literals | 0 |
| NT8 APIs added | 0 (existing acc.Orders / Order.Name only) |
