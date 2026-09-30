# PTT Workspace Protocol
**Version:** 1.0  
**Effective:** 2026-07-06  
**Applies to:** All Prop Trader Tools (PTT) NinjaTrader build work

---

## Workspace Routing -- Zero Pollution Rule

Two workspaces exist. They serve different purposes. They NEVER cross-pollinate.

| Workspace | Path | What lives here |
|---|---|---|
| **Director** | `c:\WSGTA\universal-or-strategy-director` | Specs, docs, brain artifacts, architecture plans, tickets, verification reports, protocol docs |
| **Wave** | `c:\WSGTA\universal-or-strategy` | C# source files (`src/`), `.csproj`, tests, build output |

### The hard rule

> If it is a `.cs`, `.csproj`, or test file -- it lives in **Wave**.  
> Everything else -- it lives in **Director**.

The engineer writes `src/PropTraderTools/` in Wave.  
The verifier reads `src/PropTraderTools/` from Wave (read-only) and writes its report to Director.  
The orchestrator, architect, reviewer, and final reviewer never touch Wave's `src/`.

**Why:** V12 wave epics live in Wave's `src/`. PTT source lives in `src/PropTraderTools/`. Keeping them in the same workspace but different subdirectories is intentional -- they share the same NT8 assembly references and build system. But brain artifacts (plans, tickets, reviews) belong in Director so they don't pollute the wave epic structure.

---

## The 7 Mandatory Scans

These scans run in Wave workspace on `src/PropTraderTools/`. They transfer from the V12 wave discipline -- the same rules apply to NT8 Add-On code.

| ID | What | Command | Must return |
|---|---|---|---|
| SCAN-01 | No `lock()` | `grep -r "lock(" src/PropTraderTools/` | 0 results |
| SCAN-02 | ASCII-only | `Get-Content src/PropTraderTools/*.cs \| Where-Object {$_ -match '[^\x00-\x7F]'}` | 0 results |
| SCAN-03 | No FontFamily override | `Select-String -Path src/PropTraderTools/*.cs -Pattern "FontFamily"` | 0 results |
| SCAN-04 | No hardcoded hex colors | `Select-String -Path src/PropTraderTools/*.cs -Pattern "#[0-9A-Fa-f]{6}"` | 0 results |
| SCAN-05 | PTT- prefix on all orders | All `CreateOrder` name params start with `"PTT-"` | 0 violations |
| SCAN-06 | No `DateTime.Now` | `Select-String -Path src/PropTraderTools/*.cs -Pattern "DateTime\.Now[^U]"` | 0 results |
| SCAN-07 | No `lock()` (belt-and-suspenders) | `Select-String -Path src/PropTraderTools/*.cs -Pattern "\block\s*\("` | 0 results |

SCAN-01 and SCAN-07 are intentionally redundant -- `lock()` is a P0 production incident for NT8 Add-Ons because it blocks the NT UI thread.

### Why each scan exists

**SCAN-01/07 -- `lock()`**  
NT8 Add-Ons share threads with the NT UI and strategy engine. A `lock()` in `OnOrderUpdate` can freeze the entire NT process. Replaced by `Interlocked`, `ConcurrentDictionary`, and `volatile`. This is the same rule as V12 (JS-021).

**SCAN-02 -- ASCII-only**  
NT8 serializes C# source to NinjaScript XML internally. Non-ASCII characters corrupt the export/import pipeline. Same rule as V12.

**SCAN-03 -- No FontFamily override**  
NT8 has its own WPF theme. Overriding `FontFamily` breaks the 100% NT-native appearance pillar -- the user sees a different font from every other NT control. The rule: inherit, never override.

**SCAN-04 -- No hardcoded hex**  
NT's dark/light theme switches change background and foreground colors. Hardcoded `#RRGGBB` values break in the opposite theme. Use `NTBrushes.*` resource keys instead -- they update automatically with the theme.

**SCAN-05 -- PTT- prefix**  
`IsBracketLeg()` uses the `PTT-` prefix as Layer 2 protection. If a `CancelPendingEntries` call fires while a `PTT-Flatten` order is still `Working`, the prefix prevents self-cancellation. Every `CreateOrder` call must name its order with `"PTT-[verb]"`.

**SCAN-06 -- `DateTime.Now`**  
NT8 strategies and Add-Ons run in a deterministic event model. `DateTime.Now` is wall-clock-dependent and produces different results across machines and time zones. Use `DateTime.UtcNow` for all timestamps. Same rule as V12 (OKF: FSM determinism).

---

## Knowledge Transfer from V12 Wave Work

The following V12 principles apply unchanged to NinjaTrader Add-On code:

| V12 Rule | NT8 Application |
|---|---|
| JS-021: No `lock()` | NT8 Add-On -- same thread model, same risk. `Interlocked` / `ConcurrentDictionary` / `volatile` only. |
| JS-023: Atomic primitives | `_isCopyEnabled` = `volatile bool`. Toggle from either UI surface atomically. |
| JS-025: Lock-free structures | `ConcurrentDictionary<string, long>` for dedup TTL. Never `Dictionary<T>` + manual lock. |
| JS-001: No exceptions in hot path | `OnOrderUpdate` fires on every order event. `SendCopy()` returns `bool`. No `throw`. |
| JS-003: Correctness by construction | `TrimSignal` has no qty field. Illegal state is structurally unrepresentable. |
| JS-008: Readonly structs | `CopySignal`, `CopyRule`, `TrimSignal` -- all `private readonly struct`. |
| JS-010: Private constructors | Signal structs use `private` ctor + `static Create()` factory. |
| ASCII-only | NT8 NinjaScript export/import is ASCII. Same rule, same scans. |
| CYC <= 8 | Every method in `src/PropTraderTools/`. Gate chain = linear early-returns. |
| xUnit only | `[Fact]` / `Assert.Equal()`. Never NUnit or MSTest. |
| `DateTime.UtcNow` | Deterministic timestamps. Never `DateTime.Now`. |

### What is NT8-specific (not in V12)

| NT8 Rule | Why |
|---|---|
| `NTButtonStyle` for all buttons | NT-native appearance pillar -- looks like it shipped with NT |
| `AccountComboBoxStyle` for account selectors | Matches NT's own ChartTrader account dropdown exactly |
| `NTBrushes.*` for all colors | Theme-aware -- no hardcoded hex |
| No `FontFamily` override | Inherit NT WPF theme -- never fight the host |
| `"PTT-"` prefix on all `CreateOrder` names | `IsBracketLeg()` Layer 2 safety -- prevents self-cancellation race |
| `IsBracketLeg()` 3-layer guard | Layer 1: `FromEntrySignal != null` (ATM-stamped, structural). Layer 2: PTT- prefix. Layer 3: Stop/Target name prefix. |
| `AllAccounts(instrument)` as single scope source | Instrument fence -- MES cancel never sees MNQ accounts |
| `Account.All.OrderUpdate` hooked ONCE | Never per-chart. Add-On lives outside any chart. |

---

## Brain Directory Convention

All PTT brain artifacts live in Director:

```
docs/brain/PTT-COPIER-B1/     -- Block 1
docs/brain/PTT-COPIER-B2/     -- Block 2 (future)
docs/brain/PTT-[FEATURE]-B1/  -- any future NT Add-On feature
```

Each brain dir contains the standard `/nt-builder` artifact set (7-phase pipeline):

```
manifest.json              -- initialized by Tier 1 pre-flight
02-architecture-plan.md    -- Phase 1: architect output
02-plan-review.md          -- Phase 2: plan reviewer output (plan vs spec + RULES_CATALOG)
04-tickets.md              -- Phase 3: ticket generation output
04-ticket-review.md        -- Phase 3.5: ticket reviewer output (NEW -- tickets vs plan + spec + RULES_CATALOG)
ticket-N-completion.md     -- Phase 4a: engineer output per ticket
ticket-N-verification.md   -- Phase 4b: verifier output per ticket
05-final-review.md         -- Phase 5: final review (MUST include Section K)
06-deferred-backlog.md     -- Phase 5: deferred ledger (REQUIRED -- blocks FINAL_PASS if missing)
```

### Phase 3.5 — Ticket Review Gate

`04-ticket-review.md` is a **hard gate** added in V1.1 of the pipeline.
No engineer is spawned until `TICKET_REVIEW_PASS` is confirmed in this file.

What it checks:
- Traceability: every ticket item maps to a spec requirement or plan item
- JS pre-check: no bad patterns described in ticket text (lock, throw, null, magic string)
- CYC pre-check: no method with estimated CYC > 8
- NT8 constraints: no async lifecycle, no Account.All in ctor, no sealed Window
- Test coverage: every new method has a [Fact] test specified
- Scan checklist: all 7 scans listed in every ticket

### Jane Street DNA in Agents

All ptt- agents (architect, plan-reviewer, ticket-reviewer, engineer, verifier) have
the full Jane Street rule set hardcoded into their roleDefinition.
Rules are not just in RULES_CATALOG.md — they are IN the agent.
This means violations are caught by agent reasoning, not just by grep scans.
The catalog file is still read at each phase for full traceability and rule lookups,
but the critical rules are also enforced at the agent decision level.

---

## PTT Pipeline Team Map

Every PTT agent knows its own phase. This section gives every agent the full picture so no one makes decisions based on incomplete context.

```
Tier 1 Director         copier-spec mode
                          Pre-flight checks. Emits Tier 2 prompt. Never writes .cs.

Tier 2 Orchestrator     ptt-orchestrator mode
                          Drives all start_subtask chains. Owns Lamport clock.
                          Never writes .cs or tickets directly.

Phase 1 -- Architect    ptt-architect
                          Reads spec + prior backlog. Writes 02-architecture-plan.md.
                          Must run 8+ sequential thoughts before writing.

Phase 2 -- Plan Review  ptt-plan-reviewer
                          Plan vs spec + RULES_CATALOG. REVIEW_PASS unlocks Phase 3.
                          REVIEW_FAIL sends back to architect (max 2 cycles).

Phase 3 -- Tickets      ptt-architect
                          Writes 04-tickets.md from REVIEW_PASS plan.
                          Each ticket MUST include spec IDs, signatures, [Fact] tests,
                          and the 7-scan checklist (SCAN-01 through SCAN-07).

Phase 3.5 -- Tkt Review ptt-ticket-reviewer
                          Tickets vs plan + spec + RULES_CATALOG.
                          TICKET_REVIEW_PASS unlocks Phase 4a.
                          TICKET_REVIEW_FAIL sends back to architect (max 2 cycles).

Phase 4a -- Engineer    ptt-engineer
                          Implements C# in Wave workspace from the reviewed ticket.
                          Runs all 7 scans (Layer 2). Reports in ticket-N-completion.md.
                          BUILD_PASS requires all 7 scans at zero.

Phase 4b -- Verifier    ptt-verifier
                          Independently re-runs all 7 scans (Layer 3).
                          Cross-checks against engineer Layer 2 report.
                          VERIFY_PASS required before next ticket or Phase 5.

Phase 5 -- Final Review ptt-plan-reviewer
                          Cross-file coherence. All 7 scans zero across full src/.
                          Writes 05-final-review.md (Section K required).
                          Writes 06-deferred-backlog.md (FINAL_PASS blocked without it).
```

---

## Per-Ticket 7-Scan Checklist SOP

**This is a mandatory non-negotiable workflow element. Read before questioning it.**

The 7-scan checklist appears in three places per ticket on purpose. This is **defense in depth**, not redundancy. Each layer serves a distinct purpose:

| Layer | Where | Who | Purpose |
|-------|-------|-----|---------|
| **Layer 1** | Inside each ticket in `04-tickets.md` | Architect writes it | Engineer's contract — defines exactly what scans must pass before BUILD_PASS |
| **Layer 2** | Inside `ticket-N-completion.md` | Engineer self-reports | Engineer's attestation — proves scans were run in the correct workspace |
| **Layer 3** | Inside `ticket-N-verification.md` | Verifier runs independently | Integrity check — cross-checks Layer 2 against independent execution |

### Why each layer is required

**Layer 1 (per-ticket checklist in tickets):**
Without this, the engineer has no explicit contract to fulfill. They may run fewer scans, run them against the wrong path, or assume the orchestrator handles them. The checklist in the ticket is the only document the engineer is guaranteed to read before implementing.

**Layer 2 (engineer self-report in completion):**
Without this, the verifier has nothing to cross-check. The verifier's job is to compare their own scan results against what the engineer reported. If the engineer never reported, the verifier cannot detect a false negative. This is the attestation layer.

**Layer 3 (verifier independent run):**
Without this, the engineer's self-report is unverified. The verifier is the independent audit that makes the attestation meaningful. Layer 3 only works if Layer 1 first established what "zero" means for this ticket.

### Gate behavior

| Agent | Action required | Failure behavior |
|-------|----------------|-----------------|
| `ptt-ticket-reviewer` | FAIL any ticket missing SCAN-01 through SCAN-07 | `TICKET_REVIEW_FAIL` → re-architect tickets |
| `ptt-engineer` | Run all 7 scans to zero, report all in completion.md | `BUILD_FAIL` → retry up to 2x |
| `ptt-verifier` | Re-run all 7 scans independently, cross-check Layer 2 | `VERIFY_FAIL` → engineer retry up to 3x |
| `ptt-orchestrator` | Enforce gate ordering via Lamport clock | Block next phase if any scan layer missing |

### What NOT to do

- **Never** consolidate per-ticket scan checklists into a single shared checklist at the end of `04-tickets.md`.
- **Never** flag per-ticket scan checklists as "redundant" or "unnecessary overhead".
- **Never** accept `TICKET_REVIEW_PASS` from a session that did not check for per-ticket scan checklists.
- **Never** return `BUILD_PASS` with any scan un-run or any scan showing results > 0.
- **Never** return `VERIFY_PASS` without running all 7 scans independently via `ctx_shell`.

---
