# B184 Ticket Review
## ptt-ticket-reviewer Phase 3.5

**Date**: 2026-07-07
**Author**: ptt-ticket-reviewer
**Epic**: B184 — Restore CopyEngine.cs BE Event Infrastructure + Full ATR Removal
**Tickets under review**: `docs/brain/B184/04-tickets.md`
**Plan**: `docs/brain/B184/02-architecture-plan.md` — REVIEW_PASS (cycle 2)
**Spec**: `docs/brain/B184/00-orchestrator-prompt.md`
**Rules**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## OVERALL VERDICT

**TICKET_REVIEW_PASS**

Both tickets pass all mandatory checks. No violations found. The engineer may proceed.

---

## T1 — B184-T1: Restore CopyEngine.cs BE Event Infrastructure

### Traceability: PASS

All 7 spec Acceptance Criteria (from `00-orchestrator-prompt.md` T1 section) map to the ticket:

| Spec AC | Ticket Coverage | Location |
|---------|----------------|----------|
| AC1: `dotnet build` 0 errors | ✓ AC item 1, Step 10 | T1 §6, §5.Step10 |
| AC2: `BeState` enum present | ✓ AC item 2, Step 12 | T1 §6, §5.Step12 |
| AC3: `PendingBeArmed` + `GlobalBeAllDisarmed` present | ✓ AC items 3-4 | T1 §6 |
| AC4: `DisarmPendingBe`, `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` bodies present | ✓ AC items 5-7 | T1 §6 |
| AC5: B180-2 removals not re-introduced | ✓ AC item 8, Step 8 | T1 §6, §5.Step8 |
| AC6: `verify_critical_symbols.ps1` exits 0 | ✓ AC item 9, Step 9 | T1 §6, §5.Step9 |
| AC7: `ptt-sync.ps1` runs clean | ✓ AC item 10, Step 11 | T1 §6, §5.Step11 |

All 6 T1 symbols listed with exact method signatures and structural placement instructions. ✓

Deferred work items DW-B183-06, DW-B183-07, DW-B183-08, DW-B183-09 all cited and mapped. ✓

Phantom work: None detected — all ticket items trace to spec or plan. ✓
Missing work: None detected — all plan T1 components are present in the ticket. ✓

### JS Pre-Check: PASS

No bad patterns described in the ticket:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock() ban) | Step 5 and SCAN-01 explicitly gate on lock() — STOP escalation if found | PASS |
| JS-001 (no throw in hot path) | Section 2 note on DisarmPendingBe: "Must null-guard, NOT throw (JS-001)"; SCAN-06 enforces | PASS |
| JS-002 (no return null) | SCAN-02 present; methods return void or bool — structurally N/A, correctly noted | PASS |
| JS-033 (no async void) | SCAN-03 present; restored methods confirmed non-async | PASS |
| JS-008/009 (immutability) | No mutable struct or SolidColorBrush patterns described | PASS |
| Concurrent/Immutable for shared state | Section 2 correctly notes IsPendingSlotsEmpty must use ConcurrentDictionary.IsEmpty | PASS |

### CYC Pre-Check: PASS

All restored method CYC estimates are within budget:

| Method | Estimated CYC | Budget | Result |
|--------|--------------|--------|--------|
| `DisarmPendingBe` | ~2–4 | ≤ 8 | PASS |
| `IsPendingSlotsEmpty` | ~1 | ≤ 8 | PASS |
| `RaiseBeAllDisarmed` | ~1 | ≤ 8 | PASS |

SCAN-04 requires measurement of all 3 restored methods. ✓

### NT8 Check: PASS

| Constraint | Check | Result |
|-----------|-------|--------|
| No async/await in lifecycle methods | Restored methods are non-async (SCAN-03 enforces) | PASS |
| No Account.All outside Loaded handler | Not applicable to T1 restoration scope | PASS |
| No sealed on TradeCopierWindow | Not applicable | PASS |
| No FontFamily override | Not applicable | PASS |
| No hardcoded hex color | Not applicable | PASS |
| No CreateOrder without "PTT-" | Not applicable | PASS |
| No DateTime.Now | Not applicable | PASS |
| lock() in restored methods → STOP | Step 5 + SCAN-01 enforce escalation before commit | PASS |

### Test Coverage: PASS

6 concrete [Fact] test names specified with meaningful assertions:

| Test Name | Assertion |
|-----------|-----------|
| `BeState_Enum_HasIdle_Armed_Connected_Values` | Enum values correct |
| `PendingBeArmed_Event_IsActionStringString_Delegate` | Event type correct |
| `GlobalBeAllDisarmed_Event_IsAction_Delegate` | Event type correct |
| `IsPendingSlotsEmpty_ReturnsTrue_WhenNoPendingSlots` | Returns true on fresh instance |
| `DisarmPendingBe_NullLeader_DoesNotThrow` | Null-guard honoured (JS-001) |
| `RaiseBeAllDisarmed_FiresGlobalBeAllDisarmed_Event` | Event fires on invocation |

Deferred implementation rationale ("no test project at time of B184") is clearly stated, with
instruction to record as "Planned — no test project" in ticket-1-completion.md. ✓

### Scan Checklist: PASS

All 7 scans present with ticket-specific grep commands:

| Scan | Description | Command Present | Required Result |
|------|-------------|----------------|----------------|
| SCAN-01 | lock() ban (JS-021) | `git diff HEAD ... Select-String "lock\("` | 0 results |
| SCAN-02 | return null ban (JS-002) | `git diff HEAD ... Select-String "return null"` | 0 results |
| SCAN-03 | async void ban (JS-033) | `git diff HEAD ... Select-String "async void"` | 0 results |
| SCAN-04 | CYC <= 8 (JS-066) | Measure all 3 restored methods | All <= 8 |
| SCAN-05 | ASCII-only (JS-081) | `git diff HEAD ... Select-String "[^\x00-\x7F]"` | 0 non-ASCII |
| SCAN-06 | throw ban in hot paths (JS-001) | `git diff HEAD ... Select-String "throw new"` | 0 results |
| SCAN-07 | Single-Hunk Ban (protocol) | `(git diff HEAD ... Select-String "^@@").Count` | != 1 |

All 7 scans present. Each has a ticket-specific PowerShell command and a concrete required result. ✓

### File Routing: PASS

T1 scope: `src/PropTraderTools/CopyEngine.cs` only — correct Wave workspace path. ✓
No Director workspace paths for .cs files. ✓

### Hard Constraints Check: PASS

| Constraint | Location | Result |
|-----------|----------|--------|
| All 5 B180-2 approved removals named as excluded | §7 Hard Constraints table + AC8 + Step 8 | PASS |
| Commit Size Gate (>300 lines = Director approval) | §5.Step6 (MANDATORY pre-commit) + AC12 | PASS |
| `git show f01b5155:` as fix source explicitly stated | §2 header note, Step 1 | PASS |
| BeState duplicate check before inserting | Step 2 | PASS |
| Single-Hunk Ban in AC | AC11 | PASS |

### Scope Isolation: PASS

"SCOPE: This ticket modifies ONLY `src/PropTraderTools/CopyEngine.cs`. Do not touch any other file." — stated verbatim in SCOPE LOCK section. ✓

### Engineer Instructions: PASS

13 numbered, actionable steps with concrete PowerShell commands and expected outputs. No vague language. ✓

---

### T1 VERDICT: TICKET_REVIEW_PASS

---

---

## T2 — B184-T2: Full Removal of ATR Position Sizing Tool

### Traceability: PASS

All 7 spec Acceptance Criteria (from `00-orchestrator-prompt.md` T2 section) map to the ticket:

| Spec AC | Ticket Coverage | Location |
|---------|----------------|----------|
| AC1: `dotnet build` 0 errors | ✓ AC item 1, Step 10 | T2 §6, §5.Step10 |
| AC2: No ATR references anywhere in solution | ✓ AC item 2, Steps 11–12 | T2 §6, §5.Step11 |
| AC3: FeatureFlags arity matches all 4 factory sites | ✓ AC item 5, Step 4 | T2 §6, §5.Step4 |
| AC4: No `BuildRiskAtrRow` call in `BuildUI()` | ✓ AC item 6 | T2 §6 |
| AC5: `verify_critical_symbols.ps1` exits 0 | ✓ AC item 7, Step 13 | T2 §6, §5.Step13 |
| AC6: `ptt-sync.ps1` runs clean | ✓ AC item 8, Step 14 | T2 §6, §5.Step14 |
| AC7: AtrSizingEngine.cs and AtrCalc.cs absent | ✓ AC items 3–4, Steps 9 + 12 | T2 §6, §5.Steps9,12 |

All T2 removal targets per file fully listed in Section 2:
- FeatureFlags.cs: record arity 7→6, all 4 factory sites with exact before/after ✓
- CopyEngine.cs: 2 fields + 5 methods + 1 call-site patch ✓
- TradeCopierPanel.cs: 6 fields + 10 methods + 4 call sites ✓
- TradeCopierAddOn.cs: 2 fields + 4 methods + 2 call sites ✓
- AtrSizingEngine.cs: DELETE ✓
- AtrCalc.cs: DELETE ✓

DW-B183-10 partial lift noted correctly. ✓

Phantom work: None detected. ✓
Missing work: None detected — all plan T2 components present. ✓

### JS Pre-Check: PASS

T2 is removal-only; no new code patterns introduced:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock() ban) | SCAN-01 verifies no lock() in added lines; T2 adds no new code | PASS |
| JS-001 (throw in hot path) | SCAN-06 verifies no `throw new` in added lines | PASS |
| JS-002 (return null) | SCAN-02 verifies no `return null` in added lines; call-site patch uses `signalQty` variable | PASS |
| JS-033 (async void) | SCAN-03 verifies no async void introduced | PASS |
| FeatureFlags as `sealed record` | Record type preserved — only AtrSizing param removed, no type change | PASS |
| ConcurrentDictionary (_atrEngines) | Correctly removed as part of ATR subsystem | PASS |

Note on T2 SCAN-02 rule citation: ticket cites JS-002 for SCAN-02 ("return null ban"). The plan
review (02-plan-review.md OBS-2) flagged this as a minor mis-citation (JS-002 = Option<T>, not
record arity). The ticket SCAN-02 is correctly scoped to the call-site patch (`signalQty` vs null),
not record arity. The scan content is correct. This was a known non-blocking observation from
the plan review; no new violation here.

### CYC Pre-Check: PASS

| Method | Impact | Result |
|--------|--------|--------|
| `DispatchCopy` | ResolveBaseQty removal reduces branch count; CYC can only decrease | PASS |

SCAN-04 requires explicit measurement of DispatchCopy after edit. ✓

### NT8 Check: PASS

| Constraint | Check | Result |
|-----------|-------|--------|
| No async/await in lifecycle methods | T2 adds no async code | PASS |
| No sealed on TradeCopierWindow | Not applicable | PASS |
| No FontFamily / hex color | Not applicable | PASS |
| ptt-sync.ps1 may reference deleted files | Step 14 explicitly warns "verify script does not error on their absence" | PASS |
| IsExternalInit shim preserved | §2 FeatureFlags section: "PRESERVE: IsExternalInit shim namespace block (top of file) — required for NT8 Roslyn record support. Do NOT touch lines 1-5." + Step 4 post-check | PASS |

### Test Coverage: PASS

8 concrete [Fact] test names specified:

| Test Name | Assertion |
|-----------|-----------|
| `FeatureFlags_Starter_HasSixParameters_NoAtrSizing` | Starter() returns correct 6-param instance |
| `FeatureFlags_Elite_HasSixParameters_NoAtrSizing` | Elite() returns correct 6-param instance |
| `FeatureFlags_Pro_HasSixParameters_NoAtrSizing` | Pro() returns correct 6-param instance |
| `FeatureFlags_Record_DoesNotHaveAtrSizingProperty` | AtrSizing property absent at compile time |
| `CopyEngine_DoesNotHaveAtrEngine_Field` | _atrEngine and _atrEnabled absent |
| `CopyEngine_DoesNotHave_SetAtrEngine_Method` | SetAtrEngine removed |
| `CopyEngine_DoesNotHave_GetSuggestedQty_Method` | GetSuggestedQty removed |
| `CopyEngine_DoesNotHave_ResolveBaseQty_Method` | ResolveBaseQty removed |

Deferred implementation rationale stated; record in ticket-2-completion.md instruction present. ✓

### Scan Checklist: PASS

All 7 scans present with ticket-specific commands:

| Scan | Description | Command Present | Required Result |
|------|-------------|----------------|----------------|
| SCAN-01 | lock() ban (JS-021) | `git diff HEAD -- src/ ...  Select-String "lock\("` | 0 results |
| SCAN-02 | return null ban (JS-002) | `git diff HEAD -- src/ ... Select-String "return null"` | 0 results |
| SCAN-03 | async void ban (JS-033) | `git diff HEAD -- src/ ... Select-String "async void"` | 0 results |
| SCAN-04 | CYC <= 8 (JS-066) | Measure DispatchCopy CYC after removal | <= 8 (expected <= 7) |
| SCAN-05 | ASCII-only (JS-081) | `git diff HEAD -- src/ ... Select-String "[^\x00-\x7F]"` | 0 non-ASCII |
| SCAN-06 | throw ban in hot paths (JS-001) | `git diff HEAD -- src/ ... Select-String "throw new"` | 0 results |
| SCAN-07 | Single-Hunk Ban (protocol) | `(git diff HEAD -- CopyEngine.cs ... Select-String "^@@").Count` | != 1, expected >= 4 |

All 7 scans present. Each has a ticket-specific PowerShell command and required result. ✓

SCAN-07 (Single-Hunk Ban) is present in BOTH T1 and T2. ✓

### File Routing: PASS

All C# paths under `src/PropTraderTools/` — correct Wave workspace. ✓
No Director workspace paths for .cs files. ✓

### Hard Constraints Check: PASS

| Constraint | Location | Result |
|-----------|----------|--------|
| GetSuggestedQty exclusive-caller verification step | Step 1 — mandatory pre-check | PASS |
| FeatureFlags arity table (before 7, after 6, all 4 factory sites) | §2 FeatureFlags section, complete before/after table | PASS |
| IsExternalInit shim preservation | §2 FeatureFlags + Step 4 post-check | PASS |
| AtrCalc external caller verification | Step 3 — mandatory pre-check | PASS |
| Single-Hunk Ban on CopyEngine.cs | Step 6 + AC9 | PASS |
| BE event infrastructure (T1 symbols) not removed | Scope Lock note + Step 7 post-check | PASS |
| ptt-sync.ps1 validated for missing-file tolerance | Step 14 explicit warning | PASS |

### Scope Isolation: PASS

"SCOPE: This ticket modifies ONLY the following files." with explicit per-file list (4 edits + 2 deletes). ✓
"Do NOT modify any BE event infrastructure symbols" stated in Scope Lock. ✓

### T2 Prerequisite Gate: PASS

Header: "Prerequisite: B184-T1 VERIFY_PASS must be confirmed before starting T2 work." ✓
Pre-Edit step: "Confirm T1 VERIFY_PASS before starting. Build must be green." ✓

### Engineer Instructions: PASS

15 numbered, actionable steps. Pre-edit prerequisite check listed before Step 1. Steps include
mandatory pre-checks (Steps 1–3) before any edit begins, atomic factory site update instruction
(Step 4), and explicit verification after each major edit. ✓

---

### T2 VERDICT: TICKET_REVIEW_PASS

---

---

## Summary Verdict Table

| Check | T1 | T2 |
|-------|----|----|
| Traceability (spec → ticket) | PASS | PASS |
| All plan items covered | PASS | PASS |
| JS Pre-Check (JS-021/001/002/033/008/009) | PASS | PASS |
| CYC Pre-Check (JS-066, CYC <= 8) | PASS | PASS |
| NT8 Hard Constraints | PASS | PASS |
| Test Coverage ([Fact] names present) | PASS | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS | PASS |
| SCAN-07 (Single-Hunk Ban) present | PASS | PASS |
| File Routing (Wave workspace paths) | PASS | PASS |
| Scope Isolation | PASS | PASS |
| Hard Constraints (B180-2, Commit Size Gate, etc.) | PASS | PASS |
| Engineer Instructions (numbered, actionable) | PASS | PASS |
| ptt-sync.ps1 step present | PASS | PASS |
| verify_critical_symbols.ps1 step present | PASS | PASS |
| Prerequisite gate (T2 waits for T1 VERIFY_PASS) | N/A | PASS |
| `git show f01b5155:` fix source stated | PASS | N/A |

---

## Observations (Non-Blocking)

| # | Observation | Location | Blocking? |
|---|-------------|---------|-----------|
| OBS-1 | T1 scan numbering differs from the plan's "7-Scan Checklist Preview" (plan SCAN-02=throw, ticket SCAN-02=return_null). The re-mapping is internally consistent within the ticket and all 7 scan bodies are functionally correct. Non-blocking. | T1 §7-Scan vs plan §7-Scan Preview | No |
| OBS-2 | T2 SCAN-02 cites JS-002 ("return null ban") for the call-site patch verification. JS-002 is "Option<T> instead of null." This was noted as OBS-2 in 02-plan-review.md and confirmed non-blocking. The scan content (verify `signalQty` not null) is correct. | T2 §7-Scan, SCAN-02 | No |
| OBS-3 | T2 SCAN-07 scopes the Single-Hunk Ban only to CopyEngine.cs, not the other modified files. This is correct — the Single-Hunk Ban protocol applies specifically to CopyEngine.cs per project mandate. Other files have no hunk-count requirement. | T2 §7-Scan, SCAN-07 | No |

---

## FINAL VERDICT

**TICKET_REVIEW_PASS**

Both tickets contain the full 7-scan checklist, complete spec traceability, exact method signatures,
concrete [Fact] test names, numbered engineer instructions, all hard constraint checks, correct file
routing, and proper scope isolation. No JS-XXX violations, NT8 violations, or CYC > 8 risks are
described by either ticket.

The engineer may proceed to Phase 4a.

**Execution order reminder**: T1 VERIFY_PASS is a hard prerequisite for T2. Both tickets
must reach VERIFY_PASS before Ph5 may run.

---

*Written by: ptt-ticket-reviewer (Phase 3.5) — 2026-07-07*
*Epic: B184*
*Tickets reviewed: `docs/brain/B184/04-tickets.md`*
*Plan basis: `docs/brain/B184/02-architecture-plan.md` — REVIEW_PASS (cycle 2)*
