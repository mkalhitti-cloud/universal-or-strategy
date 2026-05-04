# V12 Universal OR Strategy -- Master Roadmap
## Build-982-Phase2-RAII Closed | ADR-020 Phase 4 Next
**Last Synced**: 2026-05-04T17:32:00Z
**Protocol**: V14 Alpha | **Current Build**: 1111.004-v28.0-pr56
**Active Branch**: `feature/phase-4-event-lifecycle`

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

## THE 4 REFACTORING PHASES -- STATUS

| Phase | Title | Status |
| :---: | :--- | :---: |
| **Phase 1** | Foundation (Monolith Partition -- 20+ partial files) | ✅ DONE |
| **Phase 2** | Command Routing (IPC TCP + FSM + OCO Fix) | ✅ DONE |
| **Phase 3** | Strategy Patterns (RAII + Resource Leak Remediation) | ✅ DONE |
| **Phase 4** | Event Lifecycle Dispatcher (ADR-020) | 🟡 NEXT |

---

## MORPHEUS MILESTONES

| Milestone | Title | Status | Required? |
| :---: | :--- | :--- | :---: |
| **M1** | Monolith Partition | ✅ COMPLETE | REQUIRED |
| **M2** | Arena Frozen (Execution Arena) | ✅ COMPLETE | REQUIRED |
| **M3** | Phase 4 Event Lifecycle Dispatcher | 🟡 IN PROGRESS | REQUIRED |

> [!IMPORTANT]
> ## PRODUCTION GATE
> **M3 = finish line (no Rithmic).** When Phase 4 P7 merges to main, the project is production-complete.
> M3 closes when: Phase 4 implemented (P5) + validated (P6) + merged to main (P7).

| Milestone | Title | Status | Required? |
| :---: | :--- | :--- | :---: |
| **M4** | Rithmic Sidecar (SovereignBridge.exe) | 🔵 DEFERRED | OPTIONAL |
| **M5** | Zero-Allocation Hot Path | 🔵 PLANNED | OPTIONAL |
| **M6** | Cache-Aligned Data Structures | 🔵 PLANNED | OPTIONAL |
| **M7** | Concurrency Hardening (SPSC/MPMC) | 🔵 PLANNED | OPTIONAL |
| **M8** | Distributed Optimization (Photon Kernel) | 🔵 DEFERRED (needs M4) | OPTIONAL |
| **M9** | Full Autonomy (AMAL Loop) | ⚪ DEFERRED (needs M4/M8) | OPTIONAL |

---

## CURRENT MISSION: CLOSE M3

### Step 1 -- Git Push (IMMEDIATE -- do this first)

- [ ] Push `feature/phase-4-event-lifecycle` to GitHub
- [ ] Confirm commit SHA visible on GitHub (needed for Arena AI prompt)

### Step 2 -- P6 Validation ✅ PASSED (2026-05-04)

Verify the Build-982-Phase2-RAII surgical edits are clean before Phase 4 begins.

- [x] `ClearDispatchSyncPending` present at 2 call sites in `AccountOrders.cs` (lines 361, 581) -- 4 total text matches; 2 are definition/comment, 2 are call sites. CORRECT.
- [x] Zero `try { }` empty blocks across `src/*.cs`
- [x] Zero `lock(` in `src/*.cs` -- 3 false positives (`Block(`, `Lock (` string literals) confirmed not real locks.
- [x] `_repairInFlight.TryRemove` inside `finally` block in `REAPER.Repair.cs` line 223
- [x] **Sign-off**: **P6 PASS** (Gemini CLI, 2026-05-04T10:37)

### Step 3 -- P3 Architecture Review (Antigravity)

Review the existing ADR-020 Phase 4 implementation plan for staleness after RAII changes, then produce the final Codex engineering brief.

- [ ] Read `docs/brain/implementation_plan.md` (Phase 4 scaffold spec)
- [ ] Verify no Phase 4 target files were altered by Build-982-Phase2-RAII
- [ ] Produce updated Phase 4 Codex brief

### Step 4 -- P4 Arena Red Team

Send Phase 4 plan to Arena AI (text tab) with GitHub link + branch for adversarial audit.

- [ ] Arena prompt includes: GitHub raw link, branch `feature/phase-4-event-lifecycle`, full plan
- [ ] Unanimous sign-off (target 2/3 models minimum)
- [ ] Log verdict in `docs/brain/nexus_a2a.json`

### Step 5 -- P5 Codex Implementation

User pastes the Codex engineering brief (produced in Step 3) into Codex manually.

- [ ] Codex implements Phase 4 scaffold (`Lifecycle.State.cs`, `Lifecycle.BarUpdate.cs`)
- [ ] Codex runs self-audit: ASCII gate, zero `lock(`, zero phantom blocks
- [ ] Codex runs `deploy-sync.ps1`
- [ ] Director presses F5 -- strategy compiles with new BUILD_TAG

### Step 6 -- P7 Sentinel (Close M3)

- [ ] Push Phase 4 implementation to GitHub
- [ ] PR: `feature/phase-4-event-lifecycle` -> `main`
- [ ] Merge after review
- [ ] Sentry: no new error events

**M3 CLOSED when Step 6 is complete.**

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
| **P3-V3** | Architect (Phase 4) | Event Lifecycle Dispatcher Plan | 🟡 IN PROGRESS -- Step 3 |
| **P4-PHASE4** | Arena Red Team | Phase 4 Plan Audit | ⚪ Step 4 above |
| **P5-PHASE4** | Engineer (Codex) | Phase 4 Implementation | ⚪ Step 5 above |
| **P7** | Sentinel | GitHub Merge to main | ⚪ Step 6 above |

---

## HEALTH SNAPSHOT (Live as of 2026-05-04)

| Signal | Status |
| :--- | :--- |
| **Compilation** | [OK] `1111.004-v28.0-pr56` -- CLEAN |
| **ASCII Gate** | [PASS] Zero non-ASCII violations |
| **Lock Audit** | [PASS] Zero `lock()` in `src/*.cs` |
| **Phantom Blocks** | [PASS] Zero empty `try { }` in `src/*.cs` |
| **RAII Leak Fix** | [DONE] `ClearDispatchSyncPending` injected (2 occurrences) |
| **Hard Links** | [SYNCED] `deploy-sync.ps1` EXIT 0 |
| **Risk Audit** | [PASS] Cases 1-7 pass, 8-9 idle (no live positions) |
| **IPC Server** | [OK] Listening on 127.0.0.1:5001 (Multi-Client) |
| **Watchdog** | [OK] Started (2000ms interval, 5s timeout) |
| **OR Logic** | [OK] 4 sessions replayed correctly |
| **SIMA** | [DISABLED] Single-account mode -- expected for this config |
| **GitHub** | [PENDING] Push needed before Arena AI step |

---

## HOTSPOT MAP (Gemini CLI + jCodeMunch scan, 2026-05-04)

> [!NOTE]
> Do NOT merge hotspot refactoring into Phase 4. Phase 4 wraps these in dispatcher scaffolding.
> Refactor internals in M5-M9 AFTER dispatchers exist.

| Rank | Method | File | Complexity | Score | Phase 4? | Action |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- |
| 1 | `ManageTrailingStops` | `Trailing.cs` | 151 | 398 | Indirect | M5 Zero-Alloc |
| 2 | `TryHandleFleetCommand` | `UI.IPC.Commands.Fleet.cs` | 156 | 279 | No | Phase 2 follow-up |
| 3 | `ProcessOnStateChange` | `Lifecycle.cs` | 91 | 252 | YES | Phase 4 wraps it |
| 4 | `HydrateWorkingOrdersFromBroker` | `SIMA.Lifecycle.cs` | 96 | 230 | YES | Phase 4 wraps it |
| 5 | `ProcessQueuedExecution` | `UI.Compliance.cs` | 87 | 216 | Indirect | M9 extraction |
| 6 | `ProcessIpcCommands` | `UI.IPC.cs` | 68 | 216 | No | Phase 2 follow-up |
| 7 | `HydrateFSMsFromWorkingOrders` | `SIMA.Lifecycle.cs` | 76 | 182 | YES | Phase 4 wraps it |
| 8 | `ExecuteSmartDispatchEntry` | `SIMA.Dispatch.cs` | 100 | 179 | YES | Phase 4 scaffolds this |
| 9 | `AuditSingleFleetAccount` | `REAPER.Audit.cs` | 83 | 148 | No | M9 REAPER extraction |
| 10 | `PropagateMasterPriceMove` | `Orders.Callbacks.Propagation.cs` | 82 | 147 | No | Monitor only |

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
