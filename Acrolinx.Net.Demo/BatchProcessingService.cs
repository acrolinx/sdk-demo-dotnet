/*
 * Copyright 2025-present Acrolinx GmbH
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * You may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Acrolinx.Net.Check;
using Acrolinx.Net.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.Demo
{
    /// <summary>
    /// Hosted service that processes multiple files in batch mode using Acrolinx for content checking.
    /// This service implements intelligent concurrency control to prevent API timeouts and provides
    /// comprehensive batch reporting functionality.
    /// </summary>
    public class BatchProcessingService : IHostedService
    {
        private readonly IAcrolinxConfiguration _configuration;
        private readonly IAcrolinxService _acrolinxService;
        private readonly IFileProcessingService _fileProcessingService;
        private readonly ILogger<BatchProcessingService> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        /// <summary>
        /// Initializes a new instance of the BatchProcessingService class.
        /// </summary>
        /// <param name="configuration">The Acrolinx configuration service.</param>
        /// <param name="acrolinxService">The Acrolinx API service for content checking.</param>
        /// <param name="fileProcessingService">The file processing service for validation and filtering.</param>
        /// <param name="logger">The logger instance for this service.</param>
        /// <param name="hostApplicationLifetime">The host application lifetime service for shutdown control.</param>
        public BatchProcessingService(
            IAcrolinxConfiguration configuration,
            IAcrolinxService acrolinxService,
            IFileProcessingService fileProcessingService,
            ILogger<BatchProcessingService> logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _configuration = configuration;
            _acrolinxService = acrolinxService;
            _fileProcessingService = fileProcessingService;
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        /// <summary>
        /// Starts the batch processing service. This method processes all supported files
        /// in the configured content directory using intelligent concurrency control.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous start operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ProcessBatchAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Unhandled exception in batch processing");
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        }

        /// <summary>
        /// Stops the batch processing service. This method performs cleanup operations.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A completed task since no async operations are needed for stopping.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes all supported files in the configured directory in batch mode.
        /// This method handles user input for batch ID, file discovery, concurrency control,
        /// and result aggregation with comprehensive reporting.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous batch processing operation.</returns>
        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            // Validate configuration
            if (!_configuration.IsValid)
            {
                _logger.LogError("Invalid configuration. Please check environment variables");
                _configuration.PrintValidationErrors();
                return;
            }

            // Prompt user for Batch ID
            Console.Write("Enter a Batch ID (or press Enter for default): ");
            string? batchId = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(batchId))
            {
                batchId = $"batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                _logger.LogInformation("Using default Batch ID: {BatchId}", batchId);
            }
            else
            {
                _logger.LogInformation("Using provided Batch ID: {BatchId}", batchId);
            }

            // Get content directory from configuration
            string directoryPath = _configuration.ContentDirectory;
            if (string.IsNullOrEmpty(directoryPath))
            {
                _logger.LogError("Content directory not configured");
                return;
            }

            // Get all supported files in the directory and subdirectories
            string[] contentFiles = _fileProcessingService.GetSupportedFiles(directoryPath, includeSubdirectories: true);
            
            foreach (var file in contentFiles)
            {
                _logger.LogDebug("Found supported file: {FilePath}", file);
            }

            if (contentFiles.Length == 0)
            {
                _logger.LogWarning("No supported files found in {DirectoryPath}", directoryPath);
                return;
            }

            // Process files with concurrency control to prevent API timeouts
            const int maxConcurrency = 2; // Limit to 2 concurrent requests
            using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            
            List<Task<string?>> checkTasks = new List<Task<string?>>();
            foreach (string file in contentFiles)
            {
                _logger.LogInformation("Adding file to batch check: {FilePath}", file);
                
                var task = ProcessFileWithThrottling(file, batchId, semaphore, cancellationToken);
                checkTasks.Add(task);
            }

            _logger.LogInformation("Waiting for {TaskCount} check tasks to complete (max {MaxConcurrency} concurrent)", 
                checkTasks.Count, maxConcurrency);
            var results = await Task.WhenAll(checkTasks);
            _logger.LogInformation("All {ResultCount} check tasks completed", results.Length);

            // Process results
            _logger.LogInformation("Check Results Summary:");
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < contentFiles.Length; i++)
            {
                bool success = results[i] != null;
                if (success)
                {
                    successCount++;
                    _logger.LogInformation("SUCCESS: {FilePath} - {Url}", contentFiles[i], results[i]);
                }
                else
                {
                    failCount++;
                    _logger.LogWarning("FAILED: {FilePath}", contentFiles[i]);
                }
            }

            _logger.LogInformation("Batch processing completed: {SuccessCount} successful, {FailCount} failed", successCount, failCount);

            // Find the first valid Content Analysis Dashboard link
            string? contentAnalysisDashboardLink = results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result));

            // Print summary
            _logger.LogInformation("Batch Check Summary - Batch ID: {BatchId}", batchId);
            if (!string.IsNullOrWhiteSpace(contentAnalysisDashboardLink))
            {
                _logger.LogInformation("Content Analysis Dashboard (Batch Report): {DashboardUrl}", contentAnalysisDashboardLink);
                _acrolinxService.OpenUrlInBrowser(contentAnalysisDashboardLink);
            }
            else
            {
                _logger.LogWarning("No Content Analysis Dashboard report was generated");
            }
        }

        /// <summary>
        /// Processes a single file with throttling control to prevent API overload.
        /// This method implements semaphore-based concurrency control and API-friendly pacing.
        /// </summary>
        /// <param name="filePath">The path of the file to process.</param>
        /// <param name="batchId">The batch ID to associate with this file check.</param>
        /// <param name="semaphore">The semaphore for controlling concurrent operations.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous file processing operation, returning the content analysis dashboard URL on success or null on failure.</returns>
        private async Task<string?> ProcessFileWithThrottling(string filePath, string batchId, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Starting throttled processing for: {FilePath}", filePath);
                var result = await _acrolinxService.CheckWithAcrolinx(filePath, batchId, CheckType.Batch);
                
                // Add small delay between requests to be API-friendly
                await Task.Delay(500, cancellationToken);
                
                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
} 