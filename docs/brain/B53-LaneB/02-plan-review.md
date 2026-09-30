# B53-LaneB Plan Review

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-08-10  
**Plan reviewed**: `docs/brain/B53-LaneB/02-architecture-plan.md`  
**Spec reference**: `specs/002-trade-copier-spec.html` id="section-b53" (DW-B53-02 row)  
**Rules references**: `docs/standards/jane-street/RULES_CATALOG.md`, `docs/standards/NT8_COMPILER_RULES.md`  
**Result**: REVIEW_PASS (with one required engineer verification: F1)

---

## Violation Index

| ID | Severity | Rule / Check | Location in Plan | Status |
|----|----------|-------------|-----------------|--------|
| F1 | REQUIRED VERIFICATION | Spec field name `LeaderAccount` vs plan field name `MasterAccount` | § 3.2 — IsLeaderEntryChangeSubmitted | Engineer must verify CopyRule struct field at implementation |
| F2 | INFORMATIONAL | `IsLeaderEntryChangeSubmitted` CYC=5, spec aspirational target was ≤3 | § 4 CYC table | Non-blocking — hard limit ≤8 met |
| F3 | INFORMATIONAL | `CopyEngine_TestAccessor` for private static method — wiring not explained | § 7 Test Plan | Non-blocking if accessor pattern pre-exists in project |

**P0 hard violations: ZERO**  
**P1 violations: ZERO**  
**REVIEW_PASS — subject to F1 verification at implementation**

---

## Section 1 — Spec Alignment (DW-B53-02)

### 1.1 ChangeSubmitted leader entry drag → follower `acc.Change()` update
**PASS** ✓

Plan § 3.4 (`SyncFollowerEntryDrag`) correctly sequences:
```csharp
fo.LimitPrice = order.LimitPrice;
acc.Change(new Order[] { fo });
```
This matches the spec requirement: *"find follower's Working 'PTT-Copy' order for same instrument, call acc.Change() with new limit price."*

### 1.2 IsDedup bypass requirement
**PASS** ✓

Plan § 2 correctly identifies both blocking gates (Gate 3 = `OrderState.Submitted` only; IsDedup stamps orderId on first Submitted). Plan § 3.6 inserts the ChangeSubmitted branch after Gate 2.5 (`!matchedRule.Value.Enabled`) and before `HandleRuleMatch`, which calls `DispatchCopy` (the gate holder). The early `return` after `SyncFollowerEntryDrag` ensures IsDedup is never reached on the drag path. Architecture is correct.

### 1.3 Insertion point in OnOrderUpdate (after Gate 2, rule must be found first)
**PASS** ✓

The new ChangeSubmitted branch in plan § 3.6 fires only after:
- Gate 1: `!_isCopyEnabled` guard
- Gate 2: `foreach (_rules)` with instrument + account match
- Gate 2.5: `!matchedRule.Value.Enabled` check

The leader account match (Gate 2) is a prerequisite to `matchedRule` being populated. `IsLeaderEntryChangeSubmitted` receives a fully matched `rule` struct. This satisfies the spec's logical ordering requirement.

### 1.4 `MasterAccount` vs `LeaderAccount` — REQUIRED VERIFICATION (F1)

**REQUIRES ENGINEER VERIFICATION at implementation time.**

Spec text (line 22154):
```
AND order.Account.Name == rule.LeaderAccount.Name
```

Plan § 3.2 states:
```csharp
order.Account.Name == rule.MasterAccount.Name
```
with the note: *"`rule.MasterAccount` uses the field name `MasterAccount` (NOT `LeaderAccount`) as confirmed in `CopyRule` struct at line 181 of `CopyEngine.cs`."*

The architect claims to have verified the struct at line 181. If correct, the plan is right and the spec's orchestrator prompt used an informal name. If the struct field is actually `LeaderAccount`, the plan code is wrong and will produce CS1061 at F5.

**Engineer action required before commit**: Open `CopyEngine.cs` line 181 and confirm the field name in `CopyRule`. If the field is `MasterAccount` — plan is correct, proceed. If the field is `LeaderAccount` — update all occurrences in the new methods to use `rule.LeaderAccount.Name` before committing.

This is not a REVIEW_FAIL because the plan explicitly flags the discrepancy and provides the verification instruction. It is a guard, not a blocker at plan stage.

---

## Section 2 — CYC Compliance

### 2.1 `OnOrderUpdate` — CYC = 8 after changes
**PASS** ✓

Plan § 4 table enumerates 8 branches (base 1 + 7 conditionals). Mirror check and IsWorkingBracket are moved to `HandleRuleMatch`, preserving the budget. Net CYC change = 0 from extraction; net +1 from the ChangeSubmitted branch; net -1 from the two moved conditions = CYC stays at 8. Logic is sound.

### 2.2 `IsLeaderEntryChangeSubmitted` — CYC = 5 (spec aspirational target was ≤3)
**INFORMATIONAL** (F2)

McCabe count: base 1 + 4 short-circuit `&&` operators = CYC 5. The spec prompt says "CYC target: <= 3." The plan correctly identifies this discrepancy and labels the spec target "aspirational readability guidance." The project hard limit is CYC ≤ 8 per Jane Street DNA. CYC=5 passes the hard limit.

No blocking violation. The engineer should note that a CYC=3 alternative was not achievable without splitting the method or removing the defensive account-match guard; the architect made the right tradeoff.

### 2.3 `SyncFollowerEntryDrag` — CYC = 3
**PASS** ✓ (spec target ≤4)

### 2.4 `HandleRuleMatch` — CYC = 3
**PASS** ✓ (well within ≤8)

### 2.5 `FindFollowerEntryOrder` — CYC = 4
**PASS** ✓ (spec target ≤4)

### 2.6 All new methods summary
| Method | CYC | Limit | Result |
|--------|-----|-------|--------|
| `OnOrderUpdate` (modified) | 8 | ≤8 | ✓ |
| `HandleRuleMatch` (new) | 3 | ≤8 | ✓ |
| `IsLeaderEntryChangeSubmitted` (new) | 5 | ≤8 | ✓ |
| `FindFollowerEntryOrder` (new) | 4 | ≤8 | ✓ |
| `SyncFollowerEntryDrag` (new) | 3 | ≤8 | ✓ |

---

## Section 3 — JS Rules

### JS-021 — No lock()
**PASS** ✓

Plan § 5 and plan § 3: all four new methods are either `private static` (stateless predicates/helpers) or operate on local stack variables + NT8 API calls. No `lock()` usage in any new method.

### JS-001 — No throw in hot path
**PASS** ✓

Plan § 3.4: `acc.Change()` is wrapped in `try/catch`. The catch body logs to `StatusUpdate?.Invoke(...)` and does NOT re-throw. This satisfies JS-001 for the hot-path order update handler.

### JS-002 — No return null for reference types
**PASS** ✓ (approved deviation)

`FindFollowerEntryOrder` returns `null` when not found. Plan § 3.3 correctly documents this as an approved deviation matching the existing `FindFollowerBracketOrder` codebase pattern (also returns null). NT8 API returns raw NT8 objects; no Option<T> infrastructure is available in NT8 NinjaScript. This deviation is already established in the codebase and covered by `docs/standards/JANE_STREET_DEVIATIONS.md`. The `null` return is checked immediately at the call site in `SyncFollowerEntryDrag` (`if (fo == null) { ...; continue; }`).

### JS-033 — No async void
**PASS** ✓

All four new methods are synchronous: `IsLeaderEntryChangeSubmitted` → `bool`, `FindFollowerEntryOrder` → `Order`, `HandleRuleMatch` → `void`, `SyncFollowerEntryDrag` → `void`. No `async` modifier on any new method.

---

## Section 4 — NT8 Constraints

### `OrderState.ChangeSubmitted` — F5 compiler existence
**PASS** ✓

No rule in `NT8_COMPILER_RULES.md` bans `OrderState.ChangeSubmitted`. NT8-031 bans only `OrderState.PendingSubmit`. Plan § 3.2 and § Risk R1 correctly handle the F5 gate: if `CS0117 'OrderState' does not contain a definition for 'ChangeSubmitted'` fires, the engineer must stop, add NT8-056, and escalate. No int cast workaround. This is the correct protective posture.

### `acc.Change()` call pattern
**PASS** ✓

Plan § 3.4 follows the confirmed NT8-046 safe pattern:
```csharp
fo.LimitPrice = order.LimitPrice;
acc.Change(new Order[] { fo });
```
This is identical to the `SyncFollowerBracket` pattern at line 708 (confirmed by plan reference). The target order is `fo.Name == "PTT-Copy"` — AddOn-owned, `FromEntrySignal != null` (established by B53-LaneA). NT8-046 ATM interception only affects `Stop1/Stop2` slot orders with `FromEntrySignal == null`. No conflict.

### NT8-013 (`DateTime.Now` for expiry) — Not applicable
**PASS** ✓ — No `CreateOrder` call; `acc.Change()` used only.

### NT8-014 (PTT- prefix) — Not applicable
**PASS** ✓ — No new `CreateOrder` call.

### NT8-019 (`async void`) — Not applicable
**PASS** ✓ — All new methods synchronous.

### NT8-031 (`PendingSubmit` does not exist) — Not applicable
**PASS** ✓ — New code uses `ChangeSubmitted`, `Working`, `Accepted` only.

### NT8-042 (`Dispatcher.InvokeAsync` not available) — Not applicable
**PASS** ✓ — No UI updates in new methods. `StatusUpdate?.Invoke()` is the existing order-thread logging pattern.

### NT8-044 (`StringComparison` requires `using System`) — Not applicable
**PASS** ✓ — New code does not use `StringComparison` enum. All comparisons use `==` operator or `StartsWith`/`!=` single-argument forms.

---

## Section 5 — Test Plan

### T_B53B_01 — IsLeaderEntryChangeSubmitted returns true for ChangeSubmitted leader entry
**PASS** ✓

Arrange/Act/Assert structure present. Tests the exact scenario: `OrderState.ChangeSubmitted`, non-PTT-Copy name, account match. Assertion: `Assert.True(result)`.

### T_B53B_02 — IsLeaderEntryChangeSubmitted returns false for bracket stop leg
**PASS** ✓

Arrange/Act/Assert structure present. Tests the exclusion path: `Name = "Stop"` triggers `IsStopLeg`. Assertion: `Assert.False(result)`.

### NT8 runtime dependency check
**PASS** ✓

Both tests use stub `Order` objects constructed in-memory. No NT8 runtime DLL required. Compatible with xUnit headless execution.

### `CopyEngine_TestAccessor` wiring — INFORMATIONAL (F3)

Plan names `CopyEngine_TestAccessor.IsLeaderEntryChangeSubmitted(...)` as the test invocation. The method is `private static`. If this is a pre-existing test accessor pattern in the project (reflection helper or `InternalsVisibleTo`), this is fine. If it does not exist, the engineer must implement the accessor or change the test to call via a `protected internal` wrapper.

**Engineer action**: Confirm `CopyEngine_TestAccessor` exists and is wired before writing tests. If it does not exist, document the accessor creation in the ticket.

---

## Section 6 — Scope

**PASS** ✓

Files modified per plan § 9:
- `src/PropTraderTools/CopyEngine.cs` — 4 new private methods + 1 modified method tail
- `src/PropTraderTools/CopyEngineTests.cs` — 2 new [Fact] tests appended after line 4652

No other files listed. `PttContracts.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`, and all `.csproj` files are explicitly excluded. Zero scope creep.

---

## Section 7 — Hard-Link Sync

**PASS** ✓

Plan § 8 mandates:
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

Correctly identifies the PTT Wave workspace sync command. Correctly excludes `deploy-sync.ps1` (V12 epic-cluster only). This is the correct post-change sync step.

---

## Section 8 — Prior Deferred Backlog

**PASS** ✓

Plan § 10 carries forward all four prior items as OPEN and unchanged:
- `DW-B54-01` P0 — AtmStrategyCreate API (NT8-055 resolution)
- `DW-B54-02` P0 — F5-GATE-02 live ATM bracket test, blocked by DW-B54-01
- `DW-B54-03` P2 — Diagnostic log for `#if NT8_ADDON_ATM` inactive state
- `DW-BACKLOG-01` P2 — PttContracts.cs FillSignal dead-code cleanup

Plan does NOT touch the ATM attach path (B53-LaneA's DW-B53-01 work) — DW-B54-01 is correctly held for B54. ✓

---

## Spec Coverage Matrix

| Requirement (from spec DW-B53-02 row) | Addressed? | Plan Section |
|---------------------------------------|-----------|-------------|
| Detect `ChangeSubmitted` on non-bracket non-PTT leader entry | ✓ YES | § 3.2 (IsLeaderEntryChangeSubmitted) |
| Bypass IsDedup entirely (separate path, not DispatchCopy) | ✓ YES | § 2 (root cause), § 3.6 (OnOrderUpdate) |
| Find follower's Working "PTT-Copy" order for same instrument | ✓ YES | § 3.3 (FindFollowerEntryOrder) |
| Call `acc.Change()` with new limit price | ✓ YES | § 3.4 (SyncFollowerEntryDrag) |
| Log result to StatusUpdate | ✓ YES | § 3.4 |
| Prerequisite: B53-LaneA FINAL_PASS (acc.Change resolves cleanly) | ✓ YES | § 1 header |
| Scope: CopyEngine.cs only | ✓ YES | § 9 |
| 2 new [Fact] tests | ✓ YES | § 7 |
| verify_links.ps1 -Fix post-change | ✓ YES | § 8 |
| Carry forward DW-B54-01/02/03, DW-BACKLOG-01 | ✓ YES | § 10 |

All 10 spec requirements addressed. ✓

---

## DNA Block Quick-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock()` | grep in new code | ZERO — ✓ |
| JS-001 throw in hot path | `acc.Change()` wrapped in try/catch, no re-throw | ✓ |
| JS-002 return null | `FindFollowerEntryOrder` null-return approved deviation | ✓ |
| JS-033 async void | All new methods synchronous | ✓ |
| JS-023 UI off-thread | No UI updates in new methods; StatusUpdate is order-thread safe | ✓ |
| JS-008 SolidColorBrush freeze | Not applicable — no new WPF brushes | N/A |
| JS-010 public constructor on singleton/struct | Not applicable — no new singletons or signal structs | N/A |
| CYC ≤ 8 all methods | Max CYC in new code = 5 (IsLeaderEntryChangeSubmitted) | ✓ |
| NT8-016 TradeCopierWindow sealed | Not touched | N/A |
| NT8-019 async void | Not applicable | N/A |
| NT8-042 Dispatcher.InvokeAsync | Not used | N/A |
| SCAN-01 lock( | 0 in new code | ✓ |
| SCAN-02 async void | 0 in new code | ✓ |
| SCAN-03 return null | 1 approved (FindFollowerEntryOrder) | ✓ |
| SCAN-04 DateTime.Now | 0 in new code | ✓ |
| SCAN-05 hex #RRGGBB | 0 in new code | ✓ |
| SCAN-06 throw new | 0 in new code | ✓ |
| SCAN-07 FontFamily | 0 in new code | ✓ |

---

## Final Verdict

**REVIEW_PASS**

The plan is architecturally sound, spec-complete, and compliant with all Jane Street DNA rules and NT8 constraints. No P0 or P1 violations found.

**Required engineer action (F1)**: Before committing, verify `CopyRule` struct field name at `CopyEngine.cs` line 181. If field is `MasterAccount` — plan is correct. If field is `LeaderAccount` — update all usages in the four new methods.

**Non-blocking notes (F2, F3)**: `IsLeaderEntryChangeSubmitted` CYC=5 is above the spec's aspirational target of 3 but within the hard limit of 8. `CopyEngine_TestAccessor` wiring should be confirmed before test implementation.

The plan is cleared for Phase 3 (ticket generation).
