# VM Setup v8 - The Real Solution

**Date**: 2026-06-12T05:14 UTC
**Status**: Solution identified from v7 logs

## The Real Issue (Not a Paradox!)

Looking at the v7 logs more carefully, the issue is **NOT** a permissions paradox. It's a simple npm configuration problem:

```
npm error code EACCES
npm error path /usr/lib/node_modules/bobshell
npm error Error: EACCES: permission denied, mkdir '/usr/lib/node_modules/bobshell'
```

**The Problem**: The Bob Shell installer script runs `npm install` as the user, but npm tries to install globally to `/usr/lib/node_modules/` which requires root permissions.

**The Solution**: Configure npm to use a user-level global directory BEFORE running the Bob Shell installer.

## Why v7 Failed

The v7 script ran the Bob Shell installer as user:
```bash
su - malhitticrypto -c "curl -fsSL https://bob.ibm.com/download/bobshell.sh | bash"
```

The installer internally runs:
```bash
npm install -g bobshell@1.0.4
```

But npm's default global directory (`/usr/lib/node_modules/`) requires root, so it fails with EACCES.

## The v8 Solution

**Configure npm to use user-level global directory**:

```bash
# Set npm prefix to user's home directory
su - malhitticrypto -c "npm config set prefix ~/.npm-global"

# Add to PATH
su - malhitticrypto -c "echo 'export PATH=~/.npm-global/bin:\$PATH' >> ~/.bashrc"
su - malhitticrypto -c "echo 'export PATH=~/.npm-global/bin:\$PATH' >> ~/.profile"

# Reload environment
su - malhitticrypto -c "source ~/.bashrc"

# NOW run Bob Shell installer (will use ~/.npm-global)
su - malhitticrypto -c "curl -fsSL https://bob.ibm.com/download/bobshell.sh | bash"
```

This way:
1. ✅ npm installs to `~/.npm-global/lib/node_modules/` (user-writable)
2. ✅ `bob` binary goes to `~/.npm-global/bin/` (user-writable)
3. ✅ PATH includes `~/.npm-global/bin/` (bob command works)
4. ✅ Everything runs as user (no root needed)

## Why This Works

**npm global installation** has two modes:
- **System-wide** (default): `/usr/lib/node_modules/` - requires root
- **User-level** (configured): `~/.npm-global/` - no root needed

By setting `npm config set prefix ~/.npm-global`, we tell npm to use user-level mode.

The Bob Shell installer respects this configuration because it uses npm internally.

## v8 Script Structure

```bash
#!/bin/bash
# VM Startup Script v8 - User-level npm configuration

# 1. Install Node.js 22.x (as root)
curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
apt-get install -y nodejs

# 2. Configure npm for user-level global installs (as user)
su - malhitticrypto -c "npm config set prefix ~/.npm-global"
su - malhitticrypto -c "echo 'export PATH=~/.npm-global/bin:\$PATH' >> ~/.bashrc"
su - malhitticrypto -c "echo 'export PATH=~/.npm-global/bin:\$PATH' >> ~/.profile"

# 3. Run Bob Shell installer (as user, will use ~/.npm-global)
su - malhitticrypto -c "bash -l -c 'curl -fsSL https://bob.ibm.com/download/bobshell.sh | bash'"

# 4. Verify (as user, with updated PATH)
su - malhitticrypto -c "bash -l -c 'bob --version'"
```

## Key Differences from v7

| Aspect | v7 (Failed) | v8 (Should Work) |
|--------|-------------|------------------|
| npm prefix | Default (`/usr/lib`) | User-level (`~/.npm-global`) |
| Installation target | System directory | User directory |
| Permissions needed | Root (EACCES) | User only |
| PATH configuration | Not set | Explicitly set |
| Shell invocation | `bash -c` | `bash -l -c` (login shell, loads .bashrc) |

## Why Previous Analysis Was Wrong

I incorrectly concluded it was a "permissions paradox" because I thought:
- Running as user = permission denied ❌
- Running as root = installs for root only ❌

But the real issue was:
- Running as user with **default npm config** = permission denied ✅
- Running as user with **user-level npm config** = works perfectly ✅

The solution was always there - just needed to configure npm properly!

## Confidence Level

**HIGH** - This is a well-documented npm configuration pattern:
- Used by millions of Node.js developers
- Recommended by npm documentation
- Standard practice for non-root npm global installs
- The Bob Shell installer will respect npm's prefix configuration

## Next Steps

1. Create v8 startup script with npm prefix configuration
2. Launch v8 VM
3. Wait 8 minutes for setup
4. Verify Bob Shell installation
5. If successful, create golden image and proceed with Wave 2

## References

- **npm docs**: https://docs.npmjs.com/resolving-eacces-permissions-errors-when-installing-packages-globally
- **v7 logs**: Showed EACCES error and npm trying to write to `/usr/lib/node_modules/`
- **Bob Shell installer**: Uses npm internally, respects npm config

---

**Status**: Ready to create v8 script
**Confidence**: HIGH - standard npm configuration pattern
**Expected Result**: Bob Shell installs successfully to `~/.npm-global/`