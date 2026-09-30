# BGTM-1 Ticket 4 -- Verification Report

**Ticket**: BGTM-1 Ticket 4 -- TradeCopierWindow.cs License UI
**Verifier**: ptt-verifier (Layer 3 independent)
**Date**: 2026-08-26
**Verdict**: VERIFY_PASS

---

## Files Read

| File | Purpose |
|------|---------|
| `src/PropTraderTools/TradeCopierWindow.cs` | Actual implementation (read in full, 1200+ lines) |
| `docs/brain/BGTM-1/04-tickets.md` | Ticket 4 contract (Ticket 4 section, lines 515-676) |
| `docs/brain/BGTM-1/ticket-4-completion.md` | Engineer Layer 2 self-report |

---

## Layer 3 Independent Scan Results

All scans run independently via `Select-String` (PowerShell) and/or `Get-Content`.

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | `Select-String ... -Pattern "lock\("` | 0 hits | PASS |
| SCAN-02 | `Select-String ... -Pattern "throw new "` | 1 hit at L1007 -- PRE-EXISTING, in `AccountDisplayConverter.ConvertBack` (not a BGTM-1 method; not in scope) | PASS |
| SCAN-03 | `Select-String ... -Pattern "OnActivateClick\|ApplyFeatureFlags\|LoadLicenseKeyDisplay\|OnFeatureFlagsChanged"` | All 4 method declarations found at L379, L397, L432, L448 | PASS |
| SCAN-04 | `Select-String ... -Pattern "_licenseKeyBox\|_licenseStatusText\|_activateBtn"` | All 3 fields declared at L62-64; used in BuildLicenseRow, OnActivateClick, LoadLicenseKeyDisplay, OnFeatureFlagsChanged | PASS |
| SCAN-05 | `Get-Content ... \| Where-Object { $_ -match '[^\x00-\x7F]' }` | 0 hits | PASS |
| SCAN-06 | `Select-String ... -Pattern "FeatureFlagsChanged"` | Subscribe at L151 (OnLoaded), unsubscribe at L181 (OnWindowClosed), handler at L448 | PASS |
| SCAN-07 | `Select-String ... -Pattern "LicenseTxtPath\|LicenseClient"` | LicenseTxtPath declared at L66 (license.txt path), LicenseClient.Validate called at L389 | PASS |

### SCAN-02 Pre-Existing Throw Detail

- **File**: `TradeCopierWindow.cs:1007`
- **Content**: `throw new NotImplementedException("AccountDisplayConverter is one-way only")`
- **Location**: `AccountDisplayConverter.ConvertBack` -- one-way IValueConverter, ConvertBack intentionally unsupported
- **Assessment**: Pre-existing, not in scope of BGTM-1 Ticket 4. Not a gate method, not in OnOrderUpdate or SendCopy. Permitted per DNA rule (applies only to "OnOrderUpdate, SendCopy, or any gate method").
- **Engineer report**: Correctly noted as pre-existing at L1007.
- **Layer 2 vs Layer 3 discrepancy**: None -- both agree this hit is pre-existing and out of scope.

---

## Contract Verification (13 Items)

| # | Contract Item | Status | Evidence (file:line) |
|---|--------------|--------|---------------------|
| 1 | `_licenseKeyBox` (TextBox) field declared | PASS | `TradeCopierWindow.cs:62` |
| 2 | `_licenseStatusText` (TextBlock) field declared | PASS | `TradeCopierWindow.cs:63` |
| 3 | `LicenseTxtPath` static field pointing to `license.txt` | PASS | `TradeCopierWindow.cs:66-69` -- `Path.Combine(...UserDataDir, "PropTraderTools", "license.txt")` |
| 4 | `BuildLicenseRow` present and called from UI construction | PASS | Declared at `TradeCopierWindow.cs:333`; called from `BuildUI()` at `TradeCopierWindow.cs:319` |
| 5 | `OnActivateClick`: reads key, writes license.txt (try/catch), calls `LicenseClient.Validate`, calls `CopyEngine.Instance.SetFlags` | PASS | `TradeCopierWindow.cs:379-393` -- L381 reads key, L383-388 try/catch write, L389 Validate, L390 SetFlags, L391 ApplyFeatureFlags, L392 status text |
| 6 | `ApplyFeatureFlags(FeatureFlags f)` method present | PASS | `TradeCopierWindow.cs:397-429` -- gates _trimBtns, _flattenBtns, _cancelBtns, _beBtns, _modeCb, _addRuleBtn |
| 7 | `LoadLicenseKeyDisplay()` present | PASS | `TradeCopierWindow.cs:432-445` |
| 8 | `OnFeatureFlagsChanged(FeatureFlags f)` present | PASS | `TradeCopierWindow.cs:448-452` |
| 9 | `FeatureFlagsChanged +=` in OnLoaded | PASS | `TradeCopierWindow.cs:151` |
| 10 | `FeatureFlagsChanged -=` in Closed/OnWindowClosed | PASS | `TradeCopierWindow.cs:181` (OnWindowClosed) |
| 11 | No `lock()` in new code | PASS | SCAN-01: 0 hits in entire file |
| 12 | All new methods CYC <= 8 | PASS | BuildLicenseRow=1, OnActivateClick=1, ApplyFeatureFlags=2 (2 null guards), LoadLicenseKeyDisplay=2 (try/catch + ternary), OnFeatureFlagsChanged=1, GetStatusText=3 -- all within limit |
| 13 | No FontFamily, no hex colors, no Unicode | PASS | SCAN-05: 0 non-ASCII; no FontFamily= or #RRGGBB literal in any new method body |

---

## DNA Rules Audit

| Rule | Check | Result |
|------|-------|--------|
| JS-001 (no throw in gate/dispatch methods) | OnActivateClick, BuildLicenseRow, ApplyFeatureFlags, LoadLicenseKeyDisplay, OnFeatureFlagsChanged, GetStatusText -- 0 throw | PASS |
| JS-002 (no return null where non-null expected) | GetStatusText returns string, never null; ApplyFeatureFlags/LoadLicenseKeyDisplay/OnFeatureFlagsChanged are void | PASS |
| JS-021 (no lock) | SCAN-01: 0 lock() in entire file | PASS |
| JS-023 (UI thread marshaling) | OnFeatureFlagsChanged called on UI thread per architecture plan (no Dispatcher.InvokeAsync needed) | PASS |
| JS-008 (Freeze brushes) | All new code uses plain WPF controls without custom brushes; pre-existing brushes via MakeWinBrush(..).Freeze() | PASS |
| CYC <= 8 | Max CYC in new methods = 3 (GetStatusText) | PASS |
| SCAN-03 (no FontFamily) | None in new or existing code | PASS |
| SCAN-04 (no #RRGGBB hex strings) | Comments reference hex values (L81-84) but as plain comment text, not string literals -- no hex color string literals | PASS |
| SCAN-06 (no DateTime.Now) | No DateTime usage in new methods | PASS |
| ASCII-only | SCAN-05: 0 non-ASCII characters | PASS |

---

## Architecture Compliance

### Field Promotion (not in original ticket scope but architecturally correct)

The engineer promoted two previously-local variables to fields:
- `_modeCb` (ComboBox) -- required for ApplyFeatureFlags to gate Mirror mode
- `_addRuleBtn` (Button) -- required for ApplyFeatureFlags to gate Multi-rule

This is a correct design decision. Both fields are declared after the existing `_armBeBtns` field at L55-59.

### BuildLicenseRow Docking Order

`BuildLicenseRow(root)` called at `BuildUI():L319` -- immediately before `root.Children.Add(logScroll)`. This docks the license row above the log area, which is the correct layout position (Dock.Top, fills remaining space for log).

### CopyEngine.Instance.Flags Property

`ApplyFeatureFlags(CopyEngine.Instance.Flags)` called in OnLoaded at L152. This correctly reads the volatile `_flags` field via the `Flags` property (established in Ticket 2). No race condition since this is on the UI thread.

---

## Layer 2 vs Layer 3 Discrepancy Check

| Scan | Engineer (Layer 2) | Verifier (Layer 3) | Discrepancy? |
|------|-------------------|-------------------|-------------|
| SCAN-01 (lock) | 0 hits | 0 hits | None |
| SCAN-02 (throw new) | 1 pre-existing at L1007 (ConvertBack) | 1 pre-existing at L1007 (ConvertBack) | None |
| SCAN-03 (methods present) | All 6 new methods confirmed | All 4 required BGTM methods confirmed + GetStatusText and BuildLicenseRow also found | None |
| SCAN-04 (fields) | Lines 62-69 confirmed | Lines 62-69 confirmed | None |
| SCAN-05 (non-ASCII) | 0 hits | 0 hits | None |
| SCAN-06 (FeatureFlagsChanged) | L151, L181, L448 | L151, L181, L448 | None |
| SCAN-07 (LicenseTxtPath, LicenseClient) | L66, L389 | L66, L389 | None |

**No discrepancies between Layer 2 (engineer) and Layer 3 (verifier) scan results.**

---

## Spec Coverage

Ticket 4 references BGTM-1 deliverable 4 (license key input row, activate button, per-feature UI wiring).

| Deliverable | Status |
|-------------|--------|
| License key TextBox visible in window | PASS -- _licenseKeyBox in BuildLicenseRow |
| Activate button wired | PASS -- _activateBtn.Click += OnActivateClick at L361 |
| license.txt written on activation | PASS -- L384-388 |
| LicenseClient.Validate called | PASS -- L389 |
| CopyEngine.Instance.SetFlags called | PASS -- L390 |
| Per-feature button enable/disable | PASS -- ApplyFeatureFlags gates _trimBtns, _flattenBtns, _cancelBtns, _beBtns, _modeCb, _addRuleBtn |
| License status display | PASS -- _licenseStatusText.Text = GetStatusText(flags) |
| Event subscription (OnLoaded) | PASS -- L151 |
| Event unsubscription (OnWindowClosed) | PASS -- L181 |
| License key populated on window open | PASS -- LoadLicenseKeyDisplay() at L153 reads license.txt |

---

## xUnit Tests

Ticket 4 specification explicitly states: "No new xUnit [Fact] methods required for Ticket 4. WPF controls require a UI dispatcher to instantiate and are out of scope for the xUnit test suite."

No xUnit tests required for this ticket. Compliant.

---

## FINAL VERDICT

**VERIFY_PASS**

All 7 independent scans clean. All 13 contract items satisfied. Zero DNA violations in new code.
Pre-existing `throw new NotImplementedException` at L1007 is out of scope (not a BGTM-1 method, not a gate method). No discrepancies between Layer 2 and Layer 3.