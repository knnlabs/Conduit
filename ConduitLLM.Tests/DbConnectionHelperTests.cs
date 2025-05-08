#pragma warning disable CS0618  // Suppress obsolete method warnings
using System;
using Xunit;
using ConduitLLM.Core;

public class DbConnectionHelperTests
{
    [Fact]
    public void ReturnsPostgres_WhenDatabaseUrlIsSet()
    {
        var url = "postgresql://user:pass@localhost:5432/mydb";
        Environment.SetEnvironmentVariable("DATABASE_URL", url);
        Environment.SetEnvironmentVariable("CONDUIT_SQLITE_PATH", null);

        var (provider, connStr) = DbConnectionHelper.GetProviderAndConnectionString();
        Assert.Equal("postgres", provider, ignoreCase: true);
        Assert.Contains("Host=localhost", connStr);
        Assert.Contains("Database=mydb", connStr);
        Assert.Contains("Username=user", connStr);
        Assert.Contains("Password=pass", connStr);
    }

    [Fact]
    public void ReturnsSqlite_WhenOnlySqlitePathIsSet()
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        Environment.SetEnvironmentVariable("CONDUIT_SQLITE_PATH", "/tmp/test.db");

        var (provider, connStr) = DbConnectionHelper.GetProviderAndConnectionString();
        Assert.Equal("sqlite", provider, ignoreCase: true);
        Assert.Contains("/tmp/test.db", connStr);
    }

    [Fact]
    public void ReturnsSqlite_DefaultsToConduitConfigDb_WhenNoEnvVarsSet()
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", null);
        Environment.SetEnvironmentVariable("CONDUIT_SQLITE_PATH", null);

        var (provider, connStr) = DbConnectionHelper.GetProviderAndConnectionString();
        Assert.Equal("sqlite", provider, ignoreCase: true);
        Assert.Contains("ConduitConfig.db", connStr);
    }

    [Fact]
    public void Throws_WhenPostgresUrlMalformed()
    {
        // Test the ParsePostgresUrl method directly instead of through GetProviderAndConnectionString
        // because GetProviderAndConnectionString now has error handling that falls back to SQLite
        Assert.Throws<InvalidOperationException>(() => DbConnectionHelper.ParsePostgresUrl("postgresql://bad_url"));
    }

    [Fact]
    public void FallsBackToSqlite_WhenDatabaseUrlIsInvalidPrefix()
    {
        Environment.SetEnvironmentVariable("DATABASE_URL", "not_a_valid_url");
        Environment.SetEnvironmentVariable("CONDUIT_SQLITE_PATH", null);

        var (provider, connStr) = DbConnectionHelper.GetProviderAndConnectionString();
        Assert.Equal("sqlite", provider, ignoreCase: true);
        Assert.Contains("ConduitConfig.db", connStr);
    }

    [Fact]
    public void LogsProviderAndSanitizedConnectionString()
    {
        string? logged = null;
        var url = "postgresql://user:secret@localhost:5432/mydb";
        Environment.SetEnvironmentVariable("DATABASE_URL", url);
        Environment.SetEnvironmentVariable("CONDUIT_SQLITE_PATH", null);

        var (provider, connStr) = DbConnectionHelper.GetProviderAndConnectionString(msg => logged = msg);
        Assert.Equal("postgres", provider, ignoreCase: true);
        Assert.NotNull(logged);
        Assert.Contains("provider: postgres", logged);
        Assert.Contains("Password=*****", logged);
        Assert.DoesNotContain("secret", logged);
    }

    [Fact]
    public void ThrowsWithClearError_WhenPostgresMissingFields()
    {
        // Missing password
        var connStr = "Host=localhost;Port=5432;Database=mydb;Username=user;";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DbConnectionHelper.ValidateConnectionString("postgres", connStr)
        );
        Assert.Contains("Missing required fields", ex.Message);
    }

    [Fact]
    public void ThrowsWithClearError_WhenSqlitePathEmpty()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DbConnectionHelper.ValidateConnectionString("sqlite", "Data Source=")
        );
        // Just check that we get some error message
        Assert.NotNull(ex.Message);
    }
}
#pragma warning restore CS0618
