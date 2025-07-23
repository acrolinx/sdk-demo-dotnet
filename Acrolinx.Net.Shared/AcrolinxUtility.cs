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
using Acrolinx.Net.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.Shared
{
    public class AcrolinxService : IAcrolinxService
    {
        private readonly IAcrolinxConfiguration _configuration;
        private readonly ILogger<AcrolinxService> _logger;
        private readonly RetryPolicy _retryPolicy;
        private bool _disposed = false;

        public AcrolinxService(IAcrolinxConfiguration configuration, ILogger<AcrolinxService> logger, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _retryPolicy = RetryPolicy.CreateDefault(loggerFactory.CreateLogger<RetryPolicy>());
        }

        /// <summary>
        /// Submits the given file to Acrolinx for checking and returns the Scorecard URL.
        /// If in Batch mode, it also returns the Content Analysis Dashboard URL.
        /// </summary>
        public async Task<string?> CheckWithAcrolinx(string filePath, string? batchId = null, CheckType checkType = CheckType.Automated)
        {
            ThrowIfDisposed();
            
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

            return await _retryPolicy.ExecuteAsync(async () =>
            {
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
                        throw new AcrolinxApiException("Check result is null - API returned unexpected response", filePath);
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
                catch (TaskCanceledException)
                {
                    throw AcrolinxApiException.CreateTimeout(filePath, "Acrolinx API");
                }
                catch (TimeoutException)
                {
                    throw AcrolinxApiException.CreateTimeout(filePath, "Acrolinx API");
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("429"))
                {
                    throw AcrolinxApiException.CreateRateLimit(filePath, "Acrolinx API");
                }
                catch (System.Net.Http.HttpRequestException ex) when (ex.Message.Contains("5"))
                {
                    throw AcrolinxApiException.CreateServerError(filePath, "Acrolinx API", 500);
                }
                catch (AcrolinxApiException)
                {
                    // Re-throw our custom exceptions as-is
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during Acrolinx check for {FilePath}", filePath);
                    throw new AcrolinxApiException("Unexpected error during Acrolinx check", ex, filePath);
                }
            }, "Acrolinx Check", filePath) ?? null;
        }

        /// <summary>
        /// Opens a given URL in the default web browser.
        /// </summary>
        public void OpenUrlInBrowser(string? url)
        {
            ThrowIfDisposed();
            
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

        /// <summary>
        /// Disposes the AcrolinxService and releases any managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the AcrolinxService with the specified disposing flag.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here if any
                    _logger.LogDebug("AcrolinxService disposed");
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Throws an ObjectDisposedException if the service has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AcrolinxService));
            }
        }
    }

    // Keep the static utility class for backward compatibility if needed
    public static class AcrolinxUtility
    {
        private static readonly Lazy<AcrolinxConfiguration> LazyConfiguration = new Lazy<AcrolinxConfiguration>(() =>
        {
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            return new AcrolinxConfiguration(loggerFactory.CreateLogger<AcrolinxConfiguration>());
        });

        /// <summary>
        /// Gets the current configuration instance.
        /// </summary>
        public static AcrolinxConfiguration GetConfiguration()
        {
            return LazyConfiguration.Value;
        }

        /// <summary>
        /// Retrieves the directory where content files are stored.
        /// Returns null if directory is not configured or doesn't exist.
        /// </summary>
        public static string? GetContentDirectory()
        {
            var configuration = GetConfiguration();
            if (!configuration.IsValid)
            {
                configuration.PrintValidationErrors();
                return null;
            }

            return configuration.ContentDirectory;
        }

        /// <summary>
        /// Submits the given file to Acrolinx for checking and returns the Scorecard URL.
        /// If in Batch mode, it also returns the Content Analysis Dashboard URL.
        /// </summary>
        public static async Task<string?> CheckWithAcrolinx(string filePath, string? batchId = null, CheckType checkType = CheckType.Automated)
        {
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<AcrolinxService>();
            var service = new AcrolinxService(GetConfiguration(), logger, loggerFactory);
            return await service.CheckWithAcrolinx(filePath, batchId, checkType);
        }

        /// <summary>
        /// Opens a given URL in the default web browser.
        /// </summary>
        public static void OpenUrlInBrowser(string? url)
        {
            using var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<AcrolinxService>();
            var service = new AcrolinxService(GetConfiguration(), logger, loggerFactory);
            service.OpenUrlInBrowser(url);
        }
    }
}
