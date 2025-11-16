using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class MediaLifecycleServiceTests
    {
        private readonly Mock<IMediaRecordRepository> _mockMediaRepository;
        private readonly Mock<IMediaStorageService> _mockStorageService;
        private readonly Mock<ILogger<MediaLifecycleService>> _mockLogger;
        private readonly Mock<IOptions<MediaManagementOptions>> _mockOptions;
        private readonly MediaManagementOptions _options;
        private readonly MediaLifecycleService _service;

        public MediaLifecycleServiceTests()
        {
            _mockMediaRepository = new Mock<IMediaRecordRepository>();
            _mockStorageService = new Mock<IMediaStorageService>();
            _mockLogger = new Mock<ILogger<MediaLifecycleService>>();
            _mockOptions = new Mock<IOptions<MediaManagementOptions>>();

            _options = new MediaManagementOptions
            {
                EnableOwnershipTracking = true,
                EnableAutoCleanup = true,
                MediaRetentionDays = 90,
                OrphanCleanupEnabled = true,
                AccessControlEnabled = false
            };

            _mockOptions.Setup(x => x.Value).Returns(_options);

            _service = new MediaLifecycleService(
                _mockMediaRepository.Object,
                _mockStorageService.Object,
                _mockLogger.Object,
                _mockOptions.Object);
        }
    }
}