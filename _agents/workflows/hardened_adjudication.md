---
description: Orchestrator-Hardened Adjudication -- Re-auditing a failed implementation plan without logic drift.
---

Use this workflow when an Arena audit (P5) has failed and requires logic-drift decontamination. It splits the work between the ARCHITECT (P3) and ORCHESTRATOR (P1) to ensure safety.

---

## Phase 0: Forensic Verification (Arena Codex 5.3)

1. **Verify Logical Proof**: P1 tasks **Arena Codex 5.3** (or Sonnet 4.6) with performing a **Reachability Trace**.
   - Input: The offending code site and the failure mode.
   - Task: Trace the state-path from the NinjaTrader/Broker entry point to the vulnerable state.
2. **Generate PoF**: Codex 5.3 must produce a minimal "Proof of Failure" (pseudo-code or state-transition diagram).
3. **Drift Detection**: If Codex 5.3 cannot find a reachability path, the audit finding is flagged as "Logic Drift" and excluded from the P3 Repair Brief.

---

## Phase 1: P3 Revision (Logical Fixes)

1. **Package the Forensic Brief**: P1 extracts legitimate gaps from `arena_audit_matrix.md` and unzipped visualizer data.
2. **Launch Architect Revision**:
   - Start a **NEW** Claude conversation.
   - Handoff only the logic fixes (e.g. site omissions, portability gaps).
   - **BANNED**: Claude must NOT author Section G (Arena Prompt) in this round.
3. **Verify Revision**: P3 updates `implementation_plan.md`. P1 verifies the logic fixes are present.

---

## Phase 2: P1 Hardening (Prompt Authoring)

1. **Read Revised Plan**: P1 views `docs/brain/implementation_plan.md`.
2. **Inject Hardened Section G**: P1 surgically replaces Section G with a prompt that includes:
   - **KNOWN MODEL ERRORS**: Disarms previous hallucinations or drift logic.
   - **BUILD 981 MANDATE**: Explicitly overrides "redundancy" arguments.
   - **GITHUB-FIRST citations**: Forces evidence-based auditing.
3. **Write to Disk**: P1 applies the edit using `replace_file_content`.

---

## Phase 3: Arena Re-Run

1. **Submit to Arena**: Director pastes the P1-authored Section G into the Arena fleet.
2. **Collect Results**: Download $battlezip and extract to `battle_audit_temp/`.
3. **Update Matrix**: P1 updates `docs/arena_audit_matrix.md` with results.

---

## Phase 4: Mandatory Self-Improvement Audit

After every run:

1. Did P3 attempt to "simplify" the Build 981 guards? (Yes -> Refine Section 10).
2. Did the Arena fleet repeat a known error? (Yes -> Harden Section G further).
3. Was the handoff to P1 from P3 correct?

**Commit format:** `workflow(hardened_adjudication): [what was fixed]`
