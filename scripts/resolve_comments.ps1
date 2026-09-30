$ids = @(
  'PRRT_kwDOQ303Bc6DqaV3', 'PRRT_kwDOQ303Bc6DqaV7', 'PRRT_kwDOQ303Bc6DqaV_', 'PRRT_kwDOQ303Bc6DqbVA', 
  'PRRT_kwDOQ303Bc6DqcGP', 'PRRT_kwDOQ303Bc6DqcGS', 'PRRT_kwDOQ303Bc6Dqctp', 'PRRT_kwDOQ303Bc6Dqcts', 
  'PRRT_kwDOQ303Bc6Dqc_s', 'PRRT_kwDOQ303Bc6DqdyT', 'PRRT_kwDOQ303Bc6DqdyV', 'PRRT_kwDOQ303Bc6DqdyW', 
  'PRRT_kwDOQ303Bc6Dqe4Q', 'PRRT_kwDOQ303Bc6Dqe4S', 'PRRT_kwDOQ303Bc6Dqe4T', 'PRRT_kwDOQ303Bc6Dqe4W', 
  'PRRT_kwDOQ303Bc6Dqe4Z', 'PRRT_kwDOQ303Bc6Dqe4b', 'PRRT_kwDOQ303Bc6DrHBm', 'PRRT_kwDOQ303Bc6DrHBp', 
  'PRRT_kwDOQ303Bc6DrIZf', 'PRRT_kwDOQ303Bc6DrIZh', 'PRRT_kwDOQ303Bc6DrIZk', 'PRRT_kwDOQ303Bc6DrIZo', 
  'PRRT_kwDOQ303Bc6DrIZs', 'PRRT_kwDOQ303Bc6DrIZ2', 'PRRT_kwDOQ303Bc6D067y', 'PRRT_kwDOQ303Bc6D0673', 
  'PRRT_kwDOQ303Bc6D0674', 'PRRT_kwDOQ303Bc6D1MEc', 'PRRT_kwDOQ303Bc6D1MEv', 'PRRT_kwDOQ303Bc6D2UQZ', 
  'PRRT_kwDOQ303Bc6D2VUW'
)

foreach ($id in $ids) {
    Write-Host "Resolving thread $id..."
    gh api graphql -f query='mutation($id:ID!) { resolveReviewThread(input:{threadId:$id}) { thread { id isResolved } } }' -F id=$id
}
