# B59-LaneA Orchestrator Prompt
# DW-B59-01 -- Gate 0.5 NT8 exit-name guard
# Paste the section below into a fresh ptt-orchestrator session.

---

## ORCHESTRATOR PROMPT (paste this into ptt-orchestrator)

You are `ptt-orchestrator`. Execute B59-LaneA (DW-B59-01) using the FULL 5-phase PTT pipeline.
No phases may be skipped, combined, or reordered. All phases are mandatory.

### THE PIPELINE (non-negotiable -- copy verbatim into every sub-prompt)

```
Ph1  ptt-architect       -> docs/brain/B59-LaneA/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B59-LaneA/02-plan-review.md        (REVIEW_PASS gate)
Ph3  ptt-architect       -> docs/brain/B59-LaneA/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B59-LaneA/04-ticket-review.md     (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B59-LaneA/ticket-N-completion.md
Ph4b ptt-verifier        -> docs/brain/B59-LaneA/ticket-N-verification.md  (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> docs/brain/B59-LaneA/05-final-review.md + 06-deferred-backlog.md
```

---

### MISSION BRIEF

**Block**: B59-LaneA
**Defect**: DW-B59-01 (P1)
**Title**: Leader `Name='Close'` order copied to follower -- phantom reversal bug
**Brain dir**: `docs/brain/B59-LaneA/`

#### Root cause (confirmed from live CSV log, 2026-08-10)

Gate 0.5 in `DispatchCopy` at
[`src/PropTraderTools/CopyEngine.cs:728`](src/PropTraderTools/CopyEngine.cs:728)
currently reads:

```csharp
if (order.Name != null && order.Name.StartsWith("PTT-")) return;
```

This only blocks PTT-prefixed orders. NT8's built-in exit mechanisms produce orders with
names such as `"Close"`, `"Flatten"`, `"Rev"` (reversal), and `"Exit..."` variants.
These pass Gate 0.5 today.

**Observed failure**: During live test (Sim101 -> Sim102, MES SEP26, 2026-08-10):
- Leader hit NT8 Close button -> order `Name='Close'`, `OrderAction=Sell`, `OrderState=Submitted`
- Gate 0.5 passed (not PTT-prefixed)
- Gate 3 passed (Submitted is a trigger state)
- Gate 4 passed (Market order type)
- Gate 5 passed (new orderId)
- `SendCopy` fired -> follower placed a short entry AFTER it was already flat
- Required 3 closes instead of 2 to clean up the phantom position

#### Precise fix

**Do NOT extend the Gate 0.5 `if` expression with additional `||` clauses.**
`DispatchCopy` is already annotated `// CYC=8 (at limit)` at line 722-723.
Adding branches to Gate 0.5 inline would push CYC to 9+, violating JS-CYC-8.

**Correct approach (Jane Street -- make illegal states unrepresentable):**

1. Extract a new `internal static bool IsExitSignalName(string name)` helper on `CopyEngine`.
   CYC budget: 6 (5 cases + null guard). Directly testable without NT8 runtime.

2. Replace Gate 0.5 with a single call to the helper:
   ```csharp
   // Gate 0.5: block PTT- cascade AND known NT8 exit signal names.
   if (IsExitSignalName(order.Name)) return;
   ```

3. `IsExitSignalName` implementation (canonical -- do not deviate):
   ```csharp
   // B59 T1: IsExitSignalName -- CYC=6. Returns true for names that must not trigger follower copy.
   // Covers: (1) PTT- own signals; (2) NT8 Close button; (3) NT8 Flatten; (4) NT8 Rev reversal;
   //         (5) NT8 "Exit..." prefix family. JS-001: no throw. JS-002: returns bool.
   // TESTABILITY: internal static with string param -- directly testable without NT8 runtime.
   internal static bool IsExitSignalName(string name)
   {
       if (name == null)                                              return false;
       if (name.StartsWith("PTT-",  StringComparison.Ordinal))       return true;
       if (name == "Close")                                           return true;
       if (name == "Flatten")                                         return true;
       if (name == "Rev")                                             return true;
       if (name.StartsWith("Exit", StringComparison.Ordinal))        return true;
       return false;
   }
   ```

4. Place `IsExitSignalName` immediately after `IsDispatchTriggerState` (line 718) and before
   the `// --- B7-F0` bracket mirroring comment (currently line 720).

5. Null handling: `IsExitSignalName(null)` returns `false` (pass-through), preserving the
   existing behaviour for orders with null Name (which were already passing Gate 0.5 today
   because the old guard was `order.Name != null && ...`).
   The new Gate 0.5 call does NOT need a separate null check -- IsExitSignalName handles it.

#### Tests required (B59 T1 -- 7 new [Fact] tests in CopyEngineTests.cs)

All 7 tests call `CopyEngine.IsExitSignalName(string)` directly (internal static, no reflection
needed -- same visibility pattern as `IsDispatchTriggerState` tests at line 2686).

| ID | Name | Assert |
|----|------|--------|
| T_B59_01 | `IsExitSignalName_NullName_ReturnsFalse` | `false` |
| T_B59_02 | `IsExitSignalName_PttPrefix_ReturnsTrue` | `true` for `"PTT-Copy"` |
| T_B59_03 | `IsExitSignalName_Close_ReturnsTrue` | `true` for `"Close"` |
| T_B59_04 | `IsExitSignalName_Flatten_ReturnsTrue` | `true` for `"Flatten"` |
| T_B59_05 | `IsExitSignalName_Rev_ReturnsTrue` | `true` for `"Rev"` |
| T_B59_06 | `IsExitSignalName_ExitPrefix_ReturnsTrue` | `true` for `"Exit at target"` |
| T_B59_07 | `IsExitSignalName_ArbitrarySignal_ReturnsFalse` | `false` for `"MySignal"` |

Tests go **after line 2749** (after the last `}` of `T_B55B_01_FindRule_ReturnsNull_WhenNoRules`
and before the closing `}` of the class and `}`  of the namespace at lines 2750-2751).

#### Jane Street rules in scope

- **JS-CYC-8**: CYC <= 8 per method. Extraction to helper is mandatory.
- **JS-001**: No throw in hot path. IsExitSignalName has no throw.
- **JS-002**: No return null for value type. IsExitSignalName returns bool, never null.
- **JS-021**: No lock(). Not applicable here (no state).
- **ASCII-only**: All string literals must be ASCII. "PTT-", "Close", "Flatten", "Rev", "Exit" -- all ASCII.

#### Workspace / commit rules

- Workspace: `C:\WSGTA\universal-or-strategy` (main branch only, single directory)
- After any `.cs` edit: `powershell -File .\deploy-sync.ps1`
- After deploy-sync: `powershell -File .\scripts\verify_links.ps1 -Fix`
- Commit message: `fix(ptt): B59 -- Gate 0.5 exit-name guard via IsExitSignalName [7 tests]`
- SRC CODE BAN: no `.cs` edit outside Ph4a ptt-engineer. All other phases are doc/plan only.

---

### PHASE EXECUTION CHAIN

Execute each phase as a `start_subtask` call. Wait for PASS gate before proceeding.

#### Ph1 -- ptt-architect (architecture plan)

Start a subtask in `ptt-architect` mode with this message:

```
You are ptt-architect executing Ph1 for B59-LaneA.

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
OUTPUT:    docs/brain/B59-LaneA/02-architecture-plan.md

SRC CODE BAN: produce PLAN ONLY. Do not touch any .cs file.

DEFECT: DW-B59-01 -- Gate 0.5 in DispatchCopy (CopyEngine.cs:728) does not block
NT8 built-in exit signal names ("Close", "Flatten", "Rev", "Exit...").
Leader's Close order triggers DispatchCopy -> phantom reversal on follower.

READ FIRST (in this order):
1. docs/standards/NT8_FULL_REFERENCE.md -- grep/search for "Order" name conventions and "Close" semantics.
   The file confirms: NT8 Close button produces Order.Name = "Close" (market exit, not an entry signal).
   This is the mandatory B59-prep NT8 reference read -- ground-truth before any NT8 API claim.
2. src/PropTraderTools/CopyEngine.cs lines 716-773 (DispatchCopy + IsDispatchTriggerState)
3. src/PropTraderTools/CopyEngineTests.cs lines 2682-2699 (IsDispatchTriggerState tests -- pattern to follow)
4. docs/standards/jane-street/RULES_CATALOG.md (Rules Catalog gate -- confirm no P0 violations)

RULES CATALOG GATE (mandatory, run before writing the plan):
[ ] Read docs/standards/jane-street/RULES_CATALOG.md
[ ] Confirm JS-CYC-8 applies: CYC <= 8 per method mandatory
[ ] Confirm JS-001 applies: no throw in hot path
[ ] Confirm JS-002 applies: no return null for bool return
GATE RESULT: PASS or BLOCKED(JS-XXX at file:line)

ARCHITECTURE PLAN REQUIREMENTS (write to docs/brain/B59-LaneA/02-architecture-plan.md):
1. Problem statement: why the current single-condition Gate 0.5 is insufficient
2. Solution: extract IsExitSignalName(string name) internal static helper
   - Exact method body (7 lines as specified in mission brief)
   - CYC analysis: null guard + 4 true-return branches + 1 false-return = CYC=6
   - Placement: after IsDispatchTriggerState (line 718), before bracket mirroring comment
3. Updated Gate 0.5 call site (single line: if (IsExitSignalName(order.Name)) return;)
4. Test plan: 7 [Fact] tests targeting IsExitSignalName directly (no reflection needed)
   - T_B59_01 through T_B59_07 as specified in mission brief
5. ASCII-only compliance confirmation: all new string literals are ASCII
6. CYC budget: DispatchCopy stays at 8; new helper is 6
7. Diff size estimate: ~25 lines added in CopyEngine.cs, ~80 lines added in CopyEngineTests.cs

DELIVERABLE: docs/brain/B59-LaneA/02-architecture-plan.md written and non-empty.
```

**Gate**: Ph2 ptt-plan-reviewer must emit `REVIEW_PASS` before Ph3 begins.

---

#### Ph2 -- ptt-plan-reviewer (architecture review)

Start a subtask in `ptt-plan-reviewer` mode with this message:

```
You are ptt-plan-reviewer executing Ph2 for B59-LaneA.

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
INPUT:     docs/brain/B59-LaneA/02-architecture-plan.md
OUTPUT:    docs/brain/B59-LaneA/02-plan-review.md

SRC CODE BAN: review ONLY. Do not touch any .cs file.

READ:
1. docs/brain/B59-LaneA/02-architecture-plan.md
2. src/PropTraderTools/CopyEngine.cs lines 716-773
3. docs/standards/jane-street/RULES_CATALOG.md

REVIEW CHECKLIST (write findings to docs/brain/B59-LaneA/02-plan-review.md):
[ ] IsExitSignalName is internal static (testable without reflection)
[ ] IsExitSignalName has CYC <= 8 (count: null guard + 4 branch returns + 1 terminal = CYC=6)
[ ] DispatchCopy CYC unchanged at 8 (Gate 0.5 becomes a single call, not an inline chain)
[ ] All 5 guard cases present: PTT- prefix, "Close", "Flatten", "Rev", "Exit" prefix
[ ] Null input returns false (not true -- null name should NOT block dispatch)
[ ] No throw in IsExitSignalName (JS-001)
[ ] No lock() anywhere (JS-021)
[ ] All string literals are ASCII-only
[ ] 7 test IDs match T_B59_01..T_B59_07 from mission brief
[ ] Test placement is after line 2749 (before class closing brace)
[ ] deploy-sync.ps1 is listed in commit steps
[ ] Diff estimate is <= 10,000 characters (expected ~300 chars in .cs)

GATE: end the file with exactly one of:
  REVIEW_PASS
  REVIEW_BLOCKED: <reason>
```

**Gate**: Must read `REVIEW_PASS` before Ph3 begins.

---

#### Ph3 -- ptt-architect (tickets)

Start a subtask in `ptt-architect` mode with this message:

```
You are ptt-architect executing Ph3 for B59-LaneA.

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
INPUT:     docs/brain/B59-LaneA/02-architecture-plan.md (REVIEW_PASS confirmed)
OUTPUT:    docs/brain/B59-LaneA/04-tickets.md

SRC CODE BAN: produce TICKETS ONLY. Do not touch any .cs file.

Write docs/brain/B59-LaneA/04-tickets.md with exactly 2 tickets:

TICKET B59-T1: Add IsExitSignalName helper to CopyEngine.cs
  File: src/PropTraderTools/CopyEngine.cs
  Action: INSERT new internal static method IsExitSignalName(string name) after line 718.
  Exact body (copy verbatim):

    // B59 T1: IsExitSignalName -- CYC=6. Returns true for names that must not trigger follower copy.
    // Covers: (1) PTT- own signals; (2) NT8 Close button; (3) NT8 Flatten; (4) NT8 Rev reversal;
    //         (5) NT8 "Exit..." prefix family. JS-001: no throw. JS-002: returns bool.
    // TESTABILITY: internal static with string param -- directly testable without NT8 runtime.
    internal static bool IsExitSignalName(string name)
    {
        if (name == null)                                              return false;
        if (name.StartsWith("PTT-",  StringComparison.Ordinal))       return true;
        if (name == "Close")                                           return true;
        if (name == "Flatten")                                         return true;
        if (name == "Rev")                                             return true;
        if (name.StartsWith("Exit", StringComparison.Ordinal))        return true;
        return false;
    }

  Verification: grep src/PropTraderTools/CopyEngine.cs for "IsExitSignalName" returns >= 2 hits
  (definition + Gate 0.5 call site).

TICKET B59-T2: Update Gate 0.5 in DispatchCopy + add 7 tests to CopyEngineTests.cs
  File A: src/PropTraderTools/CopyEngine.cs
  Action A: Replace line 727-728:
    OLD:
      // Gate 0.5: PTT-prefix guard -- prevents cascade copy of our own PTT- signals. CYC: 7->8.
      if (order.Name != null && order.Name.StartsWith("PTT-")) return;
    NEW:
      // Gate 0.5: block PTT- cascade AND known NT8 exit signal names (B59). CYC: 7->8 (unchanged).
      if (IsExitSignalName(order.Name)) return;

  File B: src/PropTraderTools/CopyEngineTests.cs
  Action B: INSERT 7 [Fact] tests after line 2749 (before class closing brace at line 2750).
  Exact test bodies (copy verbatim -- standard xUnit, no reflection, no NT8 types):

    // =====================================================================
    // B59 T1: IsExitSignalName -- 7 direct tests (T_B59_01 through T_B59_07)
    // DW-B59-01 -- Gate 0.5 exit-name guard.
    // TESTABILITY: internal static -- no reflection, no NT8 runtime required.
    // =====================================================================

    [Fact]
    public void T_B59_01_IsExitSignalName_NullName_ReturnsFalse()
    {
        // Null name: unknown signal -- must NOT be blocked (pass-through).
        Assert.False(CopyEngine.IsExitSignalName(null));
    }

    [Fact]
    public void T_B59_02_IsExitSignalName_PttPrefix_ReturnsTrue()
    {
        // PTT- own signal must be blocked to prevent cascade copy.
        Assert.True(CopyEngine.IsExitSignalName("PTT-Copy"));
        Assert.True(CopyEngine.IsExitSignalName("PTT-TrimLimit"));
        Assert.True(CopyEngine.IsExitSignalName("PTT-Mirror-Close"));
    }

    [Fact]
    public void T_B59_03_IsExitSignalName_Close_ReturnsTrue()
    {
        // NT8 Close button emits Name="Close" -- must be blocked (root cause of DW-B59-01).
        Assert.True(CopyEngine.IsExitSignalName("Close"));
    }

    [Fact]
    public void T_B59_04_IsExitSignalName_Flatten_ReturnsTrue()
    {
        // NT8 Flatten signal -- must be blocked.
        Assert.True(CopyEngine.IsExitSignalName("Flatten"));
    }

    [Fact]
    public void T_B59_05_IsExitSignalName_Rev_ReturnsTrue()
    {
        // NT8 Rev (reversal) signal -- must be blocked to prevent reverse-copy.
        Assert.True(CopyEngine.IsExitSignalName("Rev"));
    }

    [Fact]
    public void T_B59_06_IsExitSignalName_ExitPrefix_ReturnsTrue()
    {
        // NT8 "Exit..." prefix family -- must be blocked.
        Assert.True(CopyEngine.IsExitSignalName("Exit at target"));
        Assert.True(CopyEngine.IsExitSignalName("Exit"));
        Assert.True(CopyEngine.IsExitSignalName("ExitOnClose"));
    }

    [Fact]
    public void T_B59_07_IsExitSignalName_ArbitrarySignal_ReturnsFalse()
    {
        // Normal user-defined signal names must pass through Gate 0.5.
        Assert.False(CopyEngine.IsExitSignalName("MySignal"));
        Assert.False(CopyEngine.IsExitSignalName("MES_Long_Entry"));
        Assert.False(CopyEngine.IsExitSignalName(""));
    }

  Verification (run after both files edited):
  1. grep src/PropTraderTools/CopyEngine.cs "IsExitSignalName" -> >= 2 hits
  2. grep src/PropTraderTools/CopyEngineTests.cs "T_B59_0" -> exactly 7 hits
  3. grep src/PropTraderTools/CopyEngine.cs "order.Name != null" -> 0 hits (old Gate 0.5 gone)
  4. dotnet build (from NT8 bin dir or via deploy-sync.ps1) -> 0 errors
```

**Gate**: Ph3.5 ptt-ticket-reviewer must emit `TICKET_REVIEW_PASS` before Ph4a begins.

---

#### Ph3.5 -- ptt-ticket-reviewer (ticket review)

Start a subtask in `ptt-ticket-reviewer` mode with this message:

```
You are ptt-ticket-reviewer executing Ph3.5 for B59-LaneA.

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
INPUT:     docs/brain/B59-LaneA/04-tickets.md
OUTPUT:    docs/brain/B59-LaneA/04-ticket-review.md

SRC CODE BAN: review ONLY. Do not touch any .cs file.

READ:
1. docs/brain/B59-LaneA/04-tickets.md
2. src/PropTraderTools/CopyEngine.cs lines 716-730
3. src/PropTraderTools/CopyEngineTests.cs lines 2740-2751

TICKET REVIEW CHECKLIST:
[ ] Ticket B59-T1: exact method body matches mission brief (7 lines inside body)
[ ] Ticket B59-T1: insertion point is after line 718 (after IsDispatchTriggerState)
[ ] Ticket B59-T2 File A: OLD text is an exact match of lines 727-728 in current source
[ ] Ticket B59-T2 File A: NEW text calls IsExitSignalName(order.Name) without null check wrapper
[ ] Ticket B59-T2 File B: exactly 7 [Fact] methods, IDs T_B59_01..T_B59_07
[ ] Ticket B59-T2 File B: insertion point is after line 2749 (before closing brace of class)
[ ] No ticket touches TradeCopierPanel.cs, TradeCopierAddOn.cs, or any other .cs file
[ ] deploy-sync.ps1 listed in Ph4a commit steps
[ ] verify_links.ps1 listed in Ph4a commit steps
[ ] Commit message format: fix(ptt): B59 -- Gate 0.5 exit-name guard via IsExitSignalName [7 tests]

GATE: end the file with exactly one of:
  TICKET_REVIEW_PASS
  TICKET_REVIEW_BLOCKED: <reason>
```

**Gate**: Must read `TICKET_REVIEW_PASS` before Ph4a begins.

---

#### Ph4a -- ptt-engineer (src edits)

Start a subtask in `ptt-engineer` mode with this message:

```
You are ptt-engineer executing Ph4a for B59-LaneA (TICKET_REVIEW_PASS confirmed).

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
INPUT:     docs/brain/B59-LaneA/04-tickets.md (TICKET_REVIEW_PASS)
OUTPUT:    src edits + docs/brain/B59-LaneA/ticket-1-completion.md

THE PIPELINE (mandatory -- all phases apply, none skippable):
Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md   <-- YOU ARE HERE
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

EXECUTE IN ORDER -- DO NOT SKIP ANY STEP:

STEP 1 -- Read current source (mandatory before any edit):
  Read src/PropTraderTools/CopyEngine.cs lines 716-730
  Read src/PropTraderTools/CopyEngineTests.cs lines 2740-2751

STEP 2 -- Apply Ticket B59-T1 (INSERT IsExitSignalName after line 718):
  Use insert_content or apply_diff on src/PropTraderTools/CopyEngine.cs.
  Insert the exact method body from 04-tickets.md after line 718.
  Do NOT alter any other code. Surgical insert only.

STEP 3 -- Apply Ticket B59-T2 File A (REPLACE Gate 0.5 at lines 727-728):
  Use apply_diff on src/PropTraderTools/CopyEngine.cs.
  OLD text (exact match required):
      // Gate 0.5: PTT-prefix guard -- prevents cascade copy of our own PTT- signals. CYC: 7->8.
      if (order.Name != null && order.Name.StartsWith("PTT-")) return;
  NEW text:
      // Gate 0.5: block PTT- cascade AND known NT8 exit signal names (B59). CYC: 7->8 (unchanged).
      if (IsExitSignalName(order.Name)) return;
  Note: line numbers will have shifted by ~16 after STEP 2. Use content match, not line numbers.

STEP 4 -- Apply Ticket B59-T2 File B (INSERT 7 tests into CopyEngineTests.cs):
  Use insert_content on src/PropTraderTools/CopyEngineTests.cs.
  Insert after line 2749 (content: "        }") -- the closing brace of T_B55B_01.
  Note: file was 2751 lines; insert before the final two closing braces at 2750-2751.
  Use the exact test bodies from 04-tickets.md.

STEP 5 -- Run deploy-sync:
  execute_command: powershell -File .\deploy-sync.ps1
  If ERRORS: fix before proceeding. Do not proceed with a broken build.

STEP 6 -- Run verify_links:
  execute_command: powershell -File .\scripts\verify_links.ps1 -Fix

STEP 7 -- Grep verification (mandatory):
  grep src/PropTraderTools/CopyEngine.cs "IsExitSignalName" -> must return >= 2 lines
  grep src/PropTraderTools/CopyEngineTests.cs "T_B59_0" -> must return exactly 7 lines
  grep src/PropTraderTools/CopyEngine.cs "order.Name != null" -> must return 0 lines

STEP 8 -- Git commit:
  git add src/PropTraderTools/CopyEngine.cs src/PropTraderTools/CopyEngineTests.cs
  git commit -m "fix(ptt): B59 -- Gate 0.5 exit-name guard via IsExitSignalName [7 tests]"

STEP 9 -- Write completion artifact:
  Write docs/brain/B59-LaneA/ticket-1-completion.md with:
  - Which files were edited (CopyEngine.cs, CopyEngineTests.cs)
  - Exact line ranges modified
  - Grep verification outputs (pass/fail for each check)
  - Commit hash
  - Any deviations from the ticket (if none: "No deviations")
```

**Gate**: Ph4b ptt-verifier must emit `VERIFY_PASS` before Ph5 begins.

---

#### Ph4b -- ptt-verifier (verification)

Start a subtask in `ptt-verifier` mode with this message:

```
You are ptt-verifier executing Ph4b for B59-LaneA.

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
INPUT:     docs/brain/B59-LaneA/ticket-1-completion.md
OUTPUT:    docs/brain/B59-LaneA/ticket-1-verification.md

READ (use different tools than Ph4a used to produce them -- independent verification):
1. src/PropTraderTools/CopyEngine.cs (search for IsExitSignalName definition)
2. src/PropTraderTools/CopyEngineTests.cs (search for T_B59_0)
3. docs/brain/B59-LaneA/ticket-1-completion.md

VERIFICATION CHECKLIST (write to docs/brain/B59-LaneA/ticket-1-verification.md):

SCAN-01: IsExitSignalName definition exists
  grep src/PropTraderTools/CopyEngine.cs "internal static bool IsExitSignalName"
  PASS: returns exactly 1 hit
  FAIL: method missing or has wrong visibility

SCAN-02: IsExitSignalName called from Gate 0.5
  grep src/PropTraderTools/CopyEngine.cs "if (IsExitSignalName"
  PASS: returns exactly 1 hit

SCAN-03: Old Gate 0.5 is gone
  grep src/PropTraderTools/CopyEngine.cs "order.Name != null"
  PASS: returns 0 hits (old single-condition guard fully replaced)

SCAN-04: Null guard is first branch in IsExitSignalName
  Read IsExitSignalName body -- first if must be "if (name == null) return false;"
  PASS: null returns false (pass-through)

SCAN-05: All 5 exit name cases present in IsExitSignalName body
  Check for: "PTT-", "Close", "Flatten", "Rev", "Exit"
  PASS: all 5 present

SCAN-06: CYC <= 8 for DispatchCopy (count decision points: Gates 0.5, 3, 4, 5 + loop + 2 loop-body guards)
  Count branches in DispatchCopy: should still be 8

SCAN-07: 7 new tests present
  grep src/PropTraderTools/CopyEngineTests.cs "T_B59_0"
  PASS: exactly 7 lines returned

SCAN-08: No lock() introduced
  grep src/PropTraderTools/CopyEngine.cs "lock("
  PASS: 0 hits

SCAN-09: No throw introduced
  grep src/PropTraderTools/CopyEngine.cs "throw new"
  PASS: 0 hits in IsExitSignalName or Gate 0.5

SCAN-10: ASCII-only in new code
  Visually confirm all string literals in IsExitSignalName are ASCII

GATE: end the file with exactly one of:
  VERIFY_PASS
  VERIFY_BLOCKED: SCAN-XX failed -- <description>
```

**Gate**: Must read `VERIFY_PASS` before Ph5 begins.

---

#### Ph5 -- ptt-plan-reviewer (final review)

Start a subtask in `ptt-plan-reviewer` mode with this message:

```
You are ptt-plan-reviewer executing Ph5 (final review) for B59-LaneA.

WORKSPACE: C:\WSGTA\universal-or-strategy (main, single dir)
BRAIN DIR: docs/brain/B59-LaneA/
INPUT:     docs/brain/B59-LaneA/ticket-1-verification.md (VERIFY_PASS confirmed)
OUTPUT:    docs/brain/B59-LaneA/05-final-review.md
           docs/brain/B59-LaneA/06-deferred-backlog.md

READ:
1. docs/brain/B59-LaneA/02-architecture-plan.md
2. docs/brain/B59-LaneA/ticket-1-verification.md
3. src/PropTraderTools/CopyEngine.cs (IsExitSignalName + Gate 0.5 area)

FINAL REVIEW CHECKLIST (write to docs/brain/B59-LaneA/05-final-review.md):
[ ] DW-B59-01 closed: Gate 0.5 now blocks "Close", "Flatten", "Rev", "Exit..." in addition to "PTT-"
[ ] IsExitSignalName helper: CYC=6, internal static, directly testable
[ ] DispatchCopy CYC unchanged at 8
[ ] 7 new [Fact] tests passing (per VERIFY_PASS)
[ ] No regression to existing tests (VERIFY_PASS covered SCAN-08 lock check, SCAN-09 throw check)
[ ] deploy-sync.ps1 ran successfully (confirmed in ticket-1-completion.md)
[ ] Commit present with correct message prefix fix(ptt): B59 --
[ ] NT8_FULL_REFERENCE.md: were any NT8 API discoveries made? (yes/no -- note if yes)
[ ] carry-forward items from B58 deferred backlog: DW-B58-01, DW-B58-02, DW-B58-03 still open

Write docs/brain/B59-LaneA/06-deferred-backlog.md with:
- Any NEW deferred items discovered during B59 (if none: "No new deferred items")
- Carry-forward: DW-B58-01, DW-B58-02, DW-B58-03 (copy descriptions from B58 backlog)
- Status of DW-B54-01 (ATM auto-inject -- still open, blocked on future block)
- Status of PRE-EXISTING-01, PRE-EXISTING-02, PRE-EXISTING-03 (pre-existing, unchanged)
- Status of DW-B57-01 (CLOSED -- confirmed working in live test 2026-08-10)
- Status of DW-B59-01 (CLOSED -- fixed in this block)

DELIVERABLE: 05-final-review.md and 06-deferred-backlog.md written and non-empty.
Signal PIPELINE_COMPLETE to the orchestrator.
```

---

### ORCHESTRATOR COMPLETION CRITERIA

B59-LaneA is complete when ALL of the following are true:
- [ ] `docs/brain/B59-LaneA/02-architecture-plan.md` exists and is non-empty
- [ ] `docs/brain/B59-LaneA/02-plan-review.md` ends with `REVIEW_PASS`
- [ ] `docs/brain/B59-LaneA/04-tickets.md` exists and is non-empty
- [ ] `docs/brain/B59-LaneA/04-ticket-review.md` ends with `TICKET_REVIEW_PASS`
- [ ] `docs/brain/B59-LaneA/ticket-1-completion.md` exists with commit hash
- [ ] `docs/brain/B59-LaneA/ticket-1-verification.md` ends with `VERIFY_PASS`
- [ ] `docs/brain/B59-LaneA/05-final-review.md` exists and is non-empty
- [ ] `docs/brain/B59-LaneA/06-deferred-backlog.md` exists and is non-empty
- [ ] `src/PropTraderTools/CopyEngine.cs` contains `IsExitSignalName` (grep confirms)
- [ ] `src/PropTraderTools/CopyEngineTests.cs` contains `T_B59_01` through `T_B59_07` (grep confirms)
- [ ] git log shows commit with `fix(ptt): B59`

If any gate is BLOCKED: stop, report the blockage ID and reason to the Director.
Do not auto-fix BLOCKED gates. Wait for Director resolution.
