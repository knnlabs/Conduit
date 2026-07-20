using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Tests.TestInfrastructure;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using System.Text.Json;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Regression tests for issue #977: GET /api/modelcosts/{id} followed by PUT with the
    /// unmodified response body returned 500 (DbUpdateException).
    /// <para>
    /// Root cause: <see cref="ModelCostRepository"/> loads ModelCost with the
    /// ModelProviderTypeAssociations → Model graph included, and <c>Model.Series</c> is
    /// initialized to a phantom <c>new ModelSeries()</c> (Id = 0) whose <c>Author</c> is a
    /// phantom <c>new ModelAuthor()</c> (Id = 0). The graph-traversing <c>DbSet.Update()</c>
    /// in the repository base attached those phantom entities as Added, inserting empty
    /// ModelSeries/ModelAuthor rows and — once an empty-named author existed — failing every
    /// subsequent save with a unique-constraint violation (IX_ModelAuthor_Name_Unique).
    /// </para>
    /// These tests run against SQLite (real relational constraints) via
    /// <see cref="RepositoryTestBase"/> so the failure shape matches production PostgreSQL.
    /// </summary>
    public class AdminModelCostServiceRoundTripTests : RepositoryTestBase
    {
        private readonly Mock<IEventBus> _mockEventBus;
        private readonly AdminModelCostService _service;

        public AdminModelCostServiceRoundTripTests()
        {
            var factory = CreateDbContextFactory();
            var costRepository = new ModelCostRepository(factory, Mock.Of<ILogger<ModelCostRepository>>());
            _mockEventBus = new Mock<IEventBus>();

            _service = new AdminModelCostService(
                costRepository,
                Mock.Of<IRequestLogRepository>(),
                factory,
                Mock.Of<ILogger<AdminModelCostService>>(),
                _mockEventBus.Object);
        }

        #region Setup helpers

        private sealed record SeededGraph(int CostId, int ModelId, int SeriesId, int AuthorId, int AssociationId);

        /// <summary>
        /// Seeds a ModelCost linked (via ModelIdentifiers.ModelCostId) to a model alias,
        /// mirroring the live "mock-image" repro from the #929 parity gate.
        /// </summary>
        private SeededGraph SeedCostWithAssociatedModel()
        {
            using var context = CreateContext();

            var author = new ModelAuthor { Name = "MockCo" };
            var series = new ModelSeries { Author = author, Name = "Mock Series", Parameters = "{}" };
            var model = new Model { Name = "mock-image", Series = series };
            var cost = new ModelCost
            {
                CostName = "mock-image-cost",
                ModelType = "image",
                PricingModel = PricingModel.Standard,
                PricingConfiguration = "{\"baseRate\":0.1}",
                InputCostPerMillionTokens = 1.5m,
                OutputCostPerMillionTokens = 2.5m,
                IsActive = true
            };
            var association = new ModelProviderTypeAssociation
            {
                Model = model,
                Identifier = "mock-image",
                Provider = ProviderType.OpenAI,
                ModelCost = cost,
                IsEnabled = true
            };

            context.AddRange(author, series, model, cost, association);
            context.SaveChanges();

            return new SeededGraph(cost.Id, model.Id, series.Id, author.Id, association.Id);
        }

        /// <summary>
        /// Builds the UpdateModelCostDto through the same JSON contract as an unmodified
        /// GET→PUT round-trip. The GET body carries associatedModelAliases (strings), not
        /// association IDs, so ModelProviderTypeAssociationIds stays null when the payload is
        /// deserialized as an update request.
        /// </summary>
        private static UpdateModelCostDto ToRoundTripUpdateDto(ModelCostDto dto)
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var payload = JsonSerializer.Serialize(dto, options);
            var update = JsonSerializer.Deserialize<UpdateModelCostDto>(payload, options);

            update.Should().NotBeNull();
            update!.ModelProviderTypeAssociationIds.Should().BeNull(
                "associatedModelAliases is read-only response data and must not be mapped as association IDs");
            return update;
        }

        #endregion

        [Fact]
        public async Task UpdateModelCostAsync_RepeatedGetPutRoundTripWithAssociatedModel_Succeeds()
        {
            // Arrange — an empty-named ModelAuthor mirrors the corrupted live state left behind
            // by an earlier buggy save (the phantom graph attach inserted Name = "" authors).
            // With the bug present, the graph attach tries to insert another "" author and the
            // save fails with a unique-constraint DbUpdateException — the live 500 on PUT.
            using (var context = CreateContext())
            {
                context.ModelAuthors.Add(new ModelAuthor { Name = "" });
                context.SaveChanges();
            }
            var seeded = SeedCostWithAssociatedModel();

            // Act — repeated unmodified GET→PUT round-trips must keep succeeding
            for (var i = 0; i < 2; i++)
            {
                var getDto = await _service.GetModelCostByIdAsync(seeded.CostId);
                getDto.Should().NotBeNull();

                var result = await _service.UpdateModelCostAsync(ToRoundTripUpdateDto(getDto!));

                // Assert
                result.Should().BeTrue();
            }
        }

        [Fact]
        public async Task UpdateModelCostAsync_RoundTripWithAssociatedModel_DoesNotInsertPhantomSeriesOrAuthor()
        {
            // Arrange
            var seeded = SeedCostWithAssociatedModel();
            var getDto = await _service.GetModelCostByIdAsync(seeded.CostId);

            // Act
            var result = await _service.UpdateModelCostAsync(ToRoundTripUpdateDto(getDto!));

            // Assert
            result.Should().BeTrue();
            using var context = CreateContext();
            context.ModelAuthors.Count().Should().Be(1, "the update must not insert phantom ModelAuthor rows");
            context.ModelSeries.Count().Should().Be(1, "the update must not insert phantom ModelSeries rows");
            var model = await context.Models.AsNoTracking().SingleAsync(m => m.Id == seeded.ModelId);
            model.ModelSeriesId.Should().Be(seeded.SeriesId, "the update must not re-point the model at a phantom series");
        }

        [Fact]
        public async Task UpdateModelCostAsync_AssociationIdsAbsentFromDto_PreservesAssociatedModelAliases()
        {
            // Arrange
            var seeded = SeedCostWithAssociatedModel();
            var getDto = await _service.GetModelCostByIdAsync(seeded.CostId);
            getDto!.AssociatedModelAliases.Should().BeEquivalentTo(new[] { "mock-image" });

            // Act
            await _service.UpdateModelCostAsync(ToRoundTripUpdateDto(getDto));

            // Assert — an unmodified round-trip must not silently drop the cost→model links
            var afterDto = await _service.GetModelCostByIdAsync(seeded.CostId);
            afterDto!.AssociatedModelAliases.Should().BeEquivalentTo(new[] { "mock-image" });
        }

        [Fact]
        public async Task UpdateModelCostAsync_ChangedUpdatableFields_PersistsAllFields()
        {
            // Arrange
            var seeded = SeedCostWithAssociatedModel();
            var getDto = await _service.GetModelCostByIdAsync(seeded.CostId);
            var updateDto = ToRoundTripUpdateDto(getDto!);
            updateDto.PricingConfiguration = "{\"baseRate\":0.25}";
            updateDto.ModelType = "video";
            updateDto.Description = "updated description";
            updateDto.Priority = 7;
            updateDto.IsActive = false;
            updateDto.InputCostPerMillionTokens = 9.75m;

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert
            result.Should().BeTrue();
            var afterDto = await _service.GetModelCostByIdAsync(seeded.CostId);
            afterDto!.PricingConfiguration.Should().Be("{\"baseRate\":0.25}");
            afterDto.ModelType.Should().Be("video");
            afterDto.Description.Should().Be("updated description");
            afterDto.Priority.Should().Be(7);
            afterDto.IsActive.Should().BeFalse();
            afterDto.InputCostPerMillionTokens.Should().Be(9.75m);
        }

        [Fact]
        public async Task UpdateModelCostAsync_OnlyPricingConfigurationChanged_PublishesModelCostChangedEvent()
        {
            // Arrange
            var seeded = SeedCostWithAssociatedModel();
            var getDto = await _service.GetModelCostByIdAsync(seeded.CostId);
            var updateDto = ToRoundTripUpdateDto(getDto!);
            updateDto.PricingConfiguration = "{\"baseRate\":0.5}";

            // Act
            await _service.UpdateModelCostAsync(updateDto);

            // Assert — cache invalidation depends on this event
            _mockEventBus.Verify(
                x => x.PublishAsync(
                    It.Is<ModelCostChanged>(e =>
                        e.ModelCostId == seeded.CostId &&
                        e.ChangeType == "Updated" &&
                        e.ChangedProperties.Contains("PricingConfiguration")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UpdateModelCostAsync_EmptyAssociationIdsProvided_ClearsAssociationsAndPublishesEvent()
        {
            // Arrange
            var seeded = SeedCostWithAssociatedModel();
            var getDto = await _service.GetModelCostByIdAsync(seeded.CostId);
            var updateDto = ToRoundTripUpdateDto(getDto!);
            updateDto.ModelProviderTypeAssociationIds = new List<int>();

            // Act
            var result = await _service.UpdateModelCostAsync(updateDto);

            // Assert — explicit empty list still means "clear all associations"
            result.Should().BeTrue();
            using (var context = CreateContext())
            {
                var association = await context.ModelProviderTypeAssociations
                    .AsNoTracking()
                    .SingleAsync(a => a.Id == seeded.AssociationId);
                association.ModelCostId.Should().BeNull();
            }

            _mockEventBus.Verify(
                x => x.PublishAsync(
                    It.Is<ModelCostChanged>(e =>
                        e.ModelCostId == seeded.CostId &&
                        e.ChangeType == "Updated"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
