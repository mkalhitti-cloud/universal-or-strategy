# PR #22 Fix Queue — wave7/pr3-s1-sima-core
# S1 SIMA Core — 3 files
# Reviewers: Qodo, CodeAnt, CodeRabbit, Gemini

---

## [LOGIC-BUG] P0 — SIMA.Lifecycle.cs: wrong dict key for T1_/T2_ prefixes

**File**: `src/V12_002.SIMA.Lifecycle.cs`
**Method**: `GetAdoptionDictionaryKey` (line ~1280)
**Reviewers**: Qodo, CodeAnt, CodeRabbit (3/4)

**Symptom**: Comment says "T1_, T2_, etc. are 2 chars" but T1_ is 3 chars.
`name.Substring(2)` on `"T1_Signal"` produces `"_Signal"` (with leading
underscore) instead of `"Signal"`. Target orders are inserted into
tracking dictionaries under wrong keys — order reconciliation silently fails.

Original code at line ~1048–1058 used `Substring(3)` for T1_/T2_/T3_/T4_/T5_.
The extracted helper consolidated to `Substring(2)` — incorrect for 3-char prefixes.

**Source lines** (current buggy state):
```csharp
/// Stop_ prefix is 5 chars; all other prefixes (T1_, T2_, etc.) are 2 chars.
private static string GetAdoptionDictionaryKey(string name)
{
    return name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase)
        ? name.Substring(5)
        : name.Substring(2);   // ← T1_ is 3 chars, produces "_Signal" not "Signal"
}
```

**OKF**: how-to-build-an-exchange.md → `correctness by construction` /
`one_in_flight` (order tracking must be exact — wrong key = ghost orders)

---

## [DNA] P1 — SIMA.Lifecycle.cs: underscore-prefix local variables

**File**: `src/V12_002.SIMA.Lifecycle.cs`
**Lines**: ~115-128

**Issue**: Local variables `_sbIdx` and `_expectedKey` use underscore prefix.
V12 DNA mandates camelCase for locals (underscore prefix reserved for private
instance fields only).

**Fix**: Rename `_sbIdx` → `sbIdx`, `_expectedKey` → `expectedKey` throughout
the method scope.

---

## [DNA] P1 — SIMA.Lifecycle.cs: underscore in method names

**Issue**: Methods named `EmergencyFlatten_*` contain underscores.
V12 DNA: method names must be PascalCase, no underscores.

**Fix**: Rename `EmergencyFlatten_X` → `EmergencyFlattenX` (PascalCase).
Verify all call sites updated.

---

## STATUS
- [ ] LOGIC-BUG: GetAdoptionDictionaryKey Substring(2→3)
- [ ] DNA: _sbIdx / _expectedKey rename
- [ ] DNA: EmergencyFlatten_ method rename
