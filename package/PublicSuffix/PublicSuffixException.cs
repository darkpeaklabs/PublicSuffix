using System;

namespace DarkPeakLabs.PublicSuffix
{
    [Serializable]
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