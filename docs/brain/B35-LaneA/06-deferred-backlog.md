# B35-LaneA Deferred Backlog
# Block: B35 | DW-B35-SILENT-REJECT | be-stop-market-guard
# Date: 2026-07-27
# Status: PIPELINE_COMPLETE — DW-B35-SILENT-REJECT closed; no new deferred items from B35-LaneA

---

## Block B35-LaneA (be-stop-market-guard) — Closed Items

All 2 tickets closed. [Fact] count = 180. Build tag: `PTT-COPIER B35 | be-stop-market-guard | 2026-07-27`.

| Item | Description | Status |
|------|-------------|--------|
| B35-01 | `WarnUser(string)` added to `IPttHostContext` in `Core/PttContracts.cs` (line 69) | CLOSED — verified by T1-verifier |
| B35-01 | Explicit implementation `void IPttHostContext.WarnUser(string message)` in `TradeCopierPanel.cs` (lines 138-141) | CLOSED — verified by T1-verifier |
| B35-01 | `T_B35_WarnUser_SetsStatusText` [Fact] in `CopyEngineTests.cs` (line 3297) | CLOSED — verified by T1-verifier |
| B35-02 | Price guard in `PttBreakEven.Execute()` after bePrice computation, before `CancelStaleBracketsLocal` (lines 75-92) | CLOSED — verified by T2-verifier |
| B35-02 | `T_B35_BE_StopAboveMarket_Skipped` [Fact] in `CopyEngineTests.cs` (line 3309) | CLOSED — verified by T2-verifier |
| B35-02 | `T_B35_BE_StopBelowMarket_Skipped` [Fact] in `CopyEngineTests.cs` (line 3329) | CLOSED — verified by T2-verifier |
| B35-02 | Build tag updated: `CopyEngine.cs:41 = "PTT-COPIER B35 \| be-stop-market-guard \| 2026-07-27"` | CLOSED — verified by T2-verifier |

DW-B35-SILENT-REJECT (P1) fully satisfied. [Fact] delta: 177 → 180. All 7 scans zero in changed lines (Layer 3 independent confirmation). Hard-link gate PASS (OK=11, DESYNC=0).

---

## Deferred Work from B35-LaneA (be-stop-market-guard)

None. B35-LaneA is a surgical 2-ticket implementation. No new architectural decisions, no new edge cases, no new patterns introduced beyond the explicit defect closure.

---

## Sim Test Gate (NOT yet run — carried from prior sessions)

The sim validation session covering BE stop-above-market warning and the full B34/B35 bracket flows has not been run. Code is live in NT8 via hard-link. This is an observational gate only.

**B35-LaneA specific validation steps** (add to the running sim gate list):
1. F5 compile in NinjaTrader with tag `PTT-COPIER B35 | be-stop-market-guard | 2026-07-27`
2. Open a Sim long position
3. Wait for market to move above entry + 1 tick (bePrice < Ask)
4. Press BE button
5. Verify Output tab shows: `[BE] WARNING: <account> BE stop @ X.XX rejected -- stop above ask market Y.YY -- position UNPROTECTED`
6. Verify panel status bar shows short warning text: `<account>: BE stop rejected (above ask Y.YY)`
7. Verify no brackets were cancelled or submitted for that account
8. Confirm other accounts in the same session were still processed normally (continue semantics)

**Owner**: Director / manual sim test session
**Blocking**: No (code is live; test is observational validation)

---

## Open Defects Carried Forward

The following items were open entering B35-LaneA and remain open. None are affected by B35-LaneA changes.

| ID | Description | Source | Priority | Notes |
|----|-------------|--------|----------|-------|
| DW-B35-LA-SIM-01 | Sim test gate: validate BE stop-above-market guard in live NT8 sim (8-step gate above) | B35-LaneA | P1 | Observational; code is live via hard-link |
| DW-B32-TRIM-MARKET-01 | buffer=0 forces market fallback — limit path degrades to market order silently | B32 | P1 | Deferred from B32; fix: remove buffer=0 market fallback from ComputeLimitPx |
| DW-B32-TRIM-ANCHOR-01 | ComputeLimitPx wrong price anchor (ask/bid peg) | B32 | P1 | Deferred from B32; architectural fix required |
| R-B32-03 / DW-B32-TRIM-CLOSE-01 | Trim ATM OCO bracket corruption on market exit path | B32 | P1 | Architect review needed; IsAtmBracketActive pattern is proposed fix |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection: TrimOneAccountLimit / FlattenOneAccountLimit lack IsAtmBracketActive guard | B32 P0 queue | P2 | Director approval needed before proceeding |
| U1 | NT8 `Account.CreateOrder` arg8 OCO group ID effectiveness on sim | B34 handoff Section 5 | LOW | CancelStaleBrackets(cancelPttBe:true) cleans up on flat regardless; requires sim test session |
| U3 | Confirm Limit order arg6=limitPrice, arg7=0 correct in live NT8 | B34 handoff Section 5 | MEDIUM | Verify via sim test output (wrong order price visible if swapped) |
| DW-B32-DEFERRED-02 | ATM Target nudge — acc.Change() silently rejected by NT8 ATM engine | B32 P0 queue | — | **REJECTED** — architectural constraint; NT8 ATM engine owns Stop/Target slot prices |

---

## B36 Candidates

| ID | Description | Priority |
|----|-------------|----------|
| DW-B35-NEXT-01 | Full sim test validation for B34 bracket-replace-BE + B35 bracket-cancel + B35-LaneA BE-guard (combined gate) | P1 |
| DW-B35-NEXT-02 | DW-B32-TRIM-MARKET-01 fix: remove buffer=0 market fallback from ComputeLimitPx path | P1 |
| DW-B35-NEXT-03 | DW-B32-TRIM-ANCHOR-01 fix: ComputeLimitPx anchor correction (ask/bid peg) | P1 |
| DW-B35-NEXT-04 | R-B32-03 / DW-B32-TRIM-CLOSE-01: ATM OCO bracket corruption on market exit — architect-led fix | P1 |
| DW-B35-NEXT-05 | DW-B32-DEFERRED-03: Limit path ATM bracket detection (TrimOneAccountLimit / FlattenOneAccountLimit) — Director approval needed | P2 |
