# BGTM-1 Final Review

**Block**: BGTM-1 (License Gating + Feature Flags)
**Reviewer**: ptt-plan-reviewer
**Phase**: 5 (Final Review)
**Date**: 2026-08-28
**Inputs read**:
- docs/brain/BGTM-1/02-architecture-plan.md
- docs/brain/BGTM-1/04-ticket-review.md (TICKET_REVIEW_PASS — CYCLE 3)
- docs/brain/BGTM-1/ticket-1-completion.md + ticket-1-verification.md (VERIFY_PASS)
- docs/brain/BGTM-1/ticket-2-completion.md + ticket-2-verification.md (VERIFY_PASS)
- docs/brain/BGTM-1/ticket-3-completion.md + ticket-3-verification.md (VERIFY_PASS)
- docs/brain/BGTM-1/ticket-4-completion.md + ticket-4-verification.md (VERIFY_PASS)
- docs/brain/BGTM-1/ticket-5-completion.md + ticket-5-verification.md (VERIFY_PASS)
- docs/brain/BGTM-1/ticket-6-completion.md + ticket-6-verification.md (VERIFY_PASS)
- docs/standards/jane-street/RULES_CATALOG.md
- docs/brain/B107/06-deferred-backlog.md (prior backlog)

---

## SECTION A — SYSTEM COHERENCE

### A1. FeatureFlags flow: LicenseClient.Validate → CopyEngine.SetFlags → FeatureFlagsChanged → Panel/Window ApplyFeatureFlags

**PASS**

Evidence from source (independent grep verification):
- `LicenseClient.Validate(key)` defined in `LicenseClient.cs` as `public static FeatureFlags Validate(string key)` — returns `FeatureFlags` never null.
- `CopyEngine.SetFlags(FeatureFlags f)` at `CopyEngine.cs:L163` assigns `_flags = f` (volatile) then fires `FeatureFlagsChanged?.Invoke(f)` at L166.
- `FeatureFlagsChanged` subscriptions confirmed:
  - `TradeCopierPanel.cs:L794` (`OnLoaded` subscribe) and `L618` (`Detach` unsubscribe).
  - `TradeCopierWindow.cs:L151` (`OnLoaded` subscribe) and `L181` (`OnWindowClosed` unsubscribe).
- `ApplyFeatureFlags(FeatureFlags f)` confirmed in both Panel (`TradeCopierPanel.cs:L3066`) and Window (`TradeCopierWindow.cs:L397`).

### A2. TradeCopierAddOn.OnInitialize calls LoadAndValidateLicense → CopyEngine.SetFlags

**PASS**

Evidence:
- `TradeCopierAddOn.cs:L71-75`: `if (State == State.Configure)` block calls `LoadAndValidateLicense()` at L73, then `CopyEngine.Instance.SetFlags(flags)` at L74.
- `LoadAndValidateLicense()` at `TradeCopierAddOn.cs:L629`: reads `license.txt` path, calls `LicenseClient.Validate(key)`, wraps entirely in try/catch returning `FeatureFlags.Starter()` on failure.

### A3. TradeCopierWindow.OnActivateClick: key → license.txt → LicenseClient.Validate → SetFlags → ApplyFeatureFlags

**PASS**

Evidence from `TradeCopierWindow.cs`:
- L381: `var key = _licenseKeyBox.Text.Trim()`
- L383-388: try/catch `File.WriteAllText(LicenseTxtPath, key)`
- L389: `LicenseClient.Validate(key)` → flags
- L390: `CopyEngine.Instance.SetFlags(flags)` — fires FeatureFlagsChanged to Panel
- L391: `ApplyFeatureFlags(flags)` — direct Window update
- L392: `_licenseStatusText.Text = GetStatusText(flags)`

Sequential flow matches architecture plan Section 6 / data flow diagram Section 13.

### A4. PttGlobalQuickExit.Execute() QxGlobalExit gate

**PASS**

Evidence: `PttGlobalQuickExit.cs:L38` — `if (!CopyEngine.Instance.Flags.QxGlobalExit)` is the absolute first executable statement of `Execute()`, followed by `Output.Process(...)` + `return` at L39-43.

### A5. Both UI layers present: engine gates + ApplyFeatureFlags on Panel and Window

**PASS**

- Engine gates: 16 gate guards confirmed present in `CopyEngine.cs` across AddRule, Trim, Flatten, CancelPendingEntries, BreakEven, ArmTrailBe, SetCopyMode, SetAtrEngine, GetSuggestedQty.
- Panel `ApplyFeatureFlags`: L3066 confirmed, wiring 7 controls (_trimBtn2, _flattenBtn2, _cancelBtn2, _beBtn2, _mirrorModeBtn, _clickTraderRow, _atrRow).
- Window `ApplyFeatureFlags`: L397 confirmed, gating trim/flatten/cancel button lists, mode ComboBox, add-rule button.
- License row UI in Window: `BuildLicenseRow` confirmed at L333.

---

## SECTION B — SPEC REQUIREMENTS (8 Deliverables)

| # | Deliverable | Status | Evidence |
|---|-------------|--------|----------|
| 1 | `LicenseClient.cs` (new) — FeatureFlags sealed record + LicenseClient static class + CacheEntry DTO | PASS | File created; sealed record confirmed at L9 by grep; all methods (Validate, TryRemoteValidate, ParseSkmResponse, TryReadCache, DeserializeCache, WriteCache, GetFeatureList, InferTierName) present; VERIFY_PASS T1 |
| 2 | `CopyEngine.cs` — `_flags`, `SetFlags`, `Flags`, `FeatureFlagsChanged`, 16 gate guards | PASS | volatile field L154, property L157, event L160, method L163 confirmed; all 16 gate guards confirmed by Layer 3 SCAN-07; VERIFY_PASS T2 |
| 3 | `TradeCopierAddOn.cs` — `LoadAndValidateLicense` in `State.Configure`, `RegisterClickTrader` gate | PASS | State.Configure block L71-75; LoadAndValidateLicense L629; ClickTrader gate L294; VERIFY_PASS T3 |
| 4 | `TradeCopierWindow.cs` — license row, `OnActivateClick`, `ApplyFeatureFlags`, `LoadLicenseKeyDisplay` | PASS | All 6 new methods confirmed; fields at L62-69; subscribe L151, unsubscribe L181; VERIFY_PASS T4 |
| 5 | `TradeCopierPanel.cs` — `ApplyFeatureFlags`, `FeatureFlagsChanged` wiring | PASS | ApplyFeatureFlags L3066, 7 control wirings, subscribe L794, unsubscribe L618; VERIFY_PASS T5 |
| 6 | `PttGlobalQuickExit.cs` — `QxGlobalExit` gate in `Execute()` | PASS | Gate at L38 confirmed; CYC 7→8 AT LIMIT PASS; VERIFY_PASS T6 |
| 7 | `scripts/build-release.ps1` — present | PASS | File confirmed by T6 SCAN-6 and T6 Layer 3 verification |
| 8 | `confuserex.crproj` — present at repo root | PASS | File confirmed by glob (`confuserex.crproj` exists); T6 Layer 3 verified content matches spec |

**All 8 deliverables: PASS**

---

## SECTION C — JS RULES COMPLIANCE

All scans performed independently via grep against `src/PropTraderTools/` .cs files.

### C1. JS-001 (no throw in gate methods / hot paths)

**PASS**

- `grep "throw new" src/PropTraderTools/LicenseClient.cs` → **0 matches**
- `grep "throw new" src/PropTraderTools/CopyEngine.cs` → **0 matches** (new code)
- `grep "throw new" src/PropTraderTools/TradeCopierAddOn.cs` → **0 matches** (new code)
- `grep "throw new" src/PropTraderTools/TradeCopierWindow.cs` → 1 pre-existing match at L1007 (`AccountDisplayConverter.ConvertBack`) — **not a BGTM-1 method, not a gate method**. Pre-existing violation, out of scope.
- `grep "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs` → **0 matches**
- `grep "throw new" src/PropTraderTools/Tests/BgtmTests.cs` → **0 matches**

**Note**: The pre-existing `throw new NotImplementedException` at `TradeCopierWindow.cs:L1007` in `AccountDisplayConverter.ConvertBack` is a one-way IValueConverter — not a gate method, not in `OnOrderUpdate`/`SendCopy`. It was present before BGTM-1 and was not introduced by this block. It is NOT a BGTM-1 violation.

### C2. JS-003 (sealed record for FeatureFlags)

**PASS**

`grep "sealed record FeatureFlags" src/PropTraderTools/LicenseClient.cs` → 1 match at L9:
`internal sealed record FeatureFlags(` — both `sealed` and `record` keywords present.

### C3. JS-021 (no lock anywhere in new code)

**PASS**

`grep -r "lock\s*\(" src/PropTraderTools/ --include="*.cs"` → 13 matches, **all in comments** (JS-021 compliance notes: `// JS-021: no lock()`, `// JS-021: ConcurrentDictionary -- lock-free`, etc.). Zero executable `lock(` statements in any file.

### C4. JS-023 (volatile for shared mutable reference)

**PASS**

`private volatile FeatureFlags _flags = FeatureFlags.Starter()` at `CopyEngine.cs:L154`. All reads/writes of `_flags` occur on UI thread per architecture plan Section 12, with the volatile qualifier providing correct CLR 4.0+ atomic reference semantics.

### C5. CYC ≤ 8 — all new methods

**PASS**

Summary of verified CYC values across all tickets:

| File | Method | CYC | Status |
|------|--------|-----|--------|
| LicenseClient.cs | Validate | 4 | PASS |
| LicenseClient.cs | TryRemoteValidate | 4 | PASS |
| LicenseClient.cs | ParseSkmResponse | 3-4 | PASS |
| LicenseClient.cs | TryReadCache | 6 | PASS |
| LicenseClient.cs | DeserializeCache | 3 | PASS |
| LicenseClient.cs | WriteCache | 2 | PASS |
| LicenseClient.cs | GetFeatureList | 8 | PASS (AT LIMIT) |
| LicenseClient.cs | InferTierName | 3 | PASS |
| CopyEngine.cs | SetFlags | 1 | PASS |
| TradeCopierAddOn.cs | LoadAndValidateLicense | 2 | PASS |
| TradeCopierAddOn.cs | RegisterClickTrader (post-gate) | 3 | PASS |
| TradeCopierWindow.cs | BuildLicenseRow | 1 | PASS |
| TradeCopierWindow.cs | OnActivateClick | 1 | PASS |
| TradeCopierWindow.cs | ApplyFeatureFlags | 2 | PASS |
| TradeCopierWindow.cs | LoadLicenseKeyDisplay | 2 | PASS |
| TradeCopierWindow.cs | OnFeatureFlagsChanged | 1 | PASS |
| TradeCopierWindow.cs | GetStatusText | 3 | PASS |
| TradeCopierPanel.cs | ApplyFeatureFlags | 1 | PASS |
| TradeCopierPanel.cs | ApplyFeatureFlagTooltips | 1 | PASS |
| TradeCopierPanel.cs | OnFeatureFlagsChanged | 1 | PASS |
| PttGlobalQuickExit.cs | Execute (post-gate) | 8 | PASS (AT LIMIT) |

All gated engine methods post-gate: max CYC=7 (Trim 4-arg). All ≤ 8. PASS.

### C6. Additional NT8 DNA Rules

**PASS**

- `grep "async\s+void" src/PropTraderTools/ --include="*.cs"` — all 36 matches are **comments only** (e.g., `// JS-033: no async void`). Zero executable `async void` in new BGTM-1 code.
- `grep "DateTime\.Now[^U]"` → 1 match in `PttBreakEven.cs` comment (documentation reference, not `DateTime.Now` usage). Zero `DateTime.Now` calls.
- `FontFamily`: no new FontFamily assignments in any BGTM-1 file.
- Hex colors `#RRGGBB`: no hardcoded hex color strings in BGTM-1 code (TradeCopierWindow.cs comments at L81-84 reference hex as documentation, not string literals).
- ASCII-only: all new files/changes confirmed ASCII by SCAN-05 in all 6 tickets.

---

## SECTION D — CROSS-FILE COHERENCE

### D1. FeatureFlags namespace consistent across all files

**PASS**

All files use `namespace PropTraderTools`:
- `LicenseClient.cs:L7` — `namespace PropTraderTools`
- `CopyEngine.cs:L48` — `namespace PropTraderTools`
- `TradeCopierAddOn.cs:L32` — `namespace PropTraderTools`
- `TradeCopierWindow.cs:L26` — `namespace PropTraderTools`
- `TradeCopierPanel.cs:L113` — `namespace PropTraderTools`
- `PttGlobalQuickExit.cs:L12` — `namespace PropTraderTools`

`FeatureFlags` is declared in `LicenseClient.cs` within `PropTraderTools`. Used in `CopyEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `TradeCopierPanel.cs`, `PttGlobalQuickExit.cs` — all in the same namespace. No cross-namespace pollution. No duplicate `sealed record FeatureFlags` declaration (verified by grep — 1 declaration only, in `LicenseClient.cs:L9`).

### D2. CopyEngine.Instance access pattern consistent

**PASS**

- `TradeCopierAddOn.cs`: `CopyEngine.Instance.SetFlags(flags)` at L74.
- `TradeCopierWindow.cs`: `CopyEngine.Instance.SetFlags(flags)` at L390; `CopyEngine.Instance.Flags` at L391+; `CopyEngine.Instance.FeatureFlagsChanged` at L151/L181.
- `TradeCopierPanel.cs`: `CopyEngine.Instance.FeatureFlagsChanged` at L794/L618; `CopyEngine.Instance.Flags` at L795.
- `PttGlobalQuickExit.cs`: `CopyEngine.Instance.Flags.QxGlobalExit` at L38.

All files use `CopyEngine.Instance.*` — no direct field access bypassing the singleton. Pattern consistent.

### D3. Event subscription lifecycle

**PASS**

| Component | Subscribe | Unsubscribe |
|-----------|-----------|-------------|
| TradeCopierWindow | `OnLoaded` (L151) | `OnWindowClosed` (L181) |
| TradeCopierPanel | `OnLoaded` (L794) | `Detach()` (L618) |

Both components subscribe in `OnLoaded` (after controls are constructed) and unsubscribe in their respective teardown methods (`OnWindowClosed` for Window, `Detach` for Panel). No subscription leak. Architecture plan Section 6 and Section 7 wiring correctly implemented.

### D4. license.txt path consistent across all files

**PASS**

Both files that reference `license.txt` use the identical path pattern:
- **TradeCopierWindow.cs:L66-69**: `static readonly string LicenseTxtPath = Path.Combine(Globals.UserDataDir, "PropTraderTools", "license.txt")`
- **TradeCopierAddOn.cs:L633-636**: `Path.Combine(Globals.UserDataDir, "PropTraderTools", "license.txt")` (inline in `LoadAndValidateLicense`)
- **LicenseClient.cs**: cache path uses `license_cache.json` in same directory (`UserDataDir/PropTraderTools/license_cache.json`) — no conflict.

`PropTraderTools` folder segment is identical across all three usages. No path divergence.

---

## SECTION E — TICKET REVIEW HISTORY

TICKET_REVIEW_PASS confirmed at **CYCLE 3** (3 review cycles required):
- CYCLE 1 FAIL: 3 test cases from plan §11.1 absent from ticket stubs and BgtmTests.cs.
- CYCLE 2 FAIL: 3 new violations introduced by architect fix (test name/body mismatch, wrong test body, stale SCAN-04 count).
- CYCLE 3 PASS: All 3 CYCLE 2 violations resolved. All 6 tickets pass all 10 checklist items.

---

## SECTION K — DEFERRED WORK (MANDATORY)

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| BGTM-1-DEFER-01 | F5 NinjaTrader 8 Compilation Gate — Director must press F5 after `ptt-sync-and-verify.ps1` confirms 0 MISMATCH. Required before any SIM or go-live validation of BGTM-1 gating behavior. | P0 | Director (immediate) | OPEN |
| BGTM-1-DEFER-02 | Cryptolens dashboard setup — Replace `CRYPTOLENS_ACCESS_TOKEN_PLACEHOLDER` and product ID `1234` in `LicenseClient.cs:TryRemoteValidate` with real Cryptolens account values. Required before any live license activation will succeed remotely. | P0 | Director / product owner | OPEN |
| BGTM-1-DEFER-03 | SKM.NET.Standard DLL physical deployment to NT8 `bin/Custom/` — The `Reference` HintPath in `PropTraderTools.csproj` points to `$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll`. This DLL must be physically present there at runtime (not just at LSP/compile time). Required for `TryRemoteValidate` to work at runtime. | P1 | Director / environment setup | OPEN |
| BGTM-1-DEFER-04 | `BgtmTests.cs` full execution in xUnit test runner — 11 `[Fact]` methods implemented; net48 compatibility with xUnit harness and `NinjaTrader.Core.Globals.UserDataDir` stub must be verified in actual `dotnet test` run. The `_testCachePath` injection avoids `UserDataDir` but test project targeting and xUnit framework compatibility need runtime confirmation. | P1 | Director / CI gate | OPEN |
| DW-B107 | MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers | P2 | B108 | OPEN (carry-forward) |
| B107-DEFER-01 | F5 NinjaTrader 8 Compilation Gate (B107 changes) | P0 | Director (immediate) | OPEN (carry-forward) |
| B107-DEFER-02 | Combo C Live Re-Test (BE-ALL then QX-ALL) | P1 | Director SIM session | OPEN (carry-forward) |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Low | B43 or future block | OPEN (carry-forward) |
| DW-B42-02 | Live NT8 F5 verification required | High | Next live F5 session | OPEN (carry-forward) |
| DW-B42-03 | IsPttQxTarget range extension for future target slots | Conditional | Block adding 4th+ target | OPEN (carry-forward) |
| DW-PTT-BE-FIX-01 | Lazy re-resolve for null followers (Option A) | Medium | Next PTT productionisation block | OPEN (carry-forward) |
| DW-PTT-BE-FIX-02 | SIM gate: Path B 3-cycle runtime verification | High | DW-B89 SIM gate session | OPEN (carry-forward) |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (83 errors + CS0433 Globals ambiguity) | High | Dedicated remediation block | OPEN (carry-forward) |
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate (DW-B89 changes) | P0 | Director (immediate) | OPEN (carry-forward) |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal | High | After DW-B89-DEFERRED-01 green | OPEN (carry-forward) |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case (short position) | High | After DW-B89-DEFERRED-01 green | OPEN (carry-forward) |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) | High | After DW-B89-DEFERRED-01 green | OPEN (carry-forward) |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle | High | After DW-B89-DEFERRED-01 green | OPEN (carry-forward) |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 in spec HTML | Medium | After all DW-B89 SIM paths green | OPEN (carry-forward) |

---

## VERDICT

**FINAL_PASS**

All 6 tickets reached VERIFY_PASS. All 8 spec deliverables satisfied. System coherence confirmed end-to-end. No JS rule violations in BGTM-1 code. All CYC values ≤ 8. Cross-file consistency verified (namespace, CopyEngine.Instance pattern, event lifecycle, license.txt path). Section K written.

BGTM-1 is PIPELINE_COMPLETE pending Director-owned deferred items (BGTM-1-DEFER-01 through 04).
