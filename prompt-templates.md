# Ready-to-Use Prompt Templates

Copy-paste templates for requesting code reviews from external AIs.

---

## TEMPLATE 1: Full Code Review Request

Use this when starting a new review with an external AI.

```
**[AI NAME] CODE REVIEW REQUEST - [PROJECT NAME]**

I need a comprehensive code review of my [PROJECT TYPE]. This code [STAKES - e.g., "runs on live funded accounts" or "handles user payments"]. Please review thoroughly and respond in a format I can share with another AI (Claude) for collaborative discussion.

---

## CRITICAL PLATFORM CONTEXT (Read First)

[PASTE RELEVANT PLATFORM CONTEXT BLOCK FROM platform-contexts.md]

---

## PROJECT CONTEXT

**What it does:** [Brief description]
**Runtime environment:** [Where/how it runs]
**User type:** [Technical level of person maintaining this]
**Critical requirements:** [What absolutely cannot break]

---

## REVIEW SCOPE

Please analyze:

1. **Logic & Correctness**
   - [Specific area of concern]
   - [Specific area of concern]

2. **Risk Management** (if applicable)
   - [Specific area of concern]

3. **Performance**
   - [Specific constraints - e.g., "runs 12+ hours"]
   - [Memory/CPU concerns]

4. **Reliability**
   - [Error handling concerns]
   - [Recovery requirements]

5. **Code Quality**
   - [Maintainability concerns]
   - [Documentation needs]

6. **Scalability** (if future growth planned)
   - [Planned features/expansion - e.g., "adding 4 more trade types"]
   - [Multi-instance concerns - e.g., "will run on 20+ charts"]
   - [Performance at scale]
   - [Architectural readiness for growth]

7. **Future Updateability**
   - [Extension points - where would new features plug in?]
   - [Configuration extensibility - can new settings be added cleanly?]
   - [Technical debt - what shortcuts need fixing before scaling?]
   - [Breaking change risks - what areas are fragile?]

---

## FUTURE ROADMAP (if applicable)

**Planned Features:** [List what you plan to add]
**Scaling Needs:** [How will usage grow?]
**Timeline:** [When do you need this ready?]

---

## THE CODE

```[LANGUAGE]
[PASTE CODE HERE]
```

---

## RESPONSE FORMAT

Please structure your response exactly like this:

**[AI NAME] CODE REVIEW - ROUND 1**

## ðŸ”´ CRITICAL ISSUES (Must Fix)
[Issues that could cause failures, data loss, or incorrect behavior]

## ðŸŸ¡ IMPORTANT CONCERNS (Should Fix)
[Issues that affect reliability or maintainability]

## ðŸŸ¢ MINOR SUGGESTIONS (Nice to Have)
[Optimizations or style improvements]

## âœ… WELL IMPLEMENTED
[Things done correctly that should NOT be changed]

## ðŸ”® SCALABILITY ASSESSMENT (if roadmap provided)
[How ready is the code for planned expansion?]
- Multi-feature readiness
- Extension points analysis
- Performance at scale concerns
- Architectural recommendations

## ðŸ”§ FUTURE UPDATEABILITY ASSESSMENT
[How maintainable is this code for ongoing development?]
- Where would new features plug in?
- Configuration extensibility
- Technical debt inventory
- Breaking change risks

## â“ QUESTIONS / CLARIFICATIONS NEEDED
[Areas where you need more context]

## ðŸ“‹ PRIORITIZED ACTION PLAN
[Numbered list in order of importance, including pre-scaling prep]

---

## EXISTING FINDINGS (Optional)

[If another AI already reviewed this, paste their findings here so you can confirm/challenge]
```

---

## TEMPLATE 2: Continue Dialogue (Claude â†’ External AI)

Use this when Claude has responded and you need to continue the discussion.

```
**CLAUDE'S RESPONSE TO [AI NAME] - ROUND [N]**

I shared your review with Claude. Here is Claude's response:

---

## âœ… AREAS OF FULL AGREEMENT

[Paste Claude's agreements]

---

## ðŸ¤ CONCESSIONS & MODIFICATIONS

[Paste Claude's concessions]

---

## ðŸ›¡ï¸ POINTS CLAUDE STILL MAINTAINS

[Paste Claude's disagreements with reasoning]

---

## ðŸ” NEW OBSERVATIONS

[Paste any new points Claude raised]

---

## ðŸ“‹ CURRENT PROPOSED ACTION PLAN

[Paste the current action plan]

---

**YOUR TASK:**

1. Review Claude's counter-arguments
2. Agree, disagree, or modify your position on each point
3. If you disagree, explain specifically how your recommendation would work within the platform constraints
4. Propose any NEW concerns you notice
5. End with confirmation or remaining disagreements

Format your response as:

**[AI NAME] RESPONSE TO CLAUDE - ROUND [N+1]**

So we can track the conversation.
```

---

## TEMPLATE 3: Continue Dialogue (External AI â†’ Claude)

Use this when bringing external AI's response back to Claude.

```
Here is [AI NAME]'s response to your review. Read it and tell me what you think - do we have consensus or do we need another round?

---

[PASTE EXTERNAL AI'S RESPONSE HERE]

---

After reviewing, provide:
1. Your assessment of their response
2. Any remaining disagreements
3. Updated action plan if we have consensus
4. Next prompt to send them if we need another round
```

---

## TEMPLATE 4: Final Consensus Request

Use this to confirm both AIs agree.

```
**CONSENSUS CHECK - FINAL ROUND**

Claude and I have been discussing your code review findings. Here's where we currently stand:

## AGREED FIXES
[List the fixes both AIs agree on]

## REJECTED SUGGESTIONS
[List suggestions that were rejected and why]

## VERIFIED AS CORRECT
[List code that was reviewed and confirmed good]

---

**Please confirm:**
1. Do you agree with this final action plan?
2. Any remaining concerns before we implement?
3. Anything else we should consider?

If you agree, respond with "CONSENSUS CONFIRMED" and any final notes.
```

---

## TEMPLATE 5: Quick Sanity Check (Shorter Version)

Use for quick validation rather than full review.

```
**QUICK SANITY CHECK - [AI NAME]**

Another AI (Claude) and I are working on [PROJECT]. Claude suggested the following change:

**The Change:**
[Describe the proposed change]

**Claude's Reasoning:**
[Why Claude thinks this is correct]

**The Code Context:**
```[LANGUAGE]
[Relevant code snippet]
```

**Platform:** [Platform name and key constraints]

**Question:** Do you agree this is the right approach, or do you see issues Claude might have missed?

Keep your response brief - just confirm or raise concerns.
```

---

## Usage Tips

1. **Always include platform context** - External AIs default to generic patterns
2. **Be explicit about what CAN'T change** - Prevents impossible suggestions
3. **Request structured output** - Makes synthesis much easier
4. **Number your rounds** - Keeps conversation trackable
5. **Paste full responses** - Don't summarize, let AIs see exact wording
