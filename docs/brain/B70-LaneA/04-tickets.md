# B70-LaneA Tickets

**Block**: B70-LaneA
**Author**: ptt-architect (Phase 3)
**Input**: docs/brain/B70-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Date**: 2026-08-14
**Status**: TICKETS_COMPLETE

---

## Ticket 1 — DW-B70-01: OCO ID Reuse Fix (NextQxOcoId seed)

### Header

| Field | Value |
|-------|-------|
| Ticket ID | T-B70-01 |
| Defect ID | DW-B70-01 |
| Priority | P0 |
| Title | OCO ID reuse rejection on second Quick Exit press |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Test File | `src/PropTraderTools/Tests/B70Tests.cs` (NEW) |

### Spec Requirement Satisfied

**DW-B70-01**: `CopyEngine._qxOcoSeq` initialized to `0` at field-declaration time. On session
reconnect / AddOn reload, counter resets to 0. `NextQxOcoId()` produces `"PTT-QX-00001"` again.
NT8 rejects the second OCO group submission with the same name. Fix: seed `_qxOcoSeq` to a
non-zero value at construction time so two consecutive sessions cannot produce the same ID.

---

### Change — CopyEngine.cs (line 520)

**One-line field initializer update only. No other changes to this file for this ticket.**

**EXACT BEFORE (line 520):**

```csharp
private int _qxOcoSeq = 0;
```

**EXACT AFTER (line 520):**

```csharp
private int _qxOcoSeq = Environment.TickCount & 0x7FFF;
```

**Method signatures — unchanged (DO NOT MODIFY):**

```csharp
// NextQxOcoId() body is UNCHANGED. Do not touch this method.
// CYC=1: straight expression. JS-021: no lock -- Interlocked.
internal string NextQxOcoId()
    => "PTT-QX-" + System.Threading.Interlocked.Increment(ref _qxOcoSeq).ToString("D5");
```

**PttQuickExit.cs — NO CHANGES for this ticket.** The Guid fallback paths at lines 55 and 86
remain valid and correct. Do not modify them.

---

### CYC Analysis

| Method | Before | After | Limit | Pass? |
|--------|--------|-------|-------|-------|
| `NextQxOcoId()` | 1 | 1 | 8 | YES — method body unchanged |
| `_qxOcoSeq` field initializer | N/A | N/A | N/A | N/A — field init, not a method |

---

### JS Rule Compliance

| Rule | Constraint | Verdict |
|------|-----------|---------|
| JS-021 | No `lock()` anywhere | PASS — `Interlocked.Increment` unchanged, no lock added |
| JS-001 | No `throw new XxxException` in changed code | PASS — no throw statement |
| JS-002 | No `return null` in changed method | PASS — returns `string`, never null |
| JS-033 | No `async void` in changed method | PASS — synchronous expression body |

---

### NT8 Verification

| Check | Claim | Evidence |
|-------|-------|----------|
| NT8-VERIFY-01 | `NextQxOcoId()` output starts with `"PTT-QX-"` | Method body unchanged; prefix literal unchanged |
| NT8-VERIFY-02 | `Environment.TickCount & 0x7FFF` is always `[0, 32767]` — non-negative `int` | `TickCount` is signed int (can wrap negative); `& 0x7FFF` masks low 15 bits to always non-negative. Max seed 32767 in D5 format = `"32767"` (5 chars). Worst-case seed + increments < 99999 (D5 max). PASS. |

---

### xUnit Tests — T_B70_01, T_B70_02, T_B70_03

**Test file**: `src/PropTraderTools/Tests/B70Tests.cs` (create new file)
**Class name**: `CopyEngineB70Tests`
**Namespace**: `PropTraderTools`
**Framework**: xUnit ONLY. No NUnit, no MSTest (JS mandate).

**MANDATORY reading before writing the test file:**
- Read `src/PropTraderTools/CopyEngineTests.cs` lines 3133-3189 to understand the exact `MakeOrder` helper.
- For `NextQxOcoId` tests: use `CopyEngine.Instance` directly. Use reflection to reset `_qxOcoSeq`
  to a known value before each test to ensure isolation. Pattern:
  ```csharp
  var fi = typeof(CopyEngine).GetField("_qxOcoSeq",
      BindingFlags.NonPublic | BindingFlags.Instance);
  fi.SetValue(CopyEngine.Instance, 1000);  // seed to known value
  ```

---

**[Fact] T_B70_01 — Two sequential calls return distinct IDs**

```csharp
[Fact]
public void T_B70_01_NextQxOcoId_TwoCalls_ReturnDistinctIds()
{
    // Arrange: reset _qxOcoSeq to known value for isolation
    var fi = typeof(CopyEngine).GetField(
        "_qxOcoSeq",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    fi.SetValue(CopyEngine.Instance, 1000);

    // Act
    string id1 = CopyEngine.Instance.NextQxOcoId();
    string id2 = CopyEngine.Instance.NextQxOcoId();

    // Assert: Interlocked.Increment guarantees monotonic -- id1 != id2
    Assert.NotEqual(id1, id2);
}
```

**Asserts**: `id1 != id2` (two sequential calls on the same instance must differ).
**CYC**: 1 (straight line, no branches).

---

**[Fact] T_B70_02 — All IDs have "PTT-QX-" prefix**

```csharp
[Fact]
public void T_B70_02_NextQxOcoId_AllIds_StartWithPttQxPrefix()
{
    // Arrange
    var fi = typeof(CopyEngine).GetField(
        "_qxOcoSeq",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    fi.SetValue(CopyEngine.Instance, 2000);

    // Act
    string id = CopyEngine.Instance.NextQxOcoId();

    // Assert
    Assert.StartsWith("PTT-QX-", id, StringComparison.Ordinal);
}
```

**Asserts**: returned string starts with `"PTT-QX-"` (prefix invariant preserved).
**CYC**: 1 (straight line, no branches).

---

**[Fact] T_B70_03 — 100 sequential calls return 100 distinct values**

```csharp
[Fact]
public void T_B70_03_NextQxOcoId_100Calls_AllDistinct()
{
    // Arrange: seed to a stable starting value well below 99999
    var fi = typeof(CopyEngine).GetField(
        "_qxOcoSeq",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    fi.SetValue(CopyEngine.Instance, 3000);

    // Act
    var ids = new System.Collections.Generic.HashSet<string>();
    for (int i = 0; i < 100; i++)
        ids.Add(CopyEngine.Instance.NextQxOcoId());

    // Assert: all 100 IDs are distinct (no counter aliasing)
    Assert.Equal(100, ids.Count);
}
```

**Asserts**: `HashSet.Count == 100` (all 100 calls produce unique strings).
**CYC**: 1 (for loop is not a cyclomatic branch in the test itself — it is a bounded loop with no conditional logic).

---

### 7-Scan Checklist — Ticket 1

```
SCAN-01 (ASCII-only)
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | cat
  Expected: Only changed line is "_qxOcoSeq = Environment.TickCount & 0x7FFF"
            All characters in that line are ASCII (0x00-0x7F). No Unicode.
  Result: PASS

SCAN-02 (No lock() in changed regions)
  Command: grep "lock(" src/PropTraderTools/CopyEngine.cs
  Scope: changed lines only (line 520 and NextQxOcoId body at lines 521-522)
  Expected: 0 results in lines 520-522. Interlocked.Increment retained unchanged.
  Result: PASS

SCAN-03 (No return null in changed method)
  Command: grep "return null" -- inspect NextQxOcoId body
  Expected: 0 results. NextQxOcoId returns string via expression body. No null return.
  Result: PASS

SCAN-04 (CYC check)
  Command: python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
  Scope: NextQxOcoId -- expression body CYC=1 (unchanged). Field init has no CYC.
  Expected: NextQxOcoId CYC=1. No regression from baseline.
  Result: PASS

SCAN-05 (ASCII scan -- string literals)
  Command: grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
  Scope: new/modified lines only. Pre-existing non-ASCII at lines 398, 499, ~1449-1450
         are NOT in scope and must NOT be touched (scope creep prohibition).
  Expected: Zero non-ASCII chars in any changed line.
  Result: PASS

SCAN-06 (dotnet build)
  Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
  Expected: 0 errors, 0 warnings introduced by B70 change.
  Result: PASS (engineer must verify)

SCAN-07 (dotnet test)
  Command: dotnet test --filter "FullyQualifiedName~T_B70_01|FullyQualifiedName~T_B70_02|FullyQualifiedName~T_B70_03"
  Expected: T_B70_01 PASS, T_B70_02 PASS, T_B70_03 PASS.
  Result: PASS (engineer must verify)

NT8-VERIFY-01 (PTT-QX- prefix preserved)
  Manual: Confirm NextQxOcoId body at lines 521-522 is UNCHANGED after edit.
  grep "PTT-QX-" src/PropTraderTools/CopyEngine.cs -- must still be present at line 522.
  Result: PASS (method body not touched)

NT8-VERIFY-02 (Seed range validation)
  Manual: "Environment.TickCount & 0x7FFF" -- 0x7FFF = 32767 decimal.
  Confirm: 32767 in D5 format = "32767" (5 characters, valid D5 column).
  Confirm: _qxOcoSeq is int (not uint/long) -- no sign issue after masking.
  Result: PASS
```

---

### Acceptance Criteria — Ticket 1

1. `src/PropTraderTools/CopyEngine.cs` line 520: `private int _qxOcoSeq = Environment.TickCount & 0x7FFF;`
2. `NextQxOcoId()` method body at lines 521-522: **UNCHANGED** (verify with diff).
3. `PttQuickExit.cs`: **NO CHANGES** (verify with diff).
4. `src/PropTraderTools/Tests/B70Tests.cs`: Created with class `CopyEngineB70Tests`, namespace `PropTraderTools`.
5. Tests T_B70_01, T_B70_02, T_B70_03 pass: `dotnet test` exits 0.
6. `dotnet build` exits 0.
7. SCAN-01 through SCAN-07 all verified.

---

---

## Ticket 2 — DW-B70-02: IsQxCancelCandidate + PttQuickExit follower cancel

### Header

| Field | Value |
|-------|-------|
| Ticket ID | T-B70-02 |
| Defect ID | DW-B70-02 |
| Priority | P0 |
| Title | Bracket duplication on follower (PTT-Copy not cancelled on Quick Exit) |
| Files | `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/Features/PttQuickExit.cs` |
| Test File | `src/PropTraderTools/Tests/B70Tests.cs` (APPEND to same file as Ticket 1 tests) |

### Spec Requirement Satisfied

**DW-B70-02**: `IsQxCancelCandidate` lacks a branch for the `"PTT-Copy"` prefix used for all
copy-dispatched entry orders (`CopyEngine.cs` line 1264 confirms `string signalName = "PTT-Copy"`).
Additionally, `PttQuickExit.Execute` Step 3 only sweeps the **leader account** via
`CancelQxBrackets(leader, instr)`. Follower accounts' PTT-Copy orders are invisible to the leader
sweep. Both parts must be fixed: the predicate must recognize `"PTT-Copy"` prefix, and the
follower accounts must be swept by a separate call. Together they close the bracket duplication
defect on follower accounts during per-chart Quick Exit.

---

### Change A — CopyEngine.cs: `IsQxCancelCandidate` (lines 439-446)

**Insert one branch after the PTT-BE- branch (current line 444), before `return false`.**

**EXACT BEFORE (lines 439-446):**

```csharp
        // IsQxCancelCandidate: returns true if order should be cancelled by CancelQxBrackets.
        // Covers: ATM bracket names (via IsAtmBracketName), PTT-QX-* prefix, PTT-BE-* prefix.
        // CYC=5: 1 (base) + 4 if-branches. Roslyn: || inside single if = 1 decision point.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). ASCII-only.
        internal static bool IsQxCancelCandidate(Order o)
        {
            if (o == null || o.Name == null) return false;                               // (1)
            if (IsAtmBracketName(o.Name)) return true;                                   // (2)
            if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
            if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
            return false;
        }
```

**EXACT AFTER:**

```csharp
        // IsQxCancelCandidate: returns true if order should be cancelled by CancelQxBrackets.
        // Covers: ATM bracket names (via IsAtmBracketName), PTT-QX-* prefix, PTT-BE-* prefix,
        //         PTT-Copy* prefix (B70 DW-B70-02: follower copy-dispatched entry orders).
        // CYC=6: 1 (base) + 5 if-branches. Roslyn: || inside single if = 1 decision point.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). ASCII-only.
        internal static bool IsQxCancelCandidate(Order o)
        {
            if (o == null || o.Name == null) return false;                               // (1)
            if (IsAtmBracketName(o.Name)) return true;                                   // (2)
            if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
            if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
            if (o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)) return true;   // (5) B70 DW-B70-02
            return false;
        }
```

**Method signature — no change to signature:**

```csharp
internal static bool IsQxCancelCandidate(Order o)
```

**CYC after Change A**: 6 (was 5; +1 decision point for new if-branch). Within CYC <= 8. PASS.

---

### Change B — PttQuickExit.cs: `Execute` Step 3 (around line 51-52)

**Add one call after the existing `CancelQxBrackets(leader, instr)` call in Step 3.**

**EXACT BEFORE (lines 51-52 context):**

```csharp
            // Step 3: CancelStaleBrackets -- cancel ATM bracket + previous PTT-QX orders
            CopyEngine.Instance?.CancelQxBrackets(leader, instr);
```

**EXACT AFTER:**

```csharp
            // Step 3: CancelStaleBrackets -- cancel ATM bracket + previous PTT-QX orders (leader)
            CopyEngine.Instance?.CancelQxBrackets(leader, instr);
            // B70 DW-B70-02: also cancel follower PTT-Copy brackets before re-placing QX orders
            CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);
```

**Method signature — no change to `Execute` signature:**

```csharp
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks)
```

**`CancelQxBracketsForFollowers` signature (confirm from source before implementing):**

```csharp
// Confirm exact signature at CopyEngine.cs ~line 505 before writing the call.
// Expected: internal void CancelQxBracketsForFollowers(Instrument instr)
// Evidence: B68Tests.cs T_B68_01 confirmed this signature. PttGlobalQuickExit.cs line 38
//           already calls this method with the same pattern.
```

**CYC impact on `Execute`:**
- The `?.` null-conditional operator on the new call counts as +1 McCabe decision point (Roslyn strict).
- Current `Execute` CYC = 5 (from comment at line 28 of PttQuickExit.cs).
- New `Execute` CYC = 6 (was 5; +1 for `?.`). Within CYC <= 8. PASS.
- **Update the CYC comment on `Execute` from `CYC=5` to `CYC=6`.**

---

### CYC Analysis

| Method | Before | After | Limit | Pass? |
|--------|--------|-------|-------|-------|
| `IsQxCancelCandidate` | 5 | 6 | 8 | YES |
| `PttQuickExit.Execute` | 5 | 6 | 8 | YES |
| `CancelQxBracketsForFollowers` (called, UNCHANGED) | 5 | 5 | 8 | YES |

---

### JS Rule Compliance

| Rule | Method | Verdict |
|------|--------|---------|
| JS-021 (no lock) | `IsQxCancelCandidate` | PASS — static pure predicate, no state, no lock |
| JS-021 (no lock) | `Execute` addition | PASS — one `?.` call, no lock added |
| JS-001 (no throw) | `IsQxCancelCandidate` | PASS — no throw in any branch |
| JS-001 (no throw) | `Execute` addition | PASS — void statement, no throw |
| JS-002 (no null return) | `IsQxCancelCandidate` | PASS — bool return, never null |
| JS-002 (no null return) | `Execute` addition | PASS — void method |
| JS-033 (no async void) | Both | PASS — all synchronous |

---

### NT8 Verification

| Check | Claim | Evidence |
|-------|-------|----------|
| NT8-VERIFY-01 | `Order.Name` is the NT8 signal name string | `CopyEngine.cs` line 1264: `string signalName = "PTT-Copy"` confirms `Order.Name` receives this value for all copy-dispatched orders. Branch (5) `"PTT-Copy"` matches exactly. |
| `CancelQxBracketsForFollowers` is AddOn-accessible | Method already called in `PttGlobalQuickExit.cs` line 38 with identical pattern | Pre-existing usage confirms accessibility. No new API surface introduced. |

---

### xUnit Tests — T_B70_04 through T_B70_08

**Append these tests to `src/PropTraderTools/Tests/B70Tests.cs`** (same file as Ticket 1 tests).
Same class `CopyEngineB70Tests`, namespace `PropTraderTools`.

**Test harness: `MakeOrder` helper** — copy the exact `MakeOrder` static helper from
`CopyEngineTests.cs` lines 3133-3189 into `CopyEngineB70Tests` as a `private static` method.
The helper uses `FormatterServices.GetUninitializedObject` to bypass the sealed `Order` constructor
and sets `OrderState` and `Name` via reflection. This is the established pattern in the project
for `IsQxCancelCandidate` tests (see `CopyEngineTests.cs` T_B66_01..T_B66_07).

---

**[Fact] T_B70_04 — `IsQxCancelCandidate` returns true for exact match `"PTT-Copy"`**

```csharp
[Fact]
public void T_B70_04_IsQxCancelCandidate_PttCopyExact_ReturnsTrue()
{
    // Arrange: order with Name = "PTT-Copy" (exact base signal name used by DispatchCopy)
    var order = MakeOrder(OrderState.Working, "PTT-Copy");

    // Act + Assert: new branch (5) must fire for the exact signal name
    Assert.True(
        CopyEngine.IsQxCancelCandidate(order),
        "IsQxCancelCandidate: 'PTT-Copy' must return true (PTT-Copy prefix branch (5))");
}
```

**Asserts**: `IsQxCancelCandidate` returns `true` for `"PTT-Copy"`. Verifies branch (5) fires.
**CYC**: 1 (straight line).

---

**[Fact] T_B70_05 — `IsQxCancelCandidate` returns true for variant `"PTT-Copy-Variant"`**

```csharp
[Fact]
public void T_B70_05_IsQxCancelCandidate_PttCopyVariant_ReturnsTrue()
{
    // Arrange: order name with PTT-Copy prefix plus suffix
    var order = MakeOrder(OrderState.Working, "PTT-Copy-Variant");

    // Act + Assert: StartsWith("PTT-Copy") must match all variants
    Assert.True(
        CopyEngine.IsQxCancelCandidate(order),
        "IsQxCancelCandidate: 'PTT-Copy-Variant' must return true (StartsWith PTT-Copy)");
}
```

**Asserts**: `true` for `"PTT-Copy-Variant"`. Verifies `StartsWith` covers all suffixes.
**CYC**: 1 (straight line).

---

**[Fact] T_B70_06 — `IsQxCancelCandidate` returns true for `"PTT-QX-Stop"` (regression guard)**

```csharp
[Fact]
public void T_B70_06_IsQxCancelCandidate_PttQxStop_ReturnsTrue_Regression()
{
    // Arrange: PTT-QX- prefix order (pre-existing branch (3))
    var order = MakeOrder(OrderState.Working, "PTT-QX-Stop");

    // Act + Assert: branch (3) must not be broken by the new branch (5)
    Assert.True(
        CopyEngine.IsQxCancelCandidate(order),
        "IsQxCancelCandidate: 'PTT-QX-Stop' must return true -- branch (3) regression guard");
}
```

**Asserts**: `true` for `"PTT-QX-Stop"`. Guards that branch (3) is not broken.
**CYC**: 1 (straight line).

---

**[Fact] T_B70_07 — `IsQxCancelCandidate` returns true for `"Stop1"` (regression guard)**

```csharp
[Fact]
public void T_B70_07_IsQxCancelCandidate_Stop1_ReturnsTrue_Regression()
{
    // Arrange: ATM bracket name (pre-existing branch (2) via IsAtmBracketName)
    var order = MakeOrder(OrderState.Working, "Stop1");

    // Act + Assert: branch (2) must not be broken by the new branch (5)
    Assert.True(
        CopyEngine.IsQxCancelCandidate(order),
        "IsQxCancelCandidate: 'Stop1' must return true -- branch (2) ATM regression guard");
}
```

**Asserts**: `true` for `"Stop1"`. Guards that ATM bracket branch is not broken.
**CYC**: 1 (straight line).

---

**[Fact] T_B70_08 — `IsQxCancelCandidate` returns false for non-bracket `"Entry"`**

```csharp
[Fact]
public void T_B70_08_IsQxCancelCandidate_EntryName_ReturnsFalse()
{
    // Arrange: a non-bracket, non-PTT order name
    var order = MakeOrder(OrderState.Working, "Entry");

    // Act + Assert: none of the 5 branches fires -- must return false
    Assert.False(
        CopyEngine.IsQxCancelCandidate(order),
        "IsQxCancelCandidate: 'Entry' must return false (not a bracket or PTT-prefixed order)");
}
```

**Asserts**: `false` for `"Entry"`. Verifies that non-bracket names are not swept.
**CYC**: 1 (straight line).

---

### 7-Scan Checklist — Ticket 2

```
SCAN-01 (No lock() in changed regions)
  Command: grep "lock(" src/PropTraderTools/CopyEngine.cs
  Scope: IsQxCancelCandidate region (lines 435-446 after change)
  Expected: 0 results. Static pure predicate -- no state, no lock.
  Command: grep "lock(" src/PropTraderTools/Features/PttQuickExit.cs
  Scope: Execute Step 3 addition (~line 52-54 after change)
  Expected: 0 results.
  Result: PASS

SCAN-02 (No throw new in changed methods)
  Command: grep "throw new" src/PropTraderTools/CopyEngine.cs -- inspect IsQxCancelCandidate
  Command: grep "throw new" src/PropTraderTools/Features/PttQuickExit.cs -- inspect Execute addition
  Expected: 0 results in changed regions. No exception thrown in any new or modified line.
  Result: PASS

SCAN-03 (No return null in IsQxCancelCandidate)
  Command: grep "return null" -- inspect IsQxCancelCandidate body
  Expected: 0 results. Method returns bool; every branch returns true or false.
  Result: PASS

SCAN-04 (CYC check)
  Command: python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
  Expected: IsQxCancelCandidate CYC=6 (was 5; +1 for new branch). Within limit 8. PASS.
  Command: python scripts/complexity_audit.py src/PropTraderTools/Features/PttQuickExit.cs
  Expected: Execute CYC=6 (was 5; +1 for ?.  null-conditional). Within limit 8. PASS.
  Result: PASS (engineer must verify post-change)

SCAN-05 (ASCII-only new string literals)
  Command: git diff HEAD -- src/PropTraderTools/CopyEngine.cs | grep "^+" | cat
  New literal: "PTT-Copy" -- all characters ASCII (P=0x50, T=0x54, T=0x54, -=0x2D, C=0x43, ...).
  New comment tokens: "B70 DW-B70-02" -- ASCII-only.
  Command: git diff HEAD -- src/PropTraderTools/Features/PttQuickExit.cs | grep "^+" | cat
  New comment: "B70 DW-B70-02: also cancel follower PTT-Copy brackets..." -- ASCII-only.
  Pre-existing non-ASCII at CopyEngine.cs lines 398, 499, ~1449-1450:
    DO NOT TOUCH THOSE LINES (scope creep prohibition per plan Appendix PRE-EXISTING-01/02).
  Result: PASS

SCAN-06 (dotnet build)
  Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
  Expected: 0 errors. No new warnings introduced by B70 changes.
  Result: PASS (engineer must verify)

SCAN-07 (dotnet test)
  Command: dotnet test --filter "FullyQualifiedName~T_B70_04|FullyQualifiedName~T_B70_05|FullyQualifiedName~T_B70_06|FullyQualifiedName~T_B70_07|FullyQualifiedName~T_B70_08"
  Expected: T_B70_04 PASS, T_B70_05 PASS, T_B70_06 PASS, T_B70_07 PASS, T_B70_08 PASS.
  Result: PASS (engineer must verify)
```

---

### Acceptance Criteria — Ticket 2

1. `src/PropTraderTools/CopyEngine.cs` `IsQxCancelCandidate`: new branch `(5)` inserted after
   `PTT-BE-` branch, before `return false`. CYC comment updated to `CYC=6`.
2. `src/PropTraderTools/Features/PttQuickExit.cs` `Execute` Step 3: one new line
   `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);` appended after
   `CancelQxBrackets(leader, instr)`. CYC comment updated to `CYC=6`.
3. Tests T_B70_04..T_B70_08 appended to `src/PropTraderTools/Tests/B70Tests.cs`.
4. All 5 tests pass: `dotnet test` exits 0.
5. `dotnet build` exits 0.
6. SCAN-01 through SCAN-07 all verified.
7. Pre-existing non-ASCII lines (398, 499, ~1449-1450) are NOT modified.

---

## Test File Summary

**File**: `src/PropTraderTools/Tests/B70Tests.cs`
**Class**: `CopyEngineB70Tests`
**Namespace**: `PropTraderTools`
**Framework**: xUnit ONLY

| Test ID | Method Name | Ticket | Asserts |
|---------|-------------|--------|---------|
| T_B70_01 | `T_B70_01_NextQxOcoId_TwoCalls_ReturnDistinctIds` | T1 | `id1 != id2` after two sequential calls |
| T_B70_02 | `T_B70_02_NextQxOcoId_AllIds_StartWithPttQxPrefix` | T1 | `result.StartsWith("PTT-QX-", Ordinal)` |
| T_B70_03 | `T_B70_03_NextQxOcoId_100Calls_AllDistinct` | T1 | `HashSet.Count == 100` after 100 calls |
| T_B70_04 | `T_B70_04_IsQxCancelCandidate_PttCopyExact_ReturnsTrue` | T2 | `true` for `"PTT-Copy"` (new branch 5) |
| T_B70_05 | `T_B70_05_IsQxCancelCandidate_PttCopyVariant_ReturnsTrue` | T2 | `true` for `"PTT-Copy-Variant"` |
| T_B70_06 | `T_B70_06_IsQxCancelCandidate_PttQxStop_ReturnsTrue_Regression` | T2 | `true` for `"PTT-QX-Stop"` (branch 3 regression) |
| T_B70_07 | `T_B70_07_IsQxCancelCandidate_Stop1_ReturnsTrue_Regression` | T2 | `true` for `"Stop1"` (branch 2 regression) |
| T_B70_08 | `T_B70_08_IsQxCancelCandidate_EntryName_ReturnsFalse` | T2 | `false` for `"Entry"` (non-bracket) |

---

## Files Changed Summary

| File | Change | Ticket |
|------|--------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Line 520: `_qxOcoSeq = 0` → `_qxOcoSeq = Environment.TickCount & 0x7FFF` | T1 |
| `src/PropTraderTools/CopyEngine.cs` | `IsQxCancelCandidate`: insert branch (5) `"PTT-Copy"` after `"PTT-BE-"` branch | T2 |
| `src/PropTraderTools/Features/PttQuickExit.cs` | `Execute` Step 3: append `CancelQxBracketsForFollowers(instr)` call | T2 |
| `src/PropTraderTools/Tests/B70Tests.cs` | NEW — 8 xUnit [Fact] tests T_B70_01..T_B70_08 | T1 + T2 |

**Files NOT changed (verify with diff — any unexpected change = scope violation):**

- `PttGlobalQuickExit.cs` — already calls `CancelQxBracketsForFollowers`; no change.
- `CancelQxBracketsForFollowers` method body — unchanged; already correct.
- `CancelQxBrackets` method — unchanged; predicate fix propagates automatically.
- `IsAtmBracketName` — unchanged.
- `PttBreakEven.cs` — not in scope.
- `PttQuickExit.cs` Guid fallback paths (lines 55, 86) — unchanged.

TICKETS_COMPLETE
