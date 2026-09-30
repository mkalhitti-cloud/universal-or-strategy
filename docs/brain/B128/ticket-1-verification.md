# B128 Ticket 1 Verification Report (Layer 3 — Independent Verifier)

**Block**: B128 — Instrument-scoped QX-Instr (2-target) + BE-Instr buttons  
**Ticket**: 1  
**Verifier**: ptt-verifier (Phase 4b)  
**Date**: 2026  
**Source**: `src/PropTraderTools/TradeCopierPanel.cs` (READ-ONLY)  
**Test File**: `src/PropTraderTools/Tests/B128Tests.cs`

---

## Gate: VERIFY_PASS

---

## Independent Scan Results (Layer 3)

| Scan | Command | Actual Result | Engineer Reported (L2) | Match? | Pass? |
|------|---------|---------------|------------------------|--------|-------|
| SCAN-01 ASCII | PowerShell byte scan: `[System.IO.File]::ReadAllBytes()` iterate for bytes > 0x7F | **0 non-ASCII bytes** in TradeCopierPanel.cs. `\u25B2`/`\u25BC` are C# Unicode escape sequences (pure ASCII text `\`, `u`, `2`, `5`...) — not raw Unicode bytes. | 0 non-ASCII in new code; pre-existing escape seqs OK | YES | PASS |
| SCAN-02 lock() | `Select-String -Pattern "lock\(" TradeCopierPanel.cs` | **1 match at L1449** — comment `// JS-021: no lock(). JS-033:...`. No actual `lock(` statement. 0 in B128 methods. | 1 match at L1449 (comment), 0 actual lock() in new methods | YES | PASS |
| SCAN-03 async void | `Select-String -Pattern "async void " TradeCopierPanel.cs` | **3 matches** — L1733 (comment), L1889 (comment), L2348 (comment). All in comment text. 0 actual `async void` declarations in B128 methods. | 3 matches all in comments, 0 actual async void | YES | PASS |
| SCAN-04 return null | `Select-String -Pattern "return null;" TradeCopierPanel.cs` | **6 matches** — L505, L565, L570, L574 (pre-existing code, before B128 range); L2090, L2100 (pre-existing `FindWorkingOrder` B41 method, after B128 range ends at L2032). **0 in B128 methods** (L1354-L2032). | 0 in new methods | YES | PASS |
| SCAN-05 build | `dotnet build src/PropTraderTools/PropTraderTools.csproj --no-restore` | **Build succeeded. 0 Warning(s). 0 Error(s).** Duration: 0.69s | `Build succeeded. 0 Warning(s). 0 Error(s).` | YES | PASS |
| SCAN-06 CYC | `python scripts/complexity_audit.py` — script NOT present at `scripts/complexity_audit.py`. Independent manual CYC count from source. | **Script absent (confirmed pre-existing DW-PTT-BE-FIX-03 note).** Manual count: BuildInstrRow=1, ComputeInstrSplit=1, OnInstrQxClick=3, OnInstrQxUp=2, OnInstrQxDown=2, OnInstrBeClick=3. All <= 8. | Manual: same counts, all <= 8 | YES | PASS |
| SCAN-07 tests | `dotnet test --filter "FullyQualifiedName~B128Tests"` | **Passed! — Failed: 0, Passed: 4, Skipped: 0, Total: 4**, Duration: 424ms | 4 passed, 0 failed | YES | PASS |

---

## Acceptance Criteria (Source-Based)

| AC | Check | Source Evidence | Result |
|----|-------|-----------------|--------|
| AC-01 | `_instrRowPanel` in `root.Children` AFTER `_beRowPanel` BEFORE `_quickRowPanel` | `TradeCopierPanel.cs` L920-L923: `root.Children.Add(_beRowPanel)` (L920) → `BuildInstrRow()` (L921) → `root.Children.Add(_instrRowPanel)` (L922) → `root.Children.Add(_quickRowPanel)` (L923). Order is correct. | PASS |
| AC-02 | `"[PTT-QX-INSTR]"` log prefix in `OnInstrQxClick` | `TradeCopierPanel.cs` L1983: `"[PTT-QX-INSTR] button: "` confirmed present in `NinjaTrader.Code.Output.Process(...)` call. | PASS |
| AC-03 | `"[PTT-BE-INSTR]"` log prefix in `OnInstrBeClick` | `TradeCopierPanel.cs` L2023: `"[PTT-BE-INSTR] button: "` confirmed present in `NinjaTrader.Code.Output.Process(...)` call. | PASS |
| AC-04 | `_engine.ArmPendingBe` (not `CopyEngine.Instance?.GlobalBe?.ArmPendingBe`) in `OnInstrBeClick` | `TradeCopierPanel.cs` L2031: `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer); // (3)` — uses `_engine` field reference. No `CopyEngine.Instance` pattern. | PASS |
| AC-05 | `ComputeInstrSplit` is `internal static` | `TradeCopierPanel.cs` L1415: `internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1) =>` — both modifiers confirmed. | PASS |
| AC-06 | 4 `[Fact]` tests, correct assertions, xUnit only, no NUnit/MSTest | `B128Tests.cs`: 4 `[Fact]` methods: `QxInstrSplit_Even_T1EqualT2`, `QxInstrSplit_Odd_T1Heavier`, `QxInstrSplit_One_BothOne`, `QxInstrSplit_Large_Odd`. Assert.Equal: (4)→(2,2), (5)→(3,2), (1)→(1,0), (7)→(4,3). `using Xunit;` only. Test run: 4/4 passed. | PASS |
| AC-07 | All 7 scans zero/pass | All 7 independent Layer 3 scans confirmed PASS (see table above). | PASS |

---

## Method Signatures Verified Against Ticket Spec

| Method | Spec Signature | Actual (TradeCopierPanel.cs) | Match? |
|--------|---------------|------------------------------|--------|
| `BuildInstrRow` | `private void BuildInstrRow()` | L1354: `private void BuildInstrRow()` | YES |
| `ComputeInstrSplit` | `internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1)` | L1415: `internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1)` | YES |
| `OnInstrQxClick` | `private void OnInstrQxClick(object sender, RoutedEventArgs e)` | L1976: `private void OnInstrQxClick(object sender, RoutedEventArgs e)` | YES |
| `OnInstrQxUp` | `private void OnInstrQxUp(object sender, RoutedEventArgs e)` | L1998: `private void OnInstrQxUp(object sender, RoutedEventArgs e)` | YES |
| `OnInstrQxDown` | `private void OnInstrQxDown(object sender, RoutedEventArgs e)` | L2006: `private void OnInstrQxDown(object sender, RoutedEventArgs e)` | YES |
| `OnInstrBeClick` | `private void OnInstrBeClick(object sender, RoutedEventArgs e)` | L2018: `private void OnInstrBeClick(object sender, RoutedEventArgs e)` | YES |

---

## New Fields Verified

| Field | Spec | Actual (TradeCopierPanel.cs) | Match? |
|-------|------|------------------------------|--------|
| `_instrQxBtn` | `private Button _instrQxBtn = null;` | L270: `private Button _instrQxBtn = null;` | YES |
| `_instrBeBtn` | `private Button _instrBeBtn = null;` | L271: `private Button _instrBeBtn = null;` | YES |
| `_instrRowPanel` | `private UniformGrid _instrRowPanel = null;` | L272: `private UniformGrid _instrRowPanel = null;` | YES |
| `_instrQxT1` | `private int _instrQxT1 = 4;` | L273: `private int _instrQxT1 = 4;` | YES |

---

## csproj Compile Entry Verified

`src/PropTraderTools/PropTraderTools.csproj` L154: `<Compile Include="Tests\B128Tests.cs" />` — confirmed present. ✅

---

## DNA Rules Check (Jane Street — All B128 Methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — no lock() | 0 actual lock() in B128 methods (L1354-L2032) | PASS |
| JS-033 — no async void | 0 async void declarations in B128 methods | PASS |
| JS-001 — no throw in hot path | 0 throw statements in B128 methods | PASS |
| JS-002 — no return null | 0 return null in B128 methods (ComputeInstrSplit returns value tuple; handlers return void) | PASS |
| ASCII-only identifiers | All new identifiers are ASCII | PASS |
| ASCII-only string literals | "[PTT-QX-INSTR]", "[PTT-BE-INSTR]", "QX-Instr", "BE-Instr" are all ASCII | PASS |
| FontFamily ban (NT8) | 0 FontFamily= in new code | PASS |
| Hex color ban (#RRGGBB) | 0 hardcoded hex colors — uses BrushTeal brush reference | PASS |
| DateTime.Now ban | 0 DateTime.Now in new code | PASS |
| sealed ban on TradeCopierWindow | Not applicable to B128 (TradeCopierWindow unchanged) | N/A |
| CYC <= 8 | All 6 methods: 1, 1, 3, 2, 2, 3 — all within budget | PASS |
| CreateOrder "PTT-" prefix | No CreateOrder calls in B128 methods | N/A |

---

## Layer 2 vs Layer 3 Discrepancies

**None.**

All 7 scans match the engineer's Layer 2 self-report exactly. The only documentation note is that the CHANGE 9 table entry in ticket-1-completion.md shows `using NinjaTrader.NinjaScript.AddOns;` (copied from the ticket spec template), but the actual on-disk file contains only `using Xunit;`. The engineer's Notes section clarifies this correctly, and the actual file is correct — all 4 tests pass. This is a template documentation artifact, not a code violation.

---

## Conclusion

**VERIFY_PASS** — all 7 independent Layer 3 scans zero/pass, all 7 acceptance criteria satisfied from source, all method signatures match ticket spec exactly, all 4 tests pass, build clean (0 warnings, 0 errors), no DNA rule violations, no Layer 2 vs Layer 3 discrepancies.