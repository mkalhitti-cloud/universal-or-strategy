# DW-B160 Tickets
<!-- block: DW-B160 -->
<!-- wave: B160 -->
<!-- phase: 3 -->
<!-- date: 2026-09-15 -->
<!-- produced-by: ptt-architect (Phase 3) -->
<!-- source-plan: docs/brain/B160/02-architecture-plan.md (REVIEW_PASS) -->
<!-- source-review: docs/brain/B160/02-plan-review.md (REVIEW_PASS) -->

---

## TICKET B160-T1 — FM-A Ghost Re-entry Guard Race Repair

### 1. Ticket ID

**B160-T1**

### 2. Spec Requirement IDs Satisfied

| Requirement | Description |
|-------------|-------------|
| REQ-1 | Three-signal guard in `IsFollowerPositionBlocked` (HasOpenPosition + HasActivePttBeExits + HasSettlingBeTargets) |
| REQ-2 | Signal 2: `HasActivePttBeExits` — pre-fill race window (PTT-BE-Stop-* Working/Accepted/Submitted) |
| REQ-3 | Signal 3: `HasSettlingBeTargets` — confirmed OCO-settle race (PTT-BE-Target-* CancelPending/CancelSubmitted, output.md L456-507) |
| REQ-4 | Zero new fields / no cached state |
| REQ-5 | Reason-tagged `[PTT-COPY-GUARD]` log at each guard return-true site in `IsFollowerPositionBlocked` |
| REQ-6 | V-01 resolution: `HasActivePttBeExits` CYC<=8 via extraction of `IsPttBeStopActiveState` |
| REQ-7 | V-02 resolution: `HasSettlingBeTargets` CYC<=8 via extraction of `IsPttBeTargetSettlingState` |
| REQ-8 | 6 xUnit [Fact] test stubs specified |
| REQ-9 | Stale comment DW-B159-K5 removed from `IsFollowerPositionBlocked` header (line 5273/5294 reference) |
| REQ-10 | Deferred items DW-B159-K1 and K5 close criteria met |

### 3. Scope Lock

**Implement changes in `CopyEngine.cs` ONLY. Do NOT modify any other file.**

Target path: `src/PropTraderTools/CopyEngine.cs`

---

### 4. Method Signatures

All methods reside inside the `CopyEngine` class.

#### 4.1 `IsPttBeStopActiveState` (NEW — private static)

```csharp
private static bool IsPttBeStopActiveState(OrderState s)
```

Expression body. Place adjacent to `HasActivePttBeExits`.

#### 4.2 `IsPttBeTargetSettlingState` (NEW — private static)

```csharp
private static bool IsPttBeTargetSettlingState(OrderState s)
```

Expression body. Place adjacent to `HasSettlingBeTargets`.

#### 4.3 `HasActivePttBeExits` (NEW — internal)

```csharp
internal bool HasActivePttBeExits(Account acc, Instrument instr)
```

Place adjacent to `IsFollowerPositionBlocked` (~L3080).

#### 4.4 `HasSettlingBeTargets` (NEW — internal)

```csharp
internal bool HasSettlingBeTargets(Account acc, Instrument instr)
```

Place adjacent to `HasActivePttBeExits`.

#### 4.5 `IsFollowerPositionBlocked` (MODIFIED — internal)

```csharp
internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)
```

Existing method at L3074. Modify in place. Remove stale comment at L3069-3073 and replace with B160 header.

#### 4.6 `ShouldSkipFollower` (MODIFIED — log removal only, private)

```csharp
private bool ShouldSkipFollower(
    Account acc,
    NinjaTrader.Cbi.Instrument instr,
    OrderAction currentAction,
    OrderAction lastAction,
    bool hasLastDirection
)
```

Existing method at L3086. Remove the `Output.Process` block at L3097-3100 ONLY. No other changes.

---

### 5. CYC Targets (REVIEW_PASS authoritative values)

| Method | CYC | Hard Limit | Note |
|--------|-----|------------|------|
| `IsPttBeStopActiveState` | 3 | 8 | base(1) + \|\|(+1) + \|\|(+1) |
| `IsPttBeTargetSettlingState` | 2 | 8 | base(1) + \|\|(+1) |
| `HasActivePttBeExits` | **8** | 8 | base(1)+acc-null(2)+instr-null(3)+foreach(4)+name-null-if(5)+name-\|\|-clause(6)+instr-check(7)+state-call(8). At hard limit — PASS. Record CYC=8 in compliance comment, NOT 7. |
| `HasSettlingBeTargets` | **8** | 8 | Same structure as above. Record CYC=8 in compliance comment, NOT 7. |
| `IsFollowerPositionBlocked` | 6 | 8 | base(1)+acc-null(2)+instr-null(3)+HasOpenPosition-branch(4)+HasActivePttBeExits-branch(5)+HasSettlingBeTargets-branch(6) |
| `ShouldSkipFollower` | 4 | 8 | Unchanged — log removal is not a branch |

---

### 6. Implementation Notes

#### 6.1 `acc.Orders.ToList()` Snapshot Pattern

Use the existing confirmed pattern from `StillAtNonBePrice` (L7016):

```csharp
foreach (var o in acc.Orders.ToList())   // snapshot live collection, no lock
```

This is the canonical AddOnBase-safe enumeration. No `lock()` anywhere.

#### 6.2 Name Prefix Filters

| Method | Prefix | StringComparison |
|--------|--------|-----------------|
| `HasActivePttBeExits` | `"PTT-BE-Stop-"` | `StringComparison.Ordinal` |
| `HasSettlingBeTargets` | `"PTT-BE-Target-"` | `StringComparison.Ordinal` |

Confirmed pattern: `o.Name.StartsWith("PTT-BE-Stop-", StringComparison.Ordinal)` at L7019.

#### 6.3 Instrument Filter

```csharp
if (o.Instrument?.FullName != instr.FullName) continue;
```

Confirmed pattern at L7021.

#### 6.4 State Predicate Delegation

Both helpers delegate state classification to the extracted predicates — no inline `||` chains in the loop body:

```csharp
// HasActivePttBeExits:
if (IsPttBeStopActiveState(o.OrderState)) return true;

// HasSettlingBeTargets:
if (IsPttBeTargetSettlingState(o.OrderState)) return true;
```

#### 6.5 `IsFollowerPositionBlocked` — Three-Signal Chain

Signal order is **fixed** (HasOpenPosition first, HasActivePttBeExits second, HasSettlingBeTargets third — no reordering):

```csharp
if (HasOpenPosition(acc, instr))          // Signal 1 — normal case (B159)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (has-position): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return true;
}
if (HasActivePttBeExits(acc, instr))      // Signal 2 — pre-fill race
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return true;
}
if (HasSettlingBeTargets(acc, instr))     // Signal 3 — confirmed OCO-settle race
{
    NinjaTrader.Code.Output.Process(
        "[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): "
            + acc.Name + "\n" + instr.FullName,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return true;
}
return false;
```

**ASCII-only log strings.** All characters 0x20-0x7E. No Unicode. No curly quotes.

#### 6.6 `ShouldSkipFollower` — Log Removal

Remove L3097-3100 (the three-line `Output.Process` block) exactly as written below. Replace the `if (IsFollowerPositionBlocked(acc, instr))` block with:

```csharp
if (IsFollowerPositionBlocked(acc, instr))
    return true;
```

No other changes to `ShouldSkipFollower`.

#### 6.7 Stale Comment Removal (DW-B159-K5)

Remove the existing `IsFollowerPositionBlocked` header at L3069-3073:

```
// B159 DW-B159-FM-A: returns true when acc already holds an open position on instr.
// Delegates to HasOpenPosition (line 5273) -- no duplicate logic.
// CYC=3: base(1) + acc-null guard(2) + instr-null guard(3).
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
// internal: accessible to xUnit via InternalsVisibleTo.
```

Replace with the B160 block (no stale line reference):

```csharp
// B160 DW-B160: extends FM-A guard with two additional signals to cover NT8 event-burst race.
// Signal 1 (HasOpenPosition): position Quantity > 0 (normal case, B159).
// Signal 2 (HasActivePttBeExits): PTT-BE-Stop-* in Working/Accepted/Submitted (pre-fill window).
// Signal 3 (HasSettlingBeTargets): PTT-BE-Target-* in CancelPending/CancelSubmitted
//   (CONFIRMED post-fill OCO-settle race -- output.md 2026-09-14 L456-507).
// CYC=6: base(1) + acc-null(2) + instr-null(3) + Signal1-branch(4) + Signal2-branch(5) + Signal3-branch(6).
// JS-001: no throw. JS-021: no lock. JS-042: ASCII-only. No DateTime.Now.
// Logging: reason-tagged log emitted at each return-true site.
// internal: accessible to xUnit via InternalsVisibleTo.
```

#### 6.8 Prohibited Patterns

- NO `DateTime.Now` — not used; no DateTime in any new/modified path
- NO `lock()` — use `acc.Orders.ToList()` snapshot
- NO Unicode in string literals — ASCII 0x20-0x7E only
- NO new instance fields, `ConcurrentDictionary`, or cached state
- NO `async void` methods
- NO inline `||` chains in state checks (delegate to predicates)

---

### 7. xUnit [Fact] Test Names (6 Required)

All methods are `internal`; accessible via `InternalsVisibleTo` (established in B159).

| # | [Fact] Name | Asserts |
|---|------------|---------|
| T1 | `HasActivePttBeExits_ReturnsTrue_WhenBeStopWorking` | Returns `true` when `acc.Orders` contains an order named `"PTT-BE-Stop-1"` with `OrderState.Working` on the matching instrument |
| T2 | `HasActivePttBeExits_ReturnsFalse_WhenBeStopFilled` | Returns `false` when `"PTT-BE-Stop-1"` has `OrderState.Filled` (terminal — not in Working/Accepted/Submitted) |
| T3 | `HasSettlingBeTargets_ReturnsTrue_WhenTargetCancelPending` | Returns `true` when `acc.Orders` contains an order named `"PTT-BE-Target-1"` with `OrderState.CancelPending` on the matching instrument |
| T4 | `HasSettlingBeTargets_ReturnsFalse_WhenTargetCancelled` | Returns `false` when `"PTT-BE-Target-1"` has `OrderState.Cancelled` (terminal — not in CancelPending/CancelSubmitted) |
| T5 | `IsFollowerPositionBlocked_ReturnsTrue_WhenSettlingTargetsPresent` | Returns `true` when `HasOpenPosition=false`, `HasActivePttBeExits=false`, `HasSettlingBeTargets=true` — the confirmed B160 race scenario (output.md L456-507) |
| T6 | `IsFollowerPositionBlocked_ReturnsFalse_WhenAllTerminal` | Returns `false` when `HasOpenPosition=false`, `HasActivePttBeExits=false`, `HasSettlingBeTargets=false` — no guard triggers, copy dispatches normally |

**Note (DW-B159-K2):** The 14 xUnit tests from B159 remain deferred pending test infrastructure setup. B160 adds these 6 tests. All test stubs are fully specified; implementation is contingent on the xUnit NT8-stub project setup from B159-K2.

---

### 8. Seven-Scan Checklist (Engineer Contract)

The engineer MUST complete all 7 scans before submitting Phase 4 completion.

| Scan | Description | Pass Criteria |
|------|-------------|---------------|
| **SCAN-01** | No new fields or `ConcurrentDictionary` added | `grep -n "private.*=\s*new\|ConcurrentDictionary\|private.*{.*get\|private.*{.*set" CopyEngine.cs` returns zero NEW hits vs B159 baseline |
| **SCAN-02** | No `lock()` usage | `grep -c "lock(" src/PropTraderTools/CopyEngine.cs` = 0 (or unchanged from B159 baseline — zero new `lock()`) |
| **SCAN-03** | All new/modified methods CYC <= 8 | Verify each: `IsPttBeStopActiveState` CYC=3, `IsPttBeTargetSettlingState` CYC=2, `HasActivePttBeExits` CYC=**8** (record 8 not 7 in source comment), `HasSettlingBeTargets` CYC=**8** (record 8 not 7 in source comment), `IsFollowerPositionBlocked` CYC=6, `ShouldSkipFollower` CYC=4. All <= 8. |
| **SCAN-04** | No `DateTime.Now` in executable paths | `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` returns zero hits in new/modified methods |
| **SCAN-05** | ASCII-only string literals in new/modified methods | Inspect all new log strings: `"[PTT-COPY-GUARD] ghost-entry blocked (has-position): "`, `"[PTT-COPY-GUARD] ghost-entry blocked (active-be-exits): "`, `"[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): "`, `"PTT-BE-Stop-"`, `"PTT-BE-Target-"`. All chars 0x20-0x7E. No Unicode, no curly quotes, no emoji. |
| **SCAN-06** | No `async void` methods | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` returns zero NEW hits |
| **SCAN-07** | All 6 [Fact] test methods present and passing | Test file contains all 6 method names listed in Section 7 exactly. All 6 pass when run against NT8 stubs. |

---

### 9. Acceptance Criteria

**Primary (P0 — must pass):**

1. **Ghost re-entry blocked during OCO-settle burst:** When PTT-BE-Stop-1/2/3 fill and `acc.Positions.Quantity` drains to 0 while PTT-BE-Target-1/2/3 are in `CancelPending`/`CancelSubmitted`, a new leader entry must NOT produce a second-cycle bracket on any follower account.
2. **`[PTT-COPY-GUARD]` log lines appear with reason tags:** Exactly one of `(has-position)`, `(active-be-exits)`, or `(settling-be-targets)` must appear in the log for each blocked copy dispatch. Zero untagged `[PTT-COPY-GUARD]` lines allowed after B160.
3. **Stale comment removed:** `IsFollowerPositionBlocked` header no longer references "line 5273" or "line 5294". B160 comment block replaces it exactly.
4. **All 6 [Fact] tests pass:** `HasActivePttBeExits_ReturnsTrue_WhenBeStopWorking`, `HasActivePttBeExits_ReturnsFalse_WhenBeStopFilled`, `HasSettlingBeTargets_ReturnsTrue_WhenTargetCancelPending`, `HasSettlingBeTargets_ReturnsFalse_WhenTargetCancelled`, `IsFollowerPositionBlocked_ReturnsTrue_WhenSettlingTargetsPresent`, `IsFollowerPositionBlocked_ReturnsFalse_WhenAllTerminal`.

**SIM Verification Criteria (post-merge):**

| Scenario | Expected Output |
|----------|----------------|
| QX→BE-All burst on 3-contract position, new leader entry during OCO-settle window | `[PTT-COPY-GUARD] ghost-entry blocked (settling-be-targets): Sim102\nMES DEC26` (and Sim103, Sim104). Zero new Stop1/Target1 brackets on followers. |
| Open position on Sim102, new leader entry | `[PTT-COPY-GUARD] ghost-entry blocked (has-position): Sim102\nMES DEC26` |
| No open position, no PTT-BE orders active, new leader entry | Zero `[PTT-COPY-GUARD]` lines. Copy dispatched normally. |

**Deferred Item Closure:**

| Item | Closes When |
|------|-------------|
| DW-B159-K1 (P0): SIM gate re-run | B160 merged + SIM pass with `[PTT-COPY-GUARD]` hits confirmed |
| DW-B159-K5 (P3): Stale comment | B160 merged (stale line reference removed per Section 6.7) |

---

### 10. File Location Summary

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY — all changes in this file only |

---

*Tickets produced by ptt-architect. Phase 3. Wave B160. Date 2026-09-15.*
*Source plan: docs/brain/B160/02-architecture-plan.md (REVIEW_PASS).*
*Source review: docs/brain/B160/02-plan-review.md (REVIEW_PASS).*
