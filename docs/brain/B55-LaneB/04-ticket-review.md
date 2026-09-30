# PTT-COPIER B55 LaneB -- Ticket Review (Second Pass)
# Phase: 3.5 (ptt-ticket-reviewer)
# Reviewed by: ptt-ticket-reviewer
# Date: 2026-08-10 (second pass after architect revision)
# Tickets file: docs/brain/B55-LaneB/04-tickets.md
# Plan file: docs/brain/B55-LaneB/02-architecture-plan.md
# Plan review: docs/brain/B55-LaneB/02-plan-review.md
# Spec: specs/002-trade-copier-spec.html id="section-b55" (LaneB, lines 23089-23162)
# Standards: docs/standards/jane-street/RULES_CATALOG.md
#            docs/standards/NT8_COMPILER_RULES.md
# Defect: DW-B47-05 P2 -- FindRule null contract undocumented (JS-002)
# Prior review: TICKET_REVIEW_FAIL (TR-B55B-01 -- T2 NOTE incorrectly called CopyRule a reference type)
# This review: SECOND PASS -- confirms TR-B55B-01 resolved, no new violations

---

## Ticket Review: B55-LaneB

---

### T1 -- Add XML Doc Comment to FindRule

**Ticket ID:** T1
**File:** `src/PropTraderTools/CopyEngine.cs` (Wave workspace)

---

#### 1. Traceability

PASS

T1 header cites `DW-B47-05 P2 -- JS-002 null contract, XML doc comment` and
`Spec requirements: DW-B47-05 P2`. Spec line 23102 mandates the XML doc comment
above FindRule; spec line 23169 confirms LaneB scope. Direct one-to-one traceability.
No phantom work. No missing plan item.

---

#### 2. JS Pre-Check

PASS

| Rule | Ticket Assessment | Verdict |
|------|-------------------|---------|
| JS-021 (no lock()) | No lock added or removed | PASS |
| JS-002 (null contract) | XML doc comment documents the null return contract | PASS |
| JS-033 (no async void) | No async usage | PASS |
| JS-001 (no throw in hot path) | No new throw | PASS |

No P0 rule violated. Pre-existing `return null` in FindRule body is not new.

---

#### 3. CYC Pre-Check

PASS

T1 explicitly states: "CYC of FindRule remains **3** (unchanged)."
Method signature section confirms: "CYC: 3 (unchanged)."
No new method. No changed method. CYC delta = 0. Well within CYC <= 8 threshold.

---

#### 4. NT8 Check

PASS

T1 contains an explicit NT8 rules table covering NT8-001, NT8-002, NT8-018/021,
NT8-019, NT8-028, plus an explicit note that XML doc syntax (`///`, `<summary>`,
`<returns>`, `<see cref="..."/>`, `<c>`) are fully supported in .NET Framework 4.8.
Zero NT8 rule violations described.

---

#### 5. Test Coverage

PASS

T1 adds no new methods -- doc comment insert only. The spec assigns T_B55B_01 to
T2 (a separate ticket). SCAN-07 in T1's checklist correctly notes: "T_B55B_01 does
NOT exist yet in Ticket-1 scope -- Ticket-2 adds it." Correct scoping.

---

#### 6. Scan Checklist

PASS

T1 contains the full 7-scan checklist (SCAN-01 through SCAN-07) with specific,
executable commands and stated expected results. No generic placeholders.

| Scan | Present | Command specific |
|------|---------|-----------------|
| SCAN-01 | YES | `Select-String "lock(" src/ -Recurse -Include *.cs` |
| SCAN-02 | YES | `Select-String "async void " src/ -Recurse -Include *.cs` |
| SCAN-03 | YES | `Select-String "return null" src/ -Recurse -Include *.cs` |
| SCAN-04 | YES | `Select-String "throw new " src/ -Recurse -Include *.cs` |
| SCAN-05 | YES | `python scripts/complexity_audit.py` |
| SCAN-06 | YES | `dotnet build` |
| SCAN-07 | YES | `dotnet test` (expected: 255 pass + 24 pre-existing fail = 279) |

All 7 scans present. SCAN-08 is correctly not on T1 (belongs to T2 per spec).

---

#### 7. File Routing

PASS

`C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
Relative path: `src/PropTraderTools/CopyEngine.cs`
Correctly points to the Wave workspace. No Director workspace path for .cs files.

---

#### 8. Spec Coverage

PASS

T1 covers DW-B47-05 P2 step 1 (XML doc comment). Spec section-b55 LaneB step 1
is fully addressed by this ticket. No uncovered spec requirement for T1.

---

#### 9. Aggregate Scope Guard

PASS

T1 states: "Do NOT alter the method signature, method body, or any surrounding line."
Summary confirms: "Zero logic changes. Zero call-site rewrites. Doc + test only."

---

#### T1 VERDICT: TICKET_REVIEW_PASS

---

---

### T2 -- T_B55B_01 Test -- CopyEngineTests.cs

**Ticket ID:** T2
**File:** `src/PropTraderTools/CopyEngineTests.cs` (Wave workspace)

---

#### 1. Traceability

PASS

T2 header cites `DW-B47-05 P2 -- T_B55B_01 documents and locks the null-return
contract` and `Spec requirements: DW-B47-05 P2 step 3`. Spec lines 23092-23097
mandate T_B55B_01. Direct traceability. No phantom work. No missing plan item.

---

#### 2. JS Pre-Check

PASS

| Rule | Ticket Assessment | Verdict |
|------|-------------------|---------|
| JS-021 (no lock()) | No lock in new test | PASS |
| JS-002 (null contract) | Test asserts null-return contract via `Assert.False(... .HasValue)` | PASS |
| JS-033 (no async void) | Synchronous xUnit [Fact] returning void; NOT async void | PASS |
| JS-001 (no throw) | No throw introduced | PASS |

No P0 rule violated. The `Assert.False(((CopyRule?)result).HasValue)` assertion
pattern is structurally correct for a void-returning synchronous [Fact].

---

#### 3. CYC Pre-Check

PASS

T2 xUnit [Fact] details table states: "CYC | 1 (straight-line, no branches)."
Verified: the test body contains zero conditional branches (no if/else/switch/while/for).
CYC = 1. Well within CYC <= 8 threshold.

---

#### 4. NT8 Check

PASS

T2 correctly notes: "Test file (`CopyEngineTests.cs`) is compiled by the Linting
.csproj (MSBuild / dotnet test), **not** by NT8's internal NinjaScript Roslyn
compiler. NT8 compiler rules do not apply to test files."

T2 includes a full NT8 table (NT8-001, NT8-002, NT8-003, NT8-004, NT8-006,
NT8-018/021, NT8-019) for completeness -- all PASS.

Specific check on `Assert.Empty(bag)` where `bag` is `ConcurrentBag<CopyRule>`:
xUnit's `Assert.Empty` uses `IEnumerable` enumeration, not `System.Linq.Any()`.
NT8-006 (System.Linq Any() banned) does not apply to test files. PASS.

---

#### 5. Test Coverage

PASS

**TR-B55B-01 RESOLUTION CONFIRMED.**

The prior FAIL was: T2 NOTE-01 stated "for reference-type CopyRule, typeof(CopyRule?)
and typeof(CopyRule) are identical at the CLR level" -- factually wrong because CopyRule
is a `private readonly struct` (value type, not a reference type).

**The revised NOTE-01 now correctly states:**

> "CopyRule is a `private readonly struct` (value type, not a reference type). For a
> value-type struct, `typeof(CopyRule?)` compiles to `typeof(Nullable<CopyRule>)`, which
> IS a distinct CLR type from `typeof(CopyRule)`. The assertion
> `Assert.Equal(typeof(Nullable<CopyRule>), mi.ReturnType)` would therefore be
> meaningful and non-vacuous -- it correctly verifies the method returns a nullable struct.
> It was removed as a simplification: the null-return contract is sufficiently locked by
> the `Assert.False(((CopyRule?)result).HasValue)` assertion below."

**All factual claims in the revised NOTE-01 are correct:**

| Claim | Correct? | Basis |
|-------|----------|-------|
| CopyRule is `private readonly struct` (value type) | YES | CopyEngine.cs L154 |
| `typeof(CopyRule?)` == `typeof(Nullable<CopyRule>)` for a struct | YES | CLR spec |
| `typeof(Nullable<CopyRule>)` != `typeof(CopyRule)` at CLR level | YES | CLR spec |
| The removed assertion would be non-vacuous for a struct | YES | Follows from above |
| null `Nullable<CopyRule>` boxes to null reference | YES | CLR Nullable boxing rule |
| `(CopyRule?)null` unboxes to HasValue = false | YES | CLR Nullable unboxing rule |
| `Assert.False(((CopyRule?)result).HasValue)` correctly asserts null return | YES | Correct |
| `Assert.Null(result)` would fail for boxed nullable struct with null inner value | YES | CLR boxing semantics |

**Engineer clarity:** The NOTE now correctly explains WHY the `HasValue` pattern is
needed (CLR Nullable boxing), correctly identifies CopyRule as a struct, and correctly
warns against the wrong `Assert.Null` form. The engineer will not be misled.

**Test body correctness verified:**
- `_rules` field reflection + ConcurrentBag cast + `Assert.Empty` -- correct pattern
  for verifying engine has no rules before invoking FindRule
- `FindRule` method reflection -- same `NonPublic | Instance` pattern as B53 LaneA
- `mi.Invoke(_engine, new object[] { (NinjaTrader.Cbi.Instrument)null })` -- hits
  the null guard in FindRule (first return null path); correct for testing null contract
- `Assert.False(((CopyRule?)result).HasValue)` -- correct assertion as per NOTE-01
  analysis above

**[Fact] method name:** `T_B55B_01_FindRule_ReturnsNull_WhenNoRules` -- present and
matches spec requirement. Framework: xUnit (NOT NUnit, NOT MSTest). PASS.

---

#### 6. Scan Checklist

PASS

T2 contains the full 7-scan checklist (SCAN-01 through SCAN-07) PLUS SCAN-08.

| Scan | Present | Command specific |
|------|---------|-----------------|
| SCAN-01 | YES | `Select-String "lock(" src/ -Recurse -Include *.cs` |
| SCAN-02 | YES | `Select-String "async void " src/ -Recurse -Include *.cs` |
| SCAN-03 | YES | `Select-String "return null" src/ -Recurse -Include *.cs` |
| SCAN-04 | YES | `Select-String "throw new " src/ -Recurse -Include *.cs` |
| SCAN-05 | YES | `python scripts/complexity_audit.py` |
| SCAN-06 | YES | `dotnet build` |
| SCAN-07 | YES | `dotnet test` (T_B55B_01 PASS; 280 total: 256 pass + 24 fail) |
| SCAN-08 | YES | FindRule call-site audit with PowerShell command + per-site guard table |

All 8 scans present. All commands specific and executable. Expected results stated
for each scan. Non-negotiable 3-layer contract (ticket, engineer attestation, verifier
cross-check) is fully intact.

---

#### 7. File Routing

PASS

`C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
Relative path: `src/PropTraderTools/CopyEngineTests.cs`
Correctly points to Wave workspace. No Director workspace path for .cs files.

---

#### 8. Spec Coverage

PASS

T2 covers DW-B47-05 P2 step 3 (new [Fact] test). Spec lines 23092-23097 mandate
T_B55B_01_FindRule_ReturnsNull_WhenNoRules. Fully addressed.

---

#### 9. Aggregate Scope Guard

PASS

T2 states: "Do NOT modify any existing test method, field, using directive, or closing brace."
Summary confirms: "Zero logic changes. Zero call-site rewrites. Doc + test only."

---

#### T2 VERDICT: TICKET_REVIEW_PASS

---

---

## Aggregate Checks

### Spec Coverage (aggregate)

PASS

| Spec Requirement | Covered By | Status |
|-----------------|-----------|--------|
| DW-B47-05 P2 step 1 -- XML doc comment on FindRule | T1 | COVERED |
| DW-B47-05 P2 step 3 -- T_B55B_01 [Fact] test | T2 | COVERED |

No uncovered requirement. No duplicate coverage.

### Call-Site Audit (SCAN-08)

PASS

T2 SCAN-08 cites all production FindRule call sites with exact file:line references
and guard status. Both call sites (L1185, L1355) are GUARDED. Architecture plan
section 5 confirms this independently. SCAN-08 = ALL GUARDED.

### Scope Guard

PASS

Both tickets scope is doc + test only. No logic changes. No call-site rewrites.
No out-of-scope work described.

---

## Violations Summary

| ID | Ticket | Severity | Description | Status |
|----|--------|----------|-------------|--------|
| TR-B55B-01 | T2 | ~~FAIL~~ | ~~T2 NOTE stated "reference-type CopyRule" -- factually wrong, CopyRule is a struct~~ | **RESOLVED** in revised tickets |

No violations remain. All checks PASS for both tickets.

---

## Overall: TICKET_REVIEW_PASS

All 9 checks PASS for both T1 and T2.

The sole prior violation TR-B55B-01 is fully resolved:
- NOTE-01 now correctly identifies CopyRule as `private readonly struct` (value type)
- NOTE-01 correctly explains CLR Nullable<T> boxing behavior
- NOTE-01 correctly states the removed `typeof(Nullable<CopyRule>)` assertion would
  have been non-vacuous (meaningful) for a struct
- NOTE-01 correctly warns against `Assert.Null(result)` with accurate CLR rationale
- The test body `Assert.False(((CopyRule?)result).HasValue)` is correct

Engineer may proceed to implementation. Verifier (Phase 4b) has full per-ticket
SCAN-01 through SCAN-08 checklists as the layer-1 contract anchor.

---

*ptt-ticket-reviewer | B55-LaneB | Phase 3.5 | Second Pass | 2026-08-10*
