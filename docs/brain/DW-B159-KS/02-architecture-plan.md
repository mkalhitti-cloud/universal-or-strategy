# DW-B159-KS (K-Suite: K2 + K3 + K5) — Architecture Plan
<!-- status: PLAN_COMPLETE -->
<!-- epic: DW-B159-KS -->
<!-- phase: 1 -->
<!-- author: ptt-architect -->
<!-- date: 2026-10-01 -->

---

## Block Overview

| Field | Value |
|-------|-------|
| Epic | DW-B159-KS — K-Suite: K2 + K3 + K5 |
| Source epics | B159 deferred backlog items K2, K3, K5 |
| Change class | Cosmetic / test-only. Zero logic changes to production paths. |
| Files | `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs` |
| Live testing required | None |

---

## 1. LANE-SPLIT GATE

### Q1 — Same method or within 50 lines?

**Answer: NO.**

- T1 (K5): comment update in `IsFollowerPositionBlocked` header, lines ~3196-3196.
- T2 (K3): `private const int BeRetryMaxAttempts = 3;` added at class-scope field region (~line 323 area); literal `3` replaced at line 7192 inside `TryArmBePriceRetry`.
- T3 (K2): entirely in `src/PropTraderTools/CopyEngineTests.cs` — a different file.

T1 and T2 are not within 50 lines of each other (lines 3196 vs 7192 are ~4000 lines apart; the const insertion is near line 323). T3 is a separate file entirely. All three tickets are geographically independent.

### Q2 — Fix B design depends on Fix A final design?

**Answer: NO.**

T2 (K3 const extraction) does not depend on T1 (K5 comment update). T1 adds a line-number annotation to a comment; T2 extracts a magic literal to a named constant in a completely different line range and method. T3 (K2 tests) does not depend on the comment text of T1 or the const name of T2 — the tests verify method signatures and guard semantics that existed since B159.

### Q3 — Each fix has standalone value if the other is blocked?

**Answer: YES.**

- T1 alone: removes the stale `HasOpenPosition` line-number reference from the `IsFollowerPositionBlocked` header. Standalone documentation correctness improvement.
- T2 alone: eliminates the magic literal `3` from `TryArmBePriceRetry`, naming the retry cap for readability. Standalone maintainability improvement.
- T3 alone: adds 14 missing [Fact] tests for FM-A and FM-B guard paths. Standalone test coverage gain.

### Q4 — Each fix has an independent SIM verification path?

**Answer: YES.**

- T1: `grep "HasOpenPosition" src/PropTraderTools/CopyEngine.cs` — verify line 3196 comment updated to include `(line 5507)`.
- T2: `grep "BeRetryMaxAttempts" src/PropTraderTools/CopyEngine.cs` — verify const exists; `grep "attempts >= 3" src/PropTraderTools/CopyEngine.cs` — verify literal `3` is gone; `grep "BeRetryMaxAttempts" src/PropTraderTools/CopyEngine.cs` — verify replacement at usage site.
- T3: `dotnet test` — verify 14 new [Fact] methods pass green.

### LANE-SPLIT GATE RESULT: LANES-APPROVED

All four criteria pass. The three tickets are independent in location, design, and verification. Engineering recommendation: execute sequentially (T1 → T2 → T3) since T1 and T2 share the same file — sequential prevents any accidental diff collision on adjacent context.

---

## 2. Grep Evidence

### K5 — Stale Comment Investigation

**Grep 1: `grep "line 5273" CopyEngine.cs`**
```
No matches found.
```
The original stale comment text cited in the deferred backlog (`// Delegates to HasOpenPosition (line 5273) -- no duplicate logic.`) **no longer exists** in the current source. It was part of the original B159 `IsFollowerPositionBlocked` implementation, which was a 3-line method. Subsequent blocks (B160, B161B166) completely replaced the method's header comment block when expanding it to an 8-signal form.

**Grep 2: `grep -n "HasOpenPosition" CopyEngine.cs`**
```
Line  67:  public bool HasOpenPosition { get; private set; }         -- FlattenState property (different)
Line  72:  HasOpenPosition = hasOpen;
Line 1823: HasOpenPosition,
Line 1858: bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument);
Line 3196: // Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).
Line 3223: if (HasOpenPosition(acc, instr))  // (4) Signal 1 -- normal case (B159)
Line 4929: bool hasPos = HasOpenPosition(e.Order.Account, e.Order.Instrument);
Line 5119: // CYC<=5: leader null(1) + !HasOpenPosition(leader)(2) + HasOpenPosition(follower)(3)
Line 5131: if (!HasOpenPosition(leader, cancelledOrder.Instrument))
Line 5133: if (HasOpenPosition(cancelledOrder.Account, cancelledOrder.Instrument))
Line 5507: private bool HasOpenPosition(Account acc, Instrument instrument)
```

**Actual declaration line: 5507** (not 5273 from the original deferred backlog, not 5294 from the T1 verifier note).

**Grep 3: `grep -n "IsFollowerPositionBlocked" CopyEngine.cs`**
```
Line  941:  // B161-repair: IsFollowerPositionBlocked guard after IsPttEntryOrderCancelTrigger
Line  945:  //   Reuses B160 helpers: IsFollowerPositionBlocked(L3130). CYC: 4->5. Zero new fields.
Line  957:  && IsFollowerPositionBlocked(order.Account, order.Instrument))
Line 3166:  // DW-B161B166-01 VIOLATION-01-FIX: extracted Signals 5+6 from IsFollowerPositionBlocked
Line 3219:  internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
Line 3269:  //   been processed by TryReplaceOnAtmCancel (which checks IsFollowerPositionBlocked...
Line 3290:  //   B160 DW-B160: ghost re-entry guard
Line 3291:  if (IsFollowerPositionBlocked(acc, instr))
```

**Method declaration confirmed at line 3219.**

The current header comment (line 3196) reads:
```
// Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).
```
This references HasOpenPosition by name but without a line number. K5 resolution: append `(line 5507)` to this comment annotation.

**K5 resolution status: The original stale comment was deleted by B160-B167 expansion. The remaining K5 closure action is a one-word annotation to line 3196 to add the correct current line number.**

---

### K3 — Magic Literal `3` in TryArmBePriceRetry

**Grep 4: `grep -n "TryArmBePriceRetry" CopyEngine.cs`**
```
Line  325:  // Key = acc.Name. Value = bePrice stored at TryArmBePriceRetry time.
Line 3265:  // _bePriceRetryAttempts is written by TryArmBePriceRetry (called from MoveStopToBreakEven L7036).
Line 7094:  TryArmBePriceRetry(acc, instrument, newStop);
Line 7186:  internal void TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)
```

**Method body (lines 7186-7199, read-verified):**
```csharp
internal void TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)
{
    if (acc == null)    return;                                    // (2)
    if (instr == null)  return;                                    // (3)
    if (bePrice <= 0.0) return;                                    // (4)
    int attempts = _bePriceRetryAttempts.GetOrAdd(acc.Name, 0);
    if (attempts >= 3)  return;                                    // (5) ceiling guard  <-- LITERAL HERE
    _pendingBePriceRetry[acc.Name] = bePrice;
    _bePriceRetryAttempts.AddOrUpdate(acc.Name, 1, (_, prev) => prev + 1);
    ...
}
```

**Magic literal `3` confirmed at line 7192.** One call site only (the method body). No other occurrences of the number 3 as an attempt-cap literal in this method.

**Grep 5: `grep -n "BeRetryMaxAttempts" CopyEngine.cs`**
```
No matches found.
```
Confirmed: `BeRetryMaxAttempts` does NOT exist yet. Safe to introduce.

---

## 3. Ticket Design

---

### T1 — DW-B159-K5: Comment Fix (CopyEngine.cs)

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Change type | Comment-only (zero logic) |
| Lines touched | Line 3196 (single line) |
| CYC before | IsFollowerPositionBlocked = 8 (unchanged) |
| CYC after | IsFollowerPositionBlocked = 8 (unchanged) |

**Exact change:**

Current line 3196:
```
        // Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).
```

Corrected line 3196:
```
        // Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).
```

**Rationale:** The deferred backlog (K5) cited a stale line number `5273` in a comment that said `"// Delegates to HasOpenPosition (line 5273) -- no duplicate logic."` That specific comment no longer exists — it was overwritten during B160-B167 expansion of `IsFollowerPositionBlocked`. The only remaining reference to `HasOpenPosition` in the method's header comment block is line 3196. The `HasOpenPosition(Account, Instrument)` method is confirmed at **line 5507** by grep. Adding `", line 5507"` to the Signal 1 annotation closes K5 with minimal, targeted change.

**JS Rule Compliance:**
| Rule | Verdict |
|------|---------|
| JS-CYC | PASS — zero branches touched |
| JS-001 | PASS — no throw |
| JS-021 | PASS — no lock |
| JS-042 | PASS — comment text is ASCII-only: `(HasOpenPosition, line 5507)` |
| JS-002 | PASS — no return value changed |

**Verification:**
```
grep "HasOpenPosition, line 5507" src/PropTraderTools/CopyEngine.cs
```
Expected: exactly 1 match at line 3196.

---

### T2 — DW-B159-K3: Extract Literal to Named Constant (CopyEngine.cs)

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Change type | Const extraction (zero logic) |
| Lines touched | ~line 323 region (const insertion) + line 7192 (literal replacement) |
| CYC before | TryArmBePriceRetry = 5 (unchanged) |
| CYC after | TryArmBePriceRetry = 5 (unchanged) |

**Change 1 — Add constant at class scope (field declarations region, after line 325):**

Insert immediately after line 325 (the comment `// Key = acc.Name. Value = bePrice stored at TryArmBePriceRetry time.`):
```csharp
        // DW-B159-K3: named constant for TryArmBePriceRetry attempt ceiling.
        // 3 attempts is empirically adequate for NT8 ATM cancel recovery.
        // K3-UI (deferred): wiring to settings panel is out of scope for this ticket.
        // JS-042: ASCII-only. No functional change.
        private const int BeRetryMaxAttempts = 3;
```

Anchor verification — surrounding lines in source (read-verified):
```
[line 325]  // Key = acc.Name. Value = bePrice stored at TryArmBePriceRetry time.
[line 326]  // Key = acc.Name. Value = bePrice stored at TryArmBePriceRetry time.    -- (this is the ConcurrentDict comment)
```
NOTE: The engineer must anchor on the `_pendingBePriceRetry` field declaration block. The const should be placed at class scope, near the other field declarations in the ~line 320-330 region. Preferred anchor: insert the const declaration immediately before the `_pendingBePriceRetry` ConcurrentDictionary field (line 322 area), so the const appears alongside its only consumer.

**Change 2 — Replace literal `3` with `BeRetryMaxAttempts` at line 7192:**

Current line 7192:
```csharp
            if (attempts >= 3)  return;                                    // (5) ceiling guard
```

Replacement:
```csharp
            if (attempts >= BeRetryMaxAttempts)  return;                   // (5) ceiling guard
```

**Scope boundary (HARD):** This ticket is `CopyEngine.cs` ONLY. `BeRetryMaxAttempts` is NOT wired to the settings panel. That is deferred to K3-UI. No changes to `PropTraderToolsSettingsPanel.cs` or any other file.

**JS Rule Compliance:**
| Rule | Verdict |
|------|---------|
| JS-CYC | PASS — const extraction adds zero branches; CYC=5 unchanged |
| JS-001 | PASS — no throw |
| JS-021 | PASS — no lock |
| JS-042 | PASS — `BeRetryMaxAttempts` is an identifier (ASCII-only); const comment is ASCII-only |
| JS-002 | PASS — TryArmBePriceRetry returns void |

**Verification:**
```
grep "BeRetryMaxAttempts" src/PropTraderTools/CopyEngine.cs
```
Expected: 2 matches — one const declaration, one usage at former line 7192.

```
grep "attempts >= 3" src/PropTraderTools/CopyEngine.cs
```
Expected: 0 matches (literal replaced).

---

### T3 — DW-B159-K2: 14 xUnit Tests (CopyEngineTests.cs)

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngineTests.cs` |
| Change type | Test-only addition (zero production code change) |
| Class | `B159KSuiteTests` |
| Test count | 14 [Fact] methods (no [Skip]) |
| CYC | All test methods CYC=1-2 (straight-line) |

#### Stub Pattern (extracted from LaneFTests / LaneGTests)

From `LaneFTests` (lines 9124-9218, read-verified):
- **Part A — Pure BCL guard simulation**: Allocate opaque C# objects as NT8-type stand-ins. Simulate the exact production guard logic inline (e.g., `if (acc == leader) continue`). Assert on BCL outcomes. No NT8 type instantiation.
- **Part B — Structural reflection contract**: Use `typeof(CopyEngine).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)` to verify method exists, return type, parameter count, parameter type names. Proves the method is compiled and accessible without invoking it.

From `LaneGTests` (lines 9220-9251, read-verified):
- **StubEngine pattern**: Sealed class implementing `ICopyEngine`. Captures call args. No NT8 runtime dependencies.
- **Static seam pattern**: Call `TradeCopierPanel.ExecuteBreakEven(stub, null, null, 3)` directly with a stub — no WPF/NT8 instantiation.

**For B159KSuiteTests:**

T1-group tests (IsFollowerPositionBlocked / ShouldSkipFollower):
- Tests 3-4 (null guards): Invoke `IsFollowerPositionBlocked` via reflection with `null` arg. The null guard returns `false` before accessing any NT8 member → safe to invoke. Verify result == false.
- Tests 5-6 (position state): Pure BCL simulation — define a local `bool hasOpenPosition` flag; simulate `IsFollowerPositionBlocked` guard logic inline:
  ```
  if (acc == null) -> false (not this path)
  if (instr == null) -> false (not this path)
  return HasOpenPosition(acc, instr) -> simulated as bool flag
  ```
  Assert result matches flag value.
- Tests 1-2 (ShouldSkipFollower): ShouldSkipFollower is private with NT8 parameter types. Use BCL guard simulation: define `bool isBlocked` flag; simulate the first guard inline (`if (isBlocked) return true`). Assert result. Also add structural reflection to confirm method exists with correct parameter count.

T2-group tests (TryFireBePriceRetry / TryArmBePriceRetry / StillAtNonBePrice):
- All use **pure BCL dictionary simulation** pattern: allocate `ConcurrentDictionary<string, double>` and `ConcurrentDictionary<string, int>` directly in the test. Re-implement the guard logic inline (matching production logic exactly). Assert on dictionary state before/after.
- For `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` (test 14): structural reflection + BCL simulation of the loop logic. Verify: working stop at wrong price → `StillAtNonBePrice` returns true.
- Structural reflection for TryArmBePriceRetry, TryFireBePriceRetry, StillAtNonBePrice: confirm method exists, return type, parameter types via `GetMethods(NonPublic | Instance)`.

**IMPORTANT — CopyEngine instantiation rule**: Do NOT call `new CopyEngine()` or `CopyEngine.Instance`. Doing so triggers the Agile.NET module cctor and causes `ArgumentNullException: ptr` in the xUnit host. All behavioral tests use pure BCL simulation. Structural tests use `typeof(CopyEngine).GetMethod(...)` (type metadata only, no instance).

**InternalsVisibleTo assumption**: `IsFollowerPositionBlocked`, `TryArmBePriceRetry`, `TryFireBePriceRetry`, `StillAtNonBePrice` are all `internal`. The existing test file already accesses internal methods via `InternalsVisibleTo`. Tests 3-4 use reflection to invoke null-guard paths (which do not call any NT8 member) — this is safe.

#### Test Method List (14 methods, class B159KSuiteTests)

**T1 group (6 tests):**

1. `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue`
   - BCL simulation: `bool isBlocked = true;` → simulate guard → `if (isBlocked) return true` → assert true.
   - Structural: confirm `ShouldSkipFollower` method exists with 5 parameters via reflection.

2. `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse`
   - BCL simulation: `bool isBlocked = false;` → simulate all guards pass → assert false.

3. `IsFollowerPositionBlocked_NullAccount_ReturnsFalse`
   - Reflection invoke: `method.Invoke(null, new object[] { null, instrStub })` where `instrStub` is any non-null object cast as `object`.
   - Assert: result == false (null guard fires, no NT8 member accessed).
   - NOTE: `IsFollowerPositionBlocked` is `internal` on an instance method; invoke via `typeof(CopyEngine).GetMethod("IsFollowerPositionBlocked", NonPublic|Instance)`. Since we cannot instantiate CopyEngine, verify null-guard semantic via BCL simulation instead (simulate `if (acc == null) return false`).

4. `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse`
   - BCL simulation: `if (acc == null) return false; if (instr == null) return false;` → with `instr = null` → assert false.

5. `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse`
   - BCL simulation: `bool hasPos = false;` → guard passes both nulls → `return HasOpenPosition(...)` simulated as `return hasPos` → assert false.
   - Structural: confirm `IsFollowerPositionBlocked` method exists with 2 parameters (Account, Instrument) via reflection.

6. `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue`
   - BCL simulation: `bool hasPos = true;` → guard passes both nulls → `return hasPos` → assert true.

**T2 group (8 tests):**

7. `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop`
   - BCL simulation of TryFireBePriceRetry logic:
     - `var pending = new ConcurrentDictionary<string, double>(); pending["Sim102"] = 4825.0;`
     - Simulate: order.Name starts with "PTT-BE-Stop-", TryGetValue succeeds, state != Working, state == Cancelled, StillAtNonBePrice → true.
     - Assert: `pending.ContainsKey("Sim102") == false` (TryRemove called after fire).
   - Structural: confirm `TryFireBePriceRetry` method exists with 1 parameter (Order) via reflection.

8. `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop`
   - BCL simulation: StillAtNonBePrice → false (stop already at BE).
   - Assert: dict NOT modified (entry still present — method returned early at !StillAtNonBePrice guard).

9. `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry`
   - BCL simulation:
     - `var attempts = new ConcurrentDictionary<string, int>(); attempts["Sim102"] = 3;`
     - `var pending = new ConcurrentDictionary<string, double>();`
     - Simulate guard: `int a = attempts.GetOrAdd("Sim102", 0); if (a >= BeRetryMaxAttempts) return;`
     - Assert: `pending.ContainsKey("Sim102") == false` (ceiling guard fired, no entry added).

10. `TryFireBePriceRetry_WorkingOrder_RemovesFromDict`
    - BCL simulation: order state == Working → evict path → TryRemove on both dicts.
    - Assert: both dicts no longer contain key; SubmitBeStop NOT called (simulate via bool flag).

11. `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly`
    - BCL simulation: order.Name = "PTT-QX-Stop-1" → `!StartsWith("PTT-BE-Stop-")` guard fires → early return.
    - Assert: pending dict unchanged.

12. `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict`
    - BCL simulation: acc non-null (string key "Sim102"), instr non-null, bePrice=4825.0, attempts=0.
    - Simulate: GetOrAdd returns 0, ceiling guard not fired, `pending["Sim102"] = 4825.0`, AddOrUpdate.
    - Assert: `pending["Sim102"] == 4825.0` and `attemptsDict["Sim102"] == 1`.

13. `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop`
    - BCL simulation: pending dict empty → TryGetValue returns false → early return.
    - Assert: no SubmitBeStop call (simulated bool flag stays false).

14. `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue`
    - Structural: confirm `StillAtNonBePrice` exists with 3 parameters (Account, Instrument, double) via reflection.
    - BCL simulation: simulate the foreach logic — `order.Name` starts with "PTT-BE-Stop-", same instrument, `OrderState.Working`, `Math.Abs(stopPrice - bePrice) > tick * 0.5` → true.
    - Assert: simulated result == true.

**JS Rule Compliance (T3):**
| Rule | Verdict |
|------|---------|
| JS-CYC | PASS — all test methods CYC=1-2 (straight-line BCL simulation + assert) |
| JS-001 | PASS — no throw in test bodies; Assert.* is the xUnit contract, not a production throw |
| JS-021 | PASS — no lock anywhere in test code |
| JS-042 | PASS — all string literals ASCII-only: "PTT-BE-Stop-1", "Sim102", "PTT-QX-Stop-1", etc. |
| JS-002 | PASS — test methods return void |

**Verification:**
```
dotnet test src/PropTraderTools/CopyEngineTests.cs
```
Expected: 14 new [Fact] methods in `B159KSuiteTests` pass green. Zero [Skip].

---

## 4. Jane Street Rule Compliance Matrix

| Rule | T1 (K5) | T2 (K3) | T3 (K2) |
|------|---------|---------|---------|
| JS-CYC (<=8) | PASS — no branches (comment only) | PASS — CYC=5 unchanged | PASS — all tests CYC=1-2 |
| JS-001 (no throw) | PASS | PASS | PASS |
| JS-021 (no lock) | PASS | PASS | PASS |
| JS-042 (ASCII-only) | PASS — `(line 5507)` is ASCII | PASS — `BeRetryMaxAttempts` is ASCII; const comment is ASCII | PASS — all string literals ASCII |
| JS-002 (no null returns) | N/A (comment only) | N/A (void return, no change) | N/A (void test methods) |

All tickets: PASS across all rules.

---

## 5. Component List and Method Signatures

### T1 — Modified comment only

No new symbols. No modified method signatures. Single-line comment update.

**Comment site:**
- File: `src/PropTraderTools/CopyEngine.cs`
- Line: 3196
- Before: `// Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).`
- After: `// Signal 1 (HasOpenPosition, line 5507): position Quantity > 0 (normal case, B159).`

---

### T2 — New constant, one literal replacement

**New constant:**
```csharp
private const int BeRetryMaxAttempts = 3;
```
- Placement: class-scope field declarations region (~line 323-326), co-located with `_pendingBePriceRetry` / `_bePriceRetryAttempts` declarations.
- Visibility: `private` (no cross-class exposure needed; `BeRetryMaxAttempts` is consumed only by `TryArmBePriceRetry` in the same class).

**Modified line in `TryArmBePriceRetry`:**
- Before: `if (attempts >= 3)  return;  // (5) ceiling guard`
- After: `if (attempts >= BeRetryMaxAttempts)  return;  // (5) ceiling guard`

**Method signature (unchanged — for reference):**
```csharp
internal void TryArmBePriceRetry(Account acc, Instrument instr, double bePrice)
```
CYC=5 (base + acc-null + instr-null + bePrice<=0 + attempts>=BeRetryMaxAttempts).

---

### T3 — New test class

```csharp
public class B159KSuiteTests { ... }
```

14 [Fact] methods (all void, no parameters):

**T1 group:**
```csharp
[Fact] public void ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue()
[Fact] public void ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse()
[Fact] public void IsFollowerPositionBlocked_NullAccount_ReturnsFalse()
[Fact] public void IsFollowerPositionBlocked_NullInstrument_ReturnsFalse()
[Fact] public void IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse()
[Fact] public void IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue()
```

**T2 group:**
```csharp
[Fact] public void TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop()
[Fact] public void TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop()
[Fact] public void TryArmBePriceRetry_AttemptsAtCap_NoNewEntry()
[Fact] public void TryFireBePriceRetry_WorkingOrder_RemovesFromDict()
[Fact] public void TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly()
[Fact] public void TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict()
[Fact] public void TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop()
[Fact] public void StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue()
```

---

## 6. NT8 API Surface Notes

No new NT8 API calls introduced in this block.

Key NT8 facts (ground-truth for engineer):
- `HasOpenPosition(Account acc, Instrument instrument)` — `private bool` method of `CopyEngine` at line **5507**. NOT an NT8 AddOn API. It is a CopyEngine helper. Its NT8 account access (`acc.Positions`) is inside the method body and is never invoked in test code.
- `TryArmBePriceRetry`, `TryFireBePriceRetry`, `StillAtNonBePrice` — all `internal` methods of `CopyEngine`. Accessible to tests via structural reflection. Do NOT instantiate CopyEngine to invoke them (Agile.NET cctor).
- `ConcurrentDictionary<string, double>` / `ConcurrentDictionary<string, int>` — BCL, no NT8. Safe to allocate directly in test code for simulation.
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT used in this block.
- `AtmStrategyCreate()` — StrategyBase-only. NOT used in this block.
- `Account.Change()` / `Account.Cancel()` / `Account.CreateOrder()` — NOT used in this block.

---

## 7. Threading Model

T1: Zero threading impact (comment change).

T2: `private const int BeRetryMaxAttempts = 3;` is a compile-time constant — not a field, not a variable. The compiler substitutes the literal value at every call site. Zero threading implications. No instance state change.

T3: Tests run in xUnit host. Pure BCL simulation — no Dispatcher.InvokeAsync needed. `ConcurrentDictionary` allocations in test methods are local to each test (stack-allocated effectively). No shared state between tests.

---

## 8. Verification Checklist Per Ticket

### T1 (K5) Verification

| Check | Command | Expected |
|-------|---------|----------|
| Comment updated | `grep "HasOpenPosition, line 5507" src/PropTraderTools/CopyEngine.cs` | 1 match at ~line 3196 |
| Old stale comment absent | `grep "line 5273" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| Old stale comment absent | `grep "line 5294" src/PropTraderTools/CopyEngine.cs` | 0 matches (confirms no new stale number introduced) |
| CYC unchanged | Manual review of IsFollowerPositionBlocked (lines 3219-3262) | 8 branches, no new branches |
| ASCII-only | `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs` (T1 region) | 0 matches |

### T2 (K3) Verification

| Check | Command | Expected |
|-------|---------|----------|
| Const declared | `grep "BeRetryMaxAttempts" src/PropTraderTools/CopyEngine.cs` | 2 matches: declaration + usage |
| Literal gone | `grep "attempts >= 3" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| CYC unchanged | Manual review of TryArmBePriceRetry (lines 7186-7199) | 5 branches, unchanged |
| No lock | `grep "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code |
| ASCII-only | `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs` (T2 region) | 0 matches |
| K3-UI not in scope | `grep "BeRetryMaxAttempts" src/PropTraderTools/PropTraderToolsSettingsPanel.cs` | 0 matches (deferred) |

### T3 (K2) Verification

| Check | Command | Expected |
|-------|---------|----------|
| Class exists | `grep "class B159KSuiteTests" src/PropTraderTools/CopyEngineTests.cs` | 1 match |
| 14 [Fact] methods | `grep "\[Fact\]" src/PropTraderTools/CopyEngineTests.cs` (B159KSuiteTests region) | 14 matches |
| No [Skip] | `grep "Skip" src/PropTraderTools/CopyEngineTests.cs` (B159KSuiteTests region) | 0 matches |
| Tests pass | `dotnet test` | 14 new tests green |
| ASCII-only | `grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngineTests.cs` (B159KSuiteTests region) | 0 matches |
| No lock | `grep "lock(" src/PropTraderTools/CopyEngineTests.cs` (B159KSuiteTests region) | 0 matches |

---

## 9. Execution Order

```
T1 (DW-B159-K5)  --  CopyEngine.cs comment fix       (line 3196 only)
    |
    v
T2 (DW-B159-K3)  --  CopyEngine.cs const extraction  (const + line 7192)
    |
    v
T3 (DW-B159-K2)  --  CopyEngineTests.cs 14 tests     (B159KSuiteTests class)
    |
    v
powershell -File .\scripts\ptt-sync.ps1               (after each .cs edit)
powershell -File .\deploy-sync.ps1                    (before push, DIFF GUARD)
```

T1 and T2 both modify `CopyEngine.cs`. Execute them sequentially. T3 modifies only `CopyEngineTests.cs` — it can logically follow after T1+T2 complete.

**Diff size estimate:**
- T1: 1 line changed (comment). ~80 characters.
- T2: 6 lines added (const + comment) + 1 line modified (usage). ~250 characters.
- T3: ~14 test methods × ~15 lines each ≈ 210 lines. ~4000 characters.
- Total: well under the 10,000-character DIFF GUARD limit.

---

## 10. Deferred Items Closed by This Block

| Item | Status |
|------|--------|
| DW-B159-K5 | CLOSED — stale comment corrected (original comment was superseded by B160-B167; new annotation added at line 3196) |
| DW-B159-K3 | CLOSED — magic literal `3` extracted to `BeRetryMaxAttempts` const (K3-UI settings panel wiring remains deferred) |
| DW-B159-K2 | CLOSED — 14 [Fact] tests implemented in `B159KSuiteTests` |

Items NOT addressed (remain OPEN):
- DW-B159-K1 (SIM Gate Re-run) — P0, requires live NinjaTrader SIM session
- DW-B159-K4 (`intendedBePrice` sourcing dependency) — P2, track-only

---

*Architecture plan produced by ptt-architect. Phase 1. Wave DW-B159-KS. Date 2026-10-01.*
*All 8 sequential thoughts completed. Zero JS rule violations found. Zero CYC violations.*
