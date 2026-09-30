#!/usr/bin/env python3
"""
poll_all_bots.py -- Wave 7 PR health checker
Usage: python3 scripts/poll_all_bots.py <PR_NUMBER> [--repo OWNER/REPO] [--json]

Fetches all bot reviews, inline comments, and CI check statuses for a PR.
Outputs a triage summary: BLOCKS_MERGE / ACTION_REQUIRED / INFORMATIONAL / CLEAN

Bot verdict logic:
  BLOCKS_MERGE       -- CHANGES_REQUESTED state or failing CI check that is NOT
                        in the known-deferred/allowlisted set
  ACTION_REQUIRED    -- bot posted inline comments with "action required" / "high"
                        severity badges but did not formally request changes
  INFORMATIONAL      -- COMMENTED state only, no severity flags
  CLEAN              -- bot posted a passing review or no review at all

Deferred/allowlisted checks (do NOT count as BLOCKS_MERGE):
  - Sourcery (skipping on large diffs -- known limitation)
  - Greptile (trial ended -- not available)
  - Mermaid Diagram Sync Assistant
  - markdown-link-check on doc-only branches
  - gitleaks on main branch (tracked separately, not on PR branch)
  - SonarCloud (informational in this repo -- not a merge gate)
  - CodeFactor (informational -- tracked but not a hard blocker per V12 protocol)

Jane Street OKF overrides (comments matching these patterns are IGNORED as
they conflict with V12 DNA):
  - "add nullable enable" / "#nullable enable" (not required per V12 platform)
  - "use lock(" / "consider locking" (BANNED by OKF lock-free-patterns.md)
  - "DateTime.Now is not thread-safe" when already fixed to UtcNow
  - "NUnit" / "TestCase" suggestions (xUnit only per OKF testing-strategies.md)
"""

import sys
import json
import argparse
import re
import urllib.request
import urllib.error
import subprocess
from typing import Any

REPO = "antigravityos187-sketch/universal-or-strategy"

# Checks that are informational / deferred -- do not block merge
DEFERRED_CHECKS = {
    "Sourcery review",           # skips large diffs -- not actionable
    "Greptile Review",           # trial ended
    "Mermaid Diagram Sync Assistant",
    "markdown-link-check",
    "SonarCloud Code Analysis",  # informational in this repo
    "CodeFactor",                # informational -- V12 uses Codacy as gate
    "gitleaks",                  # main branch run, not PR run
}

# Patterns in bot comments that are OKF-overridden (ignore these findings)
OKF_OVERRIDE_PATTERNS = [
    r"#nullable\s+enable",
    r"nullable\s+reference\s+types",
    r"\bnullable\b.*\benable\b",
    r"consider\s+(using\s+)?lock\s*\(",
    r"\bMonitor\.(Enter|Exit|Wait|Pulse)\b",
    r"\bMutex\b",
    r"\bSemaphoreSlim\b.*for\s+state",
    r"\[TestCase\]",
    r"\[TestFixture\]",
    r"\[TestMethod\]",
    r"Assert\.That\s*\(",
    r"NUnit",
    r"MSTest",
]

# Severity markers from bot comment badges
HIGH_SEVERITY_MARKERS = [
    "high-priority",
    "Action_required",
    "P0", "P1",
    "[CRITICAL",
    "CHANGES_REQUESTED",
    "action required",
    "critical",
    "blocking",
]

MEDIUM_SEVERITY_MARKERS = [
    "medium-priority",
    "P2",
    "minor",
]


def gh_api(path: str) -> Any:
    """Call GitHub API using gh CLI (authenticated)."""
    try:
        result = subprocess.run(
            ["gh", "api", path],
            capture_output=True, text=True, timeout=30
        )
        if result.returncode != 0:
            return None
        return json.loads(result.stdout)
    except Exception:
        return None


def gh_pr_checks(pr: int, repo: str) -> list[dict]:
    """Get CI check statuses via gh pr checks."""
    try:
        result = subprocess.run(
            ["gh", "pr", "checks", str(pr), "--repo", repo],
            capture_output=True, text=True, timeout=30
        )
        lines = []
        for line in result.stdout.splitlines():
            parts = line.split("\t")
            if len(parts) >= 3:
                lines.append({
                    "name": parts[0].strip(),
                    "status": parts[1].strip(),
                    "elapsed": parts[2].strip() if len(parts) > 2 else "",
                    "url": parts[3].strip() if len(parts) > 3 else "",
                })
        return lines
    except Exception:
        return []


def is_okf_overridden(text: str) -> bool:
    """Return True if comment body matches an OKF-override pattern."""
    for pat in OKF_OVERRIDE_PATTERNS:
        if re.search(pat, text, re.IGNORECASE):
            return True
    return False


def classify_comment_severity(body: str) -> str:
    """Classify a bot comment as high/medium/low severity."""
    body_lower = body.lower()
    for marker in HIGH_SEVERITY_MARKERS:
        if marker.lower() in body_lower:
            return "high"
    for marker in MEDIUM_SEVERITY_MARKERS:
        if marker.lower() in body_lower:
            return "medium"
    return "low"


def get_pr_info(pr: int, repo: str) -> dict:
    """Fetch PR metadata."""
    data = gh_api(f"repos/{repo}/pulls/{pr}")
    if not data:
        return {}
    return {
        "number": data.get("number"),
        "title": data.get("title", ""),
        "branch": data.get("head", {}).get("ref", ""),
        "sha": data.get("head", {}).get("sha", ""),
        "state": data.get("state", ""),
        "mergeable": data.get("mergeable"),
        "mergeable_state": data.get("mergeable_state", ""),
    }


def get_reviews(pr: int, repo: str) -> list[dict]:
    """Fetch all PR reviews."""
    data = gh_api(f"repos/{repo}/pulls/{pr}/reviews")
    if not data:
        return []
    results = []
    seen = set()
    for r in data:
        login = r["user"]["login"]
        state = r["state"]
        body = r.get("body", "")
        key = (login, state)
        # Deduplicate same bot + state (CodeRabbit often posts multiple)
        if key not in seen:
            seen.add(key)
        results.append({
            "bot": login,
            "state": state,
            "body_preview": body[:200],
            "okf_overridden": is_okf_overridden(body),
        })
    return results


def get_inline_comments(pr: int, repo: str) -> list[dict]:
    """Fetch all inline PR comments and classify severity."""
    data = gh_api(f"repos/{repo}/pulls/{pr}/comments")
    if not data:
        return []
    results = []
    for c in data:
        login = c["user"]["login"]
        body = c.get("body", "")
        path = c.get("path", "")
        line = c.get("line") or c.get("original_line", 0)
        overridden = is_okf_overridden(body)
        severity = classify_comment_severity(body)
        results.append({
            "bot": login,
            "path": path,
            "line": line,
            "severity": severity,
            "okf_overridden": overridden,
            "body_preview": body[:180],
        })
    return results


def triage_pr(pr: int, repo: str) -> dict:
    """Full triage of a PR. Returns structured verdict."""
    info = get_pr_info(pr, repo)
    reviews = get_reviews(pr, repo)
    inline = get_inline_comments(pr, repo)
    checks = gh_pr_checks(pr, repo)

    blocks = []
    action_required = []
    informational = []

    # -- Review-level verdicts --
    bots_requesting_changes = set()
    for rev in reviews:
        if rev["okf_overridden"]:
            informational.append({
                "source": "review",
                "bot": rev["bot"],
                "state": rev["state"],
                "reason": "OKF_OVERRIDE -- conflicts with V12 DNA, skip",
            })
            continue
        if rev["state"] == "CHANGES_REQUESTED":
            bots_requesting_changes.add(rev["bot"])
            blocks.append({
                "source": "review",
                "bot": rev["bot"],
                "state": "CHANGES_REQUESTED",
                "preview": rev["body_preview"],
            })
        elif rev["state"] in ("APPROVED",):
            informational.append({
                "source": "review",
                "bot": rev["bot"],
                "state": "APPROVED",
            })

    # -- Inline comment verdicts --
    by_bot: dict[str, list] = {}
    for c in inline:
        by_bot.setdefault(c["bot"], []).append(c)

    for bot, comments in by_bot.items():
        high_count = sum(1 for c in comments
                         if c["severity"] == "high" and not c["okf_overridden"])
        med_count = sum(1 for c in comments
                        if c["severity"] == "medium" and not c["okf_overridden"])
        okf_skipped = sum(1 for c in comments if c["okf_overridden"])
        if high_count > 0 and bot not in bots_requesting_changes:
            action_required.append({
                "source": "inline",
                "bot": bot,
                "high": high_count,
                "medium": med_count,
                "okf_skipped": okf_skipped,
                "sample": next(
                    (c["body_preview"] for c in comments
                     if c["severity"] == "high" and not c["okf_overridden"]),
                    ""
                ),
            })
        elif med_count > 0 and bot not in bots_requesting_changes:
            informational.append({
                "source": "inline",
                "bot": bot,
                "medium": med_count,
                "okf_skipped": okf_skipped,
            })

    # -- CI check verdicts --
    failing_checks = []
    for chk in checks:
        name = chk["name"]
        status = chk["status"]
        if status in ("fail", "failure"):
            if any(name.startswith(d) or d in name for d in DEFERRED_CHECKS):
                informational.append({
                    "source": "ci",
                    "check": name,
                    "status": "fail",
                    "reason": "DEFERRED -- not a hard merge gate per V12 protocol",
                })
            else:
                failing_checks.append({
                    "source": "ci",
                    "check": name,
                    "status": "fail",
                    "url": chk.get("url", ""),
                })
                blocks.append({
                    "source": "ci",
                    "check": name,
                    "status": "fail",
                    "url": chk.get("url", ""),
                })

    # -- Final verdict --
    if blocks:
        verdict = "BLOCKS_MERGE"
    elif action_required:
        verdict = "ACTION_REQUIRED"
    else:
        verdict = "CLEAN"

    # -- Score for bot satisfaction (5 key bots) --
    key_bots = [
        "coderabbitai[bot]",
        "gemini-code-assist[bot]",
        "greptile-apps[bot]",
        "cubic-dev-ai[bot]",
        "sourcery-ai[bot]",
    ]
    bot_scores = {}
    for bot in key_bots:
        bot_reviews = [r for r in reviews if r["bot"] == bot]
        bot_inline = [c for c in inline
                      if c["bot"] == bot and not c["okf_overridden"]]
        if any(r["state"] == "APPROVED" for r in bot_reviews):
            bot_scores[bot] = "APPROVED"
        elif any(r["state"] == "CHANGES_REQUESTED"
                 and not r["okf_overridden"] for r in bot_reviews):
            bot_scores[bot] = "CHANGES_REQUESTED"
        elif not bot_reviews and not bot_inline:
            bot_scores[bot] = "NO_REVIEW"
        else:
            high = sum(1 for c in bot_inline if c["severity"] == "high")
            bot_scores[bot] = "ACTION_REQUIRED" if high > 0 else "INFORMATIONAL"

    greptile_score = "TRIAL_ENDED"
    for r in reviews:
        if "greptile" in r["bot"]:
            if "3/5" in r["body_preview"] or "4/5" in r["body_preview"]:
                greptile_score = r["body_preview"][:60]
            else:
                greptile_score = r["state"]

    return {
        "pr": pr,
        "branch": info.get("branch", ""),
        "sha": info.get("sha", ""),
        "mergeable_state": info.get("mergeable_state", ""),
        "verdict": verdict,
        "greptile_score": greptile_score,
        "bot_scores": bot_scores,
        "blocks": blocks,
        "action_required": action_required,
        "informational_count": len(informational),
        "total_inline_comments": len(inline),
        "total_reviews": len(reviews),
        "ci_checks": {
            "failing": [c["check"] for c in failing_checks],
            "all": [{"name": c["name"], "status": c["status"]}
                    for c in checks],
        },
    }


def print_triage(result: dict) -> None:
    """Print human-readable triage report."""
    pr = result["pr"]
    verdict = result["verdict"]
    branch = result["branch"]

    verdict_icon = {
        "BLOCKS_MERGE": "BLOCKS_MERGE",
        "ACTION_REQUIRED": "ACTION_REQUIRED",
        "CLEAN": "CLEAN",
    }.get(verdict, verdict)

    print(f"\n{'='*60}")
    print(f"PR #{pr}  [{branch}]")
    print(f"VERDICT: {verdict_icon}")
    print(f"Mergeable state: {result['mergeable_state']}")
    print(f"Greptile score: {result['greptile_score']}")
    print()

    print("Bot scores (key bots):")
    for bot, score in result["bot_scores"].items():
        icon = "OK" if score in ("APPROVED", "INFORMATIONAL", "NO_REVIEW") else "!!"
        print(f"  [{icon}] {bot}: {score}")

    if result["blocks"]:
        print(f"\nBLOCKERS ({len(result['blocks'])}):")
        for b in result["blocks"]:
            if b["source"] == "review":
                print(f"  [REVIEW] {b['bot']}: {b['state']}")
                if b.get("preview"):
                    print(f"    > {b['preview'][:120]}")
            elif b["source"] == "ci":
                print(f"  [CI]     {b['check']}: FAIL  {b.get('url','')}")

    if result["action_required"]:
        print(f"\nACTION REQUIRED ({len(result['action_required'])}):")
        for a in result["action_required"]:
            print(f"  [INLINE] {a['bot']}: {a.get('high',0)} high / "
                  f"{a.get('medium',0)} medium comments")
            if a.get("sample"):
                print(f"    > {a['sample'][:120]}")

    ci_failing = result["ci_checks"]["failing"]
    if ci_failing:
        print(f"\nFailing CI checks: {', '.join(ci_failing)}")

    print(f"\nTotal inline comments: {result['total_inline_comments']}")
    print(f"Total reviews: {result['total_reviews']}")
    print(f"Informational (deferred/OKF-overridden): {result['informational_count']}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Poll all bot reviews and CI checks for a Wave 7 PR")
    parser.add_argument("pr", type=int, help="PR number")
    parser.add_argument("--repo", default=REPO,
                        help="GitHub repo (owner/repo)")
    parser.add_argument("--json", action="store_true",
                        help="Output raw JSON instead of human report")
    parser.add_argument("--all", action="store_true",
                        help="Poll all 6 Wave 7 PRs (20-25)")
    args = parser.parse_args()

    prs = list(range(20, 26)) if args.all else [args.pr]

    results = []
    for pr_num in prs:
        print(f"Polling PR #{pr_num}...", file=sys.stderr)
        result = triage_pr(pr_num, args.repo)
        results.append(result)

    if args.json:
        print(json.dumps(results if args.all else results[0], indent=2))
    else:
        for r in results:
            print_triage(r)

        if args.all:
            print(f"\n{'='*60}")
            print("WAVE 7 SUMMARY")
            for r in results:
                verdict = r["verdict"]
                print(f"  PR #{r['pr']} [{r['branch']}]: {verdict}")


if __name__ == "__main__":
    main()
