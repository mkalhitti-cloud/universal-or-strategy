# WSGTA Futures Playbook — Trading Rules Cheat Sheet
*Source: WSGTA - Futures Playbook-2.pdf*
*Saved: 2026-07-16*

---

## Chart Types Used
- 1 min, 5 min, 15 min, 30 min

---

## 1. The TREND Trade

| Instrument | Entry Criteria | Entry Type | Stop Loss | Exit Criteria |
|------------|---------------|------------|-----------|---------------|
| ES (5min) | No more than 2.5 points between 9 and 15 EMA | LIMIT | 4 points past 2nd entry | 1 contract at 2/3 points |
| NQ (15min) | No more than 10 points between 9 and 15 EMA | LIMIT | 5 points on 9 EMA | 1 contract at 10 points |
| CL (30min) | No more than 10 ticks between 9 and 15 EMA | — | 10 ticks past 2nd entry | 1 contract at 5 ticks |
| GC | 20 ticks | — | — | — |

**Notes:**
- 9 EMA: 1 contract, no stop
- 15 EMA: remaining contracts
- Trend trade NOT recommended in NQ (15 EMA would be a new trade)
- Hold until end of day OR until trend is broken
- Remaining contracts managed per your risk

---

## 2. Regular Moving Average Trade (RMA)

*EMAs: 30, 65, 200 + Pivot Points or VWAP*

| Instrument | Entry Criteria | Entry Type | Stop Loss | Exit (Scale) |
|------------|---------------|------------|-----------|--------------|
| ES (5min) | 10 points | LIMIT | 4 points | 1 contract at 2/3 pts (3/4) |
| NQ (15min) | 30–50 points | LIMIT | 10 points | 1 contract at 10 pts (15/20) |
| CL (30min) | 25–30 ticks | — | 10 ticks | 1 contract at 5 ticks (10/15) |
| GC | 20 ticks | — | — | — |

*Additional contracts managed per your risk*

---

## 3. Far From Moving Average Trade (FFMA)

*RSI > 80 Short / RSI < 20 Long — then switch to 1 min chart, buy/sell when green/red bar appears*

| Instrument | Entry Criteria | Entry Type | Stop Loss | Exit (Scale) |
|------------|---------------|------------|-----------|--------------|
| ES (5min) | 10 points | MKT (as soon as price begins to move toward EMAs) | 4 points | 1 contract at 2/3 pts (3/4) |
| NQ | 40–50 points | MKT | 10 points | 1 contract at 10 pts (15/20) |
| CL | 25–30 ticks | MKT | 10 ticks | 1 contract at 5 ticks (10/15) |
| GC | 20 ticks | MKT | — | — |

*Additional contracts managed per your risk*

---

## 4. The MOMO Trade

| Instrument | Range | Entry Type | Execution | Stop Loss | Exit (Scale) |
|------------|-------|------------|-----------|-----------|--------------|
| ES (5min) | 2 point range | Stop Mkt | 2 ticks above/below | .5 point above/below | 1 contract at 2/3 pts (3/4) |
| NQ (5min) | 10 point range | — | 1 point above/below | 3 points above/below | 1 contract at 10 pts (15/20) |
| CL (5min) | 10 tick range | — | 2 ticks above/below | 3 ticks above/below | 1 contract at 5 ticks (10/15) |
| GC | 20 ticks | — | — | — | — |

*Additional contracts managed per your risk*

---

## 5. The BASE Trade

| Instrument | Range | Entry Type | Stop Loss | Exit (Scale) |
|------------|-------|------------|-----------|--------------|
| ES (5min) | 2 point range | Stop Mkt | .5 point above/below | 1 contract at 2/3 pts (3/4) |
| NQ (5min) | 10 point range | — | 3 points above/below | 1 contract at 10 pts (15/20) |
| CL (5min) | 10 tick range | — | 3 ticks above/below | 1 contract at 5 ticks (10/15) |
| GC | 20 ticks | — | — | — |

*Additional contracts managed per your risk*

---

## 6. Double Bottom / Double Top Trade (DB / DT)

| Instrument | Setup | Entry Type | Stop Loss | Exit (Scale) |
|------------|-------|------------|-----------|--------------|
| ES (5min) | Must move 10 points from prior high/low | LIMIT — buy at 2nd bottom / sell at 2nd top | 4 points | 1 contract at 2/3 pts (3/4) |
| NQ (5min) | Must move 30+ points from prior high/low | LIMIT | 10 points | 1 contract at 10 pts (15/20) |
| CL (5min) | Must move 10 ticks from prior high/low | — | 10 ticks | 1 contract at 5 ticks (10/15) |
| GC | Must move 10 ticks from prior high/low | — | 20 ticks | — |

*Additional contracts managed per your risk*

---

## Instrument Reference

| Instrument | Full Name | Tick Size | $ per Tick | Notes |
|------------|-----------|-----------|------------|-------|
| ES | E-mini S&P 500 | 0.25 pts | $12.50 | — |
| MES | Micro E-mini S&P 500 | 0.25 pts | $1.25 | 1/10 ES |
| NQ | E-mini Nasdaq 100 | 0.25 pts | $5.00 | — |
| MNQ | Micro E-mini Nasdaq 100 | 0.25 pts | $0.50 | 1/10 NQ |
| CL | Crude Oil | 0.01 pts | $10.00 | — |
| GC | Gold | 0.10 pts | $10.00 | — |
| MGC | Micro Gold | 0.10 pts | $1.00 | 1/10 GC |
