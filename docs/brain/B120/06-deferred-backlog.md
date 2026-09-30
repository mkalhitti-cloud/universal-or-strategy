# B120 Deferred Backlog

Block: B120 (DW-B129 Leader Fallback Flatten)
Date: 2026-08-28
Status: PIPELINE_COMPLETE (coding phases)

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B129 | Leader left open after B118 PTT-BE cancel — fallback flatten via `acc.Flatten(pos.Instrument)` in `PttGlobalQuickExit.Execute()` | B120-T1 |

---

## New Deferred Items (B120 pipeline)

### B120-DEFER-01 — F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 — prerequisite for SIM gate and go-live
**Context**: B120 changes are code-complete (BUILD_PASS, VERIFY_PASS). The F5 NinjaTrader 8
compilation step is the runtime compile gate. After `ptt-sync-and-verify.ps1` completes with
0 MISMATCH, Director must press F5 in NinjaTrader 8 to produce "Compilation succeeded"
(zero errors) in the NT8 output window. This confirms the NT8 Roslyn compiler accepts all
B120 changes: `NeedsLeaderFallbackFlatten(int, int, int)` helper, `acc.Flatten(pos.Instrument)`
call, and `ExecuteFollowers(Account, Position, List<...>, (int,int), double)` extraction.
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for B120-DEFER-02).

---

### B120-DEFER-02 — SIM Gate: Fallback Flatten Behavioral Verification

**Priority**: P1 — required before next live session with BE-ALL then QX-ALL
**Context**: DW-B129 code changes verified by independent code inspection (VERIFY_PASS).
Full behavioral validation requires a live NT8 SIM session with leader (Sim101) in PTT-BE
state, then QX-ALL fired.

**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier.
2. Fire BE-ALL — PTT-BE brackets placed on all 4 accounts.
3. Fire QX-ALL.
   - Expected on leader: `[PTT-QX-ALL] CancelPttBeOrders: acc=Sim101 count=6`
   - Expected on leader: `[PTT-QX-ALL] WaitForPttBeCancelled: acc=Sim101 completed`
   - Expected on leader: `[PTT-QX-FLATTEN] leader fallback flatten: Sim101 MES SEP26 qty=N`
   - Expected: leader position closed, no naked open position.
4. Followers should run normal QX path (not affected by B120).
   - Expected on followers: `[PTT-QX-ALL] follower: SimXXX ...` + normal QX bracket swap.
   - Expected: `[PTT-QX-FLATTEN]` does NOT appear for any follower.

**Pass criterion**: `[PTT-QX-FLATTEN]` line appears for leader; leader position closes;
followers unaffected (normal QX path); zero naked positions after QX-ALL.
**Fail criterion**: any naked position; `[PTT-QX-FLATTEN]` appears for follower (wrong path);
leader position remains open after QX-ALL.
**Deferred to**: Director SIM gate session (after B120-DEFER-01 green).

---

## Carry-Forward Items (from B119 — status unchanged)

Items below are copied from `docs/brain/B119/06-deferred-backlog.md`.
B120 changes do not affect any of these items.

---

### B119-DEFER-01 — F5 NinjaTrader 8 Compilation Gate (B119 changes)

**Priority**: P0 — prerequisite for SIM gate and go-live
**Context**: B119 changes are code-complete (BUILD_PASS, VERIFY_PASS). The F5 NinjaTrader 8
compilation step is the runtime compile gate. After `ptt-sync-and-verify.ps1` completes with
0 MISMATCH, Director must press F5 in NinjaTrader 8 to produce "Compilation succeeded"
(zero errors) in the NT8 output window. This confirms the NT8 Roslyn compiler accepts all
B119 changes including `ConcurrentDictionary<string, OrderAction>` field and the
`internal static bool IsReversalToFlatFollower(...)` method.
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for B119-DEFER-02).

---

### B119-DEFER-02 — SIM Gate: Reversal Guard Behavioral Verification

**Priority**: P1 — required before next live trading session involving reversal entries
**Context**: DW-B128 code changes have been implemented and verified by independent code
inspection (VERIFY_PASS). Full behavioral validation requires a live NT8 SIM session
with leader + follower accounts. The following scenarios must be exercised:

**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier (Long).
2. Close the position (all accounts go flat).
3. Open position in the opposite direction on leader (Short reversal).
   - **Expected**: `[PTT-COPY-GUARD] skip reversal entry: SimXXX <instrument> follower flat`
     appears in NT8 Output tab for each follower that is flat at dispatch time.
   - **Expected**: Flat followers do NOT receive the reversal entry — no unwanted short position opened.
4. Re-enter in same direction (leader sends Buy again after Short).
   - **Expected**: Guard does NOT fire (same direction as last dispatched). All followers receive.
5. Enter from cold start (no prior direction recorded for instrument).
   - **Expected**: Guard does NOT fire (first entry). All followers receive.
6. Enter on leader with some followers flat and some with open position (mixed state).
   - **Expected**: Flat followers skipped (guard fires per-follower); followers with open position receive copy.

**Pass criterion**: `[PTT-COPY-GUARD]` log lines appear for flat followers on reversal only;
zero unwanted positions opened on flat followers; first-entry and same-direction entries
copy normally; per-follower independence confirmed.
**Fail criterion**: any unwanted position opened on a flat follower; any first-entry or
same-direction entry blocked; any `[PTT-COPY-GUARD]` line for a non-flat follower.
**Deferred to**: Director SIM gate session (after B119-DEFER-01 green).

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 — correctness violation, functionally benign in observed test
**Discovered**: 2026-08-25 live BE-ALL test (stopped out, Copier ON, 4 accounts)
**Context**: `MoveStopToBreakEven` Step A (`CopyEngine.cs` ~L3380) collects target orders into
a single flat list with no native-vs-PTT discrimination and no count cap. A stale
`PTT-BE-Target-4` from a prior session (still `Working` in `acc.Orders`) was included in
the snapshot and an extra OCO pair submitted. Same class as DW-B106 (which fixed the QX path
in B107-T1 — BE path not in scope).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**Spec section**: `specs/002-trade-copier-spec.html#section-b107`

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate (B107 changes)

**Priority**: P0 — prerequisite for SIM gate and go-live
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified) for B107.
Director must press F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1 — required before next live trading session involving BE-ALL then QX-ALL
**Context**: DW-B105 + DW-B106 code changes verified. Full behavioral validation of Combo C
scenario (QX-ALL followed by BE-ALL, stale partial-fill residue case) requires a live NT8 session.

**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier
2. Fire BE-ALL — confirm BE brackets placed on all 4 accounts
3. Fire QX-ALL — confirm `[PTT-QX-GUARD] pre-cancel follower brackets: SimXXX` appears for each follower,
   zero `[BE-DIAG]` lines during QX sweep, all 4 accounts covered, exactly 3 PTT-QX-T* brackets
4. Confirm no naked positions after sweep

**Pass criterion**: zero `[BE-DIAG]` lines during QX sweep; all 4 accounts covered;
exactly 3 PTT-QX-T* brackets; no unprotected position.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification required

**Priority**: High — required before next live trading session
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All -> BE All must place targets at BE price (not bare stop)
- Direction 2: BE All -> Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)
**Action**: Press sequence in SIM account before go-live.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If a future block adds PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — DW-B85 Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, the Option B
warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the
account later appears in Account.All. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.
**Fix**: In AllAccounts(), replace null-skip with a lazy re-resolve.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High — required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles, checking stops=N > 0 on each follower) requires a live NT8 session.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).
**Action**: Run Path B test sequence (3 cycles) in SIM before go-live.

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L4093. Confirmed pre-existing and unrelated to B120.
Note: B120Tests.cs compiles cleanly; the 83 errors are in unrelated test files.
**Deferred to**: Dedicated test infrastructure remediation block.
**Action**: Separate remediation track.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 — blocks DW-B89 SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader for DW-B89 changes produces
"Compilation succeeded" 0 errors.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Context**: Entry -> BE-ALL -> verify Output tab has NO `[BE-ERR]` lines, stops=N for all accounts.
3 cycles. PASS criterion: zero error popups, zero naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short -> BE-ALL buf=0t immediately. Verify `[BE-ERR]` lines appear if price
moved OR stops placed successfully if price still at entry. NO naked positions. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger.
Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
Merges DW-PTT-BE-FIX-02 (Path B 3-cycle verification).
Note: B107-DEFER-02 (Combo C re-test) is the complementary test covering BE-ALL then QX-ALL.
Both remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections `#section-b89`, `#section-b88`, `#section-b87`
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Action**: Director updates spec after full SIM gate PASS.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary Table

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 1 | DW-B129 |
| New deferred (B120) | 2 | B120-DEFER-01 (F5), B120-DEFER-02 (SIM gate — fallback flatten behavioral) |
| Carry-forward (B119 items) | 2 | B119-DEFER-01 (F5), B119-DEFER-02 (Reversal Guard SIM verification) |
| Carry-forward (B107 pipeline) | 3 | DW-B107 (MoveStopToBreakEven stale PTT-BE-Target-*), B107-DEFER-01 (F5), B107-DEFER-02 (Combo C live test) |
| Carry-forward (DW-B89 and earlier) | 11 | DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |
| **Total open items** | **18** | B120-DEFER-01/02, B119-DEFER-01/02, DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |
