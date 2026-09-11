# Ticket Review — PTT-REPAIRS-12-SKIP-REMOVAL

**Epic:** PTT-REPAIRS-12-SKIP-REMOVAL  
**Phase:** 3.5 (Ticket Review)  
**Reviewer:** PTT Ticket Reviewer  
**Tickets reviewed:** `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/04-tickets.md`  
**Plan reviewed:** `docs/brain/PTT-REPAIRS-12-SKIP-REMOVAL/02-architecture-plan.md` (REVIEW_PASS)  
**Rules applied:** Jane Street DNA (role-hardcoded), per-role DNA block, 7-scan contract mandate  

---

## Inputs Verified

| Document | Status |
|----------|--------|
| `04-tickets.md` | READ — 4 tickets, 415 lines |
| `02-architecture-plan.md` | READ — PLAN_COMPLETE, 339 lines |
| `02-plan-review.md` | READ — REVIEW_PASS, 0 violations |
| `docs/protocol/RULES_CATALOG.md` | NOT FOUND in this workspace (lives in Director workspace). JS rules applied from plan-review attestation and role-hardcoded DNA. |

---

## Scan Label Divergence — Pre-Review Finding

The architecture plan §6 defines the 7-scan template as:

| Plan Scan | Content |
|-----------|---------|
| SCAN-01 | ASCII-only: No Unicode, emoji, or curly quotes added |
| SCAN-02 | No lock(): no lock() added |
| SCAN-03 | No new throw: no throw statement added |
| SCAN-04 | No DateTime.Now reference added |
| SCAN-05 | NT8-runtime skips untouched: grep count unchanged (335) |
| SCAN-06 | Protected tests untouched: GetSenderAccountName / LogBeSlotEviction [Fact] count unchanged |
| SCAN-07 | dotnet test gate: 0 failed; obfuscation-skip count decreased by expected delta |

The tickets implement a **redesigned scan set** as follows (consistent across all 4 tickets):

| Ticket Scan | Content |
|-------------|---------|
| SCAN-01 | `grep "lock("` — 0 matches |
| SCAN-02 | `grep "throw "` — count must NOT increase |
| SCAN-03 | CYC check — N/A (attribute substitution only) |
| SCAN-04 | `grep "obfuscation:"` — must decrease by exactly N |
| SCAN-05 | `grep "NT8-runtime:"` — must remain exactly 335 |
| SCAN-06 | `dotnet build` — 0 errors, 0 warnings |
| SCAN-07 | `dotnet test --filter ...` — 0 failed |

**Assessment:** The plan's SCAN-01 (ASCII-only), SCAN-04 (DateTime.Now), and SCAN-06 (protected tests check) are not present as scan-table commands. Their underlying requirements are covered by:
- ASCII-only and DateTime.Now → stated in Global Invariants section of `04-tickets.md`
- Protected tests → stated in per-ticket "Do NOT Touch" sections with explicit line numbers

The tickets have 7 numbered scans (SCAN-01 through SCAN-07) each with a command and expected result, satisfying the format requirement. The content substitution (build check + obfuscation count check) adds value. The absence of the plan's SCAN-06 (protected-tests grep) from scan tables means the engineer lacks a scan-level attestation command for that check.  
**Classification: WARN (non-blocking)** — the `dotnet test` gate in SCAN-07 would catch any accidental breakage of already-passing protected tests, providing equivalent safety. Noted for architect awareness.

---

## T1 — B79CancelRaceGuardTests (59 obfuscation-skip removals)

### 1. Traceability
- Spec requirement cited: `DW-09-04 (partial — 59 of 137)` ✓
- Maps to plan §5 T1 ✓
- No phantom work (only attribute removal in specified line range) ✓
- **PASS**

### 2. Scope Isolation
- Class: `B79CancelRaceGuardTests` only ✓
- File: `src/PropTraderTools/CopyEngineTests.cs` only ✓
- No production `.cs` file touched ✓
- **PASS**

### 3. Exact Operation
- SEARCH: `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]` ✓
- REPLACE: `[Fact]` ✓
- 59 exact line numbers listed; count verified (matches plan §5 T1 line list exactly) ✓
- **PASS**

### 4. Binding Flags
- Class helper (L5845–L5847): `BindingFlags.NonPublic | BindingFlags.Instance` ✓
- `IsPositionFlatOrMissing` tests (L6357, L6367): inline `BindingFlags.NonPublic | BindingFlags.Static` ✓
- Note on "57 instance + 2 static inline = 59 total" correctly stated ✓
- No structural additions required — confirmed ✓
- **PASS**

### 5. Protected Tests & NT8 Invariant
- NT8-runtime-skip 335-count invariant stated in Do NOT Touch section ✓
- T1 does not contain the 4 protected tests (they are in T3 scope) — correct, not required here ✓
- **PASS**

### 6. Post-Implementation Gate
- `dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests"` — 0 failed mandated ✓
- Required passed-count increase stated (59, or 59 minus rollbacks) ✓
- **PASS**

### 7. Rollback Protocol
- Re-add Skip string to failing test ✓
- Document as `DW-12-01` in `06-deferred-backlog.md` ✓
- Continue remaining tests (no whole-ticket abort) ✓
- Re-run gate after each rollback — 0 failed before T2 ✓
- **PASS**

### 8. 7-Scan Checklist Presence
- SCAN-01 through SCAN-07: all present with command and expected result ✓
- SCAN-05 specifically checks NT8-runtime: = 335 ✓
- SCAN-07 scoped to `B79CancelRaceGuardTests` filter ✓
- **PASS** (WARN: SCAN-01 expected result says "0 matches" for lock() — appropriate since CopyEngineTests.cs is a pure test file with no existing lock() usage; SCAN-06 is dotnet build rather than plan's protected-tests grep — noted in pre-review finding above)

### 9. NT8 Invariant
- SCAN-05: `grep "NT8-runtime:" src/PropTraderTools/CopyEngineTests.cs` → must remain exactly 335 ✓
- **PASS**

### 10. Sequencing
- T1 has no prerequisite (first in chain) — correct ✓
- Global invariants state: "T1 must reach 0 failed before T2 starts" ✓
- **PASS**

### 11. JS/DNA Rules
- No lock() added ✓ (SCAN-01)
- No throw added ✓ (SCAN-02)
- ASCII-only stated in global invariants ✓
- No CYC change — operation is attribute substitution only ✓ (SCAN-03)
- No production .cs file touched ✓
- No DateTime.Now ✓ (global invariants)
- No FontFamily, no sealed, no async/await in lifecycle — N/A ✓
- **PASS**

### T1 VERDICT: TICKET_REVIEW_PASS

---

## T2 — BwaveCycT1R1BeHelperTests (23 obfuscation-skip removals)

### 1. Traceability
- Spec requirement cited: `DW-09-04 (partial — 23 of 137)` ✓
- Maps to plan §5 T2 ✓
- No phantom work ✓
- **PASS**

### 2. Scope Isolation
- Class: `BwaveCycT1R1BeHelperTests` only ✓
- File: `src/PropTraderTools/CopyEngineTests.cs` only ✓
- **PASS**

### 3. Exact Operation
- SEARCH/REPLACE strings globally defined and referenced ✓
- 23 exact line numbers listed; count verified (matches plan §5 T2 line list exactly) ✓
- **PASS**

### 4. Binding Flags
- Class helper (L6477–L6478): `BindingFlags.NonPublic | BindingFlags.Instance` ✓
- All 23 tests target instance methods — list of 14 method names confirmed against plan §3.2 ✓
- No `GetStaticMethod` needed — correctly not added ✓
- **PASS**

### 5. Protected Tests & NT8 Invariant
- NT8-runtime-skip 335-count invariant stated in Do NOT Touch section ✓
- L6570 and L6580 (`GetSenderAccountName_ShouldReturnEmpty_*`) explicitly called out as NT8-runtime skips — DO NOT MODIFY ✓
- (The T3-scope protected tests are correctly not listed here) ✓
- **PASS**

### 6. Post-Implementation Gate
- `dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests"` — 0 failed mandated ✓
- Required passed-count increase stated (23, or 23 minus rollbacks) ✓
- **PASS**

### 7. Rollback Protocol
- Re-add Skip to failing test ✓
- Document as `DW-12-02` ✓
- Continue remaining tests ✓
- 0 failed before proceeding to T3 ✓
- **PASS**

### 8. 7-Scan Checklist Presence
- SCAN-01 through SCAN-07: all present with command and expected result ✓
- SCAN-04 compares against post-T1 count (correct for sequential pipeline) ✓
- SCAN-05 specifically checks NT8-runtime: = 335 ✓
- SCAN-07 scoped to `BwaveCycT1R1BeHelperTests` filter ✓
- **PASS**

### 9. NT8 Invariant
- SCAN-05: `grep "NT8-runtime:"` → must remain exactly 335 ✓
- **PASS**

### 10. Sequencing
- Prerequisite: T1 complete with 0 failed ✓ (explicitly stated)
- **PASS**

### 11. JS/DNA Rules
- All same as T1 — no violations ✓
- **PASS**

### T2 VERDICT: TICKET_REVIEW_PASS

---

## T3 — BwaveCycTaR2HelperTests + BwaveCycTaR3HelperTests (45 obfuscation-skip removals)

### 1. Traceability
- Spec requirement cited: `DW-09-04 (partial — 45 of 137)` ✓
- Maps to plan §5 T3 (R2 + R3 combined per plan's explicit design) ✓
- No phantom work ✓
- **PASS**

### 2. Scope Isolation
- Classes: `BwaveCycTaR2HelperTests` AND `BwaveCycTaR3HelperTests` — matches plan design ✓
- File: `src/PropTraderTools/CopyEngineTests.cs` only ✓
- R2 line range L6701–L6791 and R3 line range L6830–L7115 are disjoint ✓
- **PASS**

### 3. Exact Operation
- SEARCH/REPLACE strings globally defined ✓
- R2: 12 exact lines listed — count verified against plan §5 T3 R2 list ✓
- R3: 33 exact lines listed — count verified against plan §5 T3 R3 list ✓
- Total 45 confirmed (12 + 33) ✓
- **PASS**

### 4. Binding Flags
- R2 helpers (L6694–L6697): `GetMethod` (instance) + `GetStaticMethod` (static) — already present ✓
- R3 helpers (L6822–L6826): same two helpers — already present ✓
- All 12 R2 obfuscation-skip tests use `GetMethod` (instance) — 9 method names enumerated ✓
- All 33 R3 obfuscation-skip tests use `GetMethod` (instance) — method list confirmed against plan §3.4 ✓
- Zero additions required — confirmed ✓
- **PASS**

### 5. Protected Tests & NT8 Invariant
- **All 4 protected tests explicitly listed by name, line, class, and reason:**

| Test Name | Line | Class | Reason |
|-----------|------|-------|--------|
| `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate` | L6806 | R2 | Already-fixed plain `[Fact]`; uses `GetStaticMethod` |
| `OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod` | L6795 | R2 | Already-fixed plain `[Fact]` |
| `LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod` | L6937 | R3 | Already-fixed plain `[Fact]`; uses `GetStaticMethod` |
| `LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters` | L6944 | R3 | Already-fixed plain `[Fact]`; uses `GetStaticMethod` |

  Explicit mandate: "must not be modified in any way" ✓  
  NT8-runtime-skip 335-count invariant stated ✓  
- **PASS**

### 6. Post-Implementation Gate
- `dotnet test --filter "FullyQualifiedName~BwaveCycTaR2HelperTests|BwaveCycTaR3HelperTests"` — 0 failed mandated ✓
- Required passed-count increase stated (45, or fewer with rollbacks) ✓
- **PASS**

### 7. Rollback Protocol
- Re-add Skip to failing test ✓
- Document as `DW-12-03` ✓
- Continue remaining tests ✓
- 0 failed before proceeding to T4 ✓
- **PASS**

### 8. 7-Scan Checklist Presence
- SCAN-01 through SCAN-07: all present with command and expected result ✓
- SCAN-04 compares against post-T2 count ✓
- SCAN-05 specifically checks NT8-runtime: = 335 ✓
- SCAN-07: pipe character correctly shell-escaped as `\|` in filter string ✓
- **PASS**

### 9. NT8 Invariant
- SCAN-05: `grep "NT8-runtime:"` → must remain exactly 335 ✓
- **PASS**

### 10. Sequencing
- Prerequisite: T2 complete with 0 failed ✓ (explicitly stated)
- **PASS**

### 11. JS/DNA Rules
- All same as T1/T2 — no violations ✓
- **PASS**

### T3 VERDICT: TICKET_REVIEW_PASS

---

## T4 — BwaveCycTaR6HelperTests (10 obfuscation-skip removals — DW-09-04 FINAL CLOSURE)

### 1. Traceability
- Spec requirement cited: `DW-09-04 (final closure — 10 of 137; total 137 after T1+T2+T3+T4)` ✓
- Maps to plan §5 T4 ✓
- Cumulative total explicitly confirmed: 59+23+45+10=137 ✓
- **PASS**

### 2. Scope Isolation
- Class: `BwaveCycTaR6HelperTests` only ✓
- File: `src/PropTraderTools/CopyEngineTests.cs` only ✓
- **PASS**

### 3. Exact Operation
- SEARCH/REPLACE strings globally defined ✓
- 10 exact line numbers listed; count verified against plan §5 T4 line list ✓
- **PASS**

### 4. Binding Flags
- Helpers (L7123–L7127): `GetStaticMethod` + `GetInstanceMethod` — both already present ✓
- Note correctly states: NO unnamed `GetMethod` in this class (uses explicit `GetStaticMethod`/`GetInstanceMethod`) ✓
- Per-test binding table provided for all 10 removals:
  - 6 STATIC tests (L7131, L7138, L7178, L7185, L7265, L7272) → `GetStaticMethod` ✓
  - 4 INSTANCE tests (L7195, L7202, L7212, L7219) → `GetInstanceMethod` ✓
- All binding flags match plan §3.5 table exactly ✓
- Zero additions required ✓
- **PASS**

### 5. Protected Tests & NT8 Invariant
- Do NOT Touch lists: L7149, L7156, L7166 (`ExtractLegSuffix_*`) and L7229, L7236, L7245, L7254 (`IsPositionStateRelevant_*`) explicitly listed with NT8-runtime skip designation ✓
- NT8-runtime-skip 335-count invariant stated ✓
- (T4-scope protected tests are non-obfuscation skips; T3's 4 protected tests are not in T4 scope — correct) ✓
- **PASS**

### 6. Post-Implementation Gate
- Per-class gate: `dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests"` — 0 failed ✓
- **Final State Verify Gate** (full suite): `dotnet test src/PropTraderTools/CopyEngineTests.cs` with expected Passed=163, Failed=0, Skipped=351, Total=514 ✓
- Final confirmation: `grep "obfuscation:"` → 0 matches; `grep "NT8-runtime:"` → 335 matches ✓
- DW-09-04 CLOSED declaration present ✓
- **PASS**

### 7. Rollback Protocol
- Re-add Skip to failing test ✓
- Document as `DW-12-04` ✓
- Continue remaining tests ✓
- 0 failed before closing the epic ✓
- **PASS**

### 8. 7-Scan Checklist Presence
- SCAN-01 through SCAN-07: all present with command and expected result ✓
- SCAN-04: final value must be 0 (correctly stated for epic closure) ✓
- SCAN-05: `grep "NT8-runtime:"` → must remain exactly 335 ✓
- SCAN-07: full-suite `dotnet test` with exact Passed/Skipped/Total targets ✓
- **PASS**

### 9. NT8 Invariant
- SCAN-05: `grep "NT8-runtime:"` → must remain exactly 335 ✓
- **PASS**

### 10. Sequencing
- Prerequisite: T3 complete with 0 failed ✓ (explicitly stated)
- Is the terminal ticket in the chain ✓
- **PASS**

### 11. JS/DNA Rules
- All same as T1/T2/T3 — no violations ✓
- **PASS**

### T4 VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Checks

### Spec Coverage Matrix

| Requirement | Covered? | Ticket(s) |
|-------------|----------|-----------|
| Remove 137 `[Fact(Skip="obfuscation:...")]` | YES | T1(59)+T2(23)+T3(45)+T4(10)=137 ✓ |
| Touch ONLY `CopyEngineTests.cs` | YES | Global invariants + file field in all tickets ✓ |
| Do NOT touch 335 NT8-runtime-skip annotations | YES | SCAN-05 in all tickets + Do NOT Touch sections ✓ |
| Do NOT touch 3 already-fixed tests (GetSenderAccountName, LogBeSlotEviction ×2) | YES | T3 protected-tests table + T2 Do NOT Touch (L6570/L6580) ✓ |
| After every ticket: `dotnet test` = 0 failed (HARD) | YES | Post-Implementation Verify Gate in all tickets ✓ |
| Static method tests use correct BindingFlags | YES | Binding Flags Context sections in all tickets ✓ |
| ASCII-only, no lock(), no new throw, no CYC change | YES | Global invariants + per-ticket scan tables ✓ |
| Baseline 26p/0f/488s/514t → Target 163p/0f/351s/514t | YES | Baseline/Target table in tickets preamble ✓ |
| DW-09-04 closure | YES | Cited in all tickets; explicitly closed in T4 ✓ |
| Per-ticket 7-scan checklist | YES | Present in all 4 tickets ✓ |
| Rollback strategy | YES | Present and consistent in all 4 tickets ✓ |
| Sequential T1→T2→T3→T4 ordering | YES | Execution Order section + per-ticket prerequisites ✓ |

All 12 spec requirements covered. Zero uncovered. Zero phantom work.

### Warnings (Non-Blocking)

| ID | Ticket(s) | Description |
|----|-----------|-------------|
| WARN-01 | All (T1–T4) | Scan-set content diverges from plan §6 template: plan SCAN-01 (ASCII-only), SCAN-04 (DateTime.Now), and SCAN-06 (protected tests check) are absent from scan tables; covered instead by global invariants and Do NOT Touch sections. The SCAN-07 `dotnet test` gate provides equivalent protection for protected tests. Recommend architect note the divergence for future ticket-template consistency. |
| WARN-02 | All (T1–T4) | SCAN-01 expected result is "0 matches" for `grep "lock("`. Correct for this test file (no pre-existing lock() usage), but strictly the formulation should be "count must NOT increase." Not a FAIL since CopyEngineTests.cs is a pure test file with no existing lock() usage per all prior reviews. |

### Violations Requiring TICKET_REVIEW_FAIL

**None found.**

---

## Overall: TICKET_REVIEW_PASS

**Violations found: 0**  
**Warnings: 2 (non-blocking, noted above)**

All 4 tickets pass all 11 review checklist items:
1. TRACEABILITY — PASS (DW-09-04 cited in every ticket)
2. SCOPE ISOLATION — PASS (exact class + file + line range per ticket)
3. EXACT OPERATION — PASS (SEARCH/REPLACE strings precisely specified)
4. BINDING FLAGS — PASS (per-class, per-test analysis present and correct)
5. PROTECTED TESTS — PASS (T3 lists all 4 with DO NOT TOUCH; 335-count in all)
6. POST-IMPLEMENTATION GATE — PASS (dotnet test = 0 failed in every ticket)
7. ROLLBACK PROTOCOL — PASS (present and consistent in all 4 tickets)
8. 7-SCAN CHECKLIST — PASS (SCAN-01 through SCAN-07 with commands in all 4 tickets)
9. NT8 INVARIANT — PASS (SCAN-05 = 335 in all 4 tickets)
10. SEQUENCING — PASS (T1→T2→T3→T4 with explicit prerequisites)
11. JS/DNA RULES — PASS (no lock, no throw, ASCII-only, no CYC change, no production .cs)

**Gate: UNLOCKED — Phase 4 (engineering) may proceed.**

---

*Review completed by PTT Ticket Reviewer — PTT-REPAIRS-12-SKIP-REMOVAL Phase 3.5*  
*Source tickets: REVIEW_PASS confirmed by PTT Plan Reviewer (Phase 2)*  
*Total skip removals reviewed: 137 (T1=59 + T2=23 + T3=45 + T4=10)*
