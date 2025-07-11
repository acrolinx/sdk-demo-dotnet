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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acrolinx.Net.Check;
using Acrolinx.Net.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.Demo
{
    public class BatchProcessingService : IHostedService
    {
        private readonly IAcrolinxConfiguration _configuration;
        private readonly IAcrolinxService _acrolinxService;
        private readonly ILogger<BatchProcessingService> _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        // Define the supported file types based on Acrolinx documentation
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "xml", "xhtm", "xhtml", "svg", "resx", "xlf", "xliff", "dita", "ditamap", "ditaval",
            "html", "htm",
            "markdown", "mdown", "mkdn", "mkd", "md",
            "txt",
            "java",
            "c", "h", "cc", "cpp", "cxx", "c++", "hh", "hpp", "hxx", "h++", "dic",
            "yaml", "yml",
            "properties",
            "json"
        };

        public BatchProcessingService(
            IAcrolinxConfiguration configuration,
            IAcrolinxService acrolinxService,
            ILogger<BatchProcessingService> logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _configuration = configuration;
            _acrolinxService = acrolinxService;
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

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

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

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

            // Get all files in the directory and subdirectories
            string[] allFiles = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            _logger.LogInformation("Found {FileCount} files in directory", allFiles.Length);

            foreach (var file in allFiles)
            {
                _logger.LogDebug("Found file: {FilePath}", file);
            }

            // Filter files to only include supported formats
            string[] contentFiles = allFiles
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).TrimStart('.').ToLower()))
                .ToArray();

            _logger.LogInformation("Found {SupportedFileCount} supported files after filtering", contentFiles.Length);
            foreach (var file in contentFiles)
            {
                _logger.LogDebug("Filtered file: {FilePath}", file);
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