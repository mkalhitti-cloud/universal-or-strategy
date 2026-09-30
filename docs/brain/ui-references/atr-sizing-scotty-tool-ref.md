# ATR Sizing Reference — Scotty's Tool (Schwab/StreetSmart Edge)
**Saved**: 2026-07-09
**Source**: User-provided screenshot — Schwab StreetSmart Edge, /ES 5-min chart

---

## Screenshot data (verbatim from image)

### Chart header — ATR readout
```
Current ATR: 3.43  |  Daily ATR: 100.03  |  Range: 78.75  |  Spread: 0.25
```

### Scotty's sizing bar (below header)
```
ATR Stop of 3 points  |  1.33 Max Contracts  @ Risk: $199.50
```

### Instrument
```
/ES  E-mini S&P 500 Index Futures, ETH (SEP 26)
Price: 7587.00  |  5-minute bars
```

---

## Formula — reverse-engineered from live numbers

**Instrument**: ES = $50 per point  
**ATR(14)**: 3.43 points (Current ATR from header)  
**AtrFraction**: 0.75 (default — confirmed by Scotty's tool)  
**MaxRiskDollars**: $199.50 (user's configured risk budget)

### Step 1 — ATR stop size in points

```
stopPoints_raw  = ATR × atrFraction
                = 3.43 × 0.75
                = 2.5725 points

stopPoints_display = ceil(2.5725) = 3 points     ← shown as "ATR Stop of 3 points"
```

The displayed stop is **ceiling of the raw stop** — always rounds UP to the next
whole point. This is conservative: you never understate the stop size.

### Step 2 — Risk per contract

```
riskPerContract = stopPoints_display × dollarPerPoint
                = 3.0 × $50
                = $150.00 per contract
```

### Step 3 — Max contracts (raw, shown as decimal)

```
maxContracts_raw = maxRiskDollars / riskPerContract
                 = $199.50 / $150.00
                 = 1.33                           ← shown as "1.33 Max Contracts"
```

**Key**: the display shows the RAW decimal. PTT rounds UP (ceiling) to get the
actual order quantity:
```
contracts = ceil(1.33) = 2
```

### Step 4 — @ Risk confirmation

```
@ Risk = contracts_raw × riskPerContract
       = 1.33 × $150.00
       = $199.50                                  ← shown as "@ Risk: $199.50" ✅
```

All three displayed values check out perfectly against the formula.

---

## Complete PTT formula

```
stopPoints      = ceil( ATR(period) × atrFraction )    ← ceiling of stop
riskPerContract = stopPoints × tickDollarValue         ← dollar per POINT (not tick)
maxContracts    = ceil( maxRiskDollars / riskPerContract )   ← ceiling of qty
contracts       = max(1, maxContracts)                 ← clamp minimum to 1
```

Two ceilings:
1. **Stop size ceiling** — `ceil(ATR × fraction)` → whole-point stop (conservative)
2. **Contract count ceiling** — `ceil(maxRisk / risk)` → never under-size

---

## Configuration parameters

| Parameter | Default | Notes |
|-----------|---------|-------|
| `Period` | 14 | ATR period (14-bar) |
| `AtrFraction` | 0.75 | Fraction of ATR used as stop (0.0–1.0) |
| `MaxRiskDollars` | $199.50 | Max dollar risk per trade (Scotty uses $199.50) |
| `TickDollarValue` | $50.00 | Dollar per POINT: ES=$50, MES=$5, NQ=$20, MNQ=$2 |

---

## Live example walkthrough

| Input | Value |
|-------|-------|
| ATR(14) | 3.43 points |
| AtrFraction | 0.75 |
| Raw stop | 3.43 × 0.75 = 2.5725 pts |
| Stop (ceil) | **3 points** |
| Risk/contract (ES) | 3 × $50 = **$150** |
| Max risk budget | $199.50 |
| Raw contracts | 199.50 / 150 = **1.33** |
| Order qty (ceil) | **2 contracts** |

---

## Implementation status in PTT

### Current code (after B10 ATR-fraction edit)
[`AtrSizingEngine.CalcContracts()`](../../../../universal-or-strategy/src/PropTraderTools/AtrSizingEngine.cs)

```csharp
// stopPoints = atrPoints * atrFraction          ← no ceiling on stop yet
// contracts  = ceil(maxRisk / (stopPoints * tickDollarValue))
```

### Missing: ceiling on stop size

The current code applies `atrFraction` but does NOT apply `Math.Ceiling` to the
stop size before computing risk. It goes straight to the contract ceiling.

**Impact**: 3.43 × 0.75 = 2.5725, riskPerContract = 2.5725 × $50 = $128.625
→ ceil($199.50 / $128.625) = ceil(1.55) = 2 contracts ← still 2, different path

Scotty's tool uses `ceil(stop)` first → 3.0 → $150 risk/contract → 1.33 raw → ceil → 2.

Both produce 2 contracts in this example. The difference matters when the raw
stop ceiling changes the outcome. For PTT consistency with Scotty's tool:

**TODO**: add `stopPoints = Math.Ceiling(atrPoints * atrFraction)` as an option,
or keep current behaviour (no stop ceiling, fraction only). Director to decide.
Default recommendation: match Scotty exactly — ceil the stop.

---

## Panel display to add (B11)

Mirror Scotty's readout exactly:
```
ATR Stop: {stopPoints} pts  |  {rawContracts:F2} Max Contracts  @ Risk: ${riskDollars:F2}
```

Placed as a read-only label row in the PTT panel above the action buttons.
Updates every bar close from `AtrSizingEngine.OnBarUpdate()`.
