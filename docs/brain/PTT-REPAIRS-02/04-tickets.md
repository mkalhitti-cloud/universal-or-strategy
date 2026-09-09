# PTT-REPAIRS-02 Tickets
**Status**: TICKETS_COMPLETE
**Phase**: 3 (Ticket Generation)
**Epic**: PTT-REPAIRS-02
**Author**: ptt-architect
**Date**: 2026-09-07
**Source Plan**: `docs/brain/PTT-REPAIRS-02/02-architecture-plan.md` (REVIEW_PASS — Cycle 2)
**Source Review**: `docs/brain/PTT-REPAIRS-02/02-plan-review.md` (REVIEW_PASS)

---

## SCOPE LOCK

**TICKET 1 OF 1 -- CopyEngine.cs ONLY**

This epic contains a single ticket. No other files are in scope except the two listed under
section C. Any change to a file not listed here is a protocol violation.

---

## Ticket PTT-REPAIRS-02-T1

### A. Spec Requirement IDs

| Requirement ID | Plan Section | Description |
|----------------|-------------|-------------|
| PTT-REPAIRS-02-T1 | Plan Sections 1, 3, 5, 9 | Fix `_liveEntryInstruments` over-blocking on rapid re-entry after fill -- clear instrKey in `EvictDedup` on `Filled` state |
| PTT-REPAIRS-02-T1-TEST | Plan Sections 7, 9 | xUnit `[Fact]` `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` verifying the 5-step alternating-dispatch scenario |

Defect confirmed (plan Section 1): `_liveEntryInstruments` is NOT cleared on leader order fill.
All subsequent `DispatchCopy` calls for the same instrument+direction are silently blocked at
Gate 5 check (a), producing the alternating pattern:

- attempt 1: leader + followers OK
- attempt 2: leader only (followers MISSED) ← defect
- attempt 3: leader + followers OK
- attempt 4: leader only (followers MISSED) ← defect

Root cause: `EvictDedup` Filled branch evicts `_entryInstrKeyByOrderId` but does NOT call
`_liveEntryInstruments.TryRemove`. Fix: mirror the Cancelled branch pattern.

---

### B. Scope Lock

**TICKET 1 OF 1 -- CopyEngine.cs ONLY**

---

### C. Exact Files Touched

| File | Role |
|------|------|
| `src/PropTraderTools/CopyEngine.cs` | Production fix -- `EvictDedup` Filled block (lines 5762-5768) |
| `src/PropTraderTools/CopyEngineTests.cs` | New `[Fact]` appended after T_R6 block (line 7779) |

**No other files are touched by this ticket.**

---

### D. Method Signatures

#### Method Under Repair

**Current signature** (line 5740, unchanged):
```csharp
internal void EvictDedup(string orderId, OrderState state)
```

No signature change. The method is `internal` for test seam access via
`InternalsVisibleTo("PropTraderTools.Tests")` (line 46). Visibility and parameter list are
**unchanged**.

#### What Changes Inside `EvictDedup`

The Filled block body (lines 5762-5768) is replaced. See section E for exact before/after.

The header comment on line 5738 is updated to reflect the new CYC count. See section E.

#### Helper Methods

No helper extraction required. `EvictDedup` CYC after fix = 6, within the <=7 budget.
No new helper methods are created.

#### Unchanged Signatures Referenced

| Method | Line | Signature | Role |
|--------|------|-----------|------|
| `IsLiveEntryBlocked` | 5710 | `private bool IsLiveEntryBlocked(string instrKey, string orderId, double limitPrice)` | Gate 5 check -- UNCHANGED |
| `ClearLiveEntryForInstrument` | 5726 | `private void ClearLiveEntryForInstrument(string instrFullName)` | Secondary guard -- UNCHANGED |

---

### E. Exact Code Change

#### Change 1: Header Comment (line 5738)

**BEFORE** (line 5738):
```csharp
        // DW-B142-MGC-02: CYC=5: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4).
```

**AFTER** (line 5738):
```csharp
        // PTT-REPAIRS-02: CYC=6: terminal-guard(1) + Cancelled(2) + instrKey-lookup(3) + Filled(4) + filledInstrKey-remove(5).
```

#### Change 2: `EvictDedup` Filled Block (lines 5762-5768)

**BEFORE** (lines 5762-5768):
```csharp
            if (state == OrderState.Filled)
            {
                // DW-B142-MGC-02: clean up companion map (lazy).
                // Do NOT remove _liveEntryInstruments key -- trade is live.
                // PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup.
                _entryInstrKeyByOrderId.TryRemove(orderId, out _);
            }
```

**AFTER** (lines 5762-5768):
```csharp
            if (state == OrderState.Filled)
            {
                // PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.
                // Mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.
                // MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.
                if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
                    _liveEntryInstruments.TryRemove(filledInstrKey, out _);
            }
```

**Lines deleted** (from the BEFORE block):
- Line 5764: `// DW-B142-MGC-02: clean up companion map (lazy).`
- Line 5765: `// Do NOT remove _liveEntryInstruments key -- trade is live.`
- Line 5766: `// PositionStateChanged flat gate (ClearLiveEntryForInstrument) is the authoritative cleanup.`
- Line 5767: `_entryInstrKeyByOrderId.TryRemove(orderId, out _);`

**Lines added** (in the AFTER block):
- `// PTT-REPAIRS-02: clear instrKey on fill -- entry lifecycle complete, followers dispatched.`
- `// Mirrors Cancelled branch. ClearLiveEntryForInstrument remains as secondary guard.`
- `// MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A) -- not instrKey.`
- `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))`
- `    _liveEntryInstruments.TryRemove(filledInstrKey, out _);`

**Pattern proof**: The new Filled block mirrors the Cancelled branch at lines 5758-5759:
```csharp
// Cancelled branch (lines 5758-5759 -- UNCHANGED, mirrors this pattern):
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
    _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);

// Filled branch (new -- same pattern):
if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))
    _liveEntryInstruments.TryRemove(filledInstrKey, out _);
```

---

### F. CYC Constraint

| Method | File | CYC Before | CYC After | Budget | Status |
|--------|------|-----------|-----------|--------|--------|
| `EvictDedup` | `CopyEngine.cs:5740` | 5 | **6** | <=7 | **PASS** |
| `IsLiveEntryBlocked` | `CopyEngine.cs:5710` | 4 | 4 (unchanged) | <=5 | **PASS** |

**CYC=6 branch count for `EvictDedup` after fix:**

| # | Branch | Note |
|---|--------|------|
| 1 | `if (state != Filled && state != Cancelled && state != Rejected)` | terminal guard |
| 2 | `if (state == OrderState.Cancelled)` | Cancelled arm |
| 3 | `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))` | Cancelled instrKey lookup |
| 4 | `if (state == OrderState.Filled)` | Filled arm |
| 5 | `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey))` | **NEW** Filled instrKey lookup |

Base(1) + 5 decision points = **CYC=6**. Budget <=7. **No extraction required.**

**CYC=4 for `IsLiveEntryBlocked`** (unchanged):

| # | Branch |
|---|--------|
| 1 | `if (_liveEntryInstruments.ContainsKey(instrKey))` |
| 2 | `if (IsDedup(orderId, limitPrice))` |
| 3 | `if (IsEntryDispatched(orderId))` |

Base(1) + 3 decision points = **CYC=4**. Budget <=5. **Unchanged.**

---

### G. MGC Guard Integrity Assertion

**The fix does NOT remove the `_entryDispatchedOrders` guard (DW-B142-MGC-02 / DW-B91-A).**

The `_entryDispatchedOrders` dictionary is the primary MGC double-dispatch guard, checked at
`IsLiveEntryBlocked` line 5716 via `IsEntryDispatched(orderId)`. This check is **untouched**
by this fix. Once an orderId is dispatched, `IsEntryDispatched(orderId)` blocks it at
Gate 5 check (c), independent of `_liveEntryInstruments`.

**The Cancelled cleanup path (lines 5758-5759) is untouched.**

The fix operates only inside the `if (state == OrderState.Filled)` block at lines 5762-5768.
The Cancelled block at lines 5751-5760 is not modified in any way. The Cancelled branch
removes `_entryDispatchedOrders[orderId]`, `_entryInstrKeyByOrderId[orderId]`, and
`_liveEntryInstruments[cancelledInstrKey]` as before.

**MGC cancel+resubmit scenario is intact after fix:**

| Scenario | Before Fix | After Fix | Verdict |
|----------|------------|-----------|---------|
| First dispatch: instrKey set in `_liveEntryInstruments` | YES | YES (unchanged) | OK |
| MGC cancel: `EvictDedup(Cancelled)` clears instrKey | YES | YES (unchanged at lines 5758-5759) | OK |
| MGC resubmit NEW orderId: instrKey clear, TryAdd succeeds, dispatched | YES | YES (unchanged) | OK |
| MGC resubmit BEFORE cancel: instrKey still set, blocked | YES | YES (Cancelled path, not Filled) | OK |
| Leader fill: instrKey cleared by new fix | NO (stuck) | YES (released) | **FIXED** |
| Position flat: `ClearLiveEntryForInstrument` clears instrKey | YES | YES (now secondary guard) | OK |

**Proof that `_entryDispatchedOrders` provides MGC protection independent of instrKey:**
The cancel+resubmit scenario creates a NEW orderId for the resubmitted order. The original
instrKey was cleared on Cancelled (line 5758-5759). The new orderId's dispatch is gated by
`TryAdd(instrKey)` succeeding (cleared by Cancelled) AND `TryAdd(newOrderId)` to
`_entryInstrKeyByOrderId` succeeding (new orderId, never seen). The Filled branch releasing
instrKey affects only post-fill re-entries with a DIFFERENT orderId -- a scenario the Cancelled
path does not cover and instrKey correctly guards against until fill.

---

### H. xUnit [Fact] Test

**Test Name**: `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`
**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Insertion point**: Append after the T_R6 block closing brace (currently line 7778), before the
closing braces of the test class and namespace (lines 7779-7780).
**[Fact] count before this ticket**: 300
**[Fact] count after this ticket**: 301

**Seams used** (all confirmed present at `CopyEngine.cs` lines 4264-4282, granted by
`InternalsVisibleTo("PropTraderTools.Tests")` at line 46):

| Seam Method | Line | Signature | Used in Step |
|-------------|------|-----------|-------------|
| `ClearLiveEntryForInstrument_ForTest` | 4273 | `internal void (string instrFullName)` | Pre-condition |
| `IsLiveEntryBlocked_ForTest` | 4264 | `internal bool (string instrKey, string orderId, double limitPrice)` | Steps 1, 5 |
| `LiveEntryInstrumentsContains_ForTest` | 4276 | `internal bool (string key)` | Steps 2, 4 |
| `EvictDedup_ForTest` | 4270 | `internal void (string orderId, NinjaTrader.Cbi.OrderState state)` | Step 3 |

**Full test implementation** (append to `CopyEngineTests.cs`, inside the test class, after T_R6):

```csharp
        // PTT-REPAIRS-02 T1: verify _liveEntryInstruments is cleared on Filled,
        // allowing a second DispatchCopy for the same instrKey to pass Gate 5 check (a).
        // Uses InternalsVisibleTo seams declared at CopyEngine.cs:46.
        // No NT8 type construction required -- seams operate on string keys and OrderState enum.
        [Fact]
        public void IsLiveEntryBlocked_ClearsOnFill_AllowsReentry()
        {
            const string instrKey = "MGC DEC26|Sell";
            const string orderId1 = "PTTR02-orderId-1";
            const string orderId2 = "PTTR02-orderId-2";
            const double limitPrice = 0.0;

            // Pre-condition: clear any residual state from other tests
            _engine.ClearLiveEntryForInstrument_ForTest("MGC DEC26");

            // Step 1: First dispatch -- Gate 5 should pass (instrKey not yet set)
            bool blocked1 = _engine.IsLiveEntryBlocked_ForTest(instrKey, orderId1, limitPrice);
            Assert.False(blocked1); // first dispatch must proceed

            // Step 2: instrKey is now set in _liveEntryInstruments
            Assert.True(_engine.LiveEntryInstrumentsContains_ForTest(instrKey));

            // Step 3: Leader order fills -- EvictDedup must clear instrKey (the fix)
            _engine.EvictDedup_ForTest(orderId1, NinjaTrader.Cbi.OrderState.Filled);

            // Step 4: instrKey must be cleared from _liveEntryInstruments after the fill
            Assert.False(_engine.LiveEntryInstrumentsContains_ForTest(instrKey));

            // Step 5: Second dispatch for same instrKey -- Gate 5 check (a) must pass
            bool blocked2 = _engine.IsLiveEntryBlocked_ForTest(instrKey, orderId2, limitPrice);
            Assert.False(blocked2); // second dispatch must proceed (was blocked before fix)
        }
```

**5-step behavioral assertions:**

| Step | Action | Assert | What It Verifies |
|------|--------|--------|-----------------|
| Pre | `ClearLiveEntryForInstrument_ForTest("MGC DEC26")` | -- | Isolates test from residual state |
| 1 | `IsLiveEntryBlocked_ForTest(instrKey, orderId1, 0.0)` | `False` | Gate 5 passes on first dispatch (baseline) |
| 2 | `LiveEntryInstrumentsContains_ForTest(instrKey)` | `True` | instrKey was set by step 1 dispatch |
| 3 | `EvictDedup_ForTest(orderId1, OrderState.Filled)` | -- | Simulates leader fill event |
| 4 | `LiveEntryInstrumentsContains_ForTest(instrKey)` | `False` | **Fix verified: instrKey cleared on Filled** |
| 5 | `IsLiveEntryBlocked_ForTest(instrKey, orderId2, 0.0)` | `False` | Re-entry passes Gate 5 (defect resolved) |

**JS compliance:**
- No `lock()`. No `DateTime.Now`. ASCII-only string literals. `[Fact]` only (no `[Theory]`).
- CYC=1 (no branches in test body). PASS.

---

### I. 7-Scan Checklist

The engineer MUST run all 7 scans against the modified methods before marking this ticket done.
All scans must return 0 violations.

| Scan | Command (PowerShell from repo root) | Expected Result |
|------|-------------------------------------|-----------------|
| **SCAN-01** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' \| Where-Object { $_.LineNumber -ge 5735 -and $_.LineNumber -le 5775 }` | **0 matches** |
| **SCAN-02** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'DateTime\.Now' \| Where-Object { $_.LineNumber -ge 5735 -and $_.LineNumber -le 5775 }` | **0 matches** |
| **SCAN-03** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'return null' \| Where-Object { $_.LineNumber -ge 5710 -and $_.LineNumber -le 5775 }` | **0 matches** (`EvictDedup` is `void`; `IsLiveEntryBlocked` returns `bool`) |
| **SCAN-04** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'async void' \| Where-Object { $_.LineNumber -ge 5710 -and $_.LineNumber -le 5775 }` | **0 matches** |
| **SCAN-05** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]' \| Where-Object { $_.LineNumber -ge 5735 -and $_.LineNumber -le 5775 }` | **0 matches** (ASCII-only) |
| **SCAN-06** | `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\?\.\w+\s*-=' \| Where-Object { $_.LineNumber -ge 5710 -and $_.LineNumber -le 5775 }` | **0 matches** |
| **SCAN-07** | Manual CYC count per section F above | `EvictDedup`=**6** (<=7 PASS), `IsLiveEntryBlocked`=**4** (<=5 PASS) |

**Engineer contract**: Do NOT mark this ticket complete until all 7 scans confirm 0 violations
and the CYC counts match the values in section F.

---

### J. Jane Street Rule Compliance Table

| Rule | Description | This Ticket | Status |
|------|-------------|-------------|--------|
| JS-021 | No `lock()` -- all mutable state via lock-free primitives | `EvictDedup` uses `ConcurrentDictionary.TryRemove` exclusively; no `lock(` introduced | **PASS** |
| JS-023 | No UI update from off-thread without `Dispatcher.InvokeAsync` | Fix is confined to `CopyEngine.cs` backend; no UI thread involvement | **N/A** |
| JS-025 | `ConcurrentDictionary` is the lock-free canonical collection | Both `_entryInstrKeyByOrderId.TryRemove` and `_liveEntryInstruments.TryRemove` are `ConcurrentDictionary` operations; new code mirrors existing Cancelled branch pattern | **PASS** |
| JS-008 | No mutable struct fields / unfrozen brushes | No struct usage introduced | **N/A** |

---

### K. Definition of Done

This ticket is complete ONLY when ALL of the following conditions are true:

| Condition | Verification Method |
|-----------|---------------------|
| SCAN-01: 0 `lock()` matches in modified methods (lines 5735-5775) | PowerShell SCAN-01 command (section I) |
| SCAN-02: 0 `DateTime.Now` matches in modified methods | PowerShell SCAN-02 command (section I) |
| SCAN-03: 0 `return null` matches in modified methods | PowerShell SCAN-03 command (section I) |
| SCAN-04: 0 `async void` matches in modified methods | PowerShell SCAN-04 command (section I) |
| SCAN-05: 0 non-ASCII characters in modified lines | PowerShell SCAN-05 command (section I) |
| SCAN-06: 0 `?.Event -=` patterns in modified methods | PowerShell SCAN-06 command (section I) |
| SCAN-07: CYC(EvictDedup) = 6 (<=7); CYC(IsLiveEntryBlocked) = 4 (<=5) | Manual count per section F |
| `[Fact]` test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` exists and passes | `dotnet test --filter "IsLiveEntryBlocked_ClearsOnFill_AllowsReentry"` |
| Alternating-dispatch scenario passes: Step 5 `blocked2 == false` | Test assertion in step 5 |
| `_entryDispatchedOrders` guard confirmed intact (line 5716 untouched) | `grep -n "_entryDispatchedOrders" src/PropTraderTools/CopyEngine.cs` -- line 5716 present |
| Cancelled cleanup path (lines 5758-5759) confirmed untouched | `grep -n "cancelledInstrKey" src/PropTraderTools/CopyEngine.cs` -- lines 5758-5759 present |
| `[Fact]` count = prior count + 1 | `Select-String -Pattern '^\s*\[Fact\]' src/PropTraderTools/CopyEngineTests.cs \| Measure-Object` |

---

## Deferred Work Carried Forward

The following items are out of scope for PTT-REPAIRS-02. They are listed here for handoff
traceability only.

| ID | Item | Priority | Target | Status |
|----|------|----------|--------|--------|
| DW-B24-01 | NT8-043 null-conditional unsubscription runtime crash confirmation | P2 | B27+ | OPEN |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verify in live NT8 session | P1 | ASAP post-merge | OPEN |
| DW-B24-03 | Skip-duplicate guard `[Fact]` for `if (acc == leader) continue` | P2 | B27+ | OPEN |
| DW-B25-01 | Companion field race on `_pendingBeAccount` / `_pendingBeInstrument` plain refs | P3 | B28+ | OPEN |
| DW-B26-01 | Reflection test upgrade Option B to Option A for `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` | P2 | B28+ | OPEN |
| DW-REPAIRS-01-01 | R5 Account.All constructor-path risk in `BuildRuleRow` | P2 | B28+ | OPEN |
| DW-REPAIRS-01-02 | R2 `PendingCancelCount` non-volatile read in `TryDrainWatchdog` | P3 | Future | OPEN |
| DW-REPAIRS-02-01 | `_entryDispatchedOrders` not cleared in Filled branch of `EvictDedup` -- may linger if `TryEvictFollowerBeSlot` does not fire; runtime E2E verification needed | P2 | B28+ | OPEN |

---

*ptt-architect · PTT-REPAIRS-02 · 2026-09-07*
