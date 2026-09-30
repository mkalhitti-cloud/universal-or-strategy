# B129 LaneB Final Review — DW-B134

**Block**: B129 LaneB
**Defect**: DW-B134 — ATM Bracket Drag Not Synced to Followers
**Phase**: 5 (Final Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-31
**Inputs read**:
- `docs/brain/B129/LaneB-02-architecture-plan.md`
- `docs/brain/B129/LaneB-04-ticket-review.md` — TICKET_REVIEW_PASS
- `docs/brain/B129/LaneB-ticket-2-completion.md` — BUILD_PASS
- `docs/brain/B129/LaneB-ticket-2-verification.md` — VERIFY_PASS
- `docs/brain/B129/06-deferred-backlog.md` — LaneA carry-forward (READ ONLY)
- `src/PropTraderTools/CopyEngine.cs` — READ ONLY (lines verified below)
- `docs/standards/jane-street/RULES_CATALOG.md` — P0 rules

---

## FK-1: Build Clean

**PASS**

Verification report SCAN-07 (Layer 3 independent):
- `dotnet build src/PropTraderTools --no-incremental` → **Build succeeded. 0 Warning(s). 0 Error(s).**
- `dotnet test ... --filter "FullyQualifiedName~B129" --no-build` → **Failed: 0, Passed: 8, Skipped: 0. Total: 8.**

Tests confirmed passing:
- 5 pre-existing B128-range tests matched by filter (non-regression)
- `B129Tests.B129_DW134_STPSuffixDetectedByIsBracketLegStatic` — PASS
- `B129Tests.B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` — PASS
- `B129Tests.B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` — PASS

---

## FK-2: Layer 2 vs Layer 3 Agreement

**PASS**

Verification report documents zero discrepancies across all 7 scans:

| Scan | Layer 2 | Layer 3 | Match |
|------|---------|---------|-------|
| SCAN-01 (lock) | 0 live hits | 0 hits | MATCH |
| SCAN-02 (async void) | 0 hits | 0 hits | MATCH |
| SCAN-03 (return null, new methods) | 0 hits in new methods | 0 hits in L2025-2160 | MATCH |
| SCAN-04 (throw new) | 0 hits | 0 hits | MATCH |
| SCAN-05 (PTT-STP-Drag) | 1 hit at L2143 | 1 hit at L2143 | MATCH |
| SCAN-06 (IsTrailingStop ordering) | L2067 < L2073 | L2067 < L2073 | MATCH |
| SCAN-07 (build + tests) | 8 passed, 0 failed | 8 passed, 0 failed | MATCH |

No discrepancies found between Layer 2 (engineer self-report) and Layer 3 (independent verification).

---

## FK-3: IsBracketLegStatic STP Clause Coherence

**PASS**

**IsStopLeg (L3599)** — confirmed in source: already has the `EndsWith("STP", StringComparison.OrdinalIgnoreCase)` clause, added in B25, referencing the "12s Buy STP" ATM format:
```
src/PropTraderTools/CopyEngine.cs:3605  || (order.Name != null
                                             && order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase))
```

**IsBracketLegStatic (L3612)** — confirmed in source: STP clause added at L3621 as the 4th `||` branch inside the null-guarded Name block, exactly mirroring `IsStopLeg`:
```
src/PropTraderTools/CopyEngine.cs:3621  || order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
```
Comment at L3610-3611 explicitly documents the mirror relationship.

**IsBracketLeg (instance, L3631)** — confirmed in source: NOT touched. The instance method contains only `StartsWith("Stop")` and `StartsWith("Target")` clauses. No STP clause. This is correct per plan: `IsBracketLeg` is used exclusively by `CancelOneAccount`, not the drag path. No scope creep.

---

## FK-4: Branch Ordering Correctness

**PASS**

Confirmed in source (`CopyEngine.cs` L2043–2077):

- ATM STP branch: L2067 — `if (isStop && IsAtmSTPOrder(fo)) // (3) DW-B134`
- IsTrailingStop guard: L2073 — `if (isStop && IsTrailingStop(fo)) // (4)`

L2067 < L2073: ATM STP fires **before** the IsTrailingStop guard. Correct ordering confirmed.

`IsTrailingStop` at L2018 is still present and unchanged (definition: `return order.OrderType == OrderType.StopMarket`). The guard at L2073 remains in the method body (not removed). Existing trailing-stop skip behavior is preserved for non-ATM StopMarket orders.

---

## FK-5: OQ-03 Resolution Confirmed in Code

**PASS**

OQ-03 SAFE comment confirmed at `CopyEngine.cs` L2111-2112:
```
// OQ-03: cancel of follower ATM bracket is SAFE -- Gate 2 (FindMatchingRule L1609)
//        returns null for follower account orders, blocking TryCancelFollowerEntries.
```

Safety chain confirmed in plan Section C and independently verified:
1. `acc.Cancel(fo)` fires `OrderUpdate` for the follower account (e.g., `Account.Name = "Sim102"`).
2. `FindMatchingRule` at L1609 compares `order.Account.Name` against `rule.MasterAccount?.Name`.
3. Follower account ("Sim102") never equals the master account name ("Sim101") → `FindMatchingRule` returns `null`.
4. Gate 2 at L1348-1350: `if (matchedRule == null ...) return;` → **immediate return**. Lines 1353–1641 never reached.
5. `TryCancelFollowerEntries` and `CancelOneAccount` are never called.

Gate 2 null-return path (Block B `newStop == null` guard at L2147) is separate: if `acc.CreateOrder` returns null, execution returns with a `StatusUpdate` log — no PTT-STP-Drag order cascade risk.

---

## FK-6: NT8-014 PTT- Prefix Compliance

**PASS**

Confirmed in source: `"PTT-STP-Drag"` appears at exactly **1 location** — `CopyEngine.cs:L2143`, inside `SyncAtmFollowerBracket` Block B as the `name` argument to `acc.CreateOrder(...)`.

SCAN-05 (Layer 2) and SCAN-05 (Layer 3) both report: 1 hit at L2143. No additional hits elsewhere in the file. NT8-014 (PTT- prefix requirement) satisfied.

---

## FK-7: P0 Jane Street Rule Compliance

**PASS**

| Rule | Check | Source Evidence | Result |
|------|-------|-----------------|--------|
| JS-021 — no `lock()` | SCAN-01: 0 live hits (both layers) | No `lock(` in any new or modified code | PASS |
| JS-001 — no throw in hot paths | SCAN-04: 0 `throw new` hits; two independent try/catch, both catches log via `StatusUpdate?.Invoke()` only; no rethrow | L2121-2128 (Block A), L2131-2158 (Block B) | PASS |
| JS-002 — no `return null` in new methods | `IsAtmSTPOrder` returns `bool`; `SyncAtmFollowerBracket` returns `void`; SCAN-03: 0 hits in L2025-2160 | L2028-2030, L2113 | PASS |
| JS-033 — no `async void` | SCAN-02: 0 hits in CopyEngine.cs | All new methods synchronous | PASS |
| ASCII-only | "PTT-STP-Drag" (L2143), log strings ("STP cancel error", "STP create error", "ATM STP resubmit -> ", "ATM STP CreateOrder returned null") all ASCII; SCAN-04 (Layer 3): no non-ASCII confirmed | L2127, L2143, L2149, L2153, L2157 | PASS |

No JS P0 violations in new or modified code.

---

## FK-8: Carry-Forward Items Unaffected by LaneB Changes

**PASS**

LaneB scope is confined to:
1. `src/PropTraderTools/CopyEngine.cs` — 4 edits: `IsBracketLegStatic`, `IsAtmSTPOrder` (new), `SyncFollowerBracket`, `SyncAtmFollowerBracket` (new).
2. `src/PropTraderTools/Tests/B129Tests.cs` — new file, 3 `[Fact]` tests.
3. `src/PropTraderTools/PropTraderTools.csproj` — 1 `<Compile>` entry added.

Carry-forward items reviewed for intersection with LaneB changes:

| Item | Scope | Intersects LaneB? |
|------|-------|-------------------|
| DW-B129-01 | Quick2t/QAll2t SIM gate (TradeCopierWindow, TradeCopierPanel) | NO |
| DW-B133 | PttGlobalQuickExit forced 2-target count | NO |
| DW-B124-01/02 | OnGlobalBeClick disarm behavior / test assertion | NO |
| DW-B107 | MoveStopToBreakEven Step A stale snapshot | NO — different method |
| B107-DEFER-01 | F5 NT8 compilation gate | NO |
| B107-DEFER-02 | Combo C live re-test | NO |
| DW-B42-01/02/03 | IsPttQxTarget, live F5, range extension | NO |
| DW-PTT-BE-FIX-01/02/03 | BE lazy re-resolve, SIM gate PATH B, test build errors | NO |
| DW-B89-DEFERRED-01..06 | Ctrl+F5, SIM PATH A/B, spec update | NO |

None of the 18 carry-forward items reference `IsBracketLegStatic`, `SyncFollowerBracket`, `IsAtmSTPOrder`, `SyncAtmFollowerBracket`, or ATM STP bracket handling. LaneB changes do not affect any carry-forward item.

---

## Section K — Deferred Work Register

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B134-OCO | OCO Orphan Risk After ATM STP Cancel+Resubmit | P2 | B130 or first SIM gate session after DW-B134 sync | OPEN |

**DW-B134-OCO description**: When `SyncAtmFollowerBracket` cancels the follower's ATM STP bracket and resubmits as a standalone "PTT-STP-Drag" `StopMarket` order, the new stop is **not part of the original ATM OCO pair**. NT8's ATM engine manages the stop and target as an OCO group; cancelling the original "Buy STP" may also cancel the paired OCO target bracket (or leave it orphaned depending on ATM OCO cancellation behavior). The new "PTT-STP-Drag" stop is submitted without an OCO partner, meaning: if the original ATM target fills, the ATM engine cancels the OCO partner — but the OCO partner is now the already-cancelled original "Buy STP", not "PTT-STP-Drag". The new stop may remain working after target fill, creating an orphaned stop position.

**Fix**: Investigate NT8 ATM OCO behavior on `acc.Cancel` — determine whether OCO partner is auto-cancelled at cancel time. If yes: add paired `CreateOrder` for the target bracket in Block B of `SyncAtmFollowerBracket`. If no: add stop-cleanup logic triggered on target fill detection in `OnOrderUpdate`.

**Prerequisite**: Director SIM gate to observe actual NT8 OCO behavior when `acc.Cancel` is called on an ATM bracket in an active ATM strategy. This gates the fix design.

**Deferred to**: B130 or first SIM gate session after DW-B134 sync.

---

## Cross-File Coherence Assessment

LaneB implements a 3-layer fix to a single root-cause chain:

1. **Layer 1** (`IsBracketLegStatic`) — gate fix enabling ATM STP orders to reach the drag path.
2. **Layer 2+3** (`SyncFollowerBracket` routing + `SyncAtmFollowerBracket` cancel+resubmit) — correct update mechanism bypassing NT8 ATM engine's silent `acc.Change()` no-op.

The three layers form a coherent end-to-end fix. Each layer addresses a distinct failure point in the cascade documented in the architecture plan (Section B). No cross-file pollution: `TradeCopierWindow.cs` and `TradeCopierPanel.cs` are unmodified. `CopyEngineTests.cs` and `B76Tests.cs` are unmodified. The fix is surgical and confined.

The two-independent-try/catch structure (TR-06 from TICKET_REVIEW) is correctly implemented in source (Block A at L2121, Block B at L2131) — naked-position risk from a Cancel exception is eliminated. The `acc.Submit(new[] { newStop })` API call at L2152 matches the established pattern in the codebase (L1089, L2327, L2802), not the incorrect `newStop?.Submit()` form that caused the CS1061 build failure during development.

OQ-03 (cascade safety) is verified by code inspection (Gate 2 null-return at `FindMatchingRule` L1609) and by a passing xUnit test (`B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel`).

The one open risk (`DW-B134-OCO`) is documented, deferred, and does not affect the correctness of the primary fix for the reported defect.

---

## Overall Verdict

**FINAL_PASS**

| Check | Result |
|-------|--------|
| FK-1: Build clean (0 errors, 0 warnings, 8/8 tests pass) | PASS |
| FK-2: Layer 2 vs Layer 3 agreement (0 discrepancies) | PASS |
| FK-3: IsBracketLegStatic STP clause mirrors IsStopLeg; IsBracketLeg instance untouched | PASS |
| FK-4: ATM STP branch at L2067 before IsTrailingStop at L2073; guard not removed | PASS |
| FK-5: OQ-03 SAFE comment at L2111-2112; Gate 2 null-return path confirmed | PASS |
| FK-6: "PTT-STP-Drag" at L2143; exactly 1 hit in SyncAtmFollowerBracket | PASS |
| FK-7: JS-021, JS-001, JS-002, JS-033, ASCII-only — all PASS; 0 P0 violations | PASS |
| FK-8: 18 carry-forward items unaffected by LaneB scope | PASS |
| Section K present | YES |
| LaneB-06-deferred-backlog.md written | YES |

**FINAL_PASS**

*Final review written by ptt-plan-reviewer. Phase 5 complete.*
*DW-B134 primary fix verified. DW-B134-OCO deferred to B130.*
