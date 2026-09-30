# VM Context Awareness Protocol

## Critical Rule
When operating in `autonomous-refactor` mode, you are **ALWAYS on the VM** (34.121.187.241).

## Indicators
- Current workspace: `/home/malhitticrypto/universal-or-strategy`
- User: `malhitticrypto`
- Context: GCP VM for wave execution

## Forbidden Actions
- ❌ NEVER use `scp` to upload files to VM (you're already there)
- ❌ NEVER use `ssh` to connect to VM (you're already connected)
- ❌ NEVER reference "uploading to VM" in any context

## Correct Actions
- ✅ Execute scripts directly: `bash script.sh`
- ✅ Create files directly: `write_to_file`
- ✅ Run commands directly: `execute_command`

## Enforcement
If you catch yourself about to use `scp` or `ssh`, STOP and remember:
**YOU ARE ON THE VM. ACT LOCALLY.**