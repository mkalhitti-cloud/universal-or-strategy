---
name: binance-futures
description: Access Binance USDS Futures trading and account management via authenticated API endpoints.
---

# Binance USDS-Futures Skill

Access Binance USDS Futures trading and account management via authenticated API endpoints.

## Core Features

- 80+ endpoints for account configuration, balance, and order management.
- Position management: Hedge mode, leverage (1-125x), and margin adjustments.
- Real-time market data: Klines, funding rates, open interest, and taker sentiment.
- Algo orders support: Conditional, Stop-Loss, Take-Profit.

## Authentication

Requires `BINANCE_API_KEY` and `BINANCE_SECRET_KEY`. Supports both mainnet and testnet.

## Quick Reference

| Endpoint                | Description                     |
| ----------------------- | ------------------------------- |
| `/fapi/v1/order`        | Place/Modify/Cancel orders      |
| `/fapi/v2/positionRisk` | View current positions and risk |
| `/fapi/v2/balance`      | Account balance                 |
| `/fapi/v1/klines`       | Market data                     |

## Parameters

- `symbol`: Ticker (e.g., BTCUSDT)
- `side`: BUY | SELL
- `positionSide`: BOTH | LONG | SHORT (for Hedge Mode)
- `type`: LIMIT | MARKET | STOP | TAKE_PROFIT
- `leverage`: 1 to 125
