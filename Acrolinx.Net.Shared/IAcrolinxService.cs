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
using System.Threading.Tasks;
using Acrolinx.Net.Check;

namespace Acrolinx.Net.Shared
{
    public interface IAcrolinxService : IDisposable
    {
        Task<string?> CheckWithAcrolinx(string filePath, string? batchId = null, CheckType checkType = CheckType.Automated);
        void OpenUrlInBrowser(string? url);
    }
} 