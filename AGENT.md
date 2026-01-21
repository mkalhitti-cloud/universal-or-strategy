# AGENT.md - AI Assistant Context File

This file provides context for ALL AI assistants (Claude, Gemini, Grok, DeepSeek, etc.) working on this codebase.

## Project Overview

**Universal Opening Range Strategy** - A NinjaTrader 8 trading strategy for opening range and other trade setups.

**Purpose**: Live funded trading on Apex accounts using Rithmic data feed.

**User Profile**: Mo - Non-coder trader who needs complete, compilable code with clear instructions.

---

## Critical Context

### Trading Environment
- **Platform**: NinjaTrader 8
- **Data Feed**: Rithmic (NOT Continuum)
- **Broker**: Apex Funded Account
- **Instruments**: MES (Micro E-mini S&P), MGC (Micro Gold)
- **User Location**: California (Pacific Time, but trades on Eastern Time)

### Architecture Constraints
- **IsUnmanaged = true**: Strategy uses unmanaged orders for full control
- **RAM Sensitivity**: Must run on laptop with 20+ charts, memory at 80%+
- **Reliability Critical**: Live funded trading - no bugs allowed
- **Future Scale**: Architecture must support 20 accounts eventually

---

## Current Version: V8.2+

### Latest Versions
- **V8.2**: 4-target system with frequency-based trailing stops (Current Production)
- **V8_2_UI_HORIZONTAL** (Planned): Horizontal button layout redesign. [Plan Reference](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent-cli/plans/expressive-zooming-bengio.md)
- **V7.0**: Copy trading edition (Master + Slave architecture)
- **V5.13**: Standalone 4-target system (no copy trading)
- **V9 External Remote**: C# WPF application for external trade orchestration and TOS RTD integration.

### Key V8 Features
1. **4-Target System** - T1, T2, T3, Runner with individual management
2. **Frequency-Based Trailing** - Adaptive trailing based on hit frequency
3. **Enhanced UI** - Dropdown menus for target configuration
4. **RMA + TREND Entries** - Multiple entry types beyond OR
5. **Copy Trading** (V7) - Master/Slave signal broadcasting

### V8 vs V7 vs V5 Comparison
| Feature | V5.13 | V7.0 | V8.2 |
|---------|-------|------|------|
| Targets | 2 (T1, T2) | 2 (T1, T2) | 4 (T1, T2, T3, Runner) |
| Trailing | Fixed ATR | Fixed ATR | Frequency-based adaptive |
| Copy Trading | ❌ | ✅ | ❌ (standalone) |
| UI | Vertical stack | Vertical stack | Dropdown menus |
| Entry Types | OR, RMA | OR, RMA | OR, RMA, TREND |

---

## Code Standards

### For Mo (Non-Coder)
1. **ALWAYS** provide complete, compilable code blocks
2. **ALWAYS** specify exact file location (region, method name)
3. **ALWAYS** show before/after when modifying existing code
4. **ALWAYS** include backup instructions before changes
5. **NEVER** provide partial code snippets
6. **NEVER** use coding jargon without explanation

### NinjaTrader Specifics
- Use `Print()` for debugging (shows in Output window)
- UI changes must use `ChartControl.Dispatcher.InvokeAsync()`
- Order names must be unique (use timestamp suffix)
- Stop orders need validation (can't be at/past market price)

### Naming Conventions
- Entry orders: `Long_[timestamp]` or `Short_[timestamp]`
- Stop orders: `Stop_[entryName]`
- Target orders: `T1_[entryName]`, `T2_[entryName]`, etc.
- Flatten orders: `Flatten_[entryName]`

---

## Version Safety Protocol

### CRITICAL RULES
1. **NEVER overwrite existing files** - Always create new file with descriptive suffix
2. **NEVER auto-commit to GitHub** - Only commit when user explicitly requests
3. **ALWAYS deploy to both locations** - Project repo AND NinjaTrader strategies folder
4. **ALWAYS confirm filename** - Ask user before saving if suffix is unclear

### File Naming Convention
| Change Type | Naming Pattern | Example |
|-------------|----------------|---------|
| UI changes | `_UI_[DESCRIPTION]` | `V8_2_UI_HORIZONTAL.cs` |
| Bug fixes | `_BUGFIX` or `_FIX_[WHAT]` | `V8_2_FIX_STOPS.cs` |
| New feature | `_[FEATURE_NAME]` | `V8_2_COPY_TRADING.cs` |
| Test/experiment | `_TEST_[WHAT]` | `V8_2_TEST_SCALING.cs` |

### Deployment Locations
```
1. Project Repository:
   c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\

2. NinjaTrader Strategies Folder:
   C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\
```

Both files must have:
- Matching class name (update `public class ClassName` to match filename)
- Updated version string in UI title bar
- Same functionality

---

## Skills Directory & Catalog

### Finding the Right Skill
All skills are located in `.agent/skills/` directory (37 total, organized into 8 categories).

**Quick Reference:**
- 🔍 **Search by task**: See [skills_catalog.md](.agent/skills/skills_catalog.md)
- 📋 **Organized by category**: Core Infrastructure, Code Development, NinjaTrader Trading, Documents, File Mgmt, UI/Design, Analytics, Utilities
- 💰 **Cost comparison**: Which skills save money vs which require Opus
- 🔗 **Dependency graph**: Which skills work together

**Essential Skills for This Project:**
1. **delegation-bridge** - Route file I/O to Gemini Flash (save 99% on deployment)
2. **multi-ide-router** - Choose right AI model for each task
3. **version-safety** - Safe file versioning and deployment
4. **ninjatrader-strategy-dev** - NinjaTrader-specific development
5. **file-manager** - Deploy to both project repo and NinjaTrader bin

**Start here:** [.agent/skills/skills_catalog.md](.agent/skills/skills_catalog.md)

---

## Sub-Agent Architecture (Claude Code CLI Only)

### Model Hierarchy
```
Haiku ($0.25/M)   → Routine file operations, simple tasks
Sonnet ($3/M)     → Coordination, context gathering
Opus ($15/M)      → Code work and critical logic
Opus Thinking     → Manual switch only (emergencies)
```

### Available Sub-Agent Skills (Haiku)
Located in `.agent/skills/` folder:

1. **version-manager** - Load/list strategy versions
2. **file-manager** - Create and deploy new files to both locations
3. **docs-manager** - Update CHANGELOG.md and milestones
4. **context-transfer** - Generate handoff prompts for new sessions
5. **code-formatter** - Clean up C# code (remove debug prints, fix indentation)

### Multi-IDE & Model Delegation Workflow
**RULE**: Maximize intelligence for logic; maximize speed/saving for operations.

1. **OPUS (The Brain)**: 
   - Mandatory for all trading logic, math, and code architecture.
   - **PROTOCOL**: Opus produces code/designs but SHOULD NOT perform redundant `write_to_file` operations for deployment. 
   - **HANDOFF**: Opus outputs a "Deployment Manifest" (Filename + Version) and instructs the user to switch to Gemini Flash.

2. **GEMINI FLASH (The Hands)**:
   - Mandatory for file I/O, documentation updates, and deployment.
   - **PROTOCOL**: Gemini Flash reads the Brain's output, applies the `file-manager` skill, and performs the dual-deployment to Project + NinjaTrader.
   - **BENEFIT**: ~94% cost reduction for file writing.

### Opus Deployment in Antigravity IDE

**IMPORTANT**: Opus generates code but cannot auto-deploy without configuration.

**3 Deployment Options:**

1. **Option 1: MCP Bridge (Recommended)** - Fully automated
   - Opus calls `call_gemini_flash()` via MCP
   - Cost: $0.0001 per deployment
   - Time: 0 minutes (automatic)
   - **See**: [opus-deployment-guide](.agent/skills/opus-deployment-guide/SKILL.md) for setup

2. **Option 2: Haiku Handoff** - No setup required
   - Opus returns code → Copy/paste to Claude Code CLI → Haiku deploys
   - Cost: $0.004 per deployment
   - Time: 2 minutes (manual copy/paste)
   - **See**: [opus-deployment-guide](.agent/skills/opus-deployment-guide/SKILL.md)

3. **Option 3: Manual** - Fallback
   - You save files manually to both locations
   - Cost: $0.12 + your time
   - Time: 5 minutes (fully manual)
   - **See**: [opus-deployment-guide](.agent/skills/opus-deployment-guide/SKILL.md)

**Decision**: Use Option 1 if time permits setup, Option 2 for quick deployment, Option 3 for emergency only.

### Multi-IDE Workflow

**PRIMARY IDE**: Antigravity (Opus 4.5 Thinking)
- ALL code work happens here first
- Shows reasoning for decisions
- Separate credit pool
- Returns code with deployment instructions

**SECONDARY IDE**: Claude Code CLI
- Sonnet: Context gathering, prompt generation
- Haiku: Routine file/doc operations (auto-spawned)
- Opus: Fallback when Antigravity depleted
- Handles deployment if Opus uses Option 2

**ALTERNATIVE IDE**: Cursor (with Gemini 3 Flash)
- Gemini Flash: Direct native deployment (no MCP needed)
- Cost: $0.0001 per deployment
- Speed: Fastest option available
- Use for file operations and routine tasks

---

## Universal AI & IDE Setup

### IDE-Agnostic Configuration
This project is designed to work with ANY AI model (Claude, Gemini, Grok, etc.) in ANY IDE (Antigravity, Claude Code, Cursor, Windsurf).

**Key Configuration Files:**
- `.agent-cli/settings.local.json` - IDE-specific settings (uses `${PROJECT_ROOT}`, `${USERNAME}` variables)
- `.agent/config/deployment_targets.json` - Where to deploy files (NinjaTrader paths)
- `.agent/config/ai_capabilities.json` - Cost & capability matrix for all AI models
- `.agent/state/session_state.json` - Current session state (pick up where you left off)

### Switching IDEs Mid-Project
1. `git push` from current IDE
2. Open new IDE with fresh account/credits
3. `git pull` to latest code
4. Read `.agent/state/session_state.json` to see where you left off
5. Any AI reads skills from `.agent/skills/` (universal, not AI-specific)
6. Continue work seamlessly

### Path Resolution
All hardcoded paths have been replaced with variables:
- `${PROJECT_ROOT}` → `c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\`
- `${USERNAME}` → `Mohammed Khalid` (auto-detected from OS)
- `${NINJATRADER_BIN}` → `C:\Users\${USERNAME}\Documents\NinjaTrader 8\bin\Custom\Strategies\`

**Result**: Same config works on different machines, different users, different PCs.

### Universal Skills Library
All 37 skills in `.agent/skills/` work with ANY AI model:
- No Claude-specific API calls
- No hardcoded paths
- No vendor lock-in
- Framework: MCP (Model Control Protocol) compatible

---

## Common Issues & Solutions

### "Stop at market" Error
**Cause**: Stop price too close to current price
**Solution**: SubmitValidatedStop() method ensures 4-tick buffer

### Position Tracking Mismatch
**Cause**: External flatten (Control Center) doesn't notify strategy
**Solution**: OnPositionUpdate() cleans up internal tracking

### Memory Growth
**Cause**: Rays accumulate, strings allocated repeatedly
**Solution**: V4+ uses single box, StringBuilder pooling

### Order Rejection
**Cause**: Various (stop at market, duplicate name, insufficient margin)
**Solution**: Unique order names, validated stop prices, risk checks

### Close[0] Bug (CRITICAL)
**Cause**: Using `Close[0]` for trailing stops only updates at bar close
**Solution**: Use OnMarketData() for tick-level price tracking
**Reference**: See `.agent/skills/references/live-price-tracking.md`

---

## Testing Checklist

Before any production deployment:
- [ ] Compiles without errors or warnings
- [ ] Test on Market Replay (not just backtest)
- [ ] Sim account for at least 1 hour
- [ ] Verify OR box draws correctly (if applicable)
- [ ] Verify hotkeys work (L/S/F)
- [ ] Verify stops submit correctly
- [ ] Verify targets submit correctly
- [ ] Verify trailing stop updates
- [ ] Test flatten functionality
- [ ] Monitor memory for 1+ hour
- [ ] Test UI buttons/dropdowns (V8)
- [ ] Verify copy trading (V7 only)

---

## File Structure

```
universal-or-strategy/
├── UniversalORStrategyV8.cs ← V8.0 baseline
├── UniversalORStrategyV8_2.cs ← V8.2 current (4 targets + frequency trailing)
├── UniversalORStrategyV7.cs ← V7.0 copy trading (Master)
├── UniversalORSlaveV7.cs ← V7.0 slave copier
├── SignalBroadcaster.cs ← Shared signal broadcaster
├── UniversalORStrategyV5.cs ← V5.13 standalone
├── archived-versions/ (V4, V5.x, V6 FAILED)
├── Order_Management.xlsx ← SINGLE SOURCE OF TRUTH
├── .agent/
│   ├── skills/ (sub-agent skills)
│   ├── context/ (session tracking)
│   ├── rules/ (workspace rules)
│   └── UNANSWERED_QUESTIONS.md
├── CHANGELOG.md (all version history)
├── PLAN.md (development roadmap)
├── QUICK_REFERENCE.md (common Q&A)
├── AGENT.md (this file - AI context)
├── README.md (project overview)
└── MILESTONE_*.md (version summaries)
```

---

## Communication Protocol

### When Starting a Session
Ask Mo for:
1. Current status of last changes
2. Results of last testing
3. Immediate goals for this session

### When Ending a Session
Provide Mo with:
1. Summary of changes made
2. What was tested and results
3. Next steps recommended
4. Any risks or concerns

### Code Changes
1. Always show the complete method/region being changed
2. Explain in trading terms (not coding terms)
3. Provide step-by-step instructions for implementation
4. Include backup reminder
5. Follow version-safety protocol (new file, descriptive name)

---

## Quick Reference

### Key Methods
- `OnBarUpdate()` - Main logic loop, called on each bar
- `OnOrderUpdate()` - Handles all order state changes
- `OnPositionUpdate()` - Detects external position changes
- `ExecuteLong()` / `ExecuteShort()` - Entry submission
- `ManageTrailingStops()` - Updates stops based on profit
- `SubmitValidatedStop()` - Safe stop submission with validation
- `FlattenAll()` - Emergency exit all positions

### Key Variables (V8.2)
- `sessionHigh`, `sessionLow`, `sessionRange` - OR levels
- `orComplete` - True when OR window ends
- `activePositions` - Dictionary of tracked positions
- `tickSize`, `pointValue` - Instrument specifics
- `target1Frequency`, `target2Frequency`, etc. - Hit tracking for trailing

### Hotkeys
- **L** - Execute Long
- **S** - Execute Short
- **F** - Flatten All

---

## Skills System (.agent/skills/)

The `.agent/skills/` folder contains specialized knowledge files that AI assistants can reference:

### Core Skills
- `ninjatrader-strategy-dev.md` - NinjaTrader development patterns
- `version-safety/SKILL.md` - File versioning protocol

### Sub-Agent Skills (Haiku)
- `version-manager/SKILL.md` - Load/list versions
- `file-manager/SKILL.md` - Create and deploy files
- `docs-manager/SKILL.md` - Update documentation
- `context-transfer/SKILL.md` - Generate handoff prompts
- `code-formatter/SKILL.md` - Clean up C# code

### Project Skills
- `universal-or-strategy/SKILL.md` - Project context and status

### Multi-IDE Skills
- `multi-ide-router/SKILL.md` - Optimize IDE and model selection
- `opus-critical/SKILL.md` - When to use Opus vs Opus Thinking

### References
- `live-price-tracking.md` - CRITICAL: Close[0] bug fix

---

## Version History Summary

| Version | Key Feature | Status |
|---------|-------------|--------|
| V8.2 | 4 targets + frequency-based trailing | Current |
| V8.1 | Enhanced UI with dropdown menus | Stable |
| V8.0 | 4-target system baseline | Stable |
| V7.0 | Copy trading (Master + Slave) | Stable |
| V5.13 | Standalone 4-target system | Archived |
| V5.12 | Target management improvements | Archived |
| V5.3 | Live price tracking fix | Archived |
| V4 | Box visualization, RAM optimized | Archived |
| V3 | Multi-target, trailing stops | Archived |
| V2 | Unmanaged orders, basic bracket | Archived |

---

## For AI Assistants

### Universal Compatibility
This file uses AGENT.md naming (not CLAUDE.md) to work with all AI models:
- ✅ Claude (Anthropic)
- ✅ Gemini (Google)
- ✅ Grok (xAI)
- ✅ DeepSeek
- ✅ GPT (OpenAI)
- ✅ Any future AI models

### How to Use This File
1. Read this file to understand project context
2. Reference `.agent/skills/` for specialized knowledge
3. Follow version-safety protocol for all code changes
4. Use trading terminology (not coding jargon) when explaining to Mo
5. Always provide complete, compilable code
6. Test conceptually before suggesting implementation

### Critical Reminders
- **NEVER use Close[0] for real-time price tracking** (use OnMarketData)
- **NEVER overwrite existing files** (always create new with descriptive suffix)
- **NEVER auto-commit to GitHub** (ask first)
- **ALWAYS deploy to both locations** (project + NinjaTrader)
- **ALWAYS reference Order_Management.xlsx** (single source of truth for parameters)

---

**Last Updated**: 2026-01-20 (Skill Standardization & Flash Integration)
