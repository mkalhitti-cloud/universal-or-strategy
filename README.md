# UniversalORStrategy - Complete Project Package

**Version:** V5.3.1  
**Status:** Ready for GitHub + IDE Development  
**Date:** January 2025

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
├── UniversalORStrategyV5_v5_2_MILESTONE.cs ← Current version to use
├── archived-versions/
│   └── All previous strategy versions (V4, V5, etc.)
├── Order_Management.xlsx ← SINGLE SOURCE OF TRUTH for parameters
├── .claude/
│   ├── skills/
│   │   ├── README.md (skills library overview)
│   │   ├── CLAUDE.md (Claude Code preferences)
│   │   ├── core/
│   │   │   └── ninjatrader-strategy-dev.md
│   │   ├── trading/ (add more as needed)
│   │   ├── project-specific/
│   │   │   └── universal-strategy-v6-context.md
│   │   ├── references/
│   │   │   └── live-price-tracking.md (CRITICAL)
│   │   └── changelog/
│   └── context/
│       └── current-session.md
├── CHANGELOG.md (all version history)
├── PLAN.md (development roadmap)
├── QUICK_REFERENCE.md (common Q&A)
├── CLAUDE.md (Claude.ai project context)
├── Trade_Rules.docx (Apex rules summary)
├── CLAUDE_CODE_SKILLS_SETUP.md (complete setup guide)
├── CLAUDE_CODE_QUICK_REFERENCE.md (how to use skills)
├── SETUP_CHECKLIST.md (implementation steps)
├── SKILL_FILES_TEMPLATE.md (ready-to-use templates)
├── WSGTA_Update_Templates.md
├── MILESTONE_V4_0_1_SUMMARY.md
├── MILESTONE_V5_2_SUMMARY.md
├── README_MULTI_AI_REVIEW.md
├── prompt-templates.md
├── synthesis-checklist.md
└── platform-contexts.md
```

---

## 📚 Key Files Reference

### Strategy Code
- **Current:** `UniversalORStrategyV5_v5_2_MILESTONE.cs` (91 KB - the one to compile)
- **Archive:** `archived-versions/` (all previous versions for reference)

### Parameters
- **Single Source of Truth:** `Order_Management.xlsx`
  - All trading parameters
  - Position sizing rules
  - Risk management settings
  - Session-specific configs

### Documentation
- **Changelog:** `CHANGELOG.md` - Full version history and what changed
- **Plan:** `PLAN.md` - Development roadmap
- **Quick Help:** `QUICK_REFERENCE.md` - Common questions answered
- **Overview:** `README.md` - Project introduction

### Claude Code Skills
- **Skills Library:** `.claude/skills/` - Auto-referenced by Claude Code
  - `core/ninjatrader-strategy-dev.md` - Development patterns
  - `references/live-price-tracking.md` - **CRITICAL BUG DOCUMENTATION**
  - `project-specific/universal-strategy-v6-context.md` - Project status
  
- **Session Context:** `.claude/context/current-session.md` - Track progress

### Setup Guides
- **Complete Setup:** `CLAUDE_CODE_SKILLS_SETUP.md` - Full instructions
- **Quick Reference:** `CLAUDE_CODE_QUICK_REFERENCE.md` - How to use skills
- **Checklist:** `SETUP_CHECKLIST.md` - Implementation steps
- **Templates:** `SKILL_FILES_TEMPLATE.md` - Ready-to-use file content

---

## 🎯 Current Status (V5.3.1)

### ✅ Completed
- Opening Range Breakout (ORB) strategy
- RMA click-entry system with Shift+Click orders
- Live price tracking with OnMarketData (CRITICAL FIX)
- ATR-based position sizing
- Dual profit targets (TP1 50%, TP2 100%)
- Rate-limited order modifications (Apex compliance)

### 🔄 In Development
- Fibonacci retracement confluence levels
- FFMA, MOMO, DBDT, TREND strategies
- Memory optimization (currently 80%+, targeting <70%)

### ⚠️ Known Issues
- Memory usage high on systems with limited RAM
- Being addressed through optimization (string pooling, etc.)

---

## 🚀 How to Use This Package

### For Development
1. Clone to your IDE
2. Open in Windsurf/Cursor
3. Ask Claude Code about strategy improvements
4. Make changes to the code
5. Commit to GitHub

### For Learning
- Read `QUICK_REFERENCE.md` for common questions
- Check `.claude/skills/` for detailed documentation
- Review `CHANGELOG.md` for what's been fixed
- Look at `archived-versions/` to see evolution

### For Reference
- Parameter changes: Always edit `Order_Management.xlsx` first
- Price tracking questions: See `live-price-tracking.md` (CRITICAL)
- Development patterns: See `ninjatrader-strategy-dev.md`
- Project status: See `universal-strategy-v6-context.md`

---

## 💡 Using with Claude Code

Ask Claude Code questions like:
```
"Review my code for the Close[0] bug using the live-price-tracking skill"
"Use ninjatrader-strategy-dev to suggest improvements"
"What's the current status of Fibonacci confluence development?"
```

Claude Code will automatically reference the relevant skill files.

---

## 🔑 Critical Information

### The Close[0] Bug
**Problem:** Using `Close[0]` for trailing stops only updates at bar close
**Solution:** See `live-price-tracking.md` for complete fix with code examples
**Status:** FIXED in V5.3.1 using OnMarketData

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
   - Ask Claude Code about your strategy
   - Make improvements
   - Commit changes to GitHub
   - Rinse and repeat

---

**Ready to start?** Upload to GitHub, clone to your IDE, and ask Claude Code your first question! 🚀
