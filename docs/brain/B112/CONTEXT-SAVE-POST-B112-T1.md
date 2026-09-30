# Context Save — Post-B112-T1 Validation

**Date**: 2026-08-26 (live testing in progress)
**Block**: B112 | **Ticket**: T1
**Author**: ptt-copier-spec mode (session context save — anti-compaction)
**Status**: Live re-tests in progress (Combo D running first)

---

## 1. What B112-T1 Shipped (FULLY VERIFIED)

### File: `src/PropTraderTools/CopyEngine.cs`
**Method**: `CountLeaderTargets` — L3336–3363

| Change | Description | Verified |
|--------|-------------|---------|
| CHANGE 1 | `isTarget` predicate narrowed to native `Target1..9` ONLY. PTT-QX-T* and PTT-BE-Target-* branches REMOVED. Flat 5-term conjunction: `!IsNullOrEmpty`, `.Length>=7`, `.StartsWith("Target",Ordinal)`, `char.IsDigit(o.Name[6])`, `o.Name[6]!='0'` | VERIFY_PASS ITEM-01 |
| CHANGE 2 | `stateOk` = `o.OrderState == OrderState.Working` ONLY. Accepted + Submitted removed. | VERIFY_PASS ITEM-02 |
| CHANGE 3 | `return Math.Min(count, 3)` (was `return count`) | VERIFY_PASS ITEM-03 |
| CHANGE 4 | 7-line method header comment: DW-B116, Working-only, Math.Min, no PTT-prefix, ASCII-only | VERIFY_PASS ITEM-04 |

**CYC = 4** (project convention). 16/16 sync OK. All 7 scans PASS.

### File: `src/PropTraderTools/Tests/B112Tests.cs` (new file)
5 xUnit `[Fact]` tests — all confirmed present:
- `T_B112_01`: CountLeaderTargets_Returns3_WhenLeaderHas3WorkingNativeTargets
- `T_B112_02`: CountLeaderTargets_ExcludesPttBeTargetResidues
- `T_B112_03`: CountLeaderTargets_ExcludesPttQxTResidues
- `T_B112_04`: CountLeaderTargets_CapsAt3_WhenMoreThan3NativeTargets
- `T_B112_05`: CountLeaderTargets_ExcludesAcceptedAndSubmittedNativeTargets

### Pipeline gates:
- `ticket-1-verification.md` → **VERIFY_PASS** (2026-08-26)
- `05-final-review.md` → **PIPELINE_COMPLETE** (2026-08-26)
- `06-deferred-backlog.md` → written, F5 gate CLOSED
- Sync: `ptt-sync-and-verify.ps1` → 16/16 OK, 0 MISMATCH
- F5 in NT8 → Director confirmed compilation succeeded 0 errors (2026-08-26)

---

## 2. Diagnostic Probe Currently in CopyEngine.cs

**Location**: `src/PropTraderTools/CopyEngine.cs` L1230–1250

```csharp
// DW-B117-DIAG: log ATM bracket name transitions on follower accounts to confirm
// whether native ATM brackets arrive Working AFTER PTT-QX orders have been submitted.
// Diagnostic only -- no state change. Remove after root cause confirmed.
if (
    e.Order.OrderState == OrderState.Working
    && e.Order.Name != null
    && IsAtmBracketName(e.Order.Name)
    && e.Order.Account != null
    && IsFollowerAccount(e.Order.Account)
)
{
    NinjaTrader.Code.Output.Process(
        "[DW-B117-DIAG] ATM bracket Working on follower: "
            + e.Order.Account.Name
            + " name="
            + e.Order.Name
            + " instr="
            + (e.Order.Instrument?.FullName ?? "?"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

**Status**: Synced (16/16 OK). F5 compiled. Active.
**MUST BE REMOVED** as part of B113-T1 when DW-B117 fix pipeline runs.

---

## 3. Spec HTML Status (specs/002-trade-copier-spec.html)

| Section | Label | Status |
|---------|-------|--------|
| `#section-dw-b116` | `DW-B116 — P1 — CLOSED B112-T1` | ✅ Written |
| `#section-dw-b113` | `DW-B113 — P0 — CLOSED B112-T1` | ✅ Written |
| `#section-dw-b114` | `DW-B114 — P1 — RESOLVED as side-effect of DW-B116 fix (B112-T1)` | ✅ Written |
| `#section-dw-b111` | `DW-B111 — P0 — CLOSED B112-T1` | ✅ Written |
| `#section-dw-b117` | `DW-B117 — P1 — OPEN — cancel-after fix direction documented` | ✅ Written |
| Live-test table | Post-B112-T1 Combo D + Combo C rows | ❌ NOT YET — awaiting live test results |

---

## 4. Defect Status Summary

| DW | Priority | Status | Notes |
|----|----------|--------|-------|
| DW-B111 | P0 | CLOSED B112-T1 (code) | Pending Combo D live confirm |
| DW-B112 | P0 | CLOSED B111-T1 (code) | Pending Combo C live confirm |
| DW-B113 | P0 | CLOSED B112-T1 (side-effect) | Pending Combo D live confirm |
| DW-B114 | P1 | RESOLVED as side-effect B112-T1 | Monitor: if 1→3→5 reappears → DW-B114-TRACK |
| DW-B115 | P1 | OPEN — Director triage required | ATM T1 qty mismatch — not in B112 scope |
| DW-B116 | P1 | CLOSED B112-T1 (code + VERIFY_PASS) | Pending Combo D live confirm |
| DW-B117 | P1 | OPEN — root cause confirmed | Fix direction: cancel-after in OnOrderUpdate. Director approval required for B113 |

---

## 5. DW-B117 — Root Cause (confirmed by DW-B117-DIAG)

**Symptom**: After QX-ALL on fresh 3-target ATM entry, PTT-QX-T3 missing on some followers
(Sim102/Sim103 affected, Sim104 occasionally). Native Target1+Target2 remain Working alongside
PTT-QX-T1/T2 (two competing limit orders per tranche).

**Root cause**: The pre-cancel in `ExecuteOne` (`PttGlobalQuickExit.cs`) successfully cancels the
follower ATM brackets (Target1/2/3 → CancelSubmitted). NT8's ATM engine detects cancelled brackets
and re-arms. Re-arm produces Target1+Target2 Working (wave 2) but NOT Target3 (partial re-arm).
Delayed re-armed Target3 then arrives Working during the PTT-QX submit loop and conflicts with
PTT-QX-T3. NT8's OCO management cancels PTT-QX-T3.

**Why post-submit cancel is UNSAFE**: Cancelling re-armed brackets triggers another re-arm →
infinite loop (same structural failure as DW-B111). Option A as originally written: REJECTED.

**Correct fix**: Cancel-After pattern:
1. Remove pre-cancel step from `ExecuteOne` (PttGlobalQuickExit.cs) for follower path
2. Submit PTT-QX orders against live ATM brackets (coexist momentarily)
3. In `OnOrderUpdate`: when PTT-QX-T* goes Working on follower, cancel corresponding native ATM bracket
4. State flag: `_qxPendingFollowerCleanup` (ConcurrentDictionary<string,(Instrument,DateTime)>) with 2s TTL
5. Files: PttGlobalQuickExit.cs + CopyEngine.cs (field + OnOrderUpdate branch)
6. **Requires full pipeline** — Director approval needed before B113 starts

**Diagnostic log confirmed (2026-08-26 second test run)**:
- Wave 1: all 3 ATM brackets Working on all 3 followers (initial arming)
- Wave 2: Target1+Target2 only re-armed after pre-cancel (no Target3)
- Wave 3: further re-arm during Sim103/Sim104 QX sequence

---

## 6. Live Re-Tests Still Pending

### MANDATORY PREREQUISITES (every test)
1. RESTART NT8 (fresh session — zero order history — orders-for-instr < 20)
2. Press F5 → confirm "Compilation succeeded" 0 errors
3. Enter position (Sim101 master, Sim102/103/104 followers, 3-target ATM, MES SEP26)
4. Verify in Account Data: all 4 accounts identical qty + same Working order count
5. If any account differs: flatten, do NOT test, paste entry dispatch log

### COMBO D — Run FIRST (DW-B113 clean re-test)
Sequence: Enter position → QX-ALL → confirm all PTT-QX Working → BE-ALL → paste Output Tab 1

**PASS criteria**:
- NO `"partial targets=N leader=5"` log line on any follower
- NO `[BE-RETRY]` attempt loop (no "attempt 1/5" lines)
- All 4 accounts get PTT-BE-Stop-1/2/3 + PTT-BE-Target-1/2/3 Working after BE-ALL
- No unprotected position
- orders-for-instr < 20 at BE-ALL time

**FAIL / ANOMALY handling**:
- If `"partial targets=N leader=5"` still appears → DW-B116 fix not applied correctly or
  different code path also calling CountLeaderTargets incorrectly. Document as DW-B117 (additional
  root cause). Do NOT write pipeline prompt. Director decides.
- If `"attempt 1/5"` appears but without leader=5 → DW-B113 has independent root cause.
  Document as DW-B118 with exact log (targets.Count and leaderCount at trigger time). Director decides.

### COMBO C — Run SECOND (DW-B112 non-regression, fresh position)
Sequence: Enter fresh position → BE-ALL → wait 2s → confirm all 4 accounts have PTT-BE brackets →
QX-ALL → paste Output Tab 1

**PASS criteria**:
- `[BE-DIAG] TryReplacePttBeBrackets: SimXXX — PTT-QX orders Working/Submitted, skipping recovery (DW-B112)` fires for Sim102, Sim103, Sim104
- Zero "attempt 1/5" on any follower
- No unprotected position

**FAIL / ANOMALY handling**:
- If DW-B112 guard stops firing → B112-T1 may have accidentally modified TryReplacePttBeBrackets.
  Read CopyEngine.cs TryReplacePttBeBrackets (L2284–2356), confirm DW-B112 block (L2303–2324) intact.
  Document as DW-B119 regression. STOP. Director decides.

---

## 7. ON FULL PASS (both combos)

Spec updates required in `specs/002-trade-copier-spec.html`:
1. Add green live-test rows to live-test table:
   - "D — clean re-test (post B112-T1)": green PASS, date, key log lines
   - "C — non-regression (post B112-T1)": green PASS, date, key log lines
2. Confirm DW-B113 fully closed (code + live confirmed) — add "Live: CONFIRMED [date]" to closure card
3. Confirm DW-B111 fully closed — add "Live: CONFIRMED [date]" to closure card

Then report:
> "DW-B111 CLOSED. DW-B112 CLOSED. DW-B113 CLOSED. DW-B116 CLOSED.
> Copier state: Combo A ✓  Combo B ✓  Combo C ✓  Combo D ✓  Combo E ✓  Combo F ✓
> Remaining open: DW-B114 (P1, counter double-increment — monitor on clean session),
>   DW-B115 (P1, ATM T1 qty distribution — all accounts close flat, not blocking),
>   DW-B117 (P1, PTT-QX-T3 missing on followers — cancel-after fix pending Director approval).
> What is the next test or defect to address?"

---

## 8. Key File Locations

| File | Role |
|------|------|
| `specs/002-trade-copier-spec.html` | Main spec — all DW closure cards |
| `src/PropTraderTools/CopyEngine.cs` | L3336–3363 (CountLeaderTargets fix), L1230–1250 (DW-B117-DIAG probe) |
| `src/PropTraderTools/Tests/B112Tests.cs` | 5 xUnit [Fact] tests for DW-B116 |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | QX-ALL orchestration — will be modified in B113 |
| `docs/brain/B112/ticket-1-verification.md` | VERIFY_PASS |
| `docs/brain/B112/05-final-review.md` | PIPELINE_COMPLETE |
| `docs/brain/B112/06-deferred-backlog.md` | Open items + carry-forward |
| `docs/brain/DW-B117/` | DW-B117 defect brief (if created) |
| `scripts/ptt-sync-and-verify.ps1` | Sync + MD5 verify to NT8 |
| `docs/standards/jane-street/RULES_CATALOG.md` | Rules gate — read L1–30 at session start |

---

## 9. What Was NOT Done (important)

- Live-test rows NOT yet added to spec table (awaiting test results from Director)
- DW-B113 closure card says "Live re-test required" — NOT yet confirmed live
- DW-B111 closure card says "pending Combo D clean re-test" — NOT yet confirmed live
- B113 pipeline NOT started — Director approval required for DW-B117 fix
- DW-B117-DIAG probe must remain in CopyEngine.cs until B113 removes it

---

*End of context save — 2026-08-26*
