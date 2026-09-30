# Worker Assignment — account_14
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_14
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
- `[session_211]` P5 — 6/30/22 Recession?/ FFMAT Review/ Trailing Stops
- `[session_214]` P5 — 6/2/22 AMD trend trade review, support and resistance multi time frame, Amazon stock split
- `[session_215]` P5 — 6/09/22 SPY Trend trade, BABA recap, Far from moving average, ETFs, UVXY Options
- `[session_216]` P5 — 5/26/22 Drawing Channels on SPY?, What is ATR, Trade Management, Trend Trade Review
- `[session_218]` P5 — 5/12/22 - Review SPY, long term investing, Far From Moving Average Trade
- `[session_220]` P5 — 04/28/22 Review Tesla, SPY, Base Trade
- `[session_221]` P5 — 04/21/22 Review SNAP & Tesla earnings, Trend Trade, Far From Moving Average Trade and Swing Trade.
- `[session_228]` P5 — 3/3/22 - Review Reversal Swing Traded And FFMA
- `[session_232]` P5 — 2/10/22 Pivot Points/TREND TRADES/ AMD review
- `[session_234]` P5 — 1/27/22 Apple Earnings/ Swing trading/ Trend Trade (86:42)
- `[session_239]` P5 — 12/23/21 Review Trade setups FFMA, Trend Trades, Gamestop, NVDA,VXX (84:09)
- `[session_241]` P5 — 12/9/21 FFMA ORCL, NVDA And DOUBLE TOP/BOTTOM Review

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
- `account_2X_download_batch_14.md` for download workers
- `account_3X_transcribe_batch_14.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_14`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_14`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_14 complete — {N} sessions processed"
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
