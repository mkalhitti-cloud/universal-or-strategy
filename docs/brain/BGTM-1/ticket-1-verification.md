# Ticket 1 Verification -- BGTM-1

**Block**: BGTM-1 (License Gating + Feature Flags)
**Ticket**: T1 -- LicenseClient.cs + FeatureFlags + csproj
**Verifier**: ptt-verifier
**Date**: 2026-08-26
**Inputs Read**:
- `src/PropTraderTools/LicenseClient.cs` (READ -- actual file)
- `src/PropTraderTools/PropTraderTools.csproj` (READ -- actual file)
- `src/PropTraderTools/Tests/BgtmTests.cs` (READ via Get-Content -- bobignored)
- `docs/brain/BGTM-1/04-tickets.md`
- `docs/brain/BGTM-1/ticket-1-completion.md`
- `docs/brain/BGTM-1/02-architecture-plan.md`

---

## Verdict

**VERIFY_PASS**

All 7 scans: PASS. All 11 contract items: PASS. No DNA violations found. Engineer Layer 2 report is accurate (one minor CYC discrepancy on ParseSkmResponse -- still <=8, not a violation).

---

## Independent Scan Results (Layer 3)

All scans run independently via `execute_command` (PowerShell `Select-String` / byte scan). Engineer scan results were NOT trusted before running.

### SCAN-01 -- lock() scan
**Command**: `Select-String -Path "src/PropTraderTools/LicenseClient.cs" -Pattern "lock\s*\("`
**Result**: 0 matches
**Verdict**: PASS
**Engineer reported**: 0 matches
**Cross-check**: MATCH

### SCAN-02 -- throw new scan
**Command**: `Select-String -Path "src/PropTraderTools/LicenseClient.cs" -Pattern "throw new "`
**Result**: 0 matches
**Verdict**: PASS
**Engineer reported**: 0 matches
**Cross-check**: MATCH

### SCAN-03 -- return null review
**Command**: `Select-String -Path "src/PropTraderTools/LicenseClient.cs" -Pattern "return null"`
**Result**: 10 matches at lines 88, 90, 96, 104, 120, 124, 126, 133, 150, 154

Location breakdown (verified against source):
| Line | Method | Access | Compliant? |
|------|--------|--------|-----------|
| L88  | TryRemoteValidate | private | YES |
| L90  | TryRemoteValidate | private | YES |
| L96  | TryRemoteValidate | private | YES |
| L104 | ParseSkmResponse  | private | YES |
| L120 | TryReadCache      | private | YES |
| L124 | TryReadCache      | private | YES |
| L126 | TryReadCache      | private | YES |
| L133 | TryReadCache      | private | YES |
| L150 | DeserializeCache  | private | YES |
| L154 | DeserializeCache  | private | YES |

`public static FeatureFlags Validate(...)` at L56-70: ZERO `return null` occurrences. JS-002 PASS.
**Verdict**: PASS
**Engineer reported**: 11 matches (all private) -- minor count difference (engineer counted 11, I count 10 unique lines; the engineer may have counted a comment line as a match). Both are compliant -- all `return null` in private methods only.
**Cross-check**: EQUIVALENT (both 0 in public method, all in private -- PASS)

### SCAN-04 -- CYC audit per method
**Method**: Branch-counting from source (if/catch/foreach/while/||/&&)

| Method | Decision Points Found | CYC | <=8? | Engineer Reported | Delta |
|--------|----------------------|-----|------|------------------|-------|
| FeatureFlags.Starter | 0 | 1 | YES | 1 | MATCH |
| FeatureFlags.Pro | 0 | 1 | YES | 1 | MATCH |
| FeatureFlags.Elite | 0 | 1 | YES | 1 | MATCH |
| FeatureFlags.FromFeatureList | 0 | 1 | YES | 1 | MATCH |
| LicenseClient.Validate | 3 if | 4 | YES | 4 | MATCH |
| LicenseClient.TryRemoteValidate | 2 if + 1 catch | 4 | YES | 4 | MATCH |
| LicenseClient.ParseSkmResponse | 2 if + 1 foreach | 4 | YES | 3 | MINOR DISCREPANCY (foreach = +1; still <=8) |
| LicenseClient.TryReadCache | 3 if + 1 || + 1 catch | 6 | YES | 6 | MATCH |
| LicenseClient.DeserializeCache | 2 catch | 3 | YES | 3 | MATCH |
| LicenseClient.WriteCache | 1 catch | 2 | YES | 2 | MATCH |
| LicenseClient.GetFeatureList | 7 if | 8 | YES (AT LIMIT) | 8 | MATCH |
| LicenseClient.InferTierName | 2 if | 3 | YES | 3 | MATCH |

All methods CYC <=8. PASS.
Minor CYC discrepancy on ParseSkmResponse (engineer=3, verifier=4) does NOT constitute a violation; both are <=8.

**Verdict**: PASS

### SCAN-05 -- ASCII-only
**Command**: Byte scan via `[System.IO.File]::ReadAllBytes(...)` -- checked all bytes > 127
**Result**: 0 non-ASCII bytes
**Verdict**: PASS
**Engineer reported**: 0 non-ASCII bytes
**Cross-check**: MATCH

### SCAN-06 -- async void
**Command**: `Select-String -Path "src/PropTraderTools/LicenseClient.cs" -Pattern "async\s+void"`
**Result**: 0 matches
**Verdict**: PASS
**Engineer reported**: 0 matches (engineer scanned for "async void" specifically)
**Cross-check**: MATCH

### SCAN-07 -- sealed record FeatureFlags
**Command**: `Select-String -Path "src/PropTraderTools/LicenseClient.cs" -Pattern "sealed record"`
**Result**: 1 match at L9 -- `internal sealed record FeatureFlags(`
Both `sealed` and `record` keywords on same line. JS-003 PASS.
**Verdict**: PASS
**Engineer reported**: 1 match at L9
**Cross-check**: MATCH

---

## Supplementary DNA Scans

| Scan | Pattern | Command | Result | Verdict |
|------|---------|---------|--------|---------|
| SCAN-03 (FontFamily) | `FontFamily` | Select-String | 0 matches | PASS |
| SCAN-04 (Hex color) | `#[0-9A-Fa-f]{6}` | Select-String | 0 matches | PASS |
| SCAN-06 (DateTime.Now) | `DateTime\.Now[^U]` | Select-String | 0 matches | PASS |

---

## Contract Item Checklist

Source evidence cross-referenced against `docs/brain/BGTM-1/04-tickets.md` contract.

| # | Contract Item | Status | Evidence |
|---|--------------|--------|---------|
| 1 | FeatureFlags has exactly 7 positional parameters (MultiRule, TrimFlatten, BreakEven, AtrSizing, ClickTrader, MirrorMode, QxGlobalExit) | PASS | L9-16: all 7 present, correct names, correct order |
| 2 | Validate() never returns null (returns FeatureFlags.Starter() as fallback) | PASS | L56-70: 3 exit paths, all return FeatureFlags value; SCAN-03 confirmed 0 `return null` in Validate() |
| 3 | TryRemoteValidate and TryReadCache return nullable FeatureFlags? (private helpers) | PASS | L73 `private static FeatureFlags TryRemoteValidate`, L114 `private static FeatureFlags TryReadCache`; Nullable disabled in csproj (net48 NT8 target) -- nullability by convention is correct |
| 4 | IsExternalInit shim present in System.Runtime.CompilerServices namespace | PASS | L2-5: correct shim before PropTraderTools namespace; comment matches ticket spec verbatim |
| 5 | Starter()/Pro()/Elite()/FromFeatureList() all present | PASS | L18, L21, L24, L27: all four factory methods present with correct signatures |
| 6 | CachePath uses NinjaTrader.Core.Globals.UserDataDir (or test injection hook) | PASS | L42: `internal static string _testCachePath = null;`; L49-53: `CachePath => _testCachePath ?? Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "PropTraderTools", "license_cache.json")` |
| 7 | WriteCache silently swallows all exceptions | PASS | L159-178: `try { ... } catch { }` -- bare catch block swallows all exceptions |
| 8 | No lock() anywhere | PASS | SCAN-01: 0 results |
| 9 | LangVersion updated to 9.0 in csproj | PASS | csproj L19: `<LangVersion>9.0</LangVersion>` |
| 10 | SKM.NET reference present in csproj | PASS | csproj L56-59: `<Reference Include="SKGL.Extension">` with HintPath; csproj L85: `<PackageReference Include="SKGL.Extension" Version="2.0.23" />` |
| 11 | LicenseClient.cs and Tests\BgtmTests.cs compile entries present in csproj | PASS | csproj L98: `<Compile Include="LicenseClient.cs" />`; csproj L144: `<Compile Include="Tests\BgtmTests.cs" />` |

**All 11 contract items: PASS**

---

## xUnit Test Stub Verification

**File**: `src/PropTraderTools/Tests/BgtmTests.cs`
**Read via**: `Get-Content` (file is bobignored)

11 `[Fact]` stubs verified present with correct method names:

| Test Name | Status |
|-----------|--------|
| T_BGTM1_LicenseClient_NullKey_ReturnsStarter | PASS |
| T_BGTM1_LicenseClient_EmptyKey_ReturnsStarter | PASS |
| T_BGTM1_LicenseClient_WhitespaceKey_ReturnsStarter | PASS |
| T_BGTM1_LicenseClient_OfflineCache_HitReturnsCachedFlags | PASS |
| T_BGTM1_LicenseClient_OfflineCache_ExpiredReturnsStarter | PASS |
| T_BGTM1_LicenseClient_WrongKeyCache_ReturnsStarter | PASS |
| T_BGTM1_FeatureFlags_Starter_AllFalse | PASS |
| T_BGTM1_FeatureFlags_Pro_MultiRuleTrimBreakEvenTrue | PASS |
| T_BGTM1_FeatureFlags_Elite_AllTrue | PASS |
| T_BGTM1_FeatureFlags_FromFeatureList_OnlyMultiRule | PASS |
| T_BGTM1_LicenseClient_ValidKey_FromFeatureList | PASS |

All 11 names match ticket contract exactly. Stubs have empty bodies `{ }` (correct -- full implementations per T6). PASS.

---

## JS Rule Compliance (independent check)

| Rule | Requirement | Evidence | Status |
|------|------------|---------|--------|
| JS-001 (no throw in hot paths) | Zero `throw new` in any method | SCAN-02: 0 matches | PASS |
| JS-002 (no return null on public API) | Validate() never returns null | SCAN-03 + source read: 0 `return null` in Validate() | PASS |
| JS-003 (sealed record) | FeatureFlags is `internal sealed record` | SCAN-07: L9 `internal sealed record FeatureFlags(` | PASS |
| JS-021 (no lock) | Zero `lock(` anywhere | SCAN-01: 0 matches | PASS |
| JS-023 (volatile) | N/A for LicenseClient.cs | LicenseClient is stateless static class | N/A |

---

## Architecture Plan Compliance

| Plan Section | Requirement | Status |
|-------------|-------------|--------|
| Section 2 (FeatureFlags) | 7-param sealed record with 4 factory methods | PASS |
| Section 3.1 (Constants/Paths) | ProductId = "PTT_COPIER_V1", CachePath with test injection | PASS |
| Section 3.3 (Validate CYC=4) | 3 branches + base | PASS (independently confirmed CYC=4) |
| Section 3.4 (TryRemoteValidate) | SKM.V3 call, try/catch, returns null on fail | PASS |
| Section 3.5 (ParseSkmResponse) | DataObjects.Name extraction | PASS |
| Section 3.6 (TryReadCache) | File+parse+expiry guards | PASS |
| Section 3.7 (DeserializeCache) | DataContractJsonSerializer, dual catch | PASS |
| Section 3.8 (WriteCache CYC=2) | Swallow all exceptions | PASS |
| Section 3.9 (CacheEntry DTO) | DataContract/DataMember with lowercase Name= overrides | PASS |
| Section 3.10 (SKM.NET ref) | HintPath DLL + PackageReference in csproj | PASS |
| Section 9 (csproj) | LangVersion 9.0, SKGL ref, compile entries | PASS |

---

## Engineer Self-Report Accuracy (Layer 2 vs Layer 3)

| Scan | Engineer (L2) | Verifier (L3) | Match? | Notes |
|------|--------------|--------------|--------|-------|
| SCAN-01 lock() | 0 | 0 | YES | |
| SCAN-02 throw new | 0 | 0 | YES | |
| SCAN-03 return null count | 11 matches (all private) | 10 matches (all private) | EQUIV | Probable 1-line count diff; compliance is identical |
| SCAN-04 ParseSkmResponse CYC | 3 | 4 | MINOR DIFF | foreach adds +1; still <=8, not a violation |
| SCAN-05 ASCII | 0 | 0 | YES | |
| SCAN-06 async void | 0 | 0 | YES | |
| SCAN-07 sealed record | 1 match L9 | 1 match L9 | YES | |

No violations. Minor count discrepancies are non-consequential.

---

## Violations

**NONE**

---

## Final Verdict

**VERIFY_PASS**

All 7 independent scans passed. All 11 contract items confirmed against actual source. No DNA violations. Engineer Layer 2 report is accurate. Implementation faithfully follows the ticket spec and architecture plan.