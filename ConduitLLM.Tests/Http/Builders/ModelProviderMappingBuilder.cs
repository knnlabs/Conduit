using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Http.Builders
{
    public class ModelProviderMappingBuilder
    {
        private static int _idCounter = 0;
        private ModelProviderMapping _mapping;
        private Provider? _provider;
        private Model? _model;
        private ModelCapabilities? _capabilities;
        private ModelSeries? _series;

        public ModelProviderMappingBuilder()
        {
            var baseId = Interlocked.Increment(ref _idCounter);
            
            _series = new ModelSeries
            {
                Id = baseId * 10 + 1,
                Name = "GPT-4",
                Parameters = "{}"
            };

            _capabilities = new ModelCapabilities
            {
                Id = baseId * 10 + 2,
                MaxTokens = 4096,
                MinTokens = 1,
                TokenizerType = TokenizerType.Cl100KBase,
                SupportsChat = true,
                SupportsStreaming = false,
                SupportsVision = false,
                SupportsFunctionCalling = false,
                SupportsAudioTranscription = false,
                SupportsTextToSpeech = false,
                SupportsRealtimeAudio = false,
                SupportsVideoGeneration = false,
                SupportsImageGeneration = false,
                SupportsEmbeddings = false
            };

            _model = new Model
            {
                Id = baseId * 10 + 3,
                Name = "GPT-4",
                Version = "v1",
                Description = "Test model",
                ModelCardUrl = "https://example.com/model",
                ModelSeriesId = _series.Id,
                Series = _series,
                ModelCapabilitiesId = _capabilities.Id,
                Capabilities = _capabilities,
                IsActive = true
            };

            _provider = new Provider
            {
                Id = baseId * 10 + 4,
                ProviderName = "OpenAI",
                ProviderType = ProviderType.OpenAI,
                IsEnabled = true,
                BaseUrl = "https://api.openai.com"
            };

            _mapping = new ModelProviderMapping
            {
                Id = baseId * 10 + 5,
                ModelAlias = "test-model",
                ProviderModelId = "test-model-id",
                ProviderId = _provider.Id,
                Provider = _provider,
                IsEnabled = true,
                ModelId = _model.Id,
                Model = _model,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public ModelProviderMappingBuilder WithModelAlias(string alias)
        {
            _mapping.ModelAlias = alias;
            return this;
        }

        public ModelProviderMappingBuilder WithProviderModelId(string id)
        {
            _mapping.ProviderModelId = id;
            return this;
        }

        public ModelProviderMappingBuilder WithProviderId(int id)
        {
            _mapping.ProviderId = id;
            return this;
        }

        public ModelProviderMappingBuilder WithProvider(Provider provider)
        {
            _provider = provider;
            _mapping.Provider = provider;
            _mapping.ProviderId = provider.Id;
            return this;
        }

        public ModelProviderMappingBuilder WithMappingEnabled(bool isEnabled)
        {
            _mapping.IsEnabled = isEnabled;
            return this;
        }

        public ModelProviderMappingBuilder WithProviderEnabled(bool isEnabled)
        {
            if (_provider != null)
            {
                _provider.IsEnabled = isEnabled;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithModelId(int id)
        {
            _mapping.ModelId = id;
            if (_model != null)
            {
                _model.Id = id;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithModel(Model? model)
        {
            _model = model;
            _mapping.Model = model!;
            if (model != null)
            {
                _mapping.ModelId = model.Id;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithCapabilities(ModelCapabilities? capabilities)
        {
            _capabilities = capabilities;
            if (_model != null && capabilities != null)
            {
                _model.Capabilities = capabilities;
                _model.ModelCapabilitiesId = capabilities.Id;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithVisionSupport(bool supports)
        {
            if (_capabilities != null)
            {
                _capabilities.SupportsVision = supports;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithStreamingSupport(bool supports)
        {
            if (_capabilities != null)
            {
                _capabilities.SupportsStreaming = supports;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithFullCapabilities()
        {
            if (_capabilities != null)
            {
                _capabilities.SupportsChat = true;
                _capabilities.SupportsStreaming = true;
                _capabilities.SupportsVision = true;
                _capabilities.SupportsFunctionCalling = true;
                _capabilities.SupportsAudioTranscription = true;
                _capabilities.SupportsTextToSpeech = true;
                _capabilities.SupportsRealtimeAudio = true;
                _capabilities.SupportsVideoGeneration = true;
                _capabilities.SupportsImageGeneration = true;
                _capabilities.SupportsEmbeddings = true;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithDescription(string? description)
        {
            if (_model != null)
            {
                _model.Description = description;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithModelCardUrl(string? url)
        {
            if (_model != null)
            {
                _model.ModelCardUrl = url;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithMaxTokens(int maxTokens)
        {
            if (_capabilities != null)
            {
                _capabilities.MaxTokens = maxTokens;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithTokenizerType(TokenizerType type)
        {
            if (_capabilities != null)
            {
                _capabilities.TokenizerType = type;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithSeriesParameters(string parameters)
        {
            if (_series != null)
            {
                _series.Parameters = parameters;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithSeriesName(string name)
        {
            if (_series != null)
            {
                _series.Name = name;
            }
            return this;
        }

        public ModelProviderMapping Build()
        {
            // Return a new instance to prevent mutation
            var mapping = new ModelProviderMapping
            {
                Id = _mapping.Id,
                ModelAlias = _mapping.ModelAlias,
                ProviderModelId = _mapping.ProviderModelId,
                ProviderId = _mapping.ProviderId,
                Provider = _provider!,
                IsEnabled = _mapping.IsEnabled,
                ModelId = _mapping.ModelId,
                Model = _model!,
                CreatedAt = _mapping.CreatedAt,
                UpdatedAt = _mapping.UpdatedAt,
                MaxContextTokensOverride = _mapping.MaxContextTokensOverride,
                CapabilityOverrides = _mapping.CapabilityOverrides,
                IsDefault = _mapping.IsDefault,
                DefaultCapabilityType = _mapping.DefaultCapabilityType,
                ProviderVariation = _mapping.ProviderVariation,
                QualityScore = _mapping.QualityScore
            };

            // Ensure the Model has the correct references
            if (_model != null)
            {
                mapping.Model = new Model
                {
                    Id = _model.Id,
                    Name = _model.Name,
                    Version = _model.Version,
                    Description = _model.Description,
                    ModelCardUrl = _model.ModelCardUrl,
                    ModelSeriesId = _model.ModelSeriesId,
                    Series = _series!,
                    ModelCapabilitiesId = _model.ModelCapabilitiesId,
                    Capabilities = _capabilities!,
                    IsActive = _model.IsActive,
                    ModelParameters = _model.ModelParameters,
                    CreatedAt = _model.CreatedAt,
                    UpdatedAt = _model.UpdatedAt
                };
            }

            return mapping;
        }
    }
}