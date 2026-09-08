# Ticket 2 Completion Report

TICKET: 2
FINDINGS: C4, C5, C6, C7
FILES: src/PropTraderTools/Features/PttFlatten.cs, PttTrim.cs

---

## Changes Made

### C4 — PttFlatten.cs ResolveOrderParams (lines 182-184)
Added `buffer > 0` as the first condition in `useLimitOrder` to prevent placing a
limit order at the exact touch price when buffer is zero.

Before:
```csharp
bool useLimitOrder =
    tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```
After:
```csharp
bool useLimitOrder =
    buffer > 0
    && tickSize > 0.0
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0);
```
XML doc updated: CYC=5 -> CYC=6, added C4 rationale line.

### C5 — PttFlatten.cs FindPositionLocal (lines 204-206)
Changed reference comparison to FullName string comparison to handle NT8 supplying
distinct Instrument instances for the same contract after reconnect.

Before:
```csharp
if (p.Instrument == instr)
```
After:
```csharp
if (p.Instrument?.FullName == instr?.FullName)
```
XML doc updated to mention FullName comparison and C5 rationale.

### C6 — PttTrim.cs ResolveOrderParams (lines 192-194)
Same fix as C4 applied to PttTrim.cs. Added `buffer > 0` as first condition.
XML doc updated: CYC=5 -> CYC=6, added C6 rationale line.

### C7 — PttTrim.cs FindPositionLocal (lines 214-215)
Same fix as C5 applied to PttTrim.cs. Changed to FullName comparison.
XML doc updated to mention FullName comparison and C7 rationale.

---

## Scan Results

BUILD: 0 errors / 0 warnings (pre-existing SA/CS warnings in other files unchanged)
  Command: dotnet build C:\WSGTA\ptt-features\Linting.csproj
  Result:  Build succeeded. 0 Error(s)

LIZARD CCN>8: none
  Command: lizard C:\WSGTA\ptt-features\src\PropTraderTools -x */bin/* -x */obj/* --CCN 8
  Result:  No thresholds exceeded (cyclomatic_complexity > 8 or length > 1000 ...)
  PttFlatten methods (max CCN=6): ResolveOrderParams=6, FindPositionLocal=6,
           FlattenPositionLocal=6, Execute=3
  PttTrim methods (max CCN=6):    ResolveOrderParams=6, FindPositionLocal=6,
           TrimPositionLocal=6, Execute=3

LOCK SCAN: 0 matches
  Command: Select-String -Path ...PttFlatten.cs,...PttTrim.cs -Pattern "lock\("
  Result:  No output (0 matches)

ASCII SCAN: 0 matches
  Command: Select-String -Path ...PttFlatten.cs,...PttTrim.cs -Pattern "[^\x00-\x7F]" -Encoding UTF8
  Result:  No output (0 matches)

REF-COMPARE SCAN: 0 matches
  Command: Select-String -Path ...PttFlatten.cs,...PttTrim.cs -Pattern "== instr\s*[^?]|== instr$"
  Result:  No output (0 matches)
  Note:    Naive pattern "== instr\b" hits FullName lines (false positive). Refined pattern
           "== instr\s*[^?]|== instr$" confirms true reference comparisons are gone.

---

STATUS: BUILD_PASS
