# PTT-COPIER-B8 — Ticket T2 Completion Report
**Ticket**: T2 — FollowerAtmMode Behavioral Wiring (DW-B7-03)
**Engineer**: ptt-engineer + orchestrator (Phase 4a)
**Date**: 2026-07-08
**Status**: BUILD_PASS

---

## Methods Implemented

### CopyEngine.cs

| Method | Signature | CYC | Notes |
|--------|-----------|-----|-------|
| `SendCopy()` modified | `private bool SendCopy(Account, Instrument, in CopySignal, FollowerAtmMode)` | 5 | signalName always "PTT-Copy"; Market=force market; Named=atmTemplate param |
| `GetAtmMode()` | `private static FollowerAtmMode GetAtmMode(CopyRule, string)` | 2 | Returns Inherit if not found; never null |
| `ParseAtmModeName()` | `internal static FollowerAtmMode ParseAtmModeName(string)` | 3 | Deserializes "Inherit"/"Market"/"Named:XXX" |
| `AtmModeToString()` | `internal static string AtmModeToString(FollowerAtmMode)` | 3 | Serializes mode to string |
| `SetAtmMode()` | `internal void SetAtmMode(string, string, FollowerAtmMode)` | 3 | ConcurrentBag rebuild, ImmutableDictionary.SetItem |
| `DispatchCopy()` updated | Mode retrieved via `GetAtmMode(rule, acc.Name)`, passed to `SendCopy` | 8 (unchanged) | GetAtmMode is a call, not a branch |
| `RuleToDto()` updated | Uses `AtmModeToString(GetAtmMode(rule, accName))` per follower | 3 | Replaces T1 "Inherit" placeholder |
| `DtoToRule()` updated | Parses `FollowerAtmModeNames` via `ParseAtmModeName` into `atmMap` | 5 | B6/B7 XML backward compat preserved |

### TradeCopierPanel.cs

| Item | Notes |
|------|-------|
| `FollowerItem.AtmModeName` string property | Default "Inherit" |
| ATM mode ComboBox in follower row | Items: Inherit/Market; wired to `OnFollowerAtmComboLoaded` + `OnFollowerAtmModeChanged` |
| `OnFollowerAtmComboLoaded()` | Populates items on loaded |
| `OnFollowerAtmModeChanged()` | Sets `item.AtmModeName` on selection change |
| `OnApplyRule()` updated | Collects `AtmModeName` per follower; builds `ImmutableDictionary<string, FollowerAtmMode>` atmMap |
| `ParseAtmModeNameLocal()` | Private static mirror of `CopyEngine.ParseAtmModeName` |

### TradeCopierWindow.cs

| Item | Notes |
|------|-------|
| `BuildRuleRow()` Col 9 | ATM ComboBox (Inherit/Market, Width=80) added |
| `BuildDynamicRuleRow()` Col 9 | ATM ComboBox added, reference stored in `atmCbDyn` |
| Column definitions | Added 10th column (Auto width) to both row builders |
| `OnRowApply()` updated | Reads `tag[3]` (ComboBox), builds `atmMap`, calls 5-arg `AddRule()` |
| `using System.Collections.Immutable` | Added |

---

## 7-Scan Results (Independent)

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` | **ZERO** |
| SCAN-02 | `throw new \w+Exception` in dispatch | **ZERO** |
| SCAN-03 | `return null` in new B8 methods | **ZERO** — pre-existing nullable returns only |
| SCAN-04 | `new Dictionary<` mutable | **ZERO** — only ImmutableDictionary + ConcurrentDictionary |
| SCAN-05 | `DateTime.Now` | **ZERO** |
| SCAN-06 | `async void` | **ZERO** |
| SCAN-07 | Hex `#RRGGBB` | **ZERO** — MakeWinBrush(r,g,b) pattern only |

---

## Regression Check

- CopyEngineTests.cs [Fact] count: **27** — unmodified
- 3-arg `AddRule()` overload: **PRESERVED UNCHANGED** at CopyEngine.cs:189-192

---

## DW-B7-03 Satisfaction

- `FollowerAtmMode` sealed hierarchy: Inherit / Market / Named(string) — COMPLETE
- `SendCopy()` dispatches on mode: Inherit=pass-through, Market=force market, Named=ATM template param — COMPLETE
- Per-follower ATM ComboBox in Panel — COMPLETE
- Per-rule ATM ComboBox in Window — COMPLETE
- Persistence: ATM mode names serialized/deserialized via `AtmModeToString`/`ParseAtmModeName` — COMPLETE
- Backward compat: B6/B7 XML with no FollowerAtmModeNames → all Inherit default — COMPLETE
