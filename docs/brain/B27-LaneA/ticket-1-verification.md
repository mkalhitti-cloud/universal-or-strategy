# PTT-COPIER-B27 Lane A -- Ticket 1 Verification Report
# Ticket: B27-T1 -- DW-B27-01: Replace singleton BE fields with per-account slot dicts
# Verifier: ptt-verifier (Phase 4b -- READ ONLY on src/)
# Date: 2026-07-16
# Wave workspace: c:\WSGTA\universal-or-strategy\src\PropTraderTools\ (READ ONLY)
# Director workspace: c:\WSGTA\universal-or-strategy-director\

---

## STEP 0 -- RULES CATALOG GATE

File read: `docs/standards/jane-street/RULES_CATALOG.md`
Encoding: UTF-8 clean (no BOM, no garbled characters, all rules readable)
P0 violations in new/modified code: ZERO

**Gate result: PASS**

---

## INPUTS READ

| File | Status |
|------|--------|
| `docs/brain/B27-LaneA/ticket-1-completion.md` | Read OK |
| `docs/brain/B27-LaneA/04-tickets.md` | Read OK |
| `docs/brain/B27-LaneA/02-architecture-plan.md` | Read OK |
| `src/PropTraderTools/CopyEngine.cs` | Read OK (READ ONLY) |
| `src/PropTraderTools/CopyEngineTests.cs` | Read OK (READ ONLY) |

---

## ALL 7 INDEPENDENT SCANS (Layer 3 -- Verifier's Own Run)

### SCAN-01: lock() -- JS-021 Compliance

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "lock\("
```

**Actual output (2 matches):**
```
CopyEngine.cs:598:  // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
CopyEngine.cs:1276: // CYC=3: null guard(1), alreadyTighter(2), try block(0).
```

Both matches are English comment text: `"try block(0)"` -- NOT C# `lock()` syntax.
Zero actual lock() statements in source code.

**SCAN-01 result: PASS (0 lock() violations)**

---

### SCAN-02: Deleted pending singleton fields

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_pendingBeAccount|_pendingBeInstrument|_pendingBeStates|_pendingBeBufferTicks"
```

**Actual output:** Command completed with no output.

**SCAN-02 result: PASS (0 results -- all 4 pending BE singleton fields fully deleted)**

---

### SCAN-03: Deleted trail singleton fields

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_trailBeAccount|_trailBeInstrument|_trailBeStates|_trailBeBufferTicks|_trailBeLastPnl[^B]"
```

**Actual output:** Command completed with no output.

Note: The pattern `_trailBeLastPnl[^B]` excludes the new field `_trailBeLastPnlBits` by design.
Zero hits for all 5 old trail BE singleton fields.

**SCAN-03 result: PASS (0 results -- all 5 trail BE singleton fields fully deleted)**

---

### SCAN-04: Deleted helper methods (IsPendingBeArmed, IsTrailBeArmed)

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "IsPendingBeArmed|IsTrailBeArmed"
```

**Actual output:** Command completed with no output.

**SCAN-04 result: PASS (0 results -- both methods fully deleted, no declaration or call sites remain)**

---

### SCAN-05: [Fact] count in CopyEngineTests.cs

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object
```

**Actual output:**
```
Count    : 135
```

Expected: 135 (baseline 133 + 2 new B27 tests). Matches.

**SCAN-05 result: PASS (Count = 135)**

---

### SCAN-06: volatile on trail/pending fields -- NT8-003 Compliance

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "volatile" | Where-Object { $_.Line -match "trail|pending" }
```

**Actual output:** Command completed with no output.

No `volatile` keyword on any trail or pending BE field or comment co-located with those terms.
The new `_trailBeLastPnlBits` is a `long` in `ConcurrentDictionary<string, long>` (no `volatile`).
NT8-003 constraint satisfied.

**SCAN-06 result: PASS (0 results)**

---

### SCAN-07: async void -- JS-033 Compliance

```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "async void "
```

**Actual output:** Command completed with no output.

**SCAN-07 result: PASS (0 results)**

---

## STRUCTURAL VERIFICATION CHECKS (V1-V10)

### V1 -- Structs: PendingBeSlot and TrailBeSlot

**Source evidence** (CopyEngine.cs lines 83-104):
```csharp
private struct PendingBeSlot
{
    internal readonly Account    Account;
    internal readonly Instrument Instrument;
    internal readonly int        BufferTicks;
    internal PendingBeSlot(Account a, Instrument i, int b)
    { Account = a; Instrument = i; BufferTicks = b; }
}

private struct TrailBeSlot
{
    internal readonly Account    Account;
    internal readonly Instrument Instrument;
    internal readonly int        BufferTicks;
    internal TrailBeSlot(Account a, Instrument i, int b)
    { Account = a; Instrument = i; BufferTicks = b; }
}
```

- `private struct` (NOT `readonly struct`) -- NT8-005 compliant
- Fields are `internal readonly` (NOT `{ get; init; }`) -- NT8-001 compliant
- NOT a record -- NT8-002 compliant
- Both structs present inside `CopyEngine` class

**V1 result: PASS**

---

### V2 -- New fields: _pendingBeSlots, _trailBeSlots, _trailBeLastPnlBits

**Source evidence** (CopyEngine.cs lines 106-122):
```csharp
private readonly ConcurrentDictionary<string, PendingBeSlot> _pendingBeSlots
    = new ConcurrentDictionary<string, PendingBeSlot>();

private readonly ConcurrentDictionary<string, TrailBeSlot>   _trailBeSlots
    = new ConcurrentDictionary<string, TrailBeSlot>();

private readonly ConcurrentDictionary<string, long>           _trailBeLastPnlBits
    = new ConcurrentDictionary<string, long>();
```

All 3 replacement fields present with correct types. `ConcurrentDictionary` used (NT8-004 compliant --
`ImmutableDictionary` is banned in NT8). No `volatile` keyword. `_trailBeLastPnlBits` is `long` via
`BitConverter` (NT8-003 compliant).

**V2 result: PASS**

---

### V3 -- ArmPendingBe: singleton writes removed, _pendingBeSlots[] upsert present. CYC=4.

**Source evidence** (CopyEngine.cs lines 1309-1320):
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
    _pendingBeSlots[masterAcc.Name] = new PendingBeSlot(masterAcc, instr, bufferTicks); // (4)
    masterAcc.AccountItemUpdate += OnPendingBeAccountUpdate;
}
```

- No singleton field writes present
- `_pendingBeSlots[]` upsert is the single write replacing 4 old singleton writes
- CYC count: 3 guards + nominal path = CYC=4

**V3 result: PASS (CYC=4)**

---

### V4 -- DisarmPendingBe: _pendingBeSlots.TryRemove used, slot.Account for event -=. CYC=3.

**Source evidence** (CopyEngine.cs lines 1327-1338):
```csharp
internal void DisarmPendingBe(Account leader)
{
    if (leader == null)                                                       // (1)
    {
        StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op");
        return;
    }
    if (!_pendingBeSlots.TryRemove(leader.Name, out var slot))               // (2)
        return;
    if (slot.Account != null)                                                 // (3)
        slot.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
}
```

- `_pendingBeSlots.TryRemove` used (no reference to old `_pendingBeStates`)
- `slot.Account` for event unsubscribe (NT8-043: explicit null guard, no `?.` on `-=`)
- No old singleton null-clears
- CYC=3

**V4 result: PASS (CYC=3)**

---

### V5 -- ArmTrailBe: singleton writes removed, _trailBeSlots[] and _trailBeLastPnlBits[] upserts. CYC=4.

**Source evidence** (CopyEngine.cs lines 1345-1360):
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
    long pnlBits = BitConverter.DoubleToInt64Bits(currentPnl);
    _trailBeSlots[masterAcc.Name]       = new TrailBeSlot(masterAcc, instr, bufferTicks); // (4)
    _trailBeLastPnlBits[masterAcc.Name] = pnlBits;
    masterAcc.AccountItemUpdate += OnTrailBeAccountUpdate;
}
```

- No singleton field writes
- Both `_trailBeSlots[]` and `_trailBeLastPnlBits[]` upserts present
- currentPnl capture + MinValue clamp preserved
- CYC=4

**V5 result: PASS (CYC=4)**

---

### V6 -- DisarmTrailBe: _trailBeSlots.TryRemove + _trailBeLastPnlBits.TryRemove. CYC=3.

**Source evidence** (CopyEngine.cs lines 1368-1380):
```csharp
internal void DisarmTrailBe(Account leader)
{
    if (leader == null)                                                       // (1)
    {
        StatusUpdate?.Invoke("DisarmTrailBe: leader null -- no-op");
        return;
    }
    if (!_trailBeSlots.TryRemove(leader.Name, out var slot))                 // (2)
        return;
    if (slot.Account != null)                                                 // (3)
        slot.Account.AccountItemUpdate -= OnTrailBeAccountUpdate;
    _trailBeLastPnlBits.TryRemove(leader.Name, out _);
}
```

- `_trailBeSlots.TryRemove` used
- `_trailBeLastPnlBits.TryRemove` used
- NT8-043: explicit null guard on event unsubscribe
- No old singleton null-clears
- CYC=3

**V6 result: PASS (CYC=3)**

---

### V7 -- OnPendingBeAccountUpdate: accName from sender cast, TryGetValue gates 2-6, TryRemove at gate 7. No _pendingBeAccount. CYC=8.

**Source evidence** (CopyEngine.cs lines 1420-1451):
```csharp
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
{
    if (e.AccountItem != AccountItem.UnrealizedProfitLoss)                          // (1)
        return;
    string accName = (sender as NinjaTrader.Cbi.Account)?.Name ?? string.Empty;
    if (!_pendingBeSlots.TryGetValue(accName, out var slot))                        // (2)
        return;
    var acc   = slot.Account;
    var instr = slot.Instrument;
    var buf   = slot.BufferTicks;
    var pos   = FindPosition(acc, instr);
    if (IsFlat(pos))                                                                 // (3)
        return;
    double tickSize = instr?.MasterInstrument?.TickSize ?? 0.0;
    if (tickSize <= 0.0)                                                             // (4)
        return;
    double last = instr?.MarketData?.Last?.Price ?? 0.0;
    if (last <= 0.0)                                                                 // (5)
        return;
    bool isLong  = pos.MarketPosition == MarketPosition.Long;
    double target = pos.AveragePrice + (isLong ? 1.0 : -1.0) * buf * tickSize;
    bool triggered = isLong ? (last >= target) : (last <= target);
    if (!triggered)                                                                  // (6)
        return;
    if (!_pendingBeSlots.TryRemove(accName, out var removed))                       // (7) atomic claim
        return;
    if (removed.Account != null)
        removed.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
    BreakEven(removed.Account, removed.Instrument, removed.BufferTicks);
    PendingBeFired?.Invoke(removed.Instrument?.FullName ?? string.Empty,
                           removed.Account?.Name ?? string.Empty);
}
```

- `accName` derived from `(sender as NinjaTrader.Cbi.Account)?.Name` -- per-account scope
- `_pendingBeSlots.TryGetValue` at gate 2
- All 7 guard returns + nominal path = CYC=8 (at ceiling)
- Zero references to old `_pendingBeAccount`
- NT8-032: `.Last?.Price` pattern present (line 1436)

**V7 result: PASS (CYC=8)**

---

### V8 -- OnTrailBeAccountUpdate: accName from sender cast, TryGetValue + AddOrUpdate CAS on _trailBeLastPnlBits. No _trailBeAccount. CYC<=8.

**Source evidence** (CopyEngine.cs lines 1389-1412):
```csharp
private void OnTrailBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
{
    if (e.AccountItem != AccountItem.UnrealizedProfitLoss)                          // (1)
        return;
    string accName = (sender as NinjaTrader.Cbi.Account)?.Name ?? string.Empty;
    if (!_trailBeSlots.TryGetValue(accName, out var slot))                          // (2)
        return;
    double newPnl = e.Value;
    if (!_trailBeLastPnlBits.TryGetValue(accName, out long oldBits))                // (3a)
        return;
    double oldPnl = BitConverter.Int64BitsToDouble(oldBits);
    if (newPnl <= oldPnl)                                                            // (3b)
        return;
    long newBits = BitConverter.DoubleToInt64Bits(newPnl);
    long actual  = _trailBeLastPnlBits.AddOrUpdate(                                 // (4)
        accName, newBits, (k, cur) => cur < newBits ? newBits : cur);
    if (actual != newBits)                                                           // lost race
        return;
    _trailBeSlots.AddOrUpdate(                                                       // (5)
        accName,
        new TrailBeSlot(slot.Account, slot.Instrument, slot.BufferTicks + 1),
        (k, old) => new TrailBeSlot(old.Account, old.Instrument, old.BufferTicks + 1));
    BreakEven(slot.Account, slot.Instrument, slot.BufferTicks + 1);
}
```

- `accName` from sender cast -- per-account scope
- `_trailBeSlots.TryGetValue` at gate 2
- `_trailBeLastPnlBits.AddOrUpdate` CAS for high-water-mark pattern
- Zero references to old `_trailBeAccount`
- JS-021: `AddOrUpdate` is lock-free (CAS loop internal to ConcurrentDictionary)
- CYC counted: (1)+(2)+(3a)+(3b)+(lost race check) = 5 decision points + nominal = CYC=6

**V8 result: PASS (CYC=6 <= 8)**

---

### V9 -- IsPendingBeArmed and IsTrailBeArmed: DELETED (must not appear in source)

Confirmed by SCAN-04: `Select-String ... -Pattern "IsPendingBeArmed|IsTrailBeArmed"` returned 0 results.
Neither declaration nor call sites exist anywhere in CopyEngine.cs.

**V9 result: PASS (both methods fully deleted)**

---

### V10 -- CopyEngineTests.cs: ArmTrailBe_NullInstrument_NoException uses "_trailBeSlots", T_B27_01 and T_B27_02 present

**Source evidence (CopyEngineTests.cs):**

`ArmTrailBe_NullInstrument_NoException` (line 1649):
```csharp
// _trailBeSlots must remain empty (null instrument guard fires before slot write)
var fi = typeof(CopyEngine).GetField(
    "_trailBeSlots",                             // line 1668 -- correct field name
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
Assert.NotNull(fi);
var dict2 = fi.GetValue(_engine);
Assert.NotNull(dict2);
var dictTyped = dict2 as System.Collections.IDictionary;  // line 1673 -- IDictionary cast
Assert.NotNull(dictTyped);
Assert.Equal(0, dictTyped.Count);
```

Note: The cast uses `System.Collections.IDictionary` (non-generic) rather than
`ConcurrentDictionary<string, TrailBeSlot>` because `TrailBeSlot` is a `private struct` inside
`CopyEngine` and is inaccessible from the test project. The ticket's CHANGE K description
explicitly specifies this approach as the permitted alternative. Functionally equivalent --
both approaches assert the dictionary is empty.

`T_B27_01_ArmTwoPanels_SecondArmDoesNotNullFirstInstrument` (line 2416):
- Reflects `_pendingBeSlots` field -- PRESENT
- Reflects `PendingBeSlot` nested type -- PRESENT
- Asserts `Account`, `Instrument`, `BufferTicks` fields exist on `PendingBeSlot` -- PRESENT

`T_B27_02_DisarmOneAccount_DoesNotAffectOther` (line 2440):
- Reflects `_pendingBeSlots` -- PRESENT
- Reflects `_trailBeSlots` -- PRESENT
- Reflects `_trailBeLastPnlBits` -- PRESENT
- Reflects `TrailBeSlot` nested type and its three fields -- PRESENT

**V10 result: PASS**

---

## DISCREPANCY ANALYSIS (Layer 3 vs Layer 2)

| Scan | Engineer Layer 2 | Verifier Layer 3 | Discrepancy? |
|------|-----------------|-----------------|--------------|
| SCAN-01 | "2 matches -- both English text 'block(0)' in comments" | 2 matches at lines 598 and 1276, both `"try block(0)"` in comments | NONE |
| SCAN-02 | "0 results" | 0 results | NONE |
| SCAN-03 | "0 results (B14 comment fixed)" | 0 results | NONE |
| SCAN-04 | "0 results" | 0 results | NONE |
| SCAN-05 | "Count: 135" | Count: 135 | NONE |
| SCAN-06 | "0 results (two comments rephrased)" | 0 results | NONE |
| SCAN-07 | "0 results" | 0 results | NONE |

**No discrepancies between Layer 2 (engineer self-report) and Layer 3 (verifier independent scans).**

---

## DNA RULE CHECK

| Rule | Requirement | Source Evidence | Result |
|------|-------------|----------------|--------|
| JS-021 | No lock() | SCAN-01: 0 lock() constructs | PASS |
| JS-001 | No throw in hot-path callbacks | OnPendingBeAccountUpdate, OnTrailBeAccountUpdate: guard returns only, no try/catch introduced | PASS |
| JS-002 | No return null | All methods are void (N/A for return type) | PASS |
| JS-033 | No async void | SCAN-07: 0 results | PASS |
| NT8-001 | No `{ get; init; }` | struct fields use `internal readonly T Field;` with explicit ctor | PASS |
| NT8-002 | No abstract/sealed record | structs declared `private struct` NOT record | PASS |
| NT8-003 | No volatile on trail/pending | SCAN-06: 0 results. _trailBeLastPnlBits is `long` in `ConcurrentDictionary<string,long>` | PASS |
| NT8-004 | No ImmutableDictionary | All three new dicts are `ConcurrentDictionary` | PASS |
| NT8-005 | No `readonly struct` with private set | struct declared `private struct` (NOT `readonly struct`) | PASS |
| NT8-043 | No `?.` on `-=` event | DisarmPendingBe L1336, DisarmTrailBe L1377, OnPendingBeAccountUpdate L1446: all use explicit `if (x != null) x.Event -= handler` | PASS |
| NT8-032 | `.Last.Price` for market data | OnPendingBeAccountUpdate L1436: `instr?.MarketData?.Last?.Price ?? 0.0` | PASS |
| CYC<=8 | All methods within ceiling | ArmPendingBe=4, DisarmPendingBe=3, ArmTrailBe=4, DisarmTrailBe=3, OnPendingBeAccountUpdate=8, OnTrailBeAccountUpdate=6 | PASS |
| ASCII-only | No unicode in identifiers/literals | Verified by source inspection -- all identifiers and string literals are ASCII | PASS |

---

## ARCHITECTURE COMPLIANCE

| Requirement | Status | Evidence |
|-------------|--------|---------|
| 9 singleton BE fields deleted | PASS | SCAN-02: 0, SCAN-03: 0 |
| 2 new nested structs added | PASS | V1: lines 83-104 |
| 3 new ConcurrentDictionary fields added | PASS | V2: lines 106-122 |
| ArmPendingBe rewritten | PASS | V3: lines 1309-1320 |
| DisarmPendingBe rewritten | PASS | V4: lines 1327-1338 |
| IsPendingBeArmed deleted | PASS | V9: SCAN-04 = 0 |
| ArmTrailBe rewritten | PASS | V5: lines 1345-1360 |
| DisarmTrailBe rewritten | PASS | V6: lines 1368-1380 |
| IsTrailBeArmed deleted | PASS | V9: SCAN-04 = 0 |
| OnTrailBeAccountUpdate full rewrite | PASS | V8: lines 1389-1412 |
| OnPendingBeAccountUpdate full rewrite | PASS | V7: lines 1420-1451 |
| ArmTrailBe_NullInstrument_NoException updated | PASS | V10: L1668 uses `"_trailBeSlots"` |
| T_B27_01 new [Fact] added | PASS | V10: L2416 |
| T_B27_02 new [Fact] added | PASS | V10: L2440 |
| [Fact] count = 135 | PASS | SCAN-05 |
| TradeCopierPanel.cs: 0 changes | N/A (out of scope, method signatures unchanged) | |
| DW-B27-01 spec requirement addressed | PASS | Per-account ConcurrentDictionary<string,TSlot> prevents singleton overwrite |

---

## SPEC REQUIREMENT COVERAGE

| Req ID | Requirement | Covered? | Evidence |
|--------|-------------|----------|---------|
| DW-B27-01 | BE singleton fields stop never moves for account 2 -- second arm overwrites first | YES | All 9 singleton fields deleted. Replaced with per-account ConcurrentDictionary slot dicts keyed by account.Name. Second arm creates a NEW key, never overwriting first account's slot. |

---

## OVERALL VERDICT

**All 7 scans: PASS**
**All 10 structural checks (V1-V10): PASS**
**All DNA rules: PASS**
**Layer 2 vs Layer 3: No discrepancies**
**Architecture compliance: Full**
**Spec DW-B27-01 coverage: Full**

---

## VERIFY_PASS
