using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Models;
using ConduitLLM.Functions.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConduitLLM.Tests.Functions.Services;

public class FunctionExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCostCalculationFailsAfterProviderCompletes_PreservesEstimatedCost()
    {
        var configurationRepository = new Mock<IFunctionConfigurationRepository>();
        var credentialRepository = new Mock<IFunctionCredentialRepository>();
        var executionRepository = new Mock<IFunctionExecutionRepository>();
        var costCalculationService = new Mock<IFunctionCostCalculationService>();
        var clientFactory = new Mock<IFunctionClientFactory>();
        var client = new Mock<IFunctionClient>();

        configurationRepository
            .Setup(repository => repository.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FunctionConfiguration
            {
                Id = 7,
                ConfigurationName = "test search",
                ProviderType = FunctionProviderType.Exa,
                Purpose = FunctionPurpose.Search,
                IsEnabled = true
            });
        credentialRepository
            .Setup(repository => repository.GetByProviderTypeAsync(
                FunctionProviderType.Exa,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FunctionCredential
                {
                    ProviderType = FunctionProviderType.Exa,
                    ApiKey = "test-key",
                    IsEnabled = true
                }
            ]);
        executionRepository
            .Setup(repository => repository.CreateAsync(
                It.IsAny<FunctionExecution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FunctionExecution execution, CancellationToken _) => execution.Id);
        executionRepository
            .Setup(repository => repository.UpdateAsync(
                It.IsAny<FunctionExecution>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        costCalculationService
            .Setup(service => service.EstimateCostAsync(
                7,
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.25m);
        costCalculationService
            .Setup(service => service.CalculateCostAsync(
                7,
                It.IsAny<FunctionExecutionUsage>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cost database unavailable"));
        client
            .Setup(provider => provider.ExecuteAsync(
                It.IsAny<Dictionary<string, object>>(),
                "test-key",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FunctionExecutionResult
            {
                IsSuccess = true,
                ResponseJson = "{\"results\":[]}",
                HttpStatusCode = 200
            });
        client
            .Setup(provider => provider.CalculateUsageFromResponse(
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<FunctionExecutionResult>()))
            .Returns(new FunctionExecutionUsage { ResultCount = 1 });
        clientFactory
            .Setup(factory => factory.GetClientAsync(FunctionProviderType.Exa, 7))
            .ReturnsAsync(client.Object);

        var service = new FunctionExecutionService(
            configurationRepository.Object,
            credentialRepository.Object,
            executionRepository.Object,
            costCalculationService.Object,
            clientFactory.Object,
            Mock.Of<ILogger<FunctionExecutionService>>());

        var execution = await service.ExecuteAsync(7, 11, new Dictionary<string, object>());

        Assert.Equal(ExecutionState.Failed, execution.State);
        Assert.Equal(0.25m, execution.ActualCost);
        Assert.Equal("Cost database unavailable", execution.ErrorMessage);
        Assert.Equal("{\"results\":[]}", execution.ResponseJson);
        executionRepository.Verify(repository => repository.UpdateAsync(
            It.Is<FunctionExecution>(saved =>
                saved.State == ExecutionState.Failed &&
                saved.ActualCost == 0.25m),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
