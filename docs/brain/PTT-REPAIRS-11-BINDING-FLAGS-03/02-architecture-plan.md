# Architecture Plan — PTT-REPAIRS-11-BINDING-FLAGS-03

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-03  
**Phase:** 1 — Architecture  
**Deferred item closed:** DW-09-03  
**Status:** PLAN_COMPLETE  

---

## 1. LANE-SPLIT GATE RESULT

| Question | Answer |
|----------|--------|
| Q1. Same method or within 50 lines? | Both changes in same class `BwaveCycTaR2HelperTests`, ~L6694–L6810. |
| Q2. Fix B design depends on Fix A final design? | YES — the test call site depends on the new overload existing. |
| Q3. Each fix has standalone value if the other is blocked? | NO — overload alone is dead code; test switch alone fails to compile. |
| Q4. Each fix has an independent SIM verification path? | NO — both verified by a single `dotnet test` run. |

**GATE RESULT: SINGLE PIPELINE** — Fix A (new helper) and Fix B (test attribute + call site) are one indivisible atomic unit.

---

## 2. Root Cause Analysis

### Why the test was `[Fact(Skip)]`

The Skip reason stated:
> `"obfuscation: AgileDotNetRT renames private members; cannot locate by string name"`

This was a **misdiagnosis**. The actual failure is a `BindingFlags` mismatch:

- `GetSenderAccountName` in `CopyEngine.cs` is declared `internal static`.
- The class-level `GetMethod` helper uses `BindingFlags.NonPublic | BindingFlags.Instance`.
- `BindingFlags.Instance` excludes static members. `Type.GetMethod` returns `null` for a static member when only `Instance` is requested.
- `GetSenderAccountName` carries `[ObfuscationAttribute(Feature = "rename", Exclude = true)]`, so its name is already protected from obfuscation renaming.

**Fix:** A second overload `GetStaticMethod` using `BindingFlags.NonPublic | BindingFlags.Static` resolves the null return without touching any production code.

---

## 3. Component List

| # | Component | File | Lines Affected |
|---|-----------|------|---------------|
| A | New reflection helper `GetStaticMethod` | `src/PropTraderTools/CopyEngineTests.cs` | Insert after L6695 |
| B | Test attribute change `[Fact(Skip=...)]` → `[Fact]` | `src/PropTraderTools/CopyEngineTests.cs` | L6803 |
| C | Test call site change `GetMethod(...)` → `GetStaticMethod(...)` | `src/PropTraderTools/CopyEngineTests.cs` | L6808 |

**Total files changed: 1.** No production `.cs` files touched. No `deploy-sync.ps1` required.

---

## 4. Class and Method Signatures

### 4.1 New Method (to be inserted at L6696)

```csharp
private static MethodInfo GetStaticMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```

**Placement:** Immediately after the existing `GetMethod` helper (L6694–6695) in `BwaveCycTaR2HelperTests`.

**Do NOT modify** the existing helper:
```csharp
// L6694 — existing, untouched
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```

### 4.2 Modified Test

**Before (L6803–L6810):**
```csharp
[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
public void GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate()
{
    // Verifies the shared helper exists (reused by both OnPendingBeAccountUpdate
    // and OnTrailBeAccountUpdate after TA-R2 refactor).
    var m = GetMethod("GetSenderAccountName");
    Assert.NotNull(m);
}
```

**After:**
```csharp
[Fact]
public void GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate()
{
    // Verifies the shared helper exists (reused by both OnPendingBeAccountUpdate
    // and OnTrailBeAccountUpdate after TA-R2 refactor).
    var m = GetStaticMethod("GetSenderAccountName");
    Assert.NotNull(m);
}
```

---

## 5. Data Flow

```
dotnet test
  -> xUnit discovers [Fact] on GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate
  -> Calls GetStaticMethod("GetSenderAccountName")
  -> typeof(CopyEngine).GetMethod("GetSenderAccountName",
         BindingFlags.NonPublic | BindingFlags.Static)
  -> Matches: internal static string GetSenderAccountName(object sender)   [CopyEngine.cs L6970]
     - internal  => covered by BindingFlags.NonPublic
     - static    => covered by BindingFlags.Static
     - Name      => protected from rename by [ObfuscationAttribute(Exclude=true)]
     - Accessible => InternalsVisibleTo("PropTraderTools.Tests") at CopyEngine.cs L46
  -> Returns MethodInfo (non-null)
  -> Assert.NotNull(m) PASSES
```

---

## 6. Threading Model

No threading concerns. This change is confined to xUnit test infrastructure code:
- `GetStaticMethod` is a pure expression-body static method — no async, no dispatch, no shared state.
- `BwaveCycTaR2HelperTests` has no instance fields and no setup/teardown.
- xUnit parallelism does not affect this class (no `[Collection]` isolation required; class is stateless).

---

## 7. NinjaTrader 8 API Usage

**None.** This change touches only `System.Reflection` and xUnit `[Fact]` attributes. No NT8 types are added or called. No NT8 runtime host is required for this test — it is a pure MethodInfo existence assertion (`Assert.NotNull(m)`).

---

## 8. JS Rules Compliance

| Rule | Status | Notes |
|------|--------|-------|
| No `lock()` | PASS | No locking of any kind |
| No new `throw` in dispatch | PASS | No throw statements added |
| No mutable struct | PASS | No structs touched |
| No `DateTime.Now` | PASS | No datetime usage |
| No hardcoded hex colors | PASS | No UI code touched |
| No `FontFamily` | PASS | No UI code touched |
| ASCII-only identifiers | PASS | `GetStaticMethod` — all ASCII |
| CYC <= 8 per method | PASS | `GetStaticMethod` CYC=1; test CYC=1 |

---

## 9. Verification Targets

### Count delta (this epic only)

| Metric | Before | After | Delta |
|--------|--------|-------|-------|
| Passed | ≥23 | ≥24 | +1 |
| Skipped | ≥491 | ≥490 | -1 |
| Failed | 0 | 0 | 0 |
| Total | 514 | 514 | 0 |

> **Note:** The mission brief states a combined target of 26 passed / 488 skipped across both `02` and `03` epics. This epic (`03`) contributes +1 pass / -1 skip of that combined delta.

### Build gate

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: `0 Error(s)`

### Test gate

```
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~BwaveCycTaR2HelperTests"
```
Expected: `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` — `PASSED`

### DO NOT TOUCH list (engineer must verify no diff)

| Location | Reason |
|----------|--------|
| `BwaveCycT1R1BeHelperTests` L6570 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull` | NT8-runtime-skipped; instance invocation path; out of scope |
| `BwaveCycT1R1BeHelperTests` L6580 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount` | NT8-runtime-skipped; instance invocation path; out of scope |
| `BwaveCycTaR2HelperTests` L6694-6695 — existing `GetMethod(string name)` helper | Must not be modified; correct for all other tests in this class |
| All other `[Fact(Skip=...)]` tests | Must not have Skip removed |
| All production `.cs` files | Zero production changes required or permitted |

---

## 10. Contingency: If Test Fails After Skip Removal

Per mission brief constraint:
> "If it fails, do NOT remove Skip — document as new deferred item."

However, given:
- `GetSenderAccountName` carries `[ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- `InternalsVisibleTo` is confirmed present
- `BindingFlags.NonPublic | BindingFlags.Static` is the correct flag combination for `internal static`
- The test only calls `Assert.NotNull(m)` — no NT8 runtime required

Failure probability: **negligible**. No contingency deferred item expected.

If failure occurs anyway, document as: `DW-REPAIRS-03-FALLBACK: GetSenderAccountName reflection fails with NonPublic|Static despite ObfuscationAttribute(Exclude=true). Investigate assembly load order.`

---

## 11. Summary

Single-ticket, single-file, three-line fix. The root cause is a `BindingFlags.Instance` vs `BindingFlags.Static` mismatch. The fix adds a parallel `GetStaticMethod` helper (CYC=1) and switches one test to use it, removing an incorrectly diagnosed obfuscation Skip. No production code changes. No NT8 runtime required.
