# BGTM-1 Plan Review

**Block**: BGTM-1 (License Gating + Feature Flags)
**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Date**: 2026-08-26
**Input**: docs/brain/BGTM-1/02-architecture-plan.md
**Rules Ref**: docs/standards/jane-street/RULES_CATALOG.md

---

## RESULT: REVIEW_PASS

No violations found. All 12 checklist items PASS. Zero rule citations triggered.

---

## Violation Log

*No violations.*

| # | Rule ID | Description | Plan Location | Status |
|---|---------|-------------|---------------|--------|
| — | — | No violations found | — | — |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| LicenseClient: static class, single entry Validate(string) → FeatureFlags | ✅ YES | §3 |
| LicenseClient: no throw anywhere (JS-001) | ✅ YES | §0 table, §3.2–3.8 |
| LicenseClient: no lock (JS-021) | ✅ YES | §0 table, §4.1 |
| FeatureFlags: sealed record (JS-003) | ✅ YES | §2 |
| FeatureFlags: 7 flags (MultiRule/TrimFlatten/BreakEven/AtrSizing/ClickTrader/MirrorMode/QxGlobalExit) | ✅ YES | §2 |
| FeatureFlags: factory methods Starter/Pro/Elite/FromFeatureList | ✅ YES | §2 |
| CopyEngine: _flags field (volatile) | ✅ YES | §4.1 |
| CopyEngine: SetFlags method | ✅ YES | §4.1 |
| CopyEngine: Flags property | ✅ YES | §4.1 |
| CopyEngine: FeatureFlagsChanged event | ✅ YES | §4.1 |
| CopyEngine: 9+ gated methods (16 overloads covered) | ✅ YES | §4.2 |
| TradeCopierAddOn.OnInitialize: reads license.txt, calls Validate(), SetFlags() | ✅ YES | §5.1 |
| TradeCopierAddOn.RegisterClickTrader: gate on Flags.ClickTrader | ✅ YES | §5.2 |
| TradeCopierWindow: license key row | ✅ YES | §6.1, §6.2 |
| TradeCopierWindow: OnActivateClick | ✅ YES | §6.2 |
| TradeCopierWindow: ApplyFeatureFlags | ✅ YES | §6.2 |
| TradeCopierWindow: LoadLicenseKeyDisplay | ✅ YES | §6.2 |
| TradeCopierPanel: ApplyFeatureFlags | ✅ YES | §7.1 |
| TradeCopierPanel: wired to FeatureFlagsChanged | ✅ YES | §7.1 |
| PttGlobalQuickExit.Execute(): gate on Flags.QxGlobalExit | ✅ YES | §8 |
| NuGet: SKM.NET.Standard added to PropTraderTools.csproj | ✅ YES | §3.10, §9 |
| xUnit tests: all 10 named test cases | ✅ YES | §11.1 |
| CYC ≤ 8 all new methods | ✅ YES | §3.2–3.8, §5–8 |

---

## Checklist Detail

### 1. All 8 deliverables covered

All 8 spec deliverables are present: LicenseClient+FeatureFlags (T1), CopyEngine gates (T2),
TradeCopierAddOn init (T3), TradeCopierWindow UI (T4), TradeCopierPanel (T5),
PttGlobalQuickExit gate (T6), SKM.NET NuGet (§3.10 + §9), xUnit tests (T6 / §11).

**→ PASS**

### 2. JS-001 (no throw in LicenseClient)

All `LicenseClient` methods are wrapped in `try/catch` returning `null` or `FeatureFlags.Starter()`
on exception. `Validate()` is documented "Never throws." `LoadAndValidateLicense()` in
TradeCopierAddOn also catches and returns `Starter()`. No plan path permits a thrown exception to
escape any public or internal surface.

**→ PASS**

### 3. JS-003 (sealed record for FeatureFlags)

Plan specifies `internal sealed record FeatureFlags(...)` with positional constructor, factory
methods, and the NT8 CS0518 IsExternalInit shim to support C# 9 record syntax.

**→ PASS**

### 4. JS-021 (no lock anywhere)

No `lock()` appears in any code block or method description throughout the plan. State mutation in
`SetFlags` uses direct `volatile` assignment (`_flags = f`). No Monitor, Mutex, or SemaphoreSlim
used for state protection.

**→ PASS**

### 5. CYC ≤ 8 all new methods

Every method table and CYC annotation reviewed:

| Method | CYC |
|--------|-----|
| Validate | 4 |
| TryRemoteValidate | 3 |
| ParseSkmResponse | 2 |
| TryReadCache | 4 |
| DeserializeCache | 3 |
| WriteCache | 2 |
| SetFlags | 1 |
| LoadAndValidateLicense | 2 |
| RegisterClickTrader (after gate) | 3 |
| BuildLicenseRow | 1 |
| OnActivateClick | 1 |
| ApplyFeatureFlags (Window) | 1 |
| LoadLicenseKeyDisplay | 2 |
| OnFeatureFlagsChanged (Window) | 1 |
| GetStatusText | 3 |
| ApplyFeatureFlags (Panel) | 1 |
| OnFeatureFlagsChanged (Panel) | 1 |
| PttGlobalQuickExit.Execute (after gate) | 8 ← AT LIMIT, PASS |

Gate additions to CopyEngine methods each add +1; all are documented at ≤7 before gate,
yielding ≤8 after. Engineer is warned in §4.2 to verify and extract if any are already at limit.

**→ PASS**

### 6. volatile / thread-safety for _flags

`private volatile FeatureFlags _flags` declared in §4.1. Threading model documented in §12:
all `SetFlags` calls and all gate reads occur on the UI thread. `volatile` provides a conservative
guarantee (visibility fence) that is correct and sufficient. No cross-thread access path is present
that would require stronger synchronization.

**→ PASS**

### 7. NT8 DLL placement for SKM.NET

§3.10 specifies the complete deployment procedure:
- Extract net46/net48 DLL from NuGet package
- Copy to `$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\`
- Add `<Reference>` with `<HintPath>` and `<Private>false</Private>` to csproj
- Add `<PackageReference>` for OmniSharp IntelliSense only

§9 repeats the csproj changes. NT8 runtime DLL placement is fully addressed.

**→ PASS**

### 8. Cache format: key, features[], cached_utc, expires_utc, 7-day TTL

`CacheEntry` DTO in §3.9:
- `[DataMember(Name = "key")]` → key ✅
- `[DataMember(Name = "features")] public List<string> Features` → features[] ✅
- `[DataMember(Name = "cached_utc")]` → cached_utc ✅
- `[DataMember(Name = "expires_utc")]` → expires_utc ✅
- TTL: `ExpiresUtc = DateTime.UtcNow.AddDays(7)` in WriteCache (§3.8) ✅

**→ PASS**

### 9. Two-layer gate: engine + UI

Engine layer: §4.2 — 16 method overloads in CopyEngine each carry a first-line guard.
UI layer: §6.2 (`TradeCopierWindow.ApplyFeatureFlags`) and §7.1 (`TradeCopierPanel.ApplyFeatureFlags`)
disable/hide controls. §13 data-flow diagram confirms both layers fire on every `SetFlags` call.

**→ PASS**

### 10. Event wiring: both Window and Panel subscribed

TradeCopierWindow (§6.2):
- Subscribe: `CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged` in OnLoaded
- Unsubscribe: `CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged` in OnWindowClosed

TradeCopierPanel (§7.1):
- Subscribe: `CopyEngine.Instance.FeatureFlagsChanged += OnFeatureFlagsChanged` in OnLoaded
- Unsubscribe: `CopyEngine.Instance.FeatureFlagsChanged -= OnFeatureFlagsChanged` in Detach()

Both subscribe and unsubscribe paths present for both subscribers.

**→ PASS**

### 11. Ticket grouping — zero file cross-contamination

| Ticket | File(s) | Cross-touch risk |
|--------|---------|------------------|
| T1 | LicenseClient.cs, PropTraderTools.csproj | None — all csproj edits in T1 including BgtmTests compile entry |
| T2 | CopyEngine.cs | None |
| T3 | TradeCopierAddOn.cs | None |
| T4 | TradeCopierWindow.cs | None |
| T5 | TradeCopierPanel.cs | None |
| T6 | PttGlobalQuickExit.cs, Tests/BgtmTests.cs, scripts/build-release.ps1, confuserex.crproj | None |

T1 adds both compile entries upfront (`LicenseClient.cs` and `Tests\BgtmTests.cs`) so T6 does not
need to re-touch PropTraderTools.csproj.

**→ PASS**

### 12. B107 carry-forward items explicitly out of scope

§14 enumerates all carry-forward items with explicit disposition:
- DW-B107 (MoveStopToBreakEven) → deferred to B108
- B107-DEFER-01 (F5 NT8 gate) → Director-owned
- B107-DEFER-02 (Combo C live re-test) → Director-owned
- DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01..06 → carry-forward

**→ PASS**

---

## NT8 Hard Constraint Checks

| Constraint | Status |
|------------|--------|
| No async/await in OnInitialize/OnDestroyed/OnWindowCreated | PASS — LoadAndValidateLicense is synchronous |
| No Account.All in constructor | PASS — not present |
| sealed TradeCopierWindow unchanged | PASS — class declaration not modified |
| No FontFamily override | PASS — §0 table confirmed |
| No hardcoded #RRGGBB hex | PASS — MakeWinBrush(r,g,b) used |
| No CreateOrder without PTT- prefix | PASS — no order creation in this plan |
| No DateTime.Now (UtcNow only) | PASS — §0 table confirmed |

---

## Notes for Engineer

1. **CYC pre-check mandatory**: §4.2 explicitly instructs engineer to verify current CYC of each
   gated CopyEngine method before adding the guard. If any method is already at CYC=8, an inner
   helper must be extracted first. This is documented but falls on engineer verification.

2. **Control field name assumptions**: §7.1 lists assumed field names in TradeCopierPanel
   (`_trimBtn`, `_flattenBtn`, etc.). Engineer must verify actual names against source. Plan
   documents this requirement explicitly.

3. **SKM.NET timeout**: §12 specifies `timeout: 3` seconds on the SKM.NET Activate call to
   prevent UI freeze. Engineer must apply this at implementation time.

4. **IsExternalInit shim placement**: Must be added BEFORE the namespace declaration in
   LicenseClient.cs as documented in §2. Incorrect placement causes CS0518.

---

## Summary

The architecture plan for BGTM-1 is complete, internally consistent, and compliant with all
applicable Jane Street rules (JS-001, JS-003, JS-021, JS-023), NT8 hard constraints, and spec
requirements. The plan correctly addresses all 8 deliverables, provides explicit CYC budgets for
every new method, covers the full two-layer gating architecture, wires both event subscribers with
proper unsubscribe paths, achieves clean ticket separation, and explicitly defers all B107
carry-forward items.

**Phase 3 (ticket generation) is UNLOCKED.**
