using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Http.Builders
{
    public class ProviderBuilder
    {
        private Provider _provider;

        public ProviderBuilder()
        {
            _provider = new Provider
            {
                Id = 1,
                ProviderName = "OpenAI",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                BaseUrl = "https://api.openai.com",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public ProviderBuilder WithProviderId(int id)
        {
            _provider.Id = id;
            return this;
        }

        public ProviderBuilder WithName(string name)
        {
            _provider.ProviderName = name;
            return this;
        }

        public ProviderBuilder WithProviderType(ProviderType type)
        {
            _provider.ProviderType = type;
            return this;
        }

        public ProviderBuilder WithEnabled(bool isEnabled)
        {
            _provider.IsEnabled = isEnabled;
            return this;
        }

        public ProviderBuilder WithBaseUrl(string baseUrl)
        {
            _provider.BaseUrl = baseUrl;
            return this;
        }

        public Provider Build()
        {
            // Return a new instance to prevent mutation
            return new Provider
            {
                Id = _provider.Id,
                ProviderName = _provider.ProviderName,
                ProviderType = _provider.ProviderType,
                IsEnabled = _provider.IsEnabled,
                BaseUrl = _provider.BaseUrl,
                CreatedAt = _provider.CreatedAt,
                UpdatedAt = _provider.UpdatedAt
            };
        }
    }
}