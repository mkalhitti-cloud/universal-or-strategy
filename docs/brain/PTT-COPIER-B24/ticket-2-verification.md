# PTT-COPIER-B24 -- Ticket 2 Verification Report
**Phase**: 4b (Verifier)
**Verifier**: ptt-verifier
**Date**: 2026-07-07
**Defect**: DW-B23-BE-ALLACCOUNTS-01
**Ticket Scope**: TradeCopierPanel.cs (5 call-site changes) + CopyEngineTests.cs (2 new [Fact] tests)
**Engineer Layer 2 Report**: ticket-2-completion.md

---

## Verdict

**VERIFY_PASS**

All 7 independent scans passed. All 5 call-site changes confirmed. Both new [Fact] tests confirmed correct.
[Fact] count = 128. One minor L2 over-report noted (non-violating). No DNA violations found.

---

## Check A -- TradeCopierPanel.cs: 5 Call-Site Changes

All 5 call sites independently read from source. Results:

| # | Line | Method | Actual Content | Pass? |
|---|------|--------|----------------|-------|
| 1 | 782 | `OnBeUp` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` | PASS |
| 2 | 791 | `OnBeDown` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` | PASS |
| 3 | 859 | `OnBeConnected` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` | PASS |
| 4 | 1299 | `OnBreakEven` | `_engine.BreakEven(_leaderAccount, _instrument, ticks);` | PASS |
| 5 | 1418 | `DispatchShortcut` Key.B | `_engine.BreakEven(_leaderAccount, _instrument, buf);` | PASS |

**Old 2-param calls eliminated**:
```
Select-String -Pattern "_engine\.BreakEven\(_instrument" -> 0 matches
```
All BreakEven calls in TradeCopierPanel.cs now supply `_leaderAccount` as the first argument.

Full BreakEven call inventory (independent grep):
- Line 782: `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` (3-param) ✓
- Line 791: `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` (3-param) ✓
- Line 859: `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` (3-param) ✓
- Line 1299: `_engine.BreakEven(_leaderAccount, _instrument, ticks);` (3-param) ✓
- Line 1418: `_engine.BreakEven(_leaderAccount, _instrument, buf);` (3-param) ✓
- Line 1293: `private void OnBreakEven(...)` -- method declaration, not a call

**Check A: PASS**

---

## Check B -- CopyEngineTests.cs: 2 New [Fact] Tests

Both tests read directly from source (lines 2272-2307).

### Test 1 -- `BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull` (line 2274)
- `[Fact]` attribute: PRESENT (line 2273) ✓
- `public void` declaration: PRESENT ✓
- `Assert.Null(ex)` at line 2281: PRESENT ✓
- `Assert.Equal("PTT-BE: leader null -- BE skipped", received)` at line 2283: PRESENT ✓
- `Record.Exception(() => _engine.BreakEven((Account)null, (Instrument)null, 2))`: PRESENT ✓
- Braces balanced: ✓

### Test 2 -- `BreakEven_AccountOverload_NullInstrument_NoException` (line 2287)
- `[Fact]` attribute: PRESENT (line 2286) ✓
- `public void` declaration: PRESENT ✓
- `Assert.Null(skipEx)` (no-account path, line 2296): PRESENT ✓
- `Assert.Null(ex)` (stub path, line 2302): PRESENT ✓
- `Record.Exception(...)` pattern: PRESENT ✓
- Braces balanced: ✓
- Class closes at line 2306, namespace at 2307: ✓

### [Fact] Count (SCAN-06 independent run)
```powershell
Select-String -Path "...\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
Count: 128
```
**Expected: 128 (baseline 126 + 2 new). Actual: 128. PASS**

**Check B: PASS**

---

## Check C -- CYC of Modified Methods

All 5 methods had only their call-site argument extended (2-param → 3-param). No new decision points
(if/else/for/while/case/&&/||) were added to any method by T2.

| Method | Pre-T2 CYC | T2 Change | Post-T2 CYC | <= 8? |
|--------|-----------|-----------|------------|-------|
| `OnBeUp` | 2 | 1 arg added, no branch | 2 | PASS |
| `OnBeDown` | 2 | 1 arg added, no branch | 2 | PASS |
| `OnBeConnected` | 4 (3 guards counted) | 1 arg added, no branch | 4 | PASS |
| `OnBreakEven` | 3 (null guard + TryParse) | 1 arg added, no branch | 3 | PASS |
| `DispatchShortcut` Key.B | unchanged | 1 arg added, no branch | unchanged | PASS |

Note: Engineer's L2 report stated `OnBeConnected` CYC=3. Independent count of actual source gives CYC=4
(3 `if` branches at lines 854, 857, 860). However: (a) this method was not structurally changed by T2,
(b) CYC=4 still satisfies the <=8 constraint, (c) T2 introduced 0 new branches. This is a pre-existing
minor CYC mis-count in the completion doc -- not a T2 violation.

**Check C: PASS**

---

## Check D -- JS Rule Compliance in Changed Code

| Rule | Pattern | Scan Result | Changed Lines? | Pass? |
|------|---------|-------------|---------------|-------|
| JS-021 no `lock(` | `lock\(` | 0 matches in TradeCopierPanel.cs | N/A | PASS |
| JS-033 no `async void` | `async void ` | 0 matches | N/A | PASS |
| JS-002 no `return null` in changed code | `return null;` | 1 match line 353 (pre-existing `FindPriceCanvasPanel`) -- not in changed lines 782/791/859/1299/1418 | No | PASS |
| JS-001 no throw in hot path | no `throw` in test code | `Record.Exception` pattern used (correct) | N/A | PASS |
| NT8-043 no `?.Event -=` | `\?\.\w+\s*-=` | 0 matches | N/A | PASS |
| NT8 FontFamily | `FontFamily` | 0 matches | N/A | PASS |
| NT8 hex color string | `#[0-9A-Fa-f]{6}` | 4 matches at lines 193-196 -- all in code COMMENTS only, not string literals; pre-existing, not introduced by T2 | No | PASS |
| DateTime.Now | `DateTime\.Now[^U]` | 0 matches in TradeCopierPanel.cs or CopyEngineTests.cs | N/A | PASS |

**Check D: PASS**

---

## Check E -- Independent 7-Scan Results

Scans run independently via `execute_command` (ctx_shell WAL collision fallback applied for initial lock scan).

| Scan | Command | Expected | Actual | Pass? |
|------|---------|----------|--------|-------|
| SCAN-01 | `lock\(` in TradeCopierPanel.cs | 0 | 0 | PASS |
| SCAN-02 | `async void ` in TradeCopierPanel.cs | 0 | 0 | PASS |
| SCAN-03 | `return null;` in changed code (lines 782/791/859/1299/1418) | 0 | 0 (line 353 pre-existing, not in changed lines) | PASS |
| SCAN-04 | CYC of 5 modified methods <= 8 | all <= 8 | all <= 8 (max 4) | PASS |
| SCAN-05 | `_engine\.BreakEven\(_instrument` in TradeCopierPanel.cs | 0 | 0 | PASS |
| SCAN-06 | `\[Fact\]` count in CopyEngineTests.cs | 128 | 128 | PASS |
| SCAN-07 | New test syntax: [Fact] + Assert.Null(ex) + correct assertions | Pass | Pass (read lines 2272-2307) | PASS |

**Check E: PASS (all 7 scans)**

---

## Check F -- Cross-Check Engineer's Layer 2 Report

Comparison of engineer's self-reported results (ticket-2-completion.md) vs independent Layer 3 verification:

| Item | Engineer L2 | Verifier L3 | Match? |
|------|------------|-------------|--------|
| Line 782 content | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` | Confirmed identical | YES |
| Line 791 content | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` | Confirmed identical | YES |
| Line 859 content | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` | Confirmed identical | YES |
| Line 1299 content | `_engine.BreakEven(_leaderAccount, _instrument, ticks)` | Confirmed identical | YES |
| Line 1418 content | `_engine.BreakEven(_leaderAccount, _instrument, buf)` | Confirmed identical | YES |
| SCAN-01 lock() | 0 | 0 | YES |
| SCAN-02 async void | 0 | 0 | YES |
| SCAN-03 return null in changed code | 0 | 0 | YES |
| SCAN-05 old 2-param calls | 0 | 0 | YES |
| SCAN-06 [Fact] count | 128 | 128 | YES |
| Test 1 assertions | Assert.Null(ex) + Assert.Equal("PTT-BE: leader null -- BE skipped", ...) | Confirmed at lines 2281-2283 | YES |
| Test 2 assertions | Assert.Null(ex) | Confirmed at lines 2295-2302 | YES |
| CYC of OnBeConnected | Reported CYC=3 | Actual CYC=4 (3 if-branches in source) | MINOR DISCREPANCY |
| Test 1 event unsubscribe line | Completion doc listed `_engine.StatusUpdate -= msg => received = msg;` | NOT present in actual source | MINOR OVER-REPORT |

### Discrepancy Assessment

**Discrepancy 1 -- OnBeConnected CYC mis-count (CYC=3 reported, CYC=4 actual)**
- Severity: NON-VIOLATING
- Reason: Method satisfies CYC<=8 regardless (actual CYC=4). T2 introduced zero new branches.
  The pre-existing method had this complexity before T2. Minor documentation error only.
- Verdict: Not a VERIFY_FAIL

**Discrepancy 2 -- Test 1 event unsubscribe line missing from source**
- Engineer reported cleanup line: `_engine.StatusUpdate -= msg => received = msg;`
- Actual source (lines 2274-2284): test ends without this unsubscribe call
- Severity: NON-VIOLATING
- Reason: Event handler leak in xUnit test scope is not a DNA rule violation. The `_engine`
  instance is per-test (constructor-injected); the lambda cannot escape beyond test lifetime.
  No NT8 rule, JS rule, or spec requirement mandates event unsubscription in unit test bodies.
- Verdict: Not a VERIFY_FAIL

**Check F: PASS (2 minor documentation discrepancies, neither violating)**

---

## Architecture Compliance

| Requirement | Check | Result |
|-------------|-------|--------|
| Only TradeCopierPanel.cs and CopyEngineTests.cs modified by T2 | Git status confirms only these 2 files + CopyEngine.cs (T1) touched | PASS |
| Single-line changes only at the 5 call sites | Confirmed by direct read of each method | PASS |
| _leaderAccount field exists at TradeCopierPanel.cs:120 | Pre-existing field, confirmed in scope of call sites | PASS |
| T1 BreakEven(Account,Instrument,int) overload exists (T2 dependency) | T1 VERIFY_PASS confirmed as prerequisite per ticket | PASS |
| No new helper methods or test infrastructure added | 2 new [Fact] methods only, reuse existing patterns | PASS |

---

## Spec Coverage (T2 Scope)

| Requirement ID | Description | Status |
|----------------|-------------|--------|
| REQ-B24-04 (5 of 6 sites) | 5 TradeCopierPanel.cs call sites updated to 3-param form | COVERED |
| REQ-B24-05 | Test count 126 -> 128 | COVERED (confirmed 128) |
| DW-B23-BE-ALLACCOUNTS-01 | Defect wired end-to-end (T1 overload + T2 call sites) | COVERED |

---

## Summary

| Check | Result |
|-------|--------|
| A -- 5 call-site changes in TradeCopierPanel.cs | PASS |
| B -- 2 new [Fact] tests + count = 128 | PASS |
| C -- CYC of modified methods <= 8 | PASS |
| D -- JS/NT8 rule compliance in changed code | PASS |
| E -- All 7 independent scans | PASS |
| F -- Layer 2 vs Layer 3 cross-check | PASS (2 minor documentation discrepancies, non-violating) |

---

## Final Verdict

**VERIFY_PASS**

*ptt-verifier · PTT-COPIER-B24 · Ticket 2 · 2026-07-07*
