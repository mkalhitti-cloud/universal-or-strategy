
# Wave 9 VM Handoff Prompt

**For**: New Bob IDE session on VM (34.121.187.241)
**Mode**: `wave9-orch`
**Repo**: `/home/malhitticrypto/universal-or-strategy`
**Commit**: `9ad5bb20` (Wave 9 infrastructure initialized)

---

## Paste this as your opening message in a new `wave9-orch` session:

---

```
══════════════════════════════════════════════════════
WAVE 9 LAUNCH — Jane Street OKF Violation Repair
Repo: /home/malhitticrypto/universal-or-strategy
Mode: wave9-orch
══════════════════════════════════════════════════════

CONTEXT:
Wave 7 (CYC reduction): 100% complete. 1,378/1,378 methods at CYC <= 8.
Wave 8 (deferred OKF debt): 100% complete. All 20 DD entries fixed. Commit b5b4bb84.
Wave 9 (Jane Street OKF violations): THIS WAVE. ~335 entries across 8 lanes.

STARTUP STEPS (execute in order, no confirmations needed):
  1. git checkout main
  2. git pull origin main        (expect commit 9ad5bb20 or later)
  3. dotnet test tests/V12_Performance.Tests/ --verbosity quiet  (expect 338/338 PASS -- or 52/52 if net8.0 build)
  4. grep -r "lock(" src/        (expect 0 results)
  5. Read docs/brain/wave9-register/wave9-debt-register.md   (authoritative register)
  6. Read .lamport/wave9/event_log.jsonl                     (resume check -- clock starts at 242)

THEN execute the Wave 9 orchestration protocol exactly as specified in your
wave9-orch roleDefinition. No further instructions needed.

REGISTER SUMMARY:
  L1 A  18  DateTime.Now -> DateTime.UtcNow                          (Rule 3)
  L2 A  12  Account.All -> Account.All.ToArray()                     (Rule 5)
  L3 A  10  silent catch {} -> NinjaTrader.Code.Output.Process(...)  (Rule 5)
  L4 A  35  LINQ hot path -> explicit for loops                      (Rule 7)
  L5 B  223 magic numbers -> private const SCREAMING_SNAKE_CASE      (Rule 6)
  L6 B  12  hot-path throws -> bool/Result pattern                   (Rule 5)
  L7 B  21  LOC>80 methods -> private helper extraction              (Rule 6)
  L8 B  4   M5 dispatch -> static readonly Dictionary dispatch       (Rule 6)

CLASS B PRE-APPROVAL:
  Director explicitly pre-approved ALL Class B lanes (L5-L8) in this session.
  DO NOT ask for re-approval. Execute L5, L6, L7, L8 fully autonomously.

KEY CONSTRAINTS:
  - After EVERY src/ change: powershell -File .\deploy-sync.ps1 (NT8 hard-link sync)
  - After EVERY fix commit: dotnet test (338/338), grep lock( (0), pre_push_validation.ps1 -Fast (11/11)
  - Commit format: fix(wave9): W9-L{N}-{ID} -- {violation_type} in {file}:{line}
  - L3 lines are TBD in register -- wave9-scan MUST find exact lines first
  - L5 is batched by file -- one commit per file, group constants by semantic domain
  - L7 extractions: CYC must stay same or decrease after extraction
  - L8: read docs/intel/jane-street/how-to-build-an-exchange.md BEFORE starting
  - Lamport clock starts at 242 (wave8 ended at 241)
  - Sequential lane execution only: spawn L1, wait LANE_COMPLETE, spawn L2, etc.

COMPLETION CRITERIA:
  All 8 lanes log LANE_COMPLETE in .lamport/wave9/event_log.jsonl
  wave9_complete event logged with findings_fixed=~335
  All rows in wave9-debt-register.md marked: resolved: wave9 {commit_sha}

Report WAVE9_COMPLETE when done.
══════════════════════════════════════════════════════
```

---

## VM Pre-Flight Checklist

Before pasting the prompt above, verify on the VM:

```bash
cd /home/malhitticrypto/universal-or-strategy
git checkout main
git pull origin main
git log --oneline -3
# Expect: 9ad5bb20 feat(wave9): initialize Wave 9...
```

If the commit is not present, the push from local hasn't been pulled yet. Run `git pull` again.

---

## Infrastructure Already In Place

| Artifact | Location | Status |
|----------|----------|--------|
| Wave 9 register | `docs/brain/wave9-register/wave9-debt-register.md` | ✅ Complete |
| wave9-orch mode | `.bob/custom_modes.yaml` | ✅ Appended |
| wave9-lane mode | `.bob/custom_modes.yaml` | ✅ Appended |
| wave9-scan mode | `.bob/custom_modes.yaml` | ✅ Appended |
| Findings dir | `docs/brain/wave9-findings/README.md` | ✅ Created |
| Lamport log | `.lamport/wave9/event_log.jsonl` (local only) | ✅ clock=242 |

---

## Expected Wave 9 Duration

Wave 8 (20 single-line fixes) completed in ~6 minutes on VM.
Wave 9 has ~335 entries but many are multi-file batches (L5) and structural changes (L7/L8).
Estimated: 45-90 minutes depending on L7/L8 complexity.

L3 will take slightly longer because wave9-scan must first locate the exact line numbers
for the 10 silent catch{} entries (marked TBD in register).

L5 (223 magic numbers, 52 files) is the largest lane but each fix is mechanical
(grep + extract const). Wave9-scan batches by file so it's 52 scan+fix cycles not 223.
