# B193 Plan Review

**Epic**: B193 -- Formalize B193-DIRECT-1/2/3: per-panel Quick ALL + button rename
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-10-06
**Plan version reviewed**: docs/brain/B193/02-architecture-plan.md (PLAN_COMPLETE 2026-10-06)
**Preceding backlog**: docs/brain/B191/06-deferred-backlog.md

---

## VERDICT: REVIEW_PASS

Zero violations found. Plan is cleared for Phase 3 (ticket generation).

---

## Section A -- Lane-Split Gate Compliance

| Check | Result | Notes |
|-------|--------|-------|
| Gate result stated in plan | PASS | "LANE-SPLIT GATE RESULT: LANES-APPROVED" present |
| Q1=NO (not same method / not within 50 lines) | PASS | T1=new file, T2=different files, T3=HTML |
| Q2=NO (Fix B design independent of Fix A) | PASS | T2 string replacements independent of T1 structure |
| Q3=YES (each fix has standalone value) | PASS | T1 adds new coverage; T2 unbreaks existing suite |
| Q4=YES (each fix has independent SIM verification) | PASS | T1: dotnet test B193Tests; T2: dotnet test full suite |
| Execution order stated as T1->T2->T3 sequential | PASS | "single sequential pipeline T1 -> T2 -> T3 per Director spec" |

**Gate result: LANES-APPROVED. All gate conditions satisfied.**

---

## Section B -- Content Checks

### B1. Ticket Ordering

| Check | Result |
|-------|--------|
| T1 -> T2 -> T3 sequential order stated | PASS |
| All 3 tickets share .csproj context -- sequential green-gate required | PASS -- acknowledged in plan |

### B2. T1 -- Test Cases Coverage

| Test ID | Method under test | Covered in plan | Input | Expected |
|---------|-------------------|-----------------|-------|----------|
| T_B193_01 | InstrumentDefaults.GetQuickTicks | YES | "MGC" | (10, 20) |
| T_B193_02 | InstrumentDefaults.GetQuickTicks | YES | "GC" | (10, 20) |
| T_B193_03 | InstrumentDefaults.GetQuickTicks | YES | "MES" | (4, 8) |
| T_B193_04 | InstrumentDefaults.GetQuickTicks | YES | "ES" | (4, 8) |
| T_B193_05 | InvokeFormatBuffer | YES | ("3x", 10) | "3x +10" |
| T_B193_06 | InvokeFormatBuffer | YES | ("2x", 10) | "2x +10" |
| T_B193_07 | InvokeFormatBuffer | YES | ("2x All", 10) | "2x All +10" |
| T_B193_08 | InvokeFormatQuickAllBuffer | YES | ("3x All", 10) | "3x All +10t" |
| T_B193_09 | typeof(TradeCopierPanel).GetField("_quickAllT1") | YES | -- | field != null |
| T_B193_10 | InstrumentDefaults.GetQuickTicks("MGC").t1 | YES | -- | == 10 |

All 10 required test cases covered. **PASS.**

### B3. T2 -- Stale Assertion Fix Locations

| Edit Block | File | Test Method | Old String | New String | Covered |
|------------|------|-------------|-----------|-----------|---------|
| A | CopyEngineTests.cs | T_B189_T4_01 (~line 10578-10581) | "Quick2t" | "2x" | YES |
| B | CopyEngineTests.cs | T_B189_T4_02 (~line 10587-10590) | "QAll2t +4" | "2x All +4" | YES |
| C | CopyEngineTests.cs | T_B189_T4_06 (~line 10646-10652) | "Quick"/"Quick2t" | "3x"/"2x" | YES |
| D | B73Tests.cs | T_LABEL_01 (~line 183-188) | "Quick ALL" / "Quick ALL +4t" | "3x All" / "3x All +4t" | YES |
| E | B73Tests.cs | T_LABEL_03 (~line 201-205) | "Quick ALL" | "3x All" | YES |
| F | B73Tests.cs | T_LABEL_04 (~line 210-215) | "Quick ALL" / "Quick ALL +0t" | "3x All" / "3x All +0t" | YES |

All 6 stale assertion fix locations covered (CopyEngineTests.cs x3, B73Tests.cs x3). **PASS.**

### B4. T2 dotnet Test Gate

Plan specifies: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build` with 0 failures required.
Named tests T_B189_T4_01, T_B189_T4_02, T_B189_T4_06 and T_LABEL_01, T_LABEL_03, T_LABEL_04 must PASS. **PASS.**

### B5. T3 -- Spec Update Points

| Section | Description | Covered |
|---------|-------------|---------|
| Section 1 | Button label rename table (Quick->3x, Quick2t->2x, Quick ALL->3x All, QAll2t->2x All) | YES |
| Section 2 | Block history table -- add B193 row | YES |
| Section 3 | InstrumentDefaults table -- verify ES, NQ, MNQ, GC, MGC rows | YES |
| Section 4 | SetInstrument() AC for per-panel _quickAllT1 behavior | YES |
| Section 5 | DW-B192-SINST-01 open card | YES |
| Section 6 | DW-B192-QALL-RESET-01 -- close with resolution note | YES |

All 6 spec update points covered. **PASS.**

### B6. 7-Scan Checklist per Ticket

#### T1 (B193Tests.cs)

| Scan | Stated | Scope |
|------|--------|-------|
| SCAN-01 CYC | YES | each [Fact] method CYC=1; helpers CYC=1; all <= 8 |
| SCAN-02 lock() | YES | grep "lock(" on B193Tests.cs -- 0 matches required |
| SCAN-03 async void | YES | grep "async void " -- 0 matches required |
| SCAN-04 return null | YES | grep "return null;" -- 0 matches required |
| SCAN-05 throw new | YES | grep "throw new" -- 0 matches required |
| SCAN-06 ASCII | YES | all string literals verified ASCII |
| SCAN-07 JS-033 | YES | all [Fact] methods synchronous void |

#### T2 (CopyEngineTests.cs + B73Tests.cs)

| Scan | Stated | Scope |
|------|--------|-------|
| SCAN-01 CYC | YES | no new branches; existing CYC unchanged |
| SCAN-02 lock() | YES | 0 new matches on edited lines |
| SCAN-03 async void | YES | no new async void introduced |
| SCAN-04 return null | YES | no new return null |
| SCAN-05 throw new | YES | no new throw new |
| SCAN-06 ASCII | YES | "3x", "2x", "3x All", "2x All" verified ASCII |
| SCAN-07 JS-033 | YES | no change to async method signatures |
| POST-GATE | YES | dotnet test -- 0 failures |

#### T3 (specs/002-trade-copier-spec.html)

| Scan | Stated | Notes |
|------|--------|-------|
| SCAN-01..05 | YES (N/A stated) | HTML file -- correct to mark N/A |
| SCAN-06 ASCII | YES | verify no Unicode in newly added text |
| SCAN-07 JS-033 | YES (N/A stated) | Correct |

All 7 scans present per ticket. **PASS.**

### B7. DW-B183-03 Carry-Forward

Plan includes in Deferred Work Register:

> DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen. Any branch addition without prior helper extraction is a P1 JS-066 violation. | OPEN (B183-B193, unmodified)

Matches B191 backlog entry exactly. **PASS.**

### B8. P0 JS Rule Violations in Proposed Code

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in any proposed code | PASS -- test file uses reflection + Assert only |
| JS-001 | No throw in hot paths / test methods | PASS -- no throw statements in proposed code |
| JS-002 | No return null | PASS -- helpers return string; [Fact] methods return void |
| JS-033 | No async void | PASS -- all methods synchronous void |
| JS-066 | CYC <= 8 | PASS -- all methods CYC=1 |
| JS-081 | ASCII-only string literals | PASS -- all literals are ASCII |
| NT8 | async/await in lifecycle hooks | PASS -- no NT8 lifecycle hooks in test file |
| NT8 | DateTime.Now (not UtcNow) | PASS -- no DateTime usage |
| NT8 | CreateOrder without PTT- prefix | PASS -- no CreateOrder calls |
| NT8 | Hardcoded hex colors | PASS -- no hex color strings |

Zero P0 violations. **PASS.**

### B9. Reflection Helper Signatures

| Helper | Plan signature | Pattern source | Consistent? |
|--------|---------------|----------------|-------------|
| InvokeFormatBuffer | `private static string InvokeFormatBuffer(string name, int ticks)` | B189T4SpinnerTests (CopyEngineTests.cs:10566) uses `private static object` with cast at call site; plan returns `string` with internal cast | ACCEPTABLE -- semantically equivalent; cast moved inside helper. Not a rule violation. |
| InvokeFormatQuickAllBuffer | `private static string InvokeFormatQuickAllBuffer(string name, int ticks)` | New helper; mirrors same reflection pattern | PASS |

Note: The plan claims to "mirror" the B189T4SpinnerTests pattern. The actual existing helper (`B189T4SpinnerTests.InvokeFormatBuffer`) returns `object`, not `string`. The plan's helper returns `string` by performing the cast internally: `return (string)m.Invoke(null, new object[] { name, ticks });`. This is a valid and stricter typing; it is not a violation of any JS rule. The behavior is identical. **PASS.**

---

## Section C -- Spec Alignment

### C1. Button Rename Table vs B193-DIRECT-3

The plan states B193-DIRECT-3 (commit 3a098a88) implemented:
- Quick -> 3x
- Quick2t -> 2x
- Quick ALL -> 3x All
- QAll2t -> 2x All

Verification against live test artifacts:
- `CopyEngineTests.cs` line 10579: still contains `"Quick2t"` (stale -- this IS the T2 edit target) -- confirms old label was "Quick2t".
- `CopyEngineTests.cs` line 10588: still contains `"QAll2t +4"` (stale -- T2 target) -- confirms old label.
- `CopyEngineTests.cs` lines 10648-10649: still contains `"Quick"` and `"Quick2t"` (stale -- T2 target).
- Rename table in plan matches the stale strings present in tests and the new strings expected after T2 edit.

**Rename table is consistent with B193-DIRECT-3 production state. PASS.**

### C2. Test Cases T_B193_01..04 vs Actual InstrumentDefaults Values (B192)

Cross-referenced against `src/PropTraderTools/Features/PttQuickExit.cs` lines 387-404:

| Test | Plan expects | Actual code returns | Match |
|------|-------------|-------------------|-------|
| T_B193_01: GetQuickTicks("MGC") | (10, 20) | `if masterName.StartsWith("MGC") return (10, 20)` | YES |
| T_B193_02: GetQuickTicks("GC") | (10, 20) | `if masterName.StartsWith("GC") return (10, 20)` | YES |
| T_B193_03: GetQuickTicks("MES") | (4, 8) | `if masterName.StartsWith("MES") return (4, 8)` | YES |
| T_B193_04: GetQuickTicks("ES") | (4, 8) | `if masterName.StartsWith("ES") return (4, 8)` | YES |

All 4 assertions match the live InstrumentDefaults implementation on main. **PASS.**

### C3. T_B193_08 FormatQuickAllBuffer vs FormatBuffer Distinction

Plan correctly distinguishes:
- T_B193_07 uses `InvokeFormatBuffer("2x All", 10)` -> `"2x All +10"` (no `t` suffix) via `FormatBuffer`
- T_B193_08 uses `InvokeFormatQuickAllBuffer("3x All", 10)` -> `"3x All +10t"` (with `t` suffix) via `FormatQuickAllBuffer`

Production code reference (plan lines 59-68): `FormatBuffer` returns `name + " +" + ticks`; `FormatQuickAllBuffer` returns `name + " +" + ticks + "t"`. The test assertions correctly reflect this distinction. **PASS.**

---

## Section D -- Violation Log

| # | Rule ID | Description | Location in Plan | Severity |
|---|---------|-------------|-----------------|----------|
| (none) | -- | -- | -- | -- |

**Zero violations found.**

---

## Section E -- Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| 10 new B193 formalization tests | YES | T1 |
| Stale assertion fix: CopyEngineTests.cs T_B189_T4_01 | YES | T2 Edit block A |
| Stale assertion fix: CopyEngineTests.cs T_B189_T4_02 | YES | T2 Edit block B |
| Stale assertion fix: CopyEngineTests.cs T_B189_T4_06 | YES | T2 Edit block C |
| Stale assertion fix: B73Tests.cs T_LABEL_01 | YES | T2 Edit block D |
| Stale assertion fix: B73Tests.cs T_LABEL_03 | YES | T2 Edit block E |
| Stale assertion fix: B73Tests.cs T_LABEL_04 | YES | T2 Edit block F |
| dotnet test gate after T2 (0 failures) | YES | T2 Post-edit gate |
| Spec Section 1: button label rename table | YES | T3 Section 1 |
| Spec Section 2: block history table B193 row | YES | T3 Section 2 |
| Spec Section 3: InstrumentDefaults table rows | YES | T3 Section 3 |
| Spec Section 4: SetInstrument() AC for _quickAllT1 | YES | T3 Section 4 |
| Spec Section 5: DW-B192-SINST-01 open card | YES | T3 Section 5 |
| Spec Section 6: DW-B192-QALL-RESET-01 close | YES | T3 Section 6 |
| DW-B183-03 (P1) carry-forward | YES | Deferred Work Register |
| Lane-split gate compliance | YES | LANE-SPLIT GATE RESULT section |
| T1->T2->T3 sequential ordering | YES | Ticket Ordering Summary |

All spec requirements addressed. **PASS.**

---

## Section F -- Notes (Non-Blocking)

1. **InvokeFormatBuffer return type**: Plan helper returns `string`; existing B189T4SpinnerTests pattern returns `object`. Both are valid. The plan's version is stricter (no call-site cast required). No action needed.

2. **DW-B193-01 (P3, OPEN)**: Plan correctly identifies that `OnQuickAllBufferChanged` (~line 1027-1029 in TradeCopierPanel.cs) still uses old label strings "Quick ALL"/"QAll2t" and notes this is low-severity (singleton-sink callback path, rarely triggered since B193-DIRECT-2 moved display to per-panel). Correctly deferred to future block.

3. **T3 Section 3 scope**: The plan says to "verify" InstrumentDefaults rows for ES, NQ, MNQ, GC, MGC. These were added by B192. If any are missing, T3 adds them. The verification-then-add pattern is correct -- no assumption about current spec state.

---

## FINAL VERDICT: REVIEW_PASS

- Lane-split gate: **LANES-APPROVED** (all 4 questions correctly answered)
- Ticket ordering: **T1->T2->T3 sequential** (confirmed)
- Test cases T_B193_01..10: **all 10 present and correct**
- T2 stale fix locations: **all 6 covered**
- T2 dotnet test gate: **present**
- T3 spec sections: **all 6 covered**
- 7-scan checklist: **all 7 scans per ticket**
- DW-B183-03 carry-forward: **present**
- P0 JS violations: **zero**
- Reflection helper signatures: **valid**
- Spec alignment (rename table + InstrumentDefaults values): **verified against live code**

**Phase 3 (ticket generation) is UNBLOCKED.**

---

*Written by: ptt-plan-reviewer*
*Epic: B193*
*Phase: 2 (Plan Review)*
*Status: REVIEW_PASS*
