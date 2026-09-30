# PowerShell Syntax Rules

## Command Chaining

**CRITICAL**: PowerShell does NOT support `&&` for command chaining.

### Wrong (Bash syntax)
```powershell
cd c:/path && command
```

### Correct (PowerShell syntax)
```powershell
cd c:/path; command
```

OR use separate execute_command calls:
```xml
<execute_command>
<command>cd c:/path</command>
</execute_command>

<execute_command>
<command>command</command>
<cwd>c:/path</cwd>
</execute_command>
```

OR use the `cwd` parameter:
```xml
<execute_command>
<command>command</command>
<cwd>c:/path</cwd>
</execute_command>
```

## Enforcement

**ALWAYS** use the `cwd` parameter when you need to run a command in a specific directory on Windows.

**NEVER** use `&&` in PowerShell commands.

## Detection

If you see this error:
```
The token '&&' is not a valid statement separator in this version.
```

You used bash syntax in PowerShell. Fix it immediately using the `cwd` parameter.