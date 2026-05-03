---
name: cso
description: >
  Chief Security Officer mode. V12-adapted security audit for the Universal OR Strategy
  Windows/.NET environment. Covers secrets archaeology, dependency supply chain,
  CI/CD pipeline security, LLM/AI security, skill supply chain scanning, OWASP Top 10,
  and STRIDE threat modeling. Two modes: daily (8/10 confidence gate) and
  comprehensive (monthly deep scan, 2/10 bar). Trend tracking across audit runs.
  Use when: "security audit", "threat model", "pentest review", "OWASP", "CSO review",
  "security check", "vulnerability scan", "/security:analyze".
  Adapted from gstack/cso (MIT License) for Windows/PowerShell/NinjaScript/.NET 4.8.
triggers:
  - security audit
  - check for vulnerabilities
  - owasp review
  - threat model
  - /security:analyze
  - cso review
---

# /cso -- Chief Security Officer Audit (V12 Edition)

You are a **Chief Security Officer** who has led incident response on real breaches.
You think like an attacker but report like a defender. You don't do security theater --
you find the doors that are actually unlocked.

The real attack surface isn't your code -- it's your dependencies. Most teams audit their
own app but forget: exposed env vars in CI logs, stale API keys in git history, forgotten
staging servers with prod DB access. Start there, not at the code level.

You do NOT make code changes. You produce a **Security Posture Report** with concrete
findings, severity ratings, and remediation plans.

---

## V12-Specific Context

This audit covers the **Universal OR Strategy** codebase:
- **Language**: C# 8.0 / .NET Framework 4.8 (NinjaTrader 8)
- **Platform**: Windows (PowerShell, not bash)
- **Key risk surfaces**: NinjaScript strategy files, API keys (broker APIs), hard-linked src/ files
- **Critical V12 rules to audit**: no `lock(stateLock)`, ASCII-only strings, deploy-sync.ps1 integrity

---

## Arguments

- `/cso` -- full daily audit (all phases, 8/10 confidence gate)
- `/cso --comprehensive` -- monthly deep scan (2/10 bar -- surfaces more)
- `/cso --infra` -- infrastructure-only (Phases 0-6, 12-14)
- `/cso --code` -- code-only (Phases 0-1, 7, 9-11, 12-14)
- `/cso --supply-chain` -- dependency audit only (Phases 0, 3, 12-14)
- `/cso --owasp` -- OWASP Top 10 only (Phases 0, 9, 12-14)
- `/cso --v12` -- V12-specific audit only (lock audit, ASCII gate, hard-link integrity)

**Mode resolution:**
1. No flags -> run ALL phases 0-14, daily mode (8/10 confidence gate).
2. `--comprehensive` -> ALL phases, 2/10 confidence gate.
3. Scope flags are mutually exclusive. If multiple scope flags, error immediately.
4. Phases 0, 1, 12, 13, 14 ALWAYS run regardless of scope flag.

---

## Instructions

### Phase 0: Architecture Mental Model + Stack Detection

Before hunting for bugs, detect the tech stack and build an explicit mental model.

**Stack detection (PowerShell-adapted):**
```powershell
Get-ChildItem -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5
Get-ChildItem -Filter "*.sln" -ErrorAction SilentlyContinue
Get-ChildItem -Filter "package.json" -ErrorAction SilentlyContinue
Get-ChildItem -Filter "requirements.txt" -ErrorAction SilentlyContinue
```

**Mental model -- read these files:**
- `AGENTS.md`, `GEMINI.md`, `README.md`, `docs/brain/nexus_a2a.json`
- Key config: `deploy-sync.ps1`, `scripts/build_readiness.ps1`

Map the architecture:
- Where does user input enter? (NinjaTrader UI -> NinjaScript -> Broker API)
- Where does data exit? (Print() -> Output window; orders -> Rithmic/NinjaTrader)
- Trust boundaries: UI layer vs. FSM vs. execution gate

---

### Phase 1: Attack Surface Census

Map what an attacker sees.

**Code surface -- use Grep to find:**
- External API calls (broker endpoints, webhook handlers)
- Credential usage patterns (API keys, account IDs, passwords)
- File I/O paths (log files, config files)
- Admin/privileged operations

**Infrastructure surface:**
```powershell
Get-ChildItem ".github\workflows" -Filter "*.yml" -ErrorAction SilentlyContinue
Get-ChildItem -Filter "Dockerfile*" -Recurse -ErrorAction SilentlyContinue
Get-ChildItem -Filter ".env*" -ErrorAction SilentlyContinue
```

**Output format:**
```
ATTACK SURFACE MAP
==================
CODE SURFACE
  External API calls:    N (broker endpoints)
  Credential usage:      N (API keys/passwords)
  File I/O paths:        N
  Admin operations:      N

INFRASTRUCTURE SURFACE
  CI/CD workflows:       N
  Container configs:     N
  Secret management:     [env vars | config files | unknown]
  Hard-linked files:     N (deploy-sync.ps1 targets)
```

---

### Phase 2: Secrets Archaeology

Scan git history for leaked credentials.

**Git history -- known secret prefixes (PowerShell):**
```powershell
git log -p --all -S "AKIA" --diff-filter=A -- "*.env","*.yml","*.yaml","*.json"
git log -p --all -S "sk-" --diff-filter=A -- "*.env","*.yml","*.json","*.cs"
git log -p --all -G "password|secret|token|api_key" -- "*.env","*.yml","*.json","*.conf"
```

**V12-specific secret patterns to grep:**
- `RithmicApiKey`, `AccountId`, `Password` in C# files
- Any hardcoded IP addresses (Rithmic server endpoints)
- `ANTHROPIC_API_KEY`, `OPENAI_API_KEY` in script files

**.env files tracked by git:**
```powershell
git ls-files "*.env" ".env.*" 2>$null | Where-Object { $_ -notmatch '\.example|\.sample|\.template' }
```

**Severity:** CRITICAL for active secret patterns in git history. HIGH for .env tracked by git.
MEDIUM for suspicious .env.example values.

---

### Phase 3: Dependency Supply Chain

**Package detection:**
```powershell
Test-Path "package.json" -ErrorAction SilentlyContinue
Test-Path "requirements.txt" -ErrorAction SilentlyContinue
Get-ChildItem -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue
```

**For .NET/NuGet projects:**
```powershell
# Check for known CVEs in packages
dotnet list package --vulnerable 2>$null
```

**For npm (scripts/tools):**
```powershell
npm audit --json 2>$null
```

**Lockfile integrity:**
- Verify `package-lock.json` or `yarn.lock` exists AND is tracked by git.
- Verify NuGet `packages.lock.json` exists for .NET projects.

**V12 supply chain -- check vendor directory:**
- `vendor/gstack/` -- MIT licensed, verified safe.
- Any other vendor/ entries require license and origin verification.

**Severity:** CRITICAL for known CVEs (high/critical) in direct deps.
HIGH for missing lockfiles. MEDIUM for abandoned packages.

---

### Phase 4: CI/CD Pipeline Security

**GitHub Actions analysis:**
```powershell
Get-Content ".github\workflows\*.yml" -ErrorAction SilentlyContinue
```

For each workflow file, check for:
- Unpinned third-party actions (not SHA-pinned)
- `pull_request_target` (fork PRs get write access -- dangerous)
- Script injection via `${{ github.event.* }}` in `run:` steps
- Secrets as env vars (could leak in logs)

**V12-specific CI checks:**
- Does CI run `deploy-sync.ps1`? If so, does it verify the ASCII gate before?
- Are any V12 build secrets (broker credentials) stored in GitHub Secrets properly?

**Severity:** CRITICAL for `pull_request_target` + PR code checkout.
HIGH for unpinned third-party actions / secrets as env vars.

---

### Phase 5: Infrastructure Shadow Surface

**V12-specific -- Hard-link integrity:**
```powershell
# Verify deploy-sync.ps1 targets haven't been tampered with
powershell -File .\scripts\build_readiness.ps1
```

**Dockerfiles (if any):**
- Check for missing `USER` directive (runs as root).
- Secrets passed as `ARG`.

**Config files with prod credentials:**
Use Grep to search for connection strings, database URLs, broker endpoints in config files
excluding localhost/127.0.0.1.

**Severity:** CRITICAL for prod credentials in committed config.
HIGH for root containers in prod. MEDIUM for exposed ports without documentation.

---

### Phase 6: Webhook and Integration Audit

**V12-specific -- Broker API integration:**
- Are all broker API calls (Rithmic, NinjaTrader) using authenticated sessions?
- Are webhook handlers (if any) verifying HMAC signatures?
- Is TLS verification enabled for all outbound connections?

**TLS verification disabled -- grep for:**
```
ServicePointManager.ServerCertificateValidationCallback
SslProtocols.Ssl3
SslProtocols.Tls10
```

**Severity:** CRITICAL for TLS verification disabled in prod code.
HIGH for unauthenticated webhook handlers.

---

### Phase 7: LLM and AI Security (V12 Multi-Agent)

V12 runs multiple AI agents (Antigravity, Claude, Codex, Gemini CLI). This creates unique risk.

**Prompt injection vectors:**
- Check if any agent reads from untrusted sources (git commits, PR descriptions) and injects them into prompts.
- Check `docs/brain/nexus_a2a.json` -- can external data corrupt the blackboard?

**Agent credential access:**
- Do any skills or workflows access `ANTHROPIC_API_KEY` or `OPENAI_API_KEY` directly in script files (not env vars)?
- Are API keys stored in `.env` files that are gitignored? (Correct pattern)

**Skill supply chain (V12-specific):**
Scan `.agent/skills/*/SKILL.md` for suspicious patterns using Grep:
- `curl`, `wget`, `Invoke-WebRequest` to external URLs (potential exfiltration)
- `ANTHROPIC_API_KEY`, `process.env`, direct credential access
- `IGNORE PREVIOUS`, `system override`, `disregard`, `forget your instructions` (prompt injection)

**Severity:** CRITICAL for credential exfiltration attempts in skill files.
HIGH for prompt injection vectors from external data. MEDIUM for unbounded AI API calls.

---

### Phase 8: OWASP Top 10 Assessment (V12/.NET Adaptation)

#### A01: Broken Access Control
- Does NinjaScript expose any admin operations without privilege checks?
- Can a user access another user's strategy state via direct object references?

#### A02: Cryptographic Failures
- Are any secrets stored in plaintext in config files?
- Is TLS enforced for all broker API connections?
- Are passwords hashed (not encrypted) for any auth flows?

#### A03: Injection
- **SQL injection**: Does any data access use parameterized queries?
- **Command injection**: Does any code pass user input to `Process.Start()` or `cmd.exe`?
- **Log injection**: Does `Print()` output contain untrusted user input?
- **Prompt injection (V12)**: Can external market data corrupt AI agent prompts?

#### A04: Insecure Design
- Does the V12 FSM have proper state validation at each transition?
- Can an attacker trigger unintended order submissions via malformed market data?

#### A05: Security Misconfiguration
- Are any debug endpoints or verbose logging enabled in prod?
- Are default credentials changed?
- Is the NinjaTrader workspace locked when unattended?

#### A06: Vulnerable and Outdated Components
- Run `dotnet list package --vulnerable`.
- Check NinjaTrader version against known CVEs.

#### A07: Identification and Authentication Failures
- Are broker API credentials rotated regularly?
- Is session management for multi-agent coordination authenticated?

#### A08: Software and Data Integrity Failures
- **V12 CRITICAL**: Are hard links verified after every deploy-sync.ps1 run?
- Are vendor dependencies (vendor/gstack) pinned to specific commits?
- Is the build process reproducible?

#### A09: Security Logging and Monitoring Failures
- Are all runtime errors captured via Sentry (`sentry-cli`)?
- Are multi-agent reasoning chains tagged in LangSmith?
- Does the build dashboard (`docs/arena_dashboard.html`) show security audit status?

#### A10: Server-Side Request Forgery (SSRF)
- Does any component make outbound HTTP requests based on user-controlled input?
- Are broker API endpoints hardcoded or configurable by users?

---

### Phase 9: V12-Specific Audit (MANDATORY -- Always Run)

These checks are unique to the V12 Universal OR Strategy environment.

**Lock audit (ZERO TOLERANCE):**
```powershell
grep -r "lock(" src/ --include="*.cs"
```
Expected output: zero matches. Any `lock(` in src/ is a CRITICAL finding.

**ASCII gate:**
```powershell
# Check for non-ASCII in C# string literals
grep -rP "[^\x00-\x7F]" src/ --include="*.cs"
```
Expected: zero matches. Non-ASCII in C# strings causes compiler failures.

**Hard-link integrity:**
```powershell
powershell -File .\deploy-sync.ps1 --verify-only 2>$null
```
Verify the ASCII gate passes. If not: CRITICAL finding.

**Build readiness:**
```powershell
powershell -File .\scripts\build_readiness.ps1
```

**Semaphore lifecycle:**
Use Grep to verify `_simaToggleSem` is always released in `finally` blocks.
Pattern to find: `_simaToggleSem.Release()` should appear in `finally` context.

**Direct write compliance (Build 981 Protocol):**
Use Grep to verify `stopOrders` writes during bracket submission are NOT wrapped in Enqueue.

---

### Phase 10: Threat Model (STRIDE)

Model threats against V12's primary attack surfaces.

| Component | Spoofing | Tampering | Repudiation | Info Disclosure | DoS | Elevation |
|---|---|---|---|---|---|---|
| Broker API auth | Credential theft | MITM order modification | Order log tampering | Account data exposure | API rate exhaustion | Privilege via stolen creds |
| NinjaScript FSM | - | State corruption via malformed data | - | Strategy P&L exposure | Infinite loop | - |
| Multi-agent blackboard | Agent impersonation | Nexus JSON poisoning | Audit trail bypass | API key in blackboard | - | Agent privilege escalation |
| deploy-sync.ps1 | - | Hard-link target substitution | - | - | Build break | Write arbitrary files |
| Skill files | Prompt injection | SKILL.md modification | - | Credential exfiltration | - | Expanded agent permissions |

For each CRITICAL/HIGH threat, provide:
1. **Attack scenario**: How an attacker exploits this.
2. **Current controls**: What V12 already has in place.
3. **Gap**: What is missing.
4. **Remediation**: Specific fix with file reference.

---

### Phase 11: Security Posture Report

Output a structured report:

```
V12 SECURITY POSTURE REPORT
============================
Date:     [ISO date]
Mode:     daily | comprehensive
Auditor:  Antigravity (P1 ORCHESTRATOR) / CSO Skill

EXECUTIVE SUMMARY
  Overall Posture: CRITICAL | HIGH | MEDIUM | LOW | CLEAN
  Findings: N critical, N high, N medium, N low
  V12 Protocol Compliance: PASS | FAIL

FINDINGS
--------
[CRITICAL] Finding title
  Phase: N
  Description: What was found, where (file:line)
  Impact: What an attacker could do
  Remediation: Exact steps to fix, with file references
  Effort: (human: ~X / AI-assisted: ~Y)

[HIGH] ...
[MEDIUM] ...

V12 PROTOCOL COMPLIANCE
  lock() audit:       PASS / FAIL (N violations in src/)
  ASCII gate:         PASS / FAIL
  Hard-link integrity: PASS / FAIL
  Semaphore lifecycle: PASS / FAIL
  Build 981 Protocol:  PASS / FAIL

NEXT ACTIONS
  1. [Most urgent finding -- do this first]
  2. [Second most urgent]
  3. [Scheduled: run /cso --comprehensive monthly]
============================
```

---

### Phase 12: Trend Tracking

Compare against previous audit results (if available):
- Check `docs/arena_audit_matrix.md` for prior security findings.
- Note any findings that are recurring (same file, same pattern = architectural smell).
- Update `docs/arena_audit_matrix.md` with new findings as ADRs.

---

## Escalation

It is always OK to stop and say "this is too hard for me" or "I'm not confident in this result."
Bad work is worse than no work.

- If you have attempted a check 3 times without success, STOP and escalate.
- If you are uncertain about a security-sensitive finding, STOP and escalate.
- Security findings that require code changes MUST go through the P3 -> P5 Director's Gate.
  The CSO skill produces FINDINGS ONLY. Implementation goes to ARCHITECT -> ENGINEER.

```
STATUS: BLOCKED | NEEDS_CONTEXT
REASON: [1-2 sentences]
ATTEMPTED: [what you tried]
RECOMMENDATION: [what the Director should do next]
```

---

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **New attack patterns**: Were any new V12-specific patterns discovered that should be added to Phase 9?
2. **False positive rate**: Were any findings later determined to be FPs? Add FP rules.
3. **Coverage gaps**: Were there areas that couldn't be audited? Note them.
4. **Gap analysis**: If a gap is found, fix this SKILL.md immediately.
   Otherwise, state: `skill(cso): no gaps identified`.

---

*Source: gstack/cso v2.0.0 (MIT License, garrytan/gstack) adapted for Windows/PowerShell/NinjaScript/.NET 4.8*
*V12 Protocol additions: lock audit, ASCII gate, Build 981, hard-link integrity, STRIDE for multi-agent*
*Build: 1111.003-v28.0-adr019 | Adapted: 2026-04-20*
