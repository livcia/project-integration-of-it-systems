namespace jira.Data;

public static class ConnectionStringHelper
{
    public static string Build()
    {
        var host = GetRequired("POSTGRES_HOST");
        var port = GetRequired("POSTGRES_PORT");
        var db = GetRequired("POSTGRES_DB");
        var user = GetRequired("POSTGRES_USER");
        var pass = GetRequired("POSTGRES_PASSWORD");

        return $"Host={host};Port={port};Database={db};Username={user};Password={pass};" +
               "Trust Server Certificate=true;SSL Mode=Disable";
    }

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Brakuje zmiennej środowiskowej '{name}'. Uzupełnij plik .env.");
}
