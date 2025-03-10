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

#nullable enable

using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Acrolinx.Net.Shared;
using Acrolinx.Net.Check;

namespace Acrolinx.Net.AutoCheck
{
    class AutomatedCheckProgram
    {
        static void Main()
        {
            string? watchPath = Environment.GetEnvironmentVariable("ACROLINX_CONTENT_DIR");
            if (string.IsNullOrEmpty(watchPath) || !Directory.Exists(watchPath))
            {
                Console.WriteLine("ERROR: Please set ACROLINX_CONTENT_DIR to a valid directory.");
                return;
            }

            FileSystemWatcher watcher = new FileSystemWatcher(watchPath);
            watcher.IncludeSubdirectories = false;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.Created += OnFileChanged; // Trigger check when new files are added
            watcher.Changed += OnFileChanged; // Trigger check when files are modified
            watcher.Renamed += OnFileRenamed; // Trigger check when files are renamed

            watcher.EnableRaisingEvents = true;
            Console.WriteLine($"[AutoCheck] Watching '{watchPath}' for file changes...");
            Console.WriteLine("Press 'p' to pause, 'r' to resume, or Ctrl+C to quit.");

            bool exitRequested = false;
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                exitRequested = true;
                Console.WriteLine("\nStopping monitoring...");
            };

            while (!exitRequested)
            {
                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(100);
                    continue;
                }
                char key = Console.ReadKey(intercept: true).KeyChar;
                if (key == 'p' || key == 'P')
                {
                    watcher.EnableRaisingEvents = false;
                    Console.WriteLine("[AutoCheck] Monitoring PAUSED.");
                }
                else if (key == 'r' || key == 'R')
                {
                    watcher.EnableRaisingEvents = true;
                    Console.WriteLine("[AutoCheck] Monitoring RESUMED.");
                }
            }

            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            Console.WriteLine("Monitoring stopped. Exiting program.");
        }

        private static async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(e.FullPath)) return; // Ensure file exists
            Console.WriteLine($"[AutoCheck] File {e.ChangeType}: {e.FullPath}");

            // Perform Automated Acrolinx Check
            string? scorecardUrl = await AcrolinxUtility.CheckWithAcrolinx(e.FullPath, checkType: CheckType.Automated);
            if (!string.IsNullOrEmpty(scorecardUrl))
            {
                OpenScorecard(scorecardUrl);
            }
        }

        private static async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (!File.Exists(e.FullPath)) return;
            Console.WriteLine($"[AutoCheck] File Renamed: {e.OldFullPath} -> {e.FullPath}");

            // Treat rename as a new file check
            string? scorecardUrl = await AcrolinxUtility.CheckWithAcrolinx(e.FullPath, checkType: CheckType.Automated);
            if (!string.IsNullOrEmpty(scorecardUrl))
            {
                OpenScorecard(scorecardUrl);
            }
        }

        private static void OpenScorecard(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                Console.WriteLine($"[AutoCheck] Opened scorecard: {url}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to open scorecard URL: {ex.Message}");
            }
        }
    }
}
