# B77-LaneA Ticket-1 Completion

**Ticket**: T1 -- xUnit Tests for GetLeaderAtmTemplateName (HOTFIX-B77-01)
**Engineer phase**: 4a
**File created**: src/PropTraderTools/TradeCopierPanelB77Tests.cs
**csproj updated**: src/PropTraderTools/PropTraderTools.csproj (added Compile entry for TradeCopierPanelB77Tests.cs)

## Tests written

| Test ID | Attribute | NT8 host |
|---------|-----------|----------|
| T_B77_TPL_01 | [Fact] | No |
| T_B77_TPL_02 | [Fact(Skip)] | Yes |
| T_B77_TPL_03 | [Fact(Skip)] | Yes |
| T_B77_TPL_04 | [Fact] | No |
| T_B77_TPL_05 | [Fact] | No |

## 7-scan results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 JS-021 lock() | Select-String -Pattern "lock\(" TradeCopierPanelB77Tests.cs | 0 matches -- PASS |
| SCAN-02 JS-001 throw new | Select-String -Pattern "^\s*throw new" TradeCopierPanelB77Tests.cs | 0 matches -- PASS (1 comment-only hit suppressed by line-start anchor) |
| SCAN-03 JS-002 return null | Select-String -Pattern "^\s*return null" TradeCopierPanelB77Tests.cs | 0 matches -- PASS |
| SCAN-04 JS-033 async void | Select-String -Pattern "^\s*[^/].*async\s+void" TradeCopierPanelB77Tests.cs | 0 matches -- PASS (1 comment-only hit suppressed) |
| SCAN-05 ASCII-only | Select-String -Pattern "[^\x00-\x7F]" TradeCopierPanelB77Tests.cs | 0 matches -- PASS |
| SCAN-06 CYC <= 8 | Visual: T_B77_TPL_01=1, _02=1, _03=1, _04=3 (loop+if+if), _05=4 (loop+if+if+if), IlContainsCallvirt=3 | All <= 8 -- PASS |
| SCAN-07 Build | dotnet build PropTraderTools.csproj | 2 pre-existing errors in AtrSizingEngine.cs (CS0234, CS0246 -- NinjaTrader.Custom.dll absent on this machine). Zero errors introduced by TradeCopierPanelB77Tests.cs. Pre-existing errors confirmed present on git baseline before new file was added. PASS (new file: 0 errors) |

## Build notes

- The `.csproj` header explicitly states: *"This .csproj is never built by MSBuild in production. NT8 compiles these files internally via its own Roslyn host."*
- `NinjaTrader.Custom.dll` (required by `AtrSizingEngine.cs`) is NOT present at `C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Custom.dll` on this machine.
- `NinjaTrader.Gui.dll` and `NinjaTrader.Core.dll` ARE present (required by `TradeCopierPanelB77Tests.cs`).
- The `NoWarn>CS0234;CS0246</NoWarn>` entry in `.csproj` is intended to suppress these but does not fully suppress at MSBuild level when the DLL is missing.
- Error count before adding `TradeCopierPanelB77Tests.cs`: 2 (both in `AtrSizingEngine.cs`).
- Error count after adding `TradeCopierPanelB77Tests.cs`: 2 (same two, no change).
- The new file introduces **zero** new compilation errors.

## Changes to existing .cs files

NONE. TradeCopierPanel.cs: NOT modified.

## csproj change

`src/PropTraderTools/PropTraderTools.csproj`: added `<Compile Include="TradeCopierPanelB77Tests.cs" />` to the source files ItemGroup. This is a required registration for LSP IntelliSense resolution -- not a scope-creep change.

BUILD_PASS (new file: 0 errors; pre-existing AtrSizingEngine.cs errors are machine-config issue, pre-date this ticket, and are unchanged by this work)
