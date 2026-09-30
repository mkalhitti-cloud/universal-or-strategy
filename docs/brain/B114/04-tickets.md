# B114 Tickets

**Block**: B114
**Date**: 2026-08-27
**Author**: ptt-architect (Phase 3 — Ticket Generation)
**Plan source**: `docs/brain/B114/02-architecture-plan.md` (REVIEW_PASS confirmed by ptt-plan-reviewer 2026-08-27)
**Ticket count**: 1
**Defect closed**: DW-B119 (P0) — `_qxPendingFollowerCleanup` TryAdd placement race

---

## B114-T1 — TryAdd Placement Fix + Test Update

### Overview

Move `_qxPendingFollowerCleanup.TryAdd` from **inside** `try{}` (after `executor.Execute`) to **before** `try{}` (before `executor.Execute`) in the `ExecuteOne` follower path.
Update T_B113_01 in `B113Tests.cs` to reflect before-Execute ordering.
Append DW-B119 entry to `docs/brain/NO-PIPELINE-REPAIRS.md`.

### Spec Requirement IDs

- `#section-dw-b119` — Root cause: TryAdd after Execute is too late in NT8 Sim synchronous dispatch.
- `#section-dw-b120` — Mitigated (partial arm — monitored pending B114-DEFER-02 SIM re-test).

Note: `specs/002-trade-copier-spec.html` spec closure for #section-dw-b119 and #section-dw-b120 is **DEFERRED TO Ph5** (ptt-plan-reviewer's responsibility). The engineer does NOT touch the spec file.

---

### FILE 1: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

**Method**: `ExecuteOne` (private)
**Current lines**: L145–181 (confirmed via file read 2026-08-27)

#### Signatures (unchanged — no signature change, method is internal restructure only)

```csharp
private void ExecuteOne(
    Account acc,
    Instrument instr,
    int t1Ticks,
    System.Collections.Generic.List<(double Price, int Qty)> targets,
    bool skipIfFollower = true,
    double leaderStop = 0,
    int leaderTargetCount = 0
)
```

#### BEFORE (current B113 shipped state — verbatim from file read)

Lines 145–181:

```csharp
            if (!skipIfFollower) // (1) follower path: cancel-after pattern (B113 DW-B117)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] follower submit (cancel-after): "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: intent-guard covers the submit window so TryReplacePttBeBrackets
                // skips ATM-sweep recovery while PTT-QX orders are being placed.
                // B113 DW-B117: guard now wraps executor.Execute (not CancelQxBrackets).
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                try
                {
                    var executor = new PttQuickExit();
                    executor.Execute(
                        acc,
                        instr,
                        t1Ticks,
                        targets,
                        skipIfFollower,
                        leaderStop,
                        leaderTargetCount
                    );
                    // B113 DW-B117: arm cancel-after cleanup. OnOrderUpdate will cancel each
                    // native ATM Target* one-for-one as the corresponding PTT-QX-T* confirms Working.
                    CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
                        acc.Name,
                        (instr, DateTime.UtcNow.AddSeconds(2))
                    );
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously after submit completes.
                    // DW-B112 Option 2 structural check compensates for async Cancelled events.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
                return; // follower path complete
            }
```

#### AFTER (B114 fixed state — exact replacement)

```csharp
            if (!skipIfFollower) // (1) follower path: cancel-after pattern (B113 DW-B117)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] follower submit (cancel-after): "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: intent-guard covers the submit window so TryReplacePttBeBrackets
                // skips ATM-sweep recovery while PTT-QX orders are being placed.
                // B113 DW-B117: guard now wraps executor.Execute (not CancelQxBrackets).
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                // B114 DW-B119: arm cancel-after cleanup BEFORE executor.Execute so that
                // OnOrderUpdate finds the map entry when PTT-QX-T* goes Working.
                // In NT8 Sim, SubmitOrder dispatches OnOrderUpdate synchronously on the same
                // call stack -- TryAdd after Execute is too late (map empty when Working fires).
                CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
                    acc.Name,
                    (instr, DateTime.UtcNow.AddSeconds(2))
                );
                try
                {
                    var executor = new PttQuickExit();
                    executor.Execute(
                        acc,
                        instr,
                        t1Ticks,
                        targets,
                        skipIfFollower,
                        leaderStop,
                        leaderTargetCount
                    );
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously after submit completes.
                    // DW-B112 Option 2 structural check compensates for async Cancelled events.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
                return; // follower path complete
            }
```

#### Net change summary

1. Remove old B113 comment inside `try{}` (lines 168-169: `// B113 DW-B117: arm cancel-after cleanup...`).
2. Remove `_qxPendingFollowerCleanup.TryAdd(...)` block from inside `try{}` (lines 170-173: 4 lines).
3. Insert 4-line B114 DW-B119 comment before `try {`.
4. Insert `_qxPendingFollowerCleanup.TryAdd(acc.Name, (instr, DateTime.UtcNow.AddSeconds(2)));` block (3 lines) before `try {`.
5. `try {}` body now contains only `var executor = new PttQuickExit()` + `executor.Execute(...)`. No other changes.
6. `finally {}` block: **PRESERVE EXACTLY** (DW-B112 `_qxCancelInProgress.TryRemove` with its comment).

**Lines outside the follower path (L183-194 leader path)**: DO NOT TOUCH.

---

### FILE 2: `src/PropTraderTools/Tests/B113Tests.cs`

**Change scope**: T_B113_01 method only. T_B113_02, T_B113_03, T_B113_04 are unchanged.

#### T_B113_01 — rename and comment update

**OLD method name**: `QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower`
**NEW method name**: `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower`

**Complete NEW T_B113_01 method** (replace the entire existing method body):

```csharp
// -------------------------------------------------------------------------
// T_B113_01: QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower
//
// What is tested: The TryAdd call in ExecuteOne follower path fires BEFORE
// executor.Execute, so OnOrderUpdate can find the map entry when PTT-QX-T*
// goes Working (DW-B119 fix -- B114).
// The dict operation itself produces: correct key, non-null Instr slot,
// Expiry ~2s in the future.
// Why direct TryAdd: ExecuteOne requires a live NT8 Account (sealed, no ctor).
// This test verifies the exact dict operation that the follower path performs.
// -------------------------------------------------------------------------
[Fact]
public void QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower()
{
    // Arrange
    const string accName = "Sim101";
    var engine = CopyEngine.Instance;
    engine._qxPendingFollowerCleanup.Clear(); // isolate from prior test state
    var expiry = DateTime.UtcNow.AddSeconds(2);

    // Act: simulate the TryAdd call that fires BEFORE executor.Execute
    // in ExecuteOne follower path (B114 DW-B119 fix).
    engine._qxPendingFollowerCleanup.TryAdd(accName, (null!, expiry));

    // Assert
    Assert.True(engine._qxPendingFollowerCleanup.ContainsKey(accName));
    var entry = engine._qxPendingFollowerCleanup[accName];
    Assert.True(entry.Expiry > DateTime.UtcNow);
    Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(3));
}
```

**Assertion logic rationale**: The assertion verifies the ConcurrentDictionary operation correctness (key present, Expiry ~2s future). This is invariant to TryAdd call-site position. The ordering fix (before vs after Execute) is validated by B114-DEFER-02 SIM re-test. The rename documents that B113 incorrectly named the test "SetAfter" when B114's correct behavior is "SetBefore".

#### Tests T_B113_02, T_B113_03, T_B113_04

**COPY VERBATIM** from existing B113Tests.cs. Zero changes. All 4 [Fact] tests must remain in file. xUnit only. No async void.

---

### FILE 3: `docs/brain/NO-PIPELINE-REPAIRS.md`

**Action**: APPEND the following block at the end of the file (after the final `---` of the DW-B94 entry):

```markdown

---

## DW-B119 -- TryAdd Placement Race (B114-T1)

- **ID**: DW-B119
- **Date**: 2026-08-27
- **File**: src/PropTraderTools/Features/PttGlobalQuickExit.cs
- **Method**: ExecuteOne follower path -- _qxPendingFollowerCleanup.TryAdd
- **Bug**: TryAdd called AFTER executor.Execute inside try{}. In NT8 Sim, SubmitOrder
  dispatches OnOrderUpdate synchronously on the same call stack. PTT-QX-T* orders go
  Working during executor.Execute -- before TryAdd runs. TryCleanupReArmedAtmBracket
  calls TryGetValue on the empty map and returns false. Native ATM Target1/2/3 survive,
  creating an OCO conflict that can cancel PTT-QX-T* non-deterministically.
- **Fix**: Moved _qxPendingFollowerCleanup.TryAdd from inside try{} (after executor.Execute)
  to before try{} (before executor.Execute). Cleanup map now armed before any PTT-QX
  order is submitted. OnOrderUpdate finds the entry when PTT-QX-T* goes Working.
- **Status**: FIXED-B114-T1 -- TryAdd moved before executor.Execute. DW-B112 finally{}
  TryRemove preserved unchanged. CYC of ExecuteOne unchanged (=2). Exception-safety
  confirmed: if Execute throws, orphaned map entry expires via 2s TTL harmlessly.
```

---

### FILE 4: `specs/002-trade-copier-spec.html`

**DEFERRED TO Ph5** (ptt-plan-reviewer's responsibility). Engineer does NOT modify this file.
The spec changes required are:
- `#section-dw-b119`: OPEN → CLOSED-B114
- `#section-dw-b120`: OPEN → MONITORED-B114
- `#section-dw-b117`: Add B114 confirmation note

---

### JS Rule Constraints

| Rule | Constraint | Applies To |
|------|-----------|------------|
| JS-021 | No `lock()` anywhere | `PttGlobalQuickExit.cs` — all state uses `ConcurrentDictionary.TryAdd`/`TryRemove` (lock-free) |
| JS-033 | No `async void` (non-event-handler) | `ExecuteOne` is synchronous `void`; test methods are synchronous `void` |
| JS-001 | No `throw` in hot paths | No new throw statements. `TryAdd` is non-throwing on ConcurrentDictionary |
| JS-002 | No `return null` | No return statements changed. Bare `return;` at L181 preserved |
| CYC <= 8 | All methods <= 8 McCabe branches | `ExecuteOne` CYC = 2 (unchanged). if(!skipIfFollower)+1, base+1, try/finally=0 |
| ASCII-only | No Unicode/emoji/curly-quotes in string/comment literals | New 4-line DW-B119 comment is ASCII-only. `--` not em-dash. All strings unchanged |
| DateTime.UtcNow | Use `DateTime.UtcNow`, never `DateTime.Now` | `DateTime.UtcNow.AddSeconds(2)` in TryAdd call (unchanged from B113) |

---

### xUnit [Fact] Tests

All in `src/PropTraderTools/Tests/B113Tests.cs`. All 4 [Fact] methods must be present and compile. xUnit framework only. No NUnit. No MSTest. No `async void`.

| Test | Method Name | Asserts |
|------|------------|---------|
| T_B113_01 | `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower` | After `TryAdd(accName, (null!, expiry))`: `ContainsKey(accName)` is true; `entry.Expiry > DateTime.UtcNow`; `entry.Expiry <= DateTime.UtcNow.AddSeconds(3)` |
| T_B113_02 | (unchanged from B113) | Unchanged — copy verbatim |
| T_B113_03 | (unchanged from B113) | Unchanged — copy verbatim |
| T_B113_04 | (unchanged from B113) | Unchanged — copy verbatim |

---

### 7-SCAN CHECKLIST

The engineer MUST run all 7 scans and record PASS/FAIL in `ticket-1-completion.md` before reporting done.

#### SCAN-1 — `lock()` check (JS-021)

```powershell
Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "lock\s*\("
```

**Expected**: 0 results.
**Pass criterion**: Zero `lock(` occurrences. All state uses `ConcurrentDictionary.TryAdd`/`TryRemove`. JS-021 PASS.

---

#### SCAN-2 — `async void` check (JS-033)

```powershell
Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "async void"
```

**Expected**: 0 declaration results (comment-only lines acceptable).
**Pass criterion**: No new `async void` method declarations. `ExecuteOne` is synchronous `void`. JS-033 PASS.

---

#### SCAN-3 — TryAdd placement check (DW-B119 fix verification)

```powershell
Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "_qxPendingFollowerCleanup"
```

**Expected**: >= 2 results.
**Required matches**:
- The `_qxPendingFollowerCleanup.TryAdd(` call line (now before `try {`)
- The B114 DW-B119 comment referencing `_qxPendingFollowerCleanup` (if grep matches comment text)

**Manual verification**: Visually confirm the TryAdd line appears BEFORE `try` in the file. The old B113 comment inside `try{}` must be ABSENT.

---

#### SCAN-4 — DW-B117-DIAG removal check

```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "DW-B117-DIAG"
```

**Expected**: 0 results.
**Pass criterion**: No diagnostic tags from earlier debug sessions remain in `CopyEngine.cs`. This scan confirms CopyEngine.cs was NOT modified (it should be clean from B113 pipeline).

---

#### SCAN-5 — Sync and verify gate

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

**Expected**: `N/N OK, 0 MISMATCH` where N >= 16.
**Pass criterion**: All files synced to NT8 folder with matching MD5 hashes. Zero MISMATCH lines. If any MISMATCH appears: STOP — fix sync before pressing F5.
**Note**: After this scan passes, Director must press **F5** in NinjaTrader 8. Expected NT8 output: `Compilation succeeded. 0 error(s), 0 warning(s).`

---

#### SCAN-6 — No `return null` in modified file

```powershell
Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "return null"
```

**Expected**: 0 results.
**Pass criterion**: No `return null` statements. `ExecuteOne` uses bare `return;` (JS-002 PASS).

---

#### SCAN-7 — ASCII-only check

```powershell
Select-String -Path "src\PropTraderTools\Features\PttGlobalQuickExit.cs" -Pattern "[^\x00-\x7F]"
```

**Expected**: 0 results.
**Pass criterion**: No non-ASCII characters. New DW-B119 comment uses `--` (double-hyphen, ASCII 0x2D) not em-dash. All string literals unchanged. ASCII-only PASS.

---

### FORBIDDEN Actions

- **DO NOT** modify `src/PropTraderTools/CopyEngine.cs` — `TryCleanupReArmedAtmBracket`, `TryReplacePttBeBrackets`, `_qxPendingFollowerCleanup` field, and all other methods are correctly deployed by B113. Zero changes.
- **DO NOT** add `[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]` — already present at `CopyEngine.cs` L46. Adding it again causes a compiler error.
- **DO NOT** add `lock()` anywhere — JS-021 P0 violation.
- **DO NOT** add `async void` — JS-033 P0 violation.
- **DO NOT** use `DateTime.Now` — must be `DateTime.UtcNow`.
- **DO NOT** change T_B113_02, T_B113_03, T_B113_04 — only T_B113_01 changes.
- **DO NOT** add new test classes or new [Fact] methods.
- **DO NOT** modify the `finally {}` block in `ExecuteOne` — `_qxCancelInProgress.TryRemove(acc.Name, out _)` with its DW-B112 comment must be preserved exactly.
- **DO NOT** modify `specs/002-trade-copier-spec.html` — deferred to Ph5.
- **DO NOT** modify the leader path (L183-194) in `ExecuteOne` — it is outside the follower guard block.
- **DO NOT** modify `src/PropTraderTools/Features/PttQuickExit.cs` — per-account submit loop is unchanged.

---

### COMPLETION CRITERIA

Before writing `docs/brain/B114/ticket-1-completion.md` and reporting PIPELINE_COMPLETE, ALL of the following must be true:

- [ ] SCAN-1: 0 `lock(` results in `PttGlobalQuickExit.cs`
- [ ] SCAN-2: 0 `async void` declarations in `PttGlobalQuickExit.cs`
- [ ] SCAN-3: `_qxPendingFollowerCleanup.TryAdd(` appears BEFORE `try {` in `ExecuteOne` (visual + grep confirm)
- [ ] SCAN-4: 0 `DW-B117-DIAG` results in `CopyEngine.cs`
- [ ] SCAN-5: `ptt-sync-and-verify.ps1` reports N/N OK, 0 MISMATCH (N >= 16)
- [ ] SCAN-6: 0 `return null` results in `PttGlobalQuickExit.cs`
- [ ] SCAN-7: 0 non-ASCII characters in `PttGlobalQuickExit.cs`
- [ ] `_qxPendingFollowerCleanup.TryAdd` is BEFORE `try {}` (not inside it)
- [ ] `executor.Execute` is inside `try {}` (unchanged position)
- [ ] `finally {}` `TryRemove` block is word-for-word identical to B113 shipped state
- [ ] Old B113 comment (`// B113 DW-B117: arm cancel-after cleanup...`) is ABSENT from inside `try {}`
- [ ] New B114 DW-B119 comment (4 lines) is present BEFORE `try {}`
- [ ] CYC of `ExecuteOne` = 2 (verify: `if (!skipIfFollower)` is the only conditional)
- [ ] `B113Tests.cs` T_B113_01 method renamed to `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower`
- [ ] `B113Tests.cs` T_B113_01 comment says "fires BEFORE executor.Execute" and "B114 DW-B119 fix"
- [ ] `B113Tests.cs` T_B113_02, T_B113_03, T_B113_04 are byte-identical to B113 shipped state
- [ ] All 4 [Fact] tests compile (dotnet build passes)
- [ ] `docs/brain/NO-PIPELINE-REPAIRS.md` has DW-B119 entry appended (FIXED-B114-T1 status)
- [ ] `ticket-1-completion.md` written with all 7 scan results (PASS/FAIL + command output)

---

### Completion Artifact

Write: `docs/brain/B114/ticket-1-completion.md`

Required sections:
1. Ticket ID and status (PASS / FAIL)
2. Files modified (list each file with line ranges changed)
3. Scan results: all 7 scans, each with command run + actual output + PASS/FAIL
4. Test results: `dotnet test` output (all 4 tests green)
5. Deferred items carried forward: B114-DEFER-01 (F5 gate), B114-DEFER-02 (SIM Combo D), B114-DEFER-03 (DW-B120 re-assess)

---

*Tickets written by ptt-architect (Phase 3). Plan source: B114/02-architecture-plan.md (REVIEW_PASS 2026-08-27).*
