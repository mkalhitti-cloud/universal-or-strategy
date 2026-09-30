# B36-LaneB Deferred Backlog
# Block: B36 | Lane B | DW-B35-TARGETS-01 | be-targets-oco
# Date: 2026-07-27
# Status: PIPELINE_COMPLETE — DW-B35-TARGETS-01 CLOSED; 3 new sim-gate items deferred

---

## Block B36-LaneB — Closed Items

Single ticket T1 closed. [Fact] count = 184 (180 → +4). Build tag: `PTT-COPIER B36 | be-targets-oco | 2026-07-27`.

| Item | Description | Status |
|------|-------------|--------|
| DW-B35-TARGETS-01 | BE button places bare stop with no OCO group and no take-profit targets | **CLOSED** — all 3 root cause parts resolved |
| C1 | `SnapshotTargetsLocal` added to PttBreakEven.cs (lines 244–264) | CLOSED — verified by VERIFY_PASS |
| C2 | `IsAtmTargetName` added to PttBreakEven.cs (lines 230–235) | CLOSED — verified by VERIFY_PASS |
| C3 | `SubmitBeTargetsLocal` added to PttBreakEven.cs (lines 288–339) | CLOSED — verified by VERIFY_PASS |
| C4 | Execute() 5-step A→E ordering (lines 95–102) | CLOSED — verified by VERIFY_PASS |
| C5 | `SubmitBeStopLocal` ocoId param + arg8=ocoId (lines 162–163, 183) | CLOSED — verified by VERIFY_PASS |
| BuildBeOcoId | Helper extracted per REVIEW_PASS mandate (lines 270–275) | CLOSED — verified by VERIFY_PASS |
| T1–T4 | 4 new [Fact] tests in CopyEngineTests.cs (lines 3346–3412) | CLOSED — [Fact] count 184 confirmed |
| Hard-link | OK=11, DESYNC=0, SKIPPED=1 | CLOSED — verified by VERIFY_PASS |
| Build tag | `PTT-COPIER B36 \| be-targets-oco \| 2026-07-27` in CopyEngine.cs:41 | CLOSED — verified by VERIFY_PASS |

---

## DW-B35-TARGETS-01 — Full Closure Summary

**Status**: CLOSED by B36-LaneB

**Root cause parts resolved**:
1. `SubmitBeStopLocal` arg8 was `string.Empty` → now passes `ocoId` from `BuildBeOcoId`
2. No snapshot of Working ATM targets existed before cancel → `SnapshotTargetsLocal` added,
   called BEFORE `CancelStaleBracketsLocal` in Execute() foreach body
3. No resubmission of ATM targets after BE stop → `SubmitBeTargetsLocal` added,
   called AFTER `SubmitBeStopLocal` with same `ocoId`, using snapshotted target prices and qtys

**7-Scan result (new code)**:
- SCAN-01 lock(): 0
- SCAN-02 async void: 0
- SCAN-03 LINQ: 0 code matches (2 doc comment mentions only)
- SCAN-04 get;init;: 0
- SCAN-05 DateTime.Now: 0 code matches (1 doc comment mention only)
- SCAN-06 dotnet build: 0 new errors (2 pre-existing AtrSizingEngine.cs errors, B34 baseline)
- SCAN-07 [Fact] count: 184 (independently confirmed by verifier Layer 3)

---

## New Deferred Items Introduced by B36-LaneB

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B36-SIM-01 | Sim test: press BE button, verify Output shows PTT-BE-Stop and PTT-BE-Target-N orders, verify both appear in Active Orders grid with matching OCO group ID | P1 | B37 / sim session | OPEN |
| DW-B36-SIM-02 | Sim test: confirm OCO fill auto-cancel — if PTT-BE-Stop fills, verify PTT-BE-Target-N orders are automatically cancelled by NT8 OCO engine; if PTT-BE-Target-N fills, verify PTT-BE-Stop is cancelled | P1 | Sim session | OPEN |
| DW-B36-SIM-03 | Sim: confirm arg8 OCO group ID effectiveness in BE context (first block to actively use arg8 in PttBreakEven; extends and supersedes U1 from B34 in BE scope) | MEDIUM | Sim session | OPEN |

**Owner**: Director / manual sim test session
**Blocking**: No (code is live via hard-link; tests are observational validation)

**Sim validation steps for B36-LaneB** (add to running sim gate list):
1. F5 compile in NinjaTrader with tag `PTT-COPIER B36 | be-targets-oco | 2026-07-27`
2. Open a Sim long or short position with an active ATM strategy (at least 1 Target order Working)
3. Press BE button
4. Verify Output tab shows:
   - `[BE] Snapshot target: Target1 ...` (for each working ATM target)
   - `[BE] SubmitBeStopLocal Sell/BuyToCover N @ X.XX on <account>`
   - `[BE] SubmitBeTargetsLocal Target-1 ... @ X.XX`
   - `[BE] SubmitBeTargetsLocal: N targets for <account>`
5. Verify Active Orders grid: PTT-BE-Stop (StopMarket, Working) present
6. Verify Active Orders grid: PTT-BE-Target-1..N (Limit, Working/Accepted) present
7. Verify all PTT-BE-* orders share the same OCO group string (inspect via NT8 Order Properties)
8. Verify original ATM Target1..N orders are gone (cancelled by CancelStaleBracketsLocal)

---

## Carry-Forward: Open Items from B35-LaneA and B35-LaneB

None of the items below were affected by B36-LaneB. All remain open as-of this block.

| ID | Description | Source | Priority | Notes |
|----|-------------|--------|----------|-------|
| DW-B35-LA-SIM-01 | Sim: validate BE stop-above-market guard in live NT8 sim (8-step gate from B35-LaneA backlog) | B35-LaneA | P1 | Observational; code is live via hard-link |
| DW-B32-TRIM-MARKET-01 | buffer=0 forces market fallback — limit path degrades to market order silently | B32 | P1 | Fix: remove buffer=0 market fallback from ComputeLimitPx |
| DW-B32-TRIM-ANCHOR-01 | ComputeLimitPx wrong price anchor (ask/bid peg) | B32 | P1 | Architectural fix required |
| R-B32-03 / DW-B32-TRIM-CLOSE-01 | Trim ATM OCO bracket corruption on market exit path | B32 | P1 | Architect review needed; IsAtmBracketActive pattern is proposed fix |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection: TrimOneAccountLimit / FlattenOneAccountLimit lack IsAtmBracketActive guard | B32 | P2 | Director approval needed before proceeding |
| U1 | NT8 `Account.CreateOrder` arg8 OCO group ID effectiveness on sim (general) | B34 | LOW | CancelStaleBrackets(cancelPttBe:true) cleans up on flat regardless; partially superseded by DW-B36-SIM-03 in BE context |
| U3 | Confirm Limit order arg6=limitPrice, arg7=0 correct in live NT8 | B34 | MEDIUM | Verify via sim test output (wrong order price visible if swapped) |
| DW-B32-DEFERRED-02 | ATM Target nudge — acc.Change() silently rejected by NT8 ATM engine | B32 | — | **REJECTED** — architectural constraint; NT8 ATM engine owns Stop/Target slot prices |

---

## Sim Test Gate Consolidated (B34 through B36-LaneB)

The following sim validation steps are pending for the full B34–B36 feature set.
None are blocking (code is live in NT8 via hard-link). All are observational.

**B34 / B35 sim steps** (from B35-LaneB backlog — still pending):
1. F5 compile in NinjaTrader (B36 final tag)
2. Open a Sim position on an ATM strategy
3. Press BE button — verify Output shows bracket-replace messages
4. Verify only PTT-BE-Stop and PTT-BE-Target-N appear in Active Orders grid
5. Verify original ATM Stop1/Target1..N are gone
6. Press Trim — verify CancelStaleBrackets fires before PTT-Trim CreateOrder
7. Press Flatten — verify CancelStaleBrackets fires before PTT-Flatten CreateOrder
8. Let position go flat — verify no orphan bracket orders remain

**B35-LaneA sim steps** (from B35-LaneA backlog — still pending):
1. Open a Sim long position
2. Wait for market to move above entry + 1 tick (bePrice < Ask)
3. Press BE button
4. Verify Output tab shows: `[BE] WARNING: <account> BE stop @ X.XX rejected -- stop above ask market Y.YY -- position UNPROTECTED`
5. Verify panel status bar shows short warning text: `<account>: BE stop rejected (above ask Y.YY)`
6. Verify no brackets were cancelled or submitted for that account
7. Confirm other accounts in the same session were still processed normally

**B36-LaneB sim steps** (new — see DW-B36-SIM-01/02/03 above):
1–8 as documented in the new deferred items section above.

---

## B37 Candidates

| ID | Description | Priority |
|----|-------------|----------|
| DW-B36-SIM-01 | Sim test: BE OCO bracket validation (BE button → PTT-BE-Stop + PTT-BE-Target-N) | P1 |
| DW-B36-SIM-02 | Sim test: OCO fill auto-cancel behavior (stop fills → targets cancelled, or vice versa) | P1 |
| DW-B35-NEXT-02 / DW-B32-TRIM-MARKET-01 | Fix: remove buffer=0 market fallback from ComputeLimitPx path | P1 |
| DW-B35-NEXT-03 / DW-B32-TRIM-ANCHOR-01 | Fix: ComputeLimitPx anchor correction (ask/bid peg) | P1 |
| DW-B35-NEXT-04 / R-B32-03 / DW-B32-TRIM-CLOSE-01 | ATM OCO bracket corruption on market exit — architect-led fix | P1 |
| DW-B35-NEXT-05 / DW-B32-DEFERRED-03 | Limit path ATM bracket detection (TrimOneAccountLimit / FlattenOneAccountLimit) — Director approval needed | P2 |
