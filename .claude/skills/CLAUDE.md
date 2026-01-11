# Claude Code Preferences for UniversalORStrategy

## Development Principles

### 1. Complete, Compilable Code Only
- Always provide complete code ready to paste and compile
- No snippets or partial solutions
- Specify exact file location and line numbers
- Show before/after when modifying existing code

### 2. Trading-First Language
- Explain concepts in trading terms, not programming jargon
- Use examples from WSGTA methodology when relevant
- Focus on practical impact (execution speed, memory, compliance)
- Acknowledge trading psychology aspects

### 3. Non-Coder Accommodation
- Ask before generating code
- Provide backup instructions (always save copy first)
- Explain the "why" before the "what"
- Test logic conceptually before suggesting implementation

### 4. Critical Safety Checks

**Before ANY code change:**
- [ ] Audit for Close[0] usage (bar-close bug)
- [ ] Check OnMarketData hooks if using live price
- [ ] Verify Apex compliance (no excessive orders)
- [ ] Memory impact assessment (80%+ threshold concern)
- [ ] Execution speed impact (< 50ms target)
- [ ] Rithmic data feed compatibility

**After ANY code change:**
- [ ] Provide complete code, not snippet
- [ ] Show before/after comparison
- [ ] Specify exact file and location
- [ ] Include backup instructions
- [ ] Test logic conceptually

### 5. Performance Non-Negotiables
1. Order execution < 50ms from signal
2. Hotkey response (L/S) instant, no lag
3. Memory efficiency for 20+ simultaneous charts
4. No memory leaks during 12+ hour sessions
5. Minimal garbage collection pauses

### 6. Code Quality Standards
- Always use OnMarketData for live price tracking
- Never use Close[0] for real-time price updates
- Implement GetLivePrice() helper with bid/ask fallbacks
- Use StringBuilder pooling to reduce GC pressure
- Rate-limit stop modifications (max 1/second)
- Proper error handling and logging

## Project Context

### Current State
- **Version:** V5.3.1
- **Last Milestone:** Live price tracking with OnMarketData
- **Current Focus:** RMA click-entry refinement and Fibonacci confluence tools

### Architecture
- **Strategies:** ORB, RMA, FFMA, MOMO, DBDT, TREND (WSGTA)
- **Account:** Apex funded (Rithmic data feed)
- **Instruments:** MES, MGC (micro futures)
- **Data Source:** Order_Management.xlsx (single source of truth)

### Critical Files
- `Order_Management.xlsx` - All trading parameters
- `UniversalORStrategyV5_v5_2_MILESTONE.cs` - Latest stable version
- `.claude/skills/` - This skills library
- `CHANGELOG.md` - Version history and fixes

## Skills Location
All local skills are in `.claude/skills/` folder structure:
- Core development: `core/` subfolder
- Trading methodology: `trading/` subfolder
- Project-specific: `project-specific/` subfolder
- References and guides: `references/` subfolder
- Version changelogs: `changelog/` subfolder

## Communication Preferences
- Start by asking current status and last test results
- Explain trade-offs (performance vs. code complexity)
- Always mention which skill(s) were referenced
- Include memory/execution impact assessment
- For major changes, ask for confirmation before proceeding

## Success Metrics
- Code compiles without errors
- Executes sub-50ms order submission
- No memory leaks in 12+ hour sessions
- Passes multi-AI code review (Claude, Gemini, DeepSeek, Grok)
- Maintains Apex compliance and WSGTA rule adherence
- RMA and ORB entry accuracy within 1-2 ticks

## Last Updated
January 2025 - V5.3.1 milestone
