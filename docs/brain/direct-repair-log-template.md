# /direct Session Repair Log -- TEMPLATE
#
# Copy this file to docs/brain/NO-PIPELINE-REPAIRS.md for each session.
# Each entry below is one hotfix applied without the full PTT pipeline.
# Status lifecycle: APPLIED -> TESTED-PASS | TESTED-FAIL -> pipeline run -> PIPELINE-COMPLETE
#
# Statuses:
#   APPLIED           -- edit done, not yet tested in NT8
#   TESTED-PASS       -- confirmed working in NT8 live test
#   TESTED-FAIL       -- tested, did not fix the bug, follow-up required
#   PIPELINE-PENDING  -- tested and passing, pipeline run not yet started
#   PIPELINE-COMPLETE -- full Ph1->Ph5 pipeline run completed for this block

---

## SESSION: {DATE} -- {BRIEF DESCRIPTION}

### Parallel Lane Map

| Lane | Engineer Mode        | File           | Method              | HOTFIX-ID                  | Status           |
|------|----------------------|----------------|---------------------|----------------------------|------------------|
| L1   | ptt-direct-engineer  | CopyEngine.cs  | {MethodName}        | HOTFIX-{TOPIC}-01          | APPLIED          |
| L2   | ptt-direct-engineer  | CopyEngine.cs  | {MethodName}        | HOTFIX-{TOPIC}-02          | APPLIED          |
| L3   | ptt-direct-engineer  | {File}.cs      | {MethodName}        | HOTFIX-{TOPIC}-03          | NOT-STARTED      |

Sync rule: {ONE sync after all lanes confirm edits done | each lane syncs independently}

---

## HOTFIX-{TOPIC}-01

**Date**: {DATE}
**File**: `src/PropTraderTools/{File}.cs`
**Method**: `{MethodName}` (line ~{N})
**Status**: APPLIED -- awaiting pipeline

### Bug
{One sentence: what was wrong, evidence from logs or code}

### Root Cause
{One sentence: exact line and reason why it failed}

### Fix
{One sentence: what was changed and why it works -- reference the working pattern used}

### Working Reference Pattern
{e.g. PttBreakEven.ExecuteOneAccount -- CancelStaleBracketsLocal + SubmitBeTargetsLocal}

### Diff (minimal)
```diff
- {old line(s)}
+ {new line(s)}
```

### Test Result
- [ ] PASS -- `[MSTBE] {expected log message}` seen in Output Tab 1
- [ ] PASS -- {expected behavior in Orders tab}
- [ ] FAIL -- paste failure output here

### JS-DNA Compliance
- No `lock()` added: {YES/NO}
- No `throw new` added: {YES/NO}
- No `return null` added: {YES/NO}
- No `async void` added: {YES/NO}
- ASCII-only: {YES/NO}

### Pipeline Work Needed
- Ph1 Architecture: {document the design decision made by this fix}
- Ph4a Engineer: {confirm fix survives complexity audit -- CYC <= 8}
- Ph4b Verifier: {re-run all 7 scans}

---

## HOTFIX-{TOPIC}-02

**Date**: {DATE}
**File**: `src/PropTraderTools/{File}.cs`
**Method**: `{MethodName}` (line ~{N})
**Status**: APPLIED -- awaiting pipeline

### Bug
{One sentence}

### Root Cause
{One sentence}

### Fix
{One sentence}

### Diff (minimal)
```diff
- {old line(s)}
+ {new line(s)}
```

### Test Result
- [ ] PASS
- [ ] FAIL

### JS-DNA Compliance
- No `lock()` added: YES
- No `throw new` added: YES
- No `return null` added: YES
- No `async void` added: YES
- ASCII-only: YES

---
# END OF SESSION LOG
