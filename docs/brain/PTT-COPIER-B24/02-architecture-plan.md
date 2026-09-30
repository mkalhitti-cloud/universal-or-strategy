# PTT-COPIER-B24 — Architecture Plan
**Phase**: 1 (Architecture)  
**Status**: REVIEW_PENDING  
**Defect**: DW-B23-BE-ALLACCOUNTS-01  
**Author**: ptt-architect  
**Date**: 2026-07-07  

---

## 1. Problem Statement and Root Cause

### Defect Summary
`BreakEven()` silently does nothing when no copy rule is registered for the instrument.

### Root Cause Chain
```
BreakEven(Instrument, int)            -- line 1176, CopyEngine.cs
  -> AllAccounts(instrument)          -- line 1050
      -> FindRule(instrument)         -- line 1064
          -> returns null (no rule)
      -> yield break                  -- line 1054 : SILENT EXIT
  -> MoveStopToBreakEven NEVER CALLED
```

[`AllAccounts`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1050) calls
[`FindRule`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1064), which returns
`null` when no rule exists for the instrument. `AllAccounts` immediately yields nothing
(`yield break` at line 1054). The `foreach` in
[`BreakEven(Instrument, int)`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1176)
iterates zero elements. `MoveStopToBreakEven` is never called. No error, no log.

### Why This Matters
A trader with a single account (no followers, no rule registered) presses B or triggers
auto-BE. Nothing moves. The stop stays in place. Silent loss of risk management.

---

## 2. Solution Design

### Approved Architecture — DO NOT DEVIATE

The fix is a new overload that routes the **leader account directly** to `MoveStopToBreakEven`
before attempting the `AllAccounts` fan-out. This decouples BE from rule registration.

### STEP 1 — New Overload in `CopyEngine.cs`

Insert immediately after [`BreakEven(Instrument, int)`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1180) at **line 1181**:

```csharp
// CYC=3. JS-021: no lock. JS-002: null leader -> status + early return.
// Account first matches MoveStopToBreakEven(Account, Instrument, int) convention.
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
{
    if (leader == null)                                      // (1) null guard
    {
        StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
        return;
    }
    MoveStopToBreakEven(leader, instrument, bufferTicks);   // leader direct, no rule needed
    foreach (var acc in AllAccounts(instrument))            // (2) follower fan-out
    {
        if (acc == leader) continue;                        // (3) skip duplicate
        MoveStopToBreakEven(acc, instrument, bufferTicks);
    }
}
```

**CYC breakdown**:
- Branch 1: `if (leader == null)` → early return
- Branch 2: `foreach (var acc in AllAccounts(instrument))` → loop iteration
- Branch 3: `if (acc == leader) continue` → duplicate skip
- Total CYC = 3 ✓

**Overload coexistence**:  
The existing `BreakEven(Instrument, int)` (2-param) at line 1176 is **NOT modified**.  
TrailBe and any other callers of the 2-param overload continue to compile unchanged.  
C# resolves by parameter count — no ambiguity.

---

### STEP 2 — Update 6 Call Sites

#### CopyEngine.cs

| Location | Current | Replacement | Source of `acc` |
|---|---|---|---|
| [Line 1396](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1396) | `BreakEven(instr, buf)` | `BreakEven(acc, instr, buf)` | `var acc = _pendingBeAccount` at line 1389 |

**Exact diff for line 1396**:
```
- BreakEven(instr, buf);
+ BreakEven(acc, instr, buf);
```

#### TradeCopierPanel.cs

| Location | Current | Replacement | Source of `_leaderAccount` |
|---|---|---|---|
| [Line 782](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:782) | `_engine.BreakEven(_instrument, _beBuffer)` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` | `private Account _leaderAccount` (line 120) |
| [Line 791](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:791) | `_engine.BreakEven(_instrument, _beBuffer)` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` | same |
| [Line 859](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:859) | `_engine.BreakEven(_instrument, _beBuffer)` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` | same |
| [Line 1299](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:1299) | `_engine.BreakEven(_instrument, ticks)` | `_engine.BreakEven(_leaderAccount, _instrument, ticks)` | same |
| [Line 1418](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:1418) | `_engine.BreakEven(_instrument, buf)` | `_engine.BreakEven(_leaderAccount, _instrument, buf)` | same |

**`_leaderAccount` field**: Declared at [`TradeCopierPanel.cs:120`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:120) as `private Account _leaderAccount`. Set on account selection (line 388), cleared on disconnect (line 406). Always present as a nullable field — null when no account selected. The new overload handles `null` cleanly via the leader-null guard. No additional null checks needed at call sites.

---

### STEP 3 — Two New [Fact] Tests in `CopyEngineTests.cs`

**Baseline**: 126 tests → **Target**: 128 tests

#### Test 1: Null leader fires StatusUpdate and does not throw

```csharp
[Fact]
public void BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull()
{
    // Arrange
    var engine = new CopyEngine();
    string captured = null;
    engine.StatusUpdate += s => captured = s;

    // Act
    var ex = Record.Exception(() => engine.BreakEven(null, null, 2));

    // Assert
    Assert.Null(ex);
    Assert.Equal("PTT-BE: leader null -- BE skipped", captured);
}
```

**What it verifies**: Branch 1 of new overload — null leader triggers StatusUpdate and returns.
No exception escapes. Guards REQ-B24-01 and REQ-B24-03.

#### Test 2: Non-null leader with null instrument does not throw

```csharp
[Fact]
public void BreakEven_AccountOverload_NullInstrument_NoException()
{
    // Arrange
    var engine = new CopyEngine();
    // Use existing Account construction pattern from file (new Account() or test stub)
    Account stubAccount = CreateStubAccount();   // follow existing test helper pattern

    // Act
    var ex = Record.Exception(() => engine.BreakEven(stubAccount, null, 2));

    // Assert
    Assert.Null(ex);
}
```

**What it verifies**: When leader is non-null and instrument is null (no rule will match),
`MoveStopToBreakEven(leader, null, 2)` is called. `MoveStopToBreakEven` calls `FindPosition(acc, null)` which gracefully handles null. No NullReferenceException escapes. Guards REQ-B24-02.

**Note for engineer**: Use the `CreateStubAccount()` helper or inline `Account` construction
pattern already established in `CopyEngineTests.cs`. Do NOT import new test infrastructure.

---

## 3. Rule Constraints Table

| Rule | Category | Severity | Applies To | Constraint | Verified |
|---|---|---|---|---|---|
| JS-021 | Concurrency | P0 | New overload, all call sites | No `lock()` anywhere in new code | YES — zero lock() introduced |
| JS-002 | Type Safety | P0 | `leader == null` branch | Null leader → `StatusUpdate?.Invoke(...)` + `return`. Never fall-through. | YES — branch 1 of new overload |
| JS-001 | Error Handling | P0 | New overload | No `throw new XxxException(...)` in new code. Errors handled by MoveStopToBreakEven's internal try/catch. | YES — no throw in new code |
| JS-033 | Concurrency | P0 | New overload | Not `async void` — synchronous `void`. | YES |
| CYC-LIMIT | Complexity | P0 | New overload | CYC ≤ 8. Actual: CYC = 3 (null guard + foreach + skip-duplicate). | YES |
| NT8-032 | NT8 Runtime | P2 | CopyEngineTests.cs | Tests stay co-located in PropTraderTools assembly — no separate test runner .csproj. | YES — adding to existing file |
| NT8-043 | NT8 Runtime | P1 | Event unsubscription (existing code) | Use `if (acc != null) acc.Event -= handler` — never `acc?.Event -= handler`. | N/A — new overload has NO event subscription/unsubscription |

**NT8-043 note**: The existing `OnPendingBeAccountUpdate` at
[`CopyEngine.cs:1392-1393`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:1392)
already uses the compliant `if (acc != null) acc.AccountItemUpdate -= OnPendingBeAccountUpdate` pattern.
The new overload does not add any event subscriptions. NT8-043 is a watch-only constraint here —
no violation possible in new code.

---

## 4. Component Map

```
CopyEngine.cs
  ├── AllAccounts(Instrument) [UNCHANGED]           line 1050
  ├── FindRule(Instrument) [UNCHANGED]              line 1064
  ├── MoveStopToBreakEven(Account, Instrument, int) [UNCHANGED]  line 1133
  ├── BreakEven(Instrument, int) [UNCHANGED]        line 1176    ← TrailBe uses this
  ├── BreakEven(Account, Instrument, int) [NEW]     line 1181    ← 6 call sites use this
  └── OnPendingBeAccountUpdate [LINE 1396 ONLY]     line 1364

TradeCopierPanel.cs
  ├── OnBeUp          [LINE 782 ONLY]
  ├── OnBeDown        [LINE 791 ONLY]
  ├── OnBeConnected   [LINE 859 ONLY]
  ├── OnBreakEven     [LINE 1299 ONLY]
  └── DispatchShortcut[LINE 1418 ONLY]

CopyEngineTests.cs
  ├── [EXISTING 126 tests — UNTOUCHED]
  ├── BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull [NEW]
  └── BreakEven_AccountOverload_NullInstrument_NoException [NEW]
```

---

## 5. Data Flow

```
User Action (Key.B / BeUp / BeDown / OnBreakEven / OnBeConnected / PendingBe trigger)
         |
         v
[TradeCopierPanel call site]
  _engine.BreakEven(_leaderAccount, _instrument, bufferTicks)
         |
         v (new 3-param overload, CopyEngine.cs:1181)
  [Branch 1] leader == null?
      YES: StatusUpdate("PTT-BE: leader null -- BE skipped")  --> Panel label (Dispatcher.InvokeAsync)
           return
      NO: continue
         |
         v
  MoveStopToBreakEven(leader, instrument, bufferTicks)     <-- ALWAYS fires for leader
         |
         v
  foreach acc in AllAccounts(instrument)                   <-- empty when no rule
      [Branch 3] acc == leader? continue (skip duplicate)
      [Branch 2] MoveStopToBreakEven(acc, ...)             <-- fires for each follower
         |
         v
  [MoveStopToBreakEven — UNCHANGED]
    FindPosition -> IsFlat guard -> iterate acc.Orders -> acc.Change(order[])
    StatusUpdate("acc.Name: BE moved to X")
```

---

## 6. Spec Requirements Satisfied

| Requirement ID | Description | Satisfied By |
|---|---|---|
| REQ-B24-01 | BreakEven must fire for leader when no rule registered | New overload calls `MoveStopToBreakEven(leader, ...)` before `AllAccounts` fan-out |
| REQ-B24-02 | Follower fan-out still applies when rules exist (backward compat) | `AllAccounts` fan-out loop preserved in new overload |
| REQ-B24-03 | null leader guard emits StatusUpdate and returns | Branch 1: `StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped"); return;` |
| REQ-B24-04 | 6 call sites updated to supply leader Account parameter | STEP 2 table: CopyEngine.cs:1396 + 5 TradeCopierPanel.cs sites |
| REQ-B24-05 | Test count moves from 126 to 128 | STEP 3: 2 new [Fact] tests added to CopyEngineTests.cs |
| DW-B23-BE-ALLACCOUNTS-01 | Defect closed | Root cause eliminated — leader fires regardless of rule presence |

---

## 7. Seven-Scan Checklist (Pre-Population for Tickets Phase)

This checklist MUST be reproduced verbatim in `04-tickets.md` and checked off by the engineer
before every commit on this block.

### SCAN-01: JS-021 — No lock() in write-set
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\TradeCopierPanel.cs" -Pattern "lock\s*\(" | Select-Object LineNumber, Line
```
**Pass criterion**: Zero matches.

### SCAN-02: JS-002 — null leader path emits StatusUpdate and returns
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-BE: leader null -- BE skipped"
```
**Pass criterion**: Exactly 1 match inside the new `BreakEven(Account, Instrument, int)` overload.

### SCAN-03: CYC ≤ 8 — New overload complexity
```powershell
python scripts/complexity_audit.py src\PropTraderTools\CopyEngine.cs
```
**Pass criterion**: `BreakEven(Account, Instrument, int)` reports CYC ≤ 8 (expected CYC=3).

### SCAN-04: Overload coexistence — 2-param overload unchanged
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "internal void BreakEven\(Instrument"
```
**Pass criterion**: Exactly 1 match at the original line. Body unchanged.

### SCAN-05: All 6 call sites updated — no stale 2-param calls from updated sites
```powershell
Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "_engine\.BreakEven\(_instrument"
```
**Pass criterion**: Zero matches (all TradeCopierPanel sites now use 3-param form).

### SCAN-06: Test count ≥ 128
```powershell
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Pass criterion**: Count ≥ 128.

### SCAN-07: NT8-043 — No null-conditional event unsubscription in write-set
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\?\.\w+\s*-="
```
**Pass criterion**: Zero matches (null-conditional `-=` is banned; use `if (x != null) x.Event -= handler`).

---

## 8. Unchanged-Code Contract

The following symbols are explicitly **NOT modified** by this block. The engineer MUST verify
these are byte-for-byte identical after implementing the tickets:

| Symbol | File | Line | Reason |
|---|---|---|---|
| `AllAccounts(Instrument)` | CopyEngine.cs | 1050 | Core iteration — not the defect site |
| `FindRule(Instrument)` | CopyEngine.cs | 1064 | Core lookup — not the defect site |
| `MoveStopToBreakEven(Account, Instrument, int)` | CopyEngine.cs | 1133 | Core execution — not the defect site |
| `BreakEven(Instrument, int)` | CopyEngine.cs | 1176 | TrailBe depends on this 2-param form |
| All other TradeCopierPanel methods | TradeCopierPanel.cs | — | Scope: call site lines only |

---

## 9. Post-Block NT8 Rule Candidate

The brief references **NT8-043** (null-conditional event unsubscription ban). The current highest
rule in `NT8_COMPILER_RULES.md` is NT8-032. NT8-043 is reserved for this block's discovery.

**Rule to add after engineering phase** (if confirmed):
```
NT8-043 | P1 | NULL-CONDITIONAL EVENT UNSUBSCRIPTION IS BANNED
CONFIRMED: B24
ERROR: Silent CS1 runtime crash — delegate removal via ?. operator fails under NT8 Roslyn
BANNED:  acc?.AccountItemUpdate -= handler;
SAFE:    if (acc != null) acc.AccountItemUpdate -= handler;
SCAN:    grep -rn "\?\.\w\+-=" src/PropTraderTools/ --include="*.cs"
```

Engineer MUST add this rule to `docs/standards/NT8_COMPILER_RULES.md` during Phase 5 if the
null-conditional pattern is confirmed to cause issues. If the existing code already passes
without this rule, add with STATUS: WATCH (not yet triggered in B24 code).

---

## 10. Review Gate

**ptt-plan-reviewer**: Mark this plan `REVIEW_PASS` or `REVIEW_FAIL(violation-list)`.

Required for `REVIEW_PASS`:
- [ ] Overload signature matches approved architecture exactly
- [ ] All 6 call sites enumerated with correct line numbers and before/after
- [ ] CYC verified ≤ 8 for new method
- [ ] JS-021 absence confirmed
- [ ] JS-002 null-guard pattern confirmed
- [ ] SCAN-01 through SCAN-07 present and precise
- [ ] Unchanged-code contract explicitly stated
- [ ] Test stubs produce deterministic assertions (not "check it works")
