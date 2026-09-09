# PTT-REPAIRS-04-POST-BUG-F -- Ticket T1 Completion

**Engineer**: PTT Engineer (Phase 4a)
**Epic**: PTT-REPAIRS-04-POST-BUG-F
**Ticket**: T1 -- BUG-F: Clone ATM Per-Instrument Fix (Source Verification)
**Gate**: TICKET_REVIEW_PASS confirmed (04-ticket-review.md line 164)
**Ticket type**: SOURCE VERIFICATION ONLY -- no .cs edits made (all steps PASS from source)

---

## VERIFICATION RESULTS (17 steps)

---

### STEP-01 -- Per-instrument field declarations (lines 142-152) -- PASS

**New fields present:**

```
Line 145: private readonly ConcurrentDictionary<string, string> _cloneAtmCacheByInstr =
Line 146:     new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
Line 151: private readonly ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy> _cloneAtmObjectByInstr =
Line 152:     new ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>(StringComparer.Ordinal);
```

Both fields: `readonly` modifier PRESENT. `StringComparer.Ordinal` constructor arg PRESENT.

**Old volatile scalars absent:**

Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "volatile.*_cloneAtm"`
Output: zero matches (no output). PASS.

---

### STEP-02 -- `SetCloneAtmCache` signature (line 722) -- PASS

```
Line 722: internal void SetCloneAtmCache(string instrFullName, string value)
```

First parameter is `instrFullName`. Two-parameter form confirmed.

---

### STEP-03 -- `SetCloneAtmObjectCache` body (lines 730-736) -- PASS

```
Line 730: internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)
Line 731: {
Line 732:     if (atmObj != null) // branch (1) -- null means no ATM selected; remove stale entry
Line 733:         _cloneAtmObjectByInstr[instrFullName] = atmObj;
Line 734:     else
Line 735:         _cloneAtmObjectByInstr.TryRemove(instrFullName, out _);
Line 736: }
```

Null branch (TryRemove) PRESENT. Non-null path (indexer set) PRESENT. CYC = 2 (base=1 + one `if`).

---

### STEP-04 -- `GetCloneAtmMode` body (lines 741-753) -- PASS

```
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

- `_cloneAtmObjectByInstr.TryGetValue(instrFullName, ...)` -- keyed on `instrFullName` CONFIRMED
- `_cloneAtmCacheByInstr.TryGetValue(instrFullName, ...)` -- keyed on `instrFullName` CONFIRMED
- `new FollowerAtmMode.Inherit()` final fallback -- never null CONFIRMED
- CYC = 4: base=1, if(line 744)=+1, ternary `?:`(line 746)=+1, if(line 750)=+1

NOTE: Source comment at line 738 reads `// CYC=2` -- pre-existing doc error acknowledged
in architecture plan (plan line 167). Actual CYC is 4. No functional impact. No action required.

---

### STEP-05 -- `DispatchToFollower` call site (line 2672) -- PASS

```
Line 2672: var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
```

Third argument is `order.Instrument.FullName` (not a string literal). CONFIRMED.

---

### STEP-06 -- `ReplaceFollowerCopyOnAtmCancel` call site (line 4404) -- PASS

```
Line 4404: var mode = ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName);
```

Third argument is `cancelledOrder.Instrument.FullName` (not a string literal). CONFIRMED.

---

### STEP-07 -- `ResolveAtmMode` 3-param signature (lines 5067-5072) -- PASS

```
Line 5067: private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)
Line 5068: {
Line 5069:     if (GetCopyMode() == CopyMode.Clone) // branch (1)
Line 5070:         return GetCloneAtmMode(instrFullName);
Line 5071:     return GetAtmMode(rule, accountName);
Line 5072: }
```

- 3 parameters: `rule`, `accountName`, `instrFullName` CONFIRMED
- Clone mode path: `GetCloneAtmMode(instrFullName)` -- passes `instrFullName` through CONFIRMED
- Signal/Mirror mode path: `GetAtmMode(rule, accountName)` -- unchanged CONFIRMED
- CYC = 2: base=1 + one `if` branch

---

### STEP-08 -- `instrKey` assignment (TradeCopierPanel.cs line 1852) -- PASS

```
Line 1852: string instrKey = _instrument?.FullName ?? string.Empty;
```

Null-conditional `?.` PRESENT. Null-coalescing `??` PRESENT. CONFIRMED.

---

### STEP-09 -- `SetCloneAtmObjectCache` call with `instrKey` (line 1860) -- PASS

```
Line 1860: CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj);
```

`instrKey` is first argument. `atmObj` is second. CONFIRMED.

---

### STEP-10 -- `SetCloneAtmCache` call with `instrKey` (line 1862) -- PASS

```
Line 1862: CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl);
```

`instrKey` is first argument. `tpl` is second. CONFIRMED.

---

### STEP-11 -- lock() scan: CopyEngine.cs -- PASS

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```

Output: zero matches (no output). 0 non-comment `lock(` matches. PASS.

---

### STEP-12 -- lock() scan: TradeCopierPanel.cs -- PASS

Command:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```

Output: zero matches (no output). 0 non-comment `lock(` matches. PASS.

---

### STEP-13 -- Non-ASCII scan: CopyEngine.cs -- PASS

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```

Output: zero matches (no output). PASS.

---

### STEP-14 -- Non-ASCII scan: TradeCopierPanel.cs -- PASS

Command:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "[^\x00-\x7F]"
```

Output: zero matches (no output). PASS.

---

### STEP-15 -- CYC spot-check -- PASS

Branch counts derived from STEP-03, STEP-04, STEP-07:

| Method | Counted CYC | <= 8? |
|--------|-------------|-------|
| `SetCloneAtmObjectCache` (STEP-03) | 2 | YES |
| `GetCloneAtmMode` (STEP-04) | 4 | YES |
| `ResolveAtmMode` (STEP-07) | 2 | YES |

Maximum CYC = 4. All values <= 8. JS-013: PASS.

---

### STEP-16 -- Build: CopyEngine.cs / TradeCopierPanel.cs errors -- PASS

Command:
```powershell
dotnet build Linting.csproj /nologo 2>&1
```

Result: **Zero errors in CopyEngine.cs or TradeCopierPanel.cs.**

All build errors in output are in unrelated V12_002.* and V12_002.Properties.cs files
(pre-existing NT8 assembly-reference issues in the V12 strategy codebase, not PTT add-on files).
No errors reference `src/PropTraderTools/CopyEngine.cs` or `src/PropTraderTools/TradeCopierPanel.cs`.

**BUILD_PASS declared for CopyEngine.cs and TradeCopierPanel.cs.**

---

### STEP-17 -- Hard-link sync verification -- PASS

**CopyEngine.cs:**
```
fsutil hardlink list src/PropTraderTools/CopyEngine.cs

\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
```
2 paths. NinjaTrader deployment link intact. PASS.

**TradeCopierPanel.cs:**
```
fsutil hardlink list src/PropTraderTools/TradeCopierPanel.cs

\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\TradeCopierPanel.cs
```
2 paths. NinjaTrader deployment link intact. PASS.

---

## 7-SCAN CHECKLIST

- [x] lock() scan: `CopyEngine.cs` -- **0 non-comment matches** (STEP-11)
- [x] lock() scan: `TradeCopierPanel.cs` -- **0 non-comment matches** (STEP-12)
- [x] non-ASCII scan: `CopyEngine.cs` -- **0 matches** (STEP-13)
- [x] non-ASCII scan: `TradeCopierPanel.cs` -- **0 matches** (STEP-14)
- [x] CYC spot-check: `SetCloneAtmObjectCache`=2, `GetCloneAtmMode`=4, `ResolveAtmMode`=2 (all <= 8) (STEP-15)
- [x] Build: `dotnet build Linting.csproj /nologo` -- **zero CopyEngine.cs / TradeCopierPanel.cs errors** (STEP-16)
- [x] Hard-link: `fsutil hardlink list` -- **2 paths each** for both files (STEP-17)

---

## DEFERRED ITEMS NOTE

**DEFERRED-1**: Stale T_CLONE_* tests in CopyEngineTests.cs lines ~4620-4685 deferred to Option A session.
These tests call the old zero-param `GetCloneAtmMode()` signature. The new signature requires
one parameter (`string instrFullName`). These will fail to compile against the new signature.
They are scoped out of this ticket per the plan (02-architecture-plan.md line 180) and confirmed
non-blocking per STEP-16 acceptance criteria (04-tickets.md lines 233-238).

---

## FINAL DECLARATIONS

**BUILD_PASS** -- Zero errors in CopyEngine.cs and TradeCopierPanel.cs.

**VERIFY_READY** -- All 17 verification steps PASS. All 7 scans show zero violations.
Source changes exactly match the architecture plan (02-architecture-plan.md).
No .cs edits were required -- all BUG-F changes were already present in source.
