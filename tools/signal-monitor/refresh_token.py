"""
refresh_token.py — Run this ANY TIME before starting the server to ensure
the Schwab access token is fresh. Also used as a standalone background keepalive.

Usage:
    python refresh_token.py          # refresh once and exit
    python refresh_token.py --watch  # refresh every 25 min forever (run in separate terminal)
"""
import base64
import json
import sys
import time
from pathlib import Path

import httpx
from dotenv import load_dotenv

load_dotenv()

TOKEN_URL  = "https://api.schwabapi.com/v1/oauth/token"
TOKEN_FILE = Path(__file__).parent / ".token_cache.json"
ENV_FILE   = Path(__file__).parent / ".env"

REFRESH_INTERVAL = 25 * 60  # seconds — refresh every 25 min (access token lasts 30)


def _read_env() -> tuple[str, str]:
    env: dict[str, str] = {}
    if ENV_FILE.exists():
        for line in ENV_FILE.read_text().splitlines():
            if "=" in line and not line.startswith("#"):
                k, v = line.split("=", 1)
                env[k.strip()] = v.strip()
    import os
    key    = env.get("SCHWAB_APP_KEY",    os.getenv("SCHWAB_APP_KEY", ""))
    secret = env.get("SCHWAB_APP_SECRET", os.getenv("SCHWAB_APP_SECRET", ""))
    return key, secret


def refresh_now() -> bool:
    """Refresh the access token. Returns True on success."""
    key, secret = _read_env()
    if not key or key == "your_app_key_here":
        print("[refresh] No SCHWAB_APP_KEY — skipping.")
        return False

    if not TOKEN_FILE.exists():
        print("[refresh] No token cache found — run server.py --auth first.")
        return False

    tok = json.loads(TOKEN_FILE.read_text())
    secs_left = tok.get("expires_at", 0) - time.time()

    if secs_left > 120:
        print("[refresh] Token still valid ({:.0f}s left) — no refresh needed.".format(secs_left))
        return True

    print("[refresh] Token expires in {:.0f}s — refreshing...".format(secs_left))
    creds = base64.b64encode("{}:{}".format(key, secret).encode()).decode()

    try:
        resp = httpx.post(
            TOKEN_URL,
            headers={
                "Authorization": "Basic " + creds,
                "Content-Type": "application/x-www-form-urlencoded",
            },
            data={
                "grant_type":    "refresh_token",
                "refresh_token": tok["refresh_token"],
            },
            timeout=20,
        )
        resp.raise_for_status()
    except Exception as exc:
        print("[refresh] ERROR: {}".format(exc))
        return False

    new_tok = resp.json()
    new_tok["expires_at"] = time.time() + new_tok.get("expires_in", 1800)
    if "refresh_token" not in new_tok:
        new_tok["refresh_token"] = tok["refresh_token"]
    TOKEN_FILE.write_text(json.dumps(new_tok, indent=2))
    print("[refresh] Done. New token expires in {:.0f}s.".format(new_tok.get("expires_in", 1800)))
    return True


def main() -> None:
    if "--watch" in sys.argv:
        print("[refresh] Watch mode — refreshing every {} minutes.".format(REFRESH_INTERVAL // 60))
        while True:
            refresh_now()
            print("[refresh] Sleeping {} min...".format(REFRESH_INTERVAL // 60))
            time.sleep(REFRESH_INTERVAL)
    else:
        refresh_now()


if __name__ == "__main__":
    main()
