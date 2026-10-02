# B185 $continue Prompt
# Paste this entire block into a new session to resume.

$continue | PTT-COPIER B185 | BE ALL Instrument Scope + DispatchCopy CYC Headroom + Click-Trader Qty + B184 Tests

## Situation

B185 pipeline ran externally. Changes are UNCOMMITTED (working tree dirty) -- no commit yet.
Your first job is to validate the output, then tell the Director when it is safe to compile
in NinjaTrader.

## State at handoff

- **Baseline commit**: c72f0cb7 (B184-T2 -- last clean commit)
- **Working tree**: 5 modified files, not committed:
  - src/PropTraderTools/CopyEngine.cs
  - src/PropTraderTools/CopyEngineTests.cs
  - src/PropTraderTools/Features/PttBeSwapAll.cs
  - src/PropTraderTools/PropTraderTools.Tests.csproj
  - src/PropTraderTools/TradeCopierPanel.cs
- **Brain dir**: docs/brain/B185/
- **Orchestrator prompt**: docs/brain/B185/00-orchestrator-prompt.md  (READ THIS FIRST)
- **Spec**: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html

## B185 Ticket Scope (what the pipeline was supposed to do)

### T1 -- BE ALL instrument scope change
- `PttBeSwapAll.Execute(int)` -> `Execute(int bufferTicks, Instrument instrument)`
- Inner position loop skips positions whose instrument does not match the passed instrument
- Null guard + feature gate combined: `if (!flags.BreakEven || instrument == null) return;`
- `OnGlobalBeClick` in TradeCopierPanel.cs passes `_instrument` as second arg
- Execute CYC target: <= 8 (was 6, +1 for filter = 7 preferred)
- 3 new tests: T_B185_01, T_B185_02, T_B185_03

### T2 -- DispatchCopy helper extraction (DW-B184-01)
- Extract `TryGetLastDirection` and `RecordLastDirection` from inline DispatchCopy body
- DispatchCopy CYC stays at 8 (no branches removed -- extraction only)
- 3 new tests: T_B185_04, T_B185_05, T_B185_06

### T3 -- Click-trader qty + _laneDDispatched eviction (DW-B184-02 + DW-B183-04)
- `_clickQty = 1` field on TradeCopierPanel replaces hardcoded literal 1
- `LaneDEvictThreshold = 10000` constant + eviction branch in TryLaneDHook
- 2 new tests: T_B185_07, T_B185_08

### T4 -- 14 unwritten B184 tests (DW-B184-03)
- 6 T1-group tests (BeState enum, BE event types, DisarmPendingBe, etc.)
- 8 T2-group tests (FeatureFlags arity, ATR-symbol-absence checks via reflection)
- All added to CopyEngineTests.cs
- Test project EXISTS -- no infrastructure work needed

## Your validation steps (run in order)

### Step 1 -- Read the changes
```powershell
git diff HEAD -- src/PropTraderTools/Features/PttBeSwapAll.cs
git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs
git diff HEAD -- src/PropTraderTools/CopyEngine.cs
git diff HEAD -- src/PropTraderTools/CopyEngineTests.cs
git diff HEAD -- src/PropTraderTools/PropTraderTools.Tests.csproj
```
Read each diff carefully against the T1-T4 acceptance criteria above.

### Step 2 -- Run all 7 DNA scans on modified files (new lines only)
```powershell
# SCAN-01: no new lock()
git diff HEAD -- src/ | Select-String "^\+" | Select-String "lock\("

# SCAN-02: no new return null
git diff HEAD -- src/ | Select-String "^\+" | Select-String "return null"

# SCAN-03: no new async void
git diff HEAD -- src/ | Select-String "^\+" | Select-String "async void "

# SCAN-04: CYC -- read PttBeSwapAll.Execute and DispatchCopy and TryLaneDHook
#          count branches manually, assert <= 8

# SCAN-05: no non-ASCII in new lines
git diff HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"

# SCAN-06: no new throw new
git diff HEAD -- src/ | Select-String "^\+" | Select-String "throw new"

# SCAN-07: build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10
```

### Step 3 -- Run tests
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build 2>&1 | Select-Object -Last 20
```
Expected: 0 failures. Expected [Fact] count >= 77 (63 baseline + at minimum T1+T4 tests).

### Step 4 -- Check key acceptance criteria
```powershell
# T1-AC1: Execute signature has Instrument parameter
Select-String -Path src/PropTraderTools/Features/PttBeSwapAll.cs -Pattern "Execute.*Instrument"

# T1-AC2: OnGlobalBeClick passes _instrument
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "PttBeSwapAll.Execute.*_instrument"

# T2-AC1: helper methods present
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "TryGetLastDirection|RecordLastDirection"

# T3-AC1: _clickQty field present
Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "_clickQty"

# T3-AC3: eviction constant present
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "LaneDEvictThreshold"

# T4: 14 named tests present
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "T_B184_T1|T_B184_T2"
```

### Step 5 -- Guardian script + ptt-sync
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
```

### Step 6 -- Spec update check
Read the spec to confirm DW-B185-BE-ALL-SCOPE-01 card was added and B39 scope table updated:
```powershell
Select-String -Path "..\universal-or-strategy-director\specs\002-trade-copier-spec.html" -Pattern "DW-B185|BE-ALL-SCOPE|instrument.*panel.*_instrument" | Select-Object -First 10
```

## Pass/Fail criteria

| Check | Pass condition |
|-------|---------------|
| Build | 0 errors |
| Tests | 0 failures, >= 77 [Fact] |
| SCAN-01..06 | 0 new violations |
| T1-AC1 | Execute signature contains Instrument |
| T1-AC2 | _instrument passed at call site |
| T1-AC5 | T_B185_01 present and passes |
| T2-AC1 | TryGetLastDirection + RecordLastDirection present |
| T3-AC1 | _clickQty field present |
| T3-AC3 | LaneDEvictThreshold constant present |
| T4 | 14 T_B184_T1/T2 tests present |
| Guardian | Exit 0 OR exit 1 for DELETION-AUDIT only with each deletion having an authorising DW-card |
| ptt-sync | 0 errors |
| Spec | DW-B185 card present in spec HTML |

## When to compile in NinjaTrader

Tell the Director "safe to compile in NT8" ONLY when ALL of the following are true:
1. `dotnet build` exits 0 errors
2. `dotnet test` exits 0 failures
3. All 7 DNA scans: 0 new violations
4. `ptt-sync.ps1` exits 0 errors
5. Guardian exits 0 OR exit 1 is DELETION-AUDIT only with authorised DW-cards

If ANY check fails: stop, report the exact failure with file + line, and wait for Director
resolution before telling them to compile.

## After validation

If all checks pass:
1. Write docs/brain/B185/ticket-all-verification.md with full scan results
2. Tell Director: "B185 VERIFY_PASS -- safe to compile in NT8. Run ptt-sync.ps1 first,
   then F5 in NinjaTrader. Expected build tag in NT8 Output:
   PTT-COPIER B185 | be-all-instr-scope-dispatch-cyc-qty | {date}"
3. Add a B185 section card to the spec HTML marking B185 PIPELINE_COMPLETE

If checks fail:
1. List every failure clearly (file, line, violation type)
2. Do NOT tell Director to compile
3. Wait for Director instruction

---
*Handoff written: 2026-07-08*
*Baseline: c72f0cb7 (B184-T2 last clean commit)*
*5 files modified, uncommitted*
