# Ticket Review: DW-LB-GR-01 BE Retry Logic Bug Fix

**Block**: DW-LB-GR-01
**Phase**: 3.5 -- Ticket Review (cycle 2)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-09-07
**Input**: docs/brain/DW-LB-GR-01/04-tickets.md (revised, cycle 2)
**Prior Review**: cycle 1 -- TICKET_REVIEW_FAIL (missing SCAN-7)

---

## Cycle 2 Focus

| Violation ID | Description | Status |
|--------------|-------------|--------|
| V-01 | SCAN-7 (`ptt-sync-and-verify.ps1` -> 0 MISMATCH) absent from scan checklist | **FIXED** |
| V-02 | Ticket had fewer than 7 scans (SCAN-1 through SCAN-7) | **FIXED** |

---

## T1 -- Fix RegisterBeRetrySlotIfNeeded Guard Condition

### Traceability

| Ticket Item | Maps To | Status |
|-------------|---------|--------|
| L6118 `leaderCount == 0` -> `targetsCount == 0` | DW-LB-GR-01 spec requirement; JS-100 Sentinel finding (PR #47) | PASS |
| Architecture Lock table (method signature, caller site 1, caller site 2, L6139, etc.) | docs/brain/DW-LB-GR-01/02-architecture-plan.md architecture locks | PASS |
| JS-021/001/002/033 constraints | RULES_CATALOG.md P0 rules; plan section "Jane Street Constraints" | PASS |
| L6104 comment update (secondary) | Architecture plan secondary change note | PASS |

No phantom work. No plan item missing from ticket.

**Traceability: PASS**

---

### JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (P0) | No `lock()` in method bodies. `_pendingFollowerBeSlots` is `ConcurrentDictionary`. Fix introduces no lock. | PASS |
| JS-001 (P0) | No `throw` in hot paths. Fix introduces no throw. | PASS |
| JS-002 (P0) | No `return null`. Method is `void`. | PASS |
| JS-003/015 | No empty-string or missing-key sentinel for mode/state. Not applicable -- method uses int/bool params. | PASS |
| JS-008/009 | No mutable struct fields. No `SolidColorBrush` without `.Freeze()`. Not in scope of this fix. | PASS |
| JS-033 (P0) | No `async void` in method bodies. Fix introduces no async construct. | PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check

| Method | Documented CYC | Change Impact | Status |
|--------|---------------|---------------|--------|
| `RegisterBeRetrySlotIfNeeded` | CYC = 6 (L6104 annotation; confirmed by SCAN-1 required result) | 1-token rename (`leaderCount` -> `targetsCount`); zero new branches. CYC remains 6. | PASS |
| Optional test seam forwarder (Option B) | CYC = 1 (pure delegation, no logic) | Engineer required to confirm CYC=1 in SCAN-1 if Option B chosen. | PASS |

No method with estimated CYC > 8.

**CYC Pre-Check: PASS**

---

### NT8 Check

| Constraint | Check | Result |
|-----------|-------|--------|
| No `async`/`await` in lifecycle method | Fix is a synchronous 1-token rename | PASS |
| No `Account.All` outside Loaded handler | Not described | PASS |
| No `sealed` on `TradeCopierWindow` | Not applicable to this ticket | PASS |
| No `FontFamily` set on WPF element | Not applicable to this ticket | PASS |
| No hardcoded hex color | Not applicable to this ticket | PASS |
| No `CreateOrder` with name not starting "PTT-" | Not applicable to this ticket | PASS |
| No `DateTime.Now` usage | Not described | PASS |

**NT8 Check: PASS**

---

### Test Coverage

| Method | Test [Fact] | Status |
|--------|-------------|--------|
| `RegisterBeRetrySlotIfNeeded` (bug scenario: targetsCount>0, leaderCount=0) | `RegisterBeRetrySlotIfNeeded_LeaderZeroTargetsNonZero_DoesNotArmRetry` | PASS |
| `RegisterBeRetrySlotIfNeeded` (correct arm: targetsCount=0, leaderCount>0) | `RegisterBeRetrySlotIfNeeded_TargetsZeroLeaderNonZero_ArmsRetry` | PASS |
| `RegisterBeRetrySlotIfNeeded` (partial-targets path) | `RegisterBeRetrySlotIfNeeded_PartialTargets_ArmsRetry` | PASS |

Test seam approach documented (Option A: reflection; Option B: `internal` forwarder with `InternalsVisibleTo` already declared at L46). All method paths under test. No public or internal method missing a `[Fact]`.

**Test Coverage: PASS**

---

### Scan Checklist (cycle 2 primary fix check)

| Scan | Present? | Command | Required Result |
|------|---------|---------|-----------------|
| SCAN-1 | YES | `lizard src/PropTraderTools/CopyEngine.cs --CCN 8` | 0 warnings; CYC=6 for `RegisterBeRetrySlotIfNeeded` |
| SCAN-2 | YES | `Select-String -Pattern "lock\s*(" src/PropTraderTools/CopyEngine.cs` | 0 results in method bodies (JS-021) |
| SCAN-3 | YES | `Select-String -Pattern "async\s*void" src/PropTraderTools/CopyEngine.cs` | 0 results (JS-033) |
| SCAN-4 | YES | ASCII-only bytes check | 0 bytes > 127 in changed lines (JS-004) |
| SCAN-5 | YES | `dotnet build` | 0 errors |
| SCAN-6 | YES | `dotnet test` | All prior tests pass + 3 new [Fact] tests pass |
| SCAN-7 | YES | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH lines |

All 7 scans present. V-01 and V-02 from cycle 1 are resolved.

**Scan Checklist: PASS** (all 7 scans SCAN-1 through SCAN-7 present)

---

### File Routing

| Artifact | Path | Workspace |
|---------|------|-----------|
| C# source | `src/PropTraderTools/CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\` (Wave workspace) |
| Test file | `tests/PropTraderTools.Tests/RegisterBeRetrySlotIfNeededTests.cs` | Wave workspace |

No Director workspace path for `.cs` files.

**File Routing: PASS**

---

### Spec Coverage

Single-requirement epic (DW-LB-GR-01: wrong guard variable in `RegisterBeRetrySlotIfNeeded`).
Covered exactly once by T1. No duplicate coverage. No uncovered requirement.

**Spec Coverage: PASS**

---

### VERDICT: TICKET_REVIEW_PASS

All cycle-1 passing checks confirmed passing. Cycle-1 violation (missing SCAN-7) is fixed. All 7 scans present. No new violations introduced.

---

## Overall: TICKET_REVIEW_PASS

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | Verdict |
|--------|-------------|--------------|---------------|-----------|---------------|----------------|--------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

**The engineer is cleared to proceed with ticket T1 implementation.**
