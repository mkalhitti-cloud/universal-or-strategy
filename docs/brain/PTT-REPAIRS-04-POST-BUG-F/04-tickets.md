# PTT-REPAIRS-04-POST-BUG-F -- Tickets

Epic: PTT-REPAIRS-04-POST-BUG-F
Phase: 4a (Source Verification Only)
Plan status: REVIEW_PASS (docs/brain/PTT-REPAIRS-04-POST-BUG-F/02-plan-review.md)
Output: docs/brain/PTT-REPAIRS-04-POST-BUG-F/04-tickets.md

---

## TICKET T1 -- BUG-F: Clone ATM Per-Instrument Fix (Source Verification)

**Spec Requirement IDs:** BUG-F

**Ticket type:** SOURCE VERIFICATION ONLY
No new .cs edits unless a fix step is found missing from source.
No test updates. Stale T_CLONE_* test update is deferred (see DEFERRED-1 below).

**Files in scope:**
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/TradeCopierPanel.cs`

---

### ROOT CAUSE SUMMARY (context for verifier)

Pre-fix: two global volatile scalar fields (`_cloneAtmCache`, `_cloneAtmObject`) were
shared across all instruments. Clicking Clone on a MES panel silently overwrote the
MGC panel's ATM assignment. MGC orders would then receive MES brackets on next dispatch.

Fix applied: replaced the two scalars with per-instrument `ConcurrentDictionary` fields
keyed by `Instrument.FullName`. All call sites updated to pass the instrument key.

---

### VERIFICATION STEPS (17 total)

#### CopyEngine.cs Verifications

**STEP-01** -- Lines ~142-152: Per-instrument field declarations

Confirm the following two fields exist with exact types, `readonly` modifier, and
`StringComparer.Ordinal` constructor argument:

```
_cloneAtmCacheByInstr   : ConcurrentDictionary<string, string>
_cloneAtmObjectByInstr  : ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>
```

Confirm the old volatile scalar fields are **ABSENT** from the file:
- `volatile string _cloneAtmCache` -- must NOT exist
- `volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject` -- must NOT exist

Run: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "volatile.*_cloneAtm"`
Expected: zero matches.

Cite the exact line numbers and text of the two new field declarations.

---

**STEP-02** -- Line ~722: `SetCloneAtmCache` signature

Cite the method signature line verbatim. Confirm it reads:

```csharp
internal void SetCloneAtmCache(string instrFullName, string value)
```

First parameter must be `instrFullName`, not a void overload or old single-param form.

---

**STEP-03** -- Lines ~730-736: `SetCloneAtmObjectCache` with null branch

Cite the method body verbatim. Confirm:
- Signature: `internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)`
- Null branch present: when `atmObj == null` (or `atmObj != null` with else), calls `TryRemove(instrFullName, out _)`
- Non-null path: `_cloneAtmObjectByInstr[instrFullName] = atmObj`
- CYC = 2 (base=1 + one `if` branch)

---

**STEP-04** -- Lines ~741-753: `GetCloneAtmMode` with keyed lookup

Cite the method body verbatim. Confirm:
- Signature: `internal FollowerAtmMode GetCloneAtmMode(string instrFullName)`
- `_cloneAtmObjectByInstr.TryGetValue(instrFullName, out atmObj)` -- keyed on `instrFullName`
- `_cloneAtmCacheByInstr.TryGetValue(instrFullName, out var tpl)` -- keyed on `instrFullName`
- Returns `new FollowerAtmMode.Inherit()` as final fallback (never returns null)
- CYC = 4 (base=1, if line ~744 = +1, ternary ?: line ~746 = +1, if line ~750 = +1)

Note: A stale source comment `// CYC=2` may appear near line 738. This is a pre-existing
doc error acknowledged in the plan (plan line 167). Actual CYC is 4. No action required.

---

**STEP-05** -- Line ~2672: `DispatchToFollower` call site

Cite the exact line. Confirm the `ResolveAtmMode` call passes `order.Instrument.FullName`
as the third argument -- NOT a string literal:

```csharp
var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
```

---

**STEP-06** -- Line ~4404: `ReplaceFollowerCopyOnAtmCancel` call site

Cite the exact line. Confirm the `ResolveAtmMode` call passes
`cancelledOrder.Instrument.FullName` as the third argument -- NOT a string literal:

```csharp
var mode = ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName);
```

---

**STEP-07** -- Lines ~5059-5072: `ResolveAtmMode` 3-param signature

Cite the method signature and body verbatim. Confirm:
- Signature: `private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)`
- Three parameters: `rule`, `accountName`, `instrFullName`
- Clone mode path calls: `GetCloneAtmMode(instrFullName)` -- passes `instrFullName` through
- Signal/Mirror mode path calls: `GetAtmMode(rule, accountName)` -- unchanged
- CYC = 2 (base=1 + one `if` on CopyMode check)

---

#### TradeCopierPanel.cs Verifications

**STEP-08** -- Line ~1852: `instrKey` assignment

Cite the exact line. Confirm it reads:

```csharp
string instrKey = _instrument?.FullName ?? string.Empty;
```

Null-conditional `?.` and null-coalescing `??` operators must both be present.

---

**STEP-09** -- Line ~1860: `SetCloneAtmObjectCache` call with `instrKey`

Cite the exact line. Confirm:

```csharp
CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj);
```

`instrKey` is the first argument (instrument key). `atmObj` is the second.

---

**STEP-10** -- Line ~1862: `SetCloneAtmCache` call with `instrKey`

Cite the exact line. Confirm:

```csharp
CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl);
```

`instrKey` is the first argument. `tpl` is the second.

---

### 7-SCAN STEPS (commands to run)

**STEP-11** -- lock() scan: CopyEngine.cs

Run:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```
Expected: **zero non-comment matches.**
Report: total match count and whether any are non-comment.

---

**STEP-12** -- lock() scan: TradeCopierPanel.cs

Run:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "\block\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }
```
Expected: **zero non-comment matches.**
Report: total match count and whether any are non-comment.

---

**STEP-13** -- Non-ASCII scan: CopyEngine.cs

Run:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```
Expected: **zero matches.**

---

**STEP-14** -- Non-ASCII scan: TradeCopierPanel.cs

Run:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "[^\x00-\x7F]"
```
Expected: **zero matches.**

---

**STEP-15** -- CYC spot-check

Cite branch counts from STEP-03, STEP-04, and STEP-07:

| Method | Counted CYC | <= 8? |
|--------|-------------|-------|
| `SetCloneAtmObjectCache` (STEP-03) | 2 | YES |
| `GetCloneAtmMode` (STEP-04) | 4 | YES |
| `ResolveAtmMode` (STEP-07) | 2 | YES |

All values must be <= 8. JS-013: PASS.

---

**STEP-16** -- Build: zero errors in target files

Run:
```powershell
dotnet build src/PropTraderTools/Linting.csproj /nologo 2>&1
```
Expected: **zero errors referencing CopyEngine.cs or TradeCopierPanel.cs.**

Note: build may emit errors for stale T_CLONE_* tests in CopyEngineTests.cs.
Those errors are expected and deferred (DEFERRED-1). They do NOT constitute a
failure for this ticket. Only errors in CopyEngine.cs and TradeCopierPanel.cs
are in scope.

Declare **BUILD_PASS** if zero CopyEngine.cs / TradeCopierPanel.cs errors.
Report CopyEngineTests.cs errors separately as a note.

---

**STEP-17** -- Hard-link sync verification

Run for CopyEngine.cs:
```powershell
fsutil hardlink list src/PropTraderTools/CopyEngine.cs
```

Run for TradeCopierPanel.cs:
```powershell
fsutil hardlink list src/PropTraderTools/TradeCopierPanel.cs
```

Expected: **2 paths each** (confirming NinjaTrader hard-link sync is intact).
If either shows only 1 path, the NinjaTrader deployment link is broken -- run
`powershell -File .\deploy-sync.ps1` and re-verify.

---

### ACCEPTANCE CRITERIA

Ticket passes when ALL of the following are met:

1. All 17 steps confirmed with cited line numbers and verbatim snippets.
2. STEP-01: both new ConcurrentDictionary fields found; both old volatile scalars absent.
3. STEP-02 through STEP-10: all method signatures and call sites match the plan exactly.
4. STEP-11, STEP-12: zero non-comment `lock(` matches.
5. STEP-13, STEP-14: zero non-ASCII characters.
6. STEP-15: all three CYC values <= 8.
7. STEP-16: **BUILD_PASS** declared (zero CopyEngine.cs / TradeCopierPanel.cs errors).
8. STEP-17: 2 hard-link paths for each file.
9. **VERIFY_READY** declared.

---

### DEFERRED ITEMS (do not action in this ticket)

| ID | Item | Deferred to |
|----|------|-------------|
| DEFERRED-1 | Update stale T_CLONE_* tests in CopyEngineTests.cs (~lines 4620-4685): update calls from zero-param `GetCloneAtmMode()` to new one-param `GetCloneAtmMode(string instrFullName)` signature; add new cross-instrument test. | Option A test runner session |
| DEFERRED-2 | BUG-E (reversal-guard skip on cancelled entry) -- parallel pipeline PTT-REPAIRS-04-POST-BUG-E | PTT-REPAIRS-04-POST-BUG-E |
| DEFERRED-3 | TOCTOU window in value-guarded TryRemove pattern (carried from PTT-REPAIRS-03-POST) | Future hardening session |

---

### 7-SCAN CHECKLIST (engineer contract)

All items must be checked before declaring VERIFY_READY.

- [ ] lock() scan: `CopyEngine.cs` -- zero non-comment matches (STEP-11)
- [ ] lock() scan: `TradeCopierPanel.cs` -- zero non-comment matches (STEP-12)
- [ ] non-ASCII scan: `CopyEngine.cs` -- zero matches (STEP-13)
- [ ] non-ASCII scan: `TradeCopierPanel.cs` -- zero matches (STEP-14)
- [ ] CYC spot-check: `SetCloneAtmObjectCache`=2, `GetCloneAtmMode`=4, `ResolveAtmMode`=2 (all <= 8) (STEP-15)
- [ ] Build: `dotnet build Linting.csproj /nologo` -- zero CopyEngine.cs / TradeCopierPanel.cs errors (STEP-16)
- [ ] Hard-link: `fsutil hardlink list` -- 2 paths each for both files (STEP-17)

---

### JS RULE CONSTRAINTS APPLIED

| Rule | Method(s) | Requirement |
|------|-----------|-------------|
| JS-021 | SetCloneAtmCache, SetCloneAtmObjectCache, GetCloneAtmMode, ResolveAtmMode | No `lock()`. ConcurrentDictionary only. |
| JS-013 | SetCloneAtmObjectCache (CYC=2), GetCloneAtmMode (CYC=4), ResolveAtmMode (CYC=2) | CYC <= 8. |
| JS-001 | GetCloneAtmMode, ResolveAtmMode | No throw in dispatch/gate chain. |
| JS-002 | GetCloneAtmMode | No null return. Returns `new FollowerAtmMode.Inherit()` as final fallback. |
| JS-042 | All | ASCII-only identifiers and string literals. |
| JS-023 | All | UI updates via Dispatcher.InvokeAsync (N/A here: dictionary ops are lock-free, no UI write on callback thread). |
| JS-025 | SetCloneAtmObjectCache, GetCloneAtmMode | TryRemove, TryGetValue, indexer set -- ConcurrentDictionary atomic ops only. |

---

## RETURN STATUS

**TICKETS_COMPLETE**
