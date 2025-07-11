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
using System.Runtime.Serialization;

namespace Acrolinx.Net.Shared.Exceptions
{
    /// <summary>
    /// Exception thrown when configuration validation fails.
    /// </summary>
    [Serializable]
    public class AcrolinxConfigurationException : AcrolinxException
    {
        /// <summary>
        /// Gets the configuration key that failed validation.
        /// </summary>
        public string? ConfigurationKey { get; private set; }

        /// <summary>
        /// Gets the list of validation errors.
        /// </summary>
        public List<string> ValidationErrors { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AcrolinxConfigurationException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="configurationKey">The configuration key that failed.</param>
        /// <param name="validationErrors">List of validation errors.</param>
        public AcrolinxConfigurationException(string message, string? configurationKey = null, List<string>? validationErrors = null)
            : base("CONFIG_ERROR", message, configurationKey, isTransient: false)
        {
            ConfigurationKey = configurationKey;
            ValidationErrors = validationErrors ?? new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the AcrolinxConfigurationException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="configurationKey">The configuration key that failed.</param>
        /// <param name="validationErrors">List of validation errors.</param>
        public AcrolinxConfigurationException(string message, Exception innerException, string? configurationKey = null, List<string>? validationErrors = null)
            : base("CONFIG_ERROR", message, innerException, configurationKey, isTransient: false)
        {
            ConfigurationKey = configurationKey;
            ValidationErrors = validationErrors ?? new List<string>();
        }

        /// <summary>
        /// Initializes a new instance of the AcrolinxConfigurationException class with serialized data.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        protected AcrolinxConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ConfigurationKey = info.GetString(nameof(ConfigurationKey));
            ValidationErrors = (List<string>)info.GetValue(nameof(ValidationErrors), typeof(List<string>))!;
        }

        /// <summary>
        /// Sets the serialization info with the parameter data.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ConfigurationKey), ConfigurationKey);
            info.AddValue(nameof(ValidationErrors), ValidationErrors);
        }

        /// <summary>
        /// Creates a missing configuration exception.
        /// </summary>
        /// <param name="configurationKey">The missing configuration key.</param>
        /// <returns>A new AcrolinxConfigurationException for missing configuration.</returns>
        public static AcrolinxConfigurationException CreateMissingConfiguration(string configurationKey)
        {
            return new AcrolinxConfigurationException(
                $"Required configuration '{configurationKey}' is missing or empty.",
                configurationKey: configurationKey,
                validationErrors: new List<string> { $"Missing configuration: {configurationKey}" });
        }

        /// <summary>
        /// Creates an invalid configuration exception.
        /// </summary>
        /// <param name="configurationKey">The invalid configuration key.</param>
        /// <param name="currentValue">The current invalid value.</param>
        /// <param name="expectedFormat">The expected format.</param>
        /// <returns>A new AcrolinxConfigurationException for invalid configuration.</returns>
        public static AcrolinxConfigurationException CreateInvalidConfiguration(string configurationKey, string currentValue, string expectedFormat)
        {
            return new AcrolinxConfigurationException(
                $"Configuration '{configurationKey}' has invalid value '{currentValue}'. Expected format: {expectedFormat}.",
                configurationKey: configurationKey,
                validationErrors: new List<string> { $"Invalid configuration: {configurationKey} = {currentValue}" });
        }
    }
} 