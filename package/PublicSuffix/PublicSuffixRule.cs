using System.Collections.Generic;

namespace DarkPeakLabs.PublicSuffix
{
    internal sealed class PublicSuffixRule(string domain)
    {
        public string Domain { get; set; } = domain;

        public Dictionary<string, PublicSuffixRule> SubDomains { get; set; } = [];

        public bool IsWildCard { get; set; }

        public List<string> WildCardExceptions { get; set; } = [];
    }
}
