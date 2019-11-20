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
 
 using Acrolinx.Net.Check;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Acrolinx.Net.Demo
{
    class Program
    {
        private async static Task<string> CheckWithAcrolinx(string url, string genericToken, string username)
        {
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
            Console.WriteLine($"The Acrolinx Scorecard {checkResult.Reports["scorecard"].Link} will open in the default browser...");

            return checkResult.Reports["scorecard"].Link;
        }

        
        static void Main(string[] args)
        {
            try
            {
                var task = CheckWithAcrolinx("https://test-ssl.acrolinx.com",
                    Environment.GetEnvironmentVariable("ACROLINX_API_SSO_TOKEN"),
                    "sdk.net.testuser");

                task.Wait();
                OpenUrlInDefualtBrowser(task.Result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
            }
            Console.WriteLine("Press return to exit.");
            Console.ReadLine();
        }

        private static void OpenUrlInDefualtBrowser(string url)
        {
            if (url.StartsWith("https://"))
            {
                var processStartInfo = new ProcessStartInfo(url)
                {
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
            }
        }
    }
}
