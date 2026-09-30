# BGTM-1 Architecture Plan

**Block**: BGTM-1 (License Gating + Feature Flags)
**Status**: REVIEW_PENDING
**Date**: 2026-08-26
**Architect**: ptt-architect
**Phase**: 1 (Architecture)

---

## 0. Rules Catalog Gate

| Rule | Status | Notes |
|------|--------|-------|
| JS-001 (no throw in LicenseClient) | PASS | All error paths return null/Starter() |
| JS-002 (no return null public API) | PASS | Only private helpers return FeatureFlags? |
| JS-003 (sealed record for FeatureFlags) | PASS | `internal sealed record FeatureFlags` |
| JS-021 (no lock) | PASS | No lock() anywhere in new code |
| JS-023 (volatile for shared mutable ref) | PASS | `private volatile FeatureFlags _flags` |
| CYC <= 8 (all new methods) | PASS | See per-method table in T1 |
| DateTime.Now ban | PASS | Only DateTime.UtcNow used |
| No hex literals | PASS | ApplyFeatureFlags uses MakeWinBrush(r,g,b) |
| ASCII-only strings | PASS | All literals are ASCII |
| No FontFamily | PASS | No font assignments |

---

## 1. Component List

| Component | Type | File | Ticket |
|-----------|------|------|--------|
| `FeatureFlags` | `internal sealed record` | `LicenseClient.cs` | T1 |
| `LicenseClient` | `internal static class` | `LicenseClient.cs` | T1 |
| `CacheEntry` | `[DataContract] class` (private, JSON DTO) | `LicenseClient.cs` | T1 |
| CopyEngine `_flags` field + `SetFlags` + `Flags` + event | Field/method/property/event on existing class | `CopyEngine.cs` | T2 |
| Gate guards on 9+ CopyEngine methods | First-line guards | `CopyEngine.cs` | T2 |
| `TradeCopierAddOn` license init | `State.Configure` block | `TradeCopierAddOn.cs` | T3 |
| `TradeCopierAddOn.RegisterClickTrader` gate | First-line guard | `TradeCopierAddOn.cs` | T3 |
| License row UI + `OnActivateClick` + `ApplyFeatureFlags` | Window methods | `TradeCopierWindow.cs` | T4 |
| `TradeCopierPanel.ApplyFeatureFlags` | Panel method | `TradeCopierPanel.cs` | T5 |
| `PttGlobalQuickExit.Execute()` gate | First-line guard | `Features/PttGlobalQuickExit.cs` | T6 |
| `BgtmTests.cs` | xUnit test file | `Tests/BgtmTests.cs` | T6 |
| `build-release.ps1` | PowerShell build script | `scripts/build-release.ps1` | T6 |
| `confuserex.crproj` | ConfuserEx project file | `confuserex.crproj` | T6 |

---

## 2. FeatureFlags Sealed Record

**File**: `src/PropTraderTools/LicenseClient.cs`
**Namespace**: `PropTraderTools`

```csharp
internal sealed record FeatureFlags(
    bool MultiRule,      // Pro+
    bool TrimFlatten,    // Pro+
    bool BreakEven,      // Pro+
    bool AtrSizing,      // Elite only
    bool ClickTrader,    // Elite only
    bool MirrorMode,     // Elite only
    bool QxGlobalExit    // Elite only
)
{
    public static FeatureFlags Starter() => new(false,false,false,false,false,false,false);
    public static FeatureFlags Pro()     => new(true,true,true,false,false,false,false);
    public static FeatureFlags Elite()   => new(true,true,true,true,true,true,true);
    public static FeatureFlags FromFeatureList(IReadOnlyList<string> feats) => new(
        MultiRule:    feats.Contains("multi_rule"),
        TrimFlatten:  feats.Contains("trim_flatten"),
        BreakEven:    feats.Contains("break_even"),
        AtrSizing:    feats.Contains("atr_sizing"),
        ClickTrader:  feats.Contains("click_trader"),
        MirrorMode:   feats.Contains("mirror_mode"),
        QxGlobalExit: feats.Contains("qx_global_exit"));
}
```

**NT8 Records Workaround (MANDATORY)**:
C# positional records generate `IsExternalInit` which NT8's Roslyn rejects (CS0518). Per
existing codebase precedent (CopyEngine.cs L78-79 comment). Engineer must add the shim
at the TOP of `LicenseClient.cs` before the namespace declaration:

```csharp
// NT8 Roslyn records shim -- CS0518 workaround (same pattern as FollowerAtmMode)
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
```

Also bump csproj `<LangVersion>8.0</LangVersion>` to `<LangVersion>9.0</LangVersion>` for
LSP IntelliSense to resolve record syntax.

**JS-003**: `sealed record` is the correct type for FeatureFlags. It is immutable by
construction, value-equality semantics, and carries no mutable state.

---

## 3. LicenseClient Class

**File**: `src/PropTraderTools/LicenseClient.cs`
**Namespace**: `PropTraderTools`
**Type**: `internal static class LicenseClient`

### 3.1 Constants and Paths

```csharp
private const string ProductId = "PTT_COPIER_V1";

private static string CachePath =>
    System.IO.Path.Combine(
        NinjaTrader.Core.Globals.UserDataDir,
        "PropTraderTools",
        "license_cache.json");
```

### 3.2 Method Signatures

| Method | Return | CYC | Notes |
|--------|--------|-----|-------|
| `Validate(string key)` | `FeatureFlags` | 4 | Public entry. Never throws. |
| `TryRemoteValidate(string key)` | `FeatureFlags?` | 3 | HTTP call, try/catch returns null on fail |
| `ParseSkmResponse(object lic)` | `FeatureFlags?` | 2 | Extracts feature list from SKM response |
| `TryReadCache(string key)` | `FeatureFlags?` | 4 | File check + parse + expiry |
| `DeserializeCache(string json)` | `CacheEntry?` | 3 | DataContractJsonSerializer parse |
| `WriteCache(string key, FeatureFlags flags)` | `void` | 2 | File write, swallow exceptions |

### 3.3 Validate Method (CYC=4)

```
Validate(key):
  (1) if string.IsNullOrWhiteSpace(key) → return Starter()
  (2) cached = TryReadCache(key)
      if cached != null → return cached         [branch 2]
  (3) remote = TryRemoteValidate(key)
      if remote != null → WriteCache(key, remote); return remote  [branch 3]
  (4) return Starter()                           [fallback]
CYC = 1 + 3 branches = 4. PASS.
```

### 3.4 TryRemoteValidate Method (CYC=3)

Uses Cryptolens SDK (`SKGL.Extension` / `SKM.NET.Standard`):
- Call `SKM.V3.Methods.Key.Activate(...)` with ProductId and key
- Wrap in `try { } catch (Exception) { return null; }` (branch 1: exception path)
- Check response null/failure (branch 2)
- Return `ParseSkmResponse(response.LicenseKey)` — may return null (branch 3 skipped, comes from callee)
- CYC = 1 (base) + 2 (try/catch + null check) = 3. PASS.

### 3.5 ParseSkmResponse (CYC=2)

```
ParseSkmResponse(LicenseKey lic):
  (1) if lic == null → return null
  (2) var feats = lic.DataObjects.Select(d => d.Name).ToList()
      return FeatureFlags.FromFeatureList(feats)
CYC = 1 + 1 = 2. PASS.
```

Note: The `LicenseKey.DataObjects` collection contains feature names as DataObject.Name strings.
This maps cleanly to `FromFeatureList`.

### 3.6 TryReadCache Method (CYC=4)

```
TryReadCache(key):
  (1) if !File.Exists(CachePath) → return null
  try {
    json = File.ReadAllText(CachePath)
    entry = DeserializeCache(json)                    [branch 2: try/catch]
    if entry == null → return null                    [branch 3]
    if entry.Key != key || entry.ExpiresUtc < DateTime.UtcNow → return null  [branch 4]
    return FeatureFlags.FromFeatureList(entry.Features)
  } catch (Exception) { return null; }
CYC = 1 + 3 = 4. PASS.
```

### 3.7 DeserializeCache (CYC=3)

```
DeserializeCache(json):
  try {
    using ms = new MemoryStream(...)
    var ser = new DataContractJsonSerializer(typeof(CacheEntry))
    return (CacheEntry)ser.ReadObject(ms)            [branch 1: try body]
  }
  catch (SerializationException) { return null; }   [branch 2]
  catch (Exception) { return null; }                 [branch 3]
CYC = 1 + 2 = 3. PASS.
```

### 3.8 WriteCache (CYC=2)

```
WriteCache(key, flags):
  try {
    Directory.CreateDirectory(Path.GetDirectoryName(CachePath))  [branch 1: may throw, caught]
    var entry = new CacheEntry { Key=key, Features=..., CachedUtc=..., ExpiresUtc=... }
    // serialize to JSON via DataContractJsonSerializer
    File.WriteAllText(CachePath, json)
  }
  catch (Exception) { /* swallow -- offline write failure is benign */ }  [branch 2]
CYC = 1 + 1 = 2. PASS.
```

### 3.9 CacheEntry DTO

```csharp
[System.Runtime.Serialization.DataContract]
private sealed class CacheEntry
{
    [System.Runtime.Serialization.DataMember(Name = "key")]
    public string Key { get; set; }

    [System.Runtime.Serialization.DataMember(Name = "features")]
    public List<string> Features { get; set; }

    [System.Runtime.Serialization.DataMember(Name = "cached_utc")]
    public DateTime CachedUtc { get; set; }

    [System.Runtime.Serialization.DataMember(Name = "expires_utc")]
    public DateTime ExpiresUtc { get; set; }
}
```

Cache TTL: `ExpiresUtc = DateTime.UtcNow.AddDays(7)` (7-day TTL per spec).

### 3.10 SKM.NET NuGet / NT8 Deployment (CRITICAL)

NT8's internal Roslyn compiler does NOT support NuGet PackageReference resolution. The
Cryptolens SDK DLL must be deployed manually:

1. Engineer downloads `SKGL.Extension` NuGet package (or `SKM.NET.Standard`).
2. Extracts the net46/net48 `SKGL.Extension.dll` (or `SKM.Standard.dll`).
3. Copies DLL to: `$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\`
4. In `PropTraderTools.csproj`, add:
   ```xml
   <Reference Include="SKGL.Extension">
     <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
     <Private>false</Private>
   </Reference>
   ```
5. Also add for LSP convenience:
   ```xml
   <PackageReference Include="SKGL.Extension" Version="2.0.23" />
   ```

The `PackageReference` is for OmniSharp IntelliSense only. The `Reference` with HintPath is
what makes NT8 resolve the type at compile time.

---

## 4. CopyEngine.cs Modifications

**File**: `src/PropTraderTools/CopyEngine.cs`

### 4.1 New Field, Property, Method, Event

```csharp
// BGTM-1: Feature flags -- volatile reference (atomic on CLR 4.0+, JS-023 compliant).
// SetFlags called from UI thread (AddOn startup + Window activate click).
// Read from UI thread only (gate checks in engine methods).
private volatile FeatureFlags _flags = FeatureFlags.Starter();

// BGTM-1: Current flags snapshot (read-only property)
public FeatureFlags Flags => _flags;

// BGTM-1: Event fires on UI thread when license activation changes flags.
// Subscribers: TradeCopierWindow, TradeCopierPanel.
public event Action<FeatureFlags> FeatureFlagsChanged;

// BGTM-1: SetFlags -- assign flags and broadcast event. CYC=1.
// JS-021: no lock. Called on UI thread only.
internal void SetFlags(FeatureFlags f)
{
    _flags = f;
    FeatureFlagsChanged?.Invoke(f);
}
```

**Placement**: Add the four members after the `_cloneAtmObject` field block (~L151)
and before the `_globalBe` field.

### 4.2 Gate Method Table

All gates are added as the FIRST LINE of each method listed below.
Pattern: `if (<!flag>) { StatusUpdate("<message>"); return; }`
For `GetSuggestedQty`: `if (!_flags.AtrSizing) return 1;` (early return, no StatusUpdate needed per spec).

| Method | Gate Flag | CYC delta | Return path |
|--------|-----------|-----------|-------------|
| `AddRule(string instr, Account master, Account[] followers)` L1097 | `!_flags.MultiRule && _rules.Count >= 1` | +1 | `return;` |
| `AddRule(string, Account, Account[], int[], ...)` L1106 (full overload) | `!_flags.MultiRule && _rules.Count >= 1` | +1 | `return;` |
| `Trim(Instrument instrument)` L2785 | `!_flags.TrimFlatten` | +1 | `return;` |
| `Trim(Account leader, Instrument instrument)` L2799 | `!_flags.TrimFlatten` | +1 | `return;` |
| `Trim(Instrument instr, int exitBuffer, double ask, double bid)` L3058 | `!_flags.TrimFlatten` | +1 | `return;` |
| `Flatten(Instrument instrument)` L2791 | `!_flags.TrimFlatten` | +1 | `return;` |
| `Flatten(Account leader, Instrument instrument)` L2817 | `!_flags.TrimFlatten` | +1 | `return;` |
| `Flatten(Instrument instr, int exitBuffer, double ask, double bid)` L3072 | `!_flags.TrimFlatten` | +1 | `return;` |
| `CancelPendingEntries(Account leader, Instrument instrument)` L2835 | `!_flags.TrimFlatten` | +1 | `return;` |
| `CancelPendingEntries(Instrument instrument)` L3083 | `!_flags.TrimFlatten` | +1 | `return;` |
| `BreakEven(Instrument instrument, int bufferTicks)` L3684 | `!_flags.BreakEven` | +1 | `return;` |
| `BreakEven(Account leader, Instrument instrument, int bufferTicks)` L3698 | `!_flags.BreakEven` | +1 | `return;` |
| `ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)` L3932 | `!_flags.BreakEven` | +1 | `return;` |
| `SetCopyMode(CopyMode mode)` L548 | `!_flags.MirrorMode && mode == CopyMode.Mirror` | +1 | `return;` |
| `SetAtrEngine(AtrSizingEngine engine, bool enabled)` L525 | `!_flags.AtrSizing && enabled` gate: `enabled = false;` (no return, just force-disable) | +1 | assignment only |
| `GetSuggestedQty(Instrument instrument)` L1066 | `!_flags.AtrSizing` | +1 | `return 1;` |

**Status messages** (all ASCII, proptradertools.com/pricing in MultiRule message):

```
AddRule gate:     "Multi-rule requires Pro. Upgrade at proptradertools.com/pricing"
TrimFlatten gate: "Trim/Flatten requires Pro tier"
BreakEven gate:   "Break Even requires Pro tier"
AtrSizing gate:   "ATR sizing requires Elite tier"  (SetAtrEngine: force enabled=false, no StatusUpdate)
MirrorMode gate:  "Mirror mode requires Elite tier"
```

**CYC pre-check note for engineer**: Before adding the gate, check the current CYC comment
on each method. If a method's CYC comment shows `CYC=8 (AT LIMIT)`, the engineer must
extract an inner helper to create room for the gate (+1 branch). Expected risk methods:
`DispatchCopy` (already at CYC=8 per B12 comment) — this method is NOT in the gate list.
All gated methods are expected to be at CYC ≤ 7 based on their descriptions.

---

## 5. TradeCopierAddOn.cs Modifications

**File**: `src/PropTraderTools/TradeCopierAddOn.cs`

### 5.1 State.Configure Block (license init)

Add to `OnStateChange()` after the existing `State.SetDefaults` block:

```csharp
if (State == State.Configure)
{
    var flags = LoadAndValidateLicense();
    CopyEngine.Instance.SetFlags(flags);
}
```

New private helper (keeps OnStateChange CYC clean):

```csharp
// BGTM-1: Read license.txt and validate. CYC=2.
// JS-001: no throw -- catch returns Starter().
// NT8-021: File.ReadAllText is safe in State.Configure.
private static FeatureFlags LoadAndValidateLicense()
{
    try
    {
        var licenseTxt = System.IO.Path.Combine(
            NinjaTrader.Core.Globals.UserDataDir,
            "PropTraderTools",
            "license.txt");
        var key = System.IO.File.Exists(licenseTxt)
            ? System.IO.File.ReadAllText(licenseTxt).Trim()
            : string.Empty;
        return LicenseClient.Validate(key);
    }
    catch (Exception)
    {
        return FeatureFlags.Starter();
    }
}
```

**Signature**: `private static FeatureFlags LoadAndValidateLicense()` — CYC=2.

### 5.2 RegisterClickTrader Gate

Prepend to `RegisterClickTrader(Chart chart, TradeCopierPanel panel)` at L287:

```csharp
if (!CopyEngine.Instance.Flags.ClickTrader)
{
    StatusUpdate("Click Trader requires Elite tier");
    return;
}
```

Current method CYC=2. After gate: CYC=3. PASS.

---

## 6. TradeCopierWindow.cs Modifications

**File**: `src/PropTraderTools/TradeCopierWindow.cs`

### 6.1 New Fields

```csharp
// BGTM-1: License UI controls
private TextBox _licenseKeyBox;
private TextBlock _licenseStatusText;
private Button _activateBtn;
```

### 6.2 New Methods

| Method | Signature | CYC | Purpose |
|--------|-----------|-----|---------|
| `BuildLicenseRow` | `private void BuildLicenseRow(Panel parent)` | 1 | Adds Label+TextBox+Button+TextBlock to UI |
| `OnActivateClick` | `private void OnActivateClick(object sender, RoutedEventArgs e)` | 1 | Activation handler |
| `ApplyFeatureFlags` | `private void ApplyFeatureFlags(FeatureFlags f)` | 1 | Enable/disable per-rule buttons |
| `LoadLicenseKeyDisplay` | `private void LoadLicenseKeyDisplay()` | 2 | Read license.txt → populate _licenseKeyBox |
| `OnFeatureFlagsChanged` | `private void OnFeatureFlagsChanged(FeatureFlags f)` | 1 | Event subscriber — calls ApplyFeatureFlags |
| `GetStatusText` | `private static string GetStatusText(FeatureFlags f)` | 3 | Returns "ELITE"/"PRO"/"STARTER" label |

**BuildLicenseRow layout**: Row containing `Label("LICENSE")`, `TextBox _licenseKeyBox`,
`Button [Activate]`, `TextBlock _licenseStatusText`. All controls use existing style patterns
(no hex colors, no FontFamily, no Unicode).

**OnActivateClick flow** (CYC=1 — sequential):
1. `key = _licenseKeyBox.Text.Trim()`
2. `File.WriteAllText(licenseTxtPath, key)` in try/catch (inline or via helper)
3. `flags = LicenseClient.Validate(key)`
4. `CopyEngine.Instance.SetFlags(flags)` — fires event to Panel
5. `ApplyFeatureFlags(flags)` — this window's direct update
6. `_licenseStatusText.Text = GetStatusText(flags)`

**licenseTxtPath** (used in both OnActivateClick and LoadLicenseKeyDisplay):
```csharp
private static readonly string LicenseTxtPath = System.IO.Path.Combine(
    NinjaTrader.Core.Globals.UserDataDir, "PropTraderTools", "license.txt");
```
Declare as a `private static readonly string` field.

**ApplyFeatureFlags** (CYC=1): Enable/disable buttons based on flags. Uses `_flattenBtns`,
`_cancelBtns`, `_trimBtns`, `_beBtns` lists (already exist). Each button's `IsEnabled`
set from the corresponding flag. ToolTip set to upgrade message on disabled buttons.

**GetStatusText** (CYC=3):
```
if f.AtrSizing → "ELITE"   (branch 1)
if f.MultiRule → "PRO"     (branch 2)
return "STARTER"           (default)
```

**OnLoaded additions** (append to existing `OnLoaded` body):
```csharp
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
LoadLicenseKeyDisplay();
```

**Unsubscribe**: Add to `OnWindowClosed` handler:
```csharp
CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
```

---

## 7. TradeCopierPanel.cs Modifications

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

### 7.1 New Methods

| Method | Signature | CYC | Purpose |
|--------|-----------|-----|---------|
| `ApplyFeatureFlags` | `internal void ApplyFeatureFlags(FeatureFlags f)` | 1 | Visibility/enabled wiring for panel controls |
| `OnFeatureFlagsChanged` | `private void OnFeatureFlagsChanged(FeatureFlags f)` | 1 | Event subscriber |

**ApplyFeatureFlags** (CYC=1 — straight-line assignments):

```csharp
internal void ApplyFeatureFlags(FeatureFlags f)
{
    // f.TrimFlatten gates
    _trimBtn.IsEnabled    = f.TrimFlatten;
    _flattenBtn.IsEnabled = f.TrimFlatten;
    _cancelBtn.IsEnabled  = f.TrimFlatten;
    // f.BreakEven gates
    _beBtn.IsEnabled      = f.BreakEven;
    // f.MirrorMode gates
    _mirrorRadio.IsEnabled = f.MirrorMode;
    // f.MultiRule gates
    _addRuleBtn.IsEnabled = f.MultiRule;
    // f.ClickTrader visibility
    _clickTraderRow.Visibility = f.ClickTrader
        ? Visibility.Visible : Visibility.Collapsed;
    // f.AtrSizing visibility
    _atrRow.Visibility    = f.AtrSizing
        ? Visibility.Visible : Visibility.Collapsed;
    // f.QxGlobalExit visibility
    _qxBtn.Visibility     = f.QxGlobalExit
        ? Visibility.Visible : Visibility.Collapsed;
    // ToolTip on disabled buttons
    ApplyFeatureFlagTooltips(f);
}
```

Extract `ApplyFeatureFlagTooltips(FeatureFlags f)` to keep CYC clean (CYC=1 each).

**OnLoaded additions** (append to existing `OnLoaded` body in TradeCopierPanel):
```csharp
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
```

**Unsubscribe**: In `Detach()` method:
```csharp
CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
```

**Control name assumptions** (engineer must verify actual field names in TradeCopierPanel.cs):
- `_trimBtn` — Trim button
- `_flattenBtn` — Flatten button
- `_cancelBtn` — Cancel pending entries button
- `_beBtn` — Break Even button
- `_mirrorRadio` — Mirror mode radio/toggle
- `_addRuleBtn` — Add Rule button (the [Apply] button in panel row)
- `_clickTraderRow` — StackPanel/Grid row containing click trader controls
- `_atrRow` — StackPanel/Grid row containing ATR size controls
- `_qxBtn` — Global Quick Exit button

Engineer must read actual TradeCopierPanel.cs field names and substitute if different.

---

## 8. PttGlobalQuickExit.cs Modification

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

**Current Execute() CYC**: 7 (per L22-24 comment).
**After gate**: CYC = 8 (AT LIMIT — PASS).

Add as first line of `Execute()` body (before the Output.Process call at L38):

```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] Blocked: Global Quick Exit requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}
```

Note: uses Output.Process (not StatusUpdate) consistent with the rest of Execute().

---

## 9. PropTraderTools.csproj Modifications

**File**: `src/PropTraderTools/PropTraderTools.csproj`

1. Bump LangVersion: `<LangVersion>8.0</LangVersion>` → `<LangVersion>9.0</LangVersion>`
   Required for `sealed record` syntax in LicenseClient.cs.

2. Add Cryptolens SDK reference (see Section 3.10 for full context):
   ```xml
   <Reference Include="SKGL.Extension">
     <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
     <Private>false</Private>
   </Reference>
   ```
   AND for LSP convenience:
   ```xml
   <PackageReference Include="SKGL.Extension" Version="2.0.23" />
   ```

3. Add new compile entries:
   ```xml
   <Compile Include="LicenseClient.cs" />
   <Compile Include="Tests\BgtmTests.cs" />
   ```

---

## 10. Build Artifacts

### 10.1 scripts/build-release.ps1

PowerShell post-build script. Content per spec. Key tasks:
- Collect all PTT .cs files
- Run ConfuserEx over output DLL
- Produce obfuscated release artifact

### 10.2 confuserex.crproj

ConfuserEx project XML file at repo root. Per spec exact content.

---

## 11. Test Plan (Ticket 6)

**File**: `src/PropTraderTools/Tests/BgtmTests.cs`
**Namespace**: `PropTraderTools.Tests`
**Framework**: xUnit ([Fact] only — no NUnit/MSTest per JS testing mandate)

### 11.1 Test Cases

| Test Name | What It Asserts |
|-----------|----------------|
| `T_BGTM1_LicenseClient_NullKey` | `Validate(null)` returns `FeatureFlags.Starter()` (all false) |
| `T_BGTM1_LicenseClient_EmptyKey` | `Validate("")` returns `FeatureFlags.Starter()` |
| `T_BGTM1_LicenseClient_WhitespaceKey` | `Validate("  ")` returns `FeatureFlags.Starter()` |
| `T_BGTM1_LicenseClient_OfflineCache` | `TryReadCache` with valid unexpired cache file returns correct FeatureFlags |
| `T_BGTM1_LicenseClient_ExpiredCache` | `TryReadCache` with expired (ExpiresUtc < UtcNow) returns null |
| `T_BGTM1_LicenseClient_WrongKeyCache` | `TryReadCache` with mismatched key returns null |
| `T_BGTM1_LicenseClient_ValidKey_FromFeatureList` | `FromFeatureList(["multi_rule","trim_flatten","break_even"])` returns Pro-equivalent flags |
| `T_BGTM1_FeatureFlags_Starter_AllFalse` | `Starter()` has all 7 booleans = false |
| `T_BGTM1_FeatureFlags_Pro_CorrectBits` | `Pro()` has MultiRule/TrimFlatten/BreakEven=true, AtrSizing/ClickTrader/MirrorMode/QxGlobalExit=false |
| `T_BGTM1_FeatureFlags_Elite_AllTrue` | `Elite()` has all 7 booleans = true |

**Note on TryReadCache/TryRemoteValidate testing**: These are `private static` methods.
Test them via `Validate()` with a pre-seeded cache file (written to a temp path) or via
reflection. Prefer testing through the public `Validate()` surface to avoid reflection.

**Implementation note**: Tests that write/read cache files must use a temporary directory
and clean up in `IDisposable` or `[Fact]` teardown. Do not use `NinjaTrader.Core.Globals.UserDataDir`
in tests — inject path via a test-overridable static field `internal static string _testCachePath`.

---

## 12. Threading Model Summary

| Operation | Thread | Mechanism |
|-----------|--------|-----------|
| `LicenseClient.Validate()` at startup | UI thread (State.Configure) | Synchronous |
| `LicenseClient.Validate()` at activation | UI thread (OnActivateClick) | Synchronous |
| `CopyEngine.SetFlags()` | UI thread | Direct call — no Dispatcher needed |
| `FeatureFlagsChanged` event fire | UI thread | Direct invocation |
| Window/Panel event handlers | UI thread | Direct UI update — no Dispatcher.InvokeAsync needed |
| Gate checks in CopyEngine methods | UI thread (all engine calls are on UI thread) | Volatile read of _flags |
| `LicenseClient.TryRemoteValidate()` HTTP call | UI thread (BLOCKING) | Synchronous, 3s timeout |

**HTTP timeout note**: Engineer must configure SKM.NET to use a short timeout (max 3 seconds)
to avoid freezing the NT8 UI. Pass `timeout: 3` to the SKM.NET Activate call.

---

## 13. Data Flow Diagram

```
NT8 AddOn Load (State.Configure)
  └─ LoadAndValidateLicense()
       └─ LicenseClient.Validate(key)
            ├─ TryReadCache(key) ──── cache hit ──► FeatureFlags
            ├─ TryRemoteValidate(key) ─ hit ──────► FeatureFlags + WriteCache
            └─ fallback ─────────────────────────► FeatureFlags.Starter()
       └─ CopyEngine.Instance.SetFlags(flags)
            └─ _flags = flags
            └─ FeatureFlagsChanged?.Invoke(flags)
                 ├─ TradeCopierWindow.OnFeatureFlagsChanged ──► ApplyFeatureFlags()
                 └─ TradeCopierPanel.OnFeatureFlagsChanged ───► ApplyFeatureFlags()

User [Activate] click (TradeCopierWindow)
  └─ OnActivateClick()
       ├─ File.WriteAllText(license.txt, key)
       ├─ LicenseClient.Validate(key) ──► FeatureFlags
       ├─ CopyEngine.Instance.SetFlags(flags) ──► FeatureFlagsChanged (see above)
       ├─ ApplyFeatureFlags(flags) (direct window update)
       └─ _licenseStatusText.Text = GetStatusText(flags)

CopyEngine method call (gated)
  └─ AddRule/Trim/Flatten/BreakEven/... first-line check
       └─ !_flags.<Feature> ──► StatusUpdate(msg); return
```

---

## 14. Deferred Items NOT In Scope (Carry-Forward)

The following items from B107/06-deferred-backlog.md are explicitly NOT addressed in BGTM-1:

| Item | Status |
|------|--------|
| DW-B107 (MoveStopToBreakEven stale PTT-BE-Target-*) | Deferred to B108 |
| B107-DEFER-01 (F5 NT8 compilation gate) | Director-owned |
| B107-DEFER-02 (Combo C live re-test) | Director-owned |
| DW-B42-01/02/03 | Carry-forward |
| DW-PTT-BE-FIX-01/02/03 | Carry-forward |
| DW-B89-DEFERRED-01/02/03/04/05/06 | Carry-forward |

---

## 15. Ticket Grouping

| Ticket | File(s) | Spec Reqs | SCAN checklist |
|--------|---------|-----------|----------------|
| T1 | LicenseClient.cs, PropTraderTools.csproj | BGTM-1 deliverable 1 | SCAN-01..07 |
| T2 | CopyEngine.cs | BGTM-1 deliverable 2 | SCAN-01..07 |
| T3 | TradeCopierAddOn.cs | BGTM-1 deliverable 3 | SCAN-01..07 |
| T4 | TradeCopierWindow.cs | BGTM-1 deliverable 4 | SCAN-01..07 |
| T5 | TradeCopierPanel.cs | BGTM-1 deliverable 5 | SCAN-01..07 |
| T6 | Features/PttGlobalQuickExit.cs, Tests/BgtmTests.cs, scripts/build-release.ps1, confuserex.crproj | BGTM-1 deliverables 6/7/8 | SCAN-01..07 |

---

## 16. Pre-Flight Issues Found and Resolved

| Issue | Resolution |
|-------|------------|
| NT8 Roslyn rejects positional records (CS0518) | Add `IsExternalInit` shim in LicenseClient.cs; bump LangVersion to 9.0 in csproj |
| SKM.NET NuGet not available in NT8 Roslyn host | Add Reference HintPath to Custom folder + PackageReference for LSP |
| HTTP call blocks UI thread | Accept per spec (activation flow); engineer sets SKM.NET timeout to 3s max |
| JSON serialization (no Newtonsoft in NT8) | Use `System.Runtime.Serialization.Json.DataContractJsonSerializer` |
| State.Configure not in existing OnStateChange | Engineer adds `if (State == State.Configure)` block (standard NT8 pattern) |
| AddRule CYC+1 risk | Engineer checks current CYC before gate; extract inner helper if CYC=8 |
| PttGlobalQuickExit.Execute() CYC 7→8 | At limit (8) — PASS; no further extraction needed |

---

**PLAN_COMPLETE**
