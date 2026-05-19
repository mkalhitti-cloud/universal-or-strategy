# ENGINEER MISSION: Build 1109.001 -- Freeze-Proof Structural Repair

**Priority**: CRITICAL -- daily live trading freezes for 2+ months, $300 loss from naked position on 2026-03-31.

**Plan**: `docs/brain/implementation_plan.md` -- READ THIS ENTIRE FILE FIRST. It contains all 9 phases with exact OLD/NEW code blocks.

**Branch**: `build/1105-monolith` (current)

**BUILD_TAG**: Update to `1109.001` in `src/V12_002.Properties.cs`

---

### Execution Order (STRICT -- do phases in this order)

| # | Phase | Files | Severity |
|---|-------|-------|----------|
| 1 | Non-blocking semaphore (Wait(0) + deferred retry) | SIMA.Dispatch.cs:48, SIMA.Lifecycle.cs:51 | CRITICAL |
| 2 | Account.All snapshot in unsubscribe | SIMA.Fleet.cs:255-280 | MEDIUM |
| 3 | Chunked fleet flatten (PumpFlattenOps) | V12_002.cs (new fields), SIMA.Flatten.cs (full rewrite of 2 methods + new PumpFlattenOps) | CRITICAL |
| 4 | Naked position watchdog (independent threadpool timer) | V12_002.cs (new fields), REAPER.cs (new methods), Lifecycle.cs (heartbeat + start/stop) | HIGH |
| 5 | REAPER retry hardening (dequeue on TriggerCustomEvent failure) | REAPER.Audit.cs:183, 319 | HIGH |
| 6 | Panel refresh coalescing (volatile guard) | UI.Panel.Lifecycle.cs:58-69 | MEDIUM |
| 7 | File I/O outside lock (atomic guard replaces lock) | UI.Compliance.cs:116-131, 143-147 | MEDIUM |
| 8 | Queue depth monitoring (threshold Print warnings) | V12_002.cs:349, Orders.Callbacks.AccountOrders.cs:153 | MEDIUM |
| 9 | Chunked ManageCIT (broker call budget) | Orders.Management.Flatten.cs:82 | MEDIUM |

---

### Protocol (MANDATORY per CLAUDE.md)

1. Read `docs/brain/implementation_plan.md` for the complete OLD/NEW code blocks
2. For each phase: apply the exact edits specified (OLD -> NEW)
3. After EACH phase: run `powershell -File .\deploy-sync.ps1` (re-establishes hard links + ASCII gate)
4. ASCII-ONLY in all C# string literals -- no emoji, curly quotes, em-dashes, Unicode arrows
5. No lock(stateLock) usage -- BANNED per CLAUDE.md
6. Do NOT modify any logic outside the plan scope -- surgical edits only
7. Run /loop-critic self-audit after completing all 9 phases

### Key Invariants to Preserve

- `isFlattenRunning` must be `true` from first enqueue until last PumpFlattenOps completes
- Watchdog timer runs on threadpool (System.Timers.Timer), NEVER marshalled to strategy thread
- `_watchdogFlattenFired` uses Interlocked.CompareExchange to prevent duplicate emergency flattens
- Semaphore deferred retries capture all parameters in local variables before the lambda (closure safety)
- EmergencyFlattenSingleFleetAccount is NOT changed (single-account, already acceptable)

### Verification After All Phases

- [ ] deploy-sync.ps1 passes ASCII gate
- [ ] F5 compile succeeds in NinjaTrader
- [ ] Banner shows BUILD_TAG 1109.001
- [ ] SIM: Entry with 3+ fleet accounts -- no freeze
- [ ] SIM: Flatten with 3+ fleet accounts -- Output shows [FLATTEN_PUMP] per account
- [ ] SIM: SIMA toggle during entry -- Output shows [DISPATCH] Semaphore contended
- [ ] SIM: Rapid entry+flatten cycles -- queue depth logs if > threshold
