---
name: polymarket-arbitrage-bot
description: TypeScript bot automating the dump-and-hedge strategy on Polymarket's 15-minute Up/Down markets (BTC, ETH, SOL, XRP).
---

# Polymarket Arbitrage Bot

Bot automating the **dump-and-hedge** strategy on Polymarket's 15-minute markets.

## Strategy Flow

1. **Discovery**: Gamma API finds current 15m market slug.
2. **Monitor**: Poll CLOB orderbooks every `CHECK_INTERVAL_MS`.
3. **Watch**: First `DUMP_HEDGE_WINDOW_MINUTES`: detect if ask drops >= `MOVE_THRESHOLD`.
4. **Leg 1**: Buy `DUMP_HEDGE_SHARES` of dumped side at current ask.
5. **Leg 2**: Hedge when `leg1_entry + opposite_ask <= DUMP_HEDGE_SUM_TARGET`.
6. **Rollover**: New 15m period → reset state.

## Configuration (.env)

- `PRIVATE_KEY`: Wallet key.
- `MARKETS`: btc,eth,sol,xrp.
- `DUMP_HEDGE_MOVE_THRESHOLD`: e.g. 0.15 (15% drop).
- `DUMP_HEDGE_SUM_TARGET`: e.g. 0.95.

## Profit Mechanics

- **Revenue**: $1.00 per winning share.
- **Goal**: combined cost of Leg 1 + Leg 2 < $0.95.
- **Profit**: ~$0.05 per share pair.
