# 04-tickets.md — PTT-REPAIRS-DW-E-04

**Status**: TICKETS_COMPLETE  
**Phase**: 3 (Ticket Generation)  
**Author**: ptt-architect  
**Epic**: PTT-REPAIRS-DW-E-04  
**Source plan**: `docs/brain/PTT-REPAIRS-DW-E-04/02-architecture-plan.md` (REVIEW_PASS)  
**Plan review**: `docs/brain/PTT-REPAIRS-DW-E-04/02-plan-review.md` (REVIEW_PASS, 0 violations)

---

## Ticket Count

Single ticket: **T1** (SINGLE-PIPELINE confirmed — both extractions from the same 50-line
method body, same file; Q1 = YES, STOP rule applied).

---

# T1 — EvictDedup CYC Reduction via Extraction (CopyEngine + Test)

## 1. Spec Requirement IDs Satisfied

| Rule ID | Description | Applies To |
|---------|-------------|------------|
| JS-013 | All methods CYC ≤ 8 (McCabe) | `EvictDedup`, `EvictCancelledEntry`, `EvictFilledEntry` |
| JS-021 | No `lock()` statement | All three methods |
| JS-001 | No `throw` statement | All three methods |
| JS-002 | No null return | All three methods (all `void`) |
| JS-042 | ASCII-only identifiers and string literals | All three methods, all comments |

---

## 2. Files to Modify

| File | Wave Workspace Path | Action |
|------|---------------------|--------|
| CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | Rewrite `EvictDedup` body; add two new private methods below it |
| CopyEngineTests.cs | `src/PropTraderTools.Tests/CopyEngineTests.cs` | Add one new `[Fact]` test |

**Do NOT touch**:
- Line 1541: `EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);` — must remain UNCHANGED.
- Lines outside 5851–5901 in `CopyEngine.cs` — do not drift into adjacent methods.
- `EvictDedup_ForTest` at line 4360 — thin shim; do not modify.
- Pre-existing `return null` lines (DW-WAVE2-LA-03 scope, out of scope for this epic).

---

## 3. Method Signatures (Exact)

All three signatures below are the engineer contract. Names, parameter names, parameter types,
return types, and accessibility modifiers must match exactly.

```csharp
// Unchanged signature — call site at line 1541 requires no modification.
internal void EvictDedup(string orderId, OrderState state)

// New private helper — receives the orderId and the instrKey already extracted from the map.
private void EvictCancelledEntry(string orderId, string cancelledInstrKey)

// New private helper — receives the orderId and the instrKey already extracted from the map.
private void EvictFilledEntry(string orderId, string filledInstrKey)
```

---

## 4. CYC Targets (JS-013 limit = 8)

| Method | Post-Extraction CYC | Limit | Arithmetic |
|--------|---------------------|-------|------------|
| `EvictDedup` | **7** | 8 ✓ | Base(1) + `&&`x2 terminal guard(+2) + `if Cancelled`(+1) + `if TryRemove cancelledInstrKey`(+1) + `if Filled`(+1) + `if TryRemove filledInstrKey`(+1) = 7 |
| `EvictCancelledEntry` | **4** | 8 ✓ | Base(1) + `if(TryGetValue && storedId==orderId)` if(+1) + `&&`(+1) + `if(pipeIdx > 0)`(+1) = 4 |
| `EvictFilledEntry` | **3** | 8 ✓ | Base(1) + `if(TryGetValue && storedId==orderId)` if(+1) + `&&`(+1) = 3 |

---

## 5. Exact Method Bodies to Implement

### 5.1 EvictDedup (residual body — replace lines 5851–5901)

Replace the existing `EvictDedup` body **in place** (same line range, same surrounding comments).
The header comment block at lines 5846–5850 must be updated to reflect PTT-REPAIRS-DW-E-04 CYC=7.

```csharp
        // B62: evict dedup entry when order reaches terminal state (Filled/Cancelled/Rejected).
        // Called unconditionally from OnOrderUpdate pre-gate, after TryFirePositionState.
        // Ensures evicted orderId can be re-used for the next fresh order on the same instrument.
        // PTT-REPAIRS-DW-E-04: CYC=7. Extracted Cancelled/Filled branch bodies to
        // EvictCancelledEntry and EvictFilledEntry to satisfy JS-013 (CYC<=8).
        // Signature unchanged: call site at line 1541 must not change.
        // JS-021: no lock -- all ConcurrentDictionary ops are lock-free.
        // JS-001: no throw. JS-042: ASCII-only.
        internal void EvictDedup(string orderId, OrderState state)
        {
            if (
                state != OrderState.Filled
                && state != OrderState.Cancelled
                && state != OrderState.Rejected
            )
                return;

            _dedupCache.TryRemove(orderId, out _);

            if (state == OrderState.Cancelled)
            {
                // DW-B142-MGC-02: scoped removal -- do NOT Clear() the whole map.
                _entryDispatchedOrders.TryRemove(orderId, out _);
                if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
                    EvictCancelledEntry(orderId, cancelledInstrKey);
            }

            if (state == OrderState.Filled)
            {
                if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
                    EvictFilledEntry(orderId, filledInstrKey);
            }
            // DW-B91-A-v2: Filled/Rejected _entryDispatchedOrders eviction handled in TryEvictFollowerBeSlot.
        }
```

### 5.2 EvictCancelledEntry (new private method — insert immediately after EvictDedup closing brace)

```csharp
        // PTT-REPAIRS-DW-E-04: Extracted from EvictDedup Cancelled branch.
        // Value-guarded removal of liveEntryInstruments guard.
        // BUG-E: clears stale _lastLeaderDirection when entry cancelled without fill.
        // JS-021: no lock. JS-001: no throw. JS-042: ASCII-only.
        // CYC=4: base(1) + TryGetValue-&&-guard(2) + &&(3) + pipeIdx-guard(4).
        private void EvictCancelledEntry(string orderId, string cancelledInstrKey)
        {
            string storedId;
            if (_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)
                && storedId == orderId)
                _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
            // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
            // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
            var pipeIdx = cancelledInstrKey.IndexOf('|');
            if (pipeIdx > 0)
                _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
        }
```

### 5.3 EvictFilledEntry (new private method — insert immediately after EvictCancelledEntry closing brace)

```csharp
        // PTT-REPAIRS-DW-E-04: Extracted from EvictDedup Filled branch.
        // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.
        // PTT-REPAIRS-03-POST: value-guarded removal -- TOCTOU-safe pattern as Cancelled.
        // JS-021: no lock. JS-001: no throw. JS-042: ASCII-only.
        // CYC=3: base(1) + TryGetValue-&&-guard(2) + &&(3).
        private void EvictFilledEntry(string orderId, string filledInstrKey)
        {
            string storedId;
            if (_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)
                && storedId == orderId)
                _liveEntryInstruments.TryRemove(filledInstrKey, out _);
        }
```

---

## 6. BUG-E Fix Preservation (CRITICAL — verbatim contract)

The following four lines **must appear verbatim** inside `EvictCancelledEntry`,
immediately after the `TryRemove(cancelledInstrKey, out _)` call:

```csharp
            // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
            // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
            var pipeIdx = cancelledInstrKey.IndexOf('|');
            if (pipeIdx > 0)
                _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```

**Preservation rules — engineer MUST verify each before submission**:

1. Both comment lines appear verbatim (exact wording, exact dashes, ASCII-only).
2. `cancelledInstrKey.IndexOf('|')` — exact method call, character literal `'|'` (pipe, not dash, not slash).
3. Guard is `if (pipeIdx > 0)` — not `>= 0`, not `!= -1`.
4. `_lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _)` — exact call, no intermediate variable.
5. This code must **not** be moved to `EvictDedup` or anywhere else. It belongs only inside `EvictCancelledEntry`.
6. Engineer must diff the extracted body against original source lines 5878–5882 before declaring done.

---

## 7. Call-Site Preservation (DO NOT TOUCH)

```
File: src/PropTraderTools/CopyEngine.cs
Line: 1541
Content (exact): EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);
```

This line must remain **byte-for-byte identical** after the change. The `EvictDedup` signature
is unchanged (`internal void`, same parameters, same types), so no modification to the call site
is required or permitted.

**Verify**: After implementing, confirm with:

```
grep -n "EvictDedup" src/PropTraderTools/CopyEngine.cs | grep "1541"
```

Expected output contains the unchanged call.

---

## 8. xUnit Test to Write

**File**: `src/PropTraderTools.Tests/CopyEngineTests.cs`  
**Framework**: xUnit `[Fact]`  
**Test name**: `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`

```csharp
[Fact]
public void EvictDedup_CancelledEntry_ClearsLastLeaderDirection()
{
    // Arrange: record a leader direction and mark entry as live-dispatched.
    _engine.SetLeaderDirection_ForTest("MGC DEC26", OrderAction.Buy);
    _engine.IsLiveEntryBlocked_ForTest("MGC DEC26|Buy", "ord-1", 0.0);

    // Act: cancel the entry order -- BUG-E fix must clear the direction.
    _engine.EvictDedup_ForTest("ord-1", NinjaTrader.Cbi.OrderState.Cancelled);

    // Assert: _lastLeaderDirection entry for "MGC DEC26" must be gone.
    Assert.False(_engine.HasLeaderDirection("MGC DEC26"));
}
```

**Helper method locations (already in source — do NOT add, do NOT modify)**:

| Helper | Source Line | Note |
|--------|-------------|------|
| `HasLeaderDirection` | 4330 | No `_ForTest` suffix — call as-is |
| `SetLeaderDirection_ForTest` | 4333 | Has `_ForTest` suffix |
| `IsLiveEntryBlocked_ForTest` | 4348 | Has `_ForTest` suffix |
| `EvictDedup_ForTest` | 4360 | Has `_ForTest` suffix |

**CRITICAL**: The Assert calls `HasLeaderDirection("MGC DEC26")`, **not**
`HasLeaderDirection_ForTest("MGC DEC26")`. The `_ForTest` variant does NOT exist.
Using the wrong name produces a compile error (SCAN-03 will catch it, but avoid
the iteration by using the correct name from the start).

**Test logic rationale**:  
`"MGC DEC26|Buy"` as the `instrKey` is split at `|`, yielding `"MGC DEC26"`.
`EvictCancelledEntry` calls `_lastLeaderDirection.TryRemove("MGC DEC26", out _)`.
`HasLeaderDirection("MGC DEC26")` checks `ContainsKey`, which must return `false`.

---

## 9. 7-Scan Checklist (Engineer Contract — all 7 required before BUILD_PASS)

Each scan must be run and its result recorded in `docs/brain/PTT-REPAIRS-DW-E-04/ticket-1-completion.md`
before the ticket is declared complete.

---

### SCAN-01 — lock() audit

```
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | grep -E "EvictDedup|EvictCancelledEntry|EvictFilledEntry"
```

**Expected**: 0 matches (empty output)  
**Violation**: Any match = JS-021 failure; remove the `lock()` block.

---

### SCAN-02 — Non-ASCII audit

```
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs | grep -E "EvictDedup|EvictCancelledEntry|EvictFilledEntry"
```

**Expected**: 0 matches (empty output)  
**Violation**: Any match = JS-042 failure; replace non-ASCII character with ASCII equivalent.

---

### SCAN-03 — Build errors

```
dotnet build 2>&1 | grep " Error"
```

**Expected**: 0 Error(s) — output contains no lines with ` Error`  
**Violation**: Any build error = implementation fault; fix before proceeding.

---

### SCAN-04 — Build summary

```
dotnet build 2>&1 | Select-String "Error\(s\)"
```

**Expected**: Line reads `Build succeeded.` with `0 Error(s)`  
**Violation**: Any nonzero error count = implementation fault.

---

### SCAN-05 — Test counts

```
dotnet test 2>&1 | Select-String "passed|failed|skipped"
```

**Expected**:
- `passed` count **>= 19** (baseline was 19; new test adds 1, so ≥ 20 after this change)
- `skipped` count **>= 31**
- New test `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` must show as **Passed**
- No genuine regressions (pre-existing `failed` count of 449 is baseline; any NEW failures from
  this change = regression)

**Baseline**: 19 passed / 449 failed / 31 skipped (pre-epic).

---

### SCAN-06 — deploy-sync

```
powershell -File .\deploy-sync.ps1
```

**Expected**: Output ends with `SYNC COMPLETE`  
**Violation**: `DIFF GUARD` failure = unintended whitespace or artifact drift; isolate logic changes
and revert whitespace bloat, then re-run.

---

### SCAN-07 — Hard-link count

```powershell
(Get-Item "src/PropTraderTools/CopyEngine.cs").LinkType
fsutil hardlink list src\PropTraderTools\CopyEngine.cs
```

**Expected**: `LinkType` = `HardLink` and `fsutil` lists the NinjaTrader hard-link target.
Hard-link count = 1 entry in the `fsutil` list (one NinjaTrader DLL target).  
**Violation**: Missing hard-link = deploy-sync not run or failed silently; re-run SCAN-06.

---

## 10. Acceptance Criteria (all must pass before BUILD_PASS declaration)

| # | Criterion | How Verified |
|---|-----------|--------------|
| AC-01 | `dotnet build`: 0 Error(s) | SCAN-03 + SCAN-04 |
| AC-02 | `dotnet test`: passed >= 19, no new genuine regressions, skipped >= 31 | SCAN-05 |
| AC-03 | New test `EvictDedup_CancelledEntry_ClearsLastLeaderDirection` passes | SCAN-05 |
| AC-04 | `EvictDedup` CYC <= 8 (target 7) | CYC arithmetic §4; code review of §5.1 body |
| AC-05 | `EvictCancelledEntry` CYC <= 8 (target 4) | CYC arithmetic §4; code review of §5.2 body |
| AC-06 | `EvictFilledEntry` CYC <= 8 (target 3) | CYC arithmetic §4; code review of §5.3 body |
| AC-07 | BUG-E fix (lines 5878-5882) preserved verbatim in `EvictCancelledEntry` | §6 diff check |
| AC-08 | Call site at line 1541 unchanged | §7 grep verification |
| AC-09 | No `lock()` in any of the three methods | SCAN-01 |
| AC-10 | ASCII-only in all three methods | SCAN-02 |
| AC-11 | SCAN-06 deploy-sync reports SYNC COMPLETE | SCAN-06 |
| AC-12 | `ticket-1-completion.md` written with all 7 scan result outputs | Delivery artifact |

---

## 11. Delivery Artifact

On completion, engineer writes:

**File**: `docs/brain/PTT-REPAIRS-DW-E-04/ticket-1-completion.md`

Contents must include:
- Actual output of each scan (SCAN-01 through SCAN-07)
- Pass/Fail status for each scan
- Final `dotnet test` summary line (passed / failed / skipped counts)
- Confirmation that line 1541 is unchanged (grep output from §7)
- Confirmation that BUG-E verbatim lines are preserved (diff or grep output)

---

## 12. Risks Inherited from Architecture Plan

| # | Risk | Mitigation |
|---|------|------------|
| R1 | Engineer uses `HasLeaderDirection_ForTest` (does not exist) in test Assert | §8 explicitly flags; SCAN-03 will catch compile error |
| R2 | BUG-E comment lines stripped or altered | §6 verbatim contract; diff check required before submission |
| R3 | `_entryDispatchedOrders.TryRemove` placed inside `EvictCancelledEntry` | §5.1 shows exact placement; it stays in `EvictDedup` Cancelled branch |
| R4 | deploy-sync DIFF GUARD failure from whitespace drift | SCAN-06 required; fix diff guard before declaring done |
| R5 | Pre-existing `return null` lines (DW-WAVE2-LA-03) accidentally touched | Scope edits strictly to lines 5851-5901; SCAN-03 detects any introduced compile errors |
| R6 | test passed count drops below 19 due to pre-existing flaky tests | Baseline 19 is pre-existing; if count drops it is pre-existing flake, not a regression from this change; document in completion artifact |

---

*Ticket generation complete. Single ticket T1 covers both source files.*  
*Return: TICKETS_COMPLETE*
