using Microsoft.Extensions.Logging;
using System;

namespace DarkPeakLabs.PublicSuffix
{
    internal static partial class PublicSuffixLogMessages
    {
        [LoggerMessage(
            EventId = 1,
            Message = "Downloading data from {Uri}, Attempt: {Attempt}",
            Level = LogLevel.Information)]
        internal static partial void LogDownloadingDataFile(
            this ILogger logger,
            Uri uri,
            int attempt);

        [LoggerMessage(
            EventId = 2,
            Message = "Acquiring file lock {Path}",
            Level = LogLevel.Information)]
        internal static partial void LogAcquiringFileLock(
            this ILogger logger,
            string path);

        [LoggerMessage(
            EventId = 3,
            Message = "File lock {Path} acquired",
            Level = LogLevel.Information)]
        internal static partial void LogFileLockAcquired(
            this ILogger logger,
            string path);

        [LoggerMessage(
            EventId = 4,
            Message = "File lock {Path} released",
            Level = LogLevel.Information)]
        internal static partial void LogFileLockReleased(
            this ILogger logger,
            string path);

        [LoggerMessage(
            EventId = 5,
            Message = "Found existing file {Path}, size {Size}, last modified on {LastModified}",
            Level = LogLevel.Information)]
        internal static partial void LogFoundExistingFile(
            this ILogger logger,
            string path,
            long size,
            DateTime lastModified);

        [LoggerMessage(
            EventId = 6,
            Message = "Data initialized from embedded resource",
            Level = LogLevel.Information)]
                internal static partial void LogDataInitializedFromResource(
            this ILogger logger);

        [LoggerMessage(
            EventId = 7,
            Message = "Data initialized from file {Path}",
            Level = LogLevel.Information)]
        internal static partial void LogDataInitializedFromFile(
            this ILogger logger,
            string path);

        [LoggerMessage(
            EventId = 8,
            Message = "Downloading file from {Url} failed with error: {Error}",
            Level = LogLevel.Error)]
        internal static partial void LogDataFileDownloadFailed(
            this ILogger logger,
            Uri url,
            string error);
    }
}
