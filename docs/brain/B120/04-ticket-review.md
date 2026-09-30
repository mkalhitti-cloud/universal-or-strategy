# Ticket Review: B120 — DW-B129 Leader Fallback Flatten

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Cycle**: 2 (re-review after TR4 fix)
**Tickets reviewed**: `docs/brain/B120/04-tickets.md`
**Plan reviewed**: `docs/brain/B120/02-architecture-plan.md`
**Date**: 2026-08-28

---

## TR4 Fix Verification (cycle 2 primary check)

The prior cycle returned `TICKET_REVIEW_FAIL` on TR4: the Method Signatures block used
`internal void ExecuteFollowers` while Fix Step 1, Acceptance Criterion D, and plan Section C3
all said `private void ExecuteFollowers`.

**All four anchors now read `private void ExecuteFollowers`**:

| Anchor | Value | Result |
|--------|-------|--------|
| Fix Step 1 code block | `private void ExecuteFollowers(` | PASS |
| Method Signatures block | `private void ExecuteFollowers(` | PASS |
| Acceptance Criterion D | `ExecuteFollowers() extracted as private void` | PASS |
| Plan Section C3 | `private void ExecuteFollowers(` | PASS |
| Plan Section H invariant table | `private` | PASS |

TR4 is **RESOLVED**.

---

## T1 — DW-B129: Leader Fallback Flatten After B118 PTT-BE Cancel

### TR1 — Traceability

Every item in the ticket maps to a plan section or spec requirement:

| Ticket item | Maps to |
|-------------|---------|
| Fix Step 1 — `ExecuteFollowers` extraction | Plan Section C3 |
| Fix Step 2 — `NeedsLeaderFallbackFlatten` helper | Plan Section C1 |
| Fix Step 3 — flatten guard in `Execute()` | Plan Section C2 |
| Fix Step 4 — call site replacement | Plan Section C3 |
| Tests (3 `[Fact]` methods) | Plan Section F |
| `Account.Flatten` NT8 usage | Plan Section E |
| Acceptance criteria A–K | Plan Sections C1–C3, H |

No phantom work. No plan item absent from the ticket.

**TR1: PASS**

### TR2 — Spec Coverage

Single ticket B120-T1 covers the single in-scope defect `DW-B129`. The **Spec Req IDs**
field explicitly lists `DW-B129`. No uncovered requirement. No duplicate coverage.

**TR2: PASS**

### TR3 — JS Pre-Check (Concurrency — JS-021/023/025)

No `lock()` described anywhere in the ticket. `NeedsLeaderFallbackFlatten` is a
`static bool` method (no shared state mutation). `ExecuteFollowers` is a `private void`
instance method with no lock described. Scan 1 in the 7-scan checklist confirms
the zero-result requirement.

No `Dictionary<K,V>` on class fields described. No UI update from non-UI thread described.

**TR3: PASS**

### TR4 — Access Modifier Consistency (`private void ExecuteFollowers`)

All four anchors verified as `private void ExecuteFollowers` (see fix verification table above).

**TR4: PASS**

### TR5 — JS Pre-Check (Type Safety — JS-001/002/003)

- JS-001 (`throw`): No `throw new ...` described in any new method. `NeedsLeaderFallbackFlatten`
  returns `bool`; no exception path described.
- JS-002 (null return): `NeedsLeaderFallbackFlatten` returns `bool` (not null-capable).
  `ExecuteFollowers` returns `void`. Scan 5 explicitly states this.
- JS-003 (sentinel string for state): No string/empty-string sentinel used for mode or state.

**TR5: PASS**

### TR6 — JS Pre-Check (Immutability — JS-008/009)

No mutable fields on a struct described. No `SolidColorBrush` without `.Freeze()` described.
No `Dictionary<K,V>` on `CopyRule` or `CopyEngine` fields described.

**TR6: PASS**

### TR7 — NT8 Constraint Check

- No `async/await` in lifecycle methods described.
- No `Account.All` call outside a Loaded handler described — existing pattern in `Execute()`
  is unchanged; no new NT8 API access pattern introduced.
- No `sealed` on `TradeCopierWindow` described.
- No `FontFamily` set on a WPF element described.
- No hardcoded hex color described.
- No `CreateOrder` with a name not starting `"PTT-"` described. The exit call is
  `acc.Flatten(pos.Instrument)` — not a `CreateOrder` invocation.
- No `DateTime.Now` usage described.
- `Account.Flatten(Instrument)` confirmed as NT8-valid in Plan Section E and Scan 7 —
  AddOn-valid, no `Submit()` required.

**TR7: PASS**

### TR8 — CYC Pre-Check (JS-066 CYC ≤ 8)

| Method | Declared CYC | Limit | Result |
|--------|-------------|-------|--------|
| `Execute()` after fix | 7 | 8 | PASS |
| `ExecuteFollowers()` | 7 | 8 | PASS |
| `NeedsLeaderFallbackFlatten` | 2 | 8 | PASS |

No method described with CYC > 8.

**TR8: PASS**

### TR9 — Test Coverage

`NeedsLeaderFallbackFlatten` is `internal static` — requires `[Fact]` test coverage.
Three `[Fact]` methods specified covering the true path and both false paths:

| Test method | Covers |
|-------------|--------|
| `Test_NeedsLeaderFallbackFlatten_True_WhenBECancelledAndSnapshotEmpty` | True path |
| `Test_NeedsLeaderFallbackFlatten_False_WhenBECancelCountZero` | False — beCancelCount=0 |
| `Test_NeedsLeaderFallbackFlatten_False_WhenSnapshotHasTargets` | False — snapshotCount>0 |

`ExecuteFollowers` is `private void` — not required per the rule (public/internal methods only).
All public and internal methods have `[Fact]` coverage specified.

**TR9: PASS**

### TR10 — Scan Checklist Presence (SCAN-01 through SCAN-07)

All 7 scans present in the ticket:

| Scan | Rule | Present |
|------|------|---------|
| SCAN-01 | JS-021 — `lock()` ban | YES |
| SCAN-02 | JS-033 — `async void` ban | YES |
| SCAN-03 | JS-066 — CYC ≤ 8 | YES |
| SCAN-04 | JS-001 — no `throw` | YES |
| SCAN-05 | JS-002 — no null return | YES |
| SCAN-06 | ASCII-only | YES |
| SCAN-07 | NT8 API | YES |

**TR10: PASS**

### TR11 — xUnit Framework Mandate

Ticket line 149: "Framework: xUnit only. No NUnit. No MSTest." All three test methods use
`[Fact]` attribute. No NUnit `[Test]` or MSTest `[TestMethod]` attributes described.

**TR11: PASS**

### TR12 — File Routing

- Source file: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — Wave workspace ✅
- Test file: `src/PropTraderTools/Tests/B120Tests.cs` — Wave workspace ✅

No path pointing to Director workspace for `.cs` files.

**TR12: PASS**

### TR13 — ASCII-Only (JS-066 / project mandate)

Scan 6 confirms: `"[PTT-QX-FLATTEN] leader fallback flatten: "` and `" qty="` are ASCII-only.
No Unicode, emoji, or curly-quotes in described string literals or identifiers.

**TR13: PASS**

---

## VERDICT: B120-T1

| Check | Result |
|-------|--------|
| TR1 — Traceability | PASS |
| TR2 — Spec Coverage | PASS |
| TR3 — Concurrency (JS-021/023/025) | PASS |
| TR4 — Access modifier consistency (`private void ExecuteFollowers`) | PASS |
| TR5 — Type Safety (JS-001/002/003) | PASS |
| TR6 — Immutability (JS-008/009) | PASS |
| TR7 — NT8 Constraints | PASS |
| TR8 — CYC Pre-Check (JS-066) | PASS |
| TR9 — Test Coverage (`[Fact]` per public/internal method) | PASS |
| TR10 — 7-Scan Checklist Present (SCAN-01..07) | PASS |
| TR11 — xUnit Framework | PASS |
| TR12 — File Routing (Wave workspace) | PASS |
| TR13 — ASCII-Only | PASS |

**B120-T1 VERDICT: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

All 1 ticket(s) pass all 13 checks. The engineer may proceed with B120-T1.

**Cleared for Phase 4a (ptt-engineer).**
