#nullable enable

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
using System.Collections.Generic;
using System.Linq;
using Acrolinx.Net.Check;
using Acrolinx.Net.Shared;

namespace Acrolinx.Net.Demo
{
    class Program
    {
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

        static async Task Main(string[] args)
        {
            try
            {
                // Get the Acrolinx API configuration from environment variables
                string? acrolinxUrl = Environment.GetEnvironmentVariable("ACROLINX_URL");
                string? genericToken = Environment.GetEnvironmentVariable("ACROLINX_SSO_TOKEN");
                string? acrolinxUsername = Environment.GetEnvironmentVariable("ACROLINX_USERNAME");

                Debug.Assert(!string.IsNullOrWhiteSpace(acrolinxUrl), "No Acrolinx URL provided");
                Debug.Assert(!string.IsNullOrWhiteSpace(genericToken), "No generic SSO token provided");
                Debug.Assert(!string.IsNullOrWhiteSpace(acrolinxUsername), "No username provided");

                // Prompt user for Batch ID
                Console.Write("Enter a Batch ID (or press Enter for default): ");
                string? batchId = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    batchId = $"batch-{DateTime.UtcNow:yyyyMMdd-HHmmss}"; // Default to timestamp-based ID
                    Console.WriteLine($"Using default Batch ID: {batchId}");
                }
                else
                {
                    Console.WriteLine($"Using provided Batch ID: {batchId}");
                }

                // Get content directory from AcrolinxUtility
                string? directoryPath = AcrolinxUtility.GetContentDirectory();
                if (string.IsNullOrEmpty(directoryPath)) return;

                // Get all files in the directory and subdirectories
                string[] allFiles = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);

                // Filter files to only include supported formats
                string[] contentFiles = allFiles
                    .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).TrimStart('.').ToLower()))
                    .ToArray();

                if (contentFiles.Length == 0)
                {
                    Console.WriteLine($"No supported files found in {directoryPath}.");
                    return;
                }

                List<Task<string?>> checkTasks = new List<Task<string?>>();
                string? contentAnalysisDashboardLink = null;

                // Process each content file
                foreach (string file in contentFiles)
                {
                    Console.WriteLine($"Processing file: {file}");
                    checkTasks.Add(AcrolinxUtility.CheckWithAcrolinx(file, batchId, CheckType.Batch));
                }

                // Await all check tasks
                var results = await Task.WhenAll(checkTasks);

                // Find the first valid Content Analysis Dashboard link
                contentAnalysisDashboardLink = results.FirstOrDefault(result => !string.IsNullOrWhiteSpace(result));

                // Print the Content Analysis Dashboard link once at the end
                Console.WriteLine("\n===== Batch Check Summary =====");
                Console.WriteLine($"Batch ID: {batchId}");
                if (!string.IsNullOrWhiteSpace(contentAnalysisDashboardLink))
                {
                    Console.WriteLine($"Content Analysis Dashboard (Batch Report): {contentAnalysisDashboardLink}");
                }
                else
                {
                    Console.WriteLine("No Content Analysis Dashboard report was generated.");
                }

                // Open the first Scorecard URL in the browser (optional)
                if (!string.IsNullOrWhiteSpace(contentAnalysisDashboardLink))
                {
                    AcrolinxUtility.OpenUrlInBrowser(contentAnalysisDashboardLink);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }
    }
}
