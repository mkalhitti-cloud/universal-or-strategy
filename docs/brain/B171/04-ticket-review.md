# B171 Ticket Review
**Block:** B171 — DW-B171-01 + DW-B171-02: PTT-STP-Drag Duplicate Stop + fo=NULL on Stop Drag
**Reviewer:** ptt-ticket-reviewer (Phase 3.5)
**Source tickets:** `docs/brain/B171/04-tickets.md`
**Source plan:** `docs/brain/B171/02-architecture-plan.md` (PLAN_COMPLETE)
**Spec ground truth:** `docs/brain/B171-orchestrator-prompt.md`

---

## Ticket Review: B171

---

### T1 — ExecutePhaseCStopReplacement: Suffix + Cancel Sweep

**Spec IDs cited:** DW-B171-01 + DW-B171-02 ✓
**File:** `src/PropTraderTools/CopyEngine.cs` ✓

#### Traceability
Every changed line traces to a named work item:
- `string suffix = bracketIdx.ToString();` → DW-B171-01 + DW-B171-02 (suffix derivation)
- `if (stp <= 0.0)` guard block → DW-B171-01 (EC-1 regression protection, Variant E mandate)
- `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix)` → DW-B171-01 (cancel sweep)
- `CreateFollowerReplacementStop(..., stp, suffix)` → DW-B171-02 (suffix propagation)

No phantom work detected. All items in ticket map to plan §9 (B171-1 outline) and plan §4 Method A.
No plan items unaddressed by this ticket.

**Traceability: PASS**

#### Before/After Accuracy
BEFORE block: 3-line body with `DeriveLeaderBracketIndex`, `FindLeaderStopPrice`, `CreateFollowerReplacementStop` (no suffix, no cancel). Matches spec §Method A BEFORE verbatim. ✓

AFTER block verified element by element:
- `string suffix = bracketIdx.ToString();` ✓
- `if (stp <= 0.0)` guard ✓
- `StatusUpdate?.Invoke(acc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0")` ✓
- `return;` inside guard ✓
- `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix);` ✓
- `CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix);` ✓
- Annotation comment line 1: `// B171 DW-B171-01+DW-B171-02: ...` ✓
- Annotation comment line 2: `// CYC=2: ... JS-021: ... JS-001: ...` ✓
- Annotation comment line 3: `// JS-002: void return. ASCII-only.` ✓

AFTER block matches plan §4 Method A AFTER and spec §Method A AFTER verbatim. ✓

**Before/After Accuracy: PASS**

#### CYC Pre-Check
| Method | Before | After | Delta | Compliant |
|--------|--------|-------|-------|-----------|
| `ExecutePhaseCStopReplacement` | 2 | 2 | 0 | YES (<=8) |

Manual count confirmed: base(1) + `if(stp<=0.0)` branch(1) = CYC 2. Matches ticket table. ✓
The `if(stp<=0.0)` guard replaces the delegated `if(stopPrice<=0.0)` guard previously in `CreateFollowerReplacementStop`. Net CYC is unchanged at 2. ✓

**CYC Pre-Check: PASS**

#### JS Pre-Check
| Rule | Ticket Status | Reviewer Verdict |
|------|--------------|-----------------|
| JS-021 — No `lock()` | PASS | No `lock` keyword in AFTER block ✓ |
| JS-001 — No unhandled throw in hot path | PASS | Exception handling delegated to `SubmitReplacementStopOrder` `try/catch` ✓ |
| JS-002 — Void return | PASS | `private void` unchanged ✓ |
| ASCII-only literals | PASS | All string literals are pure ASCII ✓ |

**JS Pre-Check: PASS**

#### NT8 Check
- No `lock()` introduced ✓
- No `DateTime.Now` introduced ✓
- No `FontFamily` introduced ✓
- All string literals ASCII-only ✓
- `apply_diff` constraint stated explicitly: "Engineer MUST use `apply_diff` on `src/PropTraderTools/CopyEngine.cs`. `write_file` is BANNED on this file." ✓
- `ptt-sync.ps1` stated: "Run after ALL tickets complete" ✓
- No async/await in lifecycle method ✓
- No `CreateOrder` name not starting "PTT-": order name `"PTT-STP-Drag-" + suffix` starts with "PTT-" ✓

**NT8 Check: PASS**

#### Test Coverage
B171-1 introduces no new public or internal methods. Signature of `ExecutePhaseCStopReplacement` is unchanged. Test coverage for the B171 changes is addressed in B171-3 (per plan §9). No new testable surface introduced that is not covered downstream.

**Test Coverage: PASS** (covered in B171-3 as intended by plan)

#### Scan Checklist
| Scan | Present | Command Specified | Expected Result Specified |
|------|---------|------------------|--------------------------|
| SCAN-01 | ✓ | `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` | 0 |
| SCAN-02 | ✓ | `grep -n '"PTT-STP-Drag"' src/PropTraderTools/CopyEngine.cs` (post-B171-2) | 0 matches |
| SCAN-03 | ✓ | Manual CYC count: base(1)+if(1) | CYC=2 |
| SCAN-04 | ✓ | Inspect AFTER block string literals | No non-ASCII |
| SCAN-05 | ✓ | `grep -n "ExecutePhaseCStopReplacement" src/PropTraderTools/CopyEngine.cs` | 1 call site |
| SCAN-06 | ✓ | Review engineer session log | `write_file` not called |
| SCAN-07 | ✓ | `powershell -File .\scripts\ptt-sync.ps1` | Exit code 0 |

All 7 scans present with commands and expected results. ✓

**Scan Checklist: PASS**

#### File Routing
`src/PropTraderTools/CopyEngine.cs` → Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). Correct. ✓

**File Routing: PASS**

### VERDICT T1: TICKET_REVIEW_PASS

---

### T2 — CreateFollowerReplacementStop + SubmitReplacementStopOrder: Suffix Threading

**Spec IDs cited:** DW-B171-01 + DW-B171-02 ✓
**File:** `src/PropTraderTools/CopyEngine.cs` ✓
**Dependency on B171-1 documented:** ✓ ("B171-1 MUST be committed first — compile dependency")

#### Traceability
**Change A (`CreateFollowerReplacementStop`):**
- `string suffix` param added → DW-B171-02 (suffix pass-through to SubmitReplacementStopOrder)
- StatusUpdate message updated to include suffix → DW-B171-02 (consistent naming)
- SubmitReplacementStopOrder call gains `, suffix` → DW-B171-02 (suffix threading)

**Change B (`SubmitReplacementStopOrder`):**
- `string suffix` param added → DW-B171-02
- Order name: `"PTT-STP-Drag"` → `"PTT-STP-Drag-" + suffix` → DW-B171-02 (root-cause fix at line 4478)
- Null-check StatusUpdate: gains suffix → DW-B171-02
- Placed StatusUpdate: gains suffix → DW-B171-02

No phantom work. All items map to plan §4 Methods B+C and plan §9 (B171-2 outline).
No plan items unaddressed.

**Traceability: PASS**

#### Before/After Accuracy — Change A (CreateFollowerReplacementStop)
BEFORE: 5-param signature (no suffix), `"PTT-STP-Drag skipped"` in StatusUpdate, `SubmitReplacementStopOrder` 5-param call. Matches spec §Method B BEFORE verbatim. ✓

AFTER verified:
- 6-param signature with `string suffix` ✓
- `StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0")` ✓
- `SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix)` (6-param) ✓
- Annotation comment with B171 tag ✓

AFTER matches plan §4 Method B AFTER verbatim. ✓

**Before/After Accuracy Change A: PASS**

#### Before/After Accuracy — Change B (SubmitReplacementStopOrder)
BEFORE: 5-param signature, `"PTT-STP-Drag"` at CreateOrder name, `"PTT-STP-Drag: CreateOrder returned null"` at null-check, `"PTT-STP-Drag placed @"` at placed msg, `"PTT-STP-Drag error: "` in catch. Matches spec §Method C BEFORE verbatim. ✓

AFTER verified for all three mandated literals:
- **Literal 1 — order name:** `"PTT-STP-Drag-" + suffix` ✓
- **Literal 2 — null-check msg:** `"PTT-STP-Drag-" + suffix + ": CreateOrder returned null"` ✓
- **Literal 3 — placed msg:** `"PTT-STP-Drag-" + suffix + " placed @ "` ✓
- Catch block literal: `"PTT-STP-Drag error: "` — unsuffixed, matching plan §4 Method C AFTER (catch message is not one of the three mandated literals per spec; catch log remains unsuffixed in both plan and spec). ✓
- 6-param signature with `string suffix` ✓
- Annotation comment with `B171 DW-B171-02` tag ✓

AFTER matches plan §4 Method C AFTER verbatim. ✓

**Before/After Accuracy Change B: PASS**

#### CYC Pre-Check
| Method | Before | After | Delta | Compliant |
|--------|--------|-------|-------|-----------|
| `CreateFollowerReplacementStop` | 2 | 2 | 0 | YES (<=8) |
| `SubmitReplacementStopOrder` | 4 | 4 | 0 | YES (<=8) |

`CreateFollowerReplacementStop`: base(1) + `if(stopPrice<=0.0)`(1) = 2. ✓
`SubmitReplacementStopOrder`: base(1) + `try` block(1) + `if(newStop==null)`(1) + `catch`(1) = 4. ✓
Both match ticket CYC table. ✓

**CYC Pre-Check: PASS**

#### JS Pre-Check
| Rule | Ticket Status | Reviewer Verdict |
|------|--------------|-----------------|
| JS-021 — No `lock()` | PASS | No `lock` keyword in either AFTER block ✓ |
| JS-001 — No unhandled throw | PASS | `SubmitReplacementStopOrder` wraps in `try/catch`; `CreateFollowerReplacementStop` delegates ✓ |
| JS-002 — Void return | PASS | Both `private void` unchanged ✓ |
| ASCII-only literals | PASS | All string literals pure ASCII; `+` concat with `suffix` is ASCII ✓ |

**JS Pre-Check: PASS**

#### NT8 Check
- No `lock()` introduced ✓
- No `DateTime.Now` introduced ✓
- No `FontFamily` introduced ✓
- All string literals ASCII-only ✓
- `apply_diff` constraint stated: "Engineer MUST use `apply_diff`... `write_file` is BANNED" ✓
- Line-number shift note present: B171-1 adds 4 lines; engineer instructed to read file before issuing apply_diff ✓
- `ptt-sync.ps1` stated: "Run after ALL tickets complete" ✓

**NT8 Check: PASS**

#### Test Coverage
B171-2 modifies internal method signatures. No new public/internal methods are introduced that lack test coverage. The signature changes are covered by B171-3 Tests 1 and 2 (reflection parameter count/name assertions on `SubmitReplacementStopOrder` and `CreateFollowerReplacementStop`). ✓

**Test Coverage: PASS** (covered in B171-3)

#### Scan Checklist
| Scan | Present | Command Specified | Expected Result Specified |
|------|---------|------------------|--------------------------|
| SCAN-01 | ✓ | `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` | 0 |
| SCAN-02 | ✓ | `grep -n '"PTT-STP-Drag"' src/PropTraderTools/CopyEngine.cs` | **CRITICAL** 0 matches — all 3 unsuffixed literals now suffixed |
| SCAN-03 | ✓ | Manual CYC count both methods | CreateFollowerReplacementStop=2; SubmitReplacementStopOrder=4 |
| SCAN-04 | ✓ | Inspect all AFTER lines both methods | No non-ASCII |
| SCAN-05 | ✓ | `grep -n "CreateFollowerReplacementStop\|SubmitReplacementStopOrder" src/PropTraderTools/CopyEngine.cs` | 1 definition + 1 call site each |
| SCAN-06 | ✓ | Review engineer session log | `write_file` not called |
| SCAN-07 | ✓ | `powershell -File .\scripts\ptt-sync.ps1` | Exit code 0 |

All 7 scans present with commands and expected results. ✓

**Scan Checklist: PASS**

#### File Routing
`src/PropTraderTools/CopyEngine.cs` → Wave workspace. Correct. ✓

**File Routing: PASS**

### VERDICT T2: TICKET_REVIEW_PASS

---

### T3 — xUnit Tests

**Spec IDs cited:** DW-B171-01 + DW-B171-02 ✓
**File:** `src/PropTraderTools/CopyEngineTests.cs` ✓
**Dependency on B171-1 + B171-2 documented:** ✓ ("B171-1 AND B171-2 MUST be committed first and the file must compile")

#### Traceability
All 5 test methods trace to spec §TICKETS / B171-3 test coverage mandate:
- Tests 1–5 map to the 5 scenarios listed in spec B171-3 and plan §9 B171-3 outline. ✓
No phantom test scenarios. No plan-mandated scenarios missing. ✓

**Traceability: PASS**

#### Test Coverage — All 5 Scenarios Verified

| # | Spec Scenario | Test Method Name | Present |
|---|---------------|-----------------|---------|
| 1 | Single target drag → exactly one PTT-STP-Drag-{N} per follower | `ExecutePhaseCStopReplacement_SingleTargetDrag_ExactlyOneSuffixedStopPerFollower` | ✓ |
| 2 | Two drags same bracket → duplicate cancelled, still one stop | `ExecutePhaseCStopReplacement_TwoDragsOnSameBracket_DuplicateCancelled_StillOneStop` | ✓ |
| 3 | Two drags different brackets → two distinct suffixed stops | `ExecutePhaseCStopReplacement_TwoDragsOnDifferentBrackets_TwoDistinctSuffixedStops` | ✓ |
| 4 | Stop drag after target drag → fo != NULL | `StopDragAfterTargetDrag_FindsFollowerOrder_FoNotNull` | ✓ |
| 5 | stp <= 0 path → no cancel fired, EC-1 guard | `ExecutePhaseCStopReplacement_StpZeroOrNegative_NoCancelFired_ExistingStopPreserved` | ✓ |

All 5 scenarios present. ✓

Test access pattern: All tests use `GetMethod(..., BindingFlags.NonPublic | BindingFlags.Instance)` — reflection-based seam, consistent with established `CopyEngineTests.cs` pattern. No direct private method calls without reflection. ✓

All tests carry `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` — consistent with existing skip pattern for NT8-runtime-dependent tests. ✓

Test 4 (`StopDragAfterTargetDrag_FindsFollowerOrder_FoNotNull`) makes a live `method.Invoke` on `IsDragNameMatch` — a pure string-comparison method. The skip guard is present. This is the established pattern. ✓

**Test Coverage: PASS**

#### CYC Pre-Check
Test methods are linear arrange/assert sequences. No production-method branching introduced. Maximum test-body CYC is 2 (Test 5 has one `try/finally` block). All <= 8. ✓

**CYC Pre-Check: PASS**

#### JS Pre-Check
| Rule | Ticket Status | Reviewer Verdict |
|------|--------------|-----------------|
| JS-021 — No `lock()` | PASS | No `lock` in any test ✓ |
| JS-001 — No unhandled throw | PASS | Tests use `Assert.*`; no bare throw ✓ |
| JS-002 — Void return | PASS | All `public void` ✓ |
| ASCII-only literals | PASS | `"PTT-STP-Drag-3"`, `"3"`, `"suffix"`, `"fo"` etc. are pure ASCII ✓ |

**JS Pre-Check: PASS**

#### NT8 Check
- No `lock()` ✓
- No `DateTime.Now` introduced (existing file uses `DateTime.UtcNow.Ticks`; none added) ✓
- No `FontFamily` ✓
- ASCII-only string literals ✓
- `apply_diff` constraint stated: "`write_file` is banned on existing `.cs` files" ✓
- `ptt-sync.ps1` stated: "Run after ALL tickets complete" ✓

**NT8 Check: PASS**

#### Scan Checklist
| Scan | Present | Command Specified | Expected Result Specified |
|------|---------|------------------|--------------------------|
| SCAN-01 | ✓ | Build `src/PropTraderTools/` after adding tests | Build succeeds, zero compilation errors |
| SCAN-02 | ✓ | `grep -n "public void Exe\|public void Stop" src/PropTraderTools/CopyEngineTests.cs` | All 5 method names present |
| SCAN-03 | ✓ | `grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs` | 0 |
| SCAN-04 | ✓ | Inspect all string literals in 5 new tests | No non-ASCII |
| SCAN-05 | ✓ | Manual check against spec §TICKETS/B171-3 | Scenarios 1-5 all present |
| SCAN-06 | ✓ | Review engineer session log | `write_file` not called on `CopyEngineTests.cs` |
| SCAN-07 | ✓ | `powershell -File .\scripts\ptt-sync.ps1` | Exit code 0 |

All 7 scans present with commands and expected results. ✓

**Note:** B171-3 scan labels differ from B171-1/B171-2 (e.g., SCAN-01 is "tests compile" rather than "lock grep"). This is intentional — B171-3 operates on the test file. The scan contract remains 7 scans deep and fully specified. The engineer and verifier anchor is intact.

**Scan Checklist: PASS**

#### File Routing
`src/PropTraderTools/CopyEngineTests.cs` → Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). Correct. ✓

**File Routing: PASS**

### VERDICT T3: TICKET_REVIEW_PASS

---

## Aggregate Checks

### Spec Coverage (all three tickets combined)

| Spec Requirement | Covered By | Coverage Status |
|-----------------|-----------|----------------|
| DW-B171-01 — Cancel sweep on target-drag path | B171-1 | COVERED (single ticket) |
| DW-B171-02 — Suffix threading to fix name mismatch | B171-2 | COVERED (single ticket) |
| DW-B171-01 + DW-B171-02 — Test verification | B171-3 | COVERED (single ticket, 5 scenarios) |

No uncovered requirements. No duplicate coverage on a single requirement across tickets (cancel sweep owned by B171-1; suffix threading owned by B171-2; tests owned by B171-3). ✓

### Completeness

| Check | Status |
|-------|--------|
| 3 tickets total (B171-1, B171-2, B171-3) | ✓ |
| B171-2 dependency on B171-1 documented | ✓ |
| B171-3 dependency on B171-1 + B171-2 documented | ✓ |
| Sequential execution constraint stated | ✓ |
| Ticket dependency graph present | ✓ |

### Missing Work Check

Plan §9 defines three tickets:
- B171-1: Method A (ExecutePhaseCStopReplacement) → covered by T1 ✓
- B171-2: Methods B + C (CreateFollowerReplacementStop + SubmitReplacementStopOrder) → covered by T2 ✓
- B171-3: xUnit tests, 5 scenarios → covered by T3 ✓

No plan items absent from tickets. ✓

---

## Overall: TICKET_REVIEW_PASS

All three tickets pass all checks. Zero violations detected.

**Gate result: TICKET_REVIEW_PASS — safe to spawn ptt-engineer (Phase 4a).**

Engineer reads `docs/brain/B171/04-ticket-review.md` first, then `docs/brain/B171/04-tickets.md`.
Apply tickets in order: B171-1 → B171-2 → B171-3. Run `ptt-sync.ps1` once after all three.
