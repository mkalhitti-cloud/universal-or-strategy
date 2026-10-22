# B215 Lane C -- Architecture Plan
## Phase 1 -- ptt-architect
## Block: B215-LaneC
## Epic: DW-B209-04 -- Spec HTML Update (B209 OCO + GTC guard features + B211/B212/B213/B214 Lane B)
## Date: 2026
## Status: PLAN_COMPLETE

---

## 0. Rules Catalog Gate (mandatory non-skippable)

**Rules file read**: `docs/standards/jane-street/RULES_CATALOG.md` -- UTF-8 clean. Readable.

**Task scope**: HTML-only. Zero .cs files. Zero .csproj files. Zero test files.

**P0 rule applicability**:

| Rule | Applicable to HTML? | Result |
|------|--------------------|----|
| JS-021 (lock ban) | NO -- no C# code produced | N/A |
| JS-033 (async void) | NO -- no C# code produced | N/A |
| JS-001 (throw ban) | NO -- no C# code produced | N/A |
| JS-002 (return null) | NO -- no C# code produced | N/A |
| ASCII-only mandate | YES -- all text content | ENFORCED: plain ASCII only; no curly quotes, no Unicode dashes; HTML entities (&nbsp;, &middot;, &rarr;) permitted for structure |

**GATE RESULT: PASS** -- Zero P0 violations possible for this HTML-only lane. ASCII mandate enforced in all new content.

---

## 1. Investigation Summary

### 1.1 Spec File Structure

File: `specs/002-trade-copier-spec.html`
Total lines: 44,340

**Document structure**:
- Lines 1-8: DOCTYPE, html, head (charset, viewport, title, mermaid script)
- Lines 9-400: CSS style block (variables, typography, cards, tables, flow diagrams, rule badges)
- Lines 400+: body content -- sequential block sections

**CSS class reference (used by new sections)**:

| Class | Purpose |
|-------|---------|
| `.section` | Outer block wrapper div, each major spec block |
| `.section-label` | Monospace uppercase label with horizontal rule separator |
| `.card` | Content card: surface background, border, padding |
| `.card-title` | Card header: mono, uppercase, small |
| `.pill-green` | Green status badge (PIPELINE_COMPLETE) |
| `.pill-amber` | Amber status badge (PARTIAL, IN-PROGRESS) |
| `.pill-red` | Red status badge (CRITICAL, BUG) |

**Existing section pattern** (from B208 at line 43542):
```html
<!-- == B208 block ======================================================= -->
<div class="section" id="section-b208">
  <div class="section-label">Block 208 &nbsp;&middot;&nbsp; PTT-COPIER-B208 &nbsp;&middot;&nbsp; [title]</div>
  <div class="card">
    <div class="card-title">...</div>
    <p>...</p>
    <h4 style="margin:12px 0 6px;color:#e2e8f0;">Sub-heading</h4>
    ...
  </div>
</div>
<!-- end B208 block -->
```

**Inline style conventions** (matching existing):
- Sub-headings: `style="margin:12px 0 6px;color:#e2e8f0;"`
- Body text color: `color:var(--mid)` or `color:#e2e8f0`
- Amber highlight: `color:var(--amber)` or `style="color:#f59e0b;"`
- Green status: `color:var(--green)`
- Code references: `<code>MethodName()</code>`
- Diff blocks: `<pre style="background:var(--raised);border:1px solid var(--border);border-radius:4px;padding:10px;font-size:12px;overflow-x:auto;margin:8px 0;">`

### 1.2 Insertion Point

```
Line 44333: (content inside B208 last card)
Line 44334:         <!-- end B208 SIM Round 6 diagnosis -->
Line 44335:       </div>         <-- closes B208 content div
Line 44336:     </div>           <-- closes section div for B208
Line 44337:     <!-- end B208 block -->
Line 44338: (blank)
Line 44339:   </body>
Line 44340: </html>
```

**New content insertion**: Insert the new block between line 44337 (after `<!-- end B208 block -->`) and line 44339 (`</body>`). The new block is a sibling section to B208 at the same nesting level inside `<body>`.

### 1.3 Deferred Item Status

DW-B209-04 confirmed OPEN in `docs/brain/B215/06-deferred-backlog.md` (line 45):
> "specs/002-trade-copier-spec.html B209 block updates. Spec HTML not updated for B209 OCO + GTC guard features. Must be updated before production SIM R8."

**This lane closes DW-B209-04.**

---

## 2. Sequential Thinking Summary (8 thoughts completed)

1. **Feature inventory**: 6 FGs mapped from NO-PIPELINE-REPAIRS.md and B214 final review. All source facts verified from primary sources (no speculation).
2. **JS pre-check**: HTML-only. No C# code produced. P0 rules N/A. ASCII mandate enforced.
3. **CYC pre-check**: N/A for HTML. Insertion point confirmed at line 44337.
4. **Threading model**: N/A for HTML. ASCII entity usage clarified -- HTML entities (&nbsp;, &middot;) permitted; plain hyphens in text content.
5. **Data flow**: 6 cards, one per FG, sequential in the new section block. Content mapped from source investigation.
6. **File split validation**: Single pipeline. One HTML file. Zero cross-group dependencies. LC1 ticket chosen.
7. **NT8 API verification**: N/A (no C# produced). Source fact accuracy verified from primary documents.
8. **Pre-flight**: No violations. Plan ready. Insertion confirmed at line 44337.

---

## 3. Lane-Split Gate

| Question | Answer | Reason |
|----------|--------|--------|
| Q1. Same method or within 50 lines? | NO | Different HTML sections; FGs are independent content blocks |
| Q2. Fix B depends on Fix A final design? | NO | Each FG is a standalone HTML section with no ordering dependency |
| Q3. Each FG has standalone value? | YES | Each documents a distinct completed and pipeline-verified feature |
| Q4. Each has independent verification path? | YES | Each FG has its own section heading, content, and git-traceable source |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE (Q1=NO, Q2=NO, Q3=YES, Q4=YES -- lanes approved by gate logic; single ticket chosen because all edits are in one HTML file with zero inter-group dependencies -- no .cs files touched).**

---

## 4. Component List

| Component | Type | Change |
|-----------|------|--------|
| `specs/002-trade-copier-spec.html` | HTML spec file | ADD new block (lines after 44337) |

**No other files are touched by this lane. Zero .cs files. Zero test files. Zero .csproj files.**

---

## 5. Ticket LC1 -- Full Specification

### Ticket ID: LC1
### Title: DW-B209-04 -- Spec HTML Update (B209/B211/B212/B213/B214 Features)
### File: `specs/002-trade-copier-spec.html`
### Spec Requirement: DW-B209-04 (OPEN in docs/brain/B215/06-deferred-backlog.md)
### Method signatures: N/A (HTML-only -- no methods implemented)

---

### 5.1 Insertion Location

Insert the new HTML block between:
- Line 44337: `    <!-- end B208 block -->`
- Line 44339: `  </body>`

The new content is a `<div class="section" id="section-b209-b214">` block at the same nesting level as the B208 section. It is inserted directly after the `<!-- end B208 block -->` comment, before `</body>`.

---

### 5.2 New Section: Outer Wrapper

```html
    <!-- == B209-B214 features block ====================================== -->
    <div class="section" id="section-b209-b214">
      <div class="section-label">Blocks B209-B214 &nbsp;&middot;&nbsp; PTT-COPIER-B209-B214 &nbsp;&middot;&nbsp; OCO Guard, GTC Guard, Ask/Bid Buttons, Re-entry Suppression, Multi-Chart Fix, UI Range</div>

      [6 cards -- one per FG -- follow here]

    </div>
    <!-- end B209-B214 block -->
```

---

### 5.3 FG-1 Card: B209 OCO Guard

**Section heading text**: "FG-1 -- B209 OCO Guard - BuildBeOcoId Collision Fix (HOTFIX-BUG-BE-OCO-REUSE)"

**Status pill**: `pill-green` -- "PIPELINE-COMPLETE (B209)"

**Card content -- key bullet points**:
- **Problem**: `BuildBeOcoId` used `accName.Substring(0, 4)` as the OCO prefix component. Both `"Sim101"` and `"Sim102"` produced the same prefix `"Sim1"`. With the same `seq` value, both accounts generated identical OCO IDs (e.g., `"PTT-BE-Sim1-00001-0"`). NT8 rejected the second `CreateOrder` call: `"The OCO ID cannot be reused"`. All Sim102 BE bracket orders were rejected -- only Sim101 was protected.
- **Fix**: Changed prefix to `accName.Substring(0, 8)` (or full name if shorter than 8 chars). `"Sim101"` -> prefix `"Sim101"`, `"Sim102"` -> prefix `"Sim102"`. No collision possible between any two distinct account names.
- **Method affected**: `BuildBeOcoId` in `PttBreakEven.cs` (~line 331)
- **Diff (minimal)**:
  - BEFORE: `string prefix = accName.Length >= 4 ? accName.Substring(0, 4) : accName;`
  - AFTER:  `string prefix = accName.Length >= 8 ? accName.Substring(0, 8) : accName;`
- **JS compliance**: No lock(), no throw, no return null; CYC unchanged (one-char change in ternary)
- **Tests added**: T_OCO_ID_01 (Sim101 != Sim102 for same seq/pair), T_OCO_ID_02 (same seq + pair + different acc -> unique), T_OCO_ID_03 (short account name < 8 chars -> full name used)

**Insertion sub-location**: First card in the B209-B214 section block.

---

### 5.4 FG-2 Card: B209 GTC Guard

**Section heading text**: "FG-2 -- B209 GTC Guard - Entry Drag Dedup Fix (HOTFIX-ENTRY-DRAG-DEDUP)"

**Status pill**: `pill-green` -- "PIPELINE-COMPLETE (B209)"

**Card content -- key bullet points**:
- **Problem**: `HandleEntryChange` called `_dedupCache.TryRemove(orderId, out _)` after a drag-price update. NT8 then fired a second `OnOrderUpdate` event for the same leader order (Accepted -> Working state transition). At that point, Gate C's `TryGetValue` returned false (key was removed), the drag-price delta check was never reached, and the code fell through to `DispatchCopy`. A second `PTT-Copy` order was placed on the follower -- doubling follower contracts. Note: HOTFIX-DW-B64-01 had added `_dedupCache[orderId] = currentPrice` before calling `HandleEntryChange` to survive the TryRemove, but `HandleEntryChange` then immediately removed the key again, undoing that fix for the Working-state event.
- **Fix**: Replaced `_dedupCache.TryRemove(orderId, out _)` with `_dedupCache[orderId] = newPrice` (upsert, keep key alive). The second Working-state event hits Gate C, finds the stored price, computes `Math.Abs(newPrice - newPrice) = 0 < tickSize`, and returns without calling `DispatchCopy`. `DispatchCopy`'s own `IsDedup` check also independently blocks it.
- **Method affected**: `HandleEntryChange` in `CopyEngine.cs` (~line 1140)
- **Diff (minimal)**:
  - BEFORE: `_dedupCache.TryRemove(leaderOrder.OrderId.ToString(), out _);`
  - AFTER:  `_dedupCache[leaderOrder.OrderId.ToString()] = newPrice;`
- **JS compliance**: No lock() (`_dedupCache` is `ConcurrentDictionary`, indexer write is lock-free); no throw; no return null; CYC unchanged (one-for-one line replacement)
- **Tests added**: T_DRAG_DEDUP_02 (Working-state re-entry after drag -- Gate C sees delta=0 -> DispatchCopy NOT called), T_DRAG_DEDUP_03 (orderId key still present after HandleEntryChange at newPrice), T_DRAG_DEDUP_04 (new follower order on cancel+resubmit path -- new orderId keyed correctly)

**Insertion sub-location**: Second card in the B209-B214 section block.

---

### 5.5 FG-3 Card: B211 Ask/Bid/Ask-All/Bid-All Buttons

**Section heading text**: "FG-3 -- B211 Ask/Bid/Ask-All/Bid-All Buttons"

**Status pill**: `pill-green` -- "PIPELINE-COMPLETE (B214-LaneA)"

**Card content -- key bullet points**:

Sub-section A: Ask (single account) - HOTFIX-B211-ASK-01
- **Problem**: Ask button did nothing -- `TryFindWorkingTarget` required `PTT-` prefix but Clone-mode targets are named `Target1`..`Target9` (native NT8 ATM names, no PTT prefix). `firstTarget` was always null.
- **Fix**: Widened name filter to also accept names starting with `Target` (1..9), covering native NT8 ATM targets alongside PTT-prefixed ones.
- **Method affected**: `TryFindWorkingTarget` in `TradeCopierPanel.cs` (~line 1462 area)

Sub-section B: Ask-All and Bid-All - HOTFIX-B211-ASKALL-01, HOTFIX-B211-BIDALL-01
- **Problem**: Ask-ALL did nothing -- `OnAskAllClick` routed through `PttMoveTargetAll.Execute` -> `AllAccounts(instrument)` -> `FindRule(instrument)` -> returns null when copier is OFF or no rule loaded. Bid-ALL had the same root cause.
- **Fix**: Rewrote `OnAskAllClick` and `OnBidAllClick` to bypass `AllAccounts`/`FindRule` entirely. Direct pattern: resolve `_leaderAccount` via `TryResolveLeaderAccount()`, then `foreach (var item in _followerItems)` with `item.IsSelected` guard, calling `PttMoveTargetOne.Execute` per account.
- **Key design decision**: NOT using `AllAccounts` or `FindRule`. Direct `_leaderAccount` + `_followerItems` loop is the canonical pattern for Ask-ALL/Bid-ALL. This is correct when copier is OFF because no CopyRule lookup is needed -- the buttons operate on any open position regardless of copier state.
- **Methods affected**: `OnAskAllClick`, `OnBidAllClick` in `TradeCopierPanel.cs`
- **JS compliance**: No lock(); no throw; no return null; CYC=4 per method (acceptable); ASCII-only

Sub-section C: Multi-target (HOTFIX-B211-MOVETARGET-01)
- Ask/Bid buttons only moved the last found target when two working targets existed. `TryFindWorkingTarget` had last-write semantics (single Order ref).
- Fix: `CollectWorkingTargets(acc, instr)` returns `Order[]` via two-pass count+fill (no `List<>` to avoid NT8 NinjaScript CS0246 import issue). Batch cancel+resubmit all found targets.

**Insertion sub-location**: Third card in the B209-B214 section block.

---

### 5.6 FG-4 Card: B212 Re-entry Suppression Guards

**Section heading text**: "FG-4 -- B212 Multi-Layer Follower Re-entry Suppression"

**Status pill**: `pill-green` -- "PIPELINE-COMPLETE (B214-LaneA)"

**Card content -- key bullet points**:

Sub-section A: Root cause
- Copier ON + BE-ALL press: `PttBreakEvenSwap` cancels native ATM brackets. NT8 ATM detects bracket cancellation -> re-arms a new Entry on the leader. This Entry flows through `OnOrderUpdate` -> Gate 1 (copy enabled) -> Gate 2 (leader account) -> `DispatchCopy`. Without guards, DispatchCopy copies the re-armed Entry to all followers = spurious re-entry.
- Similarly: Flatten-ALL -> `PTT-Flatten` fills -> NT8 ATM re-arms Entry -> copied to followers.
- Also: After all BE targets fill (leader position flat) -> NT8 ATM re-arms Entry -> leader and follower re-entries if not guarded.

Sub-section B: Guard Layer 1 -- HOTFIX-B212-REENTRY-V3 (BE-ALL + Flatten-ALL window)
- Added `IsBeSwapInProgress(order.Account?.Name)` gate in `DispatchCopy` -- blocks re-armed Entry during bracket-cancel window (~50-200ms after BE-ALL). `IsBeSwapInProgress` is `ConcurrentDictionary` lookup (lock-free, O(1)).
- Added `HasActivePttFlatten(order.Account, order.Instrument)` gate in `DispatchCopy` -- scans `acc.Orders.ToList()` for `PTT-Flatten` in Submitted/Accepted/Working/PartFilled state. Blocks re-armed Entry while flatten is in-flight.
- Design rationale: leader must always receive its BE bracket (keep all PttBeSwapAll paths intact). Copier must not forward the NT8 ATM re-arm artifact to followers. These are orthogonal concerns -- `DispatchCopy` is the correct single choke point.

Sub-section C: Guard Layer 2 -- HOTFIX-B212-REENTRY-BETARGET V4 (after target fill)
- `_beSwapInProgress` clears when `PTT-BE-Stop-N` reaches Working/Cancelled (~50-200ms). Target fills happen seconds/minutes later. A different sentinel is required.
- Added `HasFilledBeTargetFast(order.Account)` check in `IsSuppressedReentry` (third guard). Returns true if `_filledBeTargetCount` (ConcurrentDictionary, lock-free) is non-zero for the account.
- Cleared via `_filledBeTargetCount.TryRemove(leader)` in `DispatchCopy` after all gates pass (confirmed legitimate new entry). This is the correct clear timing -- the re-armed Entry is blocked; the next legitimate new trade clears the counter.
- Six-site audit of all `SendCopyWithAtm` call sites confirmed. `ReplaceFollowerCopyOnAtmCancel` was the only site NOT guarded by `DispatchCopy` -- extracted `IsReplaceOnAtmCancelBlocked` helper and added `HasFilledBeTargetFast(leader)` check there (V4).

Sub-section D: Guard Layer 3 -- HOTFIX-B212-REENTRY-FOLLOWER-V1/V2 (follower drain race + Lane E)
- After BE-All, followers can re-enter via two paths not covered by `IsSuppressedReentry`: (1) stale drain parked during pre-BE drag (`SubmitDrainedEntry`), (2) Lane E (`IsLaneEEligible`) when `_laneEFlattenBlocked` is not set.
- V1 Fix 1: `HasFilledBeTargetFast(follower)` guard added in `SubmitDrainedEntry` after `IsBeSwapInProgress` check. `SubmitDrainedEntry` CYC 7->8 (at ceiling, PASS).
- V1 Fix 2: `HasFilledBeTargetFast(follower)` guard added in `DrainThenDispatch` fast-path after `IsFlattenInFlightOrRecent` check.
- V2 Fix 3 (belt-and-suspenders): `HasFilledBeTargetFast(acc)` gate (5b) added in `IsLaneEEligible`. Prerequisite: CYC extraction from 11 to 7 (four helpers extracted: `IsLaneEFlattenLatched`, `IsPositionOrInstrumentNull`, `IsFollowerSlateNotClosed`, `IsLaneEBlockedByFlattenOrLeader`). After gate (5b) added: CYC=8 (at ceiling, PASS).
- Covers case where followers close via BE-targets only (no FlattenOneAccount, so `_laneEFlattenBlocked` is NOT set).

**Insertion sub-location**: Fourth card in the B209-B214 section block.

---

### 5.7 FG-5 Card: B213 Multi-Chart Instrument Fix

**Section heading text**: "FG-5 -- B213 Multi-Chart Instrument Fix (HOTFIX-B213-MULTI-CHART-INSTRUMENT-01)"

**Status pill**: `pill-green` -- "PIPELINE-COMPLETE (B214-LaneA)"

**Card content -- key bullet points**:
- **Problem**: When two ChartTrader windows are open for different instruments (e.g., MES + MGC), standalone buttons (BE-All, Flatten, Move Target, Quick Exit) on the second window silently did nothing. Copier OFF. Only one window's buttons responded.
- **Root cause**: `TryResolveLeaderAccount()` returned null immediately when `_accountCombo == null`. `_accountCombo` is set by `WireAccountCombo()`, called by `WireLeaderAccount()` in `DoInject()`. `WireLeaderAccount()` calls `FindAccountComboBox(chartTrader)` -- if the second ChartTrader visual tree was not fully laid out at inject time (race condition), `FindAccountComboBox` returned null AND the `FindVisualChildByIndex` fallback also returned null -> `WireLeaderAccount` returned early without calling `WireAccountCombo` -> `_accountCombo` stayed null forever.
- All button handlers use `_leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); if (_leaderAccount == null) return;`. With `_accountCombo` null -> `TryResolveLeaderAccount()` -> null -> silent no-op.
- **Fix**: Extended `TryResolveLeaderAccount()` with a lazy re-wire path in the `_accountCombo == null` branch. When `_accountCombo` is null, attempts to find and wire the Account ComboBox from `_currentChart` (via `FindVisualChild<ChartTrader>` + `FindAccountComboBox`) before proceeding with resolution. Calls `WireAccountCombo(combo)` if found.
- **Method affected**: `TryResolveLeaderAccount` in `TradeCopierPanel.cs`
- **CYC delta**: 4 -> 6 (under ceiling of 8, PASS)
- **Threading**: UI-thread-only -- called from WPF Click handlers. `FindVisualChild`, `FindAccountComboBox`, `WireAccountCombo` are all UI-thread operations. No threading concern.
- **JS compliance**: No lock(); no async void; no heap allocation on hot path (button click = infrequent); CYC=6; ASCII-only
- **SIM confirmed**: Both ChartTrader windows (MES + MGC) buttons fire correctly on their respective instruments with copier OFF.

**Insertion sub-location**: Fifth card in the B209-B214 section block.

---

### 5.8 FG-6 Card: B214 Lane B UI Changes

**Section heading text**: "FG-6 -- B214 Lane B UI Changes (Label Casing, Tick Suffix, Buffer Cap)"

**Status pill**: `pill-green` -- "PIPELINE-COMPLETE (B214-LaneB)"

**Card content -- key bullet points**:

Sub-section A: UI-1 -- Button Label Casing
- "ALL" changed to "All" at 7 callsites in `TradeCopierPanel.cs` (lines 1048, 1059, 1131, 1170, 1187, 1521, 1578).
- Examples: "Be All", "Ask-All", "Bid-All", "Flatten All", "Quick Exit All".
- No functional change -- display text only.

Sub-section B: UI-2/UI-3 -- Buffer Tick Suffix Display
- Buffer numbers now show a "t" tick suffix (e.g., "+5t", "-3t").
- Zero buffer shows the label name only (e.g., "BE All" not "BE All +0t"). Zero suppression guard.
- Implemented in `FormatBuffer` and `FormatGlobalBeBuffer` (pure static helpers in `TradeCopierPanel.cs`).
- `FormatBuffer(string name, int ticks)`:
  - ticks == 0 -> return name (label only)
  - ticks > 0 -> return `name + " +" + ticks + "t"`
  - ticks < 0 -> return `name + " " + ticks + "t"` (ticks already negative)
- `FormatGlobalBeBuffer`: same pattern applied to global BE buffer display.

Sub-section C: UI-4/UI-5 -- BE All Buffer Cap
- BE All buffer ceiling raised from 10 to 50 ticks. Floor raised from -10 to -50 ticks.
- `IncrementBuffer`: gate changed from `< 10` to `< 50` in `PttGlobalBreakEven.cs` (line 18).
- `DecrementBuffer`: gate changed from `> -10` to `> -50` in `PttGlobalBreakEven.cs` (line 25).
- `InitBeSlider`: slider max changed from 10 to 50, min from -10 to -50.
- Allows fine-tuning of BE buffer over a 100-tick range (-50 to +50) instead of 20-tick range.
- **JS compliance**: No lock(); no throw; no return null; CYC unchanged (value change only); ASCII-only.

**Insertion sub-location**: Sixth card in the B209-B214 section block.

---

## 6. 7-Scan Checklist (HTML-Adapted) -- Engineer Contract

The ptt-engineer MUST complete all 7 scans before VERIFY_PASS may be issued.

| Scan | Rule | Check | Pass Criteria |
|------|------|-------|---------------|
| SCAN-1 | File scope | `git diff --name-only` | Must show ONLY `specs/002-trade-copier-spec.html`. Any additional file = SCAN-1 FAIL. |
| SCAN-2 | Test baseline | `dotnet test` | Passed/Failed counts unchanged from pre-LC1 baseline. 24 pre-existing failures carry forward. Zero new failures introduced. |
| SCAN-3 | N/A | HTML file -- no C# lock() patterns | Not applicable. Mark N/A in verification report. |
| SCAN-4 | N/A | HTML file -- no async void | Not applicable. Mark N/A in verification report. |
| SCAN-5 | N/A | HTML file -- no return null | Not applicable. Mark N/A in verification report. |
| SCAN-6 | HTML structure | Manual tag closure check | All new `<div>`, `<h4>`, `<p>`, `<pre>`, `<table>`, `<ul>`, `<ol>` tags properly closed. No unclosed tags introduced. |
| SCAN-7 | ASCII-only | `grep -P "[\x80-\xFF]" specs/002-trade-copier-spec.html` (new sections only) | Must return 0 results. Any non-ASCII byte in new content = SCAN-7 FAIL. |

---

## 7. Data Flow (Documentation-Only Lane)

This lane produces documentation, not executable code. There is no runtime data flow.

**Document flow**:
```
NO-PIPELINE-REPAIRS.md (source facts)
        |
        v
B214/05-final-review-lane-b.md (UI changes source)
        |
        v
specs/002-trade-copier-spec.html (output -- new section added after line 44337)
```

**File modified**: `specs/002-trade-copier-spec.html` (one file, one insertion point)

---

## 8. NT8 API Surface

Not applicable. This lane produces zero C# code. No NT8 API methods are called. The HTML documentation describes NT8 API behavior but does not invoke any NT8 API directly.

---

## 9. Threading Model

Not applicable. HTML file. No Dispatcher.InvokeAsync. No ConcurrentQueue. No threading concerns.

---

## 10. Section K -- Deferred Work

### Items CLOSED by LC1

| ID | Item | Closure Evidence |
|----|------|-----------------|
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | CLOSED by LC1. New HTML block documents all 6 FGs (B209/B211/B212/B213/B214 Lane B features). Insertion after line 44337. SCAN-1 through SCAN-7 pass required. |

### Items Carrying Forward (all other open items unchanged)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | B215-LaneA (separate) | OPEN -- carry-forward |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions (manual NT8 project update) | P1 | Manual | OPEN -- carry-forward |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (manual NT8 project creation) | P1 | Manual | OPEN -- carry-forward |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions (manual NT8 update) | P1 | Manual | OPEN -- carry-forward |
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B216+ | OPEN -- carry-forward |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background) | P2 | B216+ | OPEN -- carry-forward |
| DW-B211-01 | Direction-aware validation for Ask/Bid | P2 | Future block | OPEN -- carry-forward |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block | OPEN -- carry-forward |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain vs deprecate decision) | P2 | B216+ | OPEN -- carry-forward |

**Note**: LC1 is HTML-only. It introduces no new deferred items. DW-B214-LB-01/02/03 remain manual (gitignored test files in NT8 project folder -- agent tools cannot write). They are unaffected by LC1.

---

## Summary

| Gate | Result |
|------|--------|
| Rules Catalog Gate (P0 check) | PASS -- HTML-only lane; C# rules N/A; ASCII mandate enforced |
| Sequential thinking (8 thoughts) | COMPLETE |
| Feature inventory (6 FGs from source) | COMPLETE -- all facts verified from primary documents |
| Insertion point confirmed | Line 44337 (after end B208 block comment, before </body>) |
| File scope | 1 file: specs/002-trade-copier-spec.html |
| .cs files touched | ZERO |
| Test files touched | ZERO |
| Lane-split gate | SINGLE-PIPELINE |
| 7-scan checklist | COMPLETE (HTML-adapted) |
| Section K | PRESENT -- DW-B209-04 closed; 9 items carry forward |

---

## PLAN_COMPLETE
