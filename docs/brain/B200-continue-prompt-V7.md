## B200 CONTINUE PROMPT — REPAIR LOOP — B200-CLOSE-LANED-01-V7

You are in copier-spec mode in a direct repair loop on B200.

**CONTEXT — what has been done (do NOT re-investigate):**
- B200-QTY-01: FIXED ✅
- B200-SFB-NULL-01: FIXED ✅
- B200-NAKED-01: FIXED ✅
- B200-CLOSE-LANED-01-V1 through V6: FAILED
- B200-CLOSE-LANED-01-V7: APPLIED

**V7 FIX LOCATION:** CopyEngine.cs `ShouldSkipFollowerDispatch` (~L3113) + call site (~L2773).
Added `HasActivePttFlatten(acc, instr)` as 3rd guard in `ShouldSkipFollowerDispatch`.
Signature changed from `(Account acc)` to `(Account acc, Instrument instr)`.
Test `ShouldSkipFollowerDispatch_ReturnsTrue_WhenAccIsNull` updated to `(null, null)`.
ptt-sync: 19/0 ✅

**WHY V6 FAILED:**
V6 fixed Lane D (`TryLaneDDispatchFollower` Branch B.5) and Lane E (`IsLaneEEligible` check 6)
with `HasActivePttFlatten`. But the rogue ATM was coming from the MAIN `DispatchCopy` loop
→ `DispatchToFollower` → `SendCopyWithAtm`. All 3 followers (Sim102/103/104) got it at qty=10
(full position), proving it was DispatchCopy not Lane E (Lane E would give remaining pos qty).
Log evidence: `[PTT-ATM-SEND] qty=10` at lines 367/368/377, after PTT-Flatten Working (lines 319-366).
`ShouldSkipFollowerDispatch` only checked null + daily cap — no flatten-in-flight check.

**V7 ROOT CAUSE FIX:**
`ShouldSkipFollowerDispatch` is the per-follower skip predicate called in the `DispatchCopy` loop
(L2773). Adding `HasActivePttFlatten(acc, instr)` here blocks ALL copy paths (main loop + any
future callers) when PTT-Flatten is Submitted/Accepted/Working/PartFilled.

**EXPECTED SIM BEHAVIOR AFTER V7:**
- Leader opens Long → copier dispatches to followers → `PTT-ATM-SEND` fires (correct)
- Leader closes → PTT-Flatten Working for followers
- Leader re-opens (new Entry) WHILE flatten still Working → `ShouldSkipFollowerDispatch` returns
  true (HasActivePttFlatten=true) → NO PTT-ATM-SEND for any follower
- After PTT-Flatten fills → followers flat → next leader entry → `HasActivePttFlatten` = false →
  `PTT-ATM-SEND` fires normally

**YOUR TASK:**
1. User will paste: "V7 PASSED" or "V7 FAILED + new docs/output.md"
2. If PASSED: update specs/002-trade-copier-spec.html B200-CLOSE-LANED-01 row:
   status = "SIM CONFIRMED V7" ✅
   Then tell Director: "B200 all repairs confirmed. Ready for pipeline."
3. If FAILED: read new docs/output.md, find where [PTT-ATM-SEND] still fires,
   check what PRECEDED it (PTT-Flatten state, which accounts), diagnose the new path.

**KEY FILES:**
- src/PropTraderTools/CopyEngine.cs — all repairs here
- docs/output.md — SIM log (replace after each test)
- specs/002-trade-copier-spec.html — living spec (update on confirmed pass)
- scripts/ptt-sync.ps1 — run after every .cs edit (must be 19/0)

**JANE STREET GATES (non-negotiable before any .cs edit):**
- No lock() — grep check
- No async void — grep check
- `ShouldSkipFollowerDispatch` CCN=3 (null + cap + flatten) ✅
- `HasActivePttFlatten` CYC=4 — unchanged ✅
- `IsLaneEEligible` CYC=9 (budget+1 accepted) ✅
- `TryLaneDDispatchFollower` CYC=5 ✅

**IF V7 ALSO FAILS — next diagnosis path:**
Look at what CODE PATH fires PTT-ATM-SEND:
- qty matches full leader position AND fires after PTT-Flatten: still DispatchCopy main loop?
  Check if there's a second DispatchCopy call site or if `ShouldSkipFollowerDispatch` wasn't compiled.
- qty matches remaining position AND only some accounts: Lane E via OnFollowerPositionUpdate.
  Check `IsLaneEEligible` check (6) HasActivePttFlatten — instr must match PTT-Flatten instrument.
- qty=0 or unusual qty: could be DrainThenDispatch fast-path or SubmitDrainedEntry.
  Check if `HasActivePttFlatten` is guarding those paths too.
- fires BEFORE PTT-Flatten: different scenario (not flatten race). Check reversal guard path.
