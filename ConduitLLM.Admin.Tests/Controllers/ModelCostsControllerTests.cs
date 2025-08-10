using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.Admin.Controllers;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Tests.TestHelpers;
using ConduitLLM.Configuration.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Admin.Tests.Controllers
{
    /// <summary>
    /// Unit tests for the ModelCostsController class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AdminController")]
    public partial class ModelCostsControllerTests
    {
        private readonly Mock<IAdminModelCostService> _mockService;
        private readonly Mock<ILogger<ModelCostsController>> _mockLogger;
        private readonly ModelCostsController _controller;
        private readonly ITestOutputHelper _output;

        public ModelCostsControllerTests(ITestOutputHelper output)
        {
            _output = output;
            _mockService = new Mock<IAdminModelCostService>();
            _mockLogger = new Mock<ILogger<ModelCostsController>>();
            _controller = new ModelCostsController(_mockService.Object, _mockLogger.Object);
        }

    }

}