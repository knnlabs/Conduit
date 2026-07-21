using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Models.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    public partial class ModelController
    {
        /// <summary>
        /// Gets model identifiers for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of model identifiers showing which providers offer this model</returns>
        [HttpGet("{id}/identifiers")]
        [ProducesResponseType(typeof(IEnumerable<ModelIdentifierDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetModelIdentifiers(int id)
        {
            var model = await _modelRepository.GetByIdWithDetailsAsync(id);
            if (model == null)
            {
                return this.NotFoundEntity("Model", id);
            }

            var identifiers = model.Identifiers.Select(i => new ModelIdentifierDto
            {
                Id = i.Id,
                Identifier = i.Identifier,
                Provider = (int?)i.Provider,
                IsPrimary = i.IsPrimary,
                MaxInputTokens = i.MaxInputTokens,
                MaxOutputTokens = i.MaxOutputTokens,
                SpeedScore = i.SpeedScore,
                QualityScore = i.QualityScore,
                ProviderVariation = i.ProviderVariation,
                ModelCostId = i.ModelCostId
            }).ToList();

            return Ok(identifiers);
        }

        /// <summary>
        /// Gets model associations with available providers
        /// Returns only associations where matching providers are configured
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of associations with their available providers</returns>
        [HttpGet("{id}/available-providers")]
        [ProducesResponseType(typeof(IEnumerable<ModelProviderAvailabilityDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAvailableProviders(int id)
        {
            var model = await _modelRepository.GetByIdWithDetailsAsync(id);
            if (model == null)
            {
                return this.NotFoundEntity("Model", id);
            }

            var providers = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _providerRepository.GetPaginatedAsync);
            var enabledProviders = providers.Where(p => p.IsEnabled).ToList();

            var result = new List<ModelProviderAvailabilityDto>();

            foreach (var association in model.Identifiers)
            {
                // Skip associations without a provider type - they're not properly configured
                if (association.Provider == null)
                {
                    Logger.LogWarning(
                        "ModelIdentifier {AssociationId} for model {ModelId} has null Provider field - skipping",
                        association.Id, id);
                    continue;
                }

                // Find matching providers for this association
                var matchingProviders = enabledProviders.Where(p =>
                    p.ProviderType == association.Provider
                ).ToList();

                if (matchingProviders.Any())
                {
                    result.Add(new ModelProviderAvailabilityDto
                    {
                        AssociationId = association.Id,
                        Identifier = association.Identifier,
                        Provider = (int?)association.Provider,
                        ProviderVariation = association.ProviderVariation,
                        MaxInputTokens = association.MaxInputTokens,
                        MaxOutputTokens = association.MaxOutputTokens,
                        SpeedScore = association.SpeedScore,
                        QualityScore = association.QualityScore,
                        IsPrimary = association.IsPrimary,
                        AvailableProviders = matchingProviders.Select(p => new AvailableProviderDto
                        {
                            ProviderId = p.Id,
                            ProviderName = p.ProviderName,
                            ProviderType = p.ProviderType.ToString()
                        }).ToList()
                    });
                }
            }

            return Ok(result);
        }

        /// <summary>
        /// Creates a new model identifier for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="dto">The identifier data</param>
        /// <returns>The created identifier</returns>
        [HttpPost("{id}/identifiers")]
        [ProducesResponseType(typeof(CreatedModelIdentifierDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateModelIdentifier(int id, [FromBody] CreateModelIdentifierDto dto)
        {
            var model = await _modelRepository.GetByIdWithDetailsAsync(id);
            if (model == null)
            {
                return NotFound($"Model with ID {id} not found");
            }

            // Parse provider if provided as integer
            ProviderType? providerType = dto.Provider.HasValue ? (ProviderType)dto.Provider.Value : null;

            // Check if identifier already exists for this provider
            var existing = model.Identifiers.FirstOrDefault(i =>
                i.Identifier == dto.Identifier &&
                i.Provider == providerType);

            if (existing != null)
            {
                return Conflict($"Identifier '{dto.Identifier}' already exists for provider '{dto.Provider}'");
            }

            var identifier = new ModelProviderTypeAssociation
            {
                ModelId = id,
                Identifier = dto.Identifier,
                Provider = providerType,
                IsPrimary = dto.IsPrimary ?? false,
                Metadata = dto.Metadata,
                MaxInputTokens = dto.MaxInputTokens,
                MaxOutputTokens = dto.MaxOutputTokens,
                SpeedScore = dto.SpeedScore,
                QualityScore = dto.QualityScore,
                ProviderVariation = dto.ProviderVariation
            };

            model.Identifiers.Add(identifier);
            await _modelRepository.UpdateModelAsync(model);

            LogAdminAudit("Created", "ModelIdentifier", identifier.Id,
                $"ModelId: {id}, Identifier: {LoggingSanitizer.S(dto.Identifier)}");

            return CreatedAtAction(nameof(GetModelIdentifiers), new { id }, new CreatedModelIdentifierDto
            {
                Id = identifier.Id,
                Identifier = identifier.Identifier,
                Provider = (int?)identifier.Provider,
                IsPrimary = identifier.IsPrimary,
                MaxInputTokens = identifier.MaxInputTokens,
                MaxOutputTokens = identifier.MaxOutputTokens,
                SpeedScore = identifier.SpeedScore,
                QualityScore = identifier.QualityScore,
                ProviderVariation = identifier.ProviderVariation
            });
        }

        /// <summary>
        /// Updates a model identifier
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="identifierId">The identifier ID</param>
        /// <param name="dto">The updated identifier data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}/identifiers/{identifierId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateModelIdentifier(int id, int identifierId, [FromBody] UpdateModelIdentifierDto dto)
        {
            var model = await _modelRepository.GetByIdWithDetailsAsync(id);
            if (model == null)
            {
                return NotFound($"Model with ID {id} not found");
            }

            var identifier = model.Identifiers.FirstOrDefault(i => i.Id == identifierId);
            if (identifier == null)
            {
                return NotFound($"Identifier with ID {identifierId} not found for model {id}");
            }

            // Parse provider if provided as integer
            ProviderType? providerType = dto.Provider.HasValue ? (ProviderType)dto.Provider.Value : null;

            // Check if the new identifier/provider combo already exists (if changed)
            if (identifier.Identifier != dto.Identifier || identifier.Provider != providerType)
            {
                var existing = model.Identifiers.FirstOrDefault(i =>
                    i.Id != identifierId &&
                    i.Identifier == dto.Identifier &&
                    i.Provider == providerType);

                if (existing != null)
                {
                    return Conflict($"Identifier '{dto.Identifier}' already exists for provider '{dto.Provider}'");
                }
            }

            identifier.Identifier = dto.Identifier;
            identifier.Provider = providerType;
            identifier.IsPrimary = dto.IsPrimary ?? identifier.IsPrimary;
            identifier.Metadata = dto.Metadata;
            identifier.MaxInputTokens = dto.MaxInputTokens;
            identifier.MaxOutputTokens = dto.MaxOutputTokens;
            identifier.SpeedScore = dto.SpeedScore;
            identifier.QualityScore = dto.QualityScore;
            identifier.ProviderVariation = dto.ProviderVariation;

            await _modelRepository.UpdateModelAsync(model);

            LogAdminAudit("Updated", "ModelIdentifier", identifierId,
                $"ModelId: {id}, Identifier: {LoggingSanitizer.S(dto.Identifier)}");

            return NoContent();
        }

        /// <summary>
        /// Deletes a model identifier
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="identifierId">The identifier ID to delete</param>
        /// <returns>No content on success</returns>
        [HttpDelete("{id}/identifiers/{identifierId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteModelIdentifier(int id, int identifierId)
        {
            // Directly delete the identifier from the repository
            var deleted = await _modelRepository.DeleteIdentifierAsync(id, identifierId);

            if (!deleted)
            {
                throw new KeyNotFoundException($"Identifier with ID {identifierId} not found for model {id}");
            }

            LogAdminAudit("Deleted", "ModelIdentifier", identifierId, $"ModelId: {id}");

            return NoContent();
        }
    }
}
