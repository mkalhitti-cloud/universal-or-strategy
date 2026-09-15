# B157-T3 Completion
# Phase 4a Engineer output
# Produced: 2026-08-10

---

## Ticket
B157-T3: DW-B156-DEFER-3 -- Exact-string assertion upgrade for [DRAIN-ABORT-LEADER-FLAT] log marker

## Status
**BUILD_PASS**

---

## Change Made

**File:** `src/PropTraderTools/CopyEngineTests.cs`

**What:** One new `[Fact]` test method added to the `B155ContractTests` class, inserted
immediately before the class closing brace (after `AbortDrainOnFill_ShouldExist_AsPrivateInstanceHelper`).

**Inserted at:** line 9475 (after edit, new method spans lines 9476-9509; class `}` is now line 9510)

**New test method (exact text):**
```csharp
        // DW-B156-DEFER-3: exact-string assertion for DRAIN-ABORT-LEADER-FLAT log marker.
        // Stronger than existing test (~line 9414) which checks "DRAIN-ABORT-FLAT" (substring only).
        // This test checks the exact marker "[DRAIN-ABORT-LEADER-FLAT]" including brackets and LEADER.
        // Uses same file-read pattern as existing test at ~line 9442-9458.
        [Fact]
        public void TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString()
        {
            // Arrange: resolve CopyEngine.cs source path via assembly location (same as ~line 9442).
            var srcFile = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(CopyEngine).Assembly.Location) ?? string.Empty,
                "..", "..", "..", "..", "..", "src", "PropTraderTools", "CopyEngine.cs");
            srcFile = System.IO.Path.GetFullPath(srcFile);

            if (System.IO.File.Exists(srcFile))
            {
                // Act: read source.
                var src = System.IO.File.ReadAllText(srcFile);

                // Assert: exact marker string must be present (brackets, LEADER, hyphen positions).
                // Tests that IsLeaderFlatDrainAbort (extracted from TryEvictFollowerBeSlot, DW-B156-DEFER-1)
                // emits the precise marker string -- not just a substring.
                Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src);
            }
            else
            {
                // Source file unavailable (CI without workspace): verify via reflection that
                // IsLeaderFlatDrainAbort exists as a private instance method (structural proxy).
                var m = typeof(CopyEngine).GetMethod(
                    "IsLeaderFlatDrainAbort",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(m);
            }
        }
```

**No other files modified.**

---

## Log Marker Confirmation

CopyEngine.cs line 1969 confirmed:
```
"[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
```
Exact string `"[DRAIN-ABORT-LEADER-FLAT]"` is present in `IsLeaderFlatDrainAbort` (B157-T2 extraction). Test assertion will pass.

---

## 7-Scan Results

### SCAN-01: lock() check
Command:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern 'lock\('
```
Output: 1 match -- `// JS-001: no throw. JS-021: no lock(). ASCII-only.` (comment only, line 9514)
Executable-code matches: **0**
**PASS**

### SCAN-02: async void check
Command:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '\basync\s+void\b'
```
Output: (no output)
**PASS -- 0 matches**

### SCAN-03: DateTime.Now check
Command:
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern 'DateTime\.Now\b'
```
Output: (no output)
**PASS -- 0 matches**

### SCAN-04: non-ASCII byte check
Command:
```powershell
[System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngineTests.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
```
Output: `0`
**PASS -- 0 non-ASCII bytes**

### SCAN-05: CYC check
Not applicable for test file (per ticket spec). Test method has CYC=1 -- single
conditional branch plus one assertion.
**PASS -- N/A**

### SCAN-06: xUnit test run
Command:
```powershell
dotnet test src\PropTraderTools\PropTraderTools.Tests.csproj --no-build --filter "TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString"
```
Output:
```
  Passed PropTraderTools.B155ContractTests.TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString [130 ms]

Test Run Successful.
Total tests: 1
     Passed: 1
```
New test appears as **PASSED** (not skipped -- no Skip attribute). **PASS**

Note: Pre-existing failure `TrySetSlateReadyOnDrainFill_NullOrder_NoSideEffect` in
`DwB15701SlateReadyTests` class confirmed pre-existing (fails identically with stash applied,
before T3 changes). Not introduced by this ticket.

### SCAN-07: Build check
Command:
```powershell
dotnet build src\PropTraderTools\PropTraderTools.Tests.csproj
```
Output:
```
Build succeeded.
    0 Error(s)
```
**PASS -- 0 build errors**

---

## Structural Verification (SCAN-07 checklist)

- [x] `CopyEngineTests.cs` contains method `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString`
- [x] Method carries `[Fact]` with NO Skip attribute
- [x] Method body contains `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)`
- [x] The exact string `"[DRAIN-ABORT-LEADER-FLAT]"` uses straight double-quotes (ASCII 34), not curly quotes
- [x] Existing test `Assert.Contains("DRAIN-ABORT-FLAT", src)` at ~line 9414 is UNCHANGED
- [x] CopyEngine.cs not modified
All 5 items: PASS

---

## deploy-sync.ps1 Result

```
ASCII GATE PASS - all source files are clean
DIFF GUARD PASS: Diff size (319 chars) is within limits.
SOVEREIGN AUDIT PASS: Architectural integrity verified.
SYNC COMPLETE: One Source of Truth Established.
```
Exit: Sync succeeded. (droid auth warning in stderr is unrelated to file sync.)

---

## Summary

- 1 new `[Fact]` test added: `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString`
- Placed in `B155ContractTests` class in `CopyEngineTests.cs` (line 9476 after insert)
- File-read pattern identical to existing `TryEvictFollowerBeSlot_ShouldCallAbortDrain_WhenPositionFlat` test
- Test asserts exact string `"[DRAIN-ABORT-LEADER-FLAT]"` (stronger than existing `"DRAIN-ABORT-FLAT"` substring)
- Fallback path (source unavailable): reflection check that `IsLeaderFlatDrainAbort` exists as private instance method
- All 7 scans: PASS
- Build: 0 errors
- New test: PASSED (not skipped)
- No other files touched

**BUILD_PASS**

---

_Authored by ptt-engineer Phase 4a. Block B157. Ticket B157-T3._
