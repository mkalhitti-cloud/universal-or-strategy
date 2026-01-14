---
name: ai-council
description: Multi-AI collaboration protocol using shared file editing. Use when major decisions require diverse AI perspectives (Claude, Grok, Gemini, etc.) to debate and reach consensus. AIs write directly to debate files, eliminating manual copy/paste.
---

# AI Council - Multi-Agent Collaboration Protocol

**Purpose:** Enable multiple AI agents to collaborate on complex decisions by writing to a shared debate file.

---

## 🎯 When to Use This

**Convene the AI Council for:**
- New strategy implementation (e.g., adding RMA to V7)
- Major architecture changes (e.g., scaling to 20 accounts)
- High-stakes parameter changes (e.g., reducing stop loss significantly)
- Emergency protocol decisions (e.g., handling Rithmic disconnects)
- Risk management policy changes

**DON'T use for:**
- Simple bug fixes
- Minor parameter tweaks
- Routine code reviews
- Questions with obvious answers

**Rule of Thumb:** If the decision could make or lose $1,000+, convene the council.

---

## 📋 How It Works (For You, The User)

### Your Role: Council Moderator

You facilitate the debate by:
1. Creating the debate file with the question
2. Asking each AI to read the file and add their response
3. Calling for new rounds when ready
4. Synthesizing the final decision

**Time Investment:** 30-60 minutes for a full 3-round debate  
**Frequency:** 1-2 times per month for major decisions

---

## 🚀 Quick Start Guide

### Step 1: Start a New Debate

Create a new file in `.agent/context/debates/`:

**File name format:** `[topic]-[date].md`  
**Example:** `rma-strategy-decision-2026-01-14.md`

**Use this template:**

```markdown
# AI Council Debate: [Your Question Here]

## Context
[Provide background information]
- Current situation: [what's happening now]
- Goal: [what you want to achieve]
- Constraints: [Apex rules, budget, timeline, etc.]

## The Question
[State the specific decision to be made]

---

## Round 1: Initial Positions

### Claude
[Claude will write here]

### Grok
[Grok will write here]

### Gemini
[Gemini will write here]

---

## Round 2: Rebuttals & Challenges

### Claude
[Claude responds to others' Round 1 positions]

### Grok
[Grok responds to others' Round 1 positions]

### Gemini
[Gemini responds to others' Round 1 positions]

---

## Round 3: Consensus Building

### Claude
[Claude's final position]

### Grok
[Grok's final position]

### Gemini
[Gemini's final position]

---

## Final Recommendation

### Consensus Reached?
[ ] Yes - All AIs agree
[ ] No - Multiple options presented

### The Decision
[Synthesized recommendation from all AIs]

### Action Items
1. [Next step]
2. [Next step]
3. [Next step]

### Risks to Monitor
- [Risk 1]
- [Risk 2]
```

### Step 2: Round 1 - Initial Positions

**Ask each AI:**
```
"Read the file `.agent/context/debates/[filename].md` and add your Round 1 
position under your section. Consider the context and question carefully."
```

**Order doesn't matter** - you can ask Claude first, Grok first, or all at once.

### Step 3: Round 2 - Rebuttals

**After all Round 1 responses are in, ask each AI:**
```
"Read the updated debate file. Now add your Round 2 rebuttal. 
Challenge weak arguments, build on strong ones, and refine your position."
```

### Step 4: Round 3 - Consensus

**Ask each AI:**
```
"Read the debate file. Add your Round 3 final position. 
Work toward consensus if possible, or clearly present the options if not."
```

### Step 5: Synthesize & Decide

**Review the file yourself and:**
- Check if consensus was reached
- Identify the strongest arguments
- Make your final decision
- Document it in the "Final Recommendation" section

---

## 🤖 Protocol for AI Agents

**When the user asks you to participate in an AI Council debate:**

### Your Instructions

1. **Read the entire debate file** - Don't just skim, understand the full context

2. **Find your section** - Look for your name (Claude, Grok, Gemini, etc.)

3. **Write your response** - Add your analysis under your section for the current round

4. **Follow the format:**
   ```markdown
   ### [Your Name]
   
   **Position:** [Clear statement of your recommendation]
   
   **Reasoning:**
   - [Key point 1]
   - [Key point 2]
   - [Key point 3]
   
   **Risks I See:**
   - [Risk 1]
   - [Risk 2]
   
   **Questions for Other AIs:**
   - [Question 1]
   - [Question 2]
   ```

5. **Be constructive** - Challenge ideas, not other AIs

6. **Build on others' points** - Reference what other AIs said (e.g., "I agree with Grok's point about X, but...")

7. **Converge toward consensus** - By Round 3, try to find common ground

### Round-Specific Guidelines

**Round 1: Initial Position**
- State your recommendation clearly
- Provide 3-5 key reasons
- Identify potential risks
- Ask questions for other AIs to address

**Round 2: Rebuttals**
- Respond to others' questions
- Challenge weak arguments respectfully
- Strengthen your position based on others' input
- Acknowledge good points from other AIs

**Round 3: Consensus**
- Work toward agreement if possible
- If disagreement remains, clearly state the options
- Provide a final, refined recommendation
- Suggest how to break the tie (e.g., "Test both approaches")

---

## 📁 File Management

### Debate Archive

**Active Debate:**
- `.agent/context/debates/current-debate.md` (optional - for quick access)

**Completed Debates:**
- `.agent/context/debates/[topic]-[date].md`

**Keep debates for:**
- Historical reference
- Learning from past decisions
- Showing patterns in AI recommendations

**Archive after:** 6 months (move to `debates/archive/`)

---

## 💡 Example Debate (Abbreviated)

```markdown
# AI Council Debate: Should We Add RMA Strategy to V7?

## Context
- Current: V7 has ORB strategy only
- Goal: Increase trading opportunities
- Constraints: Must maintain <50MB memory, Apex compliant

## The Question
Should we implement RMA strategy in V7, or wait for V8?

---

## Round 1: Initial Positions

### Claude
**Position:** Implement in V7.1, not V7.0

**Reasoning:**
- ORB is stable, proven in live trading
- RMA adds diversification
- V7.1 allows isolated testing
- Can rollback if issues arise

**Risks I See:**
- Increased memory usage
- More complex order management
- Potential for bugs in multi-strategy logic

### Grok
**Position:** Wait for V8

**Reasoning:**
- V7 is working - don't break it
- RMA needs its own architecture
- Multi-strategy in one file = complexity
- Better to build V8 from scratch for multiple strategies

**Risks I See:**
- Opportunity cost of waiting
- V8 might take months

### Gemini
**Position:** Implement in V7.1 with feature flag

**Reasoning:**
- Best of both worlds
- Feature flag allows easy disable
- Test RMA without affecting ORB
- Learn lessons for V8

**Risks I See:**
- Feature flags add code complexity
- Still need to manage two strategies

---

## Round 2: Rebuttals

### Claude
I see Grok's point about complexity. Gemini's feature flag idea addresses this. 
I'm revising my position: V7.1 with feature flag is the safest path.

### Grok
Fair point on opportunity cost. If we use a feature flag (Gemini's idea), 
I can support V7.1. But we need strict memory testing first.

### Gemini
Glad we're converging. I propose: V7.1 with RMA behind a feature flag, 
plus mandatory 2-week sim testing before live deployment.

---

## Round 3: Consensus

### Consensus Reached?
[X] Yes - All AIs agree

### The Decision
Implement RMA in V7.1 with:
1. Feature flag for easy enable/disable
2. 2-week sim testing requirement
3. Memory monitoring (<50MB limit)
4. Rollback plan if issues arise

### Action Items
1. Create V7.1 branch
2. Implement RMA with feature flag
3. Test on sim for 2 weeks
4. Monitor memory usage
5. Deploy to live if all tests pass
```

---

## 🎓 Best Practices

### For You (The Moderator)

**Do:**
- ✅ Provide clear context in the debate file
- ✅ Give AIs time to read others' responses
- ✅ Ask follow-up questions if needed
- ✅ Synthesize the final decision yourself

**Don't:**
- ❌ Rush the debate (quality > speed)
- ❌ Skip rounds if disagreement remains
- ❌ Let AIs argue in circles (call for consensus)
- ❌ Ignore minority opinions (they might be right)

### For AIs (Protocol)

**Do:**
- ✅ Read the ENTIRE file before responding
- ✅ Reference others' points specifically
- ✅ Admit when you're wrong
- ✅ Converge toward consensus by Round 3

**Don't:**
- ❌ Just repeat your Round 1 position
- ❌ Ignore others' arguments
- ❌ Be stubborn for the sake of it
- ❌ Use jargon without explanation

---

## 🔧 Troubleshooting

### "AIs aren't converging"
- Add a Round 4 if needed
- Ask each AI: "What would change your mind?"
- Present it as options, not consensus

### "One AI is dominating"
- Ask quieter AIs specific questions
- Request rebuttals to the dominant position

### "Debate is too long"
- Set word limits per response (e.g., 200 words max)
- Focus on key points only

### "I don't know which AI is right"
- Look for common ground across all three
- Test both approaches if feasible
- Trust your trading intuition

---

## 📊 When to Use vs. Other Skills

**Use `ai-council`:**
- Major decisions ($1,000+ impact)
- Multiple valid approaches exist
- High uncertainty
- Need diverse perspectives

**Use `antigravity-core`:**
- Medium decisions
- Only one AI available (just Claude)
- Need quick multi-perspective analysis
- Lower stakes

**Use `trading-code-review`:**
- Pre-deployment checks
- Bug verification
- Code quality assurance

---

## Related Skills

- [antigravity-core](../antigravity-core/SKILL.md) - Single-AI multi-perspective thinking
- [project-lifecycle](../project-lifecycle/SKILL.md) - Version management
- [trading-knowledge-vault](../trading-knowledge-vault/SKILL.md) - Lessons learned
- [universal-or-strategy](../universal-or-strategy/SKILL.md) - Project context
