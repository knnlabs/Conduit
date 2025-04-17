using System;

using ConduitLLM.Configuration.Options;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Caching
{
    /// <summary>
    /// Extension methods for registering caching services with the DI container
    /// </summary>
    public static class CachingServiceExtensions
    {
        /// <summary>
        /// Adds caching services to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddLLMCaching(this IServiceCollection services)
        {
            // Add the cache metrics service as a singleton to track metrics across requests
            services.AddSingleton<ICacheMetricsService, CacheMetricsService>();
            
            // Register the decorator for ILLMClientFactory
            // Use a decorator pattern to wrap the existing factory
            var descriptor = new ServiceDescriptor(
                typeof(ILLMClientFactory),
                provider => 
                {
                    // Get the current implementation of ILLMClientFactory from the provider
                    var originalServices = provider.GetServices<ILLMClientFactory>();
                    ILLMClientFactory? originalFactory = null;
                    
                    // Find the non-CachingLLMClientFactory implementation
                    foreach (var service in originalServices)
                    {
                        if (service?.GetType() != typeof(CachingLLMClientFactory))
                        {
                            originalFactory = service;
                            break;
                        }
                    }
                    
                    // Fall back to a new instance if none found (shouldn't happen)
                    if (originalFactory == null)
                    {
                        throw new InvalidOperationException("No implementation of ILLMClientFactory found to wrap with caching");
                    }
                    
                    // Get the required services for the caching factory
                    var cacheService = provider.GetRequiredService<ICacheService>();
                    var metricsService = provider.GetRequiredService<ICacheMetricsService>();
                    var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>();
                    var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                    
                    // Create and return the caching factory
                    return new CachingLLMClientFactory(
                        originalFactory,
                        cacheService,
                        metricsService,
                        cacheOptions,
                        loggerFactory);
                },
                ServiceLifetime.Singleton);
            
            // Register the decorator
            services.Add(descriptor);
            
            return services;
        }
    }
    
    /// <summary>
    /// Factory that wraps LLM clients with the caching decorator
    /// </summary>
    public class CachingLLMClientFactory : ILLMClientFactory
    {
        private readonly ILLMClientFactory _innerFactory;
        private readonly ICacheService _cacheService;
        private readonly ICacheMetricsService _metricsService;
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Creates a new instance of the CachingLLMClientFactory
        /// </summary>
        public CachingLLMClientFactory(
            ILLMClientFactory innerFactory,
            ICacheService cacheService,
            ICacheMetricsService metricsService,
            IOptions<CacheOptions> cacheOptions,
            ILoggerFactory loggerFactory)
        {
            _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }
        
        /// <inheritdoc />
        public ILLMClient GetClient(string modelAlias)
        {
            // Get the original client from the inner factory
            var client = _innerFactory.GetClient(modelAlias);
            
            // Only wrap the client if caching is enabled
            if (_cacheOptions.Value.IsEnabled)
            {
                var logger = _loggerFactory.CreateLogger<CachingLLMClient>();
                
                // Wrap the client with the caching decorator
                return new CachingLLMClient(
                    client,
                    _cacheService,
                    _metricsService,
                    _cacheOptions,
                    logger);
            }
            
            // Fall back to the original client if caching is disabled
            return client;
        }
        
        /// <inheritdoc />
        public ILLMClient GetClientByProvider(string providerName)
        {
            // Get the original client from the inner factory
            var client = _innerFactory.GetClientByProvider(providerName);
            
            // Only wrap the client if caching is enabled
            if (_cacheOptions.Value.IsEnabled)
            {
                var logger = _loggerFactory.CreateLogger<CachingLLMClient>();
                
                // Wrap the client with the caching decorator
                return new CachingLLMClient(
                    client,
                    _cacheService,
                    _metricsService,
                    _cacheOptions,
                    logger);
            }
            
            // Fall back to the original client if caching is disabled
            return client;
        }
    }
}
