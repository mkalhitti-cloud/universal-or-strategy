# B34 Engineering Tickets — Multi-Account BE Fixes + Buffer Extension
<!-- PTT-COPIER B34 | be-multiAccount-fixes | 2026-07-27 -->

## Status: TICKETS_COMPLETE — Ready for ptt-engineer

---

## CRITICAL: MANDATORY IMPLEMENTATION ORDER

> **DO NOT implement tickets in number order. The compile dependency requires:**
>
> **B34-02 → B34-01 → B34-03 → B34-04**
>
> - `B34-01` references `ctx.BeBuffer` (added by B34-02). Compiling B34-01 standalone = error.
> - `B34-03` references `ctx.TrimBuffer`, `ctx.FlatBuffer`, `ctx.Ask`, `ctx.Bid` (all added by B34-02).
> - `B34-02` has zero upstream dependencies — implement it first.
> - `B34-04` runs last; it is the verifier pass and tag update only.

**Baseline to protect:**
- `CopyEngine.cs` tag line 41: `"PTT-COPIER B33 | modular-independence | 2026-07-25"`
- `[Fact]` count: **171** (must reach **177** after all tickets)
- Wave workspace root: `C:\WSGTA\universal-or-strategy\`

---

## Source Baseline (Read Before Starting Any Ticket)

| File | Location in Wave workspace | Notes |
|---|---|---|
| `PttBreakEven.cs` | `src\PropTraderTools\Features\PttBreakEven.cs` | B34-01 target |
| `PttContracts.cs` | `src\PropTraderTools\Core\PttContracts.cs` | B34-02 interface target |
| `TradeCopierPanel.cs` | `src\PropTraderTools\TradeCopierPanel.cs` | B34-02 impl target |
| `PttTrim.cs` | `src\PropTraderTools\Features\PttTrim.cs` | B34-03 target |
| `PttFlatten.cs` | `src\PropTraderTools\Features\PttFlatten.cs` | B34-03 target |
| `CopyEngine.cs` | `src\PropTraderTools\CopyEngine.cs` | B34-04 tag update only |
| Test file (BE) | `tests\PropTraderTools.Tests\Features\PttBreakEvenTests.cs` | B34-01 tests added here |
| Test file (contracts) | `tests\PropTraderTools.Tests\Core\PttContractsTests.cs` | B34-02 test added here |
| Test file (trim) | `tests\PropTraderTools.Tests\Features\PttTrimTests.cs` | B34-03 test added here |

---

## TICKET B34-02 — Add Buffer and Market Props to `IPttHostContext` + `TradeCopierPanel`

> **Implement this first.** B34-01 and B34-03 will not compile without it.

### Spec Requirements Satisfied

| Deferred Work ID | Description |
|---|---|
| DW-B33-02 | Buffer tick values (`BeBuffer`, `TrimBuffer`, `FlatBuffer`) not present on `IPttHostContext` |
| DW-B33-04 (partial) | `IPttHostContext` must expose `Ask` and `Bid` for Trim/Flatten limit order path |

### Files to READ Before Editing

1. `src\PropTraderTools\Core\PttContracts.cs`
   - Find `IPttHostContext` interface declaration.
   - Confirm the 3 existing properties: `LeaderAccount`, `Instrument`, `AllAccounts`.
   - Identify the exact closing brace `}` of the interface.
   - New properties are inserted BEFORE this closing brace.

2. `src\PropTraderTools\TradeCopierPanel.cs`
   - Run: `Select-String -Path .\src\PropTraderTools\TradeCopierPanel.cs -Pattern "IPttHostContext.AllAccounts"`
   - Note the line number returned (approximately line 130).
   - Confirm fields exist: `_beBuffer`, `_trimBuffer`, `_flattenBuffer` (all `int`).
     - Run: `Select-String -Path .\src\PropTraderTools\TradeCopierPanel.cs -Pattern "_beBuffer|_trimBuffer|_flattenBuffer"`
   - Confirm methods exist: `GetAsk()`, `GetBid()`.
     - Run: `Select-String -Path .\src\PropTraderTools\TradeCopierPanel.cs -Pattern "GetAsk\(\)|GetBid\(\)"`
   - The 5 new explicit interface implementations are inserted on the line AFTER `AllAccounts`.

### Files to Write/Edit

| File | Change Type |
|---|---|
| `src\PropTraderTools\Core\PttContracts.cs` | Add 5 properties to `IPttHostContext` interface |
| `src\PropTraderTools\TradeCopierPanel.cs` | Add 5 explicit interface implementations |

### Exact Diff Plan — `PttContracts.cs`

**BEFORE** (end of `IPttHostContext` interface, 3 existing props then closing brace):
```csharp
    IReadOnlyList<Account> AllAccounts { get; }
}
```

**AFTER** (5 new properties inserted before closing brace):
```csharp
    IReadOnlyList<Account> AllAccounts { get; }

    // B34 additions — buffer props and live market quote.
    /// <summary>Break-even buffer in ticks. From TradeCopierPanel._beBuffer.</summary>
    int BeBuffer { get; }
    /// <summary>Trim buffer in ticks. From TradeCopierPanel._trimBuffer.</summary>
    int TrimBuffer { get; }
    /// <summary>Flatten buffer in ticks. From TradeCopierPanel._flattenBuffer.</summary>
    int FlatBuffer { get; }
    /// <summary>Current ask price from instrument market data. Returns 0.0 if no quote.</summary>
    double Ask { get; }
    /// <summary>Current bid price from instrument market data. Returns 0.0 if no quote.</summary>
    double Bid { get; }
}
```

**Type rationale:** `int` for buffer props — backing fields `_beBuffer`, `_trimBuffer`, `_flattenBuffer` are declared `private int` in TradeCopierPanel. No cast needed. `double` for Ask/Bid — NT8 market data prices are `double`.

### Exact Diff Plan — `TradeCopierPanel.cs`

**Insertion point:** Line immediately after `IReadOnlyList<Account> IPttHostContext.AllAccounts { get { return _allAccounts; } }`.

**BEFORE** (line ~130, after AllAccounts impl, before next member):
```csharp
        IReadOnlyList<Account> IPttHostContext.AllAccounts { get { return _allAccounts; } }

        // (next existing member)
```

**AFTER** (5 new lines inserted after AllAccounts line):
```csharp
        IReadOnlyList<Account> IPttHostContext.AllAccounts { get { return _allAccounts; } }

        // B34 T2 -- Buffer props and market quote props wired to existing private fields/methods.
        int    IPttHostContext.BeBuffer   { get { return _beBuffer; } }
        int    IPttHostContext.TrimBuffer { get { return _trimBuffer; } }
        int    IPttHostContext.FlatBuffer { get { return _flattenBuffer; } }
        double IPttHostContext.Ask        { get { return GetAsk(); } }
        double IPttHostContext.Bid        { get { return GetBid(); } }

        // (next existing member — unchanged)
```

**NT8-001 compliance:** All use `{ get { return ...; } }` — NOT `{ get; init; }` ✓  
**CYC:** All 5 property getters = CYC 1 each ✓

### Method Signatures (new)

```csharp
// In IPttHostContext (PttContracts.cs):
int    BeBuffer   { get; }   // CYC 1
int    TrimBuffer { get; }   // CYC 1
int    FlatBuffer { get; }   // CYC 1
double Ask        { get; }   // CYC 1
double Bid        { get; }   // CYC 1

// In TradeCopierPanel (explicit interface implementations):
int    IPttHostContext.BeBuffer   { get { return _beBuffer; } }     // CYC 1
int    IPttHostContext.TrimBuffer { get { return _trimBuffer; } }   // CYC 1
int    IPttHostContext.FlatBuffer { get { return _flattenBuffer; } }// CYC 1
double IPttHostContext.Ask        { get { return GetAsk(); } }      // CYC 1
double IPttHostContext.Bid        { get { return GetBid(); } }      // CYC 1
```

### [Fact] Tests — B34-02

Add to `tests\PropTraderTools.Tests\Core\PttContractsTests.cs`.

**Count to add: 1**

```csharp
[Fact]
public void T_B34_ContextBeBuffer_Forwarded()
{
    // Verify IPttHostContext has all 5 B34 buffer/quote properties with correct types.
    var iface = typeof(IPttHostContext);

    var beBufferProp   = iface.GetProperty("BeBuffer");
    var trimBufferProp = iface.GetProperty("TrimBuffer");
    var flatBufferProp = iface.GetProperty("FlatBuffer");
    var askProp        = iface.GetProperty("Ask");
    var bidProp        = iface.GetProperty("Bid");

    Assert.NotNull(beBufferProp);
    Assert.NotNull(trimBufferProp);
    Assert.NotNull(flatBufferProp);
    Assert.NotNull(askProp);
    Assert.NotNull(bidProp);

    Assert.Equal(typeof(int),    beBufferProp.PropertyType);
    Assert.Equal(typeof(int),    trimBufferProp.PropertyType);
    Assert.Equal(typeof(int),    flatBufferProp.PropertyType);
    Assert.Equal(typeof(double), askProp.PropertyType);
    Assert.Equal(typeof(double), bidProp.PropertyType);
}
```

### NT8 Rule Constraints — B34-02

| Rule | Check | Verdict |
|---|---|---|
| NT8-001 | No `{ get; init; }` accessor used — confirmed `{ get { return ...; } }` pattern | MUST PASS |
| NT8-006 | No LINQ in property getter bodies | MUST PASS |

### JS Rule Constraints — B34-02

| Rule | Check | Verdict |
|---|---|---|
| JS-021 | No `lock()` in property getters | MUST PASS |
| JS-033 | No `async void` | MUST PASS |
| JS-001 | No `throw` in property getter bodies | MUST PASS |
| JS-002 | No `return null` — all return value types (`int`, `double`) | MUST PASS |

### 7-Scan Checklist — B34-02

Run all 7 before declaring B34-02 BUILD_PASS. All commands relative to Wave workspace root `C:\WSGTA\universal-or-strategy\`.

```
SCAN-01  grep "lock(" src\PropTraderTools\Core\PttContracts.cs
         grep "lock(" src\PropTraderTools\TradeCopierPanel.cs
         → Expected: 0 results in B34-modified lines

SCAN-02  Select-String -Path src\PropTraderTools\Core\PttContracts.cs -Pattern "async void "
         Select-String -Path src\PropTraderTools\TradeCopierPanel.cs  -Pattern "async void "
         → Expected: 0 new results

SCAN-03  Select-String -Path src\PropTraderTools\Core\PttContracts.cs  -Pattern "\.Where|\.First|\.Select|\.Any"
         Select-String -Path src\PropTraderTools\TradeCopierPanel.cs   -Pattern "\.Where|\.First|\.Select|\.Any"
         → Expected: 0 results in new B34-added lines

SCAN-04  Select-String -Path src\PropTraderTools\Core\PttContracts.cs  -Pattern "get; init;"
         Select-String -Path src\PropTraderTools\TradeCopierPanel.cs   -Pattern "get; init;"
         → Expected: 0 results

SCAN-05  Select-String -Path src\PropTraderTools\Core\PttContracts.cs  -Pattern "acc\.Positions\["
         Select-String -Path src\PropTraderTools\TradeCopierPanel.cs   -Pattern "acc\.Positions\["
         → Expected: 0 results

SCAN-06  dotnet build src\PropTraderTools\PropTraderTools.csproj
         → Expected: 0 new errors. Pre-existing LSP-only errors (max 3) acceptable.
         NOTE: B34-01 and B34-03 will show compile errors until their own tickets are done.
               The scope of this SCAN-06 is: PttContracts.cs and TradeCopierPanel.cs compile cleanly.

SCAN-07  Select-String -Path tests\PropTraderTools.Tests\ -Pattern "\[Fact\]" -Recurse | Measure-Object | Select-Object Count
         → Expected: >= 172 after B34-02 (171 baseline + 1 new test T_B34_ContextBeBuffer_Forwarded)
```

### Acceptance Criteria — B34-02

- [ ] `IPttHostContext` in `PttContracts.cs` has exactly 5 new properties: `BeBuffer`, `TrimBuffer`, `FlatBuffer`, `Ask`, `Bid`
- [ ] All 5 use plain getter-only syntax (no `init`)
- [ ] `TradeCopierPanel.cs` has 5 new explicit interface implementations directly after `AllAccounts`
- [ ] `GetAsk()` and `GetBid()` confirmed present in `TradeCopierPanel.cs` BEFORE editing (grep first)
- [ ] SCAN-01 through SCAN-05: 0 hits in modified lines
- [ ] SCAN-06: 0 new compile errors in `PttContracts.cs` and `TradeCopierPanel.cs`
- [ ] SCAN-07: `[Fact]` count >= 172
- [ ] `T_B34_ContextBeBuffer_Forwarded` passes

---

## TICKET B34-01 — Rewrite `PttBreakEven.Execute()`

> **Prerequisite: B34-02 must be fully implemented and compiling before starting B34-01.**
> Verify: `Select-String -Path src\PropTraderTools\Core\PttContracts.cs -Pattern "BeBuffer"` returns a hit.

### Spec Requirements Satisfied

| Deferred Work ID | Description |
|---|---|
| DW-B33-05 | `isLong` derived from `leaderPos` OUTSIDE foreach — short followers get wrong `OrderAction` |
| DW-B33-06 | `bePrice` = leader's `AveragePrice`, no sign flip, no buffer — wrong stop for every follower |
| DW-B33-07 | `CancelStaleBracketsLocal` called once before loop for leader only — followers retain stale brackets |

### Files to READ Before Editing

1. `src\PropTraderTools\Features\PttBreakEven.cs`
   - Locate `Execute(IPttHostContext ctx)` — identify current loop structure (lines ~52–67 approx).
   - Confirm current code has the 3 bugs: `isLong` from `leaderPos` outside loop, `bePrice = entryPrice` (no buffer), single `CancelStaleBracketsLocal` before loop.
   - Confirm private static helpers:
     - `FindPositionLocal(Account, Instrument)` returns `Position`
     - `CancelStaleBracketsLocal(Account, Instrument)` — note it currently takes `(Account, Instrument)`
     - `SubmitBeStopLocal(Account, Instrument, double, bool)`
   - Run: `Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "private static"`

2. `src\PropTraderTools\Core\PttContracts.cs`
   - Confirm `BeBuffer` property exists (B34-02 done).
   - Confirm `ctx.Instrument.MasterInstrument.TickSize` is accessible via the existing `Instrument` property.

### Files to Write/Edit

| File | Change Type |
|---|---|
| `src\PropTraderTools\Features\PttBreakEven.cs` | Replace full body of `Execute(IPttHostContext ctx)` |
| `tests\PropTraderTools.Tests\Features\PttBreakEvenTests.cs` | Add 4 new `[Fact]` methods |

### Exact Diff Plan — `PttBreakEven.cs`

**BEFORE** (current buggy `Execute` body — the ENTIRE method body, replacing only the lines between the opening `{` and closing `}` of `Execute`):

```csharp
public void Execute(IPttHostContext ctx)
{
    if (!IsEnabled) return;

    Position leaderPos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
    if (leaderPos == null || leaderPos.Quantity == 0) return;

    double entryPrice = leaderPos.AveragePrice;
    bool   isLong     = leaderPos.MarketPosition == MarketPosition.Long;
    double bePrice    = entryPrice;

    // Cancel stale ATM bracket orders BEFORE submitting new stops (NT8-051).
    // Called once for leader account -- followers share the same ATM bracket context.
    CancelStaleBracketsLocal(ctx.LeaderAccount, ctx.Instrument);

    // DW-B36-01: loop ALL accounts so leader AND followers get the BE stop.
    foreach (Account acc in ctx.AllAccounts)
        SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);

    PttBus.RaiseBe(this, new BeEventArgs(
        ctx.Instrument, bePrice, entryPrice, isLong, string.Empty));
}
```

**AFTER** (corrected `Execute` body — full method replacement):

```csharp
public void Execute(IPttHostContext ctx)
{
    if (!IsEnabled) return;                                                    // (1) guard

    Position leaderPos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
    if (leaderPos == null || leaderPos.Quantity == 0) return;                  // (2) leader guard

    double tickSize = ctx.Instrument.MasterInstrument.TickSize;
    double buf      = (double)ctx.BeBuffer;                                    // DW-B33-06 FIX

    // DW-B33-05/06/07: per-account loop. Each account uses its own
    // position, direction, entry price, and buffer sign. Cancel is per-account.
    foreach (Account acc in ctx.AllAccounts)                                   // (3) foreach
    {
        Position pos = FindPositionLocal(acc, ctx.Instrument);
        if (pos == null || pos.Quantity == 0) continue;                        // (3a) flat guard

        bool   isLong  = pos.MarketPosition == MarketPosition.Long;            // DW-B33-05 FIX
        double bePrice = pos.AveragePrice
                         + (isLong ? +buf : -buf) * tickSize;                  // DW-B33-06 FIX

        CancelStaleBracketsLocal(acc, ctx.Instrument);                         // DW-B33-07 FIX
        SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);
    }

    // Notify bus with leader context for downstream listeners.
    // NOTE DW-B34-RAISE-01: carries leader values only (mixed-direction deferred).
    bool   leaderIsLong  = leaderPos.MarketPosition == MarketPosition.Long;
    double leaderBePrice = leaderPos.AveragePrice
                           + (leaderIsLong ? +buf : -buf) * tickSize;
    PttBus.RaiseBe(this, new BeEventArgs(                                       // (4) event
        ctx.Instrument, leaderBePrice, leaderPos.AveragePrice,
        leaderIsLong, string.Empty));
}
```

**CYC verification (target ≤ 8):**

| Branch point | Count |
|---|---|
| Baseline | +1 |
| `if (!IsEnabled)` | +1 |
| `if (leaderPos == null \|\| ...Quantity == 0)` — two operands with `\|\|` | +2 |
| `foreach` loop | +1 |
| `if (pos == null \|\| ...Quantity == 0)` — two operands with `\|\|` | +2 |
| **Total CYC** | **7** ✓ |

**No other methods in `PttBreakEven.cs` are modified.** `FindPositionLocal`, `CancelStaleBracketsLocal`, `SubmitBeStopLocal` signatures are UNCHANGED.

### Method Signatures

```csharp
// Changed (body only, signature unchanged):
public void Execute(IPttHostContext ctx)   // CYC 7

// Unchanged (confirm present, do NOT modify):
private static Position FindPositionLocal(Account acc, Instrument instr)
private static void CancelStaleBracketsLocal(Account acc, Instrument instr)
private static void SubmitBeStopLocal(Account acc, Instrument instr, double bePrice, bool isLong)
```

### [Fact] Tests — B34-01

Add to `tests\PropTraderTools.Tests\Features\PttBreakEvenTests.cs`.

**Count to add: 4** (total after B34-01: 172 → 176 baseline including B34-02 test)

**ADV-02 implementation note (from reviewer):** `SubmitBeStopLocal` is `private static`. Subclass override is not possible. All 4 tests use **reflection only** to verify structural correctness. No NT8 runtime invocation.

```csharp
[Fact]
public void T_B34_BE_ShortAccountBuyToCover()
{
    // Verify SubmitBeStopLocal has the isLong parameter (bool, position 3).
    // When isLong=false the implementation must select BuyToCover — structural guarantee.
    var mi = typeof(PttBreakEven).GetMethod(
        "SubmitBeStopLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    Assert.NotNull(mi);

    var parms = mi.GetParameters();
    Assert.Equal(4, parms.Length);
    // p[0] = Account, p[1] = Instrument, p[2] = double (bePrice), p[3] = bool (isLong)
    Assert.Equal(typeof(bool), parms[3].ParameterType);
    Assert.Equal("isLong", parms[3].Name);
}

[Fact]
public void T_B34_BE_PerAccountBePrice()
{
    // Verify Execute(IPttHostContext) has exactly 1 parameter of type IPttHostContext.
    // Per-account bePrice is a runtime invariant; verified structurally via signature.
    var mi = typeof(PttBreakEven).GetMethod(
        "Execute",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

    Assert.NotNull(mi);

    var parms = mi.GetParameters();
    Assert.Equal(1, parms.Length);
    Assert.Equal(typeof(IPttHostContext), parms[0].ParameterType);
}

[Fact]
public void T_B34_BE_CancelBeforeSubmitPerAccount()
{
    // Verify CancelStaleBracketsLocal(Account, Instrument) exists.
    // This is the per-account cancel helper invoked BEFORE SubmitBeStopLocal inside the loop.
    var miCancel = typeof(PttBreakEven).GetMethod(
        "CancelStaleBracketsLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    Assert.NotNull(miCancel);

    var cp = miCancel.GetParameters();
    Assert.Equal(2, cp.Length);
    Assert.Equal(typeof(Account), cp[0].ParameterType);
    Assert.Equal(typeof(Instrument), cp[1].ParameterType);

    // Also verify SubmitBeStopLocal(Account, Instrument, double, bool) exists.
    var miSubmit = typeof(PttBreakEven).GetMethod(
        "SubmitBeStopLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    Assert.NotNull(miSubmit);

    var sp = miSubmit.GetParameters();
    Assert.Equal(4, sp.Length);
    Assert.Equal(typeof(Account),     sp[0].ParameterType);
    Assert.Equal(typeof(Instrument),  sp[1].ParameterType);
    Assert.Equal(typeof(double),      sp[2].ParameterType);
    Assert.Equal(typeof(bool),        sp[3].ParameterType);
}

[Fact]
public void T_B34_BE_BufferShortFlipped()
{
    // Verify FindPositionLocal exists with correct signature and return type.
    // The buffer sign-flip logic (isLong ? +buf : -buf) is the core DW-B33-06 fix;
    // its correct existence is guaranteed when FindPositionLocal feeds per-account positions.
    var mi = typeof(PttBreakEven).GetMethod(
        "FindPositionLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    Assert.NotNull(mi);
    Assert.Equal(typeof(Position), mi.ReturnType);

    var parms = mi.GetParameters();
    Assert.Equal(2, parms.Length);
    Assert.Equal(typeof(Account),    parms[0].ParameterType);
    Assert.Equal(typeof(Instrument), parms[1].ParameterType);
}
```

**Test name mapping (from spec to reflection body):**

| Spec Name | Verifies |
|---|---|
| `T_B34_BE_ShortAccountBuyToCover` | `SubmitBeStopLocal` has `bool isLong` as param[3] — structural guarantee of BuyToCover branch |
| `T_B34_BE_PerAccountBePrice` | `Execute` has `IPttHostContext` param — per-account logic is inside this method |
| `T_B34_BE_CancelBeforeSubmitPerAccount` | Both `CancelStaleBracketsLocal(Account, Instrument)` and `SubmitBeStopLocal(Account, Instrument, double, bool)` exist |
| `T_B34_BE_BufferShortFlipped` | `FindPositionLocal(Account, Instrument) : Position` exists — feeds per-account data into the sign-flip formula |

### NT8 Rule Constraints — B34-01

| Rule | Check | Must Pass |
|---|---|---|
| NT8-006 | No LINQ — explicit `foreach` only, no `.Where`, `.First`, `.Select`, `.Any` | YES |
| NT8-050 | No `acc.Positions[instr]` — use `FindPositionLocal` | YES |
| NT8-049 | `SubmitBeStopLocal` arg order unchanged — `arg6=0, arg7=stopPrice` for StopMarket | YES |
| NT8-014 | Signal name `"PTT-BE-Stop"` in `SubmitBeStopLocal` unchanged | YES |
| NT8-013 | `DateTime.MaxValue` for GTC in `SubmitBeStopLocal` unchanged | YES |
| NT8-001 | No `{ get; init; }` introduced | YES |

### JS Rule Constraints — B34-01

| Rule | Check | Must Pass |
|---|---|---|
| JS-021 | No `lock()` in `Execute` or any B34-modified code | YES |
| JS-033 | No `async void` | YES |
| JS-001 | No `throw` in the rewritten `Execute` body | YES |
| JS-002 | `continue` used for flat-account guard inside loop (not `return null`) | YES |

### 7-Scan Checklist — B34-01

Run all 7 before declaring B34-01 BUILD_PASS.

```
SCAN-01  Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "lock\("
         → Expected: 0 results

SCAN-02  Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "async void "
         → Expected: 0 results

SCAN-03  Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "\.Where|\.First|\.Select|\.Any"
         → Expected: 0 results in Execute() body (LINQ banned — NT8-006)

SCAN-04  Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "get; init;"
         → Expected: 0 results (NT8-001)

SCAN-05  Select-String -Path src\PropTraderTools\Features\PttBreakEven.cs -Pattern "acc\.Positions\["
         → Expected: 0 results (NT8-050 — use FindPositionLocal)

SCAN-06  dotnet build src\PropTraderTools\PropTraderTools.csproj
         → Expected: 0 new errors in PttBreakEven.cs
         → BLOCKED-BY-DEPENDENCY NOTE: if B34-02 is not yet done, ctx.BeBuffer will be
           an unresolved symbol. This is expected and not a new error introduced by B34-01.
           Confirm B34-02 is done first before running SCAN-06 on B34-01.

SCAN-07  Select-String -Path tests\PropTraderTools.Tests\ -Pattern "\[Fact\]" -Recurse | Measure-Object | Select-Object Count
         → Expected: >= 176 after B34-01 (172 from B34-02 + 4 new tests)
```

### Acceptance Criteria — B34-01

- [ ] B34-02 is DONE (verified by grep for `BeBuffer` in `PttContracts.cs`)
- [ ] `Execute(IPttHostContext ctx)` body replaced exactly as specified above
- [ ] `isLong`, `bePrice`, and `CancelStaleBracketsLocal` calls are ALL inside the `foreach` loop
- [ ] `(isLong ? +buf : -buf) * tickSize` formula present in loop body (DW-B33-06)
- [ ] `CancelStaleBracketsLocal(acc, ctx.Instrument)` called before `SubmitBeStopLocal` for each `acc` (DW-B33-07)
- [ ] `PttBus.RaiseBe` uses `leaderBePrice` computed with leader's own direction and price
- [ ] No other methods in `PttBreakEven.cs` modified
- [ ] SCAN-01 through SCAN-05: 0 hits
- [ ] SCAN-06: 0 new errors
- [ ] SCAN-07: `[Fact]` count >= 176
- [ ] All 4 reflection tests pass

---

## TICKET B34-03 — Wire Buffer in `PttTrim` and `PttFlatten`

> **Prerequisite: B34-02 must be fully implemented and compiling before starting B34-03.**
> Verify: `Select-String -Path src\PropTraderTools\Core\PttContracts.cs -Pattern "TrimBuffer"` returns a hit.

### Spec Requirements Satisfied

| Deferred Work ID | Description |
|---|---|
| DW-B33-04 | `PttTrim`/`PttFlatten` use `OrderType.Market` regardless of buffer setting |

### Files to READ Before Editing

1. `src\PropTraderTools\Features\PttTrim.cs`
   - Locate `Execute(IPttHostContext ctx)` — find call to `TrimPositionLocal`.
   - Locate `TrimPositionLocal(...)` — read current signature (approximately 4 params) and body.
   - Confirm current `OrderType.Market` hardcode.
   - Run: `Select-String -Path src\PropTraderTools\Features\PttTrim.cs -Pattern "private static"`

2. `src\PropTraderTools\Features\PttFlatten.cs`
   - Same as above for `FlattenPositionLocal`.
   - Run: `Select-String -Path src\PropTraderTools\Features\PttFlatten.cs -Pattern "private static"`

3. `src\PropTraderTools\Core\PttContracts.cs`
   - Confirm `TrimBuffer`, `FlatBuffer`, `Ask`, `Bid` properties exist (B34-02 done).

### Files to Write/Edit

| File | Change Type |
|---|---|
| `src\PropTraderTools\Features\PttTrim.cs` | Add buffer/ask/bid params to `TrimPositionLocal`; update `Execute` call site |
| `src\PropTraderTools\Features\PttFlatten.cs` | Same pattern for `FlattenPositionLocal` |
| `tests\PropTraderTools.Tests\Features\PttTrimTests.cs` | Add 1 new `[Fact]` method |

### Exact Diff Plan — `PttTrim.cs`

#### Change 1: `TrimPositionLocal` — New Signature

**BEFORE:**
```csharp
private static void TrimPositionLocal(Account acc, Instrument instr, int qty, Position pos)
```

**AFTER:**
```csharp
private static void TrimPositionLocal(Account acc, Instrument instr,
                                      int qty, Position pos,
                                      int buffer, double ask, double bid)
```

#### Change 2: `TrimPositionLocal` — Body Logic Addition

Add the following block BEFORE the existing `CreateOrder` call inside `TrimPositionLocal`. The existing null/quantity guard at the top of the method is PRESERVED unchanged.

**BEFORE** (inside `TrimPositionLocal`, just before `CreateOrder`):
```csharp
    // existing code: OrderType hardcoded as Market
    // ... acc null check, qty check ...
    Account.CreateOrder(instr, OrderAction.Sell /* or BuyToCover */, OrderType.Market,
        OrderEntry.Manual, TimeInForce.Gtc, qty, 0, 0, null, "PTT-Trim",
        DateTime.MaxValue, (NinjaTrader.Cbi.CustomOrder)null);
```

**AFTER** (add buffer/limit logic BEFORE the CreateOrder call):
```csharp
    bool   isLong     = pos.MarketPosition == MarketPosition.Long;
    double tickSize   = instr.MasterInstrument.TickSize;
    double limitPrice = 0.0;
    OrderType orderType;

    if (buffer > 0)
    {
        // Long: sell into ask + buffer ticks. Short: buy into bid - buffer ticks.
        limitPrice = isLong
            ? ask + buffer * tickSize
            : bid - buffer * tickSize;
        orderType = OrderType.Limit;
    }
    else
    {
        orderType = OrderType.Market;   // buffer == 0: preserve B33 behavior exactly
    }

    // NT8-049: Limit → arg6=limitPrice, arg7=0. Market → arg6=0, arg7=0.
    Account.CreateOrder(instr,
        isLong ? OrderAction.Sell : OrderAction.BuyToCover,
        orderType,
        OrderEntry.Manual, TimeInForce.Gtc, qty,
        limitPrice,   // arg6: limitPrice (0 for Market)
        0,            // arg7: stopPrice (always 0 here)
        null, "PTT-Trim", DateTime.MaxValue,
        (NinjaTrader.Cbi.CustomOrder)null);
```

**NOTE:** If the existing `CreateOrder` call already has an `OrderAction` variable rather than a hardcoded enum value, preserve it and only change `OrderType.Market` → `orderType` and `arg6=0` → `limitPrice`. Do not duplicate any existing null/quantity guards.

#### Change 3: `Execute` — Update Call Site

**BEFORE:**
```csharp
TrimPositionLocal(ctx.LeaderAccount, ctx.Instrument, trimQty, pos);
```

**AFTER:**
```csharp
TrimPositionLocal(ctx.LeaderAccount, ctx.Instrument, trimQty, pos,
                  ctx.TrimBuffer, ctx.Ask, ctx.Bid);
```

**CYC of `TrimPositionLocal` new:**

| Branch point | Count |
|---|---|
| Baseline | +1 |
| Existing null/qty guard (if present, e.g. `if (acc == null \|\| qty <= 0)`) | +2–3 |
| `if (buffer > 0)` | +1 |
| Ternary `isLong ? ask+... : bid-...` | +1 |
| Existing try/catch (if present) | +1 |
| **Total CYC** | **≤ 7** ✓ |

**`Execute` CYC:** unchanged at 3 (only call-site parameter update, no new branches).

### Exact Diff Plan — `PttFlatten.cs`

**Mirrors `PttTrim.cs` exactly.** Apply the same 3 changes:

1. `FlattenPositionLocal` signature: add `int buffer, double ask, double bid` params
2. `FlattenPositionLocal` body: add buffer/limit logic block (same pattern, same NT8-049 note)
   - Signal name stays `"PTT-Flatten"` (NT8-014 ✓)
3. `Execute` call site: add `ctx.FlatBuffer, ctx.Ask, ctx.Bid`

```csharp
// Execute call site AFTER:
FlattenPositionLocal(ctx.LeaderAccount, ctx.Instrument, pos,
                     ctx.FlatBuffer, ctx.Ask, ctx.Bid);
```

### Method Signatures (after B34-03)

```csharp
// PttTrim.cs:
public void Execute(IPttHostContext ctx)                                     // CYC 3, unchanged
private static void TrimPositionLocal(Account acc, Instrument instr,        // CYC ≤ 7
                                      int qty, Position pos,
                                      int buffer, double ask, double bid)

// PttFlatten.cs:
public void Execute(IPttHostContext ctx)                                     // CYC 3, unchanged
private static void FlattenPositionLocal(Account acc, Instrument instr,     // CYC ≤ 7
                                         Position pos,
                                         int buffer, double ask, double bid)
```

### [Fact] Tests — B34-03

Add to `tests\PropTraderTools.Tests\Features\PttTrimTests.cs`.

**Count to add: 1**

```csharp
[Fact]
public void T_B34_Trim_BufferContextWired()
{
    // Verify TrimPositionLocal signature now accepts buffer, ask, bid parameters.
    // This directly confirms the B34-03 change is present.
    var mi = typeof(PttTrim).GetMethod(
        "TrimPositionLocal",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    Assert.NotNull(mi);

    var parms = mi.GetParameters();
    // Minimum 5 params: (Account, Instrument, int qty, Position pos, int buffer, ...)
    // After B34-03 it should be 7: acc, instr, qty, pos, buffer, ask, bid
    Assert.True(parms.Length >= 5,
        $"TrimPositionLocal should accept buffer params (buffer, ask, bid). Found {parms.Length} params.");

    // Verify the buffer param is int and ask/bid params are double.
    // Parameter indices: 0=Account, 1=Instrument, 2=qty(int), 3=Position, 4=buffer(int), 5=ask(double), 6=bid(double)
    if (parms.Length >= 7)
    {
        Assert.Equal(typeof(int),    parms[4].ParameterType);   // buffer
        Assert.Equal(typeof(double), parms[5].ParameterType);   // ask
        Assert.Equal(typeof(double), parms[6].ParameterType);   // bid
    }
}
```

### NT8 Rule Constraints — B34-03

| Rule | Check | Must Pass |
|---|---|---|
| NT8-006 | No LINQ in `TrimPositionLocal` or `FlattenPositionLocal` | YES |
| NT8-007 | `arg11 = (NinjaTrader.Cbi.CustomOrder)null` — unchanged | YES |
| NT8-013 | `DateTime.MaxValue` for GTC — unchanged | YES |
| NT8-014 | Signal `"PTT-Trim"` and `"PTT-Flatten"` — unchanged | YES |
| NT8-049 | Limit: `arg6=limitPrice, arg7=0`. Market: `arg6=0, arg7=0`. Arg order correct. | YES |
| NT8-050 | `FindPositionLocal` unchanged — no `acc.Positions[instr]` | YES |

### JS Rule Constraints — B34-03

| Rule | Check | Must Pass |
|---|---|---|
| JS-021 | No `lock()` in modified methods | YES |
| JS-033 | No `async void` | YES |
| JS-001 | No `throw` in `TrimPositionLocal` / `FlattenPositionLocal` hot path | YES |
| JS-002 | No new `return null` | YES |

### 7-Scan Checklist — B34-03

Run all 7 before declaring B34-03 BUILD_PASS.

```
SCAN-01  Select-String -Path src\PropTraderTools\Features\PttTrim.cs    -Pattern "lock\("
         Select-String -Path src\PropTraderTools\Features\PttFlatten.cs -Pattern "lock\("
         → Expected: 0 results

SCAN-02  Select-String -Path src\PropTraderTools\Features\PttTrim.cs    -Pattern "async void "
         Select-String -Path src\PropTraderTools\Features\PttFlatten.cs -Pattern "async void "
         → Expected: 0 results

SCAN-03  Select-String -Path src\PropTraderTools\Features\PttTrim.cs    -Pattern "\.Where|\.First|\.Select|\.Any"
         Select-String -Path src\PropTraderTools\Features\PttFlatten.cs -Pattern "\.Where|\.First|\.Select|\.Any"
         → Expected: 0 results in modified code

SCAN-04  Select-String -Path src\PropTraderTools\Features\PttTrim.cs    -Pattern "get; init;"
         Select-String -Path src\PropTraderTools\Features\PttFlatten.cs -Pattern "get; init;"
         → Expected: 0 results

SCAN-05  Select-String -Path src\PropTraderTools\Features\PttTrim.cs    -Pattern "acc\.Positions\["
         Select-String -Path src\PropTraderTools\Features\PttFlatten.cs -Pattern "acc\.Positions\["
         → Expected: 0 results

SCAN-06  dotnet build src\PropTraderTools\PropTraderTools.csproj
         → Expected: 0 new errors in PttTrim.cs and PttFlatten.cs
         → Pre-existing LSP-only errors (max 3) acceptable

SCAN-07  Select-String -Path tests\PropTraderTools.Tests\ -Pattern "\[Fact\]" -Recurse | Measure-Object | Select-Object Count
         → Expected: >= 177 after B34-03 (176 from B34-01/02 + 1 new T_B34_Trim_BufferContextWired)
```

### Acceptance Criteria — B34-03

- [ ] B34-02 is DONE (verified by grep for `TrimBuffer`, `FlatBuffer`, `Ask`, `Bid` in `PttContracts.cs`)
- [ ] `TrimPositionLocal` signature extended with `int buffer, double ask, double bid` params
- [ ] `FlattenPositionLocal` signature extended with `int buffer, double ask, double bid` params
- [ ] Buffer > 0 path uses `OrderType.Limit` with `arg6=limitPrice, arg7=0` (NT8-049)
- [ ] Buffer == 0 path keeps `OrderType.Market` (B33 behavior preserved)
- [ ] `Execute` call sites updated in both `PttTrim` and `PttFlatten`
- [ ] SCAN-01 through SCAN-05: 0 hits in modified files
- [ ] SCAN-06: 0 new compile errors
- [ ] SCAN-07: `[Fact]` count >= 177
- [ ] `T_B34_Trim_BufferContextWired` passes

---

## TICKET B34-04 — Verifier Pass + Tag Update

> **Prerequisite: B34-01, B34-02, and B34-03 must ALL be complete and building cleanly.**

### Spec Requirements Satisfied

This ticket closes the block by tagging the source and verifying the full baseline.

### Files to READ Before Editing

1. `src\PropTraderTools\CopyEngine.cs`
   - Confirm current tag on line 41: `"PTT-COPIER B33 | modular-independence | 2026-07-25"`
   - This is the ONLY line changed in B34-04.

### Files to Write/Edit

| File | Change Type |
|---|---|
| `src\PropTraderTools\CopyEngine.cs` | Update tag string on line 41 only |

### Exact Diff Plan — `CopyEngine.cs`

**BEFORE** (line 41):
```csharp
"PTT-COPIER B33 | modular-independence | 2026-07-25"
```

**AFTER** (line 41 — use the actual UTC date of implementation):
```csharp
"PTT-COPIER B34 | be-multiAccount-fixes | {YYYY-MM-DD}"
```

Replace `{YYYY-MM-DD}` with the actual calendar date when the engineer implements B34-04 (e.g., `2026-07-28`). Use the date the tag is written, not the date tickets were generated.

**No other changes to `CopyEngine.cs`.** No logic, no imports, no comments — tag string update only.

### Method Signatures

No methods added or changed.

### [Fact] Tests — B34-04

**None.** B34-04 is a verification-only ticket. All 6 tests were added in B34-01, B34-02, and B34-03.

### NT8 Rule Constraints — B34-04

Not applicable — no logic code changes.

### JS Rule Constraints — B34-04

Not applicable — string literal update only.

### 7-Scan Checklist — B34-04 (Full Block Verification)

This is the definitive block-level scan. Run across ALL B34-modified files.

```
SCAN-01  Select-String -Path src\PropTraderTools\ -Pattern "lock\(" -Recurse -Include "*.cs"
         Filter to: PttBreakEven.cs, PttContracts.cs, TradeCopierPanel.cs, PttTrim.cs, PttFlatten.cs, CopyEngine.cs
         → Expected: 0 results in any B34-modified file (excluding comments)

SCAN-02  Select-String -Path src\PropTraderTools\ -Pattern "async void " -Recurse -Include "*.cs"
         → Expected: 0 results in any B34-modified file

SCAN-03  Select-String -Path src\PropTraderTools\Features\  -Pattern "\.Where|\.First|\.Select|\.Any" -Recurse -Include "*.cs"
         Select-String -Path src\PropTraderTools\Core\       -Pattern "\.Where|\.First|\.Select|\.Any" -Recurse -Include "*.cs"
         → Expected: 0 results in B34-added code

SCAN-04  Select-String -Path src\PropTraderTools\ -Pattern "get; init;" -Recurse -Include "*.cs"
         → Expected: 0 results in any B34-modified file

SCAN-05  Select-String -Path src\PropTraderTools\ -Pattern "acc\.Positions\[" -Recurse -Include "*.cs"
         → Expected: 0 results in any B34-modified file

SCAN-06  dotnet build src\PropTraderTools\PropTraderTools.csproj
         → Expected: 0 errors, 0 new warnings across all B34-modified files
         → Pre-existing LSP-only errors (max 3) acceptable — do NOT attempt to fix pre-existing
         → F5 in NinjaTrader: GREEN

SCAN-07  Select-String -Path tests\PropTraderTools.Tests\ -Pattern "\[Fact\]" -Recurse | Measure-Object | Select-Object Count
         → Expected: >= 177 (171 baseline + 6 new tests)
         → Run: dotnet test tests\PropTraderTools.Tests\PropTraderTools.Tests.csproj
         → Expected: 177 PASS, 0 FAIL, 0 regressions

ADDITIONAL — verify tag update:
         Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "PTT-COPIER B34"
         → Expected: 1 result on line 41

ADDITIONAL — verify_links.ps1:
         powershell -File scripts\verify_links.ps1 -Fix
         → Expected: 0 broken links
```

### Acceptance Criteria — B34-04

- [ ] `CopyEngine.cs` line 41 tag reads `"PTT-COPIER B34 | be-multiAccount-fixes | {date}"`
- [ ] `scripts\verify_links.ps1 -Fix` runs with 0 errors
- [ ] `dotnet build` exits 0, no new errors
- [ ] F5 in NinjaTrader: GREEN
- [ ] `dotnet test` result: **177 PASS**, 0 FAIL
- [ ] SCAN-01: 0 `lock(` hits in any B34-modified file
- [ ] SCAN-02: 0 `async void` hits
- [ ] SCAN-03: 0 LINQ hits in new B34 code
- [ ] SCAN-04: 0 `get; init;` hits
- [ ] SCAN-05: 0 `acc.Positions[` hits
- [ ] SCAN-06: 0 build errors
- [ ] SCAN-07: >= 177 `[Fact]`

---

## Test Summary — All 6 New [Fact] Tests

| # | Test Name | File | Ticket | Strategy |
|---|---|---|---|---|
| 1 | `T_B34_BE_ShortAccountBuyToCover` | `PttBreakEvenTests.cs` | B34-01 | Reflection: `SubmitBeStopLocal` param[3] is `bool isLong` |
| 2 | `T_B34_BE_PerAccountBePrice` | `PttBreakEvenTests.cs` | B34-01 | Reflection: `Execute` has 1 param of type `IPttHostContext` |
| 3 | `T_B34_BE_CancelBeforeSubmitPerAccount` | `PttBreakEvenTests.cs` | B34-01 | Reflection: both `CancelStaleBracketsLocal(Account,Instrument)` and `SubmitBeStopLocal(Account,Instrument,double,bool)` exist |
| 4 | `T_B34_BE_BufferShortFlipped` | `PttBreakEvenTests.cs` | B34-01 | Reflection: `FindPositionLocal(Account,Instrument) : Position` exists |
| 5 | `T_B34_ContextBeBuffer_Forwarded` | `PttContractsTests.cs` | B34-02 | Reflection: `IPttHostContext` has `BeBuffer(int)`, `TrimBuffer(int)`, `FlatBuffer(int)`, `Ask(double)`, `Bid(double)` |
| 6 | `T_B34_Trim_BufferContextWired` | `PttTrimTests.cs` | B34-03 | Reflection: `TrimPositionLocal` has ≥5 params; param[4]=int(buffer), param[5]=double(ask), param[6]=double(bid) |

**Baseline: 171 + 6 = 177 minimum [Fact] count after B34.**

---

## Deferred Work Created by B34

| DW ID | Description | Target Block |
|---|---|---|
| DW-B34-RAISE-01 | `PttBus.RaiseBe` carries leader values only — incorrect for mixed-direction portfolios | B35+ |
| DW-B34-TRIM-02 | Confirm `PttCopier` relay also passes `ask`/`bid` for follower trim copies | B35 relay audit |

---

*Tickets author: ptt-architect | Block: B34 | Phase 3 | 2026-07-27*
*Source plan: docs/brain/B34-multiAcct/02-architecture-plan.md (REVIEW_PASS)*
*Reviewer advisories incorporated: ADV-01 (manual NT8-049 verify note), ADV-02 (reflection-only tests), ADV-03 (NT8-029 implicit compliance noted), ADV-04 (T_B34_Trim promoted to direct signature check)*
