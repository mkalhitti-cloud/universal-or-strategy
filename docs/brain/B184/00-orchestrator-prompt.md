# B184 Orchestrator Prompt
# Paste this into a fresh ptt-orchestrator session

---

You are the **ptt-orchestrator** for epic **B184**.

This epic has TWO tickets:

- **B184-T1**: Restore 5 BE event infrastructure members to `CopyEngine.cs` (P0 compile blocker)
- **B184-T2**: Remove ATR position sizing tool completely (UI + engine + files)

Both tickets go through the **full 5-phase PTT pipeline**. No phases may be skipped, combined,
or abbreviated.

---

## THE PIPELINE (mandatory -- all phases -- no shortcuts)

```
Ph1  ptt-architect       -> docs/brain/B184/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B184/02-plan-review.md        (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B184/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B184/04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> docs/brain/B184/ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B184/05-final-review.md + docs/brain/B184/06-deferred-backlog.md
```

After Ph5 issues `FINAL_PASS`, run the $continue prompt in
`docs/brain/B184/01-continue-prompt.md`.

---

## CONTEXT

### B183 result
B183 FINAL_PASS was issued on 2026-07-07. All 16 BUG1+BUG2 regression symbols are confirmed
present in HEAD. B183 artifacts are at `docs/brain/B183/`.

### Why B184 exists
During B183 Ph5, the plan reviewer identified a P0 compile blocker (DW-B183-06/07/08/09):
5 BE event infrastructure members were dropped by megacommit 878a5b33 but were outside B183
scope. The solution currently fails with:

```
TradeCopierPanel.cs(3045): error CS0246 -- type or namespace name 'BeState' could not be found
```

In addition, the Director has approved full removal of the ATR position sizing tool (B184-T2).

---

## TICKET 1 -- B184-T1: Restore CopyEngine.cs BE Event Infrastructure

### Problem
The following 5 members are referenced by `TradeCopierPanel.cs` but are absent from
`CopyEngine.cs` (dropped by megacommit 878a5b33):

| # | Symbol | Kind | Panel reference lines |
|---|--------|------|-----------------------|
| 1 | `BeState` | enum | 3039, 3045, 3049 |
| 2 | `PendingBeArmed` | event | 790 (subscribe), 569 (unsubscribe) |
| 3 | `GlobalBeAllDisarmed` | event | 791 (subscribe), 570 (unsubscribe) |
| 4 | `DisarmPendingBe(Account)` | method | 3070 |
| 5 | `IsPendingSlotsEmpty()` | method | 3067 |
| 6 | `RaiseBeAllDisarmed()` | method | 3071 |

*(Note: "5 members" in the deferred backlog was a shorthand -- `BeState` enum + 5 methods/events = 6 items.)*

### Fix Source
```
git show f01b5155:src/PropTraderTools/CopyEngine.cs
```
Grep this historic blob for each symbol. Lift verbatim. Do NOT cherry-pick the full file.

### Hard Constraints
- Restore ONLY the 6 items above.
- The 5 B180-2 APPROVED REMOVALS remain permanently excluded:
  `ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`, `UpdateBeVisuals`, `OnBeConnected`.
- Do NOT restore any of those 5 items even if found in f01b5155.
- Single-Hunk Ban: a diff with exactly 1 hunk on `CopyEngine.cs` is automatic VERIFY_FAIL.
- Commit Size Gate: >300 changed lines on CopyEngine.cs requires Director explicit approval.

### Acceptance Criteria
1. `dotnet build` exits 0 errors (warnings OK -- the 172 CS0436 are pre-existing NT8 artifacts).
2. `BeState` enum is present in `CopyEngine.cs`.
3. `PendingBeArmed` and `GlobalBeAllDisarmed` event declarations present in `CopyEngine.cs`.
4. `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` method bodies present in `CopyEngine.cs`.
5. None of the 5 B180-2 approved removals are re-introduced.
6. `scripts/verify_critical_symbols.ps1` still passes (exit 0 -- all 16 B183 symbols still present).
7. `powershell -File .\scripts\ptt-sync.ps1` runs clean after edit.

---

## TICKET 2 -- B184-T2: Full Removal of ATR Position Sizing Tool

### Problem / Decision
The Director has approved complete removal of the ATR position sizing subsystem. This includes:
- The `Risk $` and `ATR %` spinner row UI
- The `ATR=-.-- pts -> stopTicks=-- -> qty=--` display label
- All backing fields, methods, and event handlers in `TradeCopierPanel.cs`
- All pass-through methods in `CopyEngine.cs`
- The `AtrSizingEngine.cs` file (delete entirely)
- The `AtrCalc.cs` file (delete entirely)
- The `AtrSizing` feature flag from `FeatureFlags.cs`
- The ATR engine wiring in `TradeCopierAddOn.cs`

### Exact Removal Scope

#### `TradeCopierPanel.cs` (~87 lines to remove)
Fields to remove:
- `_maxRiskDollars` (line 295)
- `_atrFraction` (line 296)
- `_riskDollarsBox` (line 297)
- `_atrFractionBox` (line 298)
- `_atrDisplayLabel` (line 301)
- `_atrRow` (line 274)

Methods to remove entirely:
- `BuildRiskAtrRow(StackPanel root)` (~line 2764)
- `BuildAtrDisplayRow()` (~line 2839)
- `OnRiskUp` (~line 2864)
- `OnRiskDown` (~line 2873)
- `OnRiskTextLostFocus` (~line 2891)
- `OnAtrFractionUp` (~line 2902)
- `OnAtrFractionDown` (~line 2911)
- `OnAtrFractionTextLostFocus` (~line 2920)
- `NotifyRiskChanged()` (~line 2931)
- `NotifyAtrFractionChanged()` (~line 2939)

Call sites to remove/clean:
- `BuildRiskAtrRow(_contentPanel);` call in `BuildUI()` (~line 866)
- `NotifyRiskChanged();` call in `OnLoaded` (~line 766)
- `NotifyAtrFractionChanged();` call in `OnLoaded` (~line 767)
- `_atrRow.Visibility = ...` block in `ApplyRowVisibilityFlags` (~lines 2975-2978)

#### `CopyEngine.cs` (~24 lines to remove)
- `_atrEngine` field declaration (~line 137)
- `_atrEnabled` field declaration (~line 136)
- `SetAtrEngine(...)` method (~line 641, 7 lines)
- `UpdateMaxRisk(double)` method (~line 650, 7 lines)
- `UpdateAtrFraction(double)` method (~line 657, ~6 lines)
- `GetSuggestedQty(Instrument)` method (~line 1325, ~6 lines) -- **only if** it is exclusively
  used by ATR sizing. Verify no other caller before deleting.
- ATR gate inside quantity resolution (~lines 2748-2753)

#### `TradeCopierAddOn.cs` (~32 lines to remove)
- `_atrEngines` ConcurrentDictionary field (~line 44)
- `_atrPollTimer` field
- `StartAtrEngine(Chart, Instrument)` method (~lines 219-257)
- `StopAtrEngine(Chart)` method (~lines 261-274)
- `UpdateAtrOverlay(string)` method (~lines 276-287)
- `OnAtrUpdated(string)` method (~lines 289-295)
- All call sites: `StartAtrEngine(...)`, `StopAtrEngine(...)`, `UpdateAtrOverlay(...)`

#### `FeatureFlags.cs`
- Remove `AtrSizing` parameter from the record definition
- Remove `AtrSizing` from `Starter()`, `Pro()`, `Elite()` constructor calls
- Remove `AtrSizing: System.Linq.Enumerable.Contains(feats, "atr_sizing")` from `FromFeatureList`

#### Files to delete entirely
- `src/PropTraderTools/AtrSizingEngine.cs` (166 lines)
- `src/PropTraderTools/AtrCalc.cs` (141 lines)

### Hard Constraints
- `AtrCalc` is only used by `AtrSizingEngine`. Once `AtrSizingEngine.cs` is deleted, `AtrCalc.cs`
  has zero callers -- delete both.
- After removal, `FeatureFlags` record must remain a valid C# record with correct positional
  parameter count in all factory methods (`Starter`, `Pro`, `Elite`, `FromFeatureList`).
- The `Elite()` preset currently sets `AtrSizing: true`. After removal, adjust Elite arity.
- Do NOT touch `ClickTrader`, `MirrorMode`, or any other feature flag.
- Single-Hunk Ban applies to `CopyEngine.cs` (as always).
- After deletion of the two files, run `ptt-sync.ps1` -- the sync script may reference them;
  verify it does not error on missing files.

### Acceptance Criteria
1. `dotnet build` exits 0 errors.
2. No reference to `AtrSizingEngine`, `AtrCalc`, `_riskDollarsBox`, `_atrFractionBox`,
   `BuildRiskAtrRow`, `_atrRow`, `_atrDisplayLabel`, `UpdateMaxRisk`, `SetAtrEngine`,
   `AtrSizing`, `_atrEngine` anywhere in the solution.
3. `FeatureFlags.cs` compiles -- record arity matches all 4 factory sites.
4. `TradeCopierPanel.cs` has no `BuildRiskAtrRow` call in `BuildUI()`.
5. `scripts/verify_critical_symbols.ps1` still exits 0 (B183 symbols unaffected).
6. `powershell -File .\scripts\ptt-sync.ps1` runs clean (no missing-file errors).
7. `AtrSizingEngine.cs` and `AtrCalc.cs` do not exist in `src/PropTraderTools/`.

---

## TICKET EXECUTION ORDER

Execute **T1 first**, then **T2**. Both must reach VERIFY_PASS before Ph5 may run.
Do not merge the two tickets into a single commit. Each ticket gets its own commit.

---

## PHASE ARTIFACTS REQUIRED

All artifact files must land under `docs/brain/B184/`:

```
02-architecture-plan.md
02-plan-review.md            (REVIEW_PASS: YES)
04-tickets.md
04-ticket-review.md          (TICKET_REVIEW_PASS: YES)
ticket-1-completion.md       (BUILD_PASS)
ticket-1-verification.md     (VERIFY_PASS: YES)
ticket-2-completion.md       (BUILD_PASS)
ticket-2-verification.md     (VERIFY_PASS: YES)
05-final-review.md           (FINAL_PASS: YES)
06-deferred-backlog.md
```

---

## GUARDIAN SCRIPT

After every `.cs` edit involving `CopyEngine.cs`, run:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```
Must exit 0 before VERIFY_PASS may be issued.

---

## HARD-LINK SYNC

After every `.cs` edit, run:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

---

## START

Begin with **Ph1 ptt-architect**.

Provide the `02-architecture-plan.md` covering both tickets. When the plan is complete, hand
off to **Ph2 ptt-plan-reviewer** per the pipeline chain above.
