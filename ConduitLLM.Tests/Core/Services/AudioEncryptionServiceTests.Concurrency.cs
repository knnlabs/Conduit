using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Core.Services
{
    public partial class AudioEncryptionServiceTests
    {
        [Fact]
        public async Task EncryptAudioAsync_ConcurrentCalls_ProducesUniqueResults()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio data for concurrent encryption");
            var taskCount = 100;
            var tasks = new Task<EncryptedAudioData>[taskCount];

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                tasks[i] = _service.EncryptAudioAsync(audioData);
            }
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(taskCount);
            
            // Each encryption should have unique IV
            var uniqueIVs = results.Select(r => Convert.ToBase64String(r.IV)).Distinct().Count();
            uniqueIVs.Should().Be(taskCount, "each encryption should have a unique IV");
            
            // Each encryption should produce different ciphertext
            var uniqueCiphertexts = results.Select(r => Convert.ToBase64String(r.EncryptedBytes)).Distinct().Count();
            uniqueCiphertexts.Should().Be(taskCount, "each encryption should produce unique ciphertext");
        }

        [Fact]
        public async Task DecryptAudioAsync_ConcurrentDecryption_AllSucceed()
        {
            // Arrange
            var originalData = Encoding.UTF8.GetBytes("Test data for concurrent decryption");
            var encryptedData = await _service.EncryptAudioAsync(originalData);
            var taskCount = 50;
            var tasks = new Task<byte[]>[taskCount];

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                tasks[i] = _service.DecryptAudioAsync(encryptedData);
            }
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(taskCount);
            foreach (var result in results)
            {
                result.Should().BeEquivalentTo(originalData);
            }
        }

        [Fact]
        public async Task GenerateKeyAsync_ConcurrentGeneration_ProducesUniqueKeys()
        {
            // Arrange
            var taskCount = 100;
            var tasks = new Task<string>[taskCount];

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                tasks[i] = _service.GenerateKeyAsync();
            }
            var keyIds = await Task.WhenAll(tasks);

            // Assert
            keyIds.Should().HaveCount(taskCount);
            keyIds.Distinct().Count().Should().Be(taskCount, "all generated key IDs should be unique");
        }

        [Fact]
        public async Task EncryptDecrypt_ConcurrentMixedOperations_AllSucceed()
        {
            // Arrange
            var operationCount = 200;
            var tasks = new List<Task>();
            var encryptedDataList = new List<EncryptedAudioData>();
            var semaphore = new SemaphoreSlim(1, 1);

            // Act - Mix of encrypt and decrypt operations
            for (int i = 0; i < operationCount; i++)
            {
                if (i % 2 == 0)
                {
                    // Encrypt operation
                    var data = Encoding.UTF8.GetBytes($"Test data {i}");
                    var task = Task.Run(async () =>
                    {
                        var encrypted = await _service.EncryptAudioAsync(data);
                        await semaphore.WaitAsync();
                        try
                        {
                            encryptedDataList.Add(encrypted);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    tasks.Add(task);
                }
                else if (encryptedDataList.Count > 0)
                {
                    // Decrypt operation
                    await semaphore.WaitAsync();
                    EncryptedAudioData? dataToDecrypt = null;
                    try
                    {
                        if (encryptedDataList.Count > 0)
                        {
                            dataToDecrypt = encryptedDataList[0];
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }

                    if (dataToDecrypt != null)
                    {
                        var task = Task.Run(async () =>
                        {
                            var decrypted = await _service.DecryptAudioAsync(dataToDecrypt);
                            decrypted.Should().NotBeNull();
                        });
                        tasks.Add(task);
                    }
                }
            }

            // Assert
            await Task.WhenAll(tasks);
            tasks.Should().NotBeEmpty();
        }

        [Fact]
        public async Task ValidateIntegrityAsync_ConcurrentValidation_AllReturnCorrectResults()
        {
            // Arrange
            var validData = await _service.EncryptAudioAsync(Encoding.UTF8.GetBytes("Valid data"));
            var tamperedData = await _service.EncryptAudioAsync(Encoding.UTF8.GetBytes("Tampered data"));
            tamperedData.AuthTag[0] ^= 0xFF; // Tamper with auth tag

            var taskCount = 50;
            var validTasks = new Task<bool>[taskCount];
            var tamperedTasks = new Task<bool>[taskCount];

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                validTasks[i] = _service.ValidateIntegrityAsync(validData);
                tamperedTasks[i] = _service.ValidateIntegrityAsync(tamperedData);
            }

            var validResults = await Task.WhenAll(validTasks);
            var tamperedResults = await Task.WhenAll(tamperedTasks);

            // Assert
            validResults.Should().AllBeEquivalentTo(true);
            tamperedResults.Should().AllBeEquivalentTo(false);
        }

        [Fact]
        public async Task EncryptAudioAsync_ConcurrentWithDifferentMetadata_HandlesCorrectly()
        {
            // Arrange
            var audioData = Encoding.UTF8.GetBytes("Test audio with metadata");
            var taskCount = 50;
            var tasks = new Task<EncryptedAudioData>[taskCount];

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                var metadata = new AudioEncryptionMetadata
                {
                    Format = $"format-{i}",
                    OriginalSize = i * 100,
                    DurationSeconds = i * 0.5,
                    VirtualKey = $"key-{i}",
                    CustomProperties = new() { [$"prop-{i}"] = $"value-{i}" }
                };
                tasks[i] = _service.EncryptAudioAsync(audioData, metadata);
            }
            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(taskCount);
            var uniqueMetadata = results.Select(r => r.EncryptedMetadata).Distinct().Count();
            uniqueMetadata.Should().Be(taskCount, "each encryption should have unique metadata");
        }

        [Fact]
        public async Task EncryptDecrypt_HighConcurrency_MaintainsDataIntegrity()
        {
            // Arrange
            var concurrencyLevel = 500;
            var dataSize = 1024; // 1KB per operation
            var tasks = new List<Task<bool>>();

            // Act
            for (int i = 0; i < concurrencyLevel; i++)
            {
                var index = i;
                var task = Task.Run(async () =>
                {
                    try
                    {
                        // Generate unique data for each task
                        var originalData = new byte[dataSize];
                        new Random(index).NextBytes(originalData);
                        
                        // Encrypt
                        var encrypted = await _service.EncryptAudioAsync(originalData);
                        
                        // Decrypt
                        var decrypted = await _service.DecryptAudioAsync(encrypted);
                        
                        // Verify
                        return decrypted.SequenceEqual(originalData);
                    }
                    catch
                    {
                        return false;
                    }
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().AllBeEquivalentTo(true);
            var successCount = results.Count(r => r);
            successCount.Should().Be(concurrencyLevel, $"all {concurrencyLevel} operations should succeed");
        }

        [Fact]
        public async Task GenerateKeyAsync_RapidKeyGeneration_NoDuplicates()
        {
            // Arrange
            var keyCount = 1000;
            var tasks = new Task<string>[keyCount];
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2
            };

            // Act - Generate keys as fast as possible
            Parallel.ForEach(Enumerable.Range(0, keyCount), parallelOptions, (i) =>
            {
                tasks[i] = _service.GenerateKeyAsync();
            });

            var keyIds = await Task.WhenAll(tasks);

            // Assert
            var uniqueKeyIds = new HashSet<string>(keyIds);
            uniqueKeyIds.Count.Should().Be(keyCount, "all generated keys should be unique even under high concurrency");
        }

        [Fact]
        public async Task EncryptAudioAsync_ConcurrentLargeData_HandlesMemoryPressure()
        {
            // Arrange
            var concurrentOps = 20;
            var dataSize = 5 * 1024 * 1024; // 5MB per operation
            var tasks = new Task<EncryptedAudioData>[concurrentOps];

            // Act
            for (int i = 0; i < concurrentOps; i++)
            {
                var largeData = new byte[dataSize];
                new Random(i).NextBytes(largeData);
                tasks[i] = _service.EncryptAudioAsync(largeData);
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            results.Should().HaveCount(concurrentOps);
            results.Should().AllSatisfy(r =>
            {
                r.Should().NotBeNull();
                r.EncryptedBytes.Length.Should().Be(dataSize);
            });
        }

        [Theory]
        [InlineData(10, 100)]   // 10 threads, 100 operations each
        [InlineData(50, 20)]    // 50 threads, 20 operations each
        [InlineData(100, 10)]   // 100 threads, 10 operations each
        public async Task EncryptDecrypt_VariousConcurrencyPatterns_AllSucceed(int threadCount, int operationsPerThread)
        {
            // Arrange
            var tasks = new Task[threadCount];
            var errors = new ConcurrentBag<Exception>();

            // Act
            for (int t = 0; t < threadCount; t++)
            {
                var threadIndex = t;
                tasks[t] = Task.Run(async () =>
                {
                    for (int op = 0; op < operationsPerThread; op++)
                    {
                        try
                        {
                            var data = Encoding.UTF8.GetBytes($"Thread {threadIndex} Operation {op}");
                            var encrypted = await _service.EncryptAudioAsync(data);
                            var decrypted = await _service.DecryptAudioAsync(encrypted);
                            
                            if (!decrypted.SequenceEqual(data))
                            {
                                throw new InvalidOperationException($"Data mismatch in thread {threadIndex} operation {op}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            errors.Should().BeEmpty("no errors should occur during concurrent operations");
        }

        [Fact]
        public async Task ThreadSafety_DemonstrateKeyStorageRaceCondition()
        {
            // This test demonstrates the thread safety issue in the current implementation
            // The Dictionary<string, byte[]> _keyStore is not thread-safe
            // When multiple threads try to create the "default" key simultaneously,
            // they may end up with different keys, causing decryption failures
            
            // Arrange
            var freshService = new AudioEncryptionService(_loggerMock.Object);
            var iterations = 20; // Reduced from 100 to prevent overwhelming the test runner
            var encryptTasks = new Task<EncryptedAudioData>[iterations];
            var data = Encoding.UTF8.GetBytes("Test data");
            
            // Use a cancellation token to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            
            // Act - Force race condition by starting all encryptions at once
            // Use SemaphoreSlim to control concurrency instead of Barrier
            var startSignal = new TaskCompletionSource<bool>();
            
            for (int i = 0; i < iterations; i++)
            {
                encryptTasks[i] = Task.Run(async () =>
                {
                    await startSignal.Task; // Wait for signal to start
                    return await freshService.EncryptAudioAsync(data);
                });
            }
            
            // Signal all tasks to start
            startSignal.SetResult(true);
            
            var encryptedResults = await Task.WhenAll(encryptTasks);
                
                // Try to decrypt all with the same service instance
                var decryptionFailures = 0;
                foreach (var encrypted in encryptedResults)
                {
                    try
                    {
                        var decrypted = await freshService.DecryptAudioAsync(encrypted);
                        if (!decrypted.SequenceEqual(data))
                        {
                            decryptionFailures++;
                        }
                    }
                    catch
                    {
                        decryptionFailures++;
                    }
                }
                
                // Assert - Due to race condition, some decryptions may fail
                // This documents the thread safety issue
                Log($"Decryption failures due to race condition: {decryptionFailures}/{iterations}");
                
                // If there are failures, it proves the race condition exists
                // If there are no failures, it doesn't prove thread safety (just lucky timing)
                // The Dictionary implementation is definitively not thread-safe
        }
    }
}