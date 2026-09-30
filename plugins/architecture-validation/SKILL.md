name: architecture-validation
description: Systematic architectural validation between planning and implementation using jCodemunch tools to prevent circular dependencies, coupling degradation, layer violations, and unclear interface contracts. Mandatory for epics touching >3 files or introducing new abstractions.

---

# Architecture Validation Skill

## Purpose
Systematic architectural validation between planning and implementation using jCodemunch tools to prevent:
- Circular dependency introduction
- Coupling metric degradation
- Layer boundary violations
- Unclear interface contracts

## When to Use

### Mandatory (ARCHITECTURE-GATE required)
- Epic touches >3 files
- Introduces new abstractions (classes, interfaces, enums)
- Extracts methods called from >2 locations
- Modifies public APIs
- Changes cross-file dependencies

### Optional
- Simple single-file extractions
- No new abstractions
- Internal refactoring only

## Integration Point
**Phase 2 (Planning)** - After `03-approach.md`, before Phase 3 validation

## Tools Required
- `get_dependency_cycles` - Detect circular dependencies
- `get_dependency_graph` - Visualize dependency structure
- `get_coupling_metrics` - Measure Ca/Ce/Instability
- `get_layer_violations` - Check architectural layer compliance
- `get_blast_radius` - Impact analysis for changes

## Output Document
`docs/brain/EPIC-{SLUG}/03-architecture.md`

## Template Structure

```markdown
# Architecture Validation Report

**Epic**: EPIC-{SLUG}
**Date**: {ISO-8601}
**Validator**: {Agent Name}

## 1. Dependency Analysis

### 1.1 Circular Dependencies
**Tool**: `get_dependency_cycles`

**Baseline (Before)**:
- Cycles detected: {count}
- Files involved: {list}

**Projected (After)**:
- New cycles introduced: {count}
- Risk assessment: {LOW|MEDIUM|HIGH}

**Mitigation**:
- {Action items if cycles detected}

### 1.2 Dependency Graph
**Tool**: `get_dependency_graph`

**Key Observations**:
- Depth of dependency tree: {number}
- Fan-out (files importing target): {count}
- Fan-in (files target imports): {count}

**Architectural Concerns**:
- {List any god-file patterns, deep nesting, etc.}

## 2. Coupling Metrics

### 2.1 Afferent Coupling (Ca)
**Tool**: `get_coupling_metrics`

**Target File**: `{file_path}`
- **Ca (Dependents)**: {count} files import this module
- **Change**: {+/-X from baseline}

**Interpretation**:
- {HIGH Ca = stable, many dependents}
- {LOW Ca = unstable, few dependents}

### 2.2 Efferent Coupling (Ce)
- **Ce (Dependencies)**: {count} files this module imports
- **Change**: {+/-X from baseline}

**Interpretation**:
- {HIGH Ce = fragile, many dependencies}
- {LOW Ce = independent}

### 2.3 Instability Score (I)
- **Formula**: I = Ce / (Ca + Ce)
- **Score**: {0.0-1.0}
- **Assessment**: {STABLE|BALANCED|UNSTABLE}

**Thresholds**:
- 0.0-0.3: Stable (good for core abstractions)
- 0.3-0.7: Balanced (acceptable)
- 0.7-1.0: Unstable (refactor if high-churn)

## 3. Layer Violations

### 3.1 Architectural Layers
**Tool**: `get_layer_violations`

**Defined Layers** (from `.jcodemunch.jsonc`):
```json
{
  "architecture": {
    "layers": [
      {
        "name": "Core",
        "paths": ["src/Core/**"],
        "may_not_import": ["src/Indicators/**", "src/Strategies/**"]
      },
      {
        "name": "Indicators",
        "paths": ["src/Indicators/**"],
        "may_not_import": ["src/Strategies/**"]
      },
      {
        "name": "Strategies",
        "paths": ["src/Strategies/**"],
        "may_not_import": []
      }
    ]
  }
}
```

**Violations Detected**:
- {List any cross-layer imports that violate rules}
- {Or "None detected"}

**Remediation**:
- {Action items if violations found}

## 4. Interface Contracts

### 4.1 Extracted Methods
**For each extracted method**:

#### Method: `{MethodName}`
- **Signature**: `{return_type} {MethodName}({params})`
- **Visibility**: `{public|private|internal}`
- **Callers**: {count} locations
- **Contract Clarity**: {CLEAR|AMBIGUOUS|UNCLEAR}

**Contract Documentation**:
```csharp
/// <summary>
/// {What does this method do?}
/// </summary>
/// <param name="{param}">{What does this parameter represent?}</param>
/// <returns>{What does this method return?}</returns>
/// <exception cref="{ExceptionType}">{When is this thrown?}</exception>
```

**Preconditions**:
- {List any assumptions about input state}

**Postconditions**:
- {List any guarantees about output state}

**Invariants**:
- {List any properties that must remain true}

### 4.2 New Abstractions
**For each new class/interface/enum**:

#### Type: `{TypeName}`
- **Purpose**: {Single-sentence description}
- **Responsibility**: {What is this type's job?}
- **Collaborators**: {What other types does it work with?}
- **Constraints**: {What rules must it follow?}

**V12 DNA Alignment**:
- [ ] Makes illegal states unrepresentable
- [ ] No lock-based synchronization
- [ ] ASCII-only string literals
- [ ] Cyclomatic complexity ≤ 15

## 5. Blast Radius Analysis

### 5.1 Impact Scope
**Tool**: `get_blast_radius`

**Symbol**: `{symbol_name}`
- **Direct Dependents**: {count} files
- **Transitive Dependents**: {count} files (depth={N})
- **Risk Score**: {0.0-1.0}

**High-Risk Dependents**:
- {List files with high complexity or churn}

**Mitigation Strategy**:
- {How will we minimize disruption?}

### 5.2 Breaking Change Assessment
- **API Changes**: {YES|NO}
- **Signature Changes**: {YES|NO}
- **Behavioral Changes**: {YES|NO}

**Backward Compatibility**:
- {MAINTAINED|BROKEN}
- {Justification if broken}

## 6. Architectural Decisions

### 6.1 Key Decisions
**For each significant architectural choice**:

#### Decision: {Short title}
- **Context**: {Why did this decision arise?}
- **Options Considered**:
  1. {Option A}: {Pros/Cons}
  2. {Option B}: {Pros/Cons}
- **Decision**: {Chosen option}
- **Rationale**: {Why this option?}
- **Consequences**: {What are the trade-offs?}

### 6.2 V12 DNA Alignment
- **"Make illegal states unrepresentable"**: {How does this design enforce correctness?}
- **CYC ≤ 15**: {How does this keep functions simple?}
- **Jane Street Cognitive Simplicity**: {How does this reduce cognitive load?}
- **Lock-free patterns**: {Does this introduce or remove locks?}
- **Zero-allocation**: {Does this affect allocation patterns?}

## 7. Success Criteria

- [ ] No new circular dependencies introduced
- [ ] Coupling metrics stable or improving (ΔI ≤ 0.1)
- [ ] All extracted methods have explicit contracts
- [ ] No layer violations detected
- [ ] Architectural decisions documented with rationale
- [ ] Blast radius understood and mitigated
- [ ] V12 DNA principles upheld

## 8. Validation Checklist

### Pre-Implementation
- [ ] Dependency cycles analyzed
- [ ] Coupling metrics baselined
- [ ] Layer violations checked
- [ ] Interface contracts defined
- [ ] Blast radius assessed

### Post-Implementation
- [ ] Re-run `get_dependency_cycles` (verify no new cycles)
- [ ] Re-run `get_coupling_metrics` (verify ΔI ≤ 0.1)
- [ ] Re-run `get_layer_violations` (verify zero violations)
- [ ] Code review confirms contracts match implementation
- [ ] Integration tests pass

## 9. Appendix

### 9.1 Tool Outputs
**Attach raw JSON outputs from**:
- `get_dependency_cycles`
- `get_coupling_metrics`
- `get_layer_violations`
- `get_blast_radius`

### 9.2 References
- Matt Pocock: "Architectural Improvement Methodology"
- V12 DNA: `AGENTS.md`
- Jane Street Intel: `docs/intel/jane-street/`
```

## V12 DNA Alignment

### Correctness by Construction
- **Validates type safety**: Interface contracts ensure method signatures are explicit
- **Prevents invalid states**: Coupling metrics catch god-file patterns before they emerge
- **Enforces boundaries**: Layer violations tool prevents architectural drift

### CYC ≤ 15
- **Architectural validation ensures extracted methods stay simple**: By analyzing blast radius and coupling, we catch over-extraction (too many small methods) and under-extraction (methods still too complex)
- **Complexity is a first-class metric**: Coupling metrics (Ca/Ce/I) correlate with cognitive complexity

### Jane Street Cognitive Simplicity
- **Structural clarity reduces cognitive load**: Dependency graphs visualize mental models
- **Explicit contracts reduce surprises**: Interface documentation makes behavior predictable
- **Layer enforcement prevents hidden dependencies**: No "action at a distance" bugs

### Lock-free patterns
- **Orthogonal**: Architectural validation doesn't conflict with lock-free design
- **Complementary**: Can validate that extracted methods don't introduce locks

### Zero-allocation
- **Orthogonal**: Architectural validation doesn't conflict with allocation patterns
- **Complementary**: Can validate that extracted methods don't introduce allocations

## Success Criteria

- [ ] No new circular dependencies introduced
- [ ] Coupling metrics stable or improving (ΔI ≤ 0.1)
- [ ] All extracted methods have explicit contracts
- [ ] No layer violations detected
- [ ] Architectural decisions documented with rationale
- [ ] Blast radius understood and mitigated
- [ ] V12 DNA principles upheld

## Example Usage

### EPIC-POSINFO (Position Info Extraction)

**Scenario**: Extracting position management logic from `V12_002.cs` (god-file with CYC 89)

**Step 1: Baseline Analysis**
```bash
# Check for existing cycles
jcodemunch get_dependency_cycles --repo universal-or-strategy

# Baseline coupling for V12_002.cs
jcodemunch get_coupling_metrics --repo universal-or-strategy --module_path src/V12_002.cs

# Check layer violations
jcodemunch get_layer_violations --repo universal-or-strategy
```

**Step 2: Projected Impact**
```bash
# Analyze blast radius for extracted methods
jcodemunch get_blast_radius --repo universal-or-strategy --symbol UpdatePositionInfo

# Visualize dependency graph
jcodemunch get_dependency_graph --repo universal-or-strategy --file src/V12_002.cs --direction both --depth 2
```

**Step 3: Document in `03-architecture.md`**
- Record baseline metrics
- Document extraction plan
- Define interface contracts for new `PositionInfo` class
- Assess coupling changes (expect ΔI ≈ -0.2 as god-file splits)

**Step 4: Post-Implementation Validation**
```bash
# Re-run all tools to verify no regressions
jcodemunch get_dependency_cycles --repo universal-or-strategy
jcodemunch get_coupling_metrics --repo universal-or-strategy --module_path src/V12_002.PositionInfo.cs
jcodemunch get_layer_violations --repo universal-or-strategy
```

**Expected Outcome**:
- ✅ No new cycles (V12_002.cs already has no cycles)
- ✅ Coupling improved (I drops from 0.8 to 0.6 as responsibilities split)
- ✅ No layer violations (PositionInfo stays in Core layer)
- ✅ Clear contracts (PositionInfo has explicit public API)

## Workflow Integration

### Phase 2 (Planning) - After `03-approach.md`

**Current Phase 2 Steps**:
1. `00-scope.md` - Define boundaries
2. `01-pattern-analysis.md` - Identify patterns
3. `02-analysis.md` - Complexity analysis
4. `03-approach.md` - Extraction strategy

**NEW: Insert Architecture Gate**:
5. **`03-architecture.md`** - Architectural validation (THIS SKILL)
6. Proceed to Phase 3 validation

**Trigger Conditions**:
- If epic is **Mandatory** (see "When to Use" section), create `03-architecture.md`
- If epic is **Optional**, skip to Phase 3

### Phase 3 (Validation) - Before Implementation

**Adjudicator Review**:
- Arena AI reviews `03-architecture.md` for V12 DNA compliance
- Checks coupling metrics against Jane Street standards
- Validates interface contracts for clarity

**Gate Criteria**:
- ✅ PASS: All success criteria met, proceed to Phase 4
- ❌ FAIL: Rework `03-approach.md` and re-run architecture validation

## Post-Use Audit

After using this skill:
1. **Check if architectural validation caught issues that would have been missed**:
   - Did it detect circular dependencies?
   - Did it catch coupling degradation?
   - Did it identify unclear contracts?
2. **Update this skill if gaps are found**:
   - Add new tool if jCodemunch adds capabilities
   - Refine thresholds based on V12 experience
   - Add examples from real epics
3. **Report outcome**:
   - `skill(architecture-validation): no gaps identified` if successful
   - `skill(architecture-validation): gap found - {description}` if issues detected

## References

- **Matt Pocock**: "Architectural Improvement Methodology" (source of this skill)
- **V12 DNA**: `AGENTS.md` (correctness by construction, CYC ≤ 15)
- **Jane Street Intel**: `docs/intel/jane-street/` (cognitive simplicity, HFT patterns)
- **jCodemunch Tools**: `docs/protocol/JCODEMUNCH_INTEGRATION.md`
- **Phase 6 Recursive Protocol**: `AGENTS.md` Section 7 (workflow integration)

## Version History

- **V1.0** (2026-06-02): Initial creation based on Matt Pocock analysis