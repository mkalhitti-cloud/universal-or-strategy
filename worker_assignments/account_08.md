# Worker Assignment — account_08
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_08
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
| P3 (High Demand) | 7 | Apex/Prop firm sessions |
| P4-P6 | 5 | Other sessions |

### Session List
- `[session_141]` P3 — 11/16/23 Trading futures with Apex and options class
- `[session_144]` P3 — 10/26/23 Apex Trader Funding, trade management, AMZN and COIN, reversal swing trade
- `[session_150]` P3 — 09/14/23 Reversal Swing Trade review, Apex trading, technical analysis, support and resistance
- `[session_153]` P3 — 8/10/23 Market review, Apex and Tradovate, Roku trade, Options with kevin
- `[session_155]` P3 — 7/27/23 Apex trading review, trading ROKU earnings, trend trade review
- `[session_156]` P3 — 7/20/23 Trading with a funded acoount, Apex trading
- `[session_164]` P3 — 5/18/23 Money management, NFLX trend trade review, imbalance trading, Evaluation funded account
- `[session_019]` P5 — 04/16/26 RMA & FFMA Trades Explained: Stop Loss Adjustments, Trend Timing, Volatility,
- `[session_025]` P5 — 02/12/26 FFMA review, stock levels, pivot points VS FFMA
- `[session_028]` P5 — 01/22/2026 Stop Loss Placement, ATR Multiples, EMA Conflicts, Daily Targets, FFMA Trades, Swing Ideas
- `[session_034]` P5 — 11/20/2025 Trend Reversals, High-ATR Strategy, ORB Logic & Smart Entry Selection
- `[session_039]` P5 — 10/23/25 Trend trade on the 65 and 200 EMA, VWAP, Gold on EMA 15 or 30 mins, count points from last high or from the closed?

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
- `account_2X_download_batch_08.md` for download workers
- `account_3X_transcribe_batch_08.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_08`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_08`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_08 complete — {N} sessions processed"
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
