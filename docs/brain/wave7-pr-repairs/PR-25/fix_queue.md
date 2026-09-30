# PR #25 Fix Queue — wave7/pr6-s6-kernel-infra
# S6 Kernel Infrastructure — 4 files
# Reviewers: Sourcery, Gemini, CodeAnt, Cubic

---

## [LOGIC-BUG] P0 — Perf.LogBuffer.cs: literal { char silently dropped

**File**: `src/V12_002.Perf.LogBuffer.cs`
**Method**: `TryExpandPlaceholder`
**Reviewers**: Sourcery, Gemini, CodeAnt, Cubic (4/4)

**Symptom**: When `TryGetSingleDigitArg` returns false (brace is not a valid
placeholder), `TryExpandPlaceholder` returns `1`. The caller `FormatInternal`
does `formatPos += 1; continue` — advancing past the `{` without ever writing
it to `_buffer`. Any `{` in a log string that is not `{0}`..`{9}` is silently
dropped from output.

**Source lines** (current buggy state):
```csharp
if (!TryGetSingleDigitArg(format, formatPos, args, out argStr))
    return 1;   // ← caller advances formatPos but writes nothing to _buffer
```

**OKF**: how-to-build-an-exchange.md → `correctness by construction` /
microsecond.md → `zero_alloc` (fix must not allocate)

---

## [LOGIC-BUG] P0 — DrawingHelpers.cs: ResolveTimeZone missing UTC case

**File**: `src/V12_002.DrawingHelpers.cs`
**Method**: `ResolveTimeZone`
**Reviewers**: Gemini, Cubic (2/4)

**Symptom**: The extracted `ResolveTimeZone` switch has no `"UTC"` case.
Falls through to `default: return TimeZoneInfo.Local`. Users with UTC selected
see OR boxes drawn at local-system time, not UTC — silent wrong-time rendering.
Original inline code at line ~164 had `case "UTC": targetZone = TimeZoneInfo.Utc`.
The wave 7 extraction regressed this.

**Source lines** (current buggy state):
```csharp
private static TimeZoneInfo ResolveTimeZone(string selectedTimeZone)
{
    switch (selectedTimeZone)
    {
        case "Eastern": return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        case "Central": return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        case "Mountain": return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
        case "Pacific":  return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        default:         return TimeZoneInfo.Local;   // ← UTC falls here, wrong
    }
}
```

**OKF**: how-to-build-an-exchange.md → `determinism` ("use consistent clock
source to ensure replayability")

---

## STATUS
- [ ] LOGIC-BUG: LogBuffer { dropped
- [ ] LOGIC-BUG: DrawingHelpers UTC case
