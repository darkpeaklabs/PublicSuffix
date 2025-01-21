using System;

namespace DarkPeakLabs.PublicSuffix
{
    public class PublicSuffixAcquireLockException : PublicSuffixException
    {
        public PublicSuffixAcquireLockException()
        {
        }

        public PublicSuffixAcquireLockException(string message) : base(message)
        {
        }

        public PublicSuffixAcquireLockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}