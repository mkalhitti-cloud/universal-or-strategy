$json = Get-Content threads.json | ConvertFrom-Json
$threads = $json.data.repository.pullRequest.reviewThreads.nodes | Where-Object { $_.isResolved -eq $false }
foreach ($t in $threads) {
    $id = $t.id
    gh api graphql -f query='mutation($tid: ID!) { resolveReviewThread(input: { threadId: $tid }) { thread { isResolved } } }' -f tid=$id
}
