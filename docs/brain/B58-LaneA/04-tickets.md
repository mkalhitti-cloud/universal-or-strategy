# B58-LaneA — Ticket Backlog

**Phase**: 3 — Ticket Generation
**Mode**: ptt-architect
**Date**: 2026-08-10
**Source plan**: docs/brain/B58-LaneA/02-architecture-plan.md (REVIEW_PASS, 0 violations)
**Ticket count**: 1

---

## Ticket-1: Add 13 Missing Members to CopyEngine.cs

### Spec Requirement IDs
- B39-§CopyEngine: `_globalBe` field + `GlobalBe` property
- B40-§3: `IsPendingSlotsEmpty()` method
- B50-§CE: `_cloneAtmCache` field + `SetCloneAtmCache` + `GetCloneAtmMode` + `ResolveAtmMode` + `DispatchCopy` call-site wire
- B54-§A1: `IsEnabled` property
- B54-§A2: `CopyRulesContainer.CopyEnabled` property
- B54-§A3: `SaveRules` wiring
- B54-§A4: `LoadRules` wiring
- B58-§1: `: ICopyEngine` class declaration + 4 relay method implementations (`RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel`)

---

### Files Modified
- `src/PropTraderTools/CopyEngine.cs` (single file — all 19 code changes here, zero changes to any other file)

### Files Read (no modification)
- `src/PropTraderTools/Core/PttContracts.cs` — ICopyEngine interface exact signatures (lines 82/85/88/91)
- `src/PropTraderTools/TradeCopierPanel.cs` — confirms callers of `FindPositionPublic`, `SnapshotTargetsPublic`, `IsEnabled`, `GlobalBe`
- `src/PropTraderTools/Features/PttCopier.cs` — confirms `PttCopier(ICopyEngine)` constructor injection and relay call sites
- `src/PropTraderTools/Features/PttGlobalBreakEven.cs` — confirms `PttGlobalBreakEven` type held by `_globalBe`

---

### Method Signatures (Engineer Contract — exact C# code, copy verbatim)

This is the engineer's sole contract. Every item below must appear exactly as written in the
final committed file. Do not abbreviate, paraphrase, or use placeholders.

---

#### Change 1 — Class declaration: add `: ICopyEngine`

**Anchor**: `internal sealed class CopyEngine`  
**Action**: Append `: ICopyEngine` to the class declaration line

```csharp
// BEFORE (line 91 in current file):
internal sealed class CopyEngine

// AFTER:
internal sealed class CopyEngine : ICopyEngine
```

---

#### Change 2 — `_cloneAtmCache` volatile field

**Anchor**: `private volatile int _copyModeValue` — insert the new field on the line AFTER  
**Spec**: B50-§CE

```csharp
        // B50 -- _cloneAtmCache: volatile string holds ATM template name captured at Clone mode activation.
        // volatile string: reference-type writes are atomic on CLR 4.0+ (JS-023 compliant).
        // NT8-003: volatile double/float BANNED -- string is safe.
        private volatile string _cloneAtmCache = string.Empty;
```

---

#### Change 3 — `_globalBe` field

**Anchor**: line inserted for `_cloneAtmCache` in Change 2 — insert the new field on the line AFTER  
**Spec**: B39-§CopyEngine

```csharp
        // B39 -- _globalBe: singleton reference to shared Global BE execution engine.
        // Lazily initialized; Panel and Window read via GlobalBe property (UI thread only).
        // JS-023: volatile null-check safe for singleton reads on CLR 4.0+.
        private PttGlobalBreakEven _globalBe = null;
```

---

#### Change 4 — `IsEnabled` property

**Anchor**: closing `}` of `SetEnabled()` method — insert AFTER  
**Spec**: B54-§A1

```csharp
        // B54 -- IsEnabled: read-only view of _isCopyEnabled (JS-023: volatile bool read).
        // CYC=1. Used by TradeCopierPanel.OnLoaded snap and TradeCopierWindow.OnLoaded snap.
        public bool IsEnabled => _isCopyEnabled;
```

---

#### Change 5 — `GlobalBe` property

**Anchor**: `public bool IsEnabled => _isCopyEnabled;` line from Change 4 — insert AFTER  
**Spec**: B39-§CopyEngine

```csharp
        // B39 -- GlobalBe: shared Global BE engine. Lazy-init on first access (UI thread only).
        // CYC=2 (null check + assignment).
        // JS-021: no lock -- CLR object reference assignment is atomic on 64-bit.
        // JS-002: always returns non-null; new PttGlobalBreakEven() as fallback.
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

---

#### Change 6 — `RelayBe` method (ICopyEngine implementation)

**Anchor**: closing `}` of `GetCopyMode()` method — insert AFTER  
**Spec**: B58-§1 + PttContracts.cs line 82

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

---

#### Change 7 — `RelayTrim` method (ICopyEngine implementation)

**Anchor**: closing `}` of `RelayBe` from Change 6 — insert AFTER  
**Spec**: B58-§1 + PttContracts.cs line 85

```csharp
        // B58 ICopyEngine -- RelayTrim: delegate to Trim(Instrument) fan-out. CYC=1.
        // Trim(Instrument) at line 1006 iterates AllAccounts and calls TrimOneAccount per account.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayTrim(TrimEventArgs e) => Trim(e.Instrument);
```

---

#### Change 8 — `RelayFlatten` method (ICopyEngine implementation)

**Anchor**: closing `}` (or expression body `;`) of `RelayTrim` from Change 7 — insert AFTER  
**Spec**: B58-§1 + PttContracts.cs line 88

```csharp
        // B58 ICopyEngine -- RelayFlatten: delegate to Flatten(Instrument) fan-out. CYC=1.
        // Flatten(Instrument) at line 1012 iterates AllAccounts and calls FlattenOneAccount per account.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayFlatten(FlatEventArgs e) => Flatten(e.Instrument);
```

---

#### Change 9 — `RelayCancel` method (ICopyEngine implementation)

**Anchor**: closing `}` (or expression body `;`) of `RelayFlatten` from Change 8 — insert AFTER  
**Spec**: B58-§1 + PttContracts.cs line 91

```csharp
        // B58 ICopyEngine -- RelayCancel: delegate to CancelPendingEntries(Instrument) fan-out. CYC=1.
        // CancelPendingEntries(Instrument) at line 1192 iterates AllAccounts and calls CancelOneAccount.
        // JS-021: no lock. JS-002: void, no return null.
        public void RelayCancel(CancelEventArgs e) => CancelPendingEntries(e.Instrument);
```

---

#### Change 10 — `SetCloneAtmCache` method

**Anchor**: closing `}` (or expression body `;`) of `RelayCancel` from Change 9 — insert AFTER  
**Spec**: B50-§CE

```csharp
        // B50 -- SetCloneAtmCache: CYC=1. Stores ATM template name for Clone mode dispatch.
        // Called from TradeCopierPanel.OnCloneModeClick after reading leader's current ATM template.
        // JS-023: volatile string write is atomic.
        internal void SetCloneAtmCache(string value)
        {
            _cloneAtmCache = value ?? string.Empty;
        }
```

---

#### Change 11 — `GetCloneAtmMode` method

**Anchor**: closing `}` of `SetCloneAtmCache` from Change 10 — insert AFTER  
**Spec**: B50-§CE

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

---

#### Change 12 — `ResolveAtmMode` method

**Anchor**: closing `}` of the existing `private static FollowerAtmMode GetAtmMode(` method — insert AFTER  
**Spec**: B50-§CE

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

---

#### Change 13 — DispatchCopy call-site: `GetAtmMode` → `ResolveAtmMode`

**Anchor**: `var mode = GetAtmMode(rule, acc.Name);` (currently at line 699)  
**Action**: Replace entire line — 1-line substitution only, 0 new branches  
**Spec**: B50-§CE

```csharp
// BEFORE:
                var mode = GetAtmMode(rule, acc.Name);

// AFTER:
                var mode = ResolveAtmMode(rule, acc.Name);
```

**CYC impact**: DispatchCopy CYC remains 8 — same-type substitution, no new branch.

---

#### Change 14 — `IsPendingSlotsEmpty` method

**Anchor**: closing `}` of `DisarmPendingBe` method — insert AFTER  
**Spec**: B40-§3

```csharp
        // B40 -- IsPendingSlotsEmpty: CYC=1. Lock-free read of ConcurrentDictionary.IsEmpty.
        // Called by TradeCopierPanel BE ALL armed/wait flow to determine gate state.
        // JS-021: ConcurrentDictionary.IsEmpty is lock-free.
        internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;
```

---

#### Change 15 — `FindPositionPublic` method

**Anchor**: closing `}` of `private Position FindPosition(` method — insert AFTER  
**Spec**: B58-§1 (panel access to private FindPosition)

```csharp
        // B58 -- FindPositionPublic: thin wrapper over private FindPosition for panel access.
        // CYC=1. Returns null if no position (pre-existing FindPosition behavior -- not new).
        // JS-002: null return is pre-existing contract of FindPosition, not a new null-return site.
        internal Position FindPositionPublic(Account acc, Instrument instrument)
            => FindPosition(acc, instrument);
```

---

#### Change 16 — `SnapshotTargetsPublic` method

**Anchor**: closing `}` (or expression body `;`) of `FindPositionPublic` from Change 15 — insert AFTER  
**Spec**: B58-§1 (panel needs working-order snapshot count for UI display)

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

---

#### Change 17 — `CopyRulesContainer.CopyEnabled` property

**Anchor**: `public List<CopyRuleDto> Rules { get; set; }` inside `private sealed class CopyRulesContainer` — insert AFTER  
**Spec**: B54-§A2  
**Note**: Confirm `[Serializable]` attribute already present on the class (line 1810 in pre-B58 file). Do NOT add a duplicate `[Serializable]`.

```csharp
        // B54 -- persists copy-enabled state so F5 cycle restores button color correctly.
        // NT8-001: { get; set; } (not init accessor). XmlSerializer requires public { set; }.
        public bool CopyEnabled { get; set; } = false;
```

---

#### Change 18 — `SaveRules`: write `container.CopyEnabled`

**Anchor**: `container.Rules.Add(RuleToDto(rule));` foreach loop closing `}` — insert the new statement AFTER the foreach closes, BEFORE `var serializer = new XmlSerializer(...)` line  
**Spec**: B54-§A3

```csharp
// ADD this single line after the foreach closing }:
                container.CopyEnabled = _isCopyEnabled;  // B54: persist enabled state
```

**Context for engineer** (showing surrounding lines):
```csharp
                var container = new CopyRulesContainer();
                foreach (var rule in _rules)
                    container.Rules.Add(RuleToDto(rule));
                container.CopyEnabled = _isCopyEnabled;  // B54: persist enabled state  ← INSERT

                var serializer = new XmlSerializer(typeof(CopyRulesContainer));
```

---

#### Change 19 — `LoadRules`: restore `_isCopyEnabled` and fire `CopyEnabledChanged`

**Anchor**: `_rules.Add(DtoToRule(dto));` foreach loop closing — insert 2 statements AFTER the foreach `}`, inside the `if (container != null && container.Rules != null)` block, BEFORE that block's closing `}`  
**Spec**: B54-§A4

```csharp
// ADD these two lines inside the container null-check if block, after the foreach:
                        _isCopyEnabled = container.CopyEnabled;             // B54: restore enabled state
                        CopyEnabledChanged?.Invoke(_isCopyEnabled);         // B54: sync UI buttons
```

**Context for engineer** (showing surrounding lines):
```csharp
                    if (container != null && container.Rules != null)
                    {
                        foreach (var dto in container.Rules)
                            _rules.Add(DtoToRule(dto));
                        _isCopyEnabled = container.CopyEnabled;             // B54: restore  ← INSERT
                        CopyEnabledChanged?.Invoke(_isCopyEnabled);         // B54: sync UI  ← INSERT
                    }
```

---

### Insertion Order (engineer must follow this exact sequence)

Apply in this order to minimize diff noise and satisfy field-before-method dependencies:

| Step | Change # | Anchor Text (grep for this) | Action |
|------|----------|----------------------------|--------|
| 1 | 1 | `internal sealed class CopyEngine` | Append `: ICopyEngine` |
| 2 | 2 | `private volatile int _copyModeValue` | Insert `_cloneAtmCache` field after |
| 3 | 3 | `private volatile string _cloneAtmCache` (from step 2) | Insert `_globalBe` field after |
| 4 | 4 | closing `}` of `SetEnabled()` | Insert `IsEnabled` property after |
| 5 | 5 | `public bool IsEnabled => _isCopyEnabled;` | Insert `GlobalBe` property after |
| 6 | 6 | closing `}` of `GetCopyMode()` | Insert `RelayBe` method after |
| 7 | 7 | closing `}` of `RelayBe` | Insert `RelayTrim` method after |
| 8 | 8 | `;` of `RelayTrim` expression body | Insert `RelayFlatten` method after |
| 9 | 9 | `;` of `RelayFlatten` expression body | Insert `RelayCancel` method after |
| 10 | 10 | `;` of `RelayCancel` expression body | Insert `SetCloneAtmCache` method after |
| 11 | 11 | closing `}` of `SetCloneAtmCache` | Insert `GetCloneAtmMode` method after |
| 12 | 12 | closing `}` of `GetAtmMode` private static method | Insert `ResolveAtmMode` method after |
| 13 | 13 | `var mode = GetAtmMode(rule, acc.Name);` | Replace with `ResolveAtmMode` call |
| 14 | 14 | closing `}` of `DisarmPendingBe` | Insert `IsPendingSlotsEmpty` method after |
| 15 | 15 | closing `}` of `private Position FindPosition(` | Insert `FindPositionPublic` method after |
| 16 | 16 | `;` of `FindPositionPublic` expression body | Insert `SnapshotTargetsPublic` method after |
| 17 | 17 | `public List<CopyRuleDto> Rules { get; set; }` inside `CopyRulesContainer` | Insert `CopyEnabled` property after |
| 18 | 18 | foreach `}` in `SaveRules` before `var serializer` | Insert `container.CopyEnabled = _isCopyEnabled;` |
| 19 | 19 | foreach `}` in `LoadRules` inside container null-check | Insert 2 lines: restore + Invoke |

**Dependency chain** (field must precede method that uses it):
- `_cloneAtmCache` (step 2) must be inserted before `SetCloneAtmCache` (step 10), `GetCloneAtmMode` (step 11), `ResolveAtmMode` (step 12)
- `GetCloneAtmMode` (step 11) must exist before `ResolveAtmMode` (step 12) calls it
- `IsEnabled` (step 4) must precede `GlobalBe` (step 5) for insertion anchoring
- All method bodies (steps 6–16) may be applied after field/property insertions are complete

---

### xUnit Test Coverage

**No new `[Fact]` tests required for this ticket.**

**Rationale**:
1. `RelayBe`, `RelayTrim`, `RelayFlatten`, `RelayCancel` — thin wrappers delegating to existing tested methods (`SubmitBeStop`, `Trim`, `Flatten`, `CancelPendingEntries`). Correctness verified by CS0535=0 at build gate.
2. `IsPendingSlotsEmpty` — existing B40 tests cover the underlying `_pendingBeSlots.IsEmpty` logic.
3. `IsEnabled`, `CopyRulesContainer.CopyEnabled`, `SaveRules`/`LoadRules` round-trip — covered by pre-existing B54 tests `T_B54_01`, `T_B54_02`, `T_B54_03`.
4. `SetCloneAtmCache`, `GetCloneAtmMode`, `ResolveAtmMode` — covered by pre-existing B50 tests `T_B50_03` and `T_B50_05`.
5. `FindPositionPublic`, `SnapshotTargetsPublic` — pure pass-through wrappers; wrapped private methods already tested.
6. `GlobalBe` property — single-UI-thread access; integration test path via TradeCopierPanel.

**[Fact] baseline**: 278 total from B54 tip. Must remain unchanged after T1 completes.

---

### 7-Scan Checklist (Engineer Contract — ALL 7 must be run and results reported)

Engineer MUST run every scan and record results in `ticket-1-completion.md`. All must show zero violations or explicit PASS.

---

**SCAN-01** — lock() ban (JS-021)

```powershell
grep -n "lock\s*(" src/PropTraderTools/CopyEngine.cs
```

Expected: **0 results**  
Rule: JS-021 — lock() is banned across all new code in this ticket  
Note: Comments containing the word "lock" are acceptable; only actual `lock(` call expressions are violations.

---

**SCAN-02** — async void ban (JS-033)

```powershell
grep -n "async void " src/PropTraderTools/CopyEngine.cs
```

Expected: **0 results**  
Rule: JS-033 — async void is banned in all new members (all new members are synchronous)

---

**SCAN-03** — return null (JS-002)

```powershell
grep -n "return null;" src/PropTraderTools/CopyEngine.cs
```

Expected: **0 new return null statements introduced by B58**  
Rule: JS-002 — do not return null for missing values  
Note: `FindPositionPublic` delegates to pre-existing private `FindPosition` which may return null. That is a pre-existing contract (documented in B50 SCAN-03), not a new null-return site introduced by B58. Engineer must report the total count and confirm no new `return null;` lines were added by this ticket.

---

**SCAN-04** — throw new (JS-001)

```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
```

Expected: **0 new throw new statements in B58 scope**  
Rule: JS-001 — never throw exceptions in hot paths; use early return or fallback values  
Note: Report total count. Zero new lines introduced by B58 is the pass condition.

---

**SCAN-05** — Cyclomatic complexity ≤ 8 (JS-066)

Engineer must verify CYC for all new and modified members:

| Member | Expected CYC | Pass threshold |
|--------|-------------|----------------|
| `RelayBe` | 2 | ≤ 8 |
| `RelayTrim` | 1 | ≤ 8 |
| `RelayFlatten` | 1 | ≤ 8 |
| `RelayCancel` | 1 | ≤ 8 |
| `IsEnabled` property | 1 | ≤ 8 |
| `GlobalBe` property getter | 2 | ≤ 8 |
| `IsPendingSlotsEmpty` | 1 | ≤ 8 |
| `SetCloneAtmCache` | 1 | ≤ 8 |
| `GetCloneAtmMode` | 2 | ≤ 8 |
| `ResolveAtmMode` | 2 | ≤ 8 |
| `DispatchCopy` (modified) | 8 | ≤ 8 (AT LIMIT — 1-line substitution, 0 new branches) |
| `FindPositionPublic` | 1 | ≤ 8 |
| `SnapshotTargetsPublic` | 3 | ≤ 8 |
| `SaveRules` (modified) | unchanged | ≤ 8 (+1 statement, 0 new branches) |
| `LoadRules` (modified) | pre+1 | ≤ 8 (+1 branch from `?.Invoke`; must remain ≤ 8) |

Maximum CYC across all new/modified code: **3** (SnapshotTargetsPublic). All within budget.

---

**SCAN-06** — Build (0 new errors)

```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

Expected: **0 new errors**  
Exempt pre-existing: `AtrSizingEngine.cs` carries 2 CS0234/CS0246 errors for NT8 runtime-only assemblies (`NinjaTrader.NinjaScript.AtmStrategy`). These 2 errors are pre-existing across all blocks B39–B57 and are exempt from B58.

Engineer must:
1. Record the pre-B58 baseline error count (expected: 2 pre-existing exempt errors)
2. Apply all 19 changes
3. Confirm post-B58 error count equals the pre-B58 baseline (0 new errors added)
4. The 4 relay methods (Changes 6–9) will resolve any CS0535 errors that adding `: ICopyEngine` (Change 1) introduces — both must land in the same commit

---

**SCAN-07** — Hard-link sync (DESYNC=0, MISSING=0)

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

Expected: `DESYNC=0, MISSING=0`  
Timing: Run AFTER commit to `CopyEngine.cs`, not before.  
Purpose: Repairs any hard-link desync between workspace `src/` and the NinjaTrader AddOn directory caused by the file modification.

---

### JS Rule Constraints (embedded — engineer must observe for all new code)

| Rule ID | Severity | Constraint | Applies To |
|---------|----------|-----------|-----------|
| JS-021 | P0 | `lock()` BANNED — no lock in any new code | All 19 changes |
| JS-033 | P0 | `async void` BANNED — all new members are synchronous | All 19 changes |
| JS-002 | P0 | Do not return null — `FindPositionPublic` delegates pre-existing null contract; all other new members return non-null or void | Changes 15, 16 |
| JS-001 | P0 | No `throw new` in hot paths — use early return or fallback | All 19 changes |
| JS-066 | P1 | CYC ≤ 8 for all methods — max new CYC is 3 (SnapshotTargetsPublic) | All new/modified methods |
| JS-023 | P1 | Volatile reads/writes: `_isCopyEnabled` (bool), `_cloneAtmCache` (string) are both safe without lock on CLR 4.0+ | Changes 2, 4, 10, 11, 18, 19 |
| NT8-001 | P1 | `{ get; set; }` on `CopyEnabled` — NOT `{ get; init; }`. XmlSerializer requires public setter. | Change 17 |
| NT8-003 | P1 | `volatile double/float` BANNED — `_cloneAtmCache` is string (reference type), which is safe | Change 2 |

---

### Definition of Done (BUILD_PASS)

Engineer writes `ticket-1-completion.md` only after ALL of the following are satisfied:

- [ ] Change 1 applied: `internal sealed class CopyEngine : ICopyEngine`
- [ ] Change 2 applied: `_cloneAtmCache` volatile string field inserted
- [ ] Change 3 applied: `_globalBe` field inserted
- [ ] Change 4 applied: `IsEnabled` property inserted
- [ ] Change 5 applied: `GlobalBe` property inserted
- [ ] Change 6 applied: `RelayBe` method inserted
- [ ] Change 7 applied: `RelayTrim` method inserted
- [ ] Change 8 applied: `RelayFlatten` method inserted
- [ ] Change 9 applied: `RelayCancel` method inserted
- [ ] Change 10 applied: `SetCloneAtmCache` method inserted
- [ ] Change 11 applied: `GetCloneAtmMode` method inserted
- [ ] Change 12 applied: `ResolveAtmMode` method inserted
- [ ] Change 13 applied: `DispatchCopy` call-site replaced `GetAtmMode` → `ResolveAtmMode`
- [ ] Change 14 applied: `IsPendingSlotsEmpty` method inserted
- [ ] Change 15 applied: `FindPositionPublic` method inserted
- [ ] Change 16 applied: `SnapshotTargetsPublic` method inserted
- [ ] Change 17 applied: `CopyRulesContainer.CopyEnabled` property inserted
- [ ] Change 18 applied: `SaveRules` writes `container.CopyEnabled = _isCopyEnabled;`
- [ ] Change 19 applied: `LoadRules` restores `_isCopyEnabled` and fires `CopyEnabledChanged`
- [ ] SCAN-01: 0 new `lock(` calls
- [ ] SCAN-02: 0 new `async void`
- [ ] SCAN-03: 0 new `return null;` introduced by B58 (pre-existing count documented)
- [ ] SCAN-04: 0 new `throw new` introduced by B58
- [ ] SCAN-05: All new/modified methods CYC ≤ 8 (max=3 for SnapshotTargetsPublic)
- [ ] SCAN-06: 0 new build errors (pre-existing 2 exempt errors unchanged)
- [ ] SCAN-07: DESYNC=0, MISSING=0 after `verify_links.ps1 -Fix`
- [ ] No regressions to existing CopyEngine behavior
- [ ] `ticket-1-completion.md` written and committed with scan results

---

*ptt-architect | Phase 3 — Ticket Generation | B58-LaneA | 2026-08-10*
