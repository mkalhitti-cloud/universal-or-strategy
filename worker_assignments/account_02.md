# Worker Assignment — account_02
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_02
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
| P1 (Crown Jewel) | 12 | Peter Tuchman Q&As |
| P2 (High Value) | 0 | Psychology sessions |
| P3 (High Demand) | 0 | Apex/Prop firm sessions |
| P4-P6 | 0 | Other sessions |

### Session List
- `[session_029]` P1 — 01/15/26 Q&A with Peter Tuchman
- `[session_049]` P1 — 08/21/25 Q&A with Peter Tuchman
- `[session_054]` P1 — 07/10/25 Q & A with Peter Tuchman
- `[session_056]` P1 — 6/19/2025 06/19/25 Peter's market recap | e-mini support level ( big levels) | trend trade review ( ESU25) | how many points stop loss for NQ and ES?
- `[session_057]` P1 — 06/12/2025 Q and A with Peter Tuchman
- `[session_076]` P1 — 02/20/25 NYSE history lesson with Peter Tuchman
- `[session_083]` P1 — 12/26/24 Q&A with Peter Tuchman
- `[session_102]` P1 — 08/22/24 Q&A with Peter Tuchman
- `[session_113]` P1 — 06/06/24 Q&A with Peter Tuchman
- `[session_123]` P1 — 3/28/24 Trading on slow days, swing trading futures MESM24, Peter's market recap, 30 EMA, stop orders trading options, what is a spread?
- `[session_125]` P1 — 03/14/24 Q&A with Peter Tuchman
- `[session_130]` P1 — 02/08/24 Setting up Apexader Funding, Nvidia swing trade, Peter's market recape, student testimonial, following the rules,

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
- `account_2X_download_batch_02.md` for download workers
- `account_3X_transcribe_batch_02.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_02`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_02`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_02 complete — {N} sessions processed"
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
