# BWAVE-NEXT Lane B -- Ticket Review

**Epic**: BWAVE-NEXT Lane B -- Cancel-Before-Dispatch Drain + Post-PR-42 Repairs
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-09-04
**Tickets reviewed**: T1 (DW-NEXT-A-07 + DW-NEXT-A-06), T2 (DW-NEW-08 Option D)
**Source files read**:
- `docs/brain/BWAVE-NEXT/LaneB/04-tickets.md`
- `docs/brain/BWAVE-NEXT/LaneB/02-architecture-plan.md`
- `docs/brain/BWAVE-NEXT/LaneB-mission-brief.md`
- `docs/brain/BWAVE-DW/Backlog/DW-NEW-08-naked-fill-race.md`
- `docs/standards/jane-street/RULES_CATALOG.md` (Type Safety + Concurrency categories)
- `docs/brain/BWAVE-NEXT/LaneA/ticket-4-verification.md`
- `docs/brain/BWAVE-NEXT/LaneA/ticket-5-verification.md`

---

## Ticket 1 -- DW-NEXT-A-07 + DW-NEXT-A-06: Post-PR-42 Repairs

### TR-01 SEQUENTIAL ORDER

PASS

Pipeline sequence is stated explicitly in the PRE-REQUISITE block and the Pipeline Sequence
diagram (04-tickets.md lines 33-43):
`Commit 92a44332 --> T1 --> T1 VERIFY_PASS --> T2 --> T2 VERIFY_PASS`
T2 PRE-CONDITION (line 325-327) explicitly gates on T1 VERIFY_PASS:
`// 1. T1 has VERIFY_PASS status -- Check: docs/brain/BWAVE-NEXT/LaneB/ticket-1-verification.md
//    exists and contains VERIFY_PASS`

### TR-02 SCOPE LOCK

PASS

T1 Scope Lock (lines 57-64): "This ticket implements ONLY the T1 changes described below.
T2 (DW-NEW-08 Option D) MUST NOT be touched in this ticket. Do NOT add PendingDispatchDrain,
_pendingDispatchDrains, DrainThenDispatch, or any drain-related code."
T2 Scope Lock (lines 312-318): "T1 MUST have VERIFY_PASS before this ticket begins. Do NOT
re-implement or modify T1 changes. Do NOT read or reference ticket-1-completion.md."
T2 explicitly bars reading T1 completion report -- no contamination vector.

### TR-03 PRE-CONDITIONS

PASS

T1 pre-conditions (lines 68-86): 4 powershell Select-String checks verify TryNakedDetect at
line 6403, _nakedDetectLastQueuedTicks at line 373, ActiveOrders at line 3437, and exact
TickCount read lines. Uses dynamic scan (not hard-coded) for TickCount locations.
T2 pre-conditions (lines 322-349): 6 checks -- T1 VERIFY_PASS, .ToList() at line ~3441,
_nakedDetectLastQueuedTicks at line 373, HandleEntryChange at line 3667, cancel block
presence, clean build. Both pre-condition blocks have explicit STOP escalation instruction.

### TR-04 7-SCAN CHECKLIST PRESENT IN EACH TICKET

PASS

T1 (lines 235-248): Full 7-scan table -- SCAN-01 through SCAN-07 with exact PowerShell
commands and required results. All 7 present.
T2 (lines 920-928): Full 7-scan table -- SCAN-01 through SCAN-07 with exact PowerShell
commands and required results. All 7 present.
Defense-in-depth contract intact for both engineer attestation (Layer 2) and verifier
cross-check (Layer 3).

### TR-05 POST-GATES PRESENT

PASS

T1 post-gates (lines 270-288): Gate 1 (NT8 sync: ptt-sync-and-verify.ps1, 18/18 OK required),
Gate 2 (dotnet build, 0 errors), Gate 3 (dotnet test --filter for T1 tests, Failed:0 Passed:2),
Gate 4 (full suite regression, 0 new failures). Verbatim output recording mandated.
T2 post-gates (lines 976-999): Gate 1-4 same pattern plus Gate 5 (banned API Select-String
scan for Account.Change, AtmStrategyCreate, AtmStrategyChangeStopTarget). 5 gates total.

### TR-06 T1 SUB-A CHANGE IS MINIMAL

PASS

Ticket specifies .ToList() added exclusively inside `ActiveOrders` body (line ~3441).
Return type explicitly stays `IEnumerable<Order>` (line 120-124).
Callers at lines 3468 (FindFollowerBracketOrder overload) and 3668 (FindFollowerEntryOrder)
explicitly stated as UNCHANGED (line 121-122).
CYC stays 1: "expression body, single LINQ chain -- no new branches" (line 123).
Both caller line numbers are confirmed by T5 verification artifact (SCAN-06 step).

### TR-07 T1 SUB-B CAST FIX

PASS

Ticket specifies `(long)(int)Environment.TickCount` two-cast sequence (lines 139-141).
Targets NakedPositionDetector method (lines 143-148): "Location: NakedPositionDetector method
(confirmed line 6424, post-T4/T5 commit)".
Does NOT hard-code line numbers -- instead mandates dynamic scan:
`Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "\(long\)Environment\.TickCount"`
at PRE-CONDITION (lines 82-84) and MANDATORY step (lines 149-155). Scope guard included:
"If the pattern appears outside the NakedPositionDetector method range (lines 6424-6451),
do NOT change those occurrences -- scope is NakedPositionDetector only." (line 154-155).

### TR-08 T1 TESTS

PASS

`[Fact] ActiveOrders_ThreadSafetyVerification()` -- present (lines 171-202). Structural
reflection test: verifies ActiveOrdersTestable seam exists (reflection on CopyEngine), verifies
filter still correct after .ToList() (1 Filled + 1 Working orders, asserts Working in result),
verifies non-null via reflection. No live NT8 Account required.

`[Fact] NakedDetector_DebounceField_UsesLongArithmetic()` -- present (lines 205-232).
Structural reflection test: verifies _nakedDetectLastQueuedTicks field exists, type is
ConcurrentDictionary<string,long>, is readonly; verifies TryNakedDetect exists as private
instance void with 1 OrderEventArgs parameter. No live NT8 Account required.

Both use xUnit [Fact] only. No NUnit, no MSTest. File: BwaveDwLaneATests.cs (confirmed by
T5 verification as the file with 14 [Fact] at lines 16-319; T1 adds after line 319).

### TR-09 T1 NT8 CONSTRAINT

PASS

T1 modifications are limited to: adding `.ToList()` to an existing LINQ expression (Sub-A)
and replacing `(long)` with `(long)(int)` cast in existing TickCount reads (Sub-B). Neither
change introduces any NT8 API call. No Account.*, no ATM*, no CreateOrder, no Submit. PASS.

### TR-10 T1 CYC UNCHANGED

PASS

ActiveOrders: expression body + single LINQ chain + .ToList() at end. No new conditional
branches. CYC stays 1 (line 123).
NakedPositionDetector: pure cast substitution `(long)` -> `(long)(int)`. No new
decision points. CYC unchanged.
TryNakedDetect: CYC=3 confirmed from T4 verification (ticket-4-verification.md). Ticket
explicitly states "CYC of TryNakedDetect confirmed=3 from T4 verification. Unchanged by
this fix." (lines 158-160). PASS.

---

## Ticket 2 -- DW-NEW-08 Option D: Cancel-Before-Dispatch Drain

### TR-11 T2 PENDINGDISPATCHDRAIN TYPE

PASS

Ticket defines `PendingDispatchDrain` as `private sealed class` (line 371) with ALL required
fields present and field types specified exactly:
- FollowerAcctKey (string) -- present (line 385)
- Instrument (Instrument) -- present (line 386)
- Qty (int) -- present (line 387)
- Price (double) -- present (line 388)
- Action (OrderAction) -- present (line 389)
- OrderType (OrderType) -- present (line 390, added over plan initial draft, needed by SubmitEntryDirect)
- FollowerAccount (Account) -- present (line 391, added per plan Section 5.3 revision)
- PendingCancelCount (int plain field, NOT property) -- present (line 392, with Interlocked.Decrement comment)
- TimestampTicks (long) -- present (line 393)
All 9 field types specified exactly in both the property declarations and the constructor
(lines 395-415). Explicit constructor used (NT8-001 compliance: no { get; init; }).
Note: OrderType and FollowerAccount are additive over the 7 required spec fields --
both are architecturally required by SubmitEntryDirect and SubmitDrainedEntry respectively.

### TR-12 T2 FIELD DECLARATION

PASS

Ticket specifies (lines 430-431):
```csharp
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```
`readonly` modifier present. `ConcurrentDictionary<string, PendingDispatchDrain>` type
confirmed. `StringComparer.Ordinal` initialization specified. Placement stated: "Immediately
after the _nakedDetectLastQueuedTicks field declaration (confirmed line 373-374, post-T1
commit). Insert at line 375." (lines 424-426).

### TR-13 T2 DrainThenDispatch CYC=4 PLAN SOUND

PASS

Flow verified (lines 459-477):
- Branch (1): `if (follower == null || instrument == null) return;`
- Branch (2): `if (!entryCandidates.Any())` -> SubmitEntryDirect + return
- Branch (3): `foreach (var e in entryCandidates)` cancel loop
- Branch (4): `if (cancelCount == 0)` edge case guard -> cleanup + SubmitEntryDirect

Using project-local CYC convention (count = number of predicates, base function=0, CYC=4).
Account.Cancel(new Order[] { e }) used exclusively. No Account.Change(). Logs "[DRAIN] acct=..."
at line 477. No lock() anywhere in method flow.

### TR-14 T2 OnDrainCancelAck CYC=3

PASS

Flow verified (lines 543-548):
- Branch (1): `if (!_pendingDispatchDrains.TryGetValue(acctKey, out var payload)) return;`
- `int remaining = Interlocked.Decrement(ref payload.PendingCancelCount);` -- Interlocked used
- Branch (2): `if (remaining < 0)` underflow guard
- Branch (3): `if (remaining == 0) SubmitDrainedEntry(acctKey);` -- fires at zero

CYC=3 (3 predicates). Synchronous void. NOT async void (line 534: "NOT subscribed to any
event -- called directly from OnOrderUpdate. Synchronous void."). JS-033 compliant.
Signature is `(string acctKey)` -- not event-handler signature.

### TR-15 T2 SubmitDrainedEntry CYC=3

PASS

Flow verified (lines 568-574):
- Branch (1): `if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) return;`
- Branch (2): `if (follower == null) return;` (from payload.FollowerAccount)
- Calls SubmitEntryDirect (order null guard is inside SubmitEntryDirect -- delegated Branch 3)

Account.CreateOrder() + Submit() via delegation to SubmitEntryDirect. [DRAIN-SUBMIT] log
emitted inside SubmitEntryDirect (noted at line 574). NO Account.Change().
CYC=3 (3 predicates using project convention including delegated branch note).

### TR-16 T2 TryDrainWatchdog

PASS

Flow verified (lines 594-600):
- Branch (1): `if (_pendingDispatchDrains.IsEmpty) return;` fast-path
- `long now = (long)(int)Environment.TickCount;` -- same cast pattern as DW-NEXT-A-06
- Branch (2): `foreach (var kv in _pendingDispatchDrains)` loop
- Branch (3): `if (now - kv.Value.TimestampTicks > 2000L)` -- 2000L threshold confirmed

On timeout: `_pendingDispatchDrains.TryRemove(kv.Key, out _)` + `Print("[DRAIN-TIMEOUT] ...")`.
Does NOT submit on timeout ("NO submit on timeout -- position may have changed", line 601).
No System.Threading.Timer (line 604: "No System.Threading.Timer. Fires as a cheap tail-call
from OnOrderUpdate."). Unconditional placement confirmed in OnOrderUpdate sequence (lines 725-726).

### TR-17 T2 MODIFIED METHODS CYC MATH

PASS

HandleEntryChange: Current CYC=7 stated explicitly (lines 612-614: code comment reproduced
showing 7 branches). T2 removes the `if (order != null)` branch and limitPx/stopPx ternaries.
Final CYC=6 (7-1=6). CYC comment update instruction present (lines 660-670). Within budget <=8.

OnOrderUpdate: Current CYC=6 stated (lines 682-690: 6 gates listed explicitly), confirmed by
T4 verification ("TryNakedDetect wired as unconditional, adds 0 branches", ticket-4-verification.md
Step 6). T2 adds +1 branch (drain-ack routing, line 701 "CYC delta = +1, branch 7") + 0 for
unconditional TryDrainWatchdog. Final CYC=7. Within budget <=8.

### TR-18 T2 NT8 API COMPLIANCE EMBEDDED VERBATIM

PASS

Explicit ban table present (lines 357-366):
| API | Status |
| Account.Change() | BANNED -- silent no-op on ATM-owned orders. NEVER call. |
| AtmStrategyCreate() | BANNED -- StrategyBase-only. NOT available in AddOnBase. NEVER call. |
| AtmStrategyChangeStopTarget() | BANNED -- StrategyBase-only. NOT available in AddOnBase. NEVER call. |
| Account.Cancel(Order[]) | ALLOWED -- AddOnBase available. Used in DrainThenDispatch. |
| Account.CreateOrder(...) | ALLOWED -- AddOnBase available. Used in SubmitEntryDirect. |
| Account.Submit(Order[]) | ALLOWED -- AddOnBase available. Used in SubmitEntryDirect. |
| lock() | BANNED -- JS-021. Use ConcurrentDictionary + Interlocked only. |

All three banned APIs listed. Allowed patterns confirmed. No lock(). Table is embedded
verbatim in the ticket body, not by reference to another document.

### TR-19 T2 TESTS (min 3 [Fact])

PASS

Test 1: `[Fact] DrainThenDispatch_CancelsExistingEntryBeforeSubmit()` -- present (lines 764-814).
Structural reflection: DrainThenDispatch method signature (6 params verified), _pendingDispatchDrains
field (readonly, ConcurrentDictionary<,>), PendingDispatchDrain nested type (sealed),
PendingCancelCount as plain int field (not property, not readonly -- Interlocked ref requirement).

Test 2: `[Fact] OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero()` -- present
(lines 815-854). Structural reflection: OnDrainCancelAck (private void, 1 string param),
SubmitDrainedEntry (private void, 1 string param), TryDrainWatchdog (private void, 0 params).

Test 3: `[Fact] DrainWatchdog_ClearsStuckDrain_AfterTimeout()` -- present (lines 856-898).
Structural reflection: PendingDispatchDrain.TimestampTicks (long type), _pendingDispatchDrains
key type is string (StringComparer.Ordinal), TryDrainWatchdog (0 params, void),
PendingDispatchDrain constructor is not public (internal, not externally constructable).

All structural reflection tests. No live NT8 Account required. xUnit [Fact] only.
Test file: `BwaveNextLaneBTests.cs` (line 739, distinct from `BwaveDwLaneATests.cs`).
csproj entry: `<Compile Include="Tests\BwaveNextLaneBTests.cs" />` specified (lines 902-911).
File header template provided (lines 744-762) with correct xUnit using, namespace, class.

### TR-20 T2 LOG MARKERS

PASS

[DRAIN]: `Print("[DRAIN] acct=" + acctKey + " cancel-sent=" + cancelCount)` (line 477).
[DRAIN-SUBMIT]: `Print("[DRAIN-SUBMIT] acct=" + follower.Name + " instr=" + instrument.FullName
+ " price=" + price)` (lines 516-518) inside SubmitEntryDirect.
[DRAIN-TIMEOUT]: `Print("[DRAIN-TIMEOUT] acct=" + kv.Key)` (lines 599-600) inside TryDrainWatchdog.
All three log format strings present. All ASCII-only. No Unicode, no curly quotes.

### TR-21 T2 SIM GATE DEFERRED

PASS

SIM Gate section (lines 1003-1019) explicitly states: "DEFERRED -- non-blocking for VERIFY_PASS."
"SIM gate requires live NT8 with SIM account. Cannot be verified by structural tests alone."
AC-17 states: "SIM gate documented as DEFERRED (non-blocking for VERIFY_PASS). Record in
ticket-2-completion.md: 'SIM gate: deferred -- requires live NT8 SIM account to verify
[DRAIN] before [DRAIN-SUBMIT] sequence and [DRAIN-TIMEOUT] after 2s stuck drain.'"
Evidence requirements specified for future SIM session (3 evidence points listed).

---

## Both Tickets -- Cross-Cutting Checks

### TR-22 TRACEABILITY -- SPEC REQUIREMENTS COVERED

PASS

**DW-NEXT-A-07 (ActiveOrders thread safety)** mission brief ACs:
- NT8 thread-safety determination documented: T1 AC-01 (AMBIGUOUS-ADDED-TOLIST) ✓
- If .ToList() added, build + sync: T1 AC-02, AC-06, AC-08 ✓
- CYC stays 1: T1 SCAN-05 + explicit note ✓
- [Fact] ActiveOrders_ThreadSafetyVerification: T1 AC-07 ✓

**DW-NEXT-A-06 (TickCount wraparound)** mission brief ACs:
- (long)(int) cast applied to all TickCount-to-long reads in debounce dict: T1 AC-03 ✓
- Dynamic scan mandated (not hard-coded lines): T1 Sub-B MANDATORY step ✓
- [Fact] NakedDetector_DebounceField_UsesLongArithmetic: T1 AC-07 ✓

**DW-NEW-08 Option D** spec ACs (naked-fill-race.md lines 125-128):
- 14+ drag cycles: flat or Entry:Filled + brackets: T2 AC-17 (SIM deferred, non-blocking) ✓
- Log [DRAIN] before [DRAIN-SUBMIT]: T2 AC-10, AC-17 SIM evidence note ✓
- Log [DRAIN-TIMEOUT] after 2s: T2 AC-07, AC-10 ✓
- No lock(), CYC <=8: T2 AC-11, AC-12, SCAN-05 ✓

**Mission brief T2 ACs** (lines 176-188):
- DrainThenDispatch CYC<=4: T2 AC-03 ✓
- OnDrainCancelAck CYC<=3: T2 AC-05 ✓
- SubmitDrainedEntry CYC<=3: T2 AC-06 ✓
- PropagateFollowerEntryReplace (HandleEntryChange) CYC <=8: T2 AC-08 ✓
- OnOrderUpdate CYC <=8: T2 AC-09 ✓
- No lock(), no Account.Change(), no ATM*: T2 AC-11, AC-12 ✓
- NT8 sync 18/18: T2 AC-16 ✓
- dotnet build 0 errors: T2 AC-14 ✓
- 3 [Fact] tests: T2 AC-15 ✓
- SIM gate deferred: T2 AC-17 ✓

No phantom work found (all ticket items trace to spec or architecture plan).
No missing work found (all spec requirements covered).

### TR-23 NO SCOPE CREEP

PASS

No TradeCopierWindow.cs in either ticket's file write-set.
DW-NEXT-A-03/04/05 explicitly excluded (04-tickets.md T3 section line 1023: "NONE REQUIRED").
Architecture plan Deferred section explicitly excludes DW-NEXT-A-03, DW-NEXT-A-04, DW-NEXT-A-05,
DW-RepairLC-01/02, DW-C39-09 LaneA (SaveRules), NEW-0x test quality gaps.
T3 Housekeeping: Director action only, no pipeline, no code (line 1023-1029 confirmed).

### TR-24 ACCEPTANCE CRITERIA NUMBERED

PASS

T1: AC-01 through AC-09 (9 ACs, lines 254-262). Each is verifiable:
AC-01 (documented in completion report), AC-02 (verifiable via Select-String), AC-03
(verifiable via Select-String), AC-04 (verifiable via Select-String), AC-05 (scan results),
AC-06 (build output), AC-07 (test results), AC-08 (sync output), AC-09 (scan results).
No vague ACs.

T2: AC-01 through AC-17 (17 ACs, lines 934-969). Each is verifiable:
AC-01 to AC-12 (structural/code checks via Select-String or reflection), AC-13 (scan output),
AC-14 (build output), AC-15 (test results), AC-16 (sync output), AC-17 (documented in
completion report). No vague ACs.

### TR-25 JANE STREET RULE COMPLIANCE ACROSS BOTH TICKETS

PASS

JS-021 no lock(): T1 SCAN-01 + AC-09; T2 SCAN-01 + AC-12 + NT8 API table (lock() BANNED row).
ConcurrentDictionary + Interlocked confirmed as the only shared-state mechanisms.

JS-033 no async void (non-handler): T1 SCAN-02 + AC-09; T2 SCAN-02 + explicit note on
OnDrainCancelAck (line 534: "NOT subscribed to any event -- called directly from OnOrderUpdate.
Synchronous void. NOT async void.") + AC-05.

JS-002 no return null in new code: T1 SCAN-03 (new code in ActiveOrders and NakedPositionDetector
has no return null); T2 SCAN-03 (all new T2 methods are void -- no return null possible).

JS-001 no throw new in hot paths: T1 SCAN-04; T2 SCAN-04. No throw new in any new or
modified method descriptions.

CYC<=8: All new and modified methods with explicit numeric counts:
T1: ActiveOrders=1, TryNakedDetect=3 (unchanged), NakedPositionDetector (cast only).
T2: DrainThenDispatch=4, SubmitEntryDirect=2, OnDrainCancelAck=3, SubmitDrainedEntry=3,
TryDrainWatchdog=3, HandleEntryChange=6, OnOrderUpdate=7. All <=8.

ASCII-only: T1 SCAN-06; T2 SCAN-06. All log strings use ASCII characters only (verified
from ticket source: "[DRAIN]", "[DRAIN-SUBMIT]", "[DRAIN-TIMEOUT]" -- no Unicode).

xUnit-only: T1 SCAN-07; T2 SCAN-07. [Fact] exclusively. No NUnit [Test], no MSTest [TestMethod].
File headers explicitly import Xunit (T2 header template line 752: `using Xunit;`).

---

## CYC Pre-Check Summary

| Method | Branches | CYC (project convention) | Budget |
|--------|----------|--------------------------|--------|
| ActiveOrders | 0 (LINQ expr body) | 1 | <=8 ✓ |
| TryNakedDetect | 2 (confirmed T4 verif) | 3 | <=8 ✓ |
| NakedPositionDetector | cast change only | unchanged | <=8 ✓ |
| DrainThenDispatch | 4 | 4 | <=8 ✓ |
| SubmitEntryDirect | 2 | 2 | <=8 ✓ |
| OnDrainCancelAck | 3 | 3 | <=8 ✓ |
| SubmitDrainedEntry | 3 (incl. delegated) | 3 | <=8 ✓ |
| TryDrainWatchdog | 3 | 3 | <=8 ✓ |
| HandleEntryChange | 6 (was 7, -1) | 6 | <=8 ✓ |
| OnOrderUpdate | 7 (was 6, +1) | 7 | <=8 ✓ |

No method at-risk. All explicit CYC numbers are within budget.

---

## Scan Checklist Presence Summary (Defense in Depth)

| Ticket | SCAN-01 | SCAN-02 | SCAN-03 | SCAN-04 | SCAN-05 | SCAN-06 | SCAN-07 |
|--------|---------|---------|---------|---------|---------|---------|---------|
| T1 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| T2 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

All 14 scan slots present (7 per ticket). Layer 1 contract intact.

---

## File Routing Check

| Ticket | File | Path | Verdict |
|--------|------|------|---------|
| T1 | CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | PASS (wave workspace) |
| T1 | Test file | `src/PropTraderTools/Tests/BwaveDwLaneATests.cs` | PASS (wave workspace) |
| T2 | CopyEngine.cs | `src/PropTraderTools/CopyEngine.cs` | PASS (wave workspace) |
| T2 | New test file | `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | PASS (wave workspace) |
| T2 | csproj | `src/PropTraderTools/PropTraderTools.csproj` | PASS (wave workspace) |

No Director workspace paths. All .cs files in `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`.

---

## Reviewer Notes (non-blocking observations)

**NOTE-01 -- T1 test file name vs architecture plan**: `04-tickets.md` correctly specifies
`BwaveDwLaneATests.cs` (matching T5 verification confirmed file). The architecture plan
(02-architecture-plan.md §4.3) erroneously says `BwaveNextLaneATests.cs`. The TICKET is
correct. This is a plan typo, not a ticket defect. No action required from engineer.

**NOTE-02 -- PendingDispatchDrain field count expansion**: Ticket defines 9 fields vs 7 in
the mission brief spec and 7+1 in the plan. The additions (OrderType, FollowerAccount) are
architecturally justified and appeared in the plan's own revision notes (Section 5.3).
The ticket constructor includes all additions coherently. No phantom scope added.

**NOTE-03 -- OnDrainCancelAck DRAIN-UNDERFLOW log marker**: The [DRAIN-UNDERFLOW] log
(line 547) is NOT in the required log markers list (TR-20), and is not in AC-10. This is a
defensive trace-only addition. Not a spec violation. Not scope creep.

---

## Check Results Summary

| TR | Description | Verdict |
|----|-------------|---------|
| TR-01 | Sequential order with T1 VERIFY_PASS pre-condition | PASS |
| TR-02 | Scope lock on both tickets | PASS |
| TR-03 | Pre-conditions on both tickets | PASS |
| TR-04 | 7-scan checklist present in each ticket | PASS |
| TR-05 | Post-gates present on both tickets | PASS |
| TR-06 | T1 Sub-A minimal change (.ToList inside body only) | PASS |
| TR-07 | T1 Sub-B cast fix with dynamic scan | PASS |
| TR-08 | T1 tests: both [Fact] methods, structural, xUnit-only | PASS |
| TR-09 | T1 NT8 constraint: no banned APIs | PASS |
| TR-10 | T1 CYC unchanged | PASS |
| TR-11 | T2 PendingDispatchDrain all required fields with exact types | PASS |
| TR-12 | T2 _pendingDispatchDrains field: readonly, ConcurrentDictionary, Ordinal | PASS |
| TR-13 | T2 DrainThenDispatch CYC=4, Account.Cancel only, logs [DRAIN] | PASS |
| TR-14 | T2 OnDrainCancelAck CYC=3, Interlocked.Decrement, sync void | PASS |
| TR-15 | T2 SubmitDrainedEntry CYC=3, TryRemove+Account guard, no Account.Change | PASS |
| TR-16 | T2 TryDrainWatchdog: cast, 2000L, [DRAIN-TIMEOUT], no submit, no Timer | PASS |
| TR-17 | T2 HandleEntryChange CYC=6, OnOrderUpdate CYC=7: math sound | PASS |
| TR-18 | T2 NT8 API compliance table embedded verbatim | PASS |
| TR-19 | T2 tests: 3 [Fact] structural tests, new file, csproj entry | PASS |
| TR-20 | T2 log markers [DRAIN], [DRAIN-SUBMIT], [DRAIN-TIMEOUT] all present | PASS |
| TR-21 | T2 SIM gate explicitly deferred, non-blocking for VERIFY_PASS | PASS |
| TR-22 | Traceability: all spec requirements covered, no phantom work | PASS |
| TR-23 | No scope creep: no TradeCopierWindow, no excluded DW items | PASS |
| TR-24 | Acceptance criteria numbered (AC-XX), all verifiable | PASS |
| TR-25 | JS compliance: lock/async void/return null/throw/CYC/ASCII/xUnit | PASS |

**Total checks: 25 | PASS: 25 | FAIL: 0**

---

## Overall Verdict

**TICKET_REVIEW_PASS**

Both tickets are complete, internally consistent, and defensively structured.
No Jane Street rule violations. No NT8 API violations. No CYC budget violations.
No scope creep. All 7-scan checklists present in both tickets (Layer 1 contract intact).
Traceability verified across DW-NEXT-A-07, DW-NEXT-A-06, and DW-NEW-08 Option D.
Safe to spawn ptt-engineer.

*Review completed: 2026-09-04 | ptt-ticket-reviewer | BWAVE-NEXT Lane B Phase 3.5*
