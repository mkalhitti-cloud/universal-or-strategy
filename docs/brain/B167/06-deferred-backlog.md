# B167 Deferred Backlog

Block: B167  
Date: 2026-09-18  
Status: PIPELINE_COMPLETE (pending SIM gate)  
Produced by: ptt-plan-reviewer (Phase 5)

---

## B167-DEFER-01: ECA-1 Double-Position Race (Pre-fill Drag + Manual Fill)

| Field | Value |
|---|---|
| ID | B167-DEFER-01 |
| Priority | P2 |
| Target Block | future / monitor at SIM gate session |
| Status | OPEN |
| DW card | None yet |

**Description:** ~100–500 ms race window where a pre-fill drag drain fires (`HadPositionAtDrainTime=false`) AND the follower manually fills before `SubmitDrainedEntry` can submit the resubmit entry. If the fill arrives and creates a position during the drain window, `SubmitDrainedEntry` will skip the flat-guard (because `HadPositionAtDrainTime=false`) and submit a second entry order — potentially creating a double follower position.

**Probability:** Low. Requires precise timing alignment of a manual drag event and a slow order fill within the drain window (~100–500 ms). Not observed in any test session to date.

**Mitigation in B167:** `[DRAIN-SUBMIT-PREFILL]` diagnostic log (T4 at L8677–L8681) will appear in NT8 Output Tab 1 on every pre-fill drag path. If a double-position race occurs, this log provides forensic evidence of the triggering drain. The log fires for every pre-fill drain, not only race cases.

**Action:** Monitor during B167 SIM gate session (§F steps 1–7). If `[DRAIN-SUBMIT-PREFILL]` fires and is followed by an unexpected second entry, create a DW card for B168+ to add a post-submit position-count guard.

---

## B167-DEFER-02: DW-B167-02 ATM Bracket Placement (fo=NULL)

| Field | Value |
|---|---|
| ID | B167-DEFER-02 |
| Priority | P1 |
| Target Block | B168 (dedicated block required) |
| Status | OPEN |
| DW card | docs/brain/DW-B167-02/00-dw-card.md |

**Description:** After leader ATM fill, follower accounts never receive Stop/Target bracket orders. Every drag on leader brackets shows `fo=NULL` in TP4-SFB logs. Follower `followerOrders` shows only `Entry:Cancelled` — no Stop or Target orders exist.

**Root cause (preliminary, per DW-B167-02 card):** `IsDispatchTriggerState` rejects `StopMarket` bracket orders at all states. Gate0.5 (`IsExitSignalNameOrAnonClose`) blocks `Target1/2/3` by name. Neither path reaches follower dispatch. ATM brackets (`Stop1/2/3`, `Target1/2/3`) are never copied to followers after leader entry fill.

**Scope of B167 fix (Ticket 2):** The `TrySetSlateReadyOnDrainFill` fix (DW-B167-02) only restores `_followerSlateReady` state for Clone/Named-ATM mode fills. It does NOT create bracket orders on followers. Even with slate correctly set to `true` after Ticket 2, `FindFollowerBracketOrder` will still return null because the bracket orders were never placed in the first place.

**Second output.md required:** DW-B167-02 card defines a second test procedure to disambiguate whether `IsWorkingBracket=False` or `IsDispatchTriggerState` rejection is the primary blocker for `StopMarket` brackets. This data is prerequisite to designing the fix.

**Action:** Requires separate block (B168 or DW-B168). Director must run the second output.md test procedure per DW-B167-02/00-dw-card.md §"Second output.md Instructions" before architect can design the fix. Preliminary fix direction: dedicated `TryInitialBracketCopy` path triggered by leader-fill event — analogous to `SyncFollowerBracket` but for initial placement.

**Priority note:** This is a P1 defect — followers have naked positions with no stop protection after each leader entry fill. Must be addressed before live trading with ATM mode.

---

## B167-DEFER-03: B161–B166 Combined SIM Gate Pending

| Field | Value |
|---|---|
| ID | B167-DEFER-03 |
| Priority | P1 |
| Target Block | next SIM session after B167 deployment |
| Status | OPEN |
| Reference | docs/brain/no-pipeline-repairs.md |

**Description:** All 7 direct repairs (B161, B162-A, B162-B, B163, B164, B165, B166) have formal pipeline records complete (DW-B161B166-01 and DW-B161B166-02-03). B166 SIM gate passed individually on 2026-09-15 (no-pipeline-repairs.md line 591). However, the combined SIM gate for the full B161–B166 block (all 7 signals working together in one session, covering ghost-entry, BE-All race, bracket arming, and settle window scenarios) has not been run.

**SIM gate format:** Single session covering all scenarios from B161 through B166 SIM gate criteria (see no-pipeline-repairs.md entries). Must demonstrate zero ghost entries, correct re-entry after BE-All settlement, and correct bracket behavior across all 3 follower accounts.

**Action:** Run combined B161–B166 SIM gate after B167 SIM gate passes. B167 SIM gate is a prerequisite because B167 (pre-fill drag fix) is a dependency for observing clean follower entry behavior during B161–B166 ghost-entry tests.

---

## B167-DEFER-04: B167 SIM Gate Pending

| Field | Value |
|---|---|
| ID | B167-DEFER-04 |
| Priority | P1 |
| Target Block | first SIM session after B167 deployment |
| Status | OPEN |
| SIM criteria | docs/brain/B167/05-final-review.md §F |

**Description:** Full 12-step SIM gate for B167 (Ticket 1 pre-fill drag + post-flatten abort + Ticket 2 SLATE-READY-DRAIN) not yet run. All production code changes have passed Phase 4b verification (VERIFY_PASS). Unit tests pass. SIM gate is the final confirmation.

**Steps 1–7 (pre-fill drag):** Place limit entry, drag before fill, confirm `[DRAIN-SUBMIT-PREFILL]` fires, confirm zero `[DRAIN-ABORT-FLAT]`, confirm follower entries reappear at new price.

**Steps 8–11 (post-flatten abort):** Fill entry, confirm position opens, press QX, confirm `[DRAIN-ABORT-FLAT]` fires on drain+QX race, confirm no double positions.

**Steps 12–16 (SLATE-READY-DRAIN):** Fill `Entry` order in Clone/ATM mode, confirm `[SLATE-READY-DRAIN]` for all 3 followers, drag leader stop, confirm `fo=StopN` (not `fo=NULL`) for all followers including Sim104.

**Action:** Hot-compile B167 changes in NinjaTrader 8. Run 12-step SIM session. Log results in a SIM gate completion note. DEFER-03 (combined B161–B166 gate) runs after this gate passes.

---

## Prior OPEN Items Carried Forward

The following items from preceding blocks remain OPEN and are not addressed in B167:

| Prior ID | Block origin | Description | Status |
|---|---|---|---|
| DW-B159-K1 | B159 | SIM gate re-run for ghost re-entry guard + BE-All rollback | OPEN — subsumed by B167-DEFER-03 (combined B161–B166 SIM gate) |
| DW-B159-K2 | B159 | 14 xUnit tests for B159 features (infrastructure required) | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 in TryArmBePriceRetry | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing dependency | OPEN |
| DW-B159-K5 | B159 | Stale line number in IsFollowerPositionBlocked comment | OPEN |

---

*Deferred backlog produced by ptt-plan-reviewer. Phase 5. Block B167. Date 2026-09-18.*  
*All 4 new items (B167-DEFER-01 through B167-DEFER-04) are non-blocking for FINAL_PASS.*  
*FINAL_PASS is confirmed for B167 subject to SIM gate passage.*
