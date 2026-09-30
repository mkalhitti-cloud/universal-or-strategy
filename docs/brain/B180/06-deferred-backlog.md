# PTT Deferred Backlog
# All open deferred items across all completed blocks.
# Format: append newest block at bottom. Do NOT overwrite prior entries.
# Source of truth for ptt-plan-reviewer Phase 5 Section K carry-forward.

---

## B180 -- DW-B180-BE-SWAP-ALL

**Block completed**: 2026-10-09
**Final reviewer**: ptt-plan-reviewer (Phase 5)
**Verdict**: FINAL_PASS

### B180 New Deferred Items

| ID | Description | Type | Priority | Status |
|----|-------------|------|----------|--------|
| DW-B180-BE-T01 | T-B180-T1-01 (PttBeSwapOne_FlatAccount_SkipsExecution): `[Fact(Skip)]` -- NT8 Account/Instrument/Position seam not available in unit test harness. Behavioral assertion `Assert.Contains(": BE flat skip", capturedStatus)` documented as commented spec in test body. Requires NT8 stub infrastructure or integration test context. | INTENTIONAL-DEFERRAL | P2 | OPEN |
| DW-B180-BE-T04 | T-B180-T1-04 (PttBeSwapAll_FollowerAccounts_AreSkipped): `[Fact(Skip)]` -- NT8 Account.All and IsFollowerAccount seam not available in unit test harness. Assertion would verify PttBreakEvenSwap.Execute called exactly once (leader only). Requires NT8 stub infrastructure. | INTENTIONAL-DEFERRAL | P2 | OPEN |
| DW-B180-SIM | SIM GATE: Director verifies in live NinjaTrader session that (a) BE swap fires immediately on button click, (b) idempotent re-click does not double-submit, (c) flatten-after-BE produces no ghost re-arm. Required before B180 is production-ready. | INTENTIONAL-DEFERRAL | P1 | OPEN |
| DW-B180-COMMENT-01 | Stale comment documentation in CopyEngine.cs references `MoveStopToBreakEven` in doc blocks for TryFireFollowerBeRetry (~line 1369), QueueBeRetryFallback (~line 1749), and field comments (lines 278, 280, 329). Live code correctly calls `PttBeSwapOne.Execute`. Comments should be updated to reference PttBeSwapOne.Execute to avoid future confusion. | TECH-DEBT | P3 | OPEN |

### B180 Inherited Deferred Items (Carry-Forward from Prior Blocks)

The following items were open in the B180 architecture plan (Section 12) and remain open.

| ID | Source Block | Description | Type | Priority | Status |
|----|-------------|-------------|------|----------|--------|
| DW-B177-CYC | B177 | TradeCopierPanel.cs OnAtrBoxPreviewKeyDown CYC=13 (lines ~3318-3344). Extract `InjectAtrChar(char c)` helper to bring to CYC <= 8. | TECH-DEBT | P2 | OPEN |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader, confirm follower stop preserved. | INTENTIONAL-DEFERRAL | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops. Blocked by B167-DEFER-02. | INTENTIONAL-DEFERRAL | P0 | OPEN |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression. | INTENTIONAL-DEFERRAL | P0 | OPEN |
| B167-DEFER-02 | B167 | fo=NULL follower ATM brackets never placed after leader fill. Blocks DW-B169-K1 Sim102. | TECH-DEBT | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. | TECH-DEBT | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate. | INTENTIONAL-DEFERRAL | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate. | INTENTIONAL-DEFERRAL | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests. | FUTURE-ENHANCEMENT | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3. | TECH-DEBT | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing. | TECH-DEBT | P2 | OPEN |

### B180 Superseded Items (CLOSED)

| ID | Description | Closed By | Reason |
|----|-------------|-----------|--------|
| DW-B179-BE-ALL-SILENT-CANCEL | BE ALL button silent cancel after early flatten. | B180 | Root cause (armed state) eliminated by construction. No armed state can race. |
| DW-B180-BE-SWAP-ALL | Replace deferred BE/BE ALL with immediate bracket swap. | B180 | Implemented: PttBeSwapOne + PttBeSwapAll + dead code removal + tests. FINAL_PASS. |

---
