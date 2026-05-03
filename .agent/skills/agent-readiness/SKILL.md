---
name: agent-readiness
description: >
  Evaluates the Agent-Machine Interface (AMI) of a repository across 82 criteria. 
  Use this whenever the user asks for a "readiness audit", "agent awareness check", 
  "hardening mission", or wants to reach "Level 6 Readiness". Executes surgical 
  infrastructure upgrades including the "Python Loophole" (Sidecar Telemetry), 
  Security Gates (Gitleaks/Size checks), and Observability (Sentry/PostHog).
---

# Agent Readiness Auditor & Hardener (Level 6)

Objective: Transform any repository into a **Sovereign Substrate (Level 6)** where autonomous agents can operate with 100% confidence.

## 1. Denominator Discovery (Phase 1)
Establish the scope before auditing.
- **Global Scope**: Identify the repository root (`.git`).
- **Application Scope**: Count the number of independently deployable applications ($N$). An app has its own build/deploy config.
- **Normalization**: Total criteria = 44 (Global) + (38 * $N$ Apps).

## 2. The 82 Gates (Phase 2)
Perform a non-destructive audit using `search_symbols`, `search_text`, and `gh`.
- **Style & Validation**: Linter, Formatter, Pre-commit (ASCII/Lock checks).
- **Build System**: AGENTS.md CLI documentation, CI Caching, CD automation.
- **Testing**: Unit/Integration existence, Parallelization, Coverage gates.
- **Documentation**: Mermaid diagrams, Documented Runbooks (Workflows).
- **Dev Environment**: .env.example, .devcontainer/devcontainer.json.
- **Observability**: Structured Logging, Tracing, Metrics, Circuit Breakers.
- **Security**: Branch Protection, Gitleaks, Dependabot/Renovate, Labeler.

## 3. The Hardening Mission (Phase 3)
Execute surgical upgrades to reach 100% (Level 6).
- **The "Python Loophole"**: 
  - If the core kernel is high-performance (C#/Rust/C++), **BANNED** from adding heavy SDKs.
  - Instead, instrument the Python Orchestration layer (`app/`) with Sentry/PostHog.
- **Security Gates**: 
  - Add `gitleaks.yml` workflow.
  - Add 10MB size check to `.git/hooks/pre-commit`.
- **Observability**: Initialize Sentry (Tracing) and PostHog (Feature Flags) in `app/`.
- **Automation**: Add `release-please.yml` and deployment webhooks.
- **Hygiene**: Configure `renovate.json` with `minimumReleaseAge: '7 days'`.

## 4. Reporting Protocol (Phase 4)
Emit report using `store_agent_readiness_report` (if available) or as a Markdown Artifact.
- **Level 4 (<80%)**: Agent-Capable.
- **Level 5 (80-99%)**: Agent-Autonomous.
- **Level 6 (100%)**: Sovereign Substrate.

## 5. Verification
Run the `build_readiness.ps1` script and verify Sentry/PostHog heartbeats before declaring success.
