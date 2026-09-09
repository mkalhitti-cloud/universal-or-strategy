# PTT-REPAIRS-04 Deferred Items

Session: PTT-REPAIRS-04
Date: 2026-09-06

---

## DEFERRED-1: BUG-F — Clone ATM state not per-instrument

### Symptom
User must re-click Clone on MGC panel after trading MES.
MGC Clone uses MES ATM instead of the MGC ATM that was selected when MGC was cloned.

### Storage (current, broken)

| Field | Location | Type | Problem |
|-------|----------|------|---------|
| `_cloneAtmObject` | CopyEngine.cs line 150 | `volatile AtmStrategy` | Single shared scalar -- overwritten by each Clone click |
| `_cloneAtmCache` | CopyEngine.cs line 145 | `volatile string` | Single shared scalar -- same problem |
| `OnCloneModeClick` | TradeCopierPanel.cs line 1848 | reads `_currentChart` | Single active chart reference -- not per-instrument |

### Root Cause
`OnCloneModeClick` captures `ct.AtmStrategy` from `_currentChart` (the currently focused panel chart).
After the user trades MES, `_currentChart` points to the MES chart.
A re-click of Clone on MGC then captures MES ATM, overwriting the global scalar.
`GetCloneAtmMode()` (CopyEngine line 738) returns this overwritten MES ATM for all Clone dispatches.

### Fix Design (approved for separate session)
Replace:
- `_cloneAtmObject` (scalar) -> `ConcurrentDictionary<string, AtmStrategy> _cloneAtmObjectByInstr`
- `_cloneAtmCache` (scalar) -> `ConcurrentDictionary<string, string> _cloneAtmCacheByInstr`
Key = `instr.FullName` from the chart being cloned.

`SetCloneAtmObjectCache(string instrFullName, AtmStrategy atm)` -- new signature
`SetCloneAtmCache(string instrFullName, string tpl)` -- new signature
`GetCloneAtmMode(string instrFullName)` -- new param
`OnCloneModeClick` extracts instrument from `_currentChart` instrument and passes as key.
`DispatchCopy` passes `instr.FullName` when calling `GetCloneAtmMode`.

### Complexity
Medium. Requires signature changes across CopyEngine + TradeCopierPanel call sites.
Must verify `GetCloneAtmMode` callers (DispatchToFollower chain).
Requires dedicated session: read TradeCopierPanel.cs + CopyEngine call graph first.

### Priority: P2 (user friction but workaround exists: re-click Clone)

---

## DEFERRED-2: Regression test for EvictDedup value-guarded TryRemove (from PTT-REPAIRS-03-POST)

Carried from DW-REPAIRS-03-POST-01: TOCTOU window in value-guarded TryRemove.
Acceptable under NT8 single-threaded OnOrderUpdate model. Revisit if stress test shows issues.

---

*PTT-REPAIRS-04 deferred.md -- 2026-09-06*
