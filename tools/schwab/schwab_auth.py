"""
schwab_auth.py -- One-time OAuth login for Schwab API
Run this script once to get your tokens.json file.
After that, use schwab_client.py for all API calls.

Usage:
    python tools/schwab/schwab_auth.py
"""

import base64
import json
import os
import urllib.parse
import urllib.request
from datetime import datetime, timezone

# ── Credentials ────────────────────────────────────────────────────────────────
CLIENT_ID     = "xMjwtQ1XkHtsF2MhRughbR5ujpr22VqpU2uUZVQaTO0Hvj2X"
CLIENT_SECRET = "cj2el9hoyG1YGSkkHsyG4Gj0Upj0COtYFW8qIBkE2cPty0inojM19dmTsy3L4OjI"
REDIRECT_URI  = "https://127.0.0.1"
TOKENS_FILE   = os.path.join(os.path.dirname(__file__), "tokens.json")

AUTH_URL  = "https://api.schwabapi.com/v1/oauth/authorize"
TOKEN_URL = "https://api.schwabapi.com/v1/oauth/token"


def build_auth_url():
    params = {
        "response_type": "code",
        "client_id":     CLIENT_ID,
        "redirect_uri":  REDIRECT_URI,
    }
    return AUTH_URL + "?" + urllib.parse.urlencode(params)


def exchange_code_for_tokens(code):
    credentials = base64.b64encode(
        "{}:{}".format(CLIENT_ID, CLIENT_SECRET).encode()
    ).decode()

    data = urllib.parse.urlencode({
        "grant_type":   "authorization_code",
        "code":         code,
        "redirect_uri": REDIRECT_URI,
    }).encode()

    req = urllib.request.Request(
        TOKEN_URL,
        data=data,
        headers={
            "Authorization": "Basic {}".format(credentials),
            "Content-Type":  "application/x-www-form-urlencoded",
        },
        method="POST",
    )

    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode())


def save_tokens(token_data):
    token_data["saved_at"] = datetime.now(timezone.utc).isoformat()
    with open(TOKENS_FILE, "w") as f:
        json.dump(token_data, f, indent=2)
    print("\n  Tokens saved to: {}".format(TOKENS_FILE))


def main():
    print("=" * 60)
    print("  Schwab OAuth Login -- WSGTA Pro")
    print("=" * 60)

    url = build_auth_url()
    print("\n  STEP 1 -- Open this URL in your browser and log in:\n")
    print("  {}\n".format(url))
    print("  After logging in, Schwab redirects to https://127.0.0.1?code=...")
    print("  The page shows an error -- that is normal.")
    print("  Copy the FULL URL from the browser address bar.\n")

    raw = input("  STEP 2 -- Paste the full redirect URL here:\n> ").strip()

    parsed = urllib.parse.urlparse(raw)
    params = urllib.parse.parse_qs(parsed.query)

    if "code" not in params:
        print("\n  ERROR: No 'code' parameter found in that URL.")
        print("  Make sure you copied the entire URL from the address bar.")
        return

    code = urllib.parse.unquote(params["code"][0])
    print("\n  Code extracted: {}...".format(code[:20]))

    print("\n  Exchanging code for tokens...")
    try:
        token_data = exchange_code_for_tokens(code)
    except urllib.error.HTTPError as e:
        raw_body = e.read()
        try:
            import gzip
            body = gzip.decompress(raw_body).decode("utf-8", errors="replace")
        except Exception:
            body = raw_body.decode("utf-8", errors="replace")
        print("\n  ERROR: Token exchange failed HTTP {}".format(e.code))
        print("  Response: {}".format(body))
        return

    save_tokens(token_data)

    print("\n-- Token Summary " + "-" * 43)
    print("  access_token  : {}...".format(token_data.get("access_token", "")[:30]))
    print("  refresh_token : {}...".format(token_data.get("refresh_token", "")[:30]))
    expires_in = token_data.get("expires_in", 1800)
    print("  expires_in    : {}s ({} min)".format(expires_in, expires_in // 60))
    print("  token_type    : {}".format(token_data.get("token_type", "Bearer")))
    print("-" * 60)
    print("\n  Done! Run schwab_client.py next to test a live quote.")


if __name__ == "__main__":
    main()
