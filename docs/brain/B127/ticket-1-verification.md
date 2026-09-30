# B127 Ticket-1 Verification

**Block**: B127
**Ticket**: T1 -- Implement Option A Lazy Re-Resolve in AllAccounts()
**Defect**: DW-PTT-BE-FIX-01
**Verifier**: ptt-verifier
**Date**: 2026-08-25
**Source Reviewed**: src/PropTraderTools/CopyEngine.cs, src/PropTraderTools/Tests/B127Tests.cs

---

## Verification Result: VERIFY_PASS

---

## Scan Results (independent -- Layer 3)

All scans run independently via execute_command. Engineer Layer 2 results not trusted until confirmed.

### SCAN 1 -- lock() audit (JS-021 P0)

Command: `Select-String -Pattern "lock\(" src/PropTraderTools/CopyEngine.cs`

Result:
- Line 297: comment only -- "JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere."
- Line 330: comment only -- "ConcurrentDictionary: thread-safe without lock(). JS-021: no lock."
- Line 2030: comment only -- "CYC=5: fo null(1), price delta(2)..."
- Line 2494: comment only -- "JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove."

**0 actual lock( calls in code. All 4 matches are in comments. PASS.**

### SCAN 2 -- async void audit (JS-033 P0)

Command: `Select-String -Pattern "async void " src/PropTraderTools/CopyEngine.cs`

Result: No output. 0 matches.

**PASS.**

### SCAN 3 -- return null audit (JS-002 P0)

Command: `Select-String -Pattern "return null" src/PropTraderTools/CopyEngine.cs`

Result: Actual `return null;` at lines 1606, 2131, 2177, 3476, 3482, 3557, 4390.

Verified: None of these lines are inside AllAccounts() (lines 3419-3464) or
DeriveFollowerNames() (lines 480-488). All are pre-existing and pre-date B127.
0 new return null in B127-modified code.

**PASS.**

### SCAN 4 -- CYC verification of AllAccounts()

Source read: lines 3412-3464 of CopyEngine.cs.

Independent decision-point count:

| # | Decision Point | Line | Type |
|---|----------------|------|------|
| 1 | `if (rule == null)` | 3422 | if |
| 2 | `for (int i = 0; i < followers.Length; i++)` | 3428 | for |
| 3 | `if (acc != null)` | 3431 | if |
| 4 | `(names != null && i < names.Length) ? names[i] : null` | 3437 | ternary |
| 5 | `if (string.IsNullOrEmpty(name))` | 3438 | if |
| 6 | `if (_resolvedFollowers.TryGetValue(name, out var cached))` | 3440 | if |
| 7 | `if (resolved != null)` | 3446 | if |

**Total CYC = 7. 7 <= 8. PASS.**

### SCAN 5 -- xUnit-only audit

Command: `Select-String -Pattern "using Xunit" src/PropTraderTools/Tests/B127Tests.cs`
Result: Line 12: `using Xunit;`

Command: `Select-String -Pattern "using NUnit|using Microsoft.VisualStudio.TestTools" src/PropTraderTools/Tests/B127Tests.cs`
Result: No output. 0 matches.

**xUnit present. No NUnit or MSTest. PASS.**

### SCAN 6 -- ASCII-only audit

Command: `Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs`
Result: No output. 0 non-ASCII characters.

Command: `Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/Tests/B127Tests.cs`
Result: No output. 0 non-ASCII characters.

**PASS.**

### SCAN 7 -- dotnet build

Command: `dotnet build src/PropTraderTools/PropTraderTools.csproj`

Result:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.19
```

**PASS. 0 errors, 0 warnings.**

---

## Acceptance Criteria (V1-V13)

| Criterion | Requirement | Source Evidence | Result |
|-----------|-------------|-----------------|--------|
| V1 | `CopyRule.FollowerAccountNames` field exists as `internal readonly string[]` | CopyEngine.cs line 423: `internal readonly string[] FollowerAccountNames;` | PASS |
| V2 | `CopyRule.Create()` has 8th optional param `followerAccountNames = null` | CopyEngine.cs line 463: `string[] followerAccountNames = null  // NEW B127: 8th optional param` | PASS |
| V3 | `DeriveFollowerNames()` helper exists as `private static` inside `CopyRule` struct | CopyEngine.cs line 480: `private static string[] DeriveFollowerNames(Account[] followers)` (inside struct closing brace at line 489) | PASS |
| V4 | `_resolvedFollowers ConcurrentDictionary<string, Account>` field exists on CopyEngine | CopyEngine.cs lines 204-205: `private readonly ConcurrentDictionary<string, Account> _resolvedFollowers = new ConcurrentDictionary<string, Account>(StringComparer.Ordinal);` | PASS |
| V5 | `AllAccounts()` implements lazy re-resolve (not just null skip) | CopyEngine.cs lines 3436-3463: full lazy path with TryGetValue, FindFollowerAccount, TryAdd, and two Output.Process calls | PASS |
| V6 | `AllAccounts()` is `internal` (not `private`) for test access | CopyEngine.cs line 3419: `internal IEnumerable<Account> AllAccounts(Instrument instrument)` | PASS |
| V7 | `DtoToRule` passes `dto.FollowerAccountNames` as 8th arg | CopyEngine.cs line 4375: `dto.FollowerAccountNames  // B127: preserve original names (covers null-account slots)` | PASS |
| V8 | `SetRuleEnabled` passes `r.FollowerAccountNames` as 8th arg | CopyEngine.cs line 1151: `r.FollowerAccountNames  // B127: preserve names through enabled/disabled rebuild` | PASS |
| V9 | `SetFollowerMultiplier` passes `r.FollowerAccountNames` as 8th arg | CopyEngine.cs line 1228: `r.FollowerAccountNames  // B127: preserve names through multiplier rebuild` | PASS |
| V10 | `SetAtmMode` passes `r.FollowerAccountNames` as 8th arg | CopyEngine.cs line 2854: `r.FollowerAccountNames  // B127: preserve names through ATM mode rebuild` | PASS |
| V11 | `LoadRules()` calls `_resolvedFollowers.Clear()` immediately after `_rules = new ConcurrentBag` | CopyEngine.cs lines 4440-4441: `_rules = new ConcurrentBag<CopyRule>();` then `_resolvedFollowers.Clear();` in sequence | PASS |
| V12 | `B127Tests.cs` has 3 `[Fact]` tests | B127Tests.cs: T1_CopyRule_FollowerAccountNames_DerivedFromAccounts_WhenNotExplicitlySupplied, T2_CopyRule_FollowerAccountNames_PreservesExplicitNames_CoveringNullSlots, T3_AllAccounts_IsInternalInstanceMethod_ReturningIEnumerableAccount | PASS |
| V13 | All 7 scans pass | See Scan Results section above | PASS |

**All 13 acceptance criteria: PASS.**

---

## Backward Compatibility Gate

- `AddRule(3-arg)` at line 1167: `CopyRule.Create(instrument, master, followers)` -- 3 args, 8th optional defaults to null, ctor derives names. No source edit needed.
- `AddRule(5-arg)` at line 1195: `CopyRule.Create(instrument, master, followers, true, multipliers, atmMap)` -- 6 args, 8th optional defaults to null. No source edit needed.
- Build succeeded with 0 errors -- backward compat confirmed by compiler.

---

## PropTraderTools.csproj Inclusion Check

PropTraderTools.csproj line 154: `<Compile Include="Tests\B127Tests.cs" />` -- present and active (no `Condition="false"`).

**B127Tests.cs is included in the build. PASS.**

---

## DNA Rules Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 -- No lock() | SCAN 1: 0 code lock() calls; ConcurrentDictionary.TryGetValue + TryAdd used throughout | PASS |
| JS-001 -- No throw in hot paths | AllAccounts() and DeriveFollowerNames() contain zero throw statements; all error paths use yield break / continue / Output.Process | PASS |
| JS-002 -- No null yielded | AllAccounts() null slots are either resolved to non-null Account or skipped; DeriveFollowerNames() returns Array.Empty<string>() not null | PASS |
| JS-008 -- readonly struct fields | FollowerAccountNames is `internal readonly string[]` on `internal readonly struct CopyRule` | PASS |
| JS-025 -- Lock-free data structures | _resolvedFollowers uses ConcurrentDictionary (lock-free). No plain Dictionary added at engine level | PASS |
| CYC <= 8 | AllAccounts() CYC=7, DeriveFollowerNames() CYC=2 | PASS |
| NT8 API | ConcurrentDictionary available since .NET 4.0; confirmed in .NET 4.8 runtime. No forbidden NT8 APIs (no async in hot path, no Account.All outside Loaded handler in new code) | PASS |

---

## Engineer Report Comparison

Engineer Layer 2 scan results vs Verifier Layer 3 independent scans:

| Scan | Engineer Claim | Verifier Finding | Match? |
|------|----------------|-----------------|--------|
| SCAN 1 | "All matches in comments only, 0 violations" | 4 comment matches, 0 code lock() | YES |
| SCAN 2 | "No output. 0 matches." | No output. 0 matches. | YES |
| SCAN 3 | "Pre-existing return null at lines 1606, 2131, 2177, 3476, 3482, 3557, 4390 only. 0 new." | Same exact lines. 0 new in B127 code. | YES |
| SCAN 4 | "CYC=7" with 7 decision points listed | Independent count = 7, same points | YES |
| SCAN 5 | "Line 12: using Xunit; present. 0 NUnit/MSTest" | Line 12 confirmed. 0 NUnit/MSTest. | YES |
| SCAN 6 | "No output on both commands. 0 non-ASCII." | No output on both. 0 non-ASCII. | YES |
| SCAN 7 | "Build succeeded. 0 Warning(s) 0 Error(s)." | Build succeeded. 0 Warning(s) 0 Error(s). | YES |

**No discrepancies between engineer Layer 2 and verifier Layer 3.**

---

## Verifier Notes

1. **Test seam approach (c) is appropriate**: The engineer used reflection and CopyRule.Create() observable behavior. Since Account.All is an NT8 API unavailable in the MSBuild test runtime, full integration testing of the lazy-resolve path is not possible without NT8. Ticket Step 12 explicitly lists option (c) as acceptable. T1 and T2 exercise the `FollowerAccountNames` field populated correctly (the struct contract). T3 verifies the method is `internal`, is an instance method, and returns `IEnumerable<Account>` -- confirming the access modifier change from ticket Step 7.

2. **Test names differ from architecture plan G.Test-1/2/3 names**: The architecture plan (section G) proposed test names (`AllAccounts_ReturnsResolvedFollower_WhenAccountPresentAtLoadTime`, etc.) while the engineer used different names (`T1_CopyRule_FollowerAccountNames_DerivedFromAccounts_WhenNotExplicitlySupplied`, etc.). The ticket Step 12 permits the engineer to adapt the test approach to the available seam. The observable behavior covered is: (T1) backward-compat DeriveFollowerNames path, (T2) explicit names preserved through 8th arg, (T3) AllAccounts() is internal IEnumerable. This is a minor deviation from the plan's test naming but the ticket spec is the authoritative contract, not the plan. The ticket names these tests by approach letter, not by specific name. No violation.

3. **StringComparer.Ordinal on _resolvedFollowers**: Ticket (Step 5) and reviewer note 3 both specify `StringComparer.Ordinal`. Source confirms it at line 205. This is a correctness improvement over the plan's default constructor. Compliant.

4. **_resolvedFollowers.Clear() placement**: Ticket Step 6 specifies "immediately after `_rules = new ConcurrentBag<CopyRule>()`". Verified at lines 4440-4441 -- the Clear() call is on the very next line, with no intervening statements. Compliant.

5. **FindFollowerAccount remains private static**: Reviewer note 4 specifies DtoToRule access modifier not changed. Confirmed: `private static Account? FindFollowerAccount(string name)` at line 4383. Compliant.

---

*Verification complete. Status: VERIFY_PASS.*
*Next phase: ptt-plan-reviewer may use this report for cross-file coherence check.*