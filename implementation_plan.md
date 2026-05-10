# Implementation Plan: Phase 6 Hot Path Execution Hardening (CLOSED)

## Observations
- Phase 6 targeted three god-functions: `ManageTrailingStops` (151 CYC), `ProcessOnExecutionUpdate` (120 CYC), and `ExecuteSmartDispatchEntry` (100 CYC).
- Extractions follow the A1-A5 alignments (no global allocations, explicit types, pure-method extraction).
- Post-extraction targets successfully hit: ManageTrailingStops < 30 CYC, ProcessOnExecutionUpdate <= 12 CYC, and ExecuteSmartDispatchEntry < 30 CYC.

## Approach
- Extractions performed in the same file to maintain logical cohesion.
- Ticket sequence executed linearly: T0 → T1.A-D → T2.A → T3.A-D → T4.

## Mermaid post-extraction call flow
[See docs/architecture.md for the updated call flow]

## Ticket Summary Table
| Ticket | Title | Files Touched | Helper Added | CYC Before -> After | Status |
|---|---|---|---|---|---|
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t0` | T0: Setup V15.4 | roadmap | - | N/A | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t1a` | T1.A: Throttle Tick | Trailing.cs | AdaptiveThrottleTick | 151 -> 130 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t1b` | T1.B: RunPerTradeBranches | Trailing.cs | ManageTrail_RunPerTradeBranches | 130 -> 100 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t1c` | T1.C: PointBasedTrailing | Trailing.cs | ManageTrail_RunPointBasedTrailing | 100 -> 70 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t1d` | T1.D: FleetSymmetrySync | Trailing.cs | ManageTrail_RunFleetSymmetrySync | 70 -> < 30 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t2a` | T2.A: ProcessOnExecutionUpdate Partition | Execution.cs | ProcessOnExecution_FinalizeFullClose | 120 -> <= 12 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t3a` | T3.A: ResolveFleetSnapshot | Dispatch.cs | Dispatch_ResolveFleetSnapshot | 100 -> 80 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t3b` | T3.B: BuildFollowerOrders | Dispatch.cs | Dispatch_BuildFollowerOrders | 80 -> 60 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t3c` | T3.C: PublishMarketBracketToPhoton | Dispatch.cs | Dispatch_PublishMarketBracketToPhoton | 60 -> 40 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t3c1` | T3.C.1: [Unused/Merged] | - | - | N/A | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t3d` | T3.D: PublishLimitEntryToPhoton | Dispatch.cs | Dispatch_PublishLimitEntryToPhoton | 40 -> < 30 | [x] |
| `ticket:d897fcf5-7eec-48e1-87cc-43d34a8ca7b7/t4` | T4: Final Acceptance | Docs | - | N/A | [x] |

## Close-out section
All gates passed (B1-B6, P1-P3, D1-D5, C1-C8). See `docs/brain/phase6_cyc_report.md` for the final CYC proof.
