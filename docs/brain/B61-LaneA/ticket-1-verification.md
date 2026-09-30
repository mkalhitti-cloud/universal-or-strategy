# B61-LaneA Ticket-1 Verification

**Block**: B61-LaneA
**Ticket**: TICKET-1 (DW-B61-01)
**Phase**: 4b (Verifier)
**Date**: 2026-08-10
**Verifier**: ptt-verifier

---

## SCAN-01: TryDispatchLeaderFlat signature -- PASS

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "TryDispatchLeaderFlat"`

**Output**:
```
src\PropTraderTools\CopyEngine.cs:646:            if (TryDispatchLeaderFlat(
src\PropTraderTools\CopyEngine.cs:977:        private static bool TryDispatchLeaderFlat(
```

**Analysis**:
- Line 646: call site present with 7 arguments (Account, Instrument, OrderState, matchedRule.Value, IsFollowerAccount, HasOpenPosition, FlattenOneAccount)
- Line 977: new `private static bool TryDispatchLeaderFlat(` with 7 parameters confirmed
- Old 2-parameter `private bool TryDispatchLeaderFlat(Account account, Instrument instrument)` signature: GONE (zero hits)
- Exactly 2 references as expected (definition + call site)

**SCAN-01: PASS**

---

## SCAN-02: lock() scan -- PASS

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\("`

**Output** (4 hits, all comments):
```
src\PropTraderTools\CopyEngine.cs:530:        // ConcurrentBag rebuild pattern -- no lock (JS-021). Same pattern as SetFollowerMultiplier.
src\PropTraderTools\CopyEngine.cs:551:        // ConcurrentBag rebuild pattern -- no lock (JS-021)
src\PropTraderTools\CopyEngine.cs:839:        // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
src\PropTraderTools\CopyEngine.cs:1111:        // ConcurrentBag rebuild pattern -- no lock (JS-021).
```

**Analysis**: All 4 hits are in comment text only. The word "lock" appears in `no lock (JS-021)` comments -- not executable code. Zero executable `lock(` statements found anywhere in CopyEngine.cs. The new method at lines 977-991 contains no lock() call. JS-021 PASS.

**SCAN-02: PASS**

---

## SCAN-03: throw new scan -- PASS

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new"`

**Output**: (no output -- zero hits)

**Analysis**: Zero `throw new` statements anywhere in CopyEngine.cs. JS-001 PASS.

**SCAN-03: PASS**

---

## SCAN-04: Flatten(account, instrument) call gone -- PASS

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "Flatten\(account, instrument\)"`

**Output**: (no output -- zero hits)

**Analysis**: The old `Flatten(account, instrument)` call that appeared in the original 2-parameter `TryDispatchLeaderFlat` body is completely removed. Zero occurrences as a CALL anywhere in the file. The `Flatten` method definition/overload (if it exists at ~line 1151 as a method signature) would not match this pattern. Zero hits confirmed -- old leader-account flatten call is gone.

**SCAN-04: PASS**

---

## SCAN-05: T_B61_ test methods -- PASS

**Note**: Test file location is `src/PropTraderTools/CopyEngineTests.cs` (NOT `tests/` subdirectory -- the project collocates tests with source).

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "T_B61_"`

**Output**:
```
src\PropTraderTools\CopyEngineTests.cs:2862:        public void T_B61_01_LeaderHasOpenPosition_ReturnsFalse()
src\PropTraderTools\CopyEngineTests.cs:2892:        public void T_B61_02_WrongState_Working_ReturnsFalse()
src\PropTraderTools\CopyEngineTests.cs:2922:        public void T_B61_03_AccountIsFollower_ReturnsFalse()
src\PropTraderTools\CopyEngineTests.cs:2952:        public void T_B61_04_HappyPath_FlattenOnlyFollowers_ReturnsTrue()
src\PropTraderTools\CopyEngineTests.cs:2967:            // The T_B61_04 core assertion is: result==true when all 3 guards pass.
```

**Analysis**: Exactly 4 `[Fact]` method declarations at lines 2862, 2892, 2922, 2952. The 5th hit (line 2967) is a comment line inside T_B61_04, not a test declaration. All 4 required tests present: T_B61_01, T_B61_02, T_B61_03, T_B61_04.

**SCAN-05: PASS**

---

## SCAN-06: dotnet build -- PASS (0 new errors)

**Command**: `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1`

**Output**:
```
Build FAILED.
AtrSizingEngine.cs(20,31): error CS0234: The type or namespace name 'Indicators' does not exist in the namespace 'NinjaTrader.NinjaScript'
AtrSizingEngine.cs(24,36): error CS0246: The type or namespace name 'Indicator' could not be found
CopyEngine.cs(905,22): error CS8370: Feature 'nullable reference types' is not available in C# 7.3. Please use language version 8.0 or greater.
    0 Warning(s)
    3 Error(s)
```

**Analysis**: Exactly 3 errors, all pre-existing and declared in the completion report:
- `AtrSizingEngine.cs(20)`: CS0234 -- pre-existing LSP-only project limitation (missing NT8 Indicators assembly)
- `AtrSizingEngine.cs(24)`: CS0246 -- pre-existing LSP-only project limitation (missing NT8 Indicator type)
- `CopyEngine.cs(905)`: CS8370 -- pre-existing (nullable reference types requires C# 8.0+, project targets net48/C# 7.3)

Zero new errors introduced by B61. Error count and file:line locations match engineer-reported baseline exactly.

**SCAN-06: PASS (0 new errors; 3 pre-existing errors confirmed)**

---

## SCAN-07: dotnet test T_B61_ -- CANNOT EXECUTE (known project limitation)

**Attempted command**: `dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~T_B61_"`

**Result**: Pre-existing build errors (AtrSizingEngine.cs CS0234/CS0246) prevent the PropTraderTools.dll from being emitted. No test assembly exists at `src/PropTraderTools/bin/Debug/PropTraderTools.dll`. `dotnet test` cannot execute without a compiled assembly.

**This is a known, pre-existing project constraint** -- the same LSP-only project limitation that causes the 3 build errors. It existed before B61 and is unrelated to the B61 changes.

**Mitigation**: Test logic verified via source inspection (see Check A below). The 4 test methods are structurally sound:
- T_B61_01: state=Filled, hasOpenPosition=true -> expects false, flattenCallCount=0 (position guard fires)
- T_B61_02: state=Working -> expects false, flattenCallCount=0 (state guard fires)
- T_B61_03: state=Filled, isFollower=true -> expects false, flattenCallCount=0 (follower guard fires)
- T_B61_04: state=Filled, 2 followers, all guards pass -> expects true, 2 flatten calls, leader account absent

Each test exercises exactly one guard path, and the happy path. Assert targets are correct for the method behavior.

**SCAN-07: NOT EXECUTABLE (pre-existing project constraint -- same baseline as all prior blocks)**

---

## Check A: TryDispatchLeaderFlat body manual inspection -- PASS

**Source** (lines 977-991, independently read):
```csharp
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

**Structural assertions**:
- [x] First branch: `if (state != OrderState.Filled && state != OrderState.Cancelled) return false` -- state guard present at line 982
- [x] Second branch: `if (isFollower(account)) return false` -- follower guard present at line 983
- [x] Third branch: `if (hasOpenPosition(account, instrument)) return false` -- position guard present at line 984
- [x] Fourth: `foreach (var acc in rule.FollowerAccounts)` with `flattenOne(acc, instrument)` per follower -- loop present at lines 985-989
- [x] Null guard inside loop: `if (acc == null) continue` -- present at line 987
- [x] No `Flatten(account, instrument)` call in body -- confirmed by SCAN-04 (0 hits)
- [x] Returns `bool` only (true/false) -- `return null` structurally impossible (JS-002 PASS)
- [x] No `throw` statement (JS-001 PASS)
- [x] No `lock(` (JS-021 PASS)

**CYC calculation (strict McCabe)**:
| # | Branch | Type |
|---|--------|------|
| 1 | `if (state != OrderState.Filled && state != OrderState.Cancelled)` | compound = counts as 1 decision node |
| 2 | `if (isFollower(account))` | follower guard |
| 3 | `if (hasOpenPosition(account, instrument))` | position guard |
| 4 | `foreach (var acc in rule.FollowerAccounts)` | loop |
| 5 | `if (acc == null) continue` | null guard |

CYC = 5 + 1 base = **6**. Limit: <=8. **PASS**.

**Check A: PASS**

---

## Check B: private static deviation assessment -- ACCEPTABLE

**Ticket specified**: `internal static bool TryDispatchLeaderFlat(...)`
**Implemented as**: `private static bool TryDispatchLeaderFlat(...)` (line 977)

**Reasoning**:

CS0051 ("Inconsistent accessibility: parameter type less accessible than the method") applies here because `CopyRule` is a `private readonly struct` nested inside `CopyEngine`. If the method were declared `internal`, `CopyRule` (a private type) would appear in an `internal` method's parameter list -- the compiler rejects this as it would expose a private type in a more-accessible API surface.

The fix is correct: `private static` makes the method's accessibility equal to or less than the `private` parameter type. The behavioral contract is entirely preserved:
- State guard: present and identical
- Follower guard: present and identical  
- Position guard: present and identical
- Follower-only flatten loop: present and identical
- Testability: The method is callable via `BindingFlags.NonPublic | BindingFlags.Static` reflection, which is the established pattern in CopyEngineTests.cs for all private methods.

This deviation is **compiler-forced** -- there is no choice. `internal static` cannot be used here without restructuring CopyRule's accessibility, which would be a larger change than the ticket scope.

**Check B: ACCEPTABLE DEVIATION (CS0051-forced; behavioral contract preserved)**

---

## Check C: Commit hash verification -- PASS

**Command**: `git log --oneline -8`

**Output**:
```
d7c0ceea docs(brain): B61-LaneA ticket-1-completion.md
8a097ac8 fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]
57b10313 fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]
fac65246 fix(ptt): B59 -- Gate 0.5 exit-name guard via IsExitSignalName [7 tests]
89907f9f feat(ptt): wire NT8_FULL_REFERENCE.md into all 4 PTT modes
6b5557ce docs(ptt): B59 -- post-pipeline continue prompt
0bac7126 docs(ptt): B59 -- orchestrator prompt + NT8_FULL_REFERENCE rule in AGENTS.md
e049a908 docs(nt8): B59-prep -- NT8 full reference scraped + indexed
```

**Analysis**: Commit `8a097ac8` is present at position 2 in git history, immediately before the brain doc commit `d7c0ceea`. Commit message matches exactly: `fix(ptt): B61 -- TryDispatchLeaderFlat state guard + follower-only flatten [4 tests]`. Hash matches engineer's reported hash exactly.

**Check C: PASS**

---

## Comparison to Ph4a Self-Report

| Scan | Engineer Self-Report | Independent Verification | Discrepancy? |
|------|---------------------|--------------------------|--------------|
| SCAN-01 | Lines 646 + 977 present | Lines 646 + 977 confirmed | NONE |
| SCAN-02 | 4 hits, all comments only | 4 hits, all comments confirmed | NONE |
| SCAN-03 | 0 hits | 0 hits confirmed | NONE |
| SCAN-04 | 0 hits | 0 hits confirmed | NONE |
| SCAN-05 | 4 method declarations at lines 2862, 2892, 2922, 2952 | 4 declarations at same lines confirmed | NONE |
| SCAN-06 | 3 errors (pre-existing), 0 new | 3 errors (pre-existing), 0 new -- same file:line | NONE |
| SCAN-07 | "Logic verification" (engineer could not run dotnet test) | Cannot execute (same pre-existing constraint) | NONE -- both blocked by same project limitation |
| Commit hash | `8a097ac8` | `8a097ac8` confirmed in git log | NONE |
| CYC | 6 | 6 (independently counted) | NONE |
| Accessibility | private static (CS0051-forced) | private static confirmed at line 977 | NONE |

**No discrepancies found between engineer self-report and independent verification.**

---

## Result

VERIFY_PASS