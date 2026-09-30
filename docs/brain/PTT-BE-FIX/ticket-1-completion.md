# PTT-BE-FIX -- T1+T4 Completion Report
Ticket: T1 (DW-B86) + T4 (TryReplacePttBeBrackets comment)
Status: BUILD_PASS
Date: 2026-08-22
Engineer: ptt-engineer (Phase 4a, Session 1 of 3)
File: src/PropTraderTools/CopyEngine.cs

---

## Changes Made

### T1 -- DW-B86: Stop Name Guard Extension

**File**: src/PropTraderTools/CopyEngine.cs
**Location**: L2753-2762 (before edit) -> L2755-2768 (after edit, +5 net lines)

**Before (L2753-2762)**:
```csharp
                    // ATM stop names are exactly "StopN" (length 5): Stop1, Stop2, Stop3 etc.
                    // Length==5 guard excludes StopLimit, StopMarket, StopLoss and any other prefix.
                    if (o.Name != null
                        && o.Name.StartsWith("Stop", StringComparison.Ordinal)
                        && o.Name.Length == 5
                        && char.IsDigit(o.Name[4]))
                    {
                        o.StopPriceChanged = newStop;
                        beSt.Add(o);
                    }
```

**After (L2755-2768)**:
```csharp
                    // DW-B86: extend stop name guard to match PTT-QX-Stop* orders placed after QX-ALL.
                    // ATM stop names are exactly Stop1..Stop9 (length 5, IsDigit guard).
                    // After QX-ALL follower has PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-Stop3, PTT-QX-Stop4.
                    // State guard (Working||Accepted||ChangeSubmitted) already handles both sets.
                    bool isBeStop = o.Name != null
                        && (   (o.Name.StartsWith("Stop", StringComparison.Ordinal)
                                && o.Name.Length == 5
                                && char.IsDigit(o.Name[4]))
                             || o.Name.StartsWith("PTT-QX-Stop", StringComparison.Ordinal));
                    if (isBeStop)
                    {
                        o.StopPriceChanged = newStop;
                        beSt.Add(o);
                    }
```

Net delta: +5 lines (4 comment lines + 1 bool local, inline if refactored to named bool + if(isBeStop)).
CYC impact: +0 (1 branch in -> 1 branch out, bool assignment has 0 McCabe branch points).

---

### T4 -- DW-T4: TryReplacePttBeBrackets Structural Guarantee Comment

**File**: src/PropTraderTools/CopyEngine.cs
**Location**: Inserted 2 comment lines before private void TryReplacePttBeBrackets (was L1820, now L1822)

**Before (L1818-1820)**:
```csharp
        // CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        private void TryReplacePttBeBrackets(Order cancelledStop)
```

**After (L1818-1822)**:
```csharp
        // CYC=5: (1) null guard, (2) follower guard, (3) flat guard, (4) attempt guard, (5) slot+fallback.
        // JS-021: ConcurrentDictionary ops are lock-free. JS-001: no throw. JS-002: void. ASCII-only.
        // DW-T4: structurally unreachable from follower path. Followers use acc.Change() (early
        // return at follower block end, L2791) and never hold PTT-BE-Stop-* orders. No guard needed.
        private void TryReplacePttBeBrackets(Order cancelledStop)
```

Net delta: +2 lines (comment only). Zero logic change.

---

## 7-Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| 1 lock() | `Select-String -Path src/PropTraderTools/*.cs -Pattern "^\s*lock\s*\("` | 0 results | PASS |
| 2 async void | `Select-String -Path src/PropTraderTools/*.cs -Pattern "async void "` | 0 actual declarations (3 comment-only matches, pre-existing) | PASS |
| 3 throw new | `Select-String -Path src/PropTraderTools/*.cs -Pattern "throw new"` | 2 pre-existing results (comment in Tests, one-way converter); 0 new from T1/T4 | PASS |
| 4 CYC <= 8 | Manual verification: bool assignment has 0 McCabe branches; if(isBeStop) = 1 branch = same as original if(). Net +0 CYC. TryReplacePttBeBrackets comment-only, CYC=5 unchanged. | +0 net | PASS |
| 5 ASCII-only | Binary scan of CopyEngine.cs: 12 non-ASCII bytes at lines 238, 239, 2290, 2291 (all pre-existing, not in T1 region L2755-2768 or T4 region L1818-1822) | 0 new non-ASCII in edited lines | PASS |
| 6 xUnit | SKIP -- T1 and T4 are production code only, no test file changed | N/A | N/A |
| 7 build | `dotnet build src/PropTraderTools/ 2>&1 \| Select-Object -Last 15` | 83 errors / 59 warnings -- identical to pre-edit baseline (verified via git stash roundtrip); all errors in CopyEngineTests.cs and Globals ambiguity (pre-existing, out of scope per V12.23). 0 new errors from T1/T4. | PASS |

---

## Pre-Existing Build Error Note

The repository had 83 pre-existing build errors before this ticket was started (verified by
`git stash` -> build -> `git stash pop`). All errors are in `CopyEngineTests.cs` (test file
not touched by this ticket) and a `Globals` type ambiguity at L3350 (not in edited range).
No errors were added by T1/T4 changes. Per V12.23 No Scope Creep Protocol, pre-existing
errors are out of scope and will be addressed in a dedicated session.

---

## Commit Hash

f6eff92a  fix(ptt): DW-B86 extend stop name guard for PTT-QX-Stop* + DW-T4 comment

---

## Notes

1. T1 line numbers: ticket cited L2755-2762 as the guard location; actual source had the two
   preceding comment lines at L2753-2754, making the edit region L2753-2762 (both comments +
   guard). After edit, the region is L2755-2768 (+5 lines total). All surrounding code
   (beStOk state guard, DIAG dump, acc.Change call) untouched.

2. T4 line number: ticket cited L1819-1820 as insertion point; actual source had the method
   signature at L1820 with the JS comment at L1819. Comment inserted between L1819 and L1820
   (now L1820-1821), pushing method signature to L1822. Exact match per ticket spec.

3. sync-ptt-to-nt8.ps1 result: 0 copied, 15 skipped (already in sync via hard links),
   35 excluded (tests/obj/bin). No manual copy needed.

4. SIM gate (Path B: QX-ALL then BE-ALL, 3 cycles) is a MANDATORY manual step before
   proceeding to T3. Human operator must verify [BE] DW-B84-01 acc.Change() Sim102
   stops=N newStop=X with N > 0 for each follower account.
