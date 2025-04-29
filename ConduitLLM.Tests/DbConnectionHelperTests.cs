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
        // Triggers exception: valid prefix but invalid format
        Environment.SetEnvironmentVariable("DATABASE_URL", "postgresql://bad_url");
        Environment.SetEnvironmentVariable("CONDUIT_SQLITE_PATH", null);

        Assert.Throws<InvalidOperationException>(() => DbConnectionHelper.GetProviderAndConnectionString());
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
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            typeof(DbConnectionHelper).GetMethod("ValidatePostgres", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, new object[] { connStr })
        );
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Postgres connection string missing required fields", ex.InnerException!.Message);
    }

    [Fact]
    public void ThrowsWithClearError_WhenSqlitePathEmpty()
    {
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            typeof(DbConnectionHelper).GetMethod("ValidateSqlite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, new object[] { "" })
        );
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("SQLite path is empty", ex.InnerException!.Message);
    }
}
