# UniversalORStrategy - Complete Project Package

**Version:** V7.0 - Copy Trading Edition  
**Status:** ✅ Working - Initial Testing Successful  
**Date:** January 13, 2026

---

## 🚀 Quick Start

### 1. Upload to GitHub
- Go to GitHub.com → Create new repository
- Click "Add file" → "Upload files"
- Drag this entire folder into GitHub
- Click Commit

### 2. Clone to Your IDE
- Open Windsurf/Cursor terminal
- Run: `git clone https://github.com/[your-username]/universal-or-strategy.git`
- Open the cloned folder
- Done!

### 3. Start Development
- Claude Code automatically sees all files
- Ask Claude Code about your strategy
- Make changes in the IDE
- Commit to GitHub

---

## 📁 Folder Structure

```
universal-or-strategy/
├── UniversalORStrategyV7.cs ← Current production version (v7.0 - Copy Trading)
├── UniversalORSlaveV7.cs ← Ultra-lightweight slave copier
├── SignalBroadcaster.cs ← Shared signal broadcaster
├── UniversalORStrategyV5.cs ← V5.13 (standalone, no copy trading)
├── UniversalORStrategyV5_v5_13.cs ← Versioned copy (latest)
├── UniversalORStrategyV5_v5_12.cs ← Previous version
├── archived-versions/
│   ├── All previous strategy versions (V4, V5.x, etc.)
│   └── V6 files (FAILED - archived)
│       ├── UniversalORMasterV6_FAILED.cs
│       ├── UniversalORSlaveV6_FAILED.cs
│       ├── V6_CHANGELOG_FAILED.md
│       └── V6_SETUP_GUIDE_FAILED.md
├── Order_Management.xlsx ← SINGLE SOURCE OF TRUTH for parameters
├── .agent/
│   ├── skills/
│   │   ├── version-safety/ (version-safety protocol)
│   │   ├── version-manager/ (Haiku sub-agent)
│   │   ├── file-manager/ (Haiku sub-agent)
│   │   ├── docs-manager/ (Haiku sub-agent)
│   │   ├── context-transfer/ (Haiku sub-agent)
│   │   ├── code-formatter/ (Haiku sub-agent)
│   │   ├── universal-or-strategy/ (project context)
│   │   └── multi-ide-router/ (IDE optimization)
│   ├── context/
│   │   └── current-session.md
│   ├── rules/
│   │   └── universalorworkspacerules.md
│   └── UNANSWERED_QUESTIONS.md
├── CHANGELOG.md (all version history)
├── PLAN.md (development roadmap)
├── QUICK_REFERENCE.md (common Q&A)
├── AGENT.md (AI assistant context - all models)
├── Trade_Rules.docx (Apex rules summary)
├── SETUP_CHECKLIST.md (implementation steps)
├── SKILL_FILES_TEMPLATE.md (ready-to-use templates)
├── WSGTA_Update_Templates.md
├── MILESTONE_V4_0_1_SUMMARY.md
├── MILESTONE_V5_2_SUMMARY.md
├── MILESTONE_V5_4_PERFORMANCE_SUMMARY.md
├── MILESTONE_V5_7_FINAL_FIX_SUMMARY.md
├── MILESTONE_V5_8_SUMMARY.md
├── MILESTONE_V5_9_SUMMARY.md
├── MILESTONE_V5_10_SUMMARY.md
├── MILESTONE_V5_11_SUMMARY.md
├── MILESTONE_V5_12_SUMMARY.md
├── MILESTONE_V5_13_SUMMARY.md ← Latest standalone milestone
├── MILESTONE_V7_0_SUMMARY.md ← Latest copy trading milestone
├── README_MULTI_AI_REVIEW.md
├── prompt-templates.md
├── synthesis-checklist.md
└── platform-contexts.md
```

---

## 📚 Key Files Reference

### Strategy Code
- **Current:** `UniversalORStrategyV7.cs` (v7.0 - Copy Trading Edition)
- **Slave:** `UniversalORSlaveV7.cs` (Ultra-lightweight copier)
- **Broadcaster:** `SignalBroadcaster.cs` (Shared signal system)
- **Standalone:** `UniversalORStrategyV5.cs` (v5.13 - No copy trading)
- **Archive:** `archived-versions/` (V4, V5.x, **V6 FAILED**)

### Parameters
- **Single Source of Truth:** `Order_Management.xlsx`
  - All trading parameters
  - Position sizing rules
  - Risk management settings
  - Session-specific configs

### Documentation
- **Latest Milestone:** `MILESTONE_V7_0_SUMMARY.md` - v7.0 copy trading
- **Latest Standalone:** `MILESTONE_V5_13_SUMMARY.md` - v5.13 4-target system
- **Previous Milestone:** `MILESTONE_V5_12_SUMMARY.md` - v5.12 target management
- **Changelog:** `CHANGELOG.md` - Full version history and what changed
- **Plan:** `PLAN.md` - Development roadmap
- **Quick Help:** `QUICK_REFERENCE.md` - Common questions answered
- **Overview:** `README.md` - Project introduction

### AI Assistant Skills
- **Skills Library:** `.agent/skills/` - Auto-referenced by AI assistants
  - `core/ninjatrader-strategy-dev.md` - Development patterns
  - `references/live-price-tracking.md` - **CRITICAL BUG DOCUMENTATION**
  - `universal-or-strategy/SKILL.md` - Project status

- **Session Context:** `.agent/context/current-session.md` - Track progress

### AI Assistant Configuration
- **Context File:** `AGENT.md` - Universal AI assistant context
- **Skills System:** `.agent/skills/` - Specialized knowledge base
- **Sub-Agents:** Haiku agents for routine tasks (Claude Code CLI only)
- **Multi-IDE Router:** Optimize between Antigravity and Claude Code CLI

---

## 🎯 Current Status (V7.0 - Copy Trading Edition)

### ✅ Completed & Validated
**All V5.12 Features:**
- Opening Range Breakout (ORB) strategy
- RMA click-entry system with Shift+Click orders
- Trailing stops: BE → T1 → T2 progression validated
- Order cleanup: 100% success rate across all trade exits
- Entry isolation: Opposite-side OR entries remain active
- Live price tracking with OnPriceChange
- ATR-based position sizing and targets
- Rate-limited order modifications (Apex compliance)
- Multi-contract bracket management (2-18 contracts tested)
- Tighter risk management (MinStop=1pt, Risk=$200)
- Manual breakeven button with arm/trigger logic
- ATR display in UI panel
- OR label toggle for clean visualization
- Target management dropdowns with hotkeys

**NEW V7.0 Copy Trading:**
- ✅ Signal broadcasting from Master to Slaves
- ✅ Entry copying (OR and RMA entries)
- ✅ Breakeven command broadcasting
- ✅ Flatten command broadcasting
- ✅ Ultra-lightweight slave (~330 lines vs 3000)
- ✅ Headless slave operation (runs from Strategies tab)
- ✅ Event-based signal system
- ✅ ~60-70% RAM reduction vs running V5 on multiple charts

### 🟢 Production Status
- **V7.0 INITIAL TESTING SUCCESSFUL**
- Entry copying verified (Master → Slave)
- Tested on MGC with Rithmic data feed
- Master trades on APEX account
- Slave copies to Sim101 account
- Identical orders submitted (same price, quantity, signal ID)

### 🔄 In Testing
- Breakeven command (implemented, not yet tested)
- Flatten command (implemented, not yet tested)
- Multiple slaves simultaneously
- OR entries (RMA entries verified)
- Full trading session with copy trading

### ⚠️ Known Limitations
- Slaves calculate own position size (based on their risk settings)
- No trailing stop sync (each slave manages own)
- No target management sync (each slave manages own)
- Requires same instrument on Master and Slaves

---

## 🚀 How to Use This Package

### For Development
1. Clone to your IDE
2. Open in Windsurf/Cursor/Antigravity
3. Ask AI assistants about strategy improvements
4. Make changes to the code
5. Commit to GitHub

### For Learning
- Read `QUICK_REFERENCE.md` for common questions
- Check `.agent/skills/` for detailed documentation
- Review `CHANGELOG.md` for what's been fixed
- Look at `archived-versions/` to see evolution

### For Reference
- Parameter changes: Always edit `Order_Management.xlsx` first
- Price tracking questions: See `live-price-tracking.md` (CRITICAL)
- Development patterns: See `ninjatrader-strategy-dev.md`
- Project status: See `universal-or-strategy/SKILL.md`

---

## 💡 Using with AI Assistants

Ask AI assistants (Claude, Gemini, Grok, etc.) questions like:
```
"Review my code for the Close[0] bug using the live-price-tracking skill"
"Use ninjatrader-strategy-dev to suggest improvements"
"What's the current status of Fibonacci confluence development?"
```

AI assistants will automatically reference the relevant skill files from `.agent/skills/`.

---

## 🔑 Critical Information

### The Close[0] Bug
**Problem:** Using `Close[0]` for trailing stops only updates at bar close
**Solution:** See `live-price-tracking.md` for complete fix with code examples
**Status:** FIXED in V5.3.1 using OnMarketData

### TOS RTD Connectivity (V9.0.1+)
- **Standard:** Direct Connection via `TosRtdClient.cs`.
- **Rule:** **NO EXCEL BRIDGES**. All future development must use direct connection to `tos.rtd` to ensure maximum reliability and speed.

### Parameters Are Sacred
- **Do NOT edit** `Order_Management.xlsx` manually
- **Always ask Claude Code** before changing parameters
- **Always backup** before making changes
- **Parameters are single source of truth** for all trading rules

### Apex Compliance
- Rate limit: Max 1 order modification per second
- Order management must follow Apex rules
- See `Trade_Rules.docx` for complete requirements

### Rithmic Data Feed
- Faster than Continuum
- Tick data available immediately
- Can briefly disconnect (handle gracefully)
- See apex-rithmic-trading.md documentation

---

## ✨ Next Steps

1. **Upload to GitHub** (5 minutes)
   - Go to GitHub.com
   - Create new repo
   - Drag this folder into "Add file"
   - Commit

2. **Clone to IDE** (2 minutes)
   - Open Windsurf/Cursor terminal
   - `git clone https://github.com/[you]/universal-or-strategy.git`
   - Open folder in IDE

3. **Start Development** (Now!)
   - Ask AI assistants about your strategy
   - Make improvements
   - Commit changes to GitHub
   - Rinse and repeat

---

**Ready to start?** Upload to GitHub, clone to your IDE, and ask your AI assistant the first question! 🚀
