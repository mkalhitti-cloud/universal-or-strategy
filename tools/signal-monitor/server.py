"""
server.py — FastAPI entry point for the WSGTA Signal Monitor.

Usage:
  python server.py           # normal run (demo if no .env)
  python server.py --auth    # run OAuth2 browser flow, then start server

Endpoints:
  GET /              → serves static/index.html
  GET /api/snapshot  → full signal snapshot for all instruments
  GET /api/status    → server status + mode
"""
from __future__ import annotations

import asyncio
import sys
import time
from pathlib import Path
from typing import Any

import uvicorn
from dotenv import load_dotenv
from fastapi import FastAPI
from fastapi.responses import FileResponse, JSONResponse
from fastapi.staticfiles import StaticFiles

load_dotenv()

from schwab_client import is_demo_mode, fetch_bars, fetch_quote, do_auth_flow
from levels import compute_levels
from signals import detect_signals, reset_states

# ─────────────────────────────────────────────
#  Config
# ─────────────────────────────────────────────

INSTRUMENTS = ["/ES", "/GC", "/CL"]
INSTRUMENT_LABELS = {"/ES": "ES", "/GC": "GC", "/CL": "CL"}
INSTRUMENT_FULLNAMES = {
    "/ES": "E-mini S&P 500",
    "/GC": "Gold Futures",
    "/CL": "Crude Oil WTI",
}
TIMEFRAMES = ["5min", "15min", "30min", "1hr", "2hr", "4hr", "1Day"]

STATIC_DIR = Path(__file__).parent / "static"

# ─────────────────────────────────────────────
#  Demo data generator
# ─────────────────────────────────────────────

def _demo_bars(n: int, base_price: float, step: float = 2.0) -> list[dict]:
    """Generate synthetic OHLCV bars for demo mode."""
    import math
    bars = []
    t = int(time.time() * 1000) - n * 5 * 60 * 1000  # start n*5min ago
    price = base_price
    for i in range(n):
        # Sine wave + noise
        price += math.sin(i * 0.15) * step + (((i * 17 + 3) % 7) - 3) * step * 0.3
        high = price + abs((i * 13 % 5)) * step * 0.4
        low = price - abs((i * 11 % 4)) * step * 0.4
        bars.append({
            "timestamp": t + i * 5 * 60 * 1000,
            "open": price - step * 0.1,
            "high": high,
            "low": low,
            "close": price,
            "volume": 500 + (i * 23 % 400),
        })
    return bars


def _demo_daily_bars(n: int, base_price: float) -> list[dict]:
    """Generate synthetic daily bars for demo pivots/prev-week."""
    import math
    bars = []
    t = int(time.time() * 1000) - n * 24 * 3600 * 1000
    price = base_price
    for i in range(n):
        price += math.sin(i * 0.3) * 15 + (i % 3 - 1) * 5
        bars.append({
            "timestamp": t + i * 24 * 3600 * 1000,
            "open": price - 5,
            "high": price + 20 + (i % 4) * 3,
            "low": price - 20 - (i % 3) * 2,
            "close": price,
            "volume": 10000 + i * 100,
        })
    return bars


_DEMO_BASES = {"/ES": 5870.0, "/CL": 77.50, "/GC": 2345.0}
_DEMO_STEPS = {"/ES": 2.0, "/CL": 0.05, "/GC": 1.5}


def _demo_bars_by_tf(symbol: str) -> dict[str, list[dict]]:
    base = _DEMO_BASES.get(symbol, 100.0)
    step = _DEMO_STEPS.get(symbol, 1.0)
    bars = {}
    bar_counts = {"5min": 500, "15min": 300, "30min": 300, "1hr": 300,
                  "2hr": 300, "4hr": 300}
    for tf, n in bar_counts.items():
        bars[tf] = _demo_bars(n, base, step)
    bars["1Day"] = _demo_daily_bars(300, base)
    return bars


def _demo_quote(symbol: str) -> dict:
    base = _DEMO_BASES.get(symbol, 100.0)
    return {"last": base, "change": 3.25, "pct_change": 0.056}


# ─────────────────────────────────────────────
#  Data cache (refreshed every 30s in live mode)
# ─────────────────────────────────────────────

_cache: dict[str, Any] = {}
_last_refresh: float = 0.0
CACHE_TTL = 30.0


async def _refresh_data() -> None:
    global _last_refresh
    demo = is_demo_mode()
    results = []

    for symbol in INSTRUMENTS:
        label = INSTRUMENT_LABELS[symbol]
        try:
            if demo:
                bars_by_tf = _demo_bars_by_tf(symbol)
                quote = _demo_quote(symbol)
            else:
                # Fetch all timeframes concurrently
                loop = asyncio.get_event_loop()
                bars_by_tf = {}
                for tf in TIMEFRAMES:
                    # httpx is sync — run in thread pool
                    bars_by_tf[tf] = await loop.run_in_executor(
                        None, fetch_bars, symbol, tf
                    )
                quote = await loop.run_in_executor(None, fetch_quote, symbol)

            levels_data = compute_levels(bars_by_tf)
            signal_data = detect_signals(label, quote["last"], levels_data)
            signal_data["quote"] = quote
            results.append(signal_data)

        except Exception as exc:  # noqa: BLE001
            results.append({
                "instrument": label,
                "error": str(exc),
                "current_price": 0,
            })

    _cache["snapshot"] = {
        "instruments": results,
        "mode": "demo" if demo else "live",
        "timestamp": time.time(),
        "timestamp_fmt": time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime()),
    }
    _last_refresh = time.time()


# ─────────────────────────────────────────────
#  Background refresh task
# ─────────────────────────────────────────────

_refresh_lock = asyncio.Lock()


async def _background_refresh() -> None:
    while True:
        async with _refresh_lock:
            try:
                await _refresh_data()
            except Exception as exc:  # noqa: BLE001
                print(f"[refresh error] {exc}")
        poll = 30  # seconds
        try:
            import os
            poll = int(os.getenv("POLL_INTERVAL_SECONDS", "30"))
        except Exception:  # noqa: BLE001
            pass
        await asyncio.sleep(poll)


# ─────────────────────────────────────────────
#  FastAPI app
# ─────────────────────────────────────────────

app = FastAPI(title="WSGTA Signal Monitor", version="1.0.0")
app.mount("/static", StaticFiles(directory=str(STATIC_DIR)), name="static")


@app.on_event("startup")
async def on_startup() -> None:
    reset_states()
    # Warm up cache immediately, then schedule background task
    try:
        await _refresh_data()
    except Exception as exc:  # noqa: BLE001
        print(f"[startup warm-up error] {exc}")
    asyncio.create_task(_background_refresh())


@app.get("/")
async def root() -> FileResponse:
    return FileResponse(str(STATIC_DIR / "index.html"))


@app.get("/api/snapshot")
async def api_snapshot() -> JSONResponse:
    if not _cache.get("snapshot"):
        # First request before warm-up finishes
        async with _refresh_lock:
            if not _cache.get("snapshot"):
                await _refresh_data()
    return JSONResponse(_cache["snapshot"])


@app.get("/api/status")
async def api_status() -> JSONResponse:
    return JSONResponse({
        "mode": "demo" if is_demo_mode() else "live",
        "last_refresh": _last_refresh,
        "last_refresh_fmt": time.strftime("%Y-%m-%d %H:%M:%S UTC", time.gmtime(_last_refresh)) if _last_refresh else "never",
        "instruments": INSTRUMENT_LABELS,
    })


# ─────────────────────────────────────────────
#  Entry point
# ─────────────────────────────────────────────

if __name__ == "__main__":
    if "--auth" in sys.argv:
        print("=== Schwab OAuth2 Auth Flow ===")
        do_auth_flow()
        print("Auth complete. Starting server...")

    uvicorn.run(
        "server:app",
        host="0.0.0.0",
        port=5000,
        reload=False,
        log_level="info",
    )
