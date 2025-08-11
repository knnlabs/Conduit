using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Configuration.DTOs;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public partial class DownloadsControllerTests : ControllerTestBase
    {
        private readonly Mock<IFileRetrievalService> _mockFileRetrievalService;
        private readonly Mock<ILogger<DownloadsController>> _mockLogger;
        private readonly Mock<IMediaRecordRepository> _mockMediaRecordRepository;
        private readonly DownloadsController _controller;

        public DownloadsControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockFileRetrievalService = new Mock<IFileRetrievalService>();
            _mockLogger = CreateLogger<DownloadsController>();
            _mockMediaRecordRepository = new Mock<IMediaRecordRepository>();

            _controller = new DownloadsController(
                _mockFileRetrievalService.Object,
                _mockLogger.Object,
                _mockMediaRecordRepository.Object);

            _controller.ControllerContext = CreateControllerContext();
        }
    }
}