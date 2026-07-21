using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints
{
    public partial class ModelEndpoints
    {
        /// <summary>
        /// Gets all provider mappings for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of provider mappings for the model</returns>
        public async Task<IResult> GetModelProviderMappings(int id)
        {
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return AdminResults.NotFoundEntity("Model", id);
            }

            // Get all mappings for this model
            var mappings = await _mappingService.GetMappingsByModelIdAsync(id);
            var dtos = mappings.Select(m => m.ToDto());

            return Ok(dtos);
        }

        /// <summary>
        /// Creates a new provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingDto">The provider mapping to create</param>
        /// <returns>The created provider mapping</returns>
        public async Task<IResult> CreateModelProviderMapping(int id, ModelProviderMappingDto mappingDto)
        {
            // Skip ModelId validation since it's no longer on the DTO
            // The ModelProviderTypeAssociationId provides the model relationship

            // Check if model exists
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return NotFound($"Model with ID {id} not found");
            }

            // Check for duplicate mapping
            var existingMappings = await _mappingService.GetMappingsByModelIdAsync(id);
            if (existingMappings.Any(m => m.ProviderId == mappingDto.ProviderId))
            {
                return Conflict($"A mapping for model ID {id} with provider ID {mappingDto.ProviderId} already exists");
            }

            // Create the mapping
            var mapping = mappingDto.ToEntity();
            var success = await _mappingService.AddMappingAsync(mapping);

            if (!success)
            {
                return BadRequest("Failed to create provider mapping");
            }

            // Get the created mapping
            var createdMappings = await _mappingService.GetMappingsByModelIdAsync(id);
            var createdMapping = createdMappings.FirstOrDefault(m => m.ProviderId == mappingDto.ProviderId);

            LogAdminAudit("Created", "ModelProviderMapping", createdMapping?.Id,
                $"ModelId: {id}, ProviderId: {mappingDto.ProviderId}");

            return Results.Created($"/api/Model/{id}/provider-mappings", createdMapping?.ToDto());
        }

        /// <summary>
        /// Updates a provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingId">The mapping ID</param>
        /// <param name="mappingDto">The updated provider mapping data</param>
        /// <returns>No content on success</returns>
        public async Task<IResult> UpdateModelProviderMapping(int id, int mappingId, ModelProviderMappingDto mappingDto)
        {
            if (mappingDto.Id != mappingId)
            {
                return BadRequest("Mapping ID in URL does not match Mapping ID in request body");
            }

            // Skip ModelId validation since it's no longer on the DTO
            // The ModelProviderTypeAssociationId provides the model relationship

            // Check if model exists
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return NotFound($"Model with ID {id} not found");
            }

            // Get and update the mapping
            var existingMapping = await _mappingService.GetMappingByIdAsync(mappingId);
            if (existingMapping == null)
            {
                return NotFound($"Provider mapping with ID {mappingId} not found");
            }

            if (existingMapping.ModelProviderTypeAssociation?.ModelId != id)
            {
                return BadRequest($"Mapping with ID {mappingId} does not belong to model with ID {id}");
            }

            existingMapping.UpdateFromDto(mappingDto);
            var success = await _mappingService.UpdateMappingAsync(existingMapping);

            if (!success)
            {
                return BadRequest("Failed to update provider mapping");
            }

            LogAdminAudit("Updated", "ModelProviderMapping", mappingId, $"ModelId: {id}");

            return NoContent();
        }

        /// <summary>
        /// Deletes a provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingId">The mapping ID to delete</param>
        /// <returns>No content on success</returns>
        public async Task<IResult> DeleteModelProviderMapping(int id, int mappingId)
        {
            // Check if model exists
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return NotFound($"Model with ID {id} not found");
            }

            // Check if mapping exists and belongs to this model
            var existingMapping = await _mappingService.GetMappingByIdAsync(mappingId);
            if (existingMapping == null)
            {
                return NotFound($"Provider mapping with ID {mappingId} not found");
            }

            if (existingMapping.ModelProviderTypeAssociation?.ModelId != id)
            {
                return BadRequest($"Mapping with ID {mappingId} does not belong to model with ID {id}");
            }

            var success = await _mappingService.DeleteMappingAsync(mappingId);

            if (!success)
            {
                return BadRequest("Failed to delete provider mapping");
            }

            LogAdminAudit("Deleted", "ModelProviderMapping", mappingId, $"ModelId: {id}");

            return NoContent();
        }
    }
}
