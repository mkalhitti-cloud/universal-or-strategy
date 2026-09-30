# PTT-COPIER-B23-LANE-B — Plan Review
# Block:    PTT-COPIER-B23
# Lane:     B
# Defect:   DW-B22-ADDRULE-ACCUMULATE-01 (P1)
# Reviewer: ptt-plan-reviewer
# Result:   REVIEW_PASS
# Date:     2026-07-16

---

## Review Summary

All 6 checklist items PASS. No violations found. Plan is approved for Phase 3 (ticket generation).

---

## Per-Check Results

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1 | ConcurrentBag rebuild pattern matches `SetFollowerMultiplier` | **PASS** | See below |
| 2 | Name equality `r.MasterAccount?.Name == master?.Name` (not reference) | **PASS** | See below |
| 3 | 3-arg `AddRule(string, Account, Account[])` overload preserved unchanged | **PASS** | See below |
| 4 | CYC of updated 5-arg `AddRule` = 4 | **PASS** | See below |
| 5 | JS P0 compliance: no `lock()`, no `return null`, no `async void` | **PASS** | See below |
| 6 | Write-set = `CopyEngine.cs` + `CopyEngineTests.cs` only | **PASS** | See below |

---

## Check Detail

### Check 1 — ConcurrentBag Rebuild Pattern (PASS)

Reality in `SetFollowerMultiplier` (lines 330–344 of `CopyEngine.cs`):
```csharp
var snapshot = new List<CopyRule>(_rules);
_rules = new ConcurrentBag<CopyRule>();
foreach (var r in snapshot)
{
    // conditional: _rules.Add(r) or rebuild-and-add
}
```

Plan's "After" block (§2):
```csharp
var snapshot = new List<CopyRule>(_rules);
_rules = new ConcurrentBag<CopyRule>();
foreach (var r in snapshot)
{
    if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name)
        continue;
    _rules.Add(r);
}
_rules.Add(CopyRule.Create(...));
```

Structural match confirmed: snapshot → new bag → foreach → conditional skip → final unconditional Add.
The only structural difference is a `continue` guard rather than a rebuild call — the bag-replace
frame is identical to the reference pattern.

---

### Check 2 — Name Equality, Not Reference Equality (PASS)

Plan §2 "After" line:
```csharp
if (r.Instrument == instrument && r.MasterAccount?.Name == master?.Name)
```

Both sides use `.Name` (string comparison). Reference equality (`r.MasterAccount == master`) is
absent. The null-safe `?.Name` operator handles the case where either Account is null without
throwing — matching JS-002 (no null return) and JS-001 (no throw). PASS.

---

### Check 3 — 3-arg Overload Preserved Unchanged (PASS)

Reality (lines 307–310 of `CopyEngine.cs`):
```csharp
internal void AddRule(string instrument, Account master, Account[] followers)
{
    _rules.Add(CopyRule.Create(instrument, master, followers));
}
```

Plan §2 "3-arg Overload":
> "Leave it unchanged to preserve all 122 existing tests."

Plan's "After" block is scoped only to the 5-arg overload. No edit to the 3-arg signature, body,
or comments is proposed. PASS.

---

### Check 4 — CYC of 5-arg AddRule = 4 (PASS)

Decision points in the plan's "After" body:
| # | Branch | +1 |
|---|--------|----|
| Base | method entry | 1 |
| `foreach` | loop iteration | +1 |
| `if (r.Instrument == instrument ...)` | guard condition | +1 |
| `&&` short-circuit | `r.MasterAccount?.Name == master?.Name` | +1 |

**Total CYC = 4.** Plan §2 "CYC Impact" states `1 → 4 (loop + continue branch + null guard)`.
Confirmed correct. 4 ≤ 8 (Jane Street strict standard). PASS.

---

### Check 5 — JS P0 Compliance (PASS)

Scanning plan "After" code block and §2 "JS Compliance":

| Rule | Pattern | Status |
|------|---------|--------|
| JS-021 | `lock(` anywhere | ABSENT — ConcurrentBag rebuild is lock-free |
| JS-001 | `throw new ...Exception` in hot path | ABSENT — pure collection manipulation |
| JS-002 | `return null` | ABSENT — method is `void` |
| JS-033 | `async void` | ABSENT — synchronous method |

Plan explicitly documents JS-021, JS-001, JS-002 compliance under "JS Compliance". PASS.

---

### Check 6 — Write-Set Correct (PASS)

Plan §3 Write-Set:

| File | Path |
|------|------|
| `CopyEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

Explicit DO-NOT-TOUCH list: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
`AtrSizingEngine.cs`, any `.md` files. Exactly two `.cs` files in scope. PASS.

---

## Violations Found

None.

---

## Gate Decision

**REVIEW_PASS**

Phase 3 (ticket generation) is unlocked.
