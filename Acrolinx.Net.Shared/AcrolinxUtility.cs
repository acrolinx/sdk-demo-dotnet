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
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Acrolinx.Net.Check;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.Shared
{
    public class AcrolinxService : IAcrolinxService
    {
        private readonly IAcrolinxConfiguration _configuration;
        private readonly ILogger<AcrolinxService> _logger;

        public AcrolinxService(IAcrolinxConfiguration configuration, ILogger<AcrolinxService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Submits the given file to Acrolinx for checking and returns the Scorecard URL.
        /// If in Batch mode, it also returns the Content Analysis Dashboard URL.
        /// </summary>
        public async Task<string?> CheckWithAcrolinx(string filePath, string? batchId = null, CheckType checkType = CheckType.Automated)
        {
            if (!_configuration.IsValid)
            {
                _logger.LogError("Configuration invalid. Cannot perform Acrolinx check");
                _configuration.PrintValidationErrors();
                return null;
            }

            // Validate file existence
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return null;
            }

            string content;
            try
            {
                content = File.ReadAllText(filePath);
                _logger.LogInformation("Successfully read file: {FilePath} (Content length: {Length} characters)", filePath, content.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not read file {FilePath}. Error: {Error}", filePath, ex.Message);
                return null;
            }

            try
            {
                var endpoint = new AcrolinxEndpoint(_configuration.AcrolinxUrl, _configuration.ClientSignature);
                _logger.LogInformation("Starting check for file: {FilePath}", filePath);
                var accessToken = await endpoint.SignInWithSSO(_configuration.ApiToken, _configuration.Username);
                _logger.LogInformation("Successfully signed in for file: {FilePath}", filePath);

                var checkRequest = new CheckRequest()
                {
                    CheckOptions = new CheckOptions()
                    {
                        CheckType = checkType, // Batch or Automated
                        ContentFormat = "AUTO",
                        BatchId = checkType == CheckType.Batch ? batchId : null
                    },
                    Document = new DocumentDescriptorRequest(filePath, new System.Collections.Generic.List<CustomField>()),
                    Content = content
                };

                _logger.LogInformation("Sending check request for file: {FilePath} (Batch ID: {BatchId}, Check Type: {CheckType})", 
                    filePath, batchId, checkType);
                var checkResult = await endpoint.Check(accessToken, checkRequest);
                _logger.LogInformation("Check request completed for file: {FilePath}", filePath);

                if (checkResult == null)
                {
                    _logger.LogWarning("Check result is null for file: {FilePath}", filePath);
                    return null;
                }

                _logger.LogInformation("Check {CheckId} completed for {FilePath}: Score {Score} ({Status})", 
                    checkResult.Id, filePath, checkResult.Quality.Score, checkResult.Quality.Status);

                string? scorecardUrl = checkResult.Reports.ContainsKey("scorecard") ? checkResult.Reports["scorecard"].Link : null;
                _logger.LogInformation("Scorecard: {ScorecardUrl}", scorecardUrl ?? "Not Available");

                if (checkType == CheckType.Batch && checkResult.Reports.ContainsKey("contentAnalysisDashboard"))
                {
                    string? dashboardUrl = checkResult.Reports["contentAnalysisDashboard"].Link;
                    _logger.LogInformation("Content Analysis Dashboard: {DashboardUrl}", dashboardUrl);
                    return dashboardUrl;
                }

                return scorecardUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError("Acrolinx check failed for {FilePath}. Error: {Error}", filePath, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Opens a given URL in the default web browser.
        /// </summary>
        public void OpenUrlInBrowser(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://"))
            {
                _logger.LogWarning("Invalid URL. Cannot open in browser");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                _logger.LogInformation("Opening {Url} in default browser", url);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to open browser. {Error}", ex.Message);
            }
        }
    }

    // Keep the static utility class for backward compatibility if needed
    public static class AcrolinxUtility
    {
        private static readonly AcrolinxConfiguration Configuration = new AcrolinxConfiguration(
            Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AcrolinxConfiguration>()
        );

        /// <summary>
        /// Gets the current configuration instance.
        /// </summary>
        public static AcrolinxConfiguration GetConfiguration()
        {
            return Configuration;
        }

        /// <summary>
        /// Retrieves the directory where content files are stored.
        /// Returns null if directory is not configured or doesn't exist.
        /// </summary>
        public static string? GetContentDirectory()
        {
            if (!Configuration.IsValid)
            {
                Configuration.PrintValidationErrors();
                return null;
            }

            return Configuration.ContentDirectory;
        }

        /// <summary>
        /// Submits the given file to Acrolinx for checking and returns the Scorecard URL.
        /// If in Batch mode, it also returns the Content Analysis Dashboard URL.
        /// </summary>
        public static async Task<string?> CheckWithAcrolinx(string filePath, string? batchId = null, CheckType checkType = CheckType.Automated)
        {
            var logger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AcrolinxService>();
            var service = new AcrolinxService(Configuration, logger);
            return await service.CheckWithAcrolinx(filePath, batchId, checkType);
        }

        /// <summary>
        /// Opens a given URL in the default web browser.
        /// </summary>
        public static void OpenUrlInBrowser(string? url)
        {
            var logger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<AcrolinxService>();
            var service = new AcrolinxService(Configuration, logger);
            service.OpenUrlInBrowser(url);
        }
    }
}
