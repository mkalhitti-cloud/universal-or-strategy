# PTT-REPAIRS-04-POST-BUG-F -- Ticket T1 Verification

**Verifier**: PTT Verifier (Phase 4b)
**Epic**: PTT-REPAIRS-04-POST-BUG-F
**Ticket**: T1 -- BUG-F: Clone ATM Per-Instrument Fix (Source Verification)
**Verdict**: **VERIFY_PASS**
**Date**: Independent Layer-3 verification; all scans run by verifier independently.

---

## SCOPE

Ticket type: SOURCE VERIFICATION ONLY -- no .cs edits performed.
Files in scope:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`

Ph4a report: `docs/brain/PTT-REPAIRS-04-POST-BUG-F/ticket-1-completion.md`

---

## LAYER-3 INDEPENDENT READINGS (Steps 1-10)

---

### STEP-01 -- Per-instrument field declarations (lines 142-152)

**Verifier read lines 142-152 from source:**

```
Line 142: // PTT-REPAIRS-04 BUG-F: per-instrument clone ATM cache.
Line 143: // Keyed by instrument FullName -- prevents MES chart ATM from overwriting MGC ATM when
Line 144: // user switches between chart panels. ConcurrentDictionary: lock-free reads/writes. JS-021.
Line 145: private readonly ConcurrentDictionary<string, string> _cloneAtmCacheByInstr =
Line 146:     new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
Line 147: (blank)
Line 148: // PTT-REPAIRS-04 BUG-F: per-instrument clone ATM object cache.
Line 149: // Keyed by instrument FullName -- same rationale as _cloneAtmCacheByInstr.
Line 150: // ConcurrentDictionary<string, AtmStrategy>: lock-free. JS-021. JS-023 N/A (not volatile).
Line 151: private readonly ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy> _cloneAtmObjectByInstr =
Line 152:     new ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>(StringComparer.Ordinal);
```

**Checks:**
- `_cloneAtmCacheByInstr`: `ConcurrentDictionary<string, string>`, `readonly`, `StringComparer.Ordinal` -- CONFIRMED
- `_cloneAtmObjectByInstr`: `ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>`, `readonly`, `StringComparer.Ordinal` -- CONFIRMED

**Old volatile scalars absent:**
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "volatile.*_cloneAtm"` -> zero matches
- Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_cloneAtmCache[^B]|_cloneAtmObject[^B]"` -> zero matches
- No usage of old scalar names (`_cloneAtmCache`, `_cloneAtmObject`) anywhere in file. CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 145 for `_cloneAtmCacheByInstr` -- MATCH (actual line 145)
- Ph4a cited line 151 for `_cloneAtmObjectByInstr` -- MATCH (actual line 151)
- Ph4a reported zero volatile matches -- MATCH

**Result: PASS**

---

### STEP-02 -- `SetCloneAtmCache` signature (line 722)

**Verifier read line 722 from source:**

```
Line 722: internal void SetCloneAtmCache(string instrFullName, string value)
```

First parameter is `instrFullName`. Two-parameter form. CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 722 -- MATCH (actual line 722)
- Ph4a cited exact signature -- MATCH

**Result: PASS**

---

### STEP-03 -- `SetCloneAtmObjectCache` body (lines 730-736)

**Verifier read lines 727-736 from source:**

```
Line 727: // PTT-REPAIRS-04 BUG-F: SetCloneAtmObjectCache -- per-instrument. CYC=1.
Line 728: // instrFullName key prevents MES panel clone click from overwriting MGC ATM object.
Line 729: // JS-021: ConcurrentDictionary indexer write is lock-free atomic. JS-001: no throw.
Line 730: internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)
Line 731: {
Line 732:     if (atmObj != null) // branch (1) -- null means no ATM selected; remove stale entry
Line 733:         _cloneAtmObjectByInstr[instrFullName] = atmObj;
Line 734:     else
Line 735:         _cloneAtmObjectByInstr.TryRemove(instrFullName, out _);
Line 736: }
```

- Signature: `internal void SetCloneAtmObjectCache(string instrFullName, ...)` -- CONFIRMED
- Null branch (TryRemove): PRESENT at line 735
- Non-null path (indexer set): PRESENT at line 733
- CYC = 2 (base=1 + one `if` branch at line 732)

**DOC-NOTE:** Source comment at line 727 reads `// CYC=1` but actual CYC is 2. This is a stale
comment (same category as the GetCloneAtmMode // CYC=2 discrepancy noted in 04-tickets.md line 92).
No functional impact. CYC=2 remains <= 8.

**Ph4a cross-check:**
- Ph4a cited line 730 for signature -- MATCH (actual line 730)
- Ph4a cited line 732 for if-branch -- MATCH (actual line 732)
- Ph4a cited line 735 for TryRemove -- MATCH (actual line 735)
- Ph4a cited line 736 for closing brace -- MATCH (actual line 736)
- Ph4a correctly stated CYC=2 (overriding the stale CYC=1 comment) -- MATCH

**Result: PASS**

---

### STEP-04 -- `GetCloneAtmMode` body (lines 741-753)

**Verifier read lines 738-753 from source:**

```
Line 738: // PTT-REPAIRS-04 BUG-F: GetCloneAtmMode -- per-instrument. CYC=2.
Line 739: // Looks up ATM by instrFullName so each chart panel uses its own cloned ATM.
Line 740: // JS-002: never returns null -- returns Inherit as fallback.
Line 741: internal FollowerAtmMode GetCloneAtmMode(string instrFullName)
Line 742: {
Line 743:     NinjaTrader.NinjaScript.AtmStrategy atmObj;
Line 744:     if (_cloneAtmObjectByInstr.TryGetValue(instrFullName, out atmObj) && atmObj != null) // branch (1)
Line 745:         return new FollowerAtmMode.Named(
Line 746:             _cloneAtmCacheByInstr.TryGetValue(instrFullName, out var tpl) ? tpl : string.Empty,
Line 747:             atmObj
Line 748:         );
Line 749:     string cache;
Line 750:     if (_cloneAtmCacheByInstr.TryGetValue(instrFullName, out cache) && cache.Length > 0) // branch (2)
Line 751:         return new FollowerAtmMode.Named(cache);
Line 752:     return new FollowerAtmMode.Inherit();
Line 753: }
```

- Signature: `internal FollowerAtmMode GetCloneAtmMode(string instrFullName)` -- CONFIRMED at line 741
- `_cloneAtmObjectByInstr.TryGetValue(instrFullName, out atmObj)` -- keyed on instrFullName CONFIRMED
- `_cloneAtmCacheByInstr.TryGetValue(instrFullName, out var tpl)` -- keyed on instrFullName CONFIRMED
- Final fallback: `return new FollowerAtmMode.Inherit()` at line 752 -- never null CONFIRMED
- CYC = 4: base=1, if(line 744)=+1, ternary `?:`(line 746)=+1, if(line 750)=+1

**DOC-NOTE:** Source comment at line 738 reads `// CYC=2` but actual CYC is 4 (simplified McCabe).
Pre-existing doc error acknowledged in architecture plan (plan line 167) and 04-tickets.md (line 92).
No functional impact. CYC=4 <= 8.

**Ph4a cross-check:**
- Ph4a cited line 741 for signature -- MATCH (actual line 741)
- Ph4a cited line 744 for first if-branch -- MATCH (actual line 744)
- Ph4a cited line 746 for ternary -- MATCH (actual line 746)
- Ph4a cited line 750 for second if-branch -- MATCH (actual line 750)
- Ph4a cited line 752 for Inherit fallback -- MATCH (actual line 752)
- Ph4a cited line 753 for closing brace -- MATCH (actual line 753)
- Ph4a correctly stated CYC=4 (overriding stale comment) -- MATCH

**Result: PASS**

---

### STEP-05 -- `DispatchToFollower` call site (line 2672)

**Verifier read line 2672 from source:**

```
Line 2672: var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
```

Third argument is `order.Instrument.FullName` (not a string literal). CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 2672 -- MATCH (actual line 2672)
- Ph4a cited exact snippet -- MATCH

**Result: PASS**

---

### STEP-06 -- `ReplaceFollowerCopyOnAtmCancel` call site (line 4404)

**Verifier read line 4404 from source:**

```
Line 4404: var mode = ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName);
```

Third argument is `cancelledOrder.Instrument.FullName` (not a string literal). CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 4404 -- MATCH (actual line 4404)
- Ph4a cited exact snippet -- MATCH

**Result: PASS**

---

### STEP-07 -- `ResolveAtmMode` 3-param signature (lines 5067-5072)

**Verifier read lines 5063-5072 from source:**

```
Line 5063: // PTT-REPAIRS-04 BUG-F: ResolveAtmMode -- per-instrument. CYC=2.
Line 5064: // instrFullName passed through so GetCloneAtmMode can look up the correct per-instrument ATM.
Line 5065: // Signal/Mirror modes delegate to GetAtmMode (per-rule) -- instrFullName unused there.
Line 5066: // JS-002: never returns null -- all branches return a FollowerAtmMode subtype.
Line 5067: private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)
Line 5068: {
Line 5069:     if (GetCopyMode() == CopyMode.Clone) // branch (1)
Line 5070:         return GetCloneAtmMode(instrFullName);
Line 5071:     return GetAtmMode(rule, accountName);
Line 5072: }
```

- 3 parameters: `rule`, `accountName`, `instrFullName` CONFIRMED
- Clone mode path: `GetCloneAtmMode(instrFullName)` -- passes instrFullName through CONFIRMED
- Signal/Mirror mode path: `GetAtmMode(rule, accountName)` -- unchanged CONFIRMED
- CYC = 2: base=1 + one `if` branch at line 5069

**Ph4a cross-check:**
- Ph4a cited line 5067 for signature -- MATCH (actual line 5067)
- Ph4a cited line 5069 for if-branch -- MATCH (actual line 5069)
- Ph4a cited line 5070 for GetCloneAtmMode call -- MATCH (actual line 5070)
- Ph4a cited line 5071 for GetAtmMode fallback -- MATCH (actual line 5071)
- Ph4a cited line 5072 for closing brace -- MATCH (actual line 5072)

**Result: PASS**

---

### STEP-08 -- `instrKey` assignment (TradeCopierPanel.cs line 1852)

**Verifier read line 1852 from source:**

```
Line 1852: string instrKey = _instrument?.FullName ?? string.Empty;
```

Null-conditional `?.` PRESENT. Null-coalescing `??` PRESENT. CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 1852 -- MATCH (actual line 1852)
- Ph4a cited exact snippet -- MATCH

**Result: PASS**

---

### STEP-09 -- `SetCloneAtmObjectCache` call with `instrKey` (TradeCopierPanel.cs line 1860)

**Verifier read line 1860 from source:**

```
Line 1860: CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj);
```

`instrKey` is first argument. `atmObj` is second. CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 1860 -- MATCH (actual line 1860)
- Ph4a cited exact snippet -- MATCH

**Result: PASS**

---

### STEP-10 -- `SetCloneAtmCache` call with `instrKey` (TradeCopierPanel.cs line 1862)

**Verifier read line 1862 from source:**

```
Line 1862: CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl);
```

`instrKey` is first argument. `tpl` is second. CONFIRMED.

**Ph4a cross-check:**
- Ph4a cited line 1862 -- MATCH (actual line 1862)
- Ph4a cited exact snippet -- MATCH

**Result: PASS**

---

## 7-SCAN RESULTS (Steps 11-17)

All scans run independently by verifier.

---

### STEP-11 -- lock() scan: CopyEngine.cs

**Command run:**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```

**Output:** zero matches (no output).

**Ph4a cross-check:** Ph4a reported zero matches -- MATCH

**Result: PASS** (JS-021 satisfied)

---

### STEP-12 -- lock() scan: TradeCopierPanel.cs

**Command run:**
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```

**Output:** zero matches (no output).

**Ph4a cross-check:** Ph4a reported zero matches -- MATCH

**Result: PASS** (JS-021 satisfied)

---

### STEP-13 -- Non-ASCII scan: CopyEngine.cs

**Command run:**
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```

**Output:** zero matches (no output).

**Ph4a cross-check:** Ph4a reported zero matches -- MATCH

**Result: PASS** (JS-042 satisfied)

---

### STEP-14 -- Non-ASCII scan: TradeCopierPanel.cs

**Command run:**
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "[^\x00-\x7F]"
```

**Output:** zero matches (no output).

**Ph4a cross-check:** Ph4a reported zero matches -- MATCH

**Result: PASS** (JS-042 satisfied)

---

### STEP-15 -- CYC spot-check

Branch counts from verifier's independent readings (STEP-03, STEP-04, STEP-07):

| Method | Verifier CYC | Ph4a CYC | Match? | <= 8? |
|--------|-------------|----------|--------|-------|
| `SetCloneAtmObjectCache` | 2 | 2 | YES | YES |
| `GetCloneAtmMode` | 4 | 4 | YES | YES |
| `ResolveAtmMode` | 2 | 2 | YES | YES |

Maximum CYC = 4. All values <= 8. JS-013: PASS.

**Doc-note (non-blocking):** Two stale source comments exist:
- Line 727: `// CYC=1` for SetCloneAtmObjectCache (actual CYC=2)
- Line 738: `// CYC=2` for GetCloneAtmMode (actual CYC=4)
These are pre-existing doc errors acknowledged in the architecture plan. Both CYC values remain <= 8.

**Ph4a cross-check:** Ph4a CYC values match verifier readings -- MATCH

**Result: PASS** (JS-013 satisfied)

---

### STEP-16 -- Build: CopyEngine.cs / TradeCopierPanel.cs errors

**Command run:**
```powershell
dotnet build Linting.csproj /nologo 2>&1
```

**Note:** Ph4a cited `dotnet build src/PropTraderTools/Linting.csproj /nologo` -- the `.csproj`
is at workspace root (`Linting.csproj`), not in `src/PropTraderTools/`. Verifier used the
correct path (`Linting.csproj`).

**Output analysis:**
Build errors present -- ALL in V12_002.* files (pre-existing NT8 assembly-reference issues).
Zero errors reference `CopyEngine.cs` or `TradeCopierPanel.cs`.

**Confirmed zero PTT-file errors:** Command
`dotnet build Linting.csproj /nologo 2>&1 > build_out_verify.txt; Get-Content build_out_verify.txt | Where-Object { $_ -match "CopyEngine|TradeCopierPanel" }`
returned zero matches.

**Ph4a cross-check:** Ph4a reported zero errors in CopyEngine.cs / TradeCopierPanel.cs -- MATCH
(Ph4a's csproj path was wrong in the reported command but the underlying build result is correct)

**BUILD_PASS declared: Zero errors in CopyEngine.cs and TradeCopierPanel.cs.**

**Result: PASS**

---

### STEP-17 -- Hard-link sync verification

**CopyEngine.cs:**
```powershell
fsutil hardlink list src/PropTraderTools/CopyEngine.cs
```
Output:
```
\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
```
2 paths. NinjaTrader deployment link intact.

**TradeCopierPanel.cs:**
```powershell
fsutil hardlink list src/PropTraderTools/TradeCopierPanel.cs
```
Output:
```
\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\TradeCopierPanel.cs
```
2 paths. NinjaTrader deployment link intact.

**Ph4a cross-check:** Ph4a reported identical 2-path output for both files -- MATCH

**Result: PASS**

---

## LINE NUMBER CROSS-CHECK SUMMARY

All Ph4a-cited line numbers verified against actual source:

| Step | Ph4a Line | Actual Line | Match? |
|------|-----------|-------------|--------|
| STEP-01 _cloneAtmCacheByInstr | 145 | 145 | YES |
| STEP-01 _cloneAtmObjectByInstr | 151 | 151 | YES |
| STEP-02 SetCloneAtmCache sig | 722 | 722 | YES |
| STEP-03 SetCloneAtmObjectCache sig | 730 | 730 | YES |
| STEP-03 if-branch | 732 | 732 | YES |
| STEP-03 TryRemove | 735 | 735 | YES |
| STEP-04 GetCloneAtmMode sig | 741 | 741 | YES |
| STEP-04 first if (line 744) | 744 | 744 | YES |
| STEP-04 ternary (line 746) | 746 | 746 | YES |
| STEP-04 second if (line 750) | 750 | 750 | YES |
| STEP-04 Inherit fallback | 752 | 752 | YES |
| STEP-05 ResolveAtmMode call #1 | 2672 | 2672 | YES |
| STEP-06 ResolveAtmMode call #2 | 4404 | 4404 | YES |
| STEP-07 ResolveAtmMode sig | 5067 | 5067 | YES |
| STEP-07 if-branch | 5069 | 5069 | YES |
| STEP-08 instrKey assignment | 1852 | 1852 | YES |
| STEP-09 SetCloneAtmObjectCache call | 1860 | 1860 | YES |
| STEP-10 SetCloneAtmCache call | 1862 | 1862 | YES |

**All 18 cited line numbers verified: zero discrepancies.**

---

## DNA RULE VERIFICATION

| Rule | Requirement | Verified? | Result |
|------|-------------|-----------|--------|
| JS-021 | No lock() in changed methods | lock() scans: 0 matches in both files | PASS |
| JS-025 | ConcurrentDictionary atomic ops only | TryRemove, TryGetValue, indexer set -- all ConcurrentDictionary | PASS |
| JS-001 | No throw in dispatch/gate chain | No throw-capable operations without null guard | PASS |
| JS-002 | No null return from GetCloneAtmMode | Returns `new FollowerAtmMode.Inherit()` as final fallback | PASS |
| JS-013 | CYC <= 8 | Max CYC=4; all methods <= 8 | PASS |
| JS-042 | ASCII-only | Non-ASCII scan: 0 matches in both files | PASS |
| JS-023 | No volatile on dict reference | `readonly ConcurrentDictionary` (no volatile) -- PASS | PASS |
| NT8 | No DateTime.Now introduced | Not present in changed methods | PASS |
| NT8 | No async/await in changed methods | Not present | PASS |
| NT8 | No lock() in NT8 context | Confirmed by STEP-11/12 scans | PASS |

---

## ADDITIONAL CHECKS

**Old volatile scalar field names:**
- `_cloneAtmCache` (bare, no ByInstr suffix): zero usages in entire CopyEngine.cs -- CONFIRMED ABSENT
- `_cloneAtmObject` (bare, no ByInstr suffix): zero usages in entire CopyEngine.cs -- CONFIRMED ABSENT
- Pattern `volatile.*_cloneAtm`: zero matches -- CONFIRMED ABSENT

**BUG-F fix correctness:**
Per-instrument `ConcurrentDictionary` keyed by `instrFullName` replaces global volatile scalars.
Each panel click writes to `_cloneAtmCacheByInstr[instrKey]` and `_cloneAtmObjectByInstr[instrKey]`
where `instrKey = _instrument?.FullName ?? string.Empty` -- panel-owned, immutable after init.
Order dispatch reads `GetCloneAtmMode(order.Instrument.FullName)` -- per-instrument keyed lookup.
Cross-panel overwrite bug eliminated. Fix architecture confirmed correct.

---

## DISCREPANCIES FROM Ph4a

None found. All 17 steps match Ph4a's readings exactly.

**Minor documentation notes (non-blocking, no functional impact):**
1. Source comment line 727: `// CYC=1` -- actual CYC=2 for SetCloneAtmObjectCache. Stale doc.
   Ph4a correctly overrode this in STEP-03. No action required.
2. Source comment line 738: `// CYC=2` -- actual CYC=4 for GetCloneAtmMode. Stale doc.
   Pre-existing, acknowledged in plan (line 167). No action required.
3. Ph4a reported build command as `dotnet build src/PropTraderTools/Linting.csproj` but the
   actual file is at workspace root as `Linting.csproj`. This is a path typo in the report;
   the build result itself is correct (zero PTT-file errors confirmed by verifier).

---

## 7-SCAN CHECKLIST (verifier)

- [x] lock() scan: `CopyEngine.cs` -- **0 non-comment matches** (STEP-11)
- [x] lock() scan: `TradeCopierPanel.cs` -- **0 non-comment matches** (STEP-12)
- [x] non-ASCII scan: `CopyEngine.cs` -- **0 matches** (STEP-13)
- [x] non-ASCII scan: `TradeCopierPanel.cs` -- **0 matches** (STEP-14)
- [x] CYC spot-check: SetCloneAtmObjectCache=2, GetCloneAtmMode=4, ResolveAtmMode=2 (all <= 8) (STEP-15)
- [x] Build: `dotnet build Linting.csproj /nologo` -- **zero CopyEngine.cs / TradeCopierPanel.cs errors** (STEP-16)
- [x] Hard-link: `fsutil hardlink list` -- **2 paths each** for both files (STEP-17)

---

## ACCEPTANCE CRITERIA CHECK

1. All 17 steps confirmed with cited line numbers and verbatim snippets: **YES**
2. Both new ConcurrentDictionary fields found; both old volatile scalars absent: **YES**
3. All method signatures and call sites match plan exactly: **YES**
4. STEP-11/12: zero non-comment lock( matches: **YES**
5. STEP-13/14: zero non-ASCII characters: **YES**
6. STEP-15: all three CYC values <= 8: **YES**
7. STEP-16: BUILD_PASS declared (zero CopyEngine.cs / TradeCopierPanel.cs errors): **YES**
8. STEP-17: 2 hard-link paths for each file: **YES**
9. VERIFY_READY declared: **YES**

---

## DEFERRED ITEMS (confirmed non-blocking)

| ID | Item | Deferred to |
|----|------|-------------|
| DEFERRED-1 | Stale T_CLONE_* tests in CopyEngineTests.cs (~lines 4620-4685) calling old zero-param GetCloneAtmMode() | Option A test runner session |
| DEFERRED-2 | BUG-E (reversal-guard skip on cancelled entry) | PTT-REPAIRS-04-POST-BUG-E pipeline |
| DEFERRED-3 | TOCTOU window in value-guarded TryRemove pattern | Future hardening session |

---

## FINAL VERDICT

**BUILD_PASS** -- Zero errors in CopyEngine.cs and TradeCopierPanel.cs.

**VERIFY_PASS** -- All 17 steps independently confirmed. All 7 scans show zero violations.
All Ph4a line numbers match actual source. No DNA rule violations. BUG-F fix architecture
confirmed: per-instrument ConcurrentDictionary keyed by instrFullName correctly replaces
global volatile scalars, eliminating cross-panel ATM overwrite.
