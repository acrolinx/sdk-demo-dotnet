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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Acrolinx.Net.Shared.Exceptions;

namespace Acrolinx.Net.Shared
{
    /// <summary>
    /// Provides retry functionality with exponential backoff for transient errors.
    /// </summary>
    public class RetryPolicy
    {
        private readonly ILogger<RetryPolicy> _logger;
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;
        private readonly TimeSpan _maxDelay;
        private readonly double _backoffMultiplier;

        /// <summary>
        /// Initializes a new instance of the RetryPolicy class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="maxRetries">Maximum number of retry attempts.</param>
        /// <param name="baseDelay">Base delay between retries.</param>
        /// <param name="maxDelay">Maximum delay between retries.</param>
        /// <param name="backoffMultiplier">Multiplier for exponential backoff.</param>
        public RetryPolicy(ILogger<RetryPolicy> logger, int maxRetries = 3, TimeSpan? baseDelay = null, TimeSpan? maxDelay = null, double backoffMultiplier = 2.0)
        {
            _logger = logger;
            _maxRetries = maxRetries;
            _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
            _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
            _backoffMultiplier = backoffMultiplier;
        }

        /// <summary>
        /// Executes an operation with retry logic for transient errors.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="operationName">The name of the operation for logging.</param>
        /// <param name="context">Additional context for logging.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        public async Task<T> ExecuteAsync<T>(
            Func<Task<T>> operation, 
            string operationName, 
            string? context = null, 
            CancellationToken cancellationToken = default)
        {
            var attempt = 0;
            Exception? lastException = null;

            while (attempt <= _maxRetries)
            {
                try
                {
                    if (attempt > 0)
                    {
                        _logger.LogInformation("Retrying {OperationName} (attempt {Attempt}/{MaxRetries}). Context: {Context}", 
                            operationName, attempt, _maxRetries, context);
                    }

                    var result = await operation();
                    
                    if (attempt > 0)
                    {
                        _logger.LogInformation("Successfully completed {OperationName} after {Attempt} retries. Context: {Context}", 
                            operationName, attempt, context);
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    // Check if the exception is transient and retryable
                    var isTransient = IsTransientError(ex);
                    
                    if (!isTransient || attempt >= _maxRetries)
                    {
                        if (!isTransient)
                        {
                            _logger.LogError(ex, "Non-transient error in {OperationName}. Context: {Context}", operationName, context);
                        }
                        else
                        {
                            _logger.LogError(ex, "Maximum retries ({MaxRetries}) exceeded for {OperationName}. Context: {Context}", 
                                _maxRetries, operationName, context);
                        }
                        throw;
                    }

                    // Calculate delay with exponential backoff
                    var delay = CalculateDelay(attempt);
                    
                    _logger.LogWarning(ex, "Transient error in {OperationName} (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms. Context: {Context}", 
                        operationName, attempt, _maxRetries, delay.TotalMilliseconds, context);

                    await Task.Delay(delay, cancellationToken);
                    attempt++;
                }
            }

            // This should never be reached due to the logic above, but just in case
            throw lastException ?? new InvalidOperationException("Retry operation failed unexpectedly");
        }

        /// <summary>
        /// Executes an operation with retry logic for transient errors (void return).
        /// </summary>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="operationName">The name of the operation for logging.</param>
        /// <param name="context">Additional context for logging.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task ExecuteAsync(
            Func<Task> operation, 
            string operationName, 
            string? context = null, 
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(async () =>
            {
                await operation();
                return true; // Return dummy value for void operations
            }, operationName, context, cancellationToken);
        }

        /// <summary>
        /// Determines if an exception represents a transient error that can be retried.
        /// </summary>
        /// <param name="exception">The exception to check.</param>
        /// <returns>True if the error is transient and should be retried.</returns>
        private bool IsTransientError(Exception exception)
        {
            // Check for our custom Acrolinx exceptions
            if (exception is AcrolinxException acrolinxException)
            {
                return acrolinxException.IsTransient;
            }

            // Check for common transient .NET exceptions
            return exception is TaskCanceledException ||
                   exception is TimeoutException ||
                   exception is System.Net.Http.HttpRequestException ||
                   exception is System.Net.Sockets.SocketException ||
                   exception is System.IO.IOException;
        }

        /// <summary>
        /// Calculates the delay for the next retry attempt using exponential backoff.
        /// </summary>
        /// <param name="attempt">The current attempt number (0-based).</param>
        /// <returns>The delay to wait before the next retry.</returns>
        private TimeSpan CalculateDelay(int attempt)
        {
            // Calculate exponential backoff: baseDelay * (backoffMultiplier ^ attempt)
            var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(_backoffMultiplier, attempt));
            
            // Add jitter to prevent thundering herd
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.1)));
            delay = delay.Add(jitter);
            
            // Cap at maximum delay
            if (delay > _maxDelay)
            {
                delay = _maxDelay;
            }

            return delay;
        }

        /// <summary>
        /// Creates a default retry policy for API operations.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <returns>A RetryPolicy configured for API operations.</returns>
        public static RetryPolicy CreateDefault(ILogger<RetryPolicy> logger)
        {
            return new RetryPolicy(
                logger: logger,
                maxRetries: 3,
                baseDelay: TimeSpan.FromSeconds(1),
                maxDelay: TimeSpan.FromSeconds(30),
                backoffMultiplier: 2.0);
        }

        /// <summary>
        /// Creates a retry policy for file operations.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <returns>A RetryPolicy configured for file operations.</returns>
        public static RetryPolicy CreateForFileOperations(ILogger<RetryPolicy> logger)
        {
            return new RetryPolicy(
                logger: logger,
                maxRetries: 2,
                baseDelay: TimeSpan.FromMilliseconds(500),
                maxDelay: TimeSpan.FromSeconds(5),
                backoffMultiplier: 1.5);
        }
    }
} 