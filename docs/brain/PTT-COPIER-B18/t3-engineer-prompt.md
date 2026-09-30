# PTT-COPIER-B18 T3 — Engineer + Verifier Prompt
# Paste into a NEW ptt-engineer session (minimal phases: engineer → verifier only)
# Date: 2026-07-15
# Urgency: P1 — live trading morning session depends on clean cancel

---

You are the **ptt-engineer** for PTT-COPIER-B18 Ticket 3.

## Context

B18 T1 and T2 are already deployed and verified. Core copy trading is working.
One P1 runtime bug remains that blocks clean order management for live trading:
follower copy orders get stuck in `Cancel pending` and cannot be cancelled even via
NT8 Control Center right-click. This must be fixed before the morning live session.

**WAVE WORKSPACE**: `c:\WSGTA\universal-or-strategy`
**NT8 ADDONS FOLDER**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\`
**MANIFEST**: `c:\WSGTA\universal-or-strategy-director\docs\brain\PTT-COPIER-B18\manifest.json`
**NT8 COMPILER RULES**: `c:\WSGTA\universal-or-strategy\docs\standards\NT8_COMPILER_RULES.md` — read before editing

## Authorization

**FILE TO MODIFY**: `src/PropTraderTools/CopyEngine.cs` ONLY
**BANNED FILES** (do NOT touch): `TradeCopierPanel.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`, `AtrSizingEngine.cs`

---

## Defect: DW-B18-CANCEL-01

### Root Cause 1 — CancelPendingEntries skips Initialized orders

[`CancelPendingEntries`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:984):

```csharp
if (order.OrderState != OrderState.Working)
    continue;   // BUG: skips Initialized follower copy orders
```

Follower copy orders created by `SendCopy` start in `OrderState.Initialized` (NT8 state
before the sim engine acknowledges the order). If the PTT Cancel button is clicked while
the follower order is still `Initialized`, the guard skips it. The order then transitions
to `Working` with no cancel outstanding — permanently stuck.

### Root Cause 2 — SendCopy passes DateTime.MaxValue as expiry

[`SendCopy`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:746):

```csharp
DateTime.MaxValue,   // BUG: GTC-equivalent — sim engine cannot cleanly cancel these
```

`TimeInForce.Day` combined with `DateTime.MaxValue` expiry creates an order the NT8 sim
engine treats as never-expiring. When such an order is in `Initialized` state, NT8's own
cancel mechanism (Control Center right-click → Cancel All) cannot reach it. Requires a
sim connection reset to clear. Unacceptable for pre-live-session cleanup.

---

## Fix — Two Changes in CopyEngine.cs

### Change 1 — CancelPendingEntries L984

Find this exact block:
```csharp
                    if (order.OrderState != OrderState.Working)
                        continue;
```

Replace with:
```csharp
                    // B18 T3: DW-B18-CANCEL-01 -- also cancel Initialized and PendingSubmit.
                    // Follower copy orders start as Initialized before sim engine acknowledges.
                    // Skipping them caused orders to get permanently stuck after Cancel click.
                    if (order.OrderState != OrderState.Working &&
                        order.OrderState != OrderState.Initialized &&
                        order.OrderState != OrderState.PendingSubmit)
                        continue;
```

### Change 2 — SendCopy L746

Find this exact line inside `SendCopy` (the `follower.CreateOrder` call arguments):
```csharp
                    DateTime.MaxValue,
```

Replace with:
```csharp
                    DateTime.Now.AddDays(1),   // B18 T3: Day order with real expiry -- not GTC-stuck
```

---

## Steps

1. Read `src/PropTraderTools/CopyEngine.cs` to confirm exact line numbers
2. Apply Change 1 at `CancelPendingEntries` (around L984)
3. Apply Change 2 at `SendCopy` (around L746) — there is only ONE `DateTime.MaxValue` inside `SendCopy`; do not change other occurrences in other methods
4. Run `dotnet build` — confirm zero errors
5. Copy DLL to NT8 AddOns folder
6. Write `docs/brain/PTT-COPIER-B18/ticket-3-completion.md` confirming both changes and build pass
7. Update `docs/standards/NT8_ADDON_KNOWLEDGE.md` — append under Testing Session Round 2:
   `### DW-B18-CANCEL-01 — CLOSED (B18 T3). CancelPendingEntries now cancels Initialized+PendingSubmit. SendCopy expiry changed from DateTime.MaxValue to DateTime.Now.AddDays(1).`

---

## Verifier Steps (run after engineer completion)

1. Read `ticket-3-completion.md`
2. Read `CopyEngine.cs` — confirm:
   - `CancelPendingEntries` filter includes `Initialized` and `PendingSubmit`
   - `SendCopy` uses `DateTime.Now.AddDays(1)` not `DateTime.MaxValue`
   - No other `DateTime.MaxValue` occurrences changed (only the one in `SendCopy`)
   - No banned files touched
3. Confirm build passes
4. Write `docs/brain/PTT-COPIER-B18/ticket-3-verification.md` with VERIFY_PASS or VERIFY_FAIL + findings
5. Update `docs/brain/PTT-COPIER-B18/manifest.json` — set `verifier_T3.status` to `"complete"`

---

## Success Gate

After F5 in NinjaTrader:
- Place a Buy Limit order on Sim101 (leader) → copy fires on SimApexSim_02 (follower)
- Immediately click PTT Cancel button
- Follower order disappears from Orders tab — does NOT stay stuck as Cancel pending
- Repeat: click Cancel All from Control Center — all orders clear cleanly
