# Universal OR Strategy - Project State

**Last Updated**: 2026-01-18
**Primary Developer**: Mo (non-coder trader, WSGTA expert)
**Platform**: NinjaTrader 8 | Apex Funded Trading | Rithmic Data Feed

---

## Current Version Status

### Production Version: V8.2
**File**: `UniversalORStrategyV8_2.cs`
**Status**: ✅ Stable
**Features**:
- 4-target system (T1, T2, T3, Runner)
- Frequency-based adaptive trailing stops
- Enhanced UI with dropdown menus for target configuration
- OR, RMA, and TREND entry types
- ATR-based position sizing
- Live price tracking with OnMarketData

**Testing**: In production validation
**Deployed To**:
- Project: `c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\`
- NinjaTrader: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\`

---

## Version History Overview

| Version | Date | Status | Key Feature | Notes |
|---------|------|--------|-------------|-------|
| V8.2 | 2026-01 | ✅ Current | 4 targets + frequency trailing | Production |
| V8.1 | 2026-01 | ✅ Stable | Enhanced UI dropdowns | Archived |
| V8.0 | 2026-01 | ✅ Stable | 4-target baseline | Archived |
| V7.0 | 2026-01 | ✅ Stable | Copy trading (Master+Slave) | Archived |
| V5.13 | 2025-01 | ✅ Stable | Standalone 4-target | Archived |
| V5.12 | 2025-01 | ✅ Stable | Target management | Archived |
| V5.3 | 2025-01 | ✅ Stable | Live price tracking fix | Archived |
| V6.x | FAILED | ❌ Failed | Copy trading attempt | Archived, lessons learned |

---

## Planned Versions

### V8_2_UI_HORIZONTAL (Next)
**Status**: 🔄 Planned (plan mode design complete)
**Purpose**: Horizontal button layout redesign
**Changes**:
- 3 horizontal button rows instead of vertical stack
- Row 1: OR Long | OR Short
- Row 2: RMA | TREND
- Row 3: T1 | T2 | T3 | Runner | BE
- Resizable panel (280-600px width)
- Dropdown menus expand below target row

**Plan File**: `C:\Users\Mohammed Khalid\.claude\plans\expressive-zooming-bengio.md`

### Future Considerations
- Additional WSGTA strategies (FFMA, MOMO, DBDT)
- Multi-chart coordination
- Account routing optimization
- Performance profiling and optimization

---

## File Locations

### Project Repository
```
c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\
├── UniversalORStrategyV8_2.cs ← Current production
├── UniversalORStrategyV8.cs
├── UniversalORStrategyV7.cs
├── UniversalORSlaveV7.cs
├── SignalBroadcaster.cs
├── UniversalORStrategyV5.cs
├── archived-versions/
├── Order_Management.xlsx ← SINGLE SOURCE OF TRUTH
├── AGENT.md ← Universal AI context
├── CHANGELOG.md
├── README.md
└── .agent/ ← Skills and context system
```

### NinjaTrader Deployment
```
C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\
├── UniversalORStrategyV8_2.cs ← Current deployed
├── (other versions as needed)
```

---

## Version Safety Protocol

### Critical Rules (NEVER VIOLATE)
1. ✅ **NEVER overwrite existing files** - Always create new with descriptive suffix
2. ✅ **NEVER auto-commit to GitHub** - Only when user explicitly requests
3. ✅ **ALWAYS deploy to both locations** - Project + NinjaTrader
4. ✅ **ALWAYS update class name** - Match filename (e.g., `public class UniversalORStrategyV8_2_UI_HORIZONTAL`)
5. ✅ **ALWAYS update UI title** - Shows version (e.g., "OR Strategy V8.2 UI HORIZONTAL")

### File Naming Convention
| Change Type | Pattern | Example |
|-------------|---------|---------|
| UI changes | `_UI_[DESCRIPTION]` | `V8_2_UI_HORIZONTAL.cs` |
| Bug fixes | `_FIX_[WHAT]` | `V8_2_FIX_STOPS.cs` |
| New feature | `_[FEATURE_NAME]` | `V8_2_COPY_TRADING.cs` |
| Test/experiment | `_TEST_[WHAT]` | `V8_2_TEST_SCALING.cs` |

---

## Sub-Agent Architecture (Claude Code CLI)

### Model Hierarchy
```
Haiku ($0.25/M)   → File ops, simple tasks (auto-spawned)
Sonnet ($3/M)     → Coordination, context (primary CLI model)
Opus ($15/M)      → Code work, critical logic (fallback)
Opus Thinking     → Manual switch only (emergencies)
```

### Active Sub-Agent Skills
Located in `.agent/skills/`:

1. **version-safety** - Never overwrite files, descriptive naming
2. **version-manager** (Haiku) - Load/list versions
3. **file-manager** (Haiku) - Create and deploy to both locations
4. **docs-manager** (Haiku) - Update CHANGELOG.md and milestones
5. **context-transfer** (Haiku) - Generate handoff prompts for new sessions
6. **code-formatter** (Haiku) - Clean up C# code (remove debug, fix indentation)
7. **multi-ide-router** - Optimize between Antigravity and Claude Code CLI
8. **opus-critical** - When to use Opus vs Opus Thinking vs Sonnet

---

## Multi-IDE Workflow

### PRIMARY: Antigravity IDE (Opus 4.5 Thinking)
**Use For**:
- ALL code work (creating, modifying, debugging)
- Critical trading logic decisions
- Complex problem-solving with reasoning

**Benefits**:
- Separate credit pool
- Shows reasoning process
- Best for code quality

### SECONDARY: Claude Code CLI
**Use For**:
- Context gathering (Sonnet)
- Prompt generation (Sonnet)
- Routine file operations (Haiku auto-spawned)
- Documentation updates (Haiku auto-spawned)

**Benefits**:
- Cost-effective for routine tasks
- Sub-agent automation
- Fallback when Antigravity depleted

---

## Key Project Constraints

### Trading Environment
- **Platform**: NinjaTrader 8
- **Data**: Rithmic feed (NOT Continuum)
- **Broker**: Apex Funded Account
- **Instruments**: MES, MGC (micro futures)
- **User**: Non-coder trader (needs complete, compilable code)

### Technical Constraints
- **RAM**: Limited (80%+ usage on 20+ charts)
- **Execution**: Must be < 50ms order submission
- **Reliability**: Live funded trading (zero bugs acceptable)
- **Orders**: IsUnmanaged=true (full control)
- **Compliance**: Apex rules (rate limiting, position limits)

### Critical Bug to Avoid
**Close[0] Bug**: Using `Close[0]` for real-time decisions only updates at bar close
**Solution**: Use `OnMarketData()` for tick-level price tracking
**Reference**: See live-price-tracking skill

---

## Single Source of Truth

**File**: `Order_Management.xlsx`

ALL trading parameters come from this spreadsheet:
- Position sizing rules
- Risk management settings
- Profit target distances
- Stop loss parameters
- Session timing
- ATR multipliers

**Rule**: Change Excel FIRST, then code reads from it.

---

## Current Development Phase

### Phase 2: Enhanced Features (Current)
- ✅ V8.2 4-target system complete
- ✅ Frequency-based trailing implemented
- 🔄 UI redesign planned (horizontal layout)
- 🔄 Production testing and refinement

### Phase 3: Future
- Multi-chart coordination
- Additional WSGTA strategies
- Performance optimization
- Advanced alerting system

---

## Success Metrics

### Code Quality
- ✅ Compiles without warnings
- ✅ Sub-50ms execution
- ✅ No memory leaks (12+ hour test)
- ✅ Apex compliance maintained

### Trading Performance
- ✅ 4-target management functional
- 🔄 Frequency-based trailing in testing
- ✅ Live price tracking active (OnMarketData)
- ✅ Version safety protocol followed

---

## Important Notes

### For AI Assistants
- Read `AGENT.md` for universal AI context
- Reference `.agent/skills/` for specialized knowledge
- Follow version-safety protocol for ALL code changes
- Use trading terminology (not coding jargon) when explaining to Mo
- Always provide complete, compilable code (no snippets)

### For Mo
- Current version: V8.2 (4 targets + frequency trailing)
- Next planned: V8_2_UI_HORIZONTAL (horizontal layout)
- Version safety: ON (no file overwrites)
- Multi-IDE setup: Antigravity for code, Claude Code CLI for routine tasks

---

## Quick Reference

### Current Active Files
- **Production**: `UniversalORStrategyV8_2.cs`
- **Parameters**: `Order_Management.xlsx`
- **AI Context**: `AGENT.md`
- **Skills**: `.agent/skills/` (8 sub-agents)
- **Plans**: Horizontal UI redesign planned

### Hotkeys (V8.2)
- **L** - Execute Long
- **S** - Execute Short
- **F** - Flatten All

### Key Variables
- `sessionHigh`, `sessionLow` - OR levels
- `target1Frequency`, `target2Frequency`, etc. - Hit tracking
- `activePositions` - Position tracking dictionary

---

**Last Review**: 2026-01-18
**Next Review**: After V8_2_UI_HORIZONTAL deployment
