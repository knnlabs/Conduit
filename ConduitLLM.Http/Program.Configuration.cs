using System.Text.Json;
using System.Text.Json.Serialization;
using ConduitLLM.Configuration;

public partial class Program
{
    public static void ConfigureBasicSettings(WebApplicationBuilder builder)
    {
        // Use environment variables ONLY for configuration
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddEnvironmentVariables();

        // Database initialization strategy
        // We use a flexible approach that works for both development and production
        bool skipDatabaseInit = Environment.GetEnvironmentVariable("CONDUIT_SKIP_DATABASE_INIT") == "true";

        if (skipDatabaseInit)
        {
            Console.WriteLine("[Conduit] WARNING: Skipping database initialization. Ensure database schema is up to date.");
        }
        else
        {
            Console.WriteLine("[Conduit] Database will be initialized automatically.");
        }

        // Configure JSON options for snake_case serialization (OpenAI compatibility)
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Store JsonSerializerOptions in the builder's services for later use
        builder.Services.AddSingleton(jsonSerializerOptions);

        // 1. Configure Conduit Settings
        builder.Services.AddOptions<ConduitSettings>()
            .Bind(builder.Configuration.GetSection("Conduit"))
            .ValidateDataAnnotations(); // Add validation if using DataAnnotations in settings classes

        // Database settings loading removed - provider configuration is now entirely database-driven
    }
}