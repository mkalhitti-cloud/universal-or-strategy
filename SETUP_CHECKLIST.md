# Claude Code Skills Setup - Quick Checklist

**Status:** Ready to implement
**Time Estimate:** 20-30 minutes for complete setup
**Difficulty:** Simple folder creation + file copying

---

## ✅ PART 1: Folder Creation (5 minutes)

Navigate to your NinjaTrader strategies folder and create folders:

- [ ] Create `.claude/` folder
- [ ] Create `.claude/skills/` folder  
- [ ] Create `.claude/skills/core/` subfolder
- [ ] Create `.claude/skills/trading/` subfolder
- [ ] Create `.claude/skills/project-specific/` subfolder
- [ ] Create `.claude/skills/references/` subfolder
- [ ] Create `.claude/skills/changelog/` subfolder
- [ ] Create `.claude/context/` folder

**Done?** You should have 10 folders total (1 + 1 + 8 subfolders)

---

## ✅ PART 2: Create Configuration Files (10 minutes)

### File 1: `.claude/skills/README.md`
- [ ] Create this file
- [ ] Copy content from CLAUDE_CODE_SKILLS_SETUP.md
- Location shown in setup guide

### File 2: `.claude/skills/CLAUDE.md`
- [ ] Create this file
- [ ] Copy content from CLAUDE_CODE_SKILLS_SETUP.md
- Location shown in setup guide

### File 3: `.claude/context/current-session.md`
- [ ] Create this file
- [ ] Copy content from CLAUDE_CODE_SKILLS_SETUP.md
- Location shown in setup guide

**Done?** You should have 3 files created.

---

## ✅ PART 3: Create Core Skill Files (10 minutes)

### Skill 1: `.claude/skills/core/ninjatrader-strategy-dev.md`
- [ ] Create this file
- [ ] Copy content from CLAUDE_CODE_SKILLS_SETUP.md
- Location shown in setup guide
- Size: ~8 KB

### Skill 2: `.claude/skills/references/live-price-tracking.md`
- [ ] Create this file
- [ ] Copy content from CLAUDE_CODE_SKILLS_SETUP.md
- Location shown in setup guide
- Size: ~6 KB

### Skill 3: `.claude/skills/project-specific/universal-strategy-v6-context.md`
- [ ] Create this file
- [ ] Copy content from CLAUDE_CODE_SKILLS_SETUP.md
- Location shown in setup guide
- Size: ~5 KB

**Done?** You should have 3 skill files created.

---

## ✅ PART 4: Verify Setup (5 minutes)

- [ ] All 10 folders exist
- [ ] All 6 files created (3 config + 3 skills)
- [ ] Files contain proper content
- [ ] File paths match the setup guide

**Folder structure check:**
```
.claude/
├── skills/
│   ├── README.md ✓
│   ├── CLAUDE.md ✓
│   ├── core/
│   │   └── ninjatrader-strategy-dev.md ✓
│   ├── trading/ (empty for now)
│   ├── project-specific/
│   │   └── universal-strategy-v6-context.md ✓
│   ├── references/
│   │   └── live-price-tracking.md ✓
│   └── changelog/ (empty for now)
└── context/
    └── current-session.md ✓
```

---

## ✅ PART 5: Test with Claude Code (2 minutes)

Once files are created:

1. [ ] Open Windsurf/Cursor IDE
2. [ ] Open your NinjaTrader strategies folder as a project
3. [ ] Ask Claude Code: "Check my live-price-tracking skill and tell me if my strategy is using OnMarketData correctly"
4. [ ] Claude Code should find and reference the skill file
5. [ ] If it works, setup is complete ✅

---

## 📋 NEXT STEPS (After Setup Complete)

### Additional Skills to Add Later
- [ ] `trading/wsgta-trading-system.md` (trading methodology)
- [ ] `project-specific/apex-rithmic-trading.md` (account/feed)
- [ ] `trading/trading-code-review.md` (quality checklist)
- [ ] `trading/trading-session-timezones.md` (session times)
- [ ] `project-specific/micro-futures-specifications.md` (contract specs)

### Start Using Claude Code
1. [ ] Ask it to review your latest strategy code
2. [ ] Request a specific feature (e.g., Fibonacci confluence)
3. [ ] Ask for Apex compliance verification
4. [ ] Request multi-AI code review checklist

### Maintain the System
1. [ ] After each session, update `.claude/context/current-session.md`
2. [ ] When discovering new bugs, add to `references/critical-bugs.md`
3. [ ] When implementing new features, add to `changelog/`
4. [ ] Keep `.claude/skills/README.md` updated with new files

---

## 🆘 Troubleshooting

**Problem:** Claude Code doesn't find my skill files
- **Solution:** Make sure files are in exactly the right location with exact filenames
- **Check:** File extensions should be `.md`, not `.txt`
- **Verify:** Folder structure matches the guide (case-sensitive on Linux, not on Windows)

**Problem:** I'm not sure if I created the folders correctly
- **Solution:** Open File Explorer, navigate to your NinjaTrader strategies folder
- **Look for:** A `.claude` folder with subfolders inside
- **Tip:** On Windows, hidden folders (starting with `.`) may not show by default
  - Enable "View > Hidden Items" in File Explorer to see them

**Problem:** What if I make a mistake creating a file?
- **Solution:** Just delete it and create a new one
- **Backup:** Keep your Order_Management.xlsx and strategy `.cs` files safe (don't edit them during this setup)

---

## 📞 Getting Help

If you get stuck:
1. Check the detailed CLAUDE_CODE_SKILLS_SETUP.md guide
2. Look at the "Folder Structure" section
3. Compare your folders to the example
4. Look at file locations - they're explicitly shown in the guide

---

## ✨ Success Indicator

You'll know setup is complete when:
- ✅ All folders created
- ✅ All 6 initial files created
- ✅ File locations match the guide
- ✅ Claude Code can reference skill files
- ✅ You can ask Claude Code about your strategy and it references the right skills

**Estimated Time:** 20-30 minutes
**Skill Level Required:** None - just folder/file creation
**Everything Needed:** In the CLAUDE_CODE_SKILLS_SETUP.md file

---

**Ready?** Start with the folders, then create the files. You've got this! 🚀
