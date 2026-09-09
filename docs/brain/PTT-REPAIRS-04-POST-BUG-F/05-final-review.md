# PTT-REPAIRS-04-POST-BUG-F -- Final Review (Phase 5)

**Reviewer**: PTT Plan Reviewer (Phase 5)
**Epic**: PTT-REPAIRS-04-POST-BUG-F
**Date**: Phase 5 Final Review

---

## PIPELINE GATE CONFIRMATIONS

| Gate | Document | Verdict | Confirmed? |
|------|----------|---------|------------|
| REVIEW_PASS | 02-plan-review.md line 10 | REVIEW_PASS | YES -- "Zero violations found. All 15 adversarial claims confirmed against source." |
| TICKET_REVIEW_PASS | 04-ticket-review.md line 164 | TICKET_REVIEW_PASS | YES -- "All six adversarial criteria pass. Zero violations found." |
| BUILD_PASS | ticket-1-completion.md line 278 | BUILD_PASS | YES -- "Zero errors in CopyEngine.cs and TradeCopierPanel.cs." |
| VERIFY_PASS | ticket-1-verification.md line 550 | VERIFY_PASS | YES -- "All 17 steps independently confirmed. All 7 scans show zero violations." |

All four upstream gates passed. Phase 5 review proceeds.

---

## CHECK A -- OLD VOLATILE SCALAR REMOVAL COHERENCE

**Scan A1:** `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_cloneAtmObject\b" | Where-Object { $_.Line -notmatch "^\s*//"}`

**Result: zero non-comment matches.**

**Scan A2:** `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_cloneAtmCache\b" | Where-Object { $_.Line -notmatch "^\s*//"}`

**Result: zero non-comment matches.**

**Verdict: PASS.** Both old volatile scalar fields (`_cloneAtmCache`, `_cloneAtmObject`) are fully removed from source. No non-comment references remain. The replacement `ConcurrentDictionary` fields (`_cloneAtmCacheByInstr`, `_cloneAtmObjectByInstr`) are the only active storage.

---

## CHECK B -- CALLER HYGIENE (no hardcoded instrFullName strings)

**Site 1 -- CopyEngine.cs line 2672 (DispatchToFollower):**

```csharp
var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
```

Third argument is `order.Instrument.FullName` -- property access on NT8 Order object. **No string literal. PASS.**

**Site 2 -- CopyEngine.cs line 4404 (ReplaceFollowerCopyOnAtmCancel):**

```csharp
var mode = ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName);
```

Third argument is `cancelledOrder.Instrument.FullName` -- property access on NT8 Order object. **No string literal. PASS.**

**Site 3 -- TradeCopierPanel.cs line 1852 (OnCloneModeClick):**

```csharp
string instrKey = _instrument?.FullName ?? string.Empty;
```

`_instrument?.FullName` -- null-conditional property access on panel-owned `Instrument` field set at init. Null-coalescing guard to `string.Empty`. **No string literal. PASS.**

Both downstream calls at lines 1860 and 1862 pass `instrKey` as first argument:

```csharp
CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj);
CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl);
```

**Verdict: PASS.** All three call sites use property access. No hardcoded instrument strings anywhere in the call chain.

---

## CHECK C -- NO lock() IN CHANGED METHOD BODIES

Confirmed by Phase 4b (VERIFY_PASS, ticket-1-verification.md STEP-11/12):

- CopyEngine.cs: `Select-String -Pattern "lock\s*\("` filtered to non-comment lines -- **zero matches** (STEP-11).
- TradeCopierPanel.cs: same scan -- **zero matches** (STEP-12).

Phase 4a also confirmed zero matches (ticket-1-completion.md STEP-11/12). Both independent layers agree.

**Verdict: PASS.** JS-021: no `lock()` in any changed method body. Confirmed by two independent layers.

---

## CHECK D -- NO NON-ASCII IN CHANGED SECTIONS

Confirmed by Phase 4b (VERIFY_PASS, ticket-1-verification.md STEP-13/14):

- CopyEngine.cs: `Select-String -Pattern "[^\x00-\x7F]"` -- **zero matches** (STEP-13).
- TradeCopierPanel.cs: same scan -- **zero matches** (STEP-14).

Both files are 100% ASCII. Confirmed independently by Ph4a and Ph4b.

**Verdict: PASS.** JS-042: ASCII-only. Confirmed by two independent layers.

---

## CHECK E -- JS COMPLIANCE SUMMARY TABLE

All CYC values from architecture plan (cross-checked by Ph4a STEP-15 and Ph4b STEP-15); all lock/ASCII from Ph4a STEP-11–14 and Ph4b STEP-11–14; all ConcurrentDictionary from STEP-01 through STEP-04.

| Method | File | CYC | lock() | ASCII | ConcurrentDictionary | Within-limit (CYC<=8) |
|--------|------|-----|--------|-------|---------------------|----------------------|
| `SetCloneAtmCache` | CopyEngine.cs:722 | 1 | 0 matches | PASS | YES (indexer set on `_cloneAtmCacheByInstr`) | YES |
| `SetCloneAtmObjectCache` | CopyEngine.cs:730 | 2 | 0 matches | PASS | YES (indexer set + TryRemove on `_cloneAtmObjectByInstr`) | YES |
| `GetCloneAtmMode` | CopyEngine.cs:741 | 4 | 0 matches | PASS | YES (TryGetValue on both dicts) | YES |
| `ResolveAtmMode` | CopyEngine.cs:5067 | 2 | 0 matches | PASS | N/A (router; no dict access) | YES |
| `OnCloneModeClick` | TradeCopierPanel.cs:1848 | 2 | 0 matches | PASS | YES (calls SetCloneAtmObjectCache + SetCloneAtmCache) | YES |

**Maximum CYC = 4 (GetCloneAtmMode). All five methods <= 8. JS-013: PASS.**

**Note on stale source comments (pre-existing, non-blocking, deferred per plan):**
- CopyEngine.cs line 727: comment `// CYC=1` for `SetCloneAtmObjectCache`; actual CYC=2. Doc-only error.
- CopyEngine.cs line 738: comment `// CYC=2` for `GetCloneAtmMode`; actual CYC=4. Doc-only error. Acknowledged in plan (plan line 167).

---

## CHECK F -- SPEC SATISFACTION

**BUG-F Requirement:** "Clone ATM state must be per-instrument -- MGC panel must retain MGC ATM after switching to MES and back, without requiring re-click of Clone."

**Fix applied:**
- Old global volatile scalars (`_cloneAtmCache`, `_cloneAtmObject`) removed.
- New `ConcurrentDictionary<string, string> _cloneAtmCacheByInstr` (keyed by `instrFullName`, `StringComparer.Ordinal`) at line 145.
- New `ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy> _cloneAtmObjectByInstr` (keyed by `instrFullName`, `StringComparer.Ordinal`) at line 151.
- Each `Clone` click in `OnCloneModeClick` writes to `_instrument?.FullName ?? string.Empty` -- the panel-owned, init-time-immutable key.
- Each order dispatch reads `GetCloneAtmMode(order.Instrument.FullName)` -- guaranteed to return the ATM registered for that instrument, not the last panel to click Clone.

**User manual test evidence (from architecture plan lines 283-288 and task brief):**
- MGC panel cloned (Clone click while MGC chart active).
- User switched to MES chart, traded MES.
- Returned to MGC panel.
- MGC panel retained MGC ATM **without re-click of Clone**.
- No regression observed in MES ATM assignment.

**Verdict: PASS.** Spec requirement fully satisfied by both implementation and manual test confirmation.

---

## CHECK G -- CROSS-FILE SYSTEM COHERENCE

The three changed components (CopyEngine fields + methods, TradeCopierPanel `OnCloneModeClick`) form a complete per-instrument clone ATM pipeline:

```
[Panel init]
  TradeCopierPanel.SetInstrument(instrument)
    -> _instrument = instrument (immutable after this point)

[Clone click -- UI thread]
  TradeCopierPanel.OnCloneModeClick
    -> instrKey = _instrument?.FullName ?? string.Empty
    -> CopyEngine.SetCloneAtmObjectCache(instrKey, atmObj)   // writes _cloneAtmObjectByInstr[instrKey]
    -> CopyEngine.SetCloneAtmCache(instrKey, tpl)             // writes _cloneAtmCacheByInstr[instrKey]

[Order dispatch -- NT8 callback thread]
  CopyEngine.DispatchToFollower(...)
    -> ResolveAtmMode(rule, acc.Name, order.Instrument.FullName)
         -> GetCloneAtmMode(instrFullName)
              -> _cloneAtmObjectByInstr.TryGetValue(instrFullName, ...)
              -> _cloneAtmCacheByInstr.TryGetValue(instrFullName, ...)
              -> returns FollowerAtmMode.Named(...) or .Inherit()

[ATM cancel replace -- NT8 callback thread]
  CopyEngine.ReplaceFollowerCopyOnAtmCancel(...)
    -> ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName)
         -> same keyed path as above
```

No missing wiring. No orphaned path. Write keys and read keys are both derived from NT8 `Instrument.FullName` -- same property, same string, guaranteed to match. `StringComparer.Ordinal` ensures byte-exact key comparison with no normalization surprises.

**Verdict: PASS.** System is coherent end-to-end across both files.

---

## CHECK H -- 7-SCAN AGGREGATE ZERO VERIFICATION

Per Phase 3.5 contract (04-ticket-review.md): the final reviewer confirms all 7 scans returned zero across `src/PropTraderTools/` in aggregate. Evidence from Ph4b (ticket-1-verification.md):

| Scan | Target | Result |
|------|--------|--------|
| SCAN-01: lock() | CopyEngine.cs | 0 non-comment matches |
| SCAN-02: lock() | TradeCopierPanel.cs | 0 non-comment matches |
| SCAN-03: non-ASCII | CopyEngine.cs | 0 matches |
| SCAN-04: non-ASCII | TradeCopierPanel.cs | 0 matches |
| SCAN-05: CYC spot-check | SetCloneAtmObjectCache=2, GetCloneAtmMode=4, ResolveAtmMode=2 | All <= 8 |
| SCAN-06: Build | Linting.csproj | 0 errors in CopyEngine.cs / TradeCopierPanel.cs |
| SCAN-07: Hard-link | CopyEngine.cs + TradeCopierPanel.cs | 2 paths each (NinjaTrader link intact) |

**All 7 scans: zero violations in aggregate.**

---

## CHECK I -- NT8 API VALIDITY

All NT8 APIs used in changed code are AddOn-safe:

| API | Usage | AddOn-safe? |
|-----|-------|-------------|
| `ChartTrader.AtmStrategy` | Read on UI thread in `OnCloneModeClick` | YES -- ChartTrader property |
| `Order.Instrument.FullName` | Read in NT8 callback threads | YES -- NT8 Order property |
| `Instrument.FullName` | Read via `_instrument.FullName` on UI thread | YES -- Instrument property |
| `ConcurrentDictionary<K,V>` | .NET BCL -- no NT8 dependency | YES |

No `AtmStrategyCreate` (StrategyBase-only API) used. No AddOn-invalid API calls.

**Verdict: PASS.**

---

## CHECK J -- DEFERRED ITEMS CARRIED FORWARD

| ID | Item | Status |
|----|------|--------|
| DEFERRED-1 | Stale T_CLONE_* tests in CopyEngineTests.cs (~lines 4620-4685) -- old zero-param `GetCloneAtmMode()` calls | OPEN -- deferred to Option A test runner session |
| DEFERRED-2 | BUG-E (reversal-guard skip on cancelled entry) | OPEN -- parallel pipeline PTT-REPAIRS-04-POST-BUG-E |
| DEFERRED-3 | TOCTOU window in value-guarded TryRemove pattern (SetCloneAtmObjectCache) | OPEN -- future hardening session |

All three deferred items are documented, non-blocking for this pipeline, and carried to 06-deferred-backlog.md (Section K).

---

## SECTION K -- DEFERRED WORK (REQUIRED)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-BUG-F-01 | Update stale T_CLONE_* tests in CopyEngineTests.cs (~lines 4620-4685): (a) update existing T_CLONE_* tests to pass `instrFullName` param to new `GetCloneAtmMode(string)` signature; (b) add new cross-instrument test: SetCloneAtmCache("MGC DEC26","MGC_TPL"), SetCloneAtmCache("MES DEC26","MES_TPL"), verify GetCloneAtmMode("MGC DEC26") returns MGC_TPL not MES_TPL. Blocked by: Option A test runner wiring (CopyEngineTests.cs not connected to executable test project). | P1 | Option A test runner session | OPEN |
| DW-BUG-F-02 | BUG-E fix: reversal-guard skip on cancelled entry. Covered in parallel pipeline PTT-REPAIRS-04-POST-BUG-E. | P1 | PTT-REPAIRS-04-POST-BUG-E | OPEN |
| DW-BUG-F-03 | TOCTOU window in value-guarded TryRemove pattern in `SetCloneAtmObjectCache` (line 732-735): check-then-remove has theoretical race under concurrent modification. Low risk for trading UI thread patterns but noted. Carried from PTT-REPAIRS-03-POST. | P2 | Future hardening session | OPEN |
| DW-BUG-F-04 | Stale source comments: CopyEngine.cs line 727 `// CYC=1` (actual CYC=2 for SetCloneAtmObjectCache) and line 738 `// CYC=2` (actual CYC=4 for GetCloneAtmMode). Doc-only corrections. No functional impact. | P2 | Next cleanup pass | OPEN |

---

## FINAL VERDICT

**Zero violations found in Phase 5 cross-file coherence review.**

- Check A (old scalar removal): PASS -- zero non-comment matches for both bare names
- Check B (caller hygiene): PASS -- all three call sites use property access, no string literals
- Check C (no lock): PASS -- confirmed by Ph4b independent scan
- Check D (ASCII-only): PASS -- confirmed by Ph4b independent scan
- Check E (JS compliance table): PASS -- max CYC=4, zero lock, 100% ASCII, ConcurrentDictionary throughout
- Check F (spec satisfaction): PASS -- per-instrument storage confirmed; manual test evidence present
- Check G (cross-file coherence): PASS -- write keys and read keys are same NT8 property; no wiring gaps
- Check H (7-scan aggregate zero): PASS -- all 7 scans zero across both files
- Check I (NT8 API validity): PASS -- all APIs AddOn-safe; no StrategyBase-only APIs used
- Check J (deferred items): 3 carried items, all documented and non-blocking

**PIPELINE_COMPLETE**
