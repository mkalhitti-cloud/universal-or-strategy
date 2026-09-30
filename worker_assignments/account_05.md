# Worker Assignment — account_05
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_05
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
- `[session_065]` P2 — 04/17/25 Market status | trading at the opening | Trading psychology review | Following the rules | Stop lost
- `[session_084]` P2 — 12/19/24 Spy levels, Rate cuts, trading psychology momentum trade, profit targets trading futures, charts up, book read Trading In The Zone
- `[session_088]` P2 — 11/21/24 - losing day trading, Bitcoin 100K, /ES double bottom trade, types of trading accouts, regular moving average trade, short sell, trend trade rule
- `[session_103]` P2 — 08/15/24 SOXL swing trade, UVXY review, CL & Tesla trade , losing trade, trading on the news trading
- `[session_108]` P2 — 07/18/24 SPY recape, losing trade psychology, taking profits on NQ trades, market on close trading, IVM shorting swing trading
- `[session_112]` P2 — 06/13/24 Forcing trades, sell stop vs buy stop, base trade. support and resistance time frame, trading psychology, market recape with Peter, trend trade, ATR stops
- `[session_114]` P2 — 05/30/24 Dell & Nvidia, Nvidia ETF, Far from move avg trade,being discipline, breakout, passing prop work challenge, holding stock over night, shorting a stock
- `[session_128]` P2 — 02/22/24 - Lesson review - Trading rules and psychology of trading
- `[session_145]` P2 — 10/19/23 TLT, trading psychology, trading rules, TSLA, money management,
- `[session_148]` P2 — 09/28/23 Opening bell, trading at 2pm, Watchlist and scanners,SPY levels on weekly and monthly, missing trades psychology
- `[session_149]` P2 — 9/21/23 Setup review /ES, mometum trade, SPY levels, MSFT trend trade, trading psychology, stop orders
- `[session_151]` P2 — 09/07/23 Market recape, SPY and TSLA review, ES Trend Trade setup, Apple news, loss aversion psychology and journaling

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
- `account_2X_download_batch_05.md` for download workers
- `account_3X_transcribe_batch_05.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_05`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_05`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_05 complete — {N} sessions processed"
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
