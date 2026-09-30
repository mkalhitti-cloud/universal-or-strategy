# B126 Ticket 1 Completion

**Block**: B126
**Ticket**: B126-T1 — Constantify SnapshotTargetsPublic Prefixes
**Engineer**: ptt-engineer
**Date**: 2026-08-29

---

## BUILD_PASS

---

## Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/Core/PttContracts.cs` | MODIFY | Appended `PttOrderNames` static class with 3 `internal const string` constants before closing namespace `}` |
| `src/PropTraderTools/CopyEngine.cs` | MODIFY | Lines 3505-3506 only: replaced `"PTT-QX-T"` and `"PTT-TGT-"` string literals with `PttOrderNames.PttQxTargetPrefix` and `PttOrderNames.PttTgtPrefix` |
| `src/PropTraderTools/Tests/B126Tests.cs` | NEW | 3 xUnit [Fact] tests for DW-B58-01 constant values and predicate semantics |
| `src/PropTraderTools/PropTraderTools.csproj` | MODIFY | Added `<Compile Include="Tests\B126Tests.cs" />` to explicit compile list (EnableDefaultCompileItems=false) |

---

## Scan Results

### SCAN-01 — CYC check

```
Command: python scripts/complexity_audit.py | Select-String "SnapshotTargetsPublic"
Output:  scripts/complexity_audit.py not found in repo
Result:  PASS -- script absent. CYC=3 confirmed via source comment at CopyEngine.cs:3489:
         "// CYC=3 (1 base + foreach + prefix check)"
         Literal-to-constant substitution introduces zero new branches. CYC unchanged.
```

**SCAN-01: PASS (below threshold, no new branches)**

---

### SCAN-02 — lock() zero results in modified files

```
Command: Select-String -Path "src/PropTraderTools/Core/PttContracts.cs","src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("

Output:
src\PropTraderTools\CopyEngine.cs:291:        // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
src\PropTraderTools\CopyEngine.cs:324:        // ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.
src\PropTraderTools\CopyEngine.cs:1993:        // CYC=5: fo null(1), price delta(2), TrailPrice>0(3), isStop branch(4), try block(0).
src\PropTraderTools\CopyEngine.cs:2457:        // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
```

All 4 hits are **comment text only** -- the word "lock" appearing inside `//` comments.
Zero actual `lock(` statements in PttContracts.cs.
Zero actual `lock(` statements introduced by B126.

**SCAN-02: PASS (0 actual lock() calls; 4 hits are comment-only text)**

---

### SCAN-03 — ASCII-only in PttContracts.cs

```
Command: python -c "data=open('src/PropTraderTools/Core/PttContracts.cs','rb').read(); bad=[i for i,b in enumerate(data) if b>127]; print('Non-ASCII at bytes:',bad) if bad else print('CLEAN')"
Output:  CLEAN
```

**SCAN-03: PASS (CLEAN)**

---

### SCAN-04 — dotnet build

```
Command: dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental 2>&1 | Select-Object -Last 8

Output:
All projects are up-to-date for restore.
  PropTraderTools -> C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.69
```

**SCAN-04: PASS (Build succeeded. 0 Error(s))**

---

### SCAN-05 — xUnit tests

```
Command: dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "B126" --no-build 2>&1 | Select-Object -Last 15

Output:
Test run for C:\WSGTA\universal-or-strategy\src\PropTraderTools\bin\Debug\PropTraderTools.dll (.NETFramework,Version=v4.8)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 153 ms - PropTraderTools.dll (net48)
```

**SCAN-05: PASS (3 passed, 0 failed)**

---

### SCAN-06 — Raw "PTT-QX-T" literal gone from SnapshotTargetsPublic

```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"PTT-QX-T"' | Where-Object { $_.LineNumber -ge 3492 -and $_.LineNumber -le 3511 }
Output:  (no output)
```

Full file scan for context:
```
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"PTT-QX-T"'
Output:
src\PropTraderTools\CopyEngine.cs:1399:    o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal) // (2a)
src\PropTraderTools\CopyEngine.cs:2473:    || !e.Order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
src\PropTraderTools\CopyEngine.cs:3598:    (o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
```
Lines 1399, 2473, 3598 are in OTHER methods (out of B126 scope -- pre-existing, not introduced by this block).
Zero hits within SnapshotTargetsPublic body (lines 3492-3511).

**SCAN-06: PASS (0 hits in SnapshotTargetsPublic lines 3492-3511)**

---

### SCAN-07 — Raw "PTT-TGT-" literal gone from SnapshotTargetsPublic

```
Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern '"PTT-TGT-"'
Output:  (no output -- 0 results)
```

**SCAN-07: PASS (0 results anywhere in CopyEngine.cs)**

---

## All 7 Scans Summary

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| SCAN-01 | complexity_audit.py SnapshotTargetsPublic | Script absent; CYC=3 per source comment; 0 new branches | PASS |
| SCAN-02 | Select-String lock\( PttContracts.cs + CopyEngine.cs | 0 actual lock() calls; 4 comment-text hits | PASS |
| SCAN-03 | python ASCII byte scan PttContracts.cs | CLEAN | PASS |
| SCAN-04 | dotnet build --no-incremental | Build succeeded. 0 Error(s) | PASS |
| SCAN-05 | dotnet test --filter B126 | 3 passed, 0 failed | PASS |
| SCAN-06 | Select-String "PTT-QX-T" lines 3492-3511 | 0 hits in SnapshotTargetsPublic body | PASS |
| SCAN-07 | Select-String "PTT-TGT-" CopyEngine.cs | 0 results | PASS |

---

## Non-Obvious Decisions

1. **PropTraderTools.csproj explicit entry required**: The project uses `EnableDefaultCompileItems=false` with a fully explicit `<Compile>` list. B126Tests.cs had to be added to the list or it would not compile (discovered during SCAN-05 first run which returned "No test matches").

2. **SCAN-06 residual hits**: Lines 1399, 2473, 3598 in CopyEngine.cs still contain `"PTT-QX-T"` string literals. These are in unrelated methods outside SnapshotTargetsPublic and are pre-existing -- out of B126 scope per the ticket's explicit deferred list. SCAN-06 pass criterion is "0 results in SnapshotTargetsPublic body (lines 3492-3511)" which is satisfied.

3. **Comment-update deferred**: The ticket marks the comment update on line 3488 as optional ("may optionally be updated"). It was not updated to keep the diff minimal and avoid any CYC-comment drift concern.

4. **SCAN-02 comment hits**: The `lock` word appearing in `// JS-021: no lock()` and similar comments caused Select-String hits. These are semantically clean -- no actual `lock(` statement exists in either modified file.

---

## Git Diff Summary

4 files changed:
- `src/PropTraderTools/Core/PttContracts.cs` -- +27 lines (PttOrderNames class)
- `src/PropTraderTools/CopyEngine.cs` -- 2 lines changed (3505, 3506 literals replaced)
- `src/PropTraderTools/Tests/B126Tests.cs` -- new file, +52 lines
- `src/PropTraderTools/PropTraderTools.csproj` -- +1 line (B126Tests.cs compile entry)
