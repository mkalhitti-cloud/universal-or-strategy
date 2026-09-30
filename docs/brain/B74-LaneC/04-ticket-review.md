# Ticket Review: B74-LaneC (REVIEW CYCLE 2)

**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Ticket file**: `docs/brain/B74-LaneC/04-tickets.md` (REVISION CYCLE 1)
**Plan status**: REVIEW_PASS confirmed in `docs/brain/B74-LaneC/02-plan-review.md`
**Cycle**: Re-review after architect fixed T6 and T7 violations from Cycle 1

**Sources read**:
1. `docs/brain/B74-LaneC/04-tickets.md` (revised)
2. `docs/brain/B74-LaneC/02-architecture-plan.md`
3. `docs/brain/B74-LaneC/02-plan-review.md`
4. `docs/standards/jane-street/RULES_CATALOG.md`
5. `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
6. `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
7. `src/PropTraderTools/Features/PttQuickExit.cs`

---

## Ticket-1 Review

### T1 — Traceability

Every test group and test ID maps to a named hotfix:

| Test Group | Test IDs | Hotfix(es) | Plan Section 5 |
|------------|----------|------------|---------------|
| Group A (BE Buffer Relay) | T_BE_BUF_RELAY_01..03 | B74-C-01, B74-C-02 | ✅ |
| Group B (GlobalQuickAllT1) | T_QA_EXEC_01..03 + bound tests | B74-C-03 | ✅ |
| Group C (N-Bracket Quick Exit) | T_QX_T3_01..09 | B74-C-04 | ✅ |
| Group D (SnapshotStopPrice) | T_SNAP_STOP_01..04 | B74-C-05 | ✅ |

All 5 hotfix IDs (B74-C-01..05) are present in Section 1 mapping table.
No phantom work (items in ticket not in plan/spec): none found.
No missing work (items in plan/spec missing from ticket): none found.

**Traceability: PASS**

---

### T2 — JS Pre-Check (Rule Violations in Ticket Descriptions)

Scanned all test body code snippets in Section 5 and the constraint table in Section 4:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` described in any test | PASS — no lock() present |
| JS-001 | No `throw new XxxException` in any test body | PASS — no throw new |
| JS-002 | No `return null` in any test body | PASS — no return null |
| JS-033 | No `async void` described | PASS — all methods synchronous |
| JS-008/009 | No mutable struct fields, no unfrozen SolidColorBrush | PASS — not applicable |
| NT8 constraints | No `sealed` on window, no `FontFamily`, no hex color, no `DateTime.Now`, no order name not starting "PTT-" | PASS — order names use "PTT-QX-*" convention |

Note: `IncrementQuickAll()` and `DecrementQuickAll()` are called directly in Group B bound tests
(T_QA_EXEC_03 additional tests). The ticket explicitly documents that these calls are safe in
xUnit context because the field mutation is synchronous and the `Dispatcher.InvokeAsync` for
the event broadcast is fire-and-forget (does not throw when Application.Current is null at
time of field mutation). This is architecturally defensible for testing the field mutation path.

**JS Pre-Check: PASS**

---

### T3 — CYC Pre-Check

Section 4 states: "Each `[Fact]` method must have CYC <= 8. Tests using `FieldInfo` reflection
loops, `Record.Exception`, and linear `Assert.*` calls stay at CYC 1-3." All test bodies in
Section 5 are linear Assert chains or single-expression computations. No test body describes
a branching structure that could approach CYC 8. S6 scan is explicitly required in Section 6.

No method with estimated CYC > 8 is described.

**CYC Pre-Check: PASS**

---

### T4 — NT8 Constraints

Concrete fake/stub approach documented per group in Section 4 ("NT8 type constraint" block):

| Group | NT8 Types Involved | Documented Approach |
|-------|--------------------|---------------------|
| Group A | `PttGlobalBreakEven` (pure C# — no NT8) | Instantiate directly; reflect on `_globalBeBuffer` field |
| Group B | `CopyEngine.Instance` (singleton) | Use `CopyEngine.Instance` per `CopyEngineTests.cs` pattern; reflect for field reset |
| Group C | `PttQuickExit.Execute` requires `Account` | Pure-logic extraction: compute targetCount/tNTicks/tNPrice/tNQty/names as inline expressions; name predicate extracted from `SnapshotTargetOrders`; compat overload verified via reflection |
| Group D | `SnapshotStopPrice` requires `Account`, `Order` | Filter predicate extracted inline using strings (no NT8 types); method existence verified via `MethodInfo` reflection |

INTEGRATION-ONLY markers are present for:
- Group A: Dispatcher path in IncrementBuffer/DecrementBuffer relay (F5 gate)
- Group B: `GlobalQuickAllBufferChanged` event broadcast via Dispatcher.InvokeAsync (F5 gate)
- Group D: `SnapshotStopPrice` full runtime execution with live NT8 Account/Orders (F5 gate)

**NT8 Check: PASS**

---

### T5 — xUnit Only

Section 2 states: "Framework: xUnit ONLY. Never NUnit. Never MSTest."
Required `using` directives list only `Xunit` — no `NUnit` or `Microsoft.VisualStudio.TestTools`.
All test attributes shown in Section 5 are `[Fact]`. No MSTest or NUnit attributes appear.
S7 scan (Section 6) explicitly checks for `NUnit|MSTest|Microsoft\.VisualStudio\.TestTools`.

**xUnit Only: PASS**

---

### T6 — Completeness (all 19 test IDs present)

Plan Section 5 defines 19 test IDs. Ticket Section 5 coverage:

| ID | Present in Ticket Section 5 | Method Name |
|----|----------------------------|-------------|
| T_BE_BUF_RELAY_01 | ✅ | `GlobalBeBuffer_ReflectionSet_Increment_PropertyReturnsNewValue` |
| T_BE_BUF_RELAY_02 | ✅ | `GlobalBeBuffer_ReflectionSet_Decrement_PropertyReturnsNewValue` |
| T_BE_BUF_RELAY_03 | ✅ | `GlobalBeBuffer_ReflectionSet_AtCeiling_ReturnsTen` + `AtFloor_ReturnsNegTen` (2 [Fact]s) |
| T_QA_EXEC_01 | ✅ | `GlobalQuickAllT1_Default_IsFour` |
| T_QA_EXEC_02 | ✅ (REVISED) | `InstrumentDefaults_GetQuickTicks_MES_ReturnsFourAndEight` |
| T_QA_EXEC_03 | ✅ (REVISED) | `Execute_TargetCount_FallbackToTwoProxy_WhenSnapshotEmpty` |
| T_QX_T3_01 | ✅ | `Execute_TargetCount_FromSnapshotWhenThreeEntries` |
| T_QX_T3_02 | ✅ | `Execute_TargetCount_FallbackToTwoWhenSnapshotEmpty` |
| T_QX_T3_03 | ✅ | `Execute_ProportionalTickSpacing_LongPosition` |
| T_QX_T3_04 | ✅ | `Execute_TnQty_FromSnapshotQty` |
| T_QX_T3_05 | ✅ | `Execute_TnQty_FallbackSplitWhenNoSnapshot` |
| T_QX_T3_06 | ✅ | `Execute_IndependentOcoIdsPerPair` |
| T_QX_T3_07 | ✅ | `Execute_StopAndTargetNames_FollowPttQxConvention` |
| T_QX_T3_08 | ✅ | `Execute_CompatOverload_DelegatesToPrimaryWithEmptyList` |
| T_QX_T3_09 | ✅ | `SnapshotTargetOrders_NameFilter_IncludesTargetPatterns` |
| T_SNAP_STOP_01 | ✅ | `SnapshotStopPrice_FullNameMatch_DifferentRefs_IsIncluded` |
| T_SNAP_STOP_02 | ✅ | `SnapshotStopPrice_MethodExists_StaticWithTwoParams` |
| T_SNAP_STOP_03 | ✅ | `SnapshotStopPrice_NullInstrumentOnOrder_IsSkipped` |
| T_SNAP_STOP_04 | ✅ | `SnapshotStopPrice_FullNameMismatch_IsSkipped` |

Note on T_QA_EXEC_02/03 revision: Plan Section 5 originally described event broadcast testing.
The revision correctly redirects T_QA_EXEC_02 to the `InstrumentDefaults.GetQuickTicks("MES")`
fallback (directly callable without Dispatcher), and T_QA_EXEC_03 to the targetCount=2 proxy
expression. Both have explicit INTEGRATION-ONLY markers for the Dispatcher-dependent broadcast
path. This is a valid scope reduction for the xUnit gate; the F5 manual gate covers the rest.

**Completeness: PASS**

---

### T7 — Testability: Group A reflection-only, no CopyEngine.Instance.RaiseBeBufferChanged from [Fact]

Verification against ticket Section 5 Group A test bodies:

**T_BE_BUF_RELAY_01** body:
- Uses `typeof(PttGlobalBreakEven).GetField("_globalBeBuffer", BindingFlags.NonPublic | BindingFlags.Instance)`
- Uses `fi.SetValue(gbe, 1)` — reflection direct field set
- Calls `gbe.GlobalBeBuffer` — property getter only
- Does NOT call `IncrementBuffer()` ✅
- Does NOT call `DecrementBuffer()` ✅
- Does NOT call `CopyEngine.Instance.RaiseBeBufferChanged(...)` ✅

**T_BE_BUF_RELAY_02** body:
- Uses reflection `fi.SetValue(gbe, -1)`
- Calls `gbe.GlobalBeBuffer` — property getter only
- Does NOT call `IncrementBuffer()` ✅
- Does NOT call `DecrementBuffer()` ✅
- Does NOT call `CopyEngine.Instance.RaiseBeBufferChanged(...)` ✅

**T_BE_BUF_RELAY_03** bodies (2 [Fact]s):
- Uses reflection `fi1.SetValue(gbe1, 10)` and `fi2.SetValue(gbe2, -10)`
- Calls `gbe1.GlobalBeBuffer` and `gbe2.GlobalBeBuffer` — property getter only
- Does NOT call `IncrementBuffer()` ✅
- Does NOT call `DecrementBuffer()` ✅
- Does NOT call `CopyEngine.Instance.RaiseBeBufferChanged(...)` ✅

INTEGRATION-ONLY comments are present in all three Group A test specifications, per revision note.

Source confirmation (PttGlobalBreakEven.cs lines 90–100): `IncrementBuffer` and `DecrementBuffer`
unconditionally call `CopyEngine.Instance.RaiseBeBufferChanged(...)` after the guard. That relay
method calls `System.Windows.Application.Current.Dispatcher.InvokeAsync(...)`. In xUnit context
`Application.Current` is null; calling the relay would throw `NullReferenceException`. The
reflection-only approach correctly bypasses this entire call chain.

**Testability (Group A reflection, no Dispatcher NRE): PASS**

---

### T8 — Method Signatures Match Source

Signatures verified against direct source reads:

| Signature in Ticket | Source Location | Match |
|--------------------|----------------|-------|
| `internal void IncrementBuffer()` CYC=2 | `PttGlobalBreakEven.cs` line 90 | ✅ exact |
| `internal void DecrementBuffer()` CYC=2 | `PttGlobalBreakEven.cs` line 96 | ✅ exact |
| `internal int GlobalBeBuffer { get; }` CYC=1 | `PttGlobalBreakEven.cs` line 88 | ✅ exact |
| `private static (int t1, int t2) ResolveQuickTicks(Instrument instr)` CYC=2 | `PttGlobalQuickExit.cs` line 58 | ✅ exact |
| `private static List<(double Price, int Qty)> SnapshotTargetOrders(Account acc, NinjaTrader.Cbi.Instrument instr)` CYC=4 | `PttGlobalQuickExit.cs` line 87 | ✅ exact |
| `internal void Execute(Account leader, Instrument instr, int t1Ticks, List<(double Price, int Qty)> targets, bool skipIfFollower = true)` CYC=8 | `PttQuickExit.cs` line 36 | ✅ exact |
| `internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks, bool skipIfFollower = true)` CYC=1 | `PttQuickExit.cs` line 168 | ✅ exact |
| `private static double SnapshotStopPrice(Account acc, Instrument instr)` CYC=2 | `PttQuickExit.cs` line 179 | ✅ exact |

Name-filter predicate in T_QX_T3_09 correctly reproduces the 4-condition boolean from
`PttGlobalQuickExit.cs` lines 100–106 (StartsWith "Target" + digit at [6]; "PTT-QX-T" + digit
at [8]; "PTT-BE-Target-"). Match confirmed against source.

**Method Signatures: PASS**

---

### T9 — Completion Artifact (ticket-1-completion.md)

Section 7 specifies:
- **File**: `docs/brain/B74-LaneC/ticket-1-completion.md` ✅
- **Required format** with exact section headings ✅
- **Test method table** with all 22 test method names, group, and hotfix ID ✅
- **Scan Results** section with S1..S7 command/output placeholders ✅
- **Build Result** section with `dotnet build` and `dotnet test` output placeholders ✅
- **Verdict** field `BUILD_PASS | BUILD_FAIL` ✅

**Completion Artifact: PASS**

---

### T10 — Dual-Section Scan Checklist (Section 4 AND Section 6)

**Section 4 checklist** (constraint table — engineer contract):
| Scan | Rule | Constraint | Expected |
| S1 | JS-021 | No lock() | 0 matches | ✅ present |
| S2 | JS-001 | No throw new | 0 matches | ✅ present |
| S3 | JS-002 | No return null | 0 matches | ✅ present |
| S4 | JS-033 | No async void | 0 matches | ✅ present |
| S5 | JS-066 | No non-ASCII | 0 bytes | ✅ present |
| S6 | JS-067 | CYC <= 8 | All pass | ✅ present |
| S7 | Testing | xUnit only | 0 NUnit/MSTest | ✅ present |

All 7 scans present in Section 4. ✅

**Section 6 checklist** (exact grep/powershell commands — engineer execution contract):
| Scan | Exact command present |
|------|----------------------|
| S1 | `Select-String ... -Pattern "lock\s*\("` ✅ |
| S2 | `Select-String ... -Pattern "throw\s+new"` ✅ |
| S3 | `Select-String ... -Pattern "return\s+null"` ✅ |
| S4 | `Select-String ... -Pattern "async\s+void"` ✅ |
| S5 | `[System.IO.File]::ReadAllBytes(...)` byte count ✅ |
| S6 | `python scripts/complexity_audit.py src/PropTraderTools/Tests/B74LaneCTests.cs` ✅ |
| S7 | `Select-String ... -Pattern "NUnit|MSTest|Microsoft\.VisualStudio\.TestTools"` ✅ |

All 7 scans present in Section 6 with exact commands. ✅

The completion artifact template in Section 7 also contains S1..S7 result slots,
providing Layer 2 (engineer self-report) and anchoring the verifier's Layer 3 cross-check.
Per-ticket scan checklists are intentional defense-in-depth (3 layers). ✅

**Dual-Section Scan Checklist: PASS**

---

## Cycle 2 Focus: Prior Violation Re-Check

### T6 Violation (was FAIL in Cycle 1): T_QA_EXEC_02/03 event broadcast coverage

**Prior violation**: T_QA_EXEC_02 described testing `GlobalQuickAllBufferChanged` event with
value `5` (requires Dispatcher.InvokeAsync — not capturable in xUnit). T_QA_EXEC_03 described
testing the event with value `3` (same problem).

**Fix verification**:
- T_QA_EXEC_02 now tests `InstrumentDefaults.GetQuickTicks("MES")` directly — pure C# call,
  returns `(4, 8)` without any WPF dependency. This is the exact fallback path in
  `ResolveQuickTicks` when `engine == null` (source: `PttGlobalQuickExit.cs` line 61). ✅
- T_QA_EXEC_03 now tests the `targetCount` fallback expression (`(emptyTargets != null &&
  emptyTargets.Count > 0) ? emptyTargets.Count : 2`) — pure C# expression, no NT8 types. ✅
- Both revised tests carry explicit INTEGRATION-ONLY comments for the Dispatcher-dependent
  event broadcast path. ✅
- The field-mutation synchronous path for `IncrementQuickAll`/`DecrementQuickAll` is still
  covered by the additional bound tests (`IncrementQuickAll_AtCeiling99_DoesNotExceed99`,
  `DecrementQuickAll_AtFloor1_DoesNotGoBelowOne`). ✅

**T6 violation: FIXED** ✅

### T7 Violation (was FAIL in Cycle 1): Group A Dispatcher NRE

**Prior violation**: Group A test bodies called `IncrementBuffer()`/`DecrementBuffer()` directly,
which unconditionally calls `CopyEngine.Instance.RaiseBeBufferChanged(...)`, which calls
`Application.Current.Dispatcher.InvokeAsync(...)`, throwing NRE when `Application.Current` is
null in xUnit context.

**Fix verification**:
- T_BE_BUF_RELAY_01: uses only `fi.SetValue(gbe, 1)` + `gbe.GlobalBeBuffer` — no call to
  `IncrementBuffer` or `DecrementBuffer` or `RaiseBeBufferChanged`. ✅
- T_BE_BUF_RELAY_02: uses only `fi.SetValue(gbe, -1)` + `gbe.GlobalBeBuffer`. ✅
- T_BE_BUF_RELAY_03: uses only `fi1.SetValue(gbe1, 10)` / `fi2.SetValue(gbe2, -10)` +
  `GlobalBeBuffer` property reads. ✅
- All three Group A test bodies have INTEGRATION-ONLY comments for the relay/Dispatcher path. ✅
- Source confirmed: relay call IS unconditional (outside the guard in PttGlobalBreakEven.cs
  lines 90–100). The reflection approach correctly sidesteps the NRE. ✅

**T7 violation: FIXED** ✅

---

## Overall Verdict Summary

| Check | Result |
|-------|--------|
| T1 — Traceability | PASS |
| T2 — JS Pre-Check (7-scan contract + rule violations) | PASS |
| T3 — CYC Pre-Check (CYC <= 8 scan present) | PASS |
| T4 — NT8 Constraints (fake/stub approach per group) | PASS |
| T5 — xUnit ONLY | PASS |
| T6 — Completeness (all 19 test IDs present, revised T_QA_EXEC_02/03) | PASS |
| T7 — Testability (Group A reflection-only, no Dispatcher NRE) | PASS |
| T8 — Method signatures match source | PASS |
| T9 — Completion artifact (ticket-1-completion.md specified in Section 7) | PASS |
| T10 — Dual-section scan checklist (Section 4 AND Section 6) | PASS |

**Violations**: None

---

## TICKET_REVIEW_PASS
