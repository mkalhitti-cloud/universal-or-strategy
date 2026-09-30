# Worker Assignment — account_03
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_03
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
| P1 (Crown Jewel) | 9 | Peter Tuchman Q&As |
| P2 (High Value) | 3 | Psychology sessions |
| P3 (High Demand) | 0 | Apex/Prop firm sessions |
| P4-P6 | 0 | Other sessions |

### Session List
- `[session_131]` P1 — 02/1/24 Peter Tuchman Market Recap
- `[session_139]` P1 — 12/07/2023 Q&A with Peter Tuchman
- `[session_169]` P1 — 4/13/23 Q&A with Peter Tuchman
- `[session_201]` P1 — 9/8/22 Q&A with Peter Tuchman
- `[session_210]` P1 — 7/7/22 Q&A with Peter Tuchman
- `[session_233]` P1 — 2/3/22 Q&A with Peter Tuchman (64:57)
- `[session_242]` P1 — 12/2/21 Q&A with Peter Tuchman
- `[session_243]` P1 — 12/14/21 The Truth About Day Trading Webinar with David Green And Peter Tuchman
- `[session_247]` P1 — 11/11/21 Q&A with Peter Tuchman (51:46)
- `[session_008]` P2 — 07/09/2026 ATR Profit Targets & Stop-Loss Rules, Trend-Trade Entries, NQ vs. ES Risk, Position Scaling & Trading Discipline
- `[session_009]` P2 — 07/02/2026 ATR & Moving-Average Rules, Trend-Trade Entries, Apex Accounts, Trading Psychology & Market Analysis
- `[session_011]` P2 — 06/18/2026 Winning Mindset, Risk vs. Reward, Probability & Trading Discipline

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
- `account_2X_download_batch_03.md` for download workers
- `account_3X_transcribe_batch_03.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_03`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_03`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_03 complete — {N} sessions processed"
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
