---
name: api-design-principles
description: >
  Best practices for designing, documenting, and consuming REST/WebSocket/gRPC APIs in the V12 project.
  Use when designing new API endpoints, integrating broker APIs (Schwab TOS, Rithmic, NinjaTrader),
  building MCP servers, or reviewing API contracts for correctness, security, and performance.
  Keywords: API design, REST, WebSocket, gRPC, broker API, Schwab, Rithmic, MCP, endpoint design.
---

# API Design Principles Skill

## Overview

Consistent, well-designed APIs are the connective tissue of the V12 Sovereign ecosystem. This skill covers REST, WebSocket, and gRPC patterns for both internal services and external broker integrations (Schwab TOS, Rithmic, NinjaTrader AT).

## REST API Design

### URL Structure

```
GET    /v1/resources          # List
GET    /v1/resources/{id}     # Read
POST   /v1/resources          # Create
PUT    /v1/resources/{id}     # Replace
PATCH  /v1/resources/{id}     # Partial update
DELETE /v1/resources/{id}     # Delete
```

**Rules:**

- Use **nouns**, never verbs, in URLs (`/orders` not `/createOrder`)
- Version all APIs (`/v1/`, `/v2/`) — never break existing contracts silently
- Use kebab-case for multi-word paths (`/market-data`, `/stop-orders`)
- Use query params for filtering, sorting, pagination (`?status=active&limit=50`)

### HTTP Status Codes

| Code | Meaning                    |
| ---- | -------------------------- |
| 200  | Success                    |
| 201  | Created                    |
| 204  | No content (delete)        |
| 400  | Bad request (client error) |
| 401  | Unauthorized               |
| 403  | Forbidden                  |
| 404  | Not found                  |
| 409  | Conflict (duplicate, race) |
| 429  | Rate limited               |
| 500  | Internal server error      |
| 503  | Service unavailable        |

### Request/Response Format

```json
{
  "data": { ... },
  "meta": { "timestamp": "2026-01-15T10:00:00Z", "requestId": "abc123" },
  "error": null
}
```

- Always include `requestId` for tracing
- Errors: `{ "error": { "code": "ORDER_REJECTED", "message": "...", "details": {} } }`
- Use ISO 8601 timestamps (UTC)
- Amounts in smallest denomination (cents for USD, ticks for futures)

## WebSocket API Design

### Connection Management

```javascript
// Always implement: connect → authenticate → subscribe → heartbeat → reconnect
ws.onopen = () => authenticate(ws, apiKey);
ws.onmessage = (msg) => route(JSON.parse(msg.data));
ws.onclose = () => reconnectWithBackoff();
ws.onerror = (err) => logAndReconnect(err);
```

**Rules:**

- Implement exponential backoff: `delay = min(1000 * 2^attempt, 30000)ms`
- Send heartbeat ping every 30s; expect pong within 10s
- All messages must include `type`, `timestamp`, `sequenceId`
- Use binary frames (MessagePack/Protobuf) for high-frequency data (>100msg/s)

### Message Schema

```json
{
  "type": "ORDER_UPDATE",
  "sequenceId": 94827,
  "timestamp": "2026-01-15T10:00:00.123Z",
  "payload": { ... }
}
```

## Broker API Integration Patterns (V12-Specific)

### Schwab TOS API

- OAuth2 PKCE flow for auth; refresh token 30min before expiry
- Use `/accounts/{accountId}/orders` for order management
- Rate limit: 120 req/min per endpoint — implement token bucket client-side
- Always check `order.status` async via WebSocket, not polling
- **V12 Rule**: Never submit bracket orders via separate calls — use the composite `complexOrderStrategyType` field

### Rithmic / NinjaTrader AT

- Follow the two-phase Replace FSM for follower order cancel+resubmit (see GEMINI.md §3)
- Never call raw `Cancel()` + `Submit()` in sequence — ghost order risk
- Use `OnAccountOrderUpdate` confirmation gate before transitioning FSM states
- All order references must be stored in `stopOrders` dict via **Direct Write** (not Enqueue) per Build 981 protocol

### MCP Server Design

- Every tool must return typed JSON with consistent error envelope
- Implement idempotency keys for state-mutating tools
- Use `waitForPreviousTools: true` to serialize dependent calls
- Document all tools in `README.md` with input/output schema

## Security Principles

- **Never log API keys** — redact in all Print() and log statements
- **Validate all inputs** — reject unknown fields (strict schema parsing)
- **Use TLS everywhere** — no plaintext for financial data
- **Rotate credentials** — implement automated refresh; alert on manual rotation
- **Rate limit client-side** — implement before hitting broker limits, not after

## Documentation Standard

For every API endpoint/tool, document:

1. **Purpose** — what it does and when to use it
2. **Request schema** — all fields with types and validation rules
3. **Response schema** — success and error shapes
4. **Rate limits** — if applicable
5. **Side effects** — state mutations, order submissions, etc.
6. **Examples** — at least one request/response pair

## Post-Use Audit

After every use:

1. Are all endpoints versioned?
2. Are error responses using the standard envelope format?
3. Are all broker-specific patterns (Schwab composite orders, Rithmic FSM) followed?
4. Are API keys protected from logs?

State: `skill(api-design-principles): no gaps identified.` or document fix.
