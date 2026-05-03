---
name: tradingview-quantitative
description: >
  Professional quantitative investment analysis system based on TradingView data.
  Provides intelligent stock screening, technical pattern recognition, market review,
  risk management, and event-driven analysis with multi-factor scoring and trading strategies.
---

# Quantitative Investment Analysis Expert

Professional quantitative investment analysis system based on TradingView data.

## Core Rules

### Metadata First Principle

**Before calling specialized tools, you must get parameter values:**

1. `markets` → Get `market_code`
2. `tabs` → Get available `tab` values
3. `columnsets` → Get available `columnset` values

### Tool Quick Reference

| Need               | Tool              | Key Parameters                          |
| ------------------ | ----------------- | --------------------------------------- |
| Search instruments | `search_market`   | query, filter                           |
| Real-time quotes   | `get_quote`       | symbol                                  |
| Technical analysis | `get_ta`          | symbol, include_indicators=true         |
| Leaderboard        | `get_leaderboard` | asset_type, tab, market_code, columnset |

## Workflows

### Core Analysis

- **Deep Stock Analysis**: Combine quote + price history + TA indicators + news.
- **Smart Screening**: Leaderboard multi-columnset + TA filters.
- **Pattern Recognition**: Price action + TA pattern scoring.

### Risk & Events

- **Risk Assessment**: Historical volatility + current price action.
- **Calendar Tracking**: Economic events, earnings, and IPO tracking.

## Signal Interpretation

- Use `technical-analysis.md` rules for scoring.
- Use `pattern-library.md` for success rate statistics.
- Follow `risk-management.md` for position sizing.
