# B58-LaneA Architecture Plan
## PTT-COPIER B58 — Copy-Engine Missing Members

**Phase**: 1 — Architecture (Revision 1 — cycle 1 of 2)
**Mode**: ptt-architect
**Date**: 2026-08-10
**Status**: REVIEW_PENDING
**Previous status**: REVIEW_FAIL (V1: relay methods absent; V2: SCAN-06/07 missing)

---

## A. Epic Summary

B58 restores 13 missing members (spanning 15 individual code changes) to `CopyEngine.cs` that were
present in stash WIP blob d085d7bf but were never committed to main. The root cause is a prior
stash-vs-commit divergence across blocks B39, B40, B50, and B54 — plus the discovery that
`ICopyEngine`'s 4 relay method bodies were never implemented in `CopyEngine.cs`.

All 13 items target a **single file** (`CopyEngine.cs`). This is a single-ticket epic: T1 adds all
missing members in one surgical pass.

Missing items confirmed by grepping the live file:
- `CopyEngine` does NOT implement `: ICopyEngine` on the class declaration (line 91)
- `RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel` method bodies are **absent** — adding
  `: ICopyEngine` without them would produce 4 CS0535 errors (confirmed: grep returns 0 matches)
- `IsEnabled` property is absent (B54-LaneA ticket shows it should exist after `SetEnabled`)
- `_globalBe` field and `GlobalBe` property are absent (B39-LaneA ticket)
- `IsPendingSlotsEmpty()` is absent (B40-LaneA ticket)
- `_cloneAtmCache` field, `SetCloneAtmCache`, `GetCloneAtmMode`, `ResolveAtmMode` are absent (B50-LaneA ticket)
- `DispatchCopy` still calls `GetAtmMode(rule, acc.Name)` at line 699 — must become `ResolveAtmMode`
- `FindPositionPublic` wrapper is absent
- `SnapshotTargetsPublic` wrapper is absent
- `CopyRulesContainer.CopyEnabled` property is absent (B54-LaneA ticket)
- `SaveRules` does not write `container.CopyEnabled` (B54-LaneA ticket)
- `LoadRules` does not restore `_isCopyEnabled` or fire `CopyEnabledChanged` (B54-LaneA ticket)

---

## B. File Inventory

### Files MODIFIED (1)

| File | Reason |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | All 13 items reside here. Zero changes to any other file. |

### Files READ (reference only — no changes)

| File | Read For |
|------|----------|
| `src/PropTraderTools/Core/PttContracts.cs` | ICopyEngine interface definition (line 79) — exact signatures for 4 relay methods |
| `src/PropTraderTools/TradeCopierPanel.cs` | Confirms callers of `FindPositionPublic`, `SnapshotTargetsPublic`, `IsEnabled`, `GlobalBe` |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | Confirms `PttGlobalBreakEven` type that `_globalBe` field holds |
| `src/PropTraderTools/Features/PttCopier.cs` | Confirms `PttCopier(ICopyEngine)` constructor injection and how relay methods are called |

---

## C. The 13 Missing Members — Detailed Plan

### Item 1 — Add `: ICopyEngine` to CopyEngine class declaration

**Type**: Class declaration amendment
**Source**: `src/PropTraderTools/Core/PttContracts.cs` line 79 (ICopyEngine interface)
**Location in CopyEngine.cs**: Line 91

**Current** (line 91):
```csharp
internal sealed class CopyEngine
```

**Replacement**:
```csharp
internal sealed class CopyEngine : ICopyEngine
```

**Rationale**: The `: ICopyEngine` token is required to satisfy the `PttCopier(ICopyEngine engine)`
constructor injection call site. Alone this change generates 4 CS0535 errors (one per unimplemented
relay method). Items 1a–1d below add the 4 required public method bodies, resolving all CS0535
errors in the same T1 pass.

**Source brain artifact**: `PttContracts.cs` ICopyEngine declaration + CS1503 error analysis.

---

### Item 1a — `RelayBe(BeEventArgs e)` method

**Type**: `public void` ICopyEngine implementation
**Source**: `PttContracts.cs` line 82; called by `PttCopier.OnBeFired` (line 72)
**Insertion point**: After closing `}` of `GetCopyMode()` — grouped with Items 1b–1d as the
"ICopyEngine implementation block"

**Code to insert**:
```csharp
        // B58 ICopyEngine -- RelayBe: fan out pre-calculated BE price to all follower accounts.
        // BeEventArgs.BePrice is already computed by PttGlobalBreakEven/BE module before firing.
        // CYC=2 (1 base + 1 foreach branch). JS-021: no lock -- AllAccounts snapshot; SubmitBeStop lock-free.
        // JS-002: void method, no return null.
        public void RelayBe(BeEventArgs e)
        {
            foreach (var acc in AllAccounts(e.Instrument))
                SubmitBeStop(acc, e.Instrument, e.BePrice);
        }
```

**CYC**: 2 (1 base + 1 foreach)
**JS compliance**: JS-021 — no lock; AllAccounts returns snapshot, SubmitBeStop is lock-free.
JS-002 — void, no null return. ASCII-only.

---

### Item 1b — `RelayTrim(TrimEventArgs e)` method

**Type**: `public void` ICopyEngine implementation
**Source**: `PttContracts.cs` line 85; called by `PttCopier.OnTrimFired` (line 77)
**Insertion point**: After `RelayBe` (Item 1a above)

**Code to insert**:
```csharp
        // B58 ICopyEngine -- RelayTrim: delegate to Trim(Instrument) fan-out. CYC=1.
        // Trim(Instrument) at line 1006 iterates AllAccounts and calls TrimOneAccount per account.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayTrim(TrimEventArgs e) => Trim(e.Instrument);
```

**CYC**: 1 (expression body, no branch)
**JS compliance**: JS-021 — no lock. JS-002 — void, no null return. Delegates to existing `Trim(Instrument)`.

---

### Item 1c — `RelayFlatten(FlatEventArgs e)` method

**Type**: `public void` ICopyEngine implementation
**Source**: `PttContracts.cs` line 88; called by `PttCopier.OnFlatFired` (line 82)
**Insertion point**: After `RelayTrim` (Item 1b above)

**Code to insert**:
```csharp
        // B58 ICopyEngine -- RelayFlatten: delegate to Flatten(Instrument) fan-out. CYC=1.
        // Flatten(Instrument) at line 1012 iterates AllAccounts and calls FlattenOneAccount per account.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayFlatten(FlatEventArgs e) => Flatten(e.Instrument);
```

**CYC**: 1 (expression body, no branch)
**JS compliance**: JS-021 — no lock. JS-002 — void, no null return. Delegates to existing `Flatten(Instrument)`.

---

### Item 1d — `RelayCancel(CancelEventArgs e)` method

**Type**: `public void` ICopyEngine implementation
**Source**: `PttContracts.cs` line 91; called by `PttCopier.OnCancelFired` (line 87)
**Insertion point**: After `RelayFlatten` (Item 1c above)

**Code to insert**:
```csharp
        // B58 ICopyEngine -- RelayCancel: delegate to CancelPendingEntries(Instrument) fan-out. CYC=1.
        // CancelPendingEntries(Instrument) at line 1192 iterates AllAccounts and calls CancelOneAccount.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayCancel(CancelEventArgs e) => CancelPendingEntries(e.Instrument);
```

**CYC**: 1 (expression body, no branch)
**JS compliance**: JS-021 — no lock. JS-002 — void, no null return. Delegates to existing `CancelPendingEntries(Instrument)`.

---

### Item 2 — `IsEnabled` property

**Type**: Public read-only property
**Source**: B54-LaneA `ticket-1-completion.md` §A1
**Insertion point**: After `SetEnabled()` method — after line 273

**Code to insert** (after line 273, before `SetDailyCapFloor`):
```csharp
        // B54 -- IsEnabled: read-only view of _isCopyEnabled (JS-023: volatile bool read).
        // CYC=1. Used by TradeCopierPanel.OnLoaded snap and TradeCopierWindow.OnLoaded snap.
        public bool IsEnabled => _isCopyEnabled;
```

**CYC**: 1 (expression body, no branch)
**JS compliance**: JS-023 — volatile bool read is safe without lock. JS-002 — no null return.

---

### Item 3 — `_globalBe` field + `GlobalBe` property

**Type**: Private field + public property
**Source**: B39-LaneA `ticket-1-completion.md` — "Line 99: GlobalBe property added"
**Insertion point A** (field): After `_copyModeValue` field — after line 103
**Insertion point B** (property): After `IsEnabled` property (Item 2 above)

**Field code** (insert after line 103):
```csharp
        // B39 -- _globalBe: singleton reference to the shared Global BE execution engine.
        // Lazily initialized; Panel + Window both read via GlobalBe property.
        // JS-023: volatile null-check safe for singleton reads on CLR 4.0+.
        private PttGlobalBreakEven _globalBe = null;
```

**Property code** (insert after `IsEnabled` property):
```csharp
        // B39 -- GlobalBe: shared Global BE engine. Lazy-init on first access.
        // CYC=2 (null check + assignment).
        // JS-021: no lock -- CLR object reference assignment is atomic on 64-bit.
        public PttGlobalBreakEven GlobalBe
        {
            get
            {
                if (_globalBe == null)
                    _globalBe = new PttGlobalBreakEven();
                return _globalBe;
            }
        }
```

**CYC**: field=0 (declaration), property getter=2 (null check + implicit else)
**JS compliance**: JS-021 — CLR reference assignment is atomic; no lock needed for lazy singleton
init in single-UI-thread context. NT8-003 — no volatile double/float.

**Source brain artifact**: B39-LaneA ticket-1-completion.md, Implementation Note #1.

---

### Item 4 — `IsPendingSlotsEmpty()` method

**Type**: Internal method
**Source**: B40-LaneA `ticket-1-completion.md` §3
**Insertion point**: After `DisarmPendingBe` method — after line 1664

**Code to insert**:
```csharp
        // B40 -- IsPendingSlotsEmpty: CYC=1. Lock-free read of ConcurrentDictionary.IsEmpty.
        // Called by TradeCopierPanel BE ALL armed/wait flow to determine gate state.
        // JS-021: ConcurrentDictionary.IsEmpty is lock-free.
        internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;
```

**CYC**: 1 (expression body delegate, no branch)
**JS compliance**: JS-021 — ConcurrentDictionary.IsEmpty is lock-free. JS-002 — returns bool (not null).

**Source brain artifact**: B40-LaneA ticket-1-completion.md §3.

---

### Item 5a — `_cloneAtmCache` volatile field

**Type**: Private volatile string field
**Source**: B50-LaneA `ticket-1-completion.md` — "`_cloneAtmCache` field: After line 108"
**Insertion point**: After `_copyModeValue` field — after line 103 (alongside Item 3 field)

**Code to insert** (after `_copyModeValue` at line 103, before `_dedupCache`):
```csharp
        // B50 -- _cloneAtmCache: volatile string holds the ATM template name captured at Clone mode activation.
        // volatile string: reference-type writes are atomic on CLR 4.0+ (JS-023 compliant).
        // NT8-003: volatile double/float BANNED -- string is safe.
        private volatile string _cloneAtmCache = string.Empty;
```

**CYC**: 0 (field declaration)
**JS compliance**: JS-023 — volatile string is safe (reference type, CLR atomic write). NT8-003 compliant.

---

### Item 5b — `SetCloneAtmCache(string)` method

**Type**: Internal method
**Source**: B50-LaneA `ticket-1-completion.md`
**Insertion point**: After `GetCopyMode()` method — after the ICopyEngine relay block (Items 1a–1d)

**Code to insert**:
```csharp
        // B50 -- SetCloneAtmCache: CYC=1. Stores ATM template name for Clone mode dispatch.
        // Called from TradeCopierPanel.OnCloneModeClick after reading leader's current ATM template.
        // JS-023: volatile string write is atomic.
        internal void SetCloneAtmCache(string value)
        {
            _cloneAtmCache = value ?? string.Empty;
        }
```

**CYC**: 1 (straight-line, no branch — null-coalesce is not a branch in CYC counting)
**JS compliance**: JS-023 — volatile write. JS-002 — no null assignment (coalesces to empty).

---

### Item 5c — `GetCloneAtmMode()` method

**Type**: Internal method
**Source**: B50-LaneA `ticket-1-completion.md`
**Insertion point**: After `SetCloneAtmCache` (Item 5b above)

**Code to insert**:
```csharp
        // B50 -- GetCloneAtmMode: CYC=2. Returns Named(cache) if cache non-empty, else Inherit.
        // Called by ResolveAtmMode when CopyMode == Clone.
        // JS-002: never returns null -- returns Inherit as fallback.
        internal FollowerAtmMode GetCloneAtmMode()
        {
            var cache = _cloneAtmCache;
            if (cache != null && cache.Length > 0)  // branch (1)
                return new FollowerAtmMode.Named(cache);
            return new FollowerAtmMode.Inherit();
        }
```

**CYC**: 2 (1 base + 1 if-branch)
**JS compliance**: JS-002 — never returns null. JS-023 — reads volatile via local snapshot.

---

### Item 5d — `ResolveAtmMode(CopyRule, string)` method

**Type**: Private method
**Source**: B50-LaneA `ticket-1-completion.md` — "After `GetAtmMode` (~line 889)"
**Insertion point**: After `GetAtmMode` at line 959 (i.e., after the closing `}` of `GetAtmMode`)

**Code to insert**:
```csharp
        // B50 -- ResolveAtmMode: CYC=2. Mode-aware ATM dispatch router.
        // Clone mode uses shared _cloneAtmCache; Signal/Mirror modes delegate to GetAtmMode (per-rule).
        // Replaces direct GetAtmMode call in DispatchCopy inner loop.
        // JS-002: never returns null -- all branches return a FollowerAtmMode subtype.
        private FollowerAtmMode ResolveAtmMode(CopyRule rule, string accountName)
        {
            if (GetCopyMode() == CopyMode.Clone)  // branch (1)
                return GetCloneAtmMode();
            return GetAtmMode(rule, accountName);
        }
```

**CYC**: 2 (1 base + 1 if-branch)
**JS compliance**: JS-002 — never returns null. No lock.

---

### Item 5e — DispatchCopy: `GetAtmMode` → `ResolveAtmMode` (1-line change)

**Type**: Call-site substitution
**Source**: B50-LaneA `ticket-1-completion.md` — "Changed `GetAtmMode(rule, acc.Name)` → `ResolveAtmMode(rule, acc.Name)` (1-line change)"
**Location**: Line 699

**Current** (line 699):
```csharp
                var mode = GetAtmMode(rule, acc.Name);
```

**Replacement**:
```csharp
                var mode = ResolveAtmMode(rule, acc.Name);
```

**CYC impact**: DispatchCopy CYC remains 8 (at limit, PASS — this is a same-type substitution, no new branch).

---

### Item 6 — `FindPositionPublic(Account, Instrument)` method

**Type**: Internal thin wrapper
**Source**: Derived — `FindPosition` is private; panel needs public access for position truth reads.
**Insertion point**: After `FindPosition` private method — after line 1429

**Code to insert**:
```csharp
        // B58 -- FindPositionPublic: thin wrapper over private FindPosition for panel access.
        // CYC=1. Returns null if no position (pre-existing FindPosition behavior -- not new).
        // JS-002: null return is pre-existing contract of FindPosition, not new code.
        internal Position FindPositionPublic(Account acc, Instrument instrument)
            => FindPosition(acc, instrument);
```

**CYC**: 1 (expression body delegate)
**JS compliance**: JS-002 — null return is pre-existing in FindPosition; this wrapper does not
introduce a new null-return site. Pre-existing debt per B50 SCAN-03 note.

---

### Item 7 — `SnapshotTargetsPublic(Account, Instrument)` method

**Type**: Internal method
**Source**: Derived — panel needs a working-order snapshot count for UI display.
**Insertion point**: After `FindPositionPublic` (Item 6 above)

**Code to insert**:
```csharp
        // B58 -- SnapshotTargetsPublic: collects Working orders with PTT-QX-T or PTT-TGT- prefix.
        // CYC=3 (1 base + foreach + prefix check). Returns List<Order> -- panel uses .Count.
        // JS-002: never returns null -- returns empty List if no matches.
        // JS-021: acc.Orders iteration; no lock required (NT8 AddOn read-only enumeration).
        internal List<Order> SnapshotTargetsPublic(Account acc, Instrument instr)
        {
            var result = new List<Order>();
            if (acc == null || instr == null) return result;             // (1) null guard
            foreach (Order o in acc.Orders)                              // (2) foreach
            {
                if (o.Instrument != instr) continue;
                if (o.OrderState != OrderState.Working) continue;
                string n = o.Name ?? string.Empty;
                if (n.StartsWith("PTT-QX-T", StringComparison.Ordinal)  // (3) prefix check
                 || n.StartsWith("PTT-TGT-", StringComparison.Ordinal))
                    result.Add(o);
            }
            return result;
        }
```

**CYC**: 3 (1 base + foreach + OR-prefix check counts as 1 branch)
**JS compliance**: JS-002 — never returns null. JS-021 — no lock. ASCII-only strings.

---

### Item 8 — `CopyRulesContainer.CopyEnabled` property

**Type**: Public serialized property on private nested class
**Source**: B54-LaneA `ticket-1-completion.md` §A2
**Location**: Inside `CopyRulesContainer` class body — after `Rules` property at line 1813

**Current** `CopyRulesContainer` (lines 1811-1814):
```csharp
private sealed class CopyRulesContainer
{
    public List<CopyRuleDto> Rules { get; set; } = new List<CopyRuleDto>();
}
```

**Replacement** `CopyRulesContainer`:
```csharp
[Serializable]
private sealed class CopyRulesContainer
{
    public List<CopyRuleDto> Rules { get; set; } = new List<CopyRuleDto>();
    // B54 -- persists copy-enabled state so F5 cycle restores button color correctly.
    // NT8-001: { get; set; } (not init accessor). XmlSerializer requires public { set; }.
    public bool CopyEnabled { get; set; } = false;
}
```

**CYC**: 0 (auto-property declaration)
**JS compliance**: NT8-001 — `{ get; set; }` (not `{ get; init; }`). Xml-serializable: public set.

---

### Item 9a — `SaveRules`: write `container.CopyEnabled`

**Type**: Statement insertion inside existing method
**Source**: B54-LaneA `ticket-1-completion.md` §A3
**Location**: `SaveRules` at line 1931 — after `container.Rules.Add(RuleToDto(rule));` loop closes,
before `var serializer = new XmlSerializer(...)` at line 1933.

**Current** (lines 1930-1933):
```csharp
                var container = new CopyRulesContainer();
                foreach (var rule in _rules)
                    container.Rules.Add(RuleToDto(rule));

                var serializer = new XmlSerializer(typeof(CopyRulesContainer));
```

**Replacement**:
```csharp
                var container = new CopyRulesContainer();
                foreach (var rule in _rules)
                    container.Rules.Add(RuleToDto(rule));
                container.CopyEnabled = _isCopyEnabled;  // B54: persist enabled state

                var serializer = new XmlSerializer(typeof(CopyRulesContainer));
```

**CYC impact**: SaveRules CYC unchanged (+1 statement, 0 branches).

---

### Item 9b — `LoadRules`: restore `_isCopyEnabled` and fire `CopyEnabledChanged`

**Type**: Statement insertion inside existing method
**Source**: B54-LaneA `ticket-1-completion.md` §A4
**Location**: `LoadRules` — after the `foreach (var dto in container.Rules)` loop at line 1975,
inside the `if (container != null && container.Rules != null)` block, before the closing `}`.

**Current** (lines 1972-1976):
```csharp
                    if (container != null && container.Rules != null)
                    {
                        foreach (var dto in container.Rules)
                            _rules.Add(DtoToRule(dto));
                    }
```

**Replacement**:
```csharp
                    if (container != null && container.Rules != null)
                    {
                        foreach (var dto in container.Rules)
                            _rules.Add(DtoToRule(dto));
                        _isCopyEnabled = container.CopyEnabled;             // B54: restore enabled state
                        CopyEnabledChanged?.Invoke(_isCopyEnabled);         // B54: sync UI buttons
                    }
```

**CYC impact**: LoadRules CYC +1 (the `?.Invoke` is 1 branch; the assignment is 0). Remains ≤ 8.

---

## D. 7-Scan Pre-Analysis

### SCAN-01 — lock() — JS-021

All 13 items: **ZERO lock() usage.** No new method requires shared-state synchronization:
- `RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel` — delegate to existing lock-free CopyEngine
  methods (`AllAccounts` snapshot + `SubmitBeStop`, `Trim`, `Flatten`, `CancelPendingEntries`).
- `IsEnabled`, `GlobalBe`, `IsPendingSlotsEmpty`, `SetCloneAtmCache`, `GetCloneAtmMode`,
  `ResolveAtmMode`, `FindPositionPublic`, `SnapshotTargetsPublic` — all read volatile fields or
  delegate to existing ConcurrentDictionary/ConcurrentBag operations.
- `CopyEnabled` property: XmlSerializer-owned, no cross-thread access.
- SaveRules/LoadRules additions: single-statement assigns, no contention path.

**SCAN-01 verdict**: PASS (pre-analysis)

---

### SCAN-02 — async void — JS-033

All 13 items: **ZERO async/await usage.** All new members are synchronous void, bool, or
expression-body returns. No DispatcherTimer, no Task.

**SCAN-02 verdict**: PASS (pre-analysis)

---

### SCAN-03 — return null — JS-002

- `RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel` → void methods, no return
- `IsEnabled` → returns `bool` (no null)
- `GlobalBe` property → lazy-init, always returns non-null `PttGlobalBreakEven`
- `IsPendingSlotsEmpty` → returns `bool`
- `SetCloneAtmCache` → void
- `GetCloneAtmMode` → always returns `FollowerAtmMode` subtype (Inherit as fallback)
- `ResolveAtmMode` → always returns `FollowerAtmMode` subtype
- `FindPositionPublic` → delegates to `FindPosition` which can return null; this is **pre-existing**
  contract (documented B50 SCAN-03), not a new null-return site introduced by B58
- `SnapshotTargetsPublic` → returns empty `List<Order>` (never null)
- `CopyEnabled`, SaveRules/LoadRules additions → bool/void, no null return

**SCAN-03 verdict**: PASS — 0 new null-return sites introduced (pre-existing in FindPosition, not new)

---

### SCAN-04 — throw new — JS-001

All 13 items: **ZERO throw new.** No exception throwing anywhere in new code. All error paths use
early return void, bool false, or fallback values.

**SCAN-04 verdict**: PASS (pre-analysis)

---

### SCAN-05 — CYC ≤ 8 per new method

| Member | CYC | At/Under Limit | Notes |
|--------|-----|----------------|-------|
| `RelayBe` | 2 | ✅ PASS | foreach = 1 branch |
| `RelayTrim` | 1 | ✅ PASS | Expression body delegate |
| `RelayFlatten` | 1 | ✅ PASS | Expression body delegate |
| `RelayCancel` | 1 | ✅ PASS | Expression body delegate |
| `IsEnabled` (property) | 1 | ✅ PASS | Expression body |
| `GlobalBe` (property getter) | 2 | ✅ PASS | Null check + assign |
| `IsPendingSlotsEmpty` | 1 | ✅ PASS | Expression body delegate |
| `SetCloneAtmCache` | 1 | ✅ PASS | Null-coalesce is not a branch |
| `GetCloneAtmMode` | 2 | ✅ PASS | 1 if-branch |
| `ResolveAtmMode` | 2 | ✅ PASS | 1 if-branch |
| `DispatchCopy` (modified) | 8 | ✅ PASS | AT LIMIT — same as B50 tip; 1-line substitution adds 0 branches |
| `FindPositionPublic` | 1 | ✅ PASS | Expression body delegate |
| `SnapshotTargetsPublic` | 3 | ✅ PASS | null guard + foreach + prefix OR |
| `SaveRules` (modified) | unchanged | ✅ PASS | +1 statement, 0 branches |
| `LoadRules` (modified) | pre+1 | ✅ PASS | +1 branch (?.Invoke); remains ≤ 8 |

Maximum CYC across all new/modified code: **3** (SnapshotTargetsPublic). All within budget.

---

### SCAN-06 — dotnet build — 0 new errors

B58 changes are confined to `CopyEngine.cs`. All types used by new methods are already in scope:
- `BeEventArgs`, `TrimEventArgs`, `FlatEventArgs`, `CancelEventArgs` — defined in `PttContracts.cs`,
  same namespace `PropTraderTools`. No new `using` directive required.
- `AllAccounts(Instrument)` — existing private helper already called at lines 1008, 1014, 1028, etc.
- `SubmitBeStop(Account, Instrument, double)` — existing internal method at line 381.
- `Trim(Instrument)`, `Flatten(Instrument)`, `CancelPendingEntries(Instrument)` — existing internal
  methods at lines 1006, 1012, 1192.

**Pre-existing exempt errors**: `AtrSizingEngine.cs` carries 2 CS0234/CS0246 errors for
NT8 runtime-only assemblies (`NinjaTrader.NinjaScript.AtmStrategy`). These are pre-existing and
exempt from B58 — same precedent as B39/B40/B50/B54. Engineer must verify the pre-B58 baseline
error count (expected: 2 pre-existing) and confirm that B58 changes add 0 new errors.

**SCAN-06 verdict**: Expected PASS — 0 new build errors. Engineer confirms against pre-existing
baseline after applying all 15 code changes.

---

### SCAN-07 — verify_links.ps1 — DESYNC=0, MISSING=0

After committing T1 changes to `CopyEngine.cs`, engineer must run:

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

This repairs any hard-link desync between the workspace `src/` and the NinjaTrader AddOn directory
caused by the file modification. Expected output after repair: `DESYNC=0, MISSING=0`.

**SCAN-07 verdict**: Expected PASS after `-Fix` run. Must be run AFTER commit, not before.

---

## E. Test Plan

No new `[Fact]` tests are required for this epic.

**Rationale**:
1. The 4 ICopyEngine relay methods (`RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel`) are
   thin wrappers that delegate to existing CopyEngine methods (`SubmitBeStop`, `Trim`, `Flatten`,
   `CancelPendingEntries`). These existing methods are already tested. The relay methods add no
   logic — they are pure delegation paths whose correctness is verified by compile-time interface
   satisfaction (CS0535 = 0) and integration with the existing test suite.
2. All other new members are thin wrappers or property exposures. Their correctness is structurally
   verified by the fact that `TradeCopierPanel.cs` and `TradeCopierWindow.cs` call them — the
   build gate (dotnet build 0 errors) is the primary test.
3. `IsPendingSlotsEmpty` logic is already fully tested by B40 existing tests.
4. `IsEnabled`, `CopyRulesContainer.CopyEnabled`, SaveRules/LoadRules round-trip: tested by B54
   existing tests `T_B54_01`, `T_B54_02`, `T_B54_03`.
5. Clone-mode methods (`SetCloneAtmCache`, `GetCloneAtmMode`, `ResolveAtmMode`): tested by B50
   existing tests `T_B50_03` and `T_B50_05`.
6. `FindPositionPublic` / `SnapshotTargetsPublic`: pure pass-through wrappers; wrapped methods tested.

**[Fact] count expectation**: Baseline unchanged (278 total from B54 tip).

---

## F. Risk Assessment

### Risk 1 — Line number drift (MEDIUM)
B57 was committed after B54; it may have shifted line numbers. **Mitigation**: Engineer MUST
search for anchor text rather than trusting absolute line numbers:
- Use `SearchText("private CopyEngine()")` to locate constructor → `SetEnabled` is 4 lines below.
- Use `SearchText("private sealed class CopyRulesContainer")` to locate DTO class.
- Use `SearchText("var mode = GetAtmMode(rule, acc.Name)")` to locate the DispatchCopy call-site.
- Use `SearchText("foreach (var dto in container.Rules)")` to locate LoadRules insertion point.
- Use `SearchText("private Position FindPosition(")` to locate FindPosition → insert after closing `}`.
- Use `SearchText("internal CopyMode GetCopyMode()")` to locate the relay method insertion point.

### Risk 2 — `GlobalBe` lazy-init threading (LOW)
`GlobalBe` property uses non-atomic lazy init (`if (_globalBe == null) _globalBe = new...`).
This is safe because both `TradeCopierPanel` and `TradeCopierWindow` access `GlobalBe` exclusively
from the WPF UI thread (Dispatcher thread). No cross-thread access to this property. Per B39
Implementation Note #1: the lambda-capture-at-call-time pattern resolves circular reference safely.

### Risk 3 — `SnapshotTargetsPublic` order-name prefixes (LOW)
Prefixes `"PTT-QX-T"` and `"PTT-TGT-"` are hardcoded ASCII strings matching the naming conventions
established in B12/B41. Risk: if future blocks add new PTT-prefixed target order names, this
method needs updating. Mitigation: document in deferred items.

### Risk 4 — ICopyEngine interface visibility (LOW)
`ICopyEngine` is `public` but `CopyEngine` is `internal sealed`. C# permits an internal class to
implement a public interface — this is the intended pattern for testability (PttCopier accepts
`ICopyEngine` for mock injection in tests). No risk.

### Risk 5 — `CopyRulesContainer` `[Serializable]` attribute (CONFIRM)
The current `CopyRulesContainer` class in the live file at line 1811 already has `[Serializable]`
on the preceding line 1810. Engineer confirms this attribute is present before inserting `CopyEnabled`
property — no duplicate attribute needed.

### Risk 6 — `RelayBe` fanout accounts (LOW)
`RelayBe` delegates `BeEventArgs.BePrice` (a pre-calculated price from the BE module) via
`SubmitBeStop` to `AllAccounts(e.Instrument)`. This is consistent with the fan-out contract:
all accounts tracking the given instrument receive the same BE price. The OcoGroup field from
`BeEventArgs` is not passed to `SubmitBeStop` because `SubmitBeStop` generates its own OcoId
via `NextQxOcoId()`. This is pre-existing `SubmitBeStop` behavior — not a new gap.

---

## G. Insertion Order for T1 Engineer

Apply changes in this exact order to minimize diff noise:

| Order | Item | Anchor Text to Search | Action |
|-------|------|-----------------------|--------|
| 1 | Class declaration | `internal sealed class CopyEngine` | Append `: ICopyEngine` |
| 2 | `_cloneAtmCache` field | `private volatile int _copyModeValue` | Insert after — volatile string field |
| 3 | `_globalBe` field | `_cloneAtmCache = string.Empty;` (from step 2) | Insert after — PttGlobalBreakEven field |
| 4 | `IsEnabled` property | closing `}` of `SetEnabled()` | Insert after |
| 5 | `GlobalBe` property | `public bool IsEnabled => _isCopyEnabled;` | Insert after |
| 6 | `RelayBe` method | closing `}` of `GetCopyMode()` | Insert after — ICopyEngine block start |
| 7 | `RelayTrim` method | closing `}` of `RelayBe` | Insert after |
| 8 | `RelayFlatten` method | closing `}` of `RelayTrim` | Insert after |
| 9 | `RelayCancel` method | closing `}` of `RelayFlatten` | Insert after — ICopyEngine block end |
| 10 | `SetCloneAtmCache` | closing `}` of `RelayCancel` (or `GetCopyMode` if 6-9 land there) | Insert after |
| 11 | `GetCloneAtmMode` | closing `}` of `SetCloneAtmCache` | Insert after |
| 12 | `ResolveAtmMode` | `private static FollowerAtmMode GetAtmMode(` ... closing `}` | Insert after |
| 13 | DispatchCopy fix | `var mode = GetAtmMode(rule, acc.Name);` | Replace text |
| 14 | `IsPendingSlotsEmpty` | closing `}` of `DisarmPendingBe` | Insert after |
| 15 | `FindPositionPublic` | closing `}` of `private Position FindPosition(` | Insert after |
| 16 | `SnapshotTargetsPublic` | closing `}` of `FindPositionPublic` | Insert after |
| 17 | `CopyEnabled` in container | `public List<CopyRuleDto> Rules { get; set; }` | Insert after (inside CopyRulesContainer) |
| 18 | SaveRules CopyEnabled | `container.Rules.Add(RuleToDto(rule));` + loop closing | Insert after foreach closing `}` |
| 19 | LoadRules restore | `_rules.Add(DtoToRule(dto));` foreach closing | Insert after foreach `}` inside container null-check |

---

## H. Deferred Items

| ID | Priority | Description |
|----|----------|-------------|
| DW-B58-01 | P2 | `SnapshotTargetsPublic` hardcoded prefixes — future blocks adding new PTT-TGT variants must update this method |
| DW-B58-02 | P2 | `GlobalBe` non-atomic lazy init — safe for current single-UI-thread access; future multi-thread caller would need `Interlocked.CompareExchange` |
| DW-B58-03 | P2 | `RelayBe` does not forward `OcoGroup` from `BeEventArgs` — `SubmitBeStop` generates its own OcoId. If future blocks need correlated OcoId fan-out, a new overload will be required. |

---

*ptt-architect | Phase 1 (Revision 1) | B58-LaneA | 2026-08-10*
