---
name: nt8-backup-manager
description: Manages NinjaTrader 8 local backups and ensures path stability after OneDrive removal.
---

# NT8 Backup Manager Skill

This skill provides a standardized way to back up NinjaTrader 8 settings and verify the local path configuration.

## Core Responsibilities
1. **Path Verification**: Ensure `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8` is the active directory.
2. **Automated Backup**: Create timestamped backups of Templates, Workspaces, and Configuration files.
3. **OneDrive Check**: Verify that `OneDrive.exe` is not locking files in the NT8 directory.

## Usage

### Run Backup
Use the included PowerShell script to create a snapshot on the Desktop:
```powershell
powershell -File ".agent/skills/nt8-backup-manager/scripts/backup.ps1"
```

## Critical Locations
- **Local Data**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8`
- **Backup Root**: `C:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\NinjaTrader_Local_Backups`

## Troubleshooting
If NinjaTrader shows "Path not found" or "File in use":
1. Check Registry: `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders` -> `Personal` should be local.
2. Kill OneDrive: `taskkill /F /IM OneDrive.exe /T`
3. Clear Recovery: Remove `bin\Custom\Recovery` if compilation fails.
