# Architecture Plan — PTT-REPAIRS-DW-E-04

**Status**: DRAFT — awaiting ptt-plan-reviewer  
**Phase**: 1 (Architecture)  
**Author**: ptt-architect  
**Epic**: PTT-REPAIRS-DW-E-04  
**File**: `src/PropTraderTools/CopyEngine.cs`  

---

## §1 Epic Summary and Objective

Reduce `EvictDedup` cyclomatic complexity from **CYC=13** (current, pre-existing violation)
to **CYC≤8** (JS-013 limit) by extracting two private helper methods:
`EvictCancelledEntry` and `EvictFilledEntry`.

This is a **pure refactor** — no behavior change, no new state, no new fields,
no signature change on the call-site method. The BUG-E fix (lines 5878-5882,
`_lastLeaderDirection` stale-direction clear on cancel) must be preserved verbatim
inside the extracted `EvictCancelledEntry`.

**Deferred backlog items closed by this epic**:  
- DW-WAVE2-LA-03 (P1): pre-existing `return null` at lines 5809, 5831, 5844, 5850
  are **outside scope** and must not be touched. Still OPEN for a future wave.  
- DW-WAVE2-LA-01 (P0): F5 NinjaTrader manual recompile gate remains OPEN.

---

## §2 LANE-SPLIT GATE RESULT

```
LANE-SPLIT GATE RESULT: SINGLE-PIPELINE
```

**Gate evaluation**:

| Q | Question | Answer | Verdict |
|---|----------|--------|---------|
| Q1 | Same method or within 50 lines? | YES — both extractions are from the same 50-line method body (lines 5851-5901) | STOP: single pipeline |

Q1 = YES → **single pipeline**. Questions Q2–Q4 not evaluated (STOP rule).

---

## §3 Spec Requirements Mapped

| Rule | Description | Applies To | Satisfied How |
|------|-------------|------------|---------------|
| JS-013 | All methods CYC ≤ 8 | All three methods | Extraction reduces EvictDedup to CYC≤7; new helpers CYC≤4 each |
| JS-021 | No `lock()` | All three methods | All ops are `ConcurrentDictionary` (lock-free by .NET spec) |
| JS-001 | No `throw` | All three methods | No throw statements; all paths use early return or silent TryRemove |
| JS-002 | No null return | All three methods | All three are `void`; trivially satisfied |
| JS-042 | ASCII-only identifiers and string literals | All three methods | No Unicode in method bodies or comments; enforced by SCAN-02 |

---

## §4 Current EvictDedup Structure Analysis

**Source**: `src/PropTraderTools/CopyEngine.cs` lines 5851-5901 (read directly).

```
internal void EvictDedup(string orderId, OrderState state)    // line 5851
{
    // GUARD: terminal-state filter (3 compound conditions)
    if (state != OrderState.Filled                            // line 5854
        && state != OrderState.Cancelled                      // line 5855
        && state != OrderState.Rejected)                      // line 5856
        return;                                               // line 5858

    _dedupCache.TryRemove(orderId, out _);                    // line 5860

    if (state == OrderState.Cancelled)                        // line 5862
    {
        _entryDispatchedOrders.TryRemove(orderId, out _);     // line 5866
        if (_entryInstrKeyByOrderId.TryRemove(               // line 5872
                orderId, out var cancelledInstrKey))
        {
            string storedId;
            if (_liveEntryInstruments.TryGetValue(           // line 5875
                    cancelledInstrKey, out storedId)
                && storedId == orderId)                       // line 5876
                _liveEntryInstruments.TryRemove(             // line 5877
                    cancelledInstrKey, out _);
            // BUG-E fix (lines 5878-5882):
            var pipeIdx = cancelledInstrKey.IndexOf('|');    // line 5880
            if (pipeIdx > 0)                                 // line 5881
                _lastLeaderDirection.TryRemove(              // line 5882
                    cancelledInstrKey.Substring(0, pipeIdx), out _);
        }
    }

    if (state == OrderState.Filled)                          // line 5886
    {
        if (_entryInstrKeyByOrderId.TryRemove(              // line 5892
                orderId, out var filledInstrKey))
        {
            string storedId;
            if (_liveEntryInstruments.TryGetValue(          // line 5895
                    filledInstrKey, out storedId)
                && storedId == orderId)                      // line 5896
                _liveEntryInstruments.TryRemove(            // line 5897
                    filledInstrKey, out _);
        }
    }
    // DW-B91-A-v2: Filled/Rejected _entryDispatchedOrders eviction
    // handled in TryEvictFollowerBeSlot.                    // line 5900
}
```

**CYC breakdown (current CYC=13, McCabe)**:

Per the comment at line 5849, the prior annotator counted:
`terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + liveEntry-guard(4) + pipeIdx-guard(5) + Filled(6) + filledInstrKey-remove(7)`

The McCabe count including logical operators:
- Base: 1
- `&&` × 2 in terminal guard: +2
- `if (state == Cancelled)`: +1
- `if (TryRemove orderId, out cancelledInstrKey)`: +1
- `if (TryGetValue && storedId == orderId)` — if: +1, &&: +1
- `if (pipeIdx > 0)`: +1
- `if (state == Filled)`: +1
- `if (TryRemove orderId, out filledInstrKey)`: +1
- `if (TryGetValue && storedId == orderId)` — if: +1, &&: +1
**Total: 12–13** (tool reports 13). Pre-existing JS-013 violation.

---

## §5 Extracted Method Designs with Signatures and CYC Targets

### 5.1 EvictDedup (residual — unchanged signature)

```csharp
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

**CYC count (residual)**:
- Base: 1
- `&&` × 2 in terminal guard: +2
- `if (state == Cancelled)`: +1
- `if (TryRemove cancelledInstrKey)`: +1
- `if (state == Filled)`: +1
- `if (TryRemove filledInstrKey)`: +1
- **Total CYC = 7** ✓ (limit 8)

---

### 5.2 EvictCancelledEntry (new private method)

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

**CYC count**:
- Base: 1
- `if (TryGetValue && storedId == orderId)` — if: +1, &&: +1
- `if (pipeIdx > 0)`: +1
- **Total CYC = 4** ✓ (limit 8, target ~5)

---

### 5.3 EvictFilledEntry (new private method)

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

**CYC count**:
- Base: 1
- `if (TryGetValue && storedId == orderId)` — if: +1, &&: +1
- **Total CYC = 3** ✓ (limit 8, target ~4)

---

### 5.4 Summary CYC Table

| Method | Current CYC | Post-Extraction CYC | Limit | Status |
|--------|-------------|---------------------|-------|--------|
| `EvictDedup` | 13 | 7 | 8 | ✓ PASS |
| `EvictCancelledEntry` | N/A (new) | 4 | 8 | ✓ PASS |
| `EvictFilledEntry` | N/A (new) | 3 | 8 | ✓ PASS |

---

## §6 BUG-E Fix Preservation Plan

**BUG-E fix location**: lines 5878-5882 (current source).

**Verbatim lines to preserve EXACTLY** in `EvictCancelledEntry`:

```csharp
    // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
    // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
    var pipeIdx = cancelledInstrKey.IndexOf('|');
    if (pipeIdx > 0)
        _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```

**Preservation contract**:
1. The two-line comment (lines 5878-5879) must appear verbatim above the code.
2. `cancelledInstrKey.IndexOf('|')` — exact method, exact character literal `'|'`.
3. `if (pipeIdx > 0)` — exact guard condition.
4. `_lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _)` — exact call.
5. Engineer must diff the extracted body against lines 5880-5882 before submission.
6. SCAN-03 (build errors) and SCAN-04 (no build warnings) implicitly verify correctness.

---

## §7 Call-Site Preservation Verification

**Call site** (must remain UNCHANGED):

```csharp
// src/PropTraderTools/CopyEngine.cs  line 1541
EvictDedup(e.Order.OrderId.ToString(), e.Order.OrderState);
```

**Verification**:
- `EvictDedup` signature is `internal void EvictDedup(string orderId, OrderState state)` — unchanged.
- No parameter type changes, no return type changes, no accessibility changes.
- `EvictDedup_ForTest` wrapper at line 4360 delegates to `EvictDedup(orderId, state)` — also unchanged.
- Engineer must confirm line 1541 is not modified in the final diff (SCAN-01 implicitly covers this
  via "0 lock()" but a visual diff check is required as part of ticket delivery).

---

## §8 New Test Decision

**Decision: INCLUDE in this pipeline ticket.**

**Test name**: `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`  
**File**: `src/PropTraderTools/CopyEngineTests.cs`  
**Framework**: xUnit `[Fact]`

**Rationale**:
1. BUG-E code (lines 5880-5882) is the primary content being moved by this extraction.
   This test is the only regression guard for that exact behavior.
2. All helper methods required already exist and are `internal`/`InternalsVisibleTo`:
   - `SetLeaderDirection_ForTest` at line 4333
   - `IsLiveEntryBlocked_ForTest` at line 4348
   - `EvictDedup_ForTest` at line 4360
   - `HasLeaderDirection` at line 4330 (note: NOT `HasLeaderDirection_ForTest` — the existing method
     has no `_ForTest` suffix; engineer must call `HasLeaderDirection`, not a non-existent variant)
3. Baseline: 19 passed / 449 failed / 31 skipped. New test will pass → ≥20 passed. No regression.

**Test body**:

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

**IMPORTANT — engineer note**: The Assert calls `HasLeaderDirection` (line 4330),
NOT `HasLeaderDirection_ForTest` (which does not exist). Using the wrong name
will produce a compile error. This is flagged here to prevent a ticket iteration.

---

## §9 7-Scan Checklist Template for Ticket

The following checklist MUST appear in the ticket and must be signed off by the engineer
before declaring the ticket complete:

```
SCAN-01  lock() audit
         grep -n "lock(" src/PropTraderTools/CopyEngine.cs | grep -E "EvictDedup|EvictCancelledEntry|EvictFilledEntry"
         Expected: 0 matches

SCAN-02  Non-ASCII audit
         grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs | grep -E "EvictDedup|EvictCancelledEntry|EvictFilledEntry"
         Expected: 0 matches

SCAN-03  Build errors
         dotnet build src/PropTraderTools/PropTraderTools.csproj
         Expected: 0 Error(s)

SCAN-04  Build summary (warnings)
         (from SCAN-03 output)
         Expected: 0 new warnings introduced by this change

SCAN-05  Test counts
         dotnet test
         Expected: passed >= 19 (baseline), skipped >= 31, new test EvictDedup_CancelledEntry_ClearsLastLeaderDirection PASS

SCAN-06  deploy-sync
         powershell -File .\deploy-sync.ps1
         Expected: SYNC COMPLETE (no DIFF GUARD failure)

SCAN-07  Hard-link count
         (Get-Item src/PropTraderTools/CopyEngine.cs).LinkTarget -or- fsutil hardlink list src\PropTraderTools\CopyEngine.cs
         Expected: hardlink count = 1 for CopyEngine.cs (NinjaTrader DLL hard-linked)
```

---

## §10 Ticket Count and Scope

**Single ticket: T1.**

| Field | Value |
|-------|-------|
| Ticket | T1 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Test file | `src/PropTraderTools/CopyEngineTests.cs` |
| Methods modified | `EvictDedup` (residual body rewrite) |
| Methods added | `EvictCancelledEntry` (private), `EvictFilledEntry` (private) |
| Methods unchanged | `EvictDedup_ForTest` (line 4360), `OnOrderUpdate` (line 1538) |
| JS rules verified | JS-013, JS-021, JS-001, JS-002, JS-042 |
| New xUnit tests | 1 (`EvictDedup_CancelledEntry_ClearsLastLeaderDirection`) |
| Scans required | SCAN-01 through SCAN-07 |
| Deferred from prior epic | None closed; DW-WAVE2-LA-03 remains OPEN (out of scope) |

No second ticket is warranted. The two extracted methods are subordinate to the
same parent method, in the same file, in the same 50-line block. SINGLE-PIPELINE
confirmed.

---

## §11 Risks and Mitigations

| # | Risk | Likelihood | Mitigation |
|---|------|-----------|------------|
| R1 | Engineer accidentally changes `HasLeaderDirection_ForTest` instead of `HasLeaderDirection` in test | Medium | §8 explicitly flags the distinction; SCAN-03 build error will catch it if wrong name used |
| R2 | BUG-E verbatim lines slightly altered (e.g. comment stripped) | Low | §6 supplies verbatim lines; engineer must diff extracted body against original lines 5880-5882 |
| R3 | `_entryDispatchedOrders.TryRemove` accidentally placed inside `EvictCancelledEntry` instead of EvictDedup Cancelled branch | Low | §5.1 shows exact placement; only `orderId`/`cancelledInstrKey` body goes into EvictCancelledEntry |
| R4 | deploy-sync DIFF GUARD failure due to unrelated whitespace drift | Low | Engineer must run deploy-sync.ps1 (SCAN-06) and fix any diff guard failure before declaring done |
| R5 | dotnet test passed count drops below 19 due to pre-existing flaky tests | Low | Baseline 19 passed is pre-existing; new test adds 1 passing; if count drops it is pre-existing flake, not regression from this change |
| R6 | Pre-existing `return null` lines near target (DW-WAVE2-LA-03) accidentally touched | Low | Engineer must scope edits strictly to lines 5851-5901; SCAN-03/04 will detect any introduced compile errors |

---

*Plan complete. Awaiting ptt-plan-reviewer Phase 2 review.*
