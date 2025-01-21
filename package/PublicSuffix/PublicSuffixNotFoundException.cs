using System;

namespace DarkPeakLabs.PublicSuffix
{
    [Serializable]
    public class PublicSuffixNotFoundException : PublicSuffixAcquireLockException
    {
        public PublicSuffixNotFoundException()
        {
        }

        public PublicSuffixNotFoundException(string message) : base(message)
        {
        }

        public PublicSuffixNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}