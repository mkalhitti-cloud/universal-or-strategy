---
type: KnowledgeRule
title: Cost Per Agent — Token Economics and the $2.84 Floor
description: Farbood (A-List) drove per-user agent cost from $100/month to $2.84 in 3-4 months. Gary Tan spends ~$100k/year to live like 2028. Applied to Arena Platform pricing and infrastructure strategy.
tags: [cost, token-economics, infrastructure, pricing, elastic-fleet]
threshold: target_cost_per_user_month <= 3.00
timestamp: 2026-06-25T00:00:00Z
source_quote: "Every single person in our app has their own OpenClaw running for them. We started at $100/month/person. We spent 3-4 months driving that down to $2.84."
---

# Cost Per Agent — Token Economics and the $2.84 Floor

**Standard**: Target per-user agent cost <= $3/month within 6 months of launch
**Why**: Farbood proved 97% cost reduction is achievable in 3–4 months. The $2.84 floor is the competitive benchmark for production agentic apps. Above this, you're leaving margin on the table. Below this, you're within margin to offer a free tier.

## The Data Points

| Source | Starting Cost | Ending Cost | Time | Method |
|--------|--------------|-------------|------|--------|
| Farbood / A-List | $100/user/month (OpenClaw + Opus) | $2.84/user/month | 3-4 months | Full stack rebuild + eval harness + elastic fleet |
| Gary Tan / personal | ~$100,000/year (~$8,333/month) | Still spending it | Ongoing | Intentional — power user "living in 2028" |
| Gary on compute future | Current token costs | 90,000x cheaper in 24-36 months | Projected | Hardware + datacenter buildout curve |

## The Power User Equation

Gary's $100k/year is not waste — it's deliberate arbitrage:
```
Today's cost: $100,000/year
2028 equivalent purchasing power at 90,000x compute increase: ~$1/year for same capability
Net gain: paying today's prices to access 2028 capabilities NOW
YC pitch frame: "$100k/year buys you 2028-level leverage today. That's the best ROI available."
```

## Arena Platform Rules

### `elastic_fleet_architecture`
Agent infrastructure must scale to zero between sessions — no idle cost.
```
Pattern: spawn-on-demand, terminate-on-completion
Metric: idle_agent_cost = $0.00 (no persistent agents between sessions)
Target: cost scales linearly with active session count (no floor cost)
Implementation: serverless/container-per-session, not persistent VM per user
Rationale: Farbood's $2.84 required elastic fleet — not a fixed allocation per user
```

### `cost_floor_target`
Arena Platform internal cost target for agent sessions.
```
Target: <= $0.50/session (infrastructure cost, not billable price)
Rationale: At $0.50 cost → $5.00 price → 10x gross margin
Free tier: 10 sessions/month = $5.00 infrastructure cost → offset by paid conversion
Pro tier: 500 sessions/month = $250 infrastructure cost → priced at $99 → still margin-positive on volume
Enterprise tier: cost-plus model with 3x markup minimum
```

### `model_cost_tiering`
Not every session requires frontier models. Route based on task complexity.
```
Tier 1 (simple retrieval): open-source model (Minimax / GLM) — 5-10x cheaper
Tier 2 (multi-step reasoning): mid-tier API (Sonnet / GPT-4o-mini)
Tier 3 (complex judgment): frontier model (Opus / GPT-5)
Routing rule: ABIF benchmark determines which tier a certified agent can use
Cost impact: Tier 1 vs Tier 3 = ~10x cost difference per session
```

### `token_cost_curve_bet`
Platform pricing strategy must assume 90,000x compute increase in 24-36 months.
```
Today: charge based on today's token costs
Year 1: token costs drop 50-80% → margin improves without price change
Year 2: token costs drop 90-99% → move to flat subscription (cost < $0.01/session)
Bet: today's infrastructure investment lock-in becomes near-free at scale
Warning: DO NOT price to zero early — capture the margin window while compute is expensive
```

### `power_user_price_ceiling`
Gary Tan proves the $100k/year willingness-to-pay ceiling for a serious power user.
```
Power user TAM: developers, founders, researchers willing to spend $10k-$100k/year
Arena enterprise tier ceiling: $5,000/month = $60,000/year (still 40% below Gary's spend)
Psychological anchor: "For less than Gary pays, you get Gary-level leverage — certified."
Price sensitivity: ZERO for this segment on matters of judgment (Gary's exact words)
```

## Measurable Outcomes

| Metric | Target | Rationale |
|--------|--------|-----------|
| Infrastructure cost per session | <= $0.50 | Enables 10x gross margin at $5/session price |
| Per-user monthly cost (free tier) | <= $5.00 | 10 free sessions, sub-dollar each |
| Time to hit Farbood floor ($2.84/user) | <= 6 months post-launch | Proven achievable in 3-4 months |
| Gross margin at Pro tier | >= 70% | Software-grade margins on agent sessions |
| Revenue impact of compute cost drop | +15-20% margin/year | Built into Year 2 financial model |

## Complexity Impact

Cost routing logic must stay simple or it becomes a maintenance burden:
- `model_cost_tiering` → Strategy pattern over task complexity score (CYC 2-3)
- `elastic_fleet_architecture` → event-driven spawn/terminate (no polling loop, CYC 2)
- `token_cost_curve_bet` → pricing table update, not code logic (CYC 1)

## Cross-References
- [open-source-model-strategy.md](open-source-model-strategy.md) — Tier 1 model selection
- [harness-is-commodity.md](harness-is-commodity.md) — cost reduction is engineering, not moat
- [live-in-the-future.md](live-in-the-future.md) — the $100k/year power user thesis
