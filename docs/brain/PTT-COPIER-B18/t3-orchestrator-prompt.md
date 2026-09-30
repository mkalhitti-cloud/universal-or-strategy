# PTT-COPIER-B18 T3 — Orchestrator Prompt
# Mode: ptt-orchestrator
# Phases: engineer (start_subtask) → verifier (start_subtask)
# Date: 2026-07-15
# Urgency: P1 — live trading morning session

---

You are the **ptt-orchestrator** for PTT-COPIER-B18 Ticket 3.

B18 T1 and T2 are already deployed and verified. Core copy trading is working.
This is a single-ticket hot fix. Run engineer then verifier as sequential subtasks.
No architect phase. No plan review. No ticket generation. Engineer → Verifier only.

**WAVE WORKSPACE**: `c:\WSGTA\universal-or-strategy`
**NT8 ADDONS FOLDER**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\`
**BRAIN**: `c:\WSGTA\universal-or-strategy-director\docs\brain\PTT-COPIER-B18\`
**NT8 COMPILER RULES**: `c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md`

---

## Your Job

Run these two phases sequentially using `start_subtask`:

1. **start_subtask — ptt-engineer** with the engineer message below
2. Wait for engineer to complete and write `ticket-3-completion.md`
3. **start_subtask — ptt-verifier** with the verifier message below
4. Wait for verifier to complete and write `ticket-3-verification.md`
5. Report DONE to Director with build status and verify result

---

## Phase 1 — Engineer Subtask Message

Title: `PTT-COPIER-B18 T3 Engineer — Fix stuck cancel orders`

Message:

```
You are the ptt-engineer for PTT-COPIER-B18 Ticket 3.

WAVE WORKSPACE: c:\WSGTA\universal-or-strategy
NT8 ADDONS FOLDER: C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\
NT8 COMPILER RULES: c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md

FILE TO MODIFY: src/PropTraderTools/CopyEngine.cs ONLY
BANNED FILES: TradeCopierPanel.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, AtrSizingEngine.cs

DEFECT: DW-B18-CANCEL-01 (P1)

ROOT CAUSE 1 — CancelPendingEntries (around L984) only cancels OrderState.Working orders.
Follower copy orders start in OrderState.Initialized (NT8 state before sim engine acknowledgement).
PTT Cancel clicks while order is Initialized silently skip it. Order transitions to Working
with no cancel outstanding — permanently stuck in Orders tab.

ROOT CAUSE 2 — SendCopy (around L746) passes DateTime.MaxValue as the expiry argument
with TimeInForce.Day. This creates a GTC-equivalent order. NT8 sim engine cannot cleanly
cancel these when stuck in Initialized state. Even Control Center right-click Cancel All
fails. Requires sim connection reset to clear.

CHANGE 1 — CancelPendingEntries:
Find this exact block (around L984):
    if (order.OrderState != OrderState.Working)
        continue;
Replace with:
    // B18 T3: DW-B18-CANCEL-01 -- also cancel Initialized and PendingSubmit.
    // Follower copy orders start as Initialized before sim engine acknowledges them.
    // Skipping caused orders stuck as Cancel pending with no way to clear.
    if (order.OrderState != OrderState.Working &&
        order.OrderState != OrderState.Initialized &&
        order.OrderState != OrderState.PendingSubmit)
        continue;

CHANGE 2 — SendCopy:
Find this exact line inside SendCopy's follower.CreateOrder call arguments (around L746):
    DateTime.MaxValue,
Replace with:
    DateTime.Now.AddDays(1),   // B18 T3: real Day expiry -- prevents GTC-stuck sim orders

IMPORTANT: Only change the DateTime.MaxValue inside SendCopy. Do NOT change any other
DateTime.MaxValue occurrences elsewhere in the file (other methods may have their own).
Confirm by reading the file first.

STEPS:
1. Read src/PropTraderTools/CopyEngine.cs — confirm exact line numbers for both changes
2. Apply Change 1 to CancelPendingEntries
3. Apply Change 2 to SendCopy only
4. Run: dotnet build in c:\WSGTA\universal-or-strategy — must be zero errors
5. Copy DLL: copy compiled output to NT8 AddOns folder
6. Append to docs/standards/NT8_ADDON_KNOWLEDGE.md under "Testing Session (2026-07-15) ROUND 2":
   ### DW-B18-CANCEL-01 — CLOSED (B18 T3)
   CancelPendingEntries now cancels Initialized and PendingSubmit orders in addition to Working.
   SendCopy expiry changed from DateTime.MaxValue to DateTime.Now.AddDays(1).
   Follower orders no longer get stuck in Cancel pending state.
7. Write docs/brain/PTT-COPIER-B18/ticket-3-completion.md with:
   - Exact lines changed (before/after)
   - Build result (PASS/FAIL)
   - DLL copy confirmation
```

---

## Phase 2 — Verifier Subtask Message

Title: `PTT-COPIER-B18 T3 Verifier — Verify stuck cancel fix`

Message:

```
You are the ptt-verifier for PTT-COPIER-B18 Ticket 3.

WAVE WORKSPACE: c:\WSGTA\universal-or-strategy
COMPLETION REPORT: c:\WSGTA\universal-or-strategy-director\docs\brain\PTT-COPIER-B18\ticket-3-completion.md

VERIFY THESE EXACT CONDITIONS in src/PropTraderTools/CopyEngine.cs:

CHECK 1 — CancelPendingEntries filter:
PASS if the cancel guard includes OrderState.Initialized and OrderState.PendingSubmit:
    if (order.OrderState != OrderState.Working &&
        order.OrderState != OrderState.Initialized &&
        order.OrderState != OrderState.PendingSubmit)
        continue;
FAIL if it still reads: if (order.OrderState != OrderState.Working) continue;

CHECK 2 — SendCopy expiry:
PASS if SendCopy's follower.CreateOrder call uses DateTime.Now.AddDays(1) not DateTime.MaxValue.
FAIL if DateTime.MaxValue is still present inside SendCopy.

CHECK 3 — No banned files touched:
PASS if ONLY CopyEngine.cs was modified.
FAIL if TradeCopierPanel.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs, or AtrSizingEngine.cs
were touched.

CHECK 4 — Build:
PASS if ticket-3-completion.md reports BUILD_PASS with zero errors.
FAIL if any build error is reported.

CHECK 5 — Other DateTime.MaxValue occurrences:
Verify no DateTime.MaxValue was incorrectly changed in other methods (Trim, Flatten,
MirrorClose, BreakEven, TightenStop etc.). Only SendCopy should use AddDays(1).

WRITE: docs/brain/PTT-COPIER-B18/ticket-3-verification.md
Format:
  VERIFY_PASS or VERIFY_FAIL
  Check 1: PASS/FAIL + evidence
  Check 2: PASS/FAIL + evidence
  Check 3: PASS/FAIL + evidence
  Check 4: PASS/FAIL + evidence
  Check 5: PASS/FAIL + evidence

UPDATE: docs/brain/PTT-COPIER-B18/manifest.json
Set "verifier_T3": { "status": "complete" }

If VERIFY_FAIL: list exact failures for engineer to fix before Director deploys.
```

---

## Completion

After both subtasks complete, report to Director:
- Engineer result (BUILD_PASS or FAIL)
- Verifier result (VERIFY_PASS or FAIL)
- DLL deployed: yes/no
- Ready for F5: yes/no
