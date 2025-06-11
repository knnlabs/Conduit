using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConduitLLM.WebUI.Interfaces;

using Moq;

namespace ConduitLLM.Tests.WebUI.Extensions
{
    /// <summary>
    /// Helper methods for testing DatabaseBackupService
    /// </summary>
    public static class DatabaseBackupServiceTestHelpers
    {
        /// <summary>
        /// Sets up a mock for CreateDatabaseBackupAsync
        /// </summary>
        /// <param name="mockClient">The mock client</param>
        /// <param name="success">Whether the backup should succeed</param>
        public static void SetupCreateDatabaseBackup(
            this Mock<IAdminApiClient> mockClient,
            bool success)
        {
            mockClient.Setup(client => client.CreateDatabaseBackupAsync())
                .Returns(Task.FromResult(success));
        }

        /// <summary>
        /// Sets up a mock for GetDatabaseBackupDownloadUrl
        /// </summary>
        /// <param name="mockClient">The mock client</param>
        /// <param name="url">The URL to return</param>
        public static void SetupGetDatabaseBackupDownloadUrl(
            this Mock<IAdminApiClient> mockClient,
            string url)
        {
            mockClient.Setup(client => client.GetDatabaseBackupDownloadUrl())
                .Returns(Task.FromResult(url));
        }
    }
}
