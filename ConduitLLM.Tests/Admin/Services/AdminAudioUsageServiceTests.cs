using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Admin.Services;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.DTOs.Audio;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Admin.Services
{
    /// <summary>
    /// Unit tests for the AdminAudioUsageService class.
    /// This partial class contains tests split across multiple files for better organization.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "AudioUsage")]
    public partial class AdminAudioUsageServiceTests
    {
        // The implementation is split across the partial class files:
        // - AdminAudioUsageServiceTests.Setup.cs: Constructor and helper methods
        // - AdminAudioUsageServiceTests.UsageLogs.cs: GetUsageLogsAsync tests
        // - AdminAudioUsageServiceTests.Summary.cs: GetUsageSummaryAsync tests
        // - AdminAudioUsageServiceTests.ByKey.cs: GetUsageByKeyAsync tests
        // - AdminAudioUsageServiceTests.ByProvider.cs: GetUsageByProviderAsync tests
        // - AdminAudioUsageServiceTests.RealtimeSessions.cs: Realtime session tests
        // - AdminAudioUsageServiceTests.Export.cs: Export tests
        // - AdminAudioUsageServiceTests.Cleanup.cs: Cleanup tests
    }
}