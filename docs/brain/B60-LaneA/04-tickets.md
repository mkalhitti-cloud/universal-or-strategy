# B60-LaneA -- Ticket-1

**Written by**: ptt-architect (Phase 3)
**Date**: 2026-08-10
**Status**: AWAITING TICKET_REVIEW_PASS (Ph3.5)
**Plan source**: docs/brain/B60-LaneA/02-architecture-plan.md (REVIEW_PASS)

---

## Header

**Spec requirement IDs**: DW-B60-01, DW-B59-02
**Files touched**:
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/CopyEngineTests.cs`

**Commit message**: `fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]`

**Rationale for single ticket**: Total diff is ~35 lines (< 50-line threshold). Both changes are
in the same file (CopyEngine.cs) and same test file (CopyEngineTests.cs). No interaction conflict.

---

## Change 1 -- DW-B59-02: IsExitSignalName prefix fix

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `IsExitSignalName` (internal static bool, CopyEngine.cs:724)
**Problem**: Line 730 uses exact equality `name == "Rev"`. NT8 reversal orders may use longer names
("Reversal", "RevLong", "RevShort") that start with "Rev" but do not match exactly. Those orders
pass Gate 0.5 and are dispatched to followers as phantom copies.
**Fix**: Replace exact equality with `StartsWith("Rev", StringComparison.Ordinal)` prefix check.
**CYC**: Before=6, After=6 (same branch count -- `==` replaced by `StartsWith`, no new branch).

### apply_diff anchor

**OLD TEXT** (from live source, lines 729-731):
```
            if (name == "Flatten")                                         return true;
            if (name == "Rev")                                             return true;
            if (name.StartsWith("Exit", StringComparison.Ordinal))        return true;
```

**NEW TEXT**:
```
            if (name == "Flatten")                                         return true;
            if (name.StartsWith("Rev", StringComparison.Ordinal))         return true;
            if (name.StartsWith("Exit", StringComparison.Ordinal))        return true;
```

**Diff summary**: Line 730 only. One character-level change: `name == "Rev"` becomes
`name.StartsWith("Rev", StringComparison.Ordinal)`. Surrounding lines unchanged for diff anchor.

---

## Change 2a -- DW-B60-01: New helper method TryDispatchLeaderFlat

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insert location**: After `HasOpenPosition` body (after line 964), before `HasWorkingEntries` (line 966).
**CYC**: 2 (two guard returns).

### apply_diff anchor for new method

**OLD TEXT** (from live source, lines 962-967):
```
            var pos = FindPosition(acc, instrument);                        // (1) branch
            if (pos == null)
                return false;
            return pos.Quantity > 0;
        }

        // CYC=3. Returns true if any working non-bracket order exists for the instrument.
```

**NEW TEXT** (insert new method between `HasOpenPosition` close-brace and `HasWorkingEntries` comment):
```
            var pos = FindPosition(acc, instrument);                        // (1) branch
            if (pos == null)
                return false;
            return pos.Quantity > 0;
        }

        // DW-B60-01: Detect leader-flat and fan out PTT-Flatten to followers.
        // CYC=2: (1) follower guard, (2) position guard.
        // Only called from OnOrderUpdate after Gates 1+2+2.5 (copy enabled, rule matched).
        // JS-001: no throw. JS-002: returns bool. JS-021: no lock.
        // TESTABILITY: private instance -- coverage via manual NT8 integration test.
        private bool TryDispatchLeaderFlat(Account account, Instrument instrument)
        {
            if (IsFollowerAccount(account)) return false;           // (1) guard: not a follower
            if (HasOpenPosition(account, instrument)) return false; // (2) guard: leader is flat
            Flatten(account, instrument);
            return true;
        }

        // CYC=3. Returns true if any working non-bracket order exists for the instrument.
```

---

## Change 2b -- DW-B60-01: Wire-up call in OnOrderUpdate

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insert location**: After Cancelled block close (after line 643), before Gate B comment (line 645).
**Effect**: When leader goes flat after a filled/cancelled order, and copy is enabled with a matched
rule, `TryDispatchLeaderFlat` fires and returns true -- skipping `DispatchCopy`. If the leader still
has a position (not flat), returns false and flow continues normally to Gate B and `DispatchCopy`.

### apply_diff anchor for OnOrderUpdate insertion

**OLD TEXT** (from live source, lines 640-647):
```
                    CancelOneAccount(acc, e.Order.Instrument);
                }
                return;
            }

            // Gate B: bracket drag detection -- divert to HandleBracketChange path
            if (IsWorkingBracket(e.Order))
            {
```

**NEW TEXT** (2 new lines inserted between Cancelled block and Gate B comment):
```
                    CancelOneAccount(acc, e.Order.Instrument);
                }
                return;
            }

            // DW-B60-01: leader went flat -- propagate close to followers
            if (TryDispatchLeaderFlat(e.Order.Account, e.Order.Instrument)) return;

            // Gate B: bracket drag detection -- divert to HandleBracketChange path
            if (IsWorkingBracket(e.Order))
            {
```

---

## Test Changes -- CopyEngineTests.cs

**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Insertion point**: After line 2809 (end of T_B59_07 test body), before line 2812 (class closing `}`).

**CRITICAL**: All tests use xUnit `[Fact]` ONLY. No NUnit. No MSTest. No `[Theory]`.
**Test class**: `CopyEngineTests` (existing class, no new class needed).

### Tests for DW-B59-02 (3 new [Fact] tests)

These 3 tests verify that the old `name == "Rev"` exact match would have returned `false` for these
inputs, and the new `StartsWith("Rev", ...)` returns `true`. Pure static calls -- no NT8 runtime.

**OLD TEXT** (from live source, lines 2808-2812 -- anchor for insertion):
```
            Assert.False(CopyEngine.IsExitSignalName(""));
        }


    }
}
```

**NEW TEXT** (insert 3 [Fact] tests before class close):
```
            Assert.False(CopyEngine.IsExitSignalName(""));
        }

        // B60 T1: Rev prefix widening -- DW-B59-02 fix verification.
        // Verifies that StartsWith("Rev") catches all NT8 reversal order name variants.
        // Old exact match (name == "Rev") would return false for all three inputs below.

        [Fact]
        public void T_B60_Rev_01_IsExitSignalName_Reversal_ReturnsTrue()
        {
            // "Reversal" starts with "Rev" -- must be blocked after StartsWith fix.
            Assert.True(CopyEngine.IsExitSignalName("Reversal"));
        }

        [Fact]
        public void T_B60_Rev_02_IsExitSignalName_RevLong_ReturnsTrue()
        {
            // "RevLong" (long reversal variant) starts with "Rev" -- must be blocked.
            Assert.True(CopyEngine.IsExitSignalName("RevLong"));
        }

        [Fact]
        public void T_B60_Rev_03_IsExitSignalName_RevShort_ReturnsTrue()
        {
            // "RevShort" (short reversal variant) starts with "Rev" -- must be blocked.
            Assert.True(CopyEngine.IsExitSignalName("RevShort"));
        }


    }
}
```

### Tests for DW-B60-01 (not unit-testable -- manual NT8 required)

`TryDispatchLeaderFlat` is a private instance method. Its two guard conditions delegate entirely to
`IsFollowerAccount` (CYC=3, covered by existing tests) and `HasOpenPosition` (CYC=2, covered by
existing tests). The method body is 3 lines with CYC=2.

**Decision**: No new automated unit tests added for `TryDispatchLeaderFlat`. Coverage is achieved
via existing tests on its delegates plus the mandatory manual NT8 integration test below.

**Automated test count for commit message**: 3 new tests (T_B60_Rev_01, Rev_02, Rev_03).
Update commit message N=3: `fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]`

---

## JS Rule Constraints

| Rule | Applies To | Requirement | Status |
|------|-----------|-------------|--------|
| JS-001 | TryDispatchLeaderFlat, OnOrderUpdate insertion | No `throw new XxxException` in any new code | PASS -- no throw introduced |
| JS-002 | TryDispatchLeaderFlat | No `return null` | PASS -- method returns bool |
| JS-021 | All new code | No `lock()` | PASS -- no lock anywhere |
| JS-033 | All new code | No `async void` (non-event) | PASS -- no async methods added |
| ASCII-only | All new string literals and comments | ASCII characters only | PASS -- "Rev", "--" comments, StringComparison.Ordinal all ASCII |

---

## CYC Constraints

| Method | CYC Before | CYC After | Verdict |
|--------|------------|-----------|---------|
| `IsExitSignalName` | 6 | 6 (unchanged -- same branch, `==` replaced by `StartsWith`) | <= 8 PASS |
| `TryDispatchLeaderFlat` (new) | N/A | 2 (two guard returns) | <= 8 PASS |
| `OnOrderUpdate` | 11 (pre-existing violation) | 12 (+1 via single helper call) | PRE-EXISTING -- not introduced by B60 |
| `HasOpenPosition` | 2 | 2 (read-only, not modified) | <= 8 PASS |
| `IsFollowerAccount` | 3 | 3 (read-only, not modified) | <= 8 PASS |
| `Flatten(Account,Instrument)` | 4 | 4 (not modified) | <= 8 PASS |

**Note**: `OnOrderUpdate` pre-existing CYC=11 is not introduced by B60. The single
`TryDispatchLeaderFlat` helper call keeps all new logic at CYC=2. Reducing `OnOrderUpdate` below
CYC=8 is a separate future epic.

---

## 7-Scan Checklist (MANDATORY -- engineer must complete ALL before BUILD_PASS)

The engineer MUST run every scan below and confirm the required result before committing.

| Scan | Command | Required Result |
|------|---------|----------------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-02 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-03 | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-04 | `grep -n "name == \"Rev\"" src/PropTraderTools/CopyEngine.cs` | 0 matches (old exact match gone) |
| SCAN-05 | `grep -n "StartsWith.*\"Rev\"" src/PropTraderTools/CopyEngine.cs` | >= 1 match (new prefix match present) |
| SCAN-06 | `grep -n "T_B60_" src/PropTraderTools/CopyEngineTests.cs` | >= 3 matches (Rev_01, Rev_02, Rev_03) |
| SCAN-07 | `grep -n "IsFollowerAccount" src/PropTraderTools/CopyEngine.cs` | >= 2 matches (definition at ~400 + TryDispatchLeaderFlat body) |
| SCAN-08 | `powershell -File .\scripts\verify_links.ps1 -Fix` | DESYNC=0, exit 0 |

---

## Verification Steps

The engineer must complete ALL steps before writing ticket-1-completion.md.

1. **Build**: `dotnet build src/PropTraderTools` -- must exit 0 with 0 errors
2. **Test**: `dotnet test src/PropTraderTools` -- must exit 0; all T_B60_Rev_01..03 facts pass
3. **All 7 scans**: Run each scan in the table above; confirm required results
4. **verify_links**: `powershell -File .\scripts\verify_links.ps1 -Fix` -- DESYNC=0
5. **NT8 F5 compile**: Copy CopyEngine.cs to NT8 path (see deploy step below), F5 in NinjaTrader -- must compile green
6. **Manual live test for DW-B60-01**:
   - Start NinjaTrader 8 with CopyEngine add-on loaded
   - Enable copy: leader=Sim101, follower=Sim102, instrument=NQ 09-26
   - Enter 1 contract long on Sim101 (leader); verify Sim102 copies the position
   - Click "Close" on Sim101 position in the Positions tab
   - **Expected**: Within ~1 second, Sim102 position closes (PTT-Flatten market order submitted)
   - **Verify**: CopyEngine status log shows "PTT-Flatten" dispatch to Sim102

---

## Deploy Steps (deploy-sync.ps1 is ARCHIVED -- manual copy required)

After build + test pass:
```powershell
Copy-Item src\PropTraderTools\CopyEngine.cs "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" -Force
```

---

## Commit Steps

After all scans + verification pass:
```
1. git add src/PropTraderTools/CopyEngine.cs src/PropTraderTools/CopyEngineTests.cs
2. git commit -m "fix(ptt): B60 -- leader-close propagation + Rev prefix fix [3 tests]"
3. Record commit hash in docs/brain/B60-LaneA/ticket-1-completion.md
4. powershell -File .\scripts\verify_links.ps1 -Fix   -- confirm DESYNC=0
```

---

## Diff Size Estimate

| Change | Lines added | Lines changed | Approx chars |
|--------|-------------|---------------|--------------|
| Change 1: IsExitSignalName line 730 | 0 | 1 | ~80 |
| Change 2a: TryDispatchLeaderFlat method (11 lines) | 11 | 0 | ~420 |
| Change 2b: OnOrderUpdate insertion (2 lines) | 2 | 0 | ~110 |
| Tests: T_B60_Rev_01..03 (3 facts, ~21 lines) | 21 | 0 | ~600 |
| **Total** | **~34** | **1** | **~1,210** |

Well within 10,000-char diff limit per JS-PR hygiene mandate.

---

## Completion Gate

After ptt-ticket-reviewer signs off with TICKET_REVIEW_PASS, ptt-engineer executes this ticket.
The engineer writes `docs/brain/B60-LaneA/ticket-1-completion.md` with:
- Exact commit hash
- Build/test exit codes
- SCAN-01..08 results (pass/fail per scan)
- Manual NT8 test result for DW-B60-01
- Any deviations from this ticket (must be approved by ptt-architect before deviating)
