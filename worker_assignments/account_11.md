# Worker Assignment — account_11
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_11
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
- `[session_124]` P5 — 03/21/24 Finding stop lost levels, what is a ATR?, trend trade review, futures trading, When not to do a far from moving avereage
- `[session_127]` P5 — 02/29/24 /NQ Labu MARA review, earnings lesson, far from moving avereage trade, trading accounts types
- `[session_129]` P5 — 02/15/24 Coinbase Far From Moving Average, taxes, SMCI & Mara daily swing trades, /NQ review
- `[session_132]` P5 — 01/25/24 Timeframes, retracement of a stock, Intel far from moving average review, trading on earnings day, futures Ticks
- `[session_133]` P5 — 01/18/24 Two clock trade, know your hit rate, AMD trade review, bracket order review, regular moving average trade
- `[session_134]` P5 — 01/11/24 /NQ trend trade review, using RSI, marketwatch.com, swing trade review
- `[session_135]` P5 — 01/4/24 - COIN far from moving average trade review, finding stop levels, spread rules, Trend Trade review, following the rules
- `[session_138]` P5 — 12/14/2023 SPY RSI high, /ES Far From Moving Average review, Tesla Trend trade recap
- `[session_140]` P5 — 11/30/2023 NVDA expanding the timeframe, COIN trade review, NVDA Far From Moving Average review, buying before market open?
- `[session_142]` P5 — 11/09/23 Finding support and resistance, $coin, Trend Trade long and short review, Regular Moving Average, /ES review
- `[session_147]` P5 — 10/05/23 - Bracket Orders , tralling stop stop orders, SPY on the 65 EMA level weekly, trading earnings, $BA levels, trend trade
- `[session_154]` P5 — 8/3/23 Trading earnings live, Trend Trade, market review with Peter, preparing for trading, Reversal Swing Trade review

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
- `account_2X_download_batch_11.md` for download workers
- `account_3X_transcribe_batch_11.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_11`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_11`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_11 complete — {N} sessions processed"
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
