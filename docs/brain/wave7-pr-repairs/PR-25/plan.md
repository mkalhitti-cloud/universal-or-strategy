# PR #25 Repair Plan
**Branch**: `wave7/pr6-s6-kernel-infra`
**Bugs**: 2 confirmed P0 logic bugs (4/4 and 2/4 reviewer consensus)
**Status**: Plan only — do NOT touch src/

---

## Bug 1 — `Perf.LogBuffer.cs`: Literal `{` silently dropped

### 1. Root Cause
When `TryGetSingleDigitArg` returns `false` (the brace at `formatPos` is not a
valid `{N}` placeholder), `TryExpandPlaceholder` returns `1`; the caller
`FormatInternal` then executes `formatPos += 1; continue`, advancing past the
`{` character without ever writing it to `_buffer`, so any bare `{` in a log
format string is silently discarded from output.

### 2. Exact Old Code
**File**: [`src/V12_002.Perf.LogBuffer.cs`](../../../../src/V12_002.Perf.LogBuffer.cs:92)  
**Method**: `TryExpandPlaceholder` (line 92)

```csharp
private static int TryExpandPlaceholder(string format, int formatPos, object[] args, ref int bufferPos)
{
    if (HasFormatSpecifier(format, formatPos))
        return -1;

    string argStr;
    if (!TryGetSingleDigitArg(format, formatPos, args, out argStr))
        return 1;   // ← BUG: caller advances formatPos but writes nothing to _buffer

    if (bufferPos + argStr.Length >= _buffer.Length)
        return -1;

    argStr.CopyTo(0, _buffer, bufferPos, argStr.Length);
    bufferPos += argStr.Length;
    return 3;
}
```

### 3. Exact New Code
Write the literal `{` into `_buffer` before returning `1`. No new branches, no
new allocations — just a buffer write that was always the correct intent.

```csharp
private static int TryExpandPlaceholder(string format, int formatPos, object[] args, ref int bufferPos)
{
    if (HasFormatSpecifier(format, formatPos))
        return -1;

    string argStr;
    if (!TryGetSingleDigitArg(format, formatPos, args, out argStr))
    {
        // Literal brace — write it to buffer before advancing past it.
        if (bufferPos >= _buffer.Length)
            return -1;
        _buffer[bufferPos++] = OpenBrace;
        return 1;
    }

    if (bufferPos + argStr.Length >= _buffer.Length)
        return -1;

    argStr.CopyTo(0, _buffer, bufferPos, argStr.Length);
    bufferPos += argStr.Length;
    return 3;
}
```

### 4. Jane Street Rationale
**OKF document**: `docs/intel/jane-street/how-to-build-an-exchange.md`  
**Pattern**: `correctness by construction`  
> "ECN matching engines as deterministic state machines — illegal states must be
> unrepresentable."

Silent data loss in a formatting buffer is an illegal silent-drop state.
Returning `1` (advance) without writing is a contract violation: the return
value semantics document the count of format characters consumed, but writing
to the buffer is the implied side-effect. The fix makes the two invariants
consistent again.

**OKF document**: `docs/intel/jane-street/microsecond-eternity.md`  
**Pattern**: `zero_alloc`  
> "Use preallocated buffers; BANNED on hot path: any allocation."

The fix writes one `char` to the already-allocated `_buffer[bufferPos]` and
increments `bufferPos`. Zero new allocations, zero new heap objects.
The existing overflow guard (`bufferPos >= _buffer.Length`) is reused — no
additional branches beyond what `TryGetSingleDigitArg` already returned.

### 5. Edge Cases to Verify
| Input | Expected output after fix |
|---|---|
| `"price={0}"` | `"price=42"` (existing happy path, unchanged) |
| `"{{warn}}"` | `"{warn}"` — both `{` chars written literally (each becomes one write) |
| `"{}"` (empty braces) | `"{}"` — `TryGetSingleDigitArg` returns false (no digit at +1), brace written, `}` written as normal literal on next iteration |
| `"{10}"` (two-digit index) | `"{"` written, then `1`, `0`, `}` as literals — graceful, no crash |
| Buffer-full at the brace | Returns `-1`, falls back to `string.Format()` — existing overflow path |
| `"{0}"` with args empty | `TryGetSingleDigitArg` returns false (argIndex >= args.Length), `{` written literally — matches .NET string.Format exception-free fallback intent |

### 6. CYC Delta
| Method | Old CYC | New CYC | Delta |
|---|---|---|---|
| `TryExpandPlaceholder` | 4 | 5 | +1 |
| `FormatInternal` | 4 | 4 | 0 |

New CYC = **5** — well within the V12 hard limit of ≤ 8.  
The +1 comes from the single new overflow guard `if (bufferPos >= _buffer.Length)`
in the false-branch. It is the minimal necessary guard to preserve the zero-alloc
contract (an unguarded write past the end would be a buffer overrun).

---

## Bug 2 — `DrawingHelpers.cs`: `ResolveTimeZone` missing `"UTC"` case

### 1. Root Cause
The wave 7 extraction of `ResolveTimeZone` copied the four US-zone `case`
branches from the original inline `switch` in `ConvertToSelectedTimeZone` but
omitted the `case "UTC": targetZone = TimeZoneInfo.Utc` branch; as a result,
when the user selects UTC the `default` arm silently returns
`TimeZoneInfo.Local`, causing OR boxes to be drawn at the local-machine time
instead of UTC.

### 2. Exact Old Code
**File**: [`src/V12_002.DrawingHelpers.cs`](../../../../src/V12_002.DrawingHelpers.cs:74)  
**Method**: `ResolveTimeZone` (line 74)

```csharp
private static TimeZoneInfo ResolveTimeZone(string selectedTimeZone)
{
    switch (selectedTimeZone)
    {
        case "Eastern":
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        case "Central":
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        case "Mountain":
            return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
        case "Pacific":
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        default:
            return TimeZoneInfo.Local;   // ← UTC falls here — wrong
    }
}
```

### 3. Exact New Code
Add the single missing `case "UTC"` before `default`. No other change.
The original inline switch (line 164 of `ConvertToSelectedTimeZone`) confirms
the value: `case "UTC": targetZone = TimeZoneInfo.Utc`.

```csharp
private static TimeZoneInfo ResolveTimeZone(string selectedTimeZone)
{
    switch (selectedTimeZone)
    {
        case "Eastern":
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        case "Central":
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        case "Mountain":
            return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
        case "Pacific":
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        case "UTC":
            return TimeZoneInfo.Utc;
        default:
            return TimeZoneInfo.Local;
    }
}
```

### 4. Jane Street Rationale
**OKF document**: `docs/intel/jane-street/how-to-build-an-exchange.md`  
**Pattern**: `determinism`  
> "Use tick timestamps instead of system clocks to ensure history replayability.
> BANNED: DateTime.Now (non-deterministic)"

`TimeZoneInfo.Local` is machine-dependent and therefore non-deterministic across
systems — the same session replay on a machine in a different timezone produces
different OR box positions. `TimeZoneInfo.Utc` is the canonical, deterministic
clock source mandated by the exchange-architecture pattern. The fix restores the
invariant that was deliberately present in the original inline code.

### 5. Edge Cases to Verify
| Input | Expected output after fix |
|---|---|
| `selectedTimeZone = "UTC"` | Returns `TimeZoneInfo.Utc` (the regression fix) |
| `selectedTimeZone = "Eastern"` | Returns `Eastern Standard Time` (unchanged) |
| `selectedTimeZone = "Pacific"` | Returns `Pacific Standard Time` (unchanged) |
| `selectedTimeZone = ""` (empty) | `default` → `TimeZoneInfo.Local` (unchanged behaviour) |
| `selectedTimeZone = "utc"` (lowercase) | `default` → `TimeZoneInfo.Local` — existing string comparison is case-sensitive; callers must pass `"UTC"` exactly as documented |
| `selectedTimeZone = null` | `switch` on null falls through to `default` → `TimeZoneInfo.Local`; no NullReferenceException in C# switch-on-string |

### 6. CYC Delta
| Method | Old CYC | New CYC | Delta |
|---|---|---|---|
| `ResolveTimeZone` | 5 | 6 | +1 |

New CYC = **6** — within the V12 hard limit of ≤ 8.  
The +1 is the single new `case "UTC"` branch. It is the minimum possible change.

---

## Agent Tracking

| Field | Value |
|---|---|
| Epic | wave7/pr6-s6-kernel-infra — PR #25 |
| Phase | 2 (Architecture / Repair Plan) |
| Agent | V12 Architecture Planner |
| OKF docs read | `how-to-build-an-exchange.md`, `microsecond-eternity.md` |
| Source files read | `src/V12_002.Perf.LogBuffer.cs` (183 lines), `src/V12_002.DrawingHelpers.cs` (lines 74–89, 145–170) |
| src/ modified | **NO** — plan only |
| CYC after fixes | LogBuffer `TryExpandPlaceholder` = 5, DrawingHelpers `ResolveTimeZone` = 6 (both ≤ 8) |
| Allocations introduced | **ZERO** (char write to pre-allocated `_buffer`) |
| Timestamp | 2026-06-25 |
