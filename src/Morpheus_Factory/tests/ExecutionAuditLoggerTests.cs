using Morpheus.Data;
using Morpheus.Execution;

namespace Morpheus.Tests
{
    public class ExecutionAuditLoggerTests
    {
        [Fact]
        public async Task LogExecutionAsyncShouldWriteToDatabase()
        {
            string dbPath = Path.GetTempFileName();
            try
            {
                ExecutionAuditLogger logger = new ExecutionAuditLogger(dbPath);
                await logger.InitializeAsync();

                ExecutionResult result = new ExecutionResult { Account = "TEST_ACC", Success = true };
                await logger.LogExecutionAsync(result);

                var count = await logger.GetLogCountAsync();
                Assert.Equal(1, count);
            }
            finally
            {
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                }
            }
        }
    }
}
