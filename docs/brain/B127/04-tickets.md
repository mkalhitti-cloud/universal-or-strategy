# B127 Tickets
# DW-PTT-BE-FIX-01: Lazy Re-Resolve for Null Followers in AllAccounts()

**Block**: B127
**Phase**: 3 -- Ticket Generation
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Date**: 2026-08-25
**Plan Source**: `docs/brain/B127/02-architecture-plan.md` (REVIEW_PASS)

---

## Ticket Count: 1

All changes form one atomic unit. Adding `FollowerAccountNames` to `CopyRule` and
threading it through every `CopyRule.Create()` call site is a single indivisible change
-- a partial implementation would not compile. Therefore exactly one ticket is issued.

---

## T1 -- Implement Option A Lazy Re-Resolve in AllAccounts()

### Spec Requirements

- **DW-PTT-BE-FIX-01** (source: `docs/brain/B107/06-deferred-backlog.md`)
  > "When a follower account is not in Account.All at LoadRules() time, Option A would
  > re-attempt resolution lazily in AllAccounts() when the account later appears in Account.All."

### Files

| File | Change |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Modify -- struct, field, 4 call sites, AllAccounts, LoadRules, new helper |
| `src/PropTraderTools/Tests/B127Tests.cs` | New file -- 3 xUnit [Fact] tests |

---

### Implementation Steps (ordered -- follow this sequence)

#### Step 1: Add `FollowerAccountNames` field to `CopyRule` struct

**Location**: `src/PropTraderTools/CopyEngine.cs`, line 411 (after `TightenTicks` field, before the constructor comment at line 413)

Insert this field:

```csharp
// B127: original follower account names parallel to FollowerAccounts[] -- enables
// lazy re-resolve in AllAccounts() for null slots at DtoToRule/LoadRules time.
// JS-008: readonly array field on readonly struct -- reference is immutable, compliant.
// DW-PTT-BE-FIX-01.
internal readonly string[] FollowerAccountNames;
```

#### Step 2: Add `DeriveFollowerNames()` private static helper to `CopyRule` struct

**Location**: `src/PropTraderTools/CopyEngine.cs`, inside the `CopyRule` struct -- after the `Create()` factory method (after line 453, still inside the struct closing brace).

```csharp
// B127: derives follower name strings from Account[] for backward-compat callers.
// Returns empty array for null/empty input. Never returns null (JS-002 convention).
// CYC=2: null/length guard (1) + for loop (1).
// JS-021: no lock. JS-001: no throw. ASCII-only.
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

#### Step 3: Update `CopyRule` private constructor (add 8th parameter)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 415-432 (the `private CopyRule(...)` constructor)

Replace the current 7-parameter constructor with this 8-parameter constructor:

```csharp
// B8 T1: updated private constructor (adds multipliers + atmTemplates parameters)
// B10 T3: updated to include tightenTicks
// B127: updated to include followerAccountNames (8th param, DW-PTT-BE-FIX-01)
private CopyRule(
    string instrument,
    Account master,
    Account[] followers,
    bool enabled,
    int[] multipliers,
    Dictionary<string, FollowerAtmMode> atmTemplates,
    int tightenTicks,
    string[] followerAccountNames  // NEW B127: 8th param
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
    // DtoToRule supplies explicit names (covering null-account slots).
    // All other callers pass null -- names are derived from resolved Account references.
    FollowerAccountNames = followerAccountNames ?? DeriveFollowerNames(followers);
}
```

#### Step 4: Update `CopyRule.Create()` factory (add 8th optional parameter)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 436-453 (the `internal static CopyRule Create(...)` factory)

Replace the current 7-parameter factory with this 8-parameter factory:

```csharp
// B8 T1: updated factory -- new optional params preserve backward compat with all existing tests
// B10 T3: adds tightenTicks optional param (default 5)
// B127: adds followerAccountNames optional param (default null = derive from followers[])
internal static CopyRule Create(
    string instrument,
    Account master,
    Account[] followers,
    bool enabled = true,
    int[] multipliers = null,
    Dictionary<string, FollowerAtmMode> atmTemplates = null,
    int tightenTicks = 5,
    string[] followerAccountNames = null  // NEW B127: 8th optional param; null = derive in ctor
) =>
    new CopyRule(
        instrument,
        master,
        followers,
        enabled,
        multipliers,
        atmTemplates ?? new Dictionary<string, FollowerAtmMode>(),
        tightenTicks,
        followerAccountNames  // passed through; null triggers DeriveFollowerNames in ctor
    );
```

**Backward compat note**: `AddRule(3-arg)` (line 1131) and `AddRule(5-arg)` (line 1159) pass
fewer than 8 arguments and DO NOT need source edits -- the new 8th optional param defaults to
`null`, which causes the constructor to call `DeriveFollowerNames(followers)` automatically.

#### Step 5: Add `_resolvedFollowers` cache field to `CopyEngine` class

**Location**: `src/PropTraderTools/CopyEngine.cs`, line 200 (immediately after the `_rules` field on line 199)

Insert this field:

```csharp
// B127: lazy-resolve cache -- name -> Account. Populated on first successful resolve in
// AllAccounts(). Lock-free: ConcurrentDictionary TryGetValue + TryAdd (JS-021 compliant).
// Cleared on each LoadRules() call to handle account reconnect / session restart scenarios.
// readonly: ConcurrentDictionary is a reference type; .Clear() works on the instance.
private readonly ConcurrentDictionary<string, Account> _resolvedFollowers =
    new ConcurrentDictionary<string, Account>(StringComparer.Ordinal);
```

#### Step 6: Update `LoadRules()` to clear the cache (line 4361)

**Location**: `src/PropTraderTools/CopyEngine.cs`, line 4361

After the existing line:
```csharp
_rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read
```

Add immediately below it:
```csharp
_resolvedFollowers.Clear(); // B127: invalidate lazy-resolve cache on rule reload (DW-PTT-BE-FIX-01)
```

#### Step 7: Replace `AllAccounts()` implementation (lines 3374-3386)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 3374-3386

Replace the current private method with the following. Also change the access modifier
from `private` to `internal` to enable xUnit test access via `InternalsVisibleTo`:

```csharp
// B127: updated to implement Option A lazy re-resolve (DW-PTT-BE-FIX-01).
// CYC=7: rule==null(1) + for(1) + acc!=null(1) + names ternary(1) + IsNullOrEmpty(1)
//         + TryGetValue(1) + resolved!=null(1). Within JS limit of 8.
// JS-021: no lock -- ConcurrentDictionary.TryGetValue + TryAdd are lock-free.
// JS-001: no throw -- all paths yield, continue, or emit Output.Process.
// JS-002: no null values yielded -- null slots are resolved or skipped.
// ASCII-only strings in all log messages.
internal IEnumerable<Account> AllAccounts(Instrument instrument)
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
                "[PTT-COPY] INFO: follower '" + name
                    + "' resolved lazily -- now copying to this account.",
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            yield return resolved;
            continue;
        }
        NinjaTrader.Code.Output.Process(
            "[PTT-COPY] WARNING: follower '" + name
                + "' not found in Account.All"
                + " -- account not connected yet; will retry on next dispatch.",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
    }
}
```

#### Step 8: Update `SetRuleEnabled()` CopyRule.Create call (line 1108)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 1108-1116

Add `r.FollowerAccountNames` as the 8th argument:

```csharp
? CopyRule.Create(
    r.Instrument,
    r.MasterAccount,
    r.FollowerAccounts,
    enabled,
    r.FollowerMultipliers,
    r.FollowerAtmTemplates,
    r.TightenTicks,
    r.FollowerAccountNames  // B127: preserve names through enabled/disabled rebuild
)
```

#### Step 9: Update `SetFollowerMultiplier()` CopyRule.Create call (lines 1184-1192)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 1184-1192

Add `r.FollowerAccountNames` as the 8th argument:

```csharp
CopyRule.Create(
    r.Instrument,
    r.MasterAccount,
    r.FollowerAccounts,
    r.Enabled,
    newMults,
    r.FollowerAtmTemplates,
    r.TightenTicks,
    r.FollowerAccountNames  // B127: preserve names through multiplier rebuild
)
```

#### Step 10: Update `SetAtmMode()` CopyRule.Create call (lines 2809-2817)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 2809-2817

Add `r.FollowerAccountNames` as the 8th argument:

```csharp
CopyRule.Create(
    r.Instrument,
    r.MasterAccount,
    r.FollowerAccounts,
    r.Enabled,
    r.FollowerMultipliers,
    newMap,
    r.TightenTicks,
    r.FollowerAccountNames  // B127: preserve names through ATM mode rebuild
)
```

#### Step 11: Update `DtoToRule()` CopyRule.Create call (lines 4289-4297)

**Location**: `src/PropTraderTools/CopyEngine.cs`, lines 4289-4297

Add `dto.FollowerAccountNames` as the 8th argument:

```csharp
return CopyRule.Create(
    dto.InstrumentName,
    master,
    followers,
    dto.IsEnabled,
    multipliers,
    atmMap,
    tightenTicks,
    dto.FollowerAccountNames  // B127: preserve original names (covers null-account slots)
);
```

#### Step 12: Create `src/PropTraderTools/Tests/B127Tests.cs` (new file)

Create the file `src/PropTraderTools/Tests/B127Tests.cs` with the following 3 xUnit
`[Fact]` tests. Before writing the tests, check existing B-series test files (e.g.,
`B126Tests.cs`, `B124Tests.cs`) for the established test harness pattern
(how CopyEngine is instantiated, how Account stubs are constructed, how internal
methods are reached via `InternalsVisibleTo`).

The assembly-level attribute `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]`
is already present at line 46 of `CopyEngine.cs` -- no changes needed to activate
test access to `internal` members.

**Test 1** (`T1`):

```csharp
[Fact]
public void T1_AllAccounts_ReturnsResolvedAccount_WhenAccountAvailableAtLoadTime()
{
    // Arrange
    // Build a CopyEngine. Create a rule where followers[0] is a non-null Account.
    // The account must be non-null to exercise the fast path (acc != null).

    // Act
    // Call AllAccounts(instrument).

    // Assert
    // The enumeration includes the follower account (non-null path executed).
    // No exception thrown.
}
```

**Test 2** (`T2`):

```csharp
[Fact]
public void T2_AllAccounts_LazyResolves_WhenAccountAppearsAfterLoad()
{
    // Arrange
    // Create a CopyRule directly (via CopyRule.Create) where:
    //   followers = new Account[]{ null }              -- null slot simulates not-found at load
    //   followerAccountNames = new[]{ "SimAccount" }  -- name preserved
    // Inject the rule into the engine's _rules bag.
    // Arrange Account.All (or the FindFollowerAccount seam) to return "SimAccount" when queried.

    // Act
    // Call AllAccounts(instrument) and materialise to a list.

    // Assert
    // The list contains the lazily resolved "SimAccount" Account.
    // The INFO message was emitted (verify via Output.Process capture or skip if not mockable).
}
```

**Test 3** (`T3`):

```csharp
[Fact]
public void T3_AllAccounts_EmitsWarningAndSkips_WhenAccountNotResolvable()
{
    // Arrange
    // Create a CopyRule with followers = new Account[]{ null }
    //   and followerAccountNames = new[]{ "MissingAccount" }.
    // Arrange Account.All to NOT contain "MissingAccount".

    // Act
    // Call AllAccounts(instrument) and materialise to a list.

    // Assert
    // Only the master account is in the result -- follower was not yielded.
    // WARNING message was emitted.
    // No exception thrown.
}
```

**Important note on test seam**: `AllAccounts()` is now `internal` (Step 7). The
`InternalsVisibleTo` attribute at line 46 makes it callable from the test project.
`FindFollowerAccount()` is `private static` and iterates `Account.All` (an NT8 API).
The engineer must choose one of:
  - (a) Wrap `FindFollowerAccount` calls behind an injectable delegate on `CopyEngine` (minimal seam).
  - (b) Use the existing test pattern from adjacent B-series tests if a seam already exists.
  - (c) Test the observable effect only (accounts yielded + messages) without stubbing `Account.All`.

Check `src/PropTraderTools/Tests/B126Tests.cs` and `B124Tests.cs` before deciding.
The architect decision is: match the established seam pattern -- do not invent a new one.

---

### CopyRule.Create Caller Inventory (confirmed by grep)

The engineer MUST update exactly these 4 call sites (the other 2 require no edit):

| Line | Method | Edit Required |
|------|--------|---------------|
| 1108 | `SetRuleEnabled` | Add `r.FollowerAccountNames` as 8th arg (Step 8) |
| 1131 | `AddRule(3-arg)` | **NO EDIT** -- 8th param defaults to null, ctor derives names |
| 1159 | `AddRule(5-arg)` | **NO EDIT** -- 8th param defaults to null, ctor derives names |
| 1184 | `SetFollowerMultiplier` | Add `r.FollowerAccountNames` as 8th arg (Step 9) |
| 2809 | `SetAtmMode` | Add `r.FollowerAccountNames` as 8th arg (Step 10) |
| 4289 | `DtoToRule` | Add `dto.FollowerAccountNames` as 8th arg (Step 11) |

---

### Warning Message Specification

| Situation | Message prefix | Output target |
|-----------|----------------|---------------|
| Null at load time (existing, unchanged) | `[PTT-COPY] WARNING: follower 'X' not found in Account.All at load time -- will be skipped until rule is re-applied (uncheck + re-check in panel).` | OutputTab1 |
| Lazy resolve success (new) | `[PTT-COPY] INFO: follower 'X' resolved lazily -- now copying to this account.` | OutputTab1 |
| Lazy resolve fail (new) | `[PTT-COPY] WARNING: follower 'X' not found in Account.All -- account not connected yet; will retry on next dispatch.` | OutputTab1 |

All strings are ASCII-only (no Unicode, no emoji). No throttle -- `AllAccounts()` fires
per trade event, not per tick.

---

### Acceptance Criteria

- [ ] `CopyRule.FollowerAccountNames` field added (`internal readonly string[]`)
- [ ] `CopyRule` private constructor has 8th param `string[] followerAccountNames`; body assigns `FollowerAccountNames = followerAccountNames ?? DeriveFollowerNames(followers)`
- [ ] `CopyRule.Create()` factory has 8th optional param `string[] followerAccountNames = null`; passes it through to constructor
- [ ] `DeriveFollowerNames(Account[])` private static helper added inside `CopyRule` struct; returns `Array.Empty<string>()` for null/empty input
- [ ] `_resolvedFollowers` field added to `CopyEngine` (`private readonly ConcurrentDictionary<string, Account>`)
- [ ] `LoadRules()` calls `_resolvedFollowers.Clear()` immediately after `_rules = new ConcurrentBag<CopyRule>()`
- [ ] `AllAccounts()` changed to `internal` and implements lazy re-resolve with CYC <= 8
- [ ] `DtoToRule()` passes `dto.FollowerAccountNames` as 8th arg to `CopyRule.Create()`
- [ ] `SetRuleEnabled()` passes `r.FollowerAccountNames` as 8th arg to `CopyRule.Create()`
- [ ] `SetFollowerMultiplier()` passes `r.FollowerAccountNames` as 8th arg to `CopyRule.Create()`
- [ ] `SetAtmMode()` passes `r.FollowerAccountNames` as 8th arg to `CopyRule.Create()`
- [ ] `AddRule(3-arg)` and `AddRule(5-arg)` compile WITHOUT source edits (backward compat)
- [ ] `src/PropTraderTools/Tests/B127Tests.cs` created with 3 passing xUnit `[Fact]` tests
- [ ] All 7 scans pass to zero (see checklist below)

---

### 7-Scan Checklist (MANDATORY -- run to zero before reporting T1 complete)

```
SCAN 1 -- lock() audit (JS-021 P0):
  Select-String -Pattern "lock\(" src/PropTraderTools/CopyEngine.cs
  Required result: 0 matches in modified code.
  Any lock( found in CopyEngine.cs = HARD FAIL. Stop and resolve.

SCAN 2 -- async void audit (JS-033 P0):
  Select-String -Pattern "async void " src/PropTraderTools/CopyEngine.cs
  Required result: 0 matches in modified code.

SCAN 3 -- return null audit (JS-002 P0):
  Select-String -Pattern "return null" src/PropTraderTools/CopyEngine.cs
  Count any NEW return null inside AllAccounts() or DeriveFollowerNames().
  Required: 0 new occurrences. Pre-existing return null in FindFollowerAccount() is
  grandfathered and must not be changed.

SCAN 4 -- CYC audit of AllAccounts():
  Manually count decision points in the new AllAccounts() body:
    rule == null          (1)
    for loop              (1)
    acc != null           (1)
    names ternary         (1)
    IsNullOrEmpty(name)   (1)
    TryGetValue(...)      (1)
    resolved != null      (1)
    TOTAL = 7 (<= 8). PASS.
  Document the count in ticket-1-completion.md.

SCAN 5 -- xUnit-only audit (testing mandate):
  Select-String -Pattern "using Xunit" src/PropTraderTools/Tests/B127Tests.cs
  Required: xUnit namespace present.
  Select-String -Pattern "using NUnit|using Microsoft.VisualStudio.TestTools" src/PropTraderTools/Tests/B127Tests.cs
  Required: 0 matches.

SCAN 6 -- ASCII-only audit (JS-077):
  Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Select-String -Pattern "[^\x00-\x7F]" src/PropTraderTools/Tests/B127Tests.cs
  Required: 0 matches in modified or new code.

SCAN 7 -- build audit:
  dotnet build src/PropTraderTools/PropTraderTools.csproj
  Required: 0 errors. 0 new warnings (pre-existing warnings are acceptable if unchanged).
  A clean build is the final gate before reporting T1 complete.
```

---

### Ticket Completion Artifact

When the engineer finishes implementation, write:
`docs/brain/B127/ticket-1-completion.md`

That file must include:
- Summary of each step completed (Steps 1-12)
- CYC count for `AllAccounts()` (must show 7)
- 7-scan results (all pass/zero)
- Build output (0 errors)
- Note on test seam approach chosen (option a, b, or c from Step 12)

---

*Ticket generation complete. Status: TICKETS_COMPLETE.*
*Next phase: ptt-engineer implements from this ticket.*
*After implementation: ptt-verifier reviews src vs this ticket.*
