# B161-B166 Ghost-Entry Repair Block — Verification Tickets

**Epic:** B161-B166
**Phase:** 3 — Ticket Generation
**Architect:** ptt-architect
**Plan:** `docs/brain/B161-B166/02-architecture-plan.md` — REVIEW_PASS
**Date:** 2026-09-15

> **SCOPE:** All 7 repairs are **already present** in `src/PropTraderTools/CopyEngine.cs`.
> These tickets are **VERIFICATION ONLY**. No code changes are required or permitted.
> **DO NOT MODIFY** `src/PropTraderTools/CopyEngine.cs`.

---

## VIOLATION-01-FIX NOTE (Mandatory Follow-On)

Per `02-plan-review.md` §8 and §4, `IsFollowerPositionBlocked` has CYC=9 (branch-counted:
1 base + 8 `if`-branches = 9), which exceeds the JS-CYC ≤8 budget. This is a known,
acknowledged, P1 violation. The verifier for Tickets T6 and T7 MUST count branches and
report the actual McCabe CYC. A separate follow-on ticket `VIOLATION-01-FIX` must be issued
to extract Signals 5+6 into `IsBeSettleOrRetrySignalActive(Account, Instrument)` helper,
reducing the parent CYC from 9 to 8.

---

## Ticket T1: B161-VERIFY

**Ticket ID:** B161-VERIFY
**Epic:** B161
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B161 ghost-entry guard on `TryReplaceOnAtmCancel`

### Method

[`TryReplaceOnAtmCancel`](src/PropTraderTools/CopyEngine.cs:947)
**Confirmed lines:** L947–L960

### CYC

| Before | After | Budget |
|--------|-------|--------|
| 4 | 5 | ≤ 8 ✓ |

### Old Code — MUST NOT be present

Direct call to `ReplaceFollowerCopyOnAtmCancel` without the `IsFollowerPositionBlocked` guard
between the `IsPttEntryOrderCancelTrigger` check and the call:

```csharp
// ABSENT (old path, no guard):
if (!IsPttEntryOrderCancelTrigger(order))
    return;
ReplaceFollowerCopyOnAtmCancel(order);
```

The absence of the `IsFollowerPositionBlocked` if-block (lines 955–958) is the bug.

### New Code — MUST be present

Three-line guard at L955–L958 inserted between guard (3) and the final call:

```csharp
if (order.Account != null
    && order.Instrument != null
    && IsFollowerPositionBlocked(order.Account, order.Instrument))
    return; // BE-All race -- skip re-place (4)
```

### Verification Criterion (positive — MUST match)

```
grep -n "IsFollowerPositionBlocked" src/PropTraderTools/CopyEngine.cs
```

Expected: at least one match inside `TryReplaceOnAtmCancel` (around L957).
Confirm context: the match must appear **after** `IsPttEntryOrderCancelTrigger` and **before**
`ReplaceFollowerCopyOnAtmCancel` within the method body.

Also confirm the comment token:
```
grep -n "BE-All race" src/PropTraderTools/CopyEngine.cs
```
Expected: match at approximately L958.

### Negative Verification (MUST return no match)

```
# No direct call to ReplaceFollowerCopyOnAtmCancel without the IsFollowerPositionBlocked guard.
# Verify by reading L947-L960: IsPttEntryOrderCancelTrigger return must be followed by the
# IsFollowerPositionBlocked block before ReplaceFollowerCopyOnAtmCancel.
# There is no grep-expressible negative for this; verifier reads the method body and confirms
# the guard is present between guards (3) and the call.
```

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | `void` method; no `throw` statement |
| JS-002 | `void`; not applicable |
| JS-021 | No `lock()`. `IsFollowerPositionBlocked` uses `acc.Orders.ToList()` snapshot |
| JS-042 | `"BE-All race -- skip re-place (4)"` is ASCII-only |
| JS-CYC | CYC=5 ≤ 8 |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L947–L960, confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — `void` method; not applicable
- [ ] **SCAN-03** JS-021 no-lock verified — grep `"lock("` in L947–L960; expect zero matches
- [ ] **SCAN-04** JS-042 ASCII-only verified — all string literals in L947–L960 are ASCII-only
- [ ] **SCAN-05** JS-CYC within budget — count branches in L947–L960; expect CYC=5
- [ ] **SCAN-06** NT8 API constraints verified — `IsFollowerPositionBlocked` call uses `acc.Orders.ToList()` (not bare `acc.Orders`); null-checks on `order.Account` and `order.Instrument` precede the call
- [ ] **SCAN-07** Signal continuity verified — guard is placed **after** `IsPttEntryOrderCancelTrigger` check (cheap name/state filter) and **before** `ReplaceFollowerCopyOnAtmCancel`

---

## Ticket T2: B162A-VERIFY

**Ticket ID:** B162A-VERIFY
**Epic:** B162-A
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B162-A atomic claim gate — `TryGetValue` → `TryRemove` at guard (4)

### Method

[`TryFireBePriceRetry`](src/PropTraderTools/CopyEngine.cs:7218)
**Confirmed lines:** L7218–L7261

### CYC

| Before | After | Budget |
|--------|-------|--------|
| 7 | 8 | ≤ 8 ✓ |

Note: the `||` compound condition at L7246–7247 (flat-cancel guard) adds one branch, taking
CYC 7→8.

### Old Code — MUST NOT be present

Non-consuming read at guard (4):

```csharp
// ABSENT:
if (!_pendingBePriceRetry.TryGetValue(o.Account.Name, out double bePrice)) return;
```

Also absent: a `_bePriceRetryAttempts.TryRemove(...)` call inside the flat-cancel
`if (!StillAtNonBePrice(...) || IsFlat(...))` block.

### New Code — MUST be present

Atomic claim at L7224:

```csharp
if (!_pendingBePriceRetry.TryRemove(o.Account.Name, out double bePrice))  // (4) B162 atomic claim
    return;
```

And **no** `_bePriceRetryAttempts.TryRemove` call inside the flat-cancel path block
(L7246–L7250). The sentinel must survive through that path.

### Verification Criterion (positive — MUST match)

```
grep -n "TryRemove" src/PropTraderTools/CopyEngine.cs
```

Filter output for lines near `_pendingBePriceRetry`. Confirm:
- `_pendingBePriceRetry.TryRemove` appears at L7224 (guard-4 position, inside
  `TryFireBePriceRetry`).
- `_bePriceRetryAttempts.TryRemove` does NOT appear inside the flat-cancel block
  (L7246–L7250).

Also confirm the B162 comment tag:
```
grep -n "B162 atomic claim" src/PropTraderTools/CopyEngine.cs
```
Expected: match at approximately L7224.

### Negative Verification (MUST return no match)

```
grep -n "TryGetValue.*_pendingBePriceRetry" src/PropTraderTools/CopyEngine.cs
```
Expected: **zero matches**. `TryGetValue` must not appear in conjunction with
`_pendingBePriceRetry` anywhere in the file.

```
grep -n "_bePriceRetryAttempts.TryRemove" src/PropTraderTools/CopyEngine.cs
```
Expected: matches at L7231 (Working path), L7253 (live-cancel path), and L2022
(`RunFollowerEvictPath`). Must NOT appear at L7246–L7250 (flat-cancel path body).
Verifier reads L7242–L7250 and confirms no `TryRemove` call in that block.

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | `void` method; no `throw` |
| JS-002 | `void`; not applicable |
| JS-021 | `ConcurrentDictionary.TryRemove` is lock-free atomic; no `lock()` |
| JS-042 | All string literals in method are ASCII-only |
| JS-CYC | CYC=8 ≤ 8 |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L7218–L7261, confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — `void` method; not applicable
- [ ] **SCAN-03** JS-021 no-lock verified — grep `"lock("` in L7218–L7261; expect zero matches
- [ ] **SCAN-04** JS-042 ASCII-only verified — all string literals in L7218–L7261 are ASCII-only
- [ ] **SCAN-05** JS-CYC within budget — count branches in L7218–L7261; expect CYC=8 (compound `||` at L7246–7247 counts as +1)
- [ ] **SCAN-06** NT8 API constraints verified — `_pendingBePriceRetry.TryRemove` (not `TryGetValue`) at guard (4); `ConcurrentDictionary` usage is correct
- [ ] **SCAN-07** Signal continuity verified — `_bePriceRetryAttempts` sentinel is NOT cleared in flat-cancel path (L7242–L7250); confirm comment "B166: counter NOT cleared here" or equivalent is present at L7226

---

## Ticket T3: B162B-VERIFY

**Ticket ID:** B162B-VERIFY
**Epic:** B162-B
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B162-B prefix fix — `"PTT-BE-Stop-"` → `"PTT-BE-Stop"` (no trailing dash)
in `HasActivePttBeExits` and `StillAtNonBePrice`

### Methods

- [`HasActivePttBeExits`](src/PropTraderTools/CopyEngine.cs:3104) — **Confirmed line:** L3110
- [`StillAtNonBePrice`](src/PropTraderTools/CopyEngine.cs:7181) — **Confirmed lines:** L7187–L7188

### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `HasActivePttBeExits` | 8 | 8 | ≤ 8 ✓ |
| `StillAtNonBePrice` | 7 | 7 | ≤ 8 ✓ |

CYC unchanged — only a string literal was modified, no branches added or removed.

### Old Code — MUST NOT be present

Dash-suffixed prefix in `HasActivePttBeExits` at L3110:

```csharp
// ABSENT in HasActivePttBeExits:
!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)
```

Dash-suffixed prefix in `StillAtNonBePrice` at L7187–L7188:

```csharp
// ABSENT in StillAtNonBePrice:
!o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)
```

### New Code — MUST be present

No-dash prefix in `HasActivePttBeExits` at L3110:

```csharp
if (o.Name == null || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) continue;
```

No-dash prefix in `StillAtNonBePrice` at L7187–L7188:

```csharp
if (o.Name == null
    || !o.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
```

### Intentional Asymmetry — `TryTriggerBeRecovery` RETAINS dash

> **CRITICAL NOTE:** `TryTriggerBeRecovery` at L5193 retains `"PTT-BE-Stop-"` **WITH** the
> trailing dash. This is intentional and correct — that guard controls OCO-pair bracket
> recovery for followers only, and bare `"PTT-BE-Stop"` (leader's atomic stop) must NOT
> trigger bracket recovery. Do NOT flag `TryTriggerBeRecovery`'s `"PTT-BE-Stop-"` as a
> negative match. It is by design.

### Verification Criterion (positive — MUST match)

```
grep -n '"PTT-BE-Stop"' src/PropTraderTools/CopyEngine.cs
```
Expected: matches in `HasActivePttBeExits` (around L3110) and `StillAtNonBePrice` (around
L7187–L7188). The matched string must be `"PTT-BE-Stop"` without a trailing dash in both
locations.

Also confirm the B162-B comment tag:
```
grep -n "B162-B" src/PropTraderTools/CopyEngine.cs
```
Expected: matches near L3110 and L7188.

### Negative Verification (MUST return no match in targeted methods)

Read L3104–L3115 and L7181–L7196. Confirm:

- `"PTT-BE-Stop-"` (with trailing dash) does **NOT** appear in `HasActivePttBeExits`.
- `"PTT-BE-Stop-"` (with trailing dash) does **NOT** appear in `StillAtNonBePrice`.

The following grep should return zero matches in those line ranges:
```
# Check HasActivePttBeExits region only (L3104-L3115):
grep -n '"PTT-BE-Stop-"' src/PropTraderTools/CopyEngine.cs
```
Any matches should be **outside** these two methods (e.g., in `TryTriggerBeRecovery`,
`HasSettlingBeTargets`, `HasActivePttBeTargets` — all of which correctly use the dash-suffixed
or `"PTT-BE-Target-"` form).

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | Both `bool`-returning; no `throw` |
| JS-002 | `false` on all guard exits; no null returns |
| JS-021 | `acc.Orders.ToList()` snapshot; no `lock()` |
| JS-042 | `"PTT-BE-Stop"` is ASCII-only |
| JS-CYC | Both methods CYC unchanged (8 and 7) |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L3104–L3115 and L7181–L7196; confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — both methods return `false` (bool) on guard exits; no null
- [ ] **SCAN-03** JS-021 no-lock verified — grep `"lock("` in both regions; expect zero matches
- [ ] **SCAN-04** JS-042 ASCII-only verified — `"PTT-BE-Stop"` is ASCII-only in both locations
- [ ] **SCAN-05** JS-CYC within budget — count branches in each method; expect CYC=8 (`HasActivePttBeExits`) and CYC=7 (`StillAtNonBePrice`), unchanged from before
- [ ] **SCAN-06** NT8 API constraints verified — `acc.Orders.ToList()` snapshot used in `HasActivePttBeExits` (L3108); `StillAtNonBePrice` also uses `acc.Orders.ToList()` (L7185)
- [ ] **SCAN-07** Signal continuity verified — `TryTriggerBeRecovery` at approximately L5193 still uses `"PTT-BE-Stop-"` WITH dash (intentional asymmetry); confirm it was not changed by this repair

---

## Ticket T4: B163-VERIFY

**Ticket ID:** B163-VERIFY
**Epic:** B163
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B163 AverageFillPrice guard in `IsPttEntryOrderCancelTrigger`

### Method

[`IsPttEntryOrderCancelTrigger`](src/PropTraderTools/CopyEngine.cs:924)
**Confirmed lines:** L924–L935

### CYC

| Before | After | Budget |
|--------|-------|--------|
| 3 | 4 | ≤ 8 ✓ |

### Old Code — MUST NOT be present

The method without the AFP guard — guards (1), (2), (3) only, then directly returning:

```csharp
// ABSENT (old, no AFP guard):
if (order.Name != "PTT-Copy" && order.Name != "Entry")
    return false; // (3)
return order.LimitPrice > 0 && order.Instrument?.FullName != null;
// (no AverageFillPrice check between guard-3 and the return)
```

### New Code — MUST be present

AFP guard at L932–L933 (guard position 4):

```csharp
if (order.AverageFillPrice > 0)
    return false; // (4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger
```

### Verification Criterion (positive — MUST match)

```
grep -n "AverageFillPrice" src/PropTraderTools/CopyEngine.cs
```
Expected: at least one match inside `IsPttEntryOrderCancelTrigger` at approximately L932.

Also confirm the B163 comment tag:
```
grep -n "B163" src/PropTraderTools/CopyEngine.cs
```
Expected: match at approximately L933 referencing `"lifecycle cancel of a previously-filled Entry"`.

### Negative Verification (MUST return no match in targeted region)

Read L924–L935. Confirm: there is no direct path from guard (3) (`order.Name` check) to the
`return order.LimitPrice > 0 ...` line without passing through the
`if (order.AverageFillPrice > 0) return false;` guard at L932.

No grep-expressible negative exists; verifier reads the method body and confirms the AFP guard
is present between `order.Name` check (L930–L931) and the final `return` (L934).

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | `static bool`-returning; no `throw` |
| JS-002 | `false` on all guard exits; no null return |
| JS-021 | `static` method; no instance state; no `lock()` |
| JS-042 | `"(4) B163: lifecycle cancel of a previously-filled Entry -- not a re-place trigger"` is ASCII-only |
| JS-CYC | CYC=4 ≤ 8 |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L924–L935; confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — `bool`-returning; all exits return `false` or a bool expression; no null
- [ ] **SCAN-03** JS-021 no-lock verified — `static` method; grep `"lock("` in L924–L935; expect zero matches
- [ ] **SCAN-04** JS-042 ASCII-only verified — all string literals and comment text in L924–L935 are ASCII-only
- [ ] **SCAN-05** JS-CYC within budget — count branches in L924–L935; expect CYC=4 (null-check, state-check, name-check, AFP-check)
- [ ] **SCAN-06** NT8 API constraints verified — `order.AverageFillPrice` is a direct property read; `order.Instrument?.FullName` uses null-conditional correctly
- [ ] **SCAN-07** Signal continuity verified — AFP guard is at position (4), **after** the name check (guard 3) and **before** the final `return`; confirms ordering preserves cheap-first guard principle

---

## Ticket T5: B164-VERIFY

**Ticket ID:** B164-VERIFY
**Epic:** B164
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B164 Signal 4 — `HasActivePttBeTargets` new helper + Signal 4 call in
`IsFollowerPositionBlocked`

### Methods

- [`HasActivePttBeTargets`](src/PropTraderTools/CopyEngine.cs:3147) — **Confirmed lines:** L3147–L3158 (new helper)
- [`IsFollowerPositionBlocked`](src/PropTraderTools/CopyEngine.cs:3182) — **Confirmed lines:** L3213–L3220 (Signal 4 call block)

### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `HasActivePttBeTargets` (new) | — | 8 | ≤ 8 ✓ |
| `IsFollowerPositionBlocked` (at B164 stage) | 6 | 7 | ≤ 8 ✓ (at this stage before B165/B166) |

### Old Code — MUST NOT be present

No prior form exists — this was a pure addition. There is nothing to be absent.

### New Code — MUST be present

Helper body at L3147–L3158:

```csharp
internal bool HasActivePttBeTargets(Account acc, Instrument instr)
{
    if (acc == null)   return false;
    if (instr == null) return false;
    foreach (var o in acc.Orders.ToList())
    {
        if (o.Name == null || !o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)) continue;
        if (o.Instrument?.FullName != instr.FullName) continue;
        if (IsPttBeStopActiveState(o.OrderState)) return true;
    }
    return false;
}
```

Signal 4 call block at L3213–L3220 in `IsFollowerPositionBlocked`:

```csharp
if (HasActivePttBeTargets(acc, instr))  // (7) Signal 4 -- post-Stop-fill race (B164)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return true;
}
```

### Verification Criterion (positive — MUST match)

```
grep -n "HasActivePttBeTargets" src/PropTraderTools/CopyEngine.cs
```
Expected: at least two matches — one for the method definition (around L3147) and one for the
call site in `IsFollowerPositionBlocked` (around L3213).

```
grep -n '"PTT-BE-Target-"' src/PropTraderTools/CopyEngine.cs
```
Expected: match inside `HasActivePttBeTargets` at approximately L3153.

```
grep -n "active-be-targets" src/PropTraderTools/CopyEngine.cs
```
Expected: match at approximately L3216 (log string in Signal 4 block).

### Negative Verification

No specific old form to verify absent. Verifier confirms:

- `HasActivePttBeTargets` method **exists** (was not already present before this block).
- The `"PTT-BE-Target-"` prefix in `HasActivePttBeTargets` retains the trailing dash (it is
  a suffixed-only name, unlike `HasActivePttBeExits` which uses bare `"PTT-BE-Stop"`).

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | Both `bool`-returning; no `throw` |
| JS-002 | `false` on all null/loop-exit paths; no null return |
| JS-021 | `acc.Orders.ToList()` snapshot; no `lock()` |
| JS-042 | `"[PTT-COPY-GUARD] ghost-entry blocked (active-be-targets): "` is ASCII-only; `"PTT-BE-Target-"` is ASCII-only |
| JS-CYC | `HasActivePttBeTargets` CYC=8 ≤ 8; `IsFollowerPositionBlocked` CYC=7 at this stage |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L3147–L3158 and L3213–L3220; confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — `HasActivePttBeTargets` returns `false` (bool) on all guard exits; no null
- [ ] **SCAN-03** JS-021 no-lock verified — grep `"lock("` in L3147–L3158; expect zero matches; `ToList()` snapshot used
- [ ] **SCAN-04** JS-042 ASCII-only verified — all string literals in both regions are ASCII-only
- [ ] **SCAN-05** JS-CYC within budget — count branches in `HasActivePttBeTargets`; expect CYC=8 (base + acc-null + instr-null + foreach + name-null + name-prefix + instr-check + state-call)
- [ ] **SCAN-06** NT8 API constraints verified — `acc.Orders.ToList()` snapshot in `HasActivePttBeTargets` (L3151); `IsPttBeStopActiveState` reused correctly for Target order state check
- [ ] **SCAN-07** Signal continuity verified — Signal 4 block appears at position (7) in `IsFollowerPositionBlocked`, after Signal 3 (`HasSettlingBeTargets`) and before Signal 5 (`IsBeSlotNonTerminal`); confirms temporal ordering per §4 signal map

---

## Ticket T6: B165-VERIFY

**Ticket ID:** B165-VERIFY
**Epic:** B165
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B165 Signal 5 — `IsBeSlotNonTerminal` call block in `IsFollowerPositionBlocked`

### Methods

- [`IsFollowerPositionBlocked`](src/PropTraderTools/CopyEngine.cs:3182) — **Confirmed lines:** L3222–L3229 (Signal 5 block)
- [`IsBeSlotNonTerminal`](src/PropTraderTools/CopyEngine.cs:9382) — **Confirmed lines:** L9382–L9386

### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `IsFollowerPositionBlocked` (at B165 stage) | 7 | 8 | ≤ 8 ✓ (at this stage before B166) |
| `IsBeSlotNonTerminal` | pre-existing | 1 | ≤ 8 ✓ |

> **VIOLATION-01 NOTE:** After B166 adds Signal 6 at L3231, `IsFollowerPositionBlocked`
> reaches CYC=9 (1 base + 8 `if`-branches). The verifier MUST perform a branch count on
> the full method body (L3182–L3241) and report the actual McCabe CYC. Expected count at
> current state: **9**. This is a known, acknowledged P1 violation per `02-architecture-plan.md`
> §6 VIOLATION-01 and `02-plan-review.md` §4. Remediation is tracked as follow-on ticket
> VIOLATION-01-FIX (extract Signals 5+6 into `IsBeSettleOrRetrySignalActive` helper).

### Old Code — MUST NOT be present

No prior form exists — this was a pure addition. Nothing to be absent.

### New Code — MUST be present

Signal 5 call block at L3222–L3229 in `IsFollowerPositionBlocked`:

```csharp
if (IsBeSlotNonTerminal(acc.Name))  // (8) Signal 5 -- BE settle window (B165)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return true;
}
```

Helper body at L9382–L9386:

```csharp
private bool IsBeSlotNonTerminal(string accName)
{
    // CYC=1
    return _pendingFollowerBeSlots.ContainsKey(accName);
}
```

### Verification Criterion (positive — MUST match)

```
grep -n "IsBeSlotNonTerminal" src/PropTraderTools/CopyEngine.cs
```
Expected: at least two matches — the method definition (around L9382) and the call site in
`IsFollowerPositionBlocked` (around L3222).

```
grep -n "be-settle-pending" src/PropTraderTools/CopyEngine.cs
```
Expected: match at approximately L3225 (log string in Signal 5 block).

### Negative Verification

No prior form to be absent. Verifier confirms:

- `IsBeSlotNonTerminal` exists at approximately L9382 and returns
  `_pendingFollowerBeSlots.ContainsKey(accName)`.
- The Signal 5 block is present between Signal 4 (`HasActivePttBeTargets`, L3213) and Signal 6
  (`IsBeRetryArmedFlat`, L3231) in `IsFollowerPositionBlocked`.

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | `bool`-returning; no `throw` |
| JS-002 | `false` on slot-absent case; no null return |
| JS-021 | `_pendingFollowerBeSlots.ContainsKey` is O(1) lock-free read; no `lock()` |
| JS-042 | `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` is ASCII-only |
| JS-CYC | `IsBeSlotNonTerminal` CYC=1; `IsFollowerPositionBlocked` CYC=8 at B165 stage (9 after B166 — see VIOLATION-01 note above) |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L3222–L3229 and L9382–L9386; confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — `IsBeSlotNonTerminal` returns `bool`; no null
- [ ] **SCAN-03** JS-021 no-lock verified — grep `"lock("` in both regions; expect zero matches; `ContainsKey` is lock-free
- [ ] **SCAN-04** JS-042 ASCII-only verified — `"[PTT-COPY-GUARD] ghost-entry blocked (be-settle-pending): "` and all other strings are ASCII-only
- [ ] **SCAN-05** JS-CYC within budget — count branches in full `IsFollowerPositionBlocked` (L3182–L3241); **report actual count**; expected CYC=9 after B166 (VIOLATION-01; follow-on fix required)
- [ ] **SCAN-06** NT8 API constraints verified — `_pendingFollowerBeSlots.ContainsKey` is a `ConcurrentDictionary` read; no bare `Dictionary` used
- [ ] **SCAN-07** Signal continuity verified — Signal 5 block at L3222 is ordered after Signal 4 (L3213) and before Signal 6 (L3231); matches §4 signal map temporal ordering

---

## Ticket T7: B166-VERIFY

**Ticket ID:** B166-VERIFY
**Epic:** B166
**Type:** Verification (no code change)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Spec requirement:** B166 — two changes:
(A) flat-cancel path in `TryFireBePriceRetry` preserves `_bePriceRetryAttempts` sentinel (no `TryRemove`)
(B) `RunFollowerEvictPath` clears `_bePriceRetryAttempts` sentinel on confirmed position close

### Methods

- [`TryFireBePriceRetry`](src/PropTraderTools/CopyEngine.cs:7218) — flat-cancel path: **Confirmed lines:** L7242–L7250
- [`RunFollowerEvictPath`](src/PropTraderTools/CopyEngine.cs:2013) — sentinel clear: **Confirmed line:** L2022

### CYC

| Method | Before | After | Budget |
|--------|--------|-------|--------|
| `TryFireBePriceRetry` | 8 | 8 | ≤ 8 ✓ |
| `RunFollowerEvictPath` | 4 | 4 | ≤ 8 ✓ |

CYC unchanged in both methods: removing a `TryRemove` call from inside an `if` body does not
add or remove branches; adding a `TryRemove` statement (not an `if` branch) to
`RunFollowerEvictPath` does not change branch count.

> **VIOLATION-01 NOTE:** `IsFollowerPositionBlocked` reaches CYC=9 after B166 adds Signal 6
> (`if (IsBeRetryArmedFlat(...))` at L3231). The verifier MUST count McCabe branches in the
> full method body (L3182–L3241) and report the actual count. Expected: **9** (1 base + 8 `if`
> branches). This is a known P1 violation per `02-architecture-plan.md` §6 and
> `02-plan-review.md` §4. Remediation is tracked as follow-on ticket VIOLATION-01-FIX.

### Old Code — MUST NOT be present

**Change A (bug — flat-cancel path):** `_bePriceRetryAttempts.TryRemove` inside the flat-cancel
`if` block:

```csharp
// ABSENT inside flat-cancel block:
if (!StillAtNonBePrice(...) || IsFlat(...))
{
    _bePriceRetryAttempts.TryRemove(o.Account.Name, out _);  // BUG -- drops Signal 6 sentinel
    return;
}
```

### New Code — MUST be present

**Change A (fix):** flat-cancel path returns **without** clearing the sentinel at L7246–L7250:

```csharp
if (!StillAtNonBePrice(o.Account, o.Instrument, bePrice)                  // (7)
    || IsFlat(FindPosition(o.Account, o.Instrument)))                      // (8) B166: flat = no position to protect
{
    return; // B166: sentinel stays alive for Signal 6 (IsBeRetryArmedFlat)
}
```

**Change B (fix):** `RunFollowerEvictPath` clears sentinel at L2022:

```csharp
_bePriceRetryAttempts.TryRemove(accName, out _);   // B166: clear Signal 6 sentinel on confirmed position close
```

This line must appear after `_filledBeTargetCount.TryRemove(accName, out _)` (L2021) and
before `AbortDrainOnFill(accName)` (L2023).

### Verification Criterion (positive — MUST match)

**Change A:**
```
grep -n "B166" src/PropTraderTools/CopyEngine.cs
```
Expected: matches including the flat-cancel path comment at approximately L7242–L7244
(`"B166: flat-cancel path -- position already gone, do NOT clear sentinel here."` or equivalent)
and at L7247 (`"(8) B166: flat = no position to protect"`).

**Change B:**
```
grep -n "_bePriceRetryAttempts.TryRemove" src/PropTraderTools/CopyEngine.cs
```
Expected matches:
- L7231 — Working path (safe clear, position still live)
- L7253 — live-cancel fire path (retry will re-arm)
- L2022 — `RunFollowerEvictPath` (sentinel clear on confirmed position close)

Confirm L2022 is inside `RunFollowerEvictPath` by reading L2013–L2026.

### Negative Verification (MUST return no match)

Read L7242–L7250 (flat-cancel block in `TryFireBePriceRetry`). Confirm:

- `_bePriceRetryAttempts.TryRemove` does **NOT** appear inside this block.
- The block contains only the condition guard and a bare `return;`.

```
# Narrow check: _bePriceRetryAttempts.TryRemove must NOT appear at L7242-L7250.
# Verifier reads those lines and confirms absence.
```

### Jane Street Rules Satisfied

| Rule | Evidence |
|------|---------|
| JS-001 | Both `void`; no `throw` |
| JS-002 | `void`; not applicable |
| JS-021 | `ConcurrentDictionary.TryRemove` is lock-free; no `lock()` |
| JS-042 | `"B166: sentinel stays alive for Signal 6 (IsBeRetryArmedFlat)"` and all comment strings are ASCII-only |
| JS-CYC | `TryFireBePriceRetry` CYC=8; `RunFollowerEvictPath` CYC=4; both ≤ 8 |

### 7-Scan Checklist

- [ ] **SCAN-01** JS-001 no-throw verified — read L7242–L7250 and L2013–L2026; confirm no `throw`
- [ ] **SCAN-02** JS-002 no-null-return verified — both `void`; not applicable
- [ ] **SCAN-03** JS-021 no-lock verified — grep `"lock("` in both regions; expect zero matches; `TryRemove` is lock-free
- [ ] **SCAN-04** JS-042 ASCII-only verified — all comment and string literals in both regions are ASCII-only
- [ ] **SCAN-05** JS-CYC within budget — count branches in `TryFireBePriceRetry` (L7218–L7261); expect CYC=8; count branches in `RunFollowerEvictPath` (L2013–L2026); expect CYC=4. **Also count branches in `IsFollowerPositionBlocked` (L3182–L3241) and report actual count — expected 9 (VIOLATION-01)**
- [ ] **SCAN-06** NT8 API constraints verified — `_bePriceRetryAttempts.TryRemove` in `RunFollowerEvictPath` is a `ConcurrentDictionary` operation; no `lock()` used
- [ ] **SCAN-07** Signal continuity verified — Signal 6 (`IsBeRetryArmedFlat`) block is present at L3231 in `IsFollowerPositionBlocked`; `_bePriceRetryAttempts` sentinel is cleared ONLY by the three designated paths (Working, live-cancel, RunFollowerEvictPath); NOT cleared in the flat-cancel block

---

## Summary Table

| Ticket | Method(s) | Lines | Change Type | Positive Grep | Negative Grep |
|--------|-----------|-------|-------------|---------------|---------------|
| B161-VERIFY | `TryReplaceOnAtmCancel` | L947–L960 | Guard added | `IsFollowerPositionBlocked` at L957 | No direct call without guard |
| B162A-VERIFY | `TryFireBePriceRetry` | L7218–L7261 | `TryGetValue` → `TryRemove` at guard (4) | `TryRemove.*_pendingBePriceRetry` at L7224 | `TryGetValue.*_pendingBePriceRetry` — zero matches |
| B162B-VERIFY | `HasActivePttBeExits`, `StillAtNonBePrice` | L3110, L7187 | Prefix fix (remove trailing dash) | `"PTT-BE-Stop"` (no dash) in both methods | `"PTT-BE-Stop-"` with dash in `HasActivePttBeExits`, `StillAtNonBePrice` |
| B163-VERIFY | `IsPttEntryOrderCancelTrigger` | L924–L935 | AFP guard added | `AverageFillPrice` at L932 | No direct path from name-check to return without AFP guard |
| B164-VERIFY | `HasActivePttBeTargets` (new), `IsFollowerPositionBlocked` | L3147, L3213 | New helper + Signal 4 | `HasActivePttBeTargets`, `"PTT-BE-Target-"`, `active-be-targets` | Nothing; pure addition |
| B165-VERIFY | `IsFollowerPositionBlocked`, `IsBeSlotNonTerminal` | L3222, L9382 | Signal 5 added | `IsBeSlotNonTerminal`, `be-settle-pending` | Nothing; pure addition |
| B166-VERIFY | `TryFireBePriceRetry`, `RunFollowerEvictPath` | L7242, L2022 | Sentinel preservation + evict clear | `_bePriceRetryAttempts.TryRemove` at L2022; B166 comment in flat-cancel path | `_bePriceRetryAttempts.TryRemove` ABSENT inside flat-cancel block |

---

## Follow-On Tickets Required

| Ticket | Priority | Description |
|--------|----------|-------------|
| VIOLATION-01-FIX | P1 | Extract Signals 5+6 from `IsFollowerPositionBlocked` into `IsBeSettleOrRetrySignalActive(Account, Instrument)` helper. Parent CYC reduces from 9 to 8. Update in-code CYC comment at L3178. |
| CONCERN-02-RENAME | P3 | Rename `IsPttBeStopActiveState` → `IsPttBeOrderActiveState`. Update all call sites (HasActivePttBeExits, HasActivePttBeTargets, plus any others found). |

---

TICKETS_COMPLETE
