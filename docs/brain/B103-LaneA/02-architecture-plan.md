# B103-LaneA Architecture Plan

**Status**: REVIEW_PASS (cycle 2 — CYC correction applied)
**Author**: ptt-architect
**Date**: 2026-08-10
**Phase**: 1 (Architecture)

---

## 1. Block Summary

Block **B103-LaneA** delivers two surgical fixes to
[`src/PropTraderTools/CopyEngine.cs`](../../src/PropTraderTools/CopyEngine.cs) only.
No other `.cs` files are touched. Both tickets are non-overlapping line-range edits
to the same file and are safe to apply sequentially.

| Ticket | DW Item | Method | Lines (approx) | CYC before | CYC after |
|--------|---------|--------|----------------|------------|-----------|
| T1 | DW-B102 | `LoadRules()` + field deletion | 3868-3871, 4075-4112 | 4 | 4 |
| T2 | DW-B103 | `TryCancelFollowerEntries()` | 1510-1523 | 4 | 6 |

---

## 2. Ticket 1 — DW-B102: Remove `_persistenceLoaded` One-Shot Guard

### 2.1 Problem Statement

[`LoadRules()`](../../src/PropTraderTools/CopyEngine.cs:4082) has a one-shot guard
(`_persistenceLoaded`) that permanently consumes a token on first call.

```
TradeCopierPanel.OnLoaded  ──┐
                              ├──► LoadRules()  ← first caller wins token
TradeCopierWindow.OnLoaded ──┘                  ← second caller is permanent no-op
```

Two callers race for this token. The second caller always receives a no-op.
If the first caller fails (XML file missing at startup, normal on first ever launch),
**no retry is possible**. This causes a post-restart copy outage: the user saves rules,
NT8 restarts, both `OnLoaded` handlers fire — but the first caller found no file,
consumed the token, and the second caller did nothing. Rules never load.

**Confirmed field location** (read at step 0):
[`L3868-3871`](../../src/PropTraderTools/CopyEngine.cs:3868):
```csharp
// -- B6: Persistence field -------------------------------------------

private volatile bool _persistenceLoaded = false;
```

**Confirmed guard location** (read at step 0):
[`L4084-4086`](../../src/PropTraderTools/CopyEngine.cs:4084):
```csharp
if (_persistenceLoaded)
    return;
_persistenceLoaded = true;
```

### 2.2 Proposed Changes

**Change 1A — Delete field and section comment**
Delete [`L3868-3871`](../../src/PropTraderTools/CopyEngine.cs:3868) in full
(section comment + blank line + field + blank line):
```
// -- B6: Persistence field -------------------------------------------
(blank)
private volatile bool _persistenceLoaded = false;
(blank)
```

**Change 1B — Replace guard block in `LoadRules()` body**
At [`L4084-4086`](../../src/PropTraderTools/CopyEngine.cs:4084), replace:
```csharp
if (_persistenceLoaded)
    return;
_persistenceLoaded = true;
```
With:
```csharp
_rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read
```

**Change 1C — Update XML doc comment on `LoadRules()`**
At [`L4075-4081`](../../src/PropTraderTools/CopyEngine.cs:4075), update to:
```csharp
/// <summary>
/// Deserializes rules from an XML file and resets _rules to a fresh ConcurrentBag.
/// Idempotent: each call clears and reloads; second caller sees the same file contents.
/// Called from TradeCopierPanel.OnLoaded and TradeCopierWindow.OnLoaded on the NT main thread.
/// No lock keyword -- UI-thread-only; _rules is ConcurrentBag (thread-safe Add).
/// CYC = 4 (File.Exists guard + try/catch + null-check + foreach)
/// </summary>
```

### 2.3 Safety Analysis

- **Established pattern**: `_rules = new ConcurrentBag<CopyRule>()` is used at L1052, L1090, L1107,
  and L2584 — not a new pattern, engineer already knows this idiom.
- **Thread safety**: `LoadRules()` is UI-thread-only (called from `OnLoaded` handlers, which
  NT8 guarantees run on the NT main thread). The reference assignment is sequentially consistent
  on a single thread. No concurrency window exists.
- **Idempotency**: Both callers clear-then-reload from the same XML file on disk. Result is
  identical rule set. No duplication because the bag is recreated before each `Add()` loop.
- **Retry is now possible**: If XML is missing on first call, `File.Exists` returns early;
  the bag was already cleared. Second caller re-attempts the same logic — same outcome, no harm.
  When file exists (the common case), both callers load successfully.
- **CYC**: Removing the `if (_persistenceLoaded) return;` branch drops CYC from 5 to 4.
  Remaining branches: `File.Exists` guard + `try/catch` + `if (container != null && container.Rules != null)` + `foreach`. CYC remains 4 ≤ 8. ✓

### 2.4 JS Rules Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no `lock()`) | No `lock()` in changed lines | ✓ PASS |
| JS-001 (no `throw new Exception`) | No new exception introduced | ✓ PASS |
| ASCII-only | All new string literals are ASCII | ✓ PASS |
| CYC ≤ 8 | `LoadRules()` CYC = 4 after fix. Still ≤ 8. | ✓ PASS |
| No `DateTime.Now` | Not used | ✓ PASS |
| No `return null` (JS-002) | Not introduced | ✓ PASS |

---

## 3. Ticket 2 — DW-B103: Guard PTT Exit Brackets in `TryCancelFollowerEntries`

### 3.1 Problem Statement

[`TryCancelFollowerEntries()`](../../src/PropTraderTools/CopyEngine.cs:1510)
propagates leader order cancels to followers.

[`IsAtmBracketName()`](../../src/PropTraderTools/CopyEngine.cs:669) only matches
NT8 native names (`Stop1..Stop9`, `Target1..Target9`).

When QX-ALL or BE-ALL places PTT-prefixed exit brackets (`PTT-QX-Stop`, `PTT-BE-Stop-1`, etc.)
and T1 fills, NT8 OCO-cancels the leader's `PTT-QX-Stop`. The cancel event propagates:

```
leader PTT-QX-Stop OCO-cancelled
    -> TryCancelFollowerEntries()
        -> IsAtmBracketName("PTT-QX-Stop") = false   <-- falls through
        -> foreach follower: CancelOneAccount(acc, instrument)
            -> IsBracketLeg(order where Name="PTT-QX-Stop2") = false
               (B29 intentionally removed PTT- prefix from IsBracketLeg)
            -> acc.Cancel([PTT-QX-Stop2])  <-- WRONG: wipes follower bracket
```

Result: followers go flat at T1; leader keeps pairs 2 and 3.

**Confirmed current state** (read at step 0)
[`L1510-1523`](../../src/PropTraderTools/CopyEngine.cs:1510):
```csharp
private bool TryCancelFollowerEntries(Order order, CopyRule rule)
{
    if (order.OrderState != OrderState.Cancelled)
        return false;
    if (IsAtmBracketName(order.Name))
        return true; // HOTFIX-B63-COPY-CANCEL-01
    foreach (var acc in rule.FollowerAccounts)
    {
        if (acc == null)
            continue;
        CancelOneAccount(acc, order.Instrument);
    }
    return true;
}
```

### 3.2 Proposed Change

Insert a new guard **after** the `IsAtmBracketName` guard (after L1515), **before** the `foreach`:

```csharp
if (order.Name != null
    && (   order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
        || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)))
    return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
```

**Full method after fix:**
```csharp
// TryCancelFollowerEntries: CYC=6. Propagates leader cancel to all follower entry orders.
// Returns true if Cancelled state was handled (caller should return immediately).
// HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
// DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).
// JS-021: no lock. JS-001: no throw.
private bool TryCancelFollowerEntries(Order order, CopyRule rule)
{
    if (order.OrderState != OrderState.Cancelled)
        return false;
    if (IsAtmBracketName(order.Name))
        return true; // HOTFIX-B63-COPY-CANCEL-01
    if (order.Name != null
        && (   order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
            || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)))
        return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
    foreach (var acc in rule.FollowerAccounts)
    {
        if (acc == null)
            continue;
        CancelOneAccount(acc, order.Instrument);
    }
    return true;
}
```

**Return value semantics clarification**: `return false` means "this cancel was NOT handled
by the follower-cancel path — do not cancel followers." The calling dispatcher treats `false`
as "not my concern, continue to next gate." This is the correct semantic: when NT8 OCO-cancels
a PTT exit bracket on the leader, the follower's equivalent bracket is managed by the follower's
own OCO logic, not by leader-propagated cancel.

### 3.3 Safety Analysis

- **CRITICAL isolation**: This change is in `TryCancelFollowerEntries()` ONLY.
- **`IsBracketLeg()` (instance) [`L3198-3205`](../../src/PropTraderTools/CopyEngine.cs:3198)**:
  B29 intentionally removed the `PTT-` prefix from `IsBracketLeg` so the Cancel button can still
  cancel PTT exit brackets. **UNTOUCHED.**
- **`CancelOneAccount()` [`L2915-2939`](../../src/PropTraderTools/CopyEngine.cs:2915)**:
  Called by `CancelPendingEntries` (user-initiated cancel) where cancelling `PTT-QX-*/PTT-BE-*`
  IS intentional. **UNTOUCHED.**
- **`IsAtmBracketName()` ~L669**: Static guard for NT8 native names. **UNTOUCHED.**
- **`_rules` field ~L178**: **UNTOUCHED.**
- **`StringComparison.Ordinal`**: Used for all prefix checks per JS performance best practice.
  Matches the convention already established at `L3203` (`order.Name.StartsWith("Stop")`
  which should also use Ordinal — but that is pre-existing code, not in scope here).
- **CYC after fix = 6**:
  1. `order.OrderState != OrderState.Cancelled`
  2. `IsAtmBracketName(order.Name)`
  3. `order.Name != null` (null guard of compound)
  4. `StartsWith("PTT-QX-") || StartsWith("PTT-BE-")` (OR branch)
  5. `foreach (var acc in rule.FollowerAccounts)` (loop)
  6. `if (acc == null)` (null guard)

  CYC = 6 ≤ 8. ✓

### 3.4 JS Rules Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no `lock()`) | No `lock()` introduced | ✓ PASS |
| JS-001 (no `throw new Exception`) | No exception introduced | ✓ PASS |
| ASCII-only | `"PTT-QX-"` and `"PTT-BE-"` are ASCII | ✓ PASS |
| CYC ≤ 8 | `TryCancelFollowerEntries()` CYC = 6 | ✓ PASS |
| `StringComparison.Ordinal` | Used for both `StartsWith` calls | ✓ PASS |
| No `return null` (JS-002) | Returns `false` (bool), not null | ✓ PASS |

---

## 4. Batch Safety — Why Both Tickets Are Safe Together

| Property | Details |
|----------|---------|
| Non-overlapping regions | T1 touches L3868-3871 and L4075-4112. T2 touches L1510-1523. Zero line overlap. |
| Same file | Both edits are in `src/PropTraderTools/CopyEngine.cs` only. |
| No shared state | `LoadRules()` manages `_rules`. `TryCancelFollowerEntries()` reads `order.Name`. No interaction. |
| Application order | T2 (lower lines) first, T1 (higher lines) second — preserves line offsets. Either order is safe since ranges do not overlap. |
| Build impact | Both changes reduce or maintain CYC. No API surface change. No new dependencies. |

---

## 5. Unchanged Regions (Protected — Engineer Must Not Touch)

| Region | Lines | Reason Protected |
|--------|-------|-----------------|
| `IsBracketLeg()` (instance) | L3198-3205 | B29 intentional design: PTT- excluded so Cancel button works |
| `CancelOneAccount()` | L2915-2939 | User-initiated cancel path: PTT-QX-*/PTT-BE-* cancel IS intentional here |
| `IsAtmBracketName()` | ~L669-682 | B63 hotfix: NT8 native bracket guard; modifying breaks the B63 fix |
| `_rules` field declaration | ~L178 | Shared state; field itself is correct — only the one-shot guard is wrong |
| All other `CopyEngine.cs` code | everywhere else | Not in scope for this block |

---

## 6. 7-Scan Checklist (Pre-Implementation Contract)

The engineer MUST run each scan after applying changes and confirm the expected result
before reporting completion.

| # | Scan | Command | Expected Result |
|---|------|---------|----------------|
| SCAN-01 | No `lock()` in changed code | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero new `lock(` in changed regions |
| SCAN-02 | No `throw new` in changed code | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | Zero new `throw new` in changed regions |
| SCAN-03 | ASCII-only in new string literals | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | Zero non-ASCII characters |
| SCAN-04 | `_persistenceLoaded` fully removed | `grep -n "_persistenceLoaded" src/PropTraderTools/CopyEngine.cs` | **0 matches** (field + guard both gone) |
| SCAN-05 | PTT-QX guard present | `grep -n "PTT-QX-" src/PropTraderTools/CopyEngine.cs` | New guard line present in `TryCancelFollowerEntries` |
| SCAN-06 | CYC of `LoadRules()` | Manual count: `File.Exists` + `try/catch` + `null-check` + `foreach` | = **4** ≤ 8 |
| SCAN-07 | CYC of `TryCancelFollowerEntries()` | Manual count: OrderState + IsAtmBracket + null + OR + foreach + acc-null | = **6** ≤ 8 |

---

## 7. Threading Model Summary

| Method | Thread Context | Mutation | Safety Verdict |
|--------|---------------|----------|----------------|
| `LoadRules()` | NT main thread (UI) — called from `OnLoaded` | `_rules = new ConcurrentBag<>()` reference assignment | Safe: single-thread sequential; no concurrent caller |
| `TryCancelFollowerEntries()` | NT order-update callback (routed through existing Dispatcher.InvokeAsync) | None — pure read of `order.Name` | Safe: read-only operation on immutable order name string |

No `Dispatcher.InvokeAsync` additions required. No new concurrency primitives required.

---

## 8. NT8 API Surface

No new NT8 API calls introduced by either ticket.

| API Access | Ticket | Existing Usage Evidence |
|-----------|--------|------------------------|
| `order.Name` (string property) | T2 | Already used at L1514, L3202-3203 — same `StartsWith` pattern |
| `ConcurrentBag<CopyRule>` constructor | T1 | Already used at L1052, L1090, L1107, L2584 |

All API surface is pre-existing and confirmed in the current file.

---

**PLAN_COMPLETE**
