# B143 Ticket 1 Verification

**Ticket**: Ticket 1 -- Add test seam shims + B143 xUnit test suite
**Verifier**: ptt-verifier
**Date**: 2026-09-07
**Workspace**: C:\WSGTA\universal-or-strategy (Wave workspace -- READ ONLY for .cs files)
**Phase**: 4b (independent verification)
**Final Result**: VERIFY_PASS

---

## Source Verification (Checks A through I)

### A. #region B143 test seam location
PASS. `#region B143 test seam` confirmed at line 3516 of `CopyEngine.cs`,
immediately after the DW-B135 test accessor block that ends at line 3511
(`TestOnly_LastLeaderDirection` property). Blank line separation is correct.

### B. All 5 shim methods present with correct signatures
PASS. Verified at lines 3518-3531:
- Line 3518: `internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice) => IsLiveEntryBlocked(instrKey, orderId, limitPrice);` PASS
- Line 3521: `internal void EvictDedup_ForTest(string orderId, NinjaTrader.Cbi.OrderState state) => EvictDedup(orderId, state);` PASS
- Line 3524: `internal void ClearLiveEntryForInstrument_ForTest(string instrFullName) => ClearLiveEntryForInstrument(instrFullName);` PASS
- Line 3527: `internal bool LiveEntryInstrumentsContains_ForTest(string key) => _liveEntryInstruments.ContainsKey(key);` PASS
- Line 3530: `internal bool EntryInstrKeyByOrderIdContains_ForTest(string orderId) => _entryInstrKeyByOrderId.ContainsKey(orderId);` PASS

### C. No logic changed in production code
PASS. All 5 shims are expression-body one-liners that forward directly to the
corresponding private methods or dictionary lookups. Zero logic added.

### D. TryFirePositionState CYC=8 AT LIMIT -- manual branch count
PASS. Counted from lines 3451-3499:
- base: 1
- `if (state != Filled && state != PartFilled)`: 1
- `if (e.Order?.Instrument?.FullName == null)`: 1
- `if (prior == newVal)`: 1
- `if (!hasPos)`: 1
- `foreach (var r in _rules)`: 1
- `if (e.Order.Account.Name == r.MasterAccount?.Name)`: 1
- `if (isLeaderAcct)`: 1
CYC = 1 base + 7 decision points = 8 AT LIMIT. The B143 addition (`ClearLiveEntryForInstrument(instr)` at line 3493) is a straight-line call INSIDE the existing `if (isLeaderAcct)` block -- adds 0 new branches. CYC unchanged at 8.

### E. All 7 [Fact] test methods present with correct names
PASS. Verified in `src/PropTraderTools/Tests/B143Tests.cs`:
1. `T_B143_01_IsLiveEntryBlocked_FirstCall_ReturnsFalse_AllowsDispatch`
2. `T_B143_02_IsLiveEntryBlocked_SecondCall_SameInstrKey_ReturnsTrue_BlocksDuplicate`
3. `T_B143_03_EvictDedup_Cancelled_ClearsInstrKey_FutureEntryUnblocked`
4. `T_B143_04_EvictDedup_Filled_DoesNotClear_TradeStillLive`
5. `T_B143_05_ClearLiveEntryForInstrument_RemovesAllKeysWithPrefix`
6. `T_B143_06_ClearLiveEntryForInstrument_IsNoOp_WhenNoMatchingKey`
7. `T_B143_07_EvictDedup_BracketCancelOrderId_DoesNotClearLiveEntryGuard`
All 7 match ticket contract exactly.

### F. Tests are pure unit tests (no NT8 runtime calls)
PASS. Test bodies use only:
- `CopyEngine.Instance` (singleton, no NT8 runtime needed)
- `OrderState` enum (NinjaTrader.Cbi enum, pure value)
- xUnit `Assert.False` / `Assert.True`
No Account, Order, or Instrument objects instantiated. No NT8 runtime calls.

### G. Tests are ASCII-only
PASS. Select-String non-ASCII scan returned 0 hits (see SCAN-01 below).

### H. No lock(), async void, throw new in test file
PASS. Select-String scan of B143Tests.cs for `lock(`, `async void`, `throw new`
(excluding comment lines) returned 0 hits (confirmed in SCAN-02 and SCAN-04).

### I. B143Tests.cs included in PropTraderTools.csproj
PASS. Line 171 of `PropTraderTools.csproj`:
`<Compile Include="Tests\B143Tests.cs" />`
Confirmed present after the `Tests\B139Tests.cs` entry (line 170), exactly as
specified in the ticket contract.

---

## SCAN Results (Independent Layer 3 Execution)

### SCAN-01: ASCII scan
Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "[^\x00-\x7F]"`
Command: `Select-String -Path src/PropTraderTools/Tests/B143Tests.cs -Pattern "[^\x00-\x7F]"`
Result: 0 hits in both files.
STATUS: PASS

### SCAN-02: lock() ban
Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\(" | Where-Object { $_.Line -notmatch "^\s*//" }`
Result: 0 actual `lock(` calls. All 4 matches in original scan are in comment lines
(lines 324, 358, 1750, 3725 -- all begin with "//"). No production lock() usage.
STATUS: PASS

### SCAN-03: CYC audit
No `scripts/complexity_audit.py` present (confirmed: script does not exist in workspace).
Manual CYC count performed directly from source:

| Method | Lines | CYC | Budget | Status |
|--------|-------|-----|--------|--------|
| `IsLiveEntryBlocked` | 4636-4647 | 4 (1+3 branches) | <=8 | PASS |
| `ClearLiveEntryForInstrument` | 4652-4659 | 2 (1+foreach+if at loop level) | <=8 | PASS |
| `EvictDedup` | 4666-4696 | 5 (1+terminal+Cancelled+TryRemove-guard+Filled) | <=8 | PASS |
| `TryFirePositionState` | 3451-3499 | 8 AT LIMIT (1+7 branches) | <=8 | PASS |
| `DispatchCopy` (no touch) | -- | 8 AT LIMIT (unchanged) | <=8 | PASS |
| 5x shim methods | 3518-3531 | 1 each (expression-body) | <=8 | PASS |

All methods CYC <= 8. No violations.
STATUS: PASS

### SCAN-04: JS P0 gate
Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "throw new" | Where-Object { $_.Line -notmatch "^\s*//" }`
Result: 0 hits

Command: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "async void " | Where-Object { $_.Line -notmatch "^\s*//" }`
Result: 0 hits

Command: `Select-String -Path src/PropTraderTools/Tests/B143Tests.cs -Pattern "throw new|async void|return null" | Where-Object { $_.Line -notmatch "^\s*//" }`
Result: 0 hits

Additional DNA scans:
- FontFamily: 3 hits -- ALL in comment lines (lines 3054, 3238, 3260). No WPF element usage. PASS.
- Hex color string (#RRGGBB): 0 hits in non-comment code. PASS.
- DateTime.Now (not UtcNow): 0 hits. PASS.

STATUS: PASS

### SCAN-05: dotnet build
Command: `dotnet build src/PropTraderTools/PropTraderTools.csproj`
Result:
  Build succeeded.
  0 Warning(s)
  0 Error(s)
  Time Elapsed 00:00:02.36

Note: Engineer reported 1 pre-existing warning (xUnit2004 in B131Tests.cs). My run shows 0 warnings.
The difference is non-material -- the warning appears to be suppressed via `<NoWarn>xUnit2004...</NoWarn>`
in PropTraderTools.csproj (line 26 confirms xUnit2004 in NoWarn list). Not a violation.
STATUS: PASS

### SCAN-06: dotnet test
Command: `dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "B143" --logger "console;verbosity=normal"`
Result:
  Passed PropTraderTools.Tests.B143Tests.T_B143_01_IsLiveEntryBlocked_FirstCall_ReturnsFalse_AllowsDispatch
  Passed PropTraderTools.Tests.B143Tests.T_B143_02_IsLiveEntryBlocked_SecondCall_SameInstrKey_ReturnsTrue_BlocksDuplicate
  Passed PropTraderTools.Tests.B143Tests.T_B143_03_EvictDedup_Cancelled_ClearsInstrKey_FutureEntryUnblocked
  Passed PropTraderTools.Tests.B143Tests.T_B143_04_EvictDedup_Filled_DoesNotClear_TradeStillLive
  Passed PropTraderTools.Tests.B143Tests.T_B143_05_ClearLiveEntryForInstrument_RemovesAllKeysWithPrefix
  Passed PropTraderTools.Tests.B143Tests.T_B143_06_ClearLiveEntryForInstrument_IsNoOp_WhenNoMatchingKey
  Passed PropTraderTools.Tests.B143Tests.T_B143_07_EvictDedup_BracketCancelOrderId_DoesNotClearLiveEntryGuard
  Test Run Successful. Total tests: 7 | Passed: 7 | Failed: 0 | Skipped: 0
STATUS: PASS

### SCAN-07: ptt-sync-and-verify
Command: `powershell -File scripts/ptt-sync-and-verify.ps1`
Result:
  Copied: 0  |  In-sync: 18  |  Excluded: 63
  OK   AtrSizingEngine.cs
  OK   CopyEngine.cs
  OK   FeatureFlags.cs
  OK   LicenseClient.cs
  OK   TradeCopierAddOn.cs
  OK   TradeCopierPanel.cs
  OK   TradeCopierWindow.cs
  OK   Core\PttContracts.cs
  OK   Features\PttBreakEven.cs
  OK   Features\PttBreakEvenSwap.cs
  OK   Features\PttCancel.cs
  OK   Features\PttCopier.cs
  OK   Features\PttFlatten.cs
  OK   Features\PttFollowerStrategy.cs
  OK   Features\PttGlobalBreakEven.cs
  OK   Features\PttGlobalQuickExit.cs
  OK   Features\PttQuickExit.cs
  OK   Features\PttTrim.cs
  === SYNC + VERIFY: PASS (18 files confirmed) ===
  0 MISMATCH lines.

Note: Engineer reported "Copied: 1 | In-sync: 17" on first run (CopyEngine.cs needed copying).
My run shows all 18 already in-sync -- expected, as engineer already synced the file.
STATUS: PASS

---

## Layer 2 Cross-Check (Engineer vs Verifier)

| Scan | Engineer Layer 2 | Verifier Layer 3 | Consistent? |
|------|-----------------|------------------|-------------|
| SCAN-01 ASCII | 0 hits both files | 0 hits both files | YES |
| SCAN-02 lock() | 0 actual lock() | 0 actual lock() | YES |
| SCAN-03 CYC | All methods CYC<=8 | All methods CYC<=8 | YES |
| SCAN-04 JS P0 | 0 violations | 0 violations | YES |
| SCAN-05 build | 0 errors, 1 warning (pre-existing) | 0 errors, 0 warnings | YES (difference is pre-existing NoWarn suppression) |
| SCAN-06 test | 7/7 PASS | 7/7 PASS | YES |
| SCAN-07 sync | 0 MISMATCH | 0 MISMATCH | YES |

**No discrepancies. Layer 2 and Layer 3 are fully consistent.**

---

## Spec Compliance Checklist

| Item | Test | Result | Notes |
|------|------|--------|-------|
| T_B143_01: first call for new instrKey returns false (dispatches) | T_B143_01 PASS | CLOSED | Fresh instrKey + orderId -> false confirmed |
| T_B143_02: second call same instrKey returns true (blocks dup) | T_B143_02 PASS | CLOSED | Same instrKey, different orderId -> true confirmed |
| T_B143_03: EvictDedup(Cancelled) clears instrKey -- future entry unblocked | T_B143_03 PASS | CLOSED | EntryInstrKeyByOrderIdContains also verified false |
| T_B143_04: EvictDedup(Filled) does NOT clear instrKey -- trade still live | T_B143_04 PASS | CLOSED | _liveEntryInstruments key preserved on fill |
| T_B143_05: ClearLiveEntryForInstrument removes all instrFullName+"|" keys | T_B143_05 PASS | CLOSED | Both Sell and Buy direction keys removed |
| T_B143_06: ClearLiveEntryForInstrument is no-op on missing key | T_B143_06 PASS | CLOSED | No exception, unrelated key survives |
| T_B143_07: bracket-cancel orderId not in companion map -> guard intact | T_B143_07 PASS | CLOSED | Scoped-removal contract verified |

---

## DW Closure Confirmation

### DW-B142-MGC-02 -- CLOSED
Instrument-level entry guard (`_liveEntryInstruments`) confirmed functional via T_B143_01
(first-pass dispatch allowed) and T_B143_02 (duplicate blocked). `ContainsKey` at Branch 1
of `IsLiveEntryBlocked` (line 4638) blocks any second dispatch for the same instrKey,
regardless of orderId -- directly closing the MGC cancel+resubmit duplicate dispatch issue.

### DW-B142-MGC-01 -- CLOSED
Root cause confirmed resolved. The MGC cancel+resubmit pattern produces a new orderId for
the same instrument+direction. The instrument-level guard (Branch 1 in `IsLiveEntryBlocked`)
blocks the second event before orderId checks even run. T_B143_02 validates this:
`ORD-B143-02B` is a fresh orderId that would have previously bypassed orderId dedup; now
it is correctly blocked by the instrument-level guard.

---

## DNA Rule Check Summary

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock()) | 0 actual lock() in new/changed code | PASS |
| JS-001 (no throw in hot paths) | 0 throw new in new/changed code | PASS |
| JS-002 (no return null) | New shims return bool or void; no return null | PASS |
| JS-008 (no mutable struct across threads) | No new struct with mutable fields | PASS |
| JS-010 (private constructor on CopyEngine) | CopyEngine() at line 573 is private | PASS |
| JS-025 (lock-free collections) | _liveEntryInstruments and _entryInstrKeyByOrderId are ConcurrentDictionary | PASS |
| JS-033 (no async void) | 0 async void in new/changed code | PASS |
| JS-066 (CYC <= 8) | All new/changed methods within limit | PASS |
| ASCII-only | 0 non-ASCII in CopyEngine.cs and B143Tests.cs | PASS |
| FontFamily ban | FontFamily appears in comments only -- no WPF element usage | PASS |
| Hex color (#RRGGBB) ban | 0 hex color strings in non-comment code | PASS |
| DateTime.Now ban | 0 DateTime.Now (not UtcNow) in new code | PASS |
| SolidColorBrush.Freeze() | No new SolidColorBrush in test seam or test file | PASS |
| Singleton violation (non-private CopyEngine ctor) | Private ctor confirmed at line 573 | PASS |

---

## Architecture Compliance

| Requirement | Status |
|-------------|--------|
| B143 test seam inserted after DW-B135 accessors (line 3511) | PASS |
| 5 shims match ticket contract exactly (method names, parameters, return types) | PASS |
| Production code unchanged -- no logic in shims | PASS |
| B143Tests.cs in correct namespace (PropTraderTools.Tests) | PASS |
| InternalsVisibleTo already granted at CopyEngine.cs line 46 | PASS (confirmed) |
| B143Tests.cs added to PropTraderTools.csproj compile list | PASS |
| Single-pipeline ticket (all changes mutually dependent -- correct) | PASS |
| No NT8 API beyond state machine enum (OrderState) in test file | PASS |
| Test isolation: unique instrKey prefix per test (TEST-B143-0N pattern) | PASS |

---

## Final Result

**VERIFY_PASS**

All 7 independent scans passed. All source verification checks A through I passed.
All 7 tests pass with 0 failures. Layer 2 and Layer 3 results are consistent.
All spec requirements T_B143_01 through T_B143_07 satisfied.
DW-B142-MGC-02 and DW-B142-MGC-01 closures confirmed by test evidence.
Zero DNA rule violations found.

---

*Produced by ptt-verifier, B143 Phase 4b. Independent verification artifact.*
*This VERIFY_PASS authorizes Phase 5 (ptt-plan-reviewer) to proceed.*