using ConduitLLM.Configuration;
using ConduitLLM.Gateway.Options;
using Microsoft.Extensions.Options;

public partial class Program
{
    public static void ConfigureBasicSettings(WebApplicationBuilder builder)
    {
        // Use environment variables ONLY for configuration
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddEnvironmentVariables();

        builder.Services.ConfigureHttpJsonOptions(options =>
            GatewayJsonOptions.Configure(options.SerializerOptions));
        builder.Services.AddSingleton(services =>
            services
                .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
                .Value
                .SerializerOptions);

        // 1. Configure Conduit Settings
        builder.Services.AddOptions<ConduitSettings>()
            .Bind(builder.Configuration.GetSection("Conduit"))
            .ValidateDataAnnotations(); // Add validation if using DataAnnotations in settings classes

        builder.Services.AddOptions<UsageTrackingOptions>()
            .Bind(builder.Configuration.GetSection("UsageTracking"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.Configure<HostOptions>(options =>
        {
            var shutdownSeconds = builder.Configuration.GetValue<int?>(
                "UsageTracking:GracefulShutdownSeconds") ?? 45;
            options.ShutdownTimeout = TimeSpan.FromSeconds(shutdownSeconds);
        });

        builder.Services.AddOptions<BillingAdmissionOptions>()
            .Bind(builder.Configuration.GetSection(BillingAdmissionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

    }
}
