# B183 Plan Review
# Reviewer: ptt-plan-reviewer (Phase 2)
# Input: docs/brain/B183/02-architecture-plan.md
# Sources: 00-orchestrator-prompt.md (spec), docs/standards/jane-street/RULES_CATALOG.md

**Date**: 2026-07-07
**Reviewer phase**: Ph2 (Plan Review)
**Plan status read**: PLAN_COMPLETE

---

## REVIEW_PASS: YES

> All checklist items PASS. No rule violations found. Plan is complete, coherent, and
> compliant with spec and Jane Street DNA. Proceed to Ph3 (ticket generation).

---

## Section A — LANE-SPLIT GATE Compliance

| Check | Result | Notes |
|-------|--------|-------|
| Gate result line present | **PASS** | "LANE-SPLIT GATE RESULT: LANES-APPROVED" present at plan line 32 |
| Q1 = NO | **PASS** | Confirmed: changes span 10+ sites across 5,951-line file |
| Q2 = NO | **PASS** | Confirmed: BUG3 (TradeCopierPanel) does not depend on BUG1+BUG2 fixes |
| Q3 = YES | **PASS** | Confirmed: each lane has standalone value |
| Q4 = YES | **PASS** | Confirmed: independent SIM paths documented in Section 9 |
| Lane assignments | **PASS** | Lane A = T1-T5 (CopyEngine), Lane B = T6-T7 (non-src + Panel) |

---

## Section B — Spec Coverage Matrix (16 Missing Symbols)

| Symbol | Group | Ticket | Plan Section | Addressed? |
|--------|-------|--------|-------------|-----------|
| `_followerSlateReady` | A1 | T1 | §5 T1 IP1 | **PASS** |
| `_laneDDispatched` | A1 | T1 | §5 T1 IP1 | **PASS** |
| `_laneEDispatched` | A1 | T1 | §5 T1 IP1 | **PASS** |
| `TryMarkFollowerSlateNotReady` | A2 | T1 | §5 T1 IP2 | **PASS** |
| `TrySetFollowerSlateReady` | A2 | T1 | §5 T1 IP2 | **PASS** |
| `IsAccountFlat` | A2 | T1 | §5 T1 IP2 | **PASS** |
| `HasAnyActiveOrders` | A2 | T1 | §5 T1 IP2 | **PASS** |
| `OnFollowerPositionUpdate` | A2 | T1 | §5 T1 IP2 | **PASS** |
| `TryMarkFollowerSlateNotReady` call site × 3 | A3 | T2 | §5 T2 IP1-3 | **PASS** |
| `FindFollowerBracketOrder` gate | A4 | T2 | §5 T2 IP4 | **PASS** |
| `PositionUpdate` subscribe | A5 | T2 | §5 T2 IP5 | **PASS** |
| `PositionUpdate` unsubscribe | A5 | T2 | §5 T2 IP6 | **PASS** |
| `FlatGuardLoop<TAccount>` | B1 | T5 | §5 T5 IP1 | **PASS** |
| `FlatGuardLoop` call sites × 2 | B2 | T5 | §5 T5 IP2-3 | **PASS** |
| `TryLaneDHook` | C2 | T3 | §5 T3 IP1 | **PASS** |
| `TryLaneDDispatchFollower` | C2 | T3 | §5 T3 IP1 | **PASS** |
| `TryLaneDHook` call in DispatchCopy | C3 | T3 | §5 T3 IP2 | **PASS** |
| Lane D skip gate in DispatchToFollower | C4 | T3 | §5 T3 IP4 | **PASS** |
| `dispatched` counter + `SetLiveEntryDispatched` | C4 | T3 | §5 T3 IP3 | **PASS** |
| `IsLaneEEligible` | D2 | T4 | §5 T4 | **PASS** |
| `TryLaneEHook` | D2 | T4 | §5 T4 | **PASS** |
| `TryLaneEDispatchFollower` | D2 | T4 | §5 T4 | **PASS** |
| `BuildLaneEKey` | D2 | T4 | §5 T4 | **PASS** |

**All 4 groups (A, B, C, D) addressed: PASS**
**All 16 missing symbols accounted for: PASS**

---

## Section C — Ticket Breakdown Verification

| Check | Result | Notes |
|-------|--------|-------|
| 7 tickets defined (T1-T7) | **PASS** | T1 through T7 all present in §5 |
| T1 scope = Group A fields + methods (A1, A2) | **PASS** | Correct |
| T2 scope = call sites + Subscribe/Unsubscribe (A3, A4, A5) | **PASS** | Correct |
| T3 scope = Lane D (Group C) | **PASS** | Correct |
| T4 scope = Lane E (Group D) | **PASS** | Correct |
| T5 scope = FlatGuardLoop (Group B) | **PASS** | Correct |
| T6 = non-src (guardian script + protocol hardening) | **PASS** | files: scripts/verify_critical_symbols.ps1, AGENTS.md, regression doc |
| T7 covers TradeCopierPanel BUG3 visual + BE arm/disarm | **PASS** | Sub-scopes 7A (visual), 7B (BE events), 7C (optional extraction) |
| Compile-order dependency (T1-T5 single session) warning | **PASS** | Documented at §4 and T4 note |

---

## Section D — Insertion Point Accuracy

| Insertion | Required per Spec | Plan States | Result |
|-----------|-------------------|------------|--------|
| T1 fields: after HEAD line 385 (after `_drainOwnedOrderIds`) | After line 385, before `// --- Status event ---` (line 387) | §5 T1 IP1: "After `_drainOwnedOrderIds` (HEAD line 385), before `// --- Status event ---` (HEAD line 387)" | **PASS** |
| T2 call site 1: CancelQxBrackets 2-param | After null guard, before `var stale = new` (~line 921) | §5 T2 IP1: HEAD line ~918, after `return; // (1)` line 921 | **PASS** |
| T2 call site 2: CancelQxBrackets 3-param | After null guard, before `var stale = new` (~line 1005) | §5 T2 IP2: HEAD line ~998, after `return; // (1)` line 1005 | **PASS** |
| T2 call site 3: FlattenOneAccount | Before `CancelAllAccountOrders` (~line 4709) | §5 T2 IP3: HEAD line ~4677, before `CancelAllAccountOrders` | **PASS** |
| T2: FindFollowerBracketOrder Account overload | HEAD lines 3571-3582, expr-body to block-body | §5 T2 IP4: HEAD lines 3571-3582 conversion documented | **PASS** |
| T2: Subscribe() + Unsubscribe() | HEAD lines 1350 / 1356 | §5 T2 IP5+6: HEAD line 1350 (Subscribe), 1356 (Unsubscribe) | **PASS** |
| T3: TryLaneDHook + TryLaneDDispatchFollower | After DispatchCopy ends (~HEAD line 2415) | §5 T3 IP1: "After `DispatchCopy` ends (HEAD line ~2415)" | **PASS** |
| T3: TryLaneDHook call in DispatchCopy | After `int baseQty = ResolveBaseQty(...)` (HEAD line 2373) | §5 T3 IP2: "After `int baseQty = ResolveBaseQty(...)` (HEAD line ~2373)" | **PASS** |
| T3: Lane D skip gate in DispatchToFollower | After `var mode = ResolveAtmMode(...)` (HEAD line 2506) | §5 T3 IP4: "After `var mode = ResolveAtmMode(rule, acc.Name);` (HEAD line ~2506)" | **PASS** |
| T3: dispatched counter loop replacement | HEAD lines 2384-2414 | §5 T3 IP3: HEAD lines 2384-2414 (idx=0 through idx++ close) | **PASS** |
| T4: 4 Lane E methods | After TryLaneDDispatchFollower (T3 insertion) | §5 T4: "After `TryLaneDDispatchFollower` ends (inserted in T3)" | **PASS** |
| T5: FlatGuardLoop method | Before PttDragOrphanPredicates class (~HEAD line 6386) | §5 T5 IP1: "Before `PttDragOrphanPredicates` class (before HEAD line 6386)" | **PASS** |
| T5: Flatten(Instrument) call site | HEAD lines 4482-4491 | §5 T5 IP2: HEAD lines 4482-4491 | **PASS** |
| T5: Flatten(Account,Instrument) call site | HEAD lines 4518-4537 | §5 T5 IP3: HEAD lines 4518-4537 | **PASS** |

---

## Section E — Jane Street Rule Compliance

### JS-021: No lock() (P0 CRITICAL)

| Ticket | Claim | Result |
|--------|-------|--------|
| T1 | "All writes use ConcurrentDictionary indexer/TryAdd — zero lock() usage" | **PASS** |
| T2 | "TryRemove is lock-free. No lock()." | **PASS** |
| T3 | "All ConcurrentDictionary operations (TryAdd, ContainsKey) are lock-free." | **PASS** |
| T4 | "All ConcurrentDictionary operations lock-free." | **PASS** |
| T5 | "Static, no shared mutable state. Pure iteration." | **PASS** |
| T7 | "No lock(). BE event subscriptions are += / -= operations (thread-safe NT8 pattern)." | **PASS** |

No new `lock()` usage introduced by any ticket. **PASS**

### JS-001: No throw in hot paths (P0 CRITICAL)

| Ticket | Methods | Claim | Result |
|--------|---------|-------|--------|
| T1 | All 5 slate gate methods | "All methods use early returns. Zero throw." | **PASS** |
| T3 | TryLaneDHook, TryLaneDDispatchFollower | "returns false on all failure paths; SendCopyWithAtm has its own try/catch" | **PASS** |
| T4 | IsLaneEEligible, TryLaneEHook, TryLaneEDispatchFollower, BuildLaneEKey | "returns false on all failure paths" | **PASS** |
| T5 | FlatGuardLoop | "No throw. NinjaTrader.Code.Output.Process is not a throw." | **PASS** |

**PASS**

### JS-002: No return null where value expected (P0 CRITICAL)

The one intentional `return null;` in `FindFollowerBracketOrder` (T2/A4) is correctly documented:
- Return type is `Order?` (explicitly nullable reference)
- Plan explicitly notes this at §5 T2 IP4: "The gate returns `return null;` for the `Order?` return type... This is NOT a JS-002 violation — the method's return type is explicitly `Order?` (nullable). Document in scan 3."
- Scan 3 in §7 correctly requires documentation of every `return null;` match and confirms the new T2 instance is the only new one.
- `BuildLaneEKey` returns `string.Empty` for null instr, never null. ✓

**PASS**

### JS-033: No async void (P0 CRITICAL)

- `OnFollowerPositionUpdate` is declared `private void` (synchronous event handler, not async void). Explicitly confirmed at T1 JS compliance notes.
- No other new async void methods introduced.

**PASS**

### JS-066: CYC <= 8 per method (P1 HIGH — DNA hardcoded rule)

| Method | CYC | Budget | Plan Note |
|--------|-----|--------|-----------|
| TryMarkFollowerSlateNotReady | 2 | PASS | §5 T1 |
| TrySetFollowerSlateReady | 4 | PASS | §5 T1 |
| IsAccountFlat | 3 | PASS | §5 T1 |
| HasAnyActiveOrders | 5 | PASS | §5 T1 |
| OnFollowerPositionUpdate | 3 | PASS | §5 T1 |
| FlatGuardLoop\<TAccount\> | 3 | PASS | §5 T5 |
| TryLaneDHook | 6 | PASS | §5 T3 |
| TryLaneDDispatchFollower | 4 | PASS | §5 T3 |
| IsLaneEEligible | 8 | AT BUDGET — FROZEN | §5 T4 (correctly flagged) |
| TryLaneEHook | 5 | PASS | §5 T4 |
| TryLaneEDispatchFollower | 5 | PASS | §5 T4 |
| BuildLaneEKey | 2 | PASS | §5 T4 |

CYC impact on modified methods:
- DispatchCopy: +1 for `if (dispatched > 0)`. Plan requires pre-insert verification (correct risk management). **PASS**
- DispatchToFollower: +1 for Lane D ContainsKey gate. HEAD CYC noted as ≤3 → becomes ≤4. **PASS**
- FindFollowerBracketOrder: +1 (expr-body CYC=1 → block-body CYC=2). **PASS**

All documented CYC values are within budget. **PASS**

### JS-081: ASCII-only strings (P1 HIGH — DNA hardcoded rule)

| Ticket | Claim | Notable Strings | Result |
|--------|-------|----------------|--------|
| T1 | "All strings are ASCII-only." | Key separator `"#"`, comment text | **PASS** |
| T3 | "All string keys use `#` as separator (ASCII, cannot appear in NT8 account names)." | `"#"` separator in orderId key | **PASS** |
| T4 | "`\"#LANE-E#\"` separator and `\"LANE-E#\"` prefix are ASCII-only." | `"#LANE-E#"`, `"LANE-E#"` | **PASS** |
| T5 | "[FLAT-GUARD] skip flat, already flat on" — ASCII-only | `Output.Process` string | **PASS** |
| T7 | "No Unicode/emoji in new string literals." | No new string literals in visual fixes | **PASS** |

**PASS**

### JS-023: UI update from off-thread (DNA hardcoded — Dispatcher.InvokeAsync mandate)

Plan at §5 T7/7B explicitly mandates:
> "Any WPF property set inside `OnPendingBeArmedDispatch` and `OnGlobalBeAllDisarmed` MUST use
> `Dispatcher.InvokeAsync`. These fire from CopyEngine on the NT8 account background thread.
> Verify f01b5155 bodies include the correct `Dispatcher.InvokeAsync` wrapping before inserting."

This is correctly identified and mandated. **PASS**

---

## Section F — NT8 API Risk Assessment

| Risk Item | Plan Handling | Result |
|-----------|--------------|--------|
| `GetCloneAtmMode` vs `ResolveAtmMode` name | Pre-insert verification grep documented at §5 T3 pre-insert and §6 NT8 API Risk | **PASS** |
| `PassesDailyCapCheck` name | Verification grep documented at §6 | **PASS** |
| `SetLiveEntryDispatched` existence | Verification grep documented at §6 | **PASS** |
| `SendCopyWithAtm` return type | Documented at §6; deviation handling described | **PASS** |
| `Account.All` in Subscribe/Unsubscribe | In `Subscribe()`/`Unsubscribe()` methods — NOT a constructor. DNA rule "Account.All in constructor = FAIL" does not apply here | **PASS** |
| `acc.Positions` in AddOnBase | Plan notes "NT8: acc.Positions is available in AddOnBase" | **PASS** |

---

## Section G — Constraint Compliance

| Constraint | Location in Plan | Result |
|------------|-----------------|--------|
| `TrySetSlateReadyOnDrainFill` explicitly excluded | §8 item 1: "This method was deliberately removed in f01b5155... Do NOT re-introduce it." | **PASS** |
| Approved Deletion List = NONE stated | §2 header: "Approved Deletion List: NONE. B183 adds code only." and §8 item 2 | **PASS** |
| B180-2 approved removals excluded from T7 scope | §5 T7 "Constraint" block lists all 5 excluded symbols; §8 item 4 | **PASS** |
| Single-hunk ban referenced | §8 item 6 and Scan 6 in §7 | **PASS** |
| No refactoring/modernization of verbatim bodies | §8 item 3: "All symbol bodies verbatim from f01b5155: Do NOT rewrite, refactor, or modernize." | **PASS** |
| `dispatched` counter in T3 (not a separate ticket) | §8 item 5; T3 scope includes it | **PASS** |

---

## Section H — Seven-Scan Checklist (§7)

| Scan | Command | Pass Condition | Presence | Result |
|------|---------|---------------|----------|--------|
| Scan 1 — JS-021 lock() | `grep -rn "lock(" CopyEngine.cs` | 0 results | Present §7 | **PASS** |
| Scan 2 — JS-033 async void | `grep -rn "async void " CopyEngine.cs` | 0 results | Present §7 | **PASS** |
| Scan 3 — JS-002 return null | `grep -rn "return null;" CopyEngine.cs` | Document all; new T2 instance is compliant (`Order?` nullable) | Present §7 | **PASS** |
| Scan 4 — JS-066 CYC | Manual review table | All 12 new methods ≤ 8; DispatchCopy and DispatchToFollower impact noted | Present §7 | **PASS** |
| Scan 5 — JS-081 ASCII | `grep -Pn "[^\x00-\x7F]" *.cs` | 0 results in B183-added lines | Present §7 | **PASS** |
| Scan 6 — Guardian script | `powershell -File .\scripts\verify_critical_symbols.ps1` | Exit 0 | Present §7 | **PASS** |
| Scan 7 — ptt-sync.ps1 | `powershell -File .\scripts\ptt-sync.ps1` | No errors, required after all .cs edits | Present §7 | **PASS** |

All 7 scans present with pass conditions. **PASS**

---

## Section I — SIM Gate Criteria (§9)

### BUG1 Verification

| Step | Required by Spec | Plan Section 9 | Result |
|------|-----------------|---------------|--------|
| TryMarkFollowerSlateNotReady called in CancelQxBrackets (both overloads) | ✓ | §9 BUG1 item 1 | **PASS** |
| Gate (1) in FindFollowerBracketOrder Account overload | ✓ | §9 BUG1 item 2 | **PASS** |
| TrySetFollowerSlateReady called from OnFollowerPositionUpdate | ✓ | §9 BUG1 item 3 | **PASS** |
| acc.PositionUpdate += OnFollowerPositionUpdate in Subscribe() | ✓ | §9 BUG1 item 4 | **PASS** |
| TryLaneDHook called from DispatchCopy after baseQty | ✓ | §9 BUG1 item 5 | **PASS** |
| Lane D skip gate in DispatchToFollower | ✓ | §9 BUG1 item 6 | **PASS** |
| Full cancel→copy trace (7 steps) | ✓ | §9 BUG1 item 7 | **PASS** |

### BUG2 Verification

| Step | Required by Spec | Plan Section 9 | Result |
|------|-----------------|---------------|--------|
| TryMarkFollowerSlateNotReady called in FlattenOneAccount | ✓ | §9 BUG2 item 1 | **PASS** |
| IsLaneEEligible checks gate is CLOSED before re-arming | ✓ | §9 BUG2 item 2 | **PASS** |
| TryLaneEHook called from OnFollowerPositionUpdate | ✓ | §9 BUG2 item 3 | **PASS** |
| Close path trace (gate=false → no re-entry) | ✓ | §9 BUG2 item 4 | **PASS** |
| Partial QX/BE path trace (Lane E re-arm) | ✓ | §9 BUG2 item 5 | **PASS** |

### BUG3 Verification

| Step | Required by Spec | Plan Section 9 | Result |
|------|-----------------|---------------|--------|
| BuildInstrRow(): _instr2tBtn.Background = BrushTeal after SetResourceReference | ✓ | §9 BUG3 item 1 | **PASS** |
| BuildInstrRow(): Foreground = Brushes.White (not BrushTeal) | ✓ | §9 BUG3 item 1 | **PASS** |
| Same for _instrQAll2tBtn | ✓ | §9 BUG3 item 1 | **PASS** |
| Specs tuple: BrushTeal as Bg for BE/BE ALL/Quick/Quick ALL | ✓ | §9 BUG3 item 2 | **PASS** |
| foreach: Foreground = Brushes.White when s.Teal | ✓ | §9 BUG3 item 3 | **PASS** |
| foreach: Background = isTeal ? BrushTeal : BrushInactive | ✓ | §9 BUG3 item 3 | **PASS** |
| OnPendingBeArmedDispatch subscribed to _engine.PendingBeArmed | ✓ | §9 BUG3 item 4 | **PASS** |
| OnGlobalBeAllDisarmed subscribed to _engine.GlobalBeAllDisarmed | ✓ | §9 BUG3 item 5 | **PASS** |
| UpdateBeAllVisuals(BeState.Idle) called from OnGlobalBeAllDisarmed | ✓ | §9 BUG3 item 6 | **PASS** |
| DisarmBeAllOnFlat(hasPosition) called from position-state update path | ✓ | §9 BUG3 item 7 | **PASS** |

---

## Section J — Informational Observations (Non-Blocking)

These observations are recorded for traceability but do NOT constitute violations.

### OBS-1: RULES_CATALOG.md Truncated at JS-041

`docs/standards/jane-street/RULES_CATALOG.md` contains rules JS-001 through JS-041 only. Rules
JS-042 through JS-110 (including JS-066 CYC and JS-081 ASCII) are absent from the file on disk.
The plan correctly references JS-066 and JS-081 as they are defined in the reviewer's hardcoded
DNA block (role definition), which supersedes the catalog file for enforcement purposes.
**Action required**: Catalog file should be restored to full content (a catalog maintenance task,
not a B183 block).

### OBS-2: IsLaneEEligible CYC=8 — FROZEN

`IsLaneEEligible` is documented as CYC=8 (at JS-066 budget). The plan correctly marks this as
FROZEN. Any future branch addition to this method will require a helper extraction before the
branch is added. This is a deferred risk already captured in the spec's DEFERRED BACKLOG section
(spec item 3).

### OBS-3: T7 Sub-scope 7C is Optional

The plan correctly designates structural extraction methods in 7C as "Engineer's Discretion."
The plan also correctly mandates that if `SubscribeEngineEvents()` is required as a wiring point
for 7B, it must be verified to exist in HEAD. This conditional handling is appropriate.

### OBS-4: DispatchCopy CYC Risk Correctly Flagged

The plan requires the engineer to verify HEAD DispatchCopy CYC before applying T3 changes.
If HEAD CYC = 8, extraction of a helper is required. This is the correct approach. No plan
violation.

---

## Section K — Deferred Work Register (Initial State)

No new deferred items from this plan review. The following are carried from the spec's
deferred backlog section (to be formally written in Ph5 06-deferred-backlog.md):

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B183-01 | xUnit tests for OnFollowerPositionUpdate, TrySetFollowerSlateReady, IsAccountFlat, HasAnyActiveOrders | P2 | B184 | OPEN |
| DW-B183-02 | xUnit test for FlatGuardLoop (delegate-injection pattern, NT8-free) | P2 | B184 | OPEN |
| DW-B183-03 | IsLaneEEligible CYC=8 at budget — plan helper extraction if branch ever added | P1 | future | OPEN |
| DW-B183-04 | `_laneDDispatched` lifetime cleanup (per-session growth, unbounded) | P2 | B185 | OPEN |
| DW-B183-05 | Restore RULES_CATALOG.md to full content (JS-042 through JS-110 missing) | P1 | ops | OPEN |

---

## Summary

| Section | Verdict |
|---------|---------|
| A — LANE-SPLIT GATE | PASS |
| B — 16 Missing Symbols Coverage | PASS |
| C — Ticket Breakdown (7 tickets, correct scope) | PASS |
| D — Insertion Point Accuracy | PASS |
| E — Jane Street Rule Compliance (JS-021, JS-001, JS-002, JS-033, JS-066, JS-081, JS-023) | PASS |
| F — NT8 API Risk | PASS |
| G — Constraint Compliance | PASS |
| H — 7-Scan Checklist | PASS |
| I — SIM Gate Criteria (BUG1, BUG2, BUG3) | PASS |

**Violations found**: 0
**REVIEW_PASS: YES**

---

*Ph2 review complete. Ph3 (ticket generation) is unblocked.*
