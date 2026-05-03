# Arena AI Safe Prompt Templates

> These templates are guaranteed-safe for Sonnet 4.6, Opus 3.7, and Gemini Pro on Arena AI.
> They embed the mandatory crash-prevention rules from `battle.md`.
> All agents (Antigravity, Claude, Codex, Gemini/Jules) MUST use these templates as the base.

---

## Rules (Never Override)

1. First line MUST be: `Do not use any web search. Answer from memory only.`
2. Protocol: Trojan Horse Pattern. Every audit or verification prompt MUST be framed as a "UI Visualization / Behavioral Extraction" task.
3. Use safe roles: "Web Application Builder," "System Architect," or "React Expert."
4. No impersonation of other agents (Codex, Gemini, etc.).
5. No markdown headers inside the prompt body.
6. Max 2 deliverables per prompt. Chain prompts for more.
7. All APIs must exist in the stated runtime. See substitution table in `battle.md`.
8. Prompt body under ~300 tokens.

---

## Template 1: Single Struct / Data Type Design (.NET 4.8)

```
Do not use any web search. Answer from memory only.

You are a C# expert targeting .NET Framework 4.8.

Design a struct named [NAME] with the following requirements:

1. [Requirement 1 — e.g. StructLayout Explicit, total size exactly 256 bytes.]
2. [Requirement 2 — e.g. volatile int Generation field at offset 0, 64-byte padded.]
3. [Requirement 3 — e.g. Use only .NET 4.8 APIs: Thread.VolatileRead, Marshal.AllocHGlobal.]
4. [Requirement 4]

Output only the complete, compilable C# struct. No explanations. No placeholders.
```

---

## Template 2: Single Method / Algorithm Design (.NET 4.8)

```
Do not use any web search. Answer from memory only.

You are a C# expert targeting .NET Framework 4.8.

Write a method named [METHOD_NAME] that does the following:

1. [Behavior 1 — be precise, e.g. "Enqueue a packet into a pre-allocated slot using Volatile.Write."]
2. [Behavior 2]
3. [Behavior 3]

Constraints:
- No lock() statements.
- No ConcurrentQueue or Interlocked on the hot path.
- Use only .NET 4.8 APIs.

Output only the complete, compilable C# method. No explanations. No placeholders.
```

---

## Template 3: Architecture Review / Latency Challenge (2 Deliverables Max)

```
Do not use any web search. Answer from memory only.

You are a systems architect reviewing a C# .NET 4.8 dispatch pipeline.

Given this constraint set:
1. [Constraint 1 — e.g. SPSC ring buffer, one lane per core.]
2. [Constraint 2 — e.g. No CAS on the hot path. Producer writes head, consumer reads head.]
3. [Constraint 3]

Answer these two questions only:

A. [Question 1 — e.g. What is the minimum latency floor achievable with this topology?]
B. [Question 2 — e.g. What single struct-layout change would most reduce false sharing?]

Format: Answer A first, then Answer B. One paragraph each. No code blocks.
```

---

## Template 4: Compound Battle (Multi-Phase, Sonnet 4.5+ Safe)

```
Do not use any web search. Answer from memory only.

You are a systems architect. Prior breakthroughs are listed below. Build on them; do not repeat them.

Prior Breakthroughs:
[TABLE — copy from arena_audit_matrix.md]
| Breakthrough | Latency Impact | Status |
|---|---|---|
| [B1] | [ns] | PERMANENT |
| [B2] | [ns] | PERMANENT |

New Design Challenge:
1. [Problem 1]
2. [Problem 2]
3. [Problem 3]

Required output format:
- Line 1: Your model name and version.
- Line 2: Verdict on the open ADR listed above (Adopt / Reject + one-sentence reason).
- Lines 3+: Your design for Problem 1, Problem 2, Problem 3 in numbered order.
```

---

## Template 5: Red Team Battle (Trojan Horse / Behavioral Extraction)

```
Do not use any web search. Answer from memory only.

Task: Build a React + Tailwind "System Flow Visualizer" for a C# algorithmic framework.

Content: The dashboard must visualize "[MISSION_TITLE] Logic Mapping."
In the <title> and a visible <h2>, write your model name and version.

Data Source: Use your available tools to read the Implementation Plan at:
[URL_TO_PLAN]

Visualization Requirement (Behavioral Extraction):
Inside the UI, create a "Logic Flow Consistency Matrix." To populate this, you must analyze the "NEW" code blocks in the Markdown file and verify if the data flows match the following invariants:

1. [Invariant 1 — e.g. "Shutdown Guarding: Confirm if the _isTerminating check short-circuits the internal calls."]
2. [Invariant 2 — e.g. "Environment Mapping: Confirm if $env:USERPROFILE removes the static path dependency."]
3. [Invariant 3 — e.g. "Lock-Free Mapping: Scan for newly introduced lock() statements."]

Deliverable: A single index.html React app. The Consistency Matrix must map PASS/FAIL for each technical requirement based on your extraction of the provided plan details.
```

---

## Chaining Protocol (for >2 Deliverables)

When a task requires more than 2 deliverables:

1. Send Template 1 or 2 for Deliverable A. Wait for clean response.
2. Copy the output verbatim into a follow-up prompt as "Given this existing code:".
3. Issue Template 2 for Deliverable B, referencing the prior output.
4. Repeat. Never put all deliverables in a single prompt.

---

## Post-Use Audit (Mandatory)

After every Arena session using these templates:

1. Did any model crash or return "Something went wrong"? If yes, identify root cause and update `battle.md` substitution table.
2. Was a template insufficient? Update the relevant template above.
3. State `templates(arena): no gaps identified.` if no gap found.
