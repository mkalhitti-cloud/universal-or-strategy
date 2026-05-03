---
name: API Design Principles
description: >
  Professional REST and GraphQL API design guidelines.
  Ensures consistent, versioned, and secure integration with external targets like Schwab TOS.
---

# API Design Principles (Professional Standard)

## Core Logic

- **Resource-Oriented**: Use nouns for resources (`/orders`) and HTTP methods for actions.
- **Strict Versioning**: Use header-based versioning to prevent breaking changes in the Sovereign Brain.
- **Idempotency**: Ensure order submissions are idempotent via client-side transaction IDs.

## Instructions

1.  **Error Mapping**: Map external API errors (e.g., TOS status codes) to internal Sovereign safety states.
2.  **Rate Limiting**: Implement token-bucket rate limiting for all outbound calls.
3.  **Audit Logs**: Log every outbound payload and its corresponding signed response.

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(api-design): no gaps identified`.
