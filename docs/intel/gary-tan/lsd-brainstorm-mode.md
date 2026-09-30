---
type: KnowledgeRule
title: LSD Brainstorm Mode — Divergent-Vector RAG for Idea Generation
description: Gary Tan's GBrain LSD (Lateral Sarcastic Drift) mode cross-products non-aligned vector spaces, re-ranks via 3-4 frontier models + Exa, and surfaces novel idea combinations. Applied to Arena Platform Brainstorm Session type specification.
tags: [lsd, brainstorm, rag, divergent-vectors, chub, session-type, gbrain]
threshold: brainstorm_mode_requires_corpus_size >= 50_000_files
timestamp: 2026-06-25T00:00:00Z
source_quote: "It's basically a collider of different vector spaces. Take every vector space not pointed in the same direction, cross them, do a re-ranking across all these ideas. It just finds bangers. 3-4 frontier models re-rank. Exa cross-reference. Finding ideas at ridiculous scale."
---

# LSD Brainstorm Mode — Divergent-Vector RAG for Idea Generation

**Standard**: Brainstorm Mode is a premium session type requiring corpus size >= 50,000 files
**Why**: Standard RAG finds similar things — it returns vectors that agree. LSD Mode does the opposite: it finds vectors that *disagree*, crosses the tension, and re-ranks the surprise. This is only valuable when the corpus is large enough to contain genuine intellectual diversity. Below 50k files, there isn't enough tension to surface non-obvious ideas.

## What LSD Mode Actually Is

Standard RAG (what everyone builds):
```
Query → find most similar vectors → return top K matches
Result: confirms what you already think
```

LSD Mode (Gary's GBrain):
```
Query → find LEAST similar vectors that still share some topical anchor
       → cross-product the divergent embeddings  
       → re-rank across 3-4 frontier models ("does this combination make sense?")
       → cross-reference against Exa (real-world evidence for the idea)
       → surface top N combinations as "bangers"
Result: ideas at the intersection of concepts that don't usually touch
```

Gary's naming: **L**ateral **S**arcastic **D**rift — intentionally crossing orthogonal idea spaces.

## The Algorithm (Inferred from Gary's Description)

```python
# Conceptual pseudocode — not Gary's actual implementation
def lsd_brainstorm(corpus, query, n_bangers=10):
    # Step 1: Find topical anchor vectors (standard RAG)
    anchor_vecs = corpus.search(query, top_k=50)
    
    # Step 2: Find divergent vectors (anti-similar search)
    all_vecs = corpus.all_embeddings()
    divergent_vecs = find_orthogonal(anchor_vecs, all_vecs, top_k=200)
    # "take every vector space NOT pointed in the same direction"
    
    # Step 3: Cross-product divergent + anchor pairs
    candidate_combinations = cross_product(anchor_vecs, divergent_vecs)
    # Each combination = one potential "banger" idea
    
    # Step 4: Multi-model re-ranking
    scored = []
    for combo in candidate_combinations:
        scores = [model.rate_novelty(combo) for model in [claude, gpt5, minimax, gemini]]
        scored.append((combo, mean(scores)))
    
    # Step 5: Exa real-world validation
    top_candidates = sorted(scored)[:50]
    for combo, score in top_candidates:
        exa_evidence = exa.search(combo.summary)
        if exa_evidence.supports and not exa_evidence.already_exists:
            combo.score += 0.2  # boost for novel + real-world-validated
    
    return sorted(top_candidates)[:n_bangers]
```

## Arena Platform Session Type Specification

### `brainstorm_session_type`
A first-class session type in the Arena Platform marketplace.
```
Session type name: "Brainstorm Mode"
Badge on chub listing: "LSD-Enabled" (requires corpus_size >= 50,000 files)
Input: natural language prompt from buyer
Output: 10-20 ranked idea combinations with evidence links
Differentiator: not a list of obvious ideas — a list of non-obvious intersections
Pricing: 3-5x standard session price (compute-intensive, multi-model re-ranking)
```

### `divergent_retrieval_requirement`
Brainstorm Mode must use divergent retrieval, not standard similarity search.
```
Standard retrieval: cosine_similarity(query, doc) → closest matches
Divergent retrieval: orthogonality_score(query, doc) → least similar that share topic anchor
Implementation: L2 distance + topic filter (not cosine similarity)
Test: outputs must have avg pairwise similarity <= 0.4 (genuine divergence)
Anti-pattern: do NOT use standard RAG for brainstorm mode — it returns what user already knows
```

### `multi_model_reranking`
Brainstorm session must use >= 2 frontier models for re-ranking candidate idea combinations.
```
Minimum: 2 models (e.g., Claude + GPT-5)
Recommended: 3-4 models (includes one open-source, e.g., Minimax or GLM 5.2)
Re-ranking prompt: "Rate the novelty and viability of this idea combination: [combo]. 
                    Score 0-1. Penalize if: (a) obvious combination, (b) already a known product."
Final score: weighted average across all models
Tie-breaking: Exa search confirms real-world evidence exists but no existing product
```

### `corpus_freshness_requirement`
LSD Mode output quality degrades on stale corpus.
```
Max corpus age for Brainstorm session: 30 days
If corpus has not been updated in > 30 days: disable LSD Mode, show "Corpus Stale" badge
Rationale: divergent vector spaces depend on recent intellectual input — stale = less tension
Creator responsibility: update corpus (re-ingest new emails, Slacks, notes) before each batch
```

### `banger_quality_gate`
A banger is a valid LSD output only if it passes a quality threshold.
```
Valid banger criteria:
  - Novelty score (multi-model) >= 0.7
  - Real-world evidence from Exa: exists
  - No existing direct product: confirmed by Exa search
  - Pairwise similarity to other bangers in session: <= 0.5 (diverse outputs)
  
Invalid output (filtered before delivery):
  - "Just combine X and Y" (obvious) → novelty score < 0.7
  - "This already exists as ProductZ" → Exa finds direct competitor
  - Duplicate of earlier banger in session → pairwise similarity > 0.5
```

## Product Differentiation

| Session Type | RAG Method | Models | Best For |
|-------------|-----------|--------|----------|
| Standard chub session | Similarity (cosine) | 1 model | Information retrieval, Q&A |
| Expert consultation | Similarity + rerank | 1-2 models | Domain-expert questions |
| Brainstorm Mode (LSD) | Divergent (orthogonal) | 3-4 models | Novel idea generation |

## Measurable Outcomes

| Metric | Target | Rationale |
|--------|--------|-----------|
| Corpus size floor for LSD Mode | >= 50,000 files | Minimum for genuine divergence |
| Avg pairwise similarity between bangers | <= 0.4 | Ensures truly diverse outputs |
| Multi-model novelty score | >= 0.7 | Filters obvious combinations |
| Session price premium vs standard | 3-5x | Compute + quality justification |
| Creator corpus update frequency | >= monthly | Freshness requirement |

## Complexity Impact

LSD pipeline is naturally decomposable into small functions:
- `find_orthogonal(anchor, corpus)` → filter + sort by distance (CYC 3)
- `cross_product(anchors, divergent)` → generator, yields pairs (CYC 2)
- `multi_model_reranking(combos, models)` → parallel async calls, aggregate (CYC 4)
- `banger_quality_gate(combo)` → 4 independent boolean checks (CYC 4)
- Total pipeline: orchestrator calls 4 helpers — each CYC <= 4, orchestrator CYC <= 6

## Cross-References
- [corpus-as-moat.md](corpus-as-moat.md) — corpus size requirement for LSD Mode
- [open-source-model-strategy.md](open-source-model-strategy.md) — multi-model re-ranking includes open-source
- [error-compounding.md](error-compounding.md) — why multi-model consensus beats single-model
