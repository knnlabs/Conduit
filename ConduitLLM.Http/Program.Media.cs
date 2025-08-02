using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Services;

public partial class Program
{
    public static void ConfigureMediaServices(WebApplicationBuilder builder)
    {
        // Register Media Storage Service
        var storageProvider = builder.Configuration.GetValue<string>("ConduitLLM:Storage:Provider") ?? "InMemory";
        Console.WriteLine($"[Conduit] Storage Provider: {storageProvider}");
        if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[Conduit] Configuring S3 Media Storage Service");
            builder.Services.Configure<ConduitLLM.Core.Options.S3StorageOptions>(
                builder.Configuration.GetSection(ConduitLLM.Core.Options.S3StorageOptions.SectionName));
            builder.Services.AddSingleton<IMediaStorageService, S3MediaStorageService>();
        }
        else
        {
            Console.WriteLine("[Conduit] Using In-Memory Media Storage (development mode)");
            // Use in-memory storage for development
            builder.Services.AddSingleton<IMediaStorageService>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<InMemoryMediaStorageService>>();
                
                // Try to get the public base URL from configuration
                var mediaBaseUrl = builder.Configuration["CONDUITLLM:MEDIA_BASE_URL"] 
                    ?? builder.Configuration["Media:BaseUrl"]
                    ?? builder.Configuration["CONDUIT_MEDIA_BASE_URL"]
                    ?? Environment.GetEnvironmentVariable("CONDUITLLM__MEDIA_BASE_URL");
                    
                // If not configured, try to determine from environment
                if (string.IsNullOrEmpty(mediaBaseUrl))
                {
                    var urls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000";
                    var firstUrl = urls.Split(';').First();
                    
                    // Replace wildcard bindings with localhost for media URLs
                    if (firstUrl.Contains("+:") || firstUrl.Contains("*:"))
                    {
                        var port = firstUrl.Split(':').Last();
                        mediaBaseUrl = $"http://localhost:{port}";
                    }
                    else
                    {
                        mediaBaseUrl = firstUrl;
                    }
                }
                
                logger.LogInformation("Media storage base URL configured as: {BaseUrl}", mediaBaseUrl);
                return new InMemoryMediaStorageService(logger, mediaBaseUrl);
            });
        }

        // Register Media Lifecycle Management Services
        builder.Services.Configure<ConduitLLM.Core.Services.MediaManagementOptions>(
            builder.Configuration.GetSection("ConduitLLM:MediaManagement"));

        builder.Services.AddScoped<ConduitLLM.Core.Interfaces.IMediaLifecycleService, ConduitLLM.Core.Services.MediaLifecycleService>();

        // Add media maintenance background service
        builder.Services.AddHostedService<ConduitLLM.Http.Services.MediaMaintenanceBackgroundService>();
    }
}