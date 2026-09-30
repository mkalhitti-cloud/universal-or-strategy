# Worker Assignment — account_17
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_17
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
- `[session_047]` P6 — 09/04/25 Mentorship Class
- `[session_048]` P6 — 08/28/25 Mentorship Class
- `[session_050]` P6 — 08/14/25 Mentorship Class
- `[session_052]` P6 — 07/24/25 Mentorship Class
- `[session_058]` P6 — 06/05/2025 Mentorship class
- `[session_059]` P6 — 05/29/2025 Mentorship Class
- `[session_060]` P6 — 05/22/25 Trade Management | Reversal Swing Trade | ATR criteria| ES setup for Double bottom
- `[session_064]` P6 — 04/24/25 Using ATR | Piot points vs 200 EMA | ES at opening bell? | bracket order | Tariffs in the market
- `[session_066]` P6 — 04/10/25 Mentorship Class
- `[session_067]` P6 — 04/03/25 Mentorship Class | market sell off
- `[session_068]` P6 — 03/30/25 Trading futures on the opening live with Daivd Green
- `[session_069]` P6 — 03/27/25 EMA difference SMA | using MACD for futures | NQ stop loss | ATR review | trading with a full time job

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
- `account_2X_download_batch_17.md` for download workers
- `account_3X_transcribe_batch_17.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_17`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_17`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_17 complete — {N} sessions processed"
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
