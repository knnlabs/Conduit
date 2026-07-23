using ConduitLLM.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConduitLLM.Tests.TestInfrastructure;

/// <summary>
/// Owns an isolated shared-cache SQLite in-memory database for one test instance.
/// A keeper connection preserves the database while contexts use independent
/// connections, matching production context lifetimes and allowing real transactions.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable, IAsyncDisposable
{
    private readonly SqliteConnection _keeperConnection;
    private bool _disposed;

    public SqliteTestDatabase(params IInterceptor[] interceptors)
    {
        var databaseName = $"conduit-tests-{Guid.NewGuid():N}";
        ConnectionString =
            $"Data Source={databaseName};Mode=Memory;Cache=Shared;Foreign Keys=True;Default Timeout=5";

        _keeperConnection = new SqliteConnection(ConnectionString);
        _keeperConnection.Open();

        var optionsBuilder = new DbContextOptionsBuilder<ConduitDbContext>()
            .UseSqlite(ConnectionString)
            .EnableSensitiveDataLogging();

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        Options = optionsBuilder.Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public string ConnectionString { get; }

    public DbContextOptions<ConduitDbContext> Options { get; }

    public ConduitDbContext CreateContext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new TestConduitDbContext(Options);
    }

    public IDbContextFactory<ConduitDbContext> CreateDbContextFactory() =>
        new TestDbContextFactory(CreateContext);

    public void Seed(Action<ConduitDbContext> seedAction)
    {
        using var context = CreateContext();
        seedAction(context);
        context.ChangeTracker.Clear();
    }

    public async Task SeedAsync(
        Func<ConduitDbContext, Task> seedAction,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        cancellationToken.ThrowIfCancellationRequested();
        await seedAction(context);
        context.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _keeperConnection.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _keeperConnection.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
