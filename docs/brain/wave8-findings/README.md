# Wave 8 Findings

This directory holds per-finding artifacts for Wave 8.
One subdirectory per DD entry: docs/brain/wave8-findings/DD-{ID}/

## Structure Per Finding

  scan.md   -- wave8-scan output: violation confirmed, blast radius, fix recommendation
  plan.md   -- v12-phase2-architecture output: detailed fix plan
  verify.md -- v12-phase5-v-verify output: post-fix verification report

## Lane Map

| Lane | Class | Findings | Violation Type | OKF Rule |
|------|-------|----------|----------------|----------|
| L1 | A | DD-001,013,014,015,019,020 | DateTime.Now | Rule 3 |
| L2 | A | DD-008,009,016,017 | Account.All missing .ToArray() | Rule 5 |
| L3 | A | DD-007,010 | Silent catch / error drop | Rule 5 |
| L4 | B | DD-005 | Null dereference guard | Rule 5 |
| L5 | B | DD-011,018 | Whitelist divergence | Rule 5 |
| L6 | B | DD-002,003,004,006,012 | SA1503/SA1204 style | Rule 6 |

## Status

Wave 8 initialized. Baseline: 338/338 tests, 0 CYC>8, 0 lock() violations.
See .lamport/wave8/event_log.jsonl for Lamport clock.
