# Ticket 1 Verification Report

**Epic**: B195
**Ticket**: 1 -- DW-B195-01: Clone Mode SubmitDrainedEntry Must Call SendCopyWithAtm
**Date**: 2026-10-11
**Verifier**: ptt-verifier
**Phase**: 4b (Independent Verification)

---

## Verdict

**VERIFY_FAIL**

**Reason**: `src/PropTraderTools/Tests/B195Tests.cs` does NOT exist.
The ticket specification (04-tickets.md, Step 3) and the BUILD_PASS gate criteria both
explicitly require creation of `B195Tests.cs` with 4 xUnit [Fact] test methods.
The engineer's completion report omits test results entirely and the file is absent from
`src/PropTraderTools/Tests/`. This is a mandatory deliverable -- its absence is a
VERIFY_FAIL regardless of the correctness of the CopyEngine.cs changes.

All 7 scans PASS. CopyEngine.cs implementation is CORRECT. The only blocking failure
is the missing test file.

---

## Independent Scan Results (All 7)

| Scan | Description | Command | My Result | Engineer Result | Match? | Status |
|------|-------------|---------|-----------|-----------------|--------|--------|
| SCAN-01 | lock() ban (JS-021) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\("` | 9 comment-only hits, all say "no lock()"; zero actual lock() calls | 9 comment-only matches; zero actual lock() | YES | PASS |
| SCAN-02 | async void ban (JS-033) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void "` | 1 comment-only hit ("NOT async void (JS-033)"); zero actual async void declarations | 1 comment-only; zero actual | YES | PASS |
| SCAN-03 | throw new Exception ban (JS-001) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new "` | 0 matches (command returned no output) | 0 matches | YES | PASS |
| SCAN-04 | CYC <= 8 (JS-066) | Manual branch count of SubmitDrainedEntry lines 6722-6758 | CYC=5 (4 branches + base 1; see CYC section below) | CYC=5 | YES | PASS |
| SCAN-05 | ASCII-only | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"` | 0 matches | 0 matches | YES | PASS |
| SCAN-06 | DateTime.Now ban | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now"` | 1 comment-only hit; zero actual calls | 1 comment-only; zero actual | YES | PASS |
| SCAN-07 | return null ban (JS-002) | `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "return null"` | Multiple pre-existing matches in other methods; zero in modified region 6713-6758; SubmitDrainedEntry is void (N/A) | Multiple pre-existing; none in modified region; void method N/A | YES | PASS |

All 7 scans: **PASS**. No discrepancy between my independent results and the engineer's reported results.

---

## CYC Verification (Independent)

### Independent Branch Count -- SubmitDrainedEntry (lines 6722-6758)

```
Branch 1: if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload))  -- line 6724
Branch 2: if (follower == null)                                              -- line 6728
Branch 3: if (atmMode is FollowerAtmMode.Named namedAtm)                    -- line 6732  [NEW]
Branch 4: foreach (var id in payload.DrainedOrderIds)                       -- line 6756
```

CYC = base(1) + 4 branch points = **5**

| Method | My CYC Count | Engineer CYC | Match? | Limit | Status |
|--------|-------------|-------------|--------|-------|--------|
| `SubmitDrainedEntry` | 5 | 5 | YES | 8 | PASS |

---

## Implementation Correctness Checklist

| # | Check | Source Evidence | Status |
|---|-------|-----------------|--------|
| 1 | GetCloneAtmMode() is called INSIDE SubmitDrainedEntry (Option A) | Line 6731: `var atmMode = GetCloneAtmMode();` inside SubmitDrainedEntry body | PASS |
| 2 | GetCloneAtmMode() is NOT called in DrainThenDispatch | DrainThenDispatch (lines 6579-6644) read in full -- no GetCloneAtmMode() call present | PASS |
| 3 | Pattern match: `if (atmMode is FollowerAtmMode.Named namedAtm)` | Line 6732: `if (atmMode is FollowerAtmMode.Named namedAtm)` -- exact match | PASS |
| 4 | Named ATM path calls SendCopyWithAtm(followerAccount, instrument, in signal, namedAtm) | Line 6741: `SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm);` | PASS |
| 5 | signal constructed via CopySignal.Create with correct parameters | Lines 6734-6740: CopySignal.Create(payload.Action, payload.OrderType, payload.Qty, payload.Price, orderId) -- 5-param match | PASS |
| 6 | orderId = FollowerAcctKey + "-drain-atm-" + TimestampTicks | Line 6739: `payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks` | PASS |
| 7 | Else path calls SubmitEntryDirect with same parameters as before | Lines 6745-6752: SubmitEntryDirect(follower, payload.Instrument, payload.Qty, payload.Price, payload.Action, payload.OrderType) -- identical to original | PASS |
| 8 | Post-dispatch drain-ID cleanup loop preserved (foreach drainedIds) -- R3-F2 | Lines 6756-6757: `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _);` present after both dispatch paths | PASS |
| 9 | PendingDispatchDrain class has NO new ATM field (Option A verified) | PendingDispatchDrain (lines 6800-6840) read in full: 10 fields only (FollowerAcctKey, Instrument, Qty, Price, Action, OrderType, DrainedOrderIds, FollowerAccount, PendingCancelCount, TimestampTicks). No ATM field added. | PASS |
| 10 | DrainThenDispatch is UNCHANGED | DrainThenDispatch (lines 6579-6644) read in full: no ATM-related code, no GetCloneAtmMode() call. Constructor call at line 6618-6629 still has 10 params (unchanged). | PASS |
| 11 | CYC comment above method updated from CYC=4 to CYC=5 | Lines 6713-6721: comment reads "B195: SubmitDrainedEntry is now ATM-mode-aware. CYC=5: ..." -- updated correctly | PASS |
| 12 | B195Tests.cs created with 4 [Fact] test cases | **File does NOT exist** -- confirmed by directory listing of src/PropTraderTools/Tests/; no B195Tests.cs present | **FAIL** |
| 13 | GetCloneAtmMode() unchanged (lines 745-754) | Lines 741-754 read: method is identical to plan spec, returns Named(obj) or Named(cache) or Inherit -- unchanged | PASS |

---

## Cross-Check Summary

### Scan Result Discrepancies
None. All 7 independent scan results match the engineer's reported results exactly.

### CYC Count Discrepancy
None. My independent count of 5 matches the engineer's reported CYC=5.

### Critical Omission -- Missing Test File
The engineer's completion report (`ticket-1-completion.md`) BUILD_PASS declaration table
contains NO row for "All 4 B195Tests.cs [Fact] methods pass". This omission was NOT
accidental -- the test file was never created.

The ticket specification (04-tickets.md) states explicitly:
> "Step 3 -- Create test file src/PropTraderTools/Tests/B195Tests.cs"
> "One file created: src/PropTraderTools/Tests/B195Tests.cs with 4 [Fact] test cases."

The BUILD_PASS gate criteria in 04-tickets.md includes:
> "[ ] All 4 B195Tests.cs [Fact] methods pass"

The engineer's completion report does not satisfy this gate criterion.
The directory listing confirms the file is absent (no B195Tests.cs in Tests/).

**This is a VERIFY_FAIL condition.**

---

## Spec Satisfaction

### DW-B195-01: Clone Mode SubmitDrainedEntry Not ATM-Aware

**CLOSED: YES (subject to test file creation)**

The root cause described in B194/06-deferred-backlog.md was:
> "SubmitDrainedEntry calls SubmitEntryDirect unconditionally. The replacement entry order has
> no ATM brackets. SyncFollowerBracket finds fo=NULL on all followers."

The fix is correctly implemented:
- `GetCloneAtmMode()` is called inside `SubmitDrainedEntry` at dispatch time (line 6731).
- When Named ATM mode is active, `SendCopyWithAtm` is called (line 6741) -- StartAtmStrategy fires.
- The else path preserves existing behavior for non-ATM mode (line 6745).
- The fix is Option A (re-fetch at dispatch, no PendingDispatchDrain field change).

**Implementation correctness**: CONFIRMED (11/12 implementation checks PASS; failure is test file only).
**Root cause closed**: YES -- the buggy unconditional `SubmitEntryDirect` call has been replaced.

### B167-DEFER-02: Clone+ATM Follower Bracket Dispatch (Historical Deferred)

**CLOSED (drain-completion path): YES (subject to test file creation)**

B167-DEFER-02 documented that Clone+ATM follower bracket dispatch was deferred from B167.
The regular copy path was fixed in B167; the drain-completion path (`SubmitDrainedEntry`) was not.
This ticket directly closes the drain-completion path gap. Followers will now receive
"Entry"-named ATM-armed orders via `SendCopyWithAtm` when `SubmitDrainedEntry` fires in Clone+ATM mode.

**Caveat**: DW-B195-02 (opened by B195 architecture plan §8) remains open: `DrainThenDispatch`
fast-path (when no entry candidates exist) still calls `SubmitEntryDirect` without ATM check.
That is a SEPARATE deferred item for B196, not in scope for this ticket.

### SIM Step 5 (fo=<order> not fo=NULL)

**Fix path confirmed: YES**

After this fix, when a drain completes in Clone+ATM mode:
1. `SubmitDrainedEntry` calls `SendCopyWithAtm` (not `SubmitEntryDirect`).
2. `SendCopyWithAtm` calls `StartAtmStrategy` with the ATM template, which creates
   "Entry"-named order + native ATM brackets (Target/Stop) on the follower account.
3. After the leader fills, `SyncFollowerBracket` queries follower orders and finds
   the bracket orders (`fo=<order>`) because the ATM strategy is active and armed.
4. The `fo=NULL` condition that triggered naked positions and PTT-Flatten loops is resolved.

The fix enables SIM Step 5 to pass. Whether it actually passes depends on SIM execution.

---

## Summary Table

| Category | Result | Notes |
|----------|--------|-------|
| SCAN-01 lock() | PASS | 9 comment-only hits, zero actual lock() |
| SCAN-02 async void | PASS | 1 comment-only hit, zero actual async void |
| SCAN-03 throw new | PASS | 0 matches |
| SCAN-04 CYC | PASS | CYC=5 (my count matches engineer) |
| SCAN-05 ASCII-only | PASS | 0 non-ASCII chars |
| SCAN-06 DateTime.Now | PASS | 1 comment-only, zero actual |
| SCAN-07 return null | PASS | Pre-existing only; none in modified region; void method |
| CYC comment updated | PASS | Lines 6713-6721: "CYC=5" present |
| Implementation correctness | PASS (11/12) | All code checks pass |
| Test file B195Tests.cs | **FAIL** | **File absent from src/PropTraderTools/Tests/** |
| BUILD_PASS gate criteria | **FAIL** | Missing test file + test results from engineer report |

---

## Required Fix for VERIFY_PASS

The engineer must:

1. **Create `src/PropTraderTools/Tests/B195Tests.cs`** with the 4 xUnit [Fact] test cases
   specified in 04-tickets.md §7:
   - `T1_SubmitDrainedEntry_CloneAtmMode_CallsSendCopyWithAtm`
   - `T1_SubmitDrainedEntry_NoAtmMode_CallsSubmitEntryDirect`
   - `T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode`
   - `T1_SubmitDrainedEntry_MissingPayload_ReturnsEarly`

2. **Run the tests** and paste xUnit output into a revised `ticket-1-completion.md` confirming
   all 4 [Fact] methods PASS.

3. **Re-submit for Phase 4b verification** (Cycle 2).

**The CopyEngine.cs changes are CORRECT and do NOT need to be modified.**

---

*Written by: ptt-verifier*
*Epic: B195*
*Phase: 4b (Verification)*
*Return: VERIFY_FAIL*
*Retry cycle: 1 of 3*