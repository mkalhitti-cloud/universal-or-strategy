# Implementation Plan Template

Use this exact structure when writing `implementation_plan.md`.

---

````markdown
# Implementation Plan -- [Mission Name]

## Root Cause

[Your independent Logical Proof of Failure -- one paragraph, your own words]

## Proposed Changes

---

### [Component Name]

#### [MODIFY] [FileName.cs](file:///absolute/path/to/FileName.cs)

[One sentence describing what changes and why]

**Before** (lines N-M):

```csharp
// exact original code
```
````

**After**:

```csharp
// complete replacement code -- no ..., no pseudocode
// must be copy-paste ready
```

#### [NEW] [NewFile.cs](file:///absolute/path/to/NewFile.cs)

```csharp
// complete file content
```

#### [DELETE] [OldFile.cs](file:///absolute/path/to/OldFile.cs)

---

## DNA Compliance Checklist

- [ ] No lock(stateLock) introduced
- [ ] All new C# strings are ASCII-only
- [ ] Enqueue used for state mutations (or Direct Write justified per Build 981)
- [ ] FSM guard lines present if follower orders are touched
- [ ] grep -r "lock(stateLock)" src/ -- 0 results in modified files
- [ ] python check_ascii.py src/[modified files] -- passes

## Verification Plan

### Automated

- `grep -r "lock(stateLock)" src/` -- must return 0 results
- `python check_ascii.py src/[file1] src/[file2]` -- must pass
- `grep -r "PendingCancel\|Submitting" src/` -- required if FSM touched

### Post-Edit Deployment (mandatory)

- `powershell -File .\deploy-sync.ps1` -- re-establishes hard links broken by file-edit tools
- Tell Director: "Press F5 in NinjaTrader to compile"
- Verify banner shows new BUILD_TAG

### Manual (Director)

- Recompile in NinjaTrader 8
- [Specific behavior to verify]

```

---

## Rules for Writing the Plan

1. **No partial code blocks.** Every `Before/After` must show the complete method or block.
2. **No `...` omissions.** If you can't fit it, break it into a separate change entry.
3. **No "the Engineer should determine..."** -- you determine it here. They copy-paste.
4. **Before/After pairs are preferred** to diffs for clarity with partial-class C# files.
```
