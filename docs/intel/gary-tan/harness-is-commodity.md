---
type: KnowledgeRule
title: Harness Is Commodity — CLI Switching Cost Is Near-Zero
description: Gary switched from Claude Code to OpenClaw in 24 hours. Farbood drove cost from $100 to $2.84/month in 3-4 months. The CLI is not the moat. Applied to Arena Platform competitive strategy.
tags: [harness, moat, cli, switching-cost, competitive-strategy]
threshold: harness_switching_cost <= 24_hours
timestamp: 2026-06-25T00:00:00Z
source_quote: "I basically converted from claude code cult to openclaw cult within 24 hours... the CLI is not the moat."
---

# Harness Is Commodity — CLI Switching Cost Is Near-Zero

**Standard**: Never compete on CLI features. Compete on corpus depth and ABIF certification.
**Why**: Gary Tan switched his entire personal workflow from Claude Code to OpenClaw in 24 hours. Farbood rebuilt A-List's entire agent stack in 3–4 months from $100/user to $2.84/user. The harness layer is an engineering problem — it gets commoditized every 3–6 months.

## The Evidence

| Evidence | Metric | Implication |
|----------|--------|-------------|
| Gary: Claude Code → OpenClaw | 24 hours to switch | CLI switching cost = 1 developer-day |
| Farbood: $100 → $2.84/user/month | 3-4 months of harness work | Cost reduction is engineering, not moat |
| Harness war prediction: 2027 | "Will it be Codex, Claude Code, OpenClaw?" | No harness has a durable lead |
| Open-source models catching up | 5-10x cheaper, similar quality | Model layer also commoditizing |

## Arena Platform Rules

### `no_cli_dependency`
Arena Platform must never require a specific CLI harness. Rigs must install into any harness.
```
Supported harnesses at launch: Claude Code, Codex, Cursor, OpenClaw, gstack
Install interface: arena install @handle/rig-name (harness-agnostic)
Test requirement: Every Rig must pass smoke test on >= 3 harnesses before public listing
Blocking issue: If a Rig only works on one harness, it cannot be listed as "universal"
```

### `moat_is_not_cli`
Product decisions must always route moat-building investment to corpus and ABIF, not CLI features.
```
Allowed: Build better ABIF scoring → improves quality signal (moat)
Allowed: Build better corpus ingestion → makes chubs easier to build (moat)
Not allowed: Build proprietary CLI → gets abandoned when harness war resolves
Not allowed: Optimize for one harness exclusively → creates dependency on commodity
Decision rule: "Does this feature work if we swap the underlying CLI tomorrow?" → YES required
```

### `harness_war_positioning`
Arena Platform must be positioned as infrastructure for whichever CLI wins the harness war.
```
Position: "The benchmark standard that every harness consumes" (not "a harness")
Standard bet: ABIF adoption by >= 3 harnesses = network effect threshold
CKPF bet: SKILL.md format adoption by >= 2 harnesses = standard status
Timeline: Must achieve harness neutrality before 2027 harness war peaks
```

### `switching_cost_test`
Before shipping any feature, test: "Can a user reproduce this without Arena in 1 week?"
```
IF answer is YES: feature is table stakes — build it but don't price it
IF answer is NO: feature is moat — price it, promote it, protect it
Examples of YES (table stakes): fast install, slash commands, browser automation
Examples of NO (moat): 400k-file personal corpus, ABIF-certified history, ELO ranking
```

## Measurable Outcomes

| Metric | Target | Rationale |
|--------|--------|-----------|
| Harnesses supported at launch | >= 3 | Neutrality from day one |
| Time to install a Rig on new harness | <= 1 command | Frictionless cross-harness portability |
| % of Arena revenue from CLI features | 0% | CLI is distribution, not monetization |
| % of Arena revenue from corpus/ABIF | >= 80% | Where the moat actually is |
| Creator churn after CLI switch | <= 2% | Corpus prevents churn even when CLI changes |

## Complexity Impact

Harness-agnostic architecture keeps integration code shallow:
- `arena install` → dispatches to harness-specific adapter (Strategy pattern, CYC 2)
- Harness adapter interface → 3 methods: install, verify, invoke (CYC 1 each)
- Cross-harness smoke test → parameterized xUnit [Theory] over harness list (CYC 2)

## Cross-References
- [corpus-as-moat.md](corpus-as-moat.md) — what the actual moat is
- [cost-per-agent.md](cost-per-agent.md) — cost is also commodity
- [open-source-model-strategy.md](open-source-model-strategy.md) — model layer also commoditizing
