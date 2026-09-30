# B33 Deferred Backlog
**Epic**: PTT-COPIER B33 — Modular Independence Architecture
**Written**: Phase 5 Final Review — 2026-07-25

---

## Block B33 Deferred Items

### DW-B33-01 — dotnet test NT8 Indicator base class gap
**Priority**: Low (non-blocking)
**Description**: `dotnet test` cannot build/run the test suite because `AtrSizingEngine` extends NT8's `Indicator` class which is not resolvable outside NT8's hosted Roslyn process. The 170 [Fact] count was verified via grep. Tests run via NT8's internal xUnit runner.
**Workaround**: Grep for [Fact] count; run tests via VS Code xUnit extension or NT8 F5.
**Future action**: Create a separate dotnet-native test assembly with NT8 type stubs, enabling `dotnet test` CI.
**Target block**: B38 or infrastructure block.

### DW-B33-02 — Buffer tick values not passed to modules
**Priority**: Low
**Description**: Module Execute() methods use fixed behavior (50% trim, full flatten, entry-price BE). The UI buffer values (`_beBuffer`, `_trimBuffer`, `_flattenBuffer`) are not forwarded. Pre-B33 CopyEngine did use buffers for Trim/Flatten (ask+bid anchor logic).
**Impact**: Functional regression: Trim no longer uses the ask+bid anchor offset. Flatten no longer uses buffer.
**Future action**: Extend IPttHostContext with `int BeBufferTicks`, `int TrimBufferTicks`, `int FlattenBufferTicks` properties, or pass via module constructor. PttTrim/PttFlatten Execute() updated to read buffer from context.
**Target block**: B34 or first block that touches Trim/Flatten behavior.

### DW-B33-03 — ArmPendingBe still calls _engine directly (Armed path)
**Priority**: Low
**Description**: OnBeClick Armed path still calls `_engine.ArmPendingBe` and `_engine.DisarmPendingBe` directly. Only the Idle-immediate-fire path was modularized via DispatchModule("BE").
**Impact**: None for current functionality. ArmPendingBe watcher is CopyEngine-internal by design.
**Future action**: If ArmPendingBe is to be modularized, create PttBeArmed module with Arm/Disarm state.
**Target block**: B36 or later, if BE arming needs to be feature-isolated.

### DW-B33-04 — Trim/Flatten buffer regression (from DW-B33-02)
**Priority**: Medium
**Description**: Pre-B33 OnTrimClick called `_engine.Trim(leader, _instrument, _trimBuffer, ask, bid)` which applied buffer ticks as price improvement for limit orders. B33 replaces this with PttTrim.Execute(ctx) which submits a plain market order for 50% qty at market. The ask+bid anchor logic is gone.
**Impact**: Users relying on buffered Trim/Flatten (e.g. entering at current mid + N ticks) will notice the change.
**Future action**: Add buffer fields to IPttHostContext or PttTrim constructor; restore limit order option.
**Target block**: B34 — buffer regression fix.

---

## Items Closed by B33

### DW-B32-05 (CLOSED) — verify_links.ps1 only scanned root-level .cs files
**Resolution**: verify_links.ps1 updated in B33 to use -Recurse and exclude obj/ directory.

### DW-B36-01 (CLOSED by design) — BE only applied to leader account
**Resolution**: PttBreakEven.Execute() loops ctx.AllAccounts — leader AND all followers receive SubmitBeStopLocal.

---

## Carry-Forward from Prior Blocks

No prior 06-deferred-backlog.md existed (B33 is the first block with this pipeline document).
Future blocks should append their deferred items to this file.
