# Multi-AI Code Review Process

This folder contains structured code reviews using multiple AI models (Claude, Grok, Gemini, Opus) for consensus validation of critical changes.

## How Multi-AI Reviews Work

### Purpose
- **Catch Blind Spots**: Each AI has different training data and approaches, catching issues others miss
- **Consensus Validation**: Only implement changes agreed upon by majority
- **Diverse Perspectives**: Different coding styles and risk assessments
- **Quality Assurance**: Critical for trading systems where bugs can be expensive

### Process

1. **Code Submission**
   - Submit the same code change to all 4 AIs
   - Include context: what the code does, trading rules, performance requirements
   - Reference relevant skill files (.grok/skills/, etc.)

2. **Individual Reviews**
   - Each AI reviews independently
   - Feedback saved in their respective .md files
   - Focus on: correctness, performance, compliance, edge cases

3. **Round 2 (if needed)**
   - Address disagreements or concerns
   - Submit revised code for second round
   - Iterate until consensus reached

4. **Synthesis**
   - Human developer reviews all feedback
   - Creates synthesis.md with final decisions
   - Documents what was implemented and why

### Folder Structure

```
reviews/
├── README.md (this file)
├── feature-name/
│   ├── round-1/
│   │   ├── claude-code.md
│   │   ├── grok-code.md
│   │   ├── gemini-code.md
│   │   └── opus-code.md
│   ├── round-2/ (if needed)
│   │   └── (same files)
│   └── synthesis.md
```

### Review Criteria

Each AI evaluates:
- **Correctness**: Does the code do what it's supposed to?
- **Performance**: Execution speed, memory usage
- **Compliance**: Apex rules, WSGTA methodology
- **Safety**: Error handling, edge cases
- **Maintainability**: Code clarity, documentation

### Success Metrics

- All AIs agree on major issues
- No critical bugs missed
- Performance targets met
- Code compiles and tests pass
- Trading rules properly enforced

### When to Use Multi-AI Review

- **Critical Changes**: Order management, risk calculations
- **New Features**: ORB, RMA, Fibonacci implementations
- **Bug Fixes**: Especially performance or compliance issues
- **Architecture Changes**: Memory management, data structures

### Benefits Proven

From V5.3 development:
- Caught Close[0] bug that single AI missed
- Identified memory leak patterns
- Improved Apex compliance checks
- Enhanced error handling for Rithmic disconnects

## Last Updated
January 2025 - V5.3.1 milestone