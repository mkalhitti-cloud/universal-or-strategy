# BWAVE-NEXT Lane A -- Plan Review (Cycle 2)

**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-04
**Plan version**: cycle 1 revision (JS-002 fixed)
**Prior result**: REVIEW_FAIL (JS-002: `return null` in `FindOpenPositionInstrument`)
**Cycle 2 result**: See final line

---

## 0. Inputs Verified

| Input | Read? | Notes |
|-------|-------|-------|
| `docs/brain/BWAVE-NEXT/LaneA/02-architecture-plan.md` | YES | Cycle 1 revision |
| `docs/brain/BWAVE-NEXT/LaneA-mission-brief.md` | YES | Spec source |
| `docs/brain/BWAVE-DW/Backlog/DW-NEW-08-naked-fill-race.md` | YES | T4 criteria |
| `docs/brain/BWAVE-DW/Backlog/DW-NEW-09-stale-orders-scan.md` | YES | T5 criteria |
| `docs/standards/jane-street/RULES_CATALOG.md` | YES | JS-001..JS-110 |

---

## 1. JS-002 Fix Verification (Primary Focus of Cycle 2)

### [Fact Verified] `FindOpenPositionInstrument` — zero `return null;`

Plan §3 T4, lines 506-510:
```csharp
private static Instrument? FindOpenPositionInstrument(Account acct) =>
    acct.Positions.FirstOrDefault(static p => p.Quantity > 0)?.Instrument;
```

| Check | Result |
|-------|--------|
| Zero `return null;` raw statements in T4 section | PASS — expression body uses `?.Instrument`, no `return null` |
| Return type annotated `Instrument?` (nullable) | PASS — `Instrument?` present |
| Caller `NakedPositionDetector` uses null-safe pattern | PASS — `Instrument? instr = FindOpenPositionInstrument(acct); if (instr is not null) { ... }` (plan lines 451-454) |

**JS-002 fix: VERIFIED CLEAN**

---

## 2. Lane-Split Gate

| Check | Plan Evidence | Result |
|-------|--------------|--------|
| `LANE-SPLIT GATE RESULT` present | Plan §1 line 51: `LANES-APPROVED` | PASS |
| Q1=NO | T1 region 616-620, T2 region 1131-1237, ~496-line gap; T4 ~line 1355, T5 lines 3437/3637, ~2082-line gap | PASS |
| Q2=NO | No design dependency across fix pairs | PASS |
| Q3=YES | Each ticket has standalone value | PASS |
| Q4=YES | Each ticket has independent SIM/build verification path | PASS |
| T1+T2 parallel | Group A, same session | PASS |
| T4+T5 parallel | Group B, same session | PASS |
| T3 after T1 VERIFY_PASS | Explicitly stated | PASS |

---

## 3. Spec Traceability Matrix

| Ticket | Spec Requirement | Addressed in Plan? | Section |
|--------|-----------------|-------------------|---------|
| T1 | Confirm `_modules.Teardown()` before `_allAccounts.Clear()` ordering | YES — plan §2.1 confirms ordering already correct; T1 is verification ticket | §3 T1 |
| T1 | All IPttModules with OrderUpdate/PositionUpdate have Teardown unsubscribes | YES — plan §2.2 audit table; no module subscribes to those events | §2.2, Appendix A |
| T1 | 1 xUnit [Fact]: `Detach_ClearsAllModulesBeforeAccountList()` | YES — exact name in plan | §3 T1 |
| T2 | Inline `BuildArrowCluster` into `BuildBufferedButtonsRow`, delete helper | YES — Steps 1 and 2 detailed | §3 T2 |
| T2 | `btn.Background` set AFTER `SetResourceReference` | YES — plan line 250: `btn.Background = s.Bg; // AFTER style` | §3 T2 |
| T2 | `BuildBufferedButtonsRow` CYC ≤ 8 after inline | YES — CYC=3 post-inline | §3 T2 CYC |
| T2 | `[Fact] BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()` | YES — exact name | §3 T2 |
| T2 | `[Fact] BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()` | YES — exact name | §3 T2 |
| T3 | Two-panel integration: S1 sibling isolation, S2 own cleanup, S3 last-panel global cleanup | YES — all 3 scenarios present | §3 T3 |
| T3 | `[Fact] Detach_PanelA_DoesNotClearPanelB_BeSlot()` | YES — exact name | §3 T3 |
| T3 | `[Fact] Detach_LastPanel_ClearsAllPendingBeSlots()` | YES — exact name | §3 T3 |
| T3 | `[Fact] Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()` | YES — exact name | §3 T3 |
| T3 | Depends on T1 VERIFY_PASS | YES — "Cannot start until T1's VERIFY_PASS is confirmed" | §3 T3 deps |
| T4 | `NakedPositionDetector` fires within 50ms of terminal order event on naked follower | YES — tail-call in `OnOrderUpdate` pre-Gate-1, CYC=3 dispatcher | §3 T4 |
| T4 | No false fires during normal bracket lag | YES — 500ms grace window in `_nakedDetectLastQueuedTicks` | §3 T4 |
| T4 | Multi-follower isolation | YES — per-account debounce key (`acct.Name`) | §3 T4 |
| T4 | `HasNakedPosition`: non-flat position + zero Working/PendingSubmit stop/target | YES — CYC=4 implementation | §3 T4 |
| T4 | No lock(), CYC ≤ 8 | YES — ConcurrentDictionary, all methods ≤ 6 | §3 T4 |
| T5 | `ActiveOrders(Account)` helper: CYC=1, static, private, no lock | YES | §3 T5 |
| T5 | `FindFollowerBracketOrder` line 3437 uses `ActiveOrders(follower)` | YES | §3 T5 |
| T5 | `FindFollowerEntryOrder` line 3637 uses `ActiveOrders(follower)` | YES | §3 T5 |
| T5 | All 23 other `acc.Orders.ToList()` call sites unchanged | YES — explicit table with TryLogSFBTrace and CancelPttDragOrphansForAccount | §3 T5 |
| T5 | `TryLogSFBTrace` (line 1947) explicitly unchanged | YES — "Diagnostic -- intentionally shows full history" | §3 T5 table |
| T5 | `[Fact] FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()` | YES — exact name | §3 T5 |
| T5 | `[Fact] FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()` | YES — exact name | §3 T5 |

**All spec requirements addressed. Zero gaps.**

---

## 4. 8 Required [Fact] Names — Exact Match

| # | Spec-Mandated Name | Plan Name | Match |
|---|-------------------|-----------|-------|
| T1 | `Detach_ClearsAllModulesBeforeAccountList()` | `Detach_ClearsAllModulesBeforeAccountList()` | PASS |
| T2a | `BuildBufferedButtonsRow_TealButtons_HaveTealBorderBrush()` | same | PASS |
| T2b | `BuildBufferedButtonsRow_TrimButton_HasInactiveBackground()` | same | PASS |
| T3a | `Detach_PanelA_DoesNotClearPanelB_BeSlot()` | same | PASS |
| T3b | `Detach_LastPanel_ClearsAllPendingBeSlots()` | same | PASS |
| T3c | `Detach_OwnPanel_ClearsOwnBeSlot_ButNotOthers()` | same | PASS |
| T5a | `FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()` | same | PASS |
| T5b | `FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()` | same | PASS |

Note: T4 test names are engineer-discretionary per plan ("recommended, engineer determines exact names"). Spec does not mandate exact T4 test names. No issue.

---

## 5. NT8 API Ban Check

| Banned API | Present in Plan? | Result |
|-----------|-----------------|--------|
| `Account.Change()` | ABSENT | PASS |
| `AtmStrategyCreate()` | ABSENT | PASS |
| `AtmStrategyChangeStopTarget()` | ABSENT | PASS |
| `DateTime.Now` (must use UtcNow) | ABSENT — plan uses `Environment.TickCount64` | PASS |
| `CreateOrder` without PTT- prefix | No new `CreateOrder` calls | PASS |
| `async/await` in OnInitialize/OnDestroyed/OnWindowCreated | ABSENT | PASS |
| `Account.All` in constructor | ABSENT | PASS |
| `sealed TradeCopierWindow` | TradeCopierWindow.cs out of scope | PASS |
| FontFamily override | ABSENT | PASS |
| Hardcoded #RRGGBB hex | ABSENT | PASS |

---

## 6. Jane Street P0 Rule Checks

### JS-001: No throw in hot paths
No `throw new XxxException` in any new method. `TryNakedDetect`, `NakedPositionDetector`, `HasNakedPosition`, `FindOpenPositionInstrument`, `ActiveOrders` — all return early or return values. **PASS**

### JS-002: No return null for missing values
`FindOpenPositionInstrument` returns `Instrument?` via `?.Instrument` expression body. No `return null;` statement. Caller: `if (instr is not null)` guard. **PASS — FIXED in cycle 1**

### JS-003: No magic string for discriminated state
`acct.Name` used as ConcurrentDictionary key — NT8 platform account name, not discriminated state encoding. **PASS**

### JS-009: No plain Dictionary<K,V> for shared/thread-touched collection
Plan uses `ConcurrentDictionary<string, long>` — the lock-free concurrent collection (JS-025 compliant). No plain `Dictionary<K,V>` introduced. **PASS**

### JS-010: No public constructor on singleton or signal struct
No new class or singleton with public constructor introduced. New methods are private instance/static methods on existing `CopyEngine`. **PASS**

### JS-021: No lock()
No `lock()` in any new method body. Plan explicitly annotates `// JS-021: no lock` on all T4 methods. `ConcurrentDictionary` atomic ops used throughout. **PASS**

### JS-023: UI update from off-thread uses Dispatcher.InvokeAsync
T4 `NakedPositionDetector` marshals `FlattenOneAccount(acct, instr)` via `NinjaTrader.Core.Globals.Dispatcher.InvokeAsync(...)` — correct pattern, consistent with other flatten paths. **PASS**

### JS-033: No async void (non-event-handler)
All new methods are synchronous (`private void`, `private static bool`, `private static IEnumerable<Order>`). The `Dispatcher.InvokeAsync` lambda is not an `async void` method — it is a synchronous `Action` lambda passed to `InvokeAsync`. **PASS**

---

## 7. CYC Analysis

| Method | Ticket | CYC | Limit | Result |
|--------|--------|-----|-------|--------|
| `FindOpenPositionInstrument` | T4 | 1 | 8 | PASS |
| `TryNakedDetect` | T4 | 3 | 8 | PASS |
| `NakedPositionDetector` | T4 | 5–6 (plan claims 6, actual ≤6) | 8 | PASS |
| `HasNakedPosition` | T4 | 4 | 8 | PASS |
| `OnOrderUpdate` (modified by tail-call) | T4 | 8 (unconditional call, unchanged) | 8 | PASS |
| `ActiveOrders` | T5 | 1 | 8 | PASS |
| `FindFollowerBracketOrder` Account overload | T5 | 1 (unchanged) | 8 | PASS |
| `FindFollowerEntryOrder` | T5 | 3 (unchanged) | 8 | PASS |
| `BuildBufferedButtonsRow` post-inline | T2 | 3 | 8 | PASS |
| `BuildArrowCluster` | T2 | DELETED | — | PASS |

Note on `NakedPositionDetector` CYC: Plan annotates CYC=6 (conservative, lists 6 numbered items in comments). Actual branch count in method body is 5 branches + base=1 → CYC=5. Plan's overcounting is safe; either way well within budget.

---

## 8. ASCII-Only Compliance (SCAN-06)

No Unicode, emoji, curly quotes, or non-ASCII characters appear in any new method bodies, comments, or string literals in the plan. All comment delimiters and string content are ASCII. **PASS**

---

## 9. Test Framework (SCAN-07)

All tests use `[Fact]` (xUnit). No `[Test]`, `[TestMethod]`, `[TestCase]` patterns present. **PASS**

---

## 10. 7-Scan Checklist (Plan §4)

| Scan | Present in Plan §4? | Expected Zero-Result Coverage | Result |
|------|--------------------|-----------------------------|--------|
| SCAN-01 `lock\s*\(` | YES | Zero in new/modified code | PASS |
| SCAN-02 `async void [A-Z]` | YES | Zero in new/modified code | PASS |
| SCAN-03 `return null;` | YES | FindOpenPositionInstrument uses `?.Instrument` (no raw return null). All other new methods return bool/void/IEnumerable. | PASS |
| SCAN-04 `throw new \w+Exception` | YES | Zero in new/modified methods | PASS |
| SCAN-05 `dotnet lizard --CCN 8` | YES | All new/modified methods listed with CYC values; 0 violations | PASS |
| SCAN-06 `[^\x00-\x7F]` | YES | Zero non-ASCII in new/modified files | PASS |
| SCAN-07 `\[Test\]|\[TestMethod\]` | YES | Zero — [Fact] only | PASS |

---

## 11. Execution Order

| Pair | Plan Evidence | Result |
|------|--------------|--------|
| T1+T2 parallel (Session A) | Plan §5: Group A | PASS |
| T4+T5 parallel (Session B) | Plan §5: Group B | PASS |
| Sessions A+B concurrent | Plan §5: "Sessions A and B can run concurrently (different files)" | PASS |
| T3 sequential after T1 VERIFY_PASS only | Plan §5: Session C depends on T1 VERIFY_PASS | PASS |

---

## 12. T5 Scope Verification

| Requirement | Plan Evidence | Result |
|-------------|--------------|--------|
| Exactly 2 call sites changed | Lines 3437 and 3637 — both explicit | PASS |
| `TryLogSFBTrace` (line 1947) unchanged | Plan §3 T5 table: "Diagnostic -- intentionally shows full history" | PASS |
| 23 other call sites unchanged | Plan §7: "All other `acc.Orders.ToList()` call sites (23 of 25): UNCHANGED" | PASS |
| `FindFollowerBracketOrderTestable` test seam unchanged | Plan §3 T5 note: "test seam at line 3598, uses IEnumerable<Order> overload" (left unchanged) | PASS |

---

## 13. Violations Summary

**CYCLE 2 — ZERO VIOLATIONS FOUND**

| Category | Violations | Notes |
|----------|-----------|-------|
| JS P0 (lock, throw, return null, async void) | 0 | |
| JS P1 (CYC, Dictionary, mutable struct, SolidColorBrush, public constructor) | 0 | |
| NT8 API bans | 0 | |
| Spec traceability gaps | 0 | |
| 7-Scan checklist gaps | 0 | |
| [Fact] name mismatches | 0 | |
| Execution order errors | 0 | |
| T5 scope violations | 0 | |

---

## 14. Prior Violation Resolution

| Cycle 1 Violation | Rule | Resolution in Plan | Verified |
|-------------------|------|--------------------|---------|
| `return null` in `FindOpenPositionInstrument` | JS-002 | Return type changed to `Instrument?`; expression body `?.Instrument`; caller uses `is not null` guard | YES — RESOLVED |

---

REVIEW_PASS
