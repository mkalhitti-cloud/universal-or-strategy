# Codex CLI Command Line Options

This page catalogs every documented Codex CLI command and flag. Use this reference to search by key or description.

The CLI inherits most defaults from `~/.codex/config.toml`. Any `-c key=value` overrides you pass at the command line take precedence for that invocation. See [Config basics](https://developers.openai.com/codex/config-basic#configuration-precedence) for more information.

## Global flags

These options apply to the base `codex` command and propagate to each subcommand unless a section below specifies otherwise.
When you run a subcommand, place global flags after it (for example, `codex exec --oss ...`) so Codex applies them as intended.

| Key | Type | Description |
| --- | --- | --- |
| `PROMPT` | string | Optional text instruction to start the session. Omit to launch the TUI without a pre-filled message. |
| `--image, -i` | path[,path...] | Attach one or more image files to the initial prompt. Separate multiple paths with commas or repeat the flag. |
| `--model, -m` | string | Override the model set in configuration (for example `gpt-5.4`). |
| `--oss` | boolean | Use the local open source model provider (equivalent to `-c model_provider="oss"`). Validates that Ollama is running (default `false`). |
| `--profile, -p` | string | Configuration profile name to load from `~/.codex/config.toml`. |
| `--sandbox, -s` | read-only \| workspace-write \| danger-full-access | Select the sandbox policy for model-generated shell commands. |
| `--ask-for-approval, -a` | untrusted \| on-request \| never | Control when Codex pauses for human approval before running a command. `on-failure` is deprecated; prefer `on-request` for interactive runs or `never` for non-interactive runs. |
| `--dangerously-bypass-approvals-and-sandbox, --yolo` | boolean | Run every command without approvals or sandboxing. Only use inside an externally hardened environment (default `false`). |
| `--cd, -C` | path | Set the working directory for the agent before it starts processing your request. |
| `--search` | boolean | Enable live web search (sets `web_search = "live"` instead of the default `"cached"`, default `false`). |
| `--add-dir` | path | Grant additional directories write access alongside the main workspace. Repeat for multiple paths. |
| `--no-alt-screen` | boolean | Disable alternate screen mode for the TUI (overrides `tui.alternate_screen` for this run, default `false`). |
| `--remote` | ws://host:port \| wss://host:port | Connect the interactive TUI to a remote app-server WebSocket endpoint. Supported for `codex`, `codex resume`, and `codex fork`; other subcommands reject remote mode. |
| `--remote-auth-token-env` | ENV_VAR | Read a bearer token from this environment variable and send it when connecting with `--remote`. Requires `--remote`; tokens are only sent over `wss://` URLs or `ws://` URLs whose host is `localhost`, `127.0.0.1`, or `::1`. |
| `--enable` | feature | Force-enable a feature flag (translates to `-c features.<name>=true`). Repeatable. |
| `--disable` | feature | Force-disable a feature flag (translates to `-c features.<name>=false`). Repeatable. |
| `--config, -c` | key=value | Override configuration values. Values parse as JSON if possible; otherwise the literal string is used. |

## Subcommand details

### `codex` (interactive)

Running `codex` with no subcommand launches the interactive terminal UI (TUI). The agent accepts the global flags above plus image attachments. Web search defaults to cached mode; use `--search` to switch to live browsing. For low-friction local work, use `--sandbox workspace-write --ask-for-approval on-request`.

Use `--remote ws://host:port` or `--remote wss://host:port` to connect the TUI to an app server started with `codex app-server --listen ws://IP:PORT`. Add `--remote-auth-token-env <ENV_VAR>` when the server requires a bearer token for WebSocket authentication.

### `codex app-server`

Launch the Codex app server locally. This is primarily for development and debugging and may change without notice.

| Key | Type | Description |
| --- | --- | --- |
| `--listen` | stdio:// \| ws://IP:PORT | Transport listener URL. Use `ws://IP:PORT` to expose a WebSocket endpoint for remote clients (default `stdio://`). |
| `--ws-auth` | capability-token \| signed-bearer-token | Authentication mode for app-server WebSocket clients. If omitted, WebSocket auth is disabled; non-local listeners warn during startup. |
| `--ws-token-file` | absolute path | File containing the shared capability token. Required with `--ws-auth capability-token`. |
| `--ws-shared-secret-file` | absolute path | File containing the HMAC shared secret used to validate signed JWT bearer tokens. Required with `--ws-auth signed-bearer-token`. |
| `--ws-issuer` | string | Expected `iss` claim for signed bearer tokens. Requires `--ws-auth signed-bearer-token`. |
| `--ws-audience` | string | Expected `aud` claim for signed bearer tokens. Requires `--ws-auth signed-bearer-token`. |
| `--ws-max-clock-skew-seconds` | number | Clock skew allowance when validating signed bearer token `exp` and `nbf` claims. Requires `--ws-auth signed-bearer-token` (default `30`). |

`codex app-server --listen stdio://` keeps the default JSONL-over-stdio behavior. `--listen ws://IP:PORT` enables WebSocket transport for app-server clients. The server accepts `ws://` listen URLs; use TLS termination or a secure proxy when clients connect with `wss://`.

### `codex app`

Launch Codex Desktop from the terminal on macOS or Windows. On macOS, Codex can open a specific workspace path; on Windows, Codex prints the path to open.

| Key | Type | Description |
| --- | --- | --- |
| `PATH` | path | Workspace path for Codex Desktop. On macOS, Codex opens this path; on Windows, Codex prints the path (default `.`). |
| `--download-url` | url | Advanced override for the Codex desktop installer URL used during install. |

### `codex debug app-server send-message-v2`

Send one message through app-server's V2 thread/turn flow using the built-in app-server test client.

| Key | Type | Description |
| --- | --- | --- |
| `USER_MESSAGE` | string | Message text sent to app-server through the built-in V2 test-client flow. |

### `codex debug models`

Print the raw model catalog Codex sees as JSON.

| Key | Type | Description |
| --- | --- | --- |
| `--bundled` | boolean | Skip refresh and print only the model catalog bundled with the current Codex binary (default `false`). |

### `codex apply`

Apply the most recent diff from a Codex cloud task to your local repository. You must authenticate and have access to the task.

| Key | Type | Description |
| --- | --- | --- |
| `TASK_ID` | string | Identifier of the Codex Cloud task whose diff should be applied. |

### `codex cloud`

Interact with Codex cloud tasks from the terminal. The default command opens an interactive picker; `codex cloud exec` submits a task directly, and `codex cloud list` returns recent tasks.

| Key | Type | Description |
| --- | --- | --- |
| `QUERY` | string | Task prompt. If omitted, Codex prompts interactively for details. |
| `--env` | ENV_ID | Target Codex Cloud environment identifier (required). Use `codex cloud` to list options. |
| `--attempts` | 1-4 | Number of assistant attempts (best-of-N) Codex Cloud should run (default `1`). |

#### `codex cloud list`

List recent cloud tasks with optional filtering and pagination.

| Key | Type | Description |
| --- | --- | --- |
| `--env` | ENV_ID | Filter tasks by environment identifier. |
| `--limit` | 1-20 | Maximum number of tasks to return (default `20`). |
| `--cursor` | string | Pagination cursor returned by a previous request. |
| `--json` | boolean | Emit machine-readable JSON instead of plain text (default `false`). |

### `codex completion`

Generate shell completion scripts.

| Key | Type | Description |
| --- | --- | --- |
| `SHELL` | bash \| zsh \| fish \| power-shell \| elvish | Shell to generate completions for. Output prints to stdout (default `bash`). |

### `codex features`

Manage feature flags stored in `~/.codex/config.toml`.

| Command | Usage | Description |
| --- | --- | --- |
| `List` | `codex features list` | Show known feature flags, their maturity stage, and their effective state. |
| `Enable` | `codex features enable <feature>` | Persistently enable a feature flag in `config.toml`. Respects the active `--profile` when provided. |
| `Disable` | `codex features disable <feature>` | Persistently disable a feature flag in `config.toml`. Respects the active `--profile` when provided. |

### `codex exec`

Use `codex exec` (or the short form `codex e`) for scripted or CI-style runs that should finish without human interaction.

| Key | Type | Description |
| --- | --- | --- |
| `PROMPT` | string \| - (read stdin) | Initial instruction for the task. Use `-` to pipe the prompt from stdin. |
| `--image, -i` | path[,path...] | Attach images to the first message. Repeatable; supports comma-separated lists. |
| `--model, -m` | string | Override the configured model for this run. |
| `--oss` | boolean | Use the local open source provider (requires a running Ollama instance, default `false`). |
| `--sandbox, -s` | read-only \| workspace-write \| danger-full-access | Sandbox policy for model-generated commands. Defaults to configuration. |
| `--profile, -p` | string | Select a configuration profile defined in config.toml. |
| `--full-auto` | boolean | Deprecated compatibility flag. Prefer `--sandbox workspace-write`. |
| `--dangerously-bypass-approvals-and-sandbox, --yolo` | boolean | Bypass approval prompts and sandboxing. Dangerous—only use inside an isolated runner (default `false`). |
| `--cd, -C` | path | Set the workspace root before executing the task. |
| `--skip-git-repo-check` | boolean | Allow running outside a Git repository (useful for one-off directories, default `false`). |
| `--ephemeral` | boolean | Run without persisting session rollout files to disk (default `false`). |
| `--ignore-user-config` | boolean | Do not load `$CODEX_HOME/config.toml`. Authentication still uses `CODEX_HOME` (default `false`). |
| `--ignore-rules` | boolean | Do not load user or project execpolicy `.rules` files for this run (default `false`). |
| `--output-schema` | path | JSON Schema file describing the expected final response shape. Codex validates tool output against it. |
| `--color` | always \| never \| auto | Control ANSI color in stdout (default `auto`). |
| `--json, --experimental-json` | boolean | Print newline-delimited JSON events instead of formatted text (default `false`). |
| `--output-last-message, -o` | path | Write the assistant’s final message to a file. Useful for downstream scripting. |
| `-c, --config` | key=value | Inline configuration override for the non-interactive run (repeatable). |

#### `codex exec resume`

| Key | Type | Description |
| --- | --- | --- |
| `SESSION_ID` | uuid | Resume the specified session. Omit and use `--last` to continue the most recent session. |
| `--last` | boolean | Resume the most recent conversation from the current working directory (default `false`). |
| `--all` | boolean | Include sessions outside the current working directory when selecting the most recent session (default `false`). |
| `--image, -i` | path[,path...] | Attach one or more images to the follow-up prompt. Separate multiple paths with commas or repeat the flag. |
| `PROMPT` | string \| - (read stdin) | Optional follow-up instruction sent immediately after resuming. |

### `codex execpolicy`

Check `execpolicy` rule files before you save them.

| Key | Type | Description |
| --- | --- | --- |
| `--rules, -r` | path | Path to an execpolicy rule file to evaluate. Provide multiple flags to combine rules across files (repeatable). |
| `--pretty` | boolean | Pretty-print the JSON result (default `false`). |
| `COMMAND...` | var-args | Command to be checked against the specified policies. |

### `codex login`

Authenticate the CLI.

| Key | Type | Description |
| --- | --- | --- |
| `--with-api-key` | boolean | Read an API key from stdin (for example `printenv OPENAI_API_KEY \| codex login --with-api-key`). |
| `--device-auth` | boolean | Use OAuth device code flow instead of launching a browser window. |
| `status subcommand` | `codex login status` | Print the active authentication mode and exit with 0 when logged in. |

### `codex logout`

Remove saved credentials. This command has no flags.

### `codex mcp`

Manage Model Context Protocol server entries stored in `~/.codex/config.toml`.

| Command | Usage | Description |
| --- | --- | --- |
| `list` | `codex mcp list` | List configured MCP servers. Add `--json` for machine-readable output. |
| `get` | `codex mcp get <name>` | Show a specific server configuration. `--json` prints the raw config entry. |
| `add` | `codex mcp add <name>` | Register a server using a stdio launcher command or a streamable HTTP URL. Supports `--env KEY=VALUE` for stdio transports. |
| `remove` | `codex mcp remove <name>` | Delete a stored MCP server definition. |
| `login` | `codex mcp login <name>` | Start an OAuth login for a streamable HTTP server. Supports `--scopes scope1,scope2`. |
| `logout` | `codex mcp logout <name>` | Remove stored OAuth credentials for a streamable HTTP server. |

#### `codex mcp add` options:

| Key | Type | Description |
| --- | --- | --- |
| `COMMAND...` | stdio transport | Executable plus arguments to launch the MCP server. Provide after `--`. |
| `--env KEY=VALUE` | repeatable | Environment variable assignments applied when launching a stdio server. |
| `--url` | https://… | Register a streamable HTTP server instead of stdio. Mutually exclusive with `COMMAND...`. |
| `--bearer-token-env-var` | ENV_VAR | Environment variable whose value is sent as a bearer token when connecting to a streamable HTTP server. |

### `codex plugin marketplace`

| Command | Usage | Description |
| --- | --- | --- |
| `add` | `codex plugin marketplace add <source>` | Install a plugin marketplace from GitHub shorthand, a Git URL, an SSH URL, or a local marketplace root directory. `--sparse` is supported only for Git sources and can be repeated. Supports `--ref REF`. |
| `upgrade` | `codex plugin marketplace upgrade [name]` | Refresh one configured Git marketplace, or all configured Git marketplaces when no name is provided. |
| `remove` | `codex plugin marketplace remove <name>` | Remove a configured plugin marketplace. |

### `codex mcp-server`

Run Codex as an MCP server over stdio so that other tools can connect. This command inherits global configuration overrides and exits when the downstream client closes the connection.

### `codex resume`

Continue an interactive session by ID or resume the most recent conversation.

| Key | Type | Description |
| --- | --- | --- |
| `SESSION_ID` | uuid | Resume the specified session. Omit and use `--last` to continue the most recent session. |
| `--last` | boolean | Skip the picker and resume the most recent conversation from the current working directory (default `false`). |
| `--all` | boolean | Include sessions outside the current working directory when selecting the most recent session (default `false`). |

### `codex fork`

Fork a previous interactive session into a new thread.

| Key | Type | Description |
| --- | --- | --- |
| `SESSION_ID` | uuid | Fork the specified session. Omit and use `--last` to fork the most recent session. |
| `--last` | boolean | Skip the picker and fork the most recent conversation automatically (default `false`). |
| `--all` | boolean | Show sessions beyond the current working directory in the picker (default `false`). |

### `codex sandbox`

Run arbitrary commands inside Codex-provided macOS, Linux, or Windows sandboxes.

#### `codex sandbox macOS` seatbelt options:

| Key | Type | Description |
| --- | --- | --- |
| `--permissions-profile` | NAME | Apply a named permissions profile from the active configuration stack. |
| `--cd, -C` | DIR | Working directory used for profile resolution and command execution. Requires `--permissions-profile`. |
| `--include-managed-config` | boolean | Include managed requirements while resolving an explicit permissions profile. Requires `--permissions-profile` (default `false`). |
| `--allow-unix-socket` | path | Allow the sandboxed command to bind or connect Unix sockets rooted at this path. Repeat to allow multiple paths. |
| `--log-denials` | boolean | Capture macOS sandbox denials with `log stream` while the command runs and print them after exit (default `false`). |
| `--config, -c` | key=value | Pass configuration overrides into the sandboxed run (repeatable). |
| `COMMAND...` | var-args | Shell command to execute under macOS Seatbelt. Everything after `--` is forwarded. |

#### `codex sandbox Linux` Landlock options:

| Key | Type | Description |
| --- | --- | --- |
| `--permissions-profile` | NAME | Apply a named permissions profile from the active configuration stack. |
| `--cd, -C` | DIR | Working directory used for profile resolution and command execution. Requires `--permissions-profile`. |
| `--include-managed-config` | boolean | Include managed requirements while resolving an explicit permissions profile. Requires `--permissions-profile` (default `false`). |
| `--config, -c` | key=value | Configuration overrides applied before launching the sandbox (repeatable). |
| `COMMAND...` | var-args | Command to execute under Landlock + seccomp. Provide the executable after `--`. |

#### `codex sandbox Windows` options:

| Key | Type | Description |
| --- | --- | --- |
| `--permissions-profile` | NAME | Apply a named permissions profile from the active configuration stack. |
| `--cd, -C` | DIR | Working directory used for profile resolution and command execution. Requires `--permissions-profile`. |
| `--include-managed-config` | boolean | Include managed requirements while resolving an explicit permissions profile. Requires `--permissions-profile` (default `false`). |
| `--config, -c` | key=value | Configuration overrides applied before launching the sandbox (repeatable). |
| `COMMAND...` | var-args | Command to execute under the native Windows sandbox. Provide the executable after `--`. |

### `codex update`

Check for and apply a Codex CLI update when the installed release supports self-update.

## Flag combinations and safety tips

- Use `--sandbox workspace-write` for unattended local work that can stay inside the workspace, and avoid `--dangerously-bypass-approvals-and-sandbox` (or `--yolo`) unless you are inside a dedicated sandbox VM.
- When you need to grant Codex write access to more directories, prefer `--add-dir` rather than forcing `--sandbox danger-full-access`.
- Pair `--json` with `--output-last-message` in CI to capture progress and a final natural-language summary.
