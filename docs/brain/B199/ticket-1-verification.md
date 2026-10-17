# B199 Ticket 1 Verification Report

**Epic**: B199 -- Direct Repair Promotion + B198 Deferred Backlog
**Ticket**: T1 -- DR-5 `EvictDedup` Promotion + 4 xUnit Tests
**Verifier**: ptt-verifier
**Date**: 2026-10-14
**Phase**: 4b (Independent Verification)
**Source**: docs/brain/B199/04-tickets.md (T1 section)
**Engineer report**: docs/brain/B199/ticket-1-completion.md
**Verdict**: VERIFY_PASS

---

## 1. DR-5 Presence Verification

All 5 DR-5 elements independently confirmed in `src/PropTraderTools/CopyEngine.cs`.

| # | DR-5 Element | Line(s) | Confirmed |
|---|-------------|---------|-----------|
| 1 | `internal void EvictDedup(string orderId, OrderState state, string instrKey = null)` | 5920 | YES -- exact match |
| 2 | `[PTT-DR5-EVICT]` log tag | 5942 | YES -- `"[PTT-DR5-EVICT] orderId=" + orderId + " instrKey=" + cancelledInstrKey` |
| 3 | `[PTT-DR5-FALLBACK]` log tag | 5951 | YES -- `"[PTT-DR5-FALLBACK] orderId=" + orderId + " instrKey=" + instrKey` |
| 4 | `_liveEntryInstruments.TryRemove(instrKey, out _)` fallback call | 5949 | YES -- inside `else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))` |
| 5 | `OnOrderUpdate` call site with 3-param EvictDedup | 1555-1559 | YES -- `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState, e.Order.Instrument?.FullName + "|" + e.Order.OrderAction)` |

**Cross-check vs engineer Layer 2**:
- Engineer reported EvictDedup at line 5920 -- CONFIRMED (exact match).
- Engineer reported `[PTT-DR5-EVICT]` at lines 5941-5943 -- verifier found 5942. MINOR DISCREPANCY (1 line offset; content correct, engineer included 3-line Output.Process block boundary).
- Engineer reported `[PTT-DR5-FALLBACK]` at lines 5950-5953 -- verifier found 5951. MINOR DISCREPANCY (1 line offset; same reason).
- Engineer reported fallback block at lines 5945-5954 -- verifier confirmed `else if` at 5945, TryRemove at 5949. CONFIRMED.
- Engineer reported OnOrderUpdate call at lines 1555-1559 -- CONFIRMED (exact match).

Line offset discrepancies are cosmetic (1 line): Output.Process line vs surrounding block.
Content and structure are correct. No substantive discrepancy.

---

## 2. Test File Verification

**File**: `src/PropTraderTools/Tests/B199Tests.cs`
**Present**: YES
**Registered in .csproj**: YES -- line 43: `<Compile Include="Tests\B199Tests.cs" />`

### Test Methods Found (4 [Fact] tests)

| Test Name | [Fact]? | What It Verifies |
|-----------|---------|-----------------|
| `T1_DR5_01_EvictDedup_Cancelled_NormalPath_RemovesBothDicts_EmitsEvictLog` | YES | `_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)` + `_liveEntryInstruments.TryRemove(cancelledInstrKey, out _)` + `[PTT-DR5-EVICT]` present |
| `T1_DR5_02_EvictDedup_Cancelled_FallbackPath_RemovesLiveEntry_EmitsFallbackLog` | YES | `else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))` + `_liveEntryInstruments.TryRemove(instrKey, out _)` + `[PTT-DR5-FALLBACK]` present |
| `T1_DR5_03_EvictDedup_Cancelled_NullInstrKey_NoOp_NoFallback` | YES | `instrKey != null && _liveEntryInstruments.ContainsKey(instrKey)` compound guard + `string instrKey = null` default param |
| `T1_DR5_04_EvictDedup_Filled_DoesNotRemoveLiveEntry_RemovesCompanionDict` | YES | `if (state == OrderState.Filled)` + `_entryInstrKeyByOrderId.TryRemove(orderId, out _)` + "Do NOT remove _liveEntryInstruments key -- trade is live." comment |

**Test seam pattern**: Source-text assertions (consistent with B197Tests.cs and B198Tests.cs pattern).
CopyEngine cannot be instantiated in xUnit host (NT8 TypeInitializationException).
All 4 tests call `Src()` helper that reads CopyEngine.cs via `File.ReadAllText` and asserts structural presence.

**Framework compliance**:
- All 4 tests use `[Fact]` attribute -- CONFIRMED
- No `[Test]` attributes -- CONFIRMED
- No `[TestMethod]` attributes -- CONFIRMED
- No NUnit using statements -- CONFIRMED
- No MSTest using statements -- CONFIRMED
- `using Xunit;` present at line 8 -- CONFIRMED

---

## 3. Independent 7-Scan Results

### SCAN-01: lock() check on B199Tests.cs

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "lock\(" -CaseSensitive
Output: (no output)
Hits: 0
```

**vs Engineer Layer 2**: Engineer reported 0. MATCH. **PASS.**

---

### SCAN-02: async void check on B199Tests.cs

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "async void"
Output: src\PropTraderTools\Tests\B199Tests.cs:7:// JS-021: no lock. JS-033: no async void. ASCII-only.
Hits: 1 (comment only -- no actual async void declaration)
```

**vs Engineer Layer 2**: Engineer reported comment match on line 7. EXACT MATCH. **PASS** (comment-only hit; no actual async void method declarations).

---

### SCAN-03: CYC check via Lizard (CopyEngine.cs, EvictDedup)

```
Command: & "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe" -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8 --warnings_only
EvictDedup result (line 5920): CCN=9
```

**Lizard CCN=9 breakdown** (independent count):
- base = 1
- `state != OrderState.Filled` (&&) = +1
- `&& state != OrderState.Cancelled` (&&) = +1
- `&& state != OrderState.Rejected` (&&) = +1
- `if (state == OrderState.Cancelled)` = +1
- `if (_entryInstrKeyByOrderId.TryRemove(...))` = +1
- `else if (instrKey != null ...)` = +1
- `&& _liveEntryInstruments.ContainsKey(instrKey)` = +1
- `if (state == OrderState.Filled)` = +1
- Total Lizard CCN = 9

**McCabe strict CYC=6** (compound conditions count as 1 decision):
- base = 1
- terminal-guard compound condition = +1
- `if (state == OrderState.Cancelled)` = +1
- `if (_entryInstrKeyByOrderId.TryRemove(...))` = +1
- `else if (instrKey != null && ContainsKey)` = +1
- `if (state == OrderState.Filled)` = +1
- Total strict McCabe CYC = 6

**Critical context**: T1 makes ZERO changes to CopyEngine.cs. This CCN=9 is a pre-existing
measurement confirmed present before T1 and unchanged by T1. The ticket-review (TICKET_REVIEW_PASS)
documents CYC=6 using strict McCabe counting. JS-066 binding constraint is CYC <= 8. The Lizard
CCN=9 vs strict McCabe CYC=6 discrepancy is documented in 04-ticket-review.md (Note 1).

**vs Engineer Layer 2**: Engineer reported Lizard CCN=9. EXACT MATCH.
**PASS (pre-existing; T1 introduced zero changes to CopyEngine.cs; strict McCabe CYC=6 < 8).**

---

### SCAN-04: ASCII check on B199Tests.cs

```
Command: Get-Content "src/PropTraderTools/Tests/B199Tests.cs" | Select-String -Pattern "[^\x00-\x7F]"
Output: (no output)
Hits: 0
```

**vs Engineer Layer 2**: Engineer reported 0. MATCH. **PASS.**

---

### SCAN-05: xUnit check on B199Tests.cs (no NUnit/MSTest)

```
Command: Select-String -Path "src/PropTraderTools/Tests/B199Tests.cs" -Pattern "\[Test\]|\[TestMethod\]"
Output: (no output)
Hits: 0
```

**vs Engineer Layer 2**: Engineer reported 0. MATCH. **PASS.**

---

### SCAN-06: throw new / DateTime.Now in CopyEngine.cs

```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new|DateTime\.Now[^U]"
Output: src\PropTraderTools\CopyEngine.cs:4748:        // ASCII-only. No DateTime.Now. No lock().
Hits: 1 (comment only -- no actual throw new or DateTime.Now in code)
```

**vs Engineer Layer 2**: Engineer reported comment match on line 4748. EXACT MATCH.
**PASS** (comment-only hit; T1 makes no changes to CopyEngine.cs; no violations in EvictDedup scope).

---

### SCAN-07: PTT sync

```
Command: powershell -File .\scripts\ptt-sync.ps1
Output:
  LINKED  CopyEngine.cs
  LINKED  FeatureFlags.cs
  LINKED  LicenseClient.cs
  LINKED  PttTickMath.cs
  LINKED  TradeCopierAddOn.cs
  LINKED  TradeCopierPanel.cs
  LINKED  TradeCopierWindow.cs
  LINKED  Core\PttContracts.cs
  LINKED  Features\PttBeSwapAll.cs
  LINKED  Features\PttBeSwapOne.cs
  LINKED  Features\PttBreakEvenSwap.cs
  LINKED  Features\PttCancel.cs
  LINKED  Features\PttCopier.cs
  LINKED  Features\PttFlatten.cs
  LINKED  Features\PttFollowerStrategy.cs
  LINKED  Features\PttGlobalBreakEven.cs
  LINKED  Features\PttGlobalQuickExit.cs
  LINKED  Features\PttQuickExit.cs
  LINKED  Features\PttTrim.cs

  ptt-sync complete: 19 linked, 0 errors
```

**vs Engineer Layer 2**: Engineer reported 19 linked, 0 errors. EXACT MATCH. **PASS.**

---

### SCAN-08: Guardian Symbols (verify_critical_symbols.ps1)

```
Command: powershell -File .\scripts\verify_critical_symbols.ps1
Exit code: 1
Output:
  GUARDIAN FAIL -- missing symbols and/or gate violations:
  - TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
```

**Assessment**: This is the pre-existing `DW-B198-GUARDIAN-01` failure (P2, TradeCopierPanel BrushTeal).
Per ticket-review Note 2 and B199-orchestrator-prompt.md (Deferred to B200):
"DW-B198-GUARDIAN-01 | P2 | TradeCopierPanel.cs not touched this block"
This failure is a documented carry-forward and **NOT a B199 T1 blocker**.
The failure is in `TradeCopierPanel.cs`, which T1 does not touch.

**vs Engineer Layer 2**: Engineer reported this exact pre-existing failure. MATCH.
**PASS (pre-existing documented carry-forward; not introduced by T1).**

---

## 4. Deletion Audit (AGENTS.md Mandate)

```
Command: git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
Output: (no output -- 0 deleted symbols)
```

**Assessment**: T1 makes ZERO changes to CopyEngine.cs. No symbols deleted. Consistent with engineer
report. Last commit (HEAD) is a docs commit (`docs(brain): B194-DIRECT stop fix`); B199 files
(B199Tests.cs et al.) are untracked/uncommitted at time of verification. **PASS.**

---

## 5. Single-Hunk Ban Check (AGENTS.md Mandate)

```
Command: (git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
Output: 0
```

**Assessment**: CopyEngine.cs is not in the most recent commit (returns 0 hunks -- file not touched).
T1 is a verify-only ticket. No full-file replace possible. **N/A / PASS.**

---

## 6. Dotnet Test Results

```
Command: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B199Tests"
Output:
  Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 199 ms
```

All 4 [Fact] tests PASS.

| Test | Result |
|------|--------|
| `T1_DR5_01_EvictDedup_Cancelled_NormalPath_RemovesBothDicts_EmitsEvictLog` | PASS |
| `T1_DR5_02_EvictDedup_Cancelled_FallbackPath_RemovesLiveEntry_EmitsFallbackLog` | PASS |
| `T1_DR5_03_EvictDedup_Cancelled_NullInstrKey_NoOp_NoFallback` | PASS |
| `T1_DR5_04_EvictDedup_Filled_DoesNotRemoveLiveEntry_RemovesCompanionDict` | PASS |

**Build**: 0 errors, pre-existing CS0436 / xUnit1004 warnings only (same warnings present in all prior tickets). **PASS.**

---

## 7. DNA Rule Checks (Jane Street)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock()) | SCAN-01: 0 lock() in B199Tests.cs | PASS |
| JS-021 (no lock()) | EvictDedup uses ConcurrentDictionary.TryRemove -- lock-free | PASS |
| JS-001 (no throw in hot path) | SCAN-06: 0 throw new in EvictDedup scope | PASS |
| JS-033 (no async void) | SCAN-02: 0 actual async void declarations in B199Tests.cs | PASS |
| JS-033 (no async void) | EvictDedup is synchronous -- no async keyword | PASS |
| JS-066 (CYC <= 8) | Lizard CCN=9 is pre-existing; strict McCabe CYC=6 (T1 zero changes) | PASS (pre-existing, not introduced by T1) |
| JS-081 (ASCII-only) | SCAN-04: 0 non-ASCII in B199Tests.cs | PASS |
| JS-081 (ASCII-only) | `[PTT-DR5-EVICT]` and `[PTT-DR5-FALLBACK]` are ASCII-only | PASS |
| NT8: no lock keyword | Confirmed no lock() in EvictDedup or OnOrderUpdate call site | PASS |
| NT8: no throw in dispatch path | EvictDedup only logs; no throw/rethrow | PASS |
| xUnit only | All 4 tests use [Fact]; no [Test] or [TestMethod] (SCAN-05) | PASS |

---

## 8. Spec Compliance Check

T1 acceptance criteria from B199-orchestrator-prompt.md and 04-tickets.md:

| Criterion | Status |
|-----------|--------|
| 4 [Fact] tests GREEN | PASS -- all 4 pass (dotnet test confirms) |
| EvictDedup CYC <= 8 | PASS -- strict McCabe CYC=6 (pre-existing Lizard CCN=9; zero changes by T1) |
| EvictDedup signature confirmed present with optional instrKey param | PASS -- line 5920 |
| Fallback path confirmed present and correctly guarded | PASS -- lines 5945-5954 |
| OnOrderUpdate call site confirmed passing instrument?.FullName + "|" + OrderAction | PASS -- lines 1555-1559 |
| SCAN-01 through SCAN-07 all PASS | PASS -- all pass |

---

## 9. Layer 2 vs Layer 3 Comparison Summary

| Scan | Engineer Layer 2 | Verifier Layer 3 | Agreement |
|------|-----------------|-----------------|-----------|
| SCAN-01 lock() | 0 hits | 0 hits | MATCH |
| SCAN-02 async void | Comment-only match (line 7) | Comment-only match (line 7) | EXACT MATCH |
| SCAN-03 CYC (Lizard) | CCN=9 pre-existing | CCN=9 confirmed | EXACT MATCH |
| SCAN-04 ASCII | 0 non-ASCII | 0 non-ASCII | MATCH |
| SCAN-05 xUnit-only | 0 [Test]/[TestMethod] hits | 0 hits | MATCH |
| SCAN-06 throw/DateTime.Now | Comment match only (line 4748) | Comment match only (line 4748) | EXACT MATCH |
| SCAN-07 ptt-sync | 19 linked, 0 errors | 19 linked, 0 errors | EXACT MATCH |
| SCAN-08 guardian | DW-B198-GUARDIAN-01 pre-existing failure | Same BrushTeal failure | EXACT MATCH |
| DR-5 elements | All 5 confirmed (with line refs) | All 5 confirmed at stated lines | MATCH |
| Test count | 4 [Fact] tests | 4 [Fact] tests | MATCH |
| Build | 0 errors | 0 errors | MATCH |
| dotnet test | Not explicitly run by engineer | 4/4 PASS (verifier ran) | N/A (verifier added) |

No discrepancies found between engineer Layer 2 report and verifier Layer 3 independent re-run.
Minor cosmetic line-number offsets (1 line) in log tag locations -- content correct.

---

## Violations

None.

---

## Verdict

**VERIFY_PASS**

All 7 scans clean. All 4 [Fact] tests pass. All 5 DR-5 elements confirmed present at stated
line numbers. Zero DNA rule violations introduced by T1. Pre-existing Lizard CCN=9 is a carry-forward
(documented in ticket-review, not introduced by T1; strict McCabe CYC=6 passes JS-066). Guardian
failure (DW-B198-GUARDIAN-01) is a documented P2 carry-forward in TradeCopierPanel.cs (not touched
by B199 T1). CopyEngine.cs unchanged (verify-only ticket). B199Tests.cs created with correct 4
[Fact] tests registered in PropTraderTools.Tests.csproj.

---

*Written by: ptt-verifier*
*Epic: B199*
*Ticket: T1*
*Phase: 4b (Independent Verification)*