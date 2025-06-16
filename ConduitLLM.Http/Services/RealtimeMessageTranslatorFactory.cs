using System;
using System.Collections.Concurrent;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Factory for creating real-time message translators.
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

            // Register default translators
            RegisterDefaultTranslators();
        }

        public IRealtimeMessageTranslator? GetTranslator(string provider)
        {
            if (_translators.TryGetValue(provider.ToLowerInvariant(), out var translator))
            {
                return translator;
            }

            // Try to resolve from DI container
            var translatorType = provider.ToLowerInvariant() switch
            {
                "openai" => typeof(Providers.Translators.OpenAIRealtimeTranslatorV2),
                // Add other providers as they're implemented
                // "ultravox" => typeof(Providers.Translators.UltravoxRealtimeTranslator),
                // "elevenlabs" => typeof(Providers.Translators.ElevenLabsRealtimeTranslator),
                _ => null
            };

            if (translatorType != null)
            {
                try
                {
                    var instance = ActivatorUtilities.CreateInstance(_serviceProvider, translatorType) as IRealtimeMessageTranslator;
                    if (instance != null)
                    {
                        RegisterTranslator(provider, instance);
                        return instance;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create translator for provider {Provider}", provider);
                }
            }

            _logger.LogWarning("No translator found for provider: {Provider}", provider);
            return null;
        }

        public void RegisterTranslator(string provider, IRealtimeMessageTranslator translator)
        {
            _translators[provider.ToLowerInvariant()] = translator;
            _logger.LogInformation("Registered translator for provider: {Provider}", provider);
        }

        public bool HasTranslator(string provider)
        {
            return _translators.ContainsKey(provider.ToLowerInvariant());
        }

        public string[] GetRegisteredProviders()
        {
            return _translators.Keys.ToArray();
        }

        private void RegisterDefaultTranslators()
        {
            // OpenAI translator will be created on demand
            // Add any translators that should be pre-registered here
        }
    }
}
