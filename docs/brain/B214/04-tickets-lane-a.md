# B214 Tickets -- Lane A
## Ph4a Engineer Contract: Validation-Only Execution

**Epic**: B214 -- B211 + B212 + B213 Repair Batch Validation
**Phase**: 3 (Ticket Generation)
**Architect**: ptt-architect
**Plan source**: `docs/brain/B214/02-architecture-plan-lane-a.md` (REVIEW_PASS)
**Plan review**: `docs/brain/B214/02-plan-review-lane-a.md` (REVIEW_PASS, 0 violations)
**Pipeline type**: VALIDATION-ONLY
**Status**: TICKETS_COMPLETE

---

## ZERO-NEW-CODE MANDATE (HARD PROTOCOL CONSTRAINT)

> **Ph4a MUST NOT modify, create, or delete any `.cs` file.**

Permitted Ph4a actions:
- READ: any source file, test file, or documentation
- RUN: ptt-sync, dotnet build, dotnet test, grep scans, CYC audit, deploy-sync, verify_critical_symbols
- REPORT: VERIFY_PASS or VERIFY_FAIL with evidence in `docs/brain/B214/ticket-1-completion.md`

**If Ph4a modifies any `.cs` file**: immediate PROTOCOL VIOLATION. Return VERIFY_FAIL citing unauthorized modification. Do NOT attempt to fix failing scans inline.

---

## T1: Validate B211+B212+B213 Hotfix Batch

### Ticket Header

| Field | Value |
|-------|-------|
| Ticket | T1 |
| Epic | B214 Lane A |
| Phase | Ph4a (Engineer Execution) |
| Type | VALIDATION-ONLY (read + scan + report) |
| Files | `src/PropTraderTools/TradeCopierPanel.cs`, `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttMoveTargetOne.cs` |
| Test files | `src/PropTraderTools/Tests/B211Tests.cs`, `src/PropTraderTools/Tests/B212Tests.cs` |
| Completion artifact | `docs/brain/B214/ticket-1-completion.md` |

---

### Spec Requirement IDs Satisfied by This Ticket

All 10 hotfixes are confirmed in source prior to Ph4a. This ticket validates their correctness:

| Spec Req ID | Description | Source (NO-PIPELINE-REPAIRS.md) |
|-------------|-------------|----------------------------------|
| HOTFIX-B211-ASK-01 | `TryFindWorkingTarget` accepts PTT- AND Target prefix; single-acct Ask button fix | L3232 |
| HOTFIX-B211-ASKALL-01 | `OnAskAllClick` direct loop (no AllAccounts/FindRule); calls PttMoveTargetOne.Execute | L3292 |
| HOTFIX-B211-BIDALL-01 | `OnBidAllClick` mirrors OnAskAllClick with useAsk: false | L3341 |
| HOTFIX-B211-MOVETARGET-01 | `CollectWorkingTargets` Order[] two-pass; `Execute` iterates ALL targets | L3262 |
| HOTFIX-B212-REENTRY-V3 | Gates B212-REENTRY-V3 and B212-REENTRY-FLATTEN in DispatchCopy before Gate 3 | L3728 |
| HOTFIX-B212-REENTRY-BETARGET | IsSuppressedReentry V4 guard; TryRemove after Gate 5; TryRecordBeTargetFill+TryEvictFollowerBeSlot | L3805, L3877, L3937, L3984 |
| HOTFIX-B212-REENTRY-FOLLOWER-V1 | HasFilledBeTargetFast gates in SubmitDrainedEntry + DrainThenDispatch | L4066 |
| HOTFIX-B212-REENTRY-FOLLOWER-V2 | IsLaneEEligible extraction (CYC 11->8) + gate (5b) | L4178, L4192 |
| HOTFIX-B212-FLATTEN-LEADERSKIP-01 | Flatten(Instrument) RelayFlatten leader-skip WHERE clause; direct overload unchanged | L3451 |
| HOTFIX-B212-MA-STOP-02 | SubmitTargetPair OCO ID + StopMarket+Limit pair; snapshotStop==0 guard | L3660 |
| HOTFIX-B213-MULTI-CHART-INSTRUMENT-01 | TryResolveLeaderAccount lazy re-wire path (6-step combo resolution) | L4240 |

---

### Method Signatures to Verify (NO new implementation -- CYC audit only)

Ph4a reads these signatures from source and verifies CYC targets. No code is written.

**File: `src/PropTraderTools/CopyEngine.cs`**

| Method | Required CYC | Hotfix | JS Rules |
|--------|-------------|--------|----------|
| `IsLaneEEligible(Account acc, Position pos)` | **= 8 EXACT** | HF-07 V2 | JS-021 (no lock), JS-066 (CYC<=8), JS-033 (no async void) |
| `SubmitDrainedEntry(...)` | **= 8 EXACT** | HF-07 V1 | JS-021, JS-066 |
| `DrainThenDispatch(...)` | **<= 8** | HF-07 V1 | JS-021, JS-066 |
| `DispatchCopy(Order order)` | **<= 8** | HF-05 + HF-06 | JS-021, JS-066, JS-001 (no throw in hot path) |
| `IsSuppressedReentry(Order order)` | **<= 8** (CYC=4 expected) | HF-06 | JS-021, JS-066 |
| `TryRecordBeTargetFill(Order order)` | **<= 8** | HF-06 | JS-021, JS-066 |
| `TryEvictFollowerBeSlot(Account acc)` | **<= 8** | HF-06 | JS-021, JS-066 |
| `Flatten(Instrument instrument)` | **<= 8** | HF-08 | JS-021, JS-066 |
| `IsLaneEFlattenLatched(Account acc, Position pos)` | **<= 2** | HF-07 V2 helper | JS-021, JS-066 |
| `IsPositionOrInstrumentNull(Position pos)` | **<= 2** | HF-07 V2 helper | JS-021, JS-066 |
| `IsFollowerSlateNotClosed(Account acc)` | **<= 2** | HF-07 V2 helper | JS-021, JS-066 |
| `IsLaneEBlockedByFlattenOrLeader(Account acc, Position pos)` | **<= 2** | HF-07 V2 helper | JS-021, JS-066 |

**File: `src/PropTraderTools/TradeCopierPanel.cs`**

| Method | Required CYC | Hotfix | JS Rules |
|--------|-------------|--------|----------|
| `OnAskClick(object sender, RoutedEventArgs e)` | **<= 8** | HF-01 | JS-021, JS-066 |
| `OnAskAllClick(object sender, RoutedEventArgs e)` | **<= 8** (CYC=4 expected) | HF-02 | JS-021, JS-066 |
| `OnBidAllClick(object sender, RoutedEventArgs e)` | **<= 8** (CYC=4 expected) | HF-03 | JS-021, JS-066 |
| `TryResolveLeaderAccount()` | **= 6 EXACT** | HF-10 | JS-021, JS-066 |

**File: `src/PropTraderTools/Features/PttMoveTargetOne.cs`**

| Method | Required CYC | Hotfix | JS Rules |
|--------|-------------|--------|----------|
| `Execute(Account acc, Instrument instr, bool useAsk, double buffer)` | **<= 8** (= 8 expected) | HF-04 | JS-021, JS-066 |
| `CollectWorkingTargets(Account acc, Instrument instr)` | **<= 8** | HF-04 | JS-021, JS-066 |
| `SubmitTargetPair(Account acc, Order target, double exitPrice, double snapshotStop, bool useAsk, int pairIdx)` | **<= 8** | HF-09 | JS-021, JS-066, JS-001 (no throw in hot path) |

---

### xUnit Tests to Confirm Pass

Ph4a runs `dotnet test` and confirms ALL tests pass. No new tests are written. These test classes must show 0 failures:

| Test Class | File | Covers |
|------------|------|--------|
| B211Tests | `src/PropTraderTools/Tests/B211Tests.cs` | HF-01 (TryFindWorkingTarget), HF-02 (OnAskAllClick), HF-03 (OnBidAllClick), HF-04 (CollectWorkingTargets / Execute) |
| B212Tests | `src/PropTraderTools/Tests/B212Tests.cs` | HF-05 (DispatchCopy gates), HF-06 (IsSuppressedReentry V4, TryRecordBeTargetFill, TryEvictFollowerBeSlot), HF-07 (IsLaneEEligible CYC=8, SubmitDrainedEntry, DrainThenDispatch), HF-08 (Flatten leader-skip), HF-09 (SubmitTargetPair OCO), HF-10 (TryResolveLeaderAccount) |

**Pass criterion**: `dotnet test` exits 0 with "0 Failed" in output. Any failure is a VERIFY_FAIL.

---

### 7-Scan Checklist (Engineer Contract)

The following scans MUST ALL PASS before VERIFY_PASS may be issued. Each scan must be run in order, results recorded in `docs/brain/B214/ticket-1-completion.md`.

```
[ ] SCAN-1: ptt-sync -- run powershell -File .\scripts\ptt-sync.ps1
    PASS condition: "21 linked, 0 errors"
    FAIL condition: any error count > 0
    Note: No .cs edits in this pipeline; pre-existing hard-link state must be preserved.

[ ] SCAN-2: dotnet test -- run dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj
    PASS condition: 0 Failed, 0 Errors (pre-existing warning count acceptable)
    FAIL condition: any test failure or error
    Test classes in scope: B211Tests.cs, B212Tests.cs

[ ] SCAN-3: CYC audit -- manually count branches OR use CYC audit tool on the following methods:
    CopyEngine.cs:
      IsLaneEEligible               MUST = 8  (8 branches: latch+mode+null+qty+slate+atm+beCount+flattenLeader)
      SubmitDrainedEntry            MUST = 8  (at ceiling from HF-07 V1)
      DrainThenDispatch             MUST <= 8 (fast-path branch only; confirm no regression)
      DispatchCopy                  MUST <= 8 (HF-05 +2 gates; HF-06 delta=0)
      IsSuppressedReentry           MUST <= 8 (expected = 4 after V4 guard)
      TryRecordBeTargetFill         MUST <= 8
      TryEvictFollowerBeSlot        MUST <= 8
      Flatten(Instrument)           MUST <= 8
      IsLaneEFlattenLatched         MUST <= 2 (helper)
      IsPositionOrInstrumentNull    MUST <= 2 (helper)
      IsFollowerSlateNotClosed      MUST <= 2 (helper)
      IsLaneEBlockedByFlattenOrLeader MUST <= 2 (helper)
    TradeCopierPanel.cs:
      OnAskClick                    MUST <= 8
      OnAskAllClick                 MUST <= 8 (expected = 4)
      OnBidAllClick                 MUST <= 8 (expected = 4)
      TryResolveLeaderAccount       MUST = 6  (was 4; HF-10 added 2 branches)
    PttMoveTargetOne.cs:
      Execute                       MUST <= 8 (was 14; DW-B212-CYC-01 closed by HF-04)
      CollectWorkingTargets         MUST <= 8 (new method, 2-pass pattern)
      SubmitTargetPair              MUST <= 8 (extracted method from HF-09)
    FAIL condition: any method exceeds its required CYC target
    Note: IsLaneEEligible = 7 is also a FAIL (regression from V2 extraction -- must equal 8 exactly).

[ ] SCAN-4: grep lock() -- run: grep -r "lock(" src/ --include="*.cs"
    PASS condition: zero results in TradeCopierPanel.cs, CopyEngine.cs, PttMoveTargetOne.cs
    FAIL condition: any match in hotfix-touched methods (JS-021 P0 violation)
    Note: Pre-existing results in untouched files are NOT a blocker but must be noted in completion report.

[ ] SCAN-5: grep async void -- run: grep -rn "async void " src/ --include="*.cs"
    PASS condition: zero results in new or modified method signatures introduced by the 10 hotfixes
    FAIL condition: any "async void" in a new/modified non-event-handler method (JS-033 P0 violation)
    Note: Pre-existing event handler async voids (e.g. Click handlers) that were NOT modified by
          these hotfixes are pre-existing and not a new violation. Document any found results.

[ ] SCAN-6: ASCII-only check -- run on all touched string literals in the three hotfix files:
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttMoveTargetOne.cs
    Additional: grep -rn "DateTime\.Now" src/ --include="*.cs"
    Additional: confirm all CreateOrder calls in SubmitTargetPair use "PTT-" prefix
      (expected names: "PTT-MA-Stop-NNNNN", "PTT-MA-Ask-NNNNN" or "PTT-MA-Bid-NNNNN")
    PASS condition: zero non-ASCII matches in modified sections; zero DateTime.Now matches; PTT- prefix confirmed
    FAIL condition: any non-ASCII character in new/modified string literals, or DateTime.Now present,
                    or CreateOrder name missing "PTT-" prefix (JS-080 / NT8-API P1 violation)

[ ] SCAN-7: git diff HEAD -- run: powershell -File .\deploy-sync.ps1 (DIFF GUARD for DW-B212-03)
    Also run: git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@"
              git show HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@"
              git show HEAD -- src/PropTraderTools/Features/PttMoveTargetOne.cs | Select-String "^@@"
    Also run: powershell -File .\scripts\verify_critical_symbols.ps1 (Guardian script)
    PASS condition:
      - deploy-sync.ps1 DIFF GUARD reports < 10,000 chars changed in src/
      - No touched file shows a single-hunk full-file replace (^@@ count > 1 per file OR expected)
      - verify_critical_symbols.ps1 reports GUARDIAN PASS
        (Exception: if ONLY the known DW-B209-01 false positive fires for
         TradeCopierPanel._instr2tBtn.Background, record it and mark PASS WITH NOTE)
    FAIL condition:
      - deploy-sync.ps1 DIFF GUARD reports >= 10,000 chars changed (AGENTS.md Platinum Standard violation)
      - Any touched file committed as single-hunk full-file replace
      - verify_critical_symbols.ps1 reports any failure OTHER than the known DW-B209-01 false positive
```

---

### JS Rule Constraints Applied Per Method

The following JS rules from `docs/standards/jane-street/RULES_CATALOG.md` are the enforceable constraints for each hotfix method. Ph4a confirms each via the scan checklist above.

| Rule ID | Severity | Scan | Applied To |
|---------|----------|------|-----------|
| JS-021 | P0 | SCAN-4 | ALL hotfix methods -- `lock()` absolutely banned; ConcurrentDictionary + Interlocked.Increment are correct patterns for `_filledBeTargetCount` and `NextBeOcoSeq()` |
| JS-033 | P0 | SCAN-5 | ALL hotfix method signatures -- `async void` banned except pre-existing Click event handlers |
| JS-001 | P0 | SCAN-6 | `DispatchCopy`, `IsSuppressedReentry`, `SubmitTargetPair` -- no `throw new XxxException` in hot path |
| JS-002 | P0 | SCAN-6 | `TryResolveLeaderAccount` -- pre-existing null sentinel (UI pattern, not hot path). Ph4a confirms HF-10 lazy re-wire did NOT add new `return null` beyond pre-existing returns |
| JS-066 | P1 | SCAN-3 | ALL 19 named methods in CYC audit table -- CYC <= 8 ceiling, exact targets where specified |
| JS-080 | P1 | SCAN-6 | ALL string literals in hotfix methods -- ASCII-only; no Unicode, emoji, curly quotes |
| NT8-049 | P1 | SCAN-6 | `SubmitTargetPair` in `PttMoveTargetOne.cs` -- CreateOrder arg positions: StopMarket uses arg7=stopPrice arg6=0.0; Limit uses arg6=limitPrice arg7=0.0 |
| PTT-prefix | P1 | SCAN-6 | `SubmitTargetPair` -- all CreateOrder names must begin with "PTT-" |
| DateTime.UtcNow | P1 | SCAN-6 | ALL hotfix methods -- `DateTime.Now` banned; `DateTime.UtcNow` required |

**NT8 API scope confirmation (no scans needed -- documented in plan):**
- `AtmStrategyCreate()`: NOT used (StrategyBase-only; not available in AddOnBase)
- `AtmStrategyChangeStopTarget()`: NOT used (StrategyBase-only; not available in AddOnBase)
- `Account.Change()`: NOT used in any hotfix
- Correct pattern used throughout: `Account.Cancel()` + `Account.CreateOrder()` + `Submit()`

---

### Deferred Items Not Closed by This Ticket

The following deferred work items from B212 are carried forward. Ph4a MUST NOT attempt to address these during validation:

| DW Item | Priority | Status | Notes |
|---------|----------|--------|-------|
| DW-B209-01 | P2 | OPEN -- carried to B215+ | Guardian script false positive for `_instr2tBtn.Background` |
| DW-B209-02 | P2 | OPEN -- cosmetic | TradeCopierPanel.cs BrushTeal gap |
| DW-B209-04 | P1 | OPEN -- pre-production | specs/002-trade-copier-spec.html B209 block updates required |
| DW-B211-01 | P2 | OPEN -- design required | Direction-aware Ask/Bid validation (V2 design needed before DW-B211-02) |
| DW-B211-02 | P2 | OPEN -- blocked by DW-B211-01 | Target nudge buttons |
| DW-B212-01 | P1 | OPEN -- investigation | Trim(Instrument) RelayTrim leader-skip investigation |
| DW-B212-02 | P2 | OPEN -- decision | PttMoveTargetAll.cs future role (retain vs deprecate) |
| DW-B212-03 | P1 | ADDRESSED BY SCAN-7 | DIFF GUARD pass in SCAN-7 = DW-B212-03 closed (ptt-final-reviewer confirms in Phase 5) |

---

### Completion Artifact

Ph4a MUST produce `docs/brain/B214/ticket-1-completion.md` containing:

1. **Scan results table**: one row per scan (SCAN-1 through SCAN-7), status (PASS / FAIL / PASS WITH NOTE), and raw command output excerpt
2. **CYC table**: all 19 methods with confirmed CYC values and PASS/FAIL status
3. **Test summary**: total tests run, passed, failed, errors
4. **P0 scan output**: raw grep results for lock(), async void, DateTime.Now
5. **DIFF GUARD output**: raw deploy-sync.ps1 output line showing char count
6. **Guardian script output**: raw verify_critical_symbols.ps1 result
7. **Final verdict**: VERIFY_PASS or VERIFY_FAIL (with specific failing check + file + line if FAIL)
8. **DW-B212-03 status**: CLOSED (if SCAN-7 PASS) or OPEN (if SCAN-7 FAIL)

**VERIFY_PASS requires ALL of the following to be true:**

| Check | Pass Condition |
|-------|---------------|
| ptt-sync (SCAN-1) | `21 linked, 0 errors` |
| dotnet test (SCAN-2) | 0 Failed, 0 Errors |
| IsLaneEEligible CYC (SCAN-3) | = 8 exactly |
| SubmitDrainedEntry CYC (SCAN-3) | = 8 exactly |
| DrainThenDispatch CYC (SCAN-3) | <= 8 |
| TryResolveLeaderAccount CYC (SCAN-3) | = 6 exactly |
| DispatchCopy CYC (SCAN-3) | <= 8 |
| IsSuppressedReentry CYC (SCAN-3) | <= 8 |
| Execute CYC (SCAN-3) | <= 8 |
| CollectWorkingTargets CYC (SCAN-3) | <= 8 |
| SubmitTargetPair CYC (SCAN-3) | <= 8 |
| 4x IsLaneEEligible helpers CYC (SCAN-3) | <= 2 each |
| OnAskAllClick, OnBidAllClick CYC (SCAN-3) | <= 8 each |
| lock() scan (SCAN-4) | 0 matches in hotfix methods |
| async void scan (SCAN-5) | 0 new/modified method signatures |
| ASCII-only + DateTime.Now (SCAN-6) | 0 non-ASCII, 0 DateTime.Now, PTT- prefix confirmed |
| DIFF GUARD (SCAN-7) | < 10,000 chars src/ diff |
| Single-hunk ban (SCAN-7) | No touched file committed as single-hunk full-file replace |
| Guardian script (SCAN-7) | PASS (DW-B209-01 false positive is known exception) |
| Zero .cs modifications | Ph4a made NO .cs edits during this ticket |

**VERIFY_FAIL**: cite the specific failing check with file + line evidence. Do NOT attempt to fix inline.

---

*Tickets produced by ptt-architect. Phase 3 complete.*
*Plan source: REVIEW_PASS (0 violations). TICKETS_COMPLETE.*
