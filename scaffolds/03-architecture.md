# Architecture Validation Report

**Epic**: EPIC-{SLUG}
**Date**: {ISO-8601}
**Validator**: {Agent Name}

## 1. Dependency Analysis

### 1.1 Circular Dependencies
**Tool**: `get_dependency_cycles`

**Baseline (Before)**:
- Cycles detected: 
- Files involved: 

**Projected (After)**:
- New cycles introduced: 
- Risk assessment: 

**Mitigation**:
- 

### 1.2 Dependency Graph
**Tool**: `get_dependency_graph`

**Key Observations**:
- Depth of dependency tree: 
- Fan-out (files importing target): 
- Fan-in (files target imports): 

**Architectural Concerns**:
- 

## 2. Coupling Metrics

### 2.1 Afferent Coupling (Ca)
**Tool**: `get_coupling_metrics`

**Target File**: `{file_path}`
- **Ca (Dependents)**: {count} files import this module
- **Change**: {+/-X from baseline}

**Interpretation**:
- 

### 2.2 Efferent Coupling (Ce)
- **Ce (Dependencies)**: {count} files this module imports
- **Change**: {+/-X from baseline}

**Interpretation**:
- 

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

**Violations Detected**:
- 

**Remediation**:
- 

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
- 

**Postconditions**:
- 

**Invariants**:
- 

### 4.2 New Abstractions
**For each new class/interface/enum**:

#### Type: `{TypeName}`
- **Purpose**: 
- **Responsibility**: 
- **Collaborators**: 
- **Constraints**: 

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
- 

**Mitigation Strategy**:
- 

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
- **Context**: 
- **Options Considered**:
  1. {Option A}: 
  2. {Option B}: 
- **Decision**: 
- **Rationale**: 
- **Consequences**: 

### 6.2 V12 DNA Alignment
- **"Make illegal states unrepresentable"**: 
- **CYC ≤ 15**: 
- **Jane Street Cognitive Simplicity**: 
- **Lock-free patterns**: 
- **Zero-allocation**: 

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