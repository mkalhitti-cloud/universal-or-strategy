# B187 Tickets
## ptt-architect Phase 3

**Date**: 2026-07-08
**Author**: ptt-architect
**Epic**: B187 -- QX Stop Snapshot Fix + DIAG Cleanup + CS0067 Suppress
**Plan**: `docs/brain/B187/02-architecture-plan.md` (REVIEW_PASS 2026-07-08)
**Execution order**: T1 -> T2 -> T3 (sequential, single-pipeline)

---

## TICKET-1: DW-B187-QX-STOP-SNAP-01 -- leaderStop snapshot order fix [P1]

**DW references**: DW-B187-QX-STOP-SNAP-01 (new), DW-B63-01, DW-B126
**Priority**: P1
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Test file**: `src/PropTraderTools/CopyEngineTests.cs` (append 4 new [Fact] methods)

### Root Cause Summary

`PttGlobalQuickExit.Execute(Instrument)` (lines 56-62) and `Execute(List<(double,int)>, Instrument)` (lines 140-142) both call `CancelPttBeOrders` + `WaitForPttBeCancelled` BEFORE `PttQuickExit.SnapshotStopPrice`. When BE ALL fires first, the only stop reference is PTT-BE-Stop-*. After the cancel+wait sequence those orders are Cancelled and invisible to SnapshotStopPrice. Result: `leaderStop = 0`, guard `if (snapshotStop <= 0) return;` prevents stop submission.

**Fix**: Move `double leaderStop = PttQuickExit.SnapshotStopPrice(...)` to BEFORE `CancelPttBeOrders` in both overloads. `SnapshotTargetOrders` stays AFTER cancel+wait (DW-B126 maintained).

---

### Change 1: `Execute(Instrument)` -- lines 55-62

**Method signature** (unchanged):
```csharp
internal void Execute(Instrument instrument)
```

**BEFORE** (lines 55-62 of `PttGlobalQuickExit.cs`):
```csharp
                    // B118 DW-B126: cancel PTT-BE-* BEFORE snapshot to eliminate BE/QX race.
                    int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
                    WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
                    // PTT-BE-* are now terminal -- snapshot sees clean order book.
                    var targets = SnapshotTargetOrders(acc, pos.Instrument);
                    // B78 DW-B63-01: snapshot leader stop BEFORE ExecuteOne cancels leader brackets.
                    double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
                    var ticks = ResolveQuickTicks(pos.Instrument);
```

**AFTER** (replace lines 55-62):
```csharp
                    // B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
                    // PTT-BE-Stop-* are the stop reference when BE ALL has fired first; cancel destroys them.
                    // SnapshotTargetOrders still runs after cancel (DW-B126 race prevention maintained).
                    double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
                    // B118 DW-B126: cancel PTT-BE-* BEFORE SnapshotTargetOrders to eliminate BE/QX target race.
                    int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
                    WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
                    // PTT-BE-* are now terminal -- target snapshot sees clean order book.
                    var targets = SnapshotTargetOrders(acc, pos.Instrument);
                    var ticks = ResolveQuickTicks(pos.Instrument);
```

**XML summary update** -- also update the `<summary>` doc comment at line 31:

BEFORE (line 31):
```csharp
        /// B78 DW-B63-01: capture leaderStop BEFORE calling ExecuteOne (which cancels leader brackets).
```

AFTER:
```csharp
        /// B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
```

**Line numbers in source**: `Execute(Instrument)` body starts at line 40; the 8-statement block to replace spans lines 55-62. Line 31 is the XML summary line to update.

---

### Change 2: `Execute(List<(double Price, int Qty)>, Instrument)` -- lines 140-143

**Method signature** (unchanged):
```csharp
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
    Instrument instrument
)
```

**BEFORE** (lines 140-143 of `PttGlobalQuickExit.cs`):
```csharp
                    int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
                    WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
                    double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
                    var ticks = ResolveQuickTicks(pos.Instrument);
```

**AFTER** (replace lines 140-143):
```csharp
                    // B78 DW-B63-01 + DW-B187: capture leaderStop BEFORE CancelPttBeOrders.
                    double leaderStop = PttQuickExit.SnapshotStopPrice(acc, pos.Instrument);
                    int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
                    WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
                    var ticks = ResolveQuickTicks(pos.Instrument);
```

**Note**: `Execute(List, Instrument)` has no `SnapshotTargetOrders` call. It uses `forcedTargets` (caller-supplied). DW-B126 is N/A for this overload -- no regression possible.

---

### CYC Impact -- T1

| Method | Before | After | Delta | Reason |
|--------|--------|-------|-------|--------|
| `Execute(Instrument)` | 8 | 8 | 0 | Pure statement reorder, no new branches |
| `Execute(List, Instrument)` | 8 | 8 | 0 | Pure statement reorder, no new branches |

---

### Tests -- T1 (4 new [Fact] methods in `CopyEngineTests.cs`)

**Append to the existing test class before the closing `}` of the `CopyEngineTests` class (before line 8942).**

The tests are structural (source-text ordering assertions). No NT8 runtime required. Each test reads `PttGlobalQuickExit.cs` as a string array, locates the method body by scanning for the method signature, and asserts line-number ordering.

---

#### T_B187_01

```csharp
        // T_B187_01 (DW-B187-QX-STOP-SNAP-01): In Execute(Instrument), SnapshotStopPrice line
        // appears BEFORE CancelPttBeOrders line.
        // Confirms the B187 fix: leaderStop captured while PTT-BE-Stop-* are still Working.
        [Fact]
        public void T_B187_01_Execute_Instrument_SnapshotStopPrice_BeforeCancelPttBeOrders()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? string.Empty,
                "..", "..", "..", "src", "PropTraderTools", "Features", "PttGlobalQuickExit.cs"
            );
            string[] lines = System.IO.File.ReadAllLines(path);

            // Locate Execute(Instrument) -- first occurrence of "internal void Execute(Instrument"
            int methodStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("internal void Execute(Instrument instrument)"))
                {
                    methodStart = i;
                    break;
                }
            }
            Assert.True(methodStart >= 0, "Could not find Execute(Instrument instrument) in PttGlobalQuickExit.cs");

            // Find the closing brace of Execute(Instrument) body
            int methodEnd = methodStart;
            int braceDepth = 0;
            bool enteredBody = false;
            for (int i = methodStart; i < lines.Length; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') { braceDepth++; enteredBody = true; }
                    else if (c == '}') { braceDepth--; }
                }
                if (enteredBody && braceDepth == 0) { methodEnd = i; break; }
            }

            int snapshotLine = -1;
            int cancelLine = -1;
            for (int i = methodStart; i <= methodEnd; i++)
            {
                if (snapshotLine < 0 && lines[i].Contains("SnapshotStopPrice"))
                    snapshotLine = i;
                if (cancelLine < 0 && lines[i].Contains("CancelPttBeOrders"))
                    cancelLine = i;
            }

            Assert.True(snapshotLine >= 0, "SnapshotStopPrice not found in Execute(Instrument)");
            Assert.True(cancelLine >= 0, "CancelPttBeOrders not found in Execute(Instrument)");
            Assert.True(
                snapshotLine < cancelLine,
                $"B187 fix: SnapshotStopPrice (line {snapshotLine + 1}) must come BEFORE CancelPttBeOrders (line {cancelLine + 1}) in Execute(Instrument)"
            );
        }
```

---

#### T_B187_02

```csharp
        // T_B187_02 (DW-B187-QX-STOP-SNAP-01): In Execute(List, Instrument), SnapshotStopPrice line
        // appears BEFORE CancelPttBeOrders line.
        // Confirms the B187 fix on the forced-2-target overload.
        [Fact]
        public void T_B187_02_Execute_List_Instrument_SnapshotStopPrice_BeforeCancelPttBeOrders()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? string.Empty,
                "..", "..", "..", "src", "PropTraderTools", "Features", "PttGlobalQuickExit.cs"
            );
            string[] lines = System.IO.File.ReadAllLines(path);

            // Locate Execute(List, Instrument) -- signature spans multiple lines; find "forcedTargets"
            // in the parameter list. Search for "internal void Execute(" and pick the second occurrence.
            int methodStart = -1;
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("internal void Execute("))
                {
                    count++;
                    if (count == 2) { methodStart = i; break; }
                }
            }
            Assert.True(methodStart >= 0, "Could not find second Execute( (List overload) in PttGlobalQuickExit.cs");

            int methodEnd = methodStart;
            int braceDepth = 0;
            bool enteredBody = false;
            for (int i = methodStart; i < lines.Length; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') { braceDepth++; enteredBody = true; }
                    else if (c == '}') { braceDepth--; }
                }
                if (enteredBody && braceDepth == 0) { methodEnd = i; break; }
            }

            int snapshotLine = -1;
            int cancelLine = -1;
            for (int i = methodStart; i <= methodEnd; i++)
            {
                if (snapshotLine < 0 && lines[i].Contains("SnapshotStopPrice"))
                    snapshotLine = i;
                if (cancelLine < 0 && lines[i].Contains("CancelPttBeOrders"))
                    cancelLine = i;
            }

            Assert.True(snapshotLine >= 0, "SnapshotStopPrice not found in Execute(List, Instrument)");
            Assert.True(cancelLine >= 0, "CancelPttBeOrders not found in Execute(List, Instrument)");
            Assert.True(
                snapshotLine < cancelLine,
                $"B187 fix: SnapshotStopPrice (line {snapshotLine + 1}) must come BEFORE CancelPttBeOrders (line {cancelLine + 1}) in Execute(List, Instrument)"
            );
        }
```

---

#### T_B187_03

```csharp
        // T_B187_03 (DW-B126 guard): In Execute(Instrument), SnapshotTargetOrders line
        // appears AFTER CancelPttBeOrders line.
        // Confirms DW-B126 constraint is maintained: target snapshot still runs after cancel+wait.
        [Fact]
        public void T_B187_03_Execute_Instrument_SnapshotTargetOrders_AfterCancelPttBeOrders()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? string.Empty,
                "..", "..", "..", "src", "PropTraderTools", "Features", "PttGlobalQuickExit.cs"
            );
            string[] lines = System.IO.File.ReadAllLines(path);

            // Locate Execute(Instrument) -- first occurrence
            int methodStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("internal void Execute(Instrument instrument)"))
                {
                    methodStart = i;
                    break;
                }
            }
            Assert.True(methodStart >= 0, "Could not find Execute(Instrument instrument) in PttGlobalQuickExit.cs");

            int methodEnd = methodStart;
            int braceDepth = 0;
            bool enteredBody = false;
            for (int i = methodStart; i < lines.Length; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') { braceDepth++; enteredBody = true; }
                    else if (c == '}') { braceDepth--; }
                }
                if (enteredBody && braceDepth == 0) { methodEnd = i; break; }
            }

            int cancelLine = -1;
            int targetSnapshotLine = -1;
            for (int i = methodStart; i <= methodEnd; i++)
            {
                if (cancelLine < 0 && lines[i].Contains("CancelPttBeOrders"))
                    cancelLine = i;
                if (targetSnapshotLine < 0 && lines[i].Contains("SnapshotTargetOrders"))
                    targetSnapshotLine = i;
            }

            Assert.True(cancelLine >= 0, "CancelPttBeOrders not found in Execute(Instrument)");
            Assert.True(targetSnapshotLine >= 0, "SnapshotTargetOrders not found in Execute(Instrument)");
            Assert.True(
                targetSnapshotLine > cancelLine,
                $"DW-B126: SnapshotTargetOrders (line {targetSnapshotLine + 1}) must come AFTER CancelPttBeOrders (line {cancelLine + 1}) in Execute(Instrument)"
            );
        }
```

---

#### T_B187_04

```csharp
        // T_B187_04 (DW-B126 guard -- List overload): Execute(List, Instrument) does NOT contain
        // SnapshotTargetOrders. It uses forcedTargets (caller-supplied); DW-B126 is N/A.
        // Confirms no regression -- SnapshotTargetOrders must never be added to this overload.
        [Fact]
        public void T_B187_04_Execute_List_Instrument_DoesNotContainSnapshotTargetOrders()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
                    ?? string.Empty,
                "..", "..", "..", "src", "PropTraderTools", "Features", "PttGlobalQuickExit.cs"
            );
            string[] lines = System.IO.File.ReadAllLines(path);

            // Locate second Execute( overload (List overload)
            int methodStart = -1;
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("internal void Execute("))
                {
                    count++;
                    if (count == 2) { methodStart = i; break; }
                }
            }
            Assert.True(methodStart >= 0, "Could not find second Execute( (List overload) in PttGlobalQuickExit.cs");

            int methodEnd = methodStart;
            int braceDepth = 0;
            bool enteredBody = false;
            for (int i = methodStart; i < lines.Length; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') { braceDepth++; enteredBody = true; }
                    else if (c == '}') { braceDepth--; }
                }
                if (enteredBody && braceDepth == 0) { methodEnd = i; break; }
            }

            for (int i = methodStart; i <= methodEnd; i++)
            {
                Assert.False(
                    lines[i].Contains("SnapshotTargetOrders"),
                    $"Execute(List, Instrument) must NOT contain SnapshotTargetOrders (DW-B126 N/A -- uses forcedTargets). Found at line {i + 1}: {lines[i].Trim()}"
                );
            }
        }
```

---

### 7-Scan Checklist -- T1

```
SCAN-01: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("         -> 0 results
SCAN-02: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"    -> 0 results
SCAN-03: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "    -> 0 results
SCAN-04: CYC manual count per modified method (see ticket)
SCAN-05: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"  -> 0 results
SCAN-06: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"     -> 0 results
SCAN-07: dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10 -> 0 errors, 0 warnings
```

**SCAN-04 detail**:
- `Execute(Instrument)`: CYC=8 before and after. No new branches. Same 8 decision points: combined gate(1), acc loop(2), follower guard(3), pos loop(4), null/flat continue(5), instrument filter(6), NeedsLeaderFallbackFlatten guard(7), ExecuteFollowers delegate(8).
- `Execute(List, Instrument)`: CYC=8 before and after. Same 8 decision points: combined gate(1), null/empty guard(2), acc loop(3), follower skip(4), pos loop(5), null/flat/instr continue(6), flatten guard(7), ExecuteFollowers delegate(8).

---

### Acceptance Criteria -- T1

1. SIM sequence: enable BE ALL -> close to within 2 ticks -> verify PTT-BE-Stop-* Working -> press QX-ALL -> PTT-QX-Stop submitted at non-zero price.
2. `Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "SnapshotStopPrice"` returns line with smaller line number than the `CancelPttBeOrders` line in `Execute(Instrument)` body.
3. Same ordering check passes for `Execute(List, Instrument)` body.
4. `SnapshotTargetOrders` line number > `CancelPttBeOrders` line number in `Execute(Instrument)` body.
5. `Execute(List, Instrument)` body contains no `SnapshotTargetOrders` call.
6. All 4 [Fact] tests T_B187_01..04 pass (`dotnet test`).
7. SCAN-07 passes (0 build errors, 0 build warnings).

---

## TICKET-2: DW-B186-DIAG-CLEANUP-01 -- Remove [DW-B115-DIAG] log blocks [P3]

**DW references**: DW-B186-DIAG-CLEANUP-01
**Priority**: P3
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Depends on**: T1 must be committed first (same file; T2 line numbers are relative to post-T1 source).

**Note**: Line numbers below are from the **current source** (pre-T1 applied). After T1 is committed the absolute line numbers will shift slightly (T1 adds ~3 lines in `Execute(Instrument)` and ~1 line in `Execute(List, Instrument)`). The engineer MUST re-read the file after T1 commit to get the updated line numbers. The string patterns below are deterministic and unique -- use `Select-String` to locate each block in the post-T1 source.

---

### Removal 1: `Execute(Instrument)` -- `LogLeaderDiag` call + 2-line comment

**Locate by pattern** (current lines 76-78):
```csharp
                    // DW-B115-DIAG: log leader per-target qty split for comparison against followers.
                    // Remove when DW-B115 root cause confirmed and fix applied.
                    LogLeaderDiag(acc, targets, pos.Quantity);
```

**AFTER**: Delete these 3 lines entirely. No replacement needed.

---

### Removal 2: `ExecuteFollowers(...)` -- full DIAG block

**Locate by pattern** (current lines 208-245; re-verify after T1):

The block begins immediately after `var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);` (line 207) and ends before `// DW-B124: when follower snapshot is empty` (line 246).

**BEFORE** (remove this entire block, current lines 208-245):
```csharp
                    // DW-B115-DIAG: log follower position qty + per-target qty split.
                    // Determines: (A) whether follower ATM has same qty split as leader,
                    // or (B) whether followerTargets is empty/partial (DW-B120 async lag).
                    // Remove when DW-B115 root cause confirmed and fix applied.
                    int _fPosQty = 0;
                    foreach (NinjaTrader.Cbi.Position _p in follower.Positions) // (4)
                    {
                        if (
                            _p != null
                            && _p.Instrument != null
                            && _p.Instrument.FullName == pos.Instrument.FullName
                        ) // (5)
                        {
                            _fPosQty = _p.Quantity;
                            break;
                        }
                    }
                    {
                        var _sb = new System.Text.StringBuilder(
                            "[DW-B115-DIAG] follower targets: "
                        );
                        _sb.Append(follower.Name);
                        _sb.Append(" count=");
                        _sb.Append(followerTargets.Count);
                        _sb.Append(" posQty=");
                        _sb.Append(_fPosQty);
                        for (int _i = 0; _i < followerTargets.Count; _i++) // (6)
                        {
                            _sb.Append(" T");
                            _sb.Append(_i + 1);
                            _sb.Append("=");
                            _sb.Append(followerTargets[_i].Qty);
                        }
                        NinjaTrader.Code.Output.Process(
                            _sb.ToString(),
                            NinjaTrader.NinjaScript.PrintTo.OutputTab1
                        );
                    }
```

**AFTER**: Delete the entire block. No replacement. `_fPosQty` is used only within this DIAG block -- safe to remove.

---

### Removal 3: `LogLeaderDiag` private static method -- lines 490-519

**Locate by pattern** (current lines 490-519):

The method starts with the XML `<summary>` at line 490 and closes at line 519.

**BEFORE** (remove these 30 lines):
```csharp
        /// <summary>
        /// Build and append DW-B115-DIAG log string for leader targets.
        /// Extracted from PttGlobalQuickExit.Execute DIAG block (lines 83-100).
        /// CYC=2: (1) for-loop, (2) always-execute append (no branch inside loop body).
        /// JS-002: void. JS-021: no lock. ASCII-only.
        /// </summary>
        private static void LogLeaderDiag(
            NinjaTrader.Cbi.Account acc,
            System.Collections.Generic.List<(double Price, int Qty)> targets,
            int posQty
        )
        {
            var _sb = new System.Text.StringBuilder("[DW-B115-DIAG] leader targets: ");
            _sb.Append(acc.Name);
            _sb.Append(" count=");
            _sb.Append(targets.Count);
            _sb.Append(" posQty=");
            _sb.Append(posQty);
            for (int _i = 0; _i < targets.Count; _i++)
            {
                _sb.Append(" T");
                _sb.Append(_i + 1);
                _sb.Append("=");
                _sb.Append(targets[_i].Qty);
            }
            NinjaTrader.Code.Output.Process(
                _sb.ToString(),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
        }
```

**AFTER**: Delete the entire method (XML summary + signature + body). No replacement.

---

### XML Summary Update: `ExecuteFollowers` -- CYC comment

**Locate by pattern** (current lines 184-186):

**BEFORE**:
```csharp
        /// <summary>
        /// ExecuteFollowers: dispatch Quick Exit to all follower accounts for the given leader position.
        /// Extracted from Execute() by B120 to maintain CYC <= 8 in Execute() after DW-B129 guard.
        /// CYC=7: rule null-check(1), follower foreach(2), follower null continue(3),
        ///        follower position foreach DIAG(4), DIAG _p null/instr guard(5), DIAG for-loop(6), delegate(7).
```

**AFTER**:
```csharp
        /// <summary>
        /// ExecuteFollowers: dispatch Quick Exit to all follower accounts for the given leader position.
        /// Extracted from Execute() by B120 to maintain CYC <= 8 in Execute() after DW-B129 guard.
        /// CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).
```

(Remove the `DIAG(4)`, `DIAG _p null/instr guard(5)`, `DIAG for-loop(6)` branches from the CYC list.)

---

### CYC Impact -- T2

| Method | Before T2 | After T2 | Delta | Reason |
|--------|-----------|----------|-------|--------|
| `Execute(Instrument)` | 8 (after T1) | 8 | 0 | `LogLeaderDiag` call is a statement, not a branch |
| `ExecuteFollowers` | 7 | 4 | -3 | Removes branches (4) foreach, (5) null/instr guard, (6) for-loop |
| `LogLeaderDiag` | 2 | DELETED | -- | Method removed entirely; no callers remain |

---

### Tests -- T2

**0 new tests.** Pure deletion. No new test coverage required.

---

### 7-Scan Checklist -- T2

```
SCAN-01: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("         -> 0 results
SCAN-02: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"    -> 0 results
SCAN-03: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "    -> 0 results
SCAN-04: CYC manual count per modified method (see ticket)
SCAN-05: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"  -> 0 results
SCAN-06: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"     -> 0 results
SCAN-07: dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10 -> 0 errors, 0 warnings
```

**SCAN-04 detail**:
- `Execute(Instrument)`: CYC=8 unchanged. `LogLeaderDiag(...)` was a statement call, not a branch. Removing it does not change CYC.
- `ExecuteFollowers`: CYC 7->4. Branches (4)(5)(6) removed with the DIAG block. Remaining: rule null-check(1), follower foreach(2), follower null continue(3), ExecuteOne delegate(4).
- `LogLeaderDiag`: deleted. No longer counted.

---

### Acceptance Criteria -- T2

1. `Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "LogLeaderDiag"` returns 0 results.
2. `Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "DW-B115-DIAG"` returns 0 results.
3. `Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "_fPosQty"` returns 0 results.
4. `ExecuteFollowers` XML summary comment reads `CYC=4: rule null-check(1), follower foreach(2), follower null continue(3), delegate(4).`
5. SCAN-07 passes (0 build errors, 0 build warnings).

---

## TICKET-3: DW-B186-WARN-01 -- Suppress CS0067 PendingBeArmed warning [P3]

**DW references**: DW-B186-WARN-01 (supersedes DW-B184-04)
**Priority**: P3
**File**: `src/PropTraderTools/CopyEngine.cs`
**Independent**: T3 is independent of T1 and T2 (different file). Executed last per SINGLE-PIPELINE mandate.

---

### Change: Wrap `PendingBeArmed` event declaration -- line 423

**Method/location**: Field-level declaration; not inside a method. CopyEngine.cs line 423.

**BEFORE** (line 423):
```csharp
        internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
```

**AFTER** (replace line 423 with 3 lines):
```csharp
#pragma warning disable CS0067  // DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel
        internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
#pragma warning restore CS0067
```

**Context** (surrounding lines 420-425 unchanged):
```csharp
        // B10 T2 -- Pending BE fired notification (fires on NT8 account bg thread; Panel marshals to UI)
        internal event Action<string, string> PendingBeFired;
        // B10 T2 -- Pending BE armed notification (fires on NT8 account bg thread; Panel marshals to UI)
#pragma warning disable CS0067  // DW-B186-WARN-01: Roslyn false positive -- event IS subscribed by TradeCopierPanel
        internal event Action<string, string> PendingBeArmed; // HOTFIX-BEALL-SYNC-01: instr, accName
#pragma warning restore CS0067
```

**Rationale**: `PendingBeArmed` IS subscribed by `TradeCopierPanel.cs` (lines 771-772) and raised via the `DisarmPendingBe`/`RaiseBeAllDisarmed` path. Roslyn CS0067 is a false positive -- the compiler does not track event subscriptions that occur in a separate partial class file.

---

### CYC Impact -- T3

No methods modified. `#pragma` directives are compile-time metadata only. CYC unchanged throughout `CopyEngine.cs`.

---

### Tests -- T3

**0 new tests.** Acceptance criterion is `dotnet build` 0 warnings (SCAN-07). No runtime behavior change.

---

### 7-Scan Checklist -- T3

```
SCAN-01: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("         -> 0 results
SCAN-02: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"    -> 0 results
SCAN-03: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "    -> 0 results
SCAN-04: CYC manual count per modified method (see ticket)
SCAN-05: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"  -> 0 results
SCAN-06: git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"     -> 0 results
SCAN-07: dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 10 -> 0 errors, 0 warnings
```

**SCAN-04 detail**: No methods modified. `#pragma` does not introduce branches. CYC = 0 delta.

**SCAN-07 note**: The specific warning being eliminated is:
```
warning CS0067: The event 'CopyEngine.PendingBeArmed' is never used
```
After T3, `dotnet build` must produce 0 results for `CS0067`.

Verification command:
```powershell
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-String "CS0067"
# Must return 0 results
```

---

### Acceptance Criteria -- T3

1. `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-String "CS0067"` returns 0 results.
2. `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "PendingBeArmed" -Context 1,1` shows the event declaration wrapped by `#pragma warning disable CS0067` above and `#pragma warning restore CS0067` below.
3. SCAN-07 passes (0 build errors, 0 build warnings).

---

## DNA Constraints (all tickets)

| Rule | Constraint | T1 | T2 | T3 |
|------|-----------|----|----|-----|
| JS-021 | No `lock()` anywhere | PASS -- no lock introduced | PASS -- only deletions | PASS -- pragma only |
| JS-001 | No `throw new` in hot paths | PASS -- no new exceptions | PASS -- only deletions | PASS -- pragma only |
| JS-002 | No `return null` | PASS -- no null returns introduced | PASS -- only deletions | PASS -- pragma only |
| JS-033 | No `async void` | PASS -- all modified methods are synchronous void | PASS | PASS |
| JS-066 | CYC <= 8 per method | PASS -- Execute=8, Execute(List)=8 | PASS -- ExecuteFollowers=4 | N/A -- no methods modified |
| JS-081 | ASCII-only identifiers and string literals | PASS -- all new comments/strings are ASCII | PASS | PASS |
| NT8-021 | Account.All safe after Loaded handler on UI thread | PASS -- threading model unchanged | PASS | N/A |

---

## Execution Order Summary

```
T1: PttGlobalQuickExit.cs
    -- Reorder leaderStop before CancelPttBeOrders in Execute(Instrument) (lines 55-62 -> 10-line block)
    -- Reorder leaderStop before CancelPttBeOrders in Execute(List, Instrument) (lines 140-143 -> 5-line block)
    -- Update Execute(Instrument) XML summary line 31: add DW-B187 reference
    -- Add T_B187_01..04 [Fact] tests to CopyEngineTests.cs (append before line 8942)
    -> powershell -File .\scripts\ptt-sync.ps1
    -> dotnet test (T_B187_01..04 must pass)
    -> VERIFY_PASS(T1) gate

T2: PttGlobalQuickExit.cs (post-T1 source)
    -- Remove LogLeaderDiag call + 2-line comment from Execute(Instrument)
    -- Remove full DIAG block (38 lines) from ExecuteFollowers
    -- Delete LogLeaderDiag private static method (30 lines, lines 490-519 in pre-T1 source)
    -- Update ExecuteFollowers XML summary: CYC=7 -> CYC=4, update branch list
    -> powershell -File .\scripts\ptt-sync.ps1
    -> Select-String "LogLeaderDiag" -> 0 results
    -> Select-String "DW-B115-DIAG" -> 0 results
    -> VERIFY_PASS(T2) gate

T3: CopyEngine.cs
    -- Wrap PendingBeArmed (line 423) with #pragma warning disable/restore CS0067
    -> powershell -File .\scripts\ptt-sync.ps1
    -> dotnet build | Select-String "CS0067" -> 0 results
    -> VERIFY_PASS(T3) gate
```

---

*Written by: ptt-architect*
*Epic: B187*
*Phase: 3 (Ticket Generation)*
*Return: TICKETS_COMPLETE*
