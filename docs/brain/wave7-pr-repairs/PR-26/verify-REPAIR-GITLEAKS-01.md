## Verification Report -- REPAIR-GITLEAKS-01

**PR**: #26
**Branch**: wave7/pr6-s6-kernel-infra-v2
**Finding**: .gitleaks.toml missing 2 allowlist entries vs main
**Commit Under Review**: 11eef904
**Verifier**: Tier 3 Independent Verifier
**Date**: 2026-07-04

---

### Summary

```
VERIFY_DONE REPAIR-GITLEAKS-01
verification_verdict: FAIL
fix_confirmed: true
build_passed: N/A (no C# files changed)
gate_passed: false
gitleaks_local: NOT_INSTALLED
```

---

### Step 1 -- Source Truth Check

Read `/tmp/wt-pr25-clean/.gitleaks.toml` directly.

**Entry A -- firebase-credentials.json.revoked** (line 45):
```toml
[[allowlists]]
description = "Exclude gitleaks report files and Firebase credentials (gitignored)"
paths = [
    '''gitleaks_report\.json$''',
    '''firebase-credentials\.json$''',
    '''firebase-credentials\.json\.revoked$''',     <-- PRESENT
    '''.*firebase-adminsdk.*\.json$'''
]
```
Status: **CONFIRMED PRESENT**

**Entry B -- EPIC-W7-114 architecture plan** (lines 49-51):
```toml
[[allowlists]]
description = "Allow EPIC-W7-114 architecture plan (false positive: jcodemunch call hierarchy text)"
paths = ['''docs/brain/EPIC-W7-114/02-architecture-plan\.md$''']
```
Status: **CONFIRMED PRESENT**

`fix_confirmed: true` -- both entries are present exactly as planned.

---

### Step 2 -- Prepush Gate

Command: `python3 scripts/wave7_prepush_gate.py --base origin/main`
CWD: `/tmp/wt-pr25-clean`

```
wave7_prepush_gate: branch=wave7/pr6-s6-kernel-infra-v2 checking 3 modified src/ file(s) vs origin/main

[FAIL] Check 0 -- CS-only (non-.cs files in diff):
  .gitleaks.toml -- non-.cs file in diff (move to a separate docs/* or chore/* PR)
  ACTION: Remove non-.cs files from this branch.
  Move docs/scripts/configs to a separate chore/* PR.

[PASS] Check 1 -- ASCII-only
[PASS] Check 2 -- DateTime.Now (none introduced)
[PASS] Check 3 -- lock() (none found)
[PASS] Check 4 -- underscore locals (none found)
[PASS] Check 5 -- diff size (2,885 raw / 2,885 stripped, under 150,000 limit)

GATE FAILED -- 1 blocking violation(s). Fix before pushing.
```

`gate_passed: false`

**Root Cause**: The gate's Check 0 (CS-only diff enforcement) fires because the branch
`wave7/pr6-s6-kernel-infra-v2` contains BOTH:
- `src/*.cs` files from earlier commits (REPAIR-01, REPAIR-ASCII-01)
- `.gitleaks.toml` from commit 11eef904 (REPAIR-GITLEAKS-01)

The gate sees the full branch diff vs `origin/main` and correctly flags that a
non-.cs config file (`.gitleaks.toml`) is co-mingled with C# source changes.
Per OKF / PR hygiene rules, config/chore changes must be on a SEPARATE branch from src/ changes.

---

### Step 3 -- Commit Verification

```
git -C /tmp/wt-pr25-clean log --oneline -5
11eef904 fix(wave7/pr26): REPAIR-GITLEAKS-01 -- sync .gitleaks.toml allowlist with main (firebase.revoked + EPIC-W7-114)
750bf0fe fix(wave7/pr26): REPAIR-ASCII-01 -- replace em dashes with ASCII double hyphen in comments
48ce82e7 fix(wave7/s6-kernel-infra): REPAIR-01 DrawingHelpers UTC case + LogBuffer literal brace -- clean CS-only branch
...
```

Commit 11eef904 is present and is the HEAD. **Confirmed.**

---

### Step 4 -- Scope Check (no src/ changes)

```
git -C /tmp/wt-pr25-clean show 11eef904 --name-only
-- only .gitleaks.toml was changed (1 file, 5 insertions)
```

Commit 11eef904 itself is clean -- only `.gitleaks.toml` modified, zero `src/` changes.
The gate failure is a **branch-level** issue (prior commits on the same branch contain `src/` changes),
not an issue with commit 11eef904 in isolation.

---

### Step 5 -- Gitleaks Local Scan

`gitleaks` binary: **NOT_INSTALLED** on this machine. Scan skipped.

---

### OKF Rule Checks

| Rule | Check | Result |
|------|-------|--------|
| lock() banned | No C# in this commit | N/A |
| ASCII-only | Gate Check 1 passed | PASS |
| DateTime.Now banned | Gate Check 2 passed | PASS |
| No underscore locals | Gate Check 4 passed | PASS |
| CYC <= 8 | No C# in this commit | N/A |
| NUnit/MSTest banned | No tests in this commit | N/A |
| CS-only diff (Check 0) | .gitleaks.toml on same branch as src/ changes | **FAIL** |

---

### Semantic Analysis (3 thoughts)

**Thought 1 -- Does the bug fix address the described finding?**
Yes. Both missing allowlist entries are now present in `.gitleaks.toml`:
- `firebase-credentials.json.revoked` added to the existing firebase credentials allowlist block
- A new `[[allowlists]]` block added for `docs/brain/EPIC-W7-114/02-architecture-plan.md`
The fix correctly patches the two false positives that were missing.

**Thought 2 -- Does the fix satisfy relevant OKF rules?**
The content of the fix is sound -- no secrets introduced, no ASCII violations, no src/ modifications.
However, the gate's CS-only check (Check 0) fires because the fix was committed onto a branch
that already carries `src/` C# changes from earlier repair commits. The branch mix violates the
PR hygiene mandate: chore/config changes must not co-mingle with src/ changes.

**Thought 3 -- Could the fix introduce a regression or new violation?**
The `.gitleaks.toml` changes are additive allowlist entries only. No existing entries were
removed or modified. The EPIC-W7-114 path regex is properly anchored (`$`). The firebase
`.revoked` entry is correctly placed inside the existing firebase credentials block. No
regression risk from the content itself. The only concern is the gate failure from branch
co-mingling.

---

### Verdict

**verification_verdict: FAIL**

The fix content is correct (`fix_confirmed: true`) -- both allowlist entries are present
and syntactically valid. However, the **mandatory prepush gate FAILED** (Check 0: CS-only
enforcement). This is a V12 PR Hygiene violation per `docs/protocol/00-pr-hygiene.md`:

> Config/chore changes (.gitleaks.toml) must be on a SEPARATE branch/PR from src/ C# changes.

**Required Action**: The `.gitleaks.toml` repair should be committed on a dedicated
`chore/gitleaks-allowlist-sync` branch (no `src/` files), then merged separately.
The gate will pass once `.gitleaks.toml` is isolated from the C# repair commits.

---

### Files Verified

- `/tmp/wt-pr25-clean/.gitleaks.toml` (read directly)
- `git show 11eef904 --name-only` (scope confirmed)
- `git log --oneline -5` (commit presence confirmed)
- `python3 scripts/wave7_prepush_gate.py --base origin/main` (gate result)
