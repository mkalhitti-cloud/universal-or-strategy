# B186 Orchestrator Prompt
# Paste into ptt-orchestrator session to kick off B186.

$execute-pipeline | PTT-COPIER B186 | Quick ALL + QAll2t Instrument Scope

## Context

Previous pipeline (B185) scoped BE ALL to `_instrument` using the PttBeSwapAll pattern.
B186 applies the identical pattern to `PttGlobalQuickExit` (Quick ALL and QAll2t buttons).

## Baseline

- Commit: 0c27cd88 (B185-POST -- last clean, NT8 compile verified)
- Spec: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html (B186 section)
- Continue prompt: docs/brain/B186/00-continue-prompt.md

## Files in scope

| File | Tickets |
|------|---------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T1, T2 |
| `src/PropTraderTools/TradeCopierPanel.cs` | T3 |
| `src/PropTraderTools/CopyEngineTests.cs` | T1+T2+T3 (6 tests) |

## Pipeline (ALL phases mandatory -- NONE skippable -- NONE combinable)

Ph1  ptt-architect       -> 02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> 02-plan-review.md       (REVIEW_PASS gate)
Ph3  ptt-architect       -> 04-tickets.md
Ph3.5 ptt-ticket-reviewer -> 04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer        -> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md

## T1 spec (ptt-architect input)

### DW-B186-QX-ALL-SCOPE-01: PttGlobalQuickExit.Execute() instrument scope

File: Features/PttGlobalQuickExit.cs

Current:
```csharp
internal void Execute()
{
    if (!CopyEngine.Instance.Flags.QxGlobalExit) return;
    // ...
    foreach (Account acc in Account.All)
    {
        if (engine.IsFollowerAccount(acc)) continue;
        foreach (Position pos in acc.Positions)
        {
            if (pos == null || pos.Quantity == 0) continue;
            // ... (loops ALL instruments)
        }
    }
}
```

After (CYC: 7->8):
```csharp
internal void Execute(Instrument instrument)
{
    if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return; // (1) combined gate
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] GlobalQuickExit fired instr=" + instrument.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    // ...
    foreach (Account acc in Account.All)                                        // (existing)
    {
        if (engine.IsFollowerAccount(acc)) continue;
        foreach (Position pos in acc.Positions)
        {
            if (pos == null || pos.Quantity == 0) continue;
            if (pos.Instrument?.FullName != instrument.FullName) continue;       // (new) +1 CYC -> 8
            // ... rest unchanged
        }
    }
}
```

Tests (NT8-runtime skip pattern -- same as T_B185_01/02):
- T_B186_01: Execute method has exactly 1 parameter of type Instrument (reflection)
- T_B186_02: Execute(null) returns immediately -- structural via reflection on combined gate

## T2 spec (ptt-architect input)

### DW-B186-QX-ALL-SCOPE-02: PttGlobalQuickExit.Execute(forcedTargets) instrument scope

File: Features/PttGlobalQuickExit.cs

Current signature: `internal void Execute(List<(double Price, int Qty)> forcedTargets)`
New signature:     `internal void Execute(List<(double Price, int Qty)> forcedTargets, Instrument instrument)`

CYC hold strategy: fold instrument filter into existing null/flat guard (branch (6)):
```csharp
// WAS:
if (pos == null || pos.Quantity == 0) continue; // (6)

// BECOMES:
if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName) continue; // (6) extended
```
This keeps CYC at 8 (extends existing branch rather than adding a new one).

Combined gate: `if (!Flags.QxGlobalExit || instrument == null) return;` -- folds instrument null check
into existing flag gate at branch (1), same as T1 and B185.

Tests:
- T_B186_04: Execute(forcedTargets, Instrument) method has exactly 2 parameters (reflection)
- T_B186_05: Execute(forcedTargets, null) returns immediately (structural)

## T3 spec (ptt-architect input)

### DW-B186-PANEL-CALLSITES-01: TradeCopierPanel.cs call sites

File: TradeCopierPanel.cs

Change 1 -- OnQuickAllClick (line ~1617):
```csharp
// WAS:
gqx.Execute();
// BECOMES:
gqx.Execute(_instrument);
```

Change 2 -- OnInstrQAll2tClick (line ~1706):
```csharp
// WAS:
new PttGlobalQuickExit().Execute(targets);
// BECOMES:
new PttGlobalQuickExit().Execute(targets, _instrument);
```

No null guard needed at call site. Both Execute() overloads gate on instrument == null in combined gate.
Pattern is identical to OnGlobalBeClick passing _instrument (B185 T1).

Tests:
- T_B186_03: reflection confirms OnQuickAllClick method body contains call with _instrument
- T_B186_06: reflection confirms OnInstrQAll2tClick method body contains call with _instrument

## DNA constraints (ALL tickets)

- JS-021: no lock()
- JS-001: no throw new in hot paths
- JS-002: no return null
- JS-033: no async void
- ASCII-only in all new string literals
- CYC <= 8 for all modified methods

## Validation checklist (ptt-verifier Ph4b)

```powershell
# SCAN-01: no new lock()
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("

# SCAN-02: no new return null
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"

# SCAN-03: no new async void
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "

# SCAN-04: CYC manual count
# Execute(Instrument): verify 8 branches
# Execute(forcedTargets, Instrument): verify 8 branches

# SCAN-05: no non-ASCII
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"

# SCAN-06: no throw new
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"

# SCAN-07: build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 5

# AC tests
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "T_B186_"
Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "Execute.*Instrument"
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "gqx\.Execute.*_instrument"

# Guardian + ptt-sync
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
```

Expected: NT8 build tag `PTT-COPIER B186 | qx-all-instr-scope | {date}`

---
*Written: 2026-07-08*
*Baseline: 0c27cd88*
