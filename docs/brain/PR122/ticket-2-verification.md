# Ticket 2 Verification Report

TICKET: 2
FINDINGS VERIFIED: C4, C5, C6, C7
VERIFIER: ptt-verifier (independent Layer 3)
WORKTREE: C:\WSGTA\ptt-features (READ ONLY)
FILES:
  src/PropTraderTools/Features/PttFlatten.cs
  src/PropTraderTools/Features/PttTrim.cs

---

## C4 PASS

**Finding**: PttFlatten.cs ResolveOrderParams buffer guard
**Expected**: `buffer > 0` as a condition in `useLimitOrder` boolean
**Evidence** (line 183-186 of PttFlatten.cs):
```csharp
bool useLimitOrder =
    buffer > 0 // (1)
    && tickSize > 0.0 // (2)
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0); // (3)(4)
```
`buffer > 0` is the first (and most restrictive) condition. Fix confirmed.

---

## C5 PASS

**Finding**: PttFlatten.cs FindPositionLocal FullName comparison
**Expected**: `p.Instrument?.FullName == instr?.FullName`
**Evidence** (line 212 of PttFlatten.cs):
```csharp
if (p.Instrument?.FullName == instr?.FullName)
```
Reference comparison `p.Instrument == instr` is gone. FullName comparison confirmed.

---

## C6 PASS

**Finding**: PttTrim.cs ResolveOrderParams buffer guard
**Expected**: `buffer > 0` as a condition in `useLimitOrder` boolean
**Evidence** (line 193-196 of PttTrim.cs):
```csharp
bool useLimitOrder =
    buffer > 0 // (1)
    && tickSize > 0.0 // (2)
    && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0); // (3)(4)
```
`buffer > 0` is the first condition. Fix confirmed.

---

## C7 PASS

**Finding**: PttTrim.cs FindPositionLocal FullName comparison
**Expected**: `p.Instrument?.FullName == instr?.FullName`
**Evidence** (line 222 of PttTrim.cs):
```csharp
if (p.Instrument?.FullName == instr?.FullName)
```
Reference comparison `p.Instrument == instr` is gone. FullName comparison confirmed.

---

## BUILD: 0 errors

Command: `dotnet build C:\WSGTA\ptt-features\Linting.csproj`
Result: Build succeeded. 0 Warning(s). 0 Error(s).
Note: PropTraderTools.csproj does not exist as a standalone file in this worktree;
      Linting.csproj is the project that builds the src/ folder (matches completion report).

---

## REF-COMPARE SCAN: 0 true reference comparisons

Command (naive): `Select-String -Pattern "== instr\b"`
Result: 2 hits -- both are FALSE POSITIVES from the FullName fix lines
  PttFlatten.cs:212: `if (p.Instrument?.FullName == instr?.FullName)`
  PttTrim.cs:222:    `if (p.Instrument?.FullName == instr?.FullName)`
  The word "instr" appears in "instr?.FullName" -- this is the correct fixed code.

Command (refined): `Select-String -Pattern "== instr\s*[^?]|== instr$"`
Result: 0 matches -- no raw reference comparisons remain. CLEAN.

---

## LOCK SCAN: 0 matches

Command: `Select-String -Path PttFlatten.cs, PttTrim.cs -Pattern "lock\("`
Result: No output (0 matches). JS-021 compliant.

---

## DNA RULES CHECK

| Rule    | Check                              | Result |
|---------|------------------------------------|--------|
| JS-021  | No lock() in either file           | PASS   |
| JS-001  | No throw new Exception in methods  | PASS   |
| JS-002  | No return null for non-nullable    | PASS   |
| JS-033  | No async void                      | PASS   |
| NT8-014 | CreateOrder signal "PTT-Flatten" / "PTT-Trim" | PASS |
| NT8-013 | DateTime.MaxValue used             | PASS   |
| CYC<=8  | All methods CYC<=6 per docstrings  | PASS   |

---

STATUS: VERIFY_PASS