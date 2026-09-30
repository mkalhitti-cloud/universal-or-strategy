# Worker Assignment — account_20
## Role: Tier 2 Pipeline Orchestrator
## Batch: batch_20
## Assigned by: Media Architect (account_01)
## Assigned at: TIMESTAMP

---

## Your Responsibility
You are a **Tier 2 Pipeline Orchestrator**. You manage the full archive
processing pipeline for your assigned batch of 44 sessions.

You do NOT do the work yourself. You assign Tier 3 workers and monitor their output.

---

## Your Batch Sessions (44 total)

| Priority | Count | Focus |
|----------|-------|-------|
| P1 (Crown Jewel) | 0 | Peter Tuchman Q&As |
| P2 (High Value) | 0 | Psychology sessions |
| P3 (High Demand) | 0 | Apex/Prop firm sessions |
| P4-P6 | 44 | Other sessions |

### Session List
- `[session_146]` P6 — 10/12/23 SPY levels, /ES trade review, bracket Orders, bond market lesson
- `[session_152]` P6 — 8/24/23 Trade management, NVDA reviews, following the rules, missing trades
- `[session_157]` P6 — 7/13/23 Trading earnings
- `[session_160]` P6 — 6/22/23 Option lesson by Kevin W
- `[session_162]` P6 — 6/08/23 - Fibonacci Lesson
- `[session_165]` P6 — 5/11/23 Market recap, /ES news driven, Swing trade review $TSN, long term investing, Option lesson
- `[session_168]` P6 — 4/20/23 Trading on the news?, stocks imbalancing, extended hours, double top/bottom pre-market, 3 daily max loss review.
- `[session_175]` P6 — 3/2/23 Q&A Options with Brian Heflin (159:57)
- `[session_184]` P6 — Options Lessons By Brian J. Heflin
- `[session_186]` P6 — 12/22/22 Market recap, options charts, 30 EMA which time frame?
- `[session_187]` P6 — 12/15/2022 - Paper account vs real account, support or resistance levels, pivot points, trading journal
- `[session_189]` P6 — 12/01/22 End of the year, $SPY resistance becomes support, Netflix trade setup, marketwatch.com
- `[session_193]` P6 — 10/27/22 Trading earnings, $AMZN & $META recape, swing trade lesson
- `[session_194]` P6 — 10/20/22 Market recap/ taxes/ trading with $2K account/ charts setup
- `[session_195]` P6 — 10/13/22 $SPY $NFLX review, swing trade review, taking the 1st trade.
- `[session_198]` P6 — ThinkorSwim Charts Setup
- `[session_202]` P6 — 9/1/22 Trading Options
- `[session_208]` P6 — 7/21/22 Swing trade review, Stop Loss, EMA levels
- `[session_209]` P6 — 7/14/2022 Market recap, trade management and David's watchlist
- `[session_212]` P6 — 6/23/22 Fibonacci lesson
- `[session_213]` P6 — 6/16/22 SPY support levels, Options or Shares, favorite technical level time frame?
- `[session_217]` P6 — 5/19/22 SPY review, Reversal Swing Trade, money management and long-term investments
- `[session_219]` P6 — 5/5/22 Market recap SPY,NFLX, TSLA and trade setups
- `[session_222]` P6 — 4/14/22 Swing Trades Review
- `[session_223]` P6 — 4/7/22 ETFs, Bull flag pattern, Double top/double bottom review
- `[session_224]` P6 — 3/31/22 Technical Analysis GME, SPY, AMD
- `[session_225]` P6 — 3/24/22 - Bracket Orders and Scanners
- `[session_226]` P6 — 3/18/22 Trade Management Review/ SOFI and BABA Swing trade/ 30 EMA
- `[session_227]` P6 — 3/10/22- Trading earnings Oracle, AMZN and GOOGL split
- `[session_229]` P6 — Watchlist & Scanners
- `[session_230]` P6 — Bracket Orders
- `[session_231]` P6 — 2/17/22
- `[session_235]` P6 — 1/20/22 Trading on floor (history story), Swing Trades, Review ROKU, NFLX, PTON and SOFI
- `[session_236]` P6 — 1/13/22 Money management review, Technical charts setups, Taxes
- `[session_238]` P6 — Introduction to Trading Options By Ali
- `[session_240]` P6 — 12/16/21 Trade Management and Money Management review
- `[session_244]` P6 — 12/30/21 Swing Trades, trading accounts, using margin, premarket trading
- `[session_245]` P6 — 11/18/21 Scanner, SPY, Long-term investments and Earning Lesson (92:33)
- `[session_246]` P6 — 11/17/21 - The Truth About Day Trading - webinar
- `[session_250]` P6 — 10/21/21 Money Management (stop loss), What is averaging into trade?, When to sell a blue sky stock?
- `[session_252]` P6 — 10/7/21 Swing Trades Review, Bracket Orders, Market Recap
- `[session_253]` P6 — 9/30/21 What Is A Spread? And Money Management
- `[session_255]` P6 — Thinkorswim Setup Watchlist and Scanners
- `[session_258]` P6 — 8/24/21 Swing trading

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
- `account_2X_download_batch_20.md` for download workers
- `account_3X_transcribe_batch_20.md` for transcribe workers
- etc.

Use: `python scripts/archive_agent/01_director.py --assign-tier3 batch_20`

### Step 3 — Monitor until all stages complete
Check status: `python scripts/archive_agent/01_director.py --batch-status batch_20`

### Step 4 — Commit and report
```powershell
git add archive/ worker_assignments/
git commit -m "feat(archive): batch batch_20 complete — {N} sessions processed"
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
- [ ] All 44 sessions in batch reach status `complete`
- [ ] All clips extracted and named correctly
- [ ] All metadata files written
- [ ] No sessions in status `failed`
- [ ] git push with completion commit done
