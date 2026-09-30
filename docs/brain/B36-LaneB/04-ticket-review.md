# B36-LaneB Ticket Review
# Epic: DW-B35-TARGETS-01 | be-targets-oco
# Reviewer: ptt-ticket-reviewer (Phase 3.5)
# Date: 2026-07-27
# Source ticket: docs/brain/B36-LaneB/04-tickets.md
# Source plan: docs/brain/B36-LaneB/02-architecture-plan.md (REVIEW_PASS)

---

## Ticket Review: B36-LaneB

### T1 — PttBreakEven OCO + Targets

---

#### CHECK 1 — Traceability

Every ticket item maps to spec requirement DW-B35-TARGETS-01.

| Ticket item | Source |
|-------------|--------|
| C1 SnapshotTargetsLocal | DW-B35-TARGETS-01 root cause part 2 (no snapshot exists) |
| C2 IsAtmTargetName | DW-B35-TARGETS-01 dependency of C1 |
| C3 SubmitBeTargetsLocal | DW-B35-TARGETS-01 root cause part 3 (no target resubmit) |
| C4 Execute() foreach body | DW-B35-TARGETS-01 integration point |
| C5 SubmitBeStopLocal ocoId | DW-B35-TARGETS-01 root cause part 1 (string.Empty at arg8) |
| BuildBeOcoId helper | Architecture plan §C4 CYC mitigation (mandatory per REVIEW_PASS) |

No phantom work found (every item has a plan anchor).
No missing work found (all plan sections C1–C5 + Helper present in ticket).

**Traceability**: PASS

---

#### CHECK 2 — Spec Coverage

Single spec requirement DW-B35-TARGETS-01 appears in exactly one ticket (T1).
No uncovered requirements. No duplicate coverage.

**Spec Coverage**: PASS

---

#### CHECK 3 — JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | No `lock()` described in any new or modified method. SCAN-01 enforces at execution time. | PASS |
| JS-033 (no async void) | All new methods are synchronous (`void`, `bool`, `string`, `List<T>`). SCAN-02 enforces. | PASS |
| JS-002 (no return null) | `SnapshotTargetsLocal` returns `new List<...>()` on null inputs per Binding Instruction #5. `FindPositionLocal`'s pre-existing `return null` is explicitly cited as exempt. | PASS |
| JS-001 (no throw in hot path) | No `throw new XxxException` in any described method body. try/catch blocks catch silently (non-fatal). | PASS |

**JS Pre-Check**: PASS

---

#### CHECK 4 — CYC Pre-Check

| Method | CYC stated | Limit | Verdict |
|--------|-----------|-------|---------|
| `Execute()` (modified) | 8 | ≤ 8 | PASS |
| `SnapshotTargetsLocal` (new) | 3 | ≤ 3 | PASS |
| `IsAtmTargetName` (new) | 2 | ≤ 3 | PASS |
| `BuildBeOcoId` (new helper) | 2 | ≤ 3 | PASS |
| `SubmitBeTargetsLocal` (new) | 4 | ≤ 4 | PASS |
| `SubmitBeStopLocal` (modified) | 3 | ≤ 3 | PASS |

CYC analysis is present and correctly derived. BuildBeOcoId extraction to keep
Execute() at CYC=8 is a binding instruction — the ternary (`acc.Name.Length >= 4 ? ...`)
would add CYC+1 if inlined, yielding CYC=9. The ticket correctly mandates the helper
as non-optional (Binding Engineer Instructions item #1).

**CYC Pre-Check**: PASS

---

#### CHECK 5 — NT8 Constraint Check

| Rule | Method | Ticket Compliance |
|------|--------|-------------------|
| NT8-006 (no LINQ) | `SnapshotTargetsLocal` | `foreach (Order o in acc.Orders)` only. No `.ToList()`, `.Where()`, `.Select()`, `.Any()`. SCAN-03 pattern explicitly includes `.ToList`. PASS |
| NT8-007 (arg11 cast) | `SubmitBeTargetsLocal` | `(NinjaTrader.Cbi.CustomOrder)null` — explicit cast, not string literal. PASS |
| NT8-013 (DateTime.MaxValue) | `SubmitBeTargetsLocal` | `DateTime.MaxValue` for GTC. SCAN-05 guards against `DateTime.Now`. PASS |
| NT8-014 (PTT- prefix) | `SubmitBeTargetsLocal` | Signal names `"PTT-BE-Target-" + (i + 1)` — all start with `"PTT-"`. PASS |
| NT8-049 (Limit arg positions) | `SubmitBeTargetsLocal` | `arg6 = t.Price` (limitPrice), `arg7 = 0` (stopPrice=0). Swap-warning explicitly stated in critical note. PASS |
| NT8-001 (no `{ get; init; }`) | All new code | SCAN-04 covers this. No `init` setters in any described code. PASS |
| No async/await in lifecycle | `Initialize`, `Teardown`, `Execute` | All synchronous. PASS |
| No Account.All outside Loaded | Not used | `ctx.AllAccounts` (from host context) used, not `Account.All`. PASS |
| No sealed on TradeCopierWindow | Not applicable | No window class modified. PASS |
| No hardcoded hex color | Not applicable | No UI color modifications. PASS |
| No FontFamily | Not applicable | No WPF element modifications. PASS |
| CreateOrder names start "PTT-" | `SubmitBeTargetsLocal` | `"PTT-BE-Target-1"` through `"PTT-BE-Target-N"`. PASS |

**NT8 Check**: PASS

---

#### CHECK 6 — Test Coverage

All public and non-trivial private methods described in the ticket have a corresponding
`[Fact]` test:

| Method | Test | Type |
|--------|------|------|
| `SnapshotTargetsLocal` | `T_B36B_SnapshotTargetsLocal_ReadsAtmTargetOrders` | Reflection — signature check |
| `IsAtmTargetName` | `T_B36B_IsAtmTargetName_MatchesTarget1To9Only` | Reflection invoke — 5 functional cases |
| `SubmitBeTargetsLocal` | `T_B36B_SubmitBeTargetsLocal_MethodExists` | Reflection — signature check |
| `BuildBeOcoId` formula | `T_B36B_OcoId_NonEmpty` | Pure arithmetic — formula verification |

All 4 `[Fact]` names present and match required names exactly.
`SubmitBeStopLocal` (modified only — adds one param, body change is a 1-line swap) is
covered by the signature contract of C5 and the SCAN-06 build gate.
No new public methods are left untested.

**Test Coverage**: PASS

---

#### CHECK 7 — Scan Checklist Presence

All 7 scans present. Each has a command, expected result, and is marked blocking (YES).

| Scan | Command | Expected | Blocking |
|------|---------|----------|---------|
| SCAN-01 | `grep -n "lock(" ...PttBreakEven.cs` | 0 results | YES |
| SCAN-02 | `grep -n "async void" ...PttBreakEven.cs` | 0 results | YES |
| SCAN-03 | `grep -n "\.Where\|\.First\|\.Select\|\.Any\|\.ToList" ...PttBreakEven.cs` | 0 results | YES |
| SCAN-04 | `grep -n "{ get; init; }" ...PttBreakEven.cs` | 0 results | YES |
| SCAN-05 | `grep -n "DateTime\.Now" ...PttBreakEven.cs` | 0 results | YES |
| SCAN-06 | `dotnet build` | BUILD_PASS | YES |
| SCAN-07 | `dotnet test --filter "T_B36"` | TEST_PASS (4/4) | YES |

Note: The plan's SCAN-05 (`return null` check) is not reproduced verbatim in the ticket's
SCAN-05 (which checks `DateTime.Now`). This is acceptable — the ticket's 7 scans are
internally consistent and complete. The `return null` constraint is enforced by Binding
Engineer Instruction #5 (which is a binding written contract in the ticket), not by a
scan command. The hard-link gate (`verify_links.ps1 -Fix`, expected `OK=11, DESYNC=0`)
is also present and blocking.

**Scan Checklist**: PASS (all 7 scans present, all blocking)

---

#### CHECK 8 — File Routing

C# source path: `c:/WSGTA/universal-or-strategy/src/PropTraderTools/Features/PttBreakEven.cs`
— Wave workspace (correct).

Test file path: `tests/PropTraderTools.Tests/CopyEngineTests.cs`
— Wave workspace relative path (correct).

No Director workspace (`c:/WSGTA/universal-or-strategy-director/src/`) paths for `.cs` files.

**File Routing**: PASS

---

#### CHECK 9 — Required-Item Checklist (14 items from task brief)

| # | Required Check | Result |
|---|---------------|--------|
| 1 | Traceability: ticket references DW-B35-TARGETS-01 | PASS |
| 2 | 7-scan checklist SCAN-01 through SCAN-07 present | PASS |
| 3 | All 4 [Fact] names present | PASS |
| 4 | NT8-006: SnapshotTargetsLocal uses foreach, not LINQ | PASS |
| 5 | NT8-049: SubmitBeTargetsLocal arg6=limitPrice, arg7=0 | PASS |
| 6 | NT8-007: arg11=(NinjaTrader.Cbi.CustomOrder)null | PASS |
| 7 | NT8-013: DateTime.MaxValue (no DateTime.Now) | PASS |
| 8 | Execute() CYC<=8 analysis present | PASS |
| 9 | All new helpers CYC<=4 | PASS |
| 10 | SubmitBeStopLocal signature: ocoId parameter added | PASS |
| 11 | Snapshot-before-cancel ordering in Execute() foreach body | PASS |
| 12 | BuildBeOcoId helper present and mandatory | PASS |
| 13 | Build tag format: "PTT-COPIER B36 \| be-targets-oco \| {date}" | PASS |
| 14 | Test baseline 180 → 184 stated | PASS |

---

### VERDICT: TICKET_REVIEW_PASS

No violations found across all 14 required checks plus full JS/NT8/CYC/scan/routing review.

The ticket is complete, self-consistent, and constitutes a valid engineer contract.
The engineer may proceed to implementation without re-review.

---

## Overall: TICKET_REVIEW_PASS

**Single ticket T1** — all checks PASS, zero violations.

**Gate**: Safe to spawn `ptt-engineer` for B36-LaneB ticket execution.

---

*Reviewer*: ptt-ticket-reviewer (Phase 3.5)
*Timestamp*: 2026-07-27
*Upstream gate*: 02-plan-review.md REVIEW_PASS 2026-07-27
*Downstream gate*: ptt-engineer may now execute ticket T1
