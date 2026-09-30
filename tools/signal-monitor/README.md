# WSGTA Signal Monitor

A lightweight local web dashboard for monitoring trading signals across MES, MCL, and MGC futures instruments.

## Features

- **Real-time Schwab API integration** — fetches live OHLCV bars from the same data feed as ThinkorSwim
- **7 timeframes** — 5min, 15min, 30min, 1hr, 2hr, 4hr, 1Day
- **Multi-layer level detection**:
  - EMA 9, 15 (5min only — trend trade)
  - EMA 30, 65, 200 (all timeframes)
  - VWAP with ±2.0 standard deviation bands (daily anchor, 5min only)
  - Previous Day High / Low / Close
  - Previous Week High / Low / Close
  - Woodie Pivot Points (PP, R1-R3, S1-S3)
- **Wilder's 14-period ATR** — always computed from 5min bars
- **Level state machine** — tracks ACTIVE/INACTIVE state for each level based on price proximity
- **Trend trade validation** — 9/15 EMA gap rule (valid if gap <= 2.5 pts)
- **Signal engine** — finds next LONG and SHORT levels that qualify at the current ATR threshold
- **Confluence detection** — groups levels within ±0.25 × ATR into zones
- **One-click copy** — formats each signal as a Zoom chat-ready string
- **Vertical price ruler** — sticky panel with all levels color-coded by type
- **Demo mode** — works out of the box with synthetic data before API credentials are configured

## Prerequisites

- **Python 3.11+**
- **pip** (Python package installer)

## Setup

### 1. Create `.env` from template

```bash
cd tools/signal-monitor
cp .env.example .env
```

### 2. Add Schwab Developer credentials

Open `.env` and replace the placeholders with your Schwab API credentials:

```
SCHWAB_APP_KEY=your_actual_app_key
SCHWAB_APP_SECRET=your_actual_app_secret
POLL_INTERVAL_SECONDS=30
```

**Where to get credentials:**
1. Go to the [Schwab Developer Portal](https://developer.schwab.com/)
2. Create an app (if you don't have one)
3. Copy the **App Key** and **App Secret** from your app dashboard

### 3. Install dependencies

```bash
pip install -r requirements.txt
```

## Usage

### First run: Authenticate with Schwab

```bash
python server.py --auth
```

This will:
- Open your browser to the Schwab login page
- Ask you to log in with your Schwab credentials
- Redirect back to `https://127.0.0.1` to capture the authorization code
- Exchange the code for an access token
- Save the token to `.token_cache.json` (gitignored)

**Note:** The redirect uses `https://127.0.0.1` (port 443) by default. If you get a permission error on Linux/Mac, the script will fall back to port 8182. You may need to update your Schwab app's Redirect URI in the developer portal to match.

### Normal run

```bash
python server.py
```

The server will:
- Automatically refresh the token if expired (using the refresh token)
- Fetch live bars from Schwab every 30 seconds
- Compute all levels and signals
- Serve the dashboard at `http://localhost:5000`

### Demo mode (no credentials)

If `.env` is not set up or `SCHWAB_APP_KEY` equals `your_app_key_here`, the server automatically runs in **demo mode** with synthetic data. This lets you test the UI and logic before connecting to the API.

## Testing against ThinkorSwim (TOS)

To verify the levels match what you see in TOS:

### Pivot points (Woodie formula)

1. In TOS, add the **Pivot Points** study
2. Select **Woodie** as the calculation method
3. Compare PP, R1-R3, S1-S3 with the Signal Monitor dashboard

**Fallback:** If you notice mismatches, open a GitHub issue — we can add a Classic pivot option.

### EMAs

1. In TOS, add **EMA(9)**, **EMA(15)**, **EMA(30)**, **EMA(65)**, **EMA(200)** studies
2. Compare the level values on the ruler with the TOS chart at the same timestamp

### VWAP

1. In TOS, add **VWAP** study with **standard deviation bands** set to **2.0**
2. Verify the VWAP and ±2σ bands match the dashboard values

## File Structure

```
tools/signal-monitor/
  ├── .env                   ← your credentials (gitignored)
  ├── .env.example           ← template
  ├── .token_cache.json      ← OAuth token (gitignored, auto-created)
  ├── requirements.txt       ← Python dependencies
  ├── README.md              ← this file
  ├── server.py              ← FastAPI entry point
  ├── schwab_client.py       ← OAuth2 auth + bar fetching
  ├── levels.py              ← EMA, VWAP, ATR, pivots
  ├── signals.py             ← state machine + signal engine
  └── static/
        └── index.html       ← dashboard UI
```

## Troubleshooting

### "No token found. Run: python server.py --auth"

You haven't authenticated yet. Run the `--auth` flow first.

### "Failed to load data: HTTP 401"

Your token expired and the refresh failed. Delete `.token_cache.json` and re-run `--auth`.

### Port 443 unavailable (Linux/Mac)

The redirect listener defaults to port 443 (privileged). If you get a permission error:
- The script will automatically fall back to port 8182
- Update your Schwab app's Redirect URI to `https://127.0.0.1:8182`

### Levels don't match ThinkorSwim

- **Pivots:** Verify you're using the same formula (Woodie vs Classic). We default to Woodie.
- **EMAs:** Check the timeframe — we compute per-TF EMAs, not a single cross-TF EMA.
- **VWAP:** Ensure you're comparing the same session anchor (daily).

### Demo mode won't turn off

Check `.env` — if `SCHWAB_APP_KEY` is still `your_app_key_here`, replace it with your actual key.

## License

MIT

## Support

For issues, questions, or feature requests, open a GitHub issue in the `universal-or-strategy-director` repo.
