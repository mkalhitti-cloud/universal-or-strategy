# PTT-REPAIRS-11-BINDING-FLAGS-02 — Architecture Plan

**Epic:** PTT-REPAIRS-11-BINDING-FLAGS-02
**Closes DW item:** DW-09-02 (OPEN, from BWAVE-CYC-IMPL-01/06-deferred-backlog.md)
**Status:** PLAN_COMPLETE
**Wave workspace:** `C:\WSGTA\universal-or-strategy\`
**Artifact:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-02/02-architecture-plan.md`
**Rules source:** DNA block (role definition) — `docs/protocol/RULES_CATALOG.md` not present in repo (confirmed by glob across all prior epics)

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

| Question | Answer | Rationale |
|---|---|---|
| Q1. Same method or within 50 lines? | **YES** | Both test changes reside within L6931-L6945 — 15-line span in the same section |
| Q2. Fix B design depends on Fix A final design? | **YES** | Both tests share the same `GetStaticMethod` helper prerequisite; one cannot be fixed without adding the helper that both use |
| Q3. Each fix has standalone value if the other is blocked? | **NO** | Both tests test the same method (`LogBeSlotEviction`); splitting leaves an inconsistent test suite |
| Q4. Each fix has an independent SIM verification path? | **NO** | Both tests are verified by the same `dotnet test` run and share the same helper |

**Gate determination:** Q1=YES, Q2=YES → default SINGLE-PIPELINE applies. No lane split.

---

## 1. DW-09-02 Context

DW-09-02 was opened in PTT-REPAIRS-09-OBFUSC-ATTR and carried forward through PTT-REPAIRS-10-B7-TYPEINIT and BWAVE-CYC-IMPL-01. Its prerequisites are now met:

- **DW-09-01 CLOSED** by BWAVE-CYC-IMPL-01: 70 private methods implemented, `LogBeSlotEviction` confirmed to remain `private static`.
- **ObfuscationAttribute present** at `CopyEngine.cs` L1778: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` — AgileDotNetRT will not rename `LogBeSlotEviction`.
- **Root cause confirmed:** `BwaveCycTaR3HelperTests.GetMethod` uses `BindingFlags.NonPublic | BindingFlags.Instance`, which excludes static members. `LogBeSlotEviction` is `private static`. `GetMethod` returns `null` for this method, causing `Assert.NotNull` to fail — hence the obfuscation-skip annotations were added as a workaround.

---

## 2. Source Confirmation

### `CopyEngine.cs` — Production method (DO NOT MODIFY)
```
L1778: [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
L1779: private static void LogBeSlotEviction(string accName, bool isRejected)
```
- Visibility: `private static` — confirmed.
- Parameters: 2 — `string accName`, `bool isRejected` — confirmed.
- ObfuscationAttribute `Exclude = true` — confirmed.

### `CopyEngineTests.cs` — Target class state (BwaveCycTaR3HelperTests)
```
L6818: public class BwaveCycTaR3HelperTests
L6819: {
L6820:     private static MethodInfo GetMethod(string name) =>
L6821:         typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
          ← INSERT GetStaticMethod here (after L6821)
...
L6931:     // TA-R4: LogBeSlotEviction helper tests
L6932:     [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
L6933:     public void LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod()
L6934:     {
L6935:         var m = GetMethod("LogBeSlotEviction");   ← CHANGE to GetStaticMethod
L6936:         Assert.NotNull(m);
L6937:     }
L6938:
L6939:     [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
L6940:     public void LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters()
L6941:     {
L6942:         var m = GetMethod("LogBeSlotEviction");   ← CHANGE to GetStaticMethod
L6943:         Assert.NotNull(m);
L6944:         Assert.Equal(2, m.GetParameters().Length);
L6945:     }
```

---

## 3. Established Precedent

The `GetStaticMethod` pattern is already used in this same file:

| Class | Lines | Signature |
|---|---|---|
| `BwaveCycTaR6HelperTests` | L7118-7119 | `private static MethodInfo GetStaticMethod(string name) => typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);` |
| `B77QxRaceGuardTests` | L4908-4912 | Same pattern (fully-qualified `System.Reflection.BindingFlags`) |
| `BwaveCycTaR2HelperTests` | L7290 | `private static MethodInfo GetStaticMethod(string name) => ...` |

The `BwaveCycTaR3HelperTests` class is the only one missing a `GetStaticMethod` helper.

---

## 4. Component List

| Component | Location | Change Type |
|---|---|---|
| `GetStaticMethod` helper | `CopyEngineTests.cs` after L6821 | **ADD** — new 2-line expression-body method |
| Test 1 `[Fact(Skip="...")]` attribute | `CopyEngineTests.cs` L6932 | **MODIFY** — replace with `[Fact]` |
| Test 1 `GetMethod(...)` call | `CopyEngineTests.cs` L6935 | **MODIFY** — replace with `GetStaticMethod(...)` |
| Test 2 `[Fact(Skip="...")]` attribute | `CopyEngineTests.cs` L6939 | **MODIFY** — replace with `[Fact]` |
| Test 2 `GetMethod(...)` call | `CopyEngineTests.cs` L6942 | **MODIFY** — replace with `GetStaticMethod(...)` |

**Files touched: 1** (`src/PropTraderTools/CopyEngineTests.cs`)
**Files NOT touched:** `src/PropTraderTools/CopyEngine.cs` and all other `.cs` files.

---

## 5. Exact Change Specification

### Change A — Insert `GetStaticMethod` helper

**Location:** After line 6821 (after the closing semicolon of the existing `GetMethod` helper), add one blank line then the new helper.

**Text to insert:**
```csharp

        private static MethodInfo GetStaticMethod(string name) =>
            typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
```

**Invariant:** The existing `GetMethod` helper at L6820-6821 is NOT modified. All other tests in `BwaveCycTaR3HelperTests` that call `GetMethod(...)` continue to use `BindingFlags.Instance` — correct for all other methods in this class which are private instance.

### Change B — Fix Test 1

**Location:** L6932 (the `[Fact(Skip = "...")]` before `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod`)

**Old:**
```csharp
        [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
        public void LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod()
        {
            var m = GetMethod("LogBeSlotEviction");
```

**New:**
```csharp
        [Fact]
        public void LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod()
        {
            var m = GetStaticMethod("LogBeSlotEviction");
```

### Change C — Fix Test 2

**Location:** L6939 (the `[Fact(Skip = "...")]` before `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters`)

**Old:**
```csharp
        [Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
        public void LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters()
        {
            var m = GetMethod("LogBeSlotEviction");
```

**New:**
```csharp
        [Fact]
        public void LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters()
        {
            var m = GetStaticMethod("LogBeSlotEviction");
```

---

## 6. Method Signatures

| Method | Visibility | Location | CCN | Notes |
|---|---|---|---|---|
| `GetStaticMethod(string name)` | `private static MethodInfo` | `BwaveCycTaR3HelperTests` | 1 | NEW — expression-body, no branches |
| `GetMethod(string name)` | `private static MethodInfo` | `BwaveCycTaR3HelperTests` | 1 | UNCHANGED — kept for all other tests |

---

## 7. Threading Model

Not applicable. This epic modifies only a test file (`CopyEngineTests.cs`). No NinjaTrader runtime interaction, no Dispatcher.InvokeAsync, no ConcurrentQueue, no async/await. The test class is a plain synchronous xUnit class.

---

## 8. Jane Street DNA Rules Compliance

| Rule | Check | Result |
|---|---|---|
| JS-001: No throw | No `throw` statements introduced | PASS |
| JS-002: No lock() | No `lock()` statements introduced | PASS |
| JS-009: ASCII-only | All identifiers and string literals are ASCII | PASS |
| JS-013: No DateTime.Now | No date/time usage introduced | PASS |
| JS-021: No FontFamily | No UI code introduced | PASS |
| JS-023: No hardcoded hex | No color values introduced | PASS |
| JS-025: ConcurrentQueue over lock | No state mutation, no lock pattern | PASS |
| CYC <= 8 | New method CCN = 1; touched test methods CCN = 1 | PASS |

---

## 9. NT8 API Surface

Not applicable to this epic. The only API used is standard .NET:
- `System.Reflection.BindingFlags` — `NonPublic | Static` flags
- `System.Reflection.MethodInfo` — return type from `Type.GetMethod()`
- `Type.GetMethod(string, BindingFlags)` — returns `null` if not found, `MethodInfo` if found

NinjaTrader 8 production code (`CopyEngine.cs`) is NOT modified.

---

## 10. Build and Test Verification

### Verification Gate 1 — Build
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: `Build succeeded. 0 Error(s)`

### Verification Gate 2 — Test Run
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --no-build
```

**Expected result after fix:**
```
BASELINE:  23 passed / 0 failed / 491 skipped / 514 total
TARGET:    25 passed / 0 failed / 489 skipped / 514 total
```

- 2 tests move from SKIPPED to PASSED (net: +2 passed, -2 skipped, total unchanged)
- 0 tests regress from PASSED to FAILED
- All other 487 skipped tests remain skipped

### Fallback (if tests still fail)
If either test reports FAIL (Assert.NotNull fails, meaning reflection still returns null):
1. Do NOT remove the `[Fact(Skip = "...")]` annotations — revert both Changes B and C
2. Keep Change A (GetStaticMethod helper — it is harmless and may be useful for future use)
3. Document as DW-09-02-BLOCKED in the deferred backlog with exact failure output
4. Baseline remains 23 passed / 491 skipped

---

## 11. Scope Lock — What Is NOT Changing

| Item | Disposition |
|---|---|
| `CopyEngine.cs` production code | NOT touched — no production changes |
| `GetMethod` instance helper in `BwaveCycTaR3HelperTests` | NOT changed — other tests depend on it |
| Any other `[Fact(Skip = "...")]` in the file | NOT removed — only the 2 LogBeSlotEviction tests |
| `deploy-sync.ps1` | NOT needed — test file only, no hard-linked production file |
| Any other class in `CopyEngineTests.cs` | NOT touched |
| DW-09-03 (`GetSenderAccountName` binding fix) | OUT OF SCOPE — separate epic |
| DW-09-04 (full obfuscation skip removal) | OUT OF SCOPE — separate epic |

---

## 12. Deferred Work Carried Forward

| ID | Description | Status |
|---|---|---|
| DW-09-02 | Fix LogBeSlotEviction binding flags in BwaveCycTaR3HelperTests + remove 2 Skips | **CLOSED by this epic** |
| DW-09-03 | Fix GetSenderAccountName binding flags in BwaveCycTaR2HelperTests: NonPublic\|Instance → NonPublic\|Static. Remove obfuscation Skip from 1 affected test. ObfuscationAttribute already present. | OPEN |
| DW-09-04 | Remove all 137 remaining obfuscation-skip annotations after each method is implemented with production logic, binding flags verified, and ObfuscationAttribute confirmed. Prerequisites: DW-09-02 (this epic) + DW-09-03 all complete. | OPEN |

---

## 13. Single Ticket Summary

This epic resolves to **1 ticket**, **1 file**, **3 surgical changes** (A, B, C as specified in Section 5).

| Field | Value |
|---|---|
| Ticket ID | T1 |
| File | `src/PropTraderTools/CopyEngineTests.cs` |
| Spec Req IDs | DW-09-02 |
| Changes | A: Add GetStaticMethod helper; B: Fix Test 1 attribute + call; C: Fix Test 2 attribute + call |
| xUnit tests affected | `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod`, `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` |
| Expected outcome | 25 passed / 0 failed / 489 skipped / 514 total |

---

## 14. 7-Scan Checklist (Mandatory Engineer Contract)

All 7 scans MUST be run after completing T1. Every scan must show zero matches / zero violations before the PR is submitted. This checklist is the engineer's sole acceptance gate.

| Scan | Command / Check | Required Result |
|---|---|---|
| **SCAN-01 — lock(** | `grep -r "lock(" src/PropTraderTools/` | Zero matches |
| **SCAN-02 — throw** | Compare diff: no new `throw` statements introduced in any `.cs` file | Zero new `throw` statements |
| **SCAN-03 — CYC** | Cyclomatic complexity of all modified methods: `GetStaticMethod` (new), `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` (modified), `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` (modified) | All CCN = 1 (unchanged for modified; 1 for new) |
| **SCAN-04 — ASCII** | Inspect all inserted/modified string literals for Unicode, emoji, or curly-quote characters | Zero non-ASCII characters in any string literal |
| **SCAN-05 — ObfuscationAttribute** | Confirm `CopyEngine.cs` L1778 still reads `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on `LogBeSlotEviction` | `Exclude = true` present; production file unmodified |
| **SCAN-06 — BindingFlags** | Confirm inserted `GetStaticMethod` uses `BindingFlags.NonPublic \| BindingFlags.Static` (not `Instance`) | `NonPublic \| Static` — zero use of `BindingFlags.Instance` in the new helper |
| **SCAN-07 — Build** | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | `Build succeeded. 0 Error(s)` |

**Scan failure action:** Any scan that fails blocks merge. Engineer must fix and re-run all 7 scans from the top before re-submitting.
