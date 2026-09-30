# B121-hotfix Verification Report

**Hotfix ID**: B121-hotfix
**Scope**: LicenseClient.cs + FeatureFlags.cs -- NT8 compile-error removal
**Verifier**: ptt-verifier (independent Layer 3 scan)
**Date**: 2026-08-11
**Verdict**: VERIFY_PASS

---

## Summary

All 8 independent scans passed with zero violations. All structural checklist items confirmed.
No DNA rule violations found. CYC <= 8 on every method. The hotfix cleanly removes the
SKGL/SKM dependency, replaces DataContractJsonSerializer with a hand-rolled JSON helper,
and fixes the IReadOnlyList<string> -> List<string> signature mismatch in FeatureFlags.

---

## Scan Results (Layer 3 -- Independent)

| Scan | Pattern | Files | Result | Notes |
|------|---------|-------|--------|-------|
| SCAN-A | `lock\s*\(` | LicenseClient.cs, FeatureFlags.cs | **PASS** (0 results) | No lock() anywhere |
| SCAN-B | `async\s+void\s+\w+` | LicenseClient.cs, FeatureFlags.cs | **PASS** (0 results) | No async void |
| SCAN-C | `SKM\.` | LicenseClient.cs | **PASS** (0 results) | All SKM references removed |
| SCAN-D | `DataContract\|DataMember\|DataContractJson` | LicenseClient.cs | **PASS** (0 results) | Serializer fully removed |
| SCAN-E | `IReadOnlyList` | FeatureFlags.cs | **PASS** (0 results) | Signature corrected to List<string> |
| SCAN-F | `Linq\|Enumerable\.Contains` | FeatureFlags.cs | **PASS** (0 results) | BCL List<T>.Contains used, not LINQ |
| SCAN-G | `[^\x00-\x7F]` | LicenseClient.cs, FeatureFlags.cs | **PASS** (0 results) | Pure ASCII |
| SCAN-H | `return null` | LicenseClient.cs | **PASS** (7 hits, all intentional) | See audit below |

### SCAN-H Detailed Audit (return null -- all intentional sentinel returns)

| Line | Method | Reason |
|------|--------|--------|
| 46 | `TryReadCache` | File does not exist -- no cache, fall through to remote |
| 50 | `TryReadCache` | Null entry or key mismatch -- cache invalid |
| 52 | `TryReadCache` | Cache expired (UtcNow > ExpiresUtc) |
| 59 | `TryReadCache` (catch) | Any IO/parse exception -- treat as cache miss |
| 72 | `DeserializeCache` | Missing required JSON field (key or expires_utc) |
| 83 | `DeserializeCache` (catch) | Any parse exception -- return null to caller |
| 91 | `ExtractJsonString` | JSON field marker not found -- field absent |

**Verdict on SCAN-H**: All 7 are private helper sentinel returns. The public `Validate()` method
(line 20) never returns null -- it falls through to `FeatureFlags.Starter()` on every failure
path. No JS-002 violation.

Note: Line 37 `TryRemoteValidate => null` is not caught by `return null` pattern (it is
`=> null` arrow syntax). This is the intentional stub comment-documented as deferred.

---

## Structural Review Checklist

### LicenseClient.cs

| Item | Status | Evidence |
|------|--------|----------|
| `#if SKGL_PRESENT` block completely gone | PASS | No `#if` preprocessor directives in file |
| `TryRemoteValidate` stub present (returns null, CYC=1) | PASS | Line 37: `private static FeatureFlags TryRemoteValidate(string key) => null;` |
| `CacheEntry` is plain sealed class, no [DataContract]/[DataMember] | PASS | Lines 172-178: plain auto-properties, no attributes |
| `DeserializeCache` uses only System.Text/System.IO/System.DateTime/System.Globalization | PASS | Uses DateTime.Parse + CultureInfo.InvariantCulture + DateTimeStyles.RoundtripKind only |
| `WriteCache` uses only System.Text.StringBuilder/System.IO.File/System.IO.Directory | PASS | StringBuilder, Directory.CreateDirectory, File.WriteAllText, Encoding.UTF8 only |
| `EscapeJson` helper present | PASS | Line 146 |
| `ExtractJsonString` helper present | PASS | Line 87 |
| `ExtractJsonArray` helper present | PASS | Line 98 |
| No new using directives for unavailable NT8 assemblies | PASS | File has ZERO using directives -- all types fully qualified |
| All method CYC <= 8 | PASS | See CYC table below |

### FeatureFlags.cs

| Item | Status | Evidence |
|------|--------|----------|
| `FromFeatureList` parameter is `List<string>` (not `IReadOnlyList`) | PASS | Line 27-28: `System.Collections.Generic.List<string> feats` |
| Body uses `feats.Contains("x")` -- 7 calls, no LINQ | PASS | Lines 30-36: 7 BCL List<T>.Contains calls, no using System.Linq |
| No other changes to FeatureFlags.cs | PASS | File contains only: IsExternalInit shim + FeatureFlags sealed record |

---

## CYC Counts -- LicenseClient.cs (All Methods)

| Method | Annotated CYC | Verified CYC | Status |
|--------|--------------|--------------|--------|
| `Validate(string key)` | 4 | 4 (base=1, 3 if-branches) | PASS (<= 8) |
| `TryRemoteValidate(string key)` | -- | 1 (stub, no branches) | PASS (<= 8) |
| `TryReadCache(string key)` | 5 | 5 (base=1, 4 branches incl. catch) | PASS (<= 8) |
| `DeserializeCache(string json)` | 5 | 4-5 (base=1, if+`\|\|`+catch) | PASS (<= 8) |
| `ExtractJsonString(string, string)` | 3 | 3 (base=1, if + ternary) | PASS (<= 8) |
| `ExtractJsonArray(string, string)` | 5 | 6 (base=1, 3 if-guards+foreach+inner-if) | PASS (<= 8) |
| `WriteCache(string, FeatureFlags)` | 3 | 4 (base=1, for+if+catch) | PASS (<= 8) |
| `EscapeJson(string s)` | 2 | 2 (base=1, null ternary) | PASS (<= 8) |
| `GetFeatureList(FeatureFlags f)` | -- | 8 (base=1, 7 if-branches) | PASS (= 8) |
| `InferTierName(FeatureFlags f)` | 3 | 3 (base=1, 2 if-branches) | PASS (<= 8) |

Note: Minor discrepancies between comment annotations and counted CYC are due to differing
treatment of catch-blocks (some counters add 1, some do not). All methods are clearly <= 8.

---

## DNA Rule Checks (Jane Street Rules Catalog)

| Rule | Description | Result |
|------|-------------|--------|
| JS-021 (P0) | `lock()` banned | PASS -- no lock() anywhere |
| JS-001 (P0) | No throw in hot paths | PASS -- no throw statements anywhere; all errors swallowed via catch or sentinel null |
| JS-002 (P0) | No return null for public API | PASS -- public `Validate()` always returns FeatureFlags (never null) |
| JS-033 (P0) | No async void | PASS -- no async keywords in either file |
| JS-036/037 (P0) | No hot-path heap allocation pattern | PASS -- no ArrayPool-eligible patterns; List is cache-path only |
| JS-010 (P1) | Non-private constructors on signal types | PASS -- LicenseClient is static; CacheEntry is private nested class |
| JS-008/009 (P1) | Mutable struct across threads | PASS -- CacheEntry is a sealed class (reference type), not struct |
| SCAN-02 equivalent | ASCII-only | PASS -- 0 non-ASCII chars |
| NT8: DateTime.Now | Must use DateTime.UtcNow | PASS -- lines 51, 80, 130, 131 all use DateTime.UtcNow |
| NT8: async/await in lifecycle methods | Banned | PASS -- no async/await in either file |

---

## PropTraderTools.csproj Audit

| Check | Result | Evidence |
|-------|--------|----------|
| No new assembly references added for B121-hotfix | PASS | Only pre-existing refs: NinjaTrader.Core, NinjaTrader.Gui, NinjaTrader.Client, NinjaTrader.Custom, SKGL.Extension (conditional) |
| SKGL.Extension reference is conditional | PASS | Line 60: `Condition="Exists('...')"` -- conditional only; does not block compile when absent |
| FeatureFlags.cs in explicit compile list | PASS | Line 101 |
| LicenseClient.cs in explicit compile list | PASS | Line 102 |
| No new PackageReference added | PASS | Only xunit refs (pre-existing) |

---

## Deviations Found

None.

---

## Final Verdict

**VERIFY_PASS**

B121-hotfix removes all SKGL/SKM dependencies (SCAN-C: 0), removes all DataContract
serialization attributes (SCAN-D: 0), replaces with a zero-dependency hand-rolled JSON
helper using only BCL types. FeatureFlags.FromFeatureList parameter corrected from
IReadOnlyList<string> (absent in NT8 runtime) to List<string> (SCAN-E: 0, SCAN-F: 0).
All DNA rules satisfied. All methods CYC <= 8. Zero non-ASCII characters. No lock() usage.
Public API never returns null. The hotfix is production-ready for NT8 F5 compile gate.