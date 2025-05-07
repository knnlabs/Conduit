using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ConduitLLM.WebUI.Controllers;
using ConduitLLM.WebUI.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Controllers
{
    public class DatabaseBackupControllerTests
    {
        private readonly Mock<IDatabaseBackupService> _backupServiceMock;
        private readonly Mock<ILogger<DatabaseBackupController>> _loggerMock;
        private readonly DatabaseBackupController _controller;
        
        public DatabaseBackupControllerTests()
        {
            _backupServiceMock = new Mock<IDatabaseBackupService>();
            _loggerMock = new Mock<ILogger<DatabaseBackupController>>();
            _controller = new DatabaseBackupController(_backupServiceMock.Object, _loggerMock.Object);
        }
        
        [Fact]
        public async Task BackupDatabase_Returns_FileWithCorrectContentType_ForSQLite()
        {
            // Arrange
            byte[] testContent = new byte[100];
            new Random().NextBytes(testContent);
            
            _backupServiceMock.Setup(s => s.GetDatabaseProvider()).Returns("sqlite");
            _backupServiceMock.Setup(s => s.CreateBackupAsync()).ReturnsAsync(testContent);
            
            // Act
            var result = await _controller.BackupDatabase();
            
            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/x-sqlite3", fileResult.ContentType);
            Assert.Contains("conduit_backup_", fileResult.FileDownloadName);
            Assert.Contains(".db", fileResult.FileDownloadName);
            Assert.Equal(testContent, fileResult.FileContents);
        }
        
        [Fact]
        public async Task BackupDatabase_Returns_FileWithCorrectContentType_ForPostgreSQL()
        {
            // Arrange
            byte[] testContent = new byte[100];
            new Random().NextBytes(testContent);
            
            _backupServiceMock.Setup(s => s.GetDatabaseProvider()).Returns("postgres");
            _backupServiceMock.Setup(s => s.CreateBackupAsync()).ReturnsAsync(testContent);
            
            // Act
            var result = await _controller.BackupDatabase();
            
            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/json", fileResult.ContentType);
            Assert.Contains("conduit_backup_", fileResult.FileDownloadName);
            Assert.Contains(".json", fileResult.FileDownloadName);
            Assert.Equal(testContent, fileResult.FileContents);
        }
        
        [Fact]
        public async Task BackupDatabase_Returns_500StatusCode_WhenExceptionOccurs()
        {
            // Arrange
            _backupServiceMock.Setup(s => s.CreateBackupAsync()).ThrowsAsync(new Exception("Test exception"));
            
            // Act
            var result = await _controller.BackupDatabase();
            
            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.Contains("Failed to create database backup", objectResult.Value?.ToString() ?? "");
        }
        
        [Fact]
        public async Task RestoreDatabase_Returns_BadRequest_WhenNoFileProvided()
        {
            // Arrange - no file in the request
            
            // Act
            IFormFile? nullFile = null;
            var result = await _controller.RestoreDatabase(nullFile);
            
            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("No file uploaded", badRequestResult.Value?.ToString() ?? "");
        }
        
        [Fact]
        public async Task RestoreDatabase_Returns_BadRequest_WhenInvalidFileFormat()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(100);
            
            _backupServiceMock.Setup(s => s.ValidateBackupAsync(It.IsAny<byte[]>())).ReturnsAsync(false);
            
            // Act
            var result = await _controller.RestoreDatabase(fileMock.Object);
            
            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Invalid backup file format", badRequestResult.Value?.ToString() ?? "");
        }
        
        [Fact]
        public async Task RestoreDatabase_Returns_OkResult_WhenRestoreSucceeds()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((stream, token) => 
                {
                    byte[] content = Encoding.UTF8.GetBytes("test content");
                    stream.Write(content, 0, content.Length);
                });
            
            _backupServiceMock.Setup(s => s.ValidateBackupAsync(It.IsAny<byte[]>())).ReturnsAsync(true);
            _backupServiceMock.Setup(s => s.RestoreFromBackupAsync(It.IsAny<byte[]>())).ReturnsAsync(true);
            
            // Act
            var result = await _controller.RestoreDatabase(fileMock.Object);
            
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Database restored successfully", okResult.Value?.ToString() ?? "");
        }
        
        [Fact]
        public async Task RestoreDatabase_Returns_500StatusCode_WhenRestoreFails()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((stream, token) => 
                {
                    byte[] content = Encoding.UTF8.GetBytes("test content");
                    stream.Write(content, 0, content.Length);
                });
            
            _backupServiceMock.Setup(s => s.ValidateBackupAsync(It.IsAny<byte[]>())).ReturnsAsync(true);
            _backupServiceMock.Setup(s => s.RestoreFromBackupAsync(It.IsAny<byte[]>())).ReturnsAsync(false);
            
            // Act
            var result = await _controller.RestoreDatabase(fileMock.Object);
            
            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.Contains("Failed to restore database", objectResult.Value?.ToString() ?? "");
        }
        
        [Fact]
        public async Task RestoreDatabase_Returns_500StatusCode_WhenExceptionOccurs()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));
            
            // Act
            var result = await _controller.RestoreDatabase(fileMock.Object);
            
            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            Assert.Contains("Failed to restore database", objectResult.Value?.ToString() ?? "");
        }
    }
}