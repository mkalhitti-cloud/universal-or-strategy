# Worker Assignment — account_07
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_07
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
| P2 (High Value) | 2 | Psychology sessions |
| P3 (High Demand) | 10 | Apex/Prop firm sessions |
| P4-P6 | 0 | Other sessions |

### Session List
- `[session_254]` P2 — 9/23/21 Money management, How to Psychologically Manage A Huge Loss.
- `[session_256]` P2 — 9/16/21 FFMA Swing Trades, Pivot Points and Trading Psychology
- `[session_013]` P3 — 05/28/2026 Trading Mentorship Discussion: Risk Management, Trade Exits, Profit Goals, Multi-Timeframe EMAs & Apex Rule Changes
- `[session_024]` P3 — NinjaTrader Setup for Apex Funded Traders (Step-by-Step)
- `[session_026]` P3 — 02/05/2026 Stop Placement, RSI, ORB & Funding Rules Explained
- `[session_031]` P3 — 01/08/2026 Slow Markets - Charts setups Thinkorswim, Apex Trader Funding, Risk Management, ORB Trade, Turn and Burn
- `[session_046]` P3 — 09/11/25 Slow Market with Scotty , opening Apex account | ORB Trade
- `[session_073]` P3 — 03/06/25 /NQ on trend trade | ATR | Boxing a trade ( bracket order) | Peter market recape | longer term trade | reversal swing trade | full time job, can I still trade? | David’s Topstep P/L
- `[session_074]` P3 — 03/03/25 Setting up a futures prop trading account
- `[session_100]` P3 — 09/03/24 Intro to trading futures and opening a prop account
- `[session_115]` P3 — 05/23/24Futures trading rules, DOW recape, Nvidia ETF, ATR, FFMA review, reversal swing trade setup, Myrna, Opening a Futures Apex account $18!
- `[session_136]` P3 — 12/28/2023 Type of trading accounts, options review, opening Apex trading account live, regular moving average, Options trading with small account

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
- `account_2X_download_batch_07.md` for download workers
- `account_3X_transcribe_batch_07.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_07`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_07`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_07 complete — {N} sessions processed"
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
