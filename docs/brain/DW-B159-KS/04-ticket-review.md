# Ticket Review: DW-B159-KS
<!-- status: TICKET_REVIEW_PASS -->
<!-- epic: DW-B159-KS — K-Suite: K2 + K3 + K5 -->
<!-- phase: 3.5 -->
<!-- author: ptt-ticket-reviewer -->
<!-- date: 2026-10-01 -->
<!-- source: docs/brain/DW-B159-KS/04-tickets.md -->
<!-- plan: docs/brain/DW-B159-KS/02-architecture-plan.md -->
<!-- backlog: docs/brain/B159/06-deferred-backlog.md -->

---

## Review Summary

| Ticket | Title | Verdict |
|--------|-------|---------|
| T1 | DW-B159-K5: Comment Fix | TICKET_REVIEW_PASS |
| T2 | DW-B159-K3: Extract Literal to Named Constant | TICKET_REVIEW_PASS |
| T3 | DW-B159-K2: 14 xUnit Tests | TICKET_REVIEW_PASS |

---

## T1 — DW-B159-K5: Comment Fix

### Traceability

**Spec req cited:** `B159 K5 — stale HasOpenPosition line-number annotation` (ticket header, line 41).
**Plan section:** §3.T1 (02-architecture-plan.md, lines 152–184).
**Backlog source:** DW-B159-K5 (06-deferred-backlog.md, lines 124–141) — stale line-number comment at `IsFollowerPositionBlocked` header.
**Phantom work:** None. Single comment update, exactly as backlog describes.
**Missing coverage:** None. T1 closes K5 precisely.

Traceability: **PASS**

---

### JS Pre-Check

| Rule | Finding | Verdict |
|------|---------|---------|
| JS-CYC | `IsFollowerPositionBlocked` CYC=8 before; CYC=8 after. Comment-only diff — zero branches. Delta=0. | PASS |
| JS-001 | Zero executable statements added. No `throw` anywhere near line 3196. | PASS |
| JS-021 | Zero executable statements added. No `lock()`. | PASS |
| JS-042 | New comment text `(HasOpenPosition, line 5507)` is entirely printable ASCII. Verified by architect grep. | PASS |
| JS-002 | No return value touched. `IsFollowerPositionBlocked` returns `bool` — no null-return path introduced. | PASS |

JS Pre-Check: **PASS**

---

### CYC Pre-Check

| Method | CYC Before | CYC After | Delta |
|--------|-----------|-----------|-------|
| `IsFollowerPositionBlocked` | 8 | 8 | **0** |

Source spot-check confirmed: line 3196 is a comment inside the method body — no branch introduced.
CYC delta = 0; well within limit of 8.

CYC Pre-Check: **PASS**

---

### NT8 Check

No NT8 API calls introduced. No lifecycle methods modified. No async/await, no Account.All, no DateTime.Now,
no FontFamily, no hardcoded hex colors. Comment-only change has zero NT8 surface.

NT8 Check: **PASS**

---

### Test Coverage

No methods created or modified. Comment-only ticket. No [Fact] test required for a comment change.

Test Coverage: **PASS** (N/A — zero methods added/modified)

---

### Scan Checklist

SCAN-01 through SCAN-07 all present at ticket lines 102–148.

| Scan | Present |
|------|---------|
| SCAN-01 | ✓ line 102 |
| SCAN-02 | ✓ line 107 |
| SCAN-03 | ✓ line 112 |
| SCAN-04 | ✓ line 117 |
| SCAN-05 | ✓ line 121 |
| SCAN-06 | ✓ line 125 |
| SCAN-07 | ✓ line 135 |

Scan Checklist: **PASS**

---

### File Routing

`src/PropTraderTools/CopyEngine.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). Correct.

File Routing: **PASS**

---

### Acceptance Criteria

5 grep-verifiable criteria at ticket lines 153–159. All include exact grep command and expected result.
CYC manual review criterion also specified.

Acceptance Criteria: **PASS**

---

### VERDICT T1: TICKET_REVIEW_PASS

---

## T2 — DW-B159-K3: Extract Literal to Named Constant

### Traceability

**Spec req cited:** `B159 K3 — magic literal 3 in TryArmBePriceRetry` (ticket header, line 172).
**Plan section:** §3.T2 (02-architecture-plan.md, lines 193–253).
**Backlog source:** DW-B159-K3 (06-deferred-backlog.md, lines 85–101) — `attempts >= 3` literal in `TryArmBePriceRetry`.
**Phantom work:** None. Exactly 2 change sites as described: const declaration + literal replacement. K3-UI (settings panel) explicitly excluded.
**Missing coverage:** None. K3-UI deferred status explicitly acknowledged (ticket line 174, plan line 233).

Traceability: **PASS**

---

### JS Pre-Check

| Rule | Finding | Verdict |
|------|---------|---------|
| JS-CYC | `TryArmBePriceRetry` CYC=5 before; CYC=5 after. `if (attempts >= 3)` replaced by `if (attempts >= BeRetryMaxAttempts)` — same branch, same condition expression, zero new branches. Delta=0. | PASS |
| JS-001 | Const declaration and literal replacement are zero-throw changes. No `throw` introduced. | PASS |
| JS-021 | `private const int BeRetryMaxAttempts = 3;` is a compile-time constant — not a field, not a variable, not a shared runtime value. No `lock()` introduced. | PASS |
| JS-042 | Const identifier `BeRetryMaxAttempts` is ASCII. All four const comment lines are ASCII-only printable text (ticket lines 195–198). | PASS |
| JS-002 | `TryArmBePriceRetry` returns `void`. No predicate return path introduced or changed. | PASS |

JS Pre-Check: **PASS**

---

### CYC Pre-Check

| Method | CYC Before | CYC After | Delta |
|--------|-----------|-----------|-------|
| `TryArmBePriceRetry` | 5 | 5 | **0** |

Branch breakdown (ticket line 228): base(1) + acc==null(2) + instr==null(3) + bePrice<=0.0(4) + attempts>=BeRetryMaxAttempts(5) = 5.
Source spot-check confirmed: line 7192 is `if (attempts >= 3)  return;` — literal-only replacement, no structural change.

CYC Pre-Check: **PASS**

---

### NT8 Check

No NT8 API calls introduced. Const is C# compile-time — no NT8 runtime type involved. No lifecycle methods
modified. No async/await, no Account.All, no DateTime.Now, no FontFamily, no hex colors.

NT8 Check: **PASS**

---

### K3 Scope Boundary

Hard boundary stated three times:
- Ticket header (line 174): `"K3-UI (settings panel wiring) remains deferred"`
- Change Description (lines 182–184): `"SCOPE BOUNDARY (HARD): CopyEngine.cs ONLY. BeRetryMaxAttempts is NOT wired to the settings panel."`
- SCAN-07 (lines 305–307): `grep "BeRetryMaxAttempts" PropTraderToolsSettingsPanel.cs` → expected 0 matches
- Acceptance Criteria (line 318): same grep → expected 0 matches

Settings panel NOT in scope. K3-UI explicitly deferred. Scope boundary: **ENFORCED**.

---

### Test Coverage

No new methods created. Const extraction is a single literal rename — no [Fact] test required for
a named-constant substitution that is a pure identity transform. Method signature unchanged.
The existing test suite (via T3 in this same block) covers `TryArmBePriceRetry` guard semantics.

Test Coverage: **PASS** (N/A — zero methods added/modified)

---

### Scan Checklist

SCAN-01 through SCAN-07 all present at ticket lines 251–308.

| Scan | Present |
|------|---------|
| SCAN-01 | ✓ line 251 |
| SCAN-02 | ✓ line 255 |
| SCAN-03 | ✓ line 262 |
| SCAN-04 | ✓ line 270 |
| SCAN-05 | ✓ line 275 |
| SCAN-06 | ✓ line 279 |
| SCAN-07 | ✓ line 289 |

Scan Checklist: **PASS**

---

### File Routing

`src/PropTraderTools/CopyEngine.cs` ONLY — Wave workspace. Correct.
No changes to `PropTraderToolsSettingsPanel.cs` or any other file.

File Routing: **PASS**

---

### Acceptance Criteria

7 grep-verifiable criteria at ticket lines 313–321. All include exact grep command, file path, and
expected result. CYC manual review criterion specified.

Acceptance Criteria: **PASS**

---

### VERDICT T2: TICKET_REVIEW_PASS

---

## T3 — DW-B159-K2: 14 xUnit Tests

### Traceability

**Spec req cited:** `B159 K2 — 14 missing [Fact] tests for FM-A and FM-B guard paths` (ticket header, line 334).
**Plan section:** §3.T3 (02-architecture-plan.md, lines 257–380).
**Backlog source:** DW-B159-K2 (06-deferred-backlog.md, lines 51–81) — 14 xUnit tests not implemented in B159 pipeline.
**Phantom work:** None. All 14 methods trace directly to the deferred backlog's canonical method list.
**Missing coverage:** None. All 5 method surfaces covered (ShouldSkipFollower, IsFollowerPositionBlocked, TryFireBePriceRetry, TryArmBePriceRetry, StillAtNonBePrice).

Traceability: **PASS**

---

### JS Pre-Check

| Rule | Finding | Verdict |
|------|---------|---------|
| JS-CYC | All 14 test methods are straight-line BCL simulation + single assert. CYC=1 or 2 per method. Max=2 vs budget=8. | PASS |
| JS-001 | No `throw` in any test body. `Assert.*` are xUnit contract methods, not production `throw` statements. SCAN-01 confirms `grep "throw" (B159KSuiteTests region)` → 0 matches. | PASS |
| JS-021 | All concurrency state uses `ConcurrentDictionary<string,T>` (BCL lock-free). No `lock()` anywhere in any test body. SCAN-02 confirms 0 matches. | PASS |
| JS-042 | All string literals enumerated at ticket lines 585–588: `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-"`, `"PTT-QX-Stop-1"`, `"Sim102"`, method name strings — all ASCII-only. | PASS |
| JS-002 | All 14 test methods return `void`. No predicate method introduced. | PASS |

JS Pre-Check: **PASS**

---

### CYC Pre-Check

All 14 test methods: CYC = 1–2 (straight-line: allocate → simulate inline → assert). No method
approaches the CYC=8 limit. No nested loops, no deep branching.

CYC Pre-Check: **PASS**

---

### NT8 Check

| Constraint | Finding | Verdict |
|-----------|---------|---------|
| No `new CopyEngine()` | Ticket lines 348–349 explicitly prohibit; acceptance criterion line 662 greps for 0 matches. | PASS |
| No `CopyEngine.Instance` | Acceptance criterion line 663 greps for 0 matches. | PASS |
| No NT8 runtime type instantiation | All behavioral tests use `ConcurrentDictionary`, `string`, `double`, `bool`, `object` (pure BCL). Structural tests use `typeof(CopyEngine).GetMethod(...)` — type metadata only, no instance. Pattern references `LaneFTests`/`LaneGTests` at ticket lines 355–360. | PASS |
| No async/await in test methods | All 14 methods are `public void` (synchronous). No lifecycle method touch. | PASS |

NT8 Check: **PASS**

---

### 14 Method Names — Verbatim Check

All 14 canonical method names present verbatim in T3 ticket (lines 375–393 and SCAN-07 lines 627–641).
Cross-checked against deferred backlog DW-B159-K2 list (06-deferred-backlog.md lines 63–79). 100% match.

| # | Method Name | Ticket | Backlog | SCAN-07 |
|---|-------------|--------|---------|---------|
| 1 | `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue` | ✓ | ✓ | ✓ |
| 2 | `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse` | ✓ | ✓ | ✓ |
| 3 | `IsFollowerPositionBlocked_NullAccount_ReturnsFalse` | ✓ | ✓ | ✓ |
| 4 | `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse` | ✓ | ✓ | ✓ |
| 5 | `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` | ✓ | ✓ | ✓ |
| 6 | `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` | ✓ | ✓ | ✓ |
| 7 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop` | ✓ | ✓ | ✓ |
| 8 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop` | ✓ | ✓ | ✓ |
| 9 | `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry` | ✓ | ✓ | ✓ |
| 10 | `TryFireBePriceRetry_WorkingOrder_RemovesFromDict` | ✓ | ✓ | ✓ |
| 11 | `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly` | ✓ | ✓ | ✓ |
| 12 | `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` | ✓ | ✓ | ✓ |
| 13 | `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` | ✓ | ✓ | ✓ |
| 14 | `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` | ✓ | ✓ | ✓ |

14-method check: **PASS**

---

### [Skip] Check

Ticket line 336: `"NO [Skip] on any"`. SCAN-07 (line 623): `grep "Skip" (B159KSuiteTests region)` → 0 matches required. No `[Skip]` described or implied anywhere.

[Skip] Check: **PASS**

---

### Test Coverage

Methods under test → test IDs:
- `ShouldSkipFollower` — tests 1, 2
- `IsFollowerPositionBlocked` — tests 3, 4, 5, 6
- `TryFireBePriceRetry` — tests 7, 8, 10, 11, 13
- `TryArmBePriceRetry` — tests 9, 12
- `StillAtNonBePrice` — test 14

All 5 K2-specified method surfaces covered. Each test has a [Fact] attribute, no parameters, returns void.

Test Coverage: **PASS**

---

### Scan Checklist

SCAN-01 through SCAN-07 all present at ticket lines 572–648.

| Scan | Present |
|------|---------|
| SCAN-01 | ✓ line 572 |
| SCAN-02 | ✓ line 578 |
| SCAN-03 | ✓ line 583 |
| SCAN-04 | ✓ line 592 |
| SCAN-05 | ✓ line 599 |
| SCAN-06 | ✓ line 605 |
| SCAN-07 | ✓ line 615 |

Scan Checklist: **PASS**

---

### File Routing

`src/PropTraderTools/CopyEngineTests.cs` — Wave workspace. Correct.
No production file (`CopyEngine.cs`) touched by T3.

File Routing: **PASS**

---

### Acceptance Criteria

9 grep/command verifiable criteria at ticket lines 653–663. Includes `dotnet test` with `Failed=0` and
`Passed increases by 14`. All 14 individual method name greps in SCAN-07 (lines 627–641).

Acceptance Criteria: **PASS**

---

### VERDICT T3: TICKET_REVIEW_PASS

---

## Execution Order Check

T1→T2→T3 sequential order specified at ticket lines 11–28 and architecture plan §9.
T1 and T2 both modify `CopyEngine.cs` — sequential execution explicitly mandated to prevent diff collision.
T3 modifies only `CopyEngineTests.cs` — correctly placed after T1+T2.
`ptt-sync.ps1` and `deploy-sync.ps1` post-completion commands present (ticket lines 682–697).

Execution Order: **PASS**

---

## Violations Log

**None.** Zero violations detected across all three tickets.

---

## Overall: TICKET_REVIEW_PASS

All three tickets pass all checks:

| Check | T1 | T2 | T3 |
|-------|----|----|-----|
| Traceability | PASS | PASS | PASS |
| JS Pre-Check | PASS | PASS | PASS |
| CYC Pre-Check (delta=0 where required) | PASS (delta=0) | PASS (delta=0) | PASS (CYC≤2) |
| NT8 Check | PASS | PASS | PASS |
| K3 Scope Boundary | N/A | PASS | N/A |
| 14 Method Names | N/A | N/A | PASS |
| [Skip] Check | N/A | N/A | PASS |
| Test Coverage | PASS (N/A) | PASS (N/A) | PASS |
| Scan Checklist (SCAN-01–07) | PASS | PASS | PASS |
| File Routing | PASS | PASS | PASS |
| Acceptance Criteria | PASS | PASS | PASS |

**Engineer contract is clean. Safe to proceed to Phase 4a.**

---

*Ticket review produced by ptt-ticket-reviewer. Phase 3.5. Wave DW-B159-KS. Date 2026-10-01.*
*Engineer reads this file first, then 04-tickets.md. Execute T1 → T2 → T3 sequentially.*
