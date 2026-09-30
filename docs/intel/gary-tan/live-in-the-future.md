---
type: KnowledgeRule
title: Live in the Future — The $100k/Year 2028-Now Power User Thesis
description: Gary Tan's core product philosophy — spend at tomorrow's consumption level today to access tomorrow's capabilities today. Applied to Arena Platform pricing strategy, early adopter targeting, and the "power user as product proof" narrative.
tags: [product-philosophy, pricing, power-user, 2028, yc-pitch, early-adopter]
threshold: power_user_price_ceiling = 100_000_usd_per_year
timestamp: 2026-06-25T00:00:00Z
source_quote: "If you're just willing to spend $100,000 a year on tokens, you can basically live like you are a normal citizen in 2028. Predict the future by living in it."
---

# Live in the Future — The $100k/Year 2028-Now Power User Thesis

**Standard**: Target early adopters who are already spending at 10-100x average — they are the 2028 normal user arriving early
**Why**: Gary Tan is the proof-of-concept. $100k/year on tokens = 2028 purchasing power at today's prices. In 24-36 months, 90,000x more compute means $100k of today's AI capability will cost < $10. The early adopter is not an outlier — they are a time traveler. Build for them first.

## The Core Insight

```
Gary's Thesis:
  Compute will increase 90,000x in 24-36 months (confirmed by hardware/datacenter pipeline)
  Token costs will fall proportionally
  Capabilities unlock at each order of magnitude
  
  Therefore:
  $100,000/year today = ~$1/year in 2028 for same capability
  = 100,000x cost compression in 36 months
  
  The person spending $100k/year today is not rich — they are early.
  They are buying access to 2028 capabilities at 2025 prices.
  
  Naval's framing: "The most reliable way to predict the future is to live in it."
  Gary's application: live in 2028 by spending like it's 2028.
```

## The Three Types of Power User

| User Type | Spend/Year | What They're Buying | Arena Equivalent |
|-----------|-----------|--------------------|--------------------|
| Gary Tan tier | ~$100k | Full 2028 AI stack — GBrain + eval harness + 10-15 parallel sprints | Enterprise Plan |
| YC founder tier | $10k-$50k | 2-3 parallel agent workflows + eval loop | Team Plan |
| Early adopter | $2k-$10k | Single-agent workflow, growing corpus | Pro Plan |
| Normal 2028 user (arriving early) | $500-$2k | One chub, occasional sessions | Standard Plan |

## Arena Platform Rules

### `power_user_as_proof`
The platform's most important first users are not volume users — they are showcase users.
```
Target: 100 Gary Tan-tier power users in Year 1 (spending >= $1,000/month on Arena)
Why: One power user with results is worth 10,000 passive signups for YC credibility
Metric: Document 10 power user case studies showing corpus growth + ABIF improvement curve
Pitch frame: "Gary built this for himself. These 10 people are doing it on Arena."
Acquisition: direct outreach to YC founders, AI researchers, top GitHub contributors
```

### `price_ceiling_validation`
Arena's enterprise pricing is validated by Gary's spend pattern — don't leave margin on the table.
```
Gary's spend: ~$8,333/month on tokens alone
Arena enterprise ceiling: $5,000/month (40% below Gary's raw token spend)
Positioning: "Less than Gary pays — with ABIF certification and marketplace income."
Pricing rule: never reduce enterprise price below $1,000/month in Year 1 
Rationale: the power user segment has zero price sensitivity on matters of judgment (Gary's words)
```

### `compute_cost_timing_window`
The 12-18 month window before 90,000x compute arrives is the monetization window.
```
Today: token costs are still high → Arena can charge premium with comfortable margins
Year 1: token costs drop 50-80% → margins improve without price change
Year 2: token costs approach near-zero → shift to flat subscription (value is corpus, not compute)
Year 3: compute is free → moat is entirely corpus + ABIF history (no compute component)

Warning: DO NOT wait for compute to be cheap before launching
The window of charging for compute-backed quality is NOW — 12-18 months
After this window, the only value is corpus and certification history
```

### `early_adopter_identification`
Arena's acquisition strategy must target people already living in the future.
```
Signals of a "future resident" early adopter:
  - GitHub: active Claude Code / Codex / OpenClaw user
  - Twitter/X: posting about multi-agent workflows, eval harnesses, skill files
  - Spend pattern: paying for API access directly (not just ChatGPT Plus)
  - Output: shipping AI products or research papers that use agents
  
Channels:
  - gstack GitHub repo — Gary's users are already Arena's target
  - YC W24/S24/W25 founder list — YC founders are living in the future by definition
  - Hacker News "Show HN" for agent tools — active builders, not spectators
```

### `productivity_leverage_claim`
Gary's 810x productivity claim is the benchmark for what Arena promises to power users.
```
Gary's baseline: 2013 coding pace
Gary's 2025 pace: 810x via 10-15 parallel Claude Code sprints + GBrain + eval harness

Arena's claim: "Match Gary's stack with verified credentials. Get paid for it."
Proof requirement: 3 documented case studies of Arena power users showing 10x+ output improvement
Metric: sessions_completed_per_month / pre_arena_baseline > 10x for power user case studies
Measurement period: 90 days post-onboarding vs 90-day pre-onboarding baseline
```

## The YC Pitch Convergence

All five documents in this wiki converge on the same YC pitch:

```
Problem: AI leverage exists but is inaccessible — most people can't build Gary's stack
Solution: Arena Platform = Gary's stack, productized, with marketplace income on top
  - GBrain corpus → Arena chub (corpus-as-moat.md)
  - Eval harness → ABIF certification (error-compounding.md)
  - LSD Mode → Brainstorm Session type (lsd-brainstorm-mode.md)
  - $2.84/user infrastructure → Arena session economics (cost-per-agent.md)
  - CLI agnosticism → Arena Rig Plugin (harness-is-commodity.md)
  - Open-source models → Arena Open tier (open-source-model-strategy.md)

Why now: 90,000x compute increase. 12-18 month window. Standards are winner-take-all.
Gary Tan is the pitch. He already built it. We're making it available to everyone.
```

## Measurable Outcomes

| Metric | Target | Rationale |
|--------|--------|-----------|
| Enterprise tier power users (>= $1k/mo) | >= 100 in Year 1 | Showcase pool, not volume |
| Average power user corpus size at 6 months | >= 100k files | Approaching Gary's 400k asymptote |
| Power user ABIF score improvement | >= +15% at 90 days | Eval loop is compounding |
| Power user monthly sessions | >= 200/month | Active use, not just registration |
| Documented case studies showing 10x+ output | >= 10 by Month 12 | Proof for YC pitch |

## Cross-References
- [corpus-as-moat.md](corpus-as-moat.md) — the long-term stickiness layer
- [cost-per-agent.md](cost-per-agent.md) — the infrastructure economics
- [harness-is-commodity.md](harness-is-commodity.md) — why CLI doesn't lock in the power user
- [lsd-brainstorm-mode.md](lsd-brainstorm-mode.md) — the signature premium feature
