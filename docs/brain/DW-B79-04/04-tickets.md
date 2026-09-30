# DW-B79-04 Engineering Tickets

**Block**: DW-B79-04
**Author**: ptt-architect (Phase 3)
**Plan**: `docs/brain/DW-B79-04/02-architecture-plan.md` (REVIEW_PASS)
**Plan Review**: `docs/brain/DW-B79-04/02-plan-review.md` (REVIEW_PASS)
**Date**: 2026-08-20
**Tickets**: 2

---

## TICKET-1: DW-B79-CANCEL-01 (P1)

### Spec Requirements Satisfied

| Req ID | Description |
|--------|-------------|
| DW-B79-CANCEL-01-R1 | Remove `OrderState.ChangeSubmitted` from `stateOk` in `CancelAllAccountOrders` |
| DW-B79-CANCEL-01-R2 | Add `toCancel.RemoveAll(o => o.OrderState == Filled \|\| Cancelled)` before `acc.Cancel()` |
| DW-B79-CANCEL-01-R3 | Update L710 comment (remove ChangeSubmitted from States list) |
| DW-B79-CANCEL-01-R4 | New xUnit `[Fact]` `CancelAllAccountOrders_SkipsChangeSubmittedOrders` |
| DW-B79-CANCEL-01-R5 | L2662 `MoveStopToBreakEven` ChangeSubmitted MUST NOT change (protect-only) |

### File Path

```
src/PropTraderTools/CopyEngine.cs          (method: CancelAllAccountOrders, L706-734)
src/PropTraderTools/Tests/B79Tests.cs      (append [Fact] to existing B79Tests class)
```

### Method Signature (unchanged)

```csharp
internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
```

### Before Code (verbatim, L706-731)

```csharp
        // B69 DW-B69-01: CancelAllAccountOrders -- cancel every active order on acc for instr
        // before submitting a market flatten. No name filter -- all order names cancelled.
        // NT8 precedent: @2Custom-0909edcc EmergencyFlattenSingleFleetAccount [938-EF-GUARD]:
        //   "Step 1: Cancel ALL working orders on this instrument for this account."
        //   States: Working|Submitted|Accepted|ChangePending|ChangeSubmitted.
        // CYC=4: null-guard(1) + foreach(2) + stateOk(3) + instrument-name(4). JS-021: no lock.
        // JS-001: no throw. JS-002: void. ASCII-only.
        internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            if (acc == null || instr == null) return;                              // (1)
            var toCancel = new System.Collections.Generic.List<Order>();
            foreach (Order o in acc.Orders)                                        // (2)
            {
                bool stateOk = o.OrderState == OrderState.Working
                            || o.OrderState == OrderState.Initialized
                            || o.OrderState == OrderState.Submitted
                            || o.OrderState == OrderState.Accepted
                            || o.OrderState == OrderState.ChangeSubmitted;
                if (!stateOk) continue;                                            // (3)
                if (o.Instrument == null
                    || o.Instrument.FullName != instr.FullName) continue;          // (4)
                toCancel.Add(o);
            }
            if (toCancel.Count == 0) return;
            try { acc.Cancel(toCancel); } catch { }
        }
```

### After Code (engineer writes exactly this, L706-734)

```csharp
        // B69 DW-B69-01: CancelAllAccountOrders -- cancel every active order on acc for instr
        // before submitting a market flatten. No name filter -- all order names cancelled.
        // NT8 precedent: @2Custom-0909edcc EmergencyFlattenSingleFleetAccount [938-EF-GUARD]:
        //   "Step 1: Cancel ALL working orders on this instrument for this account."
        //   States: Working|Submitted|Accepted|ChangePending.
        // CYC=4: null-guard(1) + foreach(2) + stateOk-4terms(3) + instrument-name(4). JS-021: no lock.
        // JS-001: no throw. JS-002: void. ASCII-only.
        internal void CancelAllAccountOrders(Account acc, NinjaTrader.Cbi.Instrument instr)
        {
            if (acc == null || instr == null) return;                              // (1)
            var toCancel = new System.Collections.Generic.List<Order>();
            foreach (Order o in acc.Orders)                                        // (2)
            {
                bool stateOk = o.OrderState == OrderState.Working
                            || o.OrderState == OrderState.Initialized
                            || o.OrderState == OrderState.Submitted
                            || o.OrderState == OrderState.Accepted;
                if (!stateOk) continue;                                            // (3)
                if (o.Instrument == null
                    || o.Instrument.FullName != instr.FullName) continue;          // (4)
                toCancel.Add(o);
            }
            // DW-B79-04: belt-and-suspenders race guard -- discard orders that
            // transitioned to terminal state between snapshot and cancel call.
            toCancel.RemoveAll(o => o.OrderState == OrderState.Filled
                                    || o.OrderState == OrderState.Cancelled);
            if (toCancel.Count == 0) return;
            try { acc.Cancel(toCancel); } catch { }
        }
```

### Change Summary

| Change | Location | Description |
|--------|----------|-------------|
| A | L710 comment | Remove `ChangeSubmitted` from States list in comment |
| B | L711 comment | Update CYC comment from `stateOk(3)` to `stateOk-4terms(3)` |
| C | L723 (delete line) | Remove `\|\| o.OrderState == OrderState.ChangeSubmitted` |
| D | Insert after foreach closing brace | Add `RemoveAll` belt-and-suspenders + 2-line comment |

**Line shift**: L733 onward shifts by +3 due to 3 added lines (comment x2 + RemoveAll). No logic
outside this method is affected. MoveStopToBreakEven at L2662 is FROZEN -- must not appear in diff.

### FROZEN Line (DO NOT TOUCH)

```
L2662 in MoveStopToBreakEven:
    || o.OrderState == OrderState.ChangeSubmitted  // DW-B79-04: NT8 sim ATM target transient state on creation
```

This line is a READ filter (snapshot prices for OCO bracket), NOT an ACTION filter. It must remain
exactly as-is. Verify after implementation: `git diff -- src/PropTraderTools/CopyEngine.cs` must
not show L2662 in the diff.

### JS Rule Constraints

| Rule | Constraint | Verification |
|------|-----------|--------------|
| JS-001 | No `throw new` in `CancelAllAccountOrders` | SCAN-05 |
| JS-002 | No `return null` (`void` method, bare `return;` only) | SCAN-04 |
| JS-021 | No `lock()` -- `acc.Orders` accessed on NT8 dispatch thread (safe); `List<Order>` is local | SCAN-02 |
| JS-033 | Method is synchronous `void` -- no `async` keyword | SCAN-03 |
| CYC<=8 | CYC=4 after change (structural convention): null-guard + foreach + stateOk-4terms + instr-check | SCAN-06 |
| ASCII | All new string literals and identifiers are ASCII-only | SCAN-01 |

### xUnit [Fact] Test

**File**: `src/PropTraderTools/Tests/B79Tests.cs`
**Class**: `B79Tests` (existing sealed class -- append `[Fact]` method)
**Method name**: `CancelAllAccountOrders_SkipsChangeSubmittedOrders`

```csharp
[Fact]
public void CancelAllAccountOrders_SkipsChangeSubmittedOrders()
{
    // Arrange
    // Reflect CancelAllAccountOrders on CopyEngine via BindingFlags.NonPublic | Instance.
    var method = typeof(CopyEngine).GetMethod(
        "CancelAllAccountOrders",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    Assert.NotNull(method);

    // Act: extract IL byte array from method body
    var body = method.GetMethodBody();
    Assert.NotNull(body);
    var il = body.GetILAsByteArray();
    Assert.NotNull(il);

    // Resolve all ldsfld tokens -- collect FieldInfo objects
    var module = typeof(CopyEngine).Module;
    var changeSubmittedField = typeof(OrderState).GetField("ChangeSubmitted");
    Assert.NotNull(changeSubmittedField);

    bool foundChangeSubmitted = false;
    bool foundWorking        = false;
    bool foundAccepted       = false;
    bool foundSubmitted      = false;
    bool foundInitialized    = false;

    for (int i = 0; i < il.Length - 4; i++)
    {
        // ldsfld opcode = 0x7E
        if (il[i] != 0x7E) continue;
        int token = System.BitConverter.ToInt32(il, i + 1);
        try
        {
            var fi = module.ResolveField(token) as System.Reflection.FieldInfo;
            if (fi == null || fi.DeclaringType != typeof(OrderState)) continue;
            if (fi.Name == "ChangeSubmitted") foundChangeSubmitted = true;
            if (fi.Name == "Working")         foundWorking         = true;
            if (fi.Name == "Accepted")        foundAccepted        = true;
            if (fi.Name == "Submitted")       foundSubmitted       = true;
            if (fi.Name == "Initialized")     foundInitialized     = true;
        }
        catch { /* token resolution may fail for non-field tokens -- skip */ }
    }

    // Primary assert: ChangeSubmitted must NOT be loaded in this method (ticket requirement)
    Assert.False(foundChangeSubmitted,
        "OrderState.ChangeSubmitted must not appear in CancelAllAccountOrders IL after DW-B79-04");

    // Secondary regression guard: the 4 valid states must still be present
    Assert.True(foundWorking,     "OrderState.Working must be present in stateOk filter");
    Assert.True(foundAccepted,    "OrderState.Accepted must be present in stateOk filter");
    Assert.True(foundSubmitted,   "OrderState.Submitted must be present in stateOk filter");
    Assert.True(foundInitialized, "OrderState.Initialized must be present in stateOk filter");
}
```

**Red-green contract**:
- BEFORE applying TICKET-1: test FAILS (ChangeSubmitted IS in the IL).
- AFTER applying TICKET-1: test PASSES (ChangeSubmitted is absent; 4 valid states present).

**Total test count after this ticket**: 292 (291 existing + 1 new).

### 7-Scan Checklist (TICKET-1)

Engineer must run all 7 scans and confirm each result before marking TICKET-1 complete.

```
SCAN-01: ASCII-only
  Command : grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in new/modified lines (L706-734)
  Pass    : zero matches
  Fail    : any non-ASCII character in new or modified lines

SCAN-02: lock() ban (JS-021 -- P0 CRITICAL)
  Command : grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results (no lock() anywhere in file)
  Pass    : zero matches
  Fail    : any match

SCAN-03: async void (JS-033 -- P0 CRITICAL)
  Command : grep -n "async void " src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in CancelAllAccountOrders (method is synchronous void)
  Pass    : zero matches in the modified method
  Fail    : any match on CancelAllAccountOrders

SCAN-04: return null (JS-002 -- P0 CRITICAL)
  Command : grep -n "return null;" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in CancelAllAccountOrders (void method uses bare return;)
  Pass    : zero matches in the modified method
  Fail    : any match in modified lines

SCAN-05: throw new (JS-001 -- P0 CRITICAL)
  Command : grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in CancelAllAccountOrders
  Pass    : zero matches in the modified method
  Fail    : any match in modified lines

SCAN-06: CYC <= 8
  Command : python scripts/complexity_audit.py
  Expected: CancelAllAccountOrders reports CYC=4 (structural)
  Also    : Verify L711 comment reads exactly:
            "CYC=4: null-guard(1) + foreach(2) + stateOk-4terms(3) + instrument-name(4). JS-021: no lock."
  Pass    : CYC=4, comment matches, complexity_audit.py shows <= 8
  Fail    : CYC > 8 OR comment still reads old text OR tool reports violation

SCAN-07: Build
  Command : powershell -File .\scripts\build_readiness.ps1
  Also    : dotnet csharpier check src/
  Expected: 0 errors, 0 warnings, 0 formatting issues
  Pass    : both commands exit with 0 errors/warnings/issues
  Fail    : any error, warning, or formatting issue
```

### Pass/Fail Criteria (TICKET-1)

| Criterion | Verification | Pass |
|-----------|-------------|------|
| `OrderState.ChangeSubmitted` removed from `stateOk` | SCAN-06 IL + code review | No ChangeSubmitted in stateOk block |
| `RemoveAll(Filled \|\| Cancelled)` inserted after foreach, before Count==0 guard | Code review L728-732 | Present in correct location |
| L710 comment updated (no ChangeSubmitted in States list) | Code review L710 | Comment reads `...ChangePending.` |
| L711 CYC comment updated | SCAN-06 | Reads `stateOk-4terms(3)` |
| L2662 MoveStopToBreakEven unchanged | `git diff` -- L2662 not in diff | Not in diff |
| New [Fact] test passes | `dotnet test` | 292/292 pass |
| All 7 scans green | Scans 1-7 above | All pass |

---

## TICKET-2: DW-B79-LOG-01 (P3)

### Spec Requirements Satisfied

| Req ID | Description |
|--------|-------------|
| DW-B79-LOG-01-R1 | Capture `bool` from `_pendingFollowerBeSlots.TryRemove` |
| DW-B79-LOG-01-R2 | Gate `Output.Process` log on `slotEvicted` bool |
| DW-B79-LOG-01-R3 | `_beReplaceAttempts.TryRemove` remains unconditional |

### File Path

```
src/PropTraderTools/CopyEngine.cs    (method: TryEvictFollowerBeSlot, L1075-1089)
```

### Method Signature (unchanged)

```csharp
private void TryEvictFollowerBeSlot(OrderEventArgs e)
```

### Before Code (verbatim, L1082-1086 context)

```csharp
            _pendingFollowerBeSlots.TryRemove(accName, out _);                     // no-op if already consumed
            _beReplaceAttempts.TryRemove(accName, out _);                          // ALWAYS reset on flat
            NinjaTrader.Code.Output.Process(
                "[BE-RETRY] " + accName + " position closed -- evicted BE slot + reset attempt counter",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

### After Code (engineer writes exactly this, L1082-1089)

```csharp
            bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);  // DW-B79-04: capture for log gate
            _beReplaceAttempts.TryRemove(accName, out _);                          // ALWAYS reset on flat
            if (slotEvicted)                                                        // DW-B79-04: only log if slot was present
            {
                NinjaTrader.Code.Output.Process(
                    "[BE-RETRY] " + accName + " position closed -- evicted BE slot + reset attempt counter",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            }
```

### Change Summary

| Change | Location | Description |
|--------|----------|-------------|
| A | L1082 | Replace `_pendingFollowerBeSlots.TryRemove(accName, out _);` with `bool slotEvicted = ...` capturing return value |
| B | L1083-1086 | Wrap `Output.Process(...)` in `if (slotEvicted) { ... }` |

**Key invariant**: `_beReplaceAttempts.TryRemove(accName, out _)` at L1083 is NOT inside the
`if (slotEvicted)` gate. The comment `// ALWAYS reset on flat` must be preserved verbatim.
Only the `NinjaTrader.Code.Output.Process(...)` call is gated.

**CYC update**: The method gains 1 decision point from `if (slotEvicted)`. CYC goes from 3 to 4.
If the method opening comment contains a CYC annotation, update it to read `CYC=4` and add:
`// CYC=4: filled-guard(1) + follower-guard(2) + flat-guard(3) + slotEvicted-gate(4). JS-021: no lock.`

### JS Rule Constraints

| Rule | Constraint | Verification |
|------|-----------|--------------|
| JS-001 | No `throw new` in `TryEvictFollowerBeSlot` | SCAN-05 |
| JS-002 | No `return null` (`void` method, bare `return;` only) | SCAN-04 |
| JS-021 | No `lock()` -- `_pendingFollowerBeSlots` and `_beReplaceAttempts` are `ConcurrentDictionary` (lock-free) | SCAN-02 |
| JS-033 | Method is synchronous `void` -- no `async` keyword | SCAN-03 |
| CYC<=8 | CYC=4 after change: filled-guard + follower-guard + flat-guard + slotEvicted-gate | SCAN-06 |
| ASCII | `bool slotEvicted` and inline comment are ASCII-only; `[BE-RETRY]` log string unchanged | SCAN-01 |

### xUnit [Fact] Test

No new test required for TICKET-2. The change is a pure log-gate; it does not alter any observable
state, return value, or branching outside of the `Output.Process` call. The `_beReplaceAttempts`
reset path is unchanged and unconditional.

**Regression verification**: Run the existing 291 [Fact] tests (292 after TICKET-1). All must
pass without modification. If any existing test references `TryEvictFollowerBeSlot` behavior,
confirm it still passes after applying TICKET-2.

```
dotnet test src/PropTraderTools/Tests/PropTraderTools.Tests.csproj
Expected: 292/292 pass (291 pre-existing + 1 from TICKET-1)
```

### 7-Scan Checklist (TICKET-2)

Engineer must run all 7 scans and confirm each result before marking TICKET-2 complete.

```
SCAN-01: ASCII-only
  Command : grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in new/modified lines (L1082-1089)
  Pass    : zero matches
  Fail    : any non-ASCII character in new or modified lines

SCAN-02: lock() ban (JS-021 -- P0 CRITICAL)
  Command : grep -n "lock(" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results (no lock() anywhere in file)
  Pass    : zero matches
  Fail    : any match

SCAN-03: async void (JS-033 -- P0 CRITICAL)
  Command : grep -n "async void " src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in TryEvictFollowerBeSlot (method is synchronous void)
  Pass    : zero matches in the modified method
  Fail    : any match on TryEvictFollowerBeSlot

SCAN-04: return null (JS-002 -- P0 CRITICAL)
  Command : grep -n "return null;" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in TryEvictFollowerBeSlot (void method uses bare return;)
  Pass    : zero matches in the modified method
  Fail    : any match in modified lines

SCAN-05: throw new (JS-001 -- P0 CRITICAL)
  Command : grep -n "throw new" src/PropTraderTools/CopyEngine.cs
  Expected: 0 results in TryEvictFollowerBeSlot
  Pass    : zero matches in the modified method
  Fail    : any match in modified lines

SCAN-06: CYC <= 8
  Command : python scripts/complexity_audit.py
  Expected: TryEvictFollowerBeSlot reports CYC=4 (structural)
  Also    : If method has a CYC annotation comment, verify it reads:
            "CYC=4: filled-guard(1) + follower-guard(2) + flat-guard(3) + slotEvicted-gate(4). JS-021: no lock."
  Pass    : CYC=4, complexity_audit.py shows <= 8
  Fail    : CYC > 8 OR tool reports violation

SCAN-07: Build
  Command : powershell -File .\scripts\build_readiness.ps1
  Also    : dotnet csharpier check src/
  Expected: 0 errors, 0 warnings, 0 formatting issues
  Pass    : both commands exit with 0 errors/warnings/issues
  Fail    : any error, warning, or formatting issue
```

### Pass/Fail Criteria (TICKET-2)

| Criterion | Verification | Pass |
|-----------|-------------|------|
| `bool slotEvicted` captures `TryRemove` return value | Code review L1082 | Variable declared, return value captured |
| `Output.Process` wrapped in `if (slotEvicted) { ... }` | Code review L1084-1089 | Log is gated |
| `_beReplaceAttempts.TryRemove` remains unconditional (not inside if-gate) | Code review L1083 | Still before the if block |
| `// ALWAYS reset on flat` comment preserved | Code review L1083 | Comment intact |
| CYC comment updated to CYC=4 (if annotation present) | SCAN-06 | Reads CYC=4 with 4 guards listed |
| All 292 [Fact] tests pass (291 + 1 from TICKET-1) | `dotnet test` | 292/292 pass |
| All 7 scans green | Scans 1-7 above | All pass |

---

## Combined Acceptance Criteria

| Criterion | Verification Method |
|-----------|---------------------|
| 292 `[Fact]` tests pass (291 existing + 1 new from T1) | `dotnet test src/PropTraderTools/Tests/` |
| Zero "Cancellation rejected" OMS popups in live session | Manual live-trading validation |
| `[BE-RETRY]` evict log fires exactly once per trade close | Live session Output tab observation |
| L2662 `ChangeSubmitted` in `MoveStopToBreakEven` unchanged | `git diff -- src/PropTraderTools/CopyEngine.cs` shows L2662 not in diff |
| CYC <= 8 for both modified methods | `python scripts/complexity_audit.py` |
| Build: 0 errors, 0 warnings | `powershell -File .\scripts\build_readiness.ps1` |
| No `lock()` usage | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` returns 0 |
| ASCII-only in all new lines | SCAN-01 passes for both tickets |
| `dotnet csharpier check src/` clean | SCAN-07 |

---

*Tickets complete. ptt-engineer: implement from TICKET-1 first, then TICKET-2. Run 7-scan checklist after each ticket independently before proceeding to the next.*
