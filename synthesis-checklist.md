# External AI Suggestion Evaluation Checklist

How to evaluate suggestions from external AIs before accepting or rejecting them.

---

## Quick Decision Framework

For each suggestion, ask:

### 1. Platform Validity Check
- [ ] Is this possible on the target platform?
- [ ] Does the AI understand the platform's constraints?
- [ ] Would this require changes the platform doesn't support?

**If NO to any â†’ REJECT with explanation**

### 2. Context Validity Check  
- [ ] Does the AI understand the specific use case?
- [ ] Are they applying generic patterns that don't fit?
- [ ] Did they miss critical context provided in the prompt?

**If NO to any â†’ REJECT with explanation**

### 3. Trade-off Analysis
- [ ] What's the benefit of this change?
- [ ] What's the cost (time, risk, complexity)?
- [ ] Is the benefit worth the cost for THIS project?

**If cost > benefit â†’ REJECT or DEFER**

### 4. Risk Assessment
- [ ] Could this change break working code?
- [ ] How hard is it to test?
- [ ] Can we easily revert if it causes issues?

**If high risk + hard to test â†’ REJECT or DEFER**

---

## Scalability & Updateability Evaluation

For suggestions related to scaling or future-proofing:

### 5. Scalability Suggestion Check
- [ ] Does the suggestion align with the stated roadmap/future needs?
- [ ] Is this solving an actual scaling problem or a theoretical one?
- [ ] Does the effort justify the future benefit?
- [ ] Can it be implemented incrementally or does it require big-bang refactor?
- [ ] Will current functionality remain stable during implementation?

**Evaluate against specific roadmap items - reject vague "might need someday" suggestions**

### 6. Updateability Suggestion Check
- [ ] Does this actually make the code easier to modify?
- [ ] Or does it add complexity that makes changes harder?
- [ ] Is the suggested pattern appropriate for this codebase size?
- [ ] Would a developer unfamiliar with the code understand it better after this change?
- [ ] Does it create new dependencies or coupling?

**Simple is usually better than clever - reject over-engineering**

### Scalability Red Flags
- "You'll need this when you scale" (without specific evidence)
- Suggesting patterns designed for 100x your actual scale
- Adding abstraction layers before you have multiple implementations
- Premature multi-threading or async for things that aren't bottlenecks

### Good Scalability Suggestions Look Like
- "Adding FFMA will be easier if you extract this shared function now"
- "This loop will slow down with 20+ positions - here's a specific fix"
- "Your position tracking can't distinguish trade types - add this field"
- Clear extension points with minimal current code changes

---

## Categorization Guide

### âœ… VALID - Accept
- Platform-appropriate
- Context-aware
- Clear benefit
- Low to moderate risk
- Aligns with project goals

### âš ï¸ PARTIALLY VALID - Modify
- Good intent but wrong implementation
- Applies to some but not all cases
- Correct principle but over/under-stated
- Needs adaptation for this platform

### âŒ INVALID - Reject
- Impossible on target platform
- Misunderstands constraints
- Would make things worse
- Based on incorrect assumptions
- Generic advice that doesn't apply

### ðŸ”„ DEFER - Future Consideration
- Valid but not priority now
- Would require major refactoring
- Nice to have, not must have
- Depends on future requirements

---

## Red Flags in External AI Suggestions

Watch out for these patterns that suggest the AI doesn't understand the context:

### Architecture Red Flags

1. **"You should split this into multiple classes/files"**
   - Often impossible or impractical on certain platforms
   - Check if platform supports this pattern

2. **"Use [Framework X] instead"**
   - May not be compatible with platform
   - Migration cost may be prohibitive

3. **"This is an anti-pattern"**
   - Anti-patterns in one context may be best practices in another
   - Platform-specific requirements override general rules

### Performance Red Flags

4. **"For better performance, you should..."**
   - Verify the "slow" code is actually a bottleneck
   - Premature optimization is often suggested

5. **"You need more abstraction"**
   - Abstraction has costs (complexity, debugging)
   - Sometimes simple/direct is better

6. **"Modern best practice is..."**
   - Best practices vary by platform and era
   - What works for web apps may not work for trading systems

### Scalability Red Flags

7. **"You'll need this when you scale"**
   - Vague future-proofing without specific roadmap alignment
   - Building for 100x scale when you need 2x

8. **"Add an interface/abstraction layer now"**
   - Premature abstraction before you have 2+ implementations
   - Adds complexity without current benefit

9. **"This won't scale"**
   - Without specific numbers or benchmarks
   - Based on theoretical concerns not measured reality

10. **"You should use [enterprise pattern]"**
    - Patterns designed for large teams may hurt small projects
    - Complexity cost often exceeds benefit for single-developer codebases

---

## Response Templates

### Agreeing with a Point
```
**âœ… VALID - [Topic]**
You're correct. [Brief explanation of why they're right]. Will implement.
```

### Partially Agreeing
```
**âš ï¸ PARTIALLY VALID - [Topic]**
Your concern is valid, but the implementation you suggest won't work because [platform constraint]. 

Instead, we can [alternative approach that addresses the concern].
```

### Rejecting a Point
```
**âŒ REJECT - [Topic]**
This isn't applicable here because [specific reason].

[Platform name] requires [constraint], which means [why their suggestion doesn't work].

The current implementation is correct for this platform.
```

### Deferring
```
**ðŸ”„ DEFER - [Topic]**
Valid point, but not priority for this phase because [reason].

Will revisit when [condition/milestone].
```

---

## Final Synthesis Template

After all rounds of discussion:

```
## CONSENSUS SUMMARY

### Agreed Fixes (Immediate)
1. [Fix] - [Why both AIs agree]
2. [Fix] - [Why both AIs agree]

### Pre-Scaling Preparation (Before Adding Features)
1. [Change] - Needed before [specific roadmap item]
2. [Change] - Will make [future feature] easier to add

### Rejected Suggestions  
1. [Suggestion] - Rejected because [platform constraint]
2. [Suggestion] - Rejected because [not applicable to use case]
3. [Suggestion] - Rejected because [over-engineering / not needed yet]

### Verified Correct
1. [Code section] - Reviewed and confirmed working as intended
2. [Code section] - No changes needed
3. [Architecture decision] - Appropriate for current and planned scale

### Scalability Assessment Summary
- Ready for: [What the code can handle now]
- Needs work for: [What requires changes before scaling]
- Extension points: [Where new features plug in]

### Open Items
1. [Item] - Needs more investigation
2. [Item] - Deferred to future version
```
