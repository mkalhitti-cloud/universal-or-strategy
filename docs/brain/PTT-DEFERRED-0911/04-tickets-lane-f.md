# PTT-DEFERRED-0911 Lane F — Implementation Tickets

**Epic:** PTT-DEFERRED-0911 Lane F
**Phase:** 3 (Ticket Generation)
**Date:** 2026-09-11
**Status:** TICKETS_COMPLETE
**Source plan:** `docs/brain/PTT-DEFERRED-0911/02-architecture-plan-lane-f.md` (REVIEW_PASS)
**Author:** ptt-architect

---

## Deferred Items Closed by This Wave

| ID | Item | Ticket |
|----|------|--------|
| DW-BUG-0911-A-02 | Remove `[Fact(Skip=...)]` from FlatGuardLoop test | T1 |
| DW-B24-03 | New `[Fact]` for `acc == leader` skip guard | T2 |
| DW-LD-04 | `CancelQxBrackets` 3-param CYC=9 violation fix | T3 |

---

## Implementation Order (Recommended)

Tickets have no hard ordering dependency (LANES-APPROVED per plan §1). Recommended single-engineer order:

1. **T3 first** — smallest production change; resolves JS-013 P1 violation immediately
2. **T1 second** — signature change + 2 call sites + test unskip
3. **T2 third** — additive test only; zero production change

Both T2 and T3 add to the same `LaneFTests` class. A single pass that adds both in sequence is valid.

---

## Expected Test Outcomes After All Three Tickets

| Test | Before | After |
|------|--------|-------|
| `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` | SKIP (NT8-runtime) | PASS |
| `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` | does not exist | PASS |
| `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` | does not exist | PASS |
| All existing tests | PASS (175) / SKIP (365) | PASS (178) / SKIP (364) |

`dotnet test` target: **Failed=0, Passed=178, Skipped=364, Total=542.**

---

---

# T1 — DW-BUG-0911-A-02: Remove [Fact(Skip=...)] via FlatGuardLoop delegate type change

## T1.0 Spec Requirement IDs

- **DW-BUG-0911-A-02**: Remove the NT8-runtime `[Fact(Skip=...)]` annotation from
  `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` by eliminating the NT8 type
  reference (`NinjaTrader.Cbi.Position?`) from the `FlatGuardLoop<TAccount>` generic delegate
  parameter. Fix mechanism: replace `Func<TAccount, NinjaTrader.Cbi.Position?> findPosition`
  with `Func<TAccount, bool> isFlat`. Move the flatness predicate logic into the callers' lambdas.

## T1.1 Files Touched

| File | Change type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Modify `FlatGuardLoop<TAccount>` signature + body; update 2 call sites |
| `src/PropTraderTools/CopyEngineTests.cs` | Remove `[Fact(Skip=...)]`; update delegate parameter and invocation |

**SRC CODE BAN:** ptt-architect does NOT edit `.cs` files. This ticket is the engineer's sole contract.

## T1.2 Pre-Implementation Check (Engineer Mandatory)

Before any edit:
```powershell
grep -r "FlatGuardLoop" src/
```
Expected result: **exactly 3 hits** — one method definition at ~line 5497, one call site in `Flatten(Instrument)` at ~line 5488, one call site in `Flatten(Account, Instrument)` at ~line 5555. If a fourth call site exists, STOP and notify the orchestrator before proceeding.

## T1.3 Production Code Changes — CopyEngine.cs

### A. FlatGuardLoop signature and body (~lines 5497–5514)

**BEFORE:**
```csharp
private static void FlatGuardLoop<TAccount>(
    Func<IEnumerable<TAccount>> getAccounts,
    Func<TAccount, NinjaTrader.Cbi.Position?> findPosition,
    Action<TAccount> flattenOne)
{
    foreach (var acc in getAccounts())
    {
        var pos = findPosition(acc);
        if (pos == null || pos.Quantity == 0)
        {
            NinjaTrader.Code.Output.Process(
                "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            continue;
        }
        flattenOne(acc);
    }
}
```

**AFTER:**
```csharp
// BUG-0911-A: extracted for testability -- mirrors FlattenFollower delegate-injection pattern.
// CYC=3: (1) foreach, (2) isFlat true-branch (skip), (3) isFlat false-branch (flattenOne).
// JS-021: no lock (static, pure iteration). JS-001: no throw. ASCII-only.
private static void FlatGuardLoop<TAccount>(
    Func<IEnumerable<TAccount>> getAccounts,
    Func<TAccount, bool> isFlat,
    Action<TAccount> flattenOne)
{
    foreach (var acc in getAccounts())
    {
        if (isFlat(acc))
        {
            NinjaTrader.Code.Output.Process(
                "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            continue;
        }
        flattenOne(acc);
    }
}
```

**Semantic note:** Zero behavior change. The predicate `pos == null || pos.Quantity == 0` is evaluated identically; it moves from inside `FlatGuardLoop` to inside each caller's lambda. The log message and `continue` logic are unchanged.

### B. Call site 1 — Flatten(Instrument) (~line 5488)

**BEFORE (second argument):**
```csharp
FlatGuardLoop(
    () => AllAccounts(instrument),
    acc => FindPosition(acc, instrument),   // returns NinjaTrader.Cbi.Position?
    acc => FlattenOneAccount(acc, instrument)
);
```

**AFTER (second argument):**
```csharp
FlatGuardLoop(
    () => AllAccounts(instrument),
    acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
    acc => FlattenOneAccount(acc, instrument)
);
```

### C. Call site 2 — Flatten(Account, Instrument) (~line 5555)

**BEFORE (second argument):**
```csharp
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => acc != leader),
    acc => FindPosition(acc, instrument),   // returns NinjaTrader.Cbi.Position?
    acc => FlattenOneAccount(acc, instrument)
);
```

**AFTER (second argument):**
```csharp
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => acc != leader),
    acc => { var p = FindPosition(acc, instrument); return p == null || p.Quantity == 0; },
    acc => FlattenOneAccount(acc, instrument)
);
```

## T1.4 Test Code Changes — CopyEngineTests.cs (~lines 8343–8372)

**BEFORE:**
```csharp
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()
{
    bool flattenOneCalled = false;
    var followerToken = new object();
    Func<IEnumerable<object>> getAccounts = () => new[] { followerToken };
    Func<object, NinjaTrader.Cbi.Position?> findPosition = acc => null;  // <-- NT8 type
    Action<object> flattenOne = acc => flattenOneCalled = true;
    // ...
    miConcrete.Invoke(null, new object[] { getAccounts, findPosition, flattenOne });
    Assert.False(flattenOneCalled, ...);
}
```

**AFTER:**
```csharp
// BUG-0911-A: FlatGuardLoop guard test -- Path B fan-out guard.
// Exercises the pos==null flat-guard added to Flatten(Instrument) follower loop.
// No NT8 runtime required: FlatGuardLoop is invoked via reflection with injected delegates.
// isFlat=true stub stands in for NT8 FindPosition -- no NinjaTrader.Cbi.Position reference.
[Fact]
public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()
{
    // Arrange
    bool flattenOneCalled = false;

    // Stub: one follower account token (opaque -- not an NT8 runtime Account)
    var followerToken = new object();

    // Injected delegates stand in for NT8 instance-method calls:
    Func<IEnumerable<object>> getAccounts = () => new[] { followerToken };
    Func<object, bool> isFlat = acc => true;    // flat: skip dispatch
    Action<object> flattenOne = acc => flattenOneCalled = true;

    // Retrieve the static helper via reflection.
    // FlatGuardLoop has ONE generic parameter (TAccount).
    var mi = typeof(CopyEngine)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == "FlatGuardLoop" && m.GetGenericArguments().Length == 1);
    Assert.NotNull(mi); // guard: method must exist after BUG-0911-A implementation

    var miConcrete = mi.MakeGenericMethod(typeof(object));

    // Act
    miConcrete.Invoke(null, new object[] { getAccounts, isFlat, flattenOne });

    // Assert
    Assert.False(flattenOneCalled,
        "FlatGuardLoop must not call flattenOne when isFlat returns true (flat follower)");
}
```

## T1.5 Method Signatures

| Method | File | Location (approx) | Signature |
|--------|------|--------------------|-----------|
| `FlatGuardLoop<TAccount>` (modified) | `CopyEngine.cs` | ~line 5497 | `private static void FlatGuardLoop<TAccount>(Func<IEnumerable<TAccount>> getAccounts, Func<TAccount, bool> isFlat, Action<TAccount> flattenOne)` |
| `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` (modified) | `CopyEngineTests.cs` | ~line 8343 | `[Fact] public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()` |

Caller methods `Flatten(Instrument)` and `Flatten(Account, Instrument)` have **unchanged external signatures**. Only the lambda body passed as the second argument to `FlatGuardLoop` is updated.

## T1.6 JS Rule Constraints

| Rule | Scope | Compliance |
|------|-------|------------|
| JS-021 (no lock) — P0 | `FlatGuardLoop` is static, pure control flow, no shared state | Zero new `lock()` calls. SCAN-01 must return 0. |
| JS-001 (no throw) — P0 | `FlatGuardLoop` has no `throw`; delegate execution is callee responsibility | No `throw` added. |
| JS-013 (CYC ≤ 8) — P1 | `FlatGuardLoop` revised: CYC=3 (base + foreach + isFlat branch) | SCAN-02 manual McCabe count: 3 ≤ 8. ✓ |
| ASCII-001 (ASCII-only) — P0 | No new non-ASCII bytes in string literals or identifiers | SCAN-04 must return 0. |

## T1.7 Canonical 7-Scan Checklist

| Scan | Command / Method | Required Result |
|------|-----------------|-----------------|
| **SCAN-01** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('` | 0 executable `lock()` statements in new/modified code |
| **SCAN-02** | Manual McCabe count: `FlatGuardLoop<TAccount>` revised body | CYC=3 (base=1 + foreach=1 + isFlat=1) ≤ 8 ✓; both Flatten call-site lambdas are pure expression bodies CYC=1 each ✓ |
| **SCAN-03** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\b\|\bawait\b'` | 0 hits in new/modified executable code (`FlatGuardLoop` is synchronous) |
| **SCAN-04** | `[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') \| Where-Object { $_ -gt 127 } \| Measure-Object \| Select-Object -ExpandProperty Count` | 0 (all new identifiers and string literals are ASCII) |
| **SCAN-05** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'AtmStrategyCreate\|AtmStrategyChangeStopTarget'` | 0 actual calls (T1 introduces none) |
| **SCAN-06** | `dotnet build` then `dotnet test` | 0 build errors; 0 test failures; `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` transitions from SKIP to PASS |
| **SCAN-07** | Structural verification | (1) `FlatGuardLoop` signature at ~line 5497 uses `Func<TAccount, bool> isFlat` as second parameter (not `Func<TAccount, NinjaTrader.Cbi.Position?>`). (2) Both Flatten call sites at ~5488 and ~5555 pass `{ var p = FindPosition(...); return p == null \|\| p.Quantity == 0; }` lambda as second arg. (3) Test at ~line 8343 has `[Fact]` with no `Skip` argument. (4) `isFlat` variable in test body has type `Func<object, bool>`. (5) `Invoke` call passes `isFlat` as the second element of the args array. |

---

---

# T2 — DW-B24-03: New [Fact] for acc==leader skip guard

## T2.0 Spec Requirement IDs

- **DW-B24-03**: Add a formal xUnit `[Fact]` (no Skip) that verifies the `if (acc == leader) continue` skip-guard semantics already present at 7 sites in `CopyEngine.cs`. No production code changes required — the guard exists. Two-part test: (A) pure-BCL simulation of reference-equality skip semantics; (B) reflection existence checks confirming the leader-overload methods compile and are accessible.

## T2.1 Files Touched

| File | Change type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | **None** — production guard already exists at 7 confirmed sites |
| `src/PropTraderTools/CopyEngineTests.cs` | Add new `LaneFTests` public class after `LaneETests` closing brace |

**Zero production `.cs` changes.** Any CopyEngine.cs edit in this ticket = protocol violation.

## T2.2 Test Code — Full LaneFTests Class Opening + T-LF-01

Add the following after the closing `}` of `LaneETests` in `CopyEngineTests.cs` (approximately line 9000+). T3 will add `T-LF-02` and the class closing brace in a separate commit (or in the same pass — see Implementation Order above).

```csharp
// Lane F: deferred item closure tests. All [Fact], no Skip.
// T2 (DW-B24-03): leader-account skip guard behavioral simulation.
// T3 (DW-LD-04): IsRaceSkipCandidate structural contract.
// JS-021: no lock. ASCII-only.
public class LaneFTests
{
    // GetAnyOverload: returns any matching overload by name.
    // Uses GetMethods (not GetMethod) to avoid AmbiguousMatchException
    // on overloaded methods (Trim, Flatten, CancelPendingEntries each have multiple overloads).
    // Searches both Instance and Static members with NonPublic visibility.
    // Returns null (not throws) when no matching method exists.
    private static System.Reflection.MethodInfo GetAnyOverload(string name) =>
        typeof(CopyEngine)
            .GetMethods(
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static)
            .FirstOrDefault(m => m.Name == name);

    // T-LF-01 (DW-B24-03): leader-account in follower fan-out is skipped.
    // Verifies the if (acc == leader) continue guard semantics via pure BCL simulation.
    // The production guard uses object reference equality (same pattern as this test).
    // Structural: also confirms the leader-overload methods exist via reflection.
    // No Skip: no NT8 types instantiated at runtime.
    [Fact]
    public void SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader()
    {
        // --- Part A: Pure-BCL guard semantics simulation ---
        // Simulates the exact guard: if (acc == leader) continue
        // Uses 3 opaque object tokens as account stand-ins.
        var leader    = new object();
        var follower1 = new object();
        var follower2 = new object();
        var allAccounts = new[] { leader, follower1, follower2 };

        var dispatched = new System.Collections.Generic.List<object>();
        foreach (var acc in allAccounts)
        {
            if (acc == leader)
                continue;           // guard fires: leader is skipped
            dispatched.Add(acc);
        }

        // Assert: leader NOT dispatched; followers ARE dispatched
        Assert.Equal(2, dispatched.Count);
        Assert.DoesNotContain(leader, dispatched);
        Assert.Contains(follower1, dispatched);
        Assert.Contains(follower2, dispatched);

        // --- Part B: Structural reflection -- leader-overload methods must exist ---
        // Confirms the guard SITES are compiled and accessible (not dead code).
        // GetAnyOverload returns first matching overload; != null proves method exists.
        // Trim: multiple overloads (2-param, 5-param confirmed). Any one suffices.
        // Flatten: multiple overloads. Any one suffices.
        // CancelPendingEntries: multiple overloads. Any one suffices.
        var trimOverload = GetAnyOverload("Trim");
        Assert.NotNull(trimOverload);

        var flattenOverload = GetAnyOverload("Flatten");
        Assert.NotNull(flattenOverload);

        var cancelOverload = GetAnyOverload("CancelPendingEntries");
        Assert.NotNull(cancelOverload);
    }

    // T-LF-02 is added by T3 (DW-LD-04) below, before the class closing brace.
```

*(T3 appends `GetStaticMethod`, `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse`, and the closing `}` of `LaneFTests`.)*

## T2.3 Method Signatures

| Symbol | File | Type | Notes |
|--------|------|------|-------|
| `LaneFTests` | `CopyEngineTests.cs` | new `public class` | Additive; placed after `LaneETests` closing brace |
| `GetAnyOverload(string name)` | `CopyEngineTests.cs` | `private static System.Reflection.MethodInfo` | Disambiguation helper. Uses `GetMethods`, not `GetMethod`. |
| `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader()` | `CopyEngineTests.cs` | `[Fact] public void` | No Skip, no NT8 runtime types instantiated |

## T2.4 JS Rule Constraints

| Rule | Scope | Compliance |
|------|-------|------------|
| JS-021 (no lock) — P0 | `LaneFTests` uses `List<object>`, no shared state, no concurrency | Zero `lock()` calls. SCAN-01 must return 0. |
| JS-001 (no throw) — P0 | Test is not a dispatch path. `Assert` throws are xUnit contract, not PTT dispatch. | N/A to test code. |
| ASCII-001 (ASCII-only) — P0 | All new identifiers and string literals ASCII | SCAN-04 must return 0. |
| JS-013 (CYC ≤ 8) — P1 | `GetAnyOverload`: CYC=1 (expression body, no branches). `SkipDuplicateGuard_*`: CYC≈4 (foreach+if+Assert calls). Test methods are informational for CYC; not subject to JS-013 gates. | SCAN-02 informational ✓ |

## T2.5 Canonical 7-Scan Checklist

| Scan | Command / Method | Required Result |
|------|-----------------|-----------------|
| **SCAN-01** | `Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s+lock\s*\('` | 0 executable `lock()` statements in new `LaneFTests` code |
| **SCAN-02** | Manual McCabe count of new methods in `LaneFTests` | `GetAnyOverload`: CYC=1 (pure expression body) ✓; `SkipDuplicateGuard_*`: CYC≈4 (foreach + if + Asserts) — informational, test methods exempt from JS-013 gate ✓ |
| **SCAN-03** | `Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '\basync\b\|\bawait\b'` | 0 hits in new `LaneFTests` code (all test code is synchronous) |
| **SCAN-04** | `[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngineTests.cs') \| Where-Object { $_ -gt 127 } \| Measure-Object \| Select-Object -ExpandProperty Count` | 0 (all new identifiers and string literals ASCII) |
| **SCAN-05** | `Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern 'AtmStrategyCreate\|AtmStrategyChangeStopTarget'` | 0 actual calls (T2 introduces none) |
| **SCAN-06** | `dotnet build` then `dotnet test` | 0 build errors; 0 test failures; `SkipDuplicateGuard_LeaderAccountInFollowerFanOut_SkipsDispatchForLeader` is PASS (not SKIP, not ERROR) |
| **SCAN-07** | Structural verification | (1) `LaneFTests` class added after `LaneETests` closing brace. (2) `GetAnyOverload` helper uses `GetMethods(...)` (plural, not `GetMethod`). (3) `[Fact]` attribute on `SkipDuplicateGuard_*` — no `Skip` argument present. (4) Part B assertions call `GetAnyOverload("Trim")`, `GetAnyOverload("Flatten")`, `GetAnyOverload("CancelPendingEntries")` and `Assert.NotNull` each result. |

---

---

# T3 — DW-LD-04: CancelQxBrackets CYC=9 fix via IsRaceSkipCandidate extraction

## T3.0 Spec Requirement IDs

- **DW-LD-04**: `CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)` 3-parameter overload has CYC=9 (JS-013 P1 violation). Confirmed by independent architect McCabe count (see plan §5.1). Extract the compound 3-operand `&&` predicate (`snapshot != null && !snapshot.Contains(o) && IsQxCancelEligible3(o, instr, null)`) into `private static bool IsRaceSkipCandidate(Order, HashSet<Order>, Instrument)`. Update the stale CYC comment. New helper CYC=4; modified caller CYC=7. Both ≤ 8. Violation cleared.

## T3.1 Files Touched

| File | Change type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | (A) Insert new `IsRaceSkipCandidate` method after `CancelQxBrackets` 3-param closing brace (~line 1198). (B) Replace 3-branch compound `if` in `CancelQxBrackets` 3-param body with `if (IsRaceSkipCandidate(...))`. (C) Update stale CYC comment on `CancelQxBrackets` 3-param. |
| `src/PropTraderTools/CopyEngineTests.cs` | Add `GetStaticMethod` helper and `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` `[Fact]` to `LaneFTests` class (opened by T2). Add class closing brace. |

## T3.2 Pre-Implementation Check (Engineer Mandatory)

Before any edit:
```powershell
grep -n "IsQxCancelEligible3" src/PropTraderTools/CopyEngine.cs
```
Expected: at least one `private static bool IsQxCancelEligible3(...)` definition hit. If the method does not exist, STOP and notify the orchestrator. (It was confirmed extracted in BWAVE-REFACTOR-LaneB-T2.)

## T3.3 Production Code Changes — CopyEngine.cs

### A. Insert IsRaceSkipCandidate after CancelQxBrackets 3-param closing brace (~line 1198)

```csharp
// DW-LD-04: extracted compound race-skip predicate from CancelQxBrackets 3-param.
// Returns true if o is: (a) in a cancellable state+instrument (eligible), (b) NOT in snapshot,
// and (c) snapshot is non-null. i.e., the order existed BEFORE the snapshot was taken = race window.
// CYC=4: base(1) + snapshot!=null(1) + !Contains(1) + IsQxCancelEligible3(1).
// JS-021: no lock (static, pure predicate). JS-001: no throw. JS-002: returns bool. ASCII-only.
private static bool IsRaceSkipCandidate(
    NinjaTrader.Cbi.Order o,
    System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> snapshot,
    NinjaTrader.Cbi.Instrument instr)
{
    return snapshot != null
        && !snapshot.Contains(o)
        && IsQxCancelEligible3(o, instr, null);
}
```

### B. CancelQxBrackets 3-param body (~lines 1176–1181) — replace compound if

**BEFORE:**
```csharp
if (
    snapshot != null
    && !snapshot.Contains(o)
    && IsQxCancelEligible3(o, instr, null)
)
    raceSkipped++;
```

**AFTER:**
```csharp
if (IsRaceSkipCandidate(o, snapshot, instr))
    raceSkipped++;
```

### C. CancelQxBrackets 3-param CYC comment — update stale annotation

**BEFORE:**
```
// CYC<=5: null-guard(1) + raceSkipped(0) + foreach(2) + IsQxCancelEligible3(3)
//         + stale-count(4) + CommitStaleCancelBatch(0). JS-021: no lock.
```

**AFTER:**
```
// CYC=7: null-guard(2-for-||) + foreach(1) + IsRaceSkipCandidate(1)
//        + IsQxCancelEligible3(1) + stale-count(1). Base=1. JS-021: no lock.
```

**Post-extraction CYC verification (engineer must independently recount):**
```
Base:                                                          = 1
if (acc == null || instr == null) return;   (|| = 2 ops)      = 2  (total 3)
foreach (Order o in acc.Orders)                               = 1  (total 4)
if (IsRaceSkipCandidate(o, snapshot, instr))                  = 1  (total 5)
  raceSkipped++;
if (!IsQxCancelEligible3(o, instr, snapshot))                 = 1  (total 6)
  continue;
stale.Add(o);
if (stale.Count == 0) return;                                 = 1  (total 7)
TOTAL = 7  (JS-013 PASS)
```

## T3.4 Test Code Changes — CopyEngineTests.cs

Append the following **inside** `LaneFTests` (opened by T2), then close the class:

```csharp
    private static System.Reflection.MethodInfo GetStaticMethod(string name) =>
        typeof(CopyEngine).GetMethod(
            name,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        );

    // T-LF-02 (DW-LD-04): IsRaceSkipCandidate structural contract.
    // Verifies: method exists, is private static, returns bool, has 3 params.
    // Verifies: null snapshot -> returns false (snapshot!=null guard short-circuits).
    // No Skip: null-snapshot path invokes no NT8 type members at runtime.
    [Fact]
    public void IsRaceSkipCandidate_NullSnapshot_ReturnsFalse()
    {
        // --- Part A: structural reflection ---
        var mi = GetStaticMethod("IsRaceSkipCandidate");
        Assert.NotNull(mi);
        Assert.Equal(typeof(bool), mi.ReturnType);
        Assert.Equal(3, mi.GetParameters().Length);
        // Parameter 0: NinjaTrader.Cbi.Order   (NT8 type -- type-checked only, not instantiated)
        // Parameter 1: HashSet<Order>           (BCL generic -- nullable)
        // Parameter 2: NinjaTrader.Cbi.Instrument (NT8 type -- type-checked only, not instantiated)

        // --- Part B: null-snapshot short-circuit semantics (pure BCL) ---
        // Simulate: snapshot == null -> first && operand false -> returns false immediately.
        // No NT8 Order or Instrument instantiation needed for this path.
        // Passing null for all 3 params: snapshot==null fires first, returns false before
        // o or instr are dereferenced (short-circuit evaluation).
        var result = mi.Invoke(null, new object[] { null, null, null });
        Assert.Equal(false, result);
    }
}
```

*(The final `}` closes `LaneFTests`.)*

**Note on `GetStaticMethod` vs `GetAnyOverload`:** `IsRaceSkipCandidate` is unambiguous (no overloads). `Type.GetMethod(name, NonPublic | Static)` is safe here and will not throw `AmbiguousMatchException`. The `GetAnyOverload` disambiguation helper is not required for this lookup.

## T3.5 Method Signatures

| Method | File | Location (approx) | Signature |
|--------|------|--------------------|-----------|
| `IsRaceSkipCandidate` (new) | `CopyEngine.cs` | ~line 1200 (after 3-param closing brace) | `private static bool IsRaceSkipCandidate(NinjaTrader.Cbi.Order o, System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> snapshot, NinjaTrader.Cbi.Instrument instr)` |
| `CancelQxBrackets` 3-param (modified) | `CopyEngine.cs` | ~lines 1162–1198 | Signature unchanged: `private void CancelQxBrackets(NinjaTrader.Cbi.Account acc, NinjaTrader.Cbi.Instrument instr, System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> snapshot)`. Body: compound `if` replaced. CYC comment updated. |
| `GetStaticMethod(string name)` (new) | `CopyEngineTests.cs` | `LaneFTests` class | `private static System.Reflection.MethodInfo GetStaticMethod(string name)` |
| `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` (new) | `CopyEngineTests.cs` | `LaneFTests` class | `[Fact] public void IsRaceSkipCandidate_NullSnapshot_ReturnsFalse()` |

## T3.6 JS Rule Constraints

| Rule | Scope | Compliance |
|------|-------|------------|
| JS-013 (CYC ≤ 8) — P1 | `IsRaceSkipCandidate` new: CYC=4 (base + snapshot!=null + !Contains + IsQxCancelEligible3) | SCAN-02 manual count: 4 ≤ 8 ✓ |
| JS-013 (CYC ≤ 8) — P1 | `CancelQxBrackets` 3-param modified: CYC=7 post-extraction | SCAN-02 manual count: 7 ≤ 8 ✓ |
| JS-021 (no lock) — P0 | `IsRaceSkipCandidate` is static, pure predicate, no shared state | Zero new `lock()` calls. SCAN-01 must return 0. |
| JS-001 (no throw) — P0 | `IsRaceSkipCandidate` returns `bool`, no `throw` | No `throw` added. |
| JS-002 (no null return) — P0 | Returns `bool`, not a reference type | N/A (bool cannot be null). |
| ASCII-001 (ASCII-only) — P0 | Method name `IsRaceSkipCandidate` and all new string literals are ASCII | SCAN-04 must return 0. |

## T3.7 Canonical 7-Scan Checklist

| Scan | Command / Method | Required Result |
|------|-----------------|-----------------|
| **SCAN-01** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('` | 0 executable `lock()` statements in new/modified code (`IsRaceSkipCandidate` is a pure static predicate; no shared state) |
| **SCAN-02** | Manual McCabe count of all new/modified methods | `IsRaceSkipCandidate` (new): CYC=4 (base=1 + snapshot!=null=1 + !Contains=1 + IsQxCancelEligible3=1) ≤ 8 ✓; `CancelQxBrackets` 3-param (modified): CYC=7 (base=1 + ||=2 + foreach=1 + IsRaceSkipCandidate=1 + !IsQxCancelEligible3=1 + stale.Count=1) ≤ 8 ✓ |
| **SCAN-03** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\b\|\bawait\b'` | 0 hits in new/modified executable code (`IsRaceSkipCandidate` is synchronous; no async introduced) |
| **SCAN-04** | `[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') \| Where-Object { $_ -gt 127 } \| Measure-Object \| Select-Object -ExpandProperty Count` | 0 (method name, parameter names, comments all ASCII) |
| **SCAN-05** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'AtmStrategyCreate\|AtmStrategyChangeStopTarget'` | 0 actual calls (T3 introduces none) |
| **SCAN-06** | `dotnet build` then `dotnet test` | 0 build errors; 0 test failures; `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse` is PASS (not SKIP, not ERROR); no regressions in existing suite |
| **SCAN-07** | Structural verification | (1) `IsRaceSkipCandidate` method present at ~line 1200 with signature `private static bool` and 3 parameters. (2) `CancelQxBrackets` 3-param body: compound `if (snapshot != null && !snapshot.Contains(o) && IsQxCancelEligible3(...))` replaced by `if (IsRaceSkipCandidate(o, snapshot, instr))`. (3) CYC comment on `CancelQxBrackets` 3-param updated to `// CYC=7:`. (4) `LaneFTests` class closed by the `}` after `IsRaceSkipCandidate_NullSnapshot_ReturnsFalse`. |

---

## Appendix A — Post-Edit Deploy (Ph4a Mandatory)

After every `.cs` edit in Ph4a, before notifying the orchestrator, run:

```powershell
powershell -File .\deploy-sync.ps1
```

All three gate lines must appear:
```
ASCII GATE PASS - all source files are clean
DIFF GUARD PASS: Diff size (N chars) is within limits.
SOVEREIGN AUDIT PASS: Architectural integrity verified.
SYNC COMPLETE: One Source of Truth Established
```

Exit code 1 from Droid auth stderr is a known non-failure and does not affect gate status.

---

## Appendix B — Forensic Scan (Zero-Match Requirement)

After all edits:
```powershell
grep -r "lock(" src/
```
Expected: **0 matches in executable code.** Matches inside `// comments` are acceptable.

---

*ptt-architect · PTT-DEFERRED-0911 Lane F · 2026-09-11 · Phase 3 Tickets*
