---
name: skill-creator
description: Create new skills, improve existing skills, and measure skill performance. Use when users want to create a skill from scratch, update or optimize an existing skill, run evals to test a skill, or benchmark skill performance with variance analysis. Trigger this skill whenever someone asks to "make a skill", "create a workflow", "turn this into a skill", "improve an existing skill", or wants to automate a repeated process into a reusable skill.
---

# Skill Creator

A skill for creating new skills and iteratively improving them.

> SOURCE: Official Anthropic claude-plugins-official repo
> https://github.com/anthropics/claude-plugins-official/tree/main/plugins/skill-creator
> Integrated: 2026-03-04 — Cross-Agent Parity Initiative

At a high level, the process of creating a skill goes like this:

- Decide what you want the skill to do and roughly how it should do it
- Write a draft of the skill
- Create a few test prompts and run claude-with-access-to-the-skill on them
- Help the user evaluate the results both qualitatively and quantitatively
  - While the runs happen in the background, draft some quantitative evals if there aren't any
  - Use the `eval-viewer/generate_review.py` script to show the user the results
  - Let them look at the quantitative metrics
- Rewrite the skill based on feedback from the user's evaluation of the results
- Repeat until satisfied
- Expand the test set and try again at larger scale

Your job when using this skill is to figure out where the user is in this process and then jump in and help them progress through these stages.

---

## Communicating with the User

Pay attention to context cues to understand how technical your communication should be.

- "evaluation" and "benchmark" are borderline but OK for this project
- For "JSON" and "assertion" — this project's users are technical (trading strategy developers), so these terms are fine
- Still explain terms briefly if you're in doubt

---

## Creating a Skill

### Capture Intent

Start by understanding the user's intent. The current conversation might already contain a workflow the user wants to capture. If so, extract answers from the conversation history first.

1. What should this skill enable Claude to do?
2. When should this skill trigger? (what user phrases/contexts)
3. What's the expected output format?
4. Should we set up test cases to verify the skill works?

### Interview and Research

Proactively ask questions about edge cases, input/output formats, example files, success criteria, and dependencies. Wait to write test prompts until you've got this part ironed out.

Check available MCPs — if useful for research, research in parallel via subagents if available.

### Write the SKILL.md

Based on the user interview, fill in these components:

- **name**: Skill identifier
- **description**: When to trigger, what it does. Make it slightly "pushy" — Claude tends to undertrigger skills. Instead of "How to do X", write "How to do X. Use this whenever the user mentions X, Y, or Z, even if they don't explicitly ask."
- **compatibility**: Required tools, dependencies (optional)
- **the rest of the skill body**

### Skill Writing Guide

#### Anatomy of a Skill

```
skill-name/
├── SKILL.md (required)
│   ├── YAML frontmatter (name, description required)
│   └── Markdown instructions
└── Bundled Resources (optional)
    ├── scripts/    - Executable code for deterministic/repetitive tasks
    ├── references/ - Docs loaded into context as needed
    └── assets/     - Files used in output (templates, icons, fonts)
```

#### Progressive Disclosure

Skills use a three-level loading system:

1. **Metadata** (name + description) - Always in context (~100 words)
2. **SKILL.md body** - In context whenever skill triggers (<500 lines ideal)
3. **Bundled resources** - As needed (unlimited, scripts can execute without loading)

Keep SKILL.md under 500 lines. If approaching the limit, add hierarchy with clear pointers.

For large reference files (>300 lines), include a table of contents.

#### Writing Style

- Use imperative form in instructions
- Explain the _why_ behind rules — don't just write "ALWAYS" or "NEVER"
- Theory of mind: understand what the user actually needs and transmit that understanding
- Write a draft, then look at it fresh and improve it
- Remove things that aren't pulling their weight

#### Test Cases

After writing the skill draft, come up with 2-3 realistic test prompts. Save to `evals/evals.json`:

```json
{
  "skill_name": "example-skill",
  "evals": [
    {
      "id": 1,
      "prompt": "User's task prompt",
      "expected_output": "Description of expected result",
      "files": []
    }
  ]
}
```

---

## Running and Evaluating Test Cases

This section is one continuous sequence — don't stop partway through.

Put results in `<skill-name>-workspace/` as a sibling to the skill directory.
Organize by iteration (`iteration-1/`, `iteration-2/`) and within that by test case (`eval-0/`, etc.).
Don't create all directories upfront — create them as you go.

### Step 1: Spawn All Runs in the Same Turn

For each test case, spawn two subagents in the same turn — one with the skill, one without.
Don't do with-skill runs first and baselines later. Launch everything at once.

**With-skill run subagent prompt:**

```
Execute this task:
- Skill path: <path-to-skill>
- Task: <eval prompt>
- Input files: <eval files if any, or "none">
- Save outputs to: <workspace>/iteration-<N>/eval-<ID>/with_skill/outputs/
- Outputs to save: <what the user cares about>
```

**Baseline run:**

- New skill: no skill at all. Save to `without_skill/outputs/`
- Improving existing skill: old version (snapshot first). Save to `old_skill/outputs/`

Write `eval_metadata.json` for each test case:

```json
{
  "eval_id": 0,
  "eval_name": "descriptive-name-here",
  "prompt": "The user's task prompt",
  "assertions": []
}
```

### Step 2: Draft Assertions While Runs Are In Progress

Don't just wait — draft assertions and explain them to the user.
Good assertions: objectively verifiable, descriptive names, readable in the benchmark viewer.
Subjective skills (writing style) are better evaluated qualitatively.

### Step 3: Capture Timing Data

When each subagent completes, save to `timing.json` in the run directory:

```json
{
  "total_tokens": 84852,
  "duration_ms": 23332,
  "total_duration_seconds": 23.3
}
```

This is the only opportunity to capture this data — do it immediately.

### Step 4: Grade, Aggregate, and Launch Viewer

1. **Grade** — spawn grader subagent reading `agents/grader.md`. Save `grading.json` with fields: `text`, `passed`, `evidence`.

2. **Aggregate**:

   ```bash
   python -m scripts.aggregate_benchmark <workspace>/iteration-N --skill-name <name>
   ```

3. **Analyst pass** — read `agents/analyzer.md` to surface patterns.

4. **Launch viewer**:

   ```bash
   nohup python <skill-creator-path>/eval-viewer/generate_review.py \
     <workspace>/iteration-N \
     --skill-name "my-skill" \
     --benchmark <workspace>/iteration-N/benchmark.json \
     > /dev/null 2>&1 &
   VIEWER_PID=$!
   ```

   > For headless/no-display: use `--static <output_path>` to write standalone HTML.

5. Tell the user: "I've opened the results in your browser. 'Outputs' tab for test cases, 'Benchmark' tab for stats. Let me know when you're done reviewing."

### Step 5: Read the Feedback

Read `feedback.json` when user is done. Empty feedback = good. Focus on cases with specific complaints.
Kill the viewer: `kill $VIEWER_PID 2>/dev/null`

---

## Improving the Skill

### How to Think About Improvements

1. **Generalize from feedback** — we're building skills for millions of uses. Don't overfit to test examples.
2. **Keep the prompt lean** — remove things that aren't pulling their weight. Read the full transcripts.
3. **Explain the why** — LLMs work better with reasoning, not rigid rules. Reframe MUST/NEVER as explanations.
4. **Bundle repeated work** — if all test cases wrote the same helper script, put it in `scripts/`.

### The Iteration Loop

1. Apply improvements to the skill
2. Rerun all test cases into `iteration-<N+1>/` (including baselines)
3. Launch reviewer with `--previous-workspace` pointing at previous iteration
4. Wait for user review
5. Read feedback, improve, repeat

Stop when:

- User says they're happy
- All feedback is empty
- No meaningful progress being made

---

## Advanced: Blind Comparison

For rigorous version comparison: read `agents/comparator.md` and `agents/analyzer.md`.
An independent agent compares outputs without knowing which is which.
Optional — human review loop is usually sufficient.

---

## Description Optimization

After creating/improving a skill, offer to optimize the description for better triggering accuracy.

### Step 1: Generate 20 trigger eval queries (mix of should-trigger and should-not-trigger)

```json
[
  { "query": "the user prompt", "should_trigger": true },
  { "query": "another prompt", "should_trigger": false }
]
```

Queries must be realistic, specific, detailed — like real user requests. Include edge cases and near-misses for the should-not-trigger set.

### Step 2: Review with User

Use the HTML template in `assets/eval_review.html`. Replace placeholders and open in browser.
User edits, toggles, then clicks "Export Eval Set" → `~/Downloads/eval_set.json`.

### Step 3: Run the Optimization Loop

```bash
python -m scripts.run_loop \
  --eval-set <path-to-trigger-eval.json> \
  --skill-path <path-to-skill> \
  --model <model-id-powering-this-session> \
  --max-iterations 5 \
  --verbose
```

Periodically tail output to give user progress updates.

### Step 4: Apply the Result

Take `best_description` from JSON output. Update SKILL.md frontmatter. Show before/after scores.

---

## Platform Adaptations

### This Project (Antigravity / Cursor IDE agents)

- Subagents: **available** — use parallel execution
- Browser: **available** — use the viewer server normally
- Python scripts: **available** via PowerShell/terminal
- `claude -p` CLI: check availability per session

### Claude.ai (no subagents)

- Run test cases one at a time, inline (no parallel)
- Skip baseline runs
- Present results in conversation if no browser
- Skip quantitative benchmarking
- Skip description optimization (requires `claude -p`)

### Cowork / Headless

- Use `--static <output_path>` for viewer (no display)
- `package_skill.py` works fine
- Description optimization works via subprocess

**CRITICAL in all environments**: GENERATE THE EVAL VIEWER _BEFORE_ evaluating inputs yourself. Get results in front of the human ASAP.

---

## Reference Files

- `agents/grader.md` — How to evaluate assertions against outputs
- `agents/comparator.md` — How to do blind A/B comparison
- `agents/analyzer.md` — How to analyze why one version beat another
- `references/schemas.md` — JSON structures for evals.json, grading.json, benchmark.json

---

## Project-Specific Notes (V12 Universal OR Strategy)

When creating skills for this project:

- All shell scripts must use `powershell` not bash (Windows environment)
- All script paths use Windows backslash format or forward-slash in PowerShell
- New skills go to `.agent/skills/<skill-name>/SKILL.md` (canonical)
- Adapter pointer files go to `.claude/skills/`, `.cursor/skills/` as needed
- Skills must follow ASCII-only rule if they generate any C# code snippets
- Check `.agent/standards_manifesto.md` for project safety standards before creating trading-related skills
