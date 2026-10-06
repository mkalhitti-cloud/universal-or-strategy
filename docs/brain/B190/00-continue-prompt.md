# B190 $continue Prompt
# Paste this entire block into a new session to resume or validate B190.

$continue | PTT-COPIER B190 | Per-Instrument Tick Defaults + Spinner Layout Fix + BE Default=0 + Deferred Closures

## Pipeline State

**IN PROGRESS -- T1..T4 VERIFY_PASS. T5 and T6 still pending.**

- Baseline commit: 9d7bfb43 (B189 T1..T5)
- B190 NOT yet committed (all changes are working-tree only -- git status shows M/untracked)
- Brain dir: docs/brain/B190/
- Spec: specs/002-trade-copier-spec.html -- B190 block

## Completed tickets (VERIFY_PASS)

| Ticket | File(s) | Change | Status |
|--------|---------|--------|--------|
| T1 | PttQuickExit.cs + TradeCopierPanel.cs | InstrumentDefaults MGC=(10,20); SetInstrument() resets _quickT1/_quickT2 + refreshes labels | VERIFY_PASS |
| T2 | TradeCopierPanel.cs | BuildInstrRow() replaced with BuildSpinnerCluster() canonical ^/v-right RepeatButton pattern | VERIFY_PASS |
| T3 | TradeCopierPanel.cs | _beBuffer = 0 (was 1) | VERIFY_PASS |
| T4 | docs/standards/jane-street/RULES_CATALOG.md | Restored JS-042..JS-110 (was truncated at JS-041) | VERIFY_PASS |

## Pending tickets

| Ticket | File(s) | Change |
|--------|---------|--------|
| T5 | specs/002-trade-copier-spec.html | Update B39 scope row: "All instruments with open positions" -> "Panel's _instrument only (Account.All)". Add closed DW-B185-BE-ALL-SCOPE-01 card. |
| T6 | src/PropTraderTools/CopyEngineTests.cs | Remove stale Skip="B180-2: method removed" from Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard (~line 8812). FlatGuardLoop exists at CopyEngine.cs:6768 -- skip is stale. Verify test passes (structural reflection only). Audit confirms Slate Gate tests at lines 8277-8441 already pass -- close DW-B183-01 in final review. |

## What to do in the new session

### Step 1 -- Run T5 (ptt-engineer)
Run the Ph4a/Ph4b cycle for T5:
- Edit specs/002-trade-copier-spec.html: find B39 scope table row containing "All instruments with open positions", change to "Panel's _instrument only (Account.All)".
- Add closed DW card for DW-B185-BE-ALL-SCOPE-01 near that section.
- Write ticket-5-completion.md and ticket-5-verification.md to docs/brain/B190/.
- Verifier confirms the B39 row text and DW card are present. No .cs changes. VERIFY_PASS.

### Step 2 -- Run T6 (ptt-engineer)
Run the Ph4a/Ph4b cycle for T6:
- Edit src/PropTraderTools/CopyEngineTests.cs: remove Skip="B180-2: method removed" from the [Fact] at ~line 8812 (Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard).
- Run dotnet test to confirm the un-skipped test passes (structural reflection -- no NT8 invoke, no Skip needed).
- Confirm Slate Gate tests at lines 8277-8441 pass (6 [Fact] + 2 [Fact(Skip=NT8-runtime)] is acceptable).
- Write ticket-6-completion.md and ticket-6-verification.md to docs/brain/B190/.
- Verifier confirms: test un-skipped, build 0E 0W, dotnet test passes. VERIFY_PASS.

### Step 3 -- Run Ph5 (ptt-plan-reviewer)
Run Ph5 final review:
- Read all 6 ticket-N-verification.md files.
- Read spec B190 block for all ACs.
- Run all 7 DNA scans across touched files.
- Confirm DW items closed: DW-B183-05, DW-B185-BE-ALL-SCOPE-01, DW-B183-02, DW-B183-01.
- Write docs/brain/B190/05-final-review.md (FINAL_PASS gate).
- Write docs/brain/B190/06-deferred-backlog.md (carry forward: DW-B183-03 P1, DW-B187-01 P3).

### Step 4 -- Commit + ptt-sync
After FINAL_PASS:
- git add -A
- git commit -m "B190-T1..T6 -- per-instrument defaults + spinner fix + BE=0 + DW closures"
- powershell -File .\scripts\ptt-sync.ps1
- Confirm: 19 linked, 0 errors.

### Step 5 -- Validate source then tell Director to compile in NinjaTrader
After ptt-sync completes and 19 linked confirmed:

**Run these source validation checks BEFORE telling the Director to compile:**

```
SCAN-01: grep -r "lock(" src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/CopyEngineTests.cs
Expected: 0 results in new/modified code.

SCAN-02: grep -rn "async void " src/PropTraderTools/TradeCopierPanel.cs
Expected: 0 results in new code.

SCAN-03: Verify _beBuffer = 0 at TradeCopierPanel.cs line 241.
Verify InstrumentDefaults MGC returns (10, 20) at PttQuickExit.cs line 392.
Verify SetInstrument() contains B190-T1 block at TradeCopierPanel.cs ~line 507.
Verify BuildSpinnerCluster() exists at TradeCopierPanel.cs ~line 1199.
Verify FlatGuardLoop test has no Skip attribute at CopyEngineTests.cs ~line 8812.

SCAN-04 (dotnet build): dotnet build src/PropTraderTools/PropTraderTools.csproj
Expected: 0 errors, 0 warnings.
```

Only after all scans pass: tell Director "B190 pipeline complete. Compile in NinjaTrader: Tools -> Edit NinjaScript -> Compile (F5)."

## NT8 compile + test instructions (after Director compiles)

**Step 1**: NinjaTrader -> Tools -> Edit NinjaScript -> Compile (F5)
Expected: 0 errors, 0 warnings.

**Step 2**: Test each fix

### T1 -- Per-instrument defaults
Open MES chart -> PTT panel:
- Expected: Quick button shows "+4", Quick2t shows "+4" (1pt = 4t on MES)
- Spin Up once: both show "+5"

Open MGC chart -> PTT panel:
- Expected: Quick button shows "+10", Quick2t shows "+10" (1pt = 10t on MGC)
- Spin Up once: both show "+11"

Quick ALL (MGC): fires T1=10t, T2=20t, T3=30t from entry (1pt/2pt/3pt)
Quick2t (MGC): fires T1=10t, T2=20t from entry (1pt/2pt)

### T2 -- Spinner layout
All 8 spinners in panel (Trim, Flatten, BE, BE ALL, Quick, Quick ALL, Quick2t, QAll2t):
- Expected: "^" top / "v" bottom arrows on the RIGHT side of the label
- No "-" or "+" buttons visible anywhere
- Hold "^" on Quick2t: increments continuously (RepeatButton auto-fire, 400ms delay then 100ms interval)

### T3 -- BE default=0
Open fresh panel:
- Expected: BE button shows "BE" (no number -- buffer=0)
- BE ALL shows "BE ALL" (no number -- already was 0, unchanged)
- Click "^" once: BE label shows "BE +1"
- Click BE with buffer=0: stop placed exactly at entry price

### T4 -- RULES_CATALOG.md (no NT8 test needed -- docs only)

### T5 -- Spec HTML (no NT8 test needed -- docs only)

### T6 -- FlatGuardLoop test (no NT8 test needed -- dotnet test confirmed in pipeline)

## Deferred items after B190 (2 open)

| ID | Priority | Description |
|----|----------|-------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen -- blocked until Lane E is next modified |
| DW-B187-01 | P3 | followerPosQty path review in ResolveFollowerTargets |

## Items closed by B190 (confirm in 05-final-review.md)

| DW ID | Closed by | Evidence |
|-------|-----------|----------|
| DW-B183-05 | T4 | RULES_CATALOG.md restored JS-042..JS-110 |
| DW-B185-BE-ALL-SCOPE-01 | T5 | B39 spec row updated |
| DW-B183-02 | T6 | FlatGuardLoop Skip removed, test passes |
| DW-B183-01 | T6 audit | Slate Gate tests confirmed present and passing |

---
*Handoff written: 2026-07-11*
*Pipeline state: T1..T4 VERIFY_PASS. T5/T6 pending.*
*Spec: specs/002-trade-copier-spec.html -- B190 block*
*Brain: docs/brain/B190/*
