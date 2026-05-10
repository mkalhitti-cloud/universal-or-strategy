$promptContent = Get-Content docs/brain/gemini_mission_brief.md -Raw
Write-Host "Executing Gemini CLI Handoff..." -ForegroundColor Cyan
gemini -p $promptContent
