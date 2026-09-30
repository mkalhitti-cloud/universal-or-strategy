# B69-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer
**Reviewed**: 2026-08-13
**Phase**: 2 (Plan Review)
**Source plan**: `docs/brain/B69-LaneA/02-architecture-plan.md`
**Target file**: `src/PropTraderTools/CopyEngine.cs`

---

## VERDICT: REVIEW_PASS

No violations found. All spec requirements addressed. All JS-DNA rules respected.

---

## Section A — Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B69-01: New `CancelAllAccountOrders` helper (name-agnostic, cancels Working/Initialized/Submitted/Accepted/ChangeSubmitted) | YES | §3.1, §4 change #2 |
| DW-B69-01: `FlattenOneAccount` calls `CancelAllAccountOrders` (not `CancelQxBrackets`) | YES | §3.2, §4 change #4 |
| DW-B69-01: `FlattenOneAccount` calls `acc.Submit()` after `CreateOrder` | YES | §3.2, §4 change #5 |
| DW-B69-01: Remove stale "Also called by FlattenOneAccount" comment from `CancelQxBrackets` | YES | §3.6, §4 change #1 |
| DW-B69-01: `CYC(CancelAllAccountOrders) = 4` | YES | §3.1 — CYC breakdown table |
| DW-B69-01: `CYC(FlattenOneAccount)` stays 4 | YES | §3.2 — CYC annotation |
| DW-B69-02: `SubmitBeStop` line 512 — FullName comparison with null-guard | YES | §3.3, §4 change #6 |
| DW-B69-02: `FindPosition` line 1778 — FullName comparison with null-guard | YES | §3.4, §4 change #8 |
| DW-B69-02: `CYC(SubmitBeStop)` stays 7 | YES | §3.3 — "signature unchanged" with CYC=7 in §6 checklist |
| DW-B69-03: `_dedupCache[order.OrderId.ToString()] = newPrice` after `acc.Submit` inside `if (order != null)` | YES | §3.5, §4 change #7 |
| DW-B69-03: `CYC(HandleEntryChange)` stays 7 | YES | §3.5 — "CYC delta = 0" |
| 7 new `[Fact]` test methods (T_B69_01..T_B69_07) | YES | §5 — all 7 defined |
| Tests in `CopyEngineTests.cs` only | YES | §4 change #9, §9 |
| All code changes in `CopyEngine.cs` only | YES | §4 change map — 2 files only |

---

## Section B — JS-DNA Rule Violations

No violations found.

| Rule | Check | Finding |
|------|-------|---------|
| JS-021 — No `lock()` | `CancelAllAccountOrders` uses `FullName` + `acc.Cancel`; `HandleEntryChange` uses `ConcurrentDictionary` atomic write | PASS |
| JS-001 — No `throw` in new code | All new/modified methods use null-guard early-return or try/catch-swallow; no rethrow | PASS |
| JS-002 — No new `null` return sites | `FindPosition` retains pre-existing `return null` contract (addressed in §6 with JS-002 note); `CancelAllAccountOrders` is `void` | PASS |
| JS-033 — No `async void` | All new/modified methods are synchronous `void` | PASS |
| CYC <= 8 — All new/modified methods | Max CYC in plan = 4 (`CancelAllAccountOrders`); all other methods unchanged or annotated at CYC=7 or below | PASS |
| ASCII-only string literals | All literals in plan are ASCII; no Unicode, emoji, or curly quotes | PASS |
| NT8 SCAN-06 — No `DateTime.Now` | Plan explicitly notes `DateTime.MaxValue` unchanged in `CreateOrder` calls | PASS |
| NT8 SCAN-05 — PTT- prefix | `CancelAllAccountOrders` contains no `CreateOrder`; `FlattenOneAccount` retains `"PTT-Flatten"` | PASS |
| NT8 SCAN-03/04 — No FontFamily / hardcoded hex | Backend methods only; no UI | PASS |
| No `async/await` in NT8 lifecycle hooks | No lifecycle hook modification proposed | PASS |
| No `Account.All` in constructor | `CancelAllAccountOrders` is a plain helper, not a constructor | PASS |
| `Dictionary<K,V>` for thread-touched state — JS-009 | Plan explicitly states `_dedupCache` is `ConcurrentDictionary` (pre-existing) | PASS |

---

## Section C — DW Item Deep Review

### DW-B69-01 (P0) — Verified complete

**CancelAllAccountOrders:**
- States enumerated in plan §3.1: `Working | Initialized | Submitted | Accepted | ChangeSubmitted` — matches spec exactly.
- CYC=4 breakdown verified: null-guard (1), foreach (2), `stateOk` compound (3), FullName gate (4). Correct.
- No name filter — confirmed: plan makes no reference to `IsQxCancelCandidate` or any name predicate.
- Insertion point stated: after line 470 (end of `CancelQxBrackets` block). Confirmed: source shows `CancelQxBrackets` ends at line 470.

**FlattenOneAccount:**
- Line 1483 replacement: `CancelQxBrackets` → `CancelAllAccountOrders` — source confirms current code has `CancelQxBrackets` at line 1483. Change map correct.
- Submit fix: Plan §3.2 and change #5 specify `var order = acc.CreateOrder(...)` capture + `if (order != null) acc.Submit(new[] { order });`. Source confirms lines 1487-1490 currently call `acc.CreateOrder(...)` with no capture and no `Submit`. Fix is complete.
- CYC stays 4: plan annotates CYC=4 for modified body. The inner `if (order != null)` is inside the existing `try` branch and does not add a new outer branch. Assessment correct.

**Comment cleanup:**
- Plan §3.6 and change #1: delete line 450 `"// Also called by FlattenOneAccount (B67 DW-B67-01)..."`. Source confirms this exact comment exists at line 450. Change map correct.

### DW-B69-02 (P1) — Verified complete

**SubmitBeStop line 512:**
- Source confirmed: current code `if (p.Instrument == instr)` at line 512. Plan specifies `if (p.Instrument != null && p.Instrument.FullName == instr.FullName)`. Correct fix.
- CYC stays 7: the null-guard and `FullName` check replace one reference-equality check inside the existing foreach iteration — no new outer branch. Correct.

**FindPosition line 1778:**
- Source confirmed: current code `if (p.Instrument == instrument) return p;` at line 1778. Plan specifies `if (p.Instrument != null && p.Instrument.FullName == instrument.FullName) return p;`. Correct fix.
- Pre-existing `return null` on line 1779 untouched — not a new null-return site (JS-002 exempt per plan §6).

### DW-B69-03 (P1) — Verified complete

**HandleEntryChange:**
- Source confirmed: `if (order != null)` block at line 1127; `acc.Submit(new[] { order });` at line 1128; no `_dedupCache` write after this. Race window confirmed present.
- Plan §3.5 specifies insertion of `_dedupCache[order.OrderId.ToString()] = newPrice;` after `acc.Submit` inside the `if (order != null)` block, before `StatusUpdate?.Invoke`. Correct position.
- CYC delta = 0: addition is a straight-line assignment inside an existing branch, not a new branch itself. Correct.
- Stale comment at lines 1091-1093 ("New entry will be re-keyed by DispatchCopy on the follower's Accepted event") will be obsoleted by the fix. Plan §3.5 header acknowledges the stale comment. **Note for engineer**: plan does not explicitly list removal/update of this stale comment in the change map. This is cosmetic and non-blocking — the fix itself is structurally complete and the stale comment will no longer be accurate after the fix is applied. Engineer may optionally update the comment when implementing the fix.

---

## Section D — Test Plan Review

| Test ID | DW Coverage | Completeness |
|---------|-------------|--------------|
| T_B69_01 | DW-B69-01 | Verifies name-agnostic cancel (`PTT-Entry` order that `CancelQxBrackets` would skip) — PASS |
| T_B69_02 | DW-B69-01 | Verifies `ChangeSubmitted` state is cancelled — PASS |
| T_B69_03 | DW-B69-01 | Verifies `Filled` orders skipped — PASS |
| T_B69_04 | DW-B69-01 | Verifies instrument isolation (wrong FullName skipped) — PASS |
| T_B69_05 | DW-B69-02 | Verifies FullName-based position lookup across object-reference mismatch — PASS |
| T_B69_06 | DW-B69-03 | Verifies `_dedupCache` preload after resubmit — PASS |
| T_B69_07 | DW-B69-01 | Verifies null-guard on `CancelAllAccountOrders` — PASS |

All 7 tests: xUnit `[Fact]`, no NUnit/MSTest. Test framework mandate satisfied.

**Gap noted:** No test for `FindPosition` FullName fix in isolation (the `FindPosition` FullName fix is exercised transitively by T_B69_05 via `SubmitBeStop` but `FindPosition` is private and can only be tested indirectly). This is acceptable — `FindPosition` is a private helper, and T_B69_05 exercises the FullName path through `SubmitBeStop`. Non-blocking.

---

## Section E — Change Map Verification

| # | File | Change | Source Verified? |
|---|------|--------|-----------------|
| 1 | `CopyEngine.cs` line 450 | Delete stale FlattenOneAccount comment | YES — comment confirmed at line 450 |
| 2 | `CopyEngine.cs` after line 470 | Insert `CancelAllAccountOrders` | YES — line 470 is end of `CancelQxBrackets` block |
| 3 | `CopyEngine.cs` line 1473 | Update CYC comment | YES — line 1473 has old CYC comment referencing `CancelQxBrackets` |
| 4 | `CopyEngine.cs` line 1483 | Replace `CancelQxBrackets` → `CancelAllAccountOrders` | YES — `CancelQxBrackets(acc, instrument)` confirmed at line 1483 |
| 5 | `CopyEngine.cs` lines 1487-1490 | Capture `var order` + add `Submit` | YES — `acc.CreateOrder(...)` with no capture confirmed at lines 1487-1490 |
| 6 | `CopyEngine.cs` line 512 | FullName + null-guard in `SubmitBeStop` | YES — reference equality confirmed at line 512 |
| 7 | `CopyEngine.cs` lines 1127-1128 | Insert `_dedupCache` preload | YES — `if (order != null)` block confirmed at lines 1127-1128 |
| 8 | `CopyEngine.cs` line 1778 | FullName + null-guard in `FindPosition` | YES — reference equality confirmed at line 1778 |
| 9 | `CopyEngineTests.cs` after line 3553 | 7 `[Fact]` tests | N/A (insertion into test file) |

All 9 changes verified against source. Change map is accurate.

---

## Section F — File Scope Verification

- **Plan is plan-only**: No `.cs` files authored by architect. §9 explicitly states "No .cs files authored by ptt-architect — PASS — plan only."
- **Two-file boundary**: `CopyEngine.cs` (production) + `CopyEngineTests.cs` (tests). No other files.

---

## Section G — NT8 API Surface Verification

- `acc.Submit()` is required after `CreateOrder()` in NT8 AddOn API — confirmed by `NT8_FULL_REFERENCE.md` (plan §7, DW-B69-01 NT8 authority row). `SubmitBeStop` at lines 524-525 provides in-codebase precedent.
- `AtmStrategyCreate()` not used (StrategyBase-only restriction is not triggered here).
- `Account.All` not used in any new constructor.
- All `CreateOrder` calls use `PTT-` prefix.

---

## FINAL VERDICT: REVIEW_PASS

**Violations**: 0
**Warnings (non-blocking)**: 1
  - Stale comment at lines 1091-1093 in `HandleEntryChange` will become inaccurate after DW-B69-03 fix. Not in change map. Engineer may update at discretion; not a gate blocker.

**Gate**: OPEN — Phase 3 (ticket generation) is unlocked.
