using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Filters;
using ConduitLLM.Admin.Models.ModelAuthors;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing ModelAuthor entities
    /// </summary>
    /// <remarks>
    /// Error handling is delegated to the global <c>AdminExceptionMiddleware</c> (thrown exceptions
    /// are mapped to standardized responses via <c>ExceptionToResponseMapper</c>), and success
    /// logging is provided by <see cref="OperationLoggingFilter"/>. This keeps actions free of the
    /// per-action <c>ExecuteAsync</c> wrapper boilerplate.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class ModelAuthorController : AdminControllerBase
    {
        private readonly IModelAuthorRepository _repository;

        /// <summary>
        /// Initializes a new instance of the ModelAuthorController
        /// </summary>
        public ModelAuthorController(
            IModelAuthorRepository repository,
            ILogger<ModelAuthorController> logger)
            : base(logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
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
            var authors = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                _repository.GetPaginatedAsync);
            return Ok(authors.Select(a => a.ToDto()));
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
            var author = await _repository.GetByIdAsync(id);
            if (author == null)
            {
                return this.NotFoundEntity("Model author", id);
            }

            return Ok(author.ToDto());
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
            var series = await _repository.GetSeriesByAuthorAsync(id);
            if (series == null)
            {
                return this.NotFoundEntity("Model author", id);
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
            // Check if author with same name already exists
            var existing = await _repository.GetByNameAsync(dto.Name);
            if (existing != null)
            {
                throw new InvalidOperationException($"A model author with name '{dto.Name}' already exists");
            }

            var author = new ModelAuthor
            {
                Name = dto.Name,
                Description = dto.Description,
                WebsiteUrl = dto.WebsiteUrl
            };

            await _repository.CreateAsync(author);
            LogAdminAudit("Created", "ModelAuthor", author.Id, $"Name: {LoggingSanitizer.S(author.Name)}");

            return CreatedAtAction(
                nameof(GetById),
                new { id = author.Id },
                author.ToDto());
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
            if (id != dto.Id)
            {
                return BadRequest("ID mismatch");
            }

            var author = await _repository.GetByIdAsync(id);
            if (author == null)
            {
                throw new KeyNotFoundException($"Model author with ID {id} not found");
            }

            // Check for name conflicts if name is being changed
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != author.Name)
            {
                var existing = await _repository.GetByNameAsync(dto.Name);
                if (existing != null && existing.Id != id)
                {
                    throw new InvalidOperationException($"A model author with name '{dto.Name}' already exists");
                }
                author.Name = dto.Name;
            }

            if (dto.Description != null)
                author.Description = dto.Description;
            if (dto.WebsiteUrl != null)
                author.WebsiteUrl = dto.WebsiteUrl;

            await _repository.UpdateAsync(author);
            LogAdminAudit("Updated", "ModelAuthor", id, $"Name: {LoggingSanitizer.S(author.Name)}");

            return NoContent();
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
            var author = await _repository.GetByIdAsync(id);
            if (author == null)
            {
                throw new KeyNotFoundException($"Model author with ID {id} not found");
            }

            // Check if author has series
            var series = await _repository.GetSeriesByAuthorAsync(id);
            if (series != null && series.Any())
            {
                throw new InvalidOperationException($"Cannot delete model author with {series.Count()} associated series. Delete the series first.");
            }

            await _repository.DeleteAsync(id);
            LogAdminAudit("Deleted", "ModelAuthor", id, $"Name: {LoggingSanitizer.S(author.Name)}");

            return NoContent();
        }

    }
}
