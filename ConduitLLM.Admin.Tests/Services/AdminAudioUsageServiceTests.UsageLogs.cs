using System;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace ConduitLLM.Admin.Tests.Services
{
    public partial class AdminAudioUsageServiceTests
    {
        #region GetUsageLogsAsync Tests

        [Fact]
        public async Task GetUsageLogsAsync_WithValidQuery_ShouldReturnPagedResults()
        {
            // Arrange
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                ProviderId = 1
            };

            var logs = CreateSampleAudioUsageLogs(15);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs.Take(10).ToList(),
                TotalCount = 15,
                Page = 1,
                PageSize = 10,
                TotalPages = 2
            };

            _mockRepository.Setup(x => x.GetPagedAsync(query))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.GetUsageLogsAsync(query);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(10);
            result.TotalCount.Should().Be(15);
            result.TotalPages.Should().Be(2);
            result.Items.First().ProviderId.Should().Be(1);
        }

        [Fact]
        public async Task GetUsageLogsAsync_WithDateRange_ShouldFilterResults()
        {
            // Arrange
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            
            var query = new AudioUsageQueryDto
            {
                Page = 1,
                PageSize = 10,
                StartDate = startDate,
                EndDate = endDate
            };

            var logs = CreateSampleAudioUsageLogs(5);
            var pagedResult = new PagedResult<AudioUsageLog>
            {
                Items = logs,
                TotalCount = 5,
                Page = 1,
                PageSize = 10,
                TotalPages = 1
            };

            _mockRepository.Setup(x => x.GetPagedAsync(It.Is<AudioUsageQueryDto>(q => 
                q.StartDate == startDate && q.EndDate == endDate)))
                .ReturnsAsync(pagedResult);

            // Act
            var result = await _service.GetUsageLogsAsync(query);

            // Assert
            result.Items.Should().HaveCount(5);
            _mockRepository.Verify(x => x.GetPagedAsync(It.Is<AudioUsageQueryDto>(q => 
                q.StartDate == startDate && q.EndDate == endDate)), Times.Once);
        }

        #endregion
    }
}