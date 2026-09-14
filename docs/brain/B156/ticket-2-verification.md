# B156/B157 -- Ticket 2 Verification (Retry 1)
# Ticket: T2 -- DW-B157-01: fo=NULL for Drain Entries (Stop/Target Drag Broken)
# Verifier: PTT Verifier (Phase 4b) -- independent Layer 3 verification
# Source: READ-ONLY from src/PropTraderTools/CopyEngine.cs and src/PropTraderTools/CopyEngineTests.cs
# Status: VERIFY_PASS

---

## 0. Scope

**TICKET SCOPE: T2 ONLY (DW-B157-01).**
No other ticket code was read or cross-referenced during this verification session.

Fix locations verified:
- `TrySetSlateReadyOnDrainFill` method (~lines 992-1010) -- tagged `[SLATE-READY-DRAIN]`
- Call site in `OnOrderUpdate` after `HandleDrainTerminalState` (~line 1756)
- New test class `DwB15701SlateReadyTests` (lines 9432-9552 of CopyEngineTests.cs)

---

## 1. Prior VERIFY_FAIL Summary

The initial Phase 4b verification (prior session) issued **VERIFY_FAIL -- VIOLATION-T2-01**:
5 required xUnit tests for `TrySetSlateReadyOnDrainFill` were absent from
`src/PropTraderTools/CopyEngineTests.cs`. The CopyEngine.cs implementation was confirmed
correct and fully DNA-compliant in that session. The failure was exclusively a test coverage
gap: the test class `DwB15701SlateReadyTests` did not exist, and all 5 ticket-specified
`TrySetSlateReadyOnDrainFill_*` test methods returned 0 grep hits. The engineer was directed
to add the 5 required tests before re-verification.

---

## 2. Retry Resolution: Tests Confirmed Present

The engineer added class `DwB15701SlateReadyTests` to `src/PropTraderTools/CopyEngineTests.cs`
(lines 9432-9552) in the retry cycle. All 5 required tests are now present.

Independent grep verification (`grep TrySetSlateReadyOnDrainFill src/PropTraderTools/CopyEngineTests.cs`):
18 matches found. Class declaration confirmed at line 9432.

| Required test name                                                  | Present? | Line  | [Fact]? |
|---------------------------------------------------------------------|----------|-------|---------|
| `TrySetSlateReadyOnDrainFill_MethodExists_StructuralCheck`          | YES      | 9444  | YES (9443) |
| `TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect`                | YES      | 9458  | YES (9457) |
| `TrySetSlateReadyOnDrainFill_NonFilledOrder_NoSideEffect`           | YES      | 9483  | YES (9482) |
| `TrySetSlateReadyOnDrainFill_NonPttCopyName_NoSideEffect`           | YES      | 9504  | YES (9503) |
| `TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal`        | YES      | 9526  | YES (9525) |

All 5 tests are `[Fact]` attributed. All use BCL/structural patterns (Reflection, System.IO.File,
System.String). No NT8 host required.

---

## 3. Independent Source Confirmation

### Method: `TrySetSlateReadyOnDrainFill` (lines 992-1010)

Source read directly from `src/PropTraderTools/CopyEngine.cs`:

```csharp
private void TrySetSlateReadyOnDrainFill(Order order)
{
    if (order == null)                                                          // (1)
        return;
    if (order.OrderState != OrderState.Filled)                                 // (2)
        return;
    if (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal))          // (3)
        return;
    if (!IsFollowerAccount(order.Account))                                     // (4)
        return;
    if (IsFlat(FindPosition(order.Account, order.Instrument)))                 // (5) no open position -- skip
        return;
    _followerSlateReady[order.Account.Name] = true;
    NinjaTrader.Code.Output.Process(
        "[SLATE-READY-DRAIN] acct=" + order.Account.Name
        + " instr=" + (order.Instrument?.FullName ?? "?"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

Signature: `private void TrySetSlateReadyOnDrainFill(Order order)` -- CONFIRMED.
5 guards in correct order: null / Filled / PTT-Copy / IsFollowerAccount / NOT flat -- CONFIRMED.
State write: `_followerSlateReady[order.Account.Name] = true` -- CONFIRMED.
Log marker: `[SLATE-READY-DRAIN] acct=...instr=...` -- CONFIRMED.

### Call site: `OnOrderUpdate` (lines 1748-1756)

```
Line 1748: HandleDrainTerminalState(e.Order);
Line 1749: (blank)
Line 1750: // DW-B157-01: restore _followerSlateReady after a PTT-Copy drain entry fills.
Line 1756: TrySetSlateReadyOnDrainFill(e.Order);
```

Call site placement: immediately after `HandleDrainTerminalState(e.Order)` on line 1748 -- CONFIRMED.
Unconditional call (CYC delta = 0) -- CONFIRMED.

---

## 4. Independent 7-Scan Results (Layer 3)

All scans run independently by the PTT Verifier. Results DO NOT rely on engineer Layer 2 reports.
Scans cover BOTH fix locations in CopyEngine.cs AND the new DwB15701SlateReadyTests code in CopyEngineTests.cs.

### SCAN-01: ASCII-Only

**Scope**: CopyEngine.cs lines 986-1010 and 1748-1756; CopyEngineTests.cs lines 9428-9553.
**Commands run**:
- `Get-Content CopyEngine.cs | Select-Object -Skip 985 -First 26 | Where-Object {$_ -match '[^\x00-\x7F]'}`
- `Get-Content CopyEngine.cs | Select-Object -Skip 1747 -First 10 | Where-Object {$_ -match '[^\x00-\x7F]'}`
- `Get-Content CopyEngineTests.cs | Select-Object -Skip 9427 -First 127 | Where-Object {$_ -match '[^\x00-\x7F]'}`

**Result: PASS -- 0 non-ASCII characters found in all three regions.**
Evidence: All string literals (`"[SLATE-READY-DRAIN] acct="`, `"PTT-Copy"`, `" instr="`) use chars 0x20-0x7E only.
Test class comments and identifiers are ASCII-only.

---

### SCAN-02: No `lock()`

**Scope**: CopyEngine.cs lines 986-1010 and 1748-1756; CopyEngineTests.cs lines 9428-9553.
**Commands run**:
- `Select-String -Path CopyEngine.cs -Pattern "lock\s*\("` filtered to relevant line ranges
- `Select-String -Path CopyEngineTests.cs -Pattern "^\s+lock\s*\("` -- 0 hits (file-level executable lock count)

**Result: PASS -- 0 executable lock() statements.**

Evidence:
- Changed blocks in CopyEngine.cs: 0 lock() hits.
- DwB15701SlateReadyTests class: comment-only `lock` mention at line 9430 (`// JS-021: no lock()`).
  This is a comment, not an executable statement.
- `_followerSlateReady[order.Account.Name] = true` uses ConcurrentDictionary indexer -- lock-free per JS-021.

---

### SCAN-03: CYC Analysis for `TrySetSlateReadyOnDrainFill`

**Manual McCabe count (independent):**

| # | Decision point                                          | Count |
|---|--------------------------------------------------------|-------|
| 0 | Base                                                   | 1     |
| 1 | `if (order == null)` guard (1)                         | +1    |
| 2 | `if (order.OrderState != OrderState.Filled)` guard (2) | +1    |
| 3 | `if (!order.Name.StartsWith(...))` guard (3)           | +1    |
| 4 | `if (!IsFollowerAccount(...))` guard (4)               | +1    |
| 5 | `if (IsFlat(FindPosition(...)))` guard (5)             | +1    |
| 6 | `?.` null-conditional (order.Instrument?.FullName)     | +1    |
| 7 | `??` null-coalesce (?? "?")                            | +1    |
|   | **Total CYC (strict McCabe)**                          | **8** |

Counting only 5 `if` decision points (omitting `?.` and `??`): CYC=6 as stated in ticket.
Under most strict McCabe convention (counting `?.` and `??` as branches): CYC=8.
Either way: CYC <= 8. **PASS.**

**INFO-1 (non-blocking, inherited)**: Source comment at line 991 reads `// CYC<=5` (simplified
decision-points-only count without base=1, and not counting `?.`/`??`). Ticket §e states strict
McCabe = 6. All values are <=8. No gate is affected.

Call site adds 0 branches. **SCAN-03: PASS.**

---

### SCAN-04: JS-001 No `throw`

**Scope**: CopyEngine.cs lines 986-1010 and 1748-1756; CopyEngineTests.cs lines 9428-9553.
**Command**: `Select-String -Pattern "\bthrow\b"` filtered to each region, excluding comment lines.

**CopyEngine.cs result**: 1 raw hit at line 991 -- inside comment `// JS-001: no throw.` -- NOT executable.
0 throw statements in executable code.

**CopyEngineTests.cs result**: 1 hit at line 9474 -- `throw;` (bare rethrow).
This is a `throw;` rethrow statement inside a test's catch block
(`catch (TargetInvocationException tie) { if (tie.InnerException is NullReferenceException) return; throw; }`).
This is idiomatic C# exception propagation in test scaffolding -- it rethrows unexpected
exceptions to surface them as test failures. It is NOT `throw new ...Exception`.
JS-001 prohibits `throw new ...Exception` in production gate methods. This is:
  (a) in CopyEngineTests.cs -- test code, not a gate method
  (b) a bare rethrow, not a `throw new` instantiation

**SCAN-04: PASS -- 0 `throw new` violations in gate methods or production code.**

---

### SCAN-05: JS-002 No void returning null

**Scope**: CopyEngine.cs lines 992-1010; CopyEngineTests.cs lines 9432-9552.
**Command**: regex `return\s+null` against both regions.

**Result: PASS -- 0 `return null` statements found.**
Evidence: `TrySetSlateReadyOnDrainFill` has `void` return type with 5 bare `return;` statements.
Test class: no void methods; all void methods in tests use void/Assert patterns with no return value.

---

### SCAN-06: NT8 UI-Thread API Check

**Scope**: CopyEngine.cs lines 986-1010 and 1748-1756; CopyEngineTests.cs lines 9428-9553.
**Command**: Regex for `Dispatcher|InvokeAsync|OnBarUpdate|Account\.All` on both regions,
excluding comment lines.

**Result: PASS -- 0 hits.**

NT8 APIs in production changed code:

| API used                                  | AddOnBase safe | Basis              |
|-------------------------------------------|----------------|--------------------|
| `order.OrderState` / `OrderState.Filled`  | Yes            | Read-only property + enum value |
| `order.Name.StartsWith()`                 | Yes            | BCL string method  |
| `order.Account` / `order.Instrument`      | Yes            | Read-only properties |
| `IsFollowerAccount()` / `IsFlat()`        | Yes            | Internal helpers   |
| `FindPosition()`                          | Yes            | Internal helper    |
| `NinjaTrader.Code.Output.Process()`       | Yes            | Standard NT8 logging API |
| `_followerSlateReady[...] = true`         | Yes            | ConcurrentDictionary indexer |

Test class (DwB15701SlateReadyTests): uses only BCL types (System.Reflection, System.IO.File,
System.String, xUnit). No NT8 APIs referenced in test bodies.

---

### SCAN-07: Minimal Change -- Every Changed Line Traces to DW-B157-01

**CopyEngine.cs changed regions:**

| Region           | Lines      | Content                                          | Traces to DW-B157-01? |
|------------------|------------|--------------------------------------------------|-----------------------|
| Method header    | 986-991    | Comment block + DW-B157-01 tag                   | YES                   |
| Method body      | 992-1010   | `TrySetSlateReadyOnDrainFill` 5-guard body       | YES                   |
| Call site comment| 1750-1755  | Root-cause explanation for DW-B157-01            | YES                   |
| Call site        | 1756       | `TrySetSlateReadyOnDrainFill(e.Order);`           | YES                   |

**CopyEngineTests.cs new region:**

| Region                    | Lines         | Content                                | Traces to DW-B157-01? |
|---------------------------|---------------|----------------------------------------|-----------------------|
| DwB15701SlateReadyTests   | 9428-9553     | 5-test class for TrySetSlateReadyOnDrainFill | YES -- resolves VIOLATION-T2-01 |

Executable `lock()` in test class: 0 (confirmed by PowerShell `^\s+lock\s*\(` scan).
`[SLATE-READY-DRAIN]` tag: appears only at line 1006 (production) and line 9429 (test comment).
`DW-B157-01` tag: appears at lines 986, 1750 (production) and line 9428 (test comment header).
No adjacent code modified, no whitespace mutations in existing tests.

**SCAN-07: PASS -- all changed lines trace directly to DW-B157-01 / VIOLATION-T2-01 resolution.**

---

## 5. Scan Summary Table

| Scan    | Description                                   | Result   | Evidence                                                              |
|---------|-----------------------------------------------|----------|-----------------------------------------------------------------------|
| SCAN-01 | ASCII-only                                    | **PASS** | 0 non-ASCII in CopyEngine.cs lines 986-1010, 1748-1756; 0 in DwB15701SlateReadyTests |
| SCAN-02 | No `lock()`                                   | **PASS** | 0 executable lock(); comment-only at 9430; ConcurrentDictionary lock-free |
| SCAN-03 | CYC <= 8                                      | **PASS** | TrySetSlateReadyOnDrainFill CYC=6-8 depending on counting convention; all <= 8 |
| SCAN-04 | JS-001: no `throw`                            | **PASS** | 0 `throw new` in production code; test line 9474 is bare rethrow in test scaffolding |
| SCAN-05 | JS-002: void, no null return                  | **PASS** | void return; all returns are bare `return;`; 0 `return null` hits |
| SCAN-06 | No NT8 UI-thread calls                        | **PASS** | No Dispatcher/async; all APIs AddOnBase-safe; test class uses BCL only |
| SCAN-07 | Minimal change -- traces to DW-B157-01        | **PASS** | All changed regions (986-1010, 1748-1756, 9428-9553) exclusively address DW-B157-01/VIOLATION-T2-01 |

**ALL 7 SCANS: PASS**

---

## 6. Implementation Correctness Assessment

| Requirement                                                      | Met? | Evidence                                    |
|------------------------------------------------------------------|------|---------------------------------------------|
| New private helper `TrySetSlateReadyOnDrainFill(Order order)`    | YES  | Lines 992-1010, signature exact             |
| Guard (1): null order -> early return                            | YES  | Line 994                                    |
| Guard (2): OrderState != Filled -> early return                  | YES  | Line 996                                    |
| Guard (3): name must start with "PTT-Copy" (Ordinal)            | YES  | Line 998                                    |
| Guard (4): must be follower account                              | YES  | Line 1000                                   |
| Guard (5): account NOT flat (has open drain position)           | YES  | Line 1002                                   |
| State write: `_followerSlateReady[order.Account.Name] = true`   | YES  | Line 1004                                   |
| Log: `[SLATE-READY-DRAIN] acct=X instr=Y`                       | YES  | Lines 1005-1009                             |
| Call site: after `HandleDrainTerminalState` in `OnOrderUpdate`  | YES  | Line 1756, after line 1748                  |
| Call site: unconditional (CYC delta = 0)                        | YES  | No wrapping branch                          |
| CYC of new method <= 8                                           | YES  | CYC=6 (strict McCabe without null-conditional) |
| Required tests present (5 TrySetSlateReadyOnDrainFill_* tests)  | YES  | DwB15701SlateReadyTests class, lines 9432-9552 |

### DNA Compliance

| Rule     | Status   | Evidence                                                          |
|----------|----------|-------------------------------------------------------------------|
| JS-001   | PASS     | No throw in method or call site; test rethrow is scaffolding-only |
| JS-002   | PASS     | void method, no null return                                       |
| JS-003   | PASS     | No magic string discriminators                                    |
| JS-013   | PASS     | CYC <= 8 (6-8 by counting convention)                            |
| JS-021   | PASS     | ConcurrentDictionary indexer (lock-free); 0 executable lock() anywhere |
| JS-023   | PASS     | No Dispatcher required; no WPF mutations in helper               |
| JS-025   | PASS     | _followerSlateReady is ConcurrentDictionary<string,bool>         |

---

## 7. Comparison with Engineer Layer 2 Report (Retry 1)

| Scan    | Engineer Layer 2 (Retry 1) | Verifier Layer 3 | Match? | Notes |
|---------|----------------------------|------------------|--------|-------|
| SCAN-01 | PASS                       | PASS             | YES    | Exact agreement |
| SCAN-02 | PASS                       | PASS             | YES    | Exact agreement |
| SCAN-03 | PASS / CYC=6               | PASS / CYC=6-8   | YES    | Both <= 8; counting convention difference is non-blocking (INFO-1) |
| SCAN-04 | PASS                       | PASS             | YES    | Both correctly note bare rethrow is test scaffolding |
| SCAN-05 | PASS                       | PASS             | YES    | Exact agreement |
| SCAN-06 | PASS                       | PASS             | YES    | Exact agreement |
| SCAN-07 | PASS                       | PASS             | YES    | Exact agreement |

Tests confirmed present: all 5 required. Layer 2 vs Layer 3: NO discrepancies.

---

## 8. Verdict

**VERIFY_PASS**

All 5 required xUnit tests for `TrySetSlateReadyOnDrainFill` are now present in
`src/PropTraderTools/CopyEngineTests.cs` as class `DwB15701SlateReadyTests` (lines 9432-9552).
All 5 tests are `[Fact]` attributed and BCL/structural (no NT8 host required).

All 7 scans PASS against both the CopyEngine.cs implementation and the new test code.
The CopyEngine.cs implementation is correct, complete, and DNA-compliant.
VIOLATION-T2-01 (missing tests) is resolved.

No violations remain. Ticket T2 (DW-B157-01) is verified and ready for Phase 5.

---

_Verification authored by PTT Verifier (Phase 4b) -- Retry 1. READ-ONLY access to src/. No .cs files modified._
_Source scans run independently. Layer 2 (engineer retry) results cross-checked against Layer 3 (verifier)._
