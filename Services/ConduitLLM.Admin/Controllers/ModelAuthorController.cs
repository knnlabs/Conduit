using ConduitLLM.Admin.Models.ModelAuthors;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Controller for managing ModelAuthor entities
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "MasterKeyPolicy")]
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
        public Task<IActionResult> GetAll()
        {
            return ExecuteAsync(
                async () =>
                {
                    var authors = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                        _repository.GetPaginatedAsync);
                    return authors.Select(a => MapToDto(a));
                },
                Ok,
                "GetAll");
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
        public Task<IActionResult> GetById(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _repository.GetByIdAsync(id),
                author => Ok(MapToDto(author)),
                "Model author",
                id,
                "GetById");
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
        public Task<IActionResult> GetSeriesByAuthor(int id)
        {
            return ExecuteWithNotFoundAsync(
                () => _repository.GetSeriesByAuthorAsync(id),
                series =>
                {
                    var dtos = series.Select(s => new SimpleModelSeriesDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        TokenizerType = s.TokenizerType
                    });

                    return Ok(dtos);
                },
                "Model author",
                id,
                "GetSeriesByAuthor");
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
        public Task<IActionResult> Create([FromBody] CreateModelAuthorDto dto)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            return ExecuteAsync(
                async () =>
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

                    return author;
                },
                author => CreatedAtAction(
                    nameof(GetById),
                    new { id = author.Id },
                    MapToDto(author)),
                "Create");
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
        public Task<IActionResult> Update(int id, [FromBody] UpdateModelAuthorDto dto)
        {
            if (!ModelState.IsValid)
            {
                return Task.FromResult<IActionResult>(BadRequest(ModelState));
            }

            if (id != dto.Id)
            {
                return Task.FromResult<IActionResult>(BadRequest("ID mismatch"));
            }

            return ExecuteAsync(
                async () =>
                {
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
                },
                NoContent(),
                "Update",
                new { Id = id });
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
        public Task<IActionResult> Delete(int id)
        {
            return ExecuteAsync(
                async () =>
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
                },
                NoContent(),
                "Delete",
                new { Id = id });
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
