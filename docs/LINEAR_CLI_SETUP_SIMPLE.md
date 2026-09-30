# Linear CLI Setup - Simple Method

## What We Installed
The `@linear/cli` package is a lightweight tool for creating issues and checking out branches. It uses Linear API keys for authentication.

## Step-by-Step Setup

### Step 1: Get Your Linear API Key

1. **Open Linear in your browser**:
   - Go to: https://linear.app
   - Log in with your account (mkalhitti@gmail.com)

2. **Navigate to API Settings**:
   - Click your profile picture (bottom left)
   - Click "Settings"
   - Click "API" in the left sidebar
   - Or go directly to: https://linear.app/settings/api

3. **Create a Personal API Key**:
   - Click "Create new key" or "Personal API keys"
   - Give it a name: `V12 CLI Access`
   - Click "Create"
   - **IMPORTANT**: Copy the key immediately (starts with `lin_api_`)
   - You won't be able to see it again!

### Step 2: Set Up Environment Variable

**Option A: Session-Only (Temporary)**
```powershell
# In PowerShell (current session only)
$env:LINEAR_API_KEY = "lin_api_YOUR_KEY_HERE"
```

**Option B: Permanent (Recommended)**
```powershell
# Add to your PowerShell profile
notepad $PROFILE

# Add this line to the file:
$env:LINEAR_API_KEY = "lin_api_YOUR_KEY_HERE"

# Save and reload
. $PROFILE
```

**Option C: Project .env File (Best for V12)**
```powershell
# Create .env file in project root
@"
LINEAR_API_KEY=lin_api_YOUR_KEY_HERE
LINEAR_TEAM_ID=your_team_id_here
"@ | Out-File -FilePath .env -Encoding utf8
```

### Step 3: Verify Setup

```powershell
# Test the CLI
npx @linear/cli new

# You should see a prompt to create a new issue
# If it works, you're authenticated!
```

## Usage Examples

### Create a New Issue
```powershell
npx @linear/cli new
```
This will prompt you for:
- Issue title
- Issue description
- Team (select from list)
- Priority
- Labels

### Create Issue from Command Line
```powershell
# Set environment variable first
$env:LINEAR_API_KEY = "lin_api_YOUR_KEY_HERE"

# Then create issue
npx @linear/cli new
```

### Checkout Branch for Issue
```powershell
# After creating an issue, checkout a branch for it
npx @linear/cli checkout
```

## Alternative: Use Python Script Instead

Since we already have `scripts/linear_sync.py`, you can use that for more advanced operations:

### Setup Python Script
```powershell
# Install dependencies
pip install requests python-dotenv

# Create .env file
@"
LINEAR_API_KEY=lin_api_YOUR_KEY_HERE
LINEAR_TEAM_ID=your_team_id_here
LINEAR_ASSIGNEE_IDS=user_id_1,user_id_2
"@ | Out-File -FilePath .env -Encoding utf8

# Run sync
python scripts/linear_sync.py
```

## Quick Start (Recommended Path)

**For V12 Project, use this workflow**:

1. **Get API Key** from https://linear.app/settings/api
2. **Create .env file**:
   ```powershell
   @"
   LINEAR_API_KEY=lin_api_YOUR_KEY_HERE
   "@ | Out-File -FilePath .env -Encoding utf8
   ```
3. **Test with CLI**:
   ```powershell
   $env:LINEAR_API_KEY = (Get-Content .env | Where-Object { $_ -match 'LINEAR_API_KEY' } | ForEach-Object { $_.Split('=')[1] })
   npx @linear/cli new
   ```

## Troubleshooting

### "Authentication failed"
- Check your API key is correct
- Verify it starts with `lin_api_`
- Make sure environment variable is set: `echo $env:LINEAR_API_KEY`

### "Team not found"
- You need to specify team ID in .env file
- Get team ID from Linear URL: `linear.app/TEAM_KEY/...`
- Or use the Python script which auto-detects teams

### "Command not found"
- Make sure you're using `npx @linear/cli` not just `linear`
- The package name is `@linear/cli` not `linear-cli`

## Next Steps

After authentication works:
1. Create EPIC-7-QUALITY epic in Linear
2. Create 5 tickets from `docs/brain/EPIC-7-QUALITY/*.md`
3. Sync roadmap: `python scripts/linear_sync.py`
4. Verify in Linear web UI

## Security Notes

⚠️ **NEVER commit .env file to git**
- Already in `.gitignore`
- Contains sensitive API keys
- Rotate keys if accidentally exposed

✅ **Safe to commit**:
- `.env.example` (template without real keys)
- Documentation files
- Scripts that read from .env