# Worker Assignment — account_13
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_13
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
- `[session_183]` P5 — 1/5/23 $TSLA & $LW trade recap, trend trade review, having a trading plan, New TOS chart setup with download link 📈
- `[session_185]` P5 — 12/29/22 Yearly recap, TSLA swing trade, KALA, trading the E-mini, regular moving average trade vs trend trade
- `[session_188]` P5 — 12/08/22 Swing trade review, $BA & $NVDA Regular Moving Average Trade
- `[session_190]` P5 — 11/17/22 Market recap, Trend Trade strategy for a Swing Trade, 30 EMA,
- `[session_191]` P5 — 11/10/22 SPY trend trade, morning preparation & Marketwatch.com, support and resistance
- `[session_192]` P5 — 11/3/22 Money management, stop orders, Regular Moving Average Trade and Trend Trade review
- `[session_197]` P5 — 9/29/22 $tesla Regular moving average trade, risk reward ratio, long-term investing, $SQQQ
- `[session_200]` P5 — 9/15/22 Options, trend trade and pivot points review
- `[session_204]` P5 — 8/18/22 Reversal swing trade, base trade review, and risk management
- `[session_205]` P5 — 8/11/22 SPY & COIN recap, regular moving average trade review, trade management
- `[session_206]` P5 — 8/4/22 Money management and trend trade review
- `[session_207]` P5 — 7/28/22 Reversal Swing Trades and Far From Moving Average Trade Review

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
- `account_2X_download_batch_13.md` for download workers
- `account_3X_transcribe_batch_13.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_13`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_13`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_13 complete — {N} sessions processed"
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
