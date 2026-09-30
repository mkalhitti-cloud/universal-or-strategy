# defect-report.md — B41-LaneA

**Epic**: PTT-COPIER B41 — Quick Exit: Per-Instrument Bracket Swap
**Validator**: ptt-validator (B41-LaneA)
**Date**: 2026-08-05
**Signal**: BLOCKED
**Defect Count**: 1

---

## VERDICT: BLOCKED

One check failed. Do NOT merge or deploy. Engineer must fix and re-submit
ticket-N-completion.md for re-verification.

---

## Defect D-01 — V08 FAIL: RefreshQuickDisplay has 0 call sites

**Severity**: HIGH (method is dead code — Card A display update never runs)

### What Was Required

The validation checklist (V08) required:
- `RefreshQuickDisplay` method defined in `TradeCopierPanel.cs` ✅
- At least 3 call sites: `OnOrderUpdate`, `OnPositionUpdate`, and panel attach ❌

The engineer's completion report (Section 5 — STEP 5) stated:

> "Wired: OnOrderUpdate, OnPositionUpdate, panel attach call sites"

### What Was Actually Found

**Independent grep of `TradeCopierPanel.cs` for `RefreshQuickDisplay(`**:

```
Command: Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs"
         -Pattern "RefreshQuickDisplay\(" | Select-Object LineNumber, Line

Results:
  LineNumber  Line
  ----------  ----
  1404        private void RefreshQuickDisplay(Account acc, Instrument instr)
```

**Only the definition** is present. Zero invocations found.

**Independent grep for `OnOrderUpdate` and `OnPositionUpdate` in the same file**:

```
Command: Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs"
         -Pattern "OnOrderUpdate|OnPositionUpdate|RefreshQuick" | Select-Object LineNumber, Line

Results (relevant):
  1401  // B41: RefreshQuickDisplay -- Card A: back-calc actual T1 ticks from live PTT-QX-T1 order.
  1404  private void RefreshQuickDisplay(Account acc, Instrument instr)
  1436  // JS-002: returns null if none (null is valid sentinel, used in RefreshQuickDisplay null guard).
```

`OnOrderUpdate` and `OnPositionUpdate` **do not appear in the file at all**. The panel does not
subscribe to NT8 order/position events that would trigger the display refresh.

### Impact

- `_quickBtn` label never updates from spinner default to live tick count.
- Card A ("back-calc actual T1 ticks from live PTT-QX-T1 order") is functionally dead.
- Tests T_B41_15 and T_B41_16 test the method body in isolation but do not verify it is
  ever actually called from the panel lifecycle.

### Fix Required

The engineer must add `OnOrderUpdate` and `OnPositionUpdate` subscriptions in the panel
lifecycle (or equivalent NT8 callbacks), and call `RefreshQuickDisplay` from each.

Minimum required call sites per the spec:
1. `OnOrderUpdate` handler (or `_engine.OrderUpdate` delegate) — when a PTT-QX-* order fills/changes state
2. `OnPositionUpdate` handler — when position changes on the leader account
3. Panel attach / `OnLoaded` — initial display sync after panel wires up

**File to fix**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

**Affected line region**: Around line 1404 (the method definition). The call sites should be
added to the appropriate NT8 event handlers in the same file.

---

## All Other Checks — PASS

| Check | Result | Evidence |
|-------|--------|---------|
| V01 — PttQuickExit.cs hard-linked | ✅ PASS | `verify_links.ps1` → OK, hard-linked |
| V02 — PttGlobalQuickExit.cs hard-linked | ✅ PASS | `verify_links.ps1` → OK, hard-linked |
| V03 — QuickExitEventArgs 7 fields | ✅ PASS | PttContracts.cs lines 211-240: Instrument, EntryPrice, T1Price, T2Price, IsLong, OcoId, TickSize (all present) |
| V04 — PttBus.QuickExitFired + RaiseQuickExit | ✅ PASS | PttContracts.cs line 117 (event) + line 143 (RaiseQuickExit method) |
| V05 — cancelPttQx param + PTT-QX- filter | ✅ PASS | CopyEngine.cs line 1780 (param) + line 1788 (filter clause) |
| V06 — Build tag PTT-COPIER B41 | ✅ PASS | CopyEngine.cs line 41: `"PTT-COPIER B41 \| quick-exit \| 2026-08-05"` |
| V07 — QuickT1Ticks, QuickT2Ticks, QuickT3Ticks | ✅ PASS | CopyEngine.cs lines 196-198 (CopyRule struct) |
| **V08 — RefreshQuickDisplay 3+ call sites** | ❌ **FAIL** | 0 call sites found — method defined at line 1404 but never invoked |
| V09 — QuickExitFired sub + unsub | ✅ PASS | TradeCopierWindow.cs line 129 (+=) + line 145 (-=) |
| V10 — dotnet build 0 new errors | ✅ PASS | 2 errors (pre-existing AtrSizingEngine, confirmed baseline) |
| V11 — 234 [Fact], T_B41_01–T_B41_17 | ✅ PASS | 234 [Fact] in CopyEngineTests.cs; T_B41_01 at line 4141, T_B41_17 at line 4321 |
| V12 — P0 scans | ✅ PASS | lock=0, async void=0, return null=0, volatile double=0 (comment lines only) |
| V13 — verify_links.ps1 OK=14 | ✅ PASS | DESYNC=0, MISSING=0, FIXED=0, SKIPPED=1 (CopyEngineTests.cs) |

---

## Re-verification Instructions

After the engineer fixes D-01:
1. Add `OnOrderUpdate` / `OnPositionUpdate` call sites for `RefreshQuickDisplay`
2. Re-run `verify_links.ps1 -Fix` to confirm hard-link sync still intact
3. Re-run `dotnet build` to confirm 0 new errors
4. Re-confirm [Fact] count is still >= 231
5. Update `ticket-N-completion.md` to reflect the fix
6. Validator re-runs V08 grep and V10/V11 checks only (all others already GREEN)

**Signal**: BLOCKED — PTT-COPIER B41 | quick-exit | 2026-08-05
