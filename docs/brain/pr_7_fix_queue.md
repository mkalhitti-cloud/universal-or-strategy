# PR #7 Fix Queue
Generated: 2026-05-31 08:57:57

## Instructions for v12-engineer

Process these issues in priority order. Mark each as FIXED after applying the fix.

### Fix #1 - [P0] CRITICAL
[x] **Bot:** cubic-dev-ai
[x] **File:** src/V12_002.SIMA.Fleet.cs:290
[x] **Issue:** Missing TrackPhotonDequeue() in abort drain loop - FIXED

<details>
<summary>Prompt for AI agents (unresolved issues)</summary>

```text

Check if these issues are valid ÔÇö if so, understand the root cause of each and fix ...

**Action Required:**
1. Read the full finding at: https://github.com/backtothefutures83-oss/universal-or-strategy/pull/7
2. Apply the fix
3. Verify locally
4. Mark as [x] FIXED

---

### Fix #2 - [P0] CRITICAL
[x] **Bot:** amazon-q-developer
[x] **File:** N/A
[x] **Issue:** HALLUCINATION - Positive feedback misclassified as issue - IGNORED

**Implementation Qualit...

**Action Required:**
1. Read the full finding at: https://github.com/backtothefutures83-oss/universal-or-strategy/pull/7
2. Apply the fix
3. Verify locally
4. Mark as [x] FIXED

---

### Fix #3 - [P1] REVIEW
[x] **Bot:** sourcery-ai
[x] **File:** src/V12_002.SIMA.Fleet.cs:337
[x] **Issue:** Duplicate Interlocked.Increment removed - FIXED

- In `VerifyPhotonSlotIntegrity` you both call `TrackPhotonCrcFailure()` and manually `Interlocked.Increment(ref _photonCrcFailures)`, which will either doub...

**Action Required:**
1. Read the full finding at: https://github.com/backtothefutures83-oss/universal-or-strategy/pull/7
2. Apply the fix
3. Verify locally
4. Mark as [x] FIXED

---

### Fix #4 - [P1] REVIEW
[x] **Bot:** coderabbitai
[x] **File:** src/V12_002.SIMA.Dispatch.cs:939,1002
[x] **Issue:** Added TrackPhotonPoolExhausted() and TrackPhotonRingFull() - FIXED

<details>
<summary>­ƒñû Prompt for all review comments with AI agents</summary>

```
Verify each finding against current code. Fix only still-valid issues, skip the
...

**Action Required:**
1. Read the full finding at: https://github.com/backtothefutures83-oss/universal-or-strategy/pull/7
2. Apply the fix
3. Verify locally
4. Mark as [x] FIXED

---

