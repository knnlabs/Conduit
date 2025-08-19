using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    /// Controller for managing ModelSeries entities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelSeriesController : ControllerBase
    {
        private readonly IModelSeriesRepository _repository;
        private readonly ILogger<ModelSeriesController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelSeriesController
        /// </summary>
        public ModelSeriesController(
            IModelSeriesRepository repository,
            ILogger<ModelSeriesController> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model series
        /// </summary>
        /// <returns>List of all model series</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelSeriesDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var series = await _repository.GetAllWithAuthorAsync();
                var dtos = series.Select(s => MapToDto(s));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model series");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving model series");
            }
        }

        /// <summary>
        /// Gets a specific model series by ID
        /// </summary>
        /// <param name="id">The series ID</param>
        /// <returns>The model series</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ModelSeriesDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var series = await _repository.GetByIdWithAuthorAsync(id);
                if (series == null)
                {
                    return NotFound($"Model series with ID {id} not found");
                }

                return Ok(MapToDto(series));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model series with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model series");
            }
        }

        /// <summary>
        /// Gets models in a series
        /// </summary>
        /// <param name="id">The series ID</param>
        /// <returns>List of models in the series</returns>
        [HttpGet("{id}/models")]
        [ProducesResponseType(typeof(IEnumerable<SeriesSimpleModelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetModelsInSeries(int id)
        {
            try
            {
                var models = await _repository.GetModelsInSeriesAsync(id);
                if (models == null)
                {
                    return NotFound($"Model series with ID {id} not found");
                }

                var dtos = models.Select(m => new SeriesSimpleModelDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    Version = m.Version,
                    ModelType = m.ModelType,
                    IsActive = m.IsActive
                });

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models in series {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving models");
            }
        }

        /// <summary>
        /// Creates a new model series
        /// </summary>
        /// <param name="dto">The model series to create</param>
        /// <returns>The created model series</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ModelSeriesDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] CreateModelSeriesDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if series with same name and author already exists
                var existing = await _repository.GetByNameAndAuthorAsync(dto.Name, dto.AuthorId);
                if (existing != null)
                {
                    return Conflict($"A model series with name '{dto.Name}' already exists for this author");
                }

                var series = new ModelSeries
                {
                    AuthorId = dto.AuthorId,
                    Name = dto.Name,
                    Description = dto.Description,
                    TokenizerType = dto.TokenizerType,
                    Parameters = dto.Parameters ?? "{}"
                };

                await _repository.CreateAsync(series);

                // Reload with author
                series = await _repository.GetByIdWithAuthorAsync(series.Id);
                
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = series.Id },
                    MapToDto(series));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model series");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model series");
            }
        }

        /// <summary>
        /// Updates an existing model series
        /// </summary>
        /// <param name="id">The series ID</param>
        /// <param name="dto">The updated model series data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateModelSeriesDto dto)
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

                var series = await _repository.GetByIdAsync(id);
                if (series == null)
                {
                    return NotFound($"Model series with ID {id} not found");
                }

                // Check for name conflicts if name is being changed
                if (!string.IsNullOrEmpty(dto.Name) && dto.Name != series.Name)
                {
                    var existing = await _repository.GetByNameAndAuthorAsync(dto.Name, series.AuthorId);
                    if (existing != null && existing.Id != id)
                    {
                        return Conflict($"A model series with name '{dto.Name}' already exists for this author");
                    }
                    series.Name = dto.Name;
                }

                if (dto.Description != null)
                    series.Description = dto.Description;
                if (dto.TokenizerType.HasValue)
                    series.TokenizerType = dto.TokenizerType.Value;
                if (dto.Parameters != null)
                    series.Parameters = dto.Parameters;

                await _repository.UpdateAsync(series);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model series with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the model series");
            }
        }

        /// <summary>
        /// Deletes a model series
        /// </summary>
        /// <param name="id">The series ID</param>
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
                var series = await _repository.GetByIdAsync(id);
                if (series == null)
                {
                    return NotFound($"Model series with ID {id} not found");
                }

                // Check if series has models
                var models = await _repository.GetModelsInSeriesAsync(id);
                if (models != null && models.Any())
                {
                    return Conflict($"Cannot delete model series with {models.Count()} associated models. Delete the models first.");
                }

                await _repository.DeleteAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model series with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the model series");
            }
        }

        private static ModelSeriesDto MapToDto(ModelSeries series)
        {
            return new ModelSeriesDto
            {
                Id = series.Id,
                AuthorId = series.AuthorId,
                AuthorName = series.Author?.Name,
                Name = series.Name,
                Description = series.Description,
                TokenizerType = series.TokenizerType,
                Parameters = series.Parameters
            };
        }
    }

    // DTOs
    /// <summary>
    /// Model series data transfer object
    /// </summary>
    public class ModelSeriesDto
    {
        /// <summary>
        /// Series ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Author ID
        /// </summary>
        public int AuthorId { get; set; }
        
        /// <summary>
        /// Author name
        /// </summary>
        public string? AuthorName { get; set; }
        
        /// <summary>
        /// Series name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Series description
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Tokenizer type
        /// </summary>
        public TokenizerType TokenizerType { get; set; }
        
        /// <summary>
        /// UI parameters JSON
        /// </summary>
        public string Parameters { get; set; } = "{}";
    }

    /// <summary>
    /// Create model series DTO
    /// </summary>
    public class CreateModelSeriesDto
    {
        /// <summary>
        /// Author ID
        /// </summary>
        public int AuthorId { get; set; }
        
        /// <summary>
        /// Series name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Series description
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Tokenizer type
        /// </summary>
        public TokenizerType TokenizerType { get; set; }
        
        /// <summary>
        /// UI parameters JSON
        /// </summary>
        public string? Parameters { get; set; }
    }

    /// <summary>
    /// Update model series DTO
    /// </summary>
    public class UpdateModelSeriesDto
    {
        /// <summary>
        /// Series ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Series name
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Series description
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Tokenizer type
        /// </summary>
        public TokenizerType? TokenizerType { get; set; }
        
        /// <summary>
        /// UI parameters JSON
        /// </summary>
        public string? Parameters { get; set; }
    }

    /// <summary>
    /// Simple model DTO for series listing
    /// </summary>
    public class SeriesSimpleModelDto
    {
        /// <summary>
        /// Model ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Model name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Model version
        /// </summary>
        public string? Version { get; set; }
        
        /// <summary>
        /// Model type
        /// </summary>
        public ModelType ModelType { get; set; }
        
        /// <summary>
        /// Is model active
        /// </summary>
        public bool IsActive { get; set; }
    }
}