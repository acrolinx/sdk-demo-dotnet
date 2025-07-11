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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Acrolinx.Net.Check;
using Acrolinx.Net.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.AutoCheck
{
    /// <summary>
    /// Background service that monitors a directory for file changes and automatically
    /// processes supported files using Acrolinx for content checking. This service provides
    /// real-time monitoring with user interaction controls and automatic browser integration.
    /// </summary>
    public class AutoCheckService : BackgroundService
    {
        private readonly IAcrolinxConfiguration _configuration;
        private readonly IAcrolinxService _acrolinxService;
        private readonly IFileProcessingService _fileProcessingService;
        private readonly ILogger<AutoCheckService> _logger;
        private FileSystemWatcher? _watcher;

        /// <summary>
        /// Initializes a new instance of the AutoCheckService class.
        /// </summary>
        /// <param name="configuration">The Acrolinx configuration service.</param>
        /// <param name="acrolinxService">The Acrolinx API service for content checking.</param>
        /// <param name="fileProcessingService">The file processing service for validation and filtering.</param>
        /// <param name="logger">The logger instance for this service.</param>
        public AutoCheckService(
            IAcrolinxConfiguration configuration,
            IAcrolinxService acrolinxService,
            IFileProcessingService fileProcessingService,
            ILogger<AutoCheckService> logger)
        {
            _configuration = configuration;
            _acrolinxService = acrolinxService;
            _fileProcessingService = fileProcessingService;
            _logger = logger;
        }

        /// <summary>
        /// Executes the background service to monitor file changes in the configured directory.
        /// This method sets up a FileSystemWatcher and handles user interaction for pause/resume functionality.
        /// </summary>
        /// <param name="stoppingToken">A cancellation token that indicates when the service should stop.</param>
        /// <returns>A task that represents the asynchronous execution of the service.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Validate configuration
            if (!_configuration.IsValid)
            {
                _logger.LogError("Invalid configuration. Please check environment variables");
                _configuration.PrintValidationErrors();
                return;
            }

            string watchPath = _configuration.ContentDirectory;
            if (!Directory.Exists(watchPath))
            {
                _logger.LogError("Content directory does not exist: {WatchPath}", watchPath);
                return;
            }

            // Set up file system watcher
            _watcher = new FileSystemWatcher(watchPath)
            {   // if you don't want to watch subdirectories, set IncludeSubdirectories to false
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };

            _watcher.Created += OnFileChanged;
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            _watcher.EnableRaisingEvents = true;
            _logger.LogInformation("AutoCheck monitoring started for directory: {WatchPath}", watchPath);

            // Keep user interaction prompts as console output
            Console.WriteLine($"[AutoCheck] Watching '{watchPath}' for file changes...");
            Console.WriteLine("Press 'p' to pause, 'r' to resume, or Ctrl+C to quit.");

            // Handle user input
            _ = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true).KeyChar;
                        if (key == 'p' || key == 'P')
                        {
                            _watcher.EnableRaisingEvents = false;
                            _logger.LogInformation("Monitoring paused by user");
                            Console.WriteLine("[AutoCheck] Monitoring PAUSED.");
                        }
                        else if (key == 'r' || key == 'R')
                        {
                            _watcher.EnableRaisingEvents = true;
                            _logger.LogInformation("Monitoring resumed by user");
                            Console.WriteLine("[AutoCheck] Monitoring RESUMED.");
                        }
                    }
                    await Task.Delay(100, stoppingToken);
                }
            }, stoppingToken);

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        /// <summary>
        /// Event handler for file creation and modification events.
        /// This method delegates to an async handler to prevent blocking the file system watcher.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A FileSystemEventArgs that contains the event data.</param>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Fire and forget - handle async operation in background
            _ = HandleFileChangedAsync(e);
        }

        /// <summary>
        /// Handles file changed events asynchronously with proper validation and error handling.
        /// Only processes files that are valid and supported by Acrolinx.
        /// </summary>
        /// <param name="e">A FileSystemEventArgs that contains the event data.</param>
        /// <returns>A task that represents the asynchronous file change handling operation.</returns>
        private async Task HandleFileChangedAsync(FileSystemEventArgs e)
        {
            try
            {
                // Validate file exists and is supported
                if (!_fileProcessingService.IsFileValid(e.FullPath))
                {
                    _logger.LogDebug("File is not valid, skipping: {FilePath}", e.FullPath);
                    return;
                }

                if (!_fileProcessingService.IsFileSupported(e.FullPath))
                {
                    _logger.LogDebug("File type not supported, skipping: {FilePath}", e.FullPath);
                    return;
                }

                _logger.LogInformation("File {ChangeType}: {FilePath}", e.ChangeType, e.FullPath);
                Console.WriteLine($"[AutoCheck] File {e.ChangeType}: {e.FullPath}");

                string? scorecardUrl = await _acrolinxService.CheckWithAcrolinx(e.FullPath, checkType: CheckType.Automated);
                if (!string.IsNullOrEmpty(scorecardUrl))
                {
                    _logger.LogInformation("Check completed successfully for {FilePath}", e.FullPath);
                    _acrolinxService.OpenUrlInBrowser(scorecardUrl);
                    Console.WriteLine($"[AutoCheck] Opened scorecard: {scorecardUrl}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file change for {FilePath}", e.FullPath);
            }
        }

        /// <summary>
        /// Event handler for file rename events.
        /// This method delegates to an async handler to prevent blocking the file system watcher.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">A RenamedEventArgs that contains the event data.</param>
        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            // Fire and forget - handle async operation in background
            _ = HandleFileRenamedAsync(e);
        }

        /// <summary>
        /// Handles file renamed events asynchronously with proper validation and error handling.
        /// Only processes files that are valid and supported by Acrolinx.
        /// </summary>
        /// <param name="e">A RenamedEventArgs that contains the event data.</param>
        /// <returns>A task that represents the asynchronous file rename handling operation.</returns>
        private async Task HandleFileRenamedAsync(RenamedEventArgs e)
        {
            try
            {
                // Validate file exists and is supported
                if (!_fileProcessingService.IsFileValid(e.FullPath))
                {
                    _logger.LogDebug("Renamed file is not valid, skipping: {FilePath}", e.FullPath);
                    return;
                }

                if (!_fileProcessingService.IsFileSupported(e.FullPath))
                {
                    _logger.LogDebug("Renamed file type not supported, skipping: {FilePath}", e.FullPath);
                    return;
                }

                _logger.LogInformation("File Renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
                Console.WriteLine($"[AutoCheck] File Renamed: {e.OldFullPath} -> {e.FullPath}");

                string? scorecardUrl = await _acrolinxService.CheckWithAcrolinx(e.FullPath, checkType: CheckType.Automated);
                if (!string.IsNullOrEmpty(scorecardUrl))
                {
                    _logger.LogInformation("Check completed successfully for renamed file {FilePath}", e.FullPath);
                    _acrolinxService.OpenUrlInBrowser(scorecardUrl);
                    Console.WriteLine($"[AutoCheck] Opened scorecard: {scorecardUrl}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file rename for {FilePath}", e.FullPath);
            }
        }

        /// <summary>
        /// Disposes the AutoCheckService and releases managed resources including the FileSystemWatcher.
        /// This method ensures proper cleanup when the service is stopped.
        /// </summary>
        public override void Dispose()
        {
            _watcher?.Dispose();
            _logger.LogInformation("AutoCheck monitoring stopped");
            Console.WriteLine("Monitoring stopped. Exiting program.");
            base.Dispose();
        }
    }
} 