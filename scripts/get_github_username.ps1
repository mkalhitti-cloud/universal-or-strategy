# Get GitHub Username Helper
# Extracts GitHub username from git remote origin
# Usage: $username = & .\scripts\get_github_username.ps1

function Get-GitHubUsername {
    $remote = git remote get-url origin
    
    # Handle both HTTPS and SSH formats
    # HTTPS: https://github.com/username/repo.git
    # SSH: git@github.com:username/repo.git
    if ($remote -match 'github\.com[:/]([^/]+)/') {
        return $Matches[1]
    }
    
    throw "Could not extract GitHub username from remote: $remote"
}

# Execute and return
try {
    Get-GitHubUsername
} catch {
    Write-Error $_.Exception.Message
    exit 1
}

# Made with Bob
