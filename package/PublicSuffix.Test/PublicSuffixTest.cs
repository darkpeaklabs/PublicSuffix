
using Microsoft.Extensions.Logging;

namespace DarkPeakLabs.PublicSuffix.Test
{
    public class PublicSuffixTest : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<PublicSuffixTest> _logger;

        public PublicSuffixTest()
        {
            _loggerFactory = LoggerFactory.Create((builder) =>
            {
                builder
                    .AddDebug()
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            _logger = _loggerFactory.CreateLogger<PublicSuffixTest>();
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

        [Fact]
        public void TestDownloadInitialization()
        {
            var options = new PublicSuffixListOptions() 
            {
                DownloadFile = true,
                UpdateAfter = TimeSpan.FromSeconds(30),
            };

            ClearData(options);

            PublicSuffixList list = new(options, _loggerFactory);
            int count = list.Count;

            Assert.True(count > 0);
            Assert.True(File.Exists(options.FilePath));

            var lastWriteTime = File.GetLastWriteTimeUtc(options.FilePath);

            list = new(options, _loggerFactory);
            Assert.Equal(count, list.Count);
            Assert.Equal(lastWriteTime, File.GetLastWriteTimeUtc(options.FilePath));

            Thread.Sleep(options.UpdateAfter + TimeSpan.FromSeconds(3));

            list = new(options, _loggerFactory);
            Assert.Equal(count, list.Count);
            Assert.True(lastWriteTime < File.GetLastWriteTimeUtc(options.FilePath));
        }

        [Fact]
        public void TestResourceInitialization()
        {
            var options = new PublicSuffixListOptions()
            {
                DownloadFile = false,
                UpdateAfter = TimeSpan.FromSeconds(30)
            };

            ClearData(options);

            PublicSuffixList list = new(options, _loggerFactory);
            int count = list.Count;

            Assert.True(count > 0);
            Assert.True(File.Exists(options.FilePath));
            var lastWriteTime = File.GetLastWriteTimeUtc(options.FilePath);

            Thread.Sleep(options.UpdateAfter + TimeSpan.FromSeconds(3));

            list = new(options, _loggerFactory);
            Assert.Equal(count, list.Count);
            Assert.True(lastWriteTime == File.GetLastWriteTimeUtc(options.FilePath));
        }

        [Fact]
        public void TestDownloadError()
        {
            var options = new PublicSuffixListOptions()
            {
                DownloadFile = true,
                DownloadUrl = new Uri($"https://{Guid.NewGuid()}.test/data.txt"),
                DownloadTimeout = TimeSpan.FromSeconds(5)
            };

            ClearData(options);

            Assert.Throws<PublicSuffixListDownloadException>(() => 
            {
                PublicSuffixList list = new(options, _loggerFactory);
                int count = list.Count;
            });
        }

        [Fact]
        public void TestGetDomainApex()
        {
            PublicSuffixList list = new(_loggerFactory);

            foreach (var suffix in list) 
            {
                _logger.LogInformation("Testing suffix {Suffix}", suffix);

                if (suffix[0] == '*')
                {
                    Assert.True(suffix.Length > 1 && suffix[1] == '.');

                    var apex = $"apex.any.{suffix[2..]}";

                    Assert.Equal(apex, list.GetDomainApex(apex));
                    Assert.Equal(apex, list.GetDomainApex($"subdomain.{apex}"));
                    Assert.Equal(apex, list.GetDomainApex($"sub1.sub2.{apex}"));
                }
                else if (suffix[0] == '!')
                {
                    var apex = suffix[1..];

                    Assert.Equal(apex, list.GetDomainApex(apex));
                    //Assert.Equal(apex, list.GetDomainApex($"subdomain.{apex}"));
                    //Assert.Equal(apex, list.GetDomainApex($"sub1.sub2.{apex}"));
                }
                else if (!char.IsLetterOrDigit(suffix[0]))
                {
                    _logger.LogWarning("Suffix {Suffix} does not start with a letter", suffix);
                }
                else
                {
                    var apex = $"apex.{suffix}";

                    Assert.Equal(apex, list.GetDomainApex(apex));
                    Assert.Equal(apex, list.GetDomainApex($"subdomain.{apex}"));
                    Assert.Equal(apex, list.GetDomainApex($"sub1.sub2.{apex}"));
                }
            }
        }

        private void ClearData(PublicSuffixListOptions options)
        {
            var appPath = Path.GetDirectoryName(options.FilePath);

            if (File.Exists(options.FilePath))
            {
                File.Delete(options.FilePath);
            }

            if (Directory.Exists(appPath))
            {
                Directory.Delete(appPath, true);
            }
        }
    }
}