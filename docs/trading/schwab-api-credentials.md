# Schwab Developer API — WSGTA Pro App

## App Details

| Field | Value |
|-------|-------|
| **App Name** | WSGTA Pro |
| **App Machine Name** | prod-mkaiqyahoocom-453c068e-fdef-4215-9c68-e75545d39fab |
| **Creator** | mka.iq@yahoo.com |
| **Create Date** | 07/27/2026 |
| **App Description** | Signal monitor and trade tools for ES, GC, CL futures |
| **Environment** | Production |
| **Status** | Ready For Use |
| **Callback URL** | https://127.0.0.1 |
| **Order Limit** | 120 |
| **Key Issued Date** | 07/27/2026 |
| **Last Modified Date** | 07/27/2026 |
| **Portal URL** | https://developer.schwab.com/dashboard/apps/app/193daa69-e339-4359-9857-c3551dcbf568 |

## API Products Enabled

- Accounts and Trading Production
- Market Data Production

## Credentials

| Field | Value |
|-------|-------|
| **Client ID (App Key)** | xMjwtQ1XkHtsF2MhRughbR5ujpr22VqpU2uUZVQaTO0Hvj2X |
| **Client Secret** | cj2el9hoyG1YGSkkHsyG4Gj0Upj0COtYFW8qIBkE2cPty0inojM19dmTsy3L4OjI |

## .env template

```env
SCHWAB_CLIENT_ID=xMjwtQ1XkHtsF2MhRughbR5ujpr22VqpU2uUZVQaTO0Hvj2X
SCHWAB_CLIENT_SECRET=cj2el9hoyG1YGSkkHsyG4Gj0Upj0COtYFW8qIBkE2cPty0inojM19dmTsy3L4OjI
SCHWAB_REDIRECT_URI=https://127.0.0.1
```

## OAuth Flow (next step)

The one-time login flow:
1. Script builds an authorization URL using Client ID + Callback URL
2. You open it in a browser → log in to Schwab → it redirects to `https://127.0.0.1?code=XXXX`
3. Copy the full redirect URL from the browser address bar
4. Script exchanges the code for `access_token` + `refresh_token`
5. Tokens stored in `tokens.json` — refresh token valid ~7 days, auto-renewed on use

## Key Endpoints

| Purpose | Endpoint |
|---------|----------|
| Real-time quotes | `GET https://api.schwabapi.com/marketdata/v1/quotes` |
| Price history (ATR) | `GET https://api.schwabapi.com/marketdata/v1/pricehistory` |
| Streaming market data | `GET https://api.schwabapi.com/marketdata/v1/stream` |
| Account details | `GET https://api.schwabapi.com/trader/v1/accounts` |
| Orders | `GET/POST https://api.schwabapi.com/trader/v1/accounts/{accountId}/orders` |

## Notes

- Rate limit: 120 requests/min
- Futures symbols: `/ES`, `/GC`, `/CL` (note the leading slash for futures)
- Access token expires every 30 minutes — refresh automatically using refresh_token
- Refresh token expires every 7 days — requires re-running the browser OAuth login
