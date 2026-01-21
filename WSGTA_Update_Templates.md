# ðŸ“‹ WSGTA Skill Update Templates
## Keep This File Handy!

---

## ðŸš€ QUICK TEMPLATES (Copy & Paste)

### Single Parameter Change
```
UPDATE: [Parameter] changed from [old] to [new]
Please update the wsgta skill and give me the new file.
```

### Multiple Changes
```
PARAMETER UPDATE REQUEST
========================
Changes made:
1. [Parameter]: [old] â†’ [new]
2. [Parameter]: [old] â†’ [new]
3. [Parameter]: [old] â†’ [new]

Please update the wsgta skill and give me the new file.
```

### New Spreadsheet Upload
```
I've uploaded the updated Order_Management.xlsx

Changes from previous version:
- [List what changed]

Please update the wsgta-trading-system skill and changelog.
```

### Full Refresh (When in Doubt)
```
FULL SKILL REFRESH
==================
I've uploaded my current Order_Management.xlsx
Please regenerate the complete wsgta-trading-system skill.
```

### Code + Skill Update
```
PARAMETER + CODE UPDATE
=======================
Changes: [Parameter]: [old] â†’ [new]

Please update:
â˜‘ wsgta-trading-system skill
â˜‘ Strategy code

Give me both files.
```

---

## ðŸ“ PARAMETER NAMES REFERENCE

Use these exact names when requesting changes:

| Category | Parameter Names |
|----------|-----------------|
| **Targets** | Target 1, Target 2, Target 3 |
| **Breakeven** | BE Trigger, BE Offset |
| **Trail 1** | Trail 1 Trigger, Trail 1 Distance |
| **Trail 2** | Trail 2 Trigger, Trail 2 Distance |
| **Trail 3** | Trail 3 Trigger, Trail 3 Distance |
| **Stops** | ORB Stop, RMA Stop, FFMA Stop, MOMO Stop, DBDT Stop |
| **TREND Stops** | TREND 9 EMA Stop, TREND 15 EMA Stop |
| **Entry Buffers** | ORB Entry Buffer, MOMO Entry Buffer |
| **ORB Targets** | OR Target 1, OR Target 2 |

---

## âœ… WHAT HAPPENS WHEN YOU UPDATE

1. Claude acknowledges changes
2. Updates reference files in skill
3. Adds entry to changelog
4. Repackages skill
5. Gives you download link
6. (If requested) Updates strategy code

---

## ðŸ“ FILE MANAGEMENT

After downloading updated skill:
1. Go to your Claude Project
2. Delete old `wsgta-trading-system.skill`
3. Upload new skill file
4. Done!

---

## ðŸ’¡ TIPS

- **Be specific:** Use exact parameter names above
- **Include old AND new:** Helps verify correct change
- **Attach spreadsheet** for major updates
- **One update at a time:** Easier to verify
