# Jane Street Sentinel Enforcement Protocol

**Version**: 2.0  
**Effective**: 2026-06-05  
**Philosophy**: "Metal Sharpens Metal" - Every agent thinks with Jane Street principles

## Core Principle

**Every agent MUST internalize Jane Street principles as their primary decision-making framework.** When any agent encounters a design decision, implementation choice, or validation question about `src/` code, the FIRST question is:

> **"What does Jane Street have to say about this?"**

This is not a checklist to run after the fact - it's the lens through which ALL src/ decisions are made.

---

## 1. Agent-Level Jane Street Integration

### 1.1 Knowledge Delivery Methods

Each agent receives Jane Street principles through multiple channels:

#### RAG/CAG (Retrieval-Augmented Generation)
- **Firebase Knowledge Base**: `jane_street_knowledge_base` collection
- **Query Tool**: `python scripts/query_kb.py <term>`
- **Auto-Generated Rules**: `.bob/rules-v12-engineer/99-jane-street-auto.md`
- **Bootstrap Context**: `scripts/agent_bootstrap.py` loads Jane Street patterns on session start

#### Explicit Instructions
- **Custom Mode Rules**: `.bob/custom_modes.yaml` embeds Jane Street DNA
- **Command Protocols**: `/epic-run`, `/pr-loop`, `/epic-loop` include Jane Street audit steps
- **Workflow Documentation**: This file and related protocol docs

#### Persistent Memory
- **Deviations Log**: `docs/standards/JANE_STREET_DEVIATIONS.md` - documented exceptions
- **Session Context**: Agents carry Jane Street decisions forward across turns

### 1.2 Agent Responsibilities

| Agent | Jane Street Role | Knowledge Source |
|-------|------------------|------------------|
| **v12-epic-planner** | Architect - Design validation against Jane Street principles | RAG (query_kb.py) + rules-v12-epic-planner/dna.md |
| **v12-engineer** | Implementer - Surgical edits aligned with Jane Street patterns | RAG (99-jane-street-auto.md) + rules-v12-engineer/dna.md |
| **v12-phase7-lead** | Concurrency Expert - Lock-free primitives per Jane Street HFT | Explicit (ABSOLUTE PROHIBITIONS + APPROVED PRIMITIVES) |
| **Advanced mode** | Validator - Verify Jane Street DNA compliance | Explicit (pre_push_validation.ps1 + JANE_STREET_DEVIATIONS.md) |
| **Orchestrator** | Coordinator - Ensure Jane Street gates are enforced | Explicit (command protocols) |

---

## 2. Jane Street Decision Framework

### 2.1 The First Question Protocol

**Before ANY src/ decision, agents MUST ask:**

```
DECISION: [What I'm about to do]

JANE STREET QUERY:
1. What principle applies? (e.g., "Make illegal states unrepresentable")
2. What pattern exists? (e.g., Actor/FSM, Result<T>, Option<T>)
3. What deviation is documented? (Check JANE_STREET_DEVIATIONS.md)
4. What would Jane Street do? (Query KB if uncertain)

DECISION: [Proceed/Adjust/Escalate to Director]
```

### 2.2 Example: Lock vs Actor Pattern

**Bad (Mechanical Checklist)**:
```
❌ Agent: "I'll use lock() here, then run grep to check if it's allowed."
```

**Good (Jane Street First)**:
```
✅ Agent: "I need thread-safe state mutation. Jane Street principle JS-021: 
          ABSOLUTE BAN on lock(). What's the approved pattern?"
          
   Query: python scripts/query_kb.py "actor pattern channel"
   
   Result: Use System.Threading.Channels.Channel<T> for FSM state transitions.
   
   Decision: Implement Actor pattern with Channel-based Enqueue.
```

### 2.3 Example: Complexity Threshold

**Bad (Tool-Driven)**:
```
❌ Agent: "Codacy says CYC 9 is too high (threshold 8). I'll split this method."
```

**Good (Jane Street First)**:
```
✅ Agent: "Method has CYC 9 with guard clauses. Jane Street principle: 
          'Cognitive simplicity over tool thresholds.'
          
   Check: JANE_STREET_DEVIATIONS.md Decision #8
   
   Result: CYC 9 with guard clauses is 40% under V12 threshold (15).
           Jane Street approves guard clause pattern for validation logic.
   
   Decision: SUPPRESS Codacy warning. Document as Jane Street deviation.
```

---

## 3. Enforcement Points (Jane Street Lens)

### 3.1 Pre-GitHub Loops

#### `/pr-loop` - Bot Forensics (Step 1)

**Jane Street Integration**:
```markdown
For each bot finding:
1. Read: docs/standards/JANE_STREET_DEVIATIONS.md
2. Ask: "Does this conflict with documented Jane Street principles?"
3. Categorize:
   - [VALID-FIX]: Aligns with Jane Street → must fix
   - [VALID-SUPPRESS]: Conflicts with Jane Street → suppress + document
   - [HALLUCINATION]: Bot error → ignore
```

**Agent Mindset**: "I'm not just categorizing issues - I'm defending Jane Street principles against tool noise."

#### `/epic-run` - Verification (Step C)

**Jane Street Integration**:
```markdown
2. JANE STREET AUDIT (MANDATORY):
   - Verify no new violations:
     * Zero locks (grep -r "lock(" src/)
     * ASCII-only
     * FSM/Actor pattern
     * Complexity ≤15
   - If conflicts detected: HALT and report
```

**Agent Mindset**: "I'm the last line of defense before F5. Jane Street DNA must be intact."

#### `/epic-loop` - Per-Epic Checks

**Jane Street Integration**:
```markdown
2. **Per-Epic**: Check #15 (jane_street_validator.py) + Check #16 (jane_street_rule_checker.py)
```

**Agent Mindset**: "Every epic must advance Jane Street alignment, never regress."

---

### 3.2 Custom Modes (Jane Street DNA)

#### v12-epic-planner

**Current State**: ✅ PLAN-ONLY - Uses jCodemunch + query_kb.py for Jane Street grounding

**Jane Street Integration**:
- **Phase 2 (Plan)**: Query KB for relevant patterns before proposing approach
- **Phase 3 (Validate)**: Stress-test plan against Jane Street principles
- **Phase 4 (Tickets)**: Each ticket must cite Jane Street rationale

**Agent Mindset**: "I'm not just planning refactoring - I'm architecting Jane Street alignment."

#### v12-engineer

**Current State**: ⚠️ PARTIAL - Has DNA rules but needs explicit Jane Street query step

**Recommended Enhancement**:
```yaml
PLAN-THEN-EXECUTE PROTOCOL:
1. Read ticket brief
2. Query Jane Street KB for relevant patterns:
   python scripts/query_kb.py "<ticket domain>"
3. Verify live src/ against Jane Street principles
4. Produce written PLAN citing Jane Street rationale
5. Execute surgical edits
6. Verify Jane Street DNA compliance
```

**Agent Mindset**: "Every edit is a Jane Street pattern application, not just a code change."

#### v12-phase7-lead

**Current State**: ✅ EXCELLENT - Explicit Jane Street prohibitions and approved primitives

**Strengths**:
- ABSOLUTE PROHIBITIONS (lines 85-90) - Jane Street lock ban
- APPROVED PRIMITIVES (lines 98-103) - Jane Street concurrency patterns
- MANDATORY ANALYSIS PROTOCOL (lines 92-96) - Jane Street research first

**Agent Mindset**: "I am the Jane Street concurrency enforcer. No compromises."

---

## 4. Jane Street Knowledge Base Integration

### 4.1 Query Protocol

**When to Query**:
- Before proposing any architectural change
- When encountering unfamiliar patterns
- When tool recommendations conflict with intuition
- When Director asks "What does Jane Street say?"

**How to Query**:
```bash
# Domain-specific query
python scripts/query_kb.py "actor pattern"
python scripts/query_kb.py "zero allocation"
python scripts/query_kb.py "cache locality"

# Principle-specific query
python scripts/query_kb.py "make illegal states unrepresentable"
python scripts/query_kb.py "correctness by construction"
```

**Output Format**:
```json
{
  "results": [
    {
      "title": "Actor Pattern for State Machines",
      "category": "Hardware-Software Codesign",
      "summary": "Use Channel<T> for FSM transitions...",
      "source": "Jane Street Tech Talk: Building an Exchange"
    }
  ]
}
```

### 4.2 Auto-Generated Rules

**File**: `.bob/rules-v12-engineer/99-jane-street-auto.md`

**Generation**: Automatic via `scripts/agent_bootstrap.py`

**Content**: Top 5 Jane Street patterns relevant to current session context

**Agent Usage**: Read on session start, reference during implementation

---

## 5. Deviation Management (Jane Street Exceptions)

### 5.1 When to Deviate

**Valid Reasons**:
1. **Hot-path optimization** (e.g., primitives over objects for zero-allocation)
2. **Platform constraints** (e.g., NinjaTrader API requirements)
3. **Testability** (e.g., instance methods for DI over static methods)
4. **Cognitive simplicity** (e.g., CYC 9 guard clauses over artificial splitting)

**Invalid Reasons**:
1. ❌ "Tool says so" (tools don't understand Jane Street context)
2. ❌ "Faster to implement" (Jane Street prioritizes correctness)
3. ❌ "Legacy code does it" (we're refactoring away from legacy)

### 5.2 Documentation Protocol

**File**: `docs/standards/JANE_STREET_DEVIATIONS.md`

**Required Fields**:
1. **Date, PR, File/Method** - Traceability
2. **Tool Complaint** - What triggered the deviation
3. **Jane Street Alignment** - Why this aligns with Jane Street principles
4. **Decision** - SUPPRESS or FIX with rationale

**Example (Decision #9)**:
```markdown
## Decision #9: Primitive Obsession in Hot Path

**Issue**: CodeScene flags 62.5% primitive types

**Jane Street Alignment**:
- HFT systems co-locate hot-path logic to minimize allocations
- Primitives are zero-allocation value types
- Jane Street's "Building an Exchange" emphasizes cache locality

**Decision**: SUPPRESS - Hot-path co-location is intentional
```

---

## 6. Validation Scripts (Jane Street Enforcers)

### 6.1 jane_street_validator.py

**Purpose**: Validate code against P0/P1/P2 rules

**Jane Street Integration**: Checks documented deviations before flagging violations

**Usage**:
```bash
python scripts/jane_street_validator.py --files src/V12_002.cs
python scripts/jane_street_validator.py --all
```

### 6.2 jane_street_rule_checker.py

**Purpose**: Check specific rule violations

**Jane Street Integration**: Supports deviation context (e.g., "CYC 9 OK if guard clauses")

**Usage**:
```bash
python scripts/jane_street_rule_checker.py --rule JS-021 --path src/
python scripts/jane_street_rule_checker.py --rule JS-067 --threshold 15
```

### 6.3 pre_push_validation.ps1 (Check #15)

**Purpose**: Gate all pushes with Jane Street audit

**Jane Street Integration**: Reads JANE_STREET_DEVIATIONS.md to allow documented exceptions

**Recommended Addition**:
```powershell
# Check #15: Jane Street Validator
Write-Host "`n[15/15] Jane Street Validator..." -ForegroundColor Cyan
$jsResult = & python scripts/jane_street_validator.py --all
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ❌ FAIL: Jane Street violations detected" -ForegroundColor Red
    Write-Host "  Query KB: python scripts/query_kb.py '<violation domain>'" -ForegroundColor Yellow
    $blockingFailed = $true
} else {
    Write-Host "  ✅ PASS: No Jane Street violations" -ForegroundColor Green
}
```

---

## 7. Metal Sharpens Metal: Agent Collaboration

### 7.1 Cross-Agent Jane Street Reinforcement

**Scenario**: v12-engineer proposes a design, Advanced mode validates

**Flow**:
```
v12-engineer: "I'll use lock() for thread safety."
              ↓
Advanced mode: "HALT. Jane Street JS-021: ABSOLUTE BAN on lock().
                Query KB for approved pattern."
              ↓
v12-engineer: "Query result: Use Channel<T> Actor pattern.
                Revised plan: Implement FSM with Enqueue."
              ↓
Advanced mode: "APPROVED. Aligns with Jane Street concurrency principles."
```

**Principle**: Agents challenge each other's Jane Street alignment, not just rubber-stamp.

### 7.2 Director Escalation Protocol

**When Agent is Uncertain**:
```
Agent: "I need to [decision]. Jane Street KB shows conflicting patterns.
        
        Option A: [pattern] (aligns with JS-XXX)
        Option B: [pattern] (aligns with JS-YYY)
        
        What does Jane Street have to say about this specific case?"

Director: [Provides context or approves deviation]
```

**Principle**: Agents don't guess - they escalate Jane Street ambiguity to Director.

---

## 8. Jane Street Rule Catalog (Quick Reference)

### P0 Rules (35 critical - BLOCKS MERGE)

| Rule ID | Principle | Pattern |
|---------|-----------|---------|
| JS-001 | Use Result<T> instead of exceptions | `Result<T, Error>` |
| JS-002 | Use Option<T> instead of null | `Option<T>` |
| JS-021 | ABSOLUTE BAN on lock() | `Channel<T>` Actor |
| JS-036 | Use Span<T> for zero-allocation | `Span<T>`, `Memory<T>` |
| JS-042 | NO MAGIC NUMBERS | Named constants |
| JS-067 | Cyclomatic complexity ≤8 (strict) | Guard clauses, early returns |

**Full catalog**: See `docs/standards/JANE_STREET_RULES_P0.md`

### P1 Rules (35 high - BLOCKING IN GODMODE)

| Rule ID | Principle | Pattern |
|---------|-----------|---------|
| JS-101 | XML documentation on all public APIs | `/// <summary>` |
| JS-103 | TODO comments must reference tickets | `// TODO(EPIC-X): ...` |
| JS-105 | Use ConfigureAwait(false) in library code | `.ConfigureAwait(false)` |

**Full catalog**: See `docs/standards/JANE_STREET_RULES_P1.md`

### P2 Rules (30 medium - BLOCKING IN GODMODE)

| Rule ID | Principle | Pattern |
|---------|-----------|---------|
| JS-106 | Consistent naming conventions | PascalCase, camelCase |
| JS-107 | No trailing whitespace | CSharpier auto-fix |
| JS-110 | Prefer expression-bodied members | `=> expression` |

**Full catalog**: See `docs/standards/JANE_STREET_RULES_P2.md`

---

## 9. Success Metrics

### 9.1 Agent-Level Metrics

- **Jane Street Query Rate**: Agents query KB before major decisions
- **Deviation Documentation**: All suppressions cite Jane Street rationale
- **Escalation Quality**: Agents escalate ambiguity, not ignorance
- **Cross-Agent Challenges**: Agents catch each other's Jane Street violations

### 9.2 Codebase-Level Metrics

- **Lock-Free Compliance**: `grep -r "lock(" src/` returns zero matches
- **Complexity Floor**: 95% of methods below CYC 15
- **Deviation Stability**: Documented deviations remain valid over time
- **Tool Noise Reduction**: 78% of bot findings are VALID-SUPPRESS (Jane Street aligned)

---

## 10. References

- **Jane Street Deviations**: `docs/standards/JANE_STREET_DEVIATIONS.md`
- **Jane Street KB Query**: `python scripts/query_kb.py <term>`
- **Auto-Generated Rules**: `.bob/rules-v12-engineer/99-jane-street-auto.md`
- **Custom Modes**: `.bob/custom_modes.yaml`
- **Epic Run Command**: `.bob/commands/epic-run.md`
- **PR Loop Command**: `.bob/commands/pr-loop.md`

---

**Last Updated**: 2026-06-05  
**Philosophy**: "Metal Sharpens Metal" - Every agent thinks Jane Street first  
**Next Review**: 2026-09-05 (Quarterly)