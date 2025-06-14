using System;
using System.Collections.Concurrent;
using System.Linq;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Providers.Translators
{
    /// <summary>
    /// Factory for creating and managing real-time message translators.
    /// </summary>
    public class RealtimeMessageTranslatorFactory : IRealtimeMessageTranslatorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RealtimeMessageTranslatorFactory> _logger;
        private readonly ConcurrentDictionary<string, IRealtimeMessageTranslator> _translators = new();

        public RealtimeMessageTranslatorFactory(
            IServiceProvider serviceProvider,
            ILogger<RealtimeMessageTranslatorFactory> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IRealtimeMessageTranslator? GetTranslator(string provider)
        {
            if (string.IsNullOrEmpty(provider))
            {
                _logger.LogWarning("Provider name is null or empty");
                return null;
            }

            // Normalize provider name
            var normalizedProvider = provider.ToLowerInvariant();

            return _translators.GetOrAdd(normalizedProvider, key =>
            {
                IRealtimeMessageTranslator? translator = key switch
                {
                    "openai" => CreateTranslator<OpenAIRealtimeTranslatorV2>(),
                    "ultravox" => CreateTranslator<UltravoxRealtimeTranslator>(),
                    "elevenlabs" => CreateTranslator<ElevenLabsRealtimeTranslator>(),
                    _ => null
                };

                if (translator == null)
                {
                    _logger.LogWarning("No translator found for provider: {Provider}", provider);
                }
                else
                {
                    _logger.LogInformation("Created translator for provider: {Provider}", provider);
                }

                return translator!;
            });
        }

        public bool HasTranslator(string provider)
        {
            if (string.IsNullOrEmpty(provider))
                return false;

            var normalizedProvider = provider.ToLowerInvariant();

            return normalizedProvider switch
            {
                "openai" => true,
                "ultravox" => true,
                "elevenlabs" => true,
                _ => false
            };
        }

        public void RegisterTranslator(string provider, IRealtimeMessageTranslator translator)
        {
            if (string.IsNullOrEmpty(provider))
                throw new ArgumentException("Provider name cannot be null or empty", nameof(provider));

            if (translator == null)
                throw new ArgumentNullException(nameof(translator));

            var normalizedProvider = provider.ToLowerInvariant();
            _translators[normalizedProvider] = translator;

            _logger.LogInformation("Registered translator for provider: {Provider}", provider);
        }

        public string[] GetRegisteredProviders()
        {
            // Return built-in providers plus any dynamically registered ones
            var builtInProviders = new[] { "openai", "ultravox", "elevenlabs" };
            var registeredProviders = _translators.Keys.ToArray();

            return builtInProviders.Union(registeredProviders).Distinct().ToArray();
        }

        private T? CreateTranslator<T>() where T : class, IRealtimeMessageTranslator
        {
            try
            {
                // Try to get from DI container first
                var translator = _serviceProvider.GetService<T>();
                if (translator != null)
                    return translator;

                // Fall back to creating with logger
                var loggerType = typeof(ILogger<>).MakeGenericType(typeof(T));
                var logger = _serviceProvider.GetRequiredService(loggerType);

                return Activator.CreateInstance(typeof(T), logger) as T;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create translator of type {Type}", typeof(T).Name);
                return null;
            }
        }
    }
}
