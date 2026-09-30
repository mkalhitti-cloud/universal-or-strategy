# Create Golden Image v3 - Self-Orchestrating Parallel Execution
# Builds on v4 VM with built-in orchestration script

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Creating Golden Image v3" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Start Time: $(Get-Date)"
Write-Host ""

$PROJECT = "project-14c86305-3cba-493f-a73"
$ZONE = "us-central1-a"
$SOURCE_VM = "v12-golden-image-v4"
$IMAGE_NAME = "v12-bob-shell-golden-v3"

# Step 1: Start source VM
Write-Host "Step 1: Starting source VM ($SOURCE_VM)..." -ForegroundColor Cyan
gcloud compute instances start $SOURCE_VM --project=$PROJECT --zone=$ZONE

Write-Host "Waiting 30 seconds for boot..."
Start-Sleep -Seconds 30
Write-Host "✅ VM started" -ForegroundColor Green
Write-Host ""

# Step 2: Upload orchestration script
Write-Host "Step 2: Uploading orchestration script..." -ForegroundColor Cyan

# First, create a temporary version without the outer script wrapper
$orchestratorContent = @'
#!/bin/bash
# Parallel Epic Orchestrator
# Reads epic list from VM metadata and executes in parallel using screen

set -e

echo "=========================================="
echo "Parallel Epic Orchestrator"
echo "=========================================="
echo "Start: $(date)"

# Load environment
source ~/.bashrc

# Get epic list from VM metadata
EPIC_JSON=$(curl -s "http://metadata.google.internal/computeMetadata/v1/instance/attributes/epics" -H "Metadata-Flavor: Google")

if [ -z "$EPIC_JSON" ] || [ "$EPIC_JSON" == "" ]; then
    echo "ERROR: No epic list found in VM metadata"
    echo "Expected metadata key: epics"
    echo "Expected format: JSON array of {id, method, cyc}"
    exit 1
fi

echo "Epic list received:"
echo "$EPIC_JSON" | jq .

# Parse epic count
EPIC_COUNT=$(echo "$EPIC_JSON" | jq 'length')
echo ""
echo "Total epics to process: $EPIC_COUNT"
echo ""

# Create logs directory
cd ~/universal-or-strategy
mkdir -p logs

# Launch each epic in a screen session
echo "Launching $EPIC_COUNT parallel agents..."
echo ""

for i in $(seq 0 $(($EPIC_COUNT - 1))); do
    EPIC_ID=$(echo "$EPIC_JSON" | jq -r ".[$i].id")
    METHOD=$(echo "$EPIC_JSON" | jq -r ".[$i].method")
    CYC=$(echo "$EPIC_JSON" | jq -r ".[$i].cyc")
    
    echo "[$((i+1))/$EPIC_COUNT] Launching $EPIC_ID ($METHOD, CYC $CYC)"
    
    # Create screen session with proper quoting
    screen -dmS "$EPIC_ID" bash -c "
        cd ~/universal-or-strategy
        source ~/.bashrc
        bob --accept-license --auth-method api-key -p \"Run epic-intake for $EPIC_ID. Target: Reduce complexity in $METHOD (CYC $CYC to 8)\" --max-coins 30 > logs/${EPIC_ID}.log 2>&1
        echo \"$EPIC_ID complete\" >> logs/${EPIC_ID}.log
    "
    
    echo "  ✓ Screen session '$EPIC_ID' started"
    sleep 1
done

echo ""
echo "All $EPIC_COUNT agents launched!"
echo ""

# Monitor progress
echo "Monitoring progress (checking every 60 seconds)..."
echo ""

while true; do
    ACTIVE=$(screen -ls | grep -c "EPIC-CCN" || true)
    
    if [ $ACTIVE -eq 0 ]; then
        echo "$(date): All agents completed!"
        break
    fi
    
    echo "$(date): $ACTIVE / $EPIC_COUNT agents still running"
    
    # Show log sizes
    for i in $(seq 0 $(($EPIC_COUNT - 1))); do
        EPIC_ID=$(echo "$EPIC_JSON" | jq -r ".[$i].id")
        if [ -f "logs/${EPIC_ID}.log" ]; then
            LINES=$(wc -l < "logs/${EPIC_ID}.log")
            echo "  $EPIC_ID: $LINES lines"
        fi
    done
    
    echo ""
    sleep 60
done

echo ""
echo "=========================================="
echo "Orchestration Complete"
echo "=========================================="
echo "End: $(date)"
echo ""
echo "Logs available in: ~/universal-or-strategy/logs/"
echo "Artifacts available in: ~/universal-or-strategy/docs/brain/"
'@

# Save to temp file
$orchestratorContent | Out-File -FilePath "scripts/temp_orchestrator.sh" -Encoding ASCII -NoNewline

# Upload to VM
gcloud compute scp scripts/temp_orchestrator.sh "$SOURCE_VM`:~/run_parallel_epics.sh" `
    --project=$PROJECT `
    --zone=$ZONE

# Make executable
gcloud compute ssh $SOURCE_VM `
    --project=$PROJECT `
    --zone=$ZONE `
    --command="chmod +x ~/run_parallel_epics.sh"

# Clean up temp file
Remove-Item "scripts/temp_orchestrator.sh"

Write-Host "✅ Orchestrator script uploaded" -ForegroundColor Green
Write-Host ""

# Step 3: Install screen and jq
Write-Host "Step 3: Installing dependencies (screen, jq)..." -ForegroundColor Cyan

gcloud compute ssh $SOURCE_VM `
    --project=$PROJECT `
    --zone=$ZONE `
    --command="sudo apt-get update && sudo apt-get install -y screen jq"

Write-Host "✅ Dependencies installed" -ForegroundColor Green
Write-Host ""

# Step 4: Create systemd service
Write-Host "Step 4: Creating systemd service..." -ForegroundColor Cyan

$serviceContent = @'
[Unit]
Description=Epic Parallel Orchestrator
After=network.target google-network-daemon.service

[Service]
Type=oneshot
User=malhitticrypto
WorkingDirectory=/home/malhitticrypto
ExecStart=/home/malhitticrypto/run_parallel_epics.sh
StandardOutput=journal
StandardError=journal
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
'@

# Save to temp file
$serviceContent | Out-File -FilePath "scripts/temp_service.txt" -Encoding ASCII -NoNewline

# Upload and install service
gcloud compute scp scripts/temp_service.txt "$SOURCE_VM`:~/epic-orchestrator.service" `
    --project=$PROJECT `
    --zone=$ZONE

gcloud compute ssh $SOURCE_VM `
    --project=$PROJECT `
    --zone=$ZONE `
    --command="sudo mv ~/epic-orchestrator.service /etc/systemd/system/ && sudo systemctl daemon-reload && sudo systemctl enable epic-orchestrator.service"

# Clean up temp file
Remove-Item "scripts/temp_service.txt"

Write-Host "✅ Systemd service configured" -ForegroundColor Green
Write-Host ""

# Step 5: Stop VM
Write-Host "Step 5: Stopping VM..." -ForegroundColor Cyan
gcloud compute instances stop $SOURCE_VM --project=$PROJECT --zone=$ZONE

Write-Host "✅ VM stopped" -ForegroundColor Green
Write-Host ""

# Step 6: Create image
Write-Host "Step 6: Creating golden image..." -ForegroundColor Cyan
gcloud compute images create $IMAGE_NAME `
    --project=$PROJECT `
    --source-disk=$SOURCE_VM `
    --source-disk-zone=$ZONE `
    --family=v12-bob-shell

Write-Host "✅ Golden Image v3 created: $IMAGE_NAME" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Golden Image v3 Complete" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "End Time: $(Get-Date)"
Write-Host ""
Write-Host "Image: $IMAGE_NAME"
Write-Host "Features:"
Write-Host "- Bob Shell with API key authentication"
Write-Host "- mise runtime manager"
Write-Host "- screen for persistent sessions"
Write-Host "- jq for JSON parsing"
Write-Host "- Built-in parallel orchestrator script"
Write-Host "- Systemd service for auto-start on boot"
Write-Host ""
Write-Host "Usage:"
Write-Host "  powershell -File scripts\launch_wave2_v3_metadata.ps1"
Write-Host ""
Write-Host "Ready to launch Wave 2!"

# Made with Bob
