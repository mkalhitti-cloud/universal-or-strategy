---
name: nt8-log-monitor
description: Autonomous monitoring of NinjaTrader 8 logs and trace files for errors, rejections, and crashes.
---

# NinjaTrader 8 Log Monitor Skill

This skill allows Antigravity to autonomously verify the health of the NinjaTrader 8 environment by scanning system logs and trace files.

**Universal Path:** `${PROJECT_ROOT}`
**Executors:** ${BRAIN} (Reasoning), ${HANDS} (Gemini Flash via delegation_bridge)

## Related Skills
- [file-manager](../file-manager/SKILL.md) - File operations and deployment
- [delegation-bridge](../delegation-bridge/SKILL.md) - Safe deployment execution
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards

## Core Capabilities

1.  **Identify Latest Logs**: Finds the most recent `.txt` log files and Rithmic/Brokerage trace files.
2.  **Error Detection**: Scans for "Rejected", "Termination", "Error", and "Exception".
3.  **Instrument Monitoring**: Scans specifically for events related to MES or MGC.

## When to Use

- **After Deployment**: Always run this skill after deploying a new version of the Master Strategy (`UniversalORStrategyV8_X`) to ensure it initialized without errors.
- **After Rejection**: If you suspect an order was rejected or a stop wasn't placed, use this skill to check the `log/` directory.
- **Connection Issues**: Check the `trace/` directory for Rithmic disconnects or timeout errors.

## Script Usage

### Find Latest Logs
```powershell
.agent\skills\nt8-log-monitor\scripts\Get-LatestLog.ps1
```

### Scan for Errors
```powershell
.agent\skills\nt8-log-monitor\scripts\Scan-LogsForErrors.ps1 -Keywords @("Rejected", "Error", "MGC")
```

## Protocol: Post-Deployment Health Check
After updating a strategy:
1.  Run `Scan-LogsForErrors.ps1`.
2.  Look for any `Termination` or `Error` logs timestamped within the last 5 minutes.
3.  Report status to the user.
