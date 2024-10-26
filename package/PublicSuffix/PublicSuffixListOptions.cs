using System;
using System.IO;

namespace DarkPeakLabs.PublicSuffix
{
    public class PublicSuffixListOptions
    {
        private const string AppName = "PublicSuffix";

        public bool DownloadFile { get; set; } = true;

        public Uri DownloadUrl { get; set; } = new Uri("https://publicsuffix.org/list/public_suffix_list.dat");

        public TimeSpan? DownloadTimeout { get; set; }

        public string FilePath { get; set; } = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                AppName,
                "public_suffix_list.dat");

        public TimeSpan UpdateAfter { get; set; } = TimeSpan.FromDays(1);
    }
}
