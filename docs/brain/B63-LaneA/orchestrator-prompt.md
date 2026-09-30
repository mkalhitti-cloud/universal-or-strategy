# B63-LaneA Orchestrator Prompt

## Block: B63 — Gate B bracket state gap (ATM Target1 leaks into DispatchCopy at Accepted state)

**Priority: P0 — fires on every ATM order. Must be fixed before B62.**

**DW item**: DW-B63-01 (live confirmed 2026-08-11):
When a leader places a limit order with an ATM strategy (e.g. "MES $200 SL4"), NT8 fires
`Target1` bracket orders at `OrderState.Accepted` BEFORE `OrderState.Working`.
`IsWorkingBracket` only checks `OrderState.Working`, so `Target1` (Limit type, Name="Target1")
passes Gate B unchecked at `Accepted` state, falls through to `DispatchCopy`, and gets
emitted as a spurious `PTT-Copy` Sell Limit on the follower — one per partial fill of the
leader's entry. The follower accumulates ghost close orders it never placed.

**Root cause in code** (line 811–813):
```csharp
private static bool IsWorkingBracket(Order order)
{
    return order.OrderState == OrderState.Working && IsBracketLegStatic(order);
}
```
`OrderState.Accepted` is not tested. `Target1` fires `Accepted` 100–200ms before `Working`.

**NT8 API evidence** (NT8_FULL_REFERENCE.md line 941–942):
> "OrderState.Accepted — Order is accepted by the broker or exchange"
> ALSO: "In real-time, some stop orders may only reach Accepted state if they are simulated/
> held on a brokers server." (line 1005)
This means bracket orders in sim may ONLY ever fire Accepted, never Working.

---

## PIPELINE CHAIN (all 7 phases mandatory — none skippable — none combinable)

```
Ph1  ptt-architect       -> docs/brain/B63-LaneA/02-architecture-plan.md
Ph2  ptt-plan-reviewer   -> docs/brain/B63-LaneA/02-plan-review.md       (gate: REVIEW_PASS)
Ph3  ptt-architect       -> docs/brain/B63-LaneA/04-tickets.md
Ph3.5 ptt-ticket-reviewer -> docs/brain/B63-LaneA/04-ticket-review.md   (gate: TICKET_REVIEW_PASS)
Ph4a ptt-engineer        -> src .cs edits + docs/brain/B63-LaneA/ticket-1-completion.md
Ph4b ptt-verifier        -> docs/brain/B63-LaneA/ticket-1-verification.md (gate: VERIFY_PASS)
Ph5  ptt-plan-reviewer   -> docs/brain/B63-LaneA/05-final-review.md
                         -> docs/brain/B63-LaneA/06-deferred-backlog.md
```

---

## EXACT CHANGE REQUIRED — 1 METHOD, 1 LINE

**File**: `src/PropTraderTools/CopyEngine.cs`

**Current** (line 811–813):
```csharp
// CYC=1. Gate predicate for bracket change detection in OnOrderUpdate.
private static bool IsWorkingBracket(Order order)
{
    return order.OrderState == OrderState.Working && IsBracketLegStatic(order);
}
```

**Required replacement**:
```csharp
// CYC=1. Gate predicate for bracket detection in OnOrderUpdate.
// B63: Accepted added -- NT8 bracket orders fire Accepted before (or instead of) Working.
// NT8_FULL_REFERENCE.md line 1005: "some stop orders may only reach Accepted state".
// Extending to Accepted is safe: SyncFollowerBracket price-delta guard absorbs double-fire.
// JS-021: no lock. JS-001: no throw.
private static bool IsWorkingBracket(Order order)
{
    return (order.OrderState == OrderState.Working
            || order.OrderState == OrderState.Accepted)
           && IsBracketLegStatic(order);
}
```

**No other changes to `OnOrderUpdate`, `HandleBracketChange`, or `SyncFollowerBracket`.**
The price-delta guard inside `SyncFollowerBracket` (`Math.Abs(newPrice - currentPrice) < tickSize`)
already absorbs any double-fire when both `Accepted` and `Working` events arrive for the same
bracket order. The second call is a no-op.

---

## SAFETY ANALYSIS (architect must verify all 4 points)

**Point 1 — Does adding `Accepted` accidentally catch entry orders?**
`IsBracketLegStatic` returns true ONLY for names starting with "Stop", "Target", or "PTT-".
Leader entry orders have names like "Entry" (Chart Trader) or signal names — none start with
those prefixes. Gate B cannot accidentally catch a leader entry order. SAFE.

**Point 2 — Does adding `Accepted` catch follower PTT-Copy orders?**
`OnOrderUpdate` is gated by Gate 2 (`e.Order.Account.Name == rule.MasterAccount?.Name`).
Follower account orders never reach Gate B. SAFE.

**Point 3 — Is the double-fire (Accepted + Working for same bracket) safe?**
`SyncFollowerBracket` line 850: `if (Math.Abs(newPrice - currentPrice) < tickSize) return;`
Second call has identical price — delta is 0, guard fires, no `acc.Change()` called. SAFE.

**Point 4 — New bracket leg: no matching follower order exists yet**
When a fresh bracket fires (entry just placed, no follower bracket exists),
`FindFollowerBracketOrder` returns null → `SyncFollowerBracket` returns immediately at line 846.
No action taken. SAFE.

---

## TESTS REQUIRED (4 new [Fact] tests, tag T_B63_01 through T_B63_04)

All tests are static-method direct tests — no reflection needed.
`IsWorkingBracket` is `private static` — must be called via reflection or made `internal static`.
**Engineer must make `IsWorkingBracket` internal for testability (same pattern as `IsExitSignalName`).**

Add `internal` modifier to `IsWorkingBracket`:
```csharp
internal static bool IsWorkingBracket(Order order)  // was: private static
```

**T_B63_01** — `IsWorkingBracket_Working_TargetName_ReturnsTrue` (regression)
- Arrange: fake Order stub with `OrderState=Working`, `Name="Target1"`.
- Assert: `CopyEngine.IsWorkingBracket(order)` returns `true`.
- Purpose: confirm existing Working behavior unchanged.

**T_B63_02** — `IsWorkingBracket_Accepted_TargetName_ReturnsTrue` (new behavior — the fix)
- Arrange: fake Order stub with `OrderState=Accepted`, `Name="Target1"`.
- Assert: `CopyEngine.IsWorkingBracket(order)` returns `true`.
- Purpose: confirm ATM bracket at Accepted is now caught by Gate B.

**T_B63_03** — `IsWorkingBracket_Accepted_EntryName_ReturnsFalse` (entry orders not caught)
- Arrange: fake Order stub with `OrderState=Accepted`, `Name="Entry"`.
- Assert: `CopyEngine.IsWorkingBracket(order)` returns `false`.
- Purpose: confirm leader entry orders at Accepted are NOT diverted to HandleBracketChange.

**T_B63_04** — `IsWorkingBracket_Submitted_TargetName_ReturnsFalse` (Submitted not caught)
- Arrange: fake Order stub with `OrderState=Submitted`, `Name="Target1"`.
- Assert: `CopyEngine.IsWorkingBracket(order)` returns `false`.
- Purpose: confirm only Accepted/Working are caught, not all states.

### Test order stub pattern (use same stub approach as existing tests)
NT8 `Order` is a sealed class — tests create a minimal stub via subclass or reflection.
Check the existing test file for how `IsExitSignalName` tests pass an `Order` or use the
`string name` directly. For `IsWorkingBracket`, the method takes `Order order` — look at
how prior tests handle NT8 sealed types.
If `Order` cannot be instantiated in test context, use a test-double wrapper:
check `CopyEngineTests.cs` for existing patterns before writing new stubs.

---

## JANE STREET / OKF COMPLIANCE

| Rule | Check |
|------|-------|
| JS-021 | No `lock()` added — method is static, no state |
| JS-001 | No `throw` — method is a pure boolean predicate |
| CYC | `IsWorkingBracket` CYC stays 1 (one compound condition, single return) |
| ASCII-only | No new string literals |
| xUnit only | All 4 tests use `[Fact]` |

---

## BRAIN ARTIFACT CHECKLIST

```
docs/brain/B63-LaneA/02-architecture-plan.md      <- Ph1
docs/brain/B63-LaneA/02-plan-review.md            <- Ph2  (must end: REVIEW_PASS)
docs/brain/B63-LaneA/04-tickets.md                <- Ph3
docs/brain/B63-LaneA/04-ticket-review.md          <- Ph3.5 (must end: TICKET_REVIEW_PASS)
docs/brain/B63-LaneA/ticket-1-completion.md       <- Ph4a (must contain git commit hash)
docs/brain/B63-LaneA/ticket-1-verification.md     <- Ph4b (must end: VERIFY_PASS)
docs/brain/B63-LaneA/05-final-review.md           <- Ph5
docs/brain/B63-LaneA/06-deferred-backlog.md       <- Ph5
```

---

## WORKSPACE RULES

- SRC CODE BAN: ptt-architect and ptt-plan-reviewer MUST NOT edit any `.cs` file.
- ptt-engineer is the ONLY mode permitted to touch `.cs` files.
- Workspace: `C:\WSGTA\universal-or-strategy` (main branch only).
- After any `.cs` edit: run `powershell -File scripts\verify_links.ps1 -Fix`
  then commit: `git add src/PropTraderTools/ && git commit -m "fix(ptt): B63 -- ..."`
- NT8 API reference: grep `docs/standards/NT8_FULL_REFERENCE.md` before any NT8 API claim.
- Jane Street rules: JS-021 no lock(), JS-001 no throw in hot path, CYC <= 8, ASCII-only, xUnit [Fact] only.
