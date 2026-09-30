# Worker Assignment — account_18
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_18
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
- `[session_070]` P6 — 03/24/25 Getting started trading futures
- `[session_078]` P6 — 02/06/25 Scalping | Peter’s market recap | ES trade setup | far from move avg. trade and reversal swing trade
- `[session_079]` P6 — 01/30/25 Identify resistance and support | options contracts risk management | identifying trend | choose the right stop loss
- `[session_087]` P6 — 11/25/24 Boot Camp Review
- `[session_089]` P6 — 11/14/24 Mentorship Class
- `[session_090]` P6 — 11/07/24 Mentorship Class
- `[session_091]` P6 — 10/31/24 Mentorship Class
- `[session_092]` P6 — 10/24/24 David’s Morning routine, IBKR watchlist, feeler order, Calendar trading, Peter “ Never say never” when trading, TOS alert, tradingeconomics.com, $BIVI, trade trend Tesla and futures, taking a profit on /MNQ
- `[session_093]` P6 — 10/17/24 Trading physiology, trading rules, wash sell, incorporated for taxes, trading rules
- `[session_094]` P6 — 10/03/24 Far from avarage NQ, Peter’s market recap, what is ATR?, Reveral swing trade, volume profile, price action, STOP limit order, NVO Levels
- `[session_096]` P6 — 09/19/24 Tesla at opening bell, rate cutes, Peter on trading floor, Nike levels, taking profits, stop limit
- `[session_098]` P6 — 09/10/24 Trading futures lesson By David Green

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
- `account_2X_download_batch_18.md` for download workers
- `account_3X_transcribe_batch_18.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_18`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_18`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_18 complete — {N} sessions processed"
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
