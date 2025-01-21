using System;

namespace DarkPeakLabs.PublicSuffix
{
    [Serializable]
    public class PublicSuffixListDownloadException : PublicSuffixAcquireLockException
    {
        public PublicSuffixListDownloadException()
        {
        }

        public PublicSuffixListDownloadException(string message) : base(message)
        {
        }

        public PublicSuffixListDownloadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}