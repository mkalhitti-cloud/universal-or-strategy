## B212 CONTINUE PROMPT -- REPAIR LOOP -- B212-REENTRY-BETARGET-V3

You are in **copier-spec mode** in a direct repair loop on B212-REENTRY-BETARGET.

**CONTEXT -- what has been done (do NOT re-investigate):**
- HOTFIX-B212-REENTRY-V3 (IsBeSwapInProgress + HasActivePttFlatten gates): APPLIED
- HOTFIX-B212-FLATTEN-LEADERSKIP-01: APPLIED
- HOTFIX-B212-REENTRY-BETARGET-V1: FAILED -- TryRemove in TryFirePositionState cleared counter too early
- HOTFIX-B212-REENTRY-BETARGET-V2: APPLIED -- TryRemove moved to DispatchCopy after Gate 5. Spurious re-entry still fires when copier is ON.
- HOTFIX-B212-REENTRY-BETARGET-V3: DIAG APPLIED -- Added [BETARGET-RECORD] to TryRecordBeTargetFill + [BETARGET-CLEAR] to DispatchCopy. ptt-sync 21/0.

**CONFIRMED CLUE:** Bug only reproduces when copier is ON. Copier OFF = no spurious re-entry.
This confirms the spurious [PTT-ATM-SEND] is copier-driven, not NT8-ATM-driven.
IsSuppressedReentry is returning false (beCount=0) when the re-armed Entry reaches DispatchCopy.

**CODE STATE AFTER V3:**
- TryRecordBeTargetFill (~L5186): increments _filledBeTargetCount[acct.Name] + logs
  [BETARGET-RECORD] acct= name= state= countAfter=
- IsSuppressedReentry (~L3422): reads _filledBeTargetCount, logs
  [BETARGET-GUARD] acct= name= state= beCount=
- DispatchCopy (~L2840): reads then removes _filledBeTargetCount, logs
  [BETARGET-DISPATCH] acct= name= state= beCountBefore=
  [BETARGET-CLEAR] acct= name= cleared=
- TryEvictFollowerBeSlot (~L1898): L1915 TryRemove is FOLLOWER-GATED (L1907 returns
  before it for leader Sim101). Cannot clear Sim101 counter.

**YOUR TASK:**
1. User will paste: "V3 PASSED" or "V3 FAILED + new docs/brain/output.md content"
2. If PASSED: Update NO-PIPELINE-REPAIRS.md HOTFIX-B212-REENTRY-BETARGET-V3 status
   to SIM-CONFIRMED. Tell Director: "B212 re-entry betarget confirmed fixed. V3
   diagnostics can be removed in pipeline run."
3. If FAILED: Read new output.md. Find [BETARGET-RECORD] and [BETARGET-GUARD] lines
   around the re-entry. Use the diagnosis tree below.

**DIAGNOSIS DECISION TREE (V3 FAILED):**

Q1: Do [BETARGET-RECORD] acct=Sim101 lines appear AFTER the PTT-BE-Target fills?

  NO  -> TryRecordBeTargetFill is not firing for Sim101. Diagnose:
         - Add [BETARGET-RAW] log in TryRecordBeTargetFill BEFORE guard (4):
             [BETARGET-RAW] name= acct= state=
           on every call to see what names/accounts actually arrive.
         - If name is null or has unexpected suffix: fix the StartsWith("PTT-BE-Target-") check.
         - If account is null: the account guard at L5196 is blocking the AddOrUpdate.
         FIX: widen guard OR add raw log, retest.

  YES ->

Q2: Does [BETARGET-GUARD] acct=Sim101 name=Entry appear WITH beCount > 0?

  YES (beCount > 0) -> Gate IS firing but [PTT-ATM-SEND] still appears. Diagnose:
         - IsSuppressedReentry returned true (correct) -- but ATM-SEND still fired.
         - ATM-SEND is coming from a path that does NOT call IsSuppressedReentry.
         - Grep log for [DRAIN] or [DRAIN-SUBMIT] before the spurious [PTT-ATM-SEND].
           If found: SubmitDrainedEntry or DrainThenDispatch is firing -- these have
           no _filledBeTargetCount check. Add the beCount guard there too.
         - Grep log for [PTT-LANED] before the spurious [PTT-ATM-SEND].
           TryLaneDHook fires AFTER all DispatchCopy gates -- if IsSuppressedReentry
           blocked, DispatchCopy returned early so Lane D cannot fire. Safe.

  NO (beCount = 0) -> Counter was cleared before re-armed Entry arrived. Diagnose:
         - Find [BETARGET-CLEAR] entries -- when did cleared=true fire for Sim101?
         - If [BETARGET-CLEAR] acct=Sim101 appears between fills and re-armed Entry:
           a prior DispatchCopy for Sim101 cleared the counter. Find which order.
         - If NO [BETARGET-CLEAR] for Sim101: counter was never set ([BETARGET-RECORD]
           absent for Sim101 -- this contradicts YES answer to Q1, re-check Q1).
         FIX: identify the order that triggered the spurious clear, add guard.

**KEY FILES:**
- src/PropTraderTools/CopyEngine.cs -- all repairs here
- docs/brain/output.md -- replace with new SIM log after each test run
- docs/brain/NO-PIPELINE-REPAIRS.md -- log each hotfix in the SESSION block at bottom
- scripts/ptt-sync.ps1 -- run after every .cs edit, must be 21/0

**JANE STREET GATES (mandatory before any .cs edit):**
- No lock() : grep -r "lock(" src/PropTraderTools/
- No async void : grep -rn "async void " src/PropTraderTools/
- CYC delta = 0 for diagnostic-only log additions (single straight-line statements)
- ASCII-only string literals in all new log messages

**AFTER EACH REPAIR ATTEMPT:**
Apply fix -> ptt-sync 21/0 -> log in NO-PIPELINE-REPAIRS.md with new version number ->
provide updated $continue prompt (increment V3 -> V4 etc) -> user retests -> pastes result.
