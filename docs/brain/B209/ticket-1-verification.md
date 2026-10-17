# B209 Ticket-1 Verification Report
# FIX-G-1: TryClearBeSwapOnStopWorking + IsBeClearStateTrigger Seam

**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-07-07
**Commit verified**: a93e222775c4dbd0f1e858238655ac46ad9ddae8
**Branch**: main
**Scope**: Ticket-1 ONLY (FIX-G-1)

---

## VERDICT

**VERIFY_PASS**

The Ticket-1 implementation is correct, surgical, and compliant with all Jane Street DNA rules.
The guardian script single-hunk flag is a known false positive for adjacent changes (documented
below). The TradeCopierPanel BrushTeal failure is pre-existing and unrelated to Ticket-1.

---

## Engineer Layer 2 Report Cross-Check

Engineer (ptt-engineer) self-reported Layer 2 scan results in `ticket-1-completion.md`.
All Layer 3 independent scans below are run independently without trusting Layer 2.

---

## Layer 3 Independent Scans

### SCAN-01: lock() check

**Command run**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("
```

**Output** (all 9 results):
```
src\PropTraderTools\CopyEngine.cs:326:  // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
src\PropTraderTools\CopyEngine.cs:360:  // ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
src\PropTraderTools\CopyEngine.cs:2376: // Value: ConcurrentBag<Order> -- thread-safe add, no lock().
src\PropTraderTools\CopyEngine.cs:5069: // ASCII-only. No DateTime.Now. No lock().
src\PropTraderTools\CopyEngine.cs:5093: // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
src\PropTraderTools\CopyEngine.cs:7536: // JS-021: no lock(). GetCloneAtmMode reads volatile field -- thread-safe.
src\PropTraderTools\CopyEngine.cs:7678: // JS-021: no lock(). Interlocked.Decrement is atomic.
src\PropTraderTools\CopyEngine.cs:7752: // CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().
src\PropTraderTools\CopyEngine.cs:7763: // JS-021: no lock(). ConcurrentDictionary enumeration is thread-safe.
```

**Analysis**: All 9 hits are in COMMENTS (describing "no lock()"). Zero actual `lock(` call in executable code.
None in lines 3154-3188 (the modified/added range).

**Result**: PASS
**Cross-check with Layer 2**: Layer 2 reported 0 results for lines 3154-3188. MATCH.

---

### SCAN-02: async void check

**Command run**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void "
```

**Output**:
```
src\PropTraderTools\CopyEngine.cs:7676: // Called directly from OnOrderUpdate -- NOT an event handler.
                                          Synchronous void. NOT async void (JS-033).
```

**Analysis**: Single hit is in a COMMENT. No `async void` executable code anywhere in the file.
None in lines 3154-3188.

**Result**: PASS
**Cross-check with Layer 2**: Layer 2 reported 0 results for lines 3154-3188. MATCH.

---

### SCAN-03: return null check

**Command run**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null"
```

**Output (summary)**: 22 results. Line numbers: 803, 808, 813, 1404, 1564, 2338, 3796, 3896,
3905, 4562, 4612, 4787, 4970, 5536, 5560, 6373, 6379, 6475, 6740, 6768, 7277, 7292.

**Analysis**: None of these lines fall in the Ticket-1 modified range 3154-3188.
- `TryClearBeSwapOnStopWorking` (lines 3160-3174): void method. Returns are `return;` (void). PASS.
- `IsBeClearStateTrigger` (lines 3179-3187): bool method. Returns are `return false;` and `return state == ...`. PASS.

**Result**: PASS (0 violations in modified/added methods)
**Cross-check with Layer 2**: Layer 2 reported 0 results for lines 3154-3188. MATCH.

---

### SCAN-04: CYC independent calculation

#### TryClearBeSwapOnStopWorking (lines 3160-3174) — live source:
```csharp
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
{
    if (e.Order.Name == null
        || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) // (1)
        return;
    bool isClearState = e.Order.OrderState == OrderState.Working    // (2a) normal: stop live
        || e.Order.OrderState == OrderState.Cancelled               // (2b) OCO: target filled
        || e.Order.OrderState == OrderState.Rejected                // (2c) bad price: evict+retry
        || e.Order.OrderState == OrderState.Accepted;               // (2d) SIM: mirrors FIX-F-A
    if (!isClearState)
        return;
    if (!IsBeSwapInProgress(e.Order.Account?.Name)) // (3)
        return;
    SetBeSwapInProgress(e.Order.Account.Name, false);
}
```

**Strict McCabe branch count**:
| Decision Point | Count |
|----------------|-------|
| Base method | 1 |
| `if (Name == null` | +1 |
| `\|\| !StartsWith(...)` | +1 |
| `\|\| OrderState.Cancelled` | +1 |
| `\|\| OrderState.Rejected` | +1 |
| `\|\| OrderState.Accepted` | +1 |
| `if (!isClearState)` | +1 |
| `if (!IsBeSwapInProgress(...))` | +1 |
| **TOTAL** | **8** |

**Independent CYC = 8. At limit, within spec max (<=8). PASS.**

Note: Architecture plan stated CYC=7 (using +1 for `if (A || B)` compound). Engineer reported
CYC=8 (strict Lizard: counts if=+1 AND ||=+1 separately). Verifier independently confirms 8.
Discrepancy between plan and engineer is in counting methodology only, not implementation.
Both are <= 8. PASS regardless.

#### IsBeClearStateTrigger (lines 3179-3187) — live source:
```csharp
internal static bool IsBeClearStateTrigger(string orderName, OrderState state)
{
    if (orderName == null || !orderName.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
        return false;
    return state == OrderState.Working
        || state == OrderState.Cancelled
        || state == OrderState.Rejected
        || state == OrderState.Accepted;
}
```

**Strict McCabe branch count**:
| Decision Point | Count |
|----------------|-------|
| Base method | 1 |
| `if (orderName == null` | +1 |
| `\|\| !StartsWith(...)` | +1 |
| `\|\| OrderState.Cancelled` | +1 |
| `\|\| OrderState.Rejected` | +1 |
| `\|\| OrderState.Accepted` | +1 |
| **TOTAL** | **6** |

**Independent CYC = 6. Well within spec max (<=8). PASS.**
Cross-check: Engineer reported CYC=6. MATCH.

---

### SCAN-05: throw new check

**Command run**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new"
```

**Output**: (no output -- 0 results)

**Result**: PASS (zero throw new in entire file, including modified/added methods)
**Cross-check with Layer 2**: Layer 2 reported 0 results. MATCH.

---

### SCAN-06: ASCII check (modified/added lines 3154-3188)

**Command run** (PowerShell byte-level check):
```powershell
$lines = Get-Content "src/PropTraderTools/CopyEngine.cs"
$lines[3153..3186] | ForEach-Object {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($_)
    $nonAscii = $bytes | Where-Object { $_ -gt 127 }
    if ($nonAscii) { "Non-ASCII found: $_" }
}
```

**Output**: (no output -- 0 non-ASCII bytes in lines 3154-3187)

**Visual inspection** of all string literals and comments:
- `"PTT-BE-Stop"` (lines 3163, 3181) -- all ASCII 0x20-0x7E. PASS.
- `"B208-FIX-G-1:"` in comments -- hyphens are ASCII 0x2D. PASS.
- `"-- "` in comments -- double-hyphens are ASCII, not em-dash. PASS.
- All other comments -- ASCII-only identifiers and keywords. PASS.

**NT8 conformance scans (independent)**:
- `Select-String -Pattern "FontFamily"` → 0 results in executable code (3 comment hits only). PASS.
- `Select-String -Pattern "#[0-9A-Fa-f]{6}"` → 0 results. PASS.
- `Select-String -Pattern "DateTime\.Now[^U]"` → 0 results in executable code (1 comment hit). PASS.

**Result**: PASS
**Cross-check with Layer 2**: Layer 2 reported PASS. MATCH.

---

### SCAN-07: ptt-sync

**Command run**:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

**Output**:
```
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

**Result**: PASS (19 linked, 0 errors)
**Cross-check with Layer 2**: Layer 2 reported 19 linked, 0 errors. MATCH.

---

## Post-Edit Audits

### Ph4b Deletion Audit

**Command run**:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
```

**Output**: (no output -- 0 results)

**Analysis**: Zero method signatures deleted. The diff shows only:
- 4 old comment lines removed (the `// B208-FIX-F-B` header)
- 1 old body line removed: `if (e.Order.OrderState != OrderState.Working) // (2) return;`
None of these are `private`/`internal`/`public` method signatures.

**Result**: PASS. No DW-card required (body replacement of the bug-bearing line is the explicit
intent of the ticket).
**Cross-check with Layer 2**: Layer 2 reported 0 results. MATCH.

---

### Single-Hunk Ban Check

**Command run**:
```powershell
(git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
```
**Output**: `1`

**Command run (for content analysis)**:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@"
```
**Output**: `@@ -3151,23 +3151,41 @@ namespace PropTraderTools`

**Command run (total diff size)**:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Measure-Object -Line
```
**Output**: `51 lines total`

**Commit metadata**:
```
1 file changed, 23 insertions(+), 5 deletions(-)
```

**Independent ruling on single-hunk ban**:

The hunk count = 1. However, the single-hunk ban rule exists to catch FULL-FILE REPLACES,
not adjacent surgical changes. The evidence rules out a full-file replace:

1. **Diff size**: 51 total diff lines. A full-file replace of CopyEngine.cs (~9000+ lines)
   would produce 9000+ diff lines. 51 lines = definitively surgical.

2. **Hunk scope**: `@@ -3151,23 +3151,41 @@` covers only lines 3151-3192 of a 9000+ line file.
   A full-file replace hunk would start at line 1.

3. **Change content**: The diff shows exactly two adjacent changes:
   - Comment header update + body replacement in `TryClearBeSwapOnStopWorking`
   - Insertion of `IsBeClearStateTrigger` method immediately after
   These two changes are 0 lines apart, so git's 3-line context window merges them into one hunk.

4. **Commit stats**: 23 insertions, 5 deletions. Full-file replace would show ~9000 each.

**Root cause of single-hunk**: The `apply_diff` + `insert_content` tooling produced two adjacent
changes (closing `}` of TryClearBeSwapOnStopWorking at line 3174 directly followed by the new
`IsBeClearStateTrigger` method at line 3176). Git context merges adjacent hunks automatically.

**RULING**: This is NOT a full-file replace. The single-hunk count is a false positive caused by
adjacent surgical changes being merged by git context. The spirit and letter of the single-hunk ban
("this indicates a full-file replace was committed") is not violated. The diff evidence is
unambiguous. **PASS on substance.**

**Guardian script single-hunk flag**: The `verify_critical_symbols.ps1` script fires the
`SINGLE-HUNK BAN` error using the same `$hunkCount -eq 1` check. This is the same false positive.
The script cannot distinguish between hunk count=1 due to full-file replace vs. hunk count=1 due
to adjacent surgical changes. The script's conclusion ("full-file replace") is incorrect for this
commit. The Director should be aware the guardian script needs logic improvement (e.g., also check
diff line count or hunk scope before firing the ban).

---

### Guardian Symbol Check

**Command run**:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

**Output**: GUARDIAN FAIL with two issues:
1. `SINGLE-HUNK BAN: CopyEngine.cs diff is a full-file replace.` (exit 1)
2. `TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)` (exit 1)

**Analysis of each issue**:

**Issue 1 — SINGLE-HUNK BAN (script false positive)**:
See Single-Hunk Ban section above. The script fires because `$hunkCount -eq 1`, but this is a
known false positive for adjacent surgical changes. The diff is 51 lines total, scoped to lines
3151-3192 in a 9000+ line file. Not a full-file replace.

All CopyEngine.cs B183 and permanent guardian symbols ARE present:
- `_followerSlateReady`, `TryMarkFollowerSlateNotReady`, `TrySetFollowerSlateReady` -- present
- `IsAccountFlat`, `HasAnyActiveOrders`, `OnFollowerPositionUpdate` -- present
- `FlatGuardLoop`, `TryLaneDHook`, `_laneDDispatched`, `TryLaneDDispatchFollower` -- present
- `TryLaneEHook`, `_laneEDispatched`, `TryLaneEDispatchFollower` -- present
- `BuildLaneEKey`, `IsLaneEEligible`, `PositionUpdate` -- present
- `FindFollowerBracketOrder`, `EvictDedup`, `TryCancelFollowerEntries` -- present
- `CancelScopedFollowerEntries`, `IsLiveEntryBlocked`, `IsReversalToFlatFollower` -- present
- `ShouldSkipForReversalGuard`, `TryDispatchLeaderFlat`, `DispatchCopy` -- present
- `OnOrderUpdate`, `Subscribe`, `Unsubscribe` -- present

**Issue 2 -- TradeCopierPanel BrushTeal (pre-existing)**:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Measure-Object -Line
# Output: 0 lines
```
Ticket-1 commit (a93e222) has ZERO changes to TradeCopierPanel.cs. The BrushTeal failure
pre-dates this ticket. This is a pre-existing gap, NOT caused by Ticket-1.

**Conclusion**: The guardian script exit=1 is caused by (1) a script false positive on the
single-hunk check and (2) a pre-existing gap in TradeCopierPanel.cs. Neither issue is a Ticket-1
regression. All CopyEngine.cs guardian symbols are present. The guardian's functional intent
(symbol presence + deletion audit + commit size) is satisfied. **PASS on substance.**

**Action item**: Guardian script single-hunk logic needs improvement. Director notified.

---

## Implementation Correctness Checks

### TryClearBeSwapOnStopWorking (lines 3160-3174) — full body verified:

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| `isClearState` bool with Working\|Cancelled\|Rejected\|Accepted | YES | Present at lines 3165-3168 | PASS |
| Comment "B208-FIX-G-1:" | YES | Line 3154: "// B208-FIX-G-1:" | PASS |
| Method is `private void` | YES | Line 3160: `private void TryClearBeSwapOnStopWorking(OrderEventArgs e)` | PASS |
| `SetBeSwapInProgress` called at end | YES | Line 3173: `SetBeSwapInProgress(e.Order.Account.Name, false);` | PASS |
| `IsBeSwapInProgress` guard present | YES | Line 3171: `if (!IsBeSwapInProgress(e.Order.Account?.Name))` | PASS |
| `StringComparison.Ordinal` on StartsWith | YES | Line 3163 | PASS |

### IsBeClearStateTrigger (lines 3179-3187) — full body verified:

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| `internal static bool` signature | YES | Line 3179 | PASS |
| Parameter types `(string orderName, OrderState state)` | YES | Line 3179 | PASS |
| Returns `false` for null/wrong name | YES | Line 3182: `return false;` | PASS |
| Returns Working\|\|Cancelled\|\|Rejected\|\|Accepted | YES | Lines 3183-3186 | PASS |
| Placement after TryClearBeSwapOnStopWorking | YES | Line 3179, after line 3174 `}` | PASS |
| Placement before `// B200-CLOSE-LANED-01` | YES | Line 3189 is `// B200-CLOSE-LANED-01` | PASS |

### Scope Lock Compliance:

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Only TryClearBeSwapOnStopWorking modified | YES | diff confirms only lines 3154-3174 changed | PASS |
| Only IsBeClearStateTrigger added | YES | diff shows only new method at 3176-3187 | PASS |
| TryCancelSingleStaleGtcExit NOT touched | YES | not in diff | PASS |
| IsBeSwapGtcSweepGuard NOT added | YES | not in file | PASS |
| B209Tests.cs NOT created | YES | file does not exist | PASS |
| No other files modified | YES | TradeCopierPanel.cs diff = 0 lines | PASS |

---

## DNA Rule Check Results

| Rule | Scope | Check | Result |
|------|-------|-------|--------|
| JS-021 (no lock) | TryClearBeSwapOnStopWorking | No `lock(` in executable code | PASS |
| JS-021 (no lock) | IsBeClearStateTrigger | No `lock(` -- pure predicate | PASS |
| JS-001 (no throw) | TryClearBeSwapOnStopWorking | No `throw new` (confirmed file-wide) | PASS |
| JS-001 (no throw) | IsBeClearStateTrigger | No `throw new` | PASS |
| JS-002 (no return null) | TryClearBeSwapOnStopWorking | `void` method -- no return value | PASS |
| JS-002 (no return null) | IsBeClearStateTrigger | Returns `bool` (`false` / expression) | PASS |
| JS-033 (no async void) | Both methods | Synchronous void/bool only | PASS |
| JS-015 (StringComparison.Ordinal) | Both methods | Present on all `StartsWith` calls | PASS |
| ASCII-only | Both methods | Zero non-ASCII bytes in lines 3154-3187 | PASS |
| CYC <= 8 | TryClearBeSwapOnStopWorking | Independent count = 8 (at limit) | PASS |
| CYC <= 8 | IsBeClearStateTrigger | Independent count = 6 | PASS |
| No FontFamily | CopyEngine.cs | 0 executable hits | PASS |
| No #RRGGBB hex | CopyEngine.cs | 0 results | PASS |
| No DateTime.Now | CopyEngine.cs | 0 executable hits | PASS |
| NT8: no sealed on window | N/A (not in scope) | N/A | N/A |
| NT8: no async in lifecycle | N/A (not in scope) | N/A | N/A |

---

## Spec Compliance Verdict

**Spec requirement**: B209-FIX-G-1
**Problem**: `TryClearBeSwapOnStopWorking` only cleared `_beSwapInProgress` on `Working` state.
OCO cancel path (PTT-BE-Stop-N transitioning to `Cancelled`) never triggered the clear, causing
permanent flag leak and NakedPositionDetector suppression.

**Implementation satisfies the spec**: YES
- `Cancelled` state now triggers `SetBeSwapInProgress(accName, false)` via the new `isClearState`
  bool. This directly eliminates the OCO cancel path flag leak.
- `Rejected` and `Accepted` states also covered for completeness.
- `IsBeClearStateTrigger` seam correctly extracts the predicate for Ticket-2 xUnit tests.

**Architecture plan compliance**: YES
- Implementation matches the exact "AFTER" body specified in Fix-G-1 Design section.
- Placement matches: `TryClearBeSwapOnStopWorking` at line 3160, `IsBeClearStateTrigger` at 3179.
- CYC target met: <= 8 for both methods.
- No NT8 constraint violations introduced.

---

## Layer 2 vs Layer 3 Cross-Check Summary

| Scan | Engineer Layer 2 | Verifier Layer 3 | Match? |
|------|-----------------|-----------------|--------|
| SCAN-01 lock() | 0 results in 3154-3188 | 0 executable hits (9 comment hits only) | MATCH |
| SCAN-02 async void | 0 results | 0 executable hits (1 comment hit) | MATCH |
| SCAN-03 return null | 0 results in 3154-3188 | 0 results in 3154-3187 | MATCH |
| SCAN-04 CYC | TryClear=8, IsBeClear=6 | TryClear=8, IsBeClear=6 | MATCH |
| SCAN-05 throw new | 0 results | 0 results (file-wide) | MATCH |
| SCAN-06 ASCII | PASS | 0 non-ASCII bytes | MATCH |
| SCAN-07 ptt-sync | 19 linked, 0 errors | 19 linked, 0 errors | MATCH |
| Ph4b deletion | 0 deleted signatures | 0 deleted signatures | MATCH |
| Single-hunk | 1 hunk (surgical, not full-file) | 1 hunk, 51 diff lines, @@ -3151 | MATCH |
| Guardian | FAIL (pre-existing BrushTeal + false positive single-hunk) | Same 2 failures | MATCH |

**No discrepancies between Layer 2 and Layer 3.** All engineer self-reports verified correct.

---

## Issues to Escalate to Director

1. **Guardian script single-hunk logic**: The `verify_critical_symbols.ps1` script fires the
   SINGLE-HUNK BAN for any hunk count = 1, regardless of diff size or hunk scope. This produces
   false positives for adjacent surgical changes that git context-merges into one hunk. The script
   should be enhanced to additionally check diff line count (e.g., if total diff lines < 300, not
   a full-file replace) or hunk start line (if hunk starts at line 1, likely full-file replace).

2. **TradeCopierPanel BrushTeal** (pre-existing): The Quick2t visual fix
   (`_instr2tBtn.Background = BrushTeal`) is missing from TradeCopierPanel.cs. This is a
   pre-existing gap not caused by Ticket-1. Requires a separate ticket or Director disposition.

---

## Summary

All 7 scans independently verified. All pass. Implementation exactly matches the spec.
No DNA violations. No scope creep. Surgical diff confirmed (51 lines, @@ -3151,23 +3151,41 @@).
Two guardian failures are pre-existing (BrushTeal) and script-logic false positive (single-hunk).

**VERIFY_PASS**