using ConduitLLM.Admin.Endpoints;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Validation;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Converters;
using ConduitLLM.Security.Middleware;

using System.Text.Json;

using JasperFx;

using Scalar.AspNetCore;

namespace ConduitLLM.Admin;

/// <summary>
/// Entry point for the Admin API application
/// </summary>
public partial class Program
{
    /// <summary>
    /// Application entry point that configures and starts the web application
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Process exit code (nonzero when the "migrate" verb fails)</returns>
    public static async Task<int> Main(string[] args)
    {
        // "migrate" verb: run the standalone migrator (release-hook entry point)
        // instead of the web host — e.g. `dotnet ConduitLLM.Admin.dll migrate`.
        if (MigrationCommand.Matches(args))
        {
            return await MigrationCommand.RunAsync();
        }

        var builder = WebApplication.CreateBuilder(args);

        // Create a startup logger for structured logging during service registration
        using var startupLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var startupLogger = startupLoggerFactory.CreateLogger("ConduitLLM.Admin.Startup");

        // Add services to the container
        builder.Services.AddControllers()
            .AddJsonOptions(options => ConfigureAdminJson(options.JsonSerializerOptions))
            .ConfigureApiBehaviorOptions(options =>
            {
                // Tier 2a (#904): return the Admin API's standard ErrorResponseDto for automatic
                // [ApiController] model-validation failures instead of the default
                // ValidationProblemDetails — unifying the validation error shape with the rest of
                // the Admin API (AdminExceptionMiddleware also emits ErrorResponseDto).
                options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create;
            });

        // Minimal APIs use Microsoft.AspNetCore.Http.Json.JsonOptions rather than MVC's
        // JsonOptions. Keep both paths on the same configuration while controllers and endpoint
        // groups coexist. This registration is before the codegen branch, so the metadata-only
        // host and the production host use the same contract.
        builder.Services.ConfigureHttpJsonOptions(options =>
            ConfigureAdminJson(options.SerializerOptions));

        // Operation-logging action filter — replaces the per-action success logging that used to
        // live in AdminControllerBase.ExecuteAsync. Applied per controller via [ServiceFilter]
        // during the incremental Tier 1a migration (#902); promote to a global filter once all
        // controllers are converted.
        builder.Services.AddScoped<OperationLoggingFilter>();

        builder.Services.AddEndpointsApiExplorer();

        // Add HttpClient factory for provider connection testing
        builder.Services.AddHttpClient();

        // Configure built-in OpenAPI support
        builder.Services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<ConduitLLM.Admin.OpenApi.AdminApiDocumentTransformer>();
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.OperationMetadataTransformer>();
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.ApiKeySecurityOperationTransformer>();
            // Tier 2b (#905): document the universal 500 once, so controllers can drop the per-action
            // [ProducesResponseType(Status500InternalServerError)] boilerplate.
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.DefaultErrorResponsesOperationTransformer>();
            options.AddOperationTransformer<ConduitLLM.Admin.OpenApi.ResponseContractOperationTransformer>();
            options.AddSchemaTransformer<ConduitLLM.Admin.OpenApi.NumericSchemaTransformer>();
            options.AddSchemaTransformer<ConduitLLM.Admin.OpenApi.TemporalSchemaTransformer>();
            options.AddSchemaTransformer<ConduitLLM.Admin.OpenApi.ModelCostResponseSchemaTransformer>();
            options.AddSchemaTransformer<ConduitLLM.Admin.OpenApi.GlobalSettingsResponseSchemaTransformer>();
            options.AddSchemaTransformer<ConduitLLM.Admin.OpenApi.IpFilterResponseSchemaTransformer>();
            options.AddDocumentTransformer<ConduitLLM.Admin.OpenApi.OperationIdValidationDocumentTransformer>();
        });

        // The build-time exporter needs endpoint metadata, not infrastructure. Avoid Postgres,
        // Redis, messaging, migrations, and hosted services on this codegen-only path.
        if (Environment.GetEnvironmentVariable("CONDUIT_OPENAPI_GENERATION") == "true")
        {
            builder.Services.AddScoped<ConduitLLM.Configuration.Interfaces.IModelAuthorRepository,
                ConduitLLM.Configuration.Repositories.ModelAuthorRepository>();
            builder.Services.AddAuthorization(options =>
                options.AddPolicy("MasterKeyPolicy", policy => policy.RequireAssertion(_ => true)));
            var openApiApp = builder.Build();
            openApiApp.MapControllers();
            openApiApp.MapModelAuthorEndpoints();
            openApiApp.MapModelSeriesEndpoints();
            openApiApp.MapNotificationsEndpoints();
            openApiApp.MapAdminTasksEndpoints();
            openApiApp.MapAdminAuthEndpoints();
            openApiApp.MapSystemInfoEndpoints();
            openApiApp.MapAdminMetricsEndpoints();
            await openApiApp.RunAsync();
            return 0;
        }

        // Configure services (partial class methods)
        ConfigureCoreServices(builder, startupLogger);
        ConfigureMessagingServices(builder, startupLogger);
        ConfigureMonitoringServices(builder, startupLogger);

        // Configure trusted-proxy forwarded-header processing so the client IP is derived
        // securely (spoof-resistant). No-op unless CONDUIT_TRUSTED_PROXY_ENABLED=true.
        builder.Services.AddTrustedProxyForwardedHeaders(builder.Configuration);

        var app = builder.Build();

        // Log deprecation warnings and validate Redis URL
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            ConduitLLM.Configuration.Extensions.DeprecationWarnings.LogEnvironmentVariableDeprecations(logger);

            // Validate Redis URL if provided
            var envRedisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
            if (!string.IsNullOrEmpty(envRedisUrl))
            {
                ConduitLLM.Configuration.Services.RedisUrlValidator.ValidateAndLog(envRedisUrl, logger, "Admin Service");
            }
        }

        // Run database migration startup handling (CONDUIT_MIGRATION_MODE); Apply mode
        // also seeds default data under the migration lock.
        await app.RunDatabaseMigrationAsync();

        // Resolve the real client IP via trusted proxies. Must run before any IP-reading middleware:
        // HTTPS redirection honors X-Forwarded-Proto, and the /metrics gate reads the client IP.
        // No-op unless CONDUIT_TRUSTED_PROXY_ENABLED=true.
        app.UseTrustedProxyForwardedHeaders();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi("/openapi/v1.json");
            app.MapScalarApiReference();
            app.Logger.LogInformation("Scalar UI available at /scalar/v1");
        }

        // Only use HTTPS redirection if explicitly enabled
        var enableHttpsRedirection = Environment.GetEnvironmentVariable("CONDUIT_ENABLE_HTTPS_REDIRECTION") != "false";
        if (enableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        // Add health endpoint authorization (early in pipeline, before authentication)
        app.UseHealthEndpointAuthorization();

        // Add middleware for authentication and request tracking
        app.UseAdminMiddleware();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // Tier 3 pilot (#906): ModelAuthor served as Minimal-API endpoints (replaces ModelAuthorController).
        app.MapModelAuthorEndpoints();
        app.MapModelSeriesEndpoints();
        app.MapNotificationsEndpoints();
        app.MapAdminTasksEndpoints();
        app.MapAdminAuthEndpoints();
        app.MapSystemInfoEndpoints();
        app.MapAdminMetricsEndpoints();

        // Map SignalR hub with master key authentication
        app.MapHub<ConduitLLM.Admin.Hubs.AdminNotificationHub>("/hubs/admin-notifications");

        // Map monitoring endpoints (health, metrics, Prometheus)
        MapMonitoringEndpoints(app);

        // app.Urls throws when no server is present (e.g. under build-time OpenAPI
        // document generation, which builds the host without Kestrel) — read the
        // address feature null-safely instead.
        var serverAddresses = ((Microsoft.AspNetCore.Builder.IApplicationBuilder)app).ServerFeatures
            .Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()?.Addresses;
        app.Logger.LogInformation(
            "Admin API started — Environment: {Environment}, URLs: {Urls}",
            app.Environment.EnvironmentName,
            string.Join(", ", serverAddresses ?? Array.Empty<string>()));

        // JasperFx command-line integration (#961): with no arguments this runs the web
        // host exactly like app.Run(); with a command verb (e.g. `dotnet run -- codegen
        // preview`) it executes the JasperFx command instead — CI uses `codegen preview`
        // to compile every Wolverine handler chain build-ahead, so codegen/service-
        // location defects (the class that hid W2, #929) fail at build time rather than
        // first delivery.
        return await app.RunJasperFxCommands(args);
    }

    private static void ConfigureAdminJson(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new UtcDateTimeConverter());
        options.Converters.Add(new NullableUtcDateTimeConverter());
    }
}

// Make Program accessible for testing
public partial class Program { }
