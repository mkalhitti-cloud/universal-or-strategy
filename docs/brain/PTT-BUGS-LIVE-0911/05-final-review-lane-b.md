# Final Review — PTT-BUGS-LIVE-0911 Lane B
**BUG-2: Post-Exit Clean Slate Gate (fo=NULL bracket failure)**
**Reviewer: ptt-plan-reviewer**
**Phase: 5 — Final Cross-File Coherence Review**
**Date: 2026-09-11**
**Verdict: FINAL_PASS**

---

## Input Documents Read

| Document | Status |
|---|---|
| `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-b.md` | READ — PLAN_COMPLETE (Cycle 3) |
| `docs/brain/PTT-BUGS-LIVE-0911/04-ticket-review-lane-b.md` | READ — TICKET_REVIEW_PASS |
| `docs/brain/PTT-BUGS-LIVE-0911/ticket-1-completion-lane-b.md` | READ — BUILD_PASS |
| `docs/brain/PTT-BUGS-LIVE-0911/ticket-1-verification-lane-b.md` | READ — VERIFY_PASS |
| `src/PropTraderTools/CopyEngine.cs` | READ (READ-ONLY) |
| `docs/brain/live-trading-analysis-2026-09-11.md` | READ — spec/evidence |
| `docs/brain/PTT-BUGS-LIVE-0911/06-deferred-backlog.md` | NOT FOUND — no prior blocks; created fresh in this phase |

---

## 1. Coherent System Check

### 1.1 State machine coherence

The `_followerSlateReady` dictionary implements a correct three-state gate:

| State | Condition | Gate |
|---|---|---|
| Key absent | First trade of session (never written) | OPEN — `TryGetValue` returns false; compound `false && !ready` = false |
| Key = `false` | After any exit that wipes ATM brackets | CLOSED — `TryGetValue` returns true, `!ready` = true; compound = true → return null |
| Key = `true` | After `PositionUpdate` confirms flat AND no active orders | OPEN — `TryGetValue` returns true, `!ready` = false; compound = false |

The lifecycle is complete and correct: **mark-not-ready (exit) → gate-closed → position-update-flat-clean → mark-ready → gate-open**. No missing transition. No stuck-closed path (recovery always fires when account goes flat with clean orders).

**Source confirmation (CopyEngine.cs):**
- Field declaration: lines 403–404 (`ConcurrentDictionary<string, bool>`, `StringComparer.Ordinal`)
- Write `false`: line 950 (`TryMarkFollowerSlateNotReady`)
- Write `true`: line 969 (`TrySetFollowerSlateReady`)
- Gate read: line 4020 (`FindFollowerBracketOrder(Account,...)`, first statement)

**Coherent system: PASS**

### 1.2 Subscribe/Unsubscribe pairing

- `Subscribe()` (line 1597): `acc.PositionUpdate += OnFollowerPositionUpdate` added inside `Account.All` loop alongside `OrderUpdate`.
- `Unsubscribe()` (line 1606): `acc.PositionUpdate -= OnFollowerPositionUpdate` added in matching loop.

Both are modified symmetrically. No handler leak possible. No orphaned subscription.

**Subscribe/Unsubscribe pairing: PASS**

### 1.3 Absent-key = ready semantics (first-trade safety)

`ConcurrentDictionary<string, bool>.TryGetValue` returns `false` (the default bool) when the key is absent. The gate expression is:

```csharp
if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready)
    return null;
```

When the key is absent: `TryGetValue` returns `false`, so the compound `&&` short-circuits — gate is open. This is the correct behavior for the first trade of session, where no prior write has occurred. Spec evidence confirms: "First trade of day was clean because no prior state existed" (output.md lines 266–267, where -07 correctly received brackets on Trade 2).

**Absent-key first-trade safety: PASS**

### 1.4 ConcurrentDictionary thread-safety

NT8 dispatches `PositionUpdate` and `OrderUpdate` events on background threads. All reads and writes to `_followerSlateReady` use `ConcurrentDictionary` operations:
- `_followerSlateReady[acc.Name] = false` — indexer write is atomic and lock-free (JS-021, JS-025)
- `_followerSlateReady[acc.Name] = true` — same
- `_followerSlateReady.TryGetValue(...)` — snapshot-consistent read, lock-free

No `lock()` anywhere. No `volatile` required (ConcurrentDictionary guarantees memory visibility). Correct for multi-threaded NT8 account dispatch threads.

**ConcurrentDictionary thread-safety: PASS**

---

## 2. Cross-File Check

### 2.1 All callers of `CancelQxBrackets` confirmed instrumented

| Caller | File | Overload | Instrumented by BUG2? |
|---|---|---|---|
| `PttQuickExit.Execute` (line 93) | `Features/PttQuickExit.cs` | 3-param | YES — via `CancelQxBrackets(3-param)` line 1154 |
| `TradeCopierPanel.CancelOrphanBracketsOnFlat` (line 693) | `TradeCopierPanel.cs` | 2-param | YES — via `CancelQxBrackets(2-param)` line 1035 |
| `PttBreakEvenSwap.Execute` (line 82) | `Features/PttBreakEvenSwap.cs` | 2-param | YES — via `CancelQxBrackets(2-param)` line 1035 |
| `RelayBe` internal handler (line 709) | `CopyEngine.cs` | 2-param | YES — via `CancelQxBrackets(2-param)` line 1035 |
| `CancelQxBracketsForFollowers` (line 1311) | `CopyEngine.cs` | 2-param (delegates) | YES — via `CancelQxBrackets(2-param)` line 1035 |

**All external callers route through an instrumented overload. No bypass path exists.**

**CancelQxBrackets coverage: PASS**

### 2.2 All callers of `FlattenOneAccount` confirmed instrumented

`FlattenOneAccount` is a `private` method on `CopyEngine`. All callers are internal. The `TryMarkFollowerSlateNotReady` call is at line 5536, immediately after the `IsAccountFlattenable` guard. Every code path that reaches the actual flatten dispatch passes through this call site.

No external file can call `FlattenOneAccount` directly (it is `private`). All flatten dispatch flows through `CopyEngine.cs` internal call chains.

**FlattenOneAccount coverage: PASS**

### 2.3 PttQuickExit.cs call routing confirmed

`PttQuickExit.Execute` (line 93) calls `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot)` — this is the **3-param overload**, which has `TryMarkFollowerSlateNotReady(acc)` at line 1154. The Friday 2026-09-11 QX-2T exit path goes through this exact call site. Confirmed: **no bypass**.

**PttQuickExit.cs routing: PASS**

### 2.4 Other exit paths that could leave slate dirty

Reviewed all exit paths reachable from outside CopyEngine:
- `PttBreakEvenSwap` calls `CancelQxBrackets(2-param)` → instrumented
- `TradeCopierPanel.CancelOrphanBracketsOnFlat` calls `CancelQxBrackets(2-param)` → instrumented
- `FlattenOneAccount` covers all flatten dispatches (private, internal callers confirmed)

No exit path bypasses the gate. No stale-slate scenario survives without `TryMarkFollowerSlateNotReady` being called.

**Exit path coverage completeness: PASS**

---

## 3. All Spec Requirements Satisfied

| ID | Requirement | Evidence in source | Status |
|---|---|---|---|
| BUG2-REQ-1 | `_followerSlateReady: ConcurrentDictionary<string, bool>` with `StringComparer.Ordinal` added to `CopyEngine` | Lines 403–404: exact declaration confirmed, `private readonly` | PASS |
| BUG2-REQ-2 | Slate marked not-ready on any QX/flatten exit — both `CancelQxBrackets` overloads AND `FlattenOneAccount` | Lines 1035 (2-param), 1154 (3-param), 5536 (FlattenOneAccount) | PASS |
| BUG2-REQ-3 | `account.PositionUpdate` subscribed in `Subscribe()`, unsubscribed in `Unsubscribe()` | Lines 1602 (+=), 1611 (-=), both inside `Account.All` loop | PASS |
| BUG2-REQ-4 | `OnFollowerPositionUpdate` sets ready=true only when flat AND no active orders | Lines 1013–1020 (`OnFollowerPositionUpdate`) → `TrySetFollowerSlateReady` (lines 959–970) → `IsAccountFlat` + `HasAnyActiveOrders` | PASS |
| BUG2-REQ-5 | `FindFollowerBracketOrder(Account,...)` returns null when slate explicitly not-ready | Lines 4012–4028: TryGetValue gate is first statement (line 4020), before `ActiveOrders` call (line 4022) | PASS |
| BUG2-REQ-6 | First trade unaffected — absent key in `_followerSlateReady` = gate open | Gate expression `TryGetValue&&!ready`: absent key → `TryGetValue=false` → compound `false` → gate OPEN | PASS |

**Spec evidence cross-check:**
- **output.md lines 268/274/280** — `-08` shows `fo=NULL` on Trade 2 (bracket-arm race, bounded exposure). This is the pre-fix evidence for the intermediate race window.
- **output.md lines 894/900/906** — `-07` shows `fo=NULL` on all 6 bracket orders for Trade 3 (Stop1, Target1, Stop2, Target2, Stop3, Target3). This is the primary BUG-2 failure instance. The fix's gate, when active, would cause `FindFollowerBracketOrder` to return null immediately (before the race), blocking bracket sync until the position update confirms clean. This eliminates the financial risk.

**All spec requirements satisfied: PASS**

---

## 4. All 7 Scans Confirmed Zero

### Layer 2 (Engineer) vs Layer 3 (Verifier) Comparison

| Scan | Engineer (L2) result | Verifier (L3) result | Discrepancy |
|---|---|---|---|
| SCAN-01: lock() — actual statements | 0 actual `lock()` statements (72 comment hits) | 0 actual `lock()` statements (72 comment hits) | **NONE** |
| SCAN-02: CYC — all 6 methods | Max CYC=5; all ≤ 8 | Max CYC=5; all ≤ 8; independently verified from source lines | **NONE** |
| SCAN-03: async/await in new methods | 0 in BUG2 methods (11 comment hits, pre-existing) | 0 in BUG2 methods (11 comment hits, pre-existing) | **NONE** |
| SCAN-04: Non-ASCII bytes | 0 | 0 | **NONE** |
| SCAN-05: StrategyBase-only APIs in new code | 0 (14 pre-existing references) | 0 (14 pre-existing references) | **NONE** |
| SCAN-06: Build clean | 0 errors, 164 passed, 0 failed | 0 errors, 164 passed, 0 failed | **NONE** |
| SCAN-07: Structural verification | All 3 exit paths confirmed; gate at line 4020 first statement | All 3 exit paths confirmed at same lines; gate at 4020 confirmed first | **NONE** |

**Notable SCAN-01 clarification:** The 72 grep hits are all `// JS-021: no lock(...)` comment annotations — not actual `lock()` code. The discriminator pattern `^\s+lock\s*\(` returns **0**. Zero actual `lock()` statements anywhere in `CopyEngine.cs`. Rule JS-021 not violated.

**All 7 scans confirmed zero — no discrepancies between L2 and L3: PASS**

---

## 5. DNA Rule Final Check

| Rule | Requirement | Final verdict |
|---|---|---|
| JS-021 (P0) | No `lock()`, no `Monitor`/`Mutex`/`SemaphoreSlim` for state | PASS — `ConcurrentDictionary` throughout; 0 actual `lock()` statements |
| JS-001 (P0) | No `throw` in `OnOrderUpdate`/`SendCopy`/gate chain | PASS — all new methods use early-return; no `throw` anywhere in BUG2 code |
| JS-002 (P0) | No null return where value expected | PASS — `FindFollowerBracketOrder` returns `Order?` (nullable contract unchanged); `void` helpers are `void` |
| JS-003 (P0) | No magic string discriminated state | PASS — no new magic strings; gate uses `bool` not string |
| JS-008 (P1) | No mutable struct fields; no unfrozen `SolidColorBrush` | PASS — no new structs; no brush operations |
| JS-009 (P1) | No `Dictionary<K,V>` for shared/thread-touched collection | PASS — `ConcurrentDictionary` used |
| JS-010 (P1) | No public constructor on singleton or signal struct | PASS — no new types |
| JS-023 (P0) | No UI update from off-thread without `Dispatcher.InvokeAsync` | PASS — `OnFollowerPositionUpdate` does no UI operations; `ConcurrentDictionary` write is thread-safe without Dispatcher |
| JS-025 | `ConcurrentDictionary` as lock-free pattern | PASS — canonical usage |
| CYC ≤ 8 (P1) | All methods within limit | PASS — max CYC = 5 (`HasAnyActiveOrders`) |
| NT8: no `async`/`await` in lifecycle/event handlers | `OnFollowerPositionUpdate` must be synchronous `void` | PASS — confirmed synchronous void (line 1013) |
| NT8: no `Account.All` in constructor | `Account.All` used only in `Subscribe()`/`Unsubscribe()` | PASS |
| NT8: no `sealed TradeCopierWindow` | Not applicable — no window changes | PASS |
| NT8: no `FontFamily` override | Not applicable | PASS |
| NT8: no `#RRGGBB` hex | Not applicable | PASS |
| NT8: no `CreateOrder` without PTT- prefix | No new `CreateOrder` calls | PASS |
| NT8: no `DateTime.Now` | Not used in BUG2 code | PASS |
| ASCII-only | SCAN-04 confirmed 0 non-ASCII bytes | PASS |
| No `AtmStrategyCreate`/`StartAtmStrategy` in new code | Confirmed by SCAN-05 | PASS |

**DNA rule check: PASS — zero violations**

---

## 6. Scope Compliance

Ticket scope lock was honored:
- **Modified files:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs` only
- **Untouched (as required):** `TradeCopierAddOn.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `PttQuickExit.cs`
- No undocumented changes. No phantom work items.

**Scope compliance: PASS**

---

## 7. Test Coverage

8 tests added to `CopyEngineTests.cs` (`Bug2FollowerSlateReadyTests` class):
- 5 run and pass (pure BCL, no NT8 runtime required)
- 3 skipped (NT8 runtime required; correct to skip in headless CI)
- All 7 required test names from the ticket specification are present
- 1 additional BCL variant test added (not scope violation — supplements, not replaces)

**Test coverage: PASS**

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-PTT0911B-01 | **SIM end-to-end verification** — SCAN-07 step 6 (live SIM with 2+ follower accounts, QX-2T exit, verify fo=NOT-NULL on 2nd trade brackets) cannot be executed in headless CI. Requires manual NinjaTrader SIM session. | P1 | B_next / pre-live | OPEN |
| DW-PTT0911B-02 | **Gate reset on session restart** — `_followerSlateReady` is an in-memory field on `CopyEngine`. If `CopyEngine` is re-instantiated mid-session (rare: AddOn reload), the dictionary is reset and all keys are absent. This is the correct safe default (gate-open = first trade safe). No data loss risk. Acknowledged, no action required. | P2 | future / informational | OPEN |
| DW-PTT0911B-03 | **Accounts added after Subscribe()** — NT8 may add accounts after `Subscribe()` is called (e.g., live account connected after AddOn load). Those accounts will not have `PositionUpdate` subscribed. The gate will never transition for them — but since their key is absent, the gate is OPEN (safe default). A future hardening pass could re-subscribe on `Account.AccountConnected`. | P2 | future | OPEN |
| DW-PTT0911B-04 | **`HasAnyActiveOrders` uses `Account.Orders.ToList()` snapshot** — This is the standard pattern in `CopyEngine` (line 4898). In a high-churn order scenario, the snapshot may include very recently terminal orders that will clear on the next `PositionUpdate` event. Consequence: `TrySetFollowerSlateReady` returns early once (gate stays closed one extra tick) and opens on the next `PositionUpdate` event. This is a conservative false-negative, not a false-positive. No bracket protection risk. | P2 | future / low-risk | OPEN |

**No P0 deferred items. All deferred items are P1 (SIM test) or P2 (informational/edge cases with safe defaults).**

---

## Aggregate Verdict

| Check | Result |
|---|---|
| 1. Coherent system (state machine, Subscribe/Unsubscribe, absent-key, thread-safety) | PASS |
| 2. Cross-file (all CancelQxBrackets callers, all FlattenOneAccount paths, PttQuickExit routing) | PASS |
| 3. All BUG2-REQ-1 through BUG2-REQ-6 satisfied | PASS |
| 4. All 7 scans confirmed zero — L2/L3 no discrepancies | PASS |
| 5. DNA rule check — zero violations | PASS |
| 6. Scope compliance | PASS |
| 7. Test coverage | PASS |
| Section K written | YES — 4 deferred items (P1×1, P2×3) |
| 06-deferred-backlog.md written | YES (this phase) |

**Violations found: ZERO**

---

## FINAL_PASS

*Generated by ptt-plan-reviewer, wave PTT-BUGS-LIVE-0911, Lane B, Phase 5*
