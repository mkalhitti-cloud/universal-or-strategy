# B170 Ticket-2 Verification — DW-B170-01 DwB170Tests Class Insertion

**Ticket:** TICKET B170-2 — Add DwB170Tests class to CopyEngineTests.cs
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Verifier role:** PTT Verifier (Phase 4b) — READ-ONLY access
**Date:** Layer 3 independent verification

---

## Scope

TICKET B170-2 ONLY. Verification of `DwB170Tests` class insertion after line 10257
(closing `}` of `DwB168_02Tests`), before namespace closing `}`.

---

## Independent V1-V7 Results (Layer 3 — Run by Verifier)

### V1. FindLeaderStopPrice_AcceptedStop_ReturnsPrice present

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "FindLeaderStopPrice_AcceptedStop_ReturnsPrice"
```

**Result:**
```
src\PropTraderTools\CopyEngineTests.cs:10275:  public void FindLeaderStopPrice_AcceptedStop_ReturnsPrice()
```

**Verdict:** ? PASS — test method present at line 10275

---

### V2. FindLeaderStopPrice_WorkingStop_StillReturnsPrice present

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "FindLeaderStopPrice_WorkingStop_StillReturnsPrice"
```

**Result:**
```
src\PropTraderTools\CopyEngineTests.cs:10297:  public void FindLeaderStopPrice_WorkingStop_StillReturnsPrice()
```

**Verdict:** ? PASS — regression guard test present at line 10297

---

### V3. DwB170Tests uses FindLeaderStopPriceTestable seam (or equivalent)

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "FindLeaderStopPriceTestable"
```

**Result (all matches):**
```
Line 10285: // Source unavailable -- verify FindLeaderStopPriceTestable seam exists as fallback.
Line 10287:     "FindLeaderStopPriceTestable",
Line 10307: // Source unavailable -- verify FindLeaderStopPriceTestable seam exists as fallback.
Line 10309:     "FindLeaderStopPriceTestable",
```

**Analysis:** The engineer implemented a BCL (Build-Chain-Lock) source-text pattern:
- **Primary path:** `File.ReadAllText(srcFile)` + `Assert.Contains("OrderState.Accepted", src)` — tests that the predicate is in source
- **Fallback path:** reflection check for `FindLeaderStopPriceTestable` seam (lines 10286–10290, 10308–10312) — runs when source file is unavailable

Both uses of `FindLeaderStopPriceTestable` (lines 10287 and 10309) are in string literals passed to `GetMethod()` for reflection-based fallback. This is the established BCL pattern. No direct call to `FindLeaderStopPriceTestable` with NT8 runtime types.

**Verdict:** ? PASS — BCL pattern correctly uses source-text primary + reflection fallback; consistent with adjacent test classes

---

### V4. No NT8 runtime references in DwB170Tests class

**Command:**
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "NinjaTrader\.Cbi\." |
    Where-Object { $_.LineNumber -ge 10258 -and $_.LineNumber -le 10315 }
```

**Result:** (no output — zero matches)

**Verdict:** ? PASS — 0 NT8 runtime references in the DwB170Tests class body

---

### V5. No whitespace mutations outside DwB170Tests class

**Direct source inspection:**

Lines 10250–10258 (before insertion):
```
10255 |         }
10256 |         }
10257 |     }
10258 | (blank)
10259 |     // DW-B170-01: Tests for FindLeaderStopPrice predicate -- Accepted state must return price.
```

Lines 10315–10317 (after insertion):
```
10315 |     }
10316 | (blank)
10317 | }
10318 |
```

`DwB168_02Tests` closing `}` at line 10257 is intact. Namespace closing `}` at line 10317 is intact. Blank separator lines present.

**Verdict:** ? PASS — no mutations outside the `DwB170Tests` insertion block

---

### V6. No existing tests removed or renamed; [Fact] count increases by exactly 2

**Command:**
```powershell
$total = (Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "^\s*\[Fact\]").Count
$inClass = (Select-String ... | Where-Object { LineNumber 10258-10315 }).Count
```

**Result:**
- Total `[Fact]` count in file: **227**
- `[Fact]` in DwB170Tests block (lines 10274 and 10296): **2**
- Engineer reported prior total: 225; delta = +2 ?

`DwB168_02Tests` class verified present at line 10179. All preceding test classes intact.

**Verdict:** ? PASS — exactly 2 new `[Fact]` attributes added; no existing tests removed or renamed

---

### V7. ptt-sync.ps1 NOT required for test-only file

**Source:** Engineer completion artifact (`ticket-2-completion.md`) explicitly states:
> "Not required. `CopyEngineTests.cs` is a test-only file; it is not hard-linked to the NinjaTrader AddOn directory. ptt-sync.ps1 synchronizes NinjaTrader hard links for production `.cs` files in `src/PropTraderTools/` only. ptt-sync.ps1 was NOT run for this ticket."

This is correct per ticket spec (04-tickets.md §TICKET B170-2 ptt-sync.ps1):
> "Not required for test-only file changes."

**Verdict:** ? PASS — ptt-sync.ps1 correctly not run; explicitly documented

---

## Cross-check vs Engineer Layer 2 Report

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy? |
|------|-------------------|-------------------|--------------|
| SCAN-01 (AcceptedStop test) | Line 10275: MATCH | Line 10275: MATCH | NONE |
| SCAN-02 (WorkingStop test) | Line 10297: MATCH | Line 10297: MATCH | NONE |
| SCAN-03 (FindLeaderStopPriceTestable matches) | "Lines 10287, 10309: 2 MATCHES" | Lines 10285, 10287, 10307, 10309: **4 matches** total | ?? MINOR DISCREPANCY (see below) |
| SCAN-04 (NT8 runtime refs) | 0 matches | 0 matches | NONE |
| SCAN-05 (no whitespace mutations) | Only DwB170Tests insertion | Confirmed: clean boundaries | NONE |
| SCAN-06 ([Fact] delta +2) | Total 227, delta +2 | Total 227, delta +2 | NONE |
| SCAN-07 (ptt-sync not run) | Not required; stated | Not required; stated | NONE |

### SCAN-03 Discrepancy Assessment

**Engineer reported:** "Lines 10287, 10309: 2 MATCHES"
**Verifier found:** 4 matches total at lines 10285, 10287, 10307, 10309

**Root cause:** Engineer counted only the string-literal lines (10287, 10309 — inside `GetMethod()` calls). The comment lines (10285, 10307 — `// Source unavailable -- verify FindLeaderStopPriceTestable seam exists as fallback.`) were not counted.

**Compliance impact:** **NONE.** The two comment lines (10285, 10307) are not functional code. They contain the word "FindLeaderStopPriceTestable" in a human-readable comment, not in an executable expression. The two functional references (10287, 10309) are string literals in `GetMethod()` calls for reflection-based fallback — no NT8 runtime types involved. SCAN-04 confirms 0 NT8 runtime references.

**Classification:** MINOR DOCUMENTATION INACCURACY in Layer 2 report. Not a code violation. Does not affect test behavior, DNA compliance, or correctness.

---

## Architecture/Spec Compliance

- Insertion point: after line 10257 (closing `}` of `DwB168_02Tests`) ?
- Class name: `DwB170Tests` ?
- Test names match spec: `FindLeaderStopPrice_AcceptedStop_ReturnsPrice`, `FindLeaderStopPrice_WorkingStop_StillReturnsPrice` ?
- BCL source-text pattern (consistent with adjacent classes using `ResolveCopyEngineSource()`) ?
- No new seam required (uses reflection fallback to existing `FindLeaderStopPriceTestable`) ?
- No NT8 runtime types in test body ?
- `[Fact]` with no `Skip` attribute ?
- CYC = 1 per test method (straight-line: resolve ? read/reflect ? assert) ?
- No whitespace mutations outside insertion site ?
- ptt-sync.ps1 not run (test-only file) — explicitly stated ?
- Net line delta: +57 lines (engineer said +57; verifier counted 10259–10315 = 57 lines) ?

---

## DNA Rule Checks

| Rule | Check | Result |
|------|-------|--------|
| JS-021 lock() | No lock( in DwB170Tests | ? PASS |
| JS-001 throw | No throw in test bodies | ? PASS |
| JS-002 return null | No return null (void methods) | ? PASS |
| NT8 runtime refs | 0 NinjaTrader.Cbi references in class | ? PASS |
| ASCII-only | Comment header ASCII-only | ? PASS |
| CYC > 8 | CYC = 1 per test | ? PASS |
| No test Skip | [Fact] no Skip attribute | ? PASS |

---

## Verdict

**VERIFY_PASS**

All 7 independent scans passed. One minor discrepancy in engineer Layer 2 report (SCAN-03 match count: engineer stated 2, verifier found 4). Root cause identified: engineer omitted 2 comment-line occurrences. **This is a documentation inaccuracy only — no code violation, no compliance impact.** Implementation correctly uses the BCL source-text pattern with reflection fallback, matches ticket spec and architecture plan, and satisfies all DNA rules.
