# B118 Deferred Backlog

Block: B118 (DW-B126 + DW-B127 fix)
Date: 2026-08-28
Status: PIPELINE_COMPLETE (pending Director F5 gate + SIM gate)

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B126 | BE/QX race -- PTT-BE-* cancelled before QX snapshot | B118-T1 |
| DW-B127 | Stale QX window rapid double-press -- structurally eliminated | B118-T1 |

---

## New Deferred Items From This Block

### B118-DEFER-01 -- F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 -- prerequisite for SIM gate
**Context**: `ptt-sync-and-verify.ps1` must confirm 0 MISMATCH after B118-T1 sync (MD5-verify
all files copied to NT8 installation directory). Once sync is confirmed clean, Director presses
F5 in NinjaTrader 8 to trigger runtime recompilation. This gate must be green before the SIM
gate (B118-DEFER-02) can begin.
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Expected output**: "Compilation succeeded" with zero errors in the NT8 Output tab.
**Fail condition**: Any compilation error in PttGlobalQuickExit.cs or related files.

---

### B118-DEFER-02 -- SIM Gate: BE-ALL then QX-ALL Race Scenario

**Priority**: P1 -- required before next live trading session
**Context**: DW-B126 fix was verified by code inspection (VERIFY_PASS, ptt-verifier, 2026-08-28).
Full behavioral validation requires a live NT8 SIM session with leader + follower accounts.

**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier
2. Fire BE-ALL -- confirm PTT-BE-Target-* and PTT-BE-Stop-* placed on all accounts
3. Wait 1-2 seconds (allow PTT-BE brackets to reach Working state)
4. Fire QX-ALL immediately
5. Confirm in Output tab: `[PTT-QX-ALL] CancelPttBeOrders: acc=SimXXX count=N` appears for each account
6. Confirm: `[PTT-QX-ALL] WaitForPttBeCancelled: acc=SimXXX waiting count=N` appears for each account
7. Confirm: `[PTT-QX-ALL] WaitForPttBeCancelled: acc=SimXXX completed` appears for each account (not TIMEOUT)
8. Confirm PTT-QX-* brackets placed correctly on all accounts
9. Confirm no naked positions, no oversell (all accounts flat or at expected residual position)

**Pass criterion**: cancel-ptt-be log appears with count > 0 for accounts with active PTT-BE
orders; WaitForPttBeCancelled completes (not TIMEOUT); correct PTT-QX sizing (no oversell);
no naked position on any account.

**Fail criterion**: TIMEOUT logged (cancel did not confirm in 1000ms); oversell observed
(position quantity overshot -- DW-B126 still active); naked position (account with open
position but no PTT-QX-Stop protecting it).

**Regression check (no prior BE-ALL)**:
- Fire QX-ALL without a prior BE-ALL.
- Confirm: `[PTT-QX-ALL] CancelPttBeOrders: acc=SimXXX count=0 (no active PTT-BE orders)` appears.
- Confirm: WaitForPttBeCancelled fast-path fires (no waiting log, returns immediately).
- Confirm: Existing QX behavior identical to pre-B118 path.

**DW-B127 structural check (rapid double-press)**:
- Fire QX-ALL twice in rapid succession (< 200ms interval).
- Confirm: Second press shows count=0 for all accounts on CancelPttBeOrders.
- Confirm: WaitForPttBeCancelled fast-paths on second press.
- Confirm: No duplicate PTT-QX-* orders (existing _qxCancelInProgress guard fires).
- Confirm: Single set of PTT-QX-* Working orders, not doubled.

**Note**: This test partially subsumes B107-DEFER-02 (Combo C live re-test). Director may
close both B107-DEFER-02 and B118-DEFER-02 after a single consolidated SIM gate session
that covers all the above sequences.

---

## Carry-Forward Items

All open items from `docs/brain/B107/06-deferred-backlog.md` carried forward unchanged.
B118 changes do not affect any of these items.

---

### DW-B107 -- MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 -- correctness violation, functionally benign in observed test
**Discovered**: 2026-08-25 live BE-ALL test (stopped out, Copier ON, 4 accounts)
**Context**: Sim102/103/104 each submitted 4 OCO bracket pairs on a 3-target ATM. Sim101
(leader) correct at 3. `MoveStopToBreakEven` Step A (`CopyEngine.cs` ~L3380) collects
target orders into a single flat list with no native-vs-PTT discrimination and no count cap.
A stale `PTT-BE-Target-4` from a prior session (still `Working` in `acc.Orders`) was
included in the snapshot and an extra OCO pair submitted.
**Same class as**: DW-B106 (which fixed the QX path in B107-T1 -- BE path not in scope).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**CYC note**: Fix requires extraction of the Step A loop into a new `SnapshotBeTargets`
helper to keep `MoveStopToBreakEven` at CYC <= 8. Architect must plan extraction first.

---

### B107-DEFER-01 -- F5 NinjaTrader 8 Compilation Gate (B107 changes)

**Priority**: P0 -- prerequisite for SIM gate and go-live
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified).
The F5 NinjaTrader 8 compilation step is the runtime compile gate. It must produce
"Compilation succeeded" (zero errors) after sync. This is Director-owned (requires local NT8 session).
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 -- Combo C Live Re-Test

**Priority**: P1 -- required before next live trading session involving BE-ALL then QX-ALL
**Context**: DW-B105 + DW-B106 code changes have been implemented and verified by independent
code inspection (VERIFY_PASS). Full behavioral validation of the Combo C scenario
(QX-ALL followed by BE-ALL, stale partial-fill residue case) requires a live NT8 session
with leader + follower accounts.
**Note**: Partially subsumed by B118-DEFER-02. Director may close both simultaneously.

**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier
2. Fire BE-ALL -- confirm BE brackets placed on all 4 accounts
3. Fire QX-ALL -- confirm:
   - `[PTT-QX-GUARD] pre-cancel follower brackets: Sim10X` appears in Output tab for each follower
   - Zero `[BE-DIAG]` lines during QX sweep (guards firing correctly)
   - All 4 accounts covered by PTT-QX-* brackets (none unprotected)
   - Exactly 3 target brackets submitted (PTT-QX-T1, T2, T3) -- no T4
4. Confirm no naked positions after sweep

**Pass criterion**: zero [BE-DIAG] lines during QX sweep; all 4 accounts covered;
exactly 3 PTT-QX-T* brackets; no unprotected position.
**Fail criterion**: any unprotected position, any T4 bracket, any [BE-DIAG] line that was
previously absent.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B42-01 -- T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 -- Live NT8 F5 verification required (B42 changes)

**Priority**: High -- required before next live trading session
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All -> BE All must place targets at BE price (not bare stop)
- Direction 2: BE All -> Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)
**Action**: Press sequence in SIM account before go-live.

---

### DW-B42-03 -- IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design
(PTT-QX-T1 in OCO-A, PTT-QX-T2 in OCO-B, T3 as potential 3rd slot). If a future block adds
PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 -- DW-B85 Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, the Option B
warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the
account later appears in Account.All. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.
**Fix**: In AllAccounts(), replace null-skip with a lazy re-resolve.

---

### DW-PTT-BE-FIX-02 -- SIM gate: Path B 3-cycle runtime verification

**Priority**: High -- required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles, checking stops=N > 0 on each follower) requires a live NT8
session with leader + follower accounts and open positions.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).
**Action**: Run Path B test sequence (3 cycles) in SIM before go-live.

---

### DW-PTT-BE-FIX-03 -- Pre-existing test build errors (CopyEngineTests.cs stub infrastructure)

**Priority**: High -- blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B118.
**Deferred to**: Dedicated test infrastructure remediation block.
**Action**: Separate remediation track.

---

### DW-B89-DEFERRED-01 -- Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 -- blocks DW-B89 SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader for DW-B89 changes produces
"Compilation succeeded" 0 errors.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 -- SIM gate PATH A nominal

**Priority**: High
**Context**: Entry -> BE-ALL -> verify Output tab has NO [BE-ERR] lines, stops=N for all accounts.
3 cycles. PASS criterion: zero error popups, zero naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 -- SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short -> BE-ALL buf=0t immediately. Verify [BE-ERR] lines appear if price
moved OR stops placed successfully if price still at entry. NO naked positions. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 -- SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger.
Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
**Merges**: DW-PTT-BE-FIX-02 (Path B 3-cycle verification).
**Note**: B107-DEFER-02 (Combo C re-test) and B118-DEFER-02 are complementary tests
covering the reverse direction (BE-ALL then QX-ALL). All remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 -- SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 -- Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Action**: Director updates spec after full SIM gate PASS.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 2 | DW-B126, DW-B127 |
| New deferred (B118 pipeline) | 2 | B118-DEFER-01 (F5), B118-DEFER-02 (SIM gate) |
| Carry-forward (unchanged from B107) | 14 | DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 16 (2 new + 14 carry-forward)
