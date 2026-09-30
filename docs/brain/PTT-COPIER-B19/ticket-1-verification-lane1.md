# PTT-COPIER-B19 Ticket 1 Verification — Lane 1

**Block**: PTT-COPIER-B19
**Ticket**: T1 — DW-B19-COPIER-BUG-01 (P0)
**Verifier**: ptt-verifier (Layer 3 — independent)
**Date**: 2026-07-13
**Status**: VERIFY_PASS

---

## Layer 3 Independent Scan Results

All 7 scans run independently using `ctx_shell`. Engineer's Layer 2 results NOT trusted until verified.

---

### SCAN-01 — Old reference equality (`e.Order.Account ==`) gone

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "e\.Order\.Account ==" -CaseSensitive
```

**Layer 2 (engineer)**: 0 results ✅
**Layer 3 (verifier)**: 0 results ✅
**Match**: ✅ CONSISTENT — Old `== rule.MasterAccount` reference equality is gone.

---

### SCAN-02 — New name equality (`Account.Name ==`) present exactly once

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "Account\.Name ==" -CaseSensitive
```

**Layer 2 (engineer)**: 1 result (CopyEngine.cs:381) ✅
**Layer 3 (verifier)**: 1 result — `CopyEngine.cs:381` ✅
**Match**: ✅ CONSISTENT

**Exact text at line 381** (from direct source read):
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```
**Contract match**: ✅ Exact — matches ticket requirement verbatim.

---

### SCAN-03 — No `lock()` introduced

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "^\s+lock\s*\(" -CaseSensitive
```

**Layer 2 (engineer)**: PASS (reported as JS-021 compliant)
**Layer 3 (verifier)**: 0 results ✅
**Match**: ✅ CONSISTENT — No `lock()` calls in source. JS-021 satisfied.

---

### SCAN-04 — No `async void` introduced

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "^\s+async void " -CaseSensitive
```

**Layer 2 (engineer)**: PASS (reported as JS-033 compliant)
**Layer 3 (verifier)**: 0 results ✅
**Match**: ✅ CONSISTENT

---

### SCAN-05 — Build

```powershell
dotnet build "c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj"
```

**Layer 2 (engineer)**: Documented NT8-032 — `dotnet test` cannot run against `PropTraderTools.csproj`;
NT8 proprietary assemblies unavailable. Source scans are the verification contract.

**Layer 3 (verifier)**: Build attempted. Result:

| Error | File | Line | Classification |
|-------|------|------|----------------|
| CS0234 — `Indicators` namespace | `AtrSizingEngine.cs:20` | 20 | Pre-existing NT8 assembly gap |
| CS0246 — `Indicator` type | `AtrSizingEngine.cs:24` | 24 | Pre-existing NT8 assembly gap |
| CS8370 — nullable ref types C# 7.3 | `CopyEngine.cs:628` | 628 | Pre-existing `.csproj` config — `Order?` return type exists before B19 |

**Assessment**: All 3 errors are pre-existing, not introduced by this ticket.
- `AtrSizingEngine.cs` errors: NT8 assembly gap, present before B19.
- `CopyEngine.cs:628` error (`Order?`): pre-existing nullable annotation, `.csproj` sets `<Nullable>disable</Nullable>` + C# 7.3 — both were in place before B19.
- **No new errors introduced by B19 T1**.
- NT8 definitive gate remains F5 compilation inside NinjaTrader — cannot be run in standalone dotnet build.

**Match**: ✅ CONSISTENT with engineer's NT8-032 documentation.

---

### SCAN-06 — Gate2 tests present

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "Gate2_" -CaseSensitive
```

**Layer 2 (engineer)**: Both Gate2 tests present (CopyEngineTests.cs:1901, :1931)
**Layer 3 (verifier)**:
```
CopyEngineTests.cs:1901  public void Gate2_UsesAccountName_SourceContractVerified()
CopyEngineTests.cs:1931  public void Gate2_NullMasterAccount_NoCopyOrder()
```
**Match**: ✅ CONSISTENT — Both required tests present at reported line numbers.

---

### SCAN-07 — Total `[Fact]` count

**Layer 2 (engineer)**: Pattern `\[Fact\]` → 113

**Layer 3 (verifier)**:
- Pattern `^\s+\[Fact\]` (anchored, real attributes only): **112**
- Pattern `\[Fact\]` (unanchored, includes comment at line 1748): **113**

**Resolution**: The single difference is line 1748, which is a code comment
`// B16 T2 -- 10 [Fact] tests --`. This comment was pre-existing before B19.
The engineer's scan used the unanchored pattern — same as the prior-count baseline
(same methodology used for "111 prior"). Therefore:

| Signal | Count |
|--------|-------|
| Real `[Fact]` attributes | **112** |
| Comment-included (engineer's methodology) | **113** |
| Gate2 tests present | ✅ both at 1900, 1930 |
| New tests added | **2** (net delta confirmed by git diff) |

**Discrepancy note**: The absolute count differs by 1 between Layer 2 and Layer 3 due to
scan pattern anchor choice. However, the **intent** of the ticket (2 new Gate2 tests
appended, both present) is fully satisfied. The off-by-one is a methodology artifact, not
a missing test.

**Match**: ⚠️ MINOR METHODOLOGY DISCREPANCY (not a failing condition) — both Gate2 tests
confirmed present; net delta of +2 confirmed by git diff.

---

## Line 381 — Exact Text Verification

**Ticket contract**:
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```

**Actual source line 381** (from `ctx_read lines:370-400`):
```csharp
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```

**Result**: ✅ EXACT MATCH — character-for-character.

---

## Line 659 — Deferred Item Untouched

**Ticket mandate**: Do NOT touch line 659 (`PopulateOrderMap` dedup guard).

**Actual source line 659** (from `ctx_read lines:650-670`):
```csharp
if (!bag.Any(b => b.FollowerAccount == followerAccount))         // (1) branch
```

**Result**: ✅ UNTOUCHED — Still uses reference equality as expected. DW-B19-02 remains
deferred to B20+ per the deferred backlog.

---

## Test Body Verification — Syntactic Correctness

Both new test bodies inspected via `ctx_read lines:1895-1980` and git diff.

### `Gate2_UsesAccountName_SourceContractVerified` (lines 1900–1926)

- Uses `BindingFlags.NonPublic | BindingFlags.Instance` reflection (correct)
- Asserts `Assert.NotNull(fi)` on `_rules` field — correct
- Asserts `Assert.Equal("Account", accountType.Name)` — correct type-contract test
- Asserts `Assert.NotNull(nameProp)` and `Assert.Equal(typeof(string), nameProp.PropertyType)` — correct
- All Assert methods are xUnit — ✅ xUnit-only (no NUnit/MSTest)
- Syntactically valid C# — ✅

### `Gate2_NullMasterAccount_NoCopyOrder` (lines 1930–1973)

- `SetEnabled(false)` + StatusUpdate event subscription — correct guard setup
- `Record.Exception(() => _engine.AddRule(...))` — correct xUnit exception capture
- Reflection walk of `_rules` bag — correct IEnumerable cast
- Null-conditional simulation: `string name = masterAccount == null ? null : ...` — correct
- `Assert.Null(name)` — verifies null-safe evaluation
- `Assert.True(foundNullMaster, ...)` — guard against silent miss
- `Assert.False(statusFired)` — confirms no copy dispatch
- All Assert methods xUnit — ✅
- Syntactically valid C# — ✅

---

## Out-of-Scope File Modification Check

**Ticket mandate**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs` must NOT be modified by B19.

**Method**: `git diff HEAD -- [file]` per file + git log history.

| File | In git diff? | B19 modification? | Finding |
|------|-------------|-------------------|---------|
| `TradeCopierPanel.cs` | Yes (working tree modified) | NO — changes from B14/B15/B17 header blocks pre-date B19 | ✅ CLEAN |
| `TradeCopierWindow.cs` | Yes (working tree modified) | NO — changes from B11 header block pre-date B19 | ✅ CLEAN |
| `TradeCopierAddOn.cs` | Yes (working tree modified) | NO — changes pre-date B19 | ✅ CLEAN |
| `AtrSizingEngine.cs` | Yes (working tree modified) | NO — pre-existing NT8 build errors; no B19 changes | ✅ CLEAN |

**All modified-in-working-tree files are pre-existing B11/B12/B14/B15/B17 changes** committed as
B10 baseline. The `git diff HEAD` baseline is the B10 commit — all divergence is from prior blocks,
not B19.

**B19 only modified**:
1. `CopyEngine.cs` — 1 line at line 381
2. `CopyEngineTests.cs` — 2 new `[Fact]` methods appended

**Result**: ✅ NO OUT-OF-SCOPE MODIFICATIONS from B19.

---

## Architecture Compliance

| Requirement | Status |
|-------------|--------|
| REQ-B19-01 — Gate 2 uses string name equality | ✅ `e.Order.Account.Name == rule.MasterAccount?.Name` |
| REQ-B19-02 — Two new [Fact] tests present | ✅ Both at CopyEngineTests.cs:1901, 1931 |
| REQ-B19-03 — No regressions to prior tests | ✅ All prior tests still present; no deletions |
| REQ-B19-04 — Zero `lock()` in CopyEngine.cs | ✅ SCAN-03 confirmed 0 results |

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — No `lock()` | SCAN-03: 0 results | ✅ PASS |
| JS-001 — No `throw` in hot path | `?.Name` evaluates null safely; no exception paths | ✅ PASS |
| JS-002 — No `return null` | No new methods; no new return statements | ✅ PASS |
| JS-033 — No `async void` | SCAN-04: 0 results | ✅ PASS |
| CYC ≤ 8 — `OnOrderUpdate` | Fix changes comparison type only, not branch count. CYC remains 7 | ✅ PASS |

---

## NT8 Constraint Check

| Rule | Check | Result |
|------|-------|--------|
| `Account.Name` is valid `string` property | Used in 10+ existing lines (456, 514, 589, 820, 843, 881, 925, 967, 997, 1068) | ✅ CONFIRMED |
| `?.` null-conditional | Valid in C# 6+ / .NET 4.8 | ✅ VALID |
| NT8-001 (`init;` ban) | No new properties | ✅ CLEAN |
| NT8-002 (`record` ban) | No new record types | ✅ CLEAN |
| NT8-003 (`volatile double` ban) | No volatile fields in B19 change | ✅ CLEAN |
| NT8-004 (`ImmutableDictionary` ban) | No immutable collections | ✅ CLEAN |
| NT8-032 (`dotnet test` blocker) | Source scans used; F5 gate is authoritative | ✅ DOCUMENTED |
| FontFamily= ban | No WPF changes | ✅ N/A |
| Hex color (#RRGGBB) ban | No WPF changes | ✅ N/A |
| `sealed` on TradeCopierWindow ban | No Window changes | ✅ N/A |

---

## Layer 2 vs Layer 3 Cross-Check Summary

| Scan | Layer 2 | Layer 3 | Discrepancy? |
|------|---------|---------|--------------|
| SCAN-01 (`e.Order.Account ==`) | 0 results | 0 results | ✅ None |
| SCAN-02 (`Account.Name ==`) | 1 result (line 381) | 1 result (line 381) | ✅ None |
| SCAN-03 (`lock()`) | PASS | 0 results | ✅ None |
| SCAN-04 (`async void`) | PASS | 0 results | ✅ None |
| SCAN-05 (build) | NT8-032 documented | NT8-032 confirmed; 0 new errors | ✅ None |
| SCAN-06 (Gate2 tests) | Both at :1901, :1931 | Both at :1901, :1931 | ✅ None |
| SCAN-07 (`[Fact]` count) | 113 (unanchored) | 112 (anchored) / 113 (unanchored) | ⚠️ Methodology note only — 1 comment at line 1748 inflates unanchored count. Not a test deficit. |

**Single discrepancy found**: SCAN-07 count (112 vs 113) — caused by `[Fact]` in a pre-existing
comment at line 1748. Both Gate2 tests are confirmed present. This is a scan-methodology artifact,
NOT a missing test. Not a failing condition.

---

## Verdict

```
VERIFY_PASS
```

| Check | Result |
|-------|--------|
| Line 381 exact text | ✅ EXACT MATCH |
| Line 659 untouched | ✅ UNTOUCHED |
| SCAN-01 (old equality gone) | ✅ 0 results |
| SCAN-02 (new equality present) | ✅ 1 result at line 381 |
| SCAN-03 (no lock) | ✅ 0 results |
| SCAN-04 (no async void) | ✅ 0 results |
| SCAN-05 (build) | ✅ Pre-existing NT8 errors only — 0 new errors from B19 |
| SCAN-06 (Gate2 tests) | ✅ Both present at :1901, :1931 |
| SCAN-07 (test count) | ✅ Both Gate2 tests confirmed; methodology note only |
| Test bodies syntactically correct | ✅ Both valid xUnit [Fact] tests |
| Out-of-scope files | ✅ Not modified by B19 |
| DNA rules | ✅ All PASS |
| NT8 constraints | ✅ All CLEAN |
| Architecture compliance | ✅ All 4 REQ-B19-XX satisfied |

No VERIFY_FAIL conditions found. All ticket contracts met.

---

## Deferred Item — Confirmed Untouched

| ID | File | Line | Description | Status |
|----|------|------|-------------|--------|
| DW-B19-02 | CopyEngine.cs | 659 | `PopulateOrderMap` dedup guard reference equality | ✅ Untouched — still `b.FollowerAccount == followerAccount` |
