# B200+B201 Formal Tickets

**Epic**: PTT-COPIER-B200 + B201 — Direct Repair Formalisation + Entry-Order Cancel Fix
**Date**: 2026-10-15
**Author**: ptt-architect (Phase 3)
**Source plan**: `docs/brain/B200/02-architecture-plan.md` (REVIEW_PASS)
**Plan review**: `docs/brain/B200/02-plan-review.md` (REVIEW_PASS — zero violations)
**Pipeline**: SINGLE-PIPELINE (T1 -> T2 -> T3 -> T4 sequential)

---

## Execution Order

```
T1  ptt-engineer  src/PropTraderTools/Tests/B200Tests.cs        (new — test-only)
T2  ptt-engineer  src/PropTraderTools/CopyEngine.cs             (B201 code fix)
T3  ptt-engineer  src/PropTraderTools/Tests/B201Tests.cs        (new — test-only, after T2)
T4  ptt-engineer  docs/brain/B200/06-deferred-backlog.md        (new doc + log confirm)
```

T3 MUST follow T2. T2's new signature `IsQxCancelCandidate(Order o, bool isFollower)` is the
contract T3 tests against. No ticket may be combined with another.

---

## JANE STREET P0 PRE-SCAN (MANDATORY — run before any .cs edit)

```powershell
# JS-021: lock ban — must return 0 results
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\(" | Measure-Object
# Expected: Count = 0

# JS-033: async void ban — must return 0 results
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
# Expected: Count = 0

# ptt-sync baseline — must be 19/0 before first edit
powershell -File .\scripts\ptt-sync.ps1
# Expected: 19 linked, 0 errors
```

If any check fails: STOP. Report violation ID + file + line. Do not proceed.

---

---

## TICKET 1 — B200Tests.cs (Test Coverage for 4 B200 Repairs)

### Ticket ID: T1
### Title: Create B200Tests.cs — xUnit coverage for B200-QTY-01, B200-SFB-NULL-01, B200-NAKED-01, B200-CLOSE-LANED-01

---

### Scope

| Action | File |
|--------|------|
| CREATE | `src/PropTraderTools/Tests/B200Tests.cs` |
| MODIFY | `src/PropTraderTools/PropTraderTools.Tests.csproj` (add compile entry) |

**Zero changes to any `.cs` source file in this ticket.**

---

### Spec Requirement IDs

| Bug ID | Description |
|--------|-------------|
| B200-QTY-01 | HandleEntryChange uses `fo.Quantity + fo.Filled` not `fo.Quantity` alone |
| B200-SFB-NULL-01 | TryLaneDDispatchFollower sets `_followerSlateReady[acc.Name] = true` after ATM success |
| B200-NAKED-01 | TryNakedDetect skips PTT-Flatten for follower "Entry"-named Filled orders |
| B200-CLOSE-LANED-01 | V13 sentinel anchor + PartFilled guard in HasActivePttFlatten and FlattenOneAccount |

---

### Method Signatures

All test methods are `[Fact]`, return `void`, take no parameters.

```csharp
[Fact]
public void T_B200_01_HandleEntryChange_PartFilledFollowerOrder_UsesOriginalQty()

[Fact]
public void T_B200_02_LaneDDispatch_SetsSlateReady_AfterAtmSuccess()

[Fact]
public void T_B200_03_TryNakedDetect_SkipsEntryNamedOrders_OnFollower()

[Fact]
public void T_B200_04_FlattenInFlight_SentinelAnchor_BlocksStaleFilledEvent()
```

---

### Implementation Steps

1. Create `src/PropTraderTools/Tests/B200Tests.cs` with `using Xunit;` (no NUnit, no MSTest).
2. Declare `public class B200Tests` in the appropriate namespace (match existing test files).

**T_B200_01 — B200-QTY-01**
3. Arrange a follower account order stub with `Quantity = 5`, `Filled = 5` (PartFilled, 10 total).
4. Invoke the path under `HandleEntryChange` that calls `DrainThenDispatch` (or inspect via
   `PendingDispatchDrain` capture seam if available).
5. Assert: the drain payload quantity equals `10` (`Quantity + Filled`), NOT `5` (`Quantity` alone).
6. Assert: if `SendCopyWithAtm` is observable, it is called with `signal.Quantity = 10`.

**T_B200_02 — B200-SFB-NULL-01**
7. Arrange a follower account. Set `_followerSlateReady[acc.Name] = false` (post-wipe state).
8. Trigger the successful `SendCopyWithAtm` path inside `TryLaneDDispatchFollower`.
9. Assert: `_followerSlateReady[acc.Name] == true` after the call.
10. Assert: `FindFollowerBracketOrder` does NOT return null for a Working bracket after dispatch.

**T_B200_03 — B200-NAKED-01**
11. Arrange a follower account receiving a Filled event for an order named `"Entry"`.
12. Assert: `TryNakedDetect` returns without firing PTT-Flatten.
13. Assert: no flatten order is submitted when `order.Name == "Entry"` on a follower account.

**T_B200_04 — B200-CLOSE-LANED-01**
14. Arrange: set `_flattenInFlightOrderId[acc.Name] = acc.Name + "#pending"` (V13 sentinel).
15. Assert: a stale Filled event with a different `OrderId` does NOT clear `_flattenInFlight[acc.Name]`.
16. Assert: `IsFlattenInFlightOrRecent` returns `true` while sentinel is set.
17. Assert: after the real submit fires, `_flattenInFlightOrderId` is overwritten with the real `OrderId`.
18. Assert: a Filled event with the matching `OrderId` correctly clears `_flattenInFlight[acc.Name]`.

**csproj update**
19. Add `<Compile Include="Tests\B200Tests.cs" />` to `PropTraderTools.Tests.csproj`.

---

### JS Rule Constraints

| Rule | Constraint | Applied To |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | B200Tests.cs — assert statements only, no locking |
| JS-033 | No `async void` (non-handler) | All test methods are synchronous `void` [Fact] |
| JS-066 | CYC <= 8 per method | Test methods are linear (CYC = 1 each). No branching in Arrange/Assert. |
| JS-001 | No `throw new` in hot paths | Use `Assert.Throws` pattern if exception testing is needed |
| xUnit-only | `[Fact]` only — no `[Test]`, no `[TestMethod]` | `using Xunit;` only — no NUnit, no MSTest |
| ASCII-only | No Unicode, emoji, or curly quotes in string literals | `"Entry"`, `"#pending"`, `"PTT-"` all ASCII |

---

### 7-Scan Checklist (T1)

Engineer MUST run all 7 scans and confirm PASS before reporting BUILD_PASS.

**SCAN-01 — Lock ban**
```powershell
Select-String -Path "src/PropTraderTools/Tests/B200Tests.cs" -Pattern "lock\s*\("
# Expected: 0 matches
```

**SCAN-02 — Async void ban**
```powershell
Select-String -Path "src/PropTraderTools/Tests/B200Tests.cs" -Pattern "async\s+void\s+\w+\("
# Expected: 0 matches
```

**SCAN-03 — Tests pass**
```powershell
dotnet test --filter "B200"
# Expected: 4 passed, 0 failed, 0 skipped
```

**SCAN-04 — No source file modified**
```powershell
git diff --name-only HEAD
# Expected: B200Tests.cs and PropTraderTools.Tests.csproj only — NO CopyEngine.cs
```

**SCAN-05 — xUnit [Fact] only**
```powershell
Select-String -Path "src/PropTraderTools/Tests/B200Tests.cs" -Pattern "\[Test\]|\[TestMethod\]"
# Expected: 0 matches (must use [Fact] only)
```

**SCAN-06 — ASCII-only string literals**
```powershell
grep -P "[\x80-\xFF]" src/PropTraderTools/Tests/B200Tests.cs
# Expected: 0 matches
```

**SCAN-07 — File scope: T1 creates B200Tests.cs ONLY**
```powershell
git diff --name-only HEAD
# Must NOT include CopyEngine.cs or any file outside Tests/ and the .csproj
```

---

### Acceptance Criteria (T1)

- [ ] `src/PropTraderTools/Tests/B200Tests.cs` created and compiles clean
- [ ] All 4 `[Fact]` tests pass: `dotnet test --filter "B200"` — 4 passed, 0 failed
- [ ] No existing test regressions: `dotnet test` (full suite) — clean run
- [ ] No `.cs` source files modified (test file + .csproj only)
- [ ] `powershell -File .\scripts\ptt-sync.ps1` reports 19 linked, 0 errors
- [ ] All 7 SCAN checks above pass

---

---

## TICKET 2 — B201-ENTRY-CANCEL-01 Code Fix (IsQxCancelCandidate isFollower Guard)

### Ticket ID: T2
### Title: Scope `IsQxCancelCandidate` "Entry" cancel to follower accounts only — B201 fix

---

### Scope

| Action | File |
|--------|------|
| MODIFY | `src/PropTraderTools/CopyEngine.cs` — 5 touch points only |

**Do NOT touch**: `PttBreakEvenSwap.cs`, `PttGlobalQuickExit.cs`, `PttQuickExit.cs`, or any Feature file.

`PttQuickExit.cs` calls `CopyEngine.BuildQxSnapshot(leader, instr)` (2-arg form) — the defaulted
`bool isFollower = false` parameter makes this backward-compatible. No change to `PttQuickExit.cs`.

---

### Spec Requirement IDs

| Bug ID | Description |
|--------|-------------|
| B201-ENTRY-CANCEL-01 | `IsQxCancelCandidate` "Entry" check not scoped to follower accounts; leader's add-to-position "Entry" limit incorrectly cancelled by QX/BE button |

---

### Method Signatures (Before / After)

**Touch point 1 — `IsQxCancelCandidate` (~L982)**

```csharp
// BEFORE:
internal static bool IsQxCancelCandidate(Order o)

// AFTER:
internal static bool IsQxCancelCandidate(Order o, bool isFollower)
```

**Touch point 2 — `IsQxCancelCandidate` body (~L994)**

```csharp
// BEFORE:
if (o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit

// AFTER:
if (isFollower && o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit -- scoped to follower only
```

**Touch point 3a — `CancelQxBrackets` 2-arg body (~L1009 — after null guard)**

```csharp
// ADD at top of method body (after null guard at L1009):
bool isFollower = IsFollowerAccount(acc);

// UPDATE call at ~L1026:
// BEFORE:
if (IsQxCancelCandidate(o)) // (5) widened via helper
// AFTER:
if (IsQxCancelCandidate(o, isFollower)) // (5) widened via helper
```

**Touch point 3b — `BuildQxSnapshot` signature (~L1050)**

```csharp
// BEFORE:
internal static System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> BuildQxSnapshot(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr
)

// AFTER:
internal static System.Collections.Generic.HashSet<NinjaTrader.Cbi.Order> BuildQxSnapshot(
    NinjaTrader.Cbi.Account acc,
    NinjaTrader.Cbi.Instrument instr,
    bool isFollower = false
)
```

**CRITICAL STATIC CONSTRAINT**: `BuildQxSnapshot` is `internal static`. `IsFollowerAccount` is an
instance method. A static method CANNOT call an instance method. The orchestrator prompt's suggestion
of `bool isFollower = IsFollowerAccount(acc)` inside `BuildQxSnapshot` is WRONG and would not compile.
The correct pattern is the defaulted parameter `bool isFollower = false` as specified above.

**Touch point 3b (continued) — `BuildQxSnapshot` body (~L1070)**

```csharp
// UPDATE call at ~L1070:
// BEFORE:
if (IsQxCancelCandidate(o)) // (4)
// AFTER:
if (IsQxCancelCandidate(o, isFollower)) // (4)
```

**Touch point 3c — `CancelQxBrackets` 3-arg body (~L1208 — after null guard)**

```csharp
// ADD at top of method body (after null guard at L1207):
bool isFollower = IsFollowerAccount(acc);

// UPDATE call at ~L1230:
// BEFORE:
if (IsQxCancelCandidate(o)) // (6)
// AFTER:
if (IsQxCancelCandidate(o, isFollower)) // (6)
```

**CancelQxBrackets 2-arg and 3-arg signatures do NOT change.** Only their bodies gain the local variable.

| Method | Before Signature | After Signature |
|--------|-----------------|-----------------|
| `IsQxCancelCandidate` | `internal static bool IsQxCancelCandidate(Order o)` | `internal static bool IsQxCancelCandidate(Order o, bool isFollower)` |
| `CancelQxBrackets` 2-arg | `internal void CancelQxBrackets(Account acc, Instrument instr)` | UNCHANGED |
| `CancelQxBrackets` 3-arg | `internal void CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)` | UNCHANGED |
| `BuildQxSnapshot` | `internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr)` | `internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr, bool isFollower = false)` |

---

### CYC Impact for T2

| Method | Pre-B201 CYC | Delta | Post-B201 CYC | Budget | Status |
|--------|-------------|-------|--------------|--------|--------|
| `IsQxCancelCandidate` | 7 | +0 | 7 | <=8 | PASS |
| `CancelQxBrackets` 2-arg | 6 | +0 | 6 | <=8 | PASS |
| `CancelQxBrackets` 3-arg | 7 | +0 | 7 | <=8 | PASS |
| `BuildQxSnapshot` | 4 | +0 | 4 | <=8 | PASS |

`isFollower` is a local variable assignment — not a branch. `isFollower &&` extends an existing
`if` condition — +0 by Roslyn McCabe convention (project standard; consistent with B19x/B20x series).

---

### Implementation Steps

1. **Pre-scan**: Run the P0 Jane Street gate commands listed at the top of this document.
   All must pass before any edit.

2. **Read the current code** at L982–L997, L1007–L1039, L1050–L1078, L1201–L1250.
   Confirm actual line numbers match the approximate values above. Proceed with exact line numbers.

3. **Touch point 1+2 — `IsQxCancelCandidate`**:
   - Change signature from `IsQxCancelCandidate(Order o)` to `IsQxCancelCandidate(Order o, bool isFollower)`.
   - Change body at the "Entry" guard: `if (o.Name == "Entry")` → `if (isFollower && o.Name == "Entry")`.
   - Update the comment from `DW-B93: Named ATM follower entry Limit` to
     `DW-B93: Named ATM follower entry Limit -- scoped to follower only`.

4. **Touch point 3a — `CancelQxBrackets` 2-arg body**:
   - After the null guard (`return; // (1)`), add one line: `bool isFollower = IsFollowerAccount(acc);`
   - Update the `IsQxCancelCandidate(o)` call to `IsQxCancelCandidate(o, isFollower)`.

5. **Touch point 3b — `BuildQxSnapshot` signature + body**:
   - Add `bool isFollower = false` as a third parameter with default value (NOT `IsFollowerAccount(acc)`).
   - Update the `IsQxCancelCandidate(o)` call inside the foreach to `IsQxCancelCandidate(o, isFollower)`.
   - Do NOT change the `PttQuickExit.cs` caller — the default parameter handles backward compatibility.

6. **Touch point 3c — `CancelQxBrackets` 3-arg body**:
   - After the null guard (`return; // (1)`), add one line: `bool isFollower = IsFollowerAccount(acc);`
   - Update the `IsQxCancelCandidate(o)` call to `IsQxCancelCandidate(o, isFollower)`.

7. **CYC comments**: Update the comment block above `IsQxCancelCandidate` to reflect the new signature.
   CYC count remains 7. Do NOT change CYC numbers.

8. **Deletion audit**: Confirm no symbols were deleted. `IsQxCancelCandidate` is a signature change,
   not a deletion. The old 1-arg overload is replaced (no separate overload retained).

9. **Run post-edit scans** (7-scan checklist below).

---

### JS Rule Constraints

| Rule | Constraint | Applied To |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | `bool isFollower = IsFollowerAccount(acc)` is a pure method call — no lock. ConcurrentDictionary used elsewhere. |
| JS-001 | No `throw` in hot path | No new `throw` statements in any touch point |
| JS-002 | No `return null` | `BuildQxSnapshot` returns `new HashSet<Order>()` on null input — no change |
| JS-033 | No `async void` (non-handler) | No new async methods |
| JS-066 | CYC <= 8 per method | All touched methods CYC unchanged at <=8 (see table above) |
| ASCII-only | No Unicode/emoji/curly quotes | `"Entry"`, `"#pending"`, `"PTT-"` all ASCII |
| static constraint | Static method cannot call instance method | `BuildQxSnapshot` uses defaulted param, NOT `IsFollowerAccount(acc)` inside the method |

---

### 7-Scan Checklist (T2)

Engineer MUST run all 7 scans and confirm PASS before reporting BUILD_PASS.

**SCAN-01 — Lock ban**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\("
# Expected: 0 matches
```

**SCAN-02 — Async void ban**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\("
# Expected: 0 matches
```

**SCAN-03 — All existing tests pass**
```powershell
dotnet test
# Expected: full suite passes — 0 failed
```

**SCAN-04 — CYC verification (read-only, manual count)**
- `IsQxCancelCandidate`: CYC=7. Source code comment at ~L981 must say `CYC=7`.
- `CancelQxBrackets` 2-arg: CYC=6. Comment at ~L1005 confirms.
- `CancelQxBrackets` 3-arg: CYC=7. Comment at ~L1197-1198 confirms.
- `BuildQxSnapshot`: CYC=4. Comment at ~L1046 confirms.
- No method CYC exceeds 8.

**SCAN-05 — Static constraint: no IsFollowerAccount inside BuildQxSnapshot**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "IsFollowerAccount" | Select-String "BuildQxSnapshot"
# Expected: 0 matches — IsFollowerAccount must NOT appear inside BuildQxSnapshot body
```

Verify the `isFollower` count pattern is correct (OVERRIDE of orchestrator prompt):
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = IsFollowerAccount"
# Expected: EXACTLY 2 matches (CancelQxBrackets 2-arg, CancelQxBrackets 3-arg)
# NOT 3 — BuildQxSnapshot uses default param, not IsFollowerAccount call
```

**SCAN-06 — ASCII-only string literals**
```powershell
grep -P "[\x80-\xFF]" src/PropTraderTools/CopyEngine.cs
# Expected: 0 matches
```

**SCAN-07 — Scope + guardian + single-hunk ban**
```powershell
# Only CopyEngine.cs modified (no Feature files):
git diff --name-only HEAD
# Must NOT include PttBreakEvenSwap.cs, PttGlobalQuickExit.cs, PttQuickExit.cs

# Single-hunk ban: diff must show multiple distinct hunks (not a full-file replace):
git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\@\@"
# Must return Count > 1

# Guardian symbol script:
powershell -File .\scripts\verify_critical_symbols.ps1
# Must report PASS
```

**Ph4b deletion audit** (mandatory before VERIFY_PASS):
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-\s+(private|internal|public)"
# Every deleted symbol line must have an authorising DW-card. For T2: IsQxCancelCandidate
# signature change is expected. No other symbols should be deleted.
```

---

### Acceptance Criteria (T2)

- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "isFollower && o.Name"` → 1 match (at ~L994)
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = IsFollowerAccount"` → **2 matches** (CancelQxBrackets 2-arg body, CancelQxBrackets 3-arg body)
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = false"` → at least 1 match (BuildQxSnapshot defaulted param)
- [ ] `BuildQxSnapshot` signature includes `bool isFollower = false` as third parameter
- [ ] `IsQxCancelCandidate` signature updated to `(Order o, bool isFollower)`
- [ ] `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0
- [ ] All existing tests pass: `dotnet test` clean run
- [ ] `powershell -File .\scripts\ptt-sync.ps1` → 19 linked, 0 errors
- [ ] Ph4b deletion audit: no symbols deleted without authorising DW-card
- [ ] Single-hunk ban: `(git show HEAD -- CopyEngine.cs | Select-String "^\@\@").Count -gt 1`
- [ ] `powershell -File .\scripts\verify_critical_symbols.ps1` → PASS
- [ ] No Feature files modified

---

### Do-NOT-Touch List (T2)

| File | Reason |
|------|--------|
| `PttBreakEvenSwap.cs` | Calls `CancelQxBrackets(acc, instr)` — 2-arg overload signature unchanged |
| `PttGlobalQuickExit.cs` | Routes through `PttQuickExit.Execute`; no direct exposure |
| `PttQuickExit.cs` | Calls `BuildQxSnapshot(leader, instr)` (2-arg) — backward-compatible via `isFollower=false` default |
| Any other Feature file | Out of scope for B201 fix |

---

---

## TICKET 3 — B201Tests.cs (Test Coverage for B201 Fix)

### Ticket ID: T3
### Title: Create B201Tests.cs — xUnit coverage for B201-ENTRY-CANCEL-01 (must follow T2)

**Dependency**: T3 MUST be written AFTER T2 is complete and VERIFY_PASS issued.
T3 tests the signature `IsQxCancelCandidate(Order o, bool isFollower)` produced by T2.

---

### Scope

| Action | File |
|--------|------|
| CREATE | `src/PropTraderTools/Tests/B201Tests.cs` |
| MODIFY | `src/PropTraderTools/PropTraderTools.Tests.csproj` (add compile entry) |

**Zero changes to any `.cs` source file in this ticket.**

---

### Spec Requirement IDs

| Bug ID | Description |
|--------|-------------|
| B201-ENTRY-CANCEL-01 | `IsQxCancelCandidate` "Entry" check not scoped to follower accounts |

---

### Method Signatures

All test methods are `[Fact]`, return `void`, take no parameters.

```csharp
[Fact]
public void T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled()

[Fact]
public void T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled()

[Fact]
public void T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled()

[Fact]
public void T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder()
```

---

### Implementation Steps

1. Create `src/PropTraderTools/Tests/B201Tests.cs` with `using Xunit;` (no NUnit, no MSTest).
2. Declare `public class B201Tests` in the appropriate namespace (match existing test files).

**T_B201_01 — Leader "Entry" NOT cancelled (B201 core assertion)**
3. Create an order stub with `o.Name = "Entry"`.
4. Call `CopyEngine.IsQxCancelCandidate(o, false)` (isFollower = false = leader).
5. Assert: result is `false`.
   (Before the B201 fix this would have returned `true` — this test proves the fix.)

**T_B201_02 — Follower "Entry" IS cancelled**
6. Create an order stub with `o.Name = "Entry"`.
7. Call `CopyEngine.IsQxCancelCandidate(o, true)` (isFollower = true = follower).
8. Assert: result is `true`.
   (Follower "Entry" limit orders must still be cancelled on QX/BE.)

**T_B201_03 — ATM bracket names always cancelled regardless of isFollower**
9. For each name in `{ "Stop1", "Target2", "PTT-QX-Stop", "PTT-BE-Target-1", "PTT-CopyEntry" }`:
   - Create order stub with that name.
   - Call `CopyEngine.IsQxCancelCandidate(o, false)` (leader, isFollower = false).
   - Assert: result is `true`.
10. This confirms that `isFollower = false` does NOT protect ATM bracket names from cancellation —
    only the `"Entry"` specific branch is guarded.

**T_B201_04 — CancelQxBrackets leader path skips "Entry"**
11. Arrange: create a stub leader account (not a follower account per `IsFollowerAccount` logic).
12. Add two orders to the account:
    - `"Entry"` Limit order in `Working` state.
    - `"Stop1"` StopMarket order in `TriggerPending` state.
13. Call `CancelQxBrackets(leaderAcc, instr)` (2-arg overload).
14. Assert: `acc.Cancel` is called with a list containing `"Stop1"` only.
15. Assert: `"Entry"` order is NOT in the cancel list.

**csproj update**
16. Add `<Compile Include="Tests\B201Tests.cs" />` to `PropTraderTools.Tests.csproj`.

---

### JS Rule Constraints

| Rule | Constraint | Applied To |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | B201Tests.cs — assert statements only, no locking |
| JS-033 | No `async void` (non-handler) | All test methods are synchronous `void` [Fact] |
| JS-066 | CYC <= 8 per method | Test methods are linear (CYC = 1 each). No branching. |
| JS-001 | No `throw new` in hot paths | Use `Assert.Throws` pattern if needed |
| xUnit-only | `[Fact]` only — no `[Test]`, no `[TestMethod]` | `using Xunit;` only |
| ASCII-only | No Unicode, emoji, or curly quotes | `"Entry"`, `"Stop1"`, `"PTT-"` all ASCII |
| T2-dependency | Must reference T2 signature | Calls `IsQxCancelCandidate(o, bool isFollower)` — not `IsQxCancelCandidate(o)` |

---

### 7-Scan Checklist (T3)

Engineer MUST run all 7 scans and confirm PASS before reporting BUILD_PASS.

**SCAN-01 — Lock ban**
```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "lock\s*\("
# Expected: 0 matches
```

**SCAN-02 — Async void ban**
```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "async\s+void\s+\w+\("
# Expected: 0 matches
```

**SCAN-03 — Tests pass**
```powershell
dotnet test --filter "B201"
# Expected: 4 passed, 0 failed, 0 skipped
```

**SCAN-04 — No source file modified**
```powershell
git diff --name-only HEAD
# Expected: B201Tests.cs and PropTraderTools.Tests.csproj only — NO CopyEngine.cs
```

**SCAN-05 — xUnit [Fact] only**
```powershell
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "\[Test\]|\[TestMethod\]"
# Expected: 0 matches
```

**SCAN-06 — ASCII-only string literals**
```powershell
grep -P "[\x80-\xFF]" src/PropTraderTools/Tests/B201Tests.cs
# Expected: 0 matches
```

**SCAN-07 — File scope: T3 creates B201Tests.cs ONLY + references T2 signature**
```powershell
# Confirm test file references the correct 2-arg IsQxCancelCandidate:
Select-String -Path "src/PropTraderTools/Tests/B201Tests.cs" -Pattern "IsQxCancelCandidate\(o,\s*(true|false)\)"
# Expected: >= 4 matches (one per Fact that calls the method)

# No src/PropTraderTools/CopyEngine.cs in diff:
git diff --name-only HEAD | Select-String "CopyEngine"
# Expected: 0 matches
```

---

### Acceptance Criteria (T3)

- [ ] `src/PropTraderTools/Tests/B201Tests.cs` created and compiles clean
- [ ] All 4 `[Fact]` tests pass: `dotnet test --filter "B201"` — 4 passed, 0 failed
- [ ] No existing test regressions: `dotnet test` (full suite) — clean run
- [ ] `T_B201_01`: `IsQxCancelCandidate(entryOrder, false)` returns `false`
- [ ] `T_B201_02`: `IsQxCancelCandidate(entryOrder, true)` returns `true`
- [ ] `T_B201_03`: All 5 ATM bracket names return `true` with `isFollower=false`
- [ ] `T_B201_04`: `CancelQxBrackets(leaderAcc, instr)` does NOT cancel `"Entry"` order
- [ ] No `.cs` source files modified (test file + .csproj only)
- [ ] `powershell -File .\scripts\ptt-sync.ps1` reports 19 linked, 0 errors
- [ ] All 7 SCAN checks above pass

---

---

## TICKET 4 — CYC Confirmation + Log No-Op + Deferred Backlog

### Ticket ID: T4
### Title: Confirm log cleanup no-op, verify CYC values, create 06-deferred-backlog.md

---

### Scope

| Action | File |
|--------|------|
| CREATE | `docs/brain/B200/06-deferred-backlog.md` |
| CONFIRM | `src/PropTraderTools/CopyEngine.cs` — read-only grep confirm that both diagnostic log tags are absent |

**CopyEngine.cs changes in T4**: NONE expected (both tags already absent per architect verification).
If a tag is found present (grep returns > 0), remove only those specific lines before creating the backlog doc.

---

### Spec Requirement IDs

| Item | Description |
|------|-------------|
| T4-LOG-AUDIT | Confirm `[PTT-ATM-CALLER]` and `[PTT-FLAT-OPEN]` already absent from CopyEngine.cs |
| T4-CYC-CONFIRM | Read-only CYC verification for all methods modified in this epic |
| T4-DEFERRED | Create `docs/brain/B200/06-deferred-backlog.md` with 3 DW items |

---

### Implementation Steps

**Step 1 — Log cleanup confirmation (grep-first, change-only-if-found)**

```powershell
# Confirm diagnostic logs already absent:
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-CALLER\]"
# Expected: 0 matches (already removed)

Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-FLAT-OPEN\]"
# Expected: 0 matches (already removed)
```

If either grep returns > 0: remove ONLY those specific log lines. Do not touch surrounding code.
If both return 0: no action on CopyEngine.cs. Proceed to Step 2.

Log tags that MUST remain untouched (do NOT remove):
- `[PTT-ATM-SEND]`
- `[PTT-DR4-CLEAR]`
- `[PTT-DR5-EVICT]`
- `[PTT-GATE5-BLOCKED]`
- `[PTT-GATE2-BLOCKED]`
- `[PTT-ATM-ERROR]`
- `[PTT-DISPATCH]`
- `[TP1-OOU]`

**Step 2 — CYC confirmation (read-only — do NOT change any CYC comments)**

Verify these values in CopyEngine.cs by reading the comment blocks. Report the actual values in
the completion document. No code edits unless a comment is factually wrong (requires Director approval).

| Method | Expected CYC | Confirm Location |
|--------|-------------|-----------------|
| `HasActivePttFlatten` | 4 | Comment at ~L3051 |
| `FlattenOneAccount` | 7 | Comment at ~L5650 |
| `IsQxCancelCandidate` | 7 | Comment at ~L981 (after T2 fix, +0 delta) |
| `IsFlattenInFlightOrRecent` | <=4 | Comment at or near the method declaration |

**Step 3 — Create `docs/brain/B200/06-deferred-backlog.md`**

Create the file with exactly 3 deferred work items as specified below. Do NOT implement any of these.

---

### Deferred Backlog File Content

Create `docs/brain/B200/06-deferred-backlog.md` with the following content:

```markdown
# B200+B201 Deferred Backlog

**Epic**: PTT-COPIER-B200 + B201
**Created**: T4 completion
**Author**: ptt-engineer (T4)

---

## Open Items

### DW-B200-LANE-E-COPY-ENABLED (P2) — OPEN

**Summary**: `TryLaneEHook` is called from `OnFollowerPositionUpdate` and is not gated on `_isCopyEnabled`.

**Detail**: If the user disables copy mid-session, Lane E can still fire. Low correctness risk —
`PassesDailyCapCheck` is the outer guard. No live trade scenario has exposed this. Deferred to B202+.

**Action required**: Add `if (!_isCopyEnabled) return;` guard at the entry of `TryLaneEHook`, or
ensure the outer call path gates on `_isCopyEnabled` before calling `TryLaneEHook`.

**Deferred to**: B202+

---

### DW-B201-OPTION-B (P3) — OPEN

**Summary**: Alternative approach for follower "Entry" order cancel fix: rename the follower
dispatch name from `"Entry"` to `"PTT-Copy-Entry"` at `SendCopyWithAtm` dispatch time.

**Detail**: B201 used Option A (isFollower scope guard). Option B is simpler long-term — removes
the need for `isFollower` threading entirely, as the name would no longer collide with the leader's
`"Entry"` ATM limit. Deferred pending Director decision on trade-off.

**Action required**: Director decision. If Option B is selected, T2 changes are replaced by a
name-override at `SendCopyWithAtm` dispatch and `IsQxCancelCandidate` reverts to the 1-arg form.

**Deferred to**: Director decision required before B202+

---

### DW-B199-H2-CLEANUP-01 (P3) — CONDITIONAL

**Summary**: B199 H2 divide remnant logic in `SendCopyWithAtm` — conditional cleanup.

**Detail**: If B199 H2 divide logic is still present as dead code in `SendCopyWithAtm`, it should
be removed. If already removed by prior engineers, close as N/A.

**Action required**: Grep for the B199 H2 divide pattern in `SendCopyWithAtm`. If found, remove
the dead-code block. If absent, close this item as N/A.

**Deferred to**: B202 or close as N/A on verification
```

---

### JS Rule Constraints

| Rule | Constraint | Applied To |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | No code changes in T4 (log-confirm only). No lock introduced. |
| JS-033 | No `async void` | No new async methods |
| JS-066 | CYC <= 8 per method | Log removal = line deletion only. CYC unchanged. |
| JS-001 | No `throw` | No new code paths |
| ASCII-only | No Unicode/emoji/curly quotes | All DW item strings are ASCII |

---

### 7-Scan Checklist (T4)

Engineer MUST run all 7 scans and confirm PASS before reporting BUILD_PASS.

**SCAN-01 — Lock ban**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\("
# Expected: 0 matches (unchanged from pre-T4)
```

**SCAN-02 — Async void ban**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\("
# Expected: 0 matches
```

**SCAN-03 — Full test suite passes**
```powershell
dotnet test
# Expected: all tests pass — 0 failed
```

**SCAN-04 — Log tags confirmed absent**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-CALLER\]"
# Expected: 0 matches

Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-FLAT-OPEN\]"
# Expected: 0 matches
```

**SCAN-05 — Permanent operational logs intact**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-SEND\]|\[PTT-DR4-CLEAR\]|\[PTT-DR5-EVICT\]|\[PTT-GATE5-BLOCKED\]|\[PTT-GATE2-BLOCKED\]"
# Expected: >= 5 matches (permanent logs untouched)
```

**SCAN-06 — ASCII-only**
```powershell
grep -P "[\x80-\xFF]" docs/brain/B200/06-deferred-backlog.md
# Expected: 0 matches
```

**SCAN-07 — Scope: T4 creates 06-deferred-backlog.md ONLY**
```powershell
git diff --name-only HEAD
# Must include: docs/brain/B200/06-deferred-backlog.md
# May include: CopyEngine.cs ONLY if one of the log tags was found present (see Step 1)
# Must NOT include: any test file, any Feature file, any unrelated .cs file
```

---

### Acceptance Criteria (T4)

- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-CALLER\]"` = 0 matches (confirmed absent)
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-FLAT-OPEN\]"` = 0 matches (confirmed absent)
- [ ] `docs/brain/B200/06-deferred-backlog.md` created with all 3 deferred DW items (DW-B200-LANE-E-COPY-ENABLED, DW-B201-OPTION-B, DW-B199-H2-CLEANUP-01)
- [ ] CYC values confirmed: HasActivePttFlatten=4, FlattenOneAccount=7, IsQxCancelCandidate=7, IsFlattenInFlightOrRecent<=4
- [ ] All existing tests pass: `dotnet test` — 0 failed
- [ ] `powershell -File .\scripts\ptt-sync.ps1` → 19 linked, 0 errors
- [ ] All 7 SCAN checks above pass

---

---

## Ph5 Final Reviewer Checklist (Reference — Not Engineer Action)

These are the Ph5 cross-file coherence checks that ptt-plan-reviewer runs after all 4 VERIFY_PASS gates.

- [ ] All 4 VERIFY_PASS gates issued (ticket-1 through ticket-4 verification.md)
- [ ] `dotnet test` clean — zero failures, zero new warnings
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\("` = 0
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\("` = 0 (non-handlers)
- [ ] `powershell -File .\scripts\ptt-sync.ps1` → 19/0
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-CALLER\]"` = 0
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "isFollower && o.Name"` = 1 match at ~L994
- [ ] `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "bool isFollower = IsFollowerAccount"` = 2 matches (NOT 3)
- [ ] `docs/brain/B200/06-deferred-backlog.md` written with 3 deferred DW items
- [ ] `powershell -File .\scripts\verify_critical_symbols.ps1` → PASS
- [ ] `specs/002-trade-copier-spec.html` B200+B201 blocks confirm PIPELINE_COMPLETE (if spec update is in scope)

---

*Written by: ptt-architect*
*Epic: B200+B201*
*Phase: 3 (Ticket Generation)*
*Source: docs/brain/B200/02-architecture-plan.md (REVIEW_PASS)*
*Plan review override applied: BuildQxSnapshot uses bool isFollower = false defaulted param (NOT IsFollowerAccount call). T2 acceptance criteria = 2 grep matches for IsFollowerAccount (NOT 3).*
