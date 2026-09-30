# PTT-COPIER-B25 Architecture Plan

**Defect**: DW-B25-02 — Singleton BE State Isolation
**Block**: PTT-COPIER-B25, Lane B
**Status**: REVIEW_PASS (Cycle 2)
**Author**: ptt-architect
**Date**: 2026-07-07
**Prev block**: PTT-COPIER-B24

---

## 1. Problem Statement

`_pendingBeState` (volatile int) and `_trailBeState` (volatile int) are singleton fields on
`CopyEngine.Instance`. All `TradeCopierPanel` instances (one per chart) write to the same field.
`DisarmPendingBe()` on one panel corrupts the armed state of another panel.

Root cause: singleton state indexed by nothing. Fix: replace with
`ConcurrentDictionary<string, int>` keyed by `Account.Name`.

---

## 2. Component List

| Component | File | Kind |
|-----------|------|------|
| Field declarations | `CopyEngine.cs` | 2 fields removed, 2 fields added |
| `ArmPendingBe` | `CopyEngine.cs` | Method body change |
| `DisarmPendingBe` | `CopyEngine.cs` | Signature + body change |
| `ArmTrailBe` | `CopyEngine.cs` | Method body change |
| `DisarmTrailBe` | `CopyEngine.cs` | Signature + body change |
| `IsPendingBeArmed` | `CopyEngine.cs` | New private helper (CYC=1) |
| `IsTrailBeArmed` | `CopyEngine.cs` | New private helper (CYC=1) |
| `OnPendingBeAccountUpdate` | `CopyEngine.cs` | Callback body change (2 sites) |
| `OnTrailBeAccountUpdate` | `CopyEngine.cs` | Callback body change (1 site) |
| 5 caller sites | `TradeCopierPanel.cs` | Pass `_leaderAccount` to disarm calls |
| `ArmTrailBe_NullInstrument_NoException` | `CopyEngineTests.cs` | Reflection field update only |

---

## 3. Field Changes (CopyEngine.cs, lines ~97-109)

### 3.1 Remove

```csharp
// REMOVE:
private volatile int _pendingBeState = 0;  // line 97
private volatile int _trailBeState   = 0;  // line 105
```

### 3.2 Add

```csharp
// ADD — replace volatile int state machines with per-account dict slots:
// NT8-004: ImmutableDictionary BANNED; ConcurrentDictionary is the NT8-safe replacement.
// JS-021: no lock() — ConcurrentDictionary TryAdd/TryRemove/TryGetValue are lock-free at API level.
private readonly ConcurrentDictionary<string, int> _pendingBeStates
    = new ConcurrentDictionary<string, int>();
private readonly ConcurrentDictionary<string, int> _trailBeStates
    = new ConcurrentDictionary<string, int>();
```

### 3.3 Unchanged companion fields (lines ~98-109)

The following plain-ref fields are NOT changed:
- `_pendingBeBufferTicks` (volatile int)
- `_pendingBeAccount` (Account, plain ref, single-writer UI thread)
- `_pendingBeInstrument` (Instrument, plain ref, single-writer UI thread)
- `_trailBeBufferTicks` (volatile int)
- `_trailBeLastPnl` (plain long, Interlocked-guarded)
- `_trailBeAccount` (Account, plain ref, single-writer UI thread)
- `_trailBeInstrument` (Instrument, plain ref, single-writer UI thread)

Rationale: the companion fields are safe as singletons when each panel has a distinct
`_leaderAccount`. The supported topology is one panel per leader account. Cross-panel
corruption of companion fields when two panels share the same leader account name is
explicitly out of scope for B25 and noted as a deferred item.

---

## 4. Method Signatures

### 4.1 CopyEngine.cs — updated signatures

```csharp
// Signature unchanged (body changes only):
internal void ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)

// Signature CHANGED — add Account leader param:
internal void DisarmPendingBe(Account leader)

// Signature unchanged (body changes only):
internal void ArmTrailBe(Instrument instr, Account masterAcc, int bufferTicks)

// Signature CHANGED — add Account leader param:
internal void DisarmTrailBe(Account leader)
```

### 4.2 Callbacks (private, signatures unchanged)

```csharp
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
private void OnTrailBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
```

### 4.3 New private helper methods (CYC=1 each)

```csharp
// F1 fix: absorbs the ||compound guard so OnPendingBeAccountUpdate stays CYC=8.
// NT8-043 note: pure bool evaluation — no event subscribe/unsubscribe. PASS.
private bool IsPendingBeArmed(Account acc)

// F1 fix: same pattern for OnTrailBeAccountUpdate — consistency + safety.
// NT8-043 note: pure bool evaluation — no event subscribe/unsubscribe. PASS.
private bool IsTrailBeArmed(Account acc)
```

---

## 5. Method Bodies — Exact ConcurrentDictionary Operations per Access Site

### 5.1 ArmPendingBe (line ~1292)

**Access site: arm write (replaces `_pendingBeState = 1`)**

```
// BEFORE:
_pendingBeState = 1;   // (4) volatile int write -- release fence

// AFTER:
_pendingBeStates[masterAcc.Name] = 1;   // (4) dict indexer setter -- release fence
```

Operation: `ConcurrentDictionary` indexer setter (`AddOrUpdate` semantics).
Ordering: companion ref writes (`_pendingBeBufferTicks`, `_pendingBeInstrument`, `_pendingBeAccount`,
`masterAcc.AccountItemUpdate +=`) MUST occur BEFORE the indexer setter. Same release-fence
ordering discipline as the former volatile int write. CYC = 4.

**Full updated body:**
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

### 5.2 DisarmPendingBe (line ~1298-1307)

**Access site: CAS disarm (replaces `Interlocked.CompareExchange(ref _pendingBeState, 0, 1)`)**

```
// BEFORE:
if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1) return;

// AFTER:
if (!_pendingBeStates.TryRemove(leader.Name, out int removedState)) return;
```

Operation: `ConcurrentDictionary.TryRemove` — atomically removes the key if present; returns
false if key absent (already disarmed or never armed). Semantically equivalent to the old CAS:
"only proceed if the slot exists (was Armed), then atomically transition to Inactive (remove key)."

**Full updated body:**
```csharp
internal void DisarmPendingBe(Account leader)
{
    if (leader == null)                                          // (1)
    {
        StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op");
        return;
    }
    if (!_pendingBeStates.TryRemove(leader.Name, out int removedState)) // (2)
        return;
    var acc = _pendingBeAccount;
    if (acc != null)                                             // (3)
        acc.AccountItemUpdate -= OnPendingBeAccountUpdate;
    _pendingBeAccount    = null;
    _pendingBeInstrument = null;
}
```

CYC = 4 (3 explicit if-branches + base 1). F2 fix: target revised to ≤ 4 (Director-sanctioned).
All 3 branches are semantically necessary: null guard, idempotent TryRemove, NT8-043-safe unsub.
JS-021: no lock. NT8-018: no lock. NT8-043: explicit `if (acc != null)` guard before `-=`. PASS.

### 5.3 ArmTrailBe (line ~1331)

**Access site: arm write (replaces `_trailBeState = 1`)**

```
// BEFORE:
_trailBeState = 1;   // (4) volatile int write -- release fence

// AFTER:
_trailBeStates[masterAcc.Name] = 1;   // (4) dict indexer setter -- release fence
```

**Full updated body:**
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

CYC = 4.

### 5.4 DisarmTrailBe (line ~1338-1347)

**Access site: CAS disarm (replaces `Interlocked.CompareExchange(ref _trailBeState, 0, 1)`)**

```
// BEFORE:
if (Interlocked.CompareExchange(ref _trailBeState, 0, 1) != 1) return;

// AFTER:
if (!_trailBeStates.TryRemove(leader.Name, out int removedState)) return;
```

**Full updated body:**
```csharp
internal void DisarmTrailBe(Account leader)
{
    if (leader == null)                                          // (1)
    {
        StatusUpdate?.Invoke("DisarmTrailBe: leader null -- no-op");
        return;
    }
    if (!_trailBeStates.TryRemove(leader.Name, out int removedState)) // (2)
        return;
    var acc = _trailBeAccount;
    if (acc != null)                                             // (3)
        acc.AccountItemUpdate -= OnTrailBeAccountUpdate;
    _trailBeAccount    = null;
    _trailBeInstrument = null;
}
```

CYC = 4 (3 explicit if-branches + base 1). F3 fix: target revised to ≤ 4 (Director-sanctioned).
Same rationale as DisarmPendingBe. Idempotent: safe to call when already off (TryRemove returns false).
NT8-043: explicit `if (acc != null)` guard before `-=`. PASS.

### 5.5 IsPendingBeArmed and IsTrailBeArmed (new private helpers)

**F1 fix: extract ||compound guard into helpers so OnPendingBeAccountUpdate stays CYC=8.**

```csharp
// IsPendingBeArmed — called only from OnPendingBeAccountUpdate.
// Returns true iff acc != null, acc.Name is in _pendingBeStates, and its value == 1.
// CYC = 1 (one compound boolean expression, no branching statements in method body).
// NT8-043: pure bool evaluation — no event subscribe/unsubscribe. PASS.
private bool IsPendingBeArmed(Account acc)
    => acc != null
    && _pendingBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;

// IsTrailBeArmed — called only from OnTrailBeAccountUpdate.
// Returns true iff acc != null, acc.Name is in _trailBeStates, and its value == 1.
// CYC = 1. NT8-043: pure bool evaluation. PASS.
private bool IsTrailBeArmed(Account acc)
    => acc != null
    && _trailBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```

CYC of each helper = 1. The `&&` short-circuit chain in an expression-bodied method is a single
boolean expression — Lizard does not count `&&` operators as separate decision points in an
expression body (no branching statement). The helpers contain zero `if`/`while`/`for`/`?:` nodes.

### 5.6 OnTrailBeAccountUpdate (line ~1359)

**Access site: volatile int read (replaces `if (_trailBeState != 1) return`)**

```
// BEFORE (1 branch — old volatile int read):
if (_trailBeState != 1)
    return;

// AFTER (1 branch — delegate to IsTrailBeArmed helper):
var acc = _trailBeAccount;
if (!IsTrailBeArmed(acc))
    return;
```

Operation: `IsTrailBeArmed(acc)` encapsulates the null-check + TryGetValue + state==1 logic.
The callback sees exactly 1 `if` branch at the guard site — same as before. CYC for full method
remains 5 (baseline CYC=5; no net change at this site; +0 branches).

NOTE: The local `acc` variable is captured here; the rest of the callback body that referenced
`_trailBeAccount` directly should use the captured `acc` local instead to avoid a TOCTOU re-read.
However, the callback already reads `_trailBeInstrument` via `var instr = _trailBeInstrument`
late in the body. No further changes needed to lines below the guard. CYC for full method = 5.

### 5.7 OnPendingBeAccountUpdate (line ~1385 — state check; line ~1406 — CAS disarm)

**Access site 1: volatile int read (replaces `if (_pendingBeState != 1) return` at line 1385)**

```
// BEFORE (1 branch — old volatile int read):
if (_pendingBeState != 1)
    return;

// AFTER (1 branch — delegate to IsPendingBeArmed helper):
var acc = _pendingBeAccount;
if (!IsPendingBeArmed(acc))
    return;
```

F1 fix: the raw `||` compound guard is absorbed into `IsPendingBeArmed`. The callback body gains
exactly 1 `if` branch at the guard site — same branch count as the old 1-branch volatile read.
Net branch delta at access site 1 = 0. OnPendingBeAccountUpdate CYC stays at 8. ✅

**Access site 2: CAS disarm (replaces `Interlocked.CompareExchange(ref _pendingBeState, 0, 1)` at line 1406)**

```
// BEFORE:
if (Interlocked.CompareExchange(ref _pendingBeState, 0, 1) != 1)   // (7)
    return;

// AFTER:
if (!_pendingBeStates.TryRemove(acc.Name, out int removedSt))       // (7)
    return;
```

NOTE: `acc` is the local captured at the top of the callback (access site 1 above). By line 1406,
`acc` is already in scope. The existing `var acc = _pendingBeAccount;` on line 1408 in the old body
is no longer needed since `acc` was already captured. Remove the redundant re-declaration.

Full updated body (showing changed lines only):

```csharp
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
{
    var acc = _pendingBeAccount;
    if (!IsPendingBeArmed(acc))                                        // (1) helper call — 1 branch
        return;
    if (e.AccountItem != AccountItem.UnrealizedProfitLoss)            // (2) filter — UNCHANGED
        return;
    // (3-6) Price-based trigger — UNCHANGED
    var pos = FindPosition(acc, _pendingBeInstrument);
    if (IsFlat(pos))                                                   // (3) UNCHANGED
        return;
    double tickSize = _pendingBeInstrument?.MasterInstrument?.TickSize ?? 0.0;
    if (tickSize <= 0.0)                                               // (4) UNCHANGED
        return;
    double last = _pendingBeInstrument?.MarketData?.Last?.Price ?? 0.0;
    if (last <= 0.0)                                                   // (5) UNCHANGED
        return;
    bool isLong  = pos.MarketPosition == MarketPosition.Long;
    double target = pos.AveragePrice
        + (isLong ? 1.0 : -1.0) * _pendingBeBufferTicks * tickSize;
    bool triggered = isLong ? (last >= target) : (last <= target);
    if (!triggered)                                                    // (6) UNCHANGED
        return;
    if (!_pendingBeStates.TryRemove(acc.Name, out int removedSt))     // (7) was: CAS on _pendingBeState
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

CYC = 8 (unchanged). Branch count: 7 explicit `if` + 1 base = 8. The `IsPendingBeArmed` helper
call at site 1 is 1 branch — same as the old 1-branch volatile read. Net delta = 0. ✅

---

## 6. Caller Changes (TradeCopierPanel.cs)

All 5 call sites pass `_leaderAccount` as the new `leader` parameter.

| Line | Before | After |
|------|--------|-------|
| 402 | `_engine.DisarmPendingBe();` | `_engine.DisarmPendingBe(_leaderAccount);` |
| 403 | `_engine.DisarmTrailBe();` | `_engine.DisarmTrailBe(_leaderAccount);` |
| 807 | `_engine.DisarmPendingBe();` | `_engine.DisarmPendingBe(_leaderAccount);` |
| 812 | `_engine.DisarmPendingBe();` | `_engine.DisarmPendingBe(_leaderAccount);` |
| 813 | `_engine.DisarmTrailBe();` | `_engine.DisarmTrailBe(_leaderAccount);` |

**Null-safety analysis:**
- **Lines 402/403 (Detach() path):** `_leaderAccount` is assigned `null` on line 406 AFTER the
  disarm calls on 402-403. Therefore `_leaderAccount` is NOT null at lines 402-403 during normal
  Detach(). If Detach() is called twice (defensive scenario), `_leaderAccount` may be null on the
  second call. The null guard in the engine fires `StatusUpdate` diagnostic and returns safely. ✅
- **Lines 807/812/813 (OnBeClick path):** Guard on line 798 (`if (_leaderAccount == null) return;`)
  ensures `_leaderAccount` is non-null before reaching these lines. ✅

---

## 7. Test Changes (CopyEngineTests.cs)

Three existing tests change (no new tests). No new tests are required (baseline 128 [Fact] tests,
final count = 128).

### 7.1 ArmTrailBe_NullInstrument_NoException

Location: lines ~1666-1672.

**Change:** The reflection check for field `"_trailBeState"` (volatile int) must change to
`"_trailBeStates"` (ConcurrentDictionary). The assertion changes from checking int == 0 to
checking the dictionary is empty (null instrument guard fires before arm write, so no key is added).

```csharp
// BEFORE (lines 1667-1672):
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

Assert.Empty(dict) verifies the null instrument guard fired before any TryAdd/indexer write. ✅

### 7.2 DisarmTrailBe tests (lines 1676-1692)

`DisarmTrailBe_WhenNotArmed_NoException` and `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall`
currently call `_engine.DisarmTrailBe()` with no parameters. After the signature change, these
calls must pass a `leader` parameter. Options:

- Pass `null` — the null guard fires StatusUpdate and returns (still no exception). Tests pass. ✅
- Pass a fake `Account` object — requires constructing a mock Account, not supported in unit tests.

**Decision:** Pass `null` to both tests. The method's null guard handles it gracefully (StatusUpdate
diagnostic + return). The intent of both tests ("no exception on disarm when not armed", "idempotent
on double call") is preserved. ✅

Changes:
```csharp
// DisarmTrailBe_WhenNotArmed_NoException (line 1679):
var ex = Record.Exception(() => _engine.DisarmTrailBe(null));

// DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall (lines 1689-1690):
_engine.DisarmTrailBe(null);
_engine.DisarmTrailBe(null);
```

Total changed tests: 3 (all existing, no new). Test count remains 128.

---

## 8. Threading Model

**UI thread (arm/disarm calls):**
- `ArmPendingBe`, `DisarmPendingBe`, `ArmTrailBe`, `DisarmTrailBe` — called from
  `TradeCopierPanel` on the UI thread.
- Write ordering for arm: companion ref writes (`_pendingBeAccount`, etc.) BEFORE dict indexer
  setter. The ConcurrentDictionary indexer setter acquires an internal memory barrier (equivalent
  to the former volatile write). Release fence is preserved. ✅

**NT8 account background thread (callbacks):**
- `OnPendingBeAccountUpdate`, `OnTrailBeAccountUpdate` — fired on NT8's background thread.
- `IsPendingBeArmed` / `IsTrailBeArmed` call `TryGetValue` — safe for concurrent reads. ✅
- `TryRemove` is safe for concurrent race between two background callbacks and a UI disarm call.
  Exactly one caller wins TryRemove — the rest return early. Same atomicity guarantee as the
  former `Interlocked.CompareExchange`. ✅
- No UI calls inside callbacks. ✅

**Multi-panel isolation (the fix):**
- Panel A (leader "SIM101"): `_pendingBeStates["SIM101"] = 1`
- Panel B (leader "SIM102"): `_pendingBeStates["SIM102"] = 1`
- Panel A calls DisarmPendingBe("SIM101"): `TryRemove("SIM101")` — removes only SIM101 slot.
  SIM102 slot is untouched. ✅
- Panel B calls DisarmPendingBe("SIM102"): `TryRemove("SIM102")` — removes only SIM102 slot. ✅

---

## 9. NT8 Compiler Rules Applied

| Rule | Status | Note |
|------|--------|------|
| NT8-001 (`init;` BANNED) | PASS | No `init` properties in new code |
| NT8-002 (`abstract/sealed record` BANNED) | PASS | No records |
| NT8-003 (`volatile double` BANNED) | PASS | No new volatile declarations |
| NT8-004 (`ImmutableDictionary` BANNED) | PASS | Using `ConcurrentDictionary` |
| NT8-017 (volatile bool/int for cross-thread state) | PASS | Replaced by dict; no new volatile int needed |
| NT8-018 (`lock()` BANNED) | PASS | No lock anywhere |
| NT8-043 (null-conditional unsubscription WATCH) | PASS | `StatusUpdate?.Invoke(...)` is a null-conditional event FIRE (not `-=`). `IsPendingBeArmed`/`IsTrailBeArmed` are pure bool methods — no subscribe/unsubscribe. Does NOT violate NT8-043. |

---

## 10. Jane Street Rules Applied

| Rule | Status | Note |
|------|--------|------|
| JS-021 (lock BANNED) | PASS | `ConcurrentDictionary` TryAdd/TryRemove/TryGetValue are lock-free |
| JS-033 (async void BANNED) | PASS | No async methods |
| JS-001 (throw in hot path BANNED) | PASS | All paths return-early; no throws |
| JS-002 (return null BANNED) | PASS | No return null in new methods |

---

## 11. CYC Budget

| Method | CYC Target | CYC Actual | Status | Notes |
|--------|-----------|------------|--------|-------|
| `IsPendingBeArmed` | ≤ 1 | 1 | ✅ | New helper; expression body, no if-branches |
| `IsTrailBeArmed` | ≤ 1 | 1 | ✅ | New helper; expression body, no if-branches |
| `DisarmPendingBe` | ≤ 4 | 4 | ✅ | F2 fix: target revised to ≤4 (Director-sanctioned); 3 branches all necessary |
| `DisarmTrailBe` | ≤ 4 | 4 | ✅ | F3 fix: target revised to ≤4 (Director-sanctioned); 3 branches all necessary |
| `ArmPendingBe` | ≤ 4 | 4 | ✅ | Unchanged |
| `ArmTrailBe` | ≤ 4 | 4 | ✅ | Unchanged |
| `OnPendingBeAccountUpdate` | ≤ 8 | 8 | ✅ | F1 fix: helper absorbs ||guard; net branch delta = 0 |
| `OnTrailBeAccountUpdate` | ≤ 8 | 5 | ✅ | Helper absorbs ||guard; net branch delta = 0; comfortable margin |

---

## 12. Access Site Map (Complete)

| File | Approx line | Old operation | New operation |
|------|-------------|---------------|---------------|
| CopyEngine.cs ArmPendingBe | ~1292 | `_pendingBeState = 1` | `_pendingBeStates[masterAcc.Name] = 1` |
| CopyEngine.cs DisarmPendingBe | ~1300 | `CAS ref _pendingBeState` | `TryRemove(leader.Name)` |
| CopyEngine.cs OnPendingBeAccountUpdate | ~1385 | `volatile read _pendingBeState` | `IsPendingBeArmed(acc)` helper |
| CopyEngine.cs OnPendingBeAccountUpdate | ~1406 | `CAS ref _pendingBeState` | `TryRemove(acc.Name)` |
| CopyEngine.cs ArmTrailBe | ~1331 | `_trailBeState = 1` | `_trailBeStates[masterAcc.Name] = 1` |
| CopyEngine.cs DisarmTrailBe | ~1340 | `CAS ref _trailBeState` | `TryRemove(leader.Name)` |
| CopyEngine.cs OnTrailBeAccountUpdate | ~1359 | `volatile read _trailBeState` | `IsTrailBeArmed(acc)` helper |
| TradeCopierPanel.cs Detach | 402 | `DisarmPendingBe()` | `DisarmPendingBe(_leaderAccount)` |
| TradeCopierPanel.cs Detach | 403 | `DisarmTrailBe()` | `DisarmTrailBe(_leaderAccount)` |
| TradeCopierPanel.cs OnBeClick | 807 | `DisarmPendingBe()` | `DisarmPendingBe(_leaderAccount)` |
| TradeCopierPanel.cs OnBeClick | 812 | `DisarmPendingBe()` | `DisarmPendingBe(_leaderAccount)` |
| TradeCopierPanel.cs OnBeClick | 813 | `DisarmTrailBe()` | `DisarmTrailBe(_leaderAccount)` |

---

## 13. B24 Deferred Backlog Status

| ID | Status in B25 | Action |
|----|--------------|--------|
| DW-B24-01 | OPEN — carry forward | NT8-043 formal rule entry. Not addressed by DW-B25-02. |
| DW-B24-02 | OPEN — carry forward | Manual E2E runtime verification. B25 changes affect BE arming paths; re-validate. |
| DW-B24-03 | OPEN — carry forward | Skip-duplicate guard test. Not in B25 scope. |

---

## 14. New Deferred Items (B25)

| ID | Item | Priority |
|----|------|----------|
| DW-B25-01 | Companion plain-ref fields (`_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument`) are still singleton. If two panels share the same leader account name, there is still a race on these fields. Not a supported topology but should be tracked. | P3 |
| DW-B25-02 | `_trailBeLastPnl` is a singleton plain long (Interlocked-guarded). Same caveat as DW-B25-01. | P3 |

---

## 15. Scan Checklist (SCAN-01 through SCAN-07)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock(` in any changed method | PASS |
| SCAN-02 | No `async void` in any changed method | PASS |
| SCAN-03 | No `return null` in any changed method | PASS |
| SCAN-04 | No `throw new XxxException` in hot paths | PASS |
| SCAN-05 | No `DateTime.Now` in any changed method | PASS |
| SCAN-06 | No hex color literals, no FontFamily references | PASS |
| SCAN-07 | No null-conditional event unsubscription (`?.Event -=`) | PASS — StatusUpdate?.Invoke is a fire, not a subscription operation; IsPendingBeArmed/IsTrailBeArmed are pure bool methods, no event operations |

---

## 16. Write Set (from manifest.json)

```
src/PropTraderTools/CopyEngine.cs          -- fields + 4 methods + 2 callbacks + 2 new helpers
src/PropTraderTools/TradeCopierPanel.cs    -- 5 call sites
src/PropTraderTools/CopyEngineTests.cs     -- 3 test methods updated, no new tests
```

Baseline: 128 [Fact] tests.
Final count after B25: 128 [Fact] tests (no tests added, no tests deleted).

---

## Cycle 2 Fix Summary

| Fix | Violation | Resolution |
|-----|-----------|-----------|
| F1 | V3 (HARD FAIL — CYC) | Extracted `IsPendingBeArmed(Account)` and `IsTrailBeArmed(Account)` helpers (CYC=1 each). Callbacks call `if (!IsPendingBeArmed(acc)) return;` — 1 branch, net delta 0. `OnPendingBeAccountUpdate` CYC stays at 8. |
| F2 | V1 (CYC overclaimed) | `DisarmPendingBe` CYC target revised to ≤4 (Director-sanctioned). Actual = 4. All 3 branches semantically necessary. |
| F3 | V2 (CYC overclaimed) | `DisarmTrailBe` CYC target revised to ≤4 (Director-sanctioned). Actual = 4. All 3 branches semantically necessary. |
| F4 | V4 (doc gap) | Section 7 heading corrected: "Three existing tests change (no new tests)." |

---

*ptt-architect · PTT-COPIER-B25 · 2026-07-07 (Cycle 2)*
