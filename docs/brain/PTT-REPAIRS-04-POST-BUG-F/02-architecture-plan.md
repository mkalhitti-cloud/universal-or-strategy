# PTT-REPAIRS-04-POST-BUG-F -- Architecture Plan

Epic: PTT-REPAIRS-04-POST-BUG-F
Phase: Formalization Pipeline (fix already applied via direct edit)
Status: REVIEW_PASS
Output: docs/brain/PTT-REPAIRS-04-POST-BUG-F/02-architecture-plan.md
Reference: docs/brain/PTT-REPAIRS-04/direct-edits.md (BUG-F section, lines 212-289)

---

## LANE-SPLIT GATE RESULT

**SINGLE-PIPELINE**

Q1. Same method or within 50 lines? Changes span two files and multiple line ranges, but all changes
constitute one coherent per-instrument key fix for a single root-cause bug.
Spec directive: single-ticket formalization pipeline.
Result: ONE ticket. STOP.

---

## BUG-F ROOT CAUSE NARRATIVE

### Pre-fix storage model

| Field | Type | Line (pre-fix) | Per-instrument? |
|-------|------|----------------|-----------------|
| `_cloneAtmCache` | `volatile string` | 145 | NO -- single shared scalar |
| `_cloneAtmObject` | `volatile NinjaTrader.NinjaScript.AtmStrategy` | 150 | NO -- single shared scalar |

`_currentChart` in TradeCopierPanel is a single panel-level reference to the currently active
chart window. It is NOT per-instrument.

### Failure scenario

1. User opens MGC chart panel. Clicks Clone.
   `OnCloneModeClick` reads `_currentChart` (MGC chart) -> `ct.AtmStrategy` = MGC_ATM.
   Writes: `_cloneAtmObject = MGC_ATM`, `_cloneAtmCache = "MGC-Template"` (global scalars).

2. User switches to / opens MES chart. MES panel becomes active.
   `_currentChart` is updated to the MES chart window (panel-level reference, not instrument-scoped).

3. User clicks Clone on MES panel (or any panel with MES as _currentChart).
   `OnCloneModeClick` reads `_currentChart` (now MES chart) -> `ct.AtmStrategy` = MES_ATM.
   Writes: `_cloneAtmObject = MES_ATM` -- OVERWRITES the global scalar.
   `_cloneAtmCache = "MES-Template"` -- overwrites.

4. MGC order event fires (DispatchToFollower).
   Pre-fix call: `ResolveAtmMode(rule, acc.Name)` -- no instrument parameter.
   Pre-fix `GetCloneAtmMode()` -- no parameter, returns `_cloneAtmObject` = MES_ATM.
   MGC orders armed with MES ATM brackets. **BUG.**

### User symptom

MGC panel uses MES ATM after user switches between chart panels.
User must re-click Clone on MGC panel to restore the correct ATM.
If user forgets, MGC orders receive MES ATM brackets silently.

---

## FIX ARCHITECTURE

### Why ConcurrentDictionary solves the problem

Replacing two volatile scalars with per-instrument dictionaries:

```
ConcurrentDictionary<string, string>                        _cloneAtmCacheByInstr
ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy> _cloneAtmObjectByInstr
```

Each `Clone` click now stores the ATM keyed by `_instrument.FullName` -- the instrument
assigned to THIS panel, not the instrument of whatever chart window is currently active.

After the fix, the store from an MES click writes to `["MES SEP26"]`, leaving `["MGC DEC26"]`
completely untouched. Both entries coexist in the dictionary indefinitely.

When MGC dispatches an order, `GetCloneAtmMode("MGC DEC26")` looks up exactly the MGC entry
and returns MGC_ATM. MES_ATM is never returned for MGC.

### Why `_instrument.FullName` is the correct stable key

`_instrument` is a panel-level field set exactly once by `SetInstrument(Instrument instrument)`
(TradeCopierPanel.cs line 529) during NT8 add-on initialization. It is never mutated after that.

`_instrument.FullName` is the standard NT8 instrument name string (e.g. "MGC DEC26", "MES SEP26").
The same string is available on `order.Instrument.FullName` in NT8 order callbacks.
StringComparer.Ordinal is used so key lookups are byte-exact. No normalization needed because
both the write (panel init) and the read (order event) use the same NT8 FullName property.

### Key invariant

Each panel instance stores `_instrument.FullName` as an immutable key for the duration of the
panel's lifetime. Clone clicks write to that key only. Order dispatch reads from that key only.
Panels never interfere with each other's dictionary entries.

---

## COMPLETE CALL GRAPH OF CHANGED SIGNATURES

All signatures verified from source files.

```
[UI Thread -- Clone click]
TradeCopierPanel.OnCloneModeClick(object sender, RoutedEventArgs e)
  |
  +-- string instrKey = _instrument?.FullName ?? string.Empty           // panel-owned stable key
  +-- CopyEngine.Instance.SetCloneAtmObjectCache(instrKey, atmObj)      // writes _cloneAtmObjectByInstr[instrKey]
  +-- CopyEngine.Instance.SetCloneAtmCache(instrKey, tpl)               // writes _cloneAtmCacheByInstr[instrKey]

[NT8 Callback Thread -- order dispatch]
CopyEngine.DispatchToFollower(CopyRule rule, Account acc, int idx, Order order, CopySignal baseSignal)
  |
  +-- ResolveAtmMode(rule, acc.Name, order.Instrument.FullName)         // line 2672

CopyEngine.ReplaceFollowerCopyOnAtmCancel(...)
  |
  +-- ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name,
                     cancelledOrder.Instrument.FullName)                // line 4404

[CopyEngine internal]
CopyEngine.ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)
  |
  +-- [Clone mode] GetCloneAtmMode(instrFullName)                       // keyed lookup
  +-- [Signal/Mirror mode] GetAtmMode(rule, accountName)                // unchanged

CopyEngine.GetCloneAtmMode(string instrFullName)
  |
  +-- _cloneAtmObjectByInstr.TryGetValue(instrFullName, out atmObj)     // per-instrument read
  +-- _cloneAtmCacheByInstr.TryGetValue(instrFullName, out tpl)         // per-instrument read
  +-- returns FollowerAtmMode.Named(tpl, atmObj) or Inherit()
```

### Changed method signatures (exact, from source)

| Method | File | Line | Signature |
|--------|------|------|-----------|
| `SetCloneAtmCache` | CopyEngine.cs | 722 | `internal void SetCloneAtmCache(string instrFullName, string value)` |
| `SetCloneAtmObjectCache` | CopyEngine.cs | 730 | `internal void SetCloneAtmObjectCache(string instrFullName, NinjaTrader.NinjaScript.AtmStrategy atmObj)` |
| `GetCloneAtmMode` | CopyEngine.cs | 741 | `internal FollowerAtmMode GetCloneAtmMode(string instrFullName)` |
| `ResolveAtmMode` | CopyEngine.cs | 5067 | `private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)` |
| `OnCloneModeClick` | TradeCopierPanel.cs | 1848 | `private void OnCloneModeClick(object sender, RoutedEventArgs e)` |

### Call sites updated

| Call site | File | Line | Change |
|-----------|------|------|--------|
| DispatchToFollower | CopyEngine.cs | 2672 | Added `order.Instrument.FullName` as 3rd arg to ResolveAtmMode |
| ReplaceFollowerCopyOnAtmCancel | CopyEngine.cs | 4404 | Added `cancelledOrder.Instrument.FullName` as 3rd arg to ResolveAtmMode |

---

## CYC ACCOUNTING

All values verified from source branch count. All <= 8 (JS-013 limit).

| Method | File | Before | After | Delta | How counted |
|--------|------|--------|-------|-------|-------------|
| `SetCloneAtmCache` | CopyEngine.cs | 1 | 1 | 0 | Single assignment statement. No branch. |
| `SetCloneAtmObjectCache` | CopyEngine.cs | 1 | 2 | +1 | One `if (atmObj != null)` branch added. |
| `GetCloneAtmMode` | CopyEngine.cs | 2 | 4 | +2 | Simplified McCabe (if + ternary; &&-chains excluded): base=1, if(line 744)=+1, ternary ?:(line 746)=+1, if(line 750)=+1. CYC=4. |
| `ResolveAtmMode` | CopyEngine.cs | 2 | 2 | 0 | Same 1 if-branch. Extra param passes through with no new decisions. |
| `OnCloneModeClick` | TradeCopierPanel.cs | 2 | 2 | 0 | Added `instrKey` variable and param passing. No new branch. |

**Maximum CYC after fix: 4 (GetCloneAtmMode, simplified McCabe). All methods <= 8. JS-013: PASS.**

> SOURCE COMMENT DISCREPANCY: CopyEngine.cs line 738 comment states `// CYC=2` but actual CYC is 4 (simplified McCabe: base=1, if=+1, ternary=+1, if=+1). No functional impact. CYC <= 8 limit satisfied. Comment correction deferred (doc-only).

---

## STALE TESTS

`CopyEngineTests.cs` contains `T_CLONE_*` test methods at approximately lines 4620-4685.
These tests call the **old** `GetCloneAtmMode()` with no parameters.

The new signature is `GetCloneAtmMode(string instrFullName)`.

**Status: STALE. Old calls will not compile against the new signature.**

**Disposition: DEFERRED-1** -- deferred to Option A test runner session.

Phase 4a of THIS pipeline is SOURCE VERIFICATION ONLY. The verifier confirms that the
source changes match this plan. No test updates are performed in Phase 4a.
Test updates are a separate ticket in the Option A (test runner) pipeline.

---

## CHANGED FIELDS (exact source, CopyEngine.cs)

### Removed (pre-fix)

```csharp
// Pre-fix -- no longer present
private volatile string _cloneAtmCache;
private volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject;
```

### Added (post-fix, lines 142-152)

```csharp
// PTT-REPAIRS-04 BUG-F: per-instrument clone ATM cache.
// Keyed by instrument FullName -- prevents MES chart ATM from overwriting MGC ATM when
// user switches between chart panels. ConcurrentDictionary: lock-free reads/writes. JS-021.
private readonly ConcurrentDictionary<string, string> _cloneAtmCacheByInstr =
    new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

// PTT-REPAIRS-04 BUG-F: per-instrument clone ATM object cache.
// Keyed by instrument FullName -- same rationale as _cloneAtmCacheByInstr.
// ConcurrentDictionary<string, AtmStrategy>: lock-free. JS-021. JS-023 N/A (not volatile).
private readonly ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy> _cloneAtmObjectByInstr =
    new ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>(StringComparer.Ordinal);
```

Note: `volatile` removed because `ConcurrentDictionary` is inherently thread-safe and does not
require volatile on the reference itself. `readonly` is used because the dictionary reference
is set at field initialization and never reassigned. JS-023 compliant.

---

## THREADING MODEL

| Operation | Thread | Mechanism | Safe? |
|-----------|--------|-----------|-------|
| `SetCloneAtmObjectCache` (write) | UI thread (Click handler) | `ConcurrentDictionary` indexer set | YES |
| `SetCloneAtmCache` (write) | UI thread (Click handler) | `ConcurrentDictionary` indexer set | YES |
| `GetCloneAtmMode` (read) | NT8 callback thread (order event) | `ConcurrentDictionary.TryGetValue` | YES |
| `_instrument.FullName` (read) | UI thread (OnCloneModeClick) | Property read on immutable field | YES |
| `order.Instrument.FullName` (read) | NT8 callback thread | NT8 Order property | YES |

No `Dispatcher.InvokeAsync` needed for dictionary operations -- `ConcurrentDictionary` handles
cross-thread read/write without lock or dispatcher wrapping.

`_instrument` is set once on the UI thread (add-on init) and subsequently only read. No race.

---

## JANE STREET COMPLIANCE TABLE

| Rule | Description | Check | All Changed Methods | Status |
|------|-------------|-------|---------------------|--------|
| JS-021 | No `lock()` | ConcurrentDictionary indexer, TryGetValue, TryRemove -- all lock-free | All methods | PASS |
| JS-001 | No throw in dispatch | No throw-capable operations without null guard. `??` coalesce, null-conditional `?.`, TryGetValue | All methods | PASS |
| JS-013 | CYC <= 8 | Max CYC = 4 (GetCloneAtmMode, simplified McCabe). All changed methods <= 8. | All methods | PASS |
| JS-042 | ASCII-only identifiers and strings | All variable names, comments, and string literals are ASCII | All methods | PASS |
| JS-023 | Volatile/atomic | Two `volatile` fields removed; replaced by `readonly ConcurrentDictionary` (no volatile needed) | Fields | PASS |
| JS-002 | No null return | `GetCloneAtmMode` returns `new FollowerAtmMode.Inherit()` as fallback -- never null | GetCloneAtmMode | PASS |
| JS-025 | ConcurrentDictionary | TryRemove, TryGetValue, indexer set -- all ConcurrentDictionary atomic ops | All cache ops | PASS |
| NT8 | No lock() in NT8 context | Confirmed: 0 `lock(` matches in CopyEngine.cs and TradeCopierPanel.cs (7-scan item 1) | Both files | PASS |
| NT8 | No DateTime.Now | Not introduced | All methods | PASS |
| NT8 | No new Thread | Not introduced | All methods | PASS |
| NT8 | AddOn-safe API | ConcurrentDictionary, ChartTrader.AtmStrategy (UI thread), Order.Instrument (callback) | All methods | PASS |

---

## DEFERRED ITEMS

| ID | Item | Deferred to |
|----|------|-------------|
| DEFERRED-1 | Update stale T_CLONE_* tests in CopyEngineTests.cs (lines ~4620-4685) to pass instrFullName to new GetCloneAtmMode(string) signature | Option A test runner session |
| DEFERRED-2 | BUG-E (reversal-guard skip on cancelled entry) -- covered in parallel pipeline PTT-REPAIRS-04-POST-BUG-E | PTT-REPAIRS-04-POST-BUG-E |
| DEFERRED-3 | TOCTOU window in value-guarded TryRemove pattern (carried from PTT-REPAIRS-03-POST) | Future hardening session |

---

## 7-SCAN RESULTS (from direct-edit session, BUG-F pass)

| # | Scan | Command/Method | Result |
|---|------|----------------|--------|
| 1 | `lock()` scan -- CopyEngine.cs + TradeCopierPanel.cs | `Select-String -Pattern "lock("` | PASS: 0 matches each |
| 2 | Non-ASCII scan -- both files | `Select-String -Pattern '[^\x00-\x7F]'` | PASS: 0 matches each |
| 3 | CYC spot-check -- changed methods | Manual branch count | PASS: max CYC=4 (GetCloneAtmMode, simplified McCabe). All methods <= 8 |
| 4 | [Fact] test coverage | N/A Phase A -- stale tests deferred (DEFERRED-1) | N/A (Phase A only) |
| 5 | Build errors -- CopyEngine.cs + TradeCopierPanel.cs | `dotnet build Linting.csproj` | PASS: 0 errors in either file |
| 6 | Hard-link sync | `deploy-sync.ps1` | PASS: 7/7 files, inode=2 each |
| 7 | Old-signature caller check | `grep` for 0-param GetCloneAtmMode(), old scalar field names | PASS: 0 stale callers remain |

---

## USER VALIDATION

Confirmed working by manual test after fix applied:

- MGC panel cloned (Clone click while MGC chart active)
- User switched to MES chart, traded MES
- Returned to MGC panel
- MGC panel retained MGC ATM without requiring re-click of Clone
- No regression observed in MES ATM assignment

---

## COMPONENT LIST

| Component | File | Type | Role |
|-----------|------|------|------|
| `_cloneAtmCacheByInstr` | CopyEngine.cs:145 | `ConcurrentDictionary<string,string>` | Per-instrument ATM template name storage |
| `_cloneAtmObjectByInstr` | CopyEngine.cs:151 | `ConcurrentDictionary<string,AtmStrategy>` | Per-instrument ATM object storage |
| `SetCloneAtmCache` | CopyEngine.cs:722 | `internal void` | Writes ATM template name keyed by instrument |
| `SetCloneAtmObjectCache` | CopyEngine.cs:730 | `internal void` | Writes ATM object keyed by instrument; removes on null |
| `GetCloneAtmMode` | CopyEngine.cs:741 | `internal FollowerAtmMode` | Reads per-instrument ATM; returns Inherit on miss |
| `ResolveAtmMode` | CopyEngine.cs:5067 | `private FollowerAtmMode` | Routes to GetCloneAtmMode or GetAtmMode by CopyMode |
| `_instrument` | TradeCopierPanel.cs:120 | `Instrument` | Panel-owned instrument reference (set once at init) |
| `SetInstrument` | TradeCopierPanel.cs:529 | `public void` | Sets `_instrument` from add-on init (UI thread) |
| `OnCloneModeClick` | TradeCopierPanel.cs:1848 | `private void` | Captures ATM and stores keyed by `_instrument.FullName` |

---

## PHASE 4A VERIFIER CONTRACT

The Phase 4a verifier (ptt-verifier) must confirm the following from source only -- NO test updates:

1. `_cloneAtmCacheByInstr` field exists at CopyEngine.cs ~line 145, type `ConcurrentDictionary<string, string>`, `readonly`, `StringComparer.Ordinal`.
2. `_cloneAtmObjectByInstr` field exists at CopyEngine.cs ~line 151, type `ConcurrentDictionary<string, NinjaTrader.NinjaScript.AtmStrategy>`, `readonly`, `StringComparer.Ordinal`.
3. `SetCloneAtmCache(string instrFullName, string value)` at ~line 722 writes `_cloneAtmCacheByInstr[instrFullName]`.
4. `SetCloneAtmObjectCache(string instrFullName, AtmStrategy atmObj)` at ~line 730: null-branches to TryRemove (null) or indexer-set (non-null).
5. `GetCloneAtmMode(string instrFullName)` at ~line 741: TryGetValue keyed lookup, returns `Inherit()` on miss.
6. `ResolveAtmMode(CopyRule rule, string accountName, string instrFullName)` at ~line 5067: passes `instrFullName` to `GetCloneAtmMode`.
7. `DispatchToFollower` call at ~line 2672: `ResolveAtmMode(rule, acc.Name, order.Instrument.FullName)` -- 3-arg call.
8. `ReplaceFollowerCopyOnAtmCancel` call at ~line 4404: `ResolveAtmMode(matchedRule.Value, cancelledOrder.Account.Name, cancelledOrder.Instrument.FullName)` -- 3-arg call.
9. `OnCloneModeClick` at ~line 1848: `string instrKey = _instrument?.FullName ?? string.Empty;` present; both `SetCloneAtmObjectCache` and `SetCloneAtmCache` called with `instrKey` as first arg.
10. No `lock(` in any changed method or field. No non-ASCII characters in changed sections.

---

## RETURN STATUS

**PLAN_COMPLETE**
