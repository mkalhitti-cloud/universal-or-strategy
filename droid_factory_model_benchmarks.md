# Which Model Reviews Code Best?
By Factory Research, Nizar Alrifai - April 29, 2026

We benchmarked 13 models to find the best price-performance tradeoff for AI code review.

## Key Findings
*   **GPT-5.2** and Claude Opus 4.6 lead the pack at ~60% F1, but GPT-5.2 does it at $1.25/PR vs $3.11 for Opus.
*   **Newer doesn't always mean better.** GPT-5.4 (47.5% F1) is too conservative — high precision (59.6%) but low recall (41.8%), missing bugs it should catch. GPT-5.5 (47.9% F1) has the opposite problem: it comments at the right rate but half are false positives (47.5% precision). Both trail GPT-5.2 significantly.
*   **Open-source models punch above their weight.** Kimi K2.5 (51.9% F1 at $0.41/PR) and GLM-5.1 (55.8% at $1.06/PR) compete with frontier models at a fraction of the price.
*   **Cost explains only ~21% of quality variance.** Model architecture and training matter far more than token budget.

## Our Picks
*   **Best Overall:** GPT-5.2 ($1.25/PR, 60.5% F1) - Top-tier quality at half the cost of Opus 4.6.
*   **Best Value:** Kimi K2.5 ($0.41/PR, 51.9% F1) - 85%+ of top-tier quality for a fraction of the price.
*   **Budget Pick:** MiniMax M2.7 ($0.15/PR, 45.6% F1) - Run eight review passes for less than one GPT-5.2 run.

## Full Rankings
1.  **GPT-5.2**: 60.5% F1 ($1.25/PR, 462K tokens)
2.  **Opus 4.6**: 59.8% F1 ($3.11/PR, 1.2M tokens)
3.  **Sonnet 4.6**: 57.4% F1 ($1.15/PR, 427K tokens)
4.  **Opus 4.7**: 55.9% F1 ($4.18/PR, 3.1M tokens)
5.  **GLM-5.1**: 55.8% F1 ($1.06/PR, 2.6M tokens)
6.  **GPT-5.3 Codex**: 55.7% F1 ($1.69/PR, 626K tokens)
7.  **Gemini 3.1 Pro**: 52.1% F1 ($2.04/PR, 755K tokens)
8.  **Kimi K2.5**: 51.9% F1 ($0.41/PR, 152K tokens)
9.  **GPT-5.4 Mini**: 51.5% F1 ($0.68/PR, 252K tokens)
10. **Gemini 3 Flash**: 49.5% F1 ($0.34/PR, 124K tokens)
11. **GPT-5.5**: 47.9% F1 ($5.63/PR, 4.2M tokens)
12. **GPT-5.4**: 47.5% F1 ($2.01/PR, 744K tokens)
13. **MiniMax M2.7**: 45.6% F1 ($0.15/PR, 56K tokens)

## The Cost Story
Token consumption varies dramatically between models. GPT-5.5 uses 4.2 million tokens per PR, while MiniMax M2.7 uses just 56K. Most of those extra tokens aren't making the review better. GPT-5.2 delivers top-tier quality at $0.021 per F1 point, while Opus 4.6 costs $0.052.

## Methodology
*   **Test set**: 50 real PRs from five open-source repositories.
*   **Golden set**: Human-curated set of known bugs and issues in each PR.
*   **Model evaluation**: Each model reviews every PR independently using the same prompt and configuration (reasoning effort "high").
*   **F1 calculation**: Precision vs. Recall harmonic mean.
