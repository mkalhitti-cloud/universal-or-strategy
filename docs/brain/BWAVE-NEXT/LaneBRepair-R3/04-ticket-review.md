# Ticket Review: BWAVE-NEXT LaneBRepair-R3
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-22
**Ticket file**: `04-tickets.md`
**Plan file**: `02-architecture-plan.md`
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — R3-F1 + R3-F2 + R3-V1 (single ticket, all R3 items)

### Traceability: PASS

| Item | Plan Reference | Ticket Section | Status |
|------|---------------|----------------|--------|
| R3-F1 | `02-architecture-plan.md` §2/§4 | Ticket R3-F1 section (lines 38–103) | TRACED |
| R3-F2 | `02-architecture-plan.md` §2/§5 | Ticket R3-F2 section (lines 106–230) | TRACED |
| R3-V1 | `02-architecture-plan.md` §2/§6/§9 | Ticket R3-V1 section (lines 234–259) | TRACED — DISMISSED |
| Deferred backlog | `02-architecture-plan.md` §11 | Ticket deferred table (lines 325–330) | TRACED — carry-forward unchanged |

No phantom work found. No plan items missing from ticket.

---

### JS Pre-Check: PASS

| Rule | Description | Ticket Claim | Verdict |
|------|-------------|--------------|---------|
| JS-021 (P0) | No `lock()` | R3-F2 JS rules table: "Not present — satisfied" | PASS |
| JS-001 (P0) | No `throw new` in hot path | No exception throwing in any fix description | PASS |
| JS-002 (P0) | No `return null` | No new null returns described | PASS |
| JS-033 (P0) | No `async void` (non-event-handler) | Not introduced in either fix | PASS |
| JS-036/037 (P0) | No heap alloc in hot path | Statement reorder only; no new allocations | PASS |

---

### CYC Pre-Check: PASS

| Method | Before | After | Change | Budget Met |
|--------|--------|-------|--------|-----------|
| `SubmitDrainedEntry` | 4 | 4 | 0 | YES (≤8) |
| `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` | N/A (test) | N/A (test) | 0 | N/A |

- CYC=4 explicitly stated in ticket (lines 193–198) and plan §8.
- "After" code block renumbers comment labels (3)↔(4) only — no new decision branch added.
- `BwaveNextLaneBTests.cs` change: CYC impact zero; test file; not tracked.

---

### NT8 Check: PASS

| Constraint | Status |
|-----------|--------|
| No `Account.Change()` | Not described in any fix |
| No `AtmStrategyCreate()` | Not described in any fix |
| No `AtmStrategyChangeStopTarget()` | Not described in any fix |
| No `try/catch` added | Ticket R3-F2 NT8 table: "No try/catch added — statement reorder only" |
| No `DateTime.Now` | Not introduced |
| No `lock()` | Not present (also JS-021) |
| No `CreateOrder` without `PTT-` prefix | Not introduced |
| R3-V1 dismissal backed by NT8 docs | `NT8_FULL_REFERENCE.md` lines 2106, 770, 3023, 3468; `NT8_ADDON_KNOWLEDGE.md` line 229 — all cited |

---

### Completeness: PASS

All 3 spec requirement IDs in scope are addressed in T1:

| Req ID | Addressed | How |
|--------|-----------|-----|
| R3-F1 | YES | Full before/after code, single-line fix at `BwaveNextLaneBTests.cs` ~172 |
| R3-F2 | YES | Full before/after code, statement reorder in `SubmitDrainedEntry` |
| R3-V1 | YES | DISMISSED with NT8 evidence table and engineer documentation requirement |

---

### Test Coverage: PASS

| Req | Test Name | Filter Key | Assert | [Fact] Present |
|-----|-----------|-----------|--------|----------------|
| R3-F1 | `FindFollowerEntryOrder_AcceptsEntryName_ForCloneMode` | `FindFollowerEntryOrder` | `Assert.NotNull(method)` | YES |
| R3-F2 | `DrainThenDispatch` | `DrainThenDispatch` | Full drain flow reaches SubmitEntryDirect | YES |
| R3-F2 | `OnDrainCancelAck` | `OnDrainCancelAck` | Drain cancel-ack transitions correctly | YES |
| R3-F2 | `DrainWatchdog` | `DrainWatchdog` | Watchdog timeout path is correct | YES |
| R3-F2 | `ActiveOrders` | `ActiveOrders` | ActiveOrders list correctness preserved | YES |
| R3-F2 | `NakedDetector` | `NakedDetector` | Naked-entry detection unaffected | YES |
| R3-F2 | `AbortDrainOnFill` | `AbortDrainOnFill` | Drain aborts correctly on fill event | YES |

Full dotnet test filter string present at SCAN-06:
```
DrainThenDispatch|OnDrainCancelAck|DrainWatchdog|ActiveOrders|NakedDetector|AbortDrainOnFill|FindFollowerEntryOrder
```

---

### Scan Checklist: PASS

All 7 scans present in ticket (lines 263–275):

| Scan | Command | Required Result | Present |
|------|---------|----------------|---------|
| SCAN-01 — lock() | `grep -r "lock(" src/ --include="*.cs"` | 0 results | ✅ |
| SCAN-02 — async void | `grep -rn "async void " src/ --include="*.cs"` | 0 results (non-event-handler) | ✅ |
| SCAN-03 — return null | `grep -rn "return null;" src/ --include="*.cs"` | Review; none new in modified files | ✅ |
| SCAN-04 — CYC | `python scripts/complexity_audit.py` | `SubmitDrainedEntry` CYC <= 4 | ✅ |
| SCAN-05 — Build | `dotnet build` | 0 errors, 0 warnings | ✅ |
| SCAN-06 — Tests | `dotnet test --filter "DrainThenDispatch\|...\|FindFollowerEntryOrder"` | All pass | ✅ |
| SCAN-07 — NT8 Sync | `powershell -File scripts\ptt-sync-and-verify.ps1` | 0 MISMATCH | ✅ |

---

### File Routing: PASS

| File | Path | Workspace |
|------|------|-----------|
| `BwaveNextLaneBTests.cs` | `src/PropTraderTools/Tests/BwaveNextLaneBTests.cs` | Wave (`c:\WSGTA\universal-or-strategy`) |
| `CopyEngine.cs` | `src/PropTraderTools/CopyEngine.cs` | Wave (`c:\WSGTA\universal-or-strategy`) |

No Director workspace paths used for `.cs` files.

---

### Specific Constraint Checks: PASS

**R3-F1 specifics**:

| Check | Evidence | Result |
|-------|----------|--------|
| Fix targets line ~172 `GetMethod` call only | Ticket line 28: "single-line BindingFlags fix at ~line 172" | PASS |
| `Priv` constant (line 15) explicitly protected (NOT changed) | Ticket line 68: "Do NOT modify the `Priv` constant at line 15"; acceptance checklist confirms | PASS |
| `BindingFlags.Static` (not Instance) in fix | Ticket "After" code: `BindingFlags.NonPublic \| BindingFlags.Static` | PASS |
| No change to other tests in file | Acceptance checklist: "Only line ~172 modified in BwaveNextLaneBTests.cs" | PASS |

**R3-F2 specifics**:

| Check | Evidence | Result |
|-------|----------|--------|
| Verify-first condition stated (read lines 6627–6647 before applying) | Ticket line 125: "Read lines 6627–6651. If foreach ... TryRemove appears before SubmitEntryDirect — apply fix." | PASS |
| `_pendingDispatchDrains.TryRemove` stays at position 1 | Acceptance checklist line 289; "After" code shows it as first statement | PASS |
| `foreach` cleanup moves to AFTER `SubmitEntryDirect` | "After" code block: SubmitEntryDirect at (3), foreach at (4) | PASS |
| No try/catch in fix | NT8 constraints table + acceptance checklist | PASS |
| CYC=4 budget stated | Ticket lines 193–198; plan §8 table | PASS |

**R3-V1 specifics**:

| Check | Evidence | Result |
|-------|----------|--------|
| DISMISSED with NT8 evidence cited | Evidence table: NT8_FULL_REFERENCE.md lines 2106/770/3023/3468; NT8_ADDON_KNOWLEDGE.md line 229 | PASS |
| No source code change for R3-V1 | Not listed in "Files Touched" table; "No code change required" stated | PASS |

**Locked architecture constraints**:

| Constraint | Evidence | Result |
|-----------|----------|--------|
| `(long)(int)Environment.TickCount` preserved (no TickCount64) | Acceptance checklist: "preserved (not changed to TickCount64)"; plan §9 dismissed table confirms lock | PASS |
| `.ToList()` preserved | Acceptance checklist: "preserved (not removed)"; plan §9 dismissed table confirms lock | PASS |
| No out-of-scope items | Deferred backlog DW-NEXT-B-01 through B-04 unchanged; no new scope introduced | PASS |

---

### VERDICT: TICKET_REVIEW_PASS

T1 satisfies all required checks with zero violations.

---

## Overall: TICKET_REVIEW_PASS

All checks across T1 pass. Safe to spawn ptt-engineer.

| Check | T1 |
|-------|----|
| Traceability | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| Completeness | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01 – SCAN-07) | PASS |
| File Routing | PASS |
| R3-F1 Specific Checks | PASS |
| R3-F2 Specific Checks | PASS |
| R3-V1 Specific Checks | PASS |
| Locked Architecture Constraints | PASS |

**TICKET_REVIEW_PASS**
