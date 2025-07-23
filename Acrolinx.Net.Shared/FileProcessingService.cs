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
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Acrolinx.Net.Shared
{
    /// <summary>
    /// Provides file processing and validation services for Acrolinx integrations.
    /// </summary>
    public class FileProcessingService : IFileProcessingService
    {
        private readonly ILogger<FileProcessingService> _logger;

        /// <summary>
        /// Supported file extensions based on Acrolinx documentation.
        /// </summary>
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // XML-based formats
            "xml", "xhtm", "xhtml", "svg", "resx", "xlf", "xliff", "dita", "ditamap", "ditaval",
            
            // HTML formats
            "html", "htm",
            
            // Markdown formats
            "markdown", "mdown", "mkdn", "mkd", "md",
            
            // Plain text
            "txt",
            
            // Programming languages
            "java",
            "c", "h", "cc", "cpp", "cxx", "c++", "hh", "hpp", "hxx", "h++", "dic",
            
            // Configuration formats
            "yaml", "yml",
            "properties",
            "json"
        };

        /// <summary>
        /// Initializes a new instance of the FileProcessingService class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public FileProcessingService(ILogger<FileProcessingService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates that a file exists and is accessible.
        /// </summary>
        /// <param name="filePath">The path to the file to validate.</param>
        /// <returns>True if the file exists and is accessible, false otherwise.</returns>
        public bool IsFileValid(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.LogWarning("File path is null or empty");
                return false;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File does not exist: {FilePath}", filePath);
                return false;
            }

            try
            {
                // Test if file is accessible by checking its attributes
                var attributes = File.GetAttributes(filePath);
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    _logger.LogWarning("Path is a directory, not a file: {FilePath}", filePath);
                    return false;
                }

                // Test if file is readable by attempting to open it
                try
                {
                    using var stream = File.OpenRead(filePath);
                    // Try to read a small amount to ensure it's truly accessible
                    var buffer = new byte[1];
                    stream.Read(buffer, 0, 1);
                }
                catch (UnauthorizedAccessException)
                {
                    _logger.LogWarning("Access denied to file: {FilePath}", filePath);
                    return false;
                }
                catch (IOException ex) when (ex.HResult == -2147024864) // File is locked or in use
                {
                    _logger.LogWarning("File is locked or in use: {FilePath}", filePath);
                    return false;
                }

                _logger.LogDebug("File is valid and accessible: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("File is not accessible: {FilePath}. Error: {Error}", filePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Determines if a file is supported by Acrolinx based on its extension.
        /// </summary>
        /// <param name="filePath">The path to the file to check.</param>
        /// <returns>True if the file type is supported, false otherwise.</returns>
        public bool IsFileSupported(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _logger.LogWarning("File path is null or empty");
                return false;
            }

            try
            {
                string extension = Path.GetExtension(filePath).TrimStart('.').ToLower();
                bool isSupported = SupportedExtensions.Contains(extension);
                
                if (!isSupported)
                {
                    _logger.LogDebug("File extension '{Extension}' is not supported: {FilePath}", extension, filePath);
                }

                return isSupported;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error checking file extension for {FilePath}: {Error}", filePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Filters a collection of file paths to only include supported file types.
        /// </summary>
        /// <param name="filePaths">The collection of file paths to filter.</param>
        /// <returns>An array of file paths that are supported by Acrolinx.</returns>
        public string[] FilterSupportedFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                _logger.LogWarning("File paths collection is null");
                return Array.Empty<string>();
            }

            var supportedFiles = filePaths.Where(IsFileSupported).ToArray();
            _logger.LogDebug("Filtered {SupportedCount} supported files from {TotalCount} total files", 
                supportedFiles.Length, filePaths.Count());
            
            return supportedFiles;
        }

        /// <summary>
        /// Gets all files in a directory and its subdirectories.
        /// </summary>
        /// <param name="directoryPath">The directory path to search.</param>
        /// <param name="includeSubdirectories">Whether to include subdirectories in the search.</param>
        /// <returns>An array of all file paths found in the directory.</returns>
        public string[] GetAllFiles(string directoryPath, bool includeSubdirectories = true)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                _logger.LogWarning("Directory path is null or empty");
                return Array.Empty<string>();
            }

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
                return Array.Empty<string>();
            }

            try
            {
                var searchOption = includeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.GetFiles(directoryPath, "*", searchOption);
                
                _logger.LogDebug("Found {FileCount} files in directory: {DirectoryPath} (Include subdirectories: {IncludeSubdirectories})", 
                    files.Length, directoryPath, includeSubdirectories);
                
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error getting files from directory {DirectoryPath}: {Error}", directoryPath, ex.Message);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets all supported files in a directory and its subdirectories.
        /// </summary>
        /// <param name="directoryPath">The directory path to search.</param>
        /// <param name="includeSubdirectories">Whether to include subdirectories in the search.</param>
        /// <returns>An array of supported file paths found in the directory.</returns>
        public string[] GetSupportedFiles(string directoryPath, bool includeSubdirectories = true)
        {
            var allFiles = GetAllFiles(directoryPath, includeSubdirectories);
            var supportedFiles = FilterSupportedFiles(allFiles);
            
            _logger.LogInformation("Found {SupportedFileCount} supported files in directory: {DirectoryPath}", 
                supportedFiles.Length, directoryPath);
            
            return supportedFiles;
        }
    }
} 