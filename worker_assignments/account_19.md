# Worker Assignment — account_19
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_19
## Assigned by: Media Architect (account_01)
## Assigned at: TIMESTAMP

---

## Your Responsibility
You are a **Tier 2 Pipeline Orchestrator**. You manage the full archive
processing pipeline for your assigned batch of 12 sessions.

You do NOT do the work yourself. You assign Tier 3 workers and monitor their output.

---

## Your Batch Sessions (12 total)

| Priority | Count | Focus |
|----------|-------|-------|
| P1 (Crown Jewel) | 0 | Peter Tuchman Q&As |
| P2 (High Value) | 0 | Psychology sessions |
| P3 (High Demand) | 0 | Apex/Prop firm sessions |
| P4-P6 | 12 | Other sessions |

### Session List
- `[session_099]` P6 — 09/05/24 3 clock trade, managing risk trading Tesla and Nvidia, taking profits, AVGO trading earnings
- `[session_104]` P6 — 08/08/24 SOXL swing trade, NVDA weekly level, entering a long term investment position, NQ risking level, shorting UVXY, ATR, carry trade and pivot points
- `[session_105]` P6 — 08/01/24 NQU trade setup review, MBLY trading, NVDA swing trade, follow the rules
- `[session_106]` P6 — 07/29/24 Futures trading lesson
- `[session_110]` P6 — 06/27/24 Mentorship Class
- `[session_111]` P6 — 06/20/24 Mentorship Class
- `[session_116]` P6 — 05/16/24 - SPY recape, trading futures /NQ, ES vs NQ trading, taking profit in futures, using ATR for stop loss, GameStop recape
- `[session_118]` P6 — 05/02/24 Mentorship Class
- `[session_120]` P6 — 04/18/24 Mentorship Class
- `[session_126]` P6 — 03/07/24
- `[session_137]` P6 — 12/21/2023 SPY review, trading at 2:30, /ES momentum trade, Nike trading earnings live, Tradovate platform
- `[session_143]` P6 — 11/02/23 Master Class Trading Earnings

---

## Pipeline Stages You Orchestrate

```
Stage 1: DOWNLOAD   → Tier 3 workers: accounts 21-30
Stage 2: TRANSCRIBE → Tier 3 workers: accounts 31-45
Stage 3: ANALYZE    → Tier 3 workers: accounts 46-60
Stage 4: EXTRACT    → Tier 3 workers: accounts 61-75
Stage 5: METADATA   → Tier 3 workers: accounts 76-90
```

---

## Your 4-Step Protocol

### Step 1 — git pull
```powershell
git pull origin main
```

### Step 2 — Assign your Tier 3 workers
For each session in your batch, write to `worker_assignments/`:
- `account_2X_download_batch_19.md` for download workers
- `account_3X_transcribe_batch_19.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_19`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_19`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_19 complete — {N} sessions processed"
git push
```

---

## Input / Output Paths

| Stage | Input | Output |
|-------|-------|--------|
| Download | URL or local path | `archive/raw/{session_id}.mp4` |
| Transcribe | `archive/raw/{session_id}.mp4` | `archive/transcripts/{session_id}.json` |
| Analyze | `archive/transcripts/{session_id}.json` | `archive/transcripts/{session_id}_clips.json` |
| Extract | `archive/raw/{session_id}.mp4` + clips.json | `archive/clips/shorts/` `archive/clips/medium/` |
| Metadata | clips + transcript | `archive/metadata/{session_id}_metadata.json` |

---

## Success Criteria
- [ ] All 12 sessions in batch reach status `complete`
- [ ] All clips extracted and named correctly
- [ ] All metadata files written
- [ ] No sessions in status `failed`
- [ ] git push with completion commit done
