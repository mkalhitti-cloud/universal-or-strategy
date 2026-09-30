# Worker Assignment — account_09
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_09
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
- `[session_040]` P5 — 10/16/2025 Why Gold is sky rocketing?, US - China tensions, Base trade review, trading minis or micros?, Dust trade review
- `[session_041]` P5 — 10/09/25 Stop orders in trend trade, ATR you consider worth your while to trade, Gold Trade
- `[session_045]` P5 — 09/11/25 Market breakdown | /ES Trend trade how do I enter? | 3 bar uptrend? | ORB Trade
- `[session_051]` P5 — 07/31/25 Trading the ATR | No rate cuts | Apple levels | Trend Trade review | Swing trading RSI rule
- `[session_055]` P5 — 06/26/25 Regular Moving Average Trades it’s ok to also use pivot points, vwap, and fibs? | how to approach going long on UVXY? | Which levels/ ranges on the FIB we are using the most? | When are you considering option trades?
- `[session_061]` P5 — 05/15/25 SPY recap | double bottom review | trend trade review | momentum trade
- `[session_071]` P5 — 03/20/25 Reversal Swing Trades and trend trade review | Moving stop to breakeven | trading  asian session
- `[session_072]` P5 — 03/17/25 Using Bollinger Bands | 2pt scapls | base trade review
- `[session_075]` P5 — 2/27/25 Trade review far from moving avg. and base trade setup | money management | 4 point stop lose on /ES
- `[session_077]` P5 — 02/13/25 Base trade /NQ | why 5 min charts? | shorting a stock | support and resistance | ATR | swing trade
- `[session_080]` P5 — 01/23/2025 ES trend trade review | FFMA | difference of stop limit & stop market | Swing Trade review
- `[session_081]` P5 — 01/16/25 KOLD trade review | RSI rule when trading futures? | FFMA trade and pivot points

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
- `account_2X_download_batch_09.md` for download workers
- `account_3X_transcribe_batch_09.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_09`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_09`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_09 complete — {N} sessions processed"
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
