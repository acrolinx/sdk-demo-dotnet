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
using System.Runtime.Serialization;

namespace Acrolinx.Net.Shared.Exceptions
{
    /// <summary>
    /// Exception thrown when an Acrolinx API operation fails.
    /// </summary>
    [Serializable]
    public class AcrolinxApiException : AcrolinxException
    {
        /// <summary>
        /// Gets the HTTP status code associated with the API error, if available.
        /// </summary>
        public int? HttpStatusCode { get; private set; }

        /// <summary>
        /// Gets the API endpoint that was being accessed when the error occurred.
        /// </summary>
        public string? ApiEndpoint { get; private set; }

        /// <summary>
        /// Initializes a new instance of the AcrolinxApiException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="filePath">The file path that was being processed.</param>
        /// <param name="apiEndpoint">The API endpoint that was being accessed.</param>
        /// <param name="httpStatusCode">The HTTP status code.</param>
        /// <param name="isTransient">Whether this is a transient error.</param>
        public AcrolinxApiException(string message, string? filePath = null, string? apiEndpoint = null, int? httpStatusCode = null, bool isTransient = false)
            : base("API_ERROR", message, filePath, isTransient)
        {
            HttpStatusCode = httpStatusCode;
            ApiEndpoint = apiEndpoint;
        }

        /// <summary>
        /// Initializes a new instance of the AcrolinxApiException class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="filePath">The file path that was being processed.</param>
        /// <param name="apiEndpoint">The API endpoint that was being accessed.</param>
        /// <param name="httpStatusCode">The HTTP status code.</param>
        /// <param name="isTransient">Whether this is a transient error.</param>
        public AcrolinxApiException(string message, Exception innerException, string? filePath = null, string? apiEndpoint = null, int? httpStatusCode = null, bool isTransient = false)
            : base("API_ERROR", message, innerException, filePath, isTransient)
        {
            HttpStatusCode = httpStatusCode;
            ApiEndpoint = apiEndpoint;
        }

        /// <summary>
        /// Initializes a new instance of the AcrolinxApiException class with serialized data.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        protected AcrolinxApiException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            HttpStatusCode = info.GetInt32(nameof(HttpStatusCode));
            ApiEndpoint = info.GetString(nameof(ApiEndpoint));
        }

        /// <summary>
        /// Sets the serialization info with the parameter data.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(HttpStatusCode), HttpStatusCode);
            info.AddValue(nameof(ApiEndpoint), ApiEndpoint);
        }

        /// <summary>
        /// Creates a timeout exception for API operations.
        /// </summary>
        /// <param name="filePath">The file path that was being processed.</param>
        /// <param name="apiEndpoint">The API endpoint that timed out.</param>
        /// <returns>A new AcrolinxApiException for timeout scenarios.</returns>
        public static AcrolinxApiException CreateTimeout(string filePath, string? apiEndpoint = null)
        {
            return new AcrolinxApiException(
                "API request timed out. This may be due to network issues or server overload.",
                filePath: filePath,
                apiEndpoint: apiEndpoint,
                httpStatusCode: 408,
                isTransient: true);
        }

        /// <summary>
        /// Creates a rate limit exception for API operations.
        /// </summary>
        /// <param name="filePath">The file path that was being processed.</param>
        /// <param name="apiEndpoint">The API endpoint that returned rate limit error.</param>
        /// <returns>A new AcrolinxApiException for rate limit scenarios.</returns>
        public static AcrolinxApiException CreateRateLimit(string filePath, string? apiEndpoint = null)
        {
            return new AcrolinxApiException(
                "API rate limit exceeded. Please reduce the number of concurrent requests.",
                filePath: filePath,
                apiEndpoint: apiEndpoint,
                httpStatusCode: 429,
                isTransient: true);
        }

        /// <summary>
        /// Creates a server error exception for API operations.
        /// </summary>
        /// <param name="filePath">The file path that was being processed.</param>
        /// <param name="apiEndpoint">The API endpoint that returned server error.</param>
        /// <param name="httpStatusCode">The HTTP status code.</param>
        /// <returns>A new AcrolinxApiException for server error scenarios.</returns>
        public static AcrolinxApiException CreateServerError(string filePath, string? apiEndpoint = null, int httpStatusCode = 500)
        {
            return new AcrolinxApiException(
                $"Server error occurred (HTTP {httpStatusCode}). The service may be temporarily unavailable.",
                filePath: filePath,
                apiEndpoint: apiEndpoint,
                httpStatusCode: httpStatusCode,
                isTransient: true);
        }
    }
} 