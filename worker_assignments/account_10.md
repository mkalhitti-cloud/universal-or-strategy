# Worker Assignment — account_10
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_10
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
- `[session_082]` P5 — 01/02/25 Trading /CL stop loss | PDT rule | base trade review | regular moving average trade | trend trade review | Pete’s market recap
- `[session_085]` P5 — 12/05/24 Types of trading accounts | Peter on the floor | swing trade and trend trade review | taking profits
- `[session_086]` P5 — 11/27/24 - following the rules, ES on the pivot point level, far from moving average trade ES, review futures trading rules
- `[session_095]` P5 — 09/26/24T&S chart setting review, Micron earnings regular moving average trade and base trade, sell stop limit order, double top review, reversal swing trade review with options, shorting a stock
- `[session_097]` P5 — 09/12/24 Trending Economics news calendar, what is a spread?, trend trade review, Peter’s market recape, trade management
- `[session_101]` P5 — 08/29/24 NQ trade review, managing risk trading futures, Nvidia earnings, RMA trade review
- `[session_107]` P5 — 07/25/24 Far From Moving Average review, /NQ Regular Moving Average, SPY on EMA 65, Follwing the rules, Stop Order
- `[session_109]` P5 — 7/11/24 Regular moving average trade /NQU24, taking profits, No support levels on 5 min charts, review of Far from moving average /NQU24, using ATR, building long term position, trading at 2pm
- `[session_117]` P5 — 05/09/24 NVDA trade review, double bottom review, trend trade review, when to take profits?
- `[session_119]` P5 — 04/25/24 market review, /NQm24 Regular Move Avarage and FFMA trade,
- `[session_121]` P5 — 04/11/24 SPY recape, trend trade NVDA, GL far from moving average, building confidence when trading, Stop loss pre & after hours trading.
- `[session_122]` P5 — 04/04/24 Fed interest recap, NQM24 FFMA & base trade, TSLA Trend Trade, LULU Opinion play

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
- `account_2X_download_batch_10.md` for download workers
- `account_3X_transcribe_batch_10.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_10`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_10`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_10 complete — {N} sessions processed"
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
