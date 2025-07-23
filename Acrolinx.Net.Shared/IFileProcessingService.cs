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
    /// <summary>
    /// Provides file processing and validation services for Acrolinx integrations.
    /// </summary>
    public interface IFileProcessingService
    {
        /// <summary>
        /// Validates that a file exists and is accessible.
        /// </summary>
        /// <param name="filePath">The path to the file to validate.</param>
        /// <returns>True if the file exists and is accessible, false otherwise.</returns>
        bool IsFileValid(string filePath);

        /// <summary>
        /// Determines if a file is supported by Acrolinx based on its extension.
        /// </summary>
        /// <param name="filePath">The path to the file to check.</param>
        /// <returns>True if the file type is supported, false otherwise.</returns>
        bool IsFileSupported(string filePath);

        /// <summary>
        /// Filters a collection of file paths to only include supported file types.
        /// </summary>
        /// <param name="filePaths">The collection of file paths to filter.</param>
        /// <returns>An array of file paths that are supported by Acrolinx.</returns>
        string[] FilterSupportedFiles(IEnumerable<string> filePaths);

        /// <summary>
        /// Gets all files in a directory and its subdirectories.
        /// </summary>
        /// <param name="directoryPath">The directory path to search.</param>
        /// <param name="includeSubdirectories">Whether to include subdirectories in the search.</param>
        /// <returns>An array of all file paths found in the directory.</returns>
        string[] GetAllFiles(string directoryPath, bool includeSubdirectories = true);

        /// <summary>
        /// Gets all supported files in a directory and its subdirectories.
        /// </summary>
        /// <param name="directoryPath">The directory path to search.</param>
        /// <param name="includeSubdirectories">Whether to include subdirectories in the search.</param>
        /// <returns>An array of supported file paths found in the directory.</returns>
        string[] GetSupportedFiles(string directoryPath, bool includeSubdirectories = true);
    }
} 