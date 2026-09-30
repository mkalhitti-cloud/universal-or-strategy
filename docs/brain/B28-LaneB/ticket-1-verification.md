# B28 Lane B — Ticket 1 Verification Report
**Defect:** DW-B28-02 | Leader-Account Overloads
**Block:** B28 | Lane: B | Phase: 5V (Verification)
**Verifier:** ptt-verifier (independent Layer 3 re-run)
**Date:** 2026-07-16
**Engineer Layer 2 Status:** BUILD_PASS
**Verifier Layer 3 Verdict:** VERIFY_PASS

---

## 1. Independent 7-Scan Results (Layer 3)

All scans run independently by verifier from Wave workspace root
(`c:\WSGTA\universal-or-strategy\`). None trust engineer self-report.
PowerShell `Select-String` used (no native `grep` on Windows PowerShell).

### SCAN-01: `lock(` in CopyEngine.cs
```
Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("
Result:  2 hits (L598, L1355)
```
**PASS** — Both hits are in CYC-annotation comments (`// ... try block(0).`),
not in any code path. Zero actual `lock()` keyword usages. JS-021 compliant.

Engineer Layer 2 report: Count=2 (comments only). **MATCH.**

### SCAN-02: `async void` in CopyEngine.cs
```
Command: Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void" | Measure-Object | Count
Result:  0
```
**PASS** — 0 results. JS-033 compliant.

Engineer Layer 2 report: 0. **MATCH.**

### SCAN-03: `lock(` in TradeCopierPanel.cs
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "lock\(" | Measure-Object | Count
Result:  0
```
**PASS** — 0 results. JS-021 compliant.

Engineer Layer 2 report: 0. **MATCH.**

### SCAN-04: `Trim(_instrument)` in TradeCopierPanel.cs
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "Trim\(_instrument\)" | Measure-Object | Count
Result:  0
```
**PASS** — 0 results. All Trim call sites migrated to leader-account overload.

Engineer Layer 2 report: 0. **MATCH.**

### SCAN-05: `Flatten(_instrument)` in TradeCopierPanel.cs
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "Flatten\(_instrument\)" | Measure-Object | Count
Result:  0
```
**PASS** — 0 results. All Flatten call sites migrated to leader-account overload.

Engineer Layer 2 report: 0. **MATCH.**

### SCAN-06: `CancelPendingEntries(_instrument)` in TradeCopierPanel.cs
```
Command: Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "CancelPendingEntries\(_instrument\)" | Measure-Object | Count
Result:  0
```
**PASS** — 0 results. All CancelPendingEntries call sites migrated to leader-account overload.

Engineer Layer 2 report: 0. **MATCH.**

### SCAN-07: `[Fact]` count in CopyEngineTests.cs
```
Command: Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object | Count
Result:  138
```
**PASS** — 138 confirmed. Baseline 135 + 3 new (T_B28_01, T_B28_02, T_B28_03).

Engineer Layer 2 report: 138. **MATCH.**

---

## 2. Structural Confirmation — 10 New Methods in CopyEngine.cs

Verified by `Select-String` pattern scan against all new method signatures.

### 5 Private Helpers

| Method | Line | Access | Verified |
|--------|------|--------|---------|
| `TrimOneAccount(Account, Instrument)` | L955 | `private void` | ✅ |
| `FlattenOneAccount(Account, Instrument)` | L981 | `private void` | ✅ |
| `CancelOneAccount(Account, Instrument)` | L1046 | `private void` | ✅ |
| `TrimOneAccountLimit(Account, Instrument, int, double, double)` | L1071 | `private void` | ✅ |
| `FlattenOneAccountLimit(Account, Instrument, int, double, double)` | L1102 | `private void` | ✅ |

### 5 Internal Leader-Account Overloads

| Method | Line | Access | Verified |
|--------|------|--------|---------|
| `Trim(Account, Instrument)` | L867 | `internal void` | ✅ |
| `Flatten(Account, Instrument)` | L884 | `internal void` | ✅ |
| `CancelPendingEntries(Account, Instrument)` | L901 | `internal void` | ✅ |
| `Trim(Account, Instrument, int, double, double)` | L918 | `internal void` | ✅ |
| `Flatten(Account, Instrument, int, double, double)` | L936 | `internal void` | ✅ |

**All 10 methods present. Total new methods: 10/10.**

### Null Guards Verified

All 5 internal overloads contain `if (leader == null)` guard with `StatusUpdate?.Invoke(...); return;`
(no throw — JS-001 compliant). Confirmed at lines L869, L886, L903, L920, L938.

### Helper Delegation in Existing Single-Arg Methods Verified

Existing `Trim(Instrument)` delegates to `TrimOneAccount(acc, instrument)` at L856.
Existing `Flatten(Instrument)` delegates to `FlattenOneAccount(acc, instrument)` at L862.
`CancelPendingEntries(Instrument)` delegates to `CancelOneAccount(acc, instrument)` at L1047.
Limit variants delegate to `TrimOneAccountLimit` / `FlattenOneAccountLimit` throughout. ✅

---

## 3. Panel Call Site Confirmation — TradeCopierPanel.cs

### Old single-arg call sites (must be GONE)

| Pattern | Count | Result |
|---------|-------|--------|
| `_engine.Trim(_instrument)` | 0 | ✅ GONE |
| `_engine.Flatten(_instrument)` | 0 | ✅ GONE |
| `_engine.CancelPendingEntries(_instrument)` | 0 | ✅ GONE |

### New leader-account call sites (must be PRESENT)

| Call Site | Line | Handler | Verified |
|-----------|------|---------|---------|
| `_engine.Trim(_leaderAccount, _instrument)` | L740 | `OnTrimClick` | ✅ |
| `_engine.Trim(_leaderAccount, _instrument, _trimBuffer, ask, bid)` | L742 | `OnTrimClick` | ✅ |
| `_engine.Flatten(_leaderAccount, _instrument)` | L766 | `OnFlattenClick` | ✅ |
| `_engine.Flatten(_leaderAccount, _instrument, _flattenBuffer, ask, bid)` | L768 | `OnFlattenClick` | ✅ |
| `_engine.CancelPendingEntries(_leaderAccount, _instrument)` | L912 | `OnCancel2` | ✅ |
| `_engine.Trim(_leaderAccount, _instrument)` | L1266 | `OnTrim` | ✅ |
| `_engine.Flatten(_leaderAccount, _instrument)` | L1271 | `OnFlatten` | ✅ |
| `_engine.CancelPendingEntries(_leaderAccount, _instrument)` | L1276 | `OnCancel` | ✅ |
| `_engine.Trim(_leaderAccount, _instrument, _trimBuffer, ...)` | L1389 | `DispatchShortcut Key.T` | ✅ |
| `_engine.Flatten(_leaderAccount, _instrument, _flattenBuffer, ...)` | L1390 | `DispatchShortcut Key.F` | ✅ |
| `_engine.CancelPendingEntries(_leaderAccount, _instrument)` | L1391 | `DispatchShortcut Key.C` | ✅ |

**All 11 new call sites confirmed. 0 old single-arg sites remain. Requirement: 3 minimum — 11 present. ✅**

---

## 4. [Fact] Test Verification — CopyEngineTests.cs

### Tests Present

| Test | Line | Verified |
|------|------|---------|
| `T_B28_01_Trim_LeaderOverload_Exists` | L2469 | ✅ |
| `T_B28_02_Flatten_LeaderOverload_Exists` | L2482 | ✅ |
| `T_B28_03_CancelPendingEntries_LeaderOverload_Exists` | L2495 | ✅ |

### Test Body Inspection

All three tests use identical structure matching spec exactly:

```csharp
var methods = typeof(CopyEngine).GetMethods(
    BindingFlags.NonPublic | BindingFlags.Instance);
var overload = methods.FirstOrDefault(m =>
    m.Name == "{Trim|Flatten|CancelPendingEntries}" &&
    m.GetParameters().Length == 2 &&
    m.GetParameters()[0].ParameterType == typeof(NinjaTrader.Cbi.Account) &&
    m.GetParameters()[1].ParameterType == typeof(NinjaTrader.NinjaScript.Instruments.Instrument));
Assert.NotNull(overload);
```

- `GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)` — resolves both `private` and `internal` ✅
- `Parameters.Length == 2` — correct guard for 2-arg overloads ✅
- `ParameterType[0] == typeof(Account)` — correct ✅
- `ParameterType[1] == typeof(Instrument)` — correct ✅
- `Assert.NotNull(overload)` — correct assertion ✅

**All 3 tests present and structurally correct per spec.**

---

## 5. Cross-Check vs Engineer Layer 2 Report

| Scan | Engineer Report | Verifier Result | Match? |
|------|----------------|-----------------|--------|
| SCAN-01 `lock(` CopyEngine.cs | Count=2 (comments) | 2 hits L598, L1355 (comments) | ✅ MATCH |
| SCAN-02 `async void` CopyEngine.cs | 0 | 0 | ✅ MATCH |
| SCAN-03 `lock(` TradeCopierPanel.cs | 0 | 0 | ✅ MATCH |
| SCAN-04 `Trim(_instrument)` Panel | 0 | 0 | ✅ MATCH |
| SCAN-05 `Flatten(_instrument)` Panel | 0 | 0 | ✅ MATCH |
| SCAN-06 `CancelPendingEntries(_instrument)` Panel | 0 | 0 | ✅ MATCH |
| SCAN-07 `[Fact]` count | 138 | 138 | ✅ MATCH |

**No discrepancies found. All 7 scans match Layer 2 report exactly.**

**Discrepancy summary:** None.

---

## 6. CYC Spot-Check

Manual branch-count verification of actual source code bodies (verified from L865–L1130):

### 2-Arg Overloads (target CYC = 4)

| Method | Decision Points | CYC | Compliant |
|--------|----------------|-----|-----------|
| `Trim(Account, Instrument)` | (1) `leader==null` if, (2) `leader` direct call is sequential, (3) `foreach`, (4) `acc==leader` if | **4** | ✅ ≤8 |
| `Flatten(Account, Instrument)` | Same structure | **4** | ✅ ≤8 |
| `CancelPendingEntries(Account, Instrument)` | Same structure | **4** | ✅ ≤8 |

### 5-Arg Overloads (target CYC ≤ 5)

| Method | Decision Points | CYC | Compliant |
|--------|----------------|-----|-----------|
| `Trim(Account, Instrument, int, double, double)` | (1) `leader==null` if, (2) `ask<=0||bid<=0||exitBuffer==0` compound if, (3) leader direct call sequential, (4) `foreach`, (5) `acc==leader` if | **5** | ✅ ≤8 |
| `Flatten(Account, Instrument, int, double, double)` | Same structure | **5** | ✅ ≤8 |

### Private Helpers (target CYC ≤ 4)

| Method | CYC | Compliant |
|--------|-----|-----------|
| `TrimOneAccount` | 3 (pos guard, action ternary, try/catch) | ✅ |
| `FlattenOneAccount` | 3 (pos guard, action ternary, try/catch) | ✅ |
| `CancelOneAccount` | 4 (foreach, instr filter, state guard, bracket guard) | ✅ |
| `TrimOneAccountLimit` | 3 (pos guard, isLong ternary, try/catch) | ✅ |
| `FlattenOneAccountLimit` | 3 (pos guard, isLong ternary, try/catch) | ✅ |

**All 10 methods CYC ≤ 8. Jane Street strict standard satisfied.**

---

## 7. JS / NT8 Compliance Check (New Code Only)

### Jane Street Rules

| Rule | Check | Result |
|------|-------|--------|
| **JS-021** | No `lock()` in any new/modified method body | ✅ PASS — zero lock() in all 10 new methods |
| **JS-001** | No bare `throw` in new methods | ✅ PASS — all exception paths use `catch (Exception ex)` + `StatusUpdate?.Invoke(...)`. No rethrow. |
| **JS-002** | No `return null` | ✅ PASS — all new methods are `void`. Null-leader paths use `StatusUpdate + return`. |
| **JS-033** | No `async void` | ✅ PASS — all new methods are synchronous `void`. |
| **JS-015** | ASCII-only identifiers and literals | ✅ PASS — all identifiers and string literals in new code are ASCII. |

### NT8 Compiler Rules

| Rule | Check | Result |
|------|-------|--------|
| **NT8-001** | No `{ get; init; }` | ✅ PASS — no new properties, only methods. |
| **NT8-002** | No `abstract/sealed record` | ✅ PASS — no new types. |
| **NT8-003** | No `volatile double` | ✅ PASS — no new fields. |
| **NT8-004** | No `ImmutableDictionary` / `System.Collections.Immutable` | ✅ PASS — no new collections. |
| **NT8-007** | `CreateOrder` arg 12 = `(NinjaTrader.Cbi.CustomOrder)null` for Limit orders | ✅ PASS — both `TrimOneAccountLimit` and `FlattenOneAccountLimit` use this cast. Market helpers correctly use plain `null` (arg 12 is not the limit-order custom order slot). |
| **NT8-014** | Signal name starts with `"PTT-"` | ✅ PASS — `"PTT-Trim"`, `"PTT-Flatten"`, `"PTT-TrimLimit"`, `"PTT-FlattenLimit"`, `"PTT-Cancel"` confirmed in source. |
| **DateTime** | No `DateTime.Now` — use `DateTime.UtcNow` or NT8 sentinel | ✅ PASS — `DateTime.MaxValue` used as NT8 order expiry sentinel throughout. |
| **Hex colors** | No `#RRGGBB` strings | ✅ PASS — no color strings in new code. |
| **FontFamily** | No `FontFamily=` WPF attribute | ✅ PASS — no WPF markup in new code. |

---

## 8. Architecture / Spec Compliance

- **Root cause addressed:** `Trim(_instrument)` / `Flatten(_instrument)` / `CancelPendingEntries(_instrument)` relied on `AllAccounts(instrument)` which iterates `_rules` (`ConcurrentBag`). When `_rules` is empty, no accounts are iterated and buttons are silent no-ops. The leader-account overloads bypass this by firing the leader directly before the fan-out. ✅
- **Pattern mirrors spec:** Matches `BreakEven(Account, Instrument, int)` at L1217 as required by ticket. ✅
- **Singleton constraint (JS-010):** No new constructors — `CopyEngine` singleton pattern unchanged. ✅
- **Thread safety:** All new methods are synchronous `void`. Panel handlers execute on WPF dispatch thread. `AllAccounts(instrument)` iterates `ConcurrentBag` (lock-free). No `Dispatcher.InvokeAsync` needed and none added. ✅
- **No scope creep:** Only 3 files modified (CopyEngine.cs, TradeCopierPanel.cs, CopyEngineTests.cs). No new files, no new `.csproj` entries, no new namespaces. ✅
- **Ticket scope boundary:** Ticket specified 3 call sites minimum. Engineer updated 11 (all handlers including keyboard shortcuts). This is correct — all button/keyboard paths that dispatched the old single-arg calls have been migrated. ✅

---

## 9. F5 Readiness Assessment

- No syntax issues found in new code (correct method signatures, proper braces, no unsupported C# features)
- All NT8 API calls use known-good patterns verified in prior blocks (B18, B19)
- `NinjaTrader.Cbi.Account` already in scope via existing `using NinjaTrader.Cbi;`
- `NinjaTrader.NinjaScript.Instruments.Instrument` already in scope
- No new `using` directives, no new namespaces, no `.csproj` entries
- `(NinjaTrader.Cbi.CustomOrder)null` cast preserved in limit helpers (NT8-007 compliant)
- **F5 READY: YES**

---

## 10. Final Verdict

| Category | Result |
|----------|--------|
| 7 Scans (all 7) | ✅ PASS |
| 10 New Methods Present | ✅ PASS |
| 5 Null Guards Present | ✅ PASS |
| Panel Old Sites Gone | ✅ PASS |
| Panel New Sites Present | ✅ PASS |
| [Fact] Count = 138 | ✅ PASS |
| T_B28_01/02/03 Correct | ✅ PASS |
| CYC ≤ 8 (all 10 methods) | ✅ PASS |
| JS Rules (JS-021/001/002/033/015) | ✅ PASS |
| NT8 Rules (NT8-001/007/014/DateTime) | ✅ PASS |
| Layer 2 vs Layer 3 discrepancies | ✅ NONE |
| F5 Readiness | ✅ READY |

---

## VERIFY_PASS

DW-B28-02 is fully resolved. All 7 scans at target values. 10 new methods present with
correct signatures, null guards, CYC budget, and JS/NT8 compliance. 11 panel call sites
updated (all button + keyboard shortcut handlers). 3 xUnit [Fact] tests added and correct.
No discrepancies vs engineer Layer 2 report. Code is F5-ready.

**Result: VERIFY_PASS**
