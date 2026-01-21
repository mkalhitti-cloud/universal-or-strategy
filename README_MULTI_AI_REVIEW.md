# Multi-AI Code Review Collaboration Kit

## What's Included

### 1. DeepSeek Prompt (Ready to Use)
**File:** `DEEPSEEK_PROMPT_FULL.md`

Copy the entire contents of this file and paste it into DeepSeek. It includes:
- Full platform context so DeepSeek understands NinjaTrader 8 constraints
- Complete code structure (key sections highlighted)
- Specific review scope
- Required response format for easy collaboration
- Existing findings from Gemini/Claude for DeepSeek to confirm or challenge

### 2. AI Collaboration Skill (For Future Use)
**Folder:** `ai-code-review-collaboration/`

A complete skill you can add to your Claude project for future multi-AI reviews:

```
ai-code-review-collaboration/
├── SKILL.md                           # Main skill definition
└── references/
    ├── platform-contexts.md           # Pre-written context blocks for NT8, Rithmic, etc.
    ├── prompt-templates.md            # Copy-paste templates for any AI
    └── synthesis-checklist.md         # How to evaluate external AI suggestions
```

**To install this skill:**
1. In Claude, go to your Project settings
2. Add a new skill
3. Copy the contents of each file into the appropriate location

---

## How to Use the DeepSeek Prompt

### Step 1: Copy the Prompt
Open `DEEPSEEK_PROMPT_FULL.md` and copy everything.

### Step 2: Paste into DeepSeek
Go to DeepSeek and paste the entire prompt.

### Step 3: Get Response
DeepSeek will provide a structured review following the format requested.

### Step 4: Bring Back to Claude
Copy DeepSeek's response and paste it back into this Claude conversation with:
```
Here is DeepSeek's response. Read it and tell me what you think - 
do we have consensus or do we need another round?

---
[PASTE DEEPSEEK'S RESPONSE HERE]
```

### Step 5: Iterate Until Consensus
Continue the back-and-forth until all AIs agree on the action plan.

---

## Current Status: Gemini Review Complete

**Consensus reached with Gemini on:**

| Priority | Fix | Status |
|----------|-----|--------|
| HIGH | StringBuilder pooling (line 1955) | Ready to implement |
| HIGH | Add Print() to empty catch blocks | Ready to implement |
| VERIFIED | Timezone logic | Confirmed correct |
| SKIP | Class splitting | Not applicable to NT8 |
| SKIP | XAML refactoring | Not possible in NT8 |

**Next:** DeepSeek review, then implement agreed fixes as V5.3

---

## Future Use

Whenever you want to get multiple AI perspectives on code:

1. Tell Claude: "I want to bounce this code off other AIs"
2. Claude will use the `ai-code-review-collaboration` skill
3. Claude generates prompts with proper platform context
4. You copy prompts to external AIs
5. Bring responses back to Claude for synthesis
6. Repeat until consensus

This saves you from having to explain platform constraints every time!
