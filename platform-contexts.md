# Platform Context Blocks

Pre-written context blocks to include in external AI prompts. These explain platform-specific constraints that external AIs typically don't understand.

---

## NinjaTrader 8 Context Block

```
**NinjaTrader 8 Constraints:**
- Strategies MUST be single `.cs` files (platform requirement, not design choice)
- UI MUST use code-behind WPF (XAML not supported for strategies)
- Lifecycle managed by platform: `State.Configure` â†’ `State.DataLoaded` â†’ `State.Historical` â†’ `State.Realtime` â†’ `State.Terminated`
- `OnBarUpdate()` runs based on Calculate setting (OnBarClose, OnEachTick, OnPriceChange)
- `OnOrderUpdate()` and `OnExecutionUpdate()` are real-time events
- `IsUnmanaged = true` means manual order management (no automatic stops)
- Cross-thread UI access requires `Dispatcher.InvokeAsync()`
- `MaximumBarsLookBack.TwoHundredFiftySix` is critical for memory on multiple charts
- Chart objects accessed via `ChartControl` and `ChartPanel`
- Time[0] is LOCAL PC time, not exchange time

**What WON'T work in NT8:**
- Splitting strategies into multiple classes/DLLs (breaks import/export)
- XAML UserControls for strategy UIs
- Async/await in OnBarUpdate (can cause issues)
- Direct file I/O without proper paths
- Accessing ChartControl from non-UI thread
```

---

## Rithmic Data Feed Context Block

```
**Rithmic Data Feed Characteristics:**
- Faster tick data than Continuum
- Different connection handling than other feeds
- Order routing goes direct to exchange
- Fill times typically faster than other brokers
- Connection drops require strategy to handle gracefully
- Historical data requests have rate limits
```

---

## Apex Funded Account Context Block

```
**Apex Trader Funding Rules:**
- Trailing drawdown (threshold moves up with profits)
- Daily loss limits
- No holding positions during major news (optional rule)
- Must close positions before end of trading day
- Scaling rules for multiple accounts
- Consistency rules (can't make all profits in one day)
```

---

## MES/MGC Micro Futures Context Block

```
**MES (Micro E-mini S&P 500):**
- Tick Size: 0.25
- Point Value: $5 per point ($1.25 per tick)
- Trading Hours: Sunday 6pm - Friday 5pm ET (with daily breaks)
- Typical ATR: 15-30 points depending on volatility
- Margin: ~$50-100 for Apex accounts

**MGC (Micro Gold):**
- Tick Size: 0.10
- Point Value: $10 per point ($1.00 per tick)  
- Trading Hours: Sunday 6pm - Friday 5pm ET (with daily breaks)
- Typical ATR: 8-20 points depending on volatility
- Margin: ~$50-100 for Apex accounts

**Key Differences:**
- MES moves in 0.25 increments, MGC in 0.10
- Different volatility profiles require different position sizing
- Different typical stop distances
```

---

## React/TypeScript Context Block (for web projects)

```
**React/TypeScript Constraints:**
- Functional components with hooks preferred
- TypeScript strict mode enabled
- State management via [Redux/Zustand/Context - specify]
- Component library: [MUI/Tailwind/etc - specify]
- Build tool: [Vite/Next.js/CRA - specify]
- Testing: [Jest/Vitest/Cypress - specify]

**Project Conventions:**
- [List your specific conventions]
```

---

## Python Trading Context Block

```
**Python Trading Bot Constraints:**
- Broker API: [Alpaca/IBKR/etc]
- Data source: [Polygon/Yahoo/etc]
- Framework: [Backtrader/Zipline/custom]
- Execution: [Async/sync]
- Deployment: [Local/cloud/VPS]

**Critical Considerations:**
- API rate limits
- Market hours handling
- Position synchronization
- Error recovery
```

---

## Usage

Copy the relevant block(s) into your external AI prompt under the "CRITICAL PLATFORM CONTEXT" section. Modify as needed for your specific setup.
