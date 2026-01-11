# Claude Code Skills Quick Reference

**Purpose:** How to use your local skills with Claude Code in Windsurf/Cursor
**For:** Mo - NinjaTrader trading strategy development

---

## The Big Idea

**Before Setup:**
- You ask Claude Code a question
- Claude Code has no context about your project
- You have to repeat yourself

**After Setup:**
- You ask Claude Code a question
- Claude Code automatically finds relevant skill files
- Context is built-in, answers are more accurate
- Your skills document evolves as you develop

---

## How to Ask Claude Code Questions

### Format 1: Explicit Skill Reference (Most Direct)
```
"Check the live-price-tracking skill and verify my OnMarketData implementation"

Claude Code will:
1. Find `.claude/skills/references/live-price-tracking.md`
2. Read your question
3. Reference the skill file
4. Give you specific, contextual answer
```

### Format 2: Project Context (Claude Code Learns)
```
"Does my strategy use RMA click-entry correctly?"

Claude Code will:
1. Find `.claude/skills/project-specific/universal-strategy-v6-context.md`
2. Find `.claude/skills/references/rma-click-entry-guide.md`
3. Automatically reference relevant files
4. Answer in trading context (not generic coding)
```

### Format 3: Development Task (Multi-Skill Query)
```
"I want to add Fibonacci confluence to my ORB strategy. What do I need to know?"

Claude Code will:
1. Reference `universal-strategy-v6-context.md` (project state)
2. Reference `wsgta-trading-system.md` (trading rules)
3. Reference `ninjatrader-strategy-dev.md` (development patterns)
4. Suggest approach based on all three skills
```

### Format 4: Code Review (Quality Assurance)
```
"Review my latest code change against the trading-code-review skill and live-price-tracking"

Claude Code will:
1. Read your code
2. Check `trading-code-review.md` (quality checklist)
3. Verify `live-price-tracking.md` (critical bug prevention)
4. Give you a detailed review with specific improvements
```

---

## Common Questions to Ask Claude Code

### Immediate Questions (Right Now)
- "Is my strategy using OnMarketData for live price tracking?"
- "Check my code for the Close[0] bug"
- "Does my order modification rate-limiting work correctly?"
- "Verify my strategy follows WSGTA rules"

### Development Questions (When Adding Features)
- "I want to implement [feature]. Reference the relevant skills and give me a development plan"
- "Is there a common NT8 anti-pattern I should watch for?"
- "How should I handle Rithmic disconnects?"
- "What's the memory impact of adding [feature]?"

### Review Questions (Before Testing)
- "Run the code-review checklist on my latest version"
- "Check for Apex compliance violations"
- "Audit for memory leaks or inefficiencies"
- "Do I need a multi-AI code review for this change?"

### Documentation Questions (Building Knowledge)
- "Add a Fibonacci confluence guide to my skills"
- "Document the [topic] we just implemented"
- "Create a troubleshooting guide for [issue]"

---

## The Three Ways to Use Your Skills

### 1️⃣ **As Reference** (Most Common)
You mention a skill, Claude Code reads it and gives context-aware answers.

**Example Conversation:**
```
You: "Is my trailing stop implementation correct?"
Claude Code: "Looking at live-price-tracking skill... 
Your code uses Close[0] instead of OnMarketData. 
Here's the fix..."
```

### 2️⃣ **As Quality Gate** (Before Testing)
You ask Claude Code to check your code against a skill checklist.

**Example Conversation:**
```
You: "Check my new code against the trading-code-review skill"
Claude Code: "Comparing against checklist...
✅ Uses OnMarketData for live prices
❌ Missing GetLivePrice() fallback
⚠️ Rate-limiting interval too short"
```

### 3️⃣ **As Learning Tool** (Discovery)
You ask Claude Code to explain a concept from your skills.

**Example Conversation:**
```
You: "Explain the Close[0] bug from live-price-tracking"
Claude Code: "From your skill file:
[Explains the bug with examples]
[Shows the solution with code]
[Provides testing checklist]"
```

---

## When to Update Your Skills

Add/update skill files when you discover:
- ✅ A new bug or anti-pattern
- ✅ A solution that worked well
- ✅ A trading rule you should remember
- ✅ A performance optimization
- ✅ A Rithmic/Apex specificity
- ✅ A pattern that applies across strategies

**Don't Create Skill Files For:**
- ❌ One-off questions
- ❌ Temporary workarounds
- ❌ Things obvious from NinjaTrader docs
- ❌ General programming concepts

---

## Example: Real Development Scenario

**Situation:** You're implementing Fibonacci confluence and want to make sure it's correct.

**Step 1: Ask Claude Code**
```
"I'm implementing Fibonacci retracement levels. Use the 
universal-strategy-v6-context and wsgta-trading-system 
skills to suggest how this should work with ORB rules."
```

**Step 2: Claude Code Responds**
```
From universal-strategy-v6-context:
- You're at V5.3.1
- Fibonacci confluence is the current development item
- Should NOT change entry rules or position sizing
- Should add alerts/visual indicators

From wsgta-trading-system:
- ORB enters at high/low + 1 tick
- Fibonacci levels can confirm ORB validity
- Use 50% and 61.8% retracement levels
- Confluence = higher probability trade
```

**Step 3: Code Development**
```
You develop the feature, test it, confirm it works
```

**Step 4: Add to Skills**
```
Create .claude/skills/references/fibonacci-confluence.md
Add implementation details, testing checklist, common gotchas
Future Claude Code will reference this automatically
```

---

## File Organization Tips

**Keep in Mind:**
- `core/` = Development fundamentals (NinjaTrader patterns)
- `trading/` = Trading methodology (WSGTA, rules, timezones)
- `project-specific/` = Your project context
- `references/` = How-to guides, bug fixes, learnings
- `changelog/` = Version history and what changed

**Example: Adding Fibonacci**
```
Which folder?
references/ ← Because it's how to implement something

Filename?
fibonacci-confluence-guide.md ← Specific and descriptive

Content?
- Problem: You want Fibonacci levels for confluence
- Solution: Implementation approach
- Code: Example snippets
- Testing: Verification checklist
- Gotchas: Common mistakes
```

---

## Collaboration with Claude Code

**Don't:** Ask Claude Code generic coding questions
**Do:** Ask Claude Code trading-specific strategy questions

**Don't:** Forget to reference skills in your question
**Do:** Mention relevant skill files (Claude learns them over time)

**Don't:** Keep all knowledge in your head
**Do:** Document discoveries in skill files for future reference

**Don't:** Let skill files get stale
**Do:** Update them as you test and verify things

---

## Power Tips

### Tip 1: Skill File Naming Convention
Use descriptive names with hyphens (not underscores):
- ✅ `live-price-tracking.md`
- ✅ `fibonacci-confluence-guide.md`
- ❌ `live_price_tracking.md`

### Tip 2: Reference Pattern in Conversation
```
"Use [skill-name] skill and [another-skill-name] skill to..."

This trains Claude Code on which skills are relevant
Over time, it auto-references them without asking
```

### Tip 3: Keep Skill Files Focused
One skill = one topic

Instead of:
- ❌ `big-strategy-guide.md` (50 KB monster)

Do:
- ✅ `orb-entry-rules.md` (2 KB focused)
- ✅ `rma-click-entry.md` (2 KB focused)
- ✅ `fibonacci-confluence.md` (2 KB focused)

### Tip 4: Update current-session.md Regularly
After each testing or development session, update:
```
.claude/context/current-session.md

- What you tested
- What you discovered
- Next priorities
- Any new insights
```

Claude Code reads this automatically for context.

### Tip 5: Use Skill Files for Debugging
When something breaks:
1. Check relevant skill file for documented solutions
2. Ask Claude Code: "My code is broken. Check [skill-name]"
3. Claude Code can often spot the issue immediately

---

## Real Example: "Help, My Trailing Stop Isn't Working"

**Before Skills System:**
```
You: "My trailing stop isn't updating properly"
Claude Code: "That could be many things..."
[Generic troubleshooting]
You: "Why are you not understanding my issue?"
```

**After Skills System:**
```
You: "My trailing stop isn't updating. Check live-price-tracking skill"
Claude Code: "Ah! Checking...
Your code uses Close[0] instead of OnMarketData
Close[0] only updates at bar close, not tick-by-tick
Here's the fix using OnMarketData..."
[Specific, correct solution]
```

---

## Maintenance Checklist (Monthly)

- [ ] Review skill files for accuracy
- [ ] Update `.claude/context/current-session.md`
- [ ] Archive old skill versions to `changelog/` if major changes
- [ ] Add new skills discovered from latest development
- [ ] Remove outdated references
- [ ] Test that Claude Code is finding skills correctly

---

## Your Skills Are Ready!

You now have:
- ✅ Folder structure set up (`.claude/skills/`)
- ✅ 3 critical skill files created
- ✅ Configuration files in place
- ✅ Context tracking set up
- ✅ This quick reference guide

**Next Steps:**
1. Open Windsurf/Cursor
2. Load your NinjaTrader strategies folder
3. Ask Claude Code a question, mention a skill
4. Watch it work!

---

## Questions to Test Your Setup

Once everything is created, try these questions in Claude Code:

### Test 1: Basic Skill Reference
```
"What's the critical bug documented in live-price-tracking?"
```
Claude Code should reference the file directly.

### Test 2: Project Context
```
"What is the current state of UniversalORStrategy?"
```
Claude Code should use universal-strategy-v6-context.md.

### Test 3: Development Pattern
```
"How should I implement live price tracking in NinjaScript?"
```
Claude Code should reference ninjatrader-strategy-dev.md.

### Test 4: Multi-Skill Query
```
"I want to add a new trading strategy. What should I consider?"
```
Claude Code should reference universal-strategy-v6-context + wsgta-trading-system.

---

**Status:** Ready to use
**Setup Time:** 20-30 minutes
**Value:** Context-aware Claude Code, learning system, documentation automation

Good luck with your trading strategy development! 🚀
