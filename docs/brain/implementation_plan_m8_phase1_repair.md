# Implementation Plan — M8 Phase 1 Structural Repair
**Document:** `docs/brain/implementation_plan_m8_phase1_repair.md`
**Mission:** M8 Phase 1 — SIMA Dispatch Layer Structural Repair
**Role:** ARCHITECT (P3) → ENGINEER (Codex)
**Status:** CONFIRMED — All 3 blocking defects + 5 secondary findings validated
**Date:** 2026-04-30
**Based on:** PR #65 Forensic Audit + P1 Orchestrator Validation

---

## 0. Executive Summary

PR #65 introduced 5 new SIMA dispatch component files. All 5 files carry three
**build-breaking defects** that will cause `CS0246` (type not found) and silent
logic failure at runtime. This plan provides exact, copy-paste-ready code for
Codex to apply in a single atomic commit against branch
`codex/implement-phase-1-interface-foundation`.

**Files in scope:**
```
src/V12_002.SIMA.Interfaces.Phase1.cs
src/V12_002.SIMA.Gate.cs
src/V12_002.SIMA.FleetResolver.cs
src/V12_002.SIMA.MembraneResolver.cs
src/V12_002.SIMA.AccountIndexResolver.cs
```

**Execution order is strict. Do not reorder steps.**

---

## 1. Defect Register

| ID | Severity | File(s) | Defect | Impact |
|----|----------|---------|--------|--------|
| D-01 | BLOCKING | All 5 | Namespace `UniversalORStrategy.SIMA.Dispatch` | CS0246 — types invisible to strategy compilation unit |
| D-02 | BLOCKING | Gate.cs | `ScheduleDeferredDispatch` drops command silently | Missed trades, no error thrown |
| D-03 | BLOCKING | All 5 | Missing `using NinjaTrader.Cbi/NinjaScript` | CS0246 — `AccountRankInfo`, `OrderAction`, `OrderType` unresolved |
| D-04 | MEDIUM | AccountIndexResolver.cs | No null guard on `print` delegate | NullReferenceException on logging path |
| D-05 | MEDIUM | Gate.cs + Interfaces | Duplicate signature format string | Format strings can diverge, causing MetadataGuard mismatches |
| D-06 | MEDIUM | FleetResolver.cs | Nested ternary clamp expression | Readability / style guide violation ("Metabolic Elegance") |
| D-07 | LOW | Gate.cs | 6 × unused captured locals (CS0219) | Compiler warning, dead code |
| D-08 | LOW | All 5 | `string.Format(...)` usage | Style guide requires interpolated strings |

---

## 2. Step-by-Step Repair Instructions

### STEP 1 — Namespace Sweep (D-01)

**Action:** In all 5 files, perform a find-and-replace on the namespace declaration.

```
FIND:    namespace UniversalORStrategy.SIMA.Dispatch
REPLACE: namespace NinjaTrader.NinjaScript.Strategies
```

**Applies to:** All 5 files listed in scope.

**Rationale:** NinjaTrader's compiler resolves strategy-layer types — including
partials, inner classes, and resolver helpers — only within
`NinjaTrader.NinjaScript.Strategies`. A foreign namespace causes `CS0246` on
every cross-reference from the main strategy class.

---

### STEP 2 — Using Directive Injection (D-03)

Add the following `using` directives to the files indicated. Do not remove
existing directives.

#### `src/V12_002.SIMA.Interfaces.Phase1.cs`
```csharp
using System.Collections.Generic;
using NinjaTrader.Cbi;         // AccountRankInfo
using NinjaTrader.NinjaScript; // OrderAction, OrderType
```

#### `src/V12_002.SIMA.Gate.cs`
```csharp
using System;
using System.Threading;
using System.Runtime.CompilerServices;
using NinjaTrader.Cbi;         // AccountRankInfo (transitive consistency)
using NinjaTrader.NinjaScript; // OrderAction, OrderType
```

#### `src/V12_002.SIMA.FleetResolver.cs`
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;         // AccountRankInfo
```

#### `src/V12_002.SIMA.AccountIndexResolver.cs`
```csharp
using System;
using System.Runtime.CompilerServices;
using NinjaTrader.Cbi;         // AccountRankInfo (transitive consistency)
```

#### `src/V12_002.SIMA.MembraneResolver.cs`
```csharp
using System;
using System.Threading;
using NinjaTrader.Cbi;         // AccountRankInfo (transitive consistency)
```

---

### STEP 3 — DispatchGate Full Repair (D-02, D-05, D-07, D-08)

**Replace the entire contents of `src/V12_002.SIMA.Gate.cs` with the
following:**

```csharp
// ============================================================
// FILE: src/V12_002.SIMA.Gate.cs
// NAMESPACE: NinjaTrader.NinjaScript.Strategies
// REPAIRS: D-01, D-02, D-03, D-05, D-07, D-08
// ============================================================
using System;
using System.Threading;
using System.Runtime.CompilerServices;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Concrete IDispatchGate. Owns the non-blocking semaphore protocol
    /// (Build 1109 freeze-proof) and MetadataGuard integration.
    ///
    /// NO-LOCKS  : SemaphoreSlim.Wait(0) is non-blocking.
    /// ZERO-ALLOC: No heap allocation in Evaluate() on the non-contended path.
    ///             string allocation in BuildSignature() is a cold pre-loop call.
    /// </summary>
    internal sealed class DispatchGate : IDispatchGate
    {
        // ── Injected dependencies ────────────────────────────────────────────
        private readonly SemaphoreSlim                   _sem;
        private readonly Func<bool>                      _getSimaEnabled;
        private readonly Func<bool>                      _getFlattenRunning;
        private readonly Func<string, string, bool>      _metadataGuard;
        private readonly Action<string>                  _print;

        /// <summary>
        /// Delegate that wraps TriggerCustomEvent on the strategy instance.
        /// The caller (orchestrator / strategy wiring layer) provides:
        ///
        ///   cmd => TriggerCustomEvent(
        ///              (o, ea) => orchestrator.Dispatch(
        ///                  cmd.TradeType, cmd.Action, cmd.Quantity,
        ///                  cmd.EntryPrice, cmd.EntryOrderType,
        ///                  cmd.MasterEntryNames),
        ///              null)
        ///
        /// This keeps DispatchGate free of NinjaScriptBase coupling.
        /// See: Section 4 — Strategy Wiring for the complete construction site.
        /// </summary>
        private readonly Action<DispatchCommand>         _schedulerCallback;

        // Tracks whether THIS gate instance acquired the semaphore so that
        // ReleaseSemaphore() can guard against double-release.
        // Volatile: ReleaseSemaphore() may be called from a finally block after
        // an exception unwind on the same thread.
        private volatile int _acquired; // 0 = not held, 1 = held

        // ── Constructor ─────────────────────────────────────────────────────
        public DispatchGate(
            SemaphoreSlim                   sem,
            Func<bool>                      getSimaEnabled,
            Func<bool>                      getFlattenRunning,
            Func<string, string, bool>      metadataGuard,
            Action<string>                  print,
            Action<DispatchCommand>         schedulerCallback)   // ← D-02 FIX: new param
        {
            _sem               = sem               ?? throw new ArgumentNullException(nameof(sem));
            _getSimaEnabled    = getSimaEnabled    ?? throw new ArgumentNullException(nameof(getSimaEnabled));
            _getFlattenRunning = getFlattenRunning ?? throw new ArgumentNullException(nameof(getFlattenRunning));
            _metadataGuard     = metadataGuard     ?? throw new ArgumentNullException(nameof(metadataGuard));
            _print             = print             ?? throw new ArgumentNullException(nameof(print));
            _schedulerCallback = schedulerCallback ?? throw new ArgumentNullException(nameof(schedulerCallback));
        }

        // ── IDispatchGate.Evaluate ───────────────────────────────────────────
        /// <inheritdoc/>
        public GateContext Evaluate(
            string      tradeType,
            OrderAction action,
            int         quantity,
            double      entryPrice)
        {
            // Step 1: Non-blocking semaphore attempt.
            // Wait(0) returns instantly. If contended, caller schedules defer.
            bool acquired = _sem.Wait(0);
            Volatile.Write(ref _acquired, acquired ? 1 : 0);

            if (!acquired)
            {
                _print("[DISPATCH] Semaphore contended -- deferring dispatch (non-blocking)");
                return new GateContext(false, false, false, false, string.Empty);
            }

            // Step 2: SIMA enabled check.
            bool simaEnabled = _getSimaEnabled();
            if (!simaEnabled)
            {
                _print("[DISPATCH] [ERR] SIMA DISABLED - Enable in strategy parameters to copy trade");
                return new GateContext(false, false, true, false, string.Empty);
            }

            // Step 3: Flatten guard [H-12].
            bool flattenRunning = _getFlattenRunning();
            if (flattenRunning)
            {
                _print("[DISPATCH] (!) Aborting dispatch -- flatten in progress (isFlattenRunning=true)");
                return new GateContext(true, true, true, false, string.Empty);
            }

            // Step 4: MetadataGuard duplicate check [MG-D1].
            // D-05 FIX: Delegate to DispatchCommand.BuildSignature() — single
            // source of truth. Gate.Evaluate() and DispatchCommand can never
            // diverge because there is now only one format string in the system.
            var sigCommand = new DispatchCommand
            {
                TradeType        = tradeType,
                Action           = action,
                Quantity         = quantity,
                EntryPrice       = entryPrice,
                EntryOrderType   = default,
                MasterEntryNames = null
            };
            string sig = sigCommand.BuildSignature(); // ← D-05 FIX

            bool isDuplicate = !_metadataGuard(sig, "SmartDispatch");
            if (isDuplicate)
                _print("[DISPATCH] (!) Duplicate dispatch rejected by MetadataGuard");

            return new GateContext(simaEnabled, flattenRunning, true, isDuplicate, sig);
        }

        // ── IDispatchGate.ReleaseSemaphore ───────────────────────────────────
        /// <inheritdoc/>
        public void ReleaseSemaphore()
        {
            // Guard against double-release via atomic swap.
            if (Interlocked.CompareExchange(ref _acquired, 0, 1) == 1)
                _sem.Release();
        }

        // ── IDispatchGate.ScheduleDeferredDispatch ───────────────────────────
        /// <inheritdoc/>
        /// <remarks>
        /// D-02 FIX: Copies DispatchCommand by value (avoids ref-to-struct
        /// lambda capture issue), then fires _schedulerCallback which wraps
        /// TriggerCustomEvent on the strategy instance. One allocation per
        /// deferred retry — pre-existing behaviour from original code; accepted.
        /// </remarks>
        public void ScheduleDeferredDispatch(in DispatchCommand command)
        {
            // Value-copy the struct BEFORE closure capture.
            // D-07 FIX: Removed 6 unused field-capture locals. Single struct
            // copy is sufficient and avoids CS0219.
            DispatchCommand captured = command;

            _print("[DISPATCH] Deferred retry scheduled via TriggerCustomEvent delegate");

            // D-02 FIX: Actually invoke the scheduler — no longer a dead stub.
            _schedulerCallback(captured);
        }
    }
}
```

---

### STEP 4 — AccountIndexResolver Repair (D-01, D-03, D-04, D-08)

**Replace the entire contents of `src/V12_002.SIMA.AccountIndexResolver.cs`
with the following:**

```csharp
// ============================================================
// FILE: src/V12_002.SIMA.AccountIndexResolver.cs
// NAMESPACE: NinjaTrader.NinjaScript.Strategies
// REPAIRS: D-01, D-03, D-04, D-08
// ============================================================
using System;
using System.Runtime.CompilerServices;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Concrete IAccountIndexResolver. Reads AccountIndexByName from the
    /// membrane snapshot (frozen — no mutation after FreezeStamp is set).
    ///
    /// NO-LOCKS     : Dictionary is read-only after membrane freeze.
    /// HOT-PATH SAFE: AggressiveInlining — called once per fleet account.
    /// </summary>
    internal sealed class AccountIndexResolver : IAccountIndexResolver
    {
        private readonly Action<string> _print;

        public AccountIndexResolver(Action<string> print)
        {
            // D-04 FIX: null guard added — mirrors DispatchGate, FleetResolver,
            // MembraneResolver. Prevents NullReferenceException on logging paths.
            _print = print ?? throw new ArgumentNullException(nameof(print));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResolve(
            FlattenedSubstrateState membrane,
            string                  accountName,
            out int                 accountIndex)
        {
            accountIndex = -1;

            if (membrane.AccountIndexByName == null)
            {
                // D-08 FIX: interpolated string
                _print($"[DISPATCH] Membrane AccountIndexByName null for {accountName} -- skipped");
                return false;
            }

            if (!membrane.AccountIndexByName.TryGetValue(accountName, out accountIndex))
            {
                // D-08 FIX: interpolated string
                _print($"[DISPATCH] Membrane index missing for {accountName} -- skipped");
                return false;
            }

            if (accountIndex < 0
                || membrane.AccountByIndex == null
                || accountIndex >= membrane.AccountByIndex.Length)
            {
                // D-08 FIX: interpolated string
                _print($"[DISPATCH] Membrane index out of range for {accountName} index={accountIndex} -- skipped");
                accountIndex = -1;
                return false;
            }

            return true;
        }
    }
}
```

---

### STEP 5 — FleetResolver Repair (D-01, D-03, D-06, D-08)

**Replace the entire contents of `src/V12_002.SIMA.FleetResolver.cs` with the
following:**

```csharp
// ============================================================
// FILE: src/V12_002.SIMA.FleetResolver.cs
// NAMESPACE: NinjaTrader.NinjaScript.Strategies
// REPAIRS: D-01, D-03, D-06, D-08
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Cbi;  // AccountRankInfo

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Concrete IFleetResolver. Takes a single consistent snapshot of
    /// activeFleetAccounts and activeTargetCount before the dispatch loop,
    /// eliminating the UI/IPC mid-loop mutation races documented in
    /// [Q3-002] and FIX-B [Build 1102Z].
    ///
    /// NO-LOCKS  : Reads from ConcurrentDictionary via LINQ snapshot.
    ///             activeTargetCount read via Volatile.Read (acquire semantics).
    /// ZERO-ALLOC: HashSet and List allocated once per dispatch (cold setup).
    ///             Hot loop reads IReadOnlyCollection.Contains() — O(1), no alloc.
    /// </summary>
    internal sealed class FleetResolver : IFleetResolver
    {
        private readonly Func<IEnumerable<KeyValuePair<string, bool>>> _getActiveFleet;
        private readonly Func<IReadOnlyList<AccountRankInfo>>          _getSortedFleet;
        private readonly Func<int>                                     _getActiveTargetCount;
        private readonly Action<string>                                _print;

        private const int MinTargetCount = 1;
        private const int MaxTargetCount = 5;

        public FleetResolver(
            Func<IEnumerable<KeyValuePair<string, bool>>> getActiveFleet,
            Func<IReadOnlyList<AccountRankInfo>>          getSortedFleet,
            Func<int>                                     getActiveTargetCount,
            Action<string>                                print)
        {
            _getActiveFleet      = getActiveFleet      ?? throw new ArgumentNullException(nameof(getActiveFleet));
            _getSortedFleet      = getSortedFleet      ?? throw new ArgumentNullException(nameof(getSortedFleet));
            _getActiveTargetCount = getActiveTargetCount ?? throw new ArgumentNullException(nameof(getActiveTargetCount));
            _print               = print               ?? throw new ArgumentNullException(nameof(print));
        }

        /// <inheritdoc/>
        public FleetContext Resolve()
        {
            // Snapshot activeFleetAccounts — single read, no lock.
            var activeNames = new HashSet<string>(
                _getActiveFleet()
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key));

            // D-06 FIX: Replace nested ternary with idiomatic Math.Max/Min clamp.
            // Style guide: "Metabolic Elegance" — avoid dense one-liners.
            int rawCount    = _getActiveTargetCount();
            int targetCount = Math.Max(MinTargetCount, Math.Min(MaxTargetCount, rawCount));

            IReadOnlyList<AccountRankInfo> fleet = _getSortedFleet();
            var ctx = new FleetContext(fleet, activeNames, targetCount);

            // D-08 FIX: interpolated string
            _print($"[DISPATCH] Fleet: {fleet.Count} total accounts | {ctx.ActiveCount} ACTIVE in Fleet Manager");

            if (fleet.Count == 0)
                _print("[DISPATCH] [ERR] NO APEX ACCOUNTS DETECTED - Check AccountPrefix setting");

            if (ctx.HasFleetButNoActive())
                _print("[DISPATCH] [ERR] NO ACCOUNTS ENABLED - Toggle accounts ON in Fleet Manager panel");

            return ctx;
        }
    }
}
```

---

### STEP 6 — Interfaces Phase1 Repair (D-01, D-03, D-05, D-08)

**Replace the entire contents of `src/V12_002.SIMA.Interfaces.Phase1.cs` with
the following:**

```csharp
// ============================================================
// FILE: src/V12_002.SIMA.Interfaces.Phase1.cs
// NAMESPACE: NinjaTrader.NinjaScript.Strategies
// REPAIRS: D-01, D-03, D-05, D-08
// ============================================================
using System.Collections.Generic;
using NinjaTrader.Cbi;         // AccountRankInfo
using NinjaTrader.NinjaScript; // OrderAction, OrderType

namespace NinjaTrader.NinjaScript.Strategies
{
    // ── Value Types ──────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshot of gate evaluation result. Immutable after construction.
    /// IsOpen() is the canonical check — do not read individual flags directly
    /// in the dispatch loop.
    /// </summary>
    public readonly struct GateContext
    {
        public bool   SimaEnabled        { get; }
        public bool   FlattenRunning     { get; }
        public bool   SemaphoreAcquired  { get; }
        public bool   IsDuplicate        { get; }
        public string DispatchSignature  { get; }

        public GateContext(
            bool   simaEnabled,
            bool   flattenRunning,
            bool   semaphoreAcquired,
            bool   isDuplicate,
            string dispatchSignature)
        {
            SimaEnabled       = simaEnabled;
            FlattenRunning    = flattenRunning;
            SemaphoreAcquired = semaphoreAcquired;
            IsDuplicate       = isDuplicate;
            DispatchSignature = dispatchSignature;
        }

        /// <summary>
        /// Returns true only when all preconditions pass and this is not a
        /// duplicate. This is the canonical gate check for the dispatch loop.
        /// </summary>
        public bool IsOpen() =>
            SemaphoreAcquired && SimaEnabled && !FlattenRunning && !IsDuplicate;
    }

    /// <summary>
    /// Zero-alloc carrier for dispatch parameters. Passed by value (in keyword)
    /// through the hot path. BuildSignature() is the SINGLE source of truth for
    /// MetadataGuard signature format — D-05 FIX.
    /// </summary>
    public struct DispatchCommand
    {
        public string      TradeType;
        public OrderAction Action;
        public int         Quantity;
        public double      EntryPrice;
        public OrderType   EntryOrderType;
        public string[]    MasterEntryNames;

        /// <summary>
        /// D-05 FIX: Centralised signature builder. Gate.Evaluate() calls this
        /// instead of inlining its own format string, guaranteeing the two can
        /// never diverge and cause MetadataGuard mismatches.
        /// </summary>
        public string BuildSignature()
            // D-08 FIX: interpolated string
            => $"SD_{TradeType}_{Action}_{Quantity}_{EntryPrice:F2}";
    }

    /// <summary>
    /// Immutable snapshot of fleet state for one dispatch cycle.
    /// Constructed once by FleetResolver.Resolve() before the loop starts.
    /// </summary>
    public readonly struct FleetContext
    {
        public IReadOnlyList<AccountRankInfo>       Fleet               { get; }
        public IReadOnlyCollection<string>          ActiveAccountNames  { get; }
        public int                                  DispatchTargetCount { get; }
        public int                                  ActiveCount         { get; }

        public FleetContext(
            IReadOnlyList<AccountRankInfo>      fleet,
            IReadOnlyCollection<string>         activeAccountNames,
            int                                 dispatchTargetCount)
        {
            Fleet               = fleet;
            ActiveAccountNames  = activeAccountNames;
            DispatchTargetCount = dispatchTargetCount;
            ActiveCount         = activeAccountNames?.Count ?? 0;
        }

        /// <summary>True when the gate should allow dispatch to proceed.</summary>
        public bool IsDispatchable() => Fleet != null && Fleet.Count > 0 && ActiveCount > 0;

        /// <summary>True when fleet exists but no accounts are enabled.</summary>
        public bool HasFleetButNoActive() => Fleet != null && Fleet.Count > 0 && ActiveCount == 0;
    }

    // ── Interfaces ───────────────────────────────────────────────────────────

    /// <summary>
    /// Owns semaphore acquisition, SIMA/flatten guard evaluation, and MetadataGuard
    /// duplicate detection. Controls deferred retry scheduling via an injected
    /// TriggerCustomEvent delegate (see DispatchGate constructor).
    /// </summary>
    public interface IDispatchGate
    {
        GateContext Evaluate(string tradeType, OrderAction action, int quantity, double entryPrice);
        void        ReleaseSemaphore();
        void        ScheduleDeferredDispatch(in DispatchCommand command);
    }

    /// <summary>
    /// Owns Volatile.Read of the management membrane and freeze-trigger logic.
    /// </summary>
    public interface IMembraneResolver
    {
        bool TryResolve(out FlattenedSubstrateState membrane);
        bool IsAccountIndexValid(FlattenedSubstrateState membrane, int accountIndex);
    }

    /// <summary>
    /// Produces an immutable FleetContext snapshot before the dispatch loop.
    /// </summary>
    public interface IFleetResolver
    {
        FleetContext Resolve();
    }

    /// <summary>
    /// Maps account names to validated membrane indices.
    /// </summary>
    public interface IAccountIndexResolver
    {
        bool TryResolve(FlattenedSubstrateState membrane, string accountName, out int accountIndex);
    }
}
```

---

### STEP 7 — MembraneResolver Namespace + Directive Repair (D-01, D-03, D-08)

Only the header requires changes. Replace the first 5 lines of
`src/V12_002.SIMA.MembraneResolver.cs` with:

```csharp
// ============================================================
// FILE: src/V12_002.SIMA.MembraneResolver.cs
// NAMESPACE: NinjaTrader.NinjaScript.Strategies
// REPAIRS: D-01, D-03, D-08
// ============================================================
using System;
using System.Threading;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    // ... existing MembraneResolver class body unchanged ...
    // Apply D-08 fix: replace any remaining string.Format(...) calls
    // with interpolated $"..." equivalents inside the class body.
}
```

---

## 3. Strategy Wiring — Construction Site Update

**This is not a source-file change — it is a wiring update inside the existing
strategy class (or `DispatchOrchestrator` if extracted). Provide this block to
the ENGINEER so the new `schedulerCallback` parameter is correctly wired.**

```csharp
// ============================================================
// LOCATION: Strategy class (NinjaScriptBase subclass) or
//           DispatchOrchestrator wiring method.
// PURPOSE : Wire the DispatchGate with the TriggerCustomEvent delegate.
// ============================================================

// OLD construction (replace this):
// _gate = new DispatchGate(_sem, () => SimaEnabled, () => isFlattenRunning,
//                          MetadataGuard, Print);

// NEW construction (use this):
_gate = new DispatchGate(
    _dispatchSemaphore,
    getSimaEnabled:    () => SimaEnabled,
    getFlattenRunning: () => isFlattenRunning,
    metadataGuard:     MetadataGuard,
    print:             Print,
    // schedulerCallback wraps TriggerCustomEvent — the ONLY place in the
    // codebase where NinjaScriptBase coupling is permitted.
    schedulerCallback: cmd => TriggerCustomEvent(
        (o, ea) => _orchestrator.Dispatch(
            cmd.TradeType,
            cmd.Action,
            cmd.Quantity,
            cmd.EntryPrice,
            cmd.EntryOrderType,
            cmd.MasterEntryNames),
        null)
);
```

---

## 4. Verification Checklist (Post-Repair)

Codex must confirm each item before marking the PR ready for merge.

```
[ ] STEP 1  — All 5 files: namespace = NinjaTrader.NinjaScript.Strategies
[ ] STEP 2  — All 5 files: using NinjaTrader.Cbi; present where required
[ ] STEP 2  — Gate.cs, Interfaces.Phase1.cs: using NinjaTrader.NinjaScript; present
[ ] STEP 3  — Gate.cs: _schedulerCallback field declared and null-guarded
[ ] STEP 3  — Gate.cs: ScheduleDeferredDispatch calls _schedulerCallback(captured)
[ ] STEP 3  — Gate.cs: 6 unused captured locals removed
[ ] STEP 3  — Gate.cs: sig derived from sigCommand.BuildSignature(), not inline Format
[ ] STEP 4  — AccountIndexResolver.cs: _print null-guarded with ArgumentNullException
[ ] STEP 4  — AccountIndexResolver.cs: all string.Format → $"..." interpolation
[ ] STEP 5  — FleetResolver.cs: nested ternary → Math.Max(Min, Math.Min(Max, raw))
[ ] STEP 5  — FleetResolver.cs: string.Format → $"..." interpolation
[ ] STEP 6  — Interfaces.Phase1.cs: DispatchCommand.BuildSignature() is sole format source
[ ] STEP 7  — MembraneResolver.cs: namespace updated, string.Format → $"..."
[ ] WIRING  — Strategy construction site updated with schedulerCallback parameter
[ ] BUILD   — dotnet build: 0 errors, 0 CS0219 warnings
[ ] TESTS   — dotnet test: all existing tests pass
```

---

## 5. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `TriggerCustomEvent` called on wrong thread | Low | HIGH | schedulerCallback is fired from strategy thread via semaphore; no cross-thread issue |
| `FlattenedSubstrateState` not found after namespace change | Low | MEDIUM | Confirm it is defined in same namespace; add explicit using if in sub-namespace |
| Construction site not updated → ArgumentNullException at startup | Medium | HIGH | Verified by STEP 3 wiring block; caught immediately on first strategy load |
| Existing tests bind to old namespace string | Low | LOW | Test assemblies reference types directly; namespace change is transparent via `using` |

---

## 6. Commit Message Template

```
fix(m8-phase1): structural repair — namespace, ScheduleDeferredDispatch, directives

D-01: Sweep all 5 SIMA files to NinjaTrader.NinjaScript.Strategies namespace
D-02: Inject Action<DispatchCommand> schedulerCallback into DispatchGate;
      ScheduleDeferredDispatch now fires TriggerCustomEvent via delegate
D-03: Add using NinjaTrader.Cbi and NinjaTrader.NinjaScript to all affected files
D-04: Null-guard _print in AccountIndexResolver constructor
D-05: Centralise MetadataGuard signature in DispatchCommand.BuildSignature()
D-06: Replace nested ternary clamp with Math.Max/Math.Min in FleetResolver
D-07: Remove 6 unused captured locals from ScheduleDeferredDispatch
D-08: Convert all string.Format calls to interpolated strings

Verified: dotnet build (0 errors), dotnet test (all pass)
Closes: PR #65 forensic audit findings
```

---

*Document produced by ARCHITECT (P3) — 2026-04-30*
*Validated against PR #65 diff and P1 Orchestrator Forensic Validation*
*Ready for handoff to Codex (ENGINEER)*
