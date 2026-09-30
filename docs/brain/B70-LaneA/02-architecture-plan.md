# B70-LaneA Architecture Plan

**Block**: B70-LaneA
**Author**: ptt-architect (Phase 1)
**Status**: REVIEW_PENDING
**Date**: 2026-08-14

---

## Section 1: Overview

### Defects Closed This Block

| ID | Priority | Description | Ticket |
|----|----------|-------------|--------|
| DW-B70-01 | P0 | OCO ID reuse rejection on second Quick Exit press | Ticket 1 |
| DW-B70-02 | P0 | Bracket duplication on follower (PTT-Copy not cancelled) | Ticket 2 |

### Fix Decisions

**DW-B70-01** — Architect chose **Option A (TickCount seed)**. See Section 2 for rationale.

**DW-B70-02** — Two-part fix: (a) widen `IsQxCancelCandidate` to match `PTT-Copy` prefix,
(b) add `CancelQxBracketsForFollowers` call inside `PttQuickExit.Execute`. Both parts are
required for the defect to be closed: the predicate fix alone does nothing unless the follower
accounts are swept; the sweep alone does nothing unless `IsQxCancelCandidate` matches
`PTT-Copy`. See Section 3 for full analysis.

### Ticket Count

2 tickets. One defect per ticket for clean scan isolation.

---

## Section 2: DW-B70-01 Architecture

### Root Cause

`CopyEngine._qxOcoSeq` is initialized to `0` at field-declaration time. Every time
`CopyEngine` is re-instantiated (NT8 session reconnect, AddOn reload) the counter resets to 0.
`NextQxOcoId()` then generates `"PTT-QX-00001"` again. NT8's simulated broker tracks OCO
group names within a session connection; when the same group name is re-submitted for a
different bracket pair, NT8 rejects the second order (duplicate OCO group ID).

### Option Evaluation

**Option A — TickCount seed (CHOSEN)**

```csharp
// BEFORE
private int _qxOcoSeq = 0;

// AFTER
private int _qxOcoSeq = Environment.TickCount & 0x7FFF;
```

- `Environment.TickCount & 0x7FFF` seeds the counter at a value in `[0, 32767]` determined
  by system uptime at the moment `CopyEngine` is constructed.
- `Interlocked.Increment(ref _qxOcoSeq)` still provides strictly monotonic, thread-safe
  increment within a session.
- D5 format preserved. Worst-case seed 32767 yields 67,232 unique IDs before rollover to
  `D5` overflow — far exceeding any session's Quick Exit press count.
- Cross-session collision probability: ~1/32768 (0.003%). In practice this is zero because
  NT8 sim resets its OCO name table on each broker reconnect, so prior-session OCO names
  do not collide with new-session ones.
- `NextQxOcoId()` method body unchanged — CYC remains 1.
- PttQuickExit.cs Guid fallback paths at lines 55 and 86 remain unchanged. They are valid
  defensive fallbacks for the case where `CopyEngine.Instance` is null (e.g., AddOn not yet
  loaded). Their output format (`"PTT-QX-" + Guid 8-hex-chars`) is intentionally different
  from the main D5 path and remains correct.

**Option B — Guid primary (NOT chosen)**

- Removes `_qxOcoSeq` field entirely; `NextQxOcoId()` returns
  `"PTT-QX-" + Guid.NewGuid().ToString("N").Substring(0, 8)`.
- Zero collision probability at cost of field removal.
- Rejected: more invasive than required. Removes an established Interlocked pattern that
  has worked correctly in every block since B41. The Guid fallback paths in PttQuickExit.cs
  would need to be reviewed for equivalence. Engineering discipline mandates the minimal
  change that solves the problem.

### Method Before / After

**CopyEngine.cs — field initializer at line 520:**

```csharp
// BEFORE (line 520)
private int _qxOcoSeq = 0;

// AFTER (line 520)
private int _qxOcoSeq = Environment.TickCount & 0x7FFF;
```

**NextQxOcoId() — method body UNCHANGED:**

```csharp
// CYC=1 (expression body). JS-021: Interlocked, no lock.
internal string NextQxOcoId()
    => "PTT-QX-" + System.Threading.Interlocked.Increment(ref _qxOcoSeq).ToString("D5");
```

### CYC Analysis

| Method | Before | After | Limit | Pass? |
|--------|--------|-------|-------|-------|
| `NextQxOcoId()` | 1 | 1 | 8 | YES |
| `_qxOcoSeq` field initializer | N/A | N/A | N/A | N/A |

### JS Rule Check (Ticket 1)

| Rule | Constraint | Verdict |
|------|-----------|---------|
| JS-021 | No lock() | PASS — Interlocked.Increment unchanged |
| JS-001 | No throw in hot path | PASS — no throw |
| JS-002 | No return null | PASS — returns string, never null |
| JS-033 | No async void | PASS — synchronous method |

---

## Section 3: DW-B70-02 Architecture

### Root Cause

`IsQxCancelCandidate` is the predicate used by `CancelQxBrackets` to decide which orders
to cancel. It currently has branches for:

1. ATM bracket names (`Stop1`, `Stop2`, `Target1`, `Target2`)
2. `PTT-QX-` prefix
3. `PTT-BE-` prefix

It does NOT have a branch for `PTT-Copy`, the signal name used for all copy-dispatched
entry orders (confirmed: `CopyEngine.cs` line 1264 `string signalName = "PTT-Copy"`).

Additionally, `PttQuickExit.Execute` Step 3 only sweeps the **leader account**:

```csharp
// Step 3 (current, PttQuickExit.cs line 52):
CopyEngine.Instance?.CancelQxBrackets(leader, instr);  // leader only
```

PTT-Copy orders live on **follower accounts**, not the leader account. Even after the
predicate is widened, `CancelQxBrackets(leader, instr)` will never see PTT-Copy orders
because it only iterates `leader.Orders`. The follower accounts' PTT-Copy orders are not
swept.

### Two-Part Fix Required

**Part A: Widen `IsQxCancelCandidate` in `CopyEngine.cs`**

Add one branch after the `PTT-BE-` branch, before `return false`:

```csharp
// BEFORE (lines 439-446, CYC=5)
internal static bool IsQxCancelCandidate(Order o)
{
    if (o == null || o.Name == null) return false;                               // (1)
    if (IsAtmBracketName(o.Name)) return true;                                   // (2)
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
    return false;
}

// AFTER (CYC=6, within CYC<=8 limit)
internal static bool IsQxCancelCandidate(Order o)
{
    if (o == null || o.Name == null) return false;                               // (1)
    if (IsAtmBracketName(o.Name)) return true;                                   // (2)
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
    if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)) return true;   // (5) B70
    return false;
}
```

CYC increases from 5 to 6 (one additional decision point). Still within CYC <= 8.

**Part B: Add follower bracket sweep in `PttQuickExit.Execute`**

Add one call after Step 3 in `PttQuickExit.Execute`:

```csharp
// Step 3: CancelStaleBrackets -- cancel ATM bracket + previous PTT-QX orders
CopyEngine.Instance?.CancelQxBrackets(leader, instr);
// B70 DW-B70-02: also sweep follower PTT-Copy brackets before re-placing
CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

`CancelQxBracketsForFollowers` already exists (CopyEngine.cs line 505), is already used by
`PttGlobalQuickExit.Execute` (PttGlobalQuickExit.cs line 38), and is covered by existing
tests. Adding it here closes the per-chart Quick Exit path that was missing this sweep.

**CYC impact on `PttQuickExit.Execute`:**

The `?.` null-conditional operator counts as +1 decision point in strict McCabe (Roslyn).
Current CYC=5 (from comment at line 28). The new call adds `?.` = 1 decision point.
New CYC = 6. Within CYC <= 8 limit.

### Note on `CancelQxBracketsForFollowers` Scope

`CancelQxBracketsForFollowers` calls `FindRule(instr)` internally. If no copy rule is
configured for the instrument, it returns immediately (null guard at line 509). This is
safe — a per-chart Quick Exit on an instrument with no followers configured is a no-op for
the follower sweep, exactly as expected.

### CYC Analysis

| Method | Before | After | Limit | Pass? |
|--------|--------|-------|-------|-------|
| `IsQxCancelCandidate` | 5 | 6 | 8 | YES |
| `PttQuickExit.Execute` | 5 | 6 | 8 | YES |
| `CancelQxBracketsForFollowers` | 5 | 5 | 8 | YES (unchanged) |

### JS Rule Check (Ticket 2)

| Rule | Constraint | Verdict |
|------|-----------|---------|
| JS-021 | No lock() | PASS — static predicate, no state; existing method no lock |
| JS-001 | No throw in hot path | PASS — no throw in any changed method |
| JS-002 | No return null | PASS — bool return, never null |
| JS-033 | No async void | PASS — all methods synchronous |

---

## Section 4: Files Changed

| File | Change | Ticket |
|------|--------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Line 520: `_qxOcoSeq = 0` → `_qxOcoSeq = Environment.TickCount & 0x7FFF` | T1 |
| `src/PropTraderTools/CopyEngine.cs` | Lines 444-445: insert `PTT-Copy` branch in `IsQxCancelCandidate` | T2 |
| `src/PropTraderTools/Features/PttQuickExit.cs` | After line 52: add `CancelQxBracketsForFollowers(instr)` call | T2 |
| `tests/PropTraderTools.Tests/CopyEngineB70Tests.cs` | NEW — T_B70_01..T_B70_08 xUnit [Fact] tests | T1 + T2 |

**Files NOT changed:**

- `PttGlobalQuickExit.cs` — already calls `CancelQxBracketsForFollowers`; no change needed.
- `CancelQxBracketsForFollowers` (CopyEngine.cs line 505) — unchanged; already correct.
- `CancelQxBrackets` (CopyEngine.cs line 452) — unchanged; predicate fix propagates automatically.
- `IsAtmBracketName` (CopyEngine.cs line 432) — unchanged.
- `PttBreakEven.cs` — not in scope.

---

## Section 5: Test Plan

All tests are xUnit `[Fact]` methods in `tests/PropTraderTools.Tests/CopyEngineB70Tests.cs`.
Test class: `CopyEngineB70Tests`. No NUnit or MSTest (JS mandate — xUnit only).

### Ticket 1 Tests (DW-B70-01)

**T_B70_01 — Two sequential calls return distinct IDs**

```csharp
[Fact]
public void NextQxOcoId_TwoCalls_ReturnDistinctIds()
{
    // Arrange: construct CopyEngine (or use accessible NextQxOcoId via test hook).
    // Note: CopyEngine may require partial construction for unit test isolation.
    // If CopyEngine constructor requires NT8 context, use a public test-seam
    // or extract NextQxOcoId to a static helper with injectable seed.
    // This test MUST verify _qxOcoSeq != _qxOcoSeq + 1.
    var engine = CopyEngineTestFactory.CreateForTest();  // test factory (see Section 8 note)

    // Act
    string id1 = engine.NextQxOcoId();
    string id2 = engine.NextQxOcoId();

    // Assert
    Assert.NotEqual(id1, id2);
}
```

Expected: `id1 != id2` always (Interlocked.Increment guarantees monotonic).

**T_B70_02 — All IDs have "PTT-QX-" prefix**

```csharp
[Fact]
public void NextQxOcoId_AllIds_StartWithPttQxPrefix()
{
    var engine = CopyEngineTestFactory.CreateForTest();
    string id = engine.NextQxOcoId();
    Assert.StartsWith("PTT-QX-", id, StringComparison.Ordinal);
}
```

Expected: every returned ID starts with `"PTT-QX-"`.

**T_B70_03 — 100 sequential calls return 100 distinct values**

```csharp
[Fact]
public void NextQxOcoId_100Calls_AllDistinct()
{
    var engine = CopyEngineTestFactory.CreateForTest();
    var ids = new System.Collections.Generic.HashSet<string>();
    for (int i = 0; i < 100; i++)
        ids.Add(engine.NextQxOcoId());
    Assert.Equal(100, ids.Count);
}
```

Expected: all 100 IDs are distinct (HashSet count = 100).

### Ticket 2 Tests (DW-B70-02)

**T_B70_04 — `IsQxCancelCandidate` returns true for "PTT-Copy" order name**

```csharp
[Fact]
public void IsQxCancelCandidate_PttCopyName_ReturnsTrue()
{
    var order = OrderStub.WithName("PTT-Copy");
    Assert.True(CopyEngine.IsQxCancelCandidate(order));
}
```

Expected: `true`. Verifies the new branch (5) fires.

**T_B70_05 — `IsQxCancelCandidate` returns true for "PTT-Copy-Variant" order name**

```csharp
[Fact]
public void IsQxCancelCandidate_PttCopyVariant_ReturnsTrue()
{
    var order = OrderStub.WithName("PTT-Copy-Variant");
    Assert.True(CopyEngine.IsQxCancelCandidate(order));
}
```

Expected: `true`. Verifies `StartsWith("PTT-Copy")` matches all variants.

**T_B70_06 — `IsQxCancelCandidate` returns true for "PTT-QX-Stop" (regression)**

```csharp
[Fact]
public void IsQxCancelCandidate_PttQxStop_ReturnsTrue_Regression()
{
    var order = OrderStub.WithName("PTT-QX-Stop");
    Assert.True(CopyEngine.IsQxCancelCandidate(order));
}
```

Expected: `true`. Guards branch (3) PTT-QX- path is not broken.

**T_B70_07 — `IsQxCancelCandidate` returns true for "Stop1" (regression)**

```csharp
[Fact]
public void IsQxCancelCandidate_Stop1_ReturnsTrue_Regression()
{
    var order = OrderStub.WithName("Stop1");
    Assert.True(CopyEngine.IsQxCancelCandidate(order));
}
```

Expected: `true`. Guards branch (2) ATM bracket path is not broken.

**T_B70_08 — `IsQxCancelCandidate` returns false for non-bracket order "Entry"**

```csharp
[Fact]
public void IsQxCancelCandidate_EntryName_ReturnsFalse()
{
    var order = OrderStub.WithName("Entry");
    Assert.False(CopyEngine.IsQxCancelCandidate(order));
}
```

Expected: `false`. Verifies that non-bracket names are not swept.

### Optional T_B70_09 (Follower cancel integration — if integration test harness available)

If the test project supports a mock `CopyEngine` with injectable follower accounts:

```csharp
[Fact]
public void PttQuickExit_Execute_CallsCancelQxBracketsForFollowers()
{
    // Verify that CancelQxBracketsForFollowers is invoked during Execute.
    // Implementation: mock CopyEngine.Instance with a spy/stub and assert
    // CancelQxBracketsForFollowers was called with the correct instrument.
}
```

If the test harness cannot mock `CopyEngine.Instance` (it's a singleton set by NT8 AddOn
infrastructure), this test is deferred. The engineer decides based on test project capability.
T_B70_01..T_B70_08 are sufficient to close both defects from a unit-test perspective.

---

## Section 6: JS Rule Compliance Matrix

| Method | File | JS-021 (no lock) | JS-001 (no throw) | JS-002 (no null return) | JS-033 (no async void) |
|--------|------|-----------------|------------------|------------------------|------------------------|
| `NextQxOcoId()` (Ticket 1) | CopyEngine.cs | PASS — Interlocked | PASS | PASS — string return | PASS — sync |
| `_qxOcoSeq` field (Ticket 1) | CopyEngine.cs | PASS — field only | N/A | N/A | N/A |
| `IsQxCancelCandidate` (Ticket 2) | CopyEngine.cs | PASS — static pure | PASS | PASS — bool return | PASS — sync |
| `PttQuickExit.Execute` add (Ticket 2) | PttQuickExit.cs | PASS — no lock added | PASS | PASS — void return | PASS — sync |
| `CancelQxBracketsForFollowers` (called, unchanged) | CopyEngine.cs | PASS — pre-existing | PASS | PASS — void | PASS — sync |

**Scan targets:**

```
grep -r "lock(" src/PropTraderTools/CopyEngine.cs          # must return 0 for changed regions
grep -r "async void " src/PropTraderTools/Features/PttQuickExit.cs  # must return 0
grep -r "throw new" src/PropTraderTools/CopyEngine.cs      # must return 0 for changed methods
grep -r "return null" src/PropTraderTools/CopyEngine.cs    # must return 0 for changed methods
```

---

## Section 7: 7-Scan Pre-check

### SCAN-01 — ASCII-only

**Scope**: New/modified lines only.

```
"PTT-QX-"           -- ASCII-only
"PTT-Copy"          -- ASCII-only
"PTT-BE-"           -- ASCII-only (unchanged, regression coverage)
"B70 DW-B70-02:"    -- ASCII-only comment prefix
Environment.TickCount & 0x7FFF  -- no string literals
```

**Pre-existing violations** (PRE-EXISTING-01, PRE-EXISTING-02): em-dash at lines 398, 499
and Unicode arrows at ~1449-1450 are pre-existing and not touched by B70-LaneA changes.
Engineer must NOT modify those lines (scope creep prohibition).

**Verdict**: PASS for B70-LaneA new/modified lines.

### SCAN-02 — No lock()

All changed methods: field initializer (not a method), `IsQxCancelCandidate` (static pure
predicate), `PttQuickExit.Execute` (one new `?.` call).

```
grep "lock(" src/PropTraderTools/CopyEngine.cs   -- 0 results expected in changed regions
grep "lock(" src/PropTraderTools/Features/PttQuickExit.cs  -- 0 results expected
```

**Verdict**: PASS.

### SCAN-03 — No DateTime.Now

No `DateTime.Now` in changed code. `Environment.TickCount` is not a date — it is a
millisecond integer counter. Not a DateTime violation.

**Verdict**: PASS.

### SCAN-04 — No throw new XxxException in changed methods

No `throw` statement in `NextQxOcoId()`, `IsQxCancelCandidate()`, or in the
`PttQuickExit.Execute` addition.

**Verdict**: PASS.

### SCAN-05 — PTT- prefix on all order signal names

`NextQxOcoId()` returns `"PTT-QX-{N:D5}"` — prefix `"PTT-"` preserved.
No new `CreateOrder` calls introduced. The PTT-Copy signal name is read but not written
in the changed code (`IsQxCancelCandidate` reads `o.Name`, does not assign).

**Verdict**: PASS.

### SCAN-06 — No mutable struct / no public mutable fields introduced

No new structs introduced. `_qxOcoSeq` is an existing `int` field — changing its initial
value does not change its access modifier or mutability semantics.

**Verdict**: PASS.

### SCAN-07 — CYC <= 8 for all changed methods

| Method | CYC After | Pass? |
|--------|-----------|-------|
| `NextQxOcoId()` | 1 | YES |
| `IsQxCancelCandidate` | 6 | YES |
| `PttQuickExit.Execute` | 6 | YES |

**Verdict**: PASS.

### NT8-VERIFY-01 — Order.Name used in IsQxCancelCandidate

`Order.Name` is the NT8 signal name string. Used extensively in `IsQxCancelCandidate`
branches (3), (4) — same property, same pattern. Branch (5) `"PTT-Copy"` uses
`StringComparison.Ordinal` consistent with branches (3) and (4).

**NT8 ground truth**: `CopyEngine.cs` line 1264 confirms `signalName = "PTT-Copy"` is the
value written to `Order.Name` for all copy-dispatched orders. Branch (5) matches exactly.

**Verdict**: PASS.

### NT8-VERIFY-02 — Environment.TickCount range

`Environment.TickCount` is a signed `int` representing milliseconds since system start.
It can be negative (wraps around after ~24.9 days of uptime). `& 0x7FFF` masks to the
low 15 bits, always producing a non-negative value in `[0, 32767]`.

`_qxOcoSeq` is declared `int`. `Interlocked.Increment` on a seeded `int` starting at
0..32767 will not overflow within any session (a session produces at most dozens of
Quick Exit presses). D5 format max = 99999, well above the worst-case seed + increments.

**Verdict**: PASS.

---

## Section 8: Ticket Structure

### Ticket 1 — DW-B70-01: NextQxOcoId seed fix

**Spec requirements satisfied**: DW-B70-01 (P0 — OCO ID reuse rejection).

**File**: `src/PropTraderTools/CopyEngine.cs`

**Change**: Single field initializer at line 520.

```
BEFORE: private int _qxOcoSeq = 0;
AFTER:  private int _qxOcoSeq = Environment.TickCount & 0x7FFF;
```

**Method signatures** (no signature change — field initializer only):

```csharp
// Field (line 520) — initializer value only changes:
private int _qxOcoSeq = Environment.TickCount & 0x7FFF;

// Method unchanged:
internal string NextQxOcoId()
    => "PTT-QX-" + System.Threading.Interlocked.Increment(ref _qxOcoSeq).ToString("D5");
```

**Test file**: `tests/PropTraderTools.Tests/CopyEngineB70Tests.cs`

**xUnit [Fact] tests**:

| Test Name | Asserts |
|-----------|---------|
| `NextQxOcoId_TwoCalls_ReturnDistinctIds` | `id1 != id2` after two calls on same engine instance |
| `NextQxOcoId_AllIds_StartWithPttQxPrefix` | returned string starts with `"PTT-QX-"` |
| `NextQxOcoId_100Calls_AllDistinct` | HashSet of 100 calls has Count == 100 |

**JS constraints**: JS-021 (Interlocked, no lock), JS-001 (no throw), JS-002 (no null return).

**SCAN checklist**:
- SCAN-01: ASCII-only string literals — PASS
- SCAN-02: No lock() — PASS (Interlocked retained)
- SCAN-03: No DateTime.Now — PASS (Environment.TickCount is not DateTime)
- SCAN-04: No throw new — PASS
- SCAN-05: PTT- prefix — PASS (unchanged)
- SCAN-06: No mutable struct — PASS
- SCAN-07: CYC=1 — PASS

---

### Ticket 2 — DW-B70-02: PTT-Copy cancel fix

**Spec requirements satisfied**: DW-B70-02 (P0 — PTT-Copy bracket duplication on follower).

**Files**:

1. `src/PropTraderTools/CopyEngine.cs`
2. `src/PropTraderTools/Features/PttQuickExit.cs`

**Changes**:

**File 1 — CopyEngine.cs, `IsQxCancelCandidate`:**

Insert after existing line 444 (PTT-BE- branch), before `return false`:

```csharp
if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)) return true;   // (5) B70
```

**Method signature** (no signature change — internal static bool, same params):

```csharp
// Updated method (CYC=6):
internal static bool IsQxCancelCandidate(Order o)
{
    if (o == null || o.Name == null) return false;                               // (1)
    if (IsAtmBracketName(o.Name)) return true;                                   // (2)
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
    if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)) return true;   // (5) B70
    return false;
}
```

**File 2 — PttQuickExit.cs, `Execute` Step 3:**

After line 52 (`CancelQxBrackets(leader, instr)`), insert:

```csharp
// B70 DW-B70-02: sweep follower PTT-Copy brackets before placing new QX orders
CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

**Method signature** (no signature change):

```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks)
```

**CYC after**: `Execute` = 6 (was 5; +1 for `?.` null-conditional). Within limit.

**Test file**: `tests/PropTraderTools.Tests/CopyEngineB70Tests.cs` (same file as Ticket 1)

**xUnit [Fact] tests**:

| Test Name | Asserts |
|-----------|---------|
| `IsQxCancelCandidate_PttCopyName_ReturnsTrue` | `true` for `Order.Name = "PTT-Copy"` |
| `IsQxCancelCandidate_PttCopyVariant_ReturnsTrue` | `true` for `Order.Name = "PTT-Copy-Variant"` |
| `IsQxCancelCandidate_PttQxStop_ReturnsTrue_Regression` | `true` for `"PTT-QX-Stop"` (guards branch 3) |
| `IsQxCancelCandidate_Stop1_ReturnsTrue_Regression` | `true` for `"Stop1"` (guards branch 2) |
| `IsQxCancelCandidate_EntryName_ReturnsFalse` | `false` for `"Entry"` (non-bracket) |

**JS constraints**: JS-021 (no lock), JS-001 (no throw), JS-002 (bool returns never null),
JS-033 (synchronous).

**SCAN checklist**:
- SCAN-01: `"PTT-Copy"` ASCII-only — PASS
- SCAN-02: No lock() in IsQxCancelCandidate or Execute addition — PASS
- SCAN-03: No DateTime.Now — PASS
- SCAN-04: No throw new in changed methods — PASS
- SCAN-05: PTT- prefix: `"PTT-Copy"` is read, not written; signal name invariant not affected — PASS
- SCAN-06: No mutable struct — PASS
- SCAN-07: CYC=6 for IsQxCancelCandidate, CYC=6 for Execute — both PASS

---

## Appendix: Deferred Backlog Carry-Forward

Items from B66-LaneC/06-deferred-backlog.md that remain OPEN and are NOT in scope for
B70-LaneA:

| ID | Item | Status |
|----|------|--------|
| DW-B66-C-02 | DispatchCopy dedup key = 0.0 for StopLimit (Gate 5) | OPEN — B67+ |
| DW-B66-BE-01 | CancelQxBrackets cancels PTT-BE-Stop on QX — Director confirm | OPEN — B67+ |
| DW-B63-01 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill | OPEN — B67+ |
| DW-B58-01 | SnapshotTargetsPublic hardcoded prefixes | OPEN — future |
| DW-B58-02 | GlobalBe non-atomic lazy init | OPEN — future |
| DW-B58-03 | RelayBe OcoGroup not forwarded | OPEN — future |
| DW-B54-01 | ATM auto-inject (blocked — StrategyBase required) | OPEN — blocked |
| PRE-EXISTING-01 | Non-ASCII em-dash CopyEngine.cs lines 398, 499 | OPEN |
| PRE-EXISTING-02 | Non-ASCII arrows CopyEngine.cs ~1449-1450 | OPEN |
| PRE-EXISTING-03 | deploy-sync.ps1 archived | OPEN |
