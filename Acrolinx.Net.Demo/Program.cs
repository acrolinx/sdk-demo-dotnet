/*
 * Copyright 2019-present Acrolinx GmbH
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
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
using System.Threading.Tasks;
using Acrolinx.Net.Check;

namespace Acrolinx.Net.Demo
{
    class Program
    {
        private async static Task<string> CheckWithAcrolinx(string url, string genericToken, string username)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(url), "No Acrolinx URL was provided");
            Debug.Assert(!string.IsNullOrWhiteSpace(genericToken), "No generic SSO token was provided");
            Debug.Assert(!string.IsNullOrWhiteSpace(username), "No username was provided");
            var endpoint = new AcrolinxEndpoint(url, "SW50ZWdyYXRpb25EZXZlbG9wbWVudERlbW9Pbmx5");

            var accessToken = await endpoint.SignInWithSSO(genericToken, username);
            var checkRequest = new CheckRequest()
            {
                CheckOptions = new CheckOptions()
                {
                    CheckType = CheckType.Automated,
                    ContentFormat = "AUTO"
                },
                Document = new DocumentDescriptorRequest(@"c:\demo.net.txt", new System.Collections.Generic.List<CustomField>()),
                Content = "This is an tesst"
            };
            var checkResult = await endpoint.Check(accessToken, checkRequest);

            Console.WriteLine($"Check {checkResult.Id}: {checkResult.Quality.Score} ({checkResult.Quality.Status})");
            Console.WriteLine($"Acrolinx Scorecard: {checkResult.Reports["scorecard"].Link}");

            return checkResult.Reports["scorecard"].Link;
        }

        static void Main(string[] args)
        {
            try
            {
                string genericToken = Environment.GetEnvironmentVariable("ACROLINX_API_SSO_TOKEN");
                var task = CheckWithAcrolinx("https://partner-dev.acrolinx.sh",
                    genericToken,
                    "sdk.net.testuser");

                task.Wait();
                if (args.Length == 0)
                {
                    OpenUrlInDefualtBrowser(task.Result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
            }
        }

        private static void OpenUrlInDefualtBrowser(string url)
        {
            if (url.StartsWith("https://"))
            {
                var processStartInfo = new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                };
                Console.WriteLine($"Opening {url} in the default browser...");
                Process.Start(processStartInfo);
            }
        }
    }
}