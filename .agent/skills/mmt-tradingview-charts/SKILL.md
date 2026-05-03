---
name: mmt-tradingview-charts
description: Best practices for building charting applications using MMT real-time crypto market data with TradingView Lightweight Charts. Use when creating price charts, volume charts, indicator overlays, multi-chart dashboards, or any visualization that renders MMT data using the TradingView Lightweight Charts library.
---

# MMT + TradingView Lightweight Charts

Rules for building charting applications that render MMT market data using TradingView Lightweight Charts (v5.x).

## Chart Setup

- createChart config, container setup, autoSize, dark/light themes
- transform MMT types (OHLCVTPublic, etc.) to Lightweight Charts format

## Real-Time Updates

- WS candle stream to series.update(), handling candle close vs in-progress
- WS trade stream to line/marker overlays, trade volume histogram

## Indicators & Overlays

- Volume Histogram: buy/sell volume as colored histogram
- Funding Rate Overlay: stats channel funding rate as line/baseline series
- Open Interest Overlay: OI candles as area/line on separate pane

## Data Management

- REST fetch for historical data, pagination support
- Timeframe & Symbol Switching: clear stores, flush timers

## Interaction

- subscribeCrosshairMove, custom tooltip with MMT data fields
- React/framework integration, cleanup, resize, memory management
