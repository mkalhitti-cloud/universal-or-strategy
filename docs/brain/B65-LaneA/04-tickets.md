# B65-LaneA Tickets

**Block**: B65-LaneA
**Phase**: 3 (Ticket Generation)
**Written by**: ptt-architect
**Date**: 2026-08-12
**Input**: docs/brain/B65-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Review gate**: docs/brain/B65-LaneA/02-plan-review.md — REVIEW_PASS confirmed, zero violations

---

## Ticket 1 of 1

---

### TICKET HEADER

- **Ticket ID**: B65-T1
- **Block**: B65
- **Title**: Post-fill leader close propagation via IsNativeExitName
- **Spec Req IDs**: DW-B65-01 (= DW-B60-01)
- **Related**: NT8_FULL_REFERENCE.md line 1721 (position race), lines 844-845 (Order.Name semantics)
- **Reviewer Gate**: REVIEW_PASS confirmed — docs/brain/B65-LaneA/02-plan-review.md
- **Engineer reads BEFORE implementing**:
  - docs/brain/B65-LaneA/02-architecture-plan.md (full)
  - docs/brain/B65-LaneA/04-ticket-review.md (if present, check for TICKET_REVIEW_PASS)
- **Status**: PENDING

---

### DEFECT SUMMARY

**DW-B65-01**: When a leader's close order fills, `OnOrderUpdate` fires immediately. However, NT8 does not update position state until the next `OnBarUpdate()` after the fill (NT8_FULL_REFERENCE.md line 1721). `TryDispatchLeaderFlat` guard (3) calls `hasOpenPosition(account, instrument)` — which returns `true` (stale) at this moment — and returns `false`, silently dropping the follower flatten. Observed live 2026-08-12: 18-second gap between leader flat and follower close.

**Fix**: Add `IsNativeExitName` helper. When the filled order has a native NT8 exit name (Close / Flatten / Rev* / Exit*), bypass guard (3) entirely and dispatch the follower flatten unconditionally.

---

### FILES CHANGED

| # | File | Change type |
|---|------|-------------|
| 1 | `src/PropTraderTools/CopyEngine.cs` | Insert `IsNativeExitName` + modify `TryDispatchLeaderFlat` + update call site |
| 2 | `src/PropTraderTools/CopyEngineTests.cs` | Update 5 B61 object[] invocations + insert T_B65_01-09 |

**No other files are touched.**

---

### METHOD SIGNATURES

#### New method (CopyEngine.cs)

```csharp
internal static bool IsNativeExitName(string name)
```

- Visibility: `internal static` (directly testable from test project in same assembly)
- Returns: `bool` (never null — JS-002 compliant)
- CYC: 6 (1 base + 5 branches: null, "Close", "Flatten", Rev-prefix, Exit-prefix) — within JS ≤8 limit
- No lock(), no throw, no return null

#### Modified method (CopyEngine.cs)

```csharp
private static bool TryDispatchLeaderFlat(
    Account account, Instrument instrument, OrderState state, string orderName,
    CopyRule rule,
    Func<Account, bool> isFollower,
    Func<Account, Instrument, bool> hasOpenPosition,
    Action<Account, Instrument> flattenOne)
```

- Change: `string orderName` added as 4th parameter (after `state`, before `rule`)
- CYC: 7 (strict McCabe) — within JS ≤8 limit
- No lock(), no throw, no return null

---

### CHANGE 1 — Add IsNativeExitName helper to CopyEngine.cs

**Insert position**: After the closing brace of `IsExitSignalName`.

Current source confirms:
- `IsExitSignalName` body runs lines 749-758
- Closing brace is at **line 758** (`        }`)
- Lines 759-761 are blank lines
- The `// --- B7-F0: Bracket mirroring methods ---` comment follows at line 762

The engineer MUST read the current line number of `return false;` at the end of
`IsExitSignalName` immediately before inserting, because prior block insertions can shift
line numbers. Find the closing brace of `IsExitSignalName` and insert the new method
immediately after it (before the blank lines).

**Code to insert** (insert as a new block after line 758, i.e. before the blank line at 759):

```csharp

        // B65 T1: IsNativeExitName -- CYC=6. Returns true for NT8 built-in exit order names ONLY.
        // Distinct from IsExitSignalName: does NOT cover PTT- prefixed signals.
        // Rationale: Only native NT8 exits (Close/Flatten/Rev/Exit) can arrive in OnOrderUpdate
        // while the leader position has not yet updated -- see NT8_FULL_REFERENCE.md line 1721:
        //   "Changes to positions will not be reflected till at least the next OnBarUpdate() event
        //    after an order fill."
        // For these names, bypass the hasOpenPosition guard in TryDispatchLeaderFlat to avoid
        // the position-race and propagate the close immediately to followers (DW-B65-01 fix).
        // NT8-VERIFY-03/04: "IsNativeExitName" confirmed NOT present in NT8 Custom codebase.
        // JS-001: no throw. JS-002: returns bool (never null). JS-021: no lock.
        internal static bool IsNativeExitName(string name)
        {
            if (name == null)                                              return false;
            if (name == "Close")                                           return true;
            if (name == "Flatten")                                         return true;
            if (name.StartsWith("Rev",  StringComparison.Ordinal))        return true;
            if (name.StartsWith("Exit", StringComparison.Ordinal))        return true;
            return false;
        }
```

**Verification**: After insert, `grep -n "IsNativeExitName" src/PropTraderTools/CopyEngine.cs`
must return exactly 2 lines: the method declaration and the reference in `TryDispatchLeaderFlat`
guard (3) (from Change 2 below).

---

### CHANGE 2 — Modify TryDispatchLeaderFlat in CopyEngine.cs

**Find** the exact text block starting with the `// CYC=4` comment through the closing brace.
Source baseline confirms it occupies **lines 1064-1084** (these line numbers will shift by
approximately +19 lines after Change 1 inserts `IsNativeExitName`; find by text, not line number).

**Find this exact text** (use text search, not line number):

```
        // CYC=4 (spec-comment) / CYC=6 (strict McCabe, counting loop + null guard):
        // (1) state guard, (2) follower guard, (3) open-position guard, (4) foreach follower.
        // Fires only on Filled or Cancelled. Skips if account is a follower.
        // Skips if leader still has an open position.
        // Loops rule.FollowerAccounts directly -- does NOT touch the leader account.
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
        private static bool TryDispatchLeaderFlat(
            Account account, Instrument instrument, OrderState state, CopyRule rule,
            Func<Account, bool> isFollower, Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne)
        {
            if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)
            if (isFollower(account)) return false;                                           // (2)
            if (hasOpenPosition(account, instrument)) return false;                          // (3)
            foreach (var acc in rule.FollowerAccounts)                                       // (4)
            {
                if (acc == null) continue;
                flattenOne(acc, instrument);
            }
            return true;
        }
```

**Replace with**:

```csharp
        // B65 T1: TryDispatchLeaderFlat -- CYC=7 (strict McCabe: loop + null guard + 4 early returns + IsNativeExitName branch).
        // (1) state guard, (2) follower guard, (3) open-position race-safe guard, (4) foreach follower.
        // Guard (3) change: bypass hasOpenPosition when orderName is a native NT8 exit.
        // Rationale: NT8_FULL_REFERENCE.md line 1721 -- position state is not updated until the next
        // OnBarUpdate() after an order fill. When leader fills a native close order (Name="Close",
        // "Flatten", "Exit*", "Rev*"), position still shows open even though the order is filled.
        // Bypassing the guard here ensures followers are flattened immediately (DW-B65-01 fix).
        // JS-021: no lock. JS-001: no throw. JS-002: no null return.
        private static bool TryDispatchLeaderFlat(
            Account account, Instrument instrument, OrderState state, string orderName,
            CopyRule rule,
            Func<Account, bool> isFollower,
            Func<Account, Instrument, bool> hasOpenPosition,
            Action<Account, Instrument> flattenOne)
        {
            if (state != OrderState.Filled && state != OrderState.Cancelled) return false; // (1)
            if (isFollower(account)) return false;                                           // (2)
            if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false; // (3)
            foreach (var acc in rule.FollowerAccounts)                                       // (4)
            {
                if (acc == null) continue;
                flattenOne(acc, instrument);
            }
            return true;
        }
```

**Guard (3) semantics** (engineer must verify):
- Non-native exit (e.g. `"BuyLimit"`, `"PTT-Copy"`): `IsNativeExitName` = `false` → `!false && hasOpenPosition(...)` = `hasOpenPosition(...)`. Behavior unchanged from B61.
- Native exit (e.g. `"Close"`, `"ExitLong"`): `IsNativeExitName` = `true` → `!true && ...` = `false` (short-circuit). Guard skipped. Flatten dispatched regardless of position state.

**CYC verification**: `python scripts/complexity_audit.py` must report `TryDispatchLeaderFlat` CYC ≤ 8.

---

### CHANGE 3 — Update call site in OnOrderUpdate (CopyEngine.cs)

**Find this exact text** (source baseline line 651-653):

```
            if (TryDispatchLeaderFlat(
                    e.Order.Account, e.Order.Instrument, e.Order.OrderState, matchedRule.Value,
                    IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```

**Replace with**:

```csharp
            if (TryDispatchLeaderFlat(
                    e.Order.Account, e.Order.Instrument, e.Order.OrderState, e.Order.Name,
                    matchedRule.Value,
                    IsFollowerAccount, HasOpenPosition, FlattenOneAccount)) return;
```

**Change**: Insert `e.Order.Name,` as the 4th argument (after `e.Order.OrderState,`), and move `matchedRule.Value,` to the next line for readability.

**Null safety**: `e.Order.Name` is never null for a filled/cancelled order. NT8 sets `Order.Name` at submission time (NT8_FULL_REFERENCE.md lines 844-845). No null guard needed.

**Sole call site**: `TryDispatchLeaderFlat` is `private static`. Only one call site exists (confirmed by plan reviewer). No other call sites to update.

---

### CHANGE 4 — Update existing B61 test invocations in CopyEngineTests.cs

The 7-parameter `TryDispatchLeaderFlat` signature expands to 8 parameters. All existing
`mi.Invoke(null, new object[] { ... })` calls with 7 elements will throw
`TargetParameterCountException` at runtime unless updated to 8 elements.

**Reflection helper compatibility**: `GetTryDispatchLeaderFlat()` at line 2856 uses `GetMethod`
by name only (no parameter type array). Since there is only one overload, it still resolves
correctly after the signature change. **No change to the helper is required.**

**The engineer MUST read CopyEngineTests.cs lines 2855-3001 to find exact current line numbers
before editing.** The line numbers below are from the source baseline read during ticket
generation and may shift from prior insertions.

There are **5 invocations** to update (4 primary tests + 1 Cancelled sub-assertion in T_B61_04):

---

#### B61 Invocation 1 — T_B61_01_LeaderHasOpenPosition_ReturnsFalse (lines ~2875-2884)

**Find**:
```
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account
                null,                                  // instrument
                OrderState.Filled,                     // state
                ruleVal,                               // rule (boxed CopyRule)
                (Func<Account, bool>)(_ => false),     // isFollower
                (Func<Account, Instrument, bool>)((_, __) => true),   // hasOpenPosition: leader still open
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Replace with**:
```csharp
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account
                null,                                  // instrument
                OrderState.Filled,                     // state
                "BuyLimit",                            // orderName (non-native: guard applies)
                ruleVal,                               // rule (boxed CopyRule)
                (Func<Account, bool>)(_ => false),     // isFollower
                (Func<Account, Instrument, bool>)((_, __) => true),   // hasOpenPosition: leader still open
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Assertion impact**: `"BuyLimit"` is non-native → `IsNativeExitName` = `false` → guard (3) still checks `hasOpenPosition` (= `true`) → still returns `false`. **Assert.False(result) unchanged.**

---

#### B61 Invocation 2 — T_B61_02_WrongState_Working_ReturnsFalse (lines ~2905-2914)

**Find**:
```
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account
                null,                                  // instrument
                OrderState.Working,                    // state (non-terminal)
                ruleVal,                               // rule
                (Func<Account, bool>)(_ => false),     // isFollower
                (Func<Account, Instrument, bool>)((_, __) => false),  // hasOpenPosition
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Replace with**:
```csharp
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account
                null,                                  // instrument
                OrderState.Working,                    // state (non-terminal)
                "BuyLimit",                            // orderName
                ruleVal,                               // rule
                (Func<Account, bool>)(_ => false),     // isFollower
                (Func<Account, Instrument, bool>)((_, __) => false),  // hasOpenPosition
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Assertion impact**: State guard (1) fires before orderName check → still returns `false`. **Assert.False(result) unchanged.**

---

#### B61 Invocation 3 — T_B61_03_AccountIsFollower_ReturnsFalse (lines ~2935-2944)

**Find**:
```
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account
                null,                                  // instrument
                OrderState.Filled,                     // state
                ruleVal,                               // rule
                (Func<Account, bool>)(_ => true),      // isFollower: account IS a follower
                (Func<Account, Instrument, bool>)((_, __) => false),  // hasOpenPosition
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Replace with**:
```csharp
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account
                null,                                  // instrument
                OrderState.Filled,                     // state
                "BuyLimit",                            // orderName (non-native)
                ruleVal,                               // rule
                (Func<Account, bool>)(_ => true),      // isFollower: account IS a follower
                (Func<Account, Instrument, bool>)((_, __) => false),  // hasOpenPosition
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Assertion impact**: Follower guard (2) fires before orderName check → still returns `false`. **Assert.False(result) unchanged.**

---

#### B61 Invocation 4 — T_B61_04_HappyPath primary (lines ~2976-2985)

**Find**:
```
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account (leader)
                null,                                  // instrument
                OrderState.Filled,                     // state (terminal)
                ruleVal,                               // rule (0 followers -- guards still exercised)
                (Func<Account, bool>)(_ => false),     // isFollower: leader is NOT a follower
                (Func<Account, Instrument, bool>)((_, __) => false),  // hasOpenPosition: leader is flat
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Replace with**:
```csharp
            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                  // account (leader)
                null,                                  // instrument
                OrderState.Filled,                     // state (terminal)
                "BuyLimit",                            // orderName (non-native exit)
                ruleVal,                               // rule (0 followers -- guards still exercised)
                (Func<Account, bool>)(_ => false),     // isFollower: leader is NOT a follower
                (Func<Account, Instrument, bool>)((_, __) => false),  // hasOpenPosition: leader is flat
                (Action<Account, Instrument>)((_, __) => flattenCallCount++) // flattenOne
            });
```

**Assertion impact**: `"BuyLimit"` non-native, `hasOpenPosition` = `false` → `!false && false` = `false` → guard (3) passes → method returns `true`. **Assert.True(result) unchanged.**

---

#### B61 Invocation 5 — T_B61_04 Cancelled sub-assertion (lines ~2993-2999)

**Find**:
```
            var resultCancelled = (bool)mi.Invoke(null, new object[]
            {
                null, null, OrderState.Cancelled, ruleVal,
                (Func<Account, bool>)(_ => false),
                (Func<Account, Instrument, bool>)((_, __) => false),
                (Action<Account, Instrument>)((_, __) => { })
            });
```

**Replace with**:
```csharp
            var resultCancelled = (bool)mi.Invoke(null, new object[]
            {
                null, null, OrderState.Cancelled, "BuyLimit", ruleVal,
                (Func<Account, bool>)(_ => false),
                (Func<Account, Instrument, bool>)((_, __) => false),
                (Action<Account, Instrument>)((_, __) => { })
            });
```

**Assertion impact**: Cancelled state passes guard (1), `"BuyLimit"` non-native + `hasOpenPosition=false` passes guard (3) → still returns `true`. **Assert.True(resultCancelled) unchanged.**

---

### CHANGE 5 — Add B65 tests to CopyEngineTests.cs

**Insert position**: After the last line of T_B61_04 (after `Assert.True(resultCancelled);` and
the closing brace of the T_B61_04 method, before the blank line and the B63 section comment).

**Source baseline confirms** the B61 region ends at line 3001 (closing brace of T_B61_04),
followed by blank lines at 3002-3003 and the B63 section header at line 3004. Insert the
B65 tests between line 3001 and line 3003 (after the T_B61_04 closing brace).

**The engineer MUST read CopyEngineTests.cs lines 2995-3010 to confirm the exact insertion
point before editing.**

**Code to insert**:

```csharp

        // ── B65 tests: IsNativeExitName + TryDispatchLeaderFlat race bypass ──
        // T_B65_01 through T_B65_07: direct IsNativeExitName unit tests.
        // T_B65_08: regression test for DW-B65-01 race bypass.
        // T_B65_09: regression guard -- non-native exit still respects position guard.

        [Fact]
        public void T_B65_01_IsNativeExitName_Null_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsNativeExitName(null));
        }

        [Fact]
        public void T_B65_02_IsNativeExitName_Close_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNativeExitName("Close"));
        }

        [Fact]
        public void T_B65_03_IsNativeExitName_Flatten_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNativeExitName("Flatten"));
        }

        [Fact]
        public void T_B65_04_IsNativeExitName_RevPrefix_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNativeExitName("RevLong"));
            Assert.True(CopyEngine.IsNativeExitName("RevShort"));
            Assert.True(CopyEngine.IsNativeExitName("Reversal"));
        }

        [Fact]
        public void T_B65_05_IsNativeExitName_ExitPrefix_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNativeExitName("ExitLong"));
            Assert.True(CopyEngine.IsNativeExitName("Exit"));
        }

        [Fact]
        public void T_B65_06_IsNativeExitName_PttPrefix_ReturnsFalse()
        {
            // "PTT-Flatten" is a PTT own signal, NOT a native NT8 exit name.
            Assert.False(CopyEngine.IsNativeExitName("PTT-Flatten"));
            Assert.False(CopyEngine.IsNativeExitName("PTT-Copy"));
        }

        [Fact]
        public void T_B65_07_IsNativeExitName_ArbitrarySignal_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsNativeExitName("BuyLimit"));
            Assert.False(CopyEngine.IsNativeExitName("MES_Long_Entry"));
            Assert.False(CopyEngine.IsNativeExitName(""));
        }

        [Fact]
        public void T_B65_08_TryDispatchLeaderFlat_NativeExitFilled_BypassesPositionRace()
        {
            // CORE B65 REGRESSION TEST (DW-B65-01):
            // orderName="Close" (native NT8 exit), state=Filled, hasOpenPosition RETURNS TRUE
            // (simulates NT8 position lag documented in NT8_FULL_REFERENCE.md line 1721).
            // Expected: flattenOne IS NOT blocked by guard (3) -- race bypassed. result = true.
            // 0 followers in rule: result=true confirms all guards passed; flattenCallCount=0
            // confirms no followers were flattened (rule has none), consistent with T_B61_04 design.
            _engine.SetEnabled(false);
            _engine.AddRule("B65T08", null, new Account[0]);
            var ruleVal = GetRuleValue(_engine, "B65T08");
            Assert.NotNull(ruleVal);
            int flattenCallCount = 0;
            var mi = GetTryDispatchLeaderFlat();
            Assert.NotNull(mi);

            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                           // account
                null,                                           // instrument
                OrderState.Filled,                              // state
                "Close",                                        // orderName (native NT8 exit)
                ruleVal,                                        // rule
                (Func<Account, bool>)(_ => false),              // isFollower: NOT a follower
                (Func<Account, Instrument, bool>)((_, __) => true),  // hasOpenPosition: TRUE (race condition)
                (Action<Account, Instrument>)((_, __) => flattenCallCount++)
            });

            Assert.True(result);          // race bypassed -- method returned true
            Assert.Equal(0, flattenCallCount); // 0 followers in rule, but guards all passed
        }

        [Fact]
        public void T_B65_09_TryDispatchLeaderFlat_NonExitFilled_LeaderHasPosition_SkipsFlat()
        {
            // Guard regression: orderName="BuyLimit" (non-native), state=Filled, hasOpenPosition=true.
            // Expected: guard (3) still fires -- result = false. flattenOne NOT called.
            // Confirms the bypass is exclusive to native NT8 exit names.
            _engine.SetEnabled(false);
            _engine.AddRule("B65T09", null, new Account[0]);
            var ruleVal = GetRuleValue(_engine, "B65T09");
            Assert.NotNull(ruleVal);
            int flattenCallCount = 0;
            var mi = GetTryDispatchLeaderFlat();
            Assert.NotNull(mi);

            var result = (bool)mi.Invoke(null, new object[]
            {
                null,                                           // account
                null,                                           // instrument
                OrderState.Filled,                              // state
                "BuyLimit",                                     // orderName (NOT a native exit)
                ruleVal,                                        // rule
                (Func<Account, bool>)(_ => false),              // isFollower
                (Func<Account, Instrument, bool>)((_, __) => true),  // hasOpenPosition: TRUE
                (Action<Account, Instrument>)((_, __) => flattenCallCount++)
            });

            Assert.False(result);         // guard (3) blocked -- non-native exit with open position
            Assert.Equal(0, flattenCallCount);
        }
```

---

### 7-SCAN CHECKLIST (MANDATORY — engineer contract)

The engineer MUST run all 7 scans and report results in `ticket-1-completion.md`.
Any scan failure is a **blocking failure** — do not commit until all 7 pass.

---

**SCAN-01 — lock() scan**
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: **zero results**. No `lock()` anywhere in `CopyEngine.cs`.
Failure action: STOP. Do not commit. Replace with Actor/Enqueue per `docs/intel/jane-street/lock-free-patterns.md`.

---

**SCAN-02 — throw scan**
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
```
Expected: **zero results in new or modified code** (`IsNativeExitName`, `TryDispatchLeaderFlat`,
and the call site in `OnOrderUpdate`). Pre-existing `throw new` lines elsewhere in the file
are pre-existing and must not be increased in count.
Failure action: STOP. Replace thrown exception with a `Result<T>` or bool return.

---

**SCAN-03 — return null scan**
```powershell
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
Expected: **zero results in `IsNativeExitName` and `TryDispatchLeaderFlat`**. Both methods
return `bool`. Pre-existing `return null` elsewhere is pre-existing.
Failure action: STOP. Both new/modified methods must return a value type only.

---

**SCAN-04 — CYC scan**
```powershell
python scripts/complexity_audit.py
```
Expected:
- `IsNativeExitName` reports CYC **≤ 8** (target: 6)
- `TryDispatchLeaderFlat` reports CYC **≤ 8** (target: 7 strict McCabe)

Failure action: If either method exceeds CYC 8, extract branches into helper methods before committing.

---

**SCAN-05 — ASCII scan**
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Expected: Results match **only the pre-existing non-ASCII lines** (lines ~398, ~499, ~1376, ~1377
per PRE-EXISTING-01/02 in deferred backlog — note these line numbers shift by ~19 after
Change 1 inserts `IsNativeExitName`). **No new non-ASCII lines introduced by B65.**
Failure action: STOP. Remove Unicode from any new line. All B65 string literals are ASCII-only.

---

**SCAN-06 — Build scan**
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: **zero errors, zero new warnings**. The 8-param `TryDispatchLeaderFlat` signature
must compile cleanly with the updated call site.
Failure action: STOP. Fix all compilation errors before committing.

---

**SCAN-07 — Test scan**
```powershell
dotnet test
```
Expected:
- **T_B65_01 through T_B65_09**: all PASS
- **T_B61_01 through T_B61_04**: all PASS (unchanged assertions, updated object[] size)
- All pre-existing tests: PASS (zero regressions)
- Zero test failures total

Failure action: STOP. Diagnose failing test, fix the implementation or test invocation, re-run.
A `TargetParameterCountException` on any B61 test means one of the 5 object[] invocations
was not updated to 8 elements.

---

### COMPLETION ARTIFACT

After all 5 changes are implemented and all 7 scans pass, the engineer writes:

**`docs/brain/B65-LaneA/ticket-1-completion.md`**

This file MUST include:
- Status: COMPLETE
- SCAN-01 through SCAN-07 results (exact command output)
- Line numbers of inserted `IsNativeExitName` method (actual post-insert lines)
- Line numbers of modified `TryDispatchLeaderFlat` method (actual post-change lines)
- Line of updated call site in `OnOrderUpdate`
- Count of B61 invocations updated (must be exactly 5)
- Count of B65 tests added (must be exactly 9: T_B65_01 through T_B65_09)
- Confirmation that `dotnet test` shows T_B61_01-04 and T_B65_01-09 all green
- Deferred items closed: DW-B65-01 (= DW-B60-01), DW-B59-02

---

*End of B65-LaneA/04-tickets.md*
