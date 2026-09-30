# PTT Deferred Backlog

## B32-LaneA block (2026-07-20)

### DW-B32-DEFERRED-01 — Pre-existing build errors
Status: NOT from this epic. 3 CS errors present before B32-LaneA, unchanged.

**Errors**:
- `AtrSizingEngine.cs(20,31)`: CS0234 — `NinjaTrader.NinjaScript.Indicators` not found (NT8 DLL absent on dev machine)
- `AtrSizingEngine.cs(24,36)`: CS0246 — `Indicator` type not found (same root cause)
- `CopyEngine.cs(680,22)`: CS8370 — nullable reference types (`Order?`) require C# 8+ (pre-existing from B27 T1; line 680 not touched by any B32-LaneA ticket)

These errors exist in the LSP-only `PropTraderTools.csproj`. NT8 compilation occurs via NT8's own Roslyn host at F5 time and is unaffected. All were present in the B28-LaneA and B29-LaneA baselines.

Action: Track separately; do not block B32-LaneA completion.

---

### DW-B32-DEFERRED-02 — ATM Target nudge not implemented
Status: Rejected. Architecture confirmed `acc.Change()` on ATM-owned Target slot orders is silently overridden by the NT8 ATM engine (same mechanism confirmed for Stop slot orders in B31 live test, DW-B32-07).

**Root cause detail**: NT8 ATM engine intercepts all `acc.Change()` calls on orders it owns (Stop1, Stop2, Target1, Target2, etc.). The engine re-applies its own managed price on the next ATM tick. No exception is thrown — the `order.StopPrice` / `order.LimitPrice` property reverts locally. This was confirmed by live test in B32 (stop flickering on chart, then snapping back). By structural symmetry in NT8's ATM architecture, Target slot orders are owned by the same ATM engine under the same OCO group and subject to the same silent rejection.

Action: If a future NT8 version exposes an ATM-native partial-exit API, revisit. Log as architectural constraint DW-B32-07 (already in `00-direct-repair-register.md` and `NT8_COMPILER_RULES.md` NT8-046/NT8-047).

---

### DW-B32-DEFERRED-03 — Limit path ATM bracket detection not added
Status: Out of scope. `TrimOneAccountLimit` / `FlattenOneAccountLimit` (limit exit paths) do not have the ATM bracket guard introduced in DW-B32-TRIM-CLOSE-01. These limit paths use `CancelStaleExitOrders` (HOTFIX-F4) instead, which cancels pre-existing PTT limit exit orders before posting a new one. This is sufficient for the limit path because:
1. Limit orders submitted by PTT carry PTT- signal names and are not part of the ATM OCO bracket.
2. The ATM bracket issue that triggered R-B32-03 is specific to the raw `CreateOrder(Market)` path (the bracket sees an unexpected fill and its cancel-then-close times out).

Action: Director review — if limit paths also require explicit ATM bracket detection in a future block, create a follow-up epic. The guard pattern from DW-B32-TRIM-CLOSE-01 can be applied symmetrically to `TrimOneAccountLimit` and `FlattenOneAccountLimit` with a new `IsAtmBracketActive` call.
