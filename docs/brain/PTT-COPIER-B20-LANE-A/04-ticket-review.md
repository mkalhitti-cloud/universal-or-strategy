# PTT-COPIER-B20-LANE-A -- Ticket Review
# Phase 3.5 output (ptt-ticket-reviewer)
# Status: TICKET_REVIEW_PASS
# Date: 2026-07-14
# Reviewer: ptt-ticket-reviewer
# Tickets reviewed: docs/brain/PTT-COPIER-B20-LANE-A/04-tickets.md
# Source evidence:
#   c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
#   c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs

---

## Ticket Review: PTT-COPIER-B20-LANE-A

---

### T1 -- PopulateOrderMap Dedup Guard (DW-B19-02)

**Traceability**: PASS
- Ticket T1 maps to spec requirement DW-B19-02 ("PopulateOrderMap dedup guard uses C# object reference equality on `Account`").
- Plan §4 and §5 describe the exact fix. The ticket body precisely implements what the plan specifies.
- No phantom work present: every item in T1 (one-line predicate change + one reflection-based test) has a matching plan section.
- No missing work: DW-B19-02 is fully addressed. Plan §8 maps it to T1 with status CLOSED.

**JS Pre-Check**: PASS
| Rule | Finding |
|------|---------|
| JS-021 | No `lock()` added. Predicate is a pure lambda expression inside `ConcurrentBag.Any()`. PASS |
| JS-002 | `PopulateOrderMap` returns `void`. No `return null` in the changed line. PASS |
| JS-001 | No `throw` added. No exception paths introduced. PASS |
| JS-033 | No `async void` modifier. PASS |
| JS-015 | No new parameter crossing an API boundary. Existing `Account followerAccount` parameter unchanged. PASS |
| JS-003 | `FollowerBinding` readonly struct is unchanged. PASS |

**CYC Pre-Check**: PASS
- `PopulateOrderMap` CYC before fix: 2 (base=1, one `if` branch). CYC after fix: 2 (the lambda predicate expression changes, but the control-flow branch count is unchanged). `?.` null-conditional operators are expression-level, not control-flow branches.
- CYC 2 << limit of 8. No split required.

**NT8 Check**: PASS
| Rule | Finding |
|------|---------|
| NT8-001 | No `{ get; init; }` accessor introduced. PASS |
| NT8-002 | No `abstract record` / `sealed record` introduced. PASS |
| NT8-003 | No `volatile double` / `volatile long` introduced. PASS |
| NT8-004 | No `ImmutableDictionary` introduced. PASS |
| NT8-007 | No `CreateOrder` call added. PASS |
| NT8-031 | `Math.Clamp` not used. PASS |
| `DateTime.Now` | Not used. Test uses `DateTime.UtcNow.Ticks`. PASS |
| `Account.Name` setter | Object-initializer `new Account { Name = "Sim101-B20" }` requires public setter. Plan §12 confirms this is a pre-condition verified by the B19 test suite (`Gate2_UsesAccountName_SourceContractVerified` at line 1957 of CopyEngineTests.cs). Fallback clause is present in the ticket. PASS |

**Test Coverage**: PASS
- New method: `PopulateOrderMap` (private). Test: `PopulateOrderMap_DedupGuard_UsesNameEquality` — invoked via reflection, asserts `bag.Count == 1` after two identical-name different-reference `Account` objects are supplied.
- `[Fact]` attribute present. `Assert.Equal(1, bag.Count)` is a clear, deterministic assertion.
- Unique signal name `"B20-DEDUP-" + DateTime.UtcNow.Ticks` prevents cross-test contamination from shared `CopyEngine.Instance` singleton state. PASS

**Scan Checklist**: PASS
All 7 scans present with exact commands and expected values:
| Scan | Present | Command | Expected |
|------|---------|---------|----------|
| SCAN-01 | YES | `grep -n "b\.FollowerAccount == followerAccount" ...` | 0 matches |
| SCAN-02 | YES | `grep -n "FollowerAccount?.Name == followerAccount?.Name" ...` | 1 match |
| SCAN-03 | YES | `grep -n "PopulateOrderMap_DedupGuard_UsesNameEquality" ...` | 1 match |
| SCAN-04 | YES | `grep -c "\[Fact\]" ...` | 119 |
| SCAN-05 | YES | `grep -n "lock(" ...` | 0 matches |
| SCAN-06 | YES | `grep -rn "async void " ...` | 0 matches |
| SCAN-07 | YES | `dotnet build` or `dotnet test` | 0 errors |

**File Routing**: PASS
- Production: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` (wave workspace) ✓
- Test: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` (wave workspace) ✓
- No director-workspace `.cs` paths referenced.

**Additional Checks**:
- Write-set compliance: PASS — only `CopyEngine.cs` and `CopyEngineTests.cs` in wave workspace. No Panel/Window/AddOn files touched.
- Signal name uniqueness (cross-test contamination prevention): PASS — `"B20-DEDUP-" + DateTime.UtcNow.Ticks` guarantees an empty bag before the test runs.
- Source verification (lines 648-665 in `CopyEngine.cs` confirmed): The current predicate at line 659 reads exactly `if (!bag.Any(b => b.FollowerAccount == followerAccount))`. The ticket's BEFORE/AFTER diff is accurate.

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 -- Copy ON/OFF State Event (DW-B17-SYNC-01)

**Traceability**: PASS
- Ticket T2 maps to spec requirement DW-B17-SYNC-01 ("No boolean event from `SetEnabled`; Panel/Window cannot reliably sync toggle").
- Plan §6 and §7 describe both additions (event field after line 125, invoke after line 234 in `SetEnabled`). The ticket body implements exactly what the plan specifies.
- No phantom work present: CHANGE A (event field) and CHANGE B (invoke site) are both in the plan.
- No missing work: DW-B17-SYNC-01 is fully addressed. Panel/Window wiring is explicitly deferred to Lane B — correctly excluded from T2 scope (V12.23 compliant).
- Plan §8 maps DW-B17-SYNC-01 to T2 with status CLOSED.

**JS Pre-Check**: PASS
| Rule | Finding |
|------|---------|
| JS-021 | No `lock()` added. `CopyEnabledChanged?.Invoke(enabled)` uses C# null-conditional operator. The compiler atomically snapshots the delegate reference before the null check — no lock required and none may be added per JS-021. PASS |
| JS-002 | `SetEnabled` returns `void`. No `return null`. PASS |
| JS-001 | No `throw` added. PASS |
| JS-033 | No `async void` modifier. PASS |
| JS-015 | No new parameter added. `bool enabled` is an existing parameter. PASS |
| JS-023 | No new `volatile` field added. `_isCopyEnabled` is an existing volatile field unchanged by T2. PASS |

**Thread-safety note** (JS-021 extension, per plan §11): `CopyEnabledChanged?.Invoke(enabled)` is the canonical C# thread-safe delegate invocation idiom. The `?.` null-conditional captures a delegate snapshot atomically. This is explicitly documented in the ticket and is correct.

**CYC Pre-Check**: PASS
- `SetEnabled` CYC before T2: 1 (base=1, no control-flow branches; the ternary `enabled ? "ON" : "OFF"` is a pre-existing expression, not a new branch).
- CYC after T2: 1 (`CopyEnabledChanged?.Invoke(enabled)` is a null-conditional expression statement, not an `if` branch).
- CYC 1 << limit of 8. No split required.

**NT8 Check**: PASS
| Rule | Finding |
|------|---------|
| NT8-001 | `public event Action<bool> CopyEnabledChanged;` is an event field declaration, not a property with `init` accessor. PASS |
| NT8-002 | No record type. PASS |
| NT8-003 | No `volatile` on `double` or `long`. PASS |
| NT8-004 | No `ImmutableDictionary`. PASS |
| NT8-007 | No `CreateOrder` call. PASS |
| NT8-031 | `Math.Clamp` not used. PASS |
| `event Action<bool>` | Standard C# delegate event field. Supported in .NET 4.8 / C# 7.x (NT8 target framework). PASS |
| `DateTime.Now` | Not used in new code. PASS |

**Test Coverage**: PASS
- New API surface: `CopyEnabledChanged` (public event). Test: `SetEnabled_FiresCopyEnabledChanged` — subscribes directly (no reflection needed), calls `SetEnabled(true)` then `SetEnabled(false)`, asserts `received == true` then `received == false`.
- `[Fact]` attribute present. `Assert.Equal(true, received)` and `Assert.Equal(false, received)` are clear, deterministic assertions covering both boolean states.
- Singleton teardown: `try/finally` block unconditionally unsubscribes `handler` via `_engine.CopyEnabledChanged -= handler`. This correctly prevents lambda accumulation across test runs for the shared `CopyEngine.Instance` singleton. PASS

**Scan Checklist**: PASS
All 7 scans present with exact commands and expected values:
| Scan | Present | Command | Expected |
|------|---------|---------|----------|
| SCAN-01 | YES | `grep -n "CopyEnabledChanged" ...CopyEngine.cs` | >= 2 matches |
| SCAN-02 | YES | `grep -n "CopyEnabledChanged?.Invoke(enabled)" ...` | 1 match |
| SCAN-03 | YES | `grep -n "SetEnabled_FiresCopyEnabledChanged" ...` | 1 match |
| SCAN-04 | YES | `grep -c "\[Fact\]" ...` | 120 |
| SCAN-05 | YES | `grep -n "lock(" ...` | 0 matches |
| SCAN-06 | YES | `grep -rn "async void " ...` | 0 matches |
| SCAN-07 | YES | `dotnet build` or `dotnet test` | 0 errors |

**File Routing**: PASS
- Production: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` (wave workspace) ✓
- Test: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` (wave workspace) ✓
- No director-workspace `.cs` paths referenced.

**Additional Checks**:
- Write-set compliance: PASS — only `CopyEngine.cs` and `CopyEngineTests.cs`. `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs` explicitly out of scope. Panel/Window wiring deferred to Lane B.
- Singleton teardown (try/finally): PASS — `_engine.CopyEnabledChanged -= handler` is in the `finally` block. Lambda cannot leak across tests even on assertion failure.
- CHANGE A insertion point: Plan §6 states "immediately after line 125 (`internal event Action<string> PendingBeFired;`)". Source-verified: lines 118–125 of `CopyEngine.cs` end with `internal event Action<string> PendingBeFired;`. Placement is accurate.
- CHANGE B insertion point: Plan §6 states "after `StatusUpdate?.Invoke(...)` (line 234)". Source-verified at lines 231–235: `SetEnabled` body ends with `StatusUpdate?.Invoke(...)` and the closing brace. Placement is accurate.
- T2 SCAN-04 count dependency on T1: The ticket documents that T2's SCAN-04 expects count 120, which presupposes T1 added the count-119 test. The execution order section explicitly requires T1 SCAN-01..07 to PASS before T2 begins. PASS (dependency correctly documented).

**VERDICT: TICKET_REVIEW_PASS**

---

## Execution Order Compliance

The ticket documents a mandatory sequential execution order:
```
T1 start → T1 SCAN-01..07 PASS → T2 start → T2 SCAN-01..07 PASS → Lane A COMPLETE
```
SCAN-04 for T2 (count=120) explicitly presupposes T1 completion (count=119). This dependency is correctly documented and enforced. PASS

---

## Spec Coverage Matrix

| Requirement | Covered By | Status |
|-------------|------------|--------|
| DW-B19-02: `PopulateOrderMap` dedup guard reference equality fix | T1 | CLOSED |
| DW-B17-SYNC-01: `CopyEnabledChanged` event declaration and fire site | T2 | CLOSED |
| Write-set: CopyEngine.cs + CopyEngineTests.cs only | T1 + T2 | PASS |
| xUnit [Fact] tests for both tickets | T1 + T2 | PASS |
| CYC <= 8 for all modified methods | T1 (CYC=2) + T2 (CYC=1) | PASS |
| JS P0 constraints satisfied | T1 + T2 | PASS |
| NT8 constraints satisfied | T1 + T2 | PASS |
| Singleton teardown for T2 event test | T2 try/finally | PASS |
| Unique signal name for T1 test | T1 `"B20-DEDUP-" + Ticks` | PASS |
| 7-scan checklist in each ticket | T1 + T2 | PASS |
| File routing to wave workspace | T1 + T2 | PASS |

---

## Violation Log

No violations found. All checks PASS for both tickets.

---

## Summary

| Check | T1 | T2 |
|-------|----|----|
| Traceability | PASS | PASS |
| JS Pre-Check | PASS | PASS |
| CYC Pre-Check | PASS | PASS |
| NT8 Check | PASS | PASS |
| Test Coverage | PASS | PASS |
| Scan Checklist (7/7 present) | PASS | PASS |
| File Routing | PASS | PASS |
| Write-Set Compliance | PASS | PASS |
| Singleton Safety | PASS (unique signal key) | PASS (try/finally teardown) |

---

## Overall: TICKET_REVIEW_PASS

Both tickets are engineering-ready. The architect has produced a clean, minimal, traceable implementation contract.
The engineer may proceed directly to execution against `04-tickets.md`.

**Return: TICKET_REVIEW_PASS**
