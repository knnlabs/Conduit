using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Models.ModelCapabilities;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing ModelCapabilities entities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelCapabilitiesController : ControllerBase
    {
        private readonly IModelCapabilitiesRepository _repository;
        private readonly ILogger<ModelCapabilitiesController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCapabilitiesController
        /// </summary>
        public ModelCapabilitiesController(
            IModelCapabilitiesRepository repository,
            ILogger<ModelCapabilitiesController> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model capabilities
        /// </summary>
        /// <returns>List of all model capabilities</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CapabilitiesDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var capabilities = await _repository.GetAllAsync();
                var dtos = capabilities.Select(c => MapToDto(c));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model capabilities");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving model capabilities");
            }
        }

        /// <summary>
        /// Gets a specific model capabilities by ID
        /// </summary>
        /// <param name="id">The capabilities ID</param>
        /// <returns>The model capabilities</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CapabilitiesDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var capabilities = await _repository.GetByIdAsync(id);
                if (capabilities == null)
                {
                    return NotFound($"Model capabilities with ID {id} not found");
                }

                return Ok(MapToDto(capabilities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model capabilities with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model capabilities");
            }
        }

        /// <summary>
        /// Gets models using specific capabilities
        /// </summary>
        /// <param name="id">The capabilities ID</param>
        /// <returns>List of models using the capabilities</returns>
        [HttpGet("{id}/models")]
        [ProducesResponseType(typeof(IEnumerable<CapabilitiesSimpleModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelsUsingCapabilities(int id)
        {
            try
            {
                var models = await _repository.GetModelsUsingCapabilitiesAsync(id);
                if (models == null)
                {
                    return NotFound($"Model capabilities with ID {id} not found");
                }

                var dtos = models.Select(m => new CapabilitiesSimpleModelDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    IsActive = m.IsActive
                });

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models using capabilities {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving models");
            }
        }

        /// <summary>
        /// Creates a new model capabilities
        /// </summary>
        /// <param name="dto">The model capabilities to create</param>
        /// <returns>The created model capabilities</returns>
        [HttpPost]
        [ProducesResponseType(typeof(CapabilitiesDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] CreateCapabilitiesDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var capabilities = new ModelCapabilities
                {
                    MaxTokens = dto.MaxTokens,
                    MinTokens = dto.MinTokens,
                    SupportsVision = dto.SupportsVision,
                    SupportsAudioTranscription = dto.SupportsAudioTranscription,
                    SupportsTextToSpeech = dto.SupportsTextToSpeech,
                    SupportsRealtimeAudio = dto.SupportsRealtimeAudio,
                    SupportsImageGeneration = dto.SupportsImageGeneration,
                    SupportsVideoGeneration = dto.SupportsVideoGeneration,
                    SupportsEmbeddings = dto.SupportsEmbeddings,
                    SupportsChat = dto.SupportsChat,
                    SupportsFunctionCalling = dto.SupportsFunctionCalling,
                    SupportsStreaming = dto.SupportsStreaming,
                    TokenizerType = dto.TokenizerType,
                    SupportedVoices = dto.SupportedVoices,
                    SupportedLanguages = dto.SupportedLanguages,
                    SupportedFormats = dto.SupportedFormats
                };

                await _repository.CreateAsync(capabilities);
                
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = capabilities.Id },
                    MapToDto(capabilities));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model capabilities");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model capabilities");
            }
        }

        /// <summary>
        /// Updates an existing model capabilities
        /// </summary>
        /// <param name="id">The capabilities ID</param>
        /// <param name="dto">The updated model capabilities data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCapabilitiesDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (id != dto.Id)
                {
                    return BadRequest("ID mismatch");
                }

                var capabilities = await _repository.GetByIdAsync(id);
                if (capabilities == null)
                {
                    return NotFound($"Model capabilities with ID {id} not found");
                }

                // Update fields
                if (dto.MaxTokens.HasValue)
                    capabilities.MaxTokens = dto.MaxTokens.Value;
                if (dto.MinTokens.HasValue)
                    capabilities.MinTokens = dto.MinTokens.Value;
                if (dto.SupportsVision.HasValue)
                    capabilities.SupportsVision = dto.SupportsVision.Value;
                if (dto.SupportsAudioTranscription.HasValue)
                    capabilities.SupportsAudioTranscription = dto.SupportsAudioTranscription.Value;
                if (dto.SupportsTextToSpeech.HasValue)
                    capabilities.SupportsTextToSpeech = dto.SupportsTextToSpeech.Value;
                if (dto.SupportsRealtimeAudio.HasValue)
                    capabilities.SupportsRealtimeAudio = dto.SupportsRealtimeAudio.Value;
                if (dto.SupportsImageGeneration.HasValue)
                    capabilities.SupportsImageGeneration = dto.SupportsImageGeneration.Value;
                if (dto.SupportsVideoGeneration.HasValue)
                    capabilities.SupportsVideoGeneration = dto.SupportsVideoGeneration.Value;
                if (dto.SupportsEmbeddings.HasValue)
                    capabilities.SupportsEmbeddings = dto.SupportsEmbeddings.Value;
                if (dto.SupportsChat.HasValue)
                    capabilities.SupportsChat = dto.SupportsChat.Value;
                if (dto.SupportsFunctionCalling.HasValue)
                    capabilities.SupportsFunctionCalling = dto.SupportsFunctionCalling.Value;
                if (dto.SupportsStreaming.HasValue)
                    capabilities.SupportsStreaming = dto.SupportsStreaming.Value;
                if (dto.TokenizerType.HasValue)
                    capabilities.TokenizerType = dto.TokenizerType.Value;
                if (dto.SupportedVoices != null)
                    capabilities.SupportedVoices = dto.SupportedVoices;
                if (dto.SupportedLanguages != null)
                    capabilities.SupportedLanguages = dto.SupportedLanguages;
                if (dto.SupportedFormats != null)
                    capabilities.SupportedFormats = dto.SupportedFormats;

                await _repository.UpdateAsync(capabilities);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model capabilities with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the model capabilities");
            }
        }

        /// <summary>
        /// Deletes a model capabilities
        /// </summary>
        /// <param name="id">The capabilities ID</param>
        /// <returns>No content on success</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var capabilities = await _repository.GetByIdAsync(id);
                if (capabilities == null)
                {
                    return NotFound($"Model capabilities with ID {id} not found");
                }

                // Check if capabilities is used by models
                var models = await _repository.GetModelsUsingCapabilitiesAsync(id);
                if (models != null && models.Any())
                {
                    return Conflict($"Cannot delete model capabilities used by {models.Count()} models. Update the models first.");
                }

                await _repository.DeleteAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model capabilities with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the model capabilities");
            }
        }

        private static CapabilitiesDto MapToDto(ModelCapabilities capabilities)
        {
            return new CapabilitiesDto
            {
                Id = capabilities.Id,
                MaxTokens = capabilities.MaxTokens,
                MinTokens = capabilities.MinTokens,
                SupportsVision = capabilities.SupportsVision,
                SupportsAudioTranscription = capabilities.SupportsAudioTranscription,
                SupportsTextToSpeech = capabilities.SupportsTextToSpeech,
                SupportsRealtimeAudio = capabilities.SupportsRealtimeAudio,
                SupportsImageGeneration = capabilities.SupportsImageGeneration,
                SupportsVideoGeneration = capabilities.SupportsVideoGeneration,
                SupportsEmbeddings = capabilities.SupportsEmbeddings,
                SupportsChat = capabilities.SupportsChat,
                SupportsFunctionCalling = capabilities.SupportsFunctionCalling,
                SupportsStreaming = capabilities.SupportsStreaming,
                TokenizerType = capabilities.TokenizerType,
                SupportedVoices = capabilities.SupportedVoices,
                SupportedLanguages = capabilities.SupportedLanguages,
                SupportedFormats = capabilities.SupportedFormats
            };
        }
    }

}
