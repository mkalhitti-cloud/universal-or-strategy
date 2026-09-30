# SKILL: Knowledge Management & RAG Organization

**Version**: 1.0.0  
**Last Updated**: 2026-05-25  
**Status**: ACTIVE - Mandatory for all agents

## Skill Name
**Knowledge Management & RAG Organization** - Systematic management of permanent knowledge sources loaded via bootstrap system

## Skill Description
This skill governs how agents organize, maintain, and expand the auto-loaded knowledge base (RAG system) that provides permanent architectural constraints and domain expertise. It ensures knowledge sources remain under 10% of context budget while maximizing agent expertise.

## When to Use
- **Session Start**: Automatically via `pre_session.py` hook
- **Knowledge Gap Detected**: When agent encounters unfamiliar pattern or technology
- **New Documentation Available**: When C# docs, Jane Street talks, or other authoritative sources are discovered
- **Rule Conflict**: When existing rules contradict or overlap
- **Post-Task Audit**: After completing any architectural or refactoring task

## Prerequisites
- Firebase credentials configured (`firebase-credentials.json`)
- Python 3.12+ with `firebase-admin` package
- Write access to `.bob/rules-v12-engineer/` directory
- Understanding of V12 DNA principles (lock-free, ASCII-only, correctness by construction)

## Knowledge Source Inventory

### Current Sources (Auto-Loaded)
1. **Jane Street Knowledge Base** (Firebase)
   - 10 documents, ~2,241 tokens (1.12% of 200k budget)
   - Categories: Hardware-Software Codesign, Trading Tools, Exchange Architecture, Production Engineering, OCaml Performance
   - Auto-generates: `.bob/rules-v12-engineer/99-jane-street-auto.md`

2. **Graphify Knowledge Graph** (Local)
   - 13,716 nodes from AST analysis
   - God nodes, community structure, import relationships
   - Location: `graphify-out/graph.json`

3. **Compound Intelligence Learnings** (Firebase)
   - Session-derived patterns and insights
   - Confidence-scored, time-windowed (30 days)
   - Collection: `learnings`

4. **Previous Session History** (Firebase)
   - Last 7 days of agent sessions
   - Task outcomes, duration, status
   - Collection: `agent_sessions`

### Potential Sources (Not Yet Indexed)
- **C# Language Documentation** (Microsoft Docs)
- **NinjaTrader 8 API Reference**
- **Lock-Free Programming Patterns** (Preshing, Sutter, Williams)
- **High-Frequency Trading Architecture** (Additional Jane Street talks)
- **Correctness-by-Construction Patterns** (Hillel Wayne, TLA+)

## Steps

### 1. Audit Current Knowledge Coverage
**Action**: Review `.bob/rules-v12-engineer/99-jane-street-auto.md` and identify gaps
**Command**: `python scripts/query_kb.py <term>` to search existing KB
**Success Criteria**: List of covered vs. uncovered domains

### 2. Identify New Knowledge Sources
**Action**: Search for authoritative documentation on uncovered domains
**Priority Order**:
1. Official language/framework docs (C#, NinjaTrader)
2. Academic papers on correctness (TLA+, Alloy)
3. Industry talks from Jane Street, Two Sigma, Citadel
4. Open-source HFT projects (matching engines, order books)

**Validation**: Source must be:
- Authoritative (official docs, peer-reviewed, or from recognized experts)
- Relevant to V12 DNA (lock-free, low-latency, correctness)
- Concise (extractable to <500 tokens per document)

### 3. Extract and Distill Knowledge
**Action**: Create distilled summaries following Jane Street KB format
**Template**:
```json
{
  "document_id": "source_topic_year",
  "title": "Distilled Intel: [Topic]",
  "presenter": "[Author/Organization]",
  "source_url": "[URL]",
  "key_takeaways": [
    "Principle 1",
    "Principle 2",
    "Principle 3"
  ],
  "v12_csharp_patterns": {
    "pattern_name": "How this applies to V12 C# code"
  }
}
```

**Quality Gate**: Each takeaway must be:
- Actionable (not abstract philosophy)
- Specific (includes concrete examples)
- Verifiable (can be checked in code review)

### 4. Add to Firebase Knowledge Base
**Action**: Upload distilled document to `jane_street_knowledge_base` collection
**Command**: 
```python
from query_kb import init_firestore
db = init_firestore()
db.collection('jane_street_knowledge_base').document(doc_id).set(data)
```

**Verification**: Run `python scripts/agent_bootstrap.py Bob architecture` and confirm new document appears

### 5. Update Bootstrap Filters (If Needed)
**Action**: If new category added, update `agent_bootstrap.py` keyword map
**Location**: `scripts/agent_bootstrap.py`, line ~101
**Example**:
```python
keyword_map = {
    'architecture': ['codesign', 'build', 'exchange', 'safe', 'production'],
    'refactoring': ['concurrency', 'coordination', 'safe', 'performance'],
    'performance': ['microsecond', 'skylake', 'performance', 'trading'],
    'testing': ['testing', 'hard', 'fix'],
    'debugging': ['tools', 'traders', 'production', 'engineering'],
    'csharp': ['dotnet', 'clr', 'gc', 'struct', 'span']  # NEW
}
```

### 6. Verify Context Budget
**Action**: Measure total token cost after addition
**Command**: `python scripts/measure_kb_size.py`
**Constraint**: Total must remain <20,000 tokens (10% of 200k budget)
**Remediation**: If exceeded, archive least-used documents or increase specificity of filters

### 7. Test Bootstrap Load
**Action**: Start new Bob session and verify rules are loaded
**Command**: `python .bob/hooks/pre_session.py`
**Success Criteria**: 
- `.bob/rules-v12-engineer/99-jane-street-auto.md` regenerated
- New principles appear in file
- No errors in bootstrap output

## Expected Outcome
- **Knowledge Base**: Expanded with new authoritative source
- **Auto-Generated Rules**: Updated to include new principles
- **Context Budget**: Remains under 10% (verified)
- **Agent Expertise**: Measurably improved on target domain

## Post-Use Audit (MANDATORY)

After using this skill, answer these questions:

1. **Coverage Gap Identified?**
   - [ ] Yes - Document the gap: _______________
   - [ ] No - Current coverage is sufficient

2. **New Source Added?**
   - [ ] Yes - Document ID: _______________, Token cost: _____ tokens
   - [ ] No - Existing sources cover the need

3. **Bootstrap Filter Updated?**
   - [ ] Yes - New keyword map entry: _______________
   - [ ] No - Existing filters are sufficient

4. **Context Budget Status?**
   - Current total: _____ tokens (___% of 200k)
   - [ ] Under 10% - Safe
   - [ ] 10-15% - Monitor
   - [ ] Over 15% - Archive or filter

5. **Skill Gaps Found?**
   - [ ] Yes - Update this skill document with: _______________
   - [ ] No - Skill is complete

6. **Rule Conflicts Detected?**
   - [ ] Yes - Conflicting rules: _______________
   - [ ] No - Rules are consistent

**Completion Statement**: `skill(knowledge_management): [no gaps identified | gaps documented above]`

## Integration with V12 Workflows

### Pre-Task (P3/P4 Planning)
- Query KB for relevant patterns: `python scripts/query_kb.py <domain>`
- Review auto-generated rules in `.bob/rules-v12-engineer/99-jane-street-auto.md`
- Identify knowledge gaps before starting implementation

### During Task (P5 Execution)
- Reference loaded principles when making architectural decisions
- Flag violations of auto-loaded rules during code review
- Document new patterns discovered during implementation

### Post-Task (P6 Audit)
- Run post-use audit (above)
- Add new learnings to compound intelligence: `scripts/agent_session_wrapper.py`
- Update this skill document if gaps found

## Maintenance Schedule

### Weekly
- Review compound intelligence learnings for patterns worth promoting to permanent rules
- Check for new Jane Street talks or papers
- Verify context budget remains under 10%

### Monthly
- Audit rule conflicts and overlaps
- Archive stale or superseded documents
- Update keyword filters based on usage patterns

### Quarterly
- Full knowledge base review
- Identify underutilized sources (consider archiving)
- Survey for new authoritative sources (C# updates, new HFT talks)

## Emergency Procedures

### Context Budget Exceeded (>15%)
1. Run `python scripts/measure_kb_size.py` to identify largest sources
2. Archive least-used documents to `.bob/rules-v12-engineer/archive/`
3. Increase filter specificity in `agent_bootstrap.py`
4. Re-test bootstrap load

### Rule Conflict Detected
1. Document conflicting rules in `docs/brain/rule_conflicts.md`
2. Escalate to Director for resolution
3. Update affected documents with clarification
4. Add conflict resolution note to this skill

### Bootstrap Failure
1. Check Firebase credentials: `firebase-credentials.json`
2. Verify Python dependencies: `pip list | grep firebase`
3. Test manual query: `python scripts/query_kb.py test`
4. Check hook execution: `python .bob/hooks/pre_session.py`
5. Fallback: Load rules manually from `.bob/rules-v12-engineer/`

## Success Metrics

- **Coverage**: 90%+ of V12 architectural decisions have relevant KB reference
- **Context Efficiency**: Knowledge base uses <10% of context budget
- **Agent Performance**: Reduced need for external documentation lookups
- **Rule Compliance**: Zero architectural violations of auto-loaded principles
- **Knowledge Freshness**: All sources updated within last 6 months

## Related Skills
- **Compound Intelligence Integration** (session learning capture)
- **Graphify Knowledge Graph** (codebase structure navigation)
- **Jane Street KB Query** (ad-hoc knowledge retrieval)

---

**Skill Maintenance Log**:
- 2026-05-25: Initial creation (v1.0.0)
- Next review: 2026-06-01
