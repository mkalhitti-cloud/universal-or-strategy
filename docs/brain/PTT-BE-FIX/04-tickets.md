# PTT-BE-FIX -- Tickets
Epic: PTT-BE-FIX
Phase: 3 (Ticket Generation)
Status: TICKETS_COMPLETE
Date: 2026-08-22
Author: ptt-architect
Input: docs/brain/PTT-BE-FIX/02-architecture-plan.md (REVIEW_PASS, Cycle 2)

---

## Execution Order

| Session | Tickets | Rationale |
|---------|---------|-----------|
| 1 | T1 + T4 | Same file (CopyEngine.cs), single build, single commit |
| 2 | T2 | Same file (CopyEngine.cs), different method, separate commit |
| 3 | T3 | New test file, separate commit |

T1 must complete before T3 (StopNameGuard_PttQxStop_* tests assert the T1 guard).
T2 is independent of T1 and T3; preferred order is T2 before T3 (production code before test code).

---

## T1 + T4 -- Session 1

---

### T1: DW-B86 -- Stop Name Guard Extension

**ID**: T1
**Title**: DW-B86 -- Extend stop name guard in MoveStopToBreakEven for PTT-QX-Stop* orders
**Priority**: P0
**File**: src/PropTraderTools/CopyEngine.cs
**Lines**: L2755-L2762 (guard replacement inside `if (IsFollowerAccount(acc))` block)

---

#### 1. Spec Requirement IDs

- section-b86 (002-trade-copier-spec.html)
- DW-B84-03 (state guard, already deployed -- no change required, cited for context)

---

#### 2. Problem Statement

After QX-ALL fires on a follower account, the ATM-owned Stop1..Stop9 orders are cancelled
and replaced with PTT-managed PTT-QX-Stop / PTT-QX-Stop2 / PTT-QX-Stop3 / PTT-QX-Stop4
orders (Accepted state on SIM). When BE-ALL is subsequently pressed, the follower path at
L2755 applies the existing name guard which only matches exactly Stop1..Stop9 (length 5,
IsDigit). PTT-QX-Stop* names fail every condition: StartsWith("Stop") is false, and Length != 5.
Result: beSt.Count == 0 for every follower, [BE-DIAG-F] fires, acc.Change() is called
with an empty array, and no stop movement occurs. Followers are left with stale QX-price stops.

---

#### 3. Acceptance Criteria

1. beSt.Count > 0 when a follower has PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3, or PTT-QX-Stop4
   orders in Working, Accepted, or ChangeSubmitted state.
2. beSt.Count > 0 when a follower has Stop1..Stop9 orders (existing ATM path -- preserved, no regression).
3. Orders named "StopMarket", "StopLimit", "PTT-BE-Stop-1", "PTT-QX-T1" are NOT added to beSt.
4. acc.Change() is called with beSt.ToArray() containing the correct stop orders.
5. SIM gate: 3 cycles of Path B (QX-ALL then BE-ALL) all report stops=N > 0 in Output tab
   for each follower account.

---

#### 4. Method Signatures

No new method introduced. The guard at L2755-2762 is refactored in-place inside:

```csharp
// existing method -- signature unchanged
private void MoveStopToBreakEven(Account acc, Instrument instrument, double newStop)
```

The refactor replaces the inline `if` with a named `bool isBeStop` local and an `if (isBeStop)` check.
Method CYC is unchanged (+0): one branch point in, one branch point out.

---

#### 5. Before / After Code (exact line numbers)

**BEFORE** (L2753-2762 -- exact source as read 2026-08-22):

```csharp
                    // ATM stop names are exactly "StopN" (length 5): Stop1, Stop2, Stop3 etc.
                    // Length==5 guard excludes StopLimit, StopMarket, StopLoss and any other prefix.
                    if (o.Name != null
                        && o.Name.StartsWith("Stop", StringComparison.Ordinal)
                        && o.Name.Length == 5
                        && char.IsDigit(o.Name[4]))
                    {
                        o.StopPriceChanged = newStop;
                        beSt.Add(o);
                    }
```

**AFTER** (replace L2753-2762 with this block -- copy-paste ready):

```csharp
                    // DW-B86: extend stop name guard to match PTT-QX-Stop* orders placed after QX-ALL.
                    // ATM stop names are exactly Stop1..Stop9 (length 5, IsDigit guard).
                    // After QX-ALL follower has PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3, PTT-QX-Stop4.
                    // State guard (Working||Accepted||ChangeSubmitted) already handles both sets.
                    bool isBeStop = o.Name != null
                        && (   (o.Name.StartsWith("Stop", StringComparison.Ordinal)
                                && o.Name.Length == 5
                                && char.IsDigit(o.Name[4]))
                             || o.Name.StartsWith("PTT-QX-Stop", StringComparison.Ordinal));
                    if (isBeStop)
                    {
                        o.StopPriceChanged = newStop;
                        beSt.Add(o);
                    }
```

Net line delta: +5 lines (4 comment lines + 1 bool line replacing original 4-condition inline if).
All surrounding code (L2764-2792: DIAG dump, acc.Change call, StatusUpdate, early return) unchanged.

---

#### 6. xUnit [Fact] Names

T1 itself produces no new test file. The tests validating T1 behavior are written in T3:
- `StopNameGuard_PttQxStop_Matches`
- `StopNameGuard_PttQxStop4_Matches`
- `StopNameGuard_AtmStop1_Matches` (regression guard for existing ATM path)
- `StopNameGuard_AtmStop9_Matches` (regression guard for existing ATM path)
- `StopNameGuard_StopMarket_Rejected` (negative case)

T3 MUST be written after T1 is committed. See T3 for full [Fact] list.

---

#### 7. JS Rule Constraints

| Rule | Application to T1 |
|------|--------------------|
| JS-021 | No lock() introduced. The `bool isBeStop` local is a stack allocation inside an existing loop. PASS. |
| JS-001 | No throw statement introduced. PASS. |
| JS-002 | No null return introduced. isBeStop bool is not nullable. PASS. |
| JS-033 | No async void introduced. PASS. |
| JS-036 | isBeStop is a `bool` stack local -- zero heap allocation. PASS. |
| JS-066 | CYC of MoveStopToBreakEven: +0 net. The inline `if` (1 branch) becomes `bool isBeStop` (0 branches in assignment) + `if (isBeStop)` (1 branch). Branch count is identical. PASS. |
| ASCII-only | All added comment text is ASCII-only. No Unicode, no curly quotes. PASS. |

---

#### 8. 7-Scan Checklist (Engineer Contract)

```
[ ] Scan 1 -- lock(): grep -r "lock(" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 2 -- async void: grep -rn "async void " src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 3 -- throw in hot path: grep -rn "throw new" src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 4 -- CYC <= 8: python scripts/complexity_audit.py -> MoveStopToBreakEven modified block <= 8
[ ] Scan 5 -- ASCII-only: grep -Prn "[^\x00-\x7F]" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 6 -- xUnit only (N/A for T1, applies to T3): skip for this ticket
[ ] Scan 7 -- build: dotnet build src/PropTraderTools/ -> 0 errors, 0 warnings
```

---

#### 9. Post-Implementation Steps

After completing T1 + T4 edits in the same file edit session:

```powershell
powershell -File .\scripts\sync-ptt-to-nt8.ps1
git add src/PropTraderTools/
git commit -m "fix(ptt): DW-B86 extend stop name guard for PTT-QX-Stop* + DW-T4 comment"
```

SIM gate (mandatory before T3):
```powershell
# Path B SIM test: QX-ALL then BE-ALL, 3 cycles, verify stops=N > 0 in Output tab for each follower.
# PASS: "[BE] DW-B84-01 acc.Change() SimXXX stops=N newStop=X" with N > 0 for each follower.
# FAIL: stops=0 or [BE-DIAG-F] fires for any follower.
```

---

### T4: DW-T4 -- TryReplacePttBeBrackets Leader-Only Guard Verification

**ID**: T4
**Title**: TryReplacePttBeBrackets -- verify structural unreachability from follower path and document it
**Priority**: P2
**File**: src/PropTraderTools/CopyEngine.cs
**Lines**: L1819-L1820 (comment insertion above method signature)

> **Engineering note**: T4 is implemented in the SAME commit as T1 (both modify CopyEngine.cs).
> Write both T1 and T4 changes in one file edit, one build, one commit.

---

#### 1. Spec Requirement IDs

- DW-T4 (architecture plan Section B T4, analysis item)
- DW-B84-01 (follower early return at L2791 -- structural basis of the guarantee)

---

#### 2. Problem Statement

The retry subsystem (TryReplacePttBeBrackets, L1820-1850) fires when an order named
"PTT-BE-Stop-*" is cancelled (call site at L964-965 in OnOrderUpdate). It must never
fire for a follower account. The structural guarantee exists but is not documented at
the method entry, creating future confusion risk. A 2-line comment closes this gap.

---

#### 3. Acceptance Criteria

1. Engineer confirms via source read that TryReplacePttBeBrackets is triggered only when
   an order named "PTT-BE-Stop-*" is cancelled (call site: L964-965).
2. Engineer confirms PTT-BE-Stop-* orders are created exclusively in Step C of
   MoveStopToBreakEven, which is only reached for non-follower accounts (leader path),
   because the follower block takes an early return at L2791 before Step C.
3. A 2-line ASCII-only comment is added above the method body (immediately before or after
   the existing comment block at L1815-1819, before L1820 `private void TryReplacePttBeBrackets`).
4. No logic change. No change to the L1823 guard (`if (!IsFollowerAccount(...)) return;`).
   Comment-only edit. Closes as ANALYSIS-COMPLETE.

---

#### 4. Method Signatures

No new method introduced. No signature change. The existing method remains:

```csharp
private void TryReplacePttBeBrackets(Order cancelledStop)
```

---

#### 5. Before / After Code (exact line numbers)

**BEFORE** (L1819-1820 -- exact source as read 2026-08-22):

```csharp
        // CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        private void TryReplacePttBeBrackets(Order cancelledStop)
```

**AFTER** (insert 2-line comment between L1819 and L1820 -- the method signature line is unchanged):

```csharp
        // CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        // DW-T4: structurally unreachable from follower path. Followers use acc.Change() (early
        // return at follower block end, L2791) and never hold PTT-BE-Stop-* orders. No guard needed.
        private void TryReplacePttBeBrackets(Order cancelledStop)
```

Net line delta: +2 lines (comment only). Zero logic change.

---

#### 6. xUnit [Fact] Names

T4 is analysis + comment only. No tests required. No test file produced.

---

#### 7. JS Rule Constraints

| Rule | Application to T4 |
|------|--------------------|
| JS-021 | No lock() introduced. Comment-only change. PASS. |
| ASCII-only | Both added comment lines use ASCII-only characters. No Unicode, no curly quotes, no em-dash (-- two hyphens). PASS. |

---

#### 8. 7-Scan Checklist (Engineer Contract)

```
[ ] Scan 1 -- lock(): grep -r "lock(" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 2 -- async void: grep -rn "async void " src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 3 -- throw in hot path: grep -rn "throw new" src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 4 -- CYC <= 8: python scripts/complexity_audit.py -> TryReplacePttBeBrackets CYC = 5 (unchanged)
[ ] Scan 5 -- ASCII-only: grep -Prn "[^\x00-\x7F]" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 6 -- xUnit only (N/A for T4): skip for this ticket
[ ] Scan 7 -- build: dotnet build src/PropTraderTools/ -> 0 errors, 0 warnings
```

---

#### 9. Post-Implementation Steps

T4 shares the same post-implementation step as T1 (same commit):

```powershell
powershell -File .\scripts\sync-ptt-to-nt8.ps1
git add src/PropTraderTools/
git commit -m "fix(ptt): DW-B86 extend stop name guard for PTT-QX-Stop* + DW-T4 comment"
```

---

## T2 -- Session 2

---

### T2: DW-B85 Option B -- Startup Warning in DtoToRule()

**ID**: T2
**Title**: DW-B85 Option B -- Emit startup warning when follower not found in Account.All
**Priority**: P1
**File**: src/PropTraderTools/CopyEngine.cs
**Lines**: L3396-L3407 (modified) + new private static helper method near DtoToRule

---

#### 1. Spec Requirement IDs

- section-b85 (002-trade-copier-spec.html), Option B (startup warning)
- DW-B85 Option A (lazy re-resolve) -- deferred to backlog per spec, NOT implemented here

---

#### 2. Problem Statement

LoadRules() runs at TradeCopierWindow OnLoad time, which may precede full NT8 account
initialization. When a follower Account is not yet in Account.All at that moment, followers[i]
stays null silently. AllAccounts() at L2484 skips null slots with `if (acc != null)`, so the
follower is saved in XML and visible in the UI but is never dispatched to and never included
in BE-ALL, QX-ALL, or copy operations at runtime. No log line is emitted, leaving the operator
with no indication that the follower is inactive.

---

#### 3. Acceptance Criteria

1. When LoadRules() runs with a follower name not in Account.All, Output Tab 1 shows exactly:
   `[PTT-COPY] WARNING: follower '<name>' not found in Account.All at load time -- will be skipped until rule is re-applied (uncheck + re-check in panel).`
2. The warning string is ASCII-only: apostrophe is 0x27, hyphens are 0x2D 0x2D (two hyphens, not em-dash), no curly quotes, no Unicode.
3. The warning is emitted once per missing follower slot (per null entry in the followers array).
4. When all followers resolve successfully, no warning is emitted.
5. DtoToRule CYC after change: 7 (was 8 before extraction -- confirmed by Section C of architecture plan).
6. FindFollowerAccount CYC: 2.
7. The build produces 0 errors and 0 warnings after this change.

---

#### 4. Method Signatures

**Modified method** (signature unchanged):

```csharp
// CYC: 8 -> 7 after T2 extraction
private static CopyRule DtoToRule(CopyRuleDto dto)
```

**New helper method** (add as private static near DtoToRule, in the same B6 persistence region):

```csharp
// DW-B85: extracted from DtoToRule inner foreach to keep DtoToRule CYC at 7.
// Returns null (Account?) when account name is not found in Account.All.
// CYC=2: foreach(1) + if(1).
// JS-002 compliant: Account? return type makes nullability explicit end-to-end.
private static Account? FindFollowerAccount(string name)
```

---

#### 5. Before / After Code (exact line numbers)

**BEFORE** (L3396-3407 -- exact source as read 2026-08-22):

```csharp
            var followers = new Account[dto.FollowerAccountNames.Length];
            for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
            {
                foreach (var acc in Account.All)
                {
                    if (acc.Name == dto.FollowerAccountNames[i])
                    {
                        followers[i] = acc;
                        break;
                    }
                }
            }
```

**AFTER** (replace L3396-3407 with this block -- copy-paste ready):

```csharp
            var followers = new Account[dto.FollowerAccountNames.Length];
            for (int i = 0; i < dto.FollowerAccountNames.Length; i++)
            {
                followers[i] = FindFollowerAccount(dto.FollowerAccountNames[i]);
                // DW-B85 Option B: warn when follower account is not yet in Account.All at load time.
                // Workaround: uncheck + re-check the follower in the panel after NT8 finishes connecting.
                if (followers[i] == null)
                    NinjaTrader.Code.Output.Process(
                        "[PTT-COPY] WARNING: follower '" + dto.FollowerAccountNames[i]
                            + "' not found in Account.All at load time"
                            + " -- will be skipped until rule is re-applied (uncheck + re-check in panel).",
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            }
```

**New helper method** (add as private static in same B6 region, after DtoToRule closing brace):

```csharp
        // DW-B85: extracted from DtoToRule inner foreach to keep DtoToRule CYC at 7.
        // Returns null (Account?) when account name is not found in Account.All.
        // CYC=2: foreach(1) + if(1).
        // JS-002 compliant: Account? return type makes nullability explicit end-to-end.
        private static Account? FindFollowerAccount(string name)
        {
            foreach (var acc in Account.All)
            {
                if (acc.Name == name)
                    return acc;
            }
            return null;
        }
```

Net line delta in DtoToRule region: -12 lines (inner foreach+if removed) + 10 lines (call + warning block) = -2 net.
Net new lines for helper: +12 lines.
All code downstream of L3407 (multipliers block L3409-3412, atmMap block L3414-3424,
tightenTicks L3426-3428, CopyRule.Create call L3430-3431) is unchanged.

---

#### 6. xUnit [Fact] Names

T2 does not produce test coverage in T3 (DtoToRule is not in T3 scope per architecture plan).
No [Fact] methods are required for T2. The operator manually verifies the warning in Output tab
by running NinjaTrader with a follower account that has not yet connected.

---

#### 7. JS Rule Constraints

| Rule | Application to T2 |
|------|--------------------|
| JS-021 | No lock() introduced. Account.All is NT8 thread-safe. PASS. |
| JS-001 | No throw statement introduced. PASS. |
| JS-002 | FindFollowerAccount returns `Account?` (nullable annotated). The caller declares `followers[i] = FindFollowerAccount(...)` where `followers` is `Account[]` -- C# nullable analysis accepts `Account?` -> `Account?[]` element assignment. Null is then explicitly tested with `if (followers[i] == null)`. Nullability is explicit end-to-end. PASS. |
| JS-033 | No async void introduced. PASS. |
| JS-036 | No heap allocation in hot path. DtoToRule runs at load time (cold path), not in the trading loop. PASS. |
| JS-066 | DtoToRule: CYC 8 -> 7 (inner foreach+if extracted, +1 for new null warning if = net -1). FindFollowerAccount: CYC=2. Both <= 8. PASS. |
| ASCII-only | All string literals in the warning use ASCII-only characters: apostrophe 0x27, hyphen-hyphen 0x2D 0x2D. No em-dash, no curly quotes, no Unicode. Verified character-by-character in plan Section B T2. PASS. |

---

#### 8. 7-Scan Checklist (Engineer Contract)

```
[ ] Scan 1 -- lock(): grep -r "lock(" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 2 -- async void: grep -rn "async void " src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 3 -- throw in hot path: grep -rn "throw new" src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 4 -- CYC <= 8: python scripts/complexity_audit.py -> DtoToRule = 7, FindFollowerAccount = 2
[ ] Scan 5 -- ASCII-only: grep -Prn "[^\x00-\x7F]" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 6 -- xUnit only (N/A for T2): skip for this ticket
[ ] Scan 7 -- build: dotnet build src/PropTraderTools/ -> 0 errors, 0 warnings
```

---

#### 9. Post-Implementation Steps

After completing T2 edit:

```powershell
powershell -File .\scripts\sync-ptt-to-nt8.ps1
git add src/PropTraderTools/
git commit -m "fix(ptt): DW-B85 Option B startup warning when follower not in Account.All"
```

Manual verification (Output tab):
- Start NinjaTrader with a copy rule that has a follower account name that is not yet in Account.All.
- Expected: `[PTT-COPY] WARNING: follower '<name>' not found in Account.All at load time -- will be skipped...`
- Expected: Warning appears once per missing follower slot.
- Expected: No warning when all followers resolve.

---

## T3 -- Session 3

---

### T3: DW-B84 -- xUnit Tests for Follower acc.Change() Path

**ID**: T3
**Title**: DW-B84 -- Add xUnit tests for follower acc.Change() path and stop name guards
**Priority**: P1
**File**: tests/PropTraderTools.Tests/CopyEngineBreakEvenFollowerTests.cs (NEW FILE)

> **Dependency**: T1 MUST be committed and build-passing before writing T3.
> StopNameGuard_PttQxStop_Matches and StopNameGuard_PttQxStop4_Matches assert the T1 guard.

---

#### 1. Spec Requirement IDs

- section-b84 (002-trade-copier-spec.html)
- DW-B84-01/02/03 (deployed: follower acc.Change() path, followers-before-leader ordering,
  state guard -- committed at 1e0e45b0; T3 adds missing test coverage)
- DW-B86 (T1 stop name guard -- tests in T3 validate T1 fix is correct)

---

#### 2. Problem Statement

DW-B84-01/02/03 are deployed production code (3 commits, last: 1e0e45b0). The follower
acc.Change() path, the isBeStop stop name guard, the state guard (Working/Accepted/
ChangeSubmitted), and the followers-before-leader ordering are all production code with
zero xUnit test coverage. T3 closes this gap. The test file is new (does not exist yet).

---

#### 3. Acceptance Criteria

1. Test file exists at tests/PropTraderTools.Tests/CopyEngineBreakEvenFollowerTests.cs.
2. All 10 [Fact] methods pass: `dotnet test --filter CopyEngineBreakEvenFollowerTests` -> 10/10.
3. No NUnit ([Test]) or MSTest ([TestMethod]) attributes anywhere in the file.
4. All 5 coverage areas are addressed: early-return, name guard ATM, name guard QX (T1), state guard, followers-before-leader.
5. Build: `dotnet build tests/PropTraderTools.Tests/` -> 0 errors.

---

#### 4. Method Signatures

No production method signatures change. The test class to create:

```csharp
public sealed class CopyEngineBreakEvenFollowerTests
```

Engineering note on NT8 type stubs: NT8 `Order` and `Account` are concrete runtime types
with no public default constructor. The engineer MUST use the test pattern already established
in `tests/PropTraderTools.Tests/` (check existing files for stubs or factory helpers).
If no existing stub pattern is found, the guard logic under test (the `isBeStop` bool
expression and the `beStOk` state expression) must be extracted to a package-private static
method that accepts primitive inputs (string name, OrderState state) so it can be tested
without a full NT8 runtime. The [Fact] names below are the binding contract; the internal
stub/mock pattern is the engineer's decision.

---

#### 5. [Fact] Methods (10 required)

The following 10 [Fact] methods MUST exist in `CopyEngineBreakEvenFollowerTests`:

```
1. FollowerPath_EarlyReturn_SkipsStepBAndC
   Asserts: when IsFollowerAccount(acc)==true, MoveStopToBreakEven returns before
   Step B (acc.Cancel on stale orders) and Step C (acc.CreateOrder/acc.Submit) are reached.
   Mechanism: verify Step B/C order-creation methods are not called on the follower account.

2. StopNameGuard_AtmStop1_Matches
   Asserts: an order named "Stop1" (length=5, IsDigit('1')) passes the isBeStop guard.
   Assert.True(isBeStop("Stop1"))

3. StopNameGuard_AtmStop9_Matches
   Asserts: an order named "Stop9" passes the isBeStop guard.
   Assert.True(isBeStop("Stop9"))

4. StopNameGuard_PttQxStop_Matches
   Asserts: an order named "PTT-QX-Stop" passes the isBeStop guard (DW-B86 new branch).
   Assert.True(isBeStop("PTT-QX-Stop"))

5. StopNameGuard_PttQxStop4_Matches
   Asserts: an order named "PTT-QX-Stop4" passes the isBeStop guard (DW-B86 new branch).
   Assert.True(isBeStop("PTT-QX-Stop4"))

6. StopNameGuard_StopMarket_Rejected
   Asserts: an order named "StopMarket" does NOT pass the isBeStop guard (Length != 5, not IsDigit).
   Assert.False(isBeStop("StopMarket"))

7. StateGuard_Working_Accepted_ChangeSubmitted_Included
   Asserts: orders in Working, Accepted, and ChangeSubmitted states all produce beStOk==true.
   Assert.True(beStOk(OrderState.Working))
   Assert.True(beStOk(OrderState.Accepted))
   Assert.True(beStOk(OrderState.ChangeSubmitted))

8. StateGuard_CancelSubmitted_Excluded
   Asserts: an order in CancelSubmitted state produces beStOk==false (not added to beSt).
   Assert.False(beStOk(OrderState.CancelSubmitted))

9. Stops0_EmitsBeDiagFLogLine
   Asserts: when beSt.Count==0, the method emits at least one log line containing
   "[BE-DIAG-F]" for the instrument's orders on the follower account.
   Assert.Contains("[BE-DIAG-F]", capturedOutput)

10. BreakEvenOverload_FollowersRunBeforeLeader
    Asserts: BreakEven(Account leader, Instrument, int) invokes MoveStopToBreakEven
    for all non-leader accounts BEFORE invoking it for the leader account.
    Mechanism: capture invocation order via an ordered List<string> of account names.
    Assert: all follower account names appear before the leader account name in the list.
```

---

#### 6. xUnit Framework Requirements

```csharp
using Xunit;
// using Xunit.Abstractions; // optional -- for ITestOutputHelper if output capture is needed
```

**Required**: `[Fact]` attribute on all 10 test methods.
**Forbidden**:
- `using NUnit.Framework;`
- `using Microsoft.VisualStudio.TestTools.UnitTesting;`
- `[Test]` attribute (NUnit)
- `[TestMethod]` attribute (MSTest)
- `Assert.IsTrue` / `Assert.AreEqual` (MSTest/NUnit style)

**Allowed Assert methods**: `Assert.True`, `Assert.False`, `Assert.Equal`, `Assert.Contains`, `Assert.NotNull`

---

#### 7. JS Rule Constraints

| Rule | Application to T3 |
|------|--------------------|
| JS-051 | xUnit ONLY. No NUnit, no MSTest. [Fact] attribute. PASS (see forbidden list above). |
| JS-021 | No lock() in test file. Use pure-logic helpers or stubs, not synchronized shared state. PASS. |
| ASCII-only | All string literals in test file are ASCII-only. No Unicode, no curly quotes. PASS. |
| JS-001 | No throw in test methods (use Assert methods, not try/catch with re-throw). PASS. |

---

#### 8. 7-Scan Checklist (Engineer Contract)

```
[ ] Scan 1 -- lock(): grep -r "lock(" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 2 -- async void: grep -rn "async void " src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 3 -- throw in hot path: grep -rn "throw new" src/PropTraderTools/ --include="*.cs" -> 0 new results
[ ] Scan 4 -- CYC <= 8: python scripts/complexity_audit.py -> no modified production methods
[ ] Scan 5 -- ASCII-only: grep -Prn "[^\x00-\x7F]" src/PropTraderTools/ --include="*.cs" -> 0 results
[ ] Scan 6 -- xUnit only: grep -rn "using NUnit\|using MSTest\|[TestMethod]\|[Test]" tests/ -> 0 results
[ ] Scan 7 -- build: dotnet build tests/PropTraderTools.Tests/ -> 0 errors, 0 warnings
```

Test runner verification (mandatory after build):

```powershell
dotnet test tests/PropTraderTools.Tests/ --filter "FullyQualifiedName~CopyEngineBreakEvenFollowerTests" --no-build -v normal
# Expected: 10 passed, 0 failed, 0 skipped
```

Full suite regression:

```powershell
dotnet test tests/ --no-build -v minimal
# Expected: all previously passing tests still pass; 10 new tests pass
```

---

#### 9. Post-Implementation Steps

After test file is written and all 10 tests pass:

```powershell
git add tests/PropTraderTools.Tests/CopyEngineBreakEvenFollowerTests.cs
git commit -m "test(ptt): DW-B84 xUnit tests for follower acc.Change() path and stop name guards"
```

---

## Section X -- Ticket Completion Matrix

| Ticket | Priority | File(s) | Commit | Depends On | Status |
|--------|----------|---------|--------|------------|--------|
| T1 | P0 | src/PropTraderTools/CopyEngine.cs | Session 1 | none | PENDING |
| T4 | P2 | src/PropTraderTools/CopyEngine.cs | Session 1 (same as T1) | none | PENDING |
| T2 | P1 | src/PropTraderTools/CopyEngine.cs | Session 2 | none | PENDING |
| T3 | P1 | tests/PropTraderTools.Tests/CopyEngineBreakEvenFollowerTests.cs (NEW) | Session 3 | T1 | PENDING |

---

## Section Y -- Deferred Backlog (not in scope for this epic)

The following items are carried from the B42-QX-BE-01 backlog and remain open after PTT-BE-FIX:

- DW-B42-01: T_BUG_QX_BE_01 does not assert PTT-QX-T3 -- remains open
- DW-B42-02: Live NT8 F5 verification (QX->BE sequence) -- remains open
- DW-B42-03: IsPttQxTarget range extension for T4/T5 slots -- remains open
- DW-B85 Option A: lazy re-resolve in AllAccounts() -- deferred per spec; not implemented here

---

## Pre-Flight Confirmation (Architect)

| Check | T1 | T2 | T3 | T4 |
|-------|----|----|----|----|
| 10 sections present | PASS | PASS | PASS | PASS |
| 7-scan checklist present | PASS | PASS | PASS | PASS |
| Spec requirement IDs cited | PASS | PASS | PASS | PASS |
| Method signatures exact | PASS | PASS | PASS | PASS |
| Before/after with line numbers | PASS | PASS | N/A (new file) | PASS |
| [Fact] names listed | N/A | N/A | PASS (10) | N/A |
| JS rule constraints cited | PASS | PASS | PASS | PASS |
| Post-implementation steps | PASS | PASS | PASS | PASS |
| No .cs source written by architect | PASS | PASS | PASS | PASS |
| No lock() anywhere | PASS | PASS | PASS | PASS |
| No DateTime.Now | PASS | PASS | PASS | PASS |
| ASCII-only literals | PASS | PASS | PASS | PASS |
| CYC <= 8 all modified methods | PASS (+0) | PASS (8->7) | N/A | PASS (5, unchanged) |
| xUnit only (T3) | N/A | N/A | PASS | N/A |
