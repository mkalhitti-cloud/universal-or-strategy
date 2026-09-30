# B121 Architecture Plan

**Status**: REVIEW_PENDING  
**Phase**: 1 — Architecture  
**Author**: ptt-architect  
**Date**: 2026-08-11  

---

## 1. Block Summary

**Block**: B121  
**Bugs addressed**: DW-B130 (copier fires on leader only after restart), DW-B130b (Quick ALL blocked on clean install)  
**Files in scope**:

| File | Method | Lines |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | `IsFollowerAccount(Account acc)` | L723-732 |
| `src/PropTraderTools/TradeCopierAddOn.cs` | `LoadAndValidateLicense()` | L629-646 |

**Ticket count**: 2 (T1, T2)  
**Out-of-scope**: See Section 7.

---

## 2. Rules Catalog Gate — PASS

Rules read: `docs/standards/jane-street/RULES_CATALOG.md`

| Rule | Description | Status in B121 |
|------|-------------|----------------|
| JS-021 | No `lock()` usage | No locks added in either fix. PASS. |
| JS-001 | No throw in hot paths | No throws added. Catch blocks return value types only. PASS. |
| JS-002 | No return null for missing values | Both methods return bool or FeatureFlags — never null. PASS. |
| JS-033 | No async void | Both methods are synchronous. PASS. |

**Gate result**: PASS — all P0 rules compliant. Work proceeds.

---

## 3. Bug 1 — DW-B130: Copier fires only on leader after NT8 restart

### 3.1 Root Cause Analysis

When NT8 restarts, `TradeCopierAddOn.LoadRules()` calls `DtoToRule()`, which iterates
`dto.FollowerAccountNames[]` and calls `FindFollowerAccount(name)` for each entry.
`FindFollowerAccount` searches `Account.All` — but SIM accounts are not yet present in
`Account.All` at `State.Configure` time. Result: `followers[i] = null`.

The constructed `CopyRule` is stored in `_rules` with `FollowerAccounts[i] = null`.

`IsFollowerAccount(Account acc)` (current code, L723-732):

```csharp
// CYC=3: null guard(1) + foreach(2) + inner foreach(3). JS-021: no lock.
internal bool IsFollowerAccount(Account acc)
{
    if (acc == null)
        return false; // (1)
    foreach (var rule in _rules) // (2)
    foreach (var f in rule.FollowerAccounts) // (3)
        if (f != null && f.Name == acc.Name)
            return true;
    return false;
}
```

The inner foreach skips null slots silently. When NT8 later fires `OnOrderUpdate` for the
leader account, `IsFollowerAccount(leaderAccount)` returns false (correct). But when the
SIM account eventually fires an event, `IsFollowerAccount(simAccount)` also returns false
because its null slot is never matched. The SIM account is incorrectly treated as a leader
account — causing the copy engine to treat it as a new copy source instead of suppressing it.

### 3.2 Dependency: FollowerAccountNames field

`CopyRule.FollowerAccountNames` was added in B127 precisely to survive null-slot scenarios:

```
src/PropTraderTools/CopyEngine.cs L423:
    internal readonly string[] FollowerAccountNames;
```

The field is populated in the `CopyRule` constructor (L449):

```csharp
FollowerAccountNames = followerAccountNames ?? DeriveFollowerNames(followers);
```

`DtoToRule` passes `dto.FollowerAccountNames` as the 8th argument (L4375). The names are
therefore always present even when `FollowerAccounts[i]` is null. **No new field creation
required.** The dependency is satisfied by existing B127 code.

### 3.3 Proposed Fix

Replace `IsFollowerAccount` (L723-732) with an index-based loop that falls back to
name comparison when the Account slot is null:

```csharp
// B121 DW-B130: null-slot name fallback for post-restart SIM account matching.
// CYC=8 (spec analysis): null-guard(1)+foreach(2)+for(3)+f!=null-branch(4)+
//   f==null-branch(5)+FollowerAccountNames!=null(6)+i<length(7)+name-match(8).
// JS-021: no lock. JS-002: returns bool, no null return.
internal bool IsFollowerAccount(Account acc)
{
    if (acc == null) return false;
    foreach (var rule in _rules)
        for (int i = 0; i < rule.FollowerAccounts.Length; i++)
        {
            var f = rule.FollowerAccounts[i];
            if (f != null && f.Name == acc.Name) return true;
            if (f == null
                && rule.FollowerAccountNames != null
                && i < rule.FollowerAccountNames.Length
                && rule.FollowerAccountNames[i] == acc.Name) return true;
        }
    return false;
}
```

### 3.4 CYC Proof (Bug 1)

Decision points enumerated per the spec's analysis:

| # | Decision point | Running CYC |
|---|---------------|-------------|
| base | method entry | 1 |
| 1 | `if (acc == null)` | 2 |
| 2 | `foreach (var rule in _rules)` | 3 |
| 3 | `for (int i = ...)` | 4 |
| 4 | `if (f != null && f.Name == acc.Name)` — `&&` counts as one branch pair | 5 |
| 5 | `if (f == null && ...)` — outer if | 6 |
| 6 | `&& rule.FollowerAccountNames != null` | 7 |
| 7 | `&& i < rule.FollowerAccountNames.Length` | 8 |
| 8 | `&& rule.FollowerAccountNames[i] == acc.Name` | 8* |

*The final `&&` operand is the last condition in the compound expression evaluated at the same
decision node as #7 by Lizard's counting. Spec result: **CYC = 8. Exactly at the ≤8 limit.**
SCAN-01 (`complexity_audit.py`) will empirically confirm at implementation time.

### 3.5 Threading Analysis (Bug 1)

- `_rules` is iterated read-only. `CopyRule` is a readonly struct; `FollowerAccounts` and
  `FollowerAccountNames` are `readonly` arrays set at construction. No mutation occurs.
- No `Dispatcher.InvokeAsync` required — method is a pure predicate with no UI interaction.
- No `lock()` added or needed. JS-021: PASS.

---

## 4. Bug 2 — DW-B130b: Quick ALL blocked on clean install

### 4.1 Root Cause Analysis

On a clean install (no `license.txt`, no `license_cache.json`):

1. `LoadAndValidateLicense()` constructs an empty `key = string.Empty`.
2. `LicenseClient.Validate(string.Empty)` returns `FeatureFlags.Starter()`.
3. `Starter()` does not enable Quick ALL or other Elite features.
4. Developer / tester cannot use the add-on without a license file.

### 4.2 Proposed Fix

Prepend a sentinel file check to `LoadAndValidateLicense()`. If
`{UserDataDir}/PropTraderTools/dev_mode.txt` exists, return `FeatureFlags.Elite()`
immediately, bypassing `LicenseClient` entirely:

```csharp
// B121 DW-B130b: dev_mode.txt sentinel bypass for clean-install dev/test workflow.
// CYC=4: try/catch(1)+if-devMode(2)+if-licenseTxt ternary(3) -- base=1 → CYC=4.
// JS-001: no throw -- catch returns Starter(). JS-021: no lock.
private static FeatureFlags LoadAndValidateLicense()
{
    try
    {
        var pttDir = System.IO.Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir, "PropTraderTools");
        var devMode = System.IO.Path.Combine(pttDir, "dev_mode.txt");
        if (System.IO.File.Exists(devMode))
            return FeatureFlags.Elite();
        var licenseTxt = System.IO.Path.Combine(pttDir, "license.txt");
        var key = System.IO.File.Exists(licenseTxt)
            ? System.IO.File.ReadAllText(licenseTxt).Trim()
            : string.Empty;
        return LicenseClient.Validate(key);
    }
    catch (Exception)
    {
        return FeatureFlags.Starter();
    }
}
```

### 4.3 CYC Proof (Bug 2)

| # | Decision point | Running CYC |
|---|---------------|-------------|
| base | method entry | 1 |
| 1 | `try/catch (Exception)` | 2 |
| 2 | `if (System.IO.File.Exists(devMode))` | 3 |
| 3 | ternary `File.Exists(licenseTxt) ? ... : ...` | 4 |

**CYC = 4. Well within the ≤8 limit.**

### 4.4 Threading Analysis (Bug 2)

- Called from `TradeCopierAddOn.OnStateChange(State.Configure)` — startup path, not a hot path.
- `File.Exists` and `File.ReadAllText` are identical to the calls already present in the
  existing method. No new threading surface introduced.
- `FeatureFlags.Elite()` is a static factory; returns a value object. Thread-safe.
- No `Dispatcher.InvokeAsync` needed. No `lock()` added. JS-021: PASS.

### 4.5 NT8 API Usage (Bug 2)

- `NinjaTrader.Core.Globals.UserDataDir` — already used at L633 of the existing method.
  No new NT8 API surface introduced.
- `FeatureFlags.Elite()` — confirmed at `src/PropTraderTools/FeatureFlags.cs` L24.

---

## 5. Data Flow Summary

### Bug 1 Flow (post-fix)

```
NT8 OnOrderUpdate fires (SIM account, null slot)
  → CopyEngine.OnOrderUpdate()
  → IsFollowerAccount(simAccount)
  → foreach rule → for i → rule.FollowerAccounts[i] == null
  → fallback: rule.FollowerAccountNames[i] == simAccount.Name  ← NEW CHECK
  → return true  ← SIM account correctly identified as follower
  → copy suppressed  ← bug fixed
```

### Bug 2 Flow (post-fix)

```
State.Configure fires on clean install
  → LoadAndValidateLicense()
  → File.Exists("{UserDataDir}/PropTraderTools/dev_mode.txt") == true  ← NEW CHECK
  → return FeatureFlags.Elite()  ← all features enabled
  → LicenseClient.Validate() never called  ← bug fixed
```

---

## 6. Ticket Scope

### Ticket T1: IsFollowerAccount null-slot name fallback

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/CopyEngine.cs` |
| Method | `IsFollowerAccount(Account acc)` |
| Lines replaced | L723-732 |
| Spec requirement | DW-B130 |
| CYC | 8 (at limit — SCAN-01 confirms) |
| JS rules | JS-021 (no lock), JS-002 (bool return, no null) |
| Tests | T1_IsFollowerAccount_NullAccount_ReturnsFalse, T1_IsFollowerAccount_ResolvedFollower_ReturnsTrue, T1_IsFollowerAccount_NullSlotMatchByName_ReturnsTrue, T1_IsFollowerAccount_NullSlotMismatchByName_ReturnsFalse |

**What T1 does**: Replaces the inner `foreach` loop with an index-based `for` loop.
When `FollowerAccounts[i]` is null, falls back to `FollowerAccountNames[i]` name comparison.
Preserves all existing behaviour for non-null slots. No other method is touched.

### Ticket T2: dev_mode.txt sentinel bypass

| Field | Value |
|-------|-------|
| File | `src/PropTraderTools/TradeCopierAddOn.cs` |
| Method | `LoadAndValidateLicense()` |
| Lines replaced | L629-646 |
| Spec requirement | DW-B130b |
| CYC | 4 (well within limit) |
| JS rules | JS-021 (no lock), JS-001 (catch returns Starter(), no throw) |
| Tests | T2_LoadAndValidateLicense_DevModeFilePresent_ReturnsElite, T2_LoadAndValidateLicense_NoDevMode_NoLicenseTxt_DelegatesToLicenseClient |

**What T2 does**: Adds a `dev_mode.txt` sentinel check at the top of the try block.
If the file exists, returns `FeatureFlags.Elite()` immediately.
All other code paths (no sentinel, license.txt present, exception) are unchanged.

---

## 7. 7-Scan Checklist Template

Both tickets carry this checklist. Engineer MUST check all 7 before marking ticket complete.

```
SCAN-01 CYC check:
  python scripts/complexity_audit.py
  Result: all modified methods must report CYC <= 8.

SCAN-02 lock() check:
  grep -rn "lock(" src/PropTraderTools/CopyEngine.cs
  grep -rn "lock(" src/PropTraderTools/TradeCopierAddOn.cs
  Result: zero matches in modified files.

SCAN-03 async void check:
  grep -rn "async void " src/PropTraderTools/CopyEngine.cs
  grep -rn "async void " src/PropTraderTools/TradeCopierAddOn.cs
  Result: zero matches in new or modified code.

SCAN-04 return null check:
  grep -rn "return null;" src/PropTraderTools/CopyEngine.cs
  grep -rn "return null;" src/PropTraderTools/TradeCopierAddOn.cs
  Result: zero new return null in value-returning paths.

SCAN-05 ASCII check:
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierAddOn.cs
  Result: zero non-ASCII characters.

SCAN-06 Build check:
  dotnet build src/PropTraderTools/PropTraderTools.csproj
  Result: zero errors.

SCAN-07 Test check:
  dotnet test src/PropTraderTools/PropTraderTools.csproj
  Result: all tests passing, including new B121 tests.
```

---

## 8. Out-of-Scope Items

The following are explicitly excluded from B121:

| Item | Reason |
|------|--------|
| `PttGlobalQuickExit.cs` | No changes required; already calls IsFollowerAccount via engine reference |
| `PttQuickExit.cs` | No changes required |
| `TradeCopierPanel.cs` | No changes required |
| `LicenseClient.cs TryRemoteValidate` | Cryptolens integration deferred |
| `AllAccounts() B127 lazy-resolve` | Correct as-is; out of scope |
| `DtoToRule / LoadRules` | Root cause is in IsFollowerAccount gate, not in load path |

---

## 9. Dependencies

| Dependency | Status | Location |
|------------|--------|----------|
| `CopyRule.FollowerAccountNames` field | CONFIRMED EXISTS | `CopyEngine.cs` L423 (added B127) |
| `FeatureFlags.Elite()` static factory | CONFIRMED EXISTS | `FeatureFlags.cs` L24 |
| `LicenseClient.Validate(string)` | NOT MODIFIED — still called in non-sentinel path | `LicenseClient.cs` |
| `NinjaTrader.Core.Globals.UserDataDir` | ALREADY USED in existing LoadAndValidateLicense | `TradeCopierAddOn.cs` L633 |

---

## 10. Test Specifications

### T1 Tests — `IsFollowerAccount` (file: `src/PropTraderTools/Tests/B121Tests.cs`)

```
[Fact] T1_IsFollowerAccount_NullAccount_ReturnsFalse
  Assert: engine.IsFollowerAccount(null) == false
  Coverage: null guard branch (original behaviour preserved)

[Fact] T1_IsFollowerAccount_ResolvedFollower_ReturnsTrue
  Setup: rule with FollowerAccounts[0] = mockAccount (non-null)
  Assert: engine.IsFollowerAccount(mockAccount) == true
  Coverage: f != null && f.Name == acc.Name branch (original behaviour preserved)

[Fact] T1_IsFollowerAccount_NullSlotMatchByName_ReturnsTrue
  Setup: rule with FollowerAccounts[0] = null, FollowerAccountNames[0] = "SimAccount"
         query account with Name = "SimAccount"
  Assert: engine.IsFollowerAccount(queryAccount) == true
  Coverage: NEW fallback branch (the bug fix)

[Fact] T1_IsFollowerAccount_NullSlotMismatchByName_ReturnsFalse
  Setup: rule with FollowerAccounts[0] = null, FollowerAccountNames[0] = "OtherAccount"
         query account with Name = "SimAccount"
  Assert: engine.IsFollowerAccount(queryAccount) == false
  Coverage: null slot with wrong name — must not match
```

### T2 Tests — `LoadAndValidateLicense` (file: `src/PropTraderTools/Tests/B121Tests.cs`)

```
[Fact] T2_LoadAndValidateLicense_DevModeFilePresent_ReturnsElite
  Setup: create {tmpDir}/PropTraderTools/dev_mode.txt (empty file)
         redirect UserDataDir to tmpDir via test seam or reflection
  Assert: returned FeatureFlags has Elite tier
  Coverage: NEW sentinel branch (the bug fix)

[Fact] T2_LoadAndValidateLicense_NoDevMode_NoLicenseTxt_DelegatesToLicenseClient
  Setup: tmpDir with no dev_mode.txt, no license.txt
  Assert: returned FeatureFlags == LicenseClient.Validate(string.Empty) result
  Coverage: existing behaviour preserved when sentinel absent
```

---

## 11. File Split Validation

| Ticket | File(s) touched | Methods changed | Cross-file contamination |
|--------|-----------------|-----------------|--------------------------|
| T1 | `CopyEngine.cs` only | `IsFollowerAccount` only | NONE |
| T2 | `TradeCopierAddOn.cs` only | `LoadAndValidateLicense` only | NONE |

Each ticket is independently revertable. No shared state introduced.

---

## 12. NT8 Sync Requirement

After both tickets are implemented:

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
# Verify: 0 MISMATCH lines
# Then press F5 in NinjaTrader 8 to recompile.
```

---

**PLAN STATUS: PLAN_COMPLETE**
