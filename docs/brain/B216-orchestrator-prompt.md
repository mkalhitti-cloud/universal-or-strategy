# B216 Orchestrator Prompt
## Block: B216 -- Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons + DW-B216-01
## Status: DIRECTOR APPROVED -- READY FOR PIPELINE
## Director approval: Ask/Bid Pattern B conversion (Part A + Part B), Cancel + Cancel All buttons

---

## SCOPE SUMMARY

This block delivers 3 lanes in 1 pipeline:

| Lane | Work | New files in src/? | Files touched |
|------|------|-------------------|---------------|
| Lane A | Convert Ask + Ask-All to Pattern B (Account.All, no _leaderAccount dep) + Part A null-leader log | No | TradeCopierPanel.cs |
| Lane B | Convert Bid + Bid-All to Pattern B (mirrors Lane A exactly) | No | TradeCopierPanel.cs |
| Lane C | Add Cancel + Cancel All buttons below the 3x row (no arrows, plain buttons) | No | TradeCopierPanel.cs |

Deferred items closed by this block:
- DW-B216-01 Part A: null-leader diagnostic log on Ask-All / Bid-All (closed by Lane A + Lane B)
- DW-B216-01 Part B: Pattern B conversion of Ask + Ask-All + Bid + Bid-All (closed by Lane A + Lane B)
- DW-B211-01 (partial): direction-aware validation concern is addressed by Pattern B -- Ask targets
  the ask price regardless of direction; Bid targets bid price regardless of direction. No extra
  direction-guard needed for the "move target" semantic. DW-B211-01 is CLOSED-RESOLVED.

Deferred items NOT in scope (carry forward):
- DW-B214-02: 24 test failures (dedicated test-debt block)
- DW-B209-04: spec HTML update (B215 Lane C)
- DW-B212-01: Trim leader-skip (B215 Lane B -- comment only)
- DW-B214-01: CYC annotation (B215 Lane B -- comment only)
- DW-B209-01/02: Guardian false positive + BrushTeal gap (P2 cosmetic)
- DW-B212-02: PttMoveTargetAll future role (P2 decision)
- DW-B211-02: Target nudge buttons (depends on DW-B211-01, now closed)

---

## ARCHITECTURE REFERENCE (Architect MUST read before Ph1)

### Pattern A (current -- broken when copier off)
```
OnAskAllClick:
    if (_instrument == null) return;                     // silent
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;                  // SILENT FAILURE -- no log
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
    foreach (item in _followerItems) ...                 // selected UI items only
```

### Pattern B (target -- matches Be All / 3x All architecture)
```
OnAskAllClick (Pattern B):
    if (_instrument == null) return;
    foreach (Account acc in Account.All)                 // all accounts
    {
        foreach (Position pos in acc.Positions)
        {
            if (pos.Instrument?.FullName != _instrument.FullName) continue;
            if (pos.Quantity == 0) continue;
            PttMoveTargetOne.Execute(acc, _instrument, useAsk: true, _askBuffer);
            break;  // one position per account for this instrument
        }
    }
```

### Single-account Ask / Bid (leader only -- keep Pattern A, add null-leader log)
```
OnAskClick / OnBidClick:
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null)
    {
        NinjaTrader.Code.Output.Process("[PTT-MA] Ask: leader null -- check account combo", OutputTab1);
        return;
    }
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
```
NOTE: Single-account Ask/Bid buttons stay Pattern A -- they are intentionally leader-scoped.
Only the "All" variants convert to Pattern B.

### Cancel All (new -- Pattern B)
```
OnCancelAllClick:
    if (_instrument == null) return;
    foreach (Account acc in Account.All)
        CancelWorkingEntriesForAccount(acc, _instrument);   // reuse PttCancel's cancel logic
```
Cancel All cancels all Working/Initialized orders (any name) on all accounts for the instrument.
It is NOT restricted to entry orders -- it cancels anything cancellable (Entry, PTT-*, Target*, Stop*).
This matches NT8's native "Cancel All" behavior.

### Cancel (single, existing -- keep Pattern A, already has LogAndDispatchModule log)
The existing Cancel button (OnCancel2 -> LogAndDispatchModule -> PttCancel module) already exists
in the Position Tools collapsible section. The NEW Cancel button requested by Director is a
SECOND cancel button in the main non-collapsible area, below the 3x row, WITHOUT spinner arrows.
It must call the same underlying PttCancel module logic OR directly call CancelWorkingEntriesLocal.
Since CancelWorkingEntriesLocal is private in PttCancel, extract it to internal static or
create a new thin static helper `PttCancelAll` that Pattern-B iterates Account.All.

### Layout target (root.Children order after this block)
```
_beRowPanel         [BE | Be All]
_askRowPanel        [Ask | Ask-All]            <- Pattern B for Ask-All
_bidRowPanel        [Bid | Bid-All]            <- Pattern B for Bid-All
_instrRowPanel      [2x | 2x All]
_quickRowPanel      [3x | 3x All]
_cancelRowPanel     [Cancel | Cancel All]      <- NEW: plain buttons, no arrows
BuildCopierSection
_statusText
BuildCollapsibleHeader + _contentPanel
```

---

## LANE MAP

| Lane | Tickets | Files | Dependencies |
|------|---------|-------|--------------|
| Lane A | T1: Ask-All Pattern B + null-leader log on Ask / Ask-All | TradeCopierPanel.cs | None |
| Lane B | T1: Bid-All Pattern B + null-leader log on Bid / Bid-All | TradeCopierPanel.cs | None (mirrors Lane A) |
| Lane C | T1: Cancel + Cancel All button row below _quickRowPanel | TradeCopierPanel.cs | None |

All three lanes touch only TradeCopierPanel.cs. They are fully independent.
Recommended execution order: Lane A, then Lane B (mirrors A), then Lane C.
All three can share a single Ph1 architecture plan since all three touch the same file.
SINGLE COMBINED PIPELINE: Ph1 covers all three lanes together. One architecture plan,
one plan review, one ticket set (T-A1, T-B1, T-C1), one ticket review, three engineers
(one per lane), three verifiers (one per lane), one final review.

---

## PIPELINE CHAIN (MANDATORY -- ALL PHASES -- NONE SKIPPABLE -- NONE COMBINABLE)

Ph1  ptt-architect       -> docs/brain/B216/02-architecture-plan.md           (covers all 3 lanes)
Ph2  ptt-plan-reviewer   -> docs/brain/B216/02-plan-review.md                 (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B216/04-tickets.md                     (T-A1, T-B1, T-C1)
Ph3.5 ptt-ticket-reviewer -> docs/brain/B216/04-ticket-review.md              (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src edits + docs/brain/B216/ticket-{X}-completion.md  (one per ticket)
Ph4b ptt-verifier        -> docs/brain/B216/ticket-{X}-verification.md        (VERIFY_PASS per ticket)
Ph5  ptt-plan-reviewer   -> docs/brain/B216/05-final-review.md + 06-deferred-backlog.md

DO NOT SKIP ANY PHASE. DO NOT COMBINE PHASES. DO NOT RUN PIPELINE UNTIL DIRECTOR SAYS "run pipeline".

---

## FULL ORCHESTRATOR PROMPT
### (paste into ptt-orchestrator session)

---

You are the ptt-orchestrator for B216.

BLOCK: B216
EPIC: Ask/Bid Pattern-B Conversion + Cancel/CancelAll Buttons
SOURCE: DW-B216-01 (Director approved 2026), DW-B211-01 (resolved by Pattern B)
PRIOR BLOCK: B214 (FINAL_PASS -- docs/brain/NO-PIPELINE-REPAIRS.md line 4326)
PRIOR BLOCK: B215 (in progress -- Lane B comment-only, Lane C spec-only; independent of B216)

---

### CONTEXT

The PTT panel has 6 buffered-button rows. All "All" buttons are meant to operate on every
account with a position in the instrument. Investigation of a live trading failure revealed:

1. Ask-All and Bid-All use Pattern A (_leaderAccount dependency). They silently no-op when
   _leaderAccount is null (copier off, panel freshly opened, or account combo not wired).
2. Be All and 3x All use Pattern B (Account.All loop). They work regardless of _leaderAccount.
3. The Director wants Ask-All and Bid-All converted to Pattern B for consistency.
4. The Director also wants Ask and Bid single buttons to emit a diagnostic log when
   _leaderAccount is null (Part A of DW-B216-01) so failures are visible in Output tab.
5. A new Cancel / Cancel All button row must be added below the 3x row.
   Cancel = cancel working orders on the leader account (Pattern A, same as existing Cancel).
   Cancel All = cancel working orders on ALL accounts for the instrument (Pattern B).
   Both buttons: plain style, NO spinner arrows, matches 3x / 3x All visual row.

---

### KEY FILES (read before ANY phase)

1. `src/PropTraderTools/TradeCopierPanel.cs`
   - OnAskClick (line ~1488): current Pattern A -- add null-leader log only
   - OnAskAllClick (line ~1500): current Pattern A -- convert to Pattern B
   - OnBidClick (line ~1545): current Pattern A -- add null-leader log only
   - OnBidAllClick (line ~1557): current Pattern A -- convert to Pattern B
   - OnCancel2 (line ~1668): existing cancel (Pattern A via LogAndDispatchModule) -- DO NOT CHANGE
   - BuildBufferedButtonsRow (line ~1073): builds _beRowPanel, _quickRowPanel, _askRowPanel, _bidRowPanel
   - BuildUI (line ~875): root.Children.Add order -- add _cancelRowPanel after _quickRowPanel (line ~906)
   - BuildSpinnerCluster (line ~1277): factory for spinner clusters -- Cancel row uses plain buttons NOT this

2. `src/PropTraderTools/Features/PttCancel.cs`
   - CancelWorkingEntriesLocal (line ~66): the per-account cancel logic
   - IsWorkingEntryOrder (line ~102): order filter predicate
   - NOTE: CancelWorkingEntriesLocal is currently private. For Cancel All Pattern B, the engineer
     has two options: (A) extract CancelWorkingEntriesLocal to internal static and call it from
     TradeCopierPanel; (B) add a new internal static method to PttCancel:
       `internal static void CancelAllAccounts(Instrument instr)` that iterates Account.All.
     Option B is preferred -- keeps Account.All iteration inside PttCancel, not the panel.
     Name it `PttCancelAll` as a new static class (mirrors PttBeSwapAll / PttGlobalQuickExit pattern).

3. `docs/brain/NO-PIPELINE-REPAIRS.md` (read HOTFIX-B211-ASK-01, HOTFIX-B211-ASKALL-01,
   HOTFIX-B211-BIDALL-01 sections -- understand what B211 fixed before changing these handlers)

4. `docs/brain/B214/06-deferred-backlog.md` (DW-B211-01, DW-B216-01 context)

5. `docs/standards/jane-street/RULES_CATALOG.md` (mandatory gate)

---

### SPEC REQUIREMENTS

#### SR-A1: Ask null-leader diagnostic log (DW-B216-01 Part A)
OnAskClick: when _leaderAccount is still null after TryResolveLeaderAccount(), emit:
  `[PTT-MA] Ask: leader null -- check account combo on chart trader`
Output.Process to OutputTab1. Then return. No other change.

#### SR-A2: Ask-All Pattern B conversion (DW-B216-01 Part B)
OnAskAllClick: remove _leaderAccount dependency. New behaviour:
  - Iterate Account.All
  - For each account, find a non-flat position for _instrument
  - If found: call PttMoveTargetOne.Execute(acc, _instrument, useAsk: true, _askBuffer)
  - Log: `[PTT-MA] Ask-All: instrument=<name> accounts=<N>` where N = accounts acted on
  - If no account had a position: log `[PTT-MA] Ask-All: no positions found for <instr>`
  - Works identically whether copier is on or off

#### SR-B1: Bid null-leader diagnostic log (DW-B216-01 Part A mirror)
OnBidClick: mirrors SR-A1 exactly for Bid.
  `[PTT-MA] Bid: leader null -- check account combo on chart trader`

#### SR-B2: Bid-All Pattern B conversion (DW-B216-01 Part B mirror)
OnBidAllClick: mirrors SR-A2 exactly with useAsk: false.
  `[PTT-MA] Bid-All: instrument=<name> accounts=<N>`

#### SR-C1: New Cancel button (Pattern A -- leader only)
New plain button labelled "Cancel" in _cancelRowPanel.
Behaviour: cancel all Working/Initialized orders on _leaderAccount for _instrument.
  - Reuse existing PttCancel module via LogAndDispatchModule OR call CancelWorkingEntriesLocal directly.
  - Preferred: call LogAndDispatchModule("[CANCEL]", "CANCEL") -- exact same as existing OnCancel2.
  - This means Cancel in the main panel and Cancel in Position Tools are identical in behaviour.
  - No spinner arrows. Full-width button in left cell of _cancelRowPanel.

#### SR-C2: New Cancel All button (Pattern B -- all accounts)
New plain button labelled "Cancel All" in _cancelRowPanel (right cell).
Behaviour: cancel all Working/Initialized orders on Account.All for _instrument.
  - Create new file: `src/PropTraderTools/Features/PttCancelAll.cs`
  - Class: `internal static class PttCancelAll`
  - Method: `internal static void Execute(Instrument instr)`
  - Logic: iterate Account.All, for each account call the same cancel-list logic as PttCancel
    (iterate acc.Orders, collect Working+Initialized for instr.FullName, call acc.Cancel(list))
  - Log: `[CANCEL-ALL] <N> orders cancelled across <M> accounts for <instr>`
  - Panel handler: `OnCancelAllClick` -> `if (_instrument == null) return; PttCancelAll.Execute(_instrument);`

#### SR-C3: Cancel row layout
_cancelRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) }
Plain buttons (NO spinner arrows). BrushTeal border + teal background + white foreground to match
the 3x / 3x All row visual style (consistent with all buffered button rows).
Added to root.Children AFTER _quickRowPanel (line ~906 in BuildUI).

---

### SCOPE CONSTRAINTS (HARD LIMITS)

- Touch only: TradeCopierPanel.cs + new PttCancelAll.cs
- Do NOT change OnCancel2 (existing Position Tools cancel -- must remain unchanged)
- Do NOT change OnAskClick / OnBidClick logic other than adding the null-leader log line
- Do NOT change PttMoveTargetOne.Execute signature or internals
- Do NOT change PttCancel.cs (read it but do not modify it)
- PttCancelAll.cs is a NEW file -- the only new src/ file permitted
- CYC <= 8 on all modified/new methods
- Zero new lock() usages. Zero new async void. ASCII-only.
- ptt-sync 21/0 after every .cs edit

---

### CYC BUDGET

| Method | Expected CYC after change |
|--------|--------------------------|
| OnAskClick | 4 (unchanged -- one new log line, not a branch) |
| OnAskAllClick (Pattern B) | 4: (1) _instrument null, (2) Account.All foreach, (3) pos foreach, (4) instrument filter |
| OnBidClick | 4 (unchanged -- mirrors OnAskClick) |
| OnBidAllClick (Pattern B) | 4 (mirrors OnAskAllClick) |
| OnCancelClick (new) | 2: (1) _instrument null, (2) dispatch |
| OnCancelAllClick (new) | 2: (1) _instrument null, (2) PttCancelAll.Execute |
| PttCancelAll.Execute | 5: (1) instr null, (2) foreach acc, (3) foreach orders, (4) IsWorkingOrder, (5) cancel |

All within JS-066 limit of 8.

---

### 7-SCAN CHECKLIST (engineer contract -- all 7 mandatory before VERIFY_PASS)

SCAN-1: P0 violations
  grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
  grep -n "lock(" src/PropTraderTools/Features/PttCancelAll.cs
  grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
  grep -n "return null;" src/PropTraderTools/Features/PttCancelAll.cs
  Expected: 0 matches in modified/new lines.

SCAN-2: dotnet test -- zero new failures
  dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v minimal
  Expected: Failed=24 (pre-existing), Passed>=200, zero new failures.
  New tests: T_B216_A1 (Ask null-leader log), T_B216_A2 (Ask-All Pattern B),
             T_B216_B1 (Bid null-leader log), T_B216_B2 (Bid-All Pattern B),
             T_B216_C1 (CancelAll no-crash), T_B216_C2 (PttCancelAll.Execute with empty Account.All)

SCAN-3: CYC <= 8 for all modified methods (verify by inspection, count decision points)

SCAN-4: ASCII-only
  [System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") | Where-Object { $_ -gt 0x7E } | Measure-Object
  [System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttCancelAll.cs") | Where-Object { $_ -gt 0x7E } | Measure-Object
  Expected: count 0 in new/changed regions.

SCAN-5: Symbol existence
  Select-String "OnAskAllClick" src/PropTraderTools/TradeCopierPanel.cs        -- 1 match
  Select-String "OnBidAllClick" src/PropTraderTools/TradeCopierPanel.cs        -- 1 match
  Select-String "OnCancelAllClick" src/PropTraderTools/TradeCopierPanel.cs     -- 1 match
  Select-String "PttCancelAll" src/PropTraderTools/Features/PttCancelAll.cs   -- >= 1 match
  Select-String "_cancelRowPanel" src/PropTraderTools/TradeCopierPanel.cs     -- >= 2 matches (declare + add to root)
  Expected: all present.

SCAN-6: Functional correctness
  T-A2 AC-1: Ask-All fires PttMoveTargetOne.Execute on all accounts with a position (Account.All loop confirmed)
  T-A2 AC-2: Ask-All produces [PTT-MA] Ask-All log in Output tab
  T-A1 AC-1: Ask with null leader produces [PTT-MA] Ask: leader null log
  T-B2 AC-1: Bid-All fires on all accounts (mirrors Ask-All)
  T-C1 AC-1: Cancel button row visible below 3x row after F5
  T-C2 AC-1: Cancel All button present, no arrows
  T-C2 AC-2: Cancel All fires PttCancelAll.Execute with _instrument

SCAN-7: Diff hygiene
  git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^@@" | Measure-Object
  Expected: multiple hunks (not a single full-file replace -- single-hunk ban).
  powershell -File .\scripts\verify_pr_hygiene.ps1           -- DIFF GUARD PASS
  powershell -File .\scripts\verify_critical_symbols.ps1     -- GUARDIAN PASS
  powershell -File .\scripts\ptt-sync.ps1                   -- 21 linked, 0 errors

---

### ACCEPTANCE CRITERIA (full block)

| AC | Criterion | How verified |
|----|-----------|-------------|
| AC-A1 | OnAskClick with null _leaderAccount prints `[PTT-MA] Ask: leader null` | SCAN-2 T_B216_A1 |
| AC-A2 | OnAskAllClick no longer requires _leaderAccount -- fires on all accounts with position | SCAN-2 T_B216_A2 + SCAN-6 |
| AC-A3 | OnAskAllClick logs `[PTT-MA] Ask-All: instrument=X accounts=N` | SCAN-6 |
| AC-B1 | OnBidClick with null _leaderAccount prints `[PTT-MA] Bid: leader null` | SCAN-2 T_B216_B1 |
| AC-B2 | OnBidAllClick no longer requires _leaderAccount | SCAN-2 T_B216_B2 + SCAN-6 |
| AC-C1 | _cancelRowPanel visible below _quickRowPanel in built UI | SCAN-5 + F5 visual |
| AC-C2 | Cancel button (left) calls LogAndDispatchModule("[CANCEL]", "CANCEL") | SCAN-5 |
| AC-C3 | Cancel All button (right) calls PttCancelAll.Execute(_instrument) | SCAN-5 + SCAN-6 |
| AC-C4 | PttCancelAll.cs exists, passes JS-DNA scan | SCAN-1 + SCAN-4 |
| AC-C5 | No spinner arrows on either Cancel or Cancel All button | F5 visual / code review |
| AC-C6 | Cancel + Cancel All have teal border, teal background, white foreground | F5 visual |
| AC-ALL | Zero new test failures | SCAN-2 |
| AC-ALL | ptt-sync 21/0 | SCAN-7 |
| AC-ALL | DIFF GUARD PASS | SCAN-7 |

---

### SUCCESS CRITERIA (FINAL_PASS gate)

- All 7 scans PASS across all 3 ticket verifications
- 05-final-review.md and 06-deferred-backlog.md produced
- DW-B216-01 (Part A + Part B) marked CLOSED in 06-deferred-backlog.md
- DW-B211-01 marked CLOSED-RESOLVED (Pattern B makes direction guard unnecessary for move-target)
- NO-PIPELINE-REPAIRS.md updated with B216 PIPELINE_COMPLETE block

---

### DIRECTOR PRE-WORK (before pipeline runs)

None required. No gitignored files need manual changes for this block.

DO NOT RUN PIPELINE UNTIL DIRECTOR SAYS "run pipeline".
