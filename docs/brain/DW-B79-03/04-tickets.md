# DW-B79-03 Tickets
# QX Conflict Guard: Pre-Cancel Follower ATM Brackets in PttGlobalQuickExit.ExecuteOne

**Status**: TICKETS_COMPLETE
**Epic**: DW-B79-03
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect
**Plan source**: docs/brain/DW-B79-03/02-architecture-plan.md (REVIEW_PASS)
**Reviewer sign-off**: docs/brain/DW-B79-03/02-plan-review.md (REVIEW_PASS, zero violations)
**Date**: 2026-08-10

---

## TICKET-1 — DW-B79-03 QX Conflict Guard (PttGlobalQuickExit.cs)

### Spec Req IDs

- DW-B79-03 (follower ATM bracket conflict on QX-ALL → BE-ALL rapid sequence)
- AC-2 (follower accounts receive PTT-QX brackets without NT8 sim conflict)

### Files to Change

| File | Change | Scope |
|------|--------|-------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | MODIFY | Add 2-line guard in `ExecuteOne` + update XML doc |
| `src/PropTraderTools/Tests/B79Tests.cs` | CREATE | New xUnit [Fact] tests — minimum 2, recommended 3 |

### Implementation Specification

**Context — why this change is needed:**

`PttGlobalQuickExit.Execute()` calls `ExecuteOne(follower, ..., skipIfFollower: false)` for
each follower account. The follower's ATM brackets (Stop1/Target1 etc.) may not yet be
visible in `acc.Orders` at QX-ALL fire time due to NT8's async bracket propagation lag
(~1-3ms, documented NT8_FULL_REFERENCE.md). When `PttQuickExit.Execute` then runs its
own `BuildQxSnapshot` / `CancelQxBrackets(snapshot)`, it sees an empty snapshot
(brackets have not arrived) and submits PTT-QX orders to an account that will shortly
receive conflicting Working ATM brackets. NT8 sim detects the conflict and cancels the
PTT-QX orders. A subsequent BE-ALL press then finds only `CancelSubmitted` PTT-QX targets
(excluded from `SnapshotTargetsLocal.stateOk`) → `targets.Count == 0` → bare-stop path.

**Direction A — chosen fix (cancel first, then submit):**

In `ExecuteOne`, immediately before constructing `PttQuickExit`, add:

```
if (!skipIfFollower)
    CopyEngine.Instance?.CancelQxBrackets(acc, instr);
```

This mirrors the leader's natural behavior: leader ATM brackets are always Working at
QX-ALL fire time and are cancelled by `PttQuickExit.Execute`'s own `BuildQxSnapshot /
CancelQxBrackets` path. For followers, this explicit pre-cancel fires first, puts all
bracket orders into `CancelSubmitted`, and then `PttQuickExit.Execute` runs against a
clean follower account (snapshot=0, internal cancel is no-op, PTT-QX submit sees no
conflict).

**Pseudocode for modified `ExecuteOne`:**

```
private void ExecuteOne(acc, instr, t1Ticks, targets, skipIfFollower=true, leaderStop=0, leaderTargetCount=0)
    // DW-B79-03 guard: pre-cancel follower ATM + PTT-* brackets BEFORE PttQuickExit snapshot.
    // Only on follower path (skipIfFollower=false). Leader path unchanged.
    IF NOT skipIfFollower:                                       // (branch 1)
        CopyEngine.Instance?.CancelQxBrackets(acc, instr)       // 2-param overload, CopyEngine.cs:586
    // Unchanged delegation:
    executor = new PttQuickExit()
    executor.Execute(acc, instr, t1Ticks, targets, skipIfFollower, leaderStop, leaderTargetCount)
```

**What `CancelQxBrackets(acc, instr)` does (2-param overload, CopyEngine.cs:586):**
- Iterates `acc.Orders` for `instr`
- Includes states: `Working | Initialized | Accepted | Submitted | TriggerPending`
- Name filter: `IsQxCancelCandidate(o)` covers `Stop1..Stop9`, `Target1..Target9`,
  `PTT-QX-*`, `PTT-BE-*`, `PTT-Copy*`
- Calls `acc.Cancel(stale.ToArray())` — silent catch on exception, no throw
- If `stale.Count == 0` (no brackets yet) → returns immediately (no-op)

**After the pre-cancel call:**
- Follower brackets enter `CancelSubmitted`
- `BuildQxSnapshot` stateOk does NOT include `CancelSubmitted` → snapshot=0
- `CancelQxBrackets(snapshot)` (3-param, called inside `PttQuickExit.Execute`) → no-op
- `Submit PTT-QX-Stop/T1..TN` → follower account is clean, NT8 sim sees no conflict

**Leader path invariant (MUST NOT change):**
- `ExecuteOne(leader, ..., skipIfFollower=true)` → `if (!skipIfFollower)` is false → guard skipped
- Leader's ATM brackets are always Working and cancelled by `PttQuickExit.Execute`'s own path
- Zero behavioral change on leader path

**Log output to add inside guard (ASCII-only):**

```csharp
NinjaTrader.Code.Output.Process(
    "[PTT-QX-GUARD] pre-cancel follower brackets: " + acc.Name + " " + instr.FullName,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

(Add this INSIDE the `if (!skipIfFollower)` block, before the `CancelQxBrackets` call.)

**Full updated XML doc for `ExecuteOne`:**

```xml
/// <summary>
/// ExecuteOne: per-account Quick Exit bracket swap.
/// HOTFIX-QUICK-T3-01: accepts targets snapshot for N-bracket submission.
/// B78 DW-B63-01: leaderStop + leaderTargetCount forwarded to PttQuickExit.Execute.
/// DW-B79-03: pre-cancel follower ATM+PTT-* brackets BEFORE constructing PttQuickExit
///   so the follower account is clean when PttQuickExit.Execute runs its own cancel step.
///   Mirrors the leader path: cancel first, then submit PTT-QX.
///   Only fires on the follower path (skipIfFollower=false).
///   Leader path (skipIfFollower=true) unchanged -- leader's own ATM brackets are
///   always Working and cancelled by PttQuickExit.Execute's internal snapshot logic.
/// CYC=2: follower guard(1) + delegate(2).
/// JS-021: no lock. JS-001: no throw. JS-002: void. JS-033: synchronous void. ASCII-only.
/// </summary>
```

### Method Signatures

All signatures are unchanged from current source. No new public methods.

| Method | File | Signature (unchanged) | CYC Before | CYC After |
|--------|------|-----------------------|------------|-----------|
| `ExecuteOne` | PttGlobalQuickExit.cs:92 | `private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, List<(double Price, int Qty)> targets, bool skipIfFollower = true, double leaderStop = 0, int leaderTargetCount = 0)` | 1 | 2 |
| `Execute` | PttGlobalQuickExit.cs:32 | `internal void Execute()` | 8 | 8 (unchanged) |
| `CancelQxBrackets` (called, not modified) | CopyEngine.cs:586 | `internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)` | 6 | 6 (unchanged) |

### CYC Budget

| Method | Before | After | Budget | Result |
|--------|--------|-------|--------|--------|
| `PttGlobalQuickExit.Execute` | 8 | 8 | <= 8 | PASS |
| `PttGlobalQuickExit.ExecuteOne` | 1 | 2 | <= 8 | PASS |
| `PttGlobalQuickExit.ResolveQuickTicks` | 2 | 2 | <= 8 | PASS |
| `PttGlobalQuickExit.SnapshotTargetOrders` | 4 | 4 | <= 8 | PASS |
| `CopyEngine.CancelQxBrackets(acc,instr)` | 6 | 6 | <= 8 | PASS (called, not modified) |

Branch count for `ExecuteOne` after fix:
- Branch 1: `if (!skipIfFollower)` — the new guard
- Branch 2: base (implicit entry) + delegation
- McCabe CYC = 1 (base) + 1 (conditional) = **2**

`Execute()` stays at CYC=8: the new guard is INSIDE `ExecuteOne`, not inside `Execute`.
Zero new branches added to `Execute()`.

### 7-Scan Checklist

Engineer MUST run all scans on `PttGlobalQuickExit.cs` and `B79Tests.cs` before commit.
All must return zero results.

**SCAN-01 — lock() ban (JS-021, P0)**
```powershell
grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "lock(" src/PropTraderTools/Tests/B79Tests.cs
```
Expected: 0 matches. Current confirmed: 0 in PttGlobalQuickExit.cs. New code adds no lock.

**SCAN-02 — throw new (JS-001, P0)**
```powershell
grep -n "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "throw new" src/PropTraderTools/Tests/B79Tests.cs
```
Expected: 0 matches in production code. Note: test arrange-only throw in B79Tests.cs
test infrastructure is acceptable if unavoidable (test scaffolding only, never in src/).

**SCAN-03 — return null (JS-002, P0)**
```powershell
grep -n "return null" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
Expected: 0 matches. `SnapshotTargetOrders` returns empty list never null (confirmed
line 112). `ExecuteOne` is void. No new return paths added.

**SCAN-04 — async void non-event-handler (JS-033, P0)**
```powershell
grep -n "async void" src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
Expected: 0 matches. All methods in PttGlobalQuickExit.cs are synchronous void.
New guard is synchronous. No async introduced.

**SCAN-05 — non-ASCII characters (JS-066)**
```powershell
Select-String -Pattern '[^\x00-\x7F]' src/PropTraderTools/Features/PttGlobalQuickExit.cs
Select-String -Pattern '[^\x00-\x7F]' src/PropTraderTools/Tests/B79Tests.cs
```
Expected: 0 matches. All string literals and identifiers in the new guard are ASCII-only.
XML doc uses ASCII-only. No Unicode, emoji, or curly quotes.

**SCAN-06 — CYC audit**
```powershell
python scripts/complexity_audit.py src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
Expected: All methods <= 8.
- `Execute` = 8
- `ExecuteOne` = 2 (was 1, +1 for new guard)
- `ResolveQuickTicks` = 2
- `SnapshotTargetOrders` = 4

**SCAN-07 — [Fact] count**
```powershell
Select-String -Path "src/PropTraderTools/**/*.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
Expected: Count >= 541 (pre-fix baseline 539 + minimum 2 new tests).

### xUnit [Fact] Test Names and Assert Conditions

**Test file**: `src/PropTraderTools/Tests/B79Tests.cs` (new file)
Framework: xUnit only. No NUnit. No MSTest.

---

**T_DW_B79_03_01** — `ExecuteOne_Follower_PreCancelsBeforeQxSubmit`

```csharp
[Fact]
public void ExecuteOne_Follower_PreCancelsBeforeQxSubmit()
{
    // Arrange: follower account with 1 Working Stop1 order (ATM bracket).
    //          Spy/mock CopyEngine.CancelQxBrackets records invocation count
    //          and records call ordering vs PttQuickExit.Execute entry.
    // Act: call ExecuteOne(follower, instr, t1=4, targets=emptyList,
    //                      skipIfFollower=false, leaderStop=0, leaderTargetCount=0)
    // Assert 1: cancelInvocationCount >= 1
    //           (CancelQxBrackets was invoked for follower account)
    // Assert 2: pre-cancel call happened BEFORE PttQuickExit.Execute was entered
    //           (verified by call-order tracking in the spy)
}
```

Assert conditions (engineer must assert both):
1. `cancelInvocationCount >= 1` — pre-cancel guard fired for follower
2. Cancel call occurred before `PttQuickExit.Execute` entry (call-order invariant)

---

**T_DW_B79_03_02** — `ExecuteOne_Leader_DoesNotPreCancelFollowerBrackets`

```csharp
[Fact]
public void ExecuteOne_Leader_DoesNotPreCancelFollowerBrackets()
{
    // Arrange: leader account with 2 Working ATM brackets.
    //          Spy records CancelQxBrackets calls originating from ExecuteOne guard ONLY
    //          (distinguish from PttQuickExit.Execute's own cancel path).
    // Act: call ExecuteOne(leader, instr, t1=4, targets=emptyList,
    //                      skipIfFollower=true, leaderStop=0, leaderTargetCount=0)
    // Assert: executeOneCancelCount == 0
    //         (the new if(!skipIfFollower) guard does NOT fire on the leader path)
}
```

Assert conditions:
1. `executeOneCancelCount == 0` — new guard skipped entirely when `skipIfFollower=true`

---

**T_DW_B79_03_03** — `BuildQxSnapshot_ExcludesCancelSubmitted_Orders` (recommended, belt-and-suspenders)

```csharp
[Fact]
public void BuildQxSnapshot_ExcludesCancelSubmitted_Orders()
{
    // Arrange: account with 1 order, OrderState = CancelSubmitted, Name = "Stop1"
    // Act: result = CopyEngine.BuildQxSnapshot(acc, instr)
    // Assert: result.Count == 0
    //         (CancelSubmitted is not in BuildQxSnapshot's stateOk -- the key invariant
    //          that makes Direction A work: after pre-cancel, follower snapshot is empty)
}
```

Assert conditions:
1. `result.Count == 0` — `CancelSubmitted` orders are excluded from snapshot, confirming
   that after pre-cancel the follower presents an empty snapshot to `PttQuickExit.Execute`

### Acceptance Criteria

- [ ] `if (!skipIfFollower) CopyEngine.Instance?.CancelQxBrackets(acc, instr);` added in `ExecuteOne` BEFORE `new PttQuickExit()`
- [ ] `[PTT-QX-GUARD]` log line present inside the guard (ASCII-only)
- [ ] XML doc on `ExecuteOne` updated with DW-B79-03 annotation (ASCII-only)
- [ ] Leader path (`skipIfFollower=true`) behavior is byte-for-byte identical to before this change
- [ ] `PttQuickExit.cs` is NOT modified (steering note: must remain unchanged)
- [ ] `CopyEngine.cs` is NOT modified
- [ ] `PttBreakEven.cs` is NOT modified
- [ ] CYC of `ExecuteOne` = 2 (SCAN-06 confirms)
- [ ] CYC of `Execute` = 8, unchanged (SCAN-06 confirms)
- [ ] All 7 scans return 0 matches
- [ ] `[Fact]` count >= 541 (SCAN-07 confirms)
- [ ] Build passes: `powershell -File .\scripts\build_readiness.ps1`
- [ ] Hard-link sync: `powershell -File .\deploy-sync.ps1`

### NT8 Constraints Applicable

| Constraint | Rule | Status |
|------------|------|--------|
| No `lock()` in any modified code | JS-021 / NT8-LOCK-BAN | PASS — `CancelQxBrackets` confirmed lock-free (CopyEngine.cs:584 header) |
| No `async void` | JS-033 | PASS — `ExecuteOne` is synchronous void |
| No `throw new` in hot path | JS-001 | PASS — `CancelQxBrackets` uses silent `catch {}` (existing); no new throw |
| No `return null` | JS-002 | PASS — `ExecuteOne` is void; `SnapshotTargetOrders` returns empty list |
| ASCII-only strings and identifiers | JS-066 | PASS — new guard, log line, and XML doc are ASCII-only |
| `Account.All` not in constructor | NT8-021 | PASS — `Account.All` is in `Execute()`, called from UI thread post-Loaded (unchanged) |
| `DateTime.MaxValue` for GTC | NT8-013 | PASS — GTC orders in `PttQuickExit.Execute` (unchanged) |
| `PTT-` prefix on all order names | NT8-014 | PASS — order names unchanged (`PTT-QX-Stop`, `PTT-QX-T*`) |
| `CreateOrder` requires explicit `Submit` | NT8-007 | PASS — `PttQuickExit.Execute` (unchanged) calls `leader.Submit(new[] { ord })` |
| No `FontFamily` override | NT8-FONT | PASS — no UI change in this ticket |
| No hardcoded hex colors | NT8-COLOR | PASS — no UI change in this ticket |
| `DateTime.UtcNow` not `DateTime.Now` | NT8-TIME | PASS — no new DateTime usage |

---

## TICKET-2 — Carry-Forward Table Update (NO-PIPELINE-REPAIRS.md)

### Spec Req IDs

- DW-B79-03 (close-out documentation)
- AC-8 (carry-forward table reflects fix status with commit hash)

### Files to Change

| File | Change | Scope |
|------|--------|-------|
| `docs/brain/NO-PIPELINE-REPAIRS.md` | MODIFY | Update DW-B79-03 row in carry-forward table to FIXED with commit hash |

### Implementation Specification

After the TICKET-1 commit is made and the commit hash is known, locate the DW-B79-03
entry in the `NO-PIPELINE-REPAIRS.md` carry-forward table and update it as follows.

**Find the existing row** (search for `DW-B79-03` in the carry-forward / pipeline status
table section, approximately the `PIPELINE STATUS — B72 / B73 / B74 / B75 / B76 / B77 /
B78` block and any DW- item log block):

Existing entry format (approximate, per Appendix C of architecture plan):
```
| DW-B79-03 | QX Conflict Guard: follower ATM cancel before PTT-QX submit | P2 | OPEN |
```

**Updated entry** (replace XXXXXXXX with actual short commit hash, 8 hex chars):
```
| DW-B79-03 | QX Conflict Guard: follower ATM cancel before PTT-QX submit | P2 | FIXED (commit XXXXXXXX) |
```

**[PTT-DIAG] notes update** — add or update the associated DIAG note:
```
Gap2 FIXED REPAIR-08 a3f68559 + QX guard FIXED DW-B79-03 (commit XXXXXXXX)
```

**Timing**: Do NOT fill this ticket until TICKET-1 has been committed and the commit
hash is known. The hash placeholder `XXXXXXXX` must be replaced with the actual 8-char
short hash from `git log --oneline -1`.

**Verification command** (run after update):
```powershell
Select-String -Pattern "DW-B79-03" docs/brain/NO-PIPELINE-REPAIRS.md
```
Expected: all matches show `FIXED` and the commit hash. Zero `OPEN` matches remain.

### Method Signatures

N/A — documentation-only ticket. No `.cs` files modified.

### CYC Budget

N/A — no source code change.

### 7-Scan Checklist

Engineer MUST run all scans before committing the TICKET-2 doc update.
TICKET-2 changes only `docs/brain/NO-PIPELINE-REPAIRS.md` (no `.cs` files); scans 01-05
run against the files touched in this build session and trivially return 0 new matches.
Scans 06-07 carry forward from TICKET-1 (verified at TICKET-1 commit; unchanged here).

| Scan | Command | Expected | Notes |
|------|---------|----------|-------|
| SCAN-01 lock() | `Select-String -Path src\PropTraderTools\*.cs,src\PropTraderTools\Features\*.cs -Pattern "lock\s*\(" -Recurse` | 0 matches | no .cs change in this ticket |
| SCAN-02 throw | `Select-String -Path src\PropTraderTools\*.cs,src\PropTraderTools\Features\*.cs -Pattern "throw\s+new" -Recurse` | 0 new matches | no .cs change in this ticket |
| SCAN-03 return null | `Select-String -Path src\PropTraderTools\*.cs,src\PropTraderTools\Features\*.cs -Pattern "return\s+null" -Recurse` | 0 new matches | no .cs change in this ticket |
| SCAN-04 async void | `Select-String -Path src\PropTraderTools\*.cs,src\PropTraderTools\Features\*.cs -Pattern "async\s+void" -Recurse` | 0 matches | no .cs change in this ticket |
| SCAN-05 non-ASCII | `(Get-Content docs\brain\NO-PIPELINE-REPAIRS.md) \| Select-String -Pattern '[^\x00-\x7F]'` | 0 non-ASCII chars | doc update only |
| SCAN-06 CYC | N/A — no .cs change in this ticket; CYC verified in TICKET-1 | N/A | carry-forward from TICKET-1 |
| SCAN-07 [Fact] count | N/A — no test file change in this ticket; count verified in TICKET-1 | N/A | carry-forward from TICKET-1 |

### xUnit [Fact] Test Names and Assert Conditions

N/A — documentation-only ticket. No new tests required.

### Acceptance Criteria

- [ ] DW-B79-03 row in `NO-PIPELINE-REPAIRS.md` carry-forward table shows status = `FIXED`
- [ ] Commit hash present (8-char short hash, e.g. `a3f68559`-style)
- [ ] `[PTT-DIAG]` note updated: `Gap2 FIXED REPAIR-08 a3f68559 + QX guard FIXED DW-B79-03 (commit XXXXXXXX)`
- [ ] `Select-String -Pattern "DW-B79-03" docs/brain/NO-PIPELINE-REPAIRS.md` returns zero `OPEN` matches
- [ ] No other rows in the carry-forward table are modified (surgical change only)
- [ ] TICKET-2 is executed AFTER TICKET-1 commit (hash known)

### NT8 Constraints Applicable

N/A — documentation-only ticket. No NT8 API surface involved.

---

## Engineer Execution Order

1. Execute TICKET-1 first: modify `PttGlobalQuickExit.cs` + create `B79Tests.cs`
2. Run all 7 scans (TICKET-1 checklist). All must return 0 / counts within threshold.
3. Commit with message: `fix(ptt): DW-B79-03 pre-cancel follower ATM brackets in ExecuteOne`
4. Note the commit hash: `git log --oneline -1`
5. Execute TICKET-2: update `NO-PIPELINE-REPAIRS.md` with commit hash
6. Run `powershell -File .\deploy-sync.ps1` (hard-link sync after source change)
7. F5 in NinjaTrader — must compile green before merge

---

TICKETS_COMPLETE
