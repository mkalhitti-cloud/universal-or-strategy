# B182 Architecture Plan
## Ph1 Output -- ptt-architect
### Date: 2026-09-28
### Status: READY_FOR_REVIEW

---

## 0. Baseline State Confirmed

Direct repair already in place (2026-09-28 direct session):
- `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- DW-B182-01 + DW-B182-02 applied
- `src/PropTraderTools/Features/PttBeSwapOne.cs`      -- DW-B182-02 caller corrected
- `src/PropTraderTools/Features/PttBeSwapAll.cs`      -- DW-B182-02 caller corrected
- `src/PropTraderTools/Tests/BwaveRefactorLaneCTests.cs` -- pre-existing compile fix

dotnet build: 0 errors, 1 pre-existing warning (xUnit2004 in B131Tests.cs). SIM PASS.

Ph4a engineer MUST read these 3 files before any edit and MUST NOT re-apply the direct repair.

---

## 1. DW Items in Scope

### DW-B182-01 (CLOSED by direct repair -- verify in pipeline)
**Bug:** `PttBreakEvenSwap.SubmitSwapPair` target limitPrice was `t.Price` (original ATM target)
instead of `newStop` (entry/BE level).
**Fix already applied:** `arg6: newStop` in the Limit CreateOrder call (line 193).
**Verify action:** Confirm source at `PttBreakEvenSwap.cs:193` reads `newStop` not `t.Price`.

### DW-B182-02 (CLOSED by direct repair -- verify in pipeline)
**Bug:** Missing QX-pattern: stop price must be snapshotted BEFORE CancelQxBrackets; the old
`IsStopPriceSubmittable` guard was removed since snapshotStop from pre-cancel ATM is always valid.
**Fix already applied:**
  - `PttBreakEvenSwap.Execute` receives `snapshotStop` param; no `IsStopPriceSubmittable` guard.
  - `PttBeSwapOne.Execute` calls `PttQuickExit.SnapshotStopPrice` BEFORE `PttBreakEvenSwap.Execute`.
  - `PttBeSwapAll.ExecuteOne` calls `PttQuickExit.SnapshotStopPrice` BEFORE `PttBreakEvenSwap.Execute`.
**Verify action:** Confirm `SnapshotStopPrice` call precedes `PttBreakEvenSwap.Execute` in both callers.

### DW-B179 (OPEN -- implement in Ticket 2)
**Problem:** When position goes flat (manual flatten or native close) while PTT-BE-* orders are
working, those orders cancel silently -- no user feedback, no visual reset.
**Design:** In `OnOrderUpdate`, add a detect path:
  - When a `PTT-BE-*` or `PTT-QX-*` order transitions to `Cancelled`...
  - ...AND the account's position for that instrument is now 0 (or already 0 at cancel time)...
  - ...emit `[BE-WARN] {acc.Name} flatten overtook BE All -- orders cancelled` to `OutputTab1`.
**Constraint:** Stateless -- B180 BE All path is stateless. Detection via order name prefix check
  + position quantity check. No armed-state machine. `IsFollowerAccount` exemption: emit for all
  accounts (leader and follower both benefit from the warn).
**Method:** Extract to `TryEmitBeFlattenWarn(OrderEventArgs e)` called from `OnOrderUpdate`
  pre-Gate-1 block. CYC target: <= 4. Called unconditionally (same as TryFireFollowerBeDisarm).

### DW-B181-G5 (OPEN -- implement in Ticket 3)
**Problem:** No startup check warns the user when ATM templates have Reverse=Yes, which causes
stop fills to create reverse entries -- a live trading hazard invisible from the PTT logs.
**Design:** In `OnStateChange` at `State.Configure` in `TradeCopierAddOn.cs`:
  - Call `CopyEngine.Instance.CheckAtmTemplatesForReverse()`.
  - `CheckAtmTemplatesForReverse` enumerates `NinjaTrader.Core.Globals.UserDataDir + Templates\ATM\*.xml`.
  - Parses each XML file for `<Reverse>true</Reverse>` (case-insensitive) or attribute `Reverse="true"`.
  - If found: emit `[PTT-STARTUP-WARN] ATM template {name} has Reverse=Yes -- stop fills will
    create reverse entries. Disable Reverse in this template before trading.`
  - Emit once per template per session (use a `HashSet<string>` of already-warned names).
  - If directory not found: emit `[PTT-STARTUP-INFO] ATM template dir not found -- skipping Reverse check`.
  - Fail-open: any I/O exception is caught and logged; no crash.

---

## 2. Ticket Structure

### Ticket 1: Verify DW-B182-01 + DW-B182-02 + xUnit tests
**Scope:** Tests only. No source changes beyond adding a new test file.
**New file:** `src/PropTraderTools/Tests/B182Tests.cs`
**Tests (4 [Fact] methods):**
  1. `BE_SubmitSwapPair_TargetPriceIsEntryNotOriginalAtm` -- pure math: verify newStop used for target limitPrice
  2. `BE_SubmitSwapPair_StopPriceIsSnapshotNotEntry` -- pure math: verify snapshotStop used for stop
  3. `BE_SubmitBareStopSwap_UsesSnapshotStop` -- pure math: verify 0-targets path uses snapshotStop
  4. `BE_Execute_NoIsStopPriceSubmittableGuard` -- structural: verify signature has snapshotStop param
**Note:** All 4 tests are pure-math / structural (no NT8 runtime objects). CYC=1 per test.

### Ticket 2: DW-B179 -- flatten race warn
**Scope:** `CopyEngine.cs` -- add `TryEmitBeFlattenWarn` helper, wire into `OnOrderUpdate`.
**Signal:** `PTT-BE-` or `PTT-QX-` order cancelled + position qty == 0 at cancel time.
**Output:** `[BE-WARN] {acc.Name} flatten overtook BE All -- orders cancelled` to OutputTab1.
**New test:** `BE_Flatten_AfterBeAll_EmitsWarnLog` in `B182Tests.cs`.
**Note:** Test uses same pure-math / logic seam approach as B180Tests.cs. NT8-touching path
  uses `[Fact(Skip="...")]` if Account/Position seam not available.

### Ticket 3: DW-B181-G5 -- ATM template Reverse check
**Scope:** `CopyEngine.cs` -- add `CheckAtmTemplatesForReverse()`. `TradeCopierAddOn.cs` -- wire call.
**New test:** `StartupWarn_EmitsForReverseYesTemplate` in `B182Tests.cs` using an in-memory XML string.
**Seam:** `CheckAtmTemplatesForReverse(string dirPath)` overload accepting a directory path for testing.

---

## 3. Method Designs

### 3.1 TryEmitBeFlattenWarn (CopyEngine.cs)
```
// CYC=4: (1) order state != Cancelled, (2) name prefix check, (3) position qty check, (4) emit.
// Called from OnOrderUpdate pre-Gate-1 block.
// JS-001: no throw. JS-021: no lock. ASCII-only.
private void TryEmitBeFlattenWarn(OrderEventArgs e)
{
    if (e.Order.OrderState != OrderState.Cancelled) return;          // (1)
    var name = e.Order.Name ?? string.Empty;
    if (!name.StartsWith("PTT-BE-") && !name.StartsWith("PTT-QX-")) return; // (2)
    var pos = FindPositionPublic(e.Order.Account, e.Order.Instrument);
    if (pos != null && pos.Quantity > 0) return;                     // (3) position still open -- not a flatten race
    NinjaTrader.Code.Output.Process(                                  // (4)
        "[BE-WARN] " + e.Order.Account.Name + " flatten overtook BE All -- orders cancelled",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

### 3.2 CheckAtmTemplatesForReverse (CopyEngine.cs)
```
// CYC=6: (1) try/catch, (2) dir not found, (3) foreach file, (4) xml parse, (5) reverse check, (6) already warned.
// JS-001: no throw (try/catch). JS-002: void. JS-021: no lock. ASCII-only.
// Called from TradeCopierAddOn.OnStateChange at State.Configure.
internal void CheckAtmTemplatesForReverse()
    => CheckAtmTemplatesForReverse(
        Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "Templates", "ATM"));

internal void CheckAtmTemplatesForReverse(string atmDir)
{
    try
    {
        if (!Directory.Exists(atmDir))
        {
            NinjaTrader.Code.Output.Process(
                "[PTT-STARTUP-INFO] ATM template dir not found -- skipping Reverse check",
                PrintTo.OutputTab1);
            return;
        }
        foreach (var file in Directory.GetFiles(atmDir, "*.xml"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (_warnedAtmTemplates.Contains(name)) continue;
            var xml = File.ReadAllText(file);
            if (xml.IndexOf("<Reverse>true</Reverse>", StringComparison.OrdinalIgnoreCase) < 0
                && xml.IndexOf("Reverse=\"true\"", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            _warnedAtmTemplates.Add(name);
            NinjaTrader.Code.Output.Process(
                "[PTT-STARTUP-WARN] ATM template " + name
                    + " has Reverse=Yes -- stop fills will create reverse entries."
                    + " Disable Reverse in this template before trading.",
                PrintTo.OutputTab1);
        }
    }
    catch (Exception ex)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-STARTUP-WARN] ATM template scan failed: " + ex.Message,
            PrintTo.OutputTab1);
    }
}
```
**New field in CopyEngine:** `private readonly HashSet<string> _warnedAtmTemplates = new HashSet<string>();`
CYC of `CheckAtmTemplatesForReverse(string)` = 6: try(1), dir-not-found(2), foreach(3), already-warned(4), xml-check(5), emit(6). PASS <= 8.

---

## 4. Wire-up Changes

### TradeCopierAddOn.OnStateChange (State.Configure block)
Add one call after `CopyEngine.Instance.SetFlags(flags)`:
```csharp
CopyEngine.Instance.CheckAtmTemplatesForReverse();
```
No other changes to TradeCopierAddOn.

### OnOrderUpdate pre-Gate-1 block
Add one call after `TryEvictFollowerBeSlot(e)` (before the drain block):
```csharp
TryEmitBeFlattenWarn(e);
```

---

## 5. CYC Audit

| Method | New CYC | Limit | Status |
|--------|---------|-------|--------|
| TryEmitBeFlattenWarn | 4 | 8 | PASS |
| CheckAtmTemplatesForReverse(string) | 6 | 8 | PASS |
| OnOrderUpdate (delta) | +1 branch | must stay <= 8 | VERIFY |

OnOrderUpdate pre-gate additions (TryEmitBeFlattenWarn call) adds 0 CYC to OnOrderUpdate itself
(it is an unconditional delegation, same as TryFireFollowerBeDisarm pattern at line 1367).

---

## 6. Jane Street Constraints

- JS-001: no throw in any new method (try/catch wraps I/O in CheckAtmTemplatesForReverse).
- JS-002: no return null (all methods void).
- JS-021: no lock (HashSet<string> _warnedAtmTemplates is only written at State.Configure -- single-threaded startup).
- JS-033: no async void (all synchronous void).
- NT8-014: all signal prefixes: `[BE-WARN]`, `[PTT-STARTUP-WARN]`, `[PTT-STARTUP-INFO]`.
- ASCII-only: all string literals use ASCII only.

---

## 7. ptt-sync.ps1 Mandate

After every .cs edit Ph4a must run:
```powershell
powershell -File .\scripts\ptt-sync.ps1
```
All 21 hard links must report LINKED. dotnet build must pass 0 errors after each ticket.
