namespace Morpheus.Execution
{
    public class ExecutionResult
    {
        public string Account { get; set; } = string.Empty;
        public bool Success { get; set; }
    }

    public class DirectExecutionRouter(string failAccount = "")
    {
        private readonly string _failAccount = failAccount;

        public async Task<List<ExecutionResult>> RouteOrderAsync(string symbol, int quantity, bool isBuy, List<string> accounts)
        {
            List<Task<ExecutionResult>> tasks = new List<Task<ExecutionResult>>();

            foreach (var account in accounts)
            {
                tasks.Add(Task.Run(() => ExecuteForAccount(account)));
            }

            var results = await Task.WhenAll(tasks);
            return [.. results];
        }

        private ExecutionResult ExecuteForAccount(string account)
        {
            // Simulate fault tolerance for B3.2
            bool success = !string.Equals(account, _failAccount, StringComparison.OrdinalIgnoreCase);

            return new ExecutionResult
            {
                Account = account,
                Success = success
            };
        }
    }
}
