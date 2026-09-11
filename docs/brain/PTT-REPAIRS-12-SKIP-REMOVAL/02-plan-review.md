# Plan Review — PTT-REPAIRS-12-SKIP-REMOVAL

**Epic:** PTT-REPAIRS-12-SKIP-REMOVAL  
**Phase:** 2 (Plan Review)  
**Reviewer:** PTT Plan Reviewer  
**Plan reviewed:** `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/02-architecture-plan.md`  
**Prior backlog read:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-03/06-deferred-backlog.md`  
**Rules applied:** Jane Street DNA (role-hardcoded), per-role DNA block  

---

## VERDICT: REVIEW_PASS

**Violations found: 0**  
**Spec requirements unaddressed: 0**

---

## Review Checklist

### R1 — LANE-SPLIT GATE Compliance

**Result declared in plan:** YES — `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` (plan §0, line 11)

**Reasoning stated:** YES — "All 4 tickets modify the same file (`CopyEngineTests.cs`) in disjoint line ranges. A serial T1→T2→T3→T4 pipeline with a `dotnet test` gate after each ticket is the correct execution model. Parallel lanes would require same-file merge coordination and add no throughput benefit for mechanical attribute removal."

**Evaluation:** SINGLE-PIPELINE is the correct call. Same-file disjoint ranges with a hard `dotnet test` gate between each ticket cannot safely parallelize. No LANES-APPROVED path was invoked, so Q1–Q4 evaluation is not required.

**Status: PASS ✓**

---

### R2 — Static Method Binding Flags Analysis (Per-Class)

Plan §3 (§3.1–§3.5) and §4 provide a complete per-class analysis:

| Class | Binding Analysis | Static Methods? | Verdict |
|-------|-----------------|-----------------|---------|
| B79CancelRaceGuardTests (§3.1) | Class helper = Instance. `IsPositionFlatOrMissing` tests use inline `NonPublic\|Static` — already correct. | YES (handled inline) | SAFE ✓ |
| BwaveCycT1R1BeHelperTests (§3.2) | Class helper = Instance. All 23 obfuscation-skip target methods are instance. | NO | SAFE ✓ |
| BwaveCycTaR2HelperTests (§3.3) | `GetStaticMethod` already present (PTT-REPAIRS-11-BINDING-FLAGS-03). All 12 obfuscation-skip tests call instance helper. | Covered by existing helper | SAFE ✓ |
| BwaveCycTaR3HelperTests (§3.4) | `GetStaticMethod` already present (PTT-REPAIRS-11-BINDING-FLAGS-02). All 33 obfuscation-skip tests call instance helper. | Covered by existing helper | SAFE ✓ |
| BwaveCycTaR6HelperTests (§3.5) | Both `GetStaticMethod` and `GetInstanceMethod` already present. Per-test binding table provided for all 10. | STATIC (5) + INSTANCE (5) | SAFE ✓ |

§4 summary table confirms: zero structural additions required. Pure attribute removal across all 5 classes.

**Status: PASS ✓**

---

### R3 — Protected Tests Explicitly Listed and Mandated NOT TOUCHED

The 3 already-fixed tests (from spec) are explicitly called out:

| Test | Location in plan | Mandate |
|------|-----------------|---------|
| `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` | §3.3 (line 108), T3 DO NOT TOUCH (line 239) | **DO NOT TOUCH** |
| `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` | §3.4 (line 127), T3 DO NOT TOUCH (line 241) | **DO NOT TOUCH** |
| `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` | §3.4 (line 128), T3 DO NOT TOUCH (line 242) | **DO NOT TOUCH** |

Additionally, T3 calls out two more plain-`[Fact]` guards:
- `OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod` (L6795) — not part of the 3 protected tests but correctly excluded.

All 3 spec-required protected tests are named, line-numbered, and carry explicit DO NOT TOUCH mandates.

**Status: PASS ✓**

---

### R4 — NT8-Runtime-Skip Protection (335 count mandated unchanged)

| Location in plan | Coverage |
|-----------------|----------|
| §6 SCAN-05 | "NT8-runtime skips untouched: grep 'NT8-runtime' count unchanged (335)" |
| §11 Expected Final State | "NT8-runtime-skip count in CopyEngineTests.cs: **335** (unchanged)" |
| T2 DO NOT TOUCH | L6570, L6580 — NT8-runtime skips protected |
| T4 DO NOT TOUCH | `ExtractLegSuffix_*` and `IsPositionStateRelevant_*` — NT8-runtime skips protected |

The 335-count invariant is enforced at: plan definition (§11), scan gate (SCAN-05), and per-ticket exclusion lists. Three-layer protection.

**Status: PASS ✓**

---

### R5 — Test-Failure Rollback Strategy

Plan §7 provides a complete and surgical rollback protocol:

1. Re-add `Skip` string to the specific failing test immediately.
2. Document failure as `DW-12-XX` in `06-deferred-backlog.md`.
3. Proceed with remaining tests in the ticket (no whole-ticket abort).
4. Run `dotnet test` after each rollback to confirm 0 failed.

The strategy correctly preserves the hard `failed=0` invariant at each step and defers, rather than discards, unexpectedly failing tests. The `DW-12-XX` naming convention aligns with prior deferred backlog practice.

**Status: PASS ✓**

---

### R6 — Ticket Structure: Scope and Per-Class Skip Counts

**Count verification (architect's code-read figures vs. spec estimates):**

| Class | Spec estimate | Plan count | Exact lines provided | Match |
|-------|--------------|------------|----------------------|-------|
| B79CancelRaceGuardTests | 59 | 59 | 59 lines listed | ✓ |
| BwaveCycT1R1BeHelperTests | 23 | 23 | 23 lines listed | ✓ |
| BwaveCycTaR2HelperTests | 12 | 12 | 12 lines listed | ✓ |
| BwaveCycTaR3HelperTests | 33 | 33 | 33 lines listed | ✓ |
| BwaveCycTaR6HelperTests | 10 | 10 | 10 lines listed | ✓ |
| **TOTAL** | **137** | **137** | **137 lines listed** | ✓ |

**Ticket assignment:**

| Ticket | Content | Removals | Verify gate |
|--------|---------|----------|-------------|
| T1 | B79CancelRaceGuardTests | 59 | `--filter "FullyQualifiedName~B79CancelRaceGuardTests"` |
| T2 | BwaveCycT1R1BeHelperTests | 23 | `--filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"` |
| T3 | R2 + R3 combined | 45 (12+33) | `--filter "FullyQualifiedName~BwaveCycTaR2HelperTests\|BwaveCycTaR3HelperTests"` |
| T4 | BwaveCycTaR6HelperTests | 10 | `--filter "FullyQualifiedName~BwaveCycTaR6HelperTests"` |

T3 is the largest ticket (45 removals) but the operation is purely mechanical (attribute removal, no logic), all exact lines are specified, and DO NOT TOUCH guards are named. Scope is manageable.

**Status: PASS ✓**

---

### R7 — 7-Scan Checklist Template

Plan §6 defines the 7-scan checklist and mandates: *"Each ticket MUST complete this checklist before marking complete."*

| Scan | Check | DNA Rule Alignment |
|------|-------|-------------------|
| SCAN-01 | ASCII-only: No Unicode, emoji, or curly quotes added | ASCII mandate |
| SCAN-02 | No `lock()` added in CopyEngineTests.cs | JS-021 |
| SCAN-03 | No `throw` statement added | JS-001 |
| SCAN-04 | No `DateTime.Now` reference added | NT8 SCAN-06 |
| SCAN-05 | NT8-runtime skips untouched: grep count unchanged (335) | Spec requirement |
| SCAN-06 | Protected tests untouched: GetSenderAccountName / LogBeSlotEviction `[Fact]` count unchanged | Spec requirement |
| SCAN-07 | `dotnet test` gate: 0 failed; obfuscation-skip count decreased by expected delta | Hard requirement |

Template is present in the plan and explicitly scoped to every ticket. Phase 3.5 (ticket reviewer) will verify that individual ticket documents carry the completed checklist.

**Status: PASS ✓**

---

### R8 — Jane Street DNA Rule Constraints

#### Concurrency (P0 — auto-FAIL triggers)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | `lock()` | Not applicable — no production code changes; test file only. SCAN-02 mandates zero `lock()` additions. PASS |
| JS-021 | Monitor/Mutex/SemaphoreSlim for state | Not applicable. PASS |
| JS-023 | UI update from off-thread without Dispatcher.InvokeAsync | Not applicable. PASS |

#### Type Safety (P0 — auto-FAIL triggers)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | `throw` in OnOrderUpdate/SendCopy/gate chain | Not applicable — no production code changes. SCAN-03 mandates zero `throw` additions. PASS |
| JS-002 | null return where value expected | Not applicable. PASS |
| JS-003 | Magic string for discriminated state | Not applicable. PASS |

#### Immutability (P1 — auto-FAIL triggers)

| Rule | Check | Result |
|------|-------|--------|
| JS-009 | Dictionary<K,V> for shared/thread-touched collection | Not applicable. PASS |
| JS-008 | Mutable fields on struct | Not applicable. PASS |
| JS-008 | SolidColorBrush not Freeze()d | Not applicable. PASS |

#### Construction (P1 — auto-FAIL triggers)

| Rule | Check | Result |
|------|-------|--------|
| JS-010 | Public constructor on singleton or signal struct | Not applicable. PASS |

#### NT8 Hard Constraints

| Constraint | Check | Result |
|------------|-------|--------|
| async/await in OnInitialize/OnDestroyed/OnWindowCreated | Not applicable. PASS |
| Account.All in constructor | Not applicable. PASS |
| sealed TradeCopierWindow | Not applicable. PASS |
| FontFamily override (SCAN-03) | Not applicable. PASS |
| Hardcoded #RRGGBB hex (SCAN-04) | Not applicable. PASS |
| CreateOrder without PTT- prefix (SCAN-05) | Not applicable. PASS |
| DateTime.Now (SCAN-06) | SCAN-04 in plan mandates none added. PASS |

#### Complexity

| Constraint | Check | Result |
|------------|-------|--------|
| CYC > 8 | Plan §8 states "No change" for CYC. Operation is `[Fact(Skip=...)]` → `[Fact]` attribute substitution — zero branching logic added. No method body changes. PASS |

**Status: ALL RULES PASS ✓**

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Remove 137 `[Fact(Skip="obfuscation:...")]` from CopyEngineTests.cs | YES | §1, §5 (T1–T4), exact line lists |
| Touch ONLY CopyEngineTests.cs — no production .cs file | YES | §2 explicit |
| Do NOT touch 335 NT8-runtime-skip annotations | YES | §6 SCAN-05, §11, per-ticket DO NOT TOUCH |
| Do NOT touch 3 already-fixed tests | YES | §3.3, §3.4, T3 DO NOT TOUCH lists |
| After every ticket: `dotnet test` = 0 failed (HARD) | YES | §6 SCAN-07, each ticket verify gate |
| Static method tests use correct BindingFlags | YES | §3.1–§3.5, §4 — all confirmed correct as-is |
| ASCII-only, no `lock()`, no new `throw`, no CYC change | YES | §6 SCAN-01/02/03, §8 |
| Baseline 26p/0f/488s/514t → Target 163p/0f/351s/514t | YES | §1 table |
| DW-09-04 closes this epic | YES | §10 |
| Per-ticket 7-scan checklist | YES | §6 (template + mandate) |
| Rollback strategy for unexpected failures | YES | §7 |
| Per-class binding flags analysis | YES | §3.1–§3.5, §4 |

All 12 spec requirements addressed.

---

## Observations (Non-Blocking)

1. **T3 ticket combines two classes (R2 + R3) into 45 removals.** This is the largest single-ticket scope. All exact lines are specified, DO NOT TOUCH guards are named. Operation is purely mechanical (no logic change). Scope is acceptable for a single engineer pass with the given line-level precision.

2. **7-scan template is centralized in §6** rather than inlined per ticket description in §5. This is architecturally clean (DRY). Phase 3.5 ticket reviewer must verify each ticket document carries the completed (checked-off) checklist at submission time.

3. **`GetSenderAccountName_ShouldReturnEmpty_*` tests (L6570/L6580 in T2)** are correctly identified as NT8-runtime skips, not obfuscation skips. Distinction is correctly preserved.

---

## Prior Backlog Alignment

| ID | Prior Status | This Epic Action | Correct? |
|----|-------------|-----------------|---------|
| DW-09-04 | OPEN (PTT-REPAIRS-11-BINDING-FLAGS-03 backlog) | CLOSED by this epic (plan §10) | YES ✓ |

---

## Final Verdict

**REVIEW_PASS**

Zero violations. Zero unaddressed spec requirements. Plan is complete, precise, and internally consistent. All Jane Street DNA rules are satisfied by design (test-file-only operation). The LANE-SPLIT gate, binding flags analysis, protected test mandates, NT8-runtime-skip protection, rollback strategy, ticket structure, and 7-scan template are all present and correct.

**Gate: UNLOCKED — Phase 3 (ticket generation) may proceed.**

---

*Review completed by PTT Plan Reviewer — PTT-REPAIRS-12-SKIP-REMOVAL Phase 2*
