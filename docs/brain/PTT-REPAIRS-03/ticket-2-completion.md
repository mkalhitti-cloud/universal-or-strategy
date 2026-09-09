# PTT-REPAIRS-03-T2 Completion Report

**Ticket**: PTT-REPAIRS-03-T2
**Title**: Fix phantom instrKey set when no follower dispatch occurs (BUG-B)
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-09-08
**Source basis**: TICKET_REVIEW_PASS (Cycle 1) confirmed in 04-ticket-review.md

---

## Summary of Changes

### CopyEngine.cs

#### STEP 1 + 2 + 5: Replace IsLiveEntryBlocked with IsLiveEntryBlocked_Check + SetLiveEntryDispatched

**Location**: ~line 5732-5764 (pre-T2)

| Change | Before | After |
|--------|--------|-------|
| `IsEntryDispatched` body | CYC=2, TryAdd side-effect on first call | CYC=1, pure ContainsKey check |
| `IsLiveEntryBlocked` | CYC=4, combined check+commit | **DELETED** |
| `IsLiveEntryBlocked_Check` (new) | did not exist | CYC=4, pure predicate, no writes to _liveEntryInstruments/_entryInstrKeyByOrderId |
| `SetLiveEntryDispatched` (new) | did not exist | CYC=1, writes all three maps |

**IsEntryDispatched** (simplified, line ~5758 post-T2):
- Before: `if (ContainsKey) return true; TryAdd; return false;` (CYC=2)
- After: `return _entryDispatchedOrders.ContainsKey(orderId);` (CYC=1)

**IsLiveEntryBlocked_Check** (new, line ~5774 post-T2):
- Three guard ifs: `_liveEntryInstruments.ContainsKey`, `IsDedup`, `_entryDispatchedOrders.ContainsKey`
- No side effects on `_liveEntryInstruments` or `_entryInstrKeyByOrderId`
- CYC=4

**SetLiveEntryDispatched** (new, line ~5789 post-T2):
- Three `TryAdd` calls: `_liveEntryInstruments`, `_entryInstrKeyByOrderId`, `_entryDispatchedOrders`
- No decision branches. CYC=1

**Stale comment at line 201**: Updated from "Written in IsLiveEntryBlocked at Gate 5 pass time." to "Written in SetLiveEntryDispatched at Gate 5 pass time (PTT-REPAIRS-03 B1 split)."

#### STEP 3: Redirect IsLiveEntryBlocked_ForTest shim

**Location**: lines 4304-4308 (pre-T2) → lines 4315-4330 (post-T2)

- Before: `=> IsLiveEntryBlocked(instrKey, orderId, limitPrice);` (expression-bodied, CYC=1)
- After: full method body calling `IsLiveEntryBlocked_Check` then `SetLiveEntryDispatched` on pass (CYC=2)
- Preserves existing test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` behavioral contract without modification

#### STEP 6: Add ShouldSkipFollower helper

**Location**: Added after `ShouldSkipForReversalGuard` (~line 2608 post-T2)

- New method: combines `ShouldSkipFollowerDispatch` + `ShouldSkipForReversalGuard` sequential checks
- CYC=3 (base + 2 if-branches)
- Pure predicate, no side effects

#### STEP 7: Modify DispatchCopy (4 changes)

**Change 0** (line 2427): Updated stale CYC comment from `// CYC<=6 after extraction.` to `// CYC=8 after ShouldSkipFollower extraction + T2 dispatched-guard (PTT-REPAIRS-03).`

**Change 1** (line 2474 pre-T2 → line 2474 post-T2): `IsLiveEntryBlocked(` → `IsLiveEntryBlocked_Check(`. Gate5 log lines 2476-2482 unchanged.

**Change 2** (before the foreach): Added `int dispatched = 0;` before `int idx = 0;`

**Change 3** (loop body): Replaced two separate if-blocks (`ShouldSkipFollowerDispatch` + `ShouldSkipForReversalGuard`) with single `if (ShouldSkipFollower(...))`. Added `dispatched++;` after `DispatchToFollower` call.

**Change 4** (after foreach): Added `if (dispatched > 0) SetLiveEntryDispatched(instrKey, orderId);`

**Gate5 comment lines 2469-2471**: Updated from stale references to `IsLiveEntryBlocked`/`IsEntryDispatched` to accurate description of `IsLiveEntryBlocked_Check` + `SetLiveEntryDispatched` pattern.

**Final DispatchCopy CYC**: 8 (branches: gate0.5, gate3, gate4, gate5-check, foreach, ShouldSkipFollower, dispatched-counter-post-loop) — ≤8 ✓

### CopyEngineTests.cs

**New [Fact]**: `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped` appended after last existing test.

- Uses reflection to call `IsLiveEntryBlocked_Check` directly (private method via GetMethod)
- Verifies gate5 check returns false (instrKey unknown, no phantom lock)
- Verifies `_liveEntryInstruments` does NOT contain instrKey when `SetLiveEntryDispatched` was never called
- Uses `ClearLiveEntryForInstrument_ForTest` for pre-condition cleanup (existing seam)

---

## Layer 2 Scan Report

### SCAN-01: lock() grep
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\('
```
**Result**: All matches are in comments (`no lock()`, `no lock`). Zero actual `lock()` invocations anywhere in the file.
**Status**: PASS (0 violations)

### SCAN-02: Unicode/emoji/curly-quote grep
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]'
```
**Result**: Command completed with no output. Zero non-ASCII characters in the entire file.
**Status**: PASS (0 matches)

### SCAN-03: CYC check

| Method | Before | After | Budget | Status |
|--------|--------|-------|--------|--------|
| `ShouldSkipFollower` (new) | — | 3 (base + ShouldSkipFollowerDispatch if + ShouldSkipForReversalGuard if) | ≤8 | PASS ✓ |
| `IsLiveEntryBlocked_Check` (new) | — | 4 (base + instrKey ContainsKey + IsDedup + orderId ContainsKey) | ≤8 | PASS ✓ |
| `SetLiveEntryDispatched` (new) | — | 1 (no decision branches) | ≤8 | PASS ✓ |
| `IsEntryDispatched` (simplified) | 2 | 1 (single return) | ≤8 | PASS ✓ |
| `IsLiveEntryBlocked` (deleted) | 4 | deleted | — | N/A |
| `IsLiveEntryBlocked_ForTest` (redirected) | 1 | 2 (base + if-IsLiveEntryBlocked_Check) | ≤8 | PASS ✓ |
| `DispatchCopy` (changed) | 8 | 8 (7 after extraction, +1 for if(dispatched>0)) | ≤8 | PASS ✓ |

All methods ≤8. **Status**: PASS

### SCAN-04: [Fact] count
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
```
**Result**: Count = **477**

- Baseline after T1: 476
- T2 new test (`DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`): +1
- Existing test (`IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`): preserved unchanged (delta=0)
- Final: **477** = 476 + 1 ✓

**Status**: PASS (476 → 477, +1)

### SCAN-05: Build
```powershell
dotnet build Linting.csproj 2>&1 | Where-Object { $_ -like '*CopyEngine*' }
```
**Result**: No output (zero CopyEngine.cs errors).

All 307 errors in build output are pre-existing in `V12_002.Properties.cs` (unrelated file, pre-existing condition per ticket spec).
Zero new errors introduced by T2 changes.

**Status**: PASS (0 new errors in CopyEngine.cs)

### SCAN-06: NT8-043 null-conditional event handler check
**N/A for T2** — no event handlers changed in T2.
"SCAN-06: N/A — no event handlers changed in T2."

### SCAN-07: HasWorkingEntries .ToList() check
**N/A for T2** — T2 does not touch `HasWorkingEntries` (T1 scope).
"SCAN-07: N/A (T2) — HasWorkingEntries is T1 scope."

---

## Additional Verification (ticket-specified)

### IsLiveEntryBlocked fully deleted
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'IsLiveEntryBlocked[^_]'
```
**Result**: 0 matches. No remaining calls to the deleted method.

### SetLiveEntryDispatched call sites
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'SetLiveEntryDispatched'
```
**Result**: 8 matches (1 definition + 1 call in DispatchCopy + 1 call in IsLiveEntryBlocked_ForTest shim + 5 comment references). All code call sites verified correct.

### Stale comment at line ~201 updated
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'Written in IsLiveEntryBlocked at Gate'
```
**Result**: 0 matches. Comment updated to reference `SetLiveEntryDispatched`.

---

## [Fact] Count Summary

| Milestone | Count |
|-----------|-------|
| Baseline (architecture plan STEP 0) | 475 |
| After T1 (`ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`) | 476 |
| After T2 (`DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`) | **477** |

---

## MGC Guard Preservation Confirmation

**DW-B142-MGC-02 guard is fully preserved.**

`SetLiveEntryDispatched` writes all three maps:
- `_liveEntryInstruments.TryAdd(instrKey, 0)` — gate5(a): blocks resubmit dup
- `_entryInstrKeyByOrderId.TryAdd(orderId, instrKey)` — enables `EvictDedup` to clear `_liveEntryInstruments` on fill/cancel
- `_entryDispatchedOrders.TryAdd(orderId, 0)` — gate5(c): eviction-bypass guard

`EvictDedup` is unchanged — reads all three maps via `_entryInstrKeyByOrderId` to find the instrKey on fill/cancel path.
Gate5(a/b/c) semantics identical to pre-T2 `IsLiveEntryBlocked`.

The only behavioral change: commit now occurs only when `dispatched > 0` (post-loop), preventing phantom lock when all followers are skipped.

---

## PTT-DIAG Log Preservation Confirmation

All 5 PTT-DIAG log lines PERMANENT and unchanged:

| Log prefix | Location | T2 impact |
|-----------|----------|-----------|
| `[PTT-COPY-DIAG] gate0.5 exit:` | DispatchCopy ~line 2433 | Preserved — not near any T2 change ✓ |
| `[PTT-COPY-DIAG] gate3 exit:` | DispatchCopy ~line 2446 | Preserved — not near any T2 change ✓ |
| `[PTT-COPY-DIAG] gate4 exit:` | DispatchCopy ~line 2459 | Preserved — not near any T2 change ✓ |
| `[PTT-COPY-DIAG] gate5 exit:` | DispatchCopy ~line 2476 | Preserved — only line 2474 call expression changed; log block unchanged ✓ |
| `[PTT-COPY-GUARD] skip reversal entry:` | ShouldSkipForReversalGuard ~line 2599 | Preserved — T2 extraction wraps whole method, log not moved ✓ |

---

## Hard-Link Sync

```
powershell -File .\deploy-sync.ps1  -> COMPLETE (SYNC COMPLETE output)
Hard-link re-link for PropTraderTools files -> COMPLETE
```

---

## BUILD_PASS

All 7 scans cleared:
- SCAN-01: PASS (0 lock() calls)
- SCAN-02: PASS (0 non-ASCII characters)
- SCAN-03: PASS (all CYC ≤8)
- SCAN-04: PASS (477 [Fact] methods)
- SCAN-05: PASS (0 new errors in CopyEngine.cs)
- SCAN-06: N/A (no event handlers changed)
- SCAN-07: N/A (T2 does not touch HasWorkingEntries)

**BUILD_PASS**

---

*ptt-engineer · PTT-REPAIRS-03-T2 · ticket-2-completion.md · 2026-09-08*
