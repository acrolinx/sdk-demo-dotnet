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

namespace Acrolinx.Net.Shared
{
    public static class AcrolinxUtility
    {
        private static readonly string AcrolinxUrl;
        private static readonly string ApiToken;
        private static readonly string AcrolinxUsername;
        private static readonly string ClientSignature;

        // Static constructor: Load configuration from environment variables
        static AcrolinxUtility()
        {
            AcrolinxUrl = Environment.GetEnvironmentVariable("ACROLINX_URL") ?? string.Empty;
            ApiToken = Environment.GetEnvironmentVariable("ACROLINX_SSO_TOKEN") ?? string.Empty;
            AcrolinxUsername = Environment.GetEnvironmentVariable("ACROLINX_USERNAME") ?? string.Empty;
            ClientSignature = Environment.GetEnvironmentVariable("ACROLINX_CLIENT_SIGNATURE") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(AcrolinxUrl) ||
                string.IsNullOrWhiteSpace(ApiToken) ||
                string.IsNullOrWhiteSpace(AcrolinxUsername) ||
                string.IsNullOrWhiteSpace(ClientSignature))
            {
                Console.WriteLine("ERROR: Missing required environment variables. Ensure ACROLINX_URL, ACROLINX_SSO_TOKEN, ACROLINX_USERNAME, and ACROLINX_CLIENT_SIGNATURE are set.");
            }
        }

        /// <summary>
        /// Retrieves the directory where content files are stored.
        /// Ensures the directory exists before returning.
        /// </summary>
        public static string? GetContentDirectory()
        {
            string? contentDir = Environment.GetEnvironmentVariable("ACROLINX_CONTENT_DIR");

            if (string.IsNullOrWhiteSpace(contentDir))
            {
                Console.WriteLine("ERROR: ACROLINX_CONTENT_DIR is not set. Please set this environment variable.");
                return null;
            }

            if (!Directory.Exists(contentDir))
            {
                Console.WriteLine($"ERROR: Specified directory '{contentDir}' does not exist.");
                return null;
            }

            return contentDir;
        }

        /// <summary>
        /// Submits the given file to Acrolinx for checking and returns the Scorecard URL.
        /// If in Batch mode, it also returns the Content Analysis Dashboard URL.
        /// </summary>
        public static async Task<string?> CheckWithAcrolinx(string filePath, string? batchId = null, CheckType checkType = CheckType.Automated)
        {
            if (string.IsNullOrWhiteSpace(AcrolinxUrl) || string.IsNullOrWhiteSpace(ApiToken))
            {
                Console.WriteLine("ERROR: Acrolinx configuration missing. Skipping file check.");
                return null;
            }

            // Validate file existence
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"WARNING: File not found: {filePath}");
                return null;
            }

            string content;
            try
            {
                content = File.ReadAllText(filePath);
                Console.WriteLine($"Successfully read file: {filePath} (Content length: {content.Length} characters)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Could not read file {filePath}. Error: {ex.Message}");
                return null;
            }

            try
            {
                var endpoint = new AcrolinxEndpoint(AcrolinxUrl, ClientSignature);
                Console.WriteLine($"Starting check for file: {filePath}");
                var accessToken = await endpoint.SignInWithSSO(ApiToken, AcrolinxUsername);
                Console.WriteLine($"Successfully signed in for file: {filePath}");

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

                Console.WriteLine($"Sending check request for file: {filePath} (Batch ID: {batchId}, Check Type: {checkType})");
                var checkResult = await endpoint.Check(accessToken, checkRequest);
                Console.WriteLine($"Check request completed for file: {filePath}");

                if (checkResult == null)
                {
                    Console.WriteLine($"WARNING: Check result is null for file: {filePath}");
                    return null;
                }

                Console.WriteLine($"Check {checkResult.Id} completed for {filePath}: Score {checkResult.Quality.Score} ({checkResult.Quality.Status})");

                string? scorecardUrl = checkResult.Reports.ContainsKey("scorecard") ? checkResult.Reports["scorecard"].Link : null;
                Console.WriteLine($"Scorecard: {scorecardUrl ?? "Not Available"}");

                if (checkType == CheckType.Batch && checkResult.Reports.ContainsKey("contentAnalysisDashboard"))
                {
                    string? dashboardUrl = checkResult.Reports["contentAnalysisDashboard"].Link;
                    Console.WriteLine($"Content Analysis Dashboard: {dashboardUrl}");
                    return dashboardUrl;
                }

                return scorecardUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Acrolinx check failed for {filePath}. Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Opens a given URL in the default web browser.
        /// </summary>
        public static void OpenUrlInBrowser(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://"))
            {
                Console.WriteLine("WARNING: Invalid URL. Cannot open in browser.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                Console.WriteLine($"Opening {url} in default browser...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to open browser. {ex.Message}");
            }
        }
    }
}
