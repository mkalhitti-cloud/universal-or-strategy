# B33 — Modular Independence Architecture — Tickets
# Version: 1.1 | Status: TICKETS_FIXED (TICKET_REVIEW_FAIL → re-submission)
# Fixes: T6-TEST-01 (ICopyEngine interface + PttCopier constructor), T8-NT8-01 (remove Enumerable.Empty)
# Author: ptt-architect
# Source plan: docs/brain/B33-Modular/02-architecture-plan.md (REVIEW_PASS)
# Source review: docs/brain/B33-Modular/02-plan-review.md (REVIEW_PASS — no violations)
# Wave workspace: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
# Baseline: 164 [Fact] | build tag "PTT-COPIER B35 | bracket-cancel + BE-fixes | 2026-07-23"
# Target: 170 [Fact] | build tag "PTT-COPIER B33 | modular-independence | 2026-07-{DATE}"

---

## DEPENDENCY ORDER

T1 → T2, T3, T4, T5, T6 (all Features depend on Core/PttContracts.cs)
T6 → T8 partial (PttCopier calls CopyEngine relay methods; T8 adds them)
T7 depends on T1–T6 (panel wires all modules)
T8 depends on T6 (relay methods live in CopyEngine, touched in T8 anyway)
T1–T5 are independently buildable after T1 is merged.
Engineer sequence: T1 → T2 → T3 → T4 → T5 → T6+T8 together → T7

---

## TICKET T1 — Core/PttContracts.cs (NEW FILE)

### Spec Requirements Satisfied
- B33-01: IPttModule interface (4 members)
- B33-01: IPttHostContext interface (3 members)
- B33-01: ICopyEngine interface (4 relay method signatures) — NEW: added by T6-TEST-01 fix
- B33-01: PttBus static event hub (4 events + 4 Raise methods)
- B33-01: BeEventArgs, TrimEventArgs, FlatEventArgs, CancelEventArgs (4 event arg classes)

### File
```
CREATE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs
```

### Dependency Rule
T1 has NO dependencies on any other B33 ticket. T1 must be implemented and building before
T2–T6 can proceed. All other tickets depend on T1.

### JS Rule Constraints
- JS-021: No lock() — PttBus events use CLR multicast delegate (thread-safe += / -= on UI thread)
- JS-033: No async void — no async anywhere in this file
- JS-001: No throw in hot paths — null guards use early return only
- JS-002: return null is NOT used (all methods are void or raise events)

### NT8 Rule Constraints
- NT8-001: EventArgs use {get; private set;} + constructor — NO {get; init;}
- NT8-002: All EventArgs are `class : EventArgs` — NO records
- NT8-003: No volatile double — no double fields in this file
- NT8-043: PttBus.Raise* uses ?.Invoke (null-conditional *call*, valid C# 7.3) — NOT null-conditional assignment
- NT8-044: Explicit `using System;` required

### Complete Implementation

```csharp
// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs
// B33 — Modular Independence: shared contracts, event hub, event args
// NT8-044: using System required — NT8 does not auto-inject System namespace

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    // ─────────────────────────────────────────────────────────────────────────
    // INTERFACES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Contract for every PTT trading module.
    /// Initialize() on panel attach; Teardown() on panel detach.
    /// All calls on UI thread only.
    /// </summary>
    public interface IPttModule
    {
        /// <summary>Stable module identifier ("BE", "TRIM", "FLAT", "CANCEL", "COPY").</summary>
        string ModuleId { get; }

        /// <summary>When false, Execute() is a no-op. Controlled by license bool.</summary>
        bool IsEnabled { get; }

        /// <summary>Subscribe to PttBus events here. UI thread only.</summary>
        void Initialize(IPttHostContext ctx);

        /// <summary>Unsubscribe all PttBus events here. UI thread only.</summary>
        void Teardown();
    }

    /// <summary>
    /// Read-only trading context passed to module Execute() calls.
    /// Implemented by TradeCopierPanel (T7).
    /// NT8-021: AllAccounts populated in panel Initialize handler, NOT in constructor.
    /// </summary>
    public interface IPttHostContext
    {
        /// <summary>Leader account (source of truth for sizing and position reads).</summary>
        Account LeaderAccount { get; }

        /// <summary>Currently tracked instrument (e.g. NQ 09-26).</summary>
        Instrument Instrument { get; }

        /// <summary>Leader + all follower accounts. Built at panel init time.</summary>
        IReadOnlyList<Account> AllAccounts { get; }
    }

    /// <summary>
    /// Relay contract for CopyEngine — the 4 public methods PttCopier calls.
    /// Implemented by CopyEngine (T8). Used as PttCopier constructor param so tests
    /// can inject MockCopyEngineRelay : ICopyEngine without subclassing CopyEngine.
    /// T6-TEST-01 fix: ICopyEngine enables testable constructor injection.
    /// NT8: interface members are void — no LINQ, no async, no init accessors.
    /// </summary>
    public interface ICopyEngine
    {
        /// <summary>Fan out BE stop to all follower accounts.</summary>
        void RelayBe(BeEventArgs e);

        /// <summary>Fan out trim to all follower accounts.</summary>
        void RelayTrim(TrimEventArgs e);

        /// <summary>Fan out flatten to all follower accounts.</summary>
        void RelayFlatten(FlatEventArgs e);

        /// <summary>Fan out cancel entries to all follower accounts.</summary>
        void RelayCancel(CancelEventArgs e);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HUB
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Static CLR event hub for PTT module communication.
    ///
    /// THREADING CONTRACT:
    ///   Subscribe (+= ) only from IPttModule.Initialize()  — UI thread
    ///   Unsubscribe (-=) only from IPttModule.Teardown()   — UI thread
    ///   Fire (?.Invoke) from module.Execute()              — UI thread
    ///
    /// JS-021: No lock() needed. CLR += / -= are atomic (new delegate list).
    ///         All sub/unsub on same UI thread — zero contention.
    /// NT8-043: ?.Invoke is a null-conditional call (valid C# 7.3).
    ///          NOT null-conditional assignment — no CS8370.
    /// </summary>
    public static class PttBus
    {
        public static event EventHandler<BeEventArgs>     BeFired;
        public static event EventHandler<TrimEventArgs>   TrimFired;
        public static event EventHandler<FlatEventArgs>   FlatFired;
        public static event EventHandler<CancelEventArgs> CancelFired;

        internal static void RaiseBe     (object sender, BeEventArgs e)     { var h = BeFired;     if (h != null) h(sender, e); }
        internal static void RaiseTrim   (object sender, TrimEventArgs e)   { var h = TrimFired;   if (h != null) h(sender, e); }
        internal static void RaiseFlatted(object sender, FlatEventArgs e)   { var h = FlatFired;   if (h != null) h(sender, e); }
        internal static void RaiseCancel (object sender, CancelEventArgs e) { var h = CancelFired; if (h != null) h(sender, e); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT ARGS
    // NT8-001: {get; private set;} + constructor — NO {get; init;}
    // NT8-002: class : EventArgs — NO records
    // ─────────────────────────────────────────────────────────────────────────

    public class BeEventArgs : EventArgs
    {
        public Instrument Instrument  { get; private set; }
        public double     BePrice     { get; private set; }
        public double     EntryPrice  { get; private set; }
        public bool       IsLong      { get; private set; }
        public string     OcoGroup    { get; private set; }

        public BeEventArgs(Instrument instr, double bePrice, double entryPrice,
                           bool isLong, string ocoGroup)
        {
            Instrument  = instr;
            BePrice     = bePrice;
            EntryPrice  = entryPrice;
            IsLong      = isLong;
            OcoGroup    = ocoGroup ?? string.Empty;
        }
    }

    public class TrimEventArgs : EventArgs
    {
        public Instrument Instrument  { get; private set; }
        public int        TrimPercent { get; private set; }
        public int        ActualQty   { get; private set; }

        public TrimEventArgs(Instrument instr, int trimPercent, int actualQty)
        {
            Instrument  = instr;
            TrimPercent = trimPercent;
            ActualQty   = actualQty;
        }
    }

    public class FlatEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public FlatEventArgs(Instrument instr) { Instrument = instr; }
    }

    public class CancelEventArgs : EventArgs
    {
        public Instrument Instrument { get; private set; }
        public CancelEventArgs(Instrument instr) { Instrument = instr; }
    }
}
```

### Notes on Raise* implementation
The plan uses `?.Invoke(sender, e)` but NT8-043 only bans null-conditional *assignment* (left side
of -=/+=). A null-conditional *call* (`?.Invoke`) is valid C# 7.3 and compiles fine in NT8.
However, to be maximally safe against any edge-case NT8 Roslyn quirk, the above implementation
uses a local-copy-then-null-check pattern (`var h = BeFired; if (h != null) h(sender, e);`)
which is 100% valid C# 6.0+. Either pattern is acceptable; engineer may use ?.Invoke if
confirmed working in their NT8 build.

### xUnit Tests
No standalone tests for T1. Tested implicitly by T2–T6 module tests which all fire PttBus events.

### 7-Scan Checklist

```powershell
# SCAN-01: lock() banned
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs" -Pattern "lock\s*\("
# Expected: zero matches

# SCAN-02: async void banned
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs" -Pattern "async\s+void"
# Expected: zero matches

# SCAN-03: init accessor banned (NT8-001)
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero matches

# SCAN-04: CreateOrder (N/A — no CreateOrder in PttContracts.cs)
# Expected: zero matches (this file has no CreateOrder calls)

# SCAN-05: dead code references (N/A for T1)
# Run post-T8 on full tree (see T8 SCAN-05)

# SCAN-06: Positions[Instrument] banned (NT8-050)
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs" -Pattern "\.Positions\["
# Expected: zero matches

# SCAN-07: PttBus subscription cleanup in tests (N/A — no test code in T1)
# Verified in T2–T6 test SCAN-07 entries
```

### Build Verification
After creating file:
```powershell
# From Wave workspace root — verify flat namespace matches CopyEngine.cs
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\PttContracts.cs" -Pattern "^namespace"
# Expected: namespace PropTraderTools
```

---

## TICKET T2 — Features/PttBreakEven.cs (NEW FILE)

### Spec Requirements Satisfied
- B33-02: PttBreakEven module with Execute() looping ctx.AllAccounts
- B33-02: CancelStaleBracketsLocal() — cancel stale brackets before BE stop
- B33-02: SubmitBeStopLocal() — submit StopMarket at entry price for each account
- B33-02: DW-B36-01 fix baked in — SubmitBeStop called for ALL accounts, not leader only

### File
```
CREATE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs
```

### Dependency Rule
Depends on T1 (IPttModule, IPttHostContext, PttBus, BeEventArgs must exist).
Does NOT depend on T3–T8. Independently buildable after T1.

### JS Rule Constraints
- JS-021: No lock() — all on UI thread, no shared mutable state
- JS-033: No async void — Execute() is synchronous void
- JS-001: No throw in hot paths — null guards use early return
- JS-002: FindPosition returns null per NT8-050 pattern; every call site guards with null check

### NT8 Rule Constraints
- NT8-006: No .Any() — use explicit foreach + List<Order> accumulator
- NT8-007: CreateOrder arg11 = (NinjaTrader.Cbi.CustomOrder)null — NOT a string
- NT8-013: DateTime.MaxValue for CreateOrder expiry — NOT DateTime.Now
- NT8-014: Signal name "PTT-BE-Stop" — starts with "PTT-"
- NT8-031: OrderState.Working + Initialized only — no PendingSubmit (does not exist in NT8)
- NT8-044: using System; required for Math.Max
- NT8-049: arg6=0 (limitPrice), arg7=bePrice (stopPrice) — CRITICAL, never swap
- NT8-050: No Positions[Instrument] — FindPosition() uses foreach over acc.Positions
- NT8-051: CancelStaleBracketsLocal() called before SubmitBeStopLocal() — sim accounts don't auto-cancel

### Complete Implementation

```csharp
// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs
// B33 — Break-even module.
// Imports: Core/PttContracts.cs + NinjaTrader.Cbi ONLY.
// DOES NOT import CopyEngine.cs — calls acc.CreateOrder() directly per spec architecture decision.
// DW-B36-01 FIX: Execute() loops ctx.AllAccounts so all accounts receive SubmitBeStop.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Break-even module: cancel stale brackets + submit stop-at-entry for ALL accounts.
    /// Called from TradeCopierPanel.OnBreakEvenClick (UI thread).
    /// </summary>
    public class PttBreakEven : IPttModule
    {
        public string ModuleId  { get; private set; }
        public bool   IsEnabled { get; private set; }

        public PttBreakEven()
        {
            ModuleId  = "BE";
            IsEnabled = true;
        }

        /// <summary>No PttBus subscription needed — this module fires BeFired, does not listen.</summary>
        public void Initialize(IPttHostContext ctx) { }

        public void Teardown() { }

        /// <summary>Allow license gate to enable/disable this module.</summary>
        public void SetEnabled(bool enabled) { IsEnabled = enabled; }

        /// <summary>
        /// Execute break-even for all accounts in ctx.AllAccounts.
        /// CYC = 4: IsEnabled guard + null/qty guard + CancelStaleBrackets call + AllAccounts loop.
        /// NT8-049: arg6=limitPrice=0, arg7=stopPrice=bePrice — enforced in SubmitBeStopLocal.
        /// DW-B36-01: foreach (Account acc in ctx.AllAccounts) — NOT leader-only.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled) return;                                        // guard 1 — CYC+1

            Position leaderPos = FindPosition(ctx.LeaderAccount, ctx.Instrument);
            if (leaderPos == null || leaderPos.Quantity == 0) return;      // guard 2 — CYC+1

            double entryPrice = leaderPos.AveragePrice;
            bool   isLong     = leaderPos.MarketPosition == MarketPosition.Long;
            double bePrice    = entryPrice;

            // NT8-051: cancel stale brackets first (sim accounts do not auto-cancel)
            CancelStaleBracketsLocal(ctx.LeaderAccount, ctx.Instrument);

            // DW-B36-01: loop all accounts — CYC+1
            foreach (Account acc in ctx.AllAccounts)
                SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);

            PttBus.RaiseBe(this, new BeEventArgs(
                ctx.Instrument, bePrice, entryPrice, isLong, string.Empty));
        }

        // ── private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Cancel stale Working/Initialized orders for the given account + instrument.
        /// Mirrors CopyEngine.CancelStaleBrackets (B35 L1680) inline — no CopyEngine import.
        /// NT8-006: explicit foreach + List accumulator — no .Where() or .Any().
        /// NT8-031: Working + Initialized only — PendingSubmit does not exist in NT8.
        /// NT8-051: safe to call on real brokers — Cancel on non-Working orders is a no-op.
        /// CYC = 4: null guard + foreach + 3 fused bool conditions + Count guard.
        /// </summary>
        private static void CancelStaleBracketsLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null) return;

            var stale = new List<Order>();
            foreach (Order o in acc.Orders)
            {
                bool stateOk = o.OrderState == OrderState.Working
                            || o.OrderState == OrderState.Initialized;
                bool instrOk = o.Instrument != null
                            && o.Instrument.FullName == instr.FullName;
                bool notPtt  = o.Name != "PTT-BE-Stop";
                if (stateOk && instrOk && notPtt)
                    stale.Add(o);
            }
            if (stale.Count == 0) return;
            try { acc.Cancel(stale.ToArray()); } catch { /* ignore — order may have filled between check and cancel */ }
        }

        /// <summary>
        /// Submit a StopMarket order at bePrice for the given account.
        /// NT8-007: arg11 = (NinjaTrader.Cbi.CustomOrder)null — NOT a string.
        /// NT8-013: DateTime.MaxValue — NOT DateTime.Now.
        /// NT8-014: "PTT-BE-Stop" — starts with "PTT-".
        /// NT8-049: arg6=0 (limitPrice), arg7=bePrice (stopPrice) — NEVER swap.
        /// NT8-050: FindPosition via foreach — NOT Positions[Instrument].
        /// CYC = 3: null guard + qty guard + ternary for direction.
        /// </summary>
        private static void SubmitBeStopLocal(Account acc, Instrument instr,
                                              double bePrice, bool isLong)
        {
            if (acc == null || instr == null) return;

            Position pos = FindPosition(acc, instr);
            if (pos == null || pos.Quantity == 0) return;

            OrderAction direction = isLong ? OrderAction.Sell : OrderAction.Buy;

            acc.CreateOrder(
                instr,
                direction,
                OrderType.StopMarket,
                OrderEntry.Manual,
                TimeInForce.Day,
                pos.Quantity,                        // qty from live position (NT8-049 bug 3 fix)
                0,                                   // arg6: limitPrice = 0 (NT8-049: NEVER bePrice here)
                bePrice,                             // arg7: stopPrice = bePrice (NT8-049)
                string.Empty,                        // oco group
                "PTT-BE-Stop",                       // signal name (NT8-014)
                DateTime.MaxValue,                   // gtd (NT8-013: not DateTime.Now)
                (NinjaTrader.Cbi.CustomOrder)null);  // arg11 (NT8-007: not a string)
        }

        /// <summary>
        /// Find position for account+instrument.
        /// NT8-050: acc.Positions exposes int indexer only; Positions[Instrument] = CS1503.
        /// Returns null if no position — caller must guard.
        /// CYC = 2: foreach + single if.
        /// </summary>
        private static Position FindPosition(Account acc, Instrument instr)
        {
            foreach (Position p in acc.Positions)
                if (p.Instrument == instr)
                    return p;
            return null;
        }
    }
}
```

### xUnit [Fact] Tests
Add to `CopyEngineTests.cs` (or a new `PttModuleTests.cs` in the test project):

```csharp
// ── T_B33_BE_Standalone ────────────────────────────────────────────────────
[Fact]
public void T_B33_BE_Standalone()
{
    // Arrange
    int firedCount = 0;
    EventHandler<BeEventArgs> handler = (s, e) => firedCount++;
    PttBus.BeFired += handler;

    var sut = new PttBreakEven();
    // MockPttHostContext: leader has 1 long position qty=2, 1 account in AllAccounts
    var mockInstr   = BuildMockInstrument();
    var mockAccount = BuildMockAccountWithLongPosition(mockInstr, qty: 2, avgPrice: 18500.0);
    var ctx = new MockPttHostContext
    {
        LeaderAccount = mockAccount,
        Instrument    = mockInstr,
        AllAccounts   = new List<Account> { mockAccount }
    };
    sut.Initialize(ctx);

    try
    {
        // Act — PttCopier is NOT subscribed; only our counter handler is
        sut.Execute(ctx);

        // Assert: BeFired raised exactly once
        Assert.Equal(1, firedCount);
    }
    finally
    {
        // PttBus is static — MUST unsubscribe to prevent test leakage
        PttBus.BeFired -= handler;
    }
}

// ── T_B33_AllAccounts_BeLoop ───────────────────────────────────────────────
[Fact]
public void T_B33_AllAccounts_BeLoop()
{
    // Arrange: 3 accounts each with a long position
    // Uses stub accounts that record CreateOrder calls
    const int accountCount = 3;
    int submitBeCallCount  = 0;

    var mockInstr    = BuildMockInstrument();
    var mockAccounts = BuildMockAccountsWithLongPositions(
        mockInstr, count: accountCount, qty: 2, avgPrice: 18500.0,
        onCreateOrder: () => submitBeCallCount++);

    var sut = new PttBreakEven();
    var ctx = new MockPttHostContext
    {
        LeaderAccount = mockAccounts[0],
        Instrument    = mockInstr,
        AllAccounts   = mockAccounts
    };

    EventHandler<BeEventArgs> handler = (s, e) => { };
    PttBus.BeFired += handler;

    try
    {
        // Act
        sut.Execute(ctx);

        // Assert: SubmitBeStopLocal called once per account (DW-B36-01)
        Assert.Equal(accountCount, submitBeCallCount);
    }
    finally
    {
        PttBus.BeFired -= handler;
    }
}
```

### 7-Scan Checklist

```powershell
# SCAN-01: lock() banned
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "lock\s*\("
# Expected: zero matches

# SCAN-02: async void banned
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "async\s+void"
# Expected: zero matches

# SCAN-03: init accessor banned (NT8-001)
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero matches

# SCAN-04: CreateOrder args — verify arg6=0, arg7=bePrice, arg11=(CustomOrder)null
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "\.CreateOrder"
# Expected: 1 match in SubmitBeStopLocal. Manually verify:
#   arg6 = 0              (limitPrice — NT8-049)
#   arg7 = bePrice        (stopPrice  — NT8-049)
#   arg11 = (NinjaTrader.Cbi.CustomOrder)null   (NT8-007)

# SCAN-05: no dead-code references in new file
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: zero matches

# SCAN-06: Positions[Instrument] banned (NT8-050)
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "\.Positions\["
# Expected: zero matches

# SCAN-07: PttBus subscription cleanup in T_B33_BE_Standalone and T_B33_AllAccounts_BeLoop
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "PttBus\.BeFired\s*\+="
# Expected: each += has a matching -= in the same test method's finally block
```

### Build Verification
```powershell
# Namespace consistency check
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "^namespace"
# Expected: namespace PropTraderTools
```

---

## TICKET T3 — Features/PttTrim.cs (NEW FILE)

### Spec Requirements Satisfied
- B33-03: PttTrim module — partial close (50% of position) on leader account
- B33-03: TrimPositionLocal() — acc.CreateOrder for market close of trimQty
- B33-03: Fires TrimFired via PttBus.RaiseTrim

### File
```
CREATE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs
```

### Dependency Rule
Depends on T1 only. No dependency on T2, T4, T5, T6, T7, T8.

### JS / NT8 Rule Constraints
- JS-021: No lock() | JS-033: No async void | JS-001: No throw
- NT8-007: arg11 = (CustomOrder)null | NT8-013: DateTime.MaxValue | NT8-014: "PTT-Trim"
- NT8-044: using System; required for Math.Max | NT8-050: FindPositionLocal via foreach

### Complete Implementation

```csharp
// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs
// B33 — Trim (partial close) module. 50% of leader position, market order.
// Imports: Core/PttContracts.cs + NinjaTrader.Cbi ONLY.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Trim module: partial close (50%) on leader account, fire TrimFired.
    /// Called from TradeCopierPanel.OnTrimClick (UI thread).
    /// </summary>
    public class PttTrim : IPttModule
    {
        public string ModuleId  { get; private set; }
        public bool   IsEnabled { get; private set; }

        public PttTrim()
        {
            ModuleId  = "TRIM";
            IsEnabled = true;
        }

        public void Initialize(IPttHostContext ctx) { }
        public void Teardown() { }

        public void SetEnabled(bool enabled) { IsEnabled = enabled; }

        /// <summary>
        /// Partial close: trim 50% of leader position.
        /// CYC = 3: IsEnabled guard + null/qty guard + TrimPositionLocal call.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled) return;

            Position pos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
            if (pos == null || pos.Quantity == 0) return;

            int trimQty = Math.Max(1, pos.Quantity / 2);
            TrimPositionLocal(ctx.LeaderAccount, ctx.Instrument, trimQty,
                              pos.MarketPosition == MarketPosition.Long);

            PttBus.RaiseTrim(this, new TrimEventArgs(ctx.Instrument, 50, trimQty));
        }

        // ── private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Submit a Market order to close trimQty shares/contracts on the leader account.
        /// NT8-007: arg11 = (CustomOrder)null.
        /// NT8-013: DateTime.MaxValue.
        /// NT8-014: "PTT-Trim".
        /// NT8-049: arg6=0 (limit), arg7=0 (stop) for market orders.
        /// CYC = 2: null guard + single CreateOrder.
        /// </summary>
        private static void TrimPositionLocal(Account acc, Instrument instr,
                                              int qty, bool isLong)
        {
            if (acc == null || instr == null || qty <= 0) return;

            OrderAction direction = isLong ? OrderAction.Sell : OrderAction.Buy;

            acc.CreateOrder(
                instr,
                direction,
                OrderType.Market,
                OrderEntry.Manual,
                TimeInForce.Day,
                qty,
                0,                                   // arg6: limitPrice = 0 (market order)
                0,                                   // arg7: stopPrice  = 0 (market order)
                string.Empty,
                "PTT-Trim",                          // NT8-014: starts with "PTT-"
                DateTime.MaxValue,                   // NT8-013
                (NinjaTrader.Cbi.CustomOrder)null);  // NT8-007
        }

        /// <summary>CYC = 2: foreach + single if. NT8-050 compliant.</summary>
        private static Position FindPositionLocal(Account acc, Instrument instr)
        {
            foreach (Position p in acc.Positions)
                if (p.Instrument == instr)
                    return p;
            return null;
        }
    }
}
```

### xUnit [Fact] Tests

```csharp
[Fact]
public void T_B33_Trim_Standalone()
{
    int firedCount = 0;
    EventHandler<TrimEventArgs> handler = (s, e) => firedCount++;
    PttBus.TrimFired += handler;

    var sut       = new PttTrim();
    var mockInstr = BuildMockInstrument();
    var mockAcc   = BuildMockAccountWithLongPosition(mockInstr, qty: 4, avgPrice: 18500.0);
    var ctx       = new MockPttHostContext
    {
        LeaderAccount = mockAcc,
        Instrument    = mockInstr,
        AllAccounts   = new List<Account> { mockAcc }
    };
    sut.Initialize(ctx);

    try
    {
        sut.Execute(ctx);
        // TrimFired raised exactly once
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.TrimFired -= handler; }
}
```

### 7-Scan Checklist

```powershell
# SCAN-01
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs" -Pattern "lock\s*\("
# Expected: zero

# SCAN-02
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs" -Pattern "async\s+void"
# Expected: zero

# SCAN-03
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero

# SCAN-04: CreateOrder args
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs" -Pattern "\.CreateOrder"
# Expected: 1 match. Manually verify arg6=0, arg7=0, signal="PTT-Trim", arg11=(CustomOrder)null

# SCAN-05: dead code references
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: zero

# SCAN-06
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttTrim.cs" -Pattern "\.Positions\["
# Expected: zero

# SCAN-07: TrimFired subscription cleanup in T_B33_Trim_Standalone
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "PttBus\.TrimFired\s*\+="
# Expected: each += has matching -= in finally block
```

---

## TICKET T4 — Features/PttFlatten.cs (NEW FILE)

### Spec Requirements Satisfied
- B33-04: PttFlatten module — full close of leader position
- B33-04: FlattenPositionLocal() — acc.CreateOrder for full close
- B33-04: Fires FlatFired via PttBus.RaiseFlatted

### File
```
CREATE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs
```

### Dependency Rule
Depends on T1 only.

### JS / NT8 Rule Constraints
Same as T3. Signal name: "PTT-Flatten".

### Complete Implementation

```csharp
// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs
// B33 — Flatten (full close) module. Full close of leader position, market order.
// Imports: Core/PttContracts.cs + NinjaTrader.Cbi ONLY.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Flatten module: full close on leader account, fire FlatFired.
    /// Called from TradeCopierPanel.OnFlattenClick (UI thread).
    /// </summary>
    public class PttFlatten : IPttModule
    {
        public string ModuleId  { get; private set; }
        public bool   IsEnabled { get; private set; }

        public PttFlatten()
        {
            ModuleId  = "FLAT";
            IsEnabled = true;
        }

        public void Initialize(IPttHostContext ctx) { }
        public void Teardown() { }

        public void SetEnabled(bool enabled) { IsEnabled = enabled; }

        /// <summary>
        /// Full close of leader position.
        /// CYC = 2: IsEnabled guard + null/qty guard.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled) return;

            Position pos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
            if (pos == null || pos.Quantity == 0) return;

            FlattenPositionLocal(ctx.LeaderAccount, ctx.Instrument, pos.Quantity,
                                 pos.MarketPosition == MarketPosition.Long);

            PttBus.RaiseFlatted(this, new FlatEventArgs(ctx.Instrument));
        }

        // ── private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Submit a Market order for the full position qty.
        /// NT8-007, NT8-013, NT8-014, NT8-049 compliant.
        /// CYC = 2: null/qty guard + CreateOrder.
        /// </summary>
        private static void FlattenPositionLocal(Account acc, Instrument instr,
                                                 int qty, bool isLong)
        {
            if (acc == null || instr == null || qty <= 0) return;

            OrderAction direction = isLong ? OrderAction.Sell : OrderAction.Buy;

            acc.CreateOrder(
                instr,
                direction,
                OrderType.Market,
                OrderEntry.Manual,
                TimeInForce.Day,
                qty,
                0,                                   // arg6: limitPrice = 0
                0,                                   // arg7: stopPrice  = 0
                string.Empty,
                "PTT-Flatten",                       // NT8-014
                DateTime.MaxValue,                   // NT8-013
                (NinjaTrader.Cbi.CustomOrder)null);  // NT8-007
        }

        /// <summary>CYC = 2. NT8-050 compliant.</summary>
        private static Position FindPositionLocal(Account acc, Instrument instr)
        {
            foreach (Position p in acc.Positions)
                if (p.Instrument == instr)
                    return p;
            return null;
        }
    }
}
```

### xUnit [Fact] Tests

```csharp
[Fact]
public void T_B33_Flatten_Standalone()
{
    int firedCount = 0;
    EventHandler<FlatEventArgs> handler = (s, e) => firedCount++;
    PttBus.FlatFired += handler;

    var sut       = new PttFlatten();
    var mockInstr = BuildMockInstrument();
    var mockAcc   = BuildMockAccountWithLongPosition(mockInstr, qty: 2, avgPrice: 18500.0);
    var ctx       = new MockPttHostContext
    {
        LeaderAccount = mockAcc,
        Instrument    = mockInstr,
        AllAccounts   = new List<Account> { mockAcc }
    };
    sut.Initialize(ctx);

    try
    {
        sut.Execute(ctx);
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.FlatFired -= handler; }
}
```

### 7-Scan Checklist

```powershell
# SCAN-01
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs" -Pattern "lock\s*\("
# Expected: zero

# SCAN-02
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs" -Pattern "async\s+void"
# Expected: zero

# SCAN-03
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero

# SCAN-04
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs" -Pattern "\.CreateOrder"
# Expected: 1 match. Verify arg6=0, arg7=0, signal="PTT-Flatten", arg11=(CustomOrder)null

# SCAN-05
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: zero

# SCAN-06
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs" -Pattern "\.Positions\["
# Expected: zero

# SCAN-07
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "PttBus\.FlatFired\s*\+="
# Expected: each += has matching -= in finally block
```

---

## TICKET T5 — Features/PttCancel.cs (NEW FILE)

### Spec Requirements Satisfied
- B33-05: PttCancel module — cancel all working entry orders for leader + instrument
- B33-05: CancelWorkingEntriesLocal() — foreach working orders → acc.Cancel
- B33-05: Fires CancelFired via PttBus.RaiseCancel

### File
```
CREATE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs
```

### Dependency Rule
Depends on T1 only.

### JS / NT8 Rule Constraints
- JS-021: No lock() | JS-033: No async void
- NT8-006: explicit foreach + List accumulator — no .Where()/.Any()
- NT8-031: Working + Initialized only — no PendingSubmit
- NT8-044: using System; required

### Complete Implementation

```csharp
// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs
// B33 — Cancel working entries module.
// Imports: Core/PttContracts.cs + NinjaTrader.Cbi ONLY.

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Cancel module: cancel all Working/Initialized entry orders for leader+instrument, fire CancelFired.
    /// Called from TradeCopierPanel.OnCancelClick (UI thread).
    /// </summary>
    public class PttCancel : IPttModule
    {
        public string ModuleId  { get; private set; }
        public bool   IsEnabled { get; private set; }

        public PttCancel()
        {
            ModuleId  = "CANCEL";
            IsEnabled = true;
        }

        public void Initialize(IPttHostContext ctx) { }
        public void Teardown() { }

        public void SetEnabled(bool enabled) { IsEnabled = enabled; }

        /// <summary>
        /// Cancel all working orders for leader account + instrument.
        /// CYC = 2: IsEnabled guard + delegate to CancelWorkingEntriesLocal.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled) return;

            CancelWorkingEntriesLocal(ctx.LeaderAccount, ctx.Instrument);

            PttBus.RaiseCancel(this, new CancelEventArgs(ctx.Instrument));
        }

        // ── private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Collect and cancel all Working/Initialized orders for account+instrument.
        /// NT8-006: explicit foreach + List — no .Where() or .Any().
        /// NT8-031: Working + Initialized only (PendingSubmit does not exist in NT8).
        /// CYC = 3: null guard + foreach loop + state/instr conditions.
        /// </summary>
        private static void CancelWorkingEntriesLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null) return;

            var toCancel = new List<Order>();
            foreach (Order o in acc.Orders)
            {
                bool stateOk = o.OrderState == OrderState.Working
                            || o.OrderState == OrderState.Initialized;
                bool instrOk = o.Instrument != null
                            && o.Instrument.FullName == instr.FullName;
                if (stateOk && instrOk)
                    toCancel.Add(o);
            }
            if (toCancel.Count == 0) return;
            try { acc.Cancel(toCancel.ToArray()); } catch { /* ignore — orders may have filled */ }
        }
    }
}
```

### xUnit [Fact] Tests

```csharp
[Fact]
public void T_B33_Cancel_Standalone()
{
    int firedCount = 0;
    EventHandler<CancelEventArgs> handler = (s, e) => firedCount++;
    PttBus.CancelFired += handler;

    var sut       = new PttCancel();
    var mockInstr = BuildMockInstrument();
    var mockAcc   = BuildMockAccountWithWorkingOrders(mockInstr, orderCount: 2);
    var ctx       = new MockPttHostContext
    {
        LeaderAccount = mockAcc,
        Instrument    = mockInstr,
        AllAccounts   = new List<Account> { mockAcc }
    };
    sut.Initialize(ctx);

    try
    {
        sut.Execute(ctx);
        // CancelFired raised exactly once regardless of order count
        Assert.Equal(1, firedCount);
    }
    finally { PttBus.CancelFired -= handler; }
}
```

### 7-Scan Checklist

```powershell
# SCAN-01
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs" -Pattern "lock\s*\("
# Expected: zero

# SCAN-02
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs" -Pattern "async\s+void"
# Expected: zero

# SCAN-03
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero

# SCAN-04: no CreateOrder in this file
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs" -Pattern "\.CreateOrder"
# Expected: zero (Cancel module does not submit new orders)

# SCAN-05
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: zero

# SCAN-06
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCancel.cs" -Pattern "\.Positions\["
# Expected: zero

# SCAN-07
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "PttBus\.CancelFired\s*\+="
# Expected: each += has matching -= in finally block
```

---

## TICKET T6 — Features/PttCopier.cs (NEW FILE)

### Spec Requirements Satisfied
- B33-06: PttCopier module — subscribes all 4 PttBus events, fans out to CopyEngine via ICopyEngine
- B33-06: Initialize() subscribes BeFired, TrimFired, FlatFired, CancelFired
- B33-06: Teardown() unsubscribes all 4 events — direct -= per NT8-043
- B33-06: 4 handler methods each calling _engine.Relay*() methods
- T6-TEST-01 FIX: PttCopier constructor accepts ICopyEngine (not CopyEngine) — enables MockCopyEngineRelay injection

### Files
```
CREATE: C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs
MODIFY: C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
        (add RelayBe, RelayTrim, RelayFlatten, RelayCancel — see T8 for full context;
         these 4 methods are listed here since PttCopier calls them.
         The physical edit to CopyEngine.cs is performed in T8 to avoid touching the file twice.)
```

### Dependency Rule
Depends on T1 (contracts) and T8 (relay methods in CopyEngine). T8 must add
RelayBe/RelayTrim/RelayFlatten/RelayCancel before T6 code can compile end-to-end.
In practice: implement PttCopier.cs (T6) and CopyEngine relay methods (T8) in the same
engineer session — they are in the same changeset.

### JS / NT8 Rule Constraints
- JS-021: No lock() — subscribe/unsubscribe on UI thread only
- JS-033: No async void
- NT8-043: Teardown() uses direct `-=`, NOT null-conditional `?.Event -= handler` (CS8370 in C# 7.3)
- FORBIDDEN EDGE: PttCopier imports CopyEngine — this is the ONLY feature file permitted to do so

### Complete Implementation

```csharp
// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs
// B33 — Copier fan-out module.
// Imports: Core/PttContracts.cs (which declares ICopyEngine) + NinjaTrader.Cbi.
// This is the ONLY Features file permitted to import CopyEngine (via ICopyEngine).
// T6-TEST-01 FIX: constructor accepts ICopyEngine — CopyEngine implements ICopyEngine (T8);
// test injects MockCopyEngineRelay : ICopyEngine without subclassing CopyEngine.
// Other Features files (PttBreakEven, PttTrim, PttFlatten, PttCancel) MUST NOT import CopyEngine.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Copier module: subscribes to all PttBus events and fans out to CopyEngine follower accounts.
    /// Initialize() subscribes; Teardown() unsubscribes — both on UI thread.
    /// NT8-043: Teardown uses direct -= (no null-conditional ?. — CS8370 in C# 7.3).
    /// T6-TEST-01 FIX: constructor takes ICopyEngine so MockCopyEngineRelay can be injected in tests.
    /// </summary>
    public class PttCopier : IPttModule
    {
        public string ModuleId  { get; private set; }
        public bool   IsEnabled { get; private set; }

        private readonly ICopyEngine _engine;

        public PttCopier(ICopyEngine engine)
        {
            ModuleId  = "COPY";
            IsEnabled = true;
            _engine   = engine;
        }

        public void SetEnabled(bool enabled) { IsEnabled = enabled; }

        /// <summary>
        /// Subscribe to all 4 PttBus events. CYC = 1.
        /// All subscriptions on UI thread — JS-021 compliant (no lock needed).
        /// </summary>
        public void Initialize(IPttHostContext ctx)
        {
            PttBus.BeFired     += OnBeFired;
            PttBus.TrimFired   += OnTrimFired;
            PttBus.FlatFired   += OnFlatFired;
            PttBus.CancelFired += OnCancelFired;
        }

        /// <summary>
        /// Unsubscribe all 4 PttBus events. CYC = 1.
        /// NT8-043: direct -= (NOT null-conditional ?.Event -= which is C# 9, CS8370 in NT8).
        /// </summary>
        public void Teardown()
        {
            PttBus.BeFired     -= OnBeFired;
            PttBus.TrimFired   -= OnTrimFired;
            PttBus.FlatFired   -= OnFlatFired;
            PttBus.CancelFired -= OnCancelFired;
        }

        // ── Event handlers (CYC = 1 each — null guard + single relay call) ──────

        private void OnBeFired    (object sender, BeEventArgs e)
        {
            if (!IsEnabled || e == null) return;
            _engine.RelayBe(e);
        }

        private void OnTrimFired  (object sender, TrimEventArgs e)
        {
            if (!IsEnabled || e == null) return;
            _engine.RelayTrim(e);
        }

        private void OnFlatFired  (object sender, FlatEventArgs e)
        {
            if (!IsEnabled || e == null) return;
            _engine.RelayFlatten(e);
        }

        private void OnCancelFired(object sender, CancelEventArgs e)
        {
            if (!IsEnabled || e == null) return;
            _engine.RelayCancel(e);
        }
    }
}
```

### CopyEngine Relay Method Contracts (implemented in T8)
The following 4 methods must be added to `CopyEngine.cs` (thin wrappers around existing private helpers):

```csharp
// Signatures only — full bodies in T8

/// <summary>Fan out BE stop to all follower accounts. Called by PttCopier. CYC ≤ 2.</summary>
public void RelayBe(BeEventArgs e)

/// <summary>Fan out trim to all follower accounts. Called by PttCopier. CYC ≤ 2.</summary>
public void RelayTrim(TrimEventArgs e)

/// <summary>Fan out flatten to all follower accounts. Called by PttCopier. CYC ≤ 2.</summary>
public void RelayFlatten(FlatEventArgs e)

/// <summary>Fan out cancel entries to all follower accounts. Called by PttCopier. CYC ≤ 2.</summary>
public void RelayCancel(CancelEventArgs e)
```

### xUnit [Fact] Tests

```csharp
[Fact]
public void T_B33_Copier_BeFanOut()
{
    // Arrange: mock CopyEngine proxy that records RelayBe calls
    bool relayBeCalled = false;
    var mockEngine = new MockCopyEngineRelay(onRelayBe: (e) => { relayBeCalled = true; });

    var copier = new PttCopier(mockEngine);
    copier.Initialize(new MockPttHostContext());

    try
    {
        // Act: raise BeFired directly (simulates PttBreakEven.Execute raising BeFired)
        var mockInstr = BuildMockInstrument();
        PttBus.RaiseBe(this, new BeEventArgs(
            mockInstr, 18500.0, 18500.0, true, string.Empty));

        // Assert: PttCopier.OnBeFired relayed to CopyEngine.RelayBe
        Assert.True(relayBeCalled);
    }
    finally
    {
        // Teardown unsubscribes — confirms NT8-043 direct -= works
        copier.Teardown();
    }
}
```

### MockCopyEngineRelay test helper required:
```csharp
/// <summary>
/// Minimal test double implementing ICopyEngine — records which relay methods are called.
/// T6-TEST-01 FIX: implements ICopyEngine (declared in PttContracts.cs T1) so it can be
/// passed to PttCopier(ICopyEngine engine) without subclassing the concrete CopyEngine class.
/// Place inline in CopyEngineTests.cs (same file as T_B33_Copier_BeFanOut test).
/// NT8: no LINQ, no async, no init accessors — plain class implementing interface.
/// </summary>
internal class MockCopyEngineRelay : ICopyEngine
{
    private readonly Action<BeEventArgs>     _onRelayBe;
    private readonly Action<TrimEventArgs>   _onRelayTrim;
    private readonly Action<FlatEventArgs>   _onRelayFlatten;
    private readonly Action<CancelEventArgs> _onRelayCancel;

    public MockCopyEngineRelay(
        Action<BeEventArgs>     onRelayBe      = null,
        Action<TrimEventArgs>   onRelayTrim    = null,
        Action<FlatEventArgs>   onRelayFlatten = null,
        Action<CancelEventArgs> onRelayCancel  = null)
    {
        _onRelayBe      = onRelayBe      ?? (_ => { });
        _onRelayTrim    = onRelayTrim    ?? (_ => { });
        _onRelayFlatten = onRelayFlatten ?? (_ => { });
        _onRelayCancel  = onRelayCancel  ?? (_ => { });
    }

    public void RelayBe     (BeEventArgs e)     { _onRelayBe(e); }
    public void RelayTrim   (TrimEventArgs e)   { _onRelayTrim(e); }
    public void RelayFlatten(FlatEventArgs e)   { _onRelayFlatten(e); }
    public void RelayCancel (CancelEventArgs e) { _onRelayCancel(e); }
}
```

### 7-Scan Checklist

```powershell
# SCAN-01
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs" -Pattern "lock\s*\("
# Expected: zero

# SCAN-02
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs" -Pattern "async\s+void"
# Expected: zero

# SCAN-03
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero

# SCAN-04: no CreateOrder in PttCopier.cs
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs" -Pattern "\.CreateOrder"
# Expected: zero (PttCopier only calls engine relay methods, not CreateOrder directly)

# SCAN-05
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: zero

# SCAN-06
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttCopier.cs" -Pattern "\.Positions\["
# Expected: zero

# SCAN-07
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "PttBus\.(BeFired|TrimFired|FlatFired|CancelFired)\s*\+="
# For T_B33_Copier_BeFanOut: verify copier.Teardown() in finally (unsubscribes all 4)
# Expected: no raw += without corresponding copier.Teardown() or explicit -=
```

---

## TICKET T7 — TradeCopierPanel.cs (MODIFY EXISTING)

### Spec Requirements Satisfied
- B33-07: TradeCopierPanel implements IPttHostContext
- B33-07: _allAccounts field + AllAccounts property
- B33-07: _modules field + AddModule() helper
- B33-07: 5 license bools (IsBeLicensed, IsTrimLicensed, IsFlattenLicensed, IsCancelLicensed, IsCopierLicensed)
- B33-07: Module registration and initialization in panel Attach/Initialize handler
- B33-07: Module Teardown wired in panel Detach/Close handler
- B33-07: OnBeClick, OnTrimClick, OnFlattenClick, OnCancelClick updated to module dispatch
- CRITICAL: OnBeClick Armed/Idle FSM preserved — only the actual execution call changes

### File
```
MODIFY: C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
```

### Dependency Rule
Depends on T1 (IPttModule, IPttHostContext), T2 (PttBreakEven), T3 (PttTrim),
T4 (PttFlatten), T5 (PttCancel), T6 (PttCopier). All previous tickets must be in
place before T7 can compile. T8 is independent of T7 (T8 is dead code removal only).

### JS / NT8 Rule Constraints
- JS-021: No lock() — module list only mutated on UI thread
- NT8-017: _modules field does not need volatile — only accessed on UI thread
- NT8-021: Account.All accessed in Initialize handler, NOT constructor
- NT8-042: No Dispatcher.InvokeAsync — all panel operations on UI thread already
- NT8-043: No null-conditional -= in module Teardown delegation

### Critical Constraint — OnBeClick Armed/Idle FSM MUST Be Preserved

The existing OnBeClick in TradeCopierPanel.cs has a 2-state FSM (BeState.Idle / BeState.Armed):

```
Idle path:
  (a) if price already at BE → fire _engine.BreakEven() immediately   ← B33 changes THIS only
  (b) else → call _engine.ArmPendingBe() to arm the pending-BE watcher ← UNCHANGED

Armed path:
  → call _engine.DisarmPendingBe()                                     ← UNCHANGED
```

B33 changes ONLY case (a): replace `_engine.BreakEven(leader, _instrument, _beBuffer)` with
module dispatch. Case (b) [ArmPendingBe] and the Armed path [DisarmPendingBe] are UNTOUCHED.

### Changes to Implement

**1. Class declaration — add IPttHostContext:**
```csharp
// BEFORE:
public class TradeCopierPanel : UserControl

// AFTER:
public class TradeCopierPanel : UserControl, IPttHostContext
```

**2. New fields (add near existing _leaderAccount and _instrument fields):**
```csharp
// IPttHostContext backing fields
private List<Account>         _allAccounts = new List<Account>();
private List<IPttModule>      _modules     = new List<IPttModule>();
```

**3. IPttHostContext property implementations (add to panel):**
```csharp
// IPttHostContext — LeaderAccount and Instrument already exist as _leaderAccount / _instrument
// Add only AllAccounts property:
public Account  LeaderAccount { get { return _leaderAccount; } }  // may already exist as property
public Instrument Instrument  { get { return _instrument;    } }  // may already exist as property
public IReadOnlyList<Account> AllAccounts { get { return _allAccounts; } }
```
NOTE: If `_leaderAccount` and `_instrument` are already exposed as public properties with
different names, rename the interface implementation accordingly or add explicit interface
implementation (`Account IPttHostContext.LeaderAccount => _leaderAccount;`).

**4. AddModule helper:**
```csharp
private void AddModule(IPttModule m)
{
    _modules.Add(m);
}
```

**5. License bool properties (add to panel — NT8 property grid visible):**
```csharp
[NinjaScriptProperty]
[Display(Name = "BE Licensed", Order = 201, GroupName = "PTT Licenses")]
public bool IsBeLicensed     { get; set; }

[NinjaScriptProperty]
[Display(Name = "Trim Licensed", Order = 202, GroupName = "PTT Licenses")]
public bool IsTrimLicensed   { get; set; }

[NinjaScriptProperty]
[Display(Name = "Flatten Licensed", Order = 203, GroupName = "PTT Licenses")]
public bool IsFlattenLicensed { get; set; }

[NinjaScriptProperty]
[Display(Name = "Cancel Licensed", Order = 204, GroupName = "PTT Licenses")]
public bool IsCancelLicensed  { get; set; }

[NinjaScriptProperty]
[Display(Name = "Copier Licensed", Order = 205, GroupName = "PTT Licenses")]
public bool IsCopierLicensed  { get; set; }
```
Default all to `true` in panel constructor or `SetDefaults` equivalent:
```csharp
IsBeLicensed = IsTrimLicensed = IsFlattenLicensed = IsCancelLicensed = IsCopierLicensed = true;
```

**6. Module initialization (add inside existing panel Attach/Initialize handler, AFTER _leaderAccount and Account.All are populated):**
```csharp
// Populate AllAccounts (NT8-021: Account.All safe here — inside event handler, not constructor)
_allAccounts.Clear();
if (_leaderAccount != null)
    _allAccounts.Add(_leaderAccount);
foreach (Account acc in Account.All)
{
    if (acc != _leaderAccount && IsFollowerAccount(acc))  // IsFollowerAccount = existing panel helper
        _allAccounts.Add(acc);
}

// Register modules
AddModule(new PttBreakEven());
AddModule(new PttTrim());
AddModule(new PttFlatten());
AddModule(new PttCancel());
AddModule(new PttCopier(_engine));   // _engine = existing CopyEngine reference in panel

// Wire license bools to modules
foreach (IPttModule m in _modules)
{
    switch (m.ModuleId)
    {
        case "BE":     ((PttBreakEven)m).SetEnabled(IsBeLicensed);      break;
        case "TRIM":   ((PttTrim)m).SetEnabled(IsTrimLicensed);         break;
        case "FLAT":   ((PttFlatten)m).SetEnabled(IsFlattenLicensed);   break;
        case "CANCEL": ((PttCancel)m).SetEnabled(IsCancelLicensed);     break;
        case "COPY":   ((PttCopier)m).SetEnabled(IsCopierLicensed);     break;
    }
}

// Initialize all modules (subscribe PttBus events)
foreach (IPttModule m in _modules)
    m.Initialize(this);   // "this" implements IPttHostContext
```

**7. Module teardown (add inside existing panel Detach/Close/Teardown handler):**
```csharp
foreach (IPttModule m in _modules)
    m.Teardown();
_modules.Clear();
_allAccounts.Clear();
```

**8. OnBeClick — preserve FSM, replace execution call only:**

The Armed/Idle FSM must remain intact. Only the Idle-immediate-fire path changes.

```csharp
// BEFORE (B35 Idle path — immediate fire):
// _engine.BreakEven(_leaderAccount, _instrument, _beBuffer);

// AFTER (B33 Idle path — module dispatch):
// The outer FSM structure (Armed/Idle check, ArmPendingBe path) is UNCHANGED.
// Replace ONLY the _engine.BreakEven() call with:
foreach (IPttModule m in _modules)
{
    if (m.ModuleId == "BE" && m.IsEnabled)
        m.Execute(this);
}
```

Full OnBeClick structure after change (illustrative — engineer must read actual source first):
```csharp
private void OnBeClick(object sender, RoutedEventArgs e)
{
    // STATE MACHINE — PRESERVED EXACTLY as B35
    if (_beState == BeState.Armed)
    {
        // Armed → Idle: disarm the pending-BE watcher
        _engine.DisarmPendingBe(_leaderAccount, _instrument);  // UNCHANGED
        _beState = BeState.Idle;
        UpdateBeButtonState();
        return;
    }

    // Idle path:
    // Check if immediate fire (price at entry) or arm for later
    // (pre-existing condition — UNCHANGED)
    if (ShouldFireImmediately())   // existing panel helper — UNCHANGED
    {
        // B33 CHANGE: replace _engine.BreakEven(...) with module dispatch
        foreach (IPttModule m in _modules)
        {
            if (m.ModuleId == "BE" && m.IsEnabled)
                m.Execute(this);
        }
    }
    else
    {
        // Arm path — UNCHANGED
        _engine.ArmPendingBe(_leaderAccount, _instrument, _beBuffer);  // UNCHANGED
        _beState = BeState.Armed;
        UpdateBeButtonState();
    }
}
```

**9. OnTrimClick, OnFlattenClick, OnCancelClick — module dispatch:**
```csharp
// Replace existing _engine.TrimOneAccount / _engine.FlattenOneAccount / _engine.CancelPendingEntries
// with module dispatch pattern (same pattern as OnBeClick module dispatch above):

private void OnTrimClick(object sender, RoutedEventArgs e)
{
    foreach (IPttModule m in _modules)
        if (m.ModuleId == "TRIM" && m.IsEnabled)
            m.Execute(this);
}

private void OnFlattenClick(object sender, RoutedEventArgs e)
{
    foreach (IPttModule m in _modules)
        if (m.ModuleId == "FLAT" && m.IsEnabled)
            m.Execute(this);
}

private void OnCancelClick(object sender, RoutedEventArgs e)
{
    foreach (IPttModule m in _modules)
        if (m.ModuleId == "CANCEL" && m.IsEnabled)
            m.Execute(this);
}
```

### xUnit Tests
No new [Fact] tests for T7 alone. The module integration is exercised via T2–T6 standalone
tests. T7 is a wiring change — verified by F5 compile and manual smoke test in NT8 sim.

### 7-Scan Checklist

```powershell
# SCAN-01: lock() banned — verify no lock introduced in T7 changes
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "lock\s*\("
# Expected: zero matches in new B33 additions (existing pre-B33 lock() = pre-existing violation, report to Director)

# SCAN-02: async void
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "async\s+void"
# Expected: zero new async void in B33 additions

# SCAN-03: init accessor
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero in B33 additions

# SCAN-04: CreateOrder (no new CreateOrder in T7 — panel delegates to modules)
# If any CreateOrder appears in T7 additions, it is an error — modules handle this, not the panel

# SCAN-05: dead code references
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: zero (these references should be removed by T8; TradeCopierPanel should not reference them)

# SCAN-06: Positions[Instrument]
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "\.Positions\["
# Expected: zero in B33 additions

# SCAN-07: PttBus event subscriptions in TradeCopierPanel (should be none — modules manage their own)
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "PttBus\."
# Expected: zero — panel does not subscribe PttBus directly; that is PttCopier's role
```

### Build Verification
```powershell
# Verify Armed/Idle FSM path — ArmPendingBe must still be referenced after B33
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "ArmPendingBe"
# Expected: exactly 1 match (the arm path in OnBeClick — preserved per critical constraint)

Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "DisarmPendingBe"
# Expected: exactly 1 match (the Armed→Idle path in OnBeClick — preserved per critical constraint)
```

---

## TICKET T8 — CopyEngine.cs (MODIFY EXISTING — DEAD CODE REMOVAL + RELAY METHODS)

### Spec Requirements Satisfied
- B33-08: Delete _trailBeSlots field (line ~136)
- B33-08: Delete _trailBeLastPnlBits field (line ~138)
- B33-08: Delete ArmTrailBe() method (line ~1930)
- B33-08: Delete DisarmTrailBe() method (line ~1953)
- B33-08: Delete OnTrailBeAccountUpdate() method (line ~1974)
- B33-08: Update build tag to "PTT-COPIER B33 | modular-independence | 2026-07-{DATE}"
- B33-06 (relay): Add RelayBe, RelayTrim, RelayFlatten, RelayCancel public methods
- T6-TEST-01 FIX: CopyEngine class declaration adds `: ICopyEngine` (implements the interface declared in T1)
- T8-NT8-01 FIX: Relay methods use AllAccounts(Instrument) inline — NO GetFollowerAccounts() helper, NO Enumerable.Empty, NO System.Linq

### File
```
MODIFY: C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

### Dependency Rule
T8 depends on T6 (PttCopier calls relay methods; relay method signatures must match T6's call sites).
T8 does NOT depend on T7. T8 can be implemented in the same session as T6.

### PRE-DELETION MANDATORY — Run BEFORE Any Deletion

Engineer MUST run all 3 pre-deletion grep commands and verify zero callers outside the
method definitions themselves. If any caller is found, STOP and report to Director:

```powershell
# Pre-deletion check 1: field callers
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "_trailBeSlots|_trailBeLastPnlBits"
# Expected: ONLY the field declaration lines (136, 138).
# If any other line references these fields: STOP — report to Director before proceeding.

# Pre-deletion check 2: method callers
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate"
# Expected: ONLY the method definition lines (~1930, ~1953, ~1974).
# If any other line calls these methods: STOP — report to Director before proceeding.

# Pre-deletion check 3: verify no other file references them
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\" -Pattern "ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate|_trailBeSlots|_trailBeLastPnlBits" -Include "*.cs" -Recurse
# Expected: ONLY lines within CopyEngine.cs (no TradeCopierPanel.cs, no test file references).
```

### Deletions

```
DELETE: CopyEngine.cs line ~136 — entire field declaration _trailBeSlots
DELETE: CopyEngine.cs line ~138 — entire field declaration _trailBeLastPnlBits
DELETE: CopyEngine.cs line ~1930 through end of ArmTrailBe method (including closing brace)
DELETE: CopyEngine.cs line ~1953 through end of DisarmTrailBe method (including closing brace)
DELETE: CopyEngine.cs line ~1974 through end of OnTrailBeAccountUpdate method (including closing brace)
```

### Build Tag Change

```
File: CopyEngine.cs line 41
BEFORE: Tag = "PTT-COPIER B35 | bracket-cancel + BE-fixes | 2026-07-23";
AFTER:  Tag = "PTT-COPIER B33 | modular-independence | 2026-07-{DATE}";
```
Replace `{DATE}` with the actual commit date (e.g. `2026-07-25`).

### CopyEngine : ICopyEngine — Class Declaration Change (T6-TEST-01 FIX)

```csharp
// File: CopyEngine.cs — class declaration (line ~89 in existing file)
// BEFORE:
//   public class CopyEngine
// AFTER:
//   public class CopyEngine : ICopyEngine
//
// ICopyEngine is declared in Core/PttContracts.cs (T1). Same namespace (PropTraderTools).
// No new using directives needed — flat compilation, same namespace.
// CopyEngine already has all 4 relay methods as public void — satisfies interface automatically.
```

### Relay Methods to Add

Add the following 4 public methods to `CopyEngine.cs`. Insert after the last existing public
method in the file (or group with BreakEven overloads at line ~1730):

**T8-NT8-01 FIX:** Relay methods use the **existing private `AllAccounts(Instrument)` method
at L1321** for fan-out iteration. This is the exact same pattern used by `Trim(Instrument)` at L881
(`foreach (var acc in AllAccounts(instrument)) TrimOneAccount(acc, instrument)`). No
`GetFollowerAccounts()` helper. No `Enumerable.Empty`. No `System.Linq`. No new using directives.

The existing `AllAccounts(Instrument)` at L1321:
- Calls `FindRule(instrument)` → iterates `_rules` ConcurrentBag
- Yields `rule.Value.MasterAccount` then each non-null `rule.Value.FollowerAccounts` entry
- Returns nothing (yield break) if no rule found — safe null case

```csharp
// ── B33 Module Relay Methods ──────────────────────────────────────────────
// Called by PttCopier to fan out module events to follower accounts.
// These are thin wrappers around existing private helpers.
// Fan-out iteration: AllAccounts(Instrument) at L1321 — same as Trim(Instrument) at L881.
// NO new using directives. NO Enumerable.Empty. NO System.Linq.
// CYC = 2 each (null guard + foreach).

/// <summary>
/// Fan out BE stop to all master+follower accounts for the instrument.
/// Iteration: AllAccounts(e.Instrument) — existing private method at L1321.
///   yields rule.Value.MasterAccount then rule.Value.FollowerAccounts entries.
/// Delegates to existing SubmitBeStop(Account, Instrument, double) at L1575.
/// Called by PttCopier.OnBeFired on UI thread.
/// CYC = 2: null guard + foreach.
/// NT8: no new using directives — AllAccounts, SubmitBeStop are in same class, same namespace.
/// </summary>
public void RelayBe(BeEventArgs e)
{
    if (e == null) return;
    foreach (var acc in AllAccounts(e.Instrument))
        SubmitBeStop(acc, e.Instrument, e.BePrice);
}

/// <summary>
/// Fan out trim to all master+follower accounts for the instrument.
/// Iteration: AllAccounts(e.Instrument) — existing private method at L1321.
/// Delegates to existing TrimOneAccount(Account, Instrument) private method at L992.
/// CYC = 2: null guard + foreach.
/// </summary>
public void RelayTrim(TrimEventArgs e)
{
    if (e == null) return;
    foreach (var acc in AllAccounts(e.Instrument))
        TrimOneAccount(acc, e.Instrument);
}

/// <summary>
/// Fan out flatten to all master+follower accounts for the instrument.
/// Iteration: AllAccounts(e.Instrument) — existing private method at L1321.
/// Delegates to existing FlattenOneAccount(Account, Instrument) private method at L1040.
/// CYC = 2: null guard + foreach.
/// </summary>
public void RelayFlatten(FlatEventArgs e)
{
    if (e == null) return;
    foreach (var acc in AllAccounts(e.Instrument))
        FlattenOneAccount(acc, e.Instrument);
}

/// <summary>
/// Fan out cancel entries to all master+follower accounts for the instrument.
/// Iteration: AllAccounts(e.Instrument) — existing private method at L1321.
/// Delegates to existing CancelOneAccount(Account, Instrument) private method at L1120.
/// (CancelOneAccount is the per-account helper; CancelPendingEntries(Account,Instrument)
///  at L937 iterates AllAccounts again internally — use CancelOneAccount to avoid double loop.)
/// CYC = 2: null guard + foreach.
/// </summary>
public void RelayCancel(CancelEventArgs e)
{
    if (e == null) return;
    foreach (var acc in AllAccounts(e.Instrument))
        CancelOneAccount(acc, e.Instrument);
}
```

### xUnit Tests
No new [Fact] tests for T8 dead code removal. The relay methods are tested via
`T_B33_Copier_BeFanOut` in T6. Dead code removal is verified by SCAN-05.

Pre-flight test protection check (run before deleting methods):
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\" -Pattern "ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate" -Include "*.cs" -Recurse
# Expected: zero matches (confirms no test file references them)
```

### 7-Scan Checklist

```powershell
# SCAN-01
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\("
# Expected: zero new lock() in relay methods (pre-existing lock() in other methods = pre-existing; report any in relay methods)

# SCAN-02
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "async\s+void"
# Expected: zero new async void in relay methods

# SCAN-03
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "\{\s*get;\s*init;\s*\}"
# Expected: zero in relay methods

# SCAN-04: CreateOrder in relay methods — relay methods delegate to existing helpers, not CreateOrder directly
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "\.CreateOrder"
# For any CreateOrder in relay methods (unexpected): verify arg6=0 limitPrice, arg7=stopPrice, arg11=(CustomOrder)null

# SCAN-05: DEAD CODE REMOVAL VERIFICATION — zero after deletion
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate" -Include "*.cs" -Recurse
# Expected: ZERO matches across all .cs files in PropTraderTools

# SCAN-06
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "\.Positions\["
# Expected: zero in new relay methods (relay methods do not read positions directly)

# SCAN-07: no PttBus references in CopyEngine.cs
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "PttBus\."
# Expected: zero — CopyEngine does not subscribe to or raise PttBus events
```

### Build Verification
```powershell
# Build tag updated
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-COPIER B33"
# Expected: 1 match at line 41

# Relay methods present
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "public void Relay(Be|Trim|Flatten|Cancel)"
# Expected: 4 matches
```

---

## POST-ALL-TICKETS VERIFICATION PROTOCOL

Run after T1–T8 are all implemented:

### Full-Tree SCAN-05 (dead code zero)
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\" -Pattern "_trailBeSlots|_trailBeLastPnlBits|ArmTrailBe|DisarmTrailBe|OnTrailBeAccountUpdate" -Include "*.cs" -Recurse
# Expected: ZERO matches across entire PropTraderTools tree
```

### Full-Tree SCAN-01 (no new lock in B33 files)
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Core\" -Pattern "lock\s*\(" -Include "*.cs" -Recurse
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\" -Pattern "lock\s*\(" -Include "*.cs" -Recurse
# Expected: zero in Core/ and Features/
```

### Test Count Verification
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\" -Pattern "\[Fact\]" -Include "*.cs" -Recurse | Measure-Object
# Expected: >= 170 (164 baseline + 6 new)
```

### Hard-Link Sync (MANDATORY after all file changes)
```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

### F5 Gate
F5 compile in NinjaTrader must pass zero errors, zero warnings.
Do NOT merge to main until F5 is green.

---

## ACCEPTANCE CRITERIA TRACEABILITY

| AC | Requirement | Ticket |
|----|-------------|--------|
| AC-1 | F5 compile clean — zero errors, zero warnings | All (7-scan per ticket) |
| AC-2 | 164 existing [Fact] pass — no regressions | T8 pre-flight (dead code no-caller verify) |
| AC-3a | T_B33_BE_Standalone | T2 |
| AC-3b | T_B33_Trim_Standalone | T3 |
| AC-3c | T_B33_Flatten_Standalone | T4 |
| AC-3d | T_B33_Cancel_Standalone | T5 |
| AC-3e | T_B33_Copier_BeFanOut | T6 |
| AC-3f | T_B33_AllAccounts_BeLoop | T2 |
| AC-3g | Total [Fact] >= 170 (164 + 6) | Post-all verification |
| AC-4 | Build tag "PTT-COPIER B33 \| modular-independence" | T8 |
| AC-5 | Hard-link sync via verify_links.ps1 -Fix | Post-all verification |

---

*Return: TICKETS_COMPLETE*
*ptt-architect | B33-Modular | Phase 3 — Ticket Generation*
