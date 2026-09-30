# B127 Architecture Plan
# DW-PTT-BE-FIX-01: Lazy Re-Resolve for Null Followers in AllAccounts()

**Block**: B127
**Defect**: DW-PTT-BE-FIX-01
**Phase**: 2 -- Architecture Plan
**Status**: REVIEW_PENDING
**Author**: ptt-architect
**Date**: 2026-08-25

---

## A. REQUIREMENT TRACE

### Spec Requirement

**Item**: DW-PTT-BE-FIX-01
**Source**: `docs/brain/B107/06-deferred-backlog.md`, section "DW-PTT-BE-FIX-01"
**Priority**: Medium
**Text**: "When a follower account is not in Account.All at LoadRules() time, the Option B warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the account later appears in Account.All."

### Option A Lazy Re-Resolve: Definition

When `AllAccounts()` encounters a `null` slot in `CopyRule.FollowerAccounts[i]`, instead of silently skipping it, it:

1. Looks up the original follower name from `CopyRule.FollowerAccountNames[i]`.
2. Checks an in-memory name-to-account cache (`_resolvedFollowers`).
3. If not cached, calls `FindFollowerAccount(name)` to re-scan `Account.All`.
4. On success: stores the resolved `Account` in the cache and yields it.
5. On failure: emits a warning and skips (same observable behavior as before, but now with retry evidence).

This eliminates the manual "uncheck + re-check in panel" workaround documented in DW-B85.

---

## B. CURRENT BEHAVIOR

### AllAccounts() Today (lines 3374-3386)

```csharp
private IEnumerable<Account> AllAccounts(Instrument instrument)
{
    var rule = FindRule(instrument);
    if (rule == null)
        yield break;

    yield return rule.Value.MasterAccount;
    foreach (var acc in rule.Value.FollowerAccounts)
    {
        if (acc != null)
            yield return acc;
    }
}
```

- **CYC**: 3 (null rule check + foreach + null acc check)
- **Behavior on null slot**: silently skips -- no retry, no warning at call time
- **Where the warning is emitted**: `DtoToRule()` at load time only (line 4255-4261)

### Why Names Are Lost Today

`CopyRule` stores `Account[] FollowerAccounts` -- resolved `Account` object references.
`DtoToRule` maps `dto.FollowerAccountNames[i]` to `Account` via `FindFollowerAccount()`.
If the account is not yet in `Account.All`, `followers[i]` is set to `null`.
The name string `dto.FollowerAccountNames[i]` is **not stored** in `CopyRule`.
After `DtoToRule` returns, the identity of the null slot is permanently lost.
`AllAccounts()` sees `null` but has no name to retry with.

---

## C. PROPOSED CHANGES

### C1. `CopyRule` Struct (lines 392-454)

**File**: `src/PropTraderTools/CopyEngine.cs`

Add one field (readonly, parallel to `FollowerAccounts[]`):

```csharp
// B127: original follower names parallel to FollowerAccounts[] -- enables lazy re-resolve
// in AllAccounts() for slots that were null at DtoToRule/LoadRules time (DW-PTT-BE-FIX-01).
// JS-008: readonly array field on readonly struct -- reference immutable, compliant.
internal readonly string[] FollowerAccountNames;
```

Update the **private constructor** (add `string[] followerAccountNames` parameter):

```csharp
private CopyRule(
    string instrument,
    Account master,
    Account[] followers,
    bool enabled,
    int[] multipliers,
    Dictionary<string, FollowerAtmMode> atmTemplates,
    int tightenTicks,
    string[] followerAccountNames        // NEW -- B127
)
{
    Instrument = instrument;
    MasterAccount = master;
    FollowerAccounts = followers;
    Enabled = enabled;
    FollowerMultipliers = multipliers;
    FollowerAtmTemplates = atmTemplates ?? new Dictionary<string, FollowerAtmMode>();
    TightenTicks = tightenTicks > 0 ? tightenTicks : 5;
    // B127: derive names from accounts when not supplied explicitly (backward compat).
    // DtoToRule supplies explicit names (which may cover null-account slots).
    // All other callers pass null -- names are derived from the resolved Account references.
    FollowerAccountNames = followerAccountNames ?? DeriveFollowerNames(followers);
}
```

Add **private static helper** `DeriveFollowerNames()` (CYC=2: null guard + loop):

```csharp
// B127: derives follower name strings from Account[] for backward-compat callers.
// Returns empty array for null/empty input. Never returns null (JS-002 convention).
// CYC=2: null guard + for loop.
private static string[] DeriveFollowerNames(Account[] followers)
{
    if (followers == null || followers.Length == 0)
        return Array.Empty<string>();
    var names = new string[followers.Length];
    for (int i = 0; i < followers.Length; i++)
        names[i] = followers[i]?.Name ?? string.Empty;
    return names;
}
```

Update the **`Create()` factory** (add optional `string[] followerAccountNames = null`):

```csharp
internal static CopyRule Create(
    string instrument,
    Account master,
    Account[] followers,
    bool enabled = true,
    int[] multipliers = null,
    Dictionary<string, FollowerAtmMode> atmTemplates = null,
    int tightenTicks = 5,
    string[] followerAccountNames = null    // NEW -- B127; null = derive from followers[]
) =>
    new CopyRule(
        instrument,
        master,
        followers,
        enabled,
        multipliers,
        atmTemplates ?? new Dictionary<string, FollowerAtmMode>(),
        tightenTicks,
        followerAccountNames               // passed through; null -> derive in ctor
    );
```

### C2. `DtoToRule()` (lines 4236-4298)

**File**: `src/PropTraderTools/CopyEngine.cs`

Change the final `CopyRule.Create()` call to pass `dto.FollowerAccountNames`:

```csharp
return CopyRule.Create(
    dto.InstrumentName,
    master,
    followers,
    dto.IsEnabled,
    multipliers,
    atmMap,
    tightenTicks,
    dto.FollowerAccountNames              // B127: preserve original names (may cover null-account slots)
);
```

No other changes to `DtoToRule()`. The warning on null at load time is **preserved as-is**.

### C3. `AddRule(3-arg)` (line 1131)

**File**: `src/PropTraderTools/CopyEngine.cs`

No change to the call site. The new optional `followerAccountNames = null` parameter means `CopyRule.Create(instrument, master, followers)` continues to compile and derives names from `followers[]` automatically. **No source edit required.**

### C4. `AddRule(5-arg)` (line 1159)

**File**: `src/PropTraderTools/CopyEngine.cs`

No change to the call site. Same backward-compat reasoning as C3. **No source edit required.**

### C5. `SetRuleEnabled()` (line 1108)

**File**: `src/PropTraderTools/CopyEngine.cs`

The rebuild must preserve `r.FollowerAccountNames`. Add the parameter to the `CopyRule.Create()` call inside the foreach:

```csharp
? CopyRule.Create(
    r.Instrument,
    r.MasterAccount,
    r.FollowerAccounts,
    enabled,
    r.FollowerMultipliers,
    r.FollowerAtmTemplates,
    r.TightenTicks,
    r.FollowerAccountNames             // B127: preserve names through rebuild
)
```

### C6. `SetFollowerMultiplier()` (line 1184)

**File**: `src/PropTraderTools/CopyEngine.cs`

Same pattern: add `r.FollowerAccountNames` to the `CopyRule.Create()` call:

```csharp
_rules.Add(
    CopyRule.Create(
        r.Instrument,
        r.MasterAccount,
        r.FollowerAccounts,
        r.Enabled,
        newMults,
        r.FollowerAtmTemplates,
        r.TightenTicks,
        r.FollowerAccountNames             // B127: preserve names through rebuild
    )
);
```

### C7. `SetAtmMode()` (line 2809)

**File**: `src/PropTraderTools/CopyEngine.cs`

Same pattern: add `r.FollowerAccountNames` to the `CopyRule.Create()` call:

```csharp
_rules.Add(
    CopyRule.Create(
        r.Instrument,
        r.MasterAccount,
        r.FollowerAccounts,
        r.Enabled,
        r.FollowerMultipliers,
        newMap,
        r.TightenTicks,
        r.FollowerAccountNames             // B127: preserve names through rebuild
    )
);
```

### C8. New field `_resolvedFollowers` (CopyEngine class level)

**File**: `src/PropTraderTools/CopyEngine.cs`

Add alongside the `_rules` field declaration area:

```csharp
// B127: lazy-resolve cache -- name -> Account. Populated on first successful resolve in
// AllAccounts(). Lock-free: ConcurrentDictionary TryGetValue + TryAdd (JS-021 compliant).
// Cleared on each LoadRules() call to handle account reconnect / session restart scenarios.
private readonly ConcurrentDictionary<string, Account> _resolvedFollowers =
    new ConcurrentDictionary<string, Account>();
```

### C9. `LoadRules()` (line 4359)

**File**: `src/PropTraderTools/CopyEngine.cs`

Add `_resolvedFollowers.Clear()` immediately after `_rules = new ConcurrentBag<CopyRule>()`:

```csharp
public void LoadRules(string overridePath = null)
{
    _rules = new ConcurrentBag<CopyRule>();
    _resolvedFollowers.Clear();           // B127: reset lazy-resolve cache on reload
    // ... remainder unchanged
}
```

### C10. `AllAccounts()` (lines 3374-3386)

**File**: `src/PropTraderTools/CopyEngine.cs`

Replace the null-skip with a lazy-resolve path:

```csharp
private IEnumerable<Account> AllAccounts(Instrument instrument)
{
    var rule = FindRule(instrument);
    if (rule == null)
        yield break;

    yield return rule.Value.MasterAccount;

    var followers = rule.Value.FollowerAccounts;
    var names = rule.Value.FollowerAccountNames;
    for (int i = 0; i < followers.Length; i++)
    {
        var acc = followers[i];
        if (acc != null)
        {
            yield return acc;
            continue;
        }
        // B127: lazy re-resolve for slot that was null at load time.
        var name = (names != null && i < names.Length) ? names[i] : null;
        if (string.IsNullOrEmpty(name))
            continue;
        if (_resolvedFollowers.TryGetValue(name, out var cached))
        {
            yield return cached;
            continue;
        }
        var resolved = FindFollowerAccount(name);
        if (resolved != null)
        {
            _resolvedFollowers.TryAdd(name, resolved);
            NinjaTrader.Code.Output.Process(
                "[PTT-COPY] INFO: follower '" + name + "' resolved lazily -- now copying to this account.",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            yield return resolved;
            continue;
        }
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY] WARNING: follower '" + name + "' not found in Account.All"
                + " -- account not connected yet; will retry on next dispatch.",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
    }
}
```

---

## D. CACHING STRATEGY

| Aspect | Decision |
|--------|----------|
| Cache type | `ConcurrentDictionary<string, Account>` |
| Key | Follower account name (string) |
| Value | Resolved `Account` reference |
| Write | `TryAdd()` on first successful lazy resolve |
| Read | `TryGetValue()` on every null slot in `AllAccounts()` |
| Clear | `_resolvedFollowers.Clear()` at start of every `LoadRules()` call |
| Lock usage | None -- `ConcurrentDictionary` is inherently lock-free (JS-021 compliant) |
| Scope | Instance field on `CopyEngine` -- shared across all `AllAccounts()` calls for this engine |

**Why C2 (separate dict) over C1 (rebuild ConcurrentBag in AllAccounts())**:
- C1 would mutate `_rules` from inside the `AllAccounts()` iterator, which iterates a local snapshot (`rule.Value`). Technically safe but architecturally noisy and expensive (full bag rebuild per resolved follower).
- C2 is minimal: one dict field, two dict operations, zero struct mutation, zero bag churn.

---

## E. CYC ANALYSIS

### AllAccounts() Post-Change

| Decision Point | Count |
|----------------|-------|
| `rule == null` (yield break) | 1 |
| `for` loop | 1 |
| `acc != null` (fast path) | 1 |
| `(names != null && i < names.Length)` ternary | 1 |
| `string.IsNullOrEmpty(name)` | 1 |
| `_resolvedFollowers.TryGetValue(...)` | 1 |
| `resolved != null` | 1 |
| **Total CYC** | **7** |

**7 <= 8: PASS. No extraction required.**

### DeriveFollowerNames() (new helper)

| Decision Point | Count |
|----------------|-------|
| `followers == null || followers.Length == 0` | 1 |
| `for` loop | 1 |
| **Total CYC** | **2** |

### All Other Modified Methods

No new decision points added to `SetRuleEnabled`, `SetFollowerMultiplier`, `SetAtmMode`, `DtoToRule`, or `LoadRules`. CYC unchanged for those methods.

---

## F. WARNING MESSAGE PLAN

| Situation | Message | Frequency |
|-----------|---------|-----------|
| Null at load time | `[PTT-COPY] WARNING: follower 'X' not found in Account.All at load time -- will be skipped until rule is re-applied (uncheck + re-check in panel).` | Once per LoadRules() (existing, unchanged) |
| Lazy resolve success | `[PTT-COPY] INFO: follower 'X' resolved lazily -- now copying to this account.` | Once per unique account (subsequent calls hit cache, no log) |
| Lazy resolve fail | `[PTT-COPY] WARNING: follower 'X' not found in Account.All -- account not connected yet; will retry on next dispatch.` | Every AllAccounts() call where lazy fails |

**Throttle decision**: No throttle. `AllAccounts()` is called per-trade-event (order fill, cancel, BE sweep), not on every tick. Repeated warnings on lazy fail are acceptable and provide useful signal that the account is still disconnected. Consistent with the existing DtoToRule warning intent.

---

## G. TEST CONTRACT

All tests in `src/PropTraderTools/Tests/B127Tests.cs`. Three xUnit `[Fact]` tests.

### Test 1: `AllAccounts_ReturnsResolvedFollower_WhenAccountPresentAtLoadTime`

**Purpose**: Confirms baseline behavior is preserved -- non-null slots still yield.

**Setup**:
- Build a `CopyEngine` with a stubbed `Account.All` containing `Sim101` (master) and `Sim102` (follower).
- Call `AddRule("MES", Sim101Account, new[] { Sim102Account })`.
- Call `AllAccounts(MesInstrument)`.

**Assert**:
- Result contains `Sim102Account`.
- No warning emitted.

### Test 2: `AllAccounts_LazyResolvesFollower_WhenAccountAppearsAfterLoad`

**Purpose**: Confirms Option A lazy re-resolve works when account appears in `Account.All` after load.

**Setup**:
- Build `CopyEngine`.
- Simulate DtoToRule scenario: call internal helper or use a stub `CopyRule` with `FollowerAccounts = [null]` and `FollowerAccountNames = ["Sim102"]`.
- At `AllAccounts()` call time, `Account.All` contains an account named `"Sim102"`.

**Assert**:
- `AllAccounts()` yields the lazily resolved `Sim102` account.
- `_resolvedFollowers` dict (if accessible via test seam) contains the entry.
- INFO message emitted (or verified via mock log capture if available).

### Test 3: `AllAccounts_SkipsFollower_WhenAccountStillNotResolvable`

**Purpose**: Confirms graceful skip + warning when account never appears.

**Setup**:
- Same as Test 2 but `Account.All` does NOT contain `"Sim102"`.
- Call `AllAccounts(MesInstrument)` twice.

**Assert**:
- `AllAccounts()` yields master only -- follower not included.
- WARNING message emitted (once per call -- two calls = two warnings).
- No exception thrown.

---

## H. RULES CATALOG COMPLIANCE

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` | PASS -- `ConcurrentDictionary.TryGetValue` + `TryAdd` used |
| JS-001 | No `throw` in hot paths | PASS -- no exceptions thrown; `FindFollowerAccount` returns null |
| JS-002 | No `return null` in public API | PASS -- `AllAccounts()` yields; no null values yielded |
| JS-008 | `readonly struct` preserved | PASS -- `FollowerAccountNames` is `internal readonly string[]` on `internal readonly struct` |
| JS-025 | Lock-free data structures | PASS -- `ConcurrentDictionary` replaces any need for locked `Dictionary` |
| CYC <= 8 | All methods <= 8 branches | PASS -- AllAccounts() = 7, DeriveFollowerNames() = 2 |

---

## I. BACKWARD COMPATIBILITY

| Caller | Change Required | Reason |
|--------|-----------------|--------|
| `AddRule(3-arg)` line 1131 | None | `followerAccountNames = null` default; ctor derives from `followers[]` |
| `AddRule(5-arg)` line 1159 | None | Same as above |
| `SetRuleEnabled` line 1108 | Add `r.FollowerAccountNames` arg | Preserve names through rebuild |
| `SetFollowerMultiplier` line 1184 | Add `r.FollowerAccountNames` arg | Preserve names through rebuild |
| `SetAtmMode` line 2809 | Add `r.FollowerAccountNames` arg | Preserve names through rebuild |
| `DtoToRule` line 4289 | Add `dto.FollowerAccountNames` arg | Pass authoritative names (covers null-account slots) |
| All existing tests | None | `AddRule(3-arg)` backward compat; all 27+ existing tests pass unchanged |
| `RuleToDto` | None | Already derives names from `FollowerAccounts[i]?.Name`; no change needed |

---

## J. CopyRule.Create CALLER INVENTORY

All six call sites confirmed by grep. Each is listed with the exact change required:

| Line | Method | Change |
|------|--------|--------|
| 1108 | `SetRuleEnabled` | Add `r.FollowerAccountNames` as 8th arg |
| 1131 | `AddRule(3-arg)` | No change (optional param, defaults to null) |
| 1159 | `AddRule(5-arg)` | No change (optional param, defaults to null) |
| 1184 | `SetFollowerMultiplier` | Add `r.FollowerAccountNames` as 8th arg |
| 2809 | `SetAtmMode` | Add `r.FollowerAccountNames` as 8th arg |
| 4289 | `DtoToRule` | Add `dto.FollowerAccountNames` as 8th arg |

**Total edits to Create() call sites**: 3 (lines 1108, 1184, 2809) + 1 (line 4289) = 4 edits.
Lines 1131 and 1159 require NO source edit (optional parameter already defaults to null).

---

## K. FILES MODIFIED (COMPLETE LIST)

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Modify (CopyRule struct, AllAccounts, LoadRules, 4 Create() call sites, new field) |
| `src/PropTraderTools/Tests/B127Tests.cs` | New file (3 xUnit [Fact] tests) |

**Prohibited**:
- No other `.cs` files touched.
- No UI files (`TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`) touched.
- No spec or protocol files touched.

---

## L. OPEN QUESTIONS / RISKS

None. All decisions resolved in this plan.

| Question | Decision |
|----------|----------|
| Cache option C1 vs C2 | **C2** chosen (ConcurrentDictionary -- minimal, lock-free, no struct mutation) |
| Warning throttle | **No throttle** -- acceptable per call frequency |
| Extract TryResolveFollower helper? | **No** -- CYC=7 is within limit; no extraction required |
| Backward compat for AddRule callers | **Automatic** -- optional param with null default; ctor derives from accounts |
| DeriveFollowerNames placement | **Private static** inside CopyRule struct -- same file section as ctor |

---

*Architecture plan complete. Status: REVIEW_PENDING.*
*Next phase: ptt-plan-reviewer reviews this document.*
*On REVIEW_PASS: ptt-architect produces 04-tickets.md (Phase 3).*
