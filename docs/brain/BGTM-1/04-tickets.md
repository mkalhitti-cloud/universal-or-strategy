# BGTM-1 Tickets

**Block**: BGTM-1 (License Gating + Feature Flags)
**Phase**: 3 (Ticket Generation)
**Source Plan**: docs/brain/BGTM-1/02-architecture-plan.md (REVIEW_PASS)
**Date**: 2026-08-26
**Architect**: ptt-architect

---

## Ticket 1 — LicenseClient.cs + FeatureFlags + csproj

**File(s)**:
- `src/PropTraderTools/LicenseClient.cs` (NEW — create file)
- `src/PropTraderTools/PropTraderTools.csproj` (MODIFY)
- `src/PropTraderTools/Tests/BgtmTests.cs` (NEW — stub; full tests written in T6)

**Spec Reqs**: BGTM-1 deliverable 1 (LicenseClient + FeatureFlags)

**Pre-conditions**:
- `src/PropTraderTools/PropTraderTools.csproj` must exist (already present)
- Engineer has extracted `SKGL.Extension.dll` (net46/net48 build) from NuGet and placed it in:
  `%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll`
- No prior `LicenseClient.cs` exists in `src/PropTraderTools/`

---

### Implementation Steps

1. **Create `src/PropTraderTools/LicenseClient.cs`** with the following structure in order:

   a. Add the `IsExternalInit` shim BEFORE the namespace declaration (required for NT8 Roslyn,
      CS0518 workaround identical to the existing pattern in `CopyEngine.cs`):
   ```csharp
   // NT8 Roslyn records shim -- CS0518 workaround (same pattern as FollowerAtmMode)
   namespace System.Runtime.CompilerServices
   {
       internal static class IsExternalInit { }
   }
   ```

   b. Open namespace `PropTraderTools` and declare the `FeatureFlags` sealed record:
   ```csharp
   namespace PropTraderTools
   {
       internal sealed record FeatureFlags(
           bool MultiRule,
           bool TrimFlatten,
           bool BreakEven,
           bool AtrSizing,
           bool ClickTrader,
           bool MirrorMode,
           bool QxGlobalExit)
       {
           public static FeatureFlags Starter() =>
               new(false, false, false, false, false, false, false);
           public static FeatureFlags Pro() =>
               new(true, true, true, false, false, false, false);
           public static FeatureFlags Elite() =>
               new(true, true, true, true, true, true, true);
           public static FeatureFlags FromFeatureList(
               System.Collections.Generic.IReadOnlyList<string> feats) =>
               new(
                   MultiRule:    feats.Contains("multi_rule"),
                   TrimFlatten:  feats.Contains("trim_flatten"),
                   BreakEven:    feats.Contains("break_even"),
                   AtrSizing:    feats.Contains("atr_sizing"),
                   ClickTrader:  feats.Contains("click_trader"),
                   MirrorMode:   feats.Contains("mirror_mode"),
                   QxGlobalExit: feats.Contains("qx_global_exit"));
       }
   ```

   c. Add the `CacheEntry` private DTO class (inside the `LicenseClient` class, not at namespace level):
   ```csharp
   [System.Runtime.Serialization.DataContract]
   private sealed class CacheEntry
   {
       [System.Runtime.Serialization.DataMember(Name = "key")]
       public string Key { get; set; }
       [System.Runtime.Serialization.DataMember(Name = "features")]
       public System.Collections.Generic.List<string> Features { get; set; }
       [System.Runtime.Serialization.DataMember(Name = "cached_utc")]
       public System.DateTime CachedUtc { get; set; }
       [System.Runtime.Serialization.DataMember(Name = "expires_utc")]
       public System.DateTime ExpiresUtc { get; set; }
   }
   ```

   d. Add `internal static string _testCachePath = null;` field immediately after the class opening
      brace (allows tests to override `CachePath` without reflection).

   e. Implement all methods listed in the Method Signatures section below in the order given.

2. **Modify `src/PropTraderTools/PropTraderTools.csproj`**:

   a. Bump `<LangVersion>` from `8.0` to `9.0`:
   ```xml
   <LangVersion>9.0</LangVersion>
   ```

   b. Add inside the first `<ItemGroup>` containing other `<Reference>` entries:
   ```xml
   <Reference Include="SKGL.Extension">
     <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
     <Private>false</Private>
   </Reference>
   ```

   c. Add inside a `<PackageReference>` group (for OmniSharp IntelliSense only):
   ```xml
   <PackageReference Include="SKGL.Extension" Version="2.0.23" />
   ```

   d. Add compile entries if the project uses explicit `<Compile Include=...>` entries. If it uses
      glob patterns (`<Compile Include="**\*.cs" />`), skip this step. Otherwise add:
   ```xml
   <Compile Include="LicenseClient.cs" />
   <Compile Include="Tests\BgtmTests.cs" />
   ```

3. **Create `src/PropTraderTools/Tests/BgtmTests.cs`** as an empty stub with correct using
   directives and namespace. Full test bodies are added in Ticket 6.

---

### Method Signatures

```csharp
// FeatureFlags (sealed record) -- in LicenseClient.cs, namespace PropTraderTools
internal sealed record FeatureFlags(
    bool MultiRule,
    bool TrimFlatten,
    bool BreakEven,
    bool AtrSizing,
    bool ClickTrader,
    bool MirrorMode,
    bool QxGlobalExit)
{
    public static FeatureFlags Starter()
    public static FeatureFlags Pro()
    public static FeatureFlags Elite()
    public static FeatureFlags FromFeatureList(IReadOnlyList<string> feats)
}

// LicenseClient -- static class, same file
internal static class LicenseClient
{
    internal static string _testCachePath = null;          // test injection hook

    private const string ProductId = "PTT_COPIER_V1";     // ASCII-only

    private static string CachePath { get; }               // => _testCachePath ?? Path.Combine(...)

    public static FeatureFlags Validate(string key)        // CYC=4. Never throws. Returns Starter on any failure.

    private static FeatureFlags? TryRemoteValidate(string key)   // CYC=3. Returns null on network failure.

    private static FeatureFlags? ParseSkmResponse(object lic)    // CYC=2. Extracts feature list from SKM LicenseKey.

    private static FeatureFlags? TryReadCache(string key)        // CYC=4. Returns null if missing/expired/wrong key.

    private static CacheEntry? DeserializeCache(string json)     // CYC=3. DataContractJsonSerializer. Returns null on error.

    private static void WriteCache(string key, FeatureFlags flags) // CYC=2. Swallows all exceptions.

    private static string InferTierName(FeatureFlags f)    // CYC=3. Returns "ELITE"/"PRO"/"STARTER".
}
```

**`CachePath` implementation**:
```csharp
private static string CachePath =>
    _testCachePath ?? System.IO.Path.Combine(
        NinjaTrader.Core.Globals.UserDataDir,
        "PropTraderTools",
        "license_cache.json");
```

**`Validate` method body (CYC=4)**:
```csharp
public static FeatureFlags Validate(string key)
{
    if (string.IsNullOrWhiteSpace(key))   // branch 1
        return FeatureFlags.Starter();
    var cached = TryReadCache(key);
    if (cached != null)                   // branch 2
        return cached;
    var remote = TryRemoteValidate(key);
    if (remote != null)                   // branch 3
    {
        WriteCache(key, remote);
        return remote;
    }
    return FeatureFlags.Starter();        // fallback
}
```

**`InferTierName` method body (CYC=3)**:
```csharp
private static string InferTierName(FeatureFlags f)
{
    if (f.AtrSizing) return "ELITE";      // branch 1
    if (f.MultiRule)  return "PRO";       // branch 2
    return "STARTER";
}
```

**Cache TTL**: `ExpiresUtc = DateTime.UtcNow.AddDays(7)` — 7-day offline TTL.

---

### JS Rules

| Rule | Constraint | Where Applied |
|------|-----------|---------------|
| **JS-001** (no throw in hot paths) | All `TryRemoteValidate`/`TryReadCache`/`WriteCache`/`Validate` wrap errors with `try/catch { return null; }` or `return Starter()`. No `throw` in any public method. | LicenseClient.cs |
| **JS-002** (no return null on public API) | `Validate()` (the only `public` method) NEVER returns null — returns `FeatureFlags` value type. `TryRemoteValidate`, `TryReadCache`, `DeserializeCache` return `FeatureFlags?` / `CacheEntry?` — these are `private`. | LicenseClient.cs |
| **JS-003** (sealed record) | `FeatureFlags` is `internal sealed record`. Immutable by construction. Value-equality semantics. | LicenseClient.cs |
| **JS-021** (no lock) | No `lock()` anywhere in LicenseClient.cs. | LicenseClient.cs |
| **JS-023** (volatile for shared ref) | Not applicable to LicenseClient.cs (stateless static class). Applies in T2 (CopyEngine). | CopyEngine.cs (T2) |

---

### xUnit Tests (stub registration only — full bodies in Ticket 6)

The following `[Fact]` names MUST be present in `src/PropTraderTools/Tests/BgtmTests.cs`:

```
[Fact] T_BGTM1_LicenseClient_NullKey_ReturnsStarter
[Fact] T_BGTM1_LicenseClient_EmptyKey_ReturnsStarter
[Fact] T_BGTM1_LicenseClient_WhitespaceKey_ReturnsStarter
[Fact] T_BGTM1_LicenseClient_OfflineCache_HitReturnsCachedFlags
[Fact] T_BGTM1_LicenseClient_OfflineCache_ExpiredReturnsStarter
[Fact] T_BGTM1_LicenseClient_WrongKeyCache_ReturnsStarter
[Fact] T_BGTM1_FeatureFlags_Starter_AllFalse
[Fact] T_BGTM1_FeatureFlags_Pro_MultiRuleTrimBreakEvenTrue
[Fact] T_BGTM1_FeatureFlags_Elite_AllTrue
[Fact] T_BGTM1_FeatureFlags_FromFeatureList_OnlyMultiRule
[Fact] T_BGTM1_LicenseClient_ValidKey_FromFeatureList
```

---

### 7-Scan Checklist

- [ ] **SCAN-01** `lock()` scan: `grep -rn "lock(" src/PropTraderTools/LicenseClient.cs` — must return 0 results
- [ ] **SCAN-02** `throw` scan: `grep -rn "throw new " src/PropTraderTools/LicenseClient.cs` — must return 0 results (no `throw` in any method)
- [ ] **SCAN-03** `return null` review: `grep -rn "return null" src/PropTraderTools/LicenseClient.cs` — permitted only in `private` methods (`TryRemoteValidate`, `TryReadCache`, `DeserializeCache`, `ParseSkmResponse`). Zero occurrences in `public static FeatureFlags Validate(...)`.
- [ ] **SCAN-04** CYC ≤ 8 audit:
  - `Validate` — CYC=4 (3 branches + base) ✓
  - `TryRemoteValidate` — CYC=3 (try/catch + null check) ✓
  - `ParseSkmResponse` — CYC=2 (null check + base) ✓
  - `TryReadCache` — CYC=4 (file-exists + try/catch + null check + expiry check) ✓
  - `DeserializeCache` — CYC=3 (try body + 2 catch branches) ✓
  - `WriteCache` — CYC=2 (try + catch) ✓
  - `InferTierName` — CYC=3 (2 if branches + base) ✓
  - `FeatureFlags.FromFeatureList` — CYC=1 (straight-line) ✓
  - `FeatureFlags.Starter/Pro/Elite` — CYC=1 each ✓
- [ ] **SCAN-05** ASCII-only: All string literals in LicenseClient.cs are ASCII-only. Check: `"PTT_COPIER_V1"`, `"PropTraderTools"`, `"license_cache.json"`, `"multi_rule"`, `"trim_flatten"`, `"break_even"`, `"atr_sizing"`, `"click_trader"`, `"mirror_mode"`, `"qx_global_exit"`, `"ELITE"`, `"PRO"`, `"STARTER"`. No Unicode, no curly quotes, no emoji.
- [ ] **SCAN-06** NT8 API: No banned NT8 patterns. Only `NinjaTrader.Core.Globals.UserDataDir` used (confirmed safe for State.Configure and AddOn context). No `AtmStrategyCreate()`. No `CreateOrder()`.
- [ ] **SCAN-07** Sealed record: `FeatureFlags` declared as `internal sealed record` — verify `sealed` and `record` keywords both present on same line.

**Completion artifact**: `docs/brain/BGTM-1/ticket-1-completion.md`

---

## Ticket 2 — CopyEngine.cs Gate Additions

**File(s)**:
- `src/PropTraderTools/CopyEngine.cs` (MODIFY)

**Spec Reqs**: BGTM-1 deliverable 2 (feature-flag gate guards on CopyEngine methods)

**Pre-conditions**:
- Ticket 1 COMPLETED — `FeatureFlags` type must be resolvable (LicenseClient.cs compiled)
- `CopyEngine.cs` compiles cleanly on current branch before any edits
- Engineer has verified current CYC on each gated method (see SCAN-04 note)

---

### Implementation Steps

1. **Locate the field block** around `_cloneAtmObject` (approximately L151). Add the four new
   members immediately after the last field in that block and before `_globalBe`:

```csharp
// BGTM-1: Feature flags -- volatile reference (atomic on CLR 4.0+, JS-023 compliant).
// SetFlags called from UI thread only. Read from UI thread only.
private volatile FeatureFlags _flags = FeatureFlags.Starter();

/// <summary>Current feature flags snapshot.</summary>
public FeatureFlags Flags => _flags;

/// <summary>Fires on UI thread when license activation changes flags.</summary>
public event Action<FeatureFlags> FeatureFlagsChanged;

// BGTM-1: Assign flags and broadcast. CYC=1. JS-021: no lock.
internal void SetFlags(FeatureFlags f)
{
    _flags = f;
    FeatureFlagsChanged?.Invoke(f);
}
```

2. **Add gate guards** — first line of each method body listed in the Gate Table below.
   Before touching any method: record the method's current CYC comment (look for `// CYC=N`
   or equivalent). If CYC is already at 8, extract an inner helper (e.g., `DoAddRuleCore(...)`)
   to create room for the +1 branch, then add the gate.

3. **Gate table** — for each row, add the guard as the ABSOLUTE FIRST EXECUTABLE LINE
   of the method body (after any opening braces):

| Method | Approximate line | Guard expression | Action |
|--------|-----------------|-----------------|--------|
| `AddRule(string instr, Account master, Account[] followers)` | L1097 | `!_flags.MultiRule && _rules.Count >= 1` | `StatusUpdate("Multi-rule requires Pro. Upgrade at proptradertools.com/pricing"); return;` |
| `AddRule(string, Account, Account[], int[], ...)` (full overload) | L1106 | `!_flags.MultiRule && _rules.Count >= 1` | `StatusUpdate("Multi-rule requires Pro. Upgrade at proptradertools.com/pricing"); return;` |
| `Trim(Instrument instrument)` | L2785 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `Trim(Account leader, Instrument instrument)` | L2799 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `Trim(Instrument instr, int exitBuffer, double ask, double bid)` | L3058 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `Flatten(Instrument instrument)` | L2791 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `Flatten(Account leader, Instrument instrument)` | L2817 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `Flatten(Instrument instr, int exitBuffer, double ask, double bid)` | L3072 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `CancelPendingEntries(Account leader, Instrument instrument)` | L2835 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `CancelPendingEntries(Instrument instrument)` | L3083 | `!_flags.TrimFlatten` | `StatusUpdate("Trim/Flatten requires Pro tier"); return;` |
| `BreakEven(Instrument instrument, int bufferTicks)` | L3684 | `!_flags.BreakEven` | `StatusUpdate("Break Even requires Pro tier"); return;` |
| `BreakEven(Account leader, Instrument instrument, int bufferTicks)` | L3698 | `!_flags.BreakEven` | `StatusUpdate("Break Even requires Pro tier"); return;` |
| `ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)` | L3932 | `!_flags.BreakEven` | `StatusUpdate("Break Even requires Pro tier"); return;` |
| `SetCopyMode(CopyMode mode)` | L548 | `!_flags.MirrorMode && mode == CopyMode.Mirror` | `StatusUpdate("Mirror mode requires Elite tier"); return;` |
| `SetAtrEngine(AtrSizingEngine engine, bool enabled)` | L525 | `!_flags.AtrSizing && enabled` | `enabled = false;` (NO return — allow fallback to disabled state, NO StatusUpdate) |
| `GetSuggestedQty(Instrument instrument)` | L1066 | `!_flags.AtrSizing` | `return 1;` (NO StatusUpdate needed — silent fallback) |

4. **Standard gate pattern** (copy-paste template, adjust flag and message per row):
```csharp
if (!_flags.MultiRule && _rules.Count >= 1)
{
    StatusUpdate("Multi-rule requires Pro. Upgrade at proptradertools.com/pricing");
    return;
}
```

---

### Method Signatures (new members added to CopyEngine)

```csharp
// Field
private volatile FeatureFlags _flags = FeatureFlags.Starter();

// Property
public FeatureFlags Flags => _flags;

// Event
public event Action<FeatureFlags> FeatureFlagsChanged;

// Method
internal void SetFlags(FeatureFlags f)   // CYC=1. JS-021 compliant (no lock).
```

---

### JS Rules

| Rule | Constraint | Where Applied |
|------|-----------|---------------|
| **JS-021** (no lock) | `SetFlags` uses `volatile` write + direct event invocation. No `lock()`. | CopyEngine.cs |
| **JS-023** (volatile for shared mutable ref) | `private volatile FeatureFlags _flags` — CLR 4.0 guarantees atomic reference reads/writes. | CopyEngine.cs |
| **JS-001** (no throw) | Gate guards only call `StatusUpdate(...)` and `return`. No exceptions thrown. | All gated methods |
| **CYC ≤ 8** | Each gated method gains +1 branch. Verify pre-gate CYC ≤ 7. If CYC=8, extract inner helper first. `SetFlags` CYC=1. | All gated methods |

---

### xUnit Tests

No new xUnit `[Fact]` methods are required for Ticket 2. Gate behaviour is validated indirectly
through integration tests in `BgtmTests.cs` (Ticket 6). Engineer may add focused unit tests if
desired; names must follow the `T_BGTM1_CopyEngine_*` prefix convention.

---

### 7-Scan Checklist

- [ ] **SCAN-01** `lock()` scan: `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` — must return 0 results in all new or modified code blocks
- [ ] **SCAN-02** `throw` scan: `grep -rn "throw new " src/PropTraderTools/CopyEngine.cs` — must return 0 results in any new code added by this ticket
- [ ] **SCAN-03** `return null` review: `grep -rn "return null" src/PropTraderTools/CopyEngine.cs` — no new `return null` introduced by this ticket (existing occurrences may remain if pre-existing)
- [ ] **SCAN-04** CYC ≤ 8 audit — for every method touched:
  - `SetFlags` — CYC=1 ✓
  - `Flags` (property getter) — CYC=1 ✓
  - Each gated method: confirm pre-gate CYC ≤ 7 (gate adds +1). If any method was already at CYC=8, document the extraction performed before gating.
  - `GetSuggestedQty` gate is an early return; confirm final CYC ≤ 8.
  - `SetAtrEngine` gate is an assignment; confirm final CYC ≤ 8.
- [ ] **SCAN-05** ASCII-only: All gate StatusUpdate messages contain only ASCII characters. Confirm: `"Multi-rule requires Pro. Upgrade at proptradertools.com/pricing"`, `"Trim/Flatten requires Pro tier"`, `"Break Even requires Pro tier"`, `"Mirror mode requires Elite tier"`, `"ATR sizing requires Elite tier"` — no Unicode, no curly quotes.
- [ ] **SCAN-06** NT8 API: No banned NT8 patterns in new code. `StatusUpdate(...)` is the existing PTT helper (not an NT8 API call). `CopyMode.Mirror` uses the existing enum (no new NT8 API surface introduced).
- [ ] **SCAN-07** Sealed record: `FeatureFlags` type (from T1) is referenced only via its public properties (`_flags.MultiRule`, etc.). No `new FeatureFlags(...)` constructor calls in CopyEngine.cs — all construction is via the static factory methods in LicenseClient.cs.

**Completion artifact**: `docs/brain/BGTM-1/ticket-2-completion.md`

---

## Ticket 3 — TradeCopierAddOn.cs License Initialization

**File(s)**:
- `src/PropTraderTools/TradeCopierAddOn.cs` (MODIFY)

**Spec Reqs**: BGTM-1 deliverable 3 (AddOn startup license load + ClickTrader gate)

**Pre-conditions**:
- Tickets 1 and 2 COMPLETED — `LicenseClient`, `FeatureFlags`, and `CopyEngine.SetFlags()` must all compile
- Engineer has confirmed the `OnStateChange()` method exists in `TradeCopierAddOn.cs` with a `State.SetDefaults` block (standard NT8 AddOn pattern)
- Engineer has confirmed `RegisterClickTrader(Chart chart, TradeCopierPanel panel)` method exists

---

### Implementation Steps

1. **Add `LoadAndValidateLicense()` helper method** inside `TradeCopierAddOn`. Place after the last
   existing private helper method and before any overrides:

```csharp
// BGTM-1: Read license.txt, validate via LicenseClient. CYC=2.
// JS-001: no throw -- any I/O error returns Starter().
// NT8: File.ReadAllText is safe in State.Configure (not the hot path).
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

2. **Add the `State.Configure` block** inside `OnStateChange()`. Locate the existing
   `if (State == State.SetDefaults)` block. Add the following block AFTER it:

```csharp
if (State == State.Configure)
{
    var flags = LoadAndValidateLicense();
    CopyEngine.Instance.SetFlags(flags);
}
```

3. **Add the `RegisterClickTrader` gate**. Locate `RegisterClickTrader(Chart chart, TradeCopierPanel panel)`
   and prepend the following as the absolute first executable statement of the method body:

```csharp
if (!CopyEngine.Instance.Flags.ClickTrader)
{
    StatusUpdate("Click Trader requires Elite tier");
    return;
}
```

---

### Method Signatures

```csharp
// New private helper added to TradeCopierAddOn
private static FeatureFlags LoadAndValidateLicense()   // CYC=2

// RegisterClickTrader gate (no signature change — guard prepended to existing method):
// private void RegisterClickTrader(Chart chart, TradeCopierPanel panel)  CYC: was N, now N+1
```

---

### JS Rules

| Rule | Constraint | Where Applied |
|------|-----------|---------------|
| **JS-001** (no throw) | `LoadAndValidateLicense()` wraps the entire body in `try/catch(Exception)` returning `Starter()`. No exceptions escape. | TradeCopierAddOn.cs |
| **JS-021** (no lock) | No new synchronization primitives. `SetFlags()` called from UI thread (State.Configure executes on NT8 UI thread). | TradeCopierAddOn.cs |
| **CYC ≤ 8** | `LoadAndValidateLicense` CYC=2 (try/catch = 1 branch + base). `RegisterClickTrader` CYC was N; after gate CYC = N+1. Verify N ≤ 7 before editing. | TradeCopierAddOn.cs |
| **DateTime.UtcNow** | No `DateTime.Now` usage. Any date references in new code use `DateTime.UtcNow`. | TradeCopierAddOn.cs |

---

### xUnit Tests

No new xUnit `[Fact]` methods required for Ticket 3. License loading integration is tested via
`BgtmTests.cs` tests that exercise `LicenseClient.Validate()` directly. The AddOn wiring is
verified by the 7-scan checklist and NT8 compile gate.

---

### 7-Scan Checklist

- [ ] **SCAN-01** `lock()` scan: `grep -rn "lock(" src/PropTraderTools/TradeCopierAddOn.cs` — must return 0 results in new code blocks added by this ticket
- [ ] **SCAN-02** `throw` scan: `grep -rn "throw new " src/PropTraderTools/TradeCopierAddOn.cs` — must return 0 results in `LoadAndValidateLicense()` and in the new `State.Configure` block
- [ ] **SCAN-03** `return null` review: `grep -rn "return null" src/PropTraderTools/TradeCopierAddOn.cs` — `LoadAndValidateLicense()` returns `FeatureFlags` (never null). Zero new `return null` in code added by this ticket.
- [ ] **SCAN-04** CYC ≤ 8 audit:
  - `LoadAndValidateLicense` — CYC=2 (try body + catch) ✓
  - `RegisterClickTrader` — was CYC=2 per architecture plan; after gate = CYC=3 ✓
  - `OnStateChange` — verify adding the `State.Configure` block does not push `OnStateChange` above CYC=8. If it does, extract a helper.
- [ ] **SCAN-05** ASCII-only: `"Click Trader requires Elite tier"` — ASCII only. `"PropTraderTools"` — ASCII only. `"license.txt"` — ASCII only.
- [ ] **SCAN-06** NT8 API: Only `NinjaTrader.Core.Globals.UserDataDir` and `System.IO.*` used in new code. `State.Configure` is the standard NT8 AddOn lifecycle state. No `AtmStrategyCreate()` or `CreateOrder()` in new code.
- [ ] **SCAN-07** Sealed record: `FeatureFlags` value returned by `LoadAndValidateLicense()` is the sealed record from T1. Method signature is `private static FeatureFlags` — not nullable, confirms JS-002 compliance at this call site.

**Completion artifact**: `docs/brain/BGTM-1/ticket-3-completion.md`

---

## Ticket 4 — TradeCopierWindow.cs License UI

**File(s)**:
- `src/PropTraderTools/TradeCopierWindow.cs` (MODIFY)

**Spec Reqs**: BGTM-1 deliverable 4 (license key input row, activate button, per-feature UI wiring)

**Pre-conditions**:
- Tickets 1, 2, and 3 COMPLETED
- Engineer has read `TradeCopierWindow.cs` and identified:
  - The existing `OnLoaded` event handler method
  - The existing `OnWindowClosed` (or `Closed`) handler method
  - The existing button-list fields (`_flattenBtns`, `_trimBtns`, `_beBtns`, `_cancelBtns`) — verify exact field names
  - An existing `Panel` control (StackPanel or Grid) to which `BuildLicenseRow` will add the new row

---

### Implementation Steps

1. **Add three new fields** in the fields region of `TradeCopierWindow` (after last existing field):

```csharp
// BGTM-1: License UI controls
private System.Windows.Controls.TextBox _licenseKeyBox;
private System.Windows.Controls.TextBlock _licenseStatusText;
private System.Windows.Controls.Button _activateBtn;

private static readonly string LicenseTxtPath = System.IO.Path.Combine(
    NinjaTrader.Core.Globals.UserDataDir,
    "PropTraderTools",
    "license.txt");
```

2. **Add `BuildLicenseRow(Panel parent)` method** — creates WPF controls and appends them
   to `parent`. Uses existing style/resource patterns from the window (no hex colors, no
   `FontFamily`, no Unicode). Minimum layout: `Label("LICENSE")` + `_licenseKeyBox` +
   `_activateBtn` (content: `"Activate"`) + `_licenseStatusText`. Wire `_activateBtn.Click`
   to `OnActivateClick`.

3. **Add `OnActivateClick` event handler** with the body described in the architecture plan
   (CYC=1 — sequential steps, no branching):
   ```
   1. key = _licenseKeyBox.Text.Trim()
   2. try { Directory.CreateDirectory(Path.GetDirectoryName(LicenseTxtPath)); File.WriteAllText(LicenseTxtPath, key); } catch(Exception) { /* benign */ }
   3. flags = LicenseClient.Validate(key)
   4. CopyEngine.Instance.SetFlags(flags)
   5. ApplyFeatureFlags(flags)
   6. _licenseStatusText.Text = GetStatusText(flags)
   ```

4. **Add `ApplyFeatureFlags(FeatureFlags f)` method** (CYC=1 — straight-line assignments).
   Enables/disables the existing button-list fields based on flags. Use the existing list
   fields (e.g., `_flattenBtns.ForEach(b => b.IsEnabled = f.TrimFlatten)` — adapt to actual
   field type; if `List<Button>`, use `foreach`). Also set `ToolTip` on disabled buttons to
   appropriate upgrade message. Set `_activateBtn` state last (it is always enabled).

5. **Add `GetStatusText(FeatureFlags f)` static method** (CYC=3):
```csharp
private static string GetStatusText(FeatureFlags f)
{
    if (f.AtrSizing) return "ELITE";    // branch 1
    if (f.MultiRule)  return "PRO";     // branch 2
    return "STARTER";
}
```

6. **Add `LoadLicenseKeyDisplay()` method** (CYC=2):
```csharp
private void LoadLicenseKeyDisplay()
{
    try
    {
        _licenseKeyBox.Text = System.IO.File.Exists(LicenseTxtPath)
            ? System.IO.File.ReadAllText(LicenseTxtPath).Trim()
            : string.Empty;
    }
    catch (Exception)
    {
        _licenseKeyBox.Text = string.Empty;
    }
}
```

7. **Add `OnFeatureFlagsChanged(FeatureFlags f)` handler** (CYC=1):
```csharp
private void OnFeatureFlagsChanged(FeatureFlags f)
{
    ApplyFeatureFlags(f);
    _licenseStatusText.Text = GetStatusText(f);
}
```

8. **Append to `OnLoaded` body**:
```csharp
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
LoadLicenseKeyDisplay();
```

9. **Append to `OnWindowClosed` (or `Closed`) handler body**:
```csharp
CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
```

10. **Call `BuildLicenseRow(parent)`** from within the window's build/init method (identify
    the method that constructs the main panel — typically called from `OnInitialized` or the
    constructor). Pass the outermost `StackPanel` or `Grid` as `parent`.

---

### Method Signatures

```csharp
private void BuildLicenseRow(System.Windows.Controls.Panel parent)   // CYC=1
private void OnActivateClick(object sender, System.Windows.RoutedEventArgs e)  // CYC=1
private void ApplyFeatureFlags(FeatureFlags f)   // CYC=1
private void LoadLicenseKeyDisplay()             // CYC=2
private void OnFeatureFlagsChanged(FeatureFlags f)  // CYC=1
private static string GetStatusText(FeatureFlags f) // CYC=3
```

---

### JS Rules

| Rule | Constraint | Where Applied |
|------|-----------|---------------|
| **JS-001** (no throw) | `OnActivateClick` and `LoadLicenseKeyDisplay` wrap I/O in `try/catch`. No exceptions escape to WPF dispatcher. | TradeCopierWindow.cs |
| **JS-021** (no lock) | Event subscription/unsubscription on UI thread. No lock. | TradeCopierWindow.cs |
| **CYC ≤ 8** | All 6 new methods comply. `OnLoaded` gains 3 lines but no new branches. Verify `OnLoaded` CYC ≤ 8 after additions. | TradeCopierWindow.cs |
| **No hex colors** | `BuildLicenseRow` must not use hardcoded hex color strings or `Brush` hex literals. Use existing `MakeWinBrush(r,g,b)` helper or resource references. | TradeCopierWindow.cs |
| **No FontFamily** | `BuildLicenseRow` must not set `FontFamily` on any control. | TradeCopierWindow.cs |
| **DateTime.UtcNow** | No `DateTime.Now` in new code. | TradeCopierWindow.cs |

---

### xUnit Tests

No new xUnit `[Fact]` methods required for Ticket 4. WPF controls require a UI dispatcher to
instantiate and are out of scope for the xUnit test suite. Visual verification is via NT8 F5
compile + manual UI inspection.

---

### 7-Scan Checklist

- [ ] **SCAN-01** `lock()` scan: `grep -rn "lock(" src/PropTraderTools/TradeCopierWindow.cs` — must return 0 results in all new code added by this ticket
- [ ] **SCAN-02** `throw` scan: `grep -rn "throw new " src/PropTraderTools/TradeCopierWindow.cs` — must return 0 results in `OnActivateClick`, `LoadLicenseKeyDisplay`, `BuildLicenseRow`, `ApplyFeatureFlags`, `OnFeatureFlagsChanged`, `GetStatusText`
- [ ] **SCAN-03** `return null` review: `grep -rn "return null" src/PropTraderTools/TradeCopierWindow.cs` — `GetStatusText` returns `string` (never null). Zero new `return null` in code added by this ticket.
- [ ] **SCAN-04** CYC ≤ 8 audit:
  - `BuildLicenseRow` — CYC=1 ✓
  - `OnActivateClick` — CYC=1 (sequential, no branches) ✓
  - `ApplyFeatureFlags` — CYC=1 (straight-line assignments) ✓
  - `LoadLicenseKeyDisplay` — CYC=2 (try/catch) ✓
  - `OnFeatureFlagsChanged` — CYC=1 ✓
  - `GetStatusText` — CYC=3 (2 if branches + base) ✓
  - `OnLoaded` — verify existing CYC + 0 new branches ≤ 8 ✓
- [ ] **SCAN-05** ASCII-only: Status strings `"ELITE"`, `"PRO"`, `"STARTER"` — ASCII only. Button content `"Activate"` — ASCII only. Label text `"LICENSE"` — ASCII only. No Unicode in any new string literal.
- [ ] **SCAN-06** NT8 API: Only `NinjaTrader.Core.Globals.UserDataDir` in new code (via `LicenseTxtPath`). No `AtmStrategyCreate()`. No `CreateOrder()`. No banned NT8 patterns.
- [ ] **SCAN-07** Sealed record: `FeatureFlags f` parameter in `ApplyFeatureFlags`, `OnFeatureFlagsChanged`, `GetStatusText` — used as value type; no mutation. Confirms immutability contract of sealed record.

**Completion artifact**: `docs/brain/BGTM-1/ticket-4-completion.md`

---

## Ticket 5 — TradeCopierPanel.cs Feature-Flag Wiring

**File(s)**:
- `src/PropTraderTools/TradeCopierPanel.cs` (MODIFY)

**Spec Reqs**: BGTM-1 deliverable 5 (panel control visibility/enabled wiring via ApplyFeatureFlags)

**Pre-conditions**:
- Tickets 1 and 2 COMPLETED
- Engineer has READ `src/PropTraderTools/TradeCopierPanel.cs` in full before writing any code
- Engineer has recorded the ACTUAL field names for all 9 UI control references listed below
  (the names in this ticket are the planned names from the architecture plan; substitute
  actual names if they differ — do NOT use assumed names without verifying)
- Engineer has identified the `OnLoaded` handler and the `Detach()` method (or equivalent
  unload/detach lifecycle method) in `TradeCopierPanel.cs`

---

### Implementation Steps

1. **Read the file first**. Before editing, use `read_file` to scan the full field declaration
   block of `TradeCopierPanel.cs`. Identify actual field names corresponding to:

   | Planned name | Purpose | Actual field name (engineer fills in) |
   |-------------|---------|--------------------------------------|
   | `_trimBtn` | Trim button | ___________________ |
   | `_flattenBtn` | Flatten button | ___________________ |
   | `_cancelBtn` | Cancel pending entries button | ___________________ |
   | `_beBtn` | Break Even button | ___________________ |
   | `_mirrorRadio` | Mirror mode radio/toggle | ___________________ |
   | `_addRuleBtn` | Add Rule / Apply button | ___________________ |
   | `_clickTraderRow` | StackPanel/Grid row for Click Trader controls | ___________________ |
   | `_atrRow` | StackPanel/Grid row for ATR size controls | ___________________ |
   | `_qxBtn` | Global Quick Exit button | ___________________ |

2. **Add `ApplyFeatureFlagTooltips(FeatureFlags f)` helper method** (CYC=1 — straight-line
   assignments). Sets `ToolTip` on buttons that are disabled:

```csharp
private void ApplyFeatureFlagTooltips(FeatureFlags f)
{
    // [actual_trim_btn].ToolTip     = f.TrimFlatten ? null : "Trim/Flatten requires Pro tier";
    // [actual_flatten_btn].ToolTip  = f.TrimFlatten ? null : "Trim/Flatten requires Pro tier";
    // [actual_cancel_btn].ToolTip   = f.TrimFlatten ? null : "Trim/Flatten requires Pro tier";
    // [actual_be_btn].ToolTip       = f.BreakEven   ? null : "Break Even requires Pro tier";
    // [actual_mirror_radio].ToolTip = f.MirrorMode  ? null : "Mirror mode requires Elite tier";
    // [actual_add_rule_btn].ToolTip = f.MultiRule   ? null : "Multi-rule requires Pro tier";
}
```
   Replace `[actual_*]` with verified field names.

3. **Add `ApplyFeatureFlags(FeatureFlags f)` method** (CYC=1 — straight-line assignments):

```csharp
internal void ApplyFeatureFlags(FeatureFlags f)
{
    [actual_trim_btn].IsEnabled     = f.TrimFlatten;
    [actual_flatten_btn].IsEnabled  = f.TrimFlatten;
    [actual_cancel_btn].IsEnabled   = f.TrimFlatten;
    [actual_be_btn].IsEnabled       = f.BreakEven;
    [actual_mirror_radio].IsEnabled = f.MirrorMode;
    [actual_add_rule_btn].IsEnabled = f.MultiRule;
    [actual_click_trader_row].Visibility = f.ClickTrader
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;
    [actual_atr_row].Visibility     = f.AtrSizing
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;
    [actual_qx_btn].Visibility      = f.QxGlobalExit
        ? System.Windows.Visibility.Visible
        : System.Windows.Visibility.Collapsed;
    ApplyFeatureFlagTooltips(f);
}
```

4. **Add `OnFeatureFlagsChanged(FeatureFlags f)` method** (CYC=1):

```csharp
private void OnFeatureFlagsChanged(FeatureFlags f)
{
    ApplyFeatureFlags(f);
}
```

   NOTE: All `CopyEngine.FeatureFlagsChanged` events fire on the UI thread (per threading model
   in architecture plan Section 12). No `Dispatcher.InvokeAsync` is needed.

5. **Append to `OnLoaded` body**:

```csharp
CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged;
ApplyFeatureFlags(CopyEngine.Instance.Flags);
```

6. **Append to `Detach()` body** (or equivalent panel unload handler — verify the exact name):

```csharp
CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged;
```

---

### Method Signatures

```csharp
internal void ApplyFeatureFlags(FeatureFlags f)          // CYC=1 (9 assignments + 1 helper call)
private void ApplyFeatureFlagTooltips(FeatureFlags f)    // CYC=1 (straight-line tooltip assignments)
private void OnFeatureFlagsChanged(FeatureFlags f)       // CYC=1
```

---

### JS Rules

| Rule | Constraint | Where Applied |
|------|-----------|---------------|
| **JS-021** (no lock) | Event subscription on UI thread. No lock. | TradeCopierPanel.cs |
| **CYC ≤ 8** | `ApplyFeatureFlags` CYC=1 (no branches — ternary `? :` for Visibility does not add cyclomatic complexity). `OnFeatureFlagsChanged` CYC=1. `ApplyFeatureFlagTooltips` CYC=1. | TradeCopierPanel.cs |
| **No hex colors** | `ApplyFeatureFlags` uses `Visibility.Visible`/`Visibility.Collapsed` enum values only. No hex brush literals. | TradeCopierPanel.cs |
| **JS-001** (no throw) | No new exception-throwing code. `ApplyFeatureFlags` is pure assignment. | TradeCopierPanel.cs |

---

### xUnit Tests

No new xUnit `[Fact]` methods required for Ticket 5. Panel control wiring requires a WPF
dispatcher and is verified by the NT8 F5 compile gate and manual UI inspection.

---

### 7-Scan Checklist

- [ ] **SCAN-01** `lock()` scan: `grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs` — must return 0 results in all new code added by this ticket
- [ ] **SCAN-02** `throw` scan: `grep -rn "throw new " src/PropTraderTools/TradeCopierPanel.cs` — must return 0 results in `ApplyFeatureFlags`, `ApplyFeatureFlagTooltips`, `OnFeatureFlagsChanged`
- [ ] **SCAN-03** `return null` review: `grep -rn "return null" src/PropTraderTools/TradeCopierPanel.cs` — zero new `return null` added by this ticket (all methods are void)
- [ ] **SCAN-04** CYC ≤ 8 audit:
  - `ApplyFeatureFlags` — CYC=1 (ternary operators do not increase cyclomatic complexity) ✓
  - `ApplyFeatureFlagTooltips` — CYC=1 ✓
  - `OnFeatureFlagsChanged` — CYC=1 ✓
  - `OnLoaded` — verify 0 new branches added ≤ existing CYC ≤ 8 ✓
  - `Detach` — verify 0 new branches added ≤ existing CYC ≤ 8 ✓
- [ ] **SCAN-05** ASCII-only: Tooltip strings `"Trim/Flatten requires Pro tier"`, `"Break Even requires Pro tier"`, `"Mirror mode requires Elite tier"`, `"Multi-rule requires Pro tier"` — ASCII only. No Unicode.
- [ ] **SCAN-06** NT8 API: No banned NT8 patterns in new code. `Visibility.Visible`/`Visibility.Collapsed` are WPF enum values (not NT8-specific). No `AtmStrategyCreate()`. No `CreateOrder()`.
- [ ] **SCAN-07** Sealed record: `FeatureFlags f` used as immutable value parameter. `ApplyFeatureFlags` reads only boolean properties (`f.TrimFlatten`, `f.BreakEven`, etc.) — no mutation. Confirms sealed record immutability contract is respected.

**Completion artifact**: `docs/brain/BGTM-1/ticket-5-completion.md`

---

## Ticket 6 — PttGlobalQuickExit Gate + Build Artifacts + xUnit Tests

**File(s)**:
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (MODIFY)
- `src/PropTraderTools/Tests/BgtmTests.cs` (NEW — full xUnit test class)
- `scripts/build-release.ps1` (NEW)
- `confuserex.crproj` (NEW — repo root)

**Spec Reqs**: BGTM-1 deliverables 6 (QxGlobalExit gate), 7 (xUnit tests), 8 (build release script + ConfuserEx config)

**Pre-conditions**:
- Tickets 1, 2, 3, 4, and 5 COMPLETED
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` exists and `Execute()` method is present with CYC=7 per architecture plan
- `src/PropTraderTools/Tests/BgtmTests.cs` stub created in Ticket 1

---

### Implementation Steps

#### Part A — PttGlobalQuickExit.cs Gate

1. Read `src/PropTraderTools/Features/PttGlobalQuickExit.cs`. Locate `Execute()`. Confirm its
   current CYC is 7 (per architecture plan Section 8).

2. Add the following as the **absolute first executable statement** of `Execute()` body (before
   the `NinjaTrader.Code.Output.Process(...)` call at L38):

```csharp
if (!CopyEngine.Instance.Flags.QxGlobalExit)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] Blocked: Global Quick Exit requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}
```

   After this gate, `Execute()` CYC = 8 (AT LIMIT — PASS per architecture plan Section 8).

#### Part B — BgtmTests.cs (full test class)

Create `src/PropTraderTools/Tests/BgtmTests.cs` with the following structure:

```csharp
// xUnit-only (JS testing mandate). No NUnit, no MSTest.
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using PropTraderTools;

namespace PropTraderTools.Tests
{
    public sealed class BgtmTests : IDisposable
    {
        private readonly string _tempDir;

        public BgtmTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "BgtmTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            // Redirect LicenseClient cache to temp dir so tests do not touch production paths
            LicenseClient._testCachePath = Path.Combine(_tempDir, "license_cache.json");
        }

        public void Dispose()
        {
            LicenseClient._testCachePath = null;
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_NullKey_ReturnsStarter()
        {
            var f = LicenseClient.Validate(null);
            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_EmptyKey_ReturnsStarter()
        {
            var f = LicenseClient.Validate(string.Empty);
            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_OfflineCache_HitReturnsCachedFlags()
        {
            // Write a valid unexpired cache entry for key "TEST-PRO"
            var cacheJson = BuildCacheJson("TEST-PRO",
                new[] { "multi_rule", "trim_flatten", "break_even" },
                DateTime.UtcNow.AddDays(7));
            File.WriteAllText(LicenseClient._testCachePath, cacheJson);

            var f = LicenseClient.Validate("TEST-PRO");

            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.False(f.AtrSizing);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_OfflineCache_ExpiredReturnsStarter()
        {
            // Write an expired cache entry (ExpiresUtc in the past)
            var cacheJson = BuildCacheJson("TEST-PRO",
                new[] { "multi_rule", "trim_flatten", "break_even" },
                DateTime.UtcNow.AddDays(-1));
            File.WriteAllText(LicenseClient._testCachePath, cacheJson);

            // No network in test; TryRemoteValidate will fail => fallback to Starter
            var f = LicenseClient.Validate("TEST-PRO");

            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_Starter_AllFalse()
        {
            var f = FeatureFlags.Starter();
            Assert.False(f.MultiRule);
            Assert.False(f.TrimFlatten);
            Assert.False(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_Pro_MultiRuleTrimBreakEvenTrue()
        {
            var f = FeatureFlags.Pro();
            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_Elite_AllTrue()
        {
            var f = FeatureFlags.Elite();
            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.True(f.AtrSizing);
            Assert.True(f.ClickTrader);
            Assert.True(f.MirrorMode);
            Assert.True(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_FeatureFlags_FromFeatureList_OnlyMultiRule()
        {
            var f = FeatureFlags.FromFeatureList(new[] { "multi_rule" });
            Assert.True(f.MultiRule);
            Assert.False(f.TrimFlatten);
            Assert.False(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_WhitespaceKey_ReturnsStarter()
        {
            var f = LicenseClient.Validate("  ");
            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_WrongKeyCache_ReturnsStarter()
        {
            // Write a valid unexpired cache entry for key "KEY-A"
            var cacheJson = BuildCacheJson("KEY-A",
                new[] { "multi_rule", "trim_flatten", "break_even" },
                DateTime.UtcNow.AddDays(7));
            File.WriteAllText(LicenseClient._testCachePath, cacheJson);

            // Validate with "KEY-B" -- cache is keyed to "KEY-A", so cache miss.
            // No network in test; TryRemoteValidate will fail => fallback to Starter.
            var f = LicenseClient.Validate("KEY-B");

            Assert.Equal(FeatureFlags.Starter(), f);
        }

        [Fact]
        public void T_BGTM1_LicenseClient_ValidKey_FromFeatureList()
        {
            var feats = new List<string> { "multi_rule", "trim_flatten", "break_even" };
            var f = FeatureFlags.FromFeatureList(feats);
            Assert.True(f.MultiRule);
            Assert.True(f.TrimFlatten);
            Assert.True(f.BreakEven);
            Assert.False(f.AtrSizing);
            Assert.False(f.ClickTrader);
            Assert.False(f.MirrorMode);
            Assert.False(f.QxGlobalExit);
        }

        // Helper: build a JSON string matching CacheEntry DataContract layout
        private static string BuildCacheJson(string key, string[] features, DateTime expiresUtc)
        {
            var featureItems = string.Join(",", Array.ConvertAll(features, f => $"\"{f}\""));
            return $"{{\"key\":\"{key}\","
                 + $"\"features\":[{featureItems}],"
                 + $"\"cached_utc\":\"\\/Date({ToEpochMs(DateTime.UtcNow)})\\/\","
                 + $"\"expires_utc\":\"\\/Date({ToEpochMs(expiresUtc)})\\/\"}}";
        }

        private static long ToEpochMs(DateTime dt) =>
            (long)(dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }
}
```

#### Part C — scripts/build-release.ps1 (NEW)

Create `scripts/build-release.ps1`:

```powershell
# build-release.ps1 -- PTT release build + ConfuserEx obfuscation
# Usage: powershell -File scripts\build-release.ps1
# Requires: ConfuserEx CLI (crass.exe) in PATH or %CONFUSER_PATH%
# Output: release\PropTraderTools.obfuscated.dll

param(
    [string]$Configuration = "Release",
    [string]$ConfuserCrproj = "confuserex.crproj",
    [string]$OutputDir = "release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "[build-release] Building $Configuration..."
dotnet build src/PropTraderTools/PropTraderTools.csproj -c $Configuration --nologo

$dllPath = Get-ChildItem "src/PropTraderTools/bin/$Configuration" -Filter "PropTraderTools.dll" -Recurse |
           Select-Object -First 1 -ExpandProperty FullName

if (-not $dllPath) {
    Write-Error "[build-release] PropTraderTools.dll not found after build."
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$confuserExe = if ($env:CONFUSER_PATH) { Join-Path $env:CONFUSER_PATH "crass.exe" } else { "crass.exe" }

if (-not (Get-Command $confuserExe -ErrorAction SilentlyContinue)) {
    Write-Warning "[build-release] ConfuserEx not found at '$confuserExe'. Skipping obfuscation."
    Copy-Item $dllPath (Join-Path $OutputDir "PropTraderTools.dll") -Force
    Write-Host "[build-release] DONE (no obfuscation). Output: $OutputDir\PropTraderTools.dll"
    exit 0
}

Write-Host "[build-release] Running ConfuserEx..."
& $confuserExe -n $ConfuserCrproj
Write-Host "[build-release] DONE. Obfuscated output in $OutputDir"
```

#### Part D — confuserex.crproj (NEW — repo root)

Create `confuserex.crproj` at the repository root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- ConfuserEx project file for PropTraderTools release obfuscation -->
<ConfuserProject outputDir="release" baseDir="src\PropTraderTools\bin\Release">
  <Packer id="compressor" />
  <Module path="PropTraderTools.dll">
    <Rule pattern="true" preset="normal" inherit="false">
      <Protection id="rename" />
      <Protection id="constants" />
    </Rule>
  </Module>
</ConfuserProject>
```

---

### Method Signatures

```csharp
// PttGlobalQuickExit.cs -- gate added to existing method
// void Execute()   -- CYC: 7 -> 8 (AT LIMIT, PASS)

// BgtmTests.cs -- full xUnit test class
public sealed class BgtmTests : IDisposable
{
    [Fact] public void T_BGTM1_LicenseClient_NullKey_ReturnsStarter()
    [Fact] public void T_BGTM1_LicenseClient_EmptyKey_ReturnsStarter()
    [Fact] public void T_BGTM1_LicenseClient_WhitespaceKey_ReturnsStarter()
    [Fact] public void T_BGTM1_LicenseClient_OfflineCache_HitReturnsCachedFlags()
    [Fact] public void T_BGTM1_LicenseClient_OfflineCache_ExpiredReturnsStarter()
    [Fact] public void T_BGTM1_LicenseClient_WrongKeyCache_ReturnsStarter()
    [Fact] public void T_BGTM1_FeatureFlags_Starter_AllFalse()
    [Fact] public void T_BGTM1_FeatureFlags_Pro_MultiRuleTrimBreakEvenTrue()
    [Fact] public void T_BGTM1_FeatureFlags_Elite_AllTrue()
    [Fact] public void T_BGTM1_FeatureFlags_FromFeatureList_OnlyMultiRule()
    [Fact] public void T_BGTM1_LicenseClient_ValidKey_FromFeatureList()
    public void Dispose()
    private static string BuildCacheJson(string key, string[] features, DateTime expiresUtc)
    private static long ToEpochMs(DateTime dt)
}
```

---

### JS Rules

| Rule | Constraint | Where Applied |
|------|-----------|---------------|
| **JS-001** (no throw) | `Execute()` gate uses `Output.Process(...)` and `return` — no throw. | PttGlobalQuickExit.cs |
| **JS-021** (no lock) | No new synchronization in gate. | PttGlobalQuickExit.cs |
| **CYC ≤ 8** | `Execute()` CYC 7→8 (AT LIMIT — PASS). Test methods CYC ≤ 3. `BuildCacheJson` CYC=1. | PttGlobalQuickExit.cs, BgtmTests.cs |
| **Testing mandate** | xUnit `[Fact]` only. No `[Test]` (NUnit). No `[TestMethod]` (MSTest). `using Xunit;` only. | BgtmTests.cs |
| **JS-002** (no return null public) | `BuildCacheJson` returns `string` (never null). `ToEpochMs` returns `long`. | BgtmTests.cs |
| **DateTime.UtcNow** | `DateTime.UtcNow` used in `BgtmTests` constructor and `BuildCacheJson` — no `DateTime.Now`. | BgtmTests.cs |

---

### xUnit Tests

All 11 `[Fact]` methods are fully implemented in `src/PropTraderTools/Tests/BgtmTests.cs` (see Part B above). Summary:

| Test Name | What It Asserts |
|-----------|----------------|
| `T_BGTM1_LicenseClient_NullKey_ReturnsStarter` | `Validate(null)` returns all-false `FeatureFlags.Starter()` |
| `T_BGTM1_LicenseClient_EmptyKey_ReturnsStarter` | `Validate("")` returns all-false `FeatureFlags.Starter()` |
| `T_BGTM1_LicenseClient_OfflineCache_HitReturnsCachedFlags` | Valid unexpired cache file returns correct Pro-equivalent flags |
| `T_BGTM1_LicenseClient_OfflineCache_ExpiredReturnsStarter` | Expired cache + no network = `Starter()` |
| `T_BGTM1_FeatureFlags_Starter_AllFalse` | All 7 booleans are `false` |
| `T_BGTM1_FeatureFlags_Pro_MultiRuleTrimBreakEvenTrue` | MultiRule/TrimFlatten/BreakEven=true; AtrSizing/ClickTrader/MirrorMode/QxGlobalExit=false |
| `T_BGTM1_FeatureFlags_Elite_AllTrue` | All 7 booleans are `true` |
| `T_BGTM1_FeatureFlags_FromFeatureList_OnlyMultiRule` | Only `"multi_rule"` in list → only `MultiRule=true` |

---

### 7-Scan Checklist

- [ ] **SCAN-01** `lock()` scan:
  - `grep -rn "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs` — must return 0 in new code
  - `grep -rn "lock(" src/PropTraderTools/Tests/BgtmTests.cs` — must return 0
- [ ] **SCAN-02** `throw` scan:
  - `grep -rn "throw new " src/PropTraderTools/Features/PttGlobalQuickExit.cs` — must return 0 in gate block
  - `grep -rn "throw new " src/PropTraderTools/Tests/BgtmTests.cs` — must return 0 (xUnit uses `Assert.Throws` when needed, not raw `throw new`)
- [ ] **SCAN-03** `return null` review:
  - `grep -rn "return null" src/PropTraderTools/Features/PttGlobalQuickExit.cs` — zero new `return null` in gate code
  - `grep -rn "return null" src/PropTraderTools/Tests/BgtmTests.cs` — zero `return null` in test methods
- [ ] **SCAN-04** CYC ≤ 8 audit:
  - `Execute()` in PttGlobalQuickExit.cs — was CYC=7, now CYC=8 (AT LIMIT, PASS) ✓
  - All 11 `[Fact]` methods — CYC ≤ 3 each ✓
  - `BuildCacheJson` — CYC=1 ✓
  - `ToEpochMs` — CYC=1 ✓
  - `Dispose` — CYC=2 (if-exists check) ✓
- [ ] **SCAN-05** ASCII-only:
  - Gate message: `"[PTT-QX-ALL] Blocked: Global Quick Exit requires Elite tier"` — ASCII only, square brackets are ASCII ✓
  - All test string literals (feature names, key strings, temp paths) — ASCII only ✓
  - `confuserex.crproj` content — ASCII only ✓
  - `build-release.ps1` content — ASCII only ✓
- [ ] **SCAN-06** NT8 API: Gate uses `NinjaTrader.Code.Output.Process(...)` and `NinjaTrader.NinjaScript.PrintTo.OutputTab1` — both are confirmed safe NT8 API (matches the pattern already used in `Execute()`). No `AtmStrategyCreate()`. No `CreateOrder()`.
- [ ] **SCAN-07** Sealed record: `BgtmTests` asserts `FeatureFlags.Starter()` equality using `Assert.Equal(FeatureFlags.Starter(), f)` — this works because `sealed record` provides value equality semantics by default. Verify this compiles without needing a custom `IEqualityComparer`.

**Completion artifact**: `docs/brain/BGTM-1/ticket-6-completion.md`

---

## Execution Order

| Ticket | Dependency | Can Start When |
|--------|-----------|---------------|
| T1 | None | Immediately |
| T2 | T1 (FeatureFlags type) | T1 complete |
| T3 | T1 + T2 (LicenseClient + SetFlags) | T2 complete |
| T4 | T1 + T2 (FeatureFlags + FeatureFlagsChanged event) | T2 complete |
| T5 | T1 + T2 (FeatureFlags + FeatureFlagsChanged event) | T2 complete — may run in parallel with T4 |
| T6 | T1 + T2 (gate on T6-A, tests on T6-B) | T2 complete — QxGlobalExit gate; tests depend on T1 |

T4 and T5 may run in parallel after T2 completes.

---

## Global Compliance Reminders (All Tickets)

1. **No `lock()`** anywhere — zero occurrences in all 6 tickets. Use `volatile` (CopyEngine) or UI-thread-only patterns.
2. **No `throw new XxxException`** in any production code path. All error paths use `try/catch { return Starter(); }` or `return null`.
3. **No `DateTime.Now`** — all date references use `DateTime.UtcNow`.
4. **No hex color literals** — all color references use `MakeWinBrush(r,g,b)` or existing resource keys.
5. **No `FontFamily`** assignments anywhere.
6. **ASCII-only string literals** throughout.
7. **xUnit only** for tests — no `using NUnit.Framework`, no `using Microsoft.VisualStudio.TestTools.UnitTesting`.
8. **Sealed record keyword** — `FeatureFlags` must have both `sealed` and `record` on the same declaration line.
9. **IsExternalInit shim** must be present in `LicenseClient.cs` before the namespace declaration.
10. **LangVersion 9.0** in csproj before F5.

---

**TICKETS_COMPLETE**
