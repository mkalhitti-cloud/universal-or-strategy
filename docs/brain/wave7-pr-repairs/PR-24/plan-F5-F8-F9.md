# PR-24 Repair Plan -- Findings F5, F8, F9 (VALID-MECHANICAL batch)

Branch: wave7/pr5-s5-signals
Cluster: S5 Signals & Entries

## Verification notes

| Finding | File | Confirmed? | Notes |
|---------|------|------------|-------|
| F5 | src/V12_002.Entries.FFMA.cs:322 | YES | Comment says "out params"; signature uses `ref`. Single-word fix. |
| F8 | src/V12_002.BarUpdate.cs:110 | YES | `sessionEndTime` param accepted but never read inside body (lines 113-128). Call-site at line 325 passes it. `sessionEndTime` local (line 309) still needed at line 315 -- only remove from signature + call arg. |
| F9 | src/V12_002.Entries.Retest.cs:303 | YES | `else` branch covers price <= sessionMid but format string says `<`. Single-char fix. |

All three are clean mechanical changes. None classified INFRA-NOISE.

---

```PLAN
# ── FINDING F5 ──────────────────────────────────────────────────────────────
file: src/V12_002.Entries.FFMA.cs
old_text: |
        // Returns false when caller must abort; writes validated values back via out params.
new_text: |
        // Returns false when caller must abort; writes validated values back via ref params.
rationale: per production-engineering-billions.md (manifest_logging / accuracy): comment
  described parameters as "out params" but the method signature uses ref; misleading
  comments create operational confusion identical to misleading log messages.
build_impact: none
okf_doc_read: production-engineering-billions.md -- manifest_logging; OKF rule
  "Make illegal states unrepresentable" applied to documentation accuracy.

# ── FINDING F8 ──────────────────────────────────────────────────────────────
# Step 1 -- remove unused parameter from method signature
file: src/V12_002.BarUpdate.cs
old_text: |
        private void ProcessSessionReset(
            DateTime barTimeInZone,
            TimeSpan currentTime,
            TimeSpan sessionStartTime,
            TimeSpan sessionEndTime,
            bool sessionCrossesMidnight
        )
new_text: |
        private void ProcessSessionReset(
            DateTime barTimeInZone,
            TimeSpan currentTime,
            TimeSpan sessionStartTime,
            bool sessionCrossesMidnight
        )
rationale: per complexity-reduction.md: removing an accepted-but-never-read parameter
  eliminates a dead API surface. sessionEndTime is still computed locally at line 309
  and consumed at line 315 for sessionCrossesMidnight -- only the redundant argument
  passing into ProcessSessionReset is removed.
build_impact: minor  # call-site must also be updated (Step 2 below)
okf_doc_read: complexity-reduction.md -- dead parameter removal; production-engineering-billions.md
  -- manifest_logging (accurate interfaces).

# Step 2 -- remove the matching argument at the call-site
file: src/V12_002.BarUpdate.cs
old_text: |
                ProcessSessionReset(
                    barTimeInZone,
                    currentTime,
                    sessionStartTime,
                    sessionEndTime,
                    sessionCrossesMidnight
                );
new_text: |
                ProcessSessionReset(
                    barTimeInZone,
                    currentTime,
                    sessionStartTime,
                    sessionCrossesMidnight
                );
rationale: companion change to Step 1 -- call-site must match updated signature.
  sessionEndTime local variable (line 309) is retained; it is still consumed at
  line 315 (sessionCrossesMidnight = sessionEndTime < sessionStartTime).
build_impact: none  # after Step 1 applied; both changes together compile cleanly.
okf_doc_read: complexity-reduction.md -- dead parameter removal.

# ── FINDING F9 ──────────────────────────────────────────────────────────────
file: src/V12_002.Entries.Retest.cs
old_text: |
                        "RETEST: Price below OR Mid ({0:F2} < {1:F2}) = SHORT at OR Low {2:F2}",
new_text: |
                        "RETEST: Price below OR Mid ({0:F2} <= {1:F2}) = SHORT at OR Low {2:F2}",
rationale: per how-to-build-an-exchange.md (determinism / sidecar_lifecycle): the else
  branch fires for currentPrice <= sessionMid (equality included); the log message used
  "<" which is factually wrong when price equals sessionMid, creating operational
  confusion in trade audit logs.
build_impact: none
okf_doc_read: how-to-build-an-exchange.md -- determinism, correctness_by_construction;
  OKF rule "Make illegal states unrepresentable" applied to log message accuracy.
```
