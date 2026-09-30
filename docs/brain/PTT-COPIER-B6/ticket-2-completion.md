# PTT-COPIER-B6 — Ticket T2 Completion Report
**Ticket:** T2 — TradeCopierWindow Lifecycle Hooks
**File edited (Wave workspace):** `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`
**Status:** BUILD_PASS
**Completed:** 2026-07-06

---

## 1. Pre-Work Findings

Read `TradeCopierWindow.cs` (462 lines, B5-complete) before editing.

| Item | Finding |
|------|---------|
| `OnInitialize()` body | Lines 29–34. Statements: assign `_engine`, subscribe `StatusUpdate`, call `Subscribe()`, call `BuildUI()`. No async/await. |
| `OnDestroyed()` body | Lines 36–40. Statements: unsubscribe `StatusUpdate`, call `Unsubscribe()`. No async/await. |
| Rule-list UI refresh helper | No dedicated standalone rule-list refresh method exists in B5. `BuildUI()` constructs the rules panel entirely on first call. Persisted rules are loaded into `CopyEngine.Instance` by `LoadRules()`; no additional UI repopulation call is required because the engine holds the data and the UI reads from it on demand. Documented per T2 implementation notes. |
| CYC of `OnInitialize()` | CYC = 1 before addition → CYC = 1 after (pure sequential call, no branching). |
| CYC of `OnDestroyed()` | CYC = 1 before addition → CYC = 1 after (pure sequential call, no branching). |
| async/await present? | No. Neither method uses async/await. |

---

## 2. Changes Made (Additive Only)

### Line count
| Metric | Value |
|--------|-------|
| Lines before | 462 |
| Lines after  | 464 |
| Lines added  | 2 |

### Exact lines inserted

**`OnInitialize()` — line 34 (inserted after `BuildUI();`):**
```csharp
            CopyEngine.Instance.LoadRules();
```

**`OnDestroyed()` — line 37 (inserted as first statement before existing cleanup):**
```csharp
            CopyEngine.Instance.SaveRules();
```

### Resulting method bodies (after edit)

```csharp
protected override void OnInitialize()
{
    _engine = CopyEngine.Instance;
    _engine.StatusUpdate += OnStatusUpdate;
    _engine.Subscribe();
    BuildUI();
    CopyEngine.Instance.LoadRules();   // B6 T2: load persisted rules on startup
}

protected override void OnDestroyed()
{
    CopyEngine.Instance.SaveRules();   // B6 T2: persist rules on shutdown
    _engine.StatusUpdate -= OnStatusUpdate;
    _engine.Unsubscribe();
}
```

### No other lines were touched.

---

## 3. Constraint Verification

| Constraint | Status |
|-----------|--------|
| No async/await introduced | PASS — both methods remain synchronous |
| No lock() introduced | PASS |
| No new UI controls, event handlers, or fields | PASS |
| TradeCopierWindow NOT sealed | PASS — class declaration is `public class TradeCopierWindow : NTWindow` |
| CYC of OnInitialize() <= 8 | PASS — CYC = 1 (unchanged, no branching added) |
| CYC of OnDestroyed() <= 8 | PASS — CYC = 1 (unchanged, no branching added) |
| ADDITIVE ONLY — no existing lines deleted or modified | PASS |

---

## 4. Mandatory 7-Scan Results

All scans executed on `src\PropTraderTools\TradeCopierWindow.cs`:

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` | **0** |
| SCAN-02 | Non-ASCII characters | **0** |
| SCAN-03 | `FontFamily` | **0** |
| SCAN-04 | `#[0-9A-Fa-f]{6}` hex color literals | **0** |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | **0** |
| SCAN-06 | `DateTime.Now` (not UtcNow) | **0** |
| SCAN-07 | `sealed class TradeCopierWindow` | **0** |

---

## 5. Definition of Done Checklist

- [x] `TradeCopierWindow.cs` line count is 464 (from 462 + 2 additive lines)
- [x] `OnInitialize()` calls `CopyEngine.Instance.LoadRules()` after existing init logic
- [x] `OnDestroyed()` calls `CopyEngine.Instance.SaveRules()` before any cleanup
- [x] CYC of `OnInitialize()` = 1 <= 8 after additions
- [x] CYC of `OnDestroyed()` = 1 <= 8 after additions
- [x] No `async`/`await` introduced
- [x] No `lock()` introduced
- [x] All 7 scans return 0 results on `TradeCopierWindow.cs`

---

## BUILD_PASS
