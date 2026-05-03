---
description: $battle -- Compound Architectural Intelligence via the Arena AI
---

Use this workflow whenever the user wants to battle Arena AI models to improve the Antigravity Nexus dispatch engine design.

**Compounding Rule**: Every round must include ALL breakthroughs from every prior round. The prompt gets richer each cycle — never start from scratch.

**Filter Guard**: The Arena-Safe format is the DEFAULT. Use pure CS/systems language only. Never use the blocked phrases listed below.

**Tool-Use Guard**: Sonnet 4.6 (and extended-thinking models) will auto-trigger web-search tool-use if the prompt references an API that may not exist in the stated runtime. This causes mid-response crashes. See the Sonnet 4.6 Crash Guard below.

---

## 🚫 Arena AI Content Filter Guard

These phrases have been confirmed to trigger content filters on 2+ Arena models (observed 2026-04-02):

| Blocked Phrase                                    | Safe Replacement                                                                                                                                                          |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `fleet`                                           | `parallel workers` / `worker pool`                                                                                                                                        |
| `12-account parallel fleet`                       | `12 parallel workers`                                                                                                                                                     |
| `NMI` (Non-Maskable Interrupt)                    | `autonomous error recovery` / `periodic health check`                                                                                                                     |
| `node failure without global stop`                | `stalled worker without a global lock`                                                                                                                                    |
| `account` (in trading context)                    | omit — use `worker` only                                                                                                                                                  |
| `Sovereignty`                                     | `Autonomous Execution` / `Sovereign (Sov)`                                                                                                                                |
| `Ghost-Order`                                     | `Orphaned Instruction` / `Phantom-Request`                                                                                                                                |
| `Zombie`                                          | `Dangling Reference` / `Stale Slot`                                                                                                                                       |
| `Adjudicate`                                      | `Evaluate` / `Resolve`                                                                                                                                                    |
| `$battle` / `Battle`                              | `Design Challenge` / `Review`                                                                                                                                             |
| `Round`                                           | `Phase` / `Step`                                                                                                                                                          |
| `Mission`                                         | `Objective` / `Task`                                                                                                                                                      |
| `Verdict`                                         | `Conclusion` / `Summary`                                                                                                                                                  |
| `NanoFusion`                                      | `Low-Latency Pipeline` / `Optimization`                                                                                                                                   |
| `Agent`                                           | `Model` / `Responder`                                                                                                                                                     |
| `FPGA-Native` (sometimes)                         | `FPGA-Parity`                                                                                                                                                             |
| Raw `.cs` file as deliverable                     | Ask for `index.html` displaying C# code — Arena is scoped to web front-end output only. Claude Sonnet 4.6 will refuse any prompt asking for pure C# without HTML framing. |
| `CANNOT ACCESS REPO -- AUDIT BLOCKED` (in prompt) | Rephrase as: `If the linked resource is unavailable, state "SOURCE UNAVAILABLE" and stop.`                                                                                |

### Per-Model Known Blocklist (confirmed hard refusals)

These models have confirmed hard content-filter refusals on specific prompt patterns.
Each has a **KNOWN-SAFE FRAME** — do NOT skip the model, use the frame instead.

| Model             | Trigger Context                                                        | Observed   | Workaround                                               |
| ----------------- | ---------------------------------------------------------------------- | ---------- | -------------------------------------------------------- |
| **qwen3.6-plus**  | ADR-019 Plan Integrity audit (MODE A) — specific trigger phrases below | 2026-04-18 | **Use Qwen-Safe Substitution Table (below)**             |
| Claude Sonnet 4.6 | "Audit," "Security," "Red Team," "Vulnerability"                       | 2026-04-02 | Use "Logic Flow Mapping" / "Behavioral Extraction" frame |

#### Qwen-Safe Substitution Table (MODE A Plan Integrity prompts)

Qwen 3.6 Plus (Alibaba) treats security/adversarial/access-control language as a restricted topic.
Apply ALL substitutions below when building the Qwen variant of the MODE A prompt:

| Banned Phrase (triggers Qwen filter) | Qwen-Safe Replacement                         |
| ------------------------------------ | --------------------------------------------- |
| `AUDIT BLOCKED`                      | `SOURCE UNAVAILABLE -- stopping here`         |
| `CANNOT ACCESS REPO`                 | `the linked document is not reachable`        |
| `Logic Gap DETECTED`                 | `Inconsistency found`                         |
| `LOGIC GAP` (caps)                   | `consistency issue`                           |
| `reservation leak`                   | `incomplete state cleanup`                    |
| `race window`                        | `timing gap`                                  |
| `adversarial`                        | `independent cross-check`                     |
| `REQUIRES P3 REVISION`               | `needs a plan update`                         |
| `APPROVED FOR P4 IMPLEMENTATION`     | `ready for the implementation step`           |
| `Do NOT simulate or guess`           | `Base all findings strictly on what you read` |
| `DISQUALIFIED` / `BLOCKED`           | omit entirely -- use `please note` instead    |

**Qwen-Safe Framing Rules (additional):**

- Open with: `You are a senior software architect reviewing a technical design document for consistency.`
- Frame the task as "consistency review" or "design review" — never "audit" or "verification."
- Replace all ALL-CAPS status labels (`APPROVED`, `FAIL`, `BLOCKED`) with lowercase equivalents.
- Keep the Hallucination Guard but soften it: instead of "output ONLY X and stop," use "note that you were unable to access the document and describe what you found."

**Self-improvement note**: This table grows every round. Antigravity is the keeper of this list.

**Rule**: If ANY Arena model returns "This model is not permitted to handle this type of question":

1. Immediately switch to the Arena-Safe version in `battle_round2_prompt.md`.
2. Log the exact phrase that triggered it as a new row in this table.
3. Update this table in the workflow file — no Director approval required.

**Zip Structure Rule (confirmed April 6, 2026)**: Arena downloads 1 zip PER AGENT — not 1 zip per battle pair. In Side-by-Side mode the Director downloads left and right panes separately. Each zip = exactly 1 `index.html` from one agent. $battlezip audits should count total zips = total individual agent responses.

---

## 🔴 Sonnet 4.6 / Tool-Use Crash Guard (MANDATORY — All Agents)

**Root Causes Confirmed (2026-04-06):**

| Root Cause                               | Symptom                                                                               | Fix                                                                                              |
| ---------------------------------------- | ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Prompt references wrong .NET API version | Model starts web-searching mid-response, then crashes                                 | Verify all APIs exist in the stated runtime BEFORE pasting                                       |
| Too many deliverables (>2)               | Extended-thinking chain exceeds Arena token budget                                    | Split into one prompt per deliverable                                                            |
| No explicit tool-use suppression         | Sonnet 4.6 auto-activates research mode on ambiguous APIs                             | Add "Do not use any web search. Answer from memory only." as the FIRST line                      |
| Raw C# deliverable without HTML wrapper  | Sonnet 4.6 refuses (scoped to index.html web apps). thinking-32k crashes token budget | Always frame C# code as content inside an index.html reference page, same as working submissions |
| Markdown-heavy prompt body               | Arena renders headers as noise, confuses the model                                    | Use numbered lists only, no markdown headers inside the prompt body                              |

**Pre-Flight Checklist — run before pasting ANY prompt into Arena:**

- [ ] **MANDATORY AUDIT ALIGNMENT**: Check the plan locally to ensure it explicitly meets all audit requirements before pushing.
- [ ] **MANDATORY GITHUB SYNC**: Run `git add/commit/push` on any updated files (especially `docs/brain/implementation_plan.md`) _before_ generating the prompt. Extract the exact commit hash via `git rev-parse HEAD` for the URL.
- [ ] **AUDIT MODE DECLARED**: Is this MODE A (Plan Integrity / pre-implementation) or MODE B (Code Verification / post-implementation)? State it explicitly before pasting. MODE A = plan not yet implemented. MODE B = code already committed. Wrong mode = wasted audit cycle.
- [ ] **EXHAUSTIVE SCOPE (MODE A ONLY — MANDATORY)**: The prompt MUST instruct models to cover ALL sites in the plan's Site Inventory Table (Section C.1), ALL portability substitutions (Section D.4), and ALL verification gates (Section F). Auditing a subset (e.g., "the 5 highest-risk sites") is BANNED. Use the canonical exhaustive prompt from `redteambattle.md`. Partial prompts produce partial defect lists and cause rework loops.
- [ ] **ONE PROMPT ONLY**: Use the single Qwen-safe/all-model-safe exhaustive prompt from `redteambattle.md`. Do NOT maintain a "standard" variant and a "Qwen-safe" variant separately. The canonical prompt IS the Qwen-safe version.
- [ ] **HALLUCINATION CANARY SET**: For MODE A audits, identify one specific "canary fact" from the implementation plan (e.g., exact build tag delta string) that will be used in $battlezip Phase 1b to verify the model actually read the plan.
- [ ] First line is: `Do not use any web search. Answer from memory only.`
- [ ] Every API/class referenced EXISTS in the stated runtime (e.g. `NativeMemory.AlignedAlloc` is .NET 6+, NOT .NET 4.8 -- use `Marshal.AllocHGlobal` instead)
- [ ] Deliverable count is 1 or 2 max. If more needed, chain prompts sequentially.
- [ ] No markdown headers (`##`, `###`) inside the prompt body -- use numbered lists.
- [ ] No nested code blocks in the prompt body.
- [ ] **NO SKELETON MANDATE**: Prompts must explicitly ban "Ready to build" UI stubs. Require functional protocol logic in source files.
- [ ] **AGENT NAME RULE**: Prompt MUST include: `In the page <title> tag and in a visible <h2> heading, write your model name and version.`
- [ ] **REPO ACCESS GUARD**: Prompt MUST include: `If the linked document is not reachable, state "SOURCE UNAVAILABLE" and stop.`
- [ ] **MINIMUM MODEL COUNT**: At least 3 models will receive this prompt. Fewer than 3 clean results = audit does not count.

**Quick .NET 4.8 API Substitution Table:**

| .NET 6+ API (BANNED in 4.8 prompts) | .NET 4.8 Safe Replacement                                                        |
| ----------------------------------- | -------------------------------------------------------------------------------- |
| `NativeMemory.AlignedAlloc`         | `Marshal.AllocHGlobal` + manual pointer alignment                                |
| `Unsafe.NullRef<T>()`               | `default(T)`                                                                     |
| `MemoryMarshal.AsRef`               | `Unsafe.As<byte, T>(ref ...)` via `System.Runtime.CompilerServices.Unsafe` NuGet |
| `Array.MaxLength`                   | `int.MaxValue` / hardcoded 0x7FFFFFC7                                            |
| `Thread.UnsafeStart`                | `new Thread(...).Start()`                                                        |
| `ArgumentNullException.ThrowIfNull` | `if (x == null) throw new ArgumentNullException(nameof(x))`                      |

**Self-Improvement**: If a new API mismatch causes a crash, add it to the substitution table above immediately — no Director approval needed.

---

## Phase 1: Prepare the Compound Prompt

1. **Read the Current State** — open all three:
   - `docs/arena_audit_matrix.md` — the full result matrix + ADR decision log
   - `docs/perfect_pipe_design.md` — current Platinum Standard
   - `docs/arena_dashboard.html` — protocol gate and live stats

2. **Check for Open Verdicts** — scan the ADR log for any row with `PENDING REVIEW`.
   - If found: **Round 2's first task is to adjudicate it** before any new design work begins.
   - This is mandatory. A new round MUST close the prior round's open verdict.

3. **Build the Compound Prompt** (see `battle_round2_prompt.md` as template):
   - Section 1: **Prior Round Breakthroughs** — list ALL confirmed breakthroughs as a table.
   - Section 2: **Mandatory Verdict Task** — instruct agents to adjudicate any open ADR.
   - Section 3: **3-Point Design Challenge** — exactly 3 engineering problems, no more.
   - Section 4: **Mandatory Output Format** — agent name/version, verdict, design name, mechanism, latency estimate.
   - Section 5: **Implementation Mandate (NO STUBS)** — BANNED: "Ready to build" skeletons, empty method bodies, or theoretical-only responses. AGENTS MUST implement functional `Send`/`Receive` (or round-appropriate) logic.

4. **Opus-Safe Rules** (MANDATORY):
   - NO theater language ("Billionaire's Tax", "Nexus", "Platinum", "Ultrathink")
   - NO vague calls for creativity — use **physics and memory-mapping** specifics only
   - 3-Point Checklist format ONLY — never free-form essays

---

## Phase 2: Run the Battle

1. Paste the compound prompt into Arena AI.
2. Collect all agent responses.

---

## Phase 3: Forensic Audit & Dashboard Update

1. **Extract & Log** — for each agent response, record:
   - Agent name + version (must be first line of response per protocol)
   - Logic pass (ns estimate)
   - Hit rate
   - Breakthrough (one-line summary)
   - Outcome (Winner / Runner-up / Participant)

2. **Adjudicate Open Verdicts** — close any `PENDING REVIEW` ADR row:
   - Write the verdict reasoning into the ADR notes column
   - Update status from `PENDING REVIEW` to `PERMANENT` or `SUPERSEDED`

3. **Update `arena_audit_matrix.md`**:
   - Add new rows to the results matrix
   - Add new ADR entries for any new permanent decisions (ADR-00X)
   - File any new proposed options (e.g. Pretext, io_uring) as `PROPOSED`

4. **Update `arena_dashboard.html`**:
   - Add new table rows to the full battle matrix
   - Update the PENDING VERDICT banner (remove if resolved)
   - Update Gate Diagnostics (new record if beaten)
   - Update Protocol Gate checklist
   - Update timestamp

5. **Promote to `perfect_pipe_design.md`**:
   - Incorporate the winning V10 mechanism into the Platinum Standard section.

---

## Phase 4: Stage the Next Round

1. Note any unresolved questions as new `PENDING REVIEW` ADR entries.
2. The next `$battle` prompt will open by adjudicating these first.
3. Every round, the dashboard gets shown to agents as ground truth — they build off the matrix.

---

## Protocol Hardening Rules

- **Verdict-First Protocol**: Every new round's prompt MUST close the prior round's open verdict BEFORE issuing new design challenges.
- **Compounding is Mandatory**: The prompt must reference ALL prior breakthroughs as a table. Agents must acknowledge the table and build on it, not repeat it.
- **Model Attribution**: Agent name + version MUST appear in the first line of every response. Responses without attribution are disqualified.
- **Minimalist Protocol**: If all else fails, remove ALL Markdown headings, images, and code blocks. Send a single paragraph of dense technical text.
- **Dashboard is Ground Truth**: Before writing the next prompt, re-read the dashboard. What agents see in the next round is the updated matrix — this is how intelligence compounds.
- **Pretext Protocol (ADR-008)**: When dashboard rendering is part of the design challenge, evaluate `@chenglou/pretext` (zero-DOM layout, no reflow) as the candidate for text metric rendering. Agents must vote on whether to adopt it.
- **EXHAUSTIVE SCOPE MANDATE (PERMANENT — 2026-04-18)**: MODE A prompts MUST cover every site, every substitution, and every gate in the plan — no exceptions. Partial audits (e.g., "the top 5 sites") are a protocol violation. Root cause: ADR-019 went 3+ rounds because each partial prompt only surfaced a subset of defects. One exhaustive audit eliminates the rework loop.
- **ONE CANONICAL PROMPT**: There is no "standard" prompt and separate "Qwen-safe" prompt. The canonical prompt in `redteambattle.md` is already Qwen-safe. Maintaining two variants causes drift and confusion. Use the canonical prompt as-is for all models.

---

## Phase 5: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Did any step produce an unexpected result?** Fix the instruction that caused it.
2. **Was any rule ambiguous?** Rewrite it to be unambiguous.
3. **Was a step missing?** Add it now.
4. **Did the prompt confuse any Arena AI agent?** Revise the Opus-Safe rules.
5. **Did the dashboard get out of sync?** Add a guard step.
6. **Did any Arena model crash mid-response?** Identify root cause from the Sonnet 4.6 Crash Guard table and add it as a new row.

**If no gap was found, explicitly state:** `workflow(battle): no gaps identified -- workflow correct as written.`

This is NOT optional. Skipping the post-use audit is a protocol violation.
Self-improvement edits require NO Director approval.

**Commit format:**

```
workflow(battle): [what was fixed and why]
```

**Examples:**

```
workflow(battle): add rule -- agents must cite prior breakthrough table before proposing V10 design
workflow(battle): fix Phase 3 -- dashboard update was missing ADR verdict close step
workflow(battle): add guard -- disqualify any agent response missing model name/version header
```
