---
name: institutional-flow-tracker
description: Use this skill to track institutional investor ownership changes and portfolio flows using 13F filings data. Analyzes hedge funds, mutual funds, and other institutional holders.
---

# Institutional Flow Tracker

## Overview

This skill tracks institutional investor activity through 13F SEC filings to identify "smart money" flows. Collective buying/selling patterns often precede significant price movements by 1-3 quarters.

## When to Use This Skill

- Validating investment ideas (checking if smart money agrees with your thesis)
- Discovering new opportunities (finding stocks institutions are accumulating)
- Sector rotation analysis (identifying where institutions are rotating capital)

## Analysis Workflow

### Step 1: Identify Significant Activity

Execute the main screening script:

```bash
python3 scripts/track_institutional_flow.py --top 50 --min-change-percent 10
```

### Step 2: Deep Dive

For detailed analysis of a specific stock:

```bash
python3 scripts/analyze_single_stock.py TICKER
```

## Signal Strength Framework

**Strong Bullish:**

- Institutional ownership increasing >15% QoQ
- Number of institutions increasing >10%
- Quality long-term investors adding positions

**Strong Bearish:**

- Institutional ownership decreasing >15% QoQ
- Number of institutions decreasing >10%
- Quality investors exiting positions
