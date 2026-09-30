# Worker Assignment — account_04
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_04
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
- `[session_012]` P2 — 06/04/26 - Trend Trades, RSI & EMA Support, ATR Profit Targets, Apex Account Management, Position Scaling & Stop-Loss Discipline
- `[session_014]` P2 — 05/21/2026 ORB, FFMA, ATR Stops, Apex Rules, Gold Futures & Trading Psychology
- `[session_015]` P2 — 05/14/2026 Heavily on trend trading execution, risk management, and trader psychology
- `[session_016]` P2 — 05/7/2026 Trading psychology, Risk management rules review, Journaling and reviewing trades
- `[session_020]` P2 — 04/09/26 RMA Trade Decisions, ORB Long/Short Rules, ATR Flexibility, Account Setup & Overtrading Psychology
- `[session_022]` P2 — 03/12/26 Trading Psychology, Risk Management, Trade Execution
- `[session_037]` P2 — 11/06/25 Double tops and double bottoms review, urge to close a trade too early, reusing the same indicator after an RMA
- `[session_038]` P2 — 10/30/25 Gold Trading Strategy, Trading Psychology & Rules, Proprietary Trading Firm over a personal brokerage account
- `[session_044]` P2 — 09/18/25 FFMA trade review | trading on multiple time frames | geting rid of bad habits
- `[session_053]` P2 — 07/17/25 Trend trade review | APEX | stop loss on ES & EMS | FOMO psychology
- `[session_062]` P2 — 05/08/25 Far from moving avaerage trade review| FFMA trade swing trade | losing day in the trading room | what is market on closing
- `[session_063]` P2 — 05/1/25 Market on close trade | Apext account setup | April best trading month | Amazon earnings | After blowing Apex account

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
- `account_2X_download_batch_04.md` for download workers
- `account_3X_transcribe_batch_04.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_04`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_04`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_04 complete — {N} sessions processed"
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
