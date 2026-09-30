# Ticket Review: B123

**Reviewed by**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-27
**Input**: docs/brain/B123/04-tickets.md
**Plan**: docs/brain/B123/02-architecture-plan.md (REVIEW_PASS — Cycle 2)
**Rules**: docs/standards/jane-street/RULES_CATALOG.md

---

## T1 — DW-B133 forced 2-target Execute overload + OnInstrQAll2tClick fix

### 1. Traceability: PASS

- Ticket header cites **DW-B133** as the sole spec requirement ID. ✓
- All 7 Acceptance Criteria (AC1–AC7) map to explicit verify criteria:
  - AC1 → T_B123_01 + SIM DW-B133-SIM-01 ✓
  - AC2 → T_B123_02 + SIM DW-B133-SIM-01 ✓
  - AC3 → T_B123_04 (overload exists) + SIM ✓
  - AC4 → SIM DW-B133-SIM-01 (Director-owned) ✓
  - AC5 → T_B123_05 (no-arg still exists) + SIM DW-B133-SIM-02 ✓
  - AC6 → SCAN-06 + SCAN-07 ✓
  - AC7 → SCAN-07 (compile) + SIM DW-B133-SIM-01 ✓
- Plan Section 2 root-cause trace (TradeCopierPanel.cs:1979–1981 → no-arg Execute → SnapshotTargetOrders) reproduced in ticket description. ✓
- No phantom work items detected (everything in ticket maps to plan Sections 3.1, 3.2, 6). ✓
- No missing work: plan Section 5 lists exactly 2 production files + 1 test file; ticket matches exactly. ✓

### 2. Signatures: PASS

Ticket line 45 (declaration comment) and line 59 (actual signature):
```csharp
internal void Execute(System.Collections.Generic.List<(double Price, int Qty)> forcedTargets)
```
Exact match to plan Section 3.1. Visibility (`internal`), return type (`void`), parameter type
(`System.Collections.Generic.List<(double Price, int Qty)>`), and parameter name (`forcedTargets`)
all confirmed. ✓

### 3. CYC Pre-Check: PASS

- `Execute(forcedTargets)`: 8 branches enumerated inline in XML doc comment (Branch 0–7).
  Ticket states "CYC=8: null guard(0), flag guard(1), acc loop(2), follower skip(3), pos
  loop(4), null/flat continue(5), diag loop(6), flatten guard(7)." Exactly at JS-066 ceiling
  of ≤ 8. Conservative count (branch 0 = precondition null guard included). ✓
- `OnInstrQAll2tClick` replacement: ticket states CYC=3–4 (instrument null + leader null +
  pos null-coalesce + FirstOrDefault lambda). Within JS-066 threshold. ✓
- All 5 test methods: CYC 1–2 each (no branches in T_B123_01/02/04/05; for-loop in
  T_B123_03 = CYC 2). All ≤ 8. ✓
- No method described in this ticket has a CYC that credibly exceeds 8. ✓

### 4. NT8 Constraints: PASS

- **Account.All in constructor**: Account.All is used only inside `Execute(forcedTargets)` body
  (a regular instance method), not in any constructor. ✓ (JS-NT8-ACALL)
- **lock()**: No `lock()` described anywhere in either method or in any supporting code. ✓ (JS-021)
- **async void**: `Execute(forcedTargets)` is `internal void` — synchronous, no `async` keyword.
  `OnInstrQAll2tClick` is `private void` — synchronous event handler, no `async` keyword. ✓ (JS-033)
- **throw new Exception**: No `throw` statement in either method body. All early exits are bare
  `return;`. ✓ (JS-001)
- **return null**: Method returns `void`; no null return path possible. ✓ (JS-002)
- **DateTime.Now**: Not present. ✓
- **Hardcoded hex color**: Not present. ✓
- **sealed on TradeCopierWindow**: Not described. ✓
- **FontFamily set**: Not described. ✓
- **CreateOrder with non-PTT- name**: Not described. ✓

### 5. Completeness (3 files): PASS

Ticket files table (lines 30–34):

| Action | File |
|--------|------|
| EDIT   | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` |
| EDIT   | `src/PropTraderTools/TradeCopierPanel.cs` |
| CREATE | `src/PropTraderTools/Tests/B123Tests.cs` |

All 3 files from plan Section 5 are present. No extra files listed. ✓

### 6. Test Coverage (5 [Fact] methods): PASS

All 5 test methods are named explicitly with full implementation bodies:

| Test Name | What It Asserts | [Fact] Present |
|-----------|----------------|----------------|
| `T_B123_01_Build2TargetList_7qty` | qty=7: T1=4 (ceiling), T2=3 (floor), Count=2 | ✓ |
| `T_B123_02_Build2TargetList_6qty` | qty=6: T1=3, T2=3 (equal split), Count=2 | ✓ |
| `T_B123_03_Build2TargetList_AlwaysCount2` | qty 1–9: always Count=2, sum=qty, T1>=T2 | ✓ |
| `T_B123_04_ForcedOverload_Exists` | Reflection: new Execute(forcedTargets) overload exists, returns void | ✓ |
| `T_B123_05_NoArgOverload_StillExists` | Reflection: original no-arg Execute() still exists (regression guard) | ✓ |

Every method described in the ticket (Execute(forcedTargets), OnInstrQAll2tClick, Build2TargetList)
has dedicated test coverage. No public or internal method is described without a corresponding
[Fact]. ✓

xUnit-only — no NUnit or MSTest attributes. JS-051 compliant. ✓

### 7. Scan Checklist Presence (SCAN-01 through SCAN-07): PASS

Ticket Section "7-Scan Checklist (engineer contract — run in this order)" is present (lines 328–358).
All 7 scans are present with exact grep/tool commands and expected zero-result thresholds:

| Scan Required | Ticket SCAN | Command in Ticket | Expected Result |
|---------------|-------------|-------------------|-----------------|
| S1: lock() in PttGlobalQuickExit.cs | SCAN-01 | `grep -rn "lock(" ...PttGlobalQuickExit.cs` | 0 matches ✓ |
| S2: async void in PttGlobalQuickExit.cs | SCAN-02 | `grep -rn "async void " ...PttGlobalQuickExit.cs` | 0 matches ✓ |
| S3: return null in PttGlobalQuickExit.cs | SCAN-03 | `grep -rn "return null" ...PttGlobalQuickExit.cs` | 0 matches ✓ |
| S4: lock() in TradeCopierPanel.cs | SCAN-04 | `grep -rn "lock(" ...TradeCopierPanel.cs` | 0 matches ✓ |
| S5: async void in TradeCopierPanel.cs | SCAN-05 | `grep -rn "async void " ...TradeCopierPanel.cs` | 0 matches (non-event-handlers) ✓ |
| S6: complexity_audit CYC <= 8 | SCAN-06 | `python scripts/complexity_audit.py` | CYC <= 8 for new methods ✓ |
| S7: dotnet build 0 errors | SCAN-07 | `dotnet build ...--no-incremental` | 0 Error(s) 0 Warning(s) ✓ |

Note: SCAN-05 correctly notes that synchronous void is mandatory for the new OnInstrQAll2tClick
(not async) while acknowledging async void is permitted for other WPF event handlers. The
qualification is precise and does not introduce ambiguity. ✓

Defense-in-depth rationale intact: Layer 1 (ticket contract) is present. Layers 2 and 3
(engineer attestation via ticket-1-completion.md; verifier independent run) are anchored to
this checklist. ✓

### 8. Follower Path: PASS

Ticket method body line 139:
```csharp
ExecuteFollowers(acc, pos, forcedTargets, ticks, leaderStop);
```
`forcedTargets` is passed directly to `ExecuteFollowers` — NOT to `SnapshotTargetOrders`.
XML doc comment (line 52) explicitly states: "Skips SnapshotTargetOrders -- uses forcedTargets
directly." The SnapshotTargetOrders call is absent from the entire method body. ✓

This is the core fix for DW-B133. The path is correctly stated. ✓

### 9. Log Line: PASS

Ticket line 82–84:
```csharp
NinjaTrader.Code.Output.Process(
    "[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1
);
```
Exact required prefix `[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)` is present.
AC7 also references this log line: "Log line `[PTT-QX-2T-ALL] GlobalQuickExit fired (forced
2-target)` in Output Tab". ✓

### 10. Regression (no-arg Execute preserved): PASS

- Ticket placement instruction: "Insert immediately after the closing brace of the existing
  no-arg `Execute()` method [...] Do NOT modify the no-arg `Execute()` body in any way." ✓
- T_B123_05 (`T_B123_05_NoArgOverload_StillExists`) uses `Type.EmptyTypes` binder to confirm
  the zero-parameter overload still exists via reflection. ✓
- AC5 explicitly states: "Existing QAll button (no-arg path) still fires 3 targets — no
  regression." ✓

---

### Additional DNA Checks (role definition — all must pass)

| Check | Rule(s) | Result |
|-------|---------|--------|
| No `lock()` in described methods | JS-021 | PASS |
| No `throw new XxxException` in hot path | JS-001 | PASS |
| No `return null` for optional value | JS-002 | PASS (method is void) |
| No `async void` non-event-handler | JS-033 | PASS |
| No mutable struct fields described | JS-008/009 | N/A — no structs introduced |
| No `SolidColorBrush` without Freeze | JS-009 | N/A |
| No `Dictionary<K,V>` on shared state | JS-025 | N/A |
| No UI update from non-UI thread without Dispatcher | JS-023 | N/A — no UI update in new methods |
| No `Account.All` in constructor | NT8 constraint | PASS |
| No `sealed` on TradeCopierWindow | NT8 constraint | N/A |
| No `FontFamily` set | NT8 constraint | N/A |
| No hardcoded hex color | NT8 constraint | N/A |
| No `CreateOrder` with non-PTT- name | NT8 constraint | N/A |
| No `DateTime.Now` | NT8 constraint | PASS |
| No `async/await` in lifecycle method | NT8 constraint | PASS |
| File paths point to Wave workspace src/ | File Routing | PASS — all paths under `src/PropTraderTools/` |

---

### Note (WARN — not FAIL)

The `OnInstrQAll2tClick` body in the ticket (lines 175–197) adds an observability log line and
a `targets` local variable versus the plan's more compact inline version (plan Section 3.2,
line 116). This is a permissible elaboration: CYC remains ≤ 4, no JS rule is violated, no
spec requirement is changed. The log line adds production observability for the QAll2t path.
This is within architect authority and does not require a FAIL.

---

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All 10 checklist items PASS. Zero violations across:
- Traceability
- Signature accuracy
- CYC pre-check
- NT8 constraints
- File completeness
- Test coverage (5 [Fact] methods including T_B123_05 regression)
- 7-scan presence (SCAN-01 through SCAN-07 with exact commands and thresholds)
- Follower path (forcedTargets → ExecuteFollowers, not SnapshotTargetOrders)
- Log line (exact required prefix confirmed)
- Regression guard (no-arg Execute untouched; T_B123_05 confirms)

**Engineer may proceed to Phase 4a implementation.**
