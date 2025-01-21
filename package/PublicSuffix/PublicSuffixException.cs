using System;

namespace DarkPeakLabs.PublicSuffix
{
    public class PublicSuffixException : Exception
    {
        public PublicSuffixException()
        {
        }

        public PublicSuffixException(string message) : base(message)
        {
        }

        public PublicSuffixException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}