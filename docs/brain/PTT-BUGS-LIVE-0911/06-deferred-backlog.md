# PTT-COPIER Deferred Work Backlog

This file is appended by ptt-plan-reviewer at the end of each block's Phase 5 final review.
Items are added when work is identified but out-of-scope for the current block. Items are
closed when addressed in a later block.

---

## Block B24 — Lane B (DW-B23-BE-ALLACCOUNTS-01 fix)

**Block Summary**: New `BreakEven(Account, Instrument, int)` overload in CopyEngine.cs. All 6 call
sites updated (1 in CopyEngine.cs + 5 in TradeCopierPanel.cs). Two new [Fact] tests. Test count
126 → 128. Defect DW-B23-BE-ALLACCOUNTS-01 structurally closed.

**Final Review**: FINAL_PASS (2026-07-07)

### Deferred Items from B24

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. Add to `docs/standards/NT8_COMPILER_RULES.md` as NT8-043 (P1). B24 code has zero null-conditional unsubscriptions (SCAN-07 = 0); rule is WATCH-only per plan Section 9. Needs explicit confirmation in a future block before promoting to P1 CONFIRMED. | P2 | B25 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification**: Press B on a solo account (no copy rule registered) in a live NinjaTrader session. Confirm stop moves. Unit tests cover null-leader path and no-throw paths but cannot substitute for in-process NT8 runtime validation. Must be done before releasing B24 changes to production users. | P1 | B25 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard test**: The `if (acc == leader) continue` guard (CopyEngine.cs:1195) prevents double-firing when the leader account appears in the `AllAccounts` fan-out. A formal [Fact] test for this scenario is absent. Add a test that wires a rule where master == leader account and verifies `MoveStopToBreakEven` is called exactly once for that account. | P2 | B25 | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B24 · 2026-07-07*

---

## Block B25 — Lane B (DW-B25-02: Per-Account BE State Isolation)

**Block Summary**: Replaced singleton `volatile int _pendingBeState` and `volatile int _trailBeState`
with `readonly ConcurrentDictionary<string, int> _pendingBeStates` and `_trailBeStates` in
CopyEngine.cs. Updated `DisarmPendingBe` and `DisarmTrailBe` signatures to accept `Account leader`
parameter. Extracted `IsPendingBeArmed(Account)` and `IsTrailBeArmed(Account)` private helper
methods. Updated all 5 TradeCopierPanel.cs call sites to pass `_leaderAccount`. Updated 3 existing
CopyEngineTests.cs tests. Test count preserved at 128. All 7 scans (SCAN-01 through SCAN-07) zero
across `src/PropTraderTools/`. DW-B25-02 CLOSED this block.

**Plan Review Cycles**: Cycle 1 FAIL (V1/V2/V3 CYC violations, V4 doc gap) → Cycle 2 PASS (all 4 resolved).

**Final Review**: FINAL_PASS (2026-07-07)

### B24 Items Carried Forward (Unchanged Status)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. NT8-043 added as P0 compiler-error rule in B23; DW-B24-01 tracks runtime crash *confirmation* specifically. B25 SCAN-07 = 0 (no new ?.Event -= usage). Rule watch continues. | P2 | B26 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification**: Press B on a solo account in a live NinjaTrader session. Confirm stop moves. B25 unit tests preserve 128 baseline; no new runtime coverage added. Must be done before production release. | P1 | B26 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard test**: Formal [Fact] for `if (acc == leader) continue` guard at CopyEngine.cs:~1195. Not addressed in B25 (out-of-scope for DW-B25-02 ticket). | P2 | B26 | OPEN |

### B25 Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-02 | **Per-account BE state isolation**: Replace singleton `volatile int _pendingBeState` / `_trailBeState` with per-account `ConcurrentDictionary<string,int>`. Update Disarm signatures and all 5 Panel call sites. | — | B25 (this block) | **CLOSED** |
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain refs (single-writer UI thread). In a multi-panel topology, two panels could race on the same singleton companion ref. Per-account isolation was scoped to state slots only in B25. Full companion-field isolation requires a larger refactor. | P3 | B26 or future | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B25 · 2026-07-07*

---

## Block B26 — Single Pipeline (DW-PR120-GREPTILE-B: _beBufferBox NullRef fix)

**Block Summary**: Removed never-assigned `private TextBox _beBufferBox` field from
`TradeCopierPanel.cs` (line 202). Replaced `Key.B` case body in `DispatchShortcut` (lines 3063-3067)
— deleted `int buf = 2;` scaffolding and null-crashing `int.TryParse(_beBufferBox.Text, out buf);`,
changed third argument to `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` using the
existing plain `int _beBuffer` field (line 243). Updated stale comment at line 3041. Added xUnit
`[Fact]` `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` (Option B reflection-based absence
assertion) in `CopyEngineTests.cs`. Test count 128 → 129. All 7 scans (SCAN-01 through SCAN-07)
zero across `src/PropTraderTools/`. Commit SHA: 4336c300.

**Plan Review Cycles**: Cycle 1 PASS (no violations found).

**Final Review**: FINAL_PASS (2026-09-07)

### B24 Items Carried Forward (Unchanged Status)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. SCAN-07 = 0 across B24, B25, B26 change sets. Rule watch continues; no new evidence in B26. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E Ctrl+Shift+B runtime verification**: Press Ctrl+Shift+B on a solo account in a live NinjaTrader 8 session. Confirm `BreakEven` fires (stop moves) and no NullReferenceException. B26 is the fix that makes this test meaningful. Priority upgraded from P1 to P1 (escalated) — must execute immediately after B26 merges. | P1 | B27 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard [Fact]**: Formal xUnit test for `if (acc == leader) continue` guard at CopyEngine.cs:~1195. Not addressed in B26 (out-of-scope). | P2 | B27 | OPEN |

### B25 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain singleton refs. Multi-panel topology could race on these. Not in B26 scope. | P3 | B27 or future | OPEN |

### B26 Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B26-01 | **Reflection test upgrade (Option B → Option A)**: `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` uses reflection-based field-absence assertion because WPF instantiation of `TradeCopierPanel` requires NT8 runtime unavailable in xUnit. If a test harness with `InternalsVisibleTo` + WPF test host becomes available, replace with Option A: instantiate panel, set `_beBuffer = 3` via reflection, invoke `DispatchShortcut(Key.B)`, assert `ICopyEngine.BreakEven` called with argument 3. The current compile-time proxy prevents regression but does not exercise the dispatch path. | P2 | B28 or future | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B26 · 2026-09-07*

---

## Block PTT-BUGS-LIVE-0911 — Lane A (BUG-1: PTT-Flatten Reverse Entry Guard)

**Block Summary**: Added `FlatGuardLoop<TAccount>` private static helper to `CopyEngine.cs`
(line 5162, CYC=3). Replaced bare `foreach` fan-out loops in both `Flatten(Instrument)` (line 5153)
and `Flatten(Account, Instrument)` (line 5220) with `FlatGuardLoop(...)` calls. Guard checks
`pos == null || pos.Quantity == 0` before any `FlattenOneAccount` invocation — eliminating
accidental reverse-entry PTT-Flatten dispatches when follower is already flat (Evidence A: 5:55 AM
-05 reverse long; Evidence B: 7:14 AM -07/-08 reverse short). One test added:
`Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` annotated `[Fact(Skip="NT8-runtime:...")]`
due to NT8 assembly dependency in test body. Total `[Fact` count: 524→525. All 7 scans zero or
accepted-deviation-documented. dotnet test: Failed=0, Passed=159, Skipped=356, Total=515.

**Plan Review Cycles**: Phase 2 REVIEW_PASS.

**Final Review**: FINAL_PASS (2026-09-11)

### B24 Items Carried Forward (Unchanged Status)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. SCAN-05 = 0 in this block (2 pre-existing comment matches only). Rule watch continues. | P2 | B27 or future | OPEN |
| DW-B24-02 | **Manual E2E Ctrl+Shift+B runtime verification**: Press Ctrl+Shift+B on a solo account in live NT8. Confirm `BreakEven` fires and no NullReferenceException. Still pending. | P1 | B27 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard [Fact]**: Formal xUnit test for `if (acc == leader) continue` guard at CopyEngine.cs:~1195. Not in scope for BUG-0911-A. | P2 | B27 | OPEN |

### B25 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain singleton refs. Multi-panel topology race possible. Not in scope. | P3 | B27 or future | OPEN |

### B26 Items Carried Forward

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B26-01 | **Reflection test upgrade (Option B → Option A)**: `TradeCopierPanel_BeBufferBox_FieldDoesNotExist` reflection-based absence assertion. Upgrade when NT8 WPF test host available. | P2 | B28 or future | OPEN |

### PTT-BUGS-LIVE-0911 Lane A Items (New This Block)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-BUG-0911-A-01 | **Mid-close race window**: `FlatGuardLoop` catches `qty == 0` definitively but does NOT catch the race where a close order is in-flight (`qty > 0`) when a second dispatch fires before the first close fills. Both dispatches see `qty > 0` and proceed; the second executes as a reverse entry after the first close fills. Complete elimination requires an atomic interlocked per-account slot (similar to `_lastHasPos` ConcurrentDictionary pattern). Scoped out — surgical guard eliminates the majority of accidental reverse entries confirmed in live trading. | P2 | B27 or future | OPEN |
| DW-BUG-0911-A-02 | **FlatGuardLoop test runtime Skip**: `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` is annotated `[Fact(Skip="NT8-runtime: CopyEngine.cctor requires NT8 host")]` because the test body references `NinjaTrader.Cbi.Position` in a typed delegate (`Func<object, NinjaTrader.Cbi.Position?> findPosition`); JIT resolution of the NT8 assembly fails in the xUnit host (no NT8 runtime). Bare `[Fact]` caused empirical `TypeInitializationException` (BUILD_FAIL). Resolution: investigate whether a testable wrapper or partial-class extraction can isolate `FlatGuardLoop` from the NT8 assembly reference in the test, or provide a test-host shim for `NinjaTrader.Cbi.Position`. | P2 | B27 or future | OPEN |

**Note:** PTT-BUGS-LIVE-0911 Lane B (BUG-2: fo=NULL bracket failure after QX cycle) deferred items
will be appended in a separate Phase 5 pass after Lane B completes.

---

*ptt-plan-reviewer · PTT-BUGS-LIVE-0911 Lane A · 2026-09-11*

---

## Block PTT-BUGS-LIVE-0911 — Lane B (BUG-2: Post-Exit Clean Slate Gate)

**Block ID:** PTT-BUGS-LIVE-0911-LANE-B
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-11
**Final verdict:** FINAL_PASS

### Block Summary

BUG-2: Post-Exit Clean Slate Gate. Implemented a `ConcurrentDictionary<string, bool> _followerSlateReady` gate in `CopyEngine.cs` that:

1. Marks follower account slate as **not-ready** (gate closed) whenever any exit dispatch wipes ATM bracket orders — covers all 3 instrumentation points: `CancelQxBrackets(2-param)`, `CancelQxBrackets(3-param)`, and `FlattenOneAccount`.
2. Marks follower account slate as **ready** (gate open) when `account.PositionUpdate` fires and the account is confirmed flat with no active orders — via the new `OnFollowerPositionUpdate` handler subscribed/unsubscribed in `Subscribe()`/`Unsubscribe()`.
3. Gates `FindFollowerBracketOrder(Account,...)` at first statement: if slate is explicitly not-ready, return null immediately — blocking bracket price sync dispatch until the slate is confirmed clean.
4. First trade of session is always unaffected: absent key in `_followerSlateReady` = gate open (TryGetValue returns false on absent key).

**Root cause addressed:** Friday 2026-09-11, PA-APEX-422136-07 entered Trade 3 (MES Short x7 @ 7674.75, 5:58 AM) with zero stop/target protection. Six consecutive `[TP4-SFB] fo=NULL` log lines confirmed (output.md lines 894–906) that the QX-2T exit on Trade 2 left -07's order slate stale (standard ATM bracket names in Cancelled state), causing `FindFollowerBracketOrder` to return null on every bracket sync attempt for Trade 3.

**Files changed:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs` only.
**Scans:** All 7 scans zero (L2 engineer + L3 verifier, no discrepancies).
**Tests:** 8 `[Fact]` tests added (5 pass, 3 skipped requiring NT8 runtime). All 7 required test names present.
**Build:** `dotnet build` succeeded, 0 errors. 164 tests pass, 0 failures, 0 regressions.

### B24–B26 and Lane A Items Carried Forward

Prior OPEN items (DW-B24-01, DW-B24-02, DW-B24-03, DW-B25-01, DW-B26-01, DW-BUG-0911-A-01, DW-BUG-0911-A-02) status unchanged. Not addressed in this Lane B ticket (out of scope).

### PTT-BUGS-LIVE-0911 Lane B Items (New This Block)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-PTT0911B-01 | **SIM end-to-end verification** (SCAN-07 step 6): manual NinjaTrader SIM session required; cannot run in headless CI. Verify fo=NOT-NULL on 2nd trade brackets after QX-2T exit with 2+ follower accounts. | P1 | pre-live | OPEN |
| DW-PTT0911B-02 | **Gate reset on AddOn reload**: `_followerSlateReady` resets on CopyEngine re-instantiation; absent-key = gate-open is the safe default. No action required; informational. | P2 | future | OPEN |
| DW-PTT0911B-03 | **Accounts added after Subscribe()**: accounts connected after AddOn load do not receive PositionUpdate subscription. Gate stays absent (open = safe default). Hardening: re-subscribe on `Account.AccountConnected` in a future pass. | P2 | future | OPEN |
| DW-PTT0911B-04 | **`HasAnyActiveOrders` snapshot timing**: ToList() snapshot in high-churn scenario may include terminal orders not yet cleared; gate stays closed one extra PositionUpdate tick — conservative false-negative, not a false-positive. No bracket protection risk. | P2 | future | OPEN |

---

*ptt-plan-reviewer · PTT-BUGS-LIVE-0911 Lane B · 2026-09-11*

## Block PTT-BUGS-LIVE-0911 — Lane C (Lane D Wiring Stub)

**Block ID:** PTT-BUGS-LIVE-0911-LANE-C
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-11
**Final verdict:** FINAL_PASS

### Block Summary

Lane C: Push Entry Dispatch Investigation + Lane D Wiring Stub.

Investigated whether a Push path ahead of `OnOrderUpdate` was architecturally necessary or
possible. Conclusion: `OnOrderUpdate` at line 1642 is the sole, deterministic, and correct
single entry point for copy dispatch — no Push path is possible in the NT8 AddOnBase model
(valid `Order` object only exists after NT8 processes the submission). The gate chain
(Gates 0.5, 3, 4, 5 + dedup commit) is complete and correct.

Added unconditional wiring stub at the identified Lane D wiring point in `DispatchCopy`:
- Call site at `CopyEngine.cs` line 2632: `TryLaneDHook(order, rule, baseSignal, baseQty);`
  (between `ResolveBaseQty` at line 2629 and B119 snapshot at line 2634).
- Stub definition at `CopyEngine.cs` lines 2671–2673: `private void TryLaneDHook(...)` with
  empty body, CYC=1, JS-001/021 annotated, ASCII-only.
- Reflection test `TryLaneDHook_MethodExists_IsPrivateVoidWith4Params` in new
  `LaneCWiringContractTests` class (`CopyEngineTests.cs` lines 8549–8565), no Skip attribute.

**Files changed:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs` only.
**Plan review cycles:** Cycle 1 FAIL (4 violations) → Cycle 2 PASS (all resolved).
**Scans:** All 7 scans zero (L2 engineer + L3 verifier, no discrepancies).
**Tests:** 1 new `[Fact]` (no Skip). Total: 165 passed, 359 skipped, 0 failed. No regressions.
**Build:** `dotnet build` succeeded, 0 errors.
**Behavior change:** Zero — `TryLaneDHook` body is empty. No dispatch path altered.

### B24–B26 and Lane A/B Items Carried Forward

Prior OPEN items (DW-B24-01, DW-B24-02, DW-B24-03, DW-B25-01, DW-B26-01, DW-BUG-0911-A-01,
DW-BUG-0911-A-02, DW-PTT0911B-01, DW-PTT0911B-02, DW-PTT0911B-03, DW-PTT0911B-04) status
unchanged. Not addressed in Lane C (out of scope).

### PTT-BUGS-LIVE-0911 Lane C Items (New This Block)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-PTT0911C-01 | **`TryLaneDHook` implementation (Lane D scope)**: Empty stub body at `CopyEngine.cs` line 2671. Lane D must implement. Hook fires once per confirmed leader entry signal after all 5 `DispatchCopy` gates, after `ResolveBaseQty`, before B119 snapshot and follower loop. Usable for diagnostic logging, latency measurement, pre-flight validation, or delayed-dispatch queue (must use `ConcurrentQueue`, not `lock`). | P1 | Lane D | OPEN |
| DW-PTT0911C-02 | **Lane D CYC constraint**: `DispatchCopy` is at CYC=8 (limit). Any conditional logic Lane D needs MUST live inside `TryLaneDHook` body — NOT at the call site in `DispatchCopy`. An `if` at the call site would raise `DispatchCopy` CYC to 9 (JS-021 FAIL). `TryLaneDHook` own CYC budget: ≤8 (currently CYC=1). | P0 | Lane D | OPEN |
| DW-PTT0911C-03 | **Lane D thread context**: `TryLaneDHook` runs on NT8 account background thread. UI updates inside hook MUST use `Dispatcher.InvokeAsync` (JS-023). All state writes must be thread-safe: `ConcurrentDictionary`, `ConcurrentQueue`, or `Interlocked` only (JS-021). | P0 | Lane D | OPEN |
| DW-PTT0911C-04 | **`StartAtmStrategy` overload for Lane D** (if ATM logic added): STATIC form only — `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(AtmStrategy, Order)` (object, line 5149) or `StartAtmStrategy(string, Order)` (string, line 5151). `AtmStrategyCreate()` BANNED. Order name must be `"Entry"` for ATM bracket arming. Verify exact overload availability against NT8 AddOnBase API reference before use. | P1 | Lane D (if ATM) | OPEN |
| DW-PTT0911C-05 | **ATM template null guard for Lane D** (if ATM logic added): `namedMode.AtmObject` can be null (string-only `Named` ctor path). Apply same null check as `SendCopyWithAtm` lines 5145–5151: prefer object overload when `AtmObject != null`, else fall back to string overload. | P1 | Lane D (if ATM) | OPEN |

---

*ptt-plan-reviewer · PTT-BUGS-LIVE-0911 Lane C · 2026-09-11*

---

## Block PTT-BUGS-LIVE-0911 — Lane D (ATM Bracket Protection at Entry)

**Block ID:** PTT-BUGS-LIVE-0911-LANE-D
**Status:** COMPLETE
**Phase 5 Reviewed:** 2026-09-11
**Final verdict:** FINAL_PASS

### Block Summary

Lane D implemented ATM bracket protection at follower entry for Clone mode. `TryLaneDHook` was
promoted from an empty stub to a fully-operative dispatcher: it fans out per-follower ATM orders
via the new `TryLaneDDispatchFollower` helper (delegating to existing `SendCopyWithAtm`) and
commits a Gate-5 dedup entry to prevent duplicate dispatch on re-fired `OnOrderUpdate` events. A
new `ConcurrentDictionary<string, byte> _laneDDispatched` field provides a lock-free per-order,
per-follower skip gate in `DispatchToFollower` that blocks the loop's Named branch from
re-dispatching followers already handled by the hook. All non-Clone and non-Limit paths remain
strictly unaffected. Six new `[Fact]` tests cover structural and behavioral contracts; all 7 scans
returned zero across `src/PropTraderTools/`.

### DW-PTT0911C Items — CLOSED This Block

| ID | Item | Status |
|----|------|--------|
| DW-PTT0911C-01 | TryLaneDHook implementation | **CLOSED** |
| DW-PTT0911C-02 | DispatchCopy CYC constraint | **CLOSED** |
| DW-PTT0911C-03 | Thread context safety | **CLOSED** |
| DW-PTT0911C-04 | StartAtmStrategy call form | **CLOSED** |
| DW-PTT0911C-05 | ATM template null guard | **CLOSED** |

### Prior Open Items Carried Forward (Unchanged Status)

All prior OPEN items from B24, B25, B26, Lane A, Lane B, and Lane C are unchanged:
DW-B24-01, DW-B24-02, DW-B24-03, DW-B25-01, DW-B26-01,
DW-BUG-0911-A-01, DW-BUG-0911-A-02,
DW-PTT0911B-01, DW-PTT0911B-02, DW-PTT0911B-03, DW-PTT0911B-04.
Not addressed in Lane D (out of scope).

### PTT-BUGS-LIVE-0911 Lane D Items (New This Block)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-LD-01 | **`_laneDDispatched` session-unbounded growth**: entries persist for session lifetime; no cleanup on session end or position close. Growth bounded (~2.4 KB/session for 4 followers). Cleanup can be added in a future pass if high-volume sessions reveal concern. | P2 | Lane E or future | OPEN |
| DW-LD-02 | **Market order ATM guard decision documented**: branch (2) returns no-op for Market-type leader orders. Correct by design — `SendCopyWithAtm` hardcodes Limit; Limit@0 is an invalid NT8 order. Market signals fall through to existing `SendCopy` path. Future Market-with-ATM capability would require a separate overload or order type routing. | P2 | future | OPEN |
| DW-LD-03 | **Lane E: re-arm ATM after QX/BE**: after QX/BE exit the follower ATM bracket is cancelled; a fresh ATM bracket is not automatically re-armed on re-entry. Wiring point (`TryLaneEHook` in `OnFollowerPositionUpdate` at `_followerSlateReady` gate-open transition) documented in plan Section 11. Implementation out of scope for Lane D. | P1 | Lane E | OPEN |

---

*ptt-plan-reviewer · PTT-BUGS-LIVE-0911 Lane D · 2026-09-11*

---
