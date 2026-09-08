# PR-121 Ticket 2 — Verification Report

**File**: `C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs`
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-08-22
**Epic**: PR-121 Group G2

---

## Scope Lock

Verified: ONLY PR-121 Ticket 2 changes examined. No other ticket completion files read this session.

---

## 7 Independent Scans (Layer 3 — Verifier Re-run)

| # | Scan | Command | Result | Status |
|---|------|---------|--------|--------|
| 1 | `lock(` mutex usage | `Select-String ... -Pattern "^\s*lock\s*\("` | **0 real lock() statements** (4 hits are `titleBlock`, `TitleBlock`, and comments — confirmed non-mutex) | PASS |
| 2 | `async void` | `Select-String ... -Pattern "async void "` | **0 hits** | PASS |
| 3 | `return null;` | `Select-String ... -Pattern "return null;"` | **2 hits — lines 1205, 1212** — both in pre-existing `FindInstrument()` returning `Instrument?`, not in Ticket-2 touched methods | PASS (pre-existing, not new) |
| 4 | Lizard CCN > 8 | `lizard ... --CCN 8` | **"No thresholds exceeded"** — Warning cnt: 0. `ApplyMirrorModeFlag` = CCN 8 (exactly at limit, not exceeding) | PASS |
| 5 | dotnet build | `dotnet build Linting.csproj` | **Build succeeded. 0 Warning(s). 0 Error(s).** | PASS |
| 6 | `_modeCb.IsEnabled = f.MirrorMode` | `Select-String ... -Pattern "_modeCb\.IsEnabled\s*=\s*f\.MirrorMode"` | **0 hits** | PASS |
| 7 | `IsNullOrWhiteSpace(name)` in OnRuleToggle | `Select-String ... -Pattern "IsNullOrWhiteSpace\(name\)"` | **1 hit — line 1013** (inside `OnRuleToggle`) | PASS |

**Note on SCAN-01**: The pattern `lock(` returns 4 hits but none are synchronization lock statements:
- Line 248: `var titleBlock = BuildWindowTitleBlock();`
- Line 276: `private TextBlock BuildWindowTitleBlock()`
- Lines 619, 792: inline comments (`no lock()`)
No `lock(_xxx)` or `Monitor.Enter` present. **JS-021 compliant.**

**Note on SCAN-03**: `return null;` hits at lines 1205 and 1212 are both inside `FindInstrument(string name)` — a pre-existing helper that returns `Instrument?`. This is not a Ticket-2 change and not in any gate method (not in `OnOrderUpdate`, `SendCopy`, or any event handler). **Not a new violation.**

---

## Fix Verification (4 Fixes)

### Fix 1 — S3/C1/C2/R7: RefreshRuleRows — 8-collection clears + Account.All binding

**Location**: [`RefreshRuleRows()`](C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs:161)  lines 161–192

**Ordering check — clears BEFORE `_rulesPanel.Children.Clear()`**:
- Line 171: `_leaderBoxes.Clear();`
- Line 172: `_followerBoxes.Clear();`
- Line 173: `_beBtns.Clear();`
- Line 174: `_trimBtns.Clear();`
- Line 175: `_flattenBtns.Clear();`
- Line 176: `_cancelBtns.Clear();`
- Line 177: `_armBeBtns.Clear();`
- Line 178: `_tightenBtns.Clear();`
- Line 179: `_rulesPanel.Children.Clear();` ← AFTER all 8

All 8 tracking collections cleared BEFORE panel clear. ✅

**Account.All binding AFTER BuildRuleRow loop**:
- Lines 183–184: `foreach (var instr in instruments)` → `BuildRuleRow` + `Children.Add`
- Line 186–187: `foreach (var cb in _leaderBoxes) cb.ItemsSource = Account.All;`
- Line 188–189: `foreach (var lb in _followerBoxes) lb.ItemsSource = Account.All;`

Both `_leaderBoxes` and `_followerBoxes` bound to `Account.All` AFTER `BuildRuleRow` populates them. ✅

**Result: CONFIRMED** — Fix 1 implemented correctly.

---

### Fix 2 — C4/R4: ApplyFeatureFlags/_modeCb — ComboBox always enabled, Mirror item selectively disabled

**Location**: [`ApplyFeatureFlags()`](C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs:448) line 456; [`ApplyMirrorModeFlag()`](C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs:465) lines 465–482

**`_modeCb.IsEnabled = f.MirrorMode` removed**: 0 grep hits. ✅

**`_modeCb.IsEnabled = true` present**: Line 469 inside `ApplyMirrorModeFlag`. ✅

**Mirror item selectively disabled**: `ApplyMirrorModeFlag` loops `_modeCb.Items`, finds items containing "Mirror" via `IndexOf("Mirror", OrdinalIgnoreCase)`, retrieves container via `ItemContainerGenerator.ContainerFromItem`, sets `container.IsEnabled = mirrorEnabled` (line 479). ✅

**`ApplyFeatureFlags` delegates**: Line 456 `ApplyMirrorModeFlag(f.MirrorMode);` — no inline `_modeCb.IsEnabled` in `ApplyFeatureFlags` body. ✅

**CCN check**: `ApplyFeatureFlags` = CCN 3 (lizard), `ApplyMirrorModeFlag` = CCN 8 (lizard) — both within CCN ≤ 8. ✅

**Result: CONFIRMED** — Fix 2 implemented correctly.

---

### Fix 3 — C5/R6: TryParseArmBeBuffer — non-negative guard

**Location**: [`TryParseArmBeBuffer()`](C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs:1049) lines 1049–1056

**Actual code (line 1053)**:
```csharp
if (bufBox != null && int.TryParse(bufBox.Text, out int parsed) && parsed >= 0)
    buf = parsed;
```

`&& parsed >= 0` guard is present. ✅  
Negative values fall through to default `buf = 2`. ✅  
Pattern matches `TryParseBeTicksFromTag`. ✅

**Result: CONFIRMED** — Fix 3 implemented correctly.

---

### Fix 4 — R5: OnRuleToggle — IsNullOrWhiteSpace(name) guard

**Location**: [`OnRuleToggle()`](C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs:1007) lines 1007–1019

**Actual code (lines 1012–1014)**:
```csharp
string name = btn.Tag is TextBox tb ? tb.Text : btn.Tag as string;
if (string.IsNullOrWhiteSpace(name))
    return;
```

Guard at line 1013 immediately after `name` is derived from `btn.Tag`. ✅  
`_engine.SetRuleEnabled(name, newState)` at line 1018 is only reached after guard passes. ✅

**Result: CONFIRMED** — Fix 4 implemented correctly.

---

## Cross-Check: Engineer Layer 2 vs. Verifier Layer 3

| Item | Engineer Reported | Verifier Found | Match? |
|------|-------------------|----------------|--------|
| SCAN-01 `lock(` (real) | 0 hits | 0 real lock() statements (4 `titleBlock`/comment hits) | ✅ Match |
| SCAN-02 `async void` | 0 hits | 0 hits | ✅ Match |
| SCAN-03 `return null` | "0 new instances" | 2 pre-existing in `FindInstrument()` | ✅ Match (pre-existing, not new) |
| SCAN-04 CCN > 8 | Warning cnt: 0 | Warning cnt: 0 (`ApplyMirrorModeFlag` = 8, exactly at limit) | ✅ Match |
| SCAN-05 Build | 0 errors, 0 warnings | 0 errors, 0 warnings (`Linting.csproj`) | ✅ Match |
| SCAN-06 `_modeCb.IsEnabled = f.MirrorMode` | 0 hits | 0 hits | ✅ Match |
| SCAN-07 `IsNullOrWhiteSpace(name)` | Hit at line 1011 | Hit at line 1013 | ✅ Match (line diff: engineer reported 1011, actual 1013 — within 2-line margin, same expression) |
| Fix 1 ordering | 8 clears before panel clear, Account.All after | Confirmed lines 171–189 | ✅ Match |
| Fix 2 `ApplyMirrorModeFlag` | New helper, `_modeCb.IsEnabled = true` at line 469 | Confirmed lines 465–482 | ✅ Match |
| Fix 3 `&& parsed >= 0` | Line 1053 | Line 1053 | ✅ Match |
| Fix 4 guard at line 1011 | Reported line 1011 | Actual line 1013 | ✅ Match (2-line offset, same guard) |

**Discrepancy noted**: Engineer reported `IsNullOrWhiteSpace(name)` at line 1011; actual line is 1013. This is a minor line-number discrepancy (2 lines), likely due to comment insertion. The guard itself is present and correct — not a violation.

---

## DNA Rules Check (Jane Street / NT8 Constraints)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock()` | No real lock statements | ✅ PASS |
| JS-033 `async void` | 0 hits | ✅ PASS |
| JS-002 `return null` in gate methods | Only in `FindInstrument()` (pre-existing) | ✅ PASS |
| NT8 `Account.All` outside Loaded handler | `Account.All` in `RefreshRuleRows` inside `Dispatcher.InvokeAsync` — called only from `OnLoaded` path | ✅ PASS |
| CCN ≤ 8 | All methods ≤ 8 (ApplyMirrorModeFlag exactly 8) | ✅ PASS |
| `async/await` in OnInitialize/OnDestroyed | None present | ✅ PASS |
| `sealed` on TradeCopierWindow | Not sealed | ✅ PASS |
| `FontFamily` usage | 0 hits | ✅ PASS |
| `#RRGGBB` hex colors | 0 code hits (comments only) | ✅ PASS |
| `DateTime.Now` (not UtcNow) | 0 hits | ✅ PASS |

---

## Summary

All 4 Ticket 2 fixes are present and correctly implemented:

1. **Fix 1** (RefreshRuleRows): All 8 tracking collections cleared at lines 171–178 before `_rulesPanel.Children.Clear()` at line 179. `Account.All` bound at lines 186–189 after `BuildRuleRow` loop. ✅
2. **Fix 2** (ApplyFeatureFlags): `_modeCb.IsEnabled = f.MirrorMode` removed (0 hits). New `ApplyMirrorModeFlag` helper at lines 465–482: `_modeCb.IsEnabled = true` (line 469), Mirror item selectively disabled via container. ✅
3. **Fix 3** (TryParseArmBeBuffer): `&& parsed >= 0` guard at line 1053. ✅
4. **Fix 4** (OnRuleToggle): `string.IsNullOrWhiteSpace(name)` guard at line 1013. ✅

All 7 scans pass. Build: 0 errors. No DNA violations introduced.

---

## Return Status

**VERIFY_PASS**