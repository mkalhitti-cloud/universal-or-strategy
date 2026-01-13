# UniversalORStrategy - Complete Project Package

**Version:** V5.12  
**Status:** ✅ Production Ready - Target Management Dropdowns  
**Date:** January 12, 2026

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
├── UniversalORStrategyV5.cs ← Current production version (v5.12)
├── UniversalORStrategyV5_v5_12.cs ← Versioned copy
├── archived-versions/
│   └── All previous strategy versions (V4, V5.x, etc.)
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
├── MILESTONE_V5_4_PERFORMANCE_SUMMARY.md
├── MILESTONE_V5_7_FINAL_FIX_SUMMARY.md
├── MILESTONE_V5_8_SUMMARY.md
├── MILESTONE_V5_9_SUMMARY.md
├── MILESTONE_V5_10_SUMMARY.md
├── MILESTONE_V5_11_SUMMARY.md
├── MILESTONE_V5_12_SUMMARY.md ← Latest milestone
├── README_MULTI_AI_REVIEW.md
├── prompt-templates.md
├── synthesis-checklist.md
└── platform-contexts.md
```

---

## 📚 Key Files Reference

### Strategy Code
- **Current:** `UniversalORStrategyV5.cs` (v5.12 - Production validated)
- **Versioned:** `UniversalORStrategyV5_v5_12.cs`
- **Archive:** `archived-versions/` (all previous versions for reference)

### Parameters
- **Single Source of Truth:** `Order_Management.xlsx`
  - All trading parameters
  - Position sizing rules
  - Risk management settings
  - Session-specific configs

### Documentation
- **Latest Milestone:** `MILESTONE_V5_12_SUMMARY.md` - v5.12 target management dropdowns
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

## 🎯 Current Status (V5.12)

### ✅ Completed & Validated
- Opening Range Breakout (ORB) strategy
- RMA click-entry system with Shift+Click orders
- **Trailing stops**: BE → T1 → T2 progression validated in live trading
- **Order cleanup**: 100% success rate across all trade exits
- **Entry isolation**: Opposite-side OR entries remain active (v5.7 FIX)
- Live price tracking with OnPriceChange (CRITICAL FIX)
- ATR-based position sizing and targets
- Rate-limited order modifications (Apex compliance)
- Multi-contract bracket management (2-18 contracts tested)
- Tighter risk management (MinStop=1pt, Risk=$200)
- **Manual breakeven button**: Click to arm, auto-triggers at entry + buffer (v5.9)
- **ATR display**: Real-time volatility shown in UI panel (v5.10)
- **OR label toggle**: Hide/show chart text for clean visualization (v5.10)
- **Breakeven toggle**: Arm/disarm before trigger, locked after (v5.11)
- **Target management dropdowns**: T1, T2, Runner action menus with hotkeys (v5.12)

### 🟢 Production Status
- **APPROVED FOR LIVE FUNDED TRADING**
- Tested on MES and MGC with Rithmic data feed
- No system freezes or stranded orders
- Clean performance across multiple sessions
- Entry cancellation bug FIXED (v5.7)
- Stop validation VERIFIED (v5.8)
- Manual breakeven TESTED (v5.9)
- ATR display & label toggle WORKING (v5.10)
- Breakeven toggle TESTED (v5.11)
- Target management dropdowns TESTED (v5.12)

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
