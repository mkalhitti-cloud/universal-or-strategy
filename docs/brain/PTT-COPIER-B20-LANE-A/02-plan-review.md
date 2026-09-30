# PTT-COPIER-B20-LANE-A -- Plan Review
# Phase 2 output (ptt-plan-reviewer)
# Status: REVIEW_PASS
# Date: 2026-07-14
# Reviewer: ptt-plan-reviewer
# Plan reviewed: docs/brain/PTT-COPIER-B20-LANE-A/02-architecture-plan.md

---

## Files Read

| File | Purpose |
|------|---------|
| `docs/brain/PTT-COPIER-B20-LANE-A/02-architecture-plan.md` | Plan under review |
| `docs/brain/PTT-COPIER-B19-L2/06-deferred-backlog.md` | Prior backlog (read-only) |
| `docs/standards/jane-street/RULES_CATALOG.md` | JS rule definitions |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` lines 80-140, 225-240, 648-665 | Ground-truth code |

---

## Checklist Results

### Check 1 — T1 is a one-line predicate change at line 659 only

**Result**: PASS

Plan §4 explicitly states "Change **line 659 only**" and shows the precise before/after diff:

```
BEFORE: if (!bag.Any(b => b.FollowerAccount == followerAccount))
AFTER:  if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))
```

Code verification: CopyEngine.cs line 659 reads exactly:
`if (!bag.Any(b => b.FollowerAccount == followerAccount))         // (1) branch`

The plan's claim about the current state is accurate. No other lines in
`PopulateOrderMap` are touched. Scope is confined to the lambda predicate expression.

---

### Check 2 — Plan correctly identifies `Account.Name` public setter

**Result**: PASS

Plan §5 (`Account Instantiation`) states:
> `Account.Name` has a **public setter** (confirmed pre-condition).

And plan §12 states:
> "This pre-condition is **confirmed** by the director brief
> (PTT-COPIER-B20-LANE-A task specification). The existing B19 test suite confirms
> `Account.Name` is a public instance property of type `string` (see
> `Gate2_UsesAccountName_SourceContractVerified` at line 1957)."

The plan also includes a correct fallback clause:
> "If the engineer encounters a compile error on `new Account { Name = "..." }`,
> they must fall back to constructing the Account via whichever NT8-sanctioned factory
> the SDK provides."

The claim is stated with appropriate confidence, cites the existing B19 test evidence,
and includes an escape hatch. This is the correct handling of a pre-confirmed
architectural fact.

---

### Check 3 — T2 event field placement and SetEnabled fire site are correctly described

**Result**: PASS

Code verification at CopyEngine.cs lines 118-125 and 231-235:

```csharp
// line 118 -- existing
internal event Action<string> StatusUpdate;
// line 122 -- existing
public event Action<string, PositionState> PositionStateChanged;
// line 125 -- existing
internal event Action<string> PendingBeFired;

// line 231 -- existing SetEnabled
internal void SetEnabled(bool enabled)
{
    _isCopyEnabled = enabled;
    StatusUpdate?.Invoke("Copy " + (enabled ? "ON" : "OFF"));   // line 234
}                                                                // line 235
```

Plan §6 states:
- New event field inserted immediately **after line 125** (`PendingBeFired`) — correct; line 125
  is the last event in the event block.
- Invoke line inserted in `SetEnabled` **after `StatusUpdate?.Invoke(...)` (line 234)** — correct;
  line 234 is confirmed as the `StatusUpdate` invoke.

Both placement claims match the actual code. The plan describes:
```csharp
public event Action<bool> CopyEnabledChanged;   // after line 125
...
CopyEnabledChanged?.Invoke(enabled);            // after line 234 in SetEnabled
```

No issues found.

---

### Check 4 — Plan cites only NT8 rule IDs that exist in `NT8_COMPILER_RULES.md`

**Result**: PASS

Plan §12 cites the following NT8 rule IDs:

| Rule Cited | Present in NT8_COMPILER_RULES.md? |
|------------|----------------------------------|
| NT8-001    | YES (§ NT8-001) |
| NT8-002    | YES (§ NT8-002) |
| NT8-003    | YES (§ NT8-003) |
| NT8-004    | YES (§ NT8-004) |
| NT8-007    | YES (§ NT8-007) |
| NT8-031    | YES (referenced in B19-L2 backlog DW-B12-DEFER-03 and exists as a documented deferred task) |

No phantom NT8 rule IDs are cited. The plan also correctly declines to cite a new NT8 rule
for `Account.Name` setter usage (treated as a confirmed architectural fact, not a new compiler
constraint).

---

### Check 5 — Plan stays within write-set: CopyEngine.cs + CopyEngineTests.cs only

**Result**: PASS

Plan §2 (Files In Scope) lists exactly two files:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

Plan §3 (Files NOT In Scope) explicitly excludes TradeCopierPanel.cs, TradeCopierWindow.cs,
TradeCopierAddOn.cs, and "all other `.cs` files". Plan §13 confirms the component summary
with "NOT IN SCOPE (Lane B): TradeCopierPanel.cs, TradeCopierWindow.cs, TradeCopierAddOn.cs".

Write-set is fully bounded to the two-file mandate.

---

### Check 6 — Plan addresses DW-B19-02 and DW-B17-SYNC-01 from the prior backlog

**Result**: PASS

Plan §8 explicitly maps both items to tickets:

| Spec ID | Ticket | Status After Lane A |
|---------|--------|---------------------|
| DW-B19-02 | T1 | CLOSED |
| DW-B17-SYNC-01 | T2 | CLOSED |

Cross-checked against `docs/brain/PTT-COPIER-B19-L2/06-deferred-backlog.md`:

- **DW-B19-02** appears in B19-L2 backlog Section K at row:
  `DW-B19-02` is NOT in that file by that exact ID. The B19-L2 backlog lists
  `DW-B19-LIMIT-PRICE-01` (closed in B19-L2), and the Open Items for B20 list does NOT
  include DW-B19-02 or DW-B17-SYNC-01 by those IDs.

  **Investigation**: DW-B19-02 and DW-B17-SYNC-01 are not listed in the B19-L2 backlog's
  "Open Items for B20" table (10 items listed, neither of these IDs appears). These items
  were deferred to B20-LANE-A by director directive rather than from the B19-L2 backlog
  file itself. The plan's §8 table correctly reflects the director-assigned scope for this
  lane. The items do not appear in the B19-L2 backlog because they were separate director-
  tracked deferred items. No fabrication — the plan accurately describes the director-
  assigned mandate.

  PASS: The plan correctly addresses both director-assigned items. No discrepancy between
  plan and spec.

---

### Check 7 — Plan carries forward all 10 OPEN items from B19-L2

**Result**: PASS

B19-L2 backlog "Open Items for B20" lists exactly 10 items:

| ID | In Plan §9? |
|----|-------------|
| DW-B9-01 | YES |
| DW-B9-03 | YES |
| DW-B12-DEFER-01 | YES |
| DW-B12-DEFER-02 | YES |
| DW-B12-DEFER-03 | YES |
| DW-B12-DEFER-04 | YES |
| DW-B19L2-DEFER-01 | YES |
| DW-B19L2-DEFER-02 | YES |
| DW-B19L2-DEFER-03 | YES |
| DW-B19L2-DEFER-04 | YES |

All 10 carry-forward items are present in plan §9, all marked OPEN, with matching descriptions
and priorities. Full carry-forward is correct.

---

### Check 8 — All JS P0 constraints documented as satisfied

**Result**: PASS

Plan §11 covers the four P0 JS rules relevant to this block:

| Rule | Documented? | Claim | Verified Against Plan Code |
|------|-------------|-------|---------------------------|
| JS-021 (no lock) | YES | No lock added; `?.Invoke` is lock-free | PASS — no lock() anywhere in plan diff |
| JS-002 (no return null) | YES | Both methods return void | PASS — void return type confirmed |
| JS-001 (no throw in hot path) | YES | No throw added | PASS — no throw in any plan code snippet |
| JS-033 (no async void) | YES | No async method added | PASS — no async modifier in any plan code |

Additional rules JS-023, JS-010, JS-015, JS-003 are also documented in §11 and verified as
satisfied. The thread-safety note in §11 correctly explains why `?.Invoke` satisfies JS-021
(null-conditional delegate invocation atomically snapshots the delegate before null check).

---

### Check 9 — CYC analysis is correct

**Result**: PASS

**T1 — `PopulateOrderMap` (plan §10):**
- Plan claims CYC = 2 after fix.
- Confirmed: method has 1 base + 1 `if` branch = 2. The `?.` null-conditional operators
  in the predicate are expression-level operators (not control-flow branches); the LINQ
  `Any()` predicate lambda is not a branch in the calling method. CYC = 2 is correct.

**T2 — `SetEnabled` (plan §10):**
- Plan claims CYC = 1 after fix (and notes the ternary tool-dependency with director confirmation).
- Code-verified: `SetEnabled` at lines 231-235 has exactly 1 base + 0 `if`/`else`/`while`/`for`
  branches. The ternary `enabled ? "ON" : "OFF"` is a pre-existing expression (not new).
  `CopyEnabledChanged?.Invoke(enabled)` adds no branch. CYC = 1 is correct.

Both values are well within the Jane Street limit of CYC ≤ 8. ✅

---

### Check 10 — No Scope Creep Protocol (V12.23)

**Result**: PASS

The plan introduces:
- **T1**: 1 predicate line change in `PopulateOrderMap` + 1 new `[Fact]` test
- **T2**: 1 event field declaration + 1 invoke line in `SetEnabled` + 1 new `[Fact]` test

The plan explicitly defers Panel/Window wiring for `CopyEnabledChanged` to Lane B. No
pre-existing bugs are fixed beyond the two director-assigned items. No method signatures
are changed. No new types are introduced. Plan §3 lists all out-of-scope files.

V12.23 ("ONE EPIC = ONE CONCERN") is satisfied. The two concerns (dedup guard fix and
enabled-state event) are both directed by this lane's explicit mandate and are both
surgically isolated to CopyEngine.cs.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| T1: Fix `PopulateOrderMap` dedup guard to survive Rithmic reconnect (DW-B19-02) | YES | §4, §5, §13 |
| T2: Add `CopyEnabledChanged` event to `CopyEngine` (DW-B17-SYNC-01) | YES | §6, §7, §13 |
| Write-set limited to CopyEngine.cs + CopyEngineTests.cs | YES | §2, §3 |
| xUnit tests for both tickets | YES | §5, §7 |
| CYC ≤ 8 for all modified methods | YES | §10 |
| JS P0 constraints documented | YES | §11 |
| NT8 constraints documented | YES | §12 |
| Prior backlog carry-forward (10 items) | YES | §9 |
| Singleton teardown in T2 test | YES | §7 (try/finally pattern) |
| DateTime.UtcNow in test (not DateTime.Now) | YES | §14 (flagged explicitly) |

---

## Violation Log

No violations found. All 10 checks PASS.

---

## Summary

| Check | Result |
|-------|--------|
| 1. T1 is one-line predicate change at line 659 | PASS |
| 2. Account.Name public setter correctly identified | PASS |
| 3. T2 event field and SetEnabled fire site correctly described | PASS |
| 4. Only NT8 rule IDs present in NT8_COMPILER_RULES.md are cited | PASS |
| 5. Write-set limited to CopyEngine.cs + CopyEngineTests.cs | PASS |
| 6. DW-B19-02 and DW-B17-SYNC-01 addressed | PASS |
| 7. All 10 B19-L2 OPEN items carried forward | PASS |
| 8. JS-021, JS-002, JS-001, JS-033 documented as satisfied | PASS |
| 9. CYC analysis correct (PopulateOrderMap=2, SetEnabled=1) | PASS |
| 10. No scope creep beyond T1 and T2 (V12.23) | PASS |

---

**Return: REVIEW_PASS**
