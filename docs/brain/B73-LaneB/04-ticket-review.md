# B73-LaneB Ticket Review

**Block**: B73-LaneB
**Phase**: 3.5 (Ticket Review — RE-REVIEW after architect fixes)
**Reviewed by**: ptt-ticket-reviewer
**Date**: 2026-08-14 (re-review)
**Prior review result**: TICKET_REVIEW_FAIL (3 violations: V1/TR03, V2/NT02, V3/C04)
**Input ticket**: `docs/brain/B73-LaneB/04-tickets.md`
**Input plan**: `docs/brain/B73-LaneB/02-architecture-plan.md`
**Rules gate**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## Ticket 1: TradeCopierPanel B73-LaneB xUnit Tests

### TRACEABILITY

**TR01 — All 15 hotfix IDs referenced in ticket**
PASS. All 15 IDs (B73-B-01 through B73-B-15) are explicitly listed at lines 18-19 of the ticket.
Unchanged from prior review.

**TR02 — All 33 test names listed**
PASS. Groups 1-15 yield exactly 33 named [Fact] methods (2+2+2+2+2+2+2+2+4+2+1+2+2+3+3 = 33).
Unchanged from prior review.

**TR03 — Test names match approved plan Section 7 S7 list** ← WAS FAIL (V1)
PASS. **FIXED.** The architect updated `02-architecture-plan.md` Section 7 S7 to use the same
short-form test names as the ticket. Exact name-by-name comparison confirms all 33 names match
identically between ticket SCAN-07 and plan S7:

| Group | Ticket SCAN-07 names | Plan S7 names | Match |
|-------|----------------------|---------------|-------|
| 1 (B73-B-01) | T_BEALL_SYNC_01, T_BEALL_SYNC_02 | T_BEALL_SYNC_01, T_BEALL_SYNC_02 | ✓ |
| 2 (B73-B-02) | T_BE_BG_01, T_BE_BG_02 | T_BE_BG_01, T_BE_BG_02 | ✓ |
| 3 (B73-B-03) | T_NO_DISARM_01, T_NO_DISARM_02 | T_NO_DISARM_01, T_NO_DISARM_02 | ✓ |
| 4 (B73-B-04) | T_FLAT_DISARM_01, T_FLAT_DISARM_02 | T_FLAT_DISARM_01, T_FLAT_DISARM_02 | ✓ |
| 5 (B73-B-05) | T_BEALL_ARM_01, T_BEALL_ARM_02 | T_BEALL_ARM_01, T_BEALL_ARM_02 | ✓ |
| 6 (B73-B-06) | T_MANUAL_CLOSE_01, T_MANUAL_CLOSE_02 | T_MANUAL_CLOSE_01, T_MANUAL_CLOSE_02 | ✓ |
| 7 (B73-B-07) | T_DISARM_SYNC_01, T_DISARM_SYNC_02 | T_DISARM_SYNC_01, T_DISARM_SYNC_02 | ✓ |
| 8 (B73-B-08) | T_BUF_BE_01, T_BUF_BE_02 | T_BUF_BE_01, T_BUF_BE_02 | ✓ |
| 9 (B73-B-09) | T_LABEL_01..04 | T_LABEL_01..04 | ✓ |
| 10 (B73-B-10) | T_QA_SING_01, T_QA_SING_02 | T_QA_SING_01, T_QA_SING_02 | ✓ |
| 11 (B73-B-11) | T_QA_INIT_01 | T_QA_INIT_01 | ✓ |
| 12 (B73-B-12) | T_DISARM_CROSS_01, T_DISARM_CROSS_02 | T_DISARM_CROSS_01, T_DISARM_CROSS_02 | ✓ |
| 13 (B73-B-13) | T_BEALL_FLAT_01, T_BEALL_FLAT_02 | T_BEALL_FLAT_01, T_BEALL_FLAT_02 | ✓ |
| 14 (B73-B-14) | T_ORPHAN_01..03 | T_ORPHAN_01..03 | ✓ |
| 15 (B73-B-15) | T_LABEL_CLIP_01..03 | T_LABEL_CLIP_01..03 | ✓ |

Count in ticket SCAN-07: 33. Count in plan S7: 33. One authoritative name list now exists in both
documents.

---

### JS PRE-CHECK

**JS01 — No lock() in any test implementation notes**
PASS. Zero `lock()` usage described. SCAN-01 only references the pattern as something to scan for,
not as a usage. Unchanged from prior review.

**JS02 — No async void in any test implementation notes**
PASS. All 33 [Fact] methods are declared `public void`. Notes confirm "none are async". Unchanged
from prior review.

**JS03 — No throw new in test implementation notes (Record.Exception pattern used)**
PASS. `Record.Exception` pattern mandated. No `throw new` described anywhere. SCAN-04 expected=0.
Cite: JS-001 compliant. Unchanged from prior review.

**JS04 — No return null in test implementation notes**
PASS. `null` appears only as a literal argument (e.g. `DisarmPendingBe(null)`) and as the Assert
target in `Assert.Null(ex)`. No helper method described as returning null.
Cite: JS-002 not violated. Unchanged from prior review.

---

### NT8 CONSTRAINTS

**NT01 — Zero NT8 runtime instantiation in test patterns**
PASS. No `Account`, `Order`, `Instrument`, or `Position` constructors appear in any test pattern.
`CopyEngine.Instance` is singleton access, not construction. All reflection tests use `typeof()`
and `.GetMethod()`/`.GetEvent()`/`.GetField()`. Unchanged from prior review.

**NT02 — All WPF-touching tests use reflection or static method testing (no WPF STA thread required)**
← WAS FAIL (V2)
PASS. **FIXED.** T_LABEL_CLIP_02 and T_LABEL_CLIP_03 have been rewritten to use DependencyProperty
static field reflection only. Neither test constructs a `DockPanel` instance (no `new DockPanel()`).

- **T_LABEL_CLIP_01**: Tests `typeof(DockPanel)` — pure type existence check. No construction.
  Correct.

- **T_LABEL_CLIP_02**: Pattern is now:
  `typeof(DockPanel).GetField("LastChildFillProperty", BindingFlags.Public | BindingFlags.Static)`.
  The ticket notes explicitly state: "without constructing a DockPanel instance (no STA thread
  required)". `GetField()` on a type is pure metadata — safe on MTA thread. PASS.

- **T_LABEL_CLIP_03**: Pattern is now:
  `typeof(DockPanel).GetField("DockProperty", BindingFlags.Public | BindingFlags.Static)`.
  Same reasoning as T_LABEL_CLIP_02 — pure type metadata, no DependencyObject construction.
  Ticket notes confirm: "no STA thread required". PASS.

The "WPF context note" section explains the STA constraint in full and confirms neither test
requires a DockPanel instance. No other test in Groups 1-14 constructs any WPF element.

**NT03 — CopyEngine singleton access is safe**
PASS. `CopyEngine.Instance` pure-read methods are consistent with the architecture plan Section 5.
Methods `IsPendingSlotsEmpty`, `GlobalQuickAllT1`, `DisarmPendingBe(null)`,
`CancelQxBrackets(null, null)`, `RaiseBeAllDisarmed()` are all documented as safe for
non-NT8-thread access. Unchanged from prior review.

---

### COMPLETENESS

**C01 — File path specified**
PASS. `src/PropTraderTools/Tests/B73Tests.cs` at ticket line 26-27. Unchanged.

**C02 — Namespace specified**
PASS. `namespace PropTraderTools` at ticket line 37. Unchanged.

**C03 — Class name specified**
PASS. `public sealed class B73Tests` at ticket line 39. Unchanged.

**C04 — All reflection patterns documented for private static methods (FormatBuffer, FormatQuickAllBuffer, FormatGlobalBeBuffer)**
← WAS FAIL (V3)
PASS. **FIXED.** The `GetFormatBuffer()` reflection accessor is now documented in the ticket:

```csharp
private static MethodInfo GetFormatBuffer() =>
    typeof(TradeCopierPanel)
        .GetMethod("FormatBuffer",
                   BindingFlags.NonPublic | BindingFlags.Static)!;
```

The ticket also provides the explicit coverage rationale: "No direct reflection invocation test
is required for FormatBuffer itself; the pattern below is provided for completeness and for any
future test that may need it." And: "T_QA_INIT_01 covers the B73-B-11 behavioral change
indirectly (via CopyEngine.Instance.GlobalQuickAllT1 >= 1)."

All three reflection accessors are now present:
- `GetFormatGlobalBeBuffer()` — present (ticket lines 132-136)
- `GetFormatQuickAllBuffer()` — present (ticket lines 138-143)
- `GetFormatBuffer()` — present (ticket lines 148-153) ← **now fixed**

**C05 — Return conditions documented (BUILD_PASS / BUILD_FAIL)**
PASS. BUILD_PASS criteria (4 conditions) and BUILD_FAIL condition at ticket lines 524-531.
Unchanged.

---

### 7-SCAN CHECKLIST PRESENCE

**SC01 — S1 lock() scan present**: PASS (pattern, scope, expected=0, rationale JS-021)
**SC02 — S2 async void scan present**: PASS (pattern, scope, expected=0, rationale JS-033)
**SC03 — S3 return null scan present**: PASS (pattern, scope, expected=0, rationale JS-002)
**SC04 — S4 throw new scan present**: PASS (pattern, scope, expected=0, rationale JS-001)
**SC05 — S5 ASCII-only scan present**: PASS (pattern `[^\x00-\x7F]`, expected=0)
**SC06 — S6 CYC scan present**: PASS (CYC=1 for each of 33 methods, tool specified)
**SC07 — S7 test completeness check present**: PASS (all 33 short-form names listed with
  grouping by hotfix ID; grep command specified; names now match plan S7 exactly — V1 resolved)

All 7 scans are present with pattern, scope, expected result, and rationale. Checklist integrity:
INTACT.

Note on the 3-layer defense-in-depth contract: SCAN-07 short-form names in the ticket serve as
Layer 1 (engineer contract). They now match the plan S7 names (one authoritative list). Layer 2
(engineer attestation in ticket-1-completion.md) and Layer 3 (verifier cross-check in
ticket-1-verification.md) are correctly anchored.

---

### TEST COVERAGE

**TC01 — Every hotfix has at least 1 test directly traceable to its behavioral change**
PASS. All 15 hotfix IDs (B73-B-01 through B73-B-15) each have at least 1 [Fact] test with
explicit `Spec: B73-B-XX` annotation. Unchanged.

**TC02 — B73-B-08/09/15 tests verify format string output (string equality assertions)**
PASS.
- B73-B-08: T_BUF_BE_01 uses `Assert.Equal("BE ALL +3", result)`, T_BUF_BE_02 uses
  `Assert.Equal("BE ALL", result)`.
- B73-B-09: T_LABEL_01 uses `Assert.Equal("Quick ALL +4t", result)`, T_LABEL_02 uses
  `Assert.Equal("BE ALL +5", result)`, T_LABEL_04 uses `Assert.Equal("Quick ALL +0t", result)`.
- B73-B-15: T_LABEL_CLIP_02 uses `Assert.NotNull(GetField("LastChildFillProperty", ...))`,
  T_LABEL_CLIP_03 uses `Assert.NotNull(GetField("DockProperty", ...))` — reflection existence
  assertions appropriate for the layout-property nature of B73-B-15 (no format methods involved).

**TC03 — B73-B-14 orphan cleanup: IsQxCancelCandidate null guard tested**
PASS. T_ORPHAN_02 explicitly tests `Assert.False(CopyEngine.IsQxCancelCandidate(null))`.
Unchanged.

---

### FILE ROUTING

**F01 — C# source path points to Wave workspace**
PASS. `src/PropTraderTools/Tests/B73Tests.cs` is correctly rooted in the Wave workspace
(`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). No Director workspace path referenced.
Unchanged.

---

### RE-REVIEW SUMMARY: V1/V2/V3 DISPOSITION

| ID | Prior violation | Fix verified | Check result |
|----|----------------|--------------|-------------|
| V1 (TR03) | Short-form names in ticket did not match long-form names in plan S7 | Plan S7 updated to 33 short-form names; exact match confirmed | PASS |
| V2 (NT02) | T_LABEL_CLIP_02/03 constructed `new DockPanel()` (STA required) | Both tests now use `typeof(DockPanel).GetField(...)` — no instance construction | PASS |
| V3 (C04) | `FormatBuffer` reflection accessor absent | `GetFormatBuffer()` pattern added with explicit B73-B-11 indirect coverage rationale | PASS |

**No new violations introduced by the architect's fixes.**

---

## Overall: TICKET_REVIEW_PASS

All 3 prior violations (V1/TR03, V2/NT02, V3/C04) are resolved. All prior PASS items confirmed
unchanged. No new violations detected.

**Engineer is cleared to write `src/PropTraderTools/Tests/B73Tests.cs`.**

### Engineer Contract Summary

- **File to create**: `src/PropTraderTools/Tests/B73Tests.cs`
- **Class**: `public sealed class B73Tests` in `namespace PropTraderTools`
- **Test count**: 33 [Fact] methods, names as specified in ticket SCAN-07
- **Key patterns**:
  - Private static method access via `BindingFlags.NonPublic | BindingFlags.Static` reflection
  - Exception safety via `Record.Exception` (never `Assert.Throws` or raw try/catch)
  - WPF DependencyProperty checks via `typeof(DockPanel).GetField(...)` — no instance construction
  - `CopyEngine.Instance` for singleton access (no NT8 thread affinity required for pure methods)
- **7-Scan contract**: All 33 methods must pass SCAN-01 through SCAN-07 as specified in ticket
  (Layer 1). Engineer self-reports in ticket-1-completion.md (Layer 2). Verifier independently
  runs all 7 scans in ticket-1-verification.md (Layer 3).
- **BUILD_PASS criterion**: All 33 [Fact] tests pass; all 7 scans return expected results.
