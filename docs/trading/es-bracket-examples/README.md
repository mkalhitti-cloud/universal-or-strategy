# WSGTA ES Bracket Examples — NinjaTrader ATM Settings

Source: Wall Street Global Trading Academy (WSGTA) video screenshots
Instrument: ES 09-25, 5-Minute chart
ATM Strategy name: **ES 4sl 2_trail**

---

## Image 1 — ES 4sl 2_trail Strategy Parameters (Main Dialog)

**File**: `es-bracket-strategy-params.png`

| Field | Value |
|---|---|
| Order quantity | 2 |
| TIF | GTC |
| Parameter type | Ticks |

### Targets

| | Quantity | Stop loss | Profit | Stop strategy |
|---|---|---|---|---|
| Target 1 | 1 | 16 ticks | 8 ticks | Tier Trail |
| Target 2 | 1 | 16 ticks | 0 ticks | Tier Trail |

### More Settings
| Setting | Value |
|---|---|
| Chase limit (t) | 1 |
| Chase | ☐ unchecked |
| Chase if touch | ☑ checked |
| Reverse at stop | ☐ unchecked |
| Reverse at target | ☐ unchecked |
| Target chase | ☐ unchecked |
| Stop limit for stop loss | ☐ unchecked |
| MIT for profit | ☐ unchecked |
| Shadow strategy | None |

---

## Image 2 — Tier Trail Stop Strategy Parameters (Inner Dialog)

**File**: `es-bracket-tier-trail-stop-params.png`

### Auto Breakeven (t)
| Setting | Value |
|---|---|
| Profit Trigger | 8 ticks |
| Plus | 0 ticks |

### Auto Trail (t)
Selected: **3 Step**

| Step | Stop loss | Profit Trigger | Frequency |
|---|---|---|---|
| Step 1 | 8 ticks | 12 ticks | 2 |
| Step 2 | 6 ticks | 16 ticks | 2 |
| Step 3 | 4 ticks | 20 ticks | 1 |

### Simulated Stop
| Setting | Value |
|---|---|
| Volume Trigger | 0 |
| Enabled | ☐ unchecked |

---

## Notes

- ES is **50× multiplier** — 1 tick = $12.50, 1 point = $50
- Stop = 16 ticks = 4 points = $200/contract risk
- T1 profit = 8 ticks = 2 points = $100/contract
- T2 profit = 0 ticks (runner — trails via Tier Trail until stopped out)
- BE triggers at +8 ticks profit, moves stop to entry + 0 (breakeven)
- The trail tightens in 3 steps as price moves further in profit
- Total qty = 2: half exits at T1 (8T), half runs with trail

## How to Save the Images

Drop the two screenshots directly into this folder:
```
docs/trading/es-bracket-examples/es-bracket-strategy-params.png
docs/trading/es-bracket-examples/es-bracket-tier-trail-stop-params.png
```
