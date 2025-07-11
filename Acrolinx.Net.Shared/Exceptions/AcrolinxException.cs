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
    /// Base exception class for all Acrolinx-related exceptions.
    /// </summary>
    [Serializable]
    public abstract class AcrolinxException : Exception
    {
        /// <summary>
        /// Gets the error code associated with this exception.
        /// </summary>
        public string ErrorCode { get; protected set; }

        /// <summary>
        /// Gets additional context information about the error.
        /// </summary>
        public string? Context { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether this exception represents a transient error that might be retried.
        /// </summary>
        public bool IsTransient { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the AcrolinxException class.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="context">Additional context information.</param>
        /// <param name="isTransient">Whether this is a transient error.</param>
        protected AcrolinxException(string errorCode, string message, string? context = null, bool isTransient = false)
            : base(message)
        {
            ErrorCode = errorCode;
            Context = context;
            IsTransient = isTransient;
        }

        /// <summary>
        /// Initializes a new instance of the AcrolinxException class.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="context">Additional context information.</param>
        /// <param name="isTransient">Whether this is a transient error.</param>
        protected AcrolinxException(string errorCode, string message, Exception innerException, string? context = null, bool isTransient = false)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            Context = context;
            IsTransient = isTransient;
        }

        /// <summary>
        /// Initializes a new instance of the AcrolinxException class with serialized data.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        protected AcrolinxException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ErrorCode = info.GetString(nameof(ErrorCode)) ?? string.Empty;
            Context = info.GetString(nameof(Context));
            IsTransient = info.GetBoolean(nameof(IsTransient));
        }

        /// <summary>
        /// Sets the serialization info with the parameter data.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(ErrorCode), ErrorCode);
            info.AddValue(nameof(Context), Context);
            info.AddValue(nameof(IsTransient), IsTransient);
        }

        /// <summary>
        /// Returns a string representation of the exception.
        /// </summary>
        /// <returns>A string representation of the exception.</returns>
        public override string ToString()
        {
            var result = $"[{ErrorCode}] {Message}";
            if (!string.IsNullOrEmpty(Context))
            {
                result += $" (Context: {Context})";
            }
            if (IsTransient)
            {
                result += " [Transient]";
            }
            return result;
        }
    }
} 