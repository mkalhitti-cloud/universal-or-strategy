# Ticket 2 Completion — PTT-COPIER-B52 Lane B

**Ticket ID**: T2 — DW-B50-02 (SRC EDIT)
**Title**: Replace _atmComboRefs hard-refs with WeakReference<ComboBox> in TradeCopierPanel.cs
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-08-08

---

## Files Modified

- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` (Wave workspace)
  - EDIT 1 (line ~199): Field declaration changed from `List<ComboBox>` to `List<WeakReference<ComboBox>>`
    - Added B52 comment line
  - EDIT 2 (line ~1480): `UpdateAtmComboVisibility` method body replaced
    - `foreach (var cb in _atmComboRefs)` with null guard → `for` loop with `TryGetTarget` + prune-on-iterate
    - CYC before: 2  |  CYC after: 4
  - EDIT 3 (line ~1974): `OnFollowerAtmTemplateComboLoaded` registration block replaced
    - `_atmComboRefs.Contains(cb)` → `foreach WeakReference TryGetTarget` dedup check
    - `_atmComboRefs.Add(cb)` → `_atmComboRefs.Add(new WeakReference<ComboBox>(cb))`

- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` (Wave workspace)
  - PttBuild.Tag updated from `"PTT-COPIER B52 | test-restore-extraction | 2026-08-08"` to
    `"PTT-COPIER B52 | knowledge-doc-weak-refs | 2026-08-08"`

---

## Scan Results

### SCAN-01 — lock() check

Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "\block\s*\("`

Result: 13 matches found — **all are comments** containing "no lock" text. Zero actual `lock(` calls.

**PASS — 0 actual lock() calls**

### SCAN-02 — async void check

Command: `Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void "`

Result: 2 matches found — **both are comments** referencing "no async void". Zero actual async void declarations.

**PASS — 0 async void declarations**

### SCAN-05 — dotnet build

Command: `dotnet build c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj`

Result:
```
Build succeeded.
19 Warning(s)
0 Error(s)
```

**PASS — 0 errors**

### SCAN-06 — CYC verification for UpdateAtmComboVisibility

Method body (lines 1484-1493):
```csharp
private void UpdateAtmComboVisibility(Visibility v)
{
    for (int i = _atmComboRefs.Count - 1; i >= 0; i--)   // branch (1)
    {
        if (_atmComboRefs[i].TryGetTarget(out var cb))    // branch (2)
            cb.Visibility = v;
        else
            _atmComboRefs.RemoveAt(i);                    // branch (3): prune dead ref
    }
}
```

Branch count: base(1) + for-loop body(1) + TryGetTarget true(1) + TryGetTarget false(1) = **CYC = 4**

4 <= 8  **PASS**

### SCAN-07 — verify_links

Command: `powershell -File c:\WSGTA\universal-or-strategy\scripts\verify_links.ps1`

Result:
```
=== SUMMARY ===
OK      : 15
DESYNC  : 0
MISSING : 0
FIXED   : 0
SKIPPED : 8
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

**PASS — DESYNC=0 MISSING=0**

---

## 7-Scan Checklist (T2)

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | lock() check | 0 actual lock() calls | PASS |
| SCAN-02 | async void check | 0 async void declarations | PASS |
| SCAN-03 | FontFamily | N/A (no new UI elements) | N/A |
| SCAN-04 | #RRGGBB literals | N/A (no new color literals) | N/A |
| SCAN-05 | dotnet build | Build succeeded, 0 errors | PASS |
| SCAN-06 | CYC UpdateAtmComboVisibility | CYC=4, <= 8 | PASS |
| SCAN-07 | verify_links | DESYNC=0 MISSING=0 | PASS |

---

## Status: BUILD_PASS
