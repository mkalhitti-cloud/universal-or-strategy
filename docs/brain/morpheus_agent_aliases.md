# Morpheus OS - Agent Alias Registry

To enable full integration of the V12 agent stack within the Multica managed substrate, the following aliases are utilized for runtimes not natively supported by the auto-discovery engine.

| Official Agent | Multica Alias (Runtime) | Provider | Binary / Source                                          |
| :------------- | :---------------------- | :------- | :------------------------------------------------------- |
| **Droid**      | `pi`                    | `pi`     | `C:\Users\Mohammed Khalid\bin\droid.exe`                 |
| **Jules**      | `hermes`                | `hermes` | `C:\Users\Mohammed Khalid\AppData\Roaming\npm\jules.ps1` |
| **Claude**     | `claude`                | `claude` | Claude Code CLI                                          |
| **Codex**      | `codex`                 | `codex`  | Codex CLI                                                |
| **Gemini**     | `gemini`                | `gemini` | Gemini CLI                                               |

## Infrastructure Mappings

These mappings are enforced via User-level environment variables:

- `MULTICA_PI_PATH` ➔ Droid
- `MULTICA_HERMES_PATH` ➔ Jules

## Usage Notes

Agents dispatched via Multica should be aware of their host substrate (Morpheus OS).
The aliasing is transparent to the end-user but must be tracked for binary updates or skill registrations.
