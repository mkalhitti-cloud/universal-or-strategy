---
type: KnowledgeRule
title: Corpus as Moat — The Knowledge Asset Thesis
description: Gary Tan's GBrain corpus (400k markdown files) is the moat, not the harness. Applied to Arena Platform chub design and creator retention strategy.
tags: [corpus, moat, chub, gbrain, creator-retention, ckpf]
threshold: corpus_size >= 10_000_files_before_monetization
timestamp: 2026-06-25T00:00:00Z
source_quote: "It has my whole corpus — all my emails, all my Slacks, all my text messages, my DMs, everything. 400,000 markdown files on literally anything I've ever thought or read."
---

# Corpus as Moat — The Knowledge Asset Thesis

**Standard**: Corpus >= 10,000 indexed files before a chub is listed for monetization
**Why**: Corpus compounds over time. CLI switches in 24 hours. The corpus never migrates.

## What Gary Actually Built

Gary's GBrain is three distinct layers:
1. **Retrieval engine** (gstack — MIT, public) — the code that indexes and queries
2. **Personal corpus** (private) — 400k markdown files: all emails, Slacks, texts, DMs, readings, thoughts
3. **Eval harness** (private) — crossmodal multi-model re-ranking to improve SKILL.md quality

The public repo contains (1). The moat is (2). The quality compounding is (3).

## The Chub Parallel

| GBrain Component | Arena Platform Equivalent |
|------------------|--------------------------|
| gstack retrieval engine | Arena chub runtime (platform infrastructure) |
| 400k markdown corpus | Creator's chub (privately indexed, publicly queryable via sessions) |
| SKILL.md files | CKPF-versioned skill files inside the chub |
| Crossmodal eval harness | ABIF quality gate on chub delta score |
| LSD Mode output | Brainstorm session type (premium chub tier) |

## Arena Platform Rules

### `corpus_minimum_before_listing`
A chub must reach a minimum corpus threshold before it earns a public listing in the marketplace.
```
Threshold: >= 10,000 indexed documents
Rationale: Below this size, retrieval quality is too low to differentiate from base model
Metric: chub.indexed_doc_count >= 10_000
Enforcement: Platform blocks public listing until threshold met; shows progress bar
```

### `corpus_stickiness_score`
Every chub tracks a Stickiness Score: how hard it is to replicate this corpus from scratch.
```
Score = f(corpus_size, source_diversity, years_of_data, eval_improvement_delta)
High score (>0.8): Creator has unique data only they own (personal emails, proprietary notes)
Low score (<0.4): Creator has only public data (anyone could replicate it in a week)
Display: shown on chub marketplace listing as "Corpus Depth" signal
```

### `compounding_eval_loop`
A chub that runs ABIF evals grows its quality score over time. The eval loop must be designed to compound.
```
Week 1:  Baseline ABIF score = S0
Week 4:  After 100 sessions + eval feedback = S0 + delta
Week 12: After 400 sessions = S0 + 3*delta (if corpus grows with usage)
Rule: Any chub with negative eval delta after 30 sessions → flagged for creator review
```

### `private_corpus_public_interface`
The corpus content stays private. Only the query results (session outputs) are shared.
```
Creator owns: all indexed documents, all SKILL.md files, all eval data
Platform owns: the session outputs (for ABIF scoring), the ELO history
Buyer gets: the session query result — never the raw corpus
Analogy: You can query Gary's GBrain but you can't download his emails
```

## Complexity Impact

These rules keep Arena Platform's chub management logic simple and auditable:
- `corpus_minimum_before_listing` → single threshold check (CYC 1)
- `corpus_stickiness_score` → computed at ingestion time, cached (CYC 3-4)
- `compounding_eval_loop` → scheduled job, not inline logic (CYC 2)
- `private_corpus_public_interface` → enforced at API gateway, not business logic (CYC 1)

## Measurable Outcomes

| Metric | Target | Rationale |
|--------|--------|-----------|
| Median corpus size at listing | >= 50,000 files | High stickiness from day one |
| Creator churn after 90 days | <= 5% monthly | Corpus lock-in prevents switching |
| Average corpus age at 6 months | >= 6 months of data | Time-depth = irreplaceable |
| Eval delta improvement rate | >= +2% ABIF per month | Proves compounding is working |

## Cross-References
- [harness-is-commodity.md](harness-is-commodity.md) — why the CLI layer doesn't matter
- [lsd-brainstorm-mode.md](lsd-brainstorm-mode.md) — what a large corpus enables
- [error-compounding.md](error-compounding.md) — why corpus quality floor matters
