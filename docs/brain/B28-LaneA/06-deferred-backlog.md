# PTT-COPIER Deferred Backlog
# Format: ## BLOCK-LANE -- [date] / ### Items / - [DW-XXX-NN] Description | Reason | Target block

---

## B28-LaneA -- 2026-07-16

### Items

No items deferred from B28-LaneA.

DW-B28-01 (P0) diagnostic hardening was implemented and verified in this block.
The +1 StatusUpdate line before acc.Change() in MoveStopToBreakEven is deployed
to NinjaTrader. No work was identified that needed to be pushed to a future block.

Pre-existing open items carried from prior blocks (not created by B28-LaneA):

- [DW-B17-SYNC-01] Copy ON/OFF sync across surfaces | Pre-existing open item from B17; not in B28 scope | future block (P2)
- [DW-B17-LEADER-01] WireLeaderAccount ComboBox walk | Pre-existing open item from B17; not in B28 scope | future block (P2)

### B28-LaneA Outcome

After next live test, Director will observe one of three outcomes in the PTT status bar:
1. "Sim101: BE attempting acc.Change -> 7566.25" AND "Sim101: BE moved to 7566.25"
   -> acc.Change() succeeded; investigate why stop price is still not updating (exchange-side issue)
2. "Sim101: BE attempting acc.Change -> 7566.25" AND "PTT-BE error: [message]"
   -> acc.Change() is throwing; the exception message reveals the NT8 API error
3. "BE attempting acc.Change" does NOT appear
   -> Execution never reaches this code; defect is upstream in the gate chain

This determines the root cause for B29.
