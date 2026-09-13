# Engineer Tickets — PTT-BUGS-LIVE-0911 Lane B
**BUG-2: Post-Exit Clean Slate (follower fo=NULL bracket failure)**
**Status: TICKETS_COMPLETE**
**Author: ptt-architect**
**Source plan: docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-b.md (REVIEW_PASS — Cycle 3 FINAL)**
**Date: 2026-09-11**

---

> **SCOPE LOCK — TICKET 1 ONLY**
> This file contains exactly one ticket (T1). Do not implement any other ticket, any other bug fix,
> or any change not listed in this document. Every changed line must trace to a requirement in
> BUG2-REQ-1 through BUG2-REQ-6 below. Changes to TradeCopierAddOn.cs, TradeCopierPanel.cs,
> TradeCopierWindow.cs, PttQuickExit.cs, or any file outside CopyEngine.cs (tests excepted) are
> **explicitly prohibited**.

---

## T1 — BUG-2: Post-Exit Clean Slate Gate

### 1. Ticket Identity

| Field | Value |
|---|---|
| Ticket ID | T1 |
| Bug ID | BUG-2 |
| Title | Post-Exit Clean Slate — per-follower readiness gate in CopyEngine |
| Source plan | `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-b.md` |
| Review status | REVIEW_PASS Cycle 3 FINAL (`02-plan-review-lane-b.md`) |
| Primary file | `src/PropTraderTools/CopyEngine.cs` |
| Test file | `src/PropTraderTools/CopyEngineTests.cs` |

---

### 2. Spec Requirement IDs

All requirements must be fully implemented. No partial implementations accepted.

| ID | Requirement | Satisfied by |
|---|---|---|
| BUG2-REQ-1 | `_followerSlateReady: ConcurrentDictionary<string, bool>` field added to `CopyEngine`. `StringComparer.Ordinal`. No lock. | New field declaration (Section 4.1) |
| BUG2-REQ-2 | Slate marked not-ready on any QX/flatten/close exit dispatch. Covers **both** `CancelQxBrackets` overloads (2-param line 938 + 3-param line 1052) and `FlattenOneAccount` (line 5417). | `TryMarkFollowerSlateNotReady` call injected into all three sites (Section 4.4) |
| BUG2-REQ-3 | `account.PositionUpdate` subscribed in `Subscribe()`, unsubscribed in `Unsubscribe()`. Follower accounts only (determined at runtime by `IsFollowerAccount`). | Modified `Subscribe()` + `Unsubscribe()` (Section 4.4) |
| BUG2-REQ-4 | `OnFollowerPositionUpdate` sets `ready=true` when account flat AND no active orders. Both conditions required simultaneously. | `TrySetFollowerSlateReady` via `IsAccountFlat` + `HasAnyActiveOrders` guards (Section 4.2) |
| BUG2-REQ-5 | `FindFollowerBracketOrder(Account, ...)` returns `null` immediately when `_followerSlateReady[acc.Name] == false`. Normal search proceeds when key is absent or `true`. | Gate added to `FindFollowerBracketOrder(Account, ...)` expression-body → block-body conversion (Section 4.4) |
| BUG2-REQ-6 | First trade of session unaffected. Absent key in `_followerSlateReady` = gate open. `TryGetValue` returns `false` for absent key; compound `false && !ready` = `false` = gate open. | Gate logic in `FindFollowerBracketOrder` (Section 4.4); gate semantics table (Section 5) |

---

### 3. Field Declarations

**File:** `src/PropTraderTools/CopyEngine.cs`
**Insert after:** line 394 (after `_drainOwnedOrderIds` declaration block, before `// --- Status event ---` comment at line 396)

```csharp
        // BUG2: Follower slate readiness gate.
        // Key = follower account name (StringComparer.Ordinal).
        // Value = true  -> slate confirmed clean; bracket sync allowed.
        //         false -> exit in progress or slate unverified; bracket sync blocked.
        // Absent key   -> treated as ready (first trade of session is always clean).
        // JS-021: ConcurrentDictionary -- lock-free. JS-023: no volatile (ConcurrentDictionary
        // guarantees memory visibility). JS-025: ConcurrentDictionary is canonical lock-free dict.
        private readonly ConcurrentDictionary<string, bool> _followerSlateReady =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
```

**Insert location precision:** After line 394 (`new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);`), before line 395 (blank line), before line 396 (`// --- Status event ---`). Add one blank line before the new comment block to maintain spacing style with surrounding field declarations.

---

### 4. Method Signatures and Implementation

#### 4.1 New Methods (5 total)

All new methods are `private` or `private static`. No new public API surface. All ASCII-only string literals and comments.

---

##### 4.1.1 `TryMarkFollowerSlateNotReady`

**Placement:** Add as a new private method in `CopyEngine.cs`. Recommended placement: alongside the new `_followerSlateReady` field's companion methods (after `HasAnyActiveOrders` and before the first existing method that uses it — `CancelQxBrackets`). Architect recommends inserting the 5 new methods as a contiguous block after line 937 (before `CancelQxBrackets` 2-param definition at line 938). The exact line within the file is flexible; the compiler position is not constrained.

**Signature:**
```csharp
private void TryMarkFollowerSlateNotReady(Account acc)
```

**Full implementation:**
```csharp
        // BUG2: Mark follower slate as not-ready when any exit wipes its ATM brackets.
        // Called from CancelQxBrackets and FlattenOneAccount on the follower account.
        // Guard: only applies to known follower accounts (IsFollowerAccount check).
        // CYC<=2: acc null(1) + IsFollowerAccount(2). JS-021: ConcurrentDictionary indexer is lock-free.
        // JS-001: no throw. JS-002: void. ASCII-only.
        private void TryMarkFollowerSlateNotReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            _followerSlateReady[acc.Name] = false;
        }
```

**NT8 constraints:** CYC = 2. No lock. No throw. No async/await. No DateTime.Now. ASCII-only.

---

##### 4.1.2 `TrySetFollowerSlateReady`

**Signature:**
```csharp
private void TrySetFollowerSlateReady(Account acc)
```

**Full implementation:**
```csharp
        // BUG2: Mark follower slate as ready after position confirmed flat + no active orders.
        // Called from OnFollowerPositionUpdate.
        // Conditions: acc non-null + IsFollowerAccount + IsAccountFlat + !HasAnyActiveOrders.
        // Only writes ready=true when ALL conditions pass. Early-returns prevent false positives.
        // CYC<=4: acc null(1) + IsFollowerAccount(2) + !IsAccountFlat(3) + HasAnyActiveOrders(4).
        // JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        private void TrySetFollowerSlateReady(Account acc)
        {
            if (acc == null)                    // (1)
                return;
            if (!IsFollowerAccount(acc))        // (2)
                return;
            if (!IsAccountFlat(acc))            // (3)
                return;
            if (HasAnyActiveOrders(acc))        // (4)
                return;
            _followerSlateReady[acc.Name] = true;
        }
```

**NT8 constraints:** CYC = 4. No lock. No throw. No async/await. ASCII-only.

---

##### 4.1.3 `IsAccountFlat`

**Signature:**
```csharp
private static bool IsAccountFlat(Account acc)
```

**Full implementation:**
```csharp
        // BUG2: Returns true when acc has no open positions across all instruments.
        // Used by TrySetFollowerSlateReady to confirm flat before marking slate ready.
        // NT8: acc.Positions is available in AddOnBase. Each Position.Quantity > 0 = open.
        // CYC<=3: acc null(1) + foreach(2) + Quantity>0(3). JS-021: no lock (read-only enumeration).
        // JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool IsAccountFlat(Account acc)
        {
            if (acc == null)                    // (1)
                return true;
            foreach (var pos in acc.Positions)  // (2)
                if (pos.Quantity > 0)           // (3)
                    return false;
            return true;
        }
```

**NT8 constraints:** CYC = 3. Uses `Account.Positions` (AddOnBase-available). No lock. No throw. Static. ASCII-only.

---

##### 4.1.4 `HasAnyActiveOrders`

**Signature:**
```csharp
private static bool HasAnyActiveOrders(Account acc)
```

**Full implementation:**
```csharp
        // BUG2: Returns true when acc has any Working/Submitted/Accepted orders remaining.
        // Used by TrySetFollowerSlateReady to confirm no residual active orders before ready.
        // NT8: acc.Orders.ToList() snapshot -- standard pattern in CopyEngine (line 4898).
        // CYC<=5: acc null(1) + foreach(2) + Working(3) + Submitted(4) + Accepted(5).
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool HasAnyActiveOrders(Account acc)
        {
            if (acc == null)                    // (1)
                return false;
            foreach (var o in acc.Orders.ToList())  // (2)
            {
                if (o.OrderState == OrderState.Working    // (3)
                    || o.OrderState == OrderState.Submitted  // (4)
                    || o.OrderState == OrderState.Accepted)  // (5)
                    return true;
            }
            return false;
        }
```

**NT8 constraints:** CYC = 5. Uses `Account.Orders` (AddOnBase-available; `.ToList()` snapshot pattern matches line 4898). No lock. No throw. Static. ASCII-only.

---

##### 4.1.5 `OnFollowerPositionUpdate`

**Signature:**
```csharp
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
```

**Full implementation:**
```csharp
        // BUG2: NT8 account.PositionUpdate event handler.
        // Fires on NT8 account background thread (same context as OnOrderUpdate).
        // When position goes flat and no active orders remain, marks slate ready for next trade.
        // NT8: PositionEventArgs.Position.Account confirmed from TradeCopierPanel.cs line 2109.
        // No UI operations -- ConcurrentDictionary write is thread-safe, no Dispatcher needed.
        // CYC<=3: e/Position/Account null(1) + IsFollowerAccount(2) + TrySetFollowerSlateReady(0).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. No async/await.
        private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
        {
            if (e?.Position?.Account == null)           // (1)
                return;
            if (!IsFollowerAccount(e.Position.Account)) // (2)
                return;
            TrySetFollowerSlateReady(e.Position.Account);
        }
```

**NT8 constraints:** CYC = 3. Fires on NT8 account background thread. No UI access permitted — `ConcurrentDictionary` write is thread-safe without `Dispatcher`. Signature must match `EventHandler<PositionEventArgs>` exactly as `(object sender, PositionEventArgs e)` per TradeCopierPanel.cs line 2109. No async/await. ASCII-only.

---

#### 4.2 Modified Methods (5 total)

---

##### 4.2.1 `FindFollowerBracketOrder(Account, ...)` — gate added

**File:** `src/PropTraderTools/CopyEngine.cs`
**Current location:** Lines 3902–3913 (expression-body `=>` form)

**Current code (lines 3902–3913):**
```csharp
        private Order? FindFollowerBracketOrder(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        ) =>
            FindFollowerBracketOrder(
                ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
                fromEntrySignalName,
                isStop,
                leaderName
            );
```

**Replace entirely with:**
```csharp
        // BUG2: Gate added -- if slate explicitly not-ready, return null immediately.
        // Absent key (first trade): gate open (TryGetValue returns false -> compound is false).
        // Key=false (after exit): gate closed -> return null, fo=NULL log suppressed.
        // Key=true (after PositionUpdate flat+clean): gate open -> normal search.
        // CYC<=2: base(1) + TryGetValue&&!ready(2). JS-021: ConcurrentDictionary.TryGetValue is lock-free.
        // JS-001: no throw. JS-002: Order? null contract unchanged. ASCII-only.
        private Order? FindFollowerBracketOrder(
            Account follower,
            string? fromEntrySignalName,
            bool isStop,
            string? leaderName = null
        )
        {
            bool ready;
            if (_followerSlateReady.TryGetValue(follower.Name, out ready) && !ready) // (1)
                return null;
            return FindFollowerBracketOrder(
                ActiveOrders(follower), // DW-NEW-09: terminal orders excluded
                fromEntrySignalName,
                isStop,
                leaderName
            );
        }
```

**Change summary:** Expression-body `=>` converted to block body. One `bool ready` local variable added. One `if` gate added (CYC +1 = 2 total). All existing comments and the `DW-NEW-09` annotation preserved.

**NT8 constraints:** CYC = 2 (was 1). `ConcurrentDictionary.TryGetValue` is lock-free. No throw. Order? null contract unchanged. ASCII-only.

---

##### 4.2.2 `Subscribe()` — add PositionUpdate subscription

**File:** `src/PropTraderTools/CopyEngine.cs`
**Current location:** Lines 1499–1503

**Current code:**
```csharp
        internal void Subscribe()
        {
            foreach (Account acc in Account.All)
                acc.OrderUpdate += OnOrderUpdate;
        }
```

**Replace with:**
```csharp
        // BUG2: PositionUpdate subscription added alongside OrderUpdate (same Account.All loop).
        // NT8: account.PositionUpdate confirmed available in AddOnBase (TradeCopierPanel.cs L861).
        // CYC unchanged.
        internal void Subscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate += OnOrderUpdate;
                acc.PositionUpdate += OnFollowerPositionUpdate; // BUG2
            }
        }
```

**Change summary:** Single-statement `foreach` body expanded to braced block. `acc.PositionUpdate += OnFollowerPositionUpdate;` added. CYC unchanged (1 foreach).

**NT8 constraints:** `account.PositionUpdate` is AddOnBase-available (confirmed TradeCopierPanel.cs line 861). No lock. No throw. ASCII-only.

---

##### 4.2.3 `Unsubscribe()` — add PositionUpdate unsubscription

**File:** `src/PropTraderTools/CopyEngine.cs`
**Current location:** Lines 1505–1509

**Current code:**
```csharp
        internal void Unsubscribe()
        {
            foreach (Account acc in Account.All)
                acc.OrderUpdate -= OnOrderUpdate;
        }
```

**Replace with:**
```csharp
        internal void Unsubscribe()
        {
            foreach (Account acc in Account.All)
            {
                acc.OrderUpdate -= OnOrderUpdate;
                acc.PositionUpdate -= OnFollowerPositionUpdate; // BUG2
            }
        }
```

**Change summary:** Single-statement `foreach` body expanded to braced block. `acc.PositionUpdate -= OnFollowerPositionUpdate;` added. CYC unchanged (1 foreach). **Pairing is mandatory:** every `+=` in `Subscribe()` must have a matching `-=` here to prevent orphaned event subscriptions.

**NT8 constraints:** No lock. No throw. ASCII-only.

---

##### 4.2.4 `CancelQxBrackets(Account, Instrument)` — mark slate not-ready (2-param overload)

**File:** `src/PropTraderTools/CopyEngine.cs`
**Current location:** Lines 938–951

**Current null guard (lines 940–941):**
```csharp
            if (acc == null || instr == null)
                return; // (1)
            var stale = new System.Collections.Generic.List<Order>();
```

**Replace the null guard + first statement with:**
```csharp
            if (acc == null || instr == null)
                return; // (1)
            TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent
            var stale = new System.Collections.Generic.List<Order>();
```

**Change summary:** One line added immediately after the null guard return (line 941), before `var stale`. No other lines in this method change. CYC unchanged (4). New call adds 0 branches.

**NT8 constraints:** `TryMarkFollowerSlateNotReady` contains its own null + `IsFollowerAccount` guards — safe to call unconditionally here. No lock. No throw. ASCII-only.

---

##### 4.2.5 `CancelQxBrackets(Account, Instrument, HashSet<Order>)` — mark slate not-ready (3-param overload, primary QX-2T path)

**File:** `src/PropTraderTools/CopyEngine.cs`
**Current location:** Lines 1052–1086

**This is the primary QX-2T execution path:** `PttGlobalQuickExit.Execute()` → `PttQuickExit.Execute()` → `CopyEngine.Instance?.CancelQxBrackets(leader, instr, snapshot)`. The Friday 2026-09-11 BUG-2 reproduction path runs through this overload.

**Current null guard (lines 1058–1060):**
```csharp
            if (acc == null || instr == null)
                return; // (1)
            var stale = new System.Collections.Generic.List<Order>();
```

**Replace the null guard + first statement with:**
```csharp
            if (acc == null || instr == null)
                return; // (1)
            TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)
            var stale = new System.Collections.Generic.List<Order>();
```

**Change summary:** One line added immediately after the null guard return (line 1059), before `var stale`. No other lines in this method change. CYC unchanged (5). New call adds 0 branches.

**NT8 constraints:** Identical pattern to 2-param overload. No lock. No throw. ASCII-only.

---

##### 4.2.6 `FlattenOneAccount(Account, Instrument)` — mark slate not-ready

**File:** `src/PropTraderTools/CopyEngine.cs`
**Current location:** Lines 5417–5425

**Current code (lines 5417–5425):**
```csharp
        private void FlattenOneAccount(Account acc, Instrument instrument)
        {
            if (!IsAccountFlattenable(acc, instrument))
                return; // (1)
            CancelAllAccountOrders(acc, instrument); // B69 DW-B69-01: cancel ALL orders first
```

**Replace the guard + first line with:**
```csharp
        private void FlattenOneAccount(Account acc, Instrument instrument)
        {
            if (!IsAccountFlattenable(acc, instrument))
                return; // (1)
            TryMarkFollowerSlateNotReady(acc); // BUG2: flatten dispatch imminent
            CancelAllAccountOrders(acc, instrument); // B69 DW-B69-01: cancel ALL orders first
```

**Change summary:** One line added immediately after the `IsAccountFlattenable` guard return, before `CancelAllAccountOrders`. No other lines change. CYC unchanged (2). New call adds 0 branches.

**NT8 constraints:** `TryMarkFollowerSlateNotReady` contains its own null + `IsFollowerAccount` guards — safe to call unconditionally here. No lock. No throw. ASCII-only.

---

### 5. Gate Semantics Reference

The engineer must implement `FindFollowerBracketOrder(Account, ...)` gate logic so that the following three-state table holds exactly:

| `_followerSlateReady` state | `TryGetValue` result | Gate expression `TryGetValue && !ready` | Gate decision |
|---|---|---|---|
| Key **absent** (session start, first trade) | returns `false` | `false && !ready` = `false` | **OPEN** — normal `ActiveOrders` search |
| Key = `false` (after `CancelQxBrackets` / `FlattenOneAccount`) | returns `true`, `ready=false` | `true && true` = `true` | **CLOSED** — return `null` immediately |
| Key = `true` (after `PositionUpdate` flat+clean) | returns `true`, `ready=true` | `true && false` = `false` | **OPEN** — normal `ActiveOrders` search |

**Critical:** The absent-key case MUST produce gate-open behavior. Do not substitute `ContainsKey` + `this[key]` which would throw on absent key. The `TryGetValue` pattern is mandatory.

---

### 6. NT8 API Compliance Annotations

| API used | Availability | Source of truth |
|---|---|---|
| `account.PositionUpdate` event | AddOnBase ✅ | TradeCopierPanel.cs line 861 (existing usage) |
| `PositionEventArgs.Position.Account` | AddOnBase ✅ | TradeCopierPanel.cs line 2109 (existing handler) |
| `Account.Positions` collection | AddOnBase ✅ | NT8 standard Account API |
| `Account.Orders` collection + `.ToList()` | AddOnBase ✅ | CopyEngine.cs line 4898 (existing pattern) |
| `Account.All` | AddOnBase ✅ | CopyEngine.cs lines 1501, 1507 (existing) |
| `AtmStrategyCreate()` | StrategyBase ONLY ❌ | NOT used in this fix — prohibited |
| `StartAtmStrategy()` | StrategyBase ONLY ❌ | NOT used in this fix — prohibited |
| `AtmStrategyChangeStopTarget()` | StrategyBase ONLY ❌ | NOT used in this fix — prohibited |

**OnFollowerPositionUpdate threading note:** This handler fires on the NT8 account background thread (same thread as `OnOrderUpdate`). It writes only to `_followerSlateReady` (a `ConcurrentDictionary`). No `Dispatcher.InvokeAsync` is needed or permitted here — UI access from this handler is prohibited.

---

### 7. Complete 7-Scan Checklist (Engineer Contract)

The engineer MUST run all 7 scans after implementation and before marking the ticket done. Each scan must reach its PASS criterion. All scan results must be documented in the PR.

---

**SCAN-01: No `lock()` keyword**

```
Command:   grep -r "lock(" src/PropTraderTools/CopyEngine.cs
PASS =     ZERO matches in CopyEngine.cs
FAIL =     Any match anywhere in the file (pre-existing violations also fail — report but do not fix in this PR)
Scope:     All new and modified methods introduced by BUG-2
Rationale: JS-021 — all new shared state must use ConcurrentDictionary (lock-free). No Monitor,
           no Mutex, no SemaphoreSlim for state guarding.
```

---

**SCAN-02: Cyclomatic complexity — all new/modified methods <= 8**

```
Tool:      Any CYC-capable linter (Lizard, NDepend, or manual branch count)
PASS =     Every method listed below reports CYC at or below its stated value

Methods to verify:
  TryMarkFollowerSlateNotReady(Account)                    -> CYC = 2    LIMIT = 8
  TrySetFollowerSlateReady(Account)                        -> CYC = 4    LIMIT = 8
  IsAccountFlat(Account)                                   -> CYC = 3    LIMIT = 8
  HasAnyActiveOrders(Account)                              -> CYC = 5    LIMIT = 8
  OnFollowerPositionUpdate(object, PositionEventArgs)      -> CYC = 3    LIMIT = 8
  FindFollowerBracketOrder(Account, string?, bool, string?) -> CYC = 2    LIMIT = 8

FAIL =     Any method above exceeds its stated CYC value.
           Unmodified methods retain their prior-passing CYC — no re-verification required
           unless the method body was touched.
```

---

**SCAN-03: No `async`/`await` in lifecycle or event handler methods**

```
Command:   grep -n "async\|await" src/PropTraderTools/CopyEngine.cs
PASS =     ZERO matches in any method introduced by this fix.
           Pre-existing async/await elsewhere in the file does NOT fail this scan
           (scope is new/modified BUG-2 methods only).
FAIL =     Any new method in this fix contains the keyword async or await.
Critical:  OnFollowerPositionUpdate MUST be synchronous void. Making it async void
           causes unhandled exception semantics on the NT8 account background thread.
```

---

**SCAN-04: ASCII-only — no Unicode or non-ASCII bytes in new code**

```
Command:   grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
PASS =     ZERO matches in any new string literal, comment, or identifier added by this fix.
FAIL =     Any match in new BUG-2 code (curly quotes, em-dashes, emoji, etc.).
           Pre-existing non-ASCII elsewhere in the file does NOT fail this scan
           (scope is new/modified BUG-2 code only).
Note:      All comments in this ticket use ASCII-only. Copy them verbatim.
```

---

**SCAN-05: NT8 API compliance — no StrategyBase-only calls**

```
Verification steps:
  1. grep -n "AtmStrategyCreate"        src/PropTraderTools/CopyEngine.cs
     PASS = zero matches in any new BUG-2 method.
  2. grep -n "StartAtmStrategy"         src/PropTraderTools/CopyEngine.cs
     PASS = zero matches in any new BUG-2 method.
  3. grep -n "AtmStrategyChangeStop"    src/PropTraderTools/CopyEngine.cs
     PASS = zero matches in any new BUG-2 method.
  4. Confirm new APIs used are AddOnBase-available:
     - account.PositionUpdate          -> confirmed (TradeCopierPanel.cs L861)
     - PositionEventArgs.Position.Account -> confirmed (TradeCopierPanel.cs L2109)
     - Account.Positions               -> standard AddOnBase API
     - Account.Orders                  -> used at CopyEngine.cs line 4898
     - ConcurrentDictionary<string,bool> -> BCL, no NT8 constraint

FAIL = Any call to AtmStrategyCreate, StartAtmStrategy, or AtmStrategyChangeStopTarget
       in any new BUG-2 method.
```

---

**SCAN-06: Build clean**

```
Command:   powershell -File .\scripts\build_readiness.ps1
PASS =     ZERO errors, ZERO warnings in build output.
FAIL =     Any error or warning.

Common failure modes for this fix:
  - OnFollowerPositionUpdate signature mismatch: must be exactly
    (object sender, PositionEventArgs e) — not (object, PositionEventArgs) or
    any other variation — to match EventHandler<PositionEventArgs> delegate type.
  - Missing `using System.Collections.Concurrent;` if not already present (check existing imports).
  - `PositionEventArgs` namespace: NinjaTrader.Cbi.PositionEventArgs — confirm against
    TradeCopierPanel.cs usage at line 2109.
  - `bool ready` declared but assigned via `out` — compiler may warn on pre-C#7 projects
    if `out var ready` form is preferred; use `bool ready; ... out ready` form as shown in
    Section 4.2.1 to match existing project style.
```

---

**SCAN-07: Functional verification (SIM test)**

```
Setup:
  - Load SIM rules with at least 2 follower accounts (e.g., PA-APEX-422136-07 + -08).
  - CopyEngine running. Subscribe() has been called (PositionUpdate wired).

Procedure:
  Step 1. Enter Trade 1 on leader.
          Verify: [TP4-SFB] logs for all followers show fo=NOT-NULL for all bracket orders
          (Stop1/Target1/Stop2/Target2/Stop3/Target3). Gate is open (absent key).

  Step 2. Exit Trade 1 using QX-2T button.
          Verify: CancelQxBrackets 3-param overload fires. TryMarkFollowerSlateNotReady
          is called for each follower account. _followerSlateReady[acc.Name] = false.

  Step 3. Observe [TP4-SFB] log lines immediately after QX-2T while gate is closed.
          Expected: fo=NULL log entries MAY appear during this window. This is correct
          behavior (TryLogSFBTrace fires before the null guard in SyncFollowerBracket).
          These fo=NULL entries during gate-closed window are EXPECTED and do NOT
          constitute a scan failure.

  Step 4. Wait for PositionUpdate(flat) to fire on all followers (accounts reach zero
          position, no active orders). OnFollowerPositionUpdate -> TrySetFollowerSlateReady
          sets _followerSlateReady[acc.Name] = true.

  Step 5. Enter Trade 2 on leader.
          Verify: ALL 6 bracket orders (Stop1/Target1/Stop2/Target2/Stop3/Target3) show
          fo=NOT-NULL in [TP4-SFB] logs for ALL follower accounts. Gate is open (key=true).

PASS CRITERIA:
  No Account.CreateOrder calls for bracket orders execute on follower accounts while
  _followerSlateReady gate is closed (false). fo=NULL log entries during the gate-closed
  window are EXPECTED and do NOT constitute a scan failure. Gate opens only after
  PositionUpdate hasPos=False fires AND no active orders remain. Verify by confirming
  no bracket submission orders are dispatched to followers on 2nd+ trade until the
  PositionUpdate flat+clean condition is confirmed.

FAIL CRITERIA:
  Any follower account receives a bracket order (Working/Submitted/Accepted stop or
  target) while _followerSlateReady[acc.Name] == false. OR: Trade 2 brackets fail to
  appear (fo=NULL on all brackets on Trade 2 after gate should be open = regression).
```

---

### 8. xUnit Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`

All 7 tests are required. Each test must be an independent `[Fact]`. No shared mutable state between tests.

---

**Test 1 — BUG2-REQ-1 + BUG2-REQ-6**

```csharp
[Fact]
public void FollowerSlateReady_AbsentKey_GateOpen_FindFollowerBracketOrderProceedsNormally()
```

**What it asserts:** When `_followerSlateReady` has no entry for the follower account (simulates first trade of session), `FindFollowerBracketOrder(Account, ...)` calls through to the `ActiveOrders` search rather than returning `null` immediately. The gate is open by default for absent keys. No prior call to `TryMarkFollowerSlateNotReady` occurs in test setup.

---

**Test 2 — BUG2-REQ-2 + BUG2-REQ-5**

```csharp
[Fact]
public void FollowerSlateReady_AfterNotReady_GateClosed_FindFollowerBracketOrderReturnsNull()
```

**What it asserts:** After `TryMarkFollowerSlateNotReady(followerAcc)` is called, `FindFollowerBracketOrder(followerAcc, ...)` returns `null` even when the follower account's order collection contains bracket orders in `Working`/`Submitted` state that would otherwise match. The gate is closed by the not-ready mark.

---

**Test 3 — BUG2-REQ-4 + BUG2-REQ-5**

```csharp
[Fact]
public void FollowerSlateReady_AfterFlatAndCleanOrders_GateOpen_FindFollowerBracketOrderFindsOrders()
```

**What it asserts:** After `TryMarkFollowerSlateNotReady` is called (gate closed), then `TrySetFollowerSlateReady` is called with a follower account that is flat (no positions) and has no active orders, the gate opens (`_followerSlateReady[acc.Name] == true`), and `FindFollowerBracketOrder` proceeds to the `ActiveOrders` search and can return a matching order.

---

**Test 4 — BUG2-REQ-4**

```csharp
[Fact]
public void TrySetFollowerSlateReady_WithOpenPosition_DoesNotSetReady()
```

**What it asserts:** When `TrySetFollowerSlateReady` is called for a follower account that has a non-zero open position (`pos.Quantity > 0`), the `_followerSlateReady` entry is NOT updated to `true`. The `IsAccountFlat` guard must block the ready mark. If the prior state was `false`, it remains `false` after the call.

---

**Test 5 — BUG2-REQ-4**

```csharp
[Fact]
public void TrySetFollowerSlateReady_WithActiveOrders_DoesNotSetReady()
```

**What it asserts:** When `TrySetFollowerSlateReady` is called for a follower account that has a zero position (flat) but still has `Working`, `Submitted`, or `Accepted` orders remaining, the `_followerSlateReady` entry is NOT updated to `true`. The `HasAnyActiveOrders` guard must block the ready mark.

---

**Test 6 — BUG2-REQ-6**

```csharp
[Fact]
public void TryMarkFollowerSlateNotReady_OnNonFollowerAccount_NoStateChange()
```

**What it asserts:** When `TryMarkFollowerSlateNotReady` is called with a non-follower account (one where `IsFollowerAccount` returns `false`), no entry is written to `_followerSlateReady`. The dictionary remains unmodified. The `IsFollowerAccount` guard in `TryMarkFollowerSlateNotReady` is effective.

---

**Test 7 — BUG2-REQ-2 + BUG2-REQ-5**

```csharp
[Fact]
public void FollowerSlateReady_GateClosed_BracketSyncBlocked_FoNullLogPreserved()
```

**What it asserts:** When `_followerSlateReady[acc.Name] == false` (gate closed), `FindFollowerBracketOrder(Account, ...)` returns `null`, causing `SyncFollowerBracket` to return early before any bracket order submission (`Account.CreateOrder`) is dispatched to the follower account. The test asserts that **no `CreateOrder` calls** occur on the follower for bracket orders while the gate is closed. The `fo=NULL` diagnostic log line (emitted by `TryLogSFBTrace` before the `if (fo == null) return` guard in `SyncFollowerBracket`) MAY appear — this is intentional observability behavior and the test does NOT assert log absence. The test criterion is bracket-propagation-blocked (no `CreateOrder` calls), not log-line-absent.

---

### 9. Prohibited Changes (Scope Lock Enforcement)

The following changes are **explicitly out of scope** for this ticket. Any implementation that includes these will be returned as TICKET_FAIL:

| Prohibited action | Reason |
|---|---|
| Editing `TradeCopierAddOn.cs` | Out of scope for BUG-2 |
| Editing `TradeCopierPanel.cs` | Out of scope for BUG-2 |
| Editing `TradeCopierWindow.cs` | Out of scope for BUG-2 |
| Editing `PttQuickExit.cs` | Out of scope for BUG-2 |
| Adding `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim` | Violates JS-021 |
| Adding `async`/`await` to any new method | Violates NT8 threading model |
| Adding `AtmStrategyCreate()`, `StartAtmStrategy()`, `AtmStrategyChangeStopTarget()` | StrategyBase-only — prohibited in AddOnBase |
| Using `DateTime.Now` | Must use `DateTime.UtcNow` if datetime needed (not needed in this fix) |
| Modifying any method not listed in Sections 4.1–4.2 | Violates scope lock |
| Adding any public method or property | No new public API surface |
| Using Unicode, emoji, or non-ASCII characters in any string or comment | ASCII-only |
| Using `Dictionary<K,V>` for `_followerSlateReady` | Must be `ConcurrentDictionary` |

---

### 10. Verification Checklist (Engineer Self-Sign-Off)

Before submitting this ticket as complete, the engineer must confirm each item:

```
[ ] _followerSlateReady field added at line ~395 with StringComparer.Ordinal
[ ] TryMarkFollowerSlateNotReady implemented — CYC=2, no lock, no throw
[ ] TrySetFollowerSlateReady implemented — CYC=4, no lock, no throw
[ ] IsAccountFlat implemented — CYC=3, static, no lock, no throw
[ ] HasAnyActiveOrders implemented — CYC=5, static, .ToList() snapshot, no lock
[ ] OnFollowerPositionUpdate implemented — CYC=3, synchronous void, no Dispatcher
[ ] FindFollowerBracketOrder(Account,...) converted to block-body with gate — CYC=2
[ ] Subscribe() updated — PositionUpdate += OnFollowerPositionUpdate added
[ ] Unsubscribe() updated — PositionUpdate -= OnFollowerPositionUpdate added (paired)
[ ] CancelQxBrackets 2-param (line 938): TryMarkFollowerSlateNotReady inserted after null guard
[ ] CancelQxBrackets 3-param (line 1052): TryMarkFollowerSlateNotReady inserted after null guard
[ ] FlattenOneAccount (line 5417): TryMarkFollowerSlateNotReady inserted after IsAccountFlattenable guard
[ ] All 7 xUnit [Fact] tests written in CopyEngineTests.cs
[ ] SCAN-01 PASS (zero lock() matches)
[ ] SCAN-02 PASS (all 6 methods at or below stated CYC)
[ ] SCAN-03 PASS (zero async/await in new methods)
[ ] SCAN-04 PASS (zero non-ASCII in new code)
[ ] SCAN-05 PASS (no StrategyBase-only calls)
[ ] SCAN-06 PASS (build clean, zero errors, zero warnings)
[ ] SCAN-07 PASS (SIM test: brackets present on Trade 2, gate semantics confirmed)
```

---

*TICKETS_COMPLETE*
