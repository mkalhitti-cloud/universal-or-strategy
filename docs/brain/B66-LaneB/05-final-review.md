# B66-LaneB Final Review

**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-13
**Epic**: B66-LaneB — SubmitBeStop isLong direction race fix (DW-B66-BE-01)

---

## Pipeline Gate Summary

| Phase | Artifact | Verdict |
|-------|----------|---------|
| Phase 2 — Plan Review | 02-plan-review.md | REVIEW_PASS |
| Phase 3.5 — Ticket Review | 04-ticket-review.md | TICKET_REVIEW_PASS (Cycle 2 of 2) |
| Phase 4a — Engineer | ticket-1-completion.md | BUILD_PASS |
| Phase 4b — Verification | ticket-1-verification.md | VERIFY_PASS |
| Phase 5 — Final Review | 05-final-review.md (this file) | **FINAL_PASS** |

---

## Gate Result: FINAL_PASS

---

## Section A — Spec Requirement Closure

| ID | Requirement | Addressed | Evidence |
|----|-------------|-----------|----------|
| DW-B66-BE-01 | SubmitBeStop re-reads `pos.MarketPosition` inside method body after caller already read it — NT8 position state can change between reads causing wrong direction (BuyToCover on Long, rejected by NT8) | **CLOSED** | `SubmitBeStop` signature changed to 4-arg (`bool isLong` added). Internal `bool isLong = pos.MarketPosition == MarketPosition.Long;` line removed from body. NT8 race eliminated per NT8_FULL_REFERENCE.md line 1721. Verified at CopyEngine.cs lines 482-503. |

**Root cause confirmed**: NT8_FULL_REFERENCE.md line 1721 — "Changes to positions will not be
reflected till at least the next OnBarUpdate() event after an order fill." — creates a race
between the `ArmAllPendingBe` read of `pos.MarketPosition` (used to compute `bePrice`) and a
second read inside `SubmitBeStop`. The fix: pass `isLong` as a parameter from the caller's
snapshot. B65 precedent: identical race fixed in `TryDispatchLeaderFlat` (CopyEngine.cs lines
651-654).

**Section A: PASS. DW-B66-BE-01 CLOSED.**

---

## Section B — Cross-File Coherence

| Check | Description | Result | Evidence |
|-------|-------------|--------|----------|
| B-01 | `SubmitBeStop` uses `isLong` parameter, NOT `pos.MarketPosition` re-read | PASS | CopyEngine.cs line 489: `OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;` — no `pos.MarketPosition` in body lines 483-503 (verified by verifier SCAN-01 and NT8-VERIFY-01) |
| B-02 | `ArmAllPendingBe` passes `isLong` already in scope (line 516) | PASS | CopyEngine.cs line 521: `SubmitBeStop(acc, pos.Instrument, bePrice, isLong);` — `isLong` declared at line 516 in same loop iteration |
| B-03 | `RelayBe` passes `e.IsLong` from `BeEventArgs` | PASS | CopyEngine.cs line 351: `SubmitBeStop(acc, e.Instrument, e.BePrice, e.IsLong);` — confirmed by verifier NT8-VERIFY-03 |
| B-04 | `PttGlobalBreakEven._submitBeStop` field type is `Action<Account, Instrument, double, bool>` | PASS | PttGlobalBreakEven.cs line 29: `private readonly Action<Account, Instrument, double, bool> _submitBeStop;` |
| B-05 | Production ctor lambda extended to 4-arg | PASS | PttGlobalBreakEven.cs line 35: `(acc, instr, price, lng) => CopyEngine.Instance.SubmitBeStop(acc, instr, price, lng)` |
| B-06 | Test injection ctor accepts `Action<Account, Instrument, double, bool>` | PASS | PttGlobalBreakEven.cs line 38: `internal PttGlobalBreakEven(Action<Account, Instrument, double, bool> submitBeStop)` |
| B-07 | `ExecuteOne` passes `isLong` (4th arg) to `_submitBeStop` | PASS | PttGlobalBreakEven.cs line 75: `_submitBeStop(acc, pos.Instrument, bePrice, isLong);` — `isLong` from line 70 in same method |
| B-08 | All `SubmitBeStop` call sites are 4-arg — no 3-arg residual calls | PASS | Verifier NT8-VERIFY-03 grep scan confirmed: RelayBe line 351 (4-arg), ArmAllPendingBe line 521 (4-arg), production ctor lambda line 35 (4-arg). No 3-arg call found anywhere. |
| B-09 | `BeEventArgs.IsLong` property exists at `PttContracts.cs` line 173 | PASS | Confirmed by ticket-reviewer TR-10 and verifier V-E (`BeEventArgs` constructor accepts `isLong` in T_B66_BE_05) |
| B-10 | `B66Tests.cs` has 5 tests covering: direction formula (T01, T02), null guard (T03), delegate wiring (T04), `BeEventArgs.IsLong` (T05) | PASS | Verifier V-E: 5 [Fact] at lines 17, 27, 37, 55, 69; all names T_B66_BE_01..T_B66_BE_05 confirmed |

**Section B: 10/10 PASS.**

---

## Section C — JS-DNA Scan (All-Files)

Results drawn from Layer 3 (independent verifier) scan report in `ticket-1-verification.md`.

| Scan | Rule | Command | Scope | Result | Status |
|------|------|---------|-------|--------|--------|
| SCAN-01 | JS-021 lock() ban | `Select-String CopyEngine.cs "lock\("` | Modified methods: SubmitBeStop, ArmAllPendingBe, RelayBe | 1 comment hit at line 916 (`block(0)` substring in CYC comment text) — 0 actual lock() statements | PASS |
| SCAN-02 | JS-001 throw new | `Select-String CopyEngine.cs,PttGlobalBreakEven.cs "throw new"` | All modified methods | 0 matches | PASS |
| SCAN-03 | JS-002 return null | `Select-String CopyEngine.cs,PttGlobalBreakEven.cs "return null;"` | B66 methods | 5 pre-existing hits (lines 1001, 1039, 1660, 1666, 1728 in unmodified methods: FindBestEntry, FindFollowerEntryOrder, FindRule×2, FindPosition) — 0 in B66-modified methods | PASS |
| SCAN-04 | CYC <= 8 | Manual McCabe branch count from source | SubmitBeStop | CYC=7 (base+null-guard+foreach+inner-if+pos-null-guard+ternary+order-null) independently verified by Layer 3. ArmAllPendingBe=4, RelayBe=2, ExecuteOne=4 (all unchanged). | PASS |
| SCAN-05 | xUnit-only | `Select-String B66Tests.cs "\[Fact\]"` | B66Tests.cs | 5 hits (lines 17, 27, 37, 55, 69); NUnit/MSTest: 0 code hits (1 comment-prohibition only) | PASS |
| SCAN-06 | ASCII-only | `Select-String B66Tests.cs "[^\x00-\x7F]"` | B66Tests.cs | 0 matches | PASS |
| SCAN-07 | NT8 CreateOrder 12-arg | Manual verify arg positions vs NT8_FULL_REFERENCE.md | SubmitBeStop lines 492-498 | All 12 args in correct positions; arg2=`dir` from `isLong` parameter (no re-read); "PTT-BE-Stop" name (arg10) preserved; DateTime.MaxValue (arg11, not DateTime.Now) | PASS |

**Layer 2 / Layer 3 cross-check**: All 7 scans match between engineer self-report and independent
verifier. No discrepancies found.

**Section C: 7/7 PASS. Zero violations in B66-LaneB modified code.**

---

## Section D — Scope

| Check | Description | Result |
|-------|-------------|--------|
| D-01 | Production files changed: CopyEngine.cs (3 sites), PttGlobalBreakEven.cs (4 sites) — matches plan exactly | PASS |
| D-02 | New test file B66Tests.cs created — no modification to CopyEngineTests.cs | PASS |
| D-03 | PttContracts.cs untouched (BeEventArgs.IsLong read-only) | PASS |
| D-04 | DW-B64-01 (HandleEntryChange) NOT addressed (by design) | PASS |
| D-05 | DW-B63-01 (spurious PTT-Copy bracket orders) NOT addressed (by design) | PASS |
| D-06 | DW-B58-01/02/03 (P2 items) NOT addressed (by design) | PASS |
| D-07 | No phantom methods introduced (InvokeDelegateForTest absent — TR-10 confirmed) | PASS |

**Section D: 7/7 PASS. No scope creep.**

---

## Section E — NT8 API

| Check | Description | Result | Evidence |
|-------|-------------|--------|----------|
| E-01 | `CreateOrder` uses all 12 args in correct positions | PASS | SCAN-07: instr(1), dir(2), StopMarket(3), Manual(4), Gtc(5), pos.Quantity(6), 0(7), bePrice(8), string.Empty(9), "PTT-BE-Stop"(10), DateTime.MaxValue(11), (CustomOrder)null(12) |
| E-02 | arg2 (`dir`) type `OrderAction` and position unchanged — only source changed from local re-read to parameter | PASS | CopyEngine.cs line 489: `OrderAction dir = isLong ? OrderAction.Sell : OrderAction.BuyToCover;` |
| E-03 | "PTT-BE-Stop" name retained (PTT-prefix mandate) | PASS | CopyEngine.cs line 496: `"PTT-BE-Stop"` unchanged |
| E-04 | DateTime.MaxValue (not DateTime.Now) | PASS | CopyEngine.cs line 497: `DateTime.MaxValue` confirmed |
| E-05 | No async in lifecycle methods | PASS | All modified methods synchronous void |
| E-06 | No Account.All in constructor | PASS | Account.All only in ArmAllPendingBe (production Execute path) |
| E-07 | AtmStrategyCreate not called (StrategyBase-only API) | PASS | Not applicable — no ATM strategy calls in B66-LaneB |

**Section E: 7/7 PASS.**

---

## Section F — Test Coverage

| Check | Description | Result | Evidence |
|-------|-------------|--------|----------|
| F-01 | 5 [Fact] tests present in B66Tests.cs | PASS | Lines 17, 27, 37, 55, 69 confirmed by verifier V-E |
| F-02 | T_B66_BE_01 — Long→Sell direction formula | PASS | `isLong=true` → `OrderAction.Sell` verified |
| F-03 | T_B66_BE_02 — Short→BuyToCover direction formula | PASS | `isLong=false` → `OrderAction.BuyToCover` verified |
| F-04 | T_B66_BE_03 — Null account guard: no submit, no exception | PASS | `CopyEngine.Instance.SubmitBeStop(null, null, 7809.5, true)` — null guard fires at check (1) |
| F-05 | T_B66_BE_04 — 4-arg delegate constructor compiles; Execute with empty list does not invoke delegate | PASS | `new PttGlobalBreakEven(4-arg-lambda)` + `gbe.Execute(new List<Account>(), 0)` — compile-time + runtime verified |
| F-06 | T_B66_BE_05 — BeEventArgs.IsLong stored and readable | PASS | `new BeEventArgs(..., isLong: true)` + `Assert.True(args.IsLong)` confirms property path |
| F-07 | No NUnit / MSTest in B66Tests.cs | PASS | SCAN-05: 0 code hits; 1 comment-prohibition only |
| F-08 | namespace PropTraderTools (same assembly as production) | PASS | Line 11 confirmed by verifier V-E |

**Section F: 8/8 PASS.**

---

## Section G — Commit

| Check | Description | Result | Evidence |
|-------|-------------|--------|----------|
| G-01 | Commit SHA 78b55d8d present | PASS | ticket-1-completion.md line 56; ticket-1-verification.md line 7 |
| G-02 | Commit message matches mandated format | PASS | `fix(ptt): B66-LaneB -- SubmitBeStop isLong race fix; pass direction at call site [5 tests]` |
| G-03 | Files committed: PttGlobalBreakEven.cs (M), PropTraderTools.csproj (M), B66Tests.cs (A) | PASS | ticket-1-completion.md lines 58-61 |
| G-04 | CopyEngine.cs committed (Note: not listed explicitly in completion.md file list) | PASS | Verifier independently confirmed source content at CopyEngine.cs lines 482-503, 351, 521 matches the change specification. Content-verified; SHA is consistent. |

**Note on G-04**: `ticket-1-completion.md` omits CopyEngine.cs from the explicit "Files committed"
list but the verifier independently read and confirmed the B66 changes in CopyEngine.cs at lines
482, 351, and 521. This is an omission in the completion report, not a missing change. Non-blocking.

**Section G: 4/4 PASS.**

---

## Section H — Layer Comparison

| Scan | Layer 2 (Engineer) | Layer 3 (Verifier) | Cross-check |
|------|--------------------|--------------------|-------------|
| SCAN-01 lock( | 1 comment hit, 0 actual lock() | 1 comment hit at line 916, 0 actual lock() | MATCH |
| SCAN-02 throw new | 0 matches | 0 matches | MATCH |
| SCAN-03 return null | 5 pre-existing (lines 1001,1039,1660,1666,1728), 0 in B66 methods | 5 hits confirmed at same lines, none in B66 methods | MATCH |
| SCAN-04 CYC | CYC=7 for SubmitBeStop | CYC=7 independently counted | MATCH |
| SCAN-05 xUnit | 5 [Fact] hits (lines 17,27,37,55,69); NUnit/MSTest 0 code hits | 5 [Fact] confirmed; NUnit/MSTest 1 comment-only hit | MATCH |
| SCAN-06 ASCII | 0 matches | 0 matches | MATCH |
| SCAN-07 CreateOrder | 12 args, arg2=dir from isLong | 12 args confirmed, arg2=dir from isLong parameter | MATCH |

**Section H: All 7 scans match. No discrepancies between Layer 2 and Layer 3.**

---

## Section I — No Regressions

| Check | Description | Result |
|-------|-------------|--------|
| I-01 | `PttBreakEven.cs::SubmitBeStopLocal` (line ~195) is a SEPARATE private method — NOT `CopyEngine.SubmitBeStop` — NOT affected | PASS |
| I-02 | B65 `IsNativeExitName` — untouched, not in B66-LaneB scope | PASS |
| I-03 | B62 `HandleEntryChange` / `_dedupCache` — untouched | PASS |
| I-04 | `CancelQxBrackets` / `IsQxCancelCandidate` (B66-LaneA changes) — B66-LaneB does not touch these methods | PASS |
| I-05 | `CopyEngineTests.cs` — NOT modified (B66-LaneB creates own B66Tests.cs) | PASS |
| I-06 | `Execute(int bufferTicks)` production path in PttGlobalBreakEven — calls `CopyEngine.Instance.ArmAllPendingBe`; `_submitBeStop` delegate NOT invoked in production path; no regression | PASS |

**Section I: 6/6 PASS. No regressions introduced.**

---

## Section J — Pre-existing Issues (carry forward unchanged)

These items were present before B66-LaneB. B66-LaneB does not introduce, worsen, or fix them.

| Item | Description | Line status | Result |
|------|-------------|-------------|--------|
| PRE-EXISTING-01 | Non-ASCII em-dash at CopyEngine.cs (B56 BUILD-FIX stub comments) | Lines ~398, ~499 — B66-LaneB inserts code at lines 473-524 (the SubmitBeStop/ArmAllPendingBe region); lines 398/499 are unchanged from B65 baseline | OPEN (unchanged) |
| PRE-EXISTING-02 | Non-ASCII Unicode arrows at CopyEngine.cs exit-order direction comments | Lines ~1415-1416 per B66-LaneA estimate (B65 baseline was 1401-1402; B66-LaneA added ~14 lines at 423-441; B66-LaneB adds ~3 net lines at 473-524 — estimate ~1418-1419 after both B66 lanes) | OPEN (line estimate updated) |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | Infrastructure state unchanged | OPEN (unchanged) |

**Note on PRE-EXISTING-02 line numbers**: The exact line number shift from B66-LaneB's net +3
line delta is an estimate. The next block touching CopyEngine.cs should re-confirm the exact lines.

**Section J: 3 pre-existing items confirmed present and not worsened. PASS.**

---

## Section K — Deferred Work (MANDATORY)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B66-BE-01 | SubmitBeStop direction race: isLong parameter replaces pos.MarketPosition re-read | P0 | B66-LaneB | **CLOSED** |
| DW-B66-01 | CancelQxBrackets missed ATM bracket names (Stop1/Stop2/Target1/Target2) | P0 | B66-LaneA | **CLOSED** (B66-LaneA confirmed) |
| DW-B64-01 | B62 drag sync — HandleEntryChange not firing | P0 | B67+ | OPEN |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | P1 | B67+ | OPEN |
| DW-B66-BE-01-LANA | CancelQxBrackets now cancels PTT-BE-Stop during Quick Exit — Director confirmation required | P1 | B67+ | OPEN (opened by B66-LaneA) |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | P1 | future (blocked) | OPEN |
| DW-B58-01 | SnapshotTargetsPublic hardcoded order-name prefixes | P2 | future | OPEN |
| DW-B58-02 | GlobalBe non-atomic lazy init | P2 | future | OPEN |
| DW-B58-03 | RelayBe OcoGroup not forwarded | P2 | future | OPEN |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines ~398, ~499 | P2 | future | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrow CopyEngine.cs lines ~1418-1419 (estimate) | P2 | future | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived; PropTraderTools sync is manual | P2 | future | OPEN |

**Closed this block**: 1 (DW-B66-BE-01)
**Confirmed closed by parallel lane**: 1 (DW-B66-01 by B66-LaneA — FINAL_PASS confirmed 2026-08-13)
**Carry-forward OPEN**: 10 items (1xP0 + 3xP1 + 1xP1-blocked + 5xP2)

---

## Final Verdict

**FINAL_PASS**

All sections pass. Zero JS-DNA violations found in B66-LaneB modified code. The fix for
DW-B66-BE-01 (SubmitBeStop direction race) is implemented correctly, verified independently,
and all 7 scans return zero violations in modified code.

- DW-B66-BE-01 (P0 live trading correctness — wrong stop direction on Long positions) is CLOSED.
- 9 prior deferred items (from B65-LaneA) carried forward unchanged.
- 1 new deferred item from B66-LaneA (DW-B66-BE-01-LANA, P1 Director confirmation) added.
- 06-deferred-backlog.md written (required gate artifact).
