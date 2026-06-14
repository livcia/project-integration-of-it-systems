using jira.Data;

namespace jira.Tests.Data;

public class ConnectionStringHelperTests
{
    private static Dictionary<string, string?> SetEnvVars(
        string host = "localhost",
        string port = "5432",
        string db = "testdb",
        string user = "testuser",
        string pass = "testpass")
    {
        var originals = new Dictionary<string, string?>();
        var values = new Dictionary<string, string>
        {
            ["POSTGRES_HOST"] = host,
            ["POSTGRES_PORT"] = port,
            ["POSTGRES_DB"] = db,
            ["POSTGRES_USER"] = user,
            ["POSTGRES_PASSWORD"] = pass
        };

        foreach (var kv in values)
        {
            originals[kv.Key] = Environment.GetEnvironmentVariable(kv.Key);
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
        }

        return originals;
    }

    private static void RestoreEnvVars(Dictionary<string, string?> originals)
    {
        foreach (var kv in originals)
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
    }

    [Fact]
    public void Build_WhenAllEnvVarsSet_ReturnsCorrectConnectionString()
    {
        var originals = SetEnvVars(
            host: "db-host",
            port: "5433",
            db: "mydb",
            user: "myuser",
            pass: "mypass");

        try
        {
            var result = ConnectionStringHelper.Build();

            Assert.Contains("Host=db-host", result);
            Assert.Contains("Port=5433", result);
            Assert.Contains("Database=mydb", result);
            Assert.Contains("Username=myuser", result);
            Assert.Contains("Password=mypass", result);
            Assert.Contains("Trust Server Certificate=true", result);
            Assert.Contains("SSL Mode=Disable", result);
        }
        finally
        {
            RestoreEnvVars(originals);
        }
    }

    [Theory]
    [InlineData("POSTGRES_HOST")]
    [InlineData("POSTGRES_PORT")]
    [InlineData("POSTGRES_DB")]
    [InlineData("POSTGRES_USER")]
    [InlineData("POSTGRES_PASSWORD")]
    public void Build_WhenRequiredVarIsMissing_ThrowsInvalidOperationException(string missingVar)
    {
        var originals = SetEnvVars();
        var missingOriginal = Environment.GetEnvironmentVariable(missingVar);
        Environment.SetEnvironmentVariable(missingVar, null);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ConnectionStringHelper.Build());
            Assert.Contains(missingVar, ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(missingVar, missingOriginal);
            RestoreEnvVars(originals);
        }
    }
}
