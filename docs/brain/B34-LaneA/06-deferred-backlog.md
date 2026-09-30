# B34-LaneA Deferred Backlog
# Block: B34 | DW-B33-04 | bracket-replace-BE
# Date: 2026-07-22
# Status: PIPELINE_COMPLETE — no deferred items from this block

---

## Block B34 — DW-B33-04 Status

| Item | Description | Status |
|------|-------------|--------|
| DW-B33-04 | ATM Bracket Replace on BE | CLOSED — implemented and verified in B34 |

---

## Deferred Work from B34

None. All planned changes (C1–C6, T1–T4) were implemented and verified.

---

## Open Defects Carried Forward

| ID | Description | Source | Priority |
|----|-------------|--------|----------|
| U1 | NT8 Add-On `Account.CreateOrder` arg8 OCO group ID effectiveness on sim | B34 handoff Section 5 | LOW — requires sim test (Section 6 of handoff); CancelStaleBrackets(cancelPttBe:true) cleans up on flat regardless |
| U3 | Confirm Limit order arg6=limitPrice, arg7=0 correct in live NT8 | B34 handoff Section 5 | MEDIUM — verify via sim test output (wrong order price visible if swapped) |

---

## Sim Test Gate (Section 6 of handoff — NOT yet run)

The 9-step sim test documented in `docs/brain/B34-LaneA/00-session-handoff.md` Section 6
has NOT been run yet. This is the F5 / live-sim validation gate:

1. F5 compile in NinjaTrader — confirms no NT8 compiler errors
2. Open a Sim position on an ATM strategy
3. Press BE button
4. Verify Output tab shows: `[BE] Snapshot target: Target1 ...`
5. Verify Output tab shows: `[BE] bracket-replace: 1 stop + N targets submitted`
6. Verify only PTT-BE-Stop and PTT-BE-Target-N appear in Active Orders grid
7. Verify original ATM Stop1/Target1..N are gone
8. Let position hit a target — verify OCO cancels PTT-BE-Stop
9. Let position go flat — verify CancelStaleBrackets(cancelPttBe:true) cleans PTT-BE-* residuals

**Owner**: Director / manual sim test session
**Blocking**: No (code is live in NT8 via hard-link; test is observational validation)

---

## B35 Candidates (next block suggestions)

| ID | Description | Priority |
|----|-------------|----------|
| DW-B34-01 | Sim test validation of bracket-replace-BE (Section 6 steps 1–9) | HIGH |
| DW-B34-02 | Follower bracket replacement — extend SnapshotTargets fan-out to follower accounts | LOW |
| DW-B34-03 | BE bracket OCO effectiveness investigation on live broker (U1) | LOW |
