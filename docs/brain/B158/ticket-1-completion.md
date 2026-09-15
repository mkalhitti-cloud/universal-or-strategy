# B158 Ticket 1 Completion Report
# ptt-engineer Phase 4a output
# Block: B158
# Ticket: T1 -- DW-B156-01 (P0) Leader-flat drain abort SIM verification gate
# Nature: VERIFICATION-ONLY (zero code changes)
# Date: 2026-09-15
# Status: BUILD_PASS

---

## Scope Confirmation

**Ticket 1 ONLY.** No other tickets read, referenced, or implemented.
T2 region (CopyEngine.cs ~L6516-6527) was not touched or examined.
This report covers only DW-B156-01, CopyEngine.cs L1929-1974 and supporting methods.

---

## Implementation Verification Results (8 Structural Items)

All 8 structural items from Fix Design Step 1 verified against
`src/PropTraderTools/CopyEngine.cs` lines 1950-1974.

### Item 1: Guard -- skip follower accounts
**Location**: L1952-1953
**Source confirmed**:
```csharp
if (IsFollowerAccount(o.Account))                                           // guard: skip follower accounts (+1)
    return;
```
**Status**: PRESENT. PASS.

### Item 2: Guard -- Filled state only
**Location**: L1954-1955
**Source confirmed**:
```csharp
if (o.OrderState != OrderState.Filled)                                     // (+1)
    return;
```
**Status**: PRESENT. PASS.

### Item 3: Guard -- leader must be flat
**Location**: L1956-1957
**Source confirmed**:
```csharp
if (!IsFlat(FindPosition(o.Account, o.Instrument)))                        // (+1)
    return;
```
**Status**: PRESENT. PASS.

### Item 4: Loop -- iterate _pendingDispatchDrains
**Location**: L1959
**Source confirmed**:
```csharp
foreach (var kv in _pendingDispatchDrains)                                 // (+1)
```
**Status**: PRESENT. ConcurrentDictionary lock-free foreach. PASS.

### Item 5: Instrument match filter
**Location**: L1961
**Source confirmed**:
```csharp
if (kv.Value.Instrument?.FullName == instrFull)                        // if (+1) + ?. (+1) = +2
```
**Status**: PRESENT. PASS.

### Item 6: AbortDrainOnFill call
**Location**: L1963
**Source confirmed**:
```csharp
AbortDrainOnFill(kv.Key);
```
**Status**: PRESENT. Passes follower account name (kv.Key) as acctKey. PASS.

### Item 7: abortCount counter
**Location**: L1958, L1964
**Source confirmed**:
```csharp
int abortCount = 0;
...
abortCount++;
```
**Status**: PRESENT. PASS.

### Item 8: Log [DRAIN-ABORT-LEADER-FLAT] when abortCount > 0
**Location**: L1967-1973
**Source confirmed**:
```csharp
if (abortCount > 0)                                                         // (+1)
    NinjaTrader.Code.Output.Process(
        "[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
        + " instr=" + instrFull
        + " aborted=" + abortCount,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
```
**Status**: PRESENT. Exact marker "[DRAIN-ABORT-LEADER-FLAT]" confirmed. PASS.

---

### Additional Structural Confirmations

**TryEvictFollowerBeSlot call site (L1938)**:
```csharp
IsLeaderFlatDrainAbort(o, instrFull);
```
Called unconditionally at L1938, BEFORE `if (!IsFollowerAccount(o.Account)) return;` at L1939.
Pre-Gate-1 placement confirmed (OnOrderUpdate L1712 calls TryEvictFollowerBeSlot before
Gate 1 at L1763). PASS.

**AbortDrainOnFill signature (L8343)**:
```csharp
private void AbortDrainOnFill(string acctKey)
```
Confirmed: `private void`, parameter `string acctKey`. CYC=3. No lock(). PASS.

**_pendingDispatchDrains field (L387-388)**:
```csharp
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```
Confirmed: ConcurrentDictionary<string, PendingDispatchDrain>, StringComparer.Ordinal.
Key = follower account name. PASS.

---

## Summary: 8/8 Structural Items PASS

| # | Item | Status |
|---|------|--------|
| 1 | Guard: IsFollowerAccount(o.Account) return | PASS |
| 2 | Guard: o.OrderState != OrderState.Filled return | PASS |
| 3 | Guard: !IsFlat(FindPosition(...)) return | PASS |
| 4 | Loop: foreach (var kv in _pendingDispatchDrains) | PASS |
| 5 | Instrument match: kv.Value.Instrument?.FullName == instrFull | PASS |
| 6 | Abort call: AbortDrainOnFill(kv.Key) | PASS |
| 7 | Counter: abortCount++ | PASS |
| 8 | Log: [DRAIN-ABORT-LEADER-FLAT] when abortCount > 0 | PASS |

---

## 7-Scan Results

### SCAN-01: Non-ASCII byte check (file-wide)
**Command**: `$bytes = [System.IO.File]::ReadAllBytes("src\PropTraderTools\CopyEngine.cs"); ($bytes | Where-Object { $_ -gt 127 } | Measure-Object).Count`
**Result**: `0`
**Status**: PASS

### SCAN-02: lock() grep (file-wide)
**Command**: `Select-String -Pattern '^\s+lock\s*\(' src\PropTraderTools\CopyEngine.cs`
**Result**: No output (0 matches)
**Status**: PASS

### SCAN-03: DateTime.Now grep (file-wide)
**Command**: `Select-String -Pattern '\bDateTime\.Now\b' src\PropTraderTools\CopyEngine.cs`
**Raw matches (all in comment lines)**:
```
CopyEngine.cs:1245:  // ASCII-only. No DateTime.Now.
CopyEngine.cs:4950:  // ASCII-only. No DateTime.Now. No lock().
CopyEngine.cs:4973:  // ASCII-only. No DateTime.Now. No lock().
CopyEngine.cs:5868:  // ASCII-only. No DateTime.Now (uses Environment.TickCount...)
CopyEngine.cs:5999:  // NT8: DateTime.MaxValue (not DateTime.Now) and null as last two args...
CopyEngine.cs:6815:  // ASCII-only. No DateTime.Now.
CopyEngine.cs:6923:  // JS-001: no throw. JS-002: void. ASCII-only. No DateTime.Now.
```
**Executable-code matches**: `Select-String ... | Where-Object { $_.Line -notmatch '^\s*//' }` returned 0.
**Status**: PASS (0 executable-code matches; comment occurrences acceptable per ticket spec)

### SCAN-04: CYC check (source comment verification)
**Command**: Source comment read via grep for CYC= in relevant line ranges.
**Results**:

| Method | Expected CYC | Source Comment Line | Comment Text | Status |
|--------|-------------|---------------------|--------------|--------|
| TryEvictFollowerBeSlot | 7 | L1926 | `CYC=7: e?.Order(1)+o==null(1)+IsEvictTriggerState(1)+o.Instrument?.FullName(1)+??(1)+IsFollowerAccount(1)=7` | PASS |
| IsLeaderFlatDrainAbort | 8 (AT LIMIT) | L1947 | `CYC=8: base(1)+IsFollowerAccount(1)+OrderState(1)+IsFlat(1)+foreach(1)+if kv(1)+?.(1)+abortCount>0(1)=8` | PASS |
| AbortDrainOnFill | 3 | L8342 | `CYC=3: base(1) + TryRemove guard(1) + foreach(1). JS-021: no lock().` | PASS |

Note: A stale CYC comment at L1904 (pre-DW-B156-DEFER-1 decomposition) states CYC=6 for
TryEvictFollowerBeSlot. This is a pre-existing inconsistency. The authoritative comment is
at L1926 (CYC=7). Noted for future cleanup per ticket instruction. NOT a violation.
**Status**: PASS (no CYC > 8)

### SCAN-05: async void grep (file-wide)
**Command**: `Select-String -Pattern '\basync void\b' src\PropTraderTools\CopyEngine.cs`
**Raw matches (all in comment lines)**:
```
CopyEngine.cs:2152:  // JS-021: no lock. JS-001: no throw. JS-033: Tick is not async void. ASCII-only.
CopyEngine.cs:8278:  // Called directly from OnOrderUpdate -- NOT an event handler. Synchronous void. NOT async void (JS-033).
```
**Executable-code matches**: `Select-String ... | Where-Object { $_.Line -notmatch '^\s*//' }` returned 0.
**Status**: PASS (0 executable-code matches)

### SCAN-06: xUnit test
**Command**: `dotnet test --filter "TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString" --nologo`
**Result**:
```
Test run for PropTraderTools.Tests.dll (.NETFramework,Version=v4.8)
A total of 1 test files matched the specified pattern.
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1, Duration: 153 ms
```
**Status**: PASS (1 test PASSED, 0 failures)

Test verified: `[Fact]` at CopyEngineTests.cs:9480, no `[Skip]` attribute.
Asserts `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)`. PASS.

### SCAN-07: git diff + zero-diff confirmation
**Command**: `git diff src\PropTraderTools\CopyEngine.cs`
**Result**: No output (empty diff)
**Status**: PASS. Zero code changes confirmed. ptt-sync.ps1 not required (zero-diff ticket).

---

## Existing Test Result

**Test**: `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString`
**Location**: CopyEngineTests.cs:9481
**Attribute**: `[Fact]` (no `[Skip]`)
**Assertion**: `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)`
**Result**: PASSED
**Duration**: 153 ms

---

## SIM Gate Documentation

**To be executed by Director in NinjaTrader SIM environment.**

**Setup**: 1 leader account (Sim101) + 2 follower accounts (Sim103, Sim104).
ATM strategy enabled. CopyEngine copy mode active for the instrument under test.

**CRITICAL MARKER DISTINCTION**:
- `[DRAIN-ABORT-LEADER-FLAT]` = Fix3 marker (IsLeaderFlatDrainAbort). THIS is what T1 verifies.
- `[DRAIN-ABORT-FLAT]` = Fix1 marker (SubmitDrainedEntry follower-flat guard, L8319). DIFFERENT fix.

| Step | Action | PASS Criteria | FAIL Criteria |
|------|--------|---------------|---------------|
| 1 | Enter Long on leader (Sim101) using ATM strategy | Followers (Sim103, Sim104) receive Long copy entry. No [PTT-COPY-GUARD] in output. | Followers do not copy. Check rule config. |
| 2 | Trigger BE All (move price to stop level or click BE button) | Followers enter drain-cancel sequence. Leader PTT-BE-Stop moves to break-even price. | BE All does not trigger. Check ATM and BE config. |
| 3 | Wait for leader PTT-BE-Stop to fill (leader goes flat at break-even) | `[DRAIN-ABORT-LEADER-FLAT] leader=Sim101 instr=<instrument> aborted=N` appears in OutputTab1. N >= 1 when follower drains were pending. | Log absent AND ghost entry present = timing race. See SIM FAIL escalation path. |
| 4 | Check follower accounts (Sim103, Sim104) | No ghost re-entry: followers do NOT receive a new Long copy entry after leader goes flat. Followers' positions are flat or closing normally. | Ghost Sell Entry (or any new entry order) appears on Sim103/Sim104 after leader flat = ghost re-entry bug present. |
| 5 | Informational: check for [DRAIN-ABORT-FLAT] | [DRAIN-ABORT-FLAT] may or may not appear (Fix1 = follower-flat guard in SubmitDrainedEntry). Its presence is a different scenario. No action required. | N/A (informational only). |

**OVERALL PASS**: Step 3 shows [DRAIN-ABORT-LEADER-FLAT] with aborted >= 1, AND Step 4 shows
no ghost entry. DW-B156-01 is CLOSED.

**OVERALL FAIL (timing race)**: Step 3 log absent AND Step 4 shows ghost entry. The follower
cancel-acks completed before the leader fill event was processed. See SIM FAIL escalation path.

**INCONCLUSIVE**: Step 3 log absent but Step 4 also shows no ghost entry. Drains were consumed
before leader filled. Do not close DW-B156-01. Escalate to Director.

### SIM FAIL Escalation Path

If the SIM gate produces FAIL (timing race):
1. Do NOT implement a new fix within B158 scope.
2. Document the failure in the completion report with exact OutputTab1 content.
3. Create defect card `DW-B158-DRAIN-RACE-01`:
   - Title: Leader-flat drain abort timing race: cancel-ack pre-consumes drain before IsLeaderFlatDrainAbort fires
   - Root cause hypothesis: follower cancel-ack sequence completes before NT8 background thread
     processes leader fill event. _pendingDispatchDrains entry already removed by SubmitDrainedEntry.
   - Architectural fix required: leader-flat guard inside SubmitDrainedEntry. Out of scope for B158.
4. Forward DW-B158-DRAIN-RACE-01 to Director for prioritization.

**SIM Status**: PENDING DIRECTOR EXECUTION. No SIM evidence available at engineer gate time.

---

## DW-B156-01 Closure Status

**Source verification**: CLOSED at implementation level. All 8 structural items confirmed present.
Fix3 (IsLeaderFlatDrainAbort) is fully implemented and matches the DW-B156-01 spec exactly.

**Unit test**: CLOSED. TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString PASSED.

**SIM verification**: PENDING. Director must execute the 5-step SIM scenario documented above.
DW-B156-01 formally CLOSED only after Director confirms OVERALL PASS on SIM Step 3+4.

---

## BUILD_PASS Verdict

**All 7 scans: PASS (zero violations)**
**All 8 structural items: PASS (fully verified)**
**Existing test: PASS (1/1 PASSED)**
**Zero code changes: CONFIRMED (empty git diff)**
**SIM gate: Documented for Director execution**

**VERDICT: BUILD_PASS**

Implementation of DW-B156-01 Fix3 confirmed present and structurally correct in
`src/PropTraderTools/CopyEngine.cs`. The SIM gate steps are documented for Director to
execute to formally close DW-B156-01. No code changes made in this ticket.

---

_Completion report authored by ptt-engineer (Phase 4a). Block B158, Ticket 1._
_Input: docs/brain/B158/04-tickets.md (T1 section), docs/brain/B158/04-ticket-review.md, docs/brain/B158/02-architecture-plan.md (S3)._
_Source verified: CopyEngine.cs L384-388, L1712, L1929-1974, L8342-8348._
_Test verified: CopyEngineTests.cs L9480-9508._
