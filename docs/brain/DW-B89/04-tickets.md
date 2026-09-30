# DW-B89 -- BE-ALL OCO Reuse + Silent Stop Rejection
## Ticket File: 04-tickets.md
**Status**: TICKETS_COMPLETE
**Phase**: Phase 3 (Ticket Generation)
**Author**: ptt-architect
**Date**: 2026-08-23
**Plan**: docs/brain/DW-B89/02-architecture-plan.md (REVIEW_PASS)

---

## T1 -- CopyEngine.cs Seed Fix

### Ticket Metadata

| Field | Value |
|-------|-------|
| **Ticket ID** | T1 |
| **Title** | CopyEngine._mstbeOcoSeq: XOR entropy seed to prevent OCO ID reuse after recompile |
| **Spec Requirements** | DW-B89-01 (OCO ID reuse root cause fix) |
| **File** | `src/PropTraderTools/CopyEngine.cs` |
| **Risk** | Low -- single field initializer change, no signature change |

---

### T1 -- Exact Change

**Location**: `src/PropTraderTools/CopyEngine.cs`, line 205

**BEFORE** (current line 205):
```csharp
private volatile int _mstbeOcoSeq = Environment.TickCount;
```

**AFTER** (replacement line 205):
```csharp
private volatile int _mstbeOcoSeq = Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));
```

**Comment block update** (lines 199-204, update seed description only, keep existing context):

**BEFORE** (lines 199-204):
```
// HOTFIX-MSTBE-OCO-TICKSEED-01: seed from Environment.TickCount (ms since OS boot).
// NT8 keeps cancelled OCO IDs for the entire NT8 session. When NT8 recompiles an AddOn
// within a running session, CopyEngine is GC'd and re-created -- if seeded at 0 the counter
// restarts at 1 and immediately collides with pre-recompile OCO IDs still in NT8 memory.
// Environment.TickCount advances even during recompile so post-recompile seq starts far above
// any value used in the prior run. JS-023: volatile int. TickCount returns int -- no cast needed.
```

**AFTER** (lines 199-204):
```
// DW-B89-01 SEED FIX: XOR Environment.TickCount with low 31 bits of DateTime.UtcNow.Ticks.
// NT8 keeps cancelled OCO IDs for the entire NT8 session. When NT8 recompiles an AddOn
// within a running session, CopyEngine is GC'd and re-created. TickCount alone can repeat
// within the same millisecond on fast recompile. XOR with Ticks (100ns resolution) ensures
// post-recompile seed is statistically unique. Math.Abs: XOR can set sign bit; wraps safely.
// JS-023: volatile int. Interlocked.Increment in NextBeOcoSeq() unchanged. No lock added.
```

---

### T1 -- Method Signatures

No method signatures change.

```csharp
// Unchanged (CopyEngine inner class):
internal int NextBeOcoSeq() => System.Threading.Interlocked.Increment(ref _mstbeOcoSeq);
```

---

### T1 -- JS Rule Constraints

| Rule | Requirement | Verification |
|------|-------------|--------------|
| JS-021 (P0) | No `lock()` anywhere | No lock added. `volatile` + `Interlocked` pattern preserved. |
| JS-023 (P1) | Use atomic primitives for simple shared state | `volatile int` field preserved. `Interlocked.Increment` in `NextBeOcoSeq()` unchanged. XOR seed is a field initializer expression -- no atomic operation needed at init time (single-threaded construction). |
| JS-033 (P0) | No `async void` (non-event-handler) | No async code touched. |
| ASCII-only | No Unicode in identifiers or string literals | `Math.Abs(...)` is pure ASCII. |
| DateTime.Now ban | Use `DateTime.UtcNow` only | XOR formula uses `DateTime.UtcNow.Ticks`. `DateTime.Now` is banned and must not appear. |

---

### T1 -- NT8 Constraints

| Constraint | Requirement |
|------------|-------------|
| NT8-007 | Not touched in this ticket. |
| NT8-049 | Not touched in this ticket. |
| NT8-013 | Not touched in this ticket. |
| NT8-014 | Not touched in this ticket. |

---

### T1 -- CYC Analysis

| Method | CYC Before | CYC After | Notes |
|--------|-----------|-----------|-------|
| `NextBeOcoSeq()` | 1 | 1 | Single `Interlocked.Increment` expression. Unchanged. |
| `_mstbeOcoSeq` field init | N/A | N/A | Initializer expression; not a method. Math.Abs + XOR + cast = no branches. |

---

### T1 -- xUnit Tests

No new test is written in T1. The entropy improvement is indirectly validated by T3's
`T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding` test which calls `NextBeOcoSeq()`.

---

### T1 -- 7-Scan Checklist (Engineer Contract)

The engineer MUST complete all applicable scans and record pass/fail before marking T1 complete.

| Scan | Command | Required Result | Status |
|------|---------|-----------------|--------|
| SCAN-01 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings on changed files | [ ] |
| SCAN-02 | Manual CYC check of `NextBeOcoSeq()` | CYC = 1 (unchanged) | [ ] |
| SCAN-03 | `grep -r "lock(" src/PropTraderTools/` | 0 matches | [ ] |
| SCAN-04 | `grep -rn "async void " src/PropTraderTools/` | 0 matches in new or modified code | [ ] |
| SCAN-05 | `grep -r ".ToString(\"D5\")" src/PropTraderTools/Features/` | Not applicable to CopyEngine.cs; verify 0 new D5 introductions | [ ] |
| SCAN-06 | `grep -r "catch { /\* non-fatal \*/ }" src/PropTraderTools/Features/PttBreakEvenSwap.cs` | Not applicable to this ticket | [ ] |
| SCAN-07 | ASCII-only check on `src/PropTraderTools/CopyEngine.cs` (changed lines only) | 0 non-ASCII characters in identifiers or string literals | [ ] |

**Primary scans for T1**: SCAN-01, SCAN-03, SCAN-07.
SCAN-02 and SCAN-04 are included as baseline hygiene but have no new code to verify.

---

## T2 -- PttBreakEvenSwap.cs Full Change Set

### Ticket Metadata

| Field | Value |
|-------|-------|
| **Ticket ID** | T2 |
| **Title** | PttBreakEvenSwap.cs: D7 format, catch logging, IsStopPriceSubmittable guard |
| **Spec Requirements** | DW-B89-01 (D7 format string, Change 1), DW-B89-02 (catch logging Change 2, submittability guard Changes 3-5) |
| **File** | `src/PropTraderTools/Features/PttBreakEvenSwap.cs` |
| **Risk** | Medium -- multiple concurrent changes to Execute(). CYC budget must be verified before close. |

---

### T2 -- Change 1: D5 -> D7 format string

**Location**: `src/PropTraderTools/Features/PttBreakEvenSwap.cs`, line 84

**BEFORE**:
```csharp
+ "-" + seq.ToString("D5") + "-" + i;
```

**AFTER**:
```csharp
+ "-" + seq.ToString("D7") + "-" + i;
```

---

### T2 -- Change 2: Replace all 3 bare catch blocks

There are exactly **3** bare `catch { /* non-fatal */ }` blocks in the file (lines 73, 101, 118 of the current source). Each must be replaced with the identical logging catch.

**BEFORE** (all 3 occurrences):
```csharp
catch { /* non-fatal */ }
```

**AFTER** (all 3 occurrences, identical replacement):
```csharp
catch (Exception ex)
{
    NinjaTrader.Code.Output.Process(
        "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**Note**: The `ex` variable must be referenced in the body (it is, via `ex.Message`). No unused variable warning will be generated.

---

### T2 -- Change 3: Add IsStopPriceSubmittable private static helper

**Location**: Insert the following method IMMEDIATELY BEFORE the `Execute()` method declaration (i.e., before the `internal static void Execute(` line).

```csharp
private static bool IsStopPriceSubmittable(
    Instrument instr, bool isLong, double stopPrice)
{
    if (isLong) return true;
    double ask = instr.MarketData?.Ask?.Price ?? 0.0;
    if (ask == 0.0) return true;
    return stopPrice >= ask;
}
```

**CYC for IsStopPriceSubmittable**:

| # | Branch |
|---|--------|
| 1 | `if (isLong) return true` |
| 2 | `if (ask == 0.0) return true` |
| -- | `return stopPrice >= ask` -- comparison expression, not a branch |

**CYC = 2**. Limit: <= 3. PASS.

**Advisory A1 from plan-review**: If Lizard reports CYC > 3 due to null-conditional chain
(`?.`), refactor `instr.MarketData?.Ask?.Price ?? 0.0` to explicit null checks:
```csharp
double ask = 0.0;
if (instr.MarketData != null && instr.MarketData.Ask != null)
    ask = instr.MarketData.Ask.Price;
```
This is a purely mechanical refactor with no behavioral change. Apply only if Lizard flags it.

---

### T2 -- Change 4: IsStopPriceSubmittable guard in with-targets for-loop

**Location**: Inside the for-loop body (currently lines 86-101), replace the stop-submit try/catch block.

**BEFORE** (stop-submit block inside for-loop):
```csharp
// Submit PTT-BE-Stop-{i+1}: StopMarket for this tranche qty.
try
{
    var sOrd = acc.CreateOrder(
        instr, stopDir, OrderType.StopMarket, OrderEntry.Manual,
        TimeInForce.Gtc, t.Qty,
        0,                                          // arg6: limitPrice=0  (NT8-049)
        newStop,                                    // arg7: stopPrice     (NT8-049)
        ocoId_i,                                    // arg8: OCO pair i
        "PTT-BE-Stop-" + (i + 1),                 // arg9: signal name   (NT8-014)
        DateTime.MaxValue,                          // arg10: GTC          (NT8-013)
        (NinjaTrader.Cbi.CustomOrder)null);         // arg11: cast         (NT8-007)
    if (sOrd != null)
        acc.Submit(new[] { sOrd });
}
catch { /* non-fatal */ }
```

**AFTER** (stop-submit block inside for-loop, `if (sOrd != null)` guard REMOVED, IsStopPriceSubmittable guard ADDED):
```csharp
// Submit PTT-BE-Stop-{i+1}: StopMarket for this tranche qty.
if (IsStopPriceSubmittable(instr, isLong, newStop))
{
    try
    {
        var sOrd = acc.CreateOrder(
            instr, stopDir, OrderType.StopMarket, OrderEntry.Manual,
            TimeInForce.Gtc, t.Qty,
            0,                                          // arg6: limitPrice=0  (NT8-049)
            newStop,                                    // arg7: stopPrice     (NT8-049)
            ocoId_i,                                    // arg8: OCO pair i
            "PTT-BE-Stop-" + (i + 1),                 // arg9: signal name   (NT8-014)
            DateTime.MaxValue,                          // arg10: GTC          (NT8-013)
            (NinjaTrader.Cbi.CustomOrder)null);         // arg11: cast         (NT8-007)
        acc.Submit(new[] { sOrd });
    }
    catch (Exception ex)
    {
        NinjaTrader.Code.Output.Process(
            "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    }
}
else
{
    NinjaTrader.Code.Output.Process(
        "[BE-ERR] " + acc.Name + " PTT-BE-Stop-" + (i + 1)
            + " stop below market @ " + newStop + " -- skipping tranche",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**CRITICAL**: The `if (sOrd != null)` guard is REMOVED. The catch block now handles any NullReferenceException from `CreateOrder`. This is the CYC exchange that keeps Execute() at CYC = 8.

**The target-submit block** (currently lines 103-118, with `if (tOrd != null)`) is **UNCHANGED** in structure -- it keeps its null guard (`if (tOrd != null)`) because a Limit order with limitPrice=0 would be catastrophic. Only replace its bare `catch { /* non-fatal */ }` with the logging catch per Change 2.

---

### T2 -- Change 5: IsStopPriceSubmittable guard in 0-targets bare-stop path

**Location**: The 0-targets branch (currently lines 56-75).

**BEFORE** (0-targets branch body):
```csharp
if (targets == null || targets.Count == 0)
{
    try
    {
        var bareStop = acc.CreateOrder(
            instr, stopDir, OrderType.StopMarket, OrderEntry.Manual,
            TimeInForce.Gtc, pos.Quantity,
            0,                                          // arg6: limitPrice=0  (NT8-049)
            newStop,                                    // arg7: stopPrice     (NT8-049)
            string.Empty,                               // arg8: no OCO
            "PTT-BE-Stop",                             // arg9: signal name   (NT8-014)
            DateTime.MaxValue,                          // arg10: GTC          (NT8-013)
            (NinjaTrader.Cbi.CustomOrder)null);         // arg11: cast         (NT8-007)
        if (bareStop != null)
            acc.Submit(new[] { bareStop });
    }
    catch { /* non-fatal */ }
    return;
}
```

**AFTER** (0-targets branch body, `if (bareStop != null)` guard REMOVED, IsStopPriceSubmittable guard ADDED):
```csharp
if (targets == null || targets.Count == 0)
{
    if (IsStopPriceSubmittable(instr, isLong, newStop))
    {
        try
        {
            var bareStop = acc.CreateOrder(
                instr, stopDir, OrderType.StopMarket, OrderEntry.Manual,
                TimeInForce.Gtc, pos.Quantity,
                0,                                          // arg6: limitPrice=0  (NT8-049)
                newStop,                                    // arg7: stopPrice     (NT8-049)
                string.Empty,                               // arg8: no OCO
                "PTT-BE-Stop",                             // arg9: signal name   (NT8-014)
                DateTime.MaxValue,                          // arg10: GTC          (NT8-013)
                (NinjaTrader.Cbi.CustomOrder)null);         // arg11: cast         (NT8-007)
            acc.Submit(new[] { bareStop });
        }
        catch (Exception ex)
        {
            NinjaTrader.Code.Output.Process(
                "[BE-ERR] " + acc.Name + " submit failed: " + ex.Message,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }
    }
    else
    {
        NinjaTrader.Code.Output.Process(
            "[BE-ERR] " + acc.Name
                + " PTT-BE-Stop stop below market @ " + newStop + " -- skipping tranche",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    }
    return;
}
```

**CRITICAL**: The `if (bareStop != null)` guard is REMOVED. The catch block now handles any NullReferenceException from `CreateOrder`. This is the CYC exchange that keeps Execute() at CYC = 8.

---

### T2 -- Header Comment Update

**Location**: File-level comment block at lines 1-10 of `PttBreakEvenSwap.cs`.

**Changes required**:
1. Line 29: `// both on same OCO id: PTT-BE-{acc.Name[..8]}-{seq:D5}-{i}` -> `// both on same OCO id: PTT-BE-{acc.Name[..8]}-{seq:D7}-{i}`
2. Line 32: `// OCO id format: PTT-BE-{acc[..8]}-{seq:D5}-{i} -- UNCHANGED from today.` -> `// OCO id format: PTT-BE-{acc[..8]}-{seq:D7}-{i} -- updated to D7 by DW-B89-01.`
3. Lines 33-35 CYC comment: update to reflect new branch numbering:
   ```
   // CYC=8: (1) null guard, (2) flat guard, (3) ternary direction,
   //        (4) targets==0 branch, (5) IsStopPriceSubmittable 0-targets guard (NEW),
   //        (6) for-loop, (7) IsStopPriceSubmittable per-tranche guard (NEW), (8) if(tOrd!=null).
   //        Removed: if(bareStop!=null) and if(sOrd!=null) -- catch absorbs NRE.
   ```

---

### T2 -- Method Signatures

```csharp
// UNCHANGED signature:
internal static void Execute(
    Account acc,
    Instrument instr,
    double newStop,
    List<(double Price, int Qty, OrderAction Action)> targets)

// NEW private static helper (added BEFORE Execute):
private static bool IsStopPriceSubmittable(
    Instrument instr, bool isLong, double stopPrice)
```

---

### T2 -- JS Rule Constraints

| Rule | Requirement | Verification |
|------|-------------|--------------|
| JS-001 (P0) | No `throw` in hot paths | No new `throw` statements. `catch(Exception ex)` only logs via `Output.Process`. |
| JS-002 (P0) | No `return null` for missing values | `IsStopPriceSubmittable` returns `bool`. No null returns anywhere in new code. |
| JS-021 (P0) | No `lock()` anywhere | No lock added. No locking construct of any kind. |
| JS-023 (P1) | Use atomic primitives for simple shared state | `Execute()` is synchronous and called with per-account instances. No shared state introduced. |
| JS-033 (P0) | No `async void` (non-event-handler) | `Execute()` is `static void` (synchronous). `IsStopPriceSubmittable` is `static bool`. No async methods added. |
| ASCII-only | No Unicode in identifiers or string literals | `[BE-ERR]`, `"submit failed: "`, `"stop below market @ "`, `"skipping tranche"` -- all ASCII. Verify with SCAN-07. |
| DateTime.Now ban | `DateTime.UtcNow` only | `DateTime.MaxValue` used for GTC (not `DateTime.Now`). No `DateTime.Now` introduced. |

---

### T2 -- NT8 Constraints

| Constraint | Requirement |
|------------|-------------|
| NT8-049 | `CreateOrder` arg6=limitPrice, arg7=stopPrice. StopMarket: arg6=0, arg7=stopPrice. Limit: arg6=limitPrice, arg7=0. NEVER swap. Verify all `CreateOrder` calls in the file after edits. |
| NT8-007 | arg11 must be `(NinjaTrader.Cbi.CustomOrder)null`. All `CreateOrder` calls preserve this. |
| NT8-013 | arg10 must be `DateTime.MaxValue` for GTC orders. All `CreateOrder` calls preserve this. |
| NT8-014 | Signal names (arg9) must start with `PTT-`. `"PTT-BE-Stop"`, `"PTT-BE-Stop-" + (i + 1)`, `"PTT-BE-Target-" + (i + 1)` all preserved unchanged. |

---

### T2 -- CYC Analysis for Execute() After All Changes

Execute() cyclomatic complexity after applying all 5 changes must equal exactly 8.

| # | Branch | Notes |
|---|--------|-------|
| 1 | `if (acc == null \|\| instr == null)` | null guard -- KEPT |
| 2 | `if (pos == null \|\| pos.Quantity == 0)` | flat guard -- KEPT |
| 3 | `isLong ? ... : ...` | direction ternary -- KEPT |
| 4 | `if (targets == null \|\| targets.Count == 0)` | 0-targets branch -- KEPT |
| 5 | `if (IsStopPriceSubmittable(instr, isLong, newStop))` | 0-targets submittable guard -- NEW |
| 6 | `for (int i = 0; i < targets.Count; i++)` | with-targets loop -- KEPT |
| 7 | `if (IsStopPriceSubmittable(instr, isLong, newStop))` | per-tranche submittable guard -- NEW |
| 8 | `if (tOrd != null)` | target null guard -- KEPT |

**Removed branches** (CYC exchange to make room for the 2 new guards):
- `if (bareStop != null)` -- REMOVED; catch(Exception ex) handles NullReferenceException
- `if (sOrd != null)` -- REMOVED; catch(Exception ex) handles NullReferenceException

**CYC = 8. Limit: <= 8. PASS.**

If engineer finds CYC > 8 after edits: re-count every `if`, `for`, `while`, `?:`, `&&`, `||`
in Execute(). The `&&` and `||` in compound conditions each count as a branch.
Consult plan Section 3 for the authoritative count.

---

### T2 -- xUnit Tests

No new xUnit [Fact] tests are written in T2. Correctness of T2 changes is verified via:
- SCAN-01 through SCAN-07 (static verification)
- SIM gate (runtime verification) per V-criteria in plan Section 6

---

### T2 -- 7-Scan Checklist (Engineer Contract)

The engineer MUST complete ALL 7 scans and record pass/fail before marking T2 complete.

| Scan | Command | Required Result | Status |
|------|---------|-----------------|--------|
| SCAN-01 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings on changed files | [ ] |
| SCAN-02 | Manual CYC count of `PttBreakEvenSwap.Execute()` and `IsStopPriceSubmittable()` | `Execute()` CYC = 8 exactly; `IsStopPriceSubmittable()` CYC <= 3 | [ ] |
| SCAN-03 | `grep -r "lock(" src/PropTraderTools/` | 0 matches | [ ] |
| SCAN-04 | `grep -rn "async void " src/PropTraderTools/` | 0 matches in new or modified code | [ ] |
| SCAN-05 | `grep -r ".ToString(\"D5\")" src/PropTraderTools/Features/` | 0 matches in `PttBreakEvenSwap.cs`. Only permitted match: `PttGlobalBreakEven.cs` (out of scope, untouched) | [ ] |
| SCAN-06 | `grep -r "catch { /\* non-fatal \*/ }" src/PropTraderTools/Features/PttBreakEvenSwap.cs` | 0 matches (all 3 bare catches replaced) | [ ] |
| SCAN-07 | ASCII-only check on `src/PropTraderTools/Features/PttBreakEvenSwap.cs` | 0 non-ASCII characters in identifiers or string literals | [ ] |

---

## T3 -- PttBreakEven.cs D7 Alignment + T_OCO_SEED_03 Test Update

### Ticket Metadata

| Field | Value |
|-------|-------|
| **Ticket ID** | T3 |
| **Title** | PttBreakEven.BuildBeOcoId: D5->D7 + CopyEngineB72Tests T_OCO_SEED_03 rename+assert update |
| **Spec Requirements** | DW-B89-01 (D7 alignment across all BE OCO paths) |
| **Files** | `src/PropTraderTools/Features/PttBreakEven.cs` (L357 + L10 header), `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` (test rename + assertion) |
| **Risk** | Low -- string literal change + test rename |

---

### T3 -- File A: PttBreakEven.cs Changes

#### Change A1: BuildBeOcoId D5 -> D7 (line 357)

**BEFORE** (line 357):
```csharp
return "PTT-BE-" + prefix + "-" + seq.ToString("D5") + "-" + pairIndex.ToString();
```

**AFTER** (line 357):
```csharp
return "PTT-BE-" + prefix + "-" + seq.ToString("D7") + "-" + pairIndex.ToString();
```

#### Change A2: Header comment D5 -> D7 (line 10)

**BEFORE** (line 10):
```
//   New formula: "PTT-BE-"+accPrefix+"-"+seq.ToString("D5")+"-"+pairIndex  (always unique)
```

**AFTER** (line 10):
```
//   New formula: "PTT-BE-"+accPrefix+"-"+seq.ToString("D7")+"-"+pairIndex  (always unique, DW-B89-01)
```

**Scope boundary**: Only `BuildBeOcoId` is modified. `PttGlobalBreakEven.cs` uses prefix
`PTT-BEG-*` with an independent counter -- D5 is intentionally preserved there and is
**out of scope for DW-B89**. Do NOT modify `PttGlobalBreakEven.cs`.

---

### T3 -- File B: CopyEngineB72Tests.cs Changes

**WARNING -- FILE ACCESS**: `CopyEngineB72Tests.cs` may be in `.bobignore`. The engineer
MUST use `execute_command` (Get-Content / Set-Content) to read and write this file. Do NOT
use `read_file` or `write_file` tool for this file.

**File location**: `src/PropTraderTools/Tests/CopyEngineB72Tests.cs`

#### Change B1: Test method rename

**BEFORE** (method name):
```csharp
public void T_OCO_SEED_03_NextBeOcoSeq_D5Format_FiveDigitPadding()
```

**AFTER** (method name):
```csharp
public void T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding()
```

If a `[Fact(DisplayName = "...")]` attribute is present, update its display name string from
`"D5"` / `"FiveDigit"` / `"five digit"` (case-insensitive) to the D7 equivalent.

#### Change B2: Assertion update

Locate the assertion(s) inside `T_OCO_SEED_03_NextBeOcoSeq_D5Format_FiveDigitPadding` that
test the formatted sequence string length or pattern.

**BEFORE** (any assertion referencing D5 / 5-char padding):
```csharp
// Any form of: seq.ToString("D5") or Assert referencing 5-char padding
string formatted = seq.ToString("D5");
Assert.Equal(5, formatted.Length);   // or similar 5-char assertion
```

**AFTER** (updated to D7 / 7-char padding):
```csharp
string formatted = seq.ToString("D7");
Assert.True(formatted.Length >= 7);
Assert.Matches(@"^\d{7,}$", formatted);
```

**Note**: If the existing test uses `Assert.Equal(5, ...)` replace with `Assert.True(formatted.Length >= 7)`.
If it uses `Assert.Matches` with a 5-digit pattern replace with `@"^\d{7,}$"`.
Preserve all other assertions in the test method verbatim.

#### Pre-existing errors (OUT OF SCOPE -- DO NOT FIX)

The test infrastructure in `CopyEngineTests.cs` has 83 pre-existing build errors (DW-PTT-BE-FIX-03).
These are **not introduced by DW-B89** and are **not to be fixed** in this ticket.
The engineer must update only the one named test method in `CopyEngineB72Tests.cs`.
Do not attempt to resolve compilation errors in any other test file.

---

### T3 -- Method Signatures

```csharp
// UNCHANGED signature (PttBreakEven.cs):
private static string BuildBeOcoId(string accName, int seq, int pairIndex)

// RENAMED test method (CopyEngineB72Tests.cs):
// BEFORE: public void T_OCO_SEED_03_NextBeOcoSeq_D5Format_FiveDigitPadding()
// AFTER:  public void T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding()
```

---

### T3 -- JS Rule Constraints

| Rule | Requirement | Verification |
|------|-------------|--------------|
| ASCII-only | No Unicode in identifiers or string literals | `"D7"` is ASCII. `@"^\d{7,}$"` is ASCII. SCAN-07 confirms. |
| JS-021 (P0) | No `lock()` anywhere | No lock added. |
| JS-033 (P0) | No `async void` | No async code touched. |
| CYC <= 8 | `BuildBeOcoId` CYC unchanged | CYC = 2. String literal `"D5"` -> `"D7"` is not a branch; CYC unchanged. |

---

### T3 -- NT8 Constraints

No NT8 API calls are modified in T3. `BuildBeOcoId` is a pure string computation.

---

### T3 -- CYC Analysis

| Method | CYC Before | CYC After | Notes |
|--------|-----------|-----------|-------|
| `BuildBeOcoId` | 2 | 2 | String literal `"D5"` -> `"D7"` is not a branch. CYC unchanged. |

---

### T3 -- xUnit Tests

**[Fact] test name** (after rename):
```
T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding
```

**What it asserts**:
- `seq.ToString("D7").Length >= 7` -- D7 pads to at least 7 digits.
- `Assert.Matches(@"^\d{7,}$", formatted)` -- result is all digits, minimum 7.

---

### T3 -- 7-Scan Checklist (Engineer Contract)

The engineer MUST complete all applicable scans and record pass/fail before marking T3 complete.

| Scan | Command | Required Result | Status |
|------|---------|-----------------|--------|
| SCAN-01 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings on changed files | [ ] |
| SCAN-02 | Manual CYC check of `BuildBeOcoId` | CYC = 2 (unchanged) | [ ] |
| SCAN-03 | `grep -r "lock(" src/PropTraderTools/` | 0 matches | [ ] |
| SCAN-04 | `grep -rn "async void " src/PropTraderTools/` | 0 matches in new or modified code | [ ] |
| SCAN-05 | `grep -r ".ToString(\"D5\")" src/PropTraderTools/Features/` | 0 matches in `PttBreakEven.cs`. Only permitted match: `PttGlobalBreakEven.cs` (out of scope, untouched) | [ ] |
| SCAN-06 | Not applicable to T3 (`PttBreakEvenSwap.cs` not touched) | N/A | [ ] |
| SCAN-07 | ASCII-only check on `src/PropTraderTools/Features/PttBreakEven.cs` (changed lines) | 0 non-ASCII characters in identifiers or string literals | [ ] |

**Primary scans for T3**: SCAN-01, SCAN-05, SCAN-07.

---

## Verification Criteria Cross-Reference

Each verification criterion from plan Section 6 maps to a ticket:

| V-ID | Criterion | Ticket | Scan |
|------|-----------|--------|------|
| V-01 | `_mstbeOcoSeq` seed uses XOR formula with `DateTime.UtcNow.Ticks` | T1 | SCAN-01, SCAN-07 |
| V-02 | `seq.ToString("D7")` in `PttBreakEvenSwap.cs` | T2 | SCAN-05 |
| V-03 | `seq.ToString("D7")` in `PttBreakEven.BuildBeOcoId` | T3 | SCAN-05 |
| V-04 | Zero bare `catch { /* non-fatal */ }` in `PttBreakEvenSwap.cs` | T2 | SCAN-06 |
| V-05 | All 3 catch blocks log `[BE-ERR] ... submit failed: ...` to OutputTab1 | T2 | SCAN-07 |
| V-06 | `IsStopPriceSubmittable` exists as `private static bool` BEFORE `Execute()` | T2 | SCAN-02 |
| V-07 | With-targets stop submit wrapped in `IsStopPriceSubmittable` guard | T2 | SCAN-02 |
| V-08 | 0-targets bare-stop path wrapped in `IsStopPriceSubmittable` guard | T2 | SCAN-02 |
| V-09 | `PttGlobalBreakEven.cs` is UNMODIFIED (D5 preserved) | T3 | `git diff src/PropTraderTools/Features/PttGlobalBreakEven.cs` = no changes |
| V-10 | `T_OCO_SEED_03` renamed to D7, asserts 7-char padding | T3 | Source read of CopyEngineB72Tests.cs |

---

## Execution Order

Tickets are independent and may be executed in any order. Recommended order:

1. **T1** (lowest risk, establishes seed fix)
2. **T2** (medium risk, multiple concurrent changes -- execute as single atomic edit session)
3. **T3** (lowest risk, string literal + test rename -- execute after T2 so SCAN-05 baseline is clean)

After all three tickets complete, run the full 7-scan suite against the combined diff before
marking DW-B89 done.
