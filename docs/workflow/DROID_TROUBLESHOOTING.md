# Droid CLI Troubleshooting

## Issues Encountered

### 1. Path with Spaces Error
```
'C:\Users\Mohammed' is not recognized as an internal or external command
```

**Cause**: The path `C:\Users\Mohammed Khalid\.git-ai\bin\git-ai.exe` has a space in "Mohammed Khalid" and isn't properly quoted.

**Solution**: This is a Droid bug with Windows paths containing spaces. Workarounds:
1. Move git-ai to a path without spaces
2. Use short path name: `C:\Users\MOHAMM~1\.git-ai\bin\git-ai.exe`
3. Disable git-ai hooks in Droid settings

### 2. Rate Limit Error (429)
```
BYOK Error: 429 status code (no body)
```

**Cause**: Your Google API key (Gemini) hit the rate limit.

**Google API Rate Limits**:
- **Free tier**: 15 requests per minute (RPM)
- **Pay-as-you-go**: 360 RPM
- **Gemini 2.0 Flash**: 10 RPM (free), 1000 RPM (paid)

**Solutions**:
1. **Wait**: Rate limits reset after 1 minute
2. **Upgrade**: Enable billing on Google Cloud Console
3. **Use Bob API**: Switch to Bob API key (uses BobCoins, not Google limits)

## Recommended Approach

### Option 1: Use Bob API with Droid (Bypass Google Limits)

Update `scripts/droid_settings.json`:
```json
{
  "provider": "bob",
  "model": "fable-5",
  "api_key": "bob_prod_bob-admin_2DNk7bgrboQmv5wERtB7RgdwpxLuMaJGz4NGDayQSb1NEiJGmhuRSyxxzRcEHtzitSqKwgq5HDFEc6gjkyXg7a5Y_GLEMZSVV35sx3T52WQbfkJdFN4HmhTp9VRFcNyxDdvGp"
}
```

Then test:
```bash
droid exec --settings scripts/droid_settings.json "What is 2+2?"
```

### Option 2: Wait for Goose CLI Setup

Goose is setting up Bob API. Once complete, test:
```bash
goose session start "What is 2+2?"
```

### Option 3: Sequential Execution with Bob CLI (Fallback)

If parallel execution continues to have issues:
```bash
# Run one epic at a time through all phases
bob --mode v12-engineer "Execute Phase 1 for EPIC-CCN-164"
# Wait for completion, then next epic
bob --mode v12-engineer "Execute Phase 1 for EPIC-CCN-107"
# etc.
```

**Time**: ~4 hours for 9 epics × 8 phases
**Cost**: ~$20-30 (within budget)
**Reliability**: 100%

## Current Status

- ✅ **Phase 0**: Complete for all 9 epics
- ⏳ **Phases 1-6**: Waiting for CLI solution
- 💰 **Budget**: 61% remaining (97.67/160 BobCoins)

## Next Steps

1. **Check Google API quota**: https://console.cloud.google.com/apis/api/generativelanguage.googleapis.com/quotas
2. **Test Droid with Bob API**: Use `scripts/droid_settings.json`
3. **Wait for Goose**: Let Goose finish Bob API setup
4. **Fallback to sequential**: Use Bob CLI if parallel continues to fail