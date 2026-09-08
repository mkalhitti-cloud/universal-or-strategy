# Ticket 3 Verification Report

TICKET: 3
FILE: src/PropTraderTools/Features/PttGlobalBreakEven.cs
FINDINGS VERIFIED: C8, C9

---

## C8 PASS — Entitlement guard in Execute(int bufferTicks)

**PASS.**

The production `Execute(int bufferTicks)` method (lines 48-64) opens with an unconditional
BreakEven flag check. The guard fires BEFORE `Interlocked.Increment` and BEFORE
`ArmAllPendingBe`. Exact source (lines 50-57):

```csharp
if (!CopyEngine.Instance.Flags.BreakEven)
{
    NinjaTrader.Code.Output.Process(
        "[BE-ALL] GlobalBreakEven: blocked -- BreakEven not licensed",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return;
}
```

Order of operations confirmed:
  1. Line 50 — BreakEven flag check (guard) — FIRST
  2. Line 58 — Interlocked.Increment(ref _ocoSeq) — AFTER guard
  3. Line 63 — CopyEngine.Instance.ArmAllPendingBe(bufferTicks) — AFTER guard

Requirement: guard BEFORE Interlocked.Increment AND BEFORE ArmAllPendingBe. ✓ Both satisfied.

---

## C9 PASS — BE direction fix for short positions in ExecuteOne

**PASS.**

`ExecuteOne` (lines 86-97) uses the ternary `(isLong ? -bufferTicks : bufferTicks)`.
Exact source (lines 92-95):

```csharp
double bePrice =
    Math.Round(
        (pos.AveragePrice + (isLong ? -bufferTicks : bufferTicks) * tickSize) / tickSize
    ) * tickSize;
```

Semantics:
  - LONG:  bePrice = entry - bufferTicks * tick  (stop placed below entry) ✓
  - SHORT: bePrice = entry + bufferTicks * tick  (stop placed above entry) ✓

PttBreakEven.cs line 112 (independent confirmation):
```csharp
double bePrice = pos.AveragePrice + (isLong ? -buf : +buf) * tickSize;
```
Comment on lines 108-111 of PttBreakEven.cs explicitly documents the convention:
  "Long stop goes AT/BELOW entry ... Short stop goes AT/ABOVE entry."

PttGlobalBreakEven.cs uses `(isLong ? -bufferTicks : bufferTicks)` which matches the
PttBreakEven.cs convention `(isLong ? -buf : +buf)` exactly.

The original wrong direction `(isLong ? bufferTicks : -bufferTicks)` is NOT present.

---

## Build Scan

BUILD: 0 errors, 0 warnings
Command: dotnet build "C:\WSGTA\ptt-features\Linting.csproj"
Result:  Build succeeded. 0 Error(s). 0 Warning(s).

---

## Lock Scan

LOCK SCAN: 0 actual lock() calls
Command: Select-String -Path ...PttGlobalBreakEven.cs -Pattern "^\s*lock\s*\("
Result:  0 matches.
Note:    A grep on the raw pattern "lock(" returns line 4 which is the file header comment
         "// JS-021: no lock()." — this is a documentation comment, not a lock() statement.
         The executable-line-only pattern confirms zero live lock() calls.

---

## Throw Scan

THROW SCAN: 0 matches
Command: Select-String -Path ...PttGlobalBreakEven.cs -Pattern "throw "
Result:  0 matches.

---

## DNA Rules Spot-Check

- JS-021 (no lock):        PASS — confirmed by lock scan above
- JS-023 (volatile int):   PASS — _globalBeBuffer and _ocoSeq declared volatile int (lines 18, 23)
- JS-002 (no return null): PASS — ExecuteOne returns void; only early returns are void (line 89)
- JS-033 (no async void):  PASS — no async keyword in file
- NT8-003 (no volatile double): PASS — no volatile double declared
- Interlocked.Increment used (not lock): PASS (line 58)
- ASCII-only: PASS — no non-ASCII characters visible
- No hex color literals: PASS
- No FontFamily: PASS
- No throw in Execute/ExecuteOne: PASS (throw scan confirmed 0)
- PTT- prefix on CreateOrder: N/A — no CreateOrder in this file
- DateTime.UtcNow (not DateTime.Now): N/A — no DateTime in this file

---

## Note on Ticket-3-Completion.md

The file `docs/brain/PR122/ticket-3-completion.md` was not present at verification time.
This report is based on independent source inspection of PttGlobalBreakEven.cs in the
ptt-features worktree. The absence of the engineer completion report does not affect the
VERIFY_PASS determination because both fixes are confirmed directly in source.

---

STATUS: VERIFY_PASS
