using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing ModelSeries entities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    public class ModelSeriesController : AdminControllerBase
    {
        private readonly IModelSeriesRepository _repository;

        /// <summary>
        /// Initializes a new instance of the ModelSeriesController
        /// </summary>
        public ModelSeriesController(
            IModelSeriesRepository repository,
            ILogger<ModelSeriesController> logger)
            : base(logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        /// <summary>
        /// Gets all model series
        /// </summary>
        /// <returns>List of all model series</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ModelSeriesDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<IActionResult> GetAll()
        {
            return ExecuteAsync(
                async () =>
                {
                    var series = await _repository.GetAllWithAuthorAsync();
                    return series.Select(s => s.ToDto());
                },
                Ok,
                "GetAll");
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
        public Task<IActionResult> GetById(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _repository.GetByIdWithAuthorAsync(id),
                series => Ok(series.ToDto()),
                "Model series",
                id,
                "GetById");
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
        public Task<IActionResult> GetModelsInSeries(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _repository.GetModelsInSeriesAsync(id),
                models =>
                {
                    var dtos = models.Select(m => new SeriesSimpleModelDto
                    {
                        Id = m.Id,
                        Name = m.Name,
                        Version = m.Version,
                        IsActive = m.IsActive
                    });

                    return Ok(dtos);
                },
                "Model series",
                id,
                "GetModelsInSeries");
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
        public Task<IActionResult> Create([FromBody] CreateModelSeriesDto dto)
        {
            return ExecuteAsync(
                async () =>
                {
                    // Check if series with same name and author already exists
                    var existing = await _repository.GetByNameAndAuthorAsync(dto.Name, dto.AuthorId);
                    if (existing != null)
                    {
                        throw new InvalidOperationException($"A model series with name '{dto.Name}' already exists for this author");
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
                    var reloaded = await _repository.GetByIdWithAuthorAsync(series.Id);
                    if (reloaded == null)
                    {
                        throw new InvalidOperationException("Failed to reload created series");
                    }

                    LogAdminAudit("Created", "ModelSeries", reloaded.Id, $"Name: {LoggingSanitizer.S(reloaded.Name)}");
                    return reloaded;
                },
                series => CreatedAtAction(
                    nameof(GetById),
                    new { id = series.Id },
                    series.ToDto()),
                "Create");
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
        public Task<IActionResult> Update(int id, [FromBody] UpdateModelSeriesDto dto)
        {
            if (id != dto.Id)
            {
                return Task.FromResult<IActionResult>(BadRequest("ID mismatch"));
            }

            return ExecuteAsync(
                async () =>
                {
                    var series = await _repository.GetByIdAsync(id);
                    if (series == null)
                    {
                        throw new KeyNotFoundException($"Model series with ID {id} not found");
                    }

                    // Check for name conflicts if name is being changed
                    if (!string.IsNullOrEmpty(dto.Name) && dto.Name != series.Name)
                    {
                        var existing = await _repository.GetByNameAndAuthorAsync(dto.Name, series.AuthorId);
                        if (existing != null && existing.Id != id)
                        {
                            throw new InvalidOperationException($"A model series with name '{dto.Name}' already exists for this author");
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
                    LogAdminAudit("Updated", "ModelSeries", id);
                },
                NoContent(),
                "Update",
                new { Id = id });
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
        public Task<IActionResult> Delete(int id)
        {
            return ExecuteAsync(
                async () =>
                {
                    var series = await _repository.GetByIdAsync(id);
                    if (series == null)
                    {
                        throw new KeyNotFoundException($"Model series with ID {id} not found");
                    }

                    // Check if series has models
                    var models = await _repository.GetModelsInSeriesAsync(id);
                    if (models != null && models.Any())
                    {
                        throw new InvalidOperationException($"Cannot delete model series with {models.Count()} associated models. Delete the models first.");
                    }

                    await _repository.DeleteAsync(id);
                    LogAdminAudit("Deleted", "ModelSeries", id);
                },
                NoContent(),
                "Delete",
                new { Id = id });
        }

    }
}
