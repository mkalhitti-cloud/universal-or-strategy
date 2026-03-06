# Director's Quick-Start: IDE Re-Alignment

Because we moved the core logic into `src/`, your open windows and "pinned" files need to be reset. Follow these steps for each tool:

## 1. Cursor & Antigravity (Visual Editors)

1. **Kill the Zombies**: Your current open tabs are "ghosts." Right-click any tab and choose **"Close All"**.
2. **The `Ctrl+P` Trick**:
   - Press `Ctrl + P` on your keyboard.
   - Type `src/`
   - Click the files you want to open.
3. **Re-Pin**: Right-click the new tabs and select **"Pin"**.

## 2. Claude Code (Desktop/CLI)

1. **New Session**: Start a new chat session so Claude doesn't get confused by old paths.
2. **Reference `src/`**: If you tell Claude to edit a file, always include the folder name (e.g., `src/UniversalORStrategyV12_002_Dev.cs`).
3. **Auto-Discovery**: Claude is smart—the first time it "lists files," it will see the `src/` folder and adjust automatically.

## 3. Codex (OpenRouter/Standalone)

1. **Refresh Sidebar**: In the file explorer on the left, click the **Refresh** icon.
2. **Folder Drill-Down**: Click the arrow `>` next to the `src` folder to see your strategy files.

## 4. NinjaTrader 8 (Deployment)

1. **Save in VS Code / Cursor**: Edit your files in the `src/` folder.
2. **Run Sync**: Run `./deploy-sync.ps1` in your terminal to push changes to NinjaTrader.
3. **Compile**: Press **F5** in NinjaTrader to compile.
   - _Note: Auto-sync hooks have been removed to prevent file-lock conflicts._

## 🚀 5. Manual Agent Launch (Local Terminal First)

When you want to run an agent in **this terminal** (not in the background), always use the clean path:

### A. Navigate to Clean Path

```powershell
cd C:\WSGTA\universal-or-strategy
```

### B. Launch Codex 5.3

```powershell
& "C:\Users\Mohammed Khalid\.cursor\extensions\openai.chatgpt-0.4.74-universal\bin\windows-x86_64\codex.exe" --model "gpt-5.3-codex"
```

### C. Launch Claude Code

```powershell
claude
```

> [!IMPORTANT]
> **Avoid Cloud Sync**: Never launch agents inside Dropbox or other cloud-sync paths to prevent file lock conflicts. Use C:\WSGTA\ instead.

---

_Status: Alpha Files Successfully Relocated to `src/`_

---

## 6. ASCII-Only Protocol (MANDATORY — Build Protocol v2)

**Why this exists:** AI agents wrote Unicode decorators in log messages (emoji, em-dashes, curly quotes).
Cleanup scripts converted curly closing-quote `"` to straight `"` — which TERMINATED C# strings early.
One broken quote in `SIMA.cs` caused 300+ cascading compile errors across all files. This cost 2 days.

### Hard Rules — All AI Agents Must Follow

| NEVER in string literals | Use instead        |
| ------------------------ | ------------------ |
| `⚠️` `✅` `❌` (emoji)   | `(!)` `[OK]` `[X]` |
| `—` `–` (em/en dash)     | `--`               |
| `"` `"` (curly quotes)   | `"` (straight)     |
| `→` `←` (arrows)         | `->` `<-`          |
| `╔═╗` (box drawing)      | `+--+`             |

### Automatic Deploy Gate

`deploy-sync.ps1` scans every `.cs` file for non-ASCII bytes **before** touching NT8.
Gate FAIL = deploy aborted + dirty filename printed.

### Emergency Fix Sequence

```
1. python C:\tmp\byte_purge.py       # nuclear byte-level purge
2. Search all .cs files for:  ?"     # any match in a Print/string line = broken string
3. Replace ?" with:  --              # or (!) as appropriate
4. Run deploy-sync.ps1 again         # gate will confirm clean
```

> **Avoid Cloud Sync**: Never launch agents from cloud-sync paths.
> Source of truth is always `C:\WSGTA\universal-or-strategy\src\`
