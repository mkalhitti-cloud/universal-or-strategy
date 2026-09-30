"""
schwab_client.py — OAuth2 PKCE auth + price history fetching for Schwab Developer API.

First run: python server.py --auth  (opens browser, captures token)
Subsequent runs: uses cached refresh token automatically.
"""
from __future__ import annotations

import base64
import hashlib
import json
import os
import secrets
import time
import urllib.parse
import webbrowser
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path
from typing import Optional

import httpx
from dotenv import load_dotenv

load_dotenv()

BASE_URL = "https://api.schwabapi.com"
AUTH_URL = f"{BASE_URL}/v1/oauth/authorize"
TOKEN_URL = f"{BASE_URL}/v1/oauth/token"
REDIRECT_URI = "https://127.0.0.1"
TOKEN_CACHE = Path(__file__).parent / ".token_cache.json"

# ─────────────────────────────────────────────
#  Bar count targets per frequency
# ─────────────────────────────────────────────
BAR_COUNTS: dict[str, int] = {
    "5min":  500,
    "15min": 300,
    "30min": 300,
    "1hr":   300,   # synthesised from 30min
    "2hr":   300,   # synthesised from 30min
    "4hr":   300,   # synthesised from 30min
    "1Day":  300,
}

# Map our label → Schwab (frequencyType, frequency)
# NOTE: Schwab futures only support minute/5, minute/15, minute/30, daily/1.
# 1hr/2hr/4hr are synthesised by resampling 30min bars — see _resample_bars().
_FREQ_MAP: dict[str, tuple[str, int]] = {
    "5min":  ("minute", 5),
    "15min": ("minute", 15),
    "30min": ("minute", 30),
    "1Day":  ("daily",  1),
}

# How many 30min bars to merge to produce each synthetic timeframe
_RESAMPLE_FROM_30MIN: dict[str, int] = {
    "1hr":  2,   # 2 × 30min = 1 hour
    "2hr":  4,   # 4 × 30min = 2 hours
    "4hr":  8,   # 8 × 30min = 4 hours
}

# ─────────────────────────────────────────────
#  Credentials
# ─────────────────────────────────────────────

def _app_key() -> str:
    return os.getenv("SCHWAB_APP_KEY", "")

def _app_secret() -> str:
    return os.getenv("SCHWAB_APP_SECRET", "")

def is_demo_mode() -> bool:
    key = _app_key()
    return not key or key == "your_app_key_here"

# Cache root→active contract mapping so we don't re-fetch on every bars call
_active_contract: dict[str, str] = {}


# ─────────────────────────────────────────────
#  Token cache helpers
# ─────────────────────────────────────────────

def _load_token() -> Optional[dict]:
    if not TOKEN_CACHE.exists():
        return None
    try:
        return json.loads(TOKEN_CACHE.read_text())
    except Exception:
        return None


def _save_token(data: dict) -> None:
    TOKEN_CACHE.write_text(json.dumps(data, indent=2))


def _token_expired(token_data: dict) -> bool:
    expires_at = token_data.get("expires_at", 0)
    return time.time() >= expires_at - 60  # 60-second buffer


# ─────────────────────────────────────────────
#  PKCE helpers
# ─────────────────────────────────────────────

def _pkce_pair() -> tuple[str, str]:
    verifier = secrets.token_urlsafe(64)
    digest = hashlib.sha256(verifier.encode()).digest()
    challenge = base64.urlsafe_b64encode(digest).rstrip(b"=").decode()
    return verifier, challenge


# ─────────────────────────────────────────────
#  One-shot local redirect capture
# ─────────────────────────────────────────────

_captured_code: Optional[str] = None


class _RedirectHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        global _captured_code
        parsed = urllib.parse.urlparse(self.path)
        params = urllib.parse.parse_qs(parsed.query)
        if "code" in params:
            _captured_code = params["code"][0]
        self.send_response(200)
        self.send_header("Content-Type", "text/html")
        self.end_headers()
        self.wfile.write(b"<h2>Auth complete. You can close this tab.</h2>")

    def log_message(self, *args):  # silence server log
        pass


def _capture_redirect_code(port: int = 443) -> str:
    """Start a temporary local HTTP server to catch the OAuth redirect."""
    # Note: Schwab redirects to https://127.0.0.1 — we listen on 443 if possible,
    # but browsers on 443 need root on Linux. Fall back to 8182 for testing.
    global _captured_code
    _captured_code = None
    server = HTTPServer(("127.0.0.1", port), _RedirectHandler)
    server.timeout = 120
    print(f"  Listening for redirect on http://127.0.0.1:{port} ...")
    while _captured_code is None:
        server.handle_request()
    server.server_close()
    return _captured_code


# ─────────────────────────────────────────────
#  Auth flow
# ─────────────────────────────────────────────

def do_auth_flow() -> None:
    """Run the interactive OAuth2 PKCE flow. Stores token to disk."""
    key = _app_key()
    secret = _app_secret()
    if not key or key == "your_app_key_here":
        raise RuntimeError("SCHWAB_APP_KEY not set in .env")

    verifier, challenge = _pkce_pair()
    state = secrets.token_hex(16)

    params = {
        "response_type": "code",
        "client_id": key,
        "redirect_uri": REDIRECT_URI,
        "scope": "readonly",
        "state": state,
        "code_challenge": challenge,
        "code_challenge_method": "S256",
    }
    url = AUTH_URL + "?" + urllib.parse.urlencode(params)
    print(f"\nOpening browser for Schwab login:\n  {url}\n")
    webbrowser.open(url)

    # Try privileged port first; fall back to 8182 for dev setups
    try:
        code = _capture_redirect_code(443)
    except PermissionError:
        print("  Port 443 unavailable, trying 8182 — you may need to update Redirect URI in Schwab dashboard.")
        code = _capture_redirect_code(8182)

    # Exchange code for tokens
    credentials = base64.b64encode(f"{key}:{secret}".encode()).decode()
    resp = httpx.post(
        TOKEN_URL,
        headers={
            "Authorization": f"Basic {credentials}",
            "Content-Type": "application/x-www-form-urlencoded",
        },
        data={
            "grant_type": "authorization_code",
            "code": code,
            "redirect_uri": REDIRECT_URI,
            "code_verifier": verifier,
        },
        timeout=30,
    )
    resp.raise_for_status()
    token_data = resp.json()
    token_data["expires_at"] = time.time() + token_data.get("expires_in", 1800)
    _save_token(token_data)
    print("  Token saved to .token_cache.json")


def _refresh_token_if_needed() -> dict:
    token_data = _load_token()
    if token_data is None:
        raise RuntimeError("No token found. Run: python server.py --auth")
    if not _token_expired(token_data):
        return token_data

    key = _app_key()
    secret = _app_secret()
    credentials = base64.b64encode(f"{key}:{secret}".encode()).decode()
    resp = httpx.post(
        TOKEN_URL,
        headers={
            "Authorization": f"Basic {credentials}",
            "Content-Type": "application/x-www-form-urlencoded",
        },
        data={
            "grant_type": "refresh_token",
            "refresh_token": token_data["refresh_token"],
        },
        timeout=30,
    )
    resp.raise_for_status()
    new_data = resp.json()
    new_data["expires_at"] = time.time() + new_data.get("expires_in", 1800)
    # Preserve refresh_token if not returned
    if "refresh_token" not in new_data:
        new_data["refresh_token"] = token_data["refresh_token"]
    _save_token(new_data)
    return new_data


# ─────────────────────────────────────────────
#  Price history fetch
# ─────────────────────────────────────────────

def _access_token() -> str:
    return _refresh_token_if_needed()["access_token"]


def _resample_bars(bars_30min: list[dict], n_merge: int) -> list[dict]:
    """Merge every n_merge consecutive 30min bars into a single synthetic bar."""
    out: list[dict] = []
    i = 0
    while i + n_merge <= len(bars_30min):
        chunk = bars_30min[i: i + n_merge]
        out.append({
            "timestamp": chunk[0]["timestamp"],
            "open":  chunk[0]["open"],
            "high":  max(b["high"] for b in chunk),
            "low":   min(b["low"]  for b in chunk),
            "close": chunk[-1]["close"],
            "volume": sum(b["volume"] for b in chunk),
        })
        i += n_merge
    return out


def fetch_bars(symbol: str, tf_label: str) -> list[dict]:
    """
    Fetch OHLCV bars for *symbol* at timeframe *tf_label*.
    Returns list of dicts: {timestamp, open, high, low, close, volume}.
    Symbol should be like '/MES', '/MCL', '/MGC' for futures.

    1hr / 2hr / 4hr are synthesised by resampling 30min bars because
    Schwab's API does not support frequencyType=minute with frequency>30.
    """
    # ── Synthetic timeframes: resample from 30min ─────────────────
    if tf_label in _RESAMPLE_FROM_30MIN:
        n_merge = _RESAMPLE_FROM_30MIN[tf_label]
        target  = BAR_COUNTS[tf_label]
        # Fetch enough 30min bars to produce target resampled bars
        raw_30min = _fetch_raw_bars(symbol, "30min", target * n_merge + n_merge)
        resampled = _resample_bars(raw_30min, n_merge)
        return resampled[-target:]

    return _fetch_raw_bars(symbol, tf_label, BAR_COUNTS[tf_label])


def _fetch_raw_bars(symbol: str, tf_label: str, n_bars: int) -> list[dict]:
    """Internal: fetch raw bars from Schwab for a supported tf_label."""
    freq_type, freq = _FREQ_MAP[tf_label]

    # Schwab allowed periods for periodType=day: 1,2,3,4,5,10
    # For futures we always want extended hours data
    if freq_type == "minute":
        period_type = "day"
        days_needed = max(1, (n_bars // 78) + 2)
        # Clamp to Schwab-allowed values: 1,2,3,4,5,10
        allowed = [1, 2, 3, 4, 5, 10]
        period = min((d for d in allowed if d >= days_needed), default=10)
    else:
        period_type = "year"
        period = 2  # 2 years of daily bars

    params: dict = {
        "periodType": period_type,
        "period": period,
        "frequencyType": freq_type,
        "frequency": freq,
        "needExtendedHoursData": "true",  # futures trade nearly 24h
    }

    token = _access_token()
    # Resolve root symbol (/ES) → active contract (/ESU26)
    active = _resolve_symbol(symbol)
    params["symbol"] = active
    resp = httpx.get(
        f"{BASE_URL}/marketdata/v1/pricehistory",
        headers={"Authorization": f"Bearer {token}"},
        params=params,
        timeout=30,
    )
    resp.raise_for_status()
    data = resp.json()

    candles = data.get("candles", [])
    bars = [
        {
            "timestamp": c["datetime"],  # ms epoch
            "open": float(c["open"]),
            "high": float(c["high"]),
            "low": float(c["low"]),
            "close": float(c["close"]),
            "volume": float(c.get("volume", 0)),
        }
        for c in candles
    ]
    return bars[-n_bars:]  # return latest n_bars


def fetch_quote(symbol: str) -> dict:
    """Fetch current quote for symbol. Returns {last, change, pct_change}.
    Also caches the active contract symbol (e.g. /ESU26) for use by fetch_bars.
    """
    token = _access_token()
    resp = httpx.get(
        f"{BASE_URL}/marketdata/v1/quotes",
        headers={"Authorization": f"Bearer {token}"},
        params={"symbols": symbol, "fields": "quote"},
        timeout=15,
    )
    resp.raise_for_status()
    data = resp.json()
    if not data:
        return {"last": 0.0, "change": 0.0, "pct_change": 0.0}
    # Cache root→active contract (e.g. /ES → /ESU26)
    first_key = next(iter(data))
    _active_contract[symbol] = first_key
    q = data[first_key].get("quote", {})
    return {
        "last": float(q.get("lastPrice", 0) or q.get("mark", 0)),
        "change": float(q.get("netChange", 0)),
        "pct_change": float(q.get("netPercentChangeInDouble", 0)),
    }


def _resolve_symbol(symbol: str) -> str:
    """Return the active contract symbol, falling back to a live quote lookup."""
    if symbol in _active_contract:
        return _active_contract[symbol]
    # Not cached yet — do a quote call to populate the cache
    fetch_quote(symbol)
    return _active_contract.get(symbol, symbol)
