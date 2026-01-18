# Unanswered Questions & Future Tasks

This file tracks questions, ideas, and tasks that need to be addressed later. All agents (Sonnet, Opus, Haiku sub-agents) can access this file.

**Last Updated**: 2026-01-18

---

## Pending Questions

### 1. Opus Sub-Agent Configuration
**Question**: Should we create Opus-specific sub-agent skills for complex tasks?

**Context**:
- Currently have 5 Haiku sub-agents (version-manager, file-manager, docs-manager, context-transfer, code-formatter)
- Opus could handle deep analysis, architecture design, critical debugging
- Need to decide: manual Opus switching vs. automatic Opus sub-agent spawning

**Considerations**:
- Cost: Opus is ~60x more expensive than Haiku
- Use cases: When is Opus truly needed vs. Sonnet being sufficient?
- Control: User manual switching vs. agent auto-delegation

**Status**: Not yet decided

---

### 2. Antigravity IDE Cheat Sheet
**Question**: Create a model-selection guide for Antigravity IDE?

**Context**:
- Antigravity has multiple models but no auto-delegation
- User manually switches between Gemini Flash, Claude Sonnet, Claude Opus
- Could create a quick reference guide for which tasks use which model

**Proposed Solution**:
- Markdown cheat sheet listing tasks and recommended models
- Could be a skill or standalone doc

**Status**: Not yet created

---

## Completed Questions

### ✅ Can Opus use the Haiku sub-agent skills?
**Answer**: Yes, all Claude models see the same `.agent/skills/` folder and can use the skills automatically. No additional configuration needed.

**Resolved**: 2026-01-18

---

## Future Task Ideas

### Code Optimization
- Profile V8_2 for memory usage during long sessions
- Identify bottlenecks in OnBarUpdate or ManageTrailingStops
- Consider object pooling for frequently allocated objects

### UI Enhancements
- Keyboard shortcuts for target dropdown menus
- Customizable button colors
- Save/load UI layout preferences

### Testing Automation
- Create automated test suite for OR detection
- Market Replay regression tests
- Performance benchmarks

### Documentation
- Create video walkthrough of V8 features
- Update README with V8 architecture
- Add troubleshooting guide for common issues

---

## Agent Instructions

### How to Use This File

**When you encounter a question you can't answer immediately**:
1. Add it to "Pending Questions" section
2. Include context and considerations
3. Mark status as "Not yet decided" or "Waiting for user input"

**When you complete a question**:
1. Move it to "Completed Questions" section
2. Add the answer and resolution date
3. Mark with ✅

**When suggesting future work**:
1. Add to "Future Task Ideas" section
2. Keep descriptions brief
3. Group related ideas together

### File Location
This file is in `.agent/` directory so all agents can access it, but it's not a skill itself.

---

## Notes

- This file is version-controlled (in git)
- Any agent can update it
- User can add questions manually too
- Keep it organized and up-to-date
