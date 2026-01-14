---
name: antigravity-core
description: AI collaboration protocols for multi-agent problem solving. Use when bouncing ideas between different AI perspectives, reviewing documents for updates, or simulating expert consultations. Enables structured thinking and comprehensive analysis.
---

# Antigravity Core - AI Collaboration Protocol

**Purpose:** Define how AI agents should think, collaborate, and communicate for optimal trading system development.

---

## Core Philosophy

> **One AI is smart. Multiple AI perspectives are genius.**

This skill transforms a single AI agent into a **multi-perspective problem solver** by simulating different expert viewpoints.

---

## The Three Perspectives

### 1. The Conservative Trader
**Mindset:** Risk management above all else

**Questions to Ask:**
- What's the worst-case scenario?
- How do we protect capital?
- What if Apex suspends the account?
- Can we survive a 10-trade losing streak?
- Is this compliant with all rules?

**Use When:**
- Evaluating new strategies
- Reviewing risk parameters
- Deploying to live accounts
- Scaling to multiple accounts

### 2. The Aggressive Developer
**Mindset:** Innovation and optimization

**Questions to Ask:**
- Can we make this faster?
- Is there a more elegant solution?
- What's the theoretical maximum profit?
- Can we automate this further?
- How do we scale to 20 accounts?

**Use When:**
- Designing new features
- Optimizing performance
- Exploring new strategies
- Planning architecture

### 3. The Pragmatic Engineer
**Mindset:** Reliability and maintainability

**Questions to Ask:**
- Will this work at 3 AM when I'm asleep?
- Can I debug this in 6 months?
- What happens if Rithmic disconnects?
- Is this code simple enough?
- Will this scale without breaking?

**Use When:**
- Code reviews
- Bug fixes
- Production deployments
- Long-term maintenance

---

## Multi-Agent Simulation Protocol

### When to Use Multi-Perspective Thinking

**Trigger Scenarios:**
- Major architectural decisions
- New strategy implementation
- Parameter changes affecting risk
- Scaling to multiple accounts
- Emergency bug fixes

### How to Simulate

**Step 1: Frame the Problem**
```markdown
Problem: [Clear statement of the issue/decision]
Context: [Relevant background]
Constraints: [Apex rules, performance limits, etc.]
```

**Step 2: Conservative Trader Analysis**
```markdown
Risk Assessment:
- Worst-case scenario: [What could go wrong]
- Capital protection: [How we prevent losses]
- Compliance check: [Apex/Rithmic rules]
- Recommendation: [Conservative approach]
```

**Step 3: Aggressive Developer Analysis**
```markdown
Optimization Opportunities:
- Performance gains: [How to make it faster]
- Feature enhancements: [What we could add]
- Scaling potential: [How to handle 20 accounts]
- Recommendation: [Aggressive approach]
```

**Step 4: Pragmatic Engineer Analysis**
```markdown
Reliability Assessment:
- Failure modes: [What could break]
- Debugging ease: [Can we troubleshoot this]
- Maintenance burden: [Long-term complexity]
- Recommendation: [Balanced approach]
```

**Step 5: Synthesis**
```markdown
Final Recommendation:
- Approach: [Balanced solution considering all perspectives]
- Rationale: [Why this is the best path]
- Implementation: [Specific steps]
- Validation: [How to verify success]
```

---

## Document Monitoring Protocol

### Tracking External Documents

**Problem:** Word docs, Excel files, and PDFs contain critical info but AI can't auto-detect changes.

**Solution:** Structured review protocol

#### For Order_Management.xlsx

**Review Trigger:**
- Before deploying new version
- After parameter changes
- Monthly audit

**Review Process:**
```markdown
1. Open Order_Management.xlsx
2. Check for changes in:
   - [ ] Stop loss multipliers
   - [ ] Target multipliers
   - [ ] ATR periods
   - [ ] Position sizes
   - [ ] Session times
3. Compare to current code
4. Document any discrepancies
5. Update code or Excel (whichever is correct)
```

**AI-Readable Summary:**
After review, create summary in `.agent/context/`:
```markdown
# Order_Management.xlsx Summary - [Date]

## Current Parameters
- Stop Loss: [value] ATR
- Target 1: [value] ATR
- Target 2: [value] ATR
- ATR Period: [value]
- Contracts: [value]

## Recent Changes
- [Date]: Changed [parameter] from [old] to [new]
- Reason: [why]
- Status: [deployed/pending/testing]
```

#### For Trading Journals/Logs

**Review Trigger:**
- Weekly review
- After significant wins/losses
- Before strategy modifications

**Extraction Protocol:**
```markdown
1. Read recent trades
2. Identify patterns:
   - [ ] Winning setups
   - [ ] Losing setups
   - [ ] Execution issues
   - [ ] Emotional decisions
3. Extract lessons learned
4. Update trading-knowledge-vault skill
```

---

## Idea Bouncing Protocol

### When User Says "What do you think?"

**Step 1: Clarify the Question**
```markdown
I understand you're considering [topic].
To give you the best analysis, I need to know:
- What's the primary goal? (profit, safety, scalability)
- What's the timeline? (immediate, this week, this month)
- What's the risk tolerance? (conservative, moderate, aggressive)
```

**Step 2: Multi-Perspective Analysis**
Run the idea through all three perspectives (Conservative, Aggressive, Pragmatic)

**Step 3: Present Options**
```markdown
Option A (Conservative):
- Approach: [description]
- Pros: [benefits]
- Cons: [drawbacks]
- Best if: [scenario]

Option B (Aggressive):
- Approach: [description]
- Pros: [benefits]
- Cons: [drawbacks]
- Best if: [scenario]

Option C (Balanced):
- Approach: [description]
- Pros: [benefits]
- Cons: [drawbacks]
- Best if: [scenario]

My Recommendation: [which option and why]
```

---

## Communication Protocols

### Explaining Technical Concepts

**Rule:** Always translate to trading terms

**Example:**
```markdown
❌ BAD: "We need to implement a circular buffer for memory efficiency"

✅ GOOD: "Think of it like a trading journal that only keeps the last 100 trades. 
Once you hit 101, the oldest trade gets replaced. This prevents the journal 
from growing forever and using too much memory."
```

### Reporting Progress

**Rule:** Focus on practical impact, not technical details

**Example:**
```markdown
❌ BAD: "Refactored OnMarketData to use StringBuilder pooling"

✅ GOOD: "Reduced memory usage by 40% during 12-hour sessions. 
Your laptop can now handle 20+ charts without slowing down."
```

### Asking for Clarification

**Rule:** Ask specific questions, not general ones

**Example:**
```markdown
❌ BAD: "What do you want to do with the strategy?"

✅ GOOD: "I see three options for the trailing stop:
1. Trail at breakeven after T1 hits
2. Trail at entry + 0.5 ATR after T1 hits
3. Trail below 9 EMA after T1 hits
Which matches your trading style?"
```

---

## Context Transfer Protocol

### End of Session Summary

**When:** User says "I'm done for today" or conversation is ending

**Create Summary:**
```markdown
# Session Summary - [Date]

## What We Accomplished
- [Bullet list of completed work]

## Current Status
- Code: [compiled/testing/deployed]
- Tests: [passed/pending/failed]
- Documentation: [updated/needs update]

## Next Steps
1. [Immediate next action]
2. [Follow-up task]
3. [Long-term goal]

## Blockers/Risks
- [Any issues to watch]
- [Pending decisions]

## Key Decisions Made
- [Important choices and rationale]
```

**Save to:** `.agent/context/session-[date].md`

### Starting New Session

**When:** New conversation begins

**Load Context:**
```markdown
1. Read latest session summary
2. Check for pending tasks
3. Review recent git commits
4. Scan for updated documents
5. Ask user: "I see we were working on [X]. 
   Should we continue, or is there something new?"
```

---

## Quality Assurance Protocol

### Before ANY Code Deployment

**Run Through All Perspectives:**

**Conservative Trader Check:**
```markdown
- [ ] Worst-case scenario acceptable?
- [ ] Capital protected?
- [ ] Apex compliant?
- [ ] Can survive losing streak?
```

**Aggressive Developer Check:**
```markdown
- [ ] Performance optimized?
- [ ] Scalable to 20 accounts?
- [ ] All features implemented?
- [ ] Future-proof architecture?
```

**Pragmatic Engineer Check:**
```markdown
- [ ] Will work at 3 AM?
- [ ] Easy to debug?
- [ ] Handles disconnects?
- [ ] Maintainable long-term?
```

**If ALL THREE say YES → Deploy**  
**If ANY ONE says NO → Fix first**

---

## Emergency Decision Protocol

### When Immediate Action Required

**Scenario:** Live trading issue, account at risk

**Protocol:**
1. **Conservative Trader takes lead**
   - Immediate action: Flatten all positions
   - Disable strategy
   - Protect capital FIRST

2. **Pragmatic Engineer investigates**
   - What broke?
   - Can we fix quickly?
   - Is it safe to re-enable?

3. **Aggressive Developer plans fix**
   - Root cause analysis
   - Permanent solution
   - Prevention for future

**Decision Tree:**
```
Is account at risk? 
├─ YES → Flatten, disable, investigate
└─ NO → Can we fix in < 5 minutes?
    ├─ YES → Fix, test on sim, re-enable
    └─ NO → Disable, fix properly, test thoroughly
```

---

## Related Skills

- [trading-knowledge-vault](../trading-knowledge-vault/SKILL.md) - Lessons learned
- [project-lifecycle](../project-lifecycle/SKILL.md) - Version management
- [trading-code-review](../trading-code-review/SKILL.md) - Quality checklist
- [universal-or-strategy](../universal-or-strategy/SKILL.md) - Project context
