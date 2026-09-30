# B113 Post-Pipeline $continue Prompt

**Generated**: 2026-08-26 (after pipeline ran — source files externally verified)
**Sync state at generation**: 16/16 OK, 0 MISMATCH (CopyEngine.cs + PttGlobalQuickExit.cs both OK)
**Pipeline state at generation**: VERIFY_PASS + PIPELINE_COMPLETE confirmed
**Use this prompt**: Paste into a NEW copier-spec session to validate B113-T1 and guide live testing.

---

## PASTE FROM HERE (everything below the line)

---

$continue — PTT Copier Post-B113 Validation (DW-B117 Cancel-After Fix)

RULES GATE (mandatory first step):
Read docs/standards/jane-street/RULES_CATALOG.md lines 1-30.
State GATE PASS or GATE BLOCKED before any other action.

SRC CODE BAN -- ACTIVE. No .cs edits. All findings go to
specs/002-trade-copier-spec.html only.

═══════════════════════════════════════════════════════════════
PRIOR CONTEXT (what was already done before this session)
═══════════════════════════════════════════════════════════════

B113 pipeline has already run (2026-08-26). All 5 phases complete.
VERIFY_PASS and PIPELINE_COMPLETE are recorded in docs/brain/B113/.
Sync: 16/16 OK, 0 MISMATCH (confirmed at pipeline completion).
Spec #section-dw-b117 already updated to "CLOSED B113-T1" by Ph5.

Defects already CLOSED before B113:
  DW-B111 (P0) -- Infinite BE-retry storm cap -- CLOSED B111-T1, Live Combo D 2026-08-26
  DW-B112 (P0) -- QX guard window race -- CLOSED B111-T1, Live Combo C 2026-08-26
  DW-B113 (P0) -- Bracketless position after retry cap -- CLOSED B112-T1, Live Combo D 2026-08-26
  DW-B114 (P1) -- _beReplaceAttempts double-increment -- RESOLVED side-effect DW-B116, B112-T1
  DW-B116 (P1) -- CountLeaderTargets returns leader=5 for 3-target ATM -- CLOSED B112-T1, Live Combo D 2026-08-26

Defect being closed by B113:
  DW-B117 (P0) -- PTT-QX-T2/T3 missing on followers after QX-ALL
  Root cause: ExecuteOne pre-cancel triggered NT8 ATM re-arm racing with PTT-QX-T2/T3
  Fix: Cancel-After pattern -- submit PTT-QX first, cancel native ATM brackets
  one-for-one in OnOrderUpdate as each PTT-QX-T* confirms Working.

═══════════════════════════════════════════════════════════════
WHAT B113-T1 SHIPPED (exact source locations to verify)
═══════════════════════════════════════════════════════════════

CHANGE 1 -- PttGlobalQuickExit.cs ExecuteOne (follower path)
  File: src/PropTraderTools/Features/PttGlobalQuickExit.cs
  Location: L127-194 (ExecuteOne full body)
  CancelQxBrackets call: REMOVED
  _qxCancelInProgress.TryAdd: at L155, BEFORE executor.Execute
  executor.Execute: at L158-167
  _qxPendingFollowerCleanup.TryAdd: at L170-173, AFTER executor.Execute
  _qxCancelInProgress.TryRemove: in finally block at L179, AFTER executor.Execute
  [PTT-QX-GUARD] log line: at L147-151, still present
  CYC: 2 (follower guard(1) + leader execute(2))

CHANGE 2 -- CopyEngine.cs new field _qxPendingFollowerCleanup
  File: src/PropTraderTools/CopyEngine.cs
  Location: L270-277
  Type: ConcurrentDictionary<string, (Instrument Instr, DateTime Expiry)>
  Initialized at declaration. No lock. JS-021 compliant.

CHANGE 3 -- CopyEngine.cs OnOrderUpdate cancel-after dispatch
  File: src/PropTraderTools/CopyEngine.cs
  Location: L1243-1246
  Call: TryCleanupReArmedAtmBracket(e) -- extracted helper
  [DW-B117-DIAG] probe: ABSENT (zero grep results)

CHANGE 3 helper -- CopyEngine.cs TryCleanupReArmedAtmBracket
  File: src/PropTraderTools/CopyEngine.cs
  Location: L2382-2444
  Condition: PTT-QX-T* Working + IsFollowerAccount + TryGetValue + TTL + instrument match
  Name index: e.Order.Name[8] ('1'/'2'/'3') -> "Target1"/"Target2"/"Target3"
  Cancel: acc.CancelOrder(toCancel)
  Log: "[PTT-QX-CLEANUP] accName cancelled TargetN (cancel-after DW-B117)"
  Removal policy: TryRemove when tChar=='3' OR TTL elapsed
  CYC: 5 (outer guard(1) + foreach(2) + if found(3) + if shouldRemove(4) + ... = 5)

CHANGE 4 (REMOVE-PROBE) -- DW-B117-DIAG block
  File: src/PropTraderTools/CopyEngine.cs
  Location: former L1230-1250 -- ENTIRELY REMOVED
  docs/brain/NO-PIPELINE-REPAIRS.md: DW-B117-DIAG status updated to REMOVED-B113-T1

Test file: src/PropTraderTools/Tests/B113Tests.cs
  4 [Fact] tests: T_B113_01..T_B113_04
  Framework: xUnit only. No NUnit. No MSTest. No async void.

TryReplacePttBeBrackets: UNCHANGED (DW-B112 guard intact at ~L2308-2360)

═══════════════════════════════════════════════════════════════
STEP 1 -- CODE REVIEW (read from source, verify each item)
═══════════════════════════════════════════════════════════════

Read src/PropTraderTools/Features/PttGlobalQuickExit.cs L127-194.
State PRESENT / ABSENT / DEVIATION with exact line for each:

  [ ] CancelQxBrackets call ABSENT from the !skipIfFollower block
  [ ] _qxCancelInProgress.TryAdd present BEFORE executor.Execute (expect ~L155)
  [ ] executor.Execute called inside try block (~L158-167)
  [ ] _qxPendingFollowerCleanup.TryAdd called AFTER executor.Execute (~L170-173)
  [ ] _qxCancelInProgress.TryRemove in finally block AFTER executor.Execute (~L179)
  [ ] [PTT-QX-GUARD] log line present (~L147-151)
  [ ] No lock() anywhere in ExecuteOne
  [ ] No async void in file
  [ ] CYC = 2 (manual count: if(!skipIfFollower) branch = 1, leader execute = 2)

Read src/PropTraderTools/CopyEngine.cs:
  Field region (L270-277):
  [ ] _qxPendingFollowerCleanup field present
  [ ] Type: ConcurrentDictionary<string, (Instrument Instr, DateTime Expiry)>
  [ ] Initialized inline at declaration (new ConcurrentDictionary<...>())
  [ ] No lock() near field

  OnOrderUpdate region (~L1243-1246):
  [ ] TryCleanupReArmedAtmBracket(e) call present
  [ ] [DW-B117-DIAG] text ABSENT from this region
  [ ] Run: Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "DW-B117-DIAG"
      Must return 0 results

  TryCleanupReArmedAtmBracket helper (L2382-2444):
  [ ] Method present at ~L2382
  [ ] Condition uses e.Order.Name[8] for digit check (not Name[7] or Name[9])
  [ ] nativeName = "Target" + tChar (maps '1'->'Target1', '2'->'Target2', '3'->'Target3')
  [ ] acc.CancelOrder(toCancel) present
  [ ] [PTT-QX-CLEANUP] log line present
  [ ] Removal policy: tChar=='3' OR TTL elapsed -> TryRemove
  [ ] No lock() anywhere in method
  [ ] CYC = 5 confirmed (outer guard(1) + foreach(2) + if found(3) + if shouldRemove(4))

  TryReplacePttBeBrackets (~L2308-2360):
  [ ] DW-B112 guard UNCHANGED (structural PTT-QX presence check still intact)

Read src/PropTraderTools/Tests/B113Tests.cs -- confirm all 4 tests by exact name:
  [ ] QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower  (T_B113_01)
  [ ] QxPendingFollowerCleanup_NotSet_ForLeader                (T_B113_02)
  [ ] QxPendingFollowerCleanup_ClearedAfterTtl                 (T_B113_03)
  [ ] CancelAfter_TargetIndexMapping                           (T_B113_04)
  [ ] All use [Fact] only (no [Theory], no [Test], no [TestMethod])
  [ ] using Xunit; present, no NUnit/MSTest import
  [ ] No async void test methods

Read docs/brain/B113/ticket-1-verification.md -- confirm: VERIFY_PASS
Read docs/brain/B113/05-final-review.md -- confirm: PIPELINE_COMPLETE
Read docs/brain/NO-PIPELINE-REPAIRS.md lines 11-20 -- confirm:
  DW-B117-DIAG status = REMOVED-B113-T1

═══════════════════════════════════════════════════════════════
STEP 2 -- JANE STREET COMPLIANCE SCAN
═══════════════════════════════════════════════════════════════

Run each scan, paste result, state PASS or FAIL:

  SCAN-A: Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "lock\s*\("
    Expected: 0 executable lock() matches

  SCAN-B: Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\("
    Expected: all matches are in comment text only (no executable lock statements)

  SCAN-C: Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "DW-B117-DIAG"
    Expected: 0 results (probe removed)

  SCAN-D: Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "_qxPendingFollowerCleanup"
    Expected: >= 4 results (field declaration + TryAdd + TryGetValue + TryRemove)

  SCAN-E: Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "_qxPendingFollowerCleanup"
    Expected: >= 1 result (TryAdd call in ExecuteOne follower path)

  SCAN-F: Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "async void"
    Expected: 0 results

  SCAN-G: CYC check -- manual count for TryCleanupReArmedAtmBracket:
    Count decision points: outer-if(1) + foreach(2) + if(toCancel!=null)(3) + if(shouldRemove)(4)
    Expected: CYC = 4 or 5, within budget (<= 8)

═══════════════════════════════════════════════════════════════
STEP 3 -- SYNC AND DEPLOY STATUS
═══════════════════════════════════════════════════════════════

Run: powershell -File scripts\ptt-sync-and-verify.ps1
Expected: N/N OK, 0 MISMATCH (N >= 16)

Report:
  [ ] CopyEngine.cs: OK
  [ ] PttGlobalQuickExit.cs: OK
  [ ] Total file count and 0 MISMATCH

If 0 MISMATCH: say "DEPLOYED -- ready for F5 compilation in NT8."
If any MISMATCH: say "SYNC FAILED -- do not F5 until mismatch resolved." List the mismatched files.

═══════════════════════════════════════════════════════════════
STEP 4 -- SPEC VERIFICATION
═══════════════════════════════════════════════════════════════

Read specs/002-trade-copier-spec.html #section-dw-b117.
Confirm:
  [ ] Section label contains "CLOSED B113-T1"
  [ ] Green closure card describes Cancel-After pattern
  [ ] T_B113_01..T_B113_04 referenced
  [ ] Live re-test pending note present

If any of the above is absent: add the missing content now (spec edits are allowed).

═══════════════════════════════════════════════════════════════
STEP 5 -- LIVE RE-TEST INSTRUCTIONS
═══════════════════════════════════════════════════════════════

When code review PASSES and sync shows 0 MISMATCH, say:

"B113-T1 code review PASSED. Sync 0 MISMATCH -- deployed to NT8.
Ready for F5 compilation and live re-test.

F5 GATE (mandatory before any live test):
  Press F5 in NinjaTrader 8 (Tools -> Edit NinjaScript -> Compile).
  Must show: 'Compilation succeeded' with 0 errors.
  If [DW-B117-DIAG] lines appear in ANY subsequent log output: STOP --
  the probe was not removed or a second copy exists. Report as DW-B120. Do not test.

ENTRY PREREQUISITE:
  Enter position -- STOP and verify in Account Data before pressing anything:
    All 4 accounts must show identical quantity.
    All 4 accounts must show same number of Working orders.
    If any account differs: flatten, do NOT test, paste the entry dispatch log.

COMBO D (DW-B117 fix verification -- run FIRST):
  Enter position (Sim101 master, Sim102/103/104 followers, 3-target ATM, MES SEP26)
  -> QX-ALL
  -> Wait 3 seconds for all order events to settle
  -> Paste full Output Tab 1 log

COMBO C (DW-B112 non-regression -- run SECOND, fresh position):
  Enter position -> BE-ALL -> wait 2 seconds
  -> Confirm all 4 accounts have PTT-BE-Stop-1/2/3 + PTT-BE-Target-1/2/3 Working
  -> QX-ALL
  -> Paste full Output Tab 1 log"

═══════════════════════════════════════════════════════════════
PASS CRITERIA
═══════════════════════════════════════════════════════════════

COMBO D pass (DW-B117 fix confirmed):
  NEW expected log lines (cancel-after working):
  ✓ [PTT-QX-CLEANUP] Sim102 cancelled Target1 (cancel-after DW-B117)
  ✓ [PTT-QX-CLEANUP] Sim102 cancelled Target2 (cancel-after DW-B117)
  ✓ [PTT-QX-CLEANUP] Sim102 cancelled Target3 (cancel-after DW-B117)
  ✓ Same 3 lines for Sim103 and Sim104 (9 total cleanup log lines)
  ACCOUNT DATA after QX-ALL settles:
  ✓ All 3 followers show PTT-QX-T1, PTT-QX-T2, PTT-QX-T3 all Working
  ✓ No native Target1/Target2/Target3 remaining Working on any follower
  ✓ No unprotected position on any account
  ✓ ABSENT: [DW-B117-DIAG] lines (probe removed)
  ✓ ABSENT: "PTT-QX-T2 Cancelled" or "PTT-QX-T3 Cancelled" in log

COMBO C pass (DW-B112 non-regression):
  ✓ "[BE-DIAG] TryReplacePttBeBrackets: SimXXX -- PTT-QX orders Working/Submitted,
    skipping recovery (DW-B112)" fires for Sim102, Sim103, Sim104
  ✓ Zero "attempt 1/5" lines on any follower
  ✓ No unprotected position
  ✓ All 3 followers show PTT-QX-T1/T2/T3 Working after QX-ALL
  ✓ ABSENT: [DW-B117-DIAG] lines

═══════════════════════════════════════════════════════════════
ON FULL PASS (both combos)
═══════════════════════════════════════════════════════════════

Update specs/002-trade-copier-spec.html:
  #section-dw-b117:
    Amend closure card to add: "Live CONFIRMED Combo D [date]: [PTT-QX-CLEANUP]
    lines present for all 3 followers (9 total). All followers PTT-QX-T1/T2/T3
    Working. No native Target* remaining. No unprotected position. DW-B117 fully closed."
    Update section label to include "Live CONFIRMED [date]".
  Live-test table: add two green rows:
    "D -- clean re-test (post B113-T1)": green PASS, date, key log lines
    "C -- non-regression (post B113-T1)": green PASS, date

Then say:
"DW-B117 CLOSED. QX-ALL cancel-after fix live confirmed.
Copier state:
  Combo A v  Combo B v  Combo C v  Combo D v  Combo E v  Combo F v
Remaining open:
  DW-B115 (P1 -- ATM T1 qty distribution mismatch -- Director triage required)
  DW-B114-TRACK (P1 monitor -- if 1->3->5 counter pattern reappears on clean session)
What is the next test or defect to address?"

═══════════════════════════════════════════════════════════════
ON ANOMALY
═══════════════════════════════════════════════════════════════

PTT-QX-T2 or T3 still Cancelled after fix:
  If [PTT-QX-CLEANUP] DID fire for that account:
    -> Cancel-after fired but re-arm occurred again after cleanup (TTL too short,
       or NT8 re-arms a second time). Document as DW-B120 with exact log timing
       showing gap between CLEANUP log and T2/T3 Cancelled event. Director decides.
  If [PTT-QX-CLEANUP] did NOT fire for that account:
    -> TryCleanupReArmedAtmBracket condition not matched. Check:
       a. Is the PTT-QX-T* order Name exactly "PTT-QX-T1" etc (Name[8] = digit)?
       b. Did _qxPendingFollowerCleanup.TryAdd actually fire (check [PTT-QX-GUARD] log)?
       c. Did TTL expire before the PTT-QX-T* went Working (2s window too tight)?
    Document as DW-B121 with exact order name from log and timing. Director decides.

[DW-B117-DIAG] lines appear after F5:
  -> Probe not removed or second copy exists in OnOrderUpdate.
  -> Read CopyEngine.cs full OnOrderUpdate body. Locate the remaining DIAG block.
  -> Document as DW-B120-PROBE. Do not test further. Director decides.

DW-B112 guard stops firing in Combo C:
  -> Read CopyEngine.cs TryReplacePttBeBrackets (~L2308-2360).
  -> Confirm the structural PTT-QX presence check is intact.
  -> Document as DW-B122 regression. STOP. Director decides.

[PTT-QX-CLEANUP] fires but native bracket not found (no cancel):
  -> This is acceptable if the native bracket was already cancelled or filled.
  -> If PTT-QX-T* is Working AND native Target* is also still Working after the log line:
     that is a bug -- document as DW-B123. Director decides.

═══════════════════════════════════════════════════════════════
SOURCE FILES (read-only for validation)
═══════════════════════════════════════════════════════════════
  src/PropTraderTools/Features/PttGlobalQuickExit.cs   L127-194 (ExecuteOne)
  src/PropTraderTools/CopyEngine.cs                    L270-277 (field)
  src/PropTraderTools/CopyEngine.cs                    L1243-1246 (OnOrderUpdate dispatch)
  src/PropTraderTools/CopyEngine.cs                    L2382-2444 (TryCleanupReArmedAtmBracket)
  src/PropTraderTools/Tests/B113Tests.cs               (4 Fact tests)
  docs/brain/B113/ticket-1-verification.md
  docs/brain/B113/05-final-review.md
  docs/brain/NO-PIPELINE-REPAIRS.md                   (DW-B117-DIAG entry)

SPEC FILE (edits allowed):
  specs/002-trade-copier-spec.html
