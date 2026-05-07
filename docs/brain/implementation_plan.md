# MISSION: B985 Phase 5 Distributed Pipeline Forensic Repair
**REPO:** universal-or-strategy
**BUILD TAG:** B985-CI-REPAIR
**BRANCH:** Current

## 1. STRATEGIC ANALYSIS & PROOF OF FAILURE
**Qwen & GLM (OpenCode) Failure:**
The `anomalyco/opencode` action expects a rigid custom provider configuration structure in `opencode.json`. Furthermore, local testing confirms that the provided "free tier" API keys for DashScope (Qwen) and Zhipu (GLM) are returning HTTP 401 (Invalid Key) and HTTP 429 (Insufficient Balance) errors. Per Director's orders, since these models fail to operate on the free tier, their respective workflows will be purged to clean the CI pipeline.

**Jules AI (Sovereign Auditor) Failure:**
The script in `.github/workflows/jules-pr-review.yml` has a hardcoded timeout of ~20 minutes (`maxAttempts = 40` at `30000`ms intervals). Jules forensic audits, particularly those traversing entire branches via `githubRepoContext`, can exceed this window and are being prematurely terminated. Since the Jules account is Pro-tier and active, repairing this is the primary focus.

## 2. STRUCTURAL REPAIR PLAN

### Phase 1: Purge Broken "Free" Workflows
We will delete the workflows and configurations for the AI models that are failing authentication/billing checks.
1. **Remove `opencode.json`**: Delete the file from the repository root.
2. **Remove `.github/workflows/qwen-review.yml`**: Delete the Qwen workflow.
3. **Remove `.github/workflows/glm-review.yml`**: Delete the GLM workflow.

### Phase 2: Jules AI Timeout Mitigation
1. **Modify `.github/workflows/jules-pr-review.yml`**:
   - Change the polling interval from `30000`ms to `60000`ms (60 seconds).
   - Increase `maxAttempts` from `40` to `60` (providing a 60-minute timeout window at 60 seconds per poll).
   - This prevents the premature termination of deep architectural audits while reducing API spam.

## 3. BMad V12 DNA & ASCII COMPLIANCE
- All edits to the YAML files will strictly adhere to the ASCII-only string requirement. 
- There will be no `lock()` statements or non-ASCII characters introduced in any of the workflow scripts.

## 4. DIRECTOR'S HANDOFF BLOCK (For P5 ENGINEER)

```text
@ENGINEER (Codex/Jules) - P5 Surgical Execution

Please execute the following structural repairs:

1. Delete the following files from the repository to purge the broken CI pipelines:
   - `opencode.json`
   - `.github/workflows/qwen-review.yml`
   - `.github/workflows/glm-review.yml`
2. In `.github/workflows/jules-pr-review.yml`, update the `maxAttempts` variable in the polling loop to `60` and change the `setTimeout` interval from `30000` to `60000` (60 seconds) to allow for a 60-minute timeout window with less aggressive polling.
3. Once edits are complete, run `powershell -File .\deploy-sync.ps1` and verify the ASCII gate passes.
```