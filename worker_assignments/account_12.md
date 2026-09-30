# Worker Assignment — account_12
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_12
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
- `[session_159]` P5 — 6/29/23 /ES Regular Moving Average trade, reading charts, Reversal Swing Trade
- `[session_161]` P5 — 6/15/23 - Futures trading, long-term investing, Trend Trade and Base trade
- `[session_163]` P5 — 5/25/23 - Nvidia trade recape, es mini futures, Far From Moving Average Trade review, Netflix trade review
- `[session_166]` P5 — 5/04/23 Fed day recape, SPY weekly monthly levels, AMD Far From Moving Average review, Relative Strength
- `[session_170]` P5 — 4/6/23 $ABNB /ES /MES trade setup recap, Trend trade review
- `[session_171]` P5 — 3/30/23 Options, $KRE, market & banks recap, stop orders, Reversal Swing Trade review, $BABA Trend Trade
- `[session_172]` P5 — 03/23/2023 SPY Trend Trade, TSLA base trade, Coin on the weekly, support and resistance.son
- `[session_173]` P5 — 03/16/23 Trend trade NVDA, VXX down trade, why did silicon valley bank collapse, when to increas shares?
- `[session_174]` P5 — 3/9/23 Trading recap, futures, Far From Moving Average Trade and Trend Trade review, following the trading rules
- `[session_176]` P5 — 2/23/23 Market recap, maximum spread, Trend trade review, trading /ES, trading accounts, trade managing
- `[session_178]` P5 — 2/09/23 Trading at the opening, using a pivot point, TSLA trade recap, base trade review, time frames, Charts setup
- `[session_180]` P5 — 1/26/23 SPX Base trade review, trading VIX, patience during trading, stop orders with options

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
- `account_2X_download_batch_12.md` for download workers
- `account_3X_transcribe_batch_12.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_12`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_12`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_12 complete — {N} sessions processed"
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
