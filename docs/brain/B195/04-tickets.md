# B195 Tickets

**Epic**: B195 -- Clone Mode ATM-Aware Drain Dispatch (DW-B195-01)
**Author**: ptt-architect (Phase 3)
**Date**: 2026-10-11
**Plan source**: docs/brain/B195/02-architecture-plan.md (REVIEW_PASS)
**Review source**: docs/brain/B195/02-plan-review.md (REVIEW_PASS, 46/46 checks)
**Total tickets**: 1 (T2 dropped -- OnAtrBoxPreviewKeyDown not found in source, DW-B195-03 opened)

---

## Ticket 1 -- DW-B195-01: Clone Mode SubmitDrainedEntry Must Call SendCopyWithAtm

### Spec Requirements

- **DW-B195-01** (primary): Clone Mode SubmitDrainedEntry Not ATM-Aware. Opened in docs/brain/B194/06-deferred-backlog.md. Closed by this ticket.
- **B167-DEFER-02** (historical): Clone+ATM follower bracket dispatch deferred from B167. This ticket completes the drain-completion path for that deferred work (the regular copy path was fixed in B167; the drain-completion path was not).
- Plan reference: docs/brain/B195/02-architecture-plan.md §2, §3, §4, §5, §7.

---

### Problem Statement

**Root cause (source-confirmed)**: `SubmitDrainedEntry` (CopyEngine.cs line 6718) calls
`SubmitEntryDirect` unconditionally at branch (3) (line 6727). `SubmitEntryDirect` creates a
bare `"PTT-Copy"` order (see line 6671) with no ATM strategy attached. In Clone+ATM mode, the
replacement entry order has no brackets.

**Exact buggy branch** (CopyEngine.cs lines 6727-6734):
```csharp
SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
    follower,
    payload.Instrument,
    payload.Qty,
    payload.Price,
    payload.Action,
    payload.OrderType
);
```

This path is reached when a follower drains its existing entry order(s), all cancel
acknowledgements arrive, and `OnDrainCancelAck` fires `SubmitDrainedEntry`. At that point,
`SendCopyWithAtm` is NOT called, so no `StartAtmStrategy` fires, and the re-entered position
has no native ATM brackets.

**Impact**: After the leader fills, `SyncFollowerBracket` logs `fo=NULL` for all affected
followers (naked position). The PTT-Flatten detector may then fire a reverse entry, creating a
runaway loop.

---

### Implementation Instructions

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method to modify**: `SubmitDrainedEntry` (line 6718)
**Design decision**: Option A -- re-fetch ATM mode at dispatch time via `GetCloneAtmMode()`.
No changes to `PendingDispatchDrain`, `DrainThenDispatch`, or any other method.

#### Step 1 -- Replace the comment block above `SubmitDrainedEntry` (lines 6713-6717)

**Current** (lines 6713-6717):
```csharp
        // DW-NEW-08 Option D: submit the parked entry after all drain cancels acknowledged.
        // CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
        //        (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit).
        // R3-F2: cleanup moved after SubmitEntryDirect -- drain IDs preserved until submit completes.
        // NT8: Account.CreateOrder + Submit via SubmitEntryDirect. NO Account.Change().
```

**Replace with**:
```csharp
        // B195: SubmitDrainedEntry is now ATM-mode-aware.
        // CYC=5: (1) TryRemove fails early return, (2) FollowerAccount null early return,
        //        (3) ATM mode branch -- Named -> SendCopyWithAtm, else -> SubmitEntryDirect,
        //        (4) F3 cleanup foreach (after submit).
        // Option A: GetCloneAtmMode() re-fetched at dispatch time (volatile read, thread-safe, JS-021).
        // JS-001: no throw. JS-033: not async. JS-066: CYC=5 (<=8 ceiling). ASCII-only.
        // NT8: SendCopyWithAtm uses StartAtmStrategy (AddOnBase-available, NT8_FULL_REFERENCE.md line 17).
        // NT8: "Entry" order name constraint satisfied inside SendCopyWithAtm (line 4680).
        // R3-F2: drain IDs cleaned AFTER dispatch -- IDs preserved if dispatch fails.
```

#### Step 2 -- Replace the body of `SubmitDrainedEntry` (lines 6719-6739)

**Current body** (lines 6719-6739):
```csharp
        private void SubmitDrainedEntry(string acctKey)
        {
            if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
                return;

            var follower = payload.FollowerAccount;
            if (follower == null) // (2)
                return;

            SubmitEntryDirect( // (3) submit first -- drain IDs still in dict here
                follower,
                payload.Instrument,
                payload.Qty,
                payload.Price,
                payload.Action,
                payload.OrderType
            );

            // R3-F2: clear drain-owned IDs AFTER submit so IDs are preserved on submit failure.
            foreach (var id in payload.DrainedOrderIds) // (4)
                _drainOwnedOrderIds.TryRemove(id, out _);
        }
```

**Replace with**:
```csharp
        private void SubmitDrainedEntry(string acctKey)
        {
            if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
                return;

            var follower = payload.FollowerAccount;
            if (follower == null) // (2)
                return;

            var atmMode = GetCloneAtmMode(); // (3) re-fetch at dispatch time -- volatile read, thread-safe
            if (atmMode is FollowerAtmMode.Named namedAtm) // (3a) Clone+ATM path
            {
                var signal = CopySignal.Create(
                    payload.Action,
                    payload.OrderType,
                    payload.Qty,
                    payload.Price,
                    payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks
                );
                SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm);
            }
            else // (3b) non-ATM path (Inherit or no Clone ATM configured)
            {
                SubmitEntryDirect(
                    follower,
                    payload.Instrument,
                    payload.Qty,
                    payload.Price,
                    payload.Action,
                    payload.OrderType
                );
            }

            // R3-F2: clear drain-owned IDs AFTER dispatch so IDs are preserved if dispatch fails.
            foreach (var id in payload.DrainedOrderIds) // (4)
                _drainOwnedOrderIds.TryRemove(id, out _);
        }
```

**Key implementation notes**:
- `payload.FollowerAccount` (line 6793, type `Account`) -- used as first arg to `SendCopyWithAtm`.
- `payload.FollowerAcctKey` (line 6783, type `string`) -- used in orderId uniqueness suffix.
- `payload.TimestampTicks` (line 6795, type `long`) -- numeric, guaranteed ASCII when string-converted.
- `CopySignal.Create` parameter order (lines 605-611): `(OrderAction action, OrderType type, int qty, double limitPrice, string orderId)`.
- `SendCopyWithAtm` return value (`bool`) is intentionally discarded -- consistent with existing codebase pattern (StatusUpdate raised internally on both success and failure paths).
- The `foreach` drain-ID cleanup executes regardless of which dispatch path was taken (Named or non-ATM). This is correct: drain IDs must be removed whether submission succeeds or fails to prevent permanent leaks.

#### Step 3 -- Create test file src/PropTraderTools/Tests/B195Tests.cs

Create a new file with the 4 xUnit [Fact] test cases described in §7. See "xUnit Test Names" section below for the required test method names and what each must assert.

---

### Method Signatures

| Method | File | Lines | Signature | Modified? |
|--------|------|-------|-----------|-----------|
| `SubmitDrainedEntry` | CopyEngine.cs | 6718 (pre), 6718 (post) | `private void SubmitDrainedEntry(string acctKey)` | YES -- body modified |
| `GetCloneAtmMode` | CopyEngine.cs | 745 | `internal FollowerAtmMode GetCloneAtmMode()` | NO -- called only |
| `SendCopyWithAtm` | CopyEngine.cs | 4661 | `private bool SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode.Named namedMode)` | NO -- called only |
| `CopySignal.Create` | CopyEngine.cs | 605 | `internal static CopySignal Create(OrderAction action, OrderType type, int qty, double limitPrice, string orderId)` | NO -- called only |
| `SubmitEntryDirect` | CopyEngine.cs | 6650 | (no signature change) | NO -- still called in non-ATM else branch |
| `PendingDispatchDrain` (class) | CopyEngine.cs | 6781 | `private sealed class PendingDispatchDrain` -- 10 fields, 10-param constructor | NO -- no field or constructor change |
| `DrainThenDispatch` | CopyEngine.cs | 6579 | (no signature change) | NO -- not touched |

---

### CYC Impact Table

| Method | File | Pre-CYC | Post-CYC | Limit | Status |
|--------|------|---------|----------|-------|--------|
| `SubmitDrainedEntry` | CopyEngine.cs | 4 | 5 | 8 | PASS (+1 branch: `if atmMode is Named`) |
| `DrainThenDispatch` | CopyEngine.cs | 4 | 4 | 8 | PASS (unchanged) |
| `PendingDispatchDrain` constructor | CopyEngine.cs | 1 | 1 | 8 | PASS (unchanged) |
| `GetCloneAtmMode` | CopyEngine.cs | 2 | 2 | 8 | PASS (called, unchanged) |
| `SendCopyWithAtm` | CopyEngine.cs | 4 | 4 | 8 | PASS (called, unchanged) |

CYC counting convention (per codebase): each `if`, `foreach`, `while`, `case`, `&&`, `||` = 1 branch point. The new `if (atmMode is FollowerAtmMode.Named namedAtm)` adds exactly 1 branch. CYC 4 + 1 = 5.

---

### 7-Scan Checklist (MANDATORY -- engineer contract)

The engineer MUST run all 7 scans to zero before reporting BUILD_PASS.
All scans target `src/PropTraderTools/CopyEngine.cs`.

**SCAN-01 -- lock() ban (JS-021)**
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```
Expected: zero matches in new or modified code lines. `GetCloneAtmMode()` uses a `volatile` field read -- no lock required. Existing `lock(` occurrences (if any) in other methods are pre-existing and out of scope; report any that appear in the modified region (lines 6713-6739).

**SCAN-02 -- async void ban (JS-033)**
```powershell
grep -n "async void " src/PropTraderTools/CopyEngine.cs
```
Expected: zero new matches. `SubmitDrainedEntry` is `private void` (not async). No new async methods introduced by B195.

**SCAN-03 -- throw new Exception ban (JS-001)**
```powershell
grep -n "throw new " src/PropTraderTools/CopyEngine.cs
```
Expected: zero new `throw new` in the modified lines 6713-6739. B195 uses early returns throughout; `SendCopyWithAtm` has its own internal try/catch at line 4668. No new throws introduced.

**SCAN-04 -- CYC <= 8 (JS-066)**
Manual branch count for `SubmitDrainedEntry` after change:
1. `if (!_pendingDispatchDrains.TryRemove(...))` -- branch 1
2. `if (follower == null)` -- branch 2
3. `if (atmMode is FollowerAtmMode.Named namedAtm)` -- branch 3
4. `foreach (var id in payload.DrainedOrderIds)` -- branch 4
Total: CYC = 4 (base) + 1 (new branch) = 5. CYC=5 <= 8. PASS.
Update the CYC comment block (lines 6713-6717) to read "CYC=5" as specified in Step 1.

**SCAN-05 -- ASCII-only**
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
Expected: zero matches in new code. New identifiers: `atmMode`, `namedAtm`, `signal`. New string literal: `"-drain-atm-"`. All are pure ASCII. `payload.TimestampTicks` is a `long`; its `.ToString()` implicit in string concatenation produces digits only.

**SCAN-06 -- DateTime.Now ban**
```powershell
grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs
```
Expected: zero matches in new code. B195 introduces no `DateTime` usage. `TimestampTicks` is an existing `long` field set at drain construction via `Environment.TickCount` (line 6613) -- not `DateTime.Now`.

**SCAN-07 -- return null ban in Option<T> context (JS-002)**
`SubmitDrainedEntry` returns `void`. Not applicable. Verify that:
- `GetCloneAtmMode()` (called but not modified) already returns `FollowerAtmMode.Inherit()` as its non-Named fallback (line 753) -- never null. No change required.
- New code does not introduce any `return null` on option-wrapped or reference-typed returns.
Expected: N/A (void return method). Confirm `GetCloneAtmMode()` unchanged at lines 745-754.

---

### xUnit Test Names

**Test file**: `src/PropTraderTools/Tests/B195Tests.cs`
**Class**: `B195Tests`
**Framework**: xUnit ([Fact] attribute only -- no NUnit, no MSTest)

The engineer must implement all 4 [Fact] methods. Each test must be independent (no shared mutable state between tests).

---

#### [Fact] T1_SubmitDrainedEntry_CloneAtmMode_CallsSendCopyWithAtm

**Spec**: When Clone+ATM mode is active (`_cloneAtmCache` has a template name), `SubmitDrainedEntry` routes to the `SendCopyWithAtm` path -- NOT `SubmitEntryDirect`.

**Setup**:
1. Construct a `CopyEngine` test instance (use existing test-seam constructor if available, or reflection to set private fields).
2. Set ATM cache: call `SetCloneAtmCache("TestTemplate")` (or equivalent internal method that writes `_cloneAtmCache`).
3. Insert a `PendingDispatchDrain` payload into `_pendingDispatchDrains` for key `"Sim102"`. All fields must be non-null: `FollowerAccount` must be a valid mock/stub `Account`.
4. Call `SubmitDrainedEntry("Sim102")`.

**Assert**:
- `GetCloneAtmMode()` returns `FollowerAtmMode.Named` (not `Inherit`).
- An `"Entry"`-named order is created (via interceptable `Account.CreateOrder` or mock), NOT a `"PTT-Copy"`-named order.
- No `SubmitEntryDirect` side effect occurs (no `"PTT-Copy"` order submitted).

---

#### [Fact] T1_SubmitDrainedEntry_NoAtmMode_CallsSubmitEntryDirect

**Spec**: When no ATM template is set (Clone mode with no ATM configured, or non-Clone mode), `SubmitDrainedEntry` routes to `SubmitEntryDirect` -- the bare `"PTT-Copy"` path.

**Setup**:
1. Construct a `CopyEngine` test instance.
2. Do NOT set any ATM cache (`_cloneAtmCache` remains null/empty).
3. Insert a `PendingDispatchDrain` payload for key `"Sim102"` with a valid `FollowerAccount`.
4. Call `SubmitDrainedEntry("Sim102")`.

**Assert**:
- `GetCloneAtmMode()` returns `FollowerAtmMode.Inherit`.
- `Account.CreateOrder` is called with a name matching `"PTT-Copy"` prefix (not `"Entry"`).
- No `StartAtmStrategy` call occurs.

---

#### [Fact] T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesNamedMode

**Spec**: When `_cloneAtmObject` is null but `_cloneAtmCache` has a non-empty template name string, `GetCloneAtmMode()` returns `FollowerAtmMode.Named` (string overload) and `SubmitDrainedEntry` routes to `SendCopyWithAtm`.

**Setup**:
1. Construct a `CopyEngine` test instance.
2. Set `_cloneAtmCache = "MyTemplate"`. Do NOT set `_cloneAtmObject` (leave null).
3. Insert a `PendingDispatchDrain` payload for `"Sim102"`.
4. Call `SubmitDrainedEntry("Sim102")`.

**Assert**:
- `GetCloneAtmMode()` returns a `FollowerAtmMode.Named` instance where `AtmObject == null` and `TemplateName == "MyTemplate"`.
- The `SendCopyWithAtm` (string-overload ATM path) executes -- not `SubmitEntryDirect`.

---

#### [Fact] T1_SubmitDrainedEntry_MissingPayload_ReturnsEarly

**Spec**: When `_pendingDispatchDrains` does not contain the given `acctKey`, `SubmitDrainedEntry` returns immediately with no side effects. This is a regression guard for branch (1) -- the existing `TryRemove` early return.

**Setup**:
1. Construct a `CopyEngine` test instance.
2. Do NOT insert any payload into `_pendingDispatchDrains`.
3. Call `SubmitDrainedEntry("Sim102")`.

**Assert**:
- No exception thrown.
- No `Account.CreateOrder` or `StartAtmStrategy` calls.
- Method returns cleanly.

---

### Completion Artifact

The engineer MUST write `docs/brain/B195/ticket-1-completion.md` upon completing this ticket.

**Required sections in ticket-1-completion.md**:
```
## Ticket 1 Completion

### Scan Results
| Scan | Pattern | Match Count in Modified Region | Result |
|------|---------|-------------------------------|--------|
| SCAN-01 lock() | lock( | 0 | PASS |
| SCAN-02 async void | async void | 0 | PASS |
| SCAN-03 throw new | throw new | 0 | PASS |
| SCAN-04 CYC manual count | (list branches 1-4) | CYC=5 | PASS |
| SCAN-05 ASCII-only | [^\x00-\x7F] | 0 | PASS |
| SCAN-06 DateTime.Now | DateTime\.Now | 0 | PASS |
| SCAN-07 return null | return null | 0 | PASS |

### CYC Verification
Method: SubmitDrainedEntry
Pre-B195 CYC: 4 (per CYC comment at line 6714)
Post-B195 CYC: 5 (4 original branches + 1 new `if atmMode is Named`)
CYC comment updated: YES (lines 6713-6717 updated to CYC=5)
Limit: 8
Status: PASS

### Test Results
B195Tests.cs: [paste xUnit output or confirm all 4 [Fact] methods pass]

### BUILD_PASS Declaration
All 7 scans: PASS
CYC post-change: 5 (within limit of 8)
All B195Tests.cs [Fact] methods: PASS
ptt-sync.ps1 executed: YES (hard-link resync completed)
BUILD_PASS: YES
```

---

## Engineer Instructions

1. **Read first**: This ticket is the complete engineer contract. Do not deviate from the method signatures, field names, or test names specified above.

2. **One file modified**: Only `src/PropTraderTools/CopyEngine.cs` is modified. Do not touch `DrainThenDispatch`, `PendingDispatchDrain`, `SendCopyWithAtm`, or any other method.

3. **One file created**: `src/PropTraderTools/Tests/B195Tests.cs` with 4 [Fact] test cases.

4. **After every .cs edit**: Run `powershell -File .\scripts\ptt-sync.ps1` to resync NinjaTrader hard links.

5. **Run all 7 scans** (SCAN-01 through SCAN-07) before reporting BUILD_PASS. Paste scan results into `docs/brain/B195/ticket-1-completion.md`.

6. **Do not open DW-B195-02**: The `DrainThenDispatch` fast-path (DW-B195-02) is a SEPARATE deferred item targeting B196. Do not fix it in this ticket. Do not modify `DrainThenDispatch`.

7. **Do not add return-value handling for SendCopyWithAtm**: The `bool` return value is intentionally discarded -- consistent with the existing codebase pattern at lines 2605, 2716, 2833. Do not add `if (!SendCopyWithAtm(...))` branches; that would increase CYC beyond the plan.

---

## BUILD_PASS Gate Criteria

Before reporting BUILD_PASS, the engineer must confirm ALL of the following:

- [ ] SCAN-01 (lock ban): 0 new `lock(` in modified region
- [ ] SCAN-02 (async void ban): 0 new `async void` in modified region
- [ ] SCAN-03 (throw new ban): 0 new `throw new` in modified region
- [ ] SCAN-04 (CYC): `SubmitDrainedEntry` post-change CYC = 5 (manual count verified)
- [ ] SCAN-05 (ASCII-only): 0 non-ASCII characters in new code
- [ ] SCAN-06 (DateTime.Now ban): 0 new `DateTime.Now` references
- [ ] SCAN-07 (return null): not applicable (void method); confirmed `GetCloneAtmMode` unchanged
- [ ] CYC comment updated at lines 6713-6717 to reflect CYC=5
- [ ] All 4 B195Tests.cs [Fact] methods pass
- [ ] `ptt-sync.ps1` executed post-edit
- [ ] `docs/brain/B195/ticket-1-completion.md` written with all required sections

---

*Written by: ptt-architect*
*Epic: B195*
*Phase: 3 (Ticket Generation)*
*Return: TICKETS_COMPLETE*
