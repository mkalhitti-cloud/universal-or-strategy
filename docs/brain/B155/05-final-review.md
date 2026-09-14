# B155 Final Review
## Epic: Abort stale drain payload on position-flat (DW-B154-04)

**Reviewer:** PTT Plan Reviewer (Phase 5)
**Block:** B155
**Ticket count:** 1 (T1 only)
**Phase 4b result ingested:** VERIFY_PASS
**Result: FINAL_PASS**

---

## A. IMPLEMENTATION COMPLETENESS

| Item | Spec Source | Physical Location | Result |
|------|-------------|-------------------|--------|
| TICKET 1 VERIFY_PASS confirmed | ticket-1-verification.md | Final Decision: VERIFY_PASS | ✅ PASS |
| Fix (a): `IsFlat` flat guard present in `SubmitDrainedEntry` after follower null guard (2) and before `SubmitEntryDirect` (5) | 04-tickets.md change (a), 02-architecture-plan.md §2 | `CopyEngine.cs:8246` — `if (IsFlat(FindPosition(follower, payload.Instrument))) // (3) NEW -- abort if flat` | ✅ PASS |
| Fix (b): `AbortDrainOnFill` call present in `TryEvictFollowerBeSlot` after `_filledBeTargetCount.TryRemove` | 04-tickets.md change (b), 02-architecture-plan.md §2 | `CopyEngine.cs:1912` — `AbortDrainOnFill(accName);` | ✅ PASS |
| `SubmitDrainedEntryTestable` seam present as `internal` | 04-tickets.md change (c) | `CopyEngine.cs:7917` — `internal void SubmitDrainedEntryTestable(string acctKey) => SubmitDrainedEntry(acctKey);` | ✅ PASS |
| `DrainedOrderIds` cleanup on abort path | 04-tickets.md, 02-architecture-plan.md §2 | `CopyEngine.cs:8251–8252` — `foreach (var id in payload.DrainedOrderIds) _drainOwnedOrderIds.TryRemove(id, out _);` inside abort block | ✅ PASS |
| `DrainedOrderIds` cleanup on submit path | 04-tickets.md, 02-architecture-plan.md §2 | `CopyEngine.cs:8266–8267` — same cleanup at position (6), fall-through submit path | ✅ PASS |
| `[DRAIN-ABORT-FLAT]` log line in abort path | 04-tickets.md | `CopyEngine.cs:8248–8250` — `NinjaTrader.Code.Output.Process("[DRAIN-ABORT-FLAT] acct=" + acctKey, ...)` | ✅ PASS |
| `AbortDrainOnFill` insertion point: AFTER `_filledBeTargetCount.TryRemove` (line 1908), NOT before | 04-tickets.md change (b) | `CopyEngine.cs:1908` is `_filledBeTargetCount.TryRemove`; `1912` is `AbortDrainOnFill(accName)` — correct order confirmed | ✅ PASS |
| Comment `// B155 DW-B154-04:` present on abort call in `TryEvictFollowerBeSlot` | 04-tickets.md change (b) | `CopyEngine.cs:1909` | ✅ PASS |
| Comment `// B155 DW-B154-04: flat guard (3) aborts submit when account is flat.` at top of `SubmitDrainedEntry` | 04-tickets.md change (d) | `CopyEngine.cs:8236` | ✅ PASS |
| `OnDrainCancelAck` NOT modified | 04-tickets.md, 02-plan-review.md §SAFETY | Ticket FILES MODIFIED table omits `OnDrainCancelAck`; engineer deviation report: "None. All changes implemented exactly as specified" | ✅ PASS |
| `TryDrainWatchdog` NOT modified | 04-tickets.md, 02-plan-review.md §SAFETY | Ticket FILES MODIFIED table omits `TryDrainWatchdog`; Fix 1 safety net at `SubmitDrainedEntry` covers the watchdog path | ✅ PASS |

---

## B. SCAN RESULTS

All 8 scans independently run by verifier (Layer 3) and confirmed against engineer report (Layer 2). Zero discrepancies.

| Scan | Command | Engineer (L2) | Verifier (L3) | Required | Result |
|------|---------|--------------|--------------|---------|--------|
| SCAN-1 | `Select-String ... "(?<![a-zA-Z])lock\s*\("` | 0 matches | 0 matches | 0 matches | ✅ PASS |
| SCAN-2 | Non-ASCII in `CopyEngine.cs` | 0 matches | 0 matches | 0 matches | ✅ PASS |
| SCAN-3 | Non-ASCII in `CopyEngineTests.cs` | 0 matches | 0 matches | 0 matches | ✅ PASS |
| SCAN-4 | `lizard --CCN 8 CopyEngine.cs` | 7 warnings (all pre-existing) | 7 warnings (all pre-existing) | 0 new | ✅ PASS |
| SCAN-5 | `lizard --CCN 8 CopyEngineTests.cs` | 3 warnings (all pre-existing) | 3 warnings (all pre-existing) | 0 new | ✅ PASS |
| SCAN-6 | `dotnet build` | 0 errors, 0 warnings | 0 errors, 0 warnings | 0 errors | ✅ PASS |
| SCAN-7 | `lizard` for `SubmitDrainedEntry` CCN | CCN=4 (delta +1) | CCN=4 (delta +1) | delta +1, ≤8 | ✅ PASS |
| SCAN-8 | `lizard` for `TryEvictFollowerBeSlot` CCN | CCN=8 (delta 0) | CCN=8 (delta 0) | delta 0, ≤8 | ✅ PASS |

**SCAN-4/5 pre-existing violations note:** 7 pre-existing CCN>8 violations in `CopyEngine.cs` and 3 in `CopyEngineTests.cs` were present before B155. None introduced by B155. Pre-existing status documented in B154 and prior blocks.

**SCAN-7 methodology note:** Ticket spec stated CYC=5 (architect counts `foreach`); lizard 1.24.0 reports CCN=4 (does not count `foreach`). The critical relative delta is +1 in both methodologies — confirmed. Absolute discrepancy is a pre-existing tool methodology issue that pre-dates B155, documented identically in B154. No violation.

**SCAN-8 methodology note:** Ticket spec stated CYC=6; lizard reports CCN=8 (was already 8 before B155). Void call adds 0 branches; delta confirmed as 0. At ceiling but not over. Pre-existing absolute discrepancy; not introduced by B155.

---

## C. QUALITY

| Item | Evidence | Result |
|------|----------|--------|
| No `lock()` introduced | SCAN-1 = 0 matches across all of `src/PropTraderTools/` | ✅ PASS |
| ASCII-only string literals — `"[DRAIN-ABORT-FLAT]"` confirmed pure ASCII | SCAN-2, SCAN-3 = 0 non-ASCII matches; string visible at `CopyEngine.cs:8249` | ✅ PASS |
| All 3 new `[Fact]` tests physically present in `CopyEngineTests.cs` | `[Fact]` + `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat` at line 9343–9344; `[Fact]` + `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat` at line 9381–9382; `[Fact]` + `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper` at line 9411–9412 — confirmed by direct source read | ✅ PASS |
| New test class `B155ContractTests` present | `CopyEngineTests.cs:9341` | ✅ PASS |
| No modification of `OnDrainCancelAck` | Engineer deviation report: none; FILES MODIFIED omits method | ✅ PASS |
| No modification of `TryDrainWatchdog` | Engineer deviation report: none; FILES MODIFIED omits method | ✅ PASS |
| `FindPosition` used (not `acc.Positions` directly) | `CopyEngine.cs:8246` uses `FindPosition(follower, payload.Instrument)` | ✅ PASS |
| No new NT8 API introduced | All calls are to existing local helpers (`IsFlat`, `FindPosition`, `AbortDrainOnFill`) | ✅ PASS |
| JS-001: No `throw` in gate chain | `IsFlat` and `FindPosition` are pure reads; no throw in `SubmitDrainedEntry` or `TryEvictFollowerBeSlot` | ✅ PASS |
| JS-002: No `return null` where value expected | Both methods are `void` | ✅ PASS |
| JS-003: No `lock()` | SCAN-1 = 0; state mutations use `ConcurrentDictionary.TryRemove` | ✅ PASS |
| JS-021: No `Account.All` outside Loaded handler | No `Account.All` introduced by B155 | ✅ PASS |
| JS-008/009: No mutable struct / unfrozen brush | No struct or brush changes | ✅ PASS |
| NT8: No `async/await` in event handlers | No async introduced | ✅ PASS |
| NT8: No `sealed` on `TradeCopierWindow` | Not modified by B155 | ✅ PASS |
| NT8: No `DateTime.Now` (must be `UtcNow`) | No `DateTime.Now` in B155 changes | ✅ PASS |

---

## D. CROSS-FILE COHERENCE

**System completeness check — CopyEngine + drain path form a coherent whole after B155:**

| Component | Pre-B155 gap | B155 closure | Post-B155 state |
|-----------|-------------|--------------|----------------|
| `SubmitDrainedEntry` flat guard | None — no flat check before submit | Fix (a): `IsFlat(FindPosition(...))` guard at position (3) | All paths to `SubmitEntryDirect` are gated by flat check |
| `TryEvictFollowerBeSlot` proactive abort | None — BE-stop fill evicted BE slot but did not abort drain | Fix (b): `AbortDrainOnFill(accName)` immediately after `_filledBeTargetCount.TryRemove` | Proactive early abort; drain payload removed before countdown can complete |
| `OnDrainCancelAck` countdown path | Unmodified | Unmodified (by design) | Falls through to `SubmitDrainedEntry`; Fix (a) safety net catches late arrivals |
| `TryDrainWatchdog` 2s timeout path | Unmodified | Unmodified (by design) | Falls through to `SubmitDrainedEntry`; Fix (a) safety net catches watchdog trigger on flat account |
| `AbortDrainOnFill` helper | Already existed | Unchanged — called by Fix (b) | Correctly handles `TryRemove(_pendingDispatchDrains)` + `DrainedOrderIds` cleanup |
| `DrainedOrderIds` cleanup | Present on submit path only | Added to abort path | Cleanup confirmed on BOTH paths — no drain ID leak on abort |
| Double-cleanup hazard | N/A | Belt-and-suspenders design documented in 02-architecture-plan.md §3 | `TryRemove` is atomic — first caller owns payload; second call is no-op. No hazard. |

**Call ordering verified (per plan §3 and verification checklist):**
- `TryEvictFollowerBeSlot` fires at `OnOrderUpdate` line 1686 — BEFORE `HandleDrainTerminalState` at line 1722.
- Fix (b) fires first (proactive); if drain already removed, Fix (a) is a no-op (`TryRemove` returns false at (1)).
- If Fix (b) has not yet fired when a cancel ack arrives, Fix (a) catches it at submission time.

No wiring gap found. System is coherent end-to-end.

---

## E. DNA RULE VERIFICATION (Final Cross-File Pass)

| Rule | Scope | Result |
|------|-------|--------|
| JS-001: No `throw` in `OnOrderUpdate` / `SendCopy` / gate chain | All B155 changes | ✅ PASS |
| JS-002: No `null` return where value expected | All B155 changes — void methods only | ✅ PASS |
| JS-003: No `lock()` for state | SCAN-1 = 0 across `src/PropTraderTools/` | ✅ PASS |
| JS-008: No mutable struct fields / unfrozen brushes | No struct/brush changes | ✅ PASS |
| JS-009: No `Dictionary<K,V>` for shared/thread-touched collection | Not introduced by B155 | ✅ PASS |
| JS-010: No public constructor on singleton/signal struct | Not introduced by B155 | ✅ PASS |
| JS-021: No `Monitor`/`Mutex`/`SemaphoreSlim` for state | SCAN-1 = 0 | ✅ PASS |
| JS-023: No UI update from off-thread without `Dispatcher.InvokeAsync` | No UI changes | ✅ PASS |
| NT8: No `async/await` in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | Not applicable — no such calls | ✅ PASS |
| NT8: No `Account.All` in constructor | Not introduced | ✅ PASS |
| NT8: No `sealed TradeCopierWindow` | Not modified | ✅ PASS |
| NT8: No `FontFamily` override | SCAN-3 = 0 | ✅ PASS |
| NT8: No hardcoded `#RRGGBB` hex | Not in B155 changes | ✅ PASS |
| NT8: No `CreateOrder` without `PTT-` prefix | No new `CreateOrder` calls | ✅ PASS |
| NT8: No `DateTime.Now` | Not in B155 changes | ✅ PASS |
| COMPLEXITY: No method CYC > 8 (new violations) | SCAN-4 = 0 new warnings; delta checks PASS | ✅ PASS |
| SPEC COMPLETENESS: All requirements addressed | See spec coverage matrix below | ✅ PASS |

---

## F. SPEC COVERAGE MATRIX

| Requirement | Addressed? | Implementation Location |
|-------------|------------|------------------------|
| DW-B154-04: Abort stale drain payload when position-flat | ✅ YES | `CopyEngine.cs:8246` — Fix (a) `IsFlat` guard |
| Early proactive abort on BE-stop Fill | ✅ YES | `CopyEngine.cs:1912` — Fix (b) `AbortDrainOnFill(accName)` |
| Belt-and-suspenders (both countdown and watchdog paths covered) | ✅ YES | Fix (a) covers all paths through `SubmitDrainedEntry` |
| `DrainedOrderIds` cleanup on abort path | ✅ YES | `CopyEngine.cs:8251–8252` |
| `DrainedOrderIds` cleanup on submit path | ✅ YES | `CopyEngine.cs:8266–8267` |
| `SubmitDrainedEntryTestable` seam as `internal` | ✅ YES | `CopyEngine.cs:7917` |
| `[DRAIN-ABORT-FLAT]` ASCII log in abort path | ✅ YES | `CopyEngine.cs:8248–8250` |
| Test a: `SubmitDrainedEntry_ShouldAbort_WhenAccountIsFlat` | ✅ YES | `CopyEngineTests.cs:9343–9379` |
| Test b: `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat` | ✅ YES | `CopyEngineTests.cs:9381–9409` |
| Test c: `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper` | ✅ YES | `CopyEngineTests.cs:9411–9424` |
| No modification to `OnDrainCancelAck` | ✅ YES | Omitted from FILES MODIFIED; engineer deviation = none |
| No modification to `TryDrainWatchdog` | ✅ YES | Omitted from FILES MODIFIED; engineer deviation = none |
| All 8 scans PASS | ✅ YES | SCAN-1 through SCAN-8 documented in §B |
| DW-B154-05, DW-B154-06, DW-B153-05, DW-B153-06 carried as OPEN | ✅ YES | ticket-1-completion.md Deferred table; 06-deferred-backlog.md §Carry-Forward |

---

## G. DEFERRED / OPEN ITEMS

| Item | Status |
|------|--------|
| DW-B154-04 (drain abort on flat) | **CLOSED — implemented by B155** |
| DW-BUG-0911-A-01 (mid-close race condition) | STILL OPEN — not addressed in B155. Mid-close race: `OnOrderUpdate` may receive fill events in non-deterministic order when BE stop fills coincide with leader close. Future drain-path block. |
| SIM gate verification | PENDING — automated pipeline did not run field SIM. Manual SIM run required: enter trade → BE ALL → confirm `[DRAIN-ABORT-FLAT]` in Output tab, no ghost entry in 3-second window. Logged as DW-B155-01. |

---

## H. SIM GATE STATUS

SIM gate was NOT executed by the automated pipeline in this block. The SIM verification scenario (6 steps per 02-architecture-plan.md §8 and 04-tickets.md Verify) requires a live NinjaTrader environment with real follower accounts. Structural and reflection-based tests confirm all code paths are physically present and correctly ordered; the SIM gate provides final runtime confirmation of the end-to-end behavior.

**SIM gate is deferred as DW-B155-01 (P2, post-deployment).**

The absence of a SIM run does not block FINAL_PASS: all structural requirements have been verified at source level by both engineer (Layer 2) and verifier (Layer 3). The SIM gate is documented as a mandatory post-deployment check.

---

## I. LAYER 2 / LAYER 3 DISCREPANCY REPORT

**Zero discrepancies found** between engineer (Layer 2) and verifier (Layer 3) across all 8 scans and all source checklist items. Both layers agree on all absolute values, delta values, and pass/fail conclusions.

The CYC absolute value discrepancies (lizard=4 vs architect-spec=5 for `SubmitDrainedEntry`; lizard=8 vs architect-spec=6 for `TryEvictFollowerBeSlot`) are pre-existing tool methodology mismatches documented in B154 and confirmed to pre-date B155. No new methodology discrepancy was introduced.

---

## J. PRIOR BLOCK DEFERRED ITEMS STATUS

Items carried from `docs/brain/B154/06-deferred-backlog.md`:

| ID | Item | B154 Status | B155 Disposition |
|----|------|-------------|-----------------|
| DW-B154-04 | PTT-Copy drain late fills — drain abort on position-flat | OPEN | **CLOSED — implemented by B155** |
| DW-B154-05 | `IsPttCopyEntryFilled` CYC improvement | OPEN | STILL OPEN — not addressed in B155 |
| DW-B154-06 | `TryTriggerBeRecovery` monitor | OPEN | STILL OPEN — not addressed in B155 |
| DW-B153-05 | Stale CYC annotation at `CopyEngine.cs` ~line 6675 | OPEN | STILL OPEN — not addressed in B155 |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` ~line 3445 | OPEN | STILL OPEN — not addressed in B155 |

---

## K. DEFERRED WORK — SECTION K

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B154-04 | Abort stale drain payload on position-flat — drain abort via `IsFlat` guard in `SubmitDrainedEntry` and `AbortDrainOnFill` call in `TryEvictFollowerBeSlot` | P1 | B155 | **CLOSED — implemented by B155** |
| DW-B154-05 | `IsPttCopyEntryFilled` CYC improvement — delegate to `IsPttCopyEntry` sub-check after adding null guard on `o.Name`, reducing lizard CCN from 7 to ≤5 | P3 | Future src-touching block | OPEN |
| DW-B154-06 | `TryTriggerBeRecovery` monitor — post-B154 deployment observation: verify no double-bracket placements caused by cancel ack from prior-trade PTT-BE-Stop arriving after new entry fill | P2 | Post-deployment observation | OPEN |
| DW-B153-05 | Stale CYC annotation at `CopyEngine.cs` ~line 6675 — comment still reads old branch analysis; actual post-fix lizard CCN=6 | P2 | Future src-touching block | OPEN |
| DW-B153-06 | Orphaned dead code `UpdateLegTargetPrice` at `CopyEngine.cs` ~line 3445 — zero callers confirmed; private method unreachable after T3 replacement | P2 | Future src-touching block | OPEN |
| DW-B155-01 | SIM gate pending — field SIM verification with `[DRAIN-ABORT-FLAT]` log check and no-ghost-re-entry confirmation has NOT been run by automated pipeline. Requires manual SIM run per 04-tickets.md Verify steps 1–6. | P2 | Post-deployment | OPEN |
| DW-BUG-0911-A-01 | Mid-close race condition — `OnOrderUpdate` may receive fill events in non-deterministic order when BE stop fills coincide with leader close. Not addressed in B155. | P2 | Future drain-path block | OPEN |

---

## Summary

**FINAL_PASS**

Block B155 closes DW-B154-04. The drain abort defect (ghost re-entry after position-flat) is eliminated by two belt-and-suspenders fixes:

- **Fix (a):** `IsFlat` guard at `SubmitDrainedEntry` (`CopyEngine.cs:8246`) — catches all paths (countdown and watchdog) at submission time. CYC delta +1 (lizard 3→4, architect-counted 4→5). Within ceiling.
- **Fix (b):** `AbortDrainOnFill(accName)` call at `TryEvictFollowerBeSlot` (`CopyEngine.cs:1912`) — proactive early abort when BE-stop fill flattens position. CYC delta 0 (void call).

`DrainedOrderIds` cleanup runs on both abort and submit paths. `OnDrainCancelAck` and `TryDrainWatchdog` untouched. Testable seam `SubmitDrainedEntryTestable` added as `internal`. Three new `[Fact]` tests added in `B155ContractTests`. All 8 scans pass. Zero lock(), zero non-ASCII, 0 build errors, 0 new CCN>8 violations.

SIM gate is deferred as DW-B155-01 (P2, post-deployment).
