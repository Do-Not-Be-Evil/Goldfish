using Microsoft.Data.SqlClient;

namespace Goldfish.IntegrationTests.Infrastructure;

/// <summary>Runs a repository .sql file as one batch, replacing sqlcmd-style $(Name) variables.</summary>
public static class SqlScriptLoader
{
    public static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Goldfish.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine([directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), .. parts]);
    }

    public static async Task RunAsync(
        string connectionString,
        string scriptPath,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        var text = await File.ReadAllTextAsync(scriptPath, cancellationToken);
        foreach (var (name, value) in variables)
        {
            text = text.Replace($"$({name})", value, StringComparison.Ordinal);
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(text, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
