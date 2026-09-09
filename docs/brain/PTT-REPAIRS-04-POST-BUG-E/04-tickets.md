# PTT-REPAIRS-04-POST-BUG-E Tickets

**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Phase:** 3 -- Ticket Generation
**Author:** ptt-architect
**Date:** 2026-09-06
**Plan reviewed:** docs/brain/PTT-REPAIRS-04-POST-BUG-E/02-plan-review.md
**Plan status:** REVIEW_PASS (all 10 checklist items confirmed, 2026-09-06)

---

## TICKET-1: BUG-E -- EvictDedup Cancelled branch clears _lastLeaderDirection

**Spec requirement IDs:** PTT-REPAIRS-04-BUG-E
**Architecture plan ref:** Section 3 (fix description), Section 4 (CYC accounting), Section 5 (JS compliance table)
**File:** src/PropTraderTools/CopyEngine.cs
**Scope:** SOURCE VERIFICATION ONLY -- the fix is already applied. No .cs edits are required
unless a verification step shows the fix is absent (in which case the engineer must report to the
Director before making any edit).

---

### Background

`EvictDedup(string orderId, OrderState state)` is called from `OnOrderUpdate` for every terminal
order state (Filled, Cancelled, Rejected). The Cancelled branch clears the dedup and live-entry
guards for the cancelled orderId. Prior to the BUG-E fix, it did NOT clear `_lastLeaderDirection`.

When a dispatched entry was cancelled without a fill, `_lastLeaderDirection` retained the stale
direction for the instrument. The reversal-entry guard (`ShouldSkipForReversalGuard`) then
incorrectly blocked the first real re-entry attempt, forcing the user to click twice before
followers copied (the second attempt self-healed because `DispatchCopy` line 2555 updated the
direction unconditionally on the wasted first click).

The BUG-E fix (lines 5878-5882) clears `_lastLeaderDirection` inside the Cancelled branch
immediately after the live-entry guard removal. Fix was applied in the PTT-REPAIRS-04 session
and confirmed working via post-fix log evidence (zero PTT-COPY-GUARD skip entries).

---

### Method signatures referenced

```
EvictDedup(string orderId, OrderState state) -- CopyEngine.cs (line 5851)
    internal void
    Called from OnOrderUpdate pre-gate.
    Returns void.

_lastLeaderDirection -- CopyEngine.cs (line 369)
    private readonly ConcurrentDictionary<string, OrderAction>
    Key: instrument FullName (e.g. "MGC DEC26")
    Value: last dispatched OrderAction for that instrument

_entryInstrKeyByOrderId -- CopyEngine.cs
    private readonly ConcurrentDictionary<string, string>
    Key: orderId
    Value: instrKey in format "InstrumentFullName|Action" (e.g. "MGC DEC26|Buy")
```

---

### Jane Street rule constraints

| Rule | Method | Constraint | Classification |
|------|--------|------------|----------------|
| JS-021 | EvictDedup | No lock() anywhere in method or fix lines | ENFORCED -- PASS |
| JS-013 | EvictDedup | CYC <= 8 per method. Post-fix CYC=13 (D=12). Pre-fix CYC=12 (D=11). BUG-E adds +1 to pre-existing violation. | DEFERRED (pre-existing, see DEFERRED-4) |
| JS-042 | EvictDedup | ASCII-only in all new/changed lines (5878-5882) | ENFORCED -- PASS |
| JS-001 | EvictDedup | No throw in dispatch path. TryRemove returns false on absent key, no exception. | ENFORCED -- PASS |
| JS-025 | EvictDedup | ConcurrentDictionary.TryRemove is lock-free | ENFORCED -- PASS |

**JS-013 NOTE:** The pre-existing CYC=12 (D=11) violated JS-013 before the BUG-E fix was
written. BUG-E adds one decision token `if (pipeIdx > 0)` at line 5881, yielding CYC=13 (D=12).
BUG-E did NOT introduce the violation. DEFERRED-4 extraction is planned for a future session
but is OUT OF SCOPE for this pipeline. The DEFERRED classification is binding per Director
scoping decision.

---

### 7-scan checklist (Layer 2 engineer contract -- all 7 required, none skippable)

```
[ ] Scan 1 -- lock() scan
    Command: Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\block\s*\("
    Required result: zero matches outside comment lines.
    Failure action: report exact output to Director before any edit.

[ ] Scan 2 -- non-ASCII scan
    Command: Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]"
    Required result: zero matches.
    Failure action: report exact output to Director before any edit.

[ ] Scan 3 -- CYC check
    Action: Count all decision tokens in EvictDedup lines 5851-5895 (method start through
    Cancelled branch close brace). Decision tokens: if, &&, ||, for, foreach, while, do,
    case, catch, ternary (?). Report running D count per line and final CYC = 1 + D.
    Expected: D=12, CYC=13 (post-fix). Pre-existing pre-fix state: D=11, CYC=12.
    BUG-E addition: one decision token (if pipeIdx > 0 at line 5881).
    Confirm no unexpected decision tokens beyond the 12 enumerated in the plan branch table.

[ ] Scan 4 -- build scan
    Command: dotnet build Linting.csproj /nologo 2>&1 | Select-String "CopyEngine"
    Required result: zero CopyEngine errors. Pre-existing V12_002 warnings are acceptable.
    Failure action: report full build output to Director.

[ ] Scan 5 -- fix presence
    Action: Read CopyEngine.cs lines 5878-5882. Confirm all five lines are present verbatim:
      line 5878: // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
      line 5879: // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
      line 5880:     var pipeIdx = cancelledInstrKey.IndexOf('|');
      line 5881:     if (pipeIdx > 0)
      line 5882:         _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
    Required result: all five lines present with exact whitespace and punctuation.
    Failure action: report discrepancy to Director before any edit.

[ ] Scan 6 -- comment presence
    Action: Confirm "PTT-REPAIRS-04 BUG-E" comment is present at line 5878.
    Required result: line 5878 contains the string "PTT-REPAIRS-04 BUG-E".
    Failure action: report to Director.

[ ] Scan 7 -- hard-link sync
    Command: fsutil hardlink list "src\PropTraderTools\CopyEngine.cs"
    Required result: exactly 2 paths listed (repo path + NT8 installation path).
    Failure action: run deploy-sync.ps1 and re-check. Report result.
```

---

### Verification steps for Ph4a engineer (all 8 steps required)

**Step 1:** Read `CopyEngine.cs` lines 5845-5895 (EvictDedup full body including leading
comment block). Confirm the method signature at line 5851 is:
```csharp
internal void EvictDedup(string orderId, OrderState state)
```
Confirm the Cancelled branch (lines 5862-5884) and Filled branch (lines 5886+) are present.
Confirm the method structure matches architecture plan Section 3 (before/after code blocks).
Cite the exact line range you read.

**Step 2:** Confirm `_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)` is
present at line 5872. Cite the exact line number. Confirm this is inside the
`if (state == OrderState.Cancelled)` block opened at line 5862.

**Step 3:** Confirm lines 5878-5882 contain exactly the following (cite each line number
individually):
```
line 5878: // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.
line 5879: // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.
line 5880:     var pipeIdx = cancelledInstrKey.IndexOf('|');
line 5881:     if (pipeIdx > 0)
line 5882:         _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);
```
Confirm lines 5878-5882 are inside the `if (_entryInstrKeyByOrderId.TryRemove(...))` block
that opens at line 5872 (i.e., they are siblings of the live-entry guard block at 5875-5877,
not nested inside it).

**Step 4:** Run Scan 1 (lock() scan). Report the exact command and exact output.
Result must be zero matches outside comment lines. If any non-comment lock() match is found,
halt and report to Director.

**Step 5:** Run Scan 2 (non-ASCII scan). Report the exact command and exact output.
Result must be zero matches. If any non-ASCII match is found, halt and report to Director.

**Step 6:** Run Scan 3 (CYC check). Count every decision token in EvictDedup lines 5851-5901
(full method body). Produce a table with columns: Line, Token, Expression, D (running).
Report your final D count and CYC = 1 + D.
Expected: D=12, CYC=13. Confirm the only decision token on or after line 5878 (within the
Cancelled branch) is the `if (pipeIdx > 0)` at line 5881. Report any unexpected additions.

**Step 7:** Run Scan 4 (build scan). Report the exact command and exact output.
Result must be zero CopyEngine errors. Pre-existing V12_002 warnings are acceptable.
If any CopyEngine error appears, halt and report to Director.

**Step 8:** Run Scan 7 (hard-link check). Report the exact command and exact output of:
`fsutil hardlink list "src\PropTraderTools\CopyEngine.cs"`
Result must show exactly 2 paths. If only 1 path appears, run `powershell -File .\deploy-sync.ps1`
and re-run the fsutil check. Report the result after sync.

---

### Acceptance criteria

- All 8 verification steps completed with cited line numbers or exact command output
- BUILD_PASS declared (zero CopyEngine errors from Scan 4)
- VERIFY_READY declared (all steps pass)
- No lock() anywhere in CopyEngine.cs (Scan 1 = zero matches)
- No non-ASCII in any changed lines (Scan 2 = zero matches)
- CYC in EvictDedup is D=12, CYC=13 post-fix; JS-013 DEFERRED classification applies
- Lines 5878-5882 exactly match the verbatim content specified in Step 3
- Hard-link shows exactly 2 paths (Scan 7)
- No .cs edits made (SOURCE VERIFICATION ONLY scope)

---

### Deferred test spec (for DEFERRED-1 -- Option A test runner wiring session)

**Test name:** `EvictDedup_CancelledEntry_ClearsLastLeaderDirection`
**File:** CopyEngineTests.cs (when InternalsVisibleTo wiring is complete)
**Class:** CopyEngineTests (or equivalent xUnit test class)

**Arrange:**
```
SetLeaderDirection_ForTest("MGC DEC26", OrderAction.Buy)
IsLiveEntryBlocked_ForTest("MGC DEC26|Buy", "ord-1", 0.0)
```

**Act:**
```
EvictDedup("ord-1", OrderState.Cancelled)
```

**Assert:**
```
HasLeaderDirection_ForTest("MGC DEC26") == false
```

**Precondition:** Test helper methods `SetLeaderDirection_ForTest`,
`IsLiveEntryBlocked_ForTest`, and `HasLeaderDirection_ForTest` require InternalsVisibleTo
wiring and test-seam accessors. These are not present in the current CopyEngineTests.cs.

**Status:** DEFERRED. CopyEngineTests.cs is NOT currently executable. This test must not be
added to the test file until the Option A InternalsVisibleTo session is completed and the
helper infrastructure is wired. Adding this test before that session will produce a build error.

**What the test asserts:** After a cancelled entry for "MGC DEC26|Buy" order "ord-1",
`_lastLeaderDirection` must NOT contain a key for "MGC DEC26". This directly verifies the
BUG-E fix: the stale direction is cleared so the next entry in any direction is not
reversal-blocked.

---

## TICKETS_COMPLETE
