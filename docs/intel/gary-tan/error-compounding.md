---
type: KnowledgeRule
title: Error Compounding — Why Quality Floor Matters in Recursive Agent Loops
description: Gary Tan's 99.9% vs 90% accuracy analysis. In recursive agent loops, errors compound exponentially. Applied to Arena Platform ABIF quality thresholds and model tier selection.
tags: [quality, error-compounding, abif, recursive-loops, model-selection]
threshold: abif_certified_agent_accuracy >= 99_percent
timestamp: 2026-06-25T00:00:00Z
source_quote: "If they're recursively looping and you run them 100 times — the one that was right 90% of the time is not going to be right 13% of the time, whereas the one that was 99.9% will drop to 80-90%. Errors compound."
---

# Error Compounding — Why Quality Floor Matters in Recursive Agent Loops

**Standard**: ABIF-certified agents must achieve >= 99% per-step accuracy on their certified benchmark
**Why**: In multi-step agent tasks, accuracy compounds multiplicatively. A 90% accurate agent becomes 13% reliable after 20 steps. A 99% accurate agent stays at 81% after 20 steps. The difference is not 9 percentage points — it is 6x reliability.

## The Mathematics (Gary's Analysis)

```
Accuracy after N recursive steps = accuracy_per_step ^ N

Scenario A: 90% accurate agent
  After 10 steps:  0.90^10 = 34.9%
  After 20 steps:  0.90^20 = 12.2%
  After 50 steps:  0.90^50 = 0.5%   ← effectively unusable

Scenario B: 99% accurate agent
  After 10 steps:  0.99^10 = 90.4%
  After 20 steps:  0.99^20 = 81.8%
  After 50 steps:  0.99^50 = 60.5%  ← still valuable

Scenario C: 99.9% accurate agent
  After 10 steps:  0.999^10 = 99.0%
  After 20 steps:  0.999^20 = 98.0%
  After 50 steps:  0.999^50 = 95.1%  ← enterprise-grade

Key insight: Error rate matters at 100x, not the accuracy level.
90% accuracy = 10% error rate
99.9% accuracy = 0.1% error rate
Difference: 100x lower error rate, not 10%
```

## Arena Platform Rules

### `abif_quality_floor`
No agent earns ABIF certification below the per-step accuracy threshold.
```
Minimum for Standard certification:  >= 95% per-step (usable for 10-step tasks)
Minimum for Pro certification:       >= 99% per-step (usable for 20-step tasks)
Minimum for Enterprise certification: >= 99.9% per-step (usable for 50-step tasks)
Test method: run certified benchmark 100x, compute mean pass rate per step
Disqualification: any run with cascading failure (step N fail causes all subsequent failures)
```

### `task_length_gating`
Arena Platform limits task length based on agent's certified accuracy tier.
```
Standard-certified agent:   max 10-step automated task (34.9% floor → acceptable for batch work)
Pro-certified agent:        max 20-step automated task (81.8% floor → acceptable for professional work)
Enterprise-certified agent: max 50-step automated task (95.1% floor → acceptable for critical work)
Enforcement: API returns 429 if task length exceeds agent's certified tier
Rationale: Prevents agents from accepting tasks they cannot reliably complete
```

### `error_detection_checkpoint`
Every agent task must emit a confidence signal at each step — not just a final pass/fail.
```
Required field per step: { step_id, action, confidence: 0.0-1.0, checkpoint_hash }
Alert threshold: if confidence < 0.70 on any step → pause and request human confirmation
ABIF scoring: step-level confidence history is part of the certification record
Benefit: detects compounding error before it reaches terminal failure
```

### `model_upgrade_trigger`
When an agent's rolling accuracy drops below tier threshold, auto-suggest model upgrade.
```
Monitor: rolling 7-day average per-step accuracy per agent
If accuracy drops below tier threshold by >= 2%: emit UpgradeRecommendation event
Upgrade path: Tier 1 (open-source) → Tier 2 (mid) → Tier 3 (frontier)
Cost impact: document expected per-session cost increase with upgrade recommendation
User decision: always opt-in, never auto-upgrade (respects cost sensitivity)
```

### `recursive_loop_budget`
Any agent operating in a recursive self-improvement or re-ranking loop must declare a step budget.
```
Max steps without human checkpoint: 20 (Pro tier) or 50 (Enterprise tier)
Budget declaration: required in Rig manifest — max_autonomous_steps: N
Enforcement: platform terminates loop at budget limit, emits summary
Rationale: Gary's 99.9% math shows even excellent agents need checkpoints at scale
```

## The Hiring Implication

Gary: "I'll always pay for more intelligence because I'd rather be right more often than wrong — you're high-leveraged."

```
Arena Platform pricing implication:
  When task involves judgment (hiring decision, code review, architecture choice):
    → Always recommend Enterprise tier agent (99.9% floor)
    → Price premium is justified by error-compounding math
    → Show buyer: "For this 30-step task, Standard agent = 4% success. Enterprise = 97%."
  
  When task is repetitive/batch (data formatting, file processing):
    → Standard tier agent is fine
    → Cost savings justify lower quality floor
```

## Measurable Outcomes

| Metric | Target | Rationale |
|--------|--------|-----------|
| ABIF Standard certification threshold | >= 95% per-step | Usable for 10-step tasks |
| ABIF Pro certification threshold | >= 99% per-step | Usable for 20-step tasks |
| ABIF Enterprise certification threshold | >= 99.9% per-step | Usable for 50-step tasks |
| Task completion rate: Pro agent, 20 steps | >= 80% | Matches Gary's math at 99% |
| Customer satisfaction above Pro tier | >= 90% | Error floor high enough for judgment work |

## Complexity Impact

Quality tracking logic must be simple to stay maintainable:
- `abif_quality_floor` → single threshold comparison per step (CYC 1)
- `task_length_gating` → lookup against agent cert tier (CYC 2)
- `error_detection_checkpoint` → emitted as structured event, not inline logic (CYC 2)
- `recursive_loop_budget` → counter decrement + boundary check (CYC 2)

## Cross-References
- [corpus-as-moat.md](corpus-as-moat.md) — corpus quality improves accuracy floor over time
- [open-source-model-strategy.md](open-source-model-strategy.md) — when to upgrade model tier
- [cost-per-agent.md](cost-per-agent.md) — cost of upgrading to higher accuracy tier
