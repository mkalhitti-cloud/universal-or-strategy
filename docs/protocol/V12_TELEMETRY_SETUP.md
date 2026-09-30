# V12 Telemetry Collection Setup Guide

**Version**: V12.22  
**Status**: Production Ready  
**Last Updated**: 2026-05-30

## Overview

This guide enables 100% autonomous data collection for all V12 EPIC work. The telemetry system routes events to 5 backends simultaneously:
- **Phoenix** (Arize AI) - LLM observability
- **LangSmith** - Agent tracing
- **Firebase** - Persistent storage
- **Obsidian** - Local knowledge vault
- **Paperclip Wiki** - Team documentation

## Architecture

```
NinjaTrader V12_002.cs
    ↓ (emits structured log events)
tail_ninjatrader_log.py (real-time parser)
    ↓ (parses 10 event types)
v12_log_parser.py (event extraction)
    ↓ (structured events)
agent_session_wrapper.py (multi-backend router)
    ↓ (routes to 5 backends)
[Phoenix, LangSmith, Firebase, Obsidian, Paperclip]
```

## Prerequisites

### 1. Python Environment
```powershell
# Install Python 3.12+ (if not already installed)
# Download from: https://www.python.org/downloads/

# Verify installation
python --version  # Should show 3.12+
```

### 2. Required Python Packages
```powershell
# Navigate to project root
cd C:\WSGTA\universal-or-strategy

# Install dependencies
pip install -r requirements.txt
```

**If `requirements.txt` doesn't exist, create it:**
```txt
# requirements.txt
arize-phoenix>=4.0.0
langsmith>=0.2.0
firebase-admin>=6.0.0
watchdog>=3.0.0
python-dotenv>=1.0.0
```

### 3. API Keys & Credentials

Create `.env` file in project root:
```bash
# .env (DO NOT COMMIT - already in .gitignore)

# Phoenix (Arize AI)
PHOENIX_API_KEY=your_phoenix_key_here
PHOENIX_PROJECT_NAME=v12-universal-or

# LangSmith
LANGSMITH_API_KEY=your_langsmith_key_here
LANGSMITH_PROJECT=v12-telemetry

# Firebase
FIREBASE_PROJECT_ID=your_firebase_project_id
FIREBASE_CREDENTIALS_PATH=path/to/firebase-credentials.json

# Obsidian Vault
OBSIDIAN_VAULT_PATH=C:\Users\Mohammed Khalid\Documents\V12-Agent-Vault

# Paperclip Wiki (optional)
PAPERCLIP_API_KEY=your_paperclip_key_here
PAPERCLIP_WORKSPACE=v12-team
```

### 4. Firebase Setup

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create new project: `v12-telemetry`
3. Enable Firestore Database
4. Generate service account key:
   - Project Settings → Service Accounts
   - Generate New Private Key
   - Save as `firebase-credentials.json` in project root
5. Update `.env` with path to credentials

### 5. Obsidian Vault

The vault already exists at:
```
C:\Users\Mohammed Khalid\Documents\V12-Agent-Vault
```

**Verify plugins are installed:**
- Dataview
- Templater
- Periodic Notes
- Calendar
- Obsidian Git
- Advanced Tables
- Kanban
- Excalidraw
- QuickAdd
- Commander

## Telemetry Wiring (Already Complete)

The following tracking calls are already wired in V12_002:

### 1. FSM Transitions
**Location**: `src/V12_002.cs:397`
```csharp
protected void Enqueue(Action<V12_002> action)
{
    if (action == null)
        return;
    TrackFsmTransition();  // ← WIRED
    _cmdQueue.Enqueue(new DelegateCommand(action));
    // ...
}
```

### 2. SIMA Dispatches
**Location**: `src/V12_002.SIMA.Fleet.cs:235`
```csharp
private void PumpFleetDispatch()
{
    TrackSimaDispatch();  // ← WIRED
    // A3-1: Abort and drain if SIMA disabled or flatten running
    // ...
}
```

### 3. REAPER Audits
**Location**: `src/V12_002.REAPER.Audit.cs:18`
```csharp
private void AuditApexPositions()
{
    TrackReaperAudit();  // ← WIRED
    bool shouldLog = (DateTime.UtcNow - lastReaperLog).TotalSeconds >= 30;
    // ...
}
```

### 4. Metrics Summary
**Location**: `src/V12_002.Lifecycle.cs:152`
```csharp
private void ShutdownUiAndServices()
{
    // ...
    DrainQueuesForShutdown();
    EmitMetricsSummary();  // ← ALREADY WIRED
    // ...
}
```

## Activation

### Step 1: Start Real-Time Log Monitoring

**Option A: Foreground (for testing)**
```powershell
cd C:\WSGTA\universal-or-strategy
python scripts\tail_ninjatrader_log.py
```

**Option B: Background (for production)**
```powershell
cd C:\WSGTA\universal-or-strategy
Start-Process python -ArgumentList "scripts\tail_ninjatrader_log.py" -WindowStyle Hidden
```

**Option C: Windows Service (recommended for 24/7)**
```powershell
# Install NSSM (Non-Sucking Service Manager)
# Download from: https://nssm.cc/download

# Create service
nssm install V12TelemetryCollector "C:\Users\Mohammed Khalid\AppData\Local\Programs\Python\Python312\python.exe" "C:\WSGTA\universal-or-strategy\scripts\tail_ninjatrader_log.py"

# Start service
nssm start V12TelemetryCollector

# Check status
nssm status V12TelemetryCollector
```

### Step 2: Verify Collection

**Check NinjaTrader Output Window:**
```
[BUILD 1111.010-epic5-perf] SESSION METRICS REPORT
  FSM Transitions   : 1234
  SIMA Dispatches   : 567
  REAPER Audits     : 89
  Symmetry Replaces : 12
  Order Submissions : 345
  IPC Commands      : 67
```

**Check Python Console:**
```
[2026-05-30 18:24:43] Parsed FSM_TRANSITION: {'count': 1234}
[2026-05-30 18:24:43] Routed to Phoenix: session_abc123
[2026-05-30 18:24:43] Routed to LangSmith: trace_xyz789
[2026-05-30 18:24:43] Routed to Firebase: /sessions/abc123
[2026-05-30 18:24:43] Routed to Obsidian: V12-Agent-Vault/sessions/2026-05-30.md
```

### Step 3: Verify Backend Storage

**Phoenix (Arize AI):**
1. Go to [Phoenix Dashboard](https://app.arize.com/)
2. Navigate to project: `v12-universal-or`
3. Check traces for session events

**LangSmith:**
1. Go to [LangSmith Dashboard](https://smith.langchain.com/)
2. Navigate to project: `v12-telemetry`
3. Check runs for agent sessions

**Firebase:**
1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Navigate to Firestore Database
3. Check collections: `sessions`, `events`, `metrics`

**Obsidian:**
1. Open vault: `C:\Users\Mohammed Khalid\Documents\V12-Agent-Vault`
2. Navigate to: `sessions/2026-05-30.md`
3. Verify session notes are being created

## Event Types Collected

The system parses and routes 10 event types:

| Event Type | Source | Frequency | Example |
|------------|--------|-----------|---------|
| `FSM_TRANSITION` | Enqueue() | Every actor command | `[TRACE:00042][FSM][SPAN] elapsed=2ms` |
| `SIMA_DISPATCH` | PumpFleetDispatch() | Every fleet broadcast | `[PUMP] Submitted 5 orders for E1_LONG` |
| `REAPER_AUDIT` | AuditApexPositions() | Every 1000ms | `[REAPER] Heartbeat: 3/5 accounts with positions` |
| `ORDER_SUBMISSION` | SubmitOrderUnmanaged() | Every order | `[ORDER] Submitted LONG 2 @ 5000.00` |
| `IPC_COMMAND` | ProcessIpcCommands() | Every IPC message | `[IPC] MODE_RMA received` |
| `METRICS_SUMMARY` | EmitMetricsSummary() | On strategy termination | `SESSION METRICS REPORT` |
| `POSITION_UPDATE` | OnPositionUpdate() | Every fill | `[POSITION] LONG 2 @ 5000.00` |
| `EXECUTION_UPDATE` | OnExecutionUpdate() | Every execution | `[EXECUTION] Filled 2 @ 5000.00` |
| `STATE_CHANGE` | OnStateChange() | Lifecycle transitions | `[STATE] Realtime → Terminated` |
| `ERROR` | Exception handlers | On errors | `[ERROR] Submit failed: ...` |

## Learning Extraction

The system automatically extracts learnings from patterns:

### Latency Spikes
```python
# Detected in v12_session_wrapper.py
if event['type'] == 'FSM_TRANSITION' and event['elapsed_ms'] > 10:
    learning = {
        'type': 'latency_spike',
        'threshold': 10,
        'actual': event['elapsed_ms'],
        'context': event['module']
    }
```

### Error Rates
```python
# Detected in v12_session_wrapper.py
error_rate = error_count / total_events
if error_rate > 0.05:  # 5% threshold
    learning = {
        'type': 'high_error_rate',
        'rate': error_rate,
        'errors': recent_errors
    }
```

### FSM Transition Rates
```python
# Detected in v12_session_wrapper.py
fsm_rate = fsm_count / session_duration_seconds
if fsm_rate > 100:  # 100 transitions/sec
    learning = {
        'type': 'high_fsm_rate',
        'rate': fsm_rate,
        'recommendation': 'Consider batching'
    }
```

## Troubleshooting

### Issue: No events being collected

**Check 1: NinjaTrader Output.txt location**
```powershell
# Default location
C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\log\Output.txt

# Verify in tail_ninjatrader_log.py
LOG_PATH = r"C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\log\Output.txt"
```

**Check 2: Python script running**
```powershell
# Check process
Get-Process python

# Check service (if using NSSM)
nssm status V12TelemetryCollector
```

**Check 3: API keys valid**
```powershell
# Test Phoenix connection
python -c "from arize_phoenix import Client; c = Client(); print('Phoenix OK')"

# Test LangSmith connection
python -c "from langsmith import Client; c = Client(); print('LangSmith OK')"
```

### Issue: Events collected but not routed

**Check 1: .env file loaded**
```python
# Add debug print to agent_session_wrapper.py
import os
print(f"PHOENIX_API_KEY: {os.getenv('PHOENIX_API_KEY')[:10]}...")
```

**Check 2: Backend credentials**
```powershell
# Verify Firebase credentials
python -c "import firebase_admin; print('Firebase OK')"
```

### Issue: High memory usage

**Solution: Adjust batch size**
```python
# In v12_session_wrapper.py
BATCH_SIZE = 100  # Reduce from 1000
FLUSH_INTERVAL = 30  # Increase from 10 seconds
```

## Maintenance

### Daily
- Check Obsidian vault for new session notes
- Review Phoenix dashboard for anomalies

### Weekly
- Review LangSmith traces for patterns
- Export Firebase data for backup

### Monthly
- Rotate API keys
- Archive old session data
- Update Python dependencies

## Integration with EPIC Workflows

### epic-run.md
The telemetry system is automatically active during epic-run:
```bash
# Step 1: Start telemetry (if not already running)
python scripts\tail_ninjatrader_log.py &

# Step 2: Run epic
bob /epic-run EPIC-8

# Step 3: Review collected data
# Check Obsidian: sessions/2026-05-30.md
# Check Phoenix: project v12-universal-or
```

### epic-tdd.md
Same integration - telemetry runs in background during TDD cycles.

## Security Notes

1. **Never commit `.env` file** - already in `.gitignore`
2. **Rotate API keys monthly** - set calendar reminder
3. **Firebase credentials** - store securely, never commit
4. **Obsidian vault** - backup regularly via Obsidian Git plugin

## Support

For issues or questions:
1. Check this guide first
2. Review `scripts/agent_session_wrapper.py` comments
3. Check `docs/brain/Living_Document_Registry.md` for updates
4. Contact: backtothefutures83@gmail.com

---

**Status**: ✅ Production Ready  
**Next Review**: 2026-06-30