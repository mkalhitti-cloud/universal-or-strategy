# B75-LaneA Ticket-2 Verification Report

**Ticket**: B75-LaneA-T2 (xUnit test stubs -- 60 [Fact] methods)
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-17
**Source file**: `src/PropTraderTools/CopyEngineTests.cs`
**Class verified**: `CopyEngineB75Tests` (lines 3661-4260)

---

## V1: Test Count

| Metric | Value |
|--------|-------|
| `[Fact]` runnable | **46** |
| `[Fact(Skip = "NT8-runtime: ...")]` skipped | **14** |
| **Total** | **60** |

**PASS** — Matches engineer report (46 runnable + 14 NT8-runtime skips = 60 total).

Verification command:
```powershell
$lines = Get-Content "src/PropTraderTools/CopyEngineTests.cs"
$b75Lines = $lines[3660..($lines.Count-1)]
($b75Lines | Select-String '^\s*\[Fact\]').Count            # 46
($b75Lines | Select-String '\[Fact\(Skip').Count             # 14
```

---

## V2: Spot-Check (10 tests)

| Test ID | Expected Assert | Actual Assert in Source | Verdict |
|---------|----------------|------------------------|---------|
| T_B63_04 (line 3744) | `Assert.True(result)` — "Close" is native exit, gate 3 bypassed | `Assert.True(result)` | **PASS** |
| T_B63_06 (line 3774) | `Assert.True(result)` — null passes PTT-prefix guard, gate 3 passes | `Assert.True(result)` | **PASS** |
| T_B64E_01 (line 3829) | `Assert.False(result)` — "Entry" hits IsNonFlatDispatchName=true | `Assert.False(result)` | **PASS** |
| T_B66N_01 (line 3992) | `Assert.False(CopyEngine.IsExitSignalName("Entry"))` — HOTFIX-B67 regression | `Assert.False(CopyEngine.IsExitSignalName("Entry"))` | **PASS** |
| T_B67E_01 (line 4036) | `Assert.False(CopyEngine.IsExitSignalName("Entry"))` — B67 primary guard | `Assert.False(CopyEngine.IsExitSignalName("Entry"))` | **PASS** |
| T_B65G_01 (line 3894) | `Assert.True(IsDispatchTriggerState(Accepted, Limit))` | `Assert.True(CopyEngine.IsDispatchTriggerState(OrderState.Accepted, OrderType.Limit))` | **PASS** |
| T_B65G_03 (line 3908) | `Assert.True(IsDispatchTriggerState(Submitted, Market))` | `Assert.True(CopyEngine.IsDispatchTriggerState(OrderState.Submitted, OrderType.Market))` | **PASS** |
| T_CYC_01 (line 4209) | `Assert.False(CopyEngine.IsBeDisarmCandidate(null))` — null guard | `Assert.False(CopyEngine.IsBeDisarmCandidate(null))` | **PASS** |
| T_CYC_05 (line 4235) | `Assert.False(CopyEngine.IsNonFlatDispatchName(null))` — null check | `Assert.False(CopyEngine.IsNonFlatDispatchName(null))` | **PASS** |
| T_B67_04 (line 4187) | `Assert.Empty(result)` — no rules for phantom instrument | `Assert.NotNull(result); Assert.Empty(result)` | **PASS** |

All 10 spot-checks **PASS**.

---

## V3: Test Run Results

`dotnet test` cannot execute outside NT8 host due to pre-existing build errors in
`AtrSizingEngine.cs` (lines 20, 24 — `NinjaTrader.NinjaScript.Indicators` namespace requires
NT8 runtime assembly). This is the established B-series pattern (same as B67-LaneA, B71-LaneA).

Pre-existing build errors (independent of this ticket — unchanged across all commits):
```
AtrSizingEngine.cs(20,31): error CS0234: 'Indicators' not in namespace NinjaTrader.NinjaScript  [PRE-EXISTING]
AtrSizingEngine.cs(24,36): error CS0246: 'Indicator' not found                                  [PRE-EXISTING]
```

**No new errors introduced by CopyEngineB75Tests.** All 46 runnable tests verified present
via `Select-String` scan. Test runtime is NT8 F5 gate (in-host execution).

**V3: PASS** (pre-existing build errors confirmed unchanged; 0 new errors from this ticket).

---

## V4: Scan Results (Layer 3 -- Independent)

All scans run independently on `src/PropTraderTools/CopyEngineTests.cs`.

| Scan | Command | Result | Verdict |
|------|---------|--------|---------|
| SCAN-01a: `lock(` | `Select-String ... -Pattern "lock\s*\(" \| Where-Object { $_.Line -notmatch "//" }` | **0 hits** | PASS |
| SCAN-01b: `async void` | `Select-String ... -Pattern "async\s+void\s+\w+\("` | **0 hits** | PASS |
| SCAN-02: Non-ASCII (B75 section) | Byte-level UTF8 length check lines 3661-4260 | **0 hits** | PASS |
| SCAN-03: `throw new XxxException` | `Select-String ... -Pattern "throw\s+new\s+\w+Exception"` | **0 hits** | PASS |
| SCAN-06: `DateTime.Now[^U]` | `Select-String ... -Pattern "DateTime\.Now[^U]"` | **0 hits** | PASS |

Layer 2 vs Layer 3 comparison: Engineer reported 0 hits on all scans. Layer 3 confirms 0 hits.
**No discrepancy.**

---

## DNA Rule Check

| Rule | Applies To | Status |
|------|-----------|--------|
| JS-021: no `lock()` | Test class | **PASS** — 0 hits |
| JS-001: no `throw new XxxException` | Test class | **PASS** — 0 hits |
| JS-002: no `return null` | All methods void | **PASS** — N/A |
| JS-033: no `async void` | Test class | **PASS** — 0 hits |
| NT8: no `FontFamily` in WPF | Test file (no XAML) | **PASS** — N/A |
| NT8: no `#RRGGBB` hex colors | Test file | **PASS** — not applicable |
| NT8: xUnit only | All attributes `[Fact]` | **PASS** — no NUnit/MSTest |
| ASCII-only | String literals | **PASS** — 0 non-ASCII bytes in B75 section |
| CYC <= 8 | All test methods | **PASS** — all methods CYC <= 2 (single Assert path) |

---

## Architecture Compliance

| Check | Status |
|-------|--------|
| Class appended inside existing `namespace PropTraderTools` block | PASS |
| `CopyEngineB75Tests` implements `IDisposable` (engine teardown) | PASS |
| Helper `InvokeTryDispatchLeaderFlat` uses reflection (`BindingFlags.NonPublic | Static`) for private method access | PASS |
| `CopyRule.Create(...)` internal factory used (no public constructor violation JS-010) | PASS |
| All 14 NT8-runtime skips carry descriptive `Skip=` message | PASS |
| No `NotImplementedException` stubs left — all test bodies contain real assertions or Skip | PASS |

---

## Verdict

**VERIFY_PASS**

- V1: 46 runnable + 14 skipped = 60 total [Fact] methods ✓
- V2: All 10 spot-checks pass — correct Assert direction for every reviewed test ✓
- V3: No new build errors; pre-existing AtrSizingEngine.cs errors unchanged (established B-series pattern) ✓
- V4: SCAN-01a lock=0, SCAN-01b async void=0, SCAN-02 non-ASCII=0, SCAN-03 throw=0, SCAN-06 DateTime.Now=0 ✓
- Layer 2 vs Layer 3: No discrepancies — engineer self-report confirmed accurate ✓