# PTT-REPAIRS-04-POST-BUG-F -- Plan Review (Phase 2)

**Reviewer**: PTT Plan Reviewer
**Epic**: PTT-REPAIRS-04-POST-BUG-F
**Plan file**: docs/brain/PTT-REPAIRS-04-POST-BUG-F/02-architecture-plan.md
**Date**: Phase 2 re-run (full review from scratch; previous cycle emitted REVIEW_FAIL on CYC claim)

---

## RESULT

**REVIEW_PASS**

Zero violations found. All 15 adversarial claims confirmed against source. All Jane Street DNA rules satisfied. All spec requirements addressed.

---

## ADVERSARIAL CLAIM VERIFICATION

Each claim verified against exact source lines. File reads performed before each determination.

### Claim 1 -- LANE-SPLIT GATE: SINGLE-PIPELINE

**STATUS: CONFIRMED**

Plan line 13: `**SINGLE-PIPELINE**`
Source evidence: plan explicitly declares single-ticket pipeline at lines 12-18.

---

### Claim 2 -- Both ConcurrentDictionary fields at lines ~142-152 with StringComparer.Ordinal

**STATUS: CONFIRMED**

```
CopyEngine.cs:145-146
  private readonly ConcurrentDictionary<string, string> _cloneAtmCacheByInstr =
      new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

CopyEngine.cs:151-152
  private readonly ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy> _cloneAtmObjectByInstr =
      new ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>(StringComparer.Ordinal);
```

Both fields are `readonly`, both use `StringComparer.Ordinal`. Both within lines 142-152.

---

### Claim 3 -- Old volatile scalar fields (_cloneAtmCache, _cloneAtmObject) are ABSENT

**STATUS: CONFIRMED**

`grep -Pattern "volatile.*_cloneAtm"` returned **zero matches** across CopyEngine.cs.
Lines 142-152 show only the two new ConcurrentDictionary fields. Old scalars do not appear anywhere in the file.

---

### Claim 4 -- SetCloneAtmCache signature has instrFullName param (lines ~719-725)

**STATUS: CONFIRMED**

```
CopyEngine.cs:722
  internal void SetCloneAtmCache(string instrFullName, string value)
```

Parameter `instrFullName` present as first argument.

---

### Claim 5 -- SetCloneAtmObjectCache has null branch doing TryRemove (CYC=2) (lines ~727-736)

**STATUS: CONFIRMED**

```
CopyEngine.cs:730-735
  internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)
  {
      if (atmObj != null) // branch (1) -- null means no ATM selected; remove stale entry
          _cloneAtmObjectByInstr[instrFullName] = atmObj;
      else
          _cloneAtmObjectByInstr.TryRemove(instrFullName, out _);
  }
```

Null branch present at line 735 calling `TryRemove`. CYC count: base=1 + one `if` = 2. Matches plan.

---

### Claim 6 -- GetCloneAtmMode has instrFullName param with TryGetValue keyed lookup; CYC matches plan (~4)

**STATUS: CONFIRMED**

```
CopyEngine.cs:741-753
  internal FollowerAtmMode GetCloneAtmMode(string instrFullName)
  {
      NinjaTrader.NinjaScript.AtmStrategy atmObj;
      if (_cloneAtmObjectByInstr.TryGetValue(instrFullName, out atmObj) && atmObj != null) // branch (1)
          return new FollowerAtmMode.Named(
              _cloneAtmCacheByInstr.TryGetValue(instrFullName, out var tpl) ? tpl : string.Empty,  // ternary (2)
              atmObj
          );
      string cache;
      if (_cloneAtmCacheByInstr.TryGetValue(instrFullName, out cache) && cache.Length > 0) // branch (3)
          return new FollowerAtmMode.Named(cache);
      return new FollowerAtmMode.Inherit();
  }
```

CYC count (simplified McCabe, &&-chains excluded):
- base = 1
- `if` at line 744 = +1
- ternary `?:` at line 746 = +1
- `if` at line 750 = +1
- **CYC = 4**

Plan states CYC=4 for this method. Source confirms CYC=4. Match.

**NOTE -- source comment discrepancy (pre-existing, not a plan violation):**
Line 738 comment reads `// CYC=2`. Actual CYC is 4 as counted above.
The plan explicitly documents this discrepancy at plan line 167:
> "SOURCE COMMENT DISCREPANCY: CopyEngine.cs line 738 comment states `// CYC=2` but actual CYC is 4"

The plan is correct; the source comment is stale. No functional impact. CYC=4 ≤ 8 (JS-013: PASS). This is a doc-only issue deferred per plan.

---

### Claim 7 -- ResolveAtmMode has three params and passes instrFullName to GetCloneAtmMode (lines ~5059-5072)

**STATUS: CONFIRMED**

```
CopyEngine.cs:5067-5072
  private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)
  {
      if (GetCopyMode() == CopyMode.Clone) // branch (1)
          return GetCloneAtmMode(instrFullName);
      return GetAtmMode(rule, accountName);
  }
```

Three parameters confirmed: `CopyRule rule`, `string accountName`, `string instrFullName`.
`instrFullName` passed directly to `GetCloneAtmMode` at line 5070.

---

### Claim 8 -- DispatchToFollower passes order.Instrument.FullName (NOT a string literal) (line ~2672)

**STATUS: CONFIRMED**

```
CopyEngine.cs:2672
  var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);
```

Third argument is `order.Instrument.FullName` -- a property access on the `Order` object, not a string literal.

---

### Claim 9 -- ReplaceFollowerCopyOnAtmCancel passes cancelledOrder.Instrument.FullName (NOT a string literal) (line ~4404)

**STATUS: CONFIRMED**

```
CopyEngine.cs:4404
  var mode = ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName);
```

Third argument is `cancelledOrder.Instrument.FullName` -- a property access, not a string literal.

---

### Claim 10 -- OnCloneModeClick has instrKey = _instrument?.FullName ?? string.Empty (line ~1852)

**STATUS: CONFIRMED**

```
TradeCopierPanel.cs:1852
  string instrKey = _instrument?.FullName ?? string.Empty;
```

Exact form confirmed. Null-conditional operator `?.` and null-coalescing `??` present.

---

### Claim 11 -- OnCloneModeClick calls SetCloneAtmObjectCache(instrKey, atmObj) (line ~1860)

**STATUS: CONFIRMED**

```
TradeCopierPanel.cs:1860
  CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj);
```

`instrKey` is the first argument (instrument key). `atmObj` is second.

---

### Claim 12 -- OnCloneModeClick calls SetCloneAtmCache(instrKey, tpl) (line ~1862)

**STATUS: CONFIRMED**

```
TradeCopierPanel.cs:1862
  CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl);
```

`instrKey` is the first argument. `tpl` is second.

---

### Claim 13 -- No lock() in any changed method

**STATUS: CONFIRMED**

`grep -Pattern "lock\("` on CopyEngine.cs: **11 matches, all in comments** (e.g. `// JS-021: no lock()`).
No actual `lock(` statement present anywhere in CopyEngine.cs.

`grep -Pattern "lock\("` on TradeCopierPanel.cs: **27 matches, all in comments** (e.g. `// JS-021: no lock()`).
No actual `lock(` statement present anywhere in TradeCopierPanel.cs.

JS-021: PASS.

---

### Claim 14 -- No non-ASCII in any changed line

**STATUS: CONFIRMED**

`grep -Pattern "[^\x00-\x7F]"` on CopyEngine.cs: **0 matches**.
`grep -Pattern "[^\x00-\x7F]"` on TradeCopierPanel.cs: **0 matches**.

Both files are 100% ASCII. JS-042: PASS.

---

### Claim 15 -- All CYC values stated in plan match what is counted from source

**STATUS: CONFIRMED**

| Method | File | Plan CYC | Counted from source | Match? |
|--------|------|----------|---------------------|--------|
| `SetCloneAtmCache` | CopyEngine.cs:722 | 1 | base=1, no branches → 1 | YES |
| `SetCloneAtmObjectCache` | CopyEngine.cs:730 | 2 | base=1 + one `if` (line 732) → 2 | YES |
| `GetCloneAtmMode` | CopyEngine.cs:741 | 4 | base=1 + if(744)+1 + ternary(746)+1 + if(750)+1 → 4 | YES |
| `ResolveAtmMode` | CopyEngine.cs:5067 | 2 | base=1 + one `if` (line 5069) → 2 | YES |
| `OnCloneModeClick` | TradeCopierPanel.cs:1848 | 2 | base=1 + one `if` (line 1855) → 2 | YES |

Maximum CYC = 4. All methods ≤ 8. JS-013: PASS.

---

## JANE STREET DNA COMPLIANCE

All rules from the RULES_CATALOG.md DNA block checked against plan and source.

| Rule ID | Rule | Evidence | Status |
|---------|------|----------|--------|
| JS-021 | No `lock()` | Zero actual `lock(` in both files (grep confirmed) | PASS |
| JS-001 | No throw in dispatch/gate chain | No `throw` in SetCloneAtmCache, SetCloneAtmObjectCache, GetCloneAtmMode, ResolveAtmMode, OnCloneModeClick | PASS |
| JS-002 | No null return where value expected | `GetCloneAtmMode` returns `new FollowerAtmMode.Inherit()` at line 752 as final fallback -- never null | PASS |
| JS-003 | No magic string for discriminated state | No magic strings used; instrument key comes from `order.Instrument.FullName` and `_instrument.FullName` | PASS |
| JS-008 | Mutable fields on struct / SolidColorBrush not Frozen | No struct or brush introduced in this change | N/A -- PASS |
| JS-009 | Dictionary<K,V> for thread-touched collection | Changed fields are `ConcurrentDictionary` -- not `Dictionary` | PASS |
| JS-010 | Public constructor on singleton | No singleton constructor change | N/A -- PASS |
| JS-013 | CYC > 8 | Maximum CYC = 4 (GetCloneAtmMode). All methods <= 8 | PASS |
| JS-023 | UI update off-thread without Dispatcher | `OnCloneModeClick` executes on UI thread; dictionary writes are lock-free; no off-thread UI update | PASS |
| NT8: no async/await in lifecycle | No async/await in OnInitialize/OnDestroyed/OnWindowCreated | Not introduced | PASS |
| NT8: no Account.All in constructor | Not introduced | Not introduced | PASS |
| NT8: SCAN-03 FontFamily | No FontFamily override | Not introduced | PASS |
| NT8: SCAN-04 #RRGGBB hex | No hardcoded color hex | Not introduced | PASS |
| NT8: SCAN-05 CreateOrder without PTT- prefix | Not introduced | Not introduced | PASS |
| NT8: SCAN-06 DateTime.Now | Not introduced | Not introduced | PASS |

---

## SPEC COVERAGE MATRIX

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| Identify root cause: global scalar ATM fields overwritten on panel switch | YES | BUG-F ROOT CAUSE NARRATIVE |
| Replace volatile scalar fields with per-instrument ConcurrentDictionary | YES | CHANGED FIELDS, FIX ARCHITECTURE |
| Updated SetCloneAtmCache / SetCloneAtmObjectCache to accept instrFullName | YES | COMPLETE CALL GRAPH, CHANGED METHOD SIGNATURES |
| Updated GetCloneAtmMode to accept instrFullName, keyed lookup | YES | COMPLETE CALL GRAPH, CHANGED METHOD SIGNATURES |
| Updated ResolveAtmMode to 3-param, pass instrFullName through | YES | COMPLETE CALL GRAPH |
| Updated DispatchToFollower call site to pass order.Instrument.FullName | YES | CALL SITES UPDATED |
| Updated ReplaceFollowerCopyOnAtmCancel call site to pass instrument key | YES | CALL SITES UPDATED |
| Updated OnCloneModeClick to use _instrument.FullName as key | YES | COMPLETE CALL GRAPH, COMPONENT LIST |
| CYC compliance for all changed methods (<= 8) | YES | CYC ACCOUNTING |
| Threading model verification | YES | THREADING MODEL |
| JS compliance table | YES | JANE STREET COMPLIANCE TABLE |
| 7-scan results | YES | 7-SCAN RESULTS |
| Stale tests identified and disposition stated | YES | STALE TESTS (DEFERRED-1) |
| User validation of fix | YES | USER VALIDATION |

All spec requirements addressed. No unaddressed requirement found.

---

## VIOLATIONS

**None.**

---

## SUMMARY

All 15 adversarial claims verified against source with exact line citations. All Jane Street DNA rules pass. All spec requirements addressed. Maximum CYC = 4, well within the JS-013 limit of 8. Zero `lock()` statements. Zero non-ASCII characters. The stale source comment (`// CYC=2` at line 738) is a pre-existing doc error that the plan explicitly acknowledges; it has no functional impact and does not constitute a rule violation.

**REVIEW_PASS**
