# B35-LaneB Deferred Backlog
# Block: B35 | DW-B32-queue | bracket-cancel + BE-fixes
# Date: 2026-07-23
# Status: PIPELINE_COMPLETE

---

## Block B35-LaneB — DW-B32-queue Status

All 5 defects from the B32 P0 queue are formally closed by this block.

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B32-01b | IsStopAlreadyAtBe short branch: `>=` → `<=` so BE triggers on short positions | P0 | **CLOSED** — CopyEngine.cs line 616; [Fact] at line 2882 |
| DW-B32-02 | MoveStopToBreakEven: add `OrderState.Accepted` to state filter | P0 | **CLOSED** — CopyEngine.cs lines 1513–1514; [Fact] at line 2913 |
| DW-B32-04b | BeState.Connected CS0117 compile fix: Connected removed from BeState enum | P0 | **CLOSED** — TradeCopierPanel.cs lines 269–273; [Fact] at line 2936 |
| DW-B32-07 | IsAtmSlotName guard in MoveStopToBreakEven (NT8-046: no acc.Change on ATM stops) | P0 | **CLOSED** — CopyEngine.cs lines 1525–1526; [Fact] at line 2955 |
| DW-B32-08 | BreakEven leader path: SubmitBeStop unconditional when position is open | P0 | **CLOSED** — CopyEngine.cs lines 1749–1755; [Fact] at line 2977 |

---

## Deferred Work from B35-LaneB

No new deferred items introduced by B35-LaneB. All 5 planned defects were
formalized, tested, and verified. The changes were pipeline-only (working-tree fixes
pre-existed; this block added comments, tests, and formal pipeline documentation).

---

## Open Defects Carried Forward

The following items were open entering B35-LaneB and remain open:

| ID | Description | Source | Priority | Notes |
|----|-------------|--------|----------|-------|
| U1 | NT8 `Account.CreateOrder` arg8 OCO group ID effectiveness on sim | B34 handoff Section 5 | LOW | CancelStaleBrackets(cancelPttBe:true) cleans up on flat regardless; requires sim test session |
| U3 | Confirm Limit order arg6=limitPrice, arg7=0 correct in live NT8 | B34 handoff Section 5 | MEDIUM | Verify via sim test output (wrong order price visible if swapped) |
| DW-B32-DEFERRED-02 | ATM Target nudge — acc.Change() silently rejected by NT8 ATM engine | B32 P0 queue | — | Rejected — architectural constraint; NT8 ATM engine owns Stop/Target slot prices |
| DW-B32-DEFERRED-03 | Limit path ATM bracket detection: TrimOneAccountLimit / FlattenOneAccountLimit lack IsAtmBracketActive guard | B32 P0 queue | P2 | Director review needed; guard pattern from DW-B32-TRIM-CLOSE-01 can be applied symmetrically |
| DW-B32-TRIM-ANCHOR-01 | ComputeLimitPx wrong price anchor (ask/bid peg) | B32 | P1 | Deferred from B32; architectural fix required |
| DW-B32-TRIM-MARKET-01 | buffer=0 forces market fallback — limit path degrades to market order silently | B32 | P1 | Deferred from B32; fix: remove buffer=0 market fallback |
| R-B32-03 / DW-B32-TRIM-CLOSE-01 | Trim ATM OCO bracket corruption on market exit path | B32 | P1 | Architect review needed; IsAtmBracketActive pattern is the proposed fix |

---

## Sim Test Gate (NOT yet run)

The sim validation session covering B34 bracket-replace-BE and B35 bracket-cancel
flows has not been run. This is an observational gate (code is live in NT8 via hard-link).

Pending validation items:
1. F5 compile in NinjaTrader (B35 final tag) — confirms no NT8 compiler errors
2. Open a Sim position on an ATM strategy
3. Press BE button — verify Output shows bracket-replace messages
4. Verify only PTT-BE-Stop and PTT-BE-Target-N appear in Active Orders grid
5. Verify original ATM Stop1/Target1..N are gone
6. Press Trim — verify CancelStaleBrackets fires before PTT-Trim CreateOrder
7. Press Flatten — verify CancelStaleBrackets fires before PTT-Flatten CreateOrder
8. Let position go flat — verify no orphan bracket orders remain

**Owner**: Director / manual sim test session
**Blocking**: No (code is live; test is observational validation)

---

## B36 Candidates

| ID | Description | Priority |
|----|-------------|----------|
| DW-B35-NEXT-01 | Sim test validation for BE bracket-replace + B35 bracket-cancel flows (full 8-step gate above) | P1 |
| DW-B35-NEXT-02 | DW-B32-TRIM-MARKET-01 fix: remove buffer=0 market fallback from ComputeLimitPx path | P1 |
| DW-B35-NEXT-03 | DW-B32-TRIM-ANCHOR-01 fix: ComputeLimitPx anchor correction (ask/bid peg) | P1 |
| DW-B35-NEXT-04 | R-B32-03 / DW-B32-TRIM-CLOSE-01: ATM OCO bracket corruption on market exit — architect-led fix | P1 |
| DW-B35-NEXT-05 | DW-B32-DEFERRED-03: Limit path ATM bracket detection (TrimOneAccountLimit / FlattenOneAccountLimit) — Director approval needed | P2 |
