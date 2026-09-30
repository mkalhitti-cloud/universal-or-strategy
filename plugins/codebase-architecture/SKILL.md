name: codebase-architecture
description: Surface architectural friction and propose module-deepening refactors that turn shallow modules into deep ones. Applies John Ousterhout's "deep module" principle for testability and AI-navigability. Complements architecture-validation by focusing on organic exploration and interface design rather than metric validation.
version: 1.0.0
status: active
maintainer: V12 Architecture Team
last_updated: 2026-06-08

---

# Codebase Architecture Improvement Skill

## Purpose

Surface architectural friction and propose **deepening opportunities** - refactors that turn shallow modules into deep ones. The aim is testability, AI-navigability, and cognitive simplicity aligned with V12 DNA.

**Relationship to `architecture-validation`**:
- **This skill**: Organic exploration, interface design, deepening opportunities (BEFORE planning)
- **`architecture-validation`**: Metric validation, coupling analysis, layer violations (DURING planning)
- **Use together**: Run this skill first to identify candidates, then use `architecture-validation` to validate the chosen approach

## Core Principles (John Ousterhout's "Deep Modules")

### Glossary

Use these terms exactly in every suggestion. Consistent language prevents drift into "component," "service," "API," or "boundary."

- **Module** - anything with an interface and an implementation (function, class, package, slice)
- **Interface** - everything a caller must know to use the module: types, invariants, error modes, ordering, config (not just the type signature)
- **Implementation** - the code inside
- **Depth** - leverage at the interface: a lot of behaviour behind a small interface
  - **Deep** = high leverage (small interface, large implementation)
  - **Shallow** = interface nearly as complex as the implementation
- **Seam** - where an interface lives; a place behaviour can be altered without editing in place (use this, not "boundary")
- **Adapter** - a concrete thing satisfying an interface at a seam
- **Leverage** - what callers get from depth
- **Locality** - what maintainers get from depth: change, bugs, knowledge concentrated in one place

### Key Principles

1. **Deletion test**: Imagine deleting the module. If complexity vanishes, it was a pass-through. If complexity reappears across N callers, it was earning its keep.
2. **The interface is the test surface**
3. **One adapter = hypothetical seam. Two adapters = real seam**

### V12 DNA Alignment

This skill enforces V12 architectural mandates:

- **Correctness by Construction**: Deep modules make illegal states unrepresentable by hiding complexity behind small, clear interfaces
- **CYC ≤ 15**: Shallow modules often indicate over-extraction; deep modules concentrate logic for easier reasoning
- **Jane Street Cognitive Simplicity**: Small interfaces reduce cognitive load; locality concentrates knowledge
- **Lock-Free Actor Pattern**: Deep modules can encapsulate FSM/Actor patterns behind simple interfaces
- **ASCII-Only Compliance**: All generated code and documentation must use ASCII-only characters

## When to Use

### Mandatory (Run BEFORE epic planning)
- Starting a new refactoring epic (EPIC-8 through EPIC-14)
- God-file splitting (CYC > 20)
- Identifying untested seams
- Consolidating tightly-coupled modules
- Improving AI-navigability of complex subsystems

### Optional
- Simple single-method extractions
- Cosmetic refactoring
- No architectural changes

## Process

### Phase 1: Organic Exploration

**Prerequisites**:
1. Read `CONTEXT.md` (domain glossary) if it exists
2. Read relevant ADRs in `docs/adr/` for the area you're touching
3. Index the repository with jCodemunch if not already done

**Exploration Strategy** (use jCodemunch tools):

```bash
# Start with repo overview
jcodemunch get_repo_outline --repo universal-or-strategy

# Identify hotspots (high complexity + high churn)
jcodemunch get_hotspots --repo universal-or-strategy --top_n 20 --days 90

# For each hotspot, explore organically
jcodemunch get_file_outline --repo universal-or-strategy --file_path src/V12_002.cs
jcodemunch search_symbols --repo universal-or-strategy --query "position management" --kind function
jcodemunch find_references --repo universal-or-strategy --identifier UpdatePositionInfo
```

**Friction Signals** (what to look for):

- **Shallow modules**: Interface nearly as complex as implementation
  - Example: `CalculateRisk(price, volume, volatility, beta, correlation, ...)` with 8 parameters doing simple math
- **Tight coupling**: Modules that leak across their seams
  - Example: `OrderManager` directly accessing `PositionTracker._internalState`
- **Untested seams**: Logic hard to test through current interface
  - Example: Pure functions extracted for testability, but real bugs hide in how they're called
- **Bouncing between modules**: Understanding one concept requires reading many small files
  - Example: Order lifecycle spread across `OrderValidator`, `OrderEnricher`, `OrderDispatcher`, `OrderLogger`
- **Pass-through modules**: Modules that just forward calls without adding value
  - Apply **deletion test**: Would deleting it concentrate complexity or just move it?

**V12-Specific Friction**:
- Lock-based synchronization where FSM/Actor would be cleaner
- God-functions (CYC > 20) that should be deep modules
- Magic numbers/strings that should be enums or constants
- Allocation-heavy code that could use object pools

### Phase 2: Generate HTML Report

**Output Location**: `$TMPDIR/architecture-review-<timestamp>.html` (never commit to repo)

**Report Structure**:

```html
<!DOCTYPE html>
<html>
<head>
  <script src="https://cdn.tailwindcss.com"></script>
  <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
  <title>Architecture Review - Universal OR Strategy</title>
</head>
<body class="bg-gray-900 text-gray-100">
  <div class="container mx-auto px-4 py-8">
    <h1 class="text-4xl font-bold mb-8">Architecture Review</h1>
    
    <!-- For each candidate -->
    <div class="bg-gray-800 rounded-lg p-6 mb-6 border-l-4 border-blue-500">
      <div class="flex justify-between items-start mb-4">
        <h2 class="text-2xl font-semibold">{Candidate Title}</h2>
        <span class="px-3 py-1 rounded text-sm font-mono bg-green-500/20 text-green-400">
          Strong | Worth exploring | Speculative
        </span>
      </div>
      
      <div class="grid grid-cols-2 gap-6 mb-6">
        <div>
          <h3 class="text-lg font-semibold mb-2">Files Involved</h3>
          <ul class="list-disc list-inside text-gray-300">
            <li>src/V12_002.cs (CYC 89)</li>
            <li>src/V12_002.PositionInfo.cs (new)</li>
          </ul>
        </div>
        
        <div>
          <h3 class="text-lg font-semibold mb-2">Problem</h3>
          <p class="text-gray-300">
            Position management logic is shallow - 12 public methods with complex signatures,
            but implementation is straightforward state tracking. Callers must understand
            internal state machine.
          </p>
        </div>
      </div>
      
      <div class="mb-6">
        <h3 class="text-lg font-semibold mb-2">Solution</h3>
        <p class="text-gray-300">
          Extract to deep PositionTracker module with 3-method interface:
          Open(order), Update(fill), Close(). Hide state machine internally.
        </p>
      </div>
      
      <div class="mb-6">
        <h3 class="text-lg font-semibold mb-2">Benefits</h3>
        <ul class="list-disc list-inside text-gray-300">
          <li><strong>Leverage</strong>: Callers use 3 methods instead of 12</li>
          <li><strong>Locality</strong>: State machine bugs isolated to one module</li>
          <li><strong>Testability</strong>: Interface is the test surface (3 methods vs 12)</li>
          <li><strong>V12 DNA</strong>: FSM pattern makes illegal states unrepresentable</li>
        </ul>
      </div>
      
      <div class="grid grid-cols-2 gap-6">
        <div>
          <h3 class="text-lg font-semibold mb-2">Before (Shallow)</h3>
          <div class="mermaid">
            graph TD
              A[Caller] -->|12 methods| B[PositionManager]
              B --> C[Internal State]
              style B fill:#ef4444
          </div>
        </div>
        
        <div>
          <h3 class="text-lg font-semibold mb-2">After (Deep)</h3>
          <div class="mermaid">
            graph TD
              A[Caller] -->|3 methods| B[PositionTracker]
              B --> C[FSM Actor]
              C --> D[State Machine]
              style B fill:#10b981
          </div>
        </div>
      </div>
      
      <!-- ADR conflict warning if applicable -->
      <div class="mt-4 p-4 bg-yellow-500/10 border border-yellow-500/30 rounded">
        <p class="text-yellow-400">
          ⚠️ Contradicts ADR-0007 (Keep position logic in main file) - but worth reopening
          because current approach has 89 CYC and is untestable.
        </p>
      </div>
    </div>
    
    <!-- Top Recommendation Section -->
    <div class="bg-blue-900/30 border border-blue-500/50 rounded-lg p-6 mt-8">
      <h2 class="text-2xl font-bold mb-4">Top Recommendation</h2>
      <p class="text-gray-300">
        Start with <strong>PositionTracker extraction</strong> because:
        1. Highest CYC reduction (89 → 15)
        2. Enables FSM/Actor pattern (V12 DNA alignment)
        3. Unblocks testing (currently 0% coverage)
      </p>
    </div>
  </div>
  
  <script>
    mermaid.initialize({ startOnLoad: true, theme: 'dark' });
  </script>
</body>
</html>
```

**Report Generation**:

```bash
# Resolve temp directory
$TMPDIR = if ($env:TMPDIR) { $env:TMPDIR } elseif ($env:TEMP) { $env:TEMP } else { "/tmp" }
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = "$TMPDIR/architecture-review-$timestamp.html"

# Write HTML content
Set-Content -Path $reportPath -Value $htmlContent

# Open in browser
if ($IsWindows) {
  Start-Process $reportPath
} elseif ($IsMacOS) {
  open $reportPath
} else {
  xdg-open $reportPath
}

Write-Host "Report generated: $reportPath"
```

**Important**: Do NOT propose interfaces yet. After generating the report, ask the user:

> "Which of these candidates would you like to explore?"

### Phase 3: Grilling Loop

Once the user picks a candidate, enter a grilling conversation to walk the design tree:

**Questions to Explore**:
1. **Constraints**: What invariants must the module maintain?
2. **Dependencies**: What does it depend on? What depends on it?
3. **Interface shape**: What's the minimal interface that provides maximum leverage?
4. **Seam location**: Where should the interface live?
5. **Test strategy**: How will we test through the interface?
6. **V12 DNA**: Does this align with FSM/Actor, CYC ≤ 15, zero-allocation?

**Side Effects During Grilling**:

1. **New domain concept not in `CONTEXT.md`?**
   - Add it immediately (same discipline as `/grill-with-docs`)
   - Format: `## {Term}\n{Definition}\n{Why it matters}\n`
   - Create `CONTEXT.md` lazily if it doesn't exist

2. **Sharpening a fuzzy term?**
   - Update `CONTEXT.md` inline

3. **User rejects with load-bearing reason?**
   - Offer ADR: "Want me to record this as an ADR so future architecture reviews don't re-suggest it?"
   - Only offer when reason would prevent future re-suggestion
   - Skip ephemeral reasons ("not worth it right now") and self-evident ones

4. **Exploring alternative interfaces?**
   - Generate 3-5 radically different designs:
     - **Minimalist**: Fewest methods possible
     - **Flexible**: More methods, more control
     - **Caller-optimized**: Interface shaped by caller needs
     - **Ports & Adapters**: Explicit seams for testing
   - Present trade-offs for each
   - Recommend strongest approach

**Example Grilling Session**:

```
Agent: "For PositionTracker, what invariants must it maintain?"

User: "Position quantity must match sum of fills. Can't close with open orders."

Agent: "Got it. Adding to CONTEXT.md:

## Position Invariants
- Quantity = sum of all fills
- Cannot close position while orders are open
- Matters because: Violating these causes accounting errors

Should the interface enforce these at compile-time or runtime?"

User: "Runtime - fills come from external system."

Agent: "Understood. So we need:
1. Open(order) -> PositionId
2. Update(positionId, fill) -> Result<Unit, PositionError>
3. Close(positionId) -> Result<Unit, PositionError>

This gives callers leverage (3 methods) and locality (invariants checked in one place).

Alternative: Could expose GetState(positionId) for debugging. Trade-off?"

User: "Yes, add GetState for diagnostics."

Agent: "Done. Interface now:
- Open/Update/Close (core operations)
- GetState (diagnostics only)

This is a deep module: small interface (4 methods), large implementation (FSM + validation).

Ready to document this in an ADR?"
```

## V12 DNA Alignment

### Correctness by Construction
- **Deep modules make illegal states unrepresentable**: Small interfaces enforce invariants
- **Deletion test prevents over-abstraction**: Only extract when complexity concentrates

### CYC ≤ 15
- **Shallow modules indicate over-extraction**: Too many small functions increase cognitive load
- **Deep modules concentrate logic**: Easier to reason about, test, and maintain

### Jane Street Cognitive Simplicity
- **Small interfaces reduce cognitive load**: Callers learn 3 methods instead of 12
- **Locality concentrates knowledge**: Bugs and changes isolated to one module

### Lock-Free Actor Pattern
- **Deep modules can encapsulate FSM/Actor**: Hide state machine behind simple interface
- **Seams enable testing**: Mock the interface, not the implementation

### Zero-Allocation
- **Deep modules can use object pools internally**: Callers don't need to know
- **Interface design affects allocation**: Consider `ref` parameters, `Span<T>`, etc.

## Integration with Existing Skills

### Relationship to `architecture-validation`

**Workflow**:
1. **This skill** (codebase-architecture): Identify deepening opportunities
2. **User decision**: Pick a candidate
3. **This skill**: Grilling loop to design interface
4. **`architecture-validation`**: Validate coupling metrics, layer violations, blast radius
5. **Implementation**: Execute the refactor

**Example**:
```bash
# Step 1: Identify candidates
# (This skill generates HTML report)

# Step 2: User picks "PositionTracker extraction"

# Step 3: Grilling loop designs interface
# (This skill updates CONTEXT.md, proposes ADR if needed)

# Step 4: Validate architecture
jcodemunch get_coupling_metrics --repo universal-or-strategy --module_path src/V12_002.cs
jcodemunch get_blast_radius --repo universal-or-strategy --symbol UpdatePositionInfo
# (architecture-validation skill documents in 03-architecture.md)

# Step 5: Implement
# (v12-engineer mode executes extraction)
```

### Relationship to `scope-boundary-check`

**Complementary**:
- **`scope-boundary-check`**: Prevents scope creep during implementation
- **This skill**: Identifies scope BEFORE implementation

**Use together**: Run this skill to define scope, then use `scope-boundary-check` to enforce it.

## Success Criteria

- [ ] HTML report generated with 3-5 candidates
- [ ] Each candidate has before/after visualization
- [ ] Top recommendation identified with rationale
- [ ] User picks a candidate
- [ ] Grilling loop completes with clear interface design
- [ ] `CONTEXT.md` updated with new domain concepts (if any)
- [ ] ADR offered if user rejects with load-bearing reason
- [ ] Interface design aligns with V12 DNA (CYC ≤ 15, FSM/Actor, zero-allocation)

## Post-Use Audit

After using this skill:

1. **Check if exploration surfaced real friction**:
   - Did it identify shallow modules?
   - Did it catch tight coupling?
   - Did it find untested seams?

2. **Verify interface design quality**:
   - Is the interface small and clear?
   - Does it provide leverage to callers?
   - Does it provide locality to maintainers?
   - Does it align with V12 DNA?

3. **Update this skill if gaps are found**:
   - Add new friction signals
   - Refine HTML report template
   - Add examples from real epics

4. **Report outcome**:
   - `skill(codebase-architecture): no gaps identified` if successful
   - `skill(codebase-architecture): gap found - {description}` if issues detected

## Known Quirks

### Quirk 1: HTML Report May Not Open Automatically
**Symptom**: Report generated but browser doesn't open
**Workaround**: Manually open the file path printed to console
**Root Cause**: Platform-specific `open` command differences

### Quirk 2: Mermaid Diagrams May Not Render
**Symptom**: Blank spaces where diagrams should be
**Workaround**: Check browser console for Mermaid errors; fallback to hand-drawn SVG
**Root Cause**: Mermaid syntax errors or CDN loading issues

### Quirk 3: CONTEXT.md May Not Exist
**Symptom**: Skill tries to read `CONTEXT.md` but file doesn't exist
**Workaround**: Create file lazily with header: `# Domain Glossary\n\n`
**Root Cause**: New projects may not have domain documentation yet

## Examples

### Example 1: God-File Splitting (EPIC-8)

**Scenario**: `V12_002.cs` has CYC 89, untestable, 3000+ lines

**Exploration**:
```bash
jcodemunch get_hotspots --repo universal-or-strategy --top_n 1
# Result: V12_002.cs (CYC 89, 47 commits in 90 days)

jcodemunch get_file_outline --repo universal-or-strategy --file_path src/V12_002.cs
# Result: 89 methods, 12 related to position management

jcodemunch find_references --repo universal-or-strategy --identifier UpdatePositionInfo
# Result: Called from 8 locations, all in V12_002.cs (tight coupling)
```

**Candidates Identified**:
1. **PositionTracker** (Strong): Extract 12 position methods → 3-method interface
2. **OrderLifecycle** (Worth exploring): Extract 15 order methods → 5-method interface
3. **RiskCalculator** (Speculative): Extract 8 risk methods → 2-method interface

**User Picks**: PositionTracker

**Grilling Loop**:
- Interface: `Open(order)`, `Update(fill)`, `Close()`
- Invariants: Quantity = sum of fills, can't close with open orders
- V12 DNA: Use FSM/Actor pattern, CYC ≤ 15 per method
- Testing: Mock interface, test state transitions

**Outcome**: Clear interface design, ready for `architecture-validation`

### Example 2: Untested Seam (EPIC-12)

**Scenario**: Order callbacks have complex logic but no tests

**Exploration**:
```bash
jcodemunch search_symbols --repo universal-or-strategy --query "OnOrderUpdate" --kind method
# Result: 5 methods handling different order states

jcodemunch get_untested_symbols --repo universal-or-strategy --file_pattern "src/V12_002.Orders.Callbacks.*.cs"
# Result: All 5 methods unreached by tests
```

**Friction Signal**: Pure functions extracted for testability, but real bugs hide in how they're called (no locality)

**Candidate**: Extract `OrderStateMachine` with `Transition(event)` interface

**Grilling Loop**:
- Interface: `Transition(OrderEvent) -> Result<OrderState, OrderError>`
- Seam: Interface allows mocking for tests
- V12 DNA: FSM pattern, makes illegal transitions unrepresentable

**Outcome**: Testable seam identified, ready for implementation

## References

- **Matt Pocock**: "Improve Codebase Architecture" (source: skills.sh)
- **John Ousterhout**: "A Philosophy of Software Design" (deep modules principle)
- **V12 DNA**: `AGENTS.md` (correctness by construction, CYC ≤ 15)
- **Jane Street Intel**: `docs/intel/jane-street/` (cognitive simplicity)
- **jCodemunch Tools**: Used for organic exploration
- **architecture-validation skill**: Complementary metric validation

## Version History

- **V1.0.0** (2026-06-08): Initial creation, adapted from Matt Pocock's skill with V12 DNA alignment