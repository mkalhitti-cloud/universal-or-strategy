# B127 Deferred Backlog

Block: B127 (DW-PTT-BE-FIX-01 -- Lazy re-resolve for null followers in AllAccounts())
Date: 2026-08-25
Status: PIPELINE_COMPLETE

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-PTT-BE-FIX-01 | Lazy re-resolve Option A for null followers in AllAccounts() | B127-T1 |

---

## New Deferred Items -- B127 Pipeline

### B127-DEFER-01 -- SIM Gate: Lazy Re-Resolve Runtime Verification

**Priority**: P1 -- required before next live session involving late-connecting follower accounts
**Context**: B127Tests.cs uses test seam option (c) -- observable struct behavior + reflection.
T1 verifies FollowerAccountNames derived from accounts (backward compat path).
T2 verifies explicit FollowerAccountNames preserved through the 8th-arg path (DtoToRule).
T3 verifies AllAccounts() method is internal and returns IEnumerable<Account>.
Account.All (NT8 API) is unavailable in the MSBuild test runtime -- the full lazy Account.All
resolution path (FindFollowerAccount call in AllAccounts()) is not exercised by the test suite.
**Action**: Director runs a SIM session:
1. Load rules with a follower account name that is not yet connected (simulated by name-only DTO entry).
2. Confirm WARNING "follower 'X' not found in Account.All -- account not connected yet" appears.
3. Connect / enable the follower account so it appears in Account.All.
4. Fire a trade event (place + fill a copy order).
5. Confirm INFO "follower 'X' resolved lazily -- now copying to this account." appears.
6. Confirm the resolved account receives the copied order.
**Deferred to**: B128 or next SIM gate block.

---

### B127-DEFER-02 -- Warning Throttle for Persistent Lazy-Fail

**Priority**: P2 -- cosmetic / low noise risk
**Context**: AllAccounts() emits one WARNING per call when a follower account is not found
during lazy re-resolve. AllAccounts() fires per trade event (order fill, cancel, BE sweep),
not per tick -- repeated warnings during a prolonged disconnect are acceptable and provide
useful signal per plan Section F. If production noise is observed (e.g., rapid BE-ALL firing
with a disconnected follower), a throttle (once per N seconds, or once until _resolvedFollowers
is cleared) could be added.
**Action**: Monitor Output tab during first live session after B127-DEFER-01 SIM pass.
If noise is excessive, add a ConcurrentDictionary<string, DateTime> throttle in AllAccounts().
**Deferred to**: future productionization block (only if noise observed in practice).

---

## Carry-Forward Items (unchanged from B107/06-deferred-backlog.md)

### DW-B42-01 -- T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate IsPttQxTarget also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets.
T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add Assert.True(IsPttQxTargetInline("PTT-QX-T3")) to T_BUG_QX_BE_01.

---

### DW-B42-02 -- Live NT8 F5 verification required

**Priority**: High -- required before next live trading session
**Context**: Quick All -> BE All must place targets at BE price; BE All -> Quick All must start
from clean slate. Requires live NT8 session.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 -- IsPttQxTarget range extension for future target slots

**Priority**: Conditional/Low
**Context**: Current range name[8] >= '1' && name[8] <= '3'. If future block adds PTT-QX-T4/T5,
IsPttQxTarget must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-02 -- SIM gate: Path B 3-cycle runtime verification

**Priority**: High -- required before next live session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes stop name guard but full SIM of Path B (QX-ALL then BE-ALL,
3 cycles, stops=N > 0 on each follower) requires live NT8 with leader + follower and open positions.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 -- Pre-existing test build errors

**Priority**: High -- blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B107/B127.
**Deferred to**: Dedicated test infrastructure remediation block.

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
moved OR stops placed successfully if still at entry. NO naked positions. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 -- SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger.
Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
Merges DW-PTT-BE-FIX-02 (Path B 3-cycle verification).
Note: B107-DEFER-02 (Combo C re-test) is the complementary test (BE-ALL then QX-ALL).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 -- SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 -- Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: specs/002-trade-copier-spec.html sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

### DW-B107 -- MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 -- correctness violation, functionally benign in observed test
**Context**: MoveStopToBreakEven Step A (~L3380) collects target orders into a flat list with
no native-vs-PTT discrimination and no count cap. A stale PTT-BE-Target-4 from a prior session
(still Working in acc.Orders) was included in the snapshot and an extra OCO pair submitted.
Same class as DW-B106 (which fixed the QX path in B107-T1 -- BE path not in scope).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: docs/brain/DW-B107/00-defect-brief.md

---

### B107-DEFER-01 -- F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 -- prerequisite for SIM gate and go-live
**Context**: ptt-sync-and-verify.ps1 completed with 0 MISMATCH (16 files MD5-verified).
The F5 NinjaTrader 8 compilation step is the runtime compile gate. Director-owned (requires local NT8 session).
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 -- Combo C Live Re-Test

**Priority**: P1 -- required before next live session involving BE-ALL then QX-ALL
**Context**: DW-B105 + DW-B106 code changes implemented and verified (VERIFY_PASS). Full
behavioral validation of Combo C (QX-ALL followed by BE-ALL, stale partial-fill residue case)
requires live NT8 with leader + follower accounts.
**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier
2. Fire BE-ALL -- confirm BE brackets on all 4 accounts
3. Fire QX-ALL -- confirm [PTT-QX-GUARD] pre-cancel lines, zero [BE-DIAG] lines, exactly 3 PTT-QX-T* brackets
4. Confirm no naked positions after sweep
**Pass criterion**: zero [BE-DIAG] during QX sweep; all 4 accounts covered; exactly 3 PTT-QX-T*; no unprotected position.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 1 | DW-PTT-BE-FIX-01 |
| New deferred (B127 pipeline) | 2 | B127-DEFER-01 (SIM gate), B127-DEFER-02 (warning throttle) |
| Carry-forward (unchanged from B107) | 14 | DW-B42-01/02/03, DW-PTT-BE-FIX-02/03, DW-B89-DEFERRED-01/02/03/04/05/06, DW-B107, B107-DEFER-01, B107-DEFER-02 |

**Total open items**: 16 (2 new B127 + 14 carry-forward)
