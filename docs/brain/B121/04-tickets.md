# B121 — Implementation Tickets

**Status**: TICKETS_COMPLETE  
**Phase**: 3 — Ticket Generation  
**Author**: ptt-architect  
**Plan source**: `docs/brain/B121/02-architecture-plan.md` (REVIEW_PASS)  
**Date**: 2026-08-11  
**Ticket count**: 2

---

## T1 — IsFollowerAccount null-slot name fallback

### Header

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Title | IsFollowerAccount null-slot name fallback |
| Spec requirement | DW-B130 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Method | `IsFollowerAccount(Account acc)` |
| Lines replaced | L723-732 |
| CYC (post-change) | 8 (at the ≤8 limit) |
| JS rules in scope | JS-021 (no lock), JS-002 (bool return, never null) |

---

### Pre-conditions

Before starting T1 the engineer MUST verify all of the following:

1. `src/PropTraderTools/CopyEngine.cs` exists and the project builds with 0 errors.
2. `IsFollowerAccount` is present at approximately L723-732 with the exact body shown in
   "Current code" below.
3. `CopyRule.FollowerAccountNames` field exists in `CopyEngine.cs` (added in B127 — confirmed
   at L423). No new field creation is required.
4. `dotnet test src/PropTraderTools/PropTraderTools.csproj` passes before T1 work begins.

---

### Current code (for reference — L723-732)

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

---

### Required replacement (exact — do NOT alter)

Replace the entire method body (including the comment header) with the following:

```csharp
// CYC=8: null guard(1) + foreach(2) + for-i(3) + f-not-null(4) +
//        f-null(5) + names-not-null(6) + i-in-range(7) + name-match(8).
// JS-021: no lock. B121: null-slot fallback to FollowerAccountNames[i].
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

The method signature (`internal bool IsFollowerAccount(Account acc)`) is unchanged.  
No other method in `CopyEngine.cs` is touched.

---

### Post-conditions

After completing T1:

1. `IsFollowerAccount` body exactly matches the "Required replacement" above.
2. `complexity_audit.py` reports `IsFollowerAccount` CYC ≤ 8.
3. No `lock(` appears anywhere in `CopyEngine.cs` (new or existing).
4. The file contains zero non-ASCII characters.
5. `dotnet build` succeeds with 0 errors.
6. All xUnit tests listed in the "Tests" section pass.

---

### xUnit tests — file: `src/PropTraderTools/Tests/B121Tests.cs`

Write (or append to) `B121Tests.cs`. Each test is an independent `[Fact]`.

#### T_B121_01 — null slot + name matches → true (the new fallback path)

```
Name:    T_B121_01_IsFollowerAccount_NullSlotNameMatch_ReturnsTrue
Asserts: engine.IsFollowerAccount(queryAccount) == true
Setup:   CopyRule with FollowerAccounts[0] = null,
                       FollowerAccountNames[0] = "SimAccount"
         queryAccount.Name = "SimAccount"
Coverage: NEW branch — f == null + FollowerAccountNames[i] == acc.Name
```

#### T_B121_02 — null slot + name does NOT match → false

```
Name:    T_B121_02_IsFollowerAccount_NullSlotNameMismatch_ReturnsFalse
Asserts: engine.IsFollowerAccount(queryAccount) == false
Setup:   CopyRule with FollowerAccounts[0] = null,
                       FollowerAccountNames[0] = "OtherAccount"
         queryAccount.Name = "SimAccount"
Coverage: null slot with wrong name must not produce a false positive
```

#### T_B121_03 — non-null slot + name matches → true (existing path preserved)

```
Name:    T_B121_03_IsFollowerAccount_ResolvedFollower_ReturnsTrue
Asserts: engine.IsFollowerAccount(mockAccount) == true
Setup:   CopyRule with FollowerAccounts[0] = mockAccount (non-null, Name = "Sim101")
         queryAccount.Name = "Sim101"
Coverage: f != null && f.Name == acc.Name branch (original behaviour preserved)
```

#### T_B121_04 — null acc argument → false (existing guard preserved)

```
Name:    T_B121_04_IsFollowerAccount_NullArg_ReturnsFalse
Asserts: engine.IsFollowerAccount(null) == false
Setup:   any configured engine (even empty _rules)
Coverage: top-level null guard (original behaviour preserved)
```

---

### 7-scan checklist (T1)

The engineer MUST run every scan below and record a PASS result before marking T1 complete.

| # | Scan | Command | Required result |
|---|------|---------|-----------------|
| SCAN-01 | CYC | `python scripts/complexity_audit.py` | `IsFollowerAccount` reports CYC ≤ 8 |
| SCAN-02 | lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-03 | async void | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` | 0 new results |
| SCAN-04 | return null | `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` | 0 new value-path nulls |
| SCAN-05 | ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-06 | Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors |
| SCAN-07 | Tests | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | all passing |

---

### Acceptance criteria (T1)

- [ ] `IsFollowerAccount` body exactly matches required replacement (character-for-character).
- [ ] SCAN-01 through SCAN-07 all PASS.
- [ ] T_B121_01 through T_B121_04 all pass.
- [ ] No other method in `CopyEngine.cs` was modified.
- [ ] Ticket is independently revertable (zero cross-file side effects).

---

---

## T2 — dev_mode.txt sentinel bypass in LoadAndValidateLicense

### Header

| Field | Value |
|-------|-------|
| Ticket ID | T2 |
| Title | dev_mode.txt sentinel bypass in LoadAndValidateLicense |
| Spec requirement | DW-B130b |
| File | `src/PropTraderTools/TradeCopierAddOn.cs` |
| Method | `LoadAndValidateLicense()` |
| Lines replaced | L629-646 |
| CYC (post-change) | 4 (well within ≤8 limit) |
| JS rules in scope | JS-021 (no lock), JS-001 (catch returns Starter(), no throw) |

---

### Pre-conditions

Before starting T2 the engineer MUST verify all of the following:

1. `src/PropTraderTools/TradeCopierAddOn.cs` exists and the project builds with 0 errors.
2. `LoadAndValidateLicense` is present at approximately L629-646 with the exact body shown in
   "Current code" below.
3. `FeatureFlags.Elite()` static factory exists in `src/PropTraderTools/FeatureFlags.cs` at
   approximately L24. No new method creation is required.
4. `NinjaTrader.Core.Globals.UserDataDir` is already referenced in the existing method body
   (L633). No new NT8 API surface is introduced.
5. `dotnet test src/PropTraderTools/PropTraderTools.csproj` passes before T2 work begins.

---

### Current code (for reference — L629-646)

```csharp
// BGTM-1: Read license.txt, validate via LicenseClient. CYC=2.
// JS-001: no throw -- any I/O error returns Starter().
// NT8: File.ReadAllText is safe in State.Configure (not the hot path).
private static FeatureFlags LoadAndValidateLicense()
{
    try
    {
        var licenseTxt = System.IO.Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir,
            "PropTraderTools",
            "license.txt");
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

---

### Required replacement (exact — do NOT alter)

Replace the entire method body (including the comment header) with the following:

```csharp
// B121/DW-B130b: dev_mode.txt sentinel bypasses LicenseClient entirely.
// CYC=4: try-enter(1) + devMode.Exists(2) + licenseTxt.Exists(3) + catch(4).
// JS-001: no throw -- any I/O error returns Starter().
// NT8: File I/O is safe in State.Configure (not the hot path).
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

The method signature (`private static FeatureFlags LoadAndValidateLicense()`) is unchanged.  
No other method in `TradeCopierAddOn.cs` is touched.

---

### Post-conditions

After completing T2:

1. `LoadAndValidateLicense` body exactly matches the "Required replacement" above.
2. `complexity_audit.py` reports `LoadAndValidateLicense` CYC ≤ 8.
3. No `lock(` appears anywhere in `TradeCopierAddOn.cs` (new or existing).
4. The file contains zero non-ASCII characters.
5. `dotnet build` succeeds with 0 errors.
6. All xUnit tests listed in the "Tests" section pass.

---

### xUnit tests — file: `src/PropTraderTools/Tests/B121Tests.cs`

Append these tests to the same `B121Tests.cs` created for T1.

#### T_B121_05 — dev_mode.txt present → Elite (the new sentinel path)

```
Name:    T_B121_05_LoadAndValidateLicense_DevModePresent_ReturnsElite
Asserts: returned FeatureFlags has Elite tier (FeatureFlags.IsElite == true or equivalent)
Setup:   Because LoadAndValidateLicense uses static System.IO.File, the test seam
         options are:
           Option A (preferred): Extract a thin IFileSystem interface (if a testability
             wrapper was already added in a prior block) and inject a stub that returns
             Exists=true for "dev_mode.txt".
           Option B (acceptable): Create a real temp directory, write an empty
             dev_mode.txt file, redirect NinjaTrader.Core.Globals.UserDataDir via
             reflection to the temp dir, invoke the method, then clean up.
           Option C (last resort): If neither A nor B is feasible in the current build,
             document this test as "manual SIM gate only" and provide an inline
             pure-logic unit test that asserts FeatureFlags.Elite().IsElite == true
             (confirming the factory behaves correctly), paired with a comment
             explaining why the file-path integration test requires a test harness
             not yet present.
Coverage: NEW sentinel branch — dev_mode.txt present → Elite returned immediately
          without calling LicenseClient.Validate.
```

#### T_B121_06 — dev_mode.txt absent + no license.txt → Starter via LicenseClient (existing path unbroken)

```
Name:    T_B121_06_LoadAndValidateLicense_NoDevMode_NoLicense_DelegatesToLicenseClient
Asserts: returned FeatureFlags == LicenseClient.Validate(string.Empty) result
         (i.e. the existing Starter path is preserved when the sentinel is absent)
Setup:   Temp directory containing neither dev_mode.txt nor license.txt.
         Same seam approach as T_B121_05.
Coverage: All original code paths (no sentinel, no license.txt, empty key) remain
          unbroken by the new sentinel check.
```

---

### 7-scan checklist (T2)

The engineer MUST run every scan below and record a PASS result before marking T2 complete.

| # | Scan | Command | Required result |
|---|------|---------|-----------------|
| SCAN-01 | CYC | `python scripts/complexity_audit.py` | `LoadAndValidateLicense` reports CYC ≤ 8 |
| SCAN-02 | lock() | `grep -rn "lock(" src/PropTraderTools/TradeCopierAddOn.cs` | 0 results |
| SCAN-03 | async void | `grep -rn "async void " src/PropTraderTools/TradeCopierAddOn.cs` | 0 new results |
| SCAN-04 | return null | `grep -rn "return null;" src/PropTraderTools/TradeCopierAddOn.cs` | 0 new value-path nulls |
| SCAN-05 | ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierAddOn.cs` | 0 results |
| SCAN-06 | Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors |
| SCAN-07 | Tests | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | all passing |

---

### Acceptance criteria (T2)

- [ ] `LoadAndValidateLicense` body exactly matches required replacement (character-for-character).
- [ ] SCAN-01 through SCAN-07 all PASS.
- [ ] T_B121_05 and T_B121_06 pass (or T_B121_05 is explicitly marked "manual SIM gate only"
      with rationale per the Option C note above).
- [ ] No other method in `TradeCopierAddOn.cs` was modified.
- [ ] Ticket is independently revertable (zero cross-file side effects).

---

---

## Cross-ticket requirements

### NT8 sync gate (mandatory — both tickets)

After BOTH tickets are complete and SCAN-07 passes, run the sync-and-verify script:

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Required result: **0 MISMATCH lines**.  
Then press **F5** in NinjaTrader 8 to recompile. File copy alone is not sufficient.

### File split validation

| Ticket | File touched | Methods changed | Cross-file contamination |
|--------|-------------|-----------------|--------------------------|
| T1 | `CopyEngine.cs` only | `IsFollowerAccount` only | NONE |
| T2 | `TradeCopierAddOn.cs` only | `LoadAndValidateLicense` only | NONE |

Each ticket is independently revertable. No shared state is introduced between T1 and T2.

### Execution order

T1 and T2 are independent. They MAY be executed in parallel or in any order.  
Both must complete before the NT8 sync gate above is run.

---

**TICKETS_COMPLETE**
