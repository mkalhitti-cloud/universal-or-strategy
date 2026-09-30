# B129 LaneA — Ticket 1: DW-B135 — Clear _lastLeaderDirection on Leader Flat

**Block**: B129 LaneA
**Ticket**: T-1 (single ticket, single concern)
**Defect**: DW-B135 — Reversal Guard False-Positive After Leader Flat
**Author**: ptt-architect
**Date**: 2026-08-31
**Plan**: `docs/brain/B129/LaneA-02-architecture-plan.md` (REVIEW_PASS — R-01..R-10 all PASS)

---

## T-01 — Spec Requirement IDs

| Requirement | Description | Status |
|-------------|-------------|--------|
| **DW-B135** | Reversal guard fires false-positive after leader closes flat. `_lastLeaderDirection` key is never cleared on leader flat transition. Fix: clear the key in `TryFirePositionState` on the `hasPos=False` path for leader accounts. | **THIS TICKET** |
| **DW-B128** | Close-order race window protection must NOT be broken. Guard must still fire when a Sell signal arrives while the leader is still long (position open, close order in flight). | **NON-REGRESSION REQUIRED** |

**Non-regression contract**: The fix must pass the DW-B128 regression test (Test 2 below). During the race window the leader's position is open (`hasPos=True`), so the `if (!hasPos)` path is NOT taken and the direction key is NOT cleared. DW-B128 protection is provably preserved.

---

## T-02 — Files to Edit

| File | Action | Description |
|------|--------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | **EDIT** | (a) Add 8-line direction-clear block inside `TryFirePositionState` on `hasPos=False` path. (b) Append 3 thin internal test accessor properties after `TryFirePositionState`. |
| `src/PropTraderTools/Tests/B129Tests.cs` | **APPEND** | Add 3 new `[Fact]` methods to existing `B129Tests` class. Do NOT overwrite or remove existing tests. |

**Files NOT to touch**: `TradeCopierWindow.cs`, `TradeCopierPanel.cs`, `PttCopier.cs`, `CopyEngineTests.cs`, `B76Tests.cs`, any other `.cs` file.

---

## T-03 — Method Signatures and Insertion Points

### 3.1 — Modify `TryFirePositionState` in `CopyEngine.cs`

**Method**: `private void TryFirePositionState(OrderEventArgs e)`
**Confirmed location**: approximately L2361–L2387 (`CopyEngine.cs`)

**Insertion location**: Immediately after the Interlocked CAS guard block ending in:

```csharp
if (prior == newVal) return;
```

(This is currently approximately L2383.) The new block is inserted BEFORE the next existing statement (currently `bool hasEntries = ...` or `PositionStateChanged?.Invoke(...)`, whichever follows the CAS guard in the code at the time of editing).

**Exact code block to insert** (copy verbatim — ASCII-only, no Unicode):

```csharp
// DW-B135: clear direction key when leader position goes flat.
// Prevents false-positive IsReversalToFlatFollower on next entry after clean close.
// DW-B128 preserved: during race window, hasPos=True, so this path not taken.
// JS-021: TryRemove is lock-free. JS-001: no throw. CYC: 3->6 (three new branches).
if (!hasPos)
{
    bool isLeaderAcct = false;
    foreach (var r in _rules)
    {
        if (e.Order.Account.Name == r.MasterAccount?.Name)
        {
            isLeaderAcct = true;
            break;
        }
    }
    if (isLeaderAcct)
        _lastLeaderDirection.TryRemove(instr, out _);
}
```

**Variable note**: `instr` refers to `e.Order.Instrument.FullName` — the same local variable already computed earlier in `TryFirePositionState` (confirmed at plan Section A, item A5 / code read of L2368–2372). Engineer must confirm `instr` is in scope at the insertion point; if the variable is named differently in the current code, use the correct local name.

### 3.2 — Append Internal Test Accessors after `TryFirePositionState`

Add the following 3 thin internal properties immediately after the closing brace of `TryFirePositionState`. These are shims only — no logic:

```csharp
// DW-B135 test accessors -- no logic, thin shims only.
internal void TryFirePositionState_ForTest(OrderEventArgs e) => TryFirePositionState(e);
internal bool HasLeaderDirection(string instrFullName) => _lastLeaderDirection.ContainsKey(instrFullName);
internal void SetLeaderDirection_ForTest(string instrFullName, OrderAction action) =>
    _lastLeaderDirection[instrFullName] = action;
```

**`[assembly: InternalsVisibleTo]` pre-check**: Confirm the attribute is already present at approximately L46 of `CopyEngine.cs`:

```csharp
[assembly: InternalsVisibleTo("PropTraderTools.Tests")]
```

If it is absent, add it. If it is already present, do not duplicate it.

---

## T-04 — xUnit Tests (APPEND to `B129Tests.cs`)

**Test class**: `B129Tests` (existing class — APPEND only, do NOT rewrite)
**Framework**: xUnit `[Fact]` — NO NUnit, NO MSTest

Append all 3 test methods inside the existing `B129Tests` class body, after the last existing `[Fact]` method.

---

### Test 1 — `B129_DW135_GuardClearedAfterLeaderFlat`

**Purpose**: Confirms that calling `TryFirePositionState_ForTest` with a Filled order on the leader account, when the leader position is flat, removes the direction key from `_lastLeaderDirection`.

**Setup**:
1. Create `CopyEngine` instance (or subclass with `HasOpenPosition` overridden — see note below).
2. Wire one rule: `MasterAccount.Name = "Sim101"`, follower account, instrument `"ES 09-26"`.
3. Call `engine.SetLeaderDirection_ForTest("ES 09-26", OrderAction.Buy)` to simulate a prior Buy dispatch.
4. Assert pre-condition: `Assert.True(engine.HasLeaderDirection("ES 09-26"))`.
5. Construct `OrderEventArgs` with:
   - `e.Order.Account.Name = "Sim101"` (leader)
   - `e.Order.Instrument.FullName = "ES 09-26"`
   - `e.OrderState = OrderState.Filled`
   - Position stub: `HasOpenPosition(Sim101, ES 09-26)` returns `false` (flat)
6. Call `engine.TryFirePositionState_ForTest(e)`.

**Asserts**:

```csharp
// Primary: direction key removed after flat event.
Assert.False(engine.HasLeaderDirection("ES 09-26"));

// Secondary: TryGetValue confirms key absent (hasLastDirection=false in next DispatchCopy).
Assert.False(engine.TestOnly_LastLeaderDirection.TryGetValue("ES 09-26", out _));
```

**`HasOpenPosition` mock note**: `HasOpenPosition` is private. The engineer must use one of:
- (a) A `protected virtual` extraction of `HasOpenPosition` + test subclass override (recommended by architect — see plan Section G).
- (b) An existing test-seam pattern already present in `CopyEngine.cs` for NT8 position isolation.

The accessor `engine.TestOnly_LastLeaderDirection` exposes `_lastLeaderDirection` directly and may be added alongside the other test shims:

```csharp
internal ConcurrentDictionary<string, OrderAction> TestOnly_LastLeaderDirection
    => _lastLeaderDirection;
```

---

### Test 2 — `B129_DW135_DW128ProtectionPreservedDuringRaceWindow`

**Purpose**: Confirms that `IsReversalToFlatFollower` still returns `true` for the DW-B128 race window scenario (direction key set, new opposite action, follower flat). Pure static predicate test — no engine wiring required.

**Code under test**: `CopyEngine.IsReversalToFlatFollower` (already `internal static`, directly callable).

**Test body**:

```csharp
[Fact]
public void B129_DW135_DW128ProtectionPreservedDuringRaceWindow()
{
    // DW-B128 race window: direction=Buy, new Sell arrives, follower flat.
    // Guard MUST fire (return true) -- correct block, not a false positive.
    Assert.True(
        CopyEngine.IsReversalToFlatFollower(
            OrderAction.Sell,
            OrderAction.Buy,
            followerIsFlat: true));
}
```

**No setup required.** This test is a pure static assertion on the existing predicate method.

---

### Test 3 — `B129_DW135_FirstEntryAfterRestartNotBlocked`

**Purpose**: Confirms that a freshly constructed `CopyEngine` has no direction key pre-populated. `HasLeaderDirection` must return `false` for any instrument. Regression anchor — catches any future code that accidentally pre-populates `_lastLeaderDirection` at construction.

**Test body**:

```csharp
[Fact]
public void B129_DW135_FirstEntryAfterRestartNotBlocked()
{
    var engine = new CopyEngine(/* minimal constructor args */);

    // No prior direction exists on fresh engine.
    Assert.False(engine.HasLeaderDirection("ES 09-26"));
    // hasLastDirection=false => IsReversalToFlatFollower never evaluated => no block.
}
```

**No `SetLeaderDirection_ForTest` call.** Test asserts on a fresh instance only.

---

## T-05 — 7-Scan Checklist

The engineer MUST run all 7 scans and achieve all pass criteria before reporting `BUILD_PASS`. No exceptions.

---

### SCAN-01 — No new `lock()` (JS-021)

```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```

**Scope**: New and modified code in `TryFirePositionState` and the 3 test accessors.
**Pass criterion**: Zero `lock(` occurrences in the new/modified code. Pre-existing `lock(` in non-PTT code (if any) are out of scope but must not be in lines added by this ticket.
**Expected result**: 0 new hits.

---

### SCAN-02 — No new `async void` (JS-033)

```powershell
grep -n "async void " src/PropTraderTools/CopyEngine.cs
```

**Scope**: New code added by this ticket.
**Pass criterion**: Zero `async void` in any line added or modified by this ticket.
`TryFirePositionState` is a synchronous `void` method. The 3 test accessors are synchronous. No new async methods introduced.
**Expected result**: 0 new hits.

---

### SCAN-03 — No new `return null` (JS-002)

```powershell
grep -n "return null;" src/PropTraderTools/CopyEngine.cs
```

**Scope**: New code added by this ticket.
**Pass criterion**: Zero `return null;` in any line added by this ticket.
`TryFirePositionState` returns `void`. The 3 test accessors return `void`, `bool`, and `void` respectively. No nullable return paths.
**Expected result**: 0 new hits.

---

### SCAN-04 — No new `throw new` (JS-001)

```powershell
grep -n "throw new " src/PropTraderTools/CopyEngine.cs
```

**Scope**: New code added by this ticket.
**Pass criterion**: Zero `throw new` in any line added by this ticket. All error paths use early-return or ConcurrentDictionary lock-free ops — no exceptions thrown.
**Expected result**: 0 new hits.

---

### SCAN-05 — `_lastLeaderDirection` reference count

```powershell
grep -n "_lastLeaderDirection" src/PropTraderTools/CopyEngine.cs
```

**Pre-ticket baseline** (confirmed from plan Section A and plan item A5):
- L331: field declaration
- L1914: `TryGetValue` read in `DispatchCopy`
- L1985: write (`[instr.FullName] = currentAction`) in `DispatchCopy`

**Total before ticket**: 3 references.

**After ticket**: The new `TryRemove` call adds 1 reference in `TryFirePositionState`. The `HasLeaderDirection` and `SetLeaderDirection_ForTest` test accessors add 2 more references. The `TestOnly_LastLeaderDirection` accessor (if added) adds 1 more.

**Pass criterion**: At minimum 4 total references (baseline 3 + 1 new `TryRemove`). With all 3 test accessors + `TestOnly_LastLeaderDirection` accessor: up to 7 total references. Engineer confirms exact count matches lines added.

---

### SCAN-06 — No overlap with LaneB range

**Manual check**: Confirm the edited method (`TryFirePositionState`) starts at or after L2361 in the current file. LaneB scope ended at approximately L2159 (`SyncAtmFollowerBracket`). There must be no line overlap between the LaneA insertion (L2361+) and the LaneB range (up to ~L2159).

**Pass criterion**: First line of `TryFirePositionState` >= L2300 (at minimum 140 lines below LaneB range). Engineer confirms by checking actual line numbers in the current file state after LaneB edits are committed.

---

### SCAN-07 — Build and test gate

```powershell
dotnet build src/PropTraderTools --no-incremental
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

```powershell
dotnet test src/PropTraderTools --filter "FullyQualifiedName~B129" --no-build
```

Expected: All B129 tests pass.
- 3 pre-existing LaneB tests (from `LaneB-ticket-2-completion.md`): PASS
- 3 new LaneA tests (T-1, T-2, T-3 from this ticket): PASS
- **Total**: 6 B129 tests, 0 failures, 0 skipped.

**Pass criterion**: `0 Error(s)`, `0 Warning(s)`, 6/6 B129 tests green.

---

## T-06 — CYC Constraint

**Method**: `TryFirePositionState`

**Pre-fix branch count** (plan Section D, confirmed REVIEW_PASS):

| # | Decision Point | Code Location |
|---|---------------|---------------|
| 1 | State filter | `if (state != Filled && state != PartFilled)` |
| 2 | Null guard | `if (e.Order?.Instrument?.FullName == null)` |
| 3 | Interlocked CAS | `if (prior == newVal)` |

**CYC BEFORE = 3**

**Post-fix new decision points added by this ticket**:

| # | Decision Point | New Code |
|---|---------------|----------|
| 4 | hasPos guard | `if (!hasPos)` — gates the entire direction-clear block |
| 5 | foreach loop | `foreach (var r in _rules)` — loop continuation condition is a branch |
| 6 | leader account check | `if (e.Order.Account.Name == r.MasterAccount?.Name)` |

**CYC AFTER = 6**

Counting convention: `if (!hasPos)` = branch 4 (explicit guard), `foreach` body continuation = branch 5, `if (isLeaderAcct == r.MasterAccount?.Name)` = branch 6. Total = 3 + 3 = **6**.

**JS-080 compliance**: CYC = 6 ≤ 8. **COMPLIANT. No extraction required.**

---

## T-07 — ASCII-Only Constraint

All new string literals and comment text in the code block inserted by this ticket must be ASCII-only (no Unicode, no emoji, no curly/smart quotes).

**Inserted comment text** (verified character-by-character):

| Comment Fragment | ASCII-only? |
|-----------------|-------------|
| `// DW-B135: clear direction key when leader position goes flat.` | YES |
| `// Prevents false-positive IsReversalToFlatFollower on next entry after clean close.` | YES |
| `// DW-B128 preserved: during race window, hasPos=True, so this path not taken.` | YES |
| `// JS-021: TryRemove is lock-free. JS-001: no throw. CYC: 3->6 (three new branches).` | YES |
| `// DW-B135 test accessors -- no logic, thin shims only.` | YES |

**No string literals** in the inserted production code (all identifiers are C# expressions, not string values).

**Test method names** (identifiers, not string literals): `B129_DW135_GuardClearedAfterLeaderFlat`, `B129_DW135_DW128ProtectionPreservedDuringRaceWindow`, `B129_DW135_FirstEntryAfterRestartNotBlocked` — all ASCII.

**ASCII-only constraint: CONFIRMED PASS**

---

## Engineer Completion Checklist

Before reporting `BUILD_PASS`, confirm each item:

- [ ] SCAN-01 PASS: 0 new `lock(` in added code
- [ ] SCAN-02 PASS: 0 new `async void` in added code
- [ ] SCAN-03 PASS: 0 new `return null;` in added code
- [ ] SCAN-04 PASS: 0 new `throw new` in added code
- [ ] SCAN-05 PASS: `_lastLeaderDirection` reference count matches lines added (minimum 4 total)
- [ ] SCAN-06 PASS: `TryFirePositionState` starts ≥ L2300, no overlap with LaneB range
- [ ] SCAN-07 PASS: Build `0 Error(s) 0 Warning(s)`, 6/6 B129 tests green
- [ ] T-06 PASS: Final CYC of `TryFirePositionState` = 6 (stated in completion report)
- [ ] T-07 PASS: All inserted text is ASCII-only (no Unicode)
- [ ] `InternalsVisibleTo("PropTraderTools.Tests")` confirmed present (do not duplicate)
- [ ] 3 test accessor shims added to `CopyEngine.cs` (no logic, thin only)
- [ ] `TryFirePositionState_ForTest`, `HasLeaderDirection`, `SetLeaderDirection_ForTest` all callable from test class
- [ ] Existing B129 LaneB tests still pass (6 total, not 3)
- [ ] `ptt-sync-and-verify.ps1` executed after edit (`0 MISMATCH` lines)
- [ ] F5 in NinjaTrader 8 after sync — green compile before reporting done
