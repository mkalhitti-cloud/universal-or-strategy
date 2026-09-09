# PTT-REPAIRS-04-POST-BUG-F -- Deferred Backlog

**Epic**: PTT-REPAIRS-04-POST-BUG-F
**Written by**: PTT Plan Reviewer (Phase 5)
**Status gate**: Required for PIPELINE_COMPLETE

---

## BLOCK: PTT-REPAIRS-04-POST-BUG-F

### DEFERRED-1: Option A test runner wiring (stale T_CLONE_* tests)

**ID**: DW-BUG-F-01
**Priority**: P1
**Target session**: Next Option A test runner session

**Description**:
`CopyEngineTests.cs` contains `T_CLONE_*` test methods at approximately lines 4620-4685.
These tests call the old zero-parameter `GetCloneAtmMode()` signature.
The new signature introduced by BUG-F is `GetCloneAtmMode(string instrFullName)`.
These tests now fail to compile against the new signature.

**Required changes**:
- (a) Update all existing `T_CLONE_*` tests to pass the `instrFullName` parameter to
  `GetCloneAtmMode(string instrFullName)` per the new signature.
- (b) Add new cross-instrument isolation test:
  ```csharp
  SetCloneAtmCache("MGC DEC26", "MGC_TPL");
  SetCloneAtmCache("MES DEC26", "MES_TPL");
  Assert(GetCloneAtmMode("MGC DEC26") returns MGC_TPL); // not MES_TPL
  Assert(GetCloneAtmMode("MES DEC26") returns MES_TPL); // not MGC_TPL
  ```
  This test proves the per-instrument isolation guarantee that BUG-F was designed to deliver.

**Blocked by**: Option A test runner wiring. `CopyEngineTests.cs` is not currently connected
to an executable test project. No test updates are possible until the test runner pipeline
(`xunit` / `dotnet test` project referencing `CopyEngineTests.cs`) is operational.

**Non-blocking for**: PTT-REPAIRS-04-POST-BUG-F source verification (confirmed in
02-architecture-plan.md line 180, 04-ticket-review.md line 95, ticket-1-verification.md line 540).

---

### DEFERRED-2: BUG-E fix (parallel pipeline)

**ID**: DW-BUG-F-02
**Priority**: P1
**Target session**: PTT-REPAIRS-04-POST-BUG-E pipeline

**Description**:
Clone ATM related bug covered in parallel pipeline PTT-REPAIRS-04-POST-BUG-E.
Specifically: reversal-guard skip on cancelled entry -- a separate root cause from BUG-F.

**Status**: Parallel pipeline. Not in scope for PTT-REPAIRS-04-POST-BUG-F.

---

### DEFERRED-3: TOCTOU window in value-guarded TryRemove

**ID**: DW-BUG-F-03
**Priority**: P2
**Target session**: Future hardening session

**Description**:
Carried from PTT-REPAIRS-03-POST. In `SetCloneAtmObjectCache` (CopyEngine.cs lines 732-735),
the null-guard pattern is:

```csharp
if (atmObj != null)
    _cloneAtmObjectByInstr[instrFullName] = atmObj;
else
    _cloneAtmObjectByInstr.TryRemove(instrFullName, out _);
```

This check-then-remove pattern has a theoretical TOCTOU (time-of-check/time-of-use) window
under concurrent modification: between the `if (atmObj != null)` check and the `TryRemove`
call, another thread could theoretically insert a non-null value under the same key.

**Risk assessment**: LOW. The write path (`OnCloneModeClick`) executes on the UI thread,
and the NT8 add-on architecture serialises panel click handlers. Concurrent writes to the
same instrument key from different threads are not a realistic scenario in the current
TradeCopierPanel design.

**Action**: Document risk; re-evaluate if the threading model changes in a future refactor.
No functional impact observed. `ConcurrentDictionary` guarantees atomicity on individual
operations; the only race is at the application-logic level between two application operations.

---

### DEFERRED-4: Stale CYC source comments

**ID**: DW-BUG-F-04
**Priority**: P2
**Target session**: Next cleanup pass

**Description**:
Two source comments in CopyEngine.cs report incorrect CYC values:
- Line 727: `// CYC=1` for `SetCloneAtmObjectCache` -- actual CYC is 2 (one `if` branch added).
- Line 738: `// CYC=2` for `GetCloneAtmMode` -- actual CYC is 4 (simplified McCabe: base=1,
  if(line 744)=+1, ternary(line 746)=+1, if(line 750)=+1).

**Impact**: None. Both methods remain well within the JS-013 limit of CYC <= 8.
Acknowledged in architecture plan (plan line 167) and independently confirmed by Ph4b
(ticket-1-verification.md line 366).

**Required fix**: Update the two comments to reflect actual CYC values:
- Line 727: change `// CYC=1` to `// CYC=2`
- Line 738: change `// CYC=2` to `// CYC=4`

---

## SECTION K SUMMARY TABLE

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-BUG-F-01 | Update stale T_CLONE_* tests to new GetCloneAtmMode(string) signature; add cross-instrument isolation test | P1 | Option A test runner session | OPEN |
| DW-BUG-F-02 | BUG-E fix (reversal-guard skip on cancelled entry) | P1 | PTT-REPAIRS-04-POST-BUG-E | OPEN |
| DW-BUG-F-03 | TOCTOU window in value-guarded TryRemove (SetCloneAtmObjectCache) -- carried from PTT-REPAIRS-03-POST | P2 | Future hardening session | OPEN |
| DW-BUG-F-04 | Stale CYC comments: line 727 CYC=1 (actual 2), line 738 CYC=2 (actual 4) -- doc-only corrections | P2 | Next cleanup pass | OPEN |
