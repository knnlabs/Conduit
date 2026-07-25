using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Tests.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Tests.Configuration.Repositories;

/// <summary>
/// Regression tests for issue #1192: POST /v1/admin/models cascaded a blank
/// ModelSeries + ModelAuthor insert on every call. The phantom navigation
/// initializers (<c>Series = new ModelSeries()</c> / <c>Author = new ModelAuthor()</c>)
/// were attached as Added by the graph-traversing <c>DbSet.Add()</c>, repointing the
/// new model away from the caller-supplied ModelSeriesId and — once a blank-name
/// author row existed — failing every later create on IX_ModelAuthor_Name_Unique.
/// Runs against SQLite (real relational constraints) via <see cref="RepositoryTestBase"/>.
/// </summary>
[Collection("RepositoryTests")]
public class ModelRepositoryCreateTests : RepositoryTestBase
{
    private readonly ModelRepository _repository;

    public ModelRepositoryCreateTests()
    {
        _repository = new ModelRepository(
            CreateDbContextFactory(),
            new LoggerFactory().CreateLogger<ModelRepository>());
    }

    private (int AuthorId, int SeriesId) SeedSeries()
    {
        using var context = CreateContext();

        var author = new ModelAuthor { Name = "Test Author" };
        var series = new ModelSeries { Author = author, Name = "Test Series", Parameters = "{}" };
        context.AddRange(author, series);
        context.SaveChanges();

        return (author.Id, series.Id);
    }

    [Fact]
    public async Task CreateModelAsync_WithSeriesIdOnly_UsesCallerSuppliedSeries()
    {
        var (_, seriesId) = SeedSeries();

        var created = await _repository.CreateModelAsync(new Model
        {
            Name = "test-model-x",
            ModelSeriesId = seriesId
        });

        using var context = CreateContext();
        var persisted = await context.Models.SingleAsync(m => m.Id == created.Id);
        Assert.Equal(seriesId, persisted.ModelSeriesId);
    }

    [Fact]
    public async Task CreateModelAsync_WithSeriesIdOnly_DoesNotInsertBlankSeriesOrAuthor()
    {
        var (_, seriesId) = SeedSeries();

        await _repository.CreateModelAsync(new Model
        {
            Name = "test-model-x",
            ModelSeriesId = seriesId
        });

        using var context = CreateContext();
        Assert.Equal(1, await context.ModelSeries.CountAsync());
        Assert.Equal(1, await context.ModelAuthors.CountAsync());
    }

    [Fact]
    public async Task CreateModelAsync_ConsecutiveCreates_BothSucceed()
    {
        // With the bug present, each create tried to insert another blank-name author,
        // so the second call failed on IX_ModelAuthor_Name_Unique (the live 500).
        var (_, seriesId) = SeedSeries();

        await _repository.CreateModelAsync(new Model { Name = "test-model-1", ModelSeriesId = seriesId });
        await _repository.CreateModelAsync(new Model { Name = "test-model-2", ModelSeriesId = seriesId });

        using var context = CreateContext();
        Assert.Equal(2, await context.Models.CountAsync());
        Assert.Equal(1, await context.ModelAuthors.CountAsync());
    }
}
