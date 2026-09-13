# NT8 Full Reference — PTT Scope Index
# This file is a navigation index into confirmed NT8 API facts used by PTT.
# It is NOT a full NT8 API dump. Agents should read the relevant section and stop.
# Last updated: 2026-09-11

---

## How to use this file

1. Search for the API name you need (Ctrl+F in your reader).
2. If it is listed, use the CONFIRMED status and the documented form — do not re-investigate.
3. If it is not listed, check the NT8 NinjaScript documentation directly
   and add a new entry here after confirmation.

---

## Line 2245-2248 — StartAtmStrategy (AddOnBase)

**CONFIRMED AddOnBase available.**

```csharp
// Object overload (preferred — use when AtmStrategy object captured from panel)
NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(AtmStrategy atmStrategy, Order entryOrder);

// String overload (fallback — use when only template name string is available)
NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(string atmTemplateName, Order entryOrder);
```

Requirements:
- Both are STATIC methods on `NinjaTrader.NinjaScript.AtmStrategy`.
- The `entryOrder` argument MUST have been created with order name `"Entry"`.
- Do NOT pass null for `atmStrategy` in the object overload — null-guard required.
- `AtmStrategyCreate()` is NOT the same and is StrategyBase-only. Do not confuse them.

---

## AtmStrategyCreate — StrategyBase-ONLY (BANNED in AddOnBase)

**CONFIRMED NOT available in AddOnBase.**
Compile error if called from AddOnBase context.
Do not use. Use `StartAtmStrategy` (static) instead.

---

## AtmStrategyChangeStopTarget — StrategyBase-ONLY (BANNED in AddOnBase)

**CONFIRMED NOT available in AddOnBase.**
Correct AddOnBase pattern for changing ATM bracket prices:
`account.Cancel(bracketOrder)` → `account.CreateOrder(...)` → `Submit(newOrder)`.

---

## Account.Change — AddOnBase available, silent no-op on ATM brackets

**CONFIRMED available in AddOnBase** but empirically confirmed to be a silent no-op
when called on orders owned by an NT8 ATM strategy (confirmed B129 SIM gate).
Do not use for ATM bracket price changes. Use Cancel + CreateOrder + Submit.

---

## account.PositionUpdate — AddOnBase available

**CONFIRMED available.**
```csharp
account.PositionUpdate += OnFollowerPositionUpdate;   // in Subscribe()
account.PositionUpdate -= OnFollowerPositionUpdate;   // in Unsubscribe()
```
Handler signature: `private void Handler(object sender, PositionEventArgs e)`
Handler must be synchronous void.

---

## account.OrderUpdate — AddOnBase available

**CONFIRMED available.** Primary event for copy dispatch in PTT.
Fires on NT8 account background thread.

---

## account.Cancel / account.CreateOrder / Submit — AddOnBase available

**CONFIRMED available.** Correct pattern for order bracket management in AddOnBase.

---

*For full NT8 API scope see NinjaTrader 8 NinjaScript documentation.*
*PTT-confirmed items only — last verified 2026-09-11.*
*Cross-reference: docs/standards/NT8_ADDON_KNOWLEDGE.md, docs/standards/RULES_CATALOG.md Section 2*
