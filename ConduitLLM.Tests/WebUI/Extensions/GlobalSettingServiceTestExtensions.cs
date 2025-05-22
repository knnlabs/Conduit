using ConduitLLM.Configuration.DTOs;
using ConduitLLM.WebUI.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Helper methods for testing GlobalSettingService
    /// </summary>
    public static class GlobalSettingServiceTestHelpers
    {
        /// <summary>
        /// Sets up a mock for GetGlobalSettingByKeyAsync
        /// </summary>
        /// <param name="mockClient">The mock client</param>
        /// <param name="key">The key to look up</param>
        /// <param name="value">The value to return</param>
        public static void SetupGetGlobalSetting(
            this Mock<IAdminApiClient> mockClient,
            string key,
            string value)
        {
            mockClient.Setup(client => client.GetGlobalSettingByKeyAsync(key))
                .Returns(Task.FromResult<GlobalSettingDto?>(new GlobalSettingDto { Key = key, Value = value }));
        }

        /// <summary>
        /// Sets up a mock for UpsertGlobalSettingAsync
        /// </summary>
        /// <param name="mockClient">The mock client</param>
        /// <param name="key">The key to look for in the DTO</param>
        /// <param name="success">Whether the operation should succeed</param>
        public static void SetupUpsertGlobalSetting(
            this Mock<IAdminApiClient> mockClient,
            string key,
            bool success)
        {
            mockClient.Setup(client => client.UpsertGlobalSettingAsync(
                It.Is<GlobalSettingDto>(dto => dto.Key == key)))
                .Returns(Task.FromResult<GlobalSettingDto?>(
                    success ? new GlobalSettingDto { Key = key, Value = "test-value" } : null));
        }
    }
}