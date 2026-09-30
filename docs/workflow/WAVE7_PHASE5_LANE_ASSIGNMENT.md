# Wave 7 Phase 5 — File-Lane Assignment Table

**Version**: V1.0 (2026-06-29)
**Purpose**: Canonical mapping of every epic to its file-lane for Phase 5 execution.
**Rule**: One lane = one `.cs` file. Epics within a lane execute STRICTLY SEQUENTIALLY.
**Parallelism**: All 40 lanes run concurrently (separate Bob IDE sessions). Zero cross-lane file conflicts by construction.

---

## Why File-Lanes? (Key Design Decisions)

### 1. File Conflict Prevention (Hard Constraint)
151 of 161 epics share their `.cs` file with at least one other epic. If two agents write to the same file concurrently → file corruption → broken build for every lane. File-lanes make same-file concurrent writes **physically impossible**.

### 2. Domain Context Injection (Quality Constraint)
Epics are grouped under 7 architectural clusters. The cluster description is injected into every worker's prompt so it knows the domain it is refactoring (REAPER safety predicates vs. UI event handlers vs. SIMA fleet dispatch). This produces better helper method names and better xUnit tests.

### 3. Build Serialization (Global Resource)
`dotnet build` is a global mutex. Within a lane, builds are naturally serialized (one epic at a time). Across lanes, a build retry protocol handles the ~1 expected cross-lane collision per wave.

### 4. Per-Ticket Execution Model (New in V3.0)
Phase 5 no longer runs one worker per epic. Instead, each ticket within an epic is its own `start_subtask` call, followed immediately by an independent verification subtask. Execution within a lane:

```
for each epic in lane (CYC desc order):
  read 04-tickets.md → get ticket_count N
  for ticket T in 1..N:
    start_subtask(v12-p5-ticket)   → writes ticket-T-completion.md
    start_subtask(v12-p5-verify)   → writes ticket-T-verification.md
    if FAIL → retry once → HARD_FAILURE if still failing
  start_subtask(v12-p6-review)     → writes 05-completion-report.md
```

---

## Cluster Descriptions (injected into every Phase 5 worker)

| Cluster | Description |
|---------|-------------|
| **S1_SIMA** | Fleet Coordination & Dispatch — multi-account fleet routing, order dispatch, lifecycle transitions. Helpers must be pure state queries or single-responsibility validators. No cross-fleet side effects. |
| **S2_EXECUTION** | Execution Engine — order callbacks, trailing stops, bracket FSM, symmetry followers, order management. Helpers maintain FSM/Actor Enqueue invariants. Zero `lock()`. Each helper is one state transition or one stop calculation. |
| **S3_UI_IO** | UI Layer & IPC Commands — panel handlers, keyboard callbacks, IPC command routing. Helpers are UI event handlers or command validators. Side effects limited to UI state. No order submission from helpers. |
| **S4_REAPER** | REAPER Defense & Safety — last-resort naked-stop detection, audit trails, watchdog, safety limits. Helpers must be **pure predicates** (bool return, no side effects). Each helper checks exactly one safety condition. |
| **S5_KERNEL** | Kernel State & Logging — sticky state persistence, lifecycle hooks, telemetry, structured logs. Helpers are state accessors or log formatters. No business logic in helpers. |
| **S6_SIGNALS** | Signals & Entry Logic — trend/OR/RMA/FFMA entry types, signal FSM, indicators. Helpers are signal evaluation predicates or entry condition checkers. Must be **stateless pure functions**. |
| **S7_MISC** | Kernel Infrastructure, Photon IO, PureLogic — base class entry points, constants, drawing helpers, ATM manager, Photon ring buffer. Helpers are infrastructure utilities. |

---

## File-Lane Table (40 lanes, 161 epics, 0 unassigned)

> **Epic order within lane**: CYC descending (hardest first). This ensures complex methods are extracted early; later simpler epics in the same file benefit from the already-cleaner context.

| Lane | Cluster | Source File | Epics (CYC desc) | Count |
|------|---------|-------------|-------------------|-------|
| **FL-01** | S1_SIMA | `V12_002.SIMA.Dispatch.cs` | W7-119(14) W7-027(9) W7-093(0) | 3 |
| **FL-02** | S1_SIMA | `V12_002.SIMA.Execution.cs` | W7-096(34) W7-094(17) W7-095(0) W7-097(0) | 4 |
| **FL-03** | S1_SIMA | `V12_002.SIMA.Flatten.cs` | W7-098(17) W7-028(0) W7-100(0) | 3 |
| **FL-04** | S1_SIMA | `V12_002.SIMA.Fleet.cs` | W7-001(31) W7-101(16) W7-062(13) W7-103(13) W7-061(12) W7-104(12) W7-105(12) W7-038(9) W7-029(0) W7-063(0) W7-106(0) | 11 |
| **FL-05** | S1_SIMA | `V12_002.SIMA.Lifecycle.cs` | W7-058(34) W7-059(34) W7-107(34) W7-109(34) W7-115(34) W7-056(28) W7-110(22) W7-054(20) W7-112(20) W7-070(13) W7-055(8) W7-005(0) W7-006(0) W7-060(0) W7-108(0) W7-111(0) W7-113(0) W7-114(0) | 18 |
| **FL-06** | S1_SIMA | `V12_002.SIMA.Shadow.cs` | W7-125(20) W7-071(13) | 2 |
| **FL-07** | S1_SIMA | `V12_002.SIMA.cs` | W7-092(13) | 1 |
| **FL-08** | S2_EXECUTION | `V12_002.Orders.Callbacks.AccountOrders.cs` | W7-026(17) W7-123(14) W7-072(12) | 3 |
| **FL-09** | S2_EXECUTION | `V12_002.Orders.Callbacks.Execution.cs` | W7-023(19) | 1 |
| **FL-10** | S2_EXECUTION | `V12_002.Orders.Callbacks.Propagation.cs` | W7-022(0) | 1 |
| **FL-11** | S2_EXECUTION | `V12_002.Orders.Callbacks.cs` | W7-020(34) W7-021(16) | 2 |
| **FL-12** | S2_EXECUTION | `V12_002.Orders.Management.Cleanup.cs` | W7-099(11) W7-030(0) | 2 |
| **FL-13** | S2_EXECUTION | `V12_002.Orders.Management.Flatten.cs` | W7-033(27) W7-008(19) W7-034(11) | 3 |
| **FL-14** | S2_EXECUTION | `V12_002.Orders.Management.StopSync.cs` | W7-035(34) W7-032(23) | 2 |
| **FL-15** | S2_EXECUTION | `V12_002.Orders.Management.cs` | W7-041(8) | 1 |
| **FL-16** | S2_EXECUTION | `V12_002.Symmetry.BracketFSM.cs` | W7-065(14) W7-102(14) W7-120(14) W7-064(11) W7-066(10) W7-122(10) W7-069(0) | 7 |
| **FL-17** | S2_EXECUTION | `V12_002.Symmetry.Follower.cs` | W7-042(16) W7-126(16) W7-127(16) W7-129(16) W7-057(10) W7-043(0) | 6 |
| **FL-18** | S2_EXECUTION | `V12_002.Symmetry.Replace.cs` | W7-131(34) W7-128(20) W7-002(16) W7-044(11) W7-121(10) W7-117(9) W7-037(0) W7-130(0) W7-132(0) | 9 |
| **FL-19** | S2_EXECUTION | `V12_002.Symmetry.cs` | W7-067(8) W7-124(0) | 2 |
| **FL-20** | S2_EXECUTION | `V12_002.Trailing.Breakeven.cs` | W7-036(34) W7-133(21) W7-040(10) W7-135(10) W7-134(0) | 5 |
| **FL-21** | S2_EXECUTION | `V12_002.Trailing.StopUpdate.cs` | W7-052(11) W7-048(0) W7-051(0) W7-053(0) W7-139(0) W7-140(0) | 6 |
| **FL-22** | S2_EXECUTION | `V12_002.Trailing.cs` | W7-050(34) W7-039(13) W7-049(11) W7-138(11) W7-136(0) W7-137(0) | 6 |
| **FL-23** | S3_UI_IO | `V12_002.UI.Callbacks.cs` | W7-046(12) W7-142(8) W7-045(0) W7-143(0) | 4 |
| **FL-24** | S3_UI_IO | `V12_002.UI.Compliance.cs` | W7-004(34) W7-003(21) W7-144(20) W7-149(20) W7-145(17) W7-147(15) W7-047(13) W7-146(13) W7-150(10) W7-151(9) | 10 |
| **FL-25** | S3_UI_IO | `V12_002.UI.IPC.Commands.Config.cs` | W7-017(22) W7-153(20) W7-152(0) | 3 |
| **FL-26** | S3_UI_IO | `V12_002.UI.IPC.Commands.Fleet.cs` | W7-016(21) W7-159(21) W7-015(18) W7-156(18) W7-019(17) W7-157(17) W7-154(11) W7-014(0) W7-155(0) | 9 |
| **FL-27** | S3_UI_IO | `V12_002.UI.IPC.Commands.Misc.cs` | W7-160(10) | 1 |
| **FL-28** | S3_UI_IO | `V12_002.UI.IPC.Server.cs` | W7-077(0) W7-078(0) | 2 |
| **FL-29** | S3_UI_IO | `V12_002.UI.IPC.cs` | W7-018(0) W7-068(0) | 2 |
| **FL-30** | S3_UI_IO | `V12_002.UI.Panel.Construction.cs` | W7-080(13) W7-011(0) W7-079(0) | 3 |
| **FL-31** | S3_UI_IO | `V12_002.UI.Panel.Handlers.cs` | W7-075(34) W7-074(12) W7-010(8) W7-076(0) | 4 |
| **FL-32** | S3_UI_IO | `V12_002.UI.Panel.Helpers.cs` | W7-009(9) | 1 |
| **FL-33** | S3_UI_IO | `V12_002.UI.Panel.StateSync.cs` | W7-012(34) W7-148(16) W7-161(10) W7-158(9) W7-013(8) | 5 |
| **FL-34** | S4_REAPER | `V12_002.REAPER.Audit.cs` | W7-082(90) W7-086(34) W7-031(19) W7-083(13) W7-116(13) W7-081(0) W7-084(0) W7-085(0) W7-087(0) W7-141(0) | 10 |
| **FL-35** | S4_REAPER | `V12_002.REAPER.Repair.cs` | W7-088(34) | 1 |
| **FL-36** | S4_REAPER | `V12_002.Safety.Watchdog.cs` | W7-089(10) W7-090(0) W7-091(0) | 3 |
| **FL-37** | S5_KERNEL | `V12_002.StickyState.cs` | W7-073(8) W7-118(0) | 2 |
| **FL-38** | S6_SIGNALS | `V12_002.Entries.FFMA.cs` | W7-025(2) | 1 |
| **FL-39** | S6_SIGNALS | `V12_002.Entries.RMA.cs` | W7-024(34) | 1 |
| **FL-40** | S7_MISC | `V12_002.PureLogic.cs` | W7-007(4) | 1 |

**Total: 40 lanes, 161 epics, 0 unassigned**

---

## Execution Model Per Lane

```
Lane FL-XX owns file src/<FILE>.cs exclusively for Phase 5 duration.

for each EPIC_ID in lane_epics (CYC desc order):
    1. Read docs/brain/EPIC_ID/04-tickets.md → ticket_count = N
    2. for T in 1..N:
         a. start_subtask(mode="v12-p5-ticket", message=TICKET_WORKER_MSG)
            → writes docs/brain/EPIC_ID/ticket-T-completion.md
            → runs: dotnet csharpier format src/
            → runs: dotnet build  (with retry on lock, max 2 retries)
            → runs: python3 scripts/complexity_audit.py | grep METHOD_NAME
         b. start_subtask(mode="v12-p5-verify", message=VERIFY_WORKER_MSG)
            → writes docs/brain/EPIC_ID/ticket-T-verification.md
            → independently re-measures CYC, checks lock(), checks xUnit, checks build
         c. if ticket-T-verification FAIL: retry ticket T once
            if still FAIL: log STUCK_TICKET to event_log.jsonl, continue to T+1
    3. after all N tickets pass: start_subtask(mode="v12-p6-review", message=REVIEW_MSG)
       → writes docs/brain/EPIC_ID/05-completion-report.md
       → final CYC measurement, wave_ready=true
    4. update manifest: all phases completed, status=complete
    5. continue to next EPIC_ID in lane

After all epics in lane complete:
    run: python3 scripts/wave7_batch_audit.py --phase 5 --epics <lane_epic_list>
    if exit 0: log lane_FL-XX_complete to event_log.jsonl
    if exit 1: retry failed epics (see redo list at /tmp/wave7_redo.txt)
```

---

## Build Collision Protocol

`dotnet build` is a global resource. Two lanes may attempt simultaneous builds.

```
On build failure with MSBuild lock error or access denied:
  wait 15 seconds
  retry dotnet build
  if still failing after 3 retries: log BUILD_COLLISION to event_log.jsonl, escalate to lane orchestrator
```

Expected collision rate: ~1 per wave (empirical estimate). Retries resolve 100% of lock collisions.

---

*Generated: 2026-06-29 | Version: V1.0*
*Source: docs/brain/wave7-epic-list.json + docs/brain/EPIC-W7-NNN/00-scope.md (for 20 blank-source epics)*
