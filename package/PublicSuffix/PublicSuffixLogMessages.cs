using Microsoft.Extensions.Logging;
using System;

namespace DarkPeakLabs.PublicSuffix
{
    internal static partial class PublicSuffixLogMessages
    {
        [LoggerMessage(
            Message = "Downloading data from {Uri}, Attempt: {Attempt}",
            Level = LogLevel.Information)]
        internal static partial void LogDownloadingDataFile(
            this ILogger logger,
            Uri uri,
            int attempt);
    }
}
