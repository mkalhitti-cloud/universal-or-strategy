# DW-B91 -- Deferred Backlog
Block: DW-B91 (Entry Dedup Survivor Guard + Flat-Follower Re-entry Guard)
Date: 2026-08-25
Status: PIPELINE_COMPLETE (coding phases)

---

## Carry-Forward Items (from DW-B89 -- all remain open)

### DW-B42-01 -- T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

### DW-B42-02 -- Live NT8 F5 verification required
**Priority**: High -- required before next live trading session
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All -> BE All must place targets at BE price (not bare stop)
- Direction 2: BE All -> Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)
**Action**: Press sequence in SIM account before go-live.

### DW-B42-03 -- IsPttQxTarget range extension for future target slots
**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design
(PTT-QX-T1 in OCO-A, PTT-QX-T2 in OCO-B, T3 as potential 3rd slot). If a future block adds
PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

### DW-PTT-BE-FIX-01 -- DW-B85 Option A: Lazy re-resolve for null followers
**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, the Option B
warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the
account later appears in Account.All. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.
**Fix**: In AllAccounts(), replace null-skip with a lazy re-resolve: if followers[i] is null
but Account.All now contains the name, update followers[i] and yield return it.

### DW-PTT-BE-FIX-02 -- SIM gate: Path B 3-cycle runtime verification
**Priority**: High -- required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles, checking stops=N > 0 on each follower) requires a live NT8
session with leader + follower accounts and open positions.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).
**Action**: Run Path B test sequence (3 cycles) in SIM before go-live.

### DW-PTT-BE-FIX-03 -- Pre-existing 83 build errors in CopyEngineTests.cs
**Priority**: High -- blocks full test suite build
**Context**: There are 83 pre-existing errors in the test project baseline (CopyEngineTests.cs
stub infrastructure) plus 1 Globals ambiguity (CS0433 at CopyEngine.cs:L3883), totalling 84
pre-existing errors. These were confirmed pre-existing by engineer stash roundtrip. Unrelated
to DW-B91 changes.
**Deferred to**: Dedicated test infrastructure remediation block.
**Action**: Separate remediation track. Investigate CopyEngineTests.cs stub failures + Globals
ambiguity at L3883.

### DW-B89-DEFERRED-01 -- Ctrl+F5 NT8 compilation gate
**Priority**: P0 -- blocks SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader produces "Compilation succeeded" 0 errors
after deploy-sync copies CopyEngine.cs, PttBreakEvenSwap.cs, PttBreakEven.cs to NT8 AddOn dir.
**Action**: run `powershell -File scripts\sync-ptt-to-nt8.ps1` then Ctrl+F5 in NT8.
Pass: "Compilation succeeded". Fail: report error, do not run SIM gate.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

### DW-B89-DEFERRED-02 -- SIM gate PATH A nominal (buf=1t or more, short or long)
**Priority**: High
**Context**: Entry -> BE-ALL -> verify Output tab has NO [BE-ERR] lines, stops=N for all accounts.
3 cycles. PASS criterion: zero error popups, zero naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

### DW-B89-DEFERRED-03 -- SIM gate PATH A buf=0 edge case (short position)
**Priority**: High
**Context**: Entry short -> BE-ALL buf=0t immediately.
Verify Output tab shows [BE-ERR] ...stop below market (if price moved) OR stops placed successfully
(if price still at entry). NO naked positions. NO error popups. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

### DW-B89-DEFERRED-04 -- SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)
**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger.
Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.
**Merges**: DW-PTT-BE-FIX-02 (Path B 3-cycle verification).

### DW-B89-DEFERRED-05 -- SIM gate DW-B87 timing race cycle
**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

### DW-B89-DEFERRED-06 -- Spec update: close DW-B89, DW-B88, DW-B87 in spec HTML
**Priority**: Medium
**Context**: Spec file specs/002-trade-copier-spec.html sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all SIM gate paths pass.
**Action**: Director updates spec after full SIM gate PASS.
**Deferred to**: After all SIM paths green.

---

## DW-B91 New Deferred Items

### DW-B91-DEFERRED-01 -- NT8 F5 compilation gate for DW-B91 changes
**Priority**: P0 -- blocks SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader compiles CopyEngine.cs cleanly
after deploy-sync copies the updated file to the NT8 AddOn directory.
DW-B91-A added `_entryDispatchedOrders` field and `IsEntryDispatched` method.
DW-B91-B added `FlattenFollower` static helper and modified `TryDispatchLeaderFlat`.
**Action**: run `powershell -File scripts\sync-ptt-to-nt8.ps1` then Ctrl+F5 in NT8.
Pass: "Compilation succeeded". Fail: report error.
**Deferred to**: Director (immediate).

### DW-B91-DEFERRED-02 -- SIM gate: DW-B91-A partial fill scenario
**Priority**: High
**Context**: Entry with 7-lot order that fills in 2 partials (3+4) -> verify followers receive
exactly 1 ATM bracket set (not 2). QX-ALL should see correct target count (3, not 4+).
Verifies that `IsEntryDispatched` blocks the second dispatch attempt that would arise after
`EvictDedup` evicts on the Filled terminal state if a second Submitted event arrives.
**Deferred to**: Director after DW-B91-DEFERRED-01 green.

### DW-B91-DEFERRED-03 -- SIM gate: DW-B91-B flat-follower scenario
**Priority**: High
**Context**: Entry -> QX target fills on followers -> leader manually closed via Chart Trader ->
verify followers do NOT receive spurious PTT-Flatten dispatch.
Verifies `FlattenFollower` correctly skips already-flat followers via `hasOpenPosition` guard.
**Deferred to**: Director after DW-B91-DEFERRED-01 green.

### DW-B91-DEFERRED-04 -- hasOpenPosition race window under fast fills
**Priority**: Low
**Context**: The per-follower `hasOpenPosition` check in `FlattenFollower` is best-effort.
If NT8 position state has not propagated by the time the leader-flat event fires, a follower
that has closed its position may still show open -> redundant flatten dispatched (harmless) or
vice versa. The `_beInFlight` flag approach (spec #section-b91b) is the fuller fallback.
**Deferred to**: Next block if SIM shows the race scenario.
