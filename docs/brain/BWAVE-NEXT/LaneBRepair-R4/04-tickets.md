# Tickets -- BWAVE-NEXT LaneBRepair-R4

**Epic**: BWAVE-NEXT LaneBRepair-R4
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Written by**: ptt-architect
**Date**: 2026-09-05
**Branch**: bwave-next-lane-b
**Plan reviewed**: `docs/brain/BWAVE-NEXT/LaneBRepair-R4/02-plan-review.md` (REVIEW_PASS)

---

## Ticket Count: 1

Single ticket T1. No lane split (LANE-SPLIT GATE: SINGLE-PIPELINE, Section 1 of plan).

---

# T1 -- R4-F1 STALE: Regression Guard Test

## Spec Requirement IDs Satisfied

| ID | Description | Status |
|----|-------------|--------|
| R4-F1 | SubmitDrainedEntry cleanup ordering investigation | STALE -- no production code change required |
| R4-T1 | Regression guard test for submit-before-cleanup ordering | NEW TEST REQUIRED |

**Finding**: R4-F1 is STALE. The bug described (cleanup running BEFORE `SubmitEntryDirect`)
does not exist in current source. R3-F2 already fixed the ordering. `SubmitEntryDirect` is at
line 6641; `foreach DrainedOrderIds` cleanup is at lines 6650-6651. The R3-F2 comment at
line 6649 documents the design intent: IDs are preserved on submit failure by deferring cleanup.

**Production code change**: NONE. `src/PropTraderTools/CopyEngine.cs` is NOT modified.

---

## File Path in Wave Workspace

| Role | File | Action |
|------|------|--------|
| Production (unchanged) | `src/PropTraderTools/CopyEngine.cs` | NO CHANGE |
| Test file (modify) | `src/PropTraderTools/CopyEngineTests.cs` | ADD 1 [Fact] |

---

## Method Signatures

### New Test Method (only deliverable for T1)

```csharp
[Fact]
public void SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()
```

- **Return type**: `void`
- **Parameters**: none
- **Attribute**: `[Fact]` (xUnit)
- **File**: `src/PropTraderTools/CopyEngineTests.cs`
- **Placement**: append to existing test class body

### No New Production Methods

`SubmitDrainedEntry(string acctKey)` in `src/PropTraderTools/CopyEngine.cs` is **not touched**.

---

## Implementation Contract

### Exact test body to add

```csharp
[Fact]
public void SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1()
{
    // Regression guard: R4-F1 was investigated and found STALE.
    // This test confirms the R3-F2 ordering comment still exists in source,
    // guarding against any future edit that moves cleanup before submit.
    // If this comment disappears, the ordering may have been changed and
    // R4-F1 should be re-evaluated.
    var sourceText = System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(
                typeof(CopyEngine).Assembly.Location),
            "..", "..", "..", "src", "PropTraderTools", "CopyEngine.cs"));
    Assert.Contains(
        "R3-F2: clear drain-owned IDs AFTER submit",
        sourceText,
        System.StringComparison.Ordinal);
}
```

### Rules for writing the test

1. Use `[Fact]` only -- no `[Theory]`, `[Test]`, NUnit, or MSTest attributes.
2. ASCII-only string literals -- the search string `"R3-F2: clear drain-owned IDs AFTER submit"` is ASCII.
3. No `lock()` anywhere in the test.
4. No `DateTime.Now` -- not applicable to this test.
5. No `return null` -- test returns `void`.
6. No `async void` -- test is synchronous `void [Fact]`.
7. `Assert.Contains` overload used: `(string expectedSubstring, string actualString, StringComparison)`.

---

## JS Rule Constraints

| Rule ID | Description | Application to T1 |
|---------|-------------|-------------------|
| JS-021 | No `lock()` usage | Test uses file I/O and string search only. No `lock(` in new code. Verified by SCAN-01. |
| JS-001 | No `throw new XxxException` in hot paths | Test uses `Assert.Contains`, not manual throws. Not a hot path. |
| JS-002 | No `return null` | Test returns `void`. |
| JS-033 | No `async void` (non-event-handler) | Test is synchronous `void [Fact]`. |
| JS-051 | xUnit `[Fact]` only (Test Framework Mandate) | `[Fact]` attribute used. No NUnit/MSTest. |
| JS-066 | CYC <= 8 per method | New test CYC = 2 (base 1 + Assert branch 1). Within budget. |
| JS-004 | ASCII-only identifiers and literals | All identifiers and string literals in new code are ASCII. |

---

## xUnit [Fact] Tests

| Test Name | Purpose | CYC | File |
|-----------|---------|-----|------|
| `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1` | Regression guard: assert R3-F2 ordering comment exists in CopyEngine.cs source, failing if cleanup is ever moved before submit | 2 | `src/PropTraderTools/CopyEngineTests.cs` |

### What the test asserts

- Reads the raw text of `CopyEngine.cs` at runtime via `typeof(CopyEngine).Assembly.Location`.
- Asserts the substring `"R3-F2: clear drain-owned IDs AFTER submit"` is present using
  `StringComparison.Ordinal`.
- If a future edit moves the cleanup before the submit (reverting the R3-F2 fix), the comment
  will either be removed or changed, and this test will fail, prompting re-investigation of R4-F1.

---

## 7-SCAN CHECKLIST (Engineer Contract)

All 7 scans MUST pass before the ticket is considered complete.

| Scan | Description | Command | Expected Result |
|------|-------------|---------|-----------------|
| **SCAN-01** | JS-021 lock() ban | `grep "lock(" src/ --include="*.cs" -r` | 0 matches in new code. Any existing matches in unchanged files are pre-existing and out of scope. |
| **SCAN-02** | JS-033 async void ban | `grep "async void " src/ --include="*.cs" -r` | 0 matches in new code. |
| **SCAN-03** | JS-002 return null ban | `grep "return null;" src/ --include="*.cs" -r` | 0 matches in new code. |
| **SCAN-04** | JS-004 ASCII-only | Inspect all new code: identifiers, string literals, comments must be ASCII-only. No Unicode, emoji, curly quotes. | 0 non-ASCII characters in new code. |
| **SCAN-05** | NT8 API: no AtmStrategyChangeStopTarget in AddOnBase context | `grep "AtmStrategyChangeStopTarget" src/ --include="*.cs" -r` | 0 matches in new code (not an AddOnBase method). |
| **SCAN-06** | CYC <= 8 | `python scripts/complexity_audit.py` or manual count: `SubmitDrainedEntry` CYC unchanged = 4; new test method CYC = 2. | All methods <= 8. Zero regressions. |
| **SCAN-07** | Zero build errors | `dotnet build src/PropTraderTools/ --no-incremental` | 0 errors, 0 new warnings. All prior tests pass. |

---

## Scope Lock

The following are LOCKED and MUST NOT be changed by ptt-engineer when executing T1:

| Item | Lock |
|------|------|
| `src/PropTraderTools/CopyEngine.cs` | NO CHANGE. `git diff src/PropTraderTools/CopyEngine.cs` must be empty after T1. |
| `(long)(int)Environment.TickCount` | Preserved -- .NET 4.8, no TickCount64 available (DW-net-1 DISMISSED). |
| `.ToList()` on `ActiveOrders` | Preserved -- copy-on-enumeration thread-safety pattern (DW-NEXT-A-07 DISMISSED). |
| Watchdog drop-on-timeout | No resubmit on expiry (Director-locked). |
| Drain key is acct-only | Multi-instrument extension deferred to DW-NEXT-B-01. |
| try/finally NOT applied | R4-F1 is STALE -- current ordering is already correct. Do not apply the hypothetical pattern. |

---

## Dismissed Findings (Carried Forward)

All 11 confirmed dismissed items from prior rounds. These are closed and MUST NOT be re-opened in T1.

| ID | Finding | Disposition |
|----|---------|-------------|
| CR5-outside-1 | Drain ID/instrument scoping | DW-NEXT-B-01. DISMISSED (future scope). |
| CR5-outside-2 | ATM mode/template preservation in payload | DW-NEXT-B-02. DISMISSED (future scope). |
| CR5-outside-3 | TryDrainWatchdog independent trigger | Advisory only. DISMISSED. |
| CR5-dup-1 | Order.Name null guard | NT8 guarantees non-null Order.Name. DISMISSED. |
| CR5-dup-2 | OnOrderUpdate helper extraction CYC | DW-NEXT-B-04. DISMISSED (future complexity epic). |
| CR5-dup-3 | _followerReplaceSpecs FSM | Scope creep. DISMISSED. |
| CR5-dup-4 | Hot-path heap alloc removal | DW-NEXT-A-07. DISMISSED. |
| CR5-test-1 | Test PascalCase no underscores | Project convention. DISMISSED. |
| CR5-test-2 | Test parameter type assertions | Advisory. DISMISSED. |
| DW-lock-1 | Watchdog resubmit vs drop | Director-locked (drop on timeout). DISMISSED. |
| DW-net-1 | TickCount64 usage | .NET 4.8 -- TickCount64 unavailable. DISMISSED. |

---

## Deferred Items (Carried Forward, No New Items)

No new DW- items are generated by R4. STALE finding produces no deferred work.

| ID | Status |
|----|--------|
| DW-NEXT-B-01 | OPEN (carried forward) |
| DW-NEXT-B-02 | OPEN (carried forward) |
| DW-NEXT-B-03 | OPEN (carried forward) |
| DW-NEXT-B-04 | OPEN (carried forward) |

---

## Acceptance Criteria

- [ ] `src/PropTraderTools/CopyEngine.cs` has zero diff (`git diff` shows no changes).
- [ ] `src/PropTraderTools/CopyEngineTests.cs` contains exactly one new `[Fact]` method named
      `SubmitDrainedEntry_SourceOrdering_SubmitBeforeCleanup_StaleR4F1`.
- [ ] Test asserts `Assert.Contains("R3-F2: clear drain-owned IDs AFTER submit", sourceText, StringComparison.Ordinal)`.
- [ ] `dotnet build` passes with 0 errors and 0 new warnings.
- [ ] All prior tests pass (no regressions).
- [ ] SCAN-01 through SCAN-07 all pass (see checklist above).
- [ ] `(long)(int)Environment.TickCount` is unchanged in production code.
- [ ] `.ToList()` on `ActiveOrders` is unchanged in production code.
- [ ] No new DW- deferred items are generated.
- [ ] All 11 dismissed findings remain DISMISSED (not re-opened).

---

## NT8 Sync Gate

After test file edit:

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected: 0 MISMATCH lines. Then press **F5** in NinjaTrader 8 to recompile. Green compile = T1 complete.

---

*Tickets written: 2026-09-05 | ptt-architect | Phase 3 | BWAVE-NEXT LaneBRepair-R4*

---

**TICKETS_COMPLETE**
