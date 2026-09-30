# B60-LaneA Architecture Plan

**Block**: B60
**Lane**: A
**Phase**: 1 (Architecture)
**Written by**: ptt-architect
**Date**: 2026-08-10
**Status**: REVIEW_PENDING

Defects closed: DW-B60-01 (leader-close propagation), DW-B59-02 (Rev prefix widening)

---

## Section A -- Rules Catalog Gate

Gate run against `docs/standards/jane-street/RULES_CATALOG.md` (UTF-8 clean, JS-001..JS-110).

| Rule | Description | Status |
|------|-------------|--------|
| JS-001 | No throw in hot path | PASS -- no throw introduced |
| JS-002 | No return null | PASS -- TryDispatchLeaderFlat returns bool, not null |
| JS-021 | No lock() | PASS -- no lock() in any new code |
| JS-033 | No async void (non-event) | PASS -- no async methods added |
| CYC<=8 | All NEW methods cyclomatic complexity <= 8 | PASS -- TryDispatchLeaderFlat CYC=2; IsExitSignalName CYC=6 (unchanged) |
| ASCII-only | All new string literals and comments are ASCII | PASS -- "Rev", "--" comments, StringComparison.Ordinal all ASCII |

**GATE RESULT: PASS**

Note: OnOrderUpdate pre-existing CYC=11 (pre-existing violation, not introduced by B60).
B60 adds 1 branch to OnOrderUpdate via a single helper call (`TryDispatchLeaderFlat`).
All new code introduced by B60 is extracted into a CYC=2 helper method -- no new violation introduced.

---

## Section B -- Problem Statements

### DW-B59-02 -- IsExitSignalName uses exact "Rev" match instead of prefix (P1)

`IsExitSignalName` at `CopyEngine.cs:730` uses `name == "Rev"` (exact equality).
The architecture plan for B59 specified `name.StartsWith("Rev", StringComparison.Ordinal)` to block
all NT8 reversal order names (e.g. "Reversal", "RevLong", "RevShort"). Only an order literally
named `"Rev"` is currently blocked. Live NT8 reversal orders may use longer names that begin with
"Rev" but do not match exactly. Those orders pass through Gate 0.5 and are dispatched to followers
as phantom copies.

**NT8 reference note**: `docs/standards/NT8_FULL_REFERENCE.md` contains no documentation of
specific reversal order names ("RevLong", "RevShort", "Reversal"). The StartsWith("Rev") prefix
approach is the correct defensive strategy to catch any NT8-generated reversal order name that
begins with "Rev" regardless of exact name.

### DW-B60-01 -- Leader manual close does not close follower position (P1)

When the leader closes their position via the Positions tab Close button, NT8 generates an order
with `Name = "Close"`. Gate 0.5 (B59) correctly blocks that order from being forwarded as a
phantom copy to followers (`IsExitSignalName("Close")` returns true inside `DispatchCopy`).
However, the follower position remains open.

**Root cause**: After Gate 0.5 fires inside `DispatchCopy`, the copy event is silently dropped.
There is no hook that detects "leader just went flat" and triggers `Flatten` on followers.
The existing `TryFirePositionState` fires the `PositionStateChanged` event BEFORE Gate 1 (before
the copy-enabled check), but that event only notifies the UI -- it does not call `Flatten`.

**Infrastructure already present** (wire-up only, no new patterns):
- `TryFirePositionState` at `CopyEngine.cs:938` fires before Gate 1 on every Filled/PartFilled/
  Cancelled/Rejected event. It already detects leader-flat via `HasOpenPosition`.
- `Flatten(Account leader, Instrument instrument)` at `CopyEngine.cs:1135` fans out `PTT-Flatten`
  market orders to all follower accounts for an instrument.
- `IsFollowerAccount(Account acc)` at `CopyEngine.cs:400` guards against follower-triggered
  recursion.
- `HasOpenPosition(Account acc, Instrument instrument)` at `CopyEngine.cs:958` checks if account
  is now flat (returns `false` when flat).

**Live evidence**: 2026-08-10 test log shows 18-second gap between leader close and follower
manual close. `PositionStateChanged hasPos=False` fired correctly but no follower flatten occurred.

---

## Section C -- Source Analysis (cited line numbers from live source)

### For DW-B59-02

| Item | Location | Content |
|------|----------|---------|
| `IsExitSignalName` definition | `CopyEngine.cs:724` | `internal static bool IsExitSignalName(string name)` |
| Exact line with `"Rev"` exact match | `CopyEngine.cs:730` | `if (name == "Rev")                                             return true;` |
| Method body extent | `CopyEngine.cs:724-733` | 6 `if` branches, CYC=6 |

Replacement: Line 730 -- replace exact-equality `==` with prefix `StartsWith` (same branch, CYC unchanged).

### For DW-B60-01

| Item | Location | Content |
|------|----------|---------|
| `OnOrderUpdate` entry | `CopyEngine.cs:600` | `private void OnOrderUpdate(object sender, OrderEventArgs e)` |
| `TryFirePositionState` call | `CopyEngine.cs:603` | Before Gate 1 -- fires unconditionally |
| Gate 1 (copy-enabled) | `CopyEngine.cs:606-607` | `if (!_isCopyEnabled) return;` |
| Gate 2 (rule match) | `CopyEngine.cs:611-621` | foreach loop + `if (matchedRule == null) return;` |
| Gate 2.5 (per-rule enabled) | `CopyEngine.cs:624-625` | `if (!matchedRule.Value.Enabled) return;` |
| Cancelled block | `CopyEngine.cs:635-643` | `if (OrderState.Cancelled)` -- cancels followers and returns |
| Gate B (bracket drag) | `CopyEngine.cs:646-652` | `if (IsWorkingBracket(e.Order))` |
| `DispatchCopy` call | `CopyEngine.cs:655` | Normal copy dispatch path |
| `TryFirePositionState` def | `CopyEngine.cs:938` | Fires before Gate 1 -- called at line 603 |
| `HasOpenPosition` def | `CopyEngine.cs:958` | Private instance method, CYC=2 |
| `IsFollowerAccount` def | `CopyEngine.cs:400` | Internal instance method, CYC=3 |
| `Flatten(Account,Instrument)` def | `CopyEngine.cs:1135` | Internal instance method, CYC=4 |

**Exact insertion point**: After line 643 (end of Cancelled block `return;`) and BEFORE line 645
(`// Gate B: bracket drag detection`). This ensures:
1. Copy is enabled (Gate 1 passed at line 607)
2. Rule is matched (Gate 2 passed at line 621)
3. Rule is enabled (Gate 2.5 passed at line 625)
4. Order is not Cancelled (Cancelled block already returned at line 643 if applicable)
5. Before DispatchCopy so leader-flat fires even for "Close" orders (which DispatchCopy would block)

---

## Section D -- Exact Changes (engineer contract)

### Change D-1: DW-B59-02 -- IsExitSignalName prefix fix

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `IsExitSignalName` (internal static bool)
**Line**: 730

**OLD text** (exact, verified from live source `CopyEngine.cs:730`):
```
            if (name == "Rev")                                             return true;
```

**NEW text**:
```
            if (name.StartsWith("Rev", StringComparison.Ordinal))         return true;
```

No other lines in the method change. CYC stays at 6.

---

### Change D-2: DW-B60-01 -- Leader-flat propagation

#### 2a. New private helper method TryDispatchLeaderFlat

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insert location**: After the body of `HasOpenPosition` (after `CopyEngine.cs:964`), before `HasWorkingEntries` (line 966).

**NEW method to insert** (exact text):
```csharp
        // DW-B60-01: Detect leader-flat and fan out PTT-Flatten to followers.
        // CYC=2: (1) follower guard, (2) position guard.
        // Only called from OnOrderUpdate after Gates 1+2+2.5 (copy enabled, rule matched).
        // JS-001: no throw. JS-002: returns bool. JS-021: no lock.
        // TESTABILITY: private instance -- testable via CopyEngine harness.
        private bool TryDispatchLeaderFlat(Account account, Instrument instrument)
        {
            if (IsFollowerAccount(account)) return false;           // (1) guard: not a follower
            if (HasOpenPosition(account, instrument)) return false; // (2) guard: leader is flat
            Flatten(account, instrument);
            return true;
        }
```

#### 2b. Insertion in OnOrderUpdate

**File**: `src/PropTraderTools/CopyEngine.cs`
**Line range context**: Lines 639-650 (after Cancelled block, before Gate B)

**OLD text** (exact lines 641-647 from live source):
```
                }
                return;
            }

            // Gate B: bracket drag detection -- divert to HandleBracketChange path
            if (IsWorkingBracket(e.Order))
            {
```

**NEW text** (insert 3 lines between the closing brace of Cancelled block and Gate B comment):
```
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

## Section E -- CYC Analysis

| Method | Pre-B60 CYC | Post-B60 CYC | Verdict |
|--------|-------------|--------------|---------|
| `IsExitSignalName` | 6 | 6 (unchanged -- `==` replaced by `StartsWith`, same branch) | <=8 PASS |
| `TryDispatchLeaderFlat` | N/A (new) | 2 | <=8 PASS |
| `OnOrderUpdate` | 11 (pre-existing) | 12 (+1 via single helper call) | PRE-EXISTING FAIL (not introduced by B60) |
| `HasOpenPosition` | 2 | 2 (read-only, not modified) | <=8 PASS |
| `IsFollowerAccount` | 3 | 3 (read-only, not modified) | <=8 PASS |
| `Flatten(Account,Instrument)` | 4 | 4 (not modified) | <=8 PASS |

**CYC note**: `OnOrderUpdate` pre-existing CYC=11 was not introduced by B60. B60 adds one branch
(the `TryDispatchLeaderFlat` helper call) making it CYC=12. All newly written code (the helper
method body) is CYC=2. The extract-helper pattern minimizes the branch added to `OnOrderUpdate`.
Reducing `OnOrderUpdate` below CYC=8 is a separate future epic.

---

## Section F -- Test Plan

All tests use xUnit `[Fact]` only. No NUnit. No MSTest.
Test class: `CopyEngineTests` (existing file: `src/PropTraderTools/CopyEngineTests.cs`).

### DW-B59-02 tests -- New [Fact] tests in region T_B59_05 or new T_B60_Rev region

**T_B60_Rev_01**: `IsExitSignalName("Reversal")` returns `true`
- Verifies full word "Reversal" (not just "Rev") is blocked after StartsWith fix.

**T_B60_Rev_02**: `IsExitSignalName("RevLong")` returns `true`
- Verifies "RevLong" (long reversal variant) is blocked.

**T_B60_Rev_03**: `IsExitSignalName("RevShort")` returns `true`
- Verifies "RevShort" (short reversal variant) is blocked.

All three tests verify that the old `name == "Rev"` would have returned `false` for these inputs
and the new `StartsWith` returns `true`. Tests are pure static method calls -- no NT8 runtime needed.

Example test body (T_B60_Rev_01):
```csharp
[Fact]
public void T_B60_Rev_01_IsExitSignalName_Reversal_ReturnsTrue()
{
    Assert.True(CopyEngine.IsExitSignalName("Reversal"));
}
```

### DW-B60-01 tests

`TryDispatchLeaderFlat` is a private instance method. Direct unit testing requires a CopyEngine
instance. The CopyEngine constructor requires NT8 runtime objects (Account.All, etc.) which are
not available in xUnit without a full NT8 harness.

**What IS testable as unit tests** (via extracted static helper pattern -- if engineer chooses to
expose the guard logic):
The two guard conditions in `TryDispatchLeaderFlat` delegate entirely to `IsFollowerAccount` and
`HasOpenPosition`, both of which are already covered by existing tests. The `TryDispatchLeaderFlat`
body is 3 lines with CYC=2. Coverage is achieved through integration test (manual NT8 test).

**T_B60_01_IsExitSignalName_RevShortVariant_ReturnsTrue** (see T_B60_Rev_03 -- same)

**Manual NT8 integration test (document in ticket, not automated)**:
1. Start NinjaTrader 8 with CopyEngine loaded.
2. Enable copy: leader=Sim101, follower=Sim102, instrument=NQ 09-26.
3. Enter 1 contract long on Sim101 (leader). Verify Sim102 copies.
4. Click "Close" on Sim101 position in the Positions tab.
5. Expected: Within 1 second, Sim102 position closes (PTT-Flatten market order submitted).
6. Verify: CopyEngine status log shows "PTT-Flatten" dispatch to Sim102.

---

## Section G -- 7-Scan Checklist (engineer contract)

These scans MUST all pass before the ticket is considered complete.

| Scan | Command | Required Result |
|------|---------|----------------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-02 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-03 | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 matches |
| SCAN-04 | `grep -n 'name == "Rev"' src/PropTraderTools/CopyEngine.cs` | 0 matches (old exact match gone) |
| SCAN-05 | `grep -n 'StartsWith.*"Rev"' src/PropTraderTools/CopyEngine.cs` | >=1 match (new prefix match present) |
| SCAN-06 | `grep -n "T_B60_" src/PropTraderTools/CopyEngineTests.cs` | >=3 matches (Rev tests) |
| SCAN-07 | `grep -n "IsFollowerAccount" src/PropTraderTools/CopyEngine.cs` | >=1 match near TryDispatchLeaderFlat |
| SCAN-08 | `powershell -File .\scripts\verify_links.ps1 -Fix` | Must report DESYNC=0 and exit 0 |

---

## Section H -- Diff Size Estimate

| Change | Lines added | Lines changed | Approx chars |
|--------|-------------|---------------|--------------|
| D-1: IsExitSignalName line 730 | 0 | 1 | ~80 chars |
| D-2a: TryDispatchLeaderFlat method | 11 | 0 | ~420 chars |
| D-2b: Insertion in OnOrderUpdate | 2 | 0 | ~110 chars |
| Tests: T_B60_Rev_01..03 (3 facts) | ~21 | 0 | ~600 chars |
| **Total** | **~34** | **1** | **~1,210 chars** |

Well within 10,000 char diff limit. ✓

---

## Section I -- NT8 API Notes

Citing `docs/standards/NT8_FULL_REFERENCE.md`:

| API | Reference Line | Usage in B60 |
|-----|---------------|--------------|
| `Order.Name` (string) | Line 1024, Line 1107 | Used in `IsExitSignalName` -- string comparison only |
| `Account.Flatten()` | Line 358-359 | NOT called directly; `CopyEngine.Flatten(Account,Instrument)` at line 1135 is our own method that submits PTT-Flatten orders via `CreateOrder+Submit` |
| `CreateOrder()` | Line 338-339 | Called by `FlattenOneAccount` (inside `CopyEngine.Flatten`) -- no change needed |

**NT8 reversal order names**: `NT8_FULL_REFERENCE.md` contains no documentation of specific
reversal order names ("RevLong", "RevShort", "Reversal"). The `StartsWith("Rev", ...)` prefix
approach defensively catches any NT8 reversal order name that begins with "Rev".

**No new NT8 API calls** are introduced in B60. Both changes wire up existing infrastructure.
`AtmStrategyCreate()` is confirmed StrategyBase-only (NT8_FULL_REFERENCE.md) -- not used in B60.

---

## Section J -- Carry-Forward Deferred Items

The following items are NOT closed in B60 and carry forward to future blocks:

| Item | Priority | Status | Notes |
|------|----------|--------|-------|
| DW-B58-01 | P2 | OPEN | `SnapshotTargetsPublic` hardcoded order-name prefixes -- future block |
| DW-B58-02 | P2 | OPEN | `GlobalBe` non-atomic lazy init -- safe until non-UI-thread caller added |
| DW-B58-03 | P2 | OPEN | `RelayBe` does not forward OcoGroup -- future block |
| DW-B54-01 | P1 | OPEN (blocked) | ATM auto-inject -- requires StrategyBase, unavailable in AddOnBase |
| PRE-EXISTING-01 | P2 | OPEN | Non-ASCII at CopyEngine.cs lines 395, 496 -- pre-existing |
| PRE-EXISTING-02 | P2 | OPEN | Non-ASCII at CopyEngine.cs lines 1256, 1257 -- pre-existing |
| PRE-EXISTING-03 | P2 | OPEN | deploy-sync.ps1 archived; manual copy workflow unchanged |

**Items closed by B60** (will be documented in 05-final-review.md):
- DW-B60-01: Leader manual close propagation -- CLOSED by Change D-2
- DW-B59-02: Rev exact-match too narrow -- CLOSED by Change D-1

---

## Section K -- Commit Steps (engineer contract)

After all scans pass:
1. `git add src/PropTraderTools/CopyEngine.cs src/PropTraderTools/CopyEngineTests.cs`
2. `git commit -m "fix(ptt): B60 -- leader-close propagation + Rev prefix fix [N tests]"`
3. Record commit hash in ticket-1-completion.md
4. Run `powershell -File .\scripts\verify_links.ps1 -Fix` -- confirm DESYNC=0
