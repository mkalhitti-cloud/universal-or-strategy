# B42-QX-BE-01 Ticket T2 — Completion Report

**Status**: BUILD_PASS
**Block**: B42-QX-BE-01
**Ticket**: T2
**Engineer**: ptt-engineer
**Date**: 2026-08-05
**Target file**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

---

## Rules Catalog Gate — PASS

| Rule | Verdict |
|------|---------|
| JS-001 (no throw in hot paths) | PASS — no throw introduced |
| JS-002 (no return null) | PASS — method returns void |
| JS-021 (no lock) | PASS — expression-body delegation, no lock |
| JS-033 (no async void) | PASS — synchronous void method |

---

## Changes Applied

### Change 1 — `CancelQxBrackets` argument flip (line 2231)

**File**: `src/PropTraderTools/CopyEngine.cs`

**Before** (line 2230):
```csharp
// B41: CancelQxBrackets -- public entry point for PttQuickExit bracket cleanup.
// cancelPttQx=true: wipes previous PTT-QX orders; cancelPttBe=false: preserves PTT-BE orders.
// CYC=1: straight delegation to private CancelStaleBrackets.
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: false, cancelPttQx: true);
```

**After** (line 2231):
```csharp
// B41: CancelQxBrackets -- public entry point for PttQuickExit bracket cleanup.
// BUG-B42-QX-BE-01 FIX (Direction 2): cancelPttBe: true so PTT-BE-* orders
// are also cancelled when Quick All fires after BE All. Clean slate guaranteed.
// CYC=1: straight delegation to private CancelStaleBrackets.
internal void CancelQxBrackets(Account acc, Instrument instr)
    => CancelStaleBrackets(acc, instr, cancelPttBe: true, cancelPttQx: true);
```

### Change 2 — `PttBuild.Tag` updated (line 41)

**Before**: `"PTT-COPIER B41 | quick-exit | 2026-08-05"`
**After**: `"PTT-COPIER B42 | qx-be-interaction | 2026-08-05"`

---

## 7-Scan Results

| Scan | Description | Command / Check | Result |
|------|-------------|-----------------|--------|
| SCAN-01 | No `lock(` in touched method | `Select-String -Pattern "lock\s*\("` — all hits are in comments only (e.g. "no lock (JS-021)") | PASS — 0 actual `lock(` calls |
| SCAN-02 | No `return null` introduced | `Select-String -Pattern "return\s+null\s*;"` filtered to non-comments — all 4 hits are pre-existing in unrelated methods, not in `CancelQxBrackets` (returns void) | PASS |
| SCAN-03 | No `async void` introduced | `Select-String -Pattern "async\s+void\s+\w+\("` — zero output | PASS — 0 hits |
| SCAN-04 | `CancelStaleBrackets` body unchanged | Read lines 1779–1802 — body identical to pre-edit; only the call-site argument in `CancelQxBrackets` changed | PASS |
| SCAN-05 | `CancelQxBrackets` CYC stays 1 | Single `=>` expression body, no branches, no conditions | PASS — CYC = 1 |
| SCAN-06 | No `IsAtmTargetName` changes | N/A for CopyEngine.cs — `IsAtmTargetName` lives in PttBreakEven.cs only | PASS — N/A |
| SCAN-07 | No new instance fields | `Select-String -Pattern "private.*_qxBe|private.*_cancelBe"` — 0 output | PASS — 0 new fields |

**All 7 scans: ZERO violations.**

---

## Rationale

`CancelStaleBrackets` uses the flag-guarded filter at line 1787:

```csharp
&& (cancelPttBe || !o.Name.StartsWith("PTT-BE-"))
```

With `cancelPttBe: false` (old), `!o.Name.StartsWith("PTT-BE-")` was `false` for any `PTT-BE-*`
order, so those orders were excluded from the cancel sweep. After BE All fires, `PTT-BE-Stop-1`
and `PTT-BE-Target-1` survived.

With `cancelPttBe: true` (new), `(true || anything)` is always `true`, so all `PTT-BE-*` orders
are included in the cancel sweep. Quick All after BE All now starts from a clean slate.

---

## BUILD_PASS
