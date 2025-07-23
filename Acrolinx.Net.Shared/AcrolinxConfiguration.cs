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
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.Shared
{
    public class AcrolinxConfiguration : IAcrolinxConfiguration
    {
        private readonly ILogger<AcrolinxConfiguration> _logger;

        public string AcrolinxUrl { get; private set; } = string.Empty;
        public string ApiToken { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public string ClientSignature { get; private set; } = string.Empty;
        public string ContentDirectory { get; private set; } = string.Empty;
        public bool IsValid { get; private set; }
        public List<string> ValidationErrors { get; private set; } = new List<string>();

        public AcrolinxConfiguration(ILogger<AcrolinxConfiguration> logger)
        {
            _logger = logger;
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            ValidationErrors.Clear();

            // Load required configuration values
            AcrolinxUrl = GetEnvironmentVariable("ACROLINX_URL", "Acrolinx URL");
            ApiToken = GetEnvironmentVariable("ACROLINX_SSO_TOKEN", "SSO Token");
            Username = GetEnvironmentVariable("ACROLINX_USERNAME", "Username");
            ClientSignature = GetEnvironmentVariable("ACROLINX_CLIENT_SIGNATURE", "Client Signature");
            ContentDirectory = GetEnvironmentVariable("ACROLINX_CONTENT_DIR", "Content Directory");

            // Validate content directory exists
            if (!string.IsNullOrWhiteSpace(ContentDirectory) && !Directory.Exists(ContentDirectory))
            {
                ValidationErrors.Add($"Content directory does not exist: {ContentDirectory}");
            }

            // Validate URL format and check for template placeholders
            if (!string.IsNullOrWhiteSpace(AcrolinxUrl))
            {
                if (AcrolinxUrl.Contains("{") && AcrolinxUrl.Contains("}"))
                {
                    ValidationErrors.Add($"Acrolinx URL contains template placeholder: {AcrolinxUrl}. Please replace with actual URL.");
                }
                else if (!Uri.IsWellFormedUriString(AcrolinxUrl, UriKind.Absolute))
                {
                    ValidationErrors.Add($"Invalid URL format: {AcrolinxUrl}");
                }
            }

            // Check for other template placeholders
            if (!string.IsNullOrWhiteSpace(ApiToken) && (ApiToken.Contains("ACROLINX-SECURELY-PROVISIONED") || ApiToken.Contains("ACROLINX-PROVISIONED")))
            {
                ValidationErrors.Add($"SSO Token contains placeholder value: {ApiToken}. Please replace with actual token.");
            }

            if (!string.IsNullOrWhiteSpace(ClientSignature) && (ClientSignature.Contains("ACROLINX-PROVISIONED") || ClientSignature.Contains("ACROLINX-SECURELY-PROVISIONED")))
            {
                ValidationErrors.Add($"Client Signature contains placeholder value: {ClientSignature}. Please replace with actual signature.");
            }

            if (!string.IsNullOrWhiteSpace(Username) && Username.Contains("myacrolinx-username"))
            {
                ValidationErrors.Add($"Username contains placeholder value: {Username}. Please replace with actual username.");
            }

            IsValid = ValidationErrors.Count == 0;

            if (IsValid)
            {
                _logger.LogInformation("Configuration loaded successfully");
            }
            else
            {
                _logger.LogError("Configuration validation failed with {ErrorCount} errors", ValidationErrors.Count);
            }
        }

        private string GetEnvironmentVariable(string variableName, string displayName)
        {
            string? value = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(value))
            {
                ValidationErrors.Add($"Missing required environment variable: {variableName} ({displayName})");
                return string.Empty;
            }
            return value;
        }

        public void ValidateOrThrow()
        {
            if (!IsValid)
            {
                string errorMessage = "Configuration validation failed:\n" + string.Join("\n", ValidationErrors);
                _logger.LogCritical("Configuration validation failed: {ErrorMessage}", errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }

        public void PrintValidationErrors()
        {
            if (ValidationErrors.Count > 0)
            {
                _logger.LogError("Configuration Errors:");
                foreach (var error in ValidationErrors)
                {
                    _logger.LogError("  - {Error}", error);
                }
            }
        }
    }
} 