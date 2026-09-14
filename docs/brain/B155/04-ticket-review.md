# Ticket Review: B155
## Epic: Abort stale drain payload on position-flat (DW-B154-04)

**Reviewer:** PTT Ticket Reviewer (Phase 3.5)
**Phase:** 3.5
**Ticket source:** `docs/brain/B155/04-tickets.md`
**Plan source:** `docs/brain/B155/02-architecture-plan.md` (REVIEW_PASS)
**Plan review source:** `docs/brain/B155/02-plan-review.md` (REVIEW_PASS)

---

## T1 — Abort drain payload when account position goes flat

**Defect:** DW-B154-04
**Spec requirement IDs:** DW-B154-04

---

### SCOPE CONTROL

| Item | Result | Evidence |
|------|--------|----------|
| Exactly ONE ticket — no ticket creep | ✅ PASS | Only T1 present in `04-tickets.md`. |
| Ticket ID is DW-B154-04 | ✅ PASS | Ticket header: `Defect: DW-B154-04 (P1 — directly causes unintended position re-entry)`. |
| Both fixes (a) and (b) in SAME ticket (coupled defect) | ✅ PASS | Fix 1 (SubmitDrainedEntry flat guard), Fix 2 (AbortDrainOnFill call), testable seam, and comment confirmation all present in T1. |

---

### COMPLETENESS — Fix (a): `SubmitDrainedEntry` flat guard

| Item | Result | Evidence |
|------|--------|----------|
| `IsFlat(FindPosition(follower, payload.Instrument))` guard placed AFTER follower null guard (2) and BEFORE `SubmitEntryDirect` (5) | ✅ PASS | Ticket code block: guard at position (3), follower null guard at (2), `SubmitEntryDirect` at (5). |
| `DrainedOrderIds` cleanup (`foreach` + `TryRemove`) present in ABORT path | ✅ PASS | Ticket lines 65–66: `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _);` inside the abort block. |
| `DrainedOrderIds` cleanup (`foreach` + `TryRemove`) present in SUBMIT path (existing code, preserved) | ✅ PASS | Ticket line 70–71: same cleanup at position (6) on the submit path — explicitly noted as renumbered-but-preserved. |
| `"[DRAIN-ABORT-FLAT] acct=" + acctKey` log line present in abort path | ✅ PASS | Ticket lines 62–64: `NinjaTrader.Code.Output.Process("[DRAIN-ABORT-FLAT] acct=" + acctKey, ...)` present in abort block. |
| ASCII-only: no Unicode in log string | ✅ PASS | Ticket explicitly states `"[DRAIN-ABORT-FLAT]"` is ASCII-only; string contains only printable ASCII characters. |
| CYC delta stated as +1 (4→5), within ceiling (≤8) | ✅ PASS | Ticket: `CYC delta: +1. Old CYC = 4, new CYC = 5. Within ceiling of 8.` |

---

### COMPLETENESS — Fix (b): `TryEvictFollowerBeSlot` abort call

| Item | Result | Evidence |
|------|--------|----------|
| `AbortDrainOnFill(accName)` call placed AFTER `_filledBeTargetCount.TryRemove` — NOT before it | ✅ PASS | Ticket section (b) shows find-target as `_filledBeTargetCount.TryRemove(accName, out _);` with insertion point immediately after. |
| Comment `"B155 DW-B154-04"` present on the new call | ✅ PASS | Ticket comment: `// B155 DW-B154-04: abort any pending drain payload when position goes flat.` |
| CYC delta stated as 0 (void call, no branch) | ✅ PASS | Ticket: `CYC delta: 0 (void call, no branch). Method CYC stays at 6.` |
| `TryEvictFollowerBeSlot` CYC stays at 6 | ✅ PASS | Explicitly confirmed via zero-branch void call. |

---

### TESTABLE SEAM

| Item | Result | Evidence |
|------|--------|----------|
| `SubmitDrainedEntryTestable` declared as `internal` (not `private`) — required for `InternalsVisibleTo` | ✅ PASS | Ticket section (c): `"access modifier MUST be `internal`, not `private`"` and code: `internal void SubmitDrainedEntryTestable(string acctKey) => SubmitDrainedEntry(acctKey);` |

---

### TEST COVERAGE

| Item | Result | Evidence |
|------|--------|----------|
| Test a: `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat` — `[Fact]` tag, verifies abort path and `DrainedOrderIds` cleanup | ✅ PASS | Ticket "Test a": `[Fact]` tag present; asserts `_pendingDispatchDrains` does not contain `acctKey` post-call, `_drainOwnedOrderIds` does not contain seeded IDs (cleanup on abort path), and `SubmitEntryDirect` was NOT called. |
| Test b: `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat` — `[Fact]` tag, structural/reflection | ✅ PASS | Ticket "Test b": `[Fact]` tag present; structural reflection approach to confirm `"AbortDrainOnFill"` is present in method body. |
| Test c: `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper` — `[Fact]` tag, existence contract via reflection | ✅ PASS | Ticket "Test c": `[Fact]` tag present; uses `typeof(CopyEngine).GetMethod("AbortDrainOnFill", BindingFlags.NonPublic | BindingFlags.Instance)` with assertions on nullity, return type, and parameter count/type. |

---

### 7-SCAN CHECKLIST PRESENCE

The ticket labels the section "7-Scan Checklist" but contains all 8 required scans (SCAN-1 through SCAN-8). Heading label is cosmetically inaccurate but all 8 scans are present with correct commands and required results.

| Scan | Result | Evidence |
|------|--------|----------|
| SCAN-1: `grep -rn "lock\s*(" src/PropTraderTools/` — 0 matches | ✅ PASS | Ticket table row 1. |
| SCAN-2: non-ASCII on `CopyEngine.cs` — 0 matches | ✅ PASS | Ticket table row 2. |
| SCAN-3: non-ASCII on `CopyEngineTests.cs` — 0 matches | ✅ PASS | Ticket table row 3. |
| SCAN-4: `lizard --CCN 8 src/PropTraderTools/CopyEngine.cs` — 0 warnings | ✅ PASS | Ticket table row 4. |
| SCAN-5: `lizard --CCN 8 src/PropTraderTools/CopyEngineTests.cs` — 0 warnings | ✅ PASS | Ticket table row 5. |
| SCAN-6: `dotnet build` — 0 errors, 0 warnings | ✅ PASS | Ticket table row 6. |
| SCAN-7: `lizard` for `SubmitDrainedEntry` — CYC = 5 | ✅ PASS | Ticket table row 7. |
| SCAN-8: `lizard` for `TryEvictFollowerBeSlot` — CYC = 6 | ✅ PASS | Ticket table row 8. |

---

### NT8 SAFETY

| Item | Result | Evidence |
|------|--------|----------|
| `FindPosition` used (not direct `acc.Positions` access) — consistent with NT8 safe pattern | ✅ PASS | Ticket (a) rules: `"use the existing FindPosition helper. Do not access acc.Positions directly."` |
| No `lock()` introduced | ✅ PASS | JS-003 compliance stated in ticket (a) and (b). All state mutations use `ConcurrentDictionary.TryRemove`. |
| No `async void` introduced | ✅ PASS | No async/await mentioned in any change description. |
| No `throw` introduced | ✅ PASS | JS-001 compliance stated in ticket (a): `"no throw — IsFlat and FindPosition are pure reads."` |

---

### TRACEABILITY

| Item | Result | Evidence |
|------|--------|----------|
| Ticket references defect DW-B154-04 from B154 deferred backlog | ✅ PASS | Ticket header: `Defect: DW-B154-04 (P1 — directly causes unintended position re-entry)` and `Spec requirement IDs: DW-B154-04`. |
| SIM verify gate present (6 steps) | ✅ PASS | Ticket "Verify" section: 6 numbered steps covering live copy confirmation, BE ALL trigger, flat wait, no-entry assert, `[DRAIN-ABORT-FLAT]` output assert, and `[DRAIN-TIMEOUT-SUBMIT]` absence assert. |
| Deferred items DW-B154-05, DW-B154-06, DW-B153-05, DW-B153-06 listed as carried forward | ✅ PASS | Ticket "Deferred" table: all four items present as OPEN. |

---

### FILE ROUTING

| Item | Result | Evidence |
|------|--------|----------|
| C# source paths point to Wave workspace (`src/PropTraderTools/`) | ✅ PASS | Ticket files table: `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs`. No director-workspace paths for `.cs` files. |

---

### JS PRE-CHECK (Jane Street DNA)

| Rule | Result | Evidence |
|------|--------|----------|
| JS-001: No `throw` in hot path (DU) | ✅ PASS | Ticket explicitly confirms no throw. `IsFlat` and `FindPosition` are pure reads. |
| JS-002: No `return null` for optional value | ✅ PASS | No new nullable return paths introduced. |
| JS-003: No `lock()` introduced | ✅ PASS | Confirmed via `ConcurrentDictionary.TryRemove` pattern throughout. |
| JS-021: No `Account.All` call outside Loaded handler | ✅ PASS | No `Account.All` introduced. |
| JS-008/009: No mutable struct fields / unfrozen brushes | ✅ PASS | No struct or brush changes. |

---

### CYC PRE-CHECK

| Method | Old CYC | New CYC | Ceiling | Result |
|--------|---------|---------|---------|--------|
| `SubmitDrainedEntry` | 4 | 5 | 8 | ✅ PASS — delta +1, within ceiling |
| `TryEvictFollowerBeSlot` | 6 | 6 | 8 | ✅ PASS — delta 0, void call |

---

## VERDICT: TICKET_REVIEW_PASS

**All 38 checklist items pass. Zero violations found.**

The ticket is a complete, traceable, engineer-ready implementation contract:
- Both fixes are in a single ticket as required by the defect coupling constraint.
- Fix (a) flat guard is correctly ordered (after follower null guard, before SubmitEntryDirect), with DrainedOrderIds cleanup on both abort and submit paths.
- Fix (b) AbortDrainOnFill call is correctly placed after `_filledBeTargetCount.TryRemove`, with the required comment tag.
- Testable seam is `internal` as required by InternalsVisibleTo.
- All three tests carry `[Fact]` tags and cover the abort path, the structural call presence, and the helper existence contract.
- All 8 scans (SCAN-1 through SCAN-8) are present with correct commands and required results.
- No lock, no async void, no throw, no Unicode, no direct acc.Positions access.
- SIM gate has 6 steps; all four deferred items are carried forward as OPEN.
- File paths are Wave workspace (`src/PropTraderTools/`).

**TICKET_REVIEW_PASS — Phase 4a engineer spawn authorized.**
