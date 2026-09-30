# PTT-COPIER B56 LaneA -- Architecture Plan
## Status: REVIEW_PASS
## Epic: B56-LaneA | DW-B56-01 | Limit Order Gate 3 Fix + Leader Cancel Propagation
## Date: 2026-08-09
## Author: ptt-architect

---

## 1. Problem Statement

Two silent-drop bugs in `CopyEngine.OnOrderUpdate` / `DispatchCopy`:

1. **Gap 1 (Gate 3)**: `DispatchCopy` line 512 guards on `OrderState.Submitted` only. NT8
   AddOn-placed limit orders (via `Account.CreateOrder`) never receive `Submitted` -- they
   transition `Initialized -> Accepted -> Working -> Filled/Cancelled`. All post-placement
   events for limit orders are silently dropped.

2. **Gap 2 (Cancel propagation)**: `OnOrderUpdate` has no handler for
   `e.Order.OrderState == OrderState.Cancelled` on the leader. When the leader is cancelled,
   follower entry orders remain `Initialized` or `Working` indefinitely (leaked orders).

---

## 2. Single Modified File

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

No other file is modified. No new files are created in `src/`.

---

## 3. Change A -- IsDispatchTriggerState Predicate

### 3.1 New Method (insert before DispatchCopy)

**Insertion point**: Before line 503 (the `// --- B7-F0: Bracket mirroring methods ---` comment
block that precedes `DispatchCopy`). The new method and its header comment are inserted as new
lines immediately before line 503.

**Exact text to insert** (6 lines, inserted before line 503):

```csharp
        // B56 T1: IsDispatchTriggerState -- CYC=2. True for states that trigger follower placement.
        // Market orders fire Submitted; AddOn limit orders fire Accepted (skip Submitted).
        // JS-002: returns bool (not null). JS-021: no lock. NT8 confirmed state set.
        // TESTABILITY: internal static -- directly testable via InternalsVisibleTo (same pattern as ShouldMirrorClose).
        internal static bool IsDispatchTriggerState(Order order)
            => order.OrderState == OrderState.Submitted   // market orders
            || order.OrderState == OrderState.Accepted;   // limit orders (AddOn path)
```

### 3.2 Gate 3 Replacement in DispatchCopy

**Target**: Line 511-513 (inclusive) of the current file:
```csharp
            // Gate 3: must be Submitted state
            if (order.OrderState != OrderState.Submitted)
                return;
```

**Replace with**:
```csharp
            // Gate 3: must be a dispatch-trigger state (Submitted for market; Accepted for AddOn limit)
            if (!IsDispatchTriggerState(order))
                return;
```

**Net line delta**: 0 lines (3 replaced with 3).

---

## 4. Change B -- Leader Cancelled Propagation

### 4.1 Insertion Point in OnOrderUpdate

The Cancelled block must be placed AFTER Gate 2.5 (line 427-429) and BEFORE Gate B
(`if (IsWorkingBracket(e.Order))` at line 436).

The current content between Gate 2.5 and Gate B (lines 431-435):
```
431: // B9 T3 -- Mirror mode relay (inserted after Gate 2.5, before Gate B)
432: if ((CopyMode)_copyModeValue == CopyMode.Mirror)
433:     MirrorOrderUpdate(e.Order, matchedRule.Value);
434: (blank)
435: // Gate B: bracket drag detection -- divert to HandleBracketChange path
```

**Insert AFTER line 433** (the `MirrorOrderUpdate` call) and BEFORE the blank line 434.
This keeps the Cancelled block before Gate B while preserving Mirror mode relay ordering.

**Exact text to insert** (10 new lines, inserted after line 433):

```csharp

            // B56 T1: propagate leader cancel to follower entry orders.
            // Fires when leader order is cancelled -- cancels all Initialized/Working
            // follower entry orders for this instrument via CancelOneAccount.
            // Placed BEFORE Gate B so bracket orders are not affected (they have their own path).
            if (e.Order.OrderState == OrderState.Cancelled)
            {
                foreach (var acc in matchedRule.Value.FollowerAccounts)
                {
                    if (acc == null) continue;
                    CancelOneAccount(acc, e.Order.Instrument);
                }
                return;
            }
```

---

## 5. Testability Decision: `internal static` (NOT reflection)

**Decision**: `IsDispatchTriggerState` is declared `internal static` (not `private static`).

**Justification**:
- Existing precedent: [`ShouldMirrorClose()`](CopyEngine.cs:452) is `internal static` with
  primitive parameters, and its comment explicitly states
  `"TESTABILITY: internal static with primitive parameters -- directly testable without NT8 runtime"`.
- `OrderState` is an NT8 enum in `NinjaTrader.Custom.dll`, which IS available in the Linting
  .csproj test project. No NT8 runtime is needed to compare enum values.
- `InternalsVisibleTo` is already wired in the assembly (evidenced by existing internal-access
  tests). Reflection adds complexity with zero benefit.
- `private static` + reflection is slower, breaks under obfuscation, and produces harder-to-read
  tests. `internal static` is the established, tested pattern in this codebase.

---

## 6. Complete Diff Summary (verbatim text blocks)

### 6a. Header comment block (top of file) -- append after existing B-block header

Insert at line 1 (prepend before existing header), pushing existing lines down:

```csharp
// PTT-COPIER-B56-LaneA-T1 -- CopyEngine.cs
// B56 T1 CHANGES:
//   1. Added IsDispatchTriggerState(Order) -- internal static predicate, CYC=2. (DW-B56-01 Gap 1)
//   2. DispatchCopy Gate 3: replaced raw Submitted check with IsDispatchTriggerState. (DW-B56-01 Gap 1)
//   3. OnOrderUpdate Cancelled block: propagate leader cancel to follower entry orders. (DW-B56-01 Gap 2)
```

### 6b. IsDispatchTriggerState (insert before line 503)

```csharp
        // B56 T1: IsDispatchTriggerState -- CYC=2. True for states that trigger follower placement.
        // Market orders fire Submitted; AddOn limit orders fire Accepted (skip Submitted).
        // JS-002: returns bool (not null). JS-021: no lock. NT8 confirmed state set.
        // TESTABILITY: internal static -- directly testable via InternalsVisibleTo (same pattern as ShouldMirrorClose).
        internal static bool IsDispatchTriggerState(Order order)
            => order.OrderState == OrderState.Submitted   // market orders
            || order.OrderState == OrderState.Accepted;   // limit orders (AddOn path)

```

### 6c. DispatchCopy Gate 3 replacement (lines 511-513)

BEFORE:
```csharp
            // Gate 3: must be Submitted state
            if (order.OrderState != OrderState.Submitted)
                return;
```

AFTER:
```csharp
            // Gate 3: must be a dispatch-trigger state (Submitted for market; Accepted for AddOn limit)
            if (!IsDispatchTriggerState(order))
                return;
```

### 6d. OnOrderUpdate Cancelled block (insert after line 433, before existing blank line 434)

```csharp

            // B56 T1: propagate leader cancel to follower entry orders.
            // Fires when leader order is cancelled -- cancels all Initialized/Working
            // follower entry orders for this instrument via CancelOneAccount.
            // Placed BEFORE Gate B so bracket orders are not affected (they have their own path).
            if (e.Order.OrderState == OrderState.Cancelled)
            {
                foreach (var acc in matchedRule.Value.FollowerAccounts)
                {
                    if (acc == null) continue;
                    CancelOneAccount(acc, e.Order.Instrument);
                }
                return;
            }
```

---

## 7. Numbered Tickets

---

### Ticket 1: CopyEngine.cs -- Two surgical edits + header comment

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

**Spec requirements satisfied**: DW-B56-01 Gap 1 (Gate 3 limit order fix), DW-B56-01 Gap 2 (leader cancel propagation)

**Method signatures to implement**:

```csharp
// NEW METHOD (internal static -- CYC=2)
internal static bool IsDispatchTriggerState(Order order)
    => order.OrderState == OrderState.Submitted
    || order.OrderState == OrderState.Accepted;
```

**Edit 1 of 3 -- Prepend B56 header comment block** (insert before existing first line):
```
// PTT-COPIER-B56-LaneA-T1 -- CopyEngine.cs
// B56 T1 CHANGES:
//   1. Added IsDispatchTriggerState(Order) -- internal static predicate, CYC=2. (DW-B56-01 Gap 1)
//   2. DispatchCopy Gate 3: replaced raw Submitted check with IsDispatchTriggerState. (DW-B56-01 Gap 1)
//   3. OnOrderUpdate Cancelled block: propagate leader cancel to follower entry orders. (DW-B56-01 Gap 2)
```

**Edit 2 of 3 -- Add IsDispatchTriggerState + replace Gate 3** (two diffs in one region):

Step 2a: Insert before the `// --- B7-F0: Bracket mirroring methods ---` comment block
(before line 503 in current file):
```csharp
        // B56 T1: IsDispatchTriggerState -- CYC=2. True for states that trigger follower placement.
        // Market orders fire Submitted; AddOn limit orders fire Accepted (skip Submitted).
        // JS-002: returns bool (not null). JS-021: no lock. NT8 confirmed state set.
        // TESTABILITY: internal static -- directly testable via InternalsVisibleTo (same pattern as ShouldMirrorClose).
        internal static bool IsDispatchTriggerState(Order order)
            => order.OrderState == OrderState.Submitted   // market orders
            || order.OrderState == OrderState.Accepted;   // limit orders (AddOn path)

```

Step 2b: Replace Gate 3 (lines 511-513 in current file, +7 after header insert):
```csharp
            // Gate 3: must be a dispatch-trigger state (Submitted for market; Accepted for AddOn limit)
            if (!IsDispatchTriggerState(order))
                return;
```

**Edit 3 of 3 -- Insert Cancelled block in OnOrderUpdate** (after Mirror mode relay, before Gate B):

Insert after line 433 (`MirrorOrderUpdate(e.Order, matchedRule.Value);`), before existing blank line:
```csharp

            // B56 T1: propagate leader cancel to follower entry orders.
            // Fires when leader order is cancelled -- cancels all Initialized/Working
            // follower entry orders for this instrument via CancelOneAccount.
            // Placed BEFORE Gate B so bracket orders are not affected (they have their own path).
            if (e.Order.OrderState == OrderState.Cancelled)
            {
                foreach (var acc in matchedRule.Value.FollowerAccounts)
                {
                    if (acc == null) continue;
                    CancelOneAccount(acc, e.Order.Instrument);
                }
                return;
            }
```

**JS rule constraints**:
| Rule | Status |
|------|--------|
| JS-021 (no lock) | PASS -- `IsDispatchTriggerState` is pure read-only. Cancelled foreach uses existing `CancelOneAccount` which already uses `ToList()` snapshot (no lock). |
| JS-002 (no return null) | PASS -- both constructs return `bool` or `void`. |
| JS-033 (no async void) | PASS -- no async anywhere. |
| JS-001 (no throw in hot path) | PASS -- no new `throw new`. |
| CYC | PASS -- `IsDispatchTriggerState` CYC=2; Cancelled foreach CYC=2. Both <= 8. |
| NT8-031 (no PendingSubmit) | PASS -- only `Submitted`, `Accepted`, `Cancelled` used. All confirmed in NT8's `OrderState` enum. |

**7-scan checklist (SCAN-01 through SCAN-07)**:

| Scan | Command | Pass Criterion |
|------|---------|----------------|
| SCAN-01 | `Select-String "lock(" src/ -Recurse -Include *.cs` | 0 actual lock() calls in new code |
| SCAN-02 | `Select-String "async void " src/ -Recurse -Include *.cs` | 0 async void in new code |
| SCAN-03 | `Select-String "return null" src/ -Recurse -Include *.cs` | 0 new return null instances |
| SCAN-04 | `Select-String "throw new " src/ -Recurse -Include *.cs` | 0 new throw new instances |
| SCAN-05 | `python scripts/complexity_audit.py` on `IsDispatchTriggerState` + Cancelled block | CYC <= 8 for all new methods |
| SCAN-06 | `dotnet build PropTraderTools.csproj` | 0 errors, 0 warnings on new code |
| SCAN-07 | `dotnet test` | T_B56_01 PASS; total 280, 256 pass, 24 fail |

Post-scan: `powershell -File scripts\verify_links.ps1 -Fix` → 0 DESYNC

---

### Ticket 2: Test -- T_B56_01 in CopyEngineTests.cs

**File**: `C:\WSGTA\universal-or-strategy\tests\PropTraderTools.Tests\CopyEngineTests.cs`

**Spec requirements satisfied**: INV-1 through INV-6 (IsDispatchTriggerState predicate correctness)

**Test method signature**:

```csharp
[Fact]
public void IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted()
```

**What the test asserts**:
- `Assert.True(CopyEngine.IsDispatchTriggerState(MakeOrder(OrderState.Submitted)))` -- INV-1
- `Assert.True(CopyEngine.IsDispatchTriggerState(MakeOrder(OrderState.Accepted)))` -- INV-2
- `Assert.False(CopyEngine.IsDispatchTriggerState(MakeOrder(OrderState.Initialized)))` -- INV-3
- `Assert.False(CopyEngine.IsDispatchTriggerState(MakeOrder(OrderState.Working)))` -- INV-4
- `Assert.False(CopyEngine.IsDispatchTriggerState(MakeOrder(OrderState.Filled)))` -- INV-5
- `Assert.False(CopyEngine.IsDispatchTriggerState(MakeOrder(OrderState.Cancelled)))` -- INV-6

**Implementation notes**:
- `MakeOrder(OrderState state)` is a private test helper (already established pattern in
  `CopyEngineTests.cs`) that constructs a stub/mock `Order` with the given `OrderState`.
  If no such helper exists yet, add one following the pattern of existing stub helpers in
  the test file.
- `CopyEngine.IsDispatchTriggerState` is `internal static` -- accessible via
  `InternalsVisibleTo` already configured in the assembly.
- Do NOT use reflection -- the method is `internal`, not `private`.
- CYC of the test method itself = 1 (straight-line, no branches).
- xUnit only -- no NUnit, no MSTest.

**7-scan checklist**:

| Scan | Command | Pass Criterion |
|------|---------|----------------|
| SCAN-01 | `Select-String "lock(" tests/ -Recurse -Include *.cs` | 0 lock() calls |
| SCAN-02 | `Select-String "async void " tests/ -Recurse -Include *.cs` | 0 async void |
| SCAN-03 | N/A (test returns void, no null concern) | N/A |
| SCAN-04 | N/A (test has no throw new) | N/A |
| SCAN-05 | `python scripts/complexity_audit.py` on test method | CYC=1 |
| SCAN-06 | `dotnet build` | 0 errors |
| SCAN-07 | `dotnet test --filter IsDispatchTriggerState_ReturnsTrueForSubmittedAndAccepted` | PASS (6 assertions all green) |

---

## 8. NT8 Rule Compliance Checklist

| Rule | Applies | Status | Notes |
|------|---------|--------|-------|
| NT8-001 (`{ get; init; }` banned) | No | N/A | No new properties |
| NT8-002 (`abstract record` banned) | No | N/A | No new records |
| NT8-003 (`volatile double` banned) | No | N/A | No new double fields |
| NT8-004 (`Immutable` banned) | No | N/A | No immutable collections |
| NT8-005 (`readonly struct` with setter) | No | N/A | No new structs |
| NT8-007 (CreateOrder arg 12 is CustomOrder) | No | N/A | No new CreateOrder calls |
| NT8-013 (`DateTime.Now` banned) | No | N/A | No new DateTime.Now |
| NT8-014 (signal name must start with PTT-) | No | N/A | No new CreateOrder calls |
| NT8-018 (`lock()` banned) | YES | PASS | No lock in new code |
| NT8-019 (`async void` banned) | YES | PASS | No async void in new code |
| NT8-031 (`OrderState.PendingSubmit` doesn't exist) | YES | PASS | Only Submitted/Accepted/Cancelled used -- all confirmed in NT8 |
| NT8-042 (`Dispatcher.InvokeAsync` banned) | No | N/A | No UI marshaling needed |
| NT8-043 (null-conditional compound assignment) | No | N/A | No `?.` on event handlers |

---

## 9. Invariant Map (ptt-verifier confirms independently)

| ID | Assertion | Ticket |
|----|-----------|--------|
| INV-1 | `IsDispatchTriggerState(Submitted)` == `true` | T2 |
| INV-2 | `IsDispatchTriggerState(Accepted)` == `true` | T2 |
| INV-3 | `IsDispatchTriggerState(Initialized)` == `false` | T2 |
| INV-4 | `IsDispatchTriggerState(Working)` == `false` | T2 |
| INV-5 | `IsDispatchTriggerState(Filled)` == `false` | T2 |
| INV-6 | `IsDispatchTriggerState(Cancelled)` == `false` | T2 |
| INV-7 | `DispatchCopy` Gate 3 calls `IsDispatchTriggerState` (grep confirms) | T1 |
| INV-8 | Cancelled block in `OnOrderUpdate` BEFORE `IsWorkingBracket` check | T1 |
| INV-9 | `CancelOneAccount` called per non-null follower on leader Cancelled | T1 |

---

## 10. CYC Analysis

| Method | New branches | CYC | Limit | Status |
|--------|-------------|-----|-------|--------|
| `IsDispatchTriggerState` | `Submitted ==` (1), `Accepted ==` (2) | 2 | 8 | PASS |
| Cancelled `foreach` block in `OnOrderUpdate` | `== Cancelled` (1), `null check` (2) | 2 | 8 | PASS |
| `OnOrderUpdate` total (existing CYC=7 + new Cancelled branch) | +1 | 8 | 8 | PASS (AT LIMIT) |
| `DispatchCopy` (existing CYC=8, Gate 3 replaced not added) | 0 net new | 8 | 8 | PASS (unchanged) |

---

## 11. Build Tag

```
PTT-COPIER B56 | limit-order-gate3-fix + leader-cancel-propagation | 2026-08-09
```

---

## 12. FINAL_PASS Criteria

- [ ] VERIFY_PASS on all 7 scans
- [ ] `IsDispatchTriggerState` method exists in `CopyEngine.cs` as `internal static`
- [ ] `DispatchCopy` Gate 3 calls `IsDispatchTriggerState` (not raw `== Submitted`)
- [ ] Cancelled propagation block present in `OnOrderUpdate` BEFORE `IsWorkingBracket` call (line 436)
- [ ] T_B56_01 PASS -- all 6 `OrderState` assertions correct
- [ ] 0 new `lock()`, 0 new `async void`, 0 new `return null`
- [ ] Hard-link sync PASS (`powershell -File scripts\verify_links.ps1 -Fix`)
- [ ] Build tag prepended to CopyEngine.cs header

---

*Architecture plan complete. Authored by ptt-architect. Handing off to ptt-engineer for Ticket 1 and Ticket 2.*
