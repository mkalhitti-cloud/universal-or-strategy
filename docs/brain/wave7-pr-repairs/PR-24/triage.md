# PR #24 Triage -- wave7/pr5-s5-signals
# S5 Signals & Entries Cluster
# Lane: L5 | Phase 7 Lane Orchestrator

## Bot Sources
- poll_all_bots.py run: 3 bots ACTION_REQUIRED (coderabbitai, greptile-apps, cubic-dev-ai)
- Greptile trial ended: treated INFORMATIONAL
- Sourcery: INFORMATIONAL (no actionable findings)
- Gemini: INFORMATIONAL (all medium findings)

## Findings Triage

| ID | Source | File | Line | Classification | Rationale |
|----|--------|------|------|---------------|-----------|
| REPAIR-F1 | Gemini/Greptile/CodeRabbit | MOMO.cs:70, OR.cs:62,106, Retest.cs:138,392 | Multiple | VALID-DNA | DateTime.Now banned per OKF FSM determinism rule. All 5 instances replaced. |
| REPAIR-F2 | Gemini/Cubic | BarUpdate.cs:73 | 73 | INFORMATIONAL | MaybeRunDailySummary throttle bypass when complianceAccounts.Count==0 -- MaybeFinalizeDailySummaries has its own internal lastDailySummaryCheck update. When accounts=0 nothing runs but next bar re-enters; no correctness regression. |
| REPAIR-F3 | Greptile P1 | MOMO.cs:85 | 85 | VALID-LOGIC-BUG | IndexOf returns -1 -> Substring(0,-1) throws ArgumentOutOfRangeException. Fixed by replacing with direction ternary already used in BuildMOMOPositionInfo. |
| REPAIR-F4 | Greptile P0 | BarUpdate.cs:94 | 94 | INFRA-NOISE | "Magic numeric literals" 30s and 10min -- these are domain constants in extracted helpers. Not a JS rule violation (no unnamed array indexes). |
| REPAIR-F5 | Cubic/CodeAnt | FFMA.cs:322 | 322 | VALID-MECHANICAL | Comment says "out params" but signature uses `ref`. Fixed: "out" -> "ref". |
| REPAIR-F6 | CodeRabbit | FFMA.cs:544, MOMO.cs:234, OR.cs:195 | Multiple | HALLUCINATION | CSharpier check on branch reports 0 issues. Bot hallucinated brace violations. |
| REPAIR-F7 | Greptile P1 | FFMA.cs:344 | 340-343 | HALLUCINATION | Bot claims stopDistance<=0 guard is unreachable. It is NOT dead -- ATR calc can produce 0 independently of the tickSize*2 guard above it. |
| REPAIR-F8 | CodeRabbit (outside diff) | BarUpdate.cs:110 | 110 | VALID-MECHANICAL | sessionEndTime accepted as ProcessSessionReset param but never read in method body. Removed from signature and call-site. Local variable retained. |
| REPAIR-F9 | Cubic P2 / CodeAnt | Retest.cs:303 | 303 | VALID-MECHANICAL | DetermineRetestDirection else log says "< {1:F2}" but else handles <=. Fixed: "<" -> "<=" in format string. |
| REPAIR-F10 | CodeRabbit (outside diff) | OR.cs -- duplicate ternary | -- | INFRA-NOISE | BuildOREntryName and GetORSignalName duplicate "ORLong"/"ORShort" ternary. Informational only; both methods serve different purposes (entry name vs signal label). |
| REPAIR-F11 | Sourcery (scripts/) | scripts/wave7_prepush_gate.py | 38,107,120 | INFRA-NOISE | Script file, not src/. Sourcery security + regex findings on gate script are out of scope. |
| REPAIR-F12 | CODEANT | BarUpdate.cs:75 | 75 | INFRA-NOISE | deploy-sync.ps1 reminder -- process note, not a code bug. |
| ALREADY-FIXED | (session note) | MOMO/Retest/OR | -- | ALREADY-FIXED | REPAIR-08: DateTime.UtcNow, _aek966/_aed966 camelCase rename committed in prior session. |

## Counts
- VALID-DNA: 1 (F1)
- VALID-LOGIC-BUG: 1 (F3)
- VALID-MECHANICAL: 3 (F5, F8, F9)
- HALLUCINATION: 2 (F6, F7)
- INFRA-NOISE: 4 (F4, F10, F11, F12)
- INFORMATIONAL: 1 (F2)
- ALREADY-FIXED: 1 (REPAIR-08)

TRIAGE_DONE PR#24 logic=1 mech=3 dna=1 hall=2 noise=4 fixed=1
