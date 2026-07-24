using JasperFx;
using ConduitLLM.Gateway.Endpoints;
using ConduitLLM.Gateway.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

// "migrate" verb: run the standalone migrator (release-hook entry point) instead of
// the web host — e.g. `dotnet ConduitLLM.Gateway.dll migrate`.
if (ConduitLLM.Configuration.Data.MigrationCommand.Matches(args))
{
    return await ConduitLLM.Configuration.Data.MigrationCommand.RunAsync();
}

// prometheus-net's Meter adapter owns the /metrics representation. Configure its
// streaming histogram buckets before any application metrics can be initialized.
ConduitLLM.Gateway.Metrics.PrometheusMeterAdapterConfiguration.Configure();

// DatabaseAwareLLMClientFactory now in Providers namespace
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});

// Configure basic settings and environment
Program.ConfigureBasicSettings(builder);

// The build-time exporter creates a host to inspect endpoint metadata. Keep that host
// infrastructure-free: no database, Redis, messaging, migrations, or hosted services.
if (Environment.GetEnvironmentVariable("CONDUIT_OPENAPI_GENERATION") == "true")
{
    Program.ConfigureOpenApiServices(builder);
    builder.Services.AddAuthorization();
    var openApiApp = builder.Build();
    openApiApp.MapModelsEndpoints();
    openApiApp.MapGatewayApiEndpoints();

    var outputPath = Environment.GetEnvironmentVariable("CONDUIT_OPENAPI_OUTPUT");
    if (!string.IsNullOrWhiteSpace(outputPath))
    {
        openApiApp.Urls.Add("http://127.0.0.1:0");
        await openApiApp.StartAsync();
        try
        {
            await using var output = File.Create(outputPath);
            var provider = openApiApp.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
            var document = await provider.GetOpenApiDocumentAsync(default);
            await document.SerializeAsJsonAsync(output, OpenApiSpecVersion.OpenApi3_1, default);
        }
        finally
        {
            await openApiApp.StopAsync();
        }
        return 0;
    }

    await openApiApp.RunAsync();
    return 0;
}

// Configure all service registrations
Program.ConfigureCoreServices(builder);
Program.ConfigureSecurityServices(builder);
Program.ConfigureCachingServices(builder);
Program.ConfigureMessagingServices(builder);
Program.ConfigureSignalRServices(builder);
Program.ConfigureMediaServices(builder);
Program.ConfigureMonitoringServices(builder);
builder.Services.AddConduitRateLimiting(builder.Configuration);

var app = builder.Build();

// Configure middleware pipeline
await Program.ConfigureMiddleware(app);

// Configure endpoints
Program.ConfigureEndpoints(app);

// JasperFx command-line integration (#961): with no arguments this runs the web host
// exactly like app.Run(); with a command verb (e.g. `dotnet run -- codegen preview`)
// it executes the JasperFx command instead — CI uses `codegen preview` to compile
// every Wolverine handler chain build-ahead, so codegen/service-location defects
// (the class that hid W2, #929) fail at build time rather than first delivery.
return await app.RunJasperFxCommands(args);

// Make Program class accessible for testing
public partial class Program { }
