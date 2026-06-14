namespace jira.Data;

public static class ConnectionStringHelper
{
    public static string Build()
    {
        var postgresHost = GetRequired("POSTGRES_HOST");
        var postgresPort = GetRequired("POSTGRES_PORT");
        var postgresDatabase = GetRequired("POSTGRES_DB");
        var postgresUser = GetRequired("POSTGRES_USER");
        var postgresPassword = GetRequired("POSTGRES_PASSWORD");

        return $"Host={postgresHost};Port={postgresPort};Database={postgresDatabase};Username={postgresUser};Password={postgresPassword};" +
               "Trust Server Certificate=true;SSL Mode=Disable";
    }

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Brakuje zmiennej środowiskowej '{name}'. Uzupełnij plik .env.");
}
