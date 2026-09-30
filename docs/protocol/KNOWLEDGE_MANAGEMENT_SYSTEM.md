# Knowledge Management System - RAG Organization Protocol

**Version**: 1.0.0  
**Last Updated**: 2026-05-25  
**Status**: ACTIVE

## Overview

The V12 Universal OR Strategy project uses an **auto-loaded knowledge management system** that provides agents with permanent architectural constraints and domain expertise at session start. This system maintains expert-level knowledge while consuming less than 10% of the context budget.

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Agent Session Start                       │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              .bob/hooks/pre_session.py                       │
│         (Automatic Bootstrap Trigger)                        │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│           scripts/agent_bootstrap.py                         │
│         (Knowledge Aggregation Engine)                       │
└─────┬───────────┬───────────┬──────────────┬────────────────┘
      │           │           │              │
      ▼           ▼           ▼              ▼
┌──────────┐ ┌─────────┐ ┌──────────┐ ┌────────────┐
│ Jane St. │ │Graphify │ │Compound  │ │  Session   │
│    KB    │ │  Graph  │ │  Intel   │ │  History   │
│(Firebase)│ │ (Local) │ │(Firebase)│ │ (Firebase) │
└──────────┘ └─────────┘ └──────────┘ └────────────┘
      │           │           │              │
      └───────────┴───────────┴──────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│    .bob/rules-v12-engineer/99-jane-street-auto.md           │
│              (Auto-Generated Rules)                          │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Agent Context Window                            │
│         (Mandatory Architectural Constraints)                │
└─────────────────────────────────────────────────────────────┘
```

## Current Knowledge Sources

### 1. Jane Street Knowledge Base (Firebase)
**Location**: `jane_street_knowledge_base` collection  
**Size**: 10 documents, ~2,241 tokens (1.12% of 200k budget)  
**Update Frequency**: Manual (when new talks/papers discovered)

**Documents**:
1. Hardware-Software Codesign (Oxide at Jane Street)
2. Building Tools for Traders (Ian Henry)
3. How to Build an Exchange
4. Production Engineering When Trading Billions
5. Making OCaml Safe for Performance Engineering
6. *(5 more slots available)*

**Format**:
```json
{
  "document_id": "source_topic_year",
  "title": "Distilled Intel: [Topic]",
  "presenter": "[Author/Organization]",
  "source_url": "[URL]",
  "key_takeaways": [
    "Actionable principle 1",
    "Actionable principle 2"
  ],
  "v12_csharp_patterns": {
    "pattern_name": "C# implementation guidance"
  }
}
```

### 2. Graphify Knowledge Graph (Local)
**Location**: `graphify-out/graph.json`  
**Size**: 13,716 nodes  
**Update Frequency**: On-demand via `graphify update .`

**Provides**:
- AST-derived code structure
- Import relationships
- God nodes (high coupling points)
- Community detection (logical modules)

### 3. Compound Intelligence Learnings (Firebase)
**Location**: `learnings` collection  
**Size**: Variable (time-windowed to 30 days)  
**Update Frequency**: Automatic after each session

**Captures**:
- Session-derived patterns
- Confidence-scored insights
- Task-specific learnings

### 4. Previous Session History (Firebase)
**Location**: `agent_sessions` collection  
**Size**: Last 7 days per agent  
**Update Frequency**: Automatic

**Tracks**:
- Task outcomes
- Duration metrics
- Agent performance

## Expansion Roadmap

### Phase 1: C# Language Documentation (Priority 1)
**Target Size**: ~3,000 tokens  
**Sources**:
- Microsoft C# Language Specification (distilled)
- .NET Performance Best Practices
- Span<T> and Memory<T> patterns
- ValueTask and async patterns
- Struct layout and cache optimization

**Extraction Strategy**:
1. Focus on performance-critical patterns (struct, Span, stackalloc)
2. Extract lock-free primitives (Interlocked, Volatile)
3. Document GC avoidance patterns
4. Capture async/await pitfalls

**Expected Benefit**: Eliminate need for external C# docs lookups during coding

### Phase 2: NinjaTrader 8 API Reference (Priority 2)
**Target Size**: ~2,000 tokens  
**Sources**:
- NinjaTrader 8 Strategy API
- Order management patterns
- Indicator lifecycle
- Drawing object API

**Extraction Strategy**:
1. Focus on V12-relevant APIs (order submission, position tracking)
2. Document lifecycle hooks (OnStateChange, OnBarUpdate)
3. Capture thread-safety requirements
4. Extract common pitfalls

**Expected Benefit**: Reduce NinjaTrader-specific errors, faster development

### Phase 3: Lock-Free Programming Patterns (Priority 3)
**Target Size**: ~2,500 tokens  
**Sources**:
- Preshing on Programming (lock-free series)
- Herb Sutter's atomic<> articles
- Anthony Williams' C++ Concurrency in Action (adapted to C#)
- LMAX Disruptor patterns

**Extraction Strategy**:
1. Distill memory ordering guarantees
2. Document ABA problem and solutions
3. Extract wait-free vs lock-free tradeoffs
4. Capture common lock-free data structures

**Expected Benefit**: Eliminate lock-based code, improve latency

### Phase 4: Correctness-by-Construction (Priority 4)
**Target Size**: ~2,000 tokens  
**Sources**:
- Hillel Wayne's "Practical TLA+"
- Alloy modeling patterns
- Type-driven design principles
- Refinement types in practice

**Extraction Strategy**:
1. Extract "make illegal states unrepresentable" patterns
2. Document state machine verification techniques
3. Capture invariant enforcement patterns
4. Distill property-based testing principles

**Expected Benefit**: Architectural decisions align with formal methods

### Phase 5: High-Frequency Trading Architecture (Priority 5)
**Target Size**: ~1,500 tokens  
**Sources**:
- Additional Jane Street talks
- Two Sigma engineering blog
- Citadel tech talks
- Open-source matching engines

**Extraction Strategy**:
1. Focus on latency optimization patterns
2. Extract order book data structures
3. Document market data handling
4. Capture risk management patterns

**Expected Benefit**: Industry-standard HFT patterns applied to V12

## Context Budget Management

### Current Allocation
- Jane Street KB: 2,241 tokens (1.12%)
- Graphify metadata: ~500 tokens (0.25%)
- Compound Intel: ~300 tokens (0.15%)
- Session history: ~200 tokens (0.10%)
- **Total**: ~3,241 tokens (1.62%)

### Target Allocation (After Expansion)
- Jane Street KB: 2,500 tokens (1.25%)
- C# Docs: 3,000 tokens (1.50%)
- NinjaTrader API: 2,000 tokens (1.00%)
- Lock-Free Patterns: 2,500 tokens (1.25%)
- Correctness Patterns: 2,000 tokens (1.00%)
- HFT Architecture: 1,500 tokens (0.75%)
- Graphify metadata: 500 tokens (0.25%)
- Compound Intel: 500 tokens (0.25%)
- Session history: 500 tokens (0.25%)
- **Total**: ~15,000 tokens (7.50%)

### Budget Safety Margin
- Hard limit: 20,000 tokens (10%)
- Soft limit: 15,000 tokens (7.5%)
- Current headroom: 16,759 tokens (8.38%)

## Adding New Knowledge Sources

### Step-by-Step Process

1. **Identify Source**
   - Must be authoritative (official docs, peer-reviewed, recognized experts)
   - Must be relevant to V12 DNA (lock-free, low-latency, correctness)
   - Must be distillable to <500 tokens per document

2. **Extract and Distill**
   - Follow Jane Street KB format (see above)
   - Focus on actionable principles
   - Include V12 C# patterns
   - Verify each takeaway is specific and verifiable

3. **Add to Firebase**
   ```python
   from query_kb import init_firestore
   db = init_firestore()
   db.collection('jane_street_knowledge_base').document(doc_id).set(data)
   ```

4. **Update Bootstrap Filters** (if new category)
   - Edit `scripts/agent_bootstrap.py`, line ~101
   - Add keywords to `keyword_map`

5. **Verify Context Budget**
   ```bash
   python scripts/measure_kb_size.py
   ```
   - Must remain under 20,000 tokens

6. **Test Bootstrap Load**
   ```bash
   python .bob/hooks/pre_session.py
   ```
   - Verify `.bob/rules-v12-engineer/99-jane-street-auto.md` updated
   - Confirm no errors

7. **Document in Skill**
   - Update `.bob/rules-v12-engineer/SKILL-knowledge-management.md`
   - Add to "Knowledge Source Inventory"
   - Update "Context Budget Status"

## Maintenance Procedures

### Weekly
- Review compound intelligence learnings
- Check for new Jane Street talks
- Verify context budget <10%

### Monthly
- Audit rule conflicts
- Archive stale documents
- Update keyword filters

### Quarterly
- Full knowledge base review
- Survey for new sources
- Identify underutilized sources

## Emergency Procedures

### Context Budget Exceeded (>15%)
1. Run `python scripts/measure_kb_size.py`
2. Archive least-used documents to `.bob/rules-v12-engineer/archive/`
3. Increase filter specificity
4. Re-test bootstrap load

### Bootstrap Failure
1. Check Firebase credentials
2. Verify Python dependencies
3. Test manual query: `python scripts/query_kb.py test`
4. Check hook execution
5. Fallback: Load rules manually

## Success Metrics

- **Coverage**: 90%+ of architectural decisions have KB reference
- **Context Efficiency**: <10% of context budget
- **Agent Performance**: Reduced external lookups
- **Rule Compliance**: Zero violations of auto-loaded principles
- **Knowledge Freshness**: All sources <6 months old

## Related Documentation

- **Skill Document**: `.bob/rules-v12-engineer/SKILL-knowledge-management.md`
- **Bootstrap Script**: `scripts/agent_bootstrap.py`
- **Hook Implementation**: `.bob/hooks/pre_session.py`
- **Query Tool**: `scripts/query_kb.py`
- **Size Measurement**: `scripts/measure_kb_size.py`

## Quick Reference Commands

```bash
# Query existing knowledge
python scripts/query_kb.py <term>

# Measure context budget
python scripts/measure_kb_size.py

# Test bootstrap load
python .bob/hooks/pre_session.py

# Manual bootstrap (with file scope)
python scripts/agent_bootstrap.py Bob architecture src/V12_002.cs

# View auto-generated rules
cat .bob/rules-v12-engineer/99-jane-street-auto.md
```

---

**Document Maintenance**:
- Created: 2026-05-25
- Next Review: 2026-06-01
- Owner: V12 Engineering Team
