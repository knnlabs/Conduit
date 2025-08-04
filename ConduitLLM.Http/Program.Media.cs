using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Services;

public partial class Program
{
    public static void ConfigureMediaServices(WebApplicationBuilder builder)
    {
        // Register Media Storage Service
        // Check both configuration key and environment variable
        Console.WriteLine("[Conduit] ConfigureMediaServices - Starting media configuration");
        
        var configProvider = builder.Configuration.GetValue<string>("ConduitLLM:Storage:Provider");
        var configEnvVar = builder.Configuration.GetValue<string>("CONDUIT_MEDIA_STORAGE_TYPE");
        var directEnvVar = Environment.GetEnvironmentVariable("CONDUIT_MEDIA_STORAGE_TYPE");
        
        Console.WriteLine($"[Conduit] Storage detection - Config: {configProvider}, ConfigEnv: {configEnvVar}, DirectEnv: {directEnvVar}");
        
        var storageProvider = configProvider ?? configEnvVar ?? directEnvVar ?? "InMemory";
        Console.WriteLine($"[Conduit] Storage Provider Selected: {storageProvider}");
        if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[Conduit] Configuring S3 Media Storage Service");
            
            // Configure S3StorageOptions with environment variable mapping
            builder.Services.Configure<ConduitLLM.Core.Options.S3StorageOptions>(options =>
            {
                // First try to bind from the configuration section
                builder.Configuration.GetSection(ConduitLLM.Core.Options.S3StorageOptions.SectionName).Bind(options);
                
                // Then override with environment variables if they exist
                var endpoint = builder.Configuration["CONDUIT_S3_ENDPOINT"] ?? Environment.GetEnvironmentVariable("CONDUIT_S3_ENDPOINT");
                if (!string.IsNullOrEmpty(endpoint))
                {
                    options.ServiceUrl = endpoint;
                    Console.WriteLine($"[Conduit] S3 Endpoint: {endpoint}");
                }
                
                var accessKey = builder.Configuration["CONDUIT_S3_ACCESS_KEY_ID"] 
                    ?? builder.Configuration["CONDUIT_S3_ACCESS_KEY"] 
                    ?? Environment.GetEnvironmentVariable("CONDUIT_S3_ACCESS_KEY_ID")
                    ?? Environment.GetEnvironmentVariable("CONDUIT_S3_ACCESS_KEY");
                if (!string.IsNullOrEmpty(accessKey))
                {
                    options.AccessKey = accessKey;
                    Console.WriteLine($"[Conduit] S3 Access Key: {accessKey.Substring(0, Math.Min(4, accessKey.Length))}...");
                }
                
                var secretKey = builder.Configuration["CONDUIT_S3_SECRET_ACCESS_KEY"] 
                    ?? builder.Configuration["CONDUIT_S3_SECRET_KEY"]
                    ?? Environment.GetEnvironmentVariable("CONDUIT_S3_SECRET_ACCESS_KEY")
                    ?? Environment.GetEnvironmentVariable("CONDUIT_S3_SECRET_KEY");
                if (!string.IsNullOrEmpty(secretKey))
                {
                    options.SecretKey = secretKey;
                    Console.WriteLine("[Conduit] S3 Secret Key: ****");
                }
                
                var bucketName = builder.Configuration["CONDUIT_S3_BUCKET_NAME"] ?? Environment.GetEnvironmentVariable("CONDUIT_S3_BUCKET_NAME");
                if (!string.IsNullOrEmpty(bucketName))
                {
                    options.BucketName = bucketName;
                    Console.WriteLine($"[Conduit] S3 Bucket: {bucketName}");
                }
                
                var region = builder.Configuration["CONDUIT_S3_REGION"] ?? Environment.GetEnvironmentVariable("CONDUIT_S3_REGION");
                if (!string.IsNullOrEmpty(region))
                {
                    options.Region = region;
                    Console.WriteLine($"[Conduit] S3 Region: {region}");
                }
                
                var publicBaseUrl = builder.Configuration["CONDUIT_S3_PUBLIC_BASE_URL"] ?? Environment.GetEnvironmentVariable("CONDUIT_S3_PUBLIC_BASE_URL");
                if (!string.IsNullOrEmpty(publicBaseUrl))
                {
                    options.PublicBaseUrl = publicBaseUrl;
                    Console.WriteLine($"[Conduit] S3 Public Base URL: {publicBaseUrl}");
                }
                
                // Set defaults for MinIO compatibility
                options.ForcePathStyle = true;
                options.AutoCreateBucket = true;
                
                // Validate required fields
                if (string.IsNullOrEmpty(options.ServiceUrl))
                {
                    throw new InvalidOperationException("S3 ServiceUrl is required. Set CONDUIT_S3_ENDPOINT environment variable.");
                }
                if (string.IsNullOrEmpty(options.AccessKey))
                {
                    throw new InvalidOperationException("S3 AccessKey is required. Set CONDUIT_S3_ACCESS_KEY or CONDUIT_S3_ACCESS_KEY_ID environment variable.");
                }
                if (string.IsNullOrEmpty(options.SecretKey))
                {
                    throw new InvalidOperationException("S3 SecretKey is required. Set CONDUIT_S3_SECRET_KEY or CONDUIT_S3_SECRET_ACCESS_KEY environment variable.");
                }
                if (string.IsNullOrEmpty(options.BucketName))
                {
                    throw new InvalidOperationException("S3 BucketName is required. Set CONDUIT_S3_BUCKET_NAME environment variable.");
                }
            });
            
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