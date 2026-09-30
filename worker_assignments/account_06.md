# Worker Assignment — account_06
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_06
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
| P2 (High Value) | 12 | Psychology sessions |
| P3 (High Demand) | 0 | Apex/Prop firm sessions |
| P4-P6 | 0 | Other sessions |

### Session List
- `[session_158]` P2 — 7/06/23 SPY trade review, swing trade with options, short term swing trade, Money Management and trading psychology
- `[session_167]` P2 — 4/27/23 FRC Puts, AMZN Trend Trade and Far From Moving Average Trade, volume vs speed, trading psychology
- `[session_177]` P2 — 2/16/23 Trading on the news, base trade, $SPY levels, trading options, 3 types of trading accounts, having discipline & patience, charts setup
- `[session_179]` P2 — 2/2/23 - January market recap, trading earnings live, trading psychology, how to use RSI?, trading with a large spreads?
- `[session_181]` P2 — 01/19/23 - Risk managment and trading psychology, EMA's crossing, student testimonials, managing a good trade and chart setup
- `[session_182]` P2 — 01/12/23 Selling covered calls and naked puts options, overcoming fear of trading
- `[session_196]` P2 — 10/06/22 - Losing day psychology, trading rules review, taking your 1st trade, trading options
- `[session_199]` P2 — 9/22/22 Market recap, $SAVA trend trade, trade management, and trading psychology
- `[session_203]` P2 — 8/25/22 Based trade and FFMAT review, beginner psychology and trading rules
- `[session_237]` P2 — 1/6/22 Losing Trades Psychology/ Regular Moving Average Trades/ Review DWAC, GameStop (79:08)
- `[session_248]` P2 — 11/4/21 FOMO Psychology/ Regular Moving Average Trades Review (76:55)
- `[session_251]` P2 — 10/14/21 Order Execution, $MRNA Recap, Disciplined Trader

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
- `account_2X_download_batch_06.md` for download workers
- `account_3X_transcribe_batch_06.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_06`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_06`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_06 complete — {N} sessions processed"
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
