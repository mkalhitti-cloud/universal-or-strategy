"""
schwab_client.py — Schwab API client with auto token refresh.

Usage (standalone test):
    python tools/schwab/schwab_client.py

Usage (from other modules):
    from tools.schwab.schwab_client import SchwabClient
    client = SchwabClient()
    quote  = client.get_quote("/ES")
"""

import base64
import json
import os
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from typing import Any

# ── Paths ──────────────────────────────────────────────────────────────────────
_DIR          = os.path.dirname(__file__)
TOKENS_FILE   = os.path.join(_DIR, "tokens.json")

# ── Schwab endpoints ───────────────────────────────────────────────────────────
TOKEN_URL      = "https://api.schwabapi.com/v1/oauth/token"
QUOTES_URL     = "https://api.schwabapi.com/marketdata/v1/quotes"
HISTORY_URL    = "https://api.schwabapi.com/marketdata/v1/pricehistory"
ACCOUNTS_URL   = "https://api.schwabapi.com/trader/v1/accounts"

# ── Credentials ────────────────────────────────────────────────────────────────
CLIENT_ID     = "xMjwtQ1XkHtsF2MhRughbR5ujpr22VqpU2uUZVQaTO0Hvj2X"
CLIENT_SECRET = "cj2el9hoyG1YGSkkHsyG4Gj0Upj0COtYFW8qIBkE2cPty0inojM19dmTsy3L4OjI"
REDIRECT_URI  = "https://127.0.0.1"

# Access token expires after 30 min — refresh 60s early to be safe
_ACCESS_TOKEN_BUFFER_SECS = 60


class SchwabClient:
    """Thin Schwab REST client. Loads tokens from tokens.json and auto-refreshes."""

    def __init__(self, tokens_file: str = TOKENS_FILE):
        self._tokens_file = tokens_file
        self._tokens: dict = {}
        self._access_expires_at: float = 0.0
        self._load_tokens()

    # ── Token management ───────────────────────────────────────────────────────

    def _load_tokens(self) -> None:
        if not os.path.exists(self._tokens_file):
            raise FileNotFoundError(
                f"tokens.json not found at {self._tokens_file}\n"
                "Run schwab_auth.py first to complete the OAuth login."
            )
        with open(self._tokens_file) as f:
            self._tokens = json.load(f)

        # Calculate when the access token actually expires
        saved_at = self._tokens.get("saved_at")
        expires_in = self._tokens.get("expires_in", 1800)
        if saved_at:
            saved_ts = datetime.fromisoformat(saved_at).timestamp()
            self._access_expires_at = saved_ts + expires_in - _ACCESS_TOKEN_BUFFER_SECS
        else:
            # Assume it's about to expire — will refresh on next request
            self._access_expires_at = 0.0

    def _save_tokens(self) -> None:
        self._tokens["saved_at"] = datetime.now(timezone.utc).isoformat()
        with open(self._tokens_file, "w") as f:
            json.dump(self._tokens, f, indent=2)

    def _refresh_access_token(self) -> None:
        """Use the refresh_token to get a new access_token."""
        refresh_token = self._tokens.get("refresh_token")
        if not refresh_token:
            raise RuntimeError(
                "No refresh_token in tokens.json. Re-run schwab_auth.py."
            )

        credentials = base64.b64encode(
            f"{CLIENT_ID}:{CLIENT_SECRET}".encode()
        ).decode()

        data = urllib.parse.urlencode({
            "grant_type":    "refresh_token",
            "refresh_token": refresh_token,
        }).encode()

        req = urllib.request.Request(
            TOKEN_URL,
            data=data,
            headers={
                "Authorization": f"Basic {credentials}",
                "Content-Type":  "application/x-www-form-urlencoded",
            },
            method="POST",
        )

        with urllib.request.urlopen(req) as resp:
            new_tokens = json.loads(resp.read().decode())

        self._tokens.update(new_tokens)
        expires_in = new_tokens.get("expires_in", 1800)
        self._access_expires_at = time.time() + expires_in - _ACCESS_TOKEN_BUFFER_SECS
        self._save_tokens()

    def _ensure_valid_token(self) -> None:
        if time.time() >= self._access_expires_at:
            self._refresh_access_token()

    def _access_token(self) -> str:
        self._ensure_valid_token()
        return self._tokens["access_token"]

    # ── HTTP helpers ───────────────────────────────────────────────────────────

    def _get(self, url: str, params: dict | None = None) -> Any:
        if params:
            url = url + "?" + urllib.parse.urlencode(params)
        req = urllib.request.Request(
            url,
            headers={"Authorization": f"Bearer {self._access_token()}"},
        )
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read().decode())

    # ── Public API methods ─────────────────────────────────────────────────────

    def get_quote(self, symbol: str) -> dict:
        """
        Get a real-time quote for one symbol.
        Use /ES, /GC, /CL for futures (note the leading slash).
        Returns the raw Schwab quote object for that symbol.
        """
        data = self._get(QUOTES_URL, {"symbols": symbol, "fields": "quote,reference"})
        # Schwab returns { "SYMBOL": { "quote": {...}, "reference": {...} } }
        key = list(data.keys())[0] if data else symbol
        return data.get(key, data)

    def get_quotes(self, symbols: list[str]) -> dict:
        """
        Get real-time quotes for multiple symbols at once.
        Returns { "SYMBOL": quote_dict, ... }
        """
        return self._get(QUOTES_URL, {"symbols": ",".join(symbols), "fields": "quote,reference"})

    def get_price_history(
        self,
        symbol: str,
        period_type: str = "day",
        period: int = 1,
        frequency_type: str = "minute",
        frequency: int = 5,
        need_extended_hours_data: bool = False,
    ) -> dict:
        """
        Get OHLCV candle history.
        Defaults: today's 5-min candles.
        Use for ATR calculation.
        """
        return self._get(HISTORY_URL, {
            "symbol":                symbol,
            "periodType":            period_type,
            "period":                period,
            "frequencyType":         frequency_type,
            "frequency":             frequency,
            "needExtendedHoursData": str(need_extended_hours_data).lower(),
        })

    def get_accounts(self, fields: str = "positions") -> list:
        """Get all linked Schwab accounts with positions."""
        return self._get(ACCOUNTS_URL, {"fields": fields})

    def token_status(self) -> dict:
        """Return a summary of current token health."""
        secs_left = max(0, self._access_expires_at - time.time())
        return {
            "access_token_expires_in_secs": int(secs_left),
            "access_token_expires_in_min":  round(secs_left / 60, 1),
            "has_refresh_token": bool(self._tokens.get("refresh_token")),
            "saved_at": self._tokens.get("saved_at"),
        }


# ── Standalone test ────────────────────────────────────────────────────────────
if __name__ == "__main__":
    print("=" * 60)
    print("  Schwab Client — Live Quote Test")
    print("=" * 60)

    client = SchwabClient()

    print("\n── Token Status ─────────────────────────────────────────")
    status = client.token_status()
    for k, v in status.items():
        print(f"  {k:<35}: {v}")

    symbols = ["/ES", "/GC", "/CL"]
    print(f"\n── Live Quotes: {', '.join(symbols)} ─────────────────────")
    try:
        quotes = client.get_quotes(symbols)
        for sym, data in quotes.items():
            q = data.get("quote", {})
            last  = q.get("lastPrice") or q.get("mark") or "—"
            bid   = q.get("bidPrice", "—")
            ask   = q.get("askPrice", "—")
            chg   = q.get("netChange", "—")
            pct   = q.get("netPercentChangeInDouble", "—")
            print(f"  {sym:<6}  last={last}  bid={bid}  ask={ask}  chg={chg}  ({pct}%)")
    except Exception as e:
        print(f"  ❌ Quote fetch failed: {e}")

    print("\n── 5-min Price History for /ES (last 20 candles) ────────")
    try:
        hist = client.get_price_history("/ES", frequency=5)
        candles = hist.get("candles", [])[-20:]
        for c in candles:
            ts = datetime.fromtimestamp(c["datetime"] / 1000).strftime("%H:%M")
            print(f"  {ts}  O={c['open']:.2f}  H={c['high']:.2f}  L={c['low']:.2f}  C={c['close']:.2f}  V={c['volume']}")
        if not candles:
            print("  (no candles returned — market may be closed)")
    except Exception as e:
        print(f"  ❌ History fetch failed: {e}")

    print("\n✅  Done.")
