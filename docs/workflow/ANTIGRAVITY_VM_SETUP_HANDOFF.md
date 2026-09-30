# Antigravity VM Setup Handoff - Test VM Configuration

**Context**: Bob has created a golden image (`v12-bob-shell-golden-v1`) with Bob Shell 1.0.4 pre-installed. A test VM has been launched but needs terminal-based setup before the epic test can run.

**Your Mission**: Configure the test VM environment and run a single epic test to validate the golden image.

## Current Status

✅ **Completed by Bob**:
- Golden image created: `v12-bob-shell-golden-v1`
- Test VM launched: `v12-test-epic-164` (IP: 162.222.180.242)
- Bob Shell v1.0.4 pre-installed on image

⏸️ **Blocked**: SSH host key confirmation needed (interactive terminal input)

## Your Tasks

### Task 1: Verify VM Environment (2 minutes)

Run this command and accept the SSH host key when prompted (type `y`):

```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'bob --version && git --version && python3 --version'"
```

**Expected Output**:
```
1.0.4
git version 2.x.x
Python 3.12.x
```

If you see this output, the golden image is working correctly! ✅

### Task 2: Clone Repository (2 minutes)

```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~ && git clone https://github.com/malhitticrypto-debug/universal-or-strategy.git && cd universal-or-strategy && git config user.email 'malhitticrypto@gmail.com' && git config user.name 'malhitticrypto'"
```

### Task 3: Run Single Epic Test (15-20 minutes)

```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && bob /epic-intake EPIC-CCN-164"
```

**What This Does**:
- Runs Phase 0-6 of the V12 epic workflow
- Tests: EPIC-CCN-164 (Complexity: 21, Low risk)
- Validates the golden image can execute a complete epic

### Task 4: Monitor Progress (Every 5 minutes)

Check epic status:
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && cat docs/brain/EPIC-CCN-164/manifest.json"
```

Look for phase statuses: `pending`, `in_progress`, `completed`, `failed`

### Task 5: Verify Completion (After ~20 minutes)

Check final report:
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && cat docs/brain/EPIC-CCN-164/05-completion-report.md"
```

## Success Criteria

✅ **Environment Verified**: Bob Shell, Git, Python all working  
✅ **Repo Cloned**: universal-or-strategy cloned successfully  
✅ **Epic Completed**: All phases (0-6) show `completed` in manifest  
✅ **Quality Gates Passed**: No compilation errors, complexity reduced  

## If Test Succeeds

**Report back to me with**:
1. ✅ "Test VM validation successful"
2. Final completion report content
3. Any warnings or issues encountered

**Then I will**:
- Document the success
- Launch Wave 2 (10 parallel VMs for remaining epics)
- Monitor full Wave 2 execution

## If Test Fails

**Report back with**:
1. ❌ Which phase failed
2. Error messages from logs
3. Manifest.json content

**Then I will**:
- Analyze the failure
- Fix the golden image (create v2)
- Retry the test

## Troubleshooting

### Issue: "bob: command not found"
**Fix**: PATH not loaded. Try:
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'source ~/.bashrc && bob --version'"
```

### Issue: Git clone fails
**Fix**: Authentication issue. Check GitHub credentials.

### Issue: Epic hangs
**Fix**: Check logs:
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && tail -50 docs/brain/EPIC-CCN-164/*.md"
```

## VM Details

- **Name**: v12-test-epic-164
- **Zone**: us-central1-a
- **IP**: 162.222.180.242
- **Machine**: n2-standard-8 (8 vCPUs, 32GB RAM)
- **Image**: v12-bob-shell-golden-v1
- **Cost**: ~$0.08/hour (SPOT instance)

## Timeline

| Task | Duration | Cumulative |
|------|----------|------------|
| Verify environment | 2 min | 2 min |
| Clone repo | 2 min | 4 min |
| Run epic test | 15-20 min | 19-24 min |
| Verify results | 2 min | 21-26 min |

**Total**: ~25 minutes

## Handoff Back to Bob

Once you've completed the test (success or failure), paste your results and I'll take over for:
- Analyzing results
- Documenting findings
- Launching Wave 2 (if test passed)
- Debugging and retry (if test failed)

---

**Ready to start? Run Task 1 first!** 🚀