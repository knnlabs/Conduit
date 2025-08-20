using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Models.ModelAuthors;
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
    /// Controller for managing ModelAuthor entities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelAuthorController : ControllerBase
    {
        private readonly IModelAuthorRepository _repository;
        private readonly ILogger<ModelAuthorController> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelAuthorController
        /// </summary>
        public ModelAuthorController(
            IModelAuthorRepository repository,
            ILogger<ModelAuthorController> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets all model authors
        /// </summary>
        /// <returns>List of all model authors</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelAuthorDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var authors = await _repository.GetAllAsync();
                var dtos = authors.Select(a => MapToDto(a));
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all model authors");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving model authors");
            }
        }

        /// <summary>
        /// Gets a specific model author by ID
        /// </summary>
        /// <param name="id">The author ID</param>
        /// <returns>The model author</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ModelAuthorDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var author = await _repository.GetByIdAsync(id);
                if (author == null)
                {
                    return NotFound($"Model author with ID {id} not found");
                }

                return Ok(MapToDto(author));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model author with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the model author");
            }
        }

        /// <summary>
        /// Gets series by author
        /// </summary>
        /// <param name="id">The author ID</param>
        /// <returns>List of model series by the author</returns>
        [HttpGet("{id}/series")]
        [ProducesResponseType(typeof(IEnumerable<SimpleModelSeriesDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSeriesByAuthor(int id)
        {
            try
            {
                var series = await _repository.GetSeriesByAuthorAsync(id);
                if (series == null)
                {
                    return NotFound($"Model author with ID {id} not found");
                }

                var dtos = series.Select(s => new SimpleModelSeriesDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    TokenizerType = s.TokenizerType
                });

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting series for author {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving series");
            }
        }

        /// <summary>
        /// Creates a new model author
        /// </summary>
        /// <param name="dto">The model author to create</param>
        /// <returns>The created model author</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ModelAuthorDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] CreateModelAuthorDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Check if author with same name already exists
                var existing = await _repository.GetByNameAsync(dto.Name);
                if (existing != null)
                {
                    return Conflict($"A model author with name '{dto.Name}' already exists");
                }

                var author = new ModelAuthor
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    WebsiteUrl = dto.WebsiteUrl
                };

                await _repository.CreateAsync(author);
                
                return CreatedAtAction(
                    nameof(GetById),
                    new { id = author.Id },
                    MapToDto(author));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating model author");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the model author");
            }
        }

        /// <summary>
        /// Updates an existing model author
        /// </summary>
        /// <param name="id">The author ID</param>
        /// <param name="dto">The updated model author data</param>
        /// <returns>No content on success</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateModelAuthorDto dto)
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

                var author = await _repository.GetByIdAsync(id);
                if (author == null)
                {
                    return NotFound($"Model author with ID {id} not found");
                }

                // Check for name conflicts if name is being changed
                if (!string.IsNullOrEmpty(dto.Name) && dto.Name != author.Name)
                {
                    var existing = await _repository.GetByNameAsync(dto.Name);
                    if (existing != null && existing.Id != id)
                    {
                        return Conflict($"A model author with name '{dto.Name}' already exists");
                    }
                    author.Name = dto.Name;
                }

                if (dto.Description != null)
                    author.Description = dto.Description;
                if (dto.WebsiteUrl != null)
                    author.WebsiteUrl = dto.WebsiteUrl;

                await _repository.UpdateAsync(author);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model author with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the model author");
            }
        }

        /// <summary>
        /// Deletes a model author
        /// </summary>
        /// <param name="id">The author ID</param>
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
                var author = await _repository.GetByIdAsync(id);
                if (author == null)
                {
                    return NotFound($"Model author with ID {id} not found");
                }

                // Check if author has series
                var series = await _repository.GetSeriesByAuthorAsync(id);
                if (series != null && series.Any())
                {
                    return Conflict($"Cannot delete model author with {series.Count()} associated series. Delete the series first.");
                }

                await _repository.DeleteAsync(id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting model author with ID {Id}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the model author");
            }
        }

        private static ModelAuthorDto MapToDto(ModelAuthor author)
        {
            return new ModelAuthorDto
            {
                Id = author.Id,
                Name = author.Name,
                Description = author.Description,
                WebsiteUrl = author.WebsiteUrl
            };
        }
    }
}
