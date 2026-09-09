# PTT-REPAIRS-03 Final Review

**Block**: PTT-REPAIRS-03
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-09-08
**Source files verified**: `src/PropTraderTools/CopyEngine.cs` (READ ONLY), `src/PropTraderTools/CopyEngineTests.cs` (READ ONLY)
**Basis**: 02-architecture-plan.md (Revision 2 — V-02), 04-ticket-review.md (TICKET_REVIEW_PASS Cycle 1),
  ticket-1-completion.md (BUILD_PASS), ticket-1-verification.md (VERIFY_PASS),
  ticket-2-completion.md (BUILD_PASS), ticket-2-verification.md (VERIFY_PASS),
  docs/brain/PTT-REPAIRS-02/06-deferred-backlog.md

---

## Section A: Coherent System Check

### A.1 — T1 + T2 Interaction (BUG-A × BUG-B)

**Claim**: T1 prevents the reversal guard false-positive; T2 prevents phantom instrKey when all
followers are skipped (including those skipped by T1's guard).

**Verified from source** (`CopyEngine.cs`):

- `ShouldSkipForReversalGuard` (lines 2576–2601): `followerIsFlat = IsFlat(FindPosition(acc, instr)) && !HasWorkingEntries(acc, instr)` — confirmed at line 2586–2587. When a follower has a working entry order, `followerIsFlat` is `false` → `IsReversalToFlatFollower` returns `false` → `ShouldSkipFollower` returns `false` → follower is NOT skipped → `dispatched++` → `SetLiveEntryDispatched` is called. **BUG-A fix and T2 interact correctly: when T1's fix ALLOWS a follower through, dispatched > 0 and instrKey IS set.**

- Interaction in the skip path: If T1's guard (or any other skip reason) causes ALL followers to be skipped, `dispatched == 0` → `if (dispatched > 0)` is false → `SetLiveEntryDispatched` is NOT called → instrKey is never written to `_liveEntryInstruments`. **T2 prevents the phantom key regardless of WHY all followers are skipped, including when T1's guard legitimately fires.** Combined behavior: first dispatch on a true reversal to a truly-flat follower (T1 guard fires correctly) → `dispatched == 0` → no phantom key → rapid re-entry not blocked at gate5(a). ✓

**Verdict**: T1 + T2 interact correctly. Both bugs resolved independently. Combined system is coherent.

---

### A.2 — PTT-REPAIRS-02 EvictDedup Fill-Clear Path

**Claim**: `EvictDedup` (PTT-REPAIRS-02 fix) still clears instrKey on fill/cancel.
`SetLiveEntryDispatched` (new T2 method) writes instrKey on first dispatch.
`EvictDedup` still clears it. Fill-clear path is intact.

**Verified from source** (`CopyEngine.cs` lines 5808–5844):

- `EvictDedup.Filled` branch (lines 5835–5842): `_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)` → `_liveEntryInstruments.TryRemove(filledInstrKey, out _)`. **Unchanged by T1 or T2.**
- `EvictDedup.Cancelled` branch (lines 5824–5832): Removes `_entryDispatchedOrders`, then `_entryInstrKeyByOrderId`, then `_liveEntryInstruments`. **Unchanged.**
- `SetLiveEntryDispatched` (lines 5789–5794): Writes all three maps. `_entryInstrKeyByOrderId.TryAdd(orderId, instrKey)` at line 5792 is the exact key that `EvictDedup` uses at TryRemove. The fill-clear chain is:
  `SetLiveEntryDispatched` writes `_entryInstrKeyByOrderId[orderId] = instrKey` → fill fires `EvictDedup(Filled)` → TryRemove `_entryInstrKeyByOrderId[orderId]` → TryRemove `_liveEntryInstruments[instrKey]`. ✓

**Verdict**: Fill-clear path is intact. PTT-REPAIRS-02 correction is not regressed by T1 or T2.

---

### A.3 — MGC Guard Preservation (DW-B142-MGC-02)

**Claim**: `SetLiveEntryDispatched` writes `_entryDispatchedOrders`; no other new code path writes or reads it incorrectly.

**Verified from source**:

- `SetLiveEntryDispatched` (line 5793): `_entryDispatchedOrders.TryAdd(orderId, 0)` — confirmed. This is the ONLY write to `_entryDispatchedOrders` in the file after T2's removal of the side effect from `IsEntryDispatched`.
- `IsLiveEntryBlocked_Check` (line 5780): `_entryDispatchedOrders.ContainsKey(orderId)` — read only. No write.
- `IsEntryDispatched` (lines 5758–5761): `return _entryDispatchedOrders.ContainsKey(orderId)` — read only. No write. (TryAdd side effect correctly removed by T2.)
- `EvictDedup.Cancelled` (line 5828): `_entryDispatchedOrders.TryRemove(orderId, out _)` — existing removal, unchanged.
- `IsLiveEntryBlocked_ForTest` shim (lines 4320–4330): calls `SetLiveEntryDispatched` on pass path — writes `_entryDispatchedOrders` via `SetLiveEntryDispatched` only. Correct.

**Gate semantics**:
- Gate5(a): `_liveEntryInstruments.ContainsKey(instrKey)` at line 5776 — written by `SetLiveEntryDispatched`, cleared by `EvictDedup`. ✓
- Gate5(b): `IsDedup` at line 5778 — unchanged. ✓
- Gate5(c): `_entryDispatchedOrders.ContainsKey(orderId)` at line 5780 — written by `SetLiveEntryDispatched`, cleared on cancel by `EvictDedup`. ✓

**Verdict**: `SetLiveEntryDispatched` writes `_entryDispatchedOrders` correctly. No incorrect reads or writes from new code. MGC guard DW-B142-MGC-02 is fully preserved. ✓

---

## Section B: Cross-File JS Violations Check

### B.1 — JS-001: No live collection enumeration without `.ToList()` in hot paths

**T1 change** (`HasWorkingEntries`, line 4869): `foreach (var order in acc.Orders.ToList())` — confirmed at source. `.ToList()` snapshot present. ✓

**T2 changes**: T2 introduces no new enumeration of any live NT8 collection (`Account.Orders`, `Position`, etc.). The new methods `ShouldSkipFollower`, `IsLiveEntryBlocked_Check`, `SetLiveEntryDispatched`, `IsEntryDispatched` use only `ConcurrentDictionary.ContainsKey` and `TryAdd` — these are not live NT8 collections and do not require `.ToList()`. ✓

**No violation found for JS-001.**

### B.2 — JS-021: No `lock()` (all state mutations must use lock-free structures)

**T1 changes**: No `lock()` in `ShouldSkipForReversalGuard` (lines 2576–2601) or `HasWorkingEntries` (lines 4863–4882). ✓
**T2 changes**: No `lock()` in any new method. All map writes use `ConcurrentDictionary.TryAdd` (lock-free). ✓

Layer 3 (ticket-2-verification.md SCAN-01): 70 lines contain `lock` text — all confirmed in comment strings (e.g., `// JS-021: no lock()`). Zero actual `lock(` invocations. ✓

**No violation found for JS-021.**

### B.3 — JS-023: CYC ≤ 8 for all modified methods

| Method | CYC | Budget | Source |
|--------|-----|--------|--------|
| `ShouldSkipForReversalGuard` | 4 | ≤8 ✓ | ticket-1-verification.md SCAN-03 + source lines 2583–2601 independently verified |
| `HasWorkingEntries` | 5 | ≤8 ✓ | ticket-1-verification.md SCAN-03 + source lines 4867–4882 independently verified |
| `ShouldSkipFollower` | 3 | ≤8 ✓ | ticket-2-verification.md SCAN-03 + source lines 2608–2621 independently verified |
| `IsLiveEntryBlocked_Check` | 4 | ≤8 ✓ | ticket-2-verification.md SCAN-03 + source lines 5774–5783 independently verified |
| `SetLiveEntryDispatched` | 1 | ≤8 ✓ | ticket-2-verification.md SCAN-03 + source lines 5789–5794 independently verified |
| `IsEntryDispatched` | 1 | ≤8 ✓ | ticket-2-verification.md SCAN-03 + source lines 5758–5761 independently verified |
| `IsLiveEntryBlocked_ForTest` | 2 | ≤8 ✓ | ticket-2-verification.md SCAN-03 + source lines 4320–4330 independently verified |
| `DispatchCopy` | 8 | ≤8 ✓ | ticket-2-verification.md SCAN-03 branch table + source lines 2429–2528 independently verified |

**Final reviewer independent CYC verification of `DispatchCopy`** (source lines 2429–2528):

| Branch | Line | Running CYC |
|--------|------|-------------|
| base | — | 1 |
| `if (IsExitSignalName(order.Name))` | 2432 | 2 |
| `if (!IsDispatchTriggerState(...))` | 2445 | 3 |
| `if (!IsDispatchableOrderType(...))` | 2458 | 4 |
| `if (IsLiveEntryBlocked_Check(...))` | 2475 | 5 |
| `foreach (var acc in rule.FollowerAccounts)` | 2510 | 6 |
| `if (ShouldSkipFollower(...))` | 2512 | 7 |
| `if (dispatched > 0)` | 2522 | **8** |

**CYC = 8 ≤ 8. ✓**

**No CYC violation found (JS-023 / JS-066).**

### B.4 — JS-025: ASCII-only across all changed lines

T1 SCAN-02 (Layer 3): 0 non-ASCII in lines 2578–2615 and 4839–4860. ✓
T2 SCAN-02 (Layer 3): 0 non-ASCII in entire file. ✓
Source lines read directly by final reviewer (HasWorkingEntries comment, ShouldSkipFollower comment, IsLiveEntryBlocked_Check comment, SetLiveEntryDispatched comment): all ASCII-only. ✓

**No violation found for JS-025 (ASCII-only / JS-042).**

---

## Section C: Missing Wiring Check

### C.1 — `IsLiveEntryBlocked` (deleted): no dangling references

Grep reported in ticket-2-completion.md:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'IsLiveEntryBlocked[^_]'
# Result: 0 matches
```
Independently confirmed in ticket-2-verification.md SCAN (section 1c): 0 matches.

Source inspection confirms:
- `DispatchCopy` line 2475: calls `IsLiveEntryBlocked_Check` — not the deleted method. ✓
- `IsLiveEntryBlocked_ForTest` shim at lines 4320–4330: calls `IsLiveEntryBlocked_Check` + `SetLiveEntryDispatched` — not the deleted method. ✓
- Stale comment at line 201 updated to reference `SetLiveEntryDispatched` (confirmed: "Written in SetLiveEntryDispatched at Gate 5 pass time (PTT-REPAIRS-03 B1 split)."). ✓

**No dangling references to `IsLiveEntryBlocked`. ✓**

### C.2 — `IsLiveEntryBlocked_ForTest` shim wiring

Source lines 4320–4330 verified:
```csharp
internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)
{
    if (IsLiveEntryBlocked_Check(instrKey, orderId, limitPrice))
        return true;
    SetLiveEntryDispatched(instrKey, orderId);
    return false;
}
```
Calls `IsLiveEntryBlocked_Check` (check) + `SetLiveEntryDispatched` (commit) — exactly replicates the `DispatchCopy` check+commit split. ✓

### C.3 — `DispatchCopy` gate5 calls `IsLiveEntryBlocked_Check` (not deleted method)

Source line 2475: `if (IsLiveEntryBlocked_Check(instrKey, orderId, order.LimitPrice))` — confirmed. ✓

### C.4 — `SetLiveEntryDispatched` called only when `dispatched > 0`

Source lines 2522–2523:
```csharp
if (dispatched > 0)
    SetLiveEntryDispatched(instrKey, orderId);
```
Confirmed. `dispatched` initialized at line 2508 (`int dispatched = 0;`), incremented at line 2520 (`dispatched++;`) only after `DispatchToFollower` returns. ✓

`SetLiveEntryDispatched` call sites (from ticket-2-verification.md): 2 production call sites — `DispatchCopy` (line 2523) and `IsLiveEntryBlocked_ForTest` shim (line 4328). Both are correct. ✓

**All wiring checks PASS. No dangling references, no broken wiring. ✓**

---

## Section D: Spec Requirements Satisfied

| Requirement | Status | Evidence |
|-------------|--------|----------|
| **BUG-A**: Reversal guard no longer blocks Buy when follower has a working Sell entry order | ✓ CLOSED | `followerIsFlat = IsFlat(...) && !HasWorkingEntries(acc, instr)` at source line 2586–2587; HasWorkingEntries=true → followerIsFlat=false → IsReversalToFlatFollower=false → guard returns false → dispatch allowed |
| **BUG-B**: instrKey not set when all followers are skipped (phantom key eliminated) | ✓ CLOSED | `if (dispatched > 0) SetLiveEntryDispatched(...)` at source lines 2522–2523; dispatched=0 path never calls SetLiveEntryDispatched → _liveEntryInstruments not written |
| **PTT-DIAG logs**: all 5 permanent log lines present and unmodified | ✓ CLOSED | ticket-2-verification.md PTT-DIAG scan: `[PTT-COPY-DIAG] gate0.5 exit:` line 2435; `gate3 exit:` line 2448; `gate4 exit:` line 2461; `gate5 exit:` line 2478; `[PTT-COPY-GUARD] skip reversal entry:` line 2591; all present and unmodified |
| **[Fact] baseline 475 → 477 (+2)** | ✓ CLOSED | T1 SCAN-04 confirmed 476 (Layer 2 + Layer 3 match); T2 SCAN-04 confirmed 477 (Layer 2 + Layer 3 match); target met exactly |
| **No `lock()` anywhere in changed code** | ✓ CLOSED | SCAN-01: T1=0 in changed ranges; T2=0 actual lock invocations whole-file |
| **ASCII-only** | ✓ CLOSED | SCAN-02: T1=0 non-ASCII in changed ranges; T2=0 non-ASCII whole file |
| **DW-REPAIRS-03-01 promoted in-scope** (`HasWorkingEntries` `.ToList()`) | ✓ CLOSED | `acc.Orders.ToList()` at source line 4869; JS-001 thread-safety comment at lines 4863–4866 |
| **DW-REPAIRS-02-02 baseline measurement** | ✓ CLOSED | Architecture plan Section 0: measured 475 before block start, consistent with PTT-REPAIRS-02 final state |

**All spec requirements satisfied. ✓**

---

## Section E: All 7 Scans — Final Cross-Check

| Scan | Condition | T1 Result | T2 Result | Final Status |
|------|-----------|-----------|-----------|--------------|
| SCAN-01 | `lock()` = 0 in changed methods | PASS (0 in 2578–2615 and 4839–4860) | PASS (0 actual invocations whole file; 70 comment-only) | **PASS** |
| SCAN-02 | Unicode = 0 in changed lines | PASS (0 in changed ranges) | PASS (0 whole file) | **PASS** |
| SCAN-03 | All method CYC ≤ 8 | PASS (ShouldSkipForReversalGuard=4, HasWorkingEntries=5) | PASS (ShouldSkipFollower=3, IsLiveEntryBlocked_Check=4, SetLiveEntryDispatched=1, IsEntryDispatched=1, IsLiveEntryBlocked_ForTest=2, DispatchCopy=8) | **PASS** |
| SCAN-04 | [Fact] = 477 | PASS (476 after T1) | PASS (477 after T2) | **PASS** — final count 477 ✓ |
| SCAN-05 | Build = 0 new errors | PASS (0 errors in CopyEngine.cs; 307 pre-existing in V12_002.*) | PASS (0 errors in CopyEngine.cs; same pre-existing) | **PASS** |
| SCAN-06 | N/A — no event handlers changed | N/A | N/A | **N/A — explicitly confirmed both tickets** |
| SCAN-07 | `HasWorkingEntries` uses `acc.Orders.ToList()` | PASS (confirmed line 4847 → post-T2: line 4869) | N/A (T2 did not touch HasWorkingEntries) | **PASS** — `.ToList()` confirmed in body |

**All 7 scans: PASS or confirmed N/A. Zero violations across `src/PropTraderTools/`. ✓**

---

## Section F: Layer 2 / Layer 3 Agreement Summary

### T1 (ticket-1-verification.md)

| Scan | Layer 2 (engineer) | Layer 3 (verifier) | Agreement |
|------|-------------------|--------------------|-----------|
| SCAN-01 lock (ShouldSkipForReversalGuard range) | 0 | 0 | MATCH ✓ |
| SCAN-01 lock (HasWorkingEntries range) | 0 | 0 | MATCH ✓ |
| SCAN-02 Unicode (both ranges) | 0 | 0 | MATCH ✓ |
| SCAN-03 ShouldSkipForReversalGuard CYC | 4 | 4 | MATCH ✓ |
| SCAN-03 HasWorkingEntries CYC | 5 | 5 | MATCH ✓ |
| SCAN-04 [Fact] count | 476 | 476 | MATCH ✓ |
| SCAN-05 CopyEngine errors | 0 new | 0 new | MATCH ✓ |
| SCAN-06 | N/A | N/A | MATCH ✓ |
| SCAN-07 .ToList() location | line 4847 | line 4847 | MATCH ✓ |

**T1: No discrepancies between Layer 2 and Layer 3. Full agreement on all 7 scans.**

### T2 (ticket-2-verification.md)

| Scan | Layer 2 (engineer) | Layer 3 (verifier) | Agreement |
|------|-------------------|--------------------|-----------|
| SCAN-01 lock | 0 actual | 0 actual (70 comments) | MATCH ✓ |
| SCAN-02 Unicode | 0 whole file | 0 whole file | MATCH ✓ |
| SCAN-03 ShouldSkipFollower | 3 | 3 | MATCH ✓ |
| SCAN-03 IsLiveEntryBlocked_Check | 4 | 4 | MATCH ✓ |
| SCAN-03 SetLiveEntryDispatched | 1 | 1 | MATCH ✓ |
| SCAN-03 IsEntryDispatched | 1 | 1 | MATCH ✓ |
| SCAN-03 IsLiveEntryBlocked_ForTest | 2 | 2 | MATCH ✓ |
| SCAN-03 DispatchCopy | 8 | 8 | MATCH ✓ |
| SCAN-04 [Fact] count | 477 | 477 | MATCH ✓ |
| SCAN-05 build | 0 new errors | 0 new errors | MATCH ✓ |
| SCAN-06 | N/A | N/A | MATCH ✓ |
| SCAN-07 | N/A | N/A | MATCH ✓ |

**T2: No discrepancies between Layer 2 and Layer 3. Full agreement on all 7 scans.**

**Cross-block agreement**: Final reviewer's independent source reads of `DispatchCopy`, `ShouldSkipFollower`, `IsLiveEntryBlocked_Check`, `SetLiveEntryDispatched`, `ShouldSkipForReversalGuard`, `HasWorkingEntries`, and the `IsLiveEntryBlocked_ForTest` shim confirm that all CYC values, wiring, and behavioral semantics reported by Layer 2 and Layer 3 are accurate. No discrepancy found at any level.

---

## Section K: Deferred Work

### Open Items Carried Forward from PTT-REPAIRS-02/06-deferred-backlog.md

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | NT8-043 formal rule entry: null-conditional event unsubscription runtime crash under NT8. SCAN-06 = N/A across PTT-REPAIRS-03 (no event handlers changed). Rule watch continues; zero new evidence. | P2 | B27 or future | OPEN |
| DW-B24-02 | Manual E2E runtime verification (escalated): verify PTT-REPAIRS-01 fixes, PTT-REPAIRS-02 alternating-dispatch defect, **and** PTT-REPAIRS-03 fixes: (a) Buy dispatch allowed when follower has working Sell entry; (b) rapid re-entry after reversal (all-skipped path) not blocked at gate5. Must execute before production release. | P1 | ASAP post-merge | OPEN |
| DW-B24-03 | Skip-duplicate guard `[Fact]`: formal xUnit test for `if (acc == leader) continue` guard. | P2 | B27 | OPEN |
| DW-B25-01 | Companion field race: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` plain singleton refs. Multi-panel topology race risk. | P3 | B28 or future | OPEN |
| DW-B26-01 | Reflection test upgrade (Option B → Option A): `TradeCopierPanel_BeBufferBox_FieldDoesNotExist`. | P2 | B28 or future | OPEN |
| DW-REPAIRS-01-01 | R5 `Account.All` constructor-path risk: null guard in `BuildRuleRow` means account selectors may be empty if `Account.All` is null at construction time. | P2 | B28 or future | OPEN |
| DW-REPAIRS-01-02 | `TryCancelBeOrders` wrapper `-1`-path `[Fact]` test: requires constructable mock `Account` that throws during `Cancel()`. | P2 | B28 or future | OPEN |
| DW-REPAIRS-02-01 | `_entryDispatchedOrders` NOT cleared in Filled branch of `EvictDedup`: orderId lingers after fill unless `TryEvictFollowerBeSlot` fires. Recommend runtime E2E verification. Include in DW-B24-02 E2E session. | P2 | B28+ / DW-B24-02 E2E | OPEN |

### Items Closed This Block

| ID | Item | Closed In | Notes |
|----|------|-----------|-------|
| DW-REPAIRS-02-02 | `[Fact]` count baseline inconsistency: re-measure at next block start | PTT-REPAIRS-03 (architecture plan Section 0) | Architecture plan measured 475 authoritative baseline before any T1/T2 changes. Discrepancy from PTT-REPAIRS-01 stated count still unexplained historically, but current baseline is resolved. CLOSED. |
| DW-REPAIRS-03-01 | `HasWorkingEntries` `acc.Orders.ToList()` fix (DW-B128 thread-safety risk promoted in-scope) | PTT-REPAIRS-03-T1 | `.ToList()` confirmed at source line 4869; JS-001 comment at lines 4863–4866. CLOSED. |

### New Items This Block

No new deferred items identified. All plan scope items were implemented and verified. The warn from 04-ticket-review.md (stale comments at DispatchCopy lines 2470–2471) was resolved by the engineer: lines 2470–2472 updated to accurate description of `IsLiveEntryBlocked_Check + SetLiveEntryDispatched` pattern (confirmed at source). No new open items arise.

---

## Final Verdict

**Violations found**: NONE

All coherence checks pass. No cross-file JS violations. No dangling references. All spec requirements satisfied. All 7 scans zero (or confirmed N/A) across `src/PropTraderTools/`. Layer 2 and Layer 3 in full agreement on both tickets. Section K written. 06-deferred-backlog.md written.

## FINAL_PASS

---

*ptt-plan-reviewer · PTT-REPAIRS-03 · 05-final-review.md · Phase 5 · 2026-09-08*
