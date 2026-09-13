# Live Trading Post-Mortem — Friday 2026-09-11
**Source files:** `NinjaTrader Grid 2026-09-11 09-17 AM.csv`, `NinjaTrader Grid 2026-09-11 09-18 AM.csv`, `docs/output.md`
**Accounts in scope:** PA-APEX-422136-05, -07, -08, -09 (leader = -09)
**Sim accounts excluded.**

---

## Trade Sequence (PA Only, Chronological)

### Trade 1 — ES Short (5:29 AM) — NOT copier, standalone
- APEX...019: Sell 10 ES @ 7652.75 → Exit Target1 @ 7638.75 ✅
- APEX...020: Sell 10 ES @ 7653.00 → Exit Target1 @ 7646.25 ✅
- **Result: Winner. Copier not involved.**

---

### Trade 2 — MES Short x8 @ 7666.00 (5:52 AM) — THE GOOD TRADE
- All 4 PA accounts entered short x8 MES at 7666.00
- Brackets confirmed correct on all accounts:
  - Stop: 7671.00 (Stop1/Stop2/Stop3 all = 7671.00)
  - Target1: 7663.50, Target2: 7662.25, Target3: 7661.00
- `-08` had a slight bracket propagation delay but caught up ✅
- **Exit: QX-2T button pressed at 5:53 AM (intentional)**
  - QX-2T cancelled all 6 ATM bracket orders on all 4 PA accounts
  - Replaced with PTT-QX-Stop + PTT-QX-T1
  - PTT-QX-T1 filled @ 7665.00 (1 unit per account, +1pt profit locked)
  - Remaining 7 units closed via "Close" at 5:55 AM
- **Result: Winner. Clean brackets, intentional QX exit.**

---

### Trade 3 — MES Short x7 @ 7674.75 (5:58 AM) — BRACKET BUG ON -07
- All 4 PA accounts entered short x7 MES at 7674.75
- Brackets on -05, -08, -09: CORRECT (Stop @ 7680.75, Targets @ 7671.75 / 7670.25 / 7668.75)
- **`-07` brackets: fo=NULL on every single bracket order (Stop1/2/3 + Target1/2/3)**
  - -07 entered this trade with NO stop/target protection from the copier
  - Root cause: QX exit on previous trade left -07's order slate in a stale state
  - The copier's named bracket lookup (Stop1, Target1 etc.) found no matching orders on -07
  - First trade (5:52) worked on -07 because it was the first trade of the day (clean slate)
- **Exit: QX-Stop fired @ 7680.75 on all accounts (intentional)**
- **Result: Loss. -07 had no bracket protection for the duration of this trade.**

---

### Accidental Event A — PTT-Flatten reverse long on -05 (5:55 AM)
- After Trade 2 Close flattened all accounts at 5:55:35 AM:
  - Position was already FLAT on all 4 accounts
- PTT-Flatten dispatch arrived on -05 one second later (5:55:36 AM)
  - -05 was already flat → copier submitted Buy x7 as new ENTRY (not a close)
  - Fired TWICE → 14L accidental long on -05
  - Closed 6s later @ 7670.25 for tiny gain (+$12.50) but completely unintended
- -07, -08, -09 did NOT get this accidental entry (only -05 affected)
- **Root cause: PTT-Flatten dispatch did not check hasPosition==false on follower before submitting**

---

### Trade 4 — MES Short x6 @ 7669.00 (7:08 AM) — HUMAN ERROR, -09 ONLY
- -09 only: Sell 6 MES @ 7669.00 (market entry from typo → wrong price submitted as market)
- Closed immediately @ 7670.25 (-1.25 pts × 6 = -$37.50)
- No copier issue. Human error: typed wrong price, NT submitted market order.

---

### Trade 5 — MES Long x6 @ 7667.00 (7:14 AM) — FLATTEN REVERSE ENTRY BUG
- All 4 PA accounts entered long x6 MES @ 7667.00 at 7:14:54-55 AM
- Brackets from log: Stop @ 7673.25, Target1 @ 7664.25, Target2 @ 7662.75, Target3 @ 7661.25
- Closed via right-click Flatten / ChartTrader Close on leader (-09)
  - -09: Sell 6 @ 7666.50 Exit "Close" ✅
  - -05: Sell 6 @ 7666.50 Exit "PTT-Flatten" ✅ (close only, no reverse entry)
  - -07: Sell 6 @ 7666.50 Exit "PTT-Flatten" ✅ THEN Sell 6 @ 7666.00 Entry "PTT-Flatten" ❌
  - -08: Sell 6 @ 7665.75 Exit "PTT-Flatten" ✅ THEN Sell 6 @ 7666.00 Entry "PTT-Flatten" ❌
- Reverse short on -07 and -08 closed @ 7667.50 (-1.5 pts × 6 × 2 accounts = -$90 on MES)
- **Root cause: PTT-Flatten dispatch on followers arrived when follower was already flat/mid-close → became new entry**

---

## Confirmed Bugs

### BUG-1: PTT-Flatten fires as Entry when follower is already flat
- **Trigger:** Leader closed via any native NT exit (right-click flatten, ChartTrader Close, Close market order) → copier dispatches PTT-Flatten to followers → followers already flat or mid-close → PTT-Flatten executes as new Entry
- **Affected:** -05 at 5:55 AM (Trade 2 exit), -07 and -08 at 7:14 AM (Trade 5 exit)
- **Fix direction:** Copier must check `hasPosition == false` on each follower BEFORE executing PTT-Flatten dispatch. If follower is already flat, PTT-Flatten must be a no-op.

### BUG-2: Follower order slate not cleared between trades — fo=NULL on -07
- **Trigger:** Any exit that replaces standard named bracket orders (QX, flatten, etc.) leaves stale order name state → next entry on follower cannot find Stop1/Target1 etc. → fo=NULL → no bracket protection
- **Affected:** -07 on Trade 3 (5:58 AM short). First trade of day was clean because no prior state existed.
- **Fix direction:** After EVERY exit (not just QX) — when PositionStateChanged hasPos=False fires on any follower — run a cleanup pass: cancel all residual non-terminal orders, verify order slate is empty, only then mark follower ready for next bracket propagation. Block bracket propagation until clean slate confirmed.

---

## Architecture Discussion — Pull vs Push Model
*(see conversation — full analysis in this session)*

Key findings from architecture discussion:
- Clone mode already uses StartAtmStrategy for follower entries — trailing stop at entry is ALREADY working
- QX and BE replace ATM brackets with raw StopMarket — trailing lost when those buttons are pressed (known, accepted trade-off)
- Post-QX/BE ATM re-arm is possible via StartAtmStrategy on held Order reference but deferred (complex, separate pipeline)
- The two Friday bugs are the only outstanding correctness issues

## PTT Pipeline Issued
Wave: PTT-BUGS-LIVE-0911
Lane A: BUG-1 PTT-Flatten position guard (reverse entry on flat follower)
Lane B: BUG-2 Post-exit clean slate (fo=NULL bracket failure on -07 after QX cycle)
Prompt issued: docs/brain/live-trading-analysis-2026-09-11.md (this file)

---

## Key Facts
- QX-2T button was used to exit Trade 2. This was intentional and correct.
- All human errors were: wrong price typed → NT submitted market order (Trade 4, -09 only)
- The QX buttons and BE buttons were NOT involved in the accidental reverse entries
- Trade 2 (5:52 AM short x8) was the only fully clean and profitable PA trade of the day
- First trade of day always has clean slate — bugs manifest on 2nd+ trade after prior exit

