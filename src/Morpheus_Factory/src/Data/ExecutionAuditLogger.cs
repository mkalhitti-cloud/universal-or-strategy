using Microsoft.Data.Sqlite;
using Morpheus.Execution;

namespace Morpheus.Data
{
    public class ExecutionAuditLogger(string dbPath)
    {
        private readonly string _connectionString = $"Data Source={dbPath}";

        public async Task InitializeAsync()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Account TEXT NOT NULL,
                Success INTEGER NOT NULL
            )";
            _ = await command.ExecuteNonQueryAsync();
        }

        public async Task LogExecutionAsync(ExecutionResult result)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO AuditLog (Account, Success) VALUES ($account, $success)";
            _ = command.Parameters.AddWithValue("$account", result.Account);
            _ = command.Parameters.AddWithValue("$success", result.Success ? 1 : 0);

            _ = await command.ExecuteNonQueryAsync();
        }

        public async Task<int> GetLogCountAsync()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AuditLog";

            return int.Parse((await command.ExecuteScalarAsync())?.ToString() ?? "0");
        }
    }
}
