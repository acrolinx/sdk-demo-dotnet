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
    public class AutoCheckService : BackgroundService
    {
        private readonly IAcrolinxConfiguration _configuration;
        private readonly IAcrolinxService _acrolinxService;
        private readonly ILogger<AutoCheckService> _logger;
        private FileSystemWatcher? _watcher;

        public AutoCheckService(
            IAcrolinxConfiguration configuration,
            IAcrolinxService acrolinxService,
            ILogger<AutoCheckService> logger)
        {
            _configuration = configuration;
            _acrolinxService = acrolinxService;
            _logger = logger;
        }

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
            {
                IncludeSubdirectories = false,
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

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(e.FullPath)) return;

            _logger.LogInformation("File {ChangeType}: {FilePath}", e.ChangeType, e.FullPath);
            Console.WriteLine($"[AutoCheck] File {e.ChangeType}: {e.FullPath}");

            try
            {
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

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (!File.Exists(e.FullPath)) return;

            _logger.LogInformation("File Renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
            Console.WriteLine($"[AutoCheck] File Renamed: {e.OldFullPath} -> {e.FullPath}");

            try
            {
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

        public override void Dispose()
        {
            _watcher?.Dispose();
            _logger.LogInformation("AutoCheck monitoring stopped");
            Console.WriteLine("Monitoring stopped. Exiting program.");
            base.Dispose();
        }
    }
} 