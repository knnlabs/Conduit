using ConduitLLM.Admin.Extensions;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    public partial class ModelController
    {
        /// <summary>
        /// Gets all provider mappings for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <returns>List of provider mappings for the model</returns>
        [HttpGet("{id}/provider-mappings")]
        [ProducesResponseType(typeof(IEnumerable<ModelProviderMappingDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetModelProviderMappings(int id)
        {
            var model = await _modelRepository.GetByIdAsync(id);
            if (model == null)
            {
                return this.NotFoundEntity("Model", id);
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
        [HttpPost("{id}/provider-mappings")]
        [ProducesResponseType(typeof(ModelProviderMappingDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateModelProviderMapping(int id, [FromBody] ModelProviderMappingDto mappingDto)
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

            return CreatedAtAction(
                nameof(GetModelProviderMappings),
                new { id = id },
                createdMapping?.ToDto()
            );
        }

        /// <summary>
        /// Updates a provider mapping for a specific model
        /// </summary>
        /// <param name="id">The model ID</param>
        /// <param name="mappingId">The mapping ID</param>
        /// <param name="mappingDto">The updated provider mapping data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}/provider-mappings/{mappingId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateModelProviderMapping(int id, int mappingId, [FromBody] ModelProviderMappingDto mappingDto)
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
        [HttpDelete("{id}/provider-mappings/{mappingId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteModelProviderMapping(int id, int mappingId)
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
