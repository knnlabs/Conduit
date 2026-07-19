using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Utilities;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Providers.Extensions;

namespace ConduitLLM.Admin;

public partial class Program
{
    /// <summary>
    /// Configures core application services: DI registrations, Redis, SignalR, distributed cache.
    /// </summary>
    private static void ConfigureCoreServices(WebApplicationBuilder builder, ILogger startupLogger)
    {
        // Add leader election service for distributed background service coordination
        builder.Services.AddLeaderElection();
        startupLogger.LogInformation("Leader election service configured for background service coordination");

        // Add Core services
        builder.Services.AddCoreServices(builder.Configuration, startupLogger);

        // Add Configuration services
        builder.Services.AddConfigurationServices(builder.Configuration);

        // Add Provider services (needed for ILLMClientFactory)
        builder.Services.AddProviderServices();

        // Add Admin services
        builder.Services.AddAdminServices(builder.Configuration);

        // Configure Data Protection with Redis persistence
        var redisConnectionString = RedisUrlParser.ResolveConnectionString();
        builder.Services.AddRedisDataProtection(redisConnectionString, "Conduit");

        // Add Redis as distributed cache for ephemeral key storage
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "conduit:";
            });
            startupLogger.LogInformation("Distributed cache configured with Redis");
        }
        else
        {
            // Fallback to in-memory cache if Redis is not configured
            builder.Services.AddDistributedMemoryCache();
            startupLogger.LogWarning("Using in-memory cache — ephemeral keys will not work across instances");
        }

        // Add SignalR with shared configuration (MessagePack, Redis backplane)
        var signalRRedisConnectionString = builder.Configuration.GetConnectionString("RedisSignalR") ?? redisConnectionString;
        builder.Services.AddConduitSignalR(
            builder.Environment,
            signalRRedisConnectionString,
            redisChannelPrefix: "conduit_admin_signalr:",
            redisDatabase: 3,
            serviceName: "ConduitLLM.Admin");

        // Add media lifecycle services (scheduler, storage, distributed locking)
        builder.Services.AddMediaLifecycleServices(builder.Configuration);
    }
}
