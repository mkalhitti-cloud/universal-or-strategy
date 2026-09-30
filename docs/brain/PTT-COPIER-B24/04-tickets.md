# PTT-COPIER-B24 — Tickets
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect
**Date**: 2026-07-07
**Plan Source**: `docs/brain/PTT-COPIER-B24/02-architecture-plan.md` (REVIEW_PASS)
**Defect Closed**: DW-B23-BE-ALLACCOUNTS-01

---

## Ticket Map

| Ticket | File(s) | Work |
|--------|---------|------|
| T1 | `src/PropTraderTools/CopyEngine.cs` | New `BreakEven(Account, Instrument, int)` overload + fix `OnPendingBeAccountUpdate` call site |
| T2 | `src/PropTraderTools/TradeCopierPanel.cs` + `src/PropTraderTools/CopyEngineTests.cs` | Update 5 panel call sites + add 2 `[Fact]` tests |

**Dependency**: T2 depends on T1 (new overload must exist before call sites can compile).

---

## T1 — CopyEngine.cs: Add `BreakEven(Account, Instrument, int)` Overload + Fix `OnPendingBeAccountUpdate`

### Spec Requirement IDs
- `DW-B23-BE-ALLACCOUNTS-01` — defect: BreakEven silent when no rule registered
- `STEP1` — insert new overload that routes leader directly to `MoveStopToBreakEven`
- `STEP2a` — update `OnPendingBeAccountUpdate` (CopyEngine.cs:1396) to supply `acc` as leader

### File Path
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

### Method Signatures

#### NEW — insert at line 1181 (immediately after existing `BreakEven(Instrument, int)` at line 1176)

```csharp
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
```

Full body:
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
- Branch 1: `if (leader == null)` — early return
- Branch 2: `foreach (var acc in AllAccounts(instrument))` — loop
- Branch 3: `if (acc == leader) continue` — skip duplicate
- CYC = 1 (base) + 3 = **4** ✓ (≤ 8 limit)

#### MODIFIED — `OnPendingBeAccountUpdate` at line 1396 (single-line change only)

```csharp
private void OnPendingBeAccountUpdate(object sender, NinjaTrader.Cbi.AccountItemEventArgs e)
```

**Exact single-line diff at line 1396**:
```
- BreakEven(instr, buf);
+ BreakEven(acc, instr, buf);
```

`acc` is the local variable already captured at **line 1389**: `var acc = _pendingBeAccount;`
No other lines in this method are touched.

### JS Rule Constraints

| Rule | Severity | Constraint | How Satisfied |
|------|----------|------------|---------------|
| JS-021 | P0 | No `lock()` anywhere in new/modified code | New overload is lock-free; single-line change at 1396 adds no lock |
| JS-002 | P0 | Null leader → `StatusUpdate?.Invoke(...)` + `return`; never fall-through | Branch 1 of new overload fires StatusUpdate then returns |
| JS-001 | P0 | No `throw new XxxException(...)` in new code | No throw statement in new overload |
| JS-033 | P0 | Not `async void` — synchronous `void` only | Method declared `internal void`, not `async void` |
| CYC ≤ 8 | P0 | New method cyclomatic complexity ≤ 8 | CYC = 4 (3 branches + 1 base) |
| NT8-043 | P1 | No null-conditional event unsubscription (`?.Event -= handler`) | New overload has zero event subscriptions/unsubscriptions |

### xUnit `[Fact]` Test Names (implemented in T2)
- `BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull`
- `BreakEven_AccountOverload_NullInstrument_NoException`

### Unchanged-Code Contract (T1 scope — engineer must verify byte-for-byte)

| Symbol | File | Line | Must Remain Unchanged |
|--------|------|------|-----------------------|
| `AllAccounts(Instrument)` | CopyEngine.cs | 1050 | Not the defect site — do not touch |
| `FindRule(Instrument)` | CopyEngine.cs | 1064 | Not the defect site — do not touch |
| `MoveStopToBreakEven(Account, Instrument, int)` | CopyEngine.cs | 1133 | Core execution — do not touch |
| `BreakEven(Instrument, int)` | CopyEngine.cs | 1176 | TrailBe depends on this 2-param form — do not modify |
| All lines in `OnPendingBeAccountUpdate` except line 1396 | CopyEngine.cs | 1364–1400 | Single-line change only |

### T1 Seven-Scan Checklist (engineer must check off before commit)

**SCAN-01** — JS-021: No `lock()` in write-set
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\TradeCopierPanel.cs" -Pattern "lock\s*\(" | Select-Object LineNumber, Line
```
**Pass criterion**: Zero matches in new/modified code.

**SCAN-02** — JS-002: Null leader path emits StatusUpdate string
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-BE: leader null -- BE skipped"
```
**Pass criterion**: Exactly 1 match — inside new `BreakEven(Account, Instrument, int)` overload.

**SCAN-03** — CYC ≤ 8: New overload complexity
```powershell
python scripts/complexity_audit.py src\PropTraderTools\CopyEngine.cs
```
**Pass criterion**: `BreakEven(Account, Instrument, int)` reports CYC ≤ 8 (expected: CYC = 3 or 4).

**SCAN-04** — Overload coexistence: 2-param overload unchanged
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "internal void BreakEven\(Instrument"
```
**Pass criterion**: Exactly 1 match at the original line. Body unchanged.

**SCAN-05** — NT8 compile gate: No stale 2-param calls from CopyEngine call site
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "BreakEven\(instr, buf\)"
```
**Pass criterion**: Zero matches (line 1396 now reads `BreakEven(acc, instr, buf)`).

**SCAN-06** — `[Fact]` count: test baseline (after T2 adds tests)
```powershell
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Pass criterion**: Count = 126 (T1 does not add tests; T2 raises count to 128).

**SCAN-07** — NT8-043: No null-conditional event unsubscription in write-set
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\?\.\w+\s*-="
```
**Pass criterion**: Zero matches.

---

## T2 — TradeCopierPanel.cs + CopyEngineTests.cs: Update 5 Call Sites + Add 2 Tests

### Spec Requirement IDs
- `DW-B23-BE-ALLACCOUNTS-01` — defect closed by T1; T2 wires all callers to the new overload
- `STEP2b-f` — update 5 TradeCopierPanel.cs call sites to 3-param form
- `STEP3` — add 2 `[Fact]` tests to `CopyEngineTests.cs` (baseline 126 → target 128)

### Dependency
**T2 must not start until T1 is committed.** The new `BreakEven(Account, Instrument, int)` overload must exist in `CopyEngine.cs` before any of the 5 call-site changes below will compile.

### File Paths
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```

### Method Signatures (Modified — call-site lines only)

All 5 methods below have **one line changed each**. No other lines in these methods are touched.

| Method | File | Line Changed | Old Call | New Call |
|--------|------|-------------|----------|----------|
| `OnBeUp` | TradeCopierPanel.cs | 782 | `_engine.BreakEven(_instrument, _beBuffer)` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` |
| `OnBeDown` | TradeCopierPanel.cs | 791 | `_engine.BreakEven(_instrument, _beBuffer)` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` |
| `OnBeConnected` | TradeCopierPanel.cs | 859 | `_engine.BreakEven(_instrument, _beBuffer)` | `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer)` |
| `OnBreakEven` | TradeCopierPanel.cs | 1299 | `_engine.BreakEven(_instrument, ticks)` | `_engine.BreakEven(_leaderAccount, _instrument, ticks)` |
| `DispatchShortcut` (Key.B branch) | TradeCopierPanel.cs | 1418 | `_engine.BreakEven(_instrument, buf)` | `_engine.BreakEven(_leaderAccount, _instrument, buf)` |

**Source of `_leaderAccount`**: `private Account _leaderAccount` declared at
[`TradeCopierPanel.cs:120`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs:120).
The field is set on account selection (line 388) and cleared on disconnect (line 406). It is `null`
when no account is selected. The new overload's Branch 1 null-guard handles this cleanly — no
additional null checks required at call sites.

### xUnit `[Fact]` Tests — Append to `CopyEngineTests.cs` before the closing `}`

**Test 1** — guards `REQ-B24-01` and `REQ-B24-03`

```csharp
[Fact]
public void BreakEven_WithLeaderAccount_NoRule_FiresStatusUpdateLeaderNull()
{
    // Arrange
    string received = null;
    _engine.StatusUpdate += msg => received = msg;
    // Act + Assert: no throw
    var ex = Record.Exception(() => _engine.BreakEven((Account)null, (Instrument)null, 2));
    Assert.Null(ex);
    // Assert: diagnostic fired
    Assert.Equal("PTT-BE: leader null -- BE skipped", received);
    _engine.StatusUpdate -= msg => received = msg;
}
```

**What it verifies**: Branch 1 of the new overload. `null` leader triggers `StatusUpdate?.Invoke(...)` with the exact sentinel string, then returns without throwing. Deterministic assertion: `Assert.Equal` checks exact string equality.

**Test 2** — guards `REQ-B24-02`

```csharp
[Fact]
public void BreakEven_AccountOverload_NullInstrument_NoException()
{
    // Arrange: any non-null Account stub — use Account.All[0] if available, else reflection
    Account stub = Account.All.Count > 0 ? Account.All[0] : null;
    // Act: null instrument path -> AllAccounts(null) -> yields nothing safely
    var ex = Record.Exception(() => _engine.BreakEven(stub, (Instrument)null, 2));
    // Assert: no exception
    Assert.Null(ex);
}
```

**What it verifies**: When leader is non-null and instrument is `null` (no rule will match),
`MoveStopToBreakEven(leader, null, 2)` is called. `MoveStopToBreakEven` internally calls
`FindPosition(acc, null)` which handles null gracefully. `AllAccounts(null)` yields nothing
(no rule). No `NullReferenceException` escapes. Deterministic assertion: `Record.Exception` must be `null`.

**Engineer note**: Use the `CreateStubAccount()` helper already established in `CopyEngineTests.cs`
if `Account.All.Count == 0` in the test environment. Do NOT import new test infrastructure or add
new test helper methods — reuse existing patterns only.

### JS Rule Constraints

| Rule | Severity | Constraint | How Satisfied |
|------|----------|------------|---------------|
| JS-021 | P0 | No `lock()` anywhere in modified code | All 5 changes are single-line call-site rewrites; no lock introduced |
| JS-002 | P0 | Null leader handled by new overload — call sites pass `_leaderAccount` which may be null | New overload's Branch 1 handles null; no additional null checks at call sites |
| JS-001 | P0 | No `throw new XxxException(...)` in new test code | Tests use `Record.Exception` pattern — no throw statements |
| NT8-032 | P2 | Tests co-located in PropTraderTools assembly — no separate test .csproj | Tests appended to existing `CopyEngineTests.cs` — no new file or project |

### Unchanged-Code Contract (T2 scope)

All methods in `TradeCopierPanel.cs` are modified at **single call-site lines only**. The engineer must verify:
- `OnBeUp`: only line 782 changes; all other lines identical
- `OnBeDown`: only line 791 changes; all other lines identical
- `OnBeConnected`: only line 859 changes; all other lines identical
- `OnBreakEven`: only line 1299 changes; all other lines identical
- `DispatchShortcut`: only line 1418 changes; all other lines identical
- `CopyEngineTests.cs`: 126 existing `[Fact]` methods are not modified; 2 new methods appended at end

### T2 Seven-Scan Checklist (engineer must check off before commit)

**SCAN-01** — JS-021: No `lock()` in write-set
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\TradeCopierPanel.cs" -Pattern "lock\s*\(" | Select-Object LineNumber, Line
```
**Pass criterion**: Zero matches in new/modified code.

**SCAN-02** — JS-002: Null leader path emits StatusUpdate string
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-BE: leader null -- BE skipped"
```
**Pass criterion**: Exactly 1 match — inside `BreakEven(Account, Instrument, int)` overload (from T1).

**SCAN-03** — CYC ≤ 8: Modified methods in TradeCopierPanel.cs
```powershell
python scripts/complexity_audit.py src\PropTraderTools\TradeCopierPanel.cs
```
**Pass criterion**: `OnBeUp`, `OnBeDown`, `OnBeConnected`, `OnBreakEven`, `DispatchShortcut` all report CYC unchanged (single-line parameter change does not alter cyclomatic complexity).

**SCAN-04** — Overload coexistence: 2-param overload unchanged
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "internal void BreakEven\(Instrument"
```
**Pass criterion**: Exactly 1 match at the original line. Body unchanged.

**SCAN-05** — All 5 TradeCopierPanel call sites migrated to 3-param form
```powershell
Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "_engine\.BreakEven\(_instrument"
```
**Pass criterion**: Zero matches (all sites now supply `_leaderAccount` as first argument).

**SCAN-06** — `[Fact]` count = 128
```powershell
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Pass criterion**: Count = 128 (126 existing + 2 new).

**SCAN-07** — NT8-043: No null-conditional event unsubscription in write-set
```powershell
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\?\.\w+\s*-="
```
**Pass criterion**: Zero matches.

---

## Spec Coverage Matrix (Both Tickets)

| Requirement ID | Description | Ticket | Plan Section |
|----------------|-------------|--------|-------------|
| REQ-B24-01 | BreakEven fires for leader when no rule registered | T1 | Section 2, STEP 1 |
| REQ-B24-02 | Follower fan-out preserved when rules exist (backward compat) | T1 | Section 2, STEP 1 |
| REQ-B24-03 | null leader guard emits StatusUpdate and returns | T1 | Section 2, STEP 1 Branch 1 |
| REQ-B24-04 | All 6 call sites updated to 3-param form | T1 (1 site) + T2 (5 sites) | Section 2, STEP 2 |
| REQ-B24-05 | Test count 126 → 128 | T2 | Section 2, STEP 3 |
| DW-B23-BE-ALLACCOUNTS-01 | Defect closed | T1 + T2 | Root cause eliminated |

---

## Execution Order

```
T1 → commit → T2 → commit → run 7-scan checklist on both → F5 NT8 compile
```

1. **T1**: Insert new overload at `CopyEngine.cs:1181` + change line 1396. Compile. Verify T1 SCAN-01 through SCAN-07.
2. **T2**: Update 5 `TradeCopierPanel.cs` call sites + append 2 tests to `CopyEngineTests.cs`. Compile. Verify T2 SCAN-01 through SCAN-07.
3. **F5 Gate**: Load in NinjaTrader, F5 compile. Zero errors = DONE.

**Write-set boundary** (no other files modified):
- `src/PropTraderTools/CopyEngine.cs` — T1 only
- `src/PropTraderTools/TradeCopierPanel.cs` — T2 only
- `src/PropTraderTools/CopyEngineTests.cs` — T2 only

---

*Generated by ptt-architect · PTT-COPIER-B24 · Phase 3 · REVIEW_PASS plan as input*
