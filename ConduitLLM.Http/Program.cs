// DatabaseAwareLLMClientFactory now in Providers namespace
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
});

// Configure basic settings and environment
Program.ConfigureBasicSettings(builder);

// Configure all service registrations
Program.ConfigureCoreServices(builder);
Program.ConfigureSecurityServices(builder);
Program.ConfigureCachingServices(builder);
Program.ConfigureMessagingServices(builder);
Program.ConfigureSignalRServices(builder);
Program.ConfigureMediaServices(builder);
Program.ConfigureMonitoringServices(builder);

var app = builder.Build();

// Configure middleware pipeline
await Program.ConfigureMiddleware(app);

// Configure endpoints
Program.ConfigureEndpoints(app);

Console.WriteLine("[Conduit] All endpoints configured, starting application...");
app.Run();

// Make Program class accessible for testing
public partial class Program { }