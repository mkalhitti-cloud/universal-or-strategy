# PTT-BUGS-LIVE-0911 -- Lane A Tickets
# BUG-1: PTT-Flatten Reverse Entry Guard
# ptt-architect | 2026-09-11 | Phase 3 Output
# Source plan: 02-architecture-plan.md (Revision 6 -- REVIEW_PASS, Cycle 6)

---

## TICKETS_COMPLETE

**Epic:** PTT-BUGS-LIVE-0911  
**Lane:** A -- BUG-1 ONLY  
**Ticket count:** 1  
**Plan status at ticket generation:** REVIEW_PASS (0 violations, Cycle 6)

---

---

# T1 -- PTT-Flatten Reverse Entry Guard (Path B Fan-Out)

## SCOPE LOCK -- TICKET T1 ONLY

This ticket touches **exactly three files**:

| File | Role |
|------|------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY -- add helper + update 2 foreach loops |
| `src/PropTraderTools/CopyEngineTests.cs` | MODIFY -- add 1 new [Fact] |
| `src/PropTraderTools/Features/PttFlatten.cs` | READ ONLY -- zero edits permitted |

**No other file may be touched by this ticket.**  
Any edit outside these three files = immediate TICKET_REVIEW_FAIL.

---

## Spec Requirement IDs

| ID | Source | Description |
|----|--------|-------------|
| BUG-1-ROOT | `02-architecture-plan.md` §1.2 | Path B (PttBus fan-out) has no explicit outer position guard before FlattenOneAccount; relies solely on IsAccountFlattenable inner guard which has an Initialized-state race window |
| BUG-1-FIX | `02-architecture-plan.md` §2.1 | Add explicit position guard at outer fan-out loop level in Flatten(Instrument) and Flatten(Account,Instrument) |
| BUG-1-EVIDENCE-A | `docs/brain/live-trading-analysis-2026-09-11.md` | 5:55 AM event: -05 double dispatch, 14L accidental long |
| BUG-1-EVIDENCE-B | `docs/brain/live-trading-analysis-2026-09-11.md` | 7:14 AM event: -07 and -08 reverse short, -$90 accidental losses |
| BUG-1-TEST | `02-architecture-plan.md` §3.1 | New [Fact] exercises FlatGuardLoop Path B guard without NT8 runtime |

---

## Method Signatures to Implement

### NEW: `FlatGuardLoop<TAccount>` private static helper

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Placement:** Insert as a new `private static` method in the same region as `Flatten(Instrument)` (near line 5154). The exact insertion point is the blank line immediately after the closing brace of `Flatten(Instrument instrument)` at line 5154.

**Signature:**
```csharp
private static void FlatGuardLoop<TAccount>(
    Func<IEnumerable<TAccount>> getAccounts,
    Func<TAccount, NinjaTrader.Cbi.Position?> findPosition,
    Action<TAccount> flattenOne
)
```

**Constraints:**
- `private static` -- zero new public API surface (SCAN-04 requirement)
- One generic parameter `TAccount` only -- `TPosition` is NOT present (V-03 closed)
- `findPosition` return type MUST be `NinjaTrader.Cbi.Position?` (concrete NT8 type) -- this ensures `pos.Quantity` compiles without CS1061
- `TAccount` is unconstrained -- `acc.ToString()` is available on `object`, no constraint needed
- CYC = 3: (1) foreach iteration, (2) `pos == null` branch, (3) `pos.Quantity == 0` branch

**Full implementation body:**
```csharp
// BUG-0911-A: extracted for testability -- mirrors FlattenFollower delegate-injection pattern.
// CYC=3: (1) foreach, (2) pos == null branch, (3) pos.Quantity == 0 branch.
private static void FlatGuardLoop<TAccount>(
    Func<IEnumerable<TAccount>> getAccounts,
    Func<TAccount, NinjaTrader.Cbi.Position?> findPosition,
    Action<TAccount> flattenOne
)
{
    foreach (var acc in getAccounts())
    {
        var pos = findPosition(acc);
        if (pos == null || pos.Quantity == 0)
        {
            NinjaTrader.Code.Output.Process(
                "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
            continue;
        }
        flattenOne(acc);
    }
}
```

**JS Rule constraints on this method:**
- JS-021: No `lock()` -- method is `static`, all state is stack-local. PASS.
- JS-001: No `throw` -- violations log and continue. PASS.
- JS-002: No return of `null` -- method returns `void`. PASS.
- JS-010 (ASCII): Log string `"[FLAT-GUARD] skip flat, already flat on "` uses only `[ ] - A-Z a-z 0-9 space`. PASS.
- JS-025 (DateTime): No `DateTime.Now` usage. No datetime involved. PASS.

---

### MODIFY: `Flatten(Instrument instrument)` at `src/PropTraderTools/CopyEngine.cs` line 5145

**Current code at lines 5152-5153:**
```csharp
foreach (var acc in AllAccounts(instrument))
    FlattenOneAccount(acc, instrument);
```

**Replace with:**
```csharp
// BUG-0911-A: FLAT-GUARD added -- skip flat followers before FlattenOneAccount. CYC: 3->4.
FlatGuardLoop(
    () => AllAccounts(instrument),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```

**Full method after edit (for reference -- do not alter the surrounding guard logic):**
```csharp
internal void Flatten(Instrument instrument)
{
    if (!_flags.TrimFlatten)
    {
        StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
        return;
    }
    // BUG-0911-A: FLAT-GUARD added -- skip flat followers before FlattenOneAccount. CYC: 3->4.
    FlatGuardLoop(
        () => AllAccounts(instrument),
        acc => FindPosition(acc, instrument),
        acc => FlattenOneAccount(acc, instrument)
    );
}
```

**CYC impact:** 3 → 4 (the foreach is replaced by a single helper call; CYC of FlatGuardLoop is counted separately as CYC=3). Both within limit.

**JS Rule constraints:**
- JS-021: No `lock()`. PASS.
- JS-001: No `throw`. PASS.

---

### MODIFY: `Flatten(Account leader, Instrument instrument)` at `src/PropTraderTools/CopyEngine.cs` line 5181

**Current code at lines 5194-5199 (follower loop):**
```csharp
foreach (var acc in AllAccounts(instrument))
{
    if (acc == leader)
        continue;
    FlattenOneAccount(acc, instrument);
}
```

**Replace with:**
```csharp
// BUG-0911-A: FLAT-GUARD added in follower loop -- pos null/qty guard before FlattenOneAccount. CYC: 5->6.
FlatGuardLoop(
    () => AllAccounts(instrument).Where(acc => acc != leader),
    acc => FindPosition(acc, instrument),
    acc => FlattenOneAccount(acc, instrument)
);
```

**Full method after edit (for reference -- the leader-null guard and leader-direct-call above the loop are unchanged):**
```csharp
// B28 T1 -- Flatten(Account,Instrument): leader-account overload. Fixes DW-B28-02.
// CYC=4: (1) leader null guard, (2) leader direct call, (3) foreach, (4) acc==leader skip. BGTM-1: TrimFlatten gate CYC=5.
// BUG-0911-A: FLAT-GUARD added in follower loop -- pos null/qty guard before FlattenOneAccount. CYC: 5->6.
internal void Flatten(Account leader, Instrument instrument)
{
    if (!_flags.TrimFlatten)
    {
        StatusUpdate?.Invoke("Trim/Flatten requires Pro tier");
        return;
    }
    if (leader == null)
    {
        StatusUpdate?.Invoke("PTT-Flatten: leader null -- skipping");
        return;
    }
    FlattenOneAccount(leader, instrument);
    // BUG-0911-A: FLAT-GUARD added in follower loop. CYC: 5->6.
    FlatGuardLoop(
        () => AllAccounts(instrument).Where(acc => acc != leader),
        acc => FindPosition(acc, instrument),
        acc => FlattenOneAccount(acc, instrument)
    );
}
```

**CYC impact:** 5 → 6 (follower foreach replaced by helper call + `Where` predicate). Within limit.

**JS Rule constraints:**
- JS-021: No `lock()`. PASS.
- JS-001: No `throw`. PASS.

---

## Log Message Specification

The log message emitted by `FlatGuardLoop` when a flat account is skipped:

```
[FLAT-GUARD] skip flat, already flat on {acc.ToString()}
```

**NT8 call form (exact):**
```csharp
NinjaTrader.Code.Output.Process(
    "[FLAT-GUARD] skip flat, already flat on " + acc.ToString(),
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**Critical constraint:** Use `acc.ToString()`, NOT `acc.Name`.  
- `TAccount` is unconstrained generic -- `.Name` is not guaranteed to exist on `TAccount` (CS1061 risk).  
- `.ToString()` is available on `object` for any `TAccount`. No compilation error.  
- This was the V-04 violation in the plan review cycle. Any use of `acc.Name` here = TICKET_REVIEW_FAIL.

**ASCII compliance:** `[ ] - A-Z a-z 0-9 space` only. No Unicode. No emoji. No curly quotes. SCAN-02 PASS.

---

## New [Fact] Test Specification

**File:** `src/PropTraderTools/CopyEngineTests.cs`  
**Test name:** `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard`  
**Annotation:** `[Fact]` -- NO `Skip` annotation required.

**Rationale for no Skip:** The test invokes `FlatGuardLoop` via reflection with injected delegates. No NT8 runtime objects are instantiated. The `findPosition` delegate returns `null` (no `NinjaTrader.Cbi.Position` object is created). Safe to run in any xUnit test runner without NT8 installed.

**Placement:** Add to the end of the test class body, before the closing `}` of `CopyEngineTests` at line 8339.

**Full test implementation:**
```csharp
// BUG-0911-A: FlatGuardLoop guard test -- Path B fan-out guard.
// Exercises the new pos==null guard added to Flatten(Instrument) follower loop.
// No NT8 runtime required: FlatGuardLoop is invoked via reflection with injected delegates.
[Fact]
public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()
{
    // Arrange
    bool flattenOneCalled = false;

    // Stub: one follower account token (opaque -- not an NT8 runtime Account)
    var followerToken = new object();

    // Injected delegates stand in for NT8 instance-method calls:
    Func<IEnumerable<object>> getAccounts = () => new[] { followerToken };
    Func<object, NinjaTrader.Cbi.Position?> findPosition = acc => null; // flat: no position entry
    Action<object> flattenOne = acc => flattenOneCalled = true;

    // Retrieve the static helper via reflection.
    // FlatGuardLoop has ONE generic parameter (TAccount) -- V-03: TPosition removed.
    var mi = typeof(CopyEngine)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == "FlatGuardLoop" && m.GetGenericArguments().Length == 1);
    Assert.NotNull(mi); // guard: method must exist after BUG-0911-A implementation

    var miConcrete = mi.MakeGenericMethod(typeof(object));

    // Act
    miConcrete.Invoke(null, new object[] { getAccounts, findPosition, flattenOne });

    // Assert
    Assert.False(flattenOneCalled,
        "FlatGuardLoop must not call flattenOne when findPosition returns null (flat follower)");
}
```

**What this test asserts:**
- When `findPosition` returns `null` (account has no position entry -- fully flat), `flattenOne` is NEVER called.
- Directly exercises the new `if (pos == null || pos.Quantity == 0)` guard on **Path B** (PttBus fan-out).
- Does NOT test `FlattenFollower` (Path A -- unmodified, already guarded).
- Does NOT require NT8 runtime -- pure delegate injection + reflection.

**CYC of test method:** 1 (straight-line). PASS.

---

## MANDATORY 7-SCAN CHECKLIST

The engineer MUST run each scan after implementation and paste results into the PR description. All expected results must match before marking T1 complete.

### SCAN-01 -- Lock-Free Verification
```
grep -r "lock(" src/
```
**Expected:** 0 new matches introduced by this ticket.  
**Rationale:** `FlatGuardLoop` is `private static` with no shared state. All variables are stack-local. No `lock()`, `Monitor`, or `Interlocked` introduced.  
**If any new `lock(` is found:** FAIL immediately. Do not merge.

### SCAN-02 -- ASCII-Only String Literals
Verify all string literals added by this ticket contain only ASCII characters:
- `"[FLAT-GUARD] skip flat, already flat on "` -- characters: `[`, `]`, `-`, `A-Z`, `a-z`, `0-9`, space. PASS.
- `"BUG-0911-A: ..."` comments -- ASCII only. PASS.
- Test string `"FlatGuardLoop must not call flattenOne when findPosition returns null (flat follower)"` -- ASCII only. PASS.  
**Expected:** 0 non-ASCII characters in any string literal added by T1.

### SCAN-03 -- Cyclomatic Complexity Per Method
Verify CYC for each touched method (McCabe, count branches: if/foreach/while/case/&&/||/? ):

| Method | Before | After | Limit | Result |
|--------|--------|-------|-------|--------|
| `Flatten(Instrument)` | 3 | 4 | 8 | PASS |
| `Flatten(Account, Instrument)` | 5 | 6 | 8 | PASS |
| `FlatGuardLoop<TAccount>` | 0 (new) | 3 | 8 | PASS |

Branch count for `FlatGuardLoop`: (1) `foreach` loop, (2) `pos == null` (left of `||`), (3) `pos.Quantity == 0` (right of `||`). CYC = 3.  
**Expected:** All three methods ≤ 8.

### SCAN-04 -- No New Public API Surface
```
grep -n "public " src/PropTraderTools/CopyEngine.cs
```
Verify `FlatGuardLoop` does NOT appear in the output.  
**Expected:** `FlatGuardLoop` is `private static`. No new `public` method, property, field, or event introduced.

### SCAN-05 -- No Null-Conditional Event Unsubscription
```
grep -rn "\?\." src/PropTraderTools/CopyEngine.cs
```
Verify no new `?.Event -=` pattern is introduced by T1.  
**Expected:** 0 new `?.Event -=` patterns. Event subscriptions are untouched.

### SCAN-06 -- New [Fact] Covers Path B Guard
Verify the new test is present and targets `FlatGuardLoop`:
```
grep -n "Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard" src/PropTraderTools/CopyEngineTests.cs
```
**Expected:** Exactly 1 match (the new test method).  
Verify the test body invokes `FlatGuardLoop` via reflection (`m.Name == "FlatGuardLoop"`).  
Verify the test asserts `Assert.False(flattenOneCalled, ...)`.  
**Expected:** All three checks pass.

### SCAN-07 -- Test Count Delta
Count `[Fact]` annotations before and after:

```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object -ExpandProperty Count
```

| Baseline (pre-T1) | Final (post-T1) | Delta | Expected |
|-------------------|-----------------|-------|----------|
| 168 | 169 | +1 | PASS |

**Expected:** Count increases from 168 to 169. Exactly +1. No other tests added or removed.

---

## Acceptance Criteria

T1 is complete when ALL of the following are true:

1. **Build passes:** `powershell -File .\scripts\build_readiness.ps1` reports no errors or new warnings.
2. **New test passes:** `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard` reports PASS in xUnit runner.
3. **No existing tests regress:** All 168 pre-existing [Fact] tests continue to pass.
4. **SCAN-01 = 0:** No new `lock(` introduced.
5. **SCAN-02 = 0:** No non-ASCII characters in added string literals.
6. **SCAN-03 = PASS:** All three methods ≤ CYC 8.
7. **SCAN-04 = 0:** No new public API surface (`FlatGuardLoop` is `private static`).
8. **SCAN-05 = 0:** No new `?.Event -=` pattern.
9. **SCAN-06 = PASS:** New [Fact] present, targets `FlatGuardLoop` via reflection, asserts `flattenOne` NOT called on flat follower.
10. **SCAN-07 = 168 → 169:** Exactly +1 [Fact].
11. **Hard-link sync:** `powershell -File .\deploy-sync.ps1` runs without DIFF GUARD failure after all edits.
12. **Behavioral:** In live testing, Path B (`PttBus.FlatFired → RelayFlatten → Flatten(Instrument)`) no longer dispatches `FlattenOneAccount` on an already-flat follower account. `[FLAT-GUARD]` log line appears in NT8 Output Tab 1 when a flat follower is skipped.

---

## Out-of-Scope Declaration

The following are explicitly **NOT** within the scope of T1. Any change touching these items = TICKET_REVIEW_FAIL:

| Item | Reason |
|------|--------|
| `TryDispatchLeaderFlat` (line 4846) | Already guarded via `FlattenFollower` at line 4887. No change needed. |
| `FlattenFollower` (line 4878) | Guard already present at line 4887. Path A is correct. No change. |
| `IsAccountFlattenable` (line 5411) | Inner guard retained as defense-in-depth second layer. Not modified. |
| `HasInflightFlatten` (line 5429) | Initialized-gap race is tracked as DW-BUG-0911-A-01 (deferred). Out of scope. |
| `PttFlatten.Execute` (`Features/PttFlatten.cs` line 47) | READ ONLY. The leader side is correct. No change. |
| `DoFlattenOrder`, `SubmitMarketFlattenOrder`, `CancelAllAccountOrders` | Order dispatch unchanged. Not the bug location. |
| QX paths (`PttGlobalQuickExit`, `CancelQxBrackets`, `TryReplacePttBeBrackets`) | Separate feature. Not involved. |
| BE paths (`MoveStopToBreakEven`, `SubmitBeStop`, `ArmPendingBe`, `TrailBe`) | Not related to PTT-Flatten dispatch. |
| Trim paths (`TrimOneAccount`, `FlattenOneAccountLimit`) | Separate feature. Not involved. |
| `Flatten(Instrument, int, double, double)` (line 5586) | Uses `PTT-FlattenLimit` signal. Not the PTT-Flatten fan-out path. |
| BUG-2 (fo=NULL bracket failure after QX cycle) | Lane B. Separate ticket. Sequential after Lane A merges. |
| Any other file in `src/PropTraderTools/` | Not in scope. |

---

## Deferred Items (Carry-Forward -- Not T1 Work)

These items are tracked but NOT addressed in T1:

| ID | Item | Priority |
|----|------|----------|
| DW-BUG-0911-A-01 | Mid-close race window: guard catches qty==0 definitively but not the in-flight case (close submitted, not yet filled, qty still >0). Full elimination requires atomic interlocked slot. Deferred. | P2 |
| DW-BUG-0911-A-02 | pos.Quantity==0 retained-entry path: currently handled by IsAccountFlattenable as second layer. If future incident confirms this path is hit, upgrade FlatGuardLoop to accept Func<TPosition, bool> isFlat predicate. | P3 |
| DW-B24-01 | NT8-043 formal rule: null-conditional event unsubscription silent crash. SCAN-07=0 in this fix. | P2 |
| DW-B24-02 | Manual E2E Ctrl+Shift+B runtime verification (BreakEven). Must execute before production. | P1 |
| DW-B24-03 | Skip-duplicate guard [Fact] for `if (acc == leader) continue` at CopyEngine.cs:~1195. | P2 |
| DW-B25-01 | Companion field race: `_pendingBeAccount`, `_trailBeAccount` plain singleton refs. Multi-panel race possible. | P3 |
| DW-B26-01 | Reflection test upgrade Option B→A for `TradeCopierPanel_BeBufferBox_FieldDoesNotExist`. | P2 |

---

*ptt-architect | PTT-BUGS-LIVE-0911 Lane A | Phase 3 | 2026-09-11*

**TICKETS_COMPLETE**
