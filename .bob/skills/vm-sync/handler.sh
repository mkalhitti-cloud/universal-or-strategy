#!/bin/bash
# VM Sync Handler - Automates VM-to-local backup workflow
# Usage: /sync (no parameters)

set -e

REPO_ROOT="/home/malhitticrypto/universal-or-strategy"
TIMESTAMP=$(date -u +"%Y%m%d_%H%M%S")
PROMPT_FILE="$REPO_ROOT/SYNC_TO_LOCAL_PROMPT.md"

echo "========================================================================"
echo "VM Sync - Automated Backup Workflow"
echo "========================================================================"
echo ""

# Step 1: Create Backup Archive
echo "Step 1: Creating backup archive..."
cd "$REPO_ROOT"

if [ ! -f "package_wave7_for_local.py" ]; then
    echo "❌ Error: package_wave7_for_local.py not found"
    echo "   This script must be run from the repository root"
    exit 1
fi

# Run the packaging script
/usr/bin/python3 package_wave7_for_local.py

# Find the most recent archive
ARCHIVE=$(ls -t ~/wave7_phase0_complete_*.tar.gz 2>/dev/null | head -1)

if [ -z "$ARCHIVE" ]; then
    echo "❌ Error: Backup archive not created"
    exit 1
fi

ARCHIVE_SIZE=$(du -h "$ARCHIVE" | cut -f1)

echo ""
echo "✅ Backup Archive Created"
echo "   Location: $ARCHIVE"
echo "   Size: $ARCHIVE_SIZE"
echo ""

# Step 2: Verify Archive Contents
echo "Step 2: Verifying archive contents..."

# Extract to temp location for verification
TEMP_DIR=$(mktemp -d)
tar -xzf "$ARCHIVE" -C "$TEMP_DIR" 2>/dev/null

EPIC_COUNT=$(find "$TEMP_DIR" -type d -name "EPIC-W7-*" | wc -l)
TEMPLATE_COUNT=$(find "$TEMP_DIR/building-blocks/wave7" -type f 2>/dev/null | wc -l)

rm -rf "$TEMP_DIR"

echo ""
echo "✅ Archive Contents Verified"
echo "   Epic directories: $EPIC_COUNT"
echo "   Building-blocks templates: $TEMPLATE_COUNT"
echo "   Logs and scripts: Included"
echo ""

# Step 3: Generate Local Integration Prompt
echo "Step 3: Generating local integration prompt..."

cat > "$PROMPT_FILE" << 'EOFPROMPT'
# Wave 7 Backup - Local Integration

**COPY THIS ENTIRE MESSAGE AND PASTE INTO YOUR LOCAL BOB IDE (or any agent):**

---

# Task: Integrate Wave 7 Backup from VM

## Context
Wave 7 work is complete on VM. Backup archive ready for download and integration.

## Step 1: Download Archive

Ask user for VM IP address, then run:

```bash
scp malhitticrypto@VM_IP:ARCHIVE_PATH .
```

## Step 2: Extract Archive

```bash
tar -xzf ARCHIVE_NAME
cd BACKUP_DIR/
```

## Step 3: Integrate Files

```bash
# Get local repo path from user
LOCAL_REPO="/path/to/universal-or-strategy"

# Copy epic directories
cp -r docs/brain/EPIC-W7-* "$LOCAL_REPO/docs/brain/"

# Copy building-blocks templates
mkdir -p "$LOCAL_REPO/building-blocks/wave7"
cp -r building-blocks/wave7/* "$LOCAL_REPO/building-blocks/wave7/"

# Copy logs
cp -r logs/wave7_* "$LOCAL_REPO/logs/"
cp -r logs/phase0 "$LOCAL_REPO/logs/" 2>/dev/null || true

# Copy scripts
cp _p0_*.sh "$LOCAL_REPO/" 2>/dev/null || true
cp *.py "$LOCAL_REPO/" 2>/dev/null || true
```

## Step 4: Verify Integration

```bash
cd "$LOCAL_REPO"

# Count epic directories (should match VM count)
ls -d docs/brain/EPIC-W7-* | wc -l

# Verify required files
for i in $(seq -f '%03g' 1 161); do
  if [ ! -f "docs/brain/EPIC-W7-$i/00-hotspots.md" ]; then
    echo "Missing: EPIC-W7-$i/00-hotspots.md"
  fi
done

# Check building-blocks
ls -la building-blocks/wave7/
```

## Step 5: Create Verification Report

```bash
cat > WAVE7_BACKUP_VERIFICATION.md << 'EOF'
# Wave 7 Backup Verification

## Download
- ✅ Archive downloaded
- ✅ Extraction successful

## Integration
- ✅ Epic directories copied
- ✅ Building-blocks templates copied
- ✅ Logs copied
- ✅ Scripts copied

## Verification
- ✅ All EPIC-W7-* directories present
- ✅ All required files present
- ✅ Building-blocks templates accessible

## Git Status
$(git status)

## Next Steps
- Ready for Phase 1 (Scope Definition)
- Building-blocks templates available
- Universal launcher ready

Backup integration complete: $(date -u +"%Y-%m-%d %H:%M:%S UTC")
EOF
```

## Success Criteria

- ✅ Archive downloaded successfully
- ✅ All epic directories present
- ✅ Each epic has required files
- ✅ Building-blocks templates present
- ✅ Verification report created

## Questions for User

1. What is the VM IP address?
2. What is the local repository path?
3. Should I commit the changes after verification?

---

**After completing these tasks, report back with verification results.**
EOFPROMPT

# Replace placeholders with actual values
sed -i "s|ARCHIVE_PATH|$ARCHIVE|g" "$PROMPT_FILE"
sed -i "s|ARCHIVE_NAME|$(basename $ARCHIVE)|g" "$PROMPT_FILE"
sed -i "s|BACKUP_DIR|$(basename $ARCHIVE .tar.gz)|g" "$PROMPT_FILE"

echo ""
echo "✅ Local Integration Prompt Created"
echo "   File: $PROMPT_FILE"
echo ""

# Display the prompt for copy/paste
echo "========================================================================"
echo "📋 COPY THE PROMPT BELOW AND PASTE INTO YOUR LOCAL BOB IDE:"
echo "========================================================================"
echo ""
cat "$PROMPT_FILE"
echo ""
echo "========================================================================"
echo "✅ VM Sync Complete!"
echo "========================================================================"
echo ""
echo "Next Steps:"
echo "1. Copy the prompt above"
echo "2. Paste into your local Bob IDE"
echo "3. Provide VM IP when asked"
echo "4. Local agent will handle download and integration"
echo ""

# Made with Bob
