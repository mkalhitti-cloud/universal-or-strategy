# Worker Assignment — account_16
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_16
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
- `[session_017]` P6 — 04/30/2026 Swing Trade Review: ATR Stopless Strategy, Rule-Based Trading & Long-Term Investing
- `[session_018]` P6 — 04/23/2026 Bracket orders setup /ES, swing trade AMD, daily goal and Chart setup with Scotty
- `[session_021]` P6 — 03/26/26 Mentorship Class
- `[session_023]` P6 — 02/26/26 Mentorship Class
- `[session_027]` P6 — 01/29/2026 Mentorship Class
- `[session_030]` P6 — 01-08-2026 How to Trade Trends Correctly: EMA Alignment, VWAP, and Entry Timing
- `[session_032]` P6 — 12/11/2025 Strategies, Entries & Exits, Market Behavior, and Platform Mechanics
- `[session_033]` P6 — 12/04/25 Mentorship Class
- `[session_035]` P6 — 11/18/25 Slow Markets, Risk Management
- `[session_036]` P6 — 11/13/2025 ATR Stops, Trend Rules, Entry Signals, and trade reviews
- `[session_042]` P6 — 10/02/2025 PD + PW resistance and support | EMAs different time frames? | Define your 4points stop loss? | ATR Stop loss
- `[session_043]` P6 — 9/25/25 Mentoship Class

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
- `account_2X_download_batch_16.md` for download workers
- `account_3X_transcribe_batch_16.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_16`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_16`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_16 complete — {N} sessions processed"
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
