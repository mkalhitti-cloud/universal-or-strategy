---
name: risk-management
description: Risk management rules learned from competition outcomes. Use when sizing positions or setting stop-losses.
---

# Risk Management

> Last updated: 2026-01-17 20:31 UTC
> Active patterns: 40
> Total samples: 13385
> Confidence threshold: 60%

## Core Principles

These rules are derived from analyzing profitable vs losing trades:

| Rule                                                              | Success Rate | Samples | Confidence | Seen |
| ----------------------------------------------------------------- | ------------ | ------- | ---------- | ---- |
| Trade count inversely correlates with performance in flat markets | 95%          | 861     | 55%        | 1x   |
| Trade frequency should adapt to market regime (choppy)            | 95%          | 950     | 95%        | 1x   |
| Validate risk per trade explicitly before entry (Limits)          | 92%          | 157     | 65%        | 1x   |
| Validate risk per trade explicitly (2% Risk / 2:1 RR)             | 92%          | 164     | 70%        | 1x   |
| Trade frequency should adapt to market regime (moderate bull)     | 92%          | 895     | 95%        | 1x   |

## Top Risk Rules

### Trade count inversely correlates with performance in flat markets

- Success rate: 95%
- Based on 861 observations
- Confidence: 55%
- Details: 3-6 trades = ~$0 PnL, 150-225 trades = -$325 to -$581.

### Trade frequency should adapt to market regime

- Success rate: 95%
- Details: Mixed/choppy markets require 0-10 trades/24h maximum.

### Validate risk per trade explicitly before entry

- Success rate: 92%
- Details: 2% equity risk and 2:1 reward ratio achieved best performance (+$1379.66).

## General Guidelines

- Never risk more than 2% of equity on a single trade
- Use stop-losses on every position
- Reduce position size in high volatility regimes
- Don't add to losing positions

## Confidence Guide

| Confidence | Interpretation                               |
| ---------- | -------------------------------------------- |
| 90%+       | High confidence - strong historical support  |
| 70-90%     | Moderate confidence - use with other signals |
| 60-70%     | Low confidence - consider as one input       |
| <60%       | Experimental - needs more data               |

_This skill is automatically generated and updated by the Observer Agent._
