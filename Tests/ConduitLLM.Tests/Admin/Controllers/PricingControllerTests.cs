using ConduitLLM.Admin.Controllers;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Models.Pricing;
using ConduitLLM.Core.Services;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

using ValidationResult = ConduitLLM.Core.Services.ValidationResult;

namespace ConduitLLM.Tests.Admin.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public class PricingControllerTests
    {
        private readonly Mock<IPricingRulesValidator> _mockValidator;
        private readonly Mock<IPricingRulesEvaluator> _mockEvaluator;
        private readonly Mock<IPricingAuditService> _mockAuditService;
        private readonly Mock<ILogger<PricingController>> _mockLogger;
        private readonly PricingController _controller;
        private readonly ITestOutputHelper _output;

        public PricingControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockValidator = new Mock<IPricingRulesValidator>();
            _mockEvaluator = new Mock<IPricingRulesEvaluator>();
            _mockAuditService = new Mock<IPricingAuditService>();
            _mockLogger = new Mock<ILogger<PricingController>>();
            _controller = new PricingController(
                _mockValidator.Object,
                _mockEvaluator.Object,
                _mockAuditService.Object,
                _mockLogger.Object);
        }

        #region ValidatePricingConfiguration Tests

        [Fact]
        public async Task ValidatePricingConfiguration_ValidConfig_ReturnsOkAndLogsAudit()
        {
            // Arrange
            var request = new PricingValidationRequest
            {
                PricingConfiguration = "{\"pricingType\":\"per_second\",\"defaultRate\":0.025}"
            };

            _mockValidator.Setup(v => v.Validate(It.IsAny<PricingRulesConfig>(), null))
                .Returns(new ValidationResult());

            // Act
            var result = await _controller.ValidatePricingConfiguration(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<PricingValidationResponse>().Subject;
            response.IsValid.Should().BeTrue();

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Admin Audit") && o.ToString()!.Contains("Validated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ValidatePricingConfiguration_InvalidJson_ReturnsOkWithErrors()
        {
            // Arrange
            var request = new PricingValidationRequest
            {
                PricingConfiguration = "not valid json"
            };

            // Act
            var result = await _controller.ValidatePricingConfiguration(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<PricingValidationResponse>().Subject;
            response.IsValid.Should().BeFalse();
            response.Errors.Should().NotBeEmpty();
        }

        #endregion

        #region SimulatePricing Tests

        [Fact]
        public async Task SimulatePricing_ValidRequest_ReturnsOkAndLogsAudit()
        {
            // Arrange
            var request = new PricingSimulationRequest
            {
                PricingConfiguration = "{\"pricingType\":\"per_second\",\"defaultRate\":0.025}",
                VideoDurationSeconds = 10.0
            };

            _mockValidator.Setup(v => v.Validate(It.IsAny<PricingRulesConfig>(), null))
                .Returns(new ValidationResult());

            _mockEvaluator.Setup(e => e.Evaluate(
                    It.IsAny<PricingRulesConfig>(),
                    It.IsAny<Dictionary<string, object>>(),
                    It.IsAny<ConduitLLM.Core.Models.Usage>()))
                .Returns(new PricingEvaluationResult
                {
                    Cost = 0.25m,
                    Rate = 0.025m,
                    Quantity = 10m,
                    UsedDefaultRate = false
                });

            // Act
            var result = await _controller.SimulatePricing(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<PricingSimulationResponse>().Subject;
            response.CalculatedCost.Should().Be(0.25m);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Admin Audit") && o.ToString()!.Contains("Simulated")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SimulatePricing_InvalidJson_ReturnsBadRequest()
        {
            // Arrange
            var request = new PricingSimulationRequest
            {
                PricingConfiguration = "bad json"
            };

            // Act
            var result = await _controller.SimulatePricing(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region QueryPricingAuditEvents Tests

        [Fact]
        public async Task QueryPricingAuditEvents_ValidRequest_ReturnsOkAndLogsAudit()
        {
            // Arrange
            var request = new PricingAuditQueryRequest
            {
                From = DateTime.UtcNow.AddDays(-7),
                To = DateTime.UtcNow,
                PageNumber = 1,
                PageSize = 50
            };

            _mockAuditService.Setup(s => s.GetAuditEventsAsync(
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                    null, null, null, 1, 50))
                .ReturnsAsync((new List<ConduitLLM.Configuration.Entities.PricingAuditEvent>(), 0));

            // Act
            var result = await _controller.QueryPricingAuditEvents(request);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<PricingAuditQueryResponse>().Subject;
            response.TotalCount.Should().Be(0);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Admin Audit") && o.ToString()!.Contains("Queried")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task QueryPricingAuditEvents_InvalidDateRange_ReturnsBadRequest()
        {
            // Arrange
            var request = new PricingAuditQueryRequest
            {
                From = DateTime.UtcNow,
                To = DateTime.UtcNow.AddDays(-7) // From > To
            };

            // Act
            var result = await _controller.QueryPricingAuditEvents(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task QueryPricingAuditEvents_PageSizeTooLarge_ReturnsBadRequest()
        {
            // Arrange
            var request = new PricingAuditQueryRequest
            {
                From = DateTime.UtcNow.AddDays(-1),
                To = DateTime.UtcNow,
                PageSize = 1001
            };

            // Act
            var result = await _controller.QueryPricingAuditEvents(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetPricingTypes Tests

        [Fact]
        public void GetPricingTypes_ReturnsOkWithTypes()
        {
            // Act
            var result = _controller.GetPricingTypes();

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().NotBeNull();
        }

        #endregion

        #region GetPricingAuditByRequestId Tests

        [Fact]
        public async Task GetPricingAuditByRequestId_NoEvents_ReturnsNotFound()
        {
            // Arrange
            _mockAuditService.Setup(s => s.GetByRequestIdAsync("req-123"))
                .ReturnsAsync(new List<ConduitLLM.Configuration.Entities.PricingAuditEvent>());

            // Act
            var result = await _controller.GetPricingAuditByRequestId("req-123");

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion
    }
}
