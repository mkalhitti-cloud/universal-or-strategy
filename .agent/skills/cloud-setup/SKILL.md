---
name: Cloud Setup
description: >
  1-Click deployment of NinjaTrader & Rithmic on GCP Windows VM. Use this whenever the user asks to "setup the cloud", "migrate to gcp", "deploy V12 to a fresh VM", or "start a new cloud instance". This skill automates VM creation, Remote Desktop, Git cloning, NinjaTrader workspace restoration, audio configuration, and Rithmic Direct Connection networking.
---

# Cloud Setup Skill (V12 Monolith Migration)

Use this skill to deploy a high-performance Windows Server 2022 instance on Google Cloud Platform, pre-configured for NinjaTrader 8, Rithmic, and the V12 Universal OR Strategy.

## Step 1 -- VM Provisioning

Use the GCP CLI to create the VM.

- **Instance Name**: `monolith-v12-nt8` (or similar, depending on user request)
- **Machine Type**: `n2-standard-2` or similar.
- **Image**: `windows-server-2022-dc-v20240415`
- **Zone**: Match the zone to the targeted exchange location (e.g., `us-central1-a` Iowa for Chicago/CME, or `us-east4-a` for NY/Equinix) -- **CRITICAL for lowest latency.**
- **Project**: Identify the correct billing project first (e.g., using `gcloud projects list`).

## Step 2 -- Network & Firewall

Configure IAP tunneling and Rithmic firewall ports.

1. Enable IAP: `gcloud compute firewall-rules create allow-rdp-ingress-from-iap --direction=INGRESS --action=allow --rules=tcp:3389 --source-ranges=35.235.240.0/20`
2. Enable Rithmic Ports: `gcloud compute firewall-rules create rithmic-direct-fixed --allow="tcp:16000-17000,tcp:40000-42099,udp:45454" --direction="INGRESS" --priority=1000 --network="default" --action="ALLOW"`

## Step 3 -- Startup Metadata & Automation

Apply the initialization scripts stored in `references/scripts/` to the VM's metadata.

| File                              | Purpose                                                                                         |
| --------------------------------- | ----------------------------------------------------------------------------------------------- |
| `references/scripts/setup_vm.ps1` | Installs Git and clones the `universal-or-strategy` repository on boot.                         |
| `references/scripts/set_pass.ps1` | Forces the admin password, installs R\|Trader Pro silently, and enables Windows Audio services. |

Apply scripts using:
`gcloud compute instances add-metadata [INSTANCE_NAME] --metadata-from-file windows-startup-script-ps1=[SCRIPT_PATH] --zone [ZONE]`

## Step 4 -- Connecting & Restoring

1. **FORENSIC PRE-FLIGHT CHECK (MANDATORY)**: Before syncing anything, verify that core modules like `StickyState.cs`, `Construction.cs`, and `V12_002.cs` exist in the repository's `src/` folder. Use `Get-ChildItem -Path "src" -Filter "*Sticky*"` to confirm.
2. **NUCLEAR SUBFOLDER PURGE (MANDATORY)**: Rename the entire `Documents\NinjaTrader 8\bin\Custom` folder to `Custom_GHOST_OLD` and create a brand-new `Custom\Strategies` structure. This kills every hidden ghost file, shadow-copy, and subfolder collision instantly.
3. **Restoring Files**:
   - If the repo is incomplete, use the "Zip Rescue" protocol: Rename any `.nt8bk` on the laptop to `.zip`, manually extract the missing `.cs` files, and move them to the repo `src/` folder.
   - If NinjaTrader errors persist during import, always default to the "Hide-and-Restore" maneuver (rename root folders).
4. **DEPLOYMENT MANDATE:** Always use `deploy-vm-safe.ps1` (or `deploy-sync.ps1`) to establish hard links from the `src/` directory to NinjaTrader.
   - **CRITICAL:** DO NOT manually copy-paste source files into the `Strategies` or `Indicators` folders during regular development. Only use manual copy as an emergency backup for Zip Rescue.
   - **CRITICAL:** The sync script MUST purge redundant subfolders (like `V12_002` subfolders) every single time it runs.
   - **CRITICAL:** If `Get-ChildItem` fails in PowerShell but the files are visible in Explorer, immediately switch to the `CMD dir /s /ah /b` method to find hidden/junction paths.

## Step 5 -- Rithmic & Audio Configuration

1. **Rithmic Connection**: Ensure the user connects NinjaTrader directly to Rithmic **without** Plug-in Mode, as mirroring the local PC's Direct Connection is the most stable path.
   - **CRITICAL REQUIREMENT:** Instruct the user to explicitly select the Rithmic Gateway that matches their target exchange location (e.g., `Chicago Area` for Aurora/CME) in NinjaTrader's Rithmic connection settings to ensure priority routing.
2. **Audio**: Audio services (`Audiosrv`, `AudioEndpointBuilder`) must be set to Automatic startup in Windows. Inform the user to enable "Play on this computer" in their local Remote Desktop settings.

## Step 6 -- Mandatory Self-Improvement

After EVERY use, you must audit this skill per the `Skill Creator` rules. If a step fails, fix this SKILL.md immediately.
