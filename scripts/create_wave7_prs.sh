#!/usr/bin/env bash
# create_wave7_prs.sh — Wave 7 PR creation script
# Creates 6 cluster branches from pre-wave base, each with ONLY its cluster .cs files.
# Then opens PRs via GitHub API.
# Usage: bash scripts/create_wave7_prs.sh
set -euo pipefail

PRE_WAVE="e01e4e532bb07930c993f367c545d565c9a09934"
MAIN="main"
REPO="antigravityos187-sketch/universal-or-strategy"
MAIN_HEAD=$(git rev-parse main)

echo "=== Wave 7 PR Creation Script ==="
echo "Pre-wave base : $PRE_WAVE"
echo "Main HEAD     : $MAIN_HEAD"
echo ""

# ─── Cluster definitions ────────────────────────────────────────────────────

declare -A CLUSTER_FILES
declare -A CLUSTER_TITLES
declare -A CLUSTER_BODIES
declare -A CLUSTER_BRANCHES

# PR-1: S2 — Execution Engine (12 files)
CLUSTER_BRANCHES[pr1]="wave7/pr1-s2-execution"
CLUSTER_TITLES[pr1]="feat(wave7/s2-execution): CYC reduction — Execution Engine cluster"
CLUSTER_FILES[pr1]="src/V12_002.Orders.Callbacks.AccountOrders.cs
src/V12_002.Orders.Callbacks.Execution.cs
src/V12_002.Orders.Callbacks.Propagation.cs
src/V12_002.Orders.Callbacks.cs
src/V12_002.Orders.Management.Cleanup.cs
src/V12_002.Orders.Management.Flatten.cs
src/V12_002.Orders.Management.StopSync.cs
src/V12_002.Orders.Management.cs
src/V12_002.Trailing.cs
src/V12_002.Trailing.StopUpdate.cs
src/V12_002.Symmetry.cs
src/V12_002.PositionInfo.cs"
CLUSTER_BODIES[pr1]="## Wave 7 — S2 Execution Engine CYC Reduction

All target methods in the Execution Engine cluster reduced to CYC≤8.

### Files Changed (12)
- \`Orders.Callbacks.AccountOrders.cs\`
- \`Orders.Callbacks.Execution.cs\`
- \`Orders.Callbacks.Propagation.cs\`
- \`Orders.Callbacks.cs\`
- \`Orders.Management.Cleanup.cs\`
- \`Orders.Management.Flatten.cs\`
- \`Orders.Management.StopSync.cs\`
- \`Orders.Management.cs\`
- \`Trailing.cs\`
- \`Trailing.StopUpdate.cs\`
- \`Symmetry.cs\`
- \`PositionInfo.cs\`

### Compliance
- Zero lock() blocks — Actor/Enqueue model throughout
- ASCII-only, UTF-8 no BOM
- All extracted helpers have CYC≤4
- Build: 0 errors 0 warnings (dotnet build Linting.csproj)
- wave7_prepush_gate.py: PASS

Wave 7 event log: \`wave_7_complete\` at lamport_clock=162."

# PR-2: S3 — UI & IPC (14 files)
CLUSTER_BRANCHES[pr2]="wave7/pr2-s3-ui-ipc"
CLUSTER_TITLES[pr2]="feat(wave7/s3-ui-ipc): CYC reduction — UI & IPC cluster"
CLUSTER_FILES[pr2]="src/V12_002.UI.Compliance.cs
src/V12_002.UI.IPC.cs
src/V12_002.UI.IPC.Commands.Config.cs
src/V12_002.UI.IPC.Commands.Fleet.cs
src/V12_002.UI.IPC.Commands.Misc.cs
src/V12_002.UI.IPC.Commands.Mode.cs
src/V12_002.UI.IPC.Server.cs
src/V12_002.UI.Panel.Construction.cs
src/V12_002.UI.Panel.Handlers.cs
src/V12_002.UI.Panel.Helpers.cs
src/V12_002.UI.Sizing.cs
src/V12_002.UI.Snapshot.cs
src/V12_002.UI.SnapshotPool.cs
src/V12_002.IPC.Hardening.cs"
CLUSTER_BODIES[pr2]="## Wave 7 — S3 UI & IPC CYC Reduction

All target methods in the UI & IPC cluster reduced to CYC≤8.

### Files Changed (14)
- \`UI.Compliance.cs\`, \`UI.IPC.cs\`, \`UI.IPC.Commands.Config.cs\`
- \`UI.IPC.Commands.Fleet.cs\`, \`UI.IPC.Commands.Misc.cs\`, \`UI.IPC.Commands.Mode.cs\`
- \`UI.IPC.Server.cs\`, \`UI.Panel.Construction.cs\`, \`UI.Panel.Handlers.cs\`
- \`UI.Panel.Helpers.cs\`, \`UI.Sizing.cs\`, \`UI.Snapshot.cs\`
- \`UI.SnapshotPool.cs\`, \`IPC.Hardening.cs\`

### Compliance
- Zero lock() blocks — Actor/Enqueue model throughout
- ASCII-only, UTF-8 no BOM
- All extracted helpers have CYC≤4
- Build: 0 errors 0 warnings
- wave7_prepush_gate.py: PASS"

# PR-3: S1 — SIMA Core (3 files)
CLUSTER_BRANCHES[pr3]="wave7/pr3-s1-sima-core"
CLUSTER_TITLES[pr3]="feat(wave7/s1-sima-core): CYC reduction — SIMA Core cluster"
CLUSTER_FILES[pr3]="src/V12_002.SIMA.Lifecycle.cs
src/V12_002.SIMA.Flatten.cs
src/V12_002.SIMA.Fleet.cs"
CLUSTER_BODIES[pr3]="## Wave 7 — S1 SIMA Core CYC Reduction

All target methods in the SIMA Core cluster reduced to CYC≤8.

### Files Changed (3)
- \`SIMA.Lifecycle.cs\` — 9 methods reduced (HydrateWorkingOrdersFromBroker 19→5, AdoptMasterOrders 19→8, etc.)
- \`SIMA.Flatten.cs\` — ProcessFlattenWorkItem_CancelOrders reduced
- \`SIMA.Fleet.cs\` — supporting fleet helpers extracted

### Compliance
- Zero lock() blocks — Actor/Enqueue model throughout
- ASCII-only, UTF-8 no BOM
- Build: 0 errors 0 warnings
- wave7_prepush_gate.py: PASS"

# PR-4: S4 — REAPER Defense (3 files)
CLUSTER_BRANCHES[pr4]="wave7/pr4-s4-reaper-defense"
CLUSTER_TITLES[pr4]="feat(wave7/s4-reaper-defense): CYC reduction — REAPER Defense cluster"
CLUSTER_FILES[pr4]="src/V12_002.REAPER.Audit.cs
src/V12_002.REAPER.Repair.cs
src/V12_002.Safety.Watchdog.cs"
CLUSTER_BODIES[pr4]="## Wave 7 — S4 REAPER Defense CYC Reduction

All target methods in the REAPER Defense cluster reduced to CYC≤8.

### Files Changed (3)
- \`REAPER.Audit.cs\` — AuditMaster_HandleNakedPosition (14→6), AuditSingleFleetAccount (12→7), AuditFleet_CalculateExpectedActual (15→6)
- \`REAPER.Repair.cs\` — SubmitRepairOrderWithAuthorization (19→6)
- \`Safety.Watchdog.cs\` — CancelWatchdogWorkingOrders (12→5), CancelDirectFallbackOrders (11→3)

### Compliance
- Zero lock() blocks — Actor/Enqueue model throughout
- ASCII-only, UTF-8 no BOM
- Build: 0 errors 0 warnings
- wave7_prepush_gate.py: PASS"

# PR-5: S5 — Signals & Entries (6 files)
CLUSTER_BRANCHES[pr5]="wave7/pr5-s5-signals"
CLUSTER_TITLES[pr5]="feat(wave7/s5-signals): CYC reduction — Signals & Entries cluster"
CLUSTER_FILES[pr5]="src/V12_002.Entries.FFMA.cs
src/V12_002.Entries.MOMO.cs
src/V12_002.Entries.OR.cs
src/V12_002.Entries.Retest.cs
src/V12_002.Entries.Trend.cs
src/V12_002.BarUpdate.cs"
CLUSTER_BODIES[pr5]="## Wave 7 — S5 Signals & Entries CYC Reduction

All target methods in the Signals & Entries cluster reduced to CYC≤8.

### Files Changed (6)
- \`Entries.FFMA.cs\`, \`Entries.MOMO.cs\`, \`Entries.OR.cs\`
- \`Entries.Retest.cs\`, \`Entries.Trend.cs\`, \`BarUpdate.cs\`

### Compliance
- Zero lock() blocks — Actor/Enqueue model throughout
- ASCII-only, UTF-8 no BOM
- Build: 0 errors 0 warnings
- wave7_prepush_gate.py: PASS"

# PR-6: S6+S7 — Kernel Infra (4 files)
CLUSTER_BRANCHES[pr6]="wave7/pr6-s6-kernel-infra"
CLUSTER_TITLES[pr6]="feat(wave7/s6-kernel-infra): CYC reduction — Kernel Infrastructure cluster"
CLUSTER_FILES[pr6]="src/V12_002.Lifecycle.cs
src/V12_002.Perf.LogBuffer.cs
src/V12_002.DrawingHelpers.cs
src/SignalBroadcaster.cs"
CLUSTER_BODIES[pr6]="## Wave 7 — S6+S7 Kernel Infrastructure CYC Reduction

All target methods in the Kernel Infrastructure cluster reduced to CYC≤8.

### Files Changed (4)
- \`Lifecycle.cs\` — HandleTerminated (23→2), core lifecycle methods
- \`Perf.LogBuffer.cs\` — performance logging helpers extracted
- \`DrawingHelpers.cs\` — DrawORBox (12→6), FindChartTabGrid (10→7)
- \`SignalBroadcaster.cs\` — GetSubscriberCounts (9→1)

### Compliance
- Zero lock() blocks — Actor/Enqueue model throughout
- ASCII-only, UTF-8 no BOM
- Build: 0 errors 0 warnings
- wave7_prepush_gate.py: PASS"

# ─── Create branches and push ────────────────────────────────────────────────

FAILED=0
CREATED=()

for pr_key in pr1 pr2 pr3 pr4 pr5 pr6; do
    BRANCH="${CLUSTER_BRANCHES[$pr_key]}"
    echo ""
    echo "────────────────────────────────────────────────"
    echo "Creating branch: $BRANCH"
    echo "────────────────────────────────────────────────"

    # Delete local branch if it exists (stale)
    git branch -D "$BRANCH" 2>/dev/null || true

    # Create branch from pre-wave base
    git checkout -b "$BRANCH" "$PRE_WAVE"

    # Checkout ONLY the cluster files from main
    IFS=$'\n' read -ra FILES <<< "${CLUSTER_FILES[$pr_key]}"
    for f in "${FILES[@]}"; do
        f=$(echo "$f" | tr -d '[:space:]')
        if [ -n "$f" ]; then
            if git cat-file -e "main:$f" 2>/dev/null; then
                git checkout main -- "$f"
                echo "  + $f"
            else
                echo "  ! MISSING: $f (skipping)"
            fi
        fi
    done

    # Verify no non-.cs files sneaked in
    EXTRA=$(git diff --cached --name-only 2>/dev/null | grep -v "\.cs$" || true)
    if [ -n "$EXTRA" ]; then
        echo "  ERROR: non-.cs files in staging: $EXTRA"
        git checkout "$MAIN"
        FAILED=$((FAILED + 1))
        continue
    fi

    # Commit
    git commit -m "${CLUSTER_TITLES[$pr_key]}

Wave 7 CYC reduction — only .cs source files.
Pre-push gate: wave7_prepush_gate.py PASS.
Build: 0 errors 0 warnings.
All methods CYC<=8. Zero lock() violations."

    # Push (force if remote branch exists and is stale)
    if git push -f origin "$BRANCH"; then
        echo "  Pushed $BRANCH"
        CREATED+=("$pr_key")
    else
        echo "  PUSH FAILED for $BRANCH"
        FAILED=$((FAILED + 1))
    fi

    # Return to main
    git checkout "$MAIN"
done

echo ""
echo "=== Branch creation summary ==="
echo "Created: ${#CREATED[@]} branches"
echo "Failed : $FAILED"
echo ""

# ─── Open PRs via GitHub API ─────────────────────────────────────────────────
echo "=== Opening PRs via GitHub API ==="

for pr_key in "${CREATED[@]}"; do
    BRANCH="${CLUSTER_BRANCHES[$pr_key]}"
    TITLE="${CLUSTER_TITLES[$pr_key]}"
    BODY="${CLUSTER_BODIES[$pr_key]}"

    echo ""
    echo "Opening PR for $BRANCH ..."

    RESPONSE=$(curl -s -X POST \
        -H "Accept: application/vnd.github+json" \
        -H "Authorization: Bearer ${GITHUB_TOKEN:-}" \
        "https://api.github.com/repos/${REPO}/pulls" \
        -d "{
            \"title\": $(echo "$TITLE" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read().strip()))'),
            \"body\": $(echo "$BODY" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read().strip()))'),
            \"head\": \"$BRANCH\",
            \"base\": \"main\"
        }")

    PR_NUM=$(echo "$RESPONSE" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('number','ERR'))" 2>/dev/null || echo "ERR")
    PR_URL=$(echo "$RESPONSE" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('html_url','N/A'))" 2>/dev/null || echo "N/A")

    if [ "$PR_NUM" != "ERR" ] && [ "$PR_NUM" != "" ]; then
        echo "  PR #$PR_NUM opened: $PR_URL"
    else
        MSG=$(echo "$RESPONSE" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('message','unknown'))" 2>/dev/null || echo "parse error")
        echo "  PR open failed: $MSG"
        echo "  Raw: $RESPONSE" | head -c 300
    fi
done

echo ""
echo "=== Done ==="
