---
type: KnowledgeIndex
title: Gary Tan Intelligence Wiki
description: Distilled business and product principles from Gary Tan (YC president, gstack builder) — sourced from Naval podcast transcript. Applied to Arena Platform product decisions and Arena Platform go-to-market strategy.
tags: [gary-tan, product, marketplace, corpus, eval, cost, moat, arena-platform]
timestamp: 2026-06-25T00:00:00Z
version: "1.0"
format: OKF-v0.1
source: docs/Video Transcripts/Riding AGI, AI Anxiety, Who Funded COVID, Defending Taiwan, and California Empire
---

# Gary Tan Intelligence Wiki

**Format**: Open Knowledge Format (OKF) v0.1
**Source**: Naval podcast — Gary Tan, Farbood (A-List), Daniel (Able Police), Naval Ravikant
**Status**: MANDATORY for Arena Platform product and go-to-market decisions
**Parallel to**: `docs/intel/jane-street/` (V12 architectural rules)

## Documents

| File | Topic | Threshold / Metric |
|------|-------|-------------------|
| [corpus-as-moat.md](corpus-as-moat.md) | GBrain corpus = chub stickiness | Corpus size >= 10k files before monetization |
| [cost-per-agent.md](cost-per-agent.md) | Token economics, per-user cost reduction | Target <= $3/user/month within 6 months of launch |
| [harness-is-commodity.md](harness-is-commodity.md) | CLI switching cost = near-zero | Moat = corpus + ABIF score, NOT the CLI |
| [error-compounding.md](error-compounding.md) | 99.9% vs 90% accuracy in recursive loops | Quality floor >= 99% for ABIF-certified agents |
| [open-source-model-strategy.md](open-source-model-strategy.md) | Open-source 5-10x cheaper, needs good harness | Arena must be model-agnostic from day 1 |
| [lsd-brainstorm-mode.md](lsd-brainstorm-mode.md) | Divergent-vector RAG for idea generation | Product spec: Brainstorm Mode session type |
| [live-in-the-future.md](live-in-the-future.md) | $100k/yr spend = 2028-now power user thesis | Price ceiling proven; enterprise tier validated |

## Query Protocol

```
read_file("docs/intel/gary-tan/<topic>.md")
```

## Core Principle (Gary Tan — one sentence)

> "If you have an eval harness, a big enough corpus, and crossmodal eval — you can improve the skill file to a point where it's indistinguishable."

This is the entire Arena Platform product thesis in one sentence: **corpus + eval harness + ABIF score = defensible moat**.

## Cross-References

- [harness-is-commodity.md](harness-is-commodity.md) → [corpus-as-moat.md](corpus-as-moat.md) (why CLI doesn't matter)
- [cost-per-agent.md](cost-per-agent.md) → [open-source-model-strategy.md](open-source-model-strategy.md) (cost reduction path)
- [lsd-brainstorm-mode.md](lsd-brainstorm-mode.md) → [corpus-as-moat.md](corpus-as-moat.md) (LSD requires large corpus)
- [error-compounding.md](error-compounding.md) → [open-source-model-strategy.md](open-source-model-strategy.md) (when to upgrade model tier)
