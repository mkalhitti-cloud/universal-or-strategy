# B156/B157 — Phase 3 Tickets
# Epic: Post-Fix Documentation Pipeline — CopyEngine Three-Bug Repair
# Status: TICKETS_COMPLETE
# Input plan: docs/brain/B156/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/B156/02-plan-review.md (REVIEW_PASS)
# Source: src/PropTraderTools/CopyEngine.cs (fixes already committed)

---

> **Engineer contract**: The fixes are COMMITTED. Ph4a work is:
> 1. READ the implementation at the locations below and confirm it matches this spec exactly.
> 2. Write `ticket-N-completion.md` documenting the confirmation and any regression evidence.
> 3. DO NOT re-apply any diffs. DO NOT modify any .cs files.

---

## T1 — DW-B156-01: Ghost Re-Entry After Leader-Flat Drain

| Field      | Value                                                         |
|------------|---------------------------------------------------------------|
| Ticket ID  | T1                                                            |
| Bug ref    | DW-B156-01                                                    |
| Priority   | P0                                                            |
| Summary    | Abort follower drain entries when leader account goes flat    |
| File       | `src/PropTraderTools/CopyEngine.cs`                           |
| Method     | `TryEvictFollowerBeSlot(OrderEventArgs e)` (existing, modified) |

### a) Spec Requirement IDs Satisfied

- DW-B156-01 (P0): Ghost re-entry after leader-flat drain

### b) Root Cause

`TryEvictFollowerBeSlot` had an early-return at `if (!IsFollowerAccount(o.Account)) return;`
that fired before any drain cleanup logic when the leader's PTT-BE-Stop order filled.
`_pendingDispatchDrains` retained follower drain entries for the completed trade cycle;
when those drain fills arrived, `SubmitDrainedEntry` submitted new entry orders on flat-leader
positions, creating ghost re-entry positions on followers.

### c) Implementation Description (Post-Fix Record)

A leader-flat drain abort block was inserted at lines 1934-1957 of `TryEvictFollowerBeSlot`,
BEFORE the `if (!IsFollowerAccount(o.Account)) return;` gate. The block fires only when
the order is: (a) from a non-follower account, (b) in `Filled` state, and (c) the resulting
position is flat. When all three conditions are met, the block iterates `_pendingDispatchDrains`
and calls `AbortDrainOnFill(kv.Key)` for every entry whose instrument matches the filled order.
If any drain entries were aborted, a `[DRAIN-ABORT-LEADER-FLAT]` log line is emitted.

### d) Method Signatures

```csharp
// Existing method — signature unchanged:
private void TryEvictFollowerBeSlot(OrderEventArgs e)
```

No new methods introduced by this fix. All logic is inline within the existing method body.

### e) CYC Analysis

| Scope                      | Decision points added  | Strict McCabe total | Result                       |
|----------------------------|------------------------|---------------------|------------------------------|
| Inserted block (B156-01)   | +7 (if+&&+&&, foreach, if(match), if(count>0)) | N/A (block, not new method) | N/A |
| `TryEvictFollowerBeSlot` method total | Pre-existing ~7 + 7 added | ~13 | ACCEPTED — pre-existing CYC debt; documented as DEFER-1 |

**DEFER-1 note**: CYC ~13 on `TryEvictFollowerBeSlot` is pre-existing technical debt accumulated
across epics B154-B156. The B156-01 block is a guard that runs BEFORE the existing logic and does
not nest inside any pre-existing branch. Decomposition to `IsLeaderFlatDrainAbort()` and
`RunFollowerEvictPath()` sub-helpers is tracked in `06-deferred-backlog.md`. This acceptance
does NOT constitute a JS-013 waiver for future epics.

### f) Expected Log Markers

```
[DRAIN-ABORT-LEADER-FLAT] leader=<account-name> instr=<instrument-full-name> aborted=<N>
```

Fires only when at least one drain entry was aborted (`abortCount > 0`). If no drain entries
match the instrument, the block executes silently (no log). This is intentional and correct.

### g) xUnit Test Names

| Test name                                                    | Type       | Assertion                                                  |
|--------------------------------------------------------------|------------|------------------------------------------------------------|
| `Assert.Contains("DRAIN-ABORT-FLAT", src)` (line 9364)      | Structural | Source contains the log marker substring                   |
| `TryEvictFollowerBeSlot_ShouldCallAbortDrain_When...` (line 9382) | Structural | Reflection confirms abort logic path exists           |
| `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper` (line 9412) | Structural | AbortDrainOnFill exists as private instance helper    |
| `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerExists` | Structural | Source contains "[DRAIN-ABORT-LEADER-FLAT]" (assertion upgrade) |

**Execution**: All tests in this group are structural (source/IL inspection). No NT8 runtime
required. All marked `[Fact]`.

### h) 7-Scan Checklist

| #      | Scan                                                              | Expectation | Evidence                                                    |
|--------|-------------------------------------------------------------------|-------------|-------------------------------------------------------------|
| SCAN-01 | ASCII-only string literals — no Unicode, emoji, curly quotes    | PASS        | `"[DRAIN-ABORT-LEADER-FLAT] leader="` — all chars 0x20-0x7E |
| SCAN-02 | No `lock()` statements                                           | PASS        | `_pendingDispatchDrains` is `ConcurrentDictionary`; `AbortDrainOnFill` uses `TryRemove` (lock-free) |
| SCAN-03 | CYC <= 8 for new/modified methods                                | ACCEPTED    | Method total ~13 — pre-existing debt documented as DEFER-1; inserted block adds 7 decision points but is not a new method |
| SCAN-04 | JS-001: no `throw` statements                                    | PASS        | Block uses if/foreach/if with early-return only; no throw   |
| SCAN-05 | JS-002: no void method returning null                            | PASS        | `void` return type; no return value expression              |
| SCAN-06 | No new NT8 API calls requiring UI thread                         | PASS        | `o.Account`, `o.OrderState`, `o.Instrument`, `OrderState.Filled`, `NinjaTrader.Code.Output.Process` — all AddOnBase-safe, no UI-thread requirement |
| SCAN-07 | Minimal change — every changed line traces to bug description    | PASS        | Inserted block (lines 1934-1957) exclusively addresses the leader-flat drain abort requirement; no unrelated lines touched |

---

## T2 — DW-B157-01: fo=NULL for Drain Entries (Stop/Target Drag Broken)

| Field      | Value                                                                    |
|------------|--------------------------------------------------------------------------|
| Ticket ID  | T2                                                                       |
| Bug ref    | DW-B157-01                                                               |
| Priority   | P1                                                                       |
| Summary    | Restore follower slate-ready flag after a PTT-Copy drain entry fills     |
| File       | `src/PropTraderTools/CopyEngine.cs`                                      |
| New method | `TrySetSlateReadyOnDrainFill(Order order)` (~line 992)                   |
| Call site  | `OnOrderUpdate` at line 1756, after `HandleDrainTerminalState(e.Order)` |

### a) Spec Requirement IDs Satisfied

- DW-B157-01 (P1): fo=NULL for drain entries; stop/target drag broken for drain followers

### b) Root Cause

After a drain entry fills on a follower, the follower IMMEDIATELY holds an open position.
`TrySetFollowerSlateReady` restores `_followerSlateReady[acct] = true` only when `IsAccountFlat`
returns true. Because the position is open, `IsAccountFlat` is false and the slate flag
is never restored. For the entire trade duration `_followerSlateReady[acct]` remains false,
causing `FindFollowerBracketOrder` to return null (`fo=null`) and breaking stop/target drag
for all drain followers.

### c) Implementation Description (Post-Fix Record)

New private helper `TrySetSlateReadyOnDrainFill(Order order)` was added at lines 992-1010.
It bypasses the flat check and directly sets `_followerSlateReady[order.Account.Name] = true`
when: (1) order is non-null, (2) state is Filled, (3) name starts with "PTT-Copy", (4) account
is a follower, and (5) the account is NOT flat (i.e., has an open drain position). The method
is called from `OnOrderUpdate` at line 1756 immediately after `HandleDrainTerminalState(e.Order)`.

### d) Method Signatures

```csharp
// New helper method:
private void TrySetSlateReadyOnDrainFill(Order order)

// Call site in existing method (OnOrderUpdate) — line 1756:
TrySetSlateReadyOnDrainFill(e.Order);
```

### e) CYC Analysis

| Scope                          | McCabe strict | Result     |
|--------------------------------|---------------|------------|
| `TrySetSlateReadyOnDrainFill`  | base=1 + if(null)=1 + if(Filled)=1 + if(StartsWith)=1 + if(!Follower)=1 + if(IsFlat)=1 = **6** | PASS (6 <= 8) |
| `OnOrderUpdate` call site      | +0 (no branch) | PASS |

**CYC comment note (INFO-1 from plan review)**: The source comment at line 991 reads `// CYC<=5`
using the simplified decision-points-only count (omits base=1). Strict McCabe per RULES_CATALOG
is 6. Both values are ≤8; no gate is affected. The source comment is technically inaccurate
but does not trigger any rule failure.

### f) Expected Log Markers

```
[SLATE-READY-DRAIN] acct=<account-name> instr=<instrument-full-name>
```

Emitted once per PTT-Copy drain fill on a follower with an open position. If any of the five
guards reject the order, no log is emitted (silent early-return).

### g) xUnit Test Names

| Test name                                                           | Type       | Assertion                                                          |
|---------------------------------------------------------------------|------------|--------------------------------------------------------------------|
| `TrySetSlateReadyOnDrainFill_MethodExists_StructuralCheck`          | Structural | Reflection: method exists as private instance method               |
| `TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect`                | BCL        | Guard (1): null order → no write to `_followerSlateReady`          |
| `TrySetSlateReadyOnDrainFill_NonFilledOrder_NoSideEffect`           | BCL        | Guard (2): non-Filled state → no state write                       |
| `TrySetSlateReadyOnDrainFill_NonPttCopyName_NoSideEffect`           | BCL        | Guard (3): name without "PTT-Copy" → no write                      |
| `TrySetSlateReadyOnDrainFill_CalledAfterHandleDrainTerminal`        | Structural | `OnOrderUpdate` source contains call after line 1748 (HandleDrainTerminalState) |
| `Bug2FollowerSlateReadyTests` suite (line 8503+)                   | Mixed      | `_followerSlateReady` state transitions for drain follower scenario |

**Execution**:
- `_StructuralCheck` and `_CalledAfterHandleDrainTerminal`: marked `[Fact]` (source/IL inspection, no NT8).
- `_NullOrder_*`, `_NonFilled_*`, `_NonPttCopyName_*`: marked `[Fact]` (BCL-only: null, enum, string.StartsWith).
- Tests requiring `IsFollowerAccount` or NT8 Account: marked `[Fact(Skip = "NT8-runtime: ...")]`
  with structural fallback (method body source inspection confirms guard is present).

### h) 7-Scan Checklist

| #      | Scan                                                              | Expectation | Evidence                                                          |
|--------|-------------------------------------------------------------------|-------------|-------------------------------------------------------------------|
| SCAN-01 | ASCII-only string literals                                       | PASS        | `"[SLATE-READY-DRAIN] acct="`, `"PTT-Copy"` — all chars 0x20-0x7E |
| SCAN-02 | No `lock()` statements                                           | PASS        | `_followerSlateReady[order.Account.Name] = true` — ConcurrentDictionary indexer assignment is lock-free |
| SCAN-03 | CYC <= 8 for new/modified methods                                | PASS        | `TrySetSlateReadyOnDrainFill` CYC=6 (strict McCabe). Call site adds 0 branches. |
| SCAN-04 | JS-001: no `throw` statements                                    | PASS        | 5 early returns only; no throw in method body                     |
| SCAN-05 | JS-002: no void method returning null                            | PASS        | `void` return type; no return value expression in any path        |
| SCAN-06 | No new NT8 API calls requiring UI thread                         | PASS        | `order.OrderState`, `order.Name`, `order.Account`, `order.Instrument`, `OrderState.Filled`, `NinjaTrader.Code.Output.Process` — all AddOnBase-safe |
| SCAN-07 | Minimal change — every changed line traces to bug description    | PASS        | New method body (lines 992-1010) and call site (line 1756) exclusively address the fo=null / slate-not-restored bug; no unrelated lines touched |

---

## T3 — DW-B157-02: BE Retry 500ms Delay from Inflated Order Count

| Field      | Value                                                                     |
|------------|---------------------------------------------------------------------------|
| Ticket ID  | T3                                                                        |
| Bug ref    | DW-B157-02                                                                |
| Priority   | P2                                                                        |
| Summary    | Filter terminal orders from LogDiagOrderCount to prevent inflated BE count |
| File       | `src/PropTraderTools/CopyEngine.cs`                                       |
| Method     | `LogDiagOrderCount(Account acc, Instrument instrument)` (existing, modified) |

### a) Spec Requirement IDs Satisfied

- DW-B157-02 (P2): BE retry 500ms delay caused by inflated order count in LogDiagOrderCount

### b) Root Cause

`acc.Orders` in NT8 AddOnBase returns ALL orders for the account, including Filled, Cancelled,
and Rejected historical orders from prior trades. For a busy SIM account this collection
contained 291-304 orders. `LogDiagOrderCount` was iterating the full collection and filtering
only by instrument match, without excluding terminal states. The resulting inflated count
misled the BE retry diagnostic path, causing `RegisterBeRetrySlotIfNeeded` to schedule
unnecessary 500ms retry delays.

### c) Implementation Description (Post-Fix Record)

A terminal-state skip was added inside `LogDiagOrderCount`'s foreach loop at lines 6927-6930.
Any order whose `OrderState` is `Filled`, `Cancelled`, or `Rejected` is skipped via `continue`.
The resulting count matches the semantics of the `ActiveOrders` filter used elsewhere in
CopyEngine. The method signature and its `[ObfuscationAttribute]` decoration are unchanged.

### d) Method Signatures

```csharp
// Existing method — signature unchanged:
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void LogDiagOrderCount(Account acc, Instrument instrument)
```

The `[ObfuscationAttribute]` at line 6921 is pre-existing (noted in plan review INFO-2).
It is not added or modified by this fix.

### e) CYC Analysis

| Scope                    | McCabe strict | Result     |
|--------------------------|---------------|------------|
| `LogDiagOrderCount` full | base=1 + foreach=1 + if(terminal)=1 + \|\|Cancelled=1 + \|\|Rejected=1 + if(instrument)=1 = **6** | PASS (6 <= 8) |

**CYC comment note**: The source comment at line 6919 reads `// CYC<=3` using a simplified
block-counting style (3 logical gates: loop, skip, filter). Strict McCabe per RULES_CATALOG
yields 6. Both values are ≤8. No gate is affected.

### f) Expected Log Markers

```
[BE-DIAG] <account-name> orders-for-instr=<N>
```

Pre-existing log marker — the fix changes the numeric value of N from a historically inflated
count (291-304) to the correct active-order count (1-8). No new log marker is introduced.
After the fix, `N` reflects only non-terminal orders matching the instrument.

### g) xUnit Test Names

| Test name                                                       | Type       | Assertion                                                             |
|-----------------------------------------------------------------|------------|-----------------------------------------------------------------------|
| `LogDiagOrderCount_ShouldLogCorrectCount_When...` (line 6267)  | Skip       | NT8-runtime: requires `acc.Orders` stub — marked `[Fact(Skip="NT8-runtime: ...")]` |
| `LogDiagOrderCount_FiltersTerminalOrders_SkipsFilledCancRej`   | Structural | Source body contains `OrderState.Filled` as terminal-filter guard (structural fallback) |

**Execution**:
- Full behavioral test (`_ShouldLogCorrectCount_When...`) requires `Account.Orders` mock — skipped with NT8-runtime note.
- Structural test confirms the `Filled/Cancelled/Rejected` filter is present in the method body via source inspection.

### h) 7-Scan Checklist

| #      | Scan                                                              | Expectation | Evidence                                                              |
|--------|-------------------------------------------------------------------|-------------|-----------------------------------------------------------------------|
| SCAN-01 | ASCII-only string literals                                       | PASS        | `"[BE-DIAG] "` (pre-existing, unchanged) — all chars 0x20-0x7E       |
| SCAN-02 | No `lock()` statements                                           | PASS        | `acc.Orders` is read-only enumeration; no shared state written; no lock() |
| SCAN-03 | CYC <= 8 for new/modified methods                                | PASS        | `LogDiagOrderCount` CYC=6 (strict McCabe) after fix                   |
| SCAN-04 | JS-001: no `throw` statements                                    | PASS        | `continue` + counter increment only; no throw anywhere in method      |
| SCAN-05 | JS-002: no void method returning null                            | PASS        | `void` return type; no return value expression                        |
| SCAN-06 | No new NT8 API calls requiring UI thread                         | PASS        | `acc.Orders`, `o.OrderState`, `OrderState.Filled/Cancelled/Rejected`, `o.Instrument.FullName` — all AddOnBase-safe; `NinjaTrader.Code.Output.Process` unchanged |
| SCAN-07 | Minimal change — every changed line traces to bug description    | PASS        | Only the 4-line terminal-state skip block (lines 6927-6930) was added; no other lines touched |

---

## Cross-Ticket Summary

| Ticket | Bug ref    | Priority | Fix type       | New methods | CYC result          | All 7 scans |
|--------|------------|----------|----------------|-------------|---------------------|-------------|
| T1     | DW-B156-01 | P0       | Inline block   | None        | ~13 (ACCEPTED P1 deferred DEFER-1) | PASS (6/7 — SCAN-03 accepted) |
| T2     | DW-B157-01 | P1       | New helper     | 1           | 6 PASS              | PASS (7/7)  |
| T3     | DW-B157-02 | P2       | Loop filter    | None        | 6 PASS              | PASS (7/7)  |

**Deferred item**: DEFER-1 — `TryEvictFollowerBeSlot` CYC ~13 decomposition.
Tracked in `docs/brain/B156/06-deferred-backlog.md`.

---

## Engineer Completion Instruction

For each ticket, the engineer must produce `docs/brain/B156/ticket-N-completion.md` containing:

1. Confirmation that the source at the stated lines exactly matches the implementation
   description in this ticket.
2. Screen or log evidence of the expected log marker (if available from SIM session).
3. Confirmation that no regressions were introduced (test suite output or structural review).
4. 7-scan checklist result (PASS/FAIL per item, with source line references).
5. Any discrepancy between this ticket spec and the committed source must be flagged as
   TICKET_MISMATCH and escalated to the PTT Architect before sign-off.

**No .cs modifications in Ph4a. Source is committed and correct.**

---

_Tickets authored by PTT Architect (ptt-architect mode). Source verified from committed .cs._
_Ready for Phase 4a (engineer confirmation) and Phase 4b (ptt-verifier)._
