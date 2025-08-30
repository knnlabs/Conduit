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
            _model = new Model
            {
                Id = baseId * 10 + 3,
                Name = "GPT-4",
                Version = "v1",
                Description = "Test model",
                ModelCardUrl = "https://example.com/model",
                ModelSeriesId = _series.Id,
                Series = _series,
                MaxInputTokens = 4096,
                MaxOutputTokens = 4096,
                TokenizerType = TokenizerType.Cl100KBase,
                SupportsChat = true,
                SupportsStreaming = false,
                SupportsVision = false,
                SupportsFunctionCalling = false,
                SupportsVideoGeneration = false,
                SupportsImageGeneration = false,
                SupportsEmbeddings = false,
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
        public ModelProviderMappingBuilder WithVisionSupport(bool supports)
        {
            if (_model != null)
            {
                _model.SupportsVision = supports;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithStreamingSupport(bool supports)
        {
            if (_model != null)
            {
                _model.SupportsStreaming = supports;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithFullCapabilities()
        {
            if (_model != null)
            {
                _model.SupportsChat = true;
                _model.SupportsStreaming = true;
                _model.SupportsVision = true;
                _model.SupportsFunctionCalling = true;
                _model.SupportsVideoGeneration = true;
                _model.SupportsImageGeneration = true;
                _model.SupportsEmbeddings = true;
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
            if (_model != null)
            {
                // Split the total max tokens between input and output
                // This maintains backward compatibility where max_tokens is the sum
                _model.MaxInputTokens = maxTokens / 2;
                _model.MaxOutputTokens = maxTokens / 2;
            }
            return this;
        }

        public ModelProviderMappingBuilder WithTokenizerType(TokenizerType type)
        {
            if (_model != null)
            {
                _model.TokenizerType = type;
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
                    
                    // Copy capability properties
                    SupportsChat = _model.SupportsChat,
                    SupportsStreaming = _model.SupportsStreaming,
                    SupportsVision = _model.SupportsVision,
                    SupportsFunctionCalling = _model.SupportsFunctionCalling,
                    SupportsVideoGeneration = _model.SupportsVideoGeneration,
                    SupportsImageGeneration = _model.SupportsImageGeneration,
                    SupportsEmbeddings = _model.SupportsEmbeddings,
                    MaxInputTokens = _model.MaxInputTokens,
                    MaxOutputTokens = _model.MaxOutputTokens,
                    TokenizerType = _model.TokenizerType,
                    
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