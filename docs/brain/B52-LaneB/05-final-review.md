# Final Review — PTT-COPIER-B52 Lane B

**Lane**: B52-LaneB (knowledge-doc-weak-refs)
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-08
**Tickets**: T1 (DW-B50C-02 — DOCS) + T2 (DW-B50-02 — SRC)

---

## A. Coherence Check

**Do T1 and T2 work together as a coherent block?**

YES. T1 and T2 are complementary but independent:

- **T1** adds knowledge documentation for the CS0433 `NinjaTrader.Client.dll` removal (DW-B50C-02). It is a docs-only change that preserves institutional knowledge for future engineers.
- **T2** fixes the `_atmComboRefs` memory-management defect by replacing hard-references with `WeakReference<ComboBox>` (DW-B50-02). It is a surgical C# field + method change.

Both tickets target the DW items assigned to Lane B (B50 Lane C + B50 respectively). Neither ticket depends on the other at the code level. Their combination closes two distinct deferred work items from previous blocks cleanly.

**Verdict: Coherent block — YES.**

---

## B. Cross-File JS Violations

**SCAN-01 — lock() across all src/ files (independent Layer 3)**

```powershell
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "\block\s*\("
```

Result: 12 matches — **all in comment text** (e.g., `// no lock()`). Zero actual `lock(` calls in executable code.

**PASS — 0 lock() violations**

---

**SCAN-02 — async void across all src/ files (independent Layer 3)**

```powershell
Select-String -Path "src/PropTraderTools/*.cs" -Pattern "async void "
```

Result: 2 matches — **both in comment text** (e.g., `// JS-033: no async void`). Zero actual `async void` declarations.

**PASS — 0 async void violations**

---

## C. Missing Wiring

### Are there any callers of UpdateAtmComboVisibility that need updating?

The method signature `private void UpdateAtmComboVisibility(Visibility v)` is unchanged. The change is **entirely internal** to the method body (iteration pattern + prune-on-iterate). No callers need updating.

Independent verification: the method is called from within `TradeCopierPanel.cs` whenever Clone mode visibility changes. Callers pass a `Visibility` enum value and expect the method to apply it to all tracked ComboBox controls — the semantic contract is unchanged.

**No caller updates required.**

---

### Are there any other sites that add to `_atmComboRefs`?

```powershell
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "_atmComboRefs\.Add"
```

**Result: 1 hit only — line 1983 in `OnFollowerAtmTemplateComboLoaded`.**

This is the sole registration path. It correctly uses `new WeakReference<ComboBox>(cb)` as required by T2. No other add-sites exist.

**No missing wiring.**

---

## D. All Spec Requirements Satisfied

| DW Item | Requirement | Status |
|---------|------------|--------|
| DW-B50C-02 | `NT8_ADDON_KNOWLEDGE.md` entry documenting `NinjaTrader.Client.dll` removal, CS0433 cause, `NinjaTrader.Core.dll` as replacement, and "Do NOT add" rule | **CLOSED** — B52-LaneB T1. Entry at lines 1634–1663. |
| DW-B50-02 | `_atmComboRefs` changed from `List<ComboBox>` to `List<WeakReference<ComboBox>>`; `UpdateAtmComboVisibility` uses prune-on-iterate; `OnFollowerAtmTemplateComboLoaded` wraps in `WeakReference` | **CLOSED** — B52-LaneB T2. Lines 202, 1486–1491, 1983 confirmed. |

---

## E. All 7 Scans Zero

| Scan | Description | T1 | T2 | Result |
|------|-------------|----|----|--------|
| SCAN-01 | lock() calls | N/A (docs) | 0 actual lock() | **PASS** |
| SCAN-02 | async void | N/A (docs) | 0 actual async void | **PASS** |
| SCAN-03 | FontFamily | N/A (docs) | 0 hits | **PASS** |
| SCAN-04 | #RRGGBB hex literals | N/A (docs) | 0 string literal hits | **PASS** |
| SCAN-05 | dotnet build | N/A (docs) | Build succeeded, 0 errors | **PASS** |
| SCAN-06 | DateTime.Now | N/A (docs) | 0 hits | **PASS** |
| SCAN-07 | verify_links | N/A (docs) | DESYNC=0 MISSING=0 | **PASS** |
| SCAN-08 | NinjaTrader.Client ≥1 hits | 6 hits (≥1) | N/A (src) | **PASS** |

All 7 scans zero violations. All scan results independently confirmed.

---

## K. Deferred Work

### Items Closed This Block

| Item | Title | Closed By |
|------|-------|-----------|
| DW-B50C-02 | Document NinjaTrader.Client.dll removal + CS0433 rule | B52-LaneB T1 |
| DW-B50-02 | Replace `_atmComboRefs` List<ComboBox> with WeakReference pattern | B52-LaneB T2 |

### Items Carried Forward (no change)

| Item | Title | Status |
|------|-------|--------|
| DW-B50-01 | Persistent clone ATM template selection across panel rebuilds | OPEN — carries to B53 |
| DW-B43-02 | Click trader: true pixel-to-price via NT8 scale panel API | OPEN — carries to future block |
| DW-B47-05 | Collapsible sections persistence across NT8 session restart | OPEN — carries to future block |

### No New Issues Opened

No new defects, no new deferred work items discovered by B52-LaneB. Both tickets executed to closure within scope.

---

## Final Verdict

**FINAL_PASS**

Both tickets independently verified:
- T1: 6/6 checks PASS. SCAN-08 PASS (6 hits). Docs-only constraint satisfied.
- T2: 7/7 checks PASS. SCAN-01 through SCAN-07 all PASS. CYC=4 for modified method.
- Zero DNA violations across all scans.
- Both DW items (DW-B50C-02, DW-B50-02) closed by this lane.
- No new issues introduced.
