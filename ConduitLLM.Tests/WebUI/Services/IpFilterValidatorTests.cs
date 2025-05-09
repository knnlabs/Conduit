using System;
using System.Collections.Generic;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.DTOs.IpFilter;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.WebUI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.WebUI.Services
{
    public class IpFilterValidatorTests
    {
        private readonly Mock<ILogger<IpFilterValidator>> _mockLogger;
        private readonly IpFilterValidator _validator;
        
        public IpFilterValidatorTests()
        {
            _mockLogger = new Mock<ILogger<IpFilterValidator>>();
            _validator = new IpFilterValidator(_mockLogger.Object);
        }
        
        [Fact]
        public void ValidateFilter_WithValidFilter_ReturnsValid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "192.168.1.1",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>();
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithInvalidFilterType_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = "invalid-type",
                IpAddressOrCidr = "192.168.1.1",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>();
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("Invalid filter type", errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithEmptyIpAddress_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>();
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("IP address or CIDR subnet cannot be empty", errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithInvalidIpAddress_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "invalid-ip",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>();
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("Invalid IP address format", errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithInvalidCidr_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "192.168.1.0/33", // Invalid prefix length
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>();
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("Invalid CIDR notation", errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithDuplicateFilter_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "192.168.1.1",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    Id = 1,
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.1",
                    Description = "Existing filter",
                    IsEnabled = true
                }
            };
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("rule already exists", errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithConflictingFilterType_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "192.168.1.1",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    Id = 1,
                    FilterType = IpFilterConstants.WHITELIST,
                    IpAddressOrCidr = "192.168.1.1",
                    Description = "Existing filter",
                    IsEnabled = true
                }
            };
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("Conflict detected", errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WhenUpdating_IgnoresExistingFilterWithSameId()
        {
            // Arrange
            var filter = new UpdateIpFilterDto
            {
                Id = 1,
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "192.168.1.1",
                Description = "Updated filter",
                IsEnabled = true
            };

            var existingFilters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    Id = 1,
                    FilterType = IpFilterConstants.BLACKLIST,
                    IpAddressOrCidr = "192.168.1.1",
                    Description = "Existing filter",
                    IsEnabled = true
                }
            };

            // Convert UpdateIpFilterDto to CreateIpFilterDto for the validator
            var createDto = new CreateIpFilterDto
            {
                FilterType = filter.FilterType,
                IpAddressOrCidr = filter.IpAddressOrCidr,
                Description = filter.Description,
                IsEnabled = filter.IsEnabled
            };

            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(createDto, existingFilters, true, 1);

            // Debug info was useful during development, keeping it commented for future reference
            // Console.WriteLine($"isValid: {isValid}, errorMessage: {errorMessage}");
            // Console.WriteLine($"Filter ID: {filter.Id}, FilterType: {filter.FilterType}, IP: {filter.IpAddressOrCidr}");

            // Assert
            Assert.True(isValid);
            Assert.Null(errorMessage);
        }
        
        [Fact]
        public void ValidateFilter_WithCidrConflict_ReturnsInvalid()
        {
            // Arrange
            var filter = new CreateIpFilterDto
            {
                FilterType = IpFilterConstants.BLACKLIST,
                IpAddressOrCidr = "192.168.1.0/24",
                Description = "Test filter",
                IsEnabled = true
            };
            
            var existingFilters = new List<IpFilterEntity>
            {
                new IpFilterEntity
                {
                    Id = 1,
                    FilterType = IpFilterConstants.WHITELIST,
                    IpAddressOrCidr = "192.168.1.0/25", // Subnet of the new filter
                    Description = "Existing filter",
                    IsEnabled = true
                }
            };
            
            // Act
            var (isValid, errorMessage) = _validator.ValidateFilter(filter, existingFilters);
            
            // Assert
            Assert.False(isValid);
            Assert.Contains("Conflict detected", errorMessage);
        }
    }
}