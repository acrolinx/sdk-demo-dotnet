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

using System.Collections.Generic;

namespace Acrolinx.Net.Shared
{
    public interface IAcrolinxConfiguration
    {
        string AcrolinxUrl { get; }
        string ApiToken { get; }
        string Username { get; }
        string ClientSignature { get; }
        string ContentDirectory { get; }
        bool IsValid { get; }
        List<string> ValidationErrors { get; }
        
        void ValidateOrThrow();
        void PrintValidationErrors();
    }
} 