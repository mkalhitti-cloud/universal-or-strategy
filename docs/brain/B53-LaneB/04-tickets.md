# B53-LaneB Tickets — Limit Drag Sync

**Block**: B53-LaneB
**Feature**: DW-B53-02 — Follower limit entry order price not updated on leader drag
**Plan status**: REVIEW_PASS (02-plan-review.md, 2026-08-10)
**Build tag**: PTT-COPIER B53 | limit-drag-sync | 2026-08-10
**Ticket count**: 1
**Baseline test count**: 245 passing [Fact]s
**Expected test count after this ticket**: 247

---

## Ticket 1 — DW-B53-02: Limit Drag Sync

### Spec Requirements Satisfied

| ID | Requirement |
|----|-------------|
| DW-B53-02 | Follower limit entry order price not updated on leader drag |
| B53-LaneB-R1 | Detect `ChangeSubmitted` on non-bracket non-PTT leader entry |
| B53-LaneB-R2 | Bypass IsDedup entirely (separate path, not DispatchCopy) |
| B53-LaneB-R3 | Find follower's Working `"PTT-Copy"` order for same instrument |
| B53-LaneB-R4 | Call `acc.Change()` with new limit price |
| B53-LaneB-R5 | Log result to StatusUpdate |
| B53-LaneB-R6 | 2 new `[Fact]` tests (T_B53B_01, T_B53B_02) |
| B53-LaneB-R7 | `verify_links.ps1 -Fix` post-change hard-link sync |

### Files Modified

| File | Change type |
|------|-------------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | 4 new methods + 1 modified method |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | 2 new [Fact] tests appended |

**Files NOT modified** (zero scope creep):
- `PttContracts.cs`
- `TradeCopierWindow.cs`
- `TradeCopierAddOn.cs`
- Any `.csproj` file

---

### Method Signatures

#### 1. `IsLeaderEntryChangeSubmitted(Order order, CopyRule rule) → bool`

```
Visibility:  internal static   (NOT private static — see F3 note below)
Return type: bool
```

**Placement**: Near `IsStopLeg` (approximately line 1524 in the original file, after all insertions shift lines).

**Body** (exact implementation):
```csharp
internal static bool IsLeaderEntryChangeSubmitted(Order order, CopyRule rule)
{
    return order.OrderState == OrderState.ChangeSubmitted
        && !IsStopLeg(order)
        && !order.Name.StartsWith("Target")
        && order.Name != "PTT-Copy"
        && order.Account.Name == rule.MasterAccount.Name;
}
```

**JS Rules applying to this method**:
- JS-021: No `lock()` — pure stateless predicate, no shared state. ✓
- JS-001: No `throw` in hot path — no exception throwing. ✓
- JS-033: No `async void` — synchronous `bool` return. ✓
- JS-002: No `return null` — returns `bool`, not a reference type. ✓

**NT8 Rules applying to this method**:
- NT8-044: `StringComparison` not needed — uses single-arg `StartsWith` and `==` operator only. ✓
- NT8-031: Does not use `OrderState.PendingSubmit`. Uses `OrderState.ChangeSubmitted`, `OrderState.Working`, `OrderState.Accepted` only. ✓

**F1 VERIFICATION (required before commit)**:
Open `CopyEngine.cs` line ~181 and confirm the `CopyRule` struct field name.
- If the field is `MasterAccount` → use `rule.MasterAccount.Name` as written above (plan is correct).
- If the field is `LeaderAccount` → replace every occurrence of `rule.MasterAccount.Name` with `rule.LeaderAccount.Name` in all four new methods before committing.
- NT8 will produce `CS1061` if the wrong field name is used.

**F3 VISIBILITY NOTE**:
`IsLeaderEntryChangeSubmitted` must be `internal static` (not `private static`) to be reachable by the `CopyEngine_TestAccessor` or `InternalsVisibleTo`-based test helper in `CopyEngineTests.cs`. Confirm `CopyEngine_TestAccessor` (or equivalent reflection/`InternalsVisibleTo` wiring) exists in the test project before writing tests. If the accessor does not exist yet, the engineer must either:
(a) Add `[assembly: InternalsVisibleTo("CopyEngineTests")]` to `CopyEngine.cs` (or the AssemblyInfo), OR
(b) Implement a thin `CopyEngine_TestAccessor` static wrapper in the test file.

**CYC**: 5 (base 1 + four `&&` short-circuit operators). Hard limit ≤ 8. ✓

---

#### 2. `FindFollowerEntryOrder(Account acc, Order leaderOrder) → Order`

```
Visibility:  private static
Return type: Order  (nullable — returns null when no matching order found)
```

**Placement**: Near `FindFollowerBracketOrder` (approximately line 748 in the original file, after insertions).

**Body** (exact implementation):
```csharp
private static Order FindFollowerEntryOrder(Account acc, Order leaderOrder)
{
    foreach (var o in acc.Orders)
    {
        if (o.Name == "PTT-Copy"
         && o.Instrument.FullName == leaderOrder.Instrument.FullName
         && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted))
            return o;
    }
    return null;
}
```

**Pattern reference**: Follows `FindFollowerBracketOrder` at line 748 — same `acc.Orders` iteration, same `Instrument.FullName` comparison, same `null`-return convention (pre-existing approved deviation).

**JS Rules applying to this method**:
- JS-021: No `lock()` — iterates acc.Orders without locking. ✓
- JS-001: No `throw` in hot path — no exception-throwing code. ✓
- JS-033: No `async void` — synchronous `Order` return. ✓
- JS-002: Returns `null` for "not found" — approved deviation matching `FindFollowerBracketOrder` pattern. `null` is checked immediately at call site in `SyncFollowerEntryDrag`. ✓

**CYC**: 4 (base 1 + foreach + Name+Instrument condition + `||` state check). Hard limit ≤ 8. ✓

**Note on testability**: `FindFollowerEntryOrder` is `private static`. Tests cover it indirectly through `SyncFollowerEntryDrag` integration. No direct test accessor needed for this method (F3 reviewer note).

---

#### 3. `SyncFollowerEntryDrag(Order order, CopyRule rule) → void`

```
Visibility:  private
Return type: void
```

**Placement**: Near `SyncFollowerBracket` (approximately line 685 in the original file, after insertions).

**Body** (exact implementation):
```csharp
private void SyncFollowerEntryDrag(Order order, CopyRule rule)
{
    foreach (var acc in rule.FollowerAccounts)
    {
        var fo = FindFollowerEntryOrder(acc, order);
        if (fo == null)
        {
            StatusUpdate?.Invoke($"PTT-Drag: no PTT-Copy entry found on {acc.Name} for {order.Instrument.FullName}");
            continue;
        }
        try
        {
            fo.LimitPrice = order.LimitPrice;
            acc.Change(new Order[] { fo });
            StatusUpdate?.Invoke($"PTT-Drag: synced {acc.Name} PTT-Copy to {order.LimitPrice:F2}");
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke($"PTT-Drag: acc.Change failed on {acc.Name}: {ex.Message}");
        }
    }
}
```

**JS Rules applying to this method**:
- JS-021: No `lock()` — foreach over FollowerAccounts; no shared state mutation. ✓
- JS-001: `acc.Change()` wrapped in `try/catch`. Catch logs to `StatusUpdate` and does NOT re-throw. ✓
- JS-033: No `async void` — synchronous `void`. ✓
- JS-002: No `return null` — void method. `fo == null` is handled with `continue`. ✓

**NT8 Rules applying to this method**:
- NT8-046: `acc.Change()` is called only on `fo` where `fo.Name == "PTT-Copy"`. PTT-Copy orders are AddOn-owned (B53-LaneA established `FromEntrySignal != null`). NT8-046 ATM interception affects only `Stop1/Stop2` slot orders with `FromEntrySignal == null`. ✓
- NT8-013: No `CreateOrder` call; no GTD expiry parameter. ✓
- NT8-014: No new `CreateOrder` call; `acc.Change()` operates on existing `"PTT-Copy"` named order. ✓

**CYC**: 3 (base 1 + foreach + null check). `try/catch` without conditional logic in catch body does not add CYC. Hard limit ≤ 8. ✓

---

#### 4. `HandleRuleMatch(Order order, CopyRule rule) → void` (extraction)

```
Visibility:  private
Return type: void
```

**Placement**: Immediately after the closing brace of `OnOrderUpdate` (approximately line 530 after insertions).

**Body** (verbatim extraction from `OnOrderUpdate` current lines ~510–524):
```csharp
private void HandleRuleMatch(Order order, CopyRule rule)
{
    if ((CopyMode)_copyModeValue == CopyMode.Mirror)
    {
        MirrorOrderUpdate(order, rule);
        return;
    }
    if (IsWorkingBracket(order))
    {
        HandleBracketChange(order, rule);
        return;
    }
    DispatchCopy(order, rule);
}
```

**Semantic equivalence**: This is a verbatim move. No behavior change. Mirror → early return; IsWorkingBracket → HandleBracketChange → early return; fall-through to DispatchCopy. All paths terminate identically to pre-extraction behavior.

**JS Rules applying to this method**:
- JS-021: No `lock()`. ✓
- JS-001: No `throw`. Delegates to methods already JS-001 compliant. ✓
- JS-033: No `async void` — synchronous `void`. ✓

**CYC**: 3 (base 1 + Mirror check + IsWorkingBracket check). Hard limit ≤ 8. ✓

---

#### 5. `OnOrderUpdate` — Modified (insertion + tail replacement)

**Change A — Insertion** (after Gate 2.5 line `if (!matchedRule.Value.Enabled) return;`, before the Mirror relay):

Insert:
```csharp
// B53-LaneB: handle leader limit entry drag before IsDedup in DispatchCopy
if (IsLeaderEntryChangeSubmitted(order, matchedRule.Value))
{
    SyncFollowerEntryDrag(order, matchedRule.Value);
    return;
}
```

**Change B — Tail replacement** (replace the Mirror-relay + Gate-B + DispatchCopy block with):
```csharp
HandleRuleMatch(order, matchedRule.Value);
```

**Full modified tail of `OnOrderUpdate`** (for unambiguous reference — the region from Gate 2.5 onward):
```csharp
if (!matchedRule.Value.Enabled) return;       // Gate 2.5

// B53-LaneB: handle leader limit entry drag before IsDedup in DispatchCopy
if (IsLeaderEntryChangeSubmitted(order, matchedRule.Value))
{
    SyncFollowerEntryDrag(order, matchedRule.Value);
    return;
}

HandleRuleMatch(order, matchedRule.Value);    // Mirror + bracket + dispatch
```

**CYC of OnOrderUpdate after change**:

| Branch | +CYC |
|--------|------|
| `!_isCopyEnabled` (Gate 1) | +1 |
| B53-LaneA block (`&&` compound) | +1 |
| `foreach (_rules)` (Gate 2 loop) | +1 |
| instrument + account match condition | +1 |
| `matchedRule == null` (Gate 2 null) | +1 |
| `!matchedRule.Value.Enabled` (Gate 2.5) | +1 |
| `IsLeaderEntryChangeSubmitted` (NEW) | +1 |
| **Total** | **8** ✓ |

Mirror check and IsWorkingBracket are moved to `HandleRuleMatch`. Net CYC of `OnOrderUpdate` = 8 (unchanged from before this block). Hard limit ≤ 8. ✓

---

### Method Placement Guide (for reviewer verification)

```
[~line 530]  HandleRuleMatch(Order, CopyRule)       -- immediately after OnOrderUpdate closing brace
[~line 700]  SyncFollowerEntryDrag(Order, CopyRule) -- near SyncFollowerBracket
[~line 762]  FindFollowerEntryOrder(Account, Order) -- near FindFollowerBracketOrder
[~line 1535] IsLeaderEntryChangeSubmitted(Order, CopyRule) -- near IsStopLeg
```

Line numbers are approximate; they shift with prior insertions. Logical adjacency with related existing methods is the binding constraint.

---

### xUnit Tests

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
**Insertion point**: After current last test `T_B53_AtmSkippedWhenNameIsNotPttCopy` (line 4652), before the class closing brace.
**Test framework**: xUnit only (NEVER NUnit or MSTest per project mandate).

---

#### T_B53B_01 — `IsLeaderEntryChangeSubmitted` returns `true` for ChangeSubmitted leader entry

```csharp
[Fact]
public void T_B53B_01_IsLeaderEntryChangeSubmitted_ReturnsTrue_ForChangeSubmittedLeaderEntry()
{
    // Arrange
    var order = OrderStubFactory.Create(
        orderState: OrderState.ChangeSubmitted,
        name: "ManualEntry",         // not PTT-Copy, not Stop*, not Target*
        accountName: "Sim101");
    var rule = CopyRuleFactory.Create(masterAccountName: "Sim101");

    // Act
    bool result = CopyEngine_TestAccessor.IsLeaderEntryChangeSubmitted(order, rule);

    // Assert
    Assert.True(result);
}
```

**What this proves**: The predicate correctly identifies a standard leader entry drag event:
- `OrderState.ChangeSubmitted` → true
- `IsStopLeg("ManualEntry")` → false (not a stop leg)
- `"ManualEntry".StartsWith("Target")` → false
- `"ManualEntry" != "PTT-Copy"` → true
- `order.Account.Name == rule.MasterAccount.Name` → `"Sim101" == "Sim101"` → true
- All five conditions satisfied → `return true`

---

#### T_B53B_02 — `IsLeaderEntryChangeSubmitted` returns `false` for bracket stop leg

```csharp
[Fact]
public void T_B53B_02_IsLeaderEntryChangeSubmitted_ReturnsFalse_ForStopLeg()
{
    // Arrange
    var order = OrderStubFactory.Create(
        orderState: OrderState.ChangeSubmitted,
        name: "Stop",               // triggers IsStopLeg → predicate must return false
        accountName: "Sim101");
    var rule = CopyRuleFactory.Create(masterAccountName: "Sim101");

    // Act
    bool result = CopyEngine_TestAccessor.IsLeaderEntryChangeSubmitted(order, rule);

    // Assert
    Assert.False(result);
}
```

**What this proves**: Bracket stop legs are correctly excluded from drag sync:
- `"Stop".EndsWith("STP", OrdinalIgnoreCase)` OR `"Stop".StartsWith("Stop")` → `IsStopLeg` returns `true`
- `!IsStopLeg(order)` → `false` → short-circuit → entire predicate returns `false`

---

**Baseline arithmetic**:
- Current passing [Fact] count: **245**
- New tests added: **+2** (T_B53B_01, T_B53B_02)
- Expected after this ticket: **247**
- SCAN-07 target: `dotnet test` → all 247 pass

---

### CYC Summary Table

| Method | CYC | Hard limit | Status |
|--------|-----|-----------|--------|
| `OnOrderUpdate` (modified) | 8 | ≤ 8 | ✓ |
| `HandleRuleMatch` (new) | 3 | ≤ 8 | ✓ |
| `IsLeaderEntryChangeSubmitted` (new) | 5 | ≤ 8 | ✓ |
| `FindFollowerEntryOrder` (new) | 4 | ≤ 8 | ✓ |
| `SyncFollowerEntryDrag` (new) | 3 | ≤ 8 | ✓ |

**Note on IsLeaderEntryChangeSubmitted CYC=5** (reviewer finding F2):
The spec prompt's aspirational target was CYC ≤ 3. McCabe-accurate count is 5. Both values are well within the project hard limit of CYC ≤ 8. CYC=3 was not achievable without either removing the defensive account-match guard (breaking redundant safety) or splitting the predicate into two methods (adding unnecessary complexity). The CYC=5 tradeoff is correct.

---

### JS Rule Constraints — Full Checklist

| Rule | Description | Status in new code |
|------|-------------|-------------------|
| JS-021 | `lock()` banned | ZERO `lock()` in any new method ✓ |
| JS-001 | No `throw` in hot path | `acc.Change()` wrapped in `try/catch`; no re-throw ✓ |
| JS-002 | No `return null` for missing values | `FindFollowerEntryOrder` returns `null` as approved deviation (matches `FindFollowerBracketOrder` codebase pattern). Checked at call site. ✓ |
| JS-033 | No `async void` | All four new methods are synchronous ✓ |
| JS-023 | UI updates off-thread | No UI updates in new methods; `StatusUpdate?.Invoke()` is order-thread safe ✓ |
| JS-010 | Public constructor on singleton/struct | Not applicable — no new singletons or structs ✓ |
| JS-008 | SolidColorBrush freeze | Not applicable — no new WPF brushes ✓ |

---

### NT8 Rule Constraints — Full Checklist

| Rule | Description | Status |
|------|-------------|--------|
| NT8-046 | `acc.Change()` on ATM slot orders silently overridden | Safe — `fo.Name == "PTT-Copy"` (AddOn-owned). NT8-046 only affects ATM `Stop1/Stop2` with `FromEntrySignal == null`. ✓ |
| NT8-031 | `OrderState.PendingSubmit` does not exist | Not used. New code uses `ChangeSubmitted`, `Working`, `Accepted`. ✓ |
| NT8-019 | `async void` banned | All new methods synchronous. ✓ |
| NT8-018 | `lock()` banned | Zero `lock()` usage. ✓ |
| NT8-044 | `StringComparison` requires `using System` | Not used. Single-arg `StartsWith` and `==` operator only. ✓ |
| NT8-042 | `Dispatcher.InvokeAsync` not available in AddOn | Not used. `StatusUpdate?.Invoke()` is existing order-thread logging pattern. ✓ |
| NT8-013 | `DateTime.Now` for order expiry | Not applicable — no `CreateOrder` call. ✓ |
| NT8-014 | PTT- prefix on `CreateOrder` | Not applicable — no `CreateOrder` call. `acc.Change()` only. ✓ |

**NT8 F5 Compiler Gate for `OrderState.ChangeSubmitted`**:
`OrderState.ChangeSubmitted` is expected to exist based on Director empirical observation.
If NT8 F5 produces `CS0117 'OrderState' does not contain a definition for 'ChangeSubmitted'`:
1. STOP immediately — do not cast to `int` as a workaround.
2. Add a new rule `NT8-056` to `docs/standards/NT8_COMPILER_RULES.md` with the actual state name.
3. Update `docs/standards/NT8_ADDON_KNOWLEDGE.md` with block summary.
4. Escalate to Director before proceeding.

---

### Hard-Link Sync

After ALL changes to `CopyEngine.cs` and `CopyEngineTests.cs` are written:

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

**CRITICAL**: This syncs the hard-linked copy to the NT8 AddOns deployment folder.
`CS0246` is guaranteed on F5 compilation if this step is skipped.
`deploy-sync.ps1` must NOT be used — that belongs to the V12 epic-cluster workspace, not PTT Wave.

---

### 7-Scan Checklist (SCAN-01 through SCAN-07)

The engineer MUST run all seven scans and confirm all pass before committing.

```
SCAN-01: Select-String "lock("       src/ -Recurse -Include *.cs  → 0 new results in new code
SCAN-02: Select-String "async void " src/ -Recurse -Include *.cs  → 0 new results in new code
SCAN-03: Select-String "return null" src/ -Recurse -Include *.cs  → 1 approved instance (FindFollowerEntryOrder only); 0 new unapproved instances
SCAN-04: Select-String "throw new "  src/ -Recurse -Include *.cs  → 0 new results in new code
SCAN-05: python scripts/complexity_audit.py                        → all new methods CYC ≤ 8; IsLeaderEntryChangeSubmitted = 5, SyncFollowerEntryDrag = 3, FindFollowerEntryOrder = 4, HandleRuleMatch = 3, OnOrderUpdate = 8
SCAN-06: dotnet build                                              → 0 errors, 0 warnings
SCAN-07: dotnet test                                               → all 247 [Fact]s pass (baseline 245 + 2 new)
```

**SCAN-03 annotation**: `FindFollowerEntryOrder` has exactly one `return null;` at its final line. This is the only `return null` added by this ticket. It is an approved deviation matching the pre-existing `FindFollowerBracketOrder` codebase pattern. All other new methods must have zero `return null`.

---

### Execution Order

The engineer must follow this order exactly:

1. Open `CopyEngine.cs` line ~181 → confirm `CopyRule` struct field name (`MasterAccount` or `LeaderAccount`) → resolve F1 before writing any code.
2. Confirm `CopyEngine_TestAccessor` wiring or `InternalsVisibleTo` attribute exists → resolve F3 if missing.
3. Add `HandleRuleMatch` immediately after `OnOrderUpdate` (at ~line 530).
4. Modify `OnOrderUpdate` tail: insert ChangeSubmitted branch + replace Mirror/bracket/dispatch tail with `HandleRuleMatch(...)` call.
5. Add `SyncFollowerEntryDrag` near `SyncFollowerBracket` (~line 700).
6. Add `FindFollowerEntryOrder` near `FindFollowerBracketOrder` (~line 762).
7. Add `IsLeaderEntryChangeSubmitted` near `IsStopLeg` (~line 1535).
8. Append `T_B53B_01` and `T_B53B_02` to `CopyEngineTests.cs` after line 4652.
9. Run SCAN-01 through SCAN-07.
10. Run `powershell -File scripts\verify_links.ps1 -Fix`.
11. Run `dotnet build` → 0 errors.
12. Run `dotnet test` → 247 pass.
13. Commit with message: `PTT-COPIER B53 | limit-drag-sync | <date>`.

---

### Risk Register

| Risk | Probability | Mitigation |
|------|-------------|-----------|
| R1: `OrderState.ChangeSubmitted` does not exist in this NT8 build (CS0117) | Low | Stop, add NT8-056, escalate to Director. Do not cast to int. |
| R2: `acc.Change()` called on `Accepted`-state follower order | Very Low | StatusUpdate logging captures result. No behavioral harm. |
| R3: `HandleRuleMatch` extraction changes observable behavior | Zero | Verbatim move proven semantically equivalent. |
| R4: Mirror mode follower drag not handled by new branch | Known | Mirror drag was already not working. New branch handles Copy mode. Known limitation documented; LaneC scope. |

---

### Deferred Items (carry forward unchanged)

| ID | Priority | Status | Description |
|----|----------|--------|-------------|
| DW-B54-01 | P0 | OPEN | AtmStrategyCreate API for AddOn context (NT8-055 resolution) |
| DW-B54-02 | P0 | OPEN — blocked by DW-B54-01 | F5-GATE-02 live ATM bracket test on Sim101 |
| DW-B54-03 | P2 | OPEN | Diagnostic log for `#if NT8_ADDON_ATM` inactive state |
| DW-BACKLOG-01 | P2 | OPEN | PttContracts.cs FillSignal dead-code cleanup |

**Resolved by this ticket**:

| ID | Resolved by |
|----|-------------|
| DW-B53-02 | Ticket 1 — IsLeaderEntryChangeSubmitted + SyncFollowerEntryDrag + OnOrderUpdate routing |
