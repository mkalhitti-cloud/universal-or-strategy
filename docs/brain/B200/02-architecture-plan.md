# B200+B201 Architecture Plan

**Epic**: PTT-COPIER-B200 + B201 — Direct Repair Formalisation + Entry-Order Cancel Fix
**Date**: 2026-10-15
**Author**: ptt-architect (Phase 1)
**Status**: DRAFT — awaiting REVIEW_PASS from ptt-plan-reviewer

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

Gate evaluation (per mandatory protocol):

- **Q1**: Are all changes in the same method or within 50 lines? → **NO**
  T2 touch points span ~L982 to ~L1230 (248 lines apart in CopyEngine.cs). T4 spans multiple distant locations.
- **Q2**: Does any fix's design depend on another fix's final design? → **YES — STOP**
  T3 (B201Tests.cs) tests `IsQxCancelCandidate(Order o, bool isFollower)` — a signature T2 produces.
  T3 cannot be finalised until T2's exact method signature is confirmed.

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## Situation Summary

B200 has **four direct repairs** already applied to `CopyEngine.cs` and SIM confirmed. B201 has **one new bug** diagnosed but not yet fixed.

| ID | Root Cause | Code Status |
|----|-----------|------------|
| B200-QTY-01 | `fo.Quantity` → `fo.Quantity + fo.Filled` in HandleEntryChange | CONFIRMED IN CODE (L4643) |
| B200-SFB-NULL-01 | `_followerSlateReady[acc.Name] = true` missing in TryLaneDDispatchFollower | CONFIRMED IN CODE (L2971) |
| B200-NAKED-01 | TryNakedDetect skips "Entry"-named orders on follower accounts | CONFIRMED IN CODE (L7179-7182) |
| B200-CLOSE-LANED-01 | V13 sentinel anchor + PartFilled in HasActivePttFlatten + FlattenOneAccount | CONFIRMED IN CODE (L3058-3061, L5671-5674, L5687) |
| B201-ENTRY-CANCEL-01 | `IsQxCancelCandidate` "Entry" check not scoped to follower accounts | FIX PENDING (T2) |

---

## Component Architecture

### Files Modified by This Epic

| Ticket | File | Change Type |
|--------|------|------------|
| T1 | `src/PropTraderTools/Tests/B200Tests.cs` | NEW — xUnit tests for 4 B200 repairs |
| T2 | `src/PropTraderTools/CopyEngine.cs` | MODIFY — B201 3-touch-point fix |
| T3 | `src/PropTraderTools/Tests/B201Tests.cs` | NEW — xUnit tests for B201 fix |
| T4 | `src/PropTraderTools/CopyEngine.cs` | MODIFY — diagnostic log confirm/cleanup |
| T4 | `docs/brain/B200/06-deferred-backlog.md` | NEW — deferred items register |
| * | `src/PropTraderTools/PropTraderTools.Tests.csproj` | MODIFY — add B200Tests.cs, B201Tests.cs entries |

**Do NOT touch**: `PttBreakEvenSwap.cs`, `PttGlobalQuickExit.cs`, `PttQuickExit.cs`, or any Feature file other than through parameter threading via `BuildQxSnapshot`'s defaulted parameter (see Section 4).

---

## Section 1: B200 Repairs — Code Verification (Read-Only Audit)

### 1.1 B200-QTY-01 — HandleEntryChange Qty Corruption

**Location verified**: `CopyEngine.cs` L4637-4647

```csharp
// B200-QTY-01 FIX: use fo.Quantity + fo.Filled (original order size) not fo.Quantity alone.
// In NT8, Order.Quantity = remaining unfilled qty. After a PartFill fo.Quantity < original.
// fo.Quantity + fo.Filled = original submitted qty = the leader's intent.
DrainThenDispatch(
    acc,
    instrument,
    fo.Quantity + fo.Filled,   // FIX
    newPrice,
    fo.OrderAction,
    fo.OrderType
);
```

**CYC audit**: No new branch. Arithmetic operand change only. CYC delta = 0.
**JS-021**: No lock. No ConcurrentDictionary mutation. PASS.

### 1.2 B200-SFB-NULL-01 — Slate-Gate Miss in TryLaneDDispatchFollower

**Location verified**: `CopyEngine.cs` L2966-2971

```csharp
// B200-SFB-NULL-01 FIX: open slate-ready gate for direct ATM path.
// JS-021: ConcurrentDictionary indexer write is atomic -- no lock needed.
_followerSlateReady[acc.Name] = true;
```

**CYC audit**: Assignment inside existing `if (ok)` block. No new branch. CYC delta = 0.
**JS-021**: `_followerSlateReady` is ConcurrentDictionary — indexer write is lock-free. PASS.

### 1.3 B200-NAKED-01 — TryNakedDetect Fires on Follower Entry Fills

**Location verified**: `CopyEngine.cs` L7173-7182

```csharp
// B200-NAKED-01 FIX: skip NakedDetect on "Entry" fills for follower accounts.
// JS-021: no lock. JS-001: no throw. CYC delta=+1 (new branch).
if (e.Order.Name == "Entry"
    && e.Order.OrderState == OrderState.Filled
    && IsFollowerAccount(e.Order.Account))
    return;
```

**CYC audit**: New `if` guard. Pre-fix CYC was 4; post-fix = 5. Budget ≤8. PASS.
**JS-021**: No lock. Early return pattern. PASS.

### 1.4 B200-CLOSE-LANED-01 — V13 Sentinel + PartFilled Guard

**HasActivePttFlatten — Location verified**: `CopyEngine.cs` L3052-3065

```csharp
// CYC=4: foreach(1) + name check(1) + state check(2).
// B200-CLOSE-LANED-01-V2 FIX: PartFilled included.
if (o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.PartFilled)  // FIX
    return true;
```

**FlattenOneAccount inline guard — Location verified**: `CopyEngine.cs` L5670-5675

```csharp
if (
    o.OrderState == OrderState.Submitted
    || o.OrderState == OrderState.Accepted
    || o.OrderState == OrderState.Working
    || o.OrderState == OrderState.PartFilled  // FIX: mirrors HasActivePttFlatten
)
```

**V13 Sentinel — Location verified**: `CopyEngine.cs` L5687-5688

```csharp
_flattenInFlightOrderId[acc.Name] = acc.Name + "#pending";  // SENTINEL FIRST
_flattenInFlight[acc.Name] = true;                           // FLAG AFTER
```

**CYC audit**:
- `HasActivePttFlatten`: Pre-fix CYC 3 → post-fix 4. Budget ≤8. PASS.
- `FlattenOneAccount`: Pre-fix CYC 6 → post-fix 7 (PartFilled added to existing compound guard; method-level CYC comment L5650 confirms 7). Budget ≤8. PASS.
- V13 sentinel: assignment order change, no new branch. CYC delta = 0.

**JS-021**: `_flattenInFlightOrderId` and `_flattenInFlight` are ConcurrentDictionary — lock-free. PASS.

---

## Section 2: B201-ENTRY-CANCEL-01 — Fix Design (T2)

### 2.1 Root Cause

`IsQxCancelCandidate` at L982 currently:
```csharp
internal static bool IsQxCancelCandidate(Order o)
{
    ...
    if (o.Name == "Entry")
        return true; // (6) DW-B93: Named ATM follower entry Limit
    return false;
}
```

NT8 names ALL ATM pending limit orders `"Entry"` — including the **leader's** add-to-position limit.
`CancelQxBrackets` is called on both leader AND follower accounts. The leader's `"Entry"` order is
incorrectly cancelled by every QX/BE button press.

### 2.2 Fix — Option A: Scope Guard with `isFollower` Parameter

**Touch point 1 — `IsQxCancelCandidate` signature** (~L982):

```csharp
// BEFORE:
internal static bool IsQxCancelCandidate(Order o)

// AFTER:
internal static bool IsQxCancelCandidate(Order o, bool isFollower)
```

**Touch point 2 — `IsQxCancelCandidate` body** (~L994):

```csharp
// BEFORE:
if (o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit

// AFTER:
if (isFollower && o.Name == "Entry")
    return true; // (6) DW-B93: Named ATM follower entry Limit -- scoped to follower only
```

**Touch point 3a — `CancelQxBrackets` 2-arg overload** (~L1007, instance method):

Add at top of method body (after null guard):
```csharp
bool isFollower = IsFollowerAccount(acc);
```

Update call at L1026:
```csharp
// BEFORE:
if (IsQxCancelCandidate(o))
// AFTER:
if (IsQxCancelCandidate(o, isFollower))
```

**Touch point 3b — `CancelQxBrackets` 3-arg overload** (~L1201, instance method):

Add at top of method body (after null guard):
```csharp
bool isFollower = IsFollowerAccount(acc);
```

Update call at L1230:
```csharp
// BEFORE:
if (IsQxCancelCandidate(o))
// AFTER:
if (IsQxCancelCandidate(o, isFollower))
```

**Touch point 3c — `BuildQxSnapshot`** (~L1050):

> **CRITICAL STATIC CONSTRAINT**: `BuildQxSnapshot` is declared `internal static` (L1050). `IsFollowerAccount` is an instance method (L861). A static method cannot call an instance method. The orchestrator's suggested pattern of adding `bool isFollower = IsFollowerAccount(acc)` inside `BuildQxSnapshot` is **NOT applicable** to this method.
>
> **Approved solution**: Add `bool isFollower = false` as a **defaulted parameter**:
> ```csharp
> // BEFORE:
> internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr)
>
> // AFTER:
> internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr, bool isFollower = false)
> ```
>
> - Existing callers (`CopyEngine.BuildQxSnapshot(leader, instr)` in PttQuickExit.cs L92) are backward-compatible via default `isFollower=false`. No Feature file changes.
> - For the leader context, `isFollower=false` is semantically correct — leader "Entry" is NOT cancelled.
> - The `isFollower` parameter is threaded into the `IsQxCancelCandidate(o)` call at L1070.

Update call at L1070:
```csharp
// BEFORE:
if (IsQxCancelCandidate(o))
// AFTER:
if (IsQxCancelCandidate(o, isFollower))
```

### 2.3 Method Signatures (Before/After)

| Method | Before | After |
|--------|--------|-------|
| `IsQxCancelCandidate` | `internal static bool IsQxCancelCandidate(Order o)` | `internal static bool IsQxCancelCandidate(Order o, bool isFollower)` |
| `CancelQxBrackets` 2-arg | `internal void CancelQxBrackets(Account acc, Instrument instr)` | unchanged (no sig change) |
| `CancelQxBrackets` 3-arg | `internal void CancelQxBrackets(Account acc, Instrument instr, HashSet<Order> snapshot)` | unchanged (no sig change) |
| `BuildQxSnapshot` | `internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr)` | `internal static HashSet<Order> BuildQxSnapshot(Account acc, Instrument instr, bool isFollower = false)` |

### 2.4 CYC Impact for T2

| Method | Pre-B201 CYC | Delta | Post-B201 CYC | Budget | Status |
|--------|-------------|-------|--------------|--------|--------|
| `IsQxCancelCandidate` | 7 | +0 | 7 | ≤8 | PASS |
| `CancelQxBrackets` 2-arg | 6 | +0 | 6 | ≤8 | PASS |
| `CancelQxBrackets` 3-arg | 7 | +0 | 7 | ≤8 | PASS |
| `BuildQxSnapshot` | 4 | +0 | 4 | ≤8 | PASS |

**CYC delta = +0 on all methods.** `isFollower` is a local variable assignment (no branch). `&&` extending an existing `if` condition is +0 at method level per project convention (Roslyn McCabe; counted within the same branch).

---

## Section 3: CYC Impact Summary — Full Epic

| Method | File | Ticket | Pre-Epic CYC | Post-Epic CYC | Budget | Status |
|--------|------|--------|-------------|--------------|--------|--------|
| `HandleEntryChange` | CopyEngine.cs | B200-QTY-01 | 6 | 6 (+0) | ≤8 | PASS |
| `TryLaneDDispatchFollower` | CopyEngine.cs | B200-SFB-NULL-01 | 4 | 4 (+0) | ≤8 | PASS |
| `TryNakedDetect` | CopyEngine.cs | B200-NAKED-01 | 4 | 5 (+1) | ≤8 | PASS |
| `HasActivePttFlatten` | CopyEngine.cs | B200-CLOSE-LANED-01 | 3 | 4 (+1) | ≤8 | PASS |
| `FlattenOneAccount` | CopyEngine.cs | B200-CLOSE-LANED-01 | 6 | 7 (+1) | ≤8 | PASS |
| `IsQxCancelCandidate` | CopyEngine.cs | B201-ENTRY-CANCEL-01 | 7 | 7 (+0) | ≤8 | PASS |
| `CancelQxBrackets` 2-arg | CopyEngine.cs | B201-ENTRY-CANCEL-01 | 6 | 6 (+0) | ≤8 | PASS |
| `CancelQxBrackets` 3-arg | CopyEngine.cs | B201-ENTRY-CANCEL-01 | 7 | 7 (+0) | ≤8 | PASS |
| `BuildQxSnapshot` | CopyEngine.cs | B201-ENTRY-CANCEL-01 | 4 | 4 (+0) | ≤8 | PASS |

---

## Section 4: Ticket Designs

### Ticket 1 — B200Tests.cs (T1)

**File**: `src/PropTraderTools/Tests/B200Tests.cs` (NEW)
**Type**: xUnit test file. Zero changes to any `.cs` source file.
**Spec requirement IDs**: B200-QTY-01, B200-SFB-NULL-01, B200-NAKED-01, B200-CLOSE-LANED-01

**Test methods (all `[Fact]`):**

1. `T_B200_01_HandleEntryChange_PartFilledFollowerOrder_UsesOriginalQty`
   - Assert: drain payload qty = `Quantity + Filled` (not `Quantity` alone)
   - Assert: `SendCopyWithAtm` called with `signal.Quantity = 10` when `Quantity=5, Filled=5`
   - Seam: call `DrainThenDispatch` directly or capture via `PendingDispatchDrain` inspector

2. `T_B200_02_LaneDDispatch_SetsSlateReady_AfterAtmSuccess`
   - Arrange: `_followerSlateReady[acc.Name] = false` (post-wipe state)
   - Assert: `_followerSlateReady[acc.Name] == true` after successful `SendCopyWithAtm`
   - Assert: `FindFollowerBracketOrder` does NOT return null for Working bracket after dispatch

3. `T_B200_03_TryNakedDetect_SkipsEntryNamedOrders_OnFollower`
   - Arrange: follower account receives Filled event for order named "Entry"
   - Assert: `TryNakedDetect` returns without firing PTT-Flatten
   - Assert: no flatten submitted when `order.Name == "Entry"`

4. `T_B200_04_FlattenInFlight_SentinelAnchor_BlocksStaleFilledEvent`
   - Arrange: `_flattenInFlightOrderId` set to `acc.Name + "#pending"` (sentinel)
   - Assert: stale Filled event with different OrderId does NOT clear `_flattenInFlight[acc.Name]`
   - Assert: `IsFlattenInFlightOrRecent` returns true while sentinel is set
   - Assert: after real submit, anchor overwritten with real OrderId
   - Assert: Filled event with matching OrderId correctly clears the flag

**PropTraderTools.Tests.csproj**: Add `<Compile Include="Tests\B200Tests.cs" />` entry.

### Ticket 2 — B201-ENTRY-CANCEL-01 Fix (T2)

**File**: `src/PropTraderTools/CopyEngine.cs`
**Type**: Code fix — 5 touch points (1 signature, 1 body, 3 callers)
**Spec requirement IDs**: B201-ENTRY-CANCEL-01

**Touch points (exact lines as of current code):**

| # | Location | Line (approx) | Change |
|---|----------|--------------|--------|
| 1 | `IsQxCancelCandidate` signature | ~L982 | Add `bool isFollower` param |
| 2 | `IsQxCancelCandidate` body | ~L994 | Add `isFollower &&` to "Entry" branch |
| 3 | `CancelQxBrackets` 2-arg body | ~L1009 | Add `bool isFollower = IsFollowerAccount(acc);` + update L1026 call |
| 4 | `BuildQxSnapshot` signature | ~L1050 | Add `bool isFollower = false` default param + update L1070 call |
| 5 | `CancelQxBrackets` 3-arg body | ~L1208 | Add `bool isFollower = IsFollowerAccount(acc);` + update L1230 call |

**Critical constraint**: `BuildQxSnapshot` is `static` — cannot call instance method `IsFollowerAccount`.
Use defaulted parameter `bool isFollower = false` instead (see Section 2.2).

**Do NOT change**: PttBreakEvenSwap.cs, PttGlobalQuickExit.cs, PttQuickExit.cs, or any Feature file.
**PttQuickExit.cs** calls `CopyEngine.BuildQxSnapshot(leader, instr)` — backward-compatible via default param. No change required.

### Ticket 3 — B201Tests.cs (T3)

**File**: `src/PropTraderTools/Tests/B201Tests.cs` (NEW)
**Type**: xUnit test file. Zero changes to any `.cs` source file.
**Spec requirement IDs**: B201-ENTRY-CANCEL-01
**Dependency**: Must be written AFTER T2 signature is confirmed (`IsQxCancelCandidate(Order o, bool isFollower)`).

**Test methods (all `[Fact]`):**

1. `T_B201_01_IsQxCancelCandidate_LeaderEntryOrder_NotCancelled`
   - `order.Name = "Entry"`, `isFollower = false`
   - Assert: `IsQxCancelCandidate(o, false)` returns `false`

2. `T_B201_02_IsQxCancelCandidate_FollowerEntryOrder_Cancelled`
   - `order.Name = "Entry"`, `isFollower = true`
   - Assert: `IsQxCancelCandidate(o, true)` returns `true`

3. `T_B201_03_IsQxCancelCandidate_AtmBracketNames_AlwaysCancelled`
   - Names: `"Stop1"`, `"Target2"`, `"PTT-QX-Stop"`, `"PTT-BE-Target-1"`, `"PTT-CopyEntry"`
   - `isFollower = false` for all
   - Assert: all return `true` (ATM bracket names always cancelled regardless of isFollower)

4. `T_B201_04_CancelQxBrackets_LeaderAccount_DoesNotCancelEntryOrder`
   - Leader account has `"Entry"` Limit (Working) + `"Stop1"` StopMarket (TriggerPending)
   - Assert: `acc.Cancel` called with `["Stop1"]` only — `"Entry"` NOT in cancel list

**PropTraderTools.Tests.csproj**: Add `<Compile Include="Tests\B201Tests.cs" />` entry.

### Ticket 4 — Code Quality (T4)

**File**: `src/PropTraderTools/CopyEngine.cs` (diagnostic log confirm)
**File**: `docs/brain/B200/06-deferred-backlog.md` (NEW — deferred items register)
**Type**: Code quality audit + documentation.

**Diagnostic log confirmation (run before any action):**

```powershell
# Confirm diagnostic logs already absent
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-CALLER\]"
# Expected: 0 matches (already removed)

Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-FLAT-OPEN\]"
# Expected: 0 matches (already removed)

# Confirm permanent operational logs intact
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "\[PTT-ATM-SEND\]|\[PTT-DR4-CLEAR\]|\[PTT-DR5-EVICT\]|\[PTT-GATE5-BLOCKED\]|\[PTT-GATE2-BLOCKED\]"
# Expected: >= 5 matches
```

**Current state (architect verified)**: Both diagnostic tags are already absent from `CopyEngine.cs`.
Log removal is a **no-op** for this ticket. Engineer must confirm and document this.

**CYC confirmation (read-only — no code changes):**

Verify these values match the plan (grep + manual count):
- `HasActivePttFlatten`: CYC = 4 (comment at L3051 confirms)
- `FlattenOneAccount`: CYC = 7 (comment at L5650 confirms)
- `IsQxCancelCandidate`: CYC = 7 (after T2 fix, +0 delta; comment at L981)
- `IsFlattenInFlightOrRecent`: CYC <= 4

**Deferred backlog** (`docs/brain/B200/06-deferred-backlog.md`):

Three items to document (do NOT implement):

1. **DW-B200-LANE-E-COPY-ENABLED** (P2):
   `TryLaneEHook` is called from `OnFollowerPositionUpdate` and is not gated on `_isCopyEnabled`.
   If the user disables copy mid-session, Lane E can still fire. Low risk (`PassesDailyCapCheck` is outer guard). Deferred B202+.

2. **DW-B201-OPTION-B** (P3):
   Alternative approach: rename follower dispatch "Entry" → "PTT-Copy-Entry" at `SendCopyWithAtm` dispatch time. Simpler long-term (removes the need for `isFollower` threading). Deferred pending Director decision.

3. **DW-B199-H2-CLEANUP-01** (P3):
   Conditional: if B199 H2 divide remnant code still present in `SendCopyWithAtm`, remove it. If already removed, close as N/A. No correctness risk; benign no-op.

---

## Section 5: JS Rules Compliance Checklist

| Rule | Description | T1 | T2 | T3 | T4 | Status |
|------|-------------|----|----|----|----|--------|
| JS-021 | No `lock()` anywhere | N/A | PASS — ConcurrentDictionary only | N/A | PASS | ALL PASS |
| JS-001 | No `throw` in hot path | N/A | PASS — no new throw | N/A | PASS | ALL PASS |
| JS-033 | No `async void` (non-handler) | N/A | PASS — no new async | N/A | PASS | ALL PASS |
| JS-066 | CYC ≤ 8 per method | N/A | PASS — all methods ≤8 | N/A | PASS | ALL PASS |
| JS-002 | No `return null` | N/A | PASS — BuildQxSnapshot returns empty set | N/A | N/A | PASS |
| xUnit | `[Fact]` only — no NUnit/MSTest | PASS | N/A | PASS | N/A | ALL PASS |
| ASCII | No Unicode/emoji/curly-quotes | PASS | PASS | PASS | PASS | ALL PASS |
| PTT-prefix | All signal names start with `PTT-` | N/A | PASS — no new signals | N/A | N/A | PASS |

**P0 pre-scan commands** (run before any `.cs` edit):

```powershell
# JS-021: lock ban
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "^\s*lock\s*\(" | Measure-Object
# Must be 0

# JS-033: async void ban
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+\(" | Measure-Object
# Must be 0

# PTT sync
powershell -File .\scripts\ptt-sync.ps1
# Must be 19/0
```

---

## Section 6: Threading Model

All changes operate on the NT8 account background thread (order/position event handlers) or NT8 dispatcher thread (QX/BE button handlers). No UI thread involvement.

| Method | Thread | Shared State | Mechanism | Safe? |
|--------|--------|-------------|-----------|-------|
| `HandleEntryChange` | Account bgd | `_pendingDispatchDrains` (ConcurrentDictionary) | Arithmetic only; dict mutation is downstream | PASS |
| `TryLaneDDispatchFollower` | Account bgd | `_followerSlateReady` (ConcurrentDictionary) | Indexer write (lock-free) | PASS |
| `TryNakedDetect` | Account bgd | None new | Early return guard (read-only) | PASS |
| `FlattenOneAccount` | Account bgd | `_flattenInFlightOrderId`, `_flattenInFlight` (ConcurrentDictionary) | Indexer write (lock-free) | PASS |
| `IsQxCancelCandidate` | Dispatcher | None | Pure predicate (no mutation) | PASS |
| `CancelQxBrackets` 2-arg | Dispatcher | `_followerSlateReady`, `_laneEDispatched` | Lock-free calls | PASS |
| `CancelQxBrackets` 3-arg | Dispatcher | Same as 2-arg | Lock-free calls | PASS |
| `BuildQxSnapshot` | Dispatcher | None | Local HashSet; no shared mutation | PASS |

**No Dispatcher.InvokeAsync required** — none of these changes touch WPF/XAML UI elements.

---

## Section 7: 7-Scan Checklist Per Ticket

### T1 — SCAN Checklist

- **SCAN-01 (Lock ban)**: No `lock()` in test file. Assert statements only. PASS.
- **SCAN-02 (Async void)**: No async methods in test file. PASS.
- **SCAN-03 (CYC)**: Test methods typically CYC=1 (linear). No branching in tests. PASS.
- **SCAN-04 (Throw)**: No `throw new` in test assertions. Use `Assert.Throws` pattern if needed. PASS.
- **SCAN-05 (Test framework)**: `using Xunit;` — `[Fact]` only. No `[TestMethod]`, no `[Test]`. PASS.
- **SCAN-06 (ASCII)**: All string literals in tests are ASCII-only. PASS.
- **SCAN-07 (File-only)**: T1 must create `B200Tests.cs` ONLY. No modifications to CopyEngine.cs or other source files.

### T2 — SCAN Checklist

- **SCAN-01 (Lock ban)**: `bool isFollower = IsFollowerAccount(acc)` — no lock. ConcurrentDictionary used elsewhere. Verify `grep "lock(" src/PropTraderTools/CopyEngine.cs` = 0 after edit.
- **SCAN-02 (Async void)**: No new async methods. Verify `grep "async void " src/PropTraderTools/CopyEngine.cs` = 0 (non-handlers).
- **SCAN-03 (CYC)**: IsQxCancelCandidate: 7 (+0). CancelQxBrackets 2-arg: 6 (+0). CancelQxBrackets 3-arg: 7 (+0). BuildQxSnapshot: 4 (+0). All ≤8.
- **SCAN-04 (Throw)**: No new `throw` statements. PASS.
- **SCAN-05 (Static constraint)**: `BuildQxSnapshot` is `static` — must use defaulted param `bool isFollower = false`. Do NOT add `IsFollowerAccount` call inside the static method.
- **SCAN-06 (ASCII)**: "Entry", "#pending", "PTT-" strings all ASCII. PASS.
- **SCAN-07 (Touch scope)**: Only `CopyEngine.cs`. 5 touch points only (no Feature file changes). Single-hunk ban: diff must show multiple distinct hunks (not a full-file replace).

### T3 — SCAN Checklist

- **SCAN-01**: No `lock()`. PASS.
- **SCAN-02**: No async void. PASS.
- **SCAN-03**: CYC=1 per test (linear [Fact] methods). PASS.
- **SCAN-04**: No throw in test logic. PASS.
- **SCAN-05**: `using Xunit;` + `[Fact]` only. PASS.
- **SCAN-06**: ASCII-only string literals. PASS.
- **SCAN-07**: T3 creates `B201Tests.cs` ONLY. No src changes. Must reference T2's new signature `IsQxCancelCandidate(Order o, bool isFollower)`.

### T4 — SCAN Checklist

- **SCAN-01**: No lock introduced by log removal. PASS.
- **SCAN-02**: No async void. PASS.
- **SCAN-03**: Log removal = line deletion only. CYC unchanged. PASS.
- **SCAN-04**: No throw. PASS.
- **SCAN-05**: No test framework changes. PASS.
- **SCAN-06**: ASCII-only. PASS.
- **SCAN-07**: T4 creates `06-deferred-backlog.md` ONLY (besides the log-confirm step). If logs already absent (architect verified: they are), no CopyEngine.cs changes in T4. If somehow present, remove only the 5 PTT-ATM-CALLER lines and 1 PTT-FLAT-OPEN line. Do NOT remove: [PTT-ATM-SEND], [PTT-GATE5-BLOCKED], [PTT-GATE2-BLOCKED], [PTT-DR4-CLEAR], [PTT-DR5-EVICT], [PTT-ATM-ERROR].

---

## Section 8: Acceptance Criteria Per Ticket

### T1 Acceptance Criteria

- [ ] `B200Tests.cs` created and compiles clean
- [ ] All 4 `[Fact]` tests pass: `dotnet test --filter "B200"`
- [ ] No existing test regressions: `dotnet test` (full suite)
- [ ] No `.cs` source files modified
- [ ] `ptt-sync.ps1` reports 19/0

### T2 Acceptance Criteria

- [ ] `grep "isFollower && o.Name" src/PropTraderTools/CopyEngine.cs` → 1 match at ~L994
- [ ] `grep "bool isFollower = IsFollowerAccount" src/PropTraderTools/CopyEngine.cs` → 2 matches (in 2-arg and 3-arg CancelQxBrackets)
- [ ] `BuildQxSnapshot` signature shows `bool isFollower = false` defaulted param
- [ ] `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0 new occurrences
- [ ] All existing tests pass: `dotnet test` clean
- [ ] `ptt-sync.ps1` reports 19/0
- [ ] Ph4b deletion audit: no symbols deleted without DW-card
- [ ] Single-hunk ban: `git show HEAD -- CopyEngine.cs | Select-String "^\@\@"` count > 1

### T3 Acceptance Criteria

- [ ] `B201Tests.cs` created and compiles clean
- [ ] All 4 `[Fact]` tests pass: `dotnet test --filter "B201"`
- [ ] No existing test regressions
- [ ] No `.cs` source files modified
- [ ] `ptt-sync.ps1` reports 19/0

### T4 Acceptance Criteria

- [ ] `grep "\[PTT-ATM-CALLER\]" src/PropTraderTools/CopyEngine.cs` = 0 (confirm absent)
- [ ] `grep "\[PTT-FLAT-OPEN\]" src/PropTraderTools/CopyEngine.cs` = 0 (confirm absent)
- [ ] `docs/brain/B200/06-deferred-backlog.md` created with 3 deferred DW items
- [ ] All existing tests pass after T4
- [ ] `ptt-sync.ps1` reports 19/0

---

## Section 9: Prior Backlog Items (B199 Carry-Forwards)

From `docs/brain/B199/06-deferred-backlog.md`:

**Items NOT addressed by B200+B201** (all carry forward unchanged):
- DW-B198-SIMSTP-SIM-01 (P1): B198 SIM gate not yet run
- DW-B183-03 (P1): IsLaneEEligible CYC=8 frozen
- DW-B167-DEFER-03 (P1): Combined B161-B166 SIM gate
- DW-B167-DEFER-04 (P1): B167 SIM gate prerequisite
- DW-B170-SIM (P1): B170 SIM gate
- DW-B197-SIM-01 (P1): B197 SIM gate
- DW-B198-GUARDIAN-01 (P2): Guardian script TradeCopierPanel BrushTeal failure
- DW-B192-SINST-01 (P2): SetInstrument() CYC ceiling
- DW-B159-K2/K3/K4 (P2): BE path tests deferred
- DW-B167-DEFER-01 (P2): ECA-1 double-position race
- DW-B197-03 (P2): Pre-existing DwB15701SlateReadyTests failures
- DW-B193-01 (P3): OnQuickAllBufferChanged old labels
- DW-B197-01 (P3): Stale CCN comment at IsPttDragOrphanCancellable
- DW-B197-02 (P3): T4 Fact names misleading
- DW-B197-04 (P3): TryCancelSingleStaleGtcExit not in plan CYC table
- DW-B199-H2-CLEANUP-01 (P3): Remove T2b H2 logic from SendCopyWithAtm (addressed by T4 as conditional check)

**New deferred items opened by B200+B201** (see Section 4, T4 deferred backlog):
- DW-B200-LANE-E-COPY-ENABLED (P2)
- DW-B201-OPTION-B (P3)
- DW-B199-H2-CLEANUP-01 disposition (P3) — conditional close or carry forward

---

## Section 10: Pipeline Chain

```
Ph1   ptt-architect       -> docs/brain/B200/02-architecture-plan.md       (THIS DOCUMENT)
Ph2   ptt-plan-reviewer   -> docs/brain/B200/02-plan-review.md             (REVIEW_PASS gate)
Ph3   ptt-architect       -> docs/brain/B200/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B200/04-ticket-review.md           (TICKET_REVIEW_PASS gate)
Ph4a  ptt-engineer        -> src .cs edits + ticket-N-completion.md (x4)
Ph4b  ptt-verifier        -> ticket-N-verification.md (VERIFY_PASS gate x4)
Ph5   ptt-plan-reviewer   -> 05-final-review.md + 06-deferred-backlog.md
```

No shortcuts. No direct `.cs` edits outside Ph4a. Director explicit approval required for deviation.

---

*Written by: ptt-architect*
*Epic: B200+B201*
*Phase: 1 (Architecture)*
*Source investigation: src/PropTraderTools/CopyEngine.cs (direct grep + read)*
*Pre-reads: RULES_CATALOG.md, B200-orchestrator-prompt.md, B200-B201-orchestrator-prompt.md, B199/06-deferred-backlog.md*
