# V12 Universal OR Strategy -- Master Roadmap

## Build-984-SourceHardening | 12 Repairs CONFIRMED LIVE -- COMPLIANCE PASS

**Last Synced**: 2026-05-08T00:00:00Z
**Protocol**: V14 Alpha | **Current Build**: 1111.006-phase-6-t0
**Status**: 🟢 **READY FOR MERGE** (StyleCop & ASCII Gates PASS)
**Active Branch**: `build-984-source-hardening` | **Last Stable PR**: #76

---

## AGENT ROLES (This Sprint)

| Role | Agent | Scope |
| :--- | :--- | :--- |
| **P3 Architect** | Antigravity | Design, implementation plans, Codex prompts |
| **P4 Red Team** | Arena AI (text tab) | Audit plans before P5 executes. GitHub link + branch MUST be in every Arena prompt |
| **P5 Engineer** | Codex (user pastes manually) | Surgical src/ edits only |
| **P6 Validator** | Gemini CLI (fresh session) | Post-surgery verification |
| **P7 Sentinel** | GitHub PR | Merge to main, Sentry check |

> [!IMPORTANT]
> **GITHUB-FIRST RULE**: Push to GitHub BEFORE sending any Arena AI prompt.
> Every Arena AI prompt MUST include the raw GitHub link and branch name so Arena can read the current code.
> Arena AI text tab is in use -- no Trojan Horse pattern needed.

---

## ARCHITECTURAL DECISIONS (Locked)

| Decision | Verdict | Rationale |
| :--- | :---: | :--- |
| Rithmic Sidecar (SovereignBridge.exe) | **DEFERRED** | Not needed while NT8 native adapter works |
| All-Leader Mode (Mode 3) | **SHELVED** | SIMA already dispatches to all accounts from 1 chart. Mode 3 only needed if accounts need independent signal logic. |
| SIMA (Mode 1) | **KEEP** | Optimal for same-signal multi-account trading. 1 chart, 1 calculation, N accounts. |

---

## THE 5 REFACTORING PHASES -- STATUS

| Phase | Title | Status |
| :---: | :--- | :---: |
| **Phase 1** | Foundation (Monolith Partition -- 20+ partial files) | ✅ DONE |
| **Phase 2** | Command Routing (IPC TCP + FSM + OCO Fix) | ✅ DONE |
| **Phase 3** | Strategy Patterns (RAII + Resource Leak Remediation) | ✅ DONE |
| **Phase 4** | Event Lifecycle Dispatcher (ADR-020) | ✅ DONE |
| **Phase 5** | Modularization (StickyState + Trend + UI/Photon IO Subgraphs) | ✅ DONE |
| **Phase 6** | Hot Path Execution Hardening (T1/T2/T3 god-function extraction) | 🟡 IN PROGRESS |

---

## MORPHEUS MILESTONES

| Milestone | Title | Status | Required? |
| :---: | :--- | :--- | :---: |
| **M1** | Monolith Partition | ✅ COMPLETE | REQUIRED |
| **M2** | Arena Frozen (Execution Arena) | ✅ COMPLETE | REQUIRED |
| **M3** | Phase 4 Event Lifecycle Dispatcher | ✅ COMPLETE -- Extraction live. Build-984 Source Hardening is next before P7 merge. | REQUIRED |

> [!IMPORTANT]
>
> ## PRODUCTION GATE
>
> **M3 = finish line (no Rithmic).** When Build-984 Source Hardening P7 merges to main, the project is production-complete.
> M3 fully closes when: Build-984 implemented (P5) + validated (P6) + merged to main (P7).

| Milestone | Title | Status | Required? |
| :---: | :--- | :--- | :---: |
| **M4** | Rithmic Sidecar (SovereignBridge.exe) | 🔵 DEFERRED | OPTIONAL |
| **M5** | Zero-Allocation Hot Path | 🔵 PLANNED | OPTIONAL |
| **M6** | Cache-Aligned Data Structures | 🔵 PLANNED | OPTIONAL |
| **M7** | Concurrency Hardening (SPSC/MPMC) | 🔵 PLANNED | OPTIONAL |
| **M8** | Distributed Optimization (Photon Kernel) | 🔵 DEFERRED (needs M4) | OPTIONAL |
| **M9** | Full Autonomy (AMAL Loop) | ⚪ DEFERRED (needs M4/M8) | OPTIONAL |

---

## CURRENT MISSION: BUILD-984 SOURCE HARDENING -- STEPS 1-4 COMPLETE

### Context: Phase 4 Declared Complete (2026-05-05)

- [x] `ProcessOnStateChange` (432-line God Function) extracted into 5 dedicated handlers
- [x] Verified live in `src/V12_002.Lifecycle.cs` (handlers at lines 93/220/302/404/451)
- [x] 12 Arena findings (F-01 to F-12) triaged as pre-existing source defects -- deferred to this mission

### Step 1 -- P3 Architecture Review ✅ COMPLETE

- [x] Antigravity authored `docs/brain/implementation_plan.md` with 12 surgical FIND/REPLACE blocks
- [x] Plan committed to `build-984-source-hardening` (commit: B984-P3)
- [x] F-09 waived -- re-analysis confirmed dict teardown ordering already correct

### Step 2 -- P4 Arena Red Team ✅ SKIPPED (Director approved directly)

- [x] Director reviewed and approved Codex's implementation plan before execution
- [x] Lock regex hardened to `(?<!\w)lock\s*\(` case-sensitive

### Step 3 -- P5 Engineer (Codex) ✅ COMPLETE

- [x] Codex applied all 11 code repairs (F-09 waived) to `src/V12_002.Lifecycle.cs`
- [x] Field `_uiSnapshotTickCounter` added to `src/V12_002.Data.cs`
- [x] BUILD_TAG bumped: `1111.004-v28.0-pr75-repairs` -> `1111.005-v28.0-b984`
- [x] Self-audit: PASS (lock, ASCII, unsafe, F-02/F-03/F-05 ordering, BUILD_TAG)
- [x] `deploy-sync.ps1`: PASS
- [x] Commit: `159fb9a` pushed to `build-984-source-hardening`

### Step 4 -- P6 Validation ✅ CONFIRMED LIVE IN NINJATRADER

- [x] Banner: `Build: 1111.005-v28.0-b984 | Sync: ONE SOURCE OF TRUTH`
- [x] F-10 ASCII banner confirmed (`[OK] BMad HARDENED DEPLOYMENT PROTOCOL ACTIVE`)
- [x] F-08 GTC telemetry confirmed (`[SHUTDOWN] GTC sweep: cancelling 0 tracked + broker-scanned orders`)
- [x] F-11 reconnect log confirmed (`[BUILD 984] Reconnect skipped -- SIMA=False, State=Realtime`)
- [x] F-06 REPAIRED banner absent from log
- [x] Photon MMIO mirrors online (F-01 layout check passed)
- [x] All 9 Risk Audit cases passed (Cases 8-9 idle: no live positions)
- [x] IPC server, watchdog, sticky state all nominal

### Step 5 -- P7 Sentinel (Close M3) ⬅ CURRENT GATE

- [ ] PR: `build-984-source-hardening` -> `main`
- [ ] Merge after review; Sentry: no new error events
- [ ] Update BUILD snapshot in roadmap after merge

**M3 FULLY CLOSED when Step 5 is complete.**

---

## CURRENT MISSION: PHASE 6 -- HOT PATH EXECUTION HARDENING
**Status**: 🟡 IN PROGRESS (V15.4 Protocol Active)
**Build**: `1111.006-phase-6-t0` | **Epic**: SIMA Subgraph Extraction

Phase 6 is a discrete milestone bridging M5 (Zero-Allocation Hot Path) and M7 (Concurrency Hardening). It focuses on extracting three primary god-functions: `ManageTrailingStops` (151 CYC), `ProcessOnExecutionUpdate` (120 CYC), and `ExecuteSmartDispatchEntry` (100 CYC).

### Recursive Protocol (V15.4) Status:
1. **Stage 0 (Forensic Intake)**: ✅ COMPLETE (`docs/brain/forensics_report.md`)
2. **Stage 1 (Vision/Spec)**: 🟡 READY FOR HANDOFF
3. **Stage 2 (Arch Planning)**: ⚪ PENDING
4. **Stage 3 (DNA Audit)**: ⚪ PENDING
5. **Stage 4 (Execution)**: ⚪ PENDING (Bob Shell configured)
6. **Stage 5 (Verification)**: ⚪ PENDING
7. **Stage 6 (Sign-off)**: ⚪ PENDING

### References

- `epic:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7`
- `spec:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/4d69f7d8-473e-412c-8928-5c0304018e82` (Epic Brief)
- `spec:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/513f05c0-ec33-4c5a-bd87-96c848fb3958` (Refactoring Approach)

### Ticket Sequence

- [x] T0: Setup V15.4 Environment & Forensic Intake
- [ ] T1.A: Decouple SIMA State (expectedPositions, _followerBrackets)
- [ ] T1.B: Extract SIMA Engine (ProcessFleetSlot, PumpFleetDispatch)
- [ ] T1.C: Extract Fleet Management (ShouldSkipFleetAccount, GetSortedAccountFleet)
- [ ] T1.D: SIMA Lifecycle Decoupling
- [ ] T2.A: ManageTrailingStops Extraction (Hotspot #1)
- [ ] T3.A: ProcessOnExecutionUpdate Partition
- [ ] T3.B: Execution Registry Extraction
- [ ] T3.C: Callback Sanitization
- [ ] T3.D: Order ID Map Optimization
- [ ] T4: Final Integration & Regression Test

---

## ADR-020 PHASE GATE STATUS

| Phase | Role | Purpose | Status |
| :---: | :--- | :--- | :--- |
| **P1** | Orchestrator | Intake & Context | ✅ COMPLETE |
| **P2** | Forensics | Evidence & Proof of Failure | ✅ COMPLETE |
| **P3-V1** | Architect | Initial Plan (FAILED -- Null Fix) | ❌ FAILED |
| **P3-V2** | Architect (Hardening) | RAII Remediation Plan | ✅ COMPLETE |
| **P4** | Adjudicator | Red Team Arena Audit | ❌ FAILED (Type 2 Leaks found) |
| **P4-RETRO** | Arena Retro Audit | Null Fix confirmed 2/2 FAIL | ✅ COMPLETE |
| **P5** | Engineer (Codex) | Build-982-Phase2-RAII Surgical Execution | ✅ COMPLETE |
| **P6** | Validator | Post-Surgery Verification | ✅ **PASS** (2026-05-04) |
| **P3-V3** | Architect (Phase 4) | Event Lifecycle Dispatcher Plan | ✅ COMPLETE (2026-05-04) |
| **P5-PR76** | Engineer (Codex) | PR #76 Repairs (D1/D2/D3/D6) | ✅ COMPLETE -- verified 2026-05-05 |
| **P4-PHASE4** | Arena Red Team | Phase 4 Plan Audit | ✅ PASS -- 12 findings triaged as pre-existing, deferred to B984 |
| **P5-PHASE4** | Engineer (Codex) | Phase 4 Extraction | ✅ CONFIRMED LIVE in src/ (2026-05-05) |
| **B984-P3** | Architect (Build-984) | Source Hardening Plan (12 deferred findings) | ✅ COMPLETE (2026-05-05) |
| **B984-P4** | Arena Red Team | Build-984 Plan Audit | ✅ SKIPPED -- Director approved directly |
| **B984-P5** | Engineer (Codex) | Build-984 Implementation | ✅ COMPLETE -- commit 159fb9a (2026-05-05) |
| **B984-P6** | Validator | Build-984 NinjaTrader Live Verification | ✅ CONFIRMED LIVE (2026-05-05T22:16Z) |
| **B984-P3-CI** | Orchestrator | PR Intelligence (Qwen/GLM/PR-Agent) | ✅ COMPLETE (2026-05-06) |
| **B984-P7** | Sentinel | GitHub PR merge to main | ✅ **COMPLETE** (2026-05-06) |

---

## HEALTH SNAPSHOT (Live as of 2026-05-05)

| Signal | Status |
| :--- | :--- |
| **Compilation** | [OK] `1111.006-v28.0-b984-complete` -- CLEAN (NinjaTrader live confirmed 2026-05-07, three sessions) |
| **ASCII Gate** | [PASS] Zero non-ASCII violations |
| **Lock Audit** | [PASS] Zero executable `lock()` in `src/*.cs` (hardened regex) |
| **StickyState Refactor** | [DONE] K0-K4 extractions live in `V12_002.StickyState.cs` (2026-05-07) |
| **Trend Refactor (T1-T3)** | [DONE] T1/T2/T3 extractions live in `V12_002.Entries.Trend.cs` (2026-05-07) |
| **UI/Photon IO Refactor (U1-U15)** | [DONE] U1-U15 extractions live across 7 UI/IPC files (2026-05-07) |
| **Phase 5 Status** | [COMPLETE] All three subgraphs done. God-function extraction mission closed. |
| **RAII Leak Fix** | [DONE] `ClearDispatchSyncPending` injected (2 occurrences) |
| **Hard Links** | [SYNCED] `deploy-sync.ps1` EXIT 0 |
| **Risk Audit** | [PASS] Cases 1-7 pass, 8-9 idle (no live positions) |
| **IPC Server** | [OK] Listening on 127.0.0.1:5001 (Multi-Client) |
| **Watchdog** | [OK] Started (2000ms interval, 5s timeout) |
| **OR Logic** | [OK] 4 sessions replayed correctly (Apr 29 - May 5) |
| **SIMA** | [DISABLED] Single-account mode -- expected for this config |
| **GitHub** | [PENDING P7] `build-984-source-hardening` -> `main` PR not yet merged. |

---

## HOTSPOT MAP (Gemini CLI + jCodeMunch scan, 2026-05-04)

> [!NOTE]
> Do NOT merge hotspot refactoring into Phase 4. Phase 4 wraps these in dispatcher scaffolding.
> Refactor internals in M5-M9 AFTER dispatchers exist.

| Rank | Method | File | Complexity | Score | Phase 4? | Action |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- |
| 1 | `ManageTrailingStops` | `Trailing.cs` | 151 | 408 | Indirect | Phase 6 / IN PROGRESS |
| 2 | `HydrateWorkingOrdersFromBroker`| `SIMA.Lifecycle.cs` | 96 | 238 | YES | Phase 4 wraps it |
| 3 | `ProcessQueuedExecution` | `UI.Compliance.cs` | 87 | 216 | Indirect | M9 extraction |
| 4 | `HydrateFSMsFromWorkingOrders` | `SIMA.Lifecycle.cs` | 76 | 188 | YES | Phase 4 wraps it |
| 5 | `ExecuteSmartDispatchEntry` | `SIMA.Dispatch.cs` | 100 | 179 | YES | Phase 6 / IN PROGRESS |
| 6 | `ProcessIpc_MatchSymbol` | `UI.IPC.cs` | 49 | 159 | No | Phase 2 follow-up |
| 7 | `SubmitBracketOrders` | `Orders.Management.cs` | 53 | 143 | No | M7 Concurrency |
| 8 | `OnStateChangeTerminated` | `Lifecycle.cs` | 43 | 121 | YES | Phase 4 wraps it |
| 9 | `AuditSingleFleetAccount` | `REAPER.Audit.cs` | 45 | 87 | No | M9 REAPER extraction |
| 10 | `ProcessOnExecutionUpdate` | `Orders.Callbacks.Execution.cs` | 120 | -- | No | Phase 6 / IN PROGRESS |
| -- | **`ExecuteTRENDEntry`** | `Entries.Trend.cs` | **10** | **--** | ✅ | **REFACTORED** |

---

## INFRASTRUCTURE DEBT (Deferred -- Rithmic track)

| ID | Severity | Description | Status |
| :---: | :---: | :--- | :--- |
| F-001 | LETHAL | False Sharing -- hot-path structs not padded to 64 bytes | DEFERRED (M5) |
| F-002 | LETHAL | Missing Memory Barriers -- SPSC ring no Volatile.Read/Write | DEFERRED (M5) |
| F-003 | MODERATE | Microsecond timestamp sync (PTP/NTP) for Rithmic sidecar | DEFERRED (M4) |
| F-004 | ADVISORY | Property-based testing gap (FsCheck) | DEFERRED (M9) |

> [!NOTE]
> F-001 and F-002 are LETHAL only for the SPSC ring buffers needed by the Rithmic sidecar.
> With Rithmic deferred, these are dormant -- they do not affect the current NT8 strategy execution.
