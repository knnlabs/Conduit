using System.Text.Json;
using System.Text.Json.Serialization;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Converters;
using ConduitLLM.Gateway.Options;

public partial class Program
{
    public static void ConfigureBasicSettings(WebApplicationBuilder builder)
    {
        // Use environment variables ONLY for configuration
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddEnvironmentVariables();

        // Configure JSON options for snake_case serialization (OpenAI compatibility)
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new UtcDateTimeConverter(), new NullableUtcDateTimeConverter() }
        };

        // Store JsonSerializerOptions in the builder's services for later use
        builder.Services.AddSingleton(jsonSerializerOptions);

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
