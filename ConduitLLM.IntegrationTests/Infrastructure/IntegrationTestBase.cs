using System.Net.Http.Headers;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Respawn;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.RabbitMq;
using Xunit.Abstractions;

namespace ConduitLLM.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests providing containerized infrastructure.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected PostgreSqlContainer PostgresContainer { get; private set; }
    protected RedisContainer RedisContainer { get; private set; }
    protected RabbitMqContainer RabbitMqContainer { get; private set; }
    protected WebApplicationFactory<Program> Factory { get; private set; }
    protected HttpClient Client { get; private set; }
    protected IServiceProvider Services { get; private set; }

    private Respawner _respawner;
    private string _postgresConnectionString;

    /// <summary>
    /// Gets a value indicating whether to use real infrastructure containers.
    /// Override in derived classes to control infrastructure usage.
    /// </summary>
    protected virtual bool UseRealInfrastructure => true;

    /// <summary>
    /// Gets a value indicating whether to use RabbitMQ.
    /// </summary>
    protected virtual bool UseRabbitMq => false;

    /// <summary>
    /// Gets test output helper for logging.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    public async Task InitializeAsync()
    {
        if (UseRealInfrastructure)
        {
            // Start PostgreSQL container
            PostgresContainer = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithPortBinding(5432, true)
                .WithDatabase("conduit_test")
                .WithUsername("conduit_test")
                .WithPassword("conduit_test")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
                .Build();

            await PostgresContainer.StartAsync();
            _postgresConnectionString = PostgresContainer.GetConnectionString();
            Output.WriteLine($"PostgreSQL started on: {_postgresConnectionString}");

            // Start Redis container
            RedisContainer = new RedisBuilder()
                .WithImage("redis:7-alpine")
                .WithPortBinding(6379, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
                .Build();

            await RedisContainer.StartAsync();
            var redisConnectionString = RedisContainer.GetConnectionString();
            Output.WriteLine($"Redis started on: {redisConnectionString}");

            // Start RabbitMQ container if needed
            if (UseRabbitMq)
            {
                RabbitMqContainer = new RabbitMqBuilder()
                    .WithImage("rabbitmq:3-management-alpine")
                    .WithPortBinding(5672, true)
                    .WithPortBinding(15672, true)
                    .WithUsername("conduit_test")
                    .WithPassword("conduit_test")
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilPortIsAvailable(5672)
                        .UntilPortIsAvailable(15672))
                    .Build();

                await RabbitMqContainer.StartAsync();
                Output.WriteLine($"RabbitMQ started on: {RabbitMqContainer.GetConnectionString()}");
            }
        }

        // Create WebApplicationFactory
        Factory = new IntegrationTestWebApplicationFactory(
            ConfigureServices,
            ConfigureWebHost,
            _postgresConnectionString,
            RedisContainer?.GetConnectionString(),
            RabbitMqContainer?.GetConnectionString(),
            Output
        );

        // Create HTTP client
        Client = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(Output);
            });
        }).CreateClient();

        Services = Factory.Services;

        // Initialize database respawner for cleanup between tests
        if (UseRealInfrastructure)
        {
            await InitializeRespawner();
        }

        await OnInitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await OnDisposeAsync();

        Client?.Dispose();
        await Factory.DisposeAsync();

        if (PostgresContainer != null)
        {
            await PostgresContainer.StopAsync();
            await PostgresContainer.DisposeAsync();
        }

        if (RedisContainer != null)
        {
            await RedisContainer.StopAsync();
            await RedisContainer.DisposeAsync();
        }

        if (RabbitMqContainer != null)
        {
            await RabbitMqContainer.StopAsync();
            await RabbitMqContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Override to provide additional service configuration.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Override to provide additional web host configuration.
    /// </summary>
    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    /// <summary>
    /// Override to perform additional initialization.
    /// </summary>
    protected virtual Task OnInitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Override to perform additional cleanup.
    /// </summary>
    protected virtual Task OnDisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates an authenticated HTTP client with the specified API key.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(string apiKey)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    /// <summary>
    /// Resets the database to a clean state.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        if (_respawner != null)
        {
            await _respawner.ResetAsync(_postgresConnectionString);
            Output.WriteLine("Database reset completed");
        }
    }

    /// <summary>
    /// Gets a service from the DI container.
    /// </summary>
    protected T GetService<T>()
    {
        return Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a scoped service from the DI container.
    /// </summary>
    protected async Task<T> GetScopedServiceAsync<T>(Func<T, Task> action)
    {
        using var scope = Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<T>();
        await action(service);
        return service;
    }

    private async Task InitializeRespawner()
    {
        if (!UseRealInfrastructure || string.IsNullOrEmpty(_postgresConnectionString))
            return;

        _respawner = await Respawner.CreateAsync(_postgresConnectionString, new RespawnerOptions
        {
            TablesToIgnore = new Respawn.Graph.Table[] { new("public", "__EFMigrationsHistory") },
            DbAdapter = DbAdapter.Postgres
        });
    }

    private class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly Action<IServiceCollection> _configureServices;
        private readonly Action<IWebHostBuilder> _configureWebHost;
        private readonly string _postgresConnectionString;
        private readonly string _redisConnectionString;
        private readonly string _rabbitMqConnectionString;
        private readonly ITestOutputHelper _output;

        public IntegrationTestWebApplicationFactory(
            Action<IServiceCollection> configureServices,
            Action<IWebHostBuilder> configureWebHost,
            string postgresConnectionString,
            string redisConnectionString,
            string rabbitMqConnectionString,
            ITestOutputHelper output)
        {
            _configureServices = configureServices;
            _configureWebHost = configureWebHost;
            _postgresConnectionString = postgresConnectionString;
            _redisConnectionString = redisConnectionString;
            _rabbitMqConnectionString = rabbitMqConnectionString;
            _output = output;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Set DATABASE_URL for ConnectionStringManager
            var databaseUrl = _postgresConnectionString ?? "postgresql://conduit_test:conduit_test@localhost:5432/conduit_test";
            Environment.SetEnvironmentVariable("DATABASE_URL", databaseUrl);

            // Configure connection strings
            builder.UseSetting("ConnectionStrings:DefaultConnection", _postgresConnectionString ?? "Host=localhost;Database=conduit_test;Username=conduit_test;Password=conduit_test");
            builder.UseSetting("ConnectionStrings:Redis", _redisConnectionString ?? "localhost:6379");
            
            if (!string.IsNullOrEmpty(_rabbitMqConnectionString))
            {
                builder.UseSetting("CONDUITLLM__RABBITMQ__HOST", "localhost");
                builder.UseSetting("CONDUITLLM__RABBITMQ__PORT", "5672");
                builder.UseSetting("CONDUITLLM__RABBITMQ__USERNAME", "conduit_test");
                builder.UseSetting("CONDUITLLM__RABBITMQ__PASSWORD", "conduit_test");
            }

            builder.ConfigureServices(services =>
            {
                // Add test logging
                services.AddSingleton(_output);
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new XUnitLoggerProvider(_output));
                    logging.SetMinimumLevel(LogLevel.Information);
                });

                _configureServices?.Invoke(services);
            });

            _configureWebHost?.Invoke(builder);

            base.ConfigureWebHost(builder);
        }
    }

    private class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XUnitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XUnitLogger(_output, categoryName);
        }

        public void Dispose()
        {
        }
    }

    private class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XUnitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                _output.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}");
                if (exception != null)
                {
                    _output.WriteLine($"Exception: {exception}");
                }
            }
            catch
            {
                // Ignore logging errors in tests
            }
        }
    }
}