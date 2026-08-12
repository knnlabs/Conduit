using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ConduitLLM.Configuration.Serialization;

namespace ConduitLLM.Core.Extensions
{
    /// <summary>
    /// Shared SignalR configuration used by both Gateway and Admin APIs.
    /// </summary>
    public static class SignalRConfigurationExtensions
    {
        /// <summary>
        /// Configures SignalR with standard hub options, optional MessagePack protocol,
        /// and optional Redis backplane. Both Gateway and Admin use identical settings
        /// except for the Redis channel prefix and database number.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="environment">The host environment.</param>
        /// <param name="redisConnectionString">Redis connection string (null to skip backplane).</param>
        /// <param name="redisChannelPrefix">Channel prefix for this service's SignalR Redis backplane.</param>
        /// <param name="redisDatabase">Redis database number for this service's SignalR backplane.</param>
        /// <param name="serviceName">Display name for console logging (e.g., "Conduit", "ConduitLLM.Admin").</param>
        /// <param name="configureHubOptions">Optional callback to add service-specific hub options (e.g., filters).</param>
        /// <returns>The configured SignalR server builder for further customization.</returns>
        public static ISignalRServerBuilder AddConduitSignalR(
            this IServiceCollection services,
            IHostEnvironment environment,
            string? redisConnectionString,
            string redisChannelPrefix,
            int redisDatabase,
            string serviceName,
            Action<HubOptions>? configureHubOptions = null)
        {
            var signalRBuilder = services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = environment.IsDevelopment();
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
                options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
                options.StreamBufferCapacity = 10;

                configureHubOptions?.Invoke(options);
            });

            signalRBuilder.AddJsonProtocol(options =>
            {
                var resolverChain = options.PayloadSerializerOptions.TypeInfoResolverChain;
                resolverChain.Insert(
                    0,
                    ConfigurationSignalRJsonContext.Default);
            });

            // Add MessagePack protocol support with LZ4 compression
            var messagePackEnabled = Environment.GetEnvironmentVariable("SIGNALR_MESSAGEPACK_ENABLED")?.ToLowerInvariant() != "false";
            if (messagePackEnabled)
            {
                signalRBuilder.AddMessagePackProtocol(options =>
                {
                    options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                        .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance)
                        .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                        .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                        .WithCompressionMinLength(256);
                });
            }

            // Configure SignalR Redis backplane for horizontal scaling
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix = new StackExchange.Redis.RedisChannel(redisChannelPrefix, StackExchange.Redis.RedisChannel.PatternMode.Literal);
                    options.Configuration.DefaultDatabase = redisDatabase;
                });
            }

            return signalRBuilder;
        }
    }
}
