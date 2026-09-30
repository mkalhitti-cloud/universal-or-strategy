---
name: best-of-n
description: Use when the user wants to get multiple independent opinions on an architecture, design decision, or technical approach and pick the best one. Trigger phrases: "best of N", "get multiple opinions", "ask multiple agents", "architecture vote", "independent review", "best of 3", "parallel design review". Spawns N independent subagents with the same prompt, collects their verdicts, synthesises the winning answer, and documents it.
---

# Best-of-N Architecture Vote

Spawn N independent subagents with the same problem statement, collect their verdicts,
synthesise the winning design using the criteria below, and document the result.

## ⚠️ The one rule that must never be broken

**Never have one agent play multiple roles sequentially.** If Agent 1 writes role A's answer
and then writes role B's answer in the same context window, role B is anchored — the agent
is critiquing its own prior work with full memory of what it already wrote. You get 3 verdicts
on paper but 1 independent reasoning path in reality. This defeats the entire purpose.
Every judge must be a **fresh context with zero knowledge of what any other judge wrote.**

## Mode selection — do this before Step 1

| Mode | When to use | Total agents | Cost multiplier |
|---|---|---|---|
| **Standard** | Routine fix, single-block scope, design space is constrained | 3 (one A, one B, one C) + Judge D after | 1× |
| **High-stakes** | Touches 2+ blocks, new engine pattern, wrong answer = multi-block rework | 9 (three A, three B, three C) + Judge D after | 3× |

**Decision rule — escalate to high-stakes if ANY of these are true:**
- The change affects more than one block's worth of files
- Getting it wrong requires a rework of engine-level code (CopyEngine, actor patterns, threading)
- The architecture decision has been debated or has known disagreement points
- It is a new pattern not previously used in the codebase (first instance of a new approach)

**Never escalate to high-stakes for:** single-method fixes, call site updates, test additions,
UI wiring bugs, or any change where the correct answer is obvious from the constraints.

In high-stakes mode, Phase 1 spawns 9 agents in parallel: A1, A2, A3, B1, B2, B3, C1, C2, C3.
All nine get identical prompts (same role, same facts, same constraints — different agent contexts).
Synthesis then looks for where the 3 within each role agree vs diverge. Judge D still runs once after.

## Step 1 — Frame the problem

Before spawning, extract from the user's request:
- **The decision** — one precise architecture/design question (not a task to execute)
- **The constraints** — non-negotiable rules the answer must satisfy (always include JS + NT8 rules below)
- **The facts** — confirmed source evidence (method names, line numbers, confirmed behaviours)
- **Mode** — standard (3×1) or high-stakes (3×3=9)? Apply the decision rule above.

If any of these are unclear, use `ask_followup_question` to resolve them before continuing.

## Step 2 — Assign judge roles

Each judge gets the same facts and constraints but a different mandate:

| Judge | Mandate | Role in standard | Role in high-stakes |
|---|---|---|---|
| A | Minimal correct fix — fewest lines changed | 1 agent | 3 independent agents (A1, A2, A3) |
| B | Forward-looking — what breaks in 6 months? | 1 agent | 3 independent agents (B1, B2, B3) |
| C | Skeptic — what does the obvious fix get wrong? | 1 agent | 3 independent agents (C1, C2, C3) |
| D | Jane Street auditor — rules compliance check on the *chosen winner* | Always 1, always runs **after** synthesis | Same — always 1, always post-synthesis |

**Judge D is different in kind.** A/B/C are designers generating competing solutions.
Judge D is a compliance auditor reading a *finished design* against a deterministic checklist.
There is no creative variance to capture by running multiple Judge Ds.
Judge D always runs after Step 4, never in parallel with A/B/C.

## Step 3 — Spawn judges in parallel

Use `spawn_subagent` for each judge simultaneously (one `function_calls` block, all spawns together).

Each prompt MUST include:
1. Judge role label (A / B / C / D …)
2. All confirmed codebase facts (method signatures, line numbers, confirmed behaviour — no speculation)
3. The full Jane Street constraint set (JS-001 to JS-041) — paste the table at the end of this step
4. The full NT8 compiler constraint set (NT8-001 to NT8-043) — paste the index table at the end of this step
5. The specific decision to design
6. The required output structure below

Required output structure to include in each prompt:
```
Return exactly:
---
JUDGE: [letter]
DECISION: [1-line summary of chosen approach]
IMPLEMENTATION: [exact code, annotated]
CYC: [number]
CALL SITES: [list file:line old→new]
TESTS: [test method names + 1-line each]
JS_VIOLATIONS: [list of any JS-XXX rules this design touches, with rationale] or NONE
NT8_VIOLATIONS: [list of any NT8-NNN rules this design touches, with rationale] or NONE
RATIONALE: [1-2 sentences]
CONCERNS: [list or NONE]
---
```

## Step 3.1 — Jane Street rules to embed in every judge prompt

Paste this table into every judge's prompt under a heading **MANDATORY CONSTRAINTS — JANE STREET RULES (V12 DNA)**:

```
P0 CRITICAL (instant work-stopper):
  JS-001: No throw new XxxException in hot paths — use Result<T,E>
  JS-002: No return null — use Option<T> or nullable reference types
  JS-003: Model discriminated unions with sealed record hierarchies (NOT in NT8 — see NT8-002)
  JS-010: Use private constructors + static factory methods (smart constructors)
  JS-015: Parse at boundaries, use validated types internally
  JS-021: lock() IS BANNED — use Actor/Channel/Interlocked
  JS-022: Use Channel-based Actor pattern for stateful concurrency
  JS-033: async void IS BANNED except event handlers — use ValueTask or Task
  JS-036: Use Span<T> for zero-allocation hot paths (stackalloc, not new byte[])
  JS-037: Use ArrayPool for reusable buffers — never new T[] in hot paths

P1 HIGH (warning, must justify):
  JS-004: Use switch expressions for exhaustive matching (not default-heavy switches)
  JS-005: Enable nullable reference types (#nullable enable)
  JS-006: Use phantom types for unit safety (currency, distance, time)
  JS-007: Use newtype (readonly struct) for semantic types — prevents mixing int orderId/userId
  JS-008: Use readonly structs for small immutable data (<16 bytes)
  JS-016: Use type-level state machines (phantom types for FSM transitions)
  JS-017: Use generic constraints for dependent types
  JS-023: Use Interlocked for simple atomic state — not lock for single-field updates
  JS-024: Use Task.WhenAll for structured concurrency — no fire-and-forget
  JS-025: Use ConcurrentQueue/ConcurrentDictionary not lock-protected List/Dict
  JS-026: Use bounded channels (backpressure) not CreateUnbounded
  JS-027: Always pass CancellationToken with timeout to async operations
  JS-032: Use ValueTask<T> not Task<T> in hot paths (avoids allocation)
  JS-038: Use ref readonly for zero-copy struct iteration
  JS-039: Use Memory<T> not Span<T> in async methods
  JS-040: readonly struct for small value types — prevents defensive copies
  JS-041: Use [StructLayout(LayoutKind.Sequential)] for cache-friendly data

P2 MEDIUM (informational):
  JS-009: ImmutableDictionary for persistent collections (NOT in NT8 — see NT8-004)
  JS-011: Use with expressions for functional updates of immutable data
  JS-012: Use Bind for monadic Result/Option composition (not nested if)
  JS-013: Use extension methods for pipeline-style APIs
  JS-014: Use LINQ query syntax for complex monadic pipelines (3+ Bind chain)
  JS-018: Implement IEquatable<T> for readonly structs (avoid boxing)
  JS-019: Override ToString for debugging on custom types
  JS-020: Use records for DTOs (NOT positional records in NT8 — see NT8-002)
  JS-028: Retry with exponential backoff — no fixed-interval spin-retry
  JS-029: Circuit breaker for external service calls
  JS-030: AsyncLocal<T> for context propagation, not ThreadLocal
  JS-031: ConfigureAwait(false) in library code (avoid sync context capture)
  JS-034: IAsyncEnumerable<T> for lazy async streams
  JS-035: SemaphoreSlim not Semaphore for async coordination
```

## Step 3.2 — NT8 compiler rules to embed in every judge prompt

Paste this table into every judge's prompt under a heading **MANDATORY CONSTRAINTS — NT8 COMPILER RULES (V1.5)**:

```
P0 (instant build break / crash — hard ban):
  NT8-001: { get; init; } BANNED — use { get; private set; } + constructor
  NT8-002: abstract record / sealed record BANNED — use abstract class + sealed class
  NT8-003: volatile double BANNED (CS0677) — use plain double (x64 atomic) or Interlocked int64 bits
  NT8-004: System.Collections.Immutable NOT AVAILABLE — use Dictionary written once
  NT8-005: readonly struct + { get; private set; } BANNED (CS8341) — use readonly field
  NT8-007: CreateOrder arg 12 is (CustomOrder)null not string
  NT8-008: chart.ChartControl does not exist — use FindVisualChild<ChartControl>
  NT8-009: ChartControl.GetValueByY() absent — stub to 0.0
  NT8-010: State.SetDefaults in Indicator must be fully namespace-qualified
  NT8-011: Add(ATR(Period)) in headless Indicator OnStateChange is invalid
  NT8-013: DateTime.Now for CreateOrder expiry WRONG — use DateTime.MaxValue
  NT8-015: AtrSizingEngine : Indicator must NOT be sealed
  NT8-016: TradeCopierWindow : Window must NOT be sealed
  NT8-019: async void BANNED in NT8 callbacks — callbacks must be synchronous void
  NT8-030: OnWindowCreated fires for EVERY NT8 window — guard with volatile bool
  NT8-031: OrderState.PendingSubmit does not exist — use OrderState.Initialized
  NT8-042: Dispatcher.InvokeAsync NOT available from AddOn context (all 3 paths CS0117/CS1061)
  NT8-043: Null-conditional compound assignment (acc?.Event -= h) BANNED — C# 7.3, use if (acc != null)

P1 (silent wrong behaviour / risk):
  NT8-006: ConcurrentBag.Any() requires explicit `using System.Linq`
  NT8-012: FrameworkElementFactory cannot add ColumnDefinitions inline — use Loaded event
  NT8-014: CreateOrder signal name MUST start with "PTT-"
  NT8-017: Cross-thread bool/int fields MUST be volatile
  NT8-018: lock() BANNED in NT8 — use volatile + ConcurrentDictionary/ConcurrentBag
  NT8-020: SolidColorBrush must be .Freeze()d before cross-thread use
  NT8-021: Account.All BANNED in constructors/field initializers — only in event handlers
  NT8-022: WPF KeyBinding with letter keys silently ignored by NT8
  NT8-023: NTWindow as UserControl base — use UserControl instead
  NT8-024: NTWindow as standalone Window base causes window-not-appearing
  NT8-025: NTMenuItem.Header as string returns null — use .ToString()
  NT8-026: Trailing stop (order.TrailPrice > 0) must be detected before acc.Change()
  NT8-027: Instrument.MarketData from AddOn context — always null-guard md, md.Last, md.Last.Price
  NT8-028: Hex color string literals BANNED — use MakeBrush(r,g,b)
  NT8-029: Tick alignment mandatory on all stop/limit prices
  NT8-032: MarketData.Ask/.Bid/.Last are MarketDataEventArgs objects — always use .Price; full null-guard required

P2 (style/risk):
  NT8-041: ChartControl.Charts not accessible via reflection — use FindVisualChild<Chart>
```

## Step 4 — Wait for all judges, then synthesise

### Standard mode (3 verdicts — A, B, C)

Use `sequentialthinking` to:
1. Find the **unanimous points** — accept without debate
2. Find the **disagreements** — reason through each using the constraints
3. Identify any **concern raised by only one judge** — evaluate whether it is valid; if valid, adopt it even if only one judge raised it (minority concerns are often the most important)
4. Reject any approach that violates a P0 constraint (JS or NT8) — P0 violations are never acceptable even if all judges agree
5. Produce the **winning design**: the minimal correct implementation that incorporates all valid concerns

### High-stakes mode (9 verdicts — A1/A2/A3, B1/B2/B3, C1/C2/C3)

Use `sequentialthinking` with this additional aggregation step first:

**Pre-synthesis: collapse 9 → 3**
- Group by role: collect all 3 A verdicts, all 3 B verdicts, all 3 C verdicts
- Within each role group: find the majority position (2-of-3 agreement = the role's canonical answer)
- Note any within-role divergence — a 1-of-3 outlier within a role is **high-signal**:
  it means even judges with the same mandate could not agree, which flags a genuinely
  ambiguous design point that the synthesis must resolve explicitly
- After collapsing, treat the 3 canonical role answers as if they were standard A/B/C
  and apply the standard synthesis steps above

**Why within-role divergence matters:** In standard mode you get one skeptic. In high-stakes
mode you get three independent skeptics — if all three find the same flaw, it is a confirmed
defect. If only one of three finds it, evaluate it as a minority concern (apply step 3 above).
If two of three find different flaws, both concerns are valid and both must be addressed.

Record the synthesis reasoning — it becomes the architecture decision rationale.
Document the mode used: `Architecture: best-of-N vote (standard 3×1)` or
`Architecture: best-of-N vote (high-stakes 3×3 = 9 independent agents)`.

## Step 4.5 — OKF Jane Street audit checkpoint (mandatory before locking design)

Before proceeding to Step 5, the orchestrator MUST confirm:

- [ ] Zero P0 JS violations (JS-001, JS-002, JS-010, JS-015, JS-021, JS-022, JS-033, JS-036, JS-037)
- [ ] Zero P0 NT8 violations (NT8-001 to NT8-005, NT8-007 to NT8-011, NT8-013, NT8-015, NT8-016, NT8-019, NT8-030, NT8-031, NT8-042, NT8-043)
- [ ] CYC of each new or modified method is <= 8 (Jane Street strict standard)
- [ ] No `lock()` anywhere in new or modified code
- [ ] No `async void` except event handler signature
- [ ] No `return null` in non-nullable context
- [ ] All new stop/limit prices are tick-aligned (NT8-029)
- [ ] All new brushes are .Freeze()d (NT8-020)
- [ ] All new cross-thread fields are `volatile` (NT8-017)
- [ ] Signal names start with "PTT-" (NT8-014)

If any checkpoint fails: go back to the judge with the violation, require a revised IMPLEMENTATION, and re-synthesise.

## Step 5 — Document

Write the winning design to the appropriate spec (HTML spec, markdown doc, or inline comment) with:
- `Architecture: best-of-N vote (standard 3×1)` or `Architecture: best-of-N vote (high-stakes 3×3 = 9 independent agents)` in the decision block
- The synthesised implementation
- Any concerns that were overruled and why
- The OKF audit result (PASS / BLOCKED + which rule)
- In high-stakes mode: any within-role divergence points and how they were resolved

## Step 6 — Produce lane prompts

If the decision requires implementation, produce ready-to-paste lane prompts for the PTT orchestrator workflow. Each prompt must include:
- Defect ID
- Write-set (exactly which files)
- The locked architecture (from Step 4)
- The [Fact] contract (expected test count before/after)
- CYC assertion per new method
- F5 gate reminder
- NT8 compiler rules gate reminder (agents must read NT8_COMPILER_RULES.md before touching any .cs file)

## Rules

- **Never have one agent play multiple roles** — always fresh contexts, always parallel, zero cross-contamination
- Never spawn judges sequentially — always in parallel (one `function_calls` block)
- Never accept a design that violates a P0 JS or NT8 constraint, even if all judges propose it
- Always adopt a minority concern if it is valid — consensus is not the goal, correctness is
- The final synthesis is the orchestrator's decision, not a vote count
- Document every overruled concern and the reason it was overruled
- SRC CODE BAN: this skill produces specs and prompts only — never edits `.cs` files directly
- Always embed the full JS and NT8 rule tables in judge prompts (Steps 3.1 and 3.2) — judges must have the complete constraint set to produce valid verdicts
- Judge D always runs **after** synthesis, never in parallel with A/B/C, always exactly once regardless of mode
- In high-stakes mode: spawn all 9 agents in a **single** `function_calls` block — do not run the 3 batches sequentially
