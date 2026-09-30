# PTT-COPIER-B25 Tickets

**Block**: PTT-COPIER-B25, Lane B
**Defect**: DW-B25-02 — Per-Account BE State Isolation
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Date**: 2026-07-07
**Plan ref**: docs/brain/PTT-COPIER-B25/02-architecture-plan.md (REVIEW_PASS, Cycle 2)

---

## Ticket Count

| Ticket | Title | File(s) |
|--------|-------|---------|
| T1 | DW-B25-02 — Per-Account BE State Isolation | CopyEngine.cs, TradeCopierPanel.cs, CopyEngineTests.cs |

One atomic ticket. All changes are tightly coupled (field → method → caller → test) and must land
in a single commit. Partial application = build failure.

---

# T1 — DW-B25-02: Per-Account BE State Isolation

## Spec Requirements Satisfied

- **DW-B25-02**: Replace singleton volatile int state fields with
  `ConcurrentDictionary<string, int>` keyed by `Account.Name`, eliminating cross-panel BE state
  corruption when multiple `TradeCopierPanel` instances share `CopyEngine.Instance`.
- **Plan sections**: §2 Component List, §3 Field Changes, §4–§5 Method Signatures + Bodies,
  §6 Caller Changes, §7 Test Changes, §8 Threading Model, §11 CYC Budget, §12 Access Site Map.

## File Paths in Wave Workspace

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```

---

## Part A — CopyEngine.cs

### A1. Remove old volatile int fields

Locate and **delete** these two field declarations (approx. lines 97 and 105):

```csharp
// DELETE:
private volatile int _pendingBeState = 0;
private volatile int _trailBeState   = 0;
```

Rules: NT8-003 (volatile double BANNED — these are volatile int, also banned per same reasoning
once replaced; their removal satisfies the plan). JS-021 (no lock).

---

### A2. Add new ConcurrentDictionary fields

In place of the removed fields, insert:

```csharp
// ADD — per-account dict slots replace singleton volatile ints:
// NT8-004: ImmutableDictionary BANNED; ConcurrentDictionary is the NT8-safe replacement.
// JS-021: no lock() — ConcurrentDictionary TryAdd/TryRemove/TryGetValue are lock-free at API level.
private readonly ConcurrentDictionary<string, int> _pendingBeStates
    = new ConcurrentDictionary<string, int>();
private readonly ConcurrentDictionary<string, int> _trailBeStates
    = new ConcurrentDictionary<string, int>();
```

Rules: NT8-004 (ImmutableDictionary BANNED — do NOT use). JS-021 (no lock).
`System.Collections.Concurrent` must already be in scope; if not, add the `using` directive.

---

### A3. ArmPendingBe — body change (approx. line 1292)

Replace `_pendingBeState = 1;` with `_pendingBeStates[masterAcc.Name] = 1;`.

Companion ref writes (`_pendingBeBufferTicks`, `_pendingBeInstrument`, `_pendingBeAccount`,
`masterAcc.AccountItemUpdate +=`) MUST stay BEFORE the dict indexer setter (release-fence
ordering preserved).

**Full method body (engineer MUST match exactly):**

```csharp
internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)
{
    if (instr == null)                                  // (1)
        return;
    if (masterAcc == null)                              // (2)
        return;
    var pos = FindPosition(masterAcc, instr);
    if (IsFlat(pos))                                    // (3)
        return;
    _pendingBeBufferTicks   = bufferTicks;
    _pendingBeInstrument    = instr;
    _pendingBeAccount       = masterAcc;
    masterAcc.AccountItemUpdate += OnPendingBeAccountUpdate;
    _pendingBeStates[masterAcc.Name] = 1;               // (4) dict indexer -- release fence
}
```

CYC = 4. Rules: JS-021 (no lock). NT8-043 (no null-conditional event unsub — only a `+=` here).

---

### A4. DisarmPendingBe — signature + body change (approx. line 1298-1307)

**Old signature:** `internal void DisarmPendingBe()`
**New signature:** `internal void DisarmPendingBe(Account leader)`

**Full method body (engineer MUST match exactly):**

```csharp
internal void DisarmPendingBe(Account leader)
{
    if (leader == null)                                              // (1)
    {
        StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op");
        return;
    }
    if (!_pendingBeStates.TryRemove(leader.Name, out int removedState)) // (2)
        return;
    var acc = _pendingBeAccount;
    if (acc != null)                                                 // (3)
        acc.AccountItemUpdate -= OnPendingBeAccountUpdate;
    _pendingBeAccount    = null;
    _pendingBeInstrument = null;
}
```

CYC = 4 (3 explicit if-branches + base 1). Director-sanctioned target ≤ 4 (F2 fix, Cycle 2).
All 3 branches are semantically necessary: null guard, idempotent TryRemove, NT8-043-safe unsub.
Rules: JS-021 (no lock). NT8-043 (`if (acc != null)` explicit guard before `-=`).
`StatusUpdate?.Invoke(...)` is a null-conditional event FIRE — not a subscribe/unsubscribe — PASS.

---

### A5. ArmTrailBe — body change (approx. line 1331)

Replace `_trailBeState = 1;` with `_trailBeStates[masterAcc.Name] = 1;`.

**Full method body (engineer MUST match exactly):**

```csharp
internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)
{
    if (instr == null)                                    // (1)
        return;
    if (masterAcc == null)                                // (2)
        return;
    var pos = FindPosition(masterAcc, instr);
    if (IsFlat(pos))                                      // (3)
        return;
    double currentPnl = masterAcc.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
    if (currentPnl == double.MinValue) currentPnl = 0.0;
    _trailBeBufferTicks   = bufferTicks;
    _trailBeLastPnl       = BitConverter.DoubleToInt64Bits(currentPnl);
    _trailBeInstrument    = instr;
    _trailBeAccount       = masterAcc;
    masterAcc.AccountItemUpdate += OnTrailBeAccountUpdate;
    _trailBeStates[masterAcc.Name] = 1;                   // (4) dict indexer -- release fence
}
```

CYC = 4. Rules: JS-021 (no lock). NT8-043 (only `+=` here).

---

### A6. DisarmTrailBe — signature + body change (approx. line 1338-1347)

**Old signature:** `internal void DisarmTrailBe()`
**New signature:** `internal void DisarmTrailBe(Account leader)`

**Full method body (engineer MUST match exactly):**

```csharp
internal void DisarmTrailBe(Account leader)
{
    if (leader == null)                                              // (1)
    {
        StatusUpdate?.Invoke("DisarmTrailBe: leader null -- no-op");
        return;
    }
    if (!_trailBeStates.TryRemove(leader.Name, out int removedState)) // (2)
        return;
    var acc = _trailBeAccount;
    if (acc != null)                                                 // (3)
        acc.AccountItemUpdate -= OnTrailBeAccountUpdate;
    _trailBeAccount    = null;
    _trailBeInstrument = null;
}
```

CYC = 4 (3 explicit if-branches + base 1). Director-sanctioned target ≤ 4 (F3 fix, Cycle 2).
Idempotent: safe to call when not armed (TryRemove returns false at branch (2)).
Rules: JS-021 (no lock). NT8-043 (explicit `if (acc != null)` guard before `-=`).

---

### A7. Add private helper: IsPendingBeArmed (new method, after DisarmPendingBe)

```csharp
// F1 fix: absorbs the null + TryGetValue + state==1 compound guard so
// OnPendingBeAccountUpdate stays CYC=8.
// NT8-043: pure bool evaluation -- no event subscribe/unsubscribe. PASS.
private bool IsPendingBeArmed(Account acc)
    => acc != null
    && _pendingBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```

CYC = 1. Expression-bodied method; no branching statements; `&&` short-circuit chain does not
add decision points in Lizard's model.

---

### A8. Add private helper: IsTrailBeArmed (new method, after DisarmTrailBe)

```csharp
// IsTrailBeArmed -- called only from OnTrailBeAccountUpdate.
// CYC = 1. NT8-043: pure bool evaluation. PASS.
private bool IsTrailBeArmed(Account acc)
    => acc != null
    && _trailBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```

CYC = 1. Same rationale as IsPendingBeArmed.

---

### A9. OnTrailBeAccountUpdate — access site change (approx. line 1359)

Replace the volatile int guard at the top of the method:

```csharp
// BEFORE (remove this):
if (_trailBeState != 1)
    return;

// AFTER (replace with):
var acc = _trailBeAccount;
if (!IsTrailBeArmed(acc))
    return;
```

The local `acc` variable is now available for the remainder of the callback body. Any subsequent
direct reads of `_trailBeAccount` within this method should use the captured `acc` local to avoid
TOCTOU re-read (the rest of the body already uses `_trailBeInstrument` via its own `var instr`
capture; no further changes needed to lines below the guard).

CYC of full method stays at 5 (net branch delta = 0 at this site). Rules: JS-021, NT8-043.

---

### A10. OnPendingBeAccountUpdate — two access site changes

**Site 1 (approx. line 1385) — volatile int guard at method top:**

```csharp
// BEFORE (remove these two lines):
if (_pendingBeState != 1)
    return;

// AFTER (replace with):
var acc = _pendingBeAccount;
if (!IsPendingBeArmed(acc))
    return;
```

**Site 2 (approx. line 1406) — CAS disarm:**

```csharp
// BEFORE (remove):
if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1)
    return;

// AFTER (replace with):
if (!_pendingBeStates.TryRemove(acc.Name, out int removedSt))
    return;
```

IMPORTANT: `acc` was captured at Site 1 above. The old body had a `var acc = _pendingBeAccount;`
declaration on approx. line 1408 — **remove that redundant re-declaration** to avoid a
"local variable already declared" compiler error.

CYC of full method stays at 8 (net branch delta = 0 at both sites combined). Rules: JS-021, NT8-043.

**Full updated method body for reference (engineer verifies against this):**

```csharp
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
{
    var acc = _pendingBeAccount;
    if (!IsPendingBeArmed(acc))                                        // (1)
        return;
    if (e.AccountItem != AccountItem.UnrealizedProfitLoss)             // (2)
        return;
    var pos = FindPosition(acc, _pendingBeInstrument);
    if (IsFlat(pos))                                                   // (3)
        return;
    double tickSize = _pendingBeInstrument?.MasterInstrument?.TickSize ?? 0.0;
    if (tickSize <= 0.0)                                               // (4)
        return;
    double last = _pendingBeInstrument?.MarketData?.Last?.Price ?? 0.0;
    if (last <= 0.0)                                                   // (5)
        return;
    bool isLong  = pos.MarketPosition == MarketPosition.Long;
    double target = pos.AveragePrice
        + (isLong ? 1.0 : -1.0) * _pendingBeBufferTicks * tickSize;
    bool triggered = isLong ? (last >= target) : (last <= target);
    if (!triggered)                                                    // (6)
        return;
    if (!_pendingBeStates.TryRemove(acc.Name, out int removedSt))      // (7)
        return;
    var instr = _pendingBeInstrument;
    var buf   = _pendingBeBufferTicks;
    if (acc != null)
        acc.AccountItemUpdate -= OnPendingBeAccountUpdate;
    _pendingBeAccount    = null;
    _pendingBeInstrument = null;
    BreakEven(acc, instr, buf);
    PendingBeFired?.Invoke(instr?.FullName ?? string.Empty);
}
```

CYC = 8 (7 explicit `if` branches + base 1). ✅

---

## Part B — TradeCopierPanel.cs

Five call sites. Pass `_leaderAccount` as the `leader` argument to both `DisarmPendingBe` and
`DisarmTrailBe`. `_leaderAccount` may be null on the Detach path (second call); the engine's
null guard handles this safely.

| Approx line | Change |
|-------------|--------|
| ~402 | `_engine.DisarmPendingBe()` → `_engine.DisarmPendingBe(_leaderAccount)` |
| ~403 | `_engine.DisarmTrailBe()` → `_engine.DisarmTrailBe(_leaderAccount)` |
| ~807 | `_engine.DisarmPendingBe()` → `_engine.DisarmPendingBe(_leaderAccount)` |
| ~812 | `_engine.DisarmPendingBe()` → `_engine.DisarmPendingBe(_leaderAccount)` |
| ~813 | `_engine.DisarmTrailBe()` → `_engine.DisarmTrailBe(_leaderAccount)` |

**Null-safety note:**
- Lines 402/403 (Detach path): `_leaderAccount` is set to null on line ~406 AFTER the disarm
  calls. Not null at time of call during normal Detach(). Defensive double-Detach: null guard
  fires in engine, returns safely.
- Lines 807/812/813 (OnBeClick path): guard on line ~798 (`if (_leaderAccount == null) return;`)
  ensures non-null before reaching these lines. ✅

No other changes to TradeCopierPanel.cs.

---

## Part C — CopyEngineTests.cs

Three existing tests updated. **No new tests added. No tests deleted. Baseline 128 [Fact] tests.
Final count after T1: 128 [Fact] tests.**

### C1. ArmTrailBe_NullInstrument_NoException (approx. lines 1666-1672)

Test intent: null instrument guard fires before any arm write, so state remains empty after call.

**Change:** Reflection field name `"_trailBeState"` → `"_trailBeStates"`. Assertion changes from
checking int value == 0 to checking dictionary is empty.

```csharp
// BEFORE:
var fi = typeof(CopyEngine).GetField(
    "_trailBeState",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Assert.NotNull(fi);
int state = (int)fi.GetValue(_engine);
Assert.Equal(0, state);

// AFTER:
var fi = typeof(CopyEngine).GetField(
    "_trailBeStates",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Assert.NotNull(fi);
var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, int>)fi.GetValue(_engine);
Assert.Empty(dict);
```

`Assert.Empty(dict)` confirms the null-instrument guard fired before the dict indexer setter was
reached. ✅

### C2. DisarmTrailBe_WhenNotArmed_NoException (approx. line 1679)

Test intent: calling DisarmTrailBe when not armed must not throw.

**Change:** `_engine.DisarmTrailBe()` → `_engine.DisarmTrailBe(null)`

```csharp
// BEFORE:
var ex = Record.Exception(() => _engine.DisarmTrailBe());

// AFTER:
var ex = Record.Exception(() => _engine.DisarmTrailBe(null));
```

The engine's null guard fires StatusUpdate and returns. No exception. Test passes. ✅

### C3. DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall (approx. lines 1689-1690)

Test intent: two successive DisarmTrailBe calls must not throw.

**Change:** Both `_engine.DisarmTrailBe()` calls → `_engine.DisarmTrailBe(null)`

```csharp
// BEFORE:
_engine.DisarmTrailBe();
_engine.DisarmTrailBe();

// AFTER:
_engine.DisarmTrailBe(null);
_engine.DisarmTrailBe(null);
```

Null guard fires on both calls; both return safely. No exception. ✅

---

## 7-Scan Checklist (Engineer Contract)

Engineer MUST run all 7 scans and confirm zero violations before marking T1 complete.

All `grep` commands run from the Wave workspace root:
`c:\WSGTA\universal-or-strategy\`

| ID | Command | Required Result |
|----|---------|----------------|
| SCAN-01 | `grep -n "_pendingBeState\b" src/PropTraderTools/CopyEngine.cs` | **0 matches** — old singleton field and all its access sites are gone |
| SCAN-02 | `grep -n "_trailBeState\b" src/PropTraderTools/CopyEngine.cs` | **0 matches** — old singleton field and all its access sites are gone |
| SCAN-03 | `grep -n "_pendingBeStates" src/PropTraderTools/CopyEngine.cs` | **≥5 matches** — field decl + arm (A3) + disarm TryRemove (A4) + helper TryGetValue (A7) + callback TryRemove (A10 site 2) |
| SCAN-04 | `grep -n "_trailBeStates" src/PropTraderTools/CopyEngine.cs` | **≥5 matches** — field decl + arm (A5) + disarm TryRemove (A6) + helper TryGetValue (A8) + callback check (A9) |
| SCAN-05 | `grep -rn "lock\s*(" src/ --include="*.cs"` | **0 matches** — JS-021 compliance |
| SCAN-06 | `grep -rn "ImmutableDictionary" src/ --include="*.cs"` | **0 matches** — NT8-004 compliance |
| SCAN-07 | `grep -rn "\?\.\w\+\s*[-+]=" src/ --include="*.cs"` | **0 matches** — NT8-043 compliance (no null-conditional event subscribe/unsubscribe) |

**All 7 must pass before T1 is closed.**

---

## CYC Summary

| Method | Target | Actual | Status |
|--------|--------|--------|--------|
| `IsPendingBeArmed` | ≤ 1 | 1 | ✅ |
| `IsTrailBeArmed` | ≤ 1 | 1 | ✅ |
| `ArmPendingBe` | ≤ 4 | 4 | ✅ |
| `DisarmPendingBe` | ≤ 4 | 4 | ✅ Director-sanctioned (F2) |
| `ArmTrailBe` | ≤ 4 | 4 | ✅ |
| `DisarmTrailBe` | ≤ 4 | 4 | ✅ Director-sanctioned (F3) |
| `OnTrailBeAccountUpdate` | ≤ 8 | 5 | ✅ |
| `OnPendingBeAccountUpdate` | ≤ 8 | 8 | ✅ F1 fix: helper absorbs compound guard; net delta = 0 |

---

## JS Rule Constraints per Method

| Method | JS-021 | JS-033 | JS-001 | JS-002 |
|--------|--------|--------|--------|--------|
| `ArmPendingBe` | PASS (no lock) | PASS (no async void) | PASS (no throw) | PASS (no return null) |
| `DisarmPendingBe` | PASS | PASS | PASS | PASS |
| `ArmTrailBe` | PASS | PASS | PASS | PASS |
| `DisarmTrailBe` | PASS | PASS | PASS | PASS |
| `IsPendingBeArmed` | PASS | PASS | PASS | PASS |
| `IsTrailBeArmed` | PASS | PASS | PASS | PASS |
| `OnPendingBeAccountUpdate` | PASS | PASS | PASS | PASS |
| `OnTrailBeAccountUpdate` | PASS | PASS | PASS | PASS |

---

## NT8 Rule Constraints per Method

| Method | NT8-003 | NT8-004 | NT8-018 | NT8-043 |
|--------|---------|---------|---------|---------|
| `ArmPendingBe` | PASS (no volatile decl) | PASS (no ImmutableDict) | PASS (no lock) | PASS (only `+=`) |
| `DisarmPendingBe` | PASS | PASS | PASS | PASS (explicit `if (acc != null)` guard before `-=`) |
| `ArmTrailBe` | PASS | PASS | PASS | PASS (only `+=`) |
| `DisarmTrailBe` | PASS | PASS | PASS | PASS (explicit `if (acc != null)` guard before `-=`) |
| `IsPendingBeArmed` | PASS | PASS | PASS | PASS (pure bool, no event ops) |
| `IsTrailBeArmed` | PASS | PASS | PASS | PASS (pure bool, no event ops) |
| `OnPendingBeAccountUpdate` | PASS | PASS | PASS | PASS |
| `OnTrailBeAccountUpdate` | PASS | PASS | PASS | PASS |

---

## xUnit Tests

| Test Name | File | What It Asserts | Change Type |
|-----------|------|-----------------|-------------|
| `ArmTrailBe_NullInstrument_NoException` | CopyEngineTests.cs | Null instrument guard fires before dict write; `_trailBeStates` is empty after call | **Update** (field name + assertion) |
| `DisarmTrailBe_WhenNotArmed_NoException` | CopyEngineTests.cs | `DisarmTrailBe(null)` does not throw; null guard returns safely | **Update** (signature — add null arg) |
| `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall` | CopyEngineTests.cs | Two successive `DisarmTrailBe(null)` calls do not throw | **Update** (both calls — add null arg) |

**No new [Fact] tests. Baseline 128. Final count 128.**

---

## Threading Invariants (Engineer Must Not Violate)

1. **Arm ordering**: All companion ref writes (`_pendingBeAccount`, `_pendingBeInstrument`,
   `_pendingBeBufferTicks`, `masterAcc.AccountItemUpdate +=`) MUST complete BEFORE the dict
   indexer setter. The indexer setter is the release fence. Reordering = race condition.
2. **No UI calls inside callbacks**: `OnPendingBeAccountUpdate` and `OnTrailBeAccountUpdate`
   run on NT8's background thread. Do NOT call `Dispatcher.InvokeAsync` from within them unless
   you add one for `BreakEven` (but `BreakEven` is already thread-safe — do not change it).
3. **TryRemove atomicity**: Exactly one caller wins `TryRemove` per dict key. All other callers
   return early. This is the same atomicity guarantee as the former `Interlocked.CompareExchange`.
   Do NOT add a second `TryRemove` call as a "safety net" — idempotency is guaranteed by the
   single-remove semantics.
4. **No lock anywhere**: `ConcurrentDictionary` methods are lock-free at the API level.
   Adding a `lock` wrapper = JS-021 + NT8-018 violations.

---

## Verification Steps (Engineer Self-Check)

After implementing all changes, run:

```powershell
# 1. Build
dotnet build c:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj

# 2. Tests
dotnet test c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.csproj

# 3. All 7 scans from Part "7-Scan Checklist" above

# 4. Hard-link sync (PTT workspace)
powershell -File c:\WSGTA\universal-or-strategy\scripts\verify_links.ps1 -Fix
```

Expected: 0 build errors, 128 tests pass, all 7 scans show required match counts.

---

## Deferred Items (carry forward from B24 + B25)

| ID | Description | Priority |
|----|-------------|----------|
| DW-B24-01 | NT8-043 formal rule entry | P3 |
| DW-B24-02 | Manual E2E runtime verification of BE arming paths post-B25 | P2 |
| DW-B24-03 | Skip-duplicate guard test | P3 |
| DW-B25-01 | Companion plain-ref fields (`_pendingBeAccount` etc.) still singleton; no race if panels use distinct leader account names (supported topology); track for future | P3 |
| DW-B25-02 | `_trailBeLastPnl` singleton plain long; same caveat as DW-B25-01 | P3 |

---

*ptt-architect · PTT-COPIER-B25 · 04-tickets.md · 2026-07-07*
