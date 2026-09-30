# Firebase Credentials Reference

**Date**: 2026-06-08T22:13:00Z  
**Status**: ✅ ACTIVE

---

## Credentials Location

**File**: `firebase-credentials.json`  
**Path**: Repository root (`c:/WSGTA/universal-or-strategy/firebase-credentials.json`)  
**Source**: `C:\Users\Mohammed Khalid\Downloads\v12-morpheus-firebase-adminsdk-fbsvc-e1de2ed749.json`

**Project**: v12-morpheus  
**Service Account**: firebase-adminsdk-fbsvc@v12-morpheus.iam.gserviceaccount.com

---

## Verification

**Test Command**:
```bash
python scripts/query_kb.py "concurrency"
```

**Expected Output**:
```
[*] Querying Jane Street Knowledge Base for: 'concurrency'...
[+] Found 1 matching document(s):

=== Distilled Intel: The Cost of Concurrency Coordination ===
Document ID : gjengset_concurrency_coordination_2020
Category    : N/A
```

**Status**: ✅ VERIFIED (2026-06-08T22:13:39Z)

---

## Firebase Collections

### jane_street_knowledge_base
**Purpose**: Jane Street HFT patterns and principles  
**Documents**: ~20 distilled intel documents  
**Usage**: `python scripts/query_kb.py "<term>"`

### learnings
**Purpose**: Captured lessons learned from epics  
**Documents**: Automated via `scripts/capture_lesson.py`  
**Usage**: `python scripts/capture_lesson.py <epic_id> <category> <insight> <confidence>`

### agent_sessions
**Purpose**: Agent session history tracking  
**Documents**: Automated via `scripts/agent_session_wrapper.py`  
**Usage**: Loaded automatically by `scripts/agent_bootstrap.py`

---

## Integration Points

### Epic Workflow
1. **`/epic-intake`**: Query KB for task-type patterns
2. **`/epic-plan`**: Verify compliance with Jane Street principles
3. **`/epic-validate`**: Audit against Jane Street DNA

### Agent Bootstrap
```bash
python agent_bootstrap.py <agent_name> <task_type> [files...]
```

**Output**: `.agent/bootstrap/<agent_name>-context.md`

---

## Security

**Git Status**: ✅ IGNORED (in `.gitignore`)  
**Backup Location**: `C:\Users\Mohammed Khalid\Downloads\`  
**Access**: Service account only (no user credentials)

---

## Recovery

If credentials are lost:

1. **Check Downloads**:
   ```powershell
   Get-ChildItem "C:\Users\Mohammed Khalid\Downloads\" -Filter "*firebase*"
   ```

2. **Copy to Repository**:
   ```powershell
   Copy-Item "C:\Users\Mohammed Khalid\Downloads\v12-morpheus-firebase-adminsdk-fbsvc-e1de2ed749.json" `
     -Destination "firebase-credentials.json"
   ```

3. **Verify**:
   ```bash
   python scripts/query_kb.py "concurrency"
   ```

---

## Maintenance

### Monthly
- Verify credentials still work
- Check Firebase quota usage
- Review captured lessons

### Quarterly
- Audit KB content
- Update patterns based on learnings
- Rotate service account key (if required)

---

## References

- Query KB: `scripts/query_kb.py`
- Agent Bootstrap: `scripts/agent_bootstrap.py`
- Lesson Capture: `scripts/capture_lesson.py`
- Integration Audit: `docs/brain/INTEGRATION_AUDIT_REPORT.md`

**Confidence**: HIGH (credentials verified working)