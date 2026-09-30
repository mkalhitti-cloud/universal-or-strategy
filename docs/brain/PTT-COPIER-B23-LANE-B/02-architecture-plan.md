# PTT-COPIER-B23-LANE-B — Architecture Plan
# Block:  PTT-COPIER-B23
# Lane:   B
# Defect: DW-B22-ADDRULE-ACCUMULATE-01 (P1)
# Status: REVIEW_PENDING
# Date:   2026-07-16

---

## §1  Defect Summary and Root Cause

### Defect ID
`DW-B22-ADDRULE-ACCUMULATE-01` (P1)

### Symptom
After restarting NT8, clicking Apply Rule fires copies to ALL accounts that were ever saved
as followers in a previous session — not just the ones currently selected in the dropdown.

### Root Cause (confirmed from code)
`AddRule()` 5-arg overload (`CopyEngine.cs` lines 314–322) calls `_rules.Add()` unconditionally:

```csharp
internal void AddRule(
    string instrument, Account master, Account[] followers,
    int[] multipliers, Dictionary<string, FollowerAtmMode> atmMap)
{
    _rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
}
```

On startup, `LoadRules()` (line 1561) already populated `_rules` with the persisted PA rule.
`OnApplyRule` then calls `AddRule()` — adding a SECOND rule for the same `(instrument, leader)`.

`Gate 2` in `OnOrderUpdate` (line 385–392) matches on `break` — **first-loaded rule wins**.
The persisted PA rule was loaded first → it fires → copies go to stale PA accounts.
The newly-applied Sim102 rule is in the bag but never reached.

### Evidence
- Director selected only Sim102; copies fired to PA-APEX-03, 04, 06 AND Sim102
- PA accounts received `qty=2` (ATR-sized, from engine state at that time)
- Sim102 received `qty=12` (leader qty — different rule)
- Two rules with same `(MES SEP26, Sim101)` key confirmed by logic trace

---

## §2  Fix Design — Replace-Not-Append in AddRule 5-arg Overload

### Strategy
Use the existing `ConcurrentBag` snapshot-rebuild pattern (already used by `SetFollowerMultiplier`
at lines 330–344) to remove any existing rule matching `(instrument, master.Name)` before
adding the new one. This is a replace-not-append semantic.

### Change Site
`CopyEngine.cs` — `AddRule()` 5-arg overload (lines 314–322)

### Before
```csharp
internal void AddRule(
    string instrument,
    Account master,
    Account[] followers,
    int[] multipliers,
    Dictionary<string, FollowerAtmMode> atmMap)
{
    _rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
}
```

### After
```csharp
internal void AddRule(
    string instrument,
    Account master,
    Account[] followers,
    int[] multipliers,
    Dictionary<string, FollowerAtmMode> atmMap)
{
    // Replace-not-append: remove any existing rule for same (instrument, leader) pair.
    // ConcurrentBag rebuild pattern -- no lock (JS-021). Same pattern as SetFollowerMultiplier.
    var snapshot = new List<CopyRule>(_rules);
    _rules = new ConcurrentBag<CopyRule>();
    foreach (var r in snapshot)
    {
        if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name)
            continue;  // drop stale rule for this (instrument, leader) pair
        _rules.Add(r);
    }
    _rules.Add(CopyRule.Create(instrument, master, followers, true, multipliers, atmMap));
}
```

### 3-arg Overload
The 3-arg overload (`AddRule(string, Account, Account[])` — line 307) is used only by tests
(backward compat). It does NOT need the replace-not-append logic because tests create fresh
engine instances. Leave it unchanged to preserve all 122 existing tests.

### CYC Impact
`AddRule` 5-arg: 1 → 4 (loop + continue branch + null guard on master?.Name). Still ≤ 8.

### JS Compliance
- JS-021: no `lock()` — ConcurrentBag rebuild is lock-free (same as SetFollowerMultiplier)
- JS-001: no throw — pure collection manipulation
- JS-002: no return null — void method

### New [Fact] Required
`AddRule_Replace_WhenSameInstrumentAndLeader` — verifies that calling AddRule twice with
the same instrument+leader replaces rather than accumulates. Assert `_rules` count = 1
after two AddRule calls. Use reflection to read `_rules` field.

---

## §3  Write-Set

| File | Path |
|------|------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

**DO NOT TOUCH**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`, any `.md` files.
