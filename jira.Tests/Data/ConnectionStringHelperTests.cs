using jira.Data;

namespace jira.Tests.Data;

/// <summary>
/// Testy jednostkowe dla ConnectionStringHelper.
/// </summary>
public class ConnectionStringHelperTests
{
    // Klucze zmiennych środowiskowych wymaganych przez ConnectionStringHelper.Build()
    private static readonly string[] RequiredVars =
    [
        "POSTGRES_HOST",
        "POSTGRES_PORT",
        "POSTGRES_DB",
        "POSTGRES_USER",
        "POSTGRES_PASSWORD"
    ];

    /// <summary>
    /// Ustawia wszystkie wymagane zmienne środowiskowe na potrzeby testu,
    /// a po jego zakończeniu przywraca poprzednie wartości.
    /// </summary>
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

    // ─── Build() ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_WhenAllEnvVarsSet_ReturnsCorrectConnectionString()
    {
        // Arrange
        var originals = SetEnvVars(
            host: "db-host",
            port: "5433",
            db: "mydb",
            user: "myuser",
            pass: "mypass");

        try
        {
            // Act
            var result = ConnectionStringHelper.Build();

            // Assert
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

    [Fact]
    public void Build_WhenAllEnvVarsSet_ReturnsStringInExpectedFormat()
    {
        // Arrange
        var originals = SetEnvVars();

        try
        {
            // Act
            var result = ConnectionStringHelper.Build();

            // Assert – format musi zaczynać się od "Host="
            Assert.StartsWith("Host=", result);
            // Musi zawierać średniki oddzielające klucze
            Assert.Contains(";", result);
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
        // Arrange – ustaw wszystkie zmienne, a następnie usuń jedną
        var originals = SetEnvVars();
        var missingOriginal = Environment.GetEnvironmentVariable(missingVar);
        Environment.SetEnvironmentVariable(missingVar, null);

        try
        {
            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => ConnectionStringHelper.Build());
            Assert.Contains(missingVar, ex.Message);
        }
        finally
        {
            // Restore
            Environment.SetEnvironmentVariable(missingVar, missingOriginal);
            RestoreEnvVars(originals);
        }
    }

    [Fact]
    public void Build_WhenAllVarsMissing_ThrowsInvalidOperationException()
    {
        // Arrange – usuń wszystkie zmienne
        var originals = new Dictionary<string, string?>();
        foreach (var key in RequiredVars)
        {
            originals[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }

        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => ConnectionStringHelper.Build());
        }
        finally
        {
            RestoreEnvVars(originals);
        }
    }

    // ─── GetRequired() via reflection ─────────────────────────────────────────
    // GetRequired jest prywatną metodą statyczną – testujemy ją przez Build()
    // (powyżej) oraz przez mechanizm refleksji poniżej.

    [Fact]
    public void GetRequired_WhenVarExists_ReturnsValue()
    {
        // Arrange
        const string varName = "CSHTEST_TEMP_VAR";
        const string expectedValue = "hello_world";
        var original = Environment.GetEnvironmentVariable(varName);
        Environment.SetEnvironmentVariable(varName, expectedValue);

        try
        {
            // Act – wywołaj prywatną metodę przez refleksję
            var method = typeof(ConnectionStringHelper)
                .GetMethod("GetRequired",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);

            Assert.NotNull(method); // upewnij się, że metoda istnieje

            var result = (string?)method!.Invoke(null, [varName]);

            // Assert
            Assert.Equal(expectedValue, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, original);
        }
    }

    [Fact]
    public void GetRequired_WhenVarMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        const string varName = "CSHTEST_NONEXISTENT_VAR_XYZ";
        var original = Environment.GetEnvironmentVariable(varName);
        Environment.SetEnvironmentVariable(varName, null);

        try
        {
            // Act – wywołaj prywatną metodę przez refleksję
            var method = typeof(ConnectionStringHelper)
                .GetMethod("GetRequired",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);

            Assert.NotNull(method);

            // Refleksja opakowuje wyjątek w TargetInvocationException
            var tex = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => method!.Invoke(null, [varName]));

            Assert.IsType<InvalidOperationException>(tex.InnerException);
            Assert.Contains(varName, tex.InnerException!.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, original);
        }
    }

    [Fact]
    public void GetRequired_ExceptionMessage_ContainsVariableName()
    {
        // Arrange
        const string missingVar = "CSHTEST_MISSING_VAR_ABC";
        Environment.SetEnvironmentVariable(missingVar, null);

        var method = typeof(ConnectionStringHelper)
            .GetMethod("GetRequired",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);

        // Act
        var tex = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => method!.Invoke(null, [missingVar]));

        // Assert
        var inner = tex.InnerException as InvalidOperationException;
        Assert.NotNull(inner);
        Assert.Contains(missingVar, inner!.Message);
        // Komunikat powinien wspominać o pliku .env
        Assert.Contains(".env", inner.Message);
    }
}
