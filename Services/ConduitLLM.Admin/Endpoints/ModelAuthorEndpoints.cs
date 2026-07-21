using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Extensions;
using ConduitLLM.Admin.Models.ModelAuthors;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Extensions;

namespace ConduitLLM.Admin.Endpoints
{
    /// <summary>
    /// Minimal-API endpoints for managing <c>ModelAuthor</c> entities — the Tier 3 pilot (#906),
    /// replacing the MVC <c>ModelAuthorController</c>.
    /// </summary>
    /// <remarks>
    /// Behavior is identical to the controller: thrown exceptions propagate to the global
    /// <c>AdminExceptionMiddleware</c> (same standardized responses); success logging via
    /// <see cref="OperationLoggingEndpointFilter"/>; audit logging via <see cref="AdminAudit"/>.
    /// </remarks>
    public static class ModelAuthorEndpoints
    {
        /// <summary>Maps the ModelAuthor endpoint group.</summary>
        public static IEndpointRouteBuilder MapModelAuthorEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/ModelAuthor")
                .RequireAuthorization("MasterKeyPolicy")
                .AddEndpointFilter<OperationLoggingEndpointFilter>()
                .WithTags("ModelAuthor");

            group.MapGet("/", GetAll)
                .WithName("ModelAuthors_List")
                .Produces<IEnumerable<ModelAuthorDto>>(StatusCodes.Status200OK);
            group.MapGet("/{id:int}", GetById).WithName("GetModelAuthorById")
                .Produces<ModelAuthorDto>(StatusCodes.Status200OK)
                .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
            group.MapGet("/{id:int}/series", GetSeriesByAuthor)
                .WithName("ModelAuthors_ListSeries")
                .Produces<IEnumerable<SimpleModelSeriesDto>>(StatusCodes.Status200OK)
                .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
            group.MapPost("/", Create)
                .WithName("ModelAuthors_Create")
                .Produces<ModelAuthorDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest);
            group.MapPut("/{id:int}", Update)
                .WithName("ModelAuthors_Update")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            group.MapDelete("/{id:int}", Delete)
                .WithName("ModelAuthors_Delete")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            return app;
        }

        private static async Task<IResult> GetAll(IModelAuthorRepository repository)
        {
            var authors = await RepositoryPaginationExtensions.GetAllViaPaginationAsync(
                repository.GetPaginatedAsync);
            return Results.Ok(authors.Select(a => a.ToDto()));
        }

        private static async Task<IResult> GetById(int id, IModelAuthorRepository repository)
        {
            var author = await repository.GetByIdAsync(id);
            return author is null
                ? NotFoundEntity("Model author", id)
                : Results.Ok(author.ToDto());
        }

        private static async Task<IResult> GetSeriesByAuthor(int id, IModelAuthorRepository repository)
        {
            var series = await repository.GetSeriesByAuthorAsync(id);
            if (series is null)
            {
                return NotFoundEntity("Model author", id);
            }

            var dtos = series.Select(s => new SimpleModelSeriesDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                TokenizerType = s.TokenizerType
            });
            return Results.Ok(dtos);
        }

        private static async Task<IResult> Create(
            CreateModelAuthorDto dto,
            IModelAuthorRepository repository,
            HttpContext httpContext,
            ILoggerFactory loggerFactory)
        {
            var existing = await repository.GetByNameAsync(dto.Name);
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

            await repository.CreateAsync(author);
            AdminAudit.Log(httpContext, Logger(loggerFactory), "Created", "ModelAuthor", author.Id,
                $"Name: {LoggingSanitizer.S(author.Name)}");

            return Results.Created($"/api/ModelAuthor/{author.Id}", author.ToDto());
        }

        private static async Task<IResult> Update(
            int id,
            UpdateModelAuthorDto dto,
            IModelAuthorRepository repository,
            HttpContext httpContext,
            ILoggerFactory loggerFactory)
        {
            if (id != dto.Id)
            {
                return Results.BadRequest("ID mismatch");
            }

            var author = await repository.GetByIdAsync(id);
            if (author == null)
            {
                throw new KeyNotFoundException($"Model author with ID {id} not found");
            }

            // Check for name conflicts if the name is being changed
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != author.Name)
            {
                var existing = await repository.GetByNameAsync(dto.Name);
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

            await repository.UpdateAsync(author);
            AdminAudit.Log(httpContext, Logger(loggerFactory), "Updated", "ModelAuthor", id,
                $"Name: {LoggingSanitizer.S(author.Name)}");

            return Results.NoContent();
        }

        private static async Task<IResult> Delete(
            int id,
            IModelAuthorRepository repository,
            HttpContext httpContext,
            ILoggerFactory loggerFactory)
        {
            var author = await repository.GetByIdAsync(id);
            if (author == null)
            {
                throw new KeyNotFoundException($"Model author with ID {id} not found");
            }

            var series = await repository.GetSeriesByAuthorAsync(id);
            if (series != null && series.Any())
            {
                throw new InvalidOperationException(
                    $"Cannot delete model author with {series.Count()} associated series. Delete the series first.");
            }

            await repository.DeleteAsync(id);
            AdminAudit.Log(httpContext, Logger(loggerFactory), "Deleted", "ModelAuthor", id,
                $"Name: {LoggingSanitizer.S(author.Name)}");

            return Results.NoContent();
        }

        private static IResult NotFoundEntity(string entityType, object? entityId)
        {
            var message = entityId != null
                ? $"{entityType} with ID '{entityId}' not found"
                : $"{entityType} not found";
            return Results.NotFound(new ErrorResponseDto(message) { Code = "not_found" });
        }

        private static ILogger Logger(ILoggerFactory factory)
            => factory.CreateLogger("ConduitLLM.Admin.Endpoints.ModelAuthor");
    }
}
