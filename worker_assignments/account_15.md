# Worker Assignment — account_15
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_15
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
- `[session_249]` P5 — 10/28/21 Review the FFMA trade (APPL) stock and TREND trade (LCID)
- `[session_257]` P5 — 9/9/21 Trend Trades
- `[session_259]` P5 — 8/12/21 - Trend Trade And Shorting A Stock
- `[session_260]` P5 — 11/20/2025 Trend Reversals, High-ATR Strategy, ORB Logic & Smart Entry Selection
- `[session_001]` P6 — July Boot Camp Registration Now Open
- `[session_002]` P6 — Registration Boot Camp Course ( live in the trading room)
- `[session_003]` P6 — Bracket Orders
- `[session_004]` P6 — Watchlist & Scanners
- `[session_005]` P6 — Fibonacci Lesson
- `[session_006]` P6 — Options Lesson
- `[session_007]` P6 — Futures FastTrack Lesson PDF
- `[session_010]` P6 — 06/25/2026 High-ATR Risk Management, Volatility Rules, Moving-Average Setups, RSI Entries, Contract Scaling & Daily Loss Limits

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
- `account_2X_download_batch_15.md` for download workers
- `account_3X_transcribe_batch_15.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_15`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_15`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_15 complete — {N} sessions processed"
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
