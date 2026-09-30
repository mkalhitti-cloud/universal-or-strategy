# UI Height Reference 001 — Trading Session Screenshot
**Captured**: 2026-07-09  
**File**: chart-trader-height-ref-001.md  
**Status**: ACTIVE REFERENCE — governs B10+ panel layout and number-sourcing decisions

---

## What the screenshot shows

Three ChartTrader panels visible (MES SEP26 × 3 charts) side by side.
NT Account Data window open top-left showing all connected accounts.

**Our panel at bottom of each ChartTrader** (all 3 visible):
- Row 1: `Apply Rule` button
- Row 2: `Copy OFF` toggle
- Row 3: `Trim 1/` | `Flatte` | `Canc` | `BE` — **clipped at bottom edge, labels truncated**

This confirms the height issue: action row is already getting cut at current resolution
with 3-4 charts open. With 5 accounts and taller Account Data panel it gets worse.

---

## Height constraint summary

| Scenario | Account Data height | ChartTrader headroom for our panel |
|---|---|---|
| 3 accounts | Smaller | ~3.5 rows visible |
| 5 accounts | Taller | ~2.5 rows visible |
| Worst case | Max | Action row may be half-clipped |

**Target**: top 3 rows always fully visible even at worst case (5 accounts).

---

## Number sourcing audit — what the screenshot reveals

### NT Account Data window columns (top-left of screenshot)
NT shows per-account:
- `Avg. price` — average entry price
- `P&L` — **this is what NT calls Unrealized P&L** (open position gain/loss live, e.g. `-$2.50`, `-$10.00`, `+$95.00`)
- `Close` — close price
- `Working Buy` / `Working Sell` — working orders
- `Quantity` — position size
- `Side` — long/short
- `Instrument`

### What we currently show in the follower dropdown rows
- **Account name** — from `Account.Name` ✅ NT native
- **Daily P&L** — from `AccountItem.RealizedProfitLoss` ✅ NT native (push event)
- **Multiplier** — our own field (1x–10x) ✅ our data, correct
- **ATM mode** — our own field (Inherit/Market/Named) ✅ our data, correct
- **CheckBox** — our own IsSelected ✅ our data, correct

### What `AccountItem.RealizedProfitLoss` actually is
`AccountItem.RealizedProfitLoss` = **closed trades P&L for the day** (what you locked in).
This is `$0.00` at start of day, goes negative/positive only as trades close.

### What the screenshot's "P&L" column in Account Data shows
The Account Data `P&L` column appears to be **unrealized P&L** (open position mark-to-market).
NT provides this via `AccountItem.UnrealizedProfitLoss`.

### Director's intent
> "follower menu numbers are tracking too" — confirming the live P&L per row is working.
> "display NT numbers always when possible instead of calculating them"

**Current state is already correct** — we use `AccountItem.RealizedProfitLoss` via the
NT push event (`Account.AccountItemUpdate`). We do our own formatting (`+$120.00`, `-$45.50`)
but the raw number IS NT's number. No custom calculation.

---

## Available NT AccountItem values we could display

NT exposes all of these via `account.Get(AccountItem.X, Currency.UsDollar)`:

| AccountItem | What it is | NT Account Data column |
|---|---|---|
| `RealizedProfitLoss` | Closed P&L today | "Daily P&L" / "Realized" |
| `UnrealizedProfitLoss` | Open position mark-to-market | "P&L" column in Account Data |
| `CashValue` | Account cash balance | "Cash" |
| `NetLiquidation` | Net liq value | "Net Liq" in Monitor |
| `BuyingPower` | Buying power remaining | N/A in Account Data |

**Current choice: `RealizedProfitLoss`** = daily closed P&L.
This is the correct choice for prop firm traders — it's the number that counts
toward your daily drawdown limit. This is what matters.

**NOT using `UnrealizedProfitLoss`** — open P&L fluctuates every tick and would be
visually noisy in a small 62px column. Also misleading — open P&L doesn't count
against your prop firm daily loss limit until the trade closes.

---

## Formatting: our code vs NT's formatting

**Our current code** (`UpdatePnl`):
```csharp
string sign  = value > 0 ? "+" : "";
DailyPnlText = sign + "$" + value.ToString("0.00");
// e.g.: "+$120.00", "-$45.50", "$0.00"
```

**NT Account Data formatting**: uses currency locale format, e.g. `$120.00`, `-$45.50`.

**Difference**: we add `+` prefix for positive values — NT doesn't. This is intentional
and better: makes positive/negative instantly scannable in a small column.
No change needed here.

---

## Open design questions (to discuss after trading session)

1. Should we show **realized** (current) or add **unrealized** as a secondary column?
   - Realized: what counts for prop firm rules ✅ keep
   - Unrealized: live open P&L — more relevant while in a trade, but visually noisy
   - Option: show realized normally, show unrealized only when position is open (swap)

2. Should the **follower name** be truncated to show a shorter account ID?
   - Currently: full `Account.Name` e.g. `PA-APEX-422136-05-...` — gets clipped
   - NT Account Data shows: `PA-APEX-422136-05-...` same truncation
   - With star-width column + ellipsis (B10-UI-01) this is handled ✅

3. Should any column show **position qty** (from `account.Positions`)?
   - Would show "3L" / "0" / "2S" alongside P&L — useful at a glance
   - NT already shows this in Account Data — is duplicating it valuable in our panel?

4. **Multiplier column** (30px) — currently shows "1", "2" etc as editable TextBox.
   This is our own data. No NT equivalent. Keep as-is.
