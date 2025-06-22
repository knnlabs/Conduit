using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Services.Adapters
{
    /// <summary>
    /// Adapter that bridges the model provider mapping service interface with the Admin API client
    /// </summary>
    public class ModelProviderMappingServiceAdapter : IModelProviderMappingService
    {
        private readonly IAdminApiClient _adminApiClient;
        private readonly ILogger<ModelProviderMappingServiceAdapter> _logger;

        public ModelProviderMappingServiceAdapter(IAdminApiClient adminApiClient, ILogger<ModelProviderMappingServiceAdapter> logger)
        {
            _adminApiClient = adminApiClient ?? throw new ArgumentNullException(nameof(adminApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model provider mappings
        /// </summary>
        /// <returns>Collection of model provider mappings</returns>
        public async Task<IEnumerable<ModelProviderMappingDto>> GetAllAsync()
        {
            try
            {
                // Use dynamic to access methods that might not be in the interface
                dynamic dynamicClient = _adminApiClient;
                var mappings = await dynamicClient.GetAllModelProviderMappingsAsync();
                return mappings ?? Enumerable.Empty<ModelProviderMappingDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model provider mappings");
                throw;
            }
        }

        /// <summary>
        /// Gets a model provider mapping by ID
        /// </summary>
        /// <param name="id">The mapping ID</param>
        /// <returns>The model provider mapping or null if not found</returns>
        public async Task<ModelProviderMappingDto?> GetByIdAsync(int id)
        {
            try
            {
                dynamic dynamicClient = _adminApiClient;
                return await dynamicClient.GetModelProviderMappingByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mapping by ID {Id}", id);
                return null;
            }
        }

        /// <summary>
        /// Gets model provider mappings by model ID
        /// </summary>
        /// <param name="modelId">The model ID</param>
        /// <returns>The model provider mapping for the model</returns>
        public async Task<ModelProviderMappingDto?> GetByModelIdAsync(string modelId)
        {
            try
            {
                dynamic dynamicClient = _adminApiClient;
                return await dynamicClient.GetModelProviderMappingByAliasAsync(modelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model provider mappings by model ID {ModelId}", modelId);
                return null;
            }
        }

        /// <summary>
        /// Creates a new model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to create</param>
        /// <returns>The created mapping or null if creation failed</returns>
        public async Task<ModelProviderMappingDto?> CreateAsync(ModelProviderMappingDto mapping)
        {
            try
            {
                if (mapping == null)
                {
                    _logger.LogWarning("Attempted to create null model provider mapping");
                    return null;
                }

                // Convert the DTO to entity for the API
                var entity = new ModelProviderMapping
                {
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    IsEnabled = mapping.IsEnabled,
                    MaxContextTokens = mapping.MaxContextLength
                };

                dynamic dynamicClient = _adminApiClient;
                var success = await dynamicClient.CreateModelProviderMappingAsync(entity);
                if (!success)
                {
                    return null;
                }

                // Retrieve the created mapping
                return await dynamicClient.GetModelProviderMappingByAliasAsync(mapping.ModelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model provider mapping");
                return null;
            }
        }

        /// <summary>
        /// Updates a model provider mapping
        /// </summary>
        /// <param name="mapping">The mapping to update</param>
        /// <returns>The updated mapping or null if update failed</returns>
        public async Task<ModelProviderMappingDto?> UpdateAsync(ModelProviderMappingDto mapping)
        {
            try
            {
                if (mapping == null || mapping.Id == 0)
                {
                    _logger.LogWarning("Attempted to update invalid model provider mapping");
                    return null;
                }

                // Convert the DTO to entity for the API
                var entity = new ModelProviderMapping
                {
                    Id = mapping.Id,
                    ModelAlias = mapping.ModelId,
                    ProviderModelName = mapping.ProviderModelId,
                    ProviderCredentialId = int.Parse(mapping.ProviderId),
                    IsEnabled = mapping.IsEnabled,
                    MaxContextTokens = mapping.MaxContextLength
                };

                dynamic dynamicClient = _adminApiClient;
                var success = await dynamicClient.UpdateModelProviderMappingAsync(mapping.Id, entity);
                if (!success)
                {
                    return null;
                }

                return mapping;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model provider mapping");
                return null;
            }
        }

        /// <summary>
        /// Deletes a model provider mapping
        /// </summary>
        /// <param name="id">The mapping ID</param>
        /// <returns>True if deleted successfully</returns>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                dynamic dynamicClient = _adminApiClient;
                return await dynamicClient.DeleteModelProviderMappingAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model provider mapping {Id}", id);
                return false;
            }
        }

        /// <summary>
        /// Gets available providers
        /// </summary>
        /// <returns>Collection of provider data</returns>
        public async Task<IEnumerable<ProviderDataDto>> GetProvidersAsync()
        {
            try
            {
                // Get provider credentials to see available providers
                var credentials = await _adminApiClient.GetAllProviderCredentialsAsync();
                if (credentials == null) return Enumerable.Empty<ProviderDataDto>();

                return credentials.Select(c => new ProviderDataDto
                {
                    Id = c.Id,
                    ProviderName = c.ProviderName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting providers");
                return Enumerable.Empty<ProviderDataDto>();
            }
        }

        /// <summary>
        /// Creates multiple model provider mappings in a single operation
        /// </summary>
        /// <param name="request">The bulk mapping request containing mappings to create</param>
        /// <returns>The bulk mapping response with results and errors</returns>
        public async Task<BulkModelMappingResponse> CreateBulkAsync(BulkModelMappingRequest request)
        {
            try
            {
                // Check if AdminApiClient implements bulk operations
                if (_adminApiClient is IModelProviderMappingService mappingService)
                {
                    return await mappingService.CreateBulkAsync(request);
                }

                // Fallback: implement bulk operations by calling individual creates
                var response = new BulkModelMappingResponse
                {
                    TotalProcessed = request.Mappings.Count
                };

                for (int i = 0; i < request.Mappings.Count; i++)
                {
                    var mappingRequest = request.Mappings[i];
                    try
                    {
                        // Check for existing mapping
                        var existingMapping = await GetByModelIdAsync(mappingRequest.ModelId);
                        if (existingMapping != null)
                        {
                            if (request.ReplaceExisting)
                            {
                                // Update existing mapping
                                existingMapping.ProviderModelId = mappingRequest.ProviderModelId;
                                existingMapping.ProviderId = mappingRequest.ProviderId;
                                existingMapping.Priority = mappingRequest.Priority;
                                existingMapping.IsEnabled = mappingRequest.IsEnabled;
                                existingMapping.SupportsVision = mappingRequest.SupportsVision;
                                existingMapping.SupportsImageGeneration = mappingRequest.SupportsImageGeneration;
                                existingMapping.SupportsAudioTranscription = mappingRequest.SupportsAudioTranscription;
                                existingMapping.SupportsTextToSpeech = mappingRequest.SupportsTextToSpeech;
                                existingMapping.SupportsRealtimeAudio = mappingRequest.SupportsRealtimeAudio;

                                var updatedMapping = await UpdateAsync(existingMapping);
                                if (updatedMapping != null)
                                {
                                    response.Updated.Add(updatedMapping);
                                }
                                else
                                {
                                    response.Failed.Add(new BulkMappingError
                                    {
                                        Index = i,
                                        Mapping = mappingRequest,
                                        ErrorMessage = "Failed to update existing mapping",
                                        ErrorType = BulkMappingErrorType.SystemError
                                    });
                                }
                            }
                            else
                            {
                                response.Failed.Add(new BulkMappingError
                                {
                                    Index = i,
                                    Mapping = mappingRequest,
                                    ErrorMessage = $"Model ID already exists: {mappingRequest.ModelId}",
                                    ErrorType = BulkMappingErrorType.Duplicate
                                });
                            }
                            continue;
                        }

                        // Create new mapping
                        var newMappingDto = new ModelProviderMappingDto
                        {
                            ModelId = mappingRequest.ModelId,
                            ProviderModelId = mappingRequest.ProviderModelId,
                            ProviderId = mappingRequest.ProviderId,
                            Priority = mappingRequest.Priority,
                            IsEnabled = mappingRequest.IsEnabled,
                            SupportsVision = mappingRequest.SupportsVision,
                            SupportsImageGeneration = mappingRequest.SupportsImageGeneration,
                            SupportsAudioTranscription = mappingRequest.SupportsAudioTranscription,
                            SupportsTextToSpeech = mappingRequest.SupportsTextToSpeech,
                            SupportsRealtimeAudio = mappingRequest.SupportsRealtimeAudio,
                            Capabilities = mappingRequest.Capabilities,
                            MaxContextLength = mappingRequest.MaxContextLength,
                            TokenizerType = mappingRequest.TokenizerType,
                            SupportedVoices = mappingRequest.SupportedVoices,
                            SupportedLanguages = mappingRequest.SupportedLanguages,
                            SupportedFormats = mappingRequest.SupportedFormats,
                            IsDefault = mappingRequest.IsDefault,
                            DefaultCapabilityType = mappingRequest.DefaultCapabilityType,
                            Notes = mappingRequest.Notes
                        };

                        var createdMapping = await CreateAsync(newMappingDto);
                        if (createdMapping != null)
                        {
                            response.Created.Add(createdMapping);
                        }
                        else
                        {
                            response.Failed.Add(new BulkMappingError
                            {
                                Index = i,
                                Mapping = mappingRequest,
                                ErrorMessage = "Failed to create mapping",
                                ErrorType = BulkMappingErrorType.SystemError
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing bulk mapping at index {Index}", i);
                        response.Failed.Add(new BulkMappingError
                        {
                            Index = i,
                            Mapping = mappingRequest,
                            ErrorMessage = $"System error: {ex.Message}",
                            ErrorType = BulkMappingErrorType.SystemError
                        });
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk mapping operation");
                
                // Return complete failure response
                return new BulkModelMappingResponse
                {
                    TotalProcessed = request.Mappings.Count,
                    Failed = request.Mappings.Select((mapping, index) => new BulkMappingError
                    {
                        Index = index,
                        Mapping = mapping,
                        ErrorMessage = $"System error: {ex.Message}",
                        ErrorType = BulkMappingErrorType.SystemError
                    }).ToList()
                };
            }
        }
    }
}