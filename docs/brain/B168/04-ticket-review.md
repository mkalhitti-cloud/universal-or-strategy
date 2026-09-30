# Ticket Review: B168
## COLLATERAL-STORM -- Concurrent multi-stop drag kills follower stops
## Block: B168 | Design ref: DW-B168-01 | Reviewer: ptt-ticket-reviewer

---

## Ticket B168-1 -- CopyEngine.cs src change

### Check 1 -- Exactly 3 change sites (no 4th site)

**PASS**

Ticket Overview states: "Exactly 3 edit sites. No other lines changed." Sites A, B, and C are
enumerated. The CYC comment update is explicitly declared to be part of Site C ("it is a
comment-only update within the same method header, not an additional edit site") -- correctly
avoiding a phantom 4th site. No 4th site mentioned or implied anywhere in the ticket.

---

### Check 2 -- No whitespace mutations outside changed lines (constraint explicitly stated)

**PASS**

Ticket Overview states: "No whitespace mutations outside the 3 sites."
Acceptance Criteria bullets: "No whitespace-only mutations outside the 3 edit sites."
Both explicit statements present.

---

### Check 3 -- Test seam pattern matches IsPttStpDragCancellableTestable

**PASS**

Existing seam at CopyEngine.cs:4141:
    internal static bool IsPttStpDragCancellableTestable(Order o) => IsPttStpDragCancellable(o);

Ticket Site B proposes:
    internal static bool IsLeaderLegBeingChangedTestable(Order? o) => IsLeaderLegBeingChanged(o);

Pattern match: `internal static`, expression-body, single forwarding call -- structurally
identical. The nullable `Order? o` parameter differs from the non-nullable pattern in the
existing seam; however, this divergence is required by the spec (DW-B168-01 SS6 NOTE:
"nullable param mirrors production signature -- required for Null test case") and is
repeated in the plan (SS5). The seam comment in the ticket explicitly states:
"DW-B168-01: nullable param mirrors production signature -- required for Null test case."
This is a deliberate and spec-mandated deviation, not a pattern violation. Structurally
the seam follows the established model in every material respect.

---

### Check 4 -- CYC comment update (ResubmitCollateralLegs CYC=2->CYC=3) listed

**PASS**

Dedicated section "CYC comment update -- ResubmitCollateralLegs header" is present.
Before/After text is explicit:
  Before: `// CYC=2`
  After:  `// DW-B142-QTY-DESYNC-01 + DW-B168-01 guard.`
          `// CYC was 2 (foreach+if), now 3 (+1 for IsLeaderLegBeingChanged guard).`
          `// CYC=3. Well within <=8.`
SCAN-06 also verifies this at execution time.

---

### Check 5 -- ptt-sync.ps1 post-step listed

**PASS**

Section "Post-change step (MANDATORY)":
    powershell -File .\scripts\ptt-sync.ps1
With requirement: "Exit code must be 0." SCAN-07 also verifies exit code.

---

### Check 6 -- 7-scan checklist present (all 7 scans)

**PASS**

All 7 scans present:
  SCAN-01: lock() grep -- 0 new matches
  SCAN-02: CYC audit -- IsLeaderLegBeingChanged=2, ResubmitCollateralLegs=3
  SCAN-03: Unicode/non-ASCII grep in changed lines
  SCAN-04: IsLeaderLegBeingChanged appears exactly 3 times
  SCAN-05: No whitespace-only line changes outside 3 edit sites
  SCAN-06: CYC comment updated to CYC=3 with DW-B168-01 annotation
  SCAN-07: ptt-sync.ps1 exit 0

---

### Check 7 -- No lock() (zero new lock() matches required)

**PASS**

Acceptance Criteria: "No new `lock()` anywhere in `CopyEngine.cs`"
JS rules table: "JS-021 | No `lock()` | Zero synchronization primitives introduced"
SCAN-01: `grep "lock(" src/PropTraderTools/CopyEngine.cs` -> 0 new matches
Three independent statements in the ticket enforce this requirement.

---

### Check 8 -- Verbatim C# at all 3 sites (full code, not descriptions)

**PASS**

Site A: 15-line verbatim csharp block present (full method with comment header).
Site B: 5-line verbatim csharp block present (full seam with comments).
Site C: 3-line verbatim csharp block present, plus a "Result" context block showing
        the full changed area in ResubmitCollateralLegs for surgeon orientation.
No site is described in prose only; all sites contain exact insertable C#.

---

### Traceability

**PASS**

All 3 change sites and the test seam trace to DW-B168-01 (spec req ID stated in ticket
header and in per-site comments). No phantom work items. Architecture plan SS3-SS6
maps 1:1 to Sites A/B/C and the seam definition.

### JS Pre-Check

**PASS**

  JS-001 (no throw in hot path): IsLeaderLegBeingChanged is expression-body with no throw.
  JS-002 (return bool not void): Returns bool.
  JS-021 (no lock): Zero synchronization primitives. Verified in JS rules table and SCAN-01.
  JS-036 (no heap in hot path): Reads existing field; no ToList, no new objects.
  ASCII-only: All comments and code are ASCII-only per acceptance criteria and SCAN-03.

### CYC Pre-Check

**PASS**

  IsLeaderLegBeingChanged (new): CYC=2. Well within <=8. No split required.
  IsLeaderLegBeingChangedTestable (new): CYC=1 (pure delegation). Well within <=8.
  ResubmitCollateralLegs (modified): CYC=3. Well within <=8. No split required.

### NT8 Check

**PASS**

  No async/await in lifecycle methods.
  No Account.All outside Loaded handler.
  No sealed on NinjaTrader window classes.
  No FontFamily, hardcoded hex color, DateTime.Now, or PTT-name violations introduced.
  The predicate reads leaderLeg.OrderState -- a valid NT8 Order property access.

### File Routing

**PASS**

File: `src/PropTraderTools/CopyEngine.cs` -- correct Wave workspace path.

---

**VERDICT B168-1: TICKET_REVIEW_PASS**

---

---

## Ticket B168-2 -- CopyEngineTests.cs test addition

### Check 9 -- All 4 [Fact] tests present with exact names from spec

**PASS**

Spec (DW-B168-01 SS6) names: ChangeSubmitted_ReturnsTrue, ChangePending_ReturnsTrue,
Working_ReturnsFalse, Null_ReturnsFalse (all prefixed IsLeaderLegBeingChanged_).
Ticket verbatim code block and the method-names table both carry all 4 exact names.
No name typos detected.

---

### Check 10 -- All 4 tests use IsLeaderLegBeingChangedTestable (not direct private access)

**PASS**

All 4 test bodies in the verbatim code block call:
    CopyEngine.IsLeaderLegBeingChangedTestable(order)  (or null)
Acceptance criteria: "All 4 use `IsLeaderLegBeingChangedTestable` seam (not direct private access)"
SCAN-02 verifies this at execution time.

---

### Check 11 -- Tests compile without NT8 runtime

**PASS**

No `NinjaTrader.Client`, `OnExecutionUpdate`, `Account.All`, or runtime-startup types
appear in test bodies. The only NT8 references are `OrderState.X` enum values and the
`StubOrder` helper -- both established patterns already in use in CopyEngineTests.cs
(following the existing IsPttStpDragCancellable test pattern) that compile without NT8
runtime. SCAN-03 verifies no runtime references at execution time.

---

### Check 12 -- StubOrder helper used for all order construction

**PASS**

Three tests (ChangeSubmitted, ChangePending, Working) each use:
    var order = StubOrder("test", OrderState.X);
Null test passes `null` directly with no order construction -- no helper needed.
Acceptance criteria explicitly acknowledges this: "StubOrder helper used for all order
constructions (3 tests; null test uses no helper)." SCAN-04 verifies at execution time.

---

### Check 13 -- 7-scan checklist present (all 7 scans)

**PASS**

All 7 scans present:
  SCAN-01: Exactly 4 [Fact] methods in DwB168Tests
  SCAN-02: All 4 bodies call CopyEngine.IsLeaderLegBeingChangedTestable
  SCAN-03: No NT8 runtime references in test bodies
  SCAN-04: StubOrder used for all order constructions
  SCAN-05: ASCII-only in all new test code
  SCAN-06: No lock() in test code
  SCAN-07: Test names match spec exactly

---

### Check 14 -- Dependency on B168-1 stated

**PASS**

Ticket header: "**Depends on:** B168-1 (requires `IsLeaderLegBeingChangedTestable` seam to exist)"
This is explicit and located in the very first metadata block of the ticket.

---

### Traceability

**PASS**

Spec req ID: DW-B168-01 SS6 (stated in ticket header). All 4 [Fact] names and the seam
reference trace directly to the test contract in the spec. No phantom work items.

### JS Pre-Check

**PASS**

  JS-021 (no lock): No synchronization in test code. SCAN-06 verifies.
  ASCII-only: Stated in acceptance criteria and SCAN-05.

### NT8 Check

**PASS**

No runtime dependencies introduced. Test file uses established StubOrder + seam pattern.

### File Routing

**PASS**

File: `src/PropTraderTools/CopyEngineTests.cs` -- correct Wave workspace path.

---

**VERDICT B168-2: TICKET_REVIEW_PASS**

---

---

## Overall

All 14 gate checks: PASS
All per-ticket sub-checks (Traceability, JS Pre-Check, CYC Pre-Check, NT8 Check,
Test Coverage, Scan Checklist, File Routing): PASS

## VERDICT: TICKET_REVIEW_PASS
